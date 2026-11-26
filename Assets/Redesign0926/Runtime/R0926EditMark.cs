using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「수정 → 저장」 표시 (2026-11-17 대표님 규칙 · docs/claude/unity-app.md) — 저장 화면에서
/// 고친 칸의 테두리를 핑크(#F06595 + 옅은 바깥 링)로 바꾸고, 저장 버튼을 핑크(#E91E63 · 흰 글자)로 바꿔 1.5초 주기로 숨 쉬게(1 → 1.06) 한다.
/// 고치지 않은 칸은 그대로. 저장되면(창이 닫히거나 다시 열릴 때) Clear — 저장이 안 되면(오류·검증 실패) 그대로 둔다.
/// 모양 기준 = 농민닷컴 판매자센터 상품관리(.pe-sec.dirty · .save-btn) · 우기 웹앱 markEdit.
/// 화면 쪽(R0926ProfileEdit 등)이 칸마다 Set(칸, 고쳤나)를 부른다 — 칸은 r0926_pill 바탕(입력칸·고르기 칸).
/// </summary>
public class R0926EditMark : MonoBehaviour
{
    [SerializeField] private Sprite ringSprite;   // r0926_pill_ring — r0926_pill 과 같은 9-slice (테두리 6px)
    [SerializeField] private Image saveImage;
    [SerializeField] private Text saveLabel;
    [SerializeField] private Color edgeColor = new Color(0.941f, 0.396f, 0.584f, 1f);     // #F06595
    [SerializeField] private Color haloColor = new Color(0.914f, 0.118f, 0.388f, 0.14f);  // rgba(233,30,99,.14)
    [SerializeField] private Color buttonColor = new Color(0.914f, 0.118f, 0.388f, 1f);   // #E91E63
    [SerializeField] private Color buttonInk = Color.white;
    [SerializeField] private float haloWidth = 5f;     // 바깥 링 두께 (캔버스 단위)
    [SerializeField] private float period = 1.5f;      // 숨 쉬는 주기 (초)
    [SerializeField] private float breathe = 1.06f;    // 가장 커질 때 배율

    private const float SpriteRadius = 63f;   // 9-slice 테두리 = 모서리 둥글기(그림 픽셀)

    private class Mark { public Image edge, halo; }
    private readonly Dictionary<RectTransform, Mark> marks = new Dictionary<RectTransform, Mark>();
    private readonly HashSet<RectTransform> on = new HashSet<RectTransform>();
    private bool pulsing;
    private Color baseFill, baseInk;
    private Vector2 basePos;
    private float t0;

    public bool Any => on.Count > 0;

    /// <summary>칸 하나의 고침 표시 — 같은 값이면 아무것도 안 한다(매 프레임 불러도 된다)</summary>
    public void Set(RectTransform area, bool dirty)
    {
        if (area == null) return;
        if (dirty == on.Contains(area)) return;
        if (dirty) on.Add(area); else on.Remove(area);
        var m = dirty ? Ensure(area) : (marks.TryGetValue(area, out var have) ? have : null);
        if (m != null)
        {
            if (m.edge != null) m.edge.gameObject.SetActive(dirty);
            if (m.halo != null) m.halo.gameObject.SetActive(dirty);
        }
        Pulse(on.Count > 0);
    }

    /// <summary>모두 지운다 — 저장됐을 때 · 화면을 다시 열 때</summary>
    public void Clear()
    {
        foreach (var m in marks.Values)
        {
            if (m.edge != null) m.edge.gameObject.SetActive(false);
            if (m.halo != null) m.halo.gameObject.SetActive(false);
        }
        on.Clear();
        Pulse(false);
    }

    void OnDisable() => Clear();

    void Update()
    {
        if (!pulsing || saveImage == null) return;
        // 1 → breathe → 1 을 부드럽게 (코사인) — 버튼 가운데를 그대로 두고 키운다
        float k = 0.5f - 0.5f * Mathf.Cos((Time.unscaledTime - t0) / Mathf.Max(0.2f, period) * Mathf.PI * 2f);
        float s = 1f + (breathe - 1f) * k;
        var rt = saveImage.rectTransform;
        rt.localScale = new Vector3(s, s, 1f);
        rt.anchoredPosition = basePos + Vector2.Scale(rt.pivot - new Vector2(0.5f, 0.5f), rt.rect.size) * (s - 1f);
    }

    private void Pulse(bool want)
    {
        if (want == pulsing || saveImage == null) return;
        pulsing = want;
        var rt = saveImage.rectTransform;
        if (want)
        {
            baseFill = saveImage.color;
            baseInk = saveLabel != null ? saveLabel.color : Color.white;
            basePos = rt.anchoredPosition;
            saveImage.color = buttonColor;
            if (saveLabel != null) saveLabel.color = buttonInk;
            t0 = Time.unscaledTime;
            return;
        }
        saveImage.color = baseFill;
        if (saveLabel != null) saveLabel.color = baseInk;
        rt.localScale = Vector3.one;
        rt.anchoredPosition = basePos;
    }

    // 칸 위 테두리 두 겹 — 안쪽 핑크 선(칸 가장자리) + 바깥 옅은 링. 바탕 바로 위 · 글 아래
    private Mark Ensure(RectTransform area)
    {
        if (marks.TryGetValue(area, out var m) && m.edge != null) return m;
        var bg = area.GetComponent<Image>();
        float mul = bg != null && bg.type == Image.Type.Sliced ? Mathf.Max(0.01f, bg.pixelsPerUnitMultiplier) : 2f;
        float r = SpriteRadius / mul;   // 칸 모서리 둥글기 (캔버스 단위)
        m = new Mark
        {
            halo = Ring(area, "EditHalo0926", haloColor, -haloWidth, SpriteRadius / (r + haloWidth)),
            edge = Ring(area, "EditEdge0926", edgeColor, 0f, mul),
        };
        marks[area] = m;
        return m;
    }

    private Image Ring(RectTransform area, string name, Color c, float inset, float mul)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        go.layer = area.gameObject.layer;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = ringSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = mul;
        img.color = c;
        var rt = img.rectTransform;
        rt.SetParent(area, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        rt.localScale = Vector3.one;
        go.SetActive(false);
        return img;
    }
}
