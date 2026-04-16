using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// [비활성] FileLogger가 자체 런타임 UI를 생성하므로 더 이상 사용하지 않음
/// VersionButton_Open 하위에 이미 생성된 FileLoggerToggleBtn/FileLoggerShareBtn 제거용
/// </summary>
[InitializeOnLoad]
public class FileLoggerButtonSetup
{
    static FileLoggerButtonSetup()
    {
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += CleanupOldButtons;
        EditorApplication.delayCall += CleanupOldButtons;
    }

    private static void CleanupOldButtons()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var found = FindInChildren(root.transform, "VersionButton_Open");
            if (found == null) continue;

            bool changed = false;
            Transform toggle = found.Find("FileLoggerToggleBtn");
            if (toggle != null) { Object.DestroyImmediate(toggle.gameObject); changed = true; }

            Transform share = found.Find("FileLoggerShareBtn");
            if (share != null) { Object.DestroyImmediate(share.gameObject); changed = true; }

            if (changed)
            {
                EditorUtility.SetDirty(found.gameObject);
                EditorSceneManager.MarkSceneDirty(found.gameObject.scene);
            }
            break;
        }
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
