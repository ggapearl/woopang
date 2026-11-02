using UnityEngine;
using System.Collections;

/// <summary>
/// 위치 권한 및 카메라 권한을 요청합니다.
/// Android: UnityEngine.Android.Permission API 사용
/// iOS: Info.plist 설정 + 기능 사용 시 자동 요청
///
/// 참고: 실제 ARSession.Reset()은 DataManager.StartLocationServiceAndFetchData()에서 수행
/// (PermissionRequester 시점에서는 isEnabledByUser가 아직 false일 수 있음)
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
        }
        else
        {
        }

        // Android 카메라 권한 확인 및 요청
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
        }
        else
        {
        }

        #elif UNITY_IOS
        // iOS 위치 권한 요청
        StartCoroutine(RequestIOSLocationPermission());

        // iOS 카메라 권한은 ARSession이 시작될 때 자동으로 요청됨
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
