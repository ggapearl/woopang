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

public class DataManager : MonoBehaviour
{
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
    
    [SerializeField] public int poolSize = 50;

    [Header("Progressive Loading Settings")]
    [Tooltip("거리별 로딩 단계 (미터)")]
    public float[] loadRadii = new float[] { 25f, 50f, 75f, 100f, 150f, 200f, 500f, 1000f, 2000f, 5000f, 10000f };

    [Tooltip("각 거리 단계 사이의 딜레이 (초)")]
    public float tierDelay = 0.5f;

    [Tooltip("같은 단계 내 오브젝트 사이의 딜레이 (초)")]
    public float objectSpawnDelay = 0.2f;

    [SerializeField] private float updateDistanceThreshold = 50f;

    [Header("빠른 이동 모드 설정")]
    [Tooltip("이 시간(초) 이내에 refreshThresholdCount회 새로고침 발생 시 빠른 이동 모드 진입")]
    [SerializeField] private float rapidRefreshWindow = 60f;
    [Tooltip("빠른 이동 모드 진입 조건 — 새로고침 횟수")]
    [SerializeField] private int rapidRefreshThresholdCount = 2;
    [Tooltip("빠른 이동 모드 자동 해제 시간 (초)")]
    [SerializeField] private float rapidModeResetInterval = 600f;

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

    // ============================================================
    // 빠른 이동 모드 — 1분 이내 4회 새로고침 시 ObjectCountUI 억제
    // ============================================================
    private List<float> recentRefreshTimes = new List<float>();
    private bool isRapidMovementMode = false;
    private float rapidModeStartTime = 0f;
    public bool IsRapidMovementMode => isRapidMovementMode;

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

            Debug.Log("[DataManager] 초기 로드 미완료 — 재시도");
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
        int petState = PlayerPrefs.GetInt("Filter_PetFriendly_V3", 0); // 0:All, 1:Only, 2:No
        currentFilters["petFriendlyAll"] = (petState == 0);
        currentFilters["petFriendlyOnly"] = (petState == 1);
        currentFilters["noPetFriendly"] = (petState == 2);
        
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

        // Cube 오브젝트 풀 초기화 (5개씩 생성하고 프레임 양보)
        for (int i = 0; i < poolSize; i++)
        {
            GameObject cubeObj = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);
            cubeObj.SetActive(false);
            cubeObjectPool.Enqueue(cubeObj);

            if (i % 5 == 4) yield return null; // 5개마다 프레임 양보
        }

        // GLB 오브젝트 풀 초기화 (5개씩 생성하고 프레임 양보)
        for (int i = 0; i < poolSize; i++)
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
        // 위치 권한이 아직 없으면 최대 30초 대기 (첫 설치 시 권한 요청 팝업 뜨는 시간)
        if (!Input.location.isEnabledByUser)
        {
            float waited = 0f;
            while (!Input.location.isEnabledByUser && waited < 30f)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
        }

        // 30초 대기 후에도 권한 없으면 → 설정 안내 패널 표시 후 대기
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
            Debug.Log("[DataManager] 위치 권한 없음 — 기본 위치로 데이터 로드 진행");
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
        // FetchDataPeriodically 내부에서 WaitUntil(SessionTracking)으로 대기하므로
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
        // Phase1: 서버에서 데이터만 수집 (오브젝트 생성 X)
        List<PlaceData> preFetchedData = new List<PlaceData>();
        yield return StartCoroutine(PreFetchAllTiers(lat, lon, preFetchedData));

        // fallback 활성화 알림 → LoadingManager가 fallback UI 시작 (새 데이터가 있을 때만)
        if (preFetchedData.Count > 0)
        {
            OnPreFetchCompleted?.Invoke();
        }

        // fallback 활성화 후 오브젝트 순차 생성
        yield return StartCoroutine(SpawnPreFetchedObjects(preFetchedData, false));

        // 에디터에서도 초기 로드 완료 표시 (FirstInstallRetry 재시도 방지)
        isInitialStartComplete = true;
        isDataLoaded = true;
        isFetching = false;
