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

    [Header("Distance Control")]
    [SerializeField] private Slider distanceSlider;
    [SerializeField] private Text distanceValueText;
    private float maxDisplayDistance;

    private List<(object place, float distance, string id, string displayText, string colorHex)> combinedPlaces = new List<(object, float, string, string, string)>();

    // 매 프레임 거리/정렬만 갱신용 — 풀빌드 시 채우고 Update()에서 GPS 변동분만 반영
    private struct LiveEntry
    {
        public string id;
        public string baseLabel;     // 거리 빼고 표시명만 (예: "스타벅스" 또는 "👤 user")
        public string colorHex;
        public float baseLat;
        public float baseLon;
    }
    private List<LiveEntry> liveEntries = new List<LiveEntry>();
    private System.Text.StringBuilder liveBuilder = new System.Text.StringBuilder(2048);
    private float lastGpsLat;
    private float lastGpsLon;
    private bool hasLiveSnapshot = false;

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

    // 카테고리별 색상 (FilterManager와 동일)
    private static readonly Dictionary<string, string> CATEGORY_COLORS = new Dictionary<string, string>
    {
        { "shop",     "4080F2" }, // 파란색
        { "food",     "FBC15D" }, // 주황색
        { "cafe",     "E854A1" }, // 핑크색
        { "park",     "4DD980" }, // 녹색
        { "toilet",   "AE54C4" }, // 보라색
        { "sport",    "33BFBF" }, // 청록색
        { "landmark", "D9A621" }, // 금색
        { "culture",  "C76ED7" }, // 문화 (연보라)
        { "gov",      "8899AA" }, // 정부기관 (회청색)
        { "edu",      "5599CC" }, // 교육 (하늘색)
        { "medical",  "FF6666" }, // 의료 (연빨강)
        { "welfare",  "77BB77" }, // 복지 (연초록)
    };

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
            { "transportData", "TRANSPORT DATA" }, { "p2pUserData", "NEARBY USERS" }
        }},
        { "ko", new Dictionary<string, string> {
            { "petFriendly", "[애견동반]" }, { "noImage", "[이미지없음]" },
            { "woopangData", "우팡 데이터" }, { "tourApiData", "공공데이터" },
            { "transportData", "대중교통 데이터" }, { "p2pUserData", "근처 사용자" }
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
        if (distanceSlider != null)
        {
            distanceSlider.minValue = 100f;
            distanceSlider.maxValue = 10000f;
            float savedDistance = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
            maxDisplayDistance = savedDistance;
            distanceSlider.value = savedDistance;
            distanceSlider.onValueChanged.AddListener(OnDistanceSliderChanged);
            UpdateDistanceValueText();

            // FilterManager IndicatorOnly 반경 초기 동기화 (FilterManager가 비활성일 수 있으므로 Include)
            FilterManager filterMgr = UnityEngine.Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
            if (filterMgr != null) filterMgr.SetIndicatorRadius(savedDistance);
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

                    // 색상 우선순위: placeDataMap 상세 색상 > 카테고리 색상 > null(기본 흰색)
                    string colorHex = placeDataMap.ContainsKey(id) ? placeDataMap[id].color : null;
                    if (string.IsNullOrEmpty(colorHex) && !string.IsNullOrEmpty(cat))
                        CATEGORY_COLORS.TryGetValue(cat, out colorHex);
                    string displayName = cached.displayName;
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
                    string pColor = p.color;
                    if (string.IsNullOrEmpty(pColor) && !string.IsNullOrEmpty(p.category))
                        CATEGORY_COLORS.TryGetValue(p.category, out pColor);
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

        combinedPlaces = combinedPlaces.OrderBy(x => x.distance).ToList();

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var item in combinedPlaces) {
            string color = string.IsNullOrEmpty(item.colorHex) ? "FFFFFF" : item.colorHex;
            sb.Append($"<color=#{color}>{item.displayText}</color>\n");
        }

        cachedFooter = $"\n{GetLocalizedText("woopangData")}: {woopangCount}\n{GetLocalizedText("tourApiData")}: {tourAPICount}\n{GetLocalizedText("transportData")}: {publicTransportCount}\n{GetLocalizedText("p2pUserData")}: {p2pUserCount}";
        sb.Append(cachedFooter);

        if (listText != null) listText.text = sb.ToString();
        hasLiveSnapshot = liveEntries.Count > 0;
        yield return null;
    }

    private string cachedFooter = "";

    void Update()
    {
        if (!hasLiveSnapshot || listText == null) return;
        if (listPanel == null || !listPanel.activeInHierarchy) return;

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

        int count = liveEntries.Count;
        var ordered = new (float d, int idx)[count];
        for (int i = 0; i < count; i++)
        {
            var e = liveEntries[i];
            ordered[i] = (CalculateDistance(lat, lon, e.baseLat, e.baseLon), i);
        }
        Array.Sort(ordered, (a, b) => a.d.CompareTo(b.d));

        liveBuilder.Clear();
        for (int i = 0; i < count; i++)
        {
            var pair = ordered[i];
            if (maxDisplayDistance > 0f && pair.d > maxDisplayDistance) continue;
            var e = liveEntries[pair.idx];
            string color = string.IsNullOrEmpty(e.colorHex) ? "FFFFFF" : e.colorHex;
            liveBuilder.Append("<color=#").Append(color).Append('>')
                       .Append(e.baseLabel).Append(" - ")
                       .Append(Mathf.FloorToInt(pair.d)).Append("m</color>\n");
        }
        liveBuilder.Append(cachedFooter);
        listText.text = liveBuilder.ToString();
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
        lat = Input.location.lastData.latitude; lon = Input.location.lastData.longitude;
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
}