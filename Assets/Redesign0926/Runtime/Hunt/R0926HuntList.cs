using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SimpleJSON;
using UnityEngine;

/// <summary>
/// 장소 목록의 보물찾기 줄 — 내가 연 게임과 참가 중인 게임 (10-13 대표님: "게임을 참석한 사람도 목록에서 게임이벤트가 보이도록 …
/// 동그라미가 아닌 보물모양 이모지가 앞에 있고 '이벤트명' 이 있도록 해서 근처 몇미터에 있는지").
/// 자리는 게임의 공개 설정대로만 쓴다 (카드 상세의 '나와의 거리'와 같다) —
///  · 정확히 공개 · 내가 연 게임: 그 자리까지 거리 (걸으면 바뀐다)
///  · 대략 공개: 원 가운데까지 100m 단위 '약' · 원 안이면 '범위 안'
///  · 비공개: 서버가 준 굵은 거리('1km 이내')만 — 방향도 없다
/// AR 보물 자리(/near 가 보이는 거리에서만 주는 것)는 쓰지 않는다 — 목록에 뜨면 위치가 드러난다 (R0926HuntTreasure).
/// 목록이 열려 있을 때만 1분에 한 번 묻는다 (PlaceListManager 가 부른다). 카드 처음 화면이 받은 목록도 그대로 받는다.
/// 찾았거나 포기한 게임 · 끝난 게임 · 보물찾기 참여를 꺼 둔 동안 · 로그아웃이면 빈 목록.
/// </summary>
public static class R0926HuntList
{
    public enum Coord : byte { Exact, Approx, Hidden }

    public struct Spot
    {
        public int id;
        public string title;
        public Coord coord;
        public double lat, lon;   // Exact: 그 자리 · Approx: 원 가운데 · Hidden: 없음
        public float approxM;     // Approx: 원 반지름
        public float bandM;       // Hidden: 굵은 거리의 끝 (1km · 5km · 20km)
        public float bandFromM;   // Hidden: 굵은 거리의 시작 — 거리 슬라이더가 이보다 크면 목록에 넣는다
        public bool locked;       // 비밀번호를 건 보물 — 목록 그림에 자물쇠
    }

    /// <summary>목록 줄 이름 앞의 금빛 상자 색 (글자 줄의 색 — 그림이 대신하지만 원본 글자에도 남긴다)</summary>
    public const string ColorHex = "E8BF4F";

    public static readonly List<Spot> Spots = new List<Spot>();
    /// <summary>목록이 바뀌었다 — PlaceListManager 가 다시 그린다</summary>
    public static event Action Changed;

    /// <summary>
    /// 하늘 옆 장 '근처 보물찾기' — 내 범위 안에서 진행 중 · 곧 시작하는 남의 게임 (아직 못 찾음 · 포기 안 함 · 자리가 남았거나 이미 참가).
    /// 새 게임 알림을 못 받은 사람도 여기서 보고 참가한다 (10-23 대표님). 순서는 서버 그대로 (공식 · 진행 중 · 가까운 순)
    /// </summary>
    public struct Near
    {
        public int id;
        public string title, prize;
        public bool live, joined, official, locked;
        public float meters;          // 정확히 · 대략 공개 게임의 거리 — 비공개면 -1
        public float bandM;           // 비공개 게임의 굵은 거리 끝 (1km · 5km · 20km) — 모르면 0
        public int players, maxPlayers;   // maxPlayers 0 = 제한 없음
        public DateTime startsAt;     // 곧 시작 — 이 기기 시각 (모르면 MinValue)
    }
    public static readonly List<Near> Nearby = new List<Near>();
    /// <summary>Nearby 가 바뀔 때마다 1씩 — 하늘 옆 장이 다시 그릴지 본다</summary>
    public static int NearbyVersion { get; private set; }
    private static string nearSig = "";

    private const float RefreshEvery = 60f;
    private static float fetchedAt = -999f, fetchStartedAt = -999f;
    private static bool fetching;   // 묻는 중 — 목록이 꺼져 코루틴이 멈추면 풀리지 않으니 30초 뒤엔 다시 묻는다
    private static string sig = "";
    private static readonly StringBuilder sb = new StringBuilder(256);

    // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 '앱을 새로 켠 것'과 같게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        Spots.Clear();
        Changed = null;
        fetchedAt = fetchStartedAt = -999f;
        fetching = false;
        sig = "";
        Nearby.Clear();
        nearSig = "";
    }

    /// <summary>목록을 그릴 때 PlaceListManager 가 부른다 — 1분이 지났으면 다시 물을 코루틴, 아니면 null</summary>
    public static IEnumerator RefreshIfDue()
    {
        if (fetching && Time.unscaledTime - fetchStartedAt < 30f) return null;
        if (Time.unscaledTime - fetchedAt < RefreshEvery) return null;
        if (!R0926HuntApi.CanCall) { Set(null); return null; }
        return Fetch();
    }

    /// <summary>참가 · 나가기 · 새 게임 — 다음에 목록을 그릴 때 바로 다시 묻는다</summary>
    public static void Stale() => fetchedAt = -999f;

    private static IEnumerator Fetch()
    {
        fetching = true;
        fetchedAt = fetchStartedAt = Time.unscaledTime;
        string q = R0926HuntCard.TryLocation(out float lat, out float lon, out _)
            ? "?lat=" + lat.ToString("F6", CultureInfo.InvariantCulture) + "&lon=" + lon.ToString("F6", CultureInfo.InvariantCulture)
            : "";
        long code = 0;
        JSONNode j = null;
        yield return R0926HuntApi.Get("/api/hunts/nearby" + q, (c, r) => { code = c; j = r; });
        fetching = false;
        if (code == 200) Take(j);
        else if (code == 401) Set(null);
        // 그 밖(끊김 등)은 지난 목록을 그대로 둔다 — 1분 뒤 다시
    }

    /// <summary>/api/hunts/nearby 응답 (카드 처음 화면도 넘겨준다)</summary>
    public static void Take(JSONNode j)
    {
        if (j == null) return;
        fetchedAt = Time.unscaledTime;
        var p = j["prefs"];
        if (p.HasKey("enabled") && !p["enabled"].AsBool) { Set(null); return; }   // 보물찾기 참여를 꺼 두었다
        var list = new List<Spot>();
        var seen = new HashSet<int>();
        foreach (var key in new[] { "hosted", "joined" })
            foreach (JSONNode h in j[key].AsArray)
                if (seen.Add(h["id"].AsInt) && Playing(h) && TryMake(h, out var s)) list.Add(s);
        Set(list);
        var near = new List<Near>();
        foreach (JSONNode h in j["hunts"].AsArray)
            if (TryNear(h, out var n)) near.Add(n);
        SetNearby(near);
    }

    private static bool TryNear(JSONNode h, out Near n)
    {
        n = default;
        string st = h["status"];
        if ((st != "live" && st != "scheduled") || h["is_host"].AsBool) return false;
        var me = h["me"];
        bool joined = me["joined"].AsBool;
        if (me["rank"].AsInt > 0 || me["gave_up"].AsBool || (!joined && h["full"].AsBool)) return false;
        string title = OneLine(h["title"]);
        n.id = h["id"].AsInt;
        n.title = title.Length > 0 ? title : R0926LocalizedText.Say("보물찾기", "Treasure hunt", "宝探し", "寻宝", "Búsqueda del tesoro");
        n.prize = OneLine(h["prize"]);
        n.live = st == "live";
        n.joined = joined;
        n.official = h["official"].AsBool;
        n.locked = h["mode"] == "code";
        n.meters = Has(h, "distance_m") ? h["distance_m"].AsFloat : -1f;
        n.bandM = n.meters < 0f ? BandKm(h["distance_text"]) * 1000f : 0f;
        n.players = h["participants"].AsInt;
        n.maxPlayers = Has(h, "max_players") ? h["max_players"].AsInt : 0;
        n.startsAt = DateTimeOffset.TryParse((string)h["starts_at"] ?? "", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at.LocalDateTime : DateTime.MinValue;
        return true;
    }

    private static string OneLine(string s) => (s ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();

    // '1km 이내' · '5km 이내' · '20km 이내' → 1 · 5 · 20, '20km 넘게' · 없음 → 0
    private static int BandKm(string t)
    {
        if (string.IsNullOrEmpty(t) || t.Contains("넘게")) return 0;
        int km = 0;
        foreach (char c in t)
        {
            if (c >= '0' && c <= '9') km = km * 10 + (c - '0');
            else if (km > 0) break;
        }
        return km;
    }

    private static void SetNearby(List<Near> list)
    {
        sb.Length = 0;
        if (list != null)
            foreach (var n in list)
                sb.Append(n.id).Append('|').Append(n.title).Append('|').Append(n.prize).Append('|').Append(n.live).Append(n.joined).Append(n.locked)
                  .Append('|').Append(Mathf.RoundToInt(n.meters / 10f)).Append('|').Append(n.bandM).Append('|').Append(n.players).Append('/').Append(n.maxPlayers)
                  .Append('|').Append(n.startsAt.Ticks).Append(';');
        string now = sb.ToString();
        if (now == nearSig) return;
        nearSig = now;
        Nearby.Clear();
        if (list != null) Nearby.AddRange(list);
        NearbyVersion++;
    }

    // 시작 전·진행 중이고, 내가 열었거나 참가해서 아직 못 찾음·포기 안 함 (R0926HuntTreasure.Track 과 같은 기준 + 포기)
    private static bool Playing(JSONNode h)
    {
        string st = h["status"];
        if (st != "live" && st != "scheduled") return false;
        if (h["is_host"].AsBool) return true;
        var me = h["me"];
        return me["joined"].AsBool && me["rank"].AsInt <= 0 && !me["gave_up"].AsBool;
    }

    private static bool TryMake(JSONNode h, out Spot s)
    {
        // 목록은 한 줄에 한 곳 — 제목의 줄바꿈은 띄어쓰기로
        string title = ((string)h["title"] ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        s = new Spot { id = h["id"].AsInt, title = title.Length > 0 ? title : R0926LocalizedText.Say("보물찾기", "Treasure hunt", "宝探し", "寻宝", "Búsqueda del tesoro"), locked = h["mode"] == "code" };
        if (Has(h, "lat") && Has(h, "lon"))
        {
            s.coord = Coord.Exact;
            s.lat = h["lat"].AsDouble;
            s.lon = h["lon"].AsDouble;
            return true;
        }
        if (Has(h, "approx_lat") && Has(h, "approx_lon"))
        {
            s.coord = Coord.Approx;
            s.lat = h["approx_lat"].AsDouble;
            s.lon = h["approx_lon"].AsDouble;
            s.approxM = h["approx_m"].AsFloat;
            return true;
        }
        // 비공개 — 서버가 내 위치로 잰 굵은 거리 ('1km 이내' · '5km 이내' · '20km 이내' · '20km 넘게'). 위치를 못 보냈으면 없다
        int km = BandKm(h["distance_text"]);
        if (km <= 0) return false;
        s.coord = Coord.Hidden;
        s.bandM = km * 1000f;
        s.bandFromM = km <= 1 ? 0f : km <= 5 ? 1000f : 5000f;
        return true;
    }

    private static bool Has(JSONNode h, string key) => h.HasKey(key) && !h[key].IsNull;

    private static void Set(List<Spot> list)
    {
        if (list == null) SetNearby(null);   // 로그아웃 · 참여 끔 — 하늘 옆 장도 비운다
        sb.Length = 0;
        if (list != null)
            foreach (var s in list)
                sb.Append(s.id).Append('|').Append(s.title).Append('|').Append((int)s.coord).Append('|')
                  .Append(s.lat.ToString("F6", CultureInfo.InvariantCulture)).Append('|').Append(s.lon.ToString("F6", CultureInfo.InvariantCulture))
                  .Append('|').Append(s.approxM).Append('|').Append(s.bandM).Append('|').Append(s.locked).Append(';');
        string now = sb.ToString();
        if (now == sig) return;
        sig = now;
        Spots.Clear();
        if (list != null) Spots.AddRange(list);
        Changed?.Invoke();
    }

    // ── 목록 줄의 거리 칸 ────────────────────────────────────
    /// <summary>숫자 대신 쓸 말 — 정확히 공개면 null (숫자 그대로)</summary>
    public static string Label(Coord c, float meters, float approxM, float bandM)
    {
        switch (c)
        {
            case Coord.Approx:
                if (meters <= approxM) return L("범위 안", "In area", "範囲内", "范围内", "En la zona");
                return L("약 ", "about ", "約", "约", "aprox. ") + Km(Mathf.Max(100f, Mathf.Round(meters / 100f) * 100f));
            case Coord.Hidden:
                string km = Mathf.RoundToInt(bandM / 1000f) + "km";
                return L(km + " 이내", "within " + km, km + "以内", km + "以内", "a menos de " + km);
            default:
                return null;
        }
    }

    /// <summary>방향(앞·오른쪽 …)을 보이지 않는 줄 — 비공개 게임 · 대략 원 안</summary>
    public static bool NoDirection(Coord c, float meters, float approxM) =>
        c == Coord.Hidden || (c == Coord.Approx && meters <= approxM);

    private static string Km(float m) => m >= 1000f ? (m / 1000f).ToString("0.0") + "km" : Mathf.RoundToInt(m) + "m";

    private static string L(string ko, string en, string ja, string zh, string es)
    {
        switch (R0926LocalizedText.Lang())
        {
            case "ko": return ko;
            case "ja": return ja;
            case "zh": return zh;
            case "es": return es;
            default: return en;
        }
    }
}
