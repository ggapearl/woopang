using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas의 Raycast 설정을 제어하여 핀치 줌이 작동하도록 함
/// </summary>
public class CanvasRaycastController : MonoBehaviour
{
    [Header("Target Canvas")]
    [Tooltip("Raycast 차단을 해제할 Canvas (비워두면 현재 Canvas)")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Raycast Settings")]
    [Tooltip("Canvas의 GraphicRaycaster를 비활성화 (모든 터치가 AR로 전달)")]
    [SerializeField] private bool disableGraphicRaycaster = false;

    [Tooltip("특정 UI만 Raycast 비활성화 (버튼/슬라이더는 유지)")]
    [SerializeField] private bool disableBackgroundRaycast = true;

    [Header("Exclusions")]
    [Tooltip("Raycast를 유지할 UI 오브젝트 이름 목록")]
    [SerializeField] private string[] keepRaycastForNames = new string[]
    {
        "Button",
        "Slider",
        "Toggle",
        "Dropdown",
        "InputField"
    };

    void Start()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (targetCanvas == null)
        {
            Debug.LogError("[CanvasRaycastController] Canvas를 찾을 수 없습니다!");
            return;
        }

        ApplyRaycastSettings();
    }

    private void ApplyRaycastSettings()
    {
        // 1. GraphicRaycaster 비활성화 (옵션)
        if (disableGraphicRaycaster)
        {
            GraphicRaycaster raycaster = targetCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }
        }

        // 2. 개별 UI 요소의 Raycast Target 제어
        if (disableBackgroundRaycast)
        {
            var graphics = targetCanvas.GetComponentsInChildren<Graphic>(true);
            int disabledCount = 0;

            foreach (var graphic in graphics)
            {
                // 예외 목록에 있는지 확인
                bool shouldKeepRaycast = false;
                foreach (string keyword in keepRaycastForNames)
                {
                    if (graphic.gameObject.name.Contains(keyword))
                    {
                        shouldKeepRaycast = true;
                        break;
                    }
                }

                // 버튼, 슬라이더 등 상호작용 요소는 유지
                if (graphic.GetComponent<Button>() != null ||
                    graphic.GetComponent<Slider>() != null ||
                    graphic.GetComponent<Toggle>() != null ||
                    graphic.GetComponent<Dropdown>() != null ||
                    graphic.GetComponent<InputField>() != null)
                {
                    shouldKeepRaycast = true;
                }

                // Raycast Target 비활성화 (예외 제외)
                if (!shouldKeepRaycast && graphic.raycastTarget)
                {
                    graphic.raycastTarget = false;
                    disabledCount++;
                }
            }

        }
    }

    /// <summary>
    /// Canvas에 CanvasGroup을 추가하여 터치 차단 제어
    /// </summary>
    [ContextMenu("Add CanvasGroup (Block Raycasts = false)")]
    public void AddCanvasGroupNonBlocking()
    {
        if (targetCanvas == null)
            return;

        CanvasGroup canvasGroup = targetCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = targetCanvas.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.blocksRaycasts = false; // ⭐ 핵심: 터치 차단 해제
        canvasGroup.interactable = true;

    }
}
