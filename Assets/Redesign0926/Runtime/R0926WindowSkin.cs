using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 창 바탕 · 네온 테 · 목록 강조 줄 (S8 · 11-23 대표님 「디스플레이 선택」 — 물방울 모드 · 네온 모드를 따로 켜고 끈다).
/// 창: 둘 다 끄면 예전 불투명 회색 그대로. 아니면 남색(창 배경 진하기 50/75/90) — 물방울이면 뒤 AR 화면이 흐리게 비치고 가장자리 빛,
///     네온이면 흰 선 하나 + 바깥 빛 (안쪽 두 번째 선은 없다 — 「그냥 외곽선 하나만 네온으로」).
///     창 안의 상자 · 글자는 R0926WindowFill 이 맞춘다(이 창이 훑어서 붙인다). 지도 · 사진 · 글자의 투명도는 그대로다.
///     다른 창 위에 겹쳐 뜬 창은 뒤를 비추지 않고 불투명 남색 (비추는 건 AR 화면이라 밑 창에 구멍이 난 것처럼 보인다).
/// 테: 도크 · 알림 알약 둘레의 네온 — 네온 모드에서만, 아니면 아무것도 그리지 않는다.
/// 줄: 목록 강조 줄(보물 · 축제 · 대피소 · 서로 아는 사람)의 네온 — 네온 모드에서만.
/// 모양 · 빛은 셰이더(R0926WindowSkin)가 계산한다 — 정점에 크기 · 모서리 · 플래그를 싣는다 (uv1 · uv2 · uv3).
/// </summary>
[RequireComponent(typeof(Image))]
public class R0926WindowSkin : BaseMeshEffect
{
    public enum Kind { Window, Rim, Row }

    /// <summary>CSS 1px (아이폰 1pt) = 캔버스 3.75 단위 (1440×3200 · 맞춤 0.5)</summary>
    public const float Px = 3.75f;
    /// <summary>네온 기본 빛 — 시안 --g 125,185,255</summary>
    public static readonly Color Blue = new Color(125f / 255f, 185f / 255f, 1f, 1f);

    [SerializeField] private Kind kind = Kind.Window;
    [SerializeField] private Material skin;            // R0926WindowSkin.mat
    [SerializeField] private Shader blur;              // R0926GlassBlur.shader — 빌드에 들어가도록 여기서 잡아 둔다
    [SerializeField] private float tint = 1f;          // 원래 그림의 알파 — 사라지는 중인지는 이 값에 대한 비율로 읽는다
    [SerializeField] private Color glow = new Color(125f / 255f, 185f / 255f, 1f, 1f);
    [SerializeField] private Image shapeFrom;          // 테: 모서리 모양을 따올 그림 (도크 · 알림 알약)
    [SerializeField] private bool fillContent = true;  // 창: 안의 상자 · 글자를 모드에 맞춘다
    [SerializeField] private GameObject[] covers;      // 창: 이 중 하나가 켜져 있으면 뒤가 AR 화면이 아니다 (이 창보다 먼저 그려지는 큰 창)

    private static readonly List<R0926WindowSkin> windows = new List<R0926WindowSkin>();
    private static readonly List<Canvas> canvases = new List<Canvas>();
    private static readonly List<Graphic> scanBuf = new List<Graphic>();
    private static readonly List<int> pathA = new List<int>(), pathB = new List<int>();
    private static readonly Vector3[] corners = new Vector3[4];

    private Image img;
    private Material before;
    private bool on, see, covered, overlay, stacked;
    private float shadeA, nextScan;
    private readonly List<Graphic> shades = new List<Graphic>();   // 창 뒤에 깔린 어두운 막 (뿌리 쪽 · 앞 형제)
    private readonly HashSet<int> seen = new HashSet<int>();
    private readonly List<R0926WindowFill> fills = new List<R0926WindowFill>();

    private RectTransform Rt => (RectTransform)transform;

    public Kind SkinKind => kind;
    public bool Showing => on;
    public bool SeeThrough => on && see;
    public bool Covered => on && covered;
    /// <summary>밑에 꼭 같은 자리 · 크기의 창이 있다 — 그 창을 통째로 덮는 안쪽 화면 (정보 수정 AI 화면)</summary>
    public bool Stacked => on && stacked;
    public float ShadeAlpha => shadeA;
    public int FillCount => fills.Count;
    public static IReadOnlyList<R0926WindowSkin> Windows => windows;

