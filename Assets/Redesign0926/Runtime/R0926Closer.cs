using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 창을 움직이며 닫는 것들의 공통 — 다 닫힌 뒤 then(원래 닫기 버튼)을 눌러 준다.
/// 시트는 아래로 미끄러지고(R0926SwipeDismiss), 프로필은 옅어진다(R0926FadePanel).
/// </summary>
public abstract class R0926Closer : MonoBehaviour
{
    public abstract void AnimateClose(Button then);
}
