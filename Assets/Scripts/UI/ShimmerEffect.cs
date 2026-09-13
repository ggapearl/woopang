using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스켈레톤 로딩용 쉬머(빛 흐름) 효과.
///
/// 이전 구현의 어색했던 점과 수정 내용:
///  1) Mathf.Repeat 로 끝나자마자 왼쪽으로 순간이동해 다시 시작 → 끊기는 느낌.
///     쓸고 지나간 뒤 잠깐 쉬는 구간(dwell)을 두고, 그 동안에는 숨긴다.
///  2) 모든 행이 Time.time 만 보고 움직여 15개가 완전히 같은 위상으로 행진.
///     형제 인덱스로 위상을 어긋나게 해 물결처럼 내려가게 한다.
///  3) 선형 Lerp 라 속도가 일정 → 기계적. SmoothStep 으로 가감속을 준다.
///  4) 행마다 512x1 텍스처와 Sprite 를 새로 만들고 Mask(스텐실)까지 붙어
///     15행이면 텍스처 15장 + 드로우콜 증가. 텍스처는 정적으로 공유하고
///     Mask 대신 RectMask2D(스텐실 불필요)를 쓴다.
/// </summary>
public class ShimmerEffect : MonoBehaviour
{
    [Header("Shimmer Settings")]
    [Tooltip("한 번 쓸고 지나가는 데 걸리는 시간(초)")]
    [Range(0.4f, 3f)]
    [SerializeField] private float sweepDuration = 1.15f;

    [Tooltip("쓸고 난 뒤 쉬는 시간(초). 0이면 쉼 없이 계속 흐른다")]
    [Range(0f, 2f)]
    [SerializeField] private float dwellDuration = 0.45f;

    [Tooltip("쉬머 띠 너비")]
    [Range(50f, 400f)]
    [SerializeField] private float shimmerWidth = 170f;

    [Tooltip("쉬머 밝기")]
    [Range(0f, 1f)]
    [SerializeField] private float shimmerIntensity = 0.35f;

    [Tooltip("쉬머 색상")]
    [SerializeField] private Color shimmerColor = Color.white;

    [Tooltip("쉬머 기울기(도)")]
    [Range(-45f, 45f)]
    [SerializeField] private float shimmerAngle = 14f;

    [Tooltip("행마다 어긋나는 정도(초). 0이면 모두 동시에 움직인다")]
    [Range(0f, 0.5f)]
    [SerializeField] private float perRowStagger = 0.09f;

    // 512x1 그라디언트는 모양이 항상 같다 — 행마다 만들 이유가 없어 공유한다
    private static Sprite sharedGradient;

    private RectTransform parentRect;
    private RectTransform shimmerRect;
    private Image shimmerImage;
    private float phaseOffset;

    void Awake()
    {
        parentRect = GetComponent<RectTransform>();
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        if (GetComponent<Image>() == null)
        {
            Debug.LogWarning("[ShimmerEffect] Image 컴포넌트가 없어 쉬머를 적용할 수 없습니다");
            enabled = false;
            return;
        }

        // 형제 순서만큼 위상을 밀어 물결처럼 보이게 한다
        phaseOffset = transform.GetSiblingIndex() * perRowStagger;

        var go = new GameObject("ShimmerGradient");
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling();

        shimmerRect = go.AddComponent<RectTransform>();
        shimmerRect.anchorMin = new Vector2(0f, 0f);
        shimmerRect.anchorMax = new Vector2(0f, 1f);
        shimmerRect.pivot = new Vector2(0.5f, 0.5f);
        shimmerRect.sizeDelta = new Vector2(shimmerWidth, 0f);
        shimmerRect.localRotation = Quaternion.Euler(0f, 0f, shimmerAngle);

        shimmerImage = go.AddComponent<Image>();
        shimmerImage.raycastTarget = false;
        shimmerImage.sprite = GetSharedGradient();
        shimmerImage.color = new Color(shimmerColor.r, shimmerColor.g, shimmerColor.b, shimmerIntensity);

        // Mask(스텐실 2패스) 대신 RectMask2D — 사각형 클리핑이면 이쪽이 싸다
        if (GetComponent<Mask>() == null && GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();
    }

    private static Sprite GetSharedGradient()
    {
        if (sharedGradient != null) return sharedGradient;

        const int width = 512;
        var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;

        for (int x = 0; x < width; x++)
        {
            float t = (float)x / (width - 1);
            // 가운데가 가장 밝은 부드러운 띠
            float d = Mathf.Abs(t - 0.5f) * 2f;
            float a = Mathf.SmoothStep(1f, 0f, d);
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();

        sharedGradient = Sprite.Create(tex, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f));
        sharedGradient.hideFlags = HideFlags.HideAndDontSave;
        return sharedGradient;
    }

    void Update()
    {
        if (shimmerImage == null || parentRect == null) return;

        float cycle = sweepDuration + dwellDuration;
        if (cycle <= 0f) return;

        float t = Mathf.Repeat(Time.time + phaseOffset, cycle);

        // 쉬는 구간에는 아예 숨긴다 — 예전엔 여기서 순간이동해 튀어 보였다
        if (t > sweepDuration)
        {
            if (shimmerImage.enabled) shimmerImage.enabled = false;
            return;
        }
        if (!shimmerImage.enabled) shimmerImage.enabled = true;

        float u = Mathf.SmoothStep(0f, 1f, t / sweepDuration);   // 가감속
        float w = parentRect.rect.width;
        float x = Mathf.Lerp(-shimmerWidth, w + shimmerWidth, u);

        shimmerRect.anchoredPosition = new Vector2(x, 0f);
    }

    // 공유 스프라이트는 파괴하지 않는다 — 다른 스켈레톤 행이 계속 쓴다
}
