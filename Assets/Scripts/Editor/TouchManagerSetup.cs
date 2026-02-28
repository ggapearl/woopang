using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;

/// <summary>
/// TouchManager가 씬에 없으면 자동 생성
/// </summary>
[InitializeOnLoad]
public class TouchManagerSetup
{
    static TouchManagerSetup()
    {
        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            EditorApplication.delayCall += CheckAndSetup;
        };
        EditorApplication.delayCall += CheckAndSetup;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckAndSetup;
    }

    private static void CheckAndSetup()
    {
        TouchManager existing = Object.FindFirstObjectByType<TouchManager>();
        if (existing != null) return;

        GameObject go = new GameObject("TouchManager");
        go.AddComponent<TouchManager>();

        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
