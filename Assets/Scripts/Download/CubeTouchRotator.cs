using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// 3D 큐브 오브젝트를 터치 드래그로 회전시키는 컴포넌트
/// 0000_Cube 프리팹의 자식 Cube 오브젝트에 부착
/// DoubleTap3D와 공존 — 싱글 드래그=회전, 더블탭=기존 로직 유지
/// </summary>
public class CubeTouchRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("드래그 회전 감도")]
    [SerializeField] private float rotationSpeed = 0.3f;

    [Tooltip("드래그 관성 감쇠 (0에 가까울수록 빨리 멈춤)")]
    [SerializeField] private float inertiaDecay = 0.92f;

    [Tooltip("드래그로 인식하는 최소 이동 거리 (px)")]
    [SerializeField] private float dragThreshold = 10f;

    [Tooltip("관성 회전 최대 속도 제한 (도/프레임)")]
    [SerializeField] private float maxInertiaSpeed = 5f;

    // 드래그 상태
    private bool isTouching = false;
    private bool isDragging = false;
    private Vector2 touchStartPos;
    private Vector2 lastDragPos;
    private float velocityX = 0f;
    private float velocityY = 0f;
    private Camera mainCamera;
    private Collider myCollider;

    private void Awake()
    {
        mainCamera = Camera.main;
        myCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        HandleTouch();
        ApplyInertia();
    }

    private void HandleTouch()
    {
#if UNITY_EDITOR
        HandleMouseInput();
#endif
        HandleTouchInput();
    }

#if UNITY_EDITOR
    private void HandleMouseInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = mouse.position.ReadValue();

            // UI 위 클릭이면 무시
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // Raycast로 이 오브젝트에 히트하는지 확인
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit) && hit.collider == myCollider)
            {
                isTouching = true;
                isDragging = false;
                touchStartPos = mousePos;
                lastDragPos = mousePos;
                velocityX = 0f;
                velocityY = 0f;
            }
        }
        else if (mouse.leftButton.isPressed && isTouching)
        {
            Vector2 currentPos = mouse.position.ReadValue();
            float dist = Vector2.Distance(touchStartPos, currentPos);

            if (!isDragging && dist >= dragThreshold)
            {
                isDragging = true;
                lastDragPos = currentPos;
            }

            if (isDragging)
            {
                ApplyRotation(currentPos);
                lastDragPos = currentPos;
            }
        }
        else if (mouse.leftButton.wasReleasedThisFrame && isTouching)
        {
            isTouching = false;
            isDragging = false;
        }
    }
#endif

    private void HandleTouchInput()
    {
        int touchCount = Touch.activeTouches.Count;
        if (touchCount != 1)
        {
            if (isTouching)
            {
                isTouching = false;
                isDragging = false;
            }
            return;
        }

        var touch = Touch.activeTouches[0];

        if (touch.phase == TouchPhase.Began)
        {
            // UI 위 터치이면 무시
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject(touch.touchId))
                return;

            Ray ray = mainCamera.ScreenPointToRay(touch.screenPosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit) && hit.collider == myCollider)
            {
                isTouching = true;
                isDragging = false;
                touchStartPos = touch.screenPosition;
                lastDragPos = touch.screenPosition;
                velocityX = 0f;
                velocityY = 0f;
            }
        }
        else if (touch.phase == TouchPhase.Moved && isTouching)
        {
            float dist = Vector2.Distance(touchStartPos, touch.screenPosition);

            if (!isDragging && dist >= dragThreshold)
            {
                isDragging = true;
                lastDragPos = touch.screenPosition;
            }

            if (isDragging)
            {
                ApplyRotation(touch.screenPosition);
                lastDragPos = touch.screenPosition;
            }
        }
        else if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) && isTouching)
        {
            isTouching = false;
            isDragging = false;
        }
    }

    private void ApplyRotation(Vector2 currentPos)
    {
        Vector2 delta = currentPos - lastDragPos;

        float rotY = -delta.x * rotationSpeed;
        float rotX = delta.y * rotationSpeed;

        transform.Rotate(Vector3.up, rotY, Space.World);
        transform.Rotate(Vector3.right, rotX, Space.World);

        velocityX = Mathf.Clamp(rotX, -maxInertiaSpeed, maxInertiaSpeed);
        velocityY = Mathf.Clamp(rotY, -maxInertiaSpeed, maxInertiaSpeed);
    }

    private void ApplyInertia()
    {
        if (isTouching) return;

        if (Mathf.Abs(velocityX) > 0.01f || Mathf.Abs(velocityY) > 0.01f)
        {
            transform.Rotate(Vector3.up, velocityY, Space.World);
            transform.Rotate(Vector3.right, velocityX, Space.World);

            velocityX *= inertiaDecay;
            velocityY *= inertiaDecay;
        }
    }
}
