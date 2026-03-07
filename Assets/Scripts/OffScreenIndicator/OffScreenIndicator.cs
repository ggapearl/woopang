using PixelPlay.OffScreenIndicator;
using System;
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

    private List<Target> targets = new List<Target>();

    public static Action<Target, bool> TargetStateChanged;

    // ============================================================
    // Fallback Mode - AR 세션 미작동 시 화살표 분산 배치 + 펄스
    // ============================================================
    private bool isFallbackMode = false;
    private Dictionary<Target, FallbackData> fallbackDataMap = new Dictionary<Target, FallbackData>();

    private class FallbackData
    {
        public Vector3 assignedPosition;    // 모서리 위의 랜덤 배치 위치
        public float assignedAngle;         // 화살표 회전각
        public float baseScale;             // 기본 스케일 (0.8~1.2)
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
        TargetStateChanged += HandleTargetStateChanged;
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

        foreach (Target target in targets)
        {
            Vector3 screenPosition = OffScreenIndicatorCore.GetScreenPosition(mainCamera, target.transform.position);
            bool isTargetVisible = OffScreenIndicatorCore.IsTargetVisible(screenPosition);
            float distanceFromCamera = target.NeedDistanceText ? target.GetDistanceFromCamera(mainCamera.transform.position) : float.MinValue;
            Indicator indicator = null;

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
                indicator.transform.position = screenPosition;
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
            }
        }
    }

    // ============================================================
    // Fallback Mode: 모서리에 화살표 랜덤 분산 + 느린 펄스 애니메이션
    // ============================================================

    /// <summary>
    /// 폴백 모드 ON/OFF (LoadingManager에서 호출)
    /// </summary>
    public void EnableFallbackMode(bool enable)
    {
        if (isFallbackMode == enable) return;
        isFallbackMode = enable;

        if (enable)
        {
            AssignFallbackPositions();
        }
        else
        {
            fallbackDataMap.Clear();
        }
    }

    /// <summary>
    /// 각 타겟에 모서리 위 랜덤 위치/스케일/위상 할당
    /// </summary>
    private void AssignFallbackPositions()
    {
        fallbackDataMap.Clear();
        if (targets.Count == 0) return;

        // 화면 모서리 경계 계산
        float marginX = Screen.width * (1f - screenBoundOffsetX) + additionalBoundOffsetLeft;
        float marginXRight = Screen.width * (1f - screenBoundOffsetX) + additionalBoundOffsetRight;
        float marginY = Screen.height * (1f - screenBoundOffsetY) + additionalBoundOffsetBottom;
        float marginYTop = Screen.height * (1f - screenBoundOffsetY) + additionalBoundOffsetTop;

        float left   = marginX;
        float right  = Screen.width - marginXRight;
        float bottom = marginY;
        float top    = Screen.height - marginYTop;

        // 4변 둘레를 따라 균등 분배 + 약간의 랜덤 오프셋
        float perimeter = 2f * (right - left) + 2f * (top - bottom);
        float spacing = perimeter / Mathf.Max(targets.Count, 1);

        for (int i = 0; i < targets.Count; i++)
        {
            // 균등 위치 + 랜덤 오프셋 (간격의 ±30%)
            float pos = (spacing * i + UnityEngine.Random.Range(-spacing * 0.3f, spacing * 0.3f) + perimeter) % perimeter;

            Vector3 screenPos;
            float angle;
            GetPositionOnPerimeter(pos, left, right, bottom, top, out screenPos, out angle);

            FallbackData data = new FallbackData
            {
                assignedPosition = screenPos,
                assignedAngle = angle,
                baseScale = UnityEngine.Random.Range(0.8f, 1.2f),
                pulsePhaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
            };

            fallbackDataMap[targets[i]] = data;
        }
    }

    /// <summary>
    /// 둘레 위 거리 값으로 화면 모서리 좌표 + 안쪽 방향 각도 계산
    /// </summary>
    private void GetPositionOnPerimeter(float dist, float left, float right, float bottom, float top, out Vector3 pos, out float angle)
    {
        float w = right - left;
        float h = top - bottom;

        pos = Vector3.zero;
        angle = 0f;

        if (dist < w) // 하단 변
        {
            pos = new Vector3(left + dist, bottom, 0);
            angle = 90f;
        }
        else if (dist < w + h) // 우측 변
        {
            pos = new Vector3(right, bottom + (dist - w), 0);
            angle = 180f;
        }
        else if (dist < 2f * w + h) // 상단 변
        {
            pos = new Vector3(right - (dist - w - h), top, 0);
            angle = 270f;
        }
        else // 좌측 변
        {
            pos = new Vector3(left, top - (dist - 2f * w - h), 0);
            angle = 0f;
        }

        // 약간의 랜덤 각도 변동 (±15도)
        angle += UnityEngine.Random.Range(-15f, 15f);
    }

    /// <summary>
    /// 폴백 모드에서의 인디케이터 렌더링
    /// </summary>
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
        if (needsReassign) AssignFallbackPositions();

        float time = Time.time;

        foreach (Target target in targets)
        {
            if (!target.NeedArrowIndicator) continue;
            if (!fallbackDataMap.ContainsKey(target)) continue;

            FallbackData data = fallbackDataMap[target];
            Vector3 screenPosition = data.assignedPosition;

            Indicator indicator = GetIndicator(ref target.indicator, IndicatorType.ARROW, target, screenPosition);

            if (indicator)
            {
                indicator.SetImageColor(target.TargetColor);
                if (target.NeedDistanceText)
                {
                    indicator.SetDistanceText(float.MinValue, target.DistanceTextColor, target.PlaceName);
                }
                else
                {
                    indicator.SetDistanceText(float.MinValue, Color.clear, "");
                }

                indicator.transform.position = screenPosition;
                indicator.transform.rotation = Quaternion.Euler(0, 0, data.assignedAngle);
                indicator.SetTextRotation(Quaternion.identity);

                // 느린 펄스 애니메이션 (12초 주기, 스케일 0.9~1.1 왕복)
                float pulse = Mathf.Sin(time * 0.52f + data.pulsePhaseOffset) * 0.1f; // ±0.1
                float size = data.baseScale * target.DefaultArrowSize * (1f + pulse);
                indicator.SetScale(new Vector3(size, size, 1f));
            }
        }
    }

    private void HandleTargetStateChanged(Target target, bool active)
    {
        if (active)
        {
            targets.Add(target);
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
            IndicatorSparkleHelper.PlaySparkleForIndicator(finalScreenPosition, type);
            target.hasPlayedSparkle = true; // Sparkle 재생 완료 표시
        }

        return indicator;
    }

    private void OnDestroy()
    {
        TargetStateChanged -= HandleTargetStateChanged;
    }
}