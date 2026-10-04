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
    private bool dirDecided, horizontal;
    private float panelWidth;

    /// <summary>키보드 위 입력줄이 떠 있는 동안 밀기만 막는다 (컴포넌트를 끄고 켜면 OnEnable 이 첫 카드로 되돌려
    /// 3D모델에 이름을 쓰고 나면 '장소' 탭으로 넘어갔다)</summary>
    [System.NonSerialized] public bool locked;
    private float panelDistance;
    private float currentAnchoredX;

    [Header("Settings")]
    [Tooltip("다음 패널 미리보기 간격 (픽셀 단위).")]
    public float panelPreviewAmount = 80f;

    [Tooltip("두 카드를 함께 옆으로 미는 기본 위치 — 첫 카드를 왼쪽에 붙이고 오른쪽 끝에 다음 카드를 살짝 보이게 할 때")]
    public float baseOffsetX = 0f;

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
        if (panel1 != null) panel1.anchoredPosition = new Vector2(baseOffsetX, 0);

        yield return null;
        ResetToFirstPanel();
    }

    public void ResetToFirstPanel()
    {
        currentPanel = 0;
        CalculateDimensions();
        currentAnchoredX = 0;
        if (panel1 != null) panel1.anchoredPosition = new Vector2(baseOffsetX, 0);
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
        // 업로드 화면이 닫혀 있을 땐 다른 화면의 가로 끌기(지도·분류 칩 등)를 받지 않는다
        if (panel1 == null || !panel1.gameObject.activeInHierarchy || locked) { isDragging = false; return; }

        // 입력 처리는 Update에서 수행
        if (Touch.activeTouches.Count > 0)
        {
            Touch touch = Touch.activeTouches[0];

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                startPos = touch.screenPosition;
                dragStartPosX = currentAnchoredX;
                isDragging = true;
                dirDecided = false;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && isDragging)
            {
                Vector2 d = touch.screenPosition - startPos;
                if (!dirDecided)
                {
                    // 확실히 옆으로 움직일 때만 카드를 민다 — 위아래로 움직이는 손가락에 카드가 같이 흔들리지 않게
                    if (Mathf.Abs(d.x) < 24f && Mathf.Abs(d.y) < 24f) return;
                    dirDecided = true;
                    horizontal = Mathf.Abs(d.x) > Mathf.Abs(d.y) * 1.2f;
                    if (!horizontal) { isDragging = false; return; }
                }
                currentAnchoredX = dragStartPosX + d.x;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended && isDragging)
            {
                isDragging = false;
                if (!dirDecided || !horizontal) return;
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
        if (panel1 == null || !panel1.gameObject.activeInHierarchy) return;   // 닫혀 있는 동안은 계산하지 않는다

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
            panel1.anchoredPosition = new Vector2(currentAnchoredX + baseOffsetX, 0);
            
            if (panel2 != null)
            {
                // panel2는 항상 panel1 기준의 상대 위치를 유지 (동기화)
                panel2.anchoredPosition = new Vector2(currentAnchoredX + baseOffsetX + panelDistance, 0);
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
