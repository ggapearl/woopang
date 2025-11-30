using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class PlaceListManager : MonoBehaviour
{
    public DataManager dataManager;
    public TourAPIManager tourAPIManager;
    public Text listText;

    [Header("UI Update Settings")]
    [Tooltip("ListPanel 게임오브젝트 참조 (활성화 상태 체크용)")]
    [SerializeField] private GameObject listPanel;

    [SerializeField] private float updateInterval = 10f; // 10초로 변경

    [Header("AR Object Distance Filter")]
    [Tooltip("AR 오브젝트 생성 거리 조정 슬라이더 (UI)")]
    [SerializeField] private Slider distanceSlider;

    [Tooltip("AR 오브젝트 최대 생성 거리 (미터, 50~200m)")]
    [SerializeField] private float maxDisplayDistance = 144f;

    [Tooltip("슬라이더 값을 표시할 텍스트 (선택사항)")]
    [SerializeField] private Text distanceValueText;

    private List<(object place, float distance, string id, string displayText, string colorHex)> combinedPlaces = new List<(object, float, string, string, string)>();
    private int woopangCount;
    private int tourAPICount;

    // 필터 설정 - 기본값: 전체 선택
    private Dictionary<string, bool> activeFilters = new Dictionary<string, bool>
    {
        { "woopangData", true },  // 우팡 데이터
        { "petFriendly", true },  // 애견동반
        { "publicData", true },   // 공공데이터
        { "subway", true },       // 지하철
        { "bus", true },          // 버스
        { "alcohol", true }       // 주류
    };

    // 언어별 텍스트 템플릿
    private Dictionary<string, Dictionary<string, string>> languageTexts = new Dictionary<string, Dictionary<string, string>>
    {
        { "en", new Dictionary<string, string> {
            { "petFriendly", "[PetFriendly]" },
            { "noImage", "[No Image]" },
            { "woopangData", "WOOPANG DATA" },
            { "tourApiData", "TourAPI DATA" }
        }},
        { "ko", new Dictionary<string, string> {
            { "petFriendly", "[애견동반]" },
            { "noImage", "[이미지없음]" },
            { "woopangData", "우팡 데이터" },
            { "tourApiData", "관광공사API 데이터" }
        }},
        { "ja", new Dictionary<string, string> {
            { "petFriendly", "[ペット同伴]" },
            { "noImage", "[画像なし]" },
            { "woopangData", "WOOPANGデータ" },
            { "tourApiData", "観光APIデータ" }
        }},
        { "zh", new Dictionary<string, string> {
            { "petFriendly", "[宠物友好]" },
            { "noImage", "[无图片]" },
            { "woopangData", "WOOPANG数据" },
            { "tourApiData", "旅游API数据" }
        }},
        { "es", new Dictionary<string, string> {
            { "petFriendly", "[AdmiteMascotas]" },
            { "noImage", "[SinImagen]" },
            { "woopangData", "DATOS WOOPANG" },
            { "tourApiData", "DATOS TourAPI" }
        }}
    };

    void Start()
    {
        if (listText == null || dataManager == null || tourAPIManager == null)
        {
            return;
        }

        // AR 오브젝트 거리 슬라이더 초기화
        if (distanceSlider != null)
        {
            distanceSlider.minValue = 50f;
            distanceSlider.maxValue = 200f;
            distanceSlider.value = maxDisplayDistance;
            distanceSlider.onValueChanged.AddListener(OnDistanceSliderChanged);
            UpdateDistanceValueText();
        }

        StartCoroutine(InitializeAndUpdateUI());
    }

    void OnEnable()
    {
        // ListPanel이 활성화될 때마다 즉시 UI 업데이트
        if (dataManager != null && dataManager.IsDataLoaded() &&
            tourAPIManager != null && tourAPIManager.IsDataLoaded())
        {
            UpdateUI();
            Debug.Log("[PlaceListManager] ListPanel 활성화 - 즉시 UI 업데이트");
        }
    }

    private string GetLanguageCode()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return "ko";
            case SystemLanguage.Japanese:
                return "ja";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return "zh";
            case SystemLanguage.Spanish:
                return "es";
            case SystemLanguage.English:
            default:
                return "en";
        }
    }

    private string GetLocalizedText(string key)
    {
        string languageCode = GetLanguageCode();
        if (languageTexts.ContainsKey(languageCode) && languageTexts[languageCode].ContainsKey(key))
        {
            return languageTexts[languageCode][key];
        }
        return languageTexts["en"][key]; // 기본값으로 영어 반환
    }

    private IEnumerator InitializeAndUpdateUI()
    {
        while (dataManager != null && !dataManager.IsDataLoaded() ||
               tourAPIManager != null && !tourAPIManager.IsDataLoaded())
        {
            yield return new WaitForSeconds(1f);
        }
        UpdateUI();
        StartCoroutine(UpdateUIPeriodically());
    }

    private IEnumerator UpdateUIPeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);

            // ListPanel이 활성화되어 있을 때만 업데이트
            if (listPanel != null && listPanel.activeInHierarchy)
            {
                UpdateUI();
                Debug.Log("[PlaceListManager] ListPanel 활성화 - UI 업데이트 실행");
            }
        }
    }

    private void UpdateUI()
    {
        float latitude, longitude;
        if (Input.location.status == LocationServiceStatus.Running)
        {
            LocationInfo currentLocation = Input.location.lastData;
            latitude = currentLocation.latitude;
            longitude = currentLocation.longitude;
        }
        else
        {
            latitude = 37.5665f;
            longitude = 126.9780f;
        }

        var woopangPlaces = dataManager != null ? dataManager.GetPlaceDataMap().Values.ToList() : new List<PlaceData>();
        var tourPlaces = tourAPIManager != null ? tourAPIManager.GetPlaceDataMap().Values.ToList() : new List<TourPlaceData>();

        woopangCount = woopangPlaces.Count;
        tourAPICount = tourPlaces.Count;

        if (listText != null)
        {
            listText.text = "";
        }
        else
        {
            return;
        }

        combinedPlaces.Clear();

        // 🔧 우팡데이터 필터 체크 추가
        bool showWoopangData = activeFilters.ContainsKey("woopangData") && activeFilters["woopangData"];
        bool showPetFriendly = activeFilters.ContainsKey("petFriendly") && activeFilters["petFriendly"];
        bool showAlcohol = activeFilters.ContainsKey("alcohol") && activeFilters["alcohol"];

        if (showWoopangData)
        {
            foreach (var place in woopangPlaces)
            {
                float distance = CalculateDistance(latitude, longitude, place.latitude, place.longitude);

                // 거리 필터링 - maxDisplayDistance 이내만 표시
                if (distance > maxDisplayDistance)
                {
                    continue;
                }

                // 애견동반 필터 체크
                if (place.pet_friendly && !showPetFriendly)
                {
                    continue; // 애견동반 필터가 꺼져있고 장소가 애견동반이면 건너뛰기
                }

                // 주류 판매 필터 체크
                if (place.alcohol_available && !showAlcohol)
                {
                    continue; // 주류 필터가 꺼져있고 장소가 주류 판매하면 건너뛰기
                }

                string distanceText = $"{Mathf.FloorToInt(distance)}m";
                string displayText = place.pet_friendly
                    ? $"{place.name} - {distanceText} {GetLocalizedText("petFriendly")}"
                    : $"{place.name} - {distanceText}";
                string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;
                combinedPlaces.Add((place, distance, place.id.ToString(), displayText, colorHex));
            }
        }

        // 공공데이터(TourAPI) 필터 체크
        if (activeFilters["publicData"])
        {
            foreach (var place in tourPlaces)
            {
                float distance = CalculateDistance(latitude, longitude, place.mapy, place.mapx);

                // 거리 필터링 - maxDisplayDistance 이내만 표시
                if (distance > maxDisplayDistance)
                {
                    continue;
                }

                // 애견동반 필터 체크 (TourAPI는 모두 애견동반)
                if (!activeFilters["petFriendly"])
                {
                    continue;
                }

                string distanceText = $"{Mathf.FloorToInt(distance)}m";
                string displayText = string.IsNullOrEmpty(place.firstimage)
                    ? $"{place.title} - {distanceText} {GetLocalizedText("noImage")} {GetLocalizedText("petFriendly")}"
                    : $"{place.title} - {distanceText} {GetLocalizedText("petFriendly")}";
                string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;
                combinedPlaces.Add((place, distance, place.contentid, displayText, colorHex));
            }
        }

        combinedPlaces = combinedPlaces.OrderBy(x => x.distance).ToList();

        Debug.Log($"[PlaceListManager] 리스트 업데이트 - 전체 데이터: 우팡={woopangPlaces.Count}, TourAPI={tourPlaces.Count}, 필터링 후 표시={combinedPlaces.Count}, 거리제한={maxDisplayDistance}m");

        foreach (var (place, distance, id, displayText, colorHex) in combinedPlaces)
        {
            string coloredText = $"<color=#{colorHex}>{displayText}</color>";
            listText.text += coloredText + "\n";
        }

        listText.text += $"\n{GetLocalizedText("woopangData")}: {woopangPlaces.Count}\n";
        listText.text += $"{GetLocalizedText("tourApiData")}: {tourPlaces.Count}";

        Canvas.ForceUpdateCanvases();
        if (listText != null)
        {
            RectTransform contentRect = listText.GetComponentInParent<RectTransform>();
            ScrollRect scrollRect = listText.GetComponentInParent<ScrollRect>();
        }
    }

    public List<(object place, float distance, string id, string displayText, string colorHex)> GetCombinedPlaces()
    {
        return combinedPlaces;
    }

    public int GetWoopangCount()
    {
        return woopangCount;
    }

    public int GetTourAPICount()
    {
        return tourAPICount;
    }

    public int GetTotalCount()
    {
        return combinedPlaces.Count;
    }

    public int GetWoopangObjectCount()
    {
        return dataManager != null ? dataManager.GetSpawnedObjectsCount() : 0;
    }

    public int GetTourAPIObjectCount()
    {
        return tourAPIManager != null ? tourAPIManager.GetSpawnedObjectsCount() : 0;
    }

    private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000;
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(Mathf.Deg2Rad * (lat1)) * Mathf.Cos(Mathf.Deg2Rad * (lat2)) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
        return R * c;
    }

    // FilterManager에서 호출하는 메서드
    public void ApplyFilters(Dictionary<string, bool> filters)
    {
        activeFilters = filters;
        UpdateUI(); // UI 즉시 업데이트
    }

    /// <summary>
    /// 거리 슬라이더 값 변경 시 호출되는 메서드
    /// </summary>
    private void OnDistanceSliderChanged(float value)
    {
        maxDisplayDistance = value;
        UpdateDistanceValueText();
        UpdateUI(); // UI 즉시 업데이트

        // AR 오브젝트 거리 필터링 적용
        ApplyDistanceFilterToARObjects();
    }

    /// <summary>
    /// 기존 AR 오브젝트들에 거리 필터 적용
    /// </summary>
    private void ApplyDistanceFilterToARObjects()
    {
        if (Input.location.status != LocationServiceStatus.Running)
            return;

        LocationInfo currentLocation = Input.location.lastData;
        float currentLat = currentLocation.latitude;
        float currentLon = currentLocation.longitude;

        // DataManager 오브젝트 필터링
        if (dataManager != null)
        {
            var woopangPlaces = dataManager.GetPlaceDataMap();
            foreach (var kvp in woopangPlaces)
            {
                var place = kvp.Value;
                float distance = CalculateDistance(currentLat, currentLon, place.latitude, place.longitude);

                // 오브젝트 활성화/비활성화
                GameObject obj = dataManager.GetSpawnedObject(kvp.Key);
                if (obj != null)
                {
                    obj.SetActive(distance <= maxDisplayDistance);
                }
            }
        }

        // TourAPIManager 오브젝트 필터링
        if (tourAPIManager != null)
        {
            var tourPlaces = tourAPIManager.GetPlaceDataMap();
            foreach (var kvp in tourPlaces)
            {
                var place = kvp.Value;
                float distance = CalculateDistance(currentLat, currentLon, place.mapy, place.mapx);

                // 오브젝트 활성화/비활성화
                GameObject obj = tourAPIManager.GetSpawnedObject(kvp.Key);
                if (obj != null)
                {
                    obj.SetActive(distance <= maxDisplayDistance);
                }
            }
        }

        Debug.Log($"[PlaceListManager] 거리 필터 적용 완료: {maxDisplayDistance}m");
    }

    /// <summary>
    /// 거리 값 텍스트 업데이트
    /// </summary>
    private void UpdateDistanceValueText()
    {
        if (distanceValueText != null)
        {
            if (maxDisplayDistance >= 1000)
            {
                distanceValueText.text = $"{(maxDisplayDistance / 1000f):F1}km";
            }
            else
            {
                distanceValueText.text = $"{Mathf.RoundToInt(maxDisplayDistance)}m";
            }
        }
    }

    /// <summary>
    /// 외부에서 최대 표시 거리를 설정하는 메서드
    /// </summary>
    public void SetMaxDisplayDistance(float distance)
    {
        maxDisplayDistance = distance;
        if (distanceSlider != null)
        {
            distanceSlider.value = distance;
        }
        UpdateDistanceValueText();
        UpdateUI();
    }

    /// <summary>
    /// 현재 최대 표시 거리를 반환
    /// </summary>
    public float GetMaxDisplayDistance()
    {
        return maxDisplayDistance;
    }
}