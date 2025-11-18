using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButtonHandler : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("Back button pressed.");

            // 현재 씬이 첫 화면이면 홈 화면으로 이동
            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
                MoveToBackground();
            }
            else
            {
                // 이전 씬으로 이동
                Debug.Log("Loading previous scene: " + (SceneManager.GetActiveScene().buildIndex - 1));
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
            }
        }
    }

    // 앱을 백그라운드로 이동 (홈 화면 표시)
    private void MoveToBackground()
    {
        Debug.Log("Attempting to move to home screen.");
        #if UNITY_ANDROID
        try
        {
            // UnityPlayerActivity의 currentActivity 가져오기
            AndroidJavaObject activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");
            if (activity != null)
            {
                // 홈 화면 인텐트 생성
                AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.MAIN");
                intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.HOME");
                intent.Call<AndroidJavaObject>("setFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
                activity.Call("startActivity", intent);
                Debug.Log("Home screen intent called successfully.");
            }
            else
            {
                Debug.LogError("Current activity is null.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to move to home screen: " + e.Message);
        }
        #else
        Debug.Log("Back button pressed, no action taken on non-Android platform.");
        #endif
    }
}