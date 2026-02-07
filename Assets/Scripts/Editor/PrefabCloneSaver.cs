#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

/// <summary>
/// 런타임에 생성된 클론(인스턴스)의 수정사항을 프리팹에 저장하는 에디터 도구
/// Play Mode에서 FollowListItem 등의 UI를 수정한 후 프리팹에 반영할 수 있음
/// </summary>
[InitializeOnLoad]
public class PrefabCloneSaver : EditorWindow
{
    private static GameObject selectedClone;
    private static string targetPrefabPath;

    // 자동 감지할 프리팹 이름 목록
    private static readonly string[] TRACKED_PREFAB_NAMES = new string[]
    {
        "FollowListItem",
        "ConversationItem",
        "AdminNoticeItem",
        "MyMessageBubble",
        "OtherMessageBubble",
        "SearchResultItem"
    };

    static PrefabCloneSaver()
    {
        // Play Mode 종료 시 자동 저장 확인
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Selection.selectionChanged += OnSelectionChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            // Play Mode 종료 시 수정된 클론이 있으면 저장 여부 확인
            CheckForModifiedClones();
        }
    }

    private static void OnSelectionChanged()
    {
        // 선택 변경 시 클론인지 확인
        if (Selection.activeGameObject != null && EditorApplication.isPlaying)
        {
            CheckIfTrackedClone(Selection.activeGameObject);
        }
    }

    private static void CheckIfTrackedClone(GameObject obj)
    {
        foreach (string prefabName in TRACKED_PREFAB_NAMES)
        {
            // 이름에 (Clone) 또는 프리팹 이름이 포함되어 있는지 확인
            if (obj.name.Contains(prefabName))
            {
                selectedClone = obj;
                Debug.Log($"[PrefabCloneSaver] 추적 중인 클론 감지: {obj.name}");
                return;
            }
        }
    }

    private static void CheckForModifiedClones()
    {
        // Play Mode 종료 전 수정된 클론 확인 (현재는 수동 저장만 지원)
    }

    /// <summary>
    /// 선택된 오브젝트를 프리팹으로 저장
    /// </summary>
    public static void SaveSelectedToPrefab()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("[PrefabCloneSaver] 선택된 오브젝트가 없습니다.");
            return;
        }

        GameObject selected = Selection.activeGameObject;
        string baseName = GetBasePrefabName(selected.name);

        if (string.IsNullOrEmpty(baseName))
        {
            Debug.LogWarning($"[PrefabCloneSaver] '{selected.name}'은 추적 대상 프리팹이 아닙니다.");
            return;
        }

        // 프리팹 경로 찾기
        string prefabPath = FindPrefabPath(baseName);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError($"[PrefabCloneSaver] '{baseName}' 프리팹을 찾을 수 없습니다.");
            return;
        }

        // 저장 확인
        bool confirmed = EditorUtility.DisplayDialog(
            "프리팹 저장 확인",
            $"'{selected.name}'의 변경사항을 프리팹에 저장하시겠습니까?\n\n프리팹 경로: {prefabPath}",
            "저장",
            "취소"
        );

        if (confirmed)
        {
            SaveToPrefab(selected, prefabPath);
        }
    }

    private static string GetBasePrefabName(string objectName)
    {
        // "(Clone)" 제거
        string cleanName = objectName.Replace("(Clone)", "").Trim();

        // 추적 대상인지 확인
        foreach (string prefabName in TRACKED_PREFAB_NAMES)
        {
            if (cleanName.Contains(prefabName))
            {
                return prefabName;
            }
        }

        return null;
    }

    private static string FindPrefabPath(string prefabName)
    {
        // 검색 경로 목록
        string[] searchPaths = new string[]
        {
            $"Assets/Prefabs/Profile/{prefabName}.prefab",
            $"Assets/Prefabs/DM/{prefabName}.prefab",
            $"Assets/Prefabs/{prefabName}.prefab",
            $"Assets/Prefab/{prefabName}.prefab"
        };

        foreach (string path in searchPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // GUID로 검색
        string[] guids = AssetDatabase.FindAssets($"t:Prefab {prefabName}");
        if (guids.Length > 0)
        {
            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }

        return null;
    }

    private static void SaveToPrefab(GameObject source, string prefabPath)
    {
        // 기존 프리팹 로드
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab == null)
        {
            Debug.LogError($"[PrefabCloneSaver] 프리팹 로드 실패: {prefabPath}");
            return;
        }

        // 임시 복제본 생성 (Play Mode에서는 직접 저장 불가)
        GameObject tempCopy = Object.Instantiate(source);
        tempCopy.name = existingPrefab.name;

        // 프리팹 저장
        try
        {
            PrefabUtility.SaveAsPrefabAsset(tempCopy, prefabPath);
            Debug.Log($"[PrefabCloneSaver] 프리팹 저장 완료: {prefabPath}");

            EditorUtility.DisplayDialog(
                "저장 완료",
                $"프리팹이 성공적으로 저장되었습니다.\n\n경로: {prefabPath}",
                "확인"
            );
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PrefabCloneSaver] 프리팹 저장 실패: {e.Message}");
        }
        finally
        {
            Object.DestroyImmediate(tempCopy);
        }
    }

    /// <summary>
    /// 에디터 윈도우 표시
    /// </summary>
    public static void ShowWindow()
    {
        GetWindow<PrefabCloneSaver>("프리팹 클론 저장");
    }

    private Vector2 scrollPosition;

    void OnGUI()
    {
        GUILayout.Label("프리팹 클론 저장 도구", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 현재 선택된 오브젝트 정보
        EditorGUILayout.LabelField("선택된 오브젝트:", Selection.activeGameObject?.name ?? "(없음)");

        if (Selection.activeGameObject != null)
        {
            string baseName = GetBasePrefabName(Selection.activeGameObject.name);
            if (!string.IsNullOrEmpty(baseName))
            {
                EditorGUILayout.LabelField("감지된 프리팹:", baseName);

                string prefabPath = FindPrefabPath(baseName);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    EditorGUILayout.LabelField("프리팹 경로:", prefabPath);

                    GUILayout.Space(10);

                    if (GUILayout.Button("선택된 오브젝트를 프리팹에 저장", GUILayout.Height(30)))
                    {
                        SaveSelectedToPrefab();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("프리팹 파일을 찾을 수 없습니다.", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("추적 대상 프리팹이 아닙니다.", MessageType.Info);
            }
        }

        GUILayout.Space(20);

        // 추적 대상 프리팹 목록
        GUILayout.Label("추적 대상 프리팹:", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
        foreach (string prefabName in TRACKED_PREFAB_NAMES)
        {
            EditorGUILayout.LabelField($"  - {prefabName}");
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);

        // 도움말
        EditorGUILayout.HelpBox(
            "사용법:\n" +
            "1. Play Mode에서 UI 클론(FollowListItem 등)을 수정합니다.\n" +
            "2. Hierarchy에서 수정한 오브젝트를 선택합니다.\n" +
            "3. '선택된 오브젝트를 프리팹에 저장' 버튼을 클릭합니다.\n\n" +
            "주의: Play Mode가 종료되면 변경사항이 사라지므로 종료 전에 저장하세요.",
            MessageType.Info
        );
    }

    void OnInspectorUpdate()
    {
        Repaint();
    }
}
#endif
