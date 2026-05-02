using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 외부 테스트용 디버그 로그 캡처 UI
// - Start Log 버튼: [BG-iOS-DBG] 로그 수집 시작/중지
// - Copy Log 버튼: 수집한 로그를 GUIUtility.systemCopyBuffer로 복사
// - 화면 중앙 하단에 부착 (위치/사이즈는 Inspector에서 조정 가능)
// ============================================================
public class DebugLogCaptureUI : MonoBehaviour
{
    [Header("UI 참조 (에디터 스크립트가 자동 연결)")]
    [SerializeField] private Button startStopButton;
    [SerializeField] private Text startStopButtonLabel;
    [SerializeField] private Button copyButton;
    [SerializeField] private Text statusLabel;

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

    void Awake()
    {
        if (autoStartOnAwake) StartCapture();
        UpdateUI();
    }

    void OnEnable()
    {
        if (startStopButton != null)
        {
            startStopButton.onClick.RemoveAllListeners();
            startStopButton.onClick.AddListener(ToggleCapture);
        }
        if (copyButton != null)
        {
            copyButton.onClick.RemoveAllListeners();
            copyButton.onClick.AddListener(CopyLogsToClipboard);
        }
    }

    void OnDestroy()
    {
        if (_isCapturing) Application.logMessageReceivedThreaded -= HandleLog;
    }

    public void ToggleCapture()
    {
        if (_isCapturing) StopCapture();
        else StartCapture();
    }

    public void StartCapture()
    {
        if (_isCapturing) return;
        _capturedLogs.Clear();
        Application.logMessageReceivedThreaded += HandleLog;
        _isCapturing = true;
        UpdateUI();
    }

    public void StopCapture()
    {
        if (!_isCapturing) return;
        Application.logMessageReceivedThreaded -= HandleLog;
        _isCapturing = false;
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
        if (!string.IsNullOrEmpty(logPrefixFilter) && !condition.Contains(logPrefixFilter))
            return;

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
            // 캡처 중일 때만 라인 카운트 갱신 (매 프레임)
            statusLabel.text = $"Capturing... {_capturedLogs.Count} lines";
        }
    }

    private void UpdateUI()
    {
        if (startStopButtonLabel != null)
            startStopButtonLabel.text = _isCapturing ? "Stop Log" : "Start Log";

        if (statusLabel != null)
            statusLabel.text = _isCapturing ? $"Capturing... {_capturedLogs.Count} lines" : $"Stopped ({_capturedLogs.Count} lines)";

        if (copyButton != null)
            copyButton.interactable = _capturedLogs.Count > 0;
    }
}
