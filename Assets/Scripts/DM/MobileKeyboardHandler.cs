using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모바일 키보드가 올라올 때 Background 패널 전체를 확장하여
/// 채팅/댓글 영역이 키보드 위에서 자연스럽게 보이도록 처리.
/// - Background 상하좌우 확장 (상단: expandedTopAnchor, 하단: 키보드 바로 위)
/// - InputArea는 Background 하단에 앵커되어 자동으로 따라감
/// - 아래로 스와이프하면 키보드 닫기
/// </summary>
public class MobileKeyboardHandler : MonoBehaviour
{
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

    [Header("=== 스와이프 다운 설정 ===")]
    [Tooltip("키보드 닫기 위한 최소 스와이프 거리 (px)")]
    public float dismissThreshold = 80f;

    [Tooltip("키보드 닫기 위한 최소 스와이프 속도 (px/s)")]
    public float velocityThreshold = 400f;

    // Background 원래 상태 저장
    private Vector2 originalBgAnchorMin;
    private Vector2 originalBgAnchorMax;
    private Vector2 originalBgOffsetMin;
    private Vector2 originalBgOffsetMax;

    // Viewport 원래 상태 저장
    private Vector2 originalViewportOffsetMin;
    private Vector2 originalViewportOffsetMax;
    private Vector2 targetViewportOffsetMin;
    private Vector2 targetViewportOffsetMax;

    // InputArea 원래 상태 저장
    private Vector2 originalInputAreaOffsetMin;
    private Vector2 originalInputAreaOffsetMax;
    private Vector2 targetInputAreaOffsetMin;
    private Vector2 targetInputAreaOffsetMax;

    // 확장 목표값
    private Vector2 targetAnchorMin;
    private Vector2 targetAnchorMax;
    private Vector2 targetOffsetMin;
    private Vector2 targetOffsetMax;

    private bool isKeyboardVisible;
    private bool initialized;
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    // 스와이프 다운 추적
    private bool isTrackingSwipe;
    private Vector2 swipeStartPos;
    private float swipeStartTime;

    void OnEnable()
    {
        if (backgroundRect != null)
        {
            // 원래 상태 저장
            originalBgAnchorMin = backgroundRect.anchorMin;
            originalBgAnchorMax = backgroundRect.anchorMax;
            originalBgOffsetMin = backgroundRect.offsetMin;
            originalBgOffsetMax = backgroundRect.offsetMax;

            // 목표값 초기화 (현재 상태)
            targetAnchorMin = originalBgAnchorMin;
            targetAnchorMax = originalBgAnchorMax;
            targetOffsetMin = originalBgOffsetMin;
            targetOffsetMax = originalBgOffsetMax;

            // Canvas 참조 캐시
            parentCanvas = backgroundRect.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                canvasRect = parentCanvas.GetComponent<RectTransform>();

            // Viewport 원래 패딩 저장
            if (viewportRect != null)
            {
                originalViewportOffsetMin = viewportRect.offsetMin;
                originalViewportOffsetMax = viewportRect.offsetMax;
                targetViewportOffsetMin = originalViewportOffsetMin;
                targetViewportOffsetMax = originalViewportOffsetMax;
            }

            // InputArea 원래 오프셋 저장
            if (inputAreaRect != null)
            {
                originalInputAreaOffsetMin = inputAreaRect.offsetMin;
                originalInputAreaOffsetMax = inputAreaRect.offsetMax;
                targetInputAreaOffsetMin = originalInputAreaOffsetMin;
                targetInputAreaOffsetMax = originalInputAreaOffsetMax;
            }

            initialized = true;
        }
    }

    void OnDisable()
    {
        if (!initialized || backgroundRect == null) return;

        // 비활성화 시 원래 상태 복원
        backgroundRect.anchorMin = originalBgAnchorMin;
        backgroundRect.anchorMax = originalBgAnchorMax;
        backgroundRect.offsetMin = originalBgOffsetMin;
        backgroundRect.offsetMax = originalBgOffsetMax;

        // Viewport 패딩 복원
        if (viewportRect != null)
        {
            viewportRect.offsetMin = originalViewportOffsetMin;
            viewportRect.offsetMax = originalViewportOffsetMax;
        }

        // InputArea 오프셋 복원
        if (inputAreaRect != null)
        {
            inputAreaRect.offsetMin = originalInputAreaOffsetMin;
            inputAreaRect.offsetMax = originalInputAreaOffsetMax;
        }

        isKeyboardVisible = false;
    }

