#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

/// <summary>
/// 모던 채팅 패널 자동 생성 에디터 스크립트
/// 2025 UI 트렌드 적용: 다크 테마, 미니멀 디자인
/// </summary>
public class ChatPanelSetup : EditorWindow
{
    // ============================================================
    // 색상 팔레트 (2025 다크 테마)
    // ============================================================
    private static readonly Color BG_COLOR = new Color(0.07f, 0.07f, 0.09f, 1f);           // #121217
    private static readonly Color HEADER_COLOR = new Color(0.1f, 0.1f, 0.12f, 1f);         // #1A1A1F
    private static readonly Color INPUT_AREA_COLOR = new Color(0.1f, 0.1f, 0.12f, 1f);     // #1A1A1F
    private static readonly Color MY_BUBBLE_COLOR = new Color(0.2f, 0.5f, 1f, 1f);         // 파랑
    private static readonly Color OTHER_BUBBLE_COLOR = new Color(0.15f, 0.15f, 0.18f, 1f); // #262630
    private static readonly Color TEXT_COLOR = new Color(0.94f, 0.94f, 0.96f, 1f);         // #F0F0F5
    private static readonly Color SUB_TEXT_COLOR = new Color(0.5f, 0.5f, 0.55f, 1f);       // #808090
    private static readonly Color ACCENT_COLOR = new Color(0.4f, 0.6f, 1f, 1f);            // 밝은 파랑
    private static readonly Color INPUT_FIELD_COLOR = new Color(0.12f, 0.12f, 0.15f, 1f);  // #1F1F26
    private static readonly Color SEND_BTN_COLOR = new Color(0.3f, 0.55f, 1f, 1f);         // 전송 버튼

