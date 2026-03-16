using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DataManager))]
public class DataManagerEditor : UnityEditor.Editor
{
    // 하드코딩된 필드 목록 (초록색으로 표시)
    private static readonly string[] hardcodedFields = new string[]
    {
    };

    private static readonly Color hardcodedBgColor = new Color(0.18f, 0.55f, 0.34f, 0.35f);
    private static readonly Color hardcodedLabelColor = new Color(0.3f, 0.85f, 0.5f, 1f);

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
                continue;
            }

            bool isHardcoded = System.Array.Exists(hardcodedFields, f => f == iterator.name);

            if (isHardcoded)
            {
                DrawHardcodedField(iterator);
            }
            else
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHardcodedField(SerializedProperty property)
    {
        Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight + 4f);

        Rect bgRect = new Rect(rect.x - 2f, rect.y - 1f, rect.width + 4f, rect.height + 2f);
        EditorGUI.DrawRect(bgRect, hardcodedBgColor);

        GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
        labelStyle.normal.textColor = hardcodedLabelColor;
        labelStyle.fontStyle = FontStyle.Bold;

        Rect labelRect = new Rect(rect.x, rect.y + 2f, EditorGUIUtility.labelWidth, rect.height);
        Rect valueRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y + 2f, rect.width - EditorGUIUtility.labelWidth - 80f, rect.height);
        Rect badgeRect = new Rect(rect.xMax - 76f, rect.y + 2f, 74f, rect.height - 2f);

        EditorGUI.LabelField(labelRect, property.displayName, labelStyle);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUI.PropertyField(valueRect, property, GUIContent.none);
        }

        GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel);
        badgeStyle.normal.textColor = hardcodedLabelColor;
        badgeStyle.alignment = TextAnchor.MiddleRight;
        badgeStyle.fontStyle = FontStyle.Bold;
        badgeStyle.fontSize = 9;
        EditorGUI.LabelField(badgeRect, "HARDCODED", badgeStyle);
    }
}
