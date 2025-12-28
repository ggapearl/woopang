#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// VirtualLocation용 에디터 도구
/// </summary>
public class VirtualLocationEditor : EditorWindow
{
    private float customLatitude = 36.6361f;
    private float customLongitude = 126.8280f;
    private int selectedPreset = 0;

    [MenuItem("Tools/Woopang/Virtual Location Manager")]
    public static void ShowWindow()
    {
        VirtualLocationEditor window = GetWindow<VirtualLocationEditor>("Virtual Location");
        window.minSize = new Vector2(400, 350);
    }

    private void OnGUI()
    {
        GUILayout.Label("가상 GPS 좌표 관리", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 현재 VirtualLocation 인스턴스 찾기
        VirtualLocation virtualLoc = FindFirstObjectByType<VirtualLocation>();

        if (virtualLoc == null)
        {
            EditorGUILayout.HelpBox("씬에 VirtualLocation 오브젝트가 없습니다.", MessageType.Warning);
            if (GUILayout.Button("VirtualLocation 오브젝트 생성", GUILayout.Height(30)))
            {
                CreateVirtualLocationObject();
            }
            return;
        }

        // 현재 좌표 표시
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("현재 좌표", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"위도: {virtualLoc.Latitude}");
        EditorGUILayout.LabelField($"경도: {virtualLoc.Longitude}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // 프리셋 선택
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("프리셋 위치", EditorStyles.boldLabel);

        string[] presetNames = new string[virtualLoc.presets.Length];
        for (int i = 0; i < virtualLoc.presets.Length; i++)
        {
            presetNames[i] = virtualLoc.presets[i].name;
        }

        selectedPreset = EditorGUILayout.Popup("위치 선택", selectedPreset, presetNames);

        if (GUILayout.Button("프리셋 적용", GUILayout.Height(30)))
        {
            virtualLoc.ApplyPreset(selectedPreset);
            EditorUtility.SetDirty(virtualLoc);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // 직접 입력
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("직접 좌표 입력", EditorStyles.boldLabel);
        customLatitude = EditorGUILayout.FloatField("위도", customLatitude);
        customLongitude = EditorGUILayout.FloatField("경도", customLongitude);

        if (GUILayout.Button("좌표 적용", GUILayout.Height(30)))
        {
            virtualLoc.SetCoordinates(customLatitude, customLongitude);
            EditorUtility.SetDirty(virtualLoc);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // Inspector에서 VirtualLocation 선택
        if (GUILayout.Button("Inspector에서 VirtualLocation 선택", GUILayout.Height(25)))
        {
            Selection.activeGameObject = virtualLoc.gameObject;
        }
    }

    private void CreateVirtualLocationObject()
    {
        GameObject go = new GameObject("VirtualLocation");
        go.AddComponent<VirtualLocation>();
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log("[VirtualLocationEditor] VirtualLocation 오브젝트가 생성되었습니다.");
    }

    [MenuItem("Tools/Woopang/Create Virtual Location")]
    public static void CreateVirtualLocation()
    {
        VirtualLocation existing = FindFirstObjectByType<VirtualLocation>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("알림", "VirtualLocation 오브젝트가 이미 존재합니다.", "확인");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        GameObject go = new GameObject("VirtualLocation");
        go.AddComponent<VirtualLocation>();
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log("[VirtualLocationEditor] VirtualLocation 오브젝트가 생성되었습니다.");
    }
}
#endif
