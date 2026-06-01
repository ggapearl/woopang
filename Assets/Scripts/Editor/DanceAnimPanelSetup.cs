using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// DanceAnimController + 확인 패널 UI를 씬에 자동 생성/연결.
/// 다른 _Setup 스크립트(CategoryToggleSetup 등)와 동일 패턴: [InitializeOnLoad] + sceneOpened + DidReloadScripts.
///
/// 이미 컨트롤러가 씬에 있으면 silent skip → idempotent.
/// 폰트는 우선 LegacyRuntime 사용 (AppleSDGothicNeoM은 Resources에서 찾으면 자동 교체).
/// </summary>
[InitializeOnLoad]
public class DanceAnimPanelSetup
{
    private const string CONTROLLER_GO_NAME = "DanceAnimController";
    private const string PANEL_GO_NAME = "DanceAnimPanel";

    static DanceAnimPanelSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndSetupSilent;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckAndSetupSilent;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CheckAndSetupSilent;
    }

    [MenuItem("Tools/WOOPANG/Setup DanceAnim Panel")]
    public static void ManualSetup()
    {
        if (EnsurePanel()) Debug.Log("[DanceAnimPanelSetup] 패널 + 컨트롤러 생성·연결 완료");
        else Debug.Log("[DanceAnimPanelSetup] 이미 존재 — skip");
    }

    private static void CheckAndSetupSilent()
    {
        if (Application.isPlaying) return;
        EnsurePanel();
    }

    /// <returns>새로 생성했으면 true, 이미 있으면 false</returns>
    private static bool EnsurePanel()
    {
        if (Application.isPlaying) return false;
        if (Object.FindAnyObjectByType<DanceAnimController>() != null) return false;

        // Canvas — ScreenSpaceOverlay 루트 캔버스만 선택 (서브캔버스·WorldSpace 제외)
        // 이전 버그: FindAnyObjectByType<Canvas>가 ContinueCaptureDialog의 서브캔버스를
        // 잡아서 패널이 거기 자식으로 박힘 + 스케일 0 부모 때문에 영원히 안 보임.
        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (c.transform.parent != null) continue; // 루트 캔버스만
            if (c.gameObject.name == "Canvas") { canvas = c; break; } // 이름 일치 우선
            if (canvas == null) canvas = c;
        }
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
        }

        Font font = TryLoadProjectFont();

        // 패널
        var panel = new GameObject(PANEL_GO_NAME, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        var pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = pRt.anchorMax = pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(640, 380);
        panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.94f);

        // 타이틀
        Text titleText = CreateText(panel.transform, "Title", "타이틀",
            new Vector2(0, -40), new Vector2(-40, 70),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
            36, Color.white, font, TextAnchor.MiddleCenter);

        // 사이즈/안내 텍스트
        Text sizeText = CreateText(panel.transform, "SizeInfo", "3D 보기 (다운로드 필요)",
            new Vector2(0, -125), new Vector2(-40, 40),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
            22, new Color(0.75f, 0.75f, 0.75f), font, TextAnchor.MiddleCenter);

        // 진행 그룹 (확인 버튼 누른 후 표시)
        var progressGroup = new GameObject("ProgressGroup", typeof(RectTransform));
        progressGroup.transform.SetParent(panel.transform, false);
        var grRt = progressGroup.GetComponent<RectTransform>();
        grRt.anchorMin = new Vector2(0, 1); grRt.anchorMax = new Vector2(1, 1);
        grRt.pivot = new Vector2(0.5f, 1f);
        grRt.anchoredPosition = new Vector2(0, -195);
        grRt.sizeDelta = new Vector2(-40, 60);
        progressGroup.SetActive(false);

        Text progressText = CreateText(progressGroup.transform, "ProgressText", "준비 중...",
            Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            24, Color.white, font, TextAnchor.MiddleCenter);
        var ptRt = progressText.GetComponent<RectTransform>();
        ptRt.offsetMin = Vector2.zero; ptRt.offsetMax = Vector2.zero;

        // 확인 / 취소 버튼
        Button confirmBtn = CreateButton(panel.transform, "ConfirmButton", "3D 보기",
            new Vector2(-110, -285), new Vector2(200, 76),
            new Color(0.21f, 0.58f, 0.91f), font);
        Button cancelBtn = CreateButton(panel.transform, "CancelButton", "취소",
            new Vector2(110, -285), new Vector2(200, 76),
            new Color(0.4f, 0.4f, 0.4f), font);

        panel.SetActive(false);

        // 컨트롤러
        var ctrlGo = new GameObject(CONTROLLER_GO_NAME);
        var ctrl = ctrlGo.AddComponent<DanceAnimController>();
        ctrl.confirmPanel = panel;
        ctrl.titleText = titleText;
        ctrl.sizeText = sizeText;
        ctrl.confirmButton = confirmBtn;
        ctrl.cancelButton = cancelBtn;
        ctrl.progressGroup = progressGroup;
        ctrl.progressText = progressText;

        EditorUtility.SetDirty(ctrlGo);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(ctrlGo.scene);
        return true;
    }

    // ---------- 헬퍼 ----------

    private static Text CreateText(Transform parent, string name, string content,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        int fontSize, Color color, Font font, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.text = content;
        t.alignment = align;
        t.color = color;
        t.fontSize = fontSize;
        t.font = font;
        return t;
    }

    private static Button CreateButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 sizeDelta, Color bg, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        go.GetComponent<Image>().color = bg;

        var txtGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
        var t = txtGo.GetComponent<Text>();
        t.text = label;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.fontSize = 28;
        t.font = font;
        return go.GetComponent<Button>();
    }

    /// <summary>프로젝트 폰트 우선, 없으면 빌트인 Legacy 폴백.</summary>
    private static Font TryLoadProjectFont()
    {
        var f = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (f != null) return f;
        // 프로젝트 전반에서 쓰는 폰트가 Resources 밖에 있으면 못 찾음 → 빌트인 폴백
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
