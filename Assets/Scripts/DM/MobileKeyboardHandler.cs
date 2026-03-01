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

    // ============================================================
    // Debug Logging
    // ============================================================

    private float lastLogTime;
    private float lastJniErrorTime;
    private const float LOG_INTERVAL = 1f;
    private bool firstUpdateLogged;
    private int updateCallCount;

    // ============================================================
    // Lifecycle
    // ============================================================

    void OnEnable()
    {
        TryInitialize();
        if (initialized)
        {
            Debug.Log($"[MKH] OnEnable: bgRect={backgroundRect != null} scrollRect={chatScrollRect != null} " +
                      $"inputArea={inputAreaRect != null} targetInput={targetInputField != null} go={gameObject.name}");

            // InputField 상태 진단 — 포커스 불가 원인 파악용
            if (targetInputField != null)
            {
                Debug.Log($"[MKH] InputField DIAG: type={targetInputField.GetType().Name} " +
                          $"interactable={targetInputField.interactable} " +
                          $"activeEnabled={targetInputField.isActiveAndEnabled} " +
                          $"textComp={targetInputField.textComponent != null} " +
                          $"graphic={((Selectable)targetInputField).targetGraphic != null} " +
                          $"goActive={targetInputField.gameObject.activeInHierarchy}");
            }
        }
    }

    void OnDisable()
    {
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
            inputAreaRect.offsetMin = origInputAreaOffsetMin;
            inputAreaRect.offsetMax = origInputAreaOffsetMax;
        }

        Debug.Log("[MKH] OnDisable: restored original state");
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
                Debug.Log($"[MKH] ScrollRect auto-discovered: {chatScrollRect.gameObject.name}");
            }
        }

        // KeyboardPersistentInputField 감지 (OnDeselect 차단 가능 여부)
        persistentInput = targetInputField as KeyboardPersistentInputField;

        initialized = true;
        Debug.Log($"[MKH] Initialized OK: canvas={canvasRect != null} viewport={viewportRect != null} scrollRect={chatScrollRect != null} persistent={persistentInput != null}");
    }

    // ============================================================
    // Update Loop
    // ============================================================

    void Update()
    {
        if (!firstUpdateLogged)
        {
            firstUpdateLogged = true;
            Debug.Log($"[MKH] Update() FIRST CALL. isEditor={Application.isEditor} init={initialized} bgRect={backgroundRect != null}");
        }

        if (!initialized) { TryInitialize(); if (!initialized) return; }
        if (backgroundRect == null) return;

        updateCallCount++;

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
                Debug.Log($"[MKH] *** UPDATE ERROR *** {e.GetType().Name}: {e.Message}");
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

            Debug.Log($"[MKH] LateUpdate REFOCUS (touching={isTouchActive})");
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

    private int hksmCount;

    private void HandleKeyboardStateMachine()
    {
        hksmCount++;

        float kbH = GetKeyboardHeightInCanvasPixels();
        bool inputFocused = targetInputField != null && targetInputField.isFocused;

        // 처음 3번 + 주기적 상태 로그
        bool shouldLog = hksmCount <= 3 || (Time.time - lastLogTime > LOG_INTERVAL);
        if (shouldLog)
        {
            Debug.Log($"[MKH] state#{hksmCount}: focused={inputFocused} kbH={kbH:F1} active={keyboardLogicallyActive} " +
                      $"touching={isTouchActive} " +
                      $"touchAge={Time.time - lastTouchActiveTime:F2}s kbGone={(kbGoneStartTime > 0 ? Time.time - kbGoneStartTime : 0):F1}s");
            lastLogTime = Time.time;
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
                Debug.Log($"[MKH] === ACTIVATED === kbH={kbH:F1} focused={inputFocused} lockFocus={persistentInput != null}");
            }

            SetExpandedTargets(kbH);
            return;
        }

        // ── 키보드 높이 == 0 ──

        if (!keyboardLogicallyActive)
            return;

        // kbH가 처음 0이 된 시점 기록
        if (kbGoneStartTime <= 0)
        {
            kbGoneStartTime = Time.time;
            Debug.Log($"[MKH] kbH→0. focused={inputFocused} touchAge={Time.time - lastTouchActiveTime:F2}s");
        }

        float timeSinceGone = Time.time - kbGoneStartTime;
        float timeSinceTouch = Time.time - lastTouchActiveTime;

        // ── 터치 활성 중 → 절대 dismiss하지 않음 (스크롤 중일 수 있음) ──
        if (isTouchActive)
        {
            SetExpandedTargets(lastKbHeight);
            kbGoneStartTime = Time.time; // gone 타이머 리셋 (터치 중에는 카운트 안 함)
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
            kbGoneStartTime = Time.time; // 타이머 리셋
            return;
        }

        // ── 터치 안 됨 + focused 아님 + grace 만료 → native dismiss ──

        Debug.Log($"[MKH] === DISMISSED (native) === gone={timeSinceGone:F2}s touchAge={timeSinceTouch:F2}s focused={inputFocused}");
        DismissKeyboardCleanly();
    }

    // ============================================================
    // Keyboard Dismiss — 깨끗한 정리
    // ============================================================

    private void DismissKeyboardCleanly()
    {
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

    // ============================================================
    // Expansion Targets
    // ============================================================

    private void SetExpandedTargets(float kbH)
    {
        if (!expandPanelOnKeyboard) return;

        float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;
        float bottomAnchor = (kbH + keyboardTopPadding) / canvasHeight;

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
            inputAreaRect.offsetMin = Vector2.Lerp(inputAreaRect.offsetMin, targetInputAreaOffsetMin, dt);
            inputAreaRect.offsetMax = Vector2.Lerp(inputAreaRect.offsetMax, targetInputAreaOffsetMax, dt);
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
    [HideInInspector] public bool editorPreviewActive;
    [HideInInspector] public float editorPreviewKeyboardHeight;
    [HideInInspector] public GameObject editorKeyboardPreviewObj;

    public void EditorPreviewKeyboard(float canvasKeyboardHeight)
    {
        if (backgroundRect == null) return;

        if (!initialized)
        {
            origBgAnchorMin = backgroundRect.anchorMin;
            origBgAnchorMax = backgroundRect.anchorMax;
            origBgOffsetMin = backgroundRect.offsetMin;
            origBgOffsetMax = backgroundRect.offsetMax;
            parentCanvas = backgroundRect.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                canvasRect = parentCanvas.GetComponent<RectTransform>();
            initialized = true;
        }

        editorPreviewActive = true;
        editorPreviewKeyboardHeight = canvasKeyboardHeight;

        float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;
        float bottomAnchor = (canvasKeyboardHeight + keyboardTopPadding) / canvasHeight;

        if (expandPanelOnKeyboard)
        {
            backgroundRect.anchorMin = new Vector2(0, bottomAnchor);
            backgroundRect.anchorMax = new Vector2(1, expandedTopAnchorY);
            backgroundRect.offsetMin = new Vector2(expandedSideOffset, 0);
            backgroundRect.offsetMax = new Vector2(-expandedSideOffset, expandedTopOffset);
        }

        if (viewportRect != null)
        {
            if (origViewportOffsetMin == Vector2.zero && origViewportOffsetMax == Vector2.zero)
            {
                origViewportOffsetMin = viewportRect.offsetMin;
                origViewportOffsetMax = viewportRect.offsetMax;
            }
            viewportRect.offsetMin = new Vector2(origViewportOffsetMin.x, expandedViewportBottom);
            viewportRect.offsetMax = new Vector2(origViewportOffsetMax.x, -expandedViewportTop);
        }

        if (inputAreaRect != null && inputAreaExpandX > 0)
        {
            if (origInputAreaOffsetMin == Vector2.zero && origInputAreaOffsetMax == Vector2.zero)
            {
                origInputAreaOffsetMin = inputAreaRect.offsetMin;
                origInputAreaOffsetMax = inputAreaRect.offsetMax;
            }
            inputAreaRect.offsetMin = new Vector2(origInputAreaOffsetMin.x - inputAreaExpandX, origInputAreaOffsetMin.y);
            inputAreaRect.offsetMax = new Vector2(origInputAreaOffsetMax.x + inputAreaExpandX, origInputAreaOffsetMax.y);
        }

        CreateEditorKeyboardPreview(canvasKeyboardHeight, canvasHeight);

        if (chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.normalizedPosition = Vector2.zero;
        }
    }

    public void EditorResetPreview()
    {
        editorPreviewActive = false;
        editorPreviewKeyboardHeight = 0;

        if (backgroundRect != null)
        {
            backgroundRect.anchorMin = origBgAnchorMin;
            backgroundRect.anchorMax = origBgAnchorMax;
            backgroundRect.offsetMin = origBgOffsetMin;
            backgroundRect.offsetMax = origBgOffsetMax;
        }

        if (viewportRect != null)
        {
            viewportRect.offsetMin = origViewportOffsetMin;
            viewportRect.offsetMax = origViewportOffsetMax;
        }

        if (inputAreaRect != null)
        {
            inputAreaRect.offsetMin = origInputAreaOffsetMin;
            inputAreaRect.offsetMax = origInputAreaOffsetMax;
        }

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
            Rect area = targetInputField.touchScreenKeyboard.area;
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
                Debug.Log($"[MKH] JNI ERROR: {e.GetType().Name}: {e.Message}");
                lastJniErrorTime = Time.time;
            }
        }
        return 0;
    }
#endif
}
