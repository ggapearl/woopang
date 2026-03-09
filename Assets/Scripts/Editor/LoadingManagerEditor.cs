using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LoadingManager))]
public class LoadingManagerEditor : UnityEditor.Editor
{
    private bool showDebugSection = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LoadingManager mgr = (LoadingManager)target;

        EditorGUILayout.Space(10);
        showDebugSection = EditorGUILayout.Foldout(showDebugSection, "AR Debug Test (Play Mode)", true, EditorStyles.foldoutHeader);

        if (!showDebugSection) return;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 모드에서만 테스트 가능합니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("AR 환경 시뮬레이션", EditorStyles.boldLabel);

        if (GUILayout.Button("Session Preparing (폴백모드 + 점 애니메이션)"))
            mgr.DebugSessionPreparing();

        if (GUILayout.Button("Dark Environment (너무 어두움)"))
            mgr.DebugDarkEnvironment();

        if (GUILayout.Button("No Features (특징점 부족)"))
            mgr.DebugNoFeatures();

        if (GUILayout.Button("Camera Covered (카메라 가림)"))
            mgr.DebugCameraCovered();

        if (GUILayout.Button("Data Loading (오브젝트 처리 중)"))
            mgr.DebugDataLoading();

        if (GUILayout.Button("Background Recovery (백그라운드 복구)"))
            mgr.DebugBackgroundRecovery();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("복구 / 제어", EditorStyles.boldLabel);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Hide Guidance (정상 복구)"))
            mgr.DebugHideGuidance();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("OffScreen Indicator 폴백", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fallback ON"))
            mgr.DebugFallbackOn();
        if (GUILayout.Button("Fallback OFF"))
            mgr.DebugFallbackOff();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }
}