#else
        // ============================================================
        // Phase 1: GPS lat/lon으로 서버 데이터 선행 수집 (Geospatial 대기 없이)
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

        // 그래도 없으면 lastPosition fallback (Start()에서 이미 받아놓은 GPS)
        if (lat == 0f && lon == 0f)
        {
            lat = lastPosition.x;
            lon = lastPosition.y;
        }

        if (lat != 0f || lon != 0f)
        {
            lastPosition = new Vector2(lat, lon);
        }

        // Phase1: 서버에서 데이터만 수집 (오브젝트 생성 X)
        List<PlaceData> preFetchedData = new List<PlaceData>();
        yield return StartCoroutine(PreFetchAllTiers(lat, lon, preFetchedData));

        // fallback 활성화 알림 → LoadingManager가 fallback UI 시작 (새 데이터가 있을 때만)
        if (preFetchedData.Count > 0)
        {
            OnPreFetchCompleted?.Invoke();
        }

        // fallback 활성화 후 오브젝트 순차 생성
        yield return StartCoroutine(SpawnPreFetchedObjects(preFetchedData, false));

        // Phase1 완료 시점에서 초기 로드 완료로 표시
        // - isInitialStartComplete: FirstInstallRetry 재시도 방지
        // - isDataLoaded: CheckPositionAndFetchData 위치 모니터링 시작
        isInitialStartComplete = true;
        isDataLoaded = true;

        // ============================================================
        // Phase 2: AR 세션 + Geospatial 대기 → 고도값 기반 정밀 배치
        // ============================================================
        yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking);

        if (!isGeospatialReady)
        {
            ShowARGuide("주변을 천천히 둘러보세요\n위치를 파악하고 있습니다...");
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
        // WaitForGeospatialTracking이 타임아웃되면 isGeospatialReady=true이지만 Earth는 아직 None일 수 있음
        var earthMgr = FindFirstObjectByType<AREarthManager>();

        if (earthMgr != null && earthMgr.EarthTrackingState == TrackingState.Tracking)
        {
            RecreateAllAnchors();
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
    /// 모든 Tier에서 서버 데이터만 수집 (오브젝트 생성 X)
    /// </summary>
    private IEnumerator PreFetchAllTiers(float lat, float lon, List<PlaceData> outData)
    {
        HashSet<int> loadedIds = new HashSet<int>(spawnedObjects.Keys);

        for (int tierIndex = 0; tierIndex < loadRadii.Length; tierIndex++)
        {
            float radius = loadRadii[tierIndex];
            string serverUrl = string.Format("{0}&lat={1}&lon={2}&radius={3}", baseServerUrl, lat, lon, radius);

            List<PlaceData> tierPlaces = new List<PlaceData>();
            yield return StartCoroutine(FetchDataFromServerForTier(serverUrl, lat, lon, loadedIds, tierPlaces));

            foreach (var place in tierPlaces)
            {
                loadedIds.Add(place.id);
                outData.Add(place);
            }
        }
    }

    /// <summary>
    /// 사전 수집된 데이터로 오브젝트를 순차 생성
    /// </summary>
    private IEnumerator SpawnPreFetchedObjects(List<PlaceData> places, bool silent)
    {
        if (!silent && objectCountUI != null)
        {
            objectCountUI.ResetUI();
        }

        foreach (PlaceData place in places)
        {
            try
            {
                CreateObjectFromData(place);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DataManager] CreateObjectFromData 예외: id={place.id}, name={place.name}: {ex.Message}");
            }

            if (objectCountUI != null)
            {
                objectCountUI.UpdateObjectCount(GetAllVisibleObjectCount(), false);
            }

            if (objectSpawnDelay > 0)
            {
                yield return new WaitForSeconds(objectSpawnDelay);
            }
        }

        // 최종 업데이트
        if (objectCountUI != null)
        {
            objectCountUI.UpdateObjectCount(GetAllVisibleObjectCount(), true);
        }

        isDataLoaded = true;
        // isFetching은 FetchDataOnce/FetchDataProgressively 호출자에서 관리
    }

    private IEnumerator CheckPositionAndFetchData()
    {
        int myGeneration = fetchGeneration;

        // 최초 데이터 로드 완료까지 대기
        yield return new WaitUntil(() => isDataLoaded || myGeneration != fetchGeneration);

        while (true)
        {
            // 세대 체크: StopAllFetching으로 무효화된 코루틴은 즉시 종료
            if (myGeneration != fetchGeneration) yield break;

#if UNITY_EDITOR
            float lat = VirtualLocation.Instance.Latitude;
            float lon = VirtualLocation.Instance.Longitude;
#else
            // GPS가 Running이면 바로 사용, 아니면 이번 사이클 skip
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
                TrackRefreshForRapidMode();
                yield return StartCoroutine(FetchDataProgressively(lat, lon));
                lastPosition = currentPos;
            }
            yield return new WaitForSeconds(5f); // 5초마다 체크 (1초는 너무 빈번)
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

        HashSet<int> loadedIds = new HashSet<int>(spawnedObjects.Keys);

        // 빠른 이동 모드에서는 ObjectCountUI 표시 억제 (오브젝트 자체는 정상 스폰)
        bool suppressCountUI = isRapidMovementMode;

        // UI 리셋 (새로운 로드 시작) — silent 모드 또는 빠른 이동 모드에서는 UI 표시 안함
        if (!silent && !suppressCountUI && objectCountUI != null)
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

            // 새로운 오브젝트를 하나씩 스폰
            foreach (PlaceData place in newPlaces)
            {
                CreateObjectFromData(place);
                loadedIds.Add(place.id);

                // UI 업데이트 — 빠른 이동 모드에서는 억제
                if (!suppressCountUI && objectCountUI != null)
                {
                    objectCountUI.UpdateObjectCount(GetAllVisibleObjectCount(), false);
                }

                if (objectSpawnDelay > 0)
                {
                    yield return new WaitForSeconds(objectSpawnDelay);
                }
            }

            // 마지막 Tier 완료 시 최종 업데이트
            if (tierIndex == loadRadii.Length - 1 && !suppressCountUI && objectCountUI != null)
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
    /// MaxDisplayDistance × 1.5 범위 밖의 오브젝트를 풀로 반환하여 메모리 관리
    /// 여유 범위(×1.5)를 두어 이동 시 미리 로드된 오브젝트가 자연스럽게 보이도록 함
    /// </summary>
    private void CleanupStaleObjects(float lat, float lon)
    {
        float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
        float cleanupRange = maxDist * 1.5f;

        List<int> toRemove = new List<int>();
        foreach (var kvp in spawnedObjects)
        {
            int id = kvp.Key;
            if (!placeDataMap.ContainsKey(id)) continue;
            PlaceData place = placeDataMap[id];
            float dist = CalculateDistance(lat, lon, place.latitude, place.longitude);
            if (dist > cleanupRange)
            {
                toRemove.Add(id);
            }
        }

        foreach (int id in toRemove)
        {
            GameObject obj = spawnedObjects[id];
            string modelType = placeDataMap.ContainsKey(id) ? (placeDataMap[id].model_type ?? "cube") : "cube";
            spawnedObjects.Remove(id);
            placeDataMap.Remove(id);
            currentlyLoadingGLB.Remove(id);
            ReturnToPool(obj, modelType);
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
                            Debug.Log($"[DataManager] 개별 파싱으로 {places.Count}/{jArray.Count}건 복구");
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
                        ShowErrorMessage("서버에서 데이터를 받아오지 못했습니다.");
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
            ShowErrorMessage("데이터 파싱에 실패했습니다.");
            yield break;
        }

        if (places == null || places.Count == 0)
        {
            ShowErrorMessage("서버에서 데이터를 받아오지 못했습니다.");
            yield break;
        }

        // 거리 순으로 정렬하고 개수 제한
        places.Sort((a, b) => CalculateDistance(currentLocation.latitude, currentLocation.longitude, a.latitude, a.longitude)
            .CompareTo(CalculateDistance(currentLocation.latitude, currentLocation.longitude, b.latitude, b.longitude)));
        places = places.Take(poolSize * 2).ToList();

        // 청크 단위로 처리
        const int CHUNK_SIZE = 5; // 청크 크기 줄임
        for (int i = 0; i < places.Count; i += CHUNK_SIZE)
        {
            var chunk = places.Skip(i).Take(CHUNK_SIZE).ToList();
            foreach (PlaceData place in chunk)
            {
                // 서버 데이터 상세 로그 제거 (lat, lon, distance, type 등)
                // Debug.Log($"[DataManager] 데이터 처리: ID={place.id}, 이름={place.name}"); // 디버깅용
                
                if (!spawnedObjects.ContainsKey(place.id))
                {
                    CreateObjectFromData(place);
                    if (spawnedObjects.ContainsKey(place.id))
                    {
                        placeDataMap[place.id] = place;
                        // Debug.Log($"[DataManager] 맵에 추가됨: ID={place.id}");
                    }
                }
                else
                {
                    UpdateExistingObject(place, spawnedObjects[place.id]);
                    placeDataMap[place.id] = place;
                    // Debug.Log($"[DataManager] 맵 업데이트됨: ID={place.id}");
                }
            }
            yield return null; // 프레임 양보
        }

        // 범위 밖 오브젝트 제거
        HashSet<int> receivedIds = new HashSet<int>(places.Select(p => p.id));
        List<int> toRemove = spawnedObjects.Keys.Where(id => !receivedIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            GameObject obj = spawnedObjects[id];
            PlaceData placeData = placeDataMap.ContainsKey(id) ? placeDataMap[id] : null;
            string modelType = placeData?.model_type ?? "cube";
            spawnedObjects.Remove(id);
            placeDataMap.Remove(id);
            currentlyLoadingGLB.Remove(id);
            ReturnToPool(obj, modelType);
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
        FilterManager filterMgr = FindFirstObjectByType<FilterManager>();
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

        // 3단계 애견동반 필터 처리
        bool petFriendlyAll = currentFilters.ContainsKey("petFriendlyAll") && currentFilters["petFriendlyAll"];
        bool petFriendlyOnly = currentFilters.ContainsKey("petFriendlyOnly") && currentFilters["petFriendlyOnly"];
        bool noPetFriendly = currentFilters.ContainsKey("noPetFriendly") && currentFilters["noPetFriendly"];

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

        // 공공데이터 필터: 공공 카테고리(gov, edu 등)인 경우 publicData 토글에 따라 표시/숨김
        string cat = place.category ?? "";
        bool isPublicCategory = FilterManager.PublicDataCategories.Contains(cat);
        if (isPublicCategory)
        {
            if (!showPublicData) return false;
        }

        bool shouldShow = showWoopangData;

        if (shouldShow)
        {
            // 애견동반 필터 적용
            if (petFriendlyOnly && !place.pet_friendly)
            {
                shouldShow = false;
            }
            else if (noPetFriendly && place.pet_friendly)
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

        ImageDisplayController displayCtrl = obj.GetComponentInChildren<ImageDisplayController>(true);
        if (displayCtrl != null && place.sub_photos != null && place.sub_photos.Count > 0)
        {
            List<string> allSubPhotos = new List<string>();
            foreach (var photoGroup in place.sub_photos)
            {
                if (photoGroup != null)
                {
                    foreach (var photo in photoGroup)
                    {
                        if (!string.IsNullOrEmpty(photo)) allSubPhotos.Add(photo);
                    }
                }
            }
            displayCtrl.SetSubPhotos(allSubPhotos);
        }

        if (place.model_type == "cube") return SetupCubeObject(obj, place);
        else if (place.model_type == "custom") return SetupGLBObject(obj, place);
        else return SetupCubeObject(obj, place);
    }

    private bool SetupCubeObject(GameObject obj, PlaceData place)
    {
        ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>(true);
        if (display != null && !string.IsNullOrEmpty(place.main_photo)) display.SetBaseMap(place.main_photo);

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
        
        string fullUrl = ApiConfig.MAIN_SERVER + "/" + place.model_url;
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
        Color placeColor;
        string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;
        if (ColorUtility.TryParseHtmlString($"#{colorHex}", out placeColor)) target.TargetColor = placeColor;
        else target.TargetColor = Color.white;
        target.PlaceName = place.name;
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

    // ============================================================
    // 빠른 이동 모드 — 새로고침 빈도 추적 + 자동 진입/해제
    // ============================================================

    /// <summary>
    /// 새로고침 시각을 기록하고, 1분 이내 4회 이상이면 빠른 이동 모드 진입
    /// </summary>
    private void TrackRefreshForRapidMode()
    {
        float now = Time.realtimeSinceStartup;

        // 빠른 이동 모드 10분 자동 해제 체크
        if (isRapidMovementMode && (now - rapidModeStartTime) >= rapidModeResetInterval)
        {
            isRapidMovementMode = false;
            recentRefreshTimes.Clear();
        }

        // 이미 빠른 이동 모드면 추가 체크 불필요
        if (isRapidMovementMode) return;

        recentRefreshTimes.Add(now);

        // 윈도우 밖의 오래된 기록 제거
        recentRefreshTimes.RemoveAll(t => (now - t) > rapidRefreshWindow);

        if (recentRefreshTimes.Count >= rapidRefreshThresholdCount)
        {
            isRapidMovementMode = true;
            rapidModeStartTime = now;
        }
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

    private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000;
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) + Mathf.Cos(Mathf.Deg2Rad * (lat1)) * Mathf.Cos(Mathf.Deg2Rad * (lat2)) * Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
        return R * c;
    }

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
        ShowErrorMessage("너무 많은 장소가 로드되었습니다.");
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
        obj.SetActive(false);
        targetPool.Enqueue(obj);
    }

    public Dictionary<int, GameObject> GetSpawnedObjects() => spawnedObjects;
    public int GetSpawnedObjectsCount() => spawnedObjects.Count;
    public Dictionary<int, PlaceData> GetPlaceDataMap() => placeDataMap;
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
    /// 백그라운드 복귀 시 모든 스폰된 오브젝트의 Geospatial 앵커를 재생성
    /// 오브젝트/컴포넌트는 유지하고 앵커만 재연결 (서버 재요청 불필요)
    /// 앵커 재생성 성공 시 Renderer가 자동 표시됨 (CustomARGeospatialCreatorAnchor.SetVisible)
    /// </summary>
    public void RecreateAllAnchors()
    {
        int recreated = 0;
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value == null) continue;

            CustomARGeospatialCreatorAnchor anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor != null)
            {
                // 렌더러 먼저 비활성화 → SetActive(true) 시 원점(Vector3.zero)에서 플래시 방지
                // RecreateAnchor() 내부에서 앵커 생성 성공 시 SetVisible(true) 호출
                Renderer[] renderers = kvp.Value.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers) r.enabled = false;

                kvp.Value.SetActive(true);
                anchor.RecreateAnchor();
                recreated++;
            }
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
                RecreateAllAnchors();
                yield break;
            }

            elapsed += 1f;
            yield return new WaitForSeconds(1f);
        }

    }

    public void UpdateDistanceFilter(float maxDistance, float currentLat, float currentLon)
    {
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

        // ResetUI를 호출하지 않는 FetchDataProgressively 사용 (UI 표시 없이 데이터만 갱신)
        fetchCoroutine = StartCoroutine(FetchDataProgressivelySilent(lat, lon));
        checkPositionCoroutine = StartCoroutine(CheckPositionAndFetchData());
    }

    private IEnumerator WaitForARSessionAndFetchData()
    {
        yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking || Time.unscaledTime > 5f);
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            ShowErrorMessage("AR 세션을 복구할 수 없습니다.");
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
                UpdateARGuideText("위치 파악이 지연되고 있습니다\n실외로 이동하거나 주변을 둘러봐 주세요");
            else if (elapsed > 15f)
                UpdateARGuideText("주변 건물이 보이도록\n핸드폰을 천천히 이동해주세요");

            elapsed += 1f;
            yield return new WaitForSeconds(1f);
        }

        isGeospatialReady = true;
        UpdateARGuideText("위치 정확도가 낮을 수 있습니다");
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

    void OnDestroy()
    {
        Input.location.Stop();
    }
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
    public float latitude { get; set; }
    public float longitude { get; set; }
    public float altitude { get; set; }
    public string color { get; set; }
    public string username { get; set; }
    public string model_type { get; set; } = "cube";
    public string original_model_type { get; set; } // 서버에서 받은 원본 값 (필터용)
    public string model_url { get; set; }
    public float model_scale { get; set; } = 1f;
    public string category { get; set; } = ""; // shop, food, cafe, park, toilet, gov, edu, utility, landmark, medical, culture, sport, religious, welfare
}