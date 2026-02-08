using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;
using System.IO;

/// <summary>
/// WOOPANG DM 시스템 자동 설정
///
/// 기존 프리팹 활용:
/// - Assets/Prefab/MessagePanel.prefab: 메시지 목록 패널
/// - Assets/Prefab/MessageChatTemplate.prefab: 대화방 아이템 템플릿
/// - Assets/Prefabs/DM/: 메시지 버블 등 DM 관련 프리팹
///
/// 씬 로드 시 자동으로:
/// - UnfollowConfirmDialog 생성 및 연결
/// - 누락된 UI 오브젝트 자동 생성
/// </summary>
[InitializeOnLoad]
public class DMSetup
{
    // 씬 로드 시 자동 설정
    static DMSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndSetupUIOnLoad;
    }

    // 스크립트 컴파일 완료 시 자동 실행 + 씬 리로드
    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        // 컴파일 직후 딜레이를 주고 씬 리로드
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                return;

            // 현재 씬 리로드 (레이아웃 수정 적용)
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (currentScene.IsValid() && !string.IsNullOrEmpty(currentScene.path))
            {
                // 저장되지 않은 변경사항 확인
                if (currentScene.isDirty)
                {
                    EditorSceneManager.SaveScene(currentScene);
                }
                EditorSceneManager.OpenScene(currentScene.path, OpenSceneMode.Single);
            }
        };
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CheckAndSetupUISilent;
    }

    private static void CheckAndSetupUIOnLoad()
    {
        CheckAndSetupUISilent();
    }

    /// <summary>
    /// 씬 로드 시 자동으로 UI 체크 및 생성 (조용히)
    /// </summary>
    private static void CheckAndSetupUISilent()
    {
        // Play Mode가 아닐 때만 실행
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;

        // FollowManager 찾기
        FollowManager followManager = Object.FindFirstObjectByType<FollowManager>();
        if (followManager != null)
        {
            // UnfollowConfirmDialog 체크 및 생성
            CheckAndCreateUnfollowDialog(followManager);

            // itemPrefab 자동 연결
            ConnectFollowListItemPrefab(followManager);
        }

        // MessagePanelManager의 closeButton 연결
        SetupMessagePanelCloseButton();

        // AdminNoticeItem 프리팹 정리 (Badge→UnreadBadge, 불필요한 래퍼 제거)
        EnsureAdminNoticeItemContent();

        // MessagePanelManager의 모든 필드 자동 연결
        AutoConnectMessagePanelManager();
    }

    /// <summary>
    /// MessagePanelManager의 모든 필드 자동 연결 (씬 로드 시)
    /// </summary>
    private static void AutoConnectMessagePanelManager()
    {
        MessagePanelManager manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null) return;

        bool wasChanged = false;

        // chatRoomPanel 연결
        if (manager.chatRoomPanel == null)
        {
            GameObject chatPanel = GameObject.Find("ChatRoomPanel");
            if (chatPanel != null)
            {
                manager.chatRoomPanel = chatPanel;
                wasChanged = true;
            }
        }

        // chatRoomTitle 연결 (실제 씬 구조: ChatRoomPanel > Background > Header > ChatTitle)
        // ★ null이 아니어도 chatRoomPanel의 자식이 아니면 재연결 (잘못된 오브젝트 참조 방지)
        if (manager.chatRoomPanel != null)
        {
            Transform titleTr = manager.chatRoomPanel.transform.Find("Background/Header/ChatTitle");
            if (titleTr == null) titleTr = manager.chatRoomPanel.transform.Find("Header/ChatTitle");
            if (titleTr != null)
            {
                Text titleText = titleTr.GetComponent<Text>();
                if (titleText != null)
                {
                    // chatRoomTitle이 null이거나 ChatRoomPanel의 자식이 아니면 재연결
                    bool needsReconnect = (manager.chatRoomTitle == null);
                    if (!needsReconnect && manager.chatRoomTitle != null)
                    {
                        // 현재 연결된 오브젝트가 chatRoomPanel의 자식인지 검증
                        needsReconnect = !manager.chatRoomTitle.transform.IsChildOf(manager.chatRoomPanel.transform);
                    }

                    if (needsReconnect)
                    {
                        manager.chatRoomTitle = titleText;
                        wasChanged = true;
                        Debug.Log("[DMSetup] chatRoomTitle → ChatRoomPanel/Background/Header/ChatTitle 재연결 완료");
                    }
                }
            }
        }

        // chatInputArea 연결 (실제 씬 구조: ChatRoomPanel > Background > InputArea)
        if (manager.chatInputArea == null && manager.chatRoomPanel != null)
        {
            Transform inputAreaTr = manager.chatRoomPanel.transform.Find("Background/InputArea");
            if (inputAreaTr == null) inputAreaTr = manager.chatRoomPanel.transform.Find("InputArea");
            if (inputAreaTr != null)
            {
                manager.chatInputArea = inputAreaTr.gameObject;
                wasChanged = true;
            }
        }

        // chatInput 연결
        if (manager.chatInput == null && manager.chatInputArea != null)
        {
            Transform inputFieldTr = manager.chatInputArea.transform.Find("ChatInput");
            if (inputFieldTr == null) inputFieldTr = manager.chatInputArea.transform.Find("InputField");
            if (inputFieldTr != null)
            {
                InputField inputField = inputFieldTr.GetComponent<InputField>();
                if (inputField != null)
                {
                    manager.chatInput = inputField;
                    wasChanged = true;
                }
            }
        }

        // sendButton 연결
        if (manager.sendButton == null && manager.chatInputArea != null)
        {
            Transform sendBtnTr = manager.chatInputArea.transform.Find("SendButton");
            if (sendBtnTr != null)
            {
                Button sendBtn = sendBtnTr.GetComponent<Button>();
                if (sendBtn != null)
                {
                    manager.sendButton = sendBtn;
                    wasChanged = true;
                }
            }
        }

        // chatMessageContent 연결 (채팅 메시지가 들어갈 Content)
        if (manager.chatRoomPanel != null)
        {
            // 실제 씬 구조: ChatRoomPanel > Background > MessageArea > Viewport > Content
            Transform contentTr = manager.chatRoomPanel.transform.Find("Background/MessageArea/Viewport/Content");
            if (contentTr == null) contentTr = manager.chatRoomPanel.transform.Find("MessageArea/Viewport/Content");

            // chatMessageContent 필드가 있는지 확인하고 연결 (리플렉션 사용 안함, public 필드면 직접 접근)
            var field = typeof(MessagePanelManager).GetField("chatMessageContent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null && contentTr != null)
            {
                var currentValue = field.GetValue(manager) as Transform;
                if (currentValue == null)
                {
                    field.SetValue(manager, contentTr);
                    wasChanged = true;
                }
            }
        }

        // DateSeparator 프리팹 자동 생성 및 연결
        if (manager.dateSeparatorPrefab == null)
        {
            EnsureDateSeparatorPrefab(manager);
            wasChanged = true;
        }

        if (wasChanged)
        {
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
    }

    /// <summary>
    /// DateSeparator 프리팹 자동 생성 (없을 때만)
    /// </summary>
    private static void EnsureDateSeparatorPrefab(MessagePanelManager manager)
    {
        string prefabDir = "Assets/Prefabs/DM";
        string prefabPath = prefabDir + "/DateSeparator.prefab";

        // 이미 존재하면 로드만
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            manager.dateSeparatorPrefab = existingPrefab;
            return;
        }

        // 폴더 확인
        if (!AssetDatabase.IsValidFolder(prefabDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "DM");
        }

        // DateSeparator 프리팹 생성
        GameObject separator = new GameObject("DateSeparator");

        RectTransform separatorRect = separator.AddComponent<RectTransform>();
        separatorRect.anchorMin = new Vector2(0, 1);
        separatorRect.anchorMax = new Vector2(1, 1);
        separatorRect.pivot = new Vector2(0.5f, 1);

        LayoutElement separatorLE = separator.AddComponent<LayoutElement>();
        separatorLE.flexibleWidth = 1;
        separatorLE.preferredHeight = 68f;

        // 텍스트
        GameObject textObj = new GameObject("DateText");
        textObj.transform.SetParent(separator.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text dateText = textObj.AddComponent<Text>();
        dateText.text = "날짜";
        dateText.fontSize = 28;
        dateText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        dateText.alignment = TextAnchor.MiddleCenter;

        Font customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/AppleSDGothicNeoM.ttf");
        if (customFont != null)
            dateText.font = customFont;

        // 프리팹 저장
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(separator, prefabPath);
        Object.DestroyImmediate(separator);

        if (prefab != null)
        {
            manager.dateSeparatorPrefab = prefab;
            Debug.Log("[DMSetup] DateSeparator 프리팹 생성 완료: " + prefabPath);
        }
    }

    /// <summary>
    /// MessagePanelManager의 closeButton 자동 연결
    /// </summary>
    private static void SetupMessagePanelCloseButton()
    {
        MessagePanelManager manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null) return;

        // 이미 연결되어 있으면 스킵
        if (manager.closeButton != null) return;

        // messagePanel 안에서 CloseButton 찾기
        if (manager.messagePanel != null)
        {
            // 직접 자식에서 찾기
            Transform closeBtn = manager.messagePanel.transform.Find("CloseButton");
            if (closeBtn == null)
            {
                // 이름 패턴으로 재귀 검색
                closeBtn = FindChildByNamePattern(manager.messagePanel.transform, new[] { "CloseButton", "Close_Button", "X_Button", "BackButton" });
            }

            if (closeBtn != null)
            {
                Button btn = closeBtn.GetComponent<Button>();
                if (btn != null)
                {
                    manager.closeButton = btn;
                    EditorUtility.SetDirty(manager);
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                    Debug.Log("[DMSetup] MessagePanel closeButton 자동 연결 완료");
                }
            }
        }
    }

    private static Transform FindChildByNamePattern(Transform parent, string[] patterns)
    {
        foreach (Transform child in parent)
        {
            foreach (string pattern in patterns)
            {
                if (child.name.Contains(pattern))
                {
                    return child;
                }
            }

            // 재귀 검색
            Transform found = FindChildByNamePattern(child, patterns);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// FollowListItem 프리팹 자동 연결
    /// </summary>
    private static void ConnectFollowListItemPrefab(FollowManager manager)
    {
        if (manager.itemPrefab != null) return;

        string[] paths = new[]
        {
            "Assets/Prefabs/FollowListItem.prefab",
            "Assets/Prefabs/Profile/FollowListItem.prefab"
        };

        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                manager.itemPrefab = prefab;
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                break;
            }
        }
    }

    /// <summary>
    /// UnfollowConfirmDialog 체크 및 자동 생성
    /// </summary>
    private static void CheckAndCreateUnfollowDialog(FollowManager manager)
    {
        // 이미 있으면 스킵
        if (manager.unfollowConfirmDialog != null) return;

        // panel이 없으면 자동으로 찾기
        if (manager.panel == null)
        {
            GameObject followPanel = GameObject.Find("FollowPanel");
            if (followPanel != null)
            {
                manager.panel = followPanel;
                EditorUtility.SetDirty(manager);
                Debug.Log("[DMSetup] FollowPanel 자동 연결됨");
            }
            else
            {
                Debug.LogWarning("[DMSetup] FollowPanel을 찾을 수 없습니다.");
                return;
            }
        }

        // 기존에 생성된 다이얼로그 찾기
        Transform existingDialog = manager.panel.transform.Find("UnfollowConfirmDialog");
        if (existingDialog != null)
        {
            // 연결만 수행
            ConnectUnfollowDialog(manager, existingDialog.gameObject);
            return;
        }

        // 새로 생성
        Debug.Log("[DMSetup] UnfollowConfirmDialog 자동 생성 중...");

        Color bgColor = new Color(0.12f, 0.12f, 0.15f, 1f);
        Color textColor = Color.white;
        Color pinkColor = new Color(0.91f, 0.33f, 0.51f, 1f);

        GameObject dialog = CreateUnfollowConfirmDialog(manager.panel.transform, bgColor, textColor, pinkColor);
        ConnectUnfollowDialog(manager, dialog);

        // 씬 변경 표시
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorUtility.SetDirty(manager);

        Debug.Log("[DMSetup] UnfollowConfirmDialog 자동 생성 및 연결 완료!");
    }

    /// <summary>
    /// UnfollowConfirmDialog 필드 연결
    /// </summary>
    private static void ConnectUnfollowDialog(FollowManager manager, GameObject dialog)
    {
        manager.unfollowConfirmDialog = dialog;
        manager.unfollowConfirmText = dialog.transform.Find("DialogBox/ConfirmText")?.GetComponent<Text>();
        manager.unfollowConfirmButton = dialog.transform.Find("DialogBox/ButtonContainer/ConfirmButton")?.GetComponent<Button>();
        manager.unfollowCancelButton = dialog.transform.Find("DialogBox/ButtonContainer/CancelButton")?.GetComponent<Button>();

        EditorUtility.SetDirty(manager);
    }

    /// <summary>
    /// 수동으로 UnfollowConfirmDialog 생성 (메뉴에서 실행)
    /// </summary>
    [MenuItem("Woopang/Setup/UnfollowConfirmDialog 생성")]
    private static void CreateUnfollowDialogManual()
    {
        FollowManager manager = Object.FindFirstObjectByType<FollowManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에서 FollowManager를 찾을 수 없습니다.", "확인");
            return;
        }

        // 기존 다이얼로그 삭제 (재생성)
        if (manager.unfollowConfirmDialog != null)
        {
            Object.DestroyImmediate(manager.unfollowConfirmDialog);
            manager.unfollowConfirmDialog = null;
        }

        // panel이 없으면 찾기
        if (manager.panel == null)
        {
            GameObject followPanel = GameObject.Find("FollowPanel");
            if (followPanel != null)
            {
                manager.panel = followPanel;
                EditorUtility.SetDirty(manager);
            }
            else
            {
                EditorUtility.DisplayDialog("오류", "FollowPanel을 찾을 수 없습니다.", "확인");
                return;
            }
        }

        // 생성
        Color bgColor = new Color(0.12f, 0.12f, 0.15f, 1f);
        Color textColor = Color.white;
        Color pinkColor = new Color(0.91f, 0.33f, 0.51f, 1f);

        GameObject dialog = CreateUnfollowConfirmDialog(manager.panel.transform, bgColor, textColor, pinkColor);
        ConnectUnfollowDialog(manager, dialog);

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorUtility.SetDirty(manager);

        EditorUtility.DisplayDialog("완료", "UnfollowConfirmDialog가 생성되었습니다.\n\n씬을 저장하세요.", "확인");
    }

    /// <summary>
    /// DM 시스템 연결 새로고침 (메뉴에서 수동 실행)
    /// MessagePanelManager의 모든 필드를 다시 연결
    /// </summary>
    [MenuItem("Woopang/Setup/DM 연결 새로고침")]
    private static void RefreshDMConnections()
    {
        MessagePanelManager manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에서 MessagePanelManager를 찾을 수 없습니다.", "확인");
            return;
        }

        int connectedCount = 0;

        // 1. messagePanel 연결
        if (manager.messagePanel == null)
        {
            GameObject msgPanel = GameObject.Find("MessagePanel");
            if (msgPanel != null)
            {
                manager.messagePanel = msgPanel;
                connectedCount++;
                Debug.Log("[DMSetup] messagePanel 연결됨");
            }
        }

        // 2. chatRoomPanel 연결
        if (manager.chatRoomPanel == null)
        {
            GameObject chatPanel = GameObject.Find("ChatRoomPanel");
            if (chatPanel != null)
            {
                manager.chatRoomPanel = chatPanel;
                connectedCount++;
                Debug.Log("[DMSetup] chatRoomPanel 연결됨");
            }
        }

        // 3. chatRoomTitle 연결
        // 실제 씬 구조: ChatRoomPanel > Background > Header > ChatTitle
        if (manager.chatRoomTitle == null && manager.chatRoomPanel != null)
        {
            Transform chatPanel = manager.chatRoomPanel.transform;

            Transform titleTr = chatPanel.Find("Background/Header/ChatTitle");
            if (titleTr == null) titleTr = chatPanel.Find("Header/ChatTitle");
            if (titleTr == null) titleTr = chatPanel.Find("Background/Header/TitleText");
            if (titleTr == null) titleTr = FindChildByNamePattern(chatPanel, new[] { "ChatTitle", "TitleText", "Title" });

            if (titleTr != null)
            {
                Text titleText = titleTr.GetComponent<Text>();
                if (titleText != null)
                {
                    manager.chatRoomTitle = titleText;
                    connectedCount++;
                    Debug.Log("[DMSetup] chatRoomTitle 연결됨 (Background/Header/ChatTitle)");
                }
            }
        }

        // 4. chatInputArea 연결 (시스템 알림에서 숨기기 위해)
        // 실제 씬 구조: ChatRoomPanel > Background > InputArea
        if (manager.chatInputArea == null && manager.chatRoomPanel != null)
        {
            Transform inputAreaTr = manager.chatRoomPanel.transform.Find("Background/InputArea");
            if (inputAreaTr == null) inputAreaTr = manager.chatRoomPanel.transform.Find("InputArea");
            if (inputAreaTr != null)
            {
                manager.chatInputArea = inputAreaTr.gameObject;
                connectedCount++;
                Debug.Log("[DMSetup] chatInputArea 연결됨 (Background/InputArea)");
            }
        }

        // 5. chatInput 연결
        if (manager.chatInput == null && manager.chatRoomPanel != null)
        {
            Transform inputAreaTr = manager.chatRoomPanel.transform.Find("Background/InputArea");
            if (inputAreaTr == null) inputAreaTr = manager.chatRoomPanel.transform.Find("InputArea");
            if (inputAreaTr != null)
            {
                Transform inputFieldTr = inputAreaTr.Find("InputField");
                if (inputFieldTr != null)
                {
                    InputField inputField = inputFieldTr.GetComponent<InputField>();
                    if (inputField != null)
                    {
                        manager.chatInput = inputField;
                        connectedCount++;
                        Debug.Log("[DMSetup] chatInput 연결됨");
                    }
                }
            }
        }

        // 6. sendButton 연결
        if (manager.sendButton == null && manager.chatRoomPanel != null)
        {
            Transform inputAreaTr = manager.chatRoomPanel.transform.Find("Background/InputArea");
            if (inputAreaTr == null) inputAreaTr = manager.chatRoomPanel.transform.Find("InputArea");
            if (inputAreaTr != null)
            {
                Transform sendBtnTr = inputAreaTr.Find("SendButton");
                if (sendBtnTr != null)
                {
                    Button sendBtn = sendBtnTr.GetComponent<Button>();
                    if (sendBtn != null)
                    {
                        manager.sendButton = sendBtn;
                        connectedCount++;
                        Debug.Log("[DMSetup] sendButton 연결됨");
                    }
                }
            }
        }

        // 7. closeButton 연결
        if (manager.closeButton == null && manager.messagePanel != null)
        {
            Transform closeBtn = FindChildByNamePattern(manager.messagePanel.transform,
                new[] { "CloseButton", "Close_Button", "X_Button", "BackButton" });
            if (closeBtn != null)
            {
                Button btn = closeBtn.GetComponent<Button>();
                if (btn != null)
                {
                    manager.closeButton = btn;
                    connectedCount++;
                    Debug.Log("[DMSetup] closeButton 연결됨");
                }
            }
        }

        // 8. conversationListContent 연결
        if (manager.conversationListContent == null && manager.messagePanel != null)
        {
            Transform contentTr = manager.messagePanel.transform.Find("ScrollArea/Content");
            if (contentTr == null) contentTr = FindChildByNamePattern(manager.messagePanel.transform,
                new[] { "Content", "ListContent", "ConversationContent" });
            if (contentTr != null)
            {
                manager.conversationListContent = contentTr;
                connectedCount++;
                Debug.Log("[DMSetup] conversationListContent 연결됨");
            }
        }

        // 9. chatRoomBackButton 연결
        if (manager.chatRoomBackButton == null && manager.chatRoomPanel != null)
        {
            Transform backBtnTr = manager.chatRoomPanel.transform.Find("Header/BackButton");
            if (backBtnTr == null) backBtnTr = FindChildByNamePattern(manager.chatRoomPanel.transform,
                new[] { "BackButton", "Back_Button", "CloseButton" });
            if (backBtnTr != null)
            {
                Button backBtn = backBtnTr.GetComponent<Button>();
                if (backBtn != null)
                {
                    manager.chatRoomBackButton = backBtn;
                    connectedCount++;
                    Debug.Log("[DMSetup] chatRoomBackButton 연결됨");
                }
            }
        }

        // 씬 변경 표시
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        string message = connectedCount > 0
            ? $"DM 연결 새로고침 완료!\n\n{connectedCount}개 필드가 연결되었습니다.\n씬을 저장하세요 (Ctrl+S)."
            : "모든 필드가 이미 연결되어 있습니다.";

        EditorUtility.DisplayDialog("DM 연결 새로고침", message, "확인");
        Debug.Log($"[DMSetup] DM 연결 새로고침 완료 - {connectedCount}개 필드 연결됨");
    }


    // 커스텀 폰트 경로
    private static readonly string CUSTOM_FONT_PATH = "Fonts/AppleSDGothicNeoM";

    /// <summary>
    /// 커스텀 폰트 로드 (없으면 기본 폰트 사용)
    /// </summary>
    private static Font GetDefaultFont()
    {
        // Resources에서 커스텀 폰트 로드 시도
        Font customFont = Resources.Load<Font>(CUSTOM_FONT_PATH);
        if (customFont != null)
        {
            return customFont;
        }

        // AssetDatabase에서 직접 로드 시도
        customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/AppleSDGothicNeoM.ttf");
        if (customFont != null)
        {
            return customFont;
        }

        // 대체 경로
        customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/AppleSDGothicNeoM.ttf");
        if (customFont != null)
        {
            return customFont;
        }

        // 폴백: Unity 기본 Arial 폰트
        Debug.LogWarning("[DMSetup] AppleSDGothicNeoM.ttf 폰트를 찾을 수 없습니다. Arial 폰트를 사용합니다.");
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // 자동 생성 기능 비활성화 - 기존 FollowPanel 사용
    /*
    static DMSetup()
    {
        // 에디터 로드 시 딜레이 후 자동 설정 체크
        EditorApplication.delayCall += OnEditorLoaded;
    }

    private static void OnEditorLoaded()
    {
        // Play Mode가 아닐 때만 실행
        if (!EditorApplication.isPlaying && !EditorApplication.isCompiling)
        {
            TryAutoSetup();
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
                TryAutoSetup();
            }
        };
    }
    */

    /// <summary>
    /// 필요한 경우에만 자동 설정 실행
    /// </summary>
    private static void TryAutoSetup()
    {
        ProfileManager profileManager = Object.FindFirstObjectByType<ProfileManager>();
        if (profileManager == null) return;

        // 프리팹 연결 확인 (항상 실행)
        AutoConnectPrefabs(profileManager);

        // FollowListPanel이 없으면 생성
        if (profileManager.followListPanel == null)
        {
            Debug.Log("[DMSetup] FollowListPanel 자동 생성 시작...");
            CreateFollowListPanel();
            return;
        }

        // 기존 패널이 있지만 스와이프 기능이 없으면 업그레이드
        if (profileManager.swipePageHandler == null)
        {
            // 구조 호환성 확인 - ContentArea가 없으면 재생성 필요
            Transform contentArea = profileManager.followListPanel.transform.Find("ContentArea");
            if (contentArea == null)
            {
                Debug.Log("[DMSetup] 기존 FollowListPanel 구조가 호환되지 않음. 재생성 중...");
                // 기존 패널 삭제 후 새로 생성
                Undo.DestroyObjectImmediate(profileManager.followListPanel);
                profileManager.followListPanel = null;
                CreateFollowListPanel();
            }
            else
            {
                Debug.Log("[DMSetup] 기존 FollowListPanel을 스와이프 버전으로 업그레이드...");
                UpgradeToSwipePanel(profileManager);
            }
        }
    }

    /// <summary>
    /// 프리팹 자동 연결 (TryAutoSetup에서 호출)
    /// </summary>
    private static void AutoConnectPrefabs(ProfileManager profileManager)
    {
        bool changed = false;

        if (profileManager.followingItemPrefab == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Profile/FollowingItem.prefab");
            if (prefab != null)
            {
                profileManager.followingItemPrefab = prefab;
                changed = true;
            }
        }

        if (profileManager.followerItemPrefab == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Profile/FollowerItem.prefab");
            if (prefab != null)
            {
                profileManager.followerItemPrefab = prefab;
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(profileManager);
            Debug.Log("[DMSetup] Follow Item 프리팹 자동 연결 완료");
        }
    }

    /// <summary>
    /// 기존 FollowListPanel을 스와이프 버전으로 업그레이드
    /// 누락된 구조(Header, TabBar, ContentArea, 프리팹)도 모두 생성
    /// </summary>
    private static void UpgradeToSwipePanel(ProfileManager profileManager)
    {
        GameObject panel = profileManager.followListPanel;
        if (panel == null) return;

        Font defaultFont = GetDefaultFont();
        Color bgColor = new Color(0.07f, 0.07f, 0.09f, 1f);
        Color headerColor = new Color(0.07f, 0.07f, 0.09f, 1f);

        // === 1. Header 확인/생성 ===
        Transform header = panel.transform.Find("Header");
        if (header == null)
        {
            Debug.Log("[DMSetup] Header 생성 중...");
            header = CreateHeader(panel.transform, defaultFont, headerColor).transform;
        }

        // === 2. TabBar 확인/생성 ===
        Transform tabBar = panel.transform.Find("TabBar");
        if (tabBar == null)
        {
            Debug.Log("[DMSetup] TabBar 생성 중...");
            tabBar = CreateTabBar(panel.transform, defaultFont, headerColor).transform;
        }

        // === 3. SearchBar 확인/생성 ===
        Transform searchBar = panel.transform.Find("SearchBar");
        if (searchBar == null)
        {
            Debug.Log("[DMSetup] SearchBar 생성 중...");
            searchBar = CreateSearchBar(panel.transform, defaultFont).transform;
        }

        // === 4. ContentArea 확인/생성 ===
        Transform contentArea = panel.transform.Find("ContentArea");
        if (contentArea == null)
        {
            Debug.Log("[DMSetup] ContentArea 생성 중...");
            contentArea = CreateContentArea(panel.transform, defaultFont, bgColor).transform;
        }

        // 이미 스와이프 구조가 있는지 확인
        if (contentArea.Find("SwipeViewport") != null)
        {
            Debug.Log("[DMSetup] 이미 스와이프 구조가 적용되어 있습니다.");
            // 참조만 다시 연결하고 프리팹 확인
            ReconnectSwipeReferences(profileManager, panel);
            EnsurePrefabsExist(profileManager);
            return;
        }

        // 기존 페이지들 찾기 (없으면 생성)
        Transform oldFollowersPage = contentArea.Find("FollowersPage");
        Transform oldFollowingPage = contentArea.Find("FollowingPage");

        if (oldFollowersPage == null)
        {
            Debug.Log("[DMSetup] FollowersPage 생성 중...");
            oldFollowersPage = CreateListPage("FollowersPage", contentArea, defaultFont).transform;
        }

        if (oldFollowingPage == null)
        {
            Debug.Log("[DMSetup] FollowingPage 생성 중...");
            oldFollowingPage = CreateListPage("FollowingPage", contentArea, defaultFont).transform;
        }

        // === 5. 스와이프 구조 적용 ===
        // 가로 스크롤용 ScrollRect 추가
        ScrollRect horizontalScroll = contentArea.gameObject.GetComponent<ScrollRect>();
        if (horizontalScroll == null)
            horizontalScroll = Undo.AddComponent<ScrollRect>(contentArea.gameObject);

        horizontalScroll.horizontal = true;
        horizontalScroll.vertical = false;
        horizontalScroll.movementType = ScrollRect.MovementType.Elastic;
        horizontalScroll.elasticity = 0.1f;
        horizontalScroll.inertia = false;

        // ContentArea에 배경 이미지 확인/추가
        Image contentAreaBg = contentArea.gameObject.GetComponent<Image>();
        if (contentAreaBg == null)
        {
            contentAreaBg = Undo.AddComponent<Image>(contentArea.gameObject);
            contentAreaBg.color = bgColor;
        }

        // SwipeViewport 생성
        GameObject swipeViewport = new GameObject("SwipeViewport");
        Undo.RegisterCreatedObjectUndo(swipeViewport, "Create SwipeViewport");
        swipeViewport.transform.SetParent(contentArea, false);

        RectTransform swipeViewportRect = swipeViewport.AddComponent<RectTransform>();
        swipeViewportRect.anchorMin = Vector2.zero;
        swipeViewportRect.anchorMax = Vector2.one;
        swipeViewportRect.offsetMin = Vector2.zero;
        swipeViewportRect.offsetMax = Vector2.zero;

        swipeViewport.AddComponent<Image>().color = Color.clear;
        swipeViewport.AddComponent<Mask>().showMaskGraphic = false;

        // SwipeContent 생성
        GameObject swipeContent = new GameObject("SwipeContent");
        Undo.RegisterCreatedObjectUndo(swipeContent, "Create SwipeContent");
        swipeContent.transform.SetParent(swipeViewport.transform, false);

        RectTransform swipeContentRect = swipeContent.AddComponent<RectTransform>();
        swipeContentRect.anchorMin = new Vector2(0, 0);
        swipeContentRect.anchorMax = new Vector2(0, 1);
        swipeContentRect.pivot = new Vector2(0, 0.5f);
        swipeContentRect.sizeDelta = new Vector2(0, 0);

        HorizontalLayoutGroup swipeHlg = swipeContent.AddComponent<HorizontalLayoutGroup>();
        swipeHlg.spacing = 0;
        swipeHlg.padding = new RectOffset(0, 0, 0, 0);
        swipeHlg.childAlignment = TextAnchor.MiddleLeft;
        swipeHlg.childForceExpandWidth = false;
        swipeHlg.childForceExpandHeight = true;
        swipeHlg.childControlWidth = false;
        swipeHlg.childControlHeight = true;

        // ScrollRect 연결
        horizontalScroll.viewport = swipeViewportRect;
        horizontalScroll.content = swipeContentRect;

        // 기존 페이지들을 SwipeContent로 이동 (순서: 팔로잉 먼저, 팔로워 나중)
        Undo.SetTransformParent(oldFollowingPage, swipeContent.transform, "Move FollowingPage");
        Undo.SetTransformParent(oldFollowersPage, swipeContent.transform, "Move FollowersPage");

        // 순서 조정 (팔로잉이 왼쪽=첫번째)
        oldFollowingPage.SetSiblingIndex(0);
        oldFollowersPage.SetSiblingIndex(1);

        // 페이지 활성화 (스와이프에서는 둘 다 활성)
        oldFollowingPage.gameObject.SetActive(true);
        oldFollowersPage.gameObject.SetActive(true);

        // 페이지에 LayoutElement 추가
        LayoutElement followingLE = oldFollowingPage.gameObject.GetComponent<LayoutElement>();
        if (followingLE == null)
            followingLE = Undo.AddComponent<LayoutElement>(oldFollowingPage.gameObject);
        followingLE.flexibleHeight = 1;

        LayoutElement followersLE = oldFollowersPage.gameObject.GetComponent<LayoutElement>();
        if (followersLE == null)
            followersLE = Undo.AddComponent<LayoutElement>(oldFollowersPage.gameObject);
        followersLE.flexibleHeight = 1;

        // 페이지 RectTransform 설정
        RectTransform followingRect = oldFollowingPage.GetComponent<RectTransform>();
        followingRect.anchorMin = Vector2.zero;
        followingRect.anchorMax = new Vector2(0, 1);
        followingRect.pivot = new Vector2(0, 0.5f);

        RectTransform followersRect = oldFollowersPage.GetComponent<RectTransform>();
        followersRect.anchorMin = Vector2.zero;
        followersRect.anchorMax = new Vector2(0, 1);
        followersRect.pivot = new Vector2(0, 0.5f);

        // SwipePageHandler 추가
        SwipePageHandler swipeHandler = contentArea.gameObject.GetComponent<SwipePageHandler>();
        if (swipeHandler == null)
            swipeHandler = Undo.AddComponent<SwipePageHandler>(contentArea.gameObject);

        swipeHandler.pageCount = 2;
        swipeHandler.scrollRect = horizontalScroll;
        swipeHandler.snapSpeed = 10f;
        swipeHandler.swipeThreshold = 0.2f;

        // SwipePageSizer 추가
        SwipePageSizer pageSizer = swipeContent.GetComponent<SwipePageSizer>();
        if (pageSizer == null)
            pageSizer = Undo.AddComponent<SwipePageSizer>(swipeContent);

        pageSizer.viewport = swipeViewportRect;

        // === 6. 탭 텍스트 및 순서 업데이트 ===
        UpdateTabTexts(panel);
        ReorderTabs(panel);

        // === 7. ProfileManager 참조 업데이트 ===
        ReconnectSwipeReferences(profileManager, panel);

        // === 8. 아이템 프리팹 생성 ===
        EnsurePrefabsExist(profileManager);

        EditorUtility.SetDirty(profileManager);
        EditorSceneManager.MarkSceneDirty(panel.scene);
        Debug.Log("[DMSetup] 스와이프 업그레이드 완료!");
    }

    /// <summary>
    /// Header 생성 (뒤로가기 버튼 + 제목)
    /// </summary>
    private static GameObject CreateHeader(Transform parent, Font font, Color bgColor)
    {
        GameObject header = new GameObject("Header");
        Undo.RegisterCreatedObjectUndo(header, "Create Header");
        header.transform.SetParent(parent, false);

        RectTransform headerRect = header.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, 60);
        headerRect.anchoredPosition = Vector2.zero;

        Image headerBg = header.AddComponent<Image>();
        headerBg.color = bgColor;

        // 뒤로가기 버튼
        GameObject backBtn = new GameObject("BackButton");
        backBtn.transform.SetParent(header.transform, false);

        RectTransform backBtnRect = backBtn.AddComponent<RectTransform>();
        backBtnRect.anchorMin = new Vector2(0, 0.5f);
        backBtnRect.anchorMax = new Vector2(0, 0.5f);
        backBtnRect.pivot = new Vector2(0, 0.5f);
        backBtnRect.sizeDelta = new Vector2(50, 50);
        backBtnRect.anchoredPosition = new Vector2(10, 0);

        Image backBtnImg = backBtn.AddComponent<Image>();
        backBtnImg.color = Color.clear;

        Button backBtnComp = backBtn.AddComponent<Button>();
        backBtnComp.targetGraphic = backBtnImg;

        GameObject backIcon = new GameObject("Icon");
        backIcon.transform.SetParent(backBtn.transform, false);
        RectTransform backIconRect = backIcon.AddComponent<RectTransform>();
        backIconRect.anchorMin = Vector2.zero;
        backIconRect.anchorMax = Vector2.one;
        backIconRect.offsetMin = Vector2.zero;
        backIconRect.offsetMax = Vector2.zero;

        Text backIconText = backIcon.AddComponent<Text>();
        backIconText.text = "<";
        backIconText.font = font;
        backIconText.fontSize = 28;
        backIconText.color = Color.white;
        backIconText.alignment = TextAnchor.MiddleCenter;

        // 제목 (사용자명)
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(header.transform, false);

        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(60, 0);
        titleRect.offsetMax = new Vector2(-60, 0);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "username";
        titleText.font = font;
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;

        return header;
    }

    /// <summary>
    /// TabBar 생성 (팔로잉 | 팔로워 탭)
    /// </summary>
    private static GameObject CreateTabBar(Transform parent, Font font, Color bgColor)
    {
        GameObject tabBar = new GameObject("TabBar");
        Undo.RegisterCreatedObjectUndo(tabBar, "Create TabBar");
        tabBar.transform.SetParent(parent, false);

        RectTransform tabBarRect = tabBar.AddComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0, 1);
        tabBarRect.anchorMax = new Vector2(1, 1);
        tabBarRect.pivot = new Vector2(0.5f, 1);
        tabBarRect.sizeDelta = new Vector2(0, 50);
        tabBarRect.anchoredPosition = new Vector2(0, -60);

        Image tabBarBg = tabBar.AddComponent<Image>();
        tabBarBg.color = bgColor;

        HorizontalLayoutGroup tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabHlg.spacing = 0;
        tabHlg.padding = new RectOffset(20, 20, 0, 0);
        tabHlg.childAlignment = TextAnchor.MiddleCenter;
        tabHlg.childForceExpandWidth = true;
        tabHlg.childForceExpandHeight = true;

        // 팔로잉 탭 (왼쪽)
        CreateInstagramTabButton("FollowingTab", tabBar.transform, "팔로잉", font, true);
        // 팔로워 탭 (오른쪽)
        CreateInstagramTabButton("FollowersTab", tabBar.transform, "팔로워", font, false);

        return tabBar;
    }

    /// <summary>
    /// SearchBar 생성
    /// </summary>
    private static GameObject CreateSearchBar(Transform parent, Font font)
    {
        Color searchBarColor = new Color(0.15f, 0.15f, 0.18f, 1f);

        GameObject searchBar = new GameObject("SearchBar");
        Undo.RegisterCreatedObjectUndo(searchBar, "Create SearchBar");
        searchBar.transform.SetParent(parent, false);

        RectTransform searchBarRect = searchBar.AddComponent<RectTransform>();
        searchBarRect.anchorMin = new Vector2(0, 1);
        searchBarRect.anchorMax = new Vector2(1, 1);
        searchBarRect.pivot = new Vector2(0.5f, 1);
        searchBarRect.sizeDelta = new Vector2(-30, 45);
        searchBarRect.anchoredPosition = new Vector2(0, -120);

        Image searchBarBg = searchBar.AddComponent<Image>();
        searchBarBg.color = searchBarColor;

        // 검색 아이콘
        GameObject searchIcon = new GameObject("SearchIcon");
        searchIcon.transform.SetParent(searchBar.transform, false);

        RectTransform searchIconRect = searchIcon.AddComponent<RectTransform>();
        searchIconRect.anchorMin = new Vector2(0, 0.5f);
        searchIconRect.anchorMax = new Vector2(0, 0.5f);
        searchIconRect.pivot = new Vector2(0, 0.5f);
        searchIconRect.sizeDelta = new Vector2(30, 30);
        searchIconRect.anchoredPosition = new Vector2(15, 0);

        Text searchIconText = searchIcon.AddComponent<Text>();
        searchIconText.text = "Q";
        searchIconText.font = font;
        searchIconText.fontSize = 18;
        searchIconText.color = new Color(0.5f, 0.5f, 0.55f, 1f);
        searchIconText.alignment = TextAnchor.MiddleCenter;

        // 검색 입력 필드
        GameObject searchInputObj = new GameObject("SearchInput");
        searchInputObj.transform.SetParent(searchBar.transform, false);

        RectTransform searchInputRect = searchInputObj.AddComponent<RectTransform>();
        searchInputRect.anchorMin = new Vector2(0, 0);
        searchInputRect.anchorMax = new Vector2(1, 1);
        searchInputRect.offsetMin = new Vector2(45, 5);
        searchInputRect.offsetMax = new Vector2(-10, -5);

        // Placeholder
        GameObject placeholder = new GameObject("Placeholder");
        placeholder.transform.SetParent(searchInputObj.transform, false);

        RectTransform phRect = placeholder.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;

        Text phText = placeholder.AddComponent<Text>();
        phText.text = "검색";
        phText.font = font;
        phText.fontSize = 18;
        phText.color = new Color(0.5f, 0.5f, 0.55f, 1f);
        phText.alignment = TextAnchor.MiddleLeft;

        // Input Text
        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(searchInputObj.transform, false);

        RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = Vector2.zero;
        inputTextRect.offsetMax = Vector2.zero;

        Text inputText = inputTextObj.AddComponent<Text>();
        inputText.font = font;
        inputText.fontSize = 18;
        inputText.color = Color.white;
        inputText.alignment = TextAnchor.MiddleLeft;

        InputField searchInput = searchInputObj.AddComponent<InputField>();
        searchInput.textComponent = inputText;
        searchInput.placeholder = phText;

        return searchBar;
    }

    /// <summary>
    /// ContentArea 생성
    /// </summary>
    private static GameObject CreateContentArea(Transform parent, Font font, Color bgColor)
    {
        GameObject contentArea = new GameObject("ContentArea");
        Undo.RegisterCreatedObjectUndo(contentArea, "Create ContentArea");
        contentArea.transform.SetParent(parent, false);

        RectTransform contentAreaRect = contentArea.AddComponent<RectTransform>();
        contentAreaRect.anchorMin = new Vector2(0, 0);
        contentAreaRect.anchorMax = new Vector2(1, 1);
        contentAreaRect.offsetMin = new Vector2(0, 0);
        contentAreaRect.offsetMax = new Vector2(0, -175); // 헤더+탭+검색바 높이

        return contentArea;
    }

    /// <summary>
    /// 리스트 페이지 생성 (세로 스크롤)
    /// </summary>
    private static GameObject CreateListPage(string name, Transform parent, Font font)
    {
        GameObject page = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(page, $"Create {name}");
        page.transform.SetParent(parent, false);

        RectTransform pageRect = page.AddComponent<RectTransform>();
        pageRect.anchorMin = Vector2.zero;
        pageRect.anchorMax = Vector2.one;
        pageRect.offsetMin = Vector2.zero;
        pageRect.offsetMax = Vector2.zero;

        ScrollRect pageSr = page.AddComponent<ScrollRect>();
        pageSr.horizontal = false;
        pageSr.vertical = true;
        pageSr.movementType = ScrollRect.MovementType.Elastic;
        pageSr.elasticity = 0.1f;

        Image pageBg = page.AddComponent<Image>();
        pageBg.color = new Color(0.07f, 0.07f, 0.09f, 1f);

        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(page.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        viewport.AddComponent<Image>().color = Color.clear;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0;
        vlg.padding = new RectOffset(0, 0, 10, 10);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        pageSr.viewport = viewportRect;
        pageSr.content = contentRect;

        return page;
    }

    /// <summary>
    /// 아이템 프리팹 존재 확인 및 생성
    /// </summary>
    private static void EnsurePrefabsExist(ProfileManager profileManager)
    {
        if (profileManager.followingItemPrefab == null)
        {
            Debug.Log("[DMSetup] FollowingItem 프리팹 생성 중...");
            CreateFollowingItemPrefab(profileManager);
        }

        if (profileManager.followerItemPrefab == null)
        {
            Debug.Log("[DMSetup] FollowerItem 프리팹 생성 중...");
            CreateFollowerItemPrefab(profileManager);
        }
    }

    /// <summary>
    /// 탭 텍스트 업데이트 (숫자 제거, "팔로잉", "팔로워"만)
    /// </summary>
    private static void UpdateTabTexts(GameObject panel)
    {
        Transform followersTabText = panel.transform.Find("TabBar/FollowersTab/Text");
        Transform followingTabText = panel.transform.Find("TabBar/FollowingTab/Text");

        if (followersTabText != null)
        {
            Text text = followersTabText.GetComponent<Text>();
            if (text != null)
            {
                Undo.RecordObject(text, "Update tab text");
                text.text = "팔로워";
            }
        }

        if (followingTabText != null)
        {
            Text text = followingTabText.GetComponent<Text>();
            if (text != null)
            {
                Undo.RecordObject(text, "Update tab text");
                text.text = "팔로잉";
            }
        }
    }

    /// <summary>
    /// 탭 순서 변경 (팔로잉을 왼쪽으로)
    /// </summary>
    private static void ReorderTabs(GameObject panel)
    {
        Transform tabBar = panel.transform.Find("TabBar");
        if (tabBar == null) return;

        Transform followingTab = tabBar.Find("FollowingTab");
        Transform followersTab = tabBar.Find("FollowersTab");

        if (followingTab != null && followersTab != null)
        {
            // 팔로잉 탭을 첫번째로
            Undo.RecordObject(followingTab, "Reorder tabs");
            followingTab.SetSiblingIndex(0);

            // 팔로잉 탭 활성화 스타일
            Text followingText = followingTab.Find("Text")?.GetComponent<Text>();
            if (followingText != null)
            {
                followingText.fontStyle = FontStyle.Bold;
                followingText.color = Color.white;
            }
            Image followingIndicator = followingTab.Find("Indicator")?.GetComponent<Image>();
            if (followingIndicator != null)
                followingIndicator.color = Color.white;

            // 팔로워 탭 비활성화 스타일
            Text followersText = followersTab.Find("Text")?.GetComponent<Text>();
            if (followersText != null)
            {
                followersText.fontStyle = FontStyle.Normal;
                followersText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            }
            Image followersIndicator = followersTab.Find("Indicator")?.GetComponent<Image>();
            if (followersIndicator != null)
                followersIndicator.color = Color.clear;
        }
    }

    /// <summary>
    /// 스와이프 관련 참조 재연결
    /// </summary>
    private static void ReconnectSwipeReferences(ProfileManager profileManager, GameObject panel)
    {
        Undo.RecordObject(profileManager, "Reconnect swipe references");

        // 헤더 연결
        profileManager.followListTitleText = panel.transform.Find("Header/TitleText")?.GetComponent<Text>();
        profileManager.followListBackButton = panel.transform.Find("Header/BackButton")?.GetComponent<Button>();

        // 검색바 연결
        profileManager.followListSearchInput = panel.transform.Find("SearchBar/SearchInput")?.GetComponent<InputField>();

        // 스와이프 핸들러 연결
        profileManager.swipePageHandler = panel.transform.Find("ContentArea")?.GetComponent<SwipePageHandler>();
        profileManager.followListSlideScrollRect = panel.transform.Find("ContentArea")?.GetComponent<ScrollRect>();

        // 페이지 연결 (스와이프 구조)
        profileManager.followingPage = panel.transform.Find("ContentArea/SwipeViewport/SwipeContent/FollowingPage")?.gameObject;
        profileManager.followersPage = panel.transform.Find("ContentArea/SwipeViewport/SwipeContent/FollowersPage")?.gameObject;

        // Content 연결
        profileManager.followingListContent = panel.transform.Find("ContentArea/SwipeViewport/SwipeContent/FollowingPage/Viewport/Content");
        profileManager.followersListContent = panel.transform.Find("ContentArea/SwipeViewport/SwipeContent/FollowersPage/Viewport/Content");

        // 탭 버튼 재연결
        profileManager.followingTabButton = panel.transform.Find("TabBar/FollowingTab")?.GetComponent<Button>();
        profileManager.followersTabButton = panel.transform.Find("TabBar/FollowersTab")?.GetComponent<Button>();
        profileManager.followingTabText = panel.transform.Find("TabBar/FollowingTab/Text")?.GetComponent<Text>();
        profileManager.followersTabText = panel.transform.Find("TabBar/FollowersTab/Text")?.GetComponent<Text>();
        profileManager.followingTabIndicator = panel.transform.Find("TabBar/FollowingTab/Indicator")?.gameObject;
        profileManager.followersTabIndicator = panel.transform.Find("TabBar/FollowersTab/Indicator")?.gameObject;

        EditorUtility.SetDirty(profileManager);
    }

    /// <summary>
    /// 메뉴에서 강제 재생성 (스와이프 팔로우 리스트 패널)
    /// </summary>
    [MenuItem("Tools/Woopang/Recreate Follow List Panel (Swipe)")]
    public static void ForceRecreateFollowListPanel()
    {
        Debug.Log("[DMSetup] 강제 재생성 시작...");
        ProfileManager profileManager = Object.FindFirstObjectByType<ProfileManager>();
        if (profileManager != null)
        {
            // 기존 패널 삭제
            if (profileManager.followListPanel != null)
            {
                Object.DestroyImmediate(profileManager.followListPanel);
                profileManager.followListPanel = null;
            }
            CreateFollowListPanel();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[DMSetup] 강제 재생성 완료!");
        }
        else
        {
            Debug.LogError("[DMSetup] ProfileManager를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 팔로우 리스트 아이템 프리팹 연결 (기존 프리팹 사용)
    /// </summary>
    [MenuItem("Tools/Woopang/Connect Follow Item Prefabs")]
    public static void ConnectFollowItemPrefabs()
    {
        ProfileManager profileManager = Object.FindFirstObjectByType<ProfileManager>();
        if (profileManager == null)
        {
            Debug.LogError("[DMSetup] ProfileManager를 찾을 수 없습니다!");
            return;
        }

        bool changed = false;

        // FollowingItem 프리팹 연결
        if (profileManager.followingItemPrefab == null)
        {
            GameObject followingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Profile/FollowingItem.prefab");
            if (followingPrefab != null)
            {
                profileManager.followingItemPrefab = followingPrefab;
                Debug.Log("[DMSetup] FollowingItem 프리팹 연결 완료");
                changed = true;
            }
            else
            {
                Debug.LogWarning("[DMSetup] FollowingItem.prefab을 찾을 수 없습니다. 경로: Assets/Resources/Prefabs/Profile/FollowingItem.prefab");
            }
        }
        else
        {
            Debug.Log("[DMSetup] FollowingItem 프리팹 이미 연결됨");
        }

        // FollowerItem 프리팹 연결
        if (profileManager.followerItemPrefab == null)
        {
            GameObject followerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Profile/FollowerItem.prefab");
            if (followerPrefab != null)
            {
                profileManager.followerItemPrefab = followerPrefab;
                Debug.Log("[DMSetup] FollowerItem 프리팹 연결 완료");
                changed = true;
            }
            else
            {
                Debug.LogWarning("[DMSetup] FollowerItem.prefab을 찾을 수 없습니다. 경로: Assets/Resources/Prefabs/Profile/FollowerItem.prefab");
            }
        }
        else
        {
            Debug.Log("[DMSetup] FollowerItem 프리팹 이미 연결됨");
        }

        if (changed)
        {
            EditorUtility.SetDirty(profileManager);
            EditorSceneManager.MarkSceneDirty(profileManager.gameObject.scene);
            Debug.Log("[DMSetup] 프리팹 연결 완료! 씬을 저장하세요.");
        }
        else
        {
            Debug.Log("[DMSetup] 모든 프리팹이 이미 연결되어 있습니다.");
        }
    }

    /// <summary>
    /// FollowListPanel 구조 수정 (SwipeViewport 투명화 등)
    /// </summary>
    [MenuItem("Tools/Woopang/Repair Follow List Panel")]
    public static void RepairFollowListPanel()
    {
        ProfileManager profileManager = Object.FindFirstObjectByType<ProfileManager>();
        if (profileManager == null || profileManager.followListPanel == null)
        {
            Debug.LogError("[DMSetup] ProfileManager 또는 FollowListPanel을 찾을 수 없습니다!");
            return;
        }

        bool changed = false;
        GameObject panel = profileManager.followListPanel;

        // SwipeViewport 수정
        Transform swipeViewport = panel.transform.Find("ContentArea/SwipeViewport");
        if (swipeViewport != null)
        {
            // Image 컴포넌트 수정 - 투명하게
            Image vpImage = swipeViewport.GetComponent<Image>();
            if (vpImage != null)
            {
                vpImage.sprite = null;
                vpImage.color = Color.clear;
                Debug.Log("[DMSetup] SwipeViewport Image를 투명하게 설정");
                changed = true;
            }

            // Mask 컴포넌트 수정
            Mask vpMask = swipeViewport.GetComponent<Mask>();
            if (vpMask != null)
            {
                vpMask.showMaskGraphic = false;
                Debug.Log("[DMSetup] SwipeViewport Mask showMaskGraphic = false");
                changed = true;
            }

            // RectTransform 확인
            RectTransform vpRect = swipeViewport.GetComponent<RectTransform>();
            if (vpRect != null)
            {
                vpRect.anchorMin = Vector2.zero;
                vpRect.anchorMax = Vector2.one;
                vpRect.offsetMin = Vector2.zero;
                vpRect.offsetMax = Vector2.zero;
                Debug.Log("[DMSetup] SwipeViewport RectTransform 전체 채우기로 설정");
            }
        }
        else
        {
            Debug.LogWarning("[DMSetup] SwipeViewport를 찾을 수 없습니다!");
        }

        // FollowersPage, FollowingPage의 Viewport도 확인
        string[] pageNames = { "FollowersPage", "FollowingPage" };
        foreach (string pageName in pageNames)
        {
            Transform pageViewport = panel.transform.Find($"ContentArea/SwipeViewport/SwipeContent/{pageName}/Viewport");
            if (pageViewport != null)
            {
                Image pageVpImage = pageViewport.GetComponent<Image>();
                if (pageVpImage != null)
                {
                    pageVpImage.sprite = null;
                    pageVpImage.color = Color.clear;
                    changed = true;
                }

                Mask pageVpMask = pageViewport.GetComponent<Mask>();
                if (pageVpMask != null)
                {
                    pageVpMask.showMaskGraphic = false;
                    changed = true;
                }
                Debug.Log($"[DMSetup] {pageName}/Viewport 수정 완료");
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(panel.scene);
            Debug.Log("[DMSetup] FollowListPanel 수정 완료! 씬을 저장하세요.");
        }
        else
        {
            Debug.Log("[DMSetup] 수정할 항목이 없습니다.");
        }
    }


    /// <summary>
    /// FollowManager UI 생성 (새로운 독립적인 팔로우 리스트 시스템)
    /// </summary>
    [MenuItem("Tools/Woopang/Create FollowManager UI")]
    public static void CreateFollowManagerUI()
    {
        Debug.Log("[DMSetup] ========== FollowManager UI 생성 시작 ==========");

        // Canvas 찾기
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[DMSetup] Canvas를 찾을 수 없습니다!");
            return;
        }

        // 기존 FollowManager 찾기 또는 생성
        FollowManager manager = Object.FindFirstObjectByType<FollowManager>();
        if (manager == null)
        {
            GameObject managerObj = new GameObject("FollowManager");
            Undo.RegisterCreatedObjectUndo(managerObj, "Create FollowManager");
            manager = managerObj.AddComponent<FollowManager>();
            Debug.Log("[DMSetup] FollowManager 오브젝트 생성");
        }

        // 기존 패널 삭제
        if (manager.panel != null)
        {
            Undo.DestroyObjectImmediate(manager.panel);
        }

        // 색상 정의
        Color bgColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        Color headerColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        Color itemBgColor = new Color(0.15f, 0.15f, 0.18f, 1f);
        Color textColor = Color.white;
        Color grayText = new Color(0.6f, 0.6f, 0.6f, 1f);
        Color pinkColor = new Color(0.91f, 0.33f, 0.51f, 1f);

        // === 메인 패널 ===
        GameObject panel = new GameObject("FollowPanel");
        Undo.RegisterCreatedObjectUndo(panel, "Create FollowPanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = bgColor;

        // === Header (상단 바) ===
        GameObject header = new GameObject("Header");
        header.transform.SetParent(panel.transform, false);

        RectTransform headerRect = header.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, 100);

        Image headerBg = header.AddComponent<Image>();
        headerBg.color = headerColor;

        // Close Button
        GameObject closeBtn = new GameObject("CloseButton");
        closeBtn.transform.SetParent(header.transform, false);

        RectTransform closeBtnRect = closeBtn.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(0, 0.5f);
        closeBtnRect.anchorMax = new Vector2(0, 0.5f);
        closeBtnRect.pivot = new Vector2(0, 0.5f);
        closeBtnRect.anchoredPosition = new Vector2(15, 0);
        closeBtnRect.sizeDelta = new Vector2(50, 50);

        Image closeBtnImg = closeBtn.AddComponent<Image>();
        closeBtnImg.color = Color.clear;

        Button closeBtnComp = closeBtn.AddComponent<Button>();
        closeBtnComp.targetGraphic = closeBtnImg;

        // Close Button Text (X)
        GameObject closeBtnText = new GameObject("Text");
        closeBtnText.transform.SetParent(closeBtn.transform, false);

        RectTransform closeBtnTextRect = closeBtnText.AddComponent<RectTransform>();
        closeBtnTextRect.anchorMin = Vector2.zero;
        closeBtnTextRect.anchorMax = Vector2.one;
        closeBtnTextRect.offsetMin = Vector2.zero;
        closeBtnTextRect.offsetMax = Vector2.zero;

        Text closeBtnTxt = closeBtnText.AddComponent<Text>();
        closeBtnTxt.text = "<";
        closeBtnTxt.fontSize = 30;
        closeBtnTxt.color = textColor;
        closeBtnTxt.alignment = TextAnchor.MiddleCenter;
        closeBtnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Title Text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(header.transform, false);

        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(300, 50);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "Username";
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = textColor;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // === Tab Bar ===
        GameObject tabBar = new GameObject("TabBar");
        tabBar.transform.SetParent(panel.transform, false);

        RectTransform tabBarRect = tabBar.AddComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0, 1);
        tabBarRect.anchorMax = new Vector2(1, 1);
        tabBarRect.pivot = new Vector2(0.5f, 1);
        tabBarRect.anchoredPosition = new Vector2(0, -100);
        tabBarRect.sizeDelta = new Vector2(0, 60);

        HorizontalLayoutGroup tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabHlg.childAlignment = TextAnchor.MiddleCenter;
        tabHlg.childForceExpandWidth = true;
        tabHlg.childForceExpandHeight = true;

        // Followers Tab
        GameObject followersTabObj = CreateTab(tabBar.transform, "FollowersTab", "팔로워", textColor);
        Button followersTabBtn = followersTabObj.GetComponent<Button>();
        Image followersTabLine = followersTabObj.transform.Find("Line").GetComponent<Image>();
        followersTabLine.color = pinkColor;

        // Following Tab
        GameObject followingTabObj = CreateTab(tabBar.transform, "FollowingTab", "팔로잉", grayText);
        Button followingTabBtn = followingTabObj.GetComponent<Button>();
        Image followingTabLine = followingTabObj.transform.Find("Line").GetComponent<Image>();
        followingTabLine.color = Color.clear;

        // === Scroll Area ===
        GameObject scrollArea = new GameObject("ScrollArea");
        scrollArea.transform.SetParent(panel.transform, false);

        RectTransform scrollAreaRect = scrollArea.AddComponent<RectTransform>();
        scrollAreaRect.anchorMin = Vector2.zero;
        scrollAreaRect.anchorMax = Vector2.one;
        scrollAreaRect.offsetMin = new Vector2(0, 0);
        scrollAreaRect.offsetMax = new Vector2(0, -160); // Header + TabBar

        ScrollRect scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = Color.clear;
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        scrollRect.viewport = viewportRect;

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
        contentVlg.spacing = 2;
        contentVlg.padding = new RectOffset(0, 0, 10, 10);
        contentVlg.childAlignment = TextAnchor.UpperCenter;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = false;
        contentVlg.childControlWidth = true;
        contentVlg.childControlHeight = false;

        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;

        // === Item Template ===
        GameObject itemTemplate = CreateItemTemplate(content.transform, itemBgColor, textColor);
        itemTemplate.SetActive(false);

        // === Unfollow Confirm Dialog ===
        GameObject unfollowDialog = CreateUnfollowConfirmDialog(panel.transform, bgColor, textColor, pinkColor);

        // === Manager 연결 ===
        manager.panel = panel;
        manager.titleText = titleText;
        manager.closeButton = closeBtnComp;
        manager.followersTab = followersTabBtn;
        manager.followingTab = followingTabBtn;
        manager.followersTabLine = followersTabLine;
        manager.followingTabLine = followingTabLine;
        manager.scrollRect = scrollRect;
        manager.contentParent = content.transform;
        manager.itemTemplate = itemTemplate;

        // 프리팹 연결 (프리팹 수정 시 레이아웃 변경 가능)
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FollowListItem.prefab");
        if (prefab != null)
            manager.itemPrefab = prefab;

        // 언팔로우 확인 다이얼로그 연결
        manager.unfollowConfirmDialog = unfollowDialog;
        manager.unfollowConfirmText = unfollowDialog.transform.Find("DialogBox/ConfirmText")?.GetComponent<Text>();
        manager.unfollowConfirmButton = unfollowDialog.transform.Find("DialogBox/ConfirmButton")?.GetComponent<Button>();
        manager.unfollowCancelButton = unfollowDialog.transform.Find("DialogBox/CancelButton")?.GetComponent<Button>();

        // 패널 비활성화
        panel.SetActive(false);

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        Debug.Log("[DMSetup] FollowManager UI 생성 완료!");
        Debug.Log("[DMSetup] 테스트: Play 모드에서 Tools > Woopang > Test FollowManager 실행");
    }

    /// <summary>
    /// FollowManager 테스트 (Play 모드에서만 동작)
    /// </summary>
    [MenuItem("Tools/Woopang/Test FollowManager")]
    public static void TestFollowManager()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DMSetup] Play 모드에서만 테스트 가능합니다!");
            return;
        }

        FollowManager manager = Object.FindFirstObjectByType<FollowManager>();
        if (manager == null)
        {
            Debug.LogError("[DMSetup] FollowManager를 찾을 수 없습니다! 먼저 'Create FollowManager UI'를 실행하세요.");
            return;
        }

        // 테스트 팔로워 목록 표시
        manager.ShowFollowers("test_user", "테스트유저");
        Debug.Log("[DMSetup] FollowManager 테스트 시작 - 팔로워 목록 표시");
    }

    /// <summary>
    /// 탭 버튼 생성
    /// </summary>
    private static GameObject CreateTab(Transform parent, string name, string text, Color textColor)
    {
        GameObject tab = new GameObject(name);
        tab.transform.SetParent(parent, false);

        RectTransform tabRect = tab.AddComponent<RectTransform>();
        tabRect.sizeDelta = new Vector2(0, 60);

        Image tabBg = tab.AddComponent<Image>();
        tabBg.color = Color.clear;

        Button tabBtn = tab.AddComponent<Button>();
        tabBtn.targetGraphic = tabBg;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(tab.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(0, 4);
        textRect.offsetMax = new Vector2(0, 0);

        Text txt = textObj.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = 20;
        txt.color = textColor;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Underline
        GameObject line = new GameObject("Line");
        line.transform.SetParent(tab.transform, false);

        RectTransform lineRect = line.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.2f, 0);
        lineRect.anchorMax = new Vector2(0.8f, 0);
        lineRect.pivot = new Vector2(0.5f, 0);
        lineRect.sizeDelta = new Vector2(0, 3);

        Image lineImg = line.AddComponent<Image>();
        lineImg.color = Color.clear;

        return tab;
    }

    /// <summary>
    /// 언팔로우 확인 다이얼로그 생성
    /// </summary>
    private static GameObject CreateUnfollowConfirmDialog(Transform parent, Color bgColor, Color textColor, Color accentColor)
    {
        // 반투명 오버레이 + 다이얼로그 박스
        GameObject dialog = new GameObject("UnfollowConfirmDialog");
        dialog.transform.SetParent(parent, false);

        RectTransform dialogRect = dialog.AddComponent<RectTransform>();
        dialogRect.anchorMin = Vector2.zero;
        dialogRect.anchorMax = Vector2.one;
        dialogRect.offsetMin = Vector2.zero;
        dialogRect.offsetMax = Vector2.zero;

        // 반투명 배경 (터치 차단용)
        Image overlayBg = dialog.AddComponent<Image>();
        overlayBg.color = new Color(0, 0, 0, 0.7f);

        // 다이얼로그 박스
        GameObject dialogBox = new GameObject("DialogBox");
        dialogBox.transform.SetParent(dialog.transform, false);

        RectTransform boxRect = dialogBox.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(600, 350);

        Image boxBg = dialogBox.AddComponent<Image>();
        boxBg.color = new Color(0.18f, 0.18f, 0.22f, 1f);

        // 메시지 텍스트
        GameObject confirmTextObj = new GameObject("ConfirmText");
        confirmTextObj.transform.SetParent(dialogBox.transform, false);

        RectTransform confirmTextRect = confirmTextObj.AddComponent<RectTransform>();
        confirmTextRect.anchorMin = new Vector2(0, 0.5f);
        confirmTextRect.anchorMax = new Vector2(1, 1);
        confirmTextRect.offsetMin = new Vector2(30, 20);
        confirmTextRect.offsetMax = new Vector2(-30, -30);

        Text confirmText = confirmTextObj.AddComponent<Text>();
        confirmText.text = "Username\n팔로우를 취소하시겠습니까?";
        confirmText.fontSize = 40;
        confirmText.color = textColor;
        confirmText.alignment = TextAnchor.MiddleCenter;
        // 폰트는 아래에서 일괄 적용

        // 버튼 컨테이너
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(dialogBox.transform, false);

        RectTransform btnContainerRect = buttonContainer.AddComponent<RectTransform>();
        btnContainerRect.anchorMin = new Vector2(0, 0);
        btnContainerRect.anchorMax = new Vector2(1, 0.4f);
        btnContainerRect.offsetMin = new Vector2(30, 30);
        btnContainerRect.offsetMax = new Vector2(-30, -10);

        HorizontalLayoutGroup hlg = buttonContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // 취소 버튼 (회색)
        GameObject cancelBtn = CreateDialogButton(buttonContainer.transform, "CancelButton", "취소",
            new Color(0.35f, 0.35f, 0.4f, 1f), textColor);

        // 확인 버튼 (핑크/빨강)
        GameObject confirmBtn = CreateDialogButton(buttonContainer.transform, "ConfirmButton", "확인",
            new Color(0.9f, 0.3f, 0.35f, 1f), textColor);

        // 다른 UI 위에 표시되도록 맨 앞으로
        dialog.transform.SetAsLastSibling();

        // 커스텀 폰트 적용
        Font customFont = GetDefaultFont();
        if (customFont != null)
        {
            confirmText.font = customFont;
            // 버튼 텍스트에도 적용
            Text cancelBtnText = cancelBtn.GetComponentInChildren<Text>();
            Text confirmBtnText = confirmBtn.GetComponentInChildren<Text>();
            if (cancelBtnText != null) cancelBtnText.font = customFont;
            if (confirmBtnText != null) confirmBtnText.font = customFont;
        }

        dialog.SetActive(false);
        return dialog;
    }

    /// <summary>
    /// 다이얼로그 버튼 생성
    /// </summary>
    private static GameObject CreateDialogButton(Transform parent, string name, string text, Color bgColor, Color textColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(200, 80);

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(bgColor.r + 0.1f, bgColor.g + 0.1f, bgColor.b + 0.1f, 1f);
        colors.pressedColor = new Color(bgColor.r - 0.1f, bgColor.g - 0.1f, bgColor.b - 0.1f, 1f);
        btn.colors = colors;

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = 80;
        le.flexibleWidth = 1;

        // 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text btnText = textObj.AddComponent<Text>();
        btnText.text = text;
        btnText.fontSize = 36;
        btnText.color = textColor;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.font = GetDefaultFont();

        return btnObj;
    }

    /// <summary>
    /// 아이템 템플릿 생성
    /// </summary>
    private static GameObject CreateItemTemplate(Transform parent, Color bgColor, Color textColor)
    {
        // 높이 2배 (80 -> 160)
        float itemHeight = 160f;
        float avatarSize = 100f;
        int fontSize = 28;
        Color buttonColor = new Color(0.35f, 0.45f, 0.95f, 1f); // 파란색 버튼

        GameObject item = new GameObject("ItemTemplate");
        item.transform.SetParent(parent, false);

        RectTransform itemRect = item.AddComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0, 1);
        itemRect.anchorMax = new Vector2(1, 1);
        itemRect.pivot = new Vector2(0.5f, 1);
        itemRect.sizeDelta = new Vector2(0, itemHeight);

        Image itemBg = item.AddComponent<Image>();
        itemBg.color = bgColor;

        Button itemBtn = item.AddComponent<Button>();
        itemBtn.targetGraphic = itemBg;
        ColorBlock colors = itemBtn.colors;
        colors.highlightedColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        colors.pressedColor = new Color(0.25f, 0.25f, 0.3f, 1f);
        itemBtn.colors = colors;

        LayoutElement itemLE = item.AddComponent<LayoutElement>();
        itemLE.minHeight = itemHeight;
        itemLE.preferredHeight = itemHeight;
        itemLE.flexibleWidth = 1;

        // Avatar (크기 2배)
        GameObject avatar = new GameObject("Avatar");
        avatar.transform.SetParent(item.transform, false);

        RectTransform avatarRect = avatar.AddComponent<RectTransform>();
        avatarRect.anchorMin = new Vector2(0, 0.5f);
        avatarRect.anchorMax = new Vector2(0, 0.5f);
        avatarRect.pivot = new Vector2(0, 0.5f);
        avatarRect.anchoredPosition = new Vector2(20, 0);
        avatarRect.sizeDelta = new Vector2(avatarSize, avatarSize);

        Image avatarImg = avatar.AddComponent<Image>();
        avatarImg.color = new Color(0.4f, 0.4f, 0.4f, 1f);

        // Avatar Mask (원형)
        Mask avatarMask = avatar.AddComponent<Mask>();
        avatarMask.showMaskGraphic = true;

        // Username (폰트 크기 증가)
        GameObject username = new GameObject("Username");
        username.transform.SetParent(item.transform, false);

        RectTransform usernameRect = username.AddComponent<RectTransform>();
        usernameRect.anchorMin = new Vector2(0, 0);
        usernameRect.anchorMax = new Vector2(1, 1);
        usernameRect.offsetMin = new Vector2(140, 0);  // 아바타 오른쪽
        usernameRect.offsetMax = new Vector2(-280, 0); // 버튼 왼쪽 여유

        Text usernameTxt = username.AddComponent<Text>();
        usernameTxt.text = "Username";
        usernameTxt.fontSize = fontSize;
        usernameTxt.color = textColor;
        usernameTxt.alignment = TextAnchor.MiddleLeft;
        usernameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ActionButton (우측 버튼 - 메시지/팔로우)
        GameObject actionBtn = new GameObject("ActionButton");
        actionBtn.transform.SetParent(item.transform, false);

        RectTransform actionBtnRect = actionBtn.AddComponent<RectTransform>();
        actionBtnRect.anchorMin = new Vector2(1, 0.5f);
        actionBtnRect.anchorMax = new Vector2(1, 0.5f);
        actionBtnRect.pivot = new Vector2(1, 0.5f);
        actionBtnRect.anchoredPosition = new Vector2(-145, 0);
        actionBtnRect.sizeDelta = new Vector2(120, 56);

        Image actionBtnImg = actionBtn.AddComponent<Image>();
        actionBtnImg.color = buttonColor;

        Button actionBtnComp = actionBtn.AddComponent<Button>();
        actionBtnComp.targetGraphic = actionBtnImg;
        ColorBlock actionColors = actionBtnComp.colors;
        actionColors.highlightedColor = new Color(0.45f, 0.55f, 1f, 1f);
        actionColors.pressedColor = new Color(0.25f, 0.35f, 0.85f, 1f);
        actionBtnComp.colors = actionColors;

        // ActionButton Text
        GameObject actionBtnTextObj = new GameObject("Text");
        actionBtnTextObj.transform.SetParent(actionBtn.transform, false);

        RectTransform actionBtnTextRect = actionBtnTextObj.AddComponent<RectTransform>();
        actionBtnTextRect.anchorMin = Vector2.zero;
        actionBtnTextRect.anchorMax = Vector2.one;
        actionBtnTextRect.offsetMin = Vector2.zero;
        actionBtnTextRect.offsetMax = Vector2.zero;

        Text actionBtnText = actionBtnTextObj.AddComponent<Text>();
        actionBtnText.text = "Follow";
        actionBtnText.fontSize = 20;
        actionBtnText.color = Color.white;
        actionBtnText.alignment = TextAnchor.MiddleCenter;
        actionBtnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // MessageButton (DM 버튼 - 팔로워 탭에서 사용, 팔로잉 탭에서는 숨김)
        GameObject msgBtn = new GameObject("MessageButton");
        msgBtn.transform.SetParent(item.transform, false);

        RectTransform msgBtnRect = msgBtn.AddComponent<RectTransform>();
        msgBtnRect.anchorMin = new Vector2(1, 0.5f);
        msgBtnRect.anchorMax = new Vector2(1, 0.5f);
        msgBtnRect.pivot = new Vector2(1, 0.5f);
        msgBtnRect.anchoredPosition = new Vector2(-15, 0);
        msgBtnRect.sizeDelta = new Vector2(120, 56);

        Image msgBtnImg = msgBtn.AddComponent<Image>();
        msgBtnImg.color = new Color(0.2f, 0.6f, 0.9f, 1f);

        Button msgBtnComp = msgBtn.AddComponent<Button>();
        msgBtnComp.targetGraphic = msgBtnImg;

        GameObject msgBtnTextObj = new GameObject("MessageText");
        msgBtnTextObj.transform.SetParent(msgBtn.transform, false);

        RectTransform msgBtnTextRect = msgBtnTextObj.AddComponent<RectTransform>();
        msgBtnTextRect.anchorMin = Vector2.zero;
        msgBtnTextRect.anchorMax = Vector2.one;
        msgBtnTextRect.offsetMin = Vector2.zero;
        msgBtnTextRect.offsetMax = Vector2.zero;

        Text msgBtnText = msgBtnTextObj.AddComponent<Text>();
        msgBtnText.text = "DM";
        msgBtnText.fontSize = 20;
        msgBtnText.color = Color.white;
        msgBtnText.alignment = TextAnchor.MiddleCenter;
        msgBtnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return item;
    }

    // 기존 프리팹 경로
    private const string MESSAGE_PANEL_PREFAB = "Assets/Prefab/MessagePanel.prefab";
    private const string CHAT_TEMPLATE_PREFAB = "Assets/Prefab/MessageChatTemplate.prefab";
    private const string DM_PREFAB_FOLDER = "Assets/Prefabs/DM";

    /// <summary>
    /// DM 원클릭 설정 (수동 호출용)
    /// </summary>
    public static void OneClickSetup()
    {
        Debug.Log("[DMSetup] ========== DM 원클릭 설정 시작 ==========");

        // 먼저 중복 정리
        CleanupDuplicates();

        // 1. Canvas 자동 찾기
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[DMSetup] Canvas를 찾을 수 없습니다! 씬에 Canvas가 있는지 확인하세요.");
            return;
        }
        Debug.Log($"[DMSetup] Canvas 발견: {canvas.name}");

        // 2. MessagePanelManager 생성 또는 찾기
        MessagePanelManager manager = SetupMessagePanelManager();
        if (manager == null)
        {
            Debug.LogError("[DMSetup] MessagePanelManager 설정 실패!");
            return;
        }

        // 3. 기존 MessagePanel 프리팹 씬에 배치
        GameObject messagePanel = PlaceMessagePanel(canvas);

        // 4. ChatRoomPanel 생성 (대화 상세 화면)
        GameObject chatRoomPanel = CreateChatRoomPanel(canvas);

        // 5. Manager에 참조 연결
        ConnectReferences(manager, messagePanel, chatRoomPanel);

        // 6. DM 프리팹 연결
        ConnectDMPrefabs(manager);

        // 씬 변경 표시
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[DMSetup] ========== DM 원클릭 설정 완료 ==========");

        EditorUtility.DisplayDialog("DM 설정 완료",
            "DM 시스템 설정이 완료되었습니다!\n\n" +
            "✓ MessagePanelManager 설정됨\n" +
            "✓ MessagePanel 배치됨\n" +
            "✓ ChatRoomPanel 생성됨\n" +
            "✓ DM 프리팹 연결됨\n" +
            "✓ 테스트 시스템 설정됨\n\n" +
            "씬을 저장하세요 (Ctrl+S)", "확인");
    }

    /// <summary>
    /// 중복된 DM 관련 오브젝트 정리
    /// </summary>
    public static void CleanupDuplicatesMenu()
    {
        int count = CleanupDuplicates();

        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("정리 완료",
                $"중복된 오브젝트 {count}개가 삭제되었습니다.\n\n씬을 저장하세요 (Ctrl+S)", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("정리 완료", "중복된 오브젝트가 없습니다.", "확인");
        }
    }

    /// <summary>
    /// 중복된 DM 관련 오브젝트 정리
    /// </summary>
    private static int CleanupDuplicates()
    {
        int deletedCount = 0;

        // 정리할 오브젝트 이름 목록
        string[] objectNames = { "MessagePanel", "ChatRoomPanel", "MessagePanelManager" };

        foreach (string objName in objectNames)
        {
            // 같은 이름의 모든 오브젝트 찾기
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            System.Collections.Generic.List<GameObject> duplicates = new System.Collections.Generic.List<GameObject>();

            foreach (GameObject obj in allObjects)
            {
                if (obj.name == objName)
                {
                    duplicates.Add(obj);
                }
            }

            // 2개 이상이면 첫번째만 남기고 삭제
            if (duplicates.Count > 1)
            {
                Debug.Log($"[DMSetup] '{objName}' {duplicates.Count}개 발견 - 중복 삭제");

                // 첫번째(가장 오래된 것)만 유지
                for (int i = 1; i < duplicates.Count; i++)
                {
                    Debug.Log($"[DMSetup] 삭제: {duplicates[i].name} (Instance ID: {duplicates[i].GetInstanceID()})");
                    Object.DestroyImmediate(duplicates[i]);
                    deletedCount++;
                }
            }
        }

        if (deletedCount > 0)
        {
            Debug.Log($"[DMSetup] 총 {deletedCount}개 중복 오브젝트 삭제됨");
        }

        return deletedCount;
    }

    #region Step 1: MessagePanelManager 설정

    private static MessagePanelManager SetupMessagePanelManager()
    {
        MessagePanelManager manager = Object.FindFirstObjectByType<MessagePanelManager>();

        if (manager == null)
        {
            GameObject managerObj = new GameObject("MessagePanelManager");
            manager = managerObj.AddComponent<MessagePanelManager>();
            Debug.Log("[DMSetup] MessagePanelManager 생성됨");
        }
        else
        {
            Debug.Log($"[DMSetup] 기존 MessagePanelManager 사용: {manager.gameObject.name}");
        }

        return manager;
    }

    #endregion

    #region Step 2: MessagePanel 배치

    private static GameObject PlaceMessagePanel(Canvas canvas)
    {
        // 이미 씬에 있는지 확인
        GameObject existing = GameObject.Find("MessagePanel");
        if (existing != null)
        {
            Debug.Log("[DMSetup] MessagePanel이 이미 씬에 존재함 - 레이아웃 수정");
            FixMessagePanelLayout(existing);
            existing.SetActive(false);
            return existing;
        }

        // 프리팹 로드
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MESSAGE_PANEL_PREFAB);
        if (prefab == null)
        {
            Debug.LogError($"[DMSetup] MessagePanel 프리팹을 찾을 수 없음: {MESSAGE_PANEL_PREFAB}");
            return null;
        }

        // 인스턴스 생성
        GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        panel.name = "MessagePanel";

        // 레이아웃 수정
        FixMessagePanelLayout(panel);

        panel.SetActive(false);
        Debug.Log("[DMSetup] MessagePanel 씬에 배치됨");

        return panel;
    }

    /// <summary>
    /// MessagePanel과 자식 요소들의 레이아웃을 화면 전체에 맞게 수정
    /// </summary>
    private static void FixMessagePanelLayout(GameObject panel)
    {
        // 1. MessagePanel 자체 - 전체 화면 stretch
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
        }

        // 2. MessagePage - 전체 화면 stretch (고정 크기에서 변경)
        Transform messagePage = panel.transform.Find("MessagePage");
        if (messagePage != null)
        {
            RectTransform pageRect = messagePage.GetComponent<RectTransform>();
            if (pageRect != null)
            {
                pageRect.anchorMin = Vector2.zero;
                pageRect.anchorMax = Vector2.one;
                pageRect.offsetMin = Vector2.zero;
                pageRect.offsetMax = Vector2.zero;
                pageRect.pivot = new Vector2(0.5f, 0.5f);
                Debug.Log("[DMSetup] MessagePage 레이아웃 수정됨 (stretch)");
            }

            // 3. Scroll View Back (배경 + 타이틀) - 전체 화면 stretch
            Transform scrollViewBack = messagePage.Find("Scroll View Back");
            if (scrollViewBack != null)
            {
                RectTransform backRect = scrollViewBack.GetComponent<RectTransform>();
                if (backRect != null)
                {
                    backRect.anchorMin = Vector2.zero;
                    backRect.anchorMax = Vector2.one;
                    backRect.offsetMin = Vector2.zero;
                    backRect.offsetMax = Vector2.zero;
                    Debug.Log("[DMSetup] Scroll View Back 레이아웃 수정됨 (stretch)");
                }

                // Title - 상단 고정
                Transform title = scrollViewBack.Find("Title");
                if (title != null)
                {
                    RectTransform titleRect = title.GetComponent<RectTransform>();
                    if (titleRect != null)
                    {
                        titleRect.anchorMin = new Vector2(0, 1);
                        titleRect.anchorMax = new Vector2(1, 1);
                        titleRect.pivot = new Vector2(0.5f, 1);
                        titleRect.anchoredPosition = new Vector2(0, -20);
                        titleRect.sizeDelta = new Vector2(0, 120);
                        Debug.Log("[DMSetup] Title 레이아웃 수정됨 (상단 고정)");
                    }
                }
            }

            // 4. Scroll View - 타이틀 아래부터 하단까지
            Transform scrollView = messagePage.Find("Scroll View");
            if (scrollView != null)
            {
                RectTransform svRect = scrollView.GetComponent<RectTransform>();
                if (svRect != null)
                {
                    svRect.anchorMin = Vector2.zero;
                    svRect.anchorMax = Vector2.one;
                    svRect.pivot = new Vector2(0.5f, 0.5f);
                    svRect.offsetMin = new Vector2(20, 20);      // 좌, 하 여백
                    svRect.offsetMax = new Vector2(-20, -160);   // 우, 상 여백 (타이틀 공간)
                    Debug.Log("[DMSetup] Scroll View 레이아웃 수정됨 (stretch with margins)");
                }

                // Viewport - 전체 stretch
                Transform viewport = scrollView.Find("Viewport");
                if (viewport != null)
                {
                    RectTransform vpRect = viewport.GetComponent<RectTransform>();
                    if (vpRect != null)
                    {
                        vpRect.anchorMin = Vector2.zero;
                        vpRect.anchorMax = Vector2.one;
                        vpRect.offsetMin = Vector2.zero;
                        vpRect.offsetMax = new Vector2(-20, 0); // 스크롤바 공간
                    }

                    // Content - 상단 정렬, 가로 stretch
                    Transform content = viewport.Find("Content");
                    if (content != null)
                    {
                        RectTransform contentRect = content.GetComponent<RectTransform>();
                        if (contentRect != null)
                        {
                            contentRect.anchorMin = new Vector2(0, 1);
                            contentRect.anchorMax = new Vector2(1, 1);
                            contentRect.pivot = new Vector2(0.5f, 1);
                            contentRect.anchoredPosition = Vector2.zero;
                            contentRect.sizeDelta = new Vector2(0, 0); // ContentSizeFitter가 조절
                        }

                        // VerticalLayoutGroup padding 조정
                        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
                        if (vlg != null)
                        {
                            vlg.padding = new RectOffset(20, 20, 20, 20);
                            vlg.spacing = 10;
                        }
                    }
                }

                // Scrollbar Vertical - 우측 고정
                Transform scrollbar = scrollView.Find("Scrollbar Vertical");
                if (scrollbar != null)
                {
                    RectTransform sbRect = scrollbar.GetComponent<RectTransform>();
                    if (sbRect != null)
                    {
                        sbRect.anchorMin = new Vector2(1, 0);
                        sbRect.anchorMax = new Vector2(1, 1);
                        sbRect.pivot = new Vector2(1, 0.5f);
                        sbRect.anchoredPosition = Vector2.zero;
                        sbRect.sizeDelta = new Vector2(20, 0);
                    }
                }
            }
        }

        // 5. CloseButton - 우측 상단 고정
        Transform closeButton = panel.transform.Find("CloseButton_MessageList");
        if (closeButton != null)
        {
            RectTransform btnRect = closeButton.GetComponent<RectTransform>();
            if (btnRect != null)
            {
                btnRect.anchorMin = new Vector2(1, 1);
                btnRect.anchorMax = new Vector2(1, 1);
                btnRect.pivot = new Vector2(1, 1);
                btnRect.anchoredPosition = new Vector2(-20, -20);
                btnRect.sizeDelta = new Vector2(60, 60);
                Debug.Log("[DMSetup] CloseButton 레이아웃 수정됨 (우측 상단)");
            }
        }

        Debug.Log("[DMSetup] MessagePanel 전체 레이아웃 수정 완료");
    }

    #endregion

    #region Step 3: ChatRoomPanel 생성

    private static GameObject CreateChatRoomPanel(Canvas canvas)
    {
        // 이미 존재하는지 확인
        GameObject existing = GameObject.Find("ChatRoomPanel");
        if (existing != null)
        {
            Debug.Log("[DMSetup] ChatRoomPanel이 이미 씬에 존재함");
            existing.SetActive(false);
            return existing;
        }

        // 대화방 패널 생성
        GameObject panel = new GameObject("ChatRoomPanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = Color.white;

        // 헤더 (60px)
        GameObject header = CreateUIElement("Header", panel.transform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 60));
        header.AddComponent<Image>().color = Color.white;

        // 뒤로가기 버튼
        GameObject backBtn = CreateButton("BackButton", header.transform, "<", 24);
        SetRect(backBtn, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(44, 44));
        backBtn.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        // 아바타
        GameObject avatar = CreateUIElement("Avatar", header.transform,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(40, 40));
        avatar.AddComponent<Image>().color = new Color(0.9f, 0.9f, 0.9f);

        // 타이틀
        GameObject title = CreateText("ChatTitle", header.transform, "WOOPANG", 17, FontStyle.Bold, new Color(1f, 0.84f, 0f, 1f)); // 골드색
        SetRect(title, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(200, 40));

        // 메시지 영역 (스크롤뷰)
        GameObject scrollView = CreateScrollView("MessageArea", panel.transform);
        RectTransform svRect = scrollView.GetComponent<RectTransform>();
        svRect.anchorMin = Vector2.zero;
        svRect.anchorMax = Vector2.one;
        svRect.offsetMin = new Vector2(0, 60);
        svRect.offsetMax = new Vector2(0, -60);

        // 입력 영역 (60px)
        GameObject inputArea = CreateUIElement("InputArea", panel.transform,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 60));
        inputArea.AddComponent<Image>().color = Color.white;

        // 메시지 입력 필드
        GameObject chatInput = CreateInputField("ChatInput", inputArea.transform, "메시지 입력...");
        RectTransform inputRect = chatInput.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0, 0.5f);
        inputRect.anchorMax = new Vector2(1, 0.5f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.sizeDelta = new Vector2(-100, 40);
        inputRect.anchoredPosition = new Vector2(-25, 0);

        // 전송 버튼
        GameObject sendBtn = CreateButton("SendButton", inputArea.transform, "→", 20);
        SetRect(sendBtn, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(44, 44));
        sendBtn.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);
        sendBtn.GetComponentInChildren<Text>().color = Color.white;

        panel.SetActive(false);
        Debug.Log("[DMSetup] ChatRoomPanel 생성됨");

        return panel;
    }

    #endregion

    #region Step 4: 참조 연결

    private static void ConnectReferences(MessagePanelManager manager, GameObject messagePanel, GameObject chatRoomPanel)
    {
        if (manager == null) return;

        // 메인 패널 연결
        manager.messagePanel = messagePanel;
        manager.chatRoomPanel = chatRoomPanel;

        // MessagePanel 내부 요소 연결
        if (messagePanel != null)
        {
            // Content 찾기 (재귀적으로)
            manager.conversationListContent = FindChildRecursive(messagePanel.transform, "Content");

            // SearchInput 찾기
            Transform searchInput = FindChildRecursive(messagePanel.transform, "SearchInput");
            if (searchInput != null)
                manager.searchInput = searchInput.GetComponent<InputField>();

            // SearchButton 찾기
            Transform searchBtn = FindChildRecursive(messagePanel.transform, "SearchButton");
            if (searchBtn != null)
                manager.searchButton = searchBtn.GetComponent<Button>();
        }

        // ChatRoomPanel 내부 요소 연결
        if (chatRoomPanel != null)
        {
            Transform chatPanel = chatRoomPanel.transform;

            // MessageArea/Viewport/Content
            Transform msgArea = chatPanel.Find("MessageArea");
            if (msgArea != null)
            {
                manager.chatMessageContent = FindChildRecursive(msgArea, "Content");
            }

            // ChatInput
            Transform chatInput = FindChildRecursive(chatPanel, "ChatInput");
            if (chatInput != null)
                manager.chatInput = chatInput.GetComponent<InputField>();

            // SendButton
            Transform sendBtn = FindChildRecursive(chatPanel, "SendButton");
            if (sendBtn != null)
                manager.sendButton = sendBtn.GetComponent<Button>();

            // BackButton
            Transform backBtn = FindChildRecursive(chatPanel, "BackButton");
            if (backBtn != null)
                manager.chatRoomBackButton = backBtn.GetComponent<Button>();

            // ChatTitle
            Transform titleTr = FindChildRecursive(chatPanel, "ChatTitle");
            if (titleTr != null)
                manager.chatRoomTitle = titleTr.GetComponent<Text>();

            // Avatar
            Transform avatarTr = chatPanel.Find("Header/Avatar");
            if (avatarTr != null)
                manager.chatRoomAvatar = avatarTr.GetComponent<Image>();

            // InputArea (시스템 알림에서 숨기기 위해)
            Transform inputAreaTr = chatPanel.Find("InputArea");
            if (inputAreaTr != null)
                manager.chatInputArea = inputAreaTr.gameObject;
        }

        EditorUtility.SetDirty(manager);
        Debug.Log("[DMSetup] Manager 참조 연결 완료");
    }

    #endregion

    #region Step 5: DM 프리팹 연결

    private static void ConnectDMPrefabs(MessagePanelManager manager)
    {
        if (manager == null) return;

        // DM 프리팹 폴더 확인 및 생성
        if (!AssetDatabase.IsValidFolder(DM_PREFAB_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "DM");
            Debug.Log("[DMSetup] DM 프리팹 폴더 생성됨");
        }

        // 프리팹 매핑
        string[] mappings = {
            "ConversationItem.prefab:conversationItemPrefab",
            "AdminNoticeItem.prefab:adminNoticePrefab",
            "MyMessageBubble.prefab:myMessageBubblePrefab",
            "OtherMessageBubble.prefab:otherMessageBubblePrefab",
            "AdminMessageBubble.prefab:adminMessageBubblePrefab",
            "SearchResultItem.prefab:searchResultItemPrefab",
            "HeartAnimation.prefab:heartAnimationPrefab"
        };

        SerializedObject so = new SerializedObject(manager);
        int connected = 0;
        int created = 0;

        foreach (string mapping in mappings)
        {
            string[] parts = mapping.Split(':');
            string fileName = parts[0];
            string propName = parts[1];

            string prefabPath = Path.Combine(DM_PREFAB_FOLDER, fileName);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            // 프리팹이 없으면 생성
            if (prefab == null)
            {
                prefab = CreateDMPrefab(fileName, prefabPath);
                if (prefab != null)
                    created++;
            }

            // 연결
            if (prefab != null)
            {
                SerializedProperty prop = so.FindProperty(propName);
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = prefab;
                    connected++;
                }
            }
        }

        // 기존 MessageChatTemplate도 연결 (ConversationItem 대용 가능)
        GameObject chatTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(CHAT_TEMPLATE_PREFAB);
        if (chatTemplate != null)
        {
            SerializedProperty convProp = so.FindProperty("conversationItemPrefab");
            if (convProp != null && convProp.objectReferenceValue == null)
            {
                // MessageChatTemplate를 ConversationItem 기본으로 사용 가능
                Debug.Log("[DMSetup] MessageChatTemplate 발견됨 - 필요시 ConversationItem으로 활용 가능");
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);

        Debug.Log($"[DMSetup] DM 프리팹: {created}개 생성, {connected}개 연결됨");
    }

    private static GameObject CreateDMPrefab(string fileName, string savePath)
    {
        GameObject obj = null;

        switch (fileName)
        {
            case "ConversationItem.prefab":
                obj = CreateConversationItemPrefab();
                break;
            case "AdminNoticeItem.prefab":
                obj = CreateAdminNoticePrefab();
                break;
            case "MyMessageBubble.prefab":
                obj = CreateMessageBubblePrefab("MyMessageBubble", true);
                break;
            case "OtherMessageBubble.prefab":
                obj = CreateMessageBubblePrefab("OtherMessageBubble", false);
                break;
            case "AdminMessageBubble.prefab":
                obj = CreateAdminMessageBubblePrefab();
                break;
            case "SearchResultItem.prefab":
                obj = CreateSearchResultItemPrefab();
                break;
            case "HeartAnimation.prefab":
                obj = CreateHeartAnimationPrefab();
                break;
        }

        if (obj != null)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, savePath);
            Object.DestroyImmediate(obj);
            Debug.Log($"[DMSetup] 프리팹 생성: {savePath}");
            return prefab;
        }

        return null;
    }

    #endregion

    #region DM 프리팹 생성 헬퍼

    private static GameObject CreateConversationItemPrefab()
    {
        GameObject obj = new GameObject("ConversationItem");
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 76);

        obj.AddComponent<Image>().color = Color.white;

        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.preferredHeight = 76;
        layout.flexibleWidth = 1;

        // Content (스와이프 대상)
        GameObject content = CreateUIElement("Content", obj.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        content.AddComponent<Image>().color = Color.white;

        // Avatar
        GameObject avatar = CreateUIElement("Avatar", content.transform,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(50, 50));
        avatar.AddComponent<Image>().color = new Color(0.93f, 0.93f, 0.93f);

        // UsernameText
        GameObject username = CreateText("UsernameText", content.transform, "사용자", 16, FontStyle.Bold, Color.black);
        SetRect(username, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(76, 10), new Vector2(200, 24));

        // PreviewText
        GameObject preview = CreateText("PreviewText", content.transform, "메시지 미리보기...", 14, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f));
        SetRect(preview, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(76, -12), new Vector2(-150, 20));

        // TimeText
        GameObject time = CreateText("TimeText", content.transform, "오후 2:30", 12, FontStyle.Normal, new Color(0.6f, 0.6f, 0.6f));
        SetRect(time, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 10), new Vector2(70, 20));
        time.GetComponent<Text>().alignment = TextAnchor.MiddleRight;

        // UnreadBadge
        GameObject badge = CreateUIElement("UnreadBadge", content.transform,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, -12), new Vector2(22, 22));
        badge.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);

        GameObject unreadCount = CreateText("UnreadCount", badge.transform, "1", 11, FontStyle.Bold, Color.white);
        SetRect(unreadCount, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        unreadCount.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        return obj;
    }

    private static GameObject CreateAdminNoticePrefab()
    {
        GameObject obj = new GameObject("AdminNoticeItem");
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 76);

        obj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);

        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.preferredHeight = 76;
        layout.flexibleWidth = 1;

        // Avatar (Gold) - 루트 직접 자식
        GameObject avatar = CreateUIElement("Avatar", obj.transform,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(50, 50));
        avatar.AddComponent<Image>().color = new Color(1f, 0.84f, 0f); // Gold

        // TitleText - 루트 직접 자식
        GameObject title = CreateText("TitleText", obj.transform, "WOOPANG", 16, FontStyle.Bold, new Color(1f, 0.84f, 0f));
        SetRect(title, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(76, 10), new Vector2(150, 24));

        // PreviewText - 루트 직접 자식
        GameObject preview = CreateText("PreviewText", obj.transform, "공지사항...", 14, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f));
        SetRect(preview, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(76, -12), new Vector2(-150, 20));

        // TimeText - 루트 직접 자식
        GameObject time = CreateText("TimeText", obj.transform, "오전 10:00", 12, FontStyle.Normal, new Color(0.6f, 0.6f, 0.6f));
        SetRect(time, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 10), new Vector2(70, 20));
        time.GetComponent<Text>().alignment = TextAnchor.MiddleRight;

        // UnreadBadge - 루트 직접 자식
        GameObject badge = CreateUIElement("UnreadBadge", obj.transform,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, -12), new Vector2(22, 22));
        badge.AddComponent<Image>().color = new Color(0.902f, 0.294f, 0.294f, 1f); // #E64B4B

        GameObject unreadCount = CreateText("UnreadCount", badge.transform, "1", 11, FontStyle.Bold, Color.white);
        SetRect(unreadCount, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        unreadCount.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        return obj;
    }

    /// <summary>
    /// AdminNoticeItem 프리팹의 Badge → UnreadBadge 이름 변경 + 불필요한 ContentWrapper 제거
    /// </summary>
    private static void EnsureAdminNoticeItemContent()
    {
        string prefabPath = Path.Combine(DM_PREFAB_FOLDER, "AdminNoticeItem.prefab");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        bool changed = false;

        // Badge → UnreadBadge 이름 변경 (루트 직접 자식)
        Transform badge = prefab.transform.Find("Badge");
        if (badge != null)
        {
            badge.gameObject.name = "UnreadBadge";
            changed = true;
        }

        // 불필요한 ContentWrapper 잔존물 제거
        Transform wrapper = prefab.transform.Find("ContentWrapper");
        if (wrapper != null)
        {
            // ContentWrapper 자식들을 루트로 이동 후 제거
            System.Collections.Generic.List<Transform> wrapperChildren = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < wrapper.childCount; i++)
                wrapperChildren.Add(wrapper.GetChild(i));
            foreach (var child in wrapperChildren)
                child.SetParent(prefab.transform, false);
            Object.DestroyImmediate(wrapper.gameObject, true);
            changed = true;
        }

        // 불필요한 빈 Content 잔존물 제거
        Transform emptyContent = prefab.transform.Find("Content");
        if (emptyContent != null && emptyContent.childCount == 0)
        {
            Object.DestroyImmediate(emptyContent.gameObject, true);
            changed = true;
        }

        if (changed)
        {
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log("[DMSetup] AdminNoticeItem 프리팹 정리 완료 (Badge→UnreadBadge)");
        }
    }

    private static GameObject CreateMessageBubblePrefab(string name, bool isMine)
    {
        GameObject obj = new GameObject(name);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 0);

        HorizontalLayoutGroup hlg = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = isMine ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        hlg.spacing = 6;
        hlg.padding = isMine ? new RectOffset(80, 12, 4, 4) : new RectOffset(12, 80, 4, 4);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;

        ContentSizeFitter csf = obj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement rootLayout = obj.AddComponent<LayoutElement>();
        rootLayout.flexibleWidth = 1;

        // 시간 (왼쪽/오른쪽)
        if (!isMine)
        {
            // 버블 먼저
            CreateBubbleContent(obj.transform, isMine);
            CreateTimeText(obj.transform);
        }
        else
        {
            // 시간 먼저
            CreateTimeText(obj.transform);
            CreateBubbleContent(obj.transform, isMine);
        }

        return obj;
    }

    private static void CreateBubbleContent(Transform parent, bool isMine)
    {
        GameObject bubble = new GameObject("Bubble");
        bubble.transform.SetParent(parent, false);

        RectTransform bubbleRect = bubble.AddComponent<RectTransform>();

        Image bubbleBg = bubble.AddComponent<Image>();
        bubbleBg.color = isMine ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.95f, 0.95f, 0.95f);

        LayoutElement bubbleLayout = bubble.AddComponent<LayoutElement>();
        bubbleLayout.preferredWidth = 250;
        bubbleLayout.flexibleWidth = 0;

        ContentSizeFitter bubbleCsf = bubble.AddComponent<ContentSizeFitter>();
        bubbleCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bubbleCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup bubbleVlg = bubble.AddComponent<VerticalLayoutGroup>();
        bubbleVlg.padding = new RectOffset(12, 12, 8, 8);
        bubbleVlg.childForceExpandWidth = false;
        bubbleVlg.childForceExpandHeight = false;
        bubbleVlg.childControlWidth = true;
        bubbleVlg.childControlHeight = true;

        // ContentText
        GameObject content = CreateText("ContentText", bubble.transform, "메시지 내용", 15, FontStyle.Normal,
            isMine ? Color.white : Color.black);
        LayoutElement contentLayout = content.AddComponent<LayoutElement>();
        contentLayout.preferredWidth = 220;

        // HeartIcon
        GameObject heart = CreateUIElement("HeartIcon", bubble.transform,
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-4, 4), new Vector2(16, 16));
        heart.AddComponent<Image>().color = new Color(1f, 0.4f, 0.6f);
        heart.SetActive(false);
    }

    private static void CreateTimeText(Transform parent)
    {
        GameObject timeArea = new GameObject("TimeArea");
        timeArea.transform.SetParent(parent, false);

        RectTransform timeRect = timeArea.AddComponent<RectTransform>();

        VerticalLayoutGroup vlg = timeArea.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.LowerCenter;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        LayoutElement timeLayout = timeArea.AddComponent<LayoutElement>();
        timeLayout.preferredWidth = 50;

        GameObject time = CreateText("TimeText", timeArea.transform, "오후 2:30", 10, FontStyle.Normal, new Color(0.6f, 0.6f, 0.6f));
        time.GetComponent<Text>().alignment = TextAnchor.LowerCenter;
    }

    private static GameObject CreateAdminMessageBubblePrefab()
    {
        GameObject obj = CreateMessageBubblePrefab("AdminMessageBubble", false);

        // 배경색을 다크로 변경
        Image bubbleBg = obj.GetComponentInChildren<Image>();
        if (bubbleBg != null && bubbleBg.gameObject.name == "Bubble")
        {
            bubbleBg.color = new Color(0.12f, 0.12f, 0.12f);
        }

        // 텍스트 색상을 흰색으로
        Text contentText = obj.GetComponentInChildren<Text>();
        if (contentText != null && contentText.gameObject.name == "ContentText")
        {
            contentText.color = Color.white;
        }

        return obj;
    }

    private static GameObject CreateSearchResultItemPrefab()
    {
        GameObject obj = new GameObject("SearchResultItem");
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 60);

        obj.AddComponent<Image>().color = Color.white;

        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.preferredHeight = 60;
        layout.flexibleWidth = 1;

        // Avatar
        GameObject avatar = CreateUIElement("Avatar", obj.transform,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(44, 44));
        avatar.AddComponent<Image>().color = new Color(0.93f, 0.93f, 0.93f);

        // UsernameText
        GameObject username = CreateText("UsernameText", obj.transform, "사용자", 15, FontStyle.Bold, Color.black);
        SetRect(username, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 6), new Vector2(150, 24));

        // StatusText
        GameObject status = CreateText("StatusText", obj.transform, "팔로잉", 12, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f));
        SetRect(status, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, -12), new Vector2(100, 20));

        // ChatButton
        GameObject chatBtn = CreateButton("ChatButton", obj.transform, "대화", 13);
        SetRect(chatBtn, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(60, 32));
        chatBtn.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);
        chatBtn.GetComponentInChildren<Text>().color = Color.white;

        return obj;
    }

    private static GameObject CreateHeartAnimationPrefab()
    {
        GameObject obj = new GameObject("HeartAnimation");
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(32, 32);

        // 하트 텍스트
        GameObject heart = CreateText("HeartText", obj.transform, "♥", 28, FontStyle.Bold, new Color(1f, 0.4f, 0.6f));
        SetRect(heart, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        heart.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        return obj;
    }

    #endregion

    #region UI 헬퍼

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;

        Transform direct = parent.Find(name);
        if (direct != null) return direct;

        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }

        return null;
    }

    private static GameObject CreateUIElement(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        return obj;
    }

    private static void SetRect(GameObject obj, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        RectTransform rect = obj.GetComponent<RectTransform>();
        if (rect == null) rect = obj.AddComponent<RectTransform>();

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;
    }

    private static GameObject CreateText(string name, Transform parent, string text, int fontSize, FontStyle style, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        Text textComp = obj.AddComponent<Text>();
        textComp.text = text;
        textComp.font = GetDefaultFont();
        textComp.fontSize = fontSize;
        textComp.fontStyle = style;
        textComp.color = color;
        textComp.alignment = TextAnchor.MiddleLeft;

        return obj;
    }

    private static GameObject CreateButton(string name, Transform parent, string text, int fontSize = 14)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        obj.AddComponent<Image>().color = new Color(0.9f, 0.9f, 0.9f);
        obj.AddComponent<Button>();

        GameObject textObj = CreateText("Text", obj.transform, text, fontSize, FontStyle.Normal, Color.black);
        SetRect(textObj, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        textObj.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        return obj;
    }

    private static GameObject CreateInputField(string name, Transform parent, string placeholder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        obj.AddComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f);

        InputField input = obj.AddComponent<InputField>();

        // Placeholder
        GameObject ph = CreateText("Placeholder", obj.transform, placeholder, 15, FontStyle.Normal, new Color(0.6f, 0.6f, 0.6f));
        SetRect(ph, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(10, 0), new Vector2(-20, 0));

        // Text
        GameObject txt = CreateText("Text", obj.transform, "", 15, FontStyle.Normal, Color.black);
        SetRect(txt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(10, 0), new Vector2(-20, 0));

        input.textComponent = txt.GetComponent<Text>();
        input.placeholder = ph.GetComponent<Text>();

        return obj;
    }

    private static GameObject CreateScrollView(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        ScrollRect sr = obj.AddComponent<ScrollRect>();
        sr.horizontal = false;

        // Viewport
        GameObject viewport = CreateUIElement("Viewport", obj.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        viewport.AddComponent<Image>().color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = true;

        // Content
        GameObject content = CreateUIElement("Content", viewport.transform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewport.GetComponent<RectTransform>();
        sr.content = content.GetComponent<RectTransform>();

        return obj;
    }

    #endregion

    #region Follow List Panel Setup

    public static void CreateFollowListPanel()
    {
        // Canvas 찾기
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[DMSetup] Canvas를 찾을 수 없습니다!");
            return;
        }

        // ProfileManager 찾기
        ProfileManager profileManager = Object.FindFirstObjectByType<ProfileManager>();
        if (profileManager == null)
        {
            Debug.LogError("[DMSetup] ProfileManager를 찾을 수 없습니다!");
            return;
        }

        // 기존 FollowListPanel 제거
        if (profileManager.followListPanel != null)
        {
            Undo.DestroyObjectImmediate(profileManager.followListPanel);
        }

        Undo.RecordObject(profileManager, "Create Follow List Panel");

        // FollowListPanel 생성 (인스타그램 스타일)
        GameObject panel = CreateFollowListPanelUI(canvas.transform);
        profileManager.followListPanel = panel;

        // 헤더 연결
        profileManager.followListTitleText = panel.transform.Find("Header/TitleText")?.GetComponent<Text>();
        profileManager.followListBackButton = panel.transform.Find("Header/BackButton")?.GetComponent<Button>();

        // 탭 버튼 연결
        profileManager.followersTabButton = panel.transform.Find("TabBar/FollowersTab")?.GetComponent<Button>();
        profileManager.followingTabButton = panel.transform.Find("TabBar/FollowingTab")?.GetComponent<Button>();
        profileManager.followersTabText = panel.transform.Find("TabBar/FollowersTab/Text")?.GetComponent<Text>();
        profileManager.followingTabText = panel.transform.Find("TabBar/FollowingTab/Text")?.GetComponent<Text>();
        profileManager.followersTabIndicator = panel.transform.Find("TabBar/FollowersTab/Indicator")?.gameObject;
        profileManager.followingTabIndicator = panel.transform.Find("TabBar/FollowingTab/Indicator")?.gameObject;

        // 검색바 연결
        profileManager.followListSearchInput = panel.transform.Find("SearchBar/SearchInput")?.GetComponent<InputField>();

        // 스와이프 핸들러 연결
        profileManager.swipePageHandler = panel.transform.Find("ContentArea")?.GetComponent<SwipePageHandler>();
        profileManager.followListSlideScrollRect = panel.transform.Find("ContentArea")?.GetComponent<ScrollRect>();

        // 페이지 연결 (스와이프 구조: ContentArea/SwipeViewport/SwipeContent/페이지)
        profileManager.followingPage = panel.transform.Find("ContentArea/SwipeViewport/SwipeContent/FollowingPage")?.gameObject;
        profileManager.followersPage = panel.transform.Find("ContentArea/SwipeViewport/SwipeContent/FollowersPage")?.gameObject;

        // 각각의 Content 연결
        profileManager.followingListContent = panel.transform.Find("ContentArea/SwipeViewport/SwipeContent/FollowingPage/Viewport/Content");
        profileManager.followersListContent = panel.transform.Find("ContentArea/SwipeViewport/SwipeContent/FollowersPage/Viewport/Content");

        // FollowListItem 프리팹 생성 (팔로잉용, 팔로워용)
        CreateFollowingItemPrefab(profileManager);
        CreateFollowerItemPrefab(profileManager);

        EditorUtility.SetDirty(profileManager);
        Debug.Log("[DMSetup] FollowListPanel (탭+슬라이드) 생성 완료!");
    }

    private static GameObject CreateFollowListPanelUI(Transform parent)
    {
        Font defaultFont = GetDefaultFont();
        Color bgColor = new Color(0.07f, 0.07f, 0.09f, 1f); // #121217
        Color headerColor = new Color(0.07f, 0.07f, 0.09f, 1f);
        Color tabBarColor = new Color(0.07f, 0.07f, 0.09f, 1f);
        Color searchBarColor = new Color(0.15f, 0.15f, 0.18f, 1f);
        Color buttonBlue = new Color(0.35f, 0.45f, 0.95f, 1f); // 파란색 버튼

        // === 메인 패널 (전체 화면) ===
        GameObject panel = new GameObject("FollowListPanel");
        panel.transform.SetParent(parent, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = bgColor;

        // === 헤더 (뒤로가기 + 사용자명) ===
        GameObject header = new GameObject("Header");
        header.transform.SetParent(panel.transform, false);

        RectTransform headerRect = header.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, 60);
        headerRect.anchoredPosition = Vector2.zero;

        Image headerBg = header.AddComponent<Image>();
        headerBg.color = headerColor;

        // 뒤로가기 버튼
        GameObject backBtn = new GameObject("BackButton");
        backBtn.transform.SetParent(header.transform, false);

        RectTransform backBtnRect = backBtn.AddComponent<RectTransform>();
        backBtnRect.anchorMin = new Vector2(0, 0.5f);
        backBtnRect.anchorMax = new Vector2(0, 0.5f);
        backBtnRect.pivot = new Vector2(0, 0.5f);
        backBtnRect.sizeDelta = new Vector2(50, 50);
        backBtnRect.anchoredPosition = new Vector2(10, 0);

        Image backBtnImg = backBtn.AddComponent<Image>();
        backBtnImg.color = Color.clear;

        Button backBtnComp = backBtn.AddComponent<Button>();
        backBtnComp.targetGraphic = backBtnImg;

        GameObject backIcon = new GameObject("Icon");
        backIcon.transform.SetParent(backBtn.transform, false);
        RectTransform backIconRect = backIcon.AddComponent<RectTransform>();
        backIconRect.anchorMin = Vector2.zero;
        backIconRect.anchorMax = Vector2.one;
        backIconRect.offsetMin = Vector2.zero;
        backIconRect.offsetMax = Vector2.zero;

        Text backIconText = backIcon.AddComponent<Text>();
        backIconText.text = "<";
        backIconText.font = defaultFont;
        backIconText.fontSize = 28;
        backIconText.color = Color.white;
        backIconText.alignment = TextAnchor.MiddleCenter;

        // 제목 (사용자명)
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(header.transform, false);

        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(60, 0);
        titleRect.offsetMax = new Vector2(-60, 0);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "username";
        titleText.font = defaultFont;
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;

        // === 탭 바 (XXX 팔로워 | XXX 팔로잉) ===
        GameObject tabBar = new GameObject("TabBar");
        tabBar.transform.SetParent(panel.transform, false);

        RectTransform tabBarRect = tabBar.AddComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0, 1);
        tabBarRect.anchorMax = new Vector2(1, 1);
        tabBarRect.pivot = new Vector2(0.5f, 1);
        tabBarRect.sizeDelta = new Vector2(0, 50);
        tabBarRect.anchoredPosition = new Vector2(0, -60);

        Image tabBarBg = tabBar.AddComponent<Image>();
        tabBarBg.color = tabBarColor;

        HorizontalLayoutGroup tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabHlg.spacing = 0;
        tabHlg.padding = new RectOffset(20, 20, 0, 0);
        tabHlg.childAlignment = TextAnchor.MiddleCenter;
        tabHlg.childForceExpandWidth = true;
        tabHlg.childForceExpandHeight = true;

        // 팔로잉 탭 (왼쪽) - 숫자 없이
        GameObject followingTab = CreateInstagramTabButton("FollowingTab", tabBar.transform, "팔로잉", defaultFont, true);
        // 팔로워 탭 (오른쪽) - 숫자 없이
        GameObject followersTab = CreateInstagramTabButton("FollowersTab", tabBar.transform, "팔로워", defaultFont, false);

        // === 검색바 ===
        GameObject searchBar = new GameObject("SearchBar");
        searchBar.transform.SetParent(panel.transform, false);

        RectTransform searchBarRect = searchBar.AddComponent<RectTransform>();
        searchBarRect.anchorMin = new Vector2(0, 1);
        searchBarRect.anchorMax = new Vector2(1, 1);
        searchBarRect.pivot = new Vector2(0.5f, 1);
        searchBarRect.sizeDelta = new Vector2(-30, 45);
        searchBarRect.anchoredPosition = new Vector2(0, -120);

        Image searchBarBg = searchBar.AddComponent<Image>();
        searchBarBg.color = searchBarColor;

        // 검색 아이콘
        GameObject searchIcon = new GameObject("SearchIcon");
        searchIcon.transform.SetParent(searchBar.transform, false);

        RectTransform searchIconRect = searchIcon.AddComponent<RectTransform>();
        searchIconRect.anchorMin = new Vector2(0, 0.5f);
        searchIconRect.anchorMax = new Vector2(0, 0.5f);
        searchIconRect.pivot = new Vector2(0, 0.5f);
        searchIconRect.sizeDelta = new Vector2(30, 30);
        searchIconRect.anchoredPosition = new Vector2(15, 0);

        Text searchIconText = searchIcon.AddComponent<Text>();
        searchIconText.text = "Q";
        searchIconText.font = defaultFont;
        searchIconText.fontSize = 18;
        searchIconText.color = new Color(0.5f, 0.5f, 0.55f, 1f);
        searchIconText.alignment = TextAnchor.MiddleCenter;

        // 검색 입력 필드
        GameObject searchInputObj = new GameObject("SearchInput");
        searchInputObj.transform.SetParent(searchBar.transform, false);

        RectTransform searchInputRect = searchInputObj.AddComponent<RectTransform>();
        searchInputRect.anchorMin = new Vector2(0, 0);
        searchInputRect.anchorMax = new Vector2(1, 1);
        searchInputRect.offsetMin = new Vector2(45, 5);
        searchInputRect.offsetMax = new Vector2(-10, -5);

        // Placeholder
        GameObject placeholder = new GameObject("Placeholder");
        placeholder.transform.SetParent(searchInputObj.transform, false);

        RectTransform phRect = placeholder.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;

        Text phText = placeholder.AddComponent<Text>();
        phText.text = "검색";
        phText.font = defaultFont;
        phText.fontSize = 18;
        phText.color = new Color(0.5f, 0.5f, 0.55f, 1f);
        phText.alignment = TextAnchor.MiddleLeft;

        // Input Text
        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(searchInputObj.transform, false);

        RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = Vector2.zero;
        inputTextRect.offsetMax = Vector2.zero;

        Text inputText = inputTextObj.AddComponent<Text>();
        inputText.font = defaultFont;
        inputText.fontSize = 18;
        inputText.color = Color.white;
        inputText.alignment = TextAnchor.MiddleLeft;

        InputField searchInput = searchInputObj.AddComponent<InputField>();
        searchInput.textComponent = inputText;
        searchInput.placeholder = phText;

        // === 콘텐츠 영역 (스와이프 가능한 가로 스크롤) ===
        GameObject contentArea = new GameObject("ContentArea");
        contentArea.transform.SetParent(panel.transform, false);

        RectTransform contentAreaRect = contentArea.AddComponent<RectTransform>();
        contentAreaRect.anchorMin = new Vector2(0, 0);
        contentAreaRect.anchorMax = new Vector2(1, 1);
        contentAreaRect.offsetMin = new Vector2(0, 0);
        contentAreaRect.offsetMax = new Vector2(0, -175); // 헤더+탭+검색바 높이

        // 가로 스크롤용 ScrollRect 추가
        ScrollRect horizontalScroll = contentArea.AddComponent<ScrollRect>();
        horizontalScroll.horizontal = true;
        horizontalScroll.vertical = false;
        horizontalScroll.movementType = ScrollRect.MovementType.Elastic;
        horizontalScroll.elasticity = 0.1f;
        horizontalScroll.inertia = false; // 스냅을 위해 관성 비활성화

        Image contentAreaBg = contentArea.AddComponent<Image>();
        contentAreaBg.color = new Color(0.07f, 0.07f, 0.09f, 1f);

        // Mask 역할을 할 Viewport
        GameObject swipeViewport = new GameObject("SwipeViewport");
        swipeViewport.transform.SetParent(contentArea.transform, false);

        RectTransform swipeViewportRect = swipeViewport.AddComponent<RectTransform>();
        swipeViewportRect.anchorMin = Vector2.zero;
        swipeViewportRect.anchorMax = Vector2.one;
        swipeViewportRect.offsetMin = Vector2.zero;
        swipeViewportRect.offsetMax = Vector2.zero;

        swipeViewport.AddComponent<Image>().color = Color.clear;
        swipeViewport.AddComponent<Mask>().showMaskGraphic = false;

        // 가로 스크롤용 Content (두 페이지를 나란히 배치)
        GameObject swipeContent = new GameObject("SwipeContent");
        swipeContent.transform.SetParent(swipeViewport.transform, false);

        RectTransform swipeContentRect = swipeContent.AddComponent<RectTransform>();
        swipeContentRect.anchorMin = new Vector2(0, 0);
        swipeContentRect.anchorMax = new Vector2(0, 1);
        swipeContentRect.pivot = new Vector2(0, 0.5f);
        // 너비는 화면 2배 (2페이지)
        swipeContentRect.sizeDelta = new Vector2(0, 0);

        // HorizontalLayoutGroup으로 페이지 나란히 배치
        HorizontalLayoutGroup swipeHlg = swipeContent.AddComponent<HorizontalLayoutGroup>();
        swipeHlg.spacing = 0;
        swipeHlg.padding = new RectOffset(0, 0, 0, 0);
        swipeHlg.childAlignment = TextAnchor.MiddleLeft;
        swipeHlg.childForceExpandWidth = false;
        swipeHlg.childForceExpandHeight = true;
        swipeHlg.childControlWidth = false;
        swipeHlg.childControlHeight = true;

        // ScrollRect 연결
        horizontalScroll.viewport = swipeViewportRect;
        horizontalScroll.content = swipeContentRect;

        // 팔로잉 페이지 (왼쪽 - 첫 번째)
        GameObject followingPage = CreateSwipeablePage("FollowingPage", swipeContent.transform, defaultFont);

        // 팔로워 페이지 (오른쪽 - 두 번째)
        GameObject followersPage = CreateSwipeablePage("FollowersPage", swipeContent.transform, defaultFont);

        // SwipePageHandler 추가
        SwipePageHandler swipeHandler = contentArea.AddComponent<SwipePageHandler>();
        swipeHandler.pageCount = 2;
        swipeHandler.scrollRect = horizontalScroll;
        swipeHandler.snapSpeed = 10f;
        swipeHandler.swipeThreshold = 0.2f;

        // SwipePageSizer 추가 (페이지 크기 동적 조절)
        SwipePageSizer pageSizer = swipeContent.AddComponent<SwipePageSizer>();
        pageSizer.viewport = swipeViewportRect;

        // 기본적으로 비활성화
        panel.SetActive(false);

        return panel;
    }

    /// <summary>
    /// 인스타그램 스타일 탭 버튼 생성
    /// </summary>
    private static GameObject CreateInstagramTabButton(string name, Transform parent, string text, Font font, bool isActive)
    {
        GameObject tab = new GameObject(name);
        tab.transform.SetParent(parent, false);

        RectTransform tabRect = tab.AddComponent<RectTransform>();

        Image tabBg = tab.AddComponent<Image>();
        tabBg.color = Color.clear;

        Button tabBtn = tab.AddComponent<Button>();
        tabBtn.targetGraphic = tabBg;

        // 탭 텍스트
        GameObject tabTextObj = new GameObject("Text");
        tabTextObj.transform.SetParent(tab.transform, false);

        RectTransform tabTextRect = tabTextObj.AddComponent<RectTransform>();
        tabTextRect.anchorMin = Vector2.zero;
        tabTextRect.anchorMax = Vector2.one;
        tabTextRect.offsetMin = Vector2.zero;
        tabTextRect.offsetMax = Vector2.zero;

        Text tabText = tabTextObj.AddComponent<Text>();
        tabText.text = text;
        tabText.font = font;
        tabText.fontSize = 20;
        tabText.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
        tabText.color = isActive ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
        tabText.alignment = TextAnchor.MiddleCenter;

        // 하단 인디케이터
        GameObject indicator = new GameObject("Indicator");
        indicator.transform.SetParent(tab.transform, false);

        RectTransform indicatorRect = indicator.AddComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(0, 0);
        indicatorRect.anchorMax = new Vector2(1, 0);
        indicatorRect.pivot = new Vector2(0.5f, 0);
        indicatorRect.sizeDelta = new Vector2(0, 2);
        indicatorRect.anchoredPosition = Vector2.zero;

        Image indicatorImg = indicator.AddComponent<Image>();
        indicatorImg.color = isActive ? Color.white : Color.clear;

        return tab;
    }

    /// <summary>
    /// 인스타그램 스타일 리스트 페이지 생성 (세로 스크롤)
    /// </summary>
    private static GameObject CreateInstagramListPage(string name, Transform parent, Font font, bool isActive)
    {
        GameObject page = new GameObject(name);
        page.transform.SetParent(parent, false);

        RectTransform pageRect = page.AddComponent<RectTransform>();
        pageRect.anchorMin = Vector2.zero;
        pageRect.anchorMax = Vector2.one;
        pageRect.offsetMin = Vector2.zero;
        pageRect.offsetMax = Vector2.zero;

        // 스크롤뷰
        ScrollRect pageSr = page.AddComponent<ScrollRect>();
        pageSr.horizontal = false;
        pageSr.vertical = true;
        pageSr.movementType = ScrollRect.MovementType.Elastic;
        pageSr.elasticity = 0.1f;

        Image pageBg = page.AddComponent<Image>();
        pageBg.color = new Color(0.07f, 0.07f, 0.09f, 1f);

        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(page.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        viewport.AddComponent<Image>().color = Color.clear;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0;
        vlg.padding = new RectOffset(0, 0, 10, 10);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        pageSr.viewport = viewportRect;
        pageSr.content = contentRect;

        // 비활성 페이지는 숨김
        page.SetActive(isActive);

        return page;
    }

    /// <summary>
    /// 스와이프 가능한 페이지 생성 (가로 스크롤 컨테이너 내부용)
    /// 각 페이지는 부모 ScrollRect의 viewport 너비와 동일한 너비를 가짐
    /// </summary>
    private static GameObject CreateSwipeablePage(string name, Transform parent, Font font)
    {
        GameObject page = new GameObject(name);
        page.transform.SetParent(parent, false);

        RectTransform pageRect = page.AddComponent<RectTransform>();
        // 크기는 런타임에 동적으로 설정됨 (부모 viewport 너비)
        pageRect.sizeDelta = new Vector2(0, 0);

        // LayoutElement로 부모의 viewport 너비를 따르도록 설정
        LayoutElement pageLE = page.AddComponent<LayoutElement>();
        pageLE.flexibleHeight = 1;
        // 너비는 SwipePageSizer 컴포넌트에서 동적으로 설정

        // 세로 스크롤뷰
        ScrollRect pageSr = page.AddComponent<ScrollRect>();
        pageSr.horizontal = false;
        pageSr.vertical = true;
        pageSr.movementType = ScrollRect.MovementType.Elastic;
        pageSr.elasticity = 0.1f;

        Image pageBg = page.AddComponent<Image>();
        pageBg.color = new Color(0.07f, 0.07f, 0.09f, 1f);

        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(page.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        viewport.AddComponent<Image>().color = Color.clear;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0;
        vlg.padding = new RectOffset(0, 0, 10, 10);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        pageSr.viewport = viewportRect;
        pageSr.content = contentRect;

        return page;
    }

    /// <summary>
    /// 팔로잉 아이템 프리팹 생성 (메시지 보내기 버튼)
    /// </summary>
    private static void CreateFollowingItemPrefab(ProfileManager profileManager)
    {
        // Resources 폴더에 생성 (런타임 로드 가능)
        string prefabFolder = "Assets/Resources/Prefabs/Profile";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            }
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Profile");
        }

        string prefabPath = $"{prefabFolder}/FollowingItem.prefab";

        // 기존 프리팹 확인
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            profileManager.followingItemPrefab = existingPrefab;
            Debug.Log("[DMSetup] 기존 FollowingItem 프리팹 사용");
            return;
        }

        Font defaultFont = GetDefaultFont();
        Color buttonBlue = new Color(0.35f, 0.45f, 0.95f, 1f);

        // 아이템 생성
        GameObject item = new GameObject("FollowingItem");
        RectTransform itemRect = item.AddComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0, 1);
        itemRect.anchorMax = new Vector2(1, 1);
        itemRect.pivot = new Vector2(0.5f, 1);
        itemRect.sizeDelta = new Vector2(0, 80);

        Image itemBg = item.AddComponent<Image>();
        itemBg.color = Color.clear;

        LayoutElement itemLE = item.AddComponent<LayoutElement>();
        itemLE.minHeight = 80;
        itemLE.preferredHeight = 80;
        itemLE.flexibleWidth = 1;

        // 클릭 가능 (프로필 열기)
        Button itemBtn = item.AddComponent<Button>();
        itemBtn.targetGraphic = itemBg;

        // HorizontalLayoutGroup
        HorizontalLayoutGroup hlg = item.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.padding = new RectOffset(15, 15, 10, 10);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // 아바타 (원형)
        GameObject avatar = new GameObject("Avatar");
        avatar.transform.SetParent(item.transform, false);
        RectTransform avatarRect = avatar.AddComponent<RectTransform>();
        avatarRect.sizeDelta = new Vector2(60, 60);
        Image avatarImg = avatar.AddComponent<Image>();
        avatarImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // 텍스트 영역
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(item.transform, false);
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.sizeDelta = new Vector2(150, 60);
        LayoutElement textAreaLE = textArea.AddComponent<LayoutElement>();
        textAreaLE.flexibleWidth = 1;

        // 사용자명
        GameObject usernameObj = new GameObject("Username");
        usernameObj.transform.SetParent(textArea.transform, false);
        RectTransform usernameRect = usernameObj.AddComponent<RectTransform>();
        usernameRect.anchorMin = new Vector2(0, 0.5f);
        usernameRect.anchorMax = new Vector2(1, 1);
        usernameRect.offsetMin = Vector2.zero;
        usernameRect.offsetMax = Vector2.zero;

        Text usernameText = usernameObj.AddComponent<Text>();
        usernameText.text = "username";
        usernameText.font = defaultFont;
        usernameText.fontSize = 20;
        usernameText.fontStyle = FontStyle.Bold;
        usernameText.color = Color.white;
        usernameText.alignment = TextAnchor.MiddleLeft;

        // 표시 이름 / 상태
        GameObject displayNameObj = new GameObject("DisplayName");
        displayNameObj.transform.SetParent(textArea.transform, false);
        RectTransform displayNameRect = displayNameObj.AddComponent<RectTransform>();
        displayNameRect.anchorMin = new Vector2(0, 0);
        displayNameRect.anchorMax = new Vector2(1, 0.5f);
        displayNameRect.offsetMin = Vector2.zero;
        displayNameRect.offsetMax = Vector2.zero;

        Text displayNameText = displayNameObj.AddComponent<Text>();
        displayNameText.text = "Display Name";
        displayNameText.font = defaultFont;
        displayNameText.fontSize = 16;
        displayNameText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        displayNameText.alignment = TextAnchor.MiddleLeft;

        // 메시지 보내기 버튼
        GameObject msgBtn = new GameObject("MessageButton");
        msgBtn.transform.SetParent(item.transform, false);
        RectTransform msgBtnRect = msgBtn.AddComponent<RectTransform>();
        msgBtnRect.sizeDelta = new Vector2(120, 40);

        Image msgBtnImg = msgBtn.AddComponent<Image>();
        msgBtnImg.color = buttonBlue;

        Button msgBtnComp = msgBtn.AddComponent<Button>();
        msgBtnComp.targetGraphic = msgBtnImg;

        GameObject msgBtnText = new GameObject("Text");
        msgBtnText.transform.SetParent(msgBtn.transform, false);
        RectTransform msgBtnTextRect = msgBtnText.AddComponent<RectTransform>();
        msgBtnTextRect.anchorMin = Vector2.zero;
        msgBtnTextRect.anchorMax = Vector2.one;
        msgBtnTextRect.offsetMin = Vector2.zero;
        msgBtnTextRect.offsetMax = Vector2.zero;

        Text msgText = msgBtnText.AddComponent<Text>();
        msgText.text = "메시지 보내기";
        msgText.font = defaultFont;
        msgText.fontSize = 16;
        msgText.fontStyle = FontStyle.Bold;
        msgText.color = Color.white;
        msgText.alignment = TextAnchor.MiddleCenter;

        // 더보기 버튼
        GameObject moreBtn = new GameObject("MoreButton");
        moreBtn.transform.SetParent(item.transform, false);
        RectTransform moreBtnRect = moreBtn.AddComponent<RectTransform>();
        moreBtnRect.sizeDelta = new Vector2(30, 40);

        Image moreBtnImg = moreBtn.AddComponent<Image>();
        moreBtnImg.color = Color.clear;

        Button moreBtnComp = moreBtn.AddComponent<Button>();
        moreBtnComp.targetGraphic = moreBtnImg;

        GameObject moreBtnText = new GameObject("Text");
        moreBtnText.transform.SetParent(moreBtn.transform, false);
        RectTransform moreBtnTextRect = moreBtnText.AddComponent<RectTransform>();
        moreBtnTextRect.anchorMin = Vector2.zero;
        moreBtnTextRect.anchorMax = Vector2.one;
        moreBtnTextRect.offsetMin = Vector2.zero;
        moreBtnTextRect.offsetMax = Vector2.zero;

        Text moreText = moreBtnText.AddComponent<Text>();
        moreText.text = "...";
        moreText.font = defaultFont;
        moreText.fontSize = 24;
        moreText.color = Color.white;
        moreText.alignment = TextAnchor.MiddleCenter;

        // 프리팹 저장
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(item, prefabPath);
        profileManager.followingItemPrefab = prefab;
        Object.DestroyImmediate(item);

        Debug.Log($"[DMSetup] FollowingItem 프리팹 생성: {prefabPath}");
    }

    /// <summary>
    /// 팔로워 아이템 프리팹 생성 (맞팔로우/팔로우 버튼 + X 버튼)
    /// </summary>
    private static void CreateFollowerItemPrefab(ProfileManager profileManager)
    {
        // Resources 폴더에 생성 (런타임 로드 가능)
        string prefabFolder = "Assets/Resources/Prefabs/Profile";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            }
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Profile");
        }

        string prefabPath = $"{prefabFolder}/FollowerItem.prefab";

        // 기존 프리팹 확인
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            profileManager.followerItemPrefab = existingPrefab;
            Debug.Log("[DMSetup] 기존 FollowerItem 프리팹 사용");
            return;
        }

        Font defaultFont = GetDefaultFont();
        Color buttonBlue = new Color(0.35f, 0.45f, 0.95f, 1f);

        // 아이템 생성
        GameObject item = new GameObject("FollowerItem");
        RectTransform itemRect = item.AddComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0, 1);
        itemRect.anchorMax = new Vector2(1, 1);
        itemRect.pivot = new Vector2(0.5f, 1);
        itemRect.sizeDelta = new Vector2(0, 80);

        Image itemBg = item.AddComponent<Image>();
        itemBg.color = Color.clear;

        LayoutElement itemLE = item.AddComponent<LayoutElement>();
        itemLE.minHeight = 80;
        itemLE.preferredHeight = 80;
        itemLE.flexibleWidth = 1;

        // 클릭 가능 (프로필 열기)
        Button itemBtn = item.AddComponent<Button>();
        itemBtn.targetGraphic = itemBg;

        // HorizontalLayoutGroup
        HorizontalLayoutGroup hlg = item.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.padding = new RectOffset(15, 15, 10, 10);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // 아바타 (원형)
        GameObject avatar = new GameObject("Avatar");
        avatar.transform.SetParent(item.transform, false);
        RectTransform avatarRect = avatar.AddComponent<RectTransform>();
        avatarRect.sizeDelta = new Vector2(60, 60);
        Image avatarImg = avatar.AddComponent<Image>();
        avatarImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // 텍스트 영역
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(item.transform, false);
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.sizeDelta = new Vector2(120, 60);
        LayoutElement textAreaLE = textArea.AddComponent<LayoutElement>();
        textAreaLE.flexibleWidth = 1;

        // 사용자명
        GameObject usernameObj = new GameObject("Username");
        usernameObj.transform.SetParent(textArea.transform, false);
        RectTransform usernameRect = usernameObj.AddComponent<RectTransform>();
        usernameRect.anchorMin = new Vector2(0, 0.5f);
        usernameRect.anchorMax = new Vector2(1, 1);
        usernameRect.offsetMin = Vector2.zero;
        usernameRect.offsetMax = Vector2.zero;

        Text usernameText = usernameObj.AddComponent<Text>();
        usernameText.text = "username";
        usernameText.font = defaultFont;
        usernameText.fontSize = 20;
        usernameText.fontStyle = FontStyle.Bold;
        usernameText.color = Color.white;
        usernameText.alignment = TextAnchor.MiddleLeft;

        // 표시 이름
        GameObject displayNameObj = new GameObject("DisplayName");
        displayNameObj.transform.SetParent(textArea.transform, false);
        RectTransform displayNameRect = displayNameObj.AddComponent<RectTransform>();
        displayNameRect.anchorMin = new Vector2(0, 0);
        displayNameRect.anchorMax = new Vector2(1, 0.5f);
        displayNameRect.offsetMin = Vector2.zero;
        displayNameRect.offsetMax = Vector2.zero;

        Text displayNameText = displayNameObj.AddComponent<Text>();
        displayNameText.text = "Display Name";
        displayNameText.font = defaultFont;
        displayNameText.fontSize = 16;
        displayNameText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        displayNameText.alignment = TextAnchor.MiddleLeft;

        // 팔로우/맞팔로우 버튼
        GameObject followBtn = new GameObject("FollowButton");
        followBtn.transform.SetParent(item.transform, false);
        RectTransform followBtnRect = followBtn.AddComponent<RectTransform>();
        followBtnRect.sizeDelta = new Vector2(100, 40);

        Image followBtnImg = followBtn.AddComponent<Image>();
        followBtnImg.color = buttonBlue;

        Button followBtnComp = followBtn.AddComponent<Button>();
        followBtnComp.targetGraphic = followBtnImg;

        GameObject followBtnText = new GameObject("Text");
        followBtnText.transform.SetParent(followBtn.transform, false);
        RectTransform followBtnTextRect = followBtnText.AddComponent<RectTransform>();
        followBtnTextRect.anchorMin = Vector2.zero;
        followBtnTextRect.anchorMax = Vector2.one;
        followBtnTextRect.offsetMin = Vector2.zero;
        followBtnTextRect.offsetMax = Vector2.zero;

        Text followText = followBtnText.AddComponent<Text>();
        followText.text = "맞팔로우";
        followText.font = defaultFont;
        followText.fontSize = 16;
        followText.fontStyle = FontStyle.Bold;
        followText.color = Color.white;
        followText.alignment = TextAnchor.MiddleCenter;

        // 삭제(X) 버튼
        GameObject removeBtn = new GameObject("RemoveButton");
        removeBtn.transform.SetParent(item.transform, false);
        RectTransform removeBtnRect = removeBtn.AddComponent<RectTransform>();
        removeBtnRect.sizeDelta = new Vector2(40, 40);

        Image removeBtnImg = removeBtn.AddComponent<Image>();
        removeBtnImg.color = Color.clear;

        Button removeBtnComp = removeBtn.AddComponent<Button>();
        removeBtnComp.targetGraphic = removeBtnImg;

        GameObject removeBtnText = new GameObject("Text");
        removeBtnText.transform.SetParent(removeBtn.transform, false);
        RectTransform removeBtnTextRect = removeBtnText.AddComponent<RectTransform>();
        removeBtnTextRect.anchorMin = Vector2.zero;
        removeBtnTextRect.anchorMax = Vector2.one;
        removeBtnTextRect.offsetMin = Vector2.zero;
        removeBtnTextRect.offsetMax = Vector2.zero;

        Text removeText = removeBtnText.AddComponent<Text>();
        removeText.text = "✕";
        removeText.font = defaultFont;
        removeText.fontSize = 20;
        removeText.color = Color.white;
        removeText.alignment = TextAnchor.MiddleCenter;

        // 프리팹 저장
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(item, prefabPath);
        profileManager.followerItemPrefab = prefab;
        Object.DestroyImmediate(item);

        Debug.Log($"[DMSetup] FollowerItem 프리팹 생성: {prefabPath}");
    }

    private static void CreateFollowListItemPrefab(ProfileManager profileManager)
    {
        // Prefabs/Profile 폴더 확인/생성
        string prefabFolder = "Assets/Prefabs/Profile";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "Profile");
        }

        string prefabPath = $"{prefabFolder}/FollowListItem.prefab";

        // 기존 프리팹 확인
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            // followListItemPrefab 레거시 필드 제거됨 - 프리팹만 존재하면 됨
            Debug.Log("[DMSetup] 기존 FollowListItem 프리팹 사용");
            return;
        }

        // 임시 GameObject 생성
        GameObject itemObj = new GameObject("FollowListItem");

        RectTransform itemRect = itemObj.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0, 100);

        Image itemBg = itemObj.AddComponent<Image>();
        itemBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        Button itemBtn = itemObj.AddComponent<Button>();
        itemBtn.targetGraphic = itemBg;

        // 호버 색상 설정
        ColorBlock colors = itemBtn.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.pressedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        itemBtn.colors = colors;

        // HorizontalLayoutGroup
        HorizontalLayoutGroup hlg = itemObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 15;
        hlg.padding = new RectOffset(15, 15, 10, 10);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // Avatar
        GameObject avatarObj = new GameObject("Avatar");
        avatarObj.transform.SetParent(itemObj.transform, false);

        RectTransform avatarRect = avatarObj.AddComponent<RectTransform>();
        avatarRect.sizeDelta = new Vector2(80, 80);

        Image avatarImg = avatarObj.AddComponent<Image>();
        avatarImg.color = new Color(0.5f, 0.5f, 0.5f, 1f);

        // Avatar를 원형으로 만들기 위한 Mask
        GameObject avatarMask = new GameObject("AvatarMask");
        avatarMask.transform.SetParent(avatarObj.transform, false);

        RectTransform maskRect = avatarMask.AddComponent<RectTransform>();
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.offsetMin = Vector2.zero;
        maskRect.offsetMax = Vector2.zero;

        // Username
        GameObject usernameObj = new GameObject("Username");
        usernameObj.transform.SetParent(itemObj.transform, false);

        RectTransform usernameRect = usernameObj.AddComponent<RectTransform>();
        usernameRect.sizeDelta = new Vector2(300, 80);

        Text usernameText = usernameObj.AddComponent<Text>();
        usernameText.text = "사용자 이름";
        usernameText.font = GetDefaultFont();
        usernameText.fontSize = 28;
        usernameText.color = Color.white;
        usernameText.alignment = TextAnchor.MiddleLeft;

        // 프리팹 저장
        PrefabUtility.SaveAsPrefabAsset(itemObj, prefabPath);
        // followListItemPrefab 레거시 필드 제거됨 - 프리팹 파일만 생성

        // 임시 오브젝트 삭제
        Object.DestroyImmediate(itemObj);

        Debug.Log($"[DMSetup] FollowListItem 프리팹 생성: {prefabPath}");
    }

    #endregion
}
