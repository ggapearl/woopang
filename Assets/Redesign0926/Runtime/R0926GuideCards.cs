using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 첫 사용 안내 — 카드뉴스 9장을 옆으로 넘겨 보는 가벼운 창 (11-09 대표님 「카드뉴스 작업한 것들 … 베이스로 옆으로 스와이프해서 넘겨보면서 안내 … 화살표 안내와 함께」).
/// 카드는 스토어 사진 꼴 — 흐린 우주 바탕 · W O O P A N G · 두 줄 제목(분홍 강조) · 한 줄 설명 · 아래로 넘치는 휴대폰 화면.
/// 휴대폰 화면은 JPG(.bytes) 로 들고 다니다 보이는 카드와 양옆만 풀고, 닫으면 버린다. 한국어는 한국어 화면 · 나머지 언어는 영어 화면.
/// 카드마다 그 기능의 하단 버튼(목록 · 추가 · 보물찾기)을 동그랗게 비추고 화살표가 까딱인다 (비출 버튼이 없는 카드는 구멍이 닫힌다).
/// 하단 버튼은 안내 중에도 눌린다 — 누르면 그 버튼 일을 하고 안내는 닫힌다 (10-19 대표님 · R0926GuideOverlay → FirstTimeGuide 확인 버튼 → Close).
/// 열기 · 닫기 · 첫 실행 판정은 FirstTimeGuide. FirstTimeGuidePanel 이 틀이라 떠 있는 동안 Showing · BlocksWorld · 노치 숨김이 그대로 선다.
/// </summary>
public class R0926GuideCards : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasRaycastFilter
{
    [System.Serializable]
    public class Card
    {
        public RectTransform root;     // 둥근 판 (Mask) — 바탕 · 글 · 휴대폰이 이 안에서 잘린다
        public Image plate;            // 판 모양 (마스크 그림)
        public CanvasGroup group;
        public RawImage bg;
        public RectTransform brandDot;
        public Text brand;
        public Text head;
        public Text sub;
        public Image phone;            // 둥근 휴대폰 (Mask)
        public RawImage shot;
        public Image stroke;
    }

    [SerializeField] private FirstTimeGuide guide;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Card[] cards;
    [Tooltip("카드마다 비출 하단 버튼 — 없는 카드는 비워 둔다")]
    [SerializeField] private RectTransform[] targets;
    [SerializeField] private RectTransform dock;
    [SerializeField] private Image[] dims;              // 위 · 아래 · 왼쪽 · 오른쪽
    [SerializeField] private RectTransform hole;
    [SerializeField] private Image ring;
    [SerializeField] private Image arrow;
    [SerializeField] private RectTransform track;       // 카드들 — 가로 화면에선 도크 쪽으로 넘치지 않게 잘린다 (RectMask2D)
    [SerializeField] private RectTransform prev, next;
    [SerializeField] private RectTransform dots;
    [SerializeField] private Image[] dotImages;
    [SerializeField] private RectTransform primary;
    [SerializeField] private Image primaryImage;
    [SerializeField] private Text primaryLabel;
    [SerializeField] private RectTransform skip;
    [SerializeField] private Text skipLabel;
    [SerializeField] private TextAsset background;
    [SerializeField] private TextAsset[] shotsKo, shotsEn;
    [SerializeField] private float holeSize = 260f;
    [SerializeField] private float dimAlpha = 0.8f;
    [Tooltip("넘기기 · 시작하기 · 건너뛰기 누름 소리 — 하단 버튼과 같은 소리")]
    [SerializeField] private AudioClip tapSound;
    [Tooltip("누름 진동 세기 — AR 오브젝트(큐브) 터치와 같게")]
    [SerializeField, Range(0f, 1f)] private float tapHaptic = 0.3f;

    // 카드 문구 — 제목은 두 줄(\n), *강조* 는 분홍. 순서 ko · en · ja · zh · es
    private static readonly string[][] Heads =
    {
        new[] { "카메라를 비추면\n*주변 장소*가 보여요", "Point your camera,\n*find what’s near*", "カメラを向けると\n*周りのスポット*が見える", "打开相机，\n*周边地点*一目了然", "Apunta la cámara y\n*descubre lo cercano*" },
        new[] { "떠 있는 장소를 누르면\n*정보*가 바로 펼쳐져요", "Tap a floating place\nto *see its details*", "浮かんだスポットをタップ\n*情報*がすぐ開きます", "点一下浮动的地点\n*详细信息*立刻展开", "Toca un lugar flotante\ny *mira su información*" },
        new[] { "‘목록’에서 근처 장소를\n*한눈에*", "See nearby places\n*at a glance* in ‘List’", "「リスト」で近くのスポットを\n*ひと目で*", "在“列表”中\n*一眼看遍*附近地点", "Lugares cercanos\n*de un vistazo* en ‘Lista’" },
        new[] { "‘지도’를 켜면\n*친구 위치*까지 한눈에", "Switch to ‘Map’\nto *find your friends*", "「地図」にすると\n*友だちの居場所*も", "切换到“地图”\n*好友在哪*一看便知", "Cambia a ‘Mapa’\ny *encuentra a tus amigos*" },
        new[] { "이 자리에\n*장소를 남기세요*", "*Pin a place*\nright where you are", "この場所に\n*スポットを残そう*", "在这里\n*留下你的地点*", "*Deja un lugar*\njusto donde estás" },
        new[] { "짜잔! 내가 올린 장소가\n*AR로 떠올라요*", "Ta-da! Your place\n*pops up in AR*", "ジャーン！投稿したスポットが\n*ARで浮かび上がる*", "当当！你的地点\n*在AR中浮现*", "¡Tachán! Tu lugar\n*aparece en AR*" },
        new[] { "지금 이 자리에\n*보물을 숨겨요*", "*Hide a treasure*\nright where you stand", "今いる場所に\n*宝物を隠そう*", "就在此地\n*藏一个宝藏*", "*Esconde un tesoro*\njusto donde estás" },
        new[] { "가장 먼저 찾으면\n*보물 상자*가 열려요", "Find it first and\n*the chest opens*", "一番に見つけると\n*宝箱が開く*", "第一个找到，\n*宝箱就会打开*", "Encuéntralo primero y\n*se abre el cofre*" },
        new[] { "근처에 있는 사람이\n*동그랗게 떠올라요*", "People nearby\n*float into view*", "近くにいる人が\n*まるく浮かびます*", "附近的人\n*以圆形头像浮现*", "La gente cercana\n*aparece flotando*" },
    };

