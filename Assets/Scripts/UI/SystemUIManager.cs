using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SystemUIManager : MonoBehaviour
{
    [Header("시스템 UI 설정")]
    public bool forceShowSystemUI = true;
    
    [Header("디버그")]
    public bool showDebugInfo = true;
    
    [Header("OneUI 대응")]
    public bool enableOneUIWorkaround = true;
    public float fallbackBottomPadding = 100f; // OneUI 감지 실패 시 기본 하단 패딩
    
    private AndroidJavaObject currentActivity;
    private AndroidJavaObject window;
    private AndroidJavaObject decorView;
    private bool isInitialized = false;
    
    // Safe Area 캐싱
    private Rect lastSafeArea;
    private float lastScreenWidth, lastScreenHeight;
    private bool isOneUIDevice = false;
    private bool hasNavigationBar = false;
    private float navigationBarHeight = 0f;
    private float statusBarHeight = 0f;
    
    // 필요한 플래그들
    private const int SYSTEM_UI_FLAG_LAYOUT_STABLE = 256;
    private const int SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN = 1024;
    private const int SYSTEM_UI_FLAG_FULLSCREEN = 4;
    private const int SYSTEM_UI_FLAG_HIDE_NAVIGATION = 2;
    private const int SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION = 512;
    
    void Start()
    {
        StartCoroutine(Initialize());
    }
    
    IEnumerator Initialize()
    {
        yield return new WaitForSeconds(0.1f);
        
        DetectOneUIDevice();
        
        if (forceShowSystemUI)
        {
            SetupSystemUI();
        }
        
        // OneUI의 경우 더 자주 체크
        float checkInterval = isOneUIDevice ? 0.5f : 1f;
        InvokeRepeating("CheckSystemUIStatus", checkInterval, checkInterval);
        
        Log("시스템 UI 관리자 초기화 완료");
        if (isOneUIDevice) Log("OneUI 디바이스 감지됨");
    }
    
    void DetectOneUIDevice()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var systemProperties = new AndroidJavaClass("android.os.SystemProperties"))
            {
                string manufacturer = SystemInfo.deviceModel.ToLower();
                string brand = SystemInfo.deviceName.ToLower();
                
                // 삼성 디바이스 감지
                isOneUIDevice = manufacturer.Contains("samsung") || brand.Contains("samsung") || 
                               manufacturer.Contains("galaxy") || brand.Contains("galaxy");
                
                if (isOneUIDevice)
                {
                    GetSystemBarDimensions();
                }
            }
        }
        catch (System.Exception e)
        {
            Log($"OneUI 감지 실패: {e.Message}");
        }
#endif
    }
    
    void GetSystemBarDimensions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var resources = activity.Call<AndroidJavaObject>("getResources");
                
                // 상태바 높이 가져오기
                int statusBarId = resources.Call<int>("getIdentifier", "status_bar_height", "dimen", "android");
                if (statusBarId > 0)
                {
                    statusBarHeight = resources.Call<int>("getDimensionPixelSize", statusBarId);
                    Log($"상태바 높이: {statusBarHeight}px");
                }
                
                // 네비게이션 바 높이 가져오기
                int navBarId = resources.Call<int>("getIdentifier", "navigation_bar_height", "dimen", "android");
                if (navBarId > 0)
                {
                    navigationBarHeight = resources.Call<int>("getDimensionPixelSize", navBarId);
                    Log($"네비게이션 바 높이: {navigationBarHeight}px");
                }
                
                // 네비게이션 바 존재 여부 체크
                hasNavigationBar = CheckNavigationBarPresence();
                Log($"네비게이션 바 존재: {hasNavigationBar}");
            }
        }
        catch (System.Exception e)
        {
            Log($"시스템 바 크기 조회 실패: {e.Message}");
        }
#endif
    }
    
    bool CheckNavigationBarPresence()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var resources = activity.Call<AndroidJavaObject>("getResources");
                
                // 네비게이션 바 존재 여부 체크 (여러 방법 시도)
                int resourceId = resources.Call<int>("getIdentifier", "config_showNavigationBar", "bool", "android");
                if (resourceId > 0)
                {
                    return resources.Call<bool>("getBoolean", resourceId);
                }
                
                // ViewConfiguration을 통한 체크
                using (var viewConfiguration = new AndroidJavaClass("android.view.ViewConfiguration"))
                {
                    var vc = viewConfiguration.CallStatic<AndroidJavaObject>("get", activity);
                    return vc.Call<bool>("hasPermanentMenuKey") == false;
                }
            }
        }
        catch (System.Exception e)
        {
            Log($"네비게이션 바 체크 실패: {e.Message}");
            return true; // 안전하게 존재한다고 가정
        }
