using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모바일 키보드가 올라올 때 채팅 Background 패널을 키보드 위로 올려서
/// iMessage/카카오톡처럼 입력창이 키보드 바로 위에 위치하도록 처리
/// </summary>
public class MobileKeyboardHandler : MonoBehaviour
{
    [Header("=== 대상 패널 ===")]
    [Tooltip("키보드에 맞춰 이동할 Background RectTransform")]
    public RectTransform backgroundRect;

    [Tooltip("메시지 스크롤뷰 (키보드 올라올 때 맨 아래로 스크롤)")]
    public ScrollRect chatScrollRect;

    [Header("=== 설정 ===")]
    [Tooltip("키보드 위 추가 여백 (px)")]
    public float keyboardTopPadding = 10f;

    [Tooltip("위치 변경 애니메이션 속도")]
    public float lerpSpeed = 12f;

    // 원래 offsetMin 저장
    private Vector2 originalOffsetMin;
    private bool isKeyboardVisible;
    private float targetOffsetMinY;
    private bool initialized;
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    void OnEnable()
    {
        if (backgroundRect != null)
        {
            originalOffsetMin = backgroundRect.offsetMin;
            targetOffsetMinY = originalOffsetMin.y;
            initialized = true;

            // Canvas 참조 캐시
            parentCanvas = backgroundRect.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                canvasRect = parentCanvas.GetComponent<RectTransform>();
        }
    }

    void OnDisable()
    {
        // 비활성화 시 원래 위치 복원
        if (initialized && backgroundRect != null)
        {
            backgroundRect.offsetMin = originalOffsetMin;
            isKeyboardVisible = false;
        }
    }

    void Update()
    {
        if (!initialized || backgroundRect == null) return;

        // 에디터에서는 동작하지 않음
        if (Application.isEditor) return;

        bool keyboardNowVisible = TouchScreenKeyboard.visible;
        float keyboardHeight = GetKeyboardHeightInCanvasPixels();

        if (keyboardNowVisible && keyboardHeight > 0)
        {
            if (!isKeyboardVisible)
            {
                // 키보드가 새로 올라옴
                isKeyboardVisible = true;
            }

            targetOffsetMinY = keyboardHeight + keyboardTopPadding;
        }
        else
        {
            if (isKeyboardVisible)
            {
                // 키보드가 내려감
                isKeyboardVisible = false;
                targetOffsetMinY = originalOffsetMin.y;
            }
        }

        // 부드러운 이동
        float currentY = backgroundRect.offsetMin.y;
        if (Mathf.Abs(currentY - targetOffsetMinY) > 0.5f)
        {
            float newY = Mathf.Lerp(currentY, targetOffsetMinY, Time.deltaTime * lerpSpeed);
            backgroundRect.offsetMin = new Vector2(backgroundRect.offsetMin.x, newY);

            // 키보드 올라오는 중이면 스크롤을 아래로
            if (isKeyboardVisible && chatScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                chatScrollRect.normalizedPosition = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 스크린 좌표의 키보드 높이를 Canvas 좌표로 변환
    /// </summary>
    private float GetKeyboardHeightInCanvasPixels()
    {
        float screenHeight = Screen.height;
        if (screenHeight <= 0) return 0;

        // TouchScreenKeyboard.area는 스크린 픽셀 좌표
        float keyboardScreenHeight = GetNativeKeyboardHeight();
        if (keyboardScreenHeight <= 0) return 0;

        // Canvas 스케일 팩터 계산
        if (canvasRect != null)
        {
            float canvasHeight = canvasRect.rect.height;
            float scaleFactor = canvasHeight / screenHeight;
            return keyboardScreenHeight * scaleFactor;
        }

        return keyboardScreenHeight;
    }

    /// <summary>
    /// 플랫폼별 네이티브 키보드 높이 가져오기
    /// </summary>
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

#if UNITY_IOS
    private float GetKeyboardHeightIOS()
    {
        // TouchScreenKeyboard.area가 iOS에서 키보드 영역 반환
        Rect keyboardArea = TouchScreenKeyboard.area;
        if (keyboardArea.height > 0)
            return keyboardArea.height;

        return 0;
    }
#endif

#if UNITY_ANDROID
    private float GetKeyboardHeightAndroid()
    {
        // Android에서 키보드 높이 가져오기
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

            // 현재 표시 영역 계산
            using (var rect = new AndroidJavaObject("android.graphics.Rect"))
            {
                decorView.Call("getWindowVisibleDisplayFrame", rect);
                int visibleBottom = rect.Call<int>("bottom");

                // 실제 화면 높이
                var display = activity.Call<AndroidJavaObject>("getWindowManager")
                                      .Call<AndroidJavaObject>("getDefaultDisplay");
                using (var size = new AndroidJavaObject("android.graphics.Point"))
                {
                    display.Call("getRealSize", size);
                    int screenHeight = size.Get<int>("y");

                    int keyboardHeight = screenHeight - visibleBottom;

                    // 네비게이션 바 높이를 제외 (작은 값은 키보드가 아님)
                    if (keyboardHeight > screenHeight * 0.15f)
                        return keyboardHeight;
                }
            }
        }
        return 0;
    }
#endif
}
