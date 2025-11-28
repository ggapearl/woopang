using UnityEngine;

/// <summary>
/// 두 손가락 핀치 제스처로 AR 카메라 줌 기능 제공
/// AR Foundation 호환 - orthographicSize 또는 FOV 조절
/// </summary>
public class PinchZoomController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera arCamera; // AR 카메라
    [SerializeField] private float defaultZoom = 1f; // 기본 줌 (1.0)
    [SerializeField] private float minZoom = 0.5f; // 최소 줌 (축소)
    [SerializeField] private float maxZoom = 3f; // 최대 줌 (확대)
    [SerializeField] private float zoomSpeed = 0.01f; // 줌 속도

    [Header("Zoom Indicator")]
    [SerializeField] private GameObject zoomIndicatorObject; // 줌 인디케이터 GameObject
    private ZoomIndicator zoomIndicator; // 줌 인디케이터 컴포넌트

    private float currentZoom = 1f;
    private float previousTouchDistance = 0f;
    private bool isPinching = false;
    private Vector3 initialScale;

    void Start()
    {
        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera == null)
            {
                Debug.LogError("[PinchZoomController] AR Camera를 찾을 수 없습니다!");
                enabled = false;
                return;
            }
        }

        // ZoomIndicator GameObject에서 컴포넌트 가져오기
        if (zoomIndicatorObject != null)
        {
            zoomIndicator = zoomIndicatorObject.GetComponent<ZoomIndicator>();
            if (zoomIndicator == null)
            {
                Debug.LogWarning("[PinchZoomController] ZoomIndicator GameObject에 ZoomIndicator 컴포넌트가 없습니다!");
            }
            else
            {
                Debug.Log("[PinchZoomController] ZoomIndicator 컴포넌트를 찾았습니다.");
            }
        }
        // GameObject가 할당되지 않았으면 자동으로 찾기
        else
        {
            zoomIndicator = FindObjectOfType<ZoomIndicator>();
            if (zoomIndicator == null)
            {
                Debug.LogWarning("[PinchZoomController] ZoomIndicator를 찾을 수 없습니다. 줌 표시 기능이 비활성화됩니다.");
            }
            else
            {
                Debug.Log("[PinchZoomController] ZoomIndicator를 자동으로 찾았습니다.");
            }
        }

        // 기본 줌 설정
        currentZoom = defaultZoom;
        initialScale = transform.localScale;

        Debug.Log($"[PinchZoomController] 초기화 완료 - 기본 Zoom: {defaultZoom}");
    }

    void Update()
    {
        // 터치 입력이 2개일 때 (핀치 제스처)
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            // 두 터치 사이의 거리 계산
            float currentTouchDistance = Vector2.Distance(touch0.position, touch1.position);

            // 핀치 시작
            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                previousTouchDistance = currentTouchDistance;
                isPinching = true;
            }
            // 핀치 진행 중
            else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                if (isPinching && previousTouchDistance > 0)
                {
                    // 거리 차이로 줌 계산
                    float distanceDelta = currentTouchDistance - previousTouchDistance;

                    // 줌 레벨 조정 (거리가 멀어지면 확대, 가까워지면 축소)
                    currentZoom += distanceDelta * zoomSpeed;
                    currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

                    // 카메라 orthographicSize 조절 (orthographic 카메라용)
                    if (arCamera.orthographic)
                    {
                        arCamera.orthographicSize = 5f / currentZoom;
                    }
                    // perspective 카메라는 Transform scale로 줌 효과
                    else
                    {
                        transform.localScale = initialScale * currentZoom;
                    }

                    previousTouchDistance = currentTouchDistance;

                    // 줌 인디케이터 업데이트
                    if (zoomIndicator != null)
                    {
                        zoomIndicator.UpdateZoom(currentZoom);
                    }

                    Debug.Log($"[PinchZoomController] Zoom: {currentZoom:F2}x");
                }
            }
        }
        // 핀치 종료
        else
        {
            if (isPinching)
            {
                isPinching = false;
                previousTouchDistance = 0f;

                // 줌 인디케이터 숨김 (2초 딜레이)
                if (zoomIndicator != null)
                {
                    zoomIndicator.HideAfterDelay(2f);
                }
            }
        }

        // 에디터에서 테스트용 (마우스 스크롤)
#if UNITY_EDITOR
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            currentZoom += scroll * 0.5f;
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

            // 카메라 orthographicSize 조절 (orthographic 카메라용)
            if (arCamera.orthographic)
            {
                arCamera.orthographicSize = 5f / currentZoom;
            }
            // perspective 카메라는 Transform scale로 줌 효과
            else
            {
                transform.localScale = initialScale * currentZoom;
            }

            if (zoomIndicator != null)
            {
                zoomIndicator.UpdateZoom(currentZoom);
                zoomIndicator.HideAfterDelay(2f);
            }

            Debug.Log($"[PinchZoomController] Editor Zoom: {currentZoom:F2}x");
        }
#endif
    }

    /// <summary>
    /// 현재 줌 레벨 반환 (1.0 = 기본, 2.0 = 2배 확대)
    /// </summary>
    public float GetZoomLevel()
    {
        return currentZoom;
    }

    /// <summary>
    /// 줌을 기본값으로 리셋
    /// </summary>
    public void ResetZoom()
    {
        currentZoom = defaultZoom;

        // 카메라 orthographicSize 조절 (orthographic 카메라용)
        if (arCamera.orthographic)
        {
            arCamera.orthographicSize = 5f / currentZoom;
        }
        // perspective 카메라는 Transform scale로 줌 효과
        else
        {
            transform.localScale = initialScale * currentZoom;
        }

        if (zoomIndicator != null)
        {
            zoomIndicator.UpdateZoom(1.0f);
            zoomIndicator.HideAfterDelay(1f);
        }

        Debug.Log("[PinchZoomController] 줌 초기화");
    }
}
