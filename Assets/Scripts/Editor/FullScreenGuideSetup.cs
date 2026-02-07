using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;

/// <summary>
/// FullScreenGuide에 CreatedByText UI 자동 설정
/// 씬 로드 시 자동으로 CreatedByText 생성 및 DoubleTap3D에 연결
/// </summary>
[InitializeOnLoad]
public class FullScreenGuideSetup
{
    // 씬 로드 시 자동 설정
    static FullScreenGuideSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndSetupUI;
    }

    // 스크립트 컴파일 완료 시 자동 실행
    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckAndSetupUISilent;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CheckAndSetupUISilent;
    }

    private static void CheckAndSetupUI()
    {
        CheckAndSetupUISilent();
    }

    /// <summary>
    /// 씬 로드 시 자동으로 UI 체크 및 생성
    /// </summary>
    private static void CheckAndSetupUISilent()
    {
        // Play Mode가 아닐 때만 실행
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;

        // FullScreenGuide 찾기
        GameObject guidePanel = FindFullScreenGuide();
        if (guidePanel == null) return;

        // CreatedByText 체크 및 생성
        CheckAndCreateCreatedByText(guidePanel);
    }

    /// <summary>
    /// FullScreenGuide 찾기
    /// </summary>
    private static GameObject FindFullScreenGuide()
    {
        GameObject guidePanel = GameObject.Find("FullScreenGuide");
        if (guidePanel != null) return guidePanel;

        // Canvas 하위에서 찾기
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            Transform found = canvas.transform.Find("FullScreenGuide");
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    /// <summary>
    /// CreatedByText 체크 및 자동 생성
    /// </summary>
    private static void CheckAndCreateCreatedByText(GameObject guidePanel)
    {
        // 이미 존재하는지 확인
        Transform existing = guidePanel.transform.Find("CreatedByText");
        Text createdByText = null;

        if (existing != null)
        {
            createdByText = existing.GetComponent<Text>();
        }
        else
        {
            // 새로 생성
            Debug.Log("[FullScreenGuideSetup] CreatedByText 자동 생성 중...");
            createdByText = CreateCreatedByText(guidePanel.transform);
        }

        if (createdByText == null) return;

        // DoubleTap3D에 연결
        ConnectToDoubleTap3D(createdByText);
    }

    /// <summary>
    /// CreatedByText UI 생성
    /// </summary>
    private static Text CreateCreatedByText(Transform parent)
    {
        GameObject textObj = new GameObject("CreatedByText");
        textObj.transform.SetParent(parent, false);

        // Text 컴포넌트
        Text text = textObj.AddComponent<Text>();
        text.text = "Created by Username";
        text.fontSize = 18;
        text.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        text.alignment = TextAnchor.MiddleLeft;

        // 폰트 로드
        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (customFont != null)
            text.font = customFont;
        else
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Shadow 효과
        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(1, -1);

        // RectTransform 설정 (하단 왼쪽)
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0, 0);
        rect.sizeDelta = new Vector2(0, 30);
        rect.anchoredPosition = new Vector2(20, 190); // CommentPreviewPanel 위

        // 초기에 비활성화
        textObj.SetActive(false);

        // 씬 변경 표시
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[FullScreenGuideSetup] CreatedByText 생성 완료");
        return text;
    }

    /// <summary>
    /// DoubleTap3D에 CreatedByText 연결
    /// </summary>
    private static void ConnectToDoubleTap3D(Text createdByText)
    {
        DoubleTap3D[] doubleTaps = Object.FindObjectsByType<DoubleTap3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int connectedCount = 0;

        foreach (var dt in doubleTaps)
        {
            if (dt.createdByText == null)
            {
                dt.createdByText = createdByText;
                EditorUtility.SetDirty(dt);
                connectedCount++;
            }
        }

        if (connectedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[FullScreenGuideSetup] CreatedByText가 {connectedCount}개의 DoubleTap3D에 연결됨");
        }
    }
}
