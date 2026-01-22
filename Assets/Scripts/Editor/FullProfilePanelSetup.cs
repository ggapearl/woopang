#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// FullProfilePanel prefab에 SNS 아이콘 컨테이너를 추가하는 Editor 도구
/// </summary>
[InitializeOnLoad]
public class FullProfilePanelSetup : EditorWindow
{
    static FullProfilePanelSetup()
    {
        // 씬 열릴 때 자동으로 SNS 컨테이너 확인 및 추가
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndAddSnsContainerOnLoad;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CheckAndAddSnsContainerSilent;
    }

    private static void CheckAndAddSnsContainerOnLoad()
    {
        CheckAndAddSnsContainerSilent();
    }

    /// <summary>
    /// SNS 컨테이너가 없으면 자동으로 추가 (조용히)
    /// </summary>
    private static void CheckAndAddSnsContainerSilent()
    {
        // FullProfilePanel 찾기
        GameObject fullProfilePanel = GameObject.Find("FullProfilePanel");
        if (fullProfilePanel == null)
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                Transform found = canvas.transform.Find("FullProfilePanel");
                if (found != null)
                {
                    fullProfilePanel = found.gameObject;
                    break;
                }
            }
        }

        if (fullProfilePanel == null) return;

        Transform content = fullProfilePanel.transform.Find("Content");
        if (content == null) return;

        // 이미 존재하면 스킵
        if (content.Find("SnsIconsContainer") != null) return;

        // SnsIconsContainer 생성
        Debug.Log("[FullProfilePanelSetup] SNS 아이콘 컨테이너 자동 추가 중...");

        GameObject container = CreateSnsIconsContainerStatic(content);
        CreateSnsIconButtonStatic(container.transform, "Instagram", new Color(0.88f, 0.19f, 0.42f));
        CreateSnsIconButtonStatic(container.transform, "X", Color.black);
        CreateSnsIconButtonStatic(container.transform, "Facebook", new Color(0.23f, 0.35f, 0.60f));

        // ProfileManager 연결
        ProfileManager profileManager = Object.FindFirstObjectByType<ProfileManager>();
        if (profileManager != null)
        {
            profileManager.snsIconsContainer = container;
            profileManager.instagramButton = container.transform.Find("InstagramButton")?.GetComponent<Button>();
            profileManager.xButton = container.transform.Find("XButton")?.GetComponent<Button>();
            profileManager.facebookButton = container.transform.Find("FacebookButton")?.GetComponent<Button>();
            EditorUtility.SetDirty(profileManager);
        }

        // 씬 변경 표시
        EditorSceneManager.MarkSceneDirty(fullProfilePanel.scene);
        Debug.Log("[FullProfilePanelSetup] SNS 아이콘 컨테이너 자동 추가 완료!");
    }

    [MenuItem("Tools/Woopang/Setup FullProfilePanel SNS Icons")]
    public static void ShowWindow()
    {
        GetWindow<FullProfilePanelSetup>("FullProfilePanel Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("FullProfilePanel SNS 아이콘 설정", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("SNS 아이콘 컨테이너 추가"))
        {
            AddSnsIconsContainerToPrefab();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("현재 씬의 FullProfilePanel에 추가"))
        {
            AddSnsIconsContainerToScene();
        }

        GUILayout.Space(20);
        GUILayout.Label("사용법:", EditorStyles.boldLabel);
        GUILayout.Label("1. 'SNS 아이콘 컨테이너 추가' 버튼 클릭");
        GUILayout.Label("2. ProfileManager Inspector에서 연결 확인");
        GUILayout.Label("3. 플레이 모드에서 SNS 아이콘 확인");
    }

    private void AddSnsIconsContainerToPrefab()
    {
        // Prefab 로드
        string prefabPath = "Assets/Prefabs/FullProfilePanel.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            EditorUtility.DisplayDialog("오류", $"Prefab을 찾을 수 없습니다: {prefabPath}", "확인");
            return;
        }

        // Prefab 인스턴스 생성
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        try
        {
            // Content 패널 찾기
            Transform content = instance.transform.Find("Content");
            if (content == null)
            {
                EditorUtility.DisplayDialog("오류", "Content 패널을 찾을 수 없습니다.", "확인");
                return;
            }

            // 기존 SnsIconsContainer 확인
            Transform existingContainer = content.Find("SnsIconsContainer");
            if (existingContainer != null)
            {
                EditorUtility.DisplayDialog("알림", "SnsIconsContainer가 이미 존재합니다.", "확인");
                DestroyImmediate(instance);
                return;
            }

            // SnsIconsContainer 생성
            GameObject container = CreateSnsIconsContainer(content);

            // SNS 버튼들 생성
            CreateSnsIconButton(container.transform, "Instagram", new Color(0.88f, 0.19f, 0.42f));
            CreateSnsIconButton(container.transform, "X", Color.black);
            CreateSnsIconButton(container.transform, "Facebook", new Color(0.23f, 0.35f, 0.60f));

            // Prefab에 변경사항 저장
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

            EditorUtility.DisplayDialog("완료", "SNS 아이콘 컨테이너가 추가되었습니다.\n\nProfileManager Inspector에서 연결을 확인하세요.", "확인");
        }
        finally
        {
            DestroyImmediate(instance);
        }
    }

    private void AddSnsIconsContainerToScene()
    {
        // 현재 씬에서 FullProfilePanel 찾기
        GameObject fullProfilePanel = GameObject.Find("FullProfilePanel");
        if (fullProfilePanel == null)
        {
            // Canvas 아래에서 찾기
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                Transform found = canvas.transform.Find("FullProfilePanel");
                if (found != null)
                {
                    fullProfilePanel = found.gameObject;
                    break;
                }
            }
        }

        if (fullProfilePanel == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에서 FullProfilePanel을 찾을 수 없습니다.", "확인");
            return;
        }

        Transform content = fullProfilePanel.transform.Find("Content");
        if (content == null)
        {
            EditorUtility.DisplayDialog("오류", "Content 패널을 찾을 수 없습니다.", "확인");
            return;
        }

        // 기존 컨테이너 확인
        Transform existingContainer = content.Find("SnsIconsContainer");
        if (existingContainer != null)
        {
            EditorUtility.DisplayDialog("알림", "SnsIconsContainer가 이미 존재합니다.", "확인");
            return;
        }

        // SnsIconsContainer 생성
        GameObject container = CreateSnsIconsContainer(content);

        // SNS 버튼들 생성
        CreateSnsIconButton(container.transform, "Instagram", new Color(0.88f, 0.19f, 0.42f));
        CreateSnsIconButton(container.transform, "X", Color.black);
        CreateSnsIconButton(container.transform, "Facebook", new Color(0.23f, 0.35f, 0.60f));

        // ProfileManager 연결 시도
        ProfileManager profileManager = FindFirstObjectByType<ProfileManager>();
        if (profileManager != null)
        {
            profileManager.snsIconsContainer = container;
            profileManager.instagramButton = container.transform.Find("InstagramButton")?.GetComponent<Button>();
            profileManager.xButton = container.transform.Find("XButton")?.GetComponent<Button>();
            profileManager.facebookButton = container.transform.Find("FacebookButton")?.GetComponent<Button>();

            EditorUtility.SetDirty(profileManager);
            Debug.Log("[FullProfilePanelSetup] ProfileManager에 SNS 컴포넌트 연결 완료");
        }

        Undo.RegisterCreatedObjectUndo(container, "Add SNS Icons Container");

        EditorUtility.DisplayDialog("완료", "SNS 아이콘 컨테이너가 씬에 추가되었습니다.", "확인");
    }

    private GameObject CreateSnsIconsContainer(Transform parent)
    {
        return CreateSnsIconsContainerStatic(parent);
    }

    private static GameObject CreateSnsIconsContainerStatic(Transform parent)
    {
        GameObject container = new GameObject("SnsIconsContainer");
        container.transform.SetParent(parent, false);

        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 80f);
        rect.anchoredPosition = new Vector2(0f, -370f); // FollowButton(-300)과 EditProfileButton(-440) 사이

        // HorizontalLayoutGroup 추가
        HorizontalLayoutGroup hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        return container;
    }

    private void CreateSnsIconButton(Transform parent, string snsName, Color bgColor)
    {
        CreateSnsIconButtonStatic(parent, snsName, bgColor);
    }

    private static void CreateSnsIconButtonStatic(Transform parent, string snsName, Color bgColor)
    {
        GameObject btnObj = new GameObject($"{snsName}Button");
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(70f, 70f);

        // 배경 이미지
        Image btnImage = btnObj.AddComponent<Image>();

        // Resources에서 아이콘 로드 시도
        string iconPath = $"SNS/{snsName.ToLower()}_icon";
        Sprite iconSprite = Resources.Load<Sprite>(iconPath);

        if (iconSprite != null)
        {
            btnImage.sprite = iconSprite;
            btnImage.color = Color.white;
        }
        else
        {
            // 폴백: 배경색
            btnImage.color = bgColor;

            // 텍스트 라벨 추가
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text label = textObj.AddComponent<Text>();
            label.text = snsName == "Instagram" ? "IG" : snsName == "X" ? "X" : "FB";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
        }

        // 버튼 컴포넌트
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        // 기본적으로 비활성화 (프로필에 SNS 정보가 있을 때만 활성화)
        btnObj.SetActive(false);
    }
}
#endif
