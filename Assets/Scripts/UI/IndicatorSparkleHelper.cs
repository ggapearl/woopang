using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Offscreen Indicator (화살표만)에 반짝임 효과 추가
/// Hierarchy에 GameObject 생성 후 이 컴포넌트 추가하여 인스펙터에서 설정 조절
/// </summary>
public class IndicatorSparkleHelper : MonoBehaviour
{
    private static IndicatorSparkleHelper instance;

    [Header("Sparkle Settings")]
    [Tooltip("Sparkle 효과 활성화")]
    public bool enableSparkle = true;

    [Tooltip("circle.png 스프라이트")]
    public Sprite sparkleSprite;

    [Tooltip("Sparkle 크기 (픽셀)")]
    public Vector2 sparkleSize = new Vector2(80f, 80f);

    [Tooltip("생성 후 딜레이 (초)")]
    public float spawnDelay = 0.5f;

    [Tooltip("페이드인 시간 (초)")]
    public float fadeInDuration = 0.3f;

    [Tooltip("페이드아웃 시간 (초)")]
    public float fadeOutDuration = 1.7f;

    [Tooltip("시작 스케일 배율")]
    public float startScale = 0.5f;

    [Tooltip("최종 스케일 배율")]
    public float maxScale = 2.0f;

    [Tooltip("반짝임 색상")]
    public Color sparkleColor = new Color(1f, 1f, 1f, 0.8f);

    [Header("Filter Settings")]
    [Tooltip("화살표 인디케이터에만 적용")]
    public bool arrowOnly = true;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.LogWarning("[IndicatorSparkleHelper] 인스턴스가 이미 존재합니다. 중복 제거.");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Indicator가 활성화될 때 반짝임 효과 재생
    /// </summary>
    public static void PlaySparkleForIndicator(Vector3 screenPosition, IndicatorType type, Sprite sprite = null)
    {
        // 인스턴스가 없으면 반환
        if (instance == null)
        {
            Debug.LogWarning("[IndicatorSparkleHelper] 인스턴스가 없습니다. Hierarchy에 IndicatorSparkleHelper를 추가하세요.");
            return;
        }

        // Sparkle 비활성화 시 반환
        if (!instance.enableSparkle) return;

        // 화살표만 적용 설정 확인
        if (instance.arrowOnly && type == IndicatorType.BOX) return;

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
        sparkleRect.sizeDelta = instance.sparkleSize; // 인스펙터에서 설정한 크기

        // Canvas 좌표로 변환
        Camera mainCamera = Camera.main;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            screenPosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
            out Vector2 canvasPos
        );

        sparkleRect.anchoredPosition = canvasPos;

        // 애니메이션 시작 (인스펙터 설정값 전달)
        SparkleAnimator animator = sparkleObj.AddComponent<SparkleAnimator>();
        animator.StartAnimation(
            sparkleImage,
            sparkleRect,
            instance.spawnDelay,
            instance.fadeInDuration,
            instance.fadeOutDuration,
            instance.startScale,
            instance.maxScale,
            instance.sparkleColor
        );
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
    private float spawnDelay;
    private float fadeInDuration;
    private float fadeOutDuration;
    private float startScale;
    private float maxScale;
    private Color sparkleColor;

    public void StartAnimation(
        Image img,
        RectTransform rect,
        float delay,
        float fadeIn,
        float fadeOut,
        float scaleStart,
        float scaleMax,
        Color color)
    {
        image = img;
        rectTransform = rect;
        spawnDelay = delay;
        fadeInDuration = fadeIn;
        fadeOutDuration = fadeOut;
        startScale = scaleStart;
        maxScale = scaleMax;
        sparkleColor = color;
        StartCoroutine(AnimateSparkle());
    }

    private System.Collections.IEnumerator AnimateSparkle()
    {

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
