using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인디케이터 글자·화살표·박스 둘레의 어두운 번짐 그림자.
/// 10-21 대표님: 외곽선처럼 약하게만 보였다 → 글자 둘레에 어두운 그림자를, 글자 크기에 맞춰 크고 작게.
/// 사본을 두 겹 고리로 깐다 — 가까운 고리는 진하게(글자 바로 둘레), 먼 고리는 옅게(번지는 끝). 고리 가운데를 살짝 아래로 내려 그림자처럼.
/// 크기는 글자 크기(그림은 짧은 변)에 비례 — 인디케이터가 거리 따라 커지고 작아지면 그림자도 같이.
/// </summary>
[DisallowMultipleComponent]
public class R0926IndicatorHalo : BaseMeshEffect
{
    // 글자 크기에 대한 비율 — 가까운 고리 · 먼 고리 · 아래로 내림 (더 넓히면 글자가 번져 보였다)
    private const float TextNear = 0.05f, TextFar = 0.12f, TextDrop = 0.03f;
    // 그림(박스·화살표)은 짧은 변에 대한 비율 — 선이 가늘어 글자보다 작게. 화살표는 돌아가므로 아래로 내리지 않는다
    private const float ImageNear = 0.02f, ImageFar = 0.045f, ImageDrop = 0f;
    // 사본이 겹친 진하기 — 글자 바로 둘레(또렷한 테두리) / 번지는 끝 (tone.a 에 곱한다)
    private const float NearShare = 0.8f, FarShare = 0.26f;
    private static readonly Vector2[] NearRing = Ring(8, 0f);
    private static readonly Vector2[] FarRing = Ring(12, 15f);
    private static readonly List<UIVertex> src = new List<UIVertex>();
    private static readonly List<UIVertex> dst = new List<UIVertex>();

    private Color tone = new Color(0f, 0f, 0f, 1f);

    /// <summary>마지막으로 그린 글자·그림이 차지한 자리 (자기 단위, 그림자 제외) — 비었으면 크기 0. 목록에서 고른 장소의 스티커 크기에 쓴다</summary>
    public Rect DrawnRect { get; private set; }

    private static Vector2[] Ring(int taps, float startDeg)
    {
        var d = new Vector2[taps];
        for (int i = 0; i < taps; i++)
        {
            float a = (startDeg + i * 360f / taps) * Mathf.Deg2Rad;
            d[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
        return d;
    }

    /// <summary>그림자 색 — a 는 글자 바로 둘레가 얼마나 진할지</summary>
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

    // 그림자 크기의 기준 (자기 단위) — 글자는 실제로 그려진 글자 크기, 그림은 짧은 변
    private float BaseSize(out bool isText)
    {
        isText = graphic is Text;
        if (graphic is Text t)
        {
            // 맞춤 크기(best fit)는 글자 생성기 단위(캔버스 배율이 곱해진 픽셀)로 나온다 — 자기 단위로 되돌린다
            float used = t.resizeTextForBestFit ? t.cachedTextGenerator.fontSizeUsedForBestFit / Mathf.Max(0.01f, t.pixelsPerUnit) : t.fontSize;
            return used > 0f ? used : t.fontSize;
        }
        Rect r = graphic != null ? graphic.rectTransform.rect : Rect.zero;
        return Mathf.Min(Mathf.Abs(r.width), Mathf.Abs(r.height));
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        src.Clear();
        vh.GetUIVertexStream(src);
        int n = src.Count;
        DrawnRect = n == 0 ? Rect.zero : Bounds(src);
        if (n == 0 || tone.a <= 0.001f) return;

        float size = BaseSize(out bool isText);
        if (size <= 0f) return;
        float near = size * (isText ? TextNear : ImageNear);
        float far = size * (isText ? TextFar : ImageFar);
        var drop = new Vector3(0f, -size * (isText ? TextDrop : ImageDrop), 0f);
        // 한 점을 덮는 사본은 고리마다 절반쯤 — 그 겹침이 목표 진하기가 되게 한 장의 진하기를 정한다
        float nearA = 1f - Mathf.Pow(1f - Mathf.Clamp01(tone.a * NearShare), 2f / NearRing.Length);
        float farA = 1f - Mathf.Pow(1f - Mathf.Clamp01(tone.a * FarShare), 2.4f / FarRing.Length);

        dst.Clear();
        int need = n * (NearRing.Length + FarRing.Length + 1);
        if (dst.Capacity < need) dst.Capacity = need;
        AddRing(FarRing, far, farA, drop, n);    // 먼 고리부터 — 가까운 고리가 그 위에
        AddRing(NearRing, near, nearA, drop, n);
        dst.AddRange(src);   // 원래 글자·그림은 그림자 위에
        vh.Clear();
        vh.AddUIVertexTriangleStream(dst);
    }

    private void AddRing(Vector2[] ring, float radius, float alpha, Vector3 drop, int n)
    {
        byte cr = (byte)(tone.r * 255f), cg = (byte)(tone.g * 255f), cb = (byte)(tone.b * 255f);
        foreach (Vector2 dir in ring)
        {
            Vector3 off = (Vector3)(dir * radius) + drop;
            for (int i = 0; i < n; i++)
            {
                UIVertex v = src[i];
                v.position += off;
                v.color = new Color32(cr, cg, cb, (byte)(alpha * v.color.a));
                dst.Add(v);
            }
        }
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
