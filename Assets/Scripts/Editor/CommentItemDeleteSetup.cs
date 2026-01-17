using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// CommentItem 프리팹에 스와이프 삭제 UI를 자동 설정하는 에디터 스크립트
/// </summary>
public class CommentItemDeleteSetup : EditorWindow
{
    [MenuItem("Woopang/Setup Comment Delete UI")]
    public static void ShowWindow()
    {
        GetWindow<CommentItemDeleteSetup>("Comment Delete Setup");
    }

    private GameObject commentItemPrefab;

    private void OnGUI()
    {
        GUILayout.Label("CommentItem 삭제 버튼 설정", EditorStyles.boldLabel);
        GUILayout.Space(10);

        commentItemPrefab = (GameObject)EditorGUILayout.ObjectField("CommentItem Prefab", commentItemPrefab, typeof(GameObject), true);

        GUILayout.Space(10);

        if (GUILayout.Button("씬에서 선택된 CommentItem에 설정", GUILayout.Height(30)))
        {
            SetupSelectedCommentItem();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("CommentItem_Template 프리팹에 설정", GUILayout.Height(30)))
        {
            SetupCommentItemPrefab();
        }

        GUILayout.Space(20);
        GUILayout.Label("설명:", EditorStyles.boldLabel);
        GUILayout.Label("이 도구는 CommentItem에 삭제 버튼을 추가합니다.\n" +
                       "- 빨간색 배경의 삭제 버튼 생성\n" +
                       "- CommentItem 스크립트에 자동 연결\n" +
                       "- 좌측 스와이프 시 버튼이 나타남", EditorStyles.wordWrappedLabel);
    }

    private void SetupSelectedCommentItem()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에서 CommentItem을 선택하세요.", "확인");
            return;
        }

        CommentItem commentItem = selected.GetComponent<CommentItem>();
        if (commentItem == null)
        {
            EditorUtility.DisplayDialog("오류", "선택된 오브젝트에 CommentItem 컴포넌트가 없습니다.", "확인");
            return;
        }

        SetupDeleteButton(selected, commentItem);
        EditorUtility.DisplayDialog("완료", "삭제 버튼이 설정되었습니다.", "확인");
    }

    private void SetupCommentItemPrefab()
    {
        // 프리팹 경로로 찾기
        string[] guids = AssetDatabase.FindAssets("CommentItem_Template t:Prefab");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("오류", "CommentItem_Template 프리팹을 찾을 수 없습니다.", "확인");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            EditorUtility.DisplayDialog("오류", "프리팹을 로드할 수 없습니다.", "확인");
            return;
        }

        // 프리팹 편집 모드로 열기
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        CommentItem commentItem = prefabRoot.GetComponent<CommentItem>();
        if (commentItem == null)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            EditorUtility.DisplayDialog("오류", "프리팹에 CommentItem 컴포넌트가 없습니다.", "확인");
            return;
        }

        SetupDeleteButton(prefabRoot, commentItem);

        // 프리팹 저장
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        EditorUtility.DisplayDialog("완료", $"프리팹이 업데이트되었습니다.\n{prefabPath}", "확인");
    }

    private void SetupDeleteButton(GameObject commentItemObj, CommentItem commentItem)
    {
        // 이미 DeleteButton이 있는지 확인
        Transform existingDelete = commentItemObj.transform.Find("DeleteButton");
        if (existingDelete != null)
        {
            // 기존 버튼 업데이트
            commentItem.deleteButton = existingDelete.gameObject;
            Text existingText = existingDelete.GetComponentInChildren<Text>();
            if (existingText != null)
                commentItem.deleteText = existingText;
            Debug.Log("[CommentItemDeleteSetup] 기존 DeleteButton 연결됨");
            EditorUtility.SetDirty(commentItem);
            return;
        }

        // RectTransform 가져오기
        RectTransform commentRect = commentItemObj.GetComponent<RectTransform>();
        if (commentRect == null)
        {
            Debug.LogError("CommentItem에 RectTransform이 없습니다.");
            return;
        }

        // DeleteButton 생성 - CommentItem의 부모에 배치 (CommentItem 뒤에 보이도록)
        GameObject deleteButtonObj = new GameObject("DeleteButton");
        deleteButtonObj.transform.SetParent(commentItemObj.transform.parent, false);

        // CommentItem 바로 앞에 배치 (sibling index)
        int commentIndex = commentItemObj.transform.GetSiblingIndex();
        deleteButtonObj.transform.SetSiblingIndex(commentIndex);

        // 또는 CommentItem의 자식으로 배치하되 오른쪽 끝에 고정
        // 이 방식이 더 간단할 수 있음
        deleteButtonObj.transform.SetParent(commentItemObj.transform, false);

        RectTransform deleteRect = deleteButtonObj.AddComponent<RectTransform>();

        // 오른쪽 끝에 고정, CommentItem 밖으로 배치
        deleteRect.anchorMin = new Vector2(1, 0);
        deleteRect.anchorMax = new Vector2(1, 1);
        deleteRect.pivot = new Vector2(0, 0.5f);
        deleteRect.anchoredPosition = new Vector2(0, 0);
        deleteRect.sizeDelta = new Vector2(100, 0); // 너비 100, 높이는 stretch

        // Image (빨간 배경)
        Image deleteImage = deleteButtonObj.AddComponent<Image>();
        deleteImage.color = new Color(0.9f, 0.2f, 0.2f, 1f); // 빨간색

        // Button
        Button deleteButton = deleteButtonObj.AddComponent<Button>();
        deleteButton.targetGraphic = deleteImage;

        // 클릭 이벤트 연결
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            deleteButton.onClick,
            commentItem.OnDeleteClicked
        );

        // Text 생성
        GameObject textObj = new GameObject("DeleteText");
        textObj.transform.SetParent(deleteButtonObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text deleteText = textObj.AddComponent<Text>();
        deleteText.text = "삭제";
        deleteText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        deleteText.fontSize = 16;
        deleteText.fontStyle = FontStyle.Bold;
        deleteText.color = Color.white;
        deleteText.alignment = TextAnchor.MiddleCenter;

        // 기본 비활성화
        deleteButtonObj.SetActive(false);

        // CommentItem에 연결
        commentItem.deleteButton = deleteButtonObj;
        commentItem.deleteText = deleteText;

        EditorUtility.SetDirty(commentItem);
        Debug.Log("[CommentItemDeleteSetup] DeleteButton 생성 및 연결 완료");
    }
}
