using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System;

public class RemoveRequest : MonoBehaviour
{
    [SerializeField] private Button removeButton; // 삭제 요청 버튼
    [SerializeField] private Button cancelButton; // 취소 버튼
    [SerializeField] private GameObject warningObj; // 경고 메시지 표시용 오브젝트
    [SerializeField] private GameObject removeRequestPanel; // 삭제 요청 UI 패널
    [SerializeField] private DoubleTap3D initialDoubleTap; // 초기 참조용 DoubleTap3D (선택적)

    private DoubleTap3D doubleTap; // 동적으로 참조할 DoubleTap3D
    private CanvasGroup fullscreenCanvasGroup; // 풀스크린 UI 패널

    private const string serverUrl = "https://woopang.com:5000/fix_upload/";

    void Start()
    {
        doubleTap = initialDoubleTap; // 초기값 설정

        if (doubleTap != null)
        {
            fullscreenCanvasGroup = doubleTap.GetComponent<CanvasGroup>(); // 풀스크린 UI 패널 참조
            if (fullscreenCanvasGroup == null)
            {
                Debug.LogError("[RemoveRequest] DoubleTap3D에서 CanvasGroup을 찾을 수 없습니다!");
            }
            Debug.Log($"[RemoveRequest] 초기 DoubleTap3D 연결됨 - ID: {doubleTap.GetId()}, GameObject: {(doubleTap != null ? doubleTap.gameObject.name : "null")}");
        }
        else
        {
            Debug.LogWarning("[RemoveRequest] 초기 DoubleTap3D가 연결되지 않았습니다. 더블 터치로 동적으로 설정됩니다.");
        }

        if (removeButton != null)
        {
            removeButton.onClick.AddListener(OnRemoveButtonClicked);
        }
        else
        {
            Debug.LogError("[RemoveRequest] RemoveButton이 할당되지 않았습니다!");
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelButtonClicked);
        }
        else
        {
            Debug.LogError("[RemoveRequest] CancelButton이 할당되지 않았습니다!");
        }

        if (warningObj != null)
        {
            warningObj.SetActive(false); // 초기 비활성화
        }
        else
        {
            Debug.LogError("[RemoveRequest] WarningObj가 할당되지 않았습니다!");
        }

        if (removeRequestPanel != null)
        {
            removeRequestPanel.SetActive(false); // 초기 비활성화
        }
        else
        {
            Debug.LogError("[RemoveRequest] RemoveRequestPanel이 할당되지 않았습니다!");
        }

