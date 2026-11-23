using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Google.XR.ARCoreExtensions;
using Google.XR.ARCoreExtensions.GeospatialCreator;
using UnityEngine.UI;
using System.Text;
using System.Linq;
using UnityEngine.XR.ARFoundation;

public class TourAPIManager : MonoBehaviour, IPlaceCacheProvider, IPlaceCacheLoadTime
{
    // Singleton pattern
    private static TourAPIManager instance;
    public static TourAPIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TourAPIManager>();
                if (instance == null)
                {
                    Debug.LogError("[TourAPIManager] Instance not found in scene!");
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
            Debug.LogWarning("[TourAPIManager] Duplicate instance detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    // 관광공사 API 는 우팡 서버(/proxy)가 대신 부른다 — 인증키는 서버 .env 에만 있고 서버가 붙인다 (앱에 키를 넣지 말 것 · CLAUDE.md 6).
    // 명세: docs/claude/server-spec-1005.md
    private string BASE_URL => ApiConfig.TOUR_API_PROXY;
    private readonly string tourApiUrlTemplate = "{0}/locationBasedList?pageNo=1&numOfRows=100&mapX={1}&mapY={2}&radius={3}&listYN=Y&arrange=A&MobileOS=ETC&MobileApp=AppTest&_type=json";
    private readonly string detailImageUrlTemplate = "{0}/detailImage?contentId={1}&imageYN=Y&numOfRows=10&MobileOS=ETC&MobileApp=AppTest&_type=json";
    private readonly string detailCommonUrlTemplate = "{0}/detailCommon?contentId={1}&defaultYN=Y&addrinfoYN=Y&overviewYN=Y&MobileOS=ETC&MobileApp=AppTest&_type=json&lang=KO";
    private readonly string detailPetTourUrlTemplate = "{0}/detailPetTour?contentId={1}&MobileOS=ETC&MobileApp=AppTest&_type=json";

    // 숙박(호텔 · 펜션 · 모텔 · 민박 · 게스트하우스 · 한옥 …)은 국문 관광정보(KorService2)에서 함께 받는다 — 반려동물 여행 자료엔 애견동반 숙소만 있다 (11-19 L3).
    // 서버 /proxy/stay 가 숙박(32)만 · 같은 키로 부른다. 서버에 그 길이 아직 없으면(404) 예전처럼 반려동물 자료만
    public static string EditorStayBase;   // 플레이 시험 — 숙박 목록 · 상세 주소를 바꾼다 (끝나면 null)
    private string STAY_BASE_URL => string.IsNullOrEmpty(EditorStayBase) ? ApiConfig.TOUR_API_PROXY + "/stay" : EditorStayBase;
    private readonly string stayListUrlTemplate = "{0}/locationBasedList?pageNo=1&numOfRows=100&mapX={1}&mapY={2}&radius={3}&arrange=E&MobileOS=ETC&MobileApp=AppTest&_type=json";
    private string BaseFor(TourPlaceData place) => place != null && place.stayOnly ? STAY_BASE_URL : BASE_URL;
    private readonly HashSet<string> stayDetailTried = new HashSet<string>();   // 숙박 상세 사진은 장소마다 한 번만 묻는다

    public GameObject samplePrefab;

    private Text debugText;
    private StringBuilder debugMessages = new StringBuilder(10000);
    private Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>(100);
    private Dictionary<string, TourPlaceData> placeDataMap = new Dictionary<string, TourPlaceData>(100);
    private Dictionary<string, TourPlaceData> cachedPlaceDetails = new Dictionary<string, TourPlaceData>(50);
    private Dictionary<string, bool> currentFilters;
    private Queue<GameObject> objectPool = new Queue<GameObject>(20);
    [SerializeField] public int poolSize = 50;

    [Header("Progressive Loading Settings")]
    [Tooltip("거리별 로딩 단계 (미터): 1km → 5km (TourAPI 429 에러 방지)")]
    public float[] loadRadii = new float[] { 1000f, 5000f };  // 11개 → 2개로 축소 (API 호출 82% 감소)
    public float tierDelay = 0.5f;
    public float objectSpawnDelay = 0.1f;

    [SerializeField] private float updateDistanceThreshold = 50f;

    [Header("Object Spawn Radius")]
    [Tooltip("이 거리(m) 이내만 3D 오브젝트 생성 + 이미지 다운로드. 밖은 좌표만 저장")]
    [SerializeField] private float objectSpawnRadius = 400f;

    [Header("IndicatorOnly (FilterManager 배분 전용)")]
    [Tooltip("IndicatorOnly 프리팹 (Target 사이즈 등 Inspector에서 조절) — FilterManager가 중앙 배분")]
    [SerializeField] private GameObject indicatorOnlyPrefab;

    // IndicatorOnly 오브젝트 (FilterManager.AllocationLoop이 배분)
    private Dictionary<string, GameObject> indicatorOnlyObjects = new Dictionary<string, GameObject>();

    // 사진을 다 받은 뒤에 띄운다 (11-19 P1 · PlaceSpawnGate) — 준비를 마친 곳을 한 프레임에 하나씩 만든다
    private readonly Queue<string> photoReadyQueue = new Queue<string>();
    private Coroutine photoReadyLoop;
    private readonly Dictionary<string, float> directWanted = new Dictionary<string, float>();   // 반경 안이라 바로 띄우려던 곳(ProcessTourData · 배분과 별개) — 부른 때
    private readonly HashSet<string> photoPrep = new HashSet<string>();   // 숙박 — 사진 주소를 묻는 중
    private FilterManager fmRef;
    private const float DirectWantedFor = 30f;

    private bool isDataLoaded = false;
    private Coroutine fetchCoroutine;
    private Vector2 lastPosition;

    // Light Cache (FilterManager 중앙 배분용)
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

    private static readonly WaitForSeconds waitOneSecond = new WaitForSeconds(1f);
    private static readonly WaitForSeconds waitFiveSeconds = new WaitForSeconds(5f);

    private static readonly Dictionary<SystemLanguage, string> SourceInfoMessages = new Dictionary<SystemLanguage, string>
    {
        { SystemLanguage.Korean, "(한국관광공사에서 제공한 정보입니다)" },
        { SystemLanguage.English, "(Provided by Korea Tourism Organization)" },
        { SystemLanguage.Japanese, "(韓国観光公社提供の情報です)" },
        { SystemLanguage.ChineseSimplified, "(韩国观光公社提供的信息)" },
        { SystemLanguage.Spanish, "(Por Korea Tourism Organization)" }
    };

    private string GetSourceInfoMessage()
    {
        SystemLanguage lang = AppLanguage.AsSystemLanguage(chineseSimplified: true);
        return SourceInfoMessages.ContainsKey(lang) ? SourceInfoMessages[lang] : SourceInfoMessages[SystemLanguage.English];
    }

    void Start()
    {
        LogDebug("[TourAPIManager] Start() 호출됨");
        if (debugText == null)
        {
            LogDebug("[TourAPIManager] 디버그 텍스트 필드가 설정되지 않음! UI 로그 표시 불가");
        }

        // PlayerPrefs에서 필터 상태 초기화 (DataManager 패턴)
        currentFilters = new Dictionary<string, bool>();
        int petState = PlayerPrefs.GetInt("Filter_PetFriendly_V3", 0);
        currentFilters["petFriendlyAll"] = (petState != 1);   // 옛 2(애견동반 안 됨)는 없앰 → 전부
        currentFilters["petFriendlyOnly"] = (petState == 1);
        currentFilters["publicData"] = PlayerPrefs.GetInt("Filter_PublicData_V2", 1) == 1;
        currentFilters["categoryFilter"] = PlayerPrefs.GetInt("Filter_Category_V1", 0) != 0;   // 분류를 골라 둔 채 켜면 그 분류만 (FilterManager 가 곧 다시 넣는다)

        InitializeObjectPool();
        StartCoroutine(StartLocationServiceAndFetchData());

        FilterManager filterMgr = FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null) filterMgr.RegisterCacheProvider(this);
    }

    private void LogDebug(string message)
    {
        debugMessages.AppendLine(message);
        if (debugText != null)
        {
            debugText.text = debugMessages.ToString();
            if (debugMessages.Length > 10000)
            {
                debugMessages.Remove(0, debugMessages.Length - 5000);
            }
            Canvas.ForceUpdateCanvases();
        }
    }

    private void InitializeObjectPool()
    {
        LogDebug("[TourAPIManager] 오브젝트 풀 초기화 시작");
        if (samplePrefab == null)
        {
            LogDebug("[TourAPIManager] samplePrefab이 설정되지 않음! 오브젝트 풀 초기화 실패");
            return;
        }
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
            obj.SetActive(false);
            objectPool.Enqueue(obj);
            LogDebug($"[TourAPIManager] 풀에 오브젝트 추가됨: {obj.name}, 현재 풀 크기: {objectPool.Count}/{poolSize}");
        }
        LogDebug("[TourAPIManager] 오브젝트 풀 초기화 완료");
    }

