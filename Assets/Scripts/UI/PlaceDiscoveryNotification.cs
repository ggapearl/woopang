using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// AR 환경에서 발견한 장소 개수를 실시간으로 표시하는 알림
/// "근처에 N개의 공간을 발견하였습니다."
/// </summary>
public class PlaceDiscoveryNotification : MonoBehaviour
{
    public static PlaceDiscoveryNotification Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private Text notificationText;

    [Header("Settings")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 1.0f;
    [SerializeField] private float displayAfterCompleteDuration = 3.0f;

    private CanvasGroup canvasGroup;
    private int currentCount = 0;
    private bool isShowing = false;
    private bool isSearchingMode = false; // 검색 모드 플래그 (메시지 고정용)
    private Coroutine fadeCoroutine;
    private Coroutine autoHideCoroutine;

    void Awake()
    {
        Debug.Log("[WoopangDebug] [PlaceDiscovery] Awake 호출됨");

        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[WoopangDebug] [PlaceDiscovery] Instance 설정 완료");
        }
        else
        {
            Debug.LogWarning("[WoopangDebug] [PlaceDiscovery] 이미 Instance가 존재하여 Destroy");
            Destroy(gameObject);
            return;
        }

        // CanvasGroup 추가 (페이드 애니메이션용)
        if (notificationPanel != null)
        {
            canvasGroup = notificationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = notificationPanel.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 0f;

            // 패널은 비활성 상태로 시작하지만 자동으로 활성화됨
            notificationPanel.SetActive(false);
            Debug.Log("[WoopangDebug] [PlaceDiscovery] notificationPanel 초기화 완료 (비활성 상태)");
        }
        else
        {
            Debug.LogError("[WoopangDebug] [PlaceDiscovery] notificationPanel이 null입니다!");
        }

        if (notificationText == null)
        {
            Debug.LogError("[WoopangDebug] [PlaceDiscovery] notificationText가 null입니다!");
        }
    }

    void OnEnable()
    {
        Debug.Log("[WoopangDebug] [PlaceDiscovery] OnEnable 호출됨");
    }

    void OnApplicationPause(bool pauseStatus)
    {
        // pauseStatus == true: 백그라운드로 전환
        // pauseStatus == false: 포그라운드로 전환
        if (!pauseStatus)
        {
            Debug.Log("[WoopangDebug] [PlaceDiscovery] 앱이 포그라운드로 전환됨 - DataManager 재로딩 요청");
            // DataManager에게 재로딩 요청
            DataManager dataManager = FindObjectOfType<DataManager>();
            if (dataManager != null)
            {
                dataManager.ReloadDataOnForeground();
            }
        }
    }

    /// <summary>
    /// 알림 시작 (초기화 및 "찾는 중" 메시지 표시 후 자동 페이드아웃)
    /// </summary>
    public void StartDiscovery()
    {
        Debug.Log($"[WoopangDebug] StartDiscovery 호출 - notificationPanel={notificationPanel != null}, notificationText={notificationText != null}");

        if (notificationPanel == null || notificationText == null)
        {
            Debug.LogError("[WoopangDebug] 필수 UI 컴포넌트 누락!");
            return;
        }

        // 이미 표시 중이어도 초기화
        // if (isShowing) { HideImmediate(); }

        currentCount = 0;
        isShowing = true;
        isSearchingMode = true; // 검색 모드 시작 (결과 표시 유예)

        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        notificationPanel.SetActive(true);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        Debug.Log("[WoopangDebug] notificationPanel 활성화 (검색 중 메시지)");

        UpdateText(0); // "찾고 있습니다" 표시

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(ShowSearchingMessageSequence());
    }

