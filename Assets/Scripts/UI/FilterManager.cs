using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;

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
    [SerializeField] private Toggle categoryToggle;

    [Header("카테고리 색상 설정")]
    [SerializeField] private Color categoryColorShop = new Color(0.25f, 0.5f, 0.95f, 1f); // 파란색
    [SerializeField] private Color categoryColorFood = new Color(0.984f, 0.757f, 0.365f, 1f);
    [SerializeField] private Color categoryColorCafe = new Color(0.91f, 0.33f, 0.63f, 1f);
    [SerializeField] private Color categoryColorPark = new Color(0.3f, 0.85f, 0.5f, 1f);
    [SerializeField] private Color categoryColorToilet = new Color(0.68f, 0.33f, 0.77f, 1f); // 보라색 (공중화장실)
    [SerializeField] private Color categoryColorSport = new Color(0.2f, 0.75f, 0.75f, 1f); // 청록색 (스포츠)
    [SerializeField] private Color categoryColorLandmark = new Color(0.85f, 0.65f, 0.13f, 1f); // 금색 (랜드마크)

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

    // 카테고리 필터 상태
    public enum CategoryFilterState
    {
        All = 0,    // 전체 (흰색)
        Shop = 1,
        Food = 2,
        Cafe = 3,
        Park = 4,
        Toilet = 5,  // 공중화장실 (보라색)
        Sport = 6,   // 스포츠 (청록색)
        Landmark = 7 // 랜드마크 (금색)
    }

    private static readonly string[] categoryStateValues = { "", "shop", "food", "cafe", "park", "toilet", "sport", "landmark" };

    // 공공데이터 카테고리 (publicData 토글로 필터링)
    public static readonly HashSet<string> PublicDataCategories = new HashSet<string>
    {
        "gov", "edu", "utility", "landmark", "medical",
        "culture", "sport", "religious", "welfare", "park"
    };

    private PetFriendlyFilterState petFriendlyState = PetFriendlyFilterState.All;  // 기본값: 포함
    private P2PFilterState p2pFilterState = P2PFilterState.FollowingOnly;  // 기본값: 팔로잉
    private CategoryFilterState categoryState = CategoryFilterState.All;  // 기본값: 전체
    private bool filterPublicData = true;
    private bool filterSubway = true;
    private bool filterAlcohol = true;
    private bool filterTrain = true;
    private bool filterTerminal = true;
    private bool filterObject3D = true;

    private bool isUpdatingToggles = false;

    // 카테고리 선택 시 비활성화할 토글들의 원래 상태 저장
    private bool savedPublicData;
    private bool savedSubway;
    private bool savedTrain;
    private bool savedTerminal;
    private bool savedAlcohol;
    private P2PFilterState savedP2PState;
    private bool isCategoryOverrideActive = false;
    private static readonly Color DISABLED_GRAY = new Color(0.5f, 0.5f, 0.5f, 1f);

    // PlayerPrefs 키 (V2)
    private const string PREF_PET_FRIENDLY = "Filter_PetFriendly_V3";
    private const string PREF_P2P = "Filter_P2P_V1";
    private const string PREF_PUBLIC_DATA = "Filter_PublicData_V2";
    private const string PREF_SUBWAY = "Filter_Subway_V2";
    private const string PREF_ALCOHOL = "Filter_Alcohol_V2";
    private const string PREF_TRAIN = "Filter_Train_V2";
    private const string PREF_TERMINAL = "Filter_Terminal_V2";
    private const string PREF_OBJECT3D = "Filter_Object3D_V2";
    private const string PREF_CATEGORY = "Filter_Category_V1";

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
            { "object3D", "3D Objects" },
            { "categoryAll", "Category (All)" },
            { "categoryShop", "Shop" },
            { "categoryFood", "Food" },
            { "categoryCafe", "Cafe" },
            { "categoryPark", "Park" },
            { "categoryToilet", "Public Restroom" },
            { "categorySport", "Sports" },
            { "categoryLandmark", "Landmark" }
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
            { "object3D", "3D 오브젝트" },
            { "categoryAll", "카테고리(전체)" },
            { "categoryShop", "샵" },
            { "categoryFood", "음식점" },
            { "categoryCafe", "카페" },
            { "categoryPark", "공원" },
            { "categoryToilet", "공중화장실" },
            { "categorySport", "스포츠" },
            { "categoryLandmark", "랜드마크" }
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
            { "object3D", "3Dオブジェクト" },
            { "categoryAll", "カテゴリ(全体)" },
            { "categoryShop", "ショップ" },
            { "categoryFood", "グルメ" },
            { "categoryCafe", "カフェ" },
            { "categoryPark", "公園" },
            { "categoryToilet", "公衆トイレ" },
            { "categorySport", "スポーツ" },
            { "categoryLandmark", "ランドマーク" }
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
            { "object3D", "3D对象" },
            { "categoryAll", "分类(全部)" },
            { "categoryShop", "商店" },
            { "categoryFood", "美食" },
            { "categoryCafe", "咖啡厅" },
            { "categoryPark", "公园" },
            { "categoryToilet", "公共卫生间" },
            { "categorySport", "运动" },
            { "categoryLandmark", "地标" }
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
            { "object3D", "Objetos 3D" },
            { "categoryAll", "Categoría (Todo)" },
            { "categoryShop", "Tienda" },
            { "categoryFood", "Comida" },
            { "categoryCafe", "Café" },
            { "categoryPark", "Parque" },
            { "categoryToilet", "Baño Público" },
            { "categorySport", "Deportes" },
            { "categoryLandmark", "Punto de Interés" }
        }}
    };

    // 현재 언어 코드 저장
    private string currentLangCode = "en";

    // P2P 토글은 P2PUserFilterPanel이 전담 처리 (충돌 방지)
    // FilterManager에서는 P2P 토글을 연결하지 않음

    private void AutoConnectFields()
    {
        if (categoryToggle == null)
        {
            Transform ct = transform.Find("CategoryToggle");
            if (ct != null) categoryToggle = ct.GetComponent<Toggle>();
        }
    }

    void Start()
    {
        AutoConnectFields();

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

        // Category는 PetFriendly처럼 multi-state이므로 별도 처리
        if (categoryToggle != null)
        {
            categoryToggle.onValueChanged.AddListener(OnCategoryToggleChanged);
            UpdateCategoryToggleUI();
        }

        // 저장된 카테고리 상태가 All이 아니면 오버라이드 적용
        if (categoryState != CategoryFilterState.All)
        {
            ApplyCategoryOverride();
        }

        ApplyAllFilters();

        // Start 시점에서 이미 프로바이더가 등록되어 있으면 바로 시작
        EnsureAllocationLoopStarted();

        // 역방향 안전장치: 매니저들이 먼저 Start되어 등록 실패한 경우 여기서 재탐색
        StartCoroutine(DelayedProviderScan());
    }

    /// <summary>
    /// Start 직후 1초 뒤 프로바이더 탐색 (매니저보다 늦게 Start되는 경우 대비)
    /// </summary>
    private IEnumerator DelayedProviderScan()
    {
        yield return new WaitForSeconds(1f);

        // 아직 등록되지 않은 프로바이더 탐색
        IPlaceCacheProvider[] allProviders = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            as IPlaceCacheProvider[];

        // FindObjectsByType는 인터페이스로 직접 검색 불가 → 개별 매니저 탐색
        if (dataManager != null && dataManager is IPlaceCacheProvider dmProvider && !cacheProviders.Contains(dmProvider))
        {
            RegisterCacheProvider(dmProvider);
            Debug.LogWarning("[dbg] FilterManager: DataManager 지연 등록");
        }
        if (tourAPIManager != null && tourAPIManager is IPlaceCacheProvider tourProvider && !cacheProviders.Contains(tourProvider))
        {
            RegisterCacheProvider(tourProvider);
            Debug.LogWarning("[dbg] FilterManager: TourAPIManager 지연 등록");
        }
        if (subwayManager != null && subwayManager is IPlaceCacheProvider subwayProvider && !cacheProviders.Contains(subwayProvider))
        {
            RegisterCacheProvider(subwayProvider);
            Debug.LogWarning("[dbg] FilterManager: SubwayManager 지연 등록");
        }
        if (trainStationManager != null && trainStationManager is IPlaceCacheProvider trainProvider && !cacheProviders.Contains(trainProvider))
        {
            RegisterCacheProvider(trainProvider);
            Debug.LogWarning("[dbg] FilterManager: TrainStationManager 지연 등록");
        }
        if (terminalManager != null && terminalManager is IPlaceCacheProvider terminalProvider && !cacheProviders.Contains(terminalProvider))
        {
            RegisterCacheProvider(terminalProvider);
            Debug.LogWarning("[dbg] FilterManager: TerminalManager 지연 등록");
        }

        Debug.LogWarning($"[dbg] FilterManager.DelayedProviderScan 완료: 총 {cacheProviders.Count}개 프로바이더");

        // 프로바이더가 있으면 AllocationLoop 시작 보장
        EnsureAllocationLoopStarted();
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
        categoryState = (filterName == "category") ? CategoryFilterState.All : CategoryFilterState.All;

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
        categoryState = (CategoryFilterState)PlayerPrefs.GetInt(PREF_CATEGORY, 0);
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
        PlayerPrefs.SetInt(PREF_CATEGORY, (int)categoryState);
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
        UpdateCategoryToggleUI();
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

    // ============================================================
    // 카테고리 토글 — 누를 때마다 순환 (All → Shop → Food → Cafe → Park → Toilet → All)
    // ============================================================

    private void OnCategoryToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        // 6단계 순환: All → Shop → Food → Cafe → Park → Toilet → All
        int next = ((int)categoryState + 1) % categoryStateValues.Length;
        categoryState = (CategoryFilterState)next;

        // 카테고리 선택 시 공공데이터/P2P 토글 비활성화
        ApplyCategoryOverride();

        UpdateCategoryToggleUI();
        SaveFilterSettings();
        ApplyAllFilters();
    }

    /// <summary>
    /// 카테고리가 선택되면 공공데이터/P2P/주류 토글을 회색으로 비활성화
    /// 카테고리=All로 돌아오면 원래 상태 복원
    /// </summary>
    private void ApplyCategoryOverride()
    {
        bool categoryActive = categoryState != CategoryFilterState.All;

        if (categoryActive && !isCategoryOverrideActive)
        {
            // 카테고리 선택됨 → 현재 상태 저장 후 비활성화
            isCategoryOverrideActive = true;
            savedPublicData = filterPublicData;
            savedSubway = filterSubway;
            savedTrain = filterTrain;
            savedTerminal = filterTerminal;
            savedAlcohol = filterAlcohol;
            savedP2PState = p2pFilterState;
        }
        else if (!categoryActive && isCategoryOverrideActive)
        {
            // 카테고리=All로 복귀 → 원래 상태 복원
            isCategoryOverrideActive = false;
            filterPublicData = savedPublicData;
            filterSubway = savedSubway;
            filterTrain = savedTrain;
            filterTerminal = savedTerminal;
            filterAlcohol = savedAlcohol;
            p2pFilterState = savedP2PState;
        }

        isUpdatingToggles = true;

        if (categoryActive)
        {
            // 체크된 채로 회색 박스 표시 (비활성화 상태)
            SetToggleDisabledVisual(publicDataToggle);
            SetToggleDisabledVisual(subwayToggle);
            SetToggleDisabledVisual(trainToggle);
            SetToggleDisabledVisual(terminalToggle);
            SetToggleDisabledVisual(alcoholToggle);

            // 토글 터치 불가
            SetToggleInteractable(publicDataToggle, false);
            SetToggleInteractable(subwayToggle, false);
            SetToggleInteractable(trainToggle, false);
            SetToggleInteractable(terminalToggle, false);
            SetToggleInteractable(alcoholToggle, false);

            // P2P도 비활성화
            var p2pPanel = Object.FindFirstObjectByType<P2PUserFilterPanel>();
            if (p2pPanel != null)
            {
                p2pPanel.SetFilterModeExternal(UserFilterMode.None);
                SetToggleInteractable(p2pPanel.GetToggle(), false);
            }
        }
        else
        {
            // 원래 상태로 복원 (SetIsOnWithoutNotify로 이벤트 발생 방지)
            if (publicDataToggle != null) publicDataToggle.SetIsOnWithoutNotify(filterPublicData);
            if (subwayToggle != null) subwayToggle.SetIsOnWithoutNotify(filterSubway);
            if (trainToggle != null) trainToggle.SetIsOnWithoutNotify(filterTrain);
            if (terminalToggle != null) terminalToggle.SetIsOnWithoutNotify(filterTerminal);
            if (alcoholToggle != null) alcoholToggle.SetIsOnWithoutNotify(filterAlcohol);

            // 토글 터치 가능으로 복원
            SetToggleInteractable(publicDataToggle, true);
            SetToggleInteractable(subwayToggle, true);
            SetToggleInteractable(trainToggle, true);
            SetToggleInteractable(terminalToggle, true);
            SetToggleInteractable(alcoholToggle, true);

            // P2P 복원
            var p2pPanel = Object.FindFirstObjectByType<P2PUserFilterPanel>();
            if (p2pPanel != null)
            {
                p2pPanel.SetFilterModeExternal(savedP2PState == P2PFilterState.None ? UserFilterMode.None
                    : savedP2PState == P2PFilterState.All ? UserFilterMode.All
                    : UserFilterMode.FollowingOnly);
                SetToggleInteractable(p2pPanel.GetToggle(), true);
            }

            // 비활성화 비주얼 해제 → 원래 on/off 색상 복원
            RestoreToggleVisual(publicDataToggle, filterPublicData);
            RestoreToggleVisual(subwayToggle, filterSubway);
            RestoreToggleVisual(trainToggle, filterTrain);
            RestoreToggleVisual(terminalToggle, filterTerminal);
            RestoreToggleVisual(alcoholToggle, filterAlcohol);

            // Unity Toggle 내부 PlayEffect가 비동기로 색상을 덮어쓸 수 있으므로
            // 1프레임 후 색상 강제 재적용
            StartCoroutine(DelayedRestoreToggleVisuals());
        }

        isUpdatingToggles = false;
    }

    /// <summary>
    /// Unity Toggle 내부 PlayEffect가 비동기로 graphic 색상을 덮어쓰는 문제 방지
    /// 1프레임 뒤에 색상 강제 재적용
    /// </summary>
    private IEnumerator DelayedRestoreToggleVisuals()
    {
        yield return null;
        RestoreToggleVisual(publicDataToggle, filterPublicData);
        RestoreToggleVisual(subwayToggle, filterSubway);
        RestoreToggleVisual(trainToggle, filterTrain);
        RestoreToggleVisual(terminalToggle, filterTerminal);
        RestoreToggleVisual(alcoholToggle, filterAlcohol);
    }

    /// <summary>
    /// 토글을 체크된 상태로 유지하면서 박스만 회색으로 표시
    /// </summary>
    private void SetToggleDisabledVisual(Toggle toggle)
    {
        if (toggle == null) return;
        toggle.SetIsOnWithoutNotify(true);
        SetToggleBackgroundAndCheckmark(toggle, DISABLED_GRAY);
        SetToggleLabelColor(toggle, DISABLED_GRAY);
    }

    /// <summary>
    /// 토글 원래 색상 복원
    /// </summary>
    private void RestoreToggleVisual(Toggle toggle, bool isOn)
    {
        if (toggle == null) return;
        toggle.SetIsOnWithoutNotify(isOn);
        Color color = isOn ? Color.white : DISABLED_GRAY;
        SetToggleBackgroundAndCheckmark(toggle, color);
        SetToggleLabelColor(toggle, Color.white);
    }

    private void SetToggleInteractable(Toggle toggle, bool interactable)
    {
        if (toggle == null) return;
        toggle.interactable = interactable;
    }

    private void UpdateCategoryToggleUI()
    {
        if (categoryToggle == null) return;

        isUpdatingToggles = true;

        // isOn은 항상 true로 유지 (Unity Toggle 색상 처리 방지)
        categoryToggle.isOn = true;

        var texts = localizedFilterNames.ContainsKey(currentLangCode)
            ? localizedFilterNames[currentLangCode]
            : localizedFilterNames["en"];

        string labelText = "";
        Color bgColor = Color.white;

        switch (categoryState)
        {
            case CategoryFilterState.All:
                bgColor = Color.white;
                labelText = texts["categoryAll"];
                break;
            case CategoryFilterState.Shop:
                bgColor = categoryColorShop;
                labelText = texts["categoryShop"];
                break;
            case CategoryFilterState.Food:
                bgColor = categoryColorFood;
                labelText = texts["categoryFood"];
                break;
            case CategoryFilterState.Cafe:
                bgColor = categoryColorCafe;
                labelText = texts["categoryCafe"];
                break;
            case CategoryFilterState.Park:
                bgColor = categoryColorPark;
                labelText = texts["categoryPark"];
                break;
            case CategoryFilterState.Toilet:
                bgColor = categoryColorToilet;
                labelText = texts["categoryToilet"];
                break;
            case CategoryFilterState.Sport:
                bgColor = categoryColorSport;
                labelText = texts["categorySport"];
                break;
            case CategoryFilterState.Landmark:
                bgColor = categoryColorLandmark;
                labelText = texts["categoryLandmark"];
                break;
        }

        SetToggleBackgroundAndCheckmark(categoryToggle, bgColor);
        SetToggleLabel(categoryToggle, labelText);
        SetToggleLabelColor(categoryToggle, categoryState == CategoryFilterState.All ? Color.white : bgColor);

        isUpdatingToggles = false;
    }

    /// <summary>
    /// 현재 선택된 카테고리 필터 값 반환 (빈 문자열 = 전체)
    /// </summary>
    public string GetActiveCategoryFilter()
    {
        return categoryStateValues[(int)categoryState];
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

        // 중앙 배분 즉시 재실행 (필터 변경 반영)
        TriggerReallocation();
    }

    public Dictionary<string, bool> GetActiveFilters()
    {
        bool categoryActive = categoryState != CategoryFilterState.All;

        return new Dictionary<string, bool>
        {
            { "petFriendlyOnly", petFriendlyState == PetFriendlyFilterState.OnlyPetFriendly },
            { "petFriendlyAll", petFriendlyState == PetFriendlyFilterState.All },
            { "noPetFriendly", petFriendlyState == PetFriendlyFilterState.NoPetFriendly },
            { "publicData", categoryActive ? false : filterPublicData },
            { "subway", categoryActive ? false : filterSubway },
            { "alcohol", categoryActive ? false : filterAlcohol },
            { "train", categoryActive ? false : filterTrain },
            { "terminal", categoryActive ? false : filterTerminal },
            { "object3D", filterObject3D },
            { "categoryFilter", categoryActive },
            { "p2pUsers", categoryActive ? false : true }
        };
    }

    // ============================================================
    // 중앙 배분 시스템 — Full 20개 + IndicatorOnly 20개 + OffScreen 16개
    // ============================================================

    [Header("Central Allocation")]
    [Tooltip("Full 3D 오브젝트 최대 수")]
    [SerializeField] private int maxFullObjects = 20;
    [Tooltip("IndicatorOnly 경량 오브젝트 최대 수")]
    [SerializeField] private int maxIndicatorObjects = 20;
    [Tooltip("Full 오브젝트 생성 반경 (m) — 이 안에서만 Full 스폰")]
    [SerializeField] private float fullObjectRadius = 500f;
    [Tooltip("배분 갱신 주기 (초)")]
    [SerializeField] private float allocationInterval = 2f;
    [Tooltip("캐시 갱신 거리 (m) — 이만큼 이동 시 전체 캐시 새로고침")]
    [SerializeField] private float cacheRefreshDistance = 5000f;

    [Header("Speed Tracking — LoadingManager fallback 판정용")]
    [Tooltip("속도 계산 이동 평균 윈도우 (초)")]
    [SerializeField] private float speedAverageWindow = 5f;

    // GPS 샘플 큐: (time, lat, lon)
    private Queue<(float time, float lat, float lon)> gpsSamples = new Queue<(float, float, float)>();
    // 외부 조회용 최근 계산 속도 (km/h). -1 = 샘플 부족
    private float cachedSpeedKmh = -1f;
    public float CurrentSpeedKmh => cachedSpeedKmh;

    private List<IPlaceCacheProvider> cacheProviders = new List<IPlaceCacheProvider>();
    private HashSet<string> currentFullAllocations = new HashSet<string>();
    private HashSet<string> currentIndicatorAllocations = new HashSet<string>();
    // Detail API 응답 대기 중인 ID + 큐잉 시각 (타임아웃 판정용)
    private Dictionary<string, float> detailPendingIds = new Dictionary<string, float>();
    private const float DETAIL_PENDING_TIMEOUT = 15f; // Detail API 대기 타임아웃 (초)
    private Vector2 lastAllocationPosition;
    private Vector2 lastCacheRefreshPosition;
    private bool allocationStarted = false;
    private Coroutine allocationCoroutine;

    /// <summary>
    /// IPlaceCacheProvider를 등록 (각 매니저가 Start에서 호출)
    /// 코루틴은 여기서 시작하지 않음 — FilterManager 자체 OnEnable에서 시작
    /// </summary>
    public void RegisterCacheProvider(IPlaceCacheProvider provider)
    {
        if (provider != null && !cacheProviders.Contains(provider))
        {
            cacheProviders.Add(provider);
            Debug.LogWarning($"[dbg] RegisterCacheProvider: {provider.FilterKey}, total={cacheProviders.Count}, active={gameObject.activeInHierarchy}");

            // 활성 상태이면 즉시 시작 시도 (비활성이면 OnEnable에서 나중에 시작)
            EnsureAllocationLoopStarted();
        }
    }

    private void OnEnable()
    {
        // 비활성→활성 전환 시 AllocationLoop 시작 (코루틴은 활성 오브젝트에서만 실행 가능)
        if (!allocationStarted && cacheProviders.Count > 0)
        {
            allocationStarted = true;
            allocationCoroutine = StartCoroutine(AllocationLoop());
            Debug.LogWarning($"[dbg] AllocationLoop OnEnable에서 시작 (providers={cacheProviders.Count})");
        }
    }

    /// <summary>
    /// AllocationLoop를 확실히 시작 — Start/DelayedProviderScan 등에서 호출
    /// </summary>
    private void EnsureAllocationLoopStarted()
    {
        if (!allocationStarted && gameObject.activeInHierarchy && cacheProviders.Count > 0)
        {
            allocationStarted = true;
            allocationCoroutine = StartCoroutine(AllocationLoop());
            Debug.LogWarning($"[dbg] AllocationLoop EnsureStart에서 시작 (providers={cacheProviders.Count})");
        }
    }

    /// <summary>
    /// 배분 주기 코루틴 — allocationInterval마다 재배분
    /// </summary>
    private IEnumerator AllocationLoop()
    {
        // 최초 캐시 준비 대기 (최소 1개 프로바이더 준비)
        yield return new WaitForSeconds(2f);

        while (true)
        {
            Vector2 gps = GetCurrentGPS();

            // 프로바이더 준비 상태 집계 (FileLogger 기록용)
            int readyCount = 0;
            foreach (var p in cacheProviders) if (p.IsCacheReady) readyCount++;

            // FileLogger: GPS 위치 + 프로바이더 상태 + 속도/모드 (fallback 진단용)
            if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
                FileLogger.Instance.LogGPS(gps.x, gps.y,
                    $"providers={cacheProviders.Count}, ready={readyCount}, speed={cachedSpeedKmh:F1}km/h");

            if (gps.x != 0f || gps.y != 0f)
            {
                // GPS 샘플 기반 평균 속도 계산 (LoadingManager fallback 판정용)
                UpdateSpeed(gps);

                // 5km 이상 이동 시 캐시 갱신
                float movedFromCache = CalculateGPSDistance(lastCacheRefreshPosition.x, lastCacheRefreshPosition.y, gps.x, gps.y);
                if (lastCacheRefreshPosition == Vector2.zero || movedFromCache >= cacheRefreshDistance)
                {
                    Debug.LogWarning($"[dbg][FilterManager][CACHE] 5km 이동 감지 → 캐시 갱신 트리거 moved={movedFromCache:F0}m threshold={cacheRefreshDistance}m");
                    RefreshAllCaches(gps.x, gps.y);
                    lastCacheRefreshPosition = gps;

                    if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
                        FileLogger.Instance.Log("CACHE", $"캐시 갱신 트리거: moved={movedFromCache:F0}m, threshold={cacheRefreshDistance}m at ({gps.x:F6},{gps.y:F6})");
                }

                AllocateObjects(gps.x, gps.y);
                lastAllocationPosition = gps;

                // 앵커 생성 실패한 오브젝트 선별 복구 — AR Limited 구간에서 실패한 항목 자동 재시도
                foreach (var provider in cacheProviders)
                {
                    if (provider == null) continue;
                    provider.RetryFailedAnchors();
                }
            }

            yield return new WaitForSeconds(allocationInterval);
        }
    }

    /// <summary>
    /// 필터 변경 시 즉시 재배분 트리거
    /// </summary>
    public void TriggerReallocation()
    {
        if (cacheProviders.Count == 0) return;
        Vector2 gps = GetCurrentGPS();
        if (gps.x != 0f || gps.y != 0f)
        {
            AllocateObjects(gps.x, gps.y);
        }
    }

    /// <summary>
    /// GPS 샘플 기반 평균 속도 계산
    /// LoadingManager가 fallback 진입 시 차량 속도 임계값 판정에 사용 (CurrentSpeedKmh 프로퍼티)
    /// </summary>
    private void UpdateSpeed(Vector2 gps)
    {
        float now = Time.realtimeSinceStartup;

        gpsSamples.Enqueue((now, gps.x, gps.y));
        while (gpsSamples.Count > 0 && now - gpsSamples.Peek().time > speedAverageWindow)
            gpsSamples.Dequeue();

        float speedKmh = -1f;
        if (gpsSamples.Count >= 2)
        {
            var first = gpsSamples.Peek();
            float elapsed = now - first.time;
            if (elapsed >= 1f)
            {
                float meters = CalculateGPSDistance(first.lat, first.lon, gps.x, gps.y);
                speedKmh = (meters / elapsed) * 3.6f;
            }
        }
        cachedSpeedKmh = speedKmh;
    }

    /// <summary>
    /// 핵심 배분 로직
    /// 1) 모든 매니저 캐시에서 데이터 수집
    /// 2) Full 후보: 필터 통과 + fullObjectRadius 이내 + 거리순 → 최대 maxFullObjects
    /// 3) IndicatorOnly 후보: 필터 무시 + Full 아닌 것 + 거리순 → 최대 maxIndicatorObjects
    /// 4) 이전 배분과 비교하여 스폰/디스폰
    /// </summary>
    private void AllocateObjects(float lat, float lon)
    {
        // 1) 전체 캐시 수집 + 거리 계산
        List<(CachedPlaceData data, IPlaceCacheProvider provider, float distance)> allPlaces
            = new List<(CachedPlaceData, IPlaceCacheProvider, float)>();

        foreach (var provider in cacheProviders)
        {
            if (!provider.IsCacheReady) continue;

            foreach (var place in provider.GetCachedPlaces())
            {
                float dist = CalculateGPSDistance(lat, lon, place.latitude, place.longitude);
                allPlaces.Add((place, provider, dist));
            }
        }

        // 캐시가 모두 준비 안 된 상태라면 조용히 리턴 (이른 호출 방어)
        if (allPlaces.Count == 0) return;

        // 거리순 정렬
        allPlaces.Sort((a, b) => a.distance.CompareTo(b.distance));

        Dictionary<string, bool> filters = GetActiveFilters();

        // 2) Full 후보: 필터 통과 + fullObjectRadius 이내
        HashSet<string> newFullSet = new HashSet<string>();
        Dictionary<string, IPlaceCacheProvider> fullProviderMap = new Dictionary<string, IPlaceCacheProvider>();

        foreach (var item in allPlaces)
        {
            if (newFullSet.Count >= maxFullObjects) break;
            if (item.distance > fullObjectRadius) continue;
            if (!IsPassingFilter(item.data, filters)) continue;

            newFullSet.Add(item.data.uniqueId);
            fullProviderMap[item.data.uniqueId] = item.provider;
        }

        // 3) IndicatorOnly 후보: 매니저 토글만 적용 (세부 필터는 무시), Full 제외
        HashSet<string> newIndicatorSet = new HashSet<string>();
        Dictionary<string, IPlaceCacheProvider> indicatorProviderMap = new Dictionary<string, IPlaceCacheProvider>();

        foreach (var item in allPlaces)
        {
            if (newIndicatorSet.Count >= maxIndicatorObjects) break;
            if (newFullSet.Contains(item.data.uniqueId)) continue;

            // 매니저별 토글 체크 (publicData, subway, train, terminal OFF 시 제외)
            if (!IsPassingManagerToggle(item.data, filters)) continue;

            newIndicatorSet.Add(item.data.uniqueId);
            indicatorProviderMap[item.data.uniqueId] = item.provider;
        }

        // 4) Diff: 디스폰 → 스폰 순서로 처리

        // Full 디스폰 (이전에 있었지만 새 배분에 없는 것)
        foreach (string id in currentFullAllocations)
        {
            if (!newFullSet.Contains(id))
            {
                detailPendingIds.Remove(id);
                foreach (var provider in cacheProviders)
                {
                    if (provider.GetSpawnedFullIds().Contains(id))
                    {
                        provider.DespawnFullObject(ExtractRawId(id));
                        break;
                    }
                    // Detail 대기 중 임시 IndicatorOnly도 함께 디스폰
                    if (provider.GetSpawnedIndicatorIds().Contains(id))
                    {
                        provider.DespawnIndicatorOnly(ExtractRawId(id));
                        break;
                    }
                }
            }
        }

        // IndicatorOnly 디스폰
        foreach (string id in currentIndicatorAllocations)
        {
            if (!newIndicatorSet.Contains(id))
            {
                foreach (var provider in cacheProviders)
                {
                    if (provider.GetSpawnedIndicatorIds().Contains(id))
                    {
                        provider.DespawnIndicatorOnly(ExtractRawId(id));
                        break;
                    }
                }
            }
        }

        // Full 스폰 (새로 추가된 것)
        // Detail API 대기 중인 Full은 임시로 IndicatorOnly 표시
        float nowTime = Time.realtimeSinceStartup;

        // pending 정리:
        //   1) Full 스폰 성공 (Detail API 응답 도착 → Full 오브젝트 생성됨)
        //   2) 타임아웃 경과 (API 응답 실패/지연 — 다시 시도 가능하게 해제)
        List<string> pendingToRemove = new List<string>();
        foreach (var kv in detailPendingIds)
        {
            bool fullSpawned = fullProviderMap.TryGetValue(kv.Key, out var pp) && pp.GetSpawnedFullIds().Contains(kv.Key);
            bool timedOut = (nowTime - kv.Value) >= DETAIL_PENDING_TIMEOUT;
            if (fullSpawned || timedOut)
                pendingToRemove.Add(kv.Key);
        }
        foreach (string rid in pendingToRemove) detailPendingIds.Remove(rid);

        HashSet<string> pendingFullIds = new HashSet<string>();
        foreach (string id in newFullSet)
        {
            if (!currentFullAllocations.Contains(id))
            {
                if (fullProviderMap.TryGetValue(id, out var provider))
                {
                    // IndicatorOnly에서 승격 또는 임시 IndicatorOnly가 남아있는 경우 먼저 디스폰
                    if (currentIndicatorAllocations.Contains(id) || provider.GetSpawnedIndicatorIds().Contains(id))
                    {
                        provider.DespawnIndicatorOnly(ExtractRawId(id));
                    }
                    bool spawned = provider.SpawnFullObject(ExtractRawId(id));
                    if (!spawned)
                    {
                        // Detail API 대기 중 → 임시 IndicatorOnly 표시 (최초 1회만)
                        pendingFullIds.Add(id);
                        detailPendingIds[id] = nowTime;
                        provider.SpawnIndicatorOnly(ExtractRawId(id));
                    }
                }
            }
            else
            {
                if (fullProviderMap.TryGetValue(id, out var provider))
                {
                    bool hasFullSpawn = provider.GetSpawnedFullIds().Contains(id);
                    bool hasIndicator = provider.GetSpawnedIndicatorIds().Contains(id);

                    if (hasFullSpawn && hasIndicator)
                    {
                        // Full 스폰 완료 후 임시 IndicatorOnly가 남아있으면 제거
                        provider.DespawnIndicatorOnly(ExtractRawId(id));
                        detailPendingIds.Remove(id);
                    }
                    else if (!hasFullSpawn && !hasIndicator && !detailPendingIds.ContainsKey(id))
                    {
                        // Detail API 대기 중이 아닌데 Full도 Indicator도 없으면 복구 시도
                        bool spawned = provider.SpawnFullObject(ExtractRawId(id));
                        if (!spawned)
                        {
                            pendingFullIds.Add(id);
                            detailPendingIds[id] = nowTime;
                            provider.SpawnIndicatorOnly(ExtractRawId(id));
                        }
                    }
                    // detailPendingIds에 있으면 → Detail API 응답 대기 중이므로 아무것도 안 함
                }
            }
        }

        // IndicatorOnly 스폰 (새로 추가된 것)
        foreach (string id in newIndicatorSet)
        {
            if (!currentIndicatorAllocations.Contains(id))
            {
                if (indicatorProviderMap.TryGetValue(id, out var provider))
                {
                    provider.SpawnIndicatorOnly(ExtractRawId(id));
                }
            }
        }

        // [DEBUG] 배분 결과 로깅 — 총 캐시 수, Full/Indicator 배분 크기 + Diff
        int totalPlaces = 0;
        foreach (var provider in cacheProviders)
            if (provider.IsCacheReady) totalPlaces += provider.GetCachedPlaces().Count;

        int fullAdded = 0, fullRemoved = 0, indicatorAdded = 0, indicatorRemoved = 0;
        foreach (string id in newFullSet) if (!currentFullAllocations.Contains(id)) fullAdded++;
        foreach (string id in currentFullAllocations) if (!newFullSet.Contains(id)) fullRemoved++;
        foreach (string id in newIndicatorSet) if (!currentIndicatorAllocations.Contains(id)) indicatorAdded++;
        foreach (string id in currentIndicatorAllocations) if (!newIndicatorSet.Contains(id)) indicatorRemoved++;

        Debug.LogWarning($"[dbg][FilterManager][ALLOC] cache={totalPlaces} Full={newFullSet.Count}(+{fullAdded}/-{fullRemoved}) Indicator={newIndicatorSet.Count}(+{indicatorAdded}/-{indicatorRemoved}) radius={fullObjectRadius}m gps=({lat:F5},{lon:F5})");

        // FileLogger: 배분 결과
        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
            FileLogger.Instance.LogAllocation(totalPlaces, newFullSet.Count, newIndicatorSet.Count, fullObjectRadius);

        currentFullAllocations = newFullSet;
        currentIndicatorAllocations = newIndicatorSet;
    }

    /// <summary>
    /// CachedPlaceData가 현재 필터를 통과하는지 판단
    /// IndicatorOnly는 이 검사를 거치지 않음 (항상 표시)
    /// </summary>
    private bool IsPassingFilter(CachedPlaceData place, Dictionary<string, bool> filters)
    {
        if (filters == null) return true;

        // 매니저별 토글 체크
        string fk = place.filterKey;
        if (!string.IsNullOrEmpty(fk) && filters.ContainsKey(fk) && !filters[fk])
            return false;

        // 카테고리 필터 (shop, food, cafe, park, toilet, sport, landmark)
        bool categoryFilterActive = filters.ContainsKey("categoryFilter") && filters["categoryFilter"];
        if (categoryFilterActive)
        {
            // 카테고리 필터가 활성이면 해당 카테고리만 통과 (publicData 토글 무시)
            string activeCat = GetActiveCategoryFilter();
            if (!string.IsNullOrEmpty(activeCat) && place.category != activeCat)
                return false;
        }
        else
        {
            // 카테고리 필터 비활성 시에만 공공데이터 토글 적용
            string cat = place.category ?? "";
            if (PublicDataCategories.Contains(cat))
            {
                bool showPublicData = !filters.ContainsKey("publicData") || filters["publicData"];
                if (!showPublicData) return false;
            }
        }

        // 3D 오브젝트 필터 (model_type == "custom")
        bool showObject3D = !filters.ContainsKey("object3D") || filters["object3D"];
        if (!showObject3D && place.modelType == "custom")
            return false;

        // 애견동반 필터
        bool petFriendlyOnly = filters.ContainsKey("petFriendlyOnly") && filters["petFriendlyOnly"];
        bool noPetFriendly = filters.ContainsKey("noPetFriendly") && filters["noPetFriendly"];
        if (petFriendlyOnly && !place.petFriendly) return false;
        if (noPetFriendly && place.petFriendly) return false;

        return true;
    }

    /// <summary>
    /// IndicatorOnly용 필터 체크
    /// 매니저 토글(publicData, subway, train, terminal) + 카테고리 필터 적용
    /// 세부 필터(petFriendly, object3D)는 적용하지 않음
    /// </summary>
    private bool IsPassingManagerToggle(CachedPlaceData place, Dictionary<string, bool> filters)
    {
        if (filters == null) return true;

        // 카테고리 필터 (shop, food, cafe, park, toilet, sport, landmark) — IndicatorOnly에도 적용
        bool categoryFilterActive = filters.ContainsKey("categoryFilter") && filters["categoryFilter"];
        if (categoryFilterActive)
        {
            // 카테고리 필터가 활성이면 해당 카테고리만 통과 (매니저 토글/publicData 무시)
            string activeCat = GetActiveCategoryFilter();
            if (!string.IsNullOrEmpty(activeCat) && place.category != activeCat)
                return false;
        }
        else
        {
            // 카테고리 필터 비활성 시에만 매니저 토글 + publicData 적용
            string fk = place.filterKey;
            if (!string.IsNullOrEmpty(fk) && filters.ContainsKey(fk) && !filters[fk])
                return false;

            string cat = place.category ?? "";
            if (PublicDataCategories.Contains(cat))
            {
                bool showPublicData = !filters.ContainsKey("publicData") || filters["publicData"];
                if (!showPublicData) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 모든 프로바이더 캐시 갱신
    /// - 최초 호출(hasInitiatedCacheOnce=false): isCacheReady=false인 provider만 호출 (중복 페치 방지)
    /// - 이후 호출(5km 이동): 모든 provider 호출
    /// </summary>
    private bool hasInitiatedCacheOnce = false;
    private void RefreshAllCaches(float lat, float lon)
    {
        int skipped = 0, called = 0;
        Debug.LogWarning($"[dbg][FilterManager][CACHE] RefreshAllCaches providers={cacheProviders.Count} lat={lat:F6} lon={lon:F6} initial={!hasInitiatedCacheOnce}");
        detailPendingIds.Clear();
        foreach (var provider in cacheProviders)
        {
            // 최초 호출 시 이미 준비된 provider는 건너뜀 (자체 Start 로드와 중복 방지)
            if (!hasInitiatedCacheOnce && provider.IsCacheReady)
            {
                skipped++;
                continue;
            }
            provider.RefreshCache(lat, lon);
            called++;
        }
        Debug.LogWarning($"[dbg][FilterManager][CACHE] RefreshAllCaches 결과 called={called} skipped={skipped}");
        hasInitiatedCacheOnce = true;
    }

    /// <summary>
    /// uniqueId에서 rawId 추출 ("dm_123" → "123", "subway_강남역" → "강남역")
    /// </summary>
    private string ExtractRawId(string uniqueId)
    {
        int underscoreIndex = uniqueId.IndexOf('_');
        if (underscoreIndex >= 0 && underscoreIndex < uniqueId.Length - 1)
            return uniqueId.Substring(underscoreIndex + 1);
        return uniqueId;
    }

    private Vector2 GetCurrentGPS()
    {
#if UNITY_EDITOR
        if (VirtualLocation.Instance != null)
            return new Vector2(VirtualLocation.Instance.Latitude, VirtualLocation.Instance.Longitude);
        return Vector2.zero;
#else
        if (Input.location.status == LocationServiceStatus.Running)
            return new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        // fallback: DataManager의 마지막 위치
        if (dataManager != null)
            return dataManager.GetLastFetchPosition();
        return Vector2.zero;
#endif
    }

    /// <summary>
    /// GPS 거리 계산 (Haversine)
    /// </summary>
    private float CalculateGPSDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000f;
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);
        float a = Mathf.Sin(dLat / 2f) * Mathf.Sin(dLat / 2f)
                + Mathf.Cos(Mathf.Deg2Rad * lat1) * Mathf.Cos(Mathf.Deg2Rad * lat2)
                * Mathf.Sin(dLon / 2f) * Mathf.Sin(dLon / 2f);
        float c = 2f * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1f - a));
        return R * c;
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
