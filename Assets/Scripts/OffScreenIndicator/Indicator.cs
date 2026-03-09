using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class Indicator : MonoBehaviour
{
    [SerializeField] private IndicatorType indicatorType;
    private Image indicatorImage;
    private Text distanceText;
    private Text nameText;

    /// <summary>
    /// 이 인디케이터가 표시하는 Target 참조 (터치 콜백용)
    /// </summary>
    [HideInInspector] public Target ownerTarget;

    [Header("Fade In Settings")]
    [Tooltip("스프라이트 페이드인 시간 (초)")]
    public float spriteFadeDuration = 0.3f;

    [Tooltip("화살표 페이드인 시간 (초)")]
    public float arrowFadeDuration = 0.3f;

    [Tooltip("스프라이트 표시 후 화살표까지 딜레이 (초)")]
    public float arrowDelay = 0.2f;

    [Header("Sprite Image for Arrow")]
    [Tooltip("화살표 발생 전 보여줄 스프라이트")]
    public Sprite sprite;

    private CanvasGroup canvasGroup;
    private CanvasGroup spriteCanvasGroup;
    private Image spriteImage; // 자동 생성됨
    private Coroutine fadeCoroutine;
    private bool isFirstActivation = true;

    public bool Active
    {
        get
        {
            return transform.gameObject.activeInHierarchy;
        }
    }

    public IndicatorType Type
    {
        get
        {
            return indicatorType;
        }
    }

    void Awake()
    {
        indicatorImage = transform.GetComponent<Image>();

        // NameText / DistanceText 분리 검색 (BoxIndicator용)
        Transform nameT = transform.Find("NameText");
        Transform distT = transform.Find("DistanceText");
        if (nameT != null) nameText = nameT.GetComponent<Text>();
        if (distT != null) distanceText = distT.GetComponent<Text>();

        // fallback: 기존 "Text" 자식 (ArrowIndicator 등 호환)
        if (distanceText == null)
        {
            distanceText = transform.GetComponentInChildren<Text>();
        }

        // 터치 가능하도록 Button 추가
        Button btn = GetComponent<Button>();
        if (btn == null)
        {
            btn = gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnIndicatorClicked);

        // CanvasGroup 추가 (페이드인용 + 터치 활성화)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        // 부모 패널의 CanvasGroup(BlocksRaycasts=0)을 무시하여 개별 인디케이터 터치 가능
        canvasGroup.ignoreParentGroups = true;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        // spriteImage가 없고 sprite가 할당되어 있으면 자동으로 Image 생성
        if (spriteImage == null && sprite != null)
        {
            CreateSpriteImage();
        }

        // spriteImage용 CanvasGroup 추가
        if (spriteImage != null)
        {
            spriteCanvasGroup = spriteImage.GetComponent<CanvasGroup>();
            if (spriteCanvasGroup == null)
            {
                spriteCanvasGroup = spriteImage.gameObject.AddComponent<CanvasGroup>();
            }

            // sprite가 할당되어 있으면 spriteImage에 적용
            if (sprite != null)
            {
                spriteImage.sprite = sprite;
            }

            // 초기에는 스프라이트 숨김
            spriteImage.gameObject.SetActive(false);
            spriteCanvasGroup.alpha = 0f;
        }
    }

    private void CreateSpriteImage()
    {
        // 새 GameObject 생성
        GameObject spriteObj = new GameObject("SpriteImage");
        spriteObj.transform.SetParent(transform, false);

        // RectTransform 설정
        RectTransform rectTransform = spriteObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(100, 100); // 기본 크기

        // Image 컴포넌트 추가
        spriteImage = spriteObj.AddComponent<Image>();
        spriteImage.sprite = sprite;
        spriteImage.raycastTarget = false; // 레이캐스트 비활성화
    }

    public void SetImageColor(Color color)
    {
        indicatorImage.color = color;
    }

    public void SetDistanceText(float value, Color textColor, string placeName)
    {
        bool hasValidDistance = value >= 0;
        string distanceStr = hasValidDistance ? $"{Mathf.Floor(value)}m" : "";

        if (indicatorType == IndicatorType.BOX && nameText != null)
        {
            // 박스 인디케이터: 이름(위) + 거리(아래) 분리 표시
            nameText.text = string.IsNullOrEmpty(placeName) ? "" : $"<b>{placeName}</b>";
            nameText.color = textColor;

            if (distanceText != null)
            {
                distanceText.text = distanceStr;
                distanceText.color = textColor;
            }
        }
        else if (distanceText != null)
        {
            if (indicatorType == IndicatorType.ARROW)
            {
                // 화살표 인디케이터: 한글 2글자, 영어 1글자로 계산하여 16글자 제한
                int length = 0;
                int charIndex = 0;
                string truncatedName = placeName;

                foreach (char c in placeName)
                {
                    length += (c >= '\uAC00' && c <= '\uD7A3') ? 2 : 1;
                    charIndex++;
                    if (length > 16)
                    {
                        truncatedName = placeName.Substring(0, charIndex - 1) + "..";
                        break;
                    }
                }

                if (string.IsNullOrEmpty(placeName))
                    distanceText.text = distanceStr;
                else if (hasValidDistance)
                    distanceText.text = $"{truncatedName}\n{distanceStr}";
                else
                    distanceText.text = truncatedName;
            }
            else
            {
                // 박스 인디케이터 fallback (nameText가 없는 경우): 기존 방식
                if (string.IsNullOrEmpty(placeName))
                    distanceText.text = hasValidDistance ? $"<b>{distanceStr}</b>" : "";
                else if (hasValidDistance)
                    distanceText.text = $"<b>{placeName}\n{distanceStr}</b>";
                else
                    distanceText.text = $"<b>{placeName}</b>";
            }
            distanceText.color = textColor;
        }
    }

    public void SetTextRotation(Quaternion rotation)
    {
        if (distanceText != null)
        {
            distanceText.rectTransform.rotation = rotation;
        }
        if (nameText != null)
        {
            nameText.rectTransform.rotation = rotation;
        }
    }

    public void Activate(bool value)
    {
        // 이미 같은 상태면 아무 것도 하지 않음 (깜빡임 방지)
        bool wasActive = transform.gameObject.activeInHierarchy;

        transform.gameObject.SetActive(value);

        if (value && isFirstActivation)
        {
            // 처음 활성화될 때만 페이드인
            isFirstActivation = false;

            // 화살표 인디케이터는 스프라이트 먼저 보여주고 딜레이 후 화살표 표시
            if (indicatorType == IndicatorType.ARROW && spriteImage != null)
            {
                StartFadeInWithSprite();
            }
            else
            {
                // BOX 인디케이터는 바로 페이드인
                StartFadeIn();
            }
        }
        else if (value && !isFirstActivation && !wasActive)
        {
            // 재활성화 시에는 페이드인 없이 즉시 표시
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }
        else if (!value)
        {
            // 비활성화될 때는 다음 활성화를 위해 플래그 리셋하지 않음 (깜빡임 방지)
            // isFirstActivation = true; // 제거

            // 페이드인 중이었다면 중단
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            // 알파값은 유지 (다시 활성화될 때 즉시 표시)
            // canvasGroup.alpha = 0f; // 제거

            // 스프라이트 숨김
            if (spriteImage != null)
            {
                spriteImage.gameObject.SetActive(false);
                if (spriteCanvasGroup != null)
                {
                    spriteCanvasGroup.alpha = 0f;
                }
            }
        }
    }

    private void StartFadeIn()
    {
        if (canvasGroup == null) return;

        // 기존 페이드인 중단
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // 페이드인 시작
        fadeCoroutine = StartCoroutine(FadeInCoroutine());
    }

    private void StartFadeInWithSprite()
    {
        if (canvasGroup == null || spriteImage == null) return;

        // 기존 페이드인 중단
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // 스프라이트 먼저 보여주고 딜레이 후 화살표 표시
        fadeCoroutine = StartCoroutine(FadeInWithSpriteCoroutine());
    }

    private IEnumerator FadeInCoroutine()
    {
        // 시작 알파값 0
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        float duration = indicatorType == IndicatorType.ARROW ? arrowFadeDuration : spriteFadeDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 페이드인
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        // 최종 알파값 1
        canvasGroup.alpha = 1f;
        fadeCoroutine = null;
    }

    private IEnumerator FadeInWithSpriteCoroutine()
    {
        // 스프라이트 활성화 및 초기화
        spriteImage.gameObject.SetActive(true);
        spriteCanvasGroup.alpha = 1f; // 완전히 보이는 상태로 시작
        spriteImage.transform.localScale = Vector3.one * 0.5f; // 작게 시작

        float elapsed = 0f;
        Vector3 startScale = Vector3.one * 0.5f; // 작게 시작
        Vector3 endScale = Vector3.one * 1.5f; // 크게 확대

        // 스프라이트: 확대되면서 동시에 페이드아웃
        // 화살표: 동시에 페이드인
        canvasGroup.alpha = 0f;

        while (elapsed < spriteFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spriteFadeDuration;

            // 스프라이트: 확대 + 페이드아웃 (동시 진행)
            spriteCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            spriteImage.transform.localScale = Vector3.Lerp(startScale, endScale, t);

            // 화살표: 페이드인 (동시 진행)
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        // 최종 상태
        canvasGroup.alpha = 1f;
        spriteCanvasGroup.alpha = 0f;
        spriteImage.gameObject.SetActive(false);

        fadeCoroutine = null;
    }

    /// <summary>
    /// 인디케이터 터치/클릭 시 호출
    /// </summary>
    private void OnIndicatorClicked()
    {
        if (ownerTarget != null && ownerTarget.OnIndicatorTapped != null)
        {
            ownerTarget.OnIndicatorTapped.Invoke();
        }
    }

    public void SetScale(Vector3 scale)
    {
        transform.localScale = scale;
    }

    /// <summary>
    /// 인디케이터 알파값 직접 설정 (전환 애니메이션용)
    /// </summary>
    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }

    /// <summary>
    /// Object Pool로 반환될 때 완전히 리셋
    /// </summary>
    public void ResetForPool()
    {
        isFirstActivation = true;
        ownerTarget = null;

        // 페이드인 중이었다면 중단
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        // 알파값 리셋
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        // 스프라이트 숨김
        if (spriteImage != null)
        {
            spriteImage.gameObject.SetActive(false);
            if (spriteCanvasGroup != null)
            {
                spriteCanvasGroup.alpha = 0f;
            }
        }
    }
}

public enum IndicatorType
{
    BOX,
    ARROW
}