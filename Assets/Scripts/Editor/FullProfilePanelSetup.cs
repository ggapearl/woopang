#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;

/// <summary>
/// FullProfilePanel prefab에 SNS 아이콘 컨테이너와 로그아웃 버튼을 추가하는 Editor 도구
/// </summary>
[InitializeOnLoad]
public class FullProfilePanelSetup : EditorWindow
{
    // SNS 아이콘 기본 사이즈 (ProfileManager에서 재정의 가능)
    private static readonly Vector2 DEFAULT_SNS_ICON_SIZE = new Vector2(50f, 50f);
    // 로그아웃 버튼 기본 사이즈 (ProfileManager에서 재정의 가능)
    private static readonly Vector2 DEFAULT_LOGOUT_BUTTON_SIZE = new Vector2(200f, 50f);
    private static readonly float DEFAULT_LOGOUT_BUTTON_Y = -520f;

    static FullProfilePanelSetup()
    {
        // 씬 열릴 때 자동으로 SNS 컨테이너 및 로그아웃 버튼 확인 및 추가
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndAddUIOnLoad;
    }

    // 스크립트 컴파일 완료 시 자동 실행
    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckAndAddUISilent;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CheckAndAddUISilent;
    }

    private static void CheckAndAddUIOnLoad()
    {
        CheckAndAddUISilent();
    }

    /// <summary>
    /// SNS 컨테이너, 로그아웃 버튼, 확인 다이얼로그를 BottomPanel 안에 추가 (조용히)
    /// </summary>
    private static void CheckAndAddUISilent()
    {
        // FullProfilePanel 찾기
        GameObject fullProfilePanel = GameObject.Find("FullProfilePanel");
        if (fullProfilePanel == null)
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                Transform found = canvas.transform.Find("FullProfilePanel");
                if (found != null)
                {
                    fullProfilePanel = found.gameObject;
                    break;
                }
            }
        }

        if (fullProfilePanel == null) return;

        Transform content = fullProfilePanel.transform.Find("Content");
        if (content == null) return;

        // BottomPanel 찾기 (모든 UI를 여기에 생성)
        Transform bottomPanel = content.Find("BottomPanel");
        if (bottomPanel == null) return;

        ProfileManager profileManager = Object.FindFirstObjectByType<ProfileManager>();
        bool sceneChanged = false;

        // SNS 컨테이너 추가 (BottomPanel 안에 생성)
        Transform snsContainer = bottomPanel.Find("SnsIconsContainer");
        if (snsContainer == null)
        {
            Debug.Log("[FullProfilePanelSetup] SNS 아이콘 컨테이너 자동 추가 중 (BottomPanel)...");

            GameObject container = CreateSnsIconsContainerStatic(bottomPanel);
            CreateSnsIconButtonStatic(container.transform, "Instagram", new Color(0.88f, 0.19f, 0.42f));
            CreateSnsIconButtonStatic(container.transform, "X", Color.black);
            CreateSnsIconButtonStatic(container.transform, "Facebook", new Color(0.23f, 0.35f, 0.60f));

            if (profileManager != null)
            {
                profileManager.snsIconsContainer = container;
                profileManager.instagramButton = container.transform.Find("InstagramButton")?.GetComponent<Button>();
                profileManager.xButton = container.transform.Find("XButton")?.GetComponent<Button>();
                profileManager.facebookButton = container.transform.Find("FacebookButton")?.GetComponent<Button>();
            }

            sceneChanged = true;
            Debug.Log("[FullProfilePanelSetup] SNS 아이콘 컨테이너 자동 추가 완료!");
        }
        else if (profileManager != null && profileManager.snsIconsContainer == null)
        {
            // 컨테이너는 있는데 연결 안 되어 있으면 연결
            profileManager.snsIconsContainer = snsContainer.gameObject;
            profileManager.instagramButton = snsContainer.Find("InstagramButton")?.GetComponent<Button>();
            profileManager.xButton = snsContainer.Find("XButton")?.GetComponent<Button>();
            profileManager.facebookButton = snsContainer.Find("FacebookButton")?.GetComponent<Button>();
            sceneChanged = true;
        }

        // 로그아웃 버튼 추가 (BottomPanel 안에 생성)
        Transform logoutBtnTransform = bottomPanel.Find("LogoutButton");
        if (logoutBtnTransform == null)
        {
            Debug.Log("[FullProfilePanelSetup] 로그아웃 버튼 자동 추가 중 (BottomPanel)...");

            GameObject logoutBtn = CreateLogoutButtonStatic(bottomPanel, profileManager);

            if (profileManager != null)
            {
                profileManager.logoutButton = logoutBtn.GetComponent<Button>();
            }

            sceneChanged = true;
            Debug.Log("[FullProfilePanelSetup] 로그아웃 버튼 자동 추가 완료!");
        }
        else if (profileManager != null && profileManager.logoutButton == null)
        {
            // 버튼은 있는데 연결 안 되어 있으면 연결
            profileManager.logoutButton = logoutBtnTransform.GetComponent<Button>();
            sceneChanged = true;
        }

        // 로그아웃 확인 다이얼로그 추가 (BottomPanel 안에 생성)
        Transform dialogTransform = bottomPanel.Find("LogoutConfirmDialog");
        if (dialogTransform == null)
        {
            Debug.Log("[FullProfilePanelSetup] 로그아웃 확인 다이얼로그 자동 추가 중...");
            GameObject dialog = CreateLogoutConfirmDialogStatic(bottomPanel, profileManager);
            dialog.transform.SetAsLastSibling();  // 맨 앞에 표시

            if (profileManager != null)
            {
                profileManager.logoutConfirmDialog = dialog;
                profileManager.logoutConfirmText = dialog.transform.Find("Panel/MessageText")?.GetComponent<Text>();
                profileManager.logoutConfirmButton = dialog.transform.Find("Panel/ConfirmButton")?.GetComponent<Button>();
                profileManager.logoutCancelButton = dialog.transform.Find("Panel/CancelButton")?.GetComponent<Button>();
            }

            sceneChanged = true;
            Debug.Log("[FullProfilePanelSetup] 로그아웃 확인 다이얼로그 자동 추가 완료!");
        }
        else if (profileManager != null && profileManager.logoutConfirmDialog == null)
        {
            // 다이얼로그는 있는데 연결 안 되어 있으면 연결
            profileManager.logoutConfirmDialog = dialogTransform.gameObject;
            profileManager.logoutConfirmText = dialogTransform.Find("Panel/MessageText")?.GetComponent<Text>();
            profileManager.logoutConfirmButton = dialogTransform.Find("Panel/ConfirmButton")?.GetComponent<Button>();
            profileManager.logoutCancelButton = dialogTransform.Find("Panel/CancelButton")?.GetComponent<Button>();
            sceneChanged = true;
        }

        // AvatarMask 버튼 → ProfileManager.avatarButton 자동 연결
        // 씬 구조: FullProfilePanel > Content > TopSection > AvatarMask (Button 컴포넌트)
        if (profileManager != null && profileManager.avatarButton == null)
        {
            Transform topSection = content.Find("TopSection");
            if (topSection != null)
            {
                Transform avatarMask = topSection.Find("AvatarMask");
                if (avatarMask != null)
                {
                    Button avatarBtn = avatarMask.GetComponent<Button>();
                    if (avatarBtn != null)
                    {
                        profileManager.avatarButton = avatarBtn;
                        sceneChanged = true;
                        Debug.Log("[FullProfilePanelSetup] avatarButton → AvatarMask 자동 연결 완료");
                    }
                }
            }
        }

        if (sceneChanged)
        {
            if (profileManager != null)
                EditorUtility.SetDirty(profileManager);
            EditorSceneManager.MarkSceneDirty(fullProfilePanel.scene);
        }
    }

    public static void ShowWindow()
    {
        GetWindow<FullProfilePanelSetup>("FullProfilePanel Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("FullProfilePanel SNS 아이콘 설정", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("SNS 아이콘 컨테이너 추가"))
        {
            AddSnsIconsContainerToPrefab();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("현재 씬의 FullProfilePanel에 추가"))
        {
            AddSnsIconsContainerToScene();
        }

        GUILayout.Space(20);
        GUILayout.Label("사용법:", EditorStyles.boldLabel);
        GUILayout.Label("1. 'SNS 아이콘 컨테이너 추가' 버튼 클릭");
        GUILayout.Label("2. ProfileManager Inspector에서 연결 확인");
        GUILayout.Label("3. 플레이 모드에서 SNS 아이콘 확인");
    }

    private void AddSnsIconsContainerToPrefab()
    {
        // Prefab 로드
        string prefabPath = "Assets/Prefabs/FullProfilePanel.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            EditorUtility.DisplayDialog("오류", $"Prefab을 찾을 수 없습니다: {prefabPath}", "확인");
            return;
        }

        // Prefab 인스턴스 생성
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        try
        {
            // Content 패널 찾기
            Transform content = instance.transform.Find("Content");
            if (content == null)
            {
                EditorUtility.DisplayDialog("오류", "Content 패널을 찾을 수 없습니다.", "확인");
                return;
            }

            // 기존 SnsIconsContainer 확인
            Transform existingContainer = content.Find("SnsIconsContainer");
            if (existingContainer != null)
            {
                EditorUtility.DisplayDialog("알림", "SnsIconsContainer가 이미 존재합니다.", "확인");
                DestroyImmediate(instance);
                return;
            }

            // SnsIconsContainer 생성
            GameObject container = CreateSnsIconsContainer(content);

            // SNS 버튼들 생성
            CreateSnsIconButton(container.transform, "Instagram", new Color(0.88f, 0.19f, 0.42f));
            CreateSnsIconButton(container.transform, "X", Color.black);
            CreateSnsIconButton(container.transform, "Facebook", new Color(0.23f, 0.35f, 0.60f));

            // Prefab에 변경사항 저장
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

            EditorUtility.DisplayDialog("완료", "SNS 아이콘 컨테이너가 추가되었습니다.\n\nProfileManager Inspector에서 연결을 확인하세요.", "확인");
        }
        finally
        {
            DestroyImmediate(instance);
        }
    }

    private void AddSnsIconsContainerToScene()
    {
        // 현재 씬에서 FullProfilePanel 찾기
        GameObject fullProfilePanel = GameObject.Find("FullProfilePanel");
        if (fullProfilePanel == null)
        {
            // Canvas 아래에서 찾기
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                Transform found = canvas.transform.Find("FullProfilePanel");
                if (found != null)
                {
                    fullProfilePanel = found.gameObject;
                    break;
                }
            }
        }

        if (fullProfilePanel == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에서 FullProfilePanel을 찾을 수 없습니다.", "확인");
            return;
        }

        Transform content = fullProfilePanel.transform.Find("Content");
        if (content == null)
        {
            EditorUtility.DisplayDialog("오류", "Content 패널을 찾을 수 없습니다.", "확인");
            return;
        }

        // 기존 컨테이너 확인
        Transform existingContainer = content.Find("SnsIconsContainer");
        if (existingContainer != null)
        {
            EditorUtility.DisplayDialog("알림", "SnsIconsContainer가 이미 존재합니다.", "확인");
            return;
        }

        // SnsIconsContainer 생성
        GameObject container = CreateSnsIconsContainer(content);

        // SNS 버튼들 생성
        CreateSnsIconButton(container.transform, "Instagram", new Color(0.88f, 0.19f, 0.42f));
        CreateSnsIconButton(container.transform, "X", Color.black);
        CreateSnsIconButton(container.transform, "Facebook", new Color(0.23f, 0.35f, 0.60f));

        // ProfileManager 연결 시도
        ProfileManager profileManager = FindFirstObjectByType<ProfileManager>();
        if (profileManager != null)
        {
            profileManager.snsIconsContainer = container;
            profileManager.instagramButton = container.transform.Find("InstagramButton")?.GetComponent<Button>();
            profileManager.xButton = container.transform.Find("XButton")?.GetComponent<Button>();
            profileManager.facebookButton = container.transform.Find("FacebookButton")?.GetComponent<Button>();

            EditorUtility.SetDirty(profileManager);
            Debug.Log("[FullProfilePanelSetup] ProfileManager에 SNS 컴포넌트 연결 완료");
        }

        Undo.RegisterCreatedObjectUndo(container, "Add SNS Icons Container");

        EditorUtility.DisplayDialog("완료", "SNS 아이콘 컨테이너가 씬에 추가되었습니다.", "확인");
    }

    private GameObject CreateSnsIconsContainer(Transform parent)
    {
        return CreateSnsIconsContainerStatic(parent);
    }

    private static GameObject CreateSnsIconsContainerStatic(Transform parent)
    {
        GameObject container = new GameObject("SnsIconsContainer");
        container.transform.SetParent(parent, false);

        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 80f);
        rect.anchoredPosition = new Vector2(0f, -370f); // FollowButton(-300)과 EditProfileButton(-440) 사이

        // HorizontalLayoutGroup 추가
        HorizontalLayoutGroup hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        return container;
    }

    private void CreateSnsIconButton(Transform parent, string snsName, Color bgColor)
    {
        CreateSnsIconButtonStatic(parent, snsName, bgColor);
    }

    private static void CreateSnsIconButtonStatic(Transform parent, string snsName, Color bgColor)
    {
        GameObject btnObj = new GameObject($"{snsName}Button");
        btnObj.transform.SetParent(parent, false);

        // ProfileManager에서 아이콘 사이즈 가져오기 (없으면 기본값 사용)
        Vector2 iconSize = DEFAULT_SNS_ICON_SIZE;
        ProfileManager profileManager = Object.FindFirstObjectByType<ProfileManager>();
        if (profileManager != null && profileManager.snsIconSize != Vector2.zero)
        {
            iconSize = profileManager.snsIconSize;
        }

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = iconSize;

        // 배경 이미지
        Image btnImage = btnObj.AddComponent<Image>();

        // Resources에서 아이콘 로드 시도
        string iconPath = $"SNS/{snsName.ToLower()}_icon";
        Sprite iconSprite = Resources.Load<Sprite>(iconPath);

        if (iconSprite != null)
        {
            btnImage.sprite = iconSprite;
            btnImage.color = Color.white;
        }
        else
        {
            // 폴백: 배경색
            btnImage.color = bgColor;

            // 텍스트 라벨 추가
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text label = textObj.AddComponent<Text>();
            label.text = snsName == "Instagram" ? "IG" : snsName == "X" ? "X" : "FB";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
        }

        // 버튼 컴포넌트
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        // 기본적으로 비활성화 (프로필에 SNS 정보가 있을 때만 활성화)
        btnObj.SetActive(false);
    }

    /// <summary>
    /// 로그아웃 버튼 생성 (내 프로필에서만 표시)
    /// </summary>
    private static GameObject CreateLogoutButtonStatic(Transform parent, ProfileManager profileManager)
    {
        GameObject btnObj = new GameObject("LogoutButton");
        btnObj.transform.SetParent(parent, false);

        // ProfileManager에서 사이즈/위치 가져오기 (없으면 기본값 사용)
        Vector2 buttonSize = DEFAULT_LOGOUT_BUTTON_SIZE;
        float yPosition = DEFAULT_LOGOUT_BUTTON_Y;

        if (profileManager != null)
        {
            if (profileManager.logoutButtonSize != Vector2.zero)
                buttonSize = profileManager.logoutButtonSize;
            if (profileManager.logoutButtonYPosition != 0)
                yPosition = profileManager.logoutButtonYPosition;
        }

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = buttonSize;
        btnRect.anchoredPosition = new Vector2(0f, yPosition);

        // 배경 이미지
        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.9f, 0.3f, 0.3f, 1f); // 붉은색 계열

        // 버튼 컴포넌트
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        // 버튼 색상 설정
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        colors.highlightedColor = new Color(0.85f, 0.25f, 0.25f, 1f);
        colors.pressedColor = new Color(0.7f, 0.2f, 0.2f, 1f);
        btn.colors = colors;

        // 텍스트 추가
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textObj.AddComponent<Text>();
        label.text = "로그아웃";

        // AppleSDGothicNeoM 폰트 로드 시도
        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (customFont != null)
        {
            label.font = customFont;
        }
        else
        {
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        label.fontSize = 20;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;

        // 기본적으로 비활성화 (내 프로필에서만 활성화됨)
        btnObj.SetActive(false);

        return btnObj;
    }

    // 다이얼로그 기본값 (ProfileManager 설정이 없을 경우 사용)
    private static readonly Vector2 DEFAULT_DIALOG_SIZE = new Vector2(300f, 180f);
    private static readonly Color DEFAULT_DIALOG_BG_COLOR = new Color(0.15f, 0.15f, 0.15f, 1f);
    private static readonly Color DEFAULT_DIALOG_OVERLAY_COLOR = new Color(0f, 0f, 0f, 0.7f);
    private static readonly Color DEFAULT_CONFIRM_BUTTON_COLOR = new Color(0.9f, 0.3f, 0.3f, 1f);
    private static readonly Color DEFAULT_CANCEL_BUTTON_COLOR = new Color(0.4f, 0.4f, 0.4f, 1f);
    private static readonly Vector2 DEFAULT_DIALOG_BUTTON_SIZE = new Vector2(100f, 40f);

    /// <summary>
    /// 로그아웃 확인 다이얼로그 생성
    /// ProfileManager의 Inspector 설정값 사용 (없으면 기본값)
    /// </summary>
    private static GameObject CreateLogoutConfirmDialogStatic(Transform parent, ProfileManager profileManager)
    {
        // ProfileManager에서 설정값 가져오기 (없으면 기본값 사용)
        Vector2 dialogSize = DEFAULT_DIALOG_SIZE;
        Color dialogBgColor = DEFAULT_DIALOG_BG_COLOR;
        Color overlayColor = DEFAULT_DIALOG_OVERLAY_COLOR;
        Color confirmButtonColor = DEFAULT_CONFIRM_BUTTON_COLOR;
        Color cancelButtonColor = DEFAULT_CANCEL_BUTTON_COLOR;
        Vector2 buttonSize = DEFAULT_DIALOG_BUTTON_SIZE;

        if (profileManager != null)
        {
            if (profileManager.logoutDialogSize != Vector2.zero)
                dialogSize = profileManager.logoutDialogSize;
            if (profileManager.logoutDialogBgColor != default)
                dialogBgColor = profileManager.logoutDialogBgColor;
            if (profileManager.logoutDialogOverlayColor != default)
                overlayColor = profileManager.logoutDialogOverlayColor;
            if (profileManager.logoutConfirmButtonColor != default)
                confirmButtonColor = profileManager.logoutConfirmButtonColor;
            if (profileManager.logoutCancelButtonColor != default)
                cancelButtonColor = profileManager.logoutCancelButtonColor;
            if (profileManager.logoutDialogButtonSize != Vector2.zero)
                buttonSize = profileManager.logoutDialogButtonSize;
        }

        // 배경 오버레이 (전체 화면 덮기)
        GameObject dialogObj = new GameObject("LogoutConfirmDialog");
        dialogObj.transform.SetParent(parent, false);

        RectTransform dialogRect = dialogObj.AddComponent<RectTransform>();
        dialogRect.anchorMin = Vector2.zero;
        dialogRect.anchorMax = Vector2.one;
        dialogRect.offsetMin = Vector2.zero;
        dialogRect.offsetMax = Vector2.zero;

        // 배경 반투명 (ProfileManager 설정 사용)
        Image bgImage = dialogObj.AddComponent<Image>();
        bgImage.color = overlayColor;
        bgImage.raycastTarget = true;

        // 다이얼로그 패널 (중앙)
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(dialogObj.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = dialogSize;  // ProfileManager 설정 사용

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = dialogBgColor;  // ProfileManager 설정 사용

        // 메시지 텍스트
        GameObject messageObj = new GameObject("MessageText");
        messageObj.transform.SetParent(panel.transform, false);

        RectTransform msgRect = messageObj.AddComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0f, 0.5f);
        msgRect.anchorMax = new Vector2(1f, 1f);
        msgRect.offsetMin = new Vector2(20f, 10f);
        msgRect.offsetMax = new Vector2(-20f, -10f);

        Text msgText = messageObj.AddComponent<Text>();
        msgText.text = "로그아웃 하시겠습니까?";
        msgText.alignment = TextAnchor.MiddleCenter;
        msgText.color = Color.white;
        msgText.fontSize = 22;

        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (customFont != null)
            msgText.font = customFont;
        else
            msgText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 버튼 Y 위치 계산 (다이얼로그 높이에 따라 동적 조정)
        float buttonYPos = -(dialogSize.y / 2f) + (buttonSize.y / 2f) + 15f;
        float buttonSpacing = buttonSize.x / 2f + 10f;

        // 확인 버튼 (ProfileManager 설정 사용)
        GameObject confirmBtn = CreateDialogButton(panel.transform, "ConfirmButton", "확인",
            new Vector2(-buttonSpacing, buttonYPos), confirmButtonColor, buttonSize);

        // 취소 버튼 (ProfileManager 설정 사용)
        GameObject cancelBtn = CreateDialogButton(panel.transform, "CancelButton", "취소",
            new Vector2(buttonSpacing, buttonYPos), cancelButtonColor, buttonSize);

        // 기본적으로 비활성화
        dialogObj.SetActive(false);

        return dialogObj;
    }

    /// <summary>
    /// 다이얼로그 버튼 생성 헬퍼
    /// </summary>
    private static GameObject CreateDialogButton(Transform parent, string name, string text, Vector2 position, Color bgColor, Vector2 buttonSize)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = buttonSize;  // ProfileManager 설정 사용
        btnRect.anchoredPosition = position;

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        // 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textObj.AddComponent<Text>();
        label.text = text;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;

        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (customFont != null)
            label.font = customFont;
        else
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return btnObj;
    }
}
#endif
