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

    public static Action<Target, bool> TargetStateChanged;

    // ============================================================
    // Fallback Mode - AR 세션 미작동 시 화살표 분산 배치 + 펄스
    // ============================================================
    private bool isFallbackMode = false;
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
            Debug.LogError("AR 카메라 또는 메인 카메라를 찾을 수 없습니다!");
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

        Debug.Log($"[WP-DBG] Awake: mainCamera={mainCamera?.name}, parentCanvas={(parentCanvas != null ? parentCanvas.renderMode.ToString() : "NULL")}, canvasRectTransform={(canvasRectTransform != null ? $"({canvasRectTransform.rect.width:F0}x{canvasRectTransform.rect.height:F0})" : "NULL")}, screen=({Screen.width}x{Screen.height})");

        TargetStateChanged += HandleTargetStateChanged;
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

        foreach (Target target in targets)
        {
            Vector3 screenPosition = OffScreenIndicatorCore.GetScreenPosition(mainCamera, target.transform.position);
            bool isTargetVisible = OffScreenIndicatorCore.IsTargetVisible(screenPosition);
            float distanceFromCamera = target.NeedDistanceText ? target.GetDistanceFromCamera(mainCamera.transform.position) : float.MinValue;
            Indicator indicator = null;

            // 전환 중 fade-out 대상 (화살표→box 전환): 기존 화살표를 fade-out
            if (isTransitioning && fadeOutTargets.Contains(target))
            {
                if (target.indicator != null)
                {
                    float fadeT = Mathf.Clamp01((Time.time - transitionStartTime) / fallbackFadeOutDuration);
                    target.indicator.SetAlpha(1f - fadeT);

                    if (fadeT >= 1f)
                    {
                        // fade-out 완료 → 풀 반환, 다음 프레임에 box로 새로 생성
                        target.indicator.SetAlpha(1f);
                        target.indicator.ResetForPool();
                        target.indicator.Activate(false);
                        target.indicator = null;
                        fadeOutTargets.Remove(target);
                    }
                    else
                    {
                        // fade-out 중에는 위치 유지
                        continue;
                    }
                }
            }

            if (target.NeedBoxIndicator && isTargetVisible)
            {
                screenPosition.z = 0;
                indicator = GetIndicator(ref target.indicator, IndicatorType.BOX, target, screenPosition);
            }
            else if (target.NeedArrowIndicator && !isTargetVisible)
            {
                float angle = float.MinValue;
                OffScreenIndicatorCore.GetArrowIndicatorPositionAndAngle(ref screenPosition, ref angle, screenCentre, screenBoundsX);

                // 수정: 추가 경계 값을 반영한 클램프
                float limitX = screenCentre.x * screenBoundOffsetX - additionalBoundOffsetLeft;
                float limitXRight = screenCentre.x * screenBoundOffsetX - additionalBoundOffsetRight;
                float limitY = screenCentre.y * screenBoundOffsetY - additionalBoundOffsetBottom;
                float limitYTop = screenCentre.y * screenBoundOffsetY - additionalBoundOffsetTop;

                screenPosition.x = Mathf.Clamp(screenPosition.x, screenCentre.x - limitX, screenCentre.x + limitXRight);
                screenPosition.y = Mathf.Clamp(screenPosition.y, screenCentre.y - limitY, screenCentre.y + limitYTop);

                indicator = GetIndicator(ref target.indicator, IndicatorType.ARROW, target, screenPosition);
                indicator.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            }

            if (indicator)
            {
                indicator.SetImageColor(target.TargetColor);
                if (target.NeedDistanceText)
                {
                    indicator.SetDistanceText(distanceFromCamera, target.DistanceTextColor, target.PlaceName);
                }
                else
                {
                    indicator.SetDistanceText(float.MinValue, Color.clear, "");
                }

                Vector3 targetPosition = ScreenToCanvasWorldPosition(screenPosition);
                indicator.SetTextRotation(Quaternion.identity);

                float size;
                if (indicator.Type == IndicatorType.BOX)
                {
                    if (distanceFromCamera <= target.MinDistance)
                        size = target.MaxBoxSize;
                    else if (distanceFromCamera >= target.MaxDistance)
                        size = target.DefaultBoxSize;
                    else
                    {
                        float t = (target.MaxDistance - distanceFromCamera) / (target.MaxDistance - target.MinDistance);
                        size = Mathf.Lerp(target.DefaultBoxSize, target.MaxBoxSize, t);
                    }
                    indicator.SetScale(new Vector3(size, size, size));
                }
                else
                {
                    if (distanceFromCamera <= target.MinDistance)
                        size = target.MaxArrowSize;
                    else if (distanceFromCamera >= target.MaxDistance)
                        size = target.DefaultArrowSize;
                    else
                    {
                        float t = (target.MaxDistance - distanceFromCamera) / (target.MaxDistance - target.MinDistance);
                        size = Mathf.Lerp(target.DefaultArrowSize, target.MaxArrowSize, t);
                    }
                    indicator.SetScale(new Vector3(size, size, 1f));
                }

                // 전환 중: fallback 위치에서 실제 위치로 보간
                if (isTransitioning && transitionDataMap.ContainsKey(target) && indicator.Type == IndicatorType.ARROW)
                {
                    TransitionData td = transitionDataMap[target];
                    Vector3 targetScale = indicator.transform.localScale; // SetScale로 설정된 정상 스케일
                    Quaternion targetRotation = indicator.transform.rotation; // 정상 회전

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

    // ============================================================
    // Fallback Mode: 모서리에 화살표 랜덤 분산 + 느린 펄스 애니메이션
    // ============================================================

    /// <summary>
    /// 폴백 모드 ON/OFF (LoadingManager에서 호출)
    /// config가 null이면 기본값 사용
    /// </summary>
    private Coroutine delayedDisableCoroutine;

    public void EnableFallbackMode(bool enable, FallbackConfig config = null)
    {
        Debug.Log($"[WP-DBG] EnableFallbackMode({enable}) called, isFallbackMode={isFallbackMode}, isTransitioning={isTransitioning}, targets={targets.Count}");

        if (enable)
        {
            // 지연 해제 코루틴이 실행 중이면 취소
            if (delayedDisableCoroutine != null)
            {
                StopCoroutine(delayedDisableCoroutine);
                delayedDisableCoroutine = null;
            }

            if (isFallbackMode) return; // 이미 활성화 상태
            isFallbackMode = true;
            isTransitioning = false;
            currentFallbackConfig = config ?? new FallbackConfig();
            fallbackStartTime = Time.realtimeSinceStartup;
            fallbackStartTimeScaled = Time.time;
            AssignFallbackPositions();
            Debug.Log($"[WP-DBG] Fallback ON: fallbackDataMap={fallbackDataMap.Count}, maxIndicator={currentFallbackConfig.maxIndicatorCount}");
        }
        else
        {
            if (!isFallbackMode && !isTransitioning) return; // 이미 비활성화 상태

            // 최소 유지 시간 체크
            float elapsed = Time.realtimeSinceStartup - fallbackStartTime;
            if (elapsed < fallbackMinDuration)
            {
                // 아직 최소 시간이 안 됐으면 지연 후 해제
                if (delayedDisableCoroutine == null)
                {
                    float delay = fallbackMinDuration - elapsed;
                    delayedDisableCoroutine = StartCoroutine(DelayedDisableFallback(delay));
                }
                return;
            }

            // 전환 애니메이션 시작
            StartFallbackTransition();
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
        transitionStartTime = Time.time;
        transitionDataMap.Clear();
        fadeOutTargets.Clear();

        foreach (Target target in targets)
        {
            if (target.indicator != null)
            {
                // 현재 위치/스케일/회전 저장
                transitionDataMap[target] = new TransitionData
                {
                    startPosition = target.indicator.transform.position,
                    startScale = target.indicator.transform.localScale,
                    startRotation = target.indicator.transform.rotation
                };

                // 화살표→box로 바뀌는 타겟은 fade-out 대상
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
        fallbackDataMap.Clear();
        if (targets.Count == 0)
        {
            Debug.Log("[WP-DBG] AssignFallbackPositions: targets=0, skip");
            return;
        }

        FallbackConfig cfg = currentFallbackConfig ?? new FallbackConfig();

        // 카메라에서 가까운 순으로 정렬하여 maxIndicatorCount개만 선택
        List<Target> sortedTargets = new List<Target>(targets);
        if (mainCamera != null)
        {
            Vector3 camPos = mainCamera.transform.position;
            sortedTargets.Sort((a, b) =>
                a.GetDistanceFromCamera(camPos).CompareTo(b.GetDistanceFromCamera(camPos)));
        }
        int displayCount = Mathf.Min(sortedTargets.Count, cfg.maxIndicatorCount);

        // Canvas의 논리적 크기 사용 (CanvasScaler 기준 해상도 반영됨)
        Vector2 canvasSize = GetCanvasSize();
        float cw = canvasSize.x;
        float ch = canvasSize.y;

        Debug.Log($"[WP-DBG] AssignFallbackPositions: targets={targets.Count}, displayCount={displayCount}, maxIndicator={cfg.maxIndicatorCount}, canvasSize=({cw:F0},{ch:F0}), mainCamera={(mainCamera != null ? "OK" : "NULL")}, canvasRect={(canvasRectTransform != null ? "OK" : "NULL")}");

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
            Debug.Log($"[WP-DBG] FallbackPos[{i}]: canvasLocal=({canvasLocalPos.x:F0},{canvasLocalPos.y:F0}), worldPos=({data.assignedPosition.x:F2},{data.assignedPosition.y:F2},{data.assignedPosition.z:F2}), angle={data.assignedAngle:F1}, scale={data.baseScale:F2}");
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
    private int drawFallbackLogCount = 0;

    private void DrawFallbackIndicators()
    {
        // 새로 추가된 타겟이 있으면 위치 재할당
        bool needsReassign = false;
        foreach (Target target in targets)
        {
            if (!fallbackDataMap.ContainsKey(target))
            {
                needsReassign = true;
                break;
            }
        }
        if (needsReassign)
        {
            Debug.Log($"[WP-DBG] DrawFallback: needsReassign=true, targets={targets.Count}, fallbackMap={fallbackDataMap.Count}");
            AssignFallbackPositions();
        }

        float time = Time.time;

        // 오프닝 애니메이션 진행률 (0→1, 1초간)
        float openingT = Mathf.Clamp01((time - fallbackStartTimeScaled) / fallbackOpeningDuration);
        // EaseOutBack 커브: 약간 오버슈트 후 자리잡기
        float openingEased = 1f + 2.70158f * Mathf.Pow(openingT - 1f, 3f) + 1.70158f * Mathf.Pow(openingT - 1f, 2f);
        openingEased = Mathf.Clamp01(openingEased);

        // 화면 중앙 월드 좌표 (오프닝 시작점)
        Vector3 centerWorld = CanvasLocalToWorldPosition(Vector2.zero);

        int renderedCount = 0;
        int skippedNoArrow = 0;
        int skippedNoFallback = 0;

        foreach (Target target in targets)
        {
            if (!target.NeedArrowIndicator) { skippedNoArrow++; continue; }
            if (!fallbackDataMap.ContainsKey(target)) { skippedNoFallback++; continue; }

            FallbackData data = fallbackDataMap[target];

            Indicator indicator = GetIndicator(ref target.indicator, IndicatorType.ARROW, target, data.assignedPosition);

            if (indicator)
            {
                indicator.SetImageColor(target.TargetColor);
                float distanceFromCamera = (target.NeedDistanceText && mainCamera != null)
                    ? target.GetDistanceFromCamera(mainCamera.transform.position)
                    : float.MinValue;

                if (target.NeedDistanceText)
                {
                    indicator.SetDistanceText(distanceFromCamera, target.DistanceTextColor, target.PlaceName);
                }
                else
                {
                    indicator.SetDistanceText(float.MinValue, Color.clear, "");
                }

                // 오프닝 중: 중앙에서 최종 위치로 이동
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

                // 펄스 애니메이션
                FallbackConfig cfg = currentFallbackConfig ?? new FallbackConfig();
                float pulse = Mathf.Sin(time * cfg.pulseSpeed + data.pulsePhaseOffset) * cfg.pulseAmplitude;
                float size = data.baseScale * target.DefaultArrowSize * (1f + pulse);

                // 오프닝 중: 스케일도 0에서 최종으로 커짐
                if (openingT < 1f)
                {
                    size *= openingEased;
                }

                indicator.SetScale(new Vector3(size, size, 1f));
                renderedCount++;
            }
        }

        // 처음 5프레임만 상세 로그
        if (drawFallbackLogCount < 5)
        {
            drawFallbackLogCount++;
            Debug.Log($"[WP-DBG] DrawFallback: rendered={renderedCount}, skippedNoArrow={skippedNoArrow}, skippedNoFallback={skippedNoFallback}, totalTargets={targets.Count}, fallbackMap={fallbackDataMap.Count}, openingT={openingT:F2}, centerWorld=({centerWorld.x:F2},{centerWorld.y:F2})");
        }
    }

    private int targetChangeLogCount = 0;

    private void HandleTargetStateChanged(Target target, bool active)
    {
        if (active)
        {
            targets.Add(target);
            // 처음 20개만 로그 (수백 개 등록 시 스팸 방지)
            if (targetChangeLogCount < 20)
            {
                targetChangeLogCount++;
                Debug.Log($"[WP-DBG] TargetAdded: total={targets.Count}, needArrow={target.NeedArrowIndicator}, needBox={target.NeedBoxIndicator}, name={target.gameObject.name}");
            }
            else if (targetChangeLogCount == 20)
            {
                targetChangeLogCount++;
                Debug.Log($"[WP-DBG] TargetAdded: total={targets.Count} (suppressing further logs...)");
            }
        }
        else
        {
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

        // 화살표 인디케이터가 새로 활성화되고, Target이 아직 Sparkle을 재생하지 않았으면 재생
        // ✅ 수정: indicator.transform.position 대신 finalScreenPosition (최종 계산된 위치) 사용
        if (isNewlyActivated && type == IndicatorType.ARROW && !target.hasPlayedSparkle)
        {
            // 폴백 모드: finalScreenPosition이 이미 월드 좌표
            // 일반 모드: Screen 좌표이므로 변환 필요
            Vector3 sparklePos = isFallbackMode ? finalScreenPosition : ScreenToCanvasWorldPosition(finalScreenPosition);
            IndicatorSparkleHelper.PlaySparkleForIndicator(sparklePos, type);
            target.hasPlayedSparkle = true;
        }

        return indicator;
    }

    private void OnDestroy()
    {
        TargetStateChanged -= HandleTargetStateChanged;
    }
}