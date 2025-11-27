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

    void Start()
    {
        // DataManager 자동 찾기
        if (dataManager == null)
        {
            dataManager = FindObjectOfType<DataManager>();
            if (dataManager == null)
            {
                Debug.LogWarning("[ARObjectZoomController] DataManager를 찾을 수 없습니다.");
            }
        }

        // TourAPIManager 자동 찾기
        if (tourAPIManager == null)
        {
            tourAPIManager = FindObjectOfType<TourAPIManager>();
            if (tourAPIManager == null)
            {
                Debug.LogWarning("[ARObjectZoomController] TourAPIManager를 찾을 수 없습니다.");
            }
        }

        // ZoomIndicator GameObject에서 컴포넌트 가져오기
        if (zoomIndicatorObject != null)
        {
            zoomIndicator = zoomIndicatorObject.GetComponent<ZoomIndicator>();
            if (zoomIndicator == null)
            {
                Debug.LogWarning("[ARObjectZoomController] ZoomIndicator 컴포넌트가 없습니다!");
            }
        }
        else
        {
            zoomIndicator = FindObjectOfType<ZoomIndicator>();
            if (zoomIndicator == null)
            {
                Debug.LogWarning("[ARObjectZoomController] ZoomIndicator를 찾을 수 없습니다. 줌 표시 기능이 비활성화됩니다.");
            }
        }

        // 기본 줌 설정
        currentZoom = defaultZoom;

        Debug.Log($"[ARObjectZoomController] 초기화 완료 - 기본 Zoom: {defaultZoom}");
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

                    // AR 오브젝트들의 스케일 조절
                    ApplyZoomToARObjects();

                    previousTouchDistance = currentTouchDistance;

                    // 줌 인디케이터 업데이트
                    if (zoomIndicator != null)
                    {
                        zoomIndicator.UpdateZoom(currentZoom);
                    }

                    Debug.Log($"[ARObjectZoomController] Zoom: {currentZoom:F2}x");
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

            // AR 오브젝트들의 스케일 조절
            ApplyZoomToARObjects();

            if (zoomIndicator != null)
            {
                zoomIndicator.UpdateZoom(currentZoom);
                zoomIndicator.HideAfterDelay(2f);
            }

            Debug.Log($"[ARObjectZoomController] Editor Zoom: {currentZoom:F2}x");
        }
#endif
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
            // TourAPIManager에는 GetSpawnedObjects가 Dictionary<string, GameObject>로 되어 있음
            var placeDataMap = tourAPIManager.GetPlaceDataMap();
            if (placeDataMap != null)
            {
                foreach (var kvp in placeDataMap)
                {
                    string contentId = kvp.Key;
                    // TourAPI는 contentId로 오브젝트를 찾아야 함
                    GameObject obj = GameObject.Find($"TourPlace_{contentId}");
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
