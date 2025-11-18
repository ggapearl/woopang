using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARCameraOptimizer : MonoBehaviour
{
    [Header("AR Camera 최적화 설정")]
    [SerializeField] private ARCameraManager arCameraManager;
    [SerializeField] private bool enableOptimization = true;
    [SerializeField] private int targetFrameRate = 30;
    
    private bool isRenderingActive = true;
    private float lastFrameTime;
    private const float FRAME_INTERVAL = 1f / 30f; // 30fps 제한
    
    void Start()
    {
        if (arCameraManager == null)
            arCameraManager = FindObjectOfType<ARCameraManager>();
            
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
        arCameraManager.autoFocusRequested = false;
        arCameraManager.requestedFacingDirection = CameraFacingDirection.World;
        
        // 렌더링 모드 최적화
        arCameraManager.requestedBackgroundRenderingMode = CameraBackgroundRenderingMode.AfterOpaques;
        
        Debug.Log("[ARCameraOptimizer] AR 카메라 최적화 완료");
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
        // iOS Metal 렌더링 최적화
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