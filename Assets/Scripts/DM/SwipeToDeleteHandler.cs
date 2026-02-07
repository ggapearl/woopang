using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 스와이프로 삭제하는 핸들러 (우측→좌측 스와이프)
/// 대화 목록 아이템에 추가
/// 에디터에서도 마우스 드래그로 동작
/// </summary>
public class SwipeToDeleteHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public string userId;
    public float swipeThreshold = 100f;
    public float deleteButtonWidth = 80f;
    public Color deleteButtonColor = new Color(0.91f, 0.33f, 0.63f, 1f); // 핑크색 #E854A1

    private RectTransform rectTransform;
    private RectTransform contentRect;
    private GameObject deleteButton;
    private Vector2 startPosition;
    private Vector2 originalPosition;
    private bool isSwipeActive = false;
    private Action onDelete;
    private bool isInitialized = false;

    // 에디터용 마우스 드래그 지원
    private Vector2 pointerDownPosition;

    public void Initialize(string userId, float threshold, Action deleteCallback)
    {
        this.userId = userId;
        this.swipeThreshold = threshold;
        this.onDelete = deleteCallback;

        rectTransform = GetComponent<RectTransform>();
        originalPosition = Vector2.zero; // 초기 위치는 0으로 설정

        // Content 영역 찾기 (스와이프 대상)
        contentRect = transform.Find("Content")?.GetComponent<RectTransform>();
        if (contentRect == null)
            contentRect = rectTransform;

        // Image 컴포넌트 확인 (레이캐스트 타겟 필요)
        Image img = GetComponent<Image>();
        if (img == null)
        {
            img = gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // 투명
        }
        img.raycastTarget = true;

        if (deleteButton == null)
            CreateDeleteButton();

        isInitialized = true;
    }

    private void CreateDeleteButton()
    {
        // 삭제 버튼 생성
        deleteButton = new GameObject("DeleteButton");
        deleteButton.transform.SetParent(transform, false);

        RectTransform btnRect = deleteButton.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 0);
        btnRect.anchorMax = new Vector2(1, 1);
        btnRect.pivot = new Vector2(1, 0.5f);
        btnRect.sizeDelta = new Vector2(deleteButtonWidth, 0);
        btnRect.anchoredPosition = Vector2.zero;

        // 배경
        Image bg = deleteButton.AddComponent<Image>();
        bg.color = deleteButtonColor;

        // 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(deleteButton.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.text = "삭제";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        // 버튼 클릭
        Button btn = deleteButton.AddComponent<Button>();
        btn.onClick.AddListener(OnDeleteClicked);

        // 초기에는 숨김
        deleteButton.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isInitialized) return;

        startPosition = eventData.position;
        isSwipeActive = true;

    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isSwipeActive || !isInitialized) return;

        float deltaX = eventData.position.x - startPosition.x;

        // 좌측으로만 스와이프 허용
        if (deltaX < 0)
        {
            float offset = Mathf.Clamp(deltaX, -deleteButtonWidth, 0);
            contentRect.anchoredPosition = new Vector2(originalPosition.x + offset, originalPosition.y);

            // 삭제 버튼 표시
            if (deleteButton != null && Mathf.Abs(offset) > 10 && !deleteButton.activeSelf)
            {
                deleteButton.SetActive(true);
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isSwipeActive || !isInitialized) return;
        isSwipeActive = false;

        float deltaX = eventData.position.x - startPosition.x;


        if (deltaX < -swipeThreshold)
        {
            // 삭제 버튼 표시 상태로 고정
            contentRect.anchoredPosition = new Vector2(originalPosition.x - deleteButtonWidth, originalPosition.y);

        }
        else
        {
            // 원래 위치로 복귀
            ResetPosition();
        }
    }

    public void ResetPosition()
    {
        contentRect.anchoredPosition = originalPosition;
        if (deleteButton != null)
            deleteButton.SetActive(false);
    }

    private void OnDeleteClicked()
    {
        onDelete?.Invoke();
    }

    void OnDisable()
    {
        ResetPosition();
    }
}
