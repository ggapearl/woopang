using UnityEngine;
using UnityEditor;

/// <summary>
/// MobileKeyboardHandler 커스텀 Inspector
/// iOS/Android 가상 키보드 프리뷰 버튼 제공
/// </summary>
[CustomEditor(typeof(MobileKeyboardHandler))]
public class MobileKeyboardHandlerEditor : UnityEditor.Editor
{
    // Canvas 기준 키보드 높이 (대략적인 비율)
    // iPhone: 화면의 약 40~45%
    // Android: 화면의 약 35~40%
    private static readonly float IOS_KEYBOARD_RATIO = 0.42f;
    private static readonly float ANDROID_KEYBOARD_RATIO = 0.37f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MobileKeyboardHandler handler = (MobileKeyboardHandler)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("가상 키보드 프리뷰", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "에디터에서 키보드 레이아웃을 미리 확인합니다.\n" +
            "iOS: 화면의 약 42% / Android: 약 37%",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();

        // iOS 키보드 프리뷰
        GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
        if (GUILayout.Button("iOS 키보드 프리뷰", GUILayout.Height(35)))
        {
            float keyboardHeight = GetCanvasKeyboardHeight(handler, IOS_KEYBOARD_RATIO);
            handler.EditorPreviewKeyboard(keyboardHeight);
            SceneView.RepaintAll();
            EditorUtility.SetDirty(handler);
        }

        // Android 키보드 프리뷰
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Android 키보드 프리뷰", GUILayout.Height(35)))
        {
            float keyboardHeight = GetCanvasKeyboardHeight(handler, ANDROID_KEYBOARD_RATIO);
            handler.EditorPreviewKeyboard(keyboardHeight);
            SceneView.RepaintAll();
            EditorUtility.SetDirty(handler);
        }

        EditorGUILayout.EndHorizontal();

        // 리셋 버튼
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("프리뷰 해제 (원래 위치)", GUILayout.Height(30)))
        {
            handler.EditorResetPreview();
            SceneView.RepaintAll();
            EditorUtility.SetDirty(handler);
        }
        GUI.backgroundColor = Color.white;

        // 현재 프리뷰 상태 표시
        if (handler.editorPreviewActive)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                $"프리뷰 활성 - 가상 키보드 높이: {handler.editorPreviewKeyboardHeight:F0}px (Canvas 기준)\n" +
                "레이아웃 조정 후 '프리뷰 해제' 버튼을 눌러 원래 상태로 복원하세요.",
                MessageType.Warning);
        }
    }

    /// <summary>
    /// Canvas 기준 키보드 높이 계산
    /// </summary>
    private float GetCanvasKeyboardHeight(MobileKeyboardHandler handler, float ratio)
    {
        // Canvas RectTransform에서 높이 가져오기
        Canvas canvas = null;

        if (handler.backgroundRect != null)
            canvas = handler.backgroundRect.GetComponentInParent<Canvas>();
        else if (handler.inputAreaRect != null)
            canvas = handler.inputAreaRect.GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
                return canvasRect.rect.height * ratio;
        }

        // fallback: 화면 높이 기준
        return Screen.height * ratio;
    }
}
