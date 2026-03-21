using PixelPlay.OffScreenIndicator;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

[DefaultExecutionOrder(-1)]
public class OffScreenIndicator : MonoBehaviour
{
    [Range(0.1f, 0.9f)]
    [Tooltip("Horizontal distance offset of the indicators from the center of the screen")]
    [SerializeField] private float screenBoundOffsetX = 0.9f;

    [Range(0.1f, 0.9f)]
    [Tooltip("Vertical distance offset of the indicators from the center of the screen")]
    [SerializeField] private float screenBoundOffsetY = 0.9f;

    // 추가: 상하좌우 추가 경계 값 (픽셀 단위)
    [Tooltip("Additional top boundary offset in pixels")]
    [SerializeField] private float additionalBoundOffsetTop = 0f;

    [Tooltip("Additional bottom boundary offset in pixels")]
    [SerializeField] private float additionalBoundOffsetBottom = 0f;

    [Tooltip("Additional left boundary offset in pixels")]
    [SerializeField] private float additionalBoundOffsetLeft = 0f;

    [Tooltip("Additional right boundary offset in pixels")]
    [SerializeField] private float additionalBoundOffsetRight = 0f;

    private Camera mainCamera;
    private Vector3 screenCentre;
    private Vector3 screenBoundsX;
    private Vector3 screenBoundsY;

    // Canvas 좌표 변환용
    private Canvas parentCanvas;
    private RectTransform canvasRectTransform;
    private RectTransform panelRectTransform;

    private List<Target> targets = new List<Target>();
    private HashSet<Target> disabledFallbackTargets = new HashSet<Target>(); // fallback 중 비활성화된 타겟 보존

    public static Action<Target, bool> TargetStateChanged;

    // ============================================================
    // Fallback Mode - AR 세션 미작동 시 화살표 분산 배치 + 펄스
    // ============================================================
    private bool isFallbackMode = false;
    public bool IsFallbackMode => isFallbackMode;
    public int GetActiveTargetCount() => targets.Count;

    // fallback 진입 전 일반 모드 인디케이터 억제 (앱 시작 시 화살표 뭉침 방지)
    private bool suppressNormalIndicators = false;
    private Dictionary<Target, FallbackData> fallbackDataMap = new Dictionary<Target, FallbackData>();
    private FallbackConfig currentFallbackConfig;
    private float fallbackStartTime = 0f; // fallback 모드 시작 시간 (realtimeSinceStartup)
    private float fallbackStartTimeScaled = 0f; // fallback 모드 시작 시간 (Time.time)

    // ============================================================
    // Fallback → 정상 전환 애니메이션
    // ============================================================
    private bool isTransitioning = false;
    private float transitionStartTime = 0f;

    [Header("=== Fallback 전환 설정 ===")]
    [Tooltip("Fallback 최소 유지 시간 (초) — 이 시간 전에 해제 요청이 오면 대기")]
    [SerializeField] private float fallbackMinDuration = 2f;

    [Tooltip("Fallback 오프닝 애니메이션 시간 (초) — 중앙에서 모서리로 퍼져나감")]
    [SerializeField] private float fallbackOpeningDuration = 1f;

    [Tooltip("Fallback → 정상 전환 시 보간 시간 (초)")]
    [SerializeField] private float fallbackTransitionDuration = 0.6f;

    [Tooltip("Fallback → Box 전환 시 fade-out 시간 (초)")]
    [SerializeField] private float fallbackFadeOutDuration = 0.4f;

    // 전환 중 각 인디케이터의 시작 위치/스케일/회전 저장
    private class TransitionData
    {
        public Vector3 startPosition;
        public Vector3 startScale;
        public Quaternion startRotation;
    }
    private Dictionary<Target, TransitionData> transitionDataMap = new Dictionary<Target, TransitionData>();
    private HashSet<Target> fadeOutTargets = new HashSet<Target>(); // box 전환으로 fade-out 대상

    // 화살표 겹침 방지용 최소 간격 (Canvas 픽셀 단위)
    [Header("=== 화살표 겹침 방지 ===")]
    [Tooltip("화살표 간 최소 간격 (Canvas 픽셀)")]
    [SerializeField] private float arrowMinSpacing = 80f;

    [Header("=== 인디케이터 거리 제한 ===")]
    [Tooltip("이 거리(m) 이상의 오브젝트는 인디케이터 표시 안함 (0이면 제한 없음)")]
    [SerializeField] private float maxIndicatorDistance = 0f;

    [Header("=== 화살표 위치 스무딩 ===")]
    [Tooltip("화살표 위치 보간 속도 (높을수록 빠르게 이동, 0이면 즉시)")]
    [SerializeField] private float arrowSmoothSpeed = 8f;

    // 각 타겟의 이전 프레임 스크린 위치/각도 캐시 (스무딩용)
    private Dictionary<Target, Vector3> previousArrowScreenPositions = new Dictionary<Target, Vector3>();
    private Dictionary<Target, float> previousArrowAngles = new Dictionary<Target, float>();

    private struct ArrowInfo
    {
        public Target target;
        public Vector3 screenPosition;
        public float angle;
        public float distanceFromCamera;
        public bool isArrow;
        public bool skipThisFrame;
    }

