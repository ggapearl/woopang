using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 신고 전송. Apple 심사 1.2(사용자 생성 콘텐츠 앱)는 신고가 실제로 동작해야 한다.
/// 서버는 신고자를 로그인 토큰에서 정하고, 운영자에게 즉시 알린다.
/// target_type: user / location / comment / dm
/// 댓글은 서로 다른 10명이 신고하면 서버가 지운다 (removed — 2026-10-26)
/// </summary>
public static class ReportService
{
    public static string EditorBase;   // 플레이 시험 — 신고를 내 PC 시험 서버로 (운영 서버에 시험 신고를 남기지 않게)

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
        public bool removed;
    }

    public static IEnumerator Send(string targetType, string targetId, string reason, string detail, Action<bool> done)
        => Send(targetType, targetId, reason, detail, (ok, removed) => done?.Invoke(ok));

    /// <summary>done(접수됐는지, 그 신고로 지워졌는지)</summary>
    public static IEnumerator Send(string targetType, string targetId, string reason, string detail, Action<bool, bool> done)
    {
        string json = JsonUtility.ToJson(new ReportBody
        {
            target_type = targetType,
            target_id = targetId,
            reason = reason,
            detail = detail ?? ""
        });

        string url = (string.IsNullOrEmpty(EditorBase) ? ApiConfig.MAIN_SERVER : EditorBase) + "/api/report";
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            LoginManager.ApplyAuth(req);
            yield return req.SendWebRequest();

            bool ok = false, removed = false;
            if (req.result == UnityWebRequest.Result.Success)
            {
                ReportResult r = JsonUtility.FromJson<ReportResult>(req.downloadHandler.text);
                ok = r != null && r.success;
                removed = ok && r.removed;
            }
            else LoginManager.CheckUnauthorized(req);
            done?.Invoke(ok, removed);
        }
    }

    /// <summary>신고 결과 안내 문구 (앱 언어)</summary>
    public static string ResultMessage(bool ok) => ok
        ? R0926LocalizedText.Say("신고가 접수되었습니다. 검토 후 조치하겠습니다.", "Report received. We will review it.", "報告を受け付けました。確認のうえ対応します。", "举报已提交，我们会审核后处理。", "Denuncia recibida. La revisaremos.")
        : R0926LocalizedText.Say("신고하지 못했습니다. 로그인 상태를 확인해주세요.", "Could not send the report. Please check you are signed in.", "報告できませんでした。ログイン状態を確認してください。", "举报失败，请确认登录状态。", "No se pudo enviar la denuncia. Comprueba que has iniciado sesión.");
}
