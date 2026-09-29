using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 대화방 '···' → 신고하기 · 차단하기 (애플 심사 1.2).
/// 상대 id 는 MessagePanelManager 의 현재 대화 상대(currentChatUserId)를 읽는다.
/// ⚠ 시안용: 확정되면 MessagePanelManager 에 공개 getter 를 두고 리플렉션을 걷어낼 것.
/// ⚠ 서버 /api/dm/send 가 아직 차단 관계를 검사하지 않는다 — 차단이 실제로 막히려면 서버 수정이 함께 필요.
/// </summary>
public class R0926ChatMenu : MonoBehaviour
{
    [SerializeField] private MessagePanelManager manager;
    [SerializeField] private GameObject popover;
    [SerializeField] private UnityEngine.UI.Button messageClose;   // 메시지 목록의 닫기(도크 X)

    private static readonly FieldInfo PartnerField =
        typeof(MessagePanelManager).GetField("currentChatUserId", BindingFlags.NonPublic | BindingFlags.Instance);

    private bool busy;

    public void TogglePopover()
    {
        if (popover != null) popover.SetActive(!popover.activeSelf);
    }

    /// <summary>
    /// 대화방 도크의 X — 대화방을 정리(CloseChatRoom: 폴링 멈춤·읽음 시간 등)한 뒤 메시지 목록까지 닫아 메인으로.
    /// (← 뒤로·안드로이드 뒤로가기는 CloseChatRoom 만 불러 목록으로 돌아간다)
    /// </summary>
    public void CloseToMain()
    {
        if (manager != null) manager.CloseChatRoom();
        if (messageClose != null) messageClose.onClick.Invoke();
    }

    private void OnDisable()
    {
        if (popover != null) popover.SetActive(false);
    }

    private string Partner()
    {
        if (manager == null || PartnerField == null) return null;
        return PartnerField.GetValue(manager) as string;
    }

    public void Report()
    {
        string id = Partner();
        if (busy || string.IsNullOrEmpty(id)) return;
        busy = true;
        if (popover != null) popover.SetActive(false);
        StartCoroutine(ReportService.Send("user", id, "inappropriate", "dm", ok =>
        {
            busy = false;
            Toast(ok, ReportService.ResultMessage(ok));
        }));
    }

    public void Block()
    {
        string id = Partner();
        if (busy || string.IsNullOrEmpty(id)) return;
        busy = true;
        if (popover != null) popover.SetActive(false);
        StartCoroutine(SendBlock(id));
    }

    private IEnumerator SendBlock(string blockedId)
    {
        string me = LoginManager.Instance != null && LoginManager.Instance.CurrentUser != null
            ? LoginManager.Instance.CurrentUser.id : "";
        string json = "{\"blocker_id\":\"" + me + "\",\"blocked_id\":\"" + blockedId + "\",\"reason\":\"dm\"}";
        using (var req = new UnityWebRequest(ApiConfig.MAIN_SERVER + "/api/block", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            LoginManager.ApplyAuth(req);
            yield return req.SendWebRequest();

            bool ok = req.result == UnityWebRequest.Result.Success;
            bool ko = R0926LocalizedText.Lang() == "ko";
            Toast(ok, ok ? (ko ? "차단했습니다. 이 사용자의 메시지를 더 받지 않습니다." : "Blocked. You won't get messages from this user.")
                         : (ko ? "차단하지 못했습니다. 로그인 상태를 확인해주세요." : "Could not block. Please check you are signed in."));
        }
        busy = false;
    }

    private static void Toast(bool ok, string msg)
    {
        if (ToastManager.Instance == null) return;
        if (ok) ToastManager.Instance.ShowSuccess(msg);
        else ToastManager.Instance.ShowError(msg);
    }
}