    [MenuItem("WOOPANG/Create Chat Panel (Modern Dark UI)")]
    public static void CreateChatPanel()
    {
        // Canvas 찾기
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "씬에 Canvas가 없습니다. Canvas를 먼저 생성해주세요.", "OK");
            return;
        }

        // 폰트 로드
        Font font = LoadFont();

        // 메인 채팅 패널 생성
        GameObject chatPanel = CreateMainPanel(canvas.transform);

        // 헤더 영역
        RectTransform header = CreateHeader(chatPanel.transform, font);

        // 메시지 스크롤 영역
        RectTransform messageArea = CreateMessageArea(chatPanel.transform);

        // 입력 영역
        RectTransform inputArea = CreateInputArea(chatPanel.transform, font);

        // ChatPanelManager 컴포넌트 추가 및 연결
        SetupManager(chatPanel, header, messageArea, inputArea);

        // 메시지 버블 프리팹 생성
        CreateBubblePrefabs(font);

        Selection.activeGameObject = chatPanel;
        EditorUtility.DisplayDialog("Success", "채팅 패널이 생성되었습니다!\n\n위치: Canvas의 맨 위\n\n프리팹 위치:\n- Assets/Prefabs/DM/ModernMyBubble.prefab\n- Assets/Prefabs/DM/ModernOtherBubble.prefab\n- Assets/Prefabs/DM/DateSeparator.prefab", "OK");
    }

    private static Font LoadFont()
    {
        // 프로젝트에서 폰트 찾기
        string[] fontGuids = AssetDatabase.FindAssets("AppleSDGothicNeoM t:Font");
        if (fontGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(fontGuids[0]);
            return AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        // 대체 폰트
        fontGuids = AssetDatabase.FindAssets("NotoSans t:Font");
        if (fontGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(fontGuids[0]);
            return AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static Sprite LoadRoundedSprite()
    {
        // 라운드 스프라이트 찾기
        string[] spriteGuids = AssetDatabase.FindAssets("UISprite t:Sprite");
        foreach (var guid in spriteGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Rounded") || path.Contains("rounded"))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static GameObject CreateMainPanel(Transform parent)
    {
        GameObject panel = new GameObject("ChatConversationPanel");
        panel.transform.SetParent(parent, false);
        panel.transform.SetAsFirstSibling(); // 맨 위에 배치

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = BG_COLOR;
        bg.raycastTarget = true;

        // CanvasGroup 추가 (애니메이션용)
        panel.AddComponent<CanvasGroup>();

        panel.SetActive(false); // 초기에는 비활성화

        return panel;
    }

    private static RectTransform CreateHeader(Transform parent, Font font)
    {
        // 헤더 컨테이너
        GameObject header = new GameObject("Header");
        header.transform.SetParent(parent, false);

        RectTransform headerRect = header.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0, 90);

        Image headerBg = header.AddComponent<Image>();
        headerBg.color = HEADER_COLOR;

        // Safe Area 패딩 (상단 노치 대응)
        GameObject safeArea = new GameObject("SafeAreaPadding");
        safeArea.transform.SetParent(header.transform, false);
        RectTransform safeRect = safeArea.AddComponent<RectTransform>();
        safeRect.anchorMin = new Vector2(0, 1);
        safeRect.anchorMax = new Vector2(1, 1);
        safeRect.pivot = new Vector2(0.5f, 1);
        safeRect.anchoredPosition = Vector2.zero;
        safeRect.sizeDelta = new Vector2(0, 44);

        // 뒤로가기 버튼
        GameObject backBtn = new GameObject("BackButton");
        backBtn.transform.SetParent(header.transform, false);

        RectTransform backRect = backBtn.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0, 0.5f);
        backRect.anchorMax = new Vector2(0, 0.5f);
        backRect.pivot = new Vector2(0, 0.5f);
        backRect.anchoredPosition = new Vector2(16, -10);
        backRect.sizeDelta = new Vector2(44, 44);

        Image backBg = backBtn.AddComponent<Image>();
        backBg.color = new Color(1, 1, 1, 0); // 투명
        backBg.raycastTarget = true;

        Button backButton = backBtn.AddComponent<Button>();
        backButton.transition = Selectable.Transition.ColorTint;

        // 뒤로가기 아이콘 (< 텍스트로 대체)
        GameObject backIcon = new GameObject("Icon");
        backIcon.transform.SetParent(backBtn.transform, false);
        RectTransform iconRect = backIcon.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Text backText = backIcon.AddComponent<Text>();
        backText.text = "<";
        backText.font = font;
        backText.fontSize = 28;
        backText.color = TEXT_COLOR;
        backText.alignment = TextAnchor.MiddleCenter;

        // 아바타
        GameObject avatar = new GameObject("Avatar");
        avatar.transform.SetParent(header.transform, false);

        RectTransform avatarRect = avatar.AddComponent<RectTransform>();
        avatarRect.anchorMin = new Vector2(0, 0.5f);
        avatarRect.anchorMax = new Vector2(0, 0.5f);
        avatarRect.pivot = new Vector2(0, 0.5f);
        avatarRect.anchoredPosition = new Vector2(70, -10);
        avatarRect.sizeDelta = new Vector2(44, 44);

        Image avatarImg = avatar.AddComponent<Image>();
        avatarImg.color = OTHER_BUBBLE_COLOR;

        // 원형 마스크
        Mask avatarMask = avatar.AddComponent<Mask>();
        avatarMask.showMaskGraphic = true;

        // 사용자 이름
        GameObject nameObj = new GameObject("Username");
        nameObj.transform.SetParent(header.transform, false);

        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.5f);
        nameRect.anchorMax = new Vector2(1, 0.5f);
        nameRect.pivot = new Vector2(0, 0.5f);
        nameRect.anchoredPosition = new Vector2(124, -2);
        nameRect.sizeDelta = new Vector2(-180, 24);

        Text nameText = nameObj.AddComponent<Text>();
        nameText.text = "사용자 이름";
        nameText.font = font;
        nameText.fontSize = 18;
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = TEXT_COLOR;
        nameText.alignment = TextAnchor.MiddleLeft;

        // 온라인 상태
        GameObject statusObj = new GameObject("OnlineStatus");
        statusObj.transform.SetParent(header.transform, false);

        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0.5f);
        statusRect.anchorMax = new Vector2(1, 0.5f);
        statusRect.pivot = new Vector2(0, 0.5f);
        statusRect.anchoredPosition = new Vector2(124, -24);
        statusRect.sizeDelta = new Vector2(-180, 20);

        Text statusText = statusObj.AddComponent<Text>();
        statusText.text = "온라인";
        statusText.font = font;
        statusText.fontSize = 13;
        statusText.color = ACCENT_COLOR;
        statusText.alignment = TextAnchor.MiddleLeft;

        // 더보기 버튼
        GameObject moreBtn = new GameObject("MoreButton");
        moreBtn.transform.SetParent(header.transform, false);

        RectTransform moreRect = moreBtn.AddComponent<RectTransform>();
        moreRect.anchorMin = new Vector2(1, 0.5f);
        moreRect.anchorMax = new Vector2(1, 0.5f);
        moreRect.pivot = new Vector2(1, 0.5f);
        moreRect.anchoredPosition = new Vector2(-16, -10);
        moreRect.sizeDelta = new Vector2(44, 44);

        Image moreBg = moreBtn.AddComponent<Image>();
        moreBg.color = new Color(1, 1, 1, 0);
        moreBg.raycastTarget = true;

        Button moreButton = moreBtn.AddComponent<Button>();

        GameObject moreIcon = new GameObject("Icon");
        moreIcon.transform.SetParent(moreBtn.transform, false);
        RectTransform moreIconRect = moreIcon.AddComponent<RectTransform>();
        moreIconRect.anchorMin = Vector2.zero;
        moreIconRect.anchorMax = Vector2.one;
        moreIconRect.offsetMin = Vector2.zero;
        moreIconRect.offsetMax = Vector2.zero;

        Text moreText = moreIcon.AddComponent<Text>();
        moreText.text = "⋮";
        moreText.font = font;
        moreText.fontSize = 24;
        moreText.color = TEXT_COLOR;
        moreText.alignment = TextAnchor.MiddleCenter;

        return headerRect;
    }

    private static RectTransform CreateMessageArea(Transform parent)
    {
        // 스크롤 뷰
        GameObject scrollView = new GameObject("MessageScrollView");
        scrollView.transform.SetParent(parent, false);

        RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(0, 70); // 입력창 높이
        scrollRect.offsetMax = new Vector2(0, -90); // 헤더 높이

        Image scrollBg = scrollView.AddComponent<Image>();
        scrollBg.color = new Color(0, 0, 0, 0); // 투명
        scrollBg.raycastTarget = true;

        ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.1f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 20f;

        // 뷰포트
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportRect.pivot = new Vector2(0, 1);

        Image viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = Color.white;
        viewportImg.raycastTarget = true;

        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        // 콘텐츠
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ScrollRect 연결
        scroll.viewport = viewportRect;
        scroll.content = contentRect;

        return scrollRect;
    }

    private static RectTransform CreateInputArea(Transform parent, Font font)
    {
        // 입력 영역 컨테이너
        GameObject inputArea = new GameObject("InputArea");
        inputArea.transform.SetParent(parent, false);

        RectTransform inputRect = inputArea.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0, 0);
        inputRect.anchorMax = new Vector2(1, 0);
        inputRect.pivot = new Vector2(0.5f, 0);
        inputRect.anchoredPosition = Vector2.zero;
        inputRect.sizeDelta = new Vector2(0, 70);

        Image inputBg = inputArea.AddComponent<Image>();
        inputBg.color = INPUT_AREA_COLOR;

        // 가로 레이아웃
        HorizontalLayoutGroup hLayout = inputArea.AddComponent<HorizontalLayoutGroup>();
        hLayout.padding = new RectOffset(12, 12, 10, 20);
        hLayout.spacing = 10;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;

        // 첨부 버튼
        GameObject attachBtn = new GameObject("AttachButton");
        attachBtn.transform.SetParent(inputArea.transform, false);

        RectTransform attachRect = attachBtn.AddComponent<RectTransform>();
        attachRect.sizeDelta = new Vector2(40, 40);

        Image attachBg = attachBtn.AddComponent<Image>();
        attachBg.color = new Color(1, 1, 1, 0);

        Button attachButton = attachBtn.AddComponent<Button>();

        LayoutElement attachLayout = attachBtn.AddComponent<LayoutElement>();
        attachLayout.preferredWidth = 40;
        attachLayout.preferredHeight = 40;

        GameObject attachIcon = new GameObject("Icon");
        attachIcon.transform.SetParent(attachBtn.transform, false);
        RectTransform attachIconRect = attachIcon.AddComponent<RectTransform>();
        attachIconRect.anchorMin = Vector2.zero;
        attachIconRect.anchorMax = Vector2.one;
        attachIconRect.offsetMin = Vector2.zero;
        attachIconRect.offsetMax = Vector2.zero;

        Text attachText = attachIcon.AddComponent<Text>();
        attachText.text = "+";
        attachText.font = font;
        attachText.fontSize = 28;
        attachText.color = SUB_TEXT_COLOR;
        attachText.alignment = TextAnchor.MiddleCenter;

        // 입력 필드 컨테이너
        GameObject inputFieldBg = new GameObject("InputFieldBackground");
        inputFieldBg.transform.SetParent(inputArea.transform, false);

        RectTransform fieldBgRect = inputFieldBg.AddComponent<RectTransform>();

        Image fieldBgImg = inputFieldBg.AddComponent<Image>();
        fieldBgImg.color = INPUT_FIELD_COLOR;
        // 둥근 모서리 효과는 9-slice 스프라이트로 구현 가능

        LayoutElement fieldBgLayout = inputFieldBg.AddComponent<LayoutElement>();
        fieldBgLayout.flexibleWidth = 1;
        fieldBgLayout.preferredHeight = 40;
        fieldBgLayout.minHeight = 40;

        // 입력 필드
        GameObject inputFieldObj = new GameObject("MessageInputField");
        inputFieldObj.transform.SetParent(inputFieldBg.transform, false);

        RectTransform inputFieldRect = inputFieldObj.AddComponent<RectTransform>();
        inputFieldRect.anchorMin = Vector2.zero;
        inputFieldRect.anchorMax = Vector2.one;
        inputFieldRect.offsetMin = new Vector2(16, 0);
        inputFieldRect.offsetMax = new Vector2(-16, 0);

        // 플레이스홀더
        GameObject placeholder = new GameObject("Placeholder");
        placeholder.transform.SetParent(inputFieldObj.transform, false);

        RectTransform phRect = placeholder.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;

        Text phText = placeholder.AddComponent<Text>();
        phText.text = "메시지를 입력하세요...";
        phText.font = font;
        phText.fontSize = 15;
        phText.fontStyle = FontStyle.Italic;
        phText.color = SUB_TEXT_COLOR;
        phText.alignment = TextAnchor.MiddleLeft;

        // 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inputFieldObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text inputText = textObj.AddComponent<Text>();
        inputText.font = font;
        inputText.fontSize = 15;
        inputText.color = TEXT_COLOR;
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.supportRichText = false;

        // InputField 컴포넌트
        InputField inputField = inputFieldObj.AddComponent<InputField>();
        inputField.textComponent = inputText;
        inputField.placeholder = phText;
        inputField.characterLimit = 500;
        inputField.lineType = InputField.LineType.SingleLine;

        // 전송 버튼
        GameObject sendBtn = new GameObject("SendButton");
        sendBtn.transform.SetParent(inputArea.transform, false);

        RectTransform sendRect = sendBtn.AddComponent<RectTransform>();
        sendRect.sizeDelta = new Vector2(40, 40);

        Image sendBg = sendBtn.AddComponent<Image>();
        sendBg.color = SEND_BTN_COLOR;

        Button sendButton = sendBtn.AddComponent<Button>();
        sendButton.targetGraphic = sendBg;

        ColorBlock colors = sendButton.colors;
        colors.normalColor = SEND_BTN_COLOR;
        colors.highlightedColor = new Color(0.4f, 0.65f, 1f, 1f);
        colors.pressedColor = new Color(0.2f, 0.45f, 0.9f, 1f);
        sendButton.colors = colors;

        LayoutElement sendLayout = sendBtn.AddComponent<LayoutElement>();
        sendLayout.preferredWidth = 40;
        sendLayout.preferredHeight = 40;

        GameObject sendIcon = new GameObject("Icon");
        sendIcon.transform.SetParent(sendBtn.transform, false);
        RectTransform sendIconRect = sendIcon.AddComponent<RectTransform>();
        sendIconRect.anchorMin = Vector2.zero;
        sendIconRect.anchorMax = Vector2.one;
        sendIconRect.offsetMin = Vector2.zero;
        sendIconRect.offsetMax = Vector2.zero;

        Text sendText = sendIcon.AddComponent<Text>();
        sendText.text = "➤";
        sendText.font = font;
        sendText.fontSize = 20;
        sendText.color = Color.white;
        sendText.alignment = TextAnchor.MiddleCenter;

        return inputRect;
    }

    private static void SetupManager(GameObject chatPanel, RectTransform header, RectTransform messageArea, RectTransform inputArea)
    {
        ChatPanelManager manager = chatPanel.AddComponent<ChatPanelManager>();

        manager.chatPanel = chatPanel;
        manager.headerArea = header;
        manager.messageScrollArea = messageArea;
        manager.inputArea = inputArea;

        // ScrollRect 연결
        ScrollRect scrollRect = messageArea.GetComponentInParent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = chatPanel.GetComponentInChildren<ScrollRect>();
        manager.scrollRect = scrollRect;

        if (scrollRect != null)
            manager.contentTransform = scrollRect.content;

        // 버튼 연결
        Button[] buttons = chatPanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn.name == "BackButton")
                manager.backButton = btn;
            else if (btn.name == "SendButton")
                manager.sendButton = btn;
        }

        // InputField 연결
        InputField inputField = chatPanel.GetComponentInChildren<InputField>(true);
        manager.messageInputField = inputField;

        // 텍스트 연결
        Text[] texts = chatPanel.GetComponentsInChildren<Text>(true);
        foreach (var text in texts)
        {
            if (text.name == "Username")
                manager.headerTitle = text;
            else if (text.name == "OnlineStatus")
                manager.onlineStatus = text;
        }

        // 아바타 연결
        Transform avatarTrans = chatPanel.transform.Find("Header/Avatar");
        if (avatarTrans != null)
            manager.headerAvatar = avatarTrans.GetComponent<Image>();
    }

    private static void CreateBubblePrefabs(Font font)
    {
        string prefabPath = "Assets/Prefabs/DM";
        if (!Directory.Exists(prefabPath))
            Directory.CreateDirectory(prefabPath);

        // 내 메시지 버블
        CreateMyBubblePrefab(prefabPath, font);

        // 상대방 메시지 버블
        CreateOtherBubblePrefab(prefabPath, font);

        // 날짜 구분선
        CreateDateSeparatorPrefab(prefabPath, font);

        AssetDatabase.Refresh();
    }

    private static void CreateMyBubblePrefab(string path, Font font)
    {
        GameObject bubble = new GameObject("ModernMyBubble");

        RectTransform bubbleRect = bubble.AddComponent<RectTransform>();
        bubbleRect.sizeDelta = new Vector2(0, 0);

        HorizontalLayoutGroup hLayout = bubble.AddComponent<HorizontalLayoutGroup>();
        hLayout.padding = new RectOffset(60, 0, 4, 4);
        hLayout.spacing = 8;
        hLayout.childAlignment = TextAnchor.MiddleRight;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;

        ContentSizeFitter fitter = bubble.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 시간 텍스트
        GameObject timeObj = new GameObject("TimeText");
        timeObj.transform.SetParent(bubble.transform, false);

        RectTransform timeRect = timeObj.AddComponent<RectTransform>();

        Text timeText = timeObj.AddComponent<Text>();
        timeText.text = "오후 2:30";
        timeText.font = font;
        timeText.fontSize = 11;
        timeText.color = SUB_TEXT_COLOR;
        timeText.alignment = TextAnchor.LowerRight;

        LayoutElement timeLayout = timeObj.AddComponent<LayoutElement>();
        timeLayout.preferredWidth = 55;
        timeLayout.preferredHeight = 20;

        // 버블
        GameObject bubbleContainer = new GameObject("Bubble");
        bubbleContainer.transform.SetParent(bubble.transform, false);

        RectTransform containerRect = bubbleContainer.AddComponent<RectTransform>();

        Image bubbleBg = bubbleContainer.AddComponent<Image>();
        bubbleBg.color = MY_BUBBLE_COLOR;
        bubbleBg.type = Image.Type.Sliced;
        bubbleBg.pixelsPerUnitMultiplier = 1;

        LayoutElement containerLayout = bubbleContainer.AddComponent<LayoutElement>();
        containerLayout.preferredWidth = 200;
        containerLayout.flexibleWidth = 0;

        // 콘텐츠 텍스트
        GameObject contentObj = new GameObject("ContentText");
        contentObj.transform.SetParent(bubbleContainer.transform, false);

        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(14, 10);
        contentRect.offsetMax = new Vector2(-14, -10);

        Text contentText = contentObj.AddComponent<Text>();
        contentText.text = "안녕하세요!";
        contentText.font = font;
        contentText.fontSize = 15;
        contentText.color = Color.white;
        contentText.alignment = TextAnchor.MiddleLeft;

        // 프리팹 저장
        string prefabFile = path + "/ModernMyBubble.prefab";
        PrefabUtility.SaveAsPrefabAsset(bubble, prefabFile);
        DestroyImmediate(bubble);
    }

    private static void CreateOtherBubblePrefab(string path, Font font)
    {
        GameObject bubble = new GameObject("ModernOtherBubble");

        RectTransform bubbleRect = bubble.AddComponent<RectTransform>();
        bubbleRect.sizeDelta = new Vector2(0, 0);

        HorizontalLayoutGroup hLayout = bubble.AddComponent<HorizontalLayoutGroup>();
        hLayout.padding = new RectOffset(0, 60, 4, 4);
        hLayout.spacing = 8;
        hLayout.childAlignment = TextAnchor.MiddleLeft;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;

        ContentSizeFitter fitter = bubble.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 아바타
        GameObject avatarObj = new GameObject("Avatar");
        avatarObj.transform.SetParent(bubble.transform, false);

        RectTransform avatarRect = avatarObj.AddComponent<RectTransform>();

        Image avatarImg = avatarObj.AddComponent<Image>();
        avatarImg.color = OTHER_BUBBLE_COLOR;

        LayoutElement avatarLayout = avatarObj.AddComponent<LayoutElement>();
        avatarLayout.preferredWidth = 36;
        avatarLayout.preferredHeight = 36;

        // 버블
        GameObject bubbleContainer = new GameObject("Bubble");
        bubbleContainer.transform.SetParent(bubble.transform, false);

        RectTransform containerRect = bubbleContainer.AddComponent<RectTransform>();

        Image bubbleBg = bubbleContainer.AddComponent<Image>();
        bubbleBg.color = OTHER_BUBBLE_COLOR;

        LayoutElement containerLayout = bubbleContainer.AddComponent<LayoutElement>();
        containerLayout.preferredWidth = 200;
        containerLayout.flexibleWidth = 0;

        // 콘텐츠 텍스트
        GameObject contentObj = new GameObject("ContentText");
        contentObj.transform.SetParent(bubbleContainer.transform, false);

        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(14, 10);
        contentRect.offsetMax = new Vector2(-14, -10);

        Text contentText = contentObj.AddComponent<Text>();
        contentText.text = "안녕하세요!";
        contentText.font = font;
        contentText.fontSize = 15;
        contentText.color = TEXT_COLOR;
        contentText.alignment = TextAnchor.MiddleLeft;

        // 시간 텍스트
        GameObject timeObj = new GameObject("TimeText");
        timeObj.transform.SetParent(bubble.transform, false);

        RectTransform timeRect = timeObj.AddComponent<RectTransform>();

        Text timeText = timeObj.AddComponent<Text>();
        timeText.text = "오후 2:30";
        timeText.font = font;
        timeText.fontSize = 11;
        timeText.color = SUB_TEXT_COLOR;
        timeText.alignment = TextAnchor.LowerLeft;

        LayoutElement timeLayout = timeObj.AddComponent<LayoutElement>();
        timeLayout.preferredWidth = 55;
        timeLayout.preferredHeight = 20;

        // 프리팹 저장
        string prefabFile = path + "/ModernOtherBubble.prefab";
        PrefabUtility.SaveAsPrefabAsset(bubble, prefabFile);
        DestroyImmediate(bubble);
    }

    private static void CreateDateSeparatorPrefab(string path, Font font)
    {
        GameObject separator = new GameObject("DateSeparator");

        RectTransform sepRect = separator.AddComponent<RectTransform>();
        sepRect.sizeDelta = new Vector2(0, 40);

        LayoutElement layout = separator.AddComponent<LayoutElement>();
        layout.preferredHeight = 40;
        layout.flexibleWidth = 1;

        // 날짜 텍스트
        GameObject dateObj = new GameObject("DateText");
        dateObj.transform.SetParent(separator.transform, false);

        RectTransform dateRect = dateObj.AddComponent<RectTransform>();
        dateRect.anchorMin = new Vector2(0.5f, 0.5f);
        dateRect.anchorMax = new Vector2(0.5f, 0.5f);
        dateRect.pivot = new Vector2(0.5f, 0.5f);
        dateRect.anchoredPosition = Vector2.zero;
        dateRect.sizeDelta = new Vector2(150, 24);

        Image dateBg = dateObj.AddComponent<Image>();
        dateBg.color = new Color(0.15f, 0.15f, 0.18f, 0.8f);

        Text dateText = dateObj.AddComponent<Text>();
        dateText.text = "2026년 1월 18일";
        dateText.font = font;
        dateText.fontSize = 12;
        dateText.color = SUB_TEXT_COLOR;
        dateText.alignment = TextAnchor.MiddleCenter;

        // 프리팹 저장
        string prefabFile = path + "/DateSeparator.prefab";
        PrefabUtility.SaveAsPrefabAsset(separator, prefabFile);
        DestroyImmediate(separator);
    }
}
#endif
