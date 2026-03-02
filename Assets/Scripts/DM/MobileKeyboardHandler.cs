using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 모바일 키보드가 올라올 때 Background 패널 전체를 확장하여
/// 채팅/댓글 영역이 키보드 위에서 자연스럽게 보이도록 처리.
///
/// 핵심 동작:
/// 1. 키보드 활성화 시 패널 확장 (Background, InputArea, Viewport)
/// 2. 대화창 터치/스크롤 시 키보드 유지 (LateUpdate에서 강제 재포커스)
/// 3. 네이티브 키보드 dismiss 버튼으로만 키보드 닫기 허용
/// 4. 패널 비활성화(OnDisable) 시 즉시 원상 복구
///
/// 상태 머신:
/// - keyboardLogicallyActive = true: 키보드가 열려있어야 하는 상태
///   → 패널 확장 유지, 포커스 손실 시 자동 재포커스
/// - keyboardLogicallyActive = false: 키보드 없는 상태
///   → 패널 원래 크기, 포커스 자유
/// </summary>
public class MobileKeyboardHandler : MonoBehaviour
{
    // ============================================================
    // Inspector Fields
    // ============================================================

    [Header("=== 대상 요소 ===")]
    [Tooltip("확장할 Background RectTransform (채팅/댓글 패널의 Background)")]
    public RectTransform backgroundRect;

    [Tooltip("메시지 스크롤뷰 (키보드 올라올 때 맨 아래로 스크롤)")]
    public ScrollRect chatScrollRect;

    [Tooltip("키보드 닫기 대상 InputField")]
    public InputField targetInputField;

    [Tooltip("InputArea RectTransform (참조용)")]
    public RectTransform inputAreaRect;

    [Tooltip("ScrollView RectTransform (참조용)")]
    public RectTransform scrollViewRect;

    [Header("=== 키보드 위치 설정 ===")]
    [Tooltip("키보드 위 추가 여백 (Canvas px)")]
    public float keyboardTopPadding = 0f;

    [Tooltip("패널 확장 애니메이션 속도")]
    public float lerpSpeed = 10f;

    [Header("=== 패널 확장 설정 ===")]
    [Tooltip("키보드 올라올 때 패널 확장 활성화")]
    public bool expandPanelOnKeyboard = true;

    [Tooltip("확장 시 상단 앵커 Y (0~1, 1=화면 최상단)")]
    public float expandedTopAnchorY = 1f;

    [Tooltip("확장 시 상단 오프셋 (음수=아래로, Inspector에서 조절)")]
    public float expandedTopOffset = 0f;

    [Tooltip("확장 시 좌우 오프셋 (0=화면 끝까지)")]
    public float expandedSideOffset = 0f;

    [Header("=== Viewport 패딩 설정 ===")]
    [Tooltip("ScrollView 내부 Viewport RectTransform (자동 연결)")]
    public RectTransform viewportRect;

    [Tooltip("키보드 확장 시 Viewport 상단 패딩")]
    public float expandedViewportTop = 160f;

    [Tooltip("키보드 확장 시 Viewport 하단 패딩")]
    public float expandedViewportBottom = 160f;

    [Header("=== InputArea 확장 설정 ===")]
    [Tooltip("키보드 올라올 때 InputArea 좌우 확장량 (양쪽 각각)")]
    public float inputAreaExpandX = 50f;

    [Header("=== 키보드 유지 설정 ===")]
    [Tooltip("키보드 높이 0 후 native dismiss 판단까지 대기 (초)")]
    public float dismissGracePeriod = 1.0f;

    [Tooltip("이 시간 이내에 Unity 터치가 있었으면 touch-caused로 판단 (초)")]
    public float touchRecencyThreshold = 0.3f;

    [Tooltip("키보드 재활성화 최대 시도 시간 (초) — 이 시간 초과 시 포기")]
    public float maxReactivateTime = 3.0f;

    // ============================================================
    // Original State (OnEnable 시점 저장)
    // ============================================================

