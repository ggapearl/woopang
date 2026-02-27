using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// 채팅방 스켈레톤 로딩 프리팹 생성 에디터
/// 메뉴: WOOPANG/Create Chat Skeleton Prefab
/// </summary>
public class ChatSkeletonPrefabCreator
{
    [MenuItem("WOOPANG/Create Chat Skeleton Prefab")]
    public static void CreatePrefab()
    {
        // 루트 컨테이너
        GameObject root = new GameObject("ChatSkeletonLoading");
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // VerticalLayoutGroup으로 버블들 배치
        VerticalLayoutGroup vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.spacing = 24f;
        vlg.padding = new RectOffset(0, 0, 40, 40);

        // 대화 형태 스켈레톤 버블 배치 (2배 사이즈)
        // (왼쪽=상대방, 오른쪽=나) 교차 패턴
        CreateBubbleRow(root.transform, false, 0.55f, 88f);   // 왼쪽 짧은
        CreateBubbleRow(root.transform, false, 0.7f, 112f);   // 왼쪽 중간
        CreateBubbleRow(root.transform, true, 0.5f, 88f);     // 오른쪽 짧은
        CreateBubbleRow(root.transform, false, 0.6f, 88f);    // 왼쪽
        CreateBubbleRow(root.transform, true, 0.65f, 112f);   // 오른쪽 중간
        CreateBubbleRow(root.transform, true, 0.4f, 88f);     // 오른쪽 짧은
        CreateBubbleRow(root.transform, false, 0.75f, 136f);  // 왼쪽 긴
        CreateBubbleRow(root.transform, true, 0.55f, 88f);    // 오른쪽

        // 프리팹 저장
        string path = "Assets/Prefabs/DM/ChatSkeletonLoading.prefab";
        // 기존 프리팹 있으면 덮어쓰기
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        else
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        Object.DestroyImmediate(root);

        // MessagePanelManager에 자동 연결
        var manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager != null)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset != null)
            {
                manager.chatLoadingPrefab = prefabAsset;
                EditorUtility.SetDirty(manager);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                Debug.Log("[ChatSkeleton] MessagePanelManager.chatLoadingPrefab 자동 연결 완료");
            }
        }

        Debug.Log($"[ChatSkeleton] 프리팹 생성 완료: {path}");
        EditorUtility.DisplayDialog("완료", $"프리팹 생성됨:\n{path}\n\nMessagePanelManager에 자동 연결됨.\nInspector에서 직접 수정 가능합니다.", "확인");
    }

    private static void CreateBubbleRow(Transform parent, bool isRight, float widthRatio, float height)
    {
        string sideName = isRight ? "Right" : "Left";
        GameObject row = new GameObject($"SkeletonBubble_{sideName}");
        row.transform.SetParent(parent, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, height + 16f);

        LayoutElement rowLe = row.AddComponent<LayoutElement>();
        rowLe.preferredHeight = height + 16f;
        rowLe.minHeight = height + 16f;

        // 수평 레이아웃 (좌/우 정렬)
        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = isRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.padding = new RectOffset(
            isRight ? 200 : 32,   // left padding
            isRight ? 32 : 200,   // right padding
            8, 8
        );

        // 왼쪽 버블이면 아바타 원 추가
        if (!isRight)
        {
            GameObject avatar = new GameObject("Avatar");
            avatar.transform.SetParent(row.transform, false);

            RectTransform avatarRect = avatar.AddComponent<RectTransform>();
            avatarRect.sizeDelta = new Vector2(72f, 72f);

            Image avatarImg = avatar.AddComponent<Image>();
            avatarImg.color = new Color(0.25f, 0.25f, 0.30f, 1f);
            avatarImg.raycastTarget = false;

            // ShimmerEffect 추가
            avatar.AddComponent<ShimmerEffect>();

            // 아바타와 버블 사이 간격
            LayoutElement avatarLe = avatar.AddComponent<LayoutElement>();
            avatarLe.preferredWidth = 72f;
            avatarLe.preferredHeight = 72f;

            // 스페이서
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(row.transform, false);
            RectTransform spacerRect = spacer.AddComponent<RectTransform>();
            spacerRect.sizeDelta = new Vector2(16f, height);
            LayoutElement spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.preferredWidth = 16f;
        }

        // 버블 본체
        GameObject bubble = new GameObject("Bubble");
        bubble.transform.SetParent(row.transform, false);

        RectTransform bubbleRect = bubble.AddComponent<RectTransform>();
        float bubbleWidth = 480f * widthRatio;
        bubbleRect.sizeDelta = new Vector2(bubbleWidth, height);

        LayoutElement bubbleLe = bubble.AddComponent<LayoutElement>();
        bubbleLe.preferredWidth = bubbleWidth;
        bubbleLe.preferredHeight = height;

        Image bubbleImg = bubble.AddComponent<Image>();
        bubbleImg.color = isRight
            ? new Color(0.20f, 0.22f, 0.32f, 0.8f)   // 내 버블 (약간 푸른 톤)
            : new Color(0.22f, 0.22f, 0.26f, 0.9f);   // 상대 버블 (어두운 톤)
        bubbleImg.raycastTarget = false;

        // ShimmerEffect 추가
        bubble.AddComponent<ShimmerEffect>();
    }
}
