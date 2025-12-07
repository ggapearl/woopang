using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 성능 최적화된 ScrollRect
/// EventSystem 부하 감소 및 즉각 반응
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class OptimizedScrollRect : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Scroll Settings")]
    [SerializeField] private float scrollMultiplier = 3.0f;
    [SerializeField] private float inertiaMult = 2.0f;

    private ScrollRect scrollRect;
    private bool isDragging = false;
    private Vector2 lastPosition;

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            // 기본 설정 최적화
            scrollRect.scrollSensitivity = 1f; // 낮게 설정 (직접 제어)
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.2f;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.05f;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (scrollRect == null) return;

        isDragging = true;
        lastPosition = eventData.position;

        // ScrollRect의 드래그 시작 (속도 초기화)
        scrollRect.velocity = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || scrollRect == null) return;

        // 델타 계산
        Vector2 currentPosition = eventData.position;
        Vector2 delta = currentPosition - lastPosition;
        lastPosition = currentPosition;

        // 직접 velocity 설정 (EventSystem 우회)
        // Y축만 사용 (수직 스크롤)
        float velocityY = delta.y * scrollMultiplier * 100f; // 픽셀을 속도로 변환

        scrollRect.velocity = new Vector2(0f, velocityY);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (scrollRect == null) return;

        isDragging = false;

        // 관성 증폭
        scrollRect.velocity *= inertiaMult;
    }

    /// <summary>
    /// 설정 조정
    /// </summary>
    public void SetScrollMultiplier(float multiplier)
    {
        scrollMultiplier = Mathf.Clamp(multiplier, 1f, 10f);
    }

    public void SetInertiaMult(float inertia)
    {
        inertiaMult = Mathf.Clamp(inertia, 0.5f, 5f);
    }
}
