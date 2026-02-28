using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// AR 디지털 줌 컨트롤러 - FOV 강제 조절 방식
/// AR Foundation이 FOV를 설정한 후, LateUpdate()에서 다시 조절하여 줌 효과 구현
/// Canvas UI는 영향받지 않음
/// </summary>
public class ARDigitalZoomController : MonoBehaviour
{
    [Header("Zoom Settings")]
    [SerializeField] private float defaultZoom = 1f; // 기본 줌 (1.0)
    [SerializeField] private float minZoom = 0.5f; // 최소 줌 (축소 - FOV 증가)
    [SerializeField] private float maxZoom = 3f; // 최대 줌 (확대 - FOV 감소)
    [SerializeField] private float zoomSpeed = 0.01f; // 줌 속도
    [SerializeField] private float smoothSpeed = 5f; // 줌 부드러움 속도

    [Header("Camera Setup")]
    [SerializeField] private Camera arCamera; // AR 카메라
    [SerializeField] private ARCameraManager arCameraManager; // AR Camera Manager

    [Header("Zoom Indicator")]
    [SerializeField] private GameObject zoomIndicatorObject; // 줌 인디케이터 GameObject
    private ZoomIndicator zoomIndicator; // 줌 인디케이터 컴포넌트

    private float currentZoom = 1f;
    private float targetZoom = 1f;
    private float baseFOV = 60f; // AR Foundation이 설정한 기본 FOV
    private float previousTouchDistance = 0f;
    private bool isPinching = false;
    private bool isInitialized = false;

    void Start()
    {
        // AR 카메라 자동 찾기
        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera == null)
            {
                Debug.LogError("[ARDigitalZoomController] AR Camera를 찾을 수 없습니다!");
                enabled = false;
                return;
            }
        }

        // ARCameraManager 자동 찾기
        if (arCameraManager == null)
        {
            arCameraManager = FindFirstObjectByType<ARCameraManager>();
            if (arCameraManager == null)
            {
                Debug.LogWarning("[ARDigitalZoomController] ARCameraManager를 찾을 수 없습니다. 일반 카메라 모드로 동작합니다.");
            }
        }

        // ZoomIndicator 찾기
        if (zoomIndicatorObject != null)
        {
            zoomIndicator = zoomIndicatorObject.GetComponent<ZoomIndicator>();
            if (zoomIndicator == null)
            {
                Debug.LogWarning("[ARDigitalZoomController] ZoomIndicator 컴포넌트가 없습니다!");
            }
        }
        else
        {
            zoomIndicator = FindFirstObjectByType<ZoomIndicator>();
            if (zoomIndicator == null)
            {
                Debug.LogWarning("[ARDigitalZoomController] ZoomIndicator를 찾을 수 없습니다. 줌 표시 기능이 비활성화됩니다.");
            }
        }

        // 기본 줌 설정
        currentZoom = defaultZoom;
        targetZoom = defaultZoom;
    }

    void Update()
    {
        // 첫 프레임에서 AR Foundation이 설정한 FOV 저장
        if (!isInitialized && arCamera != null)
        {
            baseFOV = arCamera.fieldOfView;
            isInitialized = true;
        }

        HandleTouchInput();

        // 부드러운 줌 전환
        if (Mathf.Abs(currentZoom - targetZoom) > 0.001f)
        {
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * smoothSpeed);
        }
    }

    void LateUpdate()
    {
        // AR Foundation이 FOV를 설정한 후, 우리가 다시 조절
        if (isInitialized && arCamera != null)
        {
            ApplyZoom();
        }
    }

    private void HandleTouchInput()
    {
#if UNITY_EDITOR
        return; // 에디터에서는 핀치 줌 비활성화 (실기기 전용)
#endif

        // 터치 입력이 2개일 때 (핀치 제스처)
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            // 두 터치 사이의 거리 계산
            float currentTouchDistance = Vector2.Distance(touch0.position, touch1.position);

            // 핀치 시작
            if (!isPinching)
            {
                previousTouchDistance = currentTouchDistance;
                isPinching = true;
            }
            // 핀치 진행 중
            else if (previousTouchDistance > 0)
            {
                // 거리 차이로 줌 계산
                float distanceDelta = currentTouchDistance - previousTouchDistance;

                // 줌 레벨 조정 (거리가 멀어지면 확대, 가까워지면 축소)
                targetZoom += distanceDelta * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

                previousTouchDistance = currentTouchDistance;

                // 줌 인디케이터 업데이트
                if (zoomIndicator != null)
                {
                    zoomIndicator.UpdateZoom(targetZoom);
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

    }

    /// <summary>
    /// 디지털 줌 적용 (FOV 조절)
    /// </summary>
    private void ApplyZoom()
    {
        // FOV를 조절하여 줌 효과 구현
        // 줌이 증가하면 FOV 감소 (시야각이 좁아짐 = 확대)
        // 줌이 감소하면 FOV 증가 (시야각이 넓어짐 = 축소)
        float targetFOV = baseFOV / currentZoom;
        arCamera.fieldOfView = targetFOV;
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
        targetZoom = defaultZoom;
        currentZoom = defaultZoom;
        ApplyZoom();

        if (zoomIndicator != null)
        {
            zoomIndicator.UpdateZoom(1.0f);
            zoomIndicator.HideAfterDelay(1f);
        }
    }
}
