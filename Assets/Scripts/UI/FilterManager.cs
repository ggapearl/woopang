using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장소 리스트 필터링을 관리하는 컴포넌트
/// 체크박스를 통해 애견동반, 공공데이터, 지하철, 버스, 주류 판매, 우팡데이터 등을 필터링
/// 토글 하나만 선택되면 나머지는 자동 해제 (단일 선택 모드)
/// 설정은 PlayerPrefs로 영속성 유지
/// </summary>
public class FilterManager : MonoBehaviour
{
    [Header("Filter Toggles")]
    [SerializeField] private Toggle petFriendlyToggle;  // 애견동반
    [SerializeField] private Toggle publicDataToggle;   // 공공데이터 (TourAPI)
    [SerializeField] private Toggle subwayToggle;       // 지하철
    [SerializeField] private Toggle busToggle;          // 버스
    [SerializeField] private Toggle alcoholToggle;      // 주류 판매
    [SerializeField] private Toggle woopangDataToggle;  // 우팡데이터

    [Header("Control Buttons")]
    [SerializeField] private Button selectAllButton;    // 전체 선택 버튼
    [SerializeField] private Button deselectAllButton;  // 전체 해제 버튼

    [Header("References")]
    [SerializeField] private PlaceListManager placeListManager;
    [SerializeField] private DataManager dataManager;         // 우팡 서버 데이터 (AR 큐브)
    [SerializeField] private TourAPIManager tourAPIManager;   // 공공데이터 (AR 큐브)

    [Header("Settings")]
    [SerializeField] private bool singleSelectMode = true;    // 단일 선택 모드 (하나만 켜면 다른건 꺼짐)

    // 필터 상태
    private bool filterPetFriendly = true;
    private bool filterPublicData = true;
    private bool filterSubway = true;
    private bool filterBus = true;
    private bool filterAlcohol = true;
    private bool filterWoopangData = true;

    // 프로그래매틱 변경 중 플래그 (이벤트 무한 루프 방지)
    private bool isUpdatingToggles = false;

    // PlayerPrefs 키
    private const string PREF_PET_FRIENDLY = "Filter_PetFriendly";
    private const string PREF_PUBLIC_DATA = "Filter_PublicData";
    private const string PREF_SUBWAY = "Filter_Subway";
    private const string PREF_BUS = "Filter_Bus";
    private const string PREF_ALCOHOL = "Filter_Alcohol";
    private const string PREF_WOOPANG_DATA = "Filter_WoopangData";

    void Start()
    {
        // 저장된 설정 불러오기
        LoadFilterSettings();

        // 토글 이벤트 리스너 등록
        SetupToggle(petFriendlyToggle, filterPetFriendly, OnPetFriendlyToggleChanged);
        SetupToggle(publicDataToggle, filterPublicData, OnPublicDataToggleChanged);
        SetupToggle(subwayToggle, filterSubway, OnSubwayToggleChanged);
        SetupToggle(busToggle, filterBus, OnBusToggleChanged);
        SetupToggle(alcoholToggle, filterAlcohol, OnAlcoholToggleChanged);
        SetupToggle(woopangDataToggle, filterWoopangData, OnWoopangDataToggleChanged);

        // 버튼 이벤트 리스너 등록
        if (selectAllButton != null)
        {
            selectAllButton.onClick.AddListener(SelectAll);
        }
        if (deselectAllButton != null)
        {
            deselectAllButton.onClick.AddListener(DeselectAll);
        }

        // 초기 필터 적용
        ApplyAllFilters();
    }

    private void SetupToggle(Toggle toggle, bool initialValue, UnityEngine.Events.UnityAction<bool> callback)
    {
        if (toggle != null)
        {
            toggle.isOn = initialValue;
            toggle.onValueChanged.AddListener(callback);
        }
    }

    /// <summary>
    /// 저장된 필터 설정 불러오기
    /// </summary>
    private void LoadFilterSettings()
    {
        filterPetFriendly = PlayerPrefs.GetInt(PREF_PET_FRIENDLY, 1) == 1;
        filterPublicData = PlayerPrefs.GetInt(PREF_PUBLIC_DATA, 1) == 1;
        filterSubway = PlayerPrefs.GetInt(PREF_SUBWAY, 1) == 1;
        filterBus = PlayerPrefs.GetInt(PREF_BUS, 1) == 1;
        filterAlcohol = PlayerPrefs.GetInt(PREF_ALCOHOL, 1) == 1;
        filterWoopangData = PlayerPrefs.GetInt(PREF_WOOPANG_DATA, 1) == 1;

        Debug.Log($"[FilterManager] 설정 불러오기 완료 - PetFriendly: {filterPetFriendly}, PublicData: {filterPublicData}, Alcohol: {filterAlcohol}, WoopangData: {filterWoopangData}");
    }

