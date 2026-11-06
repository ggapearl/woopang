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
// - source 를 눌렀다 떼면(MirrorSourceTap) → mirrorPanel.SetActive(true) + mirrorInput.text 동기화 + ActivateInputField
//   source 에서 시작한 끌기는 source 가 든 목록이 굴린다
// - mirrorInput.onValueChanged → source.text 동기화
// - 닫기 버튼 → mirrorPanel.SetActive(false), mirrorInput.DeactivateInputField
// - 키보드가 사라지면 자동으로 미러 패널도 닫음
// - 키보드가 끝내 안 뜨면(안드로이드가 '띄워'를 놓침) 닫았다 다시 부름 — 두 번까지, 그 뒤엔 입력줄을 누르면 (WatchKeyboard)
// - 키보드가 뜨기 전에 화면을 또 누르면 — 원래 칸이면 바로 다시 부르고, 다른 데면 닫음 (OnBarDeselected)
// - 키보드 위로 mirrorPanel 위치 lerp (UpdateMirrorPosition)
// - 미러 패널 영역 밖 터치/드래그는 backdrop이 흡수해 뒷 UI 영향 차단
// - 장소 이름·설명 칸은 입력줄에 이모지 단추 — 누르면 키보드 자리에 이모지 창이 뜨고 입력줄은 그 위에 그대로 (IR0926KeyboardHost · 11-05)
// ============================================================
public class UploadInputMirror : MonoBehaviour, IR0926KeyboardHost
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

    [Header("이모지 (11-05 대표님 「'장소추가' 하는 탭의 텍스트입력필드에서도 '이모지'」) — 이 칸들만 입력줄에 이모지 단추")]
    [SerializeField] private InputField[] emojiSources;
    [SerializeField] private GameObject emojiKey;      // 입력칸 안 오른쪽 끝 단추 (R0926EmojiButton — 적용 단계가 만든다)
    [SerializeField] private float emojiInset = 60f;   // 단추가 있을 때 글·안내 글을 오른쪽에서 이만큼 덜 쓴다

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

    // 안드로이드 유니티 키보드는 키보드 창이 포커스를 얻는 순간 한 번만 키보드를 부르고, 안 떠도 다시 부르지 않는다.
    // 그 한 번이 묻히면 입력줄만 뜨고 키보드는 끝내 안 떴다 — 입력줄을 다시 눌러도 칸은 이미 키보드를 쥔 줄 안다
    // (2026-10-29 대표님: 장소 추가·3D 모델 칸 가끔 키보드 안 뜸) → 잠깐 기다려도 안 뜨면 닫았다 다시 부른다
    private const float KEYBOARD_WAIT = 1.5f;   // 느린 기기의 첫 키보드도 이 안에 뜬다
    private const int KEYBOARD_RETRIES = 2;     // 저절로는 여기까지 — 그 뒤엔 입력줄을 누르면
    private float kbAskedAt = -1f;              // 마지막으로 키보드를 부른 때 (-1 이면 안 지켜본다)
    private int kbRetries;
    private bool kbReopen;                      // 곧 다시 부른다
    private int kbReopenFrame;                  // 놓은 프레임 — 한 프레임 통째로 놓인 채 지나야 키보드(에디터 흉내도)가 닫힌 걸 안다
    private bool kbHeld;                        // 부른 뒤 입력칸이 키보드를 쥐었다 — 그 뒤 놓으면 기기 키보드가 닫힌 것
    private bool kbTyped;                       // 글이 들어왔다 — 높이를 못 읽어도 키보드는 떠 있다
    private bool kbNoted;
    private string kbContext = "";              // 부를 때 사정 (기기 기록용)
    private string kbFirst = "";                // 처음 다시 부를 때 유니티 쪽 키보드 (기기 기록용)
    private static float lastCloseAt = -100f;   // 입력줄을 마지막으로 닫은 때 (모든 입력줄)
    private static float appBackAt = -100f;     // 앱으로 돌아온 때 (사진 고르기·권한 창 등)
    private static int sdk = -1;
    private InputField queued;                  // 닫는 사이 누른 칸 — 다 닫고 연다

    // 이모지 창 (R0926EmojiPanel) — 키보드 자리에 떠 있는 동안 입력줄은 그 위에 두고, 키보드가 내려가도 닫지 않는다
    private const float AccessoryHandoff = 0.6f;   // 이모지 창 → 키보드: 키보드가 올라올 때까지 입력줄을 그 높이로 들고 기다리는 시간
    private bool accessoryActive;
    private float accessoryHeight;
    private float accessoryHandoffUntil = -1f;
    private float lastKbCanvas;                 // 마지막으로 본 키보드 높이 (캔버스 단위) — 이모지 창을 같은 높이로
    private MirrorButtonPress emojiPress;
    private float textRightBase = float.NaN, placeholderRightBase = float.NaN;

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
        queued = null;
        initialized = false;   // 다시 켜지면 듣기·트리거를 다시 단다 (예전엔 끈 뒤로 ✓·입력이 먹지 않았다)
    }

    /// <summary>지금 입력줄로 쓰고 있는 원래 칸 (닫혀 있거나 닫는 중이면 null) — R0926InputFocus 가 테두리·스크롤에 쓴다</summary>
    public InputField ActiveSource => mirrorPanel != null && mirrorPanel.activeSelf && !closing ? activeSource : null;
    /// <summary>키보드 위에 자리 잡은 입력줄 (아직이면 null)</summary>
    public RectTransform PlacedBar => placed ? mirrorRect : null;
    public bool IsMirrorInput(InputField f) => f != null && f == mirrorInput;
    /// <summary>이번에 키보드를 다시 부른 횟수 (시험용)</summary>
    public int KeyboardRetries => kbRetries;
    /// <summary>키보드 위 입력줄의 실제 입력칸 — R0926InputFocus 가 이 칸의 커서 자리·모양을 원래 칸 안에도 같게 보여 준다</summary>
    public InputField Bar => mirrorInput;
    /// <summary>입력줄이나 그 위 막(backdrop) — 아래로 쓸어내리면 닫는 자리 (R0926KeyboardSwipe)</summary>
    public bool Covers(Transform t) => t != null && (mirrorPanel != null && t.IsChildOf(mirrorPanel.transform) || backdrop != null && t.IsChildOf(backdrop.transform));

    /// <summary>입력줄을 아래로 쓸어내렸다 (R0926KeyboardSwipe) — 기기 키보드를 내린 것과 같게 글을 원래 칸에 옮기고 닫는다 (보내기 칸이어도 보내지 않는다 · 엔터를 쳤으면 보낸다)</summary>
    public void CloseBySwipe() => BeginClose(false);

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
        if (emojiKey != null)
        {
            emojiPress = emojiKey.GetComponent<MirrorButtonPress>();
            if (emojiPress == null) emojiPress = emojiKey.AddComponent<MirrorButtonPress>();
        }
        var tap = mirrorInput.GetComponent<MirrorBarTap>();
        if (tap == null) tap = mirrorInput.gameObject.AddComponent<MirrorBarTap>();
        tap.onDown = OnBarTapped;
        tap.onDeselect = OnBarDeselected;

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
            // InputField 컴포넌트 disable → 키보드/포커스 차단. 눌렀다 떼는 것만 MirrorSourceTap 이 받는다
            src.enabled = false;
            AttachSourceTrigger(src);
        }
    }

    private void DetachSourceTriggers()
    {
        if (sourceInputs == null) return;
        for (int i = 0; i < sourceInputs.Length; i++)
        {
            var tap = sourceInputs[i] != null ? sourceInputs[i].GetComponent<MirrorSourceTap>() : null;
            if (tap != null) tap.onTap = null;
        }
    }

    // 예전엔 누르는 순간(EventTrigger PointerDown) 열었다 — EventTrigger 가 끌기까지 가로채, 칸에서 시작한 스와이프는 목록을 못 굴리고
    // 입력줄·키보드만 떴다 (2026-11-03 대표님: 프로필 편집에서 칸을 짚고 내리려 하면 키패드가 뜸). 이제 눌렀다 뗄 때만 연다
    private void AttachSourceTrigger(InputField src)
    {
        var tap = src.GetComponent<MirrorSourceTap>();
        if (tap == null) tap = src.gameObject.AddComponent<MirrorSourceTap>();
        InputField captured = src;
        tap.onTap = () => OnSourceClicked(captured);
    }

    private void OnSourceClicked(InputField src)
    {
        if (closing) { queued = src; return; }   // 닫는 두 프레임 사이 — 다 닫고 연다
        if (src == activeSource) return;         // 키보드를 기다리다 같은 칸을 다시 누른 것 — 누르는 순간 OnBarDeselected 가 다시 불렀다
        ActivateMirrorFor(src);
    }

    private void ActivateMirrorFor(InputField source)
    {
        bool tookOver = ReleaseOtherInput();
        float now = Time.unscaledTime;
        kbContext = (tookOver ? "다른 칸 키보드를 넘겨받음" : now - lastCloseAt < 1.5f ? "앞 입력줄을 막 닫음" : "앞 키보드 없음")
                    + (now - appBackAt < 3f ? " · 앱으로 막 돌아옴" : "");
        activeSource = source;
        submitRequested = false;
        PaintCloseIcon(submitActions.ContainsKey(source));
        ShowEmojiKey(AllowsEmoji(source));
        accessoryActive = false;
        accessoryHandoffUntil = -1f;

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
            mirrorPlaceholder.text = string.IsNullOrEmpty(ph) ? R0926LocalizedText.Say("입력하세요", "Type here", "入力してください", "请输入", "Escribe aquí") : ph;
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
        kbAskedAt = mirrorActivatedAt;
        kbRetries = 0;
        kbReopen = kbHeld = kbTyped = kbNoted = false;
    }

    // 다른 입력칸(뒤에 열린 대화창 등)이 아직 키보드를 쥐고 있으면 먼저 놓게 한다 — 두 칸이 한 키보드를 나눠 쓰면
    // 친 글이 그쪽으로 가거나 지워졌다 (2026-10-14 안드로이드: 보물찾기 제목·힌트가 안 들어감). 놓게 했으면 true
    private bool ReleaseOtherInput()
    {
        var es = EventSystem.current;
        var sel = es != null ? es.currentSelectedGameObject : null;
        var other = sel != null ? sel.GetComponent<InputField>() : null;
        if (other == null || other == mirrorInput) return false;
        if (other is KeyboardPersistentInputField kp) kp.lockFocus = false;
        if (!other.isFocused) return false;
        other.DeactivateInputField();
        return true;
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
        if (!accessoryActive && kb != null && kb.status != TouchScreenKeyboard.Status.Visible) return;   // 이모지 창이 넣은 글은 옮긴다
        kbTyped = true;
        lastTyped = value ?? "";
        syncing = true;
        activeSource.text = lastTyped;
        syncing = false;
    }

    private void OnCloseClicked() => BeginClose(true);

    // 입력줄 단추(✓ · 이모지)를 누르고 있다 — 누르는 순간 입력칸이 놓여 키보드가 먼저 내려간다. 그 사이 저절로 닫지 않는다
    private bool ButtonHeld() => closePress != null && closePress.Down || emojiPress != null && emojiPress.Down;

    // ── 이모지 (IR0926KeyboardHost — R0926EmojiPanel 이 부른다) ─────────────────

    private bool AllowsEmoji(InputField src)
    {
        if (emojiSources == null || src == null) return false;
        foreach (var f in emojiSources) if (f == src) return true;
        return false;
    }

    private void ShowEmojiKey(bool on)
    {
        if (emojiKey == null || mirrorInput == null) return;
        if (emojiKey.activeSelf != on) emojiKey.SetActive(on);
        Inset(mirrorInput.textComponent, ref textRightBase, on);
        Inset(mirrorInput.placeholder, ref placeholderRightBase, on);
    }

    private void Inset(Graphic g, ref float baseX, bool on)
    {
        if (g == null) return;
        var rt = g.rectTransform;
        if (float.IsNaN(baseX)) baseX = rt.offsetMax.x;
        float want = on ? baseX - emojiInset : baseX;
        if (!Mathf.Approximately(rt.offsetMax.x, want)) rt.offsetMax = new Vector2(want, rt.offsetMax.y);
    }

    public bool AccessoryHolding => accessoryActive || accessoryHandoffUntil > 0f && Time.unscaledTime < accessoryHandoffUntil;
    public float LastKeyboardHeight => lastKbCanvas;

    /// <summary>
    /// 이모지 단추를 눌렀다 (창을 열기 전) — 누르는 순간 입력칸이 놓여 키보드가 내려가는 중이다.
    /// 조합 중이던 한글은 그때 확정되므로 키보드에 남은 글을 입력줄·원래 칸에 둔다 (닫을 때 CloseRoutine 이 하는 것과 같게)
    /// </summary>
    public void BeforeEmoji()
    {
        if (activeSource == null || closing || mirrorInput == null) return;
        var kb = mirrorInput.touchScreenKeyboard;
        if (kb == null) kb = lastKb;
        string t = lastTyped ?? "";
        if (kb != null && !string.IsNullOrEmpty(kb.text) && kb.status != TouchScreenKeyboard.Status.Canceled) t = kb.text;
        if (activeSource.characterLimit > 0 && t.Length > activeSource.characterLimit) t = t.Substring(0, activeSource.characterLimit);
        lastTyped = t;
        syncing = true;
        if (mirrorInput.text != t) mirrorInput.text = t;
        activeSource.text = t;
        syncing = false;
    }

    /// <summary>키보드를 내리고 그 자리(높이 h)에 이모지 창 — 입력줄은 그 위에 그대로</summary>
    public void ShowAccessory(float h)
    {
        if (activeSource == null || closing) return;
        accessoryActive = true;
        accessoryHeight = h;
        accessoryHandoffUntil = -1f;
        kbReopen = false;
        kbAskedAt = -1f;
        kbHeld = false;
        lastKb = null;   // 내려간 키보드의 글은 이제 옛 글 — 닫을 때 다시 읽으면 이모지가 지워진다
        if (mirrorInput.isFocused) mirrorInput.DeactivateInputField();
        var es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject == mirrorInput.gameObject) es.SetSelectedGameObject(null);
    }

    public void SetAccessoryHeight(float h, bool snap = false)
    {
        if (!accessoryActive) return;
        accessoryHeight = h;
        if (!snap || mirrorRect == null || canvasRect == null || canvasRect.rect.height <= 0f) return;
        float a = Mathf.Clamp01((h + keyboardTopPadding) / canvasRect.rect.height);   // 끌어내리는 손가락을 바로 따라간다
        mirrorRect.anchorMin = new Vector2(0f, a);
        mirrorRect.anchorMax = new Vector2(1f, a);
    }

    /// <summary>이모지 창을 내린다 — toKeyboard 면 키보드를 다시 부르고(올라올 때까지 입력줄은 그 높이), 아니면 키보드를 내린 것처럼 글을 옮기고 닫는다</summary>
    public void HideAccessory(bool toKeyboard)
    {
        if (!accessoryActive) return;
        accessoryActive = false;
        if (toKeyboard && activeSource != null && !closing && mirrorInput != null && mirrorInput.isActiveAndEnabled)
        {
            accessoryHandoffUntil = Time.unscaledTime + AccessoryHandoff;
            kbRetries = 0;
            AskKeyboard();
        }
        else
        {
            accessoryHandoffUntil = -1f;
            BeginClose(false);
        }
    }

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
        var next = queued;
        queued = null;
        CloseNow(final, src, pressed || submitRequested);
        if (next != null && next.gameObject.activeInHierarchy && isActiveAndEnabled) ActivateMirrorFor(next);
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
        if (activeSource != null)
        {
            lastCloseAt = Time.unscaledTime;
            if (kbRetries > 0 && !keyboardActive && !kbTyped) NoteKeyboard(kbRetries + "번 불러도 안 뜸");
        }
        activeSource = null;
        lastKb = null;
        keyboardActive = false;
        accessoryActive = false;
        accessoryHandoffUntil = -1f;
        mirrorActivatedAt = -1f;
        kbAskedAt = -1f;
        kbReopen = kbHeld = false;
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
                // 누르기 받는 부품 누락 검사 — InstagramAccountInput처럼 비활성 → 활성 전환 후 필요
                var tap = s.GetComponent<MirrorSourceTap>();
                if (tap == null || tap.onTap == null)
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

        if (accessoryActive) return;   // 이모지 창이 키보드 자리에 — 키보드를 기다리거나 내려갔다고 닫지 않는다
        if (kbReopen && !closing && Time.frameCount > kbReopenFrame + 1) { kbReopen = false; AskKeyboard(); }
        if (KeyboardDropped()) return;

        bool inGrace = mirrorActivatedAt > 0f && (Time.unscaledTime - mirrorActivatedAt) < KEYBOARD_GRACE
                       || kbAskedAt > 0f && Time.unscaledTime - kbAskedAt < KEYBOARD_GRACE;   // 다시 부른 뒤도 — 앞 키보드가 내려가는 사이에 닫지 않는다

        float kbCanvas = GetKeyboardHeightCanvas();
        bool kbUp = kbCanvas > 0f;
        if (kbUp) lastKbCanvas = kbCanvas;

        if (kbUp)
        {
            if (!keyboardActive && kbRetries > 0) NoteKeyboard(kbRetries + "번째에 뜸");
            keyboardActive = true;
        }
        else if (keyboardActive && !inGrace && !ButtonHeld() && !AccessoryHolding)
        {
            BeginClose(false);
        }
        else if (!keyboardActive) WatchKeyboard();
    }

    // 부른 키보드가 잠깐 기다려도 안 뜨면 닫았다 다시 부른다 — 안드로이드만 (에디터는 시험용 흉내: EditorVirtualKeyboard.DropShows)
    private void WatchKeyboard()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        if (closing || kbReopen || activeSource == null || kbAskedAt < 0f || kbTyped || kbRetries >= KEYBOARD_RETRIES) return;
        if (Time.unscaledTime - kbAskedAt < KEYBOARD_WAIT) return;
        if (ImeShown()) { kbAskedAt = -1f; return; }   // 떠 있는데 높이로 못 읽는 키보드 (떠 있는 키보드·화면 나누기)
        ReopenKeyboard();
