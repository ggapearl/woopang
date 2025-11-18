using System.Collections.Generic;
using UnityEngine;

public class DebugDisplay : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool showDebugInfo = true;
    public int maxLines = 100;
    public int fontSize = 16;
    public Color textColor = Color.white;
    public Color backgroundColor = new Color(0, 0, 0, 0.5f);
    
    [Header("Filter Keywords (Legacy)")]
    public List<string> filterKeywords = new List<string> 
    { 
        "[Model3DLoader]", 
        "[GLTFLoader]", 
        "3D 모델"
    };
    
    [Header("Enhanced Filter Keywords")]
    [Tooltip("쉼표로 구분된 키워드들 (예: 키워드1, 키워드2, 키워드3)")]
    [TextArea(3, 10)]
    public string filterKeywordsString = "GLTFast, 렌더러, Renderer, material, shader, Bounds, Valid, 머티리얼, Standard, Universal, Lit, 셰이더, URP, Built-in, enabled, activeInHierarchy, bounds.size, magnitude, 검증, Error, Warning, 무효, Invalid, null, 없음";
    
    [Header("Exclude Keywords")]
    [Tooltip("제외할 키워드들 (쉼표로 구분, 이 키워드가 포함된 로그는 무시됨)")]
    [TextArea(2, 8)]
    public string excludeKeywordsString = "latitude, longitude, altitude, username, instagram_id, pet_friendly, separate_restroom, created_at, id:, name:, status:, folder:, main_photo:, sub_photos:, model_url:, model_scale:, model_rotation, animation_name:, animation_speed:, animation_loop:, animation_auto_play:, model_format:, has_animation:";
    
    [Header("Anti-Spam Settings")]
    public float duplicateMessageCooldown = 1.0f; // 중복 메시지 방지 시간
    public int maxSameMessageCount = 3; // 같은 메시지 최대 허용 횟수
    
    [Header("Scrollbar Settings")]
    public float scrollbarWidthRatio = 0.08f; // 화면 폭의 비율로 세로 스크롤바 너비 설정 (4배 증가)
    
    private Queue<string> debugLines = new Queue<string>();
    private Dictionary<string, float> lastMessageTime = new Dictionary<string, float>();
    private Dictionary<string, int> messageCount = new Dictionary<string, int>();
    private GUIStyle textStyle;
    private GUIStyle bgStyle;
    private Texture2D bgTexture;
    private Vector2 scrollPosition;
    private bool isLogHandlerRegistered = false;
    private static DebugDisplay instance;
    private List<string> parsedKeywords = new List<string>();
    private List<string> parsedExcludeKeywords = new List<string>();
    private string lastKeywordString = "";
    private string lastExcludeKeywordString = "";
    
    void Awake()
    {
        // 싱글톤 패턴 적용
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        // 배경 텍스처 생성
        bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, backgroundColor);
        bgTexture.Apply();
        
        // 스타일 설정
        textStyle = new GUIStyle();
        textStyle.fontSize = fontSize;
        textStyle.normal.textColor = textColor;
        textStyle.wordWrap = true;
        textStyle.padding = new RectOffset(5, 5, 5, 5);
        
        bgStyle = new GUIStyle();
        bgStyle.normal.background = bgTexture;
        
        // Unity 로그 수신 등록 (중복 방지)
        RegisterLogHandler();
        
        // 키워드 파싱
        ParseKeywords();
        
        AddDebugLine("=== Debug Display Started ===");
        AddDebugLine($"=== Loaded {parsedKeywords.Count} filter keywords ===");
    }
    
    void ParseKeywords()
    {
        // Include 키워드 파싱
        if (filterKeywordsString != lastKeywordString)
        {
            parsedKeywords.Clear();
            
            if (!string.IsNullOrEmpty(filterKeywordsString))
            {
                string[] keywords = filterKeywordsString.Split(',');
                foreach (string keyword in keywords)
                {
                    string trimmed = keyword.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        parsedKeywords.Add(trimmed);
                    }
                }
            }
            
            lastKeywordString = filterKeywordsString;
        }
        
        // Exclude 키워드 파싱
        if (excludeKeywordsString != lastExcludeKeywordString)
        {
            parsedExcludeKeywords.Clear();
            
            if (!string.IsNullOrEmpty(excludeKeywordsString))
            {
                string[] excludeKeywords = excludeKeywordsString.Split(',');
                foreach (string keyword in excludeKeywords)
                {
                    string trimmed = keyword.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        parsedExcludeKeywords.Add(trimmed);
                    }
                }
            }
            
            lastExcludeKeywordString = excludeKeywordsString;
            
            // 런타임에서 키워드가 변경되었다면 로그 출력
            if (Application.isPlaying && (parsedKeywords.Count > 0 || parsedExcludeKeywords.Count > 0))
            {
                AddDebugLine($"=== Keywords updated: Include {parsedKeywords.Count}, Exclude {parsedExcludeKeywords.Count} ===");
            }
        }
    }
    
    void RegisterLogHandler()
    {
        if (!isLogHandlerRegistered)
        {
            Application.logMessageReceived += HandleLog;
            isLogHandlerRegistered = true;
        }
    }
    
    void OnDestroy()
    {
        if (isLogHandlerRegistered)
        {
            Application.logMessageReceived -= HandleLog;
            isLogHandlerRegistered = false;
        }
        
        if (bgTexture != null)
            Destroy(bgTexture);
            
        if (instance == this)
            instance = null;
    }
    
    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (!showDebugInfo) return;
        
        // 런타임에서 키워드 변경 체크
        ParseKeywords();
        
        // 스팸 방지: 중복 메시지 체크
        if (IsDuplicateMessage(logString))
            return;
        
        // 제외 키워드 체크 (우선순위)
        foreach (string excludeKeyword in parsedExcludeKeywords)
        {
            if (logString.Contains(excludeKeyword))
            {
                return; // 제외 키워드가 포함되면 로그 무시
            }
        }
        
        // 필터 키워드 확인 (Enhanced + Legacy)
        bool shouldShow = false;
        
        // Enhanced 키워드 체크
        foreach (string keyword in parsedKeywords)
        {
            if (logString.Contains(keyword))
            {
                shouldShow = true;
                break;
            }
        }
        
        // Legacy 키워드 체크 (백워드 호환성)
        if (!shouldShow)
        {
            foreach (string keyword in filterKeywords)
            {
                if (logString.Contains(keyword))
                {
                    shouldShow = true;
                    break;
                }
            }
        }
        
        if (shouldShow)
        {
            string prefix = "";
            Color logColor = textColor;
            
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    prefix = "[ERROR] ";
                    logColor = Color.red;
                    break;
                case LogType.Warning:
                    prefix = "[WARN] ";
                    logColor = Color.yellow;
                    break;
                default:
                    prefix = "[LOG] ";
                    break;
            }
            
            AddDebugLineInternal(prefix + logString, logColor);
        }
    }
    
    bool IsDuplicateMessage(string message)
    {
        float currentTime = Time.time;
        
        // 메시지 카운트 체크
        if (messageCount.ContainsKey(message))
        {
            messageCount[message]++;
            if (messageCount[message] > maxSameMessageCount)
            {
                // 너무 많은 같은 메시지가 발생하면 무시
                return true;
            }
        }
        else
        {
            messageCount[message] = 1;
        }
        
        // 시간 기반 중복 체크
        if (lastMessageTime.ContainsKey(message))
        {
            if (currentTime - lastMessageTime[message] < duplicateMessageCooldown)
            {
                return true; // 쿨다운 시간 내의 중복 메시지
            }
        }
        
        lastMessageTime[message] = currentTime;
        
        // 오래된 메시지 정리 (메모리 관리)
        if (lastMessageTime.Count > 100)
        {
            CleanupOldMessages();
        }
        
        return false;
    }
    
    void CleanupOldMessages()
    {
        float currentTime = Time.time;
        List<string> keysToRemove = new List<string>();
        
        foreach (var kvp in lastMessageTime)
        {
            if (currentTime - kvp.Value > duplicateMessageCooldown * 10)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        
        foreach (string key in keysToRemove)
        {
            lastMessageTime.Remove(key);
            messageCount.Remove(key);
        }
    }
    
    void AddDebugLineInternal(string text, Color? color = null)
    {
        // 시간 추가
        string timeStamp = System.DateTime.Now.ToString("HH:mm:ss");
        string colorHex = ColorUtility.ToHtmlStringRGB(color ?? textColor);
        string formattedText = $"<color=#{colorHex}>[{timeStamp}] {text}</color>";
        
        debugLines.Enqueue(formattedText);
        
        // 최대 라인 수 유지
        while (debugLines.Count > maxLines)
        {
            debugLines.Dequeue();
        }
    }
    
    void AddDebugLine(string text, Color? color = null)
    {
        // 외부 호출용 (재귀 방지)
        AddDebugLineInternal(text, color);
    }
    
    void OnGUI()
    {
        if (!showDebugInfo || debugLines.Count == 0) return;
        
        // 화면 크기에 맞춰 조정
        float width = Screen.width * 0.9f;
        float height = Screen.height * 0.85f;
        float x = Screen.width * 0.05f;
        float y = Screen.height * 0.075f;
        
        // 배경 박스
        GUI.Box(new Rect(x - 10, y - 10, width + 20, height + 20), "", bgStyle);
        
        // 스크롤 영역
        GUILayout.BeginArea(new Rect(x, y, width, height));
        
        // 스크롤바 스타일 설정 (세로만)
        GUIStyle scrollViewStyle = GUI.skin.scrollView;
        GUIStyle verticalScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
        GUIStyle verticalScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
        
        // 세로 스크롤바 너비를 화면 폭의 비율로 설정
        float verticalScrollbarWidth = Screen.width * scrollbarWidthRatio;
        verticalScrollbarStyle.fixedWidth = verticalScrollbarWidth;
        verticalScrollbarThumbStyle.fixedWidth = verticalScrollbarWidth;
        
        // 스크롤바 핸들(썸) 크기도 동일하게 설정
        verticalScrollbarThumbStyle.fixedHeight = verticalScrollbarWidth * 3f; // 핸들을 더 길게
        
        // 세로 스크롤만 적용
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, 
            GUIStyle.none, verticalScrollbarStyle, scrollViewStyle);
        
        // 텍스트 스타일 (Rich Text 지원)
        GUIStyle richTextStyle = new GUIStyle(textStyle);
        richTextStyle.richText = true;
        
        // 디버그 라인 표시
        foreach (string line in debugLines)
        {
            GUILayout.Label(line, richTextStyle);
        }
        
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        
        // 토글 버튼 (3배 크기)
        if (GUI.Button(new Rect(Screen.width - 330, 10, 300, 90), showDebugInfo ? "Hide Debug" : "Show Debug"))
        {
            showDebugInfo = !showDebugInfo;
        }
        
        // Clear 버튼 (3배 크기)
        if (showDebugInfo && GUI.Button(new Rect(Screen.width - 330, 110, 300, 90), "Clear"))
        {
            debugLines.Clear();
            lastMessageTime.Clear();
            messageCount.Clear();
            AddDebugLine("=== Debug Cleared ===");
        }
        
        // 상태 정보 표시 (3배 크기)
        if (showDebugInfo)
        {
            string statusInfo = $"Lines: {debugLines.Count}/{maxLines} | Include: {parsedKeywords.Count} | Exclude: {parsedExcludeKeywords.Count} | Tracked: {messageCount.Count}";
            GUI.Label(new Rect(Screen.width - 600, 210, 600, 60), statusInfo);
        }
    }
    
    // 외부에서 직접 메시지 추가
    public void Log(string message)
    {
        AddDebugLine(message);
    }
    
    public void LogError(string message)
    {
        AddDebugLine(message, Color.red);
    }
    
    public void LogWarning(string message)
    {
        AddDebugLine(message, Color.yellow);
    }
    
    // 3D 모델 로딩 상태 확인용 특수 메서드
    public void LogModelStatus(string modelUrl, string status)
    {
        string message = $"[3D Model] URL: {modelUrl} - Status: {status}";
        AddDebugLine(message, status.Contains("성공") ? Color.green : Color.red);
    }
    
    // 수동으로 메시지 카운트 리셋
    public void ResetMessageCounts()
    {
        messageCount.Clear();
        lastMessageTime.Clear();
    }
    
    // 런타임에서 키워드 업데이트
    public void UpdateKeywords(string newKeywords)
    {
        filterKeywordsString = newKeywords;
        ParseKeywords();
    }
    
    // 현재 키워드 목록 가져오기
    public List<string> GetCurrentKeywords()
    {
        ParseKeywords();
        return new List<string>(parsedKeywords);
    }
}

// 사용 예시를 위한 확장 메서드
public static class DebugDisplayExtensions
{
    private static DebugDisplay _instance;
    
    private static DebugDisplay Instance
    {
        get
        {
            if (_instance == null)
                _instance = GameObject.FindObjectOfType<DebugDisplay>();
            return _instance;
        }
    }
    
    public static void LogToScreen(string message)
    {
        if (Instance != null)
            Instance.Log(message);
    }
    
    public static void LogModelStatus(string url, string status)
    {
        if (Instance != null)
            Instance.LogModelStatus(url, status);
    }
}