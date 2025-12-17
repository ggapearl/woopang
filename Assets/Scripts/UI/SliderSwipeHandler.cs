using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Allows dragging anywhere on the Slider to change its value.
/// Attach this to a GameObject with a Slider component.
/// </summary>
[RequireComponent(typeof(Slider))]
public class SliderSwipeHandler : MonoBehaviour
{
    private Slider slider;
    private RectTransform sliderRect;
    private bool isTouchingSlider = false;
    private bool isDragging = false;

    void Start()
    {
        slider = GetComponent<Slider>();
        sliderRect = GetComponent<RectTransform>();
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
                    isTouchingSlider = true;
                    UpdateSliderValue(touch.screenPosition);
                    isDragging = true;
                }
            }
            else if (touch.phase == TouchPhase.Moved && isDragging)
            {
                UpdateSliderValue(touch.screenPosition);
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                isTouchingSlider = false;
                isDragging = false;
            }
        }
    }

    private void UpdateSliderValue(Vector2 screenPosition)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            sliderRect, screenPosition, null, out localPoint))
        {
            // Calculate normalized position (0-1) based on slider direction
            float normalizedValue;
            if (slider.direction == Slider.Direction.LeftToRight || slider.direction == Slider.Direction.RightToLeft)
            {
                float width = sliderRect.rect.width;
                normalizedValue = Mathf.Clamp01((localPoint.x + width / 2f) / width);

                // Reverse if right to left
                if (slider.direction == Slider.Direction.RightToLeft)
                {
                    normalizedValue = 1f - normalizedValue;
                }
            }
            else // TopToBottom or BottomToTop
            {
                float height = sliderRect.rect.height;
                normalizedValue = Mathf.Clamp01((localPoint.y + height / 2f) / height);

                // Reverse if top to bottom
                if (slider.direction == Slider.Direction.TopToBottom)
                {
                    normalizedValue = 1f - normalizedValue;
                }
            }

            slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, normalizedValue);
        }
    }
}
