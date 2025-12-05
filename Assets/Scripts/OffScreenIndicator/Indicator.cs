using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class Indicator : MonoBehaviour
{
    [SerializeField] private IndicatorType indicatorType;
    private Image indicatorImage;
    private Text distanceText;

    [Header("Fade In Settings")]
    [Tooltip("페이드인 시간 (초)")]
    public float fadeInDuration = 0.5f;

    private CanvasGroup canvasGroup;
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
        distanceText = transform.GetComponentInChildren<Text>();

        // CanvasGroup 추가 (페이드인용)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void SetImageColor(Color color)
    {
        indicatorImage.color = color;
    }

    public void SetDistanceText(float value, Color textColor, string placeName)
    {
        if (distanceText != null)
        {
            if (indicatorType == IndicatorType.ARROW)
            {
                // 화살표 인디케이터: 한글 2글자, 영어 1글자로 계산하여 16글자 제한
                int length = 0;
                int charIndex = 0;
                string truncatedName = placeName;

                // 글자 수 계산
                foreach (char c in placeName)
                {
                    length += (c >= '\uAC00' && c <= '\uD7A3') ? 2 : 1; // 한글은 2, 영어는 1
                    charIndex++;
                    if (length > 16)
                    {
                        // 16글자 초과 시 잘라내고 ".." 추가
                        truncatedName = placeName.Substring(0, charIndex - 1) + "..";
                        break;
                    }
                }

                // 텍스트 설정: 두 줄 (이름 + 거리), 사이즈 동일, 굵기 효과 없음
                distanceText.text = string.IsNullOrEmpty(placeName) ? 
                    (value >= 0 ? $"{Mathf.Floor(value)}m" : "") : 
                    $"{truncatedName}\n{Mathf.Floor(value)}m";
            }
            else
            {
                // 박스 인디케이터: 글자 수 제한 없이 전체 이름 표시, 모두 굵게
                distanceText.text = string.IsNullOrEmpty(placeName) ? 
                    (value >= 0 ? $"<b>{Mathf.Floor(value)}m</b>" : "") : 
                    $"<b>{placeName}\n{Mathf.Floor(value)}m</b>";
            }
            distanceText.color = textColor; // 서버에서 가져온 색상 적용
        }
    }

    public void SetTextRotation(Quaternion rotation)
    {
        if (distanceText != null)
        {
            distanceText.rectTransform.rotation = rotation;
        }
    }

    public void Activate(bool value)
    {
        transform.gameObject.SetActive(value);

        if (value && isFirstActivation)
        {
            // 처음 활성화될 때만 페이드인
            isFirstActivation = false;
            StartFadeIn();
        }
        else if (!value)
        {
            // 비활성화될 때는 다음 활성화를 위해 플래그 리셋
            isFirstActivation = true;

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

    private IEnumerator FadeInCoroutine()
    {
        // 시작 알파값 0
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;

            // 페이드인
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        // 최종 알파값 1
        canvasGroup.alpha = 1f;
        fadeCoroutine = null;
    }

    public void SetScale(Vector3 scale)
    {
        transform.localScale = scale;
    }
}

public enum IndicatorType
{
    BOX,
    ARROW
}