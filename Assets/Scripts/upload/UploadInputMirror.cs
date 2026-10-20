using System.Collections;
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

    [Header("보내기 칸 (보물찾기 한마디 등) — 버튼이 ✓ 대신 보내기")]
    [SerializeField] private Image closeIcon;
    [SerializeField] private Sprite sendSprite;

    private RectTransform canvasRect;
    private InputField activeSource;
    private bool initialized;
    private bool syncing;
    private bool closing;              // 닫는 중 — 키보드가 내려가며 되돌린 글은 원래 칸에 옮기지 않는다
    private string lastTyped = "";     // 키보드가 떠 있는 동안 마지막으로 쓴 글
    private bool submitRequested;      // 키보드의 완료·엔터
    private Sprite checkSprite;
    private MirrorButtonPress closePress;   // 버튼을 누르는 순간 입력칸이 포커스를 잃어 키보드가 먼저 내려간다 — 그 사이 저절로 닫히지 않게
    private TouchScreenKeyboard lastKb;
    private readonly System.Collections.Generic.Dictionary<InputField, System.Action> submitActions =
        new System.Collections.Generic.Dictionary<InputField, System.Action>();

    private GameObject backdrop;
    private bool keyboardActive;
    private float mirrorActivatedAt = -1f;
    private const float KEYBOARD_GRACE = 0.6f;

    // 미러 활성 시 비활성화할 외부 페이지 스와이프 컨트롤러 (Touch.activeTouches 직접 폴링)
    private SwipePanelController[] cachedSwipeControllers;

    private int baselineVisibleBottom = -1;
    private float lastJniErrorTime;

    // 키보드 높이를 알기 전에는 보이지 않는다 — 예전엔 입력줄이 지난 자리(화면 아래)에 먼저 뜨고
    // 키보드가 올라온 뒤에야 따라 올라갔다 (2026-10-04). 알게 되면 그 자리에 바로 놓고 살짝 나타난다.
    private CanvasGroup mirrorGroup;
    private bool placed;                    // 키보드 위에 처음 놓았는지
    private const float SHOW_FALLBACK = 0.5f;   // 키보드 높이를 못 읽는 기기 — 이만큼 지나면 그냥 보인다

    void OnEnable()
    {
        TryInitialize();
        HideMirror();
    }

    void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        if (mirrorInput != null)
        {
            mirrorInput.onValueChanged.RemoveListener(OnMirrorTextChanged);
            mirrorInput.onSubmit.RemoveListener(OnMirrorSubmit);
        }
        DetachSourceTriggers();
        HideMirror();
        initialized = false;   // 다시 켜지면 듣기·트리거를 다시 단다 (예전엔 끈 뒤로 ✓·입력이 먹지 않았다)
    }

    /// <summary>지금 입력줄로 쓰고 있는 원래 칸 (닫혀 있거나 닫는 중이면 null) — R0926InputFocus 가 테두리·스크롤에 쓴다</summary>
    public InputField ActiveSource => mirrorPanel != null && mirrorPanel.activeSelf && !closing ? activeSource : null;
    /// <summary>키보드 위에 자리 잡은 입력줄 (아직이면 null)</summary>
    public RectTransform PlacedBar => placed ? mirrorRect : null;
    public bool IsMirrorInput(InputField f) => f != null && f == mirrorInput;
    /// <summary>키보드 위 입력줄의 실제 입력칸 — R0926InputFocus 가 이 칸의 커서 자리·모양을 원래 칸 안에도 같게 보여 준다</summary>
    public InputField Bar => mirrorInput;

    /// <summary>이 칸은 입력줄 버튼이 '완료' 가 아니라 '보내기' — 누르면(키보드 엔터도) 글을 옮기고 바로 보낸다. null 이면 해제</summary>
    public void SetSubmitAction(InputField src, System.Action onSubmit)
    {
        if (src == null) return;
        if (onSubmit == null) submitActions.Remove(src);
        else submitActions[src] = onSubmit;
    }

    private void TryInitialize()
    {
        if (initialized) return;
        if (mirrorPanel == null || mirrorInput == null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = canvas.GetComponent<RectTransform>();
        if (closeIcon != null && checkSprite == null) checkSprite = closeIcon.sprite;

        mirrorInput.interactable = true;
        mirrorInput.readOnly = false;
        mirrorInput.shouldHideMobileInput = true;

        mirrorInput.onValueChanged.RemoveListener(OnMirrorTextChanged);
        mirrorInput.onValueChanged.AddListener(OnMirrorTextChanged);
        mirrorInput.onSubmit.RemoveListener(OnMirrorSubmit);
        mirrorInput.onSubmit.AddListener(OnMirrorSubmit);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
            closePress = closeButton.GetComponent<MirrorButtonPress>();
            if (closePress == null) closePress = closeButton.gameObject.AddComponent<MirrorButtonPress>();
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
        ReleaseOtherInput();
        activeSource = source;
        submitRequested = false;
        PaintCloseIcon(submitActions.ContainsKey(source));

        if (backdrop != null)
        {
            backdrop.SetActive(true);
            backdrop.transform.SetAsFirstSibling();
        }
        if (mirrorPanel != null)
        {
            mirrorPanel.SetActive(true);
            mirrorPanel.transform.SetAsLastSibling();
            if (mirrorGroup == null)
            {
                // ?? 는 유니티의 가짜 null 을 못 거른다 — 명시적으로 확인
                mirrorGroup = mirrorPanel.GetComponent<CanvasGroup>();
                if (mirrorGroup == null) mirrorGroup = mirrorPanel.AddComponent<CanvasGroup>();
            }
            mirrorGroup.alpha = 0f;   // 키보드 위에 놓일 때까지 숨긴다
            placed = false;
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
        lastTyped = source.text ?? "";
        closing = false;
    }

    // 다른 입력칸(뒤에 열린 대화창 등)이 아직 키보드를 쥐고 있으면 먼저 놓게 한다 — 두 칸이 한 키보드를 나눠 쓰면
    // 친 글이 그쪽으로 가거나 지워졌다 (2026-10-14 안드로이드: 보물찾기 제목·힌트가 안 들어감)
    private void ReleaseOtherInput()
    {
        var es = EventSystem.current;
        var sel = es != null ? es.currentSelectedGameObject : null;
        var other = sel != null ? sel.GetComponent<InputField>() : null;
        if (other == null || other == mirrorInput) return;
        if (other is KeyboardPersistentInputField kp) kp.lockFocus = false;
        if (other.isFocused) other.DeactivateInputField();
    }

    private void PaintCloseIcon(bool send)
    {
        if (closeIcon == null) return;
        bool s = send && sendSprite != null;
        if (checkSprite == null) checkSprite = closeIcon.sprite;
        closeIcon.sprite = s ? sendSprite : checkSprite;
        var rt = closeIcon.rectTransform;
        rt.localEulerAngles = s ? new Vector3(0, 0, 180) : Vector3.zero;   // 아래 화살표를 뒤집어 보내기(위) — 채팅 입력줄과 같게
        rt.sizeDelta = s ? new Vector2(50, 62) : new Vector2(58, 58);
    }

    private void OnMirrorSubmit(string _)
    {
        if (activeSource != null && !closing) submitRequested = true;
    }

    private static string ExtractPlaceholderText(InputField src)
    {
        if (src == null || src.placeholder == null) return null;
        var t = src.placeholder as Text;
        return t != null ? t.text : null;
    }

    private void OnMirrorTextChanged(string value)
    {
        if (syncing || closing || activeSource == null) return;
        // 키보드가 닫히면서 입력칸이 처음 글로 되돌리는 것은 옮기지 않는다 (그 빈 글이 원래 칸을 덮어썼다)
        var kb = mirrorInput != null ? mirrorInput.touchScreenKeyboard : null;
        if (kb != null && kb.status != TouchScreenKeyboard.Status.Visible) return;
        lastTyped = value ?? "";
        syncing = true;
        activeSource.text = lastTyped;
        syncing = false;
    }

    private void OnCloseClicked() => BeginClose(true);

    // pressed = 입력줄 버튼을 눌렀다 (보내기 칸이면 보낸다). 키보드가 그냥 내려가면 false — 그땐 엔터로 닫혔을 때만 보낸다
    private void BeginClose(bool pressed)
    {
        if (closing || activeSource == null) return;
        if (!isActiveAndEnabled) { CloseNow(FinalText(null), activeSource, pressed || submitRequested); return; }
        StartCoroutine(CloseRoutine(pressed));
    }

    // 한글은 마지막 글자가 '조합 중'이면 아직 입력칸에 안 들어와 있다 — 키보드를 먼저 내리면 그때 확정되므로
    // 두 프레임 기다렸다가 키보드에 남은 글을 다시 읽는다. 닫는 사이 칸이 바뀌지 않게 처음 칸을 붙잡아 둔다
    private IEnumerator CloseRoutine(bool pressed)
    {
        closing = true;
        var src = activeSource;
        var kb = mirrorInput != null ? mirrorInput.touchScreenKeyboard : null;
        if (kb == null) kb = lastKb;   // 버튼을 누르는 순간 입력칸이 먼저 놓여 키보드 손잡이가 비어 있다
        string final = FinalText(kb);
        if (mirrorInput != null && mirrorInput.isFocused) mirrorInput.DeactivateInputField();
        if (kb != null)
        {
            yield return null;
            yield return null;
            // 닫힌 키보드의 글을 비워 주는 기기도 있다 — 빈 글로 친 글을 덮지 않는다
            if (!string.IsNullOrEmpty(kb.text) && kb.status != TouchScreenKeyboard.Status.Canceled) final = kb.text;
        }
        closing = false;
        CloseNow(final, src, pressed || submitRequested);
    }

    private string FinalText(TouchScreenKeyboard kb)
    {
        if (kb != null && kb.status == TouchScreenKeyboard.Status.Visible && kb.text != null) return kb.text;
        return lastTyped ?? "";
    }

    private void CloseNow(string final, InputField src, bool submit)
    {
        SyncMirrorToSource(final, src);
        if (mirrorInput != null && mirrorInput.isFocused)
            mirrorInput.DeactivateInputField();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        HideMirror();
        if (submit && src != null && submitActions.TryGetValue(src, out var send)) send?.Invoke();
    }

    /// <summary>
    /// 마지막 글을 activeSource에 강제 반영.
    /// source.enabled=false 상태에서 텍스트가 안 들어가던 문제(InstagramAccountInput 등) 해결:
    /// - 일시적으로 enabled=true로 풀어 setter가 정상 동작하게 함
    /// - ForceLabelUpdate로 라벨 즉시 갱신
    /// - 외부 구독자(저장 버튼 등) 알림 위해 onEndEdit 명시 호출
    /// </summary>
    private void SyncMirrorToSource(string finalText, InputField src)
    {
        if (src == null) return;
        finalText = finalText ?? "";
        if (src.characterLimit > 0 && finalText.Length > src.characterLimit)
            finalText = finalText.Substring(0, src.characterLimit);
        syncing = true;
        bool wasEnabled = src.enabled;
        if (!wasEnabled) src.enabled = true;

        src.text = finalText;
        src.ForceLabelUpdate();

        if (!wasEnabled) src.enabled = false;
        syncing = false;

        // 외부 코드가 onEndEdit을 구독했을 경우 알림 (예: 업로드 매니저 저장 흐름)
        src.onEndEdit?.Invoke(finalText);
    }

    private void HideMirror()
    {
        activeSource = null;
        lastKb = null;
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
            if (cachedSwipeControllers[i] != null) cachedSwipeControllers[i].locked = true;   // 끄면 다시 켤 때 첫 카드('장소')로 돌아간다
        }
    }

    private void RestoreSwipeControllers()
    {
        if (cachedSwipeControllers == null) return;
        for (int i = 0; i < cachedSwipeControllers.Length; i++)
        {
            if (cachedSwipeControllers[i] != null) cachedSwipeControllers[i].locked = false;
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
        if (mirrorInput.touchScreenKeyboard != null) lastKb = mirrorInput.touchScreenKeyboard;

        // 미러 활성 중에는 SwipeController 매 프레임 강제 비활성
        if (cachedSwipeControllers != null)
        {
            for (int i = 0; i < cachedSwipeControllers.Length; i++)
            {
                if (cachedSwipeControllers[i] != null && !cachedSwipeControllers[i].locked)
                    cachedSwipeControllers[i].locked = true;
            }
        }

        UpdateMirrorPosition();
        UpdateBackdropRect();
        if (mirrorGroup != null)
        {
            bool late = mirrorActivatedAt > 0f && Time.unscaledTime - mirrorActivatedAt > SHOW_FALLBACK;
            float want = placed || late ? 1f : 0f;
            if (mirrorGroup.alpha != want) mirrorGroup.alpha = Mathf.MoveTowards(mirrorGroup.alpha, want, Time.unscaledDeltaTime / 0.12f);
        }

        bool inGrace = mirrorActivatedAt > 0f && (Time.unscaledTime - mirrorActivatedAt) < KEYBOARD_GRACE;

        float kbCanvas = GetKeyboardHeightCanvas();
        bool kbUp = kbCanvas > 0f;

        if (kbUp) keyboardActive = true;
        else if (keyboardActive && !inGrace && (closePress == null || !closePress.Down))
        {
            BeginClose(false);
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

        if (!placed)
        {
            // 처음엔 바로 그 자리 — 아래에서 따라 올라오지 않게
            mirrorRect.anchorMin = targetMin;
            mirrorRect.anchorMax = targetMax;
            placed = true;
            return;
        }
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

/// <summary>입력줄 버튼을 누르고 있는 중인지 — 떼는 순간 클릭이 먼저 오고, 끌어서 벗어나면 그냥 풀린다</summary>
public class MirrorButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool Down { get; private set; }
    public void OnPointerDown(PointerEventData e) { Down = true; }
    public void OnPointerUp(PointerEventData e) { Down = false; }
    void OnDisable() { Down = false; }
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
