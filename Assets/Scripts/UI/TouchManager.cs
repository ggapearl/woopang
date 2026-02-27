using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class TouchManager : MonoBehaviour
{
    [Header("터치 설정")]
    public LayerMask touchableLayerMask = -1; // 터치 가능한 레이어들

    private static TouchManager instance;
    public static TouchManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TouchManager>();
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
        // 전체화면 UI가 열려있으면 3D 터치 차단
        if (IsFullscreenUIOpen()) return;

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
        }
#else
        // 디바이스에서는 터치
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            touchDetected = true;
            Touch touch = Input.GetTouch(0);
            touchPosition = touch.position;
            touchFingerId = touch.fingerId;
        }
#endif

        if (!touchDetected) return;

        // UI 터치 검사
        if (IsUITouch(touchPosition, touchFingerId))
        {
            return;
        }

        // 3D 오브젝트 터치 처리
        Handle3DTouch(touchPosition);
    }

    /// <summary>
    /// UI 터치인지 확인
    /// </summary>
    bool IsUITouch(Vector2 screenPosition, int fingerId = -1)
    {
        // EventSystem 존재 확인
        if (EventSystem.current == null)
        {
            return false;
        }

        if (!EventSystem.current.enabled)
        {
            return false;
        }

        // 방법 1: 기본 UI 터치 검사
        bool isOverUI = false;
#if UNITY_EDITOR
        isOverUI = EventSystem.current.IsPointerOverGameObject();
#else
        isOverUI = EventSystem.current.IsPointerOverGameObject(fingerId);
#endif

        if (isOverUI)
        {
            return true;
        }

        // 방법 2: 직접 UI Raycast (더 정확함)
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = screenPosition;

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        if (raycastResults.Count > 0)
        {
            return true;
        }

        // 방법 3: 모든 Canvas 직접 검사 (최후의 방법)
        if (CheckAllCanvases(pointerData))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 모든 Canvas에서 UI 터치 검사
    /// </summary>
    bool CheckAllCanvases(PointerEventData pointerData)
    {
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        foreach (Canvas canvas in allCanvases)
        {
            if (!canvas.gameObject.activeInHierarchy) continue;

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null || !raycaster.enabled) continue;

            List<RaycastResult> canvasResults = new List<RaycastResult>();
            raycaster.Raycast(pointerData, canvasResults);

            if (canvasResults.Count > 0)
            {
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
            Debug.LogError("[TouchManager] Main Camera가 없습니다!");
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, touchableLayerMask))
        {
            // 터치된 오브젝트에 터치 이벤트 전달
            I3DTouchable touchable = hit.collider.GetComponent<I3DTouchable>();
            if (touchable != null)
            {
                touchable.OnTouch(hit);
            }
            else
            {
                // 기존 DoubleTap3D 스크립트와의 호환성
                // DoubleTap3D는 자체적으로 터치를 처리함
            }
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
    }

    /// <summary>
    /// 모든 3D 터치 비활성화
    /// </summary>
    public void DisableAll3DTouch()
    {
        touchableLayerMask = 0;
    }

    /// <summary>
    /// 모든 3D 터치 활성화
    /// </summary>
    public void EnableAll3DTouch()
    {
        touchableLayerMask = -1;
    }

    /// <summary>
    /// 전체화면 UI 패널이 열려있는지 체크 (AR 터치 차단용)
    /// 모든 AR 터치 스크립트에서 이 메서드로 통일하여 체크
    /// </summary>
    public static bool IsFullscreenUIOpen()
    {
        // MessagePanelManager 패널 확인
        if (MessagePanelManager.Instance != null)
        {
            var mgr = MessagePanelManager.Instance;
            if (mgr.messagePanel != null && mgr.messagePanel.activeSelf) return true;
            if (mgr.chatRoomPanel != null && mgr.chatRoomPanel.activeSelf) return true;
        }

        // CommentManager 패널 확인
        if (CommentManager.Instance != null && CommentManager.Instance.IsPanelOpen) return true;

        // FollowManager 패널 확인
        if (FollowManager.Instance != null)
        {
            if (FollowManager.Instance.panel != null && FollowManager.Instance.panel.activeSelf) return true;
        }

        // ProfileManager 패널 확인
        if (ProfileManager.Instance != null)
        {
            if (ProfileManager.Instance.fullProfilePanel != null && ProfileManager.Instance.fullProfilePanel.activeSelf) return true;
            if (ProfileManager.Instance.miniProfilePanel != null && ProfileManager.Instance.miniProfilePanel.activeSelf) return true;
        }

        return false;
    }
}

/// <summary>
/// 3D 오브젝트가 터치 이벤트를 받기 위한 인터페이스
/// </summary>
public interface I3DTouchable
{
    void OnTouch(RaycastHit hit);
}
