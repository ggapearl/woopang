using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// MobileKeyboardHandler를 ChatRoomPanel과 CommentPanel에 자동 추가/연결
/// 씬 로드 시 자동 실행
/// </summary>
[InitializeOnLoad]
public class MobileKeyboardHandlerSetup
{
    static MobileKeyboardHandlerSetup()
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
        bool changed = false;

        // ============================================================
        // ChatRoomPanel (MessagePanelManager)
        // ============================================================
        var msgManagers = Object.FindObjectsByType<MessagePanelManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var msgMgr in msgManagers)
        {
            if (msgMgr != null && msgMgr.chatRoomPanel != null)
            {
                if (SetupChatRoomHandler(msgMgr))
                    changed = true;
            }
        }

        // ============================================================
        // CommentPanel (CommentManager)
        // ============================================================
        var commentManagers = Object.FindObjectsByType<CommentManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var commentMgr in commentManagers)
        {
            if (commentMgr != null && commentMgr.commentPanel != null)
            {
                if (SetupCommentHandler(commentMgr))
                    changed = true;
            }
        }

        if (changed)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    /// <summary>
    /// ChatRoomPanel에 MobileKeyboardHandler 설정
    /// </summary>
    private static bool SetupChatRoomHandler(MessagePanelManager mgr)
    {
        GameObject chatRoomPanel = mgr.chatRoomPanel;

        MobileKeyboardHandler handler = chatRoomPanel.GetComponent<MobileKeyboardHandler>();
        bool isNew = (handler == null);
        if (isNew)
            handler = chatRoomPanel.AddComponent<MobileKeyboardHandler>();

        bool changed = isNew;

        // Background 연결
        Transform bgTransform = chatRoomPanel.transform.Find("Background");
        if (bgTransform != null && handler.backgroundRect == null)
        {
            handler.backgroundRect = bgTransform.GetComponent<RectTransform>();
            changed = true;
        }

        // InputArea 연결
        if (handler.inputAreaRect == null)
        {
            if (mgr.chatInputArea != null)
            {
                handler.inputAreaRect = mgr.chatInputArea.GetComponent<RectTransform>();
                changed = true;
            }
            else if (bgTransform != null)
            {
                Transform inputAreaTr = bgTransform.Find("InputArea");
                if (inputAreaTr != null)
                {
                    handler.inputAreaRect = inputAreaTr.GetComponent<RectTransform>();
                    changed = true;
                }
            }
        }

        // ScrollRect + ScrollView RectTransform 연결
        if (handler.chatScrollRect == null && mgr.chatMessageContent != null)
        {
            ScrollRect sr = mgr.chatMessageContent.GetComponentInParent<ScrollRect>();
            if (sr != null)
            {
                handler.chatScrollRect = sr;
                handler.scrollViewRect = sr.GetComponent<RectTransform>();
                changed = true;

                // Viewport 자동 연결
                if (handler.viewportRect == null && sr.viewport != null)
                {
                    handler.viewportRect = sr.viewport;
                    changed = true;
                }
            }
        }

        // targetInputField 연결
        if (handler.targetInputField == null && mgr.chatInput != null)
        {
            handler.targetInputField = mgr.chatInput;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(handler);
            EditorUtility.SetDirty(chatRoomPanel);
        }

        return changed;
    }

    /// <summary>
    /// CommentPanel에 MobileKeyboardHandler 설정
    /// </summary>
    private static bool SetupCommentHandler(CommentManager mgr)
    {
        GameObject commentPanel = mgr.commentPanel;

        MobileKeyboardHandler handler = commentPanel.GetComponent<MobileKeyboardHandler>();
        bool isNew = (handler == null);
        if (isNew)
            handler = commentPanel.AddComponent<MobileKeyboardHandler>();

        bool changed = isNew;

        // Background 연결 (commentPanel 자체 또는 자식에서 찾기)
        if (handler.backgroundRect == null)
        {
            // CommentPanel의 panelRect을 background로 사용
            if (mgr.panelRect != null)
            {
                handler.backgroundRect = mgr.panelRect;
                changed = true;
            }
        }

        // InputArea 연결
        if (handler.inputAreaRect == null)
        {
            Transform inputAreaTr = commentPanel.transform.Find("InputArea");
            if (inputAreaTr == null && mgr.commentInputField != null)
                inputAreaTr = mgr.commentInputField.transform.parent;

            if (inputAreaTr != null)
            {
                handler.inputAreaRect = inputAreaTr.GetComponent<RectTransform>();
                changed = true;
            }
        }

        // ScrollRect + ScrollView RectTransform 연결
        if (handler.chatScrollRect == null && mgr.commentContent != null)
        {
            ScrollRect sr = mgr.commentContent.GetComponentInParent<ScrollRect>();
            if (sr != null)
            {
                handler.chatScrollRect = sr;
                handler.scrollViewRect = sr.GetComponent<RectTransform>();
                changed = true;

                // Viewport 자동 연결
                if (handler.viewportRect == null && sr.viewport != null)
                {
                    handler.viewportRect = sr.viewport;
                    changed = true;
                }
            }
        }

        // targetInputField 연결
        if (handler.targetInputField == null && mgr.commentInputField != null)
        {
            handler.targetInputField = mgr.commentInputField;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(handler);
            EditorUtility.SetDirty(commentPanel);
        }

        return changed;
    }
}
