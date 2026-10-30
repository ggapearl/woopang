using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class PlaceListManager : MonoBehaviour
{
    public DataManager dataManager;
    public TourAPIManager tourAPIManager;
    
    [Header("New Public Data Managers")]
    public TerminalManager terminalManager;
    public TrainStationManager trainManager;
    public SubwayManager subwayManager;

    [Header("P2P User Manager")]
    public P2PManager p2pManager;

    public Text listText;

    [Header("UI Update Settings")]
    [SerializeField] private GameObject listPanel;
    [SerializeField] private PlaceListSkeletonLoader skeletonLoader;
    [SerializeField] private float updateInterval = 10f;

    [Header("List Size Limits")]
    [Tooltip("리스트에 표시할 최대 항목 수 (거리 가까운 순). 성능 보호용 상한 — DB가 커져도 이 값 이상은 안 만짐")]
    [SerializeField] private int maxListEntries = 1000;

    [Header("Distance Control")]
    [SerializeField] private Slider distanceSlider;
    [SerializeField] private Text distanceValueText;
    private float maxDisplayDistance;

    private List<(object place, float distance, string id, string displayText, string colorHex)> combinedPlaces = new List<(object, float, string, string, string)>();

    // 매 프레임 거리/정렬만 갱신용 — 풀빌드 시 채우고 Update()에서 GPS 변동분만 반영
    // targetTf가 살아있으면 카메라 transform 기준 (OffScreenIndicator와 동일 — 매끄러움 GPS udpate freq 무관)
    // 비어있으면 GPS baseLat/baseLon fallback (먼 POI, 스폰 안 된 것)
    private class LiveEntry
    {
        public string id;
        public string baseLabel;     // 거리 빼고 표시명만 (예: "스타벅스" 또는 "@user")
        public string colorHex;
        public double baseLat;
        public double baseLon;
        public Transform targetTf;   // 스폰된 GameObject가 있으면 카메라 거리 우선
        public Target target;        // 같은 오브젝트의 Target (목록 줄 누르기·방향)
        public EntryKind kind;
        public int huntId;
        public R0926HuntList.Coord huntCoord;
        public bool huntFound;       // 내가 이미 찾은 보물 — 진행 중이라 남기되 못 찾은 것 뒤로 ('찾은 보물입니다')
        public float approxM;
        public float fixedDistance = -1f;   // 0 이상이면 이 거리 그대로 (비공개 보물찾기 — 자리를 모른다)
        public string icon;          // 장소 — 분류 그림(r0926_c_<이름>, PlaceIcon) · 축제·보호소 — 줄 앞 그림 · 보물찾기는 "lock" = 비밀번호를 건 보물 (R0926PlaceRows)
        public string start, end;    // 축제 날짜 (YYYYMMDD)
        public string link;          // 축제·보호소 — 누르면 여는 쪽
        public byte pin;             // 목록 맨 위에 — 클수록 위: 3 광고 줄 · 2 축제 / 보물 칩의 축제·보물찾기 · 1 애견동반의 보호소 (0 은 거리순)
        public bool anyDistance;     // 거리 슬라이더와 상관없이 (축제·보호소·맨 위 보물찾기)
        public bool promo;           // 관리자가 띄운 협력 업체(광고) 줄 — '광고'를 밝히고 노출·클릭을 센다
        public string adId, emojiUrl;
        public bool pet;             // 애견동반 되는 장소 — 목록 이름이 노랑 (R0926PlaceRows)
    }
    private List<LiveEntry> liveEntries = new List<LiveEntry>();

    /// <summary>목록 줄 앞의 그림 — 장소는 분류 그림(분류가 없으면 색 점), 사람은 분홍 사람, 보물찾기는 금빛 상자, 축제는 축제 분위기 그림, 보호소는 강아지</summary>
    public enum EntryKind : byte { Place, Person, Hunt, Festival, Shelter }

    /// <summary>목록 글자의 장소 한 줄 — 글자와 같은 순서·같은 개수 (R0926PlaceRows 가 줄 누르기·방향 표시에 쓴다)</summary>
    public struct ListEntry
    {
        public string id;        // DataManager·TourAPI·P2P id, 공공교통은 "종류_이름", 보물찾기는 "hunt:번호"
        public string name;      // 글자 줄의 이름 그대로 (P2P 는 '@이름')
        public float distance;   // m
        public float lat;
        public float lon;
        public Target target;    // 목록을 새로 만들 때 찾은 AR 오브젝트 — 없거나 사라졌을 수 있다 (FindTarget 으로 다시 찾는다)
        public EntryKind kind;
        public int huntId;           // 보물찾기 — 누르면 그 게임 상세
        public string distanceText;  // 거리 칸에 숫자 대신 쓸 말 (보물찾기 대략·비공개) — null 이면 숫자
        public bool noDirection;     // 방향을 보이지 않는다 (비공개 보물찾기 · 대략 원 안 · 축제)
        public string icon;          // 장소는 분류 그림 이름 · 축제·보호소 줄 앞 그림 · 보물찾기는 "lock" (자물쇠 배지)
        public string link;          // 축제·보호소 — 누르면 여는 쪽
        public bool promo;           // 광고 줄 (축제 칩 맨 위) — 거리 칸 '광고 · 거리' · 줄 앞은 고른 이모지 그림
        public string adId, emojiUrl;
        public bool pet;             // 애견동반 되는 장소 — 이름을 노랑으로
    }
    private readonly List<ListEntry> shownEntries = new List<ListEntry>();
    /// <summary>listText 의 장소 줄과 같은 순서의 장소 정보. 글자는 예전 그대로다 (다른 코드가 글자를 읽는다)</summary>
    public IReadOnlyList<ListEntry> ShownEntries => shownEntries;
    private System.Text.StringBuilder liveBuilder = new System.Text.StringBuilder(2048);
    private (float d, int idx)[] orderedBuffer = new (float, int)[64];
    private float[] distBuffer = new float[64];
    private const float PinnedFirst = 1e7f;   // 맨 위 줄은 정렬 열쇠에서 pin 마다 이만큼 빼 앞으로
    private const int MaxShelters = 3, MaxFestivals = 3, MaxNearHunts = 3;
    private float lastGpsLat;
    private float lastGpsLon;
    private bool hasLiveSnapshot = false;
    private Camera arCameraCache;
    private string lastDisplayedText = null;       // UI Text mesh rebuild 방지 — 변경 시에만 set
    private bool wasListPanelActive = false;       // listPanel 활성 전환 감지 → 즉시 풀빌드
    private int dataLoadRetryAttempts = 0;         // 데이터 비어있을 때 재시도 카운터
    private const int MAX_DATA_LOAD_RETRIES = 3;   // 최대 3회 (총 3초)

    // Stats
    private int woopangCount;
    private int tourAPICount;
    private int publicTransportCount;
    private int p2pUserCount;
    private int huntCount;
    private int festivalCount;
    private int shelterCount;
    private P2PManager filterSource;   // 사람 칩(팔로잉·전체·제외)이 바뀌면 바로 다시 그린다

    // P2P 사용자 색상 (핑크)
    private const string P2P_USER_COLOR = "E95383";

    // 교통 수단별 색상
    private const string TERMINAL_COLOR = "00FF00";
    private const string TRAIN_COLOR = "00FF00";
    private const string SUBWAY_COLOR = "3DA29C";

    // 카테고리 색상은 DataManager.GetCategoryColor / ResolvePlaceColorHex 로 단일화
    // (인디케이터·풀오브젝트·리스트가 같은 소스를 써서 항상 일치)

    private Coroutine updatePeriodicCoroutine;

    private Dictionary<string, bool> activeFilters = new Dictionary<string, bool>
    {
        { "woopangData", true },
        { "petFriendly", true },
        { "publicData", true },
        { "subway", true },
        { "bus", true },
        { "p2pUsers", true }
    };

    private Dictionary<string, Dictionary<string, string>> languageTexts = new Dictionary<string, Dictionary<string, string>>
    {
        { "en", new Dictionary<string, string> {
            { "petFriendly", "[PetFriendly]" }, { "noImage", "[No Image]" },
            { "woopangData", "WOOPANG DATA" }, { "tourApiData", "Public Data" },
            { "transportData", "TRANSPORT DATA" }, { "p2pUserData", "NEARBY USERS" }, { "huntData", "TREASURE HUNTS" }, { "festivalData", "FESTIVALS" }, { "shelterData", "SHELTERS" },
            { "noNearbyData", "No nearby data found.\nTry adjusting the distance slider or moving to a different area." },
            { "noInternet", "No internet connection.\nNearby places will load automatically once you're back online." },
            { "listNotLoaded", "Couldn't load nearby places yet.\nWe'll try again automatically." }
        }},
        { "ko", new Dictionary<string, string> {
            { "petFriendly", "[애견동반]" }, { "noImage", "[이미지없음]" },
            { "woopangData", "우팡 데이터" }, { "tourApiData", "공공데이터" },
            { "transportData", "대중교통 데이터" }, { "p2pUserData", "근처 사용자" }, { "huntData", "보물찾기" }, { "festivalData", "축제" }, { "shelterData", "보호소" },
            { "noNearbyData", "주변에 데이터가 없습니다.\n거리 슬라이더를 조정하거나 다른 위치로 이동해보세요." },
            { "noInternet", "인터넷 연결이 없어요.\n연결되면 주변 장소를 저절로 불러와요." },
            { "listNotLoaded", "주변 장소를 아직 받지 못했어요.\n잠시 뒤 저절로 다시 불러와요." }
        }},
        { "ja", new Dictionary<string, string> {
            { "petFriendly", "[ペット同伴]" }, { "noImage", "[画像なし]" },
            { "woopangData", "WOOPANGデータ" }, { "tourApiData", "公共データ" },
            { "transportData", "交通データ" }, { "p2pUserData", "近くのユーザー" }, { "huntData", "宝探し" }, { "festivalData", "お祭り" }, { "shelterData", "保護施設" },
            { "noNearbyData", "近くにデータがありません。\n距離スライダーを調整するか、別の場所に移動してみてください。" },
            { "noInternet", "インターネットに接続されていません。\n接続すると周辺の場所を自動で読み込みます。" },
            { "listNotLoaded", "周辺の場所をまだ読み込めていません。\nしばらくすると自動で再読み込みします。" }
        }},
        { "zh", new Dictionary<string, string> {
            { "petFriendly", "[宠物友好]" }, { "noImage", "[无图片]" },
            { "woopangData", "WOOPANG数据" }, { "tourApiData", "公共数据" },
            { "transportData", "交通数据" }, { "p2pUserData", "附近用户" }, { "huntData", "寻宝" }, { "festivalData", "节庆" }, { "shelterData", "收容所" },
            { "noNearbyData", "附近没有数据。\n请调整距离滑块或换个地方试试。" },
            { "noInternet", "没有网络连接。\n连接后会自动加载附近地点。" },
            { "listNotLoaded", "暂时无法加载附近地点。\n稍后会自动重试。" }
        }},
        { "es", new Dictionary<string, string> {
            { "petFriendly", "[Mascotas]" }, { "noImage", "[Sin imagen]" },
            { "woopangData", "Datos WOOPANG" }, { "tourApiData", "Datos Públicos" },
            { "transportData", "Datos de Transporte" }, { "p2pUserData", "Usuarios Cercanos" }, { "huntData", "Búsquedas del tesoro" }, { "festivalData", "Festivales" }, { "shelterData", "Refugios" },
            { "noNearbyData", "No hay datos cerca.\nAjusta el control de distancia o muévete a otra zona." },
            { "noInternet", "Sin conexión a internet.\nLos lugares cercanos se cargarán automáticamente al reconectar." },
            { "listNotLoaded", "Aún no se pudieron cargar los lugares cercanos.\nLo intentaremos de nuevo automáticamente." }
        }}
    };

    void Start()
    {
        HiddenPlaces.Hidden += OnPlaceHidden;
        R0926HuntList.Changed += OnSourceChanged;
        R0926Nearby.Changed += OnSourceChanged;
        filterSource = p2pManager != null ? p2pManager : P2PManager.Instance;
        if (filterSource != null) filterSource.UserFilterChanged += OnSourceChanged;
        // 슬라이더 유무와 무관하게 기본값 보장 — 슬라이더 없으면 maxDisplayDistance=0이 되어 모든 POI 걸리는 버그 방지
        maxDisplayDistance = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);

        if (distanceSlider != null)
        {
            distanceSlider.minValue = 100f;
            distanceSlider.maxValue = 10000f;
            distanceSlider.value = maxDisplayDistance;
            distanceSlider.onValueChanged.AddListener(OnDistanceSliderChanged);
            UpdateDistanceValueText();

            // FilterManager IndicatorOnly 반경 초기 동기화 (FilterManager가 비활성일 수 있으므로 Include)
            FilterManager filterMgr = UnityEngine.Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
            if (filterMgr != null) filterMgr.SetIndicatorRadius(maxDisplayDistance);
        }

        StartCoroutine(InitializeAndUpdateUI());
    }

    private string GetLocalizedText(string key)
    {
        // 예전엔 한국어가 아니면 전부 영어였다 — 일본어·중국어·스페인어 표도 쓰고, 없는 문구만 영어로
        string lang = AppLanguage.Code;
        if (languageTexts.TryGetValue(lang, out var t) && t.TryGetValue(key, out var v)) return v;
        return languageTexts["en"].TryGetValue(key, out var en) ? en : key;
    }

    private IEnumerator InitializeAndUpdateUI()
    {
        if (skeletonLoader != null) skeletonLoader.ShowSkeletonLoader();
        
        // Wait for data (Simplified check)
        yield return new WaitForSeconds(2f);

        if (skeletonLoader != null) skeletonLoader.HideSkeletonAndShowText();
        UpdateUI();
        updatePeriodicCoroutine = StartCoroutine(UpdateUIPeriodically());
    }

    private IEnumerator UpdateUIPeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            if (listPanel != null && listPanel.activeInHierarchy) UpdateUI();
        }
    }

    private Coroutine updateUICoroutine;

    // 인디케이터 X 로 숨긴 장소는 목록에서도 뺀다 (HiddenPlaces — 앱을 다시 켜면 초기화)
    private void OnPlaceHidden(string uniqueId)
    {
        if (isActiveAndEnabled) UpdateUI();
    }

    // 사람 칩을 누름 · 팔로잉 목록이 도착함 · 보물찾기 게임 목록 · 근처 축제·보호소가 바뀜 — 열려 있으면 바로 (예전엔 10초 주기를 기다려 '팔로잉'을 눌러도 그대로였다).
    // 닫혀 있으면 열 때 새로 그린다
    private void OnSourceChanged()
    {
        if (isActiveAndEnabled && listPanel != null && listPanel.activeInHierarchy) UpdateUI();
    }

    private void OnDestroy()
    {
        HiddenPlaces.Hidden -= OnPlaceHidden;
        R0926HuntList.Changed -= OnSourceChanged;
        R0926Nearby.Changed -= OnSourceChanged;
        if (filterSource != null) filterSource.UserFilterChanged -= OnSourceChanged;
        if (updatePeriodicCoroutine != null) StopCoroutine(updatePeriodicCoroutine);
        if (updateUICoroutine != null) StopCoroutine(updateUICoroutine);
        if (distanceSlider != null) distanceSlider.onValueChanged.RemoveListener(OnDistanceSliderChanged);
    }

    public void UpdateUI()
    {
        if (updateUICoroutine != null) StopCoroutine(updateUICoroutine);
        updateUICoroutine = StartCoroutine(UpdateUIWithFadeIn());
    }

    private IEnumerator UpdateUIWithFadeIn()
    {
        float lat = 36.6361f; float lon = 126.8280f; // Default fallback
#if UNITY_EDITOR
        // 에디터에서는 VirtualLocation 사용
        if (VirtualLocation.Instance != null) {
            lat = VirtualLocation.Instance.Latitude;
            lon = VirtualLocation.Instance.Longitude;
        }
#else
        if (Input.location.status == LocationServiceStatus.Running) {
            lat = Input.location.lastData.latitude; lon = Input.location.lastData.longitude;
        }
#endif

        combinedPlaces.Clear();
        liveEntries.Clear();
        lastGpsLat = lat; lastGpsLon = lon;
        woopangCount = 0; tourAPICount = 0; publicTransportCount = 0; p2pUserCount = 0; huntCount = 0; festivalCount = 0; shelterCount = 0;

        // 축제 / 보물 칩 — 다른 칩은 그대로 두고 지금 목록 맨 위에 가까운 축제 · 보물찾기를 더한다 (10-27 대표님, 예전엔 목록이 축제만)
        bool festivalMode = activeFilters.GetValueOrDefault("festival", false);
        bool petFriendlyOnly = activeFilters.GetValueOrDefault("petFriendlyOnly", false);
        bool petFriendlyAll = activeFilters.GetValueOrDefault("petFriendlyAll", true);
        bool showPublic = activeFilters.GetValueOrDefault("publicData", true);
        bool showSubway = activeFilters.GetValueOrDefault("subway", true);
        bool showTrain = activeFilters.GetValueOrDefault("train", true);
        bool showTerminal = activeFilters.GetValueOrDefault("terminal", true);
        bool showObject3D = activeFilters.GetValueOrDefault("object3D", true);
        bool categoryFilterActive = activeFilters.GetValueOrDefault("categoryFilter", false);

        // 카테고리 필터 값 가져오기
        string activeCategoryFilter = "";
        FilterManager filterMgr = UnityEngine.Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null)
            activeCategoryFilter = filterMgr.GetActiveCategoryFilter();

        // 1. Woopang Data (lightCache 기반 — placeDataMap에 있으면 상세 데이터 활용)
        if (dataManager != null) {
            HashSet<int> addedIds = new HashSet<int>();
            var placeDataMap = dataManager.GetPlaceDataMap();

            // 1a. lightCache에서 전체 목록 빌드
            foreach (var cached in dataManager.GetLightCache()) {
                if (!int.TryParse(cached.rawId, out int id)) continue;
                if (HiddenPlaces.IsHidden("dm_" + cached.rawId)) continue;
                string cat = cached.category ?? "";
                string modelType = cached.modelType ?? "cube";

                if (!showObject3D && modelType == "custom") continue;
                if (petFriendlyOnly && !cached.petFriendly) continue;
                if (categoryFilterActive && !string.IsNullOrEmpty(activeCategoryFilter))
                {
                    if (cat != activeCategoryFilter) continue;
                }

                float d = CalculateDistance(lat, lon, cached.latitude, cached.longitude);
                if (d <= maxDisplayDistance) {
                    bool isPublicData = cached.filterKey == "publicData";   // 등록자 기준 (카테고리 아님)
                    if (isPublicData)
                    {
                        // 카테고리 필터 활성 시 publicData 토글 무시 (카테고리 매칭이 이미 필터링)
                        if (!categoryFilterActive && !showPublic) continue;
                        tourAPICount++;
                    }
                    else
                    {
                        woopangCount++;
                    }

                    // 색상: DataManager 단일 소스 (칩 분류는 칩 색 · 분류 없음은 흰색 · 그 밖은 서버 color(HEX) 우선)
                    string displayName = cached.displayName;
                    string rawColor = placeDataMap.ContainsKey(id) ? placeDataMap[id].color : null;
                    string colorHex = DataManager.ResolvePlaceColorHex(rawColor, cat, displayName);
                    combinedPlaces.Add((cached, d, id.ToString(), $"{displayName} - {Mathf.FloorToInt(d)}m", colorHex));
                    liveEntries.Add(new LiveEntry { id = id.ToString(), baseLabel = displayName, colorHex = colorHex, baseLat = cached.latitude, baseLon = cached.longitude, icon = PlaceIcon(cat, modelType == "custom"), pet = cached.petFriendly });
                    addedIds.Add(id);
                }
            }

            // 1b. placeDataMap에만 있고 lightCache에 없는 데이터 (Detail API로 가져온 것)
            foreach (var p in placeDataMap.Values) {
                if (addedIds.Contains(p.id)) continue;
                if (HiddenPlaces.IsHidden("dm_" + p.id)) continue;
                string origType = p.original_model_type ?? p.model_type;
                if (!showObject3D && origType == "custom") continue;
                if (petFriendlyOnly && !p.pet_friendly) continue;
                if (categoryFilterActive && !string.IsNullOrEmpty(activeCategoryFilter))
                {
                    if ((p.category ?? "") != activeCategoryFilter) continue;
                }

                float d = CalculateDistance(lat, lon, p.latitude, p.longitude);
                if (d <= maxDisplayDistance) {
                    bool isPublicData = FilterManager.IsPublicSource(p.is_public, p.category);
                    if (isPublicData)
                    {
                        if (!categoryFilterActive && !showPublic) continue;
                        tourAPICount++;
                    }
                    else
                    {
                        woopangCount++;
                    }
                    string pColor = DataManager.ResolvePlaceColorHex(p.color, p.category, p.name);
                    combinedPlaces.Add((p, d, p.id.ToString(), $"{p.name} - {Mathf.FloorToInt(d)}m", pColor));
                    liveEntries.Add(new LiveEntry { id = p.id.ToString(), baseLabel = p.name, colorHex = pColor, baseLat = p.latitude, baseLon = p.longitude, icon = PlaceIcon(p.category, origType == "custom"), pet = p.pet_friendly });
                }
            }
        }

        // 2. TourAPI
        if (showPublic && tourAPIManager != null) {
            foreach(var p in tourAPIManager.GetPlaceDataMap().Values) {
                if (HiddenPlaces.IsHidden("tour_" + p.contentid)) continue;
                float d = CalculateDistance(lat, lon, p.mapy, p.mapx);
                if (d <= maxDisplayDistance) {
                    tourAPICount++;
                    combinedPlaces.Add((p, d, p.contentid, $"{p.title} - {Mathf.FloorToInt(d)}m", p.color));
                    // 관광공사 반려동물 여행 자료 — 모두 애견동반 (TourAPIManager 캐시도 petFriendly = true)
                    liveEntries.Add(new LiveEntry { id = p.contentid, baseLabel = p.title, colorHex = p.color, baseLat = p.mapy, baseLon = p.mapx, icon = "tour", pet = true });
                }
            }
        }

        // 3. New Public Transport Managers
        AddTransportData(terminalManager, showTerminal, ref publicTransportCount, lat, lon, TERMINAL_COLOR, "terminal_", "bus");
        AddTransportData(trainManager, showTrain, ref publicTransportCount, lat, lon, TRAIN_COLOR, "train_", "train");
        AddTransportData(subwayManager, showSubway, ref publicTransportCount, lat, lon, SUBWAY_COLOR, "subway_", "subway");

        // 4. P2P Users (근처 사용자)
        bool showP2PUsers = activeFilters.GetValueOrDefault("p2pUsers", true);
        if (showP2PUsers && p2pManager != null)
        {
            AddP2PUserData(lat, lon);
        }

        // 5. 보물찾기 — 내가 연 게임 · 참가 중인 게임 (분류를 고른 동안은 사람처럼 뺀다). 축제 / 보물 칩을 켜지 않아도 보인다
        if (!categoryFilterActive) AddHuntData(lat, lon);
        if (listPanel != null && listPanel.activeInHierarchy)
        {
            var huntFetch = R0926HuntList.RefreshIfDue();   // 열려 있는 동안 1분에 한 번 — 받으면 Changed 로 다시 그린다
            if (huntFetch != null) StartCoroutine(huntFetch);
            // 근처 축제·보호소 — 1km 넘게 움직였거나 30분이 지났을 때만 다시 묻는다 (받으면 Changed)
            var festFetch = festivalMode ? R0926Nearby.FestivalsIfDue(lat, lon) : null;
            if (festFetch != null) StartCoroutine(festFetch);
            var shelterFetch = petFriendlyOnly ? R0926Nearby.SheltersIfDue(lat, lon) : null;
            if (shelterFetch != null) StartCoroutine(shelterFetch);
        }

        // 6. 맨 위 줄 (거리 슬라이더와 상관없이) — 축제 / 보물: 광고 줄 · 가까운 축제 · 참가할 수 있는 근처 보물찾기 / 애견동반: 가까운 보호소
        if (festivalMode)
        {
            AddFestivalData(lat, lon);
            if (!categoryFilterActive) AddNearHuntData(lat, lon);
        }
        if (petFriendlyOnly) AddShelterData(lat, lon);

        // 거리순 정렬 + 상위 maxListEntries개만 (DB 커져도 이 값 이상은 안 만짐) — 맨 위 줄은 먼저 (pin 큰 것부터)
        // liveEntries 는 combinedPlaces 와 같은 순서로 하나씩 쌓였다 — 같은 순서로 골라 목록 줄과 1:1 로 맞춘다
        // (예전 id 기준 자르기는 매니저끼리 id 가 겹치면 어긋났다)
        var order = Enumerable.Range(0, combinedPlaces.Count)
            .OrderByDescending(i => i < liveEntries.Count ? liveEntries[i].pin : 0).ThenBy(i => i < liveEntries.Count && liveEntries[i].huntFound ? 1 : 0).ThenBy(i => combinedPlaces[i].distance)
            .Take(maxListEntries).ToList();
        var sortedPlaces = new List<(object place, float distance, string id, string displayText, string colorHex)>(order.Count);
        var sortedLive = new List<LiveEntry>(order.Count);
        foreach (int i in order)
        {
            sortedPlaces.Add(combinedPlaces[i]);
            if (i < liveEntries.Count) sortedLive.Add(liveEntries[i]);
        }
        combinedPlaces = sortedPlaces;
        liveEntries = sortedLive;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var item in combinedPlaces) {
            string color = string.IsNullOrEmpty(item.colorHex) ? "FFFFFF" : item.colorHex;
            sb.Append($"<color=#{color}>{item.displayText}</color>\n");
        }

        cachedFooter = $"\n{GetLocalizedText("woopangData")}: {woopangCount}\n{GetLocalizedText("tourApiData")}: {tourAPICount}\n{GetLocalizedText("transportData")}: {publicTransportCount}\n{GetLocalizedText("p2pUserData")}: {p2pUserCount}";
        if (huntCount > 0) cachedFooter += $"\n{GetLocalizedText("huntData")}: {huntCount}";
        // 축제 수 (광고 줄은 빼고) — 없으면 까닭 한 줄 (아직 받는 중 · 공공데이터 신청 전 · 근처에 없음)
        if (festivalMode) cachedFooter += festivalCount > 0 ? $"\n{GetLocalizedText("festivalData")}: {festivalCount}" : "\n" + R0926Nearby.EmptyMessage();
        if (shelterCount > 0) cachedFooter += $"\n{GetLocalizedText("shelterData")}: {shelterCount}";
        sb.Append(cachedFooter);

        // 데이터 비어있고 panel 열려있으면 자동 재시도 (데이터 로드 race condition 대응)
        if (combinedPlaces.Count == 0 && listPanel != null && listPanel.activeInHierarchy)
        {
            bool noInternet = Application.internetReachability == NetworkReachability.NotReachable;
            if (!noInternet && dataLoadRetryAttempts < MAX_DATA_LOAD_RETRIES)
            {
                dataLoadRetryAttempts++;
                yield return new WaitForSeconds(1f);
                UpdateUI();
                yield break;
            }
            else
            {
                // 재시도 후에도 빈 결과 → 안내 표시. 받아 둔 장소가 하나도 없을 때만 못 받은 까닭을 말한다 —
                // 인터넷이 끊겼으면 바로 '인터넷 연결이 없어요', 서버가 응답 없거나 아직 못 받았으면 '아직 받지 못했어요'.
                // 받아 둔 게 있는데 비었으면 거리·분류 때문이니 '없음'. 연결되어 받으면 다음 주기 갱신(updateInterval)이 줄로 바꾼다
                if (listText != null)
                {
                    bool nothingCached = (dataManager == null || dataManager.GetLightCache().Count == 0)
                                         && (tourAPIManager == null || tourAPIManager.GetPlaceDataMap().Count == 0);
                    string emptyMsg = GetLocalizedText(!nothingCached ? "noNearbyData"
                        : noInternet ? "noInternet"
                        : ServerHealth.Down || dataManager == null || !dataManager.IsCacheReady ? "listNotLoaded"
                        : "noNearbyData");
                    listText.text = emptyMsg;
                    lastDisplayedText = emptyMsg;
                }
                shownEntries.Clear();
                hasLiveSnapshot = false;
                yield break;
            }
        }

        dataLoadRetryAttempts = 0; // 데이터 들어왔으면 카운터 리셋

        if (listText != null)
        {
            string newText = sb.ToString();
            listText.text = newText;
            lastDisplayedText = newText;
        }

        // 활성 Target들을 placeId 기준으로 liveEntries에 매핑 (스폰된 POI는 카메라 거리 우선 사용)
        Target[] activeTargets = UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (activeTargets != null && activeTargets.Length > 0)
        {
            var byId = new Dictionary<string, Target>(activeTargets.Length);
            var byName = new Dictionary<string, Target>(activeTargets.Length);
            foreach (var t in activeTargets)
            {
                if (t == null) continue;
                if (!string.IsNullOrEmpty(t.placeId) && !byId.ContainsKey(t.placeId)) byId[t.placeId] = t;
                if (!string.IsNullOrEmpty(t.PlaceName) && !byName.ContainsKey(t.PlaceName)) byName[t.PlaceName] = t;
            }
            for (int i = 0; i < liveEntries.Count; i++)
            {
                var e = liveEntries[i];
                if (e.kind == EntryKind.Hunt) continue;   // AR 보물 자리는 목록에 쓰지 않는다 (공개 설정만 — R0926HuntList)
                if (e.kind == EntryKind.Festival || e.kind == EntryKind.Shelter) continue;   // AR 로 띄우지 않는다 — 이름이 같은 장소와 엮이지 않게
                Target tg;
                if (!string.IsNullOrEmpty(e.id) && byId.TryGetValue(e.id, out tg)) { e.target = tg; e.targetTf = tg.transform; continue; }
                if (!string.IsNullOrEmpty(e.baseLabel) && byName.TryGetValue(e.baseLabel, out tg)) { e.target = tg; e.targetTf = tg.transform; }
            }
        }

        // 목록 줄과 같은 순서의 장소 정보 (글자는 위에서 그대로 썼다)
        shownEntries.Clear();
        for (int i = 0; i < liveEntries.Count && i < combinedPlaces.Count; i++)
            shownEntries.Add(ToListEntry(liveEntries[i], combinedPlaces[i].distance));
        hasLiveSnapshot = liveEntries.Count > 0;
        if (arCameraCache == null) arCameraCache = Camera.main;
        yield return null;
    }

    private string cachedFooter = "";

    void Update()
    {
        if (listText == null) return;

        // listPanel 활성 전환 감지 — 비활성→활성 전환 시 즉시 풀빌드 (10초 대기 안 함)
        bool nowActive = listPanel != null && listPanel.activeInHierarchy;
        if (nowActive && !wasListPanelActive)
        {
            wasListPanelActive = true;
            UpdateUI();
            return;
        }
        wasListPanelActive = nowActive;

        if (!hasLiveSnapshot || !nowActive) return;

        float lat = lastGpsLat;
        float lon = lastGpsLon;
#if UNITY_EDITOR
        if (VirtualLocation.Instance != null)
        {
            lat = VirtualLocation.Instance.Latitude;
            lon = VirtualLocation.Instance.Longitude;
        }
#else
        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }
