using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SystemUIManager : MonoBehaviour
{
    [Header("시스템 UI 설정")]
    public bool forceShowSystemUI = true;
    
    [Header("디버그")]
    public bool showDebugInfo = false;  // 기본 비활성화
    
    [Header("OneUI 대응")]
    public bool enableOneUIWorkaround = true;
    public float fallbackBottomPadding = 100f; // OneUI 감지 실패 시 기본 하단 패딩
    
#if UNITY_ANDROID
    private AndroidJavaObject currentActivity;
    private AndroidJavaObject window;
    private AndroidJavaObject decorView;
#endif
#pragma warning disable CS0414 // Android 빌드에서만 사용 (#if UNITY_ANDROID)
    private bool isInitialized = false;
#pragma warning restore CS0414
    
    // Safe Area 캐싱
    private Rect lastSafeArea;
    private float lastScreenWidth, lastScreenHeight;
    private bool isOneUIDevice = false;
    private int cachedSdkInt = -1;
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
#else
        return false;
#endif
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
        // API 30+ (Android 11): WindowInsetsController 사용
        AndroidJavaClass buildClass = new AndroidJavaClass("android.os.Build$VERSION");
        int sdkInt = buildClass.GetStatic<int>("SDK_INT");

        if (sdkInt >= 30)
        {
            try
            {
                AndroidJavaObject insetsController = window.Call<AndroidJavaObject>("getInsetsController");
                if (insetsController != null)
                {
                    AndroidJavaClass insetsType = new AndroidJavaClass("android.view.WindowInsets$Type");
                    int statusBarsType = insetsType.CallStatic<int>("statusBars");
                    insetsController.Call("show", statusBarsType);

                    // [FIX] SystemUIManager는 상태바 표시 여부만 제어하고, 아이콘 색상(APPEARANCE)은 TopPanelColorChanger에 위임합니다.
                    // 여기서 setSystemBarsAppearance를 호출하면 기존 색상 설정을 덮어쓰게 되어 아이콘이 사라질 수 있습니다.
                }
            }
            catch (System.Exception e)
            {
                Log($"OneUI WindowInsetsController 설정 실패: {e.Message}");
            }
        }

        // OneUI용 더 안정적인 플래그 조합
        int flags = SYSTEM_UI_FLAG_LAYOUT_STABLE |
                   SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
                   SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION;

        // [FIX] 기존에 설정된 Light Status Bar 플래그(0x2000)가 있다면 유지합니다.
        try
        {
            int currentFlags = decorView.Call<int>("getSystemUiVisibility");
            if ((currentFlags & 0x2000) != 0) // SYSTEM_UI_FLAG_LIGHT_STATUS_BAR
            {
                flags |= 0x2000;
            }
        }
        catch { /* 무시 */ }

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

        // Status bar 투명 설정 (배경만 투명, 아이콘은 그대로)
        window.Call("addFlags", unchecked((int)0x80000000)); // FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS
        window.Call("setStatusBarColor", 0); // Color.TRANSPARENT

        // 네비게이션바: 검은색 배경 유지
        window.Call("setNavigationBarColor", unchecked((int)0xFF000000)); // Color.BLACK

        // API 29+: 상태바 대비 scrim 비활성화 (네비게이션바는 유지)
        AndroidJavaClass buildClassOneUI = new AndroidJavaClass("android.os.Build$VERSION");
        int sdkIntOneUI = buildClassOneUI.GetStatic<int>("SDK_INT");
        if (sdkIntOneUI >= 29)
        {
            try
            {
                window.Call("setStatusBarContrastEnforced", false);
            }
            catch (System.Exception) { /* API 29 미만 폴백 */ }
        }

        // 추가 대기 후 Canvas 조정
        StartCoroutine(DelayedCanvasAdjustment());
#endif
    }

    void SetupStandardSystemUI()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // API 30+ (Android 11): WindowInsetsController 사용
        AndroidJavaClass buildClass = new AndroidJavaClass("android.os.Build$VERSION");
        int sdkInt = buildClass.GetStatic<int>("SDK_INT");

        if (sdkInt >= 30)
        {
            // WindowInsetsController로 상태바 강제 표시
            try
            {
                AndroidJavaObject insetsController = window.Call<AndroidJavaObject>("getInsetsController");
                if (insetsController != null)
                {
                    // WindowInsets.Type.statusBars() = 상태바 표시
                    AndroidJavaClass insetsType = new AndroidJavaClass("android.view.WindowInsets$Type");
                    int statusBarsType = insetsType.CallStatic<int>("statusBars");
                    insetsController.Call("show", statusBarsType);

                    // [FIX] SystemUIManager는 상태바 표시 여부만 제어하고, 아이콘 색상은 건드리지 않습니다.
                }
            }
            catch (System.Exception e)
            {
                Log($"WindowInsetsController 설정 실패: {e.Message}");
            }
        }

        // 레거시 API (API 30 미만 + 추가 호환성)
        int flags = SYSTEM_UI_FLAG_LAYOUT_STABLE | SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN;

        // [FIX] 기존에 설정된 Light Status Bar 플래그(0x2000)가 있다면 유지합니다.
        try
        {
            int currentFlags = decorView.Call<int>("getSystemUiVisibility");
            if ((currentFlags & 0x2000) != 0) // SYSTEM_UI_FLAG_LIGHT_STATUS_BAR
            {
                flags |= 0x2000;
            }
        }
        catch { /* 무시 */ }

        decorView.Call("setSystemUiVisibility", flags);

        using (AndroidJavaClass wmClass = new AndroidJavaClass("android.view.WindowManager$LayoutParams"))
        {
            int FLAG_FULLSCREEN = wmClass.GetStatic<int>("FLAG_FULLSCREEN");
            int FLAG_FORCE_NOT_FULLSCREEN = wmClass.GetStatic<int>("FLAG_FORCE_NOT_FULLSCREEN");

            window.Call("clearFlags", FLAG_FULLSCREEN);
            window.Call("addFlags", FLAG_FORCE_NOT_FULLSCREEN);
        }

        // Status bar 투명 설정 (배경만 투명, 아이콘은 그대로)
        window.Call("addFlags", unchecked((int)0x80000000)); // FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS
        window.Call("setStatusBarColor", 0); // Color.TRANSPARENT

        // 네비게이션바: 검은색 배경 유지
        window.Call("setNavigationBarColor", unchecked((int)0xFF000000)); // Color.BLACK

        // API 29+: 상태바 대비 scrim 비활성화 (네비게이션바는 유지)
        if (sdkInt >= 29)
        {
            try
            {
                window.Call("setStatusBarContrastEnforced", false);
            }
            catch (System.Exception) { /* API 29 미만 폴백 */ }
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
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            // 루트 Canvas만 처리 (Nested Canvas는 부모 renderMode를 상속하므로 제외)
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
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

        // Android: 상단 status bar 영역 무시 (투명 오버레이이므로 콘텐츠가 뒤에 깔림)
        float topInset = Screen.height - (safeArea.y + safeArea.height);
        if (topInset > 0)
        {
            safeArea.height += topInset; // 상단까지 확장
        }

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

            // 시스템 UI가 숨겨졌는지 체크 (레거시 플래그)
            bool isFullscreen = (currentFlags & SYSTEM_UI_FLAG_FULLSCREEN) != 0;
            bool isNavigationHidden = (currentFlags & SYSTEM_UI_FLAG_HIDE_NAVIGATION) != 0;

            if (isFullscreen || isNavigationHidden)
            {
                RestoreSystemUI();
                Log("시스템 UI 복원됨 (레거시 플래그 감지)");
            }
            else
            {
                // API 30+: WindowInsetsController로 상태바 강제 표시
                EnsureStatusBarVisible();
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
    
    /// <summary>
    /// API 30+에서 WindowInsetsController.show(statusBars)를 호출하여 상태바 아이콘 복원
    /// 레거시 플래그로 감지되지 않는 상태바 숨김을 주기적으로 복원
    /// </summary>
    void EnsureStatusBarVisible()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (cachedSdkInt < 0)
            {
                AndroidJavaClass buildClass = new AndroidJavaClass("android.os.Build$VERSION");
                cachedSdkInt = buildClass.GetStatic<int>("SDK_INT");
            }

            if (cachedSdkInt >= 30 && window != null)
            {
                AndroidJavaObject insetsController = window.Call<AndroidJavaObject>("getInsetsController");
                if (insetsController != null)
                {
                    AndroidJavaClass insetsType = new AndroidJavaClass("android.view.WindowInsets$Type");
                    int statusBarsType = insetsType.CallStatic<int>("statusBars");
                    insetsController.Call("show", statusBarsType);
                }
            }
        }
        catch (System.Exception) { /* 무시 */ }
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
        // Unity가 백그라운드 복귀 시 상태바를 숨기는 동작과 싸우기 위해
        // 0.5초 간격으로 3번 반복해서 상태바 표시를 강제합니다.
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(0.5f);
            SetupSystemUI();
            EnsureStatusBarVisible();
        }
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
        var manager = Object.FindFirstObjectByType<SystemUIManager>();
        manager?.ForceRefresh();
    }

    public static Rect GetSafeArea()
    {
        var manager = Object.FindFirstObjectByType<SystemUIManager>();
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
        var manager = Object.FindFirstObjectByType<SystemUIManager>();
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