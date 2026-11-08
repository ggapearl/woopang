using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 창을 움직이며 닫는 것들의 공통 — 다 닫힌 뒤 then(원래 닫기 버튼)을 눌러 준다.
/// 시트는 아래로 미끄러지고(R0926SwipeDismiss), 프로필은 옅어진다(R0926FadePanel).
/// </summary>
public abstract class R0926Closer : MonoBehaviour
{
    /// <summary>이 프레임에 부른 닫기는 길게 미끄러지지 않고 옅어지며 조금만 내려간다 — 도크에서 다른 창으로 바꿀 때 (R0926DockSlotSwap).
    /// 다 내려갔다가 새 창이 올라오면 오래 걸리고 움직임이 많다 (대표님 11-08)</summary>
    public static int DissolveFrame = -1;
    protected static bool Dissolving => DissolveFrame == Time.frameCount;

    public abstract void AnimateClose(Button then);
}
