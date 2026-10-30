using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Google.XR.ARCoreExtensions;
using Google.XR.ARCoreExtensions.GeospatialCreator;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.SceneManagement;

public class DataManager : MonoBehaviour, IPlaceCacheProvider, IPlaceCacheLoadTime
{
    // 개발용 진단 로그 — WOOPANG_DEBUG 가 정의된 빌드에서만 호출이 남는다 (출시 빌드에서는 호출 자체가 빠짐)
    [System.Diagnostics.Conditional("WOOPANG_DEBUG")]
    private static void DbgLog(object message) => UnityEngine.Debug.Log(message);

    /// <summary>
    /// 위치 권한이 허용되었을 때 발행되는 이벤트
    /// TourAPIManager, TerminalManager, TrainStationManager, P2PManager가 구독하여 데이터 로드 시작
    /// </summary>
    public static event System.Action OnLocationPermissionGranted;

    /// <summary>
    /// GPS 기반 선행 데이터 로드 완료 시 발행 (LoadingManager가 구독하여 fallback 활성화)
    /// Geospatial 대기 전에 발행됨
    /// </summary>
    public static event System.Action OnPreFetchCompleted;

    // Singleton pattern
    private static DataManager instance;
    public static DataManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<DataManager>();
                if (instance == null)
                {
                    Debug.LogError("[DataManager] Instance not found in scene!");
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        // Singleton 체크
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

    }

    private string baseServerUrl = ApiConfig.LOCATIONS + "?status=approved";
    
    [Header("UI")]
    [Tooltip("오브젝트 개수 표시 UI")]
    public ObjectCountUI objectCountUI;

    // [Tooltip("PlaceList 매니저 (Tier별 업데이트용)")]
    // public PlaceListManager placeListManager; // 삭제

    [Header("Prefabs")]
    public GameObject cubePrefab;
    public GameObject glbPrefab;

    [Header("GLB Settings")]
    [SerializeField] private int maxConcurrentGLBLoads = 3;
    [SerializeField] private float glbLoadTimeout = 30f;
    [SerializeField] private bool fallbackToCube = true;
    
    private Dictionary<int, GameObject> spawnedObjects = new Dictionary<int, GameObject>();
    private Dictionary<int, PlaceData> placeDataMap = new Dictionary<int, PlaceData>();
    private Queue<GameObject> cubeObjectPool = new Queue<GameObject>();
    private Queue<GameObject> glbObjectPool = new Queue<GameObject>();
    private HashSet<int> currentlyLoadingGLB = new HashSet<int>();
    
    [SerializeField] public int poolSize = 100;       // 호환용 (다른 곳에서 참조 — 이전 단일 풀 사이즈)
    [Header("Object Pool Sizes")]
    [Tooltip("Cube 프리팹 풀 사이즈")]
    [SerializeField] public int cubePoolSize = 100;
    [Tooltip("GLB 프리팹 풀 사이즈 (메모리 비용 큼 — cube보다 적게 권장)")]
    [SerializeField] public int glbPoolSize = 50;

    [Header("Progressive Loading Settings")]
    [Tooltip("거리별 로딩 단계 (미터)")]
    public float[] loadRadii = new float[] { 25f, 50f, 75f, 100f, 150f, 200f, 500f, 1000f, 2000f, 5000f, 10000f };

    [Tooltip("각 거리 단계 사이의 딜레이 (초)")]
    public float tierDelay = 0.5f;

    [Tooltip("같은 단계 내 오브젝트 사이의 딜레이 (초)")]
    public float objectSpawnDelay = 0.2f;

    [SerializeField] private float updateDistanceThreshold = 50f;

    [Header("IndicatorOnly (FilterManager 배분 전용)")]
    [Tooltip("IndicatorOnly 프리팹 (Target 컴포넌트만 가진 경량 오브젝트) — FilterManager가 중앙 배분")]
    [SerializeField] private GameObject indicatorOnlyPrefab;
    private Dictionary<int, GameObject> indicatorOnlyObjects = new Dictionary<int, GameObject>();
    private Queue<GameObject> indicatorOnlyPool = new Queue<GameObject>(20);

    // ============================================================
    // Light Cache 시스템 (FilterManager 중앙 배분용)
    // ============================================================
    private List<CachedPlaceData> lightCache = new List<CachedPlaceData>();
    private bool isCacheReady = false;
    public event System.Action CacheBecameReady;
    private bool cacheReadyEventFired = false;
    private void MarkCacheReady()
    {
        isCacheReady = true;
        LastLoadedAt = Time.realtimeSinceStartup;
        if (cacheReadyEventFired) return;
        cacheReadyEventFired = true;
        CacheBecameReady?.Invoke();
    }
    private HashSet<int> pendingDetailFetchIds = new HashSet<int>();
    private Coroutine batchDetailCoroutine;

    [Header("주변 장소 목록 요청")]
    [Tooltip("장소 목록 요청 제한 시간 (초) — 응답이 없으면 실패로 끝내야 FilterManager 가 다시 요청한다")]
    [SerializeField] private int lightCacheTimeout = 15;
    private float lightCacheBusyUntil;   // 이 시각 전까지는 목록 요청이 진행 중 (RefreshCache 중복 요청 방지)
    private Vector2 lightCacheBusyPos;   // 진행 중인 요청의 위치
    private int lightCacheRequestSeq;

    [Header("위치 권한 안내")]
    [Tooltip("안드로이드: 시작 후 이 시간(초)이 지나고, 권한 창이 닫힌 채로 위치 권한이 없으면 바로 안내 패널을 띄운다")]
    public float androidLocationDeniedGrace = 3f;
    [Tooltip("iOS: 위치 권한 창에 답할 시간 (초, 창이 떠 있는 동안은 세지 않는다) — 지나도 위치가 꺼져 있으면 안내 패널")]
    public float iosLocationPromptGrace = 5f;
    [Tooltip("위치가 꺼져 있을 때 안내 패널을 띄우기 전 최대 대기 시간 (초)")]
    public float locationPermissionMaxWait = 30f;

    [Header("AR 준비 상태 가이드")]
    [SerializeField] private int arGuideFontSize = 22;

    private bool isDataLoaded = false;
    private bool isGeospatialReady = false;
    private bool isFetching = false; // FetchDataProgressively 중복 실행 방지
    private int fetchGeneration = 0; // 세대 번호: StopAllFetching 시 증가하여 이전 코루틴 무효화
    private Coroutine fetchCoroutine;
    private Coroutine checkPositionCoroutine;
    private Vector2 lastPosition;
    private bool isInitialStartComplete = false; // 앱 첫 시작 완료 여부 (OnApplicationFocus 무시용)

    // 현재 활성 필터 저장 (거리 필터와 동기화용)
    private Dictionary<string, bool> currentFilters;
    private string currentCategoryFilter = ""; // 카테고리 필터 ("" = 전체, "shop"/"food"/"cafe"/"park")

    void OnEnable()
    {
        ARSession.stateChanged += OnARSessionStateChanged;
    }

    void OnDisable()
    {
        ARSession.stateChanged -= OnARSessionStateChanged;
    }

    /// <summary>
    /// 모든 fetch 관련 코루틴을 중단하고 세대 번호를 증가시켜 이전 코루틴 무효화
    /// </summary>
    private void StopAllFetching()
    {
        fetchGeneration++; // 세대 증가 → 이전 FetchDataProgressively가 yield 후 자동 중단


        if (fetchCoroutine != null)
        {
            StopCoroutine(fetchCoroutine);
            fetchCoroutine = null;
        }
        if (checkPositionCoroutine != null)
        {
            StopCoroutine(checkPositionCoroutine);
            checkPositionCoroutine = null;
        }
        isFetching = false;
    }

    void Start()
    {
        // [FIX] 데이터 로드 전 저장된 필터 설정을 미리 로드하여 초기화
        LoadInitialFilters();

        // 앱 시작 시 "찾고 있습니다" 즉시 표시
        if (objectCountUI != null)
        {
            objectCountUI.ResetUI();
        }

        StartCoroutine(InitializeObjectPoolsAsync());
        StartCoroutine(StartLocationServiceAndFetchData());

        // FilterManager에 캐시 프로바이더 등록 (비활성 오브젝트도 탐색)
        FilterManager filterMgr = FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null)
            filterMgr.RegisterCacheProvider(this);

        // 첫 설치 대비: 초기 로드가 완료되지 않은 경우 10/15/20초에 자동 재시도
        // 에디터에서는 AR 세션이 없으므로 재시도 불필요 (기존 코루틴을 강제 중단하는 부작용 방지)
#if !UNITY_EDITOR
        StartCoroutine(FirstInstallRetryIfNotLoaded());
#endif
    }

    /// <summary>
    /// 첫 설치 시 AR 세션 타이밍 문제로 초기 로드 실패하는 경우 자동 재시도
    /// isInitialStartComplete가 true가 되면 즉시 종료
    /// </summary>
    private IEnumerator FirstInstallRetryIfNotLoaded()
    {
        int[] retryDelays = new int[] { 10, 5, 5 }; // 10초, 15초, 20초 (누적)
        foreach (int delay in retryDelays)
        {
            yield return new WaitForSeconds(delay);
            if (isInitialStartComplete) yield break; // 이미 완료됐으면 종료

            Debug.LogWarning("[DataManager] 초기 로드 미완료 — 재시도");
            StopAllFetching();
            isGeospatialReady = false;
            fetchCoroutine = StartCoroutine(FetchDataOnce());
            checkPositionCoroutine = StartCoroutine(CheckPositionAndFetchData());
        }
    }

    private void LoadInitialFilters()
    {
        currentFilters = new Dictionary<string, bool>();
        
        // FilterManager와 동일한 PlayerPrefs 키를 사용하여 초기값 로드
        int petState = PlayerPrefs.GetInt("Filter_PetFriendly_V3", 0); // 0:전부, 1:반려견 되는 곳만 (옛 2 '안 되는 곳만'은 없앰 → 전부)
        currentFilters["petFriendlyAll"] = (petState != 1);
        currentFilters["petFriendlyOnly"] = (petState == 1);
        
        currentFilters["object3D"] = PlayerPrefs.GetInt("Filter_Object3D_V2", 1) == 1;
        currentFilters["alcohol"] = PlayerPrefs.GetInt("Filter_Alcohol_V2", 1) == 1;
        
        // WoopangData는 기본적으로 표시
        currentFilters["woopangData"] = true; 
    }

    private IEnumerator InitializeObjectPoolsAsync()
    {
        if (cubePrefab == null || glbPrefab == null)
        {
            Debug.LogError($"[DataManager] 프리팹 누락! cubePrefab={cubePrefab}, glbPrefab={glbPrefab}");
            yield break;
        }

        // Cube 오브젝트 풀 초기화 (cubePoolSize만큼, 5개씩 생성하고 프레임 양보)
        for (int i = 0; i < cubePoolSize; i++)
        {
            GameObject cubeObj = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);
            cubeObj.SetActive(false);
            cubeObjectPool.Enqueue(cubeObj);

            if (i % 5 == 4) yield return null; // 5개마다 프레임 양보
        }

        // GLB 오브젝트 풀 초기화 (glbPoolSize만큼 — 메모리 비용 커서 cube보다 적게)
        for (int i = 0; i < glbPoolSize; i++)
        {
            GameObject glbObj = Instantiate(glbPrefab, Vector3.zero, Quaternion.identity);
            glbObj.SetActive(false);
            glbObjectPool.Enqueue(glbObj);

            if (i % 5 == 4) yield return null; // 5개마다 프레임 양보
        }
    }

    private IEnumerator StartLocationServiceAndFetchData()
    {
#if UNITY_EDITOR
        float lat = VirtualLocation.Instance.Latitude;
        float lon = VirtualLocation.Instance.Longitude;

        lastPosition = new Vector2(lat, lon);

        fetchCoroutine = StartCoroutine(FetchDataOnce());
        checkPositionCoroutine = StartCoroutine(CheckPositionAndFetchData());
        yield break;
#else
        // 위치 권한이 아직 없으면 권한 창에 답할 시간만큼 기다린다 (첫 설치 시 권한 요청 팝업).
        // 거부가 분명해지면 바로 안내한다 — 예전엔 30초를 말없이 기다렸다.
        if (!Input.location.isEnabledByUser)
        {
            float waited = 0f;
            float settled = 0f;   // 권한 창 없이(앱에 포커스) 위치가 꺼진 상태가 이어진 시간
            while (!Input.location.isEnabledByUser && waited < locationPermissionMaxWait)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
#if UNITY_ANDROID
                // ARCore 는 카메라 허용 뒤에 위치를 묻는다 — 카메라가 허용됐고 권한 창이 닫혔는데도 위치 권한이 없으면 거부
                bool denied = Application.isFocused
                    && UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera)
                    && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation);
                settled = denied ? settled + 0.5f : 0f;
                if (waited >= androidLocationDeniedGrace && settled >= 2f) break;
