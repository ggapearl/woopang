using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 둥근 네모 테 + 바깥 빛 — 그림 없이 꼭짓점 색으로 그린다. 분류 칩 · 분류 고르기 칸의 숨 쉬는 테
/// (11-26 대표님 37번 「'전체' 로 있는 버전 내에는 적당히 해당버튼 외곽에서 어울릴만한 색으로 적당히 천천히 펄스효과를 외곽선이 있도록」).
/// 칸에 꽉 차게 놓는다 — 칸 가장자리에서 gap 떨어진 자리에 stroke 두께의 선, 그 밖으로 glow 폭만큼 옅어지는 빛.
/// 숨(Breath 0~1)은 R0926ChipPulse 가 준다 — 들이쉬면 선 · 빛이 진해지고 spread 만큼 벌어진다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class R0926PulseRing : MaskableGraphic
{
    [Tooltip("칸 모서리 반지름 (캔버스 단위) — 칸 그림과 같게")]
    [SerializeField] private float radius = 34f;
    [Tooltip("칸 가장자리에서 선까지")]
    [SerializeField] private float gap = 5f;
    [SerializeField] private float stroke = 5f;
    [Tooltip("선 밖으로 옅어지는 빛의 폭")]
    [SerializeField] private float glow = 26f;
    [Tooltip("다 들이쉬었을 때 더 벌어지는 만큼")]
    [SerializeField] private float spread = 7f;
    [Tooltip("선 알파 — x 내쉼 · y 들이쉼")]
    [SerializeField] private Vector2 line = new Vector2(0.4f, 0.95f);
    [Tooltip("빛 알파 — x 내쉼 · y 들이쉼")]
    [SerializeField] private Vector2 halo = new Vector2(0.12f, 0.45f);
    [SerializeField, Range(3, 24)] private int corner = 12;

    private float breath;
    private static readonly float[] stopD = new float[6], stopA = new float[6];

    /// <summary>숨 0(내쉼) ~ 1(들이쉼) — 바뀔 때만 다시 그린다</summary>
    public float Breath
    {
        get => breath;
        set
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value - breath) < 0.004f) return;
            breath = value;
            SetVerticesDirty();
        }
    }

    /// <summary>테가 칸 밖으로 나가는 가장 먼 거리 (다 들이쉬었을 때)</summary>
    public float Reach => gap + spread + stroke + 1.4f + glow;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f) return;

        float a = color.a;
        float aLine = Mathf.Lerp(line.x, line.y, breath) * a;
        float aGlow = Mathf.Lerp(halo.x, halo.y, breath) * a;
        float o = gap + spread * breath;
        const float f = 1.4f;   // 선 가장자리를 화면 한 픽셀쯤 부드럽게
        // 칸 가장자리에서의 거리 · 알파 — 안쪽 가장자리 · 선 · 바깥 가장자리 · 빛 (지수처럼 옅어지게 중간 한 번)
        stopD[0] = o - f;                          stopA[0] = 0f;
        stopD[1] = o;                              stopA[1] = aLine;
        stopD[2] = o + stroke;                     stopA[2] = aLine;
        stopD[3] = o + stroke + f;                 stopA[3] = aGlow;
        stopD[4] = o + stroke + f + glow * 0.35f;  stopA[4] = aGlow * 0.42f;
        stopD[5] = o + stroke + f + glow;          stopA[5] = 0f;

        float rad0 = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f);
        int per = corner + 1, n = per * 4;
        Color32 rgb = new Color(color.r, color.g, color.b, 1f);
        var v = UIVertex.simpleVert;
        for (int s = 0; s < stopD.Length; s++)
        {
            float rad = Mathf.Max(0f, rad0 + stopD[s]);
            v.color = new Color32(rgb.r, rgb.g, rgb.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(stopA[s]) * 255f));
            for (int k = 0; k < 4; k++)
            {
                // 모서리 중심 — 0 오른쪽 위 · 1 왼쪽 위 · 2 왼쪽 아래 · 3 오른쪽 아래 (반시계로 돈다)
                float cx = k == 0 || k == 3 ? r.xMax - rad0 : r.xMin + rad0;
                float cy = k <= 1 ? r.yMax - rad0 : r.yMin + rad0;
                for (int j = 0; j < per; j++)
                {
                    float t = (k + j / (float)corner) * Mathf.PI * 0.5f;
                    v.position = new Vector3(cx + Mathf.Cos(t) * rad, cy + Mathf.Sin(t) * rad, 0f);
                    vh.AddVert(v);
                }
            }
        }
        for (int s = 0; s < stopD.Length - 1; s++)
        {
            int a0 = s * n, b0 = (s + 1) * n;
            for (int i = 0; i < n; i++)
            {
                int i1 = (i + 1) % n;
                vh.AddTriangle(a0 + i, b0 + i, b0 + i1);
                vh.AddTriangle(a0 + i, b0 + i1, a0 + i1);
            }
        }
    }
}