    private Vector2 origBgAnchorMin, origBgAnchorMax;
    private Vector2 origBgOffsetMin, origBgOffsetMax;
    private Vector2 origViewportOffsetMin, origViewportOffsetMax;
    private Vector2 origInputAreaOffsetMin, origInputAreaOffsetMax;

    // ============================================================
    // Animation Targets
    // ============================================================

    private Vector2 targetAnchorMin, targetAnchorMax;
    private Vector2 targetOffsetMin, targetOffsetMax;
    private Vector2 targetViewportOffsetMin, targetViewportOffsetMax;
    private Vector2 targetInputAreaOffsetMin, targetInputAreaOffsetMax;

    // ============================================================
    // Core State
    // ============================================================

    private bool initialized;
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    // ============================================================
    // Keyboard State Machine
    // ============================================================

    /// <summary>true = 키보드가 열려있어야 하는 상태 (패널 확장 유지)</summary>
    private bool keyboardLogicallyActive;

    /// <summary>마지막으로 키보드 높이 > 0이었던 시간</summary>
    private float lastKbHeightTime;

    /// <summary>마지막으로 감지된 키보드 높이 (canvas px)</summary>
    private float lastKbHeight;

    /// <summary>키보드 높이가 0이 된 시점</summary>
    private float kbGoneStartTime;

    /// <summary>마지막으로 Unity 터치가 활성이었던 시간</summary>
    private float lastTouchActiveTime;

    /// <summary>LateUpdate 재포커스 쿨다운</summary>
    private float refocusCooldown;

    /// <summary>키보드 최초 활성화 시 한 번만 스크롤 맨 아래로</summary>
    private bool needsScrollToBottom;

    /// <summary>현재 터치가 진행 중인지 (스크롤 보호용)</summary>
    private bool isTouchActive;

    /// <summary>KeyboardPersistentInputField 캐스트 (OnDeselect 차단용)</summary>
    private KeyboardPersistentInputField persistentInput;

    // ============================================================
    // Android Keyboard Height — Baseline 방식
    // ============================================================

    /// <summary>키보드 없을 때의 visibleFrame.bottom (네비게이션바 offset 제거용)</summary>
    private int baselineVisibleBottom = -1;

    private float lastJniErrorTime;
    private int updateCallCount;

    // ============================================================
    // Lifecycle
    // ============================================================

    void OnEnable()
    {
#if UNITY_EDITOR
        // 에디터 프리뷰 상태 리셋 — 패널 열 때마다 깨끗한 상태로 시작
        editorPreviewActive = false;
        editorPreviewKeyboardHeight = 0;
#endif

        Debug.Log($"[WP-DBG] OnEnable() go={gameObject.name} initialized={initialized} kbLogActive={keyboardLogicallyActive} bgRect={(backgroundRect != null ? backgroundRect.gameObject.name : "null")}");
        hksmLogCount = 0;
        expandLogCount = 0;

        if (initialized && backgroundRect != null)
        {
            // 이미 초기화된 상태에서 re-enable — 현재 위치 기준으로 원본값 재캡처
            // (SlidePanel 완료 후 re-enable되므로 올바른 위치)
            origBgAnchorMin = backgroundRect.anchorMin;
            origBgAnchorMax = backgroundRect.anchorMax;
            origBgOffsetMin = backgroundRect.offsetMin;
            origBgOffsetMax = backgroundRect.offsetMax;
            SetCollapsedTargets();
            Debug.Log($"[WP-DBG] OnEnable RE-CAPTURE: origBgAnchorMin={origBgAnchorMin} origBgAnchorMax={origBgAnchorMax} origBgOffsetMin={origBgOffsetMin} origBgOffsetMax={origBgOffsetMax}");
        }
        else
        {
            TryInitialize();
        }

        if (initialized)
        {
            Debug.Log($"[WP-DBG] OnEnable post-init: origBgAnchorMin={origBgAnchorMin} origBgAnchorMax={origBgAnchorMax} origBgOffsetMin={origBgOffsetMin} origBgOffsetMax={origBgOffsetMax}");
            Debug.Log($"[WP-DBG] OnEnable targets: tgtAnchorMin={targetAnchorMin} tgtAnchorMax={targetAnchorMax} tgtOffsetMin={targetOffsetMin} tgtOffsetMax={targetOffsetMax}");
            Debug.Log($"[WP-DBG] OnEnable current bg: anchorMin={backgroundRect.anchorMin} anchorMax={backgroundRect.anchorMax} offsetMin={backgroundRect.offsetMin} offsetMax={backgroundRect.offsetMax} anchoredPos={backgroundRect.anchoredPosition}");
        }
    }

