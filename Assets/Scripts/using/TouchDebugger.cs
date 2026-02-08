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
#pragma warning disable CS0414 // Inspector 설정용 필드
    [SerializeField] private bool enableDebugLogs = false;
#pragma warning restore CS0414
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

                // UI 위에 터치했다면 어떤 UI인지 확인
                if (isOverUI)
                {
                    List<RaycastResult> results = new List<RaycastResult>();
                    PointerEventData eventData = new PointerEventData(EventSystem.current);
                    eventData.position = touch.position;
                    EventSystem.current.RaycastAll(eventData, results);
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
        // ContextMenu debug utility - kept for editor inspection
    }
}
