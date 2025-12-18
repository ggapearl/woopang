using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text;
using System.Diagnostics;

/// <summary>
/// 스크롤 성능 및 이벤트 디버거
/// ListPanel의 스크롤 문제 원인 파악용
/// </summary>
public class ScrollDebugger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("디버깅 설정")]
    [SerializeField] private bool enableDebug = true;
    [SerializeField] private bool logToFile = false;
    [SerializeField] private int maxLogCount = 100;

    private Stopwatch frameTimer = new Stopwatch();
    private StringBuilder logBuilder = new StringBuilder();
    private int logCount = 0;
    private float lastLogTime = 0f;

    // 성능 측정
    private float dragStartTime;
    private int frameCount;
    private float totalFrameTime;

    // 이벤트 추적
    private bool isPointerDown = false;
    private bool isDragging = false;
    private Vector2 lastPosition;

    void Start()
    {
        if (enableDebug)
        {
            UnityEngine.Debug.Log("[ScrollDebug] 디버거 시작 - 스크롤 성능 모니터링 활성화");
            InvokeRepeating(nameof(LogPerformanceStats), 1f, 1f);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        frameTimer.Restart();

        if (enableDebug)
        {
            Log($"[DOWN] Pos={eventData.position}, Time={Time.realtimeSinceStartup:F3}");
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;

        if (enableDebug)
        {
            Log($"[UP] Pos={eventData.position}, Time={Time.realtimeSinceStartup:F3}");
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        dragStartTime = Time.realtimeSinceStartup;
        lastPosition = eventData.position;
        frameCount = 0;
        totalFrameTime = 0f;

        frameTimer.Restart();

        if (enableDebug)
        {
            Log($"[BEGIN_DRAG] Pos={eventData.position}, Delta={eventData.delta}");
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        frameTimer.Stop();
        float frameTime = (float)frameTimer.Elapsed.TotalMilliseconds;
        frameTimer.Restart();

        frameCount++;
        totalFrameTime += frameTime;

        Vector2 delta = eventData.position - lastPosition;
        lastPosition = eventData.position;

        // 프레임 시간이 16ms(60fps) 이상이면 경고
        if (frameTime > 16f)
        {
            Log($"[DRAG_LAG] FrameTime={frameTime:F1}ms (>16ms!), Delta={delta}, Pos={eventData.position}");
        }
        else if (enableDebug && logCount < maxLogCount)
        {
            Log($"[DRAG] FrameTime={frameTime:F1}ms, Delta={delta}");
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        float totalDragTime = Time.realtimeSinceStartup - dragStartTime;
        float avgFrameTime = frameCount > 0 ? totalFrameTime / frameCount : 0f;
        float fps = avgFrameTime > 0 ? 1000f / avgFrameTime : 0f;

        if (enableDebug)
        {
            Log($"[END_DRAG] TotalTime={totalDragTime:F3}s, Frames={frameCount}, AvgFrame={avgFrameTime:F1}ms, FPS={fps:F1}");
        }

        // 성능 경고
        if (avgFrameTime > 16f)
        {
            UnityEngine.Debug.LogWarning($"[ScrollDebug] 스크롤 성능 저하! 평균 프레임 시간: {avgFrameTime:F1}ms (목표: 16ms)");
        }
    }

    private void LogPerformanceStats()
    {
        if (!enableDebug) return;

        // 메모리 사용량
        long totalMemory = System.GC.GetTotalMemory(false);
        float memoryMB = totalMemory / (1024f * 1024f);

        // EventSystem 상태
        EventSystem eventSystem = EventSystem.current;
        string eventSystemStatus = eventSystem != null ? "Active" : "Missing!";

        // Raycaster 개수
        GraphicRaycaster[] raycasters = FindObjectsOfType<GraphicRaycaster>();

        Log($"[STATS] Memory={memoryMB:F1}MB, EventSystem={eventSystemStatus}, Raycasters={raycasters.Length}, FPS={1f / Time.deltaTime:F0}");
    }

    private void Log(string message)
    {
        if (!enableDebug || logCount >= maxLogCount) return;

        float currentTime = Time.realtimeSinceStartup;
        string logMessage = $"[{currentTime:F3}] {message}";

        UnityEngine.Debug.Log($"[ScrollDebug] {logMessage}");

        if (logToFile)
        {
            logBuilder.AppendLine(logMessage);
        }

        logCount++;

        lastLogTime = currentTime;
    }

    void OnDestroy()
    {
        if (logToFile && logBuilder.Length > 0)
        {
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, "scroll_debug.log");
            System.IO.File.WriteAllText(filePath, logBuilder.ToString());
            UnityEngine.Debug.Log($"[ScrollDebug] 로그 저장: {filePath}");
        }
    }

    /// <summary>
    /// EventSystem 충돌 검사
    /// </summary>
    [ContextMenu("Check EventSystem")]
    public void CheckEventSystem()
    {
        EventSystem[] eventSystems = FindObjectsOfType<EventSystem>();
        UnityEngine.Debug.Log($"[ScrollDebug] EventSystem 개수: {eventSystems.Length}");

        foreach (EventSystem es in eventSystems)
        {
            UnityEngine.Debug.Log($"  - {es.gameObject.name}: Enabled={es.enabled}");
        }

        // Raycaster 검사
        GraphicRaycaster[] raycasters = FindObjectsOfType<GraphicRaycaster>();
        UnityEngine.Debug.Log($"[ScrollDebug] GraphicRaycaster 개수: {raycasters.Length}");

        foreach (GraphicRaycaster rc in raycasters)
        {
            UnityEngine.Debug.Log($"  - {rc.gameObject.name}: Enabled={rc.enabled}, BlockingObjects={rc.blockingObjects}");
        }
    }
}
