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
        public string baseLabel;     // 거리 빼고 표시명만 (예: "스타벅스" 또는 "👤 user")
        public string colorHex;
        public float baseLat;
        public float baseLon;
        public Transform targetTf;   // 스폰된 GameObject가 있으면 카메라 거리 우선
    }
    private List<LiveEntry> liveEntries = new List<LiveEntry>();
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
            { "noNearbyData", "No nearby data found.\nTry adjusting the distance slider or moving to a different area." }
        }},
        { "ko", new Dictionary<string, string> {
            { "petFriendly", "[애견동반]" }, { "noImage", "[이미지없음]" },
            { "woopangData", "우팡 데이터" }, { "tourApiData", "공공데이터" },
            { "transportData", "대중교통 데이터" }, { "p2pUserData", "근처 사용자" },
            { "noNearbyData", "주변에 데이터가 없습니다.\n거리 슬라이더를 조정하거나 다른 위치로 이동해보세요." }
        }},
        { "ja", new Dictionary<string, string> {
            { "petFriendly", "[ペット同伴]" }, { "noImage", "[画像なし]" },
            { "woopangData", "WOOPANGデータ" }, { "tourApiData", "公共データ" },
            { "transportData", "交通データ" }, { "p2pUserData", "近くのユーザー" }
        }},
        { "zh", new Dictionary<string, string> {
            { "petFriendly", "[宠物友好]" }, { "noImage", "[无图片]" },
            { "woopangData", "WOOPANG数据" }, { "tourApiData", "公共数据" },
            { "transportData", "交通数据" }, { "p2pUserData", "附近用户" }
        }},
        { "es", new Dictionary<string, string> {
            { "petFriendly", "[Mascotas]" }, { "noImage", "[Sin imagen]" },
            { "woopangData", "Datos WOOPANG" }, { "tourApiData", "Datos Públicos" },
            { "transportData", "Datos de Transporte" }, { "p2pUserData", "Usuarios Cercanos" }
        }}
    };

    void Start()
    {
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
        string lang = Application.systemLanguage == SystemLanguage.Korean ? "ko" : "en";
        return languageTexts[lang].ContainsKey(key) ? languageTexts[lang][key] : key;
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

    private void OnDestroy()
    {
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
                float d = CalculateDistance(lat, lon, p.mapy, p.mapx);
                if (d <= maxDisplayDistance) {
                    tourAPICount++;
                    combinedPlaces.Add((p, d, p.contentid, $"{p.title} - {Mathf.FloorToInt(d)}m", p.color));
                    liveEntries.Add(new LiveEntry { id = p.contentid, baseLabel = p.title, colorHex = p.color, baseLat = p.mapy, baseLon = p.mapx });
                }
            }
        }

        // 3. New Public Transport Managers
        AddTransportData(terminalManager, showTerminal, ref publicTransportCount, lat, lon, TERMINAL_COLOR);
        AddTransportData(trainManager, showTrain, ref publicTransportCount, lat, lon, TRAIN_COLOR);
        AddTransportData(subwayManager, showSubway, ref publicTransportCount, lat, lon, SUBWAY_COLOR);

        // 4. P2P Users (근처 사용자)
        bool showP2PUsers = activeFilters.GetValueOrDefault("p2pUsers", true);
        if (showP2PUsers && p2pManager != null)
        {
            AddP2PUserData(lat, lon);
        }

        // 거리순 정렬 + 상위 maxListEntries개만 (DB 커져도 이 값 이상은 안 만짐)
        combinedPlaces = combinedPlaces.OrderBy(x => x.distance).Take(maxListEntries).ToList();

        // liveEntries도 combinedPlaces id 기준으로 잘라 동기화 — 매 프레임 갱신 대상도 같이 줄임
        if (liveEntries.Count > combinedPlaces.Count)
        {
            var keepIds = new HashSet<string>(combinedPlaces.Count);
            foreach (var c in combinedPlaces) keepIds.Add(c.id);
            liveEntries.RemoveAll(e => !keepIds.Contains(e.id));
        }

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
            if (dataLoadRetryAttempts < MAX_DATA_LOAD_RETRIES)
            {
                dataLoadRetryAttempts++;
                yield return new WaitForSeconds(1f);
                UpdateUI();
                yield break;
            }
            else
            {
                // 5회 시도 후에도 빈 결과 → 안내 표시
                if (listText != null)
                {
                    string emptyMsg = GetLocalizedText("noNearbyData");
                    listText.text = emptyMsg;
                    lastDisplayedText = emptyMsg;
                }
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
            var byId = new Dictionary<string, Transform>(activeTargets.Length);
            var byName = new Dictionary<string, Transform>(activeTargets.Length);
            foreach (var t in activeTargets)
            {
                if (t == null) continue;
                if (!string.IsNullOrEmpty(t.placeId) && !byId.ContainsKey(t.placeId)) byId[t.placeId] = t.transform;
                if (!string.IsNullOrEmpty(t.PlaceName) && !byName.ContainsKey(t.PlaceName)) byName[t.PlaceName] = t.transform;
            }
            for (int i = 0; i < liveEntries.Count; i++)
            {
                var e = liveEntries[i];
                Transform tf = null;
                if (!string.IsNullOrEmpty(e.id) && byId.TryGetValue(e.id, out tf)) { e.targetTf = tf; continue; }
                if (!string.IsNullOrEmpty(e.baseLabel) && byName.TryGetValue(e.baseLabel, out tf)) e.targetTf = tf;
            }
        }
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
        for (int i = 0; i < count; i++)
        {
            var pair = orderedBuffer[i];
            if (maxDisplayDistance > 0f && pair.d > maxDisplayDistance) continue;
            var e = liveEntries[pair.idx];
            string color = string.IsNullOrEmpty(e.colorHex) ? "FFFFFF" : e.colorHex;
            liveBuilder.Append("<color=#").Append(color).Append('>')
                       .Append(e.baseLabel).Append(" - ")
                       .Append(Mathf.FloorToInt(pair.d)).Append("m</color>\n");
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
                string displayText = $"👤 {user.username} - {Mathf.FloorToInt(user.distance)}m";
                combinedPlaces.Add((user, user.distance, user.user_id, displayText, P2P_USER_COLOR));
                liveEntries.Add(new LiveEntry { id = user.user_id, baseLabel = $"👤 {user.username}", colorHex = P2P_USER_COLOR, baseLat = (float)user.latitude, baseLon = (float)user.longitude });
            }
        }
    }

    private void AddTransportData<T>(T manager, bool filter, ref int count, float lat, float lon, string colorHex = "00FF00") where T : MonoBehaviour
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

            float pLat = Convert.ToSingle(latProp.GetValue(val));
            float pLon = Convert.ToSingle(lonProp.GetValue(val));
            string pName = (string)nameProp.GetValue(val);
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