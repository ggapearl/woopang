using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.IO;

/// <summary>
/// P2P 시스템 자동 설정 에디터 스크립트
/// Unity 메뉴: WOOPANG > Setup P2P System
/// </summary>
public class P2PAutoSetup : EditorWindow
{
    private const string FONT_PATH = "Assets/TextMesh Pro/Fonts/AppleSDGothicNeoM.ttf";
    private const string P2P_USER_PREFAB_PATH = "Assets/Prefabs/P2P_User.prefab";
    private const string LOGIN_PROMPT_PREFAB_PATH = "Assets/Prefabs/LoginPromptPanel.prefab";

    private static TMP_FontAsset defaultFont;
    private static bool setupComplete = false;

    [MenuItem("WOOPANG/Setup P2P System")]
    public static void SetupP2PSystem()
    {
        if (EditorUtility.DisplayDialog("P2P 시스템 설정",
            "현재 씬에 P2P 시스템을 자동으로 설정하시겠습니까?\n\n" +
            "다음 항목이 설정됩니다:\n" +
            "1. P2PManager GameObject 생성\n" +
            "2. P2PProfilePanel UI 생성\n" +
            "3. LoginManager 참조 연결\n" +
            "4. 기본 폰트 설정",
            "설정 시작", "취소"))
        {
            RunSetup();
        }
    }

    private static void RunSetup()
    {
        Debug.Log("[P2P Setup] =====시작=====");

        // 1. 폰트 로드
        LoadDefaultFont();

        // 2. P2PManager 설정
        SetupP2PManager();

        // 3. LoginManager 설정
        SetupLoginManager();

        // 4. P2PProfilePanel 설정
        SetupP2PProfilePanel();

        // 5. 씬 저장
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene()
        );

