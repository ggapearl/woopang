using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Offscreen Indicator (화살표)에 반짝임 효과 추가
/// Hierarchy에 GameObject 생성 후 이 컴포넌트 추가하여 인스펙터에서 설정 조절
/// </summary>
public class IndicatorSparkleHelper : MonoBehaviour
{
    private static IndicatorSparkleHelper instance;

    [Header("General Settings")]
    [Tooltip("Sparkle 효과 활성화")]
    public bool enableSparkle = true;

    [Tooltip("circle.png 스프라이트")]
    public Sprite sparkleSprite;

    [Tooltip("화살표 인디케이터에만 적용 (박스 제외)")]
    public bool arrowOnly = true;

    [Header("Sparkle Size & Timing")]
    [Tooltip("Sparkle 크기 (픽셀)")]
    public Vector2 sparkleSize = new Vector2(80f, 80f);

    [Tooltip("생성 후 딜레이 (초)")]
    public float spawnDelay = 0.5f;

    [Header("Scale Animation")]
    [Tooltip("시작 스케일 배율")]
    public float startScale = 0.5f;

    [Tooltip("빠른 확대 구간 최종 스케일")]
    public float rapidExpandScale = 1.5f;

    [Tooltip("빠른 확대 시간 (초)")]
    public float rapidExpandDuration = 0.15f;

    [Tooltip("느린 확대 구간 최종 스케일")]
    public float slowExpandScale = 2.0f;

    [Tooltip("느린 확대 시간 (초)")]
    public float slowExpandDuration = 0.35f;

    [Header("Fade Animation")]
    [Tooltip("페이드인 시간 (초)")]
    public float fadeInDuration = 0.2f;

    [Tooltip("최대 불투명도 유지 시간 (초)")]
    public float fullOpacityDuration = 0.1f;

    [Tooltip("빠른 페이드아웃 시간 (초)")]
    public float rapidFadeOutDuration = 0.3f;

    [Tooltip("느린 페이드아웃 시간 (초)")]
    public float slowFadeOutDuration = 0.8f;

    [Header("Color")]
    [Tooltip("반짝임 색상")]
    public Color sparkleColor = new Color(1f, 1f, 1f, 0.9f);

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

        // Sprite 설정 (우선순위: 1. instance.sparkleSprite, 2. 매개변수 sprite, 3. Resources 로드)
        if (instance.sparkleSprite != null)
        {
            sparkleImage.sprite = instance.sparkleSprite;
        }
        else if (sprite != null)
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
        sparkleRect.sizeDelta = instance.sparkleSize;

        // Canvas 좌표로 변환
        Camera mainCamera = Camera.main;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            screenPosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
            out Vector2 canvasPos
        );

        sparkleRect.anchoredPosition = canvasPos;

        // 애니메이션 시작 (세밀한 설정값 전달)
        SparkleAnimator animator = sparkleObj.AddComponent<SparkleAnimator>();
        animator.StartAnimation(
            sparkleImage,
            sparkleRect,
            instance
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
/// 구간별 세밀한 제어 가능
/// </summary>
public class SparkleAnimator : MonoBehaviour
{
    private Image image;
    private RectTransform rectTransform;
    private IndicatorSparkleHelper settings;

    public void StartAnimation(
        Image img,
        RectTransform rect,
        IndicatorSparkleHelper helper)
    {
        image = img;
        rectTransform = rect;
        settings = helper;
        StartCoroutine(AnimateSparkle());
    }

    private System.Collections.IEnumerator AnimateSparkle()
    {
        // 딜레이
        yield return new WaitForSeconds(settings.spawnDelay);

        Vector3 baseScale = Vector3.one;
        rectTransform.localScale = baseScale * settings.startScale;

        // 1단계: 페이드인 + 빠른 확대
        float elapsed = 0f;
        float rapidPhase = Mathf.Min(settings.fadeInDuration, settings.rapidExpandDuration);

        while (elapsed < rapidPhase)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rapidPhase;

            // 페이드인
            Color color = settings.sparkleColor;
            color.a = Mathf.Lerp(0f, settings.sparkleColor.a, t);
            image.color = color;

            // 빠른 스케일 업
            float scale = Mathf.Lerp(settings.startScale, settings.rapidExpandScale, t);
            rectTransform.localScale = baseScale * scale;

            yield return null;
        }

        // 2단계: 느린 확대 (이미 페이드인 완료)
        elapsed = 0f;
        while (elapsed < settings.slowExpandDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settings.slowExpandDuration;

            // 느린 스케일 업 (ease-out)
            float easeT = 1f - Mathf.Pow(1f - t, 2f); // ease-out quadratic
            float scale = Mathf.Lerp(settings.rapidExpandScale, settings.slowExpandScale, easeT);
            rectTransform.localScale = baseScale * scale;

            yield return null;
        }

        // 3단계: 최대 불투명도 유지
        yield return new WaitForSeconds(settings.fullOpacityDuration);

        // 4단계: 빠른 페이드아웃
        elapsed = 0f;
        while (elapsed < settings.rapidFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settings.rapidFadeOutDuration;

            Color color = settings.sparkleColor;
            color.a = Mathf.Lerp(settings.sparkleColor.a, settings.sparkleColor.a * 0.3f, t);
            image.color = color;

            yield return null;
        }

        // 5단계: 느린 페이드아웃 (완전히 사라짐)
        elapsed = 0f;
        float startAlpha = settings.sparkleColor.a * 0.3f;

        while (elapsed < settings.slowFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settings.slowFadeOutDuration;

            // ease-out
            float easeT = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic

            Color color = settings.sparkleColor;
            color.a = Mathf.Lerp(startAlpha, 0f, easeT);
            image.color = color;

            yield return null;
        }

        // 자동 삭제
        Destroy(gameObject);
    }
}
