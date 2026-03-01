using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// CommentManager의 UI 필드를 자동 연결
/// CommentPanel_HQ 프리팹 인스턴스를 찾아서 commentPanel, panelRect,
/// commentInputField, sendButton, commentContent, panelCanvasGroup 등을 연결
/// </summary>
[InitializeOnLoad]
public class CommentManagerSetup
{
    static CommentManagerSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndSetup;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckAndSetupSilent;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CheckAndSetupSilent;
    }

    private static void CheckAndSetup()
    {
        CheckAndSetupSilent();
    }

    private static void CheckAndSetupSilent()
    {
        var mgr = Object.FindFirstObjectByType<CommentManager>();
        if (mgr == null) return;

        bool changed = false;

        // CommentPanel_HQ 찾기
        if (mgr.commentPanel == null)
        {
            // 씬에서 CommentPanel_HQ 이름으로 찾기
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name == "CommentPanel_HQ" && obj.scene.IsValid())
                {
                    mgr.commentPanel = obj;
                    changed = true;
                    break;
                }
            }
        }

        if (mgr.commentPanel == null) return;

        // panelRect 연결 (CommentPanel_HQ 자체 또는 Sheet)
        if (mgr.panelRect == null)
        {
            // Sheet 자식을 panelRect으로 사용 (슬라이드 애니메이션 대상)
            Transform sheet = mgr.commentPanel.transform.Find("Sheet");
            if (sheet != null)
                mgr.panelRect = sheet.GetComponent<RectTransform>();
            else
                mgr.panelRect = mgr.commentPanel.GetComponent<RectTransform>();
            changed = true;
        }

        // panelCanvasGroup 연결
        if (mgr.panelCanvasGroup == null)
        {
            CanvasGroup cg = mgr.commentPanel.GetComponentInChildren<CanvasGroup>(true);
            if (cg != null)
            {
                mgr.panelCanvasGroup = cg;
                changed = true;
            }
        }

        // InputArea 하위에서 InputField 찾기
        if (mgr.commentInputField == null)
        {
            InputField inputField = mgr.commentPanel.GetComponentInChildren<InputField>(true);
            if (inputField != null)
            {
                mgr.commentInputField = inputField;
                changed = true;
            }
        }

        // SendButton 찾기 (InputArea 하위의 Button)
        if (mgr.sendButton == null)
        {
            Transform inputArea = FindChildRecursive(mgr.commentPanel.transform, "InputArea");
            if (inputArea != null)
            {
                // InputArea 하위의 Button 컴포넌트 찾기
                Button[] buttons = inputArea.GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    // InputField 내부의 버튼이 아닌 별도 Button
                    if (btn.GetComponent<InputField>() == null)
                    {
                        mgr.sendButton = btn;
                        changed = true;
                        break;
                    }
                }
            }
        }

        // commentContent 연결 (Scroll View > Viewport > Content)
        if (mgr.commentContent == null)
        {
            Transform content = FindChildRecursive(mgr.commentPanel.transform, "Content");
            if (content != null)
            {
                mgr.commentContent = content;
                changed = true;
            }
        }

        // closeButton 연결 (Header 하위의 Button)
        if (mgr.closeButton == null)
        {
            Transform header = FindChildRecursive(mgr.commentPanel.transform, "Header");
            if (header != null)
            {
                Button headerBtn = header.GetComponentInChildren<Button>(true);
                if (headerBtn != null)
                {
                    mgr.closeButton = headerBtn;
                    changed = true;
                }
            }
        }

        // titleText 연결 (Header 하위의 Text)
        if (mgr.titleText == null)
        {
            Transform header = FindChildRecursive(mgr.commentPanel.transform, "Header");
            if (header != null)
            {
                Text headerText = header.GetComponentInChildren<Text>(true);
                if (headerText != null)
                {
                    mgr.titleText = headerText;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(mgr);
            EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
        }
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
