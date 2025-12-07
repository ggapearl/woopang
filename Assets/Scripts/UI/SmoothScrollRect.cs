using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ListPanel의 스와이프 민감도를 향상시키는 ScrollRect 확장
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class SmoothScrollRect : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Scroll Sensitivity")]
    [Tooltip("스크롤 민감도 배율 (기본 1.0, 높을수록 민감)")]
    [SerializeField] private float scrollSensitivity = 2.5f;

    [Tooltip("관성 배율 (기본 1.0, 높을수록 부드럽게 스크롤)")]
    [SerializeField] private float inertiaMult = 1.5f;

    [Tooltip("최소 드래그 거리 (픽셀) - 낮을수록 민감")]
    [SerializeField] private float minDragDistance = 5f;

    [Header("Debug")]
    [Tooltip("디버그 로그 활성화")]
    [SerializeField] private bool enableDebugLog = true;

    private ScrollRect scrollRect;
    private Vector2 lastDragPosition;
    private bool isDragging = false;
    private System.Diagnostics.Stopwatch dragTimer = new System.Diagnostics.Stopwatch();

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            // ScrollRect 기본 설정 최적화
            scrollRect.scrollSensitivity = 20f; // 기본값보다 높게
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f * inertiaMult; // 관성 증가
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f; // 바운스 효과 감소

            Debug.Log($"[SmoothScrollRect] 초기화 완료 - Sensitivity={scrollSensitivity}, Inertia={inertiaMult}");
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        lastDragPosition = eventData.position;
        dragTimer.Restart();

        // 기본 ScrollRect BeginDrag 이벤트 전달
        if (scrollRect != null)
        {
            scrollRect.OnBeginDrag(eventData);

            if (enableDebugLog)
            {
                Debug.Log($"[SmoothScroll] BeginDrag - Pos={eventData.position}, ScrollRect.velocity={scrollRect.velocity}");
            }
        }
        else if (enableDebugLog)
        {
            Debug.LogError("[SmoothScroll] ScrollRect is NULL!");
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || scrollRect == null) return;

        dragTimer.Stop();
        float frameTime = (float)dragTimer.Elapsed.TotalMilliseconds;
        dragTimer.Restart();

        // 기본 ScrollRect 드래그 이벤트 전달 (즉각 반응)
        scrollRect.OnDrag(eventData);

        // 추가 민감도 증폭
        Vector2 currentDragPosition = eventData.position;
        Vector2 dragDelta = currentDragPosition - lastDragPosition;

        if (dragDelta.sqrMagnitude > 0.01f) // 매우 작은 값만 필터링
        {
            // 민감도 배율 적용하여 velocity 증폭
            float verticalDelta = dragDelta.y * scrollSensitivity;
            Vector2 additionalVelocity = new Vector2(0f, verticalDelta * 15f);

            // 기존 velocity에 추가 (누적)
            Vector2 oldVelocity = scrollRect.velocity;
            scrollRect.velocity += additionalVelocity;

            if (enableDebugLog && frameTime > 16f)
            {
                Debug.LogWarning($"[SmoothScroll] LAG! FrameTime={frameTime:F1}ms, Delta={dragDelta}, OldVel={oldVelocity}, NewVel={scrollRect.velocity}");
            }
        }

        lastDragPosition = currentDragPosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        // 기본 ScrollRect EndDrag 이벤트 전달
        if (scrollRect != null)
        {
            scrollRect.OnEndDrag(eventData);

            // 드래그 종료 시 관성 증폭
            Vector2 finalVelocity = scrollRect.velocity * inertiaMult;
            scrollRect.velocity = finalVelocity;
        }
    }

    /// <summary>
    /// 민감도 런타임 조정
    /// </summary>
    public void SetScrollSensitivity(float sensitivity)
    {
        scrollSensitivity = Mathf.Clamp(sensitivity, 0.5f, 5f);
        Debug.Log($"[SmoothScrollRect] 민감도 변경: {scrollSensitivity}");
    }

    /// <summary>
    /// 관성 배율 런타임 조정
    /// </summary>
    public void SetInertiaMultiplier(float inertia)
    {
        inertiaMult = Mathf.Clamp(inertia, 0.5f, 3f);
        if (scrollRect != null)
        {
            scrollRect.decelerationRate = 0.135f * inertiaMult;
        }
        Debug.Log($"[SmoothScrollRect] 관성 변경: {inertiaMult}");
    }
}
