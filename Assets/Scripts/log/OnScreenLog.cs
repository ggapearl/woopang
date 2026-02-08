using UnityEngine;
using UnityEngine.UI;
using System.Text;

public class OnScreenLog : MonoBehaviour
{
    public Text logText; // UI Text ������Ʈ
    private StringBuilder logBuffer = new StringBuilder(5120);
    private const int MaxLogLength = 5000;

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        logBuffer.Append($"[{System.DateTime.Now}] [{type}] {logString}\n");
        if (type == LogType.Error || type == LogType.Exception)
        {
            logBuffer.Append(stackTrace).Append("\n");
        }

        // 로그가 너무 길어지지 않도록 제한
        if (logBuffer.Length > MaxLogLength)
        {
            string trimmed = logBuffer.ToString(logBuffer.Length - MaxLogLength, MaxLogLength);
            logBuffer.Clear();
            logBuffer.Append(trimmed);
        }

        if (logText != null)
        {
            logText.text = logBuffer.ToString();
        }
    }
}