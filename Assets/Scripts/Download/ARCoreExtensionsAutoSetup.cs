using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// XR Origin에 ARCoreExtensions 컴포넌트가 없으면 자동으로 추가
/// Unity 6 + ARCore Extensions 1.51.0 필수
/// </summary>
public class ARCoreExtensionsAutoSetup : MonoBehaviour
{
    [SerializeField] private ARCoreExtensionsConfig config;

    void Awake()
    {
        // ARSession 찾기 (별도 GameObject에 있음)
        ARSession arSession = FindFirstObjectByType<ARSession>();
        if (arSession == null)
        {
            Debug.LogError("[ARCoreExtensionsAutoSetup] ARSession을 찾을 수 없습니다!");
            return;
        }

        // XR Origin 찾기 (ARSession과는 별도 GameObject!)
        Unity.XR.CoreUtils.XROrigin xrOriginComponent = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOriginComponent == null)
        {
            Debug.LogError("[ARCoreExtensionsAutoSetup] XROrigin을 찾을 수 없습니다!");
            return;
        }

        GameObject xrOrigin = xrOriginComponent.gameObject;

        // ARCoreExtensions가 있는지 확인
        ARCoreExtensions arCoreExtensions = xrOrigin.GetComponent<ARCoreExtensions>();

        if (arCoreExtensions == null)
        {
            Debug.LogWarning($"[ARCoreExtensionsAutoSetup] {xrOrigin.name}에 ARCoreExtensions가 없습니다. 자동으로 추가합니다.");

            // ARCoreExtensions 추가
            arCoreExtensions = xrOrigin.AddComponent<ARCoreExtensions>();
        }
        else
        {
        }

        // ⭐ CRITICAL: Unity 6에서는 XROrigin을 절대 제거하면 안 됩니다!
        // XROrigin은 Unity 6 + AR Foundation 6.x에서 필수 컴포넌트입니다.
        // ARSessionOrigin은 deprecated되었으며, Unity 6에서는 사용하지 않습니다.

        // Unity 6 + ARCore Extensions 1.48에서는:
        // SessionOrigin 필드를 설정하지 않아도 ARCore Extensions가 내부적으로 XROrigin을 찾아서 사용합니다.
        // 따라서 SessionOrigin 필드는 null로 두어도 됩니다.


        // Session 필드도 설정
        arCoreExtensions.Session = arSession;

        // CameraManager 설정
        ARCameraManager cameraManager = xrOrigin.GetComponentInChildren<ARCameraManager>();
        if (cameraManager != null)
        {
            arCoreExtensions.CameraManager = cameraManager;
        }

        // Config 설정 (ARCoreExtensions가 추가되었거나 이미 있는 경우 모두 설정)
        if (config != null)
        {
            arCoreExtensions.ARCoreExtensionsConfig = config;
        }
        else
        {
            Debug.LogError("[ARCoreExtensionsAutoSetup] ARCoreExtensionsConfig가 null입니다! Inspector에서 config를 연결하세요.");
        }
    }
}
