using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

/// <summary>
/// DM(다이렉트 메시지) UI 및 오브젝트 자동 생성 에디터
/// </summary>
public class DirectMessageSetup : EditorWindow
{
    private Canvas targetCanvas;
    private Color primaryColor = new Color(0.1f, 0.1f, 0.1f, 0.95f); // 다크 배경
    private Color goldColor = new Color(1f, 0.843f, 0f, 1f); // #FFD700
    private Color textColor = Color.white;
    private Color myMessageColor = new Color(1f, 0.843f, 0f, 0.9f); // 내 메시지 배경
    private Color otherMessageColor = new Color(0.2f, 0.2f, 0.2f, 1f); // 상대 메시지 배경

    [MenuItem("Woopang/Setup DM System")]
    public static void ShowWindow()
    {
        GetWindow<DirectMessageSetup>("DM Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("다이렉트 메시지(DM) 시스템 설정", EditorStyles.boldLabel);
        GUILayout.Space(10);

        targetCanvas = (Canvas)EditorGUILayout.ObjectField("Target Canvas", targetCanvas, typeof(Canvas), true);

        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "이 도구는 다음을 생성합니다:\n" +
            "1. DM 패널 (받은 메시지 리스트)\n" +
            "2. 대화 패널 (채팅 뷰)\n" +
            "3. 팔로잉 선택 패널\n" +
            "4. 메시지 아이템 프리팹\n" +
            "5. DirectMessageManager 오브젝트",
            MessageType.Info);

        GUILayout.Space(10);

        if (GUILayout.Button("DM 시스템 생성", GUILayout.Height(40)))
        {
            if (targetCanvas == null)
            {
                targetCanvas = FindObjectOfType<Canvas>();
                if (targetCanvas == null)
                {
                    EditorUtility.DisplayDialog("오류", "Canvas를 찾을 수 없습니다.", "확인");
                    return;
                }
            }

            CreateDMSystem();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("프리팹만 생성", GUILayout.Height(30)))
        {
            CreatePrefabs();
        }
    }

    private void CreateDMSystem()
    {
        // 1. DirectMessageManager 오브젝트 생성
        GameObject managerObj = new GameObject("DirectMessageManager");
        DirectMessageManager manager = managerObj.AddComponent<DirectMessageManager>();

        // 2. DM 패널 생성
        GameObject dmPanel = CreateDMPanel();
        manager.dmPanel = dmPanel;

        // 3. 대화 패널 생성
        GameObject conversationPanel = CreateConversationPanel();
        manager.conversationPanel = conversationPanel;

        // 4. 팔로잉 선택 패널 생성
        GameObject followingPanel = CreateFollowingSelectPanel();
        manager.followingSelectPanel = followingPanel;

        // 5. 프리팹 연결
        ConnectPrefabs(manager);

        // 6. 초기 상태 설정
        dmPanel.SetActive(false);
        conversationPanel.SetActive(false);
        followingPanel.SetActive(false);

        // Selection 설정
        Selection.activeGameObject = managerObj;

        EditorUtility.DisplayDialog("완료",
            "DM 시스템이 생성되었습니다.\n\n" +
            "생성된 항목:\n" +
            "- DirectMessageManager 오브젝트\n" +
            "- DMPanel (받은 메시지)\n" +
            "- ConversationPanel (대화)\n" +
            "- FollowingSelectPanel (팔로잉 선택)\n" +
            "- 프리팹들 (Assets/Prefabs/DM/)",
            "확인");
    }

    private GameObject CreateDMPanel()
    {
        // DM 패널 (전체 화면)
        GameObject panel = CreatePanel("DMPanel", targetCanvas.transform);
        RectTransform rt = panel.GetComponent<RectTransform>();
        SetFullScreen(rt);

        // 배경
        Image bg = panel.GetComponent<Image>();
        bg.color = primaryColor;

        // 헤더
        GameObject header = CreateHeader(panel.transform, "받은 메시지", true);

        // 새 메시지 버튼 (헤더 우측)
        GameObject newMsgBtn = CreateButton(header.transform, "NewMessageBtn", "+", 40, 40);
        RectTransform btnRt = newMsgBtn.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1, 0.5f);
        btnRt.anchorMax = new Vector2(1, 0.5f);
        btnRt.pivot = new Vector2(1, 0.5f);
        btnRt.anchoredPosition = new Vector2(-20, 0);

