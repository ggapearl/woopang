using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

[CustomEditor(typeof(CommentItem))]
public class CommentItemEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CommentItem commentItem = (CommentItem)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("상태 확인", EditorStyles.boldLabel);

        // likeButton 자식에 Image가 있는지 확인
        if (commentItem.likeButton != null)
        {
            Image childImage = commentItem.likeButton.GetComponentInChildren<Image>();
            if (childImage != null && childImage.gameObject != commentItem.likeButton.gameObject)
            {
                EditorGUILayout.HelpBox($"Heart Icon Image: {childImage.gameObject.name} (자동 감지됨)", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("LikeButton에 자식 Image가 없습니다. Tools > Setup CommentItem Prefab 실행 필요", MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("likeButton이 연결되지 않았습니다.", MessageType.Error);
        }

        // 스프라이트 연결 상태 확인
        if (commentItem.likeIcon == null)
        {
            EditorGUILayout.HelpBox("likeIcon (빈 하트 스프라이트)을 연결해주세요.", MessageType.Warning);
        }

        if (commentItem.likedSprite == null)
        {
            EditorGUILayout.HelpBox("likedSprite (채워진 하트 스프라이트)을 연결해주세요.", MessageType.Warning);
        }
    }
}