    void Update()
    {
        if (!initialized || backgroundRect == null) return;
        if (Application.isEditor) return;

        HandleKeyboardPosition();
        HandleSwipeDown();
        AnimatePanel();
    }

    // ============================================================
    // 키보드 위치 처리 — Background 패널 전체 확장
    // ============================================================
    private void HandleKeyboardPosition()
    {
        bool keyboardNowVisible = TouchScreenKeyboard.visible;
        float keyboardHeight = GetKeyboardHeightInCanvasPixels();

        if (keyboardNowVisible && keyboardHeight > 0)
        {
            if (!isKeyboardVisible)
                isKeyboardVisible = true;

            if (expandPanelOnKeyboard)
            {
                float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;

                // 하단: 키보드 높이를 앵커 비율로 변환
                float bottomAnchor = (keyboardHeight + keyboardTopPadding) / canvasHeight;

                targetAnchorMin = new Vector2(0, bottomAnchor);
                targetAnchorMax = new Vector2(1, expandedTopAnchorY);
                targetOffsetMin = new Vector2(expandedSideOffset, 0);
                targetOffsetMax = new Vector2(-expandedSideOffset, expandedTopOffset);

                // Viewport 패딩 확장
                if (viewportRect != null)
                {
                    targetViewportOffsetMin = new Vector2(originalViewportOffsetMin.x, expandedViewportBottom);
                    targetViewportOffsetMax = new Vector2(originalViewportOffsetMax.x, -expandedViewportTop);
                }

                // InputArea 좌우 확장 (left -expandX, right +expandX)
                if (inputAreaRect != null && inputAreaExpandX > 0)
                {
                    targetInputAreaOffsetMin = new Vector2(originalInputAreaOffsetMin.x - inputAreaExpandX, originalInputAreaOffsetMin.y);
                    targetInputAreaOffsetMax = new Vector2(originalInputAreaOffsetMax.x + inputAreaExpandX, originalInputAreaOffsetMax.y);
                }
            }
        }
        else
        {
            if (isKeyboardVisible)
            {
                isKeyboardVisible = false;

                // 원래 상태로 복원
                targetAnchorMin = originalBgAnchorMin;
                targetAnchorMax = originalBgAnchorMax;
                targetOffsetMin = originalBgOffsetMin;
                targetOffsetMax = originalBgOffsetMax;

                // Viewport 패딩 복원
                if (viewportRect != null)
                {
                    targetViewportOffsetMin = originalViewportOffsetMin;
                    targetViewportOffsetMax = originalViewportOffsetMax;
                }

                // InputArea 좌우 복원
                if (inputAreaRect != null)
                {
                    targetInputAreaOffsetMin = originalInputAreaOffsetMin;
                    targetInputAreaOffsetMax = originalInputAreaOffsetMax;
                }
            }
        }
    }

    // ============================================================
    // 부드러운 패널 확장/축소 애니메이션
    // ============================================================
    private void AnimatePanel()
    {
        if (backgroundRect == null) return;

        float dt = Time.deltaTime * lerpSpeed;

        // 앵커 보간
        backgroundRect.anchorMin = Vector2.Lerp(backgroundRect.anchorMin, targetAnchorMin, dt);
        backgroundRect.anchorMax = Vector2.Lerp(backgroundRect.anchorMax, targetAnchorMax, dt);

        // 오프셋 보간
        backgroundRect.offsetMin = Vector2.Lerp(backgroundRect.offsetMin, targetOffsetMin, dt);
        backgroundRect.offsetMax = Vector2.Lerp(backgroundRect.offsetMax, targetOffsetMax, dt);

        // Viewport 패딩 보간
        if (viewportRect != null)
        {
            viewportRect.offsetMin = Vector2.Lerp(viewportRect.offsetMin, targetViewportOffsetMin, dt);
            viewportRect.offsetMax = Vector2.Lerp(viewportRect.offsetMax, targetViewportOffsetMax, dt);
        }

        // InputArea 좌우 확장 보간
        if (inputAreaRect != null)
        {
            inputAreaRect.offsetMin = Vector2.Lerp(inputAreaRect.offsetMin, targetInputAreaOffsetMin, dt);
            inputAreaRect.offsetMax = Vector2.Lerp(inputAreaRect.offsetMax, targetInputAreaOffsetMax, dt);
        }

        // 채팅 스크롤 최하단 유지
        if (isKeyboardVisible && chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.normalizedPosition = Vector2.zero;
        }
    }

