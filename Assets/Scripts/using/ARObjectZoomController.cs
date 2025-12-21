using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AR 환경에서 작동하는 줌 컨트롤러
/// 카메라 FOV 대신 AR 오브젝트들의 스케일을 조절하여 줌 효과 구현
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

        // ZoomIndicator GameObject에서 컴포넌트 가져오기 (옵션)
        if (zoomIndicatorObject != null)
        {
            zoomIndicator = zoomIndicatorObject.GetComponent<ZoomIndicator>();
        }
        else
        {
            zoomIndicator = FindObjectOfType<ZoomIndicator>();
        }

        // 기본 줌 설정
        currentZoom = defaultZoom;

        Debug.Log($"[ARObjectZoomController] 초기화 완료 - 기본 Zoom: {defaultZoom}");
    }

    void Update()
    {
        // ⭐ 디버깅: 터치 개수 로그 (매 프레임이 아닌 변화 시에만)
        if (Input.touchCount > 0 && Input.touchCount != lastTouchCount)
        {
            Debug.Log($"[ARObjectZoomController] 터치 감지됨 - touchCount: {Input.touchCount}");
            lastTouchCount = Input.touchCount;
        }
        else if (Input.touchCount == 0 && lastTouchCount > 0)
        {
            Debug.Log($"[ARObjectZoomController] 터치 종료");
            lastTouchCount = 0;
        }

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
                Debug.Log($"[ARObjectZoomController] 핀치 시작! 거리: {currentTouchDistance:F2}px");
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

                    // AR 오브젝트들의 스케일 조절
                    ApplyZoomToARObjects();

                    previousTouchDistance = currentTouchDistance;

                    // 줌 인디케이터 업데이트
                    if (zoomIndicator != null)
                    {
                        zoomIndicator.UpdateZoom(currentZoom);
                    }

                    Debug.Log($"[ARObjectZoomController] 핀치 줌! delta: {distanceDelta:F2}, Zoom: {currentZoom:F2}x");
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
                Debug.Log($"[ARObjectZoomController] 핀치 종료! 최종 Zoom: {currentZoom:F2}x");

                // 줌 인디케이터 숨김 (2초 딜레이)
                if (zoomIndicator != null)
                {
                    zoomIndicator.HideAfterDelay(2f);
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