    /// <summary>
    /// 폴백 모드 설정값 (LoadingManager에서 전달)
    /// </summary>
    public class FallbackConfig
    {
        public float baseScaleMultiplier = 1.5f;      // 기본 스케일 배수
        public float scaleRandomMin = 0.85f;           // 스케일 랜덤 최소
        public float scaleRandomMax = 1.3f;            // 스케일 랜덤 최대
        public float pulseSpeed = 0.8f;                // 펄스 속도
        public float pulseAmplitude = 0.15f;           // 펄스 진폭
        public float marginTop = 0.08f;                // 상단 마진 (비율)
        public float marginBottom = 0.05f;             // 하단 마진
        public float marginLeft = 0.05f;               // 좌측 마진
        public float marginRight = 0.05f;              // 우측 마진
        public int maxIndicatorCount = 10;             // 최대 화살표 표시 개수
    }

    private class FallbackData
    {
        public Vector3 assignedPosition;    // 모서리 위의 Canvas 월드 좌표
        public float assignedAngle;         // 화살표 회전각
        public float baseScale;             // 기본 스케일 (랜덤)
        public float pulsePhaseOffset;      // 펄스 애니메이션 위상 오프셋
    }

    void Awake()
    {
        mainCamera = FindFirstObjectByType<ARCameraManager>()?.GetComponent<Camera>() ?? Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[OSID] AR 카메라 또는 메인 카메라를 찾을 수 없습니다!");
            return;
        }

        screenCentre = new Vector3(Screen.width, Screen.height, 0) / 2;
        screenBoundsX = screenCentre * screenBoundOffsetX;
        screenBoundsY = screenCentre * screenBoundOffsetY;

        // Canvas 좌표 변환을 위해 부모 Canvas 캐싱
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            canvasRectTransform = parentCanvas.GetComponent<RectTransform>();

        // 자신의 RectTransform 캐싱
        panelRectTransform = GetComponent<RectTransform>();

        TargetStateChanged += HandleTargetStateChanged;

