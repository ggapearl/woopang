#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// AR Preview 시스템 자동 설정 도구
/// CubeUploadManager와 ARPreviewController를 자동으로 연결합니다.
/// </summary>
public class ARPreviewSetup : EditorWindow
{
    [MenuItem("Tools/Woopang/Setup AR Preview System")]
    public static void SetupARPreview()
    {
        // 1. CubeUploadManager 찾기
        CubeUploadManager uploadManager = Object.FindObjectOfType<CubeUploadManager>();
        if (uploadManager == null)
        {
            EditorUtility.DisplayDialog("오류", "CubeUploadManager를 찾을 수 없습니다.\n씬에 CubeUploadManager가 있는지 확인하세요.", "확인");
            return;
        }

        // 2. ARPreviewController 찾기 또는 생성
        ARPreviewController arPreviewController = Object.FindObjectOfType<ARPreviewController>();
        if (arPreviewController == null)
        {
            // ARPreviewController가 없으면 생성
            GameObject arPreviewObj = new GameObject("ARPreviewController");
            arPreviewController = arPreviewObj.AddComponent<ARPreviewController>();
            Debug.Log("<color=#00FF00>[ARPreviewSetup] ARPreviewController 생성됨</color>");
        }

        // 3. 0000_Cube.prefab 찾기
        string[] cubePrefabGuids = AssetDatabase.FindAssets("0000_Cube t:Prefab");
        GameObject cubePrefab = null;

        foreach (string guid in cubePrefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("sou") || path.Contains("prefab"))
            {
                cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Debug.Log($"<color=#00FF00>[ARPreviewSetup] Cube Prefab 발견: {path}</color>");
                break;
            }
        }

        if (cubePrefab == null)
        {
            // 다른 경로에서 시도
            cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/sou/prefab/0000_Cube.prefab");
        }

        if (cubePrefab == null)
        {
            Debug.LogWarning("[ARPreviewSetup] 0000_Cube.prefab을 찾을 수 없습니다. 수동으로 연결하세요.");
        }

        // 4. AR Preview Panel 찾기 또는 생성
        GameObject previewPanel = FindOrCreatePreviewPanel();

        // 5. CubeUploadManager에 연결
        SerializedObject uploadManagerSO = new SerializedObject(uploadManager);

        SerializedProperty arPreviewProp = uploadManagerSO.FindProperty("arPreviewController");
        if (arPreviewProp != null)
        {
            arPreviewProp.objectReferenceValue = arPreviewController;
        }

        SerializedProperty cubePrefabProp = uploadManagerSO.FindProperty("cubePrefab");
        if (cubePrefabProp != null && cubePrefab != null)
        {
            cubePrefabProp.objectReferenceValue = cubePrefab;
        }

        uploadManagerSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(uploadManager);

        // 6. ARPreviewController에 연결
        SerializedObject arPreviewSO = new SerializedObject(arPreviewController);

        if (cubePrefab != null)
        {
            SerializedProperty cubePrefabArProp = arPreviewSO.FindProperty("cubePrefab");
            if (cubePrefabArProp != null)
            {
                cubePrefabArProp.objectReferenceValue = cubePrefab;
            }
        }

