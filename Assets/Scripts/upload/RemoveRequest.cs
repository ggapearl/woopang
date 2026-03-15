using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System;

public class RemoveRequest : MonoBehaviour
{
    [SerializeField] private Button removeButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private GameObject warningObj;
    [SerializeField] private GameObject removeRequestPanel;
    [SerializeField] private DoubleTap3D initialDoubleTap;

    [Header("Localized UI Texts")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text removeButtonText;
    [SerializeField] private Text cancelButtonText;

    private DoubleTap3D doubleTap;
    private CanvasGroup fullscreenCanvasGroup;

    private string serverUrl => ApiConfig.FIX_UPLOAD;

    void Start()
    {
        doubleTap = initialDoubleTap;

        if (doubleTap != null)
        {
            fullscreenCanvasGroup = doubleTap.GetComponent<CanvasGroup>();
        }

        AutoConnectFields();

        if (removeButton != null)
            removeButton.onClick.AddListener(OnRemoveButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        if (warningObj != null)
            warningObj.SetActive(false);

        LocalizeUI();

        if (removeRequestPanel != null)
            removeRequestPanel.SetActive(false);

        DoubleTap3D.OnDoubleTapEvent += HandleDoubleTap;
    }

    private void AutoConnectFields()
    {
        if (removeRequestPanel == null) return;

        if (titleText == null)
        {
            Text[] texts = removeRequestPanel.GetComponentsInChildren<Text>(true);
            foreach (Text t in texts)
            {
                if (t.transform.parent == removeRequestPanel.transform ||
                    (t.transform.parent != null && t.transform.parent.GetComponent<Button>() == null))
                {
                    titleText = t;
                    break;
                }
            }
        }

        if (removeButtonText == null && removeButton != null)
            removeButtonText = removeButton.GetComponentInChildren<Text>(true);

        if (cancelButtonText == null && cancelButton != null)
            cancelButtonText = cancelButton.GetComponentInChildren<Text>(true);
    }

    private void LocalizeUI()
    {
        if (titleText != null)
            titleText.text = GetLocalizedRemoveTitle();

        if (removeButtonText != null)
            removeButtonText.text = GetLocalizedRemoveButton();

        if (cancelButtonText != null)
            cancelButtonText.text = GetLocalizedCancelButton();
    }

    private string GetLocalizedRemoveTitle()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return "삭제 요청하시겠습니까?";
            case SystemLanguage.Japanese:
                return "削除をリクエストしますか？";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return "请求删除此地点？";
            case SystemLanguage.Spanish:
                return "¿Solicitar eliminación del lugar?";
            default:
                return "Request place removal?";
        }
    }

    private string GetLocalizedRemoveButton()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return "삭제 요청";
            case SystemLanguage.Japanese:
                return "削除リクエスト";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return "请求删除";
            case SystemLanguage.Spanish:
                return "Solicitar eliminación";
            default:
                return "Request Removal";
        }
    }

    private string GetLocalizedCancelButton()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return "취소";
            case SystemLanguage.Japanese:
                return "キャンセル";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return "取消";
            case SystemLanguage.Spanish:
                return "Cancelar";
            default:
                return "Cancel";
        }
    }

    void OnDestroy()
    {
        DoubleTap3D.OnDoubleTapEvent -= HandleDoubleTap;
    }

    private void HandleDoubleTap(DoubleTap3D tappedDoubleTap)
    {
        doubleTap = tappedDoubleTap;
    }

    private void OnRemoveButtonClicked()
    {
        if (doubleTap == null)
        {
            ShowWarning(LocalizationManager.Instance.GetText("no_object_selected"));
            return;
        }

        int id = doubleTap.GetId();
        if (id <= 0)
        {
            ShowWarning(LocalizationManager.Instance.GetText("valid_id_not_found"));
            return;
        }

        StartCoroutine(SendRemoveRequest(id));
    }

    private void OnCancelButtonClicked()
    {
        if (removeRequestPanel != null)
            removeRequestPanel.SetActive(false);

        if (fullscreenCanvasGroup != null)
            fullscreenCanvasGroup.gameObject.SetActive(false);
    }

    private IEnumerator SendRemoveRequest(int id)
    {
        WWWForm formData = new WWWForm();
        formData.AddField("target_id", id.ToString()); // locations_fix ���̺��� target_id�� ����
        formData.AddField("remove_request", "true"); // ���� ��û
        // ������ �ʵ�� �⺻������ ����
        formData.AddField("username", "");
        formData.AddField("name", "");
        formData.AddField("pet_friendly", "false");
        formData.AddField("separate_restroom", "false");
        formData.AddField("instagram_id", "");
        formData.AddField("description", "Remove request submitted via button");
        
        // �ð��� ���� �߰�
        formData.AddField("timezone", GetTimezone());
        formData.AddField("timezone_offset", GetTimezoneOffset());
        
        formData.AddField("folder", $"remove_{DateTime.Now:yyyyMMdd_HHmmss}");
        formData.AddField("main_photo", "");

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, formData))
        {
            www.timeout = 20;
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;

                if (responseText.Contains("Fix Upload Succeeded!") || www.responseCode == 200)
                {
                    ShowWarning(LocalizationManager.Instance.GetText("delete_success"));

                    if (removeRequestPanel != null)
                        removeRequestPanel.SetActive(false);

                    if (fullscreenCanvasGroup != null)
                        fullscreenCanvasGroup.gameObject.SetActive(false);
                }
                else
                {
                    ShowWarning(LocalizationManager.Instance.GetText("server_error"));
                }
            }
            else
            {
                ShowWarning(LocalizationManager.Instance.GetText("server_error"));
            }
        }
    }

    private string GetTimezone()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return "Asia/Seoul";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
                return "Asia/Shanghai";
            case SystemLanguage.Japanese:
                return "Asia/Tokyo";
            case SystemLanguage.Spanish:
                return "Europe/Madrid";
            case SystemLanguage.English:
            default:
                return "UTC";
        }
    }

    private string GetTimezoneOffset()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return "+09:00";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
                return "+08:00";
            case SystemLanguage.Japanese:
                return "+09:00";
            case SystemLanguage.Spanish:
                return "+01:00";
            case SystemLanguage.English:
            default:
                return "+00:00";
        }
    }

    private void ShowWarning(string message)
    {
        Text warningText = warningObj?.GetComponentInChildren<Text>();
        if (warningText != null)
        {
            warningText.text = message;
        }
        if (warningObj != null)
        {
            warningObj.SetActive(true);
            CancelInvoke("HideWarning");
            Invoke("HideWarning", 2f); // 2�� �� ��� �޽��� ����
        }
    }

    private void HideWarning()
    {
        if (warningObj != null)
        {
            warningObj.SetActive(false);
        }
    }
}