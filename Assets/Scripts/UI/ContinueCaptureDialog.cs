using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// iOS ActionSheet 스타일 연속 촬영 다이얼로그
/// 하단에서 슬라이드 업 애니메이션으로 표시
/// </summary>
public class ContinueCaptureDialog : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Text messageText;
    [SerializeField] private Text yesButtonText;
    [SerializeField] private Text noButtonText;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private AnimationCurve showCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve hideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Action onYes;
    private Action onNo;
    private RectTransform bottomContainer;
    private CanvasGroup overlayCanvasGroup;
    private Coroutine animationCoroutine;
    private bool isAnimating;

    private void Awake()
    {
        if (yesButton != null)
            yesButton.onClick.AddListener(OnYesButtonClicked);

        if (noButton != null)
            noButton.onClick.AddListener(OnNoButtonClicked);

        // 배경 클릭 시 닫기 (No와 동일)
        if (dialogPanel != null)
        {
            var bgButton = dialogPanel.GetComponent<Button>();
            if (bgButton != null)
                bgButton.onClick.AddListener(OnNoButtonClicked);
        }

        SetupAnimationComponents();

        if (dialogPanel != null)
            dialogPanel.SetActive(false);
    }

    private void SetupAnimationComponents()
    {
        if (dialogPanel == null) return;

        var bottomContainerTransform = dialogPanel.transform.Find("BottomContainer");
        if (bottomContainerTransform != null)
            bottomContainer = bottomContainerTransform as RectTransform;

        overlayCanvasGroup = dialogPanel.GetComponent<CanvasGroup>();
        if (overlayCanvasGroup == null)
            overlayCanvasGroup = dialogPanel.AddComponent<CanvasGroup>();
    }

    public void Show(string message, Action onYes, Action onNo)
    {
        if (isAnimating) return;

        this.onYes = onYes;
        this.onNo = onNo;

        if (titleText != null)
            titleText.text = GetLocalizedText("continue_capture_title");

        if (messageText != null)
            messageText.text = message;

        UpdateLocalizedTexts();

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(true);
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);
            animationCoroutine = StartCoroutine(ShowAnimation());
        }
    }

    private void UpdateLocalizedTexts()
    {
        if (yesButtonText != null)
            yesButtonText.text = GetLocalizedText("yes_continue");

        if (noButtonText != null)
            noButtonText.text = GetLocalizedText("no_finish");
    }

    private IEnumerator ShowAnimation()
    {
        isAnimating = true;
        float elapsed = 0f;

        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 0f;

        Vector2 startPos = new Vector2(0, -700f);
        Vector2 endPos = Vector2.zero;

        if (bottomContainer != null)
            bottomContainer.anchoredPosition = startPos;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = showCurve.Evaluate(elapsed / animationDuration);

            if (overlayCanvasGroup != null)
                overlayCanvasGroup.alpha = t;

            if (bottomContainer != null)
                bottomContainer.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            yield return null;
        }

        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 1f;

        if (bottomContainer != null)
            bottomContainer.anchoredPosition = endPos;

        isAnimating = false;
    }

    private IEnumerator HideAnimation(Action onComplete = null)
    {
        isAnimating = true;
        float elapsed = 0f;

        Vector2 startPos = Vector2.zero;
        Vector2 endPos = new Vector2(0, -700f);

        float startAlpha = overlayCanvasGroup != null ? overlayCanvasGroup.alpha : 1f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = hideCurve.Evaluate(elapsed / animationDuration);

            if (overlayCanvasGroup != null)
                overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

            if (bottomContainer != null)
                bottomContainer.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            yield return null;
        }

        if (dialogPanel != null)
            dialogPanel.SetActive(false);

        isAnimating = false;
        onComplete?.Invoke();
    }

    private void OnYesButtonClicked()
    {
        if (isAnimating) return;
        var callback = onYes;
        Hide(() => callback?.Invoke());
    }

    private void OnNoButtonClicked()
    {
        if (isAnimating) return;
        var callback = onNo;
        Hide(() => callback?.Invoke());
    }

    private void Hide(Action onComplete = null)
    {
        onYes = null;
        onNo = null;

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        if (dialogPanel != null && dialogPanel.activeSelf)
            animationCoroutine = StartCoroutine(HideAnimation(onComplete));
        else
            onComplete?.Invoke();
    }

    private string GetLocalizedText(string key)
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.GetText(key);

        // Fallback (영어 기본)
        switch (key)
        {
            case "continue_capture_title": return "Continue capturing?";
            case "yes_continue": return "Continue";
            case "no_finish": return "Done";
            default: return key;
        }
    }

    private void OnDestroy()
    {
        if (yesButton != null)
            yesButton.onClick.RemoveListener(OnYesButtonClicked);

        if (noButton != null)
            noButton.onClick.RemoveListener(OnNoButtonClicked);

        if (dialogPanel != null)
        {
            var bgButton = dialogPanel.GetComponent<Button>();
            if (bgButton != null)
                bgButton.onClick.RemoveListener(OnNoButtonClicked);
        }
    }
}