    void OnDisable()
    {
        Debug.Log($"[WP-DBG] OnDisable() go={gameObject.name} initialized={initialized} kbLogActive={keyboardLogicallyActive}");
        if (!initialized || backgroundRect == null) return;

        // 즉시 원상 복구
        if (persistentInput != null) persistentInput.lockFocus = false;
        keyboardLogicallyActive = false;
        kbGoneStartTime = 0;
        needsScrollToBottom = false;

        // ★ 애니메이션 타겟도 원상 복구 (다음 OnEnable 시 확장된 채 시작 방지)
        SetCollapsedTargets();

        backgroundRect.anchorMin = origBgAnchorMin;
        backgroundRect.anchorMax = origBgAnchorMax;
        backgroundRect.offsetMin = origBgOffsetMin;
        backgroundRect.offsetMax = origBgOffsetMax;

        if (viewportRect != null)
        {
            viewportRect.offsetMin = origViewportOffsetMin;
            viewportRect.offsetMax = origViewportOffsetMax;
        }

        if (inputAreaRect != null)
        {
            // X만 복원 — Y는 AutoExpandInputField가 제어
            inputAreaRect.offsetMin = new Vector2(origInputAreaOffsetMin.x, inputAreaRect.offsetMin.y);
            inputAreaRect.offsetMax = new Vector2(origInputAreaOffsetMax.x, inputAreaRect.offsetMax.y);
        }

    }

    // ============================================================
    // Deferred Initialization
    // ============================================================

    private void TryInitialize()
    {
        if (initialized) return;
        if (backgroundRect == null) return;

        origBgAnchorMin = backgroundRect.anchorMin;
        origBgAnchorMax = backgroundRect.anchorMax;
        origBgOffsetMin = backgroundRect.offsetMin;
        origBgOffsetMax = backgroundRect.offsetMax;

        targetAnchorMin = origBgAnchorMin;
        targetAnchorMax = origBgAnchorMax;
        targetOffsetMin = origBgOffsetMin;
        targetOffsetMax = origBgOffsetMax;

        parentCanvas = backgroundRect.GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            canvasRect = parentCanvas.GetComponent<RectTransform>();

        if (viewportRect != null)
        {
            origViewportOffsetMin = viewportRect.offsetMin;
            origViewportOffsetMax = viewportRect.offsetMax;
            targetViewportOffsetMin = origViewportOffsetMin;
            targetViewportOffsetMax = origViewportOffsetMax;
        }

        if (inputAreaRect != null)
        {
            origInputAreaOffsetMin = inputAreaRect.offsetMin;
            origInputAreaOffsetMax = inputAreaRect.offsetMax;
            targetInputAreaOffsetMin = origInputAreaOffsetMin;
            targetInputAreaOffsetMax = origInputAreaOffsetMax;
        }

        // ScrollRect 자동 탐색
        if (chatScrollRect == null)
        {
            chatScrollRect = GetComponentInChildren<ScrollRect>(true);
            if (chatScrollRect != null)
            {
                scrollViewRect = chatScrollRect.GetComponent<RectTransform>();
                if (viewportRect == null && chatScrollRect.viewport != null)
                    viewportRect = chatScrollRect.viewport;
            }
        }

        // KeyboardPersistentInputField 감지 (OnDeselect 차단 가능 여부)
        persistentInput = targetInputField as KeyboardPersistentInputField;

        initialized = true;

        Debug.Log($"[WP-DBG] TryInitialize() DONE go={gameObject.name} bgRect={backgroundRect.gameObject.name} " +
                  $"origAnchorMin={origBgAnchorMin} origAnchorMax={origBgAnchorMax} " +
                  $"origOffsetMin={origBgOffsetMin} origOffsetMax={origBgOffsetMax} " +
                  $"inputAreaRect={(inputAreaRect != null ? inputAreaRect.gameObject.name : "null")} " +
                  $"viewportRect={(viewportRect != null ? viewportRect.gameObject.name : "null")} " +
                  $"scrollRect={(chatScrollRect != null ? "found" : "null")} " +
                  $"canvasRect={(canvasRect != null ? canvasRect.rect.height.ToString("F0") : "null")}h");
    }

