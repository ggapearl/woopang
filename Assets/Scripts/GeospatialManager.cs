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
        Application.targetFrameRate = 30;
    }

    private void Start()
    {
        // Inspector에서 연결 안 된 경우 자동 탐색
        if (earthManager == null)
        {
            earthManager = FindFirstObjectByType<AREarthManager>();
            Debug.Log($"[DBG] GeoMgr Start: earthManager 자동 탐색 → {(earthManager != null ? "성공" : "실패")}");
        }
        if (arcoreExtensions == null)
        {
            arcoreExtensions = FindFirstObjectByType<ARCoreExtensions>();
            Debug.Log($"[DBG] GeoMgr Start: arcoreExtensions 자동 탐색 → {(arcoreExtensions != null ? "성공" : "실패")}");
        }

        Debug.Log($"[DBG] GeoMgr Start: earthManager={earthManager != null}, arcoreExtensions={arcoreExtensions != null}");
    }

    private float _logTimer = 0f;

    void Update()
    {
        _logTimer += Time.deltaTime;
        bool shouldLog = false;
        if (_logTimer >= 5f)
        {
            _logTimer = 0f;
            shouldLog = true;
        }

        if (earthManager == null)
        {
            // 주기적으로 재탐색 시도
            if (shouldLog)
            {
                earthManager = FindFirstObjectByType<AREarthManager>();
                Debug.Log($"[DBG] GeoMgr: earthManager null → 재탐색 {(earthManager != null ? "성공" : "실패")}");
            }
            if (earthManager == null) return;
        }

        if (arcoreExtensions == null)
        {
            arcoreExtensions = FindFirstObjectByType<ARCoreExtensions>();
            if (arcoreExtensions == null) return;
        }

        if (ARSession.state != ARSessionState.SessionInitializing &&
               ARSession.state != ARSessionState.SessionTracking)
        {
            if (shouldLog)
                Debug.Log($"[DBG] GeoMgr: ARSession.state={ARSession.state} → skip");
            return;
        }

        // Check feature support and enable Geospatial API when it's supported.
        var featureSupport = earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);

        if (shouldLog)
        {
            Debug.Log($"[DBG] GeoMgr: featureSupport={featureSupport}, GeoMode={arcoreExtensions?.ARCoreExtensionsConfig?.GeospatialMode}, EarthState={earthManager.EarthState}, EarthTracking={earthManager.EarthTrackingState}");
        }

        switch (featureSupport)
        {
            case FeatureSupported.Unknown:
                break;
            case FeatureSupported.Unsupported:
                break;
            case FeatureSupported.Supported:
                if (arcoreExtensions.ARCoreExtensionsConfig.GeospatialMode == GeospatialMode.Disabled)
                {
                    Debug.Log("[DBG] GeoMgr: GeospatialMode Disabled → Enabled 전환");
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
        locationServiceLauncher = StartCoroutine(StartLocationService());
    }

    private void OnDisable()
    {
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
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
                yield return new WaitForSeconds(3.0f);
            }
#endif

            if (!Input.location.isEnabledByUser)
            {
                yield return new WaitForSeconds(60.0f);
                continue;
            }

            Input.location.Start();

            while (Input.location.status == LocationServiceStatus.Initializing)
            {
                yield return null;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Input.location.Stop();
            }

            yield return new WaitForSeconds(60.0f);
        }
    }
}
