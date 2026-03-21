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

    [Tooltip("백그라운드 복귀 시 이 거리(m) 이상 이동했으면 데이터 전체 재로드")]
    public float backgroundRelocationThreshold = 200f;

    [Header("=== Fallback 화살표 설정 ===")]
    [Tooltip("화살표 기본 스케일 배수 (1.0 = 기본, 1.5 = 1.5배)")]
    public float fallbackBaseScaleMultiplier = 1.5f;

    [Tooltip("화살표 스케일 랜덤 범위 (최소)")]
    public float fallbackScaleRandomMin = 0.85f;

    [Tooltip("화살표 스케일 랜덤 범위 (최대)")]
    public float fallbackScaleRandomMax = 1.3f;

    [Tooltip("펄스 애니메이션 속도 (값이 클수록 빠름)")]
    public float fallbackPulseSpeed = 0.8f;

    [Tooltip("펄스 애니메이션 진폭 (0.15 = ±15%)")]
    public float fallbackPulseAmplitude = 0.15f;

    [Tooltip("화면 경계 마진 (상단, Canvas 논리적 크기 비율 0~0.5)")]
    [Range(0f, 0.5f)]
    public float fallbackMarginTop = 0.08f;

    [Tooltip("화면 경계 마진 (하단, Canvas 논리적 크기 비율 0~0.5)")]
    [Range(0f, 0.5f)]
    public float fallbackMarginBottom = 0.05f;

    [Tooltip("화면 경계 마진 (좌측, Canvas 논리적 크기 비율 0~0.5)")]
    [Range(0f, 0.5f)]
    public float fallbackMarginLeft = 0.05f;

    [Tooltip("화면 경계 마진 (우측, Canvas 논리적 크기 비율 0~0.5)")]
    [Range(0f, 0.5f)]
    public float fallbackMarginRight = 0.05f;

    [Tooltip("최대 화살표 표시 개수 (가까운 순으로 선택)")]
    [Range(1, 20)]
    public int fallbackMaxIndicatorCount = 10;

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
    private bool isBackgroundRecovering = false; // HandleBackgroundRecovery 진행 중 플래그
    public bool IsBackgroundRecovering => isBackgroundRecovering;
    private Coroutine dotAnimationCoroutine;
    private Coroutine spinnerCoroutine; // Spinner 중복 실행 방지
    private float? lastCameraBrightness = null; // ARCameraManager 밝기 캐시

    [Header("=== Fallback 타이밍 설정 ===")]
    [Tooltip("앱 시작 시 fallback 최소 유지 시간 (초)")]
    public float initialFallbackDuration = 2f;

    [Tooltip("백그라운드 복귀 시 fallback 최소 유지 시간 (초)")]
    public float backgroundFallbackDuration = 1.0f;

    [Tooltip("데이터 로드 완료 후 fallback 활성화 딜레이 (초)")]
    public float fallbackActivationDelay = 0.2f;

    private OffScreenIndicator cachedOSI; // OffScreenIndicator 캐시
    
    public enum AREnvironmentIssue
    {
        None,
        TooDark,           // 너무 어두움
        NoFeatures,        // 특징점 부족
        InsufficientLight, // 조명 부족
        TrackingLost,      // 트래킹 손실
        CameraCovered,     // 카메라 가림
        ExcessiveMotion,   // 과도한 움직임
        DataLoading,       // 데이터 로딩 중 (DataManager 통합)
        SessionPreparing   // AR 세션 작동 준비 중 (세션 미초기화/완전 실패)
    }
    
    void Awake()
    {
        InitializeMessages();
    }

    void Start()
    {
        if (loadingPanel) loadingPanel.SetActive(false);

        InitializeLanguage();

        // DataManager 선행 로드 완료 이벤트 구독
        DataManager.OnPreFetchCompleted += OnDataPreFetchCompleted;

        if (enableDataManagerMonitoring)
        {
            StartCoroutine(InitializeDataManagerMonitoring());
        }

#if UNITY_EDITOR
        // 에디터에서는 AR 서브시스템이 없으므로 환경 감지 비활성화 (loadingPanel 차단 방지)
        enableAREnvironmentDetection = false;
#else
        if (enableAREnvironmentDetection)
        {
            InitializeARComponents();
            StartCoroutine(StartAREnvironmentMonitoring());
        }

#endif
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
#if UNITY_EDITOR
        // 에디터에서는 포커스 변경 시마다 OnApplicationPause가 호출되므로 background recovery 건너뜀
        return;
#else
        if (pauseStatus)
        {
            wasInBackground = true;
        }
        else if (wasInBackground && enableBackgroundRecoveryDetection)
        {
            wasInBackground = false;
            // 이미 복구 진행 중이면 중복 실행 방지 (OnApplicationPause가 연속 호출될 수 있음)
            if (!isBackgroundRecovering)
            {
                StartCoroutine(HandleBackgroundRecovery());
            }
        }
#endif
    }
    
    IEnumerator HandleBackgroundRecovery()
    {
        isBackgroundRecovering = true;
        Debug.Log("[OSID] HandleBackgroundRecovery 시작");

        // ============================================================
        // 0. GPS 위치 변동 체크 — 200m 이상 이동 시 전체 재로드 필요 판단
        // ============================================================
        bool needFullReload = false;
#if !UNITY_EDITOR
        if (dataManager != null && Input.location.status == LocationServiceStatus.Running)
        {
            Vector2 lastFetch = dataManager.GetLastFetchPosition();
            float currentLat = Input.location.lastData.latitude;
            float currentLon = Input.location.lastData.longitude;

            // lastFetch가 유효한 경우에만 비교 (0,0이면 아직 한번도 fetch 안 한 상태)
            if (lastFetch.x != 0f || lastFetch.y != 0f)
            {
                float movedDistance = CalculateDistanceHaversine(lastFetch.x, lastFetch.y, currentLat, currentLon);
                Debug.Log($"[OSID] 백그라운드 복귀 이동거리={movedDistance:F0}m, threshold={backgroundRelocationThreshold}m");
                if (movedDistance > backgroundRelocationThreshold)
                {
                    needFullReload = true;
                    Debug.Log("[OSID] needFullReload=true (위치 대폭 변동)");
                }
            }
        }
#endif

        // 1. 먼저 fallback 모드 활성화 (타겟이 살아있는 상태에서 fallbackDataMap 확보)
        // autoDisable=false: 트래킹 복구 확인 후 수동으로 해제 (1초 타이머로 끄면 AR 미복구 상태에서 화살표 사라짐)
        OffScreenIndicator osi = GetCachedOSI();
        if (osi != null)
        {
            // 위치 대폭 변동 시 이전 위치의 fallback 데이터를 그대로 쓰면 안 됨 → fallback 모드 생략
            if (!needFullReload)
            {
                int targetCount = osi.GetActiveTargetCount();
                Debug.Log($"[OSID] fallback 활성화 시도: targets={targetCount}, isFallback={osi.IsFallbackMode}");
                osi.SetFallbackMinDuration(backgroundFallbackDuration);
#if UNITY_EDITOR
                osi.EnableFallbackMode(true, GetFallbackConfig(), autoDisable: true);
#else
                osi.EnableFallbackMode(true, GetFallbackConfig(), autoDisable: false);
#endif
                Debug.Log($"[OSID] fallback 활성화 완료: isFallback={osi.IsFallbackMode}");
            }
            else
            {
                Debug.Log("[OSID] 위치 변동으로 fallback 생략");
            }
        }

        // 2. fallback 위치 확보 후 AR 오브젝트 숨기기 (Geospatial 앵커 미복구 상태에서 카메라 앞에 렌더링 방지)
        if (dataManager != null)
        {
            int objCount = dataManager.GetSpawnedObjectsCount();
            Debug.Log($"[OSID] 오브젝트 숨김: count={objCount}");
            dataManager.SetAllObjectsVisible(false);
        }

        // 3. 다국어 복구 메시지 + 점 애니메이션 표시
        string baseMessage = GetSessionRecoveringMessage();
        if (loadingPanel) loadingPanel.SetActive(true);
        StartSpinner();
        StartDotAnimation(baseMessage);

        // 4. AR 세션이 안정화될 때까지 대기
        yield return new WaitForSeconds(backgroundRecoveryLoadingTime);

        // 5. AR 환경 감지가 활성화되어 있다면 즉시 환경 체크
        if (enableAREnvironmentDetection)
        {
            if (arSession == null)
                InitializeARComponents();

            // 백그라운드 복구 후 트래킹 상태 초기화 (DetermineEnvironmentIssue가 올바르게 동작하도록)
            trackingLostStartTime = 0f;
            lastTrackingState = TrackingState.None;
            hasShownEnvironmentGuidance = false;

            isCheckingAREnvironment = true;
            yield return new WaitForSeconds(0.5f);
            CheckAREnvironment();

            // 트래킹 복구되면 앵커 재생성 → 개별 표시 → fallback 해제
            yield return StartCoroutine(WaitForTrackingRecoveryAndCleanup(osi, needFullReload));
        }
        else
        {
            StopDotAnimation();
            HideLoadingUI();
            // fallback은 유지 (오브젝트가 있으면 WaitForFirstObjects에서 해제)
        }
    }

    /// <summary>
    /// Haversine 공식으로 두 GPS 좌표 간 거리(m) 계산
    /// </summary>
    private float CalculateDistanceHaversine(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000f;
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);
        float a = Mathf.Sin(dLat / 2f) * Mathf.Sin(dLat / 2f)
                + Mathf.Cos(Mathf.Deg2Rad * lat1) * Mathf.Cos(Mathf.Deg2Rad * lat2)
                * Mathf.Sin(dLon / 2f) * Mathf.Sin(dLon / 2f);
        float c = 2f * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1f - a));
        return R * c;
    }

    /// <summary>
    /// 백그라운드 복구 후 트래킹이 정상화되면 앵커 재생성 → 로딩 패널 숨기기 → fallback 해제
    /// SetAllObjectsVisible(true) 대신 RecreateAllAnchors()를 호출하여
    /// 앵커가 실제로 생성 완료된 오브젝트만 개별적으로 표시 (뭉침 방지)
    /// </summary>
    IEnumerator WaitForTrackingRecoveryAndCleanup(OffScreenIndicator osi, bool needFullReload = false)
    {
        float maxWait = 15f;
        float waited = 0f;
        Debug.Log($"[OSID] WaitForTrackingRecovery 시작: needFullReload={needFullReload}, maxWait={maxWait}s");

        while (waited < maxWait)
        {
            TrackingState ts = arSession?.subsystem?.trackingState ?? TrackingState.None;
            if (waited % 2f < 0.6f) // 2초마다 상태 로그
            {
                Debug.Log($"[OSID] 트래킹 대기중: state={ts}, waited={waited:F1}s");
            }

            if (ts == TrackingState.Tracking)
            {
                Debug.Log($"[OSID] 트래킹 복구됨! waited={waited:F1}s, needFullReload={needFullReload}");
                // 로딩 패널 숨기기
                StopDotAnimation();
                if (loadingPanel) loadingPanel.SetActive(false);
                StopSpinner();

                if (needFullReload)
                {
                    // 위치 대폭 변동 → 기존 오브젝트 전부 제거 + 새 위치 기준 데이터 재로드
                    if (dataManager != null)
                        dataManager.FullRefreshFromNewLocation();

                    // 시설 매니저들도 앵커 재생성 (데이터는 이미 로컬에 있으므로 앵커만 갱신)
                    RecreateTransitManagerAnchors();
                }
                else
                {
                    // 근거리 복귀 → 기존 앵커만 재생성
                    Debug.Log("[OSID] RecreateAllManagerAnchors 호출");
                    RecreateAllManagerAnchors();
                }

                // fallback 명시적 해제 (autoDisable=false이므로 수동 해제 필요)
                if (osi != null)
                {
                    Debug.Log($"[OSID] fallback 해제 요청: isFallback={osi.IsFallbackMode}");
                    osi.EnableFallbackMode(false);
                }
                isBackgroundRecovering = false;
                Debug.Log("[OSID] HandleBackgroundRecovery 완료 (트래킹 복구)");
                yield break;
            }

            waited += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        // 타임아웃 시에도 처리 (무한 숨김 방지)
        Debug.Log($"[OSID] 트래킹 복구 타임아웃 ({maxWait}s), 강제 처리 진행");
        StopDotAnimation();
        if (loadingPanel) loadingPanel.SetActive(false);
        StopSpinner();

        if (needFullReload)
        {
            if (dataManager != null)
                dataManager.FullRefreshFromNewLocation();
            RecreateTransitManagerAnchors();
        }
        else
        {
            RecreateAllManagerAnchors();
        }

        // 타임아웃이어도 오브젝트 보이기 (무한 숨김 방지)
        // RecreateAllAnchors가 Earth Tracking 실패 시 렌더러를 꺼두므로 명시적 복구
        if (dataManager != null)
        {
            Debug.Log("[OSID] 타임아웃 — SetAllObjectsVisible(true) 강제 복구");
            dataManager.SetAllObjectsVisible(true);
        }

        // 타임아웃이어도 fallback 해제 (CheckAREnvironment가 이어서 환경 감지)
        if (osi != null && !hasShownEnvironmentGuidance)
        {
            Debug.Log("[OSID] 타임아웃 — fallback 해제");
            osi.EnableFallbackMode(false);
        }
        else if (osi != null && hasShownEnvironmentGuidance)
        {
            Debug.Log("[OSID] 타임아웃 — 환경안내 활성 상태, fallback 유지");
        }
        isBackgroundRecovering = false;
        Debug.Log("[OSID] HandleBackgroundRecovery 완료 (타임아웃)");
    }

    /// <summary>
    /// 모든 매니저의 Geospatial 앵커를 재생성
    /// 기존 오브젝트/컴포넌트는 유지하고 앵커만 재연결
    /// 앵커 생성 성공 시 Renderer가 자동 표시됨 (서버 재요청 불필요)
    /// </summary>
    void RecreateAllManagerAnchors()
    {
        if (dataManager != null)
            dataManager.RecreateAllAnchors();

        if (SubwayManager.Instance != null) SubwayManager.Instance.RecreateAllAnchors();
        if (TrainStationManager.Instance != null) TrainStationManager.Instance.RecreateAllAnchors();
        if (TerminalManager.Instance != null) TerminalManager.Instance.RecreateAllAnchors();
        if (BusStationManager.Instance != null) BusStationManager.Instance.RecreateAllAnchors();
        if (TourAPIManager.Instance != null) TourAPIManager.Instance.RecreateAllAnchors();
    }

    /// <summary>
    /// DataManager를 제외한 시설 매니저들만 앵커 재생성
    /// (DataManager는 FullRefreshFromNewLocation에서 별도 처리)
    /// </summary>
    void RecreateTransitManagerAnchors()
    {
        if (SubwayManager.Instance != null) SubwayManager.Instance.RecreateAllAnchors();
        if (TrainStationManager.Instance != null) TrainStationManager.Instance.RecreateAllAnchors();
        if (TerminalManager.Instance != null) TerminalManager.Instance.RecreateAllAnchors();
        if (BusStationManager.Instance != null) BusStationManager.Instance.RecreateAllAnchors();
        if (TourAPIManager.Instance != null) TourAPIManager.Instance.RecreateAllAnchors();
    }

    void Update()
    {
        if (isMonitoringDataManager && dataManager != null && !isLoading)
        {
            CheckARObjectChanges();
        }

        if (isCheckingAREnvironment && enableAREnvironmentDetection)
        {
            // 매 프레임: 트래킹 상태 변화 즉시 감지 (오브젝트 뭉침/복구)
            CheckTrackingStateChange();

            // 2초 간격: 환경 분석 (어둡다/특징점 부족 등)
            if (Time.realtimeSinceStartup - lastEnvironmentCheckTime >= environmentCheckInterval)
            {
                CheckAREnvironment();
                lastEnvironmentCheckTime = Time.realtimeSinceStartup;
            }
        }
    }

    /// <summary>
    /// 매 프레임 트래킹 상태 변화 감지 — fallback 진입/해제만 관리
    /// Tracking↔Limited 빠른 전환 시 디바운스 적용 (1초 이내 재해제 무시)
    /// </summary>
    private TrackingState lastFrameTrackingState = TrackingState.None;

    void CheckTrackingStateChange()
    {
        if (arSession == null || arSession.subsystem == null) return;
        if (isBackgroundRecovering) return;

        TrackingState current = arSession.subsystem.trackingState;
        if (current == lastFrameTrackingState) return;

        TrackingState previous = lastFrameTrackingState;
        lastFrameTrackingState = current;

        NotTrackingReason reason = arSession.subsystem.notTrackingReason;

        // Tracking → Limited/None: fallback 진입
        if (previous == TrackingState.Tracking && current != TrackingState.Tracking)
        {
            OffScreenIndicator osi = GetCachedOSI();
            if (osi != null && !osi.IsFallbackMode)
            {
                Debug.Log($"[OSID] 트래킹 Lost → fallback 진입 (reason={reason})");
                osi.EnableFallbackMode(true, GetFallbackConfig(), autoDisable: false);
            }
        }
        // Limited/None → Tracking: 즉시 fallback 해제
        else if (current == TrackingState.Tracking && previous != TrackingState.Tracking)
        {
            if (hasShownEnvironmentGuidance)
            {
                Debug.Log("[OSID] 트래킹 복구 → 환경안내 fallback 해제");
                HideARGuidance();
                hasShownEnvironmentGuidance = false;
            }
            else
            {
                OffScreenIndicator osi = GetCachedOSI();
                if (osi != null && osi.IsFallbackMode)
                {
                    Debug.Log("[OSID] 트래킹 복구 → fallback 해제");
                    osi.EnableFallbackMode(false, forceDisable: true);
                }
            }
        }
    }
    
    public void ShowLoading(System.Action heavyWork, string category = "General")
    {
        if (isLoading) return;
        
        if (forceLoadingForAR || ShouldForceLoading(category))
        {
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
        
    }

    void InitializeARComponents()
    {
        arSession = FindFirstObjectByType<ARSession>();
        if (arSession == null)
        {
            enableAREnvironmentDetection = false;
            return;
        }

        arCameraManager = FindFirstObjectByType<ARCameraManager>();
        arPointCloudManager = FindFirstObjectByType<ARPointCloudManager>();
        arCamera = Camera.main ?? FindFirstObjectByType<Camera>();

        // ARCameraManager 밝기 이벤트 구독
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived += OnCameraFrameReceived;
        }

    }

    void OnDestroy()
    {
        DataManager.OnPreFetchCompleted -= OnDataPreFetchCompleted;
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (args.lightEstimation.averageBrightness.HasValue)
        {
            lastCameraBrightness = args.lightEstimation.averageBrightness.Value;
        }
        else if (args.lightEstimation.averageIntensityInLumens.HasValue)
        {
            // lumen 기반 추정 (1000 lumen = 밝음 ≈ 1.0)
            lastCameraBrightness = Mathf.Clamp01(args.lightEstimation.averageIntensityInLumens.Value / 1000f);
        }
    }
    
    /// <summary>
    /// DataManager 선행 데이터 로드 완료 시 호출 → 0.2초 딜레이 후 fallback 활성화
    /// </summary>
    private bool hasInitialFallbackActivated = false;

    private void OnDataPreFetchCompleted()
    {
        // 최초 데이터 로드 시에만 fallback 활성화
        // RefreshData(업로드 후 새로고침 등)에서는 fallback을 다시 켜지 않음
        if (!hasInitialFallbackActivated)
        {
            hasInitialFallbackActivated = true;

            // fallback 진입 전까지 일반 모드 인디케이터 억제 (화살표 뭉침 방지)
            OffScreenIndicator osi = GetCachedOSI();
            if (osi != null) osi.SetSuppressNormalIndicators(true);

            StartCoroutine(ActivateFallbackAfterDelay(fallbackActivationDelay, initialFallbackDuration));
        }
        else
        {
        }
    }

    /// <summary>
    /// 딜레이 후 fallback 모드 활성화
    /// </summary>
    private IEnumerator ActivateFallbackAfterDelay(float delay, float minDuration)
    {
        yield return new WaitForSeconds(delay);

        OffScreenIndicator osi = GetCachedOSI();
        if (osi == null)
        {
            yield break;
        }

#if !UNITY_EDITOR
        // 첫 설치 시 AR 세션이 아직 준비 안 됐으면 fallback 건너뜀
        // (카메라/위치 권한 승인 대기 중 → subsystem이 null이거나 트래킹 불가)
        // 권한 승인 후 정상 트래킹이 시작되면 fallback 없이 바로 정상 모드로 진입
        bool arReady = arSession != null &&
                       arSession.subsystem != null &&
                       arSession.subsystem.trackingState == TrackingState.Tracking;
        if (!arReady)
        {
            osi.SetSuppressNormalIndicators(false); // 억제 해제
            yield break;
        }
#endif

        // 오브젝트가 아직 스폰 안 됐으면 최대 3초간 대기 (SpawnPreFetchedObjects 진행 중일 수 있음)
        int objCount = dataManager != null ? dataManager.GetSpawnedObjectsCount() : 0;
        if (objCount == 0)
        {
            float waited = 0f;
            while (waited < 3f)
            {
                yield return new WaitForSeconds(0.3f);
                waited += 0.3f;
                objCount = dataManager != null ? dataManager.GetSpawnedObjectsCount() : 0;
                if (objCount > 0) break;
            }
        }

        if (objCount > 0)
        {
            // EnableFallbackMode(true) 내부에서 suppressNormalIndicators = false 처리됨
            osi.SetFallbackMinDuration(minDuration);
            osi.EnableFallbackMode(true, GetFallbackConfig(), autoDisable: true);
        }
        else
        {
            osi.SetSuppressNormalIndicators(false); // 오브젝트 없으면 억제 해제
        }
    }

    private OffScreenIndicator GetCachedOSI()
    {
        if (cachedOSI == null)
            cachedOSI = FindFirstObjectByType<OffScreenIndicator>();
        return cachedOSI;
    }

    IEnumerator StartAREnvironmentMonitoring()
    {
        // fallback은 OnDataPreFetchCompleted에서 데이터 로드 후 활성화됨
        // 여기서는 AR 환경 모니터링만 시작

        // AR 서브시스템 준비 대기 (최대 15초)
        // 첫 설치 시 카메라 권한 승인까지 서브시스템이 null일 수 있음
        float waitTime = 0f;
        float maxSubsystemWait = 15f;
        while (waitTime < maxSubsystemWait)
        {
            if (arSession != null && arSession.subsystem != null)
            {
                if (arSession.subsystem.trackingState == TrackingState.Tracking)
                    break;
            }

            waitTime += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        isCheckingAREnvironment = true;
        CheckAREnvironment();
        StartCoroutine(ForceEnvironmentCheckAfterDelay());

        // 오브젝트가 생성될 때까지 fallback 유지 (tracking 확립 후에도)
        OffScreenIndicator osi = GetCachedOSI();
        if (osi != null && enableDataManagerMonitoring)
        {
            yield return StartCoroutine(WaitForFirstObjectsAndDisableFallback(osi));
        }
    }

    /// <summary>
    /// 오브젝트 로드 상태를 모니터링 (fallback 해제는 OffScreenIndicator 자동 타이머에 위임)
    /// </summary>
    IEnumerator WaitForFirstObjectsAndDisableFallback(OffScreenIndicator osi)
    {
        float maxWait = 60f;
        float waited = 0f;

        while (waited < maxWait)
        {
            // 백그라운드 복귀 중이면 이 코루틴은 간섭하지 않음
            if (isBackgroundRecovering)
            {
                yield return new WaitForSeconds(1f);
                waited += 1f;
                continue;
            }

            int objCount = dataManager != null ? dataManager.GetSpawnedObjectsCount() : 0;
            bool isTracking = arSession?.subsystem?.trackingState == TrackingState.Tracking;
            bool isFB = osi != null && osi.IsFallbackMode;

            // 오브젝트 존재 + AR 트래킹 정상 → fallback 해제
            if (objCount > 0)
            {
#if UNITY_EDITOR
                // 에디터에서는 AR 서브시스템이 없으므로 오브젝트만 있으면 해제
                if (osi != null)
                {
                    if (osi.IsFallbackMode)
                    {
                        osi.EnableFallbackMode(false, forceDisable: true);
                        yield break;
                    }
                }
#else
                if (isTracking)
                {
                    if (osi != null)
                    {
                        if (osi.IsFallbackMode)
                        {
                            // fallback ON 상태 → 즉시 해제
                            osi.EnableFallbackMode(false);
                            yield break;
                        }
                        else
                        {
                            // 레이스 컨디션: 조건 충족이지만 fallback 아직 활성화 안 됨
                            // ActivateFallbackAfterDelay가 아직 실행 안 됐을 수 있음 → 대기
                            float extraWait = 0f;
                            while (extraWait < 3f)
                            {
                                yield return new WaitForSeconds(0.2f);
                                extraWait += 0.2f;
                                waited += 0.2f;
                                if (osi.IsFallbackMode)
                                {
                                    osi.EnableFallbackMode(false);
                                    yield break;
                                }
                            }
                            // 3초 대기해도 fallback 안 켜짐 → autoDisable 타이머에 위임
                            yield break;
                        }
                    }
                    yield break;
                }
                else
                {
                }
                // 트래킹 아직 안 됨 → fallback 유지하면서 계속 대기
#endif
            }

            if (hasShownEnvironmentGuidance)
            {
                while (hasShownEnvironmentGuidance && waited < maxWait)
                {
                    waited += 1f;
                    yield return new WaitForSeconds(1f);
                }
                if (osi != null && osi.IsFallbackMode)
                {
                    osi.EnableFallbackMode(false, forceDisable: true);
                }
                yield break;
            }

            waited += 1f;
            yield return new WaitForSeconds(1f);
        }

        if (osi != null)
        {
            osi.EnableFallbackMode(false, forceDisable: true);
        }
    }
    
    IEnumerator ForceEnvironmentCheckAfterDelay()
    {
        yield return new WaitForSeconds(5f);
        
        if (!isCheckingAREnvironment)
        {
            isCheckingAREnvironment = true;
        }
    }
    
    // 트래킹 Lost → 즉시 fallback 진입 시 사용하는 플래그
    // hasShownEnvironmentGuidance 없이 fallback에 진입한 경우를 추적
    private bool isFallbackWithoutGuidance = false;
    private float lastFallbackDisableTime = 0f; // fallback 해제 시점 (재진입 쿨다운용)


    void CheckAREnvironment()
    {
        if (arSession == null || arSession.subsystem == null)
        {
            // 세션 미초기화 시 fallback 진입하지 않음 (첫 설치 시 권한 대기 중에 fallback 영구 유지 방지)
            return;
        }

        TrackingState currentTrackingState = arSession.subsystem.trackingState;
        AREnvironmentIssue issue = DetermineEnvironmentIssue(currentTrackingState);

        // 트래킹 Lost/Recovery는 CheckTrackingStateChange()에서 매 프레임 처리
        // 여기서는 환경 분석(어둡다/특징점 부족 등)만 담당

        if (issue != AREnvironmentIssue.None)
        {
            HandleEnvironmentIssue(issue);
        }
        else if (hasShownEnvironmentGuidance)
        {
            StopDotAnimation();
            HideARGuidance();
            hasShownEnvironmentGuidance = false;
        }

        lastTrackingState = currentTrackingState;
    }
    
    AREnvironmentIssue DetermineEnvironmentIssue(TrackingState trackingState)
    {
        // 0. ExcessiveMotion(이동 중 흔들림)은 환경 문제가 아니므로 항상 무시
        // 빠른 이동 모드 여부와 무관하게 ExcessiveMotion 시 오브젝트/데이터 로드 유지
        {
            NotTrackingReason reason = NotTrackingReason.None;
            if (arSession?.subsystem != null)
                reason = arSession.subsystem.notTrackingReason;

            if (reason == NotTrackingReason.ExcessiveMotion)
            {
                // 어두움/카메라 가림 같은 진짜 환경 문제만 감지
                if (IsEnvironmentTooDark()) return AREnvironmentIssue.TooDark;
                if (IsCameraCovered()) return AREnvironmentIssue.CameraCovered;
                return AREnvironmentIssue.None;
            }
        }

        // 1. DataManager 상태 먼저 체크 (최우선순위)
        if (enableDataManagerMonitoring && dataManager != null && IsDataManagerHeavyLoading())
        {
            return AREnvironmentIssue.DataLoading;
        }

        // 2. 어두운 환경 체크 (트래킹 상태와 무관하게 우선 체크)
        if (IsEnvironmentTooDark())
        {
            return AREnvironmentIssue.TooDark;
        }

        // 3. 트래킹이 정상이고 환경도 밝으면 문제 없음
        if (trackingState == TrackingState.Tracking)
        {
            trackingLostStartTime = 0f;
            return AREnvironmentIssue.None;
        }

        // 4. DataManager 작업 중이면 환경 감지 지연 (트래킹 정상일 때만 — Limited/None이면 즉시 분석)
        if (enableDataManagerMonitoring && dataManager != null && IsDataManagerRecentlyActive())
        {
            return AREnvironmentIssue.None;
        }

        // 5. 트래킹에 문제가 있으면 환경 분석 (오브젝트 수와 무관)
        if (trackingState == TrackingState.None || trackingState == TrackingState.Limited)
        {
            // 트래킹 손실 시작 시점 기록
            if (lastTrackingState == TrackingState.Tracking || trackingLostStartTime == 0f)
            {
                trackingLostStartTime = Time.realtimeSinceStartup;
            }

            float elapsed = Time.realtimeSinceStartup - trackingLostStartTime;
            if (elapsed > trackingLostTimeout)
            {
                return AnalyzeTrackingIssue();
            }
        }

        return AREnvironmentIssue.None;
    }
    
    AREnvironmentIssue AnalyzeTrackingIssue()
    {
        // NotTrackingReason 활용 (ARFoundation 제공)
        NotTrackingReason reason = NotTrackingReason.None;
        if (arSession?.subsystem != null)
        {
            reason = arSession.subsystem.notTrackingReason;
        }

        // NotTrackingReason 기반 판단
        // ExcessiveMotion은 이동 중 흔들림이므로 환경 문제로 취급하지 않음 (None 반환)
        switch (reason)
        {
            case NotTrackingReason.InsufficientLight:
                return AREnvironmentIssue.TooDark;
            case NotTrackingReason.ExcessiveMotion:
                return AREnvironmentIssue.None; // 이동 중 흔들림은 환경 문제 아님
            case NotTrackingReason.InsufficientFeatures:
                return AREnvironmentIssue.NoFeatures;
            case NotTrackingReason.Unsupported:
                return AREnvironmentIssue.SessionPreparing;
        }

        // NotTrackingReason이 None이거나 Initializing인 경우 밝기/특징점 기반 판단
        if (IsCameraCovered())
        {
            return AREnvironmentIssue.CameraCovered;
        }

        if (IsEnvironmentTooDark())
        {
            return AREnvironmentIssue.TooDark;
        }

        if (GetFeaturePointCount() < minimumFeaturePoints)
        {
            return AREnvironmentIssue.NoFeatures;
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
        return (timeSinceLastChange <= creationTime && objectIncrease >= creationCount);
    }
    
    bool IsDataManagerRecentlyActive()
    {
        if (dataManager == null || !isMonitoringDataManager) return false;
        
        float timeSinceLastChange = Time.realtimeSinceStartup - lastObjectCountChangeTime;
        
        // DataManager 작업 완료 후 1초 동안은 환경 감지 보류
        return timeSinceLastChange <= 1f;
    }
    
    bool HasSufficientObjects()
    {
        if (dataManager == null || !isMonitoringDataManager) return false;
        
        int currentObjectCount = dataManager.GetSpawnedObjectsCount();
        return currentObjectCount >= sufficientObjectCount;
    }
    
    bool IsEnvironmentTooDark()
    {
        float averageBrightness = GetAverageBrightness();
        return averageBrightness < darkEnvironmentThreshold;
    }
    
    float GetAverageBrightness()
    {
        // 실제 ARCameraManager 밝기 사용
        if (lastCameraBrightness.HasValue)
        {
            return lastCameraBrightness.Value;
        }

        // fallback: 밝기 데이터 없을 때 트래킹 상태로 추정
        if (arSession?.subsystem?.trackingState == TrackingState.Tracking)
        {
            return 0.7f;
        }
        else if (GetFeaturePointCount() > minimumFeaturePoints)
        {
            return 0.6f;
        }

        return 0.3f; // 밝기 데이터 없고 트래킹도 안 되면 어둡다고 판단
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
    
    void HandleEnvironmentIssue(AREnvironmentIssue issue)
    {
        Debug.Log($"[OSID] HandleEnvironmentIssue: {issue}, hasShown={hasShownEnvironmentGuidance}");
        if (hasShownEnvironmentGuidance)
        {
            return;
        }

        hasShownEnvironmentGuidance = true;
        isFallbackWithoutGuidance = false; // 환경안내가 fallback 관리를 인계

        // 환경 이슈 → fallback 모드 진입 (오브젝트 뭉침 방지)
        // 이미 CheckTrackingStateChange에서 fallback 진입했으면 중복 진입하지 않음
        // DataLoading은 제외 — 데이터 로드 중에는 오브젝트가 아직 없으므로 뭉침 없음
        if (issue != AREnvironmentIssue.DataLoading)
        {
            OffScreenIndicator osi = GetCachedOSI();
            if (osi != null && !osi.IsFallbackMode)
            {
                Debug.Log($"[OSID] 환경 이슈 → fallback 진입: {issue}");
                osi.EnableFallbackMode(true, GetFallbackConfig(), autoDisable: false);
            }
        }

        if (issue == AREnvironmentIssue.SessionPreparing)
        {
            // SessionPreparing: 점 애니메이션 + 즉시 표시
            string baseMessage = GetEnvironmentGuidanceMessage(issue);
            if (loadingPanel) loadingPanel.SetActive(true);
            StartSpinner();
            StartDotAnimation(baseMessage);
            StartCoroutine(AutoRetryEnvironmentCheck(issue));
        }
        else if (issue == AREnvironmentIssue.DataLoading && enableImmediateDataManagerUI)
        {
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
            string guidanceMessage = GetEnvironmentGuidanceMessage(issue);
            ShowAREnvironmentGuidance(guidanceMessage, issue);
        }
        else if (currentIssue == AREnvironmentIssue.None)
        {
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
            [AREnvironmentIssue.ExcessiveMotion] = new Dictionary<string, string>
            {
                ["ko"] = "기기를 너무 빠르게 움직이고 있습니다.\n천천히 움직여주세요.",
                ["en"] = "Moving too fast.\nPlease move the device slowly.",
                ["zh"] = "设备移动过快。\n请缓慢移动。",
                ["ja"] = "デバイスの動きが速すぎます。\nゆっくり動かしてください。",
                ["es"] = "Movimiento demasiado rápido.\nPor favor, mueve el dispositivo lentamente."
            },
            [AREnvironmentIssue.DataLoading] = new Dictionary<string, string>
            {
                ["ko"] = "AR 오브젝트 처리 중입니다.\n잠시만 기다려주세요.",
                ["en"] = "Processing AR objects.\nPlease wait a moment.",
                ["zh"] = "正在处理AR对象。\n请稍等片刻。",
                ["ja"] = "ARオブジェクトを処理中です。\n少々お待ちください。",
                ["es"] = "Procesando objetos AR.\nPor favor, espera un momento."
            },
            [AREnvironmentIssue.SessionPreparing] = new Dictionary<string, string>
            {
                ["ko"] = "AR 세션 작동 준비 중",
                ["en"] = "Preparing AR session",
                ["zh"] = "正在准备AR会话",
                ["ja"] = "ARセッション準備中",
                ["es"] = "Preparando sesión AR"
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
        // 기존 로딩 UI만 사용 (스피너 포함)
        if (loadingPanel) loadingPanel.SetActive(true);
        StartSpinner();

        // SessionPreparing/DataLoading은 점 애니메이션 적용
        if (issue == AREnvironmentIssue.SessionPreparing || issue == AREnvironmentIssue.DataLoading)
        {
            StartDotAnimation(message);
        }
        else
        {
            UpdateMessage(message);
        }

        // DataLoading이 아닌 경우에만 AutoRetry 시작 (이미 시작된 경우 중복 방지)
        if (issue != AREnvironmentIssue.DataLoading)
        {
            StartCoroutine(AutoRetryEnvironmentCheck(issue));
        }
    }
    
    IEnumerator AutoRetryEnvironmentCheck(AREnvironmentIssue issue)
    {
        // 환경 복구(None)될 때까지 계속 모니터링 — 타임아웃으로 리셋하지 않음
        // 안내 텍스트만 일정 시간 후 숨기고, fallback 모드는 환경 복구까지 유지
        float textDisplayTime = 10f;
        float elapsed = 0f;
        bool textHidden = false;

        while (hasShownEnvironmentGuidance)
        {
            yield return new WaitForSeconds(1f);
            elapsed += 1f;

            AREnvironmentIssue currentIssue = DetermineEnvironmentIssue(
                arSession?.subsystem?.trackingState ?? TrackingState.None);

            if (currentIssue == AREnvironmentIssue.None)
            {
                // 환경 복구 → 안내 숨기고 fallback 해제
                Debug.Log("[OSID] AutoRetry: 환경 복구 → fallback 해제");
                HideARGuidance();
                hasShownEnvironmentGuidance = false;
                break;
            }
            else if (currentIssue != issue)
            {
                // 환경 이슈 종류 변경 → 안내 텍스트 업데이트
                string newGuidanceMessage = GetEnvironmentGuidanceMessage(currentIssue);
                UpdateMessage(newGuidanceMessage);
                issue = currentIssue;
                // 텍스트 타이머 리셋
                elapsed = 0f;
                textHidden = false;
                if (loadingPanel) loadingPanel.SetActive(true);
            }

            // 안내 텍스트만 일정 시간 후 숨김 (fallback 모드는 유지)
            if (!textHidden && elapsed >= textDisplayTime)
            {
                StopDotAnimation();
                StopSpinner();
                if (loadingPanel) loadingPanel.SetActive(false);
                textHidden = true;
                Debug.Log("[OSID] AutoRetry: 안내 텍스트 숨김 (fallback 유지)");
            }
        }
    }
    
    /// <summary>
    /// fallback 해제 시 모든 매니저의 오브젝트에 거리 필터 적용
    /// 200m 밖 오브젝트를 SetActive(false)로 비활성화
    /// </summary>
    private void RestoreAllManagerObjects()
    {
        float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
        float lat = 0f, lon = 0f;
#if UNITY_EDITOR
        if (VirtualLocation.Instance != null) { lat = VirtualLocation.Instance.Latitude; lon = VirtualLocation.Instance.Longitude; }
#else
        if (Input.location.status == LocationServiceStatus.Running) { lat = Input.location.lastData.latitude; lon = Input.location.lastData.longitude; }
#endif
        Debug.Log($"[OSID] RestoreAllManagerObjects: maxDist={maxDist}, lat={lat}, lon={lon}");
        if (dataManager != null) dataManager.UpdateDistanceFilter(maxDist, lat, lon);
        if (TourAPIManager.Instance != null) TourAPIManager.Instance.UpdateDistanceFilter(maxDist, lat, lon);
        if (SubwayManager.Instance != null) SubwayManager.Instance.UpdateDistanceFilter(maxDist, lat, lon);
        if (TerminalManager.Instance != null) TerminalManager.Instance.UpdateDistanceFilter(maxDist, lat, lon);
        if (TrainStationManager.Instance != null) TrainStationManager.Instance.UpdateDistanceFilter(maxDist, lat, lon);
        if (BusStationManager.Instance != null) BusStationManager.Instance.UpdateDistanceFilter(maxDist, lat, lon);
    }

    void HideARGuidance()
    {
        StopDotAnimation();
        StopSpinner();
        if (loadingPanel) loadingPanel.SetActive(false);

        // 환경 복구 → 모든 매니저 오브젝트 거리 필터 적용하며 복원
        RestoreAllManagerObjects();

        // fallback 모드 해제
        OffScreenIndicator osi = GetCachedOSI();
        if (osi != null && osi.IsFallbackMode)
        {
            Debug.Log("[OSID] HideARGuidance → fallback 해제");
            osi.EnableFallbackMode(false, forceDisable: true);
        }
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
            // ⭐ Singleton 패턴 사용으로 최적화 (FindObjectOfType 제거)
            dataManager = DataManager.Instance;

            // Instance가 아직 없으면 잠시 대기
            int maxWait = 10;
            while (dataManager == null && maxWait > 0)
            {
                yield return new WaitForSeconds(0.5f);
                dataManager = DataManager.Instance;
                maxWait--;
            }
        }

        if (dataManager != null)
        {
            lastObjectCount = dataManager.GetSpawnedObjectsCount();
            lastObjectCountChangeTime = Time.realtimeSinceStartup;
            isMonitoringDataManager = true;
        }
    }
    
    // 임계값 만족시 즉시 UI 표시
    void CheckARObjectChanges()
    {
        int currentObjectCount = dataManager.GetSpawnedObjectsCount();

        if (currentObjectCount != lastObjectCount)
        {
            int objectChange = currentObjectCount - lastObjectCount;
            float timeSinceLastChange = Time.realtimeSinceStartup - lastObjectCountChangeTime;

            if (objectChange > 0)
            {
                // 임계값 조건 만족시 즉시 UI 표시
                if (enableImmediateDataManagerUI &&
                    timeSinceLastChange <= creationTime &&
                    objectChange >= creationCount)
                {
                    if (!hasShownEnvironmentGuidance && !isLoading)
                    {
                        hasShownEnvironmentGuidance = true;
                        string message = GetEnvironmentGuidanceMessage(AREnvironmentIssue.DataLoading);
                        ShowAREnvironmentGuidance(message, AREnvironmentIssue.DataLoading);
                        StartCoroutine(AutoRetryEnvironmentCheck(AREnvironmentIssue.DataLoading));
                    }
                }
            }

            lastObjectCount = currentObjectCount;
            lastObjectCountChangeTime = Time.realtimeSinceStartup;
        }
    }
    
    void TriggerARObjectLoading(string customMessage, int objectCount)
    {
        if (isLoading || !enableDataManagerMonitoring) return;
        
        if (Time.realtimeSinceStartup - lastLoadingTime < loadingCooldown)
        {
            return;
        }

        lastLoadingTime = Time.realtimeSinceStartup;

        ShowARLoading(() => { }, customMessage);
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
        StartSpinner();
    }
    
    void HideLoadingUI()
    {
        if (loadingPanel) loadingPanel.SetActive(false);
        StopSpinner();
        StopDotAnimation();
        // StopAllCoroutines 사용 금지 — HandleBackgroundRecovery, CheckAREnvironment 등이 중단됨
    }
    
    void UpdateMessage(string message)
    {
        if (loadingText) loadingText.text = message;
    }
    
    void StartSpinner()
    {
        if (spinnerCoroutine != null) return; // 중복 방지
        if (loadingSpinner) spinnerCoroutine = StartCoroutine(SpinnerAnimation());
    }

    void StopSpinner()
    {
        if (spinnerCoroutine != null)
        {
            StopCoroutine(spinnerCoroutine);
            spinnerCoroutine = null;
        }
    }

    IEnumerator SpinnerAnimation()
    {
        while (loadingPanel && loadingPanel.activeInHierarchy && loadingSpinner)
        {
            loadingSpinner.transform.Rotate(0, 0, -90 * Time.deltaTime);
            yield return null;
        }
        spinnerCoroutine = null; // 자연 종료 시 정리
    }

    /// <summary>
    /// 점(.) 하나씩 추가되는 애니메이션 (위치 고정: 보이는 점 + 투명 점으로 총 3자리 유지)
    /// </summary>
    IEnumerator DotAnimation(string baseMessage)
    {
        int dotCount = 0;
        while (true)
        {
            dotCount = (dotCount % 3) + 1;
            string visibleDots = new string('.', dotCount);
            // 나머지 점은 투명 색상으로 채워서 전체 폭 고정 (텍스트 흔들림 방지)
            int invisibleCount = 3 - dotCount;
            string invisibleDots = invisibleCount > 0
                ? $"<color=#00000000>{new string('.', invisibleCount)}</color>"
                : "";
            UpdateMessage(baseMessage + visibleDots + invisibleDots);
            yield return new WaitForSeconds(0.6f);
        }
    }

    void StartDotAnimation(string baseMessage)
    {
        StopDotAnimation();
        dotAnimationCoroutine = StartCoroutine(DotAnimation(baseMessage));
    }

    void StopDotAnimation()
    {
        if (dotAnimationCoroutine != null)
        {
            StopCoroutine(dotAnimationCoroutine);
            dotAnimationCoroutine = null;
        }
    }

    /// <summary>
    /// "AR 세션 복구 중" 다국어 메시지
    /// </summary>
    string GetSessionRecoveringMessage()
    {
        switch (currentLanguage)
        {
            case "ko": return "AR 세션 복구 중";
            case "ja": return "ARセッション復旧中";
            case "zh": return "AR会话恢复中";
            case "es": return "Recuperando sesión AR";
            default:   return "Recovering AR session";
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
        if (dataManager == null) return;

        ShowARLoading(() => { }, operation);
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
    }
    
    /// <summary>
    /// DataManager 임계값 설정 (Creation Time과 Creation Count)
    /// </summary>
    public void SetDataManagerThreshold(float time, int count)
    {
        creationTime = time;
        creationCount = count;
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
    

    // ============================================================
    // 에디터 테스트용 public 메서드 (Inspector ContextMenu + Custom Editor 버튼)
    // ============================================================

    /// <summary>
    /// 테스트: AR 세션 준비 중 (폴백 모드 + 점 애니메이션)
    /// </summary>
    [ContextMenu("Test: Session Preparing (폴백모드)")]
    public void DebugSessionPreparing()
    {
        hasShownEnvironmentGuidance = false;
        HandleEnvironmentIssue(AREnvironmentIssue.SessionPreparing);
    }

    [ContextMenu("Test: Fallback ON")]
    public void DebugFallbackOn()
    {
        OffScreenIndicator osi = GetCachedOSI();
        if (osi != null) osi.EnableFallbackMode(true, GetFallbackConfig());
    }

    [ContextMenu("Test: Fallback OFF")]
    public void DebugFallbackOff()
    {
        OffScreenIndicator osi = GetCachedOSI();
        if (osi != null) osi.EnableFallbackMode(false, forceDisable: true);
    }

    /// <summary>
    /// Inspector 설정값으로 FallbackConfig 생성
    /// </summary>
    private OffScreenIndicator.FallbackConfig GetFallbackConfig()
    {
        return new OffScreenIndicator.FallbackConfig
        {
            baseScaleMultiplier = fallbackBaseScaleMultiplier,
            scaleRandomMin = fallbackScaleRandomMin,
            scaleRandomMax = fallbackScaleRandomMax,
            pulseSpeed = fallbackPulseSpeed,
            pulseAmplitude = fallbackPulseAmplitude,
            marginTop = fallbackMarginTop,
            marginBottom = fallbackMarginBottom,
            marginLeft = fallbackMarginLeft,
            marginRight = fallbackMarginRight,
            maxIndicatorCount = fallbackMaxIndicatorCount
        };
    }

    [ContextMenu("Test: Hide Guidance (복구)")]
    public void DebugHideGuidance()
    {
        HideARGuidance();
        hasShownEnvironmentGuidance = false;
    }

    [ContextMenu("Test: Dark Environment")]
    public void DebugDarkEnvironment()
    {
        hasShownEnvironmentGuidance = false;
        HandleEnvironmentIssue(AREnvironmentIssue.TooDark);
    }

    [ContextMenu("Test: No Features")]
    public void DebugNoFeatures()
    {
        hasShownEnvironmentGuidance = false;
        HandleEnvironmentIssue(AREnvironmentIssue.NoFeatures);
    }

    [ContextMenu("Test: Camera Covered")]
    public void DebugCameraCovered()
    {
        hasShownEnvironmentGuidance = false;
        HandleEnvironmentIssue(AREnvironmentIssue.CameraCovered);
    }

    [ContextMenu("Test: Data Loading")]
    public void DebugDataLoading()
    {
        hasShownEnvironmentGuidance = false;
        HandleEnvironmentIssue(AREnvironmentIssue.DataLoading);
    }

    [ContextMenu("Test: Background Recovery")]
    public void DebugBackgroundRecovery()
    {
        StartCoroutine(HandleBackgroundRecovery());
    }
}