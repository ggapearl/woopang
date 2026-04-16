using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Text;

/// <summary>
/// 디바이스 내부 저장소에 로그를 파일로 기록하는 시스템
/// 빠른 이동(지하철/자동차) 테스트 시 GPS 변화, 캐시 갱신, 스폰/디스폰 이벤트 등을 기록
/// 기록된 파일은 persistentDataPath/logs/ 에 저장됨
/// </summary>
public class FileLogger : MonoBehaviour
{
    public static FileLogger Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private bool autoStartOnLaunch = false;
    [SerializeField] private int maxFileSizeKB = 2048; // 2MB 제한

    [Header("UI (테스트용)")]
    [SerializeField] private Text toggleButtonText;

    private bool isLogging = false;
    private string currentLogPath;
    private StringBuilder buffer = new StringBuilder(4096);
    private float lastFlushTime;
    private const float FLUSH_INTERVAL = 5f; // 5초마다 디스크에 기록

    // 로그 통계
    private int logCount;
    private DateTime sessionStartTime;

    public bool IsLogging => isLogging;
    public string CurrentLogPath => currentLogPath;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (autoStartOnLaunch)
            StartLogging("auto");
    }

    void Update()
    {
        if (!isLogging) return;

        // 주기적으로 버퍼를 디스크에 기록
        if (Time.realtimeSinceStartup - lastFlushTime > FLUSH_INTERVAL && buffer.Length > 0)
        {
            FlushBuffer();
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (isLogging && paused)
        {
            Log("SYSTEM", "앱 백그라운드 진입");
            FlushBuffer();
        }
        else if (isLogging && !paused)
        {
            Log("SYSTEM", "앱 포그라운드 복귀");
        }
    }

    void OnDestroy()
    {
        if (isLogging)
            StopLogging();
    }

    /// <summary>
    /// 로깅 시작. 새 파일 생성.
    /// </summary>
    public void StartLogging(string sessionName = "session")
    {
        if (isLogging)
        {
            Debug.LogWarning("[FileLogger] 이미 로깅 중");
            return;
        }

        string logDir = Path.Combine(Application.persistentDataPath, "logs");
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        currentLogPath = Path.Combine(logDir, $"woopang_{sessionName}_{timestamp}.log");

        sessionStartTime = DateTime.Now;
        logCount = 0;
        buffer.Clear();
        isLogging = true;
        lastFlushTime = Time.realtimeSinceStartup;

        // 헤더 기록
        buffer.AppendLine($"=== WOOPANG FileLog ===");
        buffer.AppendLine($"Session: {sessionName}");
        buffer.AppendLine($"Start: {sessionStartTime:yyyy-MM-dd HH:mm:ss}");
        buffer.AppendLine($"Device: {SystemInfo.deviceModel}");
        buffer.AppendLine($"OS: {SystemInfo.operatingSystem}");
        buffer.AppendLine($"Path: {currentLogPath}");
        buffer.AppendLine("========================");
        buffer.AppendLine();

        FlushBuffer();
        UpdateToggleButtonText();
        Debug.LogWarning($"[FileLogger] 로깅 시작: {currentLogPath}");
    }

    /// <summary>
    /// 로깅 중지. 버퍼 최종 기록.
    /// </summary>
    public void StopLogging()
    {
        if (!isLogging) return;

        TimeSpan elapsed = DateTime.Now - sessionStartTime;
        Log("SYSTEM", $"로깅 종료 — 총 {logCount}개 로그, {elapsed.TotalMinutes:F1}분 기록");
        FlushBuffer();
        isLogging = false;
        UpdateToggleButtonText();

        Debug.LogWarning($"[FileLogger] 로깅 중지: {currentLogPath} ({logCount}개 로그)");
    }

    /// <summary>
    /// 로그 항목 기록
    /// </summary>
    public void Log(string tag, string message)
    {
        if (!isLogging) return;

        logCount++;
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        buffer.AppendLine($"[{timestamp}] [{tag}] {message}");

        // 파일 크기 제한 체크
        if (logCount % 100 == 0)
        {
            try
            {
                if (File.Exists(currentLogPath) && new FileInfo(currentLogPath).Length > maxFileSizeKB * 1024)
                {
                    buffer.AppendLine($"[{timestamp}] [SYSTEM] 파일 크기 제한 도달 ({maxFileSizeKB}KB). 로깅 중지.");
                    FlushBuffer();
                    isLogging = false;
                    return;
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// GPS 위치 변화 로그 (FilterManager/DataManager에서 호출)
    /// </summary>
    public void LogGPS(float lat, float lon, string context = "")
    {
        Log("GPS", $"({lat:F6},{lon:F6}) {context}");
    }

    /// <summary>
    /// 배분 결과 로그
    /// </summary>
    public void LogAllocation(int totalCache, int fullCount, int indicatorCount, float radius)
    {
        Log("ALLOC", $"캐시={totalCache}, Full={fullCount}, Indicator={indicatorCount}, radius={radius}m");
    }

    /// <summary>
    /// 스폰/디스폰 이벤트
    /// </summary>
    public void LogSpawn(string type, string id, string name, bool isSpawn)
    {
        string action = isSpawn ? "SPAWN" : "DESPAWN";
        Log(action, $"[{type}] id={id}, name={name}");
    }

    /// <summary>
    /// 캐시 갱신 이벤트
    /// </summary>
    public void LogCacheRefresh(string manager, int count, float lat, float lon)
    {
        Log("CACHE", $"[{manager}] {count}개 갱신 at ({lat:F6},{lon:F6})");
    }

    private void FlushBuffer()
    {
        if (buffer.Length == 0) return;

        try
        {
            File.AppendAllText(currentLogPath, buffer.ToString());
            buffer.Clear();
            lastFlushTime = Time.realtimeSinceStartup;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FileLogger] 파일 기록 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 저장된 로그 파일 목록 반환
    /// </summary>
    public string[] GetLogFiles()
    {
        string logDir = Path.Combine(Application.persistentDataPath, "logs");
        if (!Directory.Exists(logDir)) return new string[0];
        return Directory.GetFiles(logDir, "*.log");
    }

    /// <summary>
    /// 특정 로그 파일의 내용 반환 (공유용)
    /// </summary>
    public string ReadLogFile(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return null; }
    }

    /// <summary>
    /// 가장 최근 로그 파일을 Android Share Intent로 공유
    /// </summary>
    public void ShareLatestLog()
    {
        // 로깅 중이면 먼저 플러시
        if (isLogging) FlushBuffer();

        string[] files = GetLogFiles();
        if (files.Length == 0)
        {
            Debug.LogWarning("[FileLogger] 공유할 로그 파일이 없습니다");
            return;
        }

        // 가장 최근 파일 (이름 정렬 → 마지막)
        System.Array.Sort(files);
        string latestFile = files[files.Length - 1];

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");

                string content = ReadLogFile(latestFile);
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), "WOOPANG Log");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), content);

                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "로그 공유"))
                {
                    currentActivity.Call("startActivity", chooser);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[FileLogger] 공유 실패: {ex.Message}");
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS: 클립보드에 로그 내용 복사
        string content = ReadLogFile(latestFile);
        if (!string.IsNullOrEmpty(content))
        {
            GUIUtility.systemCopyBuffer = content;
            Log("SYSTEM", "로그 내용이 클립보드에 복사되었습니다 — 메모/메시지 앱에 붙여넣기 하세요");
            FlushBuffer();
        }
#else
        Debug.LogWarning($"[FileLogger] 로그 파일 경로: {latestFile}");
#endif
    }

    /// <summary>
    /// 수동 로깅 토글 (버튼용)
    /// </summary>
    public void ToggleLogging()
    {
        if (isLogging)
            StopLogging();
        else
            StartLogging("manual");
    }

    private void UpdateToggleButtonText()
    {
        if (toggleButtonText == null) return;
        toggleButtonText.text = isLogging ? "LOG STOP" : "LOG START";

        // 버튼 배경색도 변경
        var img = toggleButtonText.transform.parent.GetComponent<Image>();
        if (img != null)
            img.color = isLogging ? new Color(0.8f, 0.2f, 0.2f, 1f) : new Color(0.2f, 0.7f, 0.3f, 1f);
    }
}
