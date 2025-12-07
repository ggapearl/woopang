using UnityEngine;
using System.Collections;

/// <summary>
/// Android 시스템 UI 바 강제 표시 (깜빡임 없음)
/// Start에서 단 한 번만 실행
/// </summary>
public class AndroidSystemUIController : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject currentActivity;
    private AndroidJavaObject window;
    private AndroidJavaObject decorView;

    private const int SYSTEM_UI_FLAG_LAYOUT_STABLE = 256;
    private const int SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN = 1024;
    private const int SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION = 512;
#endif

    void Start()
    {
        StartCoroutine(SetupSystemUIAfterSplash());
    }

    private IEnumerator SetupSystemUIAfterSplash()
    {
        // 스플래시 완료 대기
        yield return new WaitForSeconds(0.5f);

#if UNITY_ANDROID && !UNITY_EDITOR
        // 깜빡임 방지를 위해 스크립트 통한 강제 설정 비활성화
        // Unity Player Settings 사용 권장
        Debug.Log("[AndroidSystemUI] 스크립트 통한 시스템 UI 강제 설정 비활성화됨.");
        
        // SetupSystemUI();
        
        // 주기적으로 상태 체크 (0.5초마다)
        // InvokeRepeating(nameof(CheckAndRestoreSystemUI), 0.5f, 0.5f);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void SetupSystemUI()
    {
        try
        {
            using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                currentActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
                window = currentActivity.Call<AndroidJavaObject>("getWindow");
                decorView = window.Call<AndroidJavaObject>("getDecorView");

                // 시스템 UI 플래그 설정 (상태바/네비게이션바 표시)
                int flags = SYSTEM_UI_FLAG_LAYOUT_STABLE |
                           SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
                           SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION;

                decorView.Call("setSystemUiVisibility", flags);

                // 윈도우 플래그 설정
                using (AndroidJavaClass wmClass = new AndroidJavaClass("android.view.WindowManager$LayoutParams"))
                {
                    int FLAG_FULLSCREEN = wmClass.GetStatic<int>("FLAG_FULLSCREEN");
                    int FLAG_FORCE_NOT_FULLSCREEN = wmClass.GetStatic<int>("FLAG_FORCE_NOT_FULLSCREEN");

                    window.Call("clearFlags", FLAG_FULLSCREEN);
                    window.Call("addFlags", FLAG_FORCE_NOT_FULLSCREEN);
                }

                Debug.Log("[AndroidSystemUI] 시스템 UI 설정 완료");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AndroidSystemUI] 설정 실패: {e.Message}");
        }
    }

    private void CheckAndRestoreSystemUI()
    {
        try
        {
            if (decorView != null)
            {
                // 현재 시스템 UI 플래그 확인
                int currentFlags = decorView.Call<int>("getSystemUiVisibility");

                // 필요한 플래그
                int requiredFlags = SYSTEM_UI_FLAG_LAYOUT_STABLE |
                                  SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
                                  SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION;

                // 플래그가 변경되었으면 재설정
                if (currentFlags != requiredFlags)
                {
                    decorView.Call("setSystemUiVisibility", requiredFlags);
                }
            }
        }
        catch (System.Exception e)
        {
            // 무시 (에러 로그 스팸 방지)
        }
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(CheckAndRestoreSystemUI));
    }
#endif
}
