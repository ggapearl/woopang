using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 업로드 페이지 InputField가 모바일 키보드와 겹치지 않도록
// contentRoot를 위로 shift. focused InputField의 bottom edge가
// 키보드 top과 딱 붙게 이동. DM 채팅용 MobileKeyboardHandler의
// 업로드 전용 경량 버전 (확장/스크롤 없음, 단순 수직 이동만).
// ============================================================
public class UploadInputKeyboardShift : MonoBehaviour
{
    [Header("대상 설정")]
    [Tooltip("위로 shift할 컨텐츠 루트 (null이면 이 GameObject의 RectTransform 사용)")]
    [SerializeField] private RectTransform contentRoot;

    [Tooltip("감시할 InputField 목록 (비어있으면 자식에서 자동 탐색)")]
    [SerializeField] private InputField[] monitoredInputs;

    [Header("동작 설정")]
    [Tooltip("input bottom과 keyboard top 간 여백 (0=딱 붙음, 음수=겹침)")]
    [SerializeField] private float bottomGap = 0f;

    [Tooltip("iOS 키보드 위 시스템 toolbar(완료/취소 자동 표시 영역) 보정 (px). area.y 기반 계산으로 자동 처리되지만, 디바이스별 미세조정 필요시 사용")]
    [SerializeField] private float iosToolbarCompensation = 0f;

    [Header("Editor 시뮬레이션 (iOS 키보드 미리보기)")]
    [Tooltip("Editor에서 InputField가 포커스되면 이 값만큼 가짜 키보드 높이로 시뮬레이션 (실제 빌드는 무시)")]
    [SerializeField] private float editorSimKeyboardHeight = 0f;
    [Tooltip("Editor에서 항상 시뮬레이션 활성화 (포커스 없어도). 빠른 미리보기용.")]
    [SerializeField] private bool editorSimAlways = false;

    [Tooltip("shift 애니메이션 속도 (클수록 빠름)")]
    [SerializeField] private float lerpSpeed = 12f;

    [Tooltip("최대 shift 거리 제한 (px, 0이면 제한없음)")]
    [SerializeField] private float maxShiftPx = 0f;

    // ─── 상태 ───
    private Vector2 origAnchoredPos;
    private bool initialized;
    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private float targetShift;
    private int baselineVisibleBottom = -1;
    private float lastJniErrorTime;

    void OnEnable()
    {
        TryInitialize();
        targetShift = 0f;
        if (initialized) contentRoot.anchoredPosition = origAnchoredPos;
    }

    void OnDisable()
    {
        if (!initialized || contentRoot == null) return;
        targetShift = 0f;
        contentRoot.anchoredPosition = origAnchoredPos;
    }

    private void TryInitialize()
    {
        if (initialized) return;
        if (contentRoot == null) contentRoot = transform as RectTransform;
        if (contentRoot == null) return;

        origAnchoredPos = contentRoot.anchoredPosition;
        parentCanvas = contentRoot.GetComponentInParent<Canvas>();
        if (parentCanvas != null) canvasRect = parentCanvas.GetComponent<RectTransform>();

        if (monitoredInputs == null || monitoredInputs.Length == 0)
            monitoredInputs = contentRoot.GetComponentsInChildren<InputField>(true);

        initialized = true;
    }

    void Update()
    {
        if (!initialized) { TryInitialize(); if (!initialized) return; }

        float kbH = GetKeyboardHeightCanvas();
        InputField focused = GetFocusedMonitoredInput();

        if (kbH > 0f && focused != null)
        {
            float required = ComputeRequiredShift(focused, kbH);
            if (maxShiftPx > 0f) required = Mathf.Min(required, maxShiftPx);
            targetShift = Mathf.Max(0f, required);
        }
        else
        {
            targetShift = 0f;
        }

        float current = contentRoot.anchoredPosition.y - origAnchoredPos.y;
        float next = Mathf.Lerp(current, targetShift, Time.unscaledDeltaTime * lerpSpeed);
        contentRoot.anchoredPosition = new Vector2(origAnchoredPos.x, origAnchoredPos.y + next);
    }

    private InputField GetFocusedMonitoredInput()
    {
        if (monitoredInputs == null) return null;
        for (int i = 0; i < monitoredInputs.Length; i++)
        {
            var inp = monitoredInputs[i];
            if (inp != null && inp.isFocused && inp.gameObject.activeInHierarchy) return inp;
        }
        return null;
    }

    private float ComputeRequiredShift(InputField inp, float keyboardCanvasHeight)
    {
        if (canvasRect == null) return 0f;

        RectTransform inpRect = inp.transform as RectTransform;
        if (inpRect == null) return 0f;

        Vector3[] corners = new Vector3[4];
        inpRect.GetWorldCorners(corners);

        Vector2 localBottomLeft = canvasRect.InverseTransformPoint(corners[0]);
        float inputBottomY = localBottomLeft.y + canvasRect.rect.height * canvasRect.pivot.y;

        float keyboardTopY = keyboardCanvasHeight;
        return (keyboardTopY + bottomGap) - inputBottomY;
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
        // Editor 시뮬레이션 — Inspector에서 editorSimKeyboardHeight 설정 시 가짜 키보드 표시
        if (editorSimKeyboardHeight > 0f)
        {
            if (editorSimAlways) return editorSimKeyboardHeight;
            // 포커스된 InputField가 있을 때만 시뮬레이션 (실제 동작에 가깝게)
            if (GetFocusedMonitoredInput() != null) return editorSimKeyboardHeight;
        }
        return 0f;
#elif UNITY_IOS
        // iOS: TouchScreenKeyboard.area의 좌표는 좌하단 원점.
        // 키보드 top 위치 = area.y + area.height (좌하단 기준)
        // 화면에서 키보드가 차지하는 높이 = Screen.height - area.y
        // → toolbar 포함된 진짜 키보드 영역 높이.
        Rect area = TouchScreenKeyboard.area;
        if (area.height <= 0) return 0f;
        float kbHeight = Screen.height - area.y;
        return Mathf.Max(area.height, kbHeight) + iosToolbarCompensation;
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
                Debug.LogWarning($"[UploadKbShift] JNI 오류: {e.GetType().Name}: {e.Message}");
                lastJniErrorTime = Time.time;
            }
        }
        return 0f;
#else
        return 0f;
#endif
    }

    // ─── 외부 API ───
    public void SetMonitoredInputs(InputField[] inputs)
    {
        monitoredInputs = inputs;
    }
}
