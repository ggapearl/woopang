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
    public BusStationManager busManager;
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
        FilterManager filterMgr = UnityEngine.Object.FindFirstObjectByType<FilterManager>();
        if (filterMgr != null)
            activeCategoryFilter = filterMgr.GetActiveCategoryFilter();

        // 1. Woopang Data
        if (dataManager != null) {
            foreach(var p in dataManager.GetPlaceDataMap().Values) {
                // Object3D 필터: custom 모델은 토글 OFF 시 목록에서도 제외
                string origType = p.original_model_type ?? p.model_type;
                if (!showObject3D && origType == "custom") continue;

                // 애견동반 필터 적용
                if (petFriendlyOnly && !p.pet_friendly) continue; // 애견동반만 (노란색)
                if (noPetFriendly && p.pet_friendly) continue;     // 애견동반 아닌곳만 (체크해제)
                // petFriendlyAll일 경우 모두 표시 (흰색)

                // 카테고리 필터 적용
                if (categoryFilterActive && !string.IsNullOrEmpty(activeCategoryFilter))
                {
                    if ((p.category ?? "") != activeCategoryFilter) continue;
                }

                float d = CalculateDistance(lat, lon, p.latitude, p.longitude);
                if (d <= maxDisplayDistance) {
                    bool isPublicData = FilterManager.PublicDataCategories.Contains(p.category ?? "");
                    if (isPublicData)
                    {
                        if (!showPublic) continue; // 공공데이터 토글 OFF 시 목록에서 제외
                        tourAPICount++;
                    }
                    else
                    {
                        woopangCount++;
                    }
                    combinedPlaces.Add((p, d, p.id.ToString(), $"{p.name} - {Mathf.FloorToInt(d)}m", p.color));
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

        sb.Append($"\n{GetLocalizedText("woopangData")}: {woopangCount}");
        sb.Append($"\n{GetLocalizedText("tourApiData")}: {tourAPICount}");
        sb.Append($"\n{GetLocalizedText("transportData")}: {publicTransportCount}");
        sb.Append($"\n{GetLocalizedText("p2pUserData")}: {p2pUserCount}");

        if (listText != null) listText.text = sb.ToString();
        yield return null;
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
        if (busManager != null) busManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (terminalManager != null) terminalManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (trainManager != null) trainManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (subwayManager != null) subwayManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (p2pManager != null) p2pManager.SetMaxTrackingDistance(maxDisplayDistance);

        // OffScreenIndicator 거리 필터 동기화
        OffScreenIndicator osi = FindFirstObjectByType<OffScreenIndicator>();
        if (osi != null) osi.SetMaxIndicatorDistance(maxDisplayDistance);
    }

    private void UpdateDistanceValueText() {
        if (distanceValueText != null)
            distanceValueText.text = maxDisplayDistance >= 1000f ? $"{(maxDisplayDistance / 1000f):F1}km" : $"{Mathf.RoundToInt(maxDisplayDistance)}m";
    }
}