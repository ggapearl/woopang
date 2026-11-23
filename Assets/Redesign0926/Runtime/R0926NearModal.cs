using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 근처 카드 — 같은 카드 · 같은 디자인으로 두 가지 (11-15 대표님 (C) · 11-18 J/M1):
///  ① 「발견한 장소」 — 걸어서 4m 안에 들어온 오브젝트 하나만. AR 화면이 뜨고 10초 뒤부터 · 설정 없음(늘 켜짐).
///     닫으면(× · 아래로 밀기) 앱을 끌 때까지 그 장소는 다시 안 뜬다. 한 번 뜨면 5초는 4m 밖이어도 머물고, 그 뒤엔 4.6m 안에 있는 동안.
///  ② 「근처 장소」 — 목록(필터 · 분류 반영) 가까운 순 10곳, 한 번 뜨면 그 10곳 고정. 앱을 껐다 켤 때 2초 뒤 한 번
///     (설정 「앱 시작 시 근처 장소 보기」 · 백그라운드에서 돌아올 땐 안 뜸), 그리고 AR 화면을 아래→위로 밀면 언제든
///     (하늘 오브젝트가 떠 있을 때 · 오브젝트를 만지며 민 손가락 · 창 위에서 민 것은 빼고).
///     목록의 보물찾기도 들지만 여기서 두 번 눌러도 열지 않는다 — 「직접 찾아가서」 안내만 (11-20 대표님 H6 · 열기는 「발견한 장소」에서 · 상자 앞 4m 면 연다).
/// 오브젝트는 전용 카메라가 투명 배경으로 찍어 RawImage 에 (핀치 줌 · 관리자 크기와 상관없이 늘 같은 크기), 그동안 AR 화면에선 빠진다(빈 레이어 28~31).
/// 「근처 장소」에서 AR 에 없거나 25m 밖인 곳은 3D 대신 대표 사진(R0926NearPhoto) — 멀리 흐리게(R0926DistanceFade)가 25m 부터 흐려 3D 가 옅게 찍힌다.
/// 여러 개면 옆으로 넘긴다 (아래 점 · ‹ ›). 두 번 톡톡 = AR 에서 두 번 누른 것과 같다 (장소 정보 · 프로필 · 보물 열기) · 사진 칸은 목록 사진을 누른 것과 같다.
/// 바탕은 물방울메뉴면 물방울 유리(R0926Glass), 기본메뉴면 진한 반투명. 창이 열리면 숨는다.
/// </summary>
public class R0926NearModal : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform card;
    [SerializeField] private CanvasGroup cardGroup;
    [SerializeField] private Button close;
    [SerializeField] private CanvasGroup head;          // 거리 · 제목 — 보는 오브젝트가 바뀌면 살짝 바꿔 끼운다
    [SerializeField] private RectTransform distTag;
    [SerializeField] private Text distText;
    [SerializeField] private Text title;
    [SerializeField] private RectTransform stage;
    [SerializeField] private RawImage[] views;          // 세 칸 — 가운데 · 양옆 미리보기
    [SerializeField] private R0926NearPhoto[] photos;   // 칸마다 사진 (views 와 같은 순서) — 「근처 장소」의 먼 곳
    [SerializeField] private Image shadow;
    [SerializeField] private Button prevButton, nextButton;
    [SerializeField] private RectTransform dots;
    [SerializeField] private Image[] dotImages;
    [SerializeField] private CanvasGroup hintGroup;
    [SerializeField] private RectTransform tapIcon;
    [SerializeField] private Text hint;
    [SerializeField] private GameObject[] covers;       // 이 창이 열려 있으면 숨는다 (물방울 유리가 비침을 끄는 창 · 미리보기 카드)
    [SerializeField] private R0926SkyWeather sky;
    [SerializeField] private RectTransform skyCard;     // 고정한 하늘 카드의 유리 — 그 자리를 비켜 선다
    [SerializeField] private RectTransform[] floor;     // 주소줄 · 도크 — 이 위에
    [SerializeField] private PlaceListManager places;   // 「근처 장소」 — 목록 그대로 (필터 · 분류 · 숨긴 장소 반영)

    private const float Enter = 4f, Leave = 4.6f, Teleport = 10f, MaxRise = 15f, HoldFor = 5f, RearmAfter = 1f, TickEvery = 0.25f;
    private const float FoundAfter = 10f, ResumeGrace = 10f, StartAfter = 2f, StartGiveUp = 20f, RemapEvery = 1f;
    private const int NearbyCount = 10, ParkLayer = 28;
    private const float Solid3D = 25f, Solid3DKeep = 28f;            // 「근처 장소」를 3D 로 찍는 거리 (한 번 3D 면 28m 까지)
    private const float SwipeUpMin = 0.12f, SwipeUpTime = 1f;        // 위로 밀기 — 화면 높이의 12% 를 1초 안에
    private const float PullClose = 220f, PullFling = 1600f, PullFlingMin = 40f, PullSlide = 1800f;   // 아래로 밀어 닫기 (캔버스 단위)
    private const float PhotoFade = 0.25f, InfoWait = 8f, PhotoRetry = 30f;
    private const int PhotoSize = 512, PhotoParallel = 3;
    private static readonly int[] ViewLayers = { 29, 30, 31 };
    private const int NearMask = (1 << 28) | (1 << 29) | (1 << 30) | (1 << 31);
    private const float Fov = 30f, Elevation = 15f, Margin = 1.12f;
    private const int BigSide = 560, SmallSide = 300;
    private const float Spacing = 430f, SideScale = 0.56f, SideAlpha = 0.55f, BobHeight = 27f, BobPeriod = 2.4f;
    private const float FlipDistance = 170f, FlipSpeed = 1400f, DoubleTap = 0.45f, FadeTime = 0.22f, SwapTime = 0.12f, MultiHintFor = 3f;
    // 카드 — 시안(우팡_가까이모달_길안내_시안_1115) 그대로: 폭 368px · 위 22 · 거리 28 · 제목 21px 두 줄 · 무대 250 · 점 7/20 · 안내 14.5 · 아래 20 (× 3.36)
    private const float CardW = 1240f, PadTop = 74f, TagH = 94f, TitleGap = 30f, TitleH = 196f, StageH = 840f, DotsH = 71f, HintH = 74f, PadBottom = 67f;
    private const float DotSize = 24f, DotWide = 67f, DotGap = 24f;
    private const float TagPadL = 34f, TagPin = 47f, TagGap = 16f, TagPadR = 40f, HintIcon = 74f, HintGap = 27f;
    private const float TopReserve = 340f, Gap = 36f, Side = 48f;

    private enum Kind { Place, Person, Hunt }
    private enum Mode { None, Found, Nearby }

    private sealed class Item
    {
        public GameObject obj;               // 「근처 장소」는 AR 에 없으면 null (사진)
        public Kind kind;
        public float dist, since, aimAt;
        public R0926NearHold hold;
        public DoubleTap3D info;
        public Object3DTouchHaptic haptic;
        public P2PUserInfo person;
        public bool host;
        public string name, key;             // key — 「발견한 장소」에서 닫은 것을 기억할 열쇠 (매니저 + 아이디)
        public bool framed, hasDir;
        public Vector3 center, goalCenter, dir;   // center = 맨 위 기준(배율을 뺀) 가운데 — 확대 · 축소와 상관없이 같은 크기로 찍힌다
        public float size, goalSize, offSince = -1f;
        // 「근처 장소」
        public bool listed, solid;
        public int rank;
        public PlaceListManager.ListEntry entry;
        public float mix;                    // 0 = 3D · 1 = 사진
        public string photoKey;              // 주소 · "res:" 앱 그림 · "" 없음 · null 아직 모름 (상세를 받는 중)
        public Texture photo;
        public bool photoOwned, photoBusy;
        public float photoFailedAt = -1f;
        public Color ring;                   // 사람 — 공개 범위 색 테두리
    }

    private sealed class Shot
    {
        public RawImage view;
        public R0926NearPhoto photo;
        public Camera cam;
        public RenderTexture rt;
        public int layer;
        public Item item;
        public bool big, fresh;
    }

    private struct Near { public GameObject obj; public Kind kind; public float dist; public string key; }

    private static readonly System.Comparison<Near> NearOrder = (a, b) => a.dist.CompareTo(b.dist);

    private static readonly HashSet<string> closedFound = new HashSet<string>();   // 「발견한 장소」에서 닫은 것 — 앱을 끌 때까지 다시 안 띄운다
    private static bool startShown;                                                // 이번 실행의 시작 「근처 장소」를 띄웠다 (또는 건너뛰었다)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        closedFound.Clear();
        startShown = false;
#if UNITY_EDITOR
        EditorQuiet = false;