#elif UNITY_IOS
                // 시스템 권한 창이 떠 있는 동안(포커스 없음)은 세지 않는다
                if (Application.isFocused) settled += 0.5f;
                if (settled >= iosLocationPromptGrace) break;
#endif
            }
        }

        // 기다린 뒤에도 위치가 꺼져 있으면 → 설정 안내 패널 표시 후 대기
        if (!Input.location.isEnabledByUser)
        {
            if (LocationPermissionManager.Instance != null)
                LocationPermissionManager.Instance.ShowPanel();

            // 사용자가 설정에서 권한 허용 후 돌아올 때까지 대기 (최대 5분)
            float settingsWait = 0f;
            while (!Input.location.isEnabledByUser && settingsWait < 300f)
            {
                yield return new WaitForSeconds(1f);
                settingsWait += 1f;
            }

            if (LocationPermissionManager.Instance != null)
                LocationPermissionManager.Instance.ClosePanel();
        }

        // 그래도 권한 없으면 기본 위치로 조용히 진행
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("[DataManager] 위치 권한 없음 — 기본 위치로 데이터 로드 진행");
        }
        else
        {
            Input.location.Start();
            int maxWait = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
            {
                yield return new WaitForSeconds(1);
                maxWait--;
            }

            if (Input.location.status == LocationServiceStatus.Failed)
            {
                Debug.LogWarning("[DataManager] 위치 서비스 시작 실패 — 기본 위치로 진행");
            }

            // iOS 첫 설치: 위치 권한 없이 시작된 ARSession의 네이티브 Geospatial 모듈이
            // 실패 상태를 캐시하여 EarthTrackingState가 영구적으로 None 유지됨.
            // 런타임 config 변경으로는 네이티브 세션을 재생성할 수 없으므로,
            // 씬 리로드로 ARSession/ARCoreExtensions를 완전히 재생성.
            // 리로드 후에는 권한이 이미 있으므로 Geospatial이 정상 초기화됨.
#if UNITY_IOS
            if (PlayerPrefs.GetInt("geospatial_init_done", 0) == 0
                && Input.location.status == LocationServiceStatus.Running)
            {
                PlayerPrefs.SetInt("geospatial_init_done", 1);
                PlayerPrefs.Save();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                yield break;
            }
#endif

            // 권한 획득 시 다른 매니저들에게 이벤트 발행 (TourAPI, Terminal, TrainStation, P2P)
            OnLocationPermissionGranted?.Invoke();
        }

        // GPS 위치 확보 (Running이면 사용, 아니면 FetchDataOnce에서 재시도)
        float latitude = 0f;
        float longitude = 0f;

        if (Input.location.status == LocationServiceStatus.Running)
        {
            latitude = Input.location.lastData.latitude;
            longitude = Input.location.lastData.longitude;
        }

        if (latitude != 0f || longitude != 0f)
        {
            lastPosition = new Vector2(latitude, longitude);
        }

        fetchCoroutine = StartCoroutine(FetchDataOnce());
        checkPositionCoroutine = StartCoroutine(CheckPositionAndFetchData());
#endif
    }

    private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
        // FetchDataOnce 내부에서 WaitUntil(SessionTracking)으로 대기하므로
        // 여기서 별도로 fetch를 시작할 필요 없음 (이중 실행 방지)
    }

    /// <summary>
    /// 최초 1회 데이터 로드 (AR 세션 + Geospatial 준비 후)
    /// 이후 위치 변경은 CheckPositionAndFetchData에서 처리
    /// </summary>
    private IEnumerator FetchDataOnce()
    {
        if (isFetching)
        {
            yield break;
        }
        isFetching = true;
#if UNITY_EDITOR
        float lat = VirtualLocation.Instance.Latitude;
        float lon = VirtualLocation.Instance.Longitude;
        isGeospatialReady = true;

        // Light API로 캐시 수집 (11-tier → 단일 요청)
        yield return StartCoroutine(FetchLightCache(lat, lon));

        // fallback 활성화 알림
        if (lightCache.Count > 0)
        {
            OnPreFetchCompleted?.Invoke();
        }

        // 에디터에서도 초기 로드 완료 표시
        isInitialStartComplete = true;
        isDataLoaded = true;
        isFetching = false;
#else
        // ============================================================
        // Phase 1: GPS lat/lon으로 Light 캐시 수집 (Geospatial 대기 없이)
        // ============================================================
        float lat = 0f;
        float lon = 0f;
        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }

        // GPS가 아직 안 잡혔으면 짧게 대기 (최대 5초)
        float waitForGPS = 0f;
        while ((lat == 0f && lon == 0f) && waitForGPS < 5f)
        {
            yield return new WaitForSeconds(0.5f);
            waitForGPS += 0.5f;
            if (Input.location.status == LocationServiceStatus.Running)
            {
                lat = Input.location.lastData.latitude;
                lon = Input.location.lastData.longitude;
            }
        }

        // 그래도 없으면 lastPosition fallback
        if (lat == 0f && lon == 0f)
        {
            lat = lastPosition.x;
            lon = lastPosition.y;
        }

        if (lat != 0f || lon != 0f)
        {
            lastPosition = new Vector2(lat, lon);
            // Light API로 캐시 수집 (11-tier → 단일 요청)
            yield return StartCoroutine(FetchLightCache(lat, lon));
        }
        // 위치를 아직 모르면 (0,0) 으로 받지 않는다 — 엉뚱한 곳의 빈 목록이 '받음'으로 굳어 1km 이동 전까지 장소가 안 떴다.
        // GPS 가 잡히면 FilterManager 가 받는다 (RefreshAllCaches · 못 받은 목록 다시 받기)

        // fallback 활성화 알림 → LoadingManager가 fallback UI 시작
        if (lightCache.Count > 0)
        {
            OnPreFetchCompleted?.Invoke();
        }

        // Phase1 완료 — FilterManager가 AllocateObjects에서 스폰 결정
        isInitialStartComplete = true;
        isDataLoaded = true;

        // ============================================================
        // Phase 2: AR 세션 + Geospatial 대기 → 고도값 기반 정밀 배치
        // ============================================================
        yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking);

        if (!isGeospatialReady)
        {
            ShowARGuide(R0926LocalizedText.Say("주변을 천천히 둘러보세요\n위치를 파악하고 있습니다...", "Look around slowly\nFinding your location...", "ゆっくり周りを見回してください\n位置を確認しています...", "请慢慢环顾四周\n正在确定位置...", "Mira a tu alrededor despacio\nBuscando tu ubicación..."));
            yield return StartCoroutine(WaitForGeospatialTracking());
            HideARGuide();
        }

        // Geospatial 준비 완료 → GPS 위치 갱신 (더 정확한 값)
        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }
        lastPosition = new Vector2(lat, lon);

        // Earth Tracking이 실제로 Tracking 상태인지 확인 후 앵커 재생성
        var earthMgr = FindFirstObjectByType<AREarthManager>();

        if (earthMgr != null && earthMgr.EarthTrackingState == TrackingState.Tracking)
        {
            CreateAllInitialAnchors();
        }
        else
        {
            StartCoroutine(WaitForEarthAndRecreateAnchors());
        }