    // ============================================================
    // 스와이프 다운으로 키보드 닫기
    // ============================================================
    private void HandleSwipeDown()
    {
        if (!isKeyboardVisible) return;

        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    isTrackingSwipe = true;
                    swipeStartPos = touch.position;
                    swipeStartTime = Time.time;
                    break;

                case TouchPhase.Moved:
                    if (!isTrackingSwipe) break;

                    float deltaY = swipeStartPos.y - touch.position.y;
                    float deltaX = Mathf.Abs(swipeStartPos.x - touch.position.x);

                    if (deltaY > 0 && deltaY > deltaX)
                    {
                        float elapsed = Time.time - swipeStartTime;
                        float velocity = elapsed > 0 ? deltaY / elapsed : 0;

                        if (deltaY >= dismissThreshold || velocity >= velocityThreshold)
                        {
                            DismissKeyboard();
                            isTrackingSwipe = false;
                        }
                    }
                    else if (deltaX > dismissThreshold * 0.5f)
                    {
                        isTrackingSwipe = false;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isTrackingSwipe = false;
                    break;
            }
        }
    }

    private void DismissKeyboard()
    {
        if (targetInputField != null)
            targetInputField.DeactivateInputField();

        if (TouchScreenKeyboard.visible)
        {
            if (targetInputField != null && targetInputField.touchScreenKeyboard != null)
                targetInputField.touchScreenKeyboard.active = false;
        }
    }

    // ============================================================
    // 키보드 높이 계산
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
    // 에디터 프리뷰 (가상 키보드 시뮬레이션)
    // ============================================================
