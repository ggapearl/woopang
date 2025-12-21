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

    [Tooltip("PlaceList 스켈레톤 로더 (옵션)")]
    [SerializeField] private PlaceListSkeletonLoader skeletonLoader;

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

        if (listText == null)
        {
        }
        if (dataManager == null)
        {
        }
        if (tourAPIManager == null)
        {
        }

        if (listText == null || dataManager == null || tourAPIManager == null)
        {
            return;
        }

        // 슬라이더 초기화
        if (distanceSlider != null)
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
        // 스켈레톤 로더 시작
        if (skeletonLoader != null)
        {
            skeletonLoader.ShowSkeletonLoader();
            Debug.Log("[WoopangDebug][PlaceListManager] 스켈레톤 로더 시작");
        }

        float waitTime = 0f;

        while ((dataManager != null && !dataManager.IsDataLoaded() && dataManager.GetPlaceDataMap().Count == 0) ||
               (tourAPIManager != null && !tourAPIManager.IsDataLoaded() && tourAPIManager.GetPlaceDataMap().Count == 0))
        {
            waitTime += 1f;
            yield return new WaitForSeconds(1f);

            if (waitTime >= 30f)
            {
                break;
            }
        }

        // 스켈레톤 로더 종료 및 텍스트 표시
        if (skeletonLoader != null)
        {
            skeletonLoader.HideSkeletonAndShowText();
            Debug.Log("[WoopangDebug][PlaceListManager] 스켈레톤 로더 종료");
        }

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
            }
        }
    }

    private Coroutine updateUICoroutine;
    private List<PlaceData> pendingWoopangPlaces = new List<PlaceData>();
    private float currentMaxRadius = 0f;
    private int lastDisplayedCount = 0; // 마지막으로 표시된 항목 수 추적

    /// <summary>
    /// DataManager에서 Tier별로 호출하는 메서드
    /// </summary>
    public void UpdateUIForTier(int tierIndex, float radius)
    {
        Debug.Log($"[WoopangDebug][PlaceListManager] UpdateUIForTier 호출 - Tier {tierIndex}, 반경 {radius}m");

        currentMaxRadius = radius;

        // 기존 코루틴 중단하지 않고 계속 누적
        if (updateUICoroutine == null)
        {
            updateUICoroutine = StartCoroutine(UpdateUIWithFadeIn());
        }
    }

    private void UpdateUI()
    {
        // 기존 업데이트 코루틴 중단
        if (updateUICoroutine != null)
        {
            StopCoroutine(updateUICoroutine);
        }

        updateUICoroutine = StartCoroutine(UpdateUIWithFadeIn());
    }

    private IEnumerator UpdateUIWithFadeIn()
    {
        Debug.Log("[WoopangDebug][PlaceListManager] UpdateUIWithFadeIn 시작");

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
            yield break;
        }

        combinedPlaces.Clear();

        // 🔧 우팡데이터 필터 체크 추가
        bool showWoopangData = activeFilters.ContainsKey("woopangData") && activeFilters["woopangData"];
        bool showPetFriendly = activeFilters.ContainsKey("petFriendly") && activeFilters["petFriendly"];
        bool showAlcohol = activeFilters.ContainsKey("alcohol") && activeFilters["alcohol"];
        bool showPublicData = activeFilters.ContainsKey("publicData") && activeFilters["publicData"];


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
        }
        else
        {
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
        }
        else
        {
        }

        // 거리순 정렬 (가까운 순)
        combinedPlaces = combinedPlaces.OrderBy(x => x.distance).ToList();

        Debug.Log($"[WoopangDebug][PlaceListManager] 정렬 완료 - 총 {combinedPlaces.Count}개 항목");

        // ⭐ 이미 표시된 항목 개수 추적 (변수 사용으로 최적화)
        int previouslyDisplayedCount = lastDisplayedCount;

        // ⭐ 항목이 줄어들었으면 전체 리셋 (필터 변경 등)
        if (combinedPlaces.Count < previouslyDisplayedCount)
        {
            previouslyDisplayedCount = 0;
            lastDisplayedCount = 0;
            Debug.Log($"[WoopangDebug][PlaceListManager] 항목 감소 감지 - UI 리셋");
        }

        Debug.Log($"[WoopangDebug][PlaceListManager] 이전 표시 항목: {previouslyDisplayedCount}개, 새 항목: {combinedPlaces.Count - previouslyDisplayedCount}개");

        // ⭐ StringBuilder를 사용하여 성능 최적화 (String concatenation 제거)
        System.Text.StringBuilder textBuilder = new System.Text.StringBuilder();

        // ⭐ 새로운 항목만 추가하는 경우: 기존 텍스트 유지
        if (previouslyDisplayedCount > 0 && previouslyDisplayedCount <= combinedPlaces.Count)
        {
            // 기존 텍스트에서 통계 정보만 제거
            string currentText = listText.text;
            currentText = currentText.Replace($"\n{GetLocalizedText("woopangData")}: {woopangPlaces.Count}\n", "")
                                     .Replace($"{GetLocalizedText("tourApiData")}: {tourPlaces.Count}", "");
            textBuilder.Append(currentText);
        }
        // ⭐ 리셋된 경우나 처음: 빈 문자열에서 시작
        else
        {
            listText.text = "";
        }

        // 새로운 항목만 0.1초 간격으로 추가
        for (int i = previouslyDisplayedCount; i < combinedPlaces.Count; i++)
        {
            var (place, distance, id, displayText, colorHex) = combinedPlaces[i];

            string coloredText = $"<color=#{colorHex}>{displayText}</color>\n";
            textBuilder.Append(coloredText);

            // UI에 즉시 반영 (페이드인 효과를 위해)
            listText.text = textBuilder.ToString();

            // 10개마다 디버그 로그
            if ((i + 1) % 10 == 0)
            {
                Debug.Log($"[WoopangDebug][PlaceListManager] {i + 1}/{combinedPlaces.Count}개 항목 표시됨");
            }

            // 0.1초 대기
            yield return new WaitForSeconds(0.1f);
        }

        // 통계 정보 추가
        textBuilder.Append($"\n{GetLocalizedText("woopangData")}: {woopangPlaces.Count}\n");
        textBuilder.Append($"{GetLocalizedText("tourApiData")}: {tourPlaces.Count}");
        listText.text = textBuilder.ToString();

        // ⭐ 표시된 항목 수 업데이트
        lastDisplayedCount = combinedPlaces.Count;

        Debug.Log($"[WoopangDebug][PlaceListManager] UpdateUIWithFadeIn 완료 - 총 {combinedPlaces.Count}개 (lastDisplayedCount: {lastDisplayedCount})");

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
        lastDisplayedCount = 0; // ⭐ 필터 변경 시 리셋
        UpdateUI(); // UI 즉시 업데이트
    }

    private void OnDistanceSliderChanged(float value)
    {
        maxDisplayDistance = value;
        PlayerPrefs.SetFloat("MaxDisplayDistance", value); // 값 저장
        PlayerPrefs.Save();

        UpdateDistanceValueText();
        lastDisplayedCount = 0; // ⭐ 거리 필터 변경 시 리셋
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