using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// 아래로 스와이프하여 패널 닫기
/// Enhanced Touch 기반 — DoubleTap3D와 동일한 입력 시스템 사용
/// EventSystem 드래그 핸들러 대신 직접 터치 추적
/// </summary>
public class SwipeToClose : MonoBehaviour
{
    public RectTransform panelRect;
    public float dismissThreshold = 100f;
    public float velocityThreshold = 500f;
    public System.Action onClose;

    [Header("Handle Bar (Optional)")]
    [Tooltip("상단의 드래그 핸들바 영역 (없으면 전체 패널에서 스와이프 가능)")]
    public RectTransform handleBar;

    [Tooltip("추가 스와이프 허용 영역들 (타이틀 텍스트 등). handleBar와 함께 OR로 판정됨.")]
    public RectTransform[] additionalSwipeAreas;

    [Header("Expand (Swipe Up)")]
    [Tooltip("위로 스와이프 시 패널 전체 확장 (키보드 없이)")]
    public bool enableSwipeUpExpand = false;
    [Tooltip("위로 스와이프 시 확장 콜백")]
    public System.Action onExpand;
    [Tooltip("확장 임계값 (위로 이동 픽셀)")]
    public float expandThreshold = 80f;

    private Vector2 startPos;
    private Vector2 lastPos;
    private float lastTime;
    private Vector2 originalAnchoredPos;
    private bool isDragging = false;
    private bool isValidSwipe = false;
    private float openTime = -1f;
    private const float OPEN_COOLDOWN = 0.5f;

    // Enhanced Touch 활성화 추적
    private bool touchEnabled;

    // 터치 식별 (멀티터치 시 동일 터치 추적)
    private int trackingFingerId = -1;

    void Start()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        originalAnchoredPos = Vector2.zero;
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        touchEnabled = true;
    }

    void OnDisable()
    {
        isDragging = false;
        isValidSwipe = false;
        trackingFingerId = -1;

        if (touchEnabled)
        {
            EnhancedTouchSupport.Disable();
            touchEnabled = false;
        }
    }

    /// <summary>
    /// 패널 오픈 시 호출 — 쿨다운 시작
    /// </summary>
    public void NotifyOpened()
    {
        openTime = Time.unscaledTime;
        isDragging = false;
        isValidSwipe = false;
        trackingFingerId = -1;
    }

    void Update()
    {
        if (panelRect == null) return;

        // Enhanced Touch 기반 터치 처리
        ProcessEnhancedTouch();

        // 에디터 마우스 fallback
#if UNITY_EDITOR
        ProcessEditorMouse();
#endif
    }

    // ============================================================
    // Enhanced Touch 처리 (New Input System)
    // ============================================================

    private void ProcessEnhancedTouch()
    {
        var activeTouches = Touch.activeTouches;
        if (activeTouches.Count == 0)
        {
            // 터치가 없으면 추적 중이던 드래그 종료
            if (isDragging && trackingFingerId >= 0)
            {
                FinishDrag(lastPos);
                trackingFingerId = -1;
            }
            return;
        }

        // 추적 중인 터치 찾기
        for (int i = 0; i < activeTouches.Count; i++)
        {
            var touch = activeTouches[i];

            if (touch.phase == TouchPhase.Began)
            {
                if (isDragging) continue; // 이미 다른 터치 추적 중
                TryStartDrag(touch.screenPosition, touch.finger.index);
            }
            else if (touch.finger.index == trackingFingerId)
            {
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    UpdateDrag(touch.screenPosition);
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    FinishDrag(touch.screenPosition);
                    trackingFingerId = -1;
                }
            }
        }
    }

    // ============================================================
    // 에디터 마우스 처리
    // ============================================================

#if UNITY_EDITOR
    private bool mouseTracking = false;

    private void ProcessEditorMouse()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!isDragging)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                TryStartDrag(mousePos, -99);
                mouseTracking = true;
            }
        }
        else if (Mouse.current.leftButton.isPressed && mouseTracking && isDragging)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            UpdateDrag(mousePos);
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame && mouseTracking)
        {
            if (isDragging)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                FinishDrag(mousePos);
            }
            mouseTracking = false;
            trackingFingerId = -1;
        }
    }