    /// <summary>
    /// 현재 필터 설정 저장하기
    /// </summary>
    private void SaveFilterSettings()
    {
        PlayerPrefs.SetInt(PREF_PET_FRIENDLY, filterPetFriendly ? 1 : 0);
        PlayerPrefs.SetInt(PREF_PUBLIC_DATA, filterPublicData ? 1 : 0);
        PlayerPrefs.SetInt(PREF_SUBWAY, filterSubway ? 1 : 0);
        PlayerPrefs.SetInt(PREF_BUS, filterBus ? 1 : 0);
        PlayerPrefs.SetInt(PREF_ALCOHOL, filterAlcohol ? 1 : 0);
        PlayerPrefs.SetInt(PREF_WOOPANG_DATA, filterWoopangData ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log("[FilterManager] 설정 저장 완료");
    }

    /// <summary>
    /// 전체 선택
    /// </summary>
    public void SelectAll()
    {
        isUpdatingToggles = true;

        filterPetFriendly = true;
        filterPublicData = true;
        filterSubway = true;
        filterBus = true;
        filterAlcohol = true;
        filterWoopangData = true;

        UpdateAllToggleUI();
        SaveFilterSettings();

        isUpdatingToggles = false;
        ApplyAllFilters();

        Debug.Log("[FilterManager] 전체 선택");
    }

    /// <summary>
    /// 전체 해제
    /// </summary>
    public void DeselectAll()
    {
        isUpdatingToggles = true;

        filterPetFriendly = false;
        filterPublicData = false;
        filterSubway = false;
        filterBus = false;
        filterAlcohol = false;
        filterWoopangData = false;

        UpdateAllToggleUI();
        SaveFilterSettings();

        isUpdatingToggles = false;
        ApplyAllFilters();

        Debug.Log("[FilterManager] 전체 해제");
    }

    /// <summary>
    /// 모든 토글 UI 업데이트
    /// </summary>
    private void UpdateAllToggleUI()
    {
        if (petFriendlyToggle != null) petFriendlyToggle.isOn = filterPetFriendly;
        if (publicDataToggle != null) publicDataToggle.isOn = filterPublicData;
        if (subwayToggle != null) subwayToggle.isOn = filterSubway;
        if (busToggle != null) busToggle.isOn = filterBus;
        if (alcoholToggle != null) alcoholToggle.isOn = filterAlcohol;
        if (woopangDataToggle != null) woopangDataToggle.isOn = filterWoopangData;
    }

    /// <summary>
    /// 단일 선택 모드일 때 다른 토글 해제
    /// </summary>
    private void HandleSingleSelectMode(string selectedFilter)
    {
        if (!singleSelectMode) return;

        isUpdatingToggles = true;

        // 선택된 필터만 켜고 나머지는 끄기
        filterPetFriendly = (selectedFilter == "petFriendly");
        filterPublicData = (selectedFilter == "publicData");
        filterSubway = (selectedFilter == "subway");
        filterBus = (selectedFilter == "bus");
        filterAlcohol = (selectedFilter == "alcohol");
        filterWoopangData = (selectedFilter == "woopangData");

        UpdateAllToggleUI();
        SaveFilterSettings();

        isUpdatingToggles = false;
    }

    private void OnPetFriendlyToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        if (singleSelectMode && isOn)
        {
            HandleSingleSelectMode("petFriendly");
        }
        else
        {
            filterPetFriendly = isOn;
            SaveFilterSettings();
        }
        ApplyAllFilters();
    }

    private void OnPublicDataToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        if (singleSelectMode && isOn)
        {
            HandleSingleSelectMode("publicData");
        }
        else
        {
            filterPublicData = isOn;
            SaveFilterSettings();
        }
        ApplyAllFilters();
    }

    private void OnSubwayToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        if (singleSelectMode && isOn)
        {
            HandleSingleSelectMode("subway");
        }
        else
        {
            filterSubway = isOn;
            SaveFilterSettings();
        }
        ApplyAllFilters();
    }

    private void OnBusToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        if (singleSelectMode && isOn)
        {
            HandleSingleSelectMode("bus");
        }
        else
        {
            filterBus = isOn;
            SaveFilterSettings();
        }
        ApplyAllFilters();
    }

    private void OnAlcoholToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        if (singleSelectMode && isOn)
        {
            HandleSingleSelectMode("alcohol");
        }
        else
        {
            filterAlcohol = isOn;
            SaveFilterSettings();
        }
        ApplyAllFilters();
    }

    private void OnWoopangDataToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        if (singleSelectMode && isOn)
        {
            HandleSingleSelectMode("woopangData");
        }
        else
        {
            filterWoopangData = isOn;
            SaveFilterSettings();
        }
        ApplyAllFilters();
    }

    /// <summary>
    /// 모든 필터를 적용 (PlaceListManager, DataManager, TourAPIManager)
    /// </summary>
    private void ApplyAllFilters()
    {
        Dictionary<string, bool> filters = GetActiveFilters();

        // UI 리스트 필터링
        if (placeListManager != null)
        {
            placeListManager.ApplyFilters(filters);
        }

        // AR 오브젝트 필터링 (우팡 데이터)
        if (dataManager != null)
        {
            dataManager.ApplyFilters(filters);
        }

        // AR 오브젝트 필터링 (공공데이터)
        if (tourAPIManager != null)
        {
            tourAPIManager.ApplyFilters(filters);
        }

        Debug.Log($"[FilterManager] 필터 적용 - PetFriendly: {filterPetFriendly}, PublicData: {filterPublicData}, Alcohol: {filterAlcohol}, WoopangData: {filterWoopangData}");
    }

    public Dictionary<string, bool> GetActiveFilters()
    {
        return new Dictionary<string, bool>
        {
            { "petFriendly", filterPetFriendly },
            { "publicData", filterPublicData },
            { "subway", filterSubway },
            { "bus", filterBus },
            { "alcohol", filterAlcohol },
            { "woopangData", filterWoopangData }
        };
    }

    /// <summary>
    /// 단일 선택 모드 설정/해제
    /// </summary>
    public void SetSingleSelectMode(bool enabled)
    {
        singleSelectMode = enabled;
        Debug.Log($"[FilterManager] 단일 선택 모드: {(enabled ? "활성화" : "비활성화")}");
    }

    /// <summary>
    /// 단일 선택 모드 상태 확인
    /// </summary>
    public bool IsSingleSelectMode()
    {
        return singleSelectMode;
    }
}