#endif

        isDataLoaded = true;
        isInitialStartComplete = true;
        isFetching = false;
    }

    /// <summary>
    /// Light API로 경량 캐시 수집 (11-tier 반복 요청 → 단일 요청으로 대체)
    /// FilterManager가 이 캐시를 기반으로 Full/IndicatorOnly 배분 결정
    /// </summary>
    private IEnumerator FetchLightCache(float lat, float lon)
    {
        string url = string.Format("{0}?lat={1}&lon={2}&radius=10000&limit=500", ApiConfig.LOCATIONS_LIGHT, lat, lon);
        int timeout = lightCacheTimeout > 0 ? lightCacheTimeout : 15;
        int seq = ++lightCacheRequestSeq;
        lightCacheBusyUntil = Time.realtimeSinceStartup + timeout + 1f;
        lightCacheBusyPos = new Vector2(lat, lon);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = timeout;
            yield return request.SendWebRequest();
            if (seq == lightCacheRequestSeq) lightCacheBusyUntil = 0f;
            ServerHealth.Report(request);
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var items = JsonConvert.DeserializeObject<List<LightPlaceData>>(request.downloadHandler.text);
                    if (items != null)
                    {
                        lightCache.Clear();
                        foreach (var item in items)
                        {
                            string cat = item.category ?? "";
                            lightCache.Add(new CachedPlaceData
                            {
                                uniqueId = "dm_" + item.id,
                                rawId = item.id.ToString(),
                                displayName = item.name ?? "",
                                latitude = item.latitude,
                                longitude = item.longitude,
                                altitude = item.altitude,
                                category = cat,
                                sourceManager = "DataManager",
                                modelType = item.model_type ?? "cube",
                                petFriendly = item.pet_friendly,
                                filterKey = FilterManager.IsPublicSource(item.is_public, cat) ? "publicData" : ""
                            });
                        }
                        MarkCacheReady();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DataManager] FetchLightCache 파싱 실패: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[DataManager] FetchLightCache 서버 요청 실패: {request.error}");
            }
        }
    }

    /// <summary>
    /// Detail API에서 특정 ID의 전체 데이터를 배치로 가져와서 스폰
    /// FilterManager.SpawnFullObject → pendingDetailFetchIds 큐잉 → 이 코루틴에서 배치 처리
    /// </summary>
    private IEnumerator BatchFetchDetailAndSpawn()
    {
        // 한 프레임 대기하여 같은 배분 사이클의 모든 ID 수집
        yield return null;

        while (pendingDetailFetchIds.Count > 0)
        {
            List<int> ids = new List<int>(pendingDetailFetchIds);
            pendingDetailFetchIds.Clear();

            string idsParam = string.Join(",", ids);
            string url = string.Format("{0}?ids={1}", ApiConfig.LOCATIONS_DETAIL, idsParam);

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var places = JsonConvert.DeserializeObject<List<PlaceData>>(request.downloadHandler.text);
                        if (places != null)
                        {
                            foreach (var place in places)
                            {
                                placeDataMap[place.id] = place;
                                if (!spawnedObjects.ContainsKey(place.id))
                                {
                                    // Full 스폰 전에 임시 IndicatorOnly가 있으면 제거
                                    string rawId = place.id.ToString();
                                    if (indicatorOnlyObjects.ContainsKey(place.id))
                                    {
                                        DespawnIndicatorOnly(rawId);
                                    }

                                    try { CreateObjectFromData(place); }
                                    catch (System.Exception ex)
                                    {
                                        Debug.LogError($"[DataManager] CreateObjectFromData 예외: id={place.id}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[DataManager] BatchFetchDetail 파싱 실패: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogError($"[DataManager] BatchFetchDetail 서버 요청 실패: {request.error}");
                }
            }
        }

        // ObjectCountUI 업데이트
        if (objectCountUI != null)
            objectCountUI.UpdateObjectCount(GetAllVisibleObjectCount(), true);

        batchDetailCoroutine = null;
    }

    private IEnumerator CheckPositionAndFetchData()
    {
        int myGeneration = fetchGeneration;

        // 최초 데이터 로드 완료까지 대기
        yield return new WaitUntil(() => isDataLoaded || myGeneration != fetchGeneration);

        while (true)
        {
            if (myGeneration != fetchGeneration) yield break;

#if UNITY_EDITOR
            float lat = VirtualLocation.Instance.Latitude;
            float lon = VirtualLocation.Instance.Longitude;
#else
            if (Input.location.status != LocationServiceStatus.Running)
            {
                yield return new WaitForSeconds(5f);
                continue;
            }
            float lat = Input.location.lastData.latitude;
            float lon = Input.location.lastData.longitude;
#endif

            Vector2 currentPos = new Vector2(lat, lon);
            float distanceMoved = CalculateDistance(lastPosition.x, lastPosition.y, currentPos.x, currentPos.y);

            if (distanceMoved > updateDistanceThreshold)
            {
                lastPosition = currentPos;
            }

            yield return new WaitForSeconds(5f);
        }
    }

    private IEnumerator FetchDataProgressivelySilent(float lat, float lon)
    {
        yield return StartCoroutine(FetchDataProgressively(lat, lon, true));
    }

    private IEnumerator FetchDataProgressively(float lat, float lon, bool silent = false)
    {
        // 중복 실행 방지
        if (isFetching)
        {
            yield break;
        }
        isFetching = true;
        int myGeneration = fetchGeneration; // 이 코루틴의 세대 번호 기록
        // placeDataMap.Keys 기준으로 중복 서버 요청 방지 (좌표만 저장된 것도 포함)
        HashSet<int> loadedIds = new HashSet<int>(placeDataMap.Keys);
        foreach (int id in spawnedObjects.Keys)
            loadedIds.Add(id);

        // UI 리셋 (새로운 로드 시작)
        if (!silent && objectCountUI != null)
        {
            objectCountUI.ResetUI();
        }

        for (int tierIndex = 0; tierIndex < loadRadii.Length; tierIndex++)
        {
            // 세대 체크: StopAllFetching으로 무효화된 코루틴은 즉시 종료
            if (myGeneration != fetchGeneration)
            {
                yield break;
            }

            float radius = loadRadii[tierIndex];
            string serverUrl = string.Format("{0}&lat={1}&lon={2}&radius={3}", baseServerUrl, lat, lon, radius);

            List<PlaceData> newPlaces = new List<PlaceData>();
            yield return StartCoroutine(FetchDataFromServerForTier(serverUrl, lat, lon, loadedIds, newPlaces));

            // yield 후 세대 재확인
            if (myGeneration != fetchGeneration)
            {
                yield break;
            }

            // 새로운 데이터 저장 — 3D 오브젝트 스폰은 FilterManager가 중앙에서 담당
            // (DataManager는 데이터 보관소 역할만, 직접 스폰 안 함)
            foreach (PlaceData place in newPlaces)
            {
                placeDataMap[place.id] = place;
                loadedIds.Add(place.id);
            }
            if (tierIndex == loadRadii.Length - 1 && objectCountUI != null)
            {
                objectCountUI.UpdateObjectCount(GetAllVisibleObjectCount(), true);
            }

            if (tierIndex < loadRadii.Length - 1 && tierDelay > 0) yield return new WaitForSeconds(tierDelay);
        }

        // Stale object cleanup: 현재 위치에서 MaxDisplayDistance × 1.5 밖의 오브젝트를 풀로 반환
        CleanupStaleObjects(lat, lon);

        isDataLoaded = true;
        isFetching = false;
    }

    /// <summary>
    /// 데이터 정리: MaxDisplayDistance × 1.5 밖의 placeDataMap 항목 제거
    /// (3D 오브젝트 스폰/디스폰은 FilterManager가 중앙 관리하므로 여기서 처리하지 않음)
    /// </summary>
    private void CleanupStaleObjects(float lat, float lon)
    {
        float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
        float dataCleanupRange = maxDist * 1.5f;

        // 데이터 정리: MaxDisplayDistance × 1.5 밖 → placeDataMap에서도 제거
        List<int> dataToRemove = new List<int>();
        foreach (var kvp in placeDataMap)
        {
            if (spawnedObjects.ContainsKey(kvp.Key)) continue; // 오브젝트 있으면 유지
            float dist = CalculateDistance(lat, lon, kvp.Value.latitude, kvp.Value.longitude);
            if (dist > dataCleanupRange)
            {
                dataToRemove.Add(kvp.Key);
            }
        }

        foreach (int id in dataToRemove)
        {
            placeDataMap.Remove(id);
            // IndicatorOnly도 함께 정리
            if (indicatorOnlyObjects.TryGetValue(id, out GameObject indicatorObj))
            {
                if (indicatorObj != null) Destroy(indicatorObj);
                indicatorOnlyObjects.Remove(id);
            }
        }
    }

    private IEnumerator FetchDataFromServerForTier(string url, float lat, float lon, HashSet<int> loadedIds, List<PlaceData> outNewPlaces)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;

                try
                {
                    List<PlaceData> places = null;
                    try
                    {
                        places = JsonConvert.DeserializeObject<List<PlaceData>>(json);
                    }
                    catch (System.Exception parseEx)
                    {
                        Debug.LogWarning($"[DataManager] JSON 일괄 파싱 실패, 개별 파싱 시도: {parseEx.Message}");
                        // 개별 파싱 fallback: JArray로 한 건씩 처리
                        try
                        {
                            var jArray = Newtonsoft.Json.Linq.JArray.Parse(json);
                            places = new List<PlaceData>();
                            foreach (var jItem in jArray)
                            {
                                try
                                {
                                    var p = jItem.ToObject<PlaceData>();
                                    if (p != null) places.Add(p);
                                }
                                catch (System.Exception itemEx)
                                {
                                    Debug.LogWarning($"[DataManager] 개별 파싱 실패: id={jItem["id"]}, name={jItem["name"]}: {itemEx.Message}");
                                }
                            }
                            Debug.LogWarning($"[DataManager] 개별 파싱으로 {places.Count}/{jArray.Count}건 복구");
                        }
                        catch (System.Exception fallbackEx)
                        {
                            Debug.LogError($"[DataManager] 개별 파싱도 실패: {fallbackEx.Message}");
                        }
                    }

                    if (places != null && places.Count > 0)
                    {
                        // 거리순 정렬
                        places.Sort((a, b) =>
                        {
                            float distA = CalculateDistance(lat, lon, a.latitude, a.longitude);
                            float distB = CalculateDistance(lat, lon, b.latitude, b.longitude);
                            return distA.CompareTo(distB);
                        });

                        foreach (var place in places)
                        {
                            if (!loadedIds.Contains(place.id)) outNewPlaces.Add(place);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[DataManager] 서버 응답 파싱 결과 null 또는 0건");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DataManager] FetchDataFromServerForTier 예외: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[DataManager] 서버 요청 실패: {request.result} / {request.error} / URL={url}");
            }
        }
    }

    private IEnumerator FetchDataFromServer(string url, LocationInfo currentLocation)
    {
        int retryCount = 3;
        for (int i = 0; i < retryCount; i++)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    yield return StartCoroutine(ProcessData(json, currentLocation));
                    break;
                }
                else
                {
                    if (i < retryCount - 1) 
                        yield return new WaitForSeconds(5f);
                    else 
                        ShowErrorMessage(R0926LocalizedText.Say("서버에서 데이터를 받아오지 못했습니다.", "Couldn't get data from the server.", "サーバーからデータを取得できませんでした。", "无法从服务器获取数据。", "No se pudieron obtener datos del servidor."));
                }
            }
        }
    }

    private IEnumerator ProcessData(string json, LocationInfo currentLocation)
    {
        List<PlaceData> places = null;
        try
        {
            places = JsonConvert.DeserializeObject<List<PlaceData>>(json);
        }
        catch (JsonException)
        {
            ShowErrorMessage(R0926LocalizedText.Say("데이터 파싱에 실패했습니다.", "Couldn't read the data.", "データを読み取れませんでした。", "数据解析失败。", "No se pudieron leer los datos."));
            yield break;
        }

        if (places == null || places.Count == 0)
        {
            ShowErrorMessage(R0926LocalizedText.Say("서버에서 데이터를 받아오지 못했습니다.", "Couldn't get data from the server.", "サーバーからデータを取得できませんでした。", "无法从服务器获取数据。", "No se pudieron obtener datos del servidor."));
            yield break;
        }

        // 거리 순으로 정렬하고 개수 제한
        places.Sort((a, b) => CalculateDistance(currentLocation.latitude, currentLocation.longitude, a.latitude, a.longitude)
            .CompareTo(CalculateDistance(currentLocation.latitude, currentLocation.longitude, b.latitude, b.longitude)));
        places = places.Take(poolSize * 2).ToList();

        float curLat = currentLocation.latitude;
        float curLon = currentLocation.longitude;

        // 청크 단위로 placeDataMap만 채움 — 3D 스폰은 FilterManager가 담당
        const int CHUNK_SIZE = 5;
        for (int i = 0; i < places.Count; i += CHUNK_SIZE)
        {
            var chunk = places.Skip(i).Take(CHUNK_SIZE).ToList();
            foreach (PlaceData place in chunk)
            {
                placeDataMap[place.id] = place;

                if (spawnedObjects.ContainsKey(place.id))
                {
                    // 이미 스폰된 오브젝트는 데이터만 업데이트
                    UpdateExistingObject(place, spawnedObjects[place.id]);
                }
            }
            yield return null; // 프레임 양보
        }
    }

    /// <summary>
    /// 카테고리 필터 문자열 설정 (FilterManager에서 호출)
    /// </summary>
    public void SetCategoryFilter(string category)
    {
        currentCategoryFilter = category ?? "";
    }

    public void ApplyFilters(Dictionary<string, bool> filters)
    {
        if (filters == null) return;
        currentFilters = filters;

        // FilterManager에서 카테고리 필터값 가져오기
        FilterManager filterMgr = FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null)
            currentCategoryFilter = filterMgr.GetActiveCategoryFilter();

        foreach (var kvp in spawnedObjects)
        {
            int placeId = kvp.Key;
            GameObject obj = kvp.Value;
            if (obj == null) continue;

            if (placeDataMap.ContainsKey(placeId))
            {
                PlaceData place = placeDataMap[placeId];
                bool shouldShow = ShouldShowObject(place);
                obj.SetActive(shouldShow);
            }
            else if (currentFilters.ContainsKey("object3D") && !currentFilters["object3D"])
            {
                // placeData 없는 오브젝트는 custom으로 간주하여 숨김
                obj.SetActive(false);
            }
        }
    }

    /// <summary>
    /// DataManager 자체 오브젝트 중 활성화(visible) 상태인 수 반환
    /// </summary>
    public int GetVisibleObjectCount()
    {
        int count = 0;
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value != null && kvp.Value.activeSelf)
                count++;
        }
        return count;
    }

    /// <summary>
    /// 모든 매니저의 visible 오브젝트 합산 카운트 (필터+거리 적용된 실제 표시 수)
    /// </summary>
    public int GetAllVisibleObjectCount()
    {
        int total = GetVisibleObjectCount();
        if (TourAPIManager.Instance != null) total += TourAPIManager.Instance.GetVisibleObjectCount();
        if (SubwayManager.Instance != null) total += SubwayManager.Instance.GetVisibleObjectCount();
        if (TerminalManager.Instance != null) total += TerminalManager.Instance.GetVisibleObjectCount();
        if (TrainStationManager.Instance != null) total += TrainStationManager.Instance.GetVisibleObjectCount();
        return total;
    }

    private bool ShouldShowObject(PlaceData place)
    {
        if (currentFilters == null) return true;

        // 애견동반 필터 (전부 / 반려견 되는 곳만)
        bool petFriendlyAll = currentFilters.ContainsKey("petFriendlyAll") && currentFilters["petFriendlyAll"];
        bool petFriendlyOnly = currentFilters.ContainsKey("petFriendlyOnly") && currentFilters["petFriendlyOnly"];

        // alcohol 키가 없으면 기본값 true
        bool showAlcohol = !currentFilters.ContainsKey("alcohol") || currentFilters["alcohol"];
        // woopangData 키가 없으면 기본값 true (모든 우팡 데이터 표시)
        bool showWoopangData = !currentFilters.ContainsKey("woopangData") || currentFilters["woopangData"];
        // object3D 키가 없으면 기본값 true
        bool showObject3D = !currentFilters.ContainsKey("object3D") || currentFilters["object3D"];
        // publicData 키가 없으면 기본값 true
        bool showPublicData = !currentFilters.ContainsKey("publicData") || currentFilters["publicData"];

        // Object3D 토글 OFF: 원본이 custom인 오브젝트만 숨김 (GLB 실패로 cube 전환된 것도 포함)
        string origType = place.original_model_type ?? place.model_type;
        if (!showObject3D && origType == "custom")
        {
            return false;
        }

        // 공공데이터 칩: 기관이 등록한 장소만 숨긴다 (사람이 '공원'으로 올린 곳은 우팡 데이터).
        // 분류(카테고리)를 고른 동안은 분류가 정한다 — 그때 publicData 는 늘 false 로 온다
        bool categoryMode = currentFilters.ContainsKey("categoryFilter") && currentFilters["categoryFilter"];
        if (!categoryMode && !showPublicData && FilterManager.IsPublicSource(place.is_public, place.category))
            return false;

        bool shouldShow = showWoopangData;

        if (shouldShow)
        {
            // 애견동반 필터 적용
            if (petFriendlyOnly && !place.pet_friendly)
            {
                shouldShow = false;
            }

            // 주류 판매 필터 적용
            if (shouldShow && place.alcohol_available && !showAlcohol)
            {
                shouldShow = false;
            }

            // 카테고리 필터 적용
            if (shouldShow && !string.IsNullOrEmpty(currentCategoryFilter))
            {
                if (place.category != currentCategoryFilter)
                    shouldShow = false;
            }
        }

        return shouldShow;
    }

    private void CreateObjectFromData(PlaceData place)
    {
        if (HiddenPlaces.IsHidden("dm_" + place.id)) return;   // 이 기기에서 X 로 숨긴 장소

        // 서버 원본 model_type 보존 (필터용 - GLB 실패 시 cube로 바뀌어도 원본 유지)
        if (string.IsNullOrEmpty(place.original_model_type))
            place.original_model_type = place.model_type;

        // GLB 동시 로딩 제한
        if (place.model_type == "custom" && currentlyLoadingGLB.Count >= maxConcurrentGLBLoads)
        {
            if (fallbackToCube)
            {
                place.model_type = "cube"; // 큐브로 fallback
            }
            else
            {
                return; // 로딩 제한으로 건너뛰기
            }
        }

        // 거리 + 필터 체크 (오브젝트 생성 전에 판단)
        bool shouldShow = ShouldShowObject(place);
        if (shouldShow)
        {
            shouldShow = IsPlaceInDisplayRange(place);
        }

        GameObject newObj = GetFromPool(place.model_type);
        if (newObj == null)
        {
            return;
        }

        // Target 컴포넌트를 비활성 상태에서 미리 끄기 (SetActive 시 인디케이터 등록 방지)
        Target targetComp = newObj.GetComponentInChildren<Target>(true);
        if (targetComp != null && !shouldShow)
            targetComp.enabled = false;

        // 렌더러 먼저 비활성화 → SetActive(true) 시 원점(Vector3.zero)에서 플래시 방지
        // SetCoordinatesAndCreateAnchor 내부에서 앵커 생성 성공 시 SetVisible(true) 호출
        Renderer[] preRenderers = newObj.GetComponentsInChildren<Renderer>(true);
        foreach (var r in preRenderers) r.enabled = false;

        // 코루틴 실행을 위해 먼저 활성화 후 컴포넌트 설정
        newObj.SetActive(true);
        newObj.name = string.Format("Place_{0}_{1}", place.id, place.model_type);

        bool setupSuccess = SetupObjectComponents(newObj, place);

        if (setupSuccess)
        {
            if (!shouldShow)
            {
                newObj.SetActive(false);
                // Target 복원 (다음에 활성화될 때 정상 동작하도록)
                if (targetComp != null) targetComp.enabled = true;
            }

            spawnedObjects[place.id] = newObj;
            placeDataMap[place.id] = place;
        }
        else
        {
            ReturnToPool(newObj, place.model_type);
        }
    }

    private void UpdateExistingObject(PlaceData place, GameObject existingObj)
    {
        SetupObjectComponents(existingObj, place);
        placeDataMap[place.id] = place;
    }

    private bool SetupObjectComponents(GameObject obj, PlaceData place)
    {
        CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
        if (anchor == null) return false;
        anchor.SetCoordinatesAndCreateAnchor(place.latitude, place.longitude, place.altitude);

        // 상세 사진은 주소만 넘긴다 (상세를 열 때 받는다). 사진을 다 지운 장소도 빈 목록으로 넘겨 앞서 받은 주소를 비운다
        ImageDisplayController displayCtrl = obj.GetComponentInChildren<ImageDisplayController>(true);
        if (displayCtrl != null) displayCtrl.SetSubPhotos(FlattenSubPhotos(place));

        if (place.model_type == "cube") return SetupCubeObject(obj, place);
        else if (place.model_type == "custom") return SetupGLBObject(obj, place);
        else return SetupCubeObject(obj, place);
    }

    public static List<string> FlattenSubPhotos(PlaceData place)
    {
        List<string> allSubPhotos = new List<string>();
        if (place.sub_photos != null)
        {
            foreach (var photoGroup in place.sub_photos)
            {
                if (photoGroup == null) continue;
                foreach (var photo in photoGroup)
                {
                    if (!string.IsNullOrEmpty(photo)) allSubPhotos.Add(photo);
                }
            }
        }
        return allSubPhotos;
    }

    // 앱 내장 심볼을 쓰는 공공데이터 카테고리 — 서버 사진 로딩 없이 Resources/PublicIcons/{cat}.png 사용.
    // 명소/문화/종교(landmark/culture/religious)는 실사진 가치가 커서 제외(서버 사진 유지).
    private static readonly HashSet<string> inAppIconCategories = new HashSet<string>
    {
        "gov", "utility", "edu", "medical", "sport", "welfare", "park", "toilet"
    };

    /// <summary>공공 심볼 카테고리면 Resources 경로, 아니면 null.</summary>
    private static string GetInAppIconPath(string category)
    {
        if (!string.IsNullOrEmpty(category) && inAppIconCategories.Contains(category))
            return "PublicIcons/" + category;
        return null;
    }

    private bool SetupCubeObject(GameObject obj, PlaceData place)
    {
        ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>(true);
        if (display != null)
        {
            // 기관이 등록한 공공데이터만 앱 내장 그림 우선(서버 로딩 0). 내장 텍스처 없으면 서버 사진 폴백.
            // 사람이 찍어 올린 곳은 카테고리가 '공원'이어도 그 사진으로 (10-17 공원 기본 그림으로 뜨던 것)
            bool usedInApp = false;
            string iconPath = FilterManager.IsPublicSource(place.is_public, place.category) ? GetInAppIconPath(place.category) : null;
            if (iconPath != null) usedInApp = display.SetBaseMapFromResources(iconPath);
            if (!usedInApp && !string.IsNullOrEmpty(place.main_photo)) display.SetBaseMap(place.main_photo);
        }

        DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>(true);
        if (doubleTap == null) return false;
        SetupDoubleTapInfo(doubleTap, place);

        Target target = obj.GetComponentInChildren<Target>(true);
        if (target == null) return false;
        SetupTargetInfo(target, place);

        return true;
    }

    private bool SetupGLBObject(GameObject obj, PlaceData place)
    {
        if (string.IsNullOrEmpty(place.model_url))
        {
            if (fallbackToCube)
            {
                place.model_type = "cube";
                return SetupCubeObject(obj, place);
            }
            return false;
        }

        GLBModelLoader glbLoader = obj.GetComponent<GLBModelLoader>();
        if (glbLoader == null) glbLoader = obj.AddComponent<GLBModelLoader>();

        glbLoader.ClearModel();

        // model_url이 이미 풀 URL(http/https)이면 그대로 사용, 상대 경로면 MAIN_SERVER 붙임.
        // 이전 버그: 무조건 prepend → "https://...woopang.com/https://..." 형식의 잘못된 URL → 404
        string fullUrl = place.model_url.StartsWith("http")
            ? place.model_url
            : ApiConfig.MAIN_SERVER + "/" + place.model_url.Replace("\\", "/");
        float scale = place.model_scale > 0 ? place.model_scale : 1.0f;
        
        currentlyLoadingGLB.Add(place.id);
        StartCoroutine(LoadGLBAsync(glbLoader, fullUrl, scale, place.id, obj, place));

        return true;
    }

    private void SetupDoubleTapInfo(DoubleTap3D doubleTap, PlaceData place)
    {
        Sprite petFriendlySprite = Resources.Load<Sprite>("Sprites/pet_friendly_icon") ?? Resources.Load<Sprite>("Sprites/default_icon");
        Sprite restroomSprite = Resources.Load<Sprite>("Sprites/separate_restroom_icon") ?? Resources.Load<Sprite>("Sprites/default_icon");
        doubleTap.SetInfoImages(petFriendlySprite, restroomSprite, place.pet_friendly, place.separate_restroom, place.instagram_id, place.name, place.id, place.username, place.instagram_id);
    }

    private void SetupTargetInfo(Target target, PlaceData place)
    {
        // 칩이 있는 분류는 칩 색, 그 밖은 서버 color(HEX) → 카테고리 색 → 흰색 (ResolvePlaceColor — 목록과 같은 값)
        target.TargetColor = ResolvePlaceColor(place.color, place.category, place.name);
        target.PlaceName = place.name;
        target.placeId = place.id.ToString();
        target.gpsLatitude = place.latitude;
        target.gpsLongitude = place.longitude;
    }

    private IEnumerator LoadGLBAsync(GLBModelLoader loader, string url, float scale, int placeId, GameObject glbObj, PlaceData place)
    {
        bool loadCompleted = false;
        bool loadSuccess = false;
        float startTime = Time.time;
        int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            loadCompleted = false;
            loadSuccess = false;
            StartCoroutine(LoadGLBCoroutine(loader, url, scale, (success) => {
                loadSuccess = success;
                loadCompleted = true;
            }));
            
            while (!loadCompleted && (Time.time - startTime) < glbLoadTimeout) yield return null;
            
            if (loadCompleted && loadSuccess) break;
            else if (attempt < maxAttempts)
            {
                yield return new WaitForSeconds(attempt + 1);
                loader.ClearModel();
            }
        }
        
        currentlyLoadingGLB.Remove(placeId);
        
        if (loadCompleted && loadSuccess)
        {
            DoubleTap3D doubleTap = glbObj.GetComponentInChildren<DoubleTap3D>();
            if (doubleTap != null) SetupDoubleTapInfo(doubleTap, place);

            Target target = glbObj.GetComponentInChildren<Target>();
            if (target != null) SetupTargetInfo(target, place);
        }
        else
        {
            if (fallbackToCube && spawnedObjects.ContainsKey(placeId))
            {
                ReturnToPool(glbObj, "custom");
                spawnedObjects.Remove(placeId);
                place.model_type = "cube";
                CreateObjectFromData(place);
            }
            else glbObj.SetActive(false);
        }
    }

    private IEnumerator LoadGLBCoroutine(GLBModelLoader loader, string url, float scale, System.Action<bool> onComplete)
    {
        yield return StartCoroutine(loader.LoadGLBModelCoroutine(url, scale, onComplete));
    }

    /// <summary>
    /// 장소가 현재 표시 거리 범위 내에 있는지 GPS 기반 체크
    /// </summary>
    private bool IsPlaceInDisplayRange(PlaceData place)
    {
        float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
        float lat = 0f, lon = 0f;
#if UNITY_EDITOR
        if (VirtualLocation.Instance != null) { lat = VirtualLocation.Instance.Latitude; lon = VirtualLocation.Instance.Longitude; }
#else
        if (Input.location.status == LocationServiceStatus.Running) { lat = Input.location.lastData.latitude; lon = Input.location.lastData.longitude; }
#endif
        if (lat == 0f && lon == 0f) { lat = lastPosition.x; lon = lastPosition.y; }
        if (lat == 0f && lon == 0f) return true; // GPS 없으면 일단 표시
        return CalculateDistance(lat, lon, place.latitude, place.longitude) <= maxDist;
    }

    private float CalculateDistance(double lat1, double lon1, double lat2, double lon2) => (float)GeoHere.Meters(lat1, lon1, lat2, lon2);

    private GameObject GetFromPool(string modelType)
    {
        Queue<GameObject> targetPool = modelType == "cube" ? cubeObjectPool : glbObjectPool;
        if (targetPool.Count > 0)
        {
            GameObject obj = targetPool.Dequeue();
            ResetObjectState(obj, modelType);
            // SetActive는 CreateObjectFromData에서 거리/필터 체크 후 호출
            obj.name = $"Place_ID_{modelType}";
            return obj;
        }
        else if (spawnedObjects.Count < poolSize * 4)
        {
            GameObject prefab = modelType == "cube" ? cubePrefab : glbPrefab;
            GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            ResetObjectState(obj, modelType);
            obj.name = $"Place_ID_{modelType}";
            return obj;
        }
        ShowErrorMessage(R0926LocalizedText.Say("너무 많은 장소가 로드되었습니다.", "Too many places were loaded.", "読み込まれた場所が多すぎます。", "加载的地点过多。", "Se cargaron demasiados lugares."));
        return null;
    }

    private void ResetObjectState(GameObject obj, string modelType)
    {
        DoubleTap3D[] doubleTaps = obj.GetComponentsInChildren<DoubleTap3D>(true);
        foreach (var doubleTap in doubleTaps) doubleTap.ResetData();

        // 이전 장소의 이미지 로딩 코루틴 중단 (이전 코루틴이 새 데이터를 덮어쓰는 문제 방지)
        // 텍스처/스프라이트 자체는 SetBaseMap/SetSubPhotos에서 새 데이터로 교체됨
        ImageDisplayController displayCtrl = obj.GetComponentInChildren<ImageDisplayController>(true);
        if (displayCtrl != null) displayCtrl.CancelPendingLoads();

        if (modelType == "custom")
        {
            GLBModelLoader glbLoader = obj.GetComponent<GLBModelLoader>();
            if (glbLoader != null) glbLoader.ClearModel();
        }
    }

    private void ReturnToPool(GameObject obj, string modelType)
    {
        Queue<GameObject> targetPool = modelType == "cube" ? cubeObjectPool : glbObjectPool;
        if (modelType == "custom")
        {
            GLBModelLoader glbLoader = obj.GetComponent<GLBModelLoader>();
            if (glbLoader != null) glbLoader.ClearModel();
        }

        // 받던 사진은 멈추고(loadGeneration 증가 — 풀 재사용 때 앞 장소 사진이 새 장소를 덮어쓰지 않게) 받아 둔 사진은 지운다.
        // 예전엔 로딩만 멈춰 풀에 든 오브젝트마다 지나온 장소 사진을 들고 있었다 (2026-10-23 아이폰 메모리)
        ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>(true);
        if (display != null) display.ClearImages();
        foreach (var doubleTap in obj.GetComponentsInChildren<DoubleTap3D>(true)) doubleTap.ReleasePhotos();
        obj.SetActive(false);
        targetPool.Enqueue(obj);
    }

    public Dictionary<int, GameObject> GetSpawnedObjects() => spawnedObjects;
    public Dictionary<int, GameObject> GetIndicatorOnlyObjects() => indicatorOnlyObjects;
    public int GetSpawnedObjectsCount() => spawnedObjects.Count;
    public Dictionary<int, PlaceData> GetPlaceDataMap() => placeDataMap;

    /// <summary>주어진 id가 dance_anim(category="anim") 항목인지 확인 — DoubleTap3D 라우팅용.</summary>
    public bool IsAnimCategory(int id)
    {
        if (placeDataMap.TryGetValue(id, out var p) && p.category == "anim") return true;
        var cached = lightCache.Find(c => c.rawId == id.ToString());
        return cached != null && cached.category == "anim";
    }

    /// <summary>
    /// dance_anim 큐브 → GLB 교체 (사용자가 다운로드 패널에서 확인 누른 후).
    /// 1) 현재 큐브 디스폰 → 풀 반환  2) in-memory에서 model_type="custom"로 전환
    /// 3) SpawnFullObject 재호출 → glbPrefab + model_url로 GLB 로드 + 애니메이션 자동 재생.
    /// 같은 id 유지 — FilterManager 배분 시스템과 자연 호환.
    /// </summary>
    public bool PromoteCubeToGLB(int id)
    {
        if (!placeDataMap.TryGetValue(id, out var place))
        {
            Debug.LogWarning($"[DataManager] id={id} placeData 없음 — Detail fetch 후 재시도 필요");
            return false;
        }
        if (place.model_type == "custom")
        {
            DbgLog($"[dbg-Promote] id={id} 이미 custom — 스킵");
            return true;
        }

        DbgLog($"[dbg-Promote] START id={id} model_url={place.model_url} scale={place.model_scale}");

        if (spawnedObjects.TryGetValue(id, out var obj))
        {
            ReturnToPool(obj, "cube");
            spawnedObjects.Remove(id);
            DbgLog($"[dbg-Promote] 큐브 디스폰 완료 id={id}");
        }
        else
        {
            Debug.LogWarning($"[DataManager] spawnedObjects에 큐브 없음 id={id}");
        }

        place.model_type = "custom";
        bool result = SpawnFullObject(id.ToString());
        DbgLog($"[dbg-Promote] SpawnFullObject 결과: {result}, spawnedObjects.ContainsKey={spawnedObjects.ContainsKey(id)}");
        return result;
    }

    /// <summary>
    /// PromoteCubeToGLB의 역방향 — anim GLB 자동 디스폰 후 model_type을 "cube"로 복원.
    /// 이걸 안 하면 FilterManager 배분 루프의 복구 스폰이 model_type="custom"을 보고
    /// 사용자 동의 없이 GLB를 다시 띄워버려 자동 디스폰(100m/3분)이 무력화됨.
    /// 복원 후엔 배분 루프가 큐브 플레이스홀더로 재스폰 → 더블탭+다운로드 opt-in 흐름 유지.
    /// GLB 바이트는 RAM 캐시에 남아 있어 재다운로드 없이 즉시 재승격 가능.
    /// </summary>
    public void DemoteAnimToCube(int id)
    {
        if (placeDataMap.TryGetValue(id, out var place) &&
            place.category == "anim" && place.model_type == "custom")
        {
            place.model_type = "cube";
            DbgLog($"[dbg-Promote] DemoteAnimToCube: id={id} → model_type=cube 복원");
        }
    }
    public List<CachedPlaceData> GetLightCache() => lightCache;
    public bool IsDataLoaded() => isDataLoaded;

    /// <summary>
    /// 모든 스폰된 오브젝트를 일시적으로 숨기기/표시 (백그라운드 복귀 시 사용)
    /// </summary>
    /// <summary>
    /// 3D 렌더러만 on/off (SetActive 건드리지 않음 → Target 유지 → fallback 화살표 정상 동작)
    /// fallback 진입 시 렌더러 숨기고, 해제 시 복원
    /// </summary>
    public void SetAllRenderersVisible(bool visible)
    {
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value == null) continue;
            CustomARGeospatialCreatorAnchor anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor != null)
            {
                anchor.SetForceHideRenderers(!visible);
            }
            else
            {
                Renderer[] renderers = kvp.Value.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers) r.enabled = visible;
            }
        }
    }

    public void SetAllObjectsVisible(bool visible)
    {
        if (visible)
        {
            // 표시 시 거리 필터 + 카테고리 필터 모두 적용
            RestoreObjectsWithDistanceFilter();
        }
        else
        {
            foreach (var kvp in spawnedObjects)
            {
                if (kvp.Value != null)
                    kvp.Value.SetActive(false);
            }
        }

        // IndicatorOnly도 동일하게 처리
        foreach (var kvp in indicatorOnlyObjects)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(visible);
        }
    }

    /// <summary>
    /// 거리 필터 + 카테고리 필터를 적용하여 오브젝트 표시 복원
    /// SetAllObjectsVisible(true) 시 거리 무관하게 전체 켜지는 문제 방지
    /// </summary>
    public void RestoreObjectsWithDistanceFilter()
    {
        float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
        float lat = 0f, lon = 0f;

#if UNITY_EDITOR
        if (VirtualLocation.Instance != null)
        {
            lat = VirtualLocation.Instance.Latitude;
            lon = VirtualLocation.Instance.Longitude;
        }
#else
        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }
#endif

        // GPS 좌표가 없으면 lastPosition 사용
        if (lat == 0f && lon == 0f)
        {
            lat = lastPosition.x;
            lon = lastPosition.y;
        }

        if (lat != 0f || lon != 0f)
        {
            UpdateDistanceFilter(maxDist, lat, lon);
        }
        else
        {
            // GPS도 없으면 필터만 적용하여 복원
            foreach (var kvp in spawnedObjects)
            {
                if (kvp.Value != null && placeDataMap.ContainsKey(kvp.Key))
                {
                    kvp.Value.SetActive(ShouldShowObject(placeDataMap[kvp.Key]));
                }
            }
        }
    }

    public GameObject GetSpawnedObject(int placeId)
    {
        return spawnedObjects.ContainsKey(placeId) ? spawnedObjects[placeId] : null;
    }

    /// <summary>
    /// 앱 시작 Phase2에서 Earth 준비 완료 시 일괄 앵커 생성 (1회성)
    /// 이후 정기 복구는 RetryFailedAnchors (FilterManager.AllocationLoop 주도)가 담당
    /// </summary>
    private void CreateAllInitialAnchors()
    {
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value == null) continue;
            if (!kvp.Value.activeSelf) continue;

            var anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor == null) continue;
            if (anchor.IsAnchorCreated) continue;
            if (anchor.IsRetrying) continue;

            anchor.RecreateAnchor();
        }
    }

    /// <summary>
    /// 앵커 생성에 실패한 오브젝트만 선별하여 재시도 (Full + IndicatorOnly 양쪽)
    /// FilterManager.AllocationLoop가 매 tick(2s) 호출 — AR Limited 구간 실패 항목 자동 복원
    /// </summary>
    public void RetryFailedAnchors()
    {
        RetryFailedAnchorsIn(spawnedObjects);
        RetryFailedAnchorsIn(indicatorOnlyObjects);
    }

    private static void RetryFailedAnchorsIn<TKey>(Dictionary<TKey, GameObject> objects)
    {
        if (objects == null) return;
        foreach (var kvp in objects)
        {
            if (kvp.Value == null) continue;
            if (!kvp.Value.activeSelf) continue;

            var anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor == null) continue;
            if (anchor.IsAnchorCreated) continue;
            if (anchor.IsRetrying) continue; // 이미 재시도 코루틴 실행 중이면 건드리지 않음

            anchor.RecreateAnchor();
        }
    }

    /// <summary>
    /// Earth Tracking이 될 때까지 대기 후 모든 앵커 재생성
    /// Phase2에서 Earth가 아직 None일 때 호출됨
    /// </summary>
    private IEnumerator WaitForEarthAndRecreateAnchors()
    {
        AREarthManager earthManager = FindFirstObjectByType<AREarthManager>();
        float elapsed = 0f;
        float maxWait = 120f; // 최대 2분 대기

        while (elapsed < maxWait)
        {
            if (earthManager == null)
                earthManager = FindFirstObjectByType<AREarthManager>();

            if (earthManager != null && earthManager.EarthTrackingState == TrackingState.Tracking)
            {
                CreateAllInitialAnchors();
                yield break;
            }

            elapsed += 1f;
            yield return new WaitForSeconds(1f);
        }

    }

    public void UpdateDistanceFilter(float maxDistance, float currentLat, float currentLon)
    {
        // GPS=(0,0)이면 lastPosition fallback 사용
        if (currentLat == 0f && currentLon == 0f)
        {
            currentLat = lastPosition.x;
            currentLon = lastPosition.y;
        }
        // 그래도 (0,0)이면 거리 필터 건너뜀 (잘못된 비활성화 방지)
        if (currentLat == 0f && currentLon == 0f) return;

        foreach (var kvp in spawnedObjects)
        {
            int id = kvp.Key;
            GameObject obj = kvp.Value;
            if (obj == null) continue;

            if (placeDataMap.ContainsKey(id))
            {
                PlaceData place = placeDataMap[id];

                // 카테고리/토글 필터 먼저 적용 (거리와 무관하게 숨김)
                if (!ShouldShowObject(place))
                {
                    if (obj.activeSelf) obj.SetActive(false);
                    continue;
                }

                // 거리 필터
                float dist = CalculateDistance(currentLat, currentLon, place.latitude, place.longitude);
                bool inRange = dist <= maxDistance;
                if (!inRange)
                {
                    if (obj.activeSelf) obj.SetActive(false);
                }
                else
                {
                    if (!obj.activeSelf) obj.SetActive(true);
                }
            }
        }

        // ★ IndicatorOnly도 동일하게 거리 필터 적용 + 활성화 복원
        // (백그라운드 진입 시 SetAllObjectsVisible(false)로 비활성화된 상태에서 복원)
        foreach (var kvp in indicatorOnlyObjects)
        {
            int id = kvp.Key;
            GameObject obj = kvp.Value;
            if (obj == null) continue;

            // lightCache에서 좌표 찾기
            string rawId = id.ToString();
            CachedPlaceData cached = lightCache.Find(c => c.rawId == rawId);
            if (cached == null) continue;

            // IndicatorOnly는 카테고리 필터만 (거리 필터는 fullObjectRadius 밖에서 표시되는 게 정상)
            float dist = CalculateDistance(currentLat, currentLon, cached.latitude, cached.longitude);
            bool inRange = dist <= maxDistance;
            if (!inRange)
            {
                if (obj.activeSelf) obj.SetActive(false);
            }
            else
            {
                if (!obj.activeSelf) obj.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 마지막 fetch 시 사용한 GPS 좌표 반환 (백그라운드 복귀 시 위치 변동 체크용)
    /// </summary>
    public Vector2 GetLastFetchPosition()
    {
        return lastPosition;
    }

    /// <summary>
    /// 기존 오브젝트 전부 제거 후 새 위치 기준으로 데이터 재로드 (위치 대폭 변동 시 사용)
    /// </summary>
    public void FullRefreshFromNewLocation()
    {
        StopAllFetching();

        // 기존 스폰된 오브젝트 전부 제거
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        spawnedObjects.Clear();
        placeDataMap.Clear();
        isDataLoaded = false;
        isFetching = false;

        // 새 GPS로 처음부터 데이터 로드
        fetchCoroutine = StartCoroutine(FetchDataOnce());
        checkPositionCoroutine = StartCoroutine(CheckPositionAndFetchData());
    }

    /// <summary>
    /// 즉시 데이터 새로고침 (업로드 성공 등 외부 호출용)
    /// </summary>
    public void RefreshData()
    {
        StopAllFetching();

#if UNITY_EDITOR
        float lat = VirtualLocation.Instance.Latitude;
        float lon = VirtualLocation.Instance.Longitude;
#else
        // GPS 우선 → lastPosition fallback (서울 기본값 방지)
        float lat = lastPosition.x;
        float lon = lastPosition.y;
        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }
#endif
        fetchCoroutine = StartCoroutine(FetchDataOnce());
        checkPositionCoroutine = StartCoroutine(CheckPositionAndFetchData());
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // 앱 첫 시작 시에는 무시 (Start → FetchDataOnce에서 이미 처리)
        if (!isInitialStartComplete) return;

        if (hasFocus)
        {
            // 설정 화면에서 돌아올 때 패널은 무조건 닫기 (허용/거부 무관)
            if (LocationPermissionManager.Instance != null)
                LocationPermissionManager.Instance.ClosePanel();

            // LoadingManager가 백그라운드 복구 중이면 중복 fetch 방지
            // (HandleBackgroundRecovery에서 위치 변동 체크 + 전체 재로드/앵커 재생성을 이미 처리)
            LoadingManager loadingMgr = FindFirstObjectByType<LoadingManager>();
            if (loadingMgr != null && loadingMgr.IsBackgroundRecovering)
                return;

            if (Input.location.isEnabledByUser)
            {
                // 권한 허용된 경우: 조용히 데이터 갱신
                StartCoroutine(WaitForARSessionAndFetchDataSilent());
            }
            else
            {
                // 권한이 여전히 거부된 경우: 패널 다시 표시
                if (LocationPermissionManager.Instance != null)
                    LocationPermissionManager.Instance.ShowPanel();
            }
        }
    }

    private IEnumerator WaitForARSessionAndFetchDataSilent()
    {
        yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking || Time.unscaledTime > 5f);
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            yield break;
        }

        StopAllFetching();

        // GPS 우선 → lastPosition fallback (서울 기본값 방지)
        float lat = lastPosition.x;
        float lon = lastPosition.y;
        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }

        // lastPosition이 0이면 아직 한번도 GPS를 받지 못한 상태 → skip
        if (lat == 0f && lon == 0f)
        {
            yield break;
        }

        // 기존 visible 오브젝트 수를 기반으로 UI 유지 (카운트 중복 방지)
        if (objectCountUI != null)
        {
            objectCountUI.UpdateObjectCount(GetAllVisibleObjectCount(), false);
        }

        // Light cache 재요청 + 위치 체크 코루틴 재시작
        lastPosition = new Vector2(lat, lon);
        StartCoroutine(FetchLightCache(lat, lon));
        checkPositionCoroutine = StartCoroutine(CheckPositionAndFetchData());

        // AllocationLoop가 10초 주기로 자동 재배분하므로 별도 트리거 불필요
        // (StaggeredReallocation 제거 — RecreateAnchor 중복 호출 부작용 + 중복 부하)
    }

    private IEnumerator WaitForARSessionAndFetchData()
    {
        yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking || Time.unscaledTime > 5f);
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            ShowErrorMessage(R0926LocalizedText.Say("AR 세션을 복구할 수 없습니다.", "Couldn't recover the AR session.", "ARセッションを復旧できません。", "无法恢复 AR 会话。", "No se pudo recuperar la sesión de AR."));
            yield break;
        }

        StopAllFetching();

        // GPS 우선 → 짧게 대기 → lastPosition fallback (서울 기본값 방지)
        float lat = 0f;
        float lon = 0f;
        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }

        // GPS가 아직 안 잡혔으면 짧게 대기
        float waitGPS = 0f;
        while ((lat == 0f && lon == 0f) && waitGPS < 3f)
        {
            yield return new WaitForSeconds(0.5f);
            waitGPS += 0.5f;
            if (Input.location.status == LocationServiceStatus.Running)
            {
                lat = Input.location.lastData.latitude;
                lon = Input.location.lastData.longitude;
            }
        }

        // 그래도 없으면 lastPosition fallback
        if (lat == 0f && lon == 0f)
        {
            lat = lastPosition.x;
            lon = lastPosition.y;
        }

        if (lat != 0f || lon != 0f)
        {
            lastPosition = new Vector2(lat, lon);
        }

        fetchCoroutine = StartCoroutine(FetchDataOnce());
        checkPositionCoroutine = StartCoroutine(CheckPositionAndFetchData());
    }

    private IEnumerator FetchDataImmediately(string url, LocationInfo currentLocation)
    {
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            yield break;
        }
        yield return StartCoroutine(FetchDataFromServer(url, currentLocation));
        fetchCoroutine = StartCoroutine(FetchDataOnce());
    }

    // ============================================================ 
    // AR Geospatial 준비 대기 + 가이드 UI
    // ============================================================ 

    private IEnumerator WaitForGeospatialTracking()
    {
        AREarthManager earthManager = FindFirstObjectByType<AREarthManager>();
        float elapsed = 0f;
        float maxWait = 60f;

        while (elapsed < maxWait)
        {
            if (earthManager == null)
                earthManager = FindFirstObjectByType<AREarthManager>();

            if (earthManager != null && earthManager.EarthTrackingState == TrackingState.Tracking)
            {
                isGeospatialReady = true;
                yield break;
            }

            // 단계별 안내 메시지 변경
            if (elapsed > 30f)
                UpdateARGuideText(R0926LocalizedText.Say("위치 파악이 지연되고 있습니다\n실외로 이동하거나 주변을 둘러봐 주세요", "Finding your location is taking a while\nMove outdoors or look around", "位置の確認に時間がかかっています\n屋外に出るか、周りを見回してください", "定位时间较长\n请移到室外或环顾四周", "Localizarte está tardando\nSal al exterior o mira a tu alrededor"));
            else if (elapsed > 15f)
                UpdateARGuideText(R0926LocalizedText.Say("주변 건물이 보이도록\n핸드폰을 천천히 이동해주세요", "Slowly move your phone\nso nearby buildings are in view", "周りの建物が映るように\nスマホをゆっくり動かしてください", "请缓慢移动手机\n让周围的建筑进入画面", "Mueve el teléfono despacio\npara que se vean los edificios cercanos"));

            elapsed += 1f;
            yield return new WaitForSeconds(1f);
        }

        isGeospatialReady = true;
        UpdateARGuideText(R0926LocalizedText.Say("위치 정확도가 낮을 수 있습니다", "Location accuracy may be low", "位置の精度が低い可能性があります", "位置精度可能较低", "La precisión de la ubicación puede ser baja"));
        yield return new WaitForSeconds(2f);
    }

    private void ShowARGuide(string message)
    {
        GameObject warningObj = GameObject.Find("WarningText");
        if (warningObj == null) return;

        Text warningText = warningObj.GetComponentInChildren<Text>();
        if (warningText != null)
        {
            warningText.text = message;
            warningText.fontSize = arGuideFontSize;
        }
        warningObj.SetActive(true);
    }

    private void HideARGuide()
    {
        GameObject warningObj = GameObject.Find("WarningText");
        if (warningObj != null)
            warningObj.SetActive(false);
    }

    private void UpdateARGuideText(string message)
    {
        GameObject warningObj = GameObject.Find("WarningText");
        if (warningObj == null) return;

        Text warningText = warningObj.GetComponentInChildren<Text>();
        if (warningText != null)
            warningText.text = message;
    }

    private void ShowErrorMessage(string message)
    {
        var errorPanel = GameObject.Find("ErrorPanel")?.GetComponent<Text>();
        if (errorPanel != null)
        {
            errorPanel.text = message;
            errorPanel.gameObject.SetActive(true);
        }
    }

    // 중복 호출 방지용 throttle (LoadingManager 여러 분기 + FilterManager 감속 리프레쉬가 동시 호출 가능)
    private float _lastResumeFetchTime = -10f;
    private const float RESUME_FETCH_THROTTLE = 2f; // 2초 안에 재호출 차단

    /// <summary>
    /// 백그라운드 복귀 후 데이터 갱신 + 위치 모니터링 재시작
    /// </summary>
    public void RestartFetchingAfterResume()
    {
        // 2초 throttle — LoadingManager + FilterManager가 같은 시점에 호출해도 1번만 실행
        float now = Time.realtimeSinceStartup;
        if (now - _lastResumeFetchTime < RESUME_FETCH_THROTTLE)
        {
            // 2초 throttle — 중복 호출 무시
            return;
        }
        _lastResumeFetchTime = now;

        StopAllFetching();

        float lat = lastPosition.x;
        float lon = lastPosition.y;
#if !UNITY_EDITOR
        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }
