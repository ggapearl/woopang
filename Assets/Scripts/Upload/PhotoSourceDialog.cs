using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// iOS ActionSheet 스타일 사진 선택 다이얼로그
/// 하단에서 슬라이드 업 애니메이션으로 표시
/// </summary>
public class PhotoSourceDialog : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private Button cameraButton;
    [SerializeField] private Button galleryButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Text cameraButtonText;
    [SerializeField] private Text galleryButtonText;
    [SerializeField] private Text cancelButtonText;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private AnimationCurve showCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve hideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Action onCameraSelected;
    private Action onGallerySelected;
    private RectTransform bottomContainer;
    private CanvasGroup overlayCanvasGroup;
    private Coroutine animationCoroutine;
    private bool isAnimating;
    private float animStartedAt;

    private void Awake()
    {
        // 앵커 보정 — 에디터 스크립트에서 잘못 설정된 값(0.04 등) 수정
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        if (cameraButton != null)
            cameraButton.onClick.AddListener(OnCameraButtonClicked);

        if (galleryButton != null)
            galleryButton.onClick.AddListener(OnGalleryButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        // 배경 클릭 시 닫기
        if (dialogPanel != null)
        {
            var bgButton = dialogPanel.GetComponent<Button>();
            if (bgButton != null)
                bgButton.onClick.AddListener(OnCancelButtonClicked);
        }

        SetupAnimationComponents();

        if (dialogPanel != null)
            dialogPanel.SetActive(false);
    }

    private void SetupAnimationComponents()
    {
        if (dialogPanel == null) return;

        // BottomContainer 찾기
        var bottomContainerTransform = dialogPanel.transform.Find("BottomContainer");
        if (bottomContainerTransform != null)
            bottomContainer = bottomContainerTransform as RectTransform;

        // CanvasGroup 추가 (페이드 효과용)
        overlayCanvasGroup = dialogPanel.GetComponent<CanvasGroup>();
        if (overlayCanvasGroup == null)
            overlayCanvasGroup = dialogPanel.AddComponent<CanvasGroup>();
    }

    public void Show(string title, Action onCamera, Action onGallery)
    {
        if (AnimatingNow()) return;

        onCameraSelected = onCamera;
        onGallerySelected = onGallery;

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
        if (cameraButtonText != null)
            cameraButtonText.text = GetLocalizedText("camera_capture");

        if (galleryButtonText != null)
            galleryButtonText.text = GetLocalizedText("gallery_select");

        if (cancelButtonText != null)
            cancelButtonText.text = GetLocalizedText("cancel");
    }

    private IEnumerator ShowAnimation()
    {
        isAnimating = true;
        animStartedAt = Time.unscaledTime;
        float elapsed = 0f;

        // 초기 상태
        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 0f;

        Vector2 startPos = new Vector2(0, -600f);
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

        // 최종 상태
        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 1f;

        if (bottomContainer != null)
            bottomContainer.anchoredPosition = endPos;

        isAnimating = false;
    }

    private IEnumerator HideAnimation(Action onComplete = null)
    {
        isAnimating = true;
        animStartedAt = Time.unscaledTime;
        float elapsed = 0f;

        Vector2 startPos = Vector2.zero;
        Vector2 endPos = new Vector2(0, -600f);

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

    private void OnCameraButtonClicked()
    {
        if (AnimatingNow()) return;
        var callback = onCameraSelected;
        Hide(() => callback?.Invoke());
    }

    private void OnGalleryButtonClicked()
    {
        if (AnimatingNow()) return;
        var callback = onGallerySelected;
        Hide(() => callback?.Invoke());
    }

    private void OnCancelButtonClicked()
    {
        if (AnimatingNow()) return;
        Hide();
    }

    // 움직임이 중간에 끊기면(코루틴이 멈추는 등) isAnimating 이 남아 시트가 다시는 안 떴다 — 움직일 시간이 한참 지났으면 버린다 (10-21)
    private bool AnimatingNow()
    {
        if (isAnimating && Time.unscaledTime - animStartedAt > animationDuration + 1f) isAnimating = false;
        return isAnimating;
    }

    private void OnDisable()
    {
        // 꺼지면 돌던 움직임 코루틴도 같이 끝난다 — 남은 표시·콜백을 지워 다음 Show 가 막히지 않게
        isAnimating = false;
        animationCoroutine = null;
        onCameraSelected = null;
        onGallerySelected = null;
        if (dialogPanel != null && dialogPanel != gameObject) dialogPanel.SetActive(false);
    }

    private void Hide(Action onComplete = null)
    {
        onCameraSelected = null;
        onGallerySelected = null;

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
            case "camera_capture": return "Camera";
            case "gallery_select": return "Album";
            case "cancel": return "Cancel";
            default: return key;
        }
    }

    private void OnDestroy()
    {
        if (cameraButton != null)
            cameraButton.onClick.RemoveListener(OnCameraButtonClicked);

        if (galleryButton != null)
            galleryButton.onClick.RemoveListener(OnGalleryButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelButtonClicked);

        if (dialogPanel != null)
        {
            var bgButton = dialogPanel.GetComponent<Button>();
            if (bgButton != null)
                bgButton.onClick.RemoveListener(OnCancelButtonClicked);
        }
    }
}