    private IEnumerator StartLocationServiceAndFetchData()
    {
        // 초기 DB 로드는 FilterManager.RefreshAllCaches()가 중앙 처리 (중복 페치 방지)
#if UNITY_EDITOR
        LogDebug("[Manager] 에디터 모드: 위치 서비스 모의 (Mock)");
        lastPosition = new Vector2(VirtualLocation.Instance.Latitude, VirtualLocation.Instance.Longitude);
        StartCoroutine(CheckPositionAndFetchData());
        yield break;
#else
        LogDebug("[TourAPIManager] 위치 서비스 시작 시도");

        // 권한이 없으면 DataManager의 이벤트를 기다린 후 진행
        if (!Input.location.isEnabledByUser)
        {
            LogDebug("[TourAPIManager] 위치 권한 없음 - DataManager.OnLocationPermissionGranted 대기");
            bool permissionGranted = false;
            System.Action onGranted = () => permissionGranted = true;
            DataManager.OnLocationPermissionGranted += onGranted;
            yield return new WaitUntil(() => permissionGranted);
            DataManager.OnLocationPermissionGranted -= onGranted;
            LogDebug("[TourAPIManager] 위치 권한 획득 확인 - 데이터 로드 재개");
        }

        Input.location.Start();
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            LogDebug("[TourAPIManager] 위치 서비스 초기화 대기 중... 남은 대기 시간: " + maxWait + "초");
            yield return waitOneSecond;
            maxWait--;
        }
        if (Input.location.status == LocationServiceStatus.Failed)
        {
            LogDebug("[TourAPIManager] 위치 서비스 시작 실패 - 데이터 로드 중단");
            yield break;
        }
        LogDebug("[TourAPIManager] 위치 서비스 초기화 성공");
        lastPosition = new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        LogDebug("[TourAPIManager] 초기 위치 설정 완료");
        StartCoroutine(CheckPositionAndFetchData());
#endif
    }

    // RefreshCache 중복 방지 — FilterManager 가 못 받은 목록을 다시 요청하므로, 같은 자리를 받는 중이면 새로 시작하지 않는다.
    // 코루틴이 중간에 멈춰도 영영 막히지 않게 시작 시각으로 판단한다
    private const float PROGRESSIVE_FETCH_MAX_SECONDS = 180f;
    private float progressiveFetchStartedAt = -1000f;
    private Vector2 progressiveFetchPos;

    private IEnumerator FetchDataProgressively(float latitude, float longitude)
    {
        LogDebug("[TourAPIManager] Progressive Loading 시작");
        progressiveFetchStartedAt = Time.realtimeSinceStartup;
        progressiveFetchPos = new Vector2(latitude, longitude);

        foreach (float radius in loadRadii)
        {
            LogDebug($"[TourAPIManager] {radius}m 반경 데이터 로딩 중...");
            string tourApiUrl = string.Format(tourApiUrlTemplate, BASE_URL, longitude, latitude, radius);
            string stayUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture, stayListUrlTemplate, STAY_BASE_URL, longitude, latitude, radius);
            string petJson = null, stayJson = null;
            Coroutine stayFetch = StartCoroutine(FetchText(stayUrl, 2, s => stayJson = s, giveUpOn4xx: true));   // 숙박은 함께 받는다 (11-19 L3)
            yield return StartCoroutine(FetchText(tourApiUrl, 3, s => petJson = s));
            yield return stayFetch;
            // 반려동물 목록을 못 받으면 예전처럼 이번 반경은 건너뛴다(가진 것을 지우지 않는다) — 숙박만 못 받으면 반려동물 자료만으로
            if (petJson != null) yield return StartCoroutine(ProcessTourData(petJson, stayJson, latitude, longitude));
            if (tierDelay > 0) yield return new WaitForSeconds(tierDelay);
        }

        LogDebug("[TourAPIManager] Progressive Loading 완료");
        isDataLoaded = true;
        progressiveFetchStartedAt = -1000f;
    }

    // 위치 추적 전용 (DB 재요청/스폰은 FilterManager가 중앙 처리)
    private IEnumerator CheckPositionAndFetchData()
    {
        while (true)
        {
#if UNITY_EDITOR
            float lat = VirtualLocation.Instance.Latitude;
            float lon = VirtualLocation.Instance.Longitude;
#else
            LocationInfo currentLocation = Input.location.lastData;
            float lat = currentLocation.latitude;
            float lon = currentLocation.longitude;
#endif

            Vector2 currentPos = new Vector2(lat, lon);
            float distanceMoved = CalculateDistance(lastPosition.x, lastPosition.y, currentPos.x, currentPos.y);
            if (distanceMoved > updateDistanceThreshold)
            {
                lastPosition = currentPos;
            }

            yield return waitFiveSeconds;
        }
    }

    /// <summary>
    /// 목록 글을 받는다 — 실패하면 5초 뒤 다시(retryCount 번). 끝내 못 받으면 done(null).
    /// giveUpOn4xx: 서버에 그 길이 없거나(404) 거절하면 다시 묻지 않는다 — 숙박 길이 아직 없는 서버에서 반려동물 자료가 늦어지지 않게 (11-19 L3)
    /// </summary>
    private IEnumerator FetchText(string url, int retryCount, System.Action<string> done, bool giveUpOn4xx = false)
    {
        LogDebug($"[TourAPIManager] 요청: {url}");
        for (int i = 0; i < retryCount; i++)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Accept-Encoding", "");
                request.timeout = 10;
                LogDebug($"[TourAPIManager] 요청 시도 {i + 1}/{retryCount}: {url}, Accept-Encoding 헤더: {request.GetRequestHeader("Accept-Encoding")}");
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    LogDebug($"[TourAPIManager] 응답 수신 성공: {json.Length} 바이트, 상태 코드: {request.responseCode}, 응답 본문: {json.Substring(0, Mathf.Min(json.Length, 500))}...");
                    done(json);
                    yield break;
                }
                LogDebug($"[TourAPIManager] 요청 실패 (시도 {i + 1}/{retryCount}): {request.error}, 응답 코드: {request.responseCode}");
                if (giveUpOn4xx && request.responseCode >= 400 && request.responseCode < 500) break;
                if (i < retryCount - 1)
                {
                    LogDebug("[TourAPIManager] 5초 후 재시도...");
                    yield return waitFiveSeconds;
                }
            }
        }
        done(null);
    }

    /// <summary>
    /// 국문 관광정보에서 받은 숙박을 반려동물 여행 자료에 더한다 (11-19 L3 대표님 「숙박업(펜션·호텔·모텔·민박) 정부 공개 API … '펜션·호텔' 분류로」).
    /// 같은 번호가 반려동물 자료에도 있으면 그쪽(애견동반 · 애견 정보)을 쓴다. 나머지는 애견동반 아님 · 상세는 숙박 길(/proxy/stay)로.
    /// 숙박 목록을 못 받았거나 읽지 못하면 반려동물 자료만 (예전과 같다)
    /// </summary>
    private List<TourPlaceData> MergeStays(List<TourPlaceData> places, string stayJson)
    {
        if (string.IsNullOrEmpty(stayJson)) return places;
        List<TourPlaceData> stays;
        try { stays = JsonConvert.DeserializeObject<TourAPIResponse>(stayJson)?.response?.body?.items?.item; }
        catch (JsonException ex) { LogDebug($"[TourAPIManager] 숙박 JSON 파싱 실패: {ex.Message}"); return places; }
        if (stays == null || stays.Count == 0) return places;
        places = places ?? new List<TourPlaceData>(stays.Count);
        var known = new HashSet<string>();
        foreach (var p in places) if (p != null && !string.IsNullOrEmpty(p.contentid)) known.Add(p.contentid);
        foreach (var s in stays)
        {
            if (s == null || string.IsNullOrEmpty(s.contentid) || s.contenttypeid != "32" || !known.Add(s.contentid)) continue;
            s.petFriendly = false;
            s.stayOnly = true;
            places.Add(s);
        }
        return places;
    }

    private IEnumerator ProcessTourData(string json, string stayJson, float latitude, float longitude)
    {
        LogDebug("[TourAPIManager] ProcessTourData 시작");
        List<TourPlaceData> places = null;

        try
        {
            TourAPIResponse response = JsonConvert.DeserializeObject<TourAPIResponse>(json);
            places = response?.response?.body?.items?.item;
            LogDebug($"[TourAPIManager] 파싱된 장소 수: {places?.Count ?? 0}");
        }
        catch (JsonException ex)
        {
            LogDebug($"[TourAPIManager] JSON 파싱 실패: {ex.Message}\nJSON: {json}");
            yield break;
        }
        places = MergeStays(places, stayJson);
        places?.RemoveAll(p => p == null);

        if (places == null || places.Count == 0)
        {
            LogDebug("[TourAPIManager] TourAPI에서 반환된 장소 데이터가 없음");
            lightCache.Clear();
            MarkCacheReady();
            yield break;
        }

        // 가까운 순 — 숙박을 더해 상한(MaxCacheSize)을 넘으면 먼 곳부터 뺀다 (예전엔 받은 순서(이름순)대로 잘렸다 · 11-19 L3)
        var distOf = new Dictionary<TourPlaceData, float>(places.Count);
        foreach (var p in places) distOf[p] = CalculateDistance(latitude, longitude, p.mapy, p.mapx);
        places.Sort((a, b) => distOf[a].CompareTo(distOf[b]));
        if (places.Count > MaxCacheSize) places.RemoveRange(MaxCacheSize, places.Count - MaxCacheSize);

        // Light 캐시 갱신
        lightCache.Clear();
        foreach (var p in places)
        {
            lightCache.Add(new CachedPlaceData
            {
                uniqueId = "tour_" + p.contentid,
                rawId = p.contentid,
                displayName = p.title ?? "",
                latitude = p.mapy,
                longitude = p.mapx,
                altitude = 0f,
                category = CategoryOf(p),   // 숙박(32)은 '숙소' — 분류를 고르면 배분이 이 값으로 고른다 (11-19 L1)
                sourceManager = "TourAPI",
                modelType = "cube",
                petFriendly = p.petFriendly,   // 반려동물 여행 자료는 애견동반 (false 라 '반려견 되는 곳만'에서 배분이 빠졌다) · 국문 관광정보에서만 온 숙박은 아님 (11-19 L3)
                // 관광공사 자료 = 공공데이터 칩. 빈 값이면 칩이 꺼져도 배분을 받아 (보이지 않으면서) 사람이 올린 곳의 자리를 차지했다
                filterKey = "publicData"
            });
        }
        MarkCacheReady();

        // 상세 정보 요청은 objectSpawnRadius 이내 장소만 수행 (API 호출 절약)
        const int CHUNK_SIZE = 10;
        for (int i = 0; i < places.Count; i += CHUNK_SIZE)
        {
            int endIndex = Mathf.Min(i + CHUNK_SIZE, places.Count);
            for (int j = i; j < endIndex; j++)
            {
                var place = places[j];
                float distance = CalculateDistance(latitude, longitude, place.mapy, place.mapx);
                place.color = ColorHexOf(CategoryOf(place));

                // 숙박(국문 관광정보에서만 온 곳)은 여기서 묻지 않는다 — 배분이 띄울 때 사진만 (SpawnFullObject · 하루 호출 한도). 받아 둔 사진은 이어 쓴다
                if (place.stayOnly)
                {
                    if (cachedPlaceDetails.TryGetValue(place.contentid, out TourPlaceData known) && known.imageUrls != null) place.imageUrls = known.imageUrls;
                }
                // objectSpawnRadius 이내만 상세 API 요청 (이미지, 공통정보, 펫투어)
                else if (distance <= objectSpawnRadius)
                {
                    yield return StartCoroutine(FetchDetailImages(place));
                    yield return StartCoroutine(FetchDetailCommon(place));
                    yield return StartCoroutine(FetchDetailPetTour(place));
                }
            }
            yield return null;
        }

        foreach (TourPlaceData place in places)
        {
            float distance = CalculateDistance(latitude, longitude, place.mapy, place.mapx);

            // 모든 데이터는 placeDataMap에 저장 (좌표/기본정보)
            placeDataMap[place.contentid] = place;

            if (spawnedObjects.ContainsKey(place.contentid))
            {
                // 기존 오브젝트 업데이트
                GameObject existingObj = spawnedObjects[place.contentid];
                if (distance <= objectSpawnRadius)
                {
                    ImageDisplayController display = existingObj.GetComponentInChildren<ImageDisplayController>();
                    if (display != null && !string.IsNullOrEmpty(place.firstimage))
                    {
                        display.SetBaseMap(place.firstimage);
                    }
                    UpdateExistingObject(place, existingObj);
                }
            }
            else if (distance <= objectSpawnRadius && !place.stayOnly)   // 숙박(국문 관광정보)은 배분이 고른 곳만 — '전체'에선 가까운 몇 곳 (FilterManager.maxStayObjects)
            {
                // objectSpawnRadius 이내는 이동 속도와 무관하게 3D 오브젝트 생성 (다른 매니저와 동일 조건) — 한 프레임에 하나씩 (SpawnPace)
                // 사진을 다 받은 뒤에 (11-19 P1) — 받는 중이면 다 받은 뒤 SpawnPhotoReady 가 띄운다. 메인 사진은 CreateObjectFromData 가 붙인다
                directWanted[place.contentid] = Time.realtimeSinceStartup;
                if (!PhotosReady(place)) continue;
                while (!SpawnPace.Ready) yield return null;
                GameObject newObj = spawnedObjects.ContainsKey(place.contentid) ? null : CreateObjectFromData(place);   // 기다리는 사이 배분이 띄웠으면 그대로
                if (newObj != null)
                {
                    SpawnPace.Took();
                    directWanted.Remove(place.contentid);
                    if (objectSpawnDelay > 0) yield return new WaitForSeconds(objectSpawnDelay);
                }
            }
        }

        // objectSpawnRadius × 1.5 밖 오브젝트 풀로 반환 (placeDataMap 유지)
        float cleanupRange = objectSpawnRadius * 1.5f;
        List<string> toRemove = new List<string>();
        foreach (var kvp in spawnedObjects)
        {
            if (!placeDataMap.ContainsKey(kvp.Key)) continue;
            TourPlaceData p = placeDataMap[kvp.Key];
            float dist = CalculateDistance(latitude, longitude, p.mapy, p.mapx);
            if (dist > cleanupRange)
            {
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var id in toRemove)
        {
            GameObject obj = spawnedObjects[id];
            spawnedObjects.Remove(id);
            ReturnToPool(obj);
        }
        LogDebug($"[TourAPIManager] ProcessTourData 완료, placeDataMap 크기: {placeDataMap.Count}, spawnedObjects 크기: {spawnedObjects.Count}");
    }

    // 만들면서 메인 사진을 바로 붙인다 (11-19 P1 — 기본 면이 먼저 보이지 않게). 받아 둔 사진이 있으면 그것, 없으면 예전처럼 받아서.
    // 꺼진 채(필터 · 거리) 만든 곳도 붙인다 — 예전엔 만든 뒤 켜진 것만 찾아 붙여 꺼진 채 만든 곳은 켜도 기본 면이었다
    private GameObject CreateObjectFromData(TourPlaceData place)
    {
        Texture2D tex = PlaceSpawnGate.Take(GateKey(place.contentid), out string src);
        try
        {
            GameObject obj = BuildObject(place);
            if (obj != null && !string.IsNullOrEmpty(place.firstimage))
            {
                ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>(true);
                if (display != null)
                {
                    if (tex != null && src == place.firstimage) { display.SetBaseMapTexture(tex, place.firstimage); tex = null; }
                    else display.SetBaseMap(place.firstimage);
                }
            }
            return obj;
        }
        finally { if (tex != null) Destroy(tex); }
    }

    private GameObject BuildObject(TourPlaceData place)
    {
        if (HiddenPlaces.IsHidden("tour_" + place.contentid)) return null;   // 이 기기에서 X 로 숨긴 장소
        if (samplePrefab == null)
        {
            LogDebug("[TourAPIManager] samplePrefab이 설정되지 않음! 오브젝트 생성 실패");
            return null;
        }
        GameObject newObj = GetFromPool();
        if (newObj == null)
        {
            LogDebug("[TourAPIManager] 풀에서 오브젝트를 가져오지 못함, 새 오브젝트 인스턴스 생성");
            newObj = Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            LogDebug($"[TourAPIManager] 풀에서 오브젝트 가져옴: {newObj.name}");
        }
        // 필터 + 거리 체크
        bool shouldShow = ShouldShow(CategoryOf(place), place.petFriendly);
        if (shouldShow)
        {
            float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
            float lat = 0f, lon = 0f;
#if UNITY_EDITOR
            if (VirtualLocation.Instance != null) { lat = VirtualLocation.Instance.Latitude; lon = VirtualLocation.Instance.Longitude; }
#else
            if (Input.location.status == LocationServiceStatus.Running) { lat = Input.location.lastData.latitude; lon = Input.location.lastData.longitude; }
#endif
            if ((lat != 0f || lon != 0f) && CalculateDistance(lat, lon, place.mapy, place.mapx) > maxDist)
                shouldShow = false;
        }

        // Target 미리 비활성화 (SetActive 시 인디케이터 등록 방지)
        if (!shouldShow)
        {
            Target targetComp = newObj.GetComponentInChildren<Target>(true);
            if (targetComp != null) targetComp.enabled = false;
        }

        newObj.SetActive(true); // 컴포넌트 설정을 위해 활성화
        newObj.name = $"Place_{place.contentid}";
        LogDebug($"[TourAPIManager] 오브젝트 이름 설정: {newObj.name}, 활성화 상태: {newObj.activeSelf}");
        if (SetupObjectComponents(newObj, place))
        {
            if (!shouldShow)
            {
                newObj.SetActive(false);
                Target targetComp = newObj.GetComponentInChildren<Target>(true);
                if (targetComp != null) targetComp.enabled = true;
            }
            ARObjectZoomController.Fit(newObj);   // 핀치 줌 중에 뜨거나 풀에서 다시 꺼내도 지금 배율로
            spawnedObjects[place.contentid] = newObj;
            placeDataMap[place.contentid] = place;
            return newObj;
        }
        LogDebug($"[TourAPIManager] SetupObjectComponents 실패, 오브젝트 풀에 반환: {newObj.name}");
        ReturnToPool(newObj);
        return null;
    }

    private void UpdateExistingObject(TourPlaceData place, GameObject existingObj)
    {
        LogDebug($"[TourAPIManager] UpdateExistingObject 시작 - ID: {place.contentid}, 이름={existingObj.name}");
        if (!SetupObjectComponents(existingObj, place))
        {
            LogDebug($"[TourAPIManager] 오브젝트 업데이트 실패 - ID: {place.contentid}");
        }
        else
        {
            LogDebug($"[TourAPIManager] 오브젝트 업데이트 완료 - ID: {place.contentid}, 이름={existingObj.name}");
        }
    }

    /// <summary>정보 창(DoubleTap3D) 글 — 오브젝트를 띄울 때와 목록 사진으로 열 때(AR 에 아직 없는 장소 — R0926PlaceInfo) 같은 값</summary>
    public void ConfigureInfo(DoubleTap3D doubleTap, TourPlaceData place)
    {
        Sprite petFriendlySprite = Resources.Load<Sprite>("Sprites/pet_friendly_icon") ?? Resources.Load<Sprite>("Sprites/default_icon");
        Sprite restroomSprite = Resources.Load<Sprite>("Sprites/separate_restroom_icon") ?? Resources.Load<Sprite>("Sprites/default_icon");
        if (petFriendlySprite == null) LogDebug("[TourAPIManager] pet_friendly_icon 로드 실패!");
        if (restroomSprite == null) LogDebug("[TourAPIManager] separate_restroom_icon 로드 실패!");

        StringBuilder addressBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(place.addr1)) addressBuilder.Append(place.addr1);
        if (!string.IsNullOrEmpty(place.addr2)) addressBuilder.Append(" ").Append(place.addr2);
        string address = addressBuilder.ToString();
        string overview = place.overview ?? "";
        string petInfo = place.petInfo ?? "";

        int placeId;
        if (!int.TryParse(place.contentid, out placeId))
        {
            LogDebug($"[TourAPIManager] ID {place.contentid}를 int로 변환 실패, 기본값 -1 사용");
            placeId = -1;
        }

        // [UI Update] Description 제거 (상단 텍스트), PlaceInfoText 통합
        string combinedOverview = overview;
        if (!string.IsNullOrEmpty(place.description))
        {
             combinedOverview = place.description + "\n\n" + overview;
        }

        doubleTap.SetInfoImages(
            sprite1: petFriendlySprite, 
            sprite2: restroomSprite, 
            petFriendly: place.petFriendly,   // 숙박(국문 관광정보에서만 온 곳)은 애견동반 아님 (11-19 L3) 
            separateRestroom: true, 
            description: null, // 상단 텍스트 제거
            name: place.title, 
            id: placeId, 
            username: "WOOPANG", // Created By WOOPANG
            instagramId: null, 
            tel: place.tel, 
            address: address, 
            overview: combinedOverview, // 개요에 제공처 정보 포함
            petInfo: petInfo
        );
        LogDebug($"[TourAPIManager] DoubleTap3D 컴포넌트 설정 완료: ID={place.contentid}, Title={place.title}");
    }

    private bool SetupObjectComponents(GameObject obj, TourPlaceData place)
    {
        LogDebug($"[TourAPIManager] SetupObjectComponents 시작 - ID: {place.contentid}, Title: {place.title ?? "null"}");
        DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>();
        if (doubleTap == null)
        {
            LogDebug($"[TourAPIManager] ID {place.contentid} 오브젝트에 DoubleTap3D 없음. 컴포넌트 설정 실패");
            return false;
        }
        ConfigureInfo(doubleTap, place);

        // 사진이 없어도 넘긴다 — 빈 목록이면 상세에서 우팡 서버(/locations/번호)를 찾지 않는다 (관광공사 번호는 우팡 장소 번호가 아니다)
        TourAPIImageController tourImageController = obj.GetComponentInChildren<TourAPIImageController>();
        if (tourImageController != null)
        {
            tourImageController.SetTourImages(place.imageUrls);
            LogDebug($"[TourAPIManager] TourAPIImageController 이미지 설정: ID={place.contentid}, 이미지 수={place.imageUrls.Count}");
        }
        else
        {
            LogDebug($"[TourAPIManager] TourAPIImageController 없음: ID={place.contentid}");
        }

        CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>();
        if (anchor == null)
        {
            LogDebug($"[TourAPIManager] ID {place.contentid} 오브젝트에 CustomARGeospatialCreatorAnchor 없음. 컴포넌트 설정 실패");
            return false;
        }
        anchor.SetCoordinatesAndCreateAnchor(place.mapy, place.mapx, 0);
        LogDebug($"[TourAPIManager] Anchor 설정 완료 - ID: {place.contentid}");

        Target target = obj.GetComponentInChildren<Target>();
        if (target == null)
        {
            LogDebug($"[TourAPIManager] ID {place.contentid} 오브젝트에 Target 컴포넌트 없음. 컴포넌트 설정 실패");
            return false;
        }
        Color placeColor;
        string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;
        if (ColorUtility.TryParseHtmlString($"#{colorHex}", out placeColor))
        {
            target.TargetColor = placeColor;
            LogDebug($"[TourAPIManager] ID {place.contentid} 오브젝트의 색상 설정: {colorHex}");
        }
        else
        {
            LogDebug($"[TourAPIManager] ID {place.contentid}의 색상 파싱 실패: {colorHex}, 기본 색상(흰색)으로 설정");
            target.TargetColor = Color.white;
        }
        if (string.IsNullOrEmpty(place.title))
        {
            LogDebug($"[TourAPIManager] ID {place.contentid}의 title이 비어 있음. 기본 이름 설정.");
            target.PlaceName = R0926LocalizedText.Say("알 수 없는 장소", "Unknown place", "不明な場所", "未知地点", "Lugar desconocido");
        }
        else
        {
            target.PlaceName = place.title;
            LogDebug($"[TourAPIManager] ID {place.contentid} 오브젝트에 장소 이름 설정: {place.title}");
        }
        target.placeId = place.contentid;
        LogDebug($"[TourAPIManager] SetupObjectComponents 완료: ID={place.contentid}");
        return true;
    }

    private float CalculateDistance(double lat1, double lon1, double lat2, double lon2) => (float)GeoHere.Meters(lat1, lon1, lat2, lon2);

    private GameObject GetFromPool()
    {
        LogDebug($"[TourAPIManager] GetFromPool 호출, 풀 크기: {objectPool.Count}");
        if (objectPool.Count > 0)
        {
            GameObject obj = objectPool.Dequeue();
            DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>();
            if (doubleTap != null)
            {
                doubleTap.ResetData();
                LogDebug("[TourAPIManager] DoubleTap3D 데이터 리셋 완료");
            }
            obj.SetActive(true);
            return obj;
        }
        else if (spawnedObjects.Count < poolSize * 2)
        {
            LogDebug("[TourAPIManager] 풀에 오브젝트 없음, 새 오브젝트 생성");
            GameObject obj = Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
            return obj;
        }
        LogDebug("[TourAPIManager] 오브젝트 풀 한계 초과, 새 오브젝트 생성 불가");
        return null;
    }

    private void ReturnToPool(GameObject obj)
    {
        // 풀에 있는 동안 사진을 들고 있지 않는다 — 다시 쓰일 때 새로 받는다
        ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>(true);
        if (display != null) display.ClearImages();
        DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>(true);
        if (doubleTap != null) doubleTap.ReleasePhotos();
        obj.SetActive(false);
        objectPool.Enqueue(obj);
        LogDebug($"[TourAPIManager] 오브젝트 풀에 반환됨: {obj.name}, 풀 크기: {objectPool.Count}");
    }

    private IEnumerator FetchDetailImages(TourPlaceData place)
    {
        if (cachedPlaceDetails.ContainsKey(place.contentid))
        {
            var cached = cachedPlaceDetails[place.contentid];
            place.imageUrls = cached.imageUrls;
            LogDebug($"[TourAPIManager] detailImage 캐시 사용: ID={place.contentid}, 이미지 수={place.imageUrls.Count}");
            yield break;
        }

        string url = string.Format(detailImageUrlTemplate, BaseFor(place), place.contentid);
        LogDebug($"[TourAPIManager] detailImage 요청: {url}");
        int retryCount = 3;
        for (int i = 0; i < retryCount; i++)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Accept-Encoding", "");
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<DetailImageResponse>(request.downloadHandler.text);
                        var items = response?.response?.body?.items?.item;
                        if (items != null)
                        {
                            place.imageUrls = new List<string>(items.Count);
                            foreach (var item in items)
                            {
                                if (!string.IsNullOrEmpty(item.originimgurl))
                                    place.imageUrls.Add(item.originimgurl);
                            }
                            cachedPlaceDetails[place.contentid] = place;
                            LogDebug($"[TourAPIManager] detailImage 성공: ID={place.contentid}, 이미지 수={place.imageUrls.Count}");
                        }
                        else
                        {
                            LogDebug($"[TourAPIManager] detailImage 응답에 이미지 데이터 없음: ID={place.contentid}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        LogDebug($"[TourAPIManager] detailImage JSON 파싱 실패: ID={place.contentid}, 에러: {ex.Message}");
                    }
                    break;
                }
                else
                {
                    LogDebug($"[TourAPIManager] detailImage 요청 실패 (시도 {i + 1}/{retryCount}): ID={place.contentid}, 에러: {request.error}");
                    if (i < retryCount - 1) yield return waitFiveSeconds;
                }
            }
        }
    }

    private IEnumerator FetchDetailCommon(TourPlaceData place)
    {
        if (cachedPlaceDetails.ContainsKey(place.contentid))
        {
            var cached = cachedPlaceDetails[place.contentid];
            place.tel = cached.tel;
            place.addr1 = cached.addr1;
            place.addr2 = cached.addr2;
            place.overview = cached.overview;
            LogDebug($"[TourAPIManager] detailCommon 캐시 사용: ID={place.contentid}");
            yield break;
        }

        string url = string.Format(detailCommonUrlTemplate, BaseFor(place), place.contentid);
        LogDebug($"[TourAPIManager] detailCommon 요청 (국문): {url}");
        int retryCount = 3;
        UnityWebRequest request = null;

        for (int i = 0; i < retryCount; i++)
        {
            request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Accept-Encoding", "");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                break;
            }
            else
            {
                LogDebug($"[TourAPIManager] detailCommon 요청 실패 (시도 {i + 1}/{retryCount}, 국문): ID={place.contentid}, 에러: {request.error}");
                if (i < retryCount - 1) yield return waitFiveSeconds;
            }
        }

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<DetailCommonResponse>(request.downloadHandler.text);
                var item = response?.response?.body?.items?.item?.FirstOrDefault();
                if (item != null)
                {
                    place.tel = item.tel;
                    place.addr1 = item.addr1;
                    place.addr2 = item.addr2;
                    place.overview = item.overview;
                    cachedPlaceDetails[place.contentid] = place;
                    LogDebug($"[TourAPIManager] detailCommon 성공 (국문): ID={place.contentid}, Tel={place.tel}, Addr1={place.addr1}, Overview={item.overview}");
                }
                else
                {
                    LogDebug($"[TourAPIManager] detailCommon 응답에 데이터 없음 (국문): ID={place.contentid}");
                }
            }
            catch (JsonException ex)
            {
                LogDebug($"[TourAPIManager] detailCommon JSON 파싱 실패 (국문): ID={place.contentid}, 에러: {ex.Message}");
            }
        }
    }

    private IEnumerator FetchDetailPetTour(TourPlaceData place)
    {
        if (cachedPlaceDetails.ContainsKey(place.contentid))
        {
            var cached = cachedPlaceDetails[place.contentid];
            place.petInfo = cached.petInfo;
            LogDebug($"[TourAPIManager] detailPetTour 캐시 사용: ID={place.contentid}");
            yield break;
        }

        string url = string.Format(detailPetTourUrlTemplate, BASE_URL, place.contentid);
        LogDebug($"[TourAPIManager] detailPetTour 요청: {url}");
        int retryCount = 3;
        UnityWebRequest request = null;

        for (int i = 0; i < retryCount; i++)
        {
            request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Accept-Encoding", "");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                break;
            }
            else
            {
                LogDebug($"[TourAPIManager] detailPetTour 요청 실패 (시도 {i + 1}/{retryCount}): ID={place.contentid}, 에러: {request.error}");
                if (i < retryCount - 1) yield return waitFiveSeconds;
            }
        }

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<DetailPetTourResponse>(request.downloadHandler.text);
                var item = response?.response?.body?.items?.item?.FirstOrDefault();
                if (item != null)
                {
                    StringBuilder petInfoBuilder = new StringBuilder();
                    if (!string.IsNullOrEmpty(item.acmpyPsblCpam)) petInfoBuilder.AppendLine(item.acmpyPsblCpam);
                    if (!string.IsNullOrEmpty(item.acmpyNeedMtr)) petInfoBuilder.AppendLine(item.acmpyNeedMtr);
                    if (!string.IsNullOrEmpty(item.etcAcmpyInfo)) petInfoBuilder.AppendLine(item.etcAcmpyInfo);
                    place.petInfo = petInfoBuilder.ToString();
                    cachedPlaceDetails[place.contentid] = place;
                    LogDebug($"[TourAPIManager] detailPetTour 성공: ID={place.contentid}, PetInfo={place.petInfo}");
                }
                else
                {
                    LogDebug($"[TourAPIManager] detailPetTour 응답에 데이터 없음: ID={place.contentid}");
                }
            }
            catch (JsonException ex)
            {
                LogDebug($"[TourAPIManager] detailPetTour JSON 파싱 실패: ID={place.contentid}, 에러: {ex.Message}");
            }
        }
    }

    public void SetAllObjectsVisible(bool visible)
    {
        if (visible)
        {
            float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
            float lat = 0f, lon = 0f;
#if UNITY_EDITOR
            if (VirtualLocation.Instance != null) { lat = VirtualLocation.Instance.Latitude; lon = VirtualLocation.Instance.Longitude; }
#else
            if (Input.location.status == LocationServiceStatus.Running) { lat = Input.location.lastData.latitude; lon = Input.location.lastData.longitude; }
#endif
            UpdateDistanceFilter(maxDist, lat, lon);
        }
        else
        {
            foreach (var kvp in spawnedObjects)
            {
                if (kvp.Value != null) kvp.Value.SetActive(false);
            }
        }
    }

    public Dictionary<string, TourPlaceData> GetPlaceDataMap()
    {
        LogDebug($"[TourAPIManager] GetPlaceDataMap 호출, 반환된 데이터 크기: {placeDataMap.Count}");
        return placeDataMap;
    }

    public int GetSpawnedObjectsCount()
    {
        LogDebug($"[TourAPIManager] GetSpawnedObjectsCount 호출, 반환된 오브젝트 수: {spawnedObjects.Count}");
        return spawnedObjects.Count;
    }

    public int GetVisibleObjectCount()
    {
        int count = 0;
        foreach (var kvp in spawnedObjects)
            if (kvp.Value != null && kvp.Value.activeSelf) count++;
        return count;
    }

    public Dictionary<string, GameObject> GetSpawnedObjects()
    {
        return spawnedObjects;
    }

    public Dictionary<string, GameObject> GetIndicatorOnlyObjects() => indicatorOnlyObjects;

    public bool IsDataLoaded()
    {
        LogDebug($"[TourAPIManager] IsDataLoaded 호출, 데이터 로드 상태: {isDataLoaded}");
        return isDataLoaded;
    }

    /// <summary>
    /// 특정 ID의 스폰된 오브젝트 가져오기
    /// </summary>
    public GameObject GetSpawnedObject(string placeId)
    {
        return spawnedObjects.ContainsKey(placeId) ? spawnedObjects[placeId] : null;
    }

    /// <summary>
    /// 앵커 생성에 실패한 오브젝트만 선별하여 재시도
    /// FilterManager.AllocationLoop가 매 tick(2s) 호출 — AR Limited 구간에서 실패한 항목을 자동 복원
    /// </summary>
    public void RetryFailedAnchors()
    {
        RetryFailedAnchorsIn(spawnedObjects);
        RetryFailedAnchorsIn(indicatorOnlyObjects);
    }

    private static void RetryFailedAnchorsIn(Dictionary<string, GameObject> objects)
    {
        if (objects == null) return;
        foreach (var kvp in objects)
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
    /// 관광공사 자료의 분류 — 숙박(관광 타입 32: 펜션 · 호텔 · 캠핑 …)은 '숙소', 나머지는 관광 (11-19 L1 대표님 「목록 카테고리에 '펜션 / 호텔' 추가」 · 같은 날 이름은 「'숙소' 라고」).
    /// 목록 줄 그림 · AR 배분 · 오브젝트 보이기가 이 값 하나를 본다
    /// </summary>
    public static string CategoryOf(TourPlaceData p) => p != null && p.contenttypeid == "32" ? "stay" : "tour";

    /// <summary>오브젝트 · 인디케이터 색 — 숙박은 '숙소' 색(DataManager.GetCategoryColor 하나), 나머지는 예전 관광 노랑</summary>
    public static string ColorHexOf(string category) => category == "stay" ? ColorUtility.ToHtmlStringRGB(DataManager.GetCategoryColor("stay")) : "FBC15D";

    private string CategoryOfId(string rawId)
    {
        if (rawId != null && placeDataMap.TryGetValue(rawId, out TourPlaceData p)) return CategoryOf(p);
        var c = rawId != null ? lightCache.Find(x => x.rawId == rawId) : null;
        return c != null ? c.category : "tour";
    }

    /// <summary>
    /// 현재 필터 상태에 따라 오브젝트를 표시할지 결정 — 분류를 고른 동안은 그 분류만 (공공데이터 칩은 보지 않는다 — 배분 · 목록과 같은 규칙),
    /// '전체'면 공공데이터 칩. 애견동반은 ShouldShow 가 먼저 본다 (반려동물 여행 자료는 모두 애견동반 · 국문 관광정보 숙박은 아님 — 11-19 L3).
    /// 예전엔 분류를 고르면 공공데이터가 꺼진 값으로 와서 관광공사 자료가 통째로 숨었다 (11-19 L1 — 숙박은 '숙소'로 떠야 한다)
    /// </summary>
    private bool ShouldShowByFilter(string category)
    {
        if (currentFilters == null) return true;

        bool showPetFriendly = currentFilters.ContainsKey("petFriendlyAll") && currentFilters["petFriendlyAll"] ||
                               currentFilters.ContainsKey("petFriendlyOnly") && currentFilters["petFriendlyOnly"];
        if (!currentFilters.ContainsKey("petFriendlyAll") && !currentFilters.ContainsKey("petFriendlyOnly"))
        {
            showPetFriendly = !currentFilters.ContainsKey("petFriendly") || currentFilters["petFriendly"];
        }
        if (!showPetFriendly) return false;

        if (currentFilters.TryGetValue("categoryFilter", out bool categoryActive) && categoryActive)
        {
            string want = FilterManager.ActiveCategoryNow;
            return string.IsNullOrEmpty(want) || want == category;
        }
        return !currentFilters.ContainsKey("publicData") || currentFilters["publicData"];
    }

    /// <summary>애견동반을 켰을 때 국문 관광정보에서만 온 숙박(애견동반 아님)은 숨긴다 — 목록 · 배분(PetAllowed)과 같은 규칙 (11-19 L3). 나머지는 ShouldShowByFilter</summary>
    private bool ShouldShow(string category, bool pet)
    {
        if (!pet && currentFilters != null && currentFilters.TryGetValue("petFriendlyOnly", out bool petOnly) && petOnly) return false;
        return ShouldShowByFilter(category);
    }

    private bool PetOfId(string rawId)
    {
        if (rawId != null && placeDataMap.TryGetValue(rawId, out TourPlaceData p)) return p.petFriendly;
        var c = rawId != null ? lightCache.Find(x => x.rawId == rawId) : null;
        return c == null || c.petFriendly;
    }

    /// <summary>
    /// 필터 적용 - 스폰된 AR 오브젝트의 표시/숨김 제어
    /// 반려동물 여행 자료는 모두 애견동반 · 국문 관광정보 숙박은 아니다(ShouldShow) — '전체'면 publicData 칩대로, 분류를 고르면 그 분류(숙박은 '숙소')만
    /// </summary>
    public void ApplyFilters(Dictionary<string, bool> filters)
    {
        if (filters == null) return;

        // 필터 상태 저장 (오브젝트 생성 시 참조)
        currentFilters = new Dictionary<string, bool>(filters);

        int shown = 0;
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value == null) continue;
            bool show = ShouldShow(CategoryOfId(kvp.Key), PetOfId(kvp.Key));
            kvp.Value.SetActive(show);
            if (show) shown++;
        }

        // 경량 인디케이터 오브젝트에도 필터 적용
        foreach (var kvp in indicatorOnlyObjects)
        {
            if (kvp.Value != null) kvp.Value.SetActive(ShouldShow(CategoryOfId(kvp.Key), PetOfId(kvp.Key)));
        }

        LogDebug($"[TourAPIManager] 필터 적용: 보이는 오브젝트 {shown}/{spawnedObjects.Count}");
    }

    /// <summary>
    /// DataManager에서 호출하는 거리 필터 업데이트 메서드
    /// </summary>
    public void SetAllRenderersVisible(bool visible)
    {
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value == null) continue;
            CustomARGeospatialCreatorAnchor anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor != null)
                anchor.SetForceHideRenderers(!visible);
        }
    }

    public void UpdateDistanceFilter(float maxDistance, float currentLat, float currentLon)
    {
        // 필터로 숨는 곳(공공데이터 칩 꺼짐 · 다른 분류를 고름)은 거리와 무관하게 숨김
        foreach (var kvp in spawnedObjects)
        {
            string id = kvp.Key;
            GameObject obj = kvp.Value;
            if (obj == null) continue;

            if (!ShouldShow(CategoryOfId(id), PetOfId(id)))
            {
                if (obj.activeSelf) obj.SetActive(false);
                continue;
            }

            if (placeDataMap.ContainsKey(id))
            {
                TourPlaceData place = placeDataMap[id];
                float dist = CalculateDistance(currentLat, currentLon, place.mapy, place.mapx);

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

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isDataLoaded)
        {
            LogDebug("[TourAPIManager] 앱 포그라운드 복귀 - 캐시 새로고침");
            if (fetchCoroutine != null)
            {
                StopCoroutine(fetchCoroutine);
                fetchCoroutine = null;
            }

#if UNITY_EDITOR
            float lat = VirtualLocation.Instance.Latitude;
            float lon = VirtualLocation.Instance.Longitude;
#else
            LocationInfo currentLocation = Input.location.lastData;
            float lat = currentLocation.latitude;
            float lon = currentLocation.longitude;
#endif

            fetchCoroutine = StartCoroutine(FetchDataProgressively(lat, lon));
        }
    }

    private void CleanupAllIndicatorOnlyObjects()
    {
        foreach (var kvp in indicatorOnlyObjects)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        indicatorOnlyObjects.Clear();
    }

    public int GetIndicatorOnlyCount() => indicatorOnlyObjects.Count;

    void OnDestroy()
    {
        CleanupAllIndicatorOnlyObjects();
        Input.location.Stop();
        LogDebug("[TourAPIManager] 위치 서비스 중지");
    }

    // ============================================================
    // IPlaceCacheProvider 구현
    // ============================================================

    public string FilterKey => "publicData";
    public int MaxCacheSize => 200;   // 반려동물 여행 자료(한 번에 100) + 국문 관광정보 숙박(100) — 11-19 L3
    public bool IsCacheReady => isCacheReady;
    public float LastLoadedAt { get; private set; } = -1f;

    public List<CachedPlaceData> GetCachedPlaces() => lightCache;

    public bool SpawnFullObject(string rawId)
    {
        if (HiddenPlaces.IsHidden("tour_" + rawId)) return false;   // 이 기기에서 X 로 숨긴 장소
        if (spawnedObjects.ContainsKey(rawId)) return true;
        if (!placeDataMap.ContainsKey(rawId)) return false;

        TourPlaceData place = placeDataMap[rawId];
        if (!PhotosReady(place)) return false;   // 사진을 받는 중 — FilterManager 가 임시 화살표를 둔다 · 다 받으면 SpawnPhotoReady 가 (11-19 P1)
        return CreateObjectFromData(place) != null;
    }

    // ── 사진을 다 받은 뒤에 띄우기 (11-19 대표님 P1 · PlaceSpawnGate) ──
    // 「메인사진, 서브사진까지 모두 불러오고나서 … 완료가 되면 오브젝트를 생성」 — 큐브 앞면(firstimage)은 메모리로 · 정보 창 사진(imageUrls)은 디스크로.
    // 숙박(국문 관광정보)은 사진 주소부터 묻는다 — 배분이 띄울 곳만 · 처음 한 번 (관광공사 하루 호출 한도 · 11-19 L3)

    private static string GateKey(string contentId) => "tour_" + contentId;

    /// <summary>사진을 다 받았으면(받을 것이 없으면) true. 아니면 준비를 시작하고 false — 다 받으면 SpawnPhotoReady 가 띄운다</summary>
    private bool PhotosReady(TourPlaceData place)
    {
        if (place == null || string.IsNullOrEmpty(place.contentid)) return false;
        string id = place.contentid;
        string key = GateKey(id);
        if (PlaceSpawnGate.Ready(key)) return true;
        if (PlaceSpawnGate.Busy(key) || photoPrep.Contains(id)) return false;
        if (place.stayOnly && !stayDetailTried.Contains(id))
        {
            photoPrep.Add(id);
            StartCoroutine(PrepareStay(place));
            return false;
        }
        string main = string.IsNullOrEmpty(place.firstimage) ? null : place.firstimage;
        if (main == null && (place.imageUrls == null || place.imageUrls.Count == 0)) return true;
        PlaceSpawnGate.Prepare(key, main, place.imageUrls, () => WantsSpawn(id), OnPhotosReady);
        return PlaceSpawnGate.Ready(key);   // 받을 것이 이미 다 디스크에 있으면 바로 끝난다
    }

    /// <summary>숙박 — 상세 사진 주소를 물은 뒤 그 사진까지 받는다 (예전엔 띄운 뒤에 물어 정보 창에만 넣었다)</summary>
    private IEnumerator PrepareStay(TourPlaceData place)
    {
        string id = place.contentid;
        stayDetailTried.Add(id);
        yield return StartCoroutine(FetchDetailImages(place));
        photoPrep.Remove(id);
        if (WantsSpawn(id) && PhotosReady(place)) OnPhotosReady(GateKey(id));
    }

    /// <summary>아직 띄울 곳인가 — 반경 안이라 바로 띄우려던 곳이거나 배분이 3D 로 고른 곳. 숨겼거나 · 이미 떴으면 false</summary>
    private bool WantsSpawn(string id)
    {
        if (this == null || id == null || spawnedObjects.ContainsKey(id) || !placeDataMap.ContainsKey(id) || HiddenPlaces.IsHidden(GateKey(id))) return false;
        if (directWanted.TryGetValue(id, out float at) && Time.realtimeSinceStartup - at < DirectWantedFor) return true;
        if (fmRef == null) fmRef = FindAnyObjectByType<FilterManager>(FindObjectsInactive.Include);
        return fmRef == null || !fmRef.isActiveAndEnabled || fmRef.WantsFull(GateKey(id));
    }

    private void OnPhotosReady(string key)
    {
        if (this == null || key == null || key.Length <= 5) return;
        photoReadyQueue.Enqueue(key.Substring(5));
        if (photoReadyLoop == null && isActiveAndEnabled) photoReadyLoop = StartCoroutine(SpawnPhotoReady());
    }

    // 사진을 다 받은 곳을 한 프레임에 하나씩 (SpawnPace — 배분 · 반경 안 바로 띄우기와 같은 문)
    private IEnumerator SpawnPhotoReady()
    {
        yield return null;   // 부른 자리(배분 중)에서 바로 만들지 않는다
        while (photoReadyQueue.Count > 0)
        {
            while (!SpawnPace.Ready) yield return null;
            string id = photoReadyQueue.Dequeue();
            string key = GateKey(id);
            if (!WantsSpawn(id)) { PlaceSpawnGate.Drop(key); directWanted.Remove(id); continue; }
            if (!PlaceSpawnGate.Ready(key)) continue;   // 그새 다른 길로 가져갔다 · 새로 준비하는 중
            if (indicatorOnlyObjects.ContainsKey(id)) DespawnIndicatorOnly(id);   // 받는 동안 둔 임시 화살표
            GameObject obj = null;
            try { obj = CreateObjectFromData(placeDataMap[id]); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TourAPIManager] CreateObjectFromData 예외: id={id}: {ex.Message}");
            }
            if (obj != null) { SpawnPace.Took(); directWanted.Remove(id); }
        }
        photoReadyLoop = null;
    }

    void OnDisable()
    {
        photoReadyLoop = null;   // 꺼지면 코루틴이 멈춘다 — 다음 준비 완료 때 다시 시작하게
        photoPrep.Clear();       // 숙박 사진 주소 묻기도 멈췄다
    }

    public bool SpawnIndicatorOnly(string rawId)
    {
        if (HiddenPlaces.IsHidden("tour_" + rawId)) return false;   // 이 기기에서 X 로 숨긴 장소
        if (indicatorOnlyObjects.ContainsKey(rawId)) return true;
        if (indicatorOnlyPrefab == null) return false;

        CachedPlaceData cached = lightCache.Find(c => c.rawId == rawId);
        if (cached == null) return false;

        GameObject obj = Instantiate(indicatorOnlyPrefab);
        obj.name = $"Indicator_Tour_{cached.displayName}";
        obj.SetActive(true);

        var target = obj.GetComponentInChildren<Target>(true);
        if (target != null)
        {
            target.PlaceName = cached.displayName;
            target.placeId = cached.rawId;
            target.gpsLatitude = cached.latitude;
            target.gpsLongitude = cached.longitude;
            Color tourColor;
            if (ColorUtility.TryParseHtmlString("#" + ColorHexOf(cached.category), out tourColor))
                target.TargetColor = tourColor;   // 숙박은 '숙소' 색
        }

        var anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
        if (anchor != null)
            anchor.SetCoordinatesAndCreateAnchor(cached.latitude, cached.longitude, 0);

        indicatorOnlyObjects[rawId] = obj;
        return true;
    }

    public void DespawnFullObject(string rawId)
    {
        if (!spawnedObjects.ContainsKey(rawId)) return;

        ReturnToPool(spawnedObjects[rawId]);
        spawnedObjects.Remove(rawId);
    }

    public void DespawnIndicatorOnly(string rawId)
    {
        if (!indicatorOnlyObjects.ContainsKey(rawId)) return;

        if (indicatorOnlyObjects[rawId] != null) Destroy(indicatorOnlyObjects[rawId]);
        indicatorOnlyObjects.Remove(rawId);
    }

    public HashSet<string> GetSpawnedFullIds()
    {
        var result = new HashSet<string>();
        foreach (string id in spawnedObjects.Keys) result.Add("tour_" + id);
        return result;
    }

    public HashSet<string> GetSpawnedIndicatorIds()
    {
        var result = new HashSet<string>();
        foreach (string id in indicatorOnlyObjects.Keys) result.Add("tour_" + id);
        return result;
    }

    public void RefreshCache(float lat, float lon)
    {
        if (Time.realtimeSinceStartup - progressiveFetchStartedAt < PROGRESSIVE_FETCH_MAX_SECONDS
            && CalculateDistance(progressiveFetchPos.x, progressiveFetchPos.y, lat, lon) < 200f) return;   // 같은 자리를 받는 중
        StartCoroutine(FetchDataProgressively(lat, lon));
    }

}

