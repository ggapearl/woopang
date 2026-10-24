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
/// 목록이 열려 있을 때 PlaceListManager 가 부른다 — 1km 넘게 움직였거나 30분이 지났을 때만 다시 묻고, 받으면 Changed.
/// </summary>
public static class R0926Nearby
{
    [Serializable]
    public class Festival
    {
        public string id, title, start, end, addr, tel, image, icon;   // start·end: YYYYMMDD
        public double lat, lon;
        public int dist;
    }

    [Serializable]
    public class Shelter
    {
        public string id, name, addr, tel, url;   // url: 카카오맵 장소 쪽 (전화·길찾기)
        public double lat, lon;
        public int dist;
    }

    [Serializable] private class FestResp { public bool ok; public string status; public Festival[] items; }
    [Serializable] private class ShelterResp { public bool ok; public string status; public Shelter[] items; }

    public static readonly List<Festival> Festivals = new List<Festival>();
    public static readonly List<Shelter> Shelters = new List<Shelter>();
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

    /// <summary>플레이 시험 — 받은 것을 잊고 다음 목록 그리기 때 다시 묻게 (Changed 구독은 그대로)</summary>
    public static void EditorForget()
    {
        Festivals.Clear();
        Shelters.Clear();
        FestivalStatus = null;
        foreach (var s in new[] { fest, shel }) { s.at = s.startedAt = -9999f; s.busy = false; }
    }
#else
    private static string Base => ApiConfig.MAIN_SERVER;
#endif

    private const float RefreshEvery = 1800f, MoveMeters = 1000f, BusyTimeout = 30f;

    private class Slot
    {
        public float at = -9999f, startedAt = -9999f;
        public bool busy;
        public double lat, lon;
    }
    private static readonly Slot fest = new Slot(), shel = new Slot();

    // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 '앱을 새로 켠 것'과 같게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        Festivals.Clear();
        Shelters.Clear();
        FestivalStatus = null;
        Changed = null;
        foreach (var s in new[] { fest, shel }) { s.at = s.startedAt = -9999f; s.busy = false; }
    }

    /// <summary>축제를 물을 때가 됐으면 그 코루틴, 아니면 null</summary>
    public static IEnumerator FestivalsIfDue(double lat, double lon) => Due(fest, lat, lon) ? FetchFestivals(lat, lon) : null;

    /// <summary>보호소를 물을 때가 됐으면 그 코루틴, 아니면 null</summary>
    public static IEnumerator SheltersIfDue(double lat, double lon) => Due(shel, lat, lon) ? FetchShelters(lat, lon) : null;

    private static bool Due(Slot s, double lat, double lon)
    {
        if (s.busy && Time.unscaledTime - s.startedAt < BusyTimeout) return false;
        return Time.unscaledTime - s.at >= RefreshEvery || Meters(lat, lon, s.lat, s.lon) >= MoveMeters;
    }

    private static string Query(double lat, double lon)
    {
        var ci = CultureInfo.InvariantCulture;
        return "?lat=" + lat.ToString("F5", ci) + "&lon=" + lon.ToString("F5", ci);
    }

    private static IEnumerator FetchFestivals(double lat, double lon)
    {
        Begin(fest, lat, lon);
        using (var req = UnityWebRequest.Get(Base + "/api/nearby/festivals" + Query(lat, lon)))
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
                fest.at = Time.unscaledTime - RefreshEvery + 60f;   // 1분 뒤 다시
                if (FestivalStatus == null || Festivals.Count == 0) { FestivalStatus = "offline"; Changed?.Invoke(); }
                yield break;
            }
            Festivals.Clear();
            if (r.items != null) Festivals.AddRange(r.items);
            FestivalStatus = string.IsNullOrEmpty(r.status) ? "ok" : r.status;
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
            if (r == null || !r.ok) { shel.at = Time.unscaledTime - RefreshEvery + 60f; yield break; }
            Shelters.Clear();
            if (r.items != null) Shelters.AddRange(r.items);
            Changed?.Invoke();
        }
    }

    private static void Begin(Slot s, double lat, double lon)
    {
        s.busy = true;
        s.at = s.startedAt = Time.unscaledTime;
        s.lat = lat;
        s.lon = lon;
    }

    private static double Meters(double lat1, double lon1, double lat2, double lon2)
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

    private static string When(string start, string end)
    {
        DateTime today = DateTime.Now.Date;
        if (TryDate(start, out var s) && s > today)
            return L(s.Month + "." + s.Day + "부터", "from " + s.Month + "/" + s.Day, s.Month + "/" + s.Day + "から",
                     s.Month + "/" + s.Day + "起", "desde " + s.Day + "/" + s.Month);
        if (TryDate(end, out var e) && e == today)
            return L("오늘까지", "ends today", "今日まで", "今天结束", "termina hoy");
        return L("진행 중", "on now", "開催中", "进行中", "en curso");
    }

    private static bool TryDate(string ymd, out DateTime d) =>
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
