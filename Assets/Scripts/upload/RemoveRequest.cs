using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System;

public class RemoveRequest : MonoBehaviour
{
    [SerializeField] private Button removeButton; // ���� ��û ��ư
    [SerializeField] private Button cancelButton; // ��� ��ư
    [SerializeField] private GameObject warningObj; // ��� �޽��� ǥ�ÿ� ������Ʈ
    [SerializeField] private GameObject removeRequestPanel; // ���� ��û UI �г�
    [SerializeField] private DoubleTap3D initialDoubleTap; // �ʱ� ������ DoubleTap3D (������)

    private DoubleTap3D doubleTap; // �������� ������ DoubleTap3D
    private CanvasGroup fullscreenCanvasGroup; // Ǯ��ũ�� UI �г�

    private string serverUrl => ApiConfig.FIX_UPLOAD + "/";

    void Start()
    {
        doubleTap = initialDoubleTap;

        if (doubleTap != null)
        {
            fullscreenCanvasGroup = doubleTap.GetComponent<CanvasGroup>();
            // CanvasGroup은 선택적 - 없어도 기능 동작에 문제 없음
        }

        if (removeButton != null)
            removeButton.onClick.AddListener(OnRemoveButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        if (warningObj != null)
            warningObj.SetActive(false);

        if (removeRequestPanel != null)
            removeRequestPanel.SetActive(false);

        // 더블 터치 이벤트 연결
        DoubleTap3D.OnDoubleTapEvent += HandleDoubleTap;
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