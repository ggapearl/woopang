using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 고정한 하늘 판 — 옆 장을 유리(area) 안에서만 보이게 좌우를 자른다 (11-26 대표님 16번 「배경탭이 발생한 경우에는 (화면고정) 해당 배경탭 내부에
/// 발생한 내용이 좌우스와이프했을 때 해당 네온배경(하늘보기탭) 내에서 좌우로 스와이프해야 하는데 지금은 이를 벗어나서」).
/// 하늘에 걸린 동안(유리 투명)은 꺼 둔다 — R0926SkyWeather.SetGlass 가 유리가 보일 때만 켠다.
///  · 위아래는 자르지 않는다 (📌 가 유리 위로 나와 있다) · 손가락은 막지 않는다 (유리 가장자리까지 그대로 받는다)
///  · 유리와 그 테(네온)는 자르지 않는다 — 가장자리 흐림에 유리 끝이 같이 옅어지지 않게
/// </summary>
public class R0926PageClip : RectMask2D
{
    [Tooltip("이 칸 안에서만 보인다 (하늘 판 유리 Glass0926)")]
    [SerializeField] private RectTransform area;
    [Tooltip("유리 가장자리에서 안쪽으로 (틀 단위)")]
    [SerializeField] private float inset = 14f;
    [Tooltip("자르는 가장자리를 이만큼 흐리게 (틀 단위)")]
    [SerializeField] private float fade = 40f;

    private const float Open = 100000f;   // 위아래 — 자르지 않는다
    private static readonly Vector3[] wc = new Vector3[4];
    private bool freed;

    public RectTransform Area => area;

    public override bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera) => true;

    protected override void OnEnable()
    {
        base.OnEnable();
        Free();
        Fit();
    }

    private void LateUpdate() => Fit();

    // 유리와 그 테는 자르지 않는다 (테는 적용 단계에서 나중에 붙는다 — 처음 켤 때 한 번)
    private void Free()
    {
        if (freed || area == null) return;
        freed = true;
        foreach (var g in area.GetComponentsInChildren<MaskableGraphic>(true)) g.maskable = false;
    }

    // 자르는 칸 = 유리의 좌우 — RectMask2D 의 padding · softness 는 캔버스 공간에서 더해진다 (판이 줄어 있어도 유리에 맞게)
    private void Fit()
    {
        if (area == null) return;
        var c = GetComponentInParent<Canvas>();
        if (c == null) return;
        var space = c.rootCanvas.transform;
        Span(rectTransform, space, out float fx0, out float fx1);
        Span(area, space, out float gx0, out float gx1);
        float w = rectTransform.rect.width;
        float k = w > 0.01f ? (fx1 - fx0) / w : 1f;   // 틀 단위 → 캔버스 단위
        var p = new Vector4(gx0 - fx0 + inset * k, -Open, fx1 - gx1 + inset * k, -Open);
        if ((padding - p).sqrMagnitude > 0.0001f) padding = p;
        var s = new Vector2Int(Mathf.RoundToInt(fade * k), 0);
        if (softness != s) softness = s;
    }

    private static void Span(RectTransform t, Transform space, out float x0, out float x1)
    {
        t.GetWorldCorners(wc);
        x0 = float.MaxValue; x1 = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            float x = space.InverseTransformPoint(wc[i]).x;
            if (x < x0) x0 = x;
            if (x > x1) x1 = x;
        }
    }
}
