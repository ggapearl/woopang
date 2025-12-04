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

    [Header("Distance Control")]
    [SerializeField] private Slider distanceSlider;
    [SerializeField] private Text distanceValueText;
    private float maxDisplayDistance;

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
            { "tourApiData", "관광공사 데이터" }
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
        Debug.Log($"[PlaceListManager] Start() 호출 - listText={listText != null}, dataManager={dataManager != null}, tourAPIManager={tourAPIManager != null}");

        if (listText == null)
        {
            Debug.LogError("[PlaceListManager] listText가 null입니다!");
        }
        if (dataManager == null)
        {
            Debug.LogError("[PlaceListManager] dataManager가 null입니다!");
        }
        if (tourAPIManager == null)
        {
            Debug.LogError("[PlaceListManager] tourAPIManager가 null입니다!");
        }

        if (listText == null || dataManager == null || tourAPIManager == null)
        {
            Debug.LogError("[PlaceListManager] 필수 컴포넌트가 null이어서 초기화를 건너뜁니다.");
            return;
        }

        // 슬라이더 초기화
        if (distanceSlider != null)
        {
            distanceSlider.minValue = 100f;
            distanceSlider.maxValue = 10000f; // 최대 10km
            
            // 저장된 값 불러오기 (기본값 5000)
            float savedDistance = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
            maxDisplayDistance = savedDistance;
            distanceSlider.value = savedDistance;
            
            distanceSlider.onValueChanged.AddListener(OnDistanceSliderChanged);
            UpdateDistanceValueText();
            
            // 초기값으로 필터 즉시 적용
            if (dataManager != null) dataManager.UpdateDistanceFilter(maxDisplayDistance, 0, 0); // 위치는 나중에 업데이트됨
            if (tourAPIManager != null) tourAPIManager.UpdateDistanceFilter(maxDisplayDistance, 0, 0);
            
            Debug.Log($"[PlaceListManager] 슬라이더 초기화: value={maxDisplayDistance}m (Saved)");
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
        // ... (기존 코드 유지)
        Debug.Log("[PlaceListManager] 데이터 로딩 대기 시작...");
        float waitTime = 0f;

        while ((dataManager != null && !dataManager.IsDataLoaded() && dataManager.GetPlaceDataMap().Count == 0) ||
               (tourAPIManager != null && !tourAPIManager.IsDataLoaded() && tourAPIManager.GetPlaceDataMap().Count == 0))
        {
            waitTime += 1f;
            Debug.Log($"[PlaceListManager] 데이터 대기 중... {waitTime}초 - DataManager={dataManager?.IsDataLoaded()}, TourAPI={tourAPIManager?.IsDataLoaded()}");
            yield return new WaitForSeconds(1f);

            if (waitTime >= 30f)
            {
                Debug.LogWarning("[PlaceListManager] 데이터 로딩 타임아웃 (30초) - 강제로 UI 업데이트 시도");
                break;
            }
        }

        Debug.Log("[PlaceListManager] 데이터 로딩 완료! 첫 UI 업데이트 시작");
        UpdateUI();
        StartCoroutine(UpdateUIPeriodically());
    }

    private IEnumerator UpdateUIPeriodically()
    {
        float startTime = Time.time;
        
        while (true)
        {
            // 처음 1분(60초) 동안은 1초 간격, 그 이후는 설정된 간격(10초)
            float currentInterval = (Time.time - startTime < 60f) ? 1f : updateInterval;
            
            yield return new WaitForSeconds(currentInterval);

            // ListPanel이 활성화되어 있을 때만 업데이트
            if (listPanel != null && listPanel.activeInHierarchy)
            {
                UpdateUI();
                Debug.Log($"[PlaceListManager] ListPanel 활성화 - UI 업데이트 실행 (간격: {currentInterval}s)");
            }
        }
    }

    private void UpdateUI()
    {
        Debug.Log("[DEBUG_TRACE] PlaceListManager: UpdateUI() Called");

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

        Debug.Log($"[DEBUG_TRACE] PlaceListManager: Fetched Data - Woopang={woopangCount}, TourAPI={tourAPICount}");

        if (listText != null)
        {
            listText.text = "";
        }
        else
        {
            Debug.LogError("[DEBUG_TRACE] PlaceListManager: listText is null!");
            return;
        }

        combinedPlaces.Clear();

        // 🔧 우팡데이터 필터 체크 추가
        bool showWoopangData = activeFilters.ContainsKey("woopangData") && activeFilters["woopangData"];
        bool showPetFriendly = activeFilters.ContainsKey("petFriendly") && activeFilters["petFriendly"];
        bool showAlcohol = activeFilters.ContainsKey("alcohol") && activeFilters["alcohol"];
        bool showPublicData = activeFilters.ContainsKey("publicData") && activeFilters["publicData"];

        Debug.Log($"[DEBUG_TRACE] PlaceListManager: Filter Status - WoopangData={showWoopangData}, PetFriendly={showPetFriendly}");

        if (showWoopangData)
        {
            int filteredCount = 0;
            foreach (var place in woopangPlaces)
            {
                // 애견동반 필터 체크
                if (place.pet_friendly && !showPetFriendly)
                {
                    filteredCount++;
                    continue; // 애견동반 필터가 꺼져있고 장소가 애견동반이면 건너뛰기
                }

                // 주류 판매 필터 체크
                if (place.alcohol_available && !showAlcohol)
                {
                    filteredCount++;
                    continue; // 주류 필터가 꺼져있고 장소가 주류 판매하면 건너뛰기
                }

                float distance = CalculateDistance(latitude, longitude, place.latitude, place.longitude);
                
                // 거리 필터 적용
                if (distance > maxDisplayDistance) continue;

                string distanceText = $"{Mathf.FloorToInt(distance)}m";
                string displayText = place.pet_friendly
                    ? $"{place.name} - {distanceText} {GetLocalizedText("petFriendly")}"
                    : $"{place.name} - {distanceText}";
                string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;
                combinedPlaces.Add((place, distance, place.id.ToString(), displayText, colorHex));
            }
            Debug.Log($"[DEBUG_TRACE] PlaceListManager: Woopang Filter Result - Added={woopangPlaces.Count - filteredCount}, Filtered={filteredCount}");
        }
        else
        {
            Debug.Log("[DEBUG_TRACE] PlaceListManager: WoopangData filter is OFF");
        }

        // 공공데이터(TourAPI) 필터 체크
        if (showPublicData)
        {
            int tourFilteredCount = 0;
            foreach (var place in tourPlaces)
            {
                // 애견동반 필터 체크 (TourAPI는 모두 애견동반)
                if (!showPetFriendly)
                {
                    tourFilteredCount++;
                    continue;
                }

                float distance = CalculateDistance(latitude, longitude, place.mapy, place.mapx);
                
                // 거리 필터 적용
                if (distance > maxDisplayDistance) continue;

                string distanceText = $"{Mathf.FloorToInt(distance)}m";
                string displayText = string.IsNullOrEmpty(place.firstimage)
                    ? $"{place.title} - {distanceText} {GetLocalizedText("noImage")} {GetLocalizedText("petFriendly")}"
                    : $"{place.title} - {distanceText} {GetLocalizedText("petFriendly")}";
                string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;
                combinedPlaces.Add((place, distance, place.contentid, displayText, colorHex));
            }
            Debug.Log($"[PlaceListManager] TourAPI데이터 처리 - 전체: {tourPlaces.Count}, 필터링됨: {tourFilteredCount}, 추가됨: {tourPlaces.Count - tourFilteredCount}");
        }
        else
        {
            Debug.Log("[PlaceListManager] 공공데이터 필터가 꺼져있어 표시하지 않음");
        }

        combinedPlaces = combinedPlaces.OrderBy(x => x.distance).ToList();

        Debug.Log($"[PlaceListManager] 리스트 업데이트 - 전체 데이터: 우팡={woopangPlaces.Count}, TourAPI={tourPlaces.Count}, 필터링 후 표시={combinedPlaces.Count}");

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

    private void OnDistanceSliderChanged(float value)
    {
        maxDisplayDistance = value;
        PlayerPrefs.SetFloat("MaxDisplayDistance", value); // 값 저장
        PlayerPrefs.Save();
        
        UpdateDistanceValueText();
        UpdateUI(); // 리스트 갱신 및 AR 오브젝트 제어
        
        // AR 오브젝트에도 거리 필터 적용
        if (dataManager != null) dataManager.UpdateDistanceFilter(maxDisplayDistance, Input.location.lastData.latitude, Input.location.lastData.longitude);
        if (tourAPIManager != null) tourAPIManager.UpdateDistanceFilter(maxDisplayDistance, Input.location.lastData.latitude, Input.location.lastData.longitude);
    }

    private void UpdateDistanceValueText()
    {
        if (distanceValueText != null)
        {
            if (maxDisplayDistance >= 1000f)
                distanceValueText.text = $"{(maxDisplayDistance / 1000f):F1}km";
            else
                distanceValueText.text = $"{Mathf.RoundToInt(maxDisplayDistance)}m";
        }
    }
}