    private static readonly string[][] Subs =
    {
        new[] { "거리와 방향을 따라 바로 찾아가세요", "Follow distance and direction right to the spot", "距離と方向をたどって、そのまま向かえます", "跟着距离和方向直接找过去", "Sigue la distancia y la dirección hasta el lugar" },
        new[] { "메뉴 · 가격 · 반려견 동반 · 화장실까지 밖에서 미리", "Menus, prices, pet-friendly & restroom info from outside", "メニュー・価格・ペット同伴・トイレまで外から確認", "菜单、价格、可否带宠物、洗手间，在门外就能看到", "Menús, precios, mascotas y baños, desde fuera" },
        new[] { "보물찾기 · 축제 · 근처 친구까지 한 목록에", "Treasure hunts, festivals and friends in one list", "宝探し・お祭り・近くの友だちまでひとつのリストに", "寻宝、庆典、附近好友，都在一个列表里", "Búsquedas del tesoro, festivales y amigos en una lista" },
        new[] { "위치를 공유한 친구와 근처 보물찾기를 지도에서 확인해요", "See friends sharing their location and hunts nearby", "位置を共有した友だちや近くの宝探しを地図で確認", "在地图上查看共享位置的好友和附近的寻宝", "Amigos que comparten ubicación y búsquedas cercanas" },
        new[] { "사진 · 이름 · 분류만 넣으면 끝, 3D 모델도 올려요", "Just a photo, a name and a category — 3D models too", "写真・名前・分類を入れるだけ、3Dモデルも投稿できます", "只需照片、名称和分类，还能上传3D模型", "Solo foto, nombre y categoría — también modelos 3D" },
        new[] { "‘추가하기’를 누르면 근처 사람들에게도 보여요", "Tap ‘Add here’ and people nearby can see it too", "「追加する」を押すと近くの人にも表示されます", "点击“添加”，附近的人也能看到", "Toca ‘Añadir’ y la gente cercana también lo verá" },
        new[] { "상품 · 힌트 · 보물 모양을 정하면 근처 사람들이 찾아 나서요", "Set a prize, a hint and a shape — people nearby go hunting", "賞品・ヒント・宝物の形を決めれば、近くの人が探しに来ます", "设定奖品、提示和宝藏外形，附近的人就会来寻找", "Elige premio, pista y forma — la gente cercana saldrá a buscarlo" },
        new[] { "상품 카드를 받고, 주최자에게 바로 메시지가 가요", "Get the prize card — the host is notified right away", "賞品カードがもらえ、主催者にすぐ知らせが届きます", "领取奖品卡，主办方会立刻收到通知", "Recibe la tarjeta del premio — el anfitrión recibe aviso" },
        new[] { "프로필을 눌러 팔로우하고 메시지를 보내 보세요", "Tap a profile to follow and send a message", "プロフィールをタップしてフォロー、メッセージを送ろう", "点击头像即可关注并发送消息", "Toca un perfil para seguir y enviar un mensaje" },
    };

