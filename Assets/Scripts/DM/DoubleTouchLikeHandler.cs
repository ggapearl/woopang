using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;

/// <summary>
/// 더블터치로 좋아요 표시하는 핸들러
/// 메시지 버블에 추가
/// </summary>
public class DoubleTouchLikeHandler : MonoBehaviour, IPointerClickHandler
{
    private int messageId;
    private RectTransform messageRect;
    private Action<int, RectTransform> onDoubleTap;
    private Action onSingleTap;
    private Coroutine singleCo;

    private float lastTapTime = 0f;
    private float doubleTapThreshold = 0.3f;

    public void Initialize(int messageId, RectTransform rect, Action<int, RectTransform> callback)
    {
        this.messageId = messageId;
        this.messageRect = rect;
        this.onDoubleTap = callback;
    }

    /// <summary>한 번 누름 — 두 번 누름(좋아요)이 아닐 때만, 두 번째 누름을 기다렸다가 부른다. null 이면 끔</summary>
    public void SetSingleTap(Action callback)
    {
        onSingleTap = callback;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        float currentTime = Time.time;
        float timeSinceLastTap = currentTime - lastTapTime;

        if (timeSinceLastTap < doubleTapThreshold && timeSinceLastTap > 0.1f)
        {
            // 더블탭 감지
            if (singleCo != null) { StopCoroutine(singleCo); singleCo = null; }
            onDoubleTap?.Invoke(messageId, messageRect);
            lastTapTime = 0f; // 리셋
        }
        else
        {
            lastTapTime = currentTime;
            if (onSingleTap != null)
            {
                if (singleCo != null) StopCoroutine(singleCo);
                singleCo = StartCoroutine(SingleAfterWait());
            }
        }
    }

    private IEnumerator SingleAfterWait()
    {
        yield return new WaitForSeconds(doubleTapThreshold);
        singleCo = null;
        onSingleTap?.Invoke();
    }
}
