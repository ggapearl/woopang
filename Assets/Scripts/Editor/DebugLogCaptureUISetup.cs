using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
// DebugLogCaptureUI 자동 생성 에디터 스크립트
// 메뉴: Tools > WOOPANG > Setup Debug Log Capture UI
// - Canvas 자식으로 DebugLogCapturePanel 생성
// - 화면 중앙 하단 anchor (위치/사이즈는 사용자가 자유롭게 조정 가능)
// - Start/Stop 버튼 + Copy 버튼 + 상태 라벨
// ============================================================
public class DebugLogCaptureUISetup
{
    private const string PANEL_NAME = "DebugLogCapturePanel";

    [MenuItem("Tools/WOOPANG/Setup Debug Log Capture UI")]
    public static void Setup()
    {
        // 활성 씬에서 Canvas 찾기 (Top-level Canvas 우선)
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas targetCanvas = null;
        foreach (var c in canvases)
        {
            if (c.renderMode != RenderMode.WorldSpace && c.transform.parent == null)
            {
                targetCanvas = c;
                break;
            }
        }
        if (targetCanvas == null && canvases.Length > 0) targetCanvas = canvases[0];

        if (targetCanvas == null)
        {
            EditorUtility.DisplayDialog("DebugLogCaptureUI", "씬에 Canvas가 없습니다. Canvas를 먼저 만들어주세요.", "OK");
            return;
        }

        // 기존 패널 제거
        Transform existing = targetCanvas.transform.Find(PANEL_NAME);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // 폰트 로드 (없으면 default)
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/AppleSDGothicNeoM.ttf");

        // 1. 컨테이너 패널 — 중앙 하단 anchor
        GameObject panel = new GameObject(PANEL_NAME, typeof(RectTransform));
        panel.transform.SetParent(targetCanvas.transform, false);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = new Vector2(0f, 200f);
        panelRT.sizeDelta = new Vector2(600f, 180f);

        // 반투명 배경
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.55f);
        panelBg.raycastTarget = true;

        // 가로 정렬 (버튼 2개 + 상태 라벨)
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 2. 버튼 가로 컨테이너
        GameObject buttonRow = new GameObject("ButtonRow", typeof(RectTransform));
        buttonRow.transform.SetParent(panel.transform, false);
        HorizontalLayoutGroup hLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 12f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = true;
        hLayout.childForceExpandHeight = true;
        LayoutElement buttonRowLE = buttonRow.AddComponent<LayoutElement>();
        buttonRowLE.preferredHeight = 80f;

        // 3. Start/Stop 버튼
        Button startBtn = CreateButton(buttonRow.transform, "StartStopButton", "Start Log", new Color(0.2f, 0.6f, 0.2f, 1f), font, out Text startBtnLabel);

        // 4. Copy 버튼
        Button copyBtn = CreateButton(buttonRow.transform, "CopyButton", "Copy Log", new Color(0.2f, 0.4f, 0.7f, 1f), font, out Text copyBtnLabel);

        // 5. 상태 라벨
        GameObject statusGO = new GameObject("StatusLabel", typeof(RectTransform));
        statusGO.transform.SetParent(panel.transform, false);
        Text statusText = statusGO.AddComponent<Text>();
        statusText.text = "Stopped (0 lines)";
        statusText.color = Color.white;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.fontSize = 28;
        if (font != null) statusText.font = font;
        else statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        LayoutElement statusLE = statusGO.AddComponent<LayoutElement>();
        statusLE.preferredHeight = 40f;

        // 6. DebugLogCaptureUI 컴포넌트 부착 + 필드 연결
        DebugLogCaptureUI ui = panel.AddComponent<DebugLogCaptureUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("startStopButton").objectReferenceValue = startBtn;
        so.FindProperty("startStopButtonLabel").objectReferenceValue = startBtnLabel;
        so.FindProperty("copyButton").objectReferenceValue = copyBtn;
        so.FindProperty("statusLabel").objectReferenceValue = statusText;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = panel;
        EditorGUIUtility.PingObject(panel);

        Debug.Log($"[DebugLogCaptureUISetup] 생성 완료. Canvas={targetCanvas.name}, 위치/사이즈는 Inspector에서 조정 가능.");
    }

    private static Button CreateButton(Transform parent, string name, string label, Color bgColor, Font font, out Text labelText)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform));
        btnGO.transform.SetParent(parent, false);

        Image btnBg = btnGO.AddComponent<Image>();
        btnBg.color = bgColor;
        btnBg.raycastTarget = true;

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = cb;

        // 라벨
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        labelText = labelGO.AddComponent<Text>();
        labelText.text = label;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontSize = 32;
        labelText.fontStyle = FontStyle.Bold;
        if (font != null) labelText.font = font;
        else labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.raycastTarget = false;

        return btn;
    }
}
