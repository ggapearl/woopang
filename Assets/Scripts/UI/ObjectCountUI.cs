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
    private Coroutine timeoutCoroutine; // 타임아웃 코루틴 추가
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
            // 기존 페이드아웃 중단
            if (fadeOutCoroutine != null)
            {
                StopCoroutine(fadeOutCoroutine);
            }

            // displayDuration 후 페이드아웃
            fadeOutCoroutine = StartCoroutine(FadeOutAfterDelay());
        }
        // 최종 완료인데 데이터가 0개인 경우 (타임아웃 전에 로딩이 끝남)
        else if (isFinal && count == 0)
        {
             // 즉시 "데이터 없음" 처리 (타임아웃 코루틴이 돌고 있다면 중복 방지 위해 정지 후 즉시 실행)
             if (timeoutCoroutine != null) StopCoroutine(timeoutCoroutine);
             timeoutCoroutine = StartCoroutine(HandleNoData());
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

    private IEnumerator CheckForNoDataTimeout()
    {
        yield return new WaitForSeconds(noDataTimeout);

        // 타임아웃 후에도 여전히 0개라면
        if (currentCount == 0)
        {
            yield return StartCoroutine(HandleNoData());
        }
    }

    private IEnumerator HandleNoData()
    {
        // 텍스트 변경: "주변에 AR데이터가 없습니다"
        // LocalizationManager에 키가 없다면 기본 텍스트 사용, 있다면 사용
        // 여기서는 직접 할당하거나 키를 추가해야 함. 일단 하드코딩 + Localization 시도
        
        string noDataText = "주변에 AR데이터가 없습니다";
        // 만약 LocalizationManager에 해당 키가 있다면 사용 (예시)
        // noDataText = LocalizationManager.Instance.GetText("no_ar_data_found"); 
        
        // 다국어 지원을 위해 간단히 분기 (LocalizationManager에 키 추가 권장)
        if (Application.systemLanguage != SystemLanguage.Korean)
        {
            noDataText = "No AR data found nearby";
        }

        if (countText != null)
        {
            countText.text = noDataText;
        }

        Debug.Log("[WoopangDebug][ObjectCountUI] 데이터 없음 - 타임아웃 메시지 표시");

        // 3초 대기 (기존 displayDuration과 동일하게 처리하거나 별도 대기)
        yield return new WaitForSeconds(3f);

        // 페이드아웃 시작
        if (fadeOutCoroutine != null) StopCoroutine(fadeOutCoroutine);
        fadeOutCoroutine = StartCoroutine(FadeOutAfterDelay(0f)); // 0초 딜레이로 즉시 페이드아웃 시작
    }

    private IEnumerator FadeOutAfterDelay(float delay = -1f)
    {
        // delay가 -1이면 기본 displayDuration 사용
        float waitTime = (delay < 0) ? displayDuration : delay;
        
        if (waitTime > 0)
        {
            yield return new WaitForSeconds(waitTime);
        }

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
        timeoutCoroutine = null; // 타임아웃 코루틴 참조 해제

        // UI 비활성화
        gameObject.SetActive(false);
    }

    /// <summary>
    /// UI를 다시 표시 (새로운 로드 시작 시)
    /// </summary>
    public void ResetUI()
    {
        Debug.Log("[WoopangDebug][ObjectCountUI] ResetUI 호출 - 활성화 시작");

        // 진행 중인 코루틴 모두 중단
        StopAllCoroutines();
        fadeOutCoroutine = null;
        timeoutCoroutine = null;

        // 초기 상태로 리셋
        currentCount = 0;
        isFinalCount = false;
        UpdateText(0, false);

        // UI 활성화 및 페이드인
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        // 타임아웃 체크 시작
        timeoutCoroutine = StartCoroutine(CheckForNoDataTimeout());

        Debug.Log("[WoopangDebug][ObjectCountUI] ResetUI 완료 - 활성화됨");
    }
}
