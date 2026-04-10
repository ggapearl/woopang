using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// DataManager/TourAPIManager의 indicatorOnlyPrefab 필드를 자동으로 연결
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
            SerializedObject so = new SerializedObject(dm);
            SerializedProperty prop = so.FindProperty("indicatorOnlyPrefab");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dm);
                EditorSceneManager.MarkSceneDirty(dm.gameObject.scene);
            }
        }

        // TourAPIManager
        TourAPIManager tam = Object.FindFirstObjectByType<TourAPIManager>(FindObjectsInactive.Include);
        if (tam != null)
        {
            SerializedObject so = new SerializedObject(tam);
            SerializedProperty prop = so.FindProperty("indicatorOnlyPrefab");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tam);
                EditorSceneManager.MarkSceneDirty(tam.gameObject.scene);
            }
        }
    }
}
