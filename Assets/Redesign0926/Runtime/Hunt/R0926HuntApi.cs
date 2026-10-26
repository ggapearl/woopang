using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>비밀번호를 건 보물의 비밀번호 글자 수 (서버 CODE_LEN 과 같다)</summary>
    public const int CodeLen = 4;

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

    /// <summary>서버 파일 주소 — 'uploads/…' 같은 상대 주소면 서버 앞주소를 붙인다 (보물 모양 사진·3D 모델)</summary>
    public static string Url(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains("://")) return path;
        return Base.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    /// <summary>파일 올리기 (보물 모양 사진·3D 모델) — multipart 'file' + 글자 칸들</summary>
    public static IEnumerator Upload(string path, byte[] data, string fileName, string mime, Dictionary<string, string> fields, Action<long, JSONNode> done)
    {
        var form = new List<IMultipartFormSection>();
        if (fields != null) foreach (var kv in fields) form.Add(new MultipartFormDataSection(kv.Key, kv.Value));
        form.Add(new MultipartFormFileSection("file", data, fileName, mime));
        using (var req = UnityWebRequest.Post(Base + path, form))
        {
            req.timeout = 120;
            LoginManager.ApplyAuth(req);
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorTestUser)) req.SetRequestHeader("X-Test-User", EditorTestUser);
#endif
            yield return req.SendWebRequest();
            LoginManager.CheckUnauthorized(req);   // 401 이면 로그인이 살아 있는지 서버에 확인 — 죽었으면 다시 로그인하라고
            JSONNode j = null;
            try { j = JSON.Parse(req.downloadHandler != null ? req.downloadHandler.text : ""); }
            catch (Exception) { j = null; }
            done?.Invoke(req.result == UnityWebRequest.Result.ConnectionError ? 0 : req.responseCode, j ?? new JSONObject());
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
            LoginManager.CheckUnauthorized(req);   // 401 이면 로그인이 살아 있는지 서버에 확인 — 죽었으면 다시 로그인하라고
            JSONNode j = null;
            try { j = JSON.Parse(req.downloadHandler != null ? req.downloadHandler.text : ""); }
            catch (Exception) { j = null; }
            done?.Invoke(req.result == UnityWebRequest.Result.ConnectionError ? 0 : req.responseCode, j ?? new JSONObject());
        }
    }

    /// <summary>서버 오류 코드 → 사용자에게 보일 말 (lang: ko · en · ja · zh · es, 그 밖은 영어)</summary>
    public static string ErrorText(string code, string lang)
    {
        string L(string ko, string en, string ja, string zh, string es) => lang == "ko" ? ko : lang == "ja" ? ja : lang == "zh" ? zh : lang == "es" ? es : en;
        switch (code)
        {
            case "login": return L("로그인하면 보물찾기를 할 수 있어요", "Log in to play treasure hunts", "ログインすると宝探しに参加できます", "登录后即可参加寻宝", "Inicia sesión para jugar a las búsquedas del tesoro");
            case "need_rules": return L("주최 수칙을 먼저 확인해 주세요", "Please read the host rules first", "先に主催ルールを確認してください", "请先阅读主办规则", "Primero lee las normas para organizar");
            case "host_suspended": return L("신고가 쌓여 지금은 보물을 숨길 수 없어요", "Hosting is paused after reports", "通報が重なったため、今は宝を隠せません", "因被多次举报，暂时无法藏宝", "Por las denuncias, ahora no puedes esconder tesoros");
            case "weak_gps": return L("GPS 가 아직 부정확해요 — 하늘이 보이는 곳에서 잠시 기다려 주세요", "GPS isn't accurate yet — wait a moment outdoors", "GPSがまだ不正確です — 空が見える場所で少しお待ちください", "GPS 还不够准确 — 请到能看到天空的地方稍等", "El GPS aún no es preciso — espera un momento al aire libre");
            case "mock_location": return L("가짜 위치로는 숨길 수 없어요", "Mock locations aren't allowed", "偽の位置情報では隠せません", "不能使用虚拟位置藏宝", "No se permiten ubicaciones simuladas");
            case "teleport": return L("위치가 갑자기 크게 바뀌었어요 — 잠시 뒤 다시 해 주세요", "Your location jumped — try again shortly", "位置が急に大きく変わりました — 少ししてからもう一度お試しください", "位置突然大幅变化 — 请稍后再试", "Tu ubicación cambió de golpe — inténtalo en un momento");
            case "daily_limit": return L("오늘은 3개까지 숨길 수 있어요", "Up to 3 games a day", "1日に隠せるのは3つまでです", "每天最多可藏 3 个", "Hasta 3 juegos al día");
            case "open_limit": return L("진행·예약 중인 게임은 2개까지예요", "Up to 2 open games at a time", "進行中・予約中のゲームは2つまでです", "进行中或预约中的游戏最多 2 个", "Hasta 2 juegos abiertos a la vez");
            case "title": return L("제목을 써 주세요", "Please add a title", "タイトルを入力してください", "请填写标题", "Añade un título");
            case "code": return L("비밀번호는 4자로 정해 주세요", "Set a 4-character password", "パスワードは4文字で設定してください", "请设置 4 个字符的密码", "La contraseña debe tener 4 caracteres");
            case "question": return L("비밀번호를 건 보물엔 힌트를 꼭 써 주세요", "A password-locked treasure needs a hint", "パスワード付きの宝にはヒントが必要です", "设置了密码的宝藏必须填写提示", "Un tesoro con contraseña necesita una pista");
            case "out_of_range": return L("참가 범위 밖이에요", "You're outside the game area", "参加範囲の外です", "你不在参加范围内", "Estás fuera del área del juego");
            case "friends_only": return L("맞팔 친구만 참가할 수 있어요", "Mutual friends only", "相互フォローの友だちだけが参加できます", "仅限互相关注的好友参加", "Solo amigos mutuos");
            case "join_code": return L("참가 코드가 맞지 않아요", "Wrong join code", "参加コードが違います", "参加代码不正确", "Código de acceso incorrecto");
            case "not_open": return L("이미 끝난 게임이에요", "This game has ended", "すでに終了したゲームです", "游戏已经结束", "Este juego ya terminó");
            case "location": return L("위치를 아직 못 받았어요", "Location not available yet", "まだ位置情報を取得できていません", "尚未获取到位置", "Aún no hay ubicación");
            case "slow_down": return L("잠시 뒤 다시 확인할게요", "Checking again shortly", "少ししてからもう一度確認します", "稍后再为你确认", "Lo comprobaremos de nuevo en breve");
            case "full": return L("참가 인원이 다 찼어요", "This game is full", "参加人数がいっぱいです", "参加人数已满", "Este juego está completo");
            case "players": return L("참가 인원은 당첨 인원보다 적을 수 없어요", "Max players can't be fewer than winners", "参加人数は当選人数より少なくできません", "参加人数不能少于中奖人数", "El máximo de jugadores no puede ser menor que el de ganadores");
            // 별점 · 신고 (프로필의 보물찾기 칸) — host · not_joined 는 참가·판정에서도 온다
            case "stars": return L("별을 눌러 점수를 골라 주세요", "Tap a star to rate", "星をタップして評価を選んでください", "点击星星选择评分", "Toca una estrella para valorar");
            case "host": return L("주최자 본인은 할 수 없어요", "Not available to the host", "主催者本人はできません", "主办者本人无法操作", "No disponible para quien organiza");
            case "not_ended": return L("게임이 끝난 뒤에 남길 수 있어요", "You can rate after the game ends", "ゲーム終了後に評価できます", "游戏结束后才能评分", "Podrás valorar cuando termine el juego");
            case "closed": return L("별점은 게임이 끝나고 7일 안에만 남길 수 있어요", "Ratings close 7 days after the game", "評価はゲーム終了から7日以内のみです", "只能在游戏结束后 7 天内评分", "Las valoraciones cierran 7 días después del juego");
            case "not_joined": return L("참가한 사람만 할 수 있어요", "Only players can do this", "参加した人だけができます", "仅限参加者", "Solo para jugadores");
            case "not_played": return L("보물을 찾았거나 1km 안까지 간 참가자만 남길 수 있어요", "Only players who found it or got within 1km can rate", "宝を見つけたか、1km以内まで近づいた参加者だけが評価できます", "只有找到宝藏或到过 1km 范围内的参加者才能评分", "Solo pueden valorar quienes lo encontraron o se acercaron a menos de 1 km");
            case "device_used": return L("이 기기로는 이미 다른 계정이 별점을 남겼어요", "Another account already rated from this device", "この端末ではすでに別のアカウントが評価しています", "此设备上已有其他账号评过分", "Otra cuenta ya valoró desde este dispositivo");
            case "device": return L("기기 정보를 읽지 못했어요", "Couldn't read this device", "端末の情報を読み取れませんでした", "无法读取设备信息", "No se pudo leer el dispositivo");
            case "not_participant": return L("이 사람 게임에 참가한 적이 있어야 신고할 수 있어요", "Only players of this host's games can report", "この人のゲームに参加したことがある人だけが通報できます", "只有参加过此人游戏的人才能举报", "Solo quienes jugaron con este organizador pueden denunciar");
            case "reason": case "target": return L("신고 내용을 다시 골라 주세요", "Please choose a reason again", "通報内容をもう一度選んでください", "请重新选择举报内容", "Vuelve a elegir un motivo");
            case "not_found": return L("찾을 수 없어요 — 이미 지워졌을 수 있어요", "Not found — it may have been removed", "見つかりません — すでに削除された可能性があります", "找不到 — 可能已被删除", "No encontrado — puede que se haya eliminado");
            // 보물 모양 (사진·3D 모델 올리기)
            case "object": case "object_url": case "file": return L("보물 모양을 다시 골라 주세요", "Please choose the treasure's look again", "宝の見た目をもう一度選んでください", "请重新选择宝藏外观", "Vuelve a elegir el aspecto del tesoro");
            case "file_type": return L("이 파일은 쓸 수 없어요 — 사진(JPG·PNG) 또는 GLB 한 파일로 골라 주세요", "Can't use this file — choose a photo (JPG/PNG) or a single GLB", "このファイルは使えません — 写真（JPG・PNG）か GLB 1ファイルを選んでください", "无法使用此文件 — 请选择照片（JPG/PNG）或单个 GLB", "No se puede usar este archivo — elige una foto (JPG/PNG) o un solo GLB");
            case "file_size": return L("파일이 너무 커요 — 3D 모델은 10MB 까지예요", "File too large — 3D models up to 10MB", "ファイルが大きすぎます — 3Dモデルは10MBまでです", "文件太大 — 3D 模型最大 10MB", "Archivo demasiado grande — modelos 3D hasta 10 MB");
            case "upload_limit": return L("오늘은 더 올릴 수 없어요 — 내일 다시 해 주세요", "Upload limit reached for today", "今日はこれ以上アップロードできません", "今天无法再上传了", "Has alcanzado el límite de subidas de hoy");
            default: return L("잠시 뒤 다시 해 주세요", "Please try again shortly", "少ししてからもう一度お試しください", "请稍后再试", "Inténtalo de nuevo en breve");
        }
    }
}
