using UnityEngine;
using UnityEngine.EventSystems;

public class SwipeToClose : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform panelRect;
    public float dismissThreshold = 150f;
    public System.Action onClose;

    private Vector2 startPos;
    private Vector2 originalAnchoredPos;
    private bool isDragging = false;

    void Start()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        // Assuming the panel is open at y=0
        originalAnchoredPos = Vector2.zero; 
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        startPos = eventData.position;
        isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        DragPanel(eventData.position.y - startPos.y);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EndDrag(eventData.position.y - startPos.y);
    }

    public void DragPanel(float totalDeltaY)
    {
        // 탄성 효과: 위로 당길 때는 저항 (delta / 3)
        if (totalDeltaY > 0)
        {
            panelRect.anchoredPosition = new Vector2(0, totalDeltaY / 3f);
        }
        else
        {
            panelRect.anchoredPosition = new Vector2(0, totalDeltaY);
        }
    }

    public void EndDrag(float totalDeltaY)
    {
        isDragging = false;
        
        // 닫기 임계값 체크 (아래로)
        if (totalDeltaY < -dismissThreshold)
        {
            onClose?.Invoke();
        }
        else
        {
            // 탄성 복귀 (Snap Back)
            StartCoroutine(SnapBack());
        }
    }

    System.Collections.IEnumerator SnapBack()
    {
        Vector2 current = panelRect.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            panelRect.anchoredPosition = Vector2.Lerp(current, originalAnchoredPos, elapsed / 0.2f);
            yield return null;
        }
        panelRect.anchoredPosition = originalAnchoredPos;
    }
}