    // ============================================================
    // Update Loop
    // ============================================================

    void Update()
    {
        if (!initialized) { TryInitialize(); if (!initialized) return; }
        if (backgroundRect == null) return;

        try
        {
            TrackTouches();
            HandleKeyboardStateMachine();
            AnimatePanel();
        }
        catch (System.Exception e)
        {
            // [MKH] 태그로 출력 → 사용자 logcat 필터에서 보임
            if (updateCallCount <= 5)
                Debug.LogWarning($"[MKH] Update 오류: {e.GetType().Name}: {e.Message}");
        }

        if (refocusCooldown > 0)
            refocusCooldown -= Time.deltaTime;
    }

    /// <summary>
    /// LateUpdate — EventSystem이 InputField를 deselect한 후 강제 재선택.
    /// 이것이 핵심: EventSystem.Update() 이후에 실행되므로 deselect를 즉시 되돌림.
    ///
    /// ⚠️ 핵심: 포커스 손실 시 즉시 재포커스 (터치 중에도!)
    ///   → ScrollRect 드래그는 pointer 이벤트로 동작하므로 selection 변경에 영향 없음
    ///   → 즉시 재포커스해야 네이티브 키보드가 닫히지 않음
    /// </summary>
    void LateUpdate()
    {
        if (!keyboardLogicallyActive) return;
        if (targetInputField == null) return;
        if (refocusCooldown > 0) return;

        // 포커스 손실 시 즉시 재포커스 — 키보드 깜빡임 방지
        if (!targetInputField.isFocused)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(targetInputField.gameObject);
            targetInputField.ActivateInputField();
            refocusCooldown = 0.15f;

        }
    }

    // ============================================================
    // Touch Tracking
    // ============================================================

    private void TrackTouches()
    {
        bool touching = IsScreenBeingTouched();

        if (touching)
        {
            lastTouchActiveTime = Time.time;
            isTouchActive = true;
        }
        else
        {
            isTouchActive = false;
        }
    }

    /// <summary>
    /// 화면 터치 여부 감지 (New Input System + Legacy 호환)
    /// </summary>
    private bool IsScreenBeingTouched()
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.isPressed)
                        return true;
                }
            }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                return true;
        }
        catch (System.Exception) { }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.GetMouseButton(0) || Input.touchCount > 0)
                return true;
        }
        catch (System.Exception) { }
