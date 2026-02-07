using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LoginManager))]
public class LoginManagerEditor : UnityEditor.Editor
{
    private string clipboardDeepLink = "";
    private bool showDeepLinkSection = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LoginManager manager = (LoginManager)target;

        EditorGUILayout.Space(10);

        // ============================================================
        // 현재 로그인 상태
        // ============================================================
        if (manager.CurrentUser != null)
        {
            EditorGUILayout.HelpBox(
                $"현재 로그인: {manager.CurrentUsername} (ID: {manager.CurrentUserId})\n" +
                $"Provider: {PlayerPrefs.GetString("LoginProvider", "editor-test")}",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("로그인되지 않음", MessageType.Warning);
        }

        EditorGUILayout.Space(5);

        // ============================================================
        // Web Login (실제 Google/Kakao/Apple 로그인)
        // ============================================================
        EditorGUILayout.LabelField("실제 로그인 (Google/Kakao/Apple)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. 'Web Login' 클릭 → 브라우저에서 Google 로그인\n" +
            "2. 로그인 완료 후 딥링크 URL 복사\n" +
            "3. 아래 '딥링크 시뮬레이션'에 붙여넣기 → 'Apply'",
            MessageType.None);

        GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
        if (GUILayout.Button("Web Login (브라우저 열기)", GUILayout.Height(35)))
        {
            manager.OpenWebLogin();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(3);

        // 딥링크 시뮬레이션 섹션
        showDeepLinkSection = EditorGUILayout.Foldout(showDeepLinkSection, "딥링크 시뮬레이션");
        if (showDeepLinkSection)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("딥링크 URL:", EditorStyles.miniLabel);
            clipboardDeepLink = EditorGUILayout.TextField(clipboardDeepLink);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Paste from Clipboard"))
            {
                clipboardDeepLink = GUIUtility.systemCopyBuffer ?? "";
            }
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            GUI.enabled = !string.IsNullOrEmpty(clipboardDeepLink) && clipboardDeepLink.StartsWith("woopang://auth");
            if (GUILayout.Button("Apply (로그인 실행)"))
            {
                manager.SimulateDeepLink(clipboardDeepLink);
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(10);

        // ============================================================
        // 에디터 퀵 테스트 (DB 없이 로컬 테스트)
        // ============================================================
        EditorGUILayout.LabelField("퀵 테스트 (로컬 전용)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f);
        if (GUILayout.Button("Test Login", GUILayout.Height(25)))
        {
            manager.EditorLogin();
        }

        GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
        if (GUILayout.Button("Logout", GUILayout.Height(25)))
        {
            manager.EditorLogout();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }
}
