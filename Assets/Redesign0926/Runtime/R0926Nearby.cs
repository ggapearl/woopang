using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 근처 축제 · 유기동물 보호소 (2026-10-24 대표님) — 우팡 서버 /api/nearby/festivals · /api/nearby/shelters.
///  · 목록의 '축제' 칩: 거리와 상관없이 가까운 축제만 — 며칠부터 하는지와 거리, 줄 앞은 축제 분위기 그림 (서버가 icon 으로 고른다)
///  · '반려견만': 목록 맨 위에 가까운 보호소 두세 곳
/// 서버는 축제를 한국관광공사 국문 관광정보, 보호소를 카카오 로컬 검색으로 찾는다 (키는 서버에만).
/// 하늘 옆 장 '근처 핫플'(/api/nearby/hot, 10-25)도 여기서 받는다 — 하늘을 볼 때 R0926SkyPages 가 부른다.
/// 목록이 열려 있을 때 PlaceListManager 가 부른다 — 1km 넘게 움직였거나 30분이 지났을 때만 다시 묻고, 받으면 Changed.
/// 축제 · 핫플 맨 위에는 관리자가 /ads/admin 에서 띄운 협력 업체(광고) 줄이 온다 (10-25) — 이 앱은 '광고'를 밝히고 이모지 그림을 붙이므로 promo=1 을 보낸다.
/// </summary>
public static class R0926Nearby
{
    [Serializable]
    public class Festival
    {
        public string id, title, start, end, addr, tel, image, icon;   // start·end: YYYYMMDD
        public double lat, lon;
        public int dist;
        // 관리자가 띄운 협력 업체(광고) 줄 — 둘째 줄(sub) · 누르면 열 곳(link, 없으면 카카오맵의 그 자리) · 이모지 그림(emoji_url)
        public bool promo;
        public string ad_id, sub, link, emoji_url;
    }

    [Serializable]
    public class Shelter
    {
        public string id, name, addr, tel, url;   // url: 카카오맵 장소 쪽 (전화·길찾기)
        public double lat, lon;
        public int dist;
    }

    /// <summary>하늘 옆 장 '근처 핫플' — 3km 안에서 요즘 좋아요 · 댓글이 많은 우팡 장소 (서버 /api/nearby/hot)</summary>
    [Serializable]
    public class Hot
    {
        public string id, name, category;
        public double lat, lon;
        public int likes, comments, recent, dist;
        public bool fresh;   // 2주 안에 사람이 새로 올린 곳
        // 관리자가 띄운 협력 업체(광고) 줄 — 그림 이름(icon) · 둘째 줄(sub) · 누르면 열 곳(link) · 이모지 그림(emoji_url)
        public bool promo;
        public string icon, sub, link, ad_id, emoji_url;
    }

    [Serializable] private class FestResp { public bool ok; public string status; public Festival[] items; }
    [Serializable] private class ShelterResp { public bool ok; public string status; public Shelter[] items; }
    [Serializable] private class HotResp { public bool ok; public Hot[] items; }

    public static readonly List<Festival> Festivals = new List<Festival>();
    public static readonly List<Shelter> Shelters = new List<Shelter>();
    public static readonly List<Hot> HotPlaces = new List<Hot>();
    /// <summary>축제 · 보호소 · 핫플 중 하나라도 새로 받을 때마다 1씩 — 하늘 옆 장이 다시 그릴지 본다</summary>
    public static int Version { get; private set; }
    /// <summary>축제를 받은 결과 — ok · not_registered(공공데이터 신청 전) · disabled · upstream(서버가 못 받음) · offline · null(아직)</summary>
    public static string FestivalStatus { get; private set; }
    /// <summary>받은 목록이 바뀌었다 — PlaceListManager 가 다시 그린다</summary>
    public static event Action Changed;

    /// <summary>목록 줄 글자 색 (그림이 대신하지만 원본 글자에도 남긴다)</summary>
    public const string FestivalHex = "FF6B57", ShelterHex = "F5A623";

#if UNITY_EDITOR
    /// <summary>플레이 시험이 시험 서버로 돌릴 때 (비어 있으면 실제 서버)</summary>
    public static string EditorBase;
    private static string Base => string.IsNullOrEmpty(EditorBase) ? ApiConfig.MAIN_SERVER : EditorBase;

    /// <summary>플레이 시험 — 근처 줄(축제 · 보호소 · 광고)을 누르면 열 쪽을 열지 않고 여기에 적는다 (null 이면 그냥 연다)</summary>
    public static List<string> EditorOpened;