#endif

        return false;
    }

    // ============================================================
    // Keyboard State Machine — 핵심 로직
    //
    // keyboardLogicallyActive 가 true인 동안:
    //   - 패널은 확장 상태 유지 (절대 축소 안됨)
    //   - LateUpdate에서 포커스 손실 시 자동 재포커스
    //   - kbH가 0이 되어도 grace period 동안 대기
    //
    // keyboardLogicallyActive 가 false가 되는 조건:
    //   1. OnDisable (패널 닫힘)
    //   2. native dismiss (kbH=0 + 최근 Unity 터치 없음)
    //   3. timeout (maxReactivateTime 초과)
    // ============================================================

    private int hksmLogCount; // 디버깅용 로그 카운터

    private void HandleKeyboardStateMachine()
    {
        float kbH = GetKeyboardHeightInCanvasPixels();
        bool inputFocused = targetInputField != null && targetInputField.isFocused;

        // 첫 몇 프레임만 로그 출력 (스팸 방지)
        if (hksmLogCount < 5)
        {
            hksmLogCount++;
#if UNITY_EDITOR
            Debug.Log($"[WP-DBG] HKSM go={gameObject.name} kbH={kbH:F1} kbLogActive={keyboardLogicallyActive} inputFocused={inputFocused} " +
                      $"editorPreview={editorPreviewActive} editorKbH={editorPreviewKeyboardHeight:F1} " +
                      $"curBgAnchorMin={backgroundRect.anchorMin} curBgAnchorMax={backgroundRect.anchorMax}");
#else
            Debug.Log($"[WP-DBG] HKSM go={gameObject.name} kbH={kbH:F1} kbLogActive={keyboardLogicallyActive} inputFocused={inputFocused} " +
                      $"curBgAnchorMin={backgroundRect.anchorMin} curBgAnchorMax={backgroundRect.anchorMax}");
#endif
        }

        // ── 키보드 높이 > 0 감지됨 ──
        if (kbH > 0)
        {
            lastKbHeight = kbH;
            lastKbHeightTime = Time.time;
            kbGoneStartTime = 0;

            if (!keyboardLogicallyActive)
            {
                keyboardLogicallyActive = true;
                needsScrollToBottom = true;
                if (persistentInput != null) persistentInput.lockFocus = true;
                Debug.Log($"[WP-DBG] ★ ACTIVATED go={gameObject.name} kbH={kbH:F1}");
            }

            SetExpandedTargets(kbH);
            return;
        }

        // ── 키보드 높이 == 0 ──

        if (!keyboardLogicallyActive)
            return;

        // kbH가 처음 0이 된 시점 기록
        if (kbGoneStartTime <= 0)
            kbGoneStartTime = Time.time;

        float timeSinceGone = Time.time - kbGoneStartTime;
        float timeSinceTouch = Time.time - lastTouchActiveTime;

        // ── 터치 활성 중 → 절대 dismiss하지 않음 (스크롤 중일 수 있음) ──
        if (isTouchActive)
        {
            SetExpandedTargets(lastKbHeight);
            kbGoneStartTime = Time.time;
            return;
        }

        // ── 터치 후 grace period ──
        if (timeSinceTouch < dismissGracePeriod)
        {
            SetExpandedTargets(lastKbHeight);
            return;
        }

        // ── InputField이 focused 상태 → 키보드가 돌아올 수 있으므로 대기 ──
        if (inputFocused)
        {
            SetExpandedTargets(lastKbHeight);
            kbGoneStartTime = Time.time;
            return;
        }

        // ── 터치 안 됨 + focused 아님 + grace 만료 → native dismiss ──
        DismissKeyboardCleanly();
    }

    // ============================================================
    // Keyboard Dismiss — 깨끗한 정리
    // ============================================================

    private void DismissKeyboardCleanly()
    {
        Debug.Log($"[WP-DBG] ★ DISMISS go={gameObject.name}");
        if (persistentInput != null) persistentInput.lockFocus = false;
        keyboardLogicallyActive = false;
        kbGoneStartTime = 0;
        needsScrollToBottom = false;

        if (targetInputField != null && targetInputField.isFocused)
            targetInputField.DeactivateInputField();

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == targetInputField?.gameObject)
            EventSystem.current.SetSelectedGameObject(null);

        SetCollapsedTargets();
    }

    /// <summary>
    /// 키보드만 dismiss (패널은 유지) — 댓글 전송 후 호출용.
    /// 키보드 레이아웃만 collapsed로 되돌리되 패널 자체는 열린 상태 유지.
    /// </summary>
    /// <summary>
    /// 키보드만 닫고 패널 확장 상태는 유지.
    /// keepExpanded=true 이면 SetCollapsedTargets를 호출하지 않음.
    /// </summary>
    public void DismissKeyboardOnly(bool keepExpanded = false)
    {
        Debug.Log($"[WP-DBG] DismissKeyboardOnly go={gameObject.name} keepExpanded={keepExpanded}");
        if (persistentInput != null) persistentInput.lockFocus = false;
        keyboardLogicallyActive = false;
        kbGoneStartTime = 0;
        needsScrollToBottom = false;

#if UNITY_EDITOR
        editorPreviewActive = false;
        editorPreviewKeyboardHeight = 0;
#endif

        if (!keepExpanded)
        {
            SetCollapsedTargets();
        }
        else
        {
            // 패널은 확장 유지하되, 키보드 영역만 제거 — bottom anchor를 0으로
            if (expandPanelOnKeyboard)
            {
                targetAnchorMin = new Vector2(0, 0);
                targetAnchorMax = new Vector2(1, expandedTopAnchorY);
                targetOffsetMin = new Vector2(expandedSideOffset, 0);
                targetOffsetMax = new Vector2(-expandedSideOffset, expandedTopOffset);
            }
        }
    }

    // ============================================================
    // Expansion Targets
    // ============================================================

    private int expandLogCount; // 디버깅용

    private void SetExpandedTargets(float kbH)
    {
        if (!expandPanelOnKeyboard) return;

        float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;
        float bottomAnchor = (kbH + keyboardTopPadding) / canvasHeight;

        if (expandLogCount < 3)
        {
            expandLogCount++;
            Debug.Log($"[WP-DBG] SetExpandedTargets go={gameObject.name} kbH={kbH:F1} canvasH={canvasHeight:F0} bottomAnchor={bottomAnchor:F3}");
        }

        targetAnchorMin = new Vector2(0, bottomAnchor);
        targetAnchorMax = new Vector2(1, expandedTopAnchorY);
        targetOffsetMin = new Vector2(expandedSideOffset, 0);
        targetOffsetMax = new Vector2(-expandedSideOffset, expandedTopOffset);

        if (viewportRect != null)
        {
            targetViewportOffsetMin = new Vector2(origViewportOffsetMin.x, expandedViewportBottom);
            targetViewportOffsetMax = new Vector2(origViewportOffsetMax.x, -expandedViewportTop);
        }

        if (inputAreaRect != null && inputAreaExpandX > 0)
        {
            targetInputAreaOffsetMin = new Vector2(origInputAreaOffsetMin.x - inputAreaExpandX, origInputAreaOffsetMin.y);
            targetInputAreaOffsetMax = new Vector2(origInputAreaOffsetMax.x + inputAreaExpandX, origInputAreaOffsetMax.y);
        }
    }

    private void SetCollapsedTargets()
    {
        Debug.Log($"[WP-DBG] SetCollapsedTargets go={gameObject.name} → origAnchorMin={origBgAnchorMin} origAnchorMax={origBgAnchorMax}");
        targetAnchorMin = origBgAnchorMin;
        targetAnchorMax = origBgAnchorMax;
        targetOffsetMin = origBgOffsetMin;
        targetOffsetMax = origBgOffsetMax;

        if (viewportRect != null)
        {
            targetViewportOffsetMin = origViewportOffsetMin;
            targetViewportOffsetMax = origViewportOffsetMax;
        }

        if (inputAreaRect != null)
        {
            targetInputAreaOffsetMin = origInputAreaOffsetMin;
            targetInputAreaOffsetMax = origInputAreaOffsetMax;
        }
    }

    // ============================================================
    // Panel Animation
    // ============================================================

    private void AnimatePanel()
    {
        if (backgroundRect == null) return;

        float dt = Time.deltaTime * lerpSpeed;

        backgroundRect.anchorMin = Vector2.Lerp(backgroundRect.anchorMin, targetAnchorMin, dt);
        backgroundRect.anchorMax = Vector2.Lerp(backgroundRect.anchorMax, targetAnchorMax, dt);
        backgroundRect.offsetMin = Vector2.Lerp(backgroundRect.offsetMin, targetOffsetMin, dt);
        backgroundRect.offsetMax = Vector2.Lerp(backgroundRect.offsetMax, targetOffsetMax, dt);

        if (viewportRect != null)
        {
            viewportRect.offsetMin = Vector2.Lerp(viewportRect.offsetMin, targetViewportOffsetMin, dt);
            viewportRect.offsetMax = Vector2.Lerp(viewportRect.offsetMax, targetViewportOffsetMax, dt);
        }

        if (inputAreaRect != null)
        {
            // X만 lerp (inputAreaExpandX 확장/축소용)
            // Y는 AutoExpandInputField가 제어하므로 건드리지 않음
            float newMinX = Mathf.Lerp(inputAreaRect.offsetMin.x, targetInputAreaOffsetMin.x, dt);
            float newMaxX = Mathf.Lerp(inputAreaRect.offsetMax.x, targetInputAreaOffsetMax.x, dt);
            inputAreaRect.offsetMin = new Vector2(newMinX, inputAreaRect.offsetMin.y);
            inputAreaRect.offsetMax = new Vector2(newMaxX, inputAreaRect.offsetMax.y);
        }

        // 키보드 최초 활성화 시 한 번만 스크롤을 맨 아래로 이동
        // ⚠️ 매 프레임 강제하면 사용자가 스크롤할 수 없음!
        if (needsScrollToBottom && chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.normalizedPosition = Vector2.zero;
            needsScrollToBottom = false;
        }
    }

    // ============================================================
    // Keyboard Height Calculation
    // ============================================================

    private float GetKeyboardHeightInCanvasPixels()
    {
#if UNITY_EDITOR
        // 에디터 프리뷰: 이미 캔버스 픽셀 단위이므로 변환 불필요
        if (editorPreviewActive && editorPreviewKeyboardHeight > 0)
            return editorPreviewKeyboardHeight;
#endif

        float screenHeight = Screen.height;
        if (screenHeight <= 0) return 0;

        float keyboardScreenHeight = GetNativeKeyboardHeight();
        if (keyboardScreenHeight <= 0) return 0;

        if (canvasRect != null)
        {
            float canvasHeight = canvasRect.rect.height;
            float scaleFactor = canvasHeight / screenHeight;
            return keyboardScreenHeight * scaleFactor;
        }

        return keyboardScreenHeight;
    }

    private float GetNativeKeyboardHeight()
    {
#if UNITY_EDITOR
        // 에디터 프리뷰 활성 시 프리뷰 키보드 높이 반환
        // → HandleKeyboardStateMachine이 expanded targets를 설정하여
        //   AnimatePanel과 정상적으로 연동됨
        if (editorPreviewActive && editorPreviewKeyboardHeight > 0)
            return editorPreviewKeyboardHeight;
#endif
#if UNITY_IOS
        return GetKeyboardHeightIOS();
#elif UNITY_ANDROID
        return GetKeyboardHeightAndroid();
#else
        return 0;
#endif
    }

    // ============================================================
    // Editor Preview
    // ============================================================