#endif

        if (lat == 0f && lon == 0f) return;

        // Light cache 재요청 + 위치 체크 코루틴 재시작
        lastPosition = new Vector2(lat, lon);
        StartCoroutine(FetchLightCache(lat, lon));
        checkPositionCoroutine = StartCoroutine(CheckPositionAndFetchData());

        // AllocationLoop가 10초 주기로 자동 재배분하므로 별도 트리거 불필요
        // (StaggeredReallocation 제거 — RecreateAnchor 중복 호출 부작용 + 중복 부하)
    }

    void OnDestroy()
    {
        Input.location.Stop();
    }

    // ============================================================
    // IPlaceCacheProvider 구현
    // ============================================================

    public string FilterKey => "publicData";
    public int MaxCacheSize => 100;
    public bool IsCacheReady => isCacheReady;
    public float LastLoadedAt { get; private set; } = -1f;

    public List<CachedPlaceData> GetCachedPlaces()
    {
        return lightCache;
    }

    public bool SpawnFullObject(string rawId)
    {
        if (!int.TryParse(rawId, out int id)) return false;
        if (HiddenPlaces.IsHidden("dm_" + rawId)) return false;   // 이 기기에서 X 로 숨긴 장소
        if (spawnedObjects.ContainsKey(id)) return true; // 이미 스폰됨

        // placeDataMap에 상세 데이터가 있으면 즉시 스폰
        if (placeDataMap.ContainsKey(id))
        {
            try
            {
                CreateObjectFromData(placeDataMap[id]);
                return spawnedObjects.ContainsKey(id);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DataManager] SpawnFullObject 예외: id={id}: {ex.Message}");
                return false;
            }
        }

        pendingDetailFetchIds.Add(id);
        if (batchDetailCoroutine == null)
        {
            batchDetailCoroutine = StartCoroutine(BatchFetchDetailAndSpawn());
        }
        return false;
    }

    public bool SpawnIndicatorOnly(string rawId)
    {
        if (!int.TryParse(rawId, out int id)) return false;
        if (HiddenPlaces.IsHidden("dm_" + rawId)) return false;
        if (indicatorOnlyObjects.ContainsKey(id)) return true;

        CachedPlaceData cached = lightCache.Find(c => c.rawId == rawId);
        if (cached == null) return false;

        if (indicatorOnlyPrefab == null) return false;

        GameObject obj = GetIndicatorFromPool();
        if (obj == null) return false;

        obj.name = $"Indicator_{id}_{cached.displayName}";
        obj.SetActive(true);

        var target = obj.GetComponentInChildren<Target>(true);
        if (target != null)
        {
            target.PlaceName = cached.displayName;
            target.placeId = id.ToString();
            target.gpsLatitude = cached.latitude;
            target.gpsLongitude = cached.longitude;

            // full 스폰·목록과 같은 값. 흰색이어도 꼭 넣는다 — 풀에서 꺼낸 인디케이터는 앞서 쓴 장소의 색을 들고 있다
            target.TargetColor = ResolvePlaceColor(placeDataMap.TryGetValue(id, out PlaceData known) ? known.color : null, cached.category, cached.displayName);

            // dance_anim은 큐브로 정상 스폰되어 더블탭으로 라우팅됨 — 인디케이터 훅 불필요
        }

        var anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
        if (anchor != null)
        {
            anchor.SetCoordinatesAndCreateAnchor(cached.latitude, cached.longitude, cached.altitude);
        }

        indicatorOnlyObjects[id] = obj;
        return true;
    }

    private GameObject GetIndicatorFromPool()
    {
        while (indicatorOnlyPool.Count > 0)
        {
            GameObject pooled = indicatorOnlyPool.Dequeue();
            if (pooled != null) return pooled;
        }
        return Instantiate(indicatorOnlyPrefab);
    }

    private void ReturnIndicatorToPool(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        indicatorOnlyPool.Enqueue(obj);
    }

    public void DespawnFullObject(string rawId)
    {
        if (!int.TryParse(rawId, out int id)) return;
        if (!spawnedObjects.ContainsKey(id)) return;

        GameObject obj = spawnedObjects[id];
        string modelType = placeDataMap.ContainsKey(id) ? (placeDataMap[id].model_type ?? "cube") : "cube";
        spawnedObjects.Remove(id);
        currentlyLoadingGLB.Remove(id);
        ReturnToPool(obj, modelType);
    }

    public void DespawnIndicatorOnly(string rawId)
    {
        if (!int.TryParse(rawId, out int id)) return;
        if (!indicatorOnlyObjects.ContainsKey(id)) return;

        GameObject obj = indicatorOnlyObjects[id];
        indicatorOnlyObjects.Remove(id);
        ReturnIndicatorToPool(obj);
    }

    public HashSet<string> GetSpawnedFullIds()
    {
        var result = new HashSet<string>();
        foreach (int id in spawnedObjects.Keys)
            result.Add("dm_" + id);
        return result;
    }

    public HashSet<string> GetSpawnedIndicatorIds()
    {
        var result = new HashSet<string>();
        foreach (int id in indicatorOnlyObjects.Keys)
            result.Add("dm_" + id);
        return result;
    }

    /// <summary>
    /// 올린 사람이 고친 장소를 바로 반영 (2026-10-26 '정보 수정') — 서버가 돌려준 값으로 바꾸고,
    /// 자리를 옮겼을 때만 앵커를 다시 세운다(매번 새로 세우면 고치지 않은 장소도 한 번 사라졌다 뜬다).
    /// 3D(GLB) 장소는 모델을 다시 받지 않고 이름·사진 정보만 바꾼다.
    /// </summary>
    public void ApplyEditedPlace(PlaceData edited, bool moved)
    {
        if (edited == null) return;
        if (placeDataMap.TryGetValue(edited.id, out PlaceData old) && old != null)
        {
            edited.original_model_type = string.IsNullOrEmpty(old.original_model_type) ? old.model_type : old.original_model_type;
            edited.model_type = old.model_type;   // GLB 대신 큐브로 떠 있던 것은 그대로
        }
        else edited.original_model_type = edited.model_type;
        placeDataMap[edited.id] = edited;

        string raw = edited.id.ToString();
        var cached = lightCache.Find(c => c.rawId == raw);
        if (cached != null)
        {
            cached.displayName = edited.name;
            cached.category = edited.category;
            cached.petFriendly = edited.pet_friendly;
            cached.latitude = edited.latitude;
            cached.longitude = edited.longitude;
            cached.altitude = edited.altitude;
        }

        if (spawnedObjects.TryGetValue(edited.id, out GameObject obj) && obj != null)
        {
            if (moved)
            {
                var anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
                if (anchor != null) anchor.SetCoordinatesAndCreateAnchor(edited.latitude, edited.longitude, edited.altitude);
            }
            var displayCtrl = obj.GetComponentInChildren<ImageDisplayController>(true);
            if (displayCtrl != null) displayCtrl.SetSubPhotos(FlattenSubPhotos(edited));
            if (edited.model_type == "custom")
            {
                var doubleTap = obj.GetComponentInChildren<DoubleTap3D>(true);
                if (doubleTap != null) SetupDoubleTapInfo(doubleTap, edited);
                var target = obj.GetComponentInChildren<Target>(true);
                if (target != null) SetupTargetInfo(target, edited);
            }
            else SetupCubeObject(obj, edited);
            obj.name = string.Format("Place_{0}_{1}", edited.id, edited.model_type);
        }

        if (indicatorOnlyObjects.TryGetValue(edited.id, out GameObject ind) && ind != null)
        {
            var target = ind.GetComponentInChildren<Target>(true);
            if (target != null)   // SpawnIndicatorOnly 와 같은 값
            {
                target.PlaceName = edited.name;
                target.gpsLatitude = edited.latitude;
                target.gpsLongitude = edited.longitude;
                target.TargetColor = ResolvePlaceColor(edited.color, edited.category, edited.name);   // 분류를 지웠으면 흰색으로
            }
            if (moved)
            {
                var anchor = ind.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
                if (anchor != null) anchor.SetCoordinatesAndCreateAnchor(edited.latitude, edited.longitude, edited.altitude);
            }
            ind.name = $"Indicator_{edited.id}_{edited.name}";
        }

        AfterPlaceChanged();
    }

    /// <summary>
    /// 두 번 누른 오브젝트가 이 매니저가 띄운 우팡 장소면 그 장소 — 아니면 null.
    /// 공공데이터(TourAPI)는 contentid 를 번호로 써서 우팡 장소 번호와 겹칠 수 있다 → 번호만 보지 않고 그 오브젝트인지 본다
    /// </summary>
    public PlaceData PlaceOf(DoubleTap3D tapped)
    {
        if (tapped == null) return null;
        int id = tapped.GetId();
        if (!spawnedObjects.TryGetValue(id, out GameObject obj) || obj == null || !tapped.transform.IsChildOf(obj.transform)) return null;
        return placeDataMap.TryGetValue(id, out PlaceData p) ? p : null;
    }

    /// <summary>올린 사람이 지운 장소를 바로 치운다 — 캐시·3D·박스·목록에서 (빈자리는 FilterManager 가 채운다)</summary>
    public void RemovePlace(int id)
    {
        string raw = id.ToString();
        lightCache.RemoveAll(c => c.rawId == raw);
        pendingDetailFetchIds.Remove(id);
        FilterManager filterMgr = FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null) filterMgr.ForgetPlace("dm_" + raw);
        DespawnFullObject(raw);
        DespawnIndicatorOnly(raw);
        placeDataMap.Remove(id);
        AfterPlaceChanged();
    }

    private void AfterPlaceChanged()
    {
        PlaceListManager plm = FindFirstObjectByType<PlaceListManager>(FindObjectsInactive.Include);
        if (plm != null) plm.UpdateUI();
        FilterManager filterMgr = FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null) filterMgr.TriggerReallocation();   // 분류를 바꿔 필터에서 빠졌거나, 옮겨 거리가 달라졌을 수 있다
    }

    public void RefreshCache(float lat, float lon)
    {
        // 같은 자리(200m 안)를 이미 받는 중이면 그 결과를 쓴다
        if (Time.realtimeSinceStartup < lightCacheBusyUntil
            && CalculateDistance(lightCacheBusyPos.x, lightCacheBusyPos.y, lat, lon) < 200f) return;
        StartCoroutine(FetchLightCache(lat, lon));
    }

    /// <summary>
    /// 카테고리(+장소명) 기반 색상 — AR 인디케이터 · 목록 · 목록 필터 칩 · 올리기 분류 칩 공통 단일 소스.
    ///
    /// 목록 필터에 칩이 있는 분류(샵·음식점·카페·공원·화장실·스포츠·랜드마크)는 칩 색 그대로 — 10-25 대표님
    /// 「각 필터링 분류의 색에 맞게 적절한 인디케이터 색이 반영되어서 목록 및 AR화면에」. 예전엔 올리기 화면·AR·칩이
    /// 서로 달랐다(스포츠 주황/청록/빨강 · 카페 핑크/모카 · 랜드마크 하늘/금색).
    /// 분류 없이 올린 곳과 '기타'는 흰색 (10-25 「체크하지 않고 올려도 … 그냥 흰색」).
    ///
    /// 공공데이터(공공시설)는 카페·음식점과 헷갈리지 않도록 "푸르스름한 차분한 색" 으로 (소방서·경찰서 모두 gov 회청색).
    /// placeName 파라미터는 향후 이름 기반 세분화 여지로 유지.
    /// </summary>
    public static Color GetCategoryColor(string category, string placeName = null)
    {
        if (string.IsNullOrEmpty(category)) return Color.white;
        switch (category)
        {
            // --- 목록 필터 칩이 있는 분류 = 칩 색 (R0926FilterChip.CatColors 가 이 값을 쓴다) ---
            case "shop":     return new Color(0.31f, 0.55f, 1f);       // 파랑 #4F8CFF
            case "food":     return new Color(1f, 0.54f, 0.24f);       // 주황 #FF8A3D
            case "cafe":     return new Color(0.643f, 0.459f, 0.318f); // 모카 #A47551 (10-23)
            case "park":     return new Color(0.22f, 0.85f, 0.54f);    // 초록 #38D98A
            case "toilet":   return new Color(0.61f, 0.48f, 1f);       // 보라 #9C7AFF
            case "sport":    return new Color(0.94f, 0.28f, 0.28f);    // 빨강 #F04747 (10-23)
            case "landmark": return new Color(0.96f, 0.77f, 0.26f);    // 금색 #F5C442

            // --- 공공데이터(기관/시설, publicData 토글) — 푸르스름/녹색, 흰색 금지 ---
            case "gov":      return new Color(0.533f, 0.6f, 0.667f);   // 회청 #8899AA (경찰서·소방서 포함)
            case "utility":  return new Color(0.533f, 0.6f, 0.667f);   // 농협/우체국 → 회청
            case "religious":return new Color(0.533f, 0.6f, 0.667f);   // 사찰/성당 → 회청
            case "edu":      return new Color(0.357f, 0.573f, 0.788f); // 하늘 #5B92C9
            case "culture":  return new Color(0.357f, 0.573f, 0.788f); // 박물관/미술관 → 하늘
            case "medical":  return new Color(0.239f, 0.643f, 0.62f);  // 청록 #3DA49E (빨강에서 변경)
            case "welfare":  return new Color(0.306f, 0.62f, 0.471f);  // 청록그린 #4E9E78

            default:         return Color.white;   // 기타(etc) · 분류 없음
        }
    }

    /// <summary>목록 필터에 칩이 있는 분류 — 이 분류는 서버에 남은 색보다 칩 색이 먼저</summary>
    public static bool IsChipCategory(string category)
        => category == "shop" || category == "food" || category == "cafe" || category == "park"
           || category == "toilet" || category == "sport" || category == "landmark";

    /// <summary>
    /// 칩이 있는 분류는 늘 칩 색 — 예전 앱이 올린 색(스포츠 주황 · 랜드마크 하늘 …)이나 서버가 이름을 바꾼 색
    /// ('purple' → AE53C5)이 남아 있어도 (10-25). 분류 없음·기타는 늘 흰색 — 예전에 올린 노란색(우팡이쉼터 · 구수한농장 …)이
    /// 남아 있어도 (10-27 대표님 「기존에 업로드했던 것들에 있어서 '노란색' 으로 되어 있는 것들 … 흰색 동그라미로」).
    /// 그 밖(공공데이터 기관 등)은 서버 color(HEX) 우선, 비었거나 무효면 카테고리 색.
    /// 풀 오브젝트·인디케이터·목록이 모두 이 한 함수를 거쳐 색이 늘 같다.
    /// </summary>
    public static Color ResolvePlaceColor(string rawColorHex, string category, string placeName = null)
    {
        if (IsChipCategory(category)) return GetCategoryColor(category, placeName);
        if (string.IsNullOrEmpty(category) || category == "etc") return Color.white;
        if (!string.IsNullOrEmpty(rawColorHex))
        {
            string hex = rawColorHex.StartsWith("#") ? rawColorHex : "#" + rawColorHex;
            if (ColorUtility.TryParseHtmlString(hex, out Color parsed))
                return parsed;
        }
        return GetCategoryColor(category, placeName);
    }

    /// <summary>ResolvePlaceColor의 HEX(RRGGBB) 문자열 버전 — 리스트 리치텍스트(&lt;color=#..&gt;)용.</summary>
    public static string ResolvePlaceColorHex(string rawColorHex, string category, string placeName = null)
    {
        return ColorUtility.ToHtmlStringRGB(ResolvePlaceColor(rawColorHex, category, placeName));
    }
}