#endif
    }

    // 입력줄을 눌렀는데 키보드가 없다 — 다시 부른다
    private void OnBarTapped()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        if (closing || kbReopen || activeSource == null || keyboardActive || kbTyped || ImeShown()) return;
        ReopenKeyboard();
#endif
    }

    // 키보드가 뜨기 전에 화면을 또 눌렀다 — 누르는 순간 입력칸이 놓이며 키보드 요청도 같이 취소된다. 예전엔 1.5초 뒤 저절로
    // 다시 불러, 다른 데를 눌러도 키보드가 떠올랐다 (기기 기록 '칸 놓침' 4건 — 2026-11-02 대표님 폰).
    // 원래 칸을 다시 누른 거면 바로 다시 부르고(몇 번째로 세지 않는다 — 기기가 놓친 게 아니다), 다른 데면 그만 쓰려는 것이라 닫는다
    private void OnBarDeselected(BaseEventData e)
    {
        if (!(e is PointerEventData p) || keyboardActive || closing || kbReopen || activeSource == null) return;
        var hit = p.pointerPressRaycast.gameObject;
        if (hit != null && mirrorPanel != null && hit.transform.IsChildOf(mirrorPanel.transform)) return;   // 입력줄의 단추 — 단추가 맡는다
        if (Over(activeSource.transform as RectTransform, p)) { kbReopen = true; kbReopenFrame = Time.frameCount; }
        else BeginClose(false);
    }

    private static bool Over(RectTransform rt, PointerEventData p)
    {
        if (rt == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, p.position, p.pressEventCamera, out var at)) return false;
        const float pad = 16f;   // 칸 가장자리를 살짝 비껴 눌러도
        var r = rt.rect;
        return at.x >= r.xMin - pad && at.x <= r.xMax + pad && at.y >= r.yMin - pad && at.y <= r.yMax + pad;
    }

    // 누르지 않았는데 입력칸이 키보드를 놓았다(선택은 그대로) — 기기 키보드가 닫혔다. 뒤로 가기·완료·친 뒤 내린 것이면 닫고,
    // 뜨기도 전에 닫힌 거면 기다리지 않고 바로 다시 부른다. 닫았으면 true
    private bool KeyboardDropped()
    {
        if (keyboardActive || closing || kbReopen || activeSource == null) return false;
        if (mirrorInput.isFocused) { kbHeld = true; return false; }
        var es = EventSystem.current;
        if (!kbHeld || es == null || es.currentSelectedGameObject != mirrorInput.gameObject) return false;
        kbHeld = false;
        if (mirrorInput.wasCanceled || submitRequested || kbTyped) { BeginClose(false); return true; }
        if (kbRetries < KEYBOARD_RETRIES) ReopenKeyboard();
        return false;
    }

    // 닫고 한 프레임 통째로 쉰 뒤 부른다 — 키보드(에디터 흉내도)가 닫힌 걸 알아야 새로 띄운다
    private void ReopenKeyboard()
    {
        kbRetries++;
        if (kbRetries == 1) kbFirst = KeyboardState();
        Debug.LogWarning($"[UpInputMirror] 키보드가 안 떠 다시 부릅니다 ({kbRetries}) — {kbFirst}");
        kbReopen = true;   // 아래에서 놓을 때 OnBarDeselected 가 또 다루지 않게 먼저
        kbReopenFrame = Time.frameCount;
        if (mirrorInput.isFocused) mirrorInput.DeactivateInputField();
        var es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject == mirrorInput.gameObject) es.SetSelectedGameObject(null);
    }

    private void AskKeyboard()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(mirrorInput.gameObject);
        mirrorInput.ActivateInputField();
        mirrorInput.caretPosition = mirrorInput.text.Length;
        keyboardActive = false;   // 앞 키보드가 아직 보여도 새 키보드로 치지 않는다
        kbHeld = false;
        kbAskedAt = Time.unscaledTime;
    }

    // 유니티 쪽 키보드 — 아예 안 불렀는지(하드웨어 키보드로 알면 안 부른다) / 불렀는데 안 떴는지
    private string KeyboardState()
    {
        var kb = mirrorInput.touchScreenKeyboard;
        var es = EventSystem.current;
        string lost = es != null && es.currentSelectedGameObject == mirrorInput.gameObject ? "칸 놓침·선택 그대로" : "칸 놓침·선택 풀림";
        string s = kb == null ? (mirrorInput.isFocused ? "칸만 잡힘·키보드 요청 없음" : lost) : "요청됨(" + kb.status + (kb.active ? "" : "·꺼짐") + ")";
        return TouchScreenKeyboard.isInPlaceEditingAllowed ? s + " · 하드웨어 키보드로 앎" : s;
    }

    // 실제 기기에서 언제 생기는지 모은다 — 앱 오류 수집(/api/crash, 기종·OS 가 같이 간다). 같은 글은 세션에 한 번
    private void NoteKeyboard(string result)
    {
        if (kbNoted) return;
        kbNoted = true;
        CrashReporter.Note($"[키보드] 안 떠서 다시 부름 — {result} · API {Sdk()} · {ImeName()} · {kbFirst} · {kbContext}");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void WatchAppFocus() => Application.focusChanged += f => { if (f) appBackAt = Time.unscaledTime; };

    private static int Sdk()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (sdk < 0)
            using (var v = new AndroidJavaClass("android.os.Build$VERSION")) sdk = v.GetStatic<int>("SDK_INT");
#endif
        return sdk;
    }

    // 안드로이드 11+ — 키보드 창이 떠 있는지 창 인셋으로 본다 (높이로 못 읽는 떠 있는 키보드·화면 나누기). 모르면 false
    private static bool ImeShown()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (Sdk() < 30) return false;
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var win = act?.Call<AndroidJavaObject>("getWindow"))
            using (var decor = win?.Call<AndroidJavaObject>("getDecorView"))
            using (var insets = decor?.Call<AndroidJavaObject>("getRootWindowInsets"))
            using (var type = new AndroidJavaClass("android.view.WindowInsets$Type"))
                return insets != null && insets.Call<bool>("isVisible", type.CallStatic<int>("ime"));
        }
        catch (System.Exception) { return false; }
