using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 아래로 스와이프하여 패널 닫기
/// 댓글창 등 하단에서 올라오는 패널에 사용
/// 템플릿 터치와 무관하게 아래 방향 스와이프로 닫힘
/// </summary>
public class SwipeToClose : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform panelRect;
    public float dismissThreshold = 100f; // 닫기 임계값 (더 낮게)
    public float velocityThreshold = 500f; // 빠른 스와이프 속도 임계값
    public System.Action onClose;

    [Header("Handle Bar (Optional)")]
    [Tooltip("상단의 드래그 핸들바 영역 (없으면 전체 패널에서 스와이프 가능)")]
    public RectTransform handleBar;

    private Vector2 startPos;
    private Vector2 lastPos;
    private float lastTime;
    private Vector2 originalAnchoredPos;
    private bool isDragging = false;
    private bool isValidSwipe = false; // 유효한 스와이프인지 (수직 우세)

    void Start()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        originalAnchoredPos = Vector2.zero;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        startPos = eventData.position;
        lastPos = eventData.position;
        lastTime = Time.unscaledTime;
        isDragging = true;
        isValidSwipe = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        Vector2 delta = eventData.position - startPos;

        // 수평 이동이 너무 크면 스와이프 무시 (스크롤과 구분)
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.5f && Mathf.Abs(delta.x) > 30f)
        {
            isValidSwipe = false;
            return;
        }

        float deltaY = eventData.position.y - startPos.y;
        lastPos = eventData.position;
        lastTime = Time.unscaledTime;

        DragPanel(deltaY);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            isDragging = false;
            return;
        }

        float deltaY = eventData.position.y - startPos.y;

        // 속도 계산
        float timeDelta = Time.unscaledTime - lastTime;
        float velocity = 0f;
        if (timeDelta > 0.001f)
        {
            velocity = (eventData.position.y - lastPos.y) / timeDelta;
        }

        EndDrag(deltaY, velocity);
    }

    public void DragPanel(float totalDeltaY)
    {
        if (!isValidSwipe) return;

        // 탄성 효과: 위로 당길 때는 저항 (delta / 4)
        if (totalDeltaY > 0)
        {
            panelRect.anchoredPosition = new Vector2(0, totalDeltaY / 4f);
        }
        else
        {
            // 아래로 당길 때는 그대로
            panelRect.anchoredPosition = new Vector2(0, totalDeltaY);
        }
    }

    public void EndDrag(float totalDeltaY, float velocity = 0f)
    {
        isDragging = false;

        if (!isValidSwipe)
        {
            StartCoroutine(SnapBack());
            return;
        }

        // 닫기 조건: 임계값 초과 OR 빠른 아래방향 스와이프
        bool shouldClose = totalDeltaY < -dismissThreshold || velocity < -velocityThreshold;

        if (shouldClose)
        {
            // 슬라이드 아웃 애니메이션 후 닫기
            StartCoroutine(SlideOutAndClose());
        }
        else
        {
            // 탄성 복귀 (Snap Back)
            StartCoroutine(SnapBack());
        }
    }

    System.Collections.IEnumerator SlideOutAndClose()
    {
        Vector2 current = panelRect.anchoredPosition;
        Vector2 target = new Vector2(0, -Screen.height);
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t; // ease in
            panelRect.anchoredPosition = Vector2.Lerp(current, target, t);
            yield return null;
        }

        panelRect.anchoredPosition = target;
        onClose?.Invoke();
    }

    System.Collections.IEnumerator SnapBack()
    {
        Vector2 current = panelRect.anchoredPosition;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1 - (1 - t) * (1 - t); // ease out
            panelRect.anchoredPosition = Vector2.Lerp(current, originalAnchoredPos, t);
            yield return null;
        }

        panelRect.anchoredPosition = originalAnchoredPos;
    }

    /// <summary>
    /// 외부에서 원래 위치 리셋
    /// </summary>
    public void ResetPosition()
    {
        if (panelRect != null)
            panelRect.anchoredPosition = originalAnchoredPos;
    }
}
