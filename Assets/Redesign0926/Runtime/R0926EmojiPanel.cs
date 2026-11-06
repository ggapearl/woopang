using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>이모지 창을 키보드 자리에 들고 있는 쪽 — 대화·댓글 창(MobileKeyboardHandler) · 장소 칸의 키보드 위 입력줄(UploadInputMirror, 11-05)</summary>
public interface IR0926KeyboardHost
{
    bool isActiveAndEnabled { get; }
    /// <summary>이모지 창을 들고 있다 — 떠 있거나 키보드로 넘기는 중. 아니면 이모지 창은 스스로 내려간다</summary>
    bool AccessoryHolding { get; }
    /// <summary>마지막으로 본 키보드 높이 (캔버스 단위) — 이모지 창을 키보드와 같은 높이로</summary>
    float LastKeyboardHeight { get; }
    /// <summary>키보드를 내리고 그 자리(높이 h, 캔버스 단위)에 이모지 창을 둔다</summary>
    void ShowAccessory(float h);
    /// <summary>이모지 창 높이가 바뀌었다 (끌어내리는 중 · 화면이 돌았다) — snap 이면 바로 그 자리에</summary>
    void SetAccessoryHeight(float h, bool snap = false);
    /// <summary>이모지 창을 내린다 — toKeyboard 면 키보드를 다시 올린다</summary>
    void HideAccessory(bool toKeyboard);
}

