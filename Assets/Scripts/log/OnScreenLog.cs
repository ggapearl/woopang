using UnityEngine;
using UnityEngine.UI;

public class OnScreenLog : MonoBehaviour
{
    public Text logText; // UI Text 오브젝트
    private string logContent = "";

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
        logContent += $"[{System.DateTime.Now}] [{type}] {logString}\n";
        if (type == LogType.Error || type == LogType.Exception)
        {
            logContent += stackTrace + "\n";
        }

        // 로그가 너무 길어지지 않도록 제한
        if (logContent.Length > 5000)
        {
            logContent = logContent.Substring(logContent.Length - 5000);
        }

        if (logText != null)
        {
            logText.text = logContent;
        }
    }
}