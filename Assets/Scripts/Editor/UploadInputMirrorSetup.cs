using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// ============================================================
// CubeUploadManager.uploadPage 안에 UploadInputMirror 컴포넌트와
// 미러 InputArea(DM 채팅 디자인 복제)를 자동 생성/연결.
//
// 트리거: 씬 오픈 / 스크립트 재컴파일 / 메뉴 수동 실행.
// 멱등: 이미 연결되어 있으면 변경 없이 종료.
// ============================================================
[InitializeOnLoad]
public static class UploadInputMirrorSetup
{
    private const string MirrorObjName = "UploadInputMirror";
    private const string MirrorInputObjName = "MirrorInput";
    private const string MirrorPlaceholderName = "Placeholder";
    private const string MirrorTextName = "Text";
    private const string MirrorCloseButtonName = "CloseButton";

    static UploadInputMirrorSetup()
    {
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += SetupIfPossible;
        EditorApplication.delayCall += SetupIfPossible;
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += SetupIfPossible;
    }

    [MenuItem("WOOPANG/Upload/Setup Input Mirror (manual)")]
    public static void ManualSetup()
    {
        SetupIfPossible();
    }

    private static void SetupIfPossible()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!SceneManager.GetActiveScene().isLoaded) return;

        var uploadMgr = GameObject.FindFirstObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
        if (uploadMgr == null) return;

        var so = new SerializedObject(uploadMgr);
        var uploadPageProp = so.FindProperty("uploadPage");
        if (uploadPageProp == null || uploadPageProp.objectReferenceValue == null) return;

        GameObject uploadPage = uploadPageProp.objectReferenceValue as GameObject;
        if (uploadPage == null) return;

        var nameInput = (so.FindProperty("nameInput")?.objectReferenceValue) as InputField;
        var instagramInput = (so.FindProperty("instagramIDInput")?.objectReferenceValue) as InputField;
        if (nameInput == null && instagramInput == null) return;

        var mirrorComp = uploadPage.GetComponent<UploadInputMirror>();
        bool added = false;
        if (mirrorComp == null)
        {
            mirrorComp = Undo.AddComponent<UploadInputMirror>(uploadPage);
            added = true;
        }

        // 미러 패널 찾거나 생성
        Transform existingMirror = uploadPage.transform.Find(MirrorObjName);
        GameObject mirrorPanel;
        if (existingMirror != null)
        {
            mirrorPanel = existingMirror.gameObject;
        }
        else
        {
            mirrorPanel = CreateMirrorPanel(uploadPage);
            added = true;
        }

        InputField mirrorInput = mirrorPanel.GetComponentInChildren<InputField>(true);
        Button closeBtn = mirrorPanel.GetComponentInChildren<Button>(true);
        Text placeholder = null;
        if (mirrorInput != null && mirrorInput.placeholder is Text p) placeholder = p;

        var ms = new SerializedObject(mirrorComp);
        bool changed = false;
        changed |= AssignIfDifferent(ms, "mirrorPanel", mirrorPanel);
        changed |= AssignIfDifferent(ms, "mirrorInput", mirrorInput);
        changed |= AssignIfDifferent(ms, "mirrorPlaceholder", placeholder);
        changed |= AssignIfDifferent(ms, "closeButton", closeBtn);
        changed |= AssignIfDifferent(ms, "mirrorRect", mirrorPanel.transform as RectTransform);
        changed |= AssignIfDifferent(ms, "nameInput", nameInput);
        changed |= AssignIfDifferent(ms, "instagramInput", instagramInput);

        if (changed || added)
        {
            ms.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(uploadPage);
            EditorSceneManager.MarkSceneDirty(uploadPage.scene);
            Debug.Log($"[UpInputMirror Setup] {uploadPage.name}에 UploadInputMirror {(added ? "생성" : "업데이트")} 완료");
        }
    }

    private static bool AssignIfDifferent(SerializedObject so, string propName, Object value)
    {
        var p = so.FindProperty(propName);
        if (p == null) return false;
        if (p.objectReferenceValue == value) return false;
        p.objectReferenceValue = value;
        return true;
    }

    /// <summary>DM ChatInput InputArea 디자인 모방 — 화면 하단 부착, 좌측 입력칸 + 우측 80x80 닫기 버튼</summary>
    private static GameObject CreateMirrorPanel(GameObject uploadPage)
    {
        // ─── 컨테이너 ───
        GameObject panel = new GameObject(MirrorObjName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(uploadPage.transform, false);
        panel.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, 0);
        rt.sizeDelta = new Vector2(0, 150);

        Image bg = panel.GetComponent<Image>();
        bg.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        // ─── 입력칸 컨테이너 (Image + InputField) ───
        GameObject inputObj = new GameObject(MirrorInputObjName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
        inputObj.transform.SetParent(panel.transform, false);
        inputObj.layer = LayerMask.NameToLayer("UI");

        RectTransform inputRt = inputObj.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0, 0.5f);
        inputRt.anchorMax = new Vector2(1, 0.5f);
        inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.anchoredPosition = new Vector2(-25, 0);
        inputRt.sizeDelta = new Vector2(-150, 100);

        Image inputBg = inputObj.GetComponent<Image>();
        inputBg.color = new Color(1f, 1f, 1f, 1f);

        InputField input = inputObj.GetComponent<InputField>();
        input.targetGraphic = inputBg;

        // ─── Placeholder ───
        GameObject placeholderObj = new GameObject(MirrorPlaceholderName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        placeholderObj.transform.SetParent(inputObj.transform, false);
        placeholderObj.layer = LayerMask.NameToLayer("UI");
        RectTransform phRt = placeholderObj.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(20, 0);
        phRt.offsetMax = new Vector2(-20, 0);
        Text phText = placeholderObj.GetComponent<Text>();
        phText.text = "입력하세요";
        phText.fontSize = 36;
        phText.alignment = TextAnchor.MiddleLeft;
        phText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        phText.font = LoadAppleFont();
        phText.supportRichText = false;
        phText.raycastTarget = false;
        input.placeholder = phText;

        // ─── Text (실제 입력 표시) ───
        GameObject textObj = new GameObject(MirrorTextName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObj.transform.SetParent(inputObj.transform, false);
        textObj.layer = LayerMask.NameToLayer("UI");
        RectTransform txRt = textObj.GetComponent<RectTransform>();
        txRt.anchorMin = Vector2.zero;
        txRt.anchorMax = Vector2.one;
        txRt.offsetMin = new Vector2(20, 0);
        txRt.offsetMax = new Vector2(-20, 0);
        Text txText = textObj.GetComponent<Text>();
        txText.text = string.Empty;
        txText.fontSize = 36;
        txText.alignment = TextAnchor.MiddleLeft;
        txText.color = Color.black;
        txText.font = LoadAppleFont();
        txText.supportRichText = false;
        txText.raycastTarget = false;
        input.textComponent = txText;

        // ─── Close Button (우측, 80x80 — DM SendButton과 동일 위치) ───
        GameObject btnObj = new GameObject(MirrorCloseButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(panel.transform, false);
        btnObj.layer = LayerMask.NameToLayer("UI");
        RectTransform btnRt = btnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1, 0.5f);
        btnRt.anchorMax = new Vector2(1, 0.5f);
        btnRt.pivot = new Vector2(1, 0.5f);
        btnRt.anchoredPosition = new Vector2(-20, 0);
        btnRt.sizeDelta = new Vector2(80, 80);
        Image btnImg = btnObj.GetComponent<Image>();
        btnImg.color = new Color(0.85f, 0.15f, 0.15f, 1f);
        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = btnImg;

        // 버튼 라벨
        GameObject btnLabelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        btnLabelObj.transform.SetParent(btnObj.transform, false);
        btnLabelObj.layer = LayerMask.NameToLayer("UI");
        RectTransform lbRt = btnLabelObj.GetComponent<RectTransform>();
        lbRt.anchorMin = Vector2.zero;
        lbRt.anchorMax = Vector2.one;
        lbRt.offsetMin = Vector2.zero;
        lbRt.offsetMax = Vector2.zero;
        Text lbText = btnLabelObj.GetComponent<Text>();
        lbText.text = "X";
        lbText.fontSize = 36;
        lbText.alignment = TextAnchor.MiddleCenter;
        lbText.color = Color.white;
        lbText.font = LoadAppleFont();
        lbText.raycastTarget = false;

        // 마지막 sibling으로 — UploadPage 자식 중 최상위에 그려짐
        panel.transform.SetAsLastSibling();
        panel.SetActive(false);

        return panel;
    }

    private static Font _cachedAppleFont;
    private static Font LoadAppleFont()
    {
        if (_cachedAppleFont != null) return _cachedAppleFont;
        _cachedAppleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/AppleSDGothicNeoM.ttf");
        if (_cachedAppleFont == null) _cachedAppleFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _cachedAppleFont;
    }
}
