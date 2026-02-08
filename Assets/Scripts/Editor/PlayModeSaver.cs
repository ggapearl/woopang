using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 플레이 모드에서 수정한 컴포넌트 값을 자동으로 저장
/// 사용법: Inspector에서 컴포넌트 우클릭 → "Save on Play Mode Exit" 선택
///
/// Clone 오브젝트(런타임 생성)는 원본 프리팹에 저장됩니다.
/// </summary>
[InitializeOnLoad]
public static class PlayModeSaver
{
    private static Dictionary<int, string> savedComponents = new Dictionary<int, string>();
    private static Dictionary<int, bool> isCloneObject = new Dictionary<int, bool>();
    private static List<ComponentToSave> componentsToSave = new List<ComponentToSave>();
    private static List<PrefabComponentToSave> prefabsToSave = new List<PrefabComponentToSave>();

    private class ComponentToSave
    {
        public int instanceId;
        public string json;
        public string componentType;
        public string gameObjectPath;
    }

    private class PrefabComponentToSave
    {
        public string prefabPath;
        public string componentType;
        public string json;
        public string childPath; // 프리팹 내 자식 경로 (루트면 빈 문자열)
    }

    static PlayModeSaver()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            // 플레이 모드 종료 직전: 마킹된 컴포넌트 값 저장
            SaveMarkedComponents();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // 에디터 모드 진입: 저장된 값 복원
            RestoreSavedComponents();
        }
    }

    private static void SaveMarkedComponents()
    {
        componentsToSave.Clear();
        prefabsToSave.Clear();

        foreach (var kvp in savedComponents)
        {
#pragma warning disable CS0618 // InstanceIDToObject is obsolete but EntityIdToObject is not available in this Unity version
            var obj = EditorUtility.InstanceIDToObject(kvp.Key) as Component;
#pragma warning restore CS0618
            if (obj != null)
            {
                // Clone 오브젝트인지 확인 (부모 계층 포함)
                bool isClone = isCloneObject.ContainsKey(kvp.Key) && isCloneObject[kvp.Key];

                if (isClone)
                {
                    // Clone 오브젝트는 프리팹에 저장
                    string prefabPath = GetPrefabPath(obj.gameObject);
                    string childPath = GetChildPathInPrefab(obj.gameObject);

                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        var prefabData = new PrefabComponentToSave
                        {
                            prefabPath = prefabPath,
                            componentType = obj.GetType().AssemblyQualifiedName,
                            json = EditorJsonUtility.ToJson(obj),
                            childPath = childPath
                        };
                        prefabsToSave.Add(prefabData);

                        string targetInfo = string.IsNullOrEmpty(childPath)
                            ? prefabPath
                            : $"{prefabPath} → {childPath}";
                        Debug.Log($"[PlayModeSaver] 프리팹 저장 예정: {obj.GetType().Name} → {targetInfo}");
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayModeSaver] Clone 오브젝트의 원본 프리팹을 찾을 수 없음: {obj.gameObject.name}");
                    }
                }
                else
                {
                    // 씬 오브젝트는 기존 방식으로 저장
                    var data = new ComponentToSave
                    {
                        instanceId = kvp.Key,
                        json = EditorJsonUtility.ToJson(obj),
                        componentType = obj.GetType().AssemblyQualifiedName,
                        gameObjectPath = GetGameObjectPath(obj.gameObject)
                    };
                    componentsToSave.Add(data);
                    Debug.Log($"[PlayModeSaver] 저장: {obj.GetType().Name} on {data.gameObjectPath}");
                }
            }
        }
    }

    /// <summary>
    /// Clone 오브젝트인지 확인 (부모 계층 포함)
    /// </summary>
    private static bool IsCloneOrChildOfClone(GameObject go, out GameObject cloneRoot)
    {
        cloneRoot = null;
        Transform current = go.transform;

        while (current != null)
        {
            if (current.name.Contains("(Clone)"))
            {
                cloneRoot = current.gameObject;
                return true;
            }
            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// Clone 오브젝트에서 원본 프리팹 경로 찾기
    /// </summary>
    private static string GetPrefabPath(GameObject go)
    {
        // Clone 루트 찾기
        GameObject cloneRoot;
        if (!IsCloneOrChildOfClone(go, out cloneRoot))
        {
            cloneRoot = go;
        }

        // (Clone) 제거한 이름으로 프리팹 찾기
        string originalName = cloneRoot.name.Replace("(Clone)", "").Trim();

        // 프리팹 검색
        string[] guids = AssetDatabase.FindAssets($"t:Prefab {originalName}");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.name == originalName)
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// 프리팹 내 자식 경로 구하기 (루트면 빈 문자열)
    /// </summary>
    private static string GetChildPathInPrefab(GameObject go)
    {
        // Clone 루트 찾기
        GameObject cloneRoot;
        if (!IsCloneOrChildOfClone(go, out cloneRoot))
        {
            return "";
        }

        // 루트와 같으면 빈 문자열
        if (go == cloneRoot)
        {
            return "";
        }

        // 자식 경로 계산
        List<string> pathParts = new List<string>();
        Transform current = go.transform;

        while (current != null && current.gameObject != cloneRoot)
        {
            pathParts.Insert(0, current.name);
            current = current.parent;
        }

        return string.Join("/", pathParts);
    }

    private static void RestoreSavedComponents()
    {
        bool hasChanges = false;

        // 1. 프리팹 변경 사항 적용
        if (prefabsToSave.Count > 0)
        {
            foreach (var prefabData in prefabsToSave)
            {
                ApplyToPrefab(prefabData);
                hasChanges = true;
            }
            prefabsToSave.Clear();
        }

        // 2. 씬 오브젝트 변경 사항 적용
        if (componentsToSave.Count > 0)
        {
            foreach (var data in componentsToSave)
            {
                // 경로로 게임오브젝트 찾기
                GameObject go = GameObject.Find(data.gameObjectPath);
                if (go == null)
                {
                    // 루트가 아닌 경우 씬에서 검색
                    go = FindGameObjectByPath(data.gameObjectPath);
                }

                if (go != null)
                {
                    var type = System.Type.GetType(data.componentType);
                    if (type != null)
                    {
                        var component = go.GetComponent(type);
                        if (component != null)
                        {
                            Undo.RecordObject(component, "Restore PlayMode Values");
                            EditorJsonUtility.FromJsonOverwrite(data.json, component);
                            EditorUtility.SetDirty(component);
                            Debug.Log($"[PlayModeSaver] 복원됨: {type.Name} on {go.name}");
                            hasChanges = true;
                        }
                    }
                }
            }
            componentsToSave.Clear();
        }

        savedComponents.Clear();
        isCloneObject.Clear();

        // 저장 알림
        if (hasChanges)
        {
            Debug.Log("[PlayModeSaver] 플레이 모드 변경 사항이 복원되었습니다. Ctrl+S로 씬을 저장하세요.");
        }
    }

    /// <summary>
    /// 프리팹에 변경 사항 적용
    /// </summary>
    private static void ApplyToPrefab(PrefabComponentToSave prefabData)
    {
        // 프리팹 로드
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabData.prefabPath);
        if (prefabAsset == null)
        {
            Debug.LogWarning($"[PlayModeSaver] 프리팹을 찾을 수 없음: {prefabData.prefabPath}");
            return;
        }

        // 타겟 오브젝트 찾기 (childPath가 비어있으면 루트)
        GameObject targetObj = prefabAsset;
        if (!string.IsNullOrEmpty(prefabData.childPath))
        {
            Transform child = prefabAsset.transform.Find(prefabData.childPath);
            if (child != null)
                targetObj = child.gameObject;
        }

        // 컴포넌트 찾기
        var type = System.Type.GetType(prefabData.componentType);
        if (type == null)
        {
            Debug.LogWarning($"[PlayModeSaver] 컴포넌트 타입을 찾을 수 없음: {prefabData.componentType}");
            return;
        }

        var component = targetObj.GetComponent(type);
        if (component == null)
        {
            Debug.LogWarning($"[PlayModeSaver] 프리팹에서 컴포넌트를 찾을 수 없음: {type.Name}");
            return;
        }

        // 프리팹 수정
        Undo.RecordObject(component, "Apply PlayMode Values to Prefab");
        EditorJsonUtility.FromJsonOverwrite(prefabData.json, component);
        EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(prefabAsset);

        // 프리팹 저장
        AssetDatabase.SaveAssets();

        Debug.Log($"[PlayModeSaver] 프리팹 저장됨: {type.Name} → {prefabData.prefabPath}");
    }

    private static string GetGameObjectPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private static GameObject FindGameObjectByPath(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length == 0) return null;

        // 루트 오브젝트들에서 검색
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == parts[0])
            {
                if (parts.Length == 1) return root;

                Transform current = root.transform;
                for (int i = 1; i < parts.Length; i++)
                {
                    current = current.Find(parts[i]);
                    if (current == null) break;
                }

                if (current != null) return current.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// 컴포넌트를 저장 목록에 추가
    /// </summary>
    public static void MarkForSave(Component component)
    {
        if (component == null) return;

        int id = component.GetInstanceID();
        if (!savedComponents.ContainsKey(id))
        {
            savedComponents[id] = "";

            // Clone 오브젝트인지 확인 (부모 계층 포함)
            GameObject cloneRoot;
            bool isClone = IsCloneOrChildOfClone(component.gameObject, out cloneRoot);
            isCloneObject[id] = isClone;

            if (isClone)
            {
                string prefabPath = GetPrefabPath(component.gameObject);
                string childPath = GetChildPathInPrefab(component.gameObject);

                if (!string.IsNullOrEmpty(prefabPath))
                {
                    string targetInfo = string.IsNullOrEmpty(childPath)
                        ? prefabPath
                        : $"{prefabPath} ({childPath})";
                    Debug.Log($"[PlayModeSaver] 마킹됨 (Clone → 프리팹): {component.GetType().Name} → {targetInfo}");
                }
                else
                {
                    Debug.LogWarning($"[PlayModeSaver] Clone 오브젝트 마킹됨, 하지만 원본 프리팹을 찾을 수 없음: {component.gameObject.name}");
                }
            }
            else
            {
                Debug.Log($"[PlayModeSaver] 마킹됨: {component.GetType().Name} - 플레이 모드 종료 시 저장됩니다.");
            }
        }
    }

    /// <summary>
    /// 컴포넌트를 저장 목록에서 제거
    /// </summary>
    public static void UnmarkForSave(Component component)
    {
        if (component == null) return;
        int id = component.GetInstanceID();
        savedComponents.Remove(id);
        isCloneObject.Remove(id);
    }

    // 컨텍스트 메뉴 추가
    [MenuItem("CONTEXT/Component/Save on Play Mode Exit")]
    private static void MarkComponentForSave(MenuCommand command)
    {
        MarkForSave(command.context as Component);
    }

    [MenuItem("CONTEXT/Component/Save on Play Mode Exit", true)]
    private static bool MarkComponentForSaveValidate(MenuCommand command)
    {
        return EditorApplication.isPlaying;
    }

    [MenuItem("CONTEXT/Component/Unmark from Play Mode Save")]
    private static void UnmarkComponentFromSave(MenuCommand command)
    {
        UnmarkForSave(command.context as Component);
        Debug.Log($"[PlayModeSaver] 마킹 해제됨: {command.context.GetType().Name}");
    }

    [MenuItem("CONTEXT/Component/Unmark from Play Mode Save", true)]
    private static bool UnmarkComponentFromSaveValidate(MenuCommand command)
    {
        return EditorApplication.isPlaying && savedComponents.ContainsKey(command.context.GetInstanceID());
    }
}