#endif
        return false;
    }
    
    void SetupSystemUI()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (currentActivity == null) return;
                
                window = currentActivity.Call<AndroidJavaObject>("getWindow");
                decorView = window.Call<AndroidJavaObject>("getDecorView");
                
                currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    // OneUI용 특별 설정
                    if (isOneUIDevice)
                    {
                        SetupOneUISystemUI();
                    }
                    else
                    {
                        SetupStandardSystemUI();
                    }
                }));
                
                isInitialized = true;
                Log("Android 시스템 UI 설정 완료");
            }
        }
        catch (System.Exception e)
        {
            Log($"시스템 UI 설정 실패: {e.Message}");
        }
#endif
    }
    
    void SetupOneUISystemUI()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // OneUI용 더 안정적인 플래그 조합
        int flags = SYSTEM_UI_FLAG_LAYOUT_STABLE | 
                   SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
                   SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION;
        
        decorView.Call("setSystemUiVisibility", flags);
        
        // 윈도우 플래그 설정
        using (AndroidJavaClass wmClass = new AndroidJavaClass("android.view.WindowManager$LayoutParams"))
        {
            int FLAG_FULLSCREEN = wmClass.GetStatic<int>("FLAG_FULLSCREEN");
            int FLAG_FORCE_NOT_FULLSCREEN = wmClass.GetStatic<int>("FLAG_FORCE_NOT_FULLSCREEN");
            int FLAG_LAYOUT_NO_LIMITS = wmClass.GetStatic<int>("FLAG_LAYOUT_NO_LIMITS");
            
            window.Call("clearFlags", FLAG_FULLSCREEN);
            window.Call("addFlags", FLAG_FORCE_NOT_FULLSCREEN);
            
            // OneUI의 경우 추가 플래그
            window.Call("clearFlags", FLAG_LAYOUT_NO_LIMITS);
        }
        
        // 추가 대기 후 Canvas 조정
        StartCoroutine(DelayedCanvasAdjustment());
#endif
    }
    
    void SetupStandardSystemUI()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        int flags = SYSTEM_UI_FLAG_LAYOUT_STABLE | SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN;
        decorView.Call("setSystemUiVisibility", flags);
        
        using (AndroidJavaClass wmClass = new AndroidJavaClass("android.view.WindowManager$LayoutParams"))
        {
            int FLAG_FULLSCREEN = wmClass.GetStatic<int>("FLAG_FULLSCREEN");
            int FLAG_FORCE_NOT_FULLSCREEN = wmClass.GetStatic<int>("FLAG_FORCE_NOT_FULLSCREEN");
            
            window.Call("clearFlags", FLAG_FULLSCREEN);
            window.Call("addFlags", FLAG_FORCE_NOT_FULLSCREEN);
        }
#endif
    }
    
    IEnumerator DelayedCanvasAdjustment()
    {
        yield return new WaitForSeconds(0.5f);
        AdjustCanvasForSystemUI();
    }
    
    void AdjustCanvasForSystemUI()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    ApplyEnhancedSafeArea(canvasRect);
                }
            }
        }
        
        Log($"Canvas 조정 완료 ({canvases.Length}개)");
    }
    
    void ApplyEnhancedSafeArea(RectTransform canvasRect)
    {
        Rect safeArea = GetEnhancedSafeArea();
        
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        
        // 정규화
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;
        
        canvasRect.anchorMin = anchorMin;
        canvasRect.anchorMax = anchorMax;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        
        Log($"Safe Area 적용: {safeArea}");
    }
    
    Rect GetEnhancedSafeArea()
    {
        Rect safeArea = Screen.safeArea;
        
        // OneUI에서 Safe Area가 제대로 감지되지 않는 경우 보정
        if (isOneUIDevice && enableOneUIWorkaround)
        {
            safeArea = CorrectSafeAreaForOneUI(safeArea);
        }
        
        return safeArea;
    }
    
    Rect CorrectSafeAreaForOneUI(Rect originalSafeArea)
    {
        Rect correctedSafeArea = originalSafeArea;
        
        // 하단 영역 보정 (네비게이션 바가 있는 경우)
        if (hasNavigationBar && navigationBarHeight > 0)
        {
            float expectedBottomMargin = navigationBarHeight;
            float currentBottomMargin = originalSafeArea.y;
            
            // Safe Area의 하단 마진이 너무 작은 경우 보정
            if (currentBottomMargin < expectedBottomMargin * 0.8f)
            {
                float adjustment = expectedBottomMargin - currentBottomMargin;
                correctedSafeArea.y = expectedBottomMargin;
                correctedSafeArea.height -= adjustment;
                
                Log($"OneUI 하단 영역 보정: {adjustment}px 추가");
            }
        }
        
        // 폴백: Safe Area가 전혀 감지되지 않는 경우
        if (originalSafeArea.width <= 0 || originalSafeArea.height <= 0)
        {
            correctedSafeArea = new Rect(
                0, 
                fallbackBottomPadding, 
                Screen.width, 
                Screen.height - fallbackBottomPadding - statusBarHeight
            );
            
            Log("OneUI 폴백 Safe Area 적용");
        }
        
        return correctedSafeArea;
    }
    
    void CheckSystemUIStatus()
    {
        // 화면 크기 변화 감지
        bool screenChanged = (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height);
        if (screenChanged)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            
            Log("화면 크기 변화 감지");
            
            if (isOneUIDevice)
            {
                // OneUI에서는 화면 크기 변화 시 시스템 바 정보 재조회
                StartCoroutine(DelayedSystemBarUpdate());
            }
            
            AdjustCanvasForSystemUI();
        }
        
        // Unity 풀스크린 모드 차단
        if (Screen.fullScreen)
        {
            Screen.fullScreen = false;
            Log("Unity 풀스크린 모드 차단됨");
        }
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized || decorView == null) return;
        
        try
        {
            int currentFlags = decorView.Call<int>("getSystemUiVisibility");
            
            // 시스템 UI가 숨겨졌는지 체크
            bool isFullscreen = (currentFlags & SYSTEM_UI_FLAG_FULLSCREEN) != 0;
            bool isNavigationHidden = (currentFlags & SYSTEM_UI_FLAG_HIDE_NAVIGATION) != 0;
            
            if (isFullscreen || isNavigationHidden)
            {
                RestoreSystemUI();
                Log("시스템 UI 복원됨");
            }
        }
        catch (System.Exception e)
        {
            Log($"상태 체크 실패: {e.Message}");
        }