#if UNITY_EDITOR
    [HideInInspector] public bool editorPreviewActive;
    [HideInInspector] public float editorPreviewKeyboardHeight;
    [HideInInspector] public GameObject editorKeyboardPreviewObj;

    /// <summary>
    /// 에디터에서 가상 키보드 프리뷰
    /// </summary>
    public void EditorPreviewKeyboard(float canvasKeyboardHeight)
    {
        if (backgroundRect == null) return;

        // 초기화
        if (!initialized)
        {
            originalBgAnchorMin = backgroundRect.anchorMin;
            originalBgAnchorMax = backgroundRect.anchorMax;
            originalBgOffsetMin = backgroundRect.offsetMin;
            originalBgOffsetMax = backgroundRect.offsetMax;
            parentCanvas = backgroundRect.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                canvasRect = parentCanvas.GetComponent<RectTransform>();
            initialized = true;
        }

        editorPreviewActive = true;
        editorPreviewKeyboardHeight = canvasKeyboardHeight;

        float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;
        float bottomAnchor = (canvasKeyboardHeight + keyboardTopPadding) / canvasHeight;

        // Background 패널 확장
        if (expandPanelOnKeyboard)
        {
            backgroundRect.anchorMin = new Vector2(0, bottomAnchor);
            backgroundRect.anchorMax = new Vector2(1, expandedTopAnchorY);
            backgroundRect.offsetMin = new Vector2(expandedSideOffset, 0);
            backgroundRect.offsetMax = new Vector2(-expandedSideOffset, expandedTopOffset);
        }

        // Viewport 패딩 확장
        if (viewportRect != null)
        {
            if (originalViewportOffsetMin == Vector2.zero && originalViewportOffsetMax == Vector2.zero)
            {
                originalViewportOffsetMin = viewportRect.offsetMin;
                originalViewportOffsetMax = viewportRect.offsetMax;
            }
            viewportRect.offsetMin = new Vector2(originalViewportOffsetMin.x, expandedViewportBottom);
            viewportRect.offsetMax = new Vector2(originalViewportOffsetMax.x, -expandedViewportTop);
        }

        // InputArea 좌우 확장
        if (inputAreaRect != null && inputAreaExpandX > 0)
        {
            if (originalInputAreaOffsetMin == Vector2.zero && originalInputAreaOffsetMax == Vector2.zero)
            {
                originalInputAreaOffsetMin = inputAreaRect.offsetMin;
                originalInputAreaOffsetMax = inputAreaRect.offsetMax;
            }
            inputAreaRect.offsetMin = new Vector2(originalInputAreaOffsetMin.x - inputAreaExpandX, originalInputAreaOffsetMin.y);
            inputAreaRect.offsetMax = new Vector2(originalInputAreaOffsetMax.x + inputAreaExpandX, originalInputAreaOffsetMax.y);
        }

        // 가상 키보드 시각 표시 생성
        CreateEditorKeyboardPreview(canvasKeyboardHeight, canvasHeight);

        // 스크롤 최하단
        if (chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.normalizedPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// 에디터 프리뷰 해제
    /// </summary>
    public void EditorResetPreview()
    {
        editorPreviewActive = false;
        editorPreviewKeyboardHeight = 0;

        if (backgroundRect != null)
        {
            backgroundRect.anchorMin = originalBgAnchorMin;
            backgroundRect.anchorMax = originalBgAnchorMax;
            backgroundRect.offsetMin = originalBgOffsetMin;
            backgroundRect.offsetMax = originalBgOffsetMax;
        }

        // Viewport 패딩 복원
        if (viewportRect != null)
        {
            viewportRect.offsetMin = originalViewportOffsetMin;
            viewportRect.offsetMax = originalViewportOffsetMax;
        }

        // InputArea 좌우 복원
        if (inputAreaRect != null)
        {
            inputAreaRect.offsetMin = originalInputAreaOffsetMin;
            inputAreaRect.offsetMax = originalInputAreaOffsetMax;
        }

        DestroyEditorKeyboardPreview();
    }

    private void CreateEditorKeyboardPreview(float keyboardHeight, float canvasHeight)
    {
        DestroyEditorKeyboardPreview();

        if (canvasRect == null) return;

        // Canvas 하위에 가상 키보드 패널 생성
        editorKeyboardPreviewObj = new GameObject("_EditorKeyboardPreview");
        editorKeyboardPreviewObj.transform.SetParent(canvasRect.transform, false);
        editorKeyboardPreviewObj.hideFlags = HideFlags.DontSave;

        RectTransform rect = editorKeyboardPreviewObj.AddComponent<RectTransform>();
        float anchorHeight = keyboardHeight / canvasHeight;
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, anchorHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 키보드 배경
        Image bg = editorKeyboardPreviewObj.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        bg.raycastTarget = false;

        // "Virtual Keyboard" 텍스트
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

        // 맨 앞에 표시
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

#if UNITY_IOS
    private float GetKeyboardHeightIOS()
    {
        Rect keyboardArea = TouchScreenKeyboard.area;
        if (keyboardArea.height > 0)
            return keyboardArea.height;
        return 0;
    }
#endif

#if UNITY_ANDROID
    private float GetKeyboardHeightAndroid()
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null) return 0;

            var window = activity.Call<AndroidJavaObject>("getWindow");
            if (window == null) return 0;

            var decorView = window.Call<AndroidJavaObject>("getDecorView");
            if (decorView == null) return 0;

            var rootView = decorView.Call<AndroidJavaObject>("getRootView");
            if (rootView == null) return 0;

            using (var rect = new AndroidJavaObject("android.graphics.Rect"))
            {
                decorView.Call("getWindowVisibleDisplayFrame", rect);
                int visibleBottom = rect.Call<int>("bottom");

                var display = activity.Call<AndroidJavaObject>("getWindowManager")
                                      .Call<AndroidJavaObject>("getDefaultDisplay");
                using (var size = new AndroidJavaObject("android.graphics.Point"))
                {
                    display.Call("getRealSize", size);
                    int screenHeight = size.Get<int>("y");

                    int keyboardHeight = screenHeight - visibleBottom;

                    if (keyboardHeight > screenHeight * 0.15f)
                        return keyboardHeight;
                }
            }
        }
        return 0;
    }
#endif
}
