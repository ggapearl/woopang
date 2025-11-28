using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// FilterButtonPanel 프리팹을 완전히 새로 생성하는 에디터 스크립트
/// Unity 메뉴: Tools > Woopang > Create Filter Panel Prefab (Complete)
/// </summary>
public class CreateFilterPanelPrefab
{
    [MenuItem("Tools/Woopang/Create Filter Panel Prefab (Complete)")]
    public static void CreateCompletePrefab()
    {
        // 1. 루트 GameObject 생성
        GameObject panel = new GameObject("FilterButtonPanel");

        // 2. RectTransform 설정
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        panelRect.sizeDelta = new Vector2(250, 600);

        // 3. Image 컴포넌트 (배경)
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);

        // 4. VerticalLayoutGroup 추가
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 5;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // ContentSizeFitter 추가 (자동 크기 조정)
        ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 5. FilterManager 컴포넌트 추가
        FilterManager filterManager = panel.AddComponent<FilterManager>();

        // 6. 토글 6개 생성
        Toggle petToggle = CreateToggle(panel.transform, "PetFriendlyToggle", "애견동반");
        Toggle publicToggle = CreateToggle(panel.transform, "PublicDataToggle", "공공데이터");
        Toggle subwayToggle = CreateToggle(panel.transform, "SubwayToggle", "지하철");
        Toggle busToggle = CreateToggle(panel.transform, "BusToggle", "버스");
        Toggle alcoholToggle = CreateToggle(panel.transform, "AlcoholToggle", "주류판매");
        Toggle woopangToggle = CreateToggle(panel.transform, "WoopangDataToggle", "우팡데이터");

        // 7. 버튼 2개 생성
        Button selectAllBtn = CreateButton(panel.transform, "SelectAllButton", "전체 선택", new Color(0.2f, 0.6f, 1f, 0.8f));
        Button deselectAllBtn = CreateButton(panel.transform, "DeselectAllButton", "전체 해제", new Color(0.78f, 0.78f, 0.78f, 0.8f));

        // 8. FilterManager에 연결 (SerializedObject 사용)
        SerializedObject serializedManager = new SerializedObject(filterManager);
        serializedManager.FindProperty("petFriendlyToggle").objectReferenceValue = petToggle;
        serializedManager.FindProperty("publicDataToggle").objectReferenceValue = publicToggle;
        serializedManager.FindProperty("subwayToggle").objectReferenceValue = subwayToggle;
        serializedManager.FindProperty("busToggle").objectReferenceValue = busToggle;
        serializedManager.FindProperty("alcoholToggle").objectReferenceValue = alcoholToggle;
        serializedManager.FindProperty("woopangDataToggle").objectReferenceValue = woopangToggle;
        serializedManager.FindProperty("selectAllButton").objectReferenceValue = selectAllBtn;
        serializedManager.FindProperty("deselectAllButton").objectReferenceValue = deselectAllBtn;
        serializedManager.ApplyModifiedProperties();

        // 9. 프리팹으로 저장
        string prefabPath = "Assets/Prefabs/FilterButtonPanel.prefab";

        // 기존 프리팹 백업
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            string backupPath = "Assets/Prefabs/FilterButtonPanel.prefab.backup_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            AssetDatabase.CopyAsset(prefabPath, backupPath);
            AssetDatabase.DeleteAsset(prefabPath);
            Debug.Log($"[CreateFilterPanel] 기존 프리팹 백업: {backupPath}");
        }

        PrefabUtility.SaveAsPrefabAsset(panel, prefabPath);

        // 10. 생성한 GameObject 삭제 (프리팹만 남김)
        Object.DestroyImmediate(panel);

        Debug.Log($"[CreateFilterPanel] ✅ FilterButtonPanel.prefab 생성 완료!");
        Debug.Log($"  - 토글 6개: 애견동반, 공공데이터, 지하철, 버스, 주류판매, 우팡데이터");
        Debug.Log($"  - 버튼 2개: 전체 선택, 전체 해제");
        Debug.Log($"  - 위치: 왼쪽 상단 (10, -10)");
        Debug.Log($"  - 크기: 250x600, 간격 5px");

        EditorUtility.DisplayDialog(
            "프리팹 생성 완료",
            "FilterButtonPanel.prefab이 완전히 새로 생성되었습니다!\n\n" +
            "- 6개 토글 (60x60)\n" +
            "- 2개 버튼 (250x50)\n" +
            "- FilterManager 연결 완료\n\n" +
            "이제 'Tools > Woopang > Setup Filter Button Panel'을 실행하여\n" +
            "씬에 추가하세요.",
            "확인"
        );

        // 프리팹 선택
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    private static Toggle CreateToggle(Transform parent, string name, string labelText)
    {
        // Toggle GameObject
        GameObject toggleObj = new GameObject(name);
        toggleObj.transform.SetParent(parent);
        toggleObj.layer = 5; // UI layer

        RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0, 0);
        toggleRect.anchorMax = new Vector2(1, 0);
        toggleRect.pivot = new Vector2(0.5f, 0.5f);
        toggleRect.sizeDelta = new Vector2(0, 60); // 높이 60 (2배 증가)

        Toggle toggle = toggleObj.AddComponent<Toggle>();
        toggle.isOn = true;

        // LayoutElement 추가 (높이 명시)
        LayoutElement layoutElement = toggleObj.AddComponent<LayoutElement>();
        layoutElement.minHeight = 60;
        layoutElement.preferredHeight = 60;

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(toggleObj.transform);
        bgObj.layer = 5;

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.5f);
        bgRect.anchorMax = new Vector2(0, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = new Vector2(40, 0);
        bgRect.sizeDelta = new Vector2(60, 60); // 60x60 (2배 증가)

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bgImage.type = Image.Type.Sliced;
        bgImage.color = Color.white;

        // Checkmark
        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(bgObj.transform);
        checkObj.layer = 5;

        RectTransform checkRect = checkObj.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkRect.pivot = new Vector2(0.5f, 0.5f);
        checkRect.anchoredPosition = Vector2.zero;
        checkRect.sizeDelta = new Vector2(50, 50); // 50x50

        Image checkImage = checkObj.AddComponent<Image>();
        checkImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        checkImage.color = new Color(0.196f, 0.196f, 0.196f, 1f);

        // Checkmark 초기 활성화 (체크된 상태로 시작)
        checkObj.SetActive(true);

        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(toggleObj.transform);
        labelObj.layer = 5;

        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(35, 0);
        labelRect.sizeDelta = new Vector2(-110, 0);

        Text labelText_comp = labelObj.AddComponent<Text>();
        labelText_comp.text = labelText;
        labelText_comp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText_comp.fontSize = 18; // 폰트 크기 증가
        labelText_comp.color = Color.white;
        labelText_comp.alignment = TextAnchor.MiddleLeft;

        // Toggle 연결
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;

        return toggle;
    }

    private static Button CreateButton(Transform parent, string name, string labelText, Color color)
    {
        // Button GameObject
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent);
        btnObj.layer = 5;

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0, 0);
        btnRect.anchorMax = new Vector2(1, 0);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(0, 50);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        btnImage.type = Image.Type.Sliced;
        btnImage.color = color;

        Button button = btnObj.AddComponent<Button>();
        button.targetGraphic = btnImage;

        // LayoutElement 추가 (높이 명시)
        LayoutElement layoutElement = btnObj.AddComponent<LayoutElement>();
        layoutElement.minHeight = 50;
        layoutElement.preferredHeight = 50;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform);
        textObj.layer = 5;

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = labelText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        return button;
    }
}