/// <summary>
/// 이모지 창 (N-U · 10-26 대표님 「이모지버튼을 누르면 키패드가 사라지고 … 이모지만 잔뜩 … 키패드 전체를 채우는 식으로」 ·
/// 11-02 「키패드 각 버튼이 입력창 글자보다 큰 것마냥 · 아래로 아래로 스크롤 · 최상단에서 아래로 스와이프하면 창이 내려가도록」).
/// 입력줄의 이모지 단추(R0926EmojiButton)를 누르면 키보드가 내려가고 그 자리에 뜬다 — 대화·댓글 창은 MobileKeyboardHandler 가 이 창 위로 올려 둔다.
/// 자주 쓰는 것 + 7 분류를 한 목록으로 굴려 본다 · 아래 탭은 그 분류로 건너뛰기 · ⌫ 는 누르고 있으면 계속 지운다.
/// 맨 위에서 아래로 끌거나(R0926EmojiScroll) 손잡이·탭 줄을 아래로 끌면 창이 손가락을 따라 내려가고, 놓으면 닫히거나 제자리로.
/// 고른 이모지는 칸의 커서 자리에 우팡 이모지 글자로 들어가고(R0926Emoji) 보낼 때 진짜 이모지로 돌아간다.
/// 캔버스에 하나 — 적용 단계(Redesign0926ApplyEmoji)가 만든다.
/// </summary>
public class R0926EmojiPanel : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const int Sections = 1 + R0926Emoji.Categories;   // 자주 쓰는 것 + 분류

    private const float SidePad = 24f, TopPad = 44f, TabsH = 136f, HeaderH = 96f, BottomPad = 32f;
    private const float CellMin = 196f, RowH = 180f, EmojiSize = 140f, DeleteW = 150f;
    private const float SlideTime = 0.22f;
    private const float CloseDrag = 0.15f, CloseSpeed = 1600f;   // 창 높이의 이만큼 · 이 빠르기(캔버스 단위/초) 이상 끌어내리면 닫는다
    private const float RepeatDelay = 0.45f, RepeatEvery = 0.08f;
    private const float KeepPages = 60f;   // 닫은 뒤 큰 그림을 이만큼 들고 있다 — 키보드와 오가도 다시 읽지 않게
    private static readonly string[] TabKeys = { "1f552", "1f600", "1f43b", "1f354", "26bd", "1f697", "1f4a1", "1f523" };   // 🕒😀🐻🍔⚽🚗💡🔣

    [SerializeField] private RectTransform sheet;        // 움직이는 몸통 — 닫혀 있으면 꺼 둔다
    [SerializeField] private R0926EmojiScroll scroll;
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform tabs;
    [SerializeField] private RectTransform[] tabCells;
    [SerializeField] private RawImage[] tabIcons;
    [SerializeField] private Image tabMark;
    [SerializeField] private RectTransform deleteKey;
    [SerializeField] private Image deleteIcon;
    [SerializeField] private Image pressMark;
    [SerializeField] private Font font;
    [SerializeField] private Color labelColor = new Color(1f, 1f, 1f, 0.5f);

    private static R0926EmojiPanel open;

    /// <summary>이모지 창이 넣고 있는 칸 (떠 있을 때만)</summary>
    public static InputField Target => open != null ? open.target : null;
    /// <summary>이모지가 들어갈 자리 — 칸이 꺼져 있어도 이 자리에 커서를 그린다 (R0926InputFocus)</summary>
    public static int InsertAt => open != null ? open.insertAt : -1;
    public static bool IsOpenFor(InputField f) => open != null && f != null && open.target == f;
    /// <summary>이 손가락은 이모지 창 몫 (R0926KeyboardSwipe.Claims)</summary>
    public static bool Covers(Vector2 screenPos) =>
        open != null && open.sheet.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(open.sheet, screenPos, open.Cam);

    private InputField target;
    private IR0926KeyboardHost keyboard;
    private int insertAt;
    private float height;
    private bool closing;

    private float slideFrom, slideTo, slideT = 1f;
    private bool sheetDragging, selfDrag;
    private float dragFrom, dragOffset, dragSpeed;

    private Canvas root;
    private Vector2 laidFor;
    private Rect laidSafe;
    private float side;
    private int cols;
    private float cell;
    private readonly List<int>[] sections = new List<int>[Sections];
    private readonly float[] sectionTop = new float[Sections];
    private float contentH;
    private int selected = -1;
    private float markX = float.NaN;

    private R0926EmojiGrid[] grids;
    private Texture2D[] pages;
    private ResourceRequest[] loading;
    private float unloadAt = -1f;
    private Text[] labels;

    private bool deleteHeld;
    private float nextRepeat;

    // 들고 있는 쪽이 지워졌으면 (유니티의 가짜 null — 인터페이스로는 != null 이 못 거른다)
    private bool KbAlive => keyboard is Object o ? o != null : keyboard != null;

    private Camera Cam => root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;

    private void Awake()
    {
        var c = GetComponentInParent<Canvas>(true);
        root = c != null ? c.rootCanvas : null;
        if (sheet != null) sheet.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (open == this) open = null;
        UnloadPages();
    }

    // ── 열기 · 닫기 ───────────────────────────────────────────

    /// <summary>f 칸에 이모지를 넣는 창을 키보드 자리에 띄운다 (키보드는 kb 가 내린다)</summary>
    public void Open(InputField f, IR0926KeyboardHost kb)
    {
        if (f == null || kb == null || sheet == null) return;
        target = f;
        keyboard = kb;
        insertAt = f.isFocused ? Mathf.Clamp(f.caretPosition, 0, f.text.Length) : f.text.Length;
        open = this;
        closing = false;
        sheetDragging = selfDrag = false;   // 끊긴 끌기가 남아 있으면 올라오는 움직임이 멈춘다
        unloadAt = -1f;
        sheet.gameObject.SetActive(true);
        Build();
        height = PanelHeight();
        Layout();
        scroll.StopMovement();
        content.anchoredPosition = Vector2.zero;
        selected = -1;
        markX = float.NaN;
        LoadPages();
        keyboard.ShowAccessory(height);
        Slide(-height, 0f);
    }

    /// <summary>키보드로 돌아간다 (입력줄의 단추가 ⌨ 일 때) — 키보드가 올라오면 창은 스스로 내려간다</summary>
    public void SwitchToKeyboard()
    {
        if (open != this || closing) return;
        var input = target != null ? target.GetComponent<R0926EmojiInput>() : null;
        if (input != null) input.CaretOnFocus(insertAt);
        keyboard.HideAccessory(true);
    }

    /// <summary>뒤로가기 — 창만 내린다</summary>
    public void CloseByBack()
    {
        if (KbAlive) keyboard.HideAccessory(false);
        BeginClose();
    }

    private void BeginClose()
    {
        if (closing || !sheet.gameObject.activeSelf) return;
        closing = true;
        if (open == this) open = null;
        sheetDragging = selfDrag = false;
        Unpress();
        ReleaseDelete();
        Slide(sheet.anchoredPosition.y, -height - 8f);
    }

    private void Finish()
    {
        closing = false;
        sheet.gameObject.SetActive(false);
        target = null;
        keyboard = null;
        unloadAt = Time.unscaledTime + KeepPages;
    }

    private void Slide(float from, float to)
    {
        slideFrom = from;
        slideTo = to;
        slideT = 0f;
        sheet.anchoredPosition = new Vector2(0f, from);
    }

    private void Update()
    {
        if (unloadAt > 0f && Time.unscaledTime >= unloadAt) UnloadPages();
        if (sheet == null || !sheet.gameObject.activeSelf) return;

        if (!closing)
        {
            bool hold = KbAlive && keyboard.isActiveAndEnabled && keyboard.AccessoryHolding
                        && target != null && target.isActiveAndEnabled;
            if (!hold) BeginClose();
        }
        if (!closing && (CanvasSize() != laidFor || Screen.safeArea != laidSafe))   // 화면이 돌았다
        {
            height = PanelHeight();
            Layout();
            keyboard.SetAccessoryHeight(height);
            if (!sheetDragging && slideT >= 1f) sheet.anchoredPosition = Vector2.zero;
        }

        if (slideT < 1f && !sheetDragging)
        {
            slideT = Mathf.Min(1f, slideT + Time.unscaledDeltaTime / SlideTime);
            float k = 1f - Mathf.Pow(1f - slideT, 3f);
            sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(slideFrom, slideTo, k));
            if (slideT >= 1f && closing) { Finish(); return; }
        }

        PollPages();
        if (deleteHeld && Time.unscaledTime >= nextRepeat)
        {
            nextRepeat = Time.unscaledTime + RepeatEvery;
            Backspace();
        }
        TrackTab();
    }

    // ── 목록 ─────────────────────────────────────────────────

    private void Build()
    {
        for (int s = 0; s < Sections; s++)
        {
            if (sections[s] == null) sections[s] = new List<int>();
            sections[s].Clear();
        }
        sections[0].AddRange(R0926Emoji.Recents());
        for (int c = 1; c <= R0926Emoji.Categories; c++)
        {
            R0926Emoji.Range(c, out int start, out int count);
            for (int i = 0; i < count; i++) sections[c].Add(start + i);
        }

        int pageCount = R0926Emoji.PageCount;
        if (grids == null || grids.Length != pageCount)
        {
            grids = new R0926EmojiGrid[pageCount];
            pages = new Texture2D[pageCount];
            loading = new ResourceRequest[pageCount];
            for (int p = 0; p < pageCount; p++)
            {
                var go = new GameObject("Page" + p, typeof(RectTransform), typeof(CanvasRenderer), typeof(R0926EmojiGrid));
                go.layer = content.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(content, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0f, 1f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                grids[p] = go.GetComponent<R0926EmojiGrid>();
                grids[p].raycastTarget = false;
            }
        }
        if (labels == null)
        {
            labels = new Text[Sections];
            for (int s = 0; s < Sections; s++)
            {
                var go = new GameObject("Label" + s, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.layer = content.gameObject.layer;
                var t = go.GetComponent<Text>();
                t.rectTransform.SetParent(content, false);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f);
                t.rectTransform.pivot = new Vector2(0f, 1f);
                t.font = font != null ? font : Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
                t.fontSize = 40;
                t.color = labelColor;
                t.alignment = TextAnchor.MiddleLeft;
                t.raycastTarget = false;
                labels[s] = t;
            }
        }
        for (int s = 0; s < Sections; s++) labels[s].text = SectionName(s);

        for (int i = 0; i < tabIcons.Length && i < TabKeys.Length; i++)
        {
            int e = R0926Emoji.Find(TabKeys[i]);
            tabIcons[i].texture = R0926Emoji.Small;
            tabIcons[i].uvRect = e >= 0 ? R0926Emoji.SmallUV(e) : new Rect(0, 0, 0, 0);
        }
    }

    private void Layout()
    {
        var size = CanvasSize();
        laidFor = size;
        laidSafe = Screen.safeArea;
        float scale = root != null && root.scaleFactor > 0f ? root.scaleFactor : 1f;
        float bottom = Mathf.Max(0f, Screen.safeArea.yMin) / scale;
        float edge = Mathf.Max(Screen.safeArea.xMin, Screen.width - Screen.safeArea.xMax) / scale;
        side = SidePad + edge;

        sheet.sizeDelta = new Vector2(0f, height);
        var srt = (RectTransform)scroll.transform;
        srt.offsetMin = new Vector2(0f, TabsH + bottom);
        srt.offsetMax = new Vector2(0f, -TopPad);
        tabs.anchoredPosition = new Vector2(0f, bottom);
        tabs.sizeDelta = new Vector2(0f, TabsH);

        float w = size.x, inner = w - 2f * side;
        cols = Mathf.Max(7, Mathf.FloorToInt(inner / CellMin));
        cell = inner / cols;
        float y = 0f;
        for (int s = 0; s < Sections; s++)
        {
            sectionTop[s] = y;
            y += HeaderH + Mathf.Ceil(sections[s].Count / (float)cols) * RowH;
        }
        contentH = y + BottomPad;
        content.sizeDelta = new Vector2(0f, contentH);

        var perPage = new List<R0926EmojiGrid.Item>[grids.Length];
        for (int p = 0; p < perPage.Length; p++) perPage[p] = new List<R0926EmojiGrid.Item>();
        for (int s = 0; s < Sections; s++)
        {
            var list = sections[s];
            for (int k = 0; k < list.Count; k++)
            {
                int e = list[k];
                perPage[e / R0926Emoji.PageSize].Add(new R0926EmojiGrid.Item { emoji = e, center = CellCenter(s, k) });
            }
        }
        float emoji = Mathf.Min(EmojiSize, cell * 0.78f);
        for (int p = 0; p < grids.Length; p++)
        {
            grids[p].size = emoji;
            grids[p].SetItems(perPage[p]);
        }
        for (int s = 0; s < Sections; s++)
        {
            var rt = labels[s].rectTransform;
            rt.anchoredPosition = new Vector2(side + 14f, -sectionTop[s]);
            rt.sizeDelta = new Vector2(inner - 28f, HeaderH);
        }
        if (pressMark != null) pressMark.rectTransform.sizeDelta = new Vector2(cell - 10f, RowH - 10f);

        float tabW = (w - 2f * side - DeleteW) / tabCells.Length;
        for (int i = 0; i < tabCells.Length; i++)
        {
            tabCells[i].anchoredPosition = new Vector2(side + (i + 0.5f) * tabW, 0f);
            tabCells[i].sizeDelta = new Vector2(tabW, TabsH);
        }
        deleteKey.anchoredPosition = new Vector2(-side, 0f);
        deleteKey.sizeDelta = new Vector2(DeleteW, TabsH);
        tabMark.rectTransform.sizeDelta = new Vector2(Mathf.Min(tabW - 10f, 128f), 104f);
        markX = float.NaN;
    }

    private Vector2 CellCenter(int s, int k)
    {
        int row = k / cols, col = k % cols;
        return new Vector2(side + (col + 0.5f) * cell, -(sectionTop[s] + HeaderH + (row + 0.5f) * RowH));
    }

    private float PanelHeight()
    {
        float h = CanvasSize().y;
        float kb = KbAlive ? keyboard.LastKeyboardHeight : 0f;
        return kb >= h * 0.25f && kb <= h * 0.6f ? kb : h * 0.4f;
    }

    private Vector2 CanvasSize() => root != null ? ((RectTransform)root.transform).rect.size : new Vector2(Screen.width, Screen.height);

    private static string SectionName(int s)
    {
        switch (s)
        {
            case 0: return R0926LocalizedText.Say("자주 쓰는", "Frequently Used", "よく使う絵文字", "常用", "Usados con frecuencia");
            case 1: return R0926LocalizedText.Say("스마일리 및 사람", "Smileys & People", "スマイリーと人々", "笑脸和人物", "Emoticonos y personas");
            case 2: return R0926LocalizedText.Say("동물 및 자연", "Animals & Nature", "動物と自然", "动物与自然", "Animales y naturaleza");
            case 3: return R0926LocalizedText.Say("음식 및 음료", "Food & Drink", "食べ物と飲み物", "食物与饮料", "Comida y bebida");
            case 4: return R0926LocalizedText.Say("활동", "Activity", "アクティビティ", "活动", "Actividades");
            case 5: return R0926LocalizedText.Say("여행 및 장소", "Travel & Places", "旅行と場所", "旅行与地点", "Viajes y lugares");
            case 6: return R0926LocalizedText.Say("사물", "Objects", "モノ", "物体", "Objetos");
            default: return R0926LocalizedText.Say("기호", "Symbols", "記号", "符号", "Símbolos");
        }
    }

    // 지금 보고 있는 분류의 탭에 표시 — 끝까지 내렸으면 마지막 분류
    private void TrackTab()
    {
        var view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        float top = content.anchoredPosition.y;
        int s = 0;
        for (int i = 0; i < Sections; i++)
            if (sectionTop[i] <= top + HeaderH) s = i;
        if (top >= contentH - view.rect.height - 1f) s = Sections - 1;
        if (s != selected)
        {
            selected = s;
            for (int i = 0; i < tabIcons.Length; i++)
            {
                var c = tabIcons[i].color;
                c.a = i == s ? 1f : 0.45f;
                tabIcons[i].color = c;
            }
        }
        float x = tabCells[selected].anchoredPosition.x;
        markX = float.IsNaN(markX) ? x : Mathf.Lerp(markX, x, Mathf.Clamp01(Time.unscaledDeltaTime * 18f));
        tabMark.rectTransform.anchoredPosition = new Vector2(markX, 0f);
    }

    private void JumpTo(int s)
    {
        var view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        scroll.StopMovement();
        content.anchoredPosition = new Vector2(0f, Mathf.Clamp(sectionTop[s], 0f, Mathf.Max(0f, contentH - view.rect.height)));
    }

    // ── 큰 그림 ───────────────────────────────────────────────

    private void LoadPages()
    {
        for (int p = 0; p < grids.Length; p++)
            if (pages[p] == null && loading[p] == null) loading[p] = Resources.LoadAsync<Texture2D>(R0926Emoji.PagePath(p));
    }

    private void PollPages()
    {
        if (loading == null) return;
        for (int p = 0; p < loading.Length; p++)
        {
            var r = loading[p];
            if (r == null || !r.isDone) continue;
            loading[p] = null;
            pages[p] = r.asset as Texture2D;
            grids[p].Page = pages[p];
        }
    }

    private void UnloadPages()
    {
        unloadAt = -1f;
        if (pages == null) return;
        for (int p = 0; p < pages.Length; p++)
        {
            loading[p] = null;
            if (pages[p] == null) continue;
            grids[p].Page = null;
            Resources.UnloadAsset(pages[p]);
            pages[p] = null;
        }
    }

    // ── 고르기 · 지우기 ───────────────────────────────────────

    internal void Press(Vector2 screen, Camera cam)
    {
        if (pressMark == null) return;
        if (!EmojiAt(screen, cam, out _, out Vector2 center)) { Unpress(); return; }
        pressMark.rectTransform.anchoredPosition = center;
        pressMark.gameObject.SetActive(true);
    }

    internal void Unpress()
    {
        if (pressMark != null && pressMark.gameObject.activeSelf) pressMark.gameObject.SetActive(false);
    }

    internal void Pick(Vector2 screen, Camera cam)
    {
        Unpress();
        if (!closing && EmojiAt(screen, cam, out int e, out _)) Insert(e);
    }

    private bool EmojiAt(Vector2 screen, Camera cam, out int e, out Vector2 center)
    {
        e = -1;
        center = default;
        if (cols <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, cam, out var lp)) return false;
        var r = content.rect;
        float x = lp.x - r.xMin, y = r.yMax - lp.y;
        int col = Mathf.FloorToInt((x - side) / cell);
        if (col < 0 || col >= cols) return false;
        for (int s = 0; s < Sections; s++)
        {
            float g = sectionTop[s] + HeaderH;
            int n = sections[s].Count;
            if (y < g || y >= g + Mathf.Ceil(n / (float)cols) * RowH) continue;
            int k = Mathf.FloorToInt((y - g) / RowH) * cols + col;
            if (k >= n) return false;
            e = sections[s][k];
            center = CellCenter(s, k);
            return true;
        }
        return false;
    }

    private void Insert(int e)
    {
        if (target == null) return;
        string t = target.text ?? "";
        int at = Mathf.Clamp(insertAt, 0, t.Length);
        // 글자 수 제한은 보낼 글(진짜 이모지) 길이로 잰다 — 이모지 하나가 2~11 글자다
        if (target.characterLimit > 0 && R0926Emoji.ToReal(t).Length + R0926Emoji.RealOf(e).Length > target.characterLimit) return;
        target.text = t.Insert(at, R0926Emoji.Pua(e).ToString());
        insertAt = at + 1;
        R0926Emoji.Used(e);
        UIFeedbackManager.Instance?.HandleKeyInput("e");
    }

    // 커서 앞 한 글자 — 모르는 이모지(진짜 글자로 남은 것)는 이은 조각까지 함께
    private void Backspace()
    {
        if (target == null) return;
        string t = target.text ?? "";
        int at = Mathf.Clamp(insertAt, 0, t.Length);
        if (at == 0) return;
        int cut = at;
        while (cut > 0)
        {
            int cp = PrevCodePoint(t, cut, out int w);
            cut -= w;
            if (IsModifier(cp)) continue;   // 꾸밈 글자를 지웠으면 그 앞 글자까지
            if (cp >= 0x1F1E6 && cp <= 0x1F1FF && cut > 0)   // 국기 = 지역 글자 두 개
            {
                int prev = PrevCodePoint(t, cut, out int pw);
                if (prev >= 0x1F1E6 && prev <= 0x1F1FF) cut -= pw;
            }
            if (cut > 0 && PrevCodePoint(t, cut, out _) == 0x200D) continue;   // 잇기(ZWJ) 뒤 글자면 잇기와 그 앞까지
            break;
        }
        target.text = t.Remove(cut, at - cut);
        insertAt = cut;
        UIFeedbackManager.Instance?.HandleKeyInput("x");
    }

    private static bool IsModifier(int cp) =>
        cp == 0xFE0F || cp == 0xFE0E || cp == 0x200D || cp == 0x20E3 || (cp >= 0x1F3FB && cp <= 0x1F3FF) || (cp >= 0xE0020 && cp <= 0xE007F);

    private static int PrevCodePoint(string s, int end, out int width)
    {
        char c = s[end - 1];
        if (char.IsLowSurrogate(c) && end >= 2 && char.IsHighSurrogate(s[end - 2]))
        {
            width = 2;
            return char.ConvertToUtf32(s[end - 2], c);
        }
        width = 1;
        return c;
    }

    private void ReleaseDelete()
    {
        deleteHeld = false;
        if (deleteIcon != null) deleteIcon.color = Color.white;
    }

    // ── 탭 줄 · ⌫ · 손잡이 (목록 밖에서 누른 손가락) ─────────────

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || closing) return;
        if (!RectTransformUtility.RectangleContainsScreenPoint(deleteKey, e.position, e.pressEventCamera)) return;
        deleteHeld = true;
        nextRepeat = Time.unscaledTime + RepeatDelay;
        if (deleteIcon != null) deleteIcon.color = new Color(1f, 1f, 1f, 0.5f);
        Backspace();
    }

    public void OnPointerUp(PointerEventData e) => ReleaseDelete();

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || e.dragging || closing) return;
        for (int i = 0; i < tabCells.Length; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(tabCells[i], e.position, e.pressEventCamera)) continue;
            JumpTo(i);
            UIFeedbackManager.Instance?.HandleTouchFeedbackDirect();
            return;
        }
    }

    public void OnBeginDrag(PointerEventData e)
    {
        Vector2 d = e.position - e.pressPosition;
        if (closing || d.y >= 0f || -d.y <= Mathf.Abs(d.x)) return;   // 아래로 시작한 끌기만
        ReleaseDelete();
        selfDrag = true;
        BeginSheetDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (selfDrag) SheetDrag(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!selfDrag) return;
        selfDrag = false;
        EndSheetDrag(e);
    }

    // ── 창 끌어내리기 ─────────────────────────────────────────

    internal void BeginSheetDrag(PointerEventData e)
    {
        if (closing) return;
        sheetDragging = true;
        slideT = 1f;
        dragFrom = e.position.y - sheet.anchoredPosition.y * Scale();
        dragOffset = -sheet.anchoredPosition.y;
        dragSpeed = 0f;
    }

    internal void SheetDrag(PointerEventData e)
    {
        if (!sheetDragging) return;
        float off = Mathf.Max(0f, (dragFrom - e.position.y) / Scale());
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
        dragSpeed = Mathf.Lerp(dragSpeed, (off - dragOffset) / dt, 0.5f);
        dragOffset = off;
        sheet.anchoredPosition = new Vector2(0f, -off);
        if (KbAlive) keyboard.SetAccessoryHeight(height - off, true);
    }

    internal void EndSheetDrag(PointerEventData e)
    {
        if (!sheetDragging) return;
        sheetDragging = false;
        bool close = dragOffset > height * CloseDrag || (dragSpeed > CloseSpeed && dragOffset > 20f);
        if (close)
        {
            if (KbAlive) keyboard.HideAccessory(false);
            BeginClose();
            return;
        }
        if (KbAlive) keyboard.SetAccessoryHeight(height);
        Slide(sheet.anchoredPosition.y, 0f);
    }

    private float Scale() => root != null && root.scaleFactor > 0f ? root.scaleFactor : 1f;
}