#if UNITY_EDITOR
    // ★ NonSerialized: 씬 저장 시 true 상태로 남아 다른 패널에서 오동작 방지
    [System.NonSerialized] public bool editorPreviewActive;
    [System.NonSerialized] public float editorPreviewKeyboardHeight;
    [System.NonSerialized] public GameObject editorKeyboardPreviewObj;

    public void EditorPreviewKeyboard(float canvasKeyboardHeight)
    {
        if (backgroundRect == null) return;

        if (!initialized)
        {
            TryInitialize();
            if (!initialized) return;
        }

        editorPreviewActive = true;
        editorPreviewKeyboardHeight = canvasKeyboardHeight;

        // 상태 머신이 expanded targets를 설정하도록 유도
        // → AnimatePanel이 자연스럽게 lerp하여 확장
        // GetNativeKeyboardHeight()가 editorPreviewKeyboardHeight를 반환하므로
        // HandleKeyboardStateMachine → SetExpandedTargets 경로로 동작

        float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;
        CreateEditorKeyboardPreview(canvasKeyboardHeight, canvasHeight);

        needsScrollToBottom = true;
    }

    public void EditorResetPreview()
    {
        editorPreviewActive = false;
        editorPreviewKeyboardHeight = 0;

        // GetNativeKeyboardHeight()가 0을 반환하게 되므로
        // HandleKeyboardStateMachine → DismissKeyboardCleanly → SetCollapsedTargets
        // → AnimatePanel이 자연스럽게 lerp하여 축소

        DestroyEditorKeyboardPreview();
    }

    private void CreateEditorKeyboardPreview(float keyboardHeight, float canvasHeight)
    {
        DestroyEditorKeyboardPreview();
        if (canvasRect == null) return;

        editorKeyboardPreviewObj = new GameObject("_EditorKeyboardPreview");
        editorKeyboardPreviewObj.transform.SetParent(canvasRect.transform, false);
        editorKeyboardPreviewObj.hideFlags = HideFlags.DontSave;

        RectTransform rect = editorKeyboardPreviewObj.AddComponent<RectTransform>();
        float anchorHeight = keyboardHeight / canvasHeight;
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, anchorHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = editorKeyboardPreviewObj.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(editorKeyboardPreviewObj.transform, false);
        textObj.hideFlags = HideFlags.DontSave;
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        Text label = textObj.AddComponent<Text>();
        label.text = "Virtual Keyboard Preview";
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 36;
        label.color = new Color(1f, 1f, 1f, 0.4f);
        label.raycastTarget = false;

        Font font = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 36);
        label.font = font;

        rect.SetAsLastSibling();
    }

    private void DestroyEditorKeyboardPreview()
    {
        if (editorKeyboardPreviewObj != null)
        {
            DestroyImmediate(editorKeyboardPreviewObj);
            editorKeyboardPreviewObj = null;
        }
    }
