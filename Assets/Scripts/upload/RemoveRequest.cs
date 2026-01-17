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
        doubleTap = initialDoubleTap; // �ʱⰪ ����

        if (doubleTap != null)
        {
            fullscreenCanvasGroup = doubleTap.GetComponent<CanvasGroup>(); // Ǯ��ũ�� UI �г� ����
            if (fullscreenCanvasGroup == null)
            {
                Debug.LogError("[RemoveRequest] DoubleTap3D���� CanvasGroup�� ã�� �� �����ϴ�!");
            }
            Debug.Log($"[RemoveRequest] �ʱ� DoubleTap3D ����� - ID: {doubleTap.GetId()}, GameObject: {(doubleTap != null ? doubleTap.gameObject.name : "null")}");
        }
        else
        {
            Debug.LogWarning("[RemoveRequest] �ʱ� DoubleTap3D�� ������� �ʾҽ��ϴ�. ���� ��ġ�� �������� �����˴ϴ�.");
        }

        if (removeButton != null)
        {
            removeButton.onClick.AddListener(OnRemoveButtonClicked);
        }
        else
        {
            Debug.LogError("[RemoveRequest] RemoveButton�� �Ҵ���� �ʾҽ��ϴ�!");
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelButtonClicked);
        }
        else
        {
            Debug.LogError("[RemoveRequest] CancelButton�� �Ҵ���� �ʾҽ��ϴ�!");
        }

        if (warningObj != null)
        {
            warningObj.SetActive(false); // �ʱ� ��Ȱ��ȭ
        }
        else
        {
            Debug.LogError("[RemoveRequest] WarningObj�� �Ҵ���� �ʾҽ��ϴ�!");
        }

        if (removeRequestPanel != null)
        {
            removeRequestPanel.SetActive(false); // �ʱ� ��Ȱ��ȭ
        }
        else
        {
            Debug.LogError("[RemoveRequest] RemoveRequestPanel�� �Ҵ���� �ʾҽ��ϴ�!");
        }

        // ���� ��ġ �̺�Ʈ ����
        DoubleTap3D.OnDoubleTapEvent += HandleDoubleTap;
        Debug.Log("[RemoveRequest] DoubleTap3D.OnDoubleTapEvent ���� �Ϸ�");
    }

    void OnDestroy()
    {
        // �̺�Ʈ ���� ����
        DoubleTap3D.OnDoubleTapEvent -= HandleDoubleTap;
        Debug.Log("[RemoveRequest] DoubleTap3D.OnDoubleTapEvent ���� ����");
    }

    private void HandleDoubleTap(DoubleTap3D tappedDoubleTap)
    {
        // ���� ��ġ�� DoubleTap3D �ν��Ͻ��� ���� ������Ʈ
        doubleTap = tappedDoubleTap;
        Debug.Log($"[RemoveRequest] ���� ��ġ�� DoubleTap3D ������Ʈ - ID: {(doubleTap != null ? doubleTap.GetId() : -1)}, GameObject: {(doubleTap != null ? doubleTap.gameObject.name : "null")}");
    }

    private void OnRemoveButtonClicked()
    {
        if (doubleTap == null)
        {
            ShowWarning(LocalizationManager.Instance.GetText("no_object_selected"));
            Debug.LogError("[RemoveRequest] OnRemoveButtonClicked: DoubleTap3D�� null�Դϴ�!");
            return;
        }

        int id = doubleTap.GetId();
        Debug.Log($"[RemoveRequest] OnRemoveButtonClicked - ID: {id}");
        if (id <= 0)
        {
            ShowWarning(LocalizationManager.Instance.GetText("valid_id_not_found"));
            Debug.LogError($"[RemoveRequest] ��ȿ���� ���� ID: {id}");
            return;
        }

        StartCoroutine(SendRemoveRequest(id));
    }

    private void OnCancelButtonClicked()
    {
        // ��� ��ư Ŭ�� �� ���� ��û UI �гΰ� Ǯ��ũ�� UI �г� �ݱ�
        if (removeRequestPanel != null)
        {
            removeRequestPanel.SetActive(false);
            Debug.Log("[RemoveRequest] ��� ��ư Ŭ�� - ���� ��û UI �г� ����");
        }
        if (fullscreenCanvasGroup != null)
        {
            fullscreenCanvasGroup.gameObject.SetActive(false);
            Debug.Log("[RemoveRequest] ��� ��ư Ŭ�� - Ǯ��ũ�� UI �г� ����");
        }
        else
        {
            Debug.LogWarning("[RemoveRequest] Ǯ��ũ�� UI �г��� ���� �� �����ϴ�: CanvasGroup�� null�Դϴ�!");
        }
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

        Debug.Log($"[RemoveRequest] ���� ��û ���� - Target ID: {id}");
        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, formData))
        {
            www.timeout = 20;
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;
                Debug.Log($"[RemoveRequest] ���� ��û ����: {responseText} (���� �ڵ�: {www.responseCode})");

                if (responseText.Contains("Fix Upload Succeeded!") || www.responseCode == 200)
                {
                    ShowWarning(LocalizationManager.Instance.GetText("delete_success"));
                    Debug.Log("[RemoveRequest] ���� ��û ����");

                    // ���� ��û UI �г� �ݱ�
                    if (removeRequestPanel != null)
                    {
                        removeRequestPanel.SetActive(false);
                        Debug.Log("[RemoveRequest] ���� ��û ���� - ���� ��û UI �г� ����");
                    }

                    // Ǯ��ũ�� UI �г� �ݱ�
                    if (fullscreenCanvasGroup != null)
                    {
                        fullscreenCanvasGroup.gameObject.SetActive(false);
                        Debug.Log("[RemoveRequest] ���� ��û ���� - Ǯ��ũ�� UI �г� ����");
                    }
                    else
                    {
                        Debug.LogWarning("[RemoveRequest] Ǯ��ũ�� UI �г��� ���� �� �����ϴ�: CanvasGroup�� null�Դϴ�!");
                    }
                }
                else
                {
                    ShowWarning(LocalizationManager.Instance.GetText("server_error"));
                    Debug.LogWarning($"[RemoveRequest] ���� ������ �������� ���ֵ��� ����: {responseText}");
                }
            }
            else
            {
                ShowWarning(LocalizationManager.Instance.GetText("server_error"));
                Debug.LogError($"[RemoveRequest] ���� ��û ����: {www.error} (���� �ڵ�: {www.responseCode})");
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