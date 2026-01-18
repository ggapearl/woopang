using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// WOOPANG DM 시스템 원클릭 설정
///
/// 기존 프리팹 활용:
/// - Assets/Prefab/MessagePanel.prefab: 메시지 목록 패널
/// - Assets/Prefab/MessageChatTemplate.prefab: 대화방 아이템 템플릿
/// - Assets/Prefabs/DM/: 메시지 버블 등 DM 관련 프리팹
///
/// 메뉴: WOOPANG > DM 원클릭 설정
/// </summary>
public class DMSetup
{
    // 기존 프리팹 경로
    private const string MESSAGE_PANEL_PREFAB = "Assets/Prefab/MessagePanel.prefab";
    private const string CHAT_TEMPLATE_PREFAB = "Assets/Prefab/MessageChatTemplate.prefab";
    private const string DM_PREFAB_FOLDER = "Assets/Prefabs/DM";

    [MenuItem("WOOPANG/DM 원클릭 설정")]
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

        // 7. 테스트 시스템 설정
        SetupTestSystem(manager);

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
    /// 중복된 DM 관련 오브젝트 정리 (별도 메뉴)
    /// </summary>
    [MenuItem("WOOPANG/DM 중복 정리")]
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
        string[] objectNames = { "MessagePanel", "ChatRoomPanel", "MessagePanelManager", "DMNotificationManager" };

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
        GameObject title = CreateText("ChatTitle", header.transform, "사용자", 17, FontStyle.Bold, Color.black);
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

    #region Step 6: 테스트 시스템 설정

    private static void SetupTestSystem(MessagePanelManager manager)
    {
        if (manager == null) return;

        // DMTestDataGenerator 추가
        DMTestDataGenerator generator = manager.GetComponent<DMTestDataGenerator>();
        if (generator == null)
        {
            generator = manager.gameObject.AddComponent<DMTestDataGenerator>();
            generator.autoGenerateOnStart = true;
            generator.conversationCount = 5;
            generator.showTestButton = true;
            generator.enableAutoMessages = false;
            Debug.Log("[DMSetup] DMTestDataGenerator 추가됨");
        }

        // DMNotificationManager 추가
        DMNotificationManager notifManager = Object.FindFirstObjectByType<DMNotificationManager>();
        if (notifManager == null)
        {
            GameObject notifObj = new GameObject("DMNotificationManager");
            notifManager = notifObj.AddComponent<DMNotificationManager>();
            Debug.Log("[DMSetup] DMNotificationManager 생성됨");
        }

        // 테스트 모드 활성화
        manager.enableTestMode = true;

        EditorUtility.SetDirty(manager);
        Debug.Log("[DMSetup] 테스트 시스템 설정 완료");
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

        // Avatar (Gold)
        GameObject avatar = CreateUIElement("Avatar", obj.transform,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(50, 50));
        avatar.AddComponent<Image>().color = new Color(1f, 0.84f, 0f); // Gold

        // TitleText
        GameObject title = CreateText("TitleText", obj.transform, "WOOPANG", 16, FontStyle.Bold, new Color(1f, 0.84f, 0f));
        SetRect(title, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(76, 10), new Vector2(150, 24));

        // PreviewText
        GameObject preview = CreateText("PreviewText", obj.transform, "공지사항...", 14, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f));
        SetRect(preview, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(76, -12), new Vector2(-150, 20));

        // TimeText
        GameObject time = CreateText("TimeText", obj.transform, "오전 10:00", 12, FontStyle.Normal, new Color(0.6f, 0.6f, 0.6f));
        SetRect(time, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 10), new Vector2(70, 20));
        time.GetComponent<Text>().alignment = TextAnchor.MiddleRight;

        return obj;
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
        textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
}
