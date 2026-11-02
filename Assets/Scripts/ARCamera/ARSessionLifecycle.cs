using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// iOS 워치독 타임아웃(0x8BADF00D) 회피를 위한 ARSession graceful shutdown.
/// - OnApplicationPause(true) / OnApplicationQuit() 시 ARSession을 명시적으로 중단
/// - ARKit AVCaptureSession과 ARCore Geospatial 백그라운드 스레드가 정리될 시간 확보
/// - Unity 종료 시 IL2CPP 스레드 join 대기에서 교착되는 것을 방지
/// </summary>
public class ARSessionLifecycle : MonoBehaviour
{
    private ARSession arSession;

    private void Awake()
    {
        arSession = FindFirstObjectByType<ARSession>();
    }

    private void OnApplicationPause(bool paused)
    {
        if (arSession == null) return;

        if (paused)
        {
            arSession.enabled = false;
        }
        else
        {
            arSession.enabled = true;
        }
    }

    private void OnApplicationQuit()
    {
        if (arSession == null) return;

        arSession.enabled = false;
        StartCoroutine(YieldOneFrame());
    }

    private IEnumerator YieldOneFrame()
    {
        yield return null;
    }
}