#endif
    }

    private readonly List<Item> items = new List<Item>();
    private readonly List<Near> near = new List<Near>();
    private readonly Dictionary<GameObject, float> dismissed = new Dictionary<GameObject, float>();   // 닫을 때 4m 안에 있던 것 — 4m 밖으로 나간 때 (-1 = 아직 안)
    private readonly List<GameObject> undismiss = new List<GameObject>();
    private readonly List<KeyValuePair<GameObject, float>> redismiss = new List<KeyValuePair<GameObject, float>>();
    private readonly List<Renderer> rends = new List<Renderer>();
    private readonly List<Vector4> spheres = new List<Vector4>();
    private readonly Vector3[] corners = new Vector3[4];
    private readonly int[] slotK = new int[3];
    private Camera[] camBuf = new Camera[8];
    private Shot[] shots;
    private Transform camRoot;
    private Item current;        // 가운데 칸

    private DataManager dataMgr;
    private TourAPIManager tourMgr;
    private SubwayManager subwayMgr;
    private TrainStationManager trainMgr;
    private TerminalManager terminalMgr;
    private StoreManager storeMgr;       // 근처 가게 (소상공인 상가정보 · 11-20 N8a)
    private float nextFind, nextTick, nextStrip;

    private Mode mode;
    private bool closing;                 // 닫는 중 — 카드가 사라지면 비운다
    private float liveAt, resumeAt = -1f, nextStartTry;
    private bool live, away;              // live: AR 화면이 떴다 (liveAt 이 정해짐)

    // 위로 밀기 (AR 화면)
    private Pointer upPointer;
    private Vector2 upFrom;
    private float upAt;
    private bool upLive;

    // 「근처 장소」 — 목록 줄과 AR 오브젝트 짝짓기
    private readonly Dictionary<Target, GameObject> rootOf = new Dictionary<Target, GameObject>();
    private readonly Dictionary<GameObject, Target> targetOf = new Dictionary<GameObject, Target>();
    private readonly HashSet<GameObject> bound = new HashSet<GameObject>();
    private readonly List<PlaceListManager.ListEntry> picks = new List<PlaceListManager.ListEntry>();
    private float nextMap;

    // 사진 · 우팡 장소 상세
    private readonly Dictionary<int, PlaceData> details = new Dictionary<int, PlaceData>();
    private readonly Dictionary<int, float> detailFailed = new Dictionary<int, float>();
    private readonly HashSet<int> detailWant = new HashSet<int>();
    private readonly List<UnityWebRequest> photoRequests = new List<UnityWebRequest>();
    private UnityWebRequest detailRequest;
    private int photoGen;
    private Coroutine infoRoutine;

    private float pos, vel;
    private int target;
    private bool dragging, dragMoved;
    private int dragPointer;
    private float dragStartX, dragStartPos, lastX, lastT, flingV;

    // 아래로 밀어 닫기
    private bool pulling, sliding;
    private int pullPointer;
    private float pull, pullV, pullFrom, pullLastY, pullLastT;

    private bool cardOn, placed;
    private float vis;
    private Vector2 cardPos;
    private float cardScale = 1f;
    private bool multiLayout;

    private Item tapItem;
    private float tapAt = -10f;
    private float multiSince = -10f;
    private int lastCount;
    private Item headItem;
    private float headAlpha = 1f, hintAlpha = 1f;
    private string titleShown, distShown, hintShown;
    private Transform frontView;

    /// <summary>카드가 떠 있다 (나타나는 중 · 사라지는 중 포함) — 아래 길안내 화살표(R0926GuideArrow)는 그동안 숨는다</summary>
    public bool Showing => cardOn;

    /// <summary>길안내 대상을 「발견한 장소」 카드가 담았다 — 안내는 거기서 끝 (R0926GuideArrow). 「근처 장소」 10곳에 든 것은 안내를 이어 간다</summary>
    public bool TookOver(Target t) => mode == Mode.Found && t != null && R0926NearHold.Held.Contains(t);

    private void Awake()
    {
        if (close != null) close.onClick.AddListener(() => Close(false));
        if (prevButton != null) prevButton.onClick.AddListener(() => Go(target - 1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Go(target + 1));
        if (card != null) card.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        Shut();
        infoRoutine = null;
    }

    private void OnDestroy()
    {
        Shut();
        FreePhotos();
        if (camRoot != null) Destroy(camRoot.gameObject);
    }

    // 백그라운드에서 돌아오면 — 시작 「근처 장소」는 안 띄우고, 「발견한 장소」는 AR 이 자리를 다시 잡을 동안(10초) 쉰다
    private void OnApplicationPause(bool paused)
    {
        if (paused) { away = true; return; }
        if (!away) return;   // 앱을 켤 때 한 번 오는 것 — 멈춘 적이 없다
        away = false;
        resumeAt = Time.unscaledTime;
        startShown = true;
        upPointer = null;
    }

    private void Update()
    {
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        if (!live && !BootOverlay.Showing && Camera.main != null) { live = true; liveAt = now; }   // AR 화면이 뜬 때 (시작 화면 뒤)
        if (!Quiet)
        {
            StartNearby(now);
            SwipeUp(now);
        }
        if (now >= nextTick) { nextTick = now + TickEvery; Tick(now); }
        bool want = items.Count > 0 && !closing && !Covered();
        if (want && !cardOn) Open(now);
        if (!cardOn)
        {
            if (mode != Mode.None && (closing || items.Count == 0)) EndMode();
            return;
        }
        vis = Mathf.MoveTowards(vis, want ? 1f : 0f, dt / FadeTime);
        if (!want && vis <= 0f)
        {
            bool end = closing || items.Count == 0;
            Shut();
            if (end) EndMode();
        }
    }

    private void LateUpdate()
    {
        if (!cardOn) return;
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        if (now >= nextStrip) { nextStrip = now + 1f; Strip(); }
        Prune();
        Carousel(dt);
        Draw(now, dt);
        Layout(now, dt);
    }

#if UNITY_EDITOR
    private static bool Quiet => EditorQuiet;
#else
    private static bool Quiet => false;
#endif

    // ── 「근처 장소」 — 시작 2초 뒤 · 위로 밀기 ──

    private void StartNearby(float now)
    {
        if (startShown || !live) return;
        if (!R0926PlaceSettings.NearOnStart) { startShown = true; return; }
        if (now - liveAt < StartAfter || now < nextStartTry) return;
        if (mode == Mode.Nearby) { startShown = true; return; }   // 그새 위로 밀어 띄웠다
        nextStartTry = now + 1f;
        if (OpenNearby(now, false)) startShown = true;
        else if (now - liveAt > StartAfter + StartGiveUp) startShown = true;   // 목록이 끝내 비었다 — 이번 실행은 건너뛴다
    }

    // AR 화면을 아래→위로 — 누른 손가락 하나를 끝까지 따라간다 (R0926KeyboardSwipe 와 같다)
    private void SwipeUp(float now)
    {
        if (upPointer == null || !upPointer.added)
        {
            upPointer = null;
            foreach (var d in InputSystem.devices)
                if (d is Pointer p && d.enabled && p.press.wasPressedThisFrame) { upPointer = p; break; }
            if (upPointer == null) return;
            upFrom = upPointer.position.ReadValue();
            upAt = now;
            upLive = Touches() <= 1 && CanSwipeUp(upFrom);
            return;
        }
        if (!upPointer.press.isPressed) { upPointer = null; return; }
        if (!upLive) return;
        if (Touches() >= 2 || now - upAt > SwipeUpTime) { upLive = false; return; }   // 두 손가락(확대) · 천천히 민 것
        if (!SwipeOk(upPointer.position.ReadValue() - upFrom)) return;
        upLive = false;
        if (!OpenNearby(now, true)) NothingNear();
    }

    private static bool SwipeOk(Vector2 d) => d.y >= Screen.height * SwipeUpMin && d.y > 2f * Mathf.Abs(d.x);

    // 누른 자리가 맨 AR 화면이어야 — 시작 화면 · 덮는 창 · 하늘 오브젝트 · UI · 오브젝트(만지며 민 것)는 빼고. 「근처 장소」가 이미 떠 있어도 빼고
    private static readonly RaycastHit[] upHits = new RaycastHit[16];

    private bool CanSwipeUp(Vector2 at)
    {
        if (BootOverlay.Showing || Covered()) return false;
        if (cardOn && mode == Mode.Nearby && !closing) return false;
        if (!WorldTouch.Free(at)) return false;
        var main = Camera.main;
        if (main == null) return false;
        // 오브젝트 위에서 민 것은 받지 않는다 (돌리기 · 두 번 톡톡) — 카드에 담겨 AR 화면에 안 보이는 것은 빼고 (콜라이더만 그 자리에 남아 있다)
        int n = Physics.RaycastNonAlloc(main.ScreenPointToRay(at), upHits, 2000f, Physics.DefaultRaycastLayers & ~NearMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var hold = upHits[i].collider.GetComponentInParent<R0926NearHold>();
            if (hold == null || hold.Layer < 0) return false;
        }
        return true;
    }

    private void NothingNear()
    {
        var tm = ToastManager.Instance;
        if (tm != null) tm.Show(L("근처에 보여 줄 장소가 없어요", "No places nearby to show", "近くに表示できる場所がありません", "附近没有可显示的地点", "No hay lugares cercanos"));
    }

    // 목록(필터 · 분류 반영) 가까운 순 장소 · 사람 · 보물찾기 10곳 — 광고 줄 · 맨 위 축제 · 보호소 · 찾은 보물은 빼고. 떠 있던 카드(「발견한 장소」 포함)는 바꿔 낀다.
    // 보물찾기는 뜨기만 한다 — 두 번 눌러도 열지 않고 직접 찾아가라고 알린다 (11-20 대표님 H6 · 상자 앞 4m 면 「발견한 장소」처럼 연다)
    private bool OpenNearby(float now, bool haptic)
    {
        if (places == null) places = FindAnyObjectByType<PlaceListManager>(FindObjectsInactive.Include);
        if (places == null) return false;
        if (places.isActiveAndEnabled) places.UpdateUI();   // 목록 창이 닫혀 있으면 줄이 묵어 있다 — 지금 자리로 다시 만든다 (줄은 바로 채워진다)
        picks.Clear();
        var list = places.ShownEntries;
        float far = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e.promo || e.top) continue;
            if (e.kind == PlaceListManager.EntryKind.Hunt)
            {
                if (e.huntId <= 0 || e.distance > far || e.distanceText == R0926HuntList.FoundText) continue;   // 근처 보물찾기는 목록이 거리와 상관없이 맨 위에 붙인다 — 거리 슬라이더 안만
            }
            else if (e.kind != PlaceListManager.EntryKind.Place && e.kind != PlaceListManager.EntryKind.Person) continue;
            picks.Add(e);
        }
        if (picks.Count == 0) return false;
        if (picks.Exists(x => x.kind == PlaceListManager.EntryKind.Hunt))
        {
            var sorted = picks.OrderBy(x => x.distance).ToList();   // 붙여 둔 보물찾기도 가까운 순 자리로 (같은 거리면 목록 순서)
            picks.Clear();
            picks.AddRange(sorted);
        }
        if (picks.Count > NearbyCount) picks.RemoveRange(NearbyCount, picks.Count - NearbyCount);

        foreach (var old in items) Release(old);
        FreePhotos();
        items.Clear();
        FindManagers(now);
        mode = Mode.Nearby;
        closing = sliding = pulling = false;
        pull = pullV = 0f;
        tapItem = null;
        var main = Camera.main;
        Vector3 me = main != null ? main.transform.position : Vector3.zero;
        for (int i = 0; i < picks.Count; i++)
        {
            var e = picks[i];
            bool person = e.kind == PlaceListManager.EntryKind.Person;
            var it = new Item
            {
                kind = person ? Kind.Person : e.kind == PlaceListManager.EntryKind.Hunt ? Kind.Hunt : Kind.Place,
                entry = e,
                listed = true,
                rank = i + 1,
                since = now,
                dist = e.distance,
                name = string.IsNullOrEmpty(e.name) ? null : e.name.StartsWith("@") ? e.name.Substring(1) : e.name,
                ring = ProfileManager.OutlineColor(e.data is NearbyUserData u ? u.visibility_mode : null),
            };
            items.Add(it);
        }
        BindAll();
        nextMap = now + RemapEvery;
        foreach (var it in items)
        {
            it.solid = Solid(it, me);
            it.mix = it.solid ? 0f : 1f;
            it.photoKey = PhotoKeyFor(it.entry);
        }
        target = 0; pos = 0f; vel = 0f;
        dragging = false;
        if (cardOn)
        {
            headItem = null;
            titleShown = distShown = hintShown = null;
            multiSince = items.Count >= 2 ? now : -10f;
            lastCount = items.Count;
            current = At(0);
            RefreshHead();
        }
        if (haptic) Light();
        return true;
    }

    // ── 누가 가까이 있나 (0.25초마다) ──

    private void Tick(float now)
    {
        var main = Camera.main;
        if (main == null) return;
        Vector3 me = main.transform.position;
        FindManagers(now);
        Undismiss(now, me);
        if (mode == Mode.Nearby) TickNearby(now, me);
        else TickFound(now, me);
        if (cardOn) RefreshHead();
    }

    // 「발견한 장소」 — 하나만. 떠나면(4.6m 밖 · 10m 넘게 튐 · 꺼짐) 카드와 같이 사라지고, 닫은 것 · 닫을 때 곁에 있던 것은 건너뛴다
    private void TickFound(float now, Vector3 me)
    {
        if (closing) return;
        if (items.Count > 0)
        {
            var it = items[0];
            if (!Alive(it) || Stale(it)) { closing = true; return; }
            it.dist = Flat(At(it.obj, it.kind), me, out float dy);
            if (Mathf.Abs(dy) > MaxRise || it.dist > Teleport || (now - it.since >= HoldFor && it.dist > Leave)) { closing = true; return; }
            Refresh(it);
            return;
        }
        if (!FoundReady(now)) return;
        near.Clear();
        GatherAll(me);
        near.Sort(NearOrder);
        foreach (var n in near)
        {
            if (n.dist > Enter) break;
            if (dismissed.ContainsKey(n.obj) || closedFound.Contains(n.key) || !Visible(n.obj) || !Anchored(n.obj)) continue;
            items.Add(Make(n, now));
            mode = Mode.Found;
            target = 0; pos = 0f; vel = 0f;
            break;
        }
        near.Clear();
    }

    // AR 화면이 뜨고 10초 뒤부터 (AR 이 자리를 잡는 동안 엉뚱한 것이 뜨지 않게) · 백그라운드에서 돌아온 뒤 10초도
    private bool FoundReady(float now) => !Quiet && live && now - liveAt >= FoundAfter && (resumeAt < 0f || now - resumeAt >= ResumeGrace);

    // 「근처 장소」 — 10곳 고정. 1초마다 AR 오브젝트와 다시 짝짓고(새로 떴거나 · 풀로 돌아갔거나), 3D/사진 · 거리 · 사진 받기
    private void TickNearby(float now, Vector3 me)
    {
        if (now >= nextMap)
        {
            nextMap = now + RemapEvery;
            BindAll();
        }
        bool asked = false, gps = false;
        double la = 0, lo = 0;
        foreach (var it in items)
        {
            if (!closing) it.solid = Solid(it, me);
            if (Live(it) && Anchored(it.obj)) it.dist = Flat(At(it.obj, it.kind), me, out _);
            else
            {
                if (!asked) { asked = true; gps = Here(out la, out lo); }
                var e = it.entry;
                if (gps && (e.lat != 0f || e.lon != 0f)) it.dist = (float)GeoHere.Meters(la, lo, e.lat, e.lon);
            }
            if (it.photoKey == null) it.photoKey = PhotoKeyFor(it.entry);
        }
        PumpDetails();
        PumpPhotos(now);
    }

    private static bool Here(out double lat, out double lon)
    {
        lat = lon = 0;
#if UNITY_EDITOR
        if (VirtualLocation.Instance == null) return false;
#endif
        return GeoHere.TryGet(out lat, out lon, out _);
    }

    private void GatherAll(Vector3 me)
    {
        if (dataMgr != null) Gather(dataMgr.GetSpawnedObjects(), Kind.Place, me, "dm:");
        if (tourMgr != null) Gather(tourMgr.GetSpawnedObjects(), Kind.Place, me, "tour:");
        if (subwayMgr != null) Gather(subwayMgr.GetSpawnedObjects(), Kind.Place, me, "sub:");
        if (trainMgr != null) Gather(trainMgr.GetSpawnedObjects(), Kind.Place, me, "train:");
        if (terminalMgr != null) Gather(terminalMgr.GetSpawnedObjects(), Kind.Place, me, "term:");
        if (storeMgr != null) Gather(storeMgr.GetSpawnedObjects(), Kind.Place, me, "store:");
        Gather(R0926HuntTreasure.SpawnedObjects, Kind.Hunt, me, "hunt:");
        // 사람은 지금 여기 있는 사람만 — 앱을 꺼 둔 지도 친구(마지막 자리 · 11-17 D1)는 AR 에는 보여도 띄우지 않는다
        var p2p = P2PManager.Instance;
        if (p2p != null) Gather(p2p.GetActiveAvatars(), Kind.Person, me, "p2p:", p2p.IsStale);
    }

    private void Gather<TKey>(Dictionary<TKey, GameObject> spawned, Kind kind, Vector3 me, string prefix, System.Func<TKey, bool> skip = null)
    {
        if (spawned == null) return;
        foreach (var kv in spawned)
        {
            var go = kv.Value;
            if (go == null || !go.activeInHierarchy || skip != null && skip(kv.Key)) continue;
            float d = Flat(At(go, kind), me, out float dy);
            if (d > Leave || Mathf.Abs(dy) > MaxRise) continue;
            near.Add(new Near { obj = go, kind = kind, dist = d, key = prefix + kv.Key });
        }
    }

    // 닫을 때 곁에 있던 것 — 4m 밖에서 1초가 지나면(또는 꺼지면) 다시 띄울 수 있다
    private void Undismiss(float now, Vector3 me)
    {
        if (dismissed.Count == 0) return;
        undismiss.Clear();
        redismiss.Clear();
        foreach (var kv in dismissed)
        {
            var go = kv.Key;
            if (go == null || !go.activeInHierarchy) { undismiss.Add(go); continue; }
            bool inside = Flat(At(go), me, out _) <= Enter;
            if (inside) { if (kv.Value >= 0f) redismiss.Add(new KeyValuePair<GameObject, float>(go, -1f)); }
            else if (kv.Value < 0f) redismiss.Add(new KeyValuePair<GameObject, float>(go, now));
            else if (now - kv.Value >= RearmAfter) undismiss.Add(go);
        }
        foreach (var go in undismiss) dismissed.Remove(go);
        foreach (var kv in redismiss) dismissed[kv.Key] = kv.Value;
    }

    // 닫으면 지금 4m 안의 것은 4m 밖으로 나갔다 다시 올 때까지 「발견한 장소」로 띄우지 않는다 (닫자마자 곁의 것이 뜨지 않게)
    private void SnoozeAround(Vector3 me)
    {
        FindManagers(Time.unscaledTime);
        near.Clear();
        GatherAll(me);
        foreach (var n in near) if (n.dist <= Enter) dismissed[n.obj] = -1f;
        near.Clear();
    }

    private static float Flat(Vector3 p, Vector3 me, out float dy)
    {
        dy = p.y - me.y;
        float dx = p.x - me.x, dz = p.z - me.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void FindManagers(float now)
    {
        if (now < nextFind || dataMgr != null && tourMgr != null && subwayMgr != null && trainMgr != null && terminalMgr != null && storeMgr != null) return;
        nextFind = now + 5f;   // 없는 매니저는 5초마다 다시 (DataManager.Instance 는 없으면 에러를 남긴다)
        if (dataMgr == null) dataMgr = FindAnyObjectByType<DataManager>();
        if (tourMgr == null) tourMgr = FindAnyObjectByType<TourAPIManager>();
        if (subwayMgr == null) subwayMgr = FindAnyObjectByType<SubwayManager>();
        if (trainMgr == null) trainMgr = FindAnyObjectByType<TrainStationManager>();
        if (terminalMgr == null) terminalMgr = FindAnyObjectByType<TerminalManager>();
        if (storeMgr == null) storeMgr = FindAnyObjectByType<StoreManager>();
    }

    private Item Make(Near n, float now)
    {
        var go = n.obj;
        var it = new Item { obj = go, kind = n.kind, dist = n.dist, since = now, key = n.key };
        switch (n.kind)
        {
            case Kind.Place:
                it.info = go.GetComponentInChildren<DoubleTap3D>(true);
                it.haptic = go.GetComponentInChildren<Object3DTouchHaptic>(true);
                break;
            case Kind.Person:
                it.person = go.GetComponentInChildren<P2PUserInfo>(true);
                break;
            case Kind.Hunt:
                it.host = R0926HuntTreasure.IsHostTreasure(go);
                break;
        }
        Refresh(it);
        return it;
    }

    // 이름은 띄운 뒤에 오기도 한다 (장소 정보 · 사람 이름) — 「근처 장소」는 목록 줄 이름 그대로
    private static void Refresh(Item it)
    {
        if (it.listed) return;
        if (it.kind == Kind.Place && it.info != null) it.name = it.info.GetName();
        else if (it.kind == Kind.Person && it.person != null) it.name = it.person.username;
    }

    // ── 「근처 장소」 줄 ↔ AR 오브젝트 ──

    // 매니저가 띄운 것의 Target → 뿌리 (목록 줄 누르기와 같은 짝 — PlaceListManager.FindTarget)
    private void MapTargets()
    {
        rootOf.Clear();
        if (targetOf.Count > 600) targetOf.Clear();
        if (dataMgr != null) MapFrom(dataMgr.GetSpawnedObjects());
        if (tourMgr != null) MapFrom(tourMgr.GetSpawnedObjects());
        if (subwayMgr != null) MapFrom(subwayMgr.GetSpawnedObjects());
        if (trainMgr != null) MapFrom(trainMgr.GetSpawnedObjects());
        if (terminalMgr != null) MapFrom(terminalMgr.GetSpawnedObjects());
        if (storeMgr != null) MapFrom(storeMgr.GetSpawnedObjects());
    }

    private void MapFrom<TKey>(Dictionary<TKey, GameObject> spawned)
    {
        if (spawned == null) return;
        foreach (var go in spawned.Values)
        {
            if (go == null || !go.activeInHierarchy) continue;
            if (!targetOf.TryGetValue(go, out var t) || t == null)
            {
                t = go.GetComponentInChildren<Target>(true);
                targetOf[go] = t;
            }
            if (t != null) rootOf[t] = go;
        }
    }

    // 같은 물체는 한 칸에만
    private void BindAll()
    {
        MapTargets();
        bound.Clear();
        foreach (var it in items)
        {
            var go = FindObj(it);
            if (go != null && !bound.Add(go)) go = null;
            Bind(it, go);
        }
    }

    private GameObject FindObj(Item it)
    {
        var e = it.entry;
        if (it.kind == Kind.Hunt)
            return R0926HuntTreasure.SpawnedObjects.TryGetValue("hunt_" + e.huntId, out var chest) && chest != null && chest.activeInHierarchy ? chest : null;
        if (it.kind == Kind.Person)
        {
            var p2p = P2PManager.Instance;
            if (p2p == null || string.IsNullOrEmpty(e.id) || p2p.IsStale(e.id)) return null;
            var avatars = p2p.GetActiveAvatars();
            return avatars != null && avatars.TryGetValue(e.id, out var av) && av != null && av.activeInHierarchy ? av : null;
        }
        if (e.target != null && rootOf.TryGetValue(e.target, out var root) && SameName(e.target, e.name)
            && (string.IsNullOrEmpty(e.target.placeId) || e.target.placeId == e.id)) return root;
        GameObject idOnly = null, nameOnly = null;
        foreach (var kv in rootOf)
        {
            var t = kv.Key;
            if (t == null) continue;
            bool nameOk = SameName(t, e.name);
            if (!string.IsNullOrEmpty(e.id) && t.placeId == e.id)
            {
                if (nameOk) return kv.Value;
                if (idOnly == null) idOnly = kv.Value;
            }
            else if (nameOk && nameOnly == null && string.IsNullOrEmpty(t.placeId)) nameOnly = kv.Value;   // 이름으로만 — 아이디가 없는 것(공공교통)만
        }
        return idOnly != null ? idOnly : nameOnly;
    }

    // 목록 이름과 Target 이름 — 사람은 목록에 '@이름' (PlaceListManager.SameName 과 같다)
    private static bool SameName(Target t, string name)
    {
        string p = t.PlaceName;
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(p)) return false;
        if (p == name) return true;
        return name.Length == p.Length + 1 && name[0] == '@' && string.CompareOrdinal(name, 1, p, 0, p.Length) == 0;
    }

    private void Bind(Item it, GameObject go)
    {
        if (ReferenceEquals(it.obj, go)) return;
        if (it.hold != null) it.hold.Release();
        it.obj = go;
        it.hold = null;
        it.info = null;
        it.haptic = null;
        it.person = null;
        it.framed = it.hasDir = false;
        it.aimAt = 0f;
        if (it.kind == Kind.Hunt) it.host = go != null && R0926HuntTreasure.IsHostTreasure(go);
        if (go == null) return;
        if (it.kind == Kind.Person) it.person = go.GetComponentInChildren<P2PUserInfo>(true);
        else if (it.kind == Kind.Hunt) { }
        else
        {
            it.info = go.GetComponentInChildren<DoubleTap3D>(true);
            it.haptic = go.GetComponentInChildren<Object3DTouchHaptic>(true);
        }
    }

    // 3D 로 찍을까 — AR 에 떠 있고(보이고 · 앵커가 있고) 25m 안 (한 번 3D 면 28m 까지 — 경계에서 깜빡이지 않게)
    private bool Solid(Item it, Vector3 me)
    {
        if (!Live(it) || !Visible(it.obj) || !Anchored(it.obj) || Stale(it)) return false;
        float d = Flat(At(it.obj, it.kind), me, out float dy);
        return d <= (it.solid ? Solid3DKeep : Solid3D) && Mathf.Abs(dy) <= MaxRise;
    }

    private static bool Live(Item it) => it.obj != null && it.obj.activeInHierarchy;

    private bool Alive(Item it) => Live(it) && Visible(it.obj);

    // 사람은 원 자리 — 기기에선 앵커(처음 본 자리) 안에서 원만 옮긴다 (P2PAvatarPlacer · 11-17 D1). 오브젝트 자리로 재면 처음 본 자리 기준이 된다
    private static Vector3 At(GameObject go, Kind kind) => kind == Kind.Person ? At(go) : go.transform.position;
    private static Vector3 At(GameObject go) => go.TryGetComponent(out P2PAvatarPlacer p) ? p.Position : go.transform.position;

    // 띄워 둔 사람이 앱을 꺼 지도 친구(마지막 자리)가 됐다 — 내린다
    private static bool Stale(Item it) => it.kind == Kind.Person && it.person != null && P2PManager.Instance != null && P2PManager.Instance.IsStale(it.person.GetUserId());

    // AR 화면에 보이는 것만 — 필터 · 거리 · 불러오는 중으로 렌더러를 꺼 둔 것은 띄우지 않는다
    private bool Visible(GameObject go)
    {
        go.GetComponentsInChildren(false, rends);
        foreach (var r in rends)
            if (r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer || r is SpriteRenderer)) return true;
        return false;
    }

    // 앵커가 아직 없으면 자리가 원점이다 (OffScreenIndicator 와 같은 셈 — 에디터엔 앵커가 없다)
    private static bool Anchored(GameObject go)
    {
#if UNITY_EDITOR
        return true;
#else
        var a = go.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
        return a == null || a.IsAnchorCreated;
#endif
    }

    private static void Release(Item it)
    {
        if (it != null && it.hold != null) it.hold.Release();
    }

    private bool Covered()
    {
        if (R0926GuideOverlay.BlocksWorld) return true;
        if (sky != null && sky.SkyShowing) return true;   // 하늘을 올려다보는 동안 — 하늘의 판(월드 캔버스)을 화면 카드가 가리지 않게
        if (covers != null)
            foreach (var c in covers)
                if (c != null && c.activeInHierarchy) return true;
        return false;
    }

    // ── 사진 (「근처 장소」의 먼 곳) ──

    // 그 장소의 대표 사진 — 목록 줄 사진(R0926PlaceRows)과 같은 것을 큰 사본(512)으로. "" 없음 · null 아직 모름 (AR 에 없는 우팡 장소 — 상세를 받아야 안다)
    private string PhotoKeyFor(PlaceListManager.ListEntry e)
    {
        switch (e.data)
        {
            case PlaceData p: return R0926PlaceRows.PlacePhoto(p, PhotoSize);
            case CachedPlaceData c:
                if (!int.TryParse(e.id, out int id)) return "";
                if (FullPlace(id, out PlaceData full)) return R0926PlaceRows.PlacePhoto(full, PhotoSize);
                string icon = DataManager.FaceIconPath(c.category, c.filterKey == "publicData");
                if (icon != null) return "res:" + icon;
                if (detailFailed.TryGetValue(id, out float at) && Time.unscaledTime - at < PhotoRetry) return "";
                WantDetail(id);
                return null;
            case TourPlaceData tp: return R0926PlaceRows.PhotoUrl(!string.IsNullOrEmpty(tp.firstimage) ? tp.firstimage : tp.firstimage2, PhotoSize);   // 관광공사 큰 대표 사진
            case FacilityData fd: return R0926PlaceRows.PhotoUrl(fd.main_photo, PhotoSize);
            case NearbyUserData u: return R0926PlaceRows.PhotoUrl(u.avatar_url, PhotoSize);
            case StoreData sd: return string.IsNullOrEmpty(sd.photo) ? "res:" + StoreManager.StickerPath(sd.category) : R0926PlaceRows.PhotoUrl(sd.photo, PhotoSize);   // 사진이 없으면 분류 스티커
        }
        return "";
    }

    private bool FullPlace(int id, out PlaceData p)
    {
        var map = dataMgr != null ? dataMgr.GetPlaceDataMap() : null;
        if (map != null && map.TryGetValue(id, out p)) return true;
        return details.TryGetValue(id, out p);
    }

    private void WantDetail(int id)
    {
        if (!details.ContainsKey(id)) detailWant.Add(id);
    }

    private void PumpDetails()
    {
        if (detailRequest != null || detailWant.Count == 0) return;
        var ids = new List<int>(detailWant);
        detailWant.Clear();
        UnityWebRequest req = null;
        try
        {
            req = UnityWebRequest.Get(ApiConfig.LOCATIONS_DETAIL + "?ids=" + string.Join(",", ids));
            req.timeout = 15;
            detailRequest = req;
            int gen = photoGen;
            var q = req;
            req.SendWebRequest().completed += _ => DetailDone(q, ids, gen);
        }
        catch (System.Exception)
        {
            if (req != null) req.Dispose();
            detailRequest = null;
            float now = Time.unscaledTime;
            foreach (int id in ids) detailFailed[id] = now;
        }
    }

    private void DetailDone(UnityWebRequest req, List<int> ids, int gen)
    {
        List<PlaceData> got = null;
        if (req.result == UnityWebRequest.Result.Success)
        {
            try { got = JsonConvert.DeserializeObject<List<PlaceData>>(req.downloadHandler.text); }
            catch (System.Exception) { got = null; }
        }
        req.Dispose();
        if (this == null) return;
        if (detailRequest == req) detailRequest = null;
        if (got != null)
        {
            if (details.Count > 200) details.Clear();
            foreach (var p in got) if (p != null) details[p.id] = p;
        }
        if (gen != photoGen) return;   // 카드를 닫으며 그만둔 것 — 받지 못한 것으로 치지 않는다
        float now = Time.unscaledTime;
        foreach (int id in ids) if (!details.ContainsKey(id)) detailFailed[id] = now;
    }

    // 가운데 칸에서 가까운 것부터 세 장씩 — 10곳 모두 미리 받아 둔다 (멀어져 사진으로 바뀔 때 빈 칸이 보이지 않게)
    private void PumpPhotos(float now)
    {
        for (int o = 0; o < items.Count && photoRequests.Count < PhotoParallel; o++)
            for (int s = 0; s < 2 && photoRequests.Count < PhotoParallel; s++)
            {
                if (o == 0 && s == 1) continue;
                int k = s == 0 ? target + o : target - o;
                if (k < 0 || k >= items.Count) continue;
                var it = items[k];
                if (it.photo != null || it.photoBusy || string.IsNullOrEmpty(it.photoKey)) continue;
                if (it.photoFailedAt >= 0f && now - it.photoFailedAt < PhotoRetry) continue;
                RequestPhoto(it);
            }
    }

    private void RequestPhoto(Item it)
    {
        string key = it.photoKey;
        if (key.StartsWith("res:"))
        {
            var t = Resources.Load<Texture2D>(key.Substring(4));   // 앱 그림 (공공데이터 분류) — 함께 쓰는 에셋이라 지우지 않는다
            if (t != null) { it.photo = t; it.photoOwned = false; }
            else it.photoFailedAt = Time.unscaledTime;
            return;
        }
        UnityWebRequest req = null;
        try
        {
            req = UnityWebRequestTexture.GetTexture(key, true);   // 읽기 전용 — CPU 쪽 사본을 남기지 않는다
            req.timeout = 15;
            photoRequests.Add(req);
            it.photoBusy = true;
            int gen = photoGen;
            var q = req;
            req.SendWebRequest().completed += _ => PhotoDone(it, key, q, gen);
        }
        catch (System.Exception)
        {
            if (req != null) { photoRequests.Remove(req); req.Dispose(); }
            it.photoBusy = false;
            it.photoFailedAt = Time.unscaledTime;
        }
    }

    private void PhotoDone(Item it, string key, UnityWebRequest req, int gen)
    {
        photoRequests.Remove(req);
        Texture2D tex = null;
        if (req.result == UnityWebRequest.Result.Success)
        {
            try { tex = DownloadHandlerTexture.GetContent(req); }
            catch (System.Exception) { tex = null; }
        }
        req.Dispose();
        if (this == null || gen != photoGen || it.photoKey != key) { if (tex != null) Destroy(tex); return; }   // 카드를 닫았다
        it.photoBusy = false;
        if (tex == null) { it.photoFailedAt = Time.unscaledTime; return; }
        tex.wrapMode = TextureWrapMode.Clamp;
        if (it.photoOwned && it.photo != null) Destroy(it.photo);
        it.photo = tex;
        it.photoOwned = true;
    }

    // 카드를 닫으면 받은 사진을 모두 놓는다
    private void FreePhotos()
    {
        photoGen++;
        if (photoRequests.Count > 0)
        {
            var reqs = photoRequests.ToArray();
            photoRequests.Clear();
            foreach (var q in reqs) q.Abort();
        }
        if (detailRequest != null) { var d = detailRequest; detailRequest = null; d.Abort(); }
        detailWant.Clear();
        if (shots != null) foreach (var sh in shots) if (sh.photo != null) sh.photo.Hide();
        foreach (var it in items)
        {
            if (it.photoOwned && it.photo != null) Destroy(it.photo);
            it.photo = null;
            it.photoOwned = it.photoBusy = false;
        }
    }

    // 사진 칸을 두 번 — 목록 사진을 누른 것과 같다: 사람은 프로필, 장소는 AR 에 떠 있으면 그 창 · 없으면 대리 오브젝트에 채워 (R0926PlaceRows.OpenPlaceInfo)
    private void OpenListed(Item it)
    {
        var e = it.entry;
        if (it.kind == Kind.Person)
        {
            var pm = ProfileManager.Instance;
            if (pm != null && !string.IsNullOrEmpty(e.id)) pm.ShowProfile(e.id);
            else InfoFailed();
            return;
        }
        if (infoRoutine != null) return;   // 여는 중
        infoRoutine = StartCoroutine(OpenInfo(e));
    }

    private IEnumerator OpenInfo(PlaceListManager.ListEntry e)
    {
        DoubleTap3D dt = R0926PlaceInfo.Spawned(places, e);
        bool ok = dt != null && dt.OpenInfo();
        if (!ok)
        {
            object data = e.data;
            if (data is CachedPlaceData && int.TryParse(e.id, out int id))
            {
                float tapped = Time.unscaledTime;
                detailFailed.Remove(id);
                PlaceData full;
                while (!FullPlace(id, out full) && Time.unscaledTime - tapped < InfoWait)
                {
                    WantDetail(id);
                    PumpDetails();
                    if (detailFailed.TryGetValue(id, out float at) && at >= tapped) break;   // 받지 못했다
                    yield return null;
                }
                data = full;
                dt = R0926PlaceInfo.Spawned(places, e);   // 기다리는 사이 AR 에 떴을 수 있다
                ok = dt != null && dt.OpenInfo();
            }
            if (!ok && data != null)
            {
                R0926PlaceInfo.Mark(e.noDirection ? 0f : e.lat, e.noDirection ? 0f : e.lon, e.name);   // 상세 위 「길안내」가 이 자리로
                dt = R0926PlaceInfo.Host();
                float until = Time.unscaledTime + 2f;
                while (dt != null && !dt.IsReady && Time.unscaledTime < until) yield return null;   // 처음 만든 대리 오브젝트는 한 프레임 뒤에 준비된다
                ok = dt != null && dt.IsReady && R0926PlaceInfo.Fill(dt, data) && dt.OpenInfo();
            }
        }
        infoRoutine = null;
        if (!ok) InfoFailed();
    }

    private static void InfoFailed()
    {
        var tm = ToastManager.Instance;
        if (tm != null) tm.ShowWarning(L("정보를 불러오지 못했어요", "Couldn't load the info", "情報を読み込めませんでした", "无法加载信息", "No se pudo cargar la información"));
    }

    // ── 보이기 · 숨기기 ──

    private void Open(float now)
    {
        Build();
        if (shots == null || card == null) return;
        cardOn = true;
        placed = false;
        vis = 0f;
        headItem = null;
        titleShown = distShown = hintShown = null;
        headAlpha = hintAlpha = 1f;
        multiSince = items.Count >= 2 ? now : -10f;
        lastCount = items.Count;
        target = Mathf.Clamp(target, 0, items.Count - 1);
        pos = target; vel = 0f;
        dragging = pulling = sliding = false;
        pull = pullV = 0f;
        card.gameObject.SetActive(true);
        Strip();
        nextStrip = now + 1f;
        current = At(target);
        RefreshHead();
    }

    private void Shut()
    {
        if (shots != null) foreach (var sh in shots) Free(sh);
        foreach (var it in items) Release(it);
        if (card != null && card.gameObject.activeSelf) card.gameObject.SetActive(false);
        cardOn = false;
        current = null;
        headItem = null;
        frontView = null;
        dragging = pulling = false;
        vis = 0f;
    }

    // 카드가 사라졌다 — 담았던 것 · 사진을 놓고 처음으로
    private void EndMode()
    {
        foreach (var it in items) Release(it);
        FreePhotos();
        items.Clear();
        mode = Mode.None;
        closing = sliding = pulling = false;
        pull = pullV = 0f;
        target = 0; pos = 0f; vel = 0f;
        tapItem = null;
        current = null;
        headItem = null;
    }

    private void Build()
    {
        if (shots != null || views == null) return;
        camRoot = new GameObject("R0926NearCams").transform;   // 화면 UI 밖 — 카메라는 월드에서 오브젝트 앞에 선다
        shots = new Shot[Mathf.Min(views.Length, ViewLayers.Length)];
        for (int i = 0; i < shots.Length; i++)
        {
            var go = new GameObject("NearCam" + i);
            go.transform.SetParent(camRoot, false);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // 투명 — 카드 유리가 비친다
            cam.fieldOfView = Fov;
            cam.allowHDR = false;                               // HDR 판은 알파가 없다
            cam.useOcclusionCulling = false;
            cam.cullingMask = 1 << ViewLayers[i];
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.antialiasing = AntialiasingMode.None;
            var photo = photos != null && i < photos.Length ? photos[i] : null;
            shots[i] = new Shot { view = views[i], photo = photo, cam = cam, layer = ViewLayers[i] };
            if (views[i] != null) views[i].enabled = false;
            if (photo != null) photo.Hide();
        }
    }

    private static void Free(Shot sh)
    {
        if (sh.cam != null) { sh.cam.enabled = false; sh.cam.targetTexture = null; }
        if (sh.rt != null) { RenderTexture.ReleaseTemporary(sh.rt); sh.rt = null; }
        if (sh.view != null) { sh.view.texture = null; sh.view.enabled = false; }
        if (sh.photo != null) sh.photo.Hide();
        sh.item = null;
    }

    // 화면에 그리는 카메라(AR 카메라 등)에서 빈 레이어 28~31 을 뺀다 — 카드에 담은 물체가 AR 화면에서 빠진다.
    // 판에 찍는 카메라(보물 축하 · 미리보기)는 그대로 — 축하 화면이 데려간 보물은 계속 찍힌다
    private void Strip()
    {
        int n = Camera.allCamerasCount;
        if (camBuf.Length < n) camBuf = new Camera[n + 4];
        n = Camera.GetAllCameras(camBuf);
        for (int i = 0; i < n; i++)
        {
            var c = camBuf[i];
            if (c == null || c.targetTexture != null || camRoot != null && c.transform.parent == camRoot) continue;
            if ((c.cullingMask & NearMask) != 0) c.cullingMask &= ~NearMask;
        }
        System.Array.Clear(camBuf, 0, n);
    }

    private Item At(int k) => k >= 0 && k < items.Count ? items[k] : null;

    // 꺼진 물체(풀로 돌아감) — 「발견한 장소」는 바로 빼고, 「근처 장소」는 사진으로 (걸린 레이어는 R0926NearHold 가 꺼질 때 되돌렸다)
    private void Prune()
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            if (Live(it)) continue;
            if (it.listed)
            {
                if (!ReferenceEquals(it.obj, null)) { Bind(it, null); it.solid = false; }
                continue;
            }
            Release(it);
            items.RemoveAt(i);
            if (tapItem == it) tapItem = null;
        }
    }

    private void Carousel(float dt)
    {
        int count = items.Count;
        if (count == 0) { current = null; return; }
        target = Mathf.Clamp(target, 0, count - 1);
        if (!dragging)
        {
            pos = Mathf.SmoothDamp(pos, target, ref vel, 0.16f, Mathf.Infinity, dt);
            if (Mathf.Abs(pos - target) < 0.0005f) { pos = target; vel = 0f; }
        }
        current = At(target);
    }

    // ── 칸마다 찍기 ──

    private void Draw(float now, float dt)
    {
        var main = Camera.main;
        Vector3 me = main != null ? main.transform.position : Vector3.zero;
        int count = items.Count;
        for (int s = 0; s < slotK.Length; s++) slotK[s] = -1;
        if (shots == null) return;
        for (int k = Mathf.Max(0, Mathf.CeilToInt(pos - 1.5f)); k < count; k++)
        {
            float d = k - pos;
            if (d >= 1.5f) break;
            if (d > -1.5f) slotK[k % shots.Length] = k;
        }

        // 3D 인지 사진인지 (「근처 장소」 — 0.25초 동안 바꿔 낀다). 3D 로 든 것은 보이는 칸이면 그 칸 레이어, 아니면 아무도 안 찍는 레이어 (둘 다 AR 화면엔 없다)
        for (int k = 0; k < count; k++)
        {
            var it = items[k];
            if (it.listed) it.mix = Mathf.MoveTowards(it.mix, it.solid ? 0f : 1f, dt / PhotoFade);
            bool use3D = Live(it) && (!it.listed || it.solid || it.mix < 1f);
            if (!use3D)
            {
                if (it.hold != null && it.hold.Layer >= 0) it.hold.Release();   // AR 로 돌려보낸다
                continue;
            }
            if (it.hold == null && !it.obj.TryGetComponent(out it.hold)) it.hold = it.obj.AddComponent<R0926NearHold>();
            int layer = slotK[k % shots.Length] == k ? ViewLayers[k % shots.Length] : ParkLayer;
            if (it.hold.Layer != layer) it.hold.Take(layer);
            else if (Time.frameCount % 15 == 0) it.hold.Scan();   // 받는 중이던 3D 모델
        }

        float best = 9f;
        Transform front = null;
        for (int s = 0; s < shots.Length; s++)
        {
            var sh = shots[s];
            if (sh.view == null || sh.cam == null) continue;
            int k = slotK[s];
            var it = k >= 0 ? At(k) : null;
            if (it == null)
            {
                if (sh.view.enabled) sh.view.enabled = false;
                sh.cam.enabled = false;
                if (sh.photo != null) sh.photo.Hide();
                sh.item = null;
                continue;
            }
            if (sh.item != it) { sh.item = it; sh.fresh = true; }
            float d = k - pos, ad = Mathf.Abs(d);
            bool big = ad < 0.5f;
            float sc = Mathf.Lerp(1f, SideScale, Mathf.Clamp01(ad));
            float a = ad <= 1f ? Mathf.Lerp(1f, SideAlpha, ad) : Mathf.Lerp(SideAlpha, 0f, (ad - 1f) / 0.5f);
            float m = it.listed ? it.mix : 0f;
            bool use3D = Live(it) && it.hold != null && it.hold.Layer >= 0 && m < 1f;
            float bob = (1f - Mathf.Cos(now * Mathf.PI * 2f / BobPeriod)) * 0.5f * BobHeight * (1f - Mathf.Clamp01(ad * 2f)) * (1f - m);
            var vr = sh.view.rectTransform;
            vr.anchoredPosition = new Vector2(d * Spacing, bob);
            vr.localScale = new Vector3(sc, sc, 1f);
            if (use3D)
            {
                Target(sh, big);
                sh.view.color = new Color(1f, 1f, 1f, a * (1f - m));
                bool render = sh.fresh || big || (Time.frameCount + s) % 3 == 0;   // 옆 미리보기는 세 프레임에 한 번
                if (render && Aim(sh, it, me, now)) sh.fresh = false;
                else if (render) render = false;
                sh.cam.enabled = render;
                if (!sh.view.enabled && it.framed) sh.view.enabled = true;
            }
            else
            {
                if (sh.view.enabled) sh.view.enabled = false;
                sh.cam.enabled = false;
                sh.fresh = true;
            }
            if (sh.photo != null)
            {
                if (m > 0f) sh.photo.Show(a * m, it.kind == Kind.Person, it.photo, it.ring, it.kind == Kind.Hunt);
                else sh.photo.Hide();
            }
            if (ad < best) { best = ad; front = vr; }
        }
        if (front != null && front != frontView) { front.SetAsLastSibling(); frontView = front; }   // 가운데 칸이 맨 앞
        if (shadow != null)
        {
            var c = shadow.color;
            float sa = 0.25f * (1f - Mathf.Clamp01(best));
            if (!Mathf.Approximately(c.a, sa)) { c.a = sa; shadow.color = c; }
        }
    }

    // 칸 크기에 맞춘 판 — 가운데는 크게(560 · MSAA 4), 옆은 작게(300 · MSAA 2). 자리가 바뀌면 판을 바꿔 끼우고 그 프레임에 바로 찍는다
    private void Target(Shot sh, bool big)
    {
        if (sh.rt != null && sh.big == big) return;
        var size = sh.view.rectTransform.rect.size;
        float k = (big ? BigSide : SmallSide) / Mathf.Max(1f, Mathf.Max(size.x, size.y));
        int w = Mathf.Max(16, Mathf.RoundToInt(size.x * k)), h = Mathf.Max(16, Mathf.RoundToInt(size.y * k));
        sh.cam.targetTexture = null;
        if (sh.rt != null) RenderTexture.ReleaseTemporary(sh.rt);
        sh.rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, big ? 4 : 2);
        sh.rt.name = big ? "R0926NearBig" : "R0926NearSmall";
        sh.big = big;
        sh.cam.targetTexture = sh.rt;
        sh.cam.aspect = (float)w / h;
        sh.view.texture = sh.rt;
        sh.fresh = true;
    }

    // 물체를 칸에 꼭 맞게 — 구(렌더러마다 감싸는 구)를 다 품는 크기를 맨 위 배율로 나눠 둔다. 0.6초 넘게 달라져야(모델이 바뀜) 따라간다 — 누를 때 커지는 건 그대로 보인다
    private bool Aim(Shot sh, Item it, Vector3 me, float now)
    {
        var root = it.obj.transform;
        float dt = it.aimAt > 0f ? Mathf.Clamp(now - it.aimAt, 0f, 0.5f) : 0f;
        it.aimAt = now;
        var ls = root.lossyScale;
        float rs = Mathf.Max(1e-4f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z))));
        if (Measure(it.obj, out var wc, out float wr))
        {
            Vector3 lc = root.InverseTransformPoint(wc);
            float sz = wr / rs;
            if (!it.framed)
            {
                it.center = it.goalCenter = lc;
                it.size = it.goalSize = sz;
                it.framed = true;
                it.offSince = -1f;
            }
            else
            {
                bool off = Mathf.Abs(sz - it.goalSize) > it.goalSize * 0.08f || (lc - it.goalCenter).magnitude > it.goalSize * 0.15f;
                if (!off) it.offSince = -1f;
                else if (it.offSince < 0f) it.offSince = now;
                else if (now - it.offSince > 0.6f) { it.goalCenter = lc; it.goalSize = sz; it.offSince = -1f; }
            }
        }
        if (!it.framed) return false;
        float f = 1f - Mathf.Exp(-5f * dt);
        it.center = Vector3.Lerp(it.center, it.goalCenter, f);
        it.size = Mathf.Lerp(it.size, it.goalSize, f);

        Vector3 c = root.TransformPoint(it.center);
        float r = Mathf.Max(0.01f, it.size * rs);
        Vector3 to = me - c;
        to.y = 0f;
        if (to.sqrMagnitude > 0.0625f)
        {
            to.Normalize();
            it.dir = it.hasDir ? Vector3.Slerp(it.dir, to, 1f - Mathf.Exp(-4f * dt)) : to;
            it.hasDir = true;
        }
        else if (!it.hasDir)
        {
            var main = Camera.main;
            Vector3 b = main != null ? -main.transform.forward : Vector3.back;
            b.y = 0f;
            it.dir = b.sqrMagnitude > 1e-4f ? b.normalized : Vector3.back;
            it.hasDir = true;
        }
        float vHalf = Fov * 0.5f * Mathf.Deg2Rad;
        float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * sh.cam.aspect);
        float dist = r / Mathf.Sin(Mathf.Min(vHalf, hHalf)) * Margin;
        float e = Elevation * Mathf.Deg2Rad;
        Vector3 eye = c + (it.dir * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e)) * dist;
        var t = sh.cam.transform;
        t.SetPositionAndRotation(eye, Quaternion.LookRotation(c - eye, Vector3.up));
        sh.cam.nearClipPlane = Mathf.Max(0.01f, dist - r * 1.6f);
        sh.cam.farClipPlane = dist + r * 1.6f + 0.1f;
        return true;
    }

    private bool Measure(GameObject go, out Vector3 center, out float radius)
    {
        go.GetComponentsInChildren(false, rends);
        spheres.Clear();
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
        foreach (var r in rends)
        {
            if (!r.enabled) continue;
            Vector3 c;
            float rad;
            if (r is SkinnedMeshRenderer)
            {
                var b = r.bounds;
                c = b.center;
                rad = b.extents.magnitude;
            }
            else if (r is MeshRenderer || r is SpriteRenderer)
            {
                var lb = r.localBounds;
                var t = r.transform;
                var s = t.lossyScale;
                c = t.TransformPoint(lb.center);
                rad = new Vector3(lb.extents.x * Mathf.Abs(s.x), lb.extents.y * Mathf.Abs(s.y), lb.extents.z * Mathf.Abs(s.z)).magnitude;
            }
            else continue;   // 반짝이(파티클) · 선은 크기에서 뺀다
            if (!(rad > 0f) || float.IsInfinity(rad)) continue;
            spheres.Add(new Vector4(c.x, c.y, c.z, rad));
            var e = new Vector3(rad, rad, rad);
            min = Vector3.Min(min, c - e);
            max = Vector3.Max(max, c + e);
        }
        if (spheres.Count == 0) { center = default; radius = 0f; return false; }
        center = (min + max) * 0.5f;
        radius = 0f;
        foreach (var s in spheres) radius = Mathf.Max(radius, Vector3.Distance(center, s) + s.w);
        return true;
    }

    // ── 카드 ──

    private void Layout(float now, float dt)
    {
        int count = items.Count;
        bool multi = count >= 2;
        if (multi && lastCount < 2) multiSince = now;
        lastCount = count;

        // 거리 · 제목 — 가운데 칸이 바뀌면 잠깐 사라졌다가 새 글로
        if (current != headItem && current != null)
        {
            if (headItem == null) { headItem = current; RefreshHead(); headAlpha = 1f; }
            else
            {
                headAlpha = Mathf.MoveTowards(headAlpha, 0f, dt / SwapTime);
                if (headAlpha <= 0f) { headItem = current; RefreshHead(); }
            }
        }
        else headAlpha = Mathf.MoveTowards(headAlpha, 1f, dt / SwapTime);
        if (head != null) head.alpha = headAlpha;

        // 안내 — 여러 개면 처음 3초는 넘기는 법, 그 뒤엔 가운데 것을 누르는 법
        bool swipe = multi && now - multiSince < MultiHintFor;
        string hw = swipe ? SwipeHint() : HintFor(headItem ?? current);
        if (hw != hintShown)
        {
            if (hintShown == null) { SetHint(hw, !swipe); hintAlpha = 1f; }
            else
            {
                hintAlpha = Mathf.MoveTowards(hintAlpha, 0f, dt / SwapTime);
                if (hintAlpha <= 0f) SetHint(hw, !swipe);
            }
        }
        else hintAlpha = Mathf.MoveTowards(hintAlpha, 1f, dt / SwapTime);
        if (hintGroup != null) hintGroup.alpha = hintAlpha;

        if (multi != multiLayout || !placed) Arrange(multi);
        Dots(multi, count);
        if (prevButton != null) Show(prevButton.gameObject, multi && target > 0);
        if (nextButton != null) Show(nextButton.gameObject, multi && target < count - 1);

        // 아래로 밀기 — 손을 떼면 제자리로, 닫으면 뗀 속도 그대로 내려가며 사라진다
        if (sliding) pull += Mathf.Min(pullV, -PullSlide) * dt;
        else if (!pulling && pull != 0f)
        {
            pull = Mathf.Lerp(pull, 0f, 1f - Mathf.Exp(-14f * dt));
            if (Mathf.Abs(pull) < 0.5f) pull = 0f;
        }

        Place(dt);
        float e = vis * vis * (3f - 2f * vis);
        float drop = pull < 0f ? Mathf.Clamp01(1f + pull / (PullClose * 2.5f)) : 1f;
        if (cardGroup != null)
        {
            cardGroup.alpha = e * drop;
            bool live = vis > 0.5f && count > 0 && !closing;
            cardGroup.blocksRaycasts = live;
            cardGroup.interactable = live;
        }
        float pop = cardScale * Mathf.Lerp(0.94f, 1f, e);
        card.localScale = new Vector3(pop, pop, 1f);
    }

    private static void Show(GameObject go, bool on)
    {
        if (go.activeSelf != on) go.SetActive(on);
    }

    // 세로 배치 — 위에서부터 거리 · 제목 · 무대 · (점) · 안내. 여러 개일 때만 점 줄이 생긴다
    private void Arrange(bool multi)
    {
        multiLayout = multi;
        float y = PadTop + TagH + TitleGap + TitleH + StageH;
        if (dots != null) dots.anchoredPosition = new Vector2(0f, -(y + DotsH * 0.5f));
        if (multi) y += DotsH;
        if (hintGroup != null) ((RectTransform)hintGroup.transform).anchoredPosition = new Vector2(0f, -(y + HintH * 0.5f));
        y += HintH + PadBottom;
        card.sizeDelta = new Vector2(CardW, y);
        if (dots != null) Show(dots.gameObject, multi);
    }

    private void Dots(bool multi, int count)
    {
        if (!multi || dotImages == null) return;
        int n = Mathf.Min(count, dotImages.Length);
        float total = DotGap * (n - 1);
        for (int i = 0; i < n; i++) total += Mathf.Lerp(DotSize, DotWide, 1f - Mathf.Clamp01(Mathf.Abs(i - pos)));
        float x = -total * 0.5f;
        for (int i = 0; i < dotImages.Length; i++)
        {
            var img = dotImages[i];
            if (img == null) continue;
            bool on = i < n;
            Show(img.gameObject, on);
            if (!on) continue;
            float f = 1f - Mathf.Clamp01(Mathf.Abs(i - pos));
            float w = Mathf.Lerp(DotSize, DotWide, f);
            img.rectTransform.sizeDelta = new Vector2(w, DotSize);
            img.rectTransform.anchoredPosition = new Vector2(x + w * 0.5f, 0f);
            img.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 1f, f));
            x += w + DotGap;
        }
    }

    // 자리 — 노치 아래 줄과 주소줄 · 도크 사이(고정한 하늘 카드가 있으면 그 자리도 비켜서) 가운데. 모자라면(가로 화면) 줄인다. 아래로 민 만큼 내려간다
    private void Place(float dt)
    {
        var host = (RectTransform)transform;
        var area = host.rect;
        float k = area.height / Mathf.Max(1f, Screen.height);
        var sa = Screen.safeArea;
        float top = area.yMax - (Screen.height - sa.yMax) * k - TopReserve;
        float bottom = area.yMin + sa.yMin * k;
        if (floor != null)
            foreach (var f in floor)
            {
                if (f == null || !f.gameObject.activeInHierarchy) continue;
                f.GetWorldCorners(corners);
                bottom = Mathf.Max(bottom, host.InverseTransformPoint(corners[1]).y);
            }
        if (sky != null && sky.Pinned && skyCard != null && skyCard.gameObject.activeInHierarchy)
        {
            skyCard.GetWorldCorners(corners);
            float lo = host.InverseTransformPoint(corners[0]).y, hi = host.InverseTransformPoint(corners[1]).y;
            if (sky.PinnedTop) top = Mathf.Min(top, lo);
            else bottom = Mathf.Max(bottom, hi);
        }
        top -= Gap;
        bottom += Gap;
        float h = card.sizeDelta.y;
        float avail = Mathf.Max(h * 0.4f, top - bottom);
        float scale = Mathf.Min(1f, avail / h, (area.width - Side * 2f) / CardW);
        var goal = new Vector2(0f, (top + bottom) * 0.5f - area.center.y);
        if (!placed) { cardPos = goal; cardScale = scale; placed = true; }
        else
        {
            float f = 1f - Mathf.Exp(-10f * dt);
            cardPos = Vector2.Lerp(cardPos, goal, f);
            cardScale = Mathf.Lerp(cardScale, scale, f);
        }
        card.anchoredPosition = cardPos + new Vector2(0f, pull);
    }

    // ── 글 ──

    private void RefreshHead()
    {
        var it = headItem ?? current;
        if (it == null) return;
        string t = TitleFor(it);
        if (t != titleShown && title != null)
        {
            titleShown = t;
            if (HasEmoji(it)) R0926EmojiText.Set(title, t);
            else title.text = t;
        }
        string kind = mode == Mode.Nearby ? L("근처 장소", "Nearby", "近くの場所", "附近地点", "Cerca")
                                          : L("발견한 장소", "Found", "見つけた場所", "发现的地点", "Encontrado");
        string m = it.kind == Kind.Hunt && it.listed && !Live(it) && !string.IsNullOrEmpty(it.entry.distanceText) ? it.entry.distanceText : Meters(it.dist);
        string d = kind + " · " + m;
        if (d != distShown && distText != null)
        {
            distShown = d;
            distText.text = d;
            if (distTag != null) distTag.sizeDelta = new Vector2(TagPadL + TagPin + TagGap + distText.preferredWidth + TagPadR, TagH);
        }
    }

    private static string Meters(float m)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (m < 10f) return m.ToString("0.0", inv) + "m";
        if (m < 1000f) return m.ToString("0", inv) + "m";
        return (m / 1000f).ToString("0.0", inv) + "km";
    }

    private static bool HasEmoji(Item it) => !string.IsNullOrEmpty(it.name) && R0926Emoji.ToDisplay(it.name) != it.name;

    private static string TitleFor(Item it)
    {
        bool plain = HasEmoji(it);   // 이모지 이름은 그림이 글자 자리를 따라가야 해 색 표시를 넣지 않는다
        string n;
        if (it.kind == Kind.Hunt) n = L("보물상자", "the treasure chest", "宝箱", "宝箱", "el cofre del tesoro");
        else
        {
            n = it.name;
            if (string.IsNullOrEmpty(n))
                n = it.kind == Kind.Person ? L("주변 사람", "someone nearby", "近くの人", "附近的人", "alguien cerca")
                                           : L("이 장소", "this place", "この場所", "这个地方", "este lugar");
            n = n.Trim().Replace('<', '‹').Replace('>', '›').Replace('\n', ' ');
            if (n.Length > 16) n = n.Substring(0, 16) + "…";
        }
        string c = plain ? n : "<color=#FFC2D4>" + n + "</color>";
        bool person = it.kind == Kind.Person && !string.IsNullOrEmpty(it.name);
        if (it.listed)
        {
            int r = it.rank;
            return L((person ? c + "님" : c) + "\n가까운 순 " + r + "번째",
                     c + "\n#" + r + " nearest",
                     (person ? c + "さん" : c) + "\n近い順 " + r + "番目",
                     c + "\n距离第" + r + "近",
                     c + "\n" + r + ".º más cercano");
        }
        return L((person ? c + "님" : c) + "\n바로 근처에 있습니다",   // 두 줄이라 「의」 없이 (11-16 대표님)
                 "You're right next to\n" + c,
                 (person ? c + "さんの" : c + "の") + "\nすぐ近くにいます",
                 "你就在\n" + c + "旁边",
                 "Estás justo al lado de\n" + c);
    }

    private static string HintFor(Item it)
    {
        if (it == null) return "";
        if (it.kind == Kind.Hunt && it.host)
            return L("누르면 내 보물찾기 현황을 볼 수 있어요", "Tap to see your treasure hunt status", "タップで自分の宝探しの状況を見られます", "点击查看我的寻宝进度", "Toca para ver el estado de tu búsqueda");
        if (it.kind == Kind.Hunt && it.listed)
            return L("보물상자는 직접 찾아가서 열 수 있어요", "Go to the chest in person to open it", "宝箱はその場所へ行くと開けられます", "需要亲自前往才能打开宝箱", "Ve hasta el cofre para abrirlo");
        if (it.kind == Kind.Hunt)
            return L("두 번 톡톡 누르면 보물상자를 열어요", "Double-tap to open the treasure chest", "ダブルタップで宝箱を開けます", "双击打开宝箱", "Toca dos veces para abrir el cofre");
        if (it.kind == Kind.Person)
            return L("두 번 톡톡 누르면 프로필을 볼 수 있어요", "Double-tap to see their profile", "ダブルタップでプロフィールを見られます", "双击查看个人资料", "Toca dos veces para ver su perfil");
        return L("두 번 톡톡 누르면 자세히 볼 수 있어요", "Double-tap to see more", "ダブルタップで詳しく見られます", "双击查看详情", "Toca dos veces para ver más");
    }

    private static string SwipeHint() =>
        L("옆으로 밀면 다음 · 가까운 순서", "Swipe for the next one · nearest first", "横にスワイプで次へ · 近い順", "左右滑动查看下一个 · 由近到远", "Desliza para ver el siguiente · del más cercano");

    private void SetHint(string s, bool icon)
    {
        hintShown = s;
        if (hint == null) return;
        hint.text = s;
        float w = hint.preferredWidth;
        float total = w + (icon ? HintIcon + HintGap : 0f);
        float x = -total * 0.5f;
        if (tapIcon != null)
        {
            Show(tapIcon.gameObject, icon);
            tapIcon.anchoredPosition = new Vector2(x + HintIcon * 0.5f, 0f);
        }
        var hr = hint.rectTransform;
        hr.sizeDelta = new Vector2(w + 4f, HintH);
        hr.anchoredPosition = new Vector2(x + (icon ? HintIcon + HintGap : 0f) + (w + 4f) * 0.5f, 0f);
    }

    private static string L(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);

    // ── 손가락 ──

    public void OnPointerDown(PointerEventData e) => dragMoved = false;

    public void OnPointerClick(PointerEventData e)
    {
        if (e.dragging || dragMoved || !cardOn || vis < 0.5f || items.Count == 0 || closing) return;
        var hit = e.pointerPressRaycast.gameObject;
        if (hit != null && shots != null)
            foreach (var sh in shots)
                if (sh.view != null && sh.item != null && sh.item != current && hit.transform.IsChildOf(sh.view.transform))
                {
                    int k = items.IndexOf(sh.item);
                    if (k >= 0) { Go(k); return; }   // 옆 미리보기(3D · 사진)를 누르면 그쪽으로
                }
        TapCurrent();
    }

    // AR 에서 누른 것과 같이 — 장소는 누를 때마다 살짝 커지고(소리 · 진동) 두 번이면 정보 창, 사람은 두 번이면 프로필, 보물은 보물찾기가 직접 (두 번 톡톡 = 열기 · 내 보물 = 현황).
    // 「근처 장소」의 사진 칸은 두 번이면 목록 사진을 누른 것과 같은 창
    private void TapCurrent()
    {
        var it = current;
        if (it == null) return;
        float now = Time.unscaledTime;
        bool twice = tapItem == it && now - tapAt <= DoubleTap;
        tapItem = twice ? null : it;
        tapAt = now;
        if (it.listed && it.kind == Kind.Hunt)
        {
            // 「근처 장소」의 보물은 멀리서 열지 않는다 — 직접 찾아가라고 알린다 (11-20 대표님 H6). 내 보물(현황) · 상자 앞(4m)이면 AR 에서 누른 것과 같이
            if (Live(it) && (it.host || AtChest(it))) R0926HuntTreasure.TapFromModal(it.obj);
            else if (twice) GoFind();
            else Light();
            return;
        }
        if (it.listed && (it.mix >= 0.5f || !Live(it)))
        {
            if (twice) OpenListed(it);
            else Light();
            return;
        }
        if (it.obj == null) return;
        switch (it.kind)
        {
            case Kind.Hunt:
                R0926HuntTreasure.TapFromModal(it.obj);
                break;
            case Kind.Person:
                if (twice && it.person != null) it.person.OnUserDoubleTapped();
                else Light();
                break;
            default:
                if (twice && it.info != null) it.info.DoubleTapFromModal();
                else if (it.haptic != null && it.haptic.isActiveAndEnabled) it.haptic.Tap();
                else Light();
                break;
        }
    }

    // 상자 앞 — 「발견한 장소」에 뜨는 거리(4m) 안이고 AR 에 보인다
    private bool AtChest(Item it)
    {
        var main = Camera.main;
        if (main == null || !Alive(it) || !Anchored(it.obj)) return false;
        return Flat(At(it.obj, it.kind), main.transform.position, out float dy) <= Enter && Mathf.Abs(dy) <= MaxRise;
    }

    private static void GoFind()
    {
        var tm = ToastManager.Instance;
        if (tm != null) tm.Show(L("보물상자는 직접 찾아가서 두 번 눌러야 열려요", "Walk to the treasure chest and double-tap it there to open it", "宝箱はその場所まで行ってダブルタップすると開きます", "请亲自走到宝箱旁双击打开", "Camina hasta el cofre y tócalo dos veces allí para abrirlo"));
    }

    private void Go(int k)
    {
        if (items.Count <= 1) return;
        k = Mathf.Clamp(k, 0, items.Count - 1);
        if (k == target) return;
        target = k;
        Light();
    }

    // 닫기 (× · 아래로 밀기) — 「발견한 장소」는 앱을 끌 때까지 그 장소를 다시 안 띄운다. 둘 다 지금 4m 안의 것은 나갔다 올 때까지 「발견한 장소」로 안 띄운다
    private void Close(bool swiped)
    {
        if (closing || items.Count == 0) return;
        if (mode == Mode.Found)
            foreach (var it in items)
                if (!string.IsNullOrEmpty(it.key)) closedFound.Add(it.key);
        var main = Camera.main;
        if (main != null) SnoozeAround(main.transform.position);
        closing = true;
        sliding = swiped;
        pulling = false;
        tapItem = null;
        Light();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        dragMoved = true;
        if (!cardOn || closing) return;
        Vector2 d = e.position - e.pressPosition;
        if (Mathf.Abs(d.y) > Mathf.Abs(d.x))
        {
            if (d.y < 0f && HostY(e, out float y))   // 아래로 — 카드를 끌어내린다 (위 · 옆은 받지 않는다)
            {
                pulling = true;
                pullPointer = e.pointerId;
                pullFrom = y - pull;   // 여기서부터 따라 내려온다 (판단 거리만큼 튀지 않게)
                pullLastY = y;
                pullLastT = Time.unscaledTime;
                pullV = 0f;
            }
            return;
        }
        if (items.Count <= 1) return;
        if (!Local(e, out float x)) return;
        dragging = true;
        dragPointer = e.pointerId;
        dragStartX = lastX = x;
        dragStartPos = pos;
        lastT = Time.unscaledTime;
        flingV = 0f;
        vel = 0f;
    }

    public void OnDrag(PointerEventData e)
    {
        if (pulling && e.pointerId == pullPointer)
        {
            if (Touches() >= 2 || closing) { pulling = false; return; }   // 두 손가락(확대) — 놓고 제자리로
            if (!HostY(e, out float y)) return;
            float p = y - pullFrom;
            pull = p < 0f ? p : p * 0.15f;   // 위로는 고무줄처럼 조금만
            float t = Time.unscaledTime, dt = t - pullLastT;
            if (dt > 0.0001f) pullV = Mathf.Lerp(pullV, (y - pullLastY) / dt, 0.5f);
            pullLastY = y;
            pullLastT = t;
            return;
        }
        if (!dragging || e.pointerId != dragPointer) return;
        if (Touches() >= 2) { dragging = false; return; }   // 두 손가락(확대)이 시작되면 놓는다 — 제자리로
        if (!Local(e, out float x)) return;
        float pp = dragStartPos - (x - dragStartX) / Spacing;
        float max = items.Count - 1;
        if (pp < 0f) pp *= 0.3f; else if (pp > max) pp = max + (pp - max) * 0.3f;   // 끝에선 고무줄처럼
        pos = pp;
        float ddt = Time.unscaledTime - lastT;
        if (ddt > 0.0001f) flingV = Mathf.Lerp(flingV, (x - lastX) / ddt, 0.5f);
        lastX = x;
        lastT = Time.unscaledTime;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (pulling && e.pointerId == pullPointer)
        {
            pulling = false;
            if (Time.unscaledTime - pullLastT > 0.12f) pullV = 0f;   // 멈췄다 뗐으면 튕긴 게 아니다
            if (pull < -PullClose || pullV < -PullFling && pull < -PullFlingMin) Close(true);
            return;
        }
        if (!dragging || e.pointerId != dragPointer) return;
        dragging = false;
        if (Time.unscaledTime - lastT > 0.12f) flingV = 0f;   // 멈췄다 뗐으면 튕긴 게 아니다
        float moved = (pos - target) * Spacing;   // + 면 손가락이 왼쪽으로 = 다음
        int to = target;
        if (moved > FlipDistance || -flingV > FlipSpeed) to = target + 1;
        else if (moved < -FlipDistance || flingV > FlipSpeed) to = target - 1;
        to = Mathf.Clamp(to, 0, items.Count - 1);
        if (to != target) { target = to; Light(); }
    }

    private bool Local(PointerEventData e, out float x)
    {
        x = 0f;
        if (card == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(card, e.position, e.pressEventCamera, out var lp)) return false;
        x = lp.x;
        return true;
    }

    // 화면 틀(카드가 사는 곳) 기준 높이 — 카드가 내려가도 손가락 자리는 그대로 잰다
    private bool HostY(PointerEventData e, out float y)
    {
        bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var lp);
        y = lp.y;
        return ok;
    }

    private static int Touches()
    {
        var ts = Touchscreen.current;
        if (ts == null) return 0;
        int n = 0;
        foreach (var t in ts.touches) if (t.isInProgress) n++;
        return n;
    }

    private static void Light()
    {
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerLightHaptic();
    }

