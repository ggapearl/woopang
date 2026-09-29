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
    [Tooltip("화살표 페이드인 시간 (초)")]
    public float arrowFadeDuration = 0.3f;

    private CanvasGroup canvasGroup;

    // 거리에 따른 목표 투명도(1=불투명). OffScreenIndicator 가 화살표일 때 매 프레임 설정한다.
    // 페이드인과 곱해져, 등장은 0→distanceAlpha 로 자연스럽게 이어진다.
    private float distanceAlpha = 1f;
    private Coroutine fadeCoroutine;
    private bool isFirstActivation = true;

    /// <summary>
    /// 마지막으로 DrawIndicators에서 업데이트된 프레임 번호
    /// 2프레임 이상 업데이트 안 되면 orphan으로 판단하여 자동 해제
    /// </summary>
    [HideInInspector] public int lastUpdatedFrame;

    // ── 닫기(X) — 박스 오른쪽 아래 끝(위쪽은 장소 이름을 가려서). 누르면 이 기기에서 그 장소를 숨긴다(HiddenPlaces: 3D·박스·화살표).
    //    씬이 켜 줄 때만 생긴다(R0926IndicatorClose). 장소가 아닌 대상(P2P 사용자 등)에는 붙지 않는다.
    public static bool CloseButtonEnabled;
    public static Sprite CloseIcon;
    public static Sprite CloseBackground;
    public static float CloseButtonSize = 96f;   // 캔버스 단위 — 박스가 거리에 따라 커지고 작아져도 이 크기 유지
    private RectTransform closeButton;
    private Target closeFor;

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
            isFirstActivation = false;
            StartFadeIn();
        }
        else if (value && !isFirstActivation && !wasActive)
        {
            // 재활성화 시에는 페이드인 없이 즉시 표시 (거리 기반 투명도 반영)
            if (canvasGroup != null)
            {
                canvasGroup.alpha = distanceAlpha;
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
        float duration = arrowFadeDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 페이드인
            canvasGroup.alpha = Mathf.Lerp(0f, distanceAlpha, t);

            yield return null;
        }

        // 최종 알파값 = 거리 기반 목표
        canvasGroup.alpha = distanceAlpha;
        fadeCoroutine = null;
    }

    private void LateUpdate()
    {
        if (indicatorType != IndicatorType.BOX) return;
        if (!CloseButtonEnabled)
        {
            if (closeButton != null && closeButton.gameObject.activeSelf) closeButton.gameObject.SetActive(false);
            return;
        }
        if (ownerTarget != closeFor)
        {
            closeFor = ownerTarget;
            bool isPlace = ownerTarget != null && HiddenPlaces.ResolveUniqueId(ownerTarget) != null;
            if (isPlace) EnsureCloseButton();
            if (closeButton != null) closeButton.gameObject.SetActive(isPlace);
        }
        if (closeButton != null && closeButton.gameObject.activeSelf)
        {
            float s = transform.localScale.x;
            if (s > 0.0001f)
            {
                closeButton.localScale = Vector3.one / s;
                // 모서리에서 살짝 오른쪽·위로 — 박스 아래 거리 글자(1778m 등)와 겹치지 않게. 박스 크기와 상관없이 같은 간격
                closeButton.anchoredPosition = new Vector2(0.3f, 0.3f) * (CloseButtonSize / s);
            }
        }
    }

    private void EnsureCloseButton()
    {
        if (closeButton != null) return;
        var go = new GameObject("Close0926", typeof(RectTransform), typeof(Image), typeof(Button));
        closeButton = (RectTransform)go.transform;
        closeButton.SetParent(transform, false);
        closeButton.anchorMin = closeButton.anchorMax = new Vector2(1f, 0f);
        closeButton.pivot = new Vector2(0.5f, 0.5f);
        closeButton.anchoredPosition = Vector2.zero;
        closeButton.sizeDelta = Vector2.one * (CloseButtonSize * 1.9f);   // 누르는 영역은 보이는 원보다 넉넉히
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        var bgRt = (RectTransform)bg.transform;
        bgRt.SetParent(closeButton, false);
        bgRt.sizeDelta = Vector2.one * CloseButtonSize;
        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = CloseBackground;
        bgImg.color = new Color(0.06f, 0.07f, 0.1f, 0.78f);
        bgImg.raycastTarget = false;

        var icon = new GameObject("X", typeof(RectTransform), typeof(Image));
        var icRt = (RectTransform)icon.transform;
        icRt.SetParent(closeButton, false);
        icRt.sizeDelta = Vector2.one * (CloseButtonSize * 0.46f);
        var icImg = icon.GetComponent<Image>();
        icImg.sprite = CloseIcon;
        icImg.color = Color.white;
        icImg.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnCloseClicked);
    }

    private void OnCloseClicked()
    {
        if (ownerTarget != null) HiddenPlaces.HideTarget(ownerTarget);
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
    /// 거리 기반 목표 투명도를 설정한다. 페이드인 중이 아니면 즉시 반영하고,
    /// 페이드인 중이면 값만 갱신해 코루틴이 이 값을 향해 진행한다(깜빡임 방지).
    /// </summary>
    public void SetDistanceAlpha(float alpha)
    {
        distanceAlpha = Mathf.Clamp01(alpha);
        if (canvasGroup != null && fadeCoroutine == null)
        {
            canvasGroup.alpha = distanceAlpha;
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
    }
}

public enum IndicatorType
{
    BOX,
    ARROW
}