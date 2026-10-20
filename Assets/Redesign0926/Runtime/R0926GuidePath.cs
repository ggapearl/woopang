using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 첫 사용 안내의 점선 길 — 안내 글('다음' 버튼 아래)에서 비춘 도크 칸 위 화살표까지 이어지는 점들이 칸 쪽으로 흘러간다.
/// 안내 글을 화면 가운데 위로 올리면서(10-20 대표님 「위쪽으로 · 크게」) 글과 칸이 멀어져, 어느 칸 이야기인지 눈으로 잇는다.
/// 3차 베지어 p0 → p3 (덮개 가운데 기준 좌표). 값은 R0926GuideOverlay 가 매 프레임 넣는다. 그리기 1번.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class R0926GuidePath : MaskableGraphic
{
    [SerializeField] private float dotRadius = 6f;
    [SerializeField] private float spacing = 32f;
    [Tooltip("점이 칸 쪽으로 흘러가는 빠르기 (초당 캔버스 단위)")]
    [SerializeField] private float speed = 60f;
    [SerializeField, Range(0f, 1f)] private float dotAlpha = 0.6f;

    [System.NonSerialized] public Vector2 p0, p1, p2, p3;
    [System.NonSerialized] public bool show;

    private const int Samples = 48;
    private readonly Vector2[] pts = new Vector2[Samples + 1];
    private readonly float[] len = new float[Samples + 1];

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void Refresh() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!show) return;
        for (int i = 0; i <= Samples; i++)
        {
            float t = i / (float)Samples, u = 1f - t;
            pts[i] = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
            len[i] = i == 0 ? 0f : len[i - 1] + (pts[i] - pts[i - 1]).magnitude;
        }
        float L = len[Samples];
        if (L < spacing * 2f) return;

        Color c = color;
        float phase = Mathf.Repeat((Application.isPlaying ? Time.unscaledTime : 0f) * speed, spacing);
        int seg = 1;
        for (float s = phase; s <= L; s += spacing)
        {
            while (seg < Samples && len[seg] < s) seg++;
            float k = Mathf.InverseLerp(len[seg - 1], len[seg], s);
            Vector2 at = Vector2.Lerp(pts[seg - 1], pts[seg], k);
            float a = dotAlpha * c.a * Mathf.SmoothStep(0f, 1f, s / 60f) * Mathf.SmoothStep(0f, 1f, (L - s) / 70f);
            if (a > 0.01f) Dot(vh, at, dotRadius, new Color(c.r, c.g, c.b, a));
        }
    }

    // 점 하나 — 팔각형 (텍스처 없이 동그랗게 보이는 크기)
    private static void Dot(VertexHelper vh, Vector2 at, float r, Color c)
    {
        int s = vh.currentVertCount;
        vh.AddVert(at, c, Vector2.zero);
        for (int i = 0; i < 8; i++)
        {
            float rad = i * Mathf.PI / 4f;
            vh.AddVert(at + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * r, c, Vector2.zero);
        }
        for (int i = 0; i < 8; i++) vh.AddTriangle(s, s + 1 + i, s + 1 + (i + 1) % 8);
    }
}
