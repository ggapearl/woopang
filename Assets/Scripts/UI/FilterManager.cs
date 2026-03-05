using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class FilterManager : MonoBehaviour
{
    [Header("Filter Toggles")]
    [SerializeField] private Toggle petFriendlyToggle;
    private Toggle p2pToggle;  // 자동 연결 (Inspector 사용 안함)
    [SerializeField] private Toggle publicDataToggle;
    [SerializeField] private Toggle subwayToggle;
    [SerializeField] private Toggle alcoholToggle;
    [SerializeField] private Toggle trainToggle;
    [SerializeField] private Toggle terminalToggle;
    [SerializeField] private Toggle object3DToggle;

    [Header("References")]
    [SerializeField] private PlaceListManager placeListManager;
    [SerializeField] private DataManager dataManager;
    [SerializeField] private TourAPIManager tourAPIManager;
    [SerializeField] private TerminalManager terminalManager;
    [SerializeField] private TrainStationManager trainStationManager;
    [SerializeField] private SubwayManager subwayManager;

    [Header("Long Press Settings")]
    [SerializeField] private float longPressDuration = 0.8f;

    // 필터 상태
    public enum PetFriendlyFilterState
    {
        All = 0,                 // 모두 표시 (흰색)
        OnlyPetFriendly = 1,     // 애견동반 되는 곳만 (노란색)
        NoPetFriendly = 2        // 애견동반 안되는 곳만 (체크해제)
    }

    // P2P 사용자 필터 상태
    public enum P2PFilterState
    {
        All = 0,                 // P2P 전체 (핑크색)
        FollowingOnly = 1,       // P2P 팔로잉 (흰색)
        None = 2                 // P2P 제외 (회색)
    }

    private PetFriendlyFilterState petFriendlyState = PetFriendlyFilterState.All;  // 기본값: 포함
    private P2PFilterState p2pFilterState = P2PFilterState.FollowingOnly;  // 기본값: 팔로잉
    private bool filterPublicData = true;
    private bool filterSubway = true;
    private bool filterAlcohol = true;
    private bool filterTrain = true;
    private bool filterTerminal = true;
    private bool filterObject3D = true;

    private bool isUpdatingToggles = false;

    // PlayerPrefs 키 (V2)
    private const string PREF_PET_FRIENDLY = "Filter_PetFriendly_V3";
    private const string PREF_P2P = "Filter_P2P_V1";
    private const string PREF_PUBLIC_DATA = "Filter_PublicData_V2";
    private const string PREF_SUBWAY = "Filter_Subway_V2";
    private const string PREF_ALCOHOL = "Filter_Alcohol_V2";
    private const string PREF_TRAIN = "Filter_Train_V2";
    private const string PREF_TERMINAL = "Filter_Terminal_V2";
    private const string PREF_OBJECT3D = "Filter_Object3D_V2";

    // 다국어 데이터
    private Dictionary<string, Dictionary<string, string>> localizedFilterNames = new Dictionary<string, Dictionary<string, string>>
    {
        { "en", new Dictionary<string, string> {
            { "petFriendlyInclude", "Pet Friendly (Include)" },
            { "petFriendlyRequired", "Pet Friendly (Required)" },
            { "petFriendlyExclude", "Pet Friendly (Exclude)" },
            { "p2pAll", "P2P (All)" },
            { "p2pFollowing", "P2P (Following)" },
            { "p2pNone", "P2P (Exclude)" },
            { "publicData", "Public Data" },
            { "subway", "Metro" },
            { "alcohol", "Alcohol" },
            { "train", "Train" },
            { "terminal", "Terminal/Airport" },
            { "object3D", "3D Objects" }
        }},
        { "ko", new Dictionary<string, string> {
            { "petFriendlyInclude", "애견동반(포함)" },
            { "petFriendlyRequired", "애견동반(필수)" },
            { "petFriendlyExclude", "애견동반(제외)" },
            { "p2pAll", "P2P(전체)" },
            { "p2pFollowing", "P2P(팔로잉)" },
            { "p2pNone", "P2P(제외)" },
            { "publicData", "공공데이터" },
            { "subway", "지하철" },
            { "alcohol", "주류판매" },
            { "train", "기차역" },
            { "terminal", "터미널" },
            { "object3D", "3D 오브젝트" }
        }},
        { "ja", new Dictionary<string, string> {
            { "petFriendlyInclude", "ペット同伴(含む)" },
            { "petFriendlyRequired", "ペット同伴(必須)" },
            { "petFriendlyExclude", "ペット同伴(除外)" },
            { "p2pAll", "P2P(全員)" },
            { "p2pFollowing", "P2P(フォロー中)" },
            { "p2pNone", "P2P(除外)" },
            { "publicData", "公共データ" },
            { "subway", "地下鉄" },
            { "alcohol", "アルコール" },
            { "train", "鉄道駅" },
            { "terminal", "ターミナル" },
            { "object3D", "3Dオブジェクト" }
        }},
        { "zh", new Dictionary<string, string> {
            { "petFriendlyInclude", "宠物友好(包含)" },
            { "petFriendlyRequired", "宠物友好(必须)" },
            { "petFriendlyExclude", "宠物友好(排除)" },
            { "p2pAll", "P2P(全部)" },
            { "p2pFollowing", "P2P(关注)" },
            { "p2pNone", "P2P(排除)" },
            { "publicData", "公共数据" },
            { "subway", "地铁" },
            { "alcohol", "酒类销售" },
            { "train", "火车站" },
            { "terminal", "航站楼" },
            { "object3D", "3D对象" }
        }},
        { "es", new Dictionary<string, string> {
            { "petFriendlyInclude", "Mascotas (Incluir)" },
            { "petFriendlyRequired", "Mascotas (Obligatorio)" },
            { "petFriendlyExclude", "Mascotas (Excluir)" },
            { "p2pAll", "P2P (Todos)" },
            { "p2pFollowing", "P2P (Siguiendo)" },
            { "p2pNone", "P2P (Excluir)" },
            { "publicData", "Datos Públicos" },
            { "subway", "Metro" },
            { "alcohol", "Alcohol" },
            { "train", "Estación de Tren" },
            { "terminal", "Terminal" },
            { "object3D", "Objetos 3D" }
        }}
    };

    // 현재 언어 코드 저장
    private string currentLangCode = "en";

    // P2P 토글은 P2PUserFilterPanel이 전담 처리 (충돌 방지)
    // FilterManager에서는 P2P 토글을 연결하지 않음

    void Start()
    {
        // Inspector에 연결되지 않은 매니저는 자동으로 찾기
        if (terminalManager == null) terminalManager = Object.FindFirstObjectByType<TerminalManager>();
        if (trainStationManager == null) trainStationManager = Object.FindFirstObjectByType<TrainStationManager>();
        if (subwayManager == null) subwayManager = Object.FindFirstObjectByType<SubwayManager>();

        LoadFilterSettings();
        UpdateLanguage();

        // PetFriendly는 별도 처리 (3-state)
        if (petFriendlyToggle != null)
        {
            petFriendlyToggle.onValueChanged.AddListener(OnPetFriendlyToggleChanged);
            UpdatePetFriendlyToggleUI();
        }

        SetupToggle(publicDataToggle, filterPublicData, OnPublicDataToggleChanged, "publicData");
        SetupToggle(subwayToggle, filterSubway, OnSubwayToggleChanged, "subway");
        SetupToggle(alcoholToggle, filterAlcohol, OnAlcoholToggleChanged, "alcohol");
        SetupToggle(trainToggle, filterTrain, OnTrainToggleChanged, "train");
        SetupToggle(terminalToggle, filterTerminal, OnTerminalToggleChanged, "terminal");
        SetupToggle(object3DToggle, filterObject3D, OnObject3DToggleChanged, "object3D");

        ApplyAllFilters();
    }

    private void UpdateLanguage()
    {
        currentLangCode = "en";
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: currentLangCode = "ko"; break;
            case SystemLanguage.Japanese: currentLangCode = "ja"; break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: currentLangCode = "zh"; break;
            case SystemLanguage.Spanish: currentLangCode = "es"; break;
        }

        if (!localizedFilterNames.ContainsKey(currentLangCode)) currentLangCode = "en";
        var texts = localizedFilterNames[currentLangCode];

        // petFriendly는 상태에 따라 다르게 표시하므로 UpdatePetFriendlyToggleUI에서 처리
        SetToggleLabel(publicDataToggle, texts["publicData"]);
        SetToggleLabel(subwayToggle, texts["subway"]);
        SetToggleLabel(alcoholToggle, texts["alcohol"]);
        SetToggleLabel(trainToggle, texts["train"]);
        SetToggleLabel(terminalToggle, texts["terminal"]);
        SetToggleLabel(object3DToggle, texts["object3D"]);
    }

    private void SetToggleLabel(Toggle toggle, string text)
    {
        if (toggle == null) return;

        // Legacy Text 시도
        Text label = toggle.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = text;
        }

        // TMPro Text 시도
        TextMeshProUGUI tmpLabel = toggle.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpLabel != null)
        {
            tmpLabel.text = text;
        }
    }

    // 애견동반 필터 색상 (하드코딩)
    private static readonly Color PET_COLOR_YELLOW = new Color(0.984f, 0.757f, 0.365f);  // 노란색 #fbc15d (필수)
    private static readonly Color PET_COLOR_WHITE = Color.white;                          // 흰색 (포함)
    private static readonly Color PET_COLOR_GRAY = new Color(0.5f, 0.5f, 0.5f);          // 회색 (제외)

    private void UpdatePetFriendlyToggleUI()
    {
        if (petFriendlyToggle == null) return;

        isUpdatingToggles = true;

        // isOn은 항상 true로 유지 (Unity Toggle 색상 처리 방지)
        petFriendlyToggle.isOn = true;

        // 다국어 텍스트 가져오기
        var texts = localizedFilterNames.ContainsKey(currentLangCode)
            ? localizedFilterNames[currentLangCode]
            : localizedFilterNames["en"];

        string newLabelText = "";
        Color bgColor = PET_COLOR_YELLOW;

        // 상태에 따라 UI 업데이트
        // 필수(노란색) -> 포함(흰색) -> 제외(회색)
        switch (petFriendlyState)
        {
            case PetFriendlyFilterState.OnlyPetFriendly:
                bgColor = PET_COLOR_YELLOW;  // 노란색
                newLabelText = texts["petFriendlyRequired"];  // 애견동반(필수)
                break;
            case PetFriendlyFilterState.All:
                bgColor = PET_COLOR_WHITE;  // 흰색
                newLabelText = texts["petFriendlyInclude"];  // 애견동반(포함)
                break;
            case PetFriendlyFilterState.NoPetFriendly:
                bgColor = PET_COLOR_GRAY;  // 회색
                newLabelText = texts["petFriendlyExclude"];  // 애견동반(제외)
                break;
        }

        SetToggleBackgroundAndCheckmark(petFriendlyToggle, bgColor);
        SetToggleLabel(petFriendlyToggle, newLabelText);

        isUpdatingToggles = false;
    }

    // P2P 필터 색상 (하드코딩)
    private static readonly Color P2P_COLOR_FOLLOWING = new Color(0.91f, 0.33f, 0.63f);  // 핑크색 #E854A1 (팔로잉)
    private static readonly Color P2P_COLOR_ALL = Color.white;                            // 흰색 (전체)
    private static readonly Color P2P_COLOR_NONE = new Color(0.5f, 0.5f, 0.5f);          // 회색 (제외)

    private void UpdateP2PToggleUI()
    {
        if (p2pToggle == null) return;

        isUpdatingToggles = true;

        // isOn은 항상 true로 유지 (Unity Toggle 색상 처리 방지)
        p2pToggle.isOn = true;

        // 다국어 텍스트 가져오기
        var texts = localizedFilterNames.ContainsKey(currentLangCode)
            ? localizedFilterNames[currentLangCode]
            : localizedFilterNames["en"];

        string newLabelText = "";
        Color bgColor = P2P_COLOR_FOLLOWING;

        // 상태에 따라 UI 업데이트
        // 팔로잉(핑크색) -> 전체(흰색) -> 제외(회색)
        switch (p2pFilterState)
        {
            case P2PFilterState.FollowingOnly:
                bgColor = P2P_COLOR_FOLLOWING;  // 핑크색
                newLabelText = texts["p2pFollowing"];
                break;
            case P2PFilterState.All:
                bgColor = P2P_COLOR_ALL;        // 흰색
                newLabelText = texts["p2pAll"];
                break;
            case P2PFilterState.None:
                bgColor = P2P_COLOR_NONE;       // 회색
                newLabelText = texts["p2pNone"];
                break;
        }

        SetToggleBackgroundAndCheckmark(p2pToggle, bgColor);
        SetToggleLabel(p2pToggle, newLabelText);

        isUpdatingToggles = false;
    }

    private void SetToggleBackground(Toggle toggle, Color color)
    {
        if (toggle == null) return;

        // Toggle의 "Background" 자식 오브젝트에서 Image 찾기
        Transform bgTransform = toggle.transform.Find("Background");
        if (bgTransform != null)
        {
            Image bgImage = bgTransform.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = color;
            }
        }
    }

    /// <summary>
    /// Toggle의 Background와 Checkmark 모두 같은 색상으로 설정
    /// </summary>
    private void SetToggleBackgroundAndCheckmark(Toggle toggle, Color color)
    {
        if (toggle == null) return;

        // Background 색상 설정
        Transform bgTransform = toggle.transform.Find("Background");
        if (bgTransform != null)
        {
            Image bgImage = bgTransform.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = color;
            }

            // Checkmark도 같은 색상으로 (Background 아래에 있는 경우)
            Transform checkmark = bgTransform.Find("Checkmark");
            if (checkmark != null)
            {
                Image checkImage = checkmark.GetComponent<Image>();
                if (checkImage != null)
                {
                    checkImage.color = color;
                }
            }
        }

        // Checkmark가 직접 자식인 경우
        Transform directCheckmark = toggle.transform.Find("Checkmark");
        if (directCheckmark != null)
        {
            Image checkImage = directCheckmark.GetComponent<Image>();
            if (checkImage != null)
            {
                checkImage.color = color;
            }
        }

        // Graphic (targetGraphic) 색상도 설정
        if (toggle.graphic != null)
        {
            toggle.graphic.color = color;
        }
    }

    private void SetToggleLabelColor(Toggle toggle, Color color)
    {
        if (toggle == null) return;

        // Toggle의 Label Text 찾기
        Text label = toggle.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.color = color;
        }
    }

    private void SetupToggle(Toggle toggle, bool initialValue, UnityEngine.Events.UnityAction<bool> callback, string filterName)
    {
        if (toggle != null)
        {
            toggle.isOn = initialValue;
            toggle.onValueChanged.AddListener(callback);

            LongPressHandler handler = toggle.gameObject.AddComponent<LongPressHandler>();
            handler.longPressDuration = longPressDuration;
            handler.onLongPress = () => OnLongPress(filterName);
        }
    }

    private void OnLongPress(string filterName)
    {
        isUpdatingToggles = true;

        // PetFriendly는 All 상태로 설정
        petFriendlyState = (filterName == "petFriendly") ? PetFriendlyFilterState.All : PetFriendlyFilterState.NoPetFriendly;
        // P2P도 마찬가지
        p2pFilterState = (filterName == "p2p") ? P2PFilterState.All : P2PFilterState.None;
        filterPublicData = (filterName == "publicData");
        filterSubway = (filterName == "subway");
        filterAlcohol = (filterName == "alcohol");
        filterTrain = (filterName == "train");
        filterTerminal = (filterName == "terminal");
        filterObject3D = (filterName == "object3D");

        UpdateAllToggleUI();
        SaveFilterSettings();

        isUpdatingToggles = false;
        ApplyAllFilters();
    }

    private void LoadFilterSettings()
    {
        petFriendlyState = (PetFriendlyFilterState)PlayerPrefs.GetInt(PREF_PET_FRIENDLY, 0); // 기본값 All (0) = 포함
        p2pFilterState = (P2PFilterState)PlayerPrefs.GetInt(PREF_P2P, 1); // 기본값 FollowingOnly (1) = 팔로잉
        filterPublicData = PlayerPrefs.GetInt(PREF_PUBLIC_DATA, 1) == 1;
        filterSubway = PlayerPrefs.GetInt(PREF_SUBWAY, 1) == 1;
        filterAlcohol = PlayerPrefs.GetInt(PREF_ALCOHOL, 1) == 1;
        filterTrain = PlayerPrefs.GetInt(PREF_TRAIN, 1) == 1;
        filterTerminal = PlayerPrefs.GetInt(PREF_TERMINAL, 1) == 1;
        filterObject3D = PlayerPrefs.GetInt(PREF_OBJECT3D, 1) == 1;
    }

    private void SaveFilterSettings()
    {
        PlayerPrefs.SetInt(PREF_PET_FRIENDLY, (int)petFriendlyState);
        PlayerPrefs.SetInt(PREF_P2P, (int)p2pFilterState);
        PlayerPrefs.SetInt(PREF_PUBLIC_DATA, filterPublicData ? 1 : 0);
        PlayerPrefs.SetInt(PREF_SUBWAY, filterSubway ? 1 : 0);
        PlayerPrefs.SetInt(PREF_ALCOHOL, filterAlcohol ? 1 : 0);
        PlayerPrefs.SetInt(PREF_TRAIN, filterTrain ? 1 : 0);
        PlayerPrefs.SetInt(PREF_TERMINAL, filterTerminal ? 1 : 0);
        PlayerPrefs.SetInt(PREF_OBJECT3D, filterObject3D ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void UpdateAllToggleUI()
    {
        UpdatePetFriendlyToggleUI();
        // P2P 토글은 P2PUserFilterPanel이 전담 처리
        if (publicDataToggle != null) publicDataToggle.isOn = filterPublicData;
        if (subwayToggle != null) subwayToggle.isOn = filterSubway;
        if (alcoholToggle != null) alcoholToggle.isOn = filterAlcohol;
        if (trainToggle != null) trainToggle.isOn = filterTrain;
        if (terminalToggle != null) terminalToggle.isOn = filterTerminal;
        if (object3DToggle != null) object3DToggle.isOn = filterObject3D;
    }

    private void OnPetFriendlyToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        // 3단계 순환: 필수(노란색) -> 포함(흰색) -> 제외(회색)
        switch (petFriendlyState)
        {
            case PetFriendlyFilterState.OnlyPetFriendly:
                petFriendlyState = PetFriendlyFilterState.All;
                break;
            case PetFriendlyFilterState.All:
                petFriendlyState = PetFriendlyFilterState.NoPetFriendly;
                break;
            case PetFriendlyFilterState.NoPetFriendly:
                petFriendlyState = PetFriendlyFilterState.OnlyPetFriendly;
                break;
        }

        UpdatePetFriendlyToggleUI();
        SaveFilterSettings();
        ApplyAllFilters();
    }

    private void OnP2PToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        // 3단계 순환: 팔로잉(핑크색) -> 전체(흰색) -> 제외(회색)
        switch (p2pFilterState)
        {
            case P2PFilterState.FollowingOnly:
                p2pFilterState = P2PFilterState.All;
                break;
            case P2PFilterState.All:
                p2pFilterState = P2PFilterState.None;
                break;
            case P2PFilterState.None:
                p2pFilterState = P2PFilterState.FollowingOnly;
                break;
        }

        UpdateP2PToggleUI();
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

    private void OnAlcoholToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        filterAlcohol = isOn;
        SaveFilterSettings();
        ApplyAllFilters();
    }

    private void OnTrainToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        filterTrain = isOn;
        SaveFilterSettings();
        ApplyAllFilters();
    }

    private void OnTerminalToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        filterTerminal = isOn;
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

    private void ApplyAllFilters()
    {
        Dictionary<string, bool> filters = GetActiveFilters();

        if (placeListManager != null) placeListManager.ApplyFilters(filters);
        if (dataManager != null) dataManager.ApplyFilters(filters);
        if (tourAPIManager != null) tourAPIManager.ApplyFilters(filters);
        if (terminalManager != null) terminalManager.ApplyFilters(filters);
        if (trainStationManager != null) trainStationManager.ApplyFilters(filters);
        if (subwayManager != null) subwayManager.ApplyFilters(filters);

        // P2P 필터는 P2PUserFilterPanel이 전담 처리
    }

    public Dictionary<string, bool> GetActiveFilters()
    {
        return new Dictionary<string, bool>
        {
            { "petFriendlyOnly", petFriendlyState == PetFriendlyFilterState.OnlyPetFriendly },
            { "petFriendlyAll", petFriendlyState == PetFriendlyFilterState.All },
            { "noPetFriendly", petFriendlyState == PetFriendlyFilterState.NoPetFriendly },
            { "publicData", filterPublicData },
            { "subway", filterSubway },
            { "alcohol", filterAlcohol },
            { "train", filterTrain },
            { "terminal", filterTerminal },
            { "object3D", filterObject3D }
        };
    }
}

public class LongPressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public float longPressDuration = 0.8f;
    public System.Action onLongPress;

    private bool isPressed = false;
    private float pressedTime = 0f;
    private bool longPressTriggered = false;
    private Toggle cachedToggle;

    void Awake()
    {
        cachedToggle = GetComponent<Toggle>();
    }

    void Update()
    {
        if (isPressed && !longPressTriggered)
        {
            pressedTime += Time.deltaTime;
            if (pressedTime >= longPressDuration)
            {
                longPressTriggered = true;
                onLongPress?.Invoke();
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        pressedTime = 0f;
        longPressTriggered = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        pressedTime = 0f;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (longPressTriggered)
        {
            if (cachedToggle != null) cachedToggle.isOn = !cachedToggle.isOn;
            longPressTriggered = false;
            eventData.Use();
        }
        else
        {
            longPressTriggered = false;
        }
    }
}