        // 더블 터치 이벤트 구독
        DoubleTap3D.OnDoubleTapEvent += HandleDoubleTap;
        Debug.Log("[RemoveRequest] DoubleTap3D.OnDoubleTapEvent 구독 완료");
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        DoubleTap3D.OnDoubleTapEvent -= HandleDoubleTap;
        Debug.Log("[RemoveRequest] DoubleTap3D.OnDoubleTapEvent 구독 해제");
    }

    private void HandleDoubleTap(DoubleTap3D tappedDoubleTap)
    {
        // 더블 터치된 DoubleTap3D 인스턴스로 참조 업데이트
        doubleTap = tappedDoubleTap;
        Debug.Log($"[RemoveRequest] 더블 터치로 DoubleTap3D 업데이트 - ID: {(doubleTap != null ? doubleTap.GetId() : -1)}, GameObject: {(doubleTap != null ? doubleTap.gameObject.name : "null")}");
    }

    private void OnRemoveButtonClicked()
    {
        if (doubleTap == null)
        {
            ShowWarning(LocalizationManager.Instance.GetText("no_object_selected"));
            Debug.LogError("[RemoveRequest] OnRemoveButtonClicked: DoubleTap3D가 null입니다!");
            return;
        }

        int id = doubleTap.GetId();
        Debug.Log($"[RemoveRequest] OnRemoveButtonClicked - ID: {id}");
        if (id <= 0)
        {
            ShowWarning(LocalizationManager.Instance.GetText("valid_id_not_found"));
            Debug.LogError($"[RemoveRequest] 유효하지 않은 ID: {id}");
            return;
        }

        StartCoroutine(SendRemoveRequest(id));
    }

    private void OnCancelButtonClicked()
    {
        // 취소 버튼 클릭 시 삭제 요청 UI 패널과 풀스크린 UI 패널 닫기
        if (removeRequestPanel != null)
        {
            removeRequestPanel.SetActive(false);
            Debug.Log("[RemoveRequest] 취소 버튼 클릭 - 삭제 요청 UI 패널 닫힘");
        }
        if (fullscreenCanvasGroup != null)
        {
            fullscreenCanvasGroup.gameObject.SetActive(false);
            Debug.Log("[RemoveRequest] 취소 버튼 클릭 - 풀스크린 UI 패널 닫힘");
        }
        else
        {
            Debug.LogWarning("[RemoveRequest] 풀스크린 UI 패널을 닫을 수 없습니다: CanvasGroup이 null입니다!");
        }
    }

    private IEnumerator SendRemoveRequest(int id)
    {
        WWWForm formData = new WWWForm();
        formData.AddField("target_id", id.ToString()); // locations_fix 테이블의 target_id로 저장
        formData.AddField("remove_request", "true"); // 삭제 요청
        // 나머지 필드는 기본값으로 설정
        formData.AddField("username", "");
        formData.AddField("name", "");
        formData.AddField("pet_friendly", "false");
        formData.AddField("separate_restroom", "false");
        formData.AddField("instagram_id", "");
        formData.AddField("description", "Remove request submitted via button");
        
        // 시간대 정보 추가
        formData.AddField("timezone", GetTimezone());
        formData.AddField("timezone_offset", GetTimezoneOffset());
        
        formData.AddField("folder", $"remove_{DateTime.Now:yyyyMMdd_HHmmss}");
        formData.AddField("main_photo", "");

        Debug.Log($"[RemoveRequest] 삭제 요청 전송 - Target ID: {id}");
        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, formData))
        {
            www.timeout = 20;
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;
                Debug.Log($"[RemoveRequest] 삭제 요청 응답: {responseText} (응답 코드: {www.responseCode})");

                if (responseText.Contains("Fix Upload Succeeded!") || www.responseCode == 200)
                {
                    ShowWarning(LocalizationManager.Instance.GetText("delete_success"));
                    Debug.Log("[RemoveRequest] 삭제 요청 성공");

                    // 삭제 요청 UI 패널 닫기
                    if (removeRequestPanel != null)
                    {
                        removeRequestPanel.SetActive(false);
                        Debug.Log("[RemoveRequest] 삭제 요청 성공 - 삭제 요청 UI 패널 닫힘");
                    }

                    // 풀스크린 UI 패널 닫기
                    if (fullscreenCanvasGroup != null)
                    {
                        fullscreenCanvasGroup.gameObject.SetActive(false);
                        Debug.Log("[RemoveRequest] 삭제 요청 성공 - 풀스크린 UI 패널 닫힘");
                    }
                    else
                    {
                        Debug.LogWarning("[RemoveRequest] 풀스크린 UI 패널을 닫을 수 없습니다: CanvasGroup이 null입니다!");
                    }
                }
                else
                {
                    ShowWarning(LocalizationManager.Instance.GetText("server_error"));
                    Debug.LogWarning($"[RemoveRequest] 서버 응답이 성공으로 간주되지 않음: {responseText}");
                }
            }
            else
            {
                ShowWarning(LocalizationManager.Instance.GetText("server_error"));
                Debug.LogError($"[RemoveRequest] 삭제 요청 실패: {www.error} (응답 코드: {www.responseCode})");
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
            Invoke("HideWarning", 2f); // 2초 후 경고 메시지 숨김
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