using UnityEngine;
using System.IO;
using System;

public static class FileLogger
{
    private static string logPath => Path.Combine(Application.persistentDataPath, "woopang_debug_log.txt");

    public static void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string finalMessage = $"[{timestamp}] {message}\n";

        // 에디터 로그에도 표시
        Debug.Log(message);

        try
        {
            // 파일에 이어쓰기 (Append)
            File.AppendAllText(logPath, finalMessage);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to write log: {ex.Message}");
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(logPath)) File.Delete(logPath);
        }
        catch { }
    }
}