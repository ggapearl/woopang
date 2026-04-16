using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// 모든 매니저의 indicatorOnlyPrefab 필드를 자동으로 연결
/// DataManager, TourAPIManager, SubwayManager, TrainStationManager, TerminalManager
/// Assets/Prefab/IndicatorOnly.prefab을 찾아 연결
/// </summary>
[InitializeOnLoad]
public class IndicatorOnlyPrefabSetup
{
    private const string PREFAB_PATH = "Assets/Prefab/IndicatorOnly.prefab";

    static IndicatorOnlyPrefabSetup()
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
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null) return;

        // DataManager
        DataManager dm = Object.FindFirstObjectByType<DataManager>(FindObjectsInactive.Include);
        if (dm != null)
        {
            ConnectPrefab(dm, prefab);
        }

        // TourAPIManager
        TourAPIManager tam = Object.FindFirstObjectByType<TourAPIManager>(FindObjectsInactive.Include);
        if (tam != null)
        {
            ConnectPrefab(tam, prefab);
        }

        // SubwayManager
        SubwayManager sm = Object.FindFirstObjectByType<SubwayManager>(FindObjectsInactive.Include);
        if (sm != null)
        {
            ConnectPrefab(sm, prefab);
        }

        // TrainStationManager
        TrainStationManager tsm = Object.FindFirstObjectByType<TrainStationManager>(FindObjectsInactive.Include);
        if (tsm != null)
        {
            ConnectPrefab(tsm, prefab);
        }

        // TerminalManager
        TerminalManager tm = Object.FindFirstObjectByType<TerminalManager>(FindObjectsInactive.Include);
        if (tm != null)
        {
            ConnectPrefab(tm, prefab);
        }
    }

    private static void ConnectPrefab(MonoBehaviour manager, GameObject prefab)
    {
        SerializedObject so = new SerializedObject(manager);
        SerializedProperty prop = so.FindProperty("indicatorOnlyPrefab");
        if (prop != null && prop.objectReferenceValue == null)
        {
            prop.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
    }
}
