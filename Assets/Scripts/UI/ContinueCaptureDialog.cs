using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 연속 촬영 모드에서 "계속 촬영하시겠습니까?" 다이얼로그
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

    private Action onYes;
    private Action onNo;

    private void Awake()
    {
        if (yesButton != null)
            yesButton.onClick.AddListener(OnYesButtonClicked);

        if (noButton != null)
            noButton.onClick.AddListener(OnNoButtonClicked);

        // 다이얼로그 초기 상태: 비활성화
        if (dialogPanel != null)
            dialogPanel.SetActive(false);
    }

    /// <summary>
    /// 다이얼로그를 표시하고 콜백 설정
    /// </summary>
    /// <param name="message">표시할 메시지 (예: "현재 3/10장\n계속 촬영하시겠습니까?")</param>
    /// <param name="onYes">예 버튼 클릭 시 콜백</param>
    /// <param name="onNo">아니오 버튼 클릭 시 콜백</param>
    public void Show(string message, Action onYes, Action onNo)
    {
        this.onYes = onYes;
        this.onNo = onNo;

        if (titleText != null)
            titleText.text = GetLocalizedText("continue_capture_title");

        if (messageText != null)
            messageText.text = message;

        // 다국어 지원
        UpdateLocalizedTexts();

        if (dialogPanel != null)
            dialogPanel.SetActive(true);
    }

    private void UpdateLocalizedTexts()
    {
        if (yesButtonText != null)
            yesButtonText.text = GetLocalizedText("yes_continue");

        if (noButtonText != null)
            noButtonText.text = GetLocalizedText("no_finish");
    }

    private void OnYesButtonClicked()
    {
        Hide();
        onYes?.Invoke();
    }

    private void OnNoButtonClicked()
    {
        Hide();
        onNo?.Invoke();
    }

    private void Hide()
    {
        if (dialogPanel != null)
            dialogPanel.SetActive(false);

        onYes = null;
        onNo = null;
    }

    private string GetLocalizedText(string key)
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.GetText(key);

        // Fallback
        switch (key)
        {
            case "continue_capture_title": return "계속 촬영하시겠습니까?";
            case "yes_continue": return "✅ 예, 계속 촬영";
            case "no_finish": return "❌ 아니오, 종료";
            default: return key;
        }
    }

    private void OnDestroy()
    {
        if (yesButton != null)
            yesButton.onClick.RemoveListener(OnYesButtonClicked);

        if (noButton != null)
            noButton.onClick.RemoveListener(OnNoButtonClicked);
    }
}
