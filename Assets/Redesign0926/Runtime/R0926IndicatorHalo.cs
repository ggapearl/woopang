using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인디케이터 글자·화살표·박스 둘레의 아주 옅은 번짐 그림자 (10-15 대표님 — 배경에 묻혀 글자가 안 보이던 것).
/// UI Outline 은 네 방향 딱딱한 선이라, 둘레 여덟 방향에 아주 옅은 사본을 깔아 부드럽게 번지게 한다.
/// 색(어둡게 / 밝게)은 Indicator 가 배경 밝기로 고른다. 두께는 캔버스 단위로 일정 — 인디케이터가 거리 따라 커지고 작아져도 같은 두께.
/// </summary>
[DisallowMultipleComponent]
public class R0926IndicatorHalo : BaseMeshEffect
{
    private const float Radius = 4.5f;   // 캔버스 단위 (≈ 1.5pt)
    private const int Taps = 8;
    private static readonly Vector2[] Dirs = MakeDirs();
    private static readonly List<UIVertex> src = new List<UIVertex>();
    private static readonly List<UIVertex> dst = new List<UIVertex>();

    private Color tone = new Color(0f, 0f, 0f, 0.5f);
    private float builtK = -1f;

    /// <summary>마지막으로 그린 글자·그림이 차지한 자리 (자기 단위, 그림자 제외) — 비었으면 크기 0. 목록에서 고른 장소의 스티커 크기에 쓴다</summary>
    public Rect DrawnRect { get; private set; }

    private static Vector2[] MakeDirs()
    {
        var d = new Vector2[Taps];
        for (int i = 0; i < Taps; i++)
        {
            float a = i * Mathf.PI * 2f / Taps;
            d[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
        return d;
    }

    /// <summary>그림자 색 — a 는 사본 여덟 장이 다 겹친 곳의 진하기</summary>
    public void SetTone(Color c)
    {
        if (c == tone) return;
        tone = c;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    /// <summary>자기 단위 1 이 캔버스 단위로 얼마인지 (인디케이터 크기 × 글자 축소)</summary>
    public float CanvasUnitsPerLocal()
    {
        var c = graphic != null ? graphic.canvas : null;
        float root = c != null ? Mathf.Abs(c.rootCanvas.transform.lossyScale.x) : 1f;
        float s = Mathf.Abs(transform.lossyScale.x);
        return root > 0f && s > 0f ? s / root : 1f;
    }

    private void LateUpdate()
    {
        // 거리 따라 크기가 크게 바뀌면 다시 깐다 — 두께가 일정하게
        if (builtK > 0f && graphic != null && Mathf.Abs(CanvasUnitsPerLocal() / builtK - 1f) > 0.12f)
            graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        src.Clear();
        vh.GetUIVertexStream(src);
        int n = src.Count;
        DrawnRect = n == 0 ? Rect.zero : Bounds(src);
        if (n == 0 || tone.a <= 0.001f) return;
        builtK = CanvasUnitsPerLocal();
        float r = Radius / builtK;
        float tapA = 1f - Mathf.Pow(1f - tone.a, 1f / Taps);
        byte cr = (byte)(tone.r * 255f), cg = (byte)(tone.g * 255f), cb = (byte)(tone.b * 255f);
        dst.Clear();
        if (dst.Capacity < n * (Taps + 1)) dst.Capacity = n * (Taps + 1);
        for (int t = 0; t < Taps; t++)
        {
            Vector3 off = Dirs[t] * r;
            for (int i = 0; i < n; i++)
            {
                UIVertex v = src[i];
                v.position += off;
                v.color = new Color32(cr, cg, cb, (byte)(tapA * v.color.a));
                dst.Add(v);
            }
        }
        dst.AddRange(src);   // 원래 글자·그림은 그림자 위에
        vh.Clear();
        vh.AddUIVertexTriangleStream(dst);
    }

    private static Rect Bounds(List<UIVertex> v)
    {
        Vector2 min = v[0].position, max = min;
        for (int i = 1; i < v.Count; i++)
        {
            Vector3 p = v[i].position;
            if (p.x < min.x) min.x = p.x; else if (p.x > max.x) max.x = p.x;
            if (p.y < min.y) min.y = p.y; else if (p.y > max.y) max.y = p.y;
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
