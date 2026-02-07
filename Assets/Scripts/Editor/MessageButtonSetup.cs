using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;

[InitializeOnLoad]
public class MessageButtonSetup
{
    static MessageButtonSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += () => SetupMessageButton(false);
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += () => SetupMessageButton(false);
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += () => SetupMessageButton(false);
    }

    private static void SetupMessageButton(bool verbose)
    {
        MessagePanelManager manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null) return;

        Button messageButton = FindMessageButton();
        if (messageButton == null) return;

        if (IsOpenMessagePanelConnected(messageButton, manager)) return;

        int callCount = messageButton.onClick.GetPersistentEventCount();
        bool needsUpdate = false;

        for (int i = 0; i < callCount; i++)
        {
            if (messageButton.onClick.GetPersistentMethodName(i) == "SetActive")
            {
                needsUpdate = true;
                break;
            }
        }

        if (needsUpdate || callCount == 0)
        {
            for (int i = callCount - 1; i >= 0; i--)
            {
                UnityEventTools.RemovePersistentListener(messageButton.onClick, i);
            }

            UnityAction openPanelAction = manager.OpenMessagePanel;
            UnityEventTools.AddVoidPersistentListener(messageButton.onClick, openPanelAction);

            EditorUtility.SetDirty(messageButton);
            EditorSceneManager.MarkSceneDirty(messageButton.gameObject.scene);

            if (verbose)
                Debug.Log("[MessageButtonSetup] Message_Button OnClick 수정 완료");
        }
    }

    private static Button FindMessageButton()
    {
        GameObject btnObj = GameObject.Find("Message_Button");
        if (btnObj != null)
        {
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null) return btn;
        }

        string[] navNames = { "BottomNavigationBar", "BottomNav", "NavigationBar" };
        foreach (var navName in navNames)
        {
            GameObject bottomNav = GameObject.Find(navName);
            if (bottomNav != null)
            {
                Transform msgBtnTr = bottomNav.transform.Find("Message_Button");
                if (msgBtnTr != null)
                {
                    Button btn = msgBtnTr.GetComponent<Button>();
                    if (btn != null) return btn;
                }
            }
        }

        Button[] allButtons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            if (btn.gameObject.name == "Message_Button")
                return btn;
        }

        return null;
    }

    private static bool IsOpenMessagePanelConnected(Button button, MessagePanelManager manager)
    {
        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (button.onClick.GetPersistentTarget(i) == manager &&
                button.onClick.GetPersistentMethodName(i) == "OpenMessagePanel")
                return true;
        }
        return false;
    }

    [MenuItem("WOOPANG/Setup/Fix Message Button Connection", false, 200)]
    private static void ManualSetup()
    {
        SetupMessageButton(verbose: true);
    }
}
