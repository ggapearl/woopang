using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// FilterButtonPanel에 CategoryToggle UI 오브젝트를 자동 생성/연결
/// 기존 토글(PetFriendlyToggle 등)과 동일한 구조로 최상단에 배치
/// </summary>
[InitializeOnLoad]
public class CategoryToggleSetup
{
    static CategoryToggleSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndSetup;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckAndSetupSilent;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CheckAndSetupSilent;
    }

    private static void CheckAndSetup()
    {
        CheckAndSetupSilent();
    }

    private static void CheckAndSetupSilent()
    {
        FilterManager filterManager = Object.FindFirstObjectByType<FilterManager>();
        if (filterManager == null) return;

        // SerializedObject로 categoryToggle 필드 확인
        SerializedObject so = new SerializedObject(filterManager);
        SerializedProperty categoryToggleProp = so.FindProperty("categoryToggle");
        if (categoryToggleProp == null) return;

        // 이미 연결되어 있으면 스킵
        if (categoryToggleProp.objectReferenceValue != null) return;

        // FilterButtonPanel 찾기 (FilterManager가 붙어있는 오브젝트)
        Transform panelTransform = filterManager.transform;

        // 이미 CategoryToggle이 존재하는지 확인
        Transform existingToggle = panelTransform.Find("CategoryToggle");
        if (existingToggle != null)
        {
            // 존재하면 연결만
            Toggle toggle = existingToggle.GetComponent<Toggle>();
            if (toggle != null)
            {
                categoryToggleProp.objectReferenceValue = toggle;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(filterManager);
                EditorSceneManager.MarkSceneDirty(filterManager.gameObject.scene);
            }
            return;
        }

        // CategoryToggle 생성
        Toggle categoryToggle = CreateCategoryToggle(panelTransform);
        if (categoryToggle == null) return;

        // 최상단에 배치 (PetFriendlyToggle 위)
        categoryToggle.transform.SetAsFirstSibling();

        // FilterManager에 연결
        categoryToggleProp.objectReferenceValue = categoryToggle;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(filterManager);
        EditorSceneManager.MarkSceneDirty(filterManager.gameObject.scene);
    }

    private static Toggle CreateCategoryToggle(Transform parent)
    {
        // 폰트 로드
        Font customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/AppleSDGothicNeoM.ttf");

        // 기존 토글에서 스프라이트 참조 가져오기
        Sprite bgSprite = null;
        Sprite checkmarkSprite = null;
        Toggle existingRef = parent.GetComponentInChildren<Toggle>(true);
        if (existingRef != null)
        {
            Transform existingBg = existingRef.transform.Find("Background");
            if (existingBg != null)
            {
                Image bgImg = existingBg.GetComponent<Image>();
                if (bgImg != null) bgSprite = bgImg.sprite;

                Transform existingCheck = existingBg.Find("Checkmark");
                if (existingCheck != null)
                {
                    Image checkImg = existingCheck.GetComponent<Image>();
                    if (checkImg != null) checkmarkSprite = checkImg.sprite;
                }
            }
        }

        // ============================================================
        // Root: CategoryToggle
        // ============================================================
        GameObject toggleGO = new GameObject("CategoryToggle");
        toggleGO.layer = 5; // UI layer
        toggleGO.transform.SetParent(parent, false);

        RectTransform toggleRect = toggleGO.AddComponent<RectTransform>();
        toggleRect.anchorMin = Vector2.zero;
        toggleRect.anchorMax = Vector2.zero;
        toggleRect.pivot = new Vector2(0.5f, 0.5f);
        toggleRect.anchoredPosition = Vector2.zero;
        toggleRect.sizeDelta = Vector2.zero;

        Toggle toggle = toggleGO.AddComponent<Toggle>();

        // LayoutElement (기존 토글과 동일: minHeight=60, preferredHeight=60)
        LayoutElement layoutElement = toggleGO.AddComponent<LayoutElement>();
        layoutElement.minHeight = 60;
        layoutElement.preferredHeight = 60;
        layoutElement.flexibleHeight = -1;
        layoutElement.layoutPriority = 1;

        // ============================================================
        // Child 1: Background
        // ============================================================
        GameObject bgGO = new GameObject("Background");
        bgGO.layer = 5;
        bgGO.transform.SetParent(toggleGO.transform, false);

        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.5f);
        bgRect.anchorMax = new Vector2(0, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = new Vector2(40, 0);
        bgRect.sizeDelta = new Vector2(60, 60);

        bgGO.AddComponent<CanvasRenderer>();
        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = Color.white;
        bgImage.raycastTarget = true;
        if (bgSprite != null)
        {
            bgImage.sprite = bgSprite;
            bgImage.type = Image.Type.Sliced;
        }

        // ============================================================
        // Child 1-1: Checkmark (under Background)
        // ============================================================
        GameObject checkGO = new GameObject("Checkmark");
        checkGO.layer = 5;
        checkGO.transform.SetParent(bgGO.transform, false);

        RectTransform checkRect = checkGO.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkRect.pivot = new Vector2(0.5f, 0.5f);
        checkRect.anchoredPosition = Vector2.zero;
        checkRect.sizeDelta = new Vector2(50, 50);

        checkGO.AddComponent<CanvasRenderer>();
        Image checkImage = checkGO.AddComponent<Image>();
        checkImage.color = new Color(0.196f, 0.196f, 0.196f, 1f);
        checkImage.raycastTarget = true;
        if (checkmarkSprite != null)
        {
            checkImage.sprite = checkmarkSprite;
            checkImage.type = Image.Type.Simple;
        }

        // ============================================================
        // Child 2: Label
        // ============================================================
        GameObject labelGO = new GameObject("Label");
        labelGO.layer = 5;
        labelGO.transform.SetParent(toggleGO.transform, false);

        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(35, 0);
        labelRect.sizeDelta = new Vector2(-110, 0);

        labelGO.AddComponent<CanvasRenderer>();
        Text labelText = labelGO.AddComponent<Text>();
        labelText.text = GetCategoryLabel();
        labelText.color = Color.white;
        labelText.fontSize = 24;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.raycastTarget = true;
        labelText.resizeTextForBestFit = true;
        labelText.resizeTextMinSize = 16;
        labelText.resizeTextMaxSize = 24;
        if (customFont != null)
        {
            labelText.font = customFont;
        }

        // ============================================================
        // Toggle 컴포넌트 설정
        // ============================================================
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = true;
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.toggleTransition = Toggle.ToggleTransition.Fade;

        return toggle;
    }

    /// <summary>
    /// 에디터 환경에서 기본 카테고리 라벨 결정
    /// </summary>
    private static string GetCategoryLabel()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: return "카테고리";
            case SystemLanguage.Japanese: return "カテゴリ";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return "分类";
            case SystemLanguage.Spanish: return "Categoría";
            default: return "Category";
        }
    }
}