    /// <summary>네온 빛 색 — 목록 창은 고른 칩 색 (R0926ListGlow) · 줄은 그 줄 색</summary>
    public Color Glow
    {
        get => glow;
        set
        {
            if (glow == value) return;
            glow = value;
            if (on && img != null) img.SetVerticesDirty();
        }
    }

    /// <summary>실행 중에 만든 것 (목록 강조 줄) — 종류 · 재질을 정하고 다시 맞춘다</summary>
    public void Setup(Kind k, Material m)
    {
        kind = k;
        skin = m;
        fillContent = k == Kind.Window;
        if (Application.isPlaying && isActiveAndEnabled) Restyle();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => windows.Clear();

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!Application.isPlaying) return;   // 에디터에서 씬에 재질이 저장되지 않게
        img = GetComponent<Image>();
        R0926MenuStyle.Changed += Restyle;
        if (kind == Kind.Window) FindShades();
        Restyle();
    }

    protected override void OnDisable()
    {
        if (Application.isPlaying)
        {
            R0926MenuStyle.Changed -= Restyle;
            SetOn(false);
            TouchFills();
        }
        base.OnDisable();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        if (Application.isPlaying && isActiveAndEnabled) Restyle();
    }

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        if (Application.isPlaying && isActiveAndEnabled) Restyle();
    }

    // img.canvas 는 옮기기 전 캔버스가 남아 있을 수 있다 — 지금 위의 켜진 캔버스를 직접 찾는다 (R0926Glass 와 같게)
    private Canvas CurrentCanvas()
    {
        Canvas c = null;
        GetComponentsInParent(false, canvases);
        foreach (var cv in canvases) if (cv.isActiveAndEnabled) { c = cv; break; }
        canvases.Clear();
        return c;
    }

    private void Restyle()
    {
        if (img == null) return;
        var c = CurrentCanvas();
        overlay = c != null && c.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
        bool want = skin != null && c != null && (kind == Kind.Window ? !R0926MenuStyle.Classic : R0926MenuStyle.Neon);
        SetOn(want);
        img.SetVerticesDirty();
        if (kind != Kind.Window) return;
        if (on && fillContent) Scan();
        TouchFills();
    }

    private void SetOn(bool v)
    {
        if (img == null || on == v) return;
        on = v;
        if (v)
        {
            before = img.material == img.defaultMaterial ? null : img.material;
            // uv1 · uv2 · uv3 를 셰이더로 보내려면 캔버스가 그 채널을 실어야 한다 — 안쪽 캔버스부터 맨 위까지
            for (var cv = CurrentCanvas(); cv != null; cv = cv.transform.parent != null ? cv.transform.parent.GetComponentInParent<Canvas>() : null)
                cv.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
            img.material = skin;
            if (kind == Kind.Window && !windows.Contains(this)) windows.Add(this);
        }
        else
        {
            SetSee(false);
            covered = stacked = false;
            windows.Remove(this);
            img.material = before;
        }
    }

    private void SetSee(bool v)
    {
        if (see == v) return;
        see = v;
        if (v) R0926GlassBlur.Need(this, blur, true);
        else R0926GlassBlur.Release(this);
        if (img != null) img.SetVerticesDirty();
    }

    private bool Visible => on && isActiveAndEnabled && img.enabled && !img.canvasRenderer.cull
                            && img.color.a * img.canvasRenderer.GetInheritedAlpha() >= 0.05f;

    private void LateUpdate()
    {
        if (!on || kind != Kind.Window) return;
        bool vis = Visible;
        // 밑 창을 통째로 덮는 안쪽 화면 — 밑 창 모양을 그대로 이어 그린다 (밑 창이 비치면 같은 흐린 AR 화면). 어느 쪽이든 불투명 —
        // 가림(불투명 남색)을 늘 같이 켜 두어 흐린 화면이 아직 없을 때도 밑 창의 칸들이 비치지 않게. 바깥 빛은 밑 창 것만 (두 번 그리면 진해진다)
        var twin = vis ? Twin() : null;
        bool st = twin != null;
        bool cov = st || vis && (CoverOn() || OverWindow());
        if (cov != covered || st != stacked) { covered = cov; stacked = st; img.SetVerticesDirty(); }
        SetSee(vis && overlay && R0926MenuStyle.Droplet && (st ? twin.SeeThrough : !covered));
        float sa = ReadShade();
        if (Mathf.Abs(sa - shadeA) > 0.003f) { shadeA = sa; img.SetVerticesDirty(); }
        if (fillContent && Time.unscaledTime >= nextScan) Scan();
    }

    private bool CoverOn()
    {
        if (covers == null) return false;
        foreach (var o in covers) if (o != null && o.activeInHierarchy) return true;
        return false;
    }

    // 먼저 그려진(밑에 깔린) 다른 창과 겹치면 뒤가 AR 화면이 아니다
    private bool OverWindow()
    {
        Rect me = WorldRect(Rt);
        foreach (var w in windows)
        {
            if (w == null || w == this || !w.Visible) continue;
            if (!WorldRect(w.Rt).Overlaps(me)) continue;
            if (DrawnBefore(w.transform, transform)) return true;
        }
        return false;
    }

    // 밑에 깔린 창 중 꼭 같은 자리 · 크기인 것 (2 캔버스 단위 안)
    private R0926WindowSkin Twin()
    {
        Rect me = WorldRect(Rt);
        float tol = 2f * Mathf.Max(1e-4f, Mathf.Abs(Rt.lossyScale.y));
        foreach (var w in windows)
        {
            if (w == null || w == this || !w.Visible) continue;
            Rect o = WorldRect(w.Rt);
            if (Mathf.Abs(o.xMin - me.xMin) > tol || Mathf.Abs(o.xMax - me.xMax) > tol
                || Mathf.Abs(o.yMin - me.yMin) > tol || Mathf.Abs(o.yMax - me.yMax) > tol) continue;
            if (DrawnBefore(w.transform, transform)) return w;
        }
        return null;
    }

    private static Rect WorldRect(RectTransform rt)
    {
        rt.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private static int SortOrder(Transform t)
    {
        for (var cv = t.GetComponentInParent<Canvas>(); cv != null; cv = cv.transform.parent != null ? cv.transform.parent.GetComponentInParent<Canvas>() : null)
            if (cv.overrideSorting || cv.isRootCanvas) return cv.sortingOrder;
        return 0;
    }

    private static void Path(Transform t, List<int> p)
    {
        p.Clear();
        for (; t != null; t = t.parent) p.Add(t.GetSiblingIndex());
        p.Reverse();
    }

    /// <summary>a 가 b 보다 먼저(밑에) 그려진다 — 정렬 순서가 같으면 위계 순서 (조상이 먼저)</summary>
    public static bool DrawnBefore(Transform a, Transform b)
    {
        int sa = SortOrder(a), sb = SortOrder(b);
        if (sa != sb) return sa < sb;
        Path(a, pathA);
        Path(b, pathB);
        int n = Mathf.Min(pathA.Count, pathB.Count);
        for (int i = 0; i < n; i++) if (pathA[i] != pathB[i]) return pathA[i] < pathB[i];
        return pathA.Count < pathB.Count;
    }

    // 창 뒤 어두운 막 — 뿌리 쪽 바탕과 그 앞 형제 중 화면을 거의 다 덮는 어두운 그림. 비치는 AR 화면도 둘레와 같은 어둡기로
    private void FindShades()
    {
        shades.Clear();
        var root = transform.root;
        for (Transform t = transform; t != null; t = t.parent)
        {
            if (t != transform && t.TryGetComponent<Graphic>(out var g) && IsShade(g)) shades.Add(g);
            var p = t.parent;
            if (p != null)
                for (int i = 0; i < t.GetSiblingIndex(); i++)
                    if (p.GetChild(i).TryGetComponent<Graphic>(out var s) && IsShade(s)) shades.Add(s);
            if (t.TryGetComponent<Canvas>(out var cv) && cv.isRootCanvas) break;
            if (t == root) break;
        }
    }

    private static bool IsShade(Graphic g)
    {
        if (!(g is Image)) return false;
        var c = g.color;
        if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) > 0.15f || c.a > 0.95f) return false;
        var cv = g.canvas != null ? g.canvas.rootCanvas : g.GetComponentInParent<Canvas>(true);
        if (cv == null) return false;
        Rect cr = WorldRect((RectTransform)cv.transform), gr = WorldRect(g.rectTransform);
        float ix = Mathf.Max(0f, Mathf.Min(cr.xMax, gr.xMax) - Mathf.Max(cr.xMin, gr.xMin));
        float iy = Mathf.Max(0f, Mathf.Min(cr.yMax, gr.yMax) - Mathf.Max(cr.yMin, gr.yMin));
        return ix * iy >= 0.8f * cr.width * cr.height;
    }

    private float ReadShade()
    {
        float keep = 1f;
        bool dockDone = false;
        foreach (var g in shades)
        {
            if (g == null) continue;
            // 도크로 여는 창의 바탕은 도크 바로 아래 한 장(R0926DockDim)이 대신 그린다 — 한 번만 센다
            bool dock = g.GetComponent<R0926DockDim>() != null || (R0926DockDim.Live && g.GetComponent<R0926DimSource>() != null);
            if (dock)
            {
                if (!dockDone && R0926DockDim.Current != null) keep *= 1f - Mathf.Clamp01(R0926DockDim.Current.Alpha);
                dockDone = true;
                continue;
            }
            if (!g.isActiveAndEnabled) continue;
            keep *= 1f - Mathf.Clamp01(g.color.a * g.canvasRenderer.GetInheritedAlpha());
        }
        return 1f - keep;
    }

    // 창 안의 상자 · 글자를 훑어 붙인다 — 목록 줄처럼 나중에 생기는 것도 (0.3초마다)
    private void Scan()
    {
        nextScan = Time.unscaledTime + 0.3f;
        if (img == null) return;
        if (seen.Count > 4096) seen.Clear();
        Rect wr = WorldRect(Rt);
        GetComponentsInChildren(true, scanBuf);
        foreach (var g in scanBuf)
        {
            if (g == null || g == img) continue;
            if (!seen.Add(g.GetInstanceID())) continue;
            var f = R0926WindowFill.Enroll(g, this, img.color, wr);
            if (f != null) fills.Add(f);
        }
        scanBuf.Clear();
    }

    private void TouchFills()
    {
        for (int i = fills.Count - 1; i >= 0; i--)
        {
            if (fills[i] == null) { fills.RemoveAt(i); continue; }
            fills[i].Touch();
        }
    }

    /// <summary>모서리 반지름 (캔버스 단위) — 늘린 알약(Sliced)은 그림 모서리 크기, 동그란 그림은 끝까지 둥글게</summary>
    public static float Radius(Image im, float hw, float hh)
    {
        float lim = Mathf.Max(0f, Mathf.Min(hw, hh));
        if (im == null || im.sprite == null) return 0f;
        if (im.type == Image.Type.Sliced || im.type == Image.Type.Tiled)
        {
            float ppu = im.pixelsPerUnit * im.pixelsPerUnitMultiplier;
            return ppu > 0f && im.sprite.border.x > 0f ? Mathf.Min(lim, im.sprite.border.x / ppu) : 0f;
        }
        string n = im.sprite.name;
        return n.Contains("circle") || n.Contains("pill") ? lim : 0f;
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!on || img == null || !IsActive())
        {
            if (kind != Kind.Window) vh.Clear();   // 테 · 줄은 네온일 때만 그린다
            return;
        }
        Rect r = ((RectTransform)transform).rect;
        float hw = r.width * 0.5f, hh = r.height * 0.5f;
        vh.Clear();
        if (hw <= 0f || hh <= 0f) return;
        Vector2 mid = r.center;
        bool neon = R0926MenuStyle.Neon;
        float rad = Radius(kind == Kind.Rim && shapeFrom != null ? shapeFrom : img, hw, hh);
        float pad = (kind == Kind.Window ? (stacked ? 0f : neon ? 36f : 1.5f) : kind == Kind.Rim ? 30f : 16f) * Px;   // 밑 창과 같은 자리면 바깥 빛은 밑 창 것만
        float flags = kind == Kind.Row ? 16f : kind == Kind.Rim ? 32f
            : (see ? 1f : 0f) + (R0926MenuStyle.Droplet ? 2f : 0f) + (neon ? 4f : 0f) + (covered ? 8f : 0f);
        var size = new Vector4(hw, hh, rad, tint > 0f ? tint : Mathf.Max(img.color.a, 0.01f));
        var extra = new Vector4(R0926MenuStyle.Density / 100f, shadeA, 0f, 0f);
        Color32 col = new Color(glow.r, glow.g, glow.b, img.color.a);

        var v = UIVertex.simpleVert;
        v.color = col;
        v.uv2 = size;
        v.uv3 = extra;
        for (int i = 0; i < 4; i++)
        {
            float x = i == 0 || i == 1 ? r.xMin - pad : r.xMax + pad;
            float y = i == 0 || i == 3 ? r.yMin - pad : r.yMax + pad;
            v.position = new Vector3(x, y, 0f);
            v.uv0 = Vector4.zero;
            v.uv1 = new Vector4(x - mid.x, y - mid.y, flags, Px);
            vh.AddVert(v);
        }
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}
