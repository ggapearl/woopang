using UnityEngine;
using UnityEngine.EventSystems;

public class SwipePanelController : MonoBehaviour
{
    public RectTransform panel1; // 첫 번째 패널
    public RectTransform panel2; // 두 번째 패널
    private Vector2 startPos;    // 드래그 시작 위치
    private Vector2 targetPos;   // 목표 위치
    private bool isDragging = false;
    private float swipeThreshold = 100f; // 스와이프로 성공으로 간주할 최소 거리
    private float moveSpeed = 15f;       // 패널 이동 속도

    private int currentPanel = 0; // 0: panel1, 1: panel2
    private Vector2 panel1Pos;    // panel1의 초기 위치
    private Vector2 panel2Pos;    // panel2의 초기 위치

    void Start()
    {
        // 초기 위치 설정
        panel1Pos = Vector2.zero; // panel1은 화면 중앙에 (0, 0)
        panel2Pos = new Vector2(Screen.width, 0); // panel2는 오른쪽에 숨기

        panel1.anchoredPosition = panel1Pos; // panel1을 화면에 표시
        panel2.anchoredPosition = panel2Pos; // panel2를 오른쪽에 숨기

        // 목표 위치 초기화
        targetPos = panel1Pos;
    }

    void Update()
    {
        // 터치 또는 마우스 입력 처리
        if (Input.GetMouseButtonDown(0))
        {
            startPos = Input.mousePosition;
            isDragging = true;
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 currentPos = Input.mousePosition;
            float deltaX = currentPos.x - startPos.x;

            // 패널을 드래그에 따라 이동
            panel1.anchoredPosition = panel1Pos + new Vector2(deltaX, 0);
            panel2.anchoredPosition = panel2Pos + new Vector2(deltaX, 0);
        }
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            Vector2 endPos = Input.mousePosition;
            float swipeDistance = endPos.x - startPos.x;

            // 스와이프 방향 및 거리에 따라 패널 전환
            if (Mathf.Abs(swipeDistance) > swipeThreshold)
            {
                if (swipeDistance < 0 && currentPanel == 0) // 왼쪽으로 스와이프
                {
                    SwitchToPanel(1);
                }
                else if (swipeDistance > 0 && currentPanel == 1) // 오른쪽으로 스와이프
                {
                    SwitchToPanel(0);
                }
            }
            else
            {
                // 스와이프 거리가 충분하지 않으면 원래 위치로 복귀
                RestoreCurrentPanelPosition();
            }
        }

        // 부드럽게 패널 이동
        if (!isDragging)
        {
            panel1.anchoredPosition = Vector2.Lerp(panel1.anchoredPosition, panel1Pos, Time.deltaTime * moveSpeed);
            panel2.anchoredPosition = Vector2.Lerp(panel2.anchoredPosition, panel2Pos, Time.deltaTime * moveSpeed);
        }
    }

    /// <summary>
    /// 지정된 패널로 전환
    /// </summary>
    /// <param name="panelIndex">0: panel1, 1: panel2</param>
    public void SwitchToPanel(int panelIndex)
    {
        currentPanel = panelIndex;
        
        if (currentPanel == 0)
        {
            // Panel1 표시
            panel1Pos = Vector2.zero;
            panel2Pos = new Vector2(Screen.width, 0);
        }
        else if (currentPanel == 1)
        {
            // Panel2 표시
            panel1Pos = new Vector2(-Screen.width, 0);
            panel2Pos = Vector2.zero;
        }
        
        Debug.Log($"[SwipePanelController] 패널 전환: {currentPanel}");
    }

    /// <summary>
    /// 현재 패널 위치 복원
    /// </summary>
    private void RestoreCurrentPanelPosition()
    {
        if (currentPanel == 0)
        {
            panel1Pos = Vector2.zero;
            panel2Pos = new Vector2(Screen.width, 0);
        }
        else
        {
            panel1Pos = new Vector2(-Screen.width, 0);
            panel2Pos = Vector2.zero;
        }
    }

    /// <summary>
    /// 현재 활성 패널 인덱스 반환
    /// </summary>
    /// <returns>0: panel1, 1: panel2</returns>
    public int GetCurrentPanel()
    {
        return currentPanel;
    }

    /// <summary>
    /// 현재 패널 설정 (외부에서 호출 가능)
    /// </summary>
    /// <param name="panelIndex">0: panel1, 1: panel2</param>
    public void SetCurrentPanel(int panelIndex)
    {
        if (panelIndex >= 0 && panelIndex <= 1)
        {
            SwitchToPanel(panelIndex);
            Debug.Log($"[SwipePanelController] 외부에서 패널 설정: {panelIndex}");
        }
    }
}