        // Preview Panel UI 연결
        if (previewPanel != null)
        {
            SerializedProperty previewPanelProp = arPreviewSO.FindProperty("previewPanel");
            if (previewPanelProp != null)
            {
                previewPanelProp.objectReferenceValue = previewPanel;
            }

            // 자식 컴포넌트들 찾아서 연결
            Text messageText = previewPanel.GetComponentInChildren<Text>();
            if (messageText != null && messageText.name.Contains("Message"))
            {
                SerializedProperty msgProp = arPreviewSO.FindProperty("messageText");
                if (msgProp != null) msgProp.objectReferenceValue = messageText;
            }

            Button[] buttons = previewPanel.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                if (btn.name.ToLower().Contains("confirm") || btn.name.ToLower().Contains("ok") || btn.name.Contains("확인"))
                {
                    SerializedProperty confirmBtnProp = arPreviewSO.FindProperty("confirmButton");
                    if (confirmBtnProp != null) confirmBtnProp.objectReferenceValue = btn;
                }
                else if (btn.name.ToLower().Contains("cancel") || btn.name.Contains("취소"))
                {
                    SerializedProperty cancelBtnProp = arPreviewSO.FindProperty("cancelButton");
                    if (cancelBtnProp != null) cancelBtnProp.objectReferenceValue = btn;
                }
            }
        }

        arPreviewSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(arPreviewController);

        // 결과 출력
        string result = "AR Preview 설정 완료!\n\n";
        result += $"CubeUploadManager: {uploadManager.gameObject.name}\n";
        result += $"ARPreviewController: {arPreviewController.gameObject.name}\n";
        result += $"Cube Prefab: {(cubePrefab != null ? cubePrefab.name : "수동 연결 필요")}\n";
        result += $"Preview Panel: {(previewPanel != null ? previewPanel.name : "수동 생성 필요")}";

        EditorUtility.DisplayDialog("AR Preview 설정", result, "확인");

        Debug.Log($"<color=#00FF00>[ARPreviewSetup] 설정 완료!</color>");

        // Selection 변경
        Selection.activeGameObject = arPreviewController.gameObject;
    }

    /// <summary>
    /// AR Preview Panel UI 생성
    /// </summary>
    [MenuItem("Tools/Woopang/Create AR Preview Panel UI")]
    public static void CreatePreviewPanelUI()
    {
        // Canvas 찾기
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "Canvas를 찾을 수 없습니다.\n씬에 Canvas가 있는지 확인하세요.", "확인");
            return;
        }

        // 기존 ARPreviewCanvas 확인
        GameObject existingCanvas = GameObject.Find("ARPreviewCanvas");
        if (existingCanvas != null)
        {
            EditorUtility.DisplayDialog("알림", "ARPreviewCanvas가 이미 존재합니다.", "확인");
            Selection.activeGameObject = existingCanvas;
            return;
        }

        // AR Preview 전용 Canvas 생성 (Screen Space - Camera 모드)
        // 이렇게 하면 3D 오브젝트가 UI 앞에 표시됨
        GameObject canvasObj = new GameObject("ARPreviewCanvas");
        Canvas arPreviewCanvas = canvasObj.AddComponent<Canvas>();
        arPreviewCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        arPreviewCanvas.sortingOrder = 100; // 다른 UI보다 앞에

        // Main Camera 연결
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            arPreviewCanvas.worldCamera = mainCamera;
            arPreviewCanvas.planeDistance = 5f; // 3D 오브젝트(4m)보다 뒤에 배치
        }
        else
        {
            Debug.LogWarning("[ARPreviewSetup] Main Camera를 찾을 수 없습니다. Canvas의 Render Camera를 수동으로 설정하세요.");
        }

        // Canvas Scaler 추가
        UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // Graphic Raycaster 추가
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // ARPreviewPanel 생성
        GameObject panel = new GameObject("ARPreviewPanel");
        panel.transform.SetParent(canvasObj.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 배경 투명 (AR 화면 + 3D 오브젝트가 보이도록)
        Image bgImage = panel.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0f);  // 완전 투명 - 3D 오브젝트가 잘 보이도록

        // 메시지 컨테이너 (화면 하단 배치 - 큐브가 상단에 보이도록)
        GameObject messageContainer = new GameObject("MessageContainer");
        messageContainer.transform.SetParent(panel.transform, false);

        RectTransform msgContainerRect = messageContainer.AddComponent<RectTransform>();
        msgContainerRect.anchorMin = new Vector2(0.05f, 0.05f);  // 하단 5%부터
        msgContainerRect.anchorMax = new Vector2(0.95f, 0.35f);  // 하단 35%까지 (충분한 공간)
        msgContainerRect.offsetMin = Vector2.zero;
        msgContainerRect.offsetMax = Vector2.zero;

        Image msgBg = messageContainer.AddComponent<Image>();
        msgBg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);  // 약간 투명도 추가

        // 메시지 텍스트
        GameObject messageObj = new GameObject("MessageText");
        messageObj.transform.SetParent(messageContainer.transform, false);

        RectTransform msgRect = messageObj.AddComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0.05f, 0.5f);
        msgRect.anchorMax = new Vector2(0.95f, 0.9f);
        msgRect.offsetMin = Vector2.zero;
        msgRect.offsetMax = Vector2.zero;

        Text msgText = messageObj.AddComponent<Text>();
        msgText.text = "이곳에 오브젝트를 추가하시겠습니까?";
        msgText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        msgText.fontSize = 24;
        msgText.color = Color.white;
        msgText.alignment = TextAnchor.MiddleCenter;

        // 버튼 컨테이너
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(messageContainer.transform, false);

        RectTransform btnContainerRect = buttonContainer.AddComponent<RectTransform>();
        btnContainerRect.anchorMin = new Vector2(0.1f, 0.1f);
        btnContainerRect.anchorMax = new Vector2(0.9f, 0.45f);
        btnContainerRect.offsetMin = Vector2.zero;
        btnContainerRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup hlg = buttonContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // 확인 버튼
        GameObject confirmBtn = CreateButton("ConfirmButton", "확인", new Color(0.2f, 0.6f, 0.2f, 1f));
        confirmBtn.transform.SetParent(buttonContainer.transform, false);

        // 취소 버튼
        GameObject cancelBtn = CreateButton("CancelButton", "취소", new Color(0.6f, 0.2f, 0.2f, 1f));
        cancelBtn.transform.SetParent(buttonContainer.transform, false);

        // 초기 비활성화
        panel.SetActive(false);

        Debug.Log("<color=#00FF00>[ARPreviewSetup] ARPreviewCanvas + ARPreviewPanel UI 생성 완료!</color>");
        Debug.Log("<color=#00FF00>  - Canvas: Screen Space - Camera 모드 (Plane Distance: 5m)</color>");
        Debug.Log("<color=#00FF00>  - 3D 오브젝트(4m)가 패널(5m) 앞에 표시됩니다.</color>");

        string message = "ARPreviewCanvas가 생성되었습니다.\n\n";
        message += "설정:\n";
        message += "- Render Mode: Screen Space - Camera\n";
        message += "- Plane Distance: 5m (3D 오브젝트보다 뒤)\n\n";
        message += "Tools > Woopang > Setup AR Preview System을 실행하여 연결하세요.";
        EditorUtility.DisplayDialog("완료", message, "확인");

        Selection.activeGameObject = canvasObj;
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create AR Preview Canvas");
    }

    private static GameObject CreateButton(string name, string text, Color bgColor)
    {
        GameObject btnObj = new GameObject(name);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        // 버튼 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text btnText = textObj.AddComponent<Text>();
        btnText.text = text;
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 20;
        btnText.color = Color.white;
        btnText.alignment = TextAnchor.MiddleCenter;

        return btnObj;
    }

    private static GameObject FindOrCreatePreviewPanel()
    {
        // 기존 Panel 찾기
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("ARPreview") && obj.name.Contains("Panel"))
            {
                Debug.Log($"<color=#00FF00>[ARPreviewSetup] 기존 Panel 발견: {obj.name}</color>");
                return obj;
            }
        }

        // 찾지 못하면 null 반환 (수동 생성 필요)
        Debug.LogWarning("[ARPreviewSetup] ARPreviewPanel을 찾을 수 없습니다. Tools > Woopang > Create AR Preview Panel UI를 실행하세요.");
        return null;
    }
}
#endif
