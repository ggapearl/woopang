using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class FilterManager : MonoBehaviour
{
    [Header("Filter Toggles")]
    [SerializeField] private Toggle petFriendlyToggle;
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

    [Header("Long Press Settings")]
    [SerializeField] private float longPressDuration = 0.8f;

    // 필터 상태
    public enum PetFriendlyFilterState
    {
        All = 0,                 // 모두 표시 (흰색)
        OnlyPetFriendly = 1,     // 애견동반 되는 곳만 (노란색)
        NoPetFriendly = 2        // 애견동반 안되는 곳만 (체크해제)
    }

    private PetFriendlyFilterState petFriendlyState = PetFriendlyFilterState.All;
    private bool filterPublicData = true;
    private bool filterSubway = true;
    private bool filterAlcohol = true;
    private bool filterTrain = true;
    private bool filterTerminal = true;
    private bool filterObject3D = true;

    private bool isUpdatingToggles = false;

    // PlayerPrefs 키 (V2)
    private const string PREF_PET_FRIENDLY = "Filter_PetFriendly_V3";
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
            { "petFriendly", "Pet Friendly" },
            { "petFriendlyRequired", "Pet Friendly (Required)" },
            { "petFriendlyNotAllowed", "Pet Friendly (N/A)" },
            { "publicData", "Public Data" },
            { "subway", "Metro" },
            { "alcohol", "Alcohol" },
            { "train", "Train" },
            { "terminal", "Terminal/Airport" },
            { "object3D", "3D Objects" }
        }},
        { "ko", new Dictionary<string, string> {
            { "petFriendly", "애견동반" },
            { "petFriendlyRequired", "애견동반(필수)" },
            { "petFriendlyNotAllowed", "애견동반(불가)" },
            { "publicData", "공공데이터" },
            { "subway", "지하철" },
            { "alcohol", "주류판매" },
            { "train", "기차역" },
            { "terminal", "터미널" },
            { "object3D", "3D 오브젝트" }
        }},
        { "ja", new Dictionary<string, string> {
            { "petFriendly", "ペット同伴" },
            { "petFriendlyRequired", "ペット同伴(必須)" },
            { "petFriendlyNotAllowed", "ペット同伴(不可)" },
            { "publicData", "公共データ" },
            { "subway", "地下鉄" },
            { "alcohol", "アルコール" },
            { "train", "鉄道駅" },
            { "terminal", "ターミナル" },
            { "object3D", "3Dオブジェクト" }
        }},
        { "zh", new Dictionary<string, string> {
            { "petFriendly", "宠物友好" },
            { "petFriendlyRequired", "宠物友好(必须)" },
            { "petFriendlyNotAllowed", "宠物友好(禁止)" },
            { "publicData", "公共数据" },
            { "subway", "地铁" },
            { "alcohol", "酒类销售" },
            { "train", "火车站" },
            { "terminal", "航站楼" },
            { "object3D", "3D对象" }
        }},
        { "es", new Dictionary<string, string> {
            { "petFriendly", "Admite Mascotas" },
            { "petFriendlyRequired", "Mascotas (Obligatorio)" },
            { "petFriendlyNotAllowed", "Mascotas (No)" },
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

    void Start()
    {
        LoadFilterSettings();
        UpdateLanguage();

        // PetFriendly는 별도 처리 (3-state) - IPointerClickHandler 방식으로 처리
        if (petFriendlyToggle != null)
        {
            // Toggle의 onValueChanged를 사용하여 클릭 감지
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
        if (toggle != null)
        {
            Text label = toggle.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
        }
    }

    private void UpdatePetFriendlyToggleUI()
    {
        if (petFriendlyToggle == null) return;

        isUpdatingToggles = true;

        // 노란색 파싱
        Color yellowColor;
        if (!ColorUtility.TryParseHtmlString("#fbc15d", out yellowColor))
        {
            yellowColor = Color.yellow; // fallback
        }

        // 다국어 텍스트 가져오기
        var texts = localizedFilterNames.ContainsKey(currentLangCode)
            ? localizedFilterNames[currentLangCode]
            : localizedFilterNames["en"];

        // 상태에 따라 UI 업데이트
        switch (petFriendlyState)
        {
            case PetFriendlyFilterState.All:
                petFriendlyToggle.isOn = true;
                // 체크박스 흰색 배경, 글자만 노란색 - 모두 표시
                SetToggleBackground(petFriendlyToggle, Color.white);
                // 레이블 텍스트: "애견동반" (기본)
                SetToggleLabel(petFriendlyToggle, texts["petFriendly"]);
                break;
            case PetFriendlyFilterState.OnlyPetFriendly:
                petFriendlyToggle.isOn = true;
                // 체크박스 노란색(#fbc15d) 배경, 노란색 글자 - 애견동반만
                SetToggleBackground(petFriendlyToggle, yellowColor);
                // 레이블 텍스트: "애견동반(필수)"
                SetToggleLabel(petFriendlyToggle, texts["petFriendlyRequired"]);
                break;
            case PetFriendlyFilterState.NoPetFriendly:
                petFriendlyToggle.isOn = false;
                // 체크박스 회색 배경, 회색 글자 - 애견동반 아닌곳만
                SetToggleBackground(petFriendlyToggle, Color.gray);
                // 레이블 텍스트: "애견동반(불가)"
                SetToggleLabel(petFriendlyToggle, texts["petFriendlyNotAllowed"]);
                break;
        }

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
        petFriendlyState = (PetFriendlyFilterState)PlayerPrefs.GetInt(PREF_PET_FRIENDLY, 0); // 기본값 All (0)
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

        // 3단계 순환: All(체크, 흰색) -> OnlyPetFriendly(체크, 노란색) -> NoPetFriendly(체크해제)
        switch (petFriendlyState)
        {
            case PetFriendlyFilterState.All:
                petFriendlyState = PetFriendlyFilterState.OnlyPetFriendly;
                break;
            case PetFriendlyFilterState.OnlyPetFriendly:
                petFriendlyState = PetFriendlyFilterState.NoPetFriendly;
                break;
            case PetFriendlyFilterState.NoPetFriendly:
                petFriendlyState = PetFriendlyFilterState.All;
                break;
        }

        UpdatePetFriendlyToggleUI();
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