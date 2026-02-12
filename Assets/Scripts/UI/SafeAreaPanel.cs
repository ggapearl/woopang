using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaPanel : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);
    private Vector2Int lastScreenSize = new Vector2Int(0, 0);
    private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;
    
    [Header("Canvas 동적 조정")]
    [SerializeField] private bool adjustCanvasResolution = true;
    
    [Header("Safe Area Settings")]
    [SerializeField] private bool ignoreLeft = false;
    [SerializeField] private bool ignoreRight = false;
    [SerializeField] private bool ignoreTop = false;
    [SerializeField] private bool ignoreBottom = false;
    
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    
    void Start()
    {
        if (adjustCanvasResolution)
        {
            AdjustCanvasToRealResolution();
        }
        ApplySafeArea();
    }
    
    void Update()
    {
        // 화면 크기나 방향이 변경되었는지 확인
        if (HasScreenChanged())
        {
            ApplySafeArea();
        }
    }
    
    private bool HasScreenChanged()
    {
        return Screen.safeArea != lastSafeArea ||
               Screen.width != lastScreenSize.x ||
               Screen.height != lastScreenSize.y ||
               Screen.orientation != lastOrientation;
    }
    
    private void ApplySafeArea()
    {
        Rect safeArea = GetSafeArea();
        
        if (safeArea.width == 0 || safeArea.height == 0)
        {
            safeArea = GetFallbackSafeArea();
        }
        
        ApplySafeAreaToRectTransform(safeArea);
        
        // 상태 업데이트
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;
        
    }
    
    private Rect GetSafeArea()
    {
        Rect safeArea = Screen.safeArea;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android에서 네이티브 API를 사용하여 더 정확한 Safe Area 계산
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow");
                AndroidJavaObject decorView = window.Call<AndroidJavaObject>("getDecorView");
                
                // Display Cutout 정보 가져오기 (API 28+)
                AndroidJavaClass buildClass = new AndroidJavaClass("android.os.Build$VERSION");
                int sdkInt = buildClass.GetStatic<int>("SDK_INT");
                
                if (sdkInt >= 28)
                {
                    AndroidJavaObject windowInsets = decorView.Call<AndroidJavaObject>("getRootWindowInsets");
                    if (windowInsets != null)
                    {
                        AndroidJavaObject displayCutout = windowInsets.Call<AndroidJavaObject>("getDisplayCutout");
                        if (displayCutout != null)
                        {
                            int safeInsetTop = displayCutout.Call<int>("getSafeInsetTop");
                            int safeInsetBottom = displayCutout.Call<int>("getSafeInsetBottom");
                            int safeInsetLeft = displayCutout.Call<int>("getSafeInsetLeft");
                            int safeInsetRight = displayCutout.Call<int>("getSafeInsetRight");
                            
                            // Display Cutout은 노치/펀치홀만 표시하므로 시스템바와 별도로 처리
                            // 여기서는 참고용으로만 사용
                        }
                    }
                }
                // 간단한 계산으로 변경
                AndroidJavaObject resources = activity.Call<AndroidJavaObject>("getResources");
                int navigationBarHeight = GetSystemBarHeight(resources, "navigation_bar_height");

                // 상단 status bar는 투명 오버레이로 처리 (iOS와 동일)
                // 하단 navigation bar만 safe area에서 제외
                safeArea = new Rect(
                    0,
                    navigationBarHeight,
                    Screen.width,
                    Screen.height - navigationBarHeight
                );
                
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SafeArea] Android SafeArea 감지 실패: {e.Message}");
        }
#endif

// iOS 코드 제거 - Android 전용
        
        return safeArea;
    }
    
#if UNITY_ANDROID && !UNITY_EDITOR
    private int GetSystemBarHeight(AndroidJavaObject resources, string resourceName)
    {
        try
        {
            int resourceId = resources.Call<int>("getIdentifier", resourceName, "dimen", "android");
            if (resourceId > 0)
            {
                return resources.Call<int>("getDimensionPixelSize", resourceId);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SafeArea] {resourceName} 가져오기 실패: {e.Message}");
        }
        return 0;
    }
#endif

// iOS 관련 메서드 제거
    
    private Rect GetFallbackSafeArea()
    {
        // Android 전용 기본 시스템 바 높이 (Unity 좌표 기준)
        float topOffset = Screen.height * 0.025f; // 상태바
        float bottomOffset = Screen.height * 0.05f; // 네비게이션바
        
        return new Rect(0, bottomOffset, Screen.width, Screen.height - topOffset - bottomOffset);
    }
    
    private void ApplySafeAreaToRectTransform(Rect safeArea)
    {
        // Screen 좌표를 Canvas 좌표로 변환
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;
        
        // 무시할 영역 처리
        if (ignoreLeft) anchorMin.x = 0f;
        if (ignoreRight) anchorMax.x = 1f;
        if (ignoreTop) anchorMax.y = 1f;
        if (ignoreBottom) anchorMin.y = 0f;
        
        // Y축만 조정 (요청사항에 따라)
        anchorMin.x = 0f;
        anchorMax.x = 1f;
        
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
    
    
    private void AdjustCanvasToRealResolution()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject display = activity.Call<AndroidJavaObject>("getWindowManager")
                    .Call<AndroidJavaObject>("getDefaultDisplay");

                // 실제 물리적 해상도 가져오기
                AndroidJavaObject realSize = new AndroidJavaObject("android.graphics.Point");
                display.Call("getRealSize", realSize);
                int realWidth = realSize.Get<int>("x");
                int realHeight = realSize.Get<int>("y");

                // Canvas Scaler 찾아서 해상도 조정
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null)
                    return;

                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                    return;

                scaler.referenceResolution = new Vector2(realWidth, realHeight);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SafeArea] Canvas 해상도 조정 실패: {e.Message}");
        }
#endif
    }
    [ContextMenu("Apply Safe Area")]
    public void RefreshSafeArea()
    {
        ApplySafeArea();
    }
    
    // 외부에서 수동으로 Safe Area 갱신을 요청할 때 사용
    public Rect GetCurrentSafeArea()
    {
        return GetSafeArea();
    }
    
    // Safe Area 적용 여부 설정
    public void SetIgnoreFlags(bool left, bool right, bool top, bool bottom)
    {
        ignoreLeft = left;
        ignoreRight = right;
        ignoreTop = top;
        ignoreBottom = bottom;
        ApplySafeArea();
    }
}