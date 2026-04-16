using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;

/// <summary>
/// FilterManager를 ListPanel 자식에서 분리하여 항상 활성화된 독립 오브젝트로 이동
/// 메뉴: WOOPANG > FilterManager를 독립 오브젝트로 분리
/// </summary>
public class FilterManagerSeparator
{
    [MenuItem("WOOPANG/FilterManager를 독립 오브젝트로 분리")]
    public static void Separate()
    {
        // 1) 기존 FilterManager 찾기
        FilterManager oldFM = Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (oldFM == null)
        {
            EditorUtility.DisplayDialog("실패", "씬에서 FilterManager를 찾을 수 없습니다.", "확인");
            return;
        }

        GameObject oldObj = oldFM.gameObject;

        // 이미 루트 레벨이면 분리 불필요
        if (oldObj.transform.parent == null)
        {
            EditorUtility.DisplayDialog("안내", "FilterManager가 이미 루트 레벨에 있습니다.", "확인");
            return;
        }

        string oldPath = GetGameObjectPath(oldObj);

        // 2) 새 독립 오브젝트 생성 (항상 활성화)
        GameObject newObj = new GameObject("FilterManager");
        newObj.SetActive(true);
        Undo.RegisterCreatedObjectUndo(newObj, "Create FilterManager Object");

        // 3) FilterManager 컴포넌트 복사
        ComponentUtility.CopyComponent(oldFM);
        ComponentUtility.PasteComponentAsNew(newObj);

        // 4) 새 FilterManager 확인
        FilterManager newFM = newObj.GetComponent<FilterManager>();
        if (newFM == null)
        {
            EditorUtility.DisplayDialog("실패", "FilterManager 복사에 실패했습니다.", "확인");
            Object.DestroyImmediate(newObj);
            return;
        }

        // 5) 기존 컴포넌트 제거
        Undo.DestroyObjectImmediate(oldFM);

        // 6) 씬 더티 마킹
        EditorUtility.SetDirty(newObj);
        EditorSceneManager.MarkSceneDirty(newObj.scene);

        Debug.Log($"[FilterManagerSeparator] FilterManager를 '{oldPath}' → 루트 'FilterManager' 오브젝트로 분리 완료");
        EditorUtility.DisplayDialog("완료",
            $"FilterManager를 독립 오브젝트로 분리했습니다.\n\n" +
            $"이전: {oldPath}\n" +
            $"이후: FilterManager (루트)\n\n" +
            $"씬을 저장하세요!", "확인");
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform t = obj.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
