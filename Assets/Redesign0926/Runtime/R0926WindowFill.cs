using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 창 안의 바탕 상자 · 글자를 창 모양(R0926WindowSkin)에 맞춘다 — 창이 스스로 훑어서 붙인다 (목록 줄처럼 나중에 생기는 것도).
/// 상자: 짙은 회색은 남색 쪽으로 (시안 74,74,82 → 58,70,104) · 물방울 모드면 바탕만 투명 (「버튼 바탕만 투명」 — 시안 --fa .4,
///       흰 버튼은 흰 .2 + 흰 테두리 1px). 원래 옅은 것(알파 .5 미만)은 그대로.
/// 글자 · 한 색 아이콘: 물방울 모드에서 투명해진 바탕 위 어두운 것만 흰색으로 — 알파는 그대로라 글자는 투명해지지 않는다.
/// 쪽: 창과 같은 색으로 창을 거의 다 덮는 바탕(지도 · 설정 쪽) — 기본 모양이 아니면 그리지 않는다 (창 바탕이 대신 보인다).
/// 지도 · 사진(RawImage) · 그림 아이콘 · 스위치 · 거리 막대는 건드리지 않는다.
/// 칸 불투명(R0926KeepSolid) 아래 상자는 물방울 모드에서도 제 알파 그대로 — 창 바탕만 비친다 (정보수정 · 추가 쪽 · 펼친 분류 목록 · 11-26 대표님).
/// 빈 칸(그림 없음)이라 상자로 붙었다가 사진이 들어온 칸은 사진이라 건드리지 않는다 (정보수정 사진이 옅게 비치던 것 · 11-26 대표님 6번).
/// </summary>
[DisallowMultipleComponent]
public class R0926WindowFill : BaseMeshEffect
{
    public enum Mode { Box, Ink, Page }

    private const float Yb = 0.07f;   // 창 바탕 밝기 (감마) — 투명해진 상자가 남색 위에서 브라우저(감마 섞기)와 같은 진하기로 보이게
    private static readonly string[] BoxSprites = { "square", "Square_Sero_white", "UISprite", "Background" };
    private static readonly Vector3[] corners = new Vector3[4];
    private static readonly Vector2[] centers = new Vector2[4], innerCenters = new Vector2[4];
    private static readonly float[] startAngle = { 180f, 270f, 0f, 90f };

    [System.NonSerialized] private Mode mode;
    [System.NonSerialized] private R0926WindowSkin owner;
    [System.NonSerialized] private Graphic g;
    [System.NonSerialized] private bool solid;

    public Mode FillMode => mode;
    public R0926WindowSkin Owner => owner;

    /// <summary>창이 훑다가 하나씩 — 붙일 것이면 붙여서 돌려준다 (아니면 null)</summary>
    public static R0926WindowFill Enroll(Graphic g, R0926WindowSkin owner, Color windowColor, Rect windowRect)
    {
        if (g == null || owner == null || g.GetComponent<R0926WindowFill>() != null) return null;
        if (g.GetComponent<R0926WindowSkin>() != null || g.GetComponent<R0926Glass>() != null) return null;
        // 지도 · 거리 막대 · 따로 둔 것 · 안쪽 창(제 것을 스스로 맞춘다)은 건드리지 않는다
        for (var t = g.transform; t != null && t != owner.transform; t = t.parent)
        {
            if (t.GetComponent<R0926FriendsMap>() != null || t.GetComponent<R0926MapTiles>() != null
                || t.GetComponent<Slider>() != null || t.GetComponent<R0926SkinIgnore>() != null) return null;
            if (t != g.transform && t.TryGetComponent<R0926WindowSkin>(out var inner) && inner.SkinKind == R0926WindowSkin.Kind.Window) return null;
        }
        Mode m;
        if (g is Text) m = Mode.Ink;
        else if (g is Image im)
        {
            if (im.type == Image.Type.Filled) return null;
            var sp = im.sprite;
            string n = sp != null ? sp.name : "";
            if (n.EndsWith("_ring")) return null;
            if (n.StartsWith("r0926_i_") || n.StartsWith("r0926_c_")) m = Mode.Ink;   // 아이콘 — 어둡게 칠한 것만 글자처럼
            else if (sp == null || im.type == Image.Type.Sliced || im.type == Image.Type.Tiled || System.Array.IndexOf(BoxSprites, n) >= 0)
                // 마스크 그림은 쪽으로 보지 않는다 — 안 그리면 마스크가 비어 안의 사진 · 캐릭터까지 다 가려진다 (정보 수정 AI 미리보기 · 11-24)
                m = IsPage(im, windowColor, windowRect) && im.GetComponent<Mask>() == null ? Mode.Page : Mode.Box;
            else return null;
        }
        else return null;   // RawImage(사진 · 지도) 등
        var f = g.gameObject.AddComponent<R0926WindowFill>();
        f.mode = m;
        f.owner = owner;
        f.g = g;
        f.solid = g.GetComponentInParent<R0926KeepSolid>(true) != null;
        f.Touch();
        return f;
    }

