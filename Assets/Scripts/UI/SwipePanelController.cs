using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Collections;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class SwipePanelController : MonoBehaviour
{
    public RectTransform panel1;
    public RectTransform panel2;

    private Vector2 startPos;
    private float dragStartPosX;
    private bool isDragging = false;
    private float swipeThreshold = 50f;
    private float moveSpeed = 15f;

    private int currentPanel = 0;
    private float panelWidth;
    private float panelDistance;
    private float currentAnchoredX;

    [Header("Settings")]
    [Tooltip("다음 패널 미리보기 간격 (픽셀 단위).")]
    public float panelPreviewAmount = 80f;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        StartCoroutine(ResetToFirstPanelDelayed());
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    private IEnumerator ResetToFirstPanelDelayed()
    {
        currentPanel = 0;
        currentAnchoredX = 0;
        if (panel1 != null) panel1.anchoredPosition = new Vector2(0, 0);

        yield return null;
        ResetToFirstPanel();
    }

    public void ResetToFirstPanel()
    {
        currentPanel = 0;
        CalculateDimensions();
        currentAnchoredX = 0;
        if (panel1 != null) panel1.anchoredPosition = new Vector2(0, 0);
        UpdatePanelPositions();
    }

    void Start()
    {
        CalculateDimensions();
        currentAnchoredX = (currentPanel == 0) ? 0 : -panelDistance;
    }

    private void CalculateDimensions()
    {
        if (panel1 == null) return;
        
        RectTransform parentRect = panel1.parent as RectTransform;
        if (parentRect != null)
        {
            panelWidth = parentRect.rect.width;
        }
        else
        {
            panelWidth = Screen.width;
        }

        if (panelWidth <= 0) panelWidth = Screen.width;
        panelDistance = panelWidth - panelPreviewAmount;
    }

    void Update()
    {
        // 입력 처리는 Update에서 수행
        if (Touch.activeTouches.Count > 0)
        {
            Touch touch = Touch.activeTouches[0];

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                startPos = touch.screenPosition;
                dragStartPosX = currentAnchoredX;
                isDragging = true;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && isDragging)
            {
                float deltaX = touch.screenPosition.x - startPos.x;
                currentAnchoredX = dragStartPosX + deltaX;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended && isDragging)
            {
                isDragging = false;
                float swipeDistance = touch.screenPosition.x - startPos.x;

                if (Mathf.Abs(swipeDistance) > swipeThreshold)
                {
                    if (swipeDistance < 0 && currentPanel == 0) SwitchToPanel(1);
                    else if (swipeDistance > 0 && currentPanel == 1) SwitchToPanel(0);
                }
            }
        }
    }

    void LateUpdate()
    {
        // 실시간 거리 갱신 (화면 회전이나 크기 변경 대응)
        CalculateDimensions();

        if (!isDragging)
        {
            // 드래그 중이 아닐 때만 목표 위치로 보간
            float targetX = (currentPanel == 0) ? 0 : -panelDistance;
            currentAnchoredX = Mathf.Lerp(currentAnchoredX, targetX, Time.deltaTime * moveSpeed);

            if (Mathf.Abs(currentAnchoredX - targetX) < 0.1f)
                currentAnchoredX = targetX;
        }

        UpdatePanelPositions();
    }

    private void UpdatePanelPositions()
    {
        if (panel1 != null)
        {
            panel1.anchoredPosition = new Vector2(currentAnchoredX, 0);
            
            if (panel2 != null)
            {
                // panel2는 항상 panel1 기준의 상대 위치를 유지 (동기화)
                panel2.anchoredPosition = new Vector2(currentAnchoredX + panelDistance, 0);
            }
        }
    }

    public void SwitchToPanel(int panelIndex)
    {
        currentPanel = Mathf.Clamp(panelIndex, 0, 1);
    }

    public int GetCurrentPanel()
    {
        return currentPanel;
    }

    public void SetCurrentPanel(int panelIndex)
    {
        SwitchToPanel(panelIndex);
    }
}
