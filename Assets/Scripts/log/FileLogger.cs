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

    private bool isLogging = false;
    private string currentLogPath;
    private StringBuilder buffer = new StringBuilder(4096);
    private float lastFlushTime;
    private const float FLUSH_INTERVAL = 5f; // 5초마다 디스크에 기록

    // 로그 통계
    private int logCount;
    private DateTime sessionStartTime;

    // 자체 생성 UI 참조
    private GameObject uiRoot;
    private Text toggleButtonText;
    private Image toggleButtonImage;

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

    void Start()
    {
        CreateUI();
    }

    /// <summary>
    /// 독립 Canvas + 버튼 2개를 런타임에 자체 생성
    /// VersionButton_Open 등 외부 오브젝트에 의존하지 않음
    /// </summary>
    private void CreateUI()
    {
        if (uiRoot != null) return;

        // Canvas 생성
        uiRoot = new GameObject("FileLoggerUI");
        uiRoot.transform.SetParent(transform);

        Canvas canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 항상 최상위

        CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        uiRoot.AddComponent<GraphicRaycaster>();

        // 버튼 컨테이너 (좌측 하단)
        GameObject container = new GameObject("Container", typeof(RectTransform));
        container.transform.SetParent(uiRoot.transform, false);
        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0);
        containerRect.anchorMax = new Vector2(0, 0);
        containerRect.pivot = new Vector2(0, 0);
        containerRect.anchoredPosition = new Vector2(20, 20);
        containerRect.sizeDelta = new Vector2(260, 160);

        // 폰트 로드
        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");

        // LOG START/STOP 토글 버튼
        GameObject toggleObj = CreateButton(container.transform, "ToggleBtn", "LOG START",
            new Vector2(0, 80), new Vector2(260, 70),
            new Color(0.2f, 0.7f, 0.3f, 1f), customFont);
        Button toggleBtn = toggleObj.GetComponent<Button>();
        toggleBtn.onClick.AddListener(ToggleLogging);
        toggleButtonText = toggleObj.GetComponentInChildren<Text>();
        toggleButtonImage = toggleObj.GetComponent<Image>();

        // SHARE LOG 버튼
        GameObject shareObj = CreateButton(container.transform, "ShareBtn", "SHARE LOG",
            new Vector2(0, 0), new Vector2(260, 70),
            new Color(0.3f, 0.5f, 0.9f, 1f), customFont);
        Button shareBtn = shareObj.GetComponent<Button>();
        shareBtn.onClick.AddListener(ShareLatestLog);

        // 기본 비표시 — VersionButton_Open 열릴 때 함께 표시하거나, 항상 표시
        // 테스트 편의상 항상 표시
        uiRoot.SetActive(true);
    }

    private GameObject CreateButton(Transform parent, string name, string label,
        Vector2 pos, Vector2 size, Color bgColor, Font font)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.layer = 5;
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.1f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;
        btn.targetGraphic = img;

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.layer = 5;
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        if (font != null) text.font = font;

        return btnObj;
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
        if (toggleButtonText != null)
            toggleButtonText.text = isLogging ? "LOG STOP" : "LOG START";

        if (toggleButtonImage != null)
        {
            Color c = isLogging ? new Color(0.8f, 0.2f, 0.2f, 1f) : new Color(0.2f, 0.7f, 0.3f, 1f);
            toggleButtonImage.color = c;

            // Button ColorBlock도 동기화
            Button btn = toggleButtonImage.GetComponent<Button>();
            if (btn != null)
            {
                var colors = btn.colors;
                colors.normalColor = c;
                colors.highlightedColor = c * 1.1f;
                colors.pressedColor = c * 0.8f;
                btn.colors = colors;
            }
        }
    }
}
