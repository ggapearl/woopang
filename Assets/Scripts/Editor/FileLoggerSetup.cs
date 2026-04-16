using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// FileLogger 컴포넌트를 씬에 자동 배치
/// 씬에 FileLogger가 없으면 빈 게임오브젝트에 자동 추가
/// </summary>
[InitializeOnLoad]
public class FileLoggerSetup
{
    static FileLoggerSetup()
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
        // 이미 씬에 FileLogger가 있으면 스킵
        FileLogger existing = Object.FindFirstObjectByType<FileLogger>(FindObjectsInactive.Include);
        if (existing != null) return;

        // DataManager의 게임오브젝트에 부착 (같은 생명주기를 공유)
        DataManager dm = Object.FindFirstObjectByType<DataManager>(FindObjectsInactive.Include);
        if (dm != null)
        {
            dm.gameObject.AddComponent<FileLogger>();
            EditorUtility.SetDirty(dm.gameObject);
            EditorSceneManager.MarkSceneDirty(dm.gameObject.scene);
        }
        else
        {
            // DataManager가 없으면 독립 오브젝트 생성
            GameObject loggerObj = new GameObject("FileLogger");
            loggerObj.AddComponent<FileLogger>();
            EditorUtility.SetDirty(loggerObj);
            if (loggerObj.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(loggerObj.scene);
        }
    }
}
