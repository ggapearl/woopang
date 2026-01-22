using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.Callbacks;

/// <summary>
/// MessagePanelManager의 UI 레퍼런스를 자동으로 연결하는 에디터 도구
/// 스크립트 컴파일 후 자동 실행됨
/// </summary>
[InitializeOnLoad]
public static class MessagePanelAutoSetup
{
    static MessagePanelAutoSetup()
    {
        // 에디터 로드 시 딜레이 후 실행 (씬 로드 대기)
        EditorApplication.delayCall += OnEditorLoaded;
    }

    private static void OnEditorLoaded()
    {
        // Play Mode가 아닐 때만 실행
        if (!EditorApplication.isPlaying && !EditorApplication.isCompiling)
        {
            TryAutoConnect();
        }
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        // 스크립트 재컴파일 후 딜레이 실행
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlaying)
            {
                TryAutoConnect();
            }
        };
    }

    private static void TryAutoConnect()
    {
        // MessagePanelManager 자동 연결
        MessagePanelManager manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager != null)
        {
            // messagePanel이 null이거나 프리팹들이 null이면 자동 연결
            bool needsSetup = manager.messagePanel == null
                || manager.conversationItemPrefab == null
                || manager.adminNoticePrefab == null
                || manager.conversationListContent == null;

            if (needsSetup)
            {
                Debug.Log("[AutoSetup] MessagePanelManager 자동 연결 시작...");
                AutoConnectMessagePanelManager();
            }
        }

        // DMNotificationManager 자동 연결
        DMNotificationManager dmManager = Object.FindFirstObjectByType<DMNotificationManager>();
        if (dmManager != null && dmManager.notificationPrefab == null)
        {
            Debug.Log("[AutoSetup] DMNotificationManager 자동 연결 시작...");
            AutoConnectDMNotificationManager();
        }
    }

    [MenuItem("WOOPANG/Setup/Auto Connect MessagePanelManager")]
    public static void AutoConnectMessagePanelManager()
    {
        // MessagePanelManager 찾기
        MessagePanelManager manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null)
        {
            Debug.LogError("[AutoSetup] MessagePanelManager를 찾을 수 없습니다!");
            return;
        }

        Undo.RecordObject(manager, "Auto Connect MessagePanelManager");

        int connectedCount = 0;

        // === 메인 패널 ===
        // MessagePanel (DMPanel 또는 MessagePanel)
        if (manager.messagePanel == null)
        {
            manager.messagePanel = FindGameObjectByNames("DMPanel", "MessagePanel", "DirectMessagePanel");
            if (manager.messagePanel != null) connectedCount++;
        }

        // ConversationListContent
        if (manager.conversationListContent == null)
        {
            manager.conversationListContent = FindTransformByPath("ConversationList/Viewport/Content")
                ?? FindTransformByNames("ConversationListContent", "MessageListContent", "Content");
            if (manager.conversationListContent != null) connectedCount++;
        }

        // === 검색 UI ===
        // SearchInput
        if (manager.searchInput == null)
        {
            manager.searchInput = FindComponentByNames<InputField>("SearchInput", "SearchField");
            if (manager.searchInput != null) connectedCount++;
        }

        // SearchButton
        if (manager.searchButton == null)
        {
            manager.searchButton = FindComponentByNames<Button>("SearchButton");
            if (manager.searchButton != null) connectedCount++;
        }

        // SearchResultPanel
        if (manager.searchResultPanel == null)
        {
            manager.searchResultPanel = FindGameObjectByNames("SearchResultPanel", "SearchResults");
            if (manager.searchResultPanel != null) connectedCount++;
        }

        // SearchResultContent
        if (manager.searchResultContent == null)
        {
            manager.searchResultContent = FindTransformByPath("SearchResultPanel/Viewport/Content")
                ?? FindTransformByNames("SearchResultContent");
            if (manager.searchResultContent != null) connectedCount++;
        }

        // === 대화방 UI ===
        // ChatRoomPanel
        if (manager.chatRoomPanel == null)
        {
            manager.chatRoomPanel = FindGameObjectByNames("ChatRoomPanel", "ChatPanel", "ConversationPanel");
            if (manager.chatRoomPanel != null) connectedCount++;
        }

        // ChatMessageContent
        if (manager.chatMessageContent == null)
        {
            manager.chatMessageContent = FindTransformByPath("ChatRoomPanel/Viewport/Content")
                ?? FindTransformByPath("ConversationPanel/Viewport/Content")
                ?? FindTransformByNames("ChatMessageContent", "MessagesContent");
            if (manager.chatMessageContent != null) connectedCount++;
        }

        // ChatInput
        if (manager.chatInput == null)
        {
            manager.chatInput = FindComponentByNames<InputField>("ChatInput", "MessageInput", "InputField");
            if (manager.chatInput != null) connectedCount++;
        }

        // SendButton
        if (manager.sendButton == null)
        {
            manager.sendButton = FindComponentByNames<Button>("SendButton", "Send");
            if (manager.sendButton != null) connectedCount++;
        }

        // ChatRoomTitle
        if (manager.chatRoomTitle == null)
        {
            manager.chatRoomTitle = FindComponentByPath<Text>("ChatRoomPanel/Header/Title")
                ?? FindComponentByNames<Text>("ChatRoomTitle", "Title");
            if (manager.chatRoomTitle != null) connectedCount++;
        }

        // ChatRoomAvatar
        if (manager.chatRoomAvatar == null)
        {
            manager.chatRoomAvatar = FindComponentByPath<Image>("ChatRoomPanel/Header/Avatar")
                ?? FindComponentByNames<Image>("ChatRoomAvatar", "Avatar");
            if (manager.chatRoomAvatar != null) connectedCount++;
        }

        // ChatRoomBackButton
        if (manager.chatRoomBackButton == null)
        {
            manager.chatRoomBackButton = FindComponentByNames<Button>("BackButton", "ChatRoomBackButton", "CloseButton");
            if (manager.chatRoomBackButton != null) connectedCount++;
        }

        // === 안읽음 카운트 ===
        if (manager.globalUnreadCountText == null)
        {
            manager.globalUnreadCountText = FindComponentByNames<Text>("UnreadCountText", "BadgeText");
            if (manager.globalUnreadCountText != null) connectedCount++;
        }

        if (manager.globalUnreadIndicator == null)
        {
            manager.globalUnreadIndicator = FindGameObjectByNames("UnreadIndicator", "Badge", "UnreadBadge");
            if (manager.globalUnreadIndicator != null) connectedCount++;
        }

        // Prefab 연결 (Resources 폴더 또는 Prefabs 폴더에서)
        ConnectPrefabs(manager, ref connectedCount);

        EditorUtility.SetDirty(manager);

        Debug.Log($"[AutoSetup] MessagePanelManager 자동 연결 완료! {connectedCount}개 필드 연결됨");
    }

    private static void ConnectPrefabs(MessagePanelManager manager, ref int connectedCount)
    {
        // Prefabs/DM 폴더에서 프리팹 찾기
        string[] prefabPaths = new string[]
        {
            "Assets/Prefabs/DM",
            "Assets/Prefab/DM",
            "Assets/Prefabs",
            "Assets/Prefab"
        };

        // ConversationItemPrefab
        if (manager.conversationItemPrefab == null)
        {
            manager.conversationItemPrefab = FindPrefab("ConversationItem", prefabPaths);
            if (manager.conversationItemPrefab != null) connectedCount++;
        }

        // AdminNoticePrefab
        if (manager.adminNoticePrefab == null)
        {
            manager.adminNoticePrefab = FindPrefab("AdminNoticeItem", prefabPaths);
            if (manager.adminNoticePrefab != null) connectedCount++;
        }

        // SearchResultItemPrefab
        if (manager.searchResultItemPrefab == null)
        {
            manager.searchResultItemPrefab = FindPrefab("SearchResultItem", prefabPaths);
            if (manager.searchResultItemPrefab != null) connectedCount++;
        }

        // MyMessageBubblePrefab
        if (manager.myMessageBubblePrefab == null)
        {
            manager.myMessageBubblePrefab = FindPrefab("MyMessageBubble", prefabPaths);
            if (manager.myMessageBubblePrefab != null) connectedCount++;
        }

        // OtherMessageBubblePrefab
        if (manager.otherMessageBubblePrefab == null)
        {
            manager.otherMessageBubblePrefab = FindPrefab("OtherMessageBubble", prefabPaths);
            if (manager.otherMessageBubblePrefab != null) connectedCount++;
        }

        // AdminMessageBubblePrefab
        if (manager.adminMessageBubblePrefab == null)
        {
            manager.adminMessageBubblePrefab = FindPrefab("AdminMessageBubble", prefabPaths);
            if (manager.adminMessageBubblePrefab != null) connectedCount++;
        }

        // HeartAnimationPrefab
        if (manager.heartAnimationPrefab == null)
        {
            manager.heartAnimationPrefab = FindPrefab("HeartAnimation", prefabPaths);
            if (manager.heartAnimationPrefab != null) connectedCount++;
        }
    }

    private static GameObject FindPrefab(string name, string[] searchPaths)
    {
        foreach (string path in searchPaths)
        {
            string fullPath = $"{path}/{name}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
            if (prefab != null) return prefab;
        }

        // GUID로 검색
        string[] guids = AssetDatabase.FindAssets($"t:Prefab {name}");
        if (guids.Length > 0)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        return null;
    }

    private static GameObject FindGameObjectByNames(params string[] names)
    {
        foreach (string name in names)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null) return obj;

            // 비활성화된 오브젝트도 찾기
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = FindInChildren(root.transform, name);
                if (found != null) return found.gameObject;
            }
        }
        return null;
    }

    private static Transform FindTransformByNames(params string[] names)
    {
        foreach (string name in names)
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = FindInChildren(root.transform, name);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static Transform FindTransformByPath(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length == 0) return null;

        // 첫 번째 부분으로 루트 찾기
        GameObject rootObj = null;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var found = FindInChildren(root.transform, parts[0]);
            if (found != null)
            {
                rootObj = found.gameObject;
                break;
            }
        }

        if (rootObj == null) return null;

        // 나머지 경로 따라가기
        Transform current = rootObj.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null) return null;
        }

        return current;
    }

    private static T FindComponentByNames<T>(params string[] names) where T : Component
    {
        foreach (string name in names)
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = FindInChildren(root.transform, name);
                if (found != null)
                {
                    var comp = found.GetComponent<T>();
                    if (comp != null) return comp;
                }
            }
        }
        return null;
    }

    private static T FindComponentByPath<T>(string path) where T : Component
    {
        Transform tr = FindTransformByPath(path);
        return tr != null ? tr.GetComponent<T>() : null;
    }

    private static Transform FindInChildren(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        foreach (Transform child in parent)
        {
            var found = FindInChildren(child, name);
            if (found != null) return found;
        }

        return null;
    }

    #region DMNotificationManager Auto Connect

    [MenuItem("WOOPANG/Setup/Auto Connect DMNotificationManager")]
    public static void AutoConnectDMNotificationManager()
    {
        DMNotificationManager manager = Object.FindFirstObjectByType<DMNotificationManager>();
        if (manager == null)
        {
            Debug.LogError("[AutoSetup] DMNotificationManager를 찾을 수 없습니다!");
            return;
        }

        Undo.RecordObject(manager, "Auto Connect DMNotificationManager");

        int connectedCount = 0;
        string[] prefabPaths = new string[]
        {
            "Assets/Prefabs/DM",
            "Assets/Prefab/DM",
            "Assets/Prefabs",
            "Assets/Prefab"
        };

        // NotificationPrefab
        if (manager.notificationPrefab == null)
        {
            manager.notificationPrefab = FindPrefab("NotificationItem", prefabPaths)
                ?? FindPrefab("DMNotificationItem", prefabPaths)
                ?? FindPrefab("Notification", prefabPaths);
            if (manager.notificationPrefab != null) connectedCount++;
        }

        // NotificationContainer - Canvas 하위의 NotificationContainer 찾기
        if (manager.notificationContainer == null)
        {
            manager.notificationContainer = FindTransformByNames("NotificationContainer", "NotificationsContainer", "ToastContainer");
            if (manager.notificationContainer != null) connectedCount++;
        }

        // NotificationSound - AudioClip 찾기
        if (manager.notificationSound == null)
        {
            manager.notificationSound = FindAudioClip("notification", "message", "alert", "ding");
            if (manager.notificationSound != null) connectedCount++;
        }

        EditorUtility.SetDirty(manager);

        if (connectedCount > 0)
        {
            Debug.Log($"[AutoSetup] DMNotificationManager 자동 연결 완료! {connectedCount}개 필드 연결됨");
        }
        else
        {
            Debug.Log("[AutoSetup] DMNotificationManager: 연결할 에셋이 없습니다. (런타임에 자동 생성됨)");
        }
    }

    private static AudioClip FindAudioClip(params string[] searchNames)
    {
        foreach (string name in searchNames)
        {
            string[] guids = AssetDatabase.FindAssets($"t:AudioClip {name}");
            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip != null) return clip;
            }
        }
        return null;
    }

    #endregion
}
