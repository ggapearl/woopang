using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch; // New Input System 추가
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch; // New Input System Touch 클래스 사용
using System.Collections.Generic;

/// <summary>
/// AR 환경에서 작동하는 줌 컨트롤러
/// 카메라 FOV 대신 AR 오브젝트들의 스케일을 조절하여 줌 효과 구현
/// New Input System (EnhancedTouch) 적용됨
/// </summary>
public class ARObjectZoomController : MonoBehaviour
{
    [Header("Zoom Settings")]
    [SerializeField] private float defaultZoom = 1f; // 기본 줌 (1.0)
    [SerializeField] private float minZoom = 0.5f; // 최소 줌 (축소 - 오브젝트가 작아짐)
    [SerializeField] private float maxZoom = 3f; // 최대 줌 (확대 - 오브젝트가 커짐)
    [SerializeField] private float zoomSpeed = 0.01f; // 줌 속도

    [Header("Zoom Indicator")]
    [SerializeField] private GameObject zoomIndicatorObject; // 줌 인디케이터 GameObject
    private ZoomIndicator zoomIndicator; // 줌 인디케이터 컴포넌트

    [Header("AR Object Managers")]
    [SerializeField] private DataManager dataManager; // 우팡 데이터 매니저
    [SerializeField] private TourAPIManager tourAPIManager; // 공공데이터 매니저

    private float currentZoom = 1f;
    private float previousTouchDistance = 0f;
    private bool isPinching = false;
    private int lastTouchCount = 0; // 디버깅용 마지막 터치 개수

    void OnEnable()
    {
        EnhancedTouchSupport.Enable(); // EnhancedTouch 활성화
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable(); // EnhancedTouch 비활성화
    }

    void Start()
    {
        // DataManager Singleton 사용
        if (dataManager == null)
        {
            dataManager = DataManager.Instance;
            if (dataManager == null)
            {
                Debug.LogWarning("[ARObjectZoomController] DataManager를 찾을 수 없습니다.");
            }
        }

        // TourAPIManager Singleton 사용
        if (tourAPIManager == null)
        {
            tourAPIManager = TourAPIManager.Instance;
            if (tourAPIManager == null)
            {
                Debug.LogWarning("[ARObjectZoomController] TourAPIManager를 찾을 수 없습니다.");
            }
        }

        // ZoomIndicator 연결 로직 개선
        if (zoomIndicatorObject != null)
        {
            zoomIndicator = zoomIndicatorObject.GetComponent<ZoomIndicator>();
        }
        
        // Inspector에 할당되지 않았거나 컴포넌트를 못 찾은 경우 씬에서 탐색
        if (zoomIndicator == null)
        {
            zoomIndicator = FindFirstObjectByType<ZoomIndicator>(FindObjectsInactive.Include);
            if (zoomIndicator != null)
            {
                zoomIndicatorObject = zoomIndicator.gameObject;
            }
        }

        // 기본 줌 설정
        currentZoom = defaultZoom;
    }

    void Update()
    {
#if UNITY_EDITOR
        // 에디터에서 마우스 스크롤 및 키보드로 줌 테스트 (New Input System 대응)
        float scroll = 0f;
        
        // 1. 마우스 스크롤 시도
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            Vector2 scrollVal = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue();
            scroll = scrollVal.y;
        }

        // 2. 키보드 화살표 시도 (Fallback)
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.upArrowKey.isPressed)
            {
                scroll = 1f; // 확대
            }
            else if (UnityEngine.InputSystem.Keyboard.current.downArrowKey.isPressed)
            {
                scroll = -1f; // 축소
            }
        }

        if (Mathf.Abs(scroll) > 0.001f)
        {
            // 스크롤 방향에 따라 줌 조절
            // 에디터에서는 zoomSpeed를 조금 더 크게 적용
            currentZoom += scroll * zoomSpeed * 0.5f; // 민감도 조정
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

            ApplyZoomToARObjects();

            if (zoomIndicator != null)
            {
                zoomIndicator.UpdateZoom(currentZoom);
                zoomIndicator.HideAfterDelay(5f);
            }
        }
#endif

        // 터치 입력이 2개일 때 (핀치 제스처)
        if (Touch.activeTouches.Count == 2)
        {
            Touch touch0 = Touch.activeTouches[0];
            Touch touch1 = Touch.activeTouches[1];

            // 두 터치 사이의 거리 계산
            float currentTouchDistance = Vector2.Distance(touch0.screenPosition, touch1.screenPosition);

            // 핀치 시작
            if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Began || touch1.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                previousTouchDistance = currentTouchDistance;
                isPinching = true;
            }
            // 핀치 진행 중
            else if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch1.phase == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                if (isPinching && previousTouchDistance > 0)
                {
                    // 거리 차이로 줌 계산
                    float distanceDelta = currentTouchDistance - previousTouchDistance;

                    // 줌 레벨 조정 (거리가 멀어지면 확대, 가까워지면 축소)
                    // 화면 해상도에 따른 속도 보정을 위해 Screen.height로 나눌 수도 있음 (선택사항)
                    // 여기서는 기존 zoomSpeed를 그대로 사용
                    currentZoom += distanceDelta * zoomSpeed;
                    currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

                    // AR 오브젝트들의 스케일 조절
                    ApplyZoomToARObjects();

                    previousTouchDistance = currentTouchDistance;

                    // 줌 인디케이터 업데이트
                    if (zoomIndicator != null)
                    {
                        zoomIndicator.UpdateZoom(currentZoom);
                    }

                    // 너무 잦은 로그는 성능 저하 유발 가능 -> 주석 처리 또는 조건부 로그
                    // Debug.Log($"[ARObjectZoomController] 핀치 줌! delta: {distanceDelta:F2}, Zoom: {currentZoom:F2}x");
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

                // 줌 인디케이터 숨김 (5초 딜레이)
                if (zoomIndicator != null)
                {
                    zoomIndicator.HideAfterDelay(5f);
                }
            }
        }

    }

    /// <summary>
    /// AR 오브젝트들의 스케일을 조절하여 줌 효과 적용
    /// </summary>
    private void ApplyZoomToARObjects()
    {
        // 우팡 데이터 오브젝트들 스케일 조절
        if (dataManager != null)
        {
            var spawnedObjects = dataManager.GetSpawnedObjects();
            if (spawnedObjects != null)
            {
                foreach (var kvp in spawnedObjects)
                {
                    GameObject obj = kvp.Value;
                    if (obj != null && obj.activeSelf)
                    {
                        // 기본 스케일에 줌 배율 곱하기
                        obj.transform.localScale = Vector3.one * currentZoom;
                    }
                }
            }
        }

        // 공공데이터 오브젝트들 스케일 조절
        if (tourAPIManager != null)
        {
            // ⭐ GetSpawnedObjects() 사용으로 최적화 (GameObject.Find 제거)
            var tourSpawnedObjects = tourAPIManager.GetSpawnedObjects();
            if (tourSpawnedObjects != null)
            {
                foreach (var kvp in tourSpawnedObjects)
                {
                    GameObject obj = kvp.Value;
                    if (obj != null && obj.activeSelf)
                    {
                        obj.transform.localScale = Vector3.one * currentZoom;
                    }
                }
            }
        }
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
        ApplyZoomToARObjects();

        if (zoomIndicator != null)
        {
            zoomIndicator.UpdateZoom(1.0f);
            zoomIndicator.HideAfterDelay(1f);
        }

        Debug.Log("[ARObjectZoomController] 줌 초기화");
    }
}
