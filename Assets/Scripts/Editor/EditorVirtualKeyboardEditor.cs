using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EditorVirtualKeyboard))]
public class EditorVirtualKeyboardEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("플랫폼 시뮬레이션", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("플레이 모드에서만 동작합니다.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.5f, 0.85f, 0.5f);
            if (GUILayout.Button("Android 키보드", GUILayout.Height(40)))
            {
                ((EditorVirtualKeyboard)target).TogglePlatformKeyboard(EditorVirtualKeyboard.SimulatedPlatform.Android);
            }

            GUI.backgroundColor = new Color(0.6f, 0.75f, 1f);
            if (GUILayout.Button("iOS 키보드", GUILayout.Height(40)))
            {
                ((EditorVirtualKeyboard)target).TogglePlatformKeyboard(EditorVirtualKeyboard.SimulatedPlatform.iOS);
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }
    }
}
