using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FirstTimeGuide))]
public class FirstTimeGuideEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        FirstTimeGuide guide = (FirstTimeGuide)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("테스트 도구", EditorStyles.boldLabel);

        int isFirstTime = PlayerPrefs.GetInt("IsFirstTime", 0);
        string status = isFirstTime == 0
            ? "<color=green>미표시 (다음 실행 시 가이드 표시)</color>"
            : "<color=#FF8888>표시 완료 (가이드 자동 표시 안됨)</color>";
        GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };
        EditorGUILayout.LabelField("현재 IsFirstTime 플래그:", status, richStyle);

        EditorGUILayout.Space(5);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("▶ Show Guide Now (Play 중에만 동작)", GUILayout.Height(32)))
            {
                guide.ForceShowGuide();
            }
        }

        if (GUILayout.Button("🔄 Reset First Time Flag", GUILayout.Height(26)))
        {
            guide.ResetFirstTimeFlag();
        }
    }
}
