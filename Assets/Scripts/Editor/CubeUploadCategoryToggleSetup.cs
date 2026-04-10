using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// CubeUploadManager의 업로드 페이지에 CategoryToggle UI 오브젝트를 자동 생성/연결
/// PetFriendlyToggle 위에 동일한 레이아웃으로 배치
/// </summary>
[InitializeOnLoad]
public class CubeUploadCategoryToggleSetup
{
    // PetFriendlyToggle과 SeparateRestroomsToggle 사이 간격
    private const float TOGGLE_SPACING = 73f;

    static CubeUploadCategoryToggleSetup()
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
        CubeUploadManager mgr = Object.FindFirstObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
        if (mgr == null) return;

        SerializedObject so = new SerializedObject(mgr);

        // PlusButton 자동 연결
        ConnectPlusButton(so, mgr);

        SerializedProperty categoryToggleProp = so.FindProperty("categoryToggle");
        SerializedProperty categoryToggleLabelProp = so.FindProperty("categoryToggleLabel");
        if (categoryToggleProp == null) return;

        // 이미 연결되어 있으면 스킵
        if (categoryToggleProp.objectReferenceValue != null)
        {
            // Label만 누락된 경우 연결
            if (categoryToggleLabelProp != null && categoryToggleLabelProp.objectReferenceValue == null)
            {
                Toggle existingToggle = categoryToggleProp.objectReferenceValue as Toggle;
                if (existingToggle != null)
                {
                    Text label = existingToggle.GetComponentInChildren<Text>(true);
                    if (label != null)
                    {
                        categoryToggleLabelProp.objectReferenceValue = label;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(mgr);
                    }
                }
            }
            return;
        }

        // PetFriendlyToggle 찾기 (위치 참조용)
        SerializedProperty petToggleProp = so.FindProperty("petFriendlyToggle");
        if (petToggleProp == null || petToggleProp.objectReferenceValue == null) return;

        Toggle petToggle = petToggleProp.objectReferenceValue as Toggle;
        if (petToggle == null) return;

        Transform parentPanel = petToggle.transform.parent;
        if (parentPanel == null) return;

