using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시작화면 렌즈의 빛 (10-18 대표님 「원 안팎 대비를 세게 · 원이 뻔하다 — 얇게, 가운데는 진한 흰색, 경계로 갈수록 투명하게」).
/// ① 렌즈 밖 그늘 — 렌즈 가장자리에서 부드럽게 차올라 바깥(옅은 문양·성운·별)을 어둡게 덮는다. 렌즈 안은 그대로 밝고,
///    가장자리 바로 바깥은 덜 덮여 렌즈에서 빛이 번져 나오는 것처럼 보인다.
/// ② 유리 테두리 — 가운데 줄만 진한 흰색이고 안팎으로 투명해진다. 빛을 받은 왼쪽 위가 조금 더 밝고,
///    반짝임 하나가 위쪽 테두리를 따라 렌즈가 가는 쪽으로 미끄러진다. 렌즈가 퍼질 때는 살짝 번지며 사라진다.
/// 예전 흰 링 그림은 렌즈와 함께 커져 퍼질 때 굵은 회색 띠가 됐다 — 메시로 그려 굵기가 늘 같다. 그리기 1번.
/// 값은 R0926Splash 가 매 프레임 넣는다. ExecuteAlways — 에디터 캡처에서도 그려지게 (Graphic 의 ExecuteAlways 는 물려받지 않는다).
/// CanvasRenderer 는 Image 처럼 처음부터 붙여 둔다 — Graphic 이 늦게 붙이는 길은 에디터에서 GetComponent 가 '가짜 null' 을 돌려줘 안 붙는다
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class R0926LensFx : MaskableGraphic
{
    [SerializeField] private int segments = 200;
    [Tooltip("진한 흰 줄의 반폭 (캔버스 단위 — 기준 폭 1440)")]
    [SerializeField] private float coreHalf = 2.4f;
    [Tooltip("흰 줄에서 투명해지기까지의 폭 (안팎 각각)")]
    [SerializeField] private float feather = 11f;
    [Tooltip("테두리 안쪽의 은은한 빛 폭")]
    [SerializeField] private float innerGlow = 70f;
    [SerializeField, Range(0f, 0.3f)] private float innerGlowAlpha = 0.07f;
    [Tooltip("그늘이 렌즈 가장자리에서 다 차오르기까지의 폭 — 이 띠만큼 렌즈 빛이 바깥으로 번져 보인다")]
    [SerializeField] private float shadeFeather = 120f;

    [System.NonSerialized] public Vector2 center;
    [System.NonSerialized] public float radius;
    [System.NonSerialized] public float shade;          // 렌즈 밖 그늘 진하기 0~1
    [System.NonSerialized] public Color shadeColor = Color.black;
    [System.NonSerialized] public float ringAlpha;
    [System.NonSerialized] public float glint = -1f;    // 반짝임 진행 0~1 (음수 = 없음)
    [System.NonSerialized] public float spread;         // 퍼질 때 번지는 정도 0~1

    // 테두리 단면 — 흰 줄 가운데로부터의 거리 비율. 가운데(±0.12)는 진한 흰색, 바깥으로 갈수록 빨리 옅어진다
    private static readonly float[] ProfileAt = { -1f, -0.62f, -0.36f, -0.12f, 0.12f, 0.36f, 0.62f, 1f };

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void Refresh() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (radius <= 0f) return;
        Rect r = rectTransform.rect;
        float far = 0f;
        far = Mathf.Max(far, (new Vector2(r.xMin, r.yMin) - center).magnitude);
        far = Mathf.Max(far, (new Vector2(r.xMin, r.yMax) - center).magnitude);
        far = Mathf.Max(far, (new Vector2(r.xMax, r.yMin) - center).magnitude);
        far = Mathf.Max(far, (new Vector2(r.xMax, r.yMax) - center).magnitude);
        Color tint = color;
        int n = Mathf.Max(48, segments);

        // ① 그늘 — 렌즈 가장자리(투명) → shadeFeather 바깥(진하게) → 화면 끝까지
        if (shade > 0.002f && radius < far)
        {
            Color sc = shadeColor * tint;
            float a = Mathf.Clamp01(shade) * tint.a;
            float r1 = radius + shadeFeather;
            Annulus(vh, n, radius, r1, With(sc, 0f), With(sc, a));
            if (r1 < far) Annulus(vh, n, r1, far + 8f, With(sc, a), With(sc, a));
        }

        // ② 유리 테두리
        float ra = Mathf.Clamp01(ringAlpha) * tint.a;
        if (ra <= 0.002f) return;
        Color white = new Color(tint.r, tint.g, tint.b, 1f);
        float gAng = Mathf.Lerp(168f, 12f, Mathf.Clamp01(glint));      // 반짝임은 왼쪽 위 → 오른쪽 위로 (렌즈가 가는 쪽)
        float gEnv = glint < 0f || glint > 1f ? 0f : Mathf.Sin(Mathf.Clamp01(glint) * Mathf.PI);
        float wide = 1f + spread;
        int P = ProfileAt.Length + 1;   // 안쪽 은은한 빛 끝 한 점을 더한다
        int start = vh.currentVertCount;
        for (int i = 0; i <= n; i++)
        {
            float deg = i * 360f / n;
            float rad = deg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float glass = 0.7f + 0.3f * Sq(0.5f + 0.5f * Mathf.Cos((deg - 135f) * Mathf.Deg2Rad));   // 빛을 받은 왼쪽 위
            float g = gEnv * Mathf.Exp(-Sq(Mathf.DeltaAngle(gAng, deg) / 15f));
            float a = ra * Mathf.Clamp01(glass + 0.6f * g);
            float core = coreHalf * (1f + 0.9f * g) * wide;
            float fw = feather * (1f + 1.3f * g) * wide;

            vh.AddVert(center + dir * (radius - innerGlow), With(white, 0f), Vector2.zero);   // 안쪽 은은한 빛 끝 (투명)
            for (int p = 0; p < ProfileAt.Length; p++)
            {
                float k = ProfileAt[p];
                float ak = Mathf.Abs(k);
                float off = ak <= 0.12f ? Mathf.Sign(k) * core : Mathf.Sign(k) * (core + (fw - core) * (ak - 0.12f) / 0.88f);
                float lv = ak <= 0.12f ? 1f : ak <= 0.36f ? 0.62f : ak <= 0.62f ? 0.24f : 0f;
                if (k <= -1f) lv = innerGlowAlpha * (1f + g);   // 안쪽 끝은 은은한 빛 높이로 이어진다
                vh.AddVert(center + dir * (radius + off), With(white, a * lv), Vector2.zero);
            }
        }
        for (int i = 0; i < n; i++)
        {
            int c0 = start + i * P, c1 = c0 + P;
            for (int p = 0; p < P - 1; p++)
            {
                vh.AddTriangle(c0 + p, c0 + p + 1, c1 + p + 1);
                vh.AddTriangle(c1 + p + 1, c1 + p, c0 + p);
            }
        }
    }

    // 원 띠 하나 — 안쪽 반지름 r0 의 색 c0 에서 바깥 r1 의 색 c1 로
    private void Annulus(VertexHelper vh, int n, float r0, float r1, Color c0, Color c1)
    {
        int s = vh.currentVertCount;
        for (int i = 0; i <= n; i++)
        {
            float rad = i * Mathf.PI * 2f / n;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            vh.AddVert(center + dir * r0, c0, Vector2.zero);
            vh.AddVert(center + dir * r1, c1, Vector2.zero);
        }
        for (int i = 0; i < n; i++)
        {
            int a = s + i * 2;
            vh.AddTriangle(a, a + 1, a + 3);
            vh.AddTriangle(a + 3, a + 2, a);
        }
    }

    private static Color With(Color c, float a) { c.a = a; return c; }
    private static float Sq(float x) => x * x;
}
