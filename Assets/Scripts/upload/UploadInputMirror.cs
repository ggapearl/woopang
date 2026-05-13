using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// ============================================================
// 업로드/수정 페이지 단순 미러 입력바.
//
// 사용자가 mirrorPanel을 씬에 직접 배치 (DM ChatPanel.InputArea 복사 권장).
// 기본 SetActive(false). source 입력칸을 클릭하면 미러 패널 활성화 + 포커스 이전.
//
// 동작:
// - source 클릭 → mirrorPanel.SetActive(true) + mirrorInput.text 동기화 + ActivateInputField
// - mirrorInput.onValueChanged → source.text 동기화
// - 닫기 버튼 → mirrorPanel.SetActive(false), mirrorInput.DeactivateInputField
// - 키보드가 사라지면 자동으로 미러 패널도 닫음
// - 키보드 위로 mirrorPanel 위치 lerp (UpdateMirrorPosition)
// - 미러 패널 영역 밖 터치/드래그는 backdrop이 흡수해 뒷 UI 영향 차단
// ============================================================
public class UploadInputMirror : MonoBehaviour
{
    [Header("미러 UI — 사용자가 씬에 배치 후 직접 연결")]
    [Tooltip("미러 입력바 루트. 기본 SetActive(false) 상태로 배치")]
    [SerializeField] private GameObject mirrorPanel;
    [SerializeField] private InputField mirrorInput;
    [SerializeField] private Text mirrorPlaceholder;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform mirrorRect;

    [Header("Source 입력칸들 — 클릭 시 미러로 포커스 이전")]
    [SerializeField] private InputField[] sourceInputs;

    [Header("동작 설정")]
    [Tooltip("미러를 키보드 top에 부착할 때 추가 여백 (canvas px)")]
    [SerializeField] private float keyboardTopPadding = 0f;

    [Tooltip("위치 보정 애니메이션 속도")]
    [SerializeField] private float lerpSpeed = 14f;

    [Header("backdrop (자동 생성) — 미러 영역 밖 터치 차단")]
    [SerializeField] private bool createBackdrop = true;

    private RectTransform canvasRect;
    private InputField activeSource;
    private bool initialized;
    private bool syncing;

    private GameObject backdrop;
    private bool keyboardActive;
    private float mirrorActivatedAt = -1f;
    private const float KEYBOARD_GRACE = 0.6f;

    // 미러 활성 시 비활성화할 외부 페이지 스와이프 컨트롤러 (Touch.activeTouches 직접 폴링)
    private SwipePanelController[] cachedSwipeControllers;

    private int baselineVisibleBottom = -1;
    private float lastJniErrorTime;

    void OnEnable()
    {
        TryInitialize();
        HideMirror();
    }

