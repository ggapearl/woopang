using UnityEngine;
using UnityEngine.XR.ARFoundation;  // ARCameraManager를 위한 네임스페이스
using Unity.XR.CoreUtils;
using Google.XR.ARCoreExtensions;

public class ARSessionOriginFix : MonoBehaviour
{
    public ARCoreExtensions arCoreExtensions;
    public XROrigin xrOrigin;

    void Start()
    {
        if (arCoreExtensions != null && xrOrigin != null)
        {
            // ARCameraManager 연결
            arCoreExtensions.CameraManager = xrOrigin.Camera.GetComponent<ARCameraManager>();
            Debug.Log("AR Session Origin 연결 완료");
        }
    }
}
