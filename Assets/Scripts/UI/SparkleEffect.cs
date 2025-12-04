using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오브젝트/인디케이터 생성 시 circle.png를 사용한 반짝임 효과
/// </summary>
public class SparkleEffect : MonoBehaviour
{
    [Header("Sparkle Settings")]
    [Tooltip("반짝임 이미지 (circle.png)")]
    public Sprite sparkleSprite;

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
    public Color sparkleColor = new Color(1f, 1f, 1f, 1f);

    [Tooltip("Canvas를 자동으로 찾을지 여부 (3D 오브젝트용)")]
    public bool autoFindCanvas = true;

    [Tooltip("특정 Canvas 할당 (UI 인디케이터용)")]
    public Canvas targetCanvas;

    private Canvas effectCanvas;
    private GameObject sparkleObject;
    private Image sparkleImage;
    private RectTransform sparkleRect;
    private bool isPlaying = false;

    void Start()
    {
        // Canvas 찾기
        if (autoFindCanvas && targetCanvas == null)
        {
            // OffScreenIndicator Canvas 찾기
            effectCanvas = FindCanvasForEffect();
            if (effectCanvas == null)
            {
                Debug.LogWarning("[SparkleEffect] Canvas를 찾을 수 없습니다. Sparkle 효과가 표시되지 않습니다.");
            }
        }
        else if (targetCanvas != null)
        {
            effectCanvas = targetCanvas;
        }
    }

    /// <summary>
    /// 반짝임 효과 재생 (3D 오브젝트용 - 월드 좌표)
    /// </summary>
    public void PlaySparkle3D()
    {
        if (isPlaying) return;
        if (effectCanvas == null) return;

        StartCoroutine(SparkleAnimation3D());
    }

    /// <summary>
    /// 반짝임 효과 재생 (UI 인디케이터용 - 스크린 좌표)
    /// </summary>
    public void PlaySparkleUI(Vector3 screenPosition)
    {
        if (isPlaying) return;
        if (effectCanvas == null) return;

        StartCoroutine(SparkleAnimationUI(screenPosition));
    }

    /// <summary>
    /// 3D 오브젝트용 반짝임 애니메이션
    /// </summary>
    private IEnumerator SparkleAnimation3D()
    {
        isPlaying = true;

        // 딜레이
        yield return new WaitForSeconds(spawnDelay);

        // Sparkle 오브젝트 생성
        CreateSparkleObject();

        // 3D 오브젝트의 월드 좌표를 스크린 좌표로 변환
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[SparkleEffect] 메인 카메라를 찾을 수 없습니다.");
            Cleanup();
            yield break;
        }

        Vector3 screenPos = mainCamera.WorldToScreenPoint(transform.position);

        // Canvas 좌표로 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            effectCanvas.GetComponent<RectTransform>(),
            screenPos,
            effectCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
            out Vector2 canvasPos
        );

        sparkleRect.anchoredPosition = canvasPos;

        // 시작 스케일 설정
        Vector3 baseScale = transform.localScale;
        sparkleRect.localScale = baseScale * startScaleMultiplier;

        // 페이드인 + 스케일 업
        float elapsed = 0f;
        Color startColor = sparkleColor;
        startColor.a = 0f;
        sparkleImage.color = startColor;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;

            // 페이드인
            Color color = sparkleColor;
            color.a = Mathf.Lerp(0f, sparkleColor.a, t);
            sparkleImage.color = color;

            // 스케일 업
            float scale = Mathf.Lerp(startScaleMultiplier, maxScaleMultiplier, t);
            sparkleRect.localScale = baseScale * scale;

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
            sparkleImage.color = color;

            yield return null;
        }

        // 정리
        Cleanup();
    }

    /// <summary>
    /// UI 인디케이터용 반짝임 애니메이션
    /// </summary>
    private IEnumerator SparkleAnimationUI(Vector3 screenPosition)
    {
        isPlaying = true;

        // 딜레이
        yield return new WaitForSeconds(spawnDelay);

        // Sparkle 오브젝트 생성
        CreateSparkleObject();

        // Canvas 좌표로 변환
        Camera mainCamera = Camera.main;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            effectCanvas.GetComponent<RectTransform>(),
            screenPosition,
            effectCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
            out Vector2 canvasPos
        );

        sparkleRect.anchoredPosition = canvasPos;

        // 시작 스케일 설정 (인디케이터 크기 기준)
        Vector3 baseScale = Vector3.one; // UI는 기본 1
        sparkleRect.localScale = baseScale * startScaleMultiplier;

        // 페이드인 + 스케일 업
        float elapsed = 0f;
        Color startColor = sparkleColor;
        startColor.a = 0f;
        sparkleImage.color = startColor;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;

            // 페이드인
            Color color = sparkleColor;
            color.a = Mathf.Lerp(0f, sparkleColor.a, t);
            sparkleImage.color = color;

            // 스케일 업
            float scale = Mathf.Lerp(startScaleMultiplier, maxScaleMultiplier, t);
            sparkleRect.localScale = baseScale * scale;

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
            sparkleImage.color = color;

            yield return null;
        }

        // 정리
        Cleanup();
    }

    private void CreateSparkleObject()
    {
        // Sparkle GameObject 생성
        sparkleObject = new GameObject("Sparkle_Effect");
        sparkleObject.transform.SetParent(effectCanvas.transform, false);

        // Image 컴포넌트 추가
        sparkleImage = sparkleObject.AddComponent<Image>();
        sparkleImage.sprite = sparkleSprite;
        sparkleImage.color = sparkleColor;

        // RectTransform 설정
        sparkleRect = sparkleObject.GetComponent<RectTransform>();
        sparkleRect.sizeDelta = new Vector2(100f, 100f); // 기본 크기 100x100
    }

    private void Cleanup()
    {
        if (sparkleObject != null)
        {
            Destroy(sparkleObject);
        }

        isPlaying = false;
    }

    private Canvas FindCanvasForEffect()
    {
        // OffScreenIndicator Canvas 찾기
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            if (canvas.name.Contains("Offscreen") || canvas.name.Contains("Indicator"))
            {
                Debug.Log($"[SparkleEffect] Canvas 찾음: {canvas.name}");
                return canvas;
            }
        }

        // 없으면 첫 번째 Canvas 사용
        if (canvases.Length > 0)
        {
            Debug.Log($"[SparkleEffect] 기본 Canvas 사용: {canvases[0].name}");
            return canvases[0];
        }

        return null;
    }

    void OnDestroy()
    {
        Cleanup();
    }
}
