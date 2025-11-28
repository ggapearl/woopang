using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// WP_1119 씬에 필요한 GameObject들을 자동으로 설정하는 헬퍼
/// Unity 상단 메뉴: Tools > Woopang > Setup Scene
/// </summary>
public class WoopangSceneSetupHelper : EditorWindow
{
    [MenuItem("Tools/Woopang/Setup AR Zoom Controller")]
    public static void SetupARZoomController()
    {
        // AR Camera 찾기
        Camera arCamera = Camera.main;
        if (arCamera == null)
        {
            arCamera = FindObjectOfType<Camera>();
        }

        // ARCameraManager 찾기
        ARCameraManager arCameraManager = FindObjectOfType<ARCameraManager>();

        // ZoomIndicator 찾기
        ZoomIndicator zoomIndicator = FindObjectOfType<ZoomIndicator>();

        // 이미 ARDigitalZoomController가 있는지 확인
        ARDigitalZoomController existingController = FindObjectOfType<ARDigitalZoomController>();
        if (existingController != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "ARDigitalZoomController 발견",
                "씬에 이미 ARDigitalZoomController가 존재합니다.\n기존 설정을 덮어쓰시겠습니까?",
                "덮어쓰기",
                "취소"
            );

            if (!overwrite)
            {
                Debug.Log("[WoopangSetup] 사용자가 취소했습니다.");
                return;
            }

            DestroyImmediate(existingController.gameObject);
        }

        // 새 GameObject 생성
        GameObject zoomControllerObj = new GameObject("ARZoomController");
        ARDigitalZoomController controller = zoomControllerObj.AddComponent<ARDigitalZoomController>();

        // SerializedObject로 private 필드 설정
        SerializedObject serializedController = new SerializedObject(controller);

        // AR Camera 설정
        SerializedProperty arCameraProp = serializedController.FindProperty("arCamera");
        arCameraProp.objectReferenceValue = arCamera;

        // ARCameraManager 설정
        if (arCameraManager != null)
        {
            SerializedProperty arCameraManagerProp = serializedController.FindProperty("arCameraManager");
            arCameraManagerProp.objectReferenceValue = arCameraManager;
        }

        // ZoomIndicator 설정
        if (zoomIndicator != null)
        {
            SerializedProperty zoomIndicatorProp = serializedController.FindProperty("zoomIndicatorObject");
            zoomIndicatorProp.objectReferenceValue = zoomIndicator.gameObject;
        }

        // 기본 설정값
        serializedController.FindProperty("defaultZoom").floatValue = 1f;
        serializedController.FindProperty("minZoom").floatValue = 0.5f;
        serializedController.FindProperty("maxZoom").floatValue = 3f;
        serializedController.FindProperty("zoomSpeed").floatValue = 0.01f;
        serializedController.FindProperty("smoothSpeed").floatValue = 5f;

        serializedController.ApplyModifiedProperties();

