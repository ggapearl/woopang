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
    [SerializeField] private float updateInterval = 60f;
    private List<(object place, float distance, string id, string displayText, string colorHex)> combinedPlaces = new List<(object, float, string, string, string)>();
    private int woopangCount;
    private int tourAPICount;

    // 필터 설정
    private Dictionary<string, bool> activeFilters = new Dictionary<string, bool>
    {
        { "petFriendly", true },
        { "publicData", true },
        { "subway", true },
        { "bus", true },
        { "alcohol", true }
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
        StartCoroutine(InitializeAndUpdateUI());
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
            UpdateUI();
            yield return new WaitForSeconds(updateInterval);
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

                float distance = CalculateDistance(latitude, longitude, place.latitude, place.longitude);
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
                // 애견동반 필터 체크 (TourAPI는 모두 애견동반)
                if (!activeFilters["petFriendly"])
                {
                    continue;
                }

                float distance = CalculateDistance(latitude, longitude, place.mapy, place.mapx);
                string distanceText = $"{Mathf.FloorToInt(distance)}m";
                string displayText = string.IsNullOrEmpty(place.firstimage)
                    ? $"{place.title} - {distanceText} {GetLocalizedText("noImage")} {GetLocalizedText("petFriendly")}"
                    : $"{place.title} - {distanceText} {GetLocalizedText("petFriendly")}";
                string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;
                combinedPlaces.Add((place, distance, place.contentid, displayText, colorHex));
            }
        }

        combinedPlaces = combinedPlaces.OrderBy(x => x.distance).ToList();
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
}