        setupComplete = true;
        Debug.Log("[P2P Setup] =====완료=====");
        EditorUtility.DisplayDialog("완료", "P2P 시스템 설정이 완료되었습니다!\n\n" +
            "다음 단계:\n" +
            "1. 서버 실행: python integrate_p2p.py\n" +
            "2. DB 초기화: curl -X POST http://210.105.65.145:5000/api/p2p/init_db\n" +
            "3. Unity 실행 후 로그인", "확인");
    }

    private static void LoadDefaultFont()
    {
        defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
        if (defaultFont == null)
        {
            Debug.LogWarning($"[P2P Setup] 폰트를 찾을 수 없습니다: {FONT_PATH}");
        }
        else
        {
            Debug.Log($"[P2P Setup] ✓ 기본 폰트 로드: {defaultFont.name}");
        }
    }

    private static void SetupP2PManager()
    {
        // 기존 P2PManager 찾기
        P2PManager existingManager = Object.FindObjectOfType<P2PManager>();
        if (existingManager != null)
        {
            Debug.Log("[P2P Setup] ✓ P2PManager가 이미 존재합니다.");
            ConfigureP2PManager(existingManager);
            return;
        }

        // 새로 생성
        GameObject managerObj = new GameObject("P2PManager");
        P2PManager manager = managerObj.AddComponent<P2PManager>();

        ConfigureP2PManager(manager);
        Debug.Log("[P2P Setup] ✓ P2PManager 생성 완료");
    }

    private static void ConfigureP2PManager(P2PManager manager)
    {
        // SerializedObject를 사용하여 private 필드 설정
        SerializedObject so = new SerializedObject(manager);

        // Server Settings
        so.FindProperty("serverUrl").stringValue = "ws://210.105.65.145:5000";
        so.FindProperty("enableP2P").boolValue = true;

        // Tracking Settings
        so.FindProperty("maxTrackingDistance").floatValue = 1000f;
        so.FindProperty("maxVisibleUsers").intValue = 20;
        so.FindProperty("positionUpdateInterval").floatValue = 5f;
        so.FindProperty("heartbeatInterval").floatValue = 30f;

        // Prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P2P_USER_PREFAB_PATH);
        if (prefab != null)
        {
            so.FindProperty("userAvatarPrefab").objectReferenceValue = prefab;
            Debug.Log("[P2P Setup] ✓ P2P_User 프리팹 연결");
        }
        else
        {
            Debug.LogWarning($"[P2P Setup] P2P_User 프리팹을 찾을 수 없습니다: {P2P_USER_PREFAB_PATH}");
        }

        so.ApplyModifiedProperties();
    }

    private static void SetupLoginManager()
    {
        LoginManager loginManager = Object.FindObjectOfType<LoginManager>();
        if (loginManager == null)
        {
            Debug.LogWarning("[P2P Setup] LoginManager를 찾을 수 없습니다.");
            return;
        }

        SerializedObject so = new SerializedObject(loginManager);

        // LoginPromptPanel 프리팹 연결
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LOGIN_PROMPT_PREFAB_PATH);
        if (prefab != null)
        {
            so.FindProperty("loginPromptPanelPrefab").objectReferenceValue = prefab;
            Debug.Log("[P2P Setup] ✓ LoginPromptPanel 프리팹 연결");
        }

        // Canvas 찾기
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            so.FindProperty("uiCanvasRoot").objectReferenceValue = canvas.gameObject;
            Debug.Log("[P2P Setup] ✓ UI Canvas 연결");
        }

        so.ApplyModifiedProperties();
    }

    private static void SetupP2PProfilePanel()
    {
        // 기존 ProfilePanel 찾기
        P2PProfilePanel existingPanel = Object.FindObjectOfType<P2PProfilePanel>();
        if (existingPanel != null)
        {
            Debug.Log("[P2P Setup] ✓ P2PProfilePanel이 이미 존재합니다.");
            return;
        }

        // Canvas 찾기
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[P2P Setup] Canvas를 찾을 수 없습니다!");
            return;
        }

        // ProfilePanel 생성
        GameObject panelObj = new GameObject("ProfilePanel");
        panelObj.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // Background
        Image background = panelObj.AddComponent<Image>();
        background.color = new Color(0, 0, 0, 0.8f);

        // CanvasGroup
        CanvasGroup canvasGroup = panelObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // P2PProfilePanel 스크립트 추가
        P2PProfilePanel panel = panelObj.AddComponent<P2PProfilePanel>();

        // ProfileContainer 생성
        GameObject container = CreateProfileContainer(panelObj.transform);

        // UI 요소 생성
        CreateProfileUI(container.transform, panel);

        Debug.Log("[P2P Setup] ✓ P2PProfilePanel 생성 완료");
    }

    private static GameObject CreateProfileContainer(Transform parent)
    {
        GameObject container = new GameObject("ProfileContainer");
        container.transform.SetParent(parent, false);

        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600, 800);

        Image bg = container.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        return container;
    }

    private static void CreateProfileUI(Transform container, P2PProfilePanel panel)
    {
        SerializedObject so = new SerializedObject(panel);

        // Avatar Image
        GameObject avatarObj = CreateUIElement("AvatarImage", container, new Vector2(0, 250), new Vector2(200, 200));
        Image avatarImage = avatarObj.AddComponent<Image>();
        avatarImage.color = Color.gray;
        so.FindProperty("avatarImage").objectReferenceValue = avatarImage;

        // Username Text
        GameObject usernameObj = CreateTextElement("UsernameText", container, new Vector2(0, 100), new Vector2(500, 60));
        TextMeshProUGUI usernameText = usernameObj.GetComponent<TextMeshProUGUI>();
        usernameText.fontSize = 36;
        usernameText.alignment = TextAlignmentOptions.Center;
        usernameText.text = "Username";
        if (defaultFont) usernameText.font = defaultFont;
        so.FindProperty("usernameText").objectReferenceValue = usernameText;

        // Bio Text
        GameObject bioObj = CreateTextElement("BioText", container, new Vector2(0, 20), new Vector2(500, 120));
        TextMeshProUGUI bioText = bioObj.GetComponent<TextMeshProUGUI>();
        bioText.fontSize = 24;
        bioText.alignment = TextAlignmentOptions.Center;
        bioText.text = "Bio...";
        if (defaultFont) bioText.font = defaultFont;
        so.FindProperty("bioText").objectReferenceValue = bioText;

        // Distance Text
        GameObject distanceObj = CreateTextElement("DistanceText", container, new Vector2(0, -60), new Vector2(500, 40));
        TextMeshProUGUI distanceText = distanceObj.GetComponent<TextMeshProUGUI>();
        distanceText.fontSize = 28;
        distanceText.alignment = TextAlignmentOptions.Center;
        distanceText.text = "0m";
        if (defaultFont) distanceText.font = defaultFont;
        so.FindProperty("distanceText").objectReferenceValue = distanceText;

        // Close Button
        GameObject closeBtn = CreateButton("CloseButton", container, new Vector2(0, -350), new Vector2(200, 60), "닫기");
        Button closeButton = closeBtn.GetComponent<Button>();
        so.FindProperty("closeButton").objectReferenceValue = closeButton;

        // Gesture Buttons (간략화)
        CreateGestureButtons(container, panel, so);

        so.ApplyModifiedProperties();
    }

    private static void CreateGestureButtons(Transform container, P2PProfilePanel panel, SerializedObject so)
    {
        GameObject gestureGroup = new GameObject("GestureButtons");
        gestureGroup.transform.SetParent(container, false);
        RectTransform rect = gestureGroup.AddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0, -150);
        rect.sizeDelta = new Vector2(500, 80);

        HorizontalLayoutGroup layout = gestureGroup.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        string[] gestures = { "👋", "👍", "❤️", "🎉" };
        string[] gestureNames = { "waveButton", "thumbsUpButton", "heartButton", "celebrateButton" };

        for (int i = 0; i < gestures.Length; i++)
        {
            GameObject btn = CreateButton(gestureNames[i], gestureGroup.transform, Vector2.zero, new Vector2(100, 80), gestures[i]);
            so.FindProperty(gestureNames[i]).objectReferenceValue = btn.GetComponent<Button>();
        }
    }

    private static GameObject CreateUIElement(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return obj;
    }

    private static GameObject CreateTextElement(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject obj = CreateUIElement(name, parent, position, size);
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        return obj;
    }

    private static GameObject CreateButton(string name, Transform parent, Vector2 position, Vector2 size, string label)
    {
        GameObject btnObj = CreateUIElement(name, parent, position, size);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.6f, 1f, 1f);

        Button btn = btnObj.AddComponent<Button>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = label;
        btnText.fontSize = 24;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
        if (defaultFont) btnText.font = defaultFont;

        return btnObj;
    }

    [MenuItem("WOOPANG/Check P2P Setup Status")]
    public static void CheckSetupStatus()
    {
        string status = "=== P2P 시스템 상태 ===\n\n";

        // P2PManager 확인
        P2PManager manager = Object.FindObjectOfType<P2PManager>();
        status += manager != null ? "✓ P2PManager: 존재\n" : "✗ P2PManager: 없음\n";

        // LoginManager 확인
        LoginManager login = Object.FindObjectOfType<LoginManager>();
        status += login != null ? "✓ LoginManager: 존재\n" : "✗ LoginManager: 없음\n";

        // P2PProfilePanel 확인
        P2PProfilePanel panel = Object.FindObjectOfType<P2PProfilePanel>();
        status += panel != null ? "✓ P2PProfilePanel: 존재\n" : "✗ P2PProfilePanel: 없음\n";

        // 프리팹 확인
        GameObject p2pUserPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(P2P_USER_PREFAB_PATH);
        status += p2pUserPrefab != null ? "✓ P2P_User 프리팹: 존재\n" : "✗ P2P_User 프리팹: 없음\n";

        // 폰트 확인
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
        status += font != null ? $"✓ 폰트: {font.name}\n" : "✗ 폰트: 없음\n";

        Debug.Log(status);
        EditorUtility.DisplayDialog("P2P 시스템 상태", status, "확인");
    }
}
