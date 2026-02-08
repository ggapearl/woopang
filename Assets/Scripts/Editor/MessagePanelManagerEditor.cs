using UnityEditor;

/// <summary>
/// MessagePanelManager 커스텀 Inspector
/// </summary>
[CustomEditor(typeof(MessagePanelManager))]
public class MessagePanelManagerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
