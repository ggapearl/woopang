using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// PlaceListManager용 스켈레톤 로더
/// 텍스트 로딩 전에 둥근 사각형 플레이스홀더를 표시하고
/// 텍스트가 페이드인되면서 자연스럽게 사라짐
/// </summary>
public class PlaceListSkeletonLoader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform contentParent; // ListText의 부모 (Scroll View Content)
    [SerializeField] private Text listText; // 실제 텍스트 컴포넌트

    [Header("Skeleton Settings")]
    [Tooltip("스켈레톤 라인 높이")]
    [SerializeField] private float skeletonLineHeight = 70f;

    [Tooltip("스켈레톤 라인 간격")]
    [SerializeField] private float skeletonLineSpacing = 10f;

    [Tooltip("스켈레톤 라인 너비 (부모 대비 비율)")]
    [SerializeField] private float skeletonWidthRatio = 0.9f;

    [Tooltip("스켈레톤 표시 개수")]
    [SerializeField] private int skeletonCount = 5;

    [Tooltip("둥근 모서리 반경")]
    [SerializeField] private float cornerRadius = 10f;

    [Tooltip("스켈레톤 색상")]
    [SerializeField] private Color skeletonColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);

    [Header("Animation Settings")]
    [Tooltip("스켈레톤 표시 후 텍스트 페이드인까지 대기 시간")]
    [SerializeField] private float delayBeforeTextFadeIn = 0.2f;

    [Tooltip("텍스트 페이드인 시간")]
    [SerializeField] private float textFadeInDuration = 0.5f;

    [Tooltip("Shimmer 효과 속도")]
    [SerializeField] private float shimmerSpeed = 1.0f;

    private List<GameObject> skeletonObjects = new List<GameObject>();
    private CanvasGroup textCanvasGroup;
    private bool isLoading = false;

    void Awake()
    {
        // CanvasGroup 추가 (페이드인용)
        if (listText != null)
        {
            textCanvasGroup = listText.GetComponent<CanvasGroup>();
            if (textCanvasGroup == null)
            {
                textCanvasGroup = listText.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    /// <summary>
    /// 스켈레톤 로더 시작 (텍스트 로딩 전에 호출)
    /// </summary>
    public void ShowSkeletonLoader()
    {
        if (isLoading) return;

        isLoading = true;

        // 텍스트 숨김
        if (textCanvasGroup != null)
        {
            textCanvasGroup.alpha = 0f;
        }

        // 스켈레톤 생성
        CreateSkeletonLines();

        // Shimmer 효과 시작
        StartCoroutine(ShimmerEffect());
    }

    /// <summary>
    /// 스켈레톤 로더 종료 및 텍스트 페이드인
    /// </summary>
    public void HideSkeletonAndShowText()
    {
        if (!isLoading) return;

        StartCoroutine(FadeOutSkeletonAndFadeInText());
    }

    private void CreateSkeletonLines()
    {
        // 기존 스켈레톤 제거
        ClearSkeletons();

        if (contentParent == null) return;

        float parentWidth = contentParent.rect.width;
        float lineWidth = parentWidth * skeletonWidthRatio;
        float startY = -skeletonLineSpacing;

        for (int i = 0; i < skeletonCount; i++)
        {
            GameObject skeletonLine = CreateRoundedRectangle(lineWidth, skeletonLineHeight);
            skeletonLine.name = $"SkeletonLine_{i}";
            skeletonLine.transform.SetParent(contentParent, false);

            RectTransform rect = skeletonLine.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f); // 상단 중앙 기준
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, startY - (i * (skeletonLineHeight + skeletonLineSpacing)));

            skeletonObjects.Add(skeletonLine);
        }

        Debug.Log($"[PlaceListSkeletonLoader] {skeletonCount}개의 스켈레톤 라인 생성 완료");
    }

    private GameObject CreateRoundedRectangle(float width, float height)
    {
        GameObject obj = new GameObject("RoundedRect");

        // Image 컴포넌트 추가
        Image image = obj.AddComponent<Image>();
        image.color = skeletonColor;

        // RectTransform 설정
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);

        // 둥근 모서리를 위해 Sprite 생성 (Unity의 기본 UI Sprite 사용)
        // 실제로는 둥근 모서리 Sprite를 사용해야 하지만, 여기서는 기본 Sprite 사용
        // 실제 프로젝트에서는 둥근 모서리 Sprite Asset을 할당해야 함
        image.sprite = Resources.Load<Sprite>("UI/Skin/UISprite");
        image.type = Image.Type.Sliced;

        // CanvasGroup 추가 (페이드아웃용)
        obj.AddComponent<CanvasGroup>();

        return obj;
    }

    private void ClearSkeletons()
    {
        foreach (GameObject skeleton in skeletonObjects)
        {
            if (skeleton != null)
            {
                Destroy(skeleton);
            }
        }
        skeletonObjects.Clear();
    }

    private IEnumerator ShimmerEffect()
    {
        float shimmerPhase = 0f;

        while (isLoading && skeletonObjects.Count > 0)
        {
            shimmerPhase += Time.deltaTime * shimmerSpeed;

            // Shimmer 효과 (알파값 변동)
            float shimmerAlpha = Mathf.Lerp(0.2f, 0.4f, (Mathf.Sin(shimmerPhase) + 1f) / 2f);

            foreach (GameObject skeleton in skeletonObjects)
            {
                if (skeleton != null)
                {
                    Image image = skeleton.GetComponent<Image>();
                    if (image != null)
                    {
                        Color color = image.color;
                        color.a = shimmerAlpha;
                        image.color = color;
                    }
                }
            }

            yield return null;
        }
    }

    private IEnumerator FadeOutSkeletonAndFadeInText()
    {
        // 0.2초 대기
        yield return new WaitForSeconds(delayBeforeTextFadeIn);

        // 스켈레톤 페이드아웃과 텍스트 페이드인 동시 진행
        float elapsed = 0f;

        while (elapsed < textFadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / textFadeInDuration;

            // 스켈레톤 페이드아웃
            foreach (GameObject skeleton in skeletonObjects)
            {
                if (skeleton != null)
                {
                    CanvasGroup cg = skeleton.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = Mathf.Lerp(1f, 0f, t);
                    }
                }
            }

            // 텍스트 페이드인
            if (textCanvasGroup != null)
            {
                textCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            }

            yield return null;
        }

        // 최종 상태
        if (textCanvasGroup != null)
        {
            textCanvasGroup.alpha = 1f;
        }

        // 스켈레톤 완전히 제거
        ClearSkeletons();
        isLoading = false;

        Debug.Log("[PlaceListSkeletonLoader] 스켈레톤 페이드아웃 및 텍스트 페이드인 완료");
    }

    void OnDestroy()
    {
        ClearSkeletons();
    }
}
