using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도크로 여는 창(목록 · 추가/보물찾기 · 메시지 · 대화 · 프로필 · 팔로우)의 어두운 바탕을 도크 바로 아래 한 장으로 그린다 —
/// 창이 열려도 도크(물방울 유리 · 아이콘 · 배지)는 닫혔을 때와 똑같고, 그 칸의 X '닫기'만 다르다.
/// 11-20 대표님 「탭이 열렸을 때랑 닫았을 때 다르게 발생하는데 동일하게 발생하고 X버튼(닫기) 만 다르게」 (1.2.68 S5) —
/// 예전엔 창 뿌리의 바탕이 도크 위에 덮여 도크가 통째로 17~35% 어두워졌다.
/// 창 쪽 바탕은 그대로 두되 그리지만 않는다(R0926DimSource) — 손가락은 예전처럼 받고(R0926DockPassThrough),
/// 밀어 닫기 · 디졸브가 바꾸는 투명도는 여기서 그대로 읽어 겹친 만큼(1 - Π(1 - a)) 칠한다.
/// 검은 그늘끼리는 겹치는 순서와 상관없이 같은 어둡기라 도크 아래 다른 것(위치 칩 · 상단 그늘 …)은 예전과 같다.
/// </summary>
[RequireComponent(typeof(Image))]
public class R0926DockDim : MonoBehaviour
{
    [SerializeField] private RectTransform dock;
    [SerializeField] private Graphic[] sources;   // 창 뿌리의 어두운 바탕 (R0926DimSource)

    private Image img;
    private static readonly List<CanvasGroup> groups = new List<CanvasGroup>();

    /// <summary>창 바탕을 대신 그리는 중 — 창 쪽(R0926DimSource)은 그리지 않는다. 이 한 장이 없으면 예전처럼 창이 직접 그린다</summary>
    public static bool Live { get; private set; }

    /// <summary>지금 그리는 한 장 — 뒤를 비추는 창(R0926WindowSkin)이 둘레와 같은 어둡기로 비추려고 읽는다</summary>
    public static R0926DockDim Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Live = false; Current = null; }

    private void OnEnable()
    {
        img = GetComponent<Image>();
        Live = dock != null;
        Current = Live ? this : null;
        Touch();
    }

    private void OnDisable()
    {
        Live = false;
        if (Current == this) Current = null;
        if (img != null) img.enabled = false;
        Touch();
    }

    private void Touch()
    {
        if (sources == null) return;
        foreach (var s in sources) if (s != null) s.SetVerticesDirty();
    }

    private void LateUpdate()
    {
        if (!Live || img == null) return;
        // 늘 도크 바로 아래 — 다른 것이 사이에 끼면 그것도 그늘 아래로 (도크 위로 올라오는 건 창 · 안내뿐)
        if (transform.parent == dock.parent)
        {
            int d = dock.GetSiblingIndex(), m = transform.GetSiblingIndex();
            if (m != d - 1) transform.SetSiblingIndex(m < d ? d - 1 : d);
        }
        float keep = 1f;
        Color rgb = Color.black;
        foreach (var s in sources)
        {
            if (s == null || !s.isActiveAndEnabled) continue;
            float a = Mathf.Clamp01(s.color.a * GroupAlpha(s.transform));
            if (a <= 0f) continue;
            keep *= 1f - a;
            rgb = s.color;
        }
        float alpha = 1f - keep;
        bool show = alpha > 0.002f;
        if (show)
        {
            var c = new Color(rgb.r, rgb.g, rgb.b, alpha);
            if (img.color != c) img.color = c;
        }
        if (img.enabled != show) img.enabled = show;
    }

    /// <summary>지금 칠하는 어둡기 (시험용)</summary>
    public float Alpha => img != null && img.enabled ? img.color.a : 0f;

    // 창 바탕이 실제로 그려질 투명도 — 캔버스 그룹(디졸브 · 프로필 옅어짐)을 거슬러 곱한다
    private static float GroupAlpha(Transform t)
    {
        float a = 1f;
        for (; t != null; t = t.parent)
        {
            t.GetComponents(groups);
            foreach (var g in groups)
            {
                if (!g.enabled) continue;
                a *= g.alpha;
                if (g.ignoreParentGroups) { groups.Clear(); return a; }
            }
        }
        groups.Clear();
        return a;
    }
}