#endif

    // ============================================================
    // Platform-Specific Keyboard Height
    // ============================================================

#if UNITY_IOS
    private float GetKeyboardHeightIOS()
    {
        Rect keyboardArea = TouchScreenKeyboard.area;
        if (keyboardArea.height > 0)
            return keyboardArea.height;

        if (targetInputField != null && targetInputField.touchScreenKeyboard != null)
        {
            Rect area = TouchScreenKeyboard.area;
            if (area.height > 0)
                return area.height;
        }

        return 0;
    }
#endif

#if UNITY_ANDROID
    /// <summary>
    /// Android 키보드 높이를 Baseline 방식으로 측정.
    /// getRealSize 대신 visibleFrame.bottom의 변화량을 사용하여
    /// 네비게이션 바 높이가 포함되지 않도록 함.
    /// → 입력필드가 키보드 바로 위에 딱 붙게 됨.
    /// </summary>
    private float GetKeyboardHeightAndroid()
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null) return 0;

                using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                {
                    if (window == null) return 0;

                    using (var decorView = window.Call<AndroidJavaObject>("getDecorView"))
                    {
                        if (decorView == null) return 0;

                        using (var rect = new AndroidJavaObject("android.graphics.Rect"))
                        {
                            decorView.Call("getWindowVisibleDisplayFrame", rect);
                            int visibleBottom = rect.Get<int>("bottom");

                            if (baselineVisibleBottom < 0)
                                baselineVisibleBottom = visibleBottom;

                            int keyboardHeight = baselineVisibleBottom - visibleBottom;

                            if (keyboardHeight > baselineVisibleBottom * 0.15f)
                                return keyboardHeight;

                            if (keyboardHeight <= 0)
                                baselineVisibleBottom = visibleBottom;
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            if (Time.time - lastJniErrorTime > 5f)
            {
                Debug.LogWarning($"[MKH] JNI 오류: {e.GetType().Name}: {e.Message}");
                lastJniErrorTime = Time.time;
            }
        }
        return 0;
    }
#endif
}
