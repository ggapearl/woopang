using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 사진 선택 방법을 선택하는 다이얼로그
/// "카메라로 촬영" 또는 "갤러리에서 선택" 옵션 제공
/// </summary>
public class PhotoSourceDialog : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private Button cameraButton;
    [SerializeField] private Button galleryButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Text cameraButtonText;
    [SerializeField] private Text galleryButtonText;
    [SerializeField] private Text cancelButtonText;

    private Action onCameraSelected;
    private Action onGallerySelected;

    private void Awake()
    {
        if (cameraButton != null)
            cameraButton.onClick.AddListener(OnCameraButtonClicked);

        if (galleryButton != null)
            galleryButton.onClick.AddListener(OnGalleryButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        // 다이얼로그 초기 상태: 비활성화
        if (dialogPanel != null)
            dialogPanel.SetActive(false);
    }

    /// <summary>
    /// 다이얼로그를 표시하고 콜백 설정
    /// </summary>
    public void Show(string title, Action onCamera, Action onGallery)
    {
        onCameraSelected = onCamera;
        onGallerySelected = onGallery;

        if (titleText != null)
            titleText.text = title;

        // 다국어 지원
        UpdateLocalizedTexts();

        if (dialogPanel != null)
            dialogPanel.SetActive(true);
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

    private void OnCameraButtonClicked()
    {
        Hide();
        onCameraSelected?.Invoke();
    }

    private void OnGalleryButtonClicked()
    {
        Hide();
        onGallerySelected?.Invoke();
    }

    private void OnCancelButtonClicked()
    {
        Hide();
    }

    private void Hide()
    {
        if (dialogPanel != null)
            dialogPanel.SetActive(false);

        onCameraSelected = null;
        onGallerySelected = null;
    }

    private string GetLocalizedText(string key)
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.GetText(key);

        // Fallback
        switch (key)
        {
            case "camera_capture": return "📷 카메라로 촬영";
            case "gallery_select": return "🖼️ 갤러리에서 선택";
            case "cancel": return "❌ 취소";
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
    }
}
