using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 업로드 페이지: NameInput / InstagramAccountInput 클릭 시
// DM 채팅 InputArea와 동일한 디자인의 미러 입력바를 키보드 위에 표시.
// 사용자는 미러 InputField에 직접 입력 → source InputField에 실시간 반영.
// 우측 닫기 버튼 → 키보드 dismiss + 미러 숨김.
//
// iOS native input toolbar와 키보드 본체 사이 갭 영역을 미러 InputArea가
// 자연스럽게 덮어 시각적 불연속 해소.
// ============================================================
public class UploadInputMirror : MonoBehaviour
{
    [Header("미러 UI (UploadInputMirrorSetup이 자동 연결)")]
    [SerializeField] private GameObject mirrorPanel;
    [SerializeField] private InputField mirrorInput;
    [SerializeField] private Text mirrorPlaceholder;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform mirrorRect;

    [Header("Source 입력칸")]
    [SerializeField] private InputField nameInput;
    [SerializeField] private InputField instagramInput;

    [Header("Placeholder 텍스트")]
    [SerializeField] private string namePlaceholder = "이름을 입력하세요";
    [SerializeField] private string instagramPlaceholder = "인스타그램 ID를 입력하세요";

    [Header("동작 설정")]
    [Tooltip("미러를 키보드 top에 부착할 때 추가 여백 (canvas px)")]
    [SerializeField] private float keyboardTopPadding = 0f;

    [Tooltip("위치 보정 애니메이션 속도")]
    [SerializeField] private float lerpSpeed = 14f;

    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private InputField activeSource;
    private bool syncing;
    private bool initialized;

    // Android 키보드 높이 baseline
    private int baselineVisibleBottom = -1;
    private float lastJniErrorTime;

    void OnEnable()
    {
        TryInitialize();
        HideMirror();
    }

    void OnDisable()
    {
        if (mirrorInput != null)
        {
            mirrorInput.onValueChanged.RemoveListener(OnMirrorChanged);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
        }
        HideMirror();
    }

    private void TryInitialize()
    {
        if (initialized) return;
        if (mirrorPanel == null || mirrorInput == null) return;

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null) canvasRect = parentCanvas.GetComponent<RectTransform>();

        mirrorInput.onValueChanged.RemoveListener(OnMirrorChanged);
        mirrorInput.onValueChanged.AddListener(OnMirrorChanged);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        initialized = true;
    }

    void Update()
    {
        if (!initialized) { TryInitialize(); if (!initialized) return; }

        InputField focused = DetectFocusedSource();

        if (focused != null && focused != activeSource)
        {
            ActivateMirrorFor(focused);
        }
        else if (focused == null && activeSource != null && !mirrorInput.isFocused)
        {
            // source / mirror 모두 포커스 잃음 → 키보드 내려간 것으로 간주
            HideMirror();
        }

        if (mirrorPanel != null && mirrorPanel.activeSelf)
        {
            UpdateMirrorPosition();
        }
    }

    private InputField DetectFocusedSource()
    {
        if (nameInput != null && nameInput.isFocused) return nameInput;
        if (instagramInput != null && instagramInput.isFocused) return instagramInput;
        return null;
    }

    private void ActivateMirrorFor(InputField source)
    {
        activeSource = source;

        if (mirrorPanel != null) mirrorPanel.SetActive(true);

        syncing = true;
        mirrorInput.text = source.text;
        if (mirrorPlaceholder != null)
        {
            mirrorPlaceholder.text = (source == nameInput) ? namePlaceholder : instagramPlaceholder;
        }

        mirrorInput.contentType = source.contentType;
        mirrorInput.lineType = source.lineType;
        mirrorInput.characterLimit = source.characterLimit;
        mirrorInput.keyboardType = source.keyboardType;
        syncing = false;

        // 포커스를 mirror로 이전 — source는 클릭 트리거 역할만
        mirrorInput.ActivateInputField();
        mirrorInput.Select();
        mirrorInput.caretPosition = mirrorInput.text.Length;
    }

    private void OnMirrorChanged(string value)
    {
        if (syncing || activeSource == null) return;
        syncing = true;
        activeSource.text = value;
        syncing = false;
    }

    private void OnCloseClicked()
    {
        if (mirrorInput != null && mirrorInput.isFocused)
            mirrorInput.DeactivateInputField();
        if (activeSource != null && activeSource.isFocused)
            activeSource.DeactivateInputField();

        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

        HideMirror();
    }

    private void HideMirror()
    {
        activeSource = null;
        if (mirrorPanel != null) mirrorPanel.SetActive(false);
    }

    private void UpdateMirrorPosition()
    {
        if (mirrorRect == null || canvasRect == null) return;

        float kbCanvas = GetKeyboardHeightCanvas();
        if (kbCanvas <= 0f) return;

        // bottom anchor를 키보드 top에 부착 — DM과 동일한 방식
        float canvasH = canvasRect.rect.height;
        float anchor = (kbCanvas + keyboardTopPadding) / canvasH;
        anchor = Mathf.Clamp01(anchor);

        Vector2 targetMin = new Vector2(0f, anchor);
        Vector2 targetMax = new Vector2(1f, anchor);

        float t = Time.unscaledDeltaTime * lerpSpeed;
        mirrorRect.anchorMin = Vector2.Lerp(mirrorRect.anchorMin, targetMin, t);
        mirrorRect.anchorMax = Vector2.Lerp(mirrorRect.anchorMax, targetMax, t);
    }

    private float GetKeyboardHeightCanvas()
    {
        float native = GetNativeKeyboardHeight();
        if (native <= 0f) return 0f;
        if (canvasRect != null)
        {
            float canvasH = canvasRect.rect.height;
            return native * (canvasH / Screen.height);
        }
        return native;
    }

    private float GetNativeKeyboardHeight()
    {
#if UNITY_EDITOR
        return 0f;
#elif UNITY_IOS
        Rect area = TouchScreenKeyboard.area;
        if (area.height <= 0) return 0f;
        return Screen.height - area.y;
#elif UNITY_ANDROID
        try
        {
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (act == null) return 0f;
                using (var win = act.Call<AndroidJavaObject>("getWindow"))
                {
                    if (win == null) return 0f;
                    using (var decor = win.Call<AndroidJavaObject>("getDecorView"))
                    {
                        if (decor == null) return 0f;
                        using (var rect = new AndroidJavaObject("android.graphics.Rect"))
                        {
                            decor.Call("getWindowVisibleDisplayFrame", rect);
                            int visibleBottom = rect.Get<int>("bottom");
                            if (baselineVisibleBottom < 0) baselineVisibleBottom = visibleBottom;
                            int kbH = baselineVisibleBottom - visibleBottom;
                            if (kbH > baselineVisibleBottom * 0.15f) return kbH;
                            if (kbH <= 0) baselineVisibleBottom = visibleBottom;
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            if (Time.time - lastJniErrorTime > 5f)
            {
                Debug.LogWarning($"[UpInputMirror] JNI 오류: {e.GetType().Name}: {e.Message}");
                lastJniErrorTime = Time.time;
            }
        }
        return 0f;
#else
        return 0f;
#endif
    }
}