#if UNITY_EDITOR
    /// <summary>플레이 시험 — 켜 두면 저절로 뜨지 않는다 (시작 「근처 장소」 · 위로 밀기 · 「발견한 장소」). 근처 · 길안내 시험만 끈다 (Redesign0926PlayTest)</summary>
    public static bool EditorQuiet;
    public int EditorCount => items.Count;
    public bool EditorShowing => cardOn && vis >= 0.99f;
    public bool EditorCardOn => cardOn;
    public string EditorTitle => title != null ? title.text : "";
    public string EditorHint => hint != null ? hint.text : "";
    public string EditorDistance => distText != null ? distText.text : "";
    public GameObject EditorCurrent => current != null ? current.obj : null;
    public int EditorTarget => target;
    public float EditorPos => pos;
    public bool EditorDismissed(GameObject go) => go != null && dismissed.ContainsKey(go);
    public GameObject EditorItem(int k) => k >= 0 && k < items.Count ? items[k].obj : null;
    public RenderTexture EditorTexture(int slot) => shots != null && slot >= 0 && slot < shots.Length ? shots[slot].rt : null;
    public Camera EditorCam(int slot) => shots != null && slot >= 0 && slot < shots.Length ? shots[slot].cam : null;
    public GameObject EditorSlotObject(int slot) => shots != null && slot >= 0 && slot < shots.Length && shots[slot].item != null ? shots[slot].item.obj : null;
    public RectTransform EditorCard => card;
    public float EditorCardScale => cardScale;
    public void EditorTick() => Tick(Time.unscaledTime);
    public void EditorGo(int k) => Go(k);
    public void EditorTap() => TapCurrent();
    public void EditorDismiss() => Close(false);
    public void EditorTapSlot(int slot)
    {
        if (shots == null || slot < 0 || slot >= shots.Length || shots[slot].item == null) return;
        int k = items.IndexOf(shots[slot].item);
        if (k >= 0 && k != target) Go(k); else TapCurrent();
    }
    // 11-18 J/M1
    public string EditorMode => mode.ToString();
    public bool EditorClosing => closing;
    public bool EditorOpenNearby() => OpenNearby(Time.unscaledTime, true);
    public bool EditorCanSwipeUp(Vector2 at) => CanSwipeUp(at);
    /// <summary>AR 화면을 from → to 로 민 것 — 위로 밀기와 같은 판단 (1초 안에 민 것으로 친다)</summary>
    public bool EditorSwipeUp(Vector2 from, Vector2 to) => CanSwipeUp(from) && SwipeOk(to - from) && OpenNearby(Time.unscaledTime, true);
    public void EditorLive(float ago) { live = true; liveAt = Time.unscaledTime - ago; resumeAt = -1f; }
    public float EditorLiveAt => liveAt;
    public bool EditorStartShown { get => startShown; set => startShown = value; }
    public void EditorResume() { OnApplicationPause(true); OnApplicationPause(false); }
    public bool EditorClosedFound(GameObject go) { string k = KeyOf(go); return k != null && closedFound.Contains(k); }
    public void EditorForget() { closedFound.Clear(); dismissed.Clear(); }
    public int EditorRank(int k) => k >= 0 && k < items.Count ? items[k].rank : 0;
    public string EditorEntryId(int k) => k >= 0 && k < items.Count && items[k].listed ? items[k].entry.id : null;
    public bool EditorPhoto(int k) => k >= 0 && k < items.Count && items[k].listed && items[k].mix > 0.5f;
    public string EditorPhotoKey(int k) => k >= 0 && k < items.Count ? items[k].photoKey : null;
    public Texture EditorPhotoTexture(int k) => k >= 0 && k < items.Count ? items[k].photo : null;
    public bool EditorPhotoShown(int slot) => shots != null && slot >= 0 && slot < shots.Length && shots[slot].photo != null && shots[slot].photo.gameObject.activeSelf;
    public R0926NearPhoto EditorPhotoFrame(int slot) => shots != null && slot >= 0 && slot < shots.Length ? shots[slot].photo : null;
    public float EditorPullOffset => pull;
    /// <summary>카드를 from → to 로 끌었다 놓은 것 (화면 좌표) — 진짜 손가락과 같은 처리</summary>
    public void EditorDrag(Vector2 from, Vector2 to)
    {
        var es = EventSystem.current;
        if (es == null) return;
        var e = new PointerEventData(es) { pointerId = -1, pressPosition = from, position = Vector2.Lerp(from, to, 0.2f) };
        OnPointerDown(e);
        OnBeginDrag(e);
        e.position = to;
        OnDrag(e);
        OnEndDrag(e);
    }

    private string KeyOf(GameObject go)
    {
        if (go == null) return null;
        FindManagers(Time.unscaledTime);
        string k = null;
        if (dataMgr != null) k = KeyIn(dataMgr.GetSpawnedObjects(), go, "dm:");
        if (k == null && tourMgr != null) k = KeyIn(tourMgr.GetSpawnedObjects(), go, "tour:");
        if (k == null && subwayMgr != null) k = KeyIn(subwayMgr.GetSpawnedObjects(), go, "sub:");
        if (k == null && trainMgr != null) k = KeyIn(trainMgr.GetSpawnedObjects(), go, "train:");
        if (k == null && terminalMgr != null) k = KeyIn(terminalMgr.GetSpawnedObjects(), go, "term:");
        if (k == null && storeMgr != null) k = KeyIn(storeMgr.GetSpawnedObjects(), go, "store:");
        if (k == null) k = KeyIn(R0926HuntTreasure.SpawnedObjects, go, "hunt:");
        if (k == null && P2PManager.Instance != null) k = KeyIn(P2PManager.Instance.GetActiveAvatars(), go, "p2p:");
        return k;
    }

    private static string KeyIn<TKey>(Dictionary<TKey, GameObject> d, GameObject go, string prefix)
    {
        if (d == null) return null;
        foreach (var kv in d) if (kv.Value == go) return prefix + kv.Key;
        return null;
    }
#endif
}
