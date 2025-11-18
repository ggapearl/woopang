using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class TouchManager : MonoBehaviour
{
    [Header("디버깅")]
    public bool enableDebugLogs = true;
    public bool enableDetailedLogs = false;
    
    [Header("디버깅 키워드")]
    [TextArea(3, 10)]
    public string debugKeywords = "TOUCH_MANAGER_INIT, TOUCH_DETECTED_EDITOR, TOUCH_DETECTED_DEVICE, UI_BLOCK, UI_BASIC_CHECK, UI_RAYCAST_CHECK, UI_RAYCAST_RESULT, UI_CANVAS_CHECK, UI_CANVAS_DETAIL, UI_CANVAS_HIT, NO_UI_TOUCH, 3D_RAYCAST, 3D_HIT, 3D_MISS, AR_OBJECT_HIT, AR_OBJECT_MISS, DOUBLETAP_COMPONENT, TAP_TIMING, COLLIDER_CHECK, LAYER_CHECK, TOUCHABLE_INTERFACE, DOUBLETAP3D_FOUND, NO_TOUCHABLE_COMPONENT, EVENTSYSTEM_MISSING, EVENTSYSTEM_DISABLED, CAMERA_MISSING, LAYER_CHANGE, ALL_TOUCH_DISABLE, ALL_TOUCH_ENABLE";
    
    [Header("터치 설정")]
    public LayerMask touchableLayerMask = -1; // 터치 가능한 레이어들
    
    private static TouchManager instance;
    public static TouchManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<TouchManager>();
                if (instance == null)
                {
                    GameObject touchManagerObject = new GameObject("TouchManager");
                    instance = touchManagerObject.AddComponent<TouchManager>();
                    DontDestroyOnLoad(touchManagerObject);
                }
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        // 싱글톤 패턴
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (enableDebugLogs)
                Debug.Log("[TOUCH_MANAGER_INIT] TouchManager 초기화 완료");
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    void Update()
    {
        HandleTouch();
    }
    
    void HandleTouch()
    {
        // 터치 또는 마우스 클릭 감지
        bool touchDetected = false;
        Vector2 touchPosition = Vector2.zero;
        int touchFingerId = -1;
        
#if UNITY_EDITOR
        // 에디터에서는 마우스 클릭
        if (Input.GetMouseButtonDown(0))
        {
            touchDetected = true;
            touchPosition = Input.mousePosition;
            
            if (enableDetailedLogs)
                Debug.Log($"[TOUCH_DETECTED_EDITOR] 에디터 마우스 클릭 감지: {touchPosition}");
        }
#else
        // 디바이스에서는 터치
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            touchDetected = true;
            Touch touch = Input.GetTouch(0);
            touchPosition = touch.position;
            touchFingerId = touch.fingerId;
            
            if (enableDetailedLogs)
                Debug.Log($"[TOUCH_DETECTED_DEVICE] 디바이스 터치 감지: {touchPosition}, FingerID: {touchFingerId}");
        }
#endif
        
        if (!touchDetected) return;
        
        // UI 터치 검사
        if (IsUITouch(touchPosition, touchFingerId))
        {
            if (enableDebugLogs)
                Debug.Log("[UI_BLOCK] UI 터치 감지됨 - 3D 터치 차단");
            return;
        }
        
        // 3D 오브젝트 터치 처리
        Handle3DTouch(touchPosition);
    }
    
    /// <summary>
    /// AR 오브젝트 상세 분석
    /// </summary>
    void AnalyzeARObject(RaycastHit hit)
    {
        GameObject hitObject = hit.collider.gameObject;
        
        if (enableDetailedLogs)
        {
            Debug.Log($"[AR_OBJECT_HIT] === AR 오브젝트 분석 시작 ===");
            Debug.Log($"[AR_OBJECT_HIT] 오브젝트명: {hitObject.name}");
            Debug.Log($"[AR_OBJECT_HIT] 태그: {hitObject.tag}");
            Debug.Log($"[AR_OBJECT_HIT] 레이어: {hitObject.layer} ({LayerMask.LayerToName(hitObject.layer)})");
            Debug.Log($"[AR_OBJECT_HIT] 위치: {hitObject.transform.position}");
            Debug.Log($"[AR_OBJECT_HIT] 활성화: {hitObject.activeInHierarchy}");
            
            // Collider 정보
            Collider collider = hit.collider;
            Debug.Log($"[COLLIDER_CHECK] Collider 타입: {collider.GetType().Name}");
            Debug.Log($"[COLLIDER_CHECK] Collider 활성화: {collider.enabled}");
            Debug.Log($"[COLLIDER_CHECK] IsTrigger: {collider.isTrigger}");
            
            // 컴포넌트 목록
            Component[] components = hitObject.GetComponents<Component>();
            Debug.Log($"[AR_OBJECT_HIT] 컴포넌트 수: {components.Length}");
            foreach (Component comp in components)
            {
                if (comp != null)
                    Debug.Log($"[AR_OBJECT_HIT]   - {comp.GetType().Name}");
            }
            
            // 부모/자식 구조
            if (hitObject.transform.parent != null)
                Debug.Log($"[AR_OBJECT_HIT] 부모: {hitObject.transform.parent.name}");
            
            Debug.Log($"[AR_OBJECT_HIT] 자식 수: {hitObject.transform.childCount}");
            
            Debug.Log($"[AR_OBJECT_HIT] === AR 오브젝트 분석 완료 ===");
        }
    }
    
    /// <summary>
    /// DoubleTap3D 동작 분석
    /// </summary>
    void AnalyzeDoubleTapBehavior(DoubleTap3D doubleTap, RaycastHit hit)
    {
        if (enableDetailedLogs)
        {
            Debug.Log($"[DOUBLETAP_COMPONENT] === DoubleTap3D 분석 시작 ===");
            Debug.Log($"[DOUBLETAP_COMPONENT] DoubleTap3D 활성화: {doubleTap.enabled}");
            Debug.Log($"[DOUBLETAP_COMPONENT] GameObject 활성화: {doubleTap.gameObject.activeInHierarchy}");
            
            // DoubleTap3D 설정값들 (public 필드만 접근 가능)
            Debug.Log($"[DOUBLETAP_COMPONENT] Tap Speed: {doubleTap.tapSpeed}");
            Debug.Log($"[DOUBLETAP_COMPONENT] Swipe Threshold: {doubleTap.swipeThreshold}");
            Debug.Log($"[DOUBLETAP_COMPONENT] Fade Duration: {doubleTap.fadeDuration}");
            
            // UI 연결 상태 확인
            bool hasFullscreenCanvas = doubleTap.fullscreenCanvasGroup != null;
            bool hasGuidePanel = doubleTap.guidePanel != null;
            bool hasFullscreenImage = doubleTap.fullscreenImage != null;
            
            Debug.Log($"[DOUBLETAP_COMPONENT] UI 연결 - Canvas: {hasFullscreenCanvas}, GuidePanel: {hasGuidePanel}, FullscreenImage: {hasFullscreenImage}");
            
            if (hasFullscreenCanvas)
            {
                bool canvasActive = doubleTap.fullscreenCanvasGroup.gameObject.activeInHierarchy;
                Debug.Log($"[DOUBLETAP_COMPONENT] FullscreenCanvas 활성화: {canvasActive}");
                
                Canvas canvas = doubleTap.fullscreenCanvasGroup.GetComponent<Canvas>();
                if (canvas != null)
                {
                    Debug.Log($"[DOUBLETAP_COMPONENT] Canvas SortOrder: {canvas.sortingOrder}");
                    Debug.Log($"[DOUBLETAP_COMPONENT] Canvas RenderMode: {canvas.renderMode}");
                }
            }
            
            Debug.Log($"[DOUBLETAP_COMPONENT] === DoubleTap3D 분석 완료 ===");
        }
        
        // 터치 타이밍 분석
        if (enableDebugLogs)
        {
            float currentTime = Time.time;
            Debug.Log($"[TAP_TIMING] 현재 터치 시간: {currentTime:F3}");
            Debug.Log($"[TAP_TIMING] DoubleTap3D가 자체적으로 터치 타이밍을 관리합니다");
        }
    }
    
    /// <summary>
    /// UI 터치인지 확인
    /// </summary>
    bool IsUITouch(Vector2 screenPosition, int fingerId = -1)
    {
        // EventSystem 존재 확인
        if (EventSystem.current == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[EVENTSYSTEM_MISSING] EventSystem이 없습니다!");
            return false;
        }
        
        bool eventSystemEnabled = EventSystem.current.enabled;
        if (!eventSystemEnabled)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[EVENTSYSTEM_DISABLED] EventSystem이 비활성화되어 있습니다!");
            return false;
        }
        
        // 방법 1: 기본 UI 터치 검사
        bool isOverUI = false;
