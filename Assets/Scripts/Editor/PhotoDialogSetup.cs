using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;

/// <summary>
/// PhotoSourceDialog 및 ContinueCaptureDialog UI 자동 생성 및 연결
/// 아이콘 기반 좌우 배치 스타일
/// </summary>
[InitializeOnLoad]
public class PhotoDialogSetup
{
    private const int DIALOG_VERSION = 6;

    static PhotoDialogSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndSetupDialogs;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckVersionAndRebuild;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CheckVersionAndRebuild;
    }

    private static void CheckAndSetupDialogs()
    {
        CheckVersionAndRebuild();
    }

    private static void CheckVersionAndRebuild()
    {
        // 이미 다이얼로그가 있으면 재생성하지 않음 (수동 수정 보호)
        var existingPhoto = Object.FindFirstObjectByType<PhotoSourceDialog>();
        var existingContinue = Object.FindFirstObjectByType<ContinueCaptureDialog>();

        if (existingPhoto != null && existingContinue != null)
        {
            // 이미 존재하면 연결만 확인
            CheckAndSetupDialogsSilent();
            return;
        }

        // 없을 때만 생성
        CheckAndSetupDialogsSilent();
    }

    private static void DestroyExistingDialogs()
    {
        var photoDialog = Object.FindFirstObjectByType<PhotoSourceDialog>();
        if (photoDialog != null)
            Object.DestroyImmediate(photoDialog.gameObject);

        var continueDialog = Object.FindFirstObjectByType<ContinueCaptureDialog>();
        if (continueDialog != null)
            Object.DestroyImmediate(continueDialog.gameObject);

        var manager = Object.FindFirstObjectByType<CubeUploadManager>();
        if (manager != null)
        {
            var photoSourceField = manager.GetType().GetField("photoSourceDialog",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (photoSourceField != null)
                photoSourceField.SetValue(manager, null);

            var continueField = manager.GetType().GetField("continueCaptureDialog",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (continueField != null)
                continueField.SetValue(manager, null);

            EditorUtility.SetDirty(manager);
        }
    }

    [MenuItem("WOOPANG/Rebuild Photo Dialogs")]
    private static void RebuildDialogsManual()
    {
        DestroyExistingDialogs();
        CheckAndSetupDialogsSilent();
        Debug.Log("[PhotoDialogSetup] 다이얼로그가 재생성되었습니다.");
    }

    private static void CheckAndSetupDialogsSilent()
    {
        var manager = Object.FindFirstObjectByType<CubeUploadManager>();
        if (manager == null) return;

        bool changed = false;

        // 기존 다이얼로그가 비활성화 상태면 활성화
        var existingPhoto = Object.FindFirstObjectByType<PhotoSourceDialog>(FindObjectsInactive.Include);
        if (existingPhoto != null && !existingPhoto.gameObject.activeSelf)
        {
            existingPhoto.gameObject.SetActive(true);
            EditorUtility.SetDirty(existingPhoto.gameObject);
            changed = true;
        }

        var existingContinue = Object.FindFirstObjectByType<ContinueCaptureDialog>(FindObjectsInactive.Include);
        if (existingContinue != null && !existingContinue.gameObject.activeSelf)
        {
            existingContinue.gameObject.SetActive(true);
            EditorUtility.SetDirty(existingContinue.gameObject);
            changed = true;
        }

        var photoSourceField = manager.GetType().GetField("photoSourceDialog",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (photoSourceField != null)
        {
            var currentDialog = photoSourceField.GetValue(manager) as PhotoSourceDialog;
            if (currentDialog == null)
            {
                var dialog = CreatePhotoSourceDialog();
                if (dialog != null)
                {
                    photoSourceField.SetValue(manager, dialog);
                    changed = true;
                }
            }
        }

        var continueField = manager.GetType().GetField("continueCaptureDialog",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (continueField != null)
        {
            var currentDialog = continueField.GetValue(manager) as ContinueCaptureDialog;
            if (currentDialog == null)
            {
                var dialog = CreateContinueCaptureDialog();
                if (dialog != null)
                {
                    continueField.SetValue(manager, dialog);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
    }

    private static PhotoSourceDialog CreatePhotoSourceDialog()
    {
        var existing = Object.FindFirstObjectByType<PhotoSourceDialog>();
        if (existing != null) return existing;

        Canvas canvas = FindOrCreateCanvas();
        if (canvas == null) return null;

        // 아이콘 로드
        Sprite cameraIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sou/UI/Dialog/icon_camera.png");
        Sprite galleryIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sou/UI/Dialog/icon_gallery.png");
        Sprite cancelIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sou/UI/Dialog/icon_cancel.png");

        GameObject root = new GameObject("PhotoSourceDialog");
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        var photoSourceDialog = root.AddComponent<PhotoSourceDialog>();

        var dialogCanvas = root.AddComponent<Canvas>();
        dialogCanvas.overrideSorting = true;
        dialogCanvas.sortingOrder = 30000;
        root.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // DialogPanel (전체 화면 오버레이)
        GameObject dialogPanel = CreateOverlayPanel(root.transform, "DialogPanel");

        // BottomContainer
        GameObject bottomContainer = CreateBottomContainer(dialogPanel.transform, "BottomContainer");

        // ContentCard (둥근 카드)
        GameObject contentCard = CreateRoundedCard(bottomContainer.transform, "ContentCard", 400f);
        RectTransform cardRect = contentCard.GetComponent<RectTransform>();
        cardRect.anchoredPosition = new Vector2(0, 100);

        // 버튼 컨테이너 (좌우 배치)
        GameObject buttonRow = new GameObject("ButtonRow");
        buttonRow.transform.SetParent(contentCard.transform, false);
        RectTransform rowRect = buttonRow.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0, 0.3f);
        rowRect.anchorMax = new Vector2(1, 1);
        rowRect.offsetMin = new Vector2(20, 0);
        rowRect.offsetMax = new Vector2(-20, -20);

        // 카메라 버튼 (왼쪽) - 런타임에 LocalizationManager가 번역
        GameObject cameraBtn = CreateIconButton(buttonRow.transform, "CameraButton", "Camera", cameraIcon);
        RectTransform cameraBtnRect = cameraBtn.GetComponent<RectTransform>();
        cameraBtnRect.anchorMin = new Vector2(0, 0);
        cameraBtnRect.anchorMax = new Vector2(0.48f, 1);
        cameraBtnRect.offsetMin = Vector2.zero;
        cameraBtnRect.offsetMax = Vector2.zero;

        // 갤러리 버튼 (오른쪽) - 런타임에 LocalizationManager가 번역
        GameObject galleryBtn = CreateIconButton(buttonRow.transform, "GalleryButton", "Album", galleryIcon);
        RectTransform galleryBtnRect = galleryBtn.GetComponent<RectTransform>();
        galleryBtnRect.anchorMin = new Vector2(0.52f, 0);
        galleryBtnRect.anchorMax = new Vector2(1, 1);
        galleryBtnRect.offsetMin = Vector2.zero;
        galleryBtnRect.offsetMax = Vector2.zero;

        // 취소 버튼 (하단 중앙) - 런타임에 LocalizationManager가 번역
        GameObject cancelBtn = CreateIconButton(contentCard.transform, "CancelButton", "Cancel", cancelIcon, true);
        RectTransform cancelBtnRect = cancelBtn.GetComponent<RectTransform>();
        cancelBtnRect.anchorMin = new Vector2(0.3f, 0);
        cancelBtnRect.anchorMax = new Vector2(0.7f, 0.28f);
        cancelBtnRect.offsetMin = new Vector2(0, 15);
        cancelBtnRect.offsetMax = new Vector2(0, -5);

        // 필드 연결
        SetPrivateField(photoSourceDialog, "dialogPanel", dialogPanel);
        SetPrivateField(photoSourceDialog, "cameraButton", cameraBtn.GetComponent<Button>());
        SetPrivateField(photoSourceDialog, "galleryButton", galleryBtn.GetComponent<Button>());
        SetPrivateField(photoSourceDialog, "cancelButton", cancelBtn.GetComponent<Button>());
        SetPrivateField(photoSourceDialog, "cameraButtonText", cameraBtn.GetComponentInChildren<Text>());
        SetPrivateField(photoSourceDialog, "galleryButtonText", galleryBtn.GetComponentInChildren<Text>());
        SetPrivateField(photoSourceDialog, "cancelButtonText", cancelBtn.GetComponentInChildren<Text>());

        dialogPanel.SetActive(false);

        return photoSourceDialog;
    }

    private static ContinueCaptureDialog CreateContinueCaptureDialog()
    {
        var existing = Object.FindFirstObjectByType<ContinueCaptureDialog>();
        if (existing != null) return existing;

        Canvas canvas = FindOrCreateCanvas();
        if (canvas == null) return null;

        // 아이콘 로드
        Sprite continueIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sou/UI/Dialog/icon_continue.png");
        Sprite finishIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sou/UI/Dialog/icon_finish.png");

        GameObject root = new GameObject("ContinueCaptureDialog");
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        var continueDialog = root.AddComponent<ContinueCaptureDialog>();

        var dialogCanvas = root.AddComponent<Canvas>();
        dialogCanvas.overrideSorting = true;
        dialogCanvas.sortingOrder = 30000;
        root.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // DialogPanel (전체 화면 오버레이)
        GameObject dialogPanel = CreateOverlayPanel(root.transform, "DialogPanel");

        // BottomContainer
        GameObject bottomContainer = CreateBottomContainer(dialogPanel.transform, "BottomContainer");

        // ContentCard (둥근 카드)
        GameObject contentCard = CreateRoundedCard(bottomContainer.transform, "ContentCard", 450f);
        RectTransform cardRect = contentCard.GetComponent<RectTransform>();
        cardRect.anchoredPosition = new Vector2(0, 100);

        // 타이틀 (런타임에 LocalizationManager가 번역)
        GameObject titleObj = CreateText(contentCard.transform, "TitleText", "Continue capturing?", 48, FontStyle.Bold, Color.white);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.78f);
        titleRect.anchorMax = new Vector2(1, 0.95f);
        titleRect.offsetMin = new Vector2(20, 0);
        titleRect.offsetMax = new Vector2(-20, 0);

        // 메시지 (런타임에 동적 설정)
        GameObject messageObj = CreateText(contentCard.transform, "MessageText", "Current 0/10", 40, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f));
        RectTransform messageRect = messageObj.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0, 0.62f);
        messageRect.anchorMax = new Vector2(1, 0.78f);
        messageRect.offsetMin = new Vector2(20, 0);
        messageRect.offsetMax = new Vector2(-20, 0);

        // 버튼 컨테이너 (좌우 배치)
        GameObject buttonRow = new GameObject("ButtonRow");
        buttonRow.transform.SetParent(contentCard.transform, false);
        RectTransform rowRect = buttonRow.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0, 0.08f);
        rowRect.anchorMax = new Vector2(1, 0.58f);
        rowRect.offsetMin = new Vector2(30, 0);
        rowRect.offsetMax = new Vector2(-30, -10);

        // 계속 촬영 버튼 (왼쪽) - 런타임에 LocalizationManager가 번역
        GameObject yesBtn = CreateIconButton(buttonRow.transform, "YesButton", "Continue", continueIcon);
        RectTransform yesBtnRect = yesBtn.GetComponent<RectTransform>();
        yesBtnRect.anchorMin = new Vector2(0, 0);
        yesBtnRect.anchorMax = new Vector2(0.48f, 1);
        yesBtnRect.offsetMin = Vector2.zero;
        yesBtnRect.offsetMax = Vector2.zero;

        // 완료 버튼 (오른쪽) - 런타임에 LocalizationManager가 번역
        GameObject noBtn = CreateIconButton(buttonRow.transform, "NoButton", "Done", finishIcon);
        RectTransform noBtnRect = noBtn.GetComponent<RectTransform>();
        noBtnRect.anchorMin = new Vector2(0.52f, 0);
        noBtnRect.anchorMax = new Vector2(1, 1);
        noBtnRect.offsetMin = Vector2.zero;
        noBtnRect.offsetMax = Vector2.zero;

        // 필드 연결
        SetPrivateField(continueDialog, "dialogPanel", dialogPanel);
        SetPrivateField(continueDialog, "yesButton", yesBtn.GetComponent<Button>());
        SetPrivateField(continueDialog, "noButton", noBtn.GetComponent<Button>());
        SetPrivateField(continueDialog, "titleText", titleObj.GetComponent<Text>());
        SetPrivateField(continueDialog, "messageText", messageObj.GetComponent<Text>());
        SetPrivateField(continueDialog, "yesButtonText", yesBtn.GetComponentInChildren<Text>());
        SetPrivateField(continueDialog, "noButtonText", noBtn.GetComponentInChildren<Text>());

        dialogPanel.SetActive(false);

        return continueDialog;
    }

    private static Canvas FindOrCreateCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                if (c.name == "MainCanvas" || c.name == "Canvas")
                    return c;
            }
        }
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        }
        return null;
    }

    private static GameObject CreateOverlayPanel(Transform parent, string name)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        img.raycastTarget = true;

        Button bgButton = panel.AddComponent<Button>();
        bgButton.transition = Selectable.Transition.None;

        return panel;
    }

    private static GameObject CreateBottomContainer(Transform parent, string name)
    {
        GameObject container = new GameObject(name);
        container.transform.SetParent(parent, false);

        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(-40, 700);

        return container;
    }

    private static GameObject CreateRoundedCard(Transform parent, string name, float height)
    {
        GameObject card = new GameObject(name);
        card.transform.SetParent(parent, false);

        RectTransform rect = card.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.sizeDelta = new Vector2(0, height);

        Image img = card.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        img.raycastTarget = true;

        return card;
    }

    private static GameObject CreateIconButton(Transform parent, string name, string labelText, Sprite icon, bool isSmall = false)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();

        Image bgImg = btnObj.AddComponent<Image>();
        bgImg.color = new Color(0.25f, 0.25f, 0.28f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bgImg;

        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.25f, 0.25f, 0.28f, 1f);
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.38f, 1f);
        colors.pressedColor = new Color(0.2f, 0.2f, 0.22f, 1f);
        colors.selectedColor = new Color(0.25f, 0.25f, 0.28f, 1f);
        btn.colors = colors;

        // 아이콘
        if (icon != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(btnObj.transform, false);

            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            if (isSmall)
            {
                iconRect.anchorMin = new Vector2(0.5f, 0.35f);
                iconRect.anchorMax = new Vector2(0.5f, 0.35f);
                iconRect.sizeDelta = new Vector2(60, 60);
            }
            else
            {
                iconRect.anchorMin = new Vector2(0.5f, 0.55f);
                iconRect.anchorMax = new Vector2(0.5f, 0.55f);
                iconRect.sizeDelta = new Vector2(120, 120);
            }
            iconRect.anchoredPosition = Vector2.zero;

            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
        }

        // 라벨
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        if (isSmall)
        {
            textRect.anchorMin = new Vector2(0, 0.55f);
            textRect.anchorMax = new Vector2(1, 0.9f);
        }
        else
        {
            textRect.anchorMin = new Vector2(0, 0.05f);
            textRect.anchorMax = new Vector2(1, 0.3f);
        }
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text textComp = textObj.AddComponent<Text>();
        textComp.text = labelText;
        textComp.alignment = TextAnchor.MiddleCenter;
        textComp.color = Color.white;
        textComp.fontSize = isSmall ? 36 : 44;

        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (customFont != null)
            textComp.font = customFont;
        else
            textComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return btnObj;
    }

    private static GameObject CreateText(Transform parent, string name, string text, int fontSize, FontStyle style, Color color)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        textObj.AddComponent<RectTransform>();

        Text textComp = textObj.AddComponent<Text>();
        textComp.text = text;
        textComp.alignment = TextAnchor.MiddleCenter;
        textComp.color = color;
        textComp.fontSize = fontSize;
        textComp.fontStyle = style;

        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (customFont != null)
            textComp.font = customFont;
        else
            textComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return textObj;
    }

    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            field.SetValue(obj, value);
    }
}
