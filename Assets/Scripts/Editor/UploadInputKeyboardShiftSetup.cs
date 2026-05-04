using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// ============================================================
// 씬 로드 / 스크립트 재컴파일 시 CubeUploadManager의 uploadPage에
// UploadInputKeyboardShift를 자동 부착하고 nameInput/locationInput/
// instagramIDInput을 monitoredInputs에 자동 연결.
// CLAUDE.md 2.3 (완전한 작업 수행) 준수.
// ============================================================
[InitializeOnLoad]
public static class UploadInputKeyboardShiftSetup
{
    static UploadInputKeyboardShiftSetup()
    {
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += SetupIfPossible;
        EditorApplication.delayCall += SetupIfPossible;
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += SetupIfPossible;
    }

    [MenuItem("WOOPANG/Upload/Setup Keyboard Shift (manual)")]
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

        var nameProp = so.FindProperty("nameInput");
        var locProp = so.FindProperty("locationInput");
        var igProp = so.FindProperty("instagramIDInput");

        InputField nameInput = nameProp?.objectReferenceValue as InputField;
        InputField locationInput = locProp?.objectReferenceValue as InputField;
        InputField igInput = igProp?.objectReferenceValue as InputField;

        var shift = uploadPage.GetComponent<UploadInputKeyboardShift>();
        bool added = false;
        if (shift == null)
        {
            shift = Undo.AddComponent<UploadInputKeyboardShift>(uploadPage);
            added = true;
        }

        var shiftSo = new SerializedObject(shift);
        var contentRootProp = shiftSo.FindProperty("contentRoot");
        if (contentRootProp.objectReferenceValue == null)
            contentRootProp.objectReferenceValue = uploadPage.transform as RectTransform;

        var monitoredProp = shiftSo.FindProperty("monitoredInputs");
        var desired = new System.Collections.Generic.List<InputField>();
        if (nameInput != null) desired.Add(nameInput);
        if (locationInput != null) desired.Add(locationInput);
        if (igInput != null) desired.Add(igInput);

        bool needsRewire = monitoredProp.arraySize != desired.Count;
        if (!needsRewire)
        {
            for (int i = 0; i < desired.Count; i++)
            {
                if (monitoredProp.GetArrayElementAtIndex(i).objectReferenceValue != desired[i])
                { needsRewire = true; break; }
            }
        }

        if (needsRewire)
        {
            monitoredProp.arraySize = desired.Count;
            for (int i = 0; i < desired.Count; i++)
                monitoredProp.GetArrayElementAtIndex(i).objectReferenceValue = desired[i];
        }

        if (shiftSo.hasModifiedProperties || added)
        {
            shiftSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(uploadPage);
            EditorSceneManager.MarkSceneDirty(uploadPage.scene);
            Debug.Log($"[UploadKbShift Setup] {uploadPage.name}에 UploadInputKeyboardShift {(added ? "추가" : "업데이트")} — 입력 {desired.Count}개 연결");
        }
    }
}
