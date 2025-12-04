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
    [SerializeField] private Toggle busToggle;
    [SerializeField] private Toggle alcoholToggle;
    [SerializeField] private Toggle woopangDataToggle;
    [SerializeField] private Toggle object3DToggle;

    [Header("References")]
    [SerializeField] private PlaceListManager placeListManager;
    [SerializeField] private DataManager dataManager;
    [SerializeField] private TourAPIManager tourAPIManager;

    [Header("Long Press Settings")]
    [SerializeField] private float longPressDuration = 0.8f;

    // 필터 상태
    private bool filterPetFriendly = true;
    private bool filterPublicData = true;
    private bool filterSubway = true;
    private bool filterBus = true;
    private bool filterAlcohol = true;
    private bool filterWoopangData = true;
    private bool filterObject3D = true;

    private bool isUpdatingToggles = false;

    // PlayerPrefs 키 (V2)
    private const string PREF_PET_FRIENDLY = "Filter_PetFriendly_V2";
    private const string PREF_PUBLIC_DATA = "Filter_PublicData_V2";
    private const string PREF_SUBWAY = "Filter_Subway_V2";
    private const string PREF_BUS = "Filter_Bus_V2";
    private const string PREF_ALCOHOL = "Filter_Alcohol_V2";
    private const string PREF_WOOPANG_DATA = "Filter_WoopangData_V2";
    private const string PREF_OBJECT3D = "Filter_Object3D_V2";

    // 다국어 데이터
    private Dictionary<string, Dictionary<string, string>> localizedFilterNames = new Dictionary<string, Dictionary<string, string>>
    {
        { "en", new Dictionary<string, string> {
            { "petFriendly", "Pet Friendly" },
            { "publicData", "Public Data" },
            { "subway", "Subway" },
            { "bus", "Bus" },
            { "alcohol", "Alcohol" },
            { "woopangData", "Woopang Data" },
            { "object3D", "3D Objects" }
        }},
        { "ko", new Dictionary<string, string> {
            { "petFriendly", "애견동반" },
            { "publicData", "공공데이터" },
            { "subway", "지하철" },
            { "bus", "버스" },
            { "alcohol", "주류판매" },
            { "woopangData", "우팡데이터" },
            { "object3D", "3D 오브젝트" }
        }},
        { "ja", new Dictionary<string, string> {
            { "petFriendly", "ペット同伴" },
            { "publicData", "公共データ" },
            { "subway", "地下鉄" },
            { "bus", "バス" },
            { "alcohol", "アルコール" },
            { "woopangData", "Woopangデータ" },
            { "object3D", "3Dオブジェクト" }
        }},
        { "zh", new Dictionary<string, string> {
            { "petFriendly", "宠物友好" },
            { "publicData", "公共数据" },
            { "subway", "地铁" },
            { "bus", "公交车" },
            { "alcohol", "酒类销售" },
            { "woopangData", "Woopang数据" },
            { "object3D", "3D对象" }
        }},
        { "es", new Dictionary<string, string> {
            { "petFriendly", "Admite Mascotas" },
            { "publicData", "Datos Públicos" },
            { "subway", "Metro" },
            { "bus", "Autobús" },
            { "alcohol", "Alcohol" },
            { "woopangData", "Datos Woopang" },
            { "object3D", "Objetos 3D" }
        }}
    };

    void Start()
    {
        LoadFilterSettings();
        UpdateLanguage();

        SetupToggle(petFriendlyToggle, filterPetFriendly, OnPetFriendlyToggleChanged, "petFriendly");
        SetupToggle(publicDataToggle, filterPublicData, OnPublicDataToggleChanged, "publicData");
        SetupToggle(subwayToggle, filterSubway, OnSubwayToggleChanged, "subway");
        SetupToggle(busToggle, filterBus, OnBusToggleChanged, "bus");
        SetupToggle(alcoholToggle, filterAlcohol, OnAlcoholToggleChanged, "alcohol");
        SetupToggle(woopangDataToggle, filterWoopangData, OnWoopangDataToggleChanged, "woopangData");
        SetupToggle(object3DToggle, filterObject3D, OnObject3DToggleChanged, "object3D");

        ApplyAllFilters();
    }

    private void UpdateLanguage()
    {
        string langCode = "en";
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: langCode = "ko"; break;
            case SystemLanguage.Japanese: langCode = "ja"; break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: langCode = "zh"; break;
            case SystemLanguage.Spanish: langCode = "es"; break;
        }

        if (!localizedFilterNames.ContainsKey(langCode)) langCode = "en";
        var texts = localizedFilterNames[langCode];

        SetToggleLabel(petFriendlyToggle, texts["petFriendly"]);
        SetToggleLabel(publicDataToggle, texts["publicData"]);
        SetToggleLabel(subwayToggle, texts["subway"]);
        SetToggleLabel(busToggle, texts["bus"]);
        SetToggleLabel(alcoholToggle, texts["alcohol"]);
        SetToggleLabel(woopangDataToggle, texts["woopangData"]);
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

    private void LoadFilterSettings()
    {
        filterPetFriendly = PlayerPrefs.GetInt(PREF_PET_FRIENDLY, 1) == 1;
        filterPublicData = PlayerPrefs.GetInt(PREF_PUBLIC_DATA, 1) == 1;
        filterSubway = PlayerPrefs.GetInt(PREF_SUBWAY, 1) == 1;
        filterBus = PlayerPrefs.GetInt(PREF_BUS, 1) == 1;
        filterAlcohol = PlayerPrefs.GetInt(PREF_ALCOHOL, 1) == 1;
        filterWoopangData = PlayerPrefs.GetInt(PREF_WOOPANG_DATA, 1) == 1;
        filterObject3D = PlayerPrefs.GetInt(PREF_OBJECT3D, 1) == 1;
    }

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
    }

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
            { "petFriendly", filterPetFriendly },
            { "publicData", filterPublicData },
            { "subway", filterSubway },
            { "bus", filterBus },
            { "alcohol", filterAlcohol },
            { "woopangData", filterWoopangData },
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