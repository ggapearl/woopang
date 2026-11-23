using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 창을 움직이며 닫는 것들의 공통 — 다 닫힌 뒤 then(원래 닫기 버튼)을 눌러 준다.
/// 시트는 아래로 미끄러지고(R0926SwipeDismiss), 프로필은 옅어진다(R0926FadePanel).
/// 도크에서 다른 창으로 바꿀 때(R0926DockSlotSwap)는 둘 다 움직이지 않고 제자리에서 디졸브 — 대표님 11-18 H2
/// 「다른 탭으로 넘어갈 때는 그냥 자연스럽게 페이드인아웃(디졸브) 로 … (너무 느리거나 잔상이 심하지 않게 / 슬라이드 없이)」.
/// 예전(11-08)엔 새 창이 56 아래에서 옅게 올라오고 옛 창은 48 내려가며 옅어졌다 — 조금 남은 그 움직임이 화면을 튀어 보이게 했다.
/// </summary>
public abstract class R0926Closer : MonoBehaviour
{
    /// <summary>디졸브 시간 (초)</summary>
    public const float DissolveTime = 0.2f;

    /// <summary>바꾸는 동안 창 뒤 어두운 바탕 — 창과 같이 옅게 · 바로 걷기 · 그대로.
    /// 두 창의 바탕이 겹치면 그동안 화면이 더 어두워졌다 밝아진다 — 맨 아래 창 하나만 깔고(Keep) 나머지는 걷는다(Hide)</summary>
    public enum Dim { Fade, Hide, Keep }

    public abstract void AnimateClose(Button then);

    /// <summary>제자리에서 옅어진 뒤 then(원래 닫기 버튼)을 누른다. top = 새 창보다 위에 그려짐</summary>
    public abstract void DissolveClose(Button then, bool top, Dim dim);

    /// <summary>방금 연 창이 제자리에서 옅게 → 진하게. top = 닫히는 창보다 위에 그려짐</summary>
    public abstract void DissolveIn(bool top, Dim dim);

    // 겹친 두 창이 같이 옅어지면 가운데쯤 둘 다 반투명이라 그 사이로 뒤 화면이 비치며 깜박인다 —
    // 위에 그려지는 쪽만 0.2초 내내 바뀌고, 아래 쪽은 위 쪽이 거의 덮고 있는 동안에만 바뀐다 (겹친 곳이 비치는 건 6% 아래)
    private const float UnderInEnds = 0.4f, UnderOutStarts = 0.6f;

    public static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    /// <summary>열리는 쪽 투명도 (p 0 → 1)</summary>
    public static float InAlpha(float p, bool top) => top ? Smooth(p) : Smooth(p / UnderInEnds);

    /// <summary>닫히는 쪽 투명도 (p 0 → 1)</summary>
    public static float OutAlpha(float p, bool top) => top ? 1f - Smooth(p) : 1f - Smooth((p - UnderOutStarts) / (1f - UnderOutStarts));

    /// <summary>a 가 b 보다 위에 그려지는지 — 같은 캔버스 안에선 공통 부모 아래 형제 순서가 뒤일수록 위 (도크 창들은 모두 Canvas 바로 아래, 카드들은 한 틀 안)</summary>
    public static bool DrawsAbove(Transform a, Transform b)
    {
        if (a == null || b == null || a == b) return false;
        if (a.IsChildOf(b)) return true;
        if (b.IsChildOf(a)) return false;
        for (var x = a; x.parent != null; x = x.parent)
        {
            if (!b.IsChildOf(x.parent)) continue;
            var y = b;
            while (y.parent != x.parent) y = y.parent;
            return x.GetSiblingIndex() > y.GetSiblingIndex();
        }
        return false;
    }
}