[System.Serializable]
public class TourPlaceData
{
    public string contentid { get; set; }
    public string contenttypeid { get; set; }   // 관광공사 관광 타입 — 32 숙박(펜션 · 호텔 · 캠핑 …) → '숙소' 분류 (11-19 L1)
    public string title { get; set; }
    public double mapx { get; set; }   // 경도 — double (AR 앵커 자리, 10-26)
    public double mapy { get; set; }   // 위도
    public string firstimage { get; set; }
    public string firstimage2 { get; set; }   // 관광공사 작은 대표 사진 — 목록 줄 사진 (R0926PlaceRows)
    public string description { get; set; }
    public string color { get; set; }
    public List<string> imageUrls { get; set; }
    public string tel { get; set; }
    public string addr1 { get; set; }
    public string addr2 { get; set; }
    public string overview { get; set; }
    public string petInfo { get; set; }
    [JsonIgnore] public bool petFriendly { get; set; } = true;   // 반려동물 여행 자료 = 애견동반 · 국문 관광정보에서만 온 숙박은 false (11-19 L3)
    [JsonIgnore] public bool stayOnly { get; set; }               // 국문 관광정보(숙박)에서만 온 곳 — 상세는 숙박 길(/proxy/stay) · 배분이 고른 곳만 띄운다

