using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 가까이 모달 (11-15 대표님 (C) — 2m 안에 들어오면 화면 가운데 투명 카드 「○○○ / 바로 근처에 있습니다」 두 줄).
/// 2m 안에 들어온 오브젝트(우팡 장소 상자 · 3D 모델 · 공공데이터 · 공공교통 · 보물상자 · 주변 사람)를 카드 가운데에 띄운다 —
/// 전용 카메라가 그 오브젝트만 투명 배경으로 찍어 RawImage 에 (핀치 줌 · 관리자 크기와 상관없이 늘 같은 크기), 그동안 AR 화면에선 빠진다(빈 레이어 28~31).
/// 여러 개면 가까운 순서로 옆으로 넘긴다 (하늘 고정 카드처럼 · 아래 점 · ‹ ›). 두 번 톡톡 = AR 에서 두 번 누른 것과 같다 (장소 정보 · 프로필 · 보물 열기).
/// 한 번 뜨면 5초는 2m 밖이어도 머물고, 그 뒤엔 2.3m 안에 있는 동안. × = 지금 카드의 오브젝트들을 2m 밖으로 나갔다 다시 올 때까지 안 띄운다.
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

    private const float Enter = 2f, Leave = 2.3f, Teleport = 10f, MaxRise = 15f, HoldFor = 5f, RearmAfter = 1f, TickEvery = 0.25f;
    private const int MaxItems = 6, ParkLayer = 28;
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

    private sealed class Item
    {
        public GameObject obj;
        public Kind kind;
        public float dist, since, aimAt;
        public R0926NearHold hold;
        public DoubleTap3D info;
        public Object3DTouchHaptic haptic;
        public P2PUserInfo person;
        public bool host;
        public string name;
        public bool framed, hasDir;
        public Vector3 center, goalCenter, dir;   // center = 맨 위 기준(배율을 뺀) 가운데 — 확대 · 축소와 상관없이 같은 크기로 찍힌다
        public float size, goalSize, offSince = -1f;
    }

    private sealed class Shot
    {
        public RawImage view;
        public Camera cam;
        public RenderTexture rt;
        public int layer;
        public Item item;
        public bool big, fresh;
    }

    private struct Near { public GameObject obj; public Kind kind; public float dist; }

    private static readonly System.Comparison<Near> NearOrder = (a, b) => a.dist.CompareTo(b.dist);
    private static readonly System.Comparison<Item> ItemOrder = (a, b) => a.dist.CompareTo(b.dist);

    private readonly List<Item> items = new List<Item>();
    private readonly List<Near> near = new List<Near>();
    private readonly Dictionary<GameObject, float> dismissed = new Dictionary<GameObject, float>();   // × — 2m 밖으로 나간 때 (-1 = 아직 안)
    private readonly List<GameObject> undismiss = new List<GameObject>();
    private readonly List<KeyValuePair<GameObject, float>> redismiss = new List<KeyValuePair<GameObject, float>>();
    private readonly List<Renderer> rends = new List<Renderer>();
    private readonly List<Vector4> spheres = new List<Vector4>();
    private readonly Vector3[] corners = new Vector3[4];
    private readonly int[] slotK = new int[3];
    private Camera[] camBuf = new Camera[8];
    private Shot[] shots;
    private Transform camRoot;
    private Item ghost;          // 마지막 하나가 떠나는 중 — 카드가 사라질 때까지 그대로 찍는다
    private Item current;        // 가운데 칸

    private DataManager dataMgr;
    private TourAPIManager tourMgr;
    private SubwayManager subwayMgr;
    private TrainStationManager trainMgr;
    private TerminalManager terminalMgr;
    private float nextFind, nextTick, nextStrip, lastReorder;

    private float pos, vel;
    private int target;
    private bool dragging, dragMoved;
    private int dragPointer;
    private float dragStartX, dragStartPos, lastX, lastT, flingV;

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

    private void Awake()
    {
        if (close != null) close.onClick.AddListener(Dismiss);
        if (prevButton != null) prevButton.onClick.AddListener(() => Go(target - 1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Go(target + 1));
        if (card != null) card.gameObject.SetActive(false);
    }

    private void OnDisable() => Shut();

    private void OnDestroy()
    {
        Shut();
        if (camRoot != null) Destroy(camRoot.gameObject);
    }

    private void Update()
    {
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        if (now >= nextTick) { nextTick = now + TickEvery; Tick(now); }
        bool want = items.Count > 0 && !Covered();
        if (want && !cardOn) Open(now);
        if (!cardOn) return;
        vis = Mathf.MoveTowards(vis, want ? 1f : 0f, dt / FadeTime);
        if (!want && vis <= 0f) Shut();
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

    // ── 누가 가까이 있나 (0.25초마다) ──

    private void Tick(float now)
    {
        var main = Camera.main;
        if (main == null) return;
        Vector3 me = main.transform.position;
        FindManagers(now);
        near.Clear();
        if (dataMgr != null) Gather(dataMgr.GetSpawnedObjects(), Kind.Place, me);
        if (tourMgr != null) Gather(tourMgr.GetSpawnedObjects(), Kind.Place, me);
        if (subwayMgr != null) Gather(subwayMgr.GetSpawnedObjects(), Kind.Place, me);
        if (trainMgr != null) Gather(trainMgr.GetSpawnedObjects(), Kind.Place, me);
        if (terminalMgr != null) Gather(terminalMgr.GetSpawnedObjects(), Kind.Place, me);
        Gather(R0926HuntTreasure.SpawnedObjects, Kind.Hunt, me);
        if (P2PManager.Instance != null) Gather(P2PManager.Instance.GetActiveAvatars(), Kind.Person, me);

        Undismiss(now, me);

        bool changed = false;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            if (!Alive(it)) { RemoveAt(i); changed = true; continue; }
            it.dist = Flat(it.obj.transform.position, me, out float dy);
            bool gone = Mathf.Abs(dy) > MaxRise || it.dist > Teleport || (now - it.since >= HoldFor && it.dist > Leave);
            if (gone) { RemoveAt(i); changed = true; continue; }
            Refresh(it);
        }

        near.Sort(NearOrder);
        foreach (var n in near)
        {
            if (n.dist > Enter || items.Count >= MaxItems) break;
            if (Has(n.obj) || dismissed.ContainsKey(n.obj) || !Visible(n.obj) || !Anchored(n.obj)) continue;
            items.Add(Make(n, now));
            changed = true;
        }

        if (items.Count > 0 && (changed || !dragging && now - lastReorder >= 2f)) Reorder(now);
        if (cardOn) RefreshHead();
    }

    private void Gather<TKey>(Dictionary<TKey, GameObject> spawned, Kind kind, Vector3 me)
    {
        if (spawned == null) return;
        foreach (var kv in spawned)
        {
            var go = kv.Value;
            if (go == null || !go.activeInHierarchy) continue;
            float d = Flat(go.transform.position, me, out float dy);
            if (d > Leave || Mathf.Abs(dy) > MaxRise) continue;
            near.Add(new Near { obj = go, kind = kind, dist = d });
        }
    }

    // × 한 것 — 2m 밖에서 1초가 지나면(또는 꺼지면) 다시 띄울 수 있다
    private void Undismiss(float now, Vector3 me)
    {
        if (dismissed.Count == 0) return;
        undismiss.Clear();
        redismiss.Clear();
        foreach (var kv in dismissed)
        {
            var go = kv.Key;
            if (go == null || !go.activeInHierarchy) { undismiss.Add(go); continue; }
            bool inside = Flat(go.transform.position, me, out _) <= Enter;
            if (inside) { if (kv.Value >= 0f) redismiss.Add(new KeyValuePair<GameObject, float>(go, -1f)); }
            else if (kv.Value < 0f) redismiss.Add(new KeyValuePair<GameObject, float>(go, now));
            else if (now - kv.Value >= RearmAfter) undismiss.Add(go);
        }
        foreach (var go in undismiss) dismissed.Remove(go);
        foreach (var kv in redismiss) dismissed[kv.Key] = kv.Value;
    }

    private static float Flat(Vector3 p, Vector3 me, out float dy)
    {
        dy = p.y - me.y;
        float dx = p.x - me.x, dz = p.z - me.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void FindManagers(float now)
    {
        if (now < nextFind || dataMgr != null && tourMgr != null && subwayMgr != null && trainMgr != null && terminalMgr != null) return;
        nextFind = now + 5f;   // 없는 매니저는 5초마다 다시 (DataManager.Instance 는 없으면 에러를 남긴다)
        if (dataMgr == null) dataMgr = FindAnyObjectByType<DataManager>();
        if (tourMgr == null) tourMgr = FindAnyObjectByType<TourAPIManager>();
        if (subwayMgr == null) subwayMgr = FindAnyObjectByType<SubwayManager>();
        if (trainMgr == null) trainMgr = FindAnyObjectByType<TrainStationManager>();
        if (terminalMgr == null) terminalMgr = FindAnyObjectByType<TerminalManager>();
    }

    private Item Make(Near n, float now)
    {
        var go = n.obj;
        var it = new Item { obj = go, kind = n.kind, dist = n.dist, since = now };
        if (!go.TryGetComponent(out it.hold)) it.hold = go.AddComponent<R0926NearHold>();
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

    // 이름은 띄운 뒤에 오기도 한다 (장소 정보 · 사람 이름)
    private static void Refresh(Item it)
    {
        if (it.kind == Kind.Place && it.info != null) it.name = it.info.GetName();
        else if (it.kind == Kind.Person && it.person != null) it.name = it.person.username;
    }

    private bool Has(GameObject go)
    {
        foreach (var it in items) if (it.obj == go) return true;
        return false;
    }

    private bool Alive(Item it) => it.obj != null && it.obj.activeInHierarchy && Visible(it.obj);

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

    private void RemoveAt(int i)
    {
        var it = items[i];
        items.RemoveAt(i);
        if (items.Count == 0 && cardOn && vis > 0f && it.obj != null && it.obj.activeInHierarchy)
        {
            if (ghost != null && ghost != it) Release(ghost);
            ghost = it;   // 카드와 같이 사라진다
            target = 0; pos = 0f; vel = 0f;
        }
        else Release(it);
        if (tapItem == it) tapItem = null;
    }

    // 가까운 순서 — 보던 것은 그 자리에 두고 나머지만 자리를 바꾼다
    private void Reorder(float now)
    {
        var cur = current != null && items.Contains(current) ? current : null;
        items.Sort(ItemOrder);
        lastReorder = now;
        if (cur == null)
        {
            target = Mathf.Clamp(target, 0, items.Count - 1);
            if (!cardOn) { target = 0; pos = 0f; vel = 0f; }
            return;
        }
        int ni = items.IndexOf(cur);
        pos += ni - target;
        target = ni;
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
        dragging = false;
        if (ghost != null) { Release(ghost); ghost = null; }
        card.gameObject.SetActive(true);
        Strip();
        nextStrip = now + 1f;
        RefreshHead();
    }

    private void Shut()
    {
        if (shots != null) foreach (var sh in shots) Free(sh);
        foreach (var it in items) Release(it);
        if (ghost != null) { Release(ghost); ghost = null; }
        if (card != null && card.gameObject.activeSelf) card.gameObject.SetActive(false);
        cardOn = false;
        current = null;
        headItem = null;
        frontView = null;
        dragging = false;
        vis = 0f;
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
            shots[i] = new Shot { view = views[i], cam = cam, layer = ViewLayers[i] };
            if (views[i] != null) views[i].enabled = false;
        }
    }

    private static void Free(Shot sh)
    {
        if (sh.cam != null) { sh.cam.enabled = false; sh.cam.targetTexture = null; }
        if (sh.rt != null) { RenderTexture.ReleaseTemporary(sh.rt); sh.rt = null; }
        if (sh.view != null) { sh.view.texture = null; sh.view.enabled = false; }
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

    private int Count => items.Count > 0 ? items.Count : ghost != null ? 1 : 0;

    private Item At(int k) => k >= 0 && k < items.Count ? items[k] : items.Count == 0 && k == 0 ? ghost : null;

    // 꺼진 물체(풀로 돌아감)는 바로 뺀다 — 걸린 레이어는 R0926NearHold 가 꺼질 때 되돌렸다
    private void Prune()
    {
        for (int i = items.Count - 1; i >= 0; i--)
            if (items[i].obj == null || !items[i].obj.activeInHierarchy) { Release(items[i]); items.RemoveAt(i); }
        if (ghost != null && (ghost.obj == null || !ghost.obj.activeInHierarchy)) { Release(ghost); ghost = null; }
        if (items.Count > 0 && ghost != null) { Release(ghost); ghost = null; }
    }

    private void Carousel(float dt)
    {
        int count = Count;
        if (count == 0) return;
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
        int count = Count;
        for (int s = 0; s < slotK.Length; s++) slotK[s] = -1;
        if (shots == null) return;
        for (int k = Mathf.Max(0, Mathf.CeilToInt(pos - 1.5f)); k < count; k++)
        {
            float d = k - pos;
            if (d >= 1.5f) break;
            if (d > -1.5f) slotK[k % shots.Length] = k;
        }

        // 카드에 든 것 — 보이는 칸이면 그 칸 레이어, 아니면 아무도 안 찍는 레이어 (둘 다 AR 화면엔 없다)
        for (int k = 0; k < count; k++)
        {
            var it = At(k);
            if (it == null || it.hold == null) continue;
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
            if (it == null || it.obj == null)
            {
                if (sh.view.enabled) sh.view.enabled = false;
                sh.cam.enabled = false;
                sh.item = null;
                continue;
            }
            if (sh.item != it) { sh.item = it; sh.fresh = true; }
            float d = k - pos, ad = Mathf.Abs(d);
            bool big = ad < 0.5f;
            Target(sh, big);
            float sc = Mathf.Lerp(1f, SideScale, Mathf.Clamp01(ad));
            float a = ad <= 1f ? Mathf.Lerp(1f, SideAlpha, ad) : Mathf.Lerp(SideAlpha, 0f, (ad - 1f) / 0.5f);
            float bob = (1f - Mathf.Cos(now * Mathf.PI * 2f / BobPeriod)) * 0.5f * BobHeight * (1f - Mathf.Clamp01(ad * 2f));
            var vr = sh.view.rectTransform;
            vr.anchoredPosition = new Vector2(d * Spacing, bob);
            vr.localScale = new Vector3(sc, sc, 1f);
            sh.view.color = new Color(1f, 1f, 1f, a);
            bool render = sh.fresh || big || (Time.frameCount + s) % 3 == 0;   // 옆 미리보기는 세 프레임에 한 번
            if (render && Aim(sh, it, me, now)) sh.fresh = false;
            else if (render) render = false;
            sh.cam.enabled = render;
            if (!sh.view.enabled && it.framed) sh.view.enabled = true;
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
        int count = Count;
        bool multi = items.Count >= 2;
        if (multi && lastCount < 2) multiSince = now;
        lastCount = items.Count;

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

        Place(dt);
        float e = vis * vis * (3f - 2f * vis);
        if (cardGroup != null)
        {
            cardGroup.alpha = e;
            bool live = vis > 0.5f && items.Count > 0;
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

    // 자리 — 노치 아래 줄과 주소줄 · 도크 사이(고정한 하늘 카드가 있으면 그 자리도 비켜서) 가운데. 모자라면(가로 화면) 줄인다
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
        card.anchoredPosition = cardPos;
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
        string d = it.dist.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "m";
        if (d != distShown && distText != null)
        {
            distShown = d;
            distText.text = d;
            if (distTag != null) distTag.sizeDelta = new Vector2(TagPadL + TagPin + TagGap + distText.preferredWidth + TagPadR, TagH);
        }
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
        if (e.dragging || dragMoved || !cardOn || vis < 0.5f || items.Count == 0) return;
        var hit = e.pointerPressRaycast.gameObject;
        if (hit != null && shots != null)
            foreach (var sh in shots)
                if (sh.view != null && hit == sh.view.gameObject && sh.item != null && sh.item != current)
                {
                    int k = items.IndexOf(sh.item);
                    if (k >= 0) { Go(k); return; }   // 옆 미리보기를 누르면 그쪽으로
                }
        TapCurrent();
    }

    // AR 에서 누른 것과 같이 — 장소는 누를 때마다 살짝 커지고(소리 · 진동) 두 번이면 정보 창, 사람은 두 번이면 프로필, 보물은 보물찾기가 직접 (두 번 톡톡 = 열기 · 내 보물 = 현황)
    private void TapCurrent()
    {
        var it = current;
        if (it == null || it.obj == null) return;
        float now = Time.unscaledTime;
        bool twice = tapItem == it && now - tapAt <= DoubleTap;
        tapItem = twice ? null : it;
        tapAt = now;
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

    private void Go(int k)
    {
        if (items.Count <= 1) return;
        k = Mathf.Clamp(k, 0, items.Count - 1);
        if (k == target) return;
        target = k;
        Light();
    }

    // × — 지금 카드의 것을 모두 2m 밖으로 나갔다 다시 올 때까지 띄우지 않는다. 가운데 것은 카드와 같이 사라진다
    private void Dismiss()
    {
        var cur = current;
        foreach (var it in items)
        {
            if (it.obj != null) dismissed[it.obj] = -1f;
            if (it != cur) Release(it);
        }
        items.Clear();
        tapItem = null;
        if (cur != null && cardOn && cur.obj != null && cur.obj.activeInHierarchy)
        {
            if (ghost != null && ghost != cur) Release(ghost);
            ghost = cur;
            target = 0; pos = 0f; vel = 0f;
        }
        else Release(cur);
        Light();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        dragMoved = true;
        if (!cardOn || items.Count <= 1) return;
        Vector2 d = e.position - e.pressPosition;
        if (Mathf.Abs(d.y) > Mathf.Abs(d.x)) return;
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
        if (!dragging || e.pointerId != dragPointer) return;
        if (Touches() >= 2) { dragging = false; return; }   // 두 손가락(확대)이 시작되면 놓는다 — 제자리로
        if (!Local(e, out float x)) return;
        float p = dragStartPos - (x - dragStartX) / Spacing;
        float max = items.Count - 1;
        if (p < 0f) p *= 0.3f; else if (p > max) p = max + (p - max) * 0.3f;   // 끝에선 고무줄처럼
        pos = p;
        float dt = Time.unscaledTime - lastT;
        if (dt > 0.0001f) flingV = Mathf.Lerp(flingV, (x - lastX) / dt, 0.5f);
        lastX = x;
        lastT = Time.unscaledTime;
    }

    public void OnEndDrag(PointerEventData e)
    {
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
    /// <summary>플레이 시험</summary>
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
    public void EditorDismiss() => Dismiss();
    public void EditorTapSlot(int slot)
    {
        if (shots == null || slot < 0 || slot >= shots.Length || shots[slot].item == null) return;
        int k = items.IndexOf(shots[slot].item);
        if (k >= 0 && k != target) Go(k); else TapCurrent();
    }
#endif
}
