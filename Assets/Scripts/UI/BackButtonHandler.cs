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
            Debug.Log("Back button pressed.");

            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
                MoveToBackground();
            }
            else
            {
                Debug.Log("Loading previous scene: " + (SceneManager.GetActiveScene().buildIndex - 1));
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
            }
        }
    }

    // ���� ��׶���� �̵� (Ȩ ȭ�� ǥ��)
    private void MoveToBackground()
    {
        Debug.Log("Attempting to move to home screen.");
        #if UNITY_ANDROID
        try
        {
            // UnityPlayerActivity�� currentActivity ��������
            AndroidJavaObject activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");
            if (activity != null)
            {
                // Ȩ ȭ�� ����Ʈ ����
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