        // 씬 더티 마킹 (저장 필요 표시)
        EditorUtility.SetDirty(zoomControllerObj);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
        );

        // 선택
        Selection.activeGameObject = zoomControllerObj;

        Debug.Log($"[WoopangSetup] ✅ ARZoomController 생성 완료!");
        Debug.Log($"  - AR Camera: {(arCamera != null ? arCamera.name : "❌ 없음")}");
        Debug.Log($"  - ARCameraManager: {(arCameraManager != null ? "✅ 연결됨" : "⚠️ 없음 (자동 검색 모드)")}");
        Debug.Log($"  - ZoomIndicator: {(zoomIndicator != null ? "✅ 연결됨" : "⚠️ 없음 (선택사항)")}");

        EditorUtility.DisplayDialog(
            "ARZoomController 설정 완료",
            $"ARZoomController가 성공적으로 생성되었습니다!\n\n" +
            $"AR Camera: {(arCamera != null ? arCamera.name : "자동 검색 모드")}\n" +
            $"ARCameraManager: {(arCameraManager != null ? "연결됨" : "자동 검색 모드")}\n" +
            $"ZoomIndicator: {(zoomIndicator != null ? "연결됨" : "없음 (선택사항)")}\n\n" +
            $"Hierarchy에서 'ARZoomController'를 확인하세요.",
            "확인"
        );
    }

    [MenuItem("Tools/Woopang/Setup Filter Button Panel")]
    public static void SetupFilterButtonPanel()
    {
        // Canvas > ListPanel 찾기
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에서 Canvas를 찾을 수 없습니다!", "확인");
            return;
        }

        Transform listPanel = canvas.transform.Find("ListPanel");
        if (listPanel == null)
        {
            EditorUtility.DisplayDialog("오류", "Canvas 하위에 'ListPanel'을 찾을 수 없습니다!", "확인");
            return;
        }

        // FilterButtonPanel 프리팹 로드
        string prefabPath = "Assets/Prefabs/FilterButtonPanel.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("오류", $"프리팹을 찾을 수 없습니다:\n{prefabPath}", "확인");
            return;
        }

        // 이미 FilterButtonPanel이 있는지 확인
        Transform existingPanel = listPanel.Find("FilterButtonPanel");
        if (existingPanel != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "FilterButtonPanel 발견",
                "ListPanel에 이미 FilterButtonPanel이 존재합니다.\n기존 것을 삭제하고 새로 만드시겠습니까?",
                "삭제 후 생성",
                "취소"
            );

            if (!overwrite)
            {
                Debug.Log("[WoopangSetup] 사용자가 취소했습니다.");
                return;
            }

            DestroyImmediate(existingPanel.gameObject);
        }

        // 프리팹 인스턴스화
        GameObject filterPanelInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, listPanel);
        filterPanelInstance.name = "FilterButtonPanel";

        // FilterManager 컴포넌트 찾기
        FilterManager filterManager = filterPanelInstance.GetComponent<FilterManager>();
        if (filterManager == null)
        {
            EditorUtility.DisplayDialog("오류", "FilterButtonPanel 프리팹에 FilterManager 컴포넌트가 없습니다!", "확인");
            return;
        }

        // 토글 완성도 체크
        SerializedObject tempCheck = new SerializedObject(filterManager);
        int missingToggles = 0;
        System.Text.StringBuilder missingList = new System.Text.StringBuilder();

        if (tempCheck.FindProperty("petFriendlyToggle").objectReferenceValue == null) { missingToggles++; missingList.AppendLine("- PetFriendlyToggle"); }
        if (tempCheck.FindProperty("publicDataToggle").objectReferenceValue == null) { missingToggles++; missingList.AppendLine("- PublicDataToggle"); }
        if (tempCheck.FindProperty("subwayToggle").objectReferenceValue == null) { missingToggles++; missingList.AppendLine("- SubwayToggle"); }
        if (tempCheck.FindProperty("busToggle").objectReferenceValue == null) { missingToggles++; missingList.AppendLine("- BusToggle"); }
        if (tempCheck.FindProperty("alcoholToggle").objectReferenceValue == null) { missingToggles++; missingList.AppendLine("- AlcoholToggle"); }
        if (tempCheck.FindProperty("woopangDataToggle").objectReferenceValue == null) { missingToggles++; missingList.AppendLine("- WoopangDataToggle"); }
        if (tempCheck.FindProperty("selectAllButton").objectReferenceValue == null) { missingToggles++; missingList.AppendLine("- SelectAllButton"); }
        if (tempCheck.FindProperty("deselectAllButton").objectReferenceValue == null) { missingToggles++; missingList.AppendLine("- DeselectAllButton"); }

        if (missingToggles > 0)
        {
            EditorUtility.DisplayDialog(
                "⚠️ 프리팹 미완성",
                $"FilterButtonPanel 프리팹에 {missingToggles}개의 UI 요소가 누락되어 있습니다:\n\n" +
                missingList.ToString() + "\n" +
                "Assets/Prefabs/FilterButtonPanel_완성가이드.md 파일을 참고하여\n" +
                "Unity Editor에서 수동으로 추가하세요.",
                "확인"
            );
            Debug.LogWarning($"[WoopangSetup] FilterButtonPanel에 {missingToggles}개 UI 요소 누락! 가이드 참조: Assets/Prefabs/FilterButtonPanel_완성가이드.md");
        }

        // 필요한 매니저들 찾기
        PlaceListManager placeListManager = FindObjectOfType<PlaceListManager>();
        DataManager dataManager = FindObjectOfType<DataManager>();
        TourAPIManager tourAPIManager = FindObjectOfType<TourAPIManager>();

        // SerializedObject로 연결
        SerializedObject serializedFilterManager = new SerializedObject(filterManager);

        SerializedProperty placeListProp = serializedFilterManager.FindProperty("placeListManager");
        placeListProp.objectReferenceValue = placeListManager;

        SerializedProperty dataProp = serializedFilterManager.FindProperty("dataManager");
        dataProp.objectReferenceValue = dataManager;

        SerializedProperty tourProp = serializedFilterManager.FindProperty("tourAPIManager");
        tourProp.objectReferenceValue = tourAPIManager;

        serializedFilterManager.ApplyModifiedProperties();

        // 씬 더티 마킹
        EditorUtility.SetDirty(filterPanelInstance);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
        );

        // 선택
        Selection.activeGameObject = filterPanelInstance;

        Debug.Log($"[WoopangSetup] ✅ FilterButtonPanel 생성 완료!");
        Debug.Log($"  - PlaceListManager: {(placeListManager != null ? "✅" : "❌")}");
        Debug.Log($"  - DataManager: {(dataManager != null ? "✅" : "❌")}");
        Debug.Log($"  - TourAPIManager: {(tourAPIManager != null ? "✅" : "❌")}");

        EditorUtility.DisplayDialog(
            "FilterButtonPanel 설정 완료",
            $"FilterButtonPanel이 ListPanel에 추가되었습니다!\n\n" +
            $"PlaceListManager: {(placeListManager != null ? "✅ 연결됨" : "❌ 없음")}\n" +
            $"DataManager: {(dataManager != null ? "✅ 연결됨" : "❌ 없음")}\n" +
            $"TourAPIManager: {(tourAPIManager != null ? "✅ 연결됨" : "❌ 없음")}\n\n" +
            $"Hierarchy에서 'Canvas > ListPanel > FilterButtonPanel'을 확인하세요.",
            "확인"
        );
    }

    [MenuItem("Tools/Woopang/Setup All (Recommended)")]
    public static void SetupAll()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "전체 설정",
            "다음 작업을 수행합니다:\n\n" +
            "1. ARZoomController 생성 및 설정\n" +
            "2. FilterButtonPanel 추가 및 연결\n\n" +
            "계속하시겠습니까?",
            "예",
            "아니오"
        );

        if (!confirm)
        {
            return;
        }

        Debug.Log("[WoopangSetup] ========== 전체 설정 시작 ==========");

        // 1. AR Zoom Controller
        SetupARZoomController();

        // 2. Filter Button Panel
        SetupFilterButtonPanel();

        Debug.Log("[WoopangSetup] ========== 전체 설정 완료 ==========");

        EditorUtility.DisplayDialog(
            "전체 설정 완료",
            "모든 설정이 완료되었습니다!\n\n" +
            "Hierarchy를 확인하고 씬을 저장(Ctrl+S)하세요.",
            "확인"
        );
    }

    [MenuItem("Tools/Woopang/Check Scene Status")]
    public static void CheckSceneStatus()
    {
        System.Text.StringBuilder status = new System.Text.StringBuilder();
        status.AppendLine("=== WP_1119 씬 상태 체크 ===\n");

        // AR Zoom Controller
        ARDigitalZoomController zoomController = FindObjectOfType<ARDigitalZoomController>();
        status.AppendLine($"ARDigitalZoomController: {(zoomController != null ? "✅ 있음" : "❌ 없음")}");

        // Filter Button Panel
        Canvas canvas = FindObjectOfType<Canvas>();
        Transform filterPanel = null;
        if (canvas != null)
        {
            Transform listPanel = canvas.transform.Find("ListPanel");
            if (listPanel != null)
            {
                filterPanel = listPanel.Find("FilterButtonPanel");
            }
        }
        status.AppendLine($"FilterButtonPanel: {(filterPanel != null ? "✅ 있음" : "❌ 없음")}");

        // Managers
        PlaceListManager placeListManager = FindObjectOfType<PlaceListManager>();
        DataManager dataManager = FindObjectOfType<DataManager>();
        TourAPIManager tourAPIManager = FindObjectOfType<TourAPIManager>();
        ZoomIndicator zoomIndicator = FindObjectOfType<ZoomIndicator>();

        status.AppendLine($"\n--- 매니저들 ---");
        status.AppendLine($"PlaceListManager: {(placeListManager != null ? "✅" : "❌")}");
        status.AppendLine($"DataManager: {(dataManager != null ? "✅" : "❌")}");
        status.AppendLine($"TourAPIManager: {(tourAPIManager != null ? "✅" : "❌")}");
        status.AppendLine($"ZoomIndicator: {(zoomIndicator != null ? "✅" : "❌")}");

        // AR Camera
        Camera arCamera = Camera.main;
        ARCameraManager arCameraManager = FindObjectOfType<ARCameraManager>();
        status.AppendLine($"\n--- AR 컴포넌트 ---");
        status.AppendLine($"Main Camera: {(arCamera != null ? "✅" : "❌")}");
        status.AppendLine($"ARCameraManager: {(arCameraManager != null ? "✅" : "❌")}");

        string statusText = status.ToString();
        Debug.Log($"[WoopangSetup]\n{statusText}");

        EditorUtility.DisplayDialog("씬 상태 체크", statusText, "확인");
    }
}
