using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

/// <summary>
/// 채팅 버블 프리팹 설정 (수동 실행만 가능)
/// 자동 실행 없음 - 사용자가 WOOPANG 메뉴에서 직접 실행
/// </summary>
public class ChatBubbleSetup
{
    // 자동 실행 제거 - 수동으로만 실행

    [MenuItem("WOOPANG/Setup/채팅 버블 프리팹 Layer 수정")]
    public static void FixPrefabLayers()
    {
        string[] prefabPaths = new string[]
        {
            "Assets/Prefabs/DM/AdminMessageBubble.prefab",
            "Assets/Prefabs/DM/ModernMyBubble.prefab",
            "Assets/Prefabs/DM/ModernOtherBubble.prefab",
            "Assets/Prefabs/DM/AdminNoticeItem.prefab",
            "Assets/Prefabs/DM/ConversationItem.prefab"
        };

        int fixedCount = 0;

        foreach (string path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[ChatBubbleSetup] 프리팹 없음: {path}");
                continue;
            }

            // 프리팹 수정 모드
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(assetPath);

            bool changed = false;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.layer != 5)
                {
                    t.gameObject.layer = 5;
                    changed = true;
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                fixedCount++;
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[ChatBubbleSetup] Layer 수정 완료: {fixedCount}개 프리팹");
    }
}