    public TourPlaceData()
    {
        switch (AppLanguage.AsSystemLanguage(chineseSimplified: true))
        {
            case SystemLanguage.Korean:
                description = "(한국관광공사에서 제공한 정보입니다)";
                break;
            case SystemLanguage.Japanese:
                description = "(韓国観光公社提供の情報です)";
                break;
            case SystemLanguage.ChineseSimplified:
                description = "(韩国观光公社提供的信息)";
                break;
            case SystemLanguage.Spanish:
                description = "(Por Korea Tourism Organization)";
                break;
            default:
                description = "(Provided by Korea Tourism Organization)";
                break;
        }
        imageUrls = new List<string>();
    }
}

[System.Serializable]
public class TourAPIResponse
{
    public TourAPIResponseBody response { get; set; }
}

[System.Serializable]
public class TourAPIResponseBody
{
    public TourAPIResponseBodyContent body { get; set; }
}

[System.Serializable]
public class TourAPIResponseBodyContent
{
    public TourAPIItems items { get; set; }
}

[System.Serializable]
public class TourAPIItems
{
    public List<TourPlaceData> item { get; set; }
}

[System.Serializable]
public class DetailImageResponse
{
    public DetailImageResponseBody response { get; set; }
}

[System.Serializable]
public class DetailImageResponseBody
{
    public DetailImageResponseBodyContent body { get; set; }
}

