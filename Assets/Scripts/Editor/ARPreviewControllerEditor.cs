using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ARPreviewController))]
public class ARPreviewControllerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ARPreviewController controller = (ARPreviewController)target;

        EditorGUILayout.Space(20);

        // ============================================================
        // 테스트 버튼 영역
        // ============================================================
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // 제목
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("AR Preview Test", titleStyle);
        EditorGUILayout.Space(5);

        // Play 모드 전체 테스트 버튼 (큰 버튼)
        if (Application.isPlaying)
        {
            // 초록색 Play 모드 버튼
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f, 1f);
            GUIStyle bigButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                fixedHeight = 50
            };

            if (GUILayout.Button("▶  전체 프리뷰 테스트 (Play Mode)", bigButtonStyle))
            {
                controller.TestFullPreviewInPlayMode();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Play 모드 테스트: 스피너 → 큐브 전환 → 파티클 버스트 → 스포트라이트 → 배경 딤 → 터치/자동 회전 모든 효과 순서대로 재생",
                MessageType.Info);
        }
        else
        {
            // Edit 모드 버튼들
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f, 1f);
            if (GUILayout.Button("큐브 생성 (Scene View)", GUILayout.Height(35)))
            {
                controller.TestARPreviewInEditor();
            }

            GUI.backgroundColor = new Color(1f, 0.5f, 0.4f, 1f);
            if (GUILayout.Button("큐브 제거", GUILayout.Height(35)))
            {
                controller.ClearTestCube();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Play 모드 안내
            GUI.backgroundColor = new Color(1f, 0.95f, 0.7f, 1f);
            GUIStyle playGuideStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };

            GUI.enabled = false;
            GUILayout.Button("▶  Play 모드에서 전체 테스트 가능", playGuideStyle);
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.HelpBox(
                "Edit 모드: 큐브 생성/제거만 가능\nPlay 모드: 스피너, 파티클, 딤, 회전 등 전체 효과 테스트",
                MessageType.Info);
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }
}