#endif
    }
    
    IEnumerator DelayedSystemBarUpdate()
    {
        yield return new WaitForSeconds(0.5f);
        GetSystemBarDimensions();
    }
    
    void RestoreSystemUI()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (currentActivity != null)
        {
            currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                if (isOneUIDevice)
                {
                    SetupOneUISystemUI();
                }
                else
                {
                    SetupStandardSystemUI();
                }
            }));
        }
#endif
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && forceShowSystemUI)
        {
            StartCoroutine(DelayedSetup());
        }
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && forceShowSystemUI)
        {
            StartCoroutine(DelayedSetup());
        }
    }
    
    IEnumerator DelayedSetup()
    {
        yield return new WaitForSeconds(0.3f);
        SetupSystemUI();
    }
    
    void OnDestroy()
    {
        CancelInvoke();
    }
    
    void Log(string message)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[SystemUI] {message}");
        }
    }
    
    // 외부 호출용 메서드들
    public void ForceRefresh()
    {
        if (isOneUIDevice)
        {
            GetSystemBarDimensions();
        }
        SetupSystemUI();
    }
    
    public Rect GetCurrentSafeArea()
    {
        return GetEnhancedSafeArea();
    }
    
    public void PrintSystemInfo()
    {
        Log("=== 시스템 정보 ===");
        Log($"화면 크기: {Screen.width}x{Screen.height}");
        Log($"기본 Safe Area: {Screen.safeArea}");
        Log($"보정된 Safe Area: {GetEnhancedSafeArea()}");
        Log($"풀스크린: {Screen.fullScreen}");
        Log($"DPI: {Screen.dpi}");
        Log($"OneUI 디바이스: {isOneUIDevice}");
        Log($"네비게이션 바 존재: {hasNavigationBar}");
        Log($"네비게이션 바 높이: {navigationBarHeight}px");
        Log($"상태바 높이: {statusBarHeight}px");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (isInitialized && decorView != null)
        {
            try
            {
                int flags = decorView.Call<int>("getSystemUiVisibility");
                Log($"시스템 UI 플래그: {flags}");
            }
            catch
            {
                Log("플래그 정보 조회 실패");
            }
        }
#endif
    }
}

// 향상된 유틸리티 클래스
public static class SystemUIHelper
{
    public static void ForceShowSystemUI()
    {
        var manager = Object.FindObjectOfType<SystemUIManager>();
        manager?.ForceRefresh();
    }
    
    public static Rect GetSafeArea()
    {
        var manager = Object.FindObjectOfType<SystemUIManager>();
        return manager?.GetCurrentSafeArea() ?? Screen.safeArea;
    }
    
    public static bool IsSystemUIVisible()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var window = activity.Call<AndroidJavaObject>("getWindow");
                var decorView = window.Call<AndroidJavaObject>("getDecorView");
                
                int flags = decorView.Call<int>("getSystemUiVisibility");
                return (flags & 4) == 0 && (flags & 2) == 0;
            }
        }
        catch
        {
            return false;
        }
#else
        return true;
#endif
    }
    
    // OneUI 전용 헬퍼 메서드
    public static float GetNavigationBarHeight()
    {
        var manager = Object.FindObjectOfType<SystemUIManager>();
        if (manager == null) return 0f;
        
        // private 필드 접근을 위한 reflection 사용
        var field = typeof(SystemUIManager).GetField("navigationBarHeight", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            return (float)field.GetValue(manager);
        }
        
        return 0f;
    }
}