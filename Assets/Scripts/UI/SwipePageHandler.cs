using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 좌우 스와이프로 페이지를 전환하는 핸들러
/// ScrollRect와 함께 사용하여 인스타그램 스타일 페이지 네비게이션 구현
/// </summary>
public class SwipePageHandler : MonoBehaviour, IEndDragHandler, IBeginDragHandler
{
    [Header("Settings")]
    [Tooltip("페이지 수")]
    public int pageCount = 2;

    [Tooltip("스냅 애니메이션 속도")]
    public float snapSpeed = 10f;

    [Tooltip("스와이프 민감도 (0~1, 페이지 너비 대비 스와이프 비율)")]
    public float swipeThreshold = 0.2f;

    [Header("References")]
    public ScrollRect scrollRect;

    // 현재 페이지 (0부터 시작)
    private int currentPage = 0;

    // 스냅 타겟 위치
    private float targetPosition;

    // 스냅 중인지 여부
    private bool isSnapping = false;

    // 드래그 시작 위치
    private float dragStartPosition;

    // 페이지 변경 이벤트
    public event Action<int> OnPageChanged;

    void Start()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();

        // 초기 위치 설정
        SetPage(0, false);
    }

    void Update()
    {
        if (isSnapping && scrollRect != null)
        {
            // 부드러운 스냅 애니메이션
            float current = scrollRect.horizontalNormalizedPosition;
            float newPos = Mathf.Lerp(current, targetPosition, Time.deltaTime * snapSpeed);
            scrollRect.horizontalNormalizedPosition = newPos;

            // 목표에 도달하면 스냅 종료
            if (Mathf.Abs(current - targetPosition) < 0.001f)
            {
                scrollRect.horizontalNormalizedPosition = targetPosition;
                isSnapping = false;
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isSnapping = false;
        if (scrollRect != null)
            dragStartPosition = scrollRect.horizontalNormalizedPosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (scrollRect == null || pageCount <= 1) return;

        float currentPos = scrollRect.horizontalNormalizedPosition;
        float dragDelta = currentPos - dragStartPosition;

        // 스와이프 방향과 거리에 따라 페이지 결정
        int newPage = currentPage;

        if (Mathf.Abs(dragDelta) > swipeThreshold)
        {
            // 충분히 스와이프한 경우
            if (dragDelta > 0 && currentPage < pageCount - 1)
            {
                newPage = currentPage + 1; // 오른쪽으로 (다음 페이지)
            }
            else if (dragDelta < 0 && currentPage > 0)
            {
                newPage = currentPage - 1; // 왼쪽으로 (이전 페이지)
            }
        }
        else
        {
            // 스와이프가 부족한 경우 가장 가까운 페이지로
            float pageWidth = 1f / (pageCount - 1);
            newPage = Mathf.RoundToInt(currentPos / pageWidth);
            newPage = Mathf.Clamp(newPage, 0, pageCount - 1);
        }

        SetPage(newPage, true);
    }

    /// <summary>
    /// 특정 페이지로 이동
    /// </summary>
    /// <param name="page">페이지 인덱스 (0부터 시작)</param>
    /// <param name="animate">애니메이션 여부</param>
    public void SetPage(int page, bool animate = true)
    {
        if (pageCount <= 1)
        {
            targetPosition = 0;
            if (scrollRect != null)
                scrollRect.horizontalNormalizedPosition = 0;
            return;
        }

        page = Mathf.Clamp(page, 0, pageCount - 1);

        // 페이지별 normalized position 계산
        targetPosition = (float)page / (pageCount - 1);

        if (animate)
        {
            isSnapping = true;
        }
        else if (scrollRect != null)
        {
            scrollRect.horizontalNormalizedPosition = targetPosition;
        }

        // 페이지가 변경된 경우 이벤트 발생
        if (page != currentPage)
        {
            currentPage = page;
            OnPageChanged?.Invoke(currentPage);
        }
        else
        {
            currentPage = page;
        }
    }

    /// <summary>
    /// 현재 페이지 가져오기
    /// </summary>
    public int GetCurrentPage()
    {
        return currentPage;
    }

    /// <summary>
    /// 다음 페이지로 이동
    /// </summary>
    public void NextPage()
    {
        if (currentPage < pageCount - 1)
            SetPage(currentPage + 1, true);
    }

    /// <summary>
    /// 이전 페이지로 이동
    /// </summary>
    public void PreviousPage()
    {
        if (currentPage > 0)
            SetPage(currentPage - 1, true);
    }
}
