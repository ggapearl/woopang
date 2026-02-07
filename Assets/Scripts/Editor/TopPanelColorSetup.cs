#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;

/// <summary>
/// Panel_Top에 TopPanelColorChanger 컴포넌트를 자동으로 추가하는 Editor 도구
/// </summary>
[InitializeOnLoad]
public class TopPanelColorSetup
{
    static TopPanelColorSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndSetupOnLoad;
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

    private static void CheckAndSetupOnLoad()
    {
        CheckAndSetupSilent();
    }

    /// <summary>
    /// Panel_Top에 TopPanelColorChanger가 없으면 자동 추가, 있으면 참조 확인
    /// </summary>
    private static void CheckAndSetupSilent()
    {
        // Panel_Top 찾기
        GameObject panelTop = GameObject.Find("Panel_Top");
        if (panelTop == null)
        {
            // Canvas 아래에서 찾기
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                Transform found = FindChildRecursive(canvas.transform, "Panel_Top");
                if (found != null)
                {
                    panelTop = found.gameObject;
                    break;
                }
            }
        }

        if (panelTop == null) return;

        // 컴포넌트 확인 또는 추가
        TopPanelColorChanger colorChanger = panelTop.GetComponent<TopPanelColorChanger>();
        bool isNewComponent = false;

        if (colorChanger == null)
        {
            Debug.Log("[TopPanelColorSetup] Panel_Top에 TopPanelColorChanger 컴포넌트 자동 추가 중...");
            colorChanger = panelTop.AddComponent<TopPanelColorChanger>();
            isNewComponent = true;
        }

        // 참조 연결 (새 컴포넌트이거나 참조가 비어있는 경우)
        Image bgImage = panelTop.GetComponent<Image>();

        // 배경 이미지 설정 수정 (반투명 스프라이트 문제 방지)
        if (bgImage != null && (bgImage.sprite != null || bgImage.type != Image.Type.Simple))
        {
            bgImage.sprite = null;
            bgImage.type = Image.Type.Simple;
            EditorUtility.SetDirty(bgImage);
            Debug.Log("[TopPanelColorSetup] Panel_Top Image를 Simple 타입으로 변경 (반투명 스프라이트 제거)");
        }

        // 자식에서 로고 이미지 찾기 (배경 이미지 제외)
        Image logoImage = null;
        Image[] childImages = panelTop.GetComponentsInChildren<Image>();
        foreach (var img in childImages)
        {
            if (img != bgImage && img.transform != panelTop.transform)
            {
                logoImage = img;
                break;
            }
        }

        // SerializedObject를 통해 private SerializeField 설정
        SerializedObject so = new SerializedObject(colorChanger);
        SerializedProperty bgProp = so.FindProperty("backgroundImage");
        SerializedProperty logoProp = so.FindProperty("logoImage");

        bool needsUpdate = false;

        // 참조가 비어있으면 연결
        if (bgProp != null && bgProp.objectReferenceValue == null && bgImage != null)
        {
            bgProp.objectReferenceValue = bgImage;
            needsUpdate = true;
        }
        if (logoProp != null && logoProp.objectReferenceValue == null && logoImage != null)
        {
            logoProp.objectReferenceValue = logoImage;
            needsUpdate = true;
        }

        if (isNewComponent || needsUpdate)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panelTop);
            EditorSceneManager.MarkSceneDirty(panelTop.scene);

            if (isNewComponent)
                Debug.Log("[TopPanelColorSetup] Panel_Top에 TopPanelColorChanger 컴포넌트 추가 완료!");
            else
                Debug.Log("[TopPanelColorSetup] Panel_Top의 TopPanelColorChanger 참조 연결 완료!");
        }
    }

    /// <summary>
    /// 재귀적으로 자식 찾기
    /// </summary>
    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