    private const float Aspect = 2778f / 1284f;            // 휴대폰 화면 세로 / 가로 (캡처 비)
    private const float OpenTime = 0.32f, CloseTime = 0.26f, SnapTime = 0.32f;
    private const float ArrowH = 106f, Bob = 20f;
    private const float DotH = 18f, DotOn = 52f, DotGap = 16f;
    private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f, 1f);
    private static readonly Color DotOff = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color DimColor = new Color(0.016f, 0.024f, 0.031f, 1f);

    private Canvas rootCanvas;
    private readonly Vector3[] c4 = new Vector3[4];
    private float W, H;
    private bool land;

    // 자리 (틀 기준 — 왼쪽 아래가 원점)
    private float cardCx, cardCy, cw, ch, step;
    private Rect clip;

    private bool opened, closing, dragging, snapping, touched;
    private int index;
    private float pos, snapFrom, snapTo, snapT;
    private float fade;
    private int fadeDir;
    private float openedAt;
    private float dragFrom, dragX, lastX, lastT, velX;
    private float holeOpen;
    private Vector2 holePos;
    private bool holePlaced;
    private float placedPos = float.NaN;
    private int placedVersion = -1, layoutVersion;

    private bool korean;
    private Texture2D bgTex;
    private Texture2D[] tex;

    // 자리를 다시 잡을지 — 화면 크기 · 안전영역 · 도크 자리 · 언어가 바뀌었을 때만
    private Vector4 keySize;
    private Rect keySafe, keyDock;
    private int keyLang = -1;

    /// <summary>지금 카드 (0부터)</summary>
    public int Index => index;
    public int Count => cards != null ? cards.Length : 0;
    /// <summary>열려 있고 닫히는 중이 아니다</summary>
    public bool IsOpen => opened && !closing;

    private RectTransform Frame => (RectTransform)transform;

    // ============================================================
    // 열기 · 닫기
    // ============================================================

    /// <summary>안내 패널을 켠 뒤 FirstTimeGuide 가 부른다 — 첫 카드부터</summary>
    public void Open(FirstTimeGuide g)
    {
        if (g != null) guide = g;
        if (Count == 0) { Finish(); return; }
        HideOldGuide();
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        korean = AppLanguage.Code == "ko";
        if (tex == null || tex.Length != cards.Length) tex = new Texture2D[cards.Length];
        if (bgTex == null) bgTex = Load(background);
        foreach (var c in cards)
            if (c != null && c.bg != null) { c.bg.texture = bgTex; c.bg.enabled = bgTex != null; }
        foreach (var c in cards)
            if (c != null && c.root != null) c.root.gameObject.SetActive(false);

        index = 0; pos = 0f;
        dragging = snapping = touched = false;
        holeOpen = 0f; holePlaced = false;
        closing = false; opened = true;
        fade = 0f; fadeDir = 1;
        openedAt = Time.unscaledTime;
        placedPos = float.NaN;
        keyLang = -1;
        if (group != null) { group.alpha = 0f; group.blocksRaycasts = true; group.interactable = true; }
        Preload();
        Changed();
    }

    /// <summary>시작하기 · 건너뛰기 · 하단 버튼 · 뒤로가기 — 사라진 뒤 FirstTimeGuide 가 패널을 끈다</summary>
    public void Close()
    {
        if (!opened) { Finish(); return; }
        if (closing) return;
        closing = true;
        dragging = false;
        fadeDir = -1;
        if (group != null) { group.blocksRaycasts = false; group.interactable = false; }
    }

    private void Finish()
    {
        opened = closing = false;
        fadeDir = 0;
        if (guide != null) guide.CardsClosed();
        else if (transform.parent != null) transform.parent.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        opened = closing = dragging = snapping = false;
        fadeDir = 0;
        Release();
    }

    // 예전 안내(어둡게 · 구멍 · 글 · 쪽 그림)는 쓰지 않는다 — 안내 패널 안에서 이 카드만 남긴다
    private void HideOldGuide()
    {
        var parent = transform.parent;
        if (parent == null) return;
        foreach (Transform ch in parent)
            if (ch != transform && ch.gameObject.activeSelf) ch.gameObject.SetActive(false);
    }

    // ============================================================
    // 버튼 · 넘기기
    // ============================================================

    private bool Busy => !opened || closing;

    public void Prev() { if (Busy) return; Tap(); Go(index - 1); }
    public void Next() { if (Busy) return; Tap(); Go(index + 1); }
    public void Primary()
    {
        if (Busy) return;
        Tap();
        if (index >= Count - 1) Close(); else Go(index + 1);
    }
    public void Skip() { if (Busy) return; Tap(); Close(); }
    public void Back() { if (Busy) return; Close(); }

    /// <summary>소리 없이 한 장 — FirstTimeGuide.OnSwipe (안내 막의 '다음' · 시험)</summary>
    public void Step(int dir) { if (Busy) return; Go(index + dir); }

    private void Go(int i)
    {
        touched = true;
        i = Mathf.Clamp(i, 0, Count - 1);
        if (i != index) { index = i; Changed(); }
        snapFrom = pos; snapTo = i; snapT = 0f; snapping = true;
    }

    // 쪽이 바뀌었다 — 아래 버튼 글 · 양옆 화살표 · 건너뛰기
    private void Changed()
    {
        bool last = index >= Count - 1;
        if (primaryLabel != null)
            primaryLabel.text = last ? R0926LocalizedText.Say("시작하기", "Get started", "はじめる", "开始", "Empezar")
                                     : R0926LocalizedText.Say("다음", "Next", "次へ", "下一步", "Siguiente");
        if (primaryImage != null) primaryImage.color = last ? Pink : new Color(1f, 1f, 1f, 0.16f);
        if (prev != null) prev.gameObject.SetActive(index > 0);
        if (next != null) next.gameObject.SetActive(!last);
        if (skip != null) skip.gameObject.SetActive(!last);
        SizePrimary();
    }

    private void Tap()
    {
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect(tapHaptic, tapSound);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (Busy) return;
        dragging = true; snapping = false; touched = true;
        dragFrom = pos; dragX = e.position.x;
        velX = 0f; lastX = e.position.x; lastT = Time.unscaledTime;
    }

    // 손가락을 그대로 따라간다 — 끝 카드 너머는 무겁게
    public void OnDrag(PointerEventData e)
    {
        if (!dragging) return;
        float units = (e.position.x - dragX) * W / Mathf.Max(1f, Screen.width);
        float p = dragFrom - units / Mathf.Max(1f, step), max = Count - 1;
        if (p < 0f) p = -Rubber(-p);
        else if (p > max) p = max + Rubber(p - max);
        pos = p;
        float now = Time.unscaledTime, dt = now - lastT;
        if (dt > 0.0001f) velX = Mathf.Lerp(velX, (e.position.x - lastX) / dt, 0.6f);
        lastX = e.position.x; lastT = now;
    }

    // 놓으면 — 카드 폭의 18% 넘게 끌었거나 빠르게 튕겼으면 옆 카드로
    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging) return;
        dragging = false;
        if (Time.unscaledTime - lastT > 0.1f) velX = 0f;   // 멈췄다 놓으면 빠르기 없음
        float fling = velX / Mathf.Max(1f, Screen.width), moved = pos - index;
        int to = index;
        if (moved > 0.18f || fling < -0.9f) to = index + 1;
        else if (moved < -0.18f || fling > 0.9f) to = index - 1;
        Go(to);
    }

    private static float Rubber(float x) => 0.3f * x / (1f + x);

    // 하단 버튼 자리는 이 창이 손가락을 받지 않는다 — 아래 도크 버튼이 그대로 눌린다 (10-19)
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        return dock == null || !dock.gameObject.activeInHierarchy || !RectTransformUtility.RectangleContainsScreenPoint(dock, sp, eventCamera);
    }

    // ============================================================
    // 매 프레임
    // ============================================================

    private void LateUpdate()
    {
        if (!opened || Count == 0 || !Fit()) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (LayoutChanged()) Relayout();

        if (fadeDir != 0)
        {
            fade = Mathf.Clamp01(fade + fadeDir * dt / (fadeDir > 0 ? OpenTime : CloseTime));
            if (fadeDir < 0 && fade <= 0f) { Finish(); return; }
            if (fadeDir > 0 && fade >= 1f) fadeDir = 0;
        }
        float e = 1f - (1f - fade) * (1f - fade) * (1f - fade);
        if (group != null) group.alpha = e;
        if (track != null) track.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, e);

        if (snapping && !dragging)
        {
            snapT = Mathf.Min(1f, snapT + dt / SnapTime);
            pos = Mathf.LerpUnclamped(snapFrom, snapTo, 1f - Mathf.Pow(1f - snapT, 3f));
            if (snapT >= 1f) { snapping = false; pos = snapTo; Preload(); }
        }
        else if (!dragging && !touched && index == 0)
        {
            // 처음 한 번 — 옆으로 넘길 수 있다는 걸 살짝 보여 준다 (다음 카드가 잠깐 비친다)
            float t = Time.unscaledTime - openedAt - 1.1f;
            pos = t > 0f && t < 0.8f ? 0.07f * Mathf.Sin(Mathf.PI * t / 0.8f) : 0f;
        }

        if (!Mathf.Approximately(pos, placedPos) || placedVersion != layoutVersion)
        {
            PlaceCards();
            PlaceDots();
            placedPos = pos;
            placedVersion = layoutVersion;
        }
        PlaceHole(dt);
    }

    // 안내 패널은 화면보다 작다 — 이 창은 화면 전체에 맞춘다
    private bool Fit()
    {
        if (rootCanvas == null)
        {
            var own = GetComponent<Canvas>();
            rootCanvas = own != null ? own.rootCanvas : GetComponentInParent<Canvas>()?.rootCanvas;
        }
        var parent = Frame.parent as RectTransform;
        if (rootCanvas == null || parent == null) return false;
        ((RectTransform)rootCanvas.transform).GetWorldCorners(c4);
        Vector3 bl = parent.InverseTransformPoint(c4[0]), tr = parent.InverseTransformPoint(c4[2]);
        var size = new Vector2(tr.x - bl.x, tr.y - bl.y);
        Vector3 at = (bl + tr) / 2f;
        if (Frame.sizeDelta != size || Frame.localPosition != at)
        {
            Frame.anchorMin = Frame.anchorMax = Frame.pivot = new Vector2(0.5f, 0.5f);
            Frame.localPosition = at;
            Frame.sizeDelta = size;
            Frame.localScale = Vector3.one;
        }
        W = size.x; H = size.y;
        return W > 1f && H > 1f;
    }

    private bool LayoutChanged()
    {
        var size = new Vector4(W, H, Screen.width, Screen.height);
        var safe = Screen.safeArea;
        DockRect(out var d);
        int lang = LangIndex();
        bool changed = lang != keyLang || (size - keySize).sqrMagnitude > 0.25f || safe != keySafe
            || Mathf.Abs(d.xMin - keyDock.xMin) > 2f || Mathf.Abs(d.yMin - keyDock.yMin) > 2f
            || Mathf.Abs(d.width - keyDock.width) > 2f || Mathf.Abs(d.height - keyDock.height) > 2f;
        if (changed) { keySize = size; keySafe = safe; keyDock = d; keyLang = lang; }
        return changed;
    }

    // ============================================================
    // 자리 잡기 — 세로: 카드는 건너뛰기 아래부터 쪽 점 위까지 · 그 아래 '다음' · 화살표 · 도크
    //             가로: 도크(오른쪽 세로 막대)와 화살표 왼쪽 자리 가득 — 카드 왼쪽 반에 글 · 점 · '다음', 오른쪽 반에 휴대폰
    // ============================================================

    private void Relayout()
    {
        layoutVersion++;
        land = W > H;
        int L = LangIndex();
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (c.brand != null) c.brand.text = "W O O P A N G";
            if (c.head != null) c.head.text = Rich(Heads[i % Heads.Length][L]);
            if (c.sub != null) c.sub.text = Subs[i % Subs.Length][L];
        }
        if (skipLabel != null)
        {
            skipLabel.text = R0926LocalizedText.Say("건너뛰기", "Skip", "スキップ", "跳过", "Omitir");
            skipLabel.fontSize = 42;
        }
        skipW = skipLabel != null ? Mathf.Max(220f, skipLabel.preferredWidth + 96f) : 220f;

        float k = H / Mathf.Max(1f, Screen.height);
        var sa = Screen.safeArea;
        float sL = sa.xMin * k, sR = (Screen.width - sa.xMax) * k, sB = sa.yMin * k, sT = (Screen.height - sa.yMax) * k;
        bool hasDock = DockRect(out var dk);
        float holeR = holeSize / 2f, arrowZone = holeR + 16f + ArrowH + 2f * Bob;

        float skipH = 96f, primH = land ? 112f : 128f, primY, dotsY, skipX, skipY, s, lw = 0f;
        if (!land)
        {
            float side = Mathf.Max(64f, Mathf.Max(sL, sR) + 40f);
            float zoneTop = hasDock ? dk.center.y + arrowZone : sB + 40f;
            primY = zoneTop + 30f + primH / 2f;
            dotsY = primY + primH / 2f + 36f + DotH / 2f;
            float cardBottom = dotsY + DotH / 2f + 34f;
            skipY = H - sT - 22f - skipH / 2f;
            float cardTop = skipY - skipH / 2f - 26f;
            cw = Mathf.Min(W - 2f * side, 1140f);
            ch = Mathf.Max(400f, cardTop - cardBottom);
            cardCx = W / 2f; cardCy = cardBottom + ch / 2f;
            step = cw + 48f;
            s = cw / 1140f;
            skipX = cardCx + cw / 2f;
            clip = Rect.MinMaxRect(0f, cardBottom - 24f, W, cardBottom + ch + 24f);
            foreach (var c in cards) if (c != null) LayoutPortrait(c, s);
        }
        else
        {
            float left = Mathf.Max(56f, sL + 36f);
            float right = hasDock ? dk.center.x - arrowZone - 28f : W - Mathf.Max(56f, sR + 36f);
            float top = H - Mathf.Max(36f, sT + 24f), bottom = Mathf.Max(36f, sB + 24f);
            cw = Mathf.Max(600f, right - left); ch = Mathf.Max(400f, top - bottom);
            cardCx = left + cw / 2f; cardCy = bottom + ch / 2f;
            step = cw + 64f;
            lw = cw * 0.54f;
            s = Mathf.Clamp(Mathf.Min(lw / 1100f, ch / 1250f), 0.6f, 1f);
            float colX = cardCx - cw / 2f + lw / 2f;
            primY = bottom + 64f + primH / 2f;
            dotsY = primY + primH / 2f + 30f + DotH / 2f;
            skipY = top - 30f - skipH / 2f;
            skipX = cardCx + cw / 2f - 34f;
            clip = Rect.MinMaxRect(0f, bottom - 24f, right + 32f, top + 24f);
            float ctrl = dotsY + DotH / 2f + 40f - bottom;   // 카드 아래 점 · '다음' 자리
            foreach (var c in cards) if (c != null) LayoutLandscape(c, s, lw, ctrl);
            cardCxText = colX;
        }
        if (land) { Put(primary, cardCxText, primY, 0f, primH); Put(dots, cardCxText, dotsY, 600f, DotH); }
        else { Put(primary, cardCx, primY, 0f, primH); Put(dots, cardCx, dotsY, 600f, DotH); }
        if (primaryLabel != null) primaryLabel.fontSize = land ? 46 : 50;
        SizePrimary();

        if (skip != null) Put(skip, skipX - skipW / 2f, skipY, skipW, skipH);

        // 양옆 ‹ › — 카드 가장자리에 반쯤 걸친다
        float bs = Mathf.Round(104f * Mathf.Max(0.8f, s));
        Put(prev, cardCx - cw / 2f, cardCy, bs, bs);
        Put(next, cardCx + cw / 2f, cardCy, bs, bs);

        if (track != null)
        {
            Put(track, clip.center.x, clip.center.y, clip.width, clip.height);
            var r = track.GetComponent<RectMask2D>();
            if (r != null) r.enabled = land;   // 세로는 카드가 화면 끝까지 미끄러진다
        }
    }

    private float cardCxText, skipW = 220f;

    private void SizePrimary()
    {
        if (primary == null || primaryLabel == null) return;
        float w = Mathf.Max(land ? 440f : 520f, primaryLabel.preferredWidth + 200f);
        primary.sizeDelta = new Vector2(w, primary.sizeDelta.y);
    }

    // 세로 카드 — 위에서부터 W O O P A N G · 제목 두 줄 · 설명 · 휴대폰 (아래로 6% 넘친다)
    private void LayoutPortrait(Card c, float s)
    {
        Plate(c);
        float pad = 64f * s, tw = cw - 2f * pad;
        Brand(c, 0f, 92f * s, s);
        float y = 150f * s;
        float hh = Head(c.head, tw, Mathf.RoundToInt(88f * s), Mathf.RoundToInt(58f * s));
        if (c.head != null) Top(c.head.rectTransform, 0f, y, tw, hh + 4f);
        y += hh + 24f * s;
        float sw = tw - 16f * s, sh = Sub(c.sub, sw, Mathf.RoundToInt(40f * s));
        if (c.sub != null) Top(c.sub.rectTransform, 0f, y, sw, sh + 4f);
        y += sh + 52f * s;
        float pw = Mathf.Clamp(Mathf.Min(cw * 0.8f, (ch * 1.06f - y) / Aspect), cw * 0.46f, cw * 0.8f);
        Phone(c, 0f, y, pw);
    }

    // 가로 카드 — 왼쪽 칸 위쪽 가운데에 글, 오른쪽 칸에 휴대폰 (카드 아래 끝까지)
    private void LayoutLandscape(Card c, float s, float lw, float ctrl)
    {
        Plate(c);
        float colX = -cw / 2f + lw / 2f, pad = 56f * s, tw = lw - 2f * pad;
        float hh = Head(c.head, tw, Mathf.RoundToInt(84f * s), Mathf.RoundToInt(54f * s));
        float sh = Sub(c.sub, tw, Mathf.RoundToInt(38f * s));
        float brandH = 40f * s, block = brandH + 36f * s + hh + 22f * s + sh;
        float y = Mathf.Max(48f * s, (ch - ctrl - block) / 2f);
        Brand(c, colX, y + brandH / 2f, s);
        y += brandH + 36f * s;
        if (c.head != null) Top(c.head.rectTransform, colX, y, tw, hh + 4f);
        y += hh + 22f * s;
        if (c.sub != null) Top(c.sub.rectTransform, colX, y, tw, sh + 4f);
        float rw = cw - lw, top = ch * 0.08f;
        float pw = Mathf.Min((ch - top) / Aspect, rw * 0.86f);
        float px = cw / 2f - rw / 2f;
        // 건너뛰기(카드 안 오른쪽 위 · 위에서 30~126)와 휴대폰 꼭대기가 같은 높이면 휴대폰을 왼쪽으로 비켜 둔다 (글 칸으로는 안 넘어가게)
        if (top < 30f + 96f + 16f) px = Mathf.Min(px, Mathf.Max(-cw / 2f + lw + pw / 2f, cw / 2f - 34f - skipW - 24f - pw / 2f));
        Phone(c, px, top, pw);
    }

    private void Plate(Card c)
    {
        if (c.root != null) c.root.sizeDelta = new Vector2(cw, ch);
        Radius(c.plate, Mathf.Clamp(Mathf.Min(cw, ch) * 0.05f, 36f, 64f));
        if (c.bg != null)
        {
            var rt = c.bg.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            // 바탕은 가득 덮는다 — 넓은 카드는 위쪽(분홍 빛) 가까이를 쓴다
            var t = c.bg.texture;
            float ta = t != null ? (float)t.width / t.height : 720f / 1300f, ra = cw / ch;
            if (ra > ta) { float vh = ta / ra; c.bg.uvRect = new Rect(0f, (1f - vh) * 0.85f, 1f, vh); }
            else { float uw = ra / ta; c.bg.uvRect = new Rect((1f - uw) / 2f, 0f, uw, 1f); }
        }
    }

    // 분홍 점 + W O O P A N G — 가운데 (x, 위에서 cy)
    private static void Brand(Card c, float x, float cy, float s)
    {
        if (c.brand == null) return;
        c.brand.fontSize = Mathf.RoundToInt(30f * s);
        c.brand.horizontalOverflow = HorizontalWrapMode.Overflow;
        float tw = c.brand.preferredWidth, th = 30f * s * 1.6f, dot = 14f * s, gap = 16f * s;
        float left = x - (dot + gap + tw) / 2f;
        Top(c.brand.rectTransform, left + dot + gap + tw / 2f, cy - th / 2f, tw + 4f, th);
        if (c.brandDot != null) Top(c.brandDot, left + dot / 2f, cy - dot / 2f, dot, dot);
    }

    // 제목 — 두 줄 그대로 두고, 한 줄이 넘치면 글자를 줄인다
    private static float Head(Text t, float maxW, int size, int min)
    {
        if (t == null) return 0f;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.fontSize = size;
        float w = t.preferredWidth;
        if (w > maxW) t.fontSize = Mathf.Max(min, Mathf.FloorToInt(size * maxW / w));
        var sh = t.GetComponent<Shadow>();
        if (sh != null) sh.effectDistance = new Vector2(0f, -Mathf.Max(2f, t.fontSize * 0.045f));
        return t.preferredHeight;
    }

    // 설명 — 폭 안에서 줄바꿈, 두 줄을 넘으면 한 번만 조금 줄인다 (앱 최소 글자 38 아래로는 안 줄이고 세 줄로)
    private static float Sub(Text t, float w, int size)
    {
        if (t == null) return 0f;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        t.fontSize = size;
        if (t.preferredHeight > size * 2.9f) t.fontSize = Mathf.Max(Mathf.Min(size, 38), Mathf.RoundToInt(size * 0.94f));
        return t.preferredHeight;
    }

    private static void Phone(Card c, float x, float top, float pw)
    {
        if (c.phone == null) return;
        float ph = pw * Aspect, r = pw * 0.085f;
        Top(c.phone.rectTransform, x, top, pw, ph);
        Radius(c.phone, r);
        if (c.shot != null)
        {
            var rt = c.shot.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        if (c.stroke != null) { Top(c.stroke.rectTransform, x, top, pw, ph); Radius(c.stroke, r); }
    }

    // 둥근 알약 그림(가장자리 63px)을 반지름 r 로
    private static void Radius(Image img, float r)
    {
        if (img != null) img.pixelsPerUnitMultiplier = 64f / Mathf.Max(1f, r);
    }

    private void PlaceCards()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c == null || c.root == null) continue;
            float d = i - pos, ad = Mathf.Abs(d);
            bool on = ad < 0.999f;
            if (c.root.gameObject.activeSelf != on)
            {
                c.root.gameObject.SetActive(on);
                if (on) EnsureShot(i);
            }
            if (!on) continue;
            c.root.anchorMin = c.root.anchorMax = Vector2.zero;
            c.root.pivot = new Vector2(0.5f, 0.5f);
            c.root.anchoredPosition = new Vector2(cardCx + d * step - clip.xMin, cardCy - clip.yMin);
            float sc = 1f - 0.06f * Mathf.Clamp01(ad);
            c.root.localScale = new Vector3(sc, sc, 1f);
            if (c.group != null) c.group.alpha = 1f - 0.45f * Mathf.Clamp01(ad);
        }
    }

    // 쪽 점 — 지금 쪽은 분홍 알약으로 늘어난다 (끄는 동안 이웃 점과 나눠 가진다)
    private void PlaceDots()
    {
        if (dotImages == null) return;
        int n = Mathf.Min(dotImages.Length, Count);
        float total = 0f;
        for (int i = 0; i < n; i++) total += DotW(i) + (i > 0 ? DotGap : 0f);
        float x = -total / 2f;
        for (int i = 0; i < n; i++)
        {
            var im = dotImages[i];
            if (im == null) continue;
            float w = DotW(i);
            var rt = im.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + w / 2f, 0f);
            rt.sizeDelta = new Vector2(w, DotH);
            im.color = Color.Lerp(DotOff, Pink, DotK(i));
            x += w + DotGap;
        }
    }

    private float DotK(int i) => 1f - Mathf.Clamp01(Mathf.Abs(i - pos));
    private float DotW(int i) => Mathf.Lerp(DotH, DotOn, DotK(i));

    // 비출 하단 버튼 — 구멍이 따라가고(없으면 닫힌다), 둘레가 퍼지고, 화살표가 까딱인다
    private void PlaceHole(float dt)
    {
        var target = targets != null && index < targets.Length ? targets[index] : null;
        bool has = target != null && target.gameObject.activeInHierarchy;
        if (has)
        {
            Vector2 want = FrameRect(target).center;
            holePos = holePlaced ? Vector2.Lerp(holePos, want, 1f - Mathf.Exp(-12f * dt)) : want;
            holePlaced = true;
        }
        holeOpen = Mathf.MoveTowards(holeOpen, has ? 1f : 0f, dt / 0.28f);
        float e = holeOpen * holeOpen * (3f - 2f * holeOpen);
        float size = holePlaced ? holeSize * e : 0f, r = size / 2f;
        Vector2 p = holePos;
        var dc = new Color(DimColor.r, DimColor.g, DimColor.b, dimAlpha);

        if (dims != null && dims.Length >= 4)
        {
            if (size < 1f)
            {
                Put(dims[0].rectTransform, W / 2f, H / 2f, W, H);
                for (int i = 1; i < 4; i++) Put(dims[i].rectTransform, 0f, 0f, 0f, 0f);
            }
            else
            {
                // 위 · 아래 · 왼쪽 · 오른쪽 — 구멍 그림 가장자리와 1씩 겹친다
                Put(dims[0].rectTransform, W / 2f, (p.y + r - 1f + H) / 2f, W, H - (p.y + r - 1f));
                Put(dims[1].rectTransform, W / 2f, (p.y - r + 1f) / 2f, W, p.y - r + 1f);
                Put(dims[2].rectTransform, (p.x - r + 1f) / 2f, p.y, p.x - r + 1f, size - 2f);
                Put(dims[3].rectTransform, (W + p.x + r - 1f) / 2f, p.y, W - (p.x + r - 1f), size - 2f);
            }
            foreach (var d in dims) if (d != null) d.color = dc;
        }
        if (hole != null)
        {
            Put(hole, p.x, p.y, size, size);
            var hi = hole.GetComponent<Image>();
            if (hi != null) hi.color = dc;
        }

        float t = Time.unscaledTime;
        if (ring != null)
        {
            float k = Mathf.Repeat(t / 2.2f, 1f);
            Put(ring.rectTransform, p.x, p.y, holeSize * 0.9f, holeSize * 0.9f);
            ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.3f, 1f - (1f - k) * (1f - k));
            ring.color = new Color(1f, 1f, 1f, 0.55f * (1f - k) * e);
        }
        if (arrow != null)
        {
            // 그림은 아래를 가리킨다 — 세로는 구멍 위에서 아래로, 가로는 구멍 왼쪽에서 오른쪽(도크)으로
            float reach = holeSize / 2f + 16f + ArrowH / 2f + Bob, bob = Bob * Mathf.Sin(t * Mathf.PI * 2f / 1.25f);
            var rt = arrow.rectTransform;
            Vector2 at = land ? new Vector2(p.x - reach - bob, p.y) : new Vector2(p.x, p.y + reach + bob);
            Put(rt, at.x, at.y, 84f, ArrowH);
            rt.localRotation = Quaternion.Euler(0f, 0f, land ? 90f : 0f);
            arrow.color = new Color(1f, 1f, 1f, e);
        }
    }

    // ============================================================
    // 그림
    // ============================================================

    // 보이는 카드와 양옆만 풀어 두고 나머지는 버린다
    private void Preload()
    {
        for (int i = 0; i < Count; i++)
        {
            if (Mathf.Abs(i - index) <= 1) EnsureShot(i);
            else Free(i);
        }
    }

    private void EnsureShot(int i)
    {
        if (tex == null || i < 0 || i >= tex.Length || tex[i] != null) return;
        var src = korean ? shotsKo : shotsEn;
        tex[i] = Load(src != null && i < src.Length ? src[i] : null);
        var c = cards[i];
        if (c != null && c.shot != null) { c.shot.texture = tex[i]; c.shot.enabled = tex[i] != null; }
    }

    private void Free(int i)
    {
        if (tex == null || i < 0 || i >= tex.Length || tex[i] == null) return;
        var c = cards[i];
        if (c != null && c.shot != null) { c.shot.texture = null; c.shot.enabled = false; }
        Destroy(tex[i]);
        tex[i] = null;
    }

    private void Release()
    {
        if (tex != null) for (int i = 0; i < tex.Length; i++) Free(i);
        if (bgTex == null) return;
        if (cards != null) foreach (var c in cards) if (c != null && c.bg != null) c.bg.texture = null;
        Destroy(bgTex);
        bgTex = null;
    }

    // JPG → 그림 (알파 없음 · 밉맵 없음 · 메모리 사본은 올린 뒤 버린다)
    private static Texture2D Load(TextAsset a)
    {
        if (a == null) return null;
        var t = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = a.name };
        if (!t.LoadImage(a.bytes, true)) { Destroy(t); return null; }
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    // ============================================================
    // 도움
    // ============================================================

    private bool DockRect(out Rect r)
    {
        r = default;
        if (dock == null || !dock.gameObject.activeInHierarchy) return false;
        r = FrameRect(dock);
        return r.width > 1f && r.height > 1f;
    }

    // 틀 기준(왼쪽 아래 원점) 사각형
    private Rect FrameRect(RectTransform rt)
    {
        rt.GetWorldCorners(c4);
        Vector3 a = Frame.InverseTransformPoint(c4[0]), b = Frame.InverseTransformPoint(c4[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x) + W / 2f, Mathf.Min(a.y, b.y) + H / 2f, Mathf.Max(a.x, b.x) + W / 2f, Mathf.Max(a.y, b.y) + H / 2f);
    }

    // 틀 기준(왼쪽 아래 원점) 가운데 · 크기 — 크기 0 이면 폭은 그대로 둔다
    private static void Put(RectTransform rt, float cx, float cy, float w, float h)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, cy);
        rt.sizeDelta = new Vector2(w > 0f ? w : (w < 0f ? 0f : rt.sizeDelta.x), Mathf.Max(0f, h));
    }

    // 카드 위 가운데 기준 — x 는 가운데에서, top 은 위에서 아래로
    private static void Top(RectTransform rt, float x, float top, float w, float h)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -top);
        rt.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
    }

    private static string Rich(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 40);
        bool on = false;
        foreach (char ch in s)
        {
            if (ch == '*') { sb.Append(on ? "</color>" : "<color=#FF6F9C>"); on = !on; }
            else sb.Append(ch);
        }
        if (on) sb.Append("</color>");
        return sb.ToString();
    }

    private static int LangIndex()
    {
        switch (AppLanguage.Code)
        {
            case "ko": return 0;
            case "ja": return 2;
            case "zh": return 3;
            case "es": return 4;
            default: return 1;
        }
    }
}
