using UnityEngine;

/// <summary>
/// 장소 신고 시트. 사유 버튼(인스펙터의 OnClick 에서 SendReason("spam") 등)을 누르면 바로 접수한다.
/// 어떤 장소인지는 마지막으로 연 장소(DoubleTap3D)를 쓴다 — RemoveRequest 와 같은 방식.
/// </summary>
public class R0926ReportSheet : MonoBehaviour
{
    private static DoubleTap3D lastPlace;
    private bool sending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Track()
    {
        DoubleTap3D.OnDoubleTapEvent -= Remember;
        DoubleTap3D.OnDoubleTapEvent += Remember;
    }

    private static void Remember(DoubleTap3D d) => lastPlace = d;

    public void SendReason(string reason)
    {
        if (sending) return;
        if (lastPlace == null)
        {
            Toast(false, R0926LocalizedText.Lang() == "ko" ? "신고할 장소를 찾지 못했습니다." : "Could not find the place to report.");
            return;
        }
        int id = lastPlace.GetId();
        string target = id > 0 ? id.ToString() : lastPlace.GetName();
        sending = true;
        StartCoroutine(ReportService.Send("location", target, reason, lastPlace.GetName(), ok =>
        {
            sending = false;
            Toast(ok, ReportService.ResultMessage(ok));
            if (ok) gameObject.SetActive(false);
        }));
    }

    public void Close() => gameObject.SetActive(false);

    private static void Toast(bool ok, string msg)
    {
        if (ToastManager.Instance == null) return;
        if (ok) ToastManager.Instance.ShowSuccess(msg);
        else ToastManager.Instance.ShowError(msg);
    }
}
