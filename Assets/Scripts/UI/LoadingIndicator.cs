using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 데이터 로딩 중 표시 UI
/// "데이터를 불러오고 있습니다..." 메시지 표시
/// </summary>
public class LoadingIndicator : MonoBehaviour
{
    public static LoadingIndicator Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Text loadingText;
    [SerializeField] private Image loadingSpinner;

    [Header("Settings")]
    [SerializeField] private float spinSpeed = 180f;
    [SerializeField] private string loadingMessage = "데이터를 불러오고 있습니다...";
    [SerializeField] private float minDisplayTime = 0.5f; // 최소 표시 시간

    private bool isShowing = false;
    private float showStartTime;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 초기에는 숨김
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    void Update()
    {
        // 로딩 스피너 회전
        if (isShowing && loadingSpinner != null)
        {
            loadingSpinner.transform.Rotate(0f, 0f, -spinSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 로딩 표시 시작
    /// </summary>
    public void Show(string message = null)
    {
        if (loadingPanel == null) return;

        isShowing = true;
        showStartTime = Time.time;

        loadingPanel.SetActive(true);

        if (loadingText != null)
        {
            loadingText.text = message ?? loadingMessage;
        }

        Debug.Log($"[LoadingIndicator] 로딩 표시 시작: {loadingText?.text}");
    }

    /// <summary>
    /// 로딩 표시 종료 (최소 표시 시간 보장)
    /// </summary>
    public void Hide()
    {
        if (!isShowing) return;

        float elapsedTime = Time.time - showStartTime;

        if (elapsedTime < minDisplayTime)
        {
            // 최소 표시 시간 미달 시 대기
            StartCoroutine(HideAfterDelay(minDisplayTime - elapsedTime));
        }
        else
        {
            HideImmediate();
        }
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideImmediate();
    }

    private void HideImmediate()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        isShowing = false;
        Debug.Log("[LoadingIndicator] 로딩 표시 종료");
    }

    /// <summary>
    /// 진행 상황 업데이트 (선택 사항)
    /// </summary>
    public void UpdateProgress(string message)
    {
        if (loadingText != null && isShowing)
        {
            loadingText.text = message;
        }
    }
}
