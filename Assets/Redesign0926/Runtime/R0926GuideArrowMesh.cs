using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 길안내 화살표 그림 (R0926GuideArrow) — 땅에 누운 분홍 3D 화살표 · 점선 고리 · 그림자 · 앞쪽 흰 꺾쇠 셋 (시안 우팡_가까이모달_길안내_시안_1115 B1).
/// 카메라 · 렌더 텍스처 없이 UI 정점으로 그린다 — 시안의 CSS 3D(원근 540 · 바닥을 62° 눕힘 · 원근 중심은 판 위쪽)와 같은 셈으로 투영.
/// 화살표는 살짝 떠서 오르내리고(1.8초) 꺾쇠는 차례로 깜빡인다(1.5초) — 보이는 동안 R0926GuideArrow 가 매 프레임 다시 그리게 한다.
/// 길이는 시안 px 로 적고 unit 배로 캔버스 단위 (판 가운데 = 이 RectTransform 의 기준점).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class R0926GuideArrowMesh : MaskableGraphic
{
    [Tooltip("시안 1px = 캔버스 단위 (폭 428px 시안 → 1440)")]
    [SerializeField] private float unit = 3.36f;

    /// <summary>화살표 방향 (도) — 기기가 향한 쪽 기준, 오른쪽 +</summary>
    public float Yaw { get; set; }
    /// <summary>방향을 아는지 — 모르면 화살표를 옅게 · 꺾쇠는 끈다</summary>
    public bool Known { get; set; } = true;
    /// <summary>바닥(주소줄 · 도크 위) 높이 — 이 RectTransform 기준 캔버스 단위. 뒤를 가리키면 앞으로 오는 꺾쇠가 그 앞에서 스러진다 (주소줄을 덮지 않게)</summary>
    public float FloorY { get; set; } = float.NegativeInfinity;

    // perspective 540 · rotateX(62deg) · perspective-origin 50% -40% (판 가운데(58%)에서 상자 높이 170 의 98% 위)
    private const float Tilt = 62f * Mathf.Deg2Rad, Persp = 540f, OriginUp = 166.6f;
    private const float Lift = 14f, Thick = 8f * 1.7f, Bob = 7f, BobPeriod = 1.8f, DimAlpha = 0.35f;
    private const float RingR = 92f, RingW = 2f, RingAlpha = 0.55f, FillR = 0.7f * 92f, FillAlpha = 0.18f;
    private const int RingDashes = 48, FillSegs = 40;
    private const float DashFill = 0.55f;
    private const float ChevLift = 6f, ChevStart = 92f, ChevStep = 30f, ChevW = 22f, ChevH = 20f, ChevInW = 14.08f, ChevInH = 12.8f;
    private const float ChevPeriod = 1.5f, ChevDelay = 0.22f, ChevAlpha = 0.9f, ChevFloorGap = 4f, ChevFloorFade = 16f;
    private const float ShadowAlpha = 0.35f, ShadowFeather = 7f;

    // 92×128 상자의 clip-path (50% 0, 100% 46%, 68% 46%, 68% 100%, 32% 100%, 32% 46%, 0 46%) — 끝이 앞(+y), 반시계 방향
    private static readonly Vector2[] Shape =
    {
        new Vector2(0f, 78f), new Vector2(-46f, 19.12f), new Vector2(-16.56f, 19.12f), new Vector2(-16.56f, -50f),
        new Vector2(16.56f, -50f), new Vector2(16.56f, 19.12f), new Vector2(46f, 19.12f),
    };
    private static readonly int[] Fill = { 0, 1, 6, 2, 3, 4, 2, 4, 5 };   // 머리 삼각형 + 몸통 네모

    private static readonly Color Side = new Color32(0x9c, 0x2f, 0x57, 255);
    private static readonly Color Top0 = new Color32(0xff, 0xb0, 0xc8, 255), Top1 = new Color32(0xff, 0x6f, 0x9c, 255), Top2 = new Color32(0xe4, 0x4a, 0x7c, 255);

    private static Vector2[] miter;   // 그림자 가장자리 — 바깥으로 민 꼭짓점 방향
    private static Vector2[] normal;  // 변의 바깥 방향

    private readonly Vector2[] rp = new Vector2[7], rm = new Vector2[7], rn = new Vector2[7];
    private readonly List<KeyValuePair<float, int>> walls = new List<KeyValuePair<float, int>>(7);
    private static readonly System.Comparison<KeyValuePair<float, int>> FarFirst = (a, b) => b.Key.CompareTo(a.Key);

    private float cosT, sinT;
    private Vector3 eye;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float a = color.a;
        if (a <= 0.001f) return;
        Prepare();
        float now = Application.isPlaying ? Time.unscaledTime : 0f;

        float yaw = Yaw * Mathf.Deg2Rad, cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
        for (int i = 0; i < Shape.Length; i++)
        {
            rp[i] = Turn(Shape[i], cy, sy);
            rm[i] = Turn(miter[i], cy, sy);
            rn[i] = Turn(normal[i], cy, sy);
        }

        // 고리 — 가운데가 밝은 원판 · 흰 점선 (돌지 않는다)
        int c0 = Vert(vh, P(0f, 0f, 0f), White(FillAlpha * a));
        int f0 = vh.currentVertCount;
        for (int i = 0; i < FillSegs; i++)
        {
            float t = i * Mathf.PI * 2f / FillSegs;
            Vert(vh, P(Mathf.Cos(t) * FillR, Mathf.Sin(t) * FillR, 0f), White(0f));
        }
        for (int i = 0; i < FillSegs; i++) vh.AddTriangle(c0, f0 + i, f0 + (i + 1) % FillSegs);
        var dash = White(RingAlpha * a);
        for (int i = 0; i < RingDashes; i++)
        {
            float t0 = i * Mathf.PI * 2f / RingDashes, t1 = t0 + Mathf.PI * 2f / RingDashes * DashFill;
            Quad(vh, Ring(t0, RingR - RingW), Ring(t0, RingR), Ring(t1, RingR), Ring(t1, RingR - RingW), dash, dash);
        }

        // 그림자 — 바닥에 (화살표는 떠 있다) · 가장자리는 흐리게
        int s0 = vh.currentVertCount;
        var sc = Black(ShadowAlpha * a);
        for (int i = 0; i < 7; i++) Vert(vh, P(rp[i].x, rp[i].y, 0f), sc);
        int e0 = vh.currentVertCount;
        for (int i = 0; i < 7; i++) Vert(vh, P(rp[i].x + rm[i].x * ShadowFeather, rp[i].y + rm[i].y * ShadowFeather, 0f), Black(0f));
        for (int k = 0; k < Fill.Length; k += 3) vh.AddTriangle(s0 + Fill[k], s0 + Fill[k + 1], s0 + Fill[k + 2]);
        for (int i = 0; i < 7; i++)
        {
            int j = (i + 1) % 7;
            vh.AddTriangle(s0 + i, s0 + j, e0 + j);
            vh.AddTriangle(s0 + i, e0 + j, e0 + i);
        }

        float arrowA = Known ? a : a * DimAlpha;

        // 꺾쇠 셋 — 화살표 끝 앞에서 차례로 깜빡인다 (방향을 모르면 끈다). 뒤를 가리키면 앞(화면 아래)으로 오는 꺾쇠는 바닥 앞에서 스러진다
        if (Known)
            for (int n = 0; n < 3; n++)
            {
                float ca = ChevAlpha * Blink(now - n * ChevDelay) * a;
                if (ca <= 0.004f) continue;
                float y0 = ChevStart + ChevStep * n;
                Vector3 A = PT(-ChevW, y0, cy, sy), B = PT(0f, y0 + ChevH, cy, sy), C = PT(0f, y0 + ChevInH, cy, sy);
                Vector3 D = PT(-ChevInW, y0, cy, sy), E = PT(ChevW, y0, cy, sy), F = PT(ChevInW, y0, cy, sy);
                float low = Mathf.Min(Mathf.Min(A.y, B.y), Mathf.Min(Mathf.Min(C.y, D.y), Mathf.Min(E.y, F.y)));
                ca *= Mathf.Clamp01((low - FloorY - ChevFloorGap * unit) / (ChevFloorFade * unit));
                if (ca <= 0.004f) continue;
                var cc = White(ca);
                int b = vh.currentVertCount;
                Vert(vh, A, cc); Vert(vh, B, cc); Vert(vh, C, cc); Vert(vh, D, cc); Vert(vh, E, cc); Vert(vh, F, cc);
                vh.AddTriangle(b, b + 1, b + 2); vh.AddTriangle(b, b + 2, b + 3);           // 왼쪽 팔
                vh.AddTriangle(b + 1, b + 4, b + 5); vh.AddTriangle(b + 1, b + 5, b + 2);   // 오른쪽 팔
            }

        // 화살표 — 옆면(카메라 쪽을 보는 것만, 먼 것부터) 뒤 윗면
        float bob = Application.isPlaying ? Bob * (0.5f - 0.5f * Mathf.Cos(now * Mathf.PI * 2f / BobPeriod)) : 0f;
        float z0 = Lift + bob, z1 = z0 + Thick, zm = (z0 + z1) * 0.5f;
        walls.Clear();
        for (int i = 0; i < 7; i++)
        {
            int j = (i + 1) % 7;
            Vector2 mid = (rp[i] + rp[j]) * 0.5f;
            Vector2 toEye = new Vector2(eye.x - mid.x, eye.y - mid.y);
            if (Vector2.Dot(rn[i], toEye) <= 0f) continue;
            walls.Add(new KeyValuePair<float, int>((new Vector3(mid.x, mid.y, zm) - eye).sqrMagnitude, i));
        }
        walls.Sort(FarFirst);
        var side = Tint(Side, arrowA);
        foreach (var w in walls)
        {
            int i = w.Value, j = (i + 1) % 7;
            Quad(vh, P(rp[i].x, rp[i].y, z1), P(rp[j].x, rp[j].y, z1), P(rp[j].x, rp[j].y, z0), P(rp[i].x, rp[i].y, z0), side, side);
        }
        int t0i = vh.currentVertCount;
        for (int i = 0; i < 7; i++) Vert(vh, P(rp[i].x, rp[i].y, z1), Tint(TopColor(Shape[i]), arrowA));
        for (int k = 0; k < Fill.Length; k += 3) vh.AddTriangle(t0i + Fill[k], t0i + Fill[k + 1], t0i + Fill[k + 2]);
    }

    private void Prepare()
    {
        cosT = Mathf.Cos(Tilt); sinT = Mathf.Sin(Tilt);
        // 보는 눈 — 화면에서 원근 중심(판 가운데 위 OriginUp) · 앞으로 Persp 떨어진 곳을 바닥 좌표로
        float Y = OriginUp, D = -Persp;
        eye = new Vector3(0f, Y * cosT + D * sinT, Y * sinT - D * cosT);
        if (miter != null) return;
        int n = Shape.Length;
        normal = new Vector2[n];
        miter = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 d = Shape[(i + 1) % n] - Shape[i];
            normal[i] = new Vector2(d.y, -d.x).normalized;   // 반시계 — 오른쪽이 바깥
        }
        for (int i = 0; i < n; i++)
        {
            Vector2 a = normal[(i + n - 1) % n], b = normal[i];
            miter[i] = (a + b) / Mathf.Max(0.2f, 1f + Vector2.Dot(a, b));
        }
    }

    // 바닥 좌표(오른쪽 x · 앞 y · 위 z, 시안 px) → 화면(캔버스 단위, 판 가운데 기준)
    private Vector3 P(float x, float y, float z)
    {
        float Y = y * cosT + z * sinT, D = y * sinT - z * cosT;
        float s = Persp / Mathf.Max(1f, Persp + D);
        return new Vector3(x * s * unit, (OriginUp + (Y - OriginUp) * s) * unit, 0f);
    }

    private Vector3 PT(float x, float y, float cy, float sy)
    {
        var v = Turn(new Vector2(x, y), cy, sy);
        return P(v.x, v.y, ChevLift);
    }

    private Vector3 Ring(float t, float r) => P(Mathf.Cos(t) * r, Mathf.Sin(t) * r, 0f);

    // 오른쪽으로 yaw 만큼 (위에서 보아 시계 방향)
    private static Vector2 Turn(Vector2 v, float c, float s) => new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c);

    // 윗면 — linear-gradient(160deg, #ffb0c8, #ff6f9c 40%, #e44a7c) 를 꼭짓점 색으로
    private static Color TopColor(Vector2 p)
    {
        const float Len = 151.8f;   // 92·sin160 + 128·|cos160|
        float t = 0.5f + (p.x * 0.342f - (p.y - 14f) * 0.940f) / Len;
        return t < 0.4f ? Color.Lerp(Top0, Top1, t / 0.4f) : Color.Lerp(Top1, Top2, (t - 0.4f) / 0.6f);
    }

    // 꺾쇠 — 0% 0 · 40% 0.9 · 100% 0 (1.5초, ease-in-out)
    private static float Blink(float t)
    {
        if (t < 0f) return 0f;
        float p = (t % ChevPeriod) / ChevPeriod;
        return p < 0.4f ? Mathf.SmoothStep(0f, 1f, p / 0.4f) : 1f - Mathf.SmoothStep(0f, 1f, (p - 0.4f) / 0.6f);
    }

    private static Color32 White(float a) => new Color(1f, 1f, 1f, a);
    private static Color32 Black(float a) => new Color(0f, 0f, 0f, a);
    private static Color32 Tint(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

    private static int Vert(VertexHelper vh, Vector3 p, Color32 c)
    {
        vh.AddVert(p, c, Vector4.zero);
        return vh.currentVertCount - 1;
    }

    private static void Quad(VertexHelper vh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 top, Color32 bottom)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, top, Vector4.zero);
        vh.AddVert(b, top, Vector4.zero);
        vh.AddVert(c, bottom, Vector4.zero);
        vh.AddVert(d, bottom, Vector4.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
