using UnityEngine;
using System.Collections;

/// <summary>
/// 위치 권한 및 카메라 권한을 요청합니다.
/// Android: UnityEngine.Android.Permission API 사용
/// iOS: Info.plist 설정 + 기능 사용 시 자동 요청
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
        // iOS에서는 Info.plist에 NSLocationWhenInUseUsageDescription이 설정되어 있고
        // Input.location.Start()를 호출하면 자동으로 권한 프롬프트가 표시됨
        Debug.Log("[iOS] Requesting Location Permission (via Input.location.Start)...");
        StartCoroutine(RequestIOSLocationPermission());

        // iOS 카메라 권한은 ARSession이 시작될 때 자동으로 요청됨
        // (Info.plist에 NSCameraUsageDescription 필요)
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

        int maxWait = 20; // 최대 20초 대기
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            Debug.Log($"[iOS] Waiting for location permission... ({maxWait}s remaining)");
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Running)
        {
            Debug.Log("[iOS] ✅ Location Permission Granted");
        }
        else if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogError("[iOS] ❌ Location Permission Denied or Failed");
        }
        else
        {
            Debug.LogWarning($"[iOS] Location status: {Input.location.status}");
        }
    }
    #endif
}
