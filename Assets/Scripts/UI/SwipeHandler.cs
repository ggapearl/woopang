using UnityEngine;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// EventSystem 기반 스와이프 감지 핸들러
/// 새 Input System과 호환됨
/// </summary>
public class SwipeHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Settings")]
    public float swipeThreshold = 50f;
    public float maxSwipeTime = 0.8f;

    // 스와이프 이벤트
    public event Action OnSwipeLeft;
    public event Action OnSwipeRight;

    private Vector2 startPos;
    private float startTime;
    private bool isHorizontalSwipe = false;

    public void OnBeginDrag(PointerEventData eventData)
    {
        startPos = eventData.position;
        startTime = Time.time;
        isHorizontalSwipe = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isHorizontalSwipe)
        {
            Vector2 delta = eventData.position - startPos;
            if (Mathf.Abs(delta.x) > 20f || Mathf.Abs(delta.y) > 20f)
            {
                isHorizontalSwipe = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float swipeTime = Time.time - startTime;
        if (swipeTime > maxSwipeTime) return;

        Vector2 delta = eventData.position - startPos;
        float horizontalDist = Mathf.Abs(delta.x);
        float verticalDist = Mathf.Abs(delta.y);

        if (horizontalDist > verticalDist && horizontalDist > swipeThreshold)
        {
            if (delta.x < 0)
            {
                Debug.Log("[SwipeHandler] 왼쪽 스와이프 감지");
                OnSwipeLeft?.Invoke();
            }
            else
            {
                Debug.Log("[SwipeHandler] 오른쪽 스와이프 감지");
                OnSwipeRight?.Invoke();
            }
        }
    }
}
