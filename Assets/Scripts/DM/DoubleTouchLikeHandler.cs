using UnityEngine;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 더블터치로 좋아요 표시하는 핸들러
/// 메시지 버블에 추가
/// </summary>
public class DoubleTouchLikeHandler : MonoBehaviour, IPointerClickHandler
{
    private int messageId;
    private RectTransform messageRect;
    private Action<int, RectTransform> onDoubleTap;

    private float lastTapTime = 0f;
    private float doubleTapThreshold = 0.3f;

    public void Initialize(int messageId, RectTransform rect, Action<int, RectTransform> callback)
    {
        this.messageId = messageId;
        this.messageRect = rect;
        this.onDoubleTap = callback;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        float currentTime = Time.time;
        float timeSinceLastTap = currentTime - lastTapTime;

        if (timeSinceLastTap < doubleTapThreshold && timeSinceLastTap > 0.1f)
        {
            // 더블탭 감지
            onDoubleTap?.Invoke(messageId, messageRect);
            lastTapTime = 0f; // 리셋
        }
        else
        {
            lastTapTime = currentTime;
        }
    }
}
