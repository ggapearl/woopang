using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LoginManager))]
public class LoginManagerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LoginManager loginManager = (LoginManager)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("에디터 테스트", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "웹에서 로그인 후 나오는 딥링크 URL을 위의 'Test Deep Link Url' 필드에 붙여넣고 아래 버튼을 클릭하세요.\n\n" +
            "예시: woopang://auth?token=6&username=apple&provider=apple",
            MessageType.Info
        );

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("딥링크 테스트 실행", GUILayout.Height(30)))
        {
            if (Application.isPlaying)
            {
                loginManager.TestDeepLinkInEditor();
            }
            else
            {
                EditorUtility.DisplayDialog("알림", "Play 모드에서만 테스트할 수 있습니다.", "확인");
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("현재 로그인 상태", EditorStyles.boldLabel);
            bool isLoggedIn = loginManager.IsLoggedIn;
            EditorGUILayout.LabelField("로그인 여부:", isLoggedIn ? "로그인됨 ✓" : "로그아웃");
            if (isLoggedIn)
            {
                EditorGUILayout.LabelField("User ID:", loginManager.CurrentUserId.ToString());
                EditorGUILayout.LabelField("Username:", loginManager.CurrentUsername ?? "(없음)");
            }
        }
    }
}
