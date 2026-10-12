using System;
using System.Collections;
using System.Text;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 보물찾기 서버 호출 — /api/hunts (server/hunts/hunts_server.py · 명세 docs/claude/plans/treasure-hunt.md).
/// 로그인 토큰을 붙여 JSON 으로 주고받는다. 응답 코드와 본문(JSON, 못 읽으면 빈 객체)을 돌려준다.
/// </summary>
public static class R0926HuntApi
{
#if UNITY_EDITOR
    /// <summary>플레이 시험이 시험 서버로 돌릴 때 (비어 있으면 실제 서버)</summary>
    public static string EditorBase;
    /// <summary>플레이 시험용 로그인 대신 — 이 값이 있으면 X-Test-User 머리글로 보낸다 (시험 서버만 받는다)</summary>
    public static string EditorTestUser;
#endif

    private static string Base
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorBase)) return EditorBase;
#endif
            return ApiConfig.MAIN_SERVER;
        }
    }

    public static bool CanCall
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorTestUser)) return true;
#endif
            return LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn;
        }
    }

    public static IEnumerator Get(string path, Action<long, JSONNode> done) => Send("GET", path, null, done);

    public static IEnumerator Post(string path, JSONNode body, Action<long, JSONNode> done) => Send("POST", path, body, done);

    private static IEnumerator Send(string method, string path, JSONNode body, Action<long, JSONNode> done)
    {
        using (var req = new UnityWebRequest(Base + path, method))
        {
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 15;
            LoginManager.ApplyAuth(req);
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorTestUser)) req.SetRequestHeader("X-Test-User", EditorTestUser);
#endif
            yield return req.SendWebRequest();
            JSONNode j = null;
            try { j = JSON.Parse(req.downloadHandler != null ? req.downloadHandler.text : ""); }
            catch (Exception) { j = null; }
            done?.Invoke(req.result == UnityWebRequest.Result.ConnectionError ? 0 : req.responseCode, j ?? new JSONObject());
        }
    }

    /// <summary>서버 오류 코드 → 사용자에게 보일 말 (한국어 · 영어)</summary>
    public static string ErrorText(string code, string lang)
    {
        bool ko = lang == "ko";
        switch (code)
        {
            case "login": return ko ? "로그인하면 보물찾기를 할 수 있어요" : "Log in to play treasure hunts";
            case "need_rules": return ko ? "주최 수칙을 먼저 확인해 주세요" : "Please read the host rules first";
            case "host_suspended": return ko ? "신고가 쌓여 지금은 보물을 숨길 수 없어요" : "Hosting is paused after reports";
            case "weak_gps": return ko ? "GPS 가 아직 부정확해요 — 하늘이 보이는 곳에서 잠시 기다려 주세요" : "GPS isn't accurate yet — wait a moment outdoors";
            case "mock_location": return ko ? "가짜 위치로는 숨길 수 없어요" : "Mock locations aren't allowed";
            case "teleport": return ko ? "위치가 갑자기 크게 바뀌었어요 — 잠시 뒤 다시 해 주세요" : "Your location jumped — try again shortly";
            case "daily_limit": return ko ? "오늘은 3개까지 숨길 수 있어요" : "Up to 3 games a day";
            case "open_limit": return ko ? "진행·예약 중인 게임은 2개까지예요" : "Up to 2 open games at a time";
            case "title": return ko ? "제목을 써 주세요" : "Please add a title";
            case "code": return ko ? "찾는 사람이 넣을 코드를 써 주세요" : "Please set the code to enter";
            case "out_of_range": return ko ? "참가 범위 밖이에요" : "You're outside the game area";
            case "friends_only": return ko ? "맞팔 친구만 참가할 수 있어요" : "Mutual friends only";
            case "join_code": return ko ? "참가 코드가 맞지 않아요" : "Wrong join code";
            case "not_open": return ko ? "이미 끝난 게임이에요" : "This game has ended";
            case "location": return ko ? "위치를 아직 못 받았어요" : "Location not available yet";
            case "slow_down": return ko ? "잠시 뒤 다시 확인할게요" : "Checking again shortly";
            case "full": return ko ? "참가 인원이 다 찼어요" : "This game is full";
            case "players": return ko ? "참가 인원은 당첨 인원보다 적을 수 없어요" : "Max players can't be fewer than winners";
            // 별점 · 신고 (프로필의 보물찾기 칸) — host · not_joined 는 참가·판정에서도 온다
            case "stars": return ko ? "별을 눌러 점수를 골라 주세요" : "Tap a star to rate";
            case "host": return ko ? "주최자 본인은 할 수 없어요" : "Not available to the host";
            case "not_ended": return ko ? "게임이 끝난 뒤에 남길 수 있어요" : "You can rate after the game ends";
            case "closed": return ko ? "별점은 게임이 끝나고 7일 안에만 남길 수 있어요" : "Ratings close 7 days after the game";
            case "not_joined": return ko ? "참가한 사람만 할 수 있어요" : "Only players can do this";
            case "not_played": return ko ? "보물을 찾았거나 1km 안까지 간 참가자만 남길 수 있어요" : "Only players who found it or got within 1km can rate";
            case "device_used": return ko ? "이 기기로는 이미 다른 계정이 별점을 남겼어요" : "Another account already rated from this device";
            case "device": return ko ? "기기 정보를 읽지 못했어요" : "Couldn't read this device";
            case "not_participant": return ko ? "이 사람 게임에 참가한 적이 있어야 신고할 수 있어요" : "Only players of this host's games can report";
            case "reason": case "target": return ko ? "신고 내용을 다시 골라 주세요" : "Please choose a reason again";
            case "not_found": return ko ? "찾을 수 없어요 — 이미 지워졌을 수 있어요" : "Not found — it may have been removed";
            default: return ko ? "잠시 뒤 다시 해 주세요" : "Please try again shortly";
        }
    }
}
