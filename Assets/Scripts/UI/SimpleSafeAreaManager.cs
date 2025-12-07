using UnityEngine;
using System.Collections;

/// <summary>
/// 깜빡임 없는 Safe Area 적용 - SystemUIManager 대체
/// 스플래시 완료 후 딱 한 번만 실행
/// </summary>
public class SimpleSafeAreaManager : MonoBehaviour
{
    [Header("Safe Area 설정")]
    [Tooltip("Safe Area를 적용할 Canvas (비워두면 모든 Canvas 대상)")]
    [SerializeField] private Canvas[] targetCanvases;

    [Tooltip("스플래시 완료 대기 시간 (초)")]
    [SerializeField] private float waitForSplash = 4.0f;

    [Tooltip("적용 전 추가 대기 (안전 마진)")]
    [SerializeField] private float safetyMargin = 0.5f;

    private Rect lastSafeArea;
    private bool isApplied = false;

    void Start()
    {
        StartCoroutine(WaitAndApplySafeArea());
    }

    private IEnumerator WaitAndApplySafeArea()
    {
        // 스플래시 플레이어가 완전히 사라질 때까지 대기
        float elapsed = 0f;
        float maxWait = 10f;

        while (elapsed < maxWait)
        {
            var splashPlayer = FindObjectOfType<SplashImagePlayer>();
            var splashPlayerV2 = FindObjectOfType<SplashImagePlayer_V2>();

            // 스플래시가 모두 사라졌으면
            if (splashPlayer == null && splashPlayerV2 == null)
            {
                break;
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        // 추가 안전 마진
        yield return new WaitForSeconds(safetyMargin);

        // Safe Area 적용 (단 한 번)
        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (isApplied) return;

        Rect safeArea = Screen.safeArea;
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        // 정규화
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        Canvas[] canvases = targetCanvases;

        // targetCanvases가 비어있으면 모든 Canvas 찾기
        if (canvases == null || canvases.Length == 0)
        {
            canvases = FindObjectsOfType<Canvas>();
        }

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null) continue;

            // 스플래시 Canvas는 건너뛰기
            if (canvas.name.Contains("Splash") || canvas.sortingOrder >= 10000)
            {
                continue;
            }

            // ScreenSpaceOverlay만 적용
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    canvasRect.anchorMin = anchorMin;
                    canvasRect.anchorMax = anchorMax;
                    canvasRect.offsetMin = Vector2.zero;
                    canvasRect.offsetMax = Vector2.zero;
                }
            }
        }

        lastSafeArea = safeArea;
        isApplied = true;

        Debug.Log($"[SimpleSafeArea] Safe Area 적용 완료: {safeArea}");
    }

    /// <summary>
    /// 화면 회전 등으로 Safe Area가 변경되었을 때 재적용
    /// </summary>
    void Update()
    {
        if (!isApplied) return;

        Rect currentSafeArea = Screen.safeArea;

        // Safe Area가 변경되었을 때만 재적용
        if (currentSafeArea != lastSafeArea)
        {
            ApplySafeArea();
        }
    }

    /// <summary>
    /// 포그라운드 복귀 시 재적용
    /// </summary>
    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && isApplied)
        {
            // 포그라운드 복귀 후 잠시 대기 후 재적용
            StartCoroutine(ReapplyAfterResume());
        }
    }

    private IEnumerator ReapplyAfterResume()
    {
        yield return new WaitForSeconds(0.2f);
        ApplySafeArea();
    }
}
