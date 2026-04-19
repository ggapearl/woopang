using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARCameraOptimizer : MonoBehaviour
{
    [Header("AR Camera 최적화 설정")]
    [SerializeField] private ARCameraManager arCameraManager;
    [SerializeField] private bool enableOptimization = true;
    [SerializeField] private int targetFrameRate = 60;

    private float lastFrameTime;
    private const float FRAME_INTERVAL = 1f / 60f;

    void Start()
    {
        if (arCameraManager == null)
            arCameraManager = FindFirstObjectByType<ARCameraManager>();

        if (enableOptimization)
        {
            OptimizeARCamera();
        }

        // 프레임레이트 제한
        Application.targetFrameRate = targetFrameRate;
    }
    
    void OptimizeARCamera()
    {
        if (arCameraManager == null) return;
        
        // iOS ARKit 최적화 설정
        arCameraManager.requestedLightEstimation = LightEstimation.None;
        // autoFocus 활성화 — 차량 이동 중 원근 전환 시 선명도 유지, 특징점 검출률↑ (InsufficientFeatures 감소)
        arCameraManager.autoFocusRequested = true;
        arCameraManager.requestedFacingDirection = CameraFacingDirection.World;
        
        // 렌더링 모드: BeforeOpaques가 더 안정적 (카메라 배경을 먼저 그리고 오브젝트를 위에 렌더링)
        // AfterOpaques는 depth 기반 렌더링으로 URP/SSAO와 충돌 가능
        arCameraManager.requestedBackgroundRenderingMode = CameraBackgroundRenderingMode.BeforeOpaques;
    }
    
    void Update()
    {
        if (!enableOptimization) return;

        // 프레임 간격 제어
        if (Time.time - lastFrameTime < FRAME_INTERVAL)
        {
            return;
        }

        lastFrameTime = Time.time;

        // iOS에서만 추가 최적화
        #if UNITY_IOS
        OptimizeForIOS();
        #endif
    }

    void OptimizeForIOS()
    {
        // ⚠️ ARFoundation 버전 불일치 문제로 인해 iOS 최적화 임시 비활성화
        // ARCameraManager를 껐다 켰다 하는 것이 텍스처 생성 에러를 악화시킴
        // TODO: ARCore Extensions를 Unity 6 + ARFoundation 6.x 지원 버전으로 업그레이드 후 재활성화
        return;

        // iOS Metal 렌더링 최적화 (비활성화됨)
        /*
        if (arCameraManager != null)
        {
            // 필요할 때만 렌더링 활성화
            bool shouldRender = ShouldRenderFrame();

            if (shouldRender != isRenderingActive)
            {
                isRenderingActive = shouldRender;

                if (!shouldRender)
                {
                    // 렌더링 일시 중단
                    arCameraManager.enabled = false;
                }
                else
                {
                    // 렌더링 재개
                    arCameraManager.enabled = true;
                }
            }
        }
        */
    }
    
    bool ShouldRenderFrame()
    {
        // 카메라가 움직이고 있거나 새로운 오브젝트가 생성될 때만 렌더링
        return true; // 기본값: 항상 렌더링 (필요에 따라 수정)
    }
    
    // 수동으로 렌더링 제어
    public void SetRenderingEnabled(bool enabled)
    {
        if (arCameraManager != null)
        {
            arCameraManager.enabled = enabled;
        }
    }
    
    // 렌더링 상태 확인
    public bool IsRenderingActive()
    {
        return arCameraManager != null && arCameraManager.enabled;
    }
}