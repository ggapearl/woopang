using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ContentArea 전용 더블탭 감지.
/// IPointerClickHandler만 구현 — 드래그 이벤트는 부모로 버블링되어
/// SwipeToDeleteHandler와 ScrollRect가 정상 동작함.
/// </summary>
public class CommentDoubleTapHandler : MonoBehaviour, IPointerClickHandler
{
    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.3f;

    private System.Action onDoubleTap;

    public void Initialize(System.Action doubleTapCallback)
    {
        onDoubleTap = doubleTapCallback;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        float timeSinceLastClick = Time.unscaledTime - lastClickTime;
        lastClickTime = Time.unscaledTime;

        if (timeSinceLastClick <= DOUBLE_CLICK_TIME && timeSinceLastClick > 0.05f)
        {
            onDoubleTap?.Invoke();
        }
    }
}
