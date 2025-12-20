using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DataManager에서 발생하는 오브젝트 개수를 실시간으로 표시하는 UI
/// </summary>
public class ObjectCountUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("오브젝트 개수를 표시할 Text 컴포넌트")]
    public Text countText;

    [Header("Fade Settings")]
    [Tooltip("최종 완료 후 UI 표시 유지 시간 (초)")]
    public float displayDuration = 3f;

    [Tooltip("페이드아웃 시간 (초)")]
    public float fadeOutDuration = 0.5f;

    [Header("Text Display Settings")]
    [Tooltip("텍스트 변경 시 최소 표시 시간 (초)")]
    public float minDisplayTime = 3f;

    private CanvasGroup canvasGroup;
    private int currentCount = 0;
    private bool isFinalCount = false;
    private Coroutine fadeOutCoroutine;
    private float lastUpdateTime = 0f;

    void Awake()
    {
        // CanvasGroup 추가 (페이드용)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 초기 상태: 숨김 (DataManager가 FetchDataProgressively 시작 시 활성화)
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        Debug.Log("[WoopangDebug][ObjectCountUI] Awake - 초기 상태: 비활성화");
    }

    /// <summary>
    /// 오브젝트 개수 업데이트
    /// </summary>
    /// <param name="count">현재 오브젝트 개수</param>
    /// <param name="isFinal">마지막 Tier 완료 여부</param>
    public void UpdateObjectCount(int count, bool isFinal)
    {
        // 최소 표시 시간이 지났는지 확인
        float timeSinceLastUpdate = Time.time - lastUpdateTime;

        if (timeSinceLastUpdate < minDisplayTime)
        {
            // 최소 표시 시간이 지나지 않았으면 대기 후 업데이트
            StartCoroutine(DelayedUpdate(count, isFinal, minDisplayTime - timeSinceLastUpdate));
        }
        else
        {
            // 즉시 업데이트
            ApplyUpdate(count, isFinal);
        }
    }

    private IEnumerator DelayedUpdate(int count, bool isFinal, float delay)
    {
        yield return new WaitForSeconds(delay);
        ApplyUpdate(count, isFinal);
    }

    private void ApplyUpdate(int count, bool isFinal)
    {
        currentCount = count;
        isFinalCount = isFinal;
        lastUpdateTime = Time.time;

        UpdateText(count, isFinal);

        // 최종 완료 시 페이드아웃 시작
        if (isFinal && count > 0)
        {
            // 기존 페이드아웃 중단
            if (fadeOutCoroutine != null)
            {
                StopCoroutine(fadeOutCoroutine);
            }

            // displayDuration 후 페이드아웃
            fadeOutCoroutine = StartCoroutine(FadeOutAfterDelay());
        }
    }

    private void UpdateText(int count, bool isFinal)
    {
        if (countText == null) return;

        if (count == 0)
        {
            // 0개일 때는 항상 "찾고 있습니다"
            countText.text = LocalizationManager.Instance.GetText("searching_objects");
        }
        else
        {
            // 1개 이상일 때는 "N개의 오브젝트를 찾았습니다"
            string template = LocalizationManager.Instance.GetText("found_objects");
            countText.text = string.Format(template, count);
        }
    }

    private IEnumerator FadeOutAfterDelay()
    {
        // displayDuration 동안 대기
        yield return new WaitForSeconds(displayDuration);

        // 페이드아웃
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;

            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

            yield return null;
        }

        canvasGroup.alpha = 0f;
        fadeOutCoroutine = null;

        // UI 비활성화
        gameObject.SetActive(false);
    }

    /// <summary>
    /// UI를 다시 표시 (새로운 로드 시작 시)
    /// </summary>
    public void ResetUI()
    {
        Debug.Log("[WoopangDebug][ObjectCountUI] ResetUI 호출 - 활성화 시작");

        // 페이드아웃 중단
        if (fadeOutCoroutine != null)
        {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }

        // 초기 상태로 리셋
        currentCount = 0;
        isFinalCount = false;
        UpdateText(0, false);

        // UI 활성화 및 페이드인
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        Debug.Log("[WoopangDebug][ObjectCountUI] ResetUI 완료 - 활성화됨");
    }
}
