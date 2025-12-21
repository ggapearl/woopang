using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// 터치 입력 디버깅 도구
/// UI가 터치를 차단하는지 확인하고 진단 정보 제공
/// </summary>
public class TouchDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float logInterval = 1f; // 로그 간격

    private float lastLogTime = 0f;

    void Update()
    {
        // 터치가 있을 때만 체크
        if (Input.touchCount > 0)
        {
            // 로그 스로틀링
            if (Time.time - lastLogTime < logInterval)
                return;

            lastLogTime = Time.time;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                // ⭐ UI 위에 터치했는지 확인
                bool isOverUI = IsPointerOverUIObject(touch.position);

                if (enableDebugLogs)
                {
                    Debug.Log($"[TouchDebugger] Touch {i}: pos={touch.position}, phase={touch.phase}, overUI={isOverUI}");
                }

                // UI 위에 터치했다면 어떤 UI인지 확인
                if (isOverUI)
                {
                    List<RaycastResult> results = new List<RaycastResult>();
                    PointerEventData eventData = new PointerEventData(EventSystem.current);
                    eventData.position = touch.position;
                    EventSystem.current.RaycastAll(eventData, results);

                    if (results.Count > 0 && enableDebugLogs)
                    {
                        Debug.LogWarning($"[TouchDebugger] ⚠️ UI가 터치 차단 중! 감지된 UI: {results[0].gameObject.name} (Canvas: {GetCanvasName(results[0].gameObject)})");

                        // 모든 차단 UI 리스트
                        for (int j = 0; j < Mathf.Min(results.Count, 3); j++)
                        {
                            Debug.Log($"[TouchDebugger]   - {j + 1}. {results[j].gameObject.name} (Raycast Target: {results[j].gameObject.GetComponent<UnityEngine.UI.Graphic>()?.raycastTarget})");
                        }
                    }
                }
            }

            // 핀치 감지 확인
            if (Input.touchCount == 2)
            {
                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);
                bool touch0OverUI = IsPointerOverUIObject(touch0.position);
                bool touch1OverUI = IsPointerOverUIObject(touch1.position);

                if (enableDebugLogs)
                {
                    if (touch0OverUI || touch1OverUI)
                    {
                        Debug.LogError($"[TouchDebugger] ❌ 핀치 제스처 차단됨! Touch0 UI차단={touch0OverUI}, Touch1 UI차단={touch1OverUI}");
                    }
                    else
                    {
                        Debug.Log($"[TouchDebugger] ✅ 핀치 제스처 정상 감지 가능!");
                    }
                }
            }
        }
    }

    /// <summary>
    /// 특정 스크린 좌표가 UI 위에 있는지 확인
    /// </summary>
    private bool IsPointerOverUIObject(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0;
    }

    /// <summary>
    /// GameObject가 속한 Canvas 이름 가져오기
    /// </summary>
    private string GetCanvasName(GameObject obj)
    {
        Canvas canvas = obj.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.gameObject.name : "Unknown";
    }

    /// <summary>
    /// 현재 씬의 모든 Canvas와 Raycast 설정 출력
    /// </summary>
    [ContextMenu("Print All Canvas Info")]
    public void PrintAllCanvasInfo()
    {
        Canvas[] allCanvas = FindObjectsOfType<Canvas>();
        Debug.Log($"[TouchDebugger] === 전체 Canvas 목록 ({allCanvas.Length}개) ===");

        foreach (Canvas canvas in allCanvas)
        {
            Debug.Log($"[TouchDebugger] Canvas: {canvas.gameObject.name}");
            Debug.Log($"  - Render Mode: {canvas.renderMode}");
            Debug.Log($"  - Sort Order: {canvas.sortingOrder}");
            Debug.Log($"  - Enabled: {canvas.enabled}");
            Debug.Log($"  - GraphicRaycaster: {canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null}");

            // Canvas 하위의 Raycast Target 체크
            var graphics = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            int raycastTargetCount = 0;
            foreach (var graphic in graphics)
            {
                if (graphic.raycastTarget)
                    raycastTargetCount++;
            }
            Debug.Log($"  - Raycast Target 활성화된 UI 요소: {raycastTargetCount}개");
        }
    }
}