/// <summary>
/// Light API 응답용 경량 데이터 구조 (이미지/모델 URL 없음)
/// </summary>
[System.Serializable]
public class LightPlaceData
{
    public int id { get; set; }
    public string name { get; set; }
    public double latitude { get; set; }   // double — float 면 AR 앵커 자리가 위도 0.4m·경도 0.7m 단위로 끊긴다 (10-26)
    public double longitude { get; set; }
    public double altitude { get; set; }
    public string category { get; set; }
    public string model_type { get; set; }
    public bool pet_friendly { get; set; }
    public bool separate_restroom { get; set; }
    /// <summary>기관이 등록한 공공데이터인지 (서버가 등록자로 판별 — 옛 서버면 null)</summary>
    public bool? is_public { get; set; }
}

[System.Serializable]
public class PlaceData
{
    public int id { get; set; }
    public string name { get; set; }
    public string main_photo { get; set; }
    public List<List<string>> sub_photos { get; set; }
    public bool pet_friendly { get; set; }
    public bool separate_restroom { get; set; }
    public bool alcohol_available { get; set; } // 주류 판매 여부
    public string instagram_id { get; set; }
    public double latitude { get; set; }   // double — float 면 AR 앵커 자리가 위도 0.4m·경도 0.7m 단위로 끊긴다 (10-26)
    public double longitude { get; set; }
    public double altitude { get; set; }
    public string color { get; set; }
    public string username { get; set; }
    public string model_type { get; set; } = "cube";
    public string original_model_type { get; set; } // 서버에서 받은 원본 값 (필터용)
    public string model_url { get; set; }
    public float model_scale { get; set; } = 1f;
    public string category { get; set; } = ""; // shop, food, cafe, park, toilet, gov, edu, utility, landmark, medical, culture, sport, religious, welfare
    /// <summary>기관이 등록한 공공데이터인지 (서버가 등록자로 판별 — 옛 서버면 null)</summary>
    public bool? is_public { get; set; }
}