using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 요소에 좌우로 흐르는 쉬머 효과를 적용 (Mask 기반)
/// </summary>
public class ShimmerEffect : MonoBehaviour
{
    [Header("Shimmer Settings")]
    [Tooltip("쉬머 효과 속도")]
    [Range(0.1f, 3f)]
    [SerializeField] private float shimmerSpeed = 0.5f;

    [Tooltip("쉬머 효과 너비")]
    [Range(50f, 400f)]
    [SerializeField] private float shimmerWidth = 150f;

    [Tooltip("쉬머 효과 강도 (밝기)")]
    [Range(0f, 2f)]
    [SerializeField] private float shimmerIntensity = 0.8f;

    [Tooltip("쉬머 색상")]
    [SerializeField] private Color shimmerColor = new Color(1f, 1f, 1f, 1f);

    [Tooltip("쉬머 각도 (도)")]
    [Range(-45f, 45f)]
    [SerializeField] private float shimmerAngle = 15f;

    [Tooltip("그라디언트 부드러움 (높을수록 부드러움)")]
    [Range(1f, 5f)]
    [SerializeField] private float gradientSmoothness = 2f;

    private Image shimmerImage;
    private RectTransform shimmerRect;
    private RectTransform parentRect;
    private Material shimmerMaterial;

    void Awake()
    {
        parentRect = GetComponent<RectTransform>();
        CreateShimmerOverlay();
    }

    void CreateShimmerOverlay()
    {
        // 부모의 Image 컴포넌트 확인
        Image parentImage = GetComponent<Image>();
        if (parentImage == null)
        {
            Debug.LogWarning("[ShimmerEffect] 부모 오브젝트에 Image 컴포넌트가 없습니다!");
            return;
        }

        // 실제 쉬머 이미지 생성 (부모 직속 자식)
        GameObject shimmerChildObj = new GameObject("ShimmerGradient");
        shimmerChildObj.transform.SetParent(transform, false);
        shimmerChildObj.transform.SetAsLastSibling(); // 맨 위에 표시

        RectTransform childRect = shimmerChildObj.AddComponent<RectTransform>();
        childRect.anchorMin = new Vector2(0, 0);
        childRect.anchorMax = new Vector2(0, 1);
        childRect.pivot = new Vector2(0.5f, 0.5f);
        childRect.sizeDelta = new Vector2(shimmerWidth, 0);
        childRect.localRotation = Quaternion.Euler(0, 0, shimmerAngle);

        shimmerImage = shimmerChildObj.AddComponent<Image>();
        shimmerImage.raycastTarget = false;

        // 부모와 동일한 sprite 사용 (Mask 효과)
        shimmerImage.type = Image.Type.Filled;
        shimmerImage.fillMethod = Image.FillMethod.Horizontal;
        shimmerImage.fillOrigin = 0;
        shimmerImage.fillAmount = 1f;

        // 그라디언트 텍스처 생성
        CreateGradientTexture();

        // Mask 컴포넌트를 부모에 추가
        Mask mask = GetComponent<Mask>();
        if (mask == null)
        {
            mask = gameObject.AddComponent<Mask>();
        }
        mask.showMaskGraphic = true; // 부모 그래픽도 보이도록
    }

    void CreateGradientTexture()
    {
        // 부드러운 그라디언트 텍스처 생성
        int width = 512;
        int height = 1;
        Texture2D gradientTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int x = 0; x < width; x++)
        {
            float t = (float)x / width;

            // 더 부드러운 곡선 (가우시안 비슷한 곡선)
            float centerDist = Mathf.Abs(t - 0.5f) * 2f; // 0~1
            float alpha = Mathf.Pow(1f - centerDist, gradientSmoothness) * shimmerIntensity;

            Color color = shimmerColor;
            color.a = Mathf.Clamp01(alpha);
            gradientTexture.SetPixel(x, 0, color);
        }

        gradientTexture.Apply();
        gradientTexture.wrapMode = TextureWrapMode.Clamp;
        gradientTexture.filterMode = FilterMode.Bilinear;

        // Sprite 생성 및 적용
        Sprite gradientSprite = Sprite.Create(
            gradientTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f)
        );

        shimmerImage.sprite = gradientSprite;
    }

    void Update()
    {
        ApplyShimmer();
    }

    void ApplyShimmer()
    {
        if (shimmerImage == null || parentRect == null) return;

        RectTransform childRect = shimmerImage.GetComponent<RectTransform>();
        if (childRect == null) return;

        // 시간에 따라 좌우로 이동
        float time = Time.time * shimmerSpeed;
        float parentWidth = parentRect.rect.width;

        // 완전히 왼쪽 밖에서 오른쪽 밖으로 이동
        float totalDistance = parentWidth + shimmerWidth * 2;
        float normalizedTime = Mathf.Repeat(time, 1f);
        float xPos = Mathf.Lerp(-shimmerWidth, parentWidth + shimmerWidth, normalizedTime);

        childRect.anchoredPosition = new Vector2(xPos, 0);
    }

    void OnDestroy()
    {
        // 텍스처 정리
        if (shimmerImage != null && shimmerImage.sprite != null)
        {
            Texture2D texture = shimmerImage.sprite.texture;
            Destroy(shimmerImage.sprite);
            if (texture != null)
            {
                Destroy(texture);
            }
        }

        if (shimmerMaterial != null)
        {
            Destroy(shimmerMaterial);
        }
    }
}
