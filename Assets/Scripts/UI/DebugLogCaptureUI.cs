using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
// 외부 테스트용 디버그 로그 캡처 UI
// - Start Log 버튼: [BG-iOS-DBG] 로그 수집 시작/중지
// - Copy Log 버튼: 수집한 로그를 GUIUtility.systemCopyBuffer로 복사
// - 화면 중앙 하단에 부착 (위치/사이즈는 Inspector에서 조정 가능)
// ============================================================
public class DebugLogCaptureUI : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    [Header("UI 참조 (에디터 스크립트가 자동 연결)")]
    [SerializeField] private Button startStopButton;
    [SerializeField] private Text startStopButtonLabel;
    [SerializeField] private Image startStopButtonBg;
    [SerializeField] private Button copyButton;
    [SerializeField] private Text statusLabel;

    private static readonly Color ColorIdle = new Color(0.2f, 0.6f, 0.2f, 1f);
    private static readonly Color ColorRec = new Color(0.85f, 0.15f, 0.15f, 1f);

    [Header("필터 설정")]
    [Tooltip("이 prefix를 포함한 로그만 캡처. 비워두면 모든 로그 캡처")]
    [SerializeField] private string logPrefixFilter = "[BG-iOS-DBG]";

    [Tooltip("최대 캡처 라인 수 — 초과 시 가장 오래된 항목 제거")]
    [SerializeField] private int maxCapturedLines = 5000;

    [Header("동작 옵션")]
    [Tooltip("앱 시작 시 자동으로 캡처 시작")]
    [SerializeField] private bool autoStartOnAwake = false;

    private readonly List<string> _capturedLogs = new List<string>();
    private bool _isCapturing = false;
    private int _totalLogsSeen = 0;       // 필터 통과 전 전체 로그 카운트
    private int _filteredOutCount = 0;    // 필터로 걸러진 로그 카운트

    void Awake()
    {
        Debug.Log($"[LogCapUI] Awake. startStopButton={(startStopButton!=null)}, copyButton={(copyButton!=null)}, statusLabel={(statusLabel!=null)}, bg={(startStopButtonBg!=null)}, label={(startStopButtonLabel!=null)}");
        WireListeners();
        if (autoStartOnAwake) StartCapture();
        UpdateUI();
    }

    void OnEnable()
    {
        Debug.Log("[LogCapUI] OnEnable");
        WireListeners();
        UpdateUI();
    }

    void Start()
    {
        // Canvas/EventSystem/Raycaster 환경 진단
        var es = EventSystem.current;
        var canvas = GetComponentInParent<Canvas>();
        var gr = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
        Debug.Log($"[LogCapUI] Start env. EventSystem={(es!=null)}, Canvas={(canvas!=null?canvas.name:"null")}, GraphicRaycaster={(gr!=null)}");
        if (startStopButton != null)
        {
            var img = startStopButton.GetComponent<Image>();
            Debug.Log($"[LogCapUI] btn state. interactable={startStopButton.interactable}, image.raycastTarget={(img!=null && img.raycastTarget)}, listenerCount={startStopButton.onClick.GetPersistentEventCount()}");
        }
    }

    private void WireListeners()
    {
        if (startStopButton != null)
        {
            startStopButton.onClick.RemoveAllListeners();
            startStopButton.onClick.AddListener(OnStartButtonClicked);
            Debug.Log("[LogCapUI] startStopButton onClick AddListener 완료");
        }
        else
        {
            Debug.LogWarning("[LogCapUI] startStopButton == null — 씬 슬롯 비어있음!");
        }
        if (copyButton != null)
        {
            copyButton.onClick.RemoveAllListeners();
            copyButton.onClick.AddListener(OnCopyButtonClicked);
        }
    }

    private void OnStartButtonClicked()
    {
        Debug.Log("[LogCapUI] onClick(StartStop) 발화됨");
        ToggleCapture();
    }

    private void OnCopyButtonClicked()
    {
        Debug.Log("[LogCapUI] onClick(Copy) 발화됨");
        CopyLogsToClipboard();
    }

    // 패널 어디든 터치/클릭 들어오는지 진단 — Button 우회 안전망
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[LogCapUI] OnPointerDown 패널수신. target={(eventData.pointerCurrentRaycast.gameObject!=null?eventData.pointerCurrentRaycast.gameObject.name:"null")}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[LogCapUI] OnPointerClick 패널수신. target={(eventData.pointerCurrentRaycast.gameObject!=null?eventData.pointerCurrentRaycast.gameObject.name:"null")}");
        // 안전망: 버튼 onClick이 안 와도 패널에 클릭이 들어오면 토글
        if (eventData.pointerCurrentRaycast.gameObject != null &&
            (eventData.pointerCurrentRaycast.gameObject.name == "StartStopButton" ||
             eventData.pointerCurrentRaycast.gameObject.transform.parent != null &&
             eventData.pointerCurrentRaycast.gameObject.transform.parent.name == "StartStopButton"))
        {
            Debug.Log("[LogCapUI] 안전망: StartStopButton 클릭 감지 → ToggleCapture");
            ToggleCapture();
        }
    }

    void OnDestroy()
    {
        if (_isCapturing) Application.logMessageReceivedThreaded -= HandleLog;
    }

    public void ToggleCapture()
    {
        Debug.Log($"[LogCapUI] ToggleCapture() 호출됨. 현재 _isCapturing={_isCapturing}");
        if (_isCapturing) StopCapture();
        else StartCapture();
    }

    public void StartCapture()
    {
        if (_isCapturing) { Debug.Log("[LogCapUI] StartCapture 무시 (이미 캡처중)"); return; }
        _capturedLogs.Clear();
        _totalLogsSeen = 0;
        _filteredOutCount = 0;
        Application.logMessageReceivedThreaded += HandleLog;
        _isCapturing = true;
        Debug.Log($"[LogCapUI] StartCapture 시작. filter='{logPrefixFilter}'");
        UpdateUI();
    }

    public void StopCapture()
    {
        if (!_isCapturing) { Debug.Log("[LogCapUI] StopCapture 무시 (캡처중 아님)"); return; }
        Application.logMessageReceivedThreaded -= HandleLog;
        _isCapturing = false;
        Debug.Log($"[LogCapUI] StopCapture. 수집={_capturedLogs.Count} / 전체본것={_totalLogsSeen} / 필터제외={_filteredOutCount}");
        UpdateUI();
    }

    public void CopyLogsToClipboard()
    {
        StringBuilder sb = new StringBuilder();
        lock (_capturedLogs)
        {
            for (int i = 0; i < _capturedLogs.Count; i++)
                sb.AppendLine(_capturedLogs[i]);
        }
        string text = sb.ToString();
        GUIUtility.systemCopyBuffer = text;

        if (statusLabel != null)
            statusLabel.text = $"Copied {_capturedLogs.Count} lines";
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        System.Threading.Interlocked.Increment(ref _totalLogsSeen);

        if (!string.IsNullOrEmpty(logPrefixFilter) && !condition.Contains(logPrefixFilter))
        {
            System.Threading.Interlocked.Increment(ref _filteredOutCount);
            return;
        }

        lock (_capturedLogs)
        {
            _capturedLogs.Add(condition);
            if (_capturedLogs.Count > maxCapturedLines)
                _capturedLogs.RemoveAt(0);
        }
    }

    void Update()
    {
        if (_isCapturing && statusLabel != null)
        {
            // 캡처 중: kept / total 둘 다 표시 — 필터 문제 진단용
            bool blink = (Time.unscaledTime % 1f) < 0.5f;
            string dot = blink ? "<color=#FF3030>●</color> " : "  ";
            statusLabel.text = $"{dot}REC  {_capturedLogs.Count}/{_totalLogsSeen} lines";
            statusLabel.supportRichText = true;
        }
    }

    private void UpdateUI()
    {
        if (startStopButtonLabel != null)
            startStopButtonLabel.text = _isCapturing ? "■ STOP" : "● REC";

        if (startStopButtonBg != null)
            startStopButtonBg.color = _isCapturing ? ColorRec : ColorIdle;

        if (statusLabel != null)
        {
            statusLabel.supportRichText = true;
            statusLabel.text = _isCapturing
                ? $"<color=#FF3030>●</color> REC  {_capturedLogs.Count}/{_totalLogsSeen} lines"
                : $"Stopped ({_capturedLogs.Count}/{_totalLogsSeen} lines)";
        }

        if (copyButton != null)
            copyButton.interactable = _capturedLogs.Count > 0;
    }
}
