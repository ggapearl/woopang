using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 업로드/수정 페이지: source InputField 클릭 시 DM 채팅 InputArea와
// 동일한 디자인의 미러 입력바를 키보드 위에 표시.
// 사용자는 미러 InputField에 직접 입력 → source InputField에 실시간 반영.
// 우측 닫기 버튼 → 키보드 dismiss + 미러 숨김.
//
// iOS native input toolbar와 키보드 본체 사이 갭 영역을 미러 InputArea가
// 자연스럽게 덮어 시각적 불연속 해소.
//
// 사용처: CubeUploadPage / ModelUploadPage / FixPage
// 각 페이지의 sourceInputs를 자동 연결 (Setup 스크립트가 처리).
// ============================================================
public class UploadInputMirror : MonoBehaviour
{
    [Header("미러 UI (UploadInputMirrorSetup이 자동 연결)")]
    [SerializeField] private GameObject mirrorPanel;
    [SerializeField] private InputField mirrorInput;
    [SerializeField] private Text mirrorPlaceholder;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform mirrorRect;

    [Header("Source 입력칸 — Setup이 자동 채움")]
    [SerializeField] private InputField[] sourceInputs;

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

        Debug.Log($"[UpInputMirror] OnEnable - mirrorPanel={(mirrorPanel != null ? mirrorPanel.name : "<null>")}, " +
                  $"mirrorInput={(mirrorInput != null ? mirrorInput.name : "<null>")}, " +
                  $"closeButton={(closeButton != null ? closeButton.name : "<null>")}, " +
                  $"sourceInputs={(sourceInputs != null ? sourceInputs.Length : 0)}");
        if (sourceInputs != null)
        {
            for (int i = 0; i < sourceInputs.Length; i++)
            {
                var s = sourceInputs[i];
                Debug.Log($"[UpInputMirror]   source[{i}] {(s != null ? s.name : "<null>")} hideMobileInput={(s != null ? s.shouldHideMobileInput.ToString() : "?")}");
            }
        }
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

        // source InputField에 native 박스 표시 차단 — 미러만 보이도록
        if (sourceInputs != null)
        {
            for (int i = 0; i < sourceInputs.Length; i++)
            {
                if (sourceInputs[i] != null)
                    sourceInputs[i].shouldHideMobileInput = true;
            }
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
            HideMirror();
        }

        if (mirrorPanel != null && mirrorPanel.activeSelf)
        {
            UpdateMirrorPosition();
        }
    }

    private InputField DetectFocusedSource()
    {
        if (sourceInputs == null) return null;
        for (int i = 0; i < sourceInputs.Length; i++)
        {
            var inp = sourceInputs[i];
            if (inp != null && inp.isFocused && inp.gameObject.activeInHierarchy) return inp;
        }
        return null;
    }

    private void ActivateMirrorFor(InputField source)
    {
        activeSource = source;

        Debug.Log($"[UpInputMirror] ActivateMirrorFor - source={source.name}, mirrorPanel.active={(mirrorPanel != null ? mirrorPanel.activeSelf.ToString() : "<null>")}");

        if (mirrorPanel != null) mirrorPanel.SetActive(true);

        syncing = true;
        mirrorInput.text = source.text;

        if (mirrorPlaceholder != null)
        {
            string ph = ExtractPlaceholderText(source);
            mirrorPlaceholder.text = string.IsNullOrEmpty(ph) ? "입력하세요" : ph;
        }

        mirrorInput.contentType = source.contentType;
        mirrorInput.lineType = source.lineType;
        mirrorInput.characterLimit = source.characterLimit;
        mirrorInput.keyboardType = source.keyboardType;
        syncing = false;

        mirrorInput.ActivateInputField();
        mirrorInput.Select();
        mirrorInput.caretPosition = mirrorInput.text.Length;
    }

    private static string ExtractPlaceholderText(InputField src)
    {
        if (src == null || src.placeholder == null) return null;
        var t = src.placeholder as Text;
        return t != null ? t.text : null;
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
