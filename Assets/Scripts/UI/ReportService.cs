using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 신고 전송. Apple 심사 1.2(사용자 생성 콘텐츠 앱)는 신고가 실제로 동작해야 한다.
/// 서버는 신고자를 로그인 토큰에서 정하고, 운영자에게 즉시 알린다.
/// target_type: user / location / comment / dm
/// </summary>
public static class ReportService
{
    [Serializable]
    private class ReportBody
    {
        public string target_type;
        public string target_id;
        public string reason;
        public string detail;
    }

    [Serializable]
    private class ReportResult
    {
        public bool success;
    }

    public static IEnumerator Send(string targetType, string targetId, string reason, string detail, Action<bool> done)
    {
        string json = JsonUtility.ToJson(new ReportBody
        {
            target_type = targetType,
            target_id = targetId,
            reason = reason,
            detail = detail ?? ""
        });

        using (UnityWebRequest req = new UnityWebRequest(ApiConfig.MAIN_SERVER + "/api/report", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            LoginManager.ApplyAuth(req);
            yield return req.SendWebRequest();

            bool ok = false;
            if (req.result == UnityWebRequest.Result.Success)
            {
                ReportResult r = JsonUtility.FromJson<ReportResult>(req.downloadHandler.text);
                ok = r != null && r.success;
            }
            done?.Invoke(ok);
        }
    }

    /// <summary>신고 결과 안내 문구 (앱 언어)</summary>
    public static string ResultMessage(bool ok) => ok
        ? R0926LocalizedText.Say("신고가 접수되었습니다. 검토 후 조치하겠습니다.", "Report received. We will review it.", "報告を受け付けました。確認のうえ対応します。", "举报已提交，我们会审核后处理。", "Denuncia recibida. La revisaremos.")
        : R0926LocalizedText.Say("신고하지 못했습니다. 로그인 상태를 확인해주세요.", "Could not send the report. Please check you are signed in.", "報告できませんでした。ログイン状態を確認してください。", "举报失败，请确认登录状态。", "No se pudo enviar la denuncia. Comprueba que has iniciado sesión.");
}
