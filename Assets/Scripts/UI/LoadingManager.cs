using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using System.Collections.Generic;

public class LoadingManager : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    public GameObject loadingPanel;
    public Text loadingText;
    public Image loadingSpinner;
    
    [Header("기본 로딩 설정")]
    public bool enablePreemptiveLoading = true;
    public bool forceLoadingForAR = true;
    public bool detectHeavyOperations = true;
    public float fixedLoadingTime = 3f;
    public float loadingCooldown = 5f;
    public string[] heavyOperationKeywords = {"Instantiate", "AR", "Create", "Load", "Generate"};
    
    [Header("DataManager 감지 설정")]
    public bool enableDataManagerMonitoring = true;
    public DataManager targetDataManager;
    public float creationTime = 1f; // 시간 (초)
    public int creationCount = 5; // 생성 개수
    public bool enableImmediateDataManagerUI = true; // ✅ 즉시 UI 표시 옵션 추가
    
    [Header("AR 환경 감지 설정")]
    public bool enableAREnvironmentDetection = true;
    public float environmentCheckInterval = 2f;
    public float darkEnvironmentThreshold = 0.1f;
    public int minimumFeaturePoints = 10;
    public float trackingLostTimeout = 2f;
    public int sufficientObjectCount = 3; // 충분한 오브젝트 수 (환경감지 생략 기준)
    
    [Header("백그라운드 복구 감지 설정")]
    public bool enableBackgroundRecoveryDetection = true;
    public float backgroundRecoveryLoadingTime = 2f;
    
    // 다국어 메시지 데이터
    private Dictionary<string, Dictionary<SystemLanguage, string[]>> allMessages;
    private bool isLoading = false;
    
    // DataManager 모니터링 관련 변수
    private DataManager dataManager;
    private int lastObjectCount = 0;
    private float lastObjectCountChangeTime;
    private bool isMonitoringDataManager = false;
    
    // 쿨다운 관련 변수
    private float lastLoadingTime = 0f;
    
    // AR 환경 감지 관련 변수
    private ARSession arSession;
    private ARCameraManager arCameraManager;
    private ARPointCloudManager arPointCloudManager;
    private Camera arCamera;
    private bool isCheckingAREnvironment = false;
    private float lastEnvironmentCheckTime = 0f;
    private float trackingLostStartTime = 0f;
    private bool hasShownEnvironmentGuidance = false;
    private TrackingState lastTrackingState = TrackingState.None;
    private string currentLanguage = "en";
    
    // 백그라운드 복구 관련 변수
    private bool wasInBackground = false;
    
    public enum AREnvironmentIssue
    {
        None,
        TooDark,           // 너무 어두움
        NoFeatures,        // 특징점 부족
        InsufficientLight, // 조명 부족
        TrackingLost,      // 트래킹 손실
        CameraCovered,     // 카메라 가림
        DataLoading        // 데이터 로딩 중 (DataManager 통합)
    }
    
    void Awake()
    {
        InitializeMessages();
    }
    
    void Start()
    {
        if (loadingPanel) loadingPanel.SetActive(false);
        
        Debug.Log($"현재 언어: {GetCurrentLanguageName()}");
        
        InitializeLanguage();
        
        if (enableDataManagerMonitoring)
        {
            StartCoroutine(InitializeDataManagerMonitoring());
        }
        
        if (enableAREnvironmentDetection)
        {
            InitializeARComponents();
            StartCoroutine(StartAREnvironmentMonitoring());
        }
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            wasInBackground = true;
            Debug.Log("[LoadingManager] 앱이 백그라운드로 이동");
        }
        else if (wasInBackground && enableBackgroundRecoveryDetection)
        {
            Debug.Log("[LoadingManager] 백그라운드에서 복구 - AR 환경 재감지 시작");
            wasInBackground = false;
            
            // 백그라운드 복구 시 AR 환경 재감지
            StartCoroutine(HandleBackgroundRecovery());
        }
    }
    
    IEnumerator HandleBackgroundRecovery()
    {
        // 1. 기본 복구 로딩 표시
        ShowARLoading(() => {
            Debug.Log("[LoadingManager] 백그라운드 복구 처리 완료");
        }, "AR 세션 복구 중..");
        
        // 2. AR 세션이 안정화될 때까지 대기
        yield return new WaitForSeconds(backgroundRecoveryLoadingTime);
        
        // 3. AR 환경 감지가 활성화되어 있다면 즉시 환경 체크
        if (enableAREnvironmentDetection)
        {
            Debug.Log("[LoadingManager] 백그라운드 복구 후 AR 환경 즉시 체크");
            
            // AR 컴포넌트 재초기화 (필요한 경우)
            if (arSession == null)
            {
                InitializeARComponents();
            }
            
            // 강제로 환경 체크 시작
            isCheckingAREnvironment = true;
            
            // 0.5초 대기 후 환경 체크 (AR 세션 안정화)
            yield return new WaitForSeconds(0.5f);
            CheckAREnvironment();
            
            Debug.Log("[LoadingManager] 백그라운드 복구 후 AR 환경 감지 재시작 완료");
        }
    }
    
    void Update()
    {
        if (isMonitoringDataManager && dataManager != null && !isLoading)
        {
            CheckARObjectChanges();
        }
        
        if (isCheckingAREnvironment && enableAREnvironmentDetection)
        {
            if (Time.realtimeSinceStartup - lastEnvironmentCheckTime >= environmentCheckInterval)
            {
                CheckAREnvironment();
                lastEnvironmentCheckTime = Time.realtimeSinceStartup;
            }
        }
    }
    
    public void ShowLoading(System.Action heavyWork, string category = "General")
    {
        if (isLoading) 
        {
            Debug.LogWarning("이미 로딩 중입니다.");
            return;
        }
        
        if (forceLoadingForAR || ShouldForceLoading(category))
        {
            Debug.Log($"무거운 작업 감지 - 강제 로딩 표시 ({category})");
            StartCoroutine(ForcedLoadingProcess(heavyWork, category));
        }
        else if (detectHeavyOperations)
        {
            StartCoroutine(LoadingProcessWithPreload(heavyWork, category));
        }
        else
        {
            StartCoroutine(LoadingProcess(heavyWork, category));
        }
    }
    
    public void ShowARLoading(System.Action arWork, string message = "")
    {
        string displayMessage = string.IsNullOrEmpty(message) ? "AR 오브젝트 처리 중.." : message;
        Debug.Log($"AR 작업 시작 - 즉시 로딩 표시: {displayMessage}");
        
        if (enableAREnvironmentDetection)
        {
            AREnvironmentIssue issue = GetCurrentEnvironmentIssue();
            if (issue != AREnvironmentIssue.None)
            {
                HandleEnvironmentIssue(issue);
                return;
            }
        }
        
        StartCoroutine(ARSpecificLoading(arWork, displayMessage));
    }
    
    void InitializeLanguage()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                currentLanguage = "ko";
                break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
                currentLanguage = "zh";
                break;
            case SystemLanguage.Japanese:
                currentLanguage = "ja";
                break;
            case SystemLanguage.Spanish:
                currentLanguage = "es";
                break;
            default:
                currentLanguage = "en";
                break;
        }
        
        Debug.Log($"LoadingManager 언어 설정: {currentLanguage}");
    }
    
    void InitializeARComponents()
    {
        arSession = FindObjectOfType<ARSession>();
        if (arSession == null)
        {
            Debug.LogWarning("ARSession을 찾을 수 없습니다. AR 환경 감지가 비활성화됩니다.");
            enableAREnvironmentDetection = false;
            return;
        }
        
        arCameraManager = FindObjectOfType<ARCameraManager>();
        arPointCloudManager = FindObjectOfType<ARPointCloudManager>();
        arCamera = Camera.main ?? FindObjectOfType<Camera>();
        
        Debug.Log("AR 환경 감지 컴포넌트 초기화 완료");
    }
    
    IEnumerator StartAREnvironmentMonitoring()
    {
        float waitTime = 0f;
        while (waitTime < 10f)
        {
            if (arSession != null && arSession.subsystem?.trackingState == TrackingState.Tracking)
            {
                break;
            }
            
            waitTime += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }
        
        isCheckingAREnvironment = true;
        Debug.Log($"[LoadingManager] AR 환경 모니터링 시작 (대기시간: {waitTime}초)");
        
        CheckAREnvironment();
        StartCoroutine(ForceEnvironmentCheckAfterDelay());
    }
    
    IEnumerator ForceEnvironmentCheckAfterDelay()
    {
        yield return new WaitForSeconds(5f);
        
        if (!isCheckingAREnvironment)
        {
            Debug.Log("[LoadingManager] AR 초기화 지연 - 강제로 환경 체크 시작");
            isCheckingAREnvironment = true;
        }
    }
    
    void CheckAREnvironment()
    {
        if (arSession == null || arSession.subsystem == null) return;
        
        TrackingState currentTrackingState = arSession.subsystem.trackingState;
        AREnvironmentIssue issue = DetermineEnvironmentIssue(currentTrackingState);
        
        if (issue != AREnvironmentIssue.None)
        {
            HandleEnvironmentIssue(issue);
        }
        else if (hasShownEnvironmentGuidance)
        {
            HideARGuidance();
            hasShownEnvironmentGuidance = false;
        }
        
        lastTrackingState = currentTrackingState;
    }
    
    AREnvironmentIssue DetermineEnvironmentIssue(TrackingState trackingState)
    {
        Debug.Log($"[LoadingManager] 환경 체크 - 트래킹 상태: {trackingState}");
        
        // 1. DataManager 상태 먼저 체크 (최우선순위)
        if (enableDataManagerMonitoring && dataManager != null && IsDataManagerHeavyLoading())
        {
            Debug.Log("[LoadingManager] DataManager 무거운 작업 감지 - 환경 감지 보류");
            return AREnvironmentIssue.DataLoading;
        }
        
        // 2. DataManager 작업 중이면 환경 감지 1초 지연
        if (enableDataManagerMonitoring && dataManager != null && IsDataManagerRecentlyActive())
        {
            Debug.Log("[LoadingManager] DataManager 작업 완료 후 1초 대기 - 환경 감지 보류");
            return AREnvironmentIssue.None;
        }
        
        // 3. 어두운 환경 체크 (트래킹 상태와 무관하게 우선 체크)
        if (IsEnvironmentTooDark())
        {
            Debug.Log("[LoadingManager] 어두운 환경 감지 - 트래킹 상태 무관");
            return AREnvironmentIssue.TooDark;
        }
        
        // 4. 트래킹이 정상이고 환경도 밝으면 문제 없음
        if (trackingState == TrackingState.Tracking)
        {
            trackingLostStartTime = 0f;
            Debug.Log("[LoadingManager] 트래킹 정상 + 환경 밝음 - 환경감지 불필요");
            return AREnvironmentIssue.None;
        }
        
        // 5. 오브젝트가 이미 충분히 있으면 환경 감지 불필요 (트래킹 문제가 있어도)
        if (enableDataManagerMonitoring && dataManager != null && HasSufficientObjects())
        {
            Debug.Log("[LoadingManager] 오브젝트가 충분히 발생한 상태 - 환경 감지 불필요");
            return AREnvironmentIssue.None;
        }
        
        // 6. 트래킹에 문제가 있고 오브젝트도 부족한 경우에만 환경 분석
        if (trackingState == TrackingState.None || trackingState == TrackingState.Limited)
        {
            if (lastTrackingState == TrackingState.Tracking)
            {
                trackingLostStartTime = Time.realtimeSinceStartup;
                Debug.Log("[LoadingManager] 트래킹 손실 시작");
            }
            
            if (Time.realtimeSinceStartup - trackingLostStartTime > trackingLostTimeout)
            {
                Debug.Log("[LoadingManager] 트래킹 문제 지속 - 환경 분석 시작");
                return AnalyzeTrackingIssue();
            }
        }
        
        return AREnvironmentIssue.None;
    }
    
    AREnvironmentIssue AnalyzeTrackingIssue()
    {
        if (IsEnvironmentTooDark())
        {
            return AREnvironmentIssue.TooDark;
        }
        
        if (GetFeaturePointCount() < minimumFeaturePoints)
        {
            return AREnvironmentIssue.NoFeatures;
        }
        
        if (IsCameraCovered())
        {
            return AREnvironmentIssue.CameraCovered;
        }
        
        return AREnvironmentIssue.InsufficientLight;
    }
    
    bool IsDataManagerHeavyLoading()
    {
        if (dataManager == null || !isMonitoringDataManager) return false;
        
        int currentObjectCount = dataManager.GetSpawnedObjectsCount();
        int objectIncrease = currentObjectCount - lastObjectCount; // 증가량만 계산
        float timeSinceLastChange = Time.realtimeSinceStartup - lastObjectCountChangeTime;
        
        // 오브젝트가 생성되는 경우만 체크 (삭제는 무시)
        if (objectIncrease <= 0) return false;
        
        // Creation Time 안에 Creation Count 이상 생성된 경우 감지
        bool isHeavyLoading = (timeSinceLastChange <= creationTime && objectIncrease >= creationCount);
        
        if (isHeavyLoading)
        {
            Debug.Log($"[LoadingManager] AR 오브젝트 생성 중.. ({creationTime}초 안에 {objectIncrease}개 생성)");
        }
        
        return isHeavyLoading;
    }
    
    bool IsDataManagerRecentlyActive()
    {
        if (dataManager == null || !isMonitoringDataManager) return false;
        
        float timeSinceLastChange = Time.realtimeSinceStartup - lastObjectCountChangeTime;
        
        // DataManager 작업 완료 후 1초 동안은 환경 감지 보류
        bool recentlyActive = timeSinceLastChange <= 1f;
        
        if (recentlyActive)
        {
            Debug.Log($"[LoadingManager] DataManager 최근 활동 감지: {timeSinceLastChange:F2}초 전 작업 - 환경 감지 1초 지연");
        }
        
        return recentlyActive;
    }
    
    bool HasSufficientObjects()
    {
        if (dataManager == null || !isMonitoringDataManager) return false;
        
        int currentObjectCount = dataManager.GetSpawnedObjectsCount();
        bool hasSufficientObjects = currentObjectCount >= sufficientObjectCount;
        
        if (hasSufficientObjects)
        {
            Debug.Log($"[LoadingManager] 충분한 오브젝트 존재: {currentObjectCount}개 (기준: {sufficientObjectCount}개) - 환경감지 생략");
        }
        
        return hasSufficientObjects;
    }
    
    bool IsEnvironmentTooDark()
    {
        float averageBrightness = GetAverageBrightness();
        return averageBrightness < darkEnvironmentThreshold;
    }
    
    float GetAverageBrightness()
    {
        if (arSession?.subsystem?.trackingState == TrackingState.Tracking)
        {
            return 0.7f;
        }
        else if (GetFeaturePointCount() > minimumFeaturePoints)
        {
            return 0.6f;
        }
        
        return 0.5f;
    }
    
    int GetFeaturePointCount()
    {
        if (arPointCloudManager?.trackables == null) return 0;
        
        int totalPoints = 0;
        foreach (var pointCloud in arPointCloudManager.trackables)
        {
            if (pointCloud.positions.HasValue)
            {
                totalPoints += pointCloud.positions.Value.Length;
            }
        }
        
        return totalPoints;
    }
    
    bool IsCameraCovered()
    {
        float brightness = GetAverageBrightness();
        return brightness < 0.01f;
    }
    
    // ✅ 수정된 HandleEnvironmentIssue - DataLoading의 경우 즉시 UI 표시
    void HandleEnvironmentIssue(AREnvironmentIssue issue)
    {
        if (hasShownEnvironmentGuidance) return;
        
        Debug.Log($"AR 환경 문제 감지: {issue}");
        hasShownEnvironmentGuidance = true;
        
        // ✅ DataLoading의 경우 즉시 UI 표시, 다른 경우는 기존대로 2.5초 지연
        if (issue == AREnvironmentIssue.DataLoading && enableImmediateDataManagerUI)
        {
            Debug.Log("[LoadingManager] DataManager 감지 - 즉시 UI 표시");
            string guidanceMessage = GetEnvironmentGuidanceMessage(issue);
            ShowAREnvironmentGuidance(guidanceMessage, issue);
            StartCoroutine(AutoRetryEnvironmentCheck(issue));
        }
        else
        {
            StartCoroutine(ShowDelayedEnvironmentGuidance(issue));
        }
    }
    
    IEnumerator ShowDelayedEnvironmentGuidance(AREnvironmentIssue issue)
    {
        yield return new WaitForSeconds(2.5f);
        
        AREnvironmentIssue currentIssue = DetermineEnvironmentIssue(
            arSession?.subsystem?.trackingState ?? TrackingState.None);
        
        if (currentIssue == issue && hasShownEnvironmentGuidance)
        {
            Debug.Log($"환경 문제 지속됨, 안내 패널 표시: {issue}");
            
            string guidanceMessage = GetEnvironmentGuidanceMessage(issue);
            ShowAREnvironmentGuidance(guidanceMessage, issue);
        }
        else if (currentIssue == AREnvironmentIssue.None)
        {
            Debug.Log("환경 문제가 자연스럽게 해결됨");
            hasShownEnvironmentGuidance = false;
        }
    }
    
    string GetEnvironmentGuidanceMessage(AREnvironmentIssue issue)
    {
        Dictionary<AREnvironmentIssue, Dictionary<string, string>> messages = 
            new Dictionary<AREnvironmentIssue, Dictionary<string, string>>
        {
            [AREnvironmentIssue.TooDark] = new Dictionary<string, string>
            {
                ["ko"] = "환경이 너무 어둡습니다.\n조명을 켜시거나 밝은 곳으로 이동해주세요.",
                ["en"] = "The environment is too dark.\nPlease turn on lights or move to a brighter area.",
                ["zh"] = "环境太暗了。\n请打开灯光或移动到明亮的地方。",
                ["ja"] = "環境が暗すぎます。\nライトをつけるか、明るい場所に移動してください。",
                ["es"] = "El ambiente está muy oscuro.\nPor favor, enciende las luces o muévete a un lugar más brillante."
            },
            [AREnvironmentIssue.NoFeatures] = new Dictionary<string, string>
            {
                ["ko"] = "특징점이 부족합니다.\n패턴이나 텍스처가 있는 표면을 비춰주세요.",
                ["en"] = "Insufficient visual features.\nPlease point camera at surfaces with patterns or textures.",
                ["zh"] = "视觉特征不足。\n请将相机对准有图案或纹理的表面。",
                ["ja"] = "視覚的特徴が不足しています。\nパターンやテクスチャのある表面にカメラを向けてください。",
                ["es"] = "Características visuales insuficientes.\nPor favor, apunta la cámara a superficies con patrones o texturas."
            },
            [AREnvironmentIssue.InsufficientLight] = new Dictionary<string, string>
            {
                ["ko"] = "조명이 부족합니다.\n더 밝은 환경에서 사용해주세요.",
                ["en"] = "Insufficient lighting.\nPlease use in a brighter environment.",
                ["zh"] = "光线不足。\n请在更明亮的环境中使用。",
                ["ja"] = "照明が不足しています。\nより明るい環境でご使用ください。",
                ["es"] = "Iluminación insuficiente.\nPor favor, úsalo en un ambiente más brillante."
            },
            [AREnvironmentIssue.CameraCovered] = new Dictionary<string, string>
            {
                ["ko"] = "카메라가 가려져 있습니다.\n손가락이나 물체를 치워주세요.",
                ["en"] = "Camera appears to be covered.\nPlease remove fingers or objects from camera.",
                ["zh"] = "相机似乎被遮挡了。\n请移开手指或物体。",
                ["ja"] = "カメラが覆われているようです。\n指や物体をカメラから取り除いてください。",
                ["es"] = "La cámara parece estar cubierta.\nPor favor, retira los dedos u objetos de la cámara."
            },
            [AREnvironmentIssue.DataLoading] = new Dictionary<string, string>
            {
                ["ko"] = "AR 오브젝트 처리 중입니다.\n잠시만 기다려주세요.",
                ["en"] = "Processing AR objects.\nPlease wait a moment.",
                ["zh"] = "正在处理AR对象。\n请稍等片刻。",
                ["ja"] = "ARオブジェクトを処理中です。\n少々お待ちください。",
                ["es"] = "Procesando objetos AR.\nPor favor, espera un momento."
            }
        };
        
        if (messages.ContainsKey(issue) && messages[issue].ContainsKey(currentLanguage))
        {
            return messages[issue][currentLanguage];
        }
        
        if (messages.ContainsKey(issue))
        {
            return messages[issue]["en"];
        }
        
        return "AR 환경을 확인해주세요.";
    }
    
    void ShowAREnvironmentGuidance(string message, AREnvironmentIssue issue)
    {
        Debug.Log($"AR 환경 안내 표시: {message}");
        
        // 기존 로딩 UI만 사용 (스피너 포함)
        if (loadingPanel) loadingPanel.SetActive(true);
        if (loadingSpinner) StartCoroutine(SpinnerAnimation());
        UpdateMessage(message);
        
        Debug.Log("기존 로딩 UI로 AR 환경 안내 표시 (스피너 포함)");
        
        // ✅ DataLoading이 아닌 경우에만 AutoRetry 시작 (이미 시작된 경우 중복 방지)
        if (issue != AREnvironmentIssue.DataLoading)
        {
            StartCoroutine(AutoRetryEnvironmentCheck(issue));
        }
    }
    
    IEnumerator AutoRetryEnvironmentCheck(AREnvironmentIssue issue)
    {
        while (hasShownEnvironmentGuidance)
        {
            yield return new WaitForSeconds(1f);
            
            AREnvironmentIssue currentIssue = DetermineEnvironmentIssue(
                arSession?.subsystem?.trackingState ?? TrackingState.None);
            
            if (currentIssue == AREnvironmentIssue.None)
            {
                Debug.Log("AR 환경 문제 해결됨 - 정상 동작 재개");
                
                HideARGuidance();
                hasShownEnvironmentGuidance = false;
                break;
            }
            else if (currentIssue != issue)
            {
                Debug.Log($"환경 문제 변경: {issue} -> {currentIssue}");
                
                string newGuidanceMessage = GetEnvironmentGuidanceMessage(currentIssue);
                UpdateMessage(newGuidanceMessage);
                
                issue = currentIssue;
            }
        }
    }
    
    void HideARGuidance()
    {
        // 기존 로딩 UI 숨기기 (스피너 애니메이션도 정지)
        if (loadingPanel) loadingPanel.SetActive(false);
        StopAllCoroutines();
        Debug.Log("기존 로딩 UI 숨김 (AR 환경 안내 종료, 스피너 정지)");
    }
    
    public AREnvironmentIssue GetCurrentEnvironmentIssue()
    {
        if (!enableAREnvironmentDetection || arSession?.subsystem == null)
        {
            return AREnvironmentIssue.None;
        }
        
        return DetermineEnvironmentIssue(arSession.subsystem.trackingState);
    }
    
    IEnumerator InitializeDataManagerMonitoring()
    {
        if (targetDataManager != null)
        {
            dataManager = targetDataManager;
        }
        else
        {
            while (dataManager == null)
            {
                dataManager = FindObjectOfType<DataManager>();
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        if (dataManager != null)
        {
            lastObjectCount = dataManager.GetSpawnedObjectsCount();
            lastObjectCountChangeTime = Time.realtimeSinceStartup;
            isMonitoringDataManager = true;
            Debug.Log("DataManager 모니터링 시작됨");
        }
        else
        {
            Debug.LogWarning("DataManager를 찾을 수 없어 모니터링을 시작할 수 없습니다.");
        }
    }
    
    // ✅ 수정된 CheckARObjectChanges - 임계값 만족시 즉시 UI 표시
    void CheckARObjectChanges()
    {
        int currentObjectCount = dataManager.GetSpawnedObjectsCount();
        
        if (currentObjectCount != lastObjectCount)
        {
            int objectChange = currentObjectCount - lastObjectCount; // 증감량 (음수=삭제, 양수=생성)
            float timeSinceLastChange = Time.realtimeSinceStartup - lastObjectCountChangeTime;
            
            if (objectChange > 0)
            {
                Debug.Log($"[LoadingManager] AR 오브젝트 생성 감지: {lastObjectCount} -> {currentObjectCount} (+{objectChange}개, 시간: {timeSinceLastChange:F2}초)");
                
                // ✅ 임계값 조건 만족시 즉시 UI 표시
                if (enableImmediateDataManagerUI && 
                    timeSinceLastChange <= creationTime && 
                    objectChange >= creationCount)
                {
                    Debug.Log($"[LoadingManager] 임계값 만족 - 즉시 UI 표시 ({creationTime}초 안에 {objectChange}개 생성)");
                    
                    // 이미 UI가 표시중이 아닐 때만 표시
                    if (!hasShownEnvironmentGuidance && !isLoading)
                    {
                        hasShownEnvironmentGuidance = true;
                        string message = GetEnvironmentGuidanceMessage(AREnvironmentIssue.DataLoading);
                        ShowAREnvironmentGuidance(message, AREnvironmentIssue.DataLoading);
                        StartCoroutine(AutoRetryEnvironmentCheck(AREnvironmentIssue.DataLoading));
                        Debug.Log("[LoadingManager] 즉시 DataManager UI 표시 완료");
                    }
                    else
                    {
                        Debug.Log("[LoadingManager] 이미 UI 표시중이므로 중복 표시 방지");
                    }
                }
            }
            else
            {
                Debug.Log($"[LoadingManager] AR 오브젝트 삭제 감지: {lastObjectCount} -> {currentObjectCount} ({objectChange}개, 시간: {timeSinceLastChange:F2}초)");
            }
            
            // 오브젝트 변화 정보 업데이트 (AR 환경 감지에서 생성만 체크)
            lastObjectCount = currentObjectCount;
            lastObjectCountChangeTime = Time.realtimeSinceStartup;
        }
    }
    
    void TriggerARObjectLoading(string customMessage, int objectCount)
    {
        if (isLoading || !enableDataManagerMonitoring) return;
        
        if (Time.realtimeSinceStartup - lastLoadingTime < loadingCooldown)
        {
            Debug.Log($"로딩 쿨다운 중... 남은 시간: {loadingCooldown - (Time.realtimeSinceStartup - lastLoadingTime):F1}초");
            return;
        }
        
        Debug.Log($"AR 오브젝트 처리 로딩 시작: {customMessage} (오브젝트 수: {objectCount})");
        lastLoadingTime = Time.realtimeSinceStartup;
        
        ShowARLoading(() => {
            Debug.Log("AR 오브젝트 처리 대기 중...");
        }, customMessage);
    }
    
    IEnumerator ARSpecificLoading(System.Action arWork, string customMessage)
    {
        isLoading = true;
        
        ShowLoadingUI();
        UpdateMessage(customMessage);
        
        float startTime = Time.realtimeSinceStartup;
        
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        try
        {
            arWork?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"AR 작업 중 오류: {e.Message}");
        }
        
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(fixedLoadingTime);
        
        HideLoadingUI();
        isLoading = false;
        
        Debug.Log($"AR 작업 완료 (총 시간: {Time.realtimeSinceStartup - startTime:F2}초)");
    }
    
    IEnumerator ForcedLoadingProcess(System.Action heavyWork, string category)
    {
        isLoading = true;
        
        ShowLoadingUI();
        string[] messages = GetMessages(category);
        UpdateMessage(messages[0]);
        
        yield return new WaitForEndOfFrame();
        
        try
        {
            heavyWork?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"강제 로딩 작업 중 오류: {e.Message}");
        }
        
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(fixedLoadingTime);
        
        HideLoadingUI();
        isLoading = false;
    }
    
    IEnumerator LoadingProcessWithPreload(System.Action heavyWork, string category)
    {
        isLoading = true;
        
        ShowLoadingUI();
        string[] categoryMessages = GetMessages(category);
        UpdateMessage(categoryMessages[0]);
        
        yield return new WaitForEndOfFrame();
        
        try
        {
            heavyWork?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"로딩 작업 중 오류: {e.Message}");
        }
        
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(fixedLoadingTime);
        
        HideLoadingUI();
        isLoading = false;
    }
    
    IEnumerator LoadingProcess(System.Action heavyWork, string category)
    {
        isLoading = true;
        
        ShowLoadingUI();
        string[] categoryMessages = GetMessages(category);
        UpdateMessage(categoryMessages[0]);
        
        yield return new WaitForEndOfFrame();
        
        try
        {
            heavyWork?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"로딩 작업 중 오류: {e.Message}");
        }
        
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(fixedLoadingTime);
        
        HideLoadingUI();
        isLoading = false;
    }
    
    void ShowLoadingUI()
    {
        if (loadingPanel) loadingPanel.SetActive(true);
        if (loadingSpinner) StartCoroutine(SpinnerAnimation());
    }
    
    void HideLoadingUI()
    {
        if (loadingPanel) loadingPanel.SetActive(false);
        StopAllCoroutines();
    }
    
    void UpdateMessage(string message)
    {
        if (loadingText) loadingText.text = message;
    }
    
    IEnumerator SpinnerAnimation()
    {
        while (loadingPanel && loadingPanel.activeInHierarchy && loadingSpinner)
        {
            loadingSpinner.transform.Rotate(0, 0, -90 * Time.deltaTime);
            yield return null;
        }
    }
    
    bool ShouldForceLoading(string category)
    {
        if (!enablePreemptiveLoading) return false;
        
        string[] forceCategories = { "Data", "AR", "Model", "Heavy", "Network" };
        foreach (string forceCategory in forceCategories)
        {
            if (category.Contains(forceCategory))
            {
                return true;
            }
        }
        
        return false;
    }
    
    string GetCurrentLanguageName()
    {
        SystemLanguage lang = Application.systemLanguage;
        switch (lang)
        {
            case SystemLanguage.Korean: return "한국어";
            case SystemLanguage.Japanese: return "日本語";
            case SystemLanguage.Chinese: return "中文";
            case SystemLanguage.Spanish: return "Español";
            default: return "English";
        }
    }
    
    void InitializeMessages()
    {
        allMessages = new Dictionary<string, Dictionary<SystemLanguage, string[]>>();
        
        allMessages["General"] = new Dictionary<SystemLanguage, string[]>
        {
            [SystemLanguage.English] = new string[] { "Loading.." },
            [SystemLanguage.Korean] = new string[] { "로딩 중.." },
            [SystemLanguage.Japanese] = new string[] { "ロード中.." },
            [SystemLanguage.Chinese] = new string[] { "正在加载.." },
            [SystemLanguage.Spanish] = new string[] { "Cargando.." }
        };
        
        allMessages["Data"] = new Dictionary<SystemLanguage, string[]>
        {
            [SystemLanguage.English] = new string[] { "Loading data.." },
            [SystemLanguage.Korean] = new string[] { "데이터 로딩 중.." },
            [SystemLanguage.Japanese] = new string[] { "データロード中.." },
            [SystemLanguage.Chinese] = new string[] { "正在加载数据.." },
            [SystemLanguage.Spanish] = new string[] { "Cargando datos.." }
        };
        
        allMessages["Network"] = new Dictionary<SystemLanguage, string[]>
        {
            [SystemLanguage.English] = new string[] { "Connecting.." },
            [SystemLanguage.Korean] = new string[] { "연결 중.." },
            [SystemLanguage.Japanese] = new string[] { "接続中.." },
            [SystemLanguage.Chinese] = new string[] { "正在连接.." },
            [SystemLanguage.Spanish] = new string[] { "Conectando.." }
        };
        
        allMessages["Optimization"] = new Dictionary<SystemLanguage, string[]>
        {
            [SystemLanguage.English] = new string[] { "Optimizing.." },
            [SystemLanguage.Korean] = new string[] { "최적화 중.." },
            [SystemLanguage.Japanese] = new string[] { "最適化中.." },
            [SystemLanguage.Chinese] = new string[] { "正在优化.." },
            [SystemLanguage.Spanish] = new string[] { "Optimizando.." }
        };
    }
    
    string[] GetMessages(string category)
    {
        SystemLanguage currentLanguage = Application.systemLanguage;
        
        if (!allMessages.ContainsKey(category))
        {
            category = "General";
        }
        
        var categoryMessages = allMessages[category];
        
        if (categoryMessages.ContainsKey(currentLanguage))
        {
            return categoryMessages[currentLanguage];
        }
        
        return categoryMessages[SystemLanguage.English];
    }
    
    public void ShowDataLoading(System.Action action) 
    { 
        ShowLoading(action, "Data"); 
    }
    
    public void ShowNetworkLoading(System.Action action) 
    { 
        ShowLoading(action, "Network"); 
    }
    
    public void LoadARObject(System.Action action) 
    { 
        ShowARLoading(action, "AR 오브젝트 로딩 중.."); 
    }
    
    public void PlaceARModel(System.Action action) 
    { 
        ShowARLoading(action, "AR 모델 배치 중.."); 
    }
    
    public void CreateARAnchors(System.Action action) 
    { 
        ShowARLoading(action, "AR 앵커 생성 중.."); 
    }
    
    public bool IsDataManagerLoading()
    {
        return dataManager != null && !dataManager.IsDataLoaded();
    }
    
    public void ShowDataManagerLoading(string operation = "데이터 처리 중..")
    {
        if (dataManager == null)
        {
            Debug.LogWarning("DataManager가 설정되지 않았습니다.");
            return;
        }
        
        ShowARLoading(() => {
            Debug.Log($"DataManager 작업 완료: {operation}");
        }, operation);
    }
    
    public bool IsLoading => isLoading;
    
    public void CheckAREnvironmentManually()
    {
        if (enableAREnvironmentDetection)
        {
            CheckAREnvironment();
        }
    }
    
    public void SetAREnvironmentDetection(bool enabled)
    {
        enableAREnvironmentDetection = enabled;
        isCheckingAREnvironment = enabled;
        
        if (!enabled)
        {
            HideARGuidance();
            hasShownEnvironmentGuidance = false;
        }
    }
    
    public void ForceResolveEnvironmentIssue()
    {
        if (hasShownEnvironmentGuidance)
        {
            Debug.Log("강제로 AR 환경 문제 해결 처리");
            HideARGuidance();
            hasShownEnvironmentGuidance = false;
        }
    }
    
    // ✅ 추가된 공개 메서드들
    
    /// <summary>
    /// DataManager 즉시 UI 표시 옵션 설정
    /// </summary>
    public void SetImmediateDataManagerUI(bool enabled)
    {
        enableImmediateDataManagerUI = enabled;
        Debug.Log($"[LoadingManager] DataManager 즉시 UI 표시: {enabled}");
    }
    
    /// <summary>
    /// DataManager 임계값 설정 (Creation Time과 Creation Count)
    /// </summary>
    public void SetDataManagerThreshold(float time, int count)
    {
        creationTime = time;
        creationCount = count;
        Debug.Log($"[LoadingManager] DataManager 임계값 설정: {time}초 안에 {count}개");
    }
    
    /// <summary>
    /// 현재 DataManager 임계값 조건 확인
    /// </summary>
    public bool IsDataManagerThresholdMet()
    {
        if (dataManager == null || !isMonitoringDataManager) return false;
        
        int currentObjectCount = dataManager.GetSpawnedObjectsCount();
        int objectIncrease = currentObjectCount - lastObjectCount;
        float timeSinceLastChange = Time.realtimeSinceStartup - lastObjectCountChangeTime;
        
        return (objectIncrease > 0 && timeSinceLastChange <= creationTime && objectIncrease >= creationCount);
    }
    
    [ContextMenu("Test Loading")]
    void TestLoading()
    {
        ShowLoading(() => {
            Debug.Log("테스트 완료!");
        }, "General");
    }
    
    [ContextMenu("Test AR Loading")]
    void TestARLoading()
    {
        ShowARLoading(() => {
            Debug.Log("AR 테스트 완료!");
        }, "AR 테스트 로딩 중..");
    }
    
    [ContextMenu("Test DataManager Immediate UI")]
    void TestDataManagerImmediateUI()
    {
        if (dataManager != null)
        {
            // 임계값 조건을 강제로 시뮬레이션
            lastObjectCount = dataManager.GetSpawnedObjectsCount() - creationCount;
            lastObjectCountChangeTime = Time.realtimeSinceStartup;
            
            Debug.Log($"[LoadingManager] DataManager 즉시 UI 테스트 - 임계값: {creationTime}초 안에 {creationCount}개");
            CheckARObjectChanges();
        }
        else
        {
            Debug.LogWarning("DataManager가 없어서 테스트할 수 없습니다.");
        }
    }
    
    [ContextMenu("Test Dark Environment")]
    void TestDarkEnvironment()
    {
        HandleEnvironmentIssue(AREnvironmentIssue.TooDark);
    }
    
    [ContextMenu("Test No Features Environment")]
    void TestNoFeaturesEnvironment()
    {
        HandleEnvironmentIssue(AREnvironmentIssue.NoFeatures);
    }
    
    [ContextMenu("Test Insufficient Light")]
    void TestInsufficientLight()
    {
        HandleEnvironmentIssue(AREnvironmentIssue.InsufficientLight);
    }
    
    [ContextMenu("Test Camera Covered")]
    void TestCameraCovered()
    {
        HandleEnvironmentIssue(AREnvironmentIssue.CameraCovered);
    }
    
    [ContextMenu("Test Data Loading")]
    void TestDataLoading()
    {
        HandleEnvironmentIssue(AREnvironmentIssue.DataLoading);
    }
    
    [ContextMenu("Test Background Recovery")]
    void TestBackgroundRecovery()
    {
        StartCoroutine(HandleBackgroundRecovery());
        Debug.Log("백그라운드 복구 테스트 시작");
    }
    
    [ContextMenu("Show Current Status")]
    void ShowCurrentStatus()
    {
        Debug.Log($"=== LoadingManager Status ===");
        Debug.Log($"Current Language: {currentLanguage}");
        Debug.Log($"Is Loading: {isLoading}");
        Debug.Log($"AR Environment Detection: {enableAREnvironmentDetection}");
        Debug.Log($"Is Checking AR Environment: {isCheckingAREnvironment}");
        Debug.Log($"Has Shown Environment Guidance: {hasShownEnvironmentGuidance}");
        Debug.Log($"DataManager Monitoring: {isMonitoringDataManager}");
        Debug.Log($"Immediate DataManager UI: {enableImmediateDataManagerUI}");
        Debug.Log($"Creation Threshold: {creationTime}초 안에 {creationCount}개");
        Debug.Log($"Is Threshold Met: {IsDataManagerThresholdMet()}");
        if (arSession?.subsystem != null)
        {
            Debug.Log($"AR Tracking State: {arSession.subsystem.trackingState}");
        }
        Debug.Log($"Feature Point Count: {GetFeaturePointCount()}");
        Debug.Log($"Average Brightness: {GetAverageBrightness()}");
        Debug.Log($"Current AR Environment Issue: {GetCurrentEnvironmentIssue()}");
        if (dataManager != null)
        {
            Debug.Log($"Current Object Count: {dataManager.GetSpawnedObjectsCount()}");
            Debug.Log($"Last Object Count: {lastObjectCount}");
            Debug.Log($"Time Since Last Change: {Time.realtimeSinceStartup - lastObjectCountChangeTime:F2}초");
        }
        Debug.Log($"========================");
    }
}