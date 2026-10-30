using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시작화면 렌즈 밖 그늘 (10-18 대표님 「원 안팎 대비를 세게」).
/// 렌즈 가장자리에서 부드럽게 차올라 바깥(옅은 문양·성운·별)을 어둡게 덮는다. 렌즈 안은 그대로 밝고,
/// 가장자리 바로 바깥은 덜 덮여 렌즈에서 빛이 번져 나오는 것처럼 보인다.
/// 10-31 대표님 「흰색 원은 삭제 — 밝기 대비만 있는 게 무난」 — 가장자리의 흰 유리 테두리·반짝임을 뺐다.
/// 값은 R0926Splash 가 매 프레임 넣는다. ExecuteAlways — 에디터 캡처에서도 그려지게 (Graphic 의 ExecuteAlways 는 물려받지 않는다).
/// CanvasRenderer 는 Image 처럼 처음부터 붙여 둔다 — Graphic 이 늦게 붙이는 길은 에디터에서 GetComponent 가 '가짜 null' 을 돌려줘 안 붙는다
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class R0926LensFx : MaskableGraphic
{
    [SerializeField] private int segments = 200;
    [Tooltip("그늘이 렌즈 가장자리에서 다 차오르기까지의 폭 (캔버스 단위 — 기준 폭 1440) — 이 띠만큼 렌즈 빛이 바깥으로 번져 보인다")]
    [SerializeField] private float shadeFeather = 120f;

    [System.NonSerialized] public Vector2 center;
    [System.NonSerialized] public float radius;
    [System.NonSerialized] public float shade;          // 렌즈 밖 그늘 진하기 0~1
    [System.NonSerialized] public Color shadeColor = Color.black;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void Refresh() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (radius <= 0f || shade <= 0.002f) return;
        Rect r = rectTransform.rect;
        float far = 0f;
        far = Mathf.Max(far, (new Vector2(r.xMin, r.yMin) - center).magnitude);
        far = Mathf.Max(far, (new Vector2(r.xMin, r.yMax) - center).magnitude);
        far = Mathf.Max(far, (new Vector2(r.xMax, r.yMin) - center).magnitude);
        far = Mathf.Max(far, (new Vector2(r.xMax, r.yMax) - center).magnitude);
        if (radius >= far) return;
        int n = Mathf.Max(48, segments);

        // 렌즈 가장자리(투명) → shadeFeather 바깥(진하게) → 화면 끝까지
        Color sc = shadeColor * color;
        float a = Mathf.Clamp01(shade) * color.a;
        float r1 = radius + shadeFeather;
        Annulus(vh, n, radius, r1, With(sc, 0f), With(sc, a));
        if (r1 < far) Annulus(vh, n, r1, far + 8f, With(sc, a), With(sc, a));
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
}
