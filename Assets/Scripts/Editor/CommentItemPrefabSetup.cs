using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

public class CommentItemPrefabSetup : EditorWindow
{
    [MenuItem("Tools/Setup CommentItem Prefab")]
    public static void SetupPrefab()
    {
        SetupHeartIcon();
        FixTextDatePosition();
        FixLikeButtonVerticalAlignment();
        LinkCommentItemPrefabToManager();
        Debug.Log("[CommentItemPrefabSetup] 모든 설정 완료!");
    }

    /// <summary>
    /// 씬의 CommentManager에 CommentItem_Template 프리팹 연결
    /// </summary>
    private static void LinkCommentItemPrefabToManager()
    {
        // CommentItem_Template 프리팹 찾기
        string[] guids = AssetDatabase.FindAssets("CommentItem_Template t:Prefab");
        if (guids.Length == 0)
        {
            Debug.LogError("[CommentItemPrefabSetup] CommentItem_Template 프리팹을 찾을 수 없습니다.");
            return;
        }

        string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            Debug.LogError($"[CommentItemPrefabSetup] 프리팹 로드 실패: {prefabPath}");
            return;
        }

        // 씬에서 CommentManager 찾기
        CommentManager commentManager = Object.FindObjectOfType<CommentManager>();
        if (commentManager == null)
        {
            Debug.LogWarning("[CommentItemPrefabSetup] 씬에서 CommentManager를 찾을 수 없습니다. 씬을 열고 다시 실행하세요.");
            return;
        }

