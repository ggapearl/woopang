using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시작화면의 반짝이는 별과 별똥별 — 별 그림 한 장을 사각형 여러 개로 한 번에 그린다(그리기 1번).
/// 별은 저마다 다른 빠르기로 밝아졌다 어두워지고 아주 천천히 흘러가며, 밝은 별에는 십자 빛이 선다.
/// 별똥별은 1.5~3초에 하나씩 긴 꼬리로 비스듬히 지나간다. (woopang.com 과 같은 색)
/// 시작화면의 줌아웃(zoom)은 가운데를 기준으로 펼쳤다 모은다 — 가까운 별일수록 조금 더 움직여 멀어지는 깊이가 느껴진다.
/// ExecuteAlways — 에디터 캡처에서도 별이 그려지게 (Graphic 의 ExecuteAlways 는 물려받지 않아 캡처에 별이 빠져 있었다)
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class R0926StarField : MaskableGraphic
{
    [SerializeField] private Sprite starSprite;
    [SerializeField] private int count = 120;
    [SerializeField, Range(0f, 1f)] private float bigFraction = 0.22f;
    [SerializeField] private int seed = 926;

    /// <summary>1 보다 크면 확대돼 보인다 — R0926Splash 가 매 프레임 넣는다 (1.15 → 1)</summary>
    [System.NonSerialized] public float zoom = 1f;

    private struct Star { public Vector2 p; public float r, phase, speed, depth; public Color c; public bool big; }
    private struct Shoot { public Vector2 start; public float t0, len; }

    private static readonly Color[] Palette =
    {
        Color.white, Color.white, Color.white, new Color(1f, 0.95f, 0.77f), new Color(0.85f, 0.70f, 0.29f),
        new Color(0.67f, 0.80f, 1f), new Color(1f, 0.67f, 1f),
    };

    private Star[] stars;
    private readonly Shoot[] shoots = new Shoot[3];
    private float time, nextShoot = 0.9f;
    private System.Random rng;

    public override Texture mainTexture => starSprite != null ? starSprite.texture : s_WhiteTexture;

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        rng = new System.Random(seed);
        stars = new Star[count];
        for (int i = 0; i < count; i++)
        {
            bool big = rng.NextDouble() < bigFraction;
            stars[i] = new Star
            {
                p = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
                r = big ? 7f + (float)rng.NextDouble() * 5f : 3.5f + (float)rng.NextDouble() * 3.5f,
                phase = (float)rng.NextDouble() * 6.283f,
                speed = 0.7f + (float)rng.NextDouble() * 2f,
                depth = 0.2f + (float)rng.NextDouble() * 0.8f,
                c = Palette[rng.Next(Palette.Length)],
                big = big,
            };
        }
        for (int i = 0; i < shoots.Length; i++) shoots[i].t0 = -10f;
        time = 0f;
        nextShoot = 0.9f;
    }

    private void Update()
    {
        time += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (time > nextShoot)
        {
            for (int i = 0; i < shoots.Length; i++)
            {
                if (time - shoots[i].t0 < 1.6f) continue;
                shoots[i] = new Shoot { start = new Vector2(0.3f + (float)rng.NextDouble() * 0.66f, 0.6f + (float)rng.NextDouble() * 0.38f), t0 = time, len = 330f + (float)rng.NextDouble() * 220f };
                break;
            }
            nextShoot = time + 1.5f + (float)rng.NextDouble() * 1.5f;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (stars == null) return;
        Rect r = rectTransform.rect;
        Vector2 mid = r.center;
        Color tint = color;

        foreach (var s in stars)
        {
            float tw = 0.5f + 0.5f * Mathf.Sin(s.phase + time * s.speed);
            float a = 0.12f + 0.88f * Mathf.Pow(tw, 1.6f);
            float x = Mathf.Repeat(s.p.x - time * 0.004f * s.depth, 1f);
            float y = Mathf.Repeat(s.p.y - time * 0.008f * s.depth, 1f);
            // 먼 별(depth 0.2)은 줌의 0.65배, 가까운 별(1)은 1.25배 — 평균은 문양과 비슷하게
            float k = 1f + (zoom - 1f) * (0.5f + 0.75f * s.depth);
            Vector2 c = mid + (new Vector2(r.xMin + x * r.width, r.yMin + y * r.height) - mid) * k;
            Color col = s.c * tint; col.a = a * tint.a;
            Quad(vh, c, new Vector2(s.r, s.r) * k, col);
            if (s.big)
            {
                // 십자 빛 — 밝을수록 길게
                float L = (16f + a * 26f) * k;
                Color fl = col; fl.a *= 0.55f;
                Quad(vh, c, new Vector2(L, 2.2f), fl);
                Quad(vh, c, new Vector2(2.2f, L), fl);
            }
        }

        // 별똥별: 오른쪽 위 → 왼쪽 아래로, 머리는 밝고 꼬리는 사라진다
        foreach (var sh in shoots)
        {
            float k = (time - sh.t0) / 1.6f;
            if (k < 0f || k >= 1f) continue;
            float env = Mathf.Sin(k * Mathf.PI);
            Vector2 dir = new Vector2(-0.84f, -0.54f);
            Vector2 head = new Vector2(r.xMin + sh.start.x * r.width, r.yMin + sh.start.y * r.height) + dir * (k * 620f);
            Vector2 tail = head - dir * sh.len;
            Line(vh, head, tail, 4.5f, new Color(1f, 1f, 1f, env * tint.a), new Color(1f, 0.94f, 0.8f, 0f));
            Quad(vh, head, new Vector2(7f, 7f), new Color(1f, 1f, 1f, env * tint.a));
        }
    }

    private static void Quad(VertexHelper vh, Vector2 c, Vector2 half, Color col)
    {
        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(c.x - half.x, c.y - half.y), col, new Vector2(0f, 0f));
        vh.AddVert(new Vector3(c.x - half.x, c.y + half.y), col, new Vector2(0f, 1f));
        vh.AddVert(new Vector3(c.x + half.x, c.y + half.y), col, new Vector2(1f, 1f));
        vh.AddVert(new Vector3(c.x + half.x, c.y - half.y), col, new Vector2(1f, 0f));
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }

    // 긴 막대 — 별 그림의 가운데 줄만 늘여 써서 부드러운 선이 된다
    private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float halfW, Color ca, Color cb)
    {
        Vector2 n = Vector2.Perpendicular((b - a).normalized) * halfW;
        int i = vh.currentVertCount;
        vh.AddVert(a - n, ca, new Vector2(0.5f, 0f));
        vh.AddVert(a + n, ca, new Vector2(0.5f, 1f));
        vh.AddVert(b + n, cb, new Vector2(0.5f, 1f));
        vh.AddVert(b - n, cb, new Vector2(0.5f, 0f));
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }
}