        // PlaceListManager의 거리 슬라이더 값과 동기화
        if (maxIndicatorDistance <= 0f)
        {
            maxIndicatorDistance = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
        }
    }

    /// <summary>
    /// 인디케이터 최대 표시 거리 설정 (PlaceListManager 거리 슬라이더 연동용)
    /// </summary>
    public void SetMaxIndicatorDistance(float distance)
    {
        maxIndicatorDistance = distance;
    }

    /// <summary>
    /// Screen 픽셀 좌표를 Canvas 월드 좌표로 변환
    /// CanvasScaler가 있으면 Screen 좌표와 Canvas 좌표가 다를 수 있음
    /// </summary>
    private Vector3 ScreenToCanvasWorldPosition(Vector3 screenPos)
    {
        if (parentCanvas == null || canvasRectTransform == null)
            return screenPos;

        Camera cam = null;
        if (parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = parentCanvas.worldCamera ?? mainCamera;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform, new Vector2(screenPos.x, screenPos.y),
            cam, out localPoint);
        return canvasRectTransform.TransformPoint(localPoint);
    }

    /// <summary>
    /// Canvas의 논리적 크기를 반환 (CanvasScaler 적용됨, 예: 1440x3200)
    /// 폴백 모드에서 화면 경계 계산에 사용
    /// </summary>
    private Vector2 GetCanvasSize()
    {
        if (canvasRectTransform != null)
            return canvasRectTransform.rect.size;
        return new Vector2(Screen.width, Screen.height);
    }

    /// <summary>
    /// Canvas 논리적 좌표(anchoredPosition 기준)를 월드 좌표로 변환
    /// Canvas pivot(0.5, 0.5) 기준: (0,0) = 화면 중앙
    /// </summary>
    private Vector3 CanvasLocalToWorldPosition(Vector2 canvasLocalPos)
    {
        if (canvasRectTransform == null)
            return new Vector3(canvasLocalPos.x, canvasLocalPos.y, 0);
        return canvasRectTransform.TransformPoint(canvasLocalPos);
    }

    void LateUpdate()
    {
        DrawIndicators();
    }

    void DrawIndicators()
    {
        if (isFallbackMode)
        {
            DrawFallbackIndicators();
            return;
        }

        // fallback 대기 중: 일반 모드 인디케이터 억제 (앱 시작 시 화살표 뭉침 방지)
        if (suppressNormalIndicators)
            return;

        // 전환 중 진행률 계산
        float transitionT = 1f;
        if (isTransitioning)
        {
            transitionT = Mathf.Clamp01((Time.time - transitionStartTime) / fallbackTransitionDuration);
            // EaseInOutCubic 커브로 부드러운 전환
            transitionT = transitionT < 0.5f
                ? 4f * transitionT * transitionT * transitionT
                : 1f - Mathf.Pow(-2f * transitionT + 2f, 3f) / 2f;

            if (transitionT >= 1f)
            {
                isTransitioning = false;
                transitionDataMap.Clear();
                fadeOutTargets.Clear();
            }
        }

        // ── Pass 1: 각 타겟의 스크린 위치/각도/타입 수집 ──
        List<ArrowInfo> arrowInfos = new List<ArrowInfo>();

        foreach (Target target in targets)
        {
            // 앵커가 아직 생성되지 않은 오브젝트는 위치가 원점(0,0,0)이므로 표시 생략
            if (!target.IsAnchorReady)
                continue;

            Vector3 screenPosition = OffScreenIndicatorCore.GetScreenPosition(mainCamera, target.transform.position);
            bool isTargetVisible = OffScreenIndicatorCore.IsTargetVisible(screenPosition);
            float distanceFromCamera = target.GetDistanceFromCamera(mainCamera.transform.position);

            // 거리 필터: maxIndicatorDistance 밖의 타겟은 인디케이터 표시 안 함
            // 카메라 거리 + GPS 거리 이중 체크 (트래킹 Lost 시 카메라 거리가 부정확할 수 있음)
            if (maxIndicatorDistance > 0f)
            {
                bool outOfRange = distanceFromCamera > maxIndicatorDistance;
                // GPS 거리도 체크 (GPS가 유효한 경우)
                if (!outOfRange && Input.location.status == LocationServiceStatus.Running)
                {
                    float gpsDist = target.GetGPSDistance(Input.location.lastData.latitude, Input.location.lastData.longitude);
                    if (gpsDist >= 0f && gpsDist > maxIndicatorDistance) outOfRange = true;
                }
                if (outOfRange)
                {
                    if (target.indicator != null)
                    {
                        target.indicator.Activate(false);
                        target.indicator = null;
                    }
                    continue;
                }
            }

            if (!target.NeedDistanceText)
                distanceFromCamera = float.MinValue;

            // 전환 중 fade-out 대상 (화살표→box 전환): 기존 화살표를 fade-out
            if (isTransitioning && fadeOutTargets.Contains(target))
            {
                if (target.indicator != null)
                {
                    float fadeT = Mathf.Clamp01((Time.time - transitionStartTime) / fallbackFadeOutDuration);
                    target.indicator.SetAlpha(1f - fadeT);

                    if (fadeT >= 1f)
                    {
                        target.indicator.SetAlpha(1f);
                        target.indicator.ResetForPool();
                        target.indicator.Activate(false);
                        target.indicator = null;
                        fadeOutTargets.Remove(target);
                    }
                    else
                    {
                        arrowInfos.Add(new ArrowInfo { target = target, skipThisFrame = true });
                        continue;
                    }
                }
            }

            if (target.NeedBoxIndicator && isTargetVisible)
            {
                screenPosition.z = 0;
                arrowInfos.Add(new ArrowInfo
                {
                    target = target, screenPosition = screenPosition, angle = 0f,
                    distanceFromCamera = distanceFromCamera, isArrow = false, skipThisFrame = false
                });
            }
            else if (target.NeedArrowIndicator && !isTargetVisible)
            {
                float angle = float.MinValue;
                OffScreenIndicatorCore.GetArrowIndicatorPositionAndAngle(ref screenPosition, ref angle, screenCentre, screenBoundsX);

                float limitX = screenCentre.x * screenBoundOffsetX - additionalBoundOffsetLeft;
                float limitXRight = screenCentre.x * screenBoundOffsetX - additionalBoundOffsetRight;
                float limitY = screenCentre.y * screenBoundOffsetY - additionalBoundOffsetBottom;
                float limitYTop = screenCentre.y * screenBoundOffsetY - additionalBoundOffsetTop;

                screenPosition.x = Mathf.Clamp(screenPosition.x, screenCentre.x - limitX, screenCentre.x + limitXRight);
                screenPosition.y = Mathf.Clamp(screenPosition.y, screenCentre.y - limitY, screenCentre.y + limitYTop);

                arrowInfos.Add(new ArrowInfo
                {
                    target = target, screenPosition = screenPosition, angle = angle,
                    distanceFromCamera = distanceFromCamera, isArrow = true, skipThisFrame = false
                });
            }
        }

        // ── Pass 1.5: 화살표 겹침 해소 ──
        ResolveArrowOverlap(arrowInfos);

        // ── Pass 1.7: 화살표 위치/각도 스무딩 (진동/텔레포트 방지) ──
        if (arrowSmoothSpeed > 0f)
        {
            float lerpT = Mathf.Clamp01(Time.deltaTime * arrowSmoothSpeed);
            HashSet<Target> activeArrowTargets = new HashSet<Target>();
            for (int i = 0; i < arrowInfos.Count; i++)
            {
                ArrowInfo info = arrowInfos[i];
                if (info.skipThisFrame || !info.isArrow) continue;

                activeArrowTargets.Add(info.target);

                // 위치 스무딩
                if (previousArrowScreenPositions.TryGetValue(info.target, out Vector3 prevPos))
                {
                    info.screenPosition = Vector3.Lerp(prevPos, info.screenPosition, lerpT);
                }
                previousArrowScreenPositions[info.target] = info.screenPosition;

                // 각도 스무딩 (라디안 → 도 변환 후 LerpAngle → 라디안 복원)
                if (previousArrowAngles.TryGetValue(info.target, out float prevAngle))
                {
                    float prevDeg = prevAngle * Mathf.Rad2Deg;
                    float curDeg = info.angle * Mathf.Rad2Deg;
                    info.angle = Mathf.LerpAngle(prevDeg, curDeg, lerpT) * Mathf.Deg2Rad;
                }
                previousArrowAngles[info.target] = info.angle;

                arrowInfos[i] = info;
            }
            // 더 이상 화살표가 아닌 타겟은 캐시에서 제거
            var keysToRemove = new List<Target>();
            foreach (var kvp in previousArrowScreenPositions)
            {
                if (!activeArrowTargets.Contains(kvp.Key))
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var key in keysToRemove)
            {
                previousArrowScreenPositions.Remove(key);
                previousArrowAngles.Remove(key);
            }
        }

        // ── Pass 2: 인디케이터 생성/업데이트 ──
        for (int i = 0; i < arrowInfos.Count; i++)
        {
            ArrowInfo info = arrowInfos[i];
            if (info.skipThisFrame) continue;

            Target target = info.target;
            Indicator indicator = null;

            if (!info.isArrow)
            {
                indicator = GetIndicator(ref target.indicator, IndicatorType.BOX, target, info.screenPosition);
            }
            else
            {
                indicator = GetIndicator(ref target.indicator, IndicatorType.ARROW, target, info.screenPosition);
                indicator.transform.rotation = Quaternion.Euler(0, 0, info.angle * Mathf.Rad2Deg);
            }

            if (indicator)
            {
                indicator.SetImageColor(target.TargetColor);
                if (target.NeedDistanceText)
                {
                    indicator.SetDistanceText(info.distanceFromCamera, target.DistanceTextColor, target.PlaceName);
                }
                else
                {
                    indicator.SetDistanceText(float.MinValue, Color.clear, "");
                }

                Vector3 targetPosition = ScreenToCanvasWorldPosition(info.screenPosition);
                indicator.SetTextRotation(Quaternion.identity);

                float size;
                if (indicator.Type == IndicatorType.BOX)
                {
                    if (info.distanceFromCamera <= target.MinDistance)
                        size = target.MaxBoxSize;
                    else if (info.distanceFromCamera >= target.MaxDistance)
                        size = target.DefaultBoxSize;
                    else
                    {
                        float t = (target.MaxDistance - info.distanceFromCamera) / (target.MaxDistance - target.MinDistance);
                        size = Mathf.Lerp(target.DefaultBoxSize, target.MaxBoxSize, t);
                    }
                    indicator.SetScale(new Vector3(size, size, size));
                }
                else
                {
                    if (info.distanceFromCamera <= target.MinDistance)
                        size = target.MaxArrowSize;
                    else if (info.distanceFromCamera >= target.MaxDistance)
                        size = target.DefaultArrowSize;
                    else
                    {
                        float t = (target.MaxDistance - info.distanceFromCamera) / (target.MaxDistance - target.MinDistance);
                        size = Mathf.Lerp(target.DefaultArrowSize, target.MaxArrowSize, t);
                    }
                    indicator.SetScale(new Vector3(size, size, 1f));
                }

                // 전환 중: fallback 위치에서 실제 위치로 보간
                if (isTransitioning && transitionDataMap.ContainsKey(target) && indicator.Type == IndicatorType.ARROW)
                {
                    TransitionData td = transitionDataMap[target];
                    Vector3 targetScale = indicator.transform.localScale;
                    Quaternion targetRotation = indicator.transform.rotation;

                    indicator.transform.position = Vector3.Lerp(td.startPosition, targetPosition, transitionT);
                    indicator.transform.localScale = Vector3.Lerp(td.startScale, targetScale, transitionT);
                    indicator.transform.rotation = Quaternion.Slerp(td.startRotation, targetRotation, transitionT);
                }
                else
                {
                    indicator.transform.position = targetPosition;
                }
            }
        }
    }

    /// <summary>
    /// 화살표 간 최소 간격을 유지하도록 겹치는 화살표를 화면 가장자리를 따라 분산
    /// </summary>
    void ResolveArrowOverlap(List<ArrowInfo> infos)
    {
        if (arrowMinSpacing <= 0f) return;

        // 화살표만 필터링 (index 유지)
        List<int> arrowIndices = new List<int>();
        for (int i = 0; i < infos.Count; i++)
        {
            if (infos[i].isArrow && !infos[i].skipThisFrame)
                arrowIndices.Add(i);
        }

        if (arrowIndices.Count < 2) return;

        float spacingSq = arrowMinSpacing * arrowMinSpacing;

        // 반복적으로 겹침 해소 (최대 3회)
        for (int pass = 0; pass < 3; pass++)
        {
            bool anyMoved = false;

            for (int a = 0; a < arrowIndices.Count; a++)
            {
                for (int b = a + 1; b < arrowIndices.Count; b++)
                {
                    ArrowInfo infoA = infos[arrowIndices[a]];
                    ArrowInfo infoB = infos[arrowIndices[b]];

                    float dx = infoA.screenPosition.x - infoB.screenPosition.x;
                    float dy = infoA.screenPosition.y - infoB.screenPosition.y;
                    float distSq = dx * dx + dy * dy;

                    if (distSq < spacingSq)
                    {
                        // 겹침 발생 — 화면 가장자리를 따라 분산
                        float dist = Mathf.Sqrt(distSq);
                        float overlap = arrowMinSpacing - dist;
                        float halfPush = overlap * 0.5f + 1f;

                        // 방향 벡터 (동일 위치면 임의 방향)
                        Vector2 dir;
                        if (dist < 0.1f)
                        {
                            float randAngle = (a * 137.5f + b * 59.3f) % 360f * Mathf.Deg2Rad;
                            dir = new Vector2(Mathf.Cos(randAngle), Mathf.Sin(randAngle));
                        }
                        else
                        {
                            dir = new Vector2(dx / dist, dy / dist);
                        }

                        // 화면 가장자리에 가까운 축을 따라 분산
                        float limitLeft = screenCentre.x - screenCentre.x * screenBoundOffsetX + additionalBoundOffsetLeft;
                        float limitRight = screenCentre.x + screenCentre.x * screenBoundOffsetX - additionalBoundOffsetRight;
                        float limitBottom = screenCentre.y - screenCentre.y * screenBoundOffsetY + additionalBoundOffsetBottom;
                        float limitTop = screenCentre.y + screenCentre.y * screenBoundOffsetY - additionalBoundOffsetTop;

                        Vector3 posA = infoA.screenPosition;
                        Vector3 posB = infoB.screenPosition;

                        // 가장자리에 붙어있는 축을 판별하여 해당 축의 반대 축으로 분산
                        bool onLeftRight = Mathf.Abs(posA.x - limitLeft) < 5f || Mathf.Abs(posA.x - limitRight) < 5f;
                        bool onTopBottom = Mathf.Abs(posA.y - limitBottom) < 5f || Mathf.Abs(posA.y - limitTop) < 5f;

                        if (onLeftRight && !onTopBottom)
                        {
                            // 좌/우 가장자리에 있으면 Y축으로 분산
                            posA.y = Mathf.Clamp(posA.y + halfPush, limitBottom, limitTop);
                            posB.y = Mathf.Clamp(posB.y - halfPush, limitBottom, limitTop);
                        }
                        else if (onTopBottom && !onLeftRight)
                        {
                            // 상/하 가장자리에 있으면 X축으로 분산
                            posA.x = Mathf.Clamp(posA.x + halfPush, limitLeft, limitRight);
                            posB.x = Mathf.Clamp(posB.x - halfPush, limitLeft, limitRight);
                        }
                        else
                        {
                            // 모서리이거나 판별 불가 — 양방향으로 분산
                            posA.x = Mathf.Clamp(posA.x + dir.x * halfPush, limitLeft, limitRight);
                            posA.y = Mathf.Clamp(posA.y + dir.y * halfPush, limitBottom, limitTop);
                            posB.x = Mathf.Clamp(posB.x - dir.x * halfPush, limitLeft, limitRight);
                            posB.y = Mathf.Clamp(posB.y - dir.y * halfPush, limitBottom, limitTop);
                        }

                        infoA.screenPosition = posA;
                        infoB.screenPosition = posB;
                        infos[arrowIndices[a]] = infoA;
                        infos[arrowIndices[b]] = infoB;
                        anyMoved = true;
                    }
                }
            }

            if (!anyMoved) break;
        }
    }

    // ============================================================
    // Fallback Mode: 모서리에 화살표 랜덤 분산 + 느린 펄스 애니메이션
    // ============================================================

    /// <summary>
    /// 폴백 모드 ON/OFF (LoadingManager에서 호출)
    /// config가 null이면 기본값 사용
    /// </summary>
    private Coroutine delayedDisableCoroutine;
    private Coroutine autoDisableCoroutine;

    /// <summary>
    /// fallback 최소 유지 시간을 동적으로 설정 (LoadingManager에서 호출)
    /// </summary>
    public void SetFallbackMinDuration(float duration)
    {
        fallbackMinDuration = duration;
    }

    /// <summary>
    /// 일반 모드 인디케이터 억제 (fallback 진입 전 화살표 뭉침 방지)
    /// LoadingManager에서 fallback 활성화 대기 중에 호출
    /// </summary>
    public void SetSuppressNormalIndicators(bool suppress)
    {
        suppressNormalIndicators = suppress;
    }

    /// <summary>
    /// fallback 모드 활성화/비활성화
    /// autoDisable=true: 타이머 후 자동 해제 (앱 시작, 백그라운드 복귀 용)
    /// autoDisable=false: 수동 해제 전까지 유지 (환경 문제 감지 용)
    /// forceDisable=true: fallbackMinDuration 무시하고 즉시 해제 (트래킹 복구 시)
    /// </summary>
    public void EnableFallbackMode(bool enable, FallbackConfig config = null, bool autoDisable = true, bool forceDisable = false)
    {

        if (enable)
        {
            // 기존 타이머 모두 취소
            if (delayedDisableCoroutine != null)
            {
                StopCoroutine(delayedDisableCoroutine);
                delayedDisableCoroutine = null;
            }
            if (autoDisableCoroutine != null)
            {
                StopCoroutine(autoDisableCoroutine);
                autoDisableCoroutine = null;
            }

            if (isFallbackMode)
            {
                // 이미 fallback 중이면 autoDisable 타이머만 재시작 (영구 유지 방지)
                if (autoDisable)
                {
                    autoDisableCoroutine = StartCoroutine(AutoDisableFallback(fallbackMinDuration));
                }
                return;
            }
            isFallbackMode = true;
            isTransitioning = false;
            suppressNormalIndicators = false; // fallback 진입 시 억제 해제
            disabledFallbackTargets.Clear();
            currentFallbackConfig = config ?? new FallbackConfig();
            fallbackStartTime = Time.realtimeSinceStartup;
            fallbackStartTimeScaled = Time.time;
            AssignFallbackPositions();

            if (autoDisable)
            {
                autoDisableCoroutine = StartCoroutine(AutoDisableFallback(fallbackMinDuration));
            }
        }
        else
        {
            // 자동 해제 타이머 취소 (수동 해제가 우선)
            if (autoDisableCoroutine != null)
            {
                StopCoroutine(autoDisableCoroutine);
                autoDisableCoroutine = null;
            }

            if (!isFallbackMode && !isTransitioning)
            {
                return;
            }

            // 최소 유지 시간 체크 (forceDisable이면 무시)
            if (!forceDisable)
            {
                float elapsed = Time.realtimeSinceStartup - fallbackStartTime;
                if (elapsed < fallbackMinDuration)
                {
                    if (delayedDisableCoroutine == null)
                    {
                        float delay = fallbackMinDuration - elapsed;
                        delayedDisableCoroutine = StartCoroutine(DelayedDisableFallback(delay));
                    }
                    else
                    {
                    }
                    return;
                }
            }
            else
            {
                // forceDisable 시 지연 해제 코루틴도 취소
                if (delayedDisableCoroutine != null)
                {
                    StopCoroutine(delayedDisableCoroutine);
                    delayedDisableCoroutine = null;
                }
            }

            StartFallbackTransition();
        }
    }

    private IEnumerator AutoDisableFallback(float duration)
    {
        yield return new WaitForSeconds(duration);
        autoDisableCoroutine = null;

        if (isFallbackMode)
        {
            StartFallbackTransition();
        }
        else
        {
        }
    }

    private IEnumerator DelayedDisableFallback(float delay)
    {
        yield return new WaitForSeconds(delay);
        delayedDisableCoroutine = null;

        if (isFallbackMode)
        {
            StartFallbackTransition();
        }
        else
        {
        }
    }

    /// <summary>
    /// fallback → 정상 모드 전환 애니메이션 시작
    /// 화살표→화살표: 위치/스케일/회전 보간
    /// 화살표→box 또는 타겟 사라짐: fade-out
    /// </summary>
    private void StartFallbackTransition()
    {
        isFallbackMode = false;
        isTransitioning = true;
        suppressNormalIndicators = false; // 전환 시작 시 억제 해제
        transitionStartTime = Time.time;
        transitionDataMap.Clear();
        fadeOutTargets.Clear();
        previousArrowScreenPositions.Clear(); // 스무딩 캐시 초기화 (fallback 위치 잔류 방지)
        previousArrowAngles.Clear();

        // 캐시된 비활성 타겟의 indicator 정리 (오브젝트가 비활성이므로 전환 불가 → 즉시 해제)
        foreach (Target target in disabledFallbackTargets)
        {
            if (target != null && target.indicator != null)
            {
                target.indicator.ResetForPool();
                target.indicator.Activate(false);
                target.indicator = null;
            }
        }
        disabledFallbackTargets.Clear();

        foreach (Target target in targets)
        {
            if (target.indicator != null)
            {
                transitionDataMap[target] = new TransitionData
                {
                    startPosition = target.indicator.transform.position,
                    startScale = target.indicator.transform.localScale,
                    startRotation = target.indicator.transform.rotation
                };

                Vector3 screenPos = OffScreenIndicatorCore.GetScreenPosition(mainCamera, target.transform.position);
                bool isVisible = OffScreenIndicatorCore.IsTargetVisible(screenPos);
                if (target.NeedBoxIndicator && isVisible)
                {
                    fadeOutTargets.Add(target);
                }
            }
        }

        fallbackDataMap.Clear();
    }

    /// <summary>
    /// 각 타겟에 모서리 위 랜덤 위치/스케일/위상 할당
    /// Canvas 논리적 크기(CanvasScaler 적용된 크기) 기준으로 계산
    /// LoadingManager에서 전달된 설정값(currentFallbackConfig)으로 경계/스케일 결정
    /// </summary>
    private void AssignFallbackPositions()
    {
        if (targets.Count == 0)
        {
            fallbackDataMap.Clear();
            return;
        }

        // Canvas의 논리적 크기 사용 (CanvasScaler 기준 해상도 반영됨)
        Vector2 canvasSize = GetCanvasSize();
        float cw = canvasSize.x;
        float ch = canvasSize.y;

        // canvasSize가 0이면 (레이아웃 미완료) 기존 위치 유지 — 다음 프레임에 재시도
        if (cw <= 0f || ch <= 0f)
        {
            return;
        }

        fallbackDataMap.Clear();

        FallbackConfig cfg = currentFallbackConfig ?? new FallbackConfig();

        // GPS 기반으로 가까운 순 정렬 → 거리 필터 + 이름 필터 → maxIndicatorCount개만 선택
        // (transform.position은 트래킹 Lost 시 부정확하므로 GPS 좌표 사용)
        List<Target> sortedTargets = new List<Target>(targets);
        float userLat = 0f, userLon = 0f;
        bool hasGPS = Input.location.status == LocationServiceStatus.Running;
        if (hasGPS)
        {
            userLat = Input.location.lastData.latitude;
            userLon = Input.location.lastData.longitude;
            sortedTargets.Sort((a, b) =>
                a.GetGPSDistance(userLat, userLon).CompareTo(b.GetGPSDistance(userLat, userLon)));
        }
        else if (mainCamera != null)
        {
            // GPS 없으면 카메라 거리 fallback
            Vector3 camPos = mainCamera.transform.position;
            sortedTargets.Sort((a, b) =>
                a.GetDistanceFromCamera(camPos).CompareTo(b.GetDistanceFromCamera(camPos)));
        }

        // 거리 필터: maxIndicatorDistance 밖의 타겟 제거 + 이름 없는 타겟 제거
        if (maxIndicatorDistance > 0f && hasGPS)
        {
            sortedTargets.RemoveAll(t =>
            {
                float gpsDist = t.GetGPSDistance(userLat, userLon);
                return gpsDist < 0f || gpsDist > maxIndicatorDistance;
            });
        }
        sortedTargets.RemoveAll(t => string.IsNullOrEmpty(t.PlaceName));

        int displayCount = Mathf.Min(sortedTargets.Count, cfg.maxIndicatorCount);

        // Canvas pivot(0.5, 0.5) 기준: LoadingManager에서 설정한 마진 적용
        float left   = -cw / 2f + cw * cfg.marginLeft;
        float right  =  cw / 2f - cw * cfg.marginRight;
        float bottom = -ch / 2f + ch * cfg.marginBottom;
        float top    =  ch / 2f - ch * cfg.marginTop;

        // 4변 둘레를 따라 균등 분배 + 약간의 랜덤 오프셋
        float w = right - left;
        float h = top - bottom;
        float perimeter = 2f * w + 2f * h;
        float spacing = perimeter / Mathf.Max(displayCount, 1);

        for (int i = 0; i < displayCount; i++)
        {
            // 균등 위치 + 랜덤 오프셋 (간격의 ±30%)
            float pos = (spacing * i + UnityEngine.Random.Range(-spacing * 0.3f, spacing * 0.3f) + perimeter) % perimeter;

            Vector2 canvasLocalPos;
            float angle;
            GetPositionOnPerimeter(pos, left, right, bottom, top, out canvasLocalPos, out angle);

            FallbackData data = new FallbackData
            {
                assignedPosition = CanvasLocalToWorldPosition(canvasLocalPos),
                assignedAngle = angle,
                baseScale = cfg.baseScaleMultiplier * UnityEngine.Random.Range(cfg.scaleRandomMin, cfg.scaleRandomMax),
                pulsePhaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
            };

            fallbackDataMap[sortedTargets[i]] = data;
        }
    }

    /// <summary>
    /// 둘레 위 거리 값으로 Canvas 로컬 좌표 + 바깥쪽 방향 각도 계산
    /// 화살표는 화면 모서리(바깥) 쪽을 향함
    /// </summary>
    private void GetPositionOnPerimeter(float dist, float left, float right, float bottom, float top, out Vector2 pos, out float angle)
    {
        float w = right - left;
        float h = top - bottom;

        pos = Vector2.zero;
        angle = 0f;

        if (dist < w) // 하단 변
        {
            pos = new Vector2(left + dist, bottom);
            angle = -90f; // 아래(바깥)를 향함
        }
        else if (dist < w + h) // 우측 변
        {
            pos = new Vector2(right, bottom + (dist - w));
            angle = 0f; // 오른쪽(바깥)을 향함
        }
        else if (dist < 2f * w + h) // 상단 변
        {
            pos = new Vector2(right - (dist - w - h), top);
            angle = 90f; // 위(바깥)를 향함
        }
        else // 좌측 변
        {
            pos = new Vector2(left, top - (dist - 2f * w - h));
            angle = 180f; // 왼쪽(바깥)을 향함
        }

        // 약간의 랜덤 각도 변동 (±10도)
        angle += UnityEngine.Random.Range(-10f, 10f);
    }

    /// <summary>
    /// 폴백 모드에서의 인디케이터 렌더링
    /// </summary>
    private void DrawFallbackIndicators()
    {
        // 활성 타겟 + 캐시된 비활성 타겟을 합쳐서 렌더링 대상 구성
        int totalAvailable = targets.Count + disabledFallbackTargets.Count;

        // 새로 추가된 타겟이 있으면 위치 재할당 (활성 타겟 기준)
        FallbackConfig checkCfg = currentFallbackConfig ?? new FallbackConfig();
        int mappedCount = 0;
        foreach (Target target in targets)
        {
            if (fallbackDataMap.ContainsKey(target))
                mappedCount++;
        }
        // 캐시된 비활성 타겟의 매핑 수도 카운트
        foreach (Target target in disabledFallbackTargets)
        {
            if (fallbackDataMap.ContainsKey(target))
                mappedCount++;
        }
        int expectedDisplay = Mathf.Min(totalAvailable, checkCfg.maxIndicatorCount);
        bool needsReassign = mappedCount < expectedDisplay && targets.Count > 0;
        if (needsReassign)
        {
            AssignFallbackPositions();
        }

        float time = Time.time;

        // 오프닝 애니메이션 진행률 (0→1, 1초간)
        float openingT = Mathf.Clamp01((time - fallbackStartTimeScaled) / fallbackOpeningDuration);
        float openingEased = 1f + 2.70158f * Mathf.Pow(openingT - 1f, 3f) + 1.70158f * Mathf.Pow(openingT - 1f, 2f);
        openingEased = Mathf.Clamp01(openingEased);

        Vector3 centerWorld = CanvasLocalToWorldPosition(Vector2.zero);

        int renderedCount = 0;
        int skippedNoArrow = 0;
        int skippedNoFallback = 0;

        // 활성 타겟 + 캐시된 비활성 타겟 모두 렌더링
        RenderFallbackTarget(targets, ref renderedCount, ref skippedNoArrow, ref skippedNoFallback, time, openingT, openingEased, centerWorld);
        RenderFallbackTarget(disabledFallbackTargets, ref renderedCount, ref skippedNoArrow, ref skippedNoFallback, time, openingT, openingEased, centerWorld);

    }

    private void RenderFallbackTarget(IEnumerable<Target> targetList, ref int renderedCount, ref int skippedNoArrow, ref int skippedNoFallback, float time, float openingT, float openingEased, Vector3 centerWorld)
    {
        foreach (Target target in targetList)
        {
            if (target == null) continue;
            if (!target.NeedArrowIndicator) { skippedNoArrow++; continue; }
            if (!fallbackDataMap.ContainsKey(target)) { skippedNoFallback++; continue; }

            FallbackData data = fallbackDataMap[target];

            Indicator indicator = GetIndicator(ref target.indicator, IndicatorType.ARROW, target, data.assignedPosition);

            if (indicator)
            {
                indicator.SetImageColor(target.TargetColor);

                // fallback 모드: GPS 좌표 기반 거리 계산 (활성/비활성 타겟 모두 동일)
                if (target.NeedDistanceText)
                {
                    float gpsDistance = -1f;
                    if (Input.location.status == LocationServiceStatus.Running)
                    {
                        float userLat = Input.location.lastData.latitude;
                        float userLon = Input.location.lastData.longitude;
                        gpsDistance = target.GetGPSDistance(userLat, userLon);
                    }

                    if (gpsDistance >= 0f)
                    {
                        indicator.SetDistanceText(gpsDistance, target.DistanceTextColor, target.PlaceName);
                    }
                    else
                    {
                        indicator.SetDistanceText(float.MinValue, target.DistanceTextColor, target.PlaceName);
                    }
                }
                else
                {
                    indicator.SetDistanceText(float.MinValue, Color.clear, "");
                }

                if (openingT < 1f)
                {
                    indicator.transform.position = Vector3.Lerp(centerWorld, data.assignedPosition, openingEased);
                }
                else
                {
                    indicator.transform.position = data.assignedPosition;
                }
                indicator.transform.rotation = Quaternion.Euler(0, 0, data.assignedAngle);
                indicator.SetTextRotation(Quaternion.identity);

                FallbackConfig cfg = currentFallbackConfig ?? new FallbackConfig();
                float pulse = Mathf.Sin(time * cfg.pulseSpeed + data.pulsePhaseOffset) * cfg.pulseAmplitude;
                float size = data.baseScale * target.DefaultArrowSize * (1f + pulse);

                if (openingT < 1f)
                {
                    size *= openingEased;
                }

                indicator.SetScale(new Vector3(size, size, 1f));
                renderedCount++;
            }
        }
    }

    private void HandleTargetStateChanged(Target target, bool active)
    {
        if (active)
        {
            if (!targets.Contains(target))
                targets.Add(target);
            disabledFallbackTargets.Remove(target);
        }
        else
        {
            // fallback 모드일 때: fallbackDataMap 보존, 타겟을 캐시에 보관 (화살표 계속 표시)
            if (isFallbackMode && fallbackDataMap.ContainsKey(target))
            {
                targets.Remove(target);
                disabledFallbackTargets.Add(target);
                return;
            }

            if (target.indicator != null)
            {
                target.indicator.ResetForPool();
                target.indicator.Activate(false);
            }
            target.indicator = null;
            targets.Remove(target);
            fallbackDataMap.Remove(target);
            transitionDataMap.Remove(target);
            fadeOutTargets.Remove(target);
        }
    }

    private Indicator GetIndicator(ref Indicator indicator, IndicatorType type, Target target, Vector3 finalScreenPosition)
    {
        bool isNewlyActivated = false;

        if (indicator != null)
        {
            if (indicator.Type != type)
            {
                indicator.ResetForPool(); // Pool 반환 전 완전히 리셋
                indicator.Activate(false);
                indicator = type == IndicatorType.BOX ? BoxObjectPool.current.GetPooledObject() : ArrowObjectPool.current.GetPooledObject();
                indicator.ownerTarget = target;
                indicator.Activate(true);
                isNewlyActivated = true;
            }
        }
        else
        {
            indicator = type == IndicatorType.BOX ? BoxObjectPool.current.GetPooledObject() : ArrowObjectPool.current.GetPooledObject();
            indicator.ownerTarget = target;
            indicator.Activate(true);
            isNewlyActivated = true;
        }

        return indicator;
    }

    private void OnDestroy()
    {
        TargetStateChanged -= HandleTargetStateChanged;
    }
}