using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// VersionButton_Open 하위에 FileLogger 테스트 버튼 2개 생성
/// - LOG START/STOP 토글 버튼
/// - SHARE LOG 공유 버튼
/// 런칭 전 테스트용 — 나중에 삭제
/// </summary>
[InitializeOnLoad]
public class FileLoggerButtonSetup
{
    private const string TOGGLE_BTN_NAME = "FileLoggerToggleBtn";
    private const string SHARE_BTN_NAME = "FileLoggerShareBtn";

    static FileLoggerButtonSetup()
    {
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += Setup;
        EditorApplication.delayCall += Setup;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += Setup;
    }

    private static void Setup()
    {
        // VersionButton_Open 찾기
        GameObject versionBtn = null;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var found = FindInChildren(root.transform, "VersionButton_Open");
            if (found != null) { versionBtn = found.gameObject; break; }
        }
        if (versionBtn == null) return;

        RectTransform parentRect = versionBtn.GetComponent<RectTransform>();
        if (parentRect == null) return;

        // 폰트 로드
        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");

        bool changed = false;

        // 토글 버튼 생성
        if (parentRect.Find(TOGGLE_BTN_NAME) == null)
        {
            CreateButton(parentRect, TOGGLE_BTN_NAME, "LOG START",
                new Vector2(0, -120), new Vector2(280, 80),
                new Color(0.2f, 0.7f, 0.3f, 1f), customFont);
            changed = true;
        }

        // 공유 버튼 생성
        if (parentRect.Find(SHARE_BTN_NAME) == null)
        {
            CreateButton(parentRect, SHARE_BTN_NAME, "SHARE LOG",
                new Vector2(0, -220), new Vector2(280, 80),
                new Color(0.3f, 0.5f, 0.9f, 1f), customFont);
            changed = true;
        }

        if (changed)
        {
            // FileLogger 연결
            FileLogger logger = Object.FindFirstObjectByType<FileLogger>(FindObjectsInactive.Include);
            if (logger != null)
            {
                // 토글 버튼 → ToggleLogging
                Transform toggleT = parentRect.Find(TOGGLE_BTN_NAME);
                if (toggleT != null)
                {
                    Button toggleBtn = toggleT.GetComponent<Button>();
                    if (toggleBtn != null)
                    {
                        UnityEditor.Events.UnityEventTools.AddPersistentListener(
                            toggleBtn.onClick,
                            new UnityEngine.Events.UnityAction(logger.ToggleLogging));
                    }

                    // toggleButtonText 필드 자동 연결
                    Text toggleText = toggleT.GetComponentInChildren<Text>();
                    if (toggleText != null)
                    {
                        var so = new SerializedObject(logger);
                        var prop = so.FindProperty("toggleButtonText");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = toggleText;
                            so.ApplyModifiedProperties();
                        }
                    }
                }

                // 공유 버튼 → ShareLatestLog
                Transform shareT = parentRect.Find(SHARE_BTN_NAME);
                if (shareT != null)
                {
                    Button shareBtn = shareT.GetComponent<Button>();
                    if (shareBtn != null)
                    {
                        UnityEditor.Events.UnityEventTools.AddPersistentListener(
                            shareBtn.onClick,
                            new UnityEngine.Events.UnityAction(logger.ShareLatestLog));
                    }
                }
            }

            EditorUtility.SetDirty(versionBtn);
            EditorSceneManager.MarkSceneDirty(versionBtn.scene);
        }
    }

    private static void CreateButton(RectTransform parent, string name, string label,
        Vector2 anchoredPos, Vector2 size, Color bgColor, Font font)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.layer = 5; // UI
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        // Background Image
        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        // Button
        Button btn = btnObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.1f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        // Text (일반 Unity Text — TMP 폰트 아틀라스 깨짐 방지)
        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.layer = 5;
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        if (font != null) text.font = font;
    }

    private static Transform FindInChildren(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindInChildren(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
