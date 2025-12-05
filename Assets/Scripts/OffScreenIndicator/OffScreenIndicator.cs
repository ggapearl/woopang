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

    void Awake()
    {
        mainCamera = FindObjectOfType<ARCameraManager>()?.GetComponent<Camera>() ?? Camera.main;
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
                float limitX = screenCentre.x * screenBoundOffsetX - additionalBoundOffsetLeft; // 좌측 경계
                float limitXRight = screenCentre.x * screenBoundOffsetX - additionalBoundOffsetRight; // 우측 경계
                float limitY = screenCentre.y * screenBoundOffsetY - additionalBoundOffsetBottom; // 하단 경계
                float limitYTop = screenCentre.y * screenBoundOffsetY - additionalBoundOffsetTop; // 상단 경계

                screenPosition.x = Mathf.Clamp(screenPosition.x, screenCentre.x - limitX, screenCentre.x + limitXRight);
                screenPosition.y = Mathf.Clamp(screenPosition.y, screenCentre.y - limitY, screenCentre.y + limitYTop);

                // ✅ 최종 위치 계산 후 GetIndicator() 호출 (Sparkle 정확한 위치에 생성)
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

                // Box와 Arrow의 스케일을 최소/최대 거리와 사이즈로 동적 조절
                float size;
                if (indicator.Type == IndicatorType.BOX)
                {
                    // BoxIndicator 스케일링
                    if (distanceFromCamera <= target.MinDistance)
                    {
                        size = target.MaxBoxSize; // 최소 거리에서 최대 사이즈
                    }
                    else if (distanceFromCamera >= target.MaxDistance)
                    {
                        size = target.DefaultBoxSize; // 최대 거리에서 기본 사이즈
                    }
                    else
                    {
                        // 최소-최대 거리 사이에서 선형 보간
                        float t = (target.MaxDistance - distanceFromCamera) / (target.MaxDistance - target.MinDistance);
                        size = Mathf.Lerp(target.DefaultBoxSize, target.MaxBoxSize, t);
                    }
                    indicator.SetScale(new Vector3(size, size, size));
                }
                else
                {
                    // ArrowIndicator 스케일링
                    if (distanceFromCamera <= target.MinDistance)
                    {
                        size = target.MaxArrowSize; // 최소 거리에서 최대 사이즈
                    }
                    else if (distanceFromCamera >= target.MaxDistance)
                    {
                        size = target.DefaultArrowSize; // 최대 거리에서 기본 사이즈
                    }
                    else
                    {
                        // 최소-최대 거리 사이에서 선형 보간
                        float t = (target.MaxDistance - distanceFromCamera) / (target.MaxDistance - target.MinDistance);
                        size = Mathf.Lerp(target.DefaultArrowSize, target.MaxArrowSize, t);
                    }
                    indicator.SetScale(new Vector3(size, size, 1f));
                }
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
            target.indicator?.Activate(false);
            target.indicator = null;
            targets.Remove(target);
        }
    }

    private Indicator GetIndicator(ref Indicator indicator, IndicatorType type, Target target, Vector3 finalScreenPosition)
    {
        bool isNewlyActivated = false;

        if (indicator != null)
        {
            if (indicator.Type != type)
            {
                indicator.Activate(false);
                indicator = type == IndicatorType.BOX ? BoxObjectPool.current.GetPooledObject() : ArrowObjectPool.current.GetPooledObject();
                indicator.Activate(true);
                isNewlyActivated = true;
            }
        }
        else
        {
            indicator = type == IndicatorType.BOX ? BoxObjectPool.current.GetPooledObject() : ArrowObjectPool.current.GetPooledObject();
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