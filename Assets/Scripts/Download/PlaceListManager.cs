using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class PlaceListManager : MonoBehaviour
{
    public DataManager dataManager;
    public TourAPIManager tourAPIManager;
    
    [Header("New Public Data Managers")]
    public TerminalManager terminalManager;
    public TrainStationManager trainManager;
    public SubwayManager subwayManager;

    [Header("P2P User Manager")]
    public P2PManager p2pManager;

    public Text listText;

    [Header("UI Update Settings")]
    [SerializeField] private GameObject listPanel;
    [SerializeField] private PlaceListSkeletonLoader skeletonLoader;
    [SerializeField] private float updateInterval = 10f;

    [Header("List Size Limits")]
    [Tooltip("리스트에 표시할 최대 항목 수 (거리 가까운 순). 성능 보호용 상한 — DB가 커져도 이 값 이상은 안 만짐")]
    [SerializeField] private int maxListEntries = 1000;

    [Header("Distance Control")]
    [SerializeField] private Slider distanceSlider;
    [SerializeField] private Text distanceValueText;
    private float maxDisplayDistance;

    private List<(object place, float distance, string id, string displayText, string colorHex)> combinedPlaces = new List<(object, float, string, string, string)>();

    // 매 프레임 거리/정렬만 갱신용 — 풀빌드 시 채우고 Update()에서 GPS 변동분만 반영
    // targetTf가 살아있으면 카메라 transform 기준 (OffScreenIndicator와 동일 — 매끄러움 GPS udpate freq 무관)
    // 비어있으면 GPS baseLat/baseLon fallback (먼 POI, 스폰 안 된 것)
    private class LiveEntry
    {
        public string id;
        public string baseLabel;     // 거리 빼고 표시명만 (예: "스타벅스" 또는 "@user")
        public string colorHex;
        public float baseLat;
        public float baseLon;
        public Transform targetTf;   // 스폰된 GameObject가 있으면 카메라 거리 우선
        public Target target;        // 같은 오브젝트의 Target (목록 줄 누르기·방향)
    }
    private List<LiveEntry> liveEntries = new List<LiveEntry>();

    /// <summary>목록 글자의 장소 한 줄 — 글자와 같은 순서·같은 개수 (R0926PlaceRows 가 줄 누르기·방향 표시에 쓴다)</summary>
    public struct ListEntry
    {
        public string id;        // DataManager·TourAPI·P2P id, 공공교통은 "종류_이름"
        public string name;      // 글자 줄의 이름 그대로 (P2P 는 '@이름')
        public float distance;   // m
        public float lat;
        public float lon;
        public Target target;    // 목록을 새로 만들 때 찾은 AR 오브젝트 — 없거나 사라졌을 수 있다 (FindTarget 으로 다시 찾는다)
    }
    private readonly List<ListEntry> shownEntries = new List<ListEntry>();
    /// <summary>listText 의 장소 줄과 같은 순서의 장소 정보. 글자는 예전 그대로다 (다른 코드가 글자를 읽는다)</summary>
    public IReadOnlyList<ListEntry> ShownEntries => shownEntries;
    private System.Text.StringBuilder liveBuilder = new System.Text.StringBuilder(2048);
    private (float d, int idx)[] orderedBuffer = new (float, int)[64];
    private float lastGpsLat;
    private float lastGpsLon;
    private bool hasLiveSnapshot = false;
    private Camera arCameraCache;
    private string lastDisplayedText = null;       // UI Text mesh rebuild 방지 — 변경 시에만 set
    private bool wasListPanelActive = false;       // listPanel 활성 전환 감지 → 즉시 풀빌드
    private int dataLoadRetryAttempts = 0;         // 데이터 비어있을 때 재시도 카운터
    private const int MAX_DATA_LOAD_RETRIES = 3;   // 최대 3회 (총 3초)

    // Stats
    private int woopangCount;
    private int tourAPICount;
    private int publicTransportCount;
    private int p2pUserCount;

    // P2P 사용자 색상 (핑크)
    private const string P2P_USER_COLOR = "E95383";

    // 교통 수단별 색상
    private const string TERMINAL_COLOR = "00FF00";
    private const string TRAIN_COLOR = "00FF00";
    private const string SUBWAY_COLOR = "3DA29C";

    // 카테고리 색상은 DataManager.GetCategoryColor / ResolvePlaceColorHex 로 단일화
    // (인디케이터·풀오브젝트·리스트가 같은 소스를 써서 항상 일치)

    private Coroutine updatePeriodicCoroutine;

    private Dictionary<string, bool> activeFilters = new Dictionary<string, bool>
    {
        { "woopangData", true },
        { "petFriendly", true },
        { "publicData", true },
        { "subway", true },
        { "bus", true },
        { "p2pUsers", true }
    };

    private Dictionary<string, Dictionary<string, string>> languageTexts = new Dictionary<string, Dictionary<string, string>>
    {
        { "en", new Dictionary<string, string> {
            { "petFriendly", "[PetFriendly]" }, { "noImage", "[No Image]" },
            { "woopangData", "WOOPANG DATA" }, { "tourApiData", "Public Data" },
            { "transportData", "TRANSPORT DATA" }, { "p2pUserData", "NEARBY USERS" },
            { "noNearbyData", "No nearby data found.\nTry adjusting the distance slider or moving to a different area." },
            { "noInternet", "No internet connection.\nNearby places will load automatically once you're back online." },
            { "listNotLoaded", "Couldn't load nearby places yet.\nWe'll try again automatically." }
        }},
        { "ko", new Dictionary<string, string> {
            { "petFriendly", "[애견동반]" }, { "noImage", "[이미지없음]" },
            { "woopangData", "우팡 데이터" }, { "tourApiData", "공공데이터" },
            { "transportData", "대중교통 데이터" }, { "p2pUserData", "근처 사용자" },
            { "noNearbyData", "주변에 데이터가 없습니다.\n거리 슬라이더를 조정하거나 다른 위치로 이동해보세요." },
            { "noInternet", "인터넷 연결이 없어요.\n연결되면 주변 장소를 저절로 불러와요." },
            { "listNotLoaded", "주변 장소를 아직 받지 못했어요.\n잠시 뒤 저절로 다시 불러와요." }
        }},
        { "ja", new Dictionary<string, string> {
            { "petFriendly", "[ペット同伴]" }, { "noImage", "[画像なし]" },
            { "woopangData", "WOOPANGデータ" }, { "tourApiData", "公共データ" },
            { "transportData", "交通データ" }, { "p2pUserData", "近くのユーザー" },
            { "noInternet", "インターネットに接続されていません。\n接続すると周辺の場所を自動で読み込みます。" },
            { "listNotLoaded", "周辺の場所をまだ読み込めていません。\nしばらくすると自動で再読み込みします。" }
        }},
        { "zh", new Dictionary<string, string> {
            { "petFriendly", "[宠物友好]" }, { "noImage", "[无图片]" },
            { "woopangData", "WOOPANG数据" }, { "tourApiData", "公共数据" },
            { "transportData", "交通数据" }, { "p2pUserData", "附近用户" },
            { "noInternet", "没有网络连接。\n连接后会自动加载附近地点。" },
            { "listNotLoaded", "暂时无法加载附近地点。\n稍后会自动重试。" }
        }},
        { "es", new Dictionary<string, string> {
            { "petFriendly", "[Mascotas]" }, { "noImage", "[Sin imagen]" },
            { "woopangData", "Datos WOOPANG" }, { "tourApiData", "Datos Públicos" },
            { "transportData", "Datos de Transporte" }, { "p2pUserData", "Usuarios Cercanos" },
            { "noInternet", "Sin conexión a internet.\nLos lugares cercanos se cargarán automáticamente al reconectar." },
            { "listNotLoaded", "Aún no se pudieron cargar los lugares cercanos.\nLo intentaremos de nuevo automáticamente." }
        }}
    };

    void Start()
    {
        HiddenPlaces.Hidden += OnPlaceHidden;
        // 슬라이더 유무와 무관하게 기본값 보장 — 슬라이더 없으면 maxDisplayDistance=0이 되어 모든 POI 걸리는 버그 방지
        maxDisplayDistance = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);

        if (distanceSlider != null)
        {
            distanceSlider.minValue = 100f;
            distanceSlider.maxValue = 10000f;
            distanceSlider.value = maxDisplayDistance;
            distanceSlider.onValueChanged.AddListener(OnDistanceSliderChanged);
            UpdateDistanceValueText();

            // FilterManager IndicatorOnly 반경 초기 동기화 (FilterManager가 비활성일 수 있으므로 Include)
            FilterManager filterMgr = UnityEngine.Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
            if (filterMgr != null) filterMgr.SetIndicatorRadius(maxDisplayDistance);
        }

        StartCoroutine(InitializeAndUpdateUI());
    }

    private string GetLocalizedText(string key)
    {
        // 예전엔 한국어가 아니면 전부 영어였다 — 일본어·중국어·스페인어 표도 쓰고, 없는 문구만 영어로
        string lang = AppLanguage.Code;
        if (languageTexts.TryGetValue(lang, out var t) && t.TryGetValue(key, out var v)) return v;
        return languageTexts["en"].TryGetValue(key, out var en) ? en : key;
    }

    private IEnumerator InitializeAndUpdateUI()
    {
        if (skeletonLoader != null) skeletonLoader.ShowSkeletonLoader();
        
        // Wait for data (Simplified check)
        yield return new WaitForSeconds(2f);

        if (skeletonLoader != null) skeletonLoader.HideSkeletonAndShowText();
        UpdateUI();
        updatePeriodicCoroutine = StartCoroutine(UpdateUIPeriodically());
    }

    private IEnumerator UpdateUIPeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            if (listPanel != null && listPanel.activeInHierarchy) UpdateUI();
        }
    }

    private Coroutine updateUICoroutine;

    // 인디케이터 X 로 숨긴 장소는 목록에서도 뺀다 (HiddenPlaces — 앱을 다시 켜면 초기화)
    private void OnPlaceHidden(string uniqueId)
    {
        if (isActiveAndEnabled) UpdateUI();
    }

    private void OnDestroy()
    {
        HiddenPlaces.Hidden -= OnPlaceHidden;
        if (updatePeriodicCoroutine != null) StopCoroutine(updatePeriodicCoroutine);
        if (updateUICoroutine != null) StopCoroutine(updateUICoroutine);
        if (distanceSlider != null) distanceSlider.onValueChanged.RemoveListener(OnDistanceSliderChanged);
    }

    public void UpdateUI()
    {
        if (updateUICoroutine != null) StopCoroutine(updateUICoroutine);
        updateUICoroutine = StartCoroutine(UpdateUIWithFadeIn());
    }

    private IEnumerator UpdateUIWithFadeIn()
    {
        float lat = 36.6361f; float lon = 126.8280f; // Default fallback
#if UNITY_EDITOR
        // 에디터에서는 VirtualLocation 사용
        if (VirtualLocation.Instance != null) {
            lat = VirtualLocation.Instance.Latitude;
            lon = VirtualLocation.Instance.Longitude;
        }
#else
        if (Input.location.status == LocationServiceStatus.Running) {
            lat = Input.location.lastData.latitude; lon = Input.location.lastData.longitude;
        }
#endif

        combinedPlaces.Clear();
        liveEntries.Clear();
        lastGpsLat = lat; lastGpsLon = lon;
        woopangCount = 0; tourAPICount = 0; publicTransportCount = 0; p2pUserCount = 0;

        bool petFriendlyOnly = activeFilters.GetValueOrDefault("petFriendlyOnly", false);
        bool petFriendlyAll = activeFilters.GetValueOrDefault("petFriendlyAll", true);
        bool noPetFriendly = activeFilters.GetValueOrDefault("noPetFriendly", false);
        bool showPublic = activeFilters.GetValueOrDefault("publicData", true);
        bool showSubway = activeFilters.GetValueOrDefault("subway", true);
        bool showTrain = activeFilters.GetValueOrDefault("train", true);
        bool showTerminal = activeFilters.GetValueOrDefault("terminal", true);
        bool showObject3D = activeFilters.GetValueOrDefault("object3D", true);
        bool categoryFilterActive = activeFilters.GetValueOrDefault("categoryFilter", false);

        // 카테고리 필터 값 가져오기
        string activeCategoryFilter = "";
        FilterManager filterMgr = UnityEngine.Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null)
            activeCategoryFilter = filterMgr.GetActiveCategoryFilter();

        // 1. Woopang Data (lightCache 기반 — placeDataMap에 있으면 상세 데이터 활용)
        if (dataManager != null) {
            HashSet<int> addedIds = new HashSet<int>();
            var placeDataMap = dataManager.GetPlaceDataMap();

            // 1a. lightCache에서 전체 목록 빌드
            foreach (var cached in dataManager.GetLightCache()) {
                if (!int.TryParse(cached.rawId, out int id)) continue;
                if (HiddenPlaces.IsHidden("dm_" + cached.rawId)) continue;
                string cat = cached.category ?? "";
                string modelType = cached.modelType ?? "cube";

                if (!showObject3D && modelType == "custom") continue;
                if (petFriendlyOnly && !cached.petFriendly) continue;
                if (noPetFriendly && cached.petFriendly) continue;
                if (categoryFilterActive && !string.IsNullOrEmpty(activeCategoryFilter))
                {
                    if (cat != activeCategoryFilter) continue;
                }

                float d = CalculateDistance(lat, lon, cached.latitude, cached.longitude);
                if (d <= maxDisplayDistance) {
                    bool isPublicData = FilterManager.PublicDataCategories.Contains(cat);
                    if (isPublicData)
                    {
                        // 카테고리 필터 활성 시 publicData 토글 무시 (카테고리 매칭이 이미 필터링)
                        if (!categoryFilterActive && !showPublic) continue;
                        tourAPICount++;
                    }
                    else
                    {
                        woopangCount++;
                    }

                    // 색상: 서버 color(HEX) 우선 → 카테고리(+이름) 색 폴백 (DataManager 단일 소스)
                    string displayName = cached.displayName;
                    string rawColor = placeDataMap.ContainsKey(id) ? placeDataMap[id].color : null;
                    string colorHex = DataManager.ResolvePlaceColorHex(rawColor, cat, displayName);
                    combinedPlaces.Add((cached, d, id.ToString(), $"{displayName} - {Mathf.FloorToInt(d)}m", colorHex));
                    liveEntries.Add(new LiveEntry { id = id.ToString(), baseLabel = displayName, colorHex = colorHex, baseLat = cached.latitude, baseLon = cached.longitude });
                    addedIds.Add(id);
                }
            }

            // 1b. placeDataMap에만 있고 lightCache에 없는 데이터 (Detail API로 가져온 것)
            foreach (var p in placeDataMap.Values) {
                if (addedIds.Contains(p.id)) continue;
                if (HiddenPlaces.IsHidden("dm_" + p.id)) continue;
                string origType = p.original_model_type ?? p.model_type;
                if (!showObject3D && origType == "custom") continue;
                if (petFriendlyOnly && !p.pet_friendly) continue;
                if (noPetFriendly && p.pet_friendly) continue;
                if (categoryFilterActive && !string.IsNullOrEmpty(activeCategoryFilter))
                {
                    if ((p.category ?? "") != activeCategoryFilter) continue;
                }

                float d = CalculateDistance(lat, lon, p.latitude, p.longitude);
                if (d <= maxDisplayDistance) {
                    bool isPublicData = FilterManager.PublicDataCategories.Contains(p.category ?? "");
                    if (isPublicData)
                    {
                        if (!categoryFilterActive && !showPublic) continue;
                        tourAPICount++;
                    }
                    else
                    {
                        woopangCount++;
                    }
                    string pColor = DataManager.ResolvePlaceColorHex(p.color, p.category, p.name);
                    combinedPlaces.Add((p, d, p.id.ToString(), $"{p.name} - {Mathf.FloorToInt(d)}m", pColor));
                    liveEntries.Add(new LiveEntry { id = p.id.ToString(), baseLabel = p.name, colorHex = pColor, baseLat = p.latitude, baseLon = p.longitude });
                }
            }
        }

        // 2. TourAPI
        if (showPublic && tourAPIManager != null) {
            foreach(var p in tourAPIManager.GetPlaceDataMap().Values) {
                if (HiddenPlaces.IsHidden("tour_" + p.contentid)) continue;
                float d = CalculateDistance(lat, lon, p.mapy, p.mapx);
                if (d <= maxDisplayDistance) {
                    tourAPICount++;
                    combinedPlaces.Add((p, d, p.contentid, $"{p.title} - {Mathf.FloorToInt(d)}m", p.color));
                    liveEntries.Add(new LiveEntry { id = p.contentid, baseLabel = p.title, colorHex = p.color, baseLat = p.mapy, baseLon = p.mapx });
                }
            }
        }

        // 3. New Public Transport Managers
        AddTransportData(terminalManager, showTerminal, ref publicTransportCount, lat, lon, TERMINAL_COLOR, "terminal_");
        AddTransportData(trainManager, showTrain, ref publicTransportCount, lat, lon, TRAIN_COLOR, "train_");
        AddTransportData(subwayManager, showSubway, ref publicTransportCount, lat, lon, SUBWAY_COLOR, "subway_");

        // 4. P2P Users (근처 사용자)
        bool showP2PUsers = activeFilters.GetValueOrDefault("p2pUsers", true);
        if (showP2PUsers && p2pManager != null)
        {
            AddP2PUserData(lat, lon);
        }

        // 거리순 정렬 + 상위 maxListEntries개만 (DB 커져도 이 값 이상은 안 만짐)
        // liveEntries 는 combinedPlaces 와 같은 순서로 하나씩 쌓였다 — 같은 순서로 골라 목록 줄과 1:1 로 맞춘다
        // (예전 id 기준 자르기는 매니저끼리 id 가 겹치면 어긋났다)
        var order = Enumerable.Range(0, combinedPlaces.Count).OrderBy(i => combinedPlaces[i].distance).Take(maxListEntries).ToList();
        var sortedPlaces = new List<(object place, float distance, string id, string displayText, string colorHex)>(order.Count);
        var sortedLive = new List<LiveEntry>(order.Count);
        foreach (int i in order)
        {
            sortedPlaces.Add(combinedPlaces[i]);
            if (i < liveEntries.Count) sortedLive.Add(liveEntries[i]);
        }
        combinedPlaces = sortedPlaces;
        liveEntries = sortedLive;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var item in combinedPlaces) {
            string color = string.IsNullOrEmpty(item.colorHex) ? "FFFFFF" : item.colorHex;
            sb.Append($"<color=#{color}>{item.displayText}</color>\n");
        }

        cachedFooter = $"\n{GetLocalizedText("woopangData")}: {woopangCount}\n{GetLocalizedText("tourApiData")}: {tourAPICount}\n{GetLocalizedText("transportData")}: {publicTransportCount}\n{GetLocalizedText("p2pUserData")}: {p2pUserCount}";
        sb.Append(cachedFooter);

        // 데이터 비어있고 panel 열려있으면 자동 재시도 (데이터 로드 race condition 대응)
        if (combinedPlaces.Count == 0 && listPanel != null && listPanel.activeInHierarchy)
        {
            bool noInternet = Application.internetReachability == NetworkReachability.NotReachable;
            if (!noInternet && dataLoadRetryAttempts < MAX_DATA_LOAD_RETRIES)
            {
                dataLoadRetryAttempts++;
                yield return new WaitForSeconds(1f);
                UpdateUI();
                yield break;
            }
            else
            {
                // 재시도 후에도 빈 결과 → 안내 표시. 못 받은 것을 '주변에 없다' 고 하지 않는다 —
                // 인터넷이 끊겼으면 바로 '인터넷 연결이 없어요', 서버가 응답 없거나 아직 못 받았으면 '아직 받지 못했어요'.
                // 연결되어 목록을 받으면 다음 주기 갱신(updateInterval)이 줄로 바꾼다
                if (listText != null)
                {
                    string emptyMsg = GetLocalizedText(noInternet ? "noInternet"
                        : ServerHealth.Down || dataManager == null || !dataManager.IsCacheReady ? "listNotLoaded"
                        : "noNearbyData");
                    listText.text = emptyMsg;
                    lastDisplayedText = emptyMsg;
                }
                shownEntries.Clear();
                hasLiveSnapshot = false;
                yield break;
            }
        }

        dataLoadRetryAttempts = 0; // 데이터 들어왔으면 카운터 리셋

        if (listText != null)
        {
            string newText = sb.ToString();
            listText.text = newText;
            lastDisplayedText = newText;
        }

        // 활성 Target들을 placeId 기준으로 liveEntries에 매핑 (스폰된 POI는 카메라 거리 우선 사용)
        Target[] activeTargets = UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (activeTargets != null && activeTargets.Length > 0)
        {
            var byId = new Dictionary<string, Target>(activeTargets.Length);
            var byName = new Dictionary<string, Target>(activeTargets.Length);
            foreach (var t in activeTargets)
            {
                if (t == null) continue;
                if (!string.IsNullOrEmpty(t.placeId) && !byId.ContainsKey(t.placeId)) byId[t.placeId] = t;
                if (!string.IsNullOrEmpty(t.PlaceName) && !byName.ContainsKey(t.PlaceName)) byName[t.PlaceName] = t;
            }
            for (int i = 0; i < liveEntries.Count; i++)
            {
                var e = liveEntries[i];
                Target tg;
                if (!string.IsNullOrEmpty(e.id) && byId.TryGetValue(e.id, out tg)) { e.target = tg; e.targetTf = tg.transform; continue; }
                if (!string.IsNullOrEmpty(e.baseLabel) && byName.TryGetValue(e.baseLabel, out tg)) { e.target = tg; e.targetTf = tg.transform; }
            }
        }

        // 목록 줄과 같은 순서의 장소 정보 (글자는 위에서 그대로 썼다)
        shownEntries.Clear();
        for (int i = 0; i < liveEntries.Count && i < combinedPlaces.Count; i++)
            shownEntries.Add(ToListEntry(liveEntries[i], combinedPlaces[i].distance));
        hasLiveSnapshot = liveEntries.Count > 0;
        if (arCameraCache == null) arCameraCache = Camera.main;
        yield return null;
    }

    private string cachedFooter = "";

    void Update()
    {
        if (listText == null) return;

        // listPanel 활성 전환 감지 — 비활성→활성 전환 시 즉시 풀빌드 (10초 대기 안 함)
        bool nowActive = listPanel != null && listPanel.activeInHierarchy;
        if (nowActive && !wasListPanelActive)
        {
            wasListPanelActive = true;
            UpdateUI();
            return;
        }
        wasListPanelActive = nowActive;

        if (!hasLiveSnapshot || !nowActive) return;

        float lat = lastGpsLat;
        float lon = lastGpsLon;
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

        // 카메라 위치 (스폰된 POI 거리 계산용 — OffScreenIndicator와 동일 방식)
        if (arCameraCache == null) arCameraCache = Camera.main;
        Vector3 camPos = arCameraCache != null ? arCameraCache.transform.position : Vector3.zero;
        bool hasCam = arCameraCache != null;

        // GPS fallback용 평면 근사 상수 (프레임당 1회 cos 계산 — Update 안에서 Haversine trig 누적 비용 제거)
        float cosLat = Mathf.Cos(Mathf.Deg2Rad * lat);

        int count = liveEntries.Count;
        if (orderedBuffer.Length < count) Array.Resize(ref orderedBuffer, Mathf.NextPowerOfTwo(count));
        for (int i = 0; i < count; i++)
        {
            var e = liveEntries[i];
            // 1순위: 스폰된 Target transform이 살아있으면 카메라 거리 (1cm 단위로 매끄럽게 갱신)
            if (hasCam && e.targetTf != null)
            {
                orderedBuffer[i] = (Vector3.Distance(camPos, e.targetTf.position), i);
            }
            else
            {
                // 2순위: GPS 거리 fallback — 평면 근사 (한국 위도/5km 이내 오차 0.5% 미만, 매 프레임 trig 회피)
                float dLatM = (e.baseLat - lat) * 111320f;
                float dLonM = (e.baseLon - lon) * 111320f * cosLat;
                orderedBuffer[i] = (Mathf.Sqrt(dLatM * dLatM + dLonM * dLonM), i);
            }
        }
        Array.Sort(orderedBuffer, 0, count, OrderedComparer.Instance);

        liveBuilder.Clear();
        shownEntries.Clear();
        for (int i = 0; i < count; i++)
        {
            var pair = orderedBuffer[i];
            if (maxDisplayDistance > 0f && pair.d > maxDisplayDistance) continue;
            var e = liveEntries[pair.idx];
            string color = string.IsNullOrEmpty(e.colorHex) ? "FFFFFF" : e.colorHex;
            liveBuilder.Append("<color=#").Append(color).Append('>')
                       .Append(e.baseLabel).Append(" - ")
                       .Append(Mathf.FloorToInt(pair.d)).Append("m</color>\n");
            shownEntries.Add(ToListEntry(e, pair.d));
        }
        liveBuilder.Append(cachedFooter);

        // 텍스트가 실제로 바뀌었을 때만 set — UI Text mesh rebuild 비용 회피 (모바일에서 큼)
        string newText = liveBuilder.ToString();
        if (newText != lastDisplayedText)
        {
            listText.text = newText;
            lastDisplayedText = newText;
        }
    }

    private static ListEntry ToListEntry(LiveEntry e, float distance)
    {
        return new ListEntry { id = e.id, name = e.baseLabel, distance = distance, lat = e.baseLat, lon = e.baseLon, target = e.target };
    }

    /// <summary>
    /// 목록 장소의 AR 오브젝트(Target) — 지금 떠 있으면, 없으면 null.
    /// id 와 이름이 둘 다 맞는 것을 먼저 고른다 (매니저끼리 숫자 id 가 겹칠 수 있다). 공공교통은 id 가 없어 이름으로.
    /// </summary>
    public Target FindTarget(ListEntry entry)
    {
        if (entry.target != null && entry.target.isActiveAndEnabled && SameName(entry.target, entry.name)) return entry.target;
        Target idOnly = null, nameOnly = null;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            bool nameOk = SameName(t, entry.name);
            if (!string.IsNullOrEmpty(entry.id) && t.placeId == entry.id)
            {
                if (nameOk) return t;
                if (idOnly == null) idOnly = t;
            }
            else if (nameOk && nameOnly == null) nameOnly = t;
        }
        return idOnly != null ? idOnly : nameOnly;
    }

    // 목록 이름과 Target 이름 비교 — P2P 사용자는 목록에 '@이름', Target 에는 '이름'
    private static bool SameName(Target t, string name)
    {
        string p = t.PlaceName;
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(p)) return false;
        if (p == name) return true;
        return name.Length == p.Length + 1 && name[0] == '@' && string.CompareOrdinal(name, 1, p, 0, p.Length) == 0;
    }

    /// <summary>
    /// P2P 사용자 데이터 추가
    /// P2PManager의 필터 모드에 따라 표시 여부 결정
    /// - None: 목록에 표시 안함
    /// - All: 모든 사용자 표시
    /// - FollowingOnly: 팔로잉한 사용자만 표시
    /// </summary>
    private void AddP2PUserData(float lat, float lon)
    {
        if (p2pManager == null) return;

        // 필터링된 사용자 목록 가져오기 (None이면 빈 리스트 반환)
        var nearbyUsers = p2pManager.GetFilteredNearbyUsers();
        if (nearbyUsers == null || nearbyUsers.Count == 0) return;

        foreach (var user in nearbyUsers)
        {
            if (user.distance <= maxDisplayDistance)
            {
                p2pUserCount++;
                string displayText = $"@{user.username} - {Mathf.FloorToInt(user.distance)}m";   // 이모지는 앱 글꼴에 없어 □로 보였다
                combinedPlaces.Add((user, user.distance, user.user_id, displayText, P2P_USER_COLOR));
                liveEntries.Add(new LiveEntry { id = user.user_id, baseLabel = $"@{user.username}", colorHex = P2P_USER_COLOR, baseLat = (float)user.latitude, baseLon = (float)user.longitude });
            }
        }
    }

    private void AddTransportData<T>(T manager, bool filter, ref int count, float lat, float lon, string colorHex = "00FF00", string hiddenPrefix = null) where T : MonoBehaviour
    {
        if (!filter || manager == null) return;

        var method = manager.GetType().GetMethod("GetPlaceDataMap");
        if (method == null) return;

        var dataMap = method.Invoke(manager, null) as IDictionary;
        if (dataMap == null) return;

        foreach (var val in dataMap.Values) {
            var latProp = val.GetType().GetProperty("latitude");
            var lonProp = val.GetType().GetProperty("longitude");
            var nameProp = val.GetType().GetProperty("name");
            var typeProp = val.GetType().GetProperty("type");

            if (latProp == null || lonProp == null || nameProp == null) continue;

            object rawLat = latProp.GetValue(val);
            object rawLon = lonProp.GetValue(val);
            string pName = (string)nameProp.GetValue(val);
            if (hiddenPrefix != null && HiddenPlaces.IsHidden(hiddenPrefix + pName + "_" + rawLat + "_" + rawLon)) continue;
            float pLat = Convert.ToSingle(rawLat);
            float pLon = Convert.ToSingle(rawLon);
            string pType = typeProp != null ? (string)typeProp.GetValue(val) : "unknown";
            string pId = $"{pType}_{pName}";

            float d = CalculateDistance(lat, lon, pLat, pLon);
            if (d <= maxDisplayDistance) {
                count++;
                combinedPlaces.Add((val, d, pId, $"{pName} - {Mathf.FloorToInt(d)}m", colorHex));
                liveEntries.Add(new LiveEntry { id = pId, baseLabel = pName, colorHex = colorHex, baseLat = pLat, baseLon = pLon });
            }
        }
    }

    private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000;
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(Mathf.Deg2Rad * (lat1)) * Mathf.Cos(Mathf.Deg2Rad * (lat2)) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        return R * 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
    }

    public void ApplyFilters(Dictionary<string, bool> filters) {
        activeFilters = filters;
        UpdateUI();
    }

    private void OnDistanceSliderChanged(float value) {
        maxDisplayDistance = value;
        PlayerPrefs.SetFloat("MaxDisplayDistance", value);
        UpdateDistanceValueText();
        UpdateUI();

        // Propagate distance filter to all managers
        float lat = 36.6361f; float lon = 126.8280f;
#if UNITY_EDITOR
        // 에디터에서는 VirtualLocation 사용
        if (VirtualLocation.Instance != null) {
            lat = VirtualLocation.Instance.Latitude;
            lon = VirtualLocation.Instance.Longitude;
        }
#else
        // GPS 미초기화/권한거부 시 (0,0) 같은 잘못된 좌표 전파 방지
        if (Input.location.status == LocationServiceStatus.Running) {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }
#endif
        if (dataManager != null) dataManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (tourAPIManager != null) tourAPIManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (terminalManager != null) terminalManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (trainManager != null) trainManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (subwayManager != null) subwayManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (p2pManager != null) p2pManager.SetMaxTrackingDistance(maxDisplayDistance);

        // OffScreenIndicator 거리 필터 동기화
        OffScreenIndicator osi = FindFirstObjectByType<OffScreenIndicator>();
        if (osi != null) osi.SetMaxIndicatorDistance(maxDisplayDistance);

        // FilterManager IndicatorOnly 스폰 반경 동기화 — 사용자가 슬라이더로 줄이면 먼 POI도 즉시 제외
        FilterManager filterMgr = UnityEngine.Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null) filterMgr.SetIndicatorRadius(maxDisplayDistance);
    }

    private void UpdateDistanceValueText() {
        if (distanceValueText != null)
            distanceValueText.text = maxDisplayDistance >= 1000f ? $"{(maxDisplayDistance / 1000f):F1}km" : $"{Mathf.RoundToInt(maxDisplayDistance)}m";
    }

    // 캐시된 비교자 — Array.Sort lambda boxing 방지
    private sealed class OrderedComparer : IComparer<(float d, int idx)>
    {
        public static readonly OrderedComparer Instance = new OrderedComparer();
        public int Compare((float d, int idx) a, (float d, int idx) b) => a.d.CompareTo(b.d);
    }
}