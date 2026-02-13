using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;
using TMPro;

/// <summary>
/// DataManager AR 가이드 UI 자동 생성
/// Geospatial 추적 준비 전 안내 패널
/// 씬 로드 / 스크립트 컴파일 시 자동 실행
/// </summary>
[InitializeOnLoad]
public class DataManagerARGuideSetup
{
    private const string GUIDE_PANEL_NAME = "ARGuidePanel";

    static DataManagerARGuideSetup()
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
        if (Application.isPlaying) return;

        DataManager dataManager = Object.FindFirstObjectByType<DataManager>();
        if (dataManager == null) return;

        // 이미 연결되어 있으면 스킵
        if (dataManager.arGuidePanel != null) return;

        // Canvas 찾기
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 기존 패널 찾기
        Transform existing = canvas.transform.Find(GUIDE_PANEL_NAME);
        if (existing != null)
        {
            ConnectToDataManager(dataManager, existing.gameObject);
            return;
        }

        // 새로 생성
        CreateARGuidePanel(dataManager, canvas);
    }

    private static void CreateARGuidePanel(DataManager dataManager, Canvas canvas)
    {
        // 폰트 로드
        TMP_FontAsset tmpFont = null;
        string[] fontGuids = AssetDatabase.FindAssets("AppleSDGothicNeoM t:TMP_FontAsset");
        if (fontGuids.Length > 0)
        {
            string fontPath = AssetDatabase.GUIDToAssetPath(fontGuids[0]);
            tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        }

        // ============================================================
        // ARGuidePanel (전체 화면 반투명 오버레이)
        // ============================================================
        GameObject panelObj = new GameObject(GUIDE_PANEL_NAME);
        panelObj.transform.SetParent(canvas.transform, false);
        panelObj.transform.SetAsLastSibling();

        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.6f);
        panelBg.raycastTarget = true;

        CanvasGroup canvasGroup = panelObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // ============================================================
        // 중앙 컨테이너 (텍스트 그룹)
        // ============================================================
        GameObject container = new GameObject("GuideContainer");
        container.transform.SetParent(panelObj.transform, false);

        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.1f, 0.35f);
        containerRect.anchorMax = new Vector2(0.9f, 0.65f);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        // ============================================================
        // 아이콘 (위치 핀 심볼 - TextMeshPro로)
        // ============================================================
        GameObject iconObj = new GameObject("GuideIcon");
        iconObj.transform.SetParent(container.transform, false);

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.65f);
        iconRect.anchorMax = new Vector2(0.5f, 0.65f);
        iconRect.sizeDelta = new Vector2(60f, 60f);
        iconRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI iconText = iconObj.AddComponent<TextMeshProUGUI>();
        iconText.text = "\u25CE"; // ◎ 위치 심볼
        iconText.fontSize = 48;
        iconText.alignment = TextAlignmentOptions.Center;
        iconText.color = new Color(1f, 1f, 1f, 0.8f);
        if (tmpFont != null) iconText.font = tmpFont;

        // ============================================================
        // 안내 텍스트 (중앙)
        // ============================================================
        GameObject textObj = new GameObject("GuideText");
        textObj.transform.SetParent(container.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.15f);
        textRect.anchorMax = new Vector2(1f, 0.55f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI guideText = textObj.AddComponent<TextMeshProUGUI>();
        guideText.text = "주변을 천천히 둘러보세요\n위치를 파악하고 있습니다...";
        guideText.fontSize = 22;
        guideText.alignment = TextAlignmentOptions.Center;
        guideText.color = Color.white;
        guideText.lineSpacing = 8f;
        if (tmpFont != null) guideText.font = tmpFont;

        // ============================================================
        // 하단 보조 텍스트
        // ============================================================
        GameObject subTextObj = new GameObject("GuideSubText");
        subTextObj.transform.SetParent(container.transform, false);

        RectTransform subTextRect = subTextObj.AddComponent<RectTransform>();
        subTextRect.anchorMin = new Vector2(0.1f, 0f);
        subTextRect.anchorMax = new Vector2(0.9f, 0.15f);
        subTextRect.offsetMin = Vector2.zero;
        subTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI subText = subTextObj.AddComponent<TextMeshProUGUI>();
        subText.text = "GPS 신호를 수신 중입니다";
        subText.fontSize = 14;
        subText.alignment = TextAlignmentOptions.Center;
        subText.color = new Color(1f, 1f, 1f, 0.5f);
        if (tmpFont != null) subText.font = tmpFont;

        // 기본 비활성 상태
        panelObj.SetActive(false);

        // DataManager에 연결
        ConnectToDataManager(dataManager, panelObj);

        // 씬 더티 마킹
        EditorUtility.SetDirty(dataManager);
        EditorSceneManager.MarkSceneDirty(dataManager.gameObject.scene);
    }

    private static void ConnectToDataManager(DataManager dataManager, GameObject panel)
    {
        dataManager.arGuidePanel = panel;

        // CanvasGroup 연결
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg != null)
            dataManager.arGuideCanvasGroup = cg;

        // GuideText 찾아서 연결
        Transform textTransform = panel.transform.Find("GuideContainer/GuideText");
        if (textTransform != null)
        {
            TextMeshProUGUI guideText = textTransform.GetComponent<TextMeshProUGUI>();
            if (guideText != null)
                dataManager.arGuideText = guideText;
        }

        EditorUtility.SetDirty(dataManager);
        EditorSceneManager.MarkSceneDirty(dataManager.gameObject.scene);
    }
}
