using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Google.XR.ARCoreExtensions.GeospatialCreator;

public class UploadPreviewManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button previewButton;
    [SerializeField] private Button exitPreviewButton;
    [SerializeField] private GameObject uploadPage;
    [SerializeField] private GameObject previewPage;

    [Header("Preview Settings")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private Camera arCamera;

    private GameObject previewObject;
    private ModelUploadManager uploadManager;
    private DataManager dataManager;
    private bool isPreviewMode = false;

    private void Start()
    {
        uploadManager = FindObjectOfType<ModelUploadManager>();
        dataManager = FindObjectOfType<DataManager>();

        if (previewButton != null)
        {
            previewButton.onClick.AddListener(StartPreview);
        }

        if (exitPreviewButton != null)
        {
            exitPreviewButton.onClick.AddListener(ExitPreview);
        }

        if (previewPage != null)
        {
            previewPage.SetActive(false);
        }
    }

    private void StartPreview()
    {
        // GPS 데이터 확인
        if (!Input.location.isEnabledByUser || Input.location.status != LocationServiceStatus.Running)
        {
            Debug.LogWarning("[Preview] 위치 서비스가 실행 중이 아님");
            return;
        }

        LocationInfo location = Input.location.lastData;

        // WGS84 고도 계산 (ModelUploadManager와 동일한 로직)
        float latitude = location.latitude;
        float longitude = location.longitude;
        float altitude = location.altitude;

        #if UNITY_IOS
        altitude = GeoidHeightCalculator.GeoidToWGS84(altitude, latitude, longitude);
        #endif

        // 다른 오브젝트 숨기기
        if (dataManager != null)
        {
            dataManager.HideAllObjects();
        }

        // 미리보기 오브젝트 생성
        if (cubePrefab != null)
        {
            previewObject = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);

            // GPS 앵커 설정
            CustomARGeospatialCreatorAnchor anchor = previewObject.GetComponentInChildren<CustomARGeospatialCreatorAnchor>();
            if (anchor != null)
            {
                anchor.SetCoordinatesAndCreateAnchor(latitude, longitude, altitude);
                Debug.Log($"[Preview] 미리보기 생성 - Lat:{latitude:F4}, Lon:{longitude:F4}, Alt:{altitude:F2}");
            }

            // DoubleTap3D 컴포넌트 활성화 (터치 가능하도록)
            DoubleTap3D doubleTap = previewObject.GetComponentInChildren<DoubleTap3D>();
            if (doubleTap != null)
            {
                doubleTap.enabled = true;
            }
        }

        // UI 전환
        if (uploadPage != null) uploadPage.SetActive(false);
        if (previewPage != null) previewPage.SetActive(true);

        isPreviewMode = true;
    }

    private void ExitPreview()
    {
        // 미리보기 오브젝트 삭제
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }

        // 다른 오브젝트 다시 표시
        if (dataManager != null)
        {
            dataManager.ShowAllObjects();
        }

        // UI 전환
        if (previewPage != null) previewPage.SetActive(false);
        if (uploadPage != null) uploadPage.SetActive(true);

        isPreviewMode = false;
    }

    private void OnDisable()
    {
        if (isPreviewMode)
        {
            ExitPreview();
        }
    }
}
