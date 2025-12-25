using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Allows dragging anywhere on the Slider to change its value with relative drag behavior.
/// When touching non-handle areas, the slider moves based on drag distance, not jump-to-position.
/// Attach this to a GameObject with a Slider component.
/// </summary>
[RequireComponent(typeof(Slider))]
public class SliderSwipeHandler : MonoBehaviour
{
    private Slider slider;
    private RectTransform sliderRect;
    private RectTransform handleRect;
    private bool isDragging = false;
    private Vector2 lastTouchPosition;
    private float dragStartValue;

    [Header("Drag Sensitivity")]
    [SerializeField] private float dragSensitivity = 1.0f; // 드래그 민감도 (높을수록 빠르게 이동)

    void Start()
    {
        slider = GetComponent<Slider>();
        sliderRect = GetComponent<RectTransform>();

        // Get handle rect for accurate handle detection
        if (slider.handleRect != null)
        {
            handleRect = slider.handleRect;
        }
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        if (Touch.activeTouches.Count == 1)
        {
            var touch = Touch.activeTouches[0];

            if (touch.phase == TouchPhase.Began)
            {
                // Check if touch is within the slider's rect
                if (RectTransformUtility.RectangleContainsScreenPoint(sliderRect, touch.screenPosition, null))
                {
                    // 터치 시작: 현재 위치와 값 저장 (점프하지 않음)
                    lastTouchPosition = touch.screenPosition;
                    dragStartValue = slider.value;
                    isDragging = true;
                }
            }
            else if (touch.phase == TouchPhase.Moved && isDragging)
            {
                // 상대적 드래그: 이동 거리에 따라 값 조절
                UpdateSliderValueRelative(touch.screenPosition);
                lastTouchPosition = touch.screenPosition;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                isDragging = false;
            }
        }
        else
        {
            // 터치가 없으면 드래그 중단
            isDragging = false;
        }
    }

    /// <summary>
    /// 상대적 드래그: 이전 위치에서 이동한 거리만큼 슬라이더 값 변경
    /// </summary>
    private void UpdateSliderValueRelative(Vector2 currentScreenPosition)
    {
        // 이동 거리 계산 (스크린 좌표)
        Vector2 dragDelta = currentScreenPosition - lastTouchPosition;

        // 슬라이더 방향에 따라 적절한 축 선택
        float movementAmount = 0f;
        if (slider.direction == Slider.Direction.LeftToRight)
        {
            movementAmount = dragDelta.x;
        }
        else if (slider.direction == Slider.Direction.RightToLeft)
        {
            movementAmount = -dragDelta.x;
        }
        else if (slider.direction == Slider.Direction.BottomToTop)
        {
            movementAmount = dragDelta.y;
        }
        else if (slider.direction == Slider.Direction.TopToBottom)
        {
            movementAmount = -dragDelta.y;
        }

        // 슬라이더 크기로 정규화 (픽셀 이동을 값 변화로 변환)
        float sliderSize = (slider.direction == Slider.Direction.LeftToRight || slider.direction == Slider.Direction.RightToLeft)
            ? sliderRect.rect.width
            : sliderRect.rect.height;

        if (sliderSize > 0)
        {
            // 정규화된 이동량 (0~1 범위)
            float normalizedMovement = (movementAmount / sliderSize) * dragSensitivity;

            // 값 범위로 변환
            float valueChange = normalizedMovement * (slider.maxValue - slider.minValue);

            // 새 값 적용 (Clamp)
            slider.value = Mathf.Clamp(slider.value + valueChange, slider.minValue, slider.maxValue);
        }
    }
}
