using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 지금 쓰고 있는 입력칸에 분홍 테두리 (2026-10-16 대표님: "inputfield 내에서도 작성되고 있구나 라는 느낌이 들도록 ·
/// 입력하면서도 잘 보일 수 있도록 적절한 위치에 스크롤").
/// 장소·보물찾기 칸은 키보드 위 입력줄(UploadInputMirror)로 쓴다 — 그때는 원래 칸에 테두리를 두고,
/// 그 칸이 입력줄 위로 보이게 칸이 든 목록을 부드럽게 올린다(입력줄이 떠 있는 동안 뒤 화면은 손으로 못 움직인다).
/// 입력줄을 쓰는 칸은 칸이 꺼져 있어 Unity 가 칸 안에 커서를 그리지 않는다(커서는 입력줄에서만 깜빡인다) —
/// 그 칸 글자 사이에도 입력줄 커서와 같은 자리·모양으로 깜빡이는 커서를 얹는다 (2026-10-20 대표님: "포커싱 되었을 때 앞에 글자 깜빡거리게").
/// 검색·댓글·대화처럼 칸 안에서 직접 치는 칸은 Unity 가 이미 깜빡이는 커서를 그리니 건드리지 않는다.
/// 캔버스에 하나 — 적용 단계(Redesign0926ApplyReadable)가 붙이고 연결한다.
/// </summary>
public class R0926InputFocus : MonoBehaviour
{
    [SerializeField] private Sprite ringSprite;     // r0926_pill_ring — r0926_pill 과 같은 9-slice
    [SerializeField] private Sprite pillSprite;     // 바탕이 이 알약이면 같은 둥글기로 감싼다
    [SerializeField] private Color ringColor = new Color(0.914f, 0.325f, 0.514f, 1f);
    [SerializeField] private UploadInputMirror[] mirrors;
    [SerializeField] private float gap = 28f;       // 칸 아래 ~ 입력줄 위 여백 (캔버스 단위)
    [SerializeField] private float scrollSpeed = 12f;

    private InputField current;
    private UploadInputMirror currentVia;
    private ScrollRect currentScroll;
    private Image ring;
    private Image caret;
    private TextGenerator measure;   // 칸 글의 자리를 재는 용 — 칸 글이 쓰는 것과 따로 둔다
    private int caretKey = -1;      // (커서 자리, 글 길이) — 바뀌면 깜빡임을 처음부터 켜진 채로
    private float blinkStart;

    void Update()
    {
        var f = Active(out var via);
        currentVia = via;
        if (f != current)
        {
            current = f;
            currentScroll = f != null ? VerticalScrollOf(f) : null;
            caretKey = -1;
            Place(f);
        }
        if (f != null && via != null && currentScroll != null) KeepAbove(f, via.PlacedBar);
    }

    // 글·입력줄 커서가 바뀐 뒤에 그려야 한 박자 늦지 않는다
    void LateUpdate()
    {
        var bar = currentVia != null ? currentVia.Bar : null;
        if (current != null && bar != null) DrawCaret(current, bar);
        else HideCaret();
    }

    void OnDestroy() => (measure as System.IDisposable)?.Dispose();

    void OnDisable()
    {
        current = null;
        currentVia = null;
        currentScroll = null;
        if (ring != null) ring.gameObject.SetActive(false);
        HideCaret();
    }

    private InputField Active(out UploadInputMirror via)
    {
        via = null;
        if (mirrors != null)
        {
            foreach (var m in mirrors)
            {
                var s = m != null && m.isActiveAndEnabled ? m.ActiveSource : null;
                if (s != null && s.gameObject.activeInHierarchy) { via = m; return s; }
            }
        }
        var es = EventSystem.current;
        var go = es != null ? es.currentSelectedGameObject : null;
        var f = go != null ? go.GetComponent<InputField>() : null;
        if (f == null || !f.isFocused || !f.gameObject.activeInHierarchy) return null;
        if (mirrors != null)
            foreach (var m in mirrors)
                if (m != null && m.IsMirrorInput(f)) return null;   // 입력줄 자신은 늘 쓰는 자리라 테두리를 두지 않는다
        return f;
    }

    private void Place(InputField f)
    {
        if (f == null)
        {
            if (ring != null) ring.gameObject.SetActive(false);
            return;
        }
        var host = Host(f, out float mul);
        if (ring == null)
        {
            var go = new GameObject("FocusRing0926", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            ring = go.GetComponent<Image>();
            ring.raycastTarget = false;
            ring.sprite = ringSprite;
            ring.type = Image.Type.Sliced;
            ring.color = ringColor;
        }
        var rt = ring.rectTransform;
        rt.SetParent(host, false);
        rt.SetAsFirstSibling();   // 바탕 바로 위 · 글 아래
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        ring.pixelsPerUnitMultiplier = mul;
        ring.gameObject.layer = host.gameObject.layer;
        ring.gameObject.SetActive(true);
    }

    // 테두리를 둘 곳 — 칸 자신이나 두 단계 위까지에서 알약 바탕을 찾는다 (팔로우 검색칸은 바탕이 부모)
    private RectTransform Host(InputField f, out float mul)
    {
        var t = f.transform;
        for (int i = 0; i < 3 && t != null; i++, t = t.parent)
        {
            var img = t.GetComponent<Image>();
            if (img != null && img.sprite != null && img.sprite == pillSprite && img.type == Image.Type.Sliced)
            {
                mul = img.pixelsPerUnitMultiplier;
                return (RectTransform)t;
            }
        }
        mul = 2f;   // 알약이 아니면 살짝 둥근 네모
        return (RectTransform)f.transform;
    }

    private static ScrollRect VerticalScrollOf(InputField f)
    {
        for (var t = f.transform.parent; t != null; t = t.parent)
        {
            var s = t.GetComponent<ScrollRect>();
            if (s != null && s.vertical && s.content != null) return s;
        }
        return null;
    }

    // 칸 글자 위에 입력줄 커서 — 글자 자리는 칸의 글이 그려지는 것과 같은 TextGenerator 로 재서 Unity 가 칸 안에 그릴 때와 같은 자리에 선다
    private void DrawCaret(InputField f, InputField bar)
    {
        var t = f.textComponent;
        if (t == null || t.font == null || !t.isActiveAndEnabled) { HideCaret(); return; }

        var rt = t.rectTransform;
        if (measure == null) measure = new TextGenerator();
        measure.Populate(t.text, t.GetGenerationSettings(rt.rect.size));   // 같은 글·같은 칸이면 다시 계산하지 않는다
        var chars = measure.characters;
        var lines = measure.lines;
        if (chars.Count == 0 || lines.Count == 0) { HideCaret(); return; }

        // 긴 글이라 칸이 일부만 보여 주면 자리를 알 수 없다 — 보이는 글의 끝에 둔다
        int len = f.text.Length;
        int at = t.text.Length == len ? Mathf.Clamp(bar.caretPosition, 0, len) : chars.Count - 1;
        at = Mathf.Min(at, chars.Count - 1);

        float ppu = t.pixelsPerUnit;
        var line = lines[LineOf(lines, at)];
        float x = Mathf.Min(chars[at].cursorPos.x / ppu, rt.rect.xMax);
        float top = line.topY / ppu, height = line.height / ppu;
        if (top <= rt.rect.yMin) { HideCaret(); return; }   // 칸 아래로 넘쳐 글도 안 보이는 줄

        if (caret == null)
        {
            var go = new GameObject("FieldCaret0926", typeof(RectTransform), typeof(Image));
            caret = go.GetComponent<Image>();
            caret.raycastTarget = false;
        }
        var c = caret.rectTransform;
        if (c.parent != rt)
        {
            c.SetParent(rt, false);
            c.anchorMin = c.anchorMax = rt.pivot;   // 칸 글의 기준점 — cursorPos 가 여기서부터 잰 자리다
            c.pivot = new Vector2(0f, 1f);
            caret.gameObject.layer = t.gameObject.layer;
        }
        var pos = new Vector2(x, top);
        var size = new Vector2(bar.caretWidth, height);
        if (c.anchoredPosition != pos) c.anchoredPosition = pos;
        if (c.sizeDelta != size) c.sizeDelta = size;
        caret.color = bar.caretColor;
        if (!caret.gameObject.activeSelf) caret.gameObject.SetActive(true);

        // 깜빡임은 Unity 칸과 같게 — 빠르기는 입력줄 설정, 치거나 옮기는 동안은 늘 켜져 있다
        int key = at * 100003 + len;
        if (key != caretKey) { caretKey = key; blinkStart = Time.unscaledTime; }
        float period = bar.caretBlinkRate > 0f ? 1f / bar.caretBlinkRate : 0f;
        caret.enabled = period <= 0f || (Time.unscaledTime - blinkStart) % period < period * 0.5f;
    }

    private void HideCaret()
    {
        if (caret != null && caret.gameObject.activeSelf) caret.gameObject.SetActive(false);
    }

    private static int LineOf(System.Collections.Generic.IList<UILineInfo> lines, int charIndex)
    {
        for (int i = 0; i < lines.Count - 1; i++)
            if (lines[i + 1].startCharIdx > charIndex) return i;
        return lines.Count - 1;
    }

    // 입력줄이 떠 있는 동안 — 원래 칸이 입력줄에 가리면 목록을 올리고, 위로 넘어가 있으면 내린다
    private readonly Vector3[] corners = new Vector3[4];

    private void KeepAbove(InputField f, RectTransform bar)
    {
        if (bar == null) return;
        var sr = currentScroll;
        var view = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
        float scrollable = sr.content.rect.height - view.rect.height;
        float scale = sr.content.lossyScale.y;
        if (scrollable <= 1f || scale <= 0f) return;

        ((RectTransform)f.transform).GetWorldCorners(corners);
        float fieldBottom = corners[0].y, fieldTop = corners[1].y;
        bar.GetWorldCorners(corners);
        float barTop = corners[1].y;
        view.GetWorldCorners(corners);
        float viewTop = corners[1].y;

        float g = gap * scale, delta = 0f;   // 목록을 위로 올릴 양 (월드)
        if (fieldBottom < barTop + g) delta = barTop + g - fieldBottom;
        else if (fieldTop > viewTop - g) delta = viewTop - g - fieldTop;
        if (Mathf.Abs(delta) < scale) return;

        float target = Mathf.Clamp01(sr.verticalNormalizedPosition - delta / scale / scrollable);
        if (Mathf.Abs(target - sr.verticalNormalizedPosition) < 0.0005f) return;   // 목록 끝 — 더 못 움직인다
        sr.velocity = Vector2.zero;
        sr.verticalNormalizedPosition = Mathf.Lerp(sr.verticalNormalizedPosition, target, Mathf.Clamp01(Time.unscaledDeltaTime * scrollSpeed));
    }
}