#endif

        // 카메라 위치 (스폰된 POI 거리 계산용 — OffScreenIndicator와 동일 방식)
        if (arCameraCache == null) arCameraCache = Camera.main;
        Vector3 camPos = arCameraCache != null ? arCameraCache.transform.position : Vector3.zero;
        bool hasCam = arCameraCache != null;

        // GPS fallback용 평면 근사 상수 (프레임당 1회 cos 계산 — Update 안에서 Haversine trig 누적 비용 제거)
        float cosLat = Mathf.Cos(Mathf.Deg2Rad * lat);

        int count = liveEntries.Count;
        if (orderedBuffer.Length < count)
        {
            Array.Resize(ref orderedBuffer, Mathf.NextPowerOfTwo(count));
            Array.Resize(ref distBuffer, orderedBuffer.Length);
        }
        for (int i = 0; i < count; i++)
        {
            var e = liveEntries[i];
            float d;
            if (e.fixedDistance >= 0f) d = e.fixedDistance;   // 비공개 보물찾기 — 굵은 거리 그대로
            // 1순위: 스폰된 Target transform이 살아있으면 카메라 거리 (1cm 단위로 매끄럽게 갱신)
            else if (hasCam && e.targetTf != null) d = Vector3.Distance(camPos, e.targetTf.position);
            else
            {
                // 2순위: GPS 거리 fallback — 평면 근사 (한국 위도/5km 이내 오차 0.5% 미만, 매 프레임 trig 회피)
                float dLatM = (float)((e.baseLat - lat) * 111320.0);   // 빼기는 double 로 — float 끼리 빼면 0.4m 단위로 끊긴다
                float dLonM = (float)((e.baseLon - lon) * 111320.0 * cosLat);
                d = Mathf.Sqrt(dLatM * dLatM + dLonM * dLonM);
            }
            distBuffer[i] = d;
            orderedBuffer[i] = (d - PinnedFirst * e.pin, i);
        }
        Array.Sort(orderedBuffer, 0, count, OrderedComparer.Instance);

        liveBuilder.Clear();
        shownEntries.Clear();
        for (int i = 0; i < count; i++)
        {
            var pair = orderedBuffer[i];
            var e = liveEntries[pair.idx];
            float d = distBuffer[pair.idx];
            if (!InRange(e, d)) continue;
            string color = string.IsNullOrEmpty(e.colorHex) ? "FFFFFF" : e.colorHex;
            liveBuilder.Append("<color=#").Append(color).Append('>')
                       .Append(e.baseLabel).Append(" - ")
                       .Append(Mathf.FloorToInt(d)).Append("m</color>\n");
            shownEntries.Add(ToListEntry(e, d));
        }
        liveBuilder.Append(cachedFooter);

        // 텍스트가 실제로 바뀌었을 때만 set — UI Text mesh rebuild 비용 회피 (모바일에서 큼)
        string newText = liveBuilder.ToString();
        if (newText != lastDisplayedText)
        {
            listText.text = newText;
            lastDisplayedText = newText;
        }
    }

    // 거리 슬라이더 안인지 — 대략 공개 보물찾기는 원 가장자리까지 (비공개는 목록을 만들 때 이미 걸렀다) · 축제·보호소는 늘
    private bool InRange(LiveEntry e, float d)
    {
        if (maxDisplayDistance <= 0f || e.fixedDistance >= 0f || e.anyDistance) return true;
        return d - e.approxM <= maxDisplayDistance;
    }

    private static ListEntry ToListEntry(LiveEntry e, float distance)
    {
        var le = new ListEntry { id = e.id, name = e.baseLabel, distance = distance, lat = (float)e.baseLat, lon = (float)e.baseLon, target = e.target, kind = e.kind, huntId = e.huntId, icon = e.icon, link = e.link,
                                 promo = e.promo, adId = e.adId, emojiUrl = e.emojiUrl, pet = e.pet };
        if (e.kind == EntryKind.Hunt)
        {
            le.distanceText = e.huntFound ? R0926HuntList.FoundText : R0926HuntList.Label(e.huntCoord, distance, e.approxM, e.fixedDistance);
            le.noDirection = R0926HuntList.NoDirection(e.huntCoord, distance, e.approxM);
        }
        else if (e.kind == EntryKind.Festival)
        {
            // '10.30부터 · 12km' — 방향 자리까지 쓴다. 광고 줄은 '광고 · 6.7km'
            le.distanceText = e.promo ? R0926Nearby.AdLabel + " · " + R0926Nearby.Km(distance) : R0926Nearby.FestivalLabel(e.start, e.end, distance);
            le.noDirection = true;
        }
        return le;
    }

    /// <summary>
    /// 목록 장소의 AR 오브젝트(Target) — 지금 떠 있으면, 없으면 null.
    /// id 와 이름이 둘 다 맞는 것을 먼저 고른다 (매니저끼리 숫자 id 가 겹칠 수 있다). 공공교통은 id 가 없어 이름으로.
    /// </summary>
    public Target FindTarget(ListEntry entry)
    {
        if (entry.target != null && entry.target.isActiveAndEnabled && SameName(entry.target, entry.name)) return entry.target;
        Target idOnly = null, nameOnly = null;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            bool nameOk = SameName(t, entry.name);
            if (!string.IsNullOrEmpty(entry.id) && t.placeId == entry.id)
            {
                if (nameOk) return t;
                if (idOnly == null) idOnly = t;
            }
            else if (nameOk && nameOnly == null) nameOnly = t;
        }
        return idOnly != null ? idOnly : nameOnly;
    }

    // 목록 이름과 Target 이름 비교 — P2P 사용자는 목록에 '@이름', Target 에는 '이름'
    private static bool SameName(Target t, string name)
    {
        string p = t.PlaceName;
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(p)) return false;
        if (p == name) return true;
        return name.Length == p.Length + 1 && name[0] == '@' && string.CompareOrdinal(name, 1, p, 0, p.Length) == 0;
    }

    /// <summary>
    /// P2P 사용자 데이터 추가
    /// P2PManager의 필터 모드에 따라 표시 여부 결정
    /// - None: 목록에 표시 안함
    /// - All: 모든 사용자 표시
    /// - FollowingOnly: 팔로잉한 사용자만 표시
    /// </summary>
    private void AddP2PUserData(float lat, float lon)
    {
        if (p2pManager == null) return;

        // 필터링된 사용자 목록 가져오기 (None이면 빈 리스트 반환)
        var nearbyUsers = p2pManager.GetFilteredNearbyUsers();
        if (nearbyUsers == null || nearbyUsers.Count == 0) return;

        foreach (var user in nearbyUsers)
        {
            if (user.distance <= maxDisplayDistance)
            {
                p2pUserCount++;
                string displayText = $"@{user.username} - {Mathf.FloorToInt(user.distance)}m";   // 이모지는 앱 글꼴에 없어 □로 보였다
                combinedPlaces.Add((user, user.distance, user.user_id, displayText, P2P_USER_COLOR));
                liveEntries.Add(new LiveEntry { id = user.user_id, baseLabel = $"@{user.username}", colorHex = P2P_USER_COLOR, baseLat = (float)user.latitude, baseLon = (float)user.longitude, kind = EntryKind.Person });
            }
        }
    }

    /// <summary>
    /// 보물찾기 — 내가 연 게임 · 참가 중인 게임 (R0926HuntList). 자리는 게임의 공개 설정대로만:
    /// 정확히 공개·주최자는 그 자리, 대략 공개는 원 가운데(원 가장자리가 슬라이더 안이면 넣는다), 비공개는 굵은 거리의 끝으로 줄을 세운다
    /// </summary>
    private void AddHuntData(float lat, float lon)
    {
        foreach (var s in R0926HuntList.Spots)
        {
            var e = new LiveEntry { id = "hunt:" + s.id, baseLabel = s.title, colorHex = R0926HuntList.ColorHex, kind = EntryKind.Hunt, huntId = s.id, huntCoord = s.coord, huntFound = s.found, icon = s.locked ? "lock" : null };
            float d;
            if (s.coord == R0926HuntList.Coord.Hidden)
            {
                if (s.bandFromM >= maxDisplayDistance) continue;
                d = e.fixedDistance = s.bandM;
            }
            else
            {
                e.baseLat = (float)s.lat;
                e.baseLon = (float)s.lon;
                e.approxM = s.coord == R0926HuntList.Coord.Approx ? s.approxM : 0f;
                d = CalculateDistance(lat, lon, e.baseLat, e.baseLon);
                if (d - e.approxM > maxDisplayDistance) continue;
            }
            huntCount++;
            combinedPlaces.Add((s, d, e.id, $"{s.title} - {Mathf.FloorToInt(d)}m", e.colorHex));
            liveEntries.Add(e);
        }
    }

    /// <summary>
    /// 축제 / 보물 칩 — 받아 둔 근처 축제 중 앞의 세 곳(서버가 가깝고 곧 하는 순으로 준다)을 목록 맨 위에, 그 안에서는 가까운 순 (거리 슬라이더와 상관없이).
    /// 줄 앞은 축제 분위기 그림, 거리 칸은 '며칠부터 · 거리'. 누르면 축제 이름으로 찾은 쪽 (R0926PlaceRows).
    /// 관리자가 띄운 광고 줄은 그보다 위에 '광고 · 거리' — 누르면 업체 링크, 축제 수에는 넣지 않는다
    /// </summary>
    private void AddFestivalData(float lat, float lon)
    {
        foreach (var f in R0926Nearby.Festivals)
        {
            if (f == null || string.IsNullOrEmpty(f.title)) continue;
            if (!f.promo && festivalCount >= MaxFestivals) continue;
            string title = f.title.Replace('\r', ' ').Replace('\n', ' ').Trim();
            var e = new LiveEntry
            {
                id = "fest:" + f.id, baseLabel = title, colorHex = R0926Nearby.FestivalHex, baseLat = (float)f.lat, baseLon = (float)f.lon,
                kind = EntryKind.Festival, icon = f.icon, start = f.start, end = f.end, anyDistance = true,
                link = f.promo ? R0926Nearby.PromoLink(f.link, title, f.lat, f.lon) : R0926Nearby.FestivalLink(title),
                pin = (byte)(f.promo ? 3 : 2), promo = f.promo, adId = f.ad_id, emojiUrl = f.emoji_url
            };
            float d = CalculateDistance(lat, lon, e.baseLat, e.baseLon);
            if (!f.promo) festivalCount++;
            combinedPlaces.Add((f, d, e.id, $"{title} - {Mathf.FloorToInt(d)}m", e.colorHex));
            liveEntries.Add(e);
        }
    }

    /// <summary>반려견만 — 가까운 유기동물 보호소 세 곳을 목록 맨 위에 (거리 슬라이더와 상관없이). 누르면 카카오맵 장소 쪽 (전화·길찾기)</summary>
    private void AddShelterData(float lat, float lon)
    {
        foreach (var s in R0926Nearby.Shelters)
        {
            if (shelterCount >= MaxShelters) break;
            if (s == null || string.IsNullOrEmpty(s.name)) continue;
            string name = s.name.Replace('\r', ' ').Replace('\n', ' ').Trim();
            var e = new LiveEntry
            {
                id = "shelter:" + s.id, baseLabel = name, colorHex = R0926Nearby.ShelterHex, baseLat = (float)s.lat, baseLon = (float)s.lon,
                kind = EntryKind.Shelter, icon = "shelter", link = s.url, pin = 1, anyDistance = true
            };
            float d = CalculateDistance(lat, lon, e.baseLat, e.baseLon);
            shelterCount++;
            combinedPlaces.Add((s, d, e.id, $"{name} - {Mathf.FloorToInt(d)}m", e.colorHex));
            liveEntries.Add(e);
        }
    }

    /// <summary>
    /// 축제 / 보물 칩 — 아직 참가하지 않은 근처 보물찾기 세 곳을 축제와 함께 맨 위에 (R0926HuntList.Nearby: 주최자가 정한 범위 안 · 진행 중이거나 곧 시작 · 자리가 남은 것).
    /// 누르면 그 게임 상세에서 보고 참가한다 (10-27 대표님 「보물찾기 참여자가 아니어도 근처 보물찾기·축제가 목록에 나오고, 누르면 정보를 보고 참가」).
    /// 참가한 게임은 칩과 상관없이 AddHuntData 가 넣는다. 자리는 게임의 공개 설정대로만 (AddHuntData 와 같다)
    /// </summary>
    private void AddNearHuntData(float lat, float lon)
    {
        int added = 0;
        foreach (var n in R0926HuntList.Nearby)
        {
            if (added >= MaxNearHunts) break;
            if (n.joined) continue;
            var e = new LiveEntry { id = "hunt:" + n.id, baseLabel = n.title, colorHex = R0926HuntList.ColorHex, kind = EntryKind.Hunt, huntId = n.id, huntCoord = n.coord,
                                    icon = n.locked ? "lock" : null, pin = 2, anyDistance = true };
            float d;
            if (n.coord == R0926HuntList.Coord.Hidden)
            {
                if (n.bandM <= 0f) continue;   // '20km 넘게' — 거리를 말할 수 없다
                d = e.fixedDistance = n.bandM;
            }
            else
            {
                e.baseLat = n.lat;
                e.baseLon = n.lon;
                e.approxM = n.coord == R0926HuntList.Coord.Approx ? n.approxM : 0f;
                d = CalculateDistance(lat, lon, e.baseLat, e.baseLon);
            }
            added++;
            huntCount++;
            combinedPlaces.Add((n, d, e.id, $"{n.title} - {Mathf.FloorToInt(d)}m", e.colorHex));
            liveEntries.Add(e);
        }
    }

    private void AddTransportData<T>(T manager, bool filter, ref int count, float lat, float lon, string colorHex = "00FF00", string hiddenPrefix = null, string icon = null) where T : MonoBehaviour
    {
        if (!filter || manager == null) return;

        var method = manager.GetType().GetMethod("GetPlaceDataMap");
        if (method == null) return;

        var dataMap = method.Invoke(manager, null) as IDictionary;
        if (dataMap == null) return;

        foreach (var val in dataMap.Values) {
            var latProp = val.GetType().GetProperty("latitude");
            var lonProp = val.GetType().GetProperty("longitude");
            var nameProp = val.GetType().GetProperty("name");
            var typeProp = val.GetType().GetProperty("type");

            if (latProp == null || lonProp == null || nameProp == null) continue;

            object rawLat = latProp.GetValue(val);
            object rawLon = lonProp.GetValue(val);
            string pName = (string)nameProp.GetValue(val);
            if (hiddenPrefix != null && HiddenPlaces.IsHidden(hiddenPrefix + pName + "_" + rawLat + "_" + rawLon)) continue;
            float pLat = Convert.ToSingle(rawLat);
            float pLon = Convert.ToSingle(rawLon);
            string pType = typeProp != null ? (string)typeProp.GetValue(val) : "unknown";
            string pId = $"{pType}_{pName}";

            float d = CalculateDistance(lat, lon, pLat, pLon);
            if (d <= maxDisplayDistance) {
                count++;
                combinedPlaces.Add((val, d, pId, $"{pName} - {Mathf.FloorToInt(d)}m", colorHex));
                // 터미널 칩에는 버스터미널과 공항이 함께 있다 — 공항은 비행기 그림
                string pIcon = icon == "bus" && pName != null && (pName.Contains("공항") || pName.IndexOf("airport", StringComparison.OrdinalIgnoreCase) >= 0) ? "airport" : icon;
                liveEntries.Add(new LiveEntry { id = pId, baseLabel = pName, colorHex = colorHex, baseLat = pLat, baseLon = pLon, icon = pIcon });
            }
        }
    }

    private float CalculateDistance(double lat1, double lon1, double lat2, double lon2) => (float)GeoHere.Meters(lat1, lon1, lat2, lon2);

    /// <summary>장소 줄 앞 분류 그림 이름 (r0926_c_<이름> — 업로드 칩·필터 칩과 같은 그림). 분류가 없는 3D 모델 · 춤 모델(anim)은 3d,
    /// 둘 다 아니면 null = 흰 점 (10-27 대표님 「아무런 필터, 분류값이 없는 건 흰색 동그라미」)</summary>
    private static string PlaceIcon(string category, bool model)
    {
        if (category == "anim" || string.IsNullOrEmpty(category) && model) return "3d";
        return string.IsNullOrEmpty(category) ? null : category;
    }

    public void ApplyFilters(Dictionary<string, bool> filters) {
        activeFilters = filters;
        UpdateUI();
    }

    private void OnDistanceSliderChanged(float value) {
        maxDisplayDistance = value;
        PlayerPrefs.SetFloat("MaxDisplayDistance", value);
        UpdateDistanceValueText();
        UpdateUI();

        // Propagate distance filter to all managers
        float lat = 36.6361f; float lon = 126.8280f;
#if UNITY_EDITOR
        // 에디터에서는 VirtualLocation 사용
        if (VirtualLocation.Instance != null) {
            lat = VirtualLocation.Instance.Latitude;
            lon = VirtualLocation.Instance.Longitude;
        }
#else
        // GPS 미초기화/권한거부 시 (0,0) 같은 잘못된 좌표 전파 방지
        if (Input.location.status == LocationServiceStatus.Running) {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }
#endif
        if (dataManager != null) dataManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (tourAPIManager != null) tourAPIManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (terminalManager != null) terminalManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (trainManager != null) trainManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (subwayManager != null) subwayManager.UpdateDistanceFilter(maxDisplayDistance, lat, lon);
        if (p2pManager != null) p2pManager.SetMaxTrackingDistance(maxDisplayDistance);

        // OffScreenIndicator 거리 필터 동기화
        OffScreenIndicator osi = FindFirstObjectByType<OffScreenIndicator>();
        if (osi != null) osi.SetMaxIndicatorDistance(maxDisplayDistance);

        // FilterManager IndicatorOnly 스폰 반경 동기화 — 사용자가 슬라이더로 줄이면 먼 POI도 즉시 제외
        FilterManager filterMgr = UnityEngine.Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null) filterMgr.SetIndicatorRadius(maxDisplayDistance);
    }

    private void UpdateDistanceValueText() {
        if (distanceValueText != null)
            distanceValueText.text = maxDisplayDistance >= 1000f ? $"{(maxDisplayDistance / 1000f):F1}km" : $"{Mathf.RoundToInt(maxDisplayDistance)}m";
    }

    // 캐시된 비교자 — Array.Sort lambda boxing 방지
    private sealed class OrderedComparer : IComparer<(float d, int idx)>
    {
        public static readonly OrderedComparer Instance = new OrderedComparer();
        public int Compare((float d, int idx) a, (float d, int idx) b) => a.d.CompareTo(b.d);
    }
}