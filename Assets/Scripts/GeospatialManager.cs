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

    private Coroutine locationServiceLauncher;

    private void Awake()
    {
        // [����] ������ �ӵ� ���� �α�
        Application.targetFrameRate = 30;
    }

    private float _logTimer = 0f;

    void Update()
    {
        // 5초마다 상태 로그 출력
        _logTimer += Time.deltaTime;
        if (_logTimer >= 5f)
        {
            _logTimer = 0f;
        }

        if (earthManager == null) return;

        if (ARSession.state != ARSessionState.SessionInitializing &&
               ARSession.state != ARSessionState.SessionTracking)
        {
            return;
        }

        // Check feature support and enable Geospatial API when it's supported.
        var featureSupport = earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
        switch (featureSupport)
        {
            case FeatureSupported.Unknown:
                break;
            case FeatureSupported.Unsupported:
                break;
            case FeatureSupported.Supported:
                if (arcoreExtensions.ARCoreExtensionsConfig.GeospatialMode == GeospatialMode.Disabled)
                {
                    arcoreExtensions.ARCoreExtensionsConfig.GeospatialMode = GeospatialMode.Enabled;
                    arcoreExtensions.ARCoreExtensionsConfig.StreetscapeGeometryMode = StreetscapeGeometryMode.Enabled;
                }
                break;
        }

        // 디버그 UI 표시 (Debug Build에서만)
        if (!Debug.isDebugBuild) return;

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
        // [����] ��ġ ���� �ڷ�ƾ ���� �α�
        locationServiceLauncher = StartCoroutine(StartLocationService());
    }

    private void OnDisable()
    {
        // [����] �ڷ�ƾ �� ��ġ ���� ���� �α�
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

            // [����] ��ġ ���� �ʱ�ȭ ���� �α�

#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                // [����] ��ġ ���� ��û �α�
                Permission.RequestUserPermission(Permission.FineLocation);
                yield return new WaitForSeconds(3.0f);
            }
#endif

            if (!Input.location.isEnabledByUser)
            {
                // [����] ��ġ ���� ��Ȱ��ȭ �α�

                yield return new WaitForSeconds(60.0f);
                continue;
            }

            // [����] ��ġ ���� ���� �α�
            Input.location.Start();

            while (Input.location.status == LocationServiceStatus.Initializing)
            {
                // [����] ��ġ ���� �ʱ�ȭ ��� �α�
                yield return null;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                // [����] ��ġ ���� ���� �α�
                Input.location.Stop();
            }
            else
            {
                // [����] ��ġ ���� ���� �α�
            }

            yield return new WaitForSeconds(60.0f);
        }
    }
}