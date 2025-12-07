using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 개선된 스플래시 이미지 플레이어 - 깜빡임 완전 제거 버전
/// Canvas 내에 Image 컴포넌트를 직접 생성하여 단순화
/// </summary>
public class SplashImagePlayer_V2 : MonoBehaviour
{
    [Header("Splash Settings")]
    [Tooltip("스플래시 이미지 스프라이트 (Texture2D 대신 Sprite 권장)")]
    [SerializeField] private Sprite splashSprite;

    [Tooltip("스플래시 이미지 텍스처 (Sprite가 없을 경우 사용)")]
    [SerializeField] private Texture2D splashTexture;

    [SerializeField] private float displayDuration = 3.0f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float dataLoadDelay = 1.0f;

    [Header("Image Fill Mode")]
    [Tooltip("Cover: 화면 꽉 채움 (원본 비율 유지, 잘림 가능) / Fit: 전체 보임 (여백 가능)")]
    [SerializeField] private ImageFillMode fillMode = ImageFillMode.Cover;

    [Header("Optional Canvas Reference")]
    [Tooltip("스플래시를 표시할 Canvas (비어있으면 자동 생성)")]
    [SerializeField] private Canvas targetCanvas;

    private GameObject splashImageObject;
    private Image splashImage;
    private CanvasGroup canvasGroup;

    public enum ImageFillMode
    {
        Cover,  // 화면 꽉 채움 (원본 비율 유지, 잘림 가능) - 추천!
        Fit     // 전체 보임 (여백 가능)
    }

    private void Awake()
    {
        Debug.Log($"[SplashV2] Awake 시작 - Time={Time.realtimeSinceStartup:F3}초");

        // Canvas 준비
        if (targetCanvas == null)
        {
            targetCanvas = CreateSplashCanvas();
        }
        else
        {
            targetCanvas.sortingOrder = 10000;
            targetCanvas.overrideSorting = true;
        }

        // Sprite 준비 (Texture2D를 Sprite로 변환)
        Sprite finalSprite = splashSprite;
        if (finalSprite == null && splashTexture != null)
        {
            finalSprite = Sprite.Create(
                splashTexture,
                new Rect(0, 0, splashTexture.width, splashTexture.height),
                new Vector2(0.5f, 0.5f)
            );
            Debug.Log($"[SplashV2] Texture를 Sprite로 변환: {splashTexture.width}x{splashTexture.height}");
        }

        if (finalSprite == null)
        {
            Debug.LogError("[SplashV2] Splash 이미지가 없습니다!");
            return;
        }

        // Image 오브젝트 생성 (즉시 최종 설정)
        CreateSplashImage(finalSprite);

        Debug.Log($"[SplashV2] Awake 완료 - FillMode={fillMode}, Time={Time.realtimeSinceStartup:F3}초");
    }

    private Canvas CreateSplashCanvas()
    {
        GameObject canvasObj = new GameObject("SplashCanvas_V2");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;
        canvas.overrideSorting = true;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        Debug.Log("[SplashV2] Canvas 생성 완료");
        return canvas;
    }

    private void CreateSplashImage(Sprite sprite)
    {
        // Image 오브젝트 생성
        splashImageObject = new GameObject("SplashImage");
        splashImageObject.transform.SetParent(targetCanvas.transform, false);

        // Image 컴포넌트 추가
        splashImage = splashImageObject.AddComponent<Image>();
        splashImage.sprite = sprite;
        splashImage.color = Color.white;

        // RectTransform 설정 (깜빡임 완전 제거)
        RectTransform rectTransform = splashImageObject.GetComponent<RectTransform>();
        SetupImageSize(rectTransform, sprite);

        // CanvasGroup 추가 (페이드아웃용)
        canvasGroup = splashImageObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        // 레이아웃 시스템 간섭 방지
        var layoutElements = splashImageObject.GetComponents<Component>();
        foreach (var comp in layoutElements)
        {
            if (comp is LayoutGroup || comp is ContentSizeFitter || comp is AspectRatioFitter)
            {
                Destroy(comp);
            }
        }

        Debug.Log($"[SplashV2] Image 생성 완료 - Size={rectTransform.sizeDelta}");
    }