#else
        return false;
#endif
    }

    // 쓰는 키보드 앱 (삼성 키보드·Gboard 등) — 모르면 ?
    private static string ImeName()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var cr = act?.Call<AndroidJavaObject>("getContentResolver"))
            using (var sec = new AndroidJavaClass("android.provider.Settings$Secure"))
            {
                string id = cr != null ? sec.CallStatic<string>("getString", cr, "default_input_method") : null;
                if (!string.IsNullOrEmpty(id)) return id.Split('/')[0];
            }
        }
        catch (System.Exception) { }
#endif
        return "?";
    }

    private void UpdateMirrorPosition()
    {
        if (mirrorRect == null || canvasRect == null) return;
        // 입력줄 단추(✓ · 이모지)를 누르고 있는 동안은 제자리 — 누르는 순간 키보드가 내려가 입력줄이 따라 내려가면, 손을 뗄 때 단추 밖이라 눌리지 않는다
        if (placed && ButtonHeld()) return;

        float kbCanvas = GetKeyboardHeightCanvas();
        if (accessoryActive) kbCanvas = accessoryHeight;                                  // 이모지 창 위
        else if (AccessoryHolding) kbCanvas = Mathf.Max(kbCanvas, accessoryHeight);        // 키보드가 다 올라올 때까지 그 높이
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

/// <summary>입력줄 칸을 눌렀다 — 키보드가 안 떠 있으면 다시 부른다 · 칸이 놓였다(어디를 눌러서인지는 onDeselect 가 본다) (UploadInputMirror)</summary>
public class MirrorBarTap : MonoBehaviour, IPointerDownHandler, IDeselectHandler
{
    public System.Action onDown;
    public System.Action<BaseEventData> onDeselect;
    public void OnPointerDown(PointerEventData e) => onDown?.Invoke();
    public void OnDeselect(BaseEventData e) => onDeselect?.Invoke(e);
}

/// <summary>
/// 원래 칸을 눌렀다 뗐다 — 끌기 받는 부품이 아니라서 칸에서 시작한 끌기는 칸이 든 목록이 굴린다 (UploadInputMirror).
/// 끌기 받는 목록이 없는 칸도 4mm 넘게 민 것은 누른 게 아니다 (페이지 넘기기 등)
/// </summary>
public class MirrorSourceTap : MonoBehaviour, IPointerClickHandler
{
    public System.Action onTap;

    public void OnPointerClick(PointerEventData e)
    {
        if (e.dragging) return;
        float slop = Screen.dpi > 0f ? Screen.dpi * 0.16f : 40f;
        if ((e.position - e.pressPosition).sqrMagnitude > slop * slop) return;
        onTap?.Invoke();
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
