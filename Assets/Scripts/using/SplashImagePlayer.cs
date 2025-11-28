using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// WP_1119 씬에서 이미지 스플래시를 표시한 후 페이드아웃
/// VideoPlayer 대신 Image를 사용하는 간단한 버전
/// </summary>
public class SplashImagePlayer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RawImage splashRawImage; // 기존 SplashVideoRawImage 사용
    [SerializeField] private CanvasGroup canvasGroup; // 페이드아웃용

    [Header("Splash Settings")]
    [SerializeField] private Texture2D splashTexture; // 스플래시 이미지
    [SerializeField] private float displayDuration = 3.0f; // 표시 시간 (3초)
    [SerializeField] private float fadeDuration = 0.5f; // 페이드아웃 시간
    [SerializeField] private float dataLoadDelay = 1.0f; // 데이터 로딩 지연 시간

    [Header("Hide UI During Splash")]
    [SerializeField] private bool hideOtherUI = true; // 스플래시 중 다른 UI 숨김

    private List<Canvas> hiddenCanvases = new List<Canvas>();
    private Canvas splashCanvas;

    private void Awake()
    {
        // Splash Canvas의 Sort Order를 최상위로 설정하여 모든 UI보다 앞에 표시
        if (splashRawImage != null)
        {
            splashCanvas = splashRawImage.GetComponentInParent<Canvas>();
            if (splashCanvas != null)
            {
                // Sort Order를 매우 높은 값으로 설정 (10000)
                splashCanvas.sortingOrder = 10000;
                // Canvas가 가장 앞에 렌더링되도록 설정
                splashCanvas.overrideSorting = true;
                Debug.Log($"[SplashImagePlayer] Splash Canvas Sort Order를 10000으로 설정 (overrideSorting=true)");
            }
        }

        // 다른 UI는 Awake에서 미리 숨김
        if (hideOtherUI)
        {
            Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
            foreach (Canvas canvas in allCanvases)
            {
                if (canvas != splashCanvas && canvas.gameObject.activeInHierarchy)
                {
                    canvas.enabled = false;
                    hiddenCanvases.Add(canvas);
                }
            }
            Debug.Log($"[SplashImagePlayer] Awake에서 {hiddenCanvases.Count}개의 Canvas를 숨겼습니다.");
        }

        // 텍스처와 AspectRatioFitter 설정 (한 번만 실행)
        if (splashRawImage != null && splashTexture != null)
        {
            splashRawImage.texture = splashTexture;

            // AspectRatioFitter 추가 - 전체 화면을 빈공간 없이 채움
            AspectRatioFitter fitter = splashRawImage.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = splashRawImage.gameObject.AddComponent<AspectRatioFitter>();
            }

            // EnvelopeParent: 전체 화면을 채우되 비율 유지 (빈공간 없음)
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            float aspectRatio = (float)splashTexture.width / (float)splashTexture.height;
            fitter.aspectRatio = aspectRatio;

            Debug.Log($"[SplashImagePlayer] AspectRatioFitter 설정 완료 - Mode: EnvelopeParent, Ratio: {aspectRatio:F2}");

            // Canvas Group 설정
            if (canvasGroup == null)
            {
                canvasGroup = splashRawImage.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = splashRawImage.gameObject.AddComponent<CanvasGroup>();
                }
            }
            canvasGroup.alpha = 1f;

            Debug.Log($"[SplashImagePlayer] Awake에서 이미지 설정 완료 - 전체 화면 채움, 비율 유지");
        }
    }

    private void Start()
    {
        // 데이터 로딩 1초 지연 시작
        DelayDataLoading();

        if (splashRawImage != null && splashTexture != null)
        {
            StartCoroutine(ShowSplash());
        }
        else
        {
            Debug.LogWarning("[SplashImagePlayer] RawImage 또는 Texture가 할당되지 않았습니다. 스플래시를 건너뜁니다.");
            HideSplash();
        }
    }

    private void ShowOtherUIElements()
    {
        // 숨겼던 Canvas들 다시 표시
        foreach (Canvas canvas in hiddenCanvases)
        {
            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }

        hiddenCanvases.Clear();
        Debug.Log("[SplashImagePlayer] Canvas를 다시 표시했습니다.");
    }

    private void DelayDataLoading()
    {
        // DataManager 찾아서 로딩 지연 적용
        DataManager dataManager = FindObjectOfType<DataManager>();
        if (dataManager != null)
        {
            StartCoroutine(DelayedDataLoad(dataManager));
        }
    }

    private IEnumerator DelayedDataLoad(DataManager dataManager)
    {
        // 데이터 로딩 1초 지연
        yield return new WaitForSeconds(dataLoadDelay);

        // DataManager의 초기 로딩 트리거 (필요시 public 메서드 호출)
        Debug.Log("[SplashImagePlayer] 데이터 로딩 시작");
    }

    private IEnumerator ShowSplash()
    {
        // 지정된 시간만큼 표시
        yield return new WaitForSeconds(displayDuration);

        // 페이드아웃
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

        // 완전히 숨김
        HideSplash();
    }

    private void HideSplash()
    {
        // 숨겼던 UI 다시 표시
        if (hideOtherUI)
        {
            ShowOtherUIElements();
        }

        if (splashRawImage != null)
        {
            splashRawImage.gameObject.SetActive(false);
        }

        // 스플래시 컨트롤러 자체를 삭제
        Destroy(gameObject);
    }
}