        // commentItemPrefab 연결
        if (commentManager.commentItemPrefab == null || commentManager.commentItemPrefab != prefab)
        {
            commentManager.commentItemPrefab = prefab;
            EditorUtility.SetDirty(commentManager);
            EditorSceneManager.MarkSceneDirty(commentManager.gameObject.scene);
            Debug.Log($"[CommentItemPrefabSetup] CommentManager.commentItemPrefab 연결 완료: {prefabPath}");
        }
        else
        {
            Debug.Log("[CommentItemPrefabSetup] CommentManager.commentItemPrefab 이미 연결됨");
        }
    }

    /// <summary>
    /// HeartIcon 설정
    /// </summary>
    private static void SetupHeartIcon()
    {
        // CommentItem_Template 프리팹 찾기
        string[] guids = AssetDatabase.FindAssets("CommentItem_Template t:Prefab");

        if (guids.Length == 0)
        {
            Debug.LogError("[CommentItemPrefabSetup] CommentItem_Template 프리팹을 찾을 수 없습니다.");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogError($"[CommentItemPrefabSetup] 프리팹 로드 실패: {path}");
            return;
        }

        // 프리팹 인스턴스 생성
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        if (instance == null)
        {
            Debug.LogError("[CommentItemPrefabSetup] 프리팹 인스턴스 생성 실패");
            return;
        }

        try
        {
            CommentItem commentItem = instance.GetComponent<CommentItem>();
            if (commentItem == null)
            {
                Debug.LogError("[CommentItemPrefabSetup] CommentItem 컴포넌트를 찾을 수 없습니다.");
                Object.DestroyImmediate(instance);
                return;
            }

            // ★ 루트에 Image 추가 (더블터치 감지를 위한 Raycast Target)
            Image rootImage = instance.GetComponent<Image>();
            if (rootImage == null)
            {
                rootImage = instance.AddComponent<Image>();
                Debug.Log("[CommentItemPrefabSetup] 루트에 Image 컴포넌트 추가 (더블터치 감지용)");
            }
            rootImage.color = new Color(0, 0, 0, 0); // 완전 투명
            rootImage.raycastTarget = true; // Raycast 활성화

            // LikeButton 찾기
            Button likeButton = commentItem.likeButton;
            if (likeButton == null)
            {
                Transform likeBtnTransform = FindChildRecursive(instance.transform, "LikeButton");
                if (likeBtnTransform != null)
                {
                    likeButton = likeBtnTransform.GetComponent<Button>();
                }
            }

            if (likeButton == null)
            {
                Debug.LogError("[CommentItemPrefabSetup] LikeButton을 찾을 수 없습니다.");
                Object.DestroyImmediate(instance);
                return;
            }

            // ★ LikeButton의 LayoutElement에 preferredWidth 설정 (핵심!)
            LayoutElement likeButtonLayout = likeButton.GetComponent<LayoutElement>();
            if (likeButtonLayout != null)
            {
                likeButtonLayout.preferredWidth = 80;
                likeButtonLayout.minWidth = 80;
                Debug.Log("[CommentItemPrefabSetup] LikeButton LayoutElement 너비 설정: 80");
            }

            // Text_Heart 찾기 및 삭제 (대소문자 주의)
            Transform textHeart = likeButton.transform.Find("Text_Heart");
            if (textHeart == null)
            {
                textHeart = likeButton.transform.Find("Text_heart");
            }
            if (textHeart != null)
            {
                Debug.Log("[CommentItemPrefabSetup] Text_Heart 삭제");
                Object.DestroyImmediate(textHeart.gameObject);
            }

            // 기존 HeartIcon 찾기
            Transform existingHeartIcon = likeButton.transform.Find("HeartIcon");
            Image heartIconImage = null;

            if (existingHeartIcon != null)
            {
                heartIconImage = existingHeartIcon.GetComponent<Image>();
                Debug.Log("[CommentItemPrefabSetup] 기존 HeartIcon 발견");
            }

            // HeartIcon이 없으면 새로 생성
            if (heartIconImage == null)
            {
                GameObject heartIconObj = new GameObject("HeartIcon");
                heartIconObj.transform.SetParent(likeButton.transform, false);

                // CanvasRenderer 추가 (UI 렌더링에 필요)
                heartIconObj.AddComponent<CanvasRenderer>();

                heartIconImage = heartIconObj.AddComponent<Image>();
                heartIconImage.color = Color.white;
                heartIconImage.raycastTarget = false;

                existingHeartIcon = heartIconObj.transform;
                Debug.Log("[CommentItemPrefabSetup] HeartIcon 새로 생성");
            }

            // HeartIcon을 첫번째 자식으로 이동 (Text_Count 위에 배치)
            existingHeartIcon.SetAsFirstSibling();

            // ★ RectTransform 설정 - 명시적 크기 지정!
            RectTransform rect = existingHeartIcon.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(60, 60); // ★ 너비와 높이 모두 명시적 지정!
            rect.anchoredPosition = Vector2.zero;

            // LayoutElement로 LayoutGroup에서도 크기 유지
            LayoutElement layoutElement = existingHeartIcon.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = existingHeartIcon.gameObject.AddComponent<LayoutElement>();
            }
            layoutElement.preferredWidth = 60;
            layoutElement.preferredHeight = 60;
            layoutElement.minWidth = 60;
            layoutElement.minHeight = 60;

            // likeIcon 스프라이트 적용
            if (commentItem.likeIcon != null)
            {
                heartIconImage.sprite = commentItem.likeIcon;
                heartIconImage.preserveAspect = true;
                Debug.Log("[CommentItemPrefabSetup] likeIcon 스프라이트 적용됨");
            }

            // 프리팹에 적용
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Debug.Log($"[CommentItemPrefabSetup] 프리팹 저장 완료: {path}");

            // 인스턴스 삭제
            Object.DestroyImmediate(instance);

            Debug.Log("[CommentItemPrefabSetup] 설정 완료!");
            Debug.Log("  - LikeButton 너비: 80");
            Debug.Log("  - HeartIcon 크기: 60x60");
            Debug.Log("  - Text_Heart 삭제됨");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CommentItemPrefabSetup] 오류 발생: {e.Message}\n{e.StackTrace}");
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// LikeButton의 하트아이콘과 좋아요 숫자를 상단으로 정렬
    /// </summary>
    private static void FixLikeButtonVerticalAlignment()
    {
        string[] guids = AssetDatabase.FindAssets("CommentItem_Template t:Prefab");
        if (guids.Length == 0) return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        try
        {
            // LikeButton 찾기
            Transform likeButton = FindChildRecursive(instance.transform, "LikeButton");
            if (likeButton == null)
            {
                Debug.LogWarning("[CommentItemPrefabSetup] LikeButton을 찾을 수 없습니다.");
                Object.DestroyImmediate(instance);
                return;
            }

            // VerticalLayoutGroup의 childAlignment를 UpperCenter로 변경 + 상단 패딩 제거
            VerticalLayoutGroup vlg = likeButton.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.padding = new RectOffset(0, 0, 0, 0); // 패딩 완전 제거
                vlg.spacing = -25; // spacing 더 좁게 (하트와 숫자 간격)
                Debug.Log("[CommentItemPrefabSetup] LikeButton 정렬: UpperCenter, padding=0, spacing=-25");
            }
            else
            {
                Debug.LogWarning("[CommentItemPrefabSetup] LikeButton에 VerticalLayoutGroup이 없습니다.");
            }

            // HeartIcon의 anchoredPosition.y를 위로 올리기
            Transform heartIcon = likeButton.transform.Find("HeartIcon");
            if (heartIcon != null)
            {
                RectTransform heartRect = heartIcon.GetComponent<RectTransform>();
                if (heartRect != null)
                {
                    // 앵커를 상단으로 변경하고 y 위치 조정
                    heartRect.anchorMin = new Vector2(0.5f, 1f); // 상단 중앙
                    heartRect.anchorMax = new Vector2(0.5f, 1f);
                    heartRect.pivot = new Vector2(0.5f, 1f); // 피벗도 상단
                    heartRect.anchoredPosition = new Vector2(0, 0); // 상단에 붙임
                    Debug.Log("[CommentItemPrefabSetup] HeartIcon 앵커를 상단으로 변경");
                }
            }

            // 루트 HorizontalLayoutGroup의 childAlignment도 상단 정렬로 변경
            HorizontalLayoutGroup rootHlg = instance.GetComponent<HorizontalLayoutGroup>();
            if (rootHlg != null)
            {
                rootHlg.childAlignment = TextAnchor.UpperLeft; // 상단 정렬
                rootHlg.padding = new RectOffset(10, 10, 5, 10); // 상단 패딩 줄이기
                Debug.Log("[CommentItemPrefabSetup] 루트 정렬: UpperLeft, top padding=5");
            }

            // 프리팹 저장
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Debug.Log("[CommentItemPrefabSetup] LikeButton 상단 정렬 완료!");

            Object.DestroyImmediate(instance);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CommentItemPrefabSetup] FixLikeButtonVerticalAlignment 오류: {e.Message}");
            if (instance != null) Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Text_date 위치 수정 및 댓글 간격 조정
    /// </summary>
    private static void FixTextDatePosition()
    {
        string[] guids = AssetDatabase.FindAssets("CommentItem_Template t:Prefab");
        if (guids.Length == 0) return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        try
        {
            // ★ 1. Text_username 설정 - 보이도록 크기 확보
            Transform textUsername = FindChildRecursive(instance.transform, "Text_username");
            if (textUsername != null)
            {
                RectTransform usernameRect = textUsername.GetComponent<RectTransform>();
                if (usernameRect != null)
                {
                    // 높이 확보 (최소 50)
                    usernameRect.sizeDelta = new Vector2(usernameRect.sizeDelta.x, 50);
                    Debug.Log("[CommentItemPrefabSetup] Text_username 높이 설정: 50");
                }

                Text usernameText = textUsername.GetComponent<Text>();
                if (usernameText != null)
                {
                    usernameText.fontSize = 36;
                    usernameText.fontStyle = FontStyle.Bold;
                    Debug.Log("[CommentItemPrefabSetup] Text_username 폰트 크기: 36, Bold");
                }

                // ContentSizeFitter 설정
                ContentSizeFitter usernameFitter = textUsername.GetComponent<ContentSizeFitter>();
                if (usernameFitter != null)
                {
                    usernameFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    usernameFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }

            // ★ 2. Text_date 설정 - 보이도록 크기 확보
            Transform textDate = FindChildRecursive(instance.transform, "Text_date");
            if (textDate != null)
            {
                RectTransform dateRect = textDate.GetComponent<RectTransform>();
                if (dateRect != null)
                {
                    // 높이 확보 (최소 50)
                    dateRect.sizeDelta = new Vector2(dateRect.sizeDelta.x, 50);
                    dateRect.anchoredPosition = new Vector2(dateRect.anchoredPosition.x, 0);
                    Debug.Log("[CommentItemPrefabSetup] Text_date 높이 설정: 50, y=0");
                }

                Text dateText = textDate.GetComponent<Text>();
                if (dateText != null)
                {
                    dateText.fontSize = 28;
                    Debug.Log("[CommentItemPrefabSetup] Text_date 폰트 크기: 28");
                }

                // ContentSizeFitter 설정
                ContentSizeFitter dateFitter = textDate.GetComponent<ContentSizeFitter>();
                if (dateFitter != null)
                {
                    dateFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    dateFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }

            // ★ 3. InfoRow 설정 - 높이 확보
            Transform infoRow = FindChildRecursive(instance.transform, "InfoRow");
            if (infoRow != null)
            {
                RectTransform infoRowRect = infoRow.GetComponent<RectTransform>();
                if (infoRowRect != null)
                {
                    // InfoRow 높이 최소 60 확보
                    infoRowRect.sizeDelta = new Vector2(infoRowRect.sizeDelta.x, 60);
                    Debug.Log("[CommentItemPrefabSetup] InfoRow 높이 설정: 60");
                }

                HorizontalLayoutGroup hlg = infoRow.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    hlg.childControlHeight = false; // 자식 높이 제어 비활성화 (각자 크기 유지)
                    hlg.childForceExpandHeight = false;
                    hlg.childAlignment = TextAnchor.MiddleLeft; // 세로 중앙 정렬
                    Debug.Log("[CommentItemPrefabSetup] InfoRow HorizontalLayoutGroup 설정 완료");
                }

                // LayoutElement 추가하여 높이 고정
                LayoutElement infoRowLayout = infoRow.GetComponent<LayoutElement>();
                if (infoRowLayout == null)
                {
                    infoRowLayout = infoRow.gameObject.AddComponent<LayoutElement>();
                }
                infoRowLayout.minHeight = 60;
                infoRowLayout.preferredHeight = 60;
            }

            // 4. ContentArea의 VerticalLayoutGroup spacing 조정
            Transform contentArea = FindChildRecursive(instance.transform, "ContentArea");
            if (contentArea != null)
            {
                VerticalLayoutGroup vlg = contentArea.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.spacing = 5; // 음수 대신 양수로 변경하여 겹치지 않게
                    vlg.childControlHeight = false; // 자식 높이 그대로 유지
                    Debug.Log("[CommentItemPrefabSetup] ContentArea spacing 조정: 5");
                }
            }

            // 5. 루트 HorizontalLayoutGroup padding 조정
            HorizontalLayoutGroup rootHlg = instance.GetComponent<HorizontalLayoutGroup>();
            if (rootHlg != null)
            {
                rootHlg.padding = new RectOffset(10, 10, 10, 10);
                Debug.Log("[CommentItemPrefabSetup] 루트 padding 조정: 10px");
            }

            // 프리팹 저장
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Debug.Log("[CommentItemPrefabSetup] 레이아웃 조정 완료!");

            Object.DestroyImmediate(instance);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CommentItemPrefabSetup] FixTextDatePosition 오류: {e.Message}");
            if (instance != null) Object.DestroyImmediate(instance);
        }
    }
}