    private IEnumerator ShowSearchingMessageSequence()
    {
        // 1. 페이드 인 (이미 떠있으면 무시됨)
        yield return StartCoroutine(FadeIn());
        
        // 2. 2.5초간 무조건 대기 (이 동안은 데이터가 들어와도 텍스트 안 바뀜)
        Debug.Log("[WoopangDebug] 검색 메시지 2.5초 고정 시작");
        yield return new WaitForSeconds(2.5f);
        
        isSearchingMode = false; // 검색 모드 해제
        
        // 3. 대기 끝난 후 결과 확인
        if (currentCount > 0)
        {
            Debug.Log($"[WoopangDebug] 2.5초 대기 종료 - 누적된 {currentCount}개 결과 표시");
            UpdateText(currentCount); // "N개 발견했습니다"로 변경
        }
        else
        {
            Debug.Log("[WoopangDebug] 2.5초 대기 종료 - 데이터 없음, 페이드아웃");
            yield return StartCoroutine(FadeOut());
        }
    }

    /// <summary>
    /// 발견된 장소 개수 업데이트 (Tier마다 호출)
    /// </summary>
    /// <param name="newCount">새로 발견된 장소 수</param>
    public void UpdateDiscoveredCount(int newCount)
    {
        Debug.Log($"[WoopangDebug] UpdateDiscoveredCount 호출 - newCount={newCount}, isShowing={isShowing}, currentCount={currentCount}");

        if (!isShowing) isShowing = true;

        if (notificationText == null) return;

        if (!notificationPanel.activeSelf || (canvasGroup != null && canvasGroup.alpha < 0.1f))
        {
            notificationPanel.SetActive(true);
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeIn());
        }

        currentCount += newCount;
        
        // 검색 모드(2.5초 대기) 중일 때는 텍스트 업데이트 안 함 (카운트만 누적)
        if (!isSearchingMode)
        {
            UpdateText(currentCount);
        }
        
        Debug.Log($"[WoopangDebug] 값 업데이트 완료 - 총 {currentCount}개 (화면 표시: {!isSearchingMode})");
    }

    /// <summary>
    /// 발견 완료 (마지막 tier 또는 사용자 설정 범위까지 완료)
    /// </summary>
    public void CompleteDiscovery()
    {
        Debug.Log($"[WoopangDebug] CompleteDiscovery 호출 - isShowing={isShowing}, currentCount={currentCount}");

        if (!isShowing)
        {
            return;
        }

        isShowing = false;

        // 3초 후 페이드 아웃
        if (autoHideCoroutine != null) StopCoroutine(autoHideCoroutine);
        autoHideCoroutine = StartCoroutine(AutoHideAfterDelay());

        Debug.Log($"[WoopangDebug] {displayAfterCompleteDuration}초 후 페이드아웃 예정");
    }

    /// <summary>
    /// 즉시 숨김 (강제 종료 시)
    /// </summary>
    public void HideImmediate()
    {
        isShowing = false;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        if (autoHideCoroutine != null) StopCoroutine(autoHideCoroutine);

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (notificationPanel != null) notificationPanel.SetActive(false);

        Debug.Log("[WoopangDebug] HideImmediate 실행됨");
    }

    private void UpdateText(int count)
    {
        if (notificationText != null)
        {
            string message;
            if (count == 0)
            {
                message = "근처에 있는 AR정보를 찾고 있습니다.";
            }
            else
            {
                message = $"근처에서 {count}개의 장소를 발견하였습니다.";
            }
            
            notificationText.text = message;
            Debug.Log($"[WoopangDebug] 텍스트 업데이트: {message}");
        }
    }

    private IEnumerator FadeIn()
    {
        Debug.Log("[WoopangDebug] FadeIn 시작");
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            if (canvasGroup != null) canvasGroup.alpha = alpha;
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        Debug.Log("[WoopangDebug] FadeIn 완료");
    }

    private IEnumerator FadeOut()
    {
        Debug.Log("[WoopangDebug] FadeOut 시작");
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            if (canvasGroup != null) canvasGroup.alpha = alpha;
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (notificationPanel != null) notificationPanel.SetActive(false);

        // currentCount = 0; // 데이터 유지
        Debug.Log("[WoopangDebug] FadeOut 완료 - 패널 비활성화됨");
    }

    private IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSeconds(displayAfterCompleteDuration);

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOut());
    }
}
