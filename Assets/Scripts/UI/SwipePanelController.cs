using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Collections;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class SwipePanelController : MonoBehaviour
{
    public RectTransform panel1;
    public RectTransform panel2;
    [Tooltip("세 번째 카드(보물찾기) — 비어 있으면 두 장")]
    public RectTransform panel3;

    private int PanelCount => panel3 != null ? 3 : 2;

    [Tooltip("세 번째 카드는 따로 — 도크 '보물찾기' 칸이 이 카드 한 장으로 연다. 추가 창은 두 장만 넘긴다 (2026-11-09)")]
    public bool panel3Alone;

    /// <summary>세 번째 카드 한 장으로 열려 있다 (도크 '보물찾기')</summary>
    public bool AloneShowing => panel3Alone && panel3 != null && currentPanel == 2;

    /// <summary>창이 켜질 때 돌아갈 카드 — 보물찾기 칸으로 열면 2. 창이 닫히면 0 으로 (R0926HuntDock — 이 컴포넌트는 늘 켜진 SwipePanel 에 있다)</summary>
    [System.NonSerialized] public int HomePanel;

    private int LastSwipePanel => panel3Alone ? 1 : PanelCount - 1;
    private const float Away = 100000f;   // 따로 쓰는 카드를 치워 두는 자리 (화면 밖 · 틀이 잘라 낸다)

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
        HomePanel = 0;
    }

    private IEnumerator ResetToFirstPanelDelayed()
    {
        currentPanel = Home;
        CalculateDimensions();
        currentAnchoredX = TargetX(currentPanel);
        UpdatePanelPositions();

        yield return null;
        ResetToFirstPanel();
    }

    private int Home => panel3Alone && panel3 != null && HomePanel == 2 ? 2 : 0;

    public void ResetToFirstPanel()
    {
        currentPanel = Home;
        CalculateDimensions();
        currentAnchoredX = TargetX(currentPanel);
        UpdatePanelPositions();
    }

    void Start()
    {
        CalculateDimensions();
        currentAnchoredX = TargetX(currentPanel);
    }

    // 첫 카드는 baseOffsetX 자리(왼쪽에 붙이고 다음 카드를 오른쪽에 살짝), 두 번째부터는 가운데 —
    // 옆 카드가 양쪽에(마지막 카드는 왼쪽에) 살짝 보인다 (2026-10-13 대표님)
    private float TargetX(int panel) => -panel * panelDistance - (panel > 0 ? baseOffsetX : 0f);

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
        if (panel1 == null || !panel1.gameObject.activeInHierarchy || locked || AloneShowing) { isDragging = false; return; }

        // 입력 처리는 Update에서 수행
        if (Touch.activeTouches.Count > 0)
        {
            Touch touch = Touch.activeTouches[0];

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                startPos = touch.screenPosition;
                dragStartPosX = currentAnchoredX;
                isDragging = !R0926PhotoStrip.Covers(touch.screenPosition);   // 옆으로 밀리는 추가 사진 줄 위 — 그 줄 몫
                dirDecided = false;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && isDragging)
            {
                Vector2 d = touch.screenPosition - startPos;
                if (!dirDecided)
                {
                    // 목록 '목록|지도|설정'(R0926SheetModes)과 같은 방향 잠금 — 14px 움직이면 정하고, 옆으로 1.3배 넘게 갈 때만 카드를 민다.
                    // 아래로 끄는 손가락은 시트 밀어 닫기(R0926SwipeDismiss) 몫 — 같은 순간 같은 기준으로 정해 둘이 함께 움직이지 않는다
                    if (d.magnitude < 14f) return;
                    dirDecided = true;
                    horizontal = Mathf.Abs(d.x) >= Mathf.Abs(d.y) * 1.3f && !R0926SwipeDismiss.AnyDragging && !R0926PhotoReorder.Holding && !R0926HuntCard.Reviewing;   // 꾹 눌러 든 사진을 옮기는 손가락 · 보물 숨기기 확인 카드
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
                    if (swipeDistance < 0 && currentPanel < LastSwipePanel) SwitchToPanel(currentPanel + 1);
                    else if (swipeDistance > 0 && currentPanel > 0) SwitchToPanel(currentPanel - 1);
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
            float targetX = TargetX(currentPanel);
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
            // 세 번째 카드를 따로 쓰면 — 그 카드가 보일 땐 앞 두 장을, 앞 두 장이 보일 땐 그 카드를 화면 밖으로 (옆에 살짝 보이지 않게)
            bool alone = AloneShowing;
            bool aside = panel3Alone && !alone;
            panel1.anchoredPosition = new Vector2(alone ? -Away : currentAnchoredX + baseOffsetX, 0);

            if (panel2 != null)
            {
                // panel2는 항상 panel1 기준의 상대 위치를 유지 (동기화)
                panel2.anchoredPosition = new Vector2(alone ? -Away : currentAnchoredX + baseOffsetX + panelDistance, 0);
            }
            if (panel3 != null) panel3.anchoredPosition = new Vector2(aside ? Away : currentAnchoredX + baseOffsetX + panelDistance * 2f, 0);
        }
    }

    public void SwitchToPanel(int panelIndex)
    {
        int to = Mathf.Clamp(panelIndex, 0, PanelCount - 1);
        bool jump = panel3Alone && panel3 != null && (to == 2) != (currentPanel == 2);
        currentPanel = to;
        if (panel3Alone) HomePanel = to == 2 ? 2 : 0;   // 열린 동안 첫 장으로 되돌리는 곳이 있어도 지금 쓰는 쪽(추가 / 보물찾기)에 머문다
        if (!jump) return;
        // 추가 두 장 ↔ 보물찾기 한 장은 옆으로 밀지 않고 바로 바꾼다
        isDragging = false;
        CalculateDimensions();
        currentAnchoredX = TargetX(currentPanel);
        UpdatePanelPositions();
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