[System.Serializable]
public class DetailImageResponseBodyContent
{
    public DetailImageItems items { get; set; }
    public int numOfRows { get; set; }
    public int pageNo { get; set; }
    public int totalCount { get; set; }
}

[System.Serializable]
public class DetailImageItems
{
    public List<DetailImageItem> item { get; set; }
}

[System.Serializable]
public class DetailImageItem
{
    public string contentid { get; set; }
    public string originimgurl { get; set; }
    public string imgname { get; set; }
    public string smallimageurl { get; set; }
    public string cpyrhtDivCd { get; set; }
    public string serialnum { get; set; }
}

[System.Serializable]
public class DetailCommonResponse
{
    public DetailCommonResponseBody response { get; set; }
}

[System.Serializable]
public class DetailCommonResponseBody
{
    public DetailCommonResponseBodyContent body { get; set; }
}

[System.Serializable]
public class DetailCommonResponseBodyContent
{
    public DetailCommonItems items { get; set; }
    public int numOfRows { get; set; }
    public int pageNo { get; set; }
    public int totalCount { get; set; }
}

[System.Serializable]
public class DetailCommonItems
{
    public List<DetailCommonItem> item { get; set; }
}

[System.Serializable]
public class DetailCommonItem
{
    public string contentid { get; set; }
    public string tel { get; set; }
    public string addr1 { get; set; }
    public string addr2 { get; set; }
    public string overview { get; set; }
}

[System.Serializable]
public class DetailPetTourResponse
{
    public DetailPetTourResponseBody response { get; set; }
}

[System.Serializable]
public class DetailPetTourResponseBody
{
    public DetailPetTourResponseBodyContent body { get; set; }
}

[System.Serializable]
public class DetailPetTourResponseBodyContent
{
    public DetailPetTourItems items { get; set; }
    public int numOfRows { get; set; }
    public int pageNo { get; set; }
    public int totalCount { get; set; }
}

[System.Serializable]
public class DetailPetTourItems
{
    public List<DetailPetTourItem> item { get; set; }
}

[System.Serializable]
public class DetailPetTourItem
{
    public string contentid { get; set; }
    public string acmpyPsblCpam { get; set; }
    public string acmpyNeedMtr { get; set; }
    public string etcAcmpyInfo { get; set; }
}