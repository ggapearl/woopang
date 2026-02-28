using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 우팡이만세 스플래시 화면
/// 앱 시작 시 귀여운 마스코트 이미지를 표시한 후 페이드아웃
///
/// 사용법:
/// 1. Assets/Textures 폴더에 woopang_mascot.png (카툰 스타일 강아지) 이미지 추가
/// 2. WP_0111 씬의 "우팡이만세" 오브젝트에 이 스크립트 추가
/// 3. Inspector에서 mascotTexture에 이미지 할당
/// </summary>
public class WoopangSplash : MonoBehaviour
{
    [Header("=== 마스코트 이미지 ===")]
    [Tooltip("카툰 스타일 마스코트 이미지 (PNG)")]
    public Texture2D mascotTexture;

    [Header("=== 배경 설정 ===")]
    [Tooltip("배경 색상 (기본: 밝은 크림색)")]
    public Color backgroundColor = new Color(0.98f, 0.96f, 0.92f, 1f);

    [Tooltip("배경 그라데이션 사용")]
    public bool useGradient = true;

    [Tooltip("그라데이션 상단 색상")]
    public Color gradientTopColor = new Color(1f, 0.95f, 0.85f, 1f);

    [Tooltip("그라데이션 하단 색상")]
    public Color gradientBottomColor = new Color(0.95f, 0.9f, 0.95f, 1f);

    [Header("=== 타이밍 설정 ===")]
    [Tooltip("표시 시간 (초)")]
    public float displayDuration = 2.5f;

    [Tooltip("페이드인 시간")]
    public float fadeInDuration = 0.3f;

    [Tooltip("페이드아웃 시간")]
    public float fadeOutDuration = 0.5f;

    [Header("=== 마스코트 설정 ===")]
    [Tooltip("마스코트 화면 비율 (0.5 = 화면의 50%)")]
    [Range(0.3f, 0.8f)]
    public float mascotSizeRatio = 0.6f;

    [Tooltip("마스코트 Y 오프셋 (0 = 중앙, 양수 = 위로)")]
    public float mascotYOffset = 30f;

    [Header("=== 텍스트 설정 ===")]
    [Tooltip("하단 텍스트 표시")]
    public bool showBottomText = true;

    [Tooltip("하단 텍스트")]
    public string bottomText = "WOOPANG";

    [Tooltip("텍스트 색상")]
    public Color textColor = new Color(0.3f, 0.3f, 0.35f, 1f);

    [Header("=== 애니메이션 ===")]
    [Tooltip("마스코트 바운스 애니메이션")]
    public bool enableBounceAnimation = true;

    [Tooltip("바운스 높이")]
    public float bounceHeight = 15f;

    [Tooltip("바운스 속도")]
    public float bounceSpeed = 3f;

    // 내부 참조
    private Canvas splashCanvas;
    private CanvasGroup canvasGroup;
    private RawImage mascotImage;
    private Text bottomTextComponent;
    private RectTransform mascotRect;
    private float initialMascotY;

    void Start()
    {
        CreateSplashUI();
        StartCoroutine(PlaySplashSequence());
    }

    void Update()
    {
        // 바운스 애니메이션
        if (enableBounceAnimation && mascotRect != null && canvasGroup != null && canvasGroup.alpha > 0.5f)
        {
            float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
            mascotRect.anchoredPosition = new Vector2(0, initialMascotY + bounce);
        }
    }

