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

    [Header("내 장소 지우기 (2026-10-26) — Redesign0926Apply10 이 연결")]
    [SerializeField] private R0926LocalizedText menuLabel;    // 장소 '···' 의 '삭제 요청' 칸 — 내 장소면 '삭제'
    [SerializeField] private R0926LocalizedText dialogText;   // 묻는 글
    [SerializeField] private R0926LocalizedText yesLabel;     // '삭제 요청' 단추 — 내 장소면 '지우기'

    /// <summary>올린 사람이 장소를 지웠다 — 고치기 창이 그 장소를 열고 있었으면 닫는다</summary>
    public static event Action<int> PlaceDeleted;
    private int ownId;        // 지금 묻는 장소가 내 장소면 그 번호 (0 = 남의 장소 — 예전처럼 삭제 요청)
    private bool deleting;
    private string[] menuReq, dialogReq, yesReq;   // 장면에 적힌 원래 문구 (남의 장소일 때)

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
        menuReq = Words(menuLabel);
        dialogReq = Words(dialogText);
        yesReq = Words(yesLabel);

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

        if (dialogText == null && titleText != null) dialogText = titleText.GetComponent<R0926LocalizedText>();
        if (yesLabel == null && removeButtonText != null) yesLabel = removeButtonText.GetComponent<R0926LocalizedText>();
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
        PlaceData p = DataManager.Instance != null ? DataManager.Instance.PlaceOf(tappedDoubleTap) : null;
        SetOwn(LooksMine(p) ? p.id : 0);   // 내 장소면 '삭제'(바로 지운다) — 서버가 한 번 더 확인한다
    }

    /// <summary>내가 올린 장소로 보이나 — 앱은 이름만 본다 (이름을 바꾼 뒤·같은 이름의 옛 계정 장소는 서버가 가린다)</summary>
    public static bool LooksMine(PlaceData p)
    {
        if (p == null || string.IsNullOrEmpty(p.username)) return false;
        LoginManager lm = LoginManager.Instance;
        return lm != null && lm.IsLoggedIn && p.username == lm.CurrentUsername;
    }

    // 메뉴 칸 · 묻는 글 · 단추를 내 장소(바로 지우기) / 남의 장소(삭제 요청)에 맞게
    private void SetOwn(int id)
    {
        ownId = id;
        if (id > 0)
        {
            Put(menuLabel, new[] { "삭제", "Delete", "削除", "删除", "Eliminar" });
            Put(dialogText, new[] { "이 장소를 지울까요?\n사진과 댓글도 함께 지워져요", "Delete this place?\nIts photos and comments go too", "この場所を削除しますか？\n写真とコメントも消えます", "要删除这个地点吗？\n照片和评论也会一起删除", "¿Eliminar este lugar?\nTambién se borran sus fotos y comentarios" });
            Put(yesLabel, new[] { "지우기", "Delete", "削除する", "删除", "Eliminar" });
        }
        else
        {
            Put(menuLabel, menuReq);
            Put(dialogText, dialogReq);
            Put(yesLabel, yesReq);
        }
    }

    private static string[] Words(R0926LocalizedText t) => t != null ? new[] { t.ko, t.en, t.ja, t.zh, t.es } : null;

    private static void Put(R0926LocalizedText t, string[] w)
    {
        if (t != null && w != null) t.Set(w[0], w[1], w[2], w[3], w[4]);
    }

    private void OnRemoveButtonClicked()
    {
        if (ownId > 0)
        {
            if (!deleting) StartCoroutine(DeleteOwnPlace(ownId));
            return;
        }

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
            LoginManager.ApplyAuth(www);
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

    // 내 장소 지우기 — 서버가 올린 사람인지 다시 확인한다 (아니면 403 → 예전처럼 삭제 요청으로)
    private IEnumerator DeleteOwnPlace(int id)
    {
        deleting = true;
        using (UnityWebRequest www = UnityWebRequest.Delete(CubeDataFixManager.PlaceUrl(id)))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            LoginManager.ApplyAuth(www);
            www.timeout = 20;
            yield return www.SendWebRequest();
            deleting = false;

            PlaceEditResponse res = PlaceEditResponse.Parse(www.downloadHandler.text);
            if (res != null && (res.ok || res.error == "not_found"))
            {
                if (DataManager.Instance != null) DataManager.Instance.RemovePlace(id);
                SetOwn(0);
                if (removeRequestPanel != null) removeRequestPanel.SetActive(false);
                if (fullscreenCanvasGroup != null) fullscreenCanvasGroup.gameObject.SetActive(false);
                PlaceDeleted?.Invoke(id);
                ShowWarning(res.ok
                    ? R0926LocalizedText.Say("지웠어요", "Deleted", "削除しました", "已删除", "Eliminado")
                    : R0926LocalizedText.Say("이미 지워진 장소예요", "This place was already deleted", "この場所はすでに削除されています", "该地点已被删除", "Este lugar ya fue eliminado"));
                yield break;
            }
            if (res != null && res.error == "not_owner")
            {
                SetOwn(0);   // 이름만 같은 남의 장소 — 예전처럼 삭제 요청으로 보낸다 ('삭제 요청이 완료되었습니다')
                yield return SendRemoveRequest(id);
                yield break;
            }

            LoginManager.CheckUnauthorized(www);
            Debug.LogWarning($"[RemoveRequest] 지우기 실패 id={id}: {www.responseCode} {res?.error} {www.error}");
            ShowWarning(res != null && res.error == "login_required"
                ? R0926LocalizedText.Say("로그인이 필요해요", "Please log in", "ログインが必要です", "请先登录", "Inicia sesión")
                : LocalizationManager.Instance.GetText("server_error"));
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