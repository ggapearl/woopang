using UnityEngine;

/// <summary>
/// 댓글 신고 시트 (2026-10-26) — 대표님 10-24 「댓글도 '신고' 할 수 있도록 하고 10개정도 신고 들어가면 그냥 삭제」.
/// 남의 댓글을 왼쪽으로 밀면 나오는 '신고' → 이 시트 → 사유를 누르면 바로 접수(인스펙터 OnClick 의 SendReason("spam") 등).
/// 접수되면 그 댓글은 내 목록에서 바로 빠진다(서버도 신고한 사람에겐 다시 주지 않는다). 서로 다른 10명이 신고하면 서버가 지운다.
/// </summary>
public class R0926CommentReportSheet : MonoBehaviour
{
    private CommentItem target;
    private bool sending;
    private SwipeToClose behind;   // 뒤의 댓글 창 끌어 닫기 — 시트가 떠 있는 동안은 쉰다 (손가락 하나에 둘이 같이 끌린다)

    private void OnEnable()
    {
        behind = GetComponentInParent<SwipeToClose>();
        if (behind != null) behind.enabled = false;
    }

    private void OnDisable()
    {
        if (behind != null) behind.enabled = true;
    }

    public void Open(CommentItem item)
    {
        target = item;
        sending = false;
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    public void SendReason(string reason)
    {
        if (sending || CommentManager.Instance == null) return;
        sending = true;
        CommentManager.Instance.ReportComment(target, reason, ok =>
        {
            sending = false;
            Toast(ok, ok
                ? R0926LocalizedText.Say("신고가 접수되었습니다. 이 댓글은 이제 보이지 않아요.", "Report received. You won't see this comment again.", "報告を受け付けました。このコメントは表示されなくなります。", "举报已提交，你将不再看到这条评论。", "Denuncia recibida. Ya no verás este comentario.")
                : ReportService.ResultMessage(false));
            if (ok && this != null) gameObject.SetActive(false);
        });
    }

    public void Close() => gameObject.SetActive(false);

    private static void Toast(bool ok, string msg)
    {
        if (ToastManager.Instance == null) return;
        if (ok) ToastManager.Instance.ShowSuccess(msg);
        else ToastManager.Instance.ShowError(msg);
    }
}
