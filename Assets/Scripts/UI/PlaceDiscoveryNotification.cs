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
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float displayAfterCompleteDuration = 2f;

    private CanvasGroup canvasGroup;
    private int currentCount = 0;
    private bool isShowing = false;
    private Coroutine fadeCoroutine;
    private Coroutine autoHideCoroutine;

    void Awake()
    {
        Debug.Log("[PlaceDiscovery] Awake 호출됨");

        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[PlaceDiscovery] Instance 설정 완료");
        }
        else
        {
            Debug.LogWarning("[PlaceDiscovery] 이미 Instance가 존재하여 Destroy");
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
            Debug.Log("[PlaceDiscovery] notificationPanel 초기화 완료 (비활성 상태)");
        }
        else
        {
            Debug.LogError("[PlaceDiscovery] notificationPanel이 null입니다!");
        }

        if (notificationText == null)
        {
            Debug.LogError("[PlaceDiscovery] notificationText가 null입니다!");
        }
    }

    void OnEnable()
    {
        Debug.Log("[PlaceDiscovery] OnEnable 호출됨");
    }

    void OnApplicationPause(bool pauseStatus)
    {
        // pauseStatus == true: 백그라운드로 전환
        // pauseStatus == false: 포그라운드로 전환
        if (!pauseStatus)
        {
            Debug.Log("[PlaceDiscovery] 앱이 포그라운드로 전환됨 - DataManager 재로딩 요청");
            // DataManager에게 재로딩 요청
            DataManager dataManager = FindObjectOfType<DataManager>();
            if (dataManager != null)
            {
                dataManager.ReloadDataOnForeground();
            }
        }
    }

    /// <summary>
    /// 알림 시작 (초기 표시)
    /// </summary>
    public void StartDiscovery()
    {
        Debug.Log($"[PlaceDiscovery] StartDiscovery 호출됨 - notificationPanel={notificationPanel != null}, notificationText={notificationText != null}");

        if (notificationPanel == null)
        {
            Debug.LogError("[PlaceDiscovery] notificationPanel이 null이어서 알림을 표시할 수 없습니다!");
            return;
        }

        if (notificationText == null)
        {
            Debug.LogError("[PlaceDiscovery] notificationText가 null이어서 텍스트를 표시할 수 없습니다!");
            return;
        }

        // 이미 표시 중이면 초기화하고 재시작
        if (isShowing)
        {
            Debug.LogWarning("[PlaceDiscovery] 이미 표시 중입니다. 초기화하고 재시작합니다.");
            HideImmediate();
        }

        currentCount = 0;
        isShowing = true;

        // 자동 숨김 코루틴 취소
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        // 패널 활성화 (중요!)
        notificationPanel.SetActive(true);
        Debug.Log("[PlaceDiscovery] notificationPanel 활성화됨");

        UpdateText(0);

        // 페이드 인
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeIn());

        Debug.Log("[PlaceDiscovery] StartDiscovery 완료");
    }

    /// <summary>
    /// 발견한 장소 개수 업데이트 (Tier마다 호출)
    /// </summary>
    /// <param name="newCount">새로 발견한 장소 개수</param>
    public void UpdateDiscoveredCount(int newCount)
    {
        Debug.Log($"[PlaceDiscovery] UpdateDiscoveredCount 호출 - newCount={newCount}, isShowing={isShowing}, currentCount={currentCount}");

        if (!isShowing)
        {
            Debug.LogWarning("[PlaceDiscovery] 표시 중이 아니어서 업데이트 무시");
            return;
        }

        if (notificationText == null)
        {
            Debug.LogError("[PlaceDiscovery] notificationText가 null이어서 업데이트 불가");
            return;
        }

        currentCount += newCount;
        UpdateText(currentCount);
        Debug.Log($"[PlaceDiscovery] 개수 업데이트 완료 - 총 {currentCount}개");
    }

    /// <summary>
    /// 발견 완료 (마지막 tier 또는 사용자 설정 범위까지 완료)
    /// </summary>
    public void CompleteDiscovery()
    {
        Debug.Log($"[PlaceDiscovery] CompleteDiscovery 호출 - isShowing={isShowing}, currentCount={currentCount}");

        if (!isShowing)
        {
            Debug.LogWarning("[PlaceDiscovery] 표시 중이 아니어서 완료 처리 무시");
            return;
        }

        isShowing = false;

        // 2초 후 페이드 아웃
        if (autoHideCoroutine != null) StopCoroutine(autoHideCoroutine);
        autoHideCoroutine = StartCoroutine(AutoHideAfterDelay());

        Debug.Log($"[PlaceDiscovery] {displayAfterCompleteDuration}초 후 페이드아웃 예정");
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

        currentCount = 0;
    }

    private void UpdateText(int count)
    {
        if (notificationText != null)
        {
            string message = $"근처에 {count}개의 공간을 발견하였습니다.";
            notificationText.text = message;
            Debug.Log($"[PlaceDiscovery] 텍스트 업데이트: {message}");
        }
    }

    private IEnumerator FadeIn()
    {
        Debug.Log("[PlaceDiscovery] FadeIn 시작");
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            if (canvasGroup != null) canvasGroup.alpha = alpha;
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        Debug.Log("[PlaceDiscovery] FadeIn 완료");
    }

    private IEnumerator FadeOut()
    {
        Debug.Log("[PlaceDiscovery] FadeOut 시작");
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

        currentCount = 0;
        Debug.Log("[PlaceDiscovery] FadeOut 완료 - 패널 비활성화됨");
    }

    private IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSeconds(displayAfterCompleteDuration);

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOut());
    }
}
