using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 주소 찾기 — 우팡 서버의 /api/geo/address(좌표 → 주소) · /api/geo/search(주소·장소 이름 → 좌표).
/// 서버가 브이월드(국토교통부 공간정보) 키를 붙여 묻는다 — 앱은 우팡 서버만 부른다. 국내 주소·한국어만.
/// 받은 주소는 화면에 보여 주기만 하고 저장하지 않는다 (브이월드 약관: 실시간으로만).
/// </summary>
public static class R0926Geo
{
    [Serializable]
    public class Address
    {
        public bool ok, found;
        public string region;   // 날씨판과 같은 '시군구 읍면동' ('예산군 대흥면')
        public string line;     // 도크 위 한 줄 — 주소의 끝 두 단계 ('대흥면 지곡리')
        public string brief;    // 핀 말풍선 — 시·도만 뺀 주소 ('중구 세종대로 110')
        public string road, parcel;
    }

    [Serializable]
    public class Place
    {
        public string title, address, kind;
        public double lat, lon;
    }

    [Serializable] private class SearchResp { public bool ok; public Place[] items; }

#if UNITY_EDITOR
    /// <summary>플레이 시험이 시험 서버로 돌릴 때 (비어 있으면 실제 서버)</summary>
    public static string EditorBase;
    private static string Base => string.IsNullOrEmpty(EditorBase) ? ApiConfig.MAIN_SERVER : EditorBase;
#else
    private static string Base => ApiConfig.MAIN_SERVER;
#endif

    /// <summary>서버가 주소를 찾아 주는 범위 (대한민국) — 밖이면 묻지 않는다</summary>
    public static bool InKorea(double lat, double lon) => lat >= 32.8 && lat <= 39.0 && lon >= 124.0 && lon <= 132.2;

    /// <summary>좌표 → 주소. 못 찾았거나 서버가 안 되면 null (부르는 쪽이 다른 길로)</summary>
    public static IEnumerator FindAddress(double lat, double lon, Action<Address> done)
    {
        if (!InKorea(lat, lon)) { done(null); yield break; }
        var ci = CultureInfo.InvariantCulture;
        using (var req = UnityWebRequest.Get(Base + "/api/geo/address?lat=" + lat.ToString("F6", ci) + "&lon=" + lon.ToString("F6", ci)))
        {
            req.timeout = 8;
            LoginManager.ApplyAuth(req);
            yield return req.SendWebRequest();
            Address a = null;
            if (req.result == UnityWebRequest.Result.Success)
            {
                try { a = JsonUtility.FromJson<Address>(req.downloadHandler.text); }
                catch (Exception) { a = null; }
            }
            done(a != null && a.ok && a.found ? a : null);
        }
    }

    /// <summary>
    /// 주소·장소 이름 → 좌표 (가까운 곳·이름이 똑같은 곳부터, 많아야 다섯). near 가 있으면 그 둘레를 먼저 찾는다.
    /// done(결과, 서버가 답했나) — 서버가 안 되면 (null, false), 못 찾았으면 (빈 배열, true)
    /// </summary>
    public static IEnumerator Search(string q, Vector2? near, Action<Place[], bool> done)
    {
        var ci = CultureInfo.InvariantCulture;
        string url = Base + "/api/geo/search?q=" + UnityWebRequest.EscapeURL(q);
        if (near.HasValue) url += "&near_lat=" + near.Value.x.ToString("F4", ci) + "&near_lon=" + near.Value.y.ToString("F4", ci);
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            LoginManager.ApplyAuth(req);
            yield return req.SendWebRequest();
            SearchResp r = null;
            if (req.result == UnityWebRequest.Result.Success)
            {
                try { r = JsonUtility.FromJson<SearchResp>(req.downloadHandler.text); }
                catch (Exception) { r = null; }
            }
            if (r == null || !r.ok) done(null, false);
            else done(r.items ?? new Place[0], true);
        }
    }
}