    void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        if (mirrorInput != null) mirrorInput.onValueChanged.RemoveListener(OnMirrorTextChanged);
        DetachSourceTriggers();
        HideMirror();
    }

    private void TryInitialize()
    {
        if (initialized) return;
        if (mirrorPanel == null || mirrorInput == null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = canvas.GetComponent<RectTransform>();

        mirrorInput.interactable = true;
        mirrorInput.readOnly = false;
        mirrorInput.shouldHideMobileInput = true;

        mirrorInput.onValueChanged.RemoveListener(OnMirrorTextChanged);
        mirrorInput.onValueChanged.AddListener(OnMirrorTextChanged);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        if (createBackdrop) EnsureBackdrop();
        AttachSourceTriggers();

        initialized = true;
    }

    private void EnsureBackdrop()
    {
        if (backdrop != null) return;
        if (mirrorPanel == null) return;

        var mirrorCanvas = mirrorPanel.GetComponent<Canvas>();
        if (mirrorCanvas == null) mirrorCanvas = mirrorPanel.AddComponent<Canvas>();
        mirrorCanvas.overrideSorting = true;
        mirrorCanvas.sortingOrder = 32001;
        if (mirrorPanel.GetComponent<GraphicRaycaster>() == null)
            mirrorPanel.AddComponent<GraphicRaycaster>();

        // backdrop은 mirrorPanel의 부모를 부모로 + mirrorPanel 위쪽 영역만 차지 (키보드 영역 제외)
        // → 키보드 키 클릭은 backdrop이 안 막음, mirrorPanel은 본인 영역 raycast 처리
        Transform backdropParent = mirrorPanel.transform.parent;
        if (backdropParent == null) backdropParent = mirrorPanel.transform;

        backdrop = new GameObject("UploadInputMirrorBackdrop",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Canvas), typeof(GraphicRaycaster));
        backdrop.transform.SetParent(backdropParent, false);
        backdrop.layer = mirrorPanel.layer;

        Image img = backdrop.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.001f);
        img.raycastTarget = true;

        var bdCanvas = backdrop.GetComponent<Canvas>();
        bdCanvas.overrideSorting = true;
        bdCanvas.sortingOrder = 32000;

        backdrop.AddComponent<EventBlocker>();
        backdrop.SetActive(false);
    }

    // backdrop을 mirrorPanel 위쪽 화면 영역만 차지하도록 매 프레임 갱신
    private void UpdateBackdropRect()
    {
        if (backdrop == null || mirrorRect == null || canvasRect == null) return;
        var brt = backdrop.transform as RectTransform;
        if (brt == null) return;

        // mirrorPanel의 anchor.y가 키보드 상단 = backdrop의 bottom
        float mpAnchorY = mirrorRect.anchorMin.y;
        brt.anchorMin = new Vector2(0f, mpAnchorY);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        brt.sizeDelta = Vector2.zero;
    }

    private void AttachSourceTriggers()
    {
        if (sourceInputs == null) return;
        for (int i = 0; i < sourceInputs.Length; i++)
        {
            var src = sourceInputs[i];
            if (src == null) continue;
            src.shouldHideMobileInput = true;
            // InputField 컴포넌트 disable → 키보드/포커스 차단. EventTrigger만 PointerDown 수신
            src.enabled = false;
            AttachSourceTrigger(src);
        }
    }

    private void EnsureSourceState()
    {
        if (sourceInputs == null) return;
        for (int i = 0; i < sourceInputs.Length; i++)
        {
            var src = sourceInputs[i];
            if (src == null) continue;

            if (src.enabled) src.enabled = false;
            if (!src.shouldHideMobileInput) src.shouldHideMobileInput = true;

            var tr = src.GetComponent<EventTrigger>();
            bool hasPointerDown = false;
            if (tr != null)
            {
                for (int j = 0; j < tr.triggers.Count; j++)
                {
                    if (tr.triggers[j].eventID == EventTriggerType.PointerDown)
                    { hasPointerDown = true; break; }
                }
            }
            if (!hasPointerDown) AttachSourceTrigger(src);
        }
    }

    private void DetachSourceTriggers()
    {
        if (sourceInputs == null) return;
        for (int i = 0; i < sourceInputs.Length; i++)
        {
            if (sourceInputs[i] == null) continue;
            var trigger = sourceInputs[i].GetComponent<EventTrigger>();
            if (trigger == null) continue;
            for (int j = trigger.triggers.Count - 1; j >= 0; j--)
            {
                if (trigger.triggers[j].eventID == EventTriggerType.PointerDown)
                    trigger.triggers.RemoveAt(j);
            }
        }
    }

    private static bool HasPointerDownTrigger(EventTrigger tr)
    {
        for (int i = 0; i < tr.triggers.Count; i++)
            if (tr.triggers[i].eventID == EventTriggerType.PointerDown) return true;
        return false;
    }

    private void AttachSourceTrigger(InputField src)
    {
        var trigger = src.GetComponent<EventTrigger>();
        if (trigger == null) trigger = src.gameObject.AddComponent<EventTrigger>();

        for (int i = trigger.triggers.Count - 1; i >= 0; i--)
        {
            if (trigger.triggers[i].eventID == EventTriggerType.PointerDown)
                trigger.triggers.RemoveAt(i);
        }

        InputField captured = src;
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entry.callback.AddListener((_) => OnSourceClicked(captured));
        trigger.triggers.Add(entry);
    }

    private void OnSourceClicked(InputField src)
    {
        ActivateMirrorFor(src);
    }

    private void ActivateMirrorFor(InputField source)
    {
        activeSource = source;

        if (backdrop != null)
        {
            backdrop.SetActive(true);
            backdrop.transform.SetAsFirstSibling();
        }
        if (mirrorPanel != null)
        {
            mirrorPanel.SetActive(true);
            mirrorPanel.transform.SetAsLastSibling();
        }

        // SwipePanelController는 EventSystem을 거치지 않고 Touch.activeTouches를 직접 폴링하므로
        // backdrop의 e.Use()로 차단 불가 → 컴포넌트 자체를 비활성화
        DisableSwipeControllers();

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

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(mirrorInput.gameObject);
        mirrorInput.ActivateInputField();
        mirrorInput.Select();
        mirrorInput.caretPosition = mirrorInput.text.Length;

        keyboardActive = false;
        mirrorActivatedAt = Time.unscaledTime;
    }

    private static string ExtractPlaceholderText(InputField src)
    {
        if (src == null || src.placeholder == null) return null;
        var t = src.placeholder as Text;
        return t != null ? t.text : null;
    }

    private void OnMirrorTextChanged(string value)
    {
        if (syncing || activeSource == null) return;
        syncing = true;
        activeSource.text = value;
        syncing = false;
    }

    private void OnCloseClicked()
    {
        // Close 직전 명시적 동기화 — onValueChanged가 일부 native 키보드 경로에서 누락되는 케이스 안전망
        // 특히 source.enabled=false에서 setter 내부 갱신이 일부 InputField 케이스에서 누락되는 문제 보완
        SyncMirrorToSource();

        if (mirrorInput != null && mirrorInput.isFocused)
            mirrorInput.DeactivateInputField();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        HideMirror();
    }

    /// <summary>
    /// mirrorInput.text를 activeSource에 강제 반영.
    /// source.enabled=false 상태에서 텍스트가 안 들어가던 문제(InstagramAccountInput 등) 해결:
    /// - 일시적으로 enabled=true로 풀어 setter가 정상 동작하게 함
    /// - ForceLabelUpdate로 라벨 즉시 갱신
    /// - 외부 구독자(저장 버튼 등) 알림 위해 onEndEdit 명시 호출
    /// </summary>
    private void SyncMirrorToSource()
    {
        if (activeSource == null || mirrorInput == null) return;

        string finalText = mirrorInput.text ?? "";
        syncing = true;
        bool wasEnabled = activeSource.enabled;
        if (!wasEnabled) activeSource.enabled = true;

        activeSource.text = finalText;
        activeSource.ForceLabelUpdate();

        if (!wasEnabled) activeSource.enabled = false;
        syncing = false;

        // 외부 코드가 onEndEdit을 구독했을 경우 알림 (예: 업로드 매니저 저장 흐름)
        activeSource.onEndEdit?.Invoke(finalText);
    }

    private void HideMirror()
    {
        activeSource = null;
        keyboardActive = false;
        mirrorActivatedAt = -1f;
        if (mirrorPanel != null) mirrorPanel.SetActive(false);
        if (backdrop != null) backdrop.SetActive(false);
        RestoreSwipeControllers();
    }

    private void DisableSwipeControllers()
    {
        if (cachedSwipeControllers == null || cachedSwipeControllers.Length == 0)
        {
            cachedSwipeControllers = FindObjectsByType<SwipePanelController>(FindObjectsSortMode.None);
        }
        if (cachedSwipeControllers == null) return;
        for (int i = 0; i < cachedSwipeControllers.Length; i++)
        {
            if (cachedSwipeControllers[i] != null) cachedSwipeControllers[i].enabled = false;
        }
    }

    private void RestoreSwipeControllers()
    {
        if (cachedSwipeControllers == null) return;
        for (int i = 0; i < cachedSwipeControllers.Length; i++)
        {
            if (cachedSwipeControllers[i] != null) cachedSwipeControllers[i].enabled = true;
        }
    }

    void Update()
    {
        if (!initialized) { TryInitialize(); if (!initialized) return; }

        // source InputField가 다시 enable되면 즉시 비활성 — InstagramAccountInput처럼 동적 활성화 케이스
        // 미러 비활성 시에도 매 프레임 체크 (다른 코드가 enable로 되돌리면 native 키보드가 떠버림)
        if (sourceInputs != null)
        {
            for (int i = 0; i < sourceInputs.Length; i++)
            {
                var s = sourceInputs[i];
                if (s == null) continue;
                if (s.enabled) s.enabled = false;
                if (!s.shouldHideMobileInput) s.shouldHideMobileInput = true;
                // EventTrigger 누락 검사 — InstagramAccountInput처럼 비활성 → 활성 전환 후 트리거 필요
                var tr = s.GetComponent<EventTrigger>();
                if (tr == null || !HasPointerDownTrigger(tr))
                    AttachSourceTrigger(s);
            }
        }

        if (mirrorPanel == null || !mirrorPanel.activeSelf) return;

        // 미러 활성 중에는 SwipeController 매 프레임 강제 비활성
        if (cachedSwipeControllers != null)
        {
            for (int i = 0; i < cachedSwipeControllers.Length; i++)
            {
                if (cachedSwipeControllers[i] != null && cachedSwipeControllers[i].enabled)
                    cachedSwipeControllers[i].enabled = false;
            }
        }

        UpdateMirrorPosition();
        UpdateBackdropRect();

        bool inGrace = mirrorActivatedAt > 0f && (Time.unscaledTime - mirrorActivatedAt) < KEYBOARD_GRACE;

        float kbCanvas = GetKeyboardHeightCanvas();
        bool kbUp = kbCanvas > 0f;

        if (kbUp) keyboardActive = true;
        else if (keyboardActive && !inGrace)
        {
            OnCloseClicked();
        }
    }

    private void UpdateMirrorPosition()
    {
        if (mirrorRect == null || canvasRect == null) return;

        float kbCanvas = GetKeyboardHeightCanvas();
        if (kbCanvas <= 0f) return;

        float canvasH = canvasRect.rect.height;
        float anchor = Mathf.Clamp01((kbCanvas + keyboardTopPadding) / canvasH);

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
#if UNITY_EDITOR
        // EditorVirtualKeyboard는 이미 canvas 좌표(px)로 키보드를 그리므로 변환 불필요
        return native;
#else
        // iOS/Android는 screen 픽셀 → canvas 좌표 변환 필요
        if (canvasRect != null)
        {
            float canvasH = canvasRect.rect.height;
            return native * (canvasH / Screen.height);
        }
        return native;
#endif
    }

    private float GetNativeKeyboardHeight()
    {
#if UNITY_EDITOR
        return EditorVirtualKeyboard.CurrentVisibleHeight;
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

/// <summary>
/// 미러 영역 밖의 모든 드래그/터치를 흡수해 뒷 UI에 전파되지 않게 함.
/// </summary>
public class EventBlocker : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public void OnBeginDrag(PointerEventData e) { e.Use(); }
    public void OnDrag(PointerEventData e) { e.Use(); }
    public void OnEndDrag(PointerEventData e) { e.Use(); }
    public void OnPointerDown(PointerEventData e) { e.Use(); }
    public void OnPointerUp(PointerEventData e) { e.Use(); }
}
