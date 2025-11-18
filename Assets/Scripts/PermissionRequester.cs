using UnityEngine;

public class PermissionRequester : MonoBehaviour
{
    void Start()
    {
        RequestPermissions();
    }

    /// <summary>
    /// 위치 권한 및 카메라 권한을 요청합니다.
    /// </summary>
    void RequestPermissions()
    {
        #if UNITY_ANDROID
        // 위치 권한 확인 및 요청
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
            Debug.Log("Requesting Fine Location Permission...");
        }
        else
        {
            Debug.Log("Fine Location Permission Already Granted");
        }

        // 카메라 권한 확인 및 요청
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
            Debug.Log("Requesting Camera Permission...");
        }
        else
        {
            Debug.Log("Camera Permission Already Granted");
        }
        #endif
    }
}
