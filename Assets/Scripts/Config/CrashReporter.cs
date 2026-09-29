using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 앱에서 난 예외(Exception)를 우팡 서버(/api/crash)로 보낸다 — Crashlytics SDK 없이 가볍게.
/// 같은 오류는 세션당 한 번, 세션당 최대 15건. 서버가 같은 오류를 묶어 두고, 처음 보는 오류일 때만 텔레그램으로 알린다.
/// 개인 정보(로그인 id·위치)는 보내지 않는다. 에디터에서는 보내지 않는다.
/// </summary>
public class CrashReporter : MonoBehaviour
{
    private const int MaxPerSession = 15;
    private static readonly ConcurrentQueue<string[]> queue = new ConcurrentQueue<string[]>();
    private static readonly HashSet<int> sent = new HashSet<int>();
    private static int count;

    [Serializable]
    private class Report
    {
        public string message;
        public string stack;
        public string version;
        public string platform;
        public string device;
        public string os;
        public string scene;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor) return;
        var go = new GameObject("CrashReporter");
        DontDestroyOnLoad(go);
        go.AddComponent<CrashReporter>();
        Application.logMessageReceivedThreaded += OnLog;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Exception) return;
        queue.Enqueue(new[] { message ?? "", stack ?? "" });
    }

    private void Update()
    {
        while (queue.TryDequeue(out var e))
        {
            int key = (e[0] + "|" + Head(e[1], 3)).GetHashCode();
            if (!sent.Add(key) || count >= MaxPerSession) continue;
            count++;
            StartCoroutine(Send(e[0], e[1]));
        }
    }

    private static string Head(string stack, int lines)
    {
        var sb = new StringBuilder();
        int n = 0;
        foreach (var l in stack.Split('\n'))
        {
            if (l.Trim().Length == 0) continue;
            sb.Append(l.Trim()).Append('\n');
            if (++n >= lines) break;
        }
        return sb.ToString();
    }

    private IEnumerator Send(string message, string stack)
    {
        var r = new Report
        {
            message = message.Length > 500 ? message.Substring(0, 500) : message,
            stack = stack.Length > 3000 ? stack.Substring(0, 3000) : stack,
            version = Application.version,
            platform = Application.platform.ToString(),
            device = SystemInfo.deviceModel,
            os = SystemInfo.operatingSystem,
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
        };
        using (var req = new UnityWebRequest(ApiConfig.MAIN_SERVER + "/api/crash", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(r)));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;
            yield return req.SendWebRequest();   // 실패해도 조용히 — 오류 보고가 또 오류를 만들면 안 된다
        }
    }
}
