using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 장소 리스트 필터링을 관리하는 컴포넌트
/// - 일반 클릭: 토글 ON/OFF
/// - 길게 누르기 (Long Press): 해당 토글만 활성화, 나머지 비활성화
/// - 설정은 PlayerPrefs로 영속성 유지
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
    [SerializeField] private Toggle object3DToggle;     // 3D 오브젝트 (GLB, OBJ 등)

    [Header("Control Buttons")]
    [SerializeField] private Button selectAllButton;    // 전체 선택 버튼
    [SerializeField] private Button deselectAllButton;  // 전체 해제 버튼

    [Header("References")]
    [SerializeField] private PlaceListManager placeListManager;
    [SerializeField] private DataManager dataManager;         // 우팡 서버 데이터 (AR 큐브)
    [SerializeField] private TourAPIManager tourAPIManager;   // 공공데이터 (AR 큐브)

    [Header("Long Press Settings")]
    [SerializeField] private float longPressDuration = 0.8f;  // 길게 누르기 판정 시간 (초)

    // 필터 상태
    private bool filterPetFriendly = true;
    private bool filterPublicData = true;
    private bool filterSubway = true;
    private bool filterBus = true;
    private bool filterAlcohol = true;
    private bool filterWoopangData = true;
    private bool filterObject3D = true;

    // 프로그래매틱 변경 중 플래그 (이벤트 무한 루프 방지)
    private bool isUpdatingToggles = false;

    // Long Press 추적
    private Dictionary<Toggle, LongPressHandler> longPressHandlers = new Dictionary<Toggle, LongPressHandler>();

    // PlayerPrefs 키
    private const string PREF_PET_FRIENDLY = "Filter_PetFriendly";
    private const string PREF_PUBLIC_DATA = "Filter_PublicData";
    private const string PREF_SUBWAY = "Filter_Subway";
    private const string PREF_BUS = "Filter_Bus";
    private const string PREF_ALCOHOL = "Filter_Alcohol";
    private const string PREF_WOOPANG_DATA = "Filter_WoopangData";
    private const string PREF_OBJECT3D = "Filter_Object3D";

    void Start()
    {
        // 저장된 설정 불러오기
        LoadFilterSettings();

        // 토글 이벤트 리스너 등록 + Long Press Handler 추가
        SetupToggle(petFriendlyToggle, filterPetFriendly, OnPetFriendlyToggleChanged, "petFriendly");
        SetupToggle(publicDataToggle, filterPublicData, OnPublicDataToggleChanged, "publicData");
        SetupToggle(subwayToggle, filterSubway, OnSubwayToggleChanged, "subway");
        SetupToggle(busToggle, filterBus, OnBusToggleChanged, "bus");
        SetupToggle(alcoholToggle, filterAlcohol, OnAlcoholToggleChanged, "alcohol");
        SetupToggle(woopangDataToggle, filterWoopangData, OnWoopangDataToggleChanged, "woopangData");
        SetupToggle(object3DToggle, filterObject3D, OnObject3DToggleChanged, "object3D");

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

    private void SetupToggle(Toggle toggle, bool initialValue, UnityEngine.Events.UnityAction<bool> callback, string filterName)
    {
        if (toggle != null)
        {
            toggle.isOn = initialValue;
            toggle.onValueChanged.AddListener(callback);

            // Long Press Handler 추가
            LongPressHandler handler = toggle.gameObject.AddComponent<LongPressHandler>();
            handler.longPressDuration = longPressDuration;
            handler.onLongPress = () => OnLongPress(filterName);
            longPressHandlers[toggle] = handler;
        }
    }

    /// <summary>
    /// 길게 누르기 이벤트 핸들러
    /// </summary>
    private void OnLongPress(string filterName)
    {
        Debug.Log($"[FilterManager] Long Press 감지: {filterName}");

        isUpdatingToggles = true;

        // 선택된 필터만 켜고 나머지는 끄기
        filterPetFriendly = (filterName == "petFriendly");
        filterPublicData = (filterName == "publicData");
        filterSubway = (filterName == "subway");
        filterBus = (filterName == "bus");
        filterAlcohol = (filterName == "alcohol");
        filterWoopangData = (filterName == "woopangData");
        filterObject3D = (filterName == "object3D");

        UpdateAllToggleUI();
        SaveFilterSettings();

        isUpdatingToggles = false;
        ApplyAllFilters();
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
        filterObject3D = PlayerPrefs.GetInt(PREF_OBJECT3D, 1) == 1;

        Debug.Log($"[FilterManager] 설정 불러오기 완료 - PetFriendly: {filterPetFriendly}, PublicData: {filterPublicData}, Alcohol: {filterAlcohol}, WoopangData: {filterWoopangData}, Object3D: {filterObject3D}");
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
        PlayerPrefs.SetInt(PREF_OBJECT3D, filterObject3D ? 1 : 0);
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
        filterObject3D = true;

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
        filterObject3D = false;

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
        if (object3DToggle != null) object3DToggle.isOn = filterObject3D;
    }

    private void OnPetFriendlyToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        filterPetFriendly = isOn;
        SaveFilterSettings();
        ApplyAllFilters();
    }

    private void OnPublicDataToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        filterPublicData = isOn;
        SaveFilterSettings();
        ApplyAllFilters();
    }

    private void OnSubwayToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        filterSubway = isOn;
        SaveFilterSettings();
        ApplyAllFilters();
    }

    private void OnBusToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        filterBus = isOn;
        SaveFilterSettings();
        ApplyAllFilters();
    }

    private void OnAlcoholToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        filterAlcohol = isOn;
        SaveFilterSettings();
        ApplyAllFilters();
    }

    private void OnWoopangDataToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        filterWoopangData = isOn;
        SaveFilterSettings();
        ApplyAllFilters();
    }

    private void OnObject3DToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        filterObject3D = isOn;
        SaveFilterSettings();
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
            Debug.Log($"[FilterManager] DataManager.ApplyFilters 호출 - woopangData={filterWoopangData}");
        }

        // AR 오브젝트 필터링 (공공데이터)
        if (tourAPIManager != null)
        {
            tourAPIManager.ApplyFilters(filters);
            Debug.Log($"[FilterManager] TourAPIManager.ApplyFilters 호출 - publicData={filterPublicData}");
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
}

/// <summary>
/// 길게 누르기(Long Press)를 감지하는 컴포넌트
/// EventTrigger를 사용하여 PointerDown/PointerUp 이벤트 처리
/// </summary>
public class LongPressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public float longPressDuration = 0.8f;
    public System.Action onLongPress;

    private bool isPressed = false;
    private float pressedTime = 0f;
    private bool longPressTriggered = false;

    void Update()
    {
        if (isPressed && !longPressTriggered)
        {
            pressedTime += Time.deltaTime;
            if (pressedTime >= longPressDuration)
            {
                longPressTriggered = true;
                onLongPress?.Invoke();
                Debug.Log($"[LongPressHandler] Long Press 발생! ({pressedTime:F2}초)");
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        pressedTime = 0f;
        longPressTriggered = false;
        Debug.Log("[LongPressHandler] Press 시작");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (longPressTriggered)
        {
            // Long Press 발생했으면 Toggle의 일반 클릭 이벤트 무시
            Debug.Log("[LongPressHandler] Long Press 완료, 일반 클릭 무시");
        }
        else
        {
            Debug.Log($"[LongPressHandler] 일반 클릭 ({pressedTime:F2}초)");
        }

        isPressed = false;
        pressedTime = 0f;
        longPressTriggered = false;
    }
}
