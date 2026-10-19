using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 지금 쓰고 있는 입력칸에 분홍 테두리 (2026-10-16 대표님: "inputfield 내에서도 작성되고 있구나 라는 느낌이 들도록 ·
/// 입력하면서도 잘 보일 수 있도록 적절한 위치에 스크롤").
/// 장소·보물찾기 칸은 키보드 위 입력줄(UploadInputMirror)로 쓴다 — 그때는 원래 칸에 테두리를 두고,
/// 그 칸이 입력줄 위로 보이게 칸이 든 목록을 부드럽게 올린다(입력줄이 떠 있는 동안 뒤 화면은 손으로 못 움직인다).
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
    private ScrollRect currentScroll;
    private Image ring;

    void Update()
    {
        var f = Active(out var via);
        if (f != current)
        {
            current = f;
            currentScroll = f != null ? VerticalScrollOf(f) : null;
            Place(f);
        }
        if (f != null && via != null && currentScroll != null) KeepAbove(f, via.PlacedBar);
    }

    void OnDisable()
    {
        current = null;
        currentScroll = null;
        if (ring != null) ring.gameObject.SetActive(false);
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