        // 스크롤 영역
        GameObject scrollArea = CreateScrollView(panel.transform, "InboxScroll");
        RectTransform scrollRt = scrollArea.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(0, 0);
        scrollRt.offsetMax = new Vector2(0, -60); // 헤더 공간

        return panel;
    }

    private GameObject CreateConversationPanel()
    {
        // 대화 패널
        GameObject panel = CreatePanel("ConversationPanel", targetCanvas.transform);
        RectTransform rt = panel.GetComponent<RectTransform>();
        SetFullScreen(rt);

        Image bg = panel.GetComponent<Image>();
        bg.color = primaryColor;

        // 헤더 (뒤로가기 + 사용자명 + 아바타)
        GameObject header = CreateConversationHeader(panel.transform);

        // 메시지 영역 (스크롤)
        GameObject scrollArea = CreateScrollView(panel.transform, "MessageScroll");
        RectTransform scrollRt = scrollArea.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(0, 60); // 입력창 공간
        scrollRt.offsetMax = new Vector2(0, -60); // 헤더 공간

        // 입력 영역
        GameObject inputArea = CreateInputArea(panel.transform);

        return panel;
    }

    private GameObject CreateFollowingSelectPanel()
    {
        // 팔로잉 선택 패널
        GameObject panel = CreatePanel("FollowingSelectPanel", targetCanvas.transform);
        RectTransform rt = panel.GetComponent<RectTransform>();
        SetFullScreen(rt);

        Image bg = panel.GetComponent<Image>();
        bg.color = primaryColor;

        // 헤더
        GameObject header = CreateHeader(panel.transform, "메시지 보내기", true);

        // 스크롤 영역
        GameObject scrollArea = CreateScrollView(panel.transform, "FollowingScroll");
        RectTransform scrollRt = scrollArea.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(0, 0);
        scrollRt.offsetMax = new Vector2(0, -60);

        return panel;
    }

    private GameObject CreateHeader(Transform parent, string title, bool hasBackButton)
    {
        GameObject header = new GameObject("Header");
        header.transform.SetParent(parent, false);

        RectTransform rt = header.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, 60);

        Image bg = header.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // 뒤로가기 버튼
        if (hasBackButton)
        {
            GameObject backBtn = CreateButton(header.transform, "BackBtn", "<", 40, 40);
            RectTransform backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0, 0.5f);
            backRt.anchorMax = new Vector2(0, 0.5f);
            backRt.pivot = new Vector2(0, 0.5f);
            backRt.anchoredPosition = new Vector2(10, 0);
        }

        // 타이틀
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(header.transform, false);

        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(200, 30);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = title;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = textColor;
        titleText.alignment = TextAnchor.MiddleCenter;

        return header;
    }

    private GameObject CreateConversationHeader(Transform parent)
    {
        GameObject header = CreateHeader(parent, "", true);

        // 아바타
        GameObject avatarObj = new GameObject("ConversationAvatar");
        avatarObj.transform.SetParent(header.transform, false);

        RectTransform avatarRt = avatarObj.AddComponent<RectTransform>();
        avatarRt.anchorMin = new Vector2(0, 0.5f);
        avatarRt.anchorMax = new Vector2(0, 0.5f);
        avatarRt.pivot = new Vector2(0, 0.5f);
        avatarRt.anchoredPosition = new Vector2(60, 0);
        avatarRt.sizeDelta = new Vector2(40, 40);

        Image avatarImg = avatarObj.AddComponent<Image>();
        avatarImg.color = Color.gray;

        // Mask for circular avatar
        Mask mask = avatarObj.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // 사용자명
        GameObject titleObj = header.transform.Find("Title").gameObject;
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0.5f);
        titleRt.anchorMax = new Vector2(0, 0.5f);
        titleRt.pivot = new Vector2(0, 0.5f);
        titleRt.anchoredPosition = new Vector2(110, 0);
        titleRt.sizeDelta = new Vector2(200, 30);

        Text titleText = titleObj.GetComponent<Text>();
        titleText.alignment = TextAnchor.MiddleLeft;

        return header;
    }

    private GameObject CreateInputArea(Transform parent)
    {
        GameObject inputArea = new GameObject("InputArea");
        inputArea.transform.SetParent(parent, false);

        RectTransform rt = inputArea.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, 60);

        Image bg = inputArea.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // 입력 필드
        GameObject inputFieldObj = new GameObject("MessageInput");
        inputFieldObj.transform.SetParent(inputArea.transform, false);

        RectTransform inputRt = inputFieldObj.AddComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0, 0.5f);
        inputRt.anchorMax = new Vector2(1, 0.5f);
        inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.offsetMin = new Vector2(15, -20);
        inputRt.offsetMax = new Vector2(-70, 20);

        Image inputBg = inputFieldObj.AddComponent<Image>();
        inputBg.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        // Input Field Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inputFieldObj.transform, false);

        RectTransform textRt = textObj.AddComponent<RectTransform>();
        SetFullScreen(textRt);
        textRt.offsetMin = new Vector2(10, 5);
        textRt.offsetMax = new Vector2(-10, -5);

        Text inputText = textObj.AddComponent<Text>();
        inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        inputText.fontSize = 14;
        inputText.color = textColor;
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.supportRichText = false;

        // Placeholder
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputFieldObj.transform, false);

        RectTransform placeholderRt = placeholderObj.AddComponent<RectTransform>();
        SetFullScreen(placeholderRt);
        placeholderRt.offsetMin = new Vector2(10, 5);
        placeholderRt.offsetMax = new Vector2(-10, -5);

        Text placeholderText = placeholderObj.AddComponent<Text>();
        placeholderText.text = "메시지 입력...";
        placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholderText.fontSize = 14;
        placeholderText.fontStyle = FontStyle.Italic;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        placeholderText.alignment = TextAnchor.MiddleLeft;

        InputField inputField = inputFieldObj.AddComponent<InputField>();
        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;

        // 전송 버튼
        GameObject sendBtn = CreateButton(inputArea.transform, "SendBtn", "→", 50, 40);
        RectTransform sendRt = sendBtn.GetComponent<RectTransform>();
        sendRt.anchorMin = new Vector2(1, 0.5f);
        sendRt.anchorMax = new Vector2(1, 0.5f);
        sendRt.pivot = new Vector2(1, 0.5f);
        sendRt.anchoredPosition = new Vector2(-10, 0);

        Image sendBg = sendBtn.GetComponent<Image>();
        sendBg.color = goldColor;

        Text sendText = sendBtn.GetComponentInChildren<Text>();
        sendText.color = Color.black;
        sendText.fontStyle = FontStyle.Bold;

        return inputArea;
    }

    private GameObject CreateScrollView(Transform parent, string name)
    {
        GameObject scrollView = new GameObject(name);
        scrollView.transform.SetParent(parent, false);

        RectTransform scrollRt = scrollView.AddComponent<RectTransform>();
        SetFullScreen(scrollRt);

        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;

        Image scrollBg = scrollView.AddComponent<Image>();
        scrollBg.color = Color.clear;

        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);

        RectTransform viewportRt = viewport.AddComponent<RectTransform>();
        SetFullScreen(viewportRt);

        Image viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = Color.clear;

        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 10;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;

        return scrollView;
    }

    private GameObject CreatePanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        RectTransform rt = panel.AddComponent<RectTransform>();
        Image img = panel.AddComponent<Image>();

        return panel;
    }

    private GameObject CreateButton(Transform parent, string name, string text, float width, float height)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);

        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bg;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.AddComponent<RectTransform>();
        SetFullScreen(textRt);

        Text btnText = textObj.AddComponent<Text>();
        btnText.text = text;
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 16;
        btnText.color = textColor;
        btnText.alignment = TextAnchor.MiddleCenter;

        return btnObj;
    }

    private void SetFullScreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void CreatePrefabs()
    {
        // 프리팹 저장 폴더 확인/생성
        string prefabPath = "Assets/Prefabs/DM";
        if (!AssetDatabase.IsValidFolder(prefabPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "DM");
        }

        // 1. DM 아이템 프리팹 (받은 메시지 리스트용)
        CreateDMItemPrefab(prefabPath);

        // 2. 내 메시지 버블 프리팹
        CreateMyMessagePrefab(prefabPath);

        // 3. 상대 메시지 버블 프리팹
        CreateOtherMessagePrefab(prefabPath);

        // 4. 팔로잉 아이템 프리팹
        CreateFollowingItemPrefab(prefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("완료",
            $"프리팹이 생성되었습니다.\n경로: {prefabPath}",
            "확인");
    }

    private void CreateDMItemPrefab(string path)
    {
        GameObject item = new GameObject("DMItem");

        RectTransform rt = item.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 70);

        Image bg = item.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        Button btn = item.AddComponent<Button>();
        btn.targetGraphic = bg;

        // 아바타
        GameObject avatar = new GameObject("Avatar");
        avatar.transform.SetParent(item.transform, false);
        RectTransform avatarRt = avatar.AddComponent<RectTransform>();
        avatarRt.anchorMin = new Vector2(0, 0.5f);
        avatarRt.anchorMax = new Vector2(0, 0.5f);
        avatarRt.pivot = new Vector2(0, 0.5f);
        avatarRt.anchoredPosition = new Vector2(15, 0);
        avatarRt.sizeDelta = new Vector2(50, 50);
        Image avatarImg = avatar.AddComponent<Image>();
        avatarImg.color = Color.gray;

        // 사용자명
        GameObject username = new GameObject("UsernameText");
        username.transform.SetParent(item.transform, false);
        RectTransform usernameRt = username.AddComponent<RectTransform>();
        usernameRt.anchorMin = new Vector2(0, 0.5f);
        usernameRt.anchorMax = new Vector2(1, 0.5f);
        usernameRt.pivot = new Vector2(0, 0.5f);
        usernameRt.anchoredPosition = new Vector2(75, 12);
        usernameRt.sizeDelta = new Vector2(-150, 20);
        Text usernameText = username.AddComponent<Text>();
        usernameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        usernameText.fontSize = 14;
        usernameText.fontStyle = FontStyle.Bold;
        usernameText.color = textColor;

        // 메시지 내용
        GameObject content = new GameObject("ContentText");
        content.transform.SetParent(item.transform, false);
        RectTransform contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 0.5f);
        contentRt.anchorMax = new Vector2(1, 0.5f);
        contentRt.pivot = new Vector2(0, 0.5f);
        contentRt.anchoredPosition = new Vector2(75, -10);
        contentRt.sizeDelta = new Vector2(-150, 20);
        Text contentText = content.AddComponent<Text>();
        contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        contentText.fontSize = 12;
        contentText.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        // 시간
        GameObject time = new GameObject("TimeText");
        time.transform.SetParent(item.transform, false);
        RectTransform timeRt = time.AddComponent<RectTransform>();
        timeRt.anchorMin = new Vector2(1, 0.5f);
        timeRt.anchorMax = new Vector2(1, 0.5f);
        timeRt.pivot = new Vector2(1, 0.5f);
        timeRt.anchoredPosition = new Vector2(-15, 12);
        timeRt.sizeDelta = new Vector2(60, 20);
        Text timeText = time.AddComponent<Text>();
        timeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timeText.fontSize = 10;
        timeText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        timeText.alignment = TextAnchor.MiddleRight;

        // 읽지 않음 표시
        GameObject unreadDot = new GameObject("UnreadDot");
        unreadDot.transform.SetParent(item.transform, false);
        RectTransform unreadRt = unreadDot.AddComponent<RectTransform>();
        unreadRt.anchorMin = new Vector2(1, 0.5f);
        unreadRt.anchorMax = new Vector2(1, 0.5f);
        unreadRt.pivot = new Vector2(1, 0.5f);
        unreadRt.anchoredPosition = new Vector2(-15, -10);
        unreadRt.sizeDelta = new Vector2(10, 10);
        Image unreadImg = unreadDot.AddComponent<Image>();
        unreadImg.color = goldColor;

        // 프리팹 저장
        string prefabFilePath = $"{path}/DMItem.prefab";
        PrefabUtility.SaveAsPrefabAsset(item, prefabFilePath);
        DestroyImmediate(item);
    }

    private void CreateMyMessagePrefab(string path)
    {
        GameObject item = new GameObject("MyMessageBubble");

        RectTransform rt = item.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 50);

        HorizontalLayoutGroup hlg = item.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 5;
        hlg.padding = new RectOffset(50, 10, 5, 5);

        ContentSizeFitter csf = item.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 시간 + 읽음
        GameObject timeRead = new GameObject("TimeRead");
        timeRead.transform.SetParent(item.transform, false);
        RectTransform trRt = timeRead.AddComponent<RectTransform>();
        trRt.sizeDelta = new Vector2(50, 30);

        VerticalLayoutGroup trVlg = timeRead.AddComponent<VerticalLayoutGroup>();
        trVlg.childAlignment = TextAnchor.LowerRight;

        // 읽음 표시
        GameObject readObj = new GameObject("ReadText");
        readObj.transform.SetParent(timeRead.transform, false);
        Text readText = readObj.AddComponent<Text>();
        readText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        readText.fontSize = 10;
        readText.color = goldColor;
        readText.alignment = TextAnchor.LowerRight;
        readText.text = "읽음";

        // 시간
        GameObject timeObj = new GameObject("TimeText");
        timeObj.transform.SetParent(timeRead.transform, false);
        Text timeText = timeObj.AddComponent<Text>();
        timeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timeText.fontSize = 10;
        timeText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        timeText.alignment = TextAnchor.LowerRight;

        // 메시지 버블
        GameObject bubble = new GameObject("Bubble");
        bubble.transform.SetParent(item.transform, false);
        RectTransform bubbleRt = bubble.AddComponent<RectTransform>();

        Image bubbleBg = bubble.AddComponent<Image>();
        bubbleBg.color = myMessageColor;

        LayoutElement le = bubble.AddComponent<LayoutElement>();
        le.preferredWidth = 200;
        le.flexibleWidth = 0;

        ContentSizeFitter bubbleCsf = bubble.AddComponent<ContentSizeFitter>();
        bubbleCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bubbleCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 메시지 텍스트
        GameObject contentObj = new GameObject("ContentText");
        contentObj.transform.SetParent(bubble.transform, false);
        RectTransform contentRt = contentObj.AddComponent<RectTransform>();

        Text contentText = contentObj.AddComponent<Text>();
        contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        contentText.fontSize = 14;
        contentText.color = Color.black;

        LayoutElement contentLe = contentObj.AddComponent<LayoutElement>();
        contentLe.preferredWidth = 180;

        ContentSizeFitter contentCsf = contentObj.AddComponent<ContentSizeFitter>();
        contentCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Bubble padding
        HorizontalLayoutGroup bubbleHlg = bubble.AddComponent<HorizontalLayoutGroup>();
        bubbleHlg.padding = new RectOffset(10, 10, 8, 8);

        string prefabFilePath = $"{path}/MyMessageBubble.prefab";
        PrefabUtility.SaveAsPrefabAsset(item, prefabFilePath);
        DestroyImmediate(item);
    }

    private void CreateOtherMessagePrefab(string path)
    {
        GameObject item = new GameObject("OtherMessageBubble");

        RectTransform rt = item.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 50);

        HorizontalLayoutGroup hlg = item.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 5;
        hlg.padding = new RectOffset(10, 50, 5, 5);

        ContentSizeFitter csf = item.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 메시지 버블
        GameObject bubble = new GameObject("Bubble");
        bubble.transform.SetParent(item.transform, false);
        RectTransform bubbleRt = bubble.AddComponent<RectTransform>();

        Image bubbleBg = bubble.AddComponent<Image>();
        bubbleBg.color = otherMessageColor;

        LayoutElement le = bubble.AddComponent<LayoutElement>();
        le.preferredWidth = 200;
        le.flexibleWidth = 0;

        ContentSizeFitter bubbleCsf = bubble.AddComponent<ContentSizeFitter>();
        bubbleCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bubbleCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 메시지 텍스트
        GameObject contentObj = new GameObject("ContentText");
        contentObj.transform.SetParent(bubble.transform, false);

        Text contentText = contentObj.AddComponent<Text>();
        contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        contentText.fontSize = 14;
        contentText.color = textColor;

        LayoutElement contentLe = contentObj.AddComponent<LayoutElement>();
        contentLe.preferredWidth = 180;

        ContentSizeFitter contentCsf = contentObj.AddComponent<ContentSizeFitter>();
        contentCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        HorizontalLayoutGroup bubbleHlg = bubble.AddComponent<HorizontalLayoutGroup>();
        bubbleHlg.padding = new RectOffset(10, 10, 8, 8);

        // 시간
        GameObject timeObj = new GameObject("TimeText");
        timeObj.transform.SetParent(item.transform, false);
        RectTransform timeRt = timeObj.AddComponent<RectTransform>();
        timeRt.sizeDelta = new Vector2(40, 20);

        Text timeText = timeObj.AddComponent<Text>();
        timeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timeText.fontSize = 10;
        timeText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        timeText.alignment = TextAnchor.LowerLeft;

        string prefabFilePath = $"{path}/OtherMessageBubble.prefab";
        PrefabUtility.SaveAsPrefabAsset(item, prefabFilePath);
        DestroyImmediate(item);
    }

    private void CreateFollowingItemPrefab(string path)
    {
        GameObject item = new GameObject("FollowingItem");

        RectTransform rt = item.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 60);

        Image bg = item.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        Button btn = item.AddComponent<Button>();
        btn.targetGraphic = bg;

        // 아바타
        GameObject avatar = new GameObject("Avatar");
        avatar.transform.SetParent(item.transform, false);
        RectTransform avatarRt = avatar.AddComponent<RectTransform>();
        avatarRt.anchorMin = new Vector2(0, 0.5f);
        avatarRt.anchorMax = new Vector2(0, 0.5f);
        avatarRt.pivot = new Vector2(0, 0.5f);
        avatarRt.anchoredPosition = new Vector2(15, 0);
        avatarRt.sizeDelta = new Vector2(45, 45);
        Image avatarImg = avatar.AddComponent<Image>();
        avatarImg.color = Color.gray;

        // 사용자명
        GameObject username = new GameObject("UsernameText");
        username.transform.SetParent(item.transform, false);
        RectTransform usernameRt = username.AddComponent<RectTransform>();
        usernameRt.anchorMin = new Vector2(0, 0.5f);
        usernameRt.anchorMax = new Vector2(1, 0.5f);
        usernameRt.pivot = new Vector2(0, 0.5f);
        usernameRt.anchoredPosition = new Vector2(75, 0);
        usernameRt.sizeDelta = new Vector2(-100, 25);
        Text usernameText = username.AddComponent<Text>();
        usernameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        usernameText.fontSize = 15;
        usernameText.fontStyle = FontStyle.Bold;
        usernameText.color = textColor;

        // 메시지 아이콘
        GameObject msgIcon = new GameObject("MessageIcon");
        msgIcon.transform.SetParent(item.transform, false);
        RectTransform iconRt = msgIcon.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(1, 0.5f);
        iconRt.anchorMax = new Vector2(1, 0.5f);
        iconRt.pivot = new Vector2(1, 0.5f);
        iconRt.anchoredPosition = new Vector2(-15, 0);
        iconRt.sizeDelta = new Vector2(30, 30);
        Text iconText = msgIcon.AddComponent<Text>();
        iconText.text = "✉";
        iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iconText.fontSize = 20;
        iconText.color = goldColor;
        iconText.alignment = TextAnchor.MiddleCenter;

        string prefabFilePath = $"{path}/FollowingItem.prefab";
        PrefabUtility.SaveAsPrefabAsset(item, prefabFilePath);
        DestroyImmediate(item);
    }

    private void ConnectPrefabs(DirectMessageManager manager)
    {
        string prefabPath = "Assets/Prefabs/DM";

        // 프리팹 로드 및 연결
        manager.dmItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{prefabPath}/DMItem.prefab");
        manager.myMessagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{prefabPath}/MyMessageBubble.prefab");
        manager.otherMessagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{prefabPath}/OtherMessageBubble.prefab");
        manager.followingItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{prefabPath}/FollowingItem.prefab");

        // UI 요소 연결
        if (manager.dmPanel != null)
        {
            Transform inboxScroll = manager.dmPanel.transform.Find("InboxScroll");
            if (inboxScroll != null)
            {
                manager.inboxContent = inboxScroll.Find("Viewport/Content");
            }

            Transform newMsgBtn = manager.dmPanel.transform.Find("Header/NewMessageBtn");
            if (newMsgBtn != null)
            {
                Button btn = newMsgBtn.GetComponent<Button>();
                btn.onClick.AddListener(() => manager.OpenFollowingSelect());
            }
        }

        if (manager.conversationPanel != null)
        {
            Transform msgScroll = manager.conversationPanel.transform.Find("MessageScroll");
            if (msgScroll != null)
            {
                manager.messageContent = msgScroll.Find("Viewport/Content");
                manager.scrollRect = msgScroll.GetComponent<ScrollRect>();
            }

            Transform inputArea = manager.conversationPanel.transform.Find("InputArea");
            if (inputArea != null)
            {
                manager.messageInput = inputArea.Find("MessageInput")?.GetComponent<InputField>();
                manager.sendButton = inputArea.Find("SendBtn")?.GetComponent<Button>();
            }

            Transform header = manager.conversationPanel.transform.Find("Header");
            if (header != null)
            {
                manager.conversationTitle = header.Find("Title")?.GetComponent<Text>();
                manager.conversationAvatar = header.Find("ConversationAvatar")?.GetComponent<Image>();
                manager.backButton = header.Find("BackBtn")?.GetComponent<Button>();
            }
        }

        if (manager.followingSelectPanel != null)
        {
            Transform followingScroll = manager.followingSelectPanel.transform.Find("FollowingScroll");
            if (followingScroll != null)
            {
                manager.followingListContent = followingScroll.Find("Viewport/Content");
            }
        }

        EditorUtility.SetDirty(manager);
    }
}
