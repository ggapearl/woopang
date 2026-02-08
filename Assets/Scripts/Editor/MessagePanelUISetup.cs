using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// MessagePanelManager의 빈 UI 필드를 자동으로 생성하고 연결
/// 주의: 자동 생성 기능 비활성화됨 - 기존 UI 오브젝트를 직접 연결하여 사용
/// </summary>
// [InitializeOnLoad]  // 자동 생성 비활성화
public class MessagePanelUISetup
{
    // 자동 생성 기능 비활성화 - 기존 UI를 사용
    /*
    static MessagePanelUISetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        // 씬이 열릴 때 자동 체크
        EditorApplication.delayCall += () =>
        {
            var manager = Object.FindObjectOfType<MessagePanelManager>();
            if (manager != null && HasMissingUI(manager))
            {
                Debug.Log("[MessagePanelUISetup] 빈 UI 필드 감지됨. 자동 설정 실행...");
                SetupMessagePanelUI();
            }
        };
    }
    */

    private static bool HasMissingUI(MessagePanelManager manager)
    {
        return manager.searchResultPanel == null;
    }

    [MenuItem("Tools/Woopang/Setup MessagePanel UI")]
    public static void SetupMessagePanelUI()
    {
        // MessagePanelManager 찾기
        var manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null)
        {
            Debug.LogError("[MessagePanelUISetup] MessagePanelManager를 찾을 수 없습니다.");
            return;
        }

        // MessagePanel 찾기
        GameObject messagePanel = manager.messagePanel;
        if (messagePanel == null)
        {
            Debug.LogError("[MessagePanelUISetup] MessagePanel이 연결되어 있지 않습니다.");
            return;
        }

        Debug.Log("[MessagePanelUISetup] MessagePanel UI 설정 시작...");

        // SearchResultPanel 생성
        if (manager.searchResultPanel == null)
        {
            CreateSearchResultPanel(manager, messagePanel.transform);
        }

        // 씬 저장 표시
        EditorUtility.SetDirty(manager);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[MessagePanelUISetup] MessagePanel UI 설정 완료!");
    }

    /// <summary>
    /// SearchResultPanel 생성
    /// </summary>
    private static void CreateSearchResultPanel(MessagePanelManager manager, Transform parent)
    {
        Debug.Log("[MessagePanelUISetup] SearchResultPanel 생성 중...");

        // SearchResultPanel
        GameObject searchResultPanel = new GameObject("SearchResultPanel");
        searchResultPanel.transform.SetParent(parent, false);
        searchResultPanel.layer = LayerMask.NameToLayer("UI");

        RectTransform panelRect = searchResultPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(1, 0.7f);
        panelRect.offsetMin = new Vector2(20, 200);
        panelRect.offsetMax = new Vector2(-20, -50);

        Image panelBg = searchResultPanel.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.1f, 0.12f, 0.98f);

        // ScrollView
        GameObject scrollView = new GameObject("ScrollView");
        scrollView.transform.SetParent(searchResultPanel.transform, false);
        scrollView.layer = LayerMask.NameToLayer("UI");

        RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;

        ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);
        viewport.layer = LayerMask.NameToLayer("UI");

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        viewport.AddComponent<Image>().color = Color.clear;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject content = new GameObject("SearchResultContent");
        content.transform.SetParent(viewport.transform, false);
        content.layer = LayerMask.NameToLayer("UI");

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = new Vector2(0, 0);
        contentRect.offsetMax = new Vector2(0, 0);
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 5;
        layout.padding = new RectOffset(10, 10, 10, 10);

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;

        // 연결
        manager.searchResultPanel = searchResultPanel;
        manager.searchResultContent = content.transform;

        // 기본 비활성화
        searchResultPanel.SetActive(false);

        Debug.Log("[MessagePanelUISetup] SearchResultPanel 생성 완료");
    }
}
