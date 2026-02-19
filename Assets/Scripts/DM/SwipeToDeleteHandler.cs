using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 스와이프로 삭제하는 핸들러 (우측→좌측 스와이프)
/// 오버레이 모드: 콘텐츠는 움직이지 않고 삭제 버튼이 우측에서 덮어씀
/// ScrollRect와 호환: 수직 드래그는 스크롤, 수평 드래그는 스와이프
/// </summary>
public class SwipeToDeleteHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public string userId;
    [SerializeField] private float swipeThreshold = 100f;
    [SerializeField] private float deleteButtonWidth = 220f;
    [SerializeField] private Color deleteButtonColor = new Color(0.902f, 0.294f, 0.294f, 1f); // #E64B4B
    [SerializeField] private int deleteFontSize = 50;

    private RectTransform rectTransform;
    private GameObject deleteButton;
    private RectTransform deleteButtonRect;
    private Vector2 startPosition;
    private bool isSwipeActive = false;

    private Action onDelete;
    private bool isInitialized = false;

    // ScrollRect 연동
    private ScrollRect parentScrollRect;
    private bool isDragDirectionDecided = false;
    private bool isHorizontalDrag = false;

    // 에디터용 마우스 드래그 지원
    private Vector2 pointerDownPosition;

    // 루트 Button 캐싱 (스와이프 시 비활성화하여 클릭 충돌 방지)
    private Button rootButton;

    // Content 내부 Button도 캐싱
    private Button contentButton;

    public void Initialize(string userId, float threshold, Action deleteCallback)
    {
        this.userId = userId;
        this.swipeThreshold = threshold;
        this.onDelete = deleteCallback;

        rectTransform = GetComponent<RectTransform>();

        // 부모 ScrollRect 캐싱
        parentScrollRect = GetComponentInParent<ScrollRect>();

        // 루트 Button 캐싱 (스와이프 중 클릭 방지용)
        rootButton = GetComponent<Button>();

        // Content 내부 Button도 캐싱
        Transform content = transform.Find("Content");
        if (content != null)
            contentButton = content.GetComponent<Button>();

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
        deleteButton = new GameObject("DeleteButton");
        deleteButton.transform.SetParent(transform, false);

        deleteButtonRect = deleteButton.AddComponent<RectTransform>();
        deleteButtonRect.anchorMin = new Vector2(1, 0);
        deleteButtonRect.anchorMax = new Vector2(1, 1);
        deleteButtonRect.pivot = new Vector2(1, 0.5f);
        deleteButtonRect.sizeDelta = new Vector2(0, 0); // 초기 너비 0
        deleteButtonRect.anchoredPosition = Vector2.zero;

        // 배경
        Image bg = deleteButton.AddComponent<Image>();
        bg.color = deleteButtonColor;

        // 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(deleteButton.transform, false);

        Text text = textObj.AddComponent<Text>();
        string deleteText = "Delete";
        if (LocalizationManager.Instance != null)
            deleteText = LocalizationManager.Instance.GetText("swipe_delete");
        text.text = deleteText;
        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        text.font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = deleteFontSize;
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

        // 삭제 버튼은 콘텐츠 위에 (오버레이)
        deleteButton.transform.SetAsLastSibling();

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
        isDragDirectionDecided = false;
        isHorizontalDrag = false;
        isSwipeActive = false;

        // 부모 ScrollRect에도 BeginDrag 전달 (방향 결정 전에 준비)
        if (parentScrollRect != null)
            parentScrollRect.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isInitialized) return;

        // 드래그 방향 결정 (최초 1회)
        if (!isDragDirectionDecided)
        {
            float deltaX = Mathf.Abs(eventData.position.x - startPosition.x);
            float deltaY = Mathf.Abs(eventData.position.y - startPosition.y);

            // 충분한 이동이 있어야 방향 결정
            if (deltaX > 10f || deltaY > 10f)
            {
                isDragDirectionDecided = true;
                isHorizontalDrag = deltaX > deltaY;

                if (isHorizontalDrag)
                {
                    isSwipeActive = true;

                    // 루트 Button 비활성화 (스와이프 중 클릭 방지)
                    if (rootButton != null)
                        rootButton.enabled = false;
                    if (contentButton != null)
                        contentButton.enabled = false;

                    // ScrollRect 드래그 취소
                    if (parentScrollRect != null)
                        parentScrollRect.OnEndDrag(eventData);
                }
            }
        }

        // 수직 드래그 → 부모 ScrollRect에 전달
        if (isDragDirectionDecided && !isHorizontalDrag)
        {
            if (parentScrollRect != null)
                parentScrollRect.OnDrag(eventData);
            return;
        }

        // 수평 스와이프 처리
        if (!isSwipeActive) return;

        float swipeDeltaX = eventData.position.x - startPosition.x;

        // 좌측으로만 스와이프 허용 → 삭제 버튼이 우측에서 오버레이로 나타남
        if (swipeDeltaX < 0)
        {
            float width = Mathf.Clamp(-swipeDeltaX, 0, deleteButtonWidth);
            deleteButtonRect.sizeDelta = new Vector2(width, 0);

            // 삭제 버튼 표시
            if (!deleteButton.activeSelf && width > 10)
                deleteButton.SetActive(true);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isInitialized) return;

        // 수직 드래그였으면 ScrollRect에 EndDrag 전달
        if (isDragDirectionDecided && !isHorizontalDrag)
        {
            if (parentScrollRect != null)
                parentScrollRect.OnEndDrag(eventData);

            isDragDirectionDecided = false;
            return;
        }

        if (!isSwipeActive)
        {
            isDragDirectionDecided = false;
            return;
        }

        isSwipeActive = false;
        isDragDirectionDecided = false;

        float deltaX = eventData.position.x - startPosition.x;

        if (deltaX < -swipeThreshold)
        {
            // 오버레이 고정 (삭제 버튼 전체 너비로 노출)
            deleteButtonRect.sizeDelta = new Vector2(deleteButtonWidth, 0);
        }
        else
        {
            // 원래 위치로 복귀
            ResetPosition();
        }
    }

    public void ResetPosition()
    {
        if (deleteButtonRect != null)
            deleteButtonRect.sizeDelta = new Vector2(0, 0);

        if (deleteButton != null)
            deleteButton.SetActive(false);

        // 루트 Button 복원 (클릭 가능 상태로)
        if (rootButton != null)
            rootButton.enabled = true;
        if (contentButton != null)
            contentButton.enabled = true;
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