    /// <summary>플레이 시험 — 받은 것을 잊고 다음 목록 그리기 때 다시 묻게 (Changed 구독은 그대로)</summary>
    public static void EditorForget()
    {
        Festivals.Clear();
        Shelters.Clear();
        HotPlaces.Clear();
        FestivalStatus = null;
        foreach (var s in new[] { fest, shel, hot }) { s.at = s.startedAt = -9999f; s.busy = false; }
        Version++;
    }
#else
    private static string Base => ApiConfig.MAIN_SERVER;
#endif

    private const float BusyTimeout = 30f;

    private class Slot
    {
        public readonly float every, move;   // 이 시간(초)이 지났거나 이만큼(m) 움직였으면 다시 묻는다
        public float at = -9999f, startedAt = -9999f;
        public bool busy;
        public double lat, lon;
        public Slot(float every, float move) { this.every = every; this.move = move; }
    }
    // 축제 · 보호소는 30분 / 1km, 핫플은 10분 / 500m (3km 안만 보여 준다)
    private static readonly Slot fest = new Slot(1800f, 1000f), shel = new Slot(1800f, 1000f), hot = new Slot(600f, 500f);

    // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 '앱을 새로 켠 것'과 같게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        Festivals.Clear();
        Shelters.Clear();
        HotPlaces.Clear();
        FestivalStatus = null;
        Changed = null;
        foreach (var s in new[] { fest, shel, hot }) { s.at = s.startedAt = -9999f; s.busy = false; }
        seen.Clear();
        emojis.Clear();
        emojiTried.Clear();
#if UNITY_EDITOR
        EditorOpened = null;
#endif
    }

    /// <summary>축제를 물을 때가 됐으면 그 코루틴, 아니면 null</summary>
    public static IEnumerator FestivalsIfDue(double lat, double lon) => Due(fest, lat, lon) ? FetchFestivals(lat, lon) : null;

    /// <summary>보호소를 물을 때가 됐으면 그 코루틴, 아니면 null</summary>
    public static IEnumerator SheltersIfDue(double lat, double lon) => Due(shel, lat, lon) ? FetchShelters(lat, lon) : null;

    /// <summary>핫플을 물을 때가 됐으면 그 코루틴, 아니면 null (하늘 옆 장이 하늘을 볼 때 부른다)</summary>
    public static IEnumerator HotIfDue(double lat, double lon) => Due(hot, lat, lon) ? FetchHot(lat, lon) : null;

    private static bool Due(Slot s, double lat, double lon)
    {
        if (s.busy && Time.unscaledTime - s.startedAt < BusyTimeout) return false;
        return Time.unscaledTime - s.at >= s.every || Meters(lat, lon, s.lat, s.lon) >= s.move;
    }

    // promo: 광고 줄도 달라고 (축제 · 핫플) — 광고 이름 · 설명은 앱 언어로
    private static string Query(double lat, double lon, bool promo = false)
    {
        var ci = CultureInfo.InvariantCulture;
        string q = "?lat=" + lat.ToString("F5", ci) + "&lon=" + lon.ToString("F5", ci);
        return promo ? q + "&promo=1&lang=" + R0926LocalizedText.Lang() : q;
    }

    private static IEnumerator FetchFestivals(double lat, double lon)
    {
        Begin(fest, lat, lon);
        using (var req = UnityWebRequest.Get(Base + "/api/nearby/festivals" + Query(lat, lon, true)))
        {
            req.timeout = 20;
            yield return req.SendWebRequest();
            fest.busy = false;
            FestResp r = null;
            if (req.result == UnityWebRequest.Result.Success)
            {
                try { r = JsonUtility.FromJson<FestResp>(req.downloadHandler.text); }
                catch (Exception) { r = null; }
            }
            if (r == null || !r.ok)
            {
                fest.at = Time.unscaledTime - fest.every + 60f;   // 1분 뒤 다시
                if (FestivalStatus == null || Festivals.Count == 0) { FestivalStatus = "offline"; Version++; Changed?.Invoke(); }
                yield break;
            }
            Festivals.Clear();
            if (r.items != null) Festivals.AddRange(r.items);
            FestivalStatus = string.IsNullOrEmpty(r.status) ? "ok" : r.status;
            Version++;
            Changed?.Invoke();
        }
    }

    private static IEnumerator FetchShelters(double lat, double lon)
    {
        Begin(shel, lat, lon);
        using (var req = UnityWebRequest.Get(Base + "/api/nearby/shelters" + Query(lat, lon)))
        {
            req.timeout = 15;
            yield return req.SendWebRequest();
            shel.busy = false;
            ShelterResp r = null;
            if (req.result == UnityWebRequest.Result.Success)
            {
                try { r = JsonUtility.FromJson<ShelterResp>(req.downloadHandler.text); }
                catch (Exception) { r = null; }
            }
            if (r == null || !r.ok) { shel.at = Time.unscaledTime - shel.every + 60f; yield break; }
            Shelters.Clear();
            if (r.items != null) Shelters.AddRange(r.items);
            Version++;
            Changed?.Invoke();
        }
    }

    // 목록(Changed)은 핫플을 안 쓰니 Version 만 올린다
    private static IEnumerator FetchHot(double lat, double lon)
    {
        Begin(hot, lat, lon);
        using (var req = UnityWebRequest.Get(Base + "/api/nearby/hot" + Query(lat, lon, true)))
        {
            req.timeout = 15;
            yield return req.SendWebRequest();
            hot.busy = false;
            HotResp r = null;
            if (req.result == UnityWebRequest.Result.Success)
            {
                try { r = JsonUtility.FromJson<HotResp>(req.downloadHandler.text); }
                catch (Exception) { r = null; }
            }
            if (r == null || !r.ok)
            {
                // 서버가 아직 이 길을 모르면(404 — 새 서버가 뜨기 전) 30분 뒤, 그 밖은 2분 뒤
                hot.at = Time.unscaledTime - hot.every + (req.responseCode == 404 ? 1800f : 120f);
                yield break;
            }
            HotPlaces.Clear();
            if (r.items != null) HotPlaces.AddRange(r.items);
            Version++;
        }
    }

    private static void Begin(Slot s, double lat, double lon)
    {
        s.busy = true;
        s.at = s.startedAt = Time.unscaledTime;
        s.lat = lat;
        s.lon = lon;
    }

    public static double Meters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * 111320.0;
        double dLon = (lon2 - lon1) * 111320.0 * Math.Cos(lat1 * Math.PI / 180.0);
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }

    // ── 목록 줄의 거리 칸 ────────────────────────────────────

    /// <summary>축제 줄 거리 칸 — '10.30부터 · 12km' · '진행 중 · 3.4km' · '오늘까지 · 800m'</summary>
    public static string FestivalLabel(string start, string end, float meters)
    {
        return When(start, end) + " · " + Km(meters);
    }

    /// <summary>'10.30부터' · '진행 중' · '오늘까지'</summary>
    public static string When(string start, string end)
    {
        DateTime today = DateTime.Now.Date;
        if (TryDate(start, out var s) && s > today)
            return L(s.Month + "." + s.Day + "부터", "from " + s.Month + "/" + s.Day, s.Month + "/" + s.Day + "から",
                     s.Month + "/" + s.Day + "起", "desde " + s.Day + "/" + s.Month);
        if (TryDate(end, out var e) && e == today)
            return L("오늘까지", "ends today", "今日まで", "今天结束", "termina hoy");
        return L("진행 중", "on now", "開催中", "进行中", "en curso");
    }

    public static bool TryDate(string ymd, out DateTime d) =>
        DateTime.TryParseExact(ymd ?? "", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    /// <summary>350m · 12.3km · 123km</summary>
    public static string Km(float m)
    {
        if (m < 1000f) return Mathf.RoundToInt(m) + "m";
        float km = m / 1000f;
        return km < 100f ? km.ToString("0.0", CultureInfo.InvariantCulture) + "km" : Mathf.RoundToInt(km) + "km";
    }

    /// <summary>축제를 눌렀을 때 — 축제 이름으로 찾은 쪽 (날짜·장소·포스터·후기가 한 번에 보인다)</summary>
    public static string FestivalLink(string title) =>
        "https://m.search.naver.com/search.naver?query=" + UnityWebRequest.EscapeURL(title ?? "");

    // ── 광고 줄 (관리자가 띄운 협력 업체) ──────────────────────

    private static readonly Dictionary<string, Sprite> emojis = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, float> emojiTried = new Dictionary<string, float>();
    private static readonly HashSet<string> seen = new HashSet<string>();

    /// <summary>광고 줄 표시 — 협력 업체 줄은 늘 '광고'를 먼저 밝힌다</summary>
    public static string AdLabel => L("광고", "Ad", "広告", "广告", "Anuncio");

    /// <summary>광고 줄 앞 그림 — 관리자가 고른 이모지 (서버가 Noto 그림으로 준다). 아직 없으면 받기 시작하고 null — 받으면 Version 이 올라 다시 그린다</summary>
    public static Sprite Emoji(string url, MonoBehaviour host)
    {
        if (string.IsNullOrEmpty(url) || !Application.isPlaying) return null;
        if (emojis.TryGetValue(url, out var s)) return s;
        if (emojiTried.TryGetValue(url, out float at) && Time.unscaledTime - at < 60f) return null;   // 받는 중 · 못 받았으면 1분 뒤 다시
        if (host == null || !host.isActiveAndEnabled) return null;
        emojiTried[url] = Time.unscaledTime;
        host.StartCoroutine(LoadEmoji(url));
        return null;
    }

    private static IEnumerator LoadEmoji(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : Base + url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            var tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null) yield break;
            tex.wrapMode = TextureWrapMode.Clamp;
            var sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "emoji_" + url.Substring(url.LastIndexOf('/') + 1).Replace(".png", "");
            emojis[url] = sp;
        }
        Version++;
    }

    /// <summary>광고 줄을 누르면 여는 쪽 — 관리자가 적은 링크(http · https 만), 없으면 카카오맵의 그 자리</summary>
    public static string PromoLink(string link, string name, double lat, double lon)
    {
        if (!string.IsNullOrEmpty(link) && (link.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || link.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
            return link;
        var ci = CultureInfo.InvariantCulture;
        return "https://map.kakao.com/link/map/" + Uri.EscapeDataString((name ?? "").Replace(',', ' ')) + "," + lat.ToString("F6", ci) + "," + lon.ToString("F6", ci);
    }

    /// <summary>광고 줄이 보였다 — 같은 광고는 한 자리에서 앱을 켤 때마다 한 번만 센다 (친구 지도 광고 칸과 같다)</summary>
    public static void AdView(MonoBehaviour host, string adId, string placement)
    {
        if (string.IsNullOrEmpty(adId) || !Application.isPlaying || host == null || !host.isActiveAndEnabled || !seen.Add(placement + "|" + adId)) return;
        host.StartCoroutine(AdEvent(adId, placement, "view"));
    }

    /// <summary>광고 줄을 눌렀다 — 클릭을 적고 그쪽을 연다</summary>
    public static void AdClick(MonoBehaviour host, string adId, string placement, string link)
    {
        if (!string.IsNullOrEmpty(adId) && host != null && host.isActiveAndEnabled) host.StartCoroutine(AdEvent(adId, placement, "click"));
        Open(link);
    }

    /// <summary>근처 줄을 눌러 그쪽을 연다 (축제 · 보호소 · 광고)</summary>
    public static void Open(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
#if UNITY_EDITOR
        if (EditorOpened != null) { EditorOpened.Add(url); return; }
#endif
        Application.OpenURL(url);
    }

    // 노출 · 클릭 기록 (광고주 보고용 — R0926AdSlot 과 같은 /ads/api/event)
    private static IEnumerator AdEvent(string adId, string placement, string type)
    {
        string json = "{\"ad_id\":\"" + adId.Replace("\"", "").Replace("\\", "") + "\",\"placement\":\"" + placement + "\",\"type\":\"" + type + "\"}";
        using (var req = new UnityWebRequest(Base + "/ads/api/event", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 8;
            yield return req.SendWebRequest();
        }
    }

    /// <summary>빈 목록 안내 — 축제 칩을 켰는데 줄이 없을 때 (집계 줄 한 줄에 들어가게 줄바꿈 없이)</summary>
    public static string EmptyMessage()
    {
        switch (FestivalStatus)
        {
            case null:
                return L("근처 축제를 찾고 있어요…", "Looking for festivals nearby…", "近くのお祭りを探しています…", "正在查找附近的节庆…", "Buscando festivales cercanos…");
            case "ok":
                return L("근처에 열리는 축제가 아직 없어요.", "No festivals coming up nearby yet.", "近くで開かれるお祭りはまだありません。", "附近暂时没有节庆。", "Aún no hay festivales cerca.");
            case "offline":
            case "upstream":
                return L("축제 정보를 받지 못했어요. 잠시 뒤 저절로 다시 불러와요.", "Couldn't load festivals. We'll try again automatically.",
                         "お祭り情報を読み込めませんでした。しばらくすると自動で再読み込みします。", "无法加载节庆信息。稍后会自动重试。", "No se pudieron cargar los festivales. Lo intentaremos de nuevo.");
            default:
                return L("축제 정보를 준비하고 있어요. 곧 여기에서 근처 축제를 볼 수 있어요.", "Festival info is on its way. Nearby festivals will show up here soon.",
                         "お祭り情報を準備中です。もうすぐここで見られます。", "节庆信息准备中。很快就能在这里看到。", "Preparando la información de festivales. Pronto aparecerán aquí.");
        }
    }

    private static string L(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);
}
