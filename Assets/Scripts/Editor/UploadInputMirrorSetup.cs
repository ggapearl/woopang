using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

// ============================================================
// CubeUploadManager / ModelUploadManager / CubeDataFixManager 의 페이지에
// UploadInputMirror 컴포넌트와 미러 InputArea(DM 채팅 디자인 복제)를
// 자동 생성/연결.
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

    /// <summary>매니저별 page 필드명 + 미러에 연결할 source InputField 필드명들</summary>
    private struct ManagerSpec
    {
        public string TypeName;
        public string PageFieldName;
        public string[] InputFieldNames;
    }

    private static readonly ManagerSpec[] Specs = new[]
    {
        new ManagerSpec
        {
            TypeName = "CubeUploadManager",
            PageFieldName = "uploadPage",
            InputFieldNames = new[] { "nameInput", "instagramIDInput" }
        },
        new ManagerSpec
        {
            TypeName = "ModelUploadManager",
            PageFieldName = "uploadPage",
            InputFieldNames = new[] { "nameInput", "instagramIDInput" }
        },
        new ManagerSpec
        {
            TypeName = "CubeDataFixManager",
            PageFieldName = "fixUIPanel",
            InputFieldNames = new[] { "nameInput", "instagramIDInput", "descriptionInput" }
        },
    };

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

        foreach (var spec in Specs)
        {
            ProcessManager(spec);
        }
    }

    private static void ProcessManager(ManagerSpec spec)
    {
        Type managerType = FindTypeByName(spec.TypeName);
        if (managerType == null) return;

        var manager = GameObject.FindFirstObjectByType(managerType, FindObjectsInactive.Include) as Component;
        if (manager == null) return;

        var so = new SerializedObject(manager);
        var pageProp = so.FindProperty(spec.PageFieldName);
        if (pageProp == null || pageProp.objectReferenceValue == null) return;

        GameObject pageObj = pageProp.objectReferenceValue as GameObject;
        if (pageObj == null) return;

        var sources = new List<InputField>();
        foreach (var fieldName in spec.InputFieldNames)
        {
            var fp = so.FindProperty(fieldName);
            var inp = fp?.objectReferenceValue as InputField;
            if (inp != null) sources.Add(inp);
        }
        if (sources.Count == 0) return;

        var mirrorComp = pageObj.GetComponent<UploadInputMirror>();
        bool added = false;
        if (mirrorComp == null)
        {
            mirrorComp = Undo.AddComponent<UploadInputMirror>(pageObj);
            added = true;
        }

        Transform existingMirror = pageObj.transform.Find(MirrorObjName);
        GameObject mirrorPanel;
        if (existingMirror != null)
        {
            mirrorPanel = existingMirror.gameObject;
        }
        else
        {
            mirrorPanel = CreateMirrorPanel(pageObj);
            added = true;
        }

        InputField mirrorInput = mirrorPanel.GetComponentInChildren<InputField>(true);
        Button closeBtn = mirrorPanel.GetComponentInChildren<Button>(true);
        Text placeholder = (mirrorInput != null && mirrorInput.placeholder is Text p) ? p : null;

        var ms = new SerializedObject(mirrorComp);
        bool changed = false;
        changed |= AssignIfDifferent(ms, "mirrorPanel", mirrorPanel);
        changed |= AssignIfDifferent(ms, "mirrorInput", mirrorInput);
        changed |= AssignIfDifferent(ms, "mirrorPlaceholder", placeholder);
        changed |= AssignIfDifferent(ms, "closeButton", closeBtn);
        changed |= AssignIfDifferent(ms, "mirrorRect", mirrorPanel.transform as RectTransform);
        changed |= AssignArrayIfDifferent(ms, "sourceInputs", sources);

        if (changed || added)
        {
            ms.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pageObj);
            EditorSceneManager.MarkSceneDirty(pageObj.scene);
            Debug.Log($"[UpInputMirror Setup] {spec.TypeName} → {pageObj.name}: 미러 {(added ? "생성" : "업데이트")}, source {sources.Count}개 연결");
        }
    }

    private static Type FindTypeByName(string name)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var t in asm.GetTypes())
            {
                if (t.Name == name) return t;
            }
        }
        return null;
    }

    private static bool AssignIfDifferent(SerializedObject so, string propName, UnityEngine.Object value)
    {
        var p = so.FindProperty(propName);
        if (p == null) return false;
        if (p.objectReferenceValue == value) return false;
        p.objectReferenceValue = value;
        return true;
    }

    private static bool AssignArrayIfDifferent(SerializedObject so, string propName, List<InputField> values)
    {
        var p = so.FindProperty(propName);
        if (p == null) return false;
        bool changed = p.arraySize != values.Count;
        if (!changed)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (p.GetArrayElementAtIndex(i).objectReferenceValue != values[i])
                { changed = true; break; }
            }
        }
        if (changed)
        {
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        return changed;
    }

    /// <summary>DM ChatInput InputArea 디자인 모방 — 화면 하단 부착, 좌측 입력칸 + 우측 80x80 닫기 버튼</summary>
    private static GameObject CreateMirrorPanel(GameObject parent)
    {
        GameObject panel = new GameObject(MirrorObjName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent.transform, false);
        panel.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, 0);
        rt.sizeDelta = new Vector2(0, 150);

        Image bg = panel.GetComponent<Image>();
        bg.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        // 입력칸 컨테이너
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

        // Placeholder
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

        // Text
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

        // Close Button
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