        // 이미 CategoryToggle이 존재하는지 확인
        Transform existingCT = parentPanel.Find("CategoryToggle");
        if (existingCT != null)
        {
            Toggle toggle = existingCT.GetComponent<Toggle>();
            if (toggle != null)
            {
                categoryToggleProp.objectReferenceValue = toggle;
                Text label = existingCT.GetComponentInChildren<Text>(true);
                if (categoryToggleLabelProp != null && label != null)
                    categoryToggleLabelProp.objectReferenceValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mgr);
                EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
            }
            return;
        }

        // PetFriendlyToggle의 위치 정보 가져오기
        RectTransform petRect = petToggle.GetComponent<RectTransform>();
        Vector2 petPosition = petRect.anchoredPosition;
        Vector3 petScale = petRect.localScale;

        // PetFriendlyToggle 및 아래 형제들을 아래로 이동
        int petSiblingIndex = petToggle.transform.GetSiblingIndex();
        ShiftSiblingsDown(parentPanel, petSiblingIndex, TOGGLE_SPACING);

        // CategoryToggle 생성
        Toggle categoryToggle = CreateCategoryToggle(parentPanel, petPosition, petScale, petToggle);
        if (categoryToggle == null) return;

        // PetFriendlyToggle 바로 위에 배치 (sibling 순서)
        categoryToggle.transform.SetSiblingIndex(petSiblingIndex);

        // CubeUploadManager에 연결
        categoryToggleProp.objectReferenceValue = categoryToggle;
        Text categoryLabel = categoryToggle.GetComponentInChildren<Text>(true);
        if (categoryToggleLabelProp != null && categoryLabel != null)
            categoryToggleLabelProp.objectReferenceValue = categoryLabel;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
    }

    /// <summary>
    /// 지정된 sibling index부터 아래의 모든 형제를 Y축 아래로 이동
    /// </summary>
    private static void ShiftSiblingsDown(Transform parent, int fromIndex, float shiftAmount)
    {
        for (int i = fromIndex; i < parent.childCount; i++)
        {
            RectTransform childRect = parent.GetChild(i).GetComponent<RectTransform>();
            if (childRect != null)
            {
                Vector2 pos = childRect.anchoredPosition;
                pos.y -= shiftAmount;
                childRect.anchoredPosition = pos;
                EditorUtility.SetDirty(childRect);
            }
        }
    }

    /// <summary>
    /// PetFriendlyToggle과 동일한 구조의 CategoryToggle 생성
    /// </summary>
    private static Toggle CreateCategoryToggle(Transform parent, Vector2 position, Vector3 scale, Toggle referenceToggle)
    {
        Font customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/AppleSDGothicNeoM.ttf");

        // 기존 토글에서 스프라이트 참조 가져오기
        Sprite bgSprite = null;
        Sprite checkmarkSprite = null;
        Transform refBg = referenceToggle.transform.Find("Background");
        if (refBg != null)
        {
            Image refBgImg = refBg.GetComponent<Image>();
            if (refBgImg != null) bgSprite = refBgImg.sprite;

            Transform refCheck = refBg.Find("Checkmark");
            if (refCheck != null)
            {
                Image refCheckImg = refCheck.GetComponent<Image>();
                if (refCheckImg != null) checkmarkSprite = refCheckImg.sprite;
            }
        }

        // 기존 토글의 Background/Label RectTransform 참조
        RectTransform refBgRect = refBg != null ? refBg.GetComponent<RectTransform>() : null;
        RectTransform refLabelRect = null;
        Transform refLabel = referenceToggle.transform.Find("Label");
        if (refLabel != null) refLabelRect = refLabel.GetComponent<RectTransform>();

        // ============================================================
        // Root: CategoryToggle
        // ============================================================
        GameObject toggleGO = new GameObject("CategoryToggle");
        toggleGO.layer = 5; // UI layer
        toggleGO.transform.SetParent(parent, false);

        RectTransform toggleRect = toggleGO.AddComponent<RectTransform>();
        toggleRect.localScale = scale;
        toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
        toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRect.pivot = new Vector2(0.5f, 0.5f);
        toggleRect.anchoredPosition = position; // PetFriendlyToggle의 원래 위치
        toggleRect.sizeDelta = Vector2.zero;

        Toggle toggle = toggleGO.AddComponent<Toggle>();

        // ============================================================
        // Child 1: Background (기존 PetFriendlyToggle의 Background 구조 복제)
        // ============================================================
        GameObject bgGO = new GameObject("Background");
        bgGO.layer = 5;
        bgGO.transform.SetParent(toggleGO.transform, false);

        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        if (refBgRect != null)
        {
            bgRect.anchorMin = refBgRect.anchorMin;
            bgRect.anchorMax = refBgRect.anchorMax;
            bgRect.pivot = refBgRect.pivot;
            bgRect.anchoredPosition = refBgRect.anchoredPosition;
            bgRect.sizeDelta = refBgRect.sizeDelta;
        }
        else
        {
            bgRect.anchorMin = new Vector2(0, 1);
            bgRect.anchorMax = new Vector2(0, 1);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(10, -10);
            bgRect.sizeDelta = new Vector2(20, 20);
        }

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
        // 기존 Checkmark 참조
        RectTransform refCheckRect = null;
        if (refBg != null)
        {
            Transform refCheckTf = refBg.Find("Checkmark");
            if (refCheckTf != null) refCheckRect = refCheckTf.GetComponent<RectTransform>();
        }
        if (refCheckRect != null)
        {
            checkRect.anchorMin = refCheckRect.anchorMin;
            checkRect.anchorMax = refCheckRect.anchorMax;
            checkRect.pivot = refCheckRect.pivot;
            checkRect.anchoredPosition = refCheckRect.anchoredPosition;
            checkRect.sizeDelta = refCheckRect.sizeDelta;
        }
        else
        {
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = Vector2.zero;
            checkRect.sizeDelta = new Vector2(20, 20);
        }

        checkGO.AddComponent<CanvasRenderer>();
        Image checkImage = checkGO.AddComponent<Image>();
        checkImage.color = Color.white; // 초기 색상 (런타임에 카테고리 색상으로 변경)
        checkImage.raycastTarget = true;
        if (checkmarkSprite != null)
        {
            checkImage.sprite = checkmarkSprite;
            checkImage.type = Image.Type.Simple;
        }

        // ============================================================
        // Child 2: Label (기존 PetFriendlyToggle의 Label 구조 복제)
        // ============================================================
        GameObject labelGO = new GameObject("Label");
        labelGO.layer = 5;
        labelGO.transform.SetParent(toggleGO.transform, false);

        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        if (refLabelRect != null)
        {
            labelRect.anchorMin = refLabelRect.anchorMin;
            labelRect.anchorMax = refLabelRect.anchorMax;
            labelRect.pivot = refLabelRect.pivot;
            labelRect.anchoredPosition = refLabelRect.anchoredPosition;
            labelRect.sizeDelta = refLabelRect.sizeDelta;
        }
        else
        {
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(9, -0.5f);
            labelRect.sizeDelta = new Vector2(-28, -3);
        }

        labelGO.AddComponent<CanvasRenderer>();
        Text labelText = labelGO.AddComponent<Text>();
        labelText.text = GetCategoryLabel();
        labelText.color = Color.white; // 흰색 글자 (런타임에 카테고리 색상으로 변경)
        labelText.raycastTarget = true; // 글자 터치로도 토글 동작하게
        labelText.alignment = TextAnchor.MiddleLeft;

        // 기존 라벨에서 폰트 설정 복사
        if (refLabel != null)
        {
            Text refText = refLabel.GetComponent<Text>();
            if (refText != null)
            {
                labelText.fontSize = refText.fontSize;
                labelText.resizeTextForBestFit = refText.resizeTextForBestFit;
                labelText.resizeTextMinSize = refText.resizeTextMinSize;
                labelText.resizeTextMaxSize = refText.resizeTextMaxSize;
                if (refText.font != null) customFont = refText.font;
            }
        }
        else
        {
            labelText.fontSize = 14;
        }

        if (customFont != null)
        {
            labelText.font = customFont;
        }

        // ============================================================
        // Toggle 컴포넌트 설정
        // ============================================================
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = false; // 초기 상태: 카테고리 미선택
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.toggleTransition = Toggle.ToggleTransition.Fade;

        // 기존 토글에 ToggleSounds가 있으면 동일하게 추가
        var refSound = referenceToggle.GetComponent("ToggleSounds");
        if (refSound != null)
        {
            var soundType = refSound.GetType();
            toggleGO.AddComponent(soundType);
        }

        return toggle;
    }

    /// <summary>
    /// PlusButton 자동 연결 (씬에서 "PlusButton" 이름으로 찾아 연결)
    /// </summary>
    private static void ConnectPlusButton(SerializedObject so, CubeUploadManager mgr)
    {
        SerializedProperty plusButtonProp = so.FindProperty("plusButton");
        if (plusButtonProp == null) return;
        if (plusButtonProp.objectReferenceValue != null) return;

        // 씬에서 PlusButton 찾기 (비활성 포함)
        foreach (GameObject root in mgr.gameObject.scene.GetRootGameObjects())
        {
            Transform found = FindChildRecursive(root.transform, "PlusButton");
            if (found != null)
            {
                plusButtonProp.objectReferenceValue = found.gameObject;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mgr);
                EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
                return;
            }
        }
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    private static string GetCategoryLabel()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: return "분류 선택";
            case SystemLanguage.Japanese: return "分類選択";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return "选择分类";
            case SystemLanguage.Spanish: return "Elegir categoría";
            default: return "Select Category";
        }
    }
}
