using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;

[InitializeOnLoad]
public class LocationPermissionSetup
{
    static LocationPermissionSetup()
    {
        EditorSceneManager.sceneOpened += (scene, mode) =>
            EditorApplication.delayCall += CheckAndSetup;
        EditorApplication.delayCall += CheckAndSetup;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckAndSetup;
    }

    private static void CheckAndSetup()
    {
        // LocationPermissionManager가 없으면 LoginManager 오브젝트에 자동으로 추가
        LocationPermissionManager mgr = Object.FindFirstObjectByType<LocationPermissionManager>();
        if (mgr == null)
        {
            LoginManager loginManager = Object.FindFirstObjectByType<LoginManager>();
            if (loginManager == null) return;

            mgr = loginManager.gameObject.AddComponent<LocationPermissionManager>();
            EditorUtility.SetDirty(loginManager.gameObject);
        }

        // 패널이 이미 연결되어 있으면 스킵
        if (mgr.locationPermissionPanel != null) return;

        // uiCanvasRoot를 Canvas로 사용
        LoginManager lm = Object.FindFirstObjectByType<LoginManager>();
        if (lm == null || lm.uiCanvasRoot == null) return;

        Canvas canvas = lm.uiCanvasRoot.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = lm.uiCanvasRoot.GetComponent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[LocationPermissionSetup] Canvas를 찾을 수 없습니다.");
            return;
        }

        CreateLocationPermissionPanel(mgr, canvas);
    }

    private static void CreateLocationPermissionPanel(LocationPermissionManager mgr, Canvas canvas)
    {
        Font customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/AppleSDGothicNeoM.ttf");
        Sprite roundedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sou/UI/square_message_sky.png");

        // ── 루트 패널 (반투명 오버레이) ──
        GameObject panel = new GameObject("LocationPermissionPanel");
        panel.transform.SetParent(canvas.transform, false);
        panel.SetActive(false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.5f);
        panelBg.raycastTarget = true;
        panel.transform.SetAsLastSibling();

        // ── Box (흰색 둥근 박스) ──
        GameObject box = new GameObject("Box");
        box.transform.SetParent(panel.transform, false);

        RectTransform boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(600, 400);
        boxRect.anchoredPosition = Vector2.zero;

        box.AddComponent<CanvasRenderer>();
        Image boxImage = box.AddComponent<Image>();
        if (roundedSprite != null) boxImage.sprite = roundedSprite;
        boxImage.color = new Color(1, 1, 1, 0.78f);

        // ── Label (안내 텍스트) ──
        GameObject label = new GameObject("Label");
        label.transform.SetParent(box.transform, false);

        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.anchoredPosition = new Vector2(0, 40);
        labelRect.sizeDelta = new Vector2(-40, -130);

        label.AddComponent<CanvasRenderer>();
        Text labelText = label.AddComponent<Text>();
        labelText.text = "주변 콘텐츠를 보려면\n위치 접근 권한이 필요합니다.\n설정에서 허용해 주세요.";
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontSize = 36;
        labelText.color = Color.white;
        if (customFont != null) labelText.font = customFont;

        // ── Buttons 컨테이너 ──
        GameObject buttons = new GameObject("Buttons");
        buttons.transform.SetParent(box.transform, false);

        RectTransform buttonsRect = buttons.AddComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0.5f, 0f);
        buttonsRect.anchorMax = new Vector2(0.5f, 0f);
        buttonsRect.sizeDelta = new Vector2(500, 90);
        buttonsRect.anchoredPosition = new Vector2(0, 50);

        // ── 설정으로 이동 버튼 ──
        Button btnSettings = CreateButton("Btn_Settings", buttons.transform,
            new Vector2(-130, 0), new Vector2(220, 90),
            customFont, roundedSprite, "설정으로 이동",
            new Color(0.2f, 0.6f, 1f, 1f), Color.white);

        // ── 닫기 버튼 ──
        Button btnClose = CreateButton("Btn_Close", buttons.transform,
            new Vector2(130, 0), new Vector2(220, 90),
            customFont, roundedSprite, "닫기",
            new Color(0.8f, 0.8f, 0.8f, 1f), new Color(0.2f, 0.2f, 0.2f));

        // ── LocationPermissionManager 필드 연결 ──
        mgr.locationPermissionPanel = panel;
        mgr.promptText = labelText;
        mgr.goToSettingsButton = btnSettings;
        mgr.cancelButton = btnClose;

        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);

        Debug.Log("[LocationPermissionSetup] LocationPermissionPanel 생성 및 연결 완료");
    }

    private static Button CreateButton(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size, Font font, Sprite sprite,
        string labelStr, Color bgColor, Color textColor)
    {
        GameObject btn = new GameObject(name);
        btn.transform.SetParent(parent, false);

        RectTransform rect = btn.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        btn.AddComponent<CanvasRenderer>();
        Image img = btn.AddComponent<Image>();
        img.color = bgColor;
        if (sprite != null) img.sprite = sprite;

        Button button = btn.AddComponent<Button>();

        // 버튼 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btn.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        textObj.AddComponent<CanvasRenderer>();
        Text text = textObj.AddComponent<Text>();
        text.text = labelStr;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 28;
        text.color = textColor;
        if (font != null) text.font = font;

        return button;
    }
}
