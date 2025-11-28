using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 화면에 줌 레벨을 표시하는 UI 인디케이터
/// 확대/축소 아이콘과 배율 텍스트로 구성
/// </summary>
public class ZoomIndicator : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text zoomText; // "1.5x", "2.0x" 등 표시
    [SerializeField] private Image zoomIcon; // 확대/축소 아이콘

    [Header("Icons")]
    [SerializeField] private Sprite zoomInIcon; // 확대 아이콘 (돋보기 +)
    [SerializeField] private Sprite zoomOutIcon; // 축소 아이콘 (돋보기 -)
    [SerializeField] private Sprite zoomResetIcon; // 기본 아이콘 (1.0x)

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    private Coroutine hideCoroutine;
    private float lastZoomLevel = 1.0f;

    void Start()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        // 초기에는 숨김
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 줌 레벨 업데이트
    /// </summary>
    /// <param name="zoomLevel">줌 배율 (1.0 = 기본, 2.0 = 2배 확대)</param>
    public void UpdateZoom(float zoomLevel)
    {
        // 줌 인디케이터 표시
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        // 페이드인 (이미 표시 중이면 취소)
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (canvasGroup.alpha < 1f)
        {
            StartCoroutine(FadeIn());
        }

        // 텍스트 업데이트
        if (zoomText != null)
        {
            zoomText.text = $"{zoomLevel:F1}x";
        }

        // 아이콘 업데이트
        if (zoomIcon != null)
        {
            if (Mathf.Approximately(zoomLevel, 1.0f))
            {
                // 기본 배율
                if (zoomResetIcon != null)
                {
                    zoomIcon.sprite = zoomResetIcon;
                }
            }
            else if (zoomLevel > lastZoomLevel)
            {
                // 확대 중
                if (zoomInIcon != null)
                {
                    zoomIcon.sprite = zoomInIcon;
                }
            }
            else if (zoomLevel < lastZoomLevel)
            {
                // 축소 중
                if (zoomOutIcon != null)
                {
                    zoomIcon.sprite = zoomOutIcon;
                }
            }
        }

        lastZoomLevel = zoomLevel;
    }

    /// <summary>
    /// 지정된 시간 후 인디케이터 숨김
    /// </summary>
    public void HideAfterDelay(float delay)
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }
        hideCoroutine = StartCoroutine(HideCoroutine(delay));
    }

    private IEnumerator FadeIn()
    {
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / fadeInDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    private IEnumerator HideCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        hideCoroutine = null;
    }

    /// <summary>
    /// 즉시 숨김
    /// </summary>
    public void Hide()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}
