using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;

/// <summary>
/// FilterButtonPanel 프리팹 자동 수정
/// - root P2PUserFilterPanel의 p2pUserToggle 연결 복구
/// - P2PUserToggle 자식에 중복된 P2PUserFilterPanel 제거
/// </summary>
public static class FilterPanelFixer
{
    private const string PREFAB_PATH = "Assets/Prefabs/FilterButtonPanel.prefab";

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += FixFilterButtonPanel;
    }

    private static void FixFilterButtonPanel()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null) return;

        string assetPath = AssetDatabase.GetAssetPath(prefab);
        GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);
        bool modified = false;

        // 1. root의 P2PUserFilterPanel 찾기
        P2PUserFilterPanel rootFilter = instance.GetComponent<P2PUserFilterPanel>();

        // 2. P2PUserToggle 자식 찾기
        Transform p2pUserToggle = instance.transform.Find("P2PUserToggle");
        if (p2pUserToggle == null)
        {
            // 자식 검색
            foreach (Transform child in instance.transform)
            {
                if (child.name.Contains("P2PUser") && child.GetComponent<Toggle>() != null)
                {
                    p2pUserToggle = child;
                    break;
                }
            }
        }

        if (p2pUserToggle != null)
        {
            Toggle toggle = p2pUserToggle.GetComponent<Toggle>();

            // 3. 자식의 중복 P2PUserFilterPanel 제거
            P2PUserFilterPanel childFilter = p2pUserToggle.GetComponent<P2PUserFilterPanel>();
            if (childFilter != null)
            {
                Object.DestroyImmediate(childFilter);
                modified = true;
            }

            // 4. root의 P2PUserFilterPanel에 토글 연결
            if (rootFilter != null && toggle != null)
            {
                SerializedObject so = new SerializedObject(rootFilter);
                SerializedProperty toggleProp = so.FindProperty("p2pUserToggle");
                if (toggleProp != null && toggleProp.objectReferenceValue == null)
                {
                    toggleProp.objectReferenceValue = toggle;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    modified = true;
                }
            }
        }

        if (modified)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            AssetDatabase.SaveAssets();
        }

        PrefabUtility.UnloadPrefabContents(instance);
    }
}