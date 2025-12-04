using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Offscreen Indicator (화살표만)에 반짝임 효과 추가
/// Indicator.cs에 부착하거나, OffScreenIndicator.cs에서 호출
/// </summary>
public class IndicatorSparkleHelper : MonoBehaviour
{
    [Header("Sparkle Settings")]
    [Tooltip("circle.png 스프라이트")]
    public Sprite sparkleSprite;

    [Tooltip("Canvas (자동 탐색)")]
    public Canvas targetCanvas;

    [Tooltip("생성 후 딜레이 (초)")]
    public float spawnDelay = 0.5f;

    [Tooltip("페이드인 시간 (초)")]
    public float fadeInDuration = 0.3f;

    [Tooltip("페이드아웃 시간 (초)")]
    public float fadeOutDuration = 1.7f;

    [Tooltip("최종 스케일 배율")]
    public float maxScaleMultiplier = 2.0f;

    [Tooltip("시작 스케일 배율")]
    public float startScaleMultiplier = 0.5f;

    [Tooltip("반짝임 색상")]
    public Color sparkleColor = new Color(1f, 1f, 1f, 0.8f);

    private static GameObject sparklePool;

    /// <summary>
    /// Indicator가 활성화될 때 반짝임 효과 재생 (화살표만)
    /// </summary>
    public static void PlaySparkleForIndicator(Vector3 screenPosition, IndicatorType type, Sprite sprite = null)
    {
        // BOX 인디케이터는 반짝임 없음 (화살표만)
        if (type == IndicatorType.BOX) return;

        // Canvas 찾기
        Canvas canvas = FindIndicatorCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[IndicatorSparkleHelper] Canvas를 찾을 수 없습니다.");
            return;
        }

        // Sparkle 오브젝트 생성 및 애니메이션
        GameObject sparkleObj = new GameObject("Indicator_Sparkle");
        sparkleObj.transform.SetParent(canvas.transform, false);

        Image sparkleImage = sparkleObj.AddComponent<Image>();

        // Sprite 설정
        if (sprite != null)
        {
            sparkleImage.sprite = sprite;
        }
        else
        {
            // circle.png 로드 시도
            Sprite circleSprite = Resources.Load<Sprite>("UI/circle");
            if (circleSprite == null)
            {
                circleSprite = Resources.Load<Sprite>("sou/UI/circle");
            }
            sparkleImage.sprite = circleSprite;
        }

        sparkleImage.color = new Color(1f, 1f, 1f, 0f);

        RectTransform sparkleRect = sparkleObj.GetComponent<RectTransform>();
        sparkleRect.sizeDelta = new Vector2(80f, 80f); // 인디케이터 크기에 맞게 조정

        // Canvas 좌표로 변환
        Camera mainCamera = Camera.main;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            screenPosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
            out Vector2 canvasPos
        );

        sparkleRect.anchoredPosition = canvasPos;

        // 애니메이션 시작
        SparkleAnimator animator = sparkleObj.AddComponent<SparkleAnimator>();
        animator.StartAnimation(sparkleImage, sparkleRect);
    }

    private static Canvas FindIndicatorCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            if (canvas.name.Contains("Offscreen") || canvas.name.Contains("Indicator"))
            {
                return canvas;
            }
        }

        if (canvases.Length > 0)
        {
            return canvases[0];
        }

        return null;
    }
}

/// <summary>
/// Sparkle 애니메이션 전용 컴포넌트 (자동 삭제)
/// </summary>
public class SparkleAnimator : MonoBehaviour
{
    private Image image;
    private RectTransform rectTransform;

    public void StartAnimation(Image img, RectTransform rect)
    {
        image = img;
        rectTransform = rect;
        StartCoroutine(AnimateSparkle());
    }

    private System.Collections.IEnumerator AnimateSparkle()
    {
        // 설정값
        float spawnDelay = 0.5f;
        float fadeInDuration = 0.3f;
        float fadeOutDuration = 1.7f;
        float startScale = 0.5f;
        float maxScale = 2.0f;
        Color sparkleColor = new Color(1f, 1f, 1f, 0.8f);

        // 딜레이
        yield return new WaitForSeconds(spawnDelay);

        // 페이드인 + 스케일 업
        Vector3 baseScale = Vector3.one;
        rectTransform.localScale = baseScale * startScale;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;

            // 페이드인
            Color color = sparkleColor;
            color.a = Mathf.Lerp(0f, sparkleColor.a, t);
            image.color = color;

            // 스케일 업
            float scale = Mathf.Lerp(startScale, maxScale, t);
            rectTransform.localScale = baseScale * scale;

            yield return null;
        }

        // 페이드아웃 (스케일 유지)
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;

            // 페이드아웃
            Color color = sparkleColor;
            color.a = Mathf.Lerp(sparkleColor.a, 0f, t);
            image.color = color;

            yield return null;
        }

        // 자동 삭제
        Destroy(gameObject);
    }
}
