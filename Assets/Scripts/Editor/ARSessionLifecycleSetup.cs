using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// ARSessionLifecycle을 ARSession GameObject에 자동 부착
/// iOS 워치독(0x8BADF00D) 회피를 위해 ARSession과 동일 생명주기로 동작해야 함
/// </summary>
[InitializeOnLoad]
public class ARSessionLifecycleSetup
{
    static ARSessionLifecycleSetup()
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
        ARSessionLifecycle existing = Object.FindFirstObjectByType<ARSessionLifecycle>(FindObjectsInactive.Include);
        if (existing != null) return;

        ARSession arSession = Object.FindFirstObjectByType<ARSession>(FindObjectsInactive.Include);
        if (arSession == null) return;

        arSession.gameObject.AddComponent<ARSessionLifecycle>();
        EditorUtility.SetDirty(arSession.gameObject);
        EditorSceneManager.MarkSceneDirty(arSession.gameObject.scene);
    }
}
