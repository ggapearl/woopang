using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class BackButtonHandler : MonoBehaviour
{
    private Keyboard keyboard;

    void Start()
    {
        keyboard = Keyboard.current;
    }

    void Update()
    {
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
                MoveToBackground();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
            }
        }
    }

    // ���� ��׶���� �̵� (Ȩ ȭ�� ǥ��)
    private void MoveToBackground()
    {
        #if UNITY_ANDROID
        try
        {
            AndroidJavaObject activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");
            if (activity != null)
            {
                AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.MAIN");
                intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.HOME");
                intent.Call<AndroidJavaObject>("setFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
                activity.Call("startActivity", intent);
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
        #endif
    }
}