#endif

    // ============================================================
    // 드래그 로직
    // ============================================================

    private void TryStartDrag(Vector2 screenPos, int fingerId)
    {
        // 쿨다운 체크
        if (Time.unscaledTime - openTime < OPEN_COOLDOWN)
        {
            return;
        }

        // 터치가 패널 위에 있는지 확인
        if (!IsTouchOnPanel(screenPos))
            return;

        startPos = screenPos;
        lastPos = screenPos;
        lastTime = Time.unscaledTime;
        isDragging = true;
        isValidSwipe = true;
        trackingFingerId = fingerId;
    }

    private void UpdateDrag(Vector2 screenPos)
    {
        if (!isDragging) return;
        if (Time.unscaledTime - openTime < OPEN_COOLDOWN)
        {
            isDragging = false;
            isValidSwipe = false;
            return;
        }

        Vector2 delta = screenPos - startPos;

        // 수평 이동이 너무 크면 스와이프 무시 (댓글 스크롤과 구분)
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.5f && Mathf.Abs(delta.x) > 30f)
        {
            isValidSwipe = false;
            return;
        }

        float deltaY = screenPos.y - startPos.y;
        lastPos = screenPos;
        lastTime = Time.unscaledTime;

        DragPanel(deltaY);
    }

    private void FinishDrag(Vector2 screenPos)
    {
        if (!isDragging)
        {
            isDragging = false;
            return;
        }

        if (Time.unscaledTime - openTime < OPEN_COOLDOWN)
        {
            isDragging = false;
            StartCoroutine(SnapBack());
            return;
        }

        float deltaY = screenPos.y - startPos.y;

        // 속도 계산
        float timeDelta = Time.unscaledTime - lastTime;
        float velocity = 0f;
        if (timeDelta > 0.001f)
        {
            velocity = (screenPos.y - lastPos.y) / timeDelta;
        }

        EndDrag(deltaY, velocity);
    }

    // ============================================================
    // 터치 위치 확인
    // ============================================================

    private bool IsTouchOnPanel(Vector2 screenPos)
    {
        // handleBar도 없고 additionalSwipeAreas도 없으면 전체 패널 체크
        if (handleBar == null && (additionalSwipeAreas == null || additionalSwipeAreas.Length == 0))
        {
            if (panelRect == null) return false;
            Camera cam = GetCanvasCamera(panelRect);
            return RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPos, cam);
        }

        // handleBar 체크
        if (handleBar != null)
        {
            Camera cam = GetCanvasCamera(handleBar);
            if (RectTransformUtility.RectangleContainsScreenPoint(handleBar, screenPos, cam))
                return true;
        }

        // 추가 영역 체크 (타이틀 텍스트 등)
        if (additionalSwipeAreas != null)
        {
            foreach (var area in additionalSwipeAreas)
            {
                if (area == null) continue;
                Camera cam = GetCanvasCamera(area);
                if (RectTransformUtility.RectangleContainsScreenPoint(area, screenPos, cam))
                    return true;
            }
        }

        return false;
    }

    private Camera GetCanvasCamera(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return canvas.worldCamera;
        return null;
    }

    // ============================================================
    // 패널 이동 / 닫기
    // ============================================================

    public void DragPanel(float totalDeltaY)
    {
        if (!isValidSwipe) return;

        if (totalDeltaY > 0)
        {
            // 위로 당길 때는 저항
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

        bool shouldClose = totalDeltaY < -dismissThreshold || velocity < -velocityThreshold;
        bool shouldExpand = enableSwipeUpExpand && (totalDeltaY > expandThreshold || velocity > velocityThreshold);

        if (shouldClose)
        {
            StartCoroutine(SlideOutAndClose());
        }
        else if (shouldExpand)
        {
            StartCoroutine(SnapBack());
            onExpand?.Invoke();
        }
        else
        {
            StartCoroutine(SnapBack());
        }
    }

    IEnumerator SlideOutAndClose()
    {
        Vector2 current = panelRect.anchoredPosition;
        Vector2 target = new Vector2(0, -Screen.height);
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t;
            panelRect.anchoredPosition = Vector2.Lerp(current, target, t);
            yield return null;
        }

        panelRect.anchoredPosition = target;
        onClose?.Invoke();
    }

    IEnumerator SnapBack()
    {
        Vector2 current = panelRect.anchoredPosition;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1 - (1 - t) * (1 - t);
            panelRect.anchoredPosition = Vector2.Lerp(current, originalAnchoredPos, t);
            yield return null;
        }

        panelRect.anchoredPosition = originalAnchoredPos;
    }

    public void ResetPosition()
    {
        if (panelRect != null)
            panelRect.anchoredPosition = originalAnchoredPos;
    }
}