    private void SetupImageSize(RectTransform rectTransform, Sprite sprite)
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        float screenAspect = screenWidth / screenHeight;

        float spriteWidth = sprite.rect.width;
        float spriteHeight = sprite.rect.height;
        float spriteAspect = spriteWidth / spriteHeight;

        Debug.Log($"[SplashV2] Screen={screenWidth}x{screenHeight} ({screenAspect:F2}), Sprite={spriteWidth}x{spriteHeight} ({spriteAspect:F2})");

        if (fillMode == ImageFillMode.Cover)
        {
            // Cover 모드: 화면 꽉 채움 (원본 비율 유지, 잘림 가능)
            Vector2 finalSize;

            if (spriteAspect > screenAspect)
            {
                // 스프라이트가 더 가로로 긴 경우 → 세로 기준으로 맞춤
                float scale = screenHeight / spriteHeight;
                finalSize = new Vector2(spriteWidth * scale, screenHeight);
            }
            else
            {
                // 스프라이트가 더 세로로 긴 경우 → 가로 기준으로 맞춤
                float scale = screenWidth / spriteWidth;
                finalSize = new Vector2(screenWidth, spriteHeight * scale);
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = finalSize;

            Debug.Log($"[SplashV2] Cover 모드 - FinalSize={finalSize}");
        }
        else
        {
            // Fit 모드: 전체 보임 (여백 가능)
            Vector2 finalSize;

            if (spriteAspect > screenAspect)
            {
                // 스프라이트가 더 가로로 긴 경우 → 가로 기준으로 맞춤
                float scale = screenWidth / spriteWidth;
                finalSize = new Vector2(screenWidth, spriteHeight * scale);
            }
            else
            {
                // 스프라이트가 더 세로로 긴 경우 → 세로 기준으로 맞춤
                float scale = screenHeight / spriteHeight;
                finalSize = new Vector2(spriteWidth * scale, screenHeight);
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = finalSize;

            Debug.Log($"[SplashV2] Fit 모드 - FinalSize={finalSize}");
        }
    }

    private void Start()
    {
        Debug.Log($"[SplashV2] Start 시작 - Time={Time.realtimeSinceStartup:F3}초");

        // 데이터 로딩 지연
        DelayDataLoading();

        // 스플래시 표시 코루틴
        if (splashImage != null)
        {
            StartCoroutine(ShowSplash());
        }
        else
        {
            HideSplash();
        }
    }

    private void DelayDataLoading()
    {
        DataManager dataManager = FindObjectOfType<DataManager>();
        if (dataManager != null)
        {
            Debug.Log($"[SplashV2] DataManager 발견 - {dataLoadDelay}초 지연 후 로딩");
            StartCoroutine(DelayedDataLoad(dataManager));
        }
    }

    private IEnumerator DelayedDataLoad(DataManager dataManager)
    {
        yield return new WaitForSeconds(dataLoadDelay);
        Debug.Log("[SplashV2] 데이터 로딩 지연 완료");
    }

    private IEnumerator ShowSplash()
    {
        Debug.Log("[SplashV2] Splash 표시 시작");
        yield return new WaitForSeconds(displayDuration);

        Debug.Log("[SplashV2] Splash 페이드아웃 시작");
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            }
            yield return null;
        }

        Debug.Log("[SplashV2] Splash 페이드아웃 완료");
        HideSplash();
    }

    private void HideSplash()
    {
        Debug.Log($"[SplashV2] Splash 숨김 - Time={Time.realtimeSinceStartup:F3}초");

        if (splashImageObject != null)
        {
            Destroy(splashImageObject);
        }

        if (targetCanvas != null && targetCanvas.gameObject.name.Contains("SplashCanvas_V2"))
        {
            Destroy(targetCanvas.gameObject);
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// 포그라운드 복귀 시 레이아웃 재조정 방지
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && splashImage != null && splashImage.sprite != null)
        {
            // 포그라운드 복귀 시 사이즈 재설정
            RectTransform rectTransform = splashImage.GetComponent<RectTransform>();
            SetupImageSize(rectTransform, splashImage.sprite);
            Debug.Log("[SplashV2] 포그라운드 복귀 - 사이즈 재설정");
        }
    }
}
