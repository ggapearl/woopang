using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;

public class BackButtonHandler : MonoBehaviour
{
    public static BackButtonHandler Instance { get; private set; }

    // 활성화된 닫기 버튼들을 관리하는 리스트 (Stack처럼 사용)
    // 가장 마지막에 추가된(가장 최근에 켜진) 버튼이 우선순위를 가짐
    private List<Button> activeButtons = new List<Button>();

    private Keyboard keyboard;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // 중복 방지 (씬 전환 시 등)
            if (Instance != this) Destroy(this);
        }
    }

    void Start()
    {
        keyboard = Keyboard.current;
        Debug.Log("[BackButtonHandler] Initialized");
    }

    /// <summary>
    /// ClickButtonOnBack 스크립트에서 호출: 버튼 등록
    /// </summary>
    public void RegisterButton(Button btn)
    {
        if (!activeButtons.Contains(btn))
        {
            activeButtons.Add(btn);
            // Debug.Log($"[BackButtonHandler] Button registered: {btn.gameObject.name} (Total: {activeButtons.Count})");
        }
    }

    /// <summary>
    /// ClickButtonOnBack 스크립트에서 호출: 버튼 해제
    /// </summary>
    public void UnregisterButton(Button btn)
    {
        if (activeButtons.Contains(btn))
        {
            activeButtons.Remove(btn);
            // Debug.Log($"[BackButtonHandler] Button unregistered: {btn.gameObject.name} (Total: {activeButtons.Count})");
        }
    }

    void Update()
    {
        try
        {
            bool backPressed = false;

#if ENABLE_INPUT_SYSTEM
            var currentKeyboard = Keyboard.current;
            if (currentKeyboard != null && currentKeyboard.escapeKey.wasPressedThisFrame)
            {
                backPressed = true;
            }
#endif
            
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!backPressed && Input.GetKeyDown(KeyCode.Escape))
            {
                backPressed = true;
            }
#endif

            if (backPressed)
            {
                HandleBackPress();
            }
        }
        catch (System.Exception)
        {
            // Ignore errors during Update to prevent log flooding
        }
    }

    public void HandleBackPress()
    {
        if (activeButtons.Count > 0)
        {
            Button lastBtn = activeButtons[activeButtons.Count - 1];
            
            if (lastBtn != null && lastBtn.gameObject.activeInHierarchy && lastBtn.interactable)
            {
                lastBtn.onClick.Invoke();
                return;
            }
            else
            {
                activeButtons.RemoveAt(activeButtons.Count - 1);
                HandleBackPress(); 
                return;
            }
        }

        if (SceneManager.GetActiveScene().buildIndex == 0 || SceneManager.sceneCountInBuildSettings <= 1)
        {
            MoveToBackground();
        }
        else
        {
            try 
            {
                if (SceneManager.GetActiveScene().buildIndex > 0)
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
                }
                else
                {
                    MoveToBackground();
                }
            }
            catch
            {
                MoveToBackground();
            }
        }
    }

    private void MoveToBackground()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity != null)
                    {
                        activity.Call<bool>("moveTaskToBack", true);
                    }
                }
            }
        }
        catch (System.Exception)
        {
            // Silent fail
        }
        #endif
    }
}