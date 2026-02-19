using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class SwipePanelController : MonoBehaviour
{
    public RectTransform panel1;
    public RectTransform panel2;
    private Vector2 startPos;
    private Vector2 targetPos;
    private bool isDragging = false;
    private float swipeThreshold = 100f;
    private float moveSpeed = 15f;

    private int currentPanel = 0;
    private Vector2 panel1Pos;
    private Vector2 panel2Pos;
    private float panelWidth; // 부모 RectTransform 너비 (panelWidth 대신)

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        ResetToFirstPanel();
    }

    private void ResetToFirstPanel()
    {
        currentPanel = 0;
        // panelWidth가 아직 설정 안됐으면 Screen.width 사용
        float w = panelWidth > 0 ? panelWidth : Screen.width;
        panel1Pos = Vector2.zero;
        panel2Pos = new Vector2(w, 0);

        if (panel1 != null)
            panel1.anchoredPosition = panel1Pos;
        if (panel2 != null)
            panel2.anchoredPosition = panel2Pos;
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        // 부모 RectTransform 너비 사용 (Screen.width보다 정확)
        RectTransform parentRect = panel1 != null ? panel1.parent as RectTransform : null;
        panelWidth = parentRect != null ? parentRect.rect.width : Screen.width;
        if (panelWidth <= 0) panelWidth = Screen.width;

        panel1Pos = Vector2.zero;
        panel2Pos = new Vector2(panelWidth, 0);

        panel1.anchoredPosition = panel1Pos;
        panel2.anchoredPosition = panel2Pos;

        targetPos = panel1Pos;
    }

    void Update()
    {
        // 터치 입력 처리
        if (Touch.activeTouches.Count > 0)
        {
            Touch touch = Touch.activeTouches[0];

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                startPos = touch.screenPosition;
                isDragging = true;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && isDragging)
            {
                Vector2 currentPos = touch.screenPosition;
                float deltaX = currentPos.x - startPos.x;

                panel1.anchoredPosition = panel1Pos + new Vector2(deltaX, 0);
                panel2.anchoredPosition = panel2Pos + new Vector2(deltaX, 0);
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended && isDragging)
            {
                isDragging = false;
                Vector2 endPos = touch.screenPosition;
                float swipeDistance = endPos.x - startPos.x;

                if (Mathf.Abs(swipeDistance) > swipeThreshold)
                {
                    if (swipeDistance < 0 && currentPanel == 0)
                    {
                        SwitchToPanel(1);
                    }
                    else if (swipeDistance > 0 && currentPanel == 1)
                    {
                        SwitchToPanel(0);
                    }
                    else
                    {
                        RestoreCurrentPanelPosition();
                    }
                }
                else
                {
                    RestoreCurrentPanelPosition();
                }
            }
        }

        // 드래그 중이 아니면 목표 위치로 부드럽게 이동 (터치 없을 때도 실행)
        if (!isDragging)
        {
            panel1.anchoredPosition = Vector2.Lerp(panel1.anchoredPosition, panel1Pos, Time.deltaTime * moveSpeed);
            panel2.anchoredPosition = Vector2.Lerp(panel2.anchoredPosition, panel2Pos, Time.deltaTime * moveSpeed);

            // 목표에 충분히 가까우면 정확한 위치로 스냅
            if (Vector2.Distance(panel1.anchoredPosition, panel1Pos) < 1f)
                panel1.anchoredPosition = panel1Pos;
            if (Vector2.Distance(panel2.anchoredPosition, panel2Pos) < 1f)
                panel2.anchoredPosition = panel2Pos;
        }
    }

    /// <summary>
    /// ������ �гη� ��ȯ
    /// </summary>
    /// <param name="panelIndex">0: panel1, 1: panel2</param>
    public void SwitchToPanel(int panelIndex)
    {
        currentPanel = panelIndex;
        
        if (currentPanel == 0)
        {
            // Panel1 ǥ��
            panel1Pos = Vector2.zero;
            panel2Pos = new Vector2(panelWidth, 0);
        }
        else if (currentPanel == 1)
        {
            // Panel2 ǥ��
            panel1Pos = new Vector2(-panelWidth, 0);
            panel2Pos = Vector2.zero;
        }
        
    }

    /// <summary>
    /// ���� �г� ��ġ ����
    /// </summary>
    private void RestoreCurrentPanelPosition()
    {
        if (currentPanel == 0)
        {
            panel1Pos = Vector2.zero;
            panel2Pos = new Vector2(panelWidth, 0);
        }
        else
        {
            panel1Pos = new Vector2(-panelWidth, 0);
            panel2Pos = Vector2.zero;
        }
    }

    /// <summary>
    /// ���� Ȱ�� �г� �ε��� ��ȯ
    /// </summary>
    /// <returns>0: panel1, 1: panel2</returns>
    public int GetCurrentPanel()
    {
        return currentPanel;
    }

    /// <summary>
    /// ���� �г� ���� (�ܺο��� ȣ�� ����)
    /// </summary>
    /// <param name="panelIndex">0: panel1, 1: panel2</param>
    public void SetCurrentPanel(int panelIndex)
    {
        if (panelIndex >= 0 && panelIndex <= 1)
        {
            SwitchToPanel(panelIndex);
        }
    }
}