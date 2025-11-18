using System.Collections;
using System.Collections.Generic;
using Google.XR.ARCoreExtensions;
using TMPro;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class GeospatialManager : MonoBehaviour
{
    [Header("Core Features")]
    [SerializeField]
    private TextMeshProUGUI geospatialStatusText;

    [SerializeField]
    private AREarthManager earthManager;

    [SerializeField]
    private ARCoreExtensions arcoreExtensions;

    private bool waitingForLocationService = false;

    private Coroutine locationServiceLauncher;

    private void Awake()
    {
        // [삭제] 프레임 속도 설정 로그
        Application.targetFrameRate = 30;
    }

    void Update()
    {
        if (!Debug.isDebugBuild || earthManager == null)
        {
            // [삭제] 디버그 빌드 또는 earthManager null 로그
            return;
        }

        if (ARSession.state != ARSessionState.SessionInitializing &&
               ARSession.state != ARSessionState.SessionTracking)
        {
            // [삭제] ARSession 상태 로그
            return;
        }

        // Check feature support and enable Geospatial API when it's supported.
        var featureSupport = earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
        switch (featureSupport)
        {
            case FeatureSupported.Unknown:
                // [삭제] Geospatial 모드 지원 알 수 없음 로그
                break;
            case FeatureSupported.Unsupported:
                // [삭제] Geospatial API 미지원 로그
                break;
            case FeatureSupported.Supported:
                if (arcoreExtensions.ARCoreExtensionsConfig.GeospatialMode == GeospatialMode.Disabled)
                {
                    // [삭제] GeospatialMode 활성화 로그
                    arcoreExtensions.ARCoreExtensionsConfig.GeospatialMode = GeospatialMode.Enabled;
                    arcoreExtensions.ARCoreExtensionsConfig.StreetscapeGeometryMode = StreetscapeGeometryMode.Enabled;
                }
                break;
        }

        var pose = earthManager.EarthState == EarthState.Enabled &&
            earthManager.EarthTrackingState == TrackingState.Tracking ?
            earthManager.CameraGeospatialPose : new GeospatialPose();
        var supported = earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);

        if (geospatialStatusText != null)
        {
            geospatialStatusText.text =
                $"SessionState: {ARSession.state}\n" +
                $"LocationServiceStatus: {Input.location.status}\n" +
                $"FeatureSupported: {supported}\n" +
                $"EarthState: {earthManager.EarthState}\n" +
                $"EarthTrackingState: {earthManager.EarthTrackingState}\n" +
                $"  LAT/LNG: {pose.Latitude:F6}, {pose.Longitude:F6}\n" +
                $"  HorizontalAcc: {pose.HorizontalAccuracy:F6}\n" +
                $"  ALT: {pose.Altitude:F2}\n" +
                $"  VerticalAcc: {pose.VerticalAccuracy:F2}\n" +
                $"  EunRotation: {pose.EunRotation:F2}\n" +
                $"  OrientationYawAcc: {pose.OrientationYawAccuracy:F2}";
        }
    }

    private void OnEnable()
    {
        // [삭제] 위치 서비스 코루틴 시작 로그
        locationServiceLauncher = StartCoroutine(StartLocationService());
    }

    private void OnDisable()
    {
        // [삭제] 코루틴 및 위치 서비스 중지 로그
        if (locationServiceLauncher != null)
        {
            StopCoroutine(locationServiceLauncher);
        }
        locationServiceLauncher = null;
        Input.location.Stop();
    }

    private IEnumerator StartLocationService()
    {
        while (true)
        {
            waitingForLocationService = true;
            // [삭제] 위치 서비스 초기화 시작 로그

#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                // [삭제] 위치 권한 요청 로그
                Permission.RequestUserPermission(Permission.FineLocation);
                yield return new WaitForSeconds(3.0f);
            }
#endif

            if (!Input.location.isEnabledByUser)
            {
                // [삭제] 위치 서비스 비활성화 로그
                waitingForLocationService = false;
                yield return new WaitForSeconds(60.0f);
                continue;
            }

            // [삭제] 위치 서비스 시작 로그
            Input.location.Start();

            while (Input.location.status == LocationServiceStatus.Initializing)
            {
                // [삭제] 위치 서비스 초기화 대기 로그
                yield return null;
            }

            waitingForLocationService = false;
            if (Input.location.status != LocationServiceStatus.Running)
            {
                // [삭제] 위치 서비스 실패 로그
                Input.location.Stop();
            }
            else
            {
                // [삭제] 위치 서비스 성공 로그
            }

            yield return new WaitForSeconds(60.0f);
        }
    }
}