    /// <summary>
    /// 스플래시 UI 동적 생성
    /// </summary>
    private void CreateSplashUI()
    {
        // 1. Canvas 생성
        GameObject canvasObj = new GameObject("WoopangSplashCanvas");
        canvasObj.transform.SetParent(transform);

        splashCanvas = canvasObj.AddComponent<Canvas>();
        splashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        splashCanvas.sortingOrder = 10000; // 최상위

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // 페이드인을 위해 0으로 시작

        // 2. 배경 생성
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();

        if (useGradient)
        {
            // 그라데이션 텍스처 생성
            bgImage.sprite = CreateGradientSprite(gradientTopColor, gradientBottomColor);
            bgImage.color = Color.white;
        }
        else
        {
            bgImage.color = backgroundColor;
        }

        // 3. 마스코트 이미지 생성
        if (mascotTexture != null)
        {
            GameObject mascotObj = new GameObject("MascotImage");
            mascotObj.transform.SetParent(canvasObj.transform, false);

            mascotRect = mascotObj.AddComponent<RectTransform>();
            mascotRect.anchorMin = new Vector2(0.5f, 0.5f);
            mascotRect.anchorMax = new Vector2(0.5f, 0.5f);
            mascotRect.pivot = new Vector2(0.5f, 0.5f);

            // 크기 계산 (화면 비율 기준)
            float screenHeight = scaler.referenceResolution.y;
            float mascotSize = screenHeight * mascotSizeRatio;

            // 비율 유지
            float aspectRatio = (float)mascotTexture.width / mascotTexture.height;
            mascotRect.sizeDelta = new Vector2(mascotSize * aspectRatio, mascotSize);
            mascotRect.anchoredPosition = new Vector2(0, mascotYOffset);
            initialMascotY = mascotYOffset;

            mascotImage = mascotObj.AddComponent<RawImage>();
            mascotImage.texture = mascotTexture;
            mascotImage.raycastTarget = false;

            // 그림자 효과 (선택적)
            Shadow shadow = mascotObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.2f);
            shadow.effectDistance = new Vector2(3, -3);
        }
        else
        {
            // 텍스처가 없으면 플레이스홀더 표시
            CreatePlaceholder(canvasObj.transform);
        }

        // 4. 하단 텍스트 생성
        if (showBottomText)
        {
            GameObject textObj = new GameObject("BottomText");
            textObj.transform.SetParent(canvasObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0);
            textRect.anchorMax = new Vector2(0.5f, 0);
            textRect.pivot = new Vector2(0.5f, 0);
            textRect.sizeDelta = new Vector2(400, 60);
            textRect.anchoredPosition = new Vector2(0, 80);

            bottomTextComponent = textObj.AddComponent<Text>();
            bottomTextComponent.text = bottomText;
            bottomTextComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bottomTextComponent.fontSize = 36;
            bottomTextComponent.fontStyle = FontStyle.Bold;
            bottomTextComponent.alignment = TextAnchor.MiddleCenter;
            bottomTextComponent.color = textColor;
            bottomTextComponent.raycastTarget = false;
        }

    }

    /// <summary>
    /// 텍스처 없을 때 플레이스홀더 표시
    /// </summary>
    private void CreatePlaceholder(Transform parent)
    {
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(parent, false);

        mascotRect = placeholderObj.AddComponent<RectTransform>();
        mascotRect.anchorMin = new Vector2(0.5f, 0.5f);
        mascotRect.anchorMax = new Vector2(0.5f, 0.5f);
        mascotRect.sizeDelta = new Vector2(300, 300);
        mascotRect.anchoredPosition = new Vector2(0, mascotYOffset);
        initialMascotY = mascotYOffset;

        Image placeholder = placeholderObj.AddComponent<Image>();
        placeholder.color = new Color(0.9f, 0.85f, 0.8f);

        // 플레이스홀더 텍스트
        GameObject textObj = new GameObject("PlaceholderText");
        textObj.transform.SetParent(placeholderObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = "마스코트\n이미지\n추가 필요";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.5f, 0.45f, 0.4f);

        Debug.LogWarning("[WoopangSplash] mascotTexture가 할당되지 않았습니다. Inspector에서 이미지를 추가해주세요.");
    }

    /// <summary>
    /// 그라데이션 스프라이트 생성
    /// </summary>
    private Sprite CreateGradientSprite(Color topColor, Color bottomColor)
    {
        int height = 256;
        int width = 1;

        Texture2D texture = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            float t = (float)y / height;
            Color color = Color.Lerp(bottomColor, topColor, t);
            texture.SetPixel(0, y, color);
        }

        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;

        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// 스플래시 시퀀스 재생
    /// </summary>
    private IEnumerator PlaySplashSequence()
    {
        // 페이드인
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // 표시 유지
        yield return new WaitForSeconds(displayDuration);

        // 페이드아웃
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);

            // 마스코트 살짝 위로 이동
            if (mascotRect != null)
            {
                float yOffset = Mathf.Lerp(0, 50f, elapsed / fadeOutDuration);
                mascotRect.anchoredPosition = new Vector2(0, initialMascotY + yOffset);
            }

            yield return null;
        }

        // 스플래시 종료
        HideSplash();
    }

    /// <summary>
    /// 스플래시 숨김 및 정리
    /// </summary>
    private void HideSplash()
    {

        if (splashCanvas != null)
        {
            Destroy(splashCanvas.gameObject);
        }

        // 스크립트만 제거하고 오브젝트는 유지 (씬에 우팡이만세 오브젝트 유지)
        Destroy(this);
    }
}