#if UNITY_EDITOR
        isOverUI = EventSystem.current.IsPointerOverGameObject();
#else
        isOverUI = EventSystem.current.IsPointerOverGameObject(fingerId);
#endif
        
        if (enableDetailedLogs)
            Debug.Log($"[UI_BASIC_CHECK] 기본 UI 검사 결과: {isOverUI}");
        
        if (isOverUI)
        {
            if (enableDebugLogs)
                Debug.Log("[UI_BASIC_CHECK] 기본 UI 검사로 터치 감지");
            return true;
        }
        
        // 방법 2: 직접 UI Raycast (더 정확함)
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = screenPosition;
        
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);
        
        if (enableDetailedLogs)
            Debug.Log($"[UI_RAYCAST_CHECK] UI Raycast 결과 수: {raycastResults.Count}");
        
        if (raycastResults.Count > 0)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[UI_RAYCAST_CHECK] UI Raycast로 {raycastResults.Count}개 요소 감지:");
                foreach (var result in raycastResults)
                {
                    Debug.Log($"[UI_RAYCAST_RESULT] - {result.gameObject.name} (Canvas: {GetCanvasName(result.gameObject)})");
                }
            }
            return true;
        }
        
        // 방법 3: 모든 Canvas 직접 검사 (최후의 방법)
        if (CheckAllCanvases(pointerData))
        {
            if (enableDebugLogs)
                Debug.Log("[UI_CANVAS_CHECK] 전체 Canvas 검사로 UI 감지");
            return true;
        }
        
        if (enableDetailedLogs)
            Debug.Log("[NO_UI_TOUCH] UI 터치 없음 - 3D 터치 진행");
        
        return false;
    }
    
    /// <summary>
    /// 모든 Canvas에서 UI 터치 검사
    /// </summary>
    bool CheckAllCanvases(PointerEventData pointerData)
    {
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        
        if (enableDetailedLogs)
            Debug.Log($"[UI_CANVAS_CHECK] 전체 Canvas 수: {allCanvases.Length}");
        
        foreach (Canvas canvas in allCanvases)
        {
            if (!canvas.gameObject.activeInHierarchy) continue;
            
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null || !raycaster.enabled) continue;
            
            List<RaycastResult> canvasResults = new List<RaycastResult>();
            raycaster.Raycast(pointerData, canvasResults);
            
            if (enableDetailedLogs)
                Debug.Log($"[UI_CANVAS_DETAIL] Canvas '{canvas.name}' 검사 결과: {canvasResults.Count}개, SortOrder: {canvas.sortingOrder}");
            
            if (canvasResults.Count > 0)
            {
                if (enableDebugLogs)
                    Debug.Log($"[UI_CANVAS_HIT] Canvas '{canvas.name}'에서 UI 감지됨 (SortOrder: {canvas.sortingOrder})");
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// GameObject가 속한 Canvas 이름 반환
    /// </summary>
    string GetCanvasName(GameObject go)
    {
        Canvas canvas = go.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.name : "Unknown";
    }
    
    /// <summary>
    /// 3D 오브젝트 터치 처리
    /// </summary>
    void Handle3DTouch(Vector2 screenPosition)
    {
        if (Camera.main == null)
        {
            if (enableDebugLogs)
                Debug.LogError("[CAMERA_MISSING] Main Camera가 없습니다!");
            return;
        }
        
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit hit;
        
        if (enableDetailedLogs)
            Debug.Log($"[3D_RAYCAST] 3D Raycast 실행 - Origin: {ray.origin}, Direction: {ray.direction}");
        
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, touchableLayerMask))
        {
            if (enableDebugLogs)
                Debug.Log($"[3D_HIT] 3D 오브젝트 터치: {hit.collider.gameObject.name} (Layer: {hit.collider.gameObject.layer}, Distance: {hit.distance:F2})");
            
            // AR 오브젝트 상세 분석
            AnalyzeARObject(hit);
            
            // 터치된 오브젝트에 터치 이벤트 전달
            I3DTouchable touchable = hit.collider.GetComponent<I3DTouchable>();
            if (touchable != null)
            {
                if (enableDetailedLogs)
                    Debug.Log($"[TOUCHABLE_INTERFACE] I3DTouchable 인터페이스로 터치 이벤트 전달");
                touchable.OnTouch(hit);
            }
            else
            {
                // 기존 DoubleTap3D 스크립트와의 호환성
                DoubleTap3D doubleTap = hit.collider.GetComponent<DoubleTap3D>();
                if (doubleTap != null)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[DOUBLETAP3D_FOUND] DoubleTap3D 컴포넌트 발견 - 자체 터치 처리 중: {hit.collider.gameObject.name}");
                    
                    // DoubleTap3D의 터치 처리 상세 분석
                    AnalyzeDoubleTapBehavior(doubleTap, hit);
                }
                else
                {
                    if (enableDetailedLogs)
                        Debug.Log($"[NO_TOUCHABLE_COMPONENT] 터치 가능한 컴포넌트가 없음: {hit.collider.gameObject.name}");
                }
            }
        }
        else
        {
            if (enableDetailedLogs)
                Debug.Log($"[3D_MISS] 3D 터치 대상 없음 (LayerMask: {touchableLayerMask})");
        }
    }
    
    /// <summary>
    /// 특정 레이어의 터치 활성화/비활성화
    /// </summary>
    public void SetLayerTouchable(int layerIndex, bool touchable)
    {
        if (touchable)
        {
            touchableLayerMask |= (1 << layerIndex);
        }
        else
        {
            touchableLayerMask &= ~(1 << layerIndex);
        }
        
        if (enableDebugLogs)
            Debug.Log($"[LAYER_CHANGE] Layer {layerIndex} 터치 {(touchable ? "활성화" : "비활성화")} (LayerMask: {touchableLayerMask})");
    }
    
    /// <summary>
    /// 모든 3D 터치 비활성화
    /// </summary>
    public void DisableAll3DTouch()
    {
        touchableLayerMask = 0;
        if (enableDebugLogs)
            Debug.Log("[ALL_TOUCH_DISABLE] 모든 3D 터치 비활성화");
    }
    
    /// <summary>
    /// 모든 3D 터치 활성화
    /// </summary>
    public void EnableAll3DTouch()
    {
        touchableLayerMask = -1;
        if (enableDebugLogs)
            Debug.Log("[ALL_TOUCH_ENABLE] 모든 3D 터치 활성화");
    }
    
    /// <summary>
    /// 현재 UI 상태 디버깅 정보 출력
    /// </summary>
    [ContextMenu("Debug UI Status")]
    public void DebugUIStatus()
    {
        Debug.Log("=== TouchManager UI 상태 디버깅 ===");
        
        // EventSystem 상태
        if (EventSystem.current == null)
        {
            Debug.Log("❌ EventSystem: 없음");
        }
        else
        {
            Debug.Log($"✅ EventSystem: {EventSystem.current.name}, 활성화: {EventSystem.current.enabled}");
        }
        
        // Canvas 상태
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        Debug.Log($"📋 전체 Canvas 수: {canvases.Length}");
        
        foreach (Canvas canvas in canvases)
        {
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            Debug.Log($"  - {canvas.name}: 활성화={canvas.gameObject.activeInHierarchy}, SortOrder={canvas.sortingOrder}, " +
                     $"RenderMode={canvas.renderMode}, GraphicRaycaster={raycaster != null && raycaster.enabled}");
        }
        
        // TouchManager 상태
        Debug.Log($"🎯 TouchableLayerMask: {touchableLayerMask}");
        Debug.Log($"🔍 디버깅 로그: {enableDebugLogs}, 상세 로그: {enableDetailedLogs}");
        
        Debug.Log("=====================================");
    }
}

/// <summary>
/// 3D 오브젝트가 터치 이벤트를 받기 위한 인터페이스
/// </summary>
public interface I3DTouchable
{
    void OnTouch(RaycastHit hit);
}