    private static bool IsPage(Image im, Color wc, Rect wr)
    {
        var c = im.color;
        if (c.a < 0.9f || Mathf.Abs(c.r - wc.r) + Mathf.Abs(c.g - wc.g) + Mathf.Abs(c.b - wc.b) >= 0.05f) return false;
        im.rectTransform.GetWorldCorners(corners);
        return corners[2].x - corners[0].x >= 0.6f * wr.width && corners[2].y - corners[0].y >= 0.4f * wr.height;
    }

    public void Touch()
    {
        if (g != null) g.SetVerticesDirty();
    }

    public static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    /// <summary>
    /// 감마 공간에서 알파 a 로 섞은 것(브라우저)과 같게 보이는 선형 공간 알파 — 바탕 밝기 Yb 위에서 밝기 yc 인 색
    /// </summary>
    public static float Comp(float a, float yc)
    {
        a = Mathf.Clamp01(a);
        float t = yc * a + Yb * (1f - a);
        float l = Mathf.Pow(t, 2.2f), cl = Mathf.Pow(yc, 2.2f), bl = Mathf.Pow(Yb, 2.2f);
        if (Mathf.Abs(cl - bl) < 1e-4f) return a;
        return Mathf.Clamp01((l - bl) / (cl - bl));
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || g == null || owner == null || !owner.Showing) return;
        bool drop = R0926MenuStyle.Droplet;
        switch (mode)
        {
            case Mode.Page: vh.Clear(); return;
            case Mode.Ink: if (drop) Ink(vh); return;
            default: Box(vh, drop); return;
        }
    }

    private void Box(VertexHelper vh, bool drop)
    {
        if (IsPhoto(g as Image)) return;
        Color c = g.color;
        float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        bool darkGray = mx - mn <= 0.06f && mx <= 0.45f;
        bool clear = drop && c.a >= 0.5f && !solid;   // 칸 불투명(R0926KeepSolid) 아래는 제 알파 그대로
        bool white = clear && mn >= 0.82f;
        if (!darkGray && !clear) return;
        Color tc = c;
        if (darkGray) tc = new Color(c.r * 0.78f, c.g * 0.95f, Mathf.Min(1f, c.b * 1.27f), c.a);
        if (white) tc = new Color(1f, 1f, 1f, Comp(c.a * 0.25f, 1f));        // 흰 버튼 = 흰 .2 남짓 + 흰 테두리 (시안 .cta)
        else if (clear) tc.a = Comp(c.a * 0.4f, Lum(tc));                   // 나머지 = 제 색 .4 (시안 --fa)
        float ka = tc.a / Mathf.Max(c.a, 1e-4f);
        var v = new UIVertex();
        int n = vh.currentVertCount;
        for (int i = 0; i < n; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            Color32 o = v.color;
            v.color = new Color(tc.r, tc.g, tc.b, o.a / 255f * ka);
            vh.SetUIVertex(v, i);
        }
        if (white && g is Image im) Ring(vh, im, Comp(0.55f * c.a, 1f));
    }

    // 흰 버튼 둘레 1px 흰 선 (inset 0 0 0 1px rgba(255,255,255,.55))
    private static void Ring(VertexHelper vh, Image im, float alpha)
    {
        Rect r = im.rectTransform.rect;
        float hw = r.width * 0.5f, hh = r.height * 0.5f;
        if (hw <= 0f || hh <= 0f) return;
        float t = Mathf.Min(R0926WindowSkin.Px, Mathf.Min(hw, hh));
        float rad = R0926WindowSkin.Radius(im, hw, hh);
        float ri = Mathf.Max(rad - t, 0f), ci = t + ri;
        Vector2 uv = new Vector2(0.5f, 0.5f);
        if (im.sprite != null)
        {
            var iu = im.type == Image.Type.Sliced || im.type == Image.Type.Tiled
                ? UnityEngine.Sprites.DataUtility.GetInnerUV(im.sprite) : UnityEngine.Sprites.DataUtility.GetOuterUV(im.sprite);
            uv = new Vector2((iu.x + iu.z) * 0.5f, (iu.y + iu.w) * 0.5f);
        }
        centers[0] = new Vector2(r.xMin + rad, r.yMin + rad); innerCenters[0] = new Vector2(r.xMin + ci, r.yMin + ci);
        centers[1] = new Vector2(r.xMax - rad, r.yMin + rad); innerCenters[1] = new Vector2(r.xMax - ci, r.yMin + ci);
        centers[2] = new Vector2(r.xMax - rad, r.yMax - rad); innerCenters[2] = new Vector2(r.xMax - ci, r.yMax - ci);
        centers[3] = new Vector2(r.xMin + rad, r.yMax - rad); innerCenters[3] = new Vector2(r.xMin + ci, r.yMax - ci);
        const int seg = 6;
        int start = vh.currentVertCount;
        var v = UIVertex.simpleVert;
        v.color = new Color(1f, 1f, 1f, alpha);
        v.uv0 = uv;
        for (int k = 0; k < 4; k++)
            for (int s = 0; s <= seg; s++)
            {
                float ang = (startAngle[k] + 90f * s / seg) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                v.position = centers[k] + dir * rad;
                vh.AddVert(v);
                v.position = innerCenters[k] + dir * ri;
                vh.AddVert(v);
            }
        int count = 4 * (seg + 1);
        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            int o0 = start + 2 * i, i0 = o0 + 1, o1 = start + 2 * j, i1 = o1 + 1;
            vh.AddTriangle(o0, o1, i1);
            vh.AddTriangle(i1, i0, o0);
        }
    }

    // 어두운 글자 · 아이콘 — 바로 뒤 바탕이 투명해진 상자일 때만 흰색 (알파 그대로)
    private void Ink(VertexHelper vh)
    {
        Color c = g.color;
        if (Lum(c) >= 0.4f || !OnClearBox()) return;
        Color32 b = c;
        var v = new UIVertex();
        int n = vh.currentVertCount;
        for (int i = 0; i < n; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            Color32 o = v.color;
            if (Mathf.Abs(o.r - b.r) > 2 || Mathf.Abs(o.g - b.g) > 2 || Mathf.Abs(o.b - b.b) > 2) continue;   // 글자 속 다른 색(태그)은 그대로
            v.color = new Color32(255, 255, 255, o.a);
            vh.SetUIVertex(v, i);
        }
    }

    // 바로 뒤 바탕 = 위로 올라가며 처음 보이는 그림 (같은 부모의 앞 형제 중 겹치는 상자 포함)
    private bool OnClearBox()
    {
        var self = g.rectTransform;
        self.GetWorldCorners(corners);
        Vector2 mid = (corners[0] + corners[2]) * 0.5f;
        var p = transform.parent;
        if (p != null)
            for (int i = transform.GetSiblingIndex() - 1; i >= 0; i--)
            {
                var s = p.GetChild(i);
                if (!s.gameObject.activeInHierarchy || !s.TryGetComponent<Image>(out var si) || !si.enabled || si.color.a < 0.05f) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(si.rectTransform, mid, null)) continue;
                return IsClearBox(si);
            }
        for (var t = p; t != null; t = t.parent)
        {
            if (owner != null && t == owner.transform) return false;   // 창 바탕 위 — 그대로
            if (!t.TryGetComponent<Image>(out var im) || !im.enabled || im.color.a < 0.05f) continue;
            return IsClearBox(im);
        }
        return false;
    }

    private static bool IsClearBox(Image im)
    {
        return im.TryGetComponent<R0926WindowFill>(out var f) && f.mode == Mode.Box && !f.solid && !IsPhoto(im) && im.color.a >= 0.5f;
    }

    // 빈 칸(그림 없음)이라 상자로 붙었는데 나중에 사진이 들어온 것 — 훑을 때라면 붙이지 않았을 그림(늘리지 않는 · 상자 그림 아닌 것)이면 사진으로 본다
    private static bool IsPhoto(Image im) => im != null && im.sprite != null && im.type != Image.Type.Sliced && im.type != Image.Type.Tiled
                                             && System.Array.IndexOf(BoxSprites, im.sprite.name) < 0;
}
