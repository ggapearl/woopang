using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// 채팅 버블 프리팹 레이아웃 자동 설정
/// 텍스트 길이에 따라 버블 너비 자동 조절 + TimeArea 가로 배치
/// </summary>
public class ChatBubblePrefabSetup : EditorWindow
{
    [MenuItem("WOOPANG/Setup/채팅 버블 프리팹 레이아웃 수정")]
    public static void SetupBubblePrefabs()
    {
        // AdminMessageBubble 수정
        SetupPrefab("Assets/Prefabs/DM/AdminMessageBubble.prefab", false);

        // OtherMessageBubble 수정
        SetupPrefab("Assets/Prefabs/DM/OtherMessageBubble.prefab", false);

        // MyMessageBubble 수정
        SetupPrefab("Assets/Prefabs/DM/MyMessageBubble.prefab", true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[ChatBubblePrefabSetup] 모든 버블 프리팹 레이아웃 수정 완료");
    }

    private static void SetupPrefab(string prefabPath, bool isMine)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[ChatBubblePrefabSetup] 프리팹을 찾을 수 없음: {prefabPath}");
            return;
        }

        // 프리팹 편집 모드
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);

        try
        {
            SetupBubbleLayout(instance, isMine);
            PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            Debug.Log($"[ChatBubblePrefabSetup] 수정됨: {prefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    private static void SetupBubbleLayout(GameObject root, bool isMine)
    {
        // ============================================================
        // 1. Root (AdminMessageBubble) - HorizontalLayoutGroup
        // ============================================================
        RectTransform rootRect = root.GetComponent<RectTransform>();

        HorizontalLayoutGroup rootHLG = root.GetComponent<HorizontalLayoutGroup>();
        if (rootHLG == null)
            rootHLG = root.AddComponent<HorizontalLayoutGroup>();

        // 정렬: 내 메시지는 오른쪽, 상대방은 왼쪽
        rootHLG.childAlignment = isMine ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        rootHLG.spacing = 8f;
        rootHLG.padding = new RectOffset(12, 12, 4, 4);
        rootHLG.childForceExpandWidth = false;
        rootHLG.childForceExpandHeight = false;
        rootHLG.childControlWidth = true;  // 자식 너비 제어 활성화 (ContentSizeFitter 작동)
        rootHLG.childControlHeight = true;

        // Root ContentSizeFitter - 세로만 자동
        ContentSizeFitter rootCSF = root.GetComponent<ContentSizeFitter>();
        if (rootCSF == null)
            rootCSF = root.AddComponent<ContentSizeFitter>();
        rootCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Root LayoutElement - flexibleWidth로 부모에 맞춤
        LayoutElement rootLE = root.GetComponent<LayoutElement>();
        if (rootLE == null)
            rootLE = root.AddComponent<LayoutElement>();
        rootLE.flexibleWidth = 1;
        rootLE.preferredWidth = -1;

        // ============================================================
        // 2. BubbleContainer - 텍스트에 맞게 자동 크기 조절
        // ============================================================
        Transform bubbleContainerTr = root.transform.Find("BubbleContainer");
        if (bubbleContainerTr != null)
        {
            GameObject bubbleContainer = bubbleContainerTr.gameObject;

            // ContentSizeFitter - 가로/세로 모두 텍스트에 맞춤
            ContentSizeFitter bubbleCSF = bubbleContainer.GetComponent<ContentSizeFitter>();
            if (bubbleCSF == null)
                bubbleCSF = bubbleContainer.AddComponent<ContentSizeFitter>();
            bubbleCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; // 가로 자동
            bubbleCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // 세로 자동

            // VerticalLayoutGroup - 자식 배치
            VerticalLayoutGroup bubbleVLG = bubbleContainer.GetComponent<VerticalLayoutGroup>();
            if (bubbleVLG == null)
                bubbleVLG = bubbleContainer.AddComponent<VerticalLayoutGroup>();
            bubbleVLG.padding = new RectOffset(20, 20, 14, 14); // 폰트 60 기준 패딩
            bubbleVLG.spacing = 4;
            bubbleVLG.childAlignment = TextAnchor.MiddleLeft;
            bubbleVLG.childForceExpandWidth = false;
            bubbleVLG.childForceExpandHeight = false;
            bubbleVLG.childControlWidth = true;
            bubbleVLG.childControlHeight = true;

            // ============================================================
            // 3. ContentText - 텍스트 길이에 맞게 자동 크기
            // ============================================================
            Transform contentTextTr = bubbleContainer.transform.Find("ContentText");
            if (contentTextTr != null)
            {
                Text contentText = contentTextTr.GetComponent<Text>();
                if (contentText != null)
                {
                    // 폰트 설정 (프리팹 기본값)
                    contentText.fontSize = 60;
                    contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    contentText.verticalOverflow = VerticalWrapMode.Overflow;
                }

                // LayoutElement - preferredWidth 제거 (자동 크기)
                LayoutElement contentLE = contentTextTr.GetComponent<LayoutElement>();
                if (contentLE != null)
                {
                    contentLE.preferredWidth = -1; // 자동
                    contentLE.minWidth = 100; // 최소 너비
                    contentLE.flexibleWidth = 0; // flexible 비활성화
                }

                // ContentSizeFitter 추가 - 텍스트 크기에 맞춤
                ContentSizeFitter contentCSF = contentTextTr.GetComponent<ContentSizeFitter>();
                if (contentCSF == null)
                    contentCSF = contentTextTr.gameObject.AddComponent<ContentSizeFitter>();
                contentCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // LabelText는 비활성화 (ChatTitle로 대체)
            Transform labelTextTr = bubbleContainer.transform.Find("LabelText");
            if (labelTextTr != null)
            {
                labelTextTr.gameObject.SetActive(false);
            }
        }

        // ============================================================
        // 4. TimeArea - 버블 옆에 가로 1줄
        // ============================================================
        Transform timeAreaTr = root.transform.Find("TimeArea");
        if (timeAreaTr != null)
        {
            // LayoutElement - 시간 텍스트 폰트 28 기준 적절한 너비
            LayoutElement timeLE = timeAreaTr.GetComponent<LayoutElement>();
            if (timeLE == null)
                timeLE = timeAreaTr.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredWidth = 150; // "오후 12:30" 정도 표시 가능
            timeLE.minWidth = 100;
            timeLE.flexibleWidth = 0;

            // VerticalLayoutGroup - 하단 정렬 (버블 아래쪽에 시간)
            VerticalLayoutGroup timeVLG = timeAreaTr.GetComponent<VerticalLayoutGroup>();
            if (timeVLG == null)
                timeVLG = timeAreaTr.gameObject.AddComponent<VerticalLayoutGroup>();
            timeVLG.childAlignment = TextAnchor.LowerLeft; // 하단 왼쪽 정렬
            timeVLG.padding = new RectOffset(0, 0, 0, 8);
            timeVLG.childForceExpandWidth = true;
            timeVLG.childForceExpandHeight = false;
            timeVLG.childControlWidth = true;
            timeVLG.childControlHeight = true;

            // TimeText 설정
            Transform timeTextTr = timeAreaTr.Find("TimeText");
            if (timeTextTr != null)
            {
                Text timeText = timeTextTr.GetComponent<Text>();
                if (timeText != null)
                {
                    timeText.fontSize = 28; // 폰트 60 기준
                    timeText.alignment = TextAnchor.LowerLeft;
                    timeText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }

                // ContentSizeFitter 추가
                ContentSizeFitter timeCSF = timeTextTr.GetComponent<ContentSizeFitter>();
                if (timeCSF == null)
                    timeCSF = timeTextTr.gameObject.AddComponent<ContentSizeFitter>();
                timeCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                timeCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // RectTransform 크기 자동
                RectTransform timeTextRect = timeTextTr.GetComponent<RectTransform>();
                if (timeTextRect != null)
                {
                    timeTextRect.sizeDelta = new Vector2(0, 0);
                }
            }
        }

        // ============================================================
        // 5. AvatarContainer - 크기 유지
        // ============================================================
        Transform avatarTr = root.transform.Find("AvatarContainer");
        if (avatarTr != null)
        {
            LayoutElement avatarLE = avatarTr.GetComponent<LayoutElement>();
            if (avatarLE == null)
                avatarLE = avatarTr.gameObject.AddComponent<LayoutElement>();
            avatarLE.preferredWidth = 100;  // 폰트 60 기준
            avatarLE.preferredHeight = 100;
            avatarLE.minWidth = 100;
            avatarLE.minHeight = 100;
        }
    }
}
