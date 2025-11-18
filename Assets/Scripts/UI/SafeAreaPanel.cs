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
    
    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo = false;
    
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
            if (showDebugInfo)
                Debug.LogWarning("[SafeArea 경고] 잘못된 SafeArea 감지됨, 대체 로직 사용");
            safeArea = GetFallbackSafeArea();
        }
        
        ApplySafeAreaToRectTransform(safeArea);
        
        // 상태 업데이트
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;
        
        if (showDebugInfo)
        {
            LogDebugInfo(safeArea);
        }
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
                
                if (showDebugInfo)
                    Debug.Log("[SafeArea 디버깅] Android 네이티브 API 호출 시작");
                
                // Display Cutout 정보 가져오기 (API 28+)
                AndroidJavaClass buildClass = new AndroidJavaClass("android.os.Build$VERSION");
                int sdkInt = buildClass.GetStatic<int>("SDK_INT");
                
                if (showDebugInfo)
                    Debug.Log($"[SafeArea 디버깅] Android API 레벨: {sdkInt}");
                
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
                            
                            if (showDebugInfo)
                                Debug.Log($"[SafeArea 디버깅] Display Cutout 감지 (노치/펀치홀): 상단={safeInsetTop}, 하단={safeInsetBottom}, 좌측={safeInsetLeft}, 우측={safeInsetRight}");
                            
                            // Display Cutout은 노치/펀치홀만 표시하므로 시스템바와 별도로 처리
                            // 여기서는 참고용으로만 사용
                        }
                    }
                }
                // 간단한 계산으로 변경
                AndroidJavaObject resources = activity.Call<AndroidJavaObject>("getResources");
                int statusBarHeight = GetSystemBarHeight(resources, "status_bar_height");
                int navigationBarHeight = GetSystemBarHeight(resources, "navigation_bar_height");
                
                if (showDebugInfo)
                    Debug.Log($"[SafeArea 디버깅] Android 시스템 바 높이: 상태바={statusBarHeight}px, 네비게이션바={navigationBarHeight}px");
                
                // Unity 동적 해상도에 맞춰 계산 (2200~2274px)
                safeArea = new Rect(
                    0,
                    navigationBarHeight,
                    Screen.width,
                    Screen.height - statusBarHeight - navigationBarHeight
                );
                
                if (showDebugInfo)
                    Debug.Log($"[SafeArea 디버깅] 동적 계산된 SafeArea: {safeArea} (Screen.height: {Screen.height})");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SafeArea 경고] Android SafeArea 감지 실패: {e.Message}");
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
            Debug.LogWarning($"[SafeArea 경고] {resourceName} 가져오기 실패: {e.Message}");
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
        
        if (showDebugInfo)
            Debug.Log($"[SafeArea 디버깅] Fallback SafeArea 사용: 상단={topOffset:F1}px, 하단={bottomOffset:F1}px");
        
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
    
    private void LogDebugInfo(Rect safeArea)
    {
        // 실제 Android API에서 가져온 값 사용 (계산하지 말고)
        float statusBarHeight = 0;
        float navigationBarHeight = 0;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject resources = activity.Call<AndroidJavaObject>("getResources");
                statusBarHeight = GetSystemBarHeight(resources, "status_bar_height");
                navigationBarHeight = GetSystemBarHeight(resources, "navigation_bar_height");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SafeArea 경고] 시스템 바 높이 가져오기 실패: {e.Message}");
        }
#endif
        
        Debug.Log($"[SafeArea 디버깅] 화면 정보:\n" +
                 $"전체 화면 크기: {Screen.width}x{Screen.height}\n" +
                 $"디바이스 해상도: {Screen.currentResolution.width}x{Screen.currentResolution.height}\n" +
                 $"화면 방향: {Screen.orientation}\n" +
                 $"DPI: {Screen.dpi}");
        
        Debug.Log($"[SafeArea 디버깅] 시스템 바 감지:\n" +
                 $"상단바 높이 (상태바): {statusBarHeight}px\n" +
                 $"하단바 높이 (네비게이션바): {navigationBarHeight}px\n" +
                 $"Unity 기본 SafeArea: {Screen.safeArea}");
                 
        Debug.Log($"[SafeArea 디버깅] Unity SafeArea vs 적용 SafeArea:\n" +
                 $"Unity SafeArea: {Screen.safeArea}\n" +
                 $"실제 적용된 SafeArea: {safeArea}\n" +
                 $"SafeArea 비율: {safeArea.width/Screen.width:F2} x {safeArea.height/Screen.height:F2}");
                 
        Debug.Log($"[SafeArea 디버깅] RectTransform 적용:\n" +
                 $"앵커 최소: {rectTransform.anchorMin}\n" +
                 $"앵커 최대: {rectTransform.anchorMax}\n" +
                 $"오프셋 최소: {rectTransform.offsetMin}\n" +
                 $"오프셋 최대: {rectTransform.offsetMax}\n" +
                 $"실제 UI 영역: {rectTransform.rect}");
                 
        Debug.Log($"[SafeArea 디버깅] 무시 설정:\n" +
                 $"좌측 무시: {ignoreLeft}, 우측 무시: {ignoreRight}\n" +
                 $"상단 무시: {ignoreTop}, 하단 무시: {ignoreBottom}");
    }
    
    private void AdjustCanvasToRealResolution()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (showDebugInfo)
                Debug.Log("[SafeArea 디버깅] Canvas 해상도 조정 시작");
                
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
                
                if (showDebugInfo)
                    Debug.Log($"[SafeArea 디버깅] 감지된 실제 해상도: {realWidth} x {realHeight}");
                
                // Canvas Scaler 찾아서 해상도 조정
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null)
                {
                    if (showDebugInfo)
                        Debug.LogWarning("[SafeArea 경고] Canvas를 찾을 수 없음");
                    return;
                }
                
                if (showDebugInfo)
                    Debug.Log($"[SafeArea 디버깅] Canvas 발견: {canvas.name}");
                
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                {
                    if (showDebugInfo)
                        Debug.LogWarning("[SafeArea 경고] CanvasScaler를 찾을 수 없음");
                    return;
                }
                
                Vector2 oldResolution = scaler.referenceResolution;
                scaler.referenceResolution = new Vector2(realWidth, realHeight);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SafeArea 디버깅] Canvas 해상도 조정 완료:\n" +
                             $"기존: {oldResolution}\n" +
                             $"변경: {realWidth} x {realHeight}\n" +
                             $"조정 전 Screen.height: {Screen.height}");
                    
                    // 1프레임 기다린 후 다시 확인
                    StartCoroutine(CheckScreenSizeAfterAdjustment());
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SafeArea 경고] Canvas 해상도 조정 실패: {e.Message}");
        }
#endif
    }
    
    private System.Collections.IEnumerator CheckScreenSizeAfterAdjustment()
    {
        yield return null; // 1프레임 기다림
        if (showDebugInfo)
            Debug.Log($"[SafeArea 디버깅] 조정 후 Screen.height: {Screen.height}");
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