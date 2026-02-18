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

    [Tooltip("데이터 없음 타임아웃 시간 (초)")]
    public float noDataTimeout = 10f;

    private CanvasGroup canvasGroup;
    private int currentCount = 0;
    private bool isFinalCount = false;
    private Coroutine fadeOutCoroutine;
    private Coroutine timeoutCoroutine;
    private Coroutine delayedUpdateCoroutine;
    private float lastUpdateTime = 0f;

    // 지연 업데이트 큐: 가장 최신 값만 유지
    private int pendingCount = 0;
    private bool pendingIsFinal = false;
    private bool hasPendingUpdate = false;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 오브젝트 개수 업데이트
    /// </summary>
    /// <param name="count">현재 오브젝트 개수</param>
    /// <param name="isFinal">마지막 Tier 완료 여부</param>
    public void UpdateObjectCount(int count, bool isFinal)
    {
        if (!gameObject.activeInHierarchy) return;

        // isFinal은 항상 즉시 처리 (지연 시 noDataTimeout과 충돌 가능)
        if (isFinal)
        {
            // 진행 중인 지연 업데이트 취소
            CancelDelayedUpdate();
            ApplyUpdate(count, true);
            return;
        }

        // 개수가 0→양수로 변할 때도 즉시 처리 ("찾고 있습니다" → "N개 찾았습니다" 전환)
        if (count > 0 && currentCount == 0 && !hasPendingUpdate)
        {
            CancelDelayedUpdate();
            ApplyUpdate(count, false);
            return;
        }

        float timeSinceLastUpdate = Time.time - lastUpdateTime;

        if (timeSinceLastUpdate < minDisplayTime)
        {
            // 이전 지연 업데이트가 있으면 최신 값으로 교체 (코루틴은 하나만 유지)
            pendingCount = count;
            pendingIsFinal = isFinal;
            hasPendingUpdate = true;

            if (delayedUpdateCoroutine == null)
            {
                delayedUpdateCoroutine = StartCoroutine(DelayedUpdate(minDisplayTime - timeSinceLastUpdate));
            }
        }
        else
        {
            CancelDelayedUpdate();
            ApplyUpdate(count, isFinal);
        }
    }

    private void CancelDelayedUpdate()
    {
        if (delayedUpdateCoroutine != null)
        {
            StopCoroutine(delayedUpdateCoroutine);
            delayedUpdateCoroutine = null;
        }
        hasPendingUpdate = false;
    }

    private IEnumerator DelayedUpdate(float delay)
    {
        yield return new WaitForSeconds(delay);

        delayedUpdateCoroutine = null;

        if (hasPendingUpdate)
        {
            hasPendingUpdate = false;
            ApplyUpdate(pendingCount, pendingIsFinal);
        }
    }

    private void ApplyUpdate(int count, bool isFinal)
    {
        currentCount = count;
        isFinalCount = isFinal;
        lastUpdateTime = Time.time;

        // 데이터가 1개라도 들어오면 타임아웃 중단
        if (count > 0 && timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        UpdateText(count, isFinal);

        // 최종 완료 시 페이드아웃 시작 (데이터가 있는 경우)
        if (isFinal && count > 0)
        {
            if (fadeOutCoroutine != null)
            {
                StopCoroutine(fadeOutCoroutine);
            }
            fadeOutCoroutine = StartCoroutine(FadeOutAfterDelay());
        }
        // 최종 완료인데 데이터가 0개인 경우
        else if (isFinal && count == 0)
        {
            if (timeoutCoroutine != null) StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = StartCoroutine(HandleNoData());
        }
    }

    private void UpdateText(int count, bool isFinal)
    {
        if (countText == null) return;

        if (count == 0)
        {
            countText.text = LocalizationManager.Instance.GetText("searching_objects");
        }
        else
        {
            string template = LocalizationManager.Instance.GetText("found_objects");
            countText.text = string.Format(template, count);
        }
    }

    private IEnumerator CheckForNoDataTimeout()
    {
        yield return new WaitForSeconds(noDataTimeout);

        // 타임아웃 후에도 여전히 0개이고 최종 완료가 아니라면
        if (currentCount == 0 && !isFinalCount)
        {
            yield return StartCoroutine(HandleNoData());
        }
    }

    private IEnumerator HandleNoData()
    {
        string noDataText = "주변에 AR데이터가 없습니다";
        if (Application.systemLanguage != SystemLanguage.Korean)
        {
            noDataText = "No AR data found nearby";
        }

        if (countText != null)
        {
            countText.text = noDataText;
        }

        yield return new WaitForSeconds(3f);

        if (fadeOutCoroutine != null) StopCoroutine(fadeOutCoroutine);
        fadeOutCoroutine = StartCoroutine(FadeOutAfterDelay(0f));
    }

    private IEnumerator FadeOutAfterDelay(float delay = -1f)
    {
        float waitTime = (delay < 0) ? displayDuration : delay;

        if (waitTime > 0)
        {
            yield return new WaitForSeconds(waitTime);
        }

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
        timeoutCoroutine = null;

        gameObject.SetActive(false);
    }

    /// <summary>
    /// UI를 다시 표시 (새로운 로드 시작 시)
    /// </summary>
    public void ResetUI()
    {
        if (gameObject.activeInHierarchy)
        {
            StopAllCoroutines();
        }
        fadeOutCoroutine = null;
        timeoutCoroutine = null;
        delayedUpdateCoroutine = null;
        hasPendingUpdate = false;

        currentCount = 0;
        isFinalCount = false;
        lastUpdateTime = 0f;
        UpdateText(0, false);

        gameObject.SetActive(true);
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        if (!gameObject.activeInHierarchy) return;

        timeoutCoroutine = StartCoroutine(CheckForNoDataTimeout());
    }
}
