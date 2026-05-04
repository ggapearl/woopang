using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

// ============================================================
// CubeUploadManager / ModelUploadManager / CubeDataFixManager 의 페이지에
// UploadInputMirror 컴포넌트와 미러 InputArea(DM 채팅과 동일 프리팹)를
// 자동 생성/연결.
//
// Assets/Prefabs/InputArea.prefab 1개를 Instantiate하므로
// 디자인 변경은 프리팹만 수정하면 4곳(DM + Cube + Model + Fix)에 일괄 반영.
//
// 트리거: 씬 오픈 / 스크립트 재컴파일 / 메뉴 수동 실행. 멱등 처리.
// ============================================================
[InitializeOnLoad]
public static class UploadInputMirrorSetup
{
    private const string MirrorObjName = "UploadInputMirror";
    private const string PrefabPath = "Assets/Prefabs/InputArea.prefab";

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

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[UpInputMirror Setup] {PrefabPath} 를 찾을 수 없음 — 프리팹 누락");
            return;
        }

        foreach (var spec in Specs)
        {
            ProcessManager(spec, prefab);
        }
    }

    private static void ProcessManager(ManagerSpec spec, GameObject prefab)
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

        // 기존 미러 자식 정리 — 이전 코드 생성한 흰박스 흔적 제거
        Transform legacy = pageObj.transform.Find(MirrorObjName);
        if (legacy != null)
        {
            UnityEngine.Object.DestroyImmediate(legacy.gameObject);
            added = true;
        }

        // 프리팹 인스턴스 생성 (프리팹 연결 유지 → 디자인 일괄 반영)
        GameObject mirrorPanel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pageObj.transform);
        mirrorPanel.name = MirrorObjName;

        // 화면 하단 부착 — DM과 동일한 anchor
        RectTransform rt = mirrorPanel.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 0);
        }
        mirrorPanel.transform.SetAsLastSibling();
        mirrorPanel.SetActive(false);

        InputField mirrorInput = mirrorPanel.GetComponentInChildren<InputField>(true);
        Button closeBtn = mirrorPanel.GetComponentInChildren<Button>(true);
        Text placeholder = (mirrorInput != null && mirrorInput.placeholder is Text p) ? p : null;

        var ms = new SerializedObject(mirrorComp);
        bool changed = false;
        changed |= AssignIfDifferent(ms, "mirrorPanel", mirrorPanel);
        changed |= AssignIfDifferent(ms, "mirrorInput", mirrorInput);
        changed |= AssignIfDifferent(ms, "mirrorPlaceholder", placeholder);
        changed |= AssignIfDifferent(ms, "closeButton", closeBtn);
        changed |= AssignIfDifferent(ms, "mirrorRect", rt);
        changed |= AssignArrayIfDifferent(ms, "sourceInputs", sources);

        if (changed || added)
        {
            ms.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pageObj);
            EditorSceneManager.MarkSceneDirty(pageObj.scene);
            Debug.Log($"[UpInputMirror Setup] {spec.TypeName} → {pageObj.name}: 프리팹 미러 {(added ? "생성" : "업데이트")}, source {sources.Count}개 연결");
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
}
