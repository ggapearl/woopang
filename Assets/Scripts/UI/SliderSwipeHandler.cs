using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Allows dragging anywhere on the Slider to change its value with relative drag behavior.
/// When touching non-handle areas, the slider moves based on drag distance, not jump-to-position.
/// Drag 시작 시 핸들 확대 + 배경 색상 변화로 터치 피드백 제공.
/// Attach this to a GameObject with a Slider component.
/// </summary>
[RequireComponent(typeof(Slider))]
public class SliderSwipeHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private Slider slider;
    private RectTransform sliderRect;
    private RectTransform handleRect;
    private bool isDragging = false;
    private bool pointerDriven = false;   // EventSystem 경로로 진입했는지 — true면 EnhancedTouch 경로 스킵 (중복 처리 방지)
    private Vector2 lastTouchPosition;
    private float dragStartValue;

    [Header("Drag Sensitivity")]
    [SerializeField] private float dragSensitivity = 1.0f;

    [Header("Touch Hit Area")]
    [Tooltip("슬라이더 raycast 영역을 위/아래/좌/우로 확장 (px). 좁은 슬라이더 터치 정확도 향상 — 핸들 외 빈 곳 누르기 쉬워짐")]
    [SerializeField] private float hitAreaPadding = 30f;
    [Tooltip("핸들 hit 영역 크기 (시각 크기 유지, 터치 영역만 확장). 0,0이면 미적용")]
    [SerializeField] private Vector2 handleHitSize = new Vector2(60f, 60f);

    [Header("Touch Feedback")]
    [Tooltip("드래그 시 핸들 확대 배율")]
    [SerializeField] private float handleScaleOnDrag = 1.4f;
    [Tooltip("핸들 확대/축소 애니메이션 속도")]
    [SerializeField] private float scaleAnimSpeed = 10f;
    [Tooltip("드래그 시 배경(사이드바) 밝아지는 정도 (0~1)")]
    [SerializeField] private float bgBrightenAmount = 0.08f;
    [Tooltip("배경 색상 변화 애니메이션 속도")]
    [SerializeField] private float bgColorAnimSpeed = 8f;

    // 핸들 스케일 애니메이션
    private Vector3 handleOriginalScale;
    private Vector3 handleTargetScale;
    private Vector3 handleCurrentScale;

    // 배경(사이드바) 색상 애니메이션
    private Image bgImage; // DistanceSliderUI 루트의 Image (사이드바 배경)
    private Color bgOriginalColor;
    private Color bgActiveColor;
    private Color bgCurrentColor;

    // 슬라이더 트랙 배경 색상 애니메이션
    private Image trackBgImage; // DistanceSlider > Background의 Image
    private Color trackBgOriginalColor;
    private Color trackBgActiveColor;

    void Awake()
    {
        slider = GetComponent<Slider>();
        sliderRect = GetComponent<RectTransform>();

        if (slider.handleRect != null)
        {
            handleRect = slider.handleRect;
            handleOriginalScale = handleRect.localScale;
            handleCurrentScale = handleOriginalScale;
            handleTargetScale = handleOriginalScale;
        }

        // Hit 영역 자동 생성 (시각엔 영향 없는 invisible Image — 슬라이더 영역 확장 + 핸들 영역 확장)
        EnsureSliderHitArea();
        EnsureHandleHitArea();
    }

    void Start()
    {
        // 사이드바 배경: 부모(DistanceSliderUI)의 Image
        Transform parent = transform.parent;
        if (parent != null)
        {
            bgImage = parent.GetComponent<Image>();
            if (bgImage != null)
            {
                bgOriginalColor = bgImage.color;
                bgActiveColor = new Color(
                    Mathf.Min(bgOriginalColor.r + bgBrightenAmount, 1f),
                    Mathf.Min(bgOriginalColor.g + bgBrightenAmount, 1f),
                    Mathf.Min(bgOriginalColor.b + bgBrightenAmount, 1f),
                    bgOriginalColor.a
                );
                bgCurrentColor = bgOriginalColor;
            }
        }

        // 슬라이더 트랙 배경: 자식 "Background"의 Image
        Transform bgChild = transform.Find("Background");
        if (bgChild != null)
        {
            trackBgImage = bgChild.GetComponent<Image>();
            if (trackBgImage != null)
            {
                trackBgOriginalColor = trackBgImage.color;
                trackBgActiveColor = new Color(
                    Mathf.Min(trackBgOriginalColor.r + bgBrightenAmount * 1.5f, 1f),
                    Mathf.Min(trackBgOriginalColor.g + bgBrightenAmount * 1.5f, 1f),
                    Mathf.Min(trackBgOriginalColor.b + bgBrightenAmount * 1.5f, 1f),
                    trackBgOriginalColor.a
                );
            }
        }
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        // 비활성화 시 드래그 상태 + 원래 시각 복원
        isDragging = false;
        pointerDriven = false;
        ResetVisuals();
    }

    void Update()
    {
        // EventSystem이 잡고 있으면 EnhancedTouch 경로 스킵 (중복 처리 방지)
        if (!pointerDriven)
        {
            if (Touch.activeTouches.Count == 1)
            {
                var touch = Touch.activeTouches[0];

                if (touch.phase == TouchPhase.Began)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(sliderRect, touch.screenPosition, null))
                    {
                        lastTouchPosition = touch.screenPosition;
                        dragStartValue = slider.value;
                        isDragging = true;
                        SetDragVisuals(true);
                    }
                }
                else if (touch.phase == TouchPhase.Moved && isDragging)
                {
                    UpdateSliderValueRelative(touch.screenPosition);
                    lastTouchPosition = touch.screenPosition;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    if (isDragging)
                    {
                        isDragging = false;
                        SetDragVisuals(false);
                    }
                }
            }
            else if (isDragging)
            {
                isDragging = false;
                SetDragVisuals(false);
            }
        }

        // 부드러운 애니메이션 보간
        AnimateVisuals();
    }

    // ===== EventSystem 경로 (마우스 + 터치 통합, 다른 UI 시스템과 자연 통합) =====

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isDragging) return;
        lastTouchPosition = eventData.position;
        dragStartValue = slider.value;
        isDragging = true;
        pointerDriven = true;
        SetDragVisuals(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerDriven) return;
        UpdateSliderValueRelative(eventData.position);
        lastTouchPosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pointerDriven) return;
        isDragging = false;
        pointerDriven = false;
        SetDragVisuals(false);
    }

    // ===== Hit 영역 자동 생성 — 시각 변경 없이 터치 영역만 확장 =====

    private void EnsureSliderHitArea()
    {
        if (hitAreaPadding <= 0f) return;
        if (transform.Find("__SliderHitArea") != null) return; // 이미 있으면 스킵

        var go = new GameObject("__SliderHitArea", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();   // Background/Handle 뒤로 — 시각 안 가림
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-hitAreaPadding, -hitAreaPadding);
        rt.offsetMax = new Vector2(hitAreaPadding, hitAreaPadding);
        var img = go.GetComponent<Image>();
        img.color = new Color(1, 1, 1, 0);  // alpha 0 — 화면엔 안 보임, raycast만 받음
        img.raycastTarget = true;
    }

    private void EnsureHandleHitArea()
    {
        if (slider == null || slider.handleRect == null) return;
        if (handleHitSize.sqrMagnitude <= 0f) return;
        if (slider.handleRect.Find("__HandleHitArea") != null) return;

        var go = new GameObject("__HandleHitArea", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(slider.handleRect, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = handleHitSize;
        var img = go.GetComponent<Image>();
        img.color = new Color(1, 1, 1, 0);
        img.raycastTarget = true;
    }

    private void SetDragVisuals(bool active)
    {
        if (handleRect != null)
        {
            handleTargetScale = active
                ? handleOriginalScale * handleScaleOnDrag
                : handleOriginalScale;
        }
    }

    private void AnimateVisuals()
    {
        float dt = Time.unscaledDeltaTime;

        // 핸들 스케일 보간
        if (handleRect != null)
        {
            handleCurrentScale = Vector3.Lerp(handleCurrentScale, handleTargetScale, dt * scaleAnimSpeed);
            handleRect.localScale = handleCurrentScale;
        }

        // 사이드바 배경 색상 보간
        if (bgImage != null)
        {
            Color targetBg = isDragging ? bgActiveColor : bgOriginalColor;
            bgCurrentColor = Color.Lerp(bgCurrentColor, targetBg, dt * bgColorAnimSpeed);
            bgImage.color = bgCurrentColor;
        }

        // 슬라이더 트랙 배경 색상 보간
        if (trackBgImage != null)
        {
            Color targetTrack = isDragging ? trackBgActiveColor : trackBgOriginalColor;
            trackBgImage.color = Color.Lerp(trackBgImage.color, targetTrack, dt * bgColorAnimSpeed);
        }
    }

    private void ResetVisuals()
    {
        if (handleRect != null)
        {
            handleRect.localScale = handleOriginalScale;
            handleCurrentScale = handleOriginalScale;
            handleTargetScale = handleOriginalScale;
        }
        if (bgImage != null)
        {
            bgImage.color = bgOriginalColor;
            bgCurrentColor = bgOriginalColor;
        }
        if (trackBgImage != null)
        {
            trackBgImage.color = trackBgOriginalColor;
        }
    }

    private void UpdateSliderValueRelative(Vector2 currentScreenPosition)
    {
        Vector2 dragDelta = currentScreenPosition - lastTouchPosition;

        float movementAmount = 0f;
        if (slider.direction == Slider.Direction.LeftToRight)
            movementAmount = dragDelta.x;
        else if (slider.direction == Slider.Direction.RightToLeft)
            movementAmount = -dragDelta.x;
        else if (slider.direction == Slider.Direction.BottomToTop)
            movementAmount = dragDelta.y;
        else if (slider.direction == Slider.Direction.TopToBottom)
            movementAmount = -dragDelta.y;

        float sliderSize = (slider.direction == Slider.Direction.LeftToRight || slider.direction == Slider.Direction.RightToLeft)
            ? sliderRect.rect.width
            : sliderRect.rect.height;

        if (sliderSize > 0)
        {
            float normalizedMovement = (movementAmount / sliderSize) * dragSensitivity;
            float valueChange = normalizedMovement * (slider.maxValue - slider.minValue);
            slider.value = Mathf.Clamp(slider.value + valueChange, slider.minValue, slider.maxValue);
        }
    }
}
