using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;

/// <summary>
/// ARPreviewController의 DimOverlay 오브젝트를 Canvas 하위에 자동 생성 + 연결
/// IndicatorSparkleHelper가 씬에 없으면 자동 생성
/// </summary>
[InitializeOnLoad]
public class ARPreviewDimOverlaySetup
{
    static ARPreviewDimOverlaySetup()
    {
        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            EditorApplication.delayCall += CheckAndSetup;
        };
        EditorApplication.delayCall += CheckAndSetup;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckAndSetup;
    }

    private static void CheckAndSetup()
    {
        SetupDimOverlay();
        SetupIndicatorSparkleHelper();
    }

    private static void SetupDimOverlay()
    {
        ARPreviewController controller = Object.FindFirstObjectByType<ARPreviewController>();
        if (controller == null) return;

        // dimOverlayObject 필드 확인
        SerializedObject so = new SerializedObject(controller);
        SerializedProperty dimProp = so.FindProperty("dimOverlayObject");
        if (dimProp == null) return;

        // 이미 연결되어 있으면 스킵
        if (dimProp.objectReferenceValue != null) return;

        // Canvas 찾기
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 이미 존재하는지 확인
        Transform existing = canvas.transform.Find("ARPreview_DimOverlay");
        GameObject dimObj;
        if (existing != null)
        {
            dimObj = existing.gameObject;
        }
        else
        {
            // Canvas 하위에 DimOverlay 생성
            dimObj = new GameObject("ARPreview_DimOverlay");
            dimObj.transform.SetParent(canvas.transform, false);

            // RectTransform 전체 화면 채우기
            RectTransform rect = dimObj.GetComponent<RectTransform>();
            if (rect == null) rect = dimObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Image 컴포넌트 (투명 검정)
            Image img = dimObj.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;

            // 다른 UI 뒤에 배치 (첫 번째 자식으로)
            dimObj.transform.SetAsFirstSibling();

            dimObj.SetActive(false);
        }

        // ARPreviewController에 연결
        dimProp.objectReferenceValue = dimObj;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }

    private static void SetupIndicatorSparkleHelper()
    {
        // 이미 씬에 있으면 스킵
        IndicatorSparkleHelper existing = Object.FindFirstObjectByType<IndicatorSparkleHelper>();
        if (existing != null) return;

        // OffScreenIndicator Panel을 찾아서 하위에 생성
        GameObject parent = null;

        // OffScreenIndicator Panel 찾기
        OffScreenIndicator osi = Object.FindFirstObjectByType<OffScreenIndicator>();
        if (osi != null)
        {
            parent = osi.gameObject;
        }

        if (parent == null)
        {
            // Canvas 하위에 생성
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
                parent = canvas.gameObject;
        }

        if (parent == null) return;

        GameObject sparkleObj = new GameObject("IndicatorSparkleHelper");
        sparkleObj.transform.SetParent(parent.transform, false);

        IndicatorSparkleHelper helper = sparkleObj.AddComponent<IndicatorSparkleHelper>();
        helper.enableSparkle = true;
        helper.arrowOnly = false; // Spawn Emphasis에서 ARROW로 호출하지만 향후 확장 고려

        // circle 스프라이트 로드 시도
        Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sou/UI/circle.png");
        if (circleSprite == null)
            circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/circle.png");
        if (circleSprite != null)
            helper.sparkleSprite = circleSprite;

        EditorUtility.SetDirty(sparkleObj);
        EditorSceneManager.MarkSceneDirty(sparkleObj.scene);
    }
}
