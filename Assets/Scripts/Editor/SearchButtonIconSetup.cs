using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// SearchButton 내부에 돋보기/X 아이콘을 자동 생성하고
/// MessagePanelManager의 searchOpenIcon/searchCloseIcon 필드에 연결
/// </summary>
[InitializeOnLoad]
public class SearchButtonIconSetup
{
    static SearchButtonIconSetup()
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
        var mgr = Object.FindFirstObjectByType<MessagePanelManager>();
        if (mgr == null) return;

        // 이미 둘 다 연결되어 있으면 스킵
        if (mgr.searchOpenIcon != null && mgr.searchCloseIcon != null) return;

        // SearchButton 찾기
        if (mgr.searchButton == null)
        {
            Debug.LogWarning("[SearchButtonIconSetup] searchButton이 연결되어 있지 않습니다.");
            return;
        }

        Transform btnTransform = mgr.searchButton.transform;

        // 기존 "Text" 자식 비활성화 (삭제 대신 비활성화로 안전하게)
        Transform oldText = btnTransform.Find("Text");
        if (oldText != null && oldText.gameObject.activeSelf)
        {
            oldText.gameObject.SetActive(false);
            EditorUtility.SetDirty(oldText.gameObject);
        }

        // ============================================================
        // SearchOpenIcon (돋보기) 생성/찾기
        // ============================================================
        GameObject openIcon = FindOrCreateChild(btnTransform, "SearchOpenIcon");
        if (mgr.searchOpenIcon == null)
        {
            SetupSearchIcon(openIcon);
            mgr.searchOpenIcon = openIcon;
            openIcon.SetActive(true);
        }

        // ============================================================
        // SearchCloseIcon (X) 생성/찾기
        // ============================================================
        GameObject closeIcon = FindOrCreateChild(btnTransform, "SearchCloseIcon");
        if (mgr.searchCloseIcon == null)
        {
            SetupCloseIcon(closeIcon);
            mgr.searchCloseIcon = closeIcon;
            closeIcon.SetActive(false);
        }

        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(80f, 80f);

        return obj;
    }

    /// <summary>
    /// 돋보기 아이콘: 원 + 대각선 손잡이 (UI Image로 구성)
    /// </summary>
    private static void SetupSearchIcon(GameObject parent)
    {
        RectTransform parentRect = parent.GetComponent<RectTransform>();
        if (parentRect == null) parentRect = parent.AddComponent<RectTransform>();

        // 기존 자식이 있으면 이미 세팅됨
        if (parent.transform.childCount > 0) return;

        Color iconColor = new Color(1f, 1f, 1f, 0.9f);

        // --- 돋보기 원 (Ring) ---
        // 외곽 원
        GameObject outerCircle = new GameObject("OuterCircle");
        outerCircle.transform.SetParent(parent.transform, false);
        RectTransform outerRect = outerCircle.AddComponent<RectTransform>();
        outerRect.anchorMin = new Vector2(0.5f, 0.5f);
        outerRect.anchorMax = new Vector2(0.5f, 0.5f);
        outerRect.anchoredPosition = new Vector2(-5f, 5f);
        outerRect.sizeDelta = new Vector2(48f, 48f);
        Image outerImg = outerCircle.AddComponent<Image>();
        outerImg.color = iconColor;
        outerImg.raycastTarget = false;
        // 원형 마스크 효과를 위해 Outline 사용하지 않고 단순 원
        // Unity 기본 Sprite 없으므로 Knob 사용
        outerImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        outerImg.type = Image.Type.Simple;

        // 내부 원 (배경색으로 채워서 링 모양 만들기)
        GameObject innerCircle = new GameObject("InnerCircle");
        innerCircle.transform.SetParent(outerCircle.transform, false);
        RectTransform innerRect = innerCircle.AddComponent<RectTransform>();
        innerRect.anchorMin = new Vector2(0.5f, 0.5f);
        innerRect.anchorMax = new Vector2(0.5f, 0.5f);
        innerRect.anchoredPosition = Vector2.zero;
        innerRect.sizeDelta = new Vector2(34f, 34f);
        Image innerImg = innerCircle.AddComponent<Image>();
        innerImg.raycastTarget = false;
        innerImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        innerImg.type = Image.Type.Simple;

        // 부모 Button의 Image 색상 가져오기 (배경색 맞추기)
        Image parentBtnImg = parent.GetComponentInParent<Image>();
        if (parentBtnImg != null)
            innerImg.color = parentBtnImg.color;
        else
            innerImg.color = new Color(0.13f, 0.13f, 0.16f, 1f);

        // --- 손잡이 (Handle) ---
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(parent.transform, false);
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = new Vector2(14f, -14f);
        handleRect.sizeDelta = new Vector2(8f, 24f);
        handleRect.localEulerAngles = new Vector3(0, 0, -45f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = iconColor;
        handleImg.raycastTarget = false;

        // 손잡이 끝 라운드
        GameObject handleCap = new GameObject("HandleCap");
        handleCap.transform.SetParent(handle.transform, false);
        RectTransform capRect = handleCap.AddComponent<RectTransform>();
        capRect.anchorMin = new Vector2(0.5f, 0f);
        capRect.anchorMax = new Vector2(0.5f, 0f);
        capRect.anchoredPosition = new Vector2(0f, -2f);
        capRect.sizeDelta = new Vector2(8f, 8f);
        Image capImg = handleCap.AddComponent<Image>();
        capImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        capImg.color = iconColor;
        capImg.raycastTarget = false;
    }

    /// <summary>
    /// X 아이콘: 두 개의 대각선 바 (UI Image로 구성)
    /// </summary>
    private static void SetupCloseIcon(GameObject parent)
    {
        RectTransform parentRect = parent.GetComponent<RectTransform>();
        if (parentRect == null) parentRect = parent.AddComponent<RectTransform>();

        // 기존 자식이 있으면 이미 세팅됨
        if (parent.transform.childCount > 0) return;

        Color iconColor = new Color(1f, 1f, 1f, 0.9f);

        // 대각선 바 1 (\)
        GameObject bar1 = new GameObject("Bar1");
        bar1.transform.SetParent(parent.transform, false);
        RectTransform bar1Rect = bar1.AddComponent<RectTransform>();
        bar1Rect.anchorMin = new Vector2(0.5f, 0.5f);
        bar1Rect.anchorMax = new Vector2(0.5f, 0.5f);
        bar1Rect.anchoredPosition = Vector2.zero;
        bar1Rect.sizeDelta = new Vector2(8f, 48f);
        bar1Rect.localEulerAngles = new Vector3(0, 0, 45f);
        Image bar1Img = bar1.AddComponent<Image>();
        bar1Img.color = iconColor;
        bar1Img.raycastTarget = false;

        // 대각선 바 2 (/)
        GameObject bar2 = new GameObject("Bar2");
        bar2.transform.SetParent(parent.transform, false);
        RectTransform bar2Rect = bar2.AddComponent<RectTransform>();
        bar2Rect.anchorMin = new Vector2(0.5f, 0.5f);
        bar2Rect.anchorMax = new Vector2(0.5f, 0.5f);
        bar2Rect.anchoredPosition = Vector2.zero;
        bar2Rect.sizeDelta = new Vector2(8f, 48f);
        bar2Rect.localEulerAngles = new Vector3(0, 0, -45f);
        Image bar2Img = bar2.AddComponent<Image>();
        bar2Img.color = iconColor;
        bar2Img.raycastTarget = false;
    }
}
