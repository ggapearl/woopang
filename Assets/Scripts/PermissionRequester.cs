using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Google.XR.ARCoreExtensions;
using System.Collections;

/// <summary>
/// 위치 권한 및 카메라 권한을 요청합니다.
/// Android: UnityEngine.Android.Permission API 사용
/// iOS: Info.plist 설정 + 기능 사용 시 자동 요청
///
/// 첫 설치 시 ARSession이 권한 부여 전에 시작되면 Geospatial이 초기화 안 됨.
/// 권한 부여 직후 ARSession.Reset()으로 재초기화하여 해결.
/// </summary>
public class PermissionRequester : MonoBehaviour
{
    void Start()
    {
        RequestPermissions();
    }

    void RequestPermissions()
    {
        #if UNITY_ANDROID
        // Android 위치 권한 확인 및 요청
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
            Debug.Log("[Android] Requesting Fine Location Permission...");
        }
        else
        {
            Debug.Log("[Android] Fine Location Permission Already Granted");
        }

        // Android 카메라 권한 확인 및 요청
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
            Debug.Log("[Android] Requesting Camera Permission...");
        }
        else
        {
            Debug.Log("[Android] Camera Permission Already Granted");
        }

        #elif UNITY_IOS
        // iOS 위치 권한 요청
        Debug.Log("[iOS] Requesting Location Permission (via Input.location.Start)...");
        StartCoroutine(RequestIOSLocationPermission());

        // iOS 카메라 권한은 ARSession이 시작될 때 자동으로 요청됨
        Debug.Log("[iOS] Camera permission will be requested when ARSession starts");
        #endif
    }

    #if UNITY_IOS
    private IEnumerator RequestIOSLocationPermission()
    {
        // iOS 위치 서비스 활성화 여부 확인
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("[iOS] Location services are disabled by user in Settings");
            yield break;
        }

        // 위치 서비스 시작 (이 시점에 권한 프롬프트 표시)
        Input.location.Start();

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Running)
        {
            Debug.Log("[DBG] iOS: Location Permission Granted");

            // Geospatial이 이미 초기화되어있으면 리셋 불필요
            AREarthManager earthManager = FindFirstObjectByType<AREarthManager>();
            if (earthManager != null && earthManager.EarthTrackingState == TrackingState.Tracking)
            {
                Debug.Log("[DBG] iOS: Earth 이미 Tracking → ARSession 리셋 불필요");
            }
            else
            {
                // 첫 설치 시 ARSession이 권한 없이 시작되면 Geospatial 초기화 안 됨
                // 리셋하면 권한이 있는 상태에서 재초기화 → 껐다 켰을 때와 동일한 효과
                ARSession arSession = FindFirstObjectByType<ARSession>();
                if (arSession != null)
                {
                    Debug.Log("[DBG] iOS: Earth NOT Tracking → ARSession.Reset() 호출");
                    arSession.Reset();
                }
            }
        }
        else if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogError("[iOS] Location Permission Denied or Failed");
        }
        else
        {
            Debug.LogWarning($"[iOS] Location status: {Input.location.status}");
        }
    }
    #endif
}
