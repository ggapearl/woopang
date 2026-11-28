using System.Collections.Generic;

/// <summary>
/// 지하철 · 기차역 · 터미널 정보 창 — 매니저가 오브젝트를 띄울 때와 목록 사진으로 열 때(R0926PlaceInfo) 같은 값 (11-27 23 · 24번)
/// 지하철역은 서버가 노선 · 출구 한 줄 · 첫차·막차/출구 안내 카드 주소를 붙여 준다 (server/transit_info.py · 국토교통부 TAGO)
/// </summary>
public static class FacilityInfo
{
    public static void Configure(DoubleTap3D dt, FacilityData f)
    {
        if (dt == null || f == null) return;
        dt.SetInfoImages(
            sprite1: null,
            sprite2: null,
            petFriendly: false,
            separateRestroom: false,
            description: null,
            name: f.name,
            id: -1,
            username: "WOOPANG",
            instagramId: null,
            tel: null,
            address: f.address,
            overview: f.extra_info,
            petInfo: null,
            intro: Intro(f));
        dt.SetImageUrls(Photos(f), false);   // 우팡 장소 번호가 아니다 — 우팡 서버에서 찾지 않는다
    }

    /// <summary>「노선: 1호선 · 2호선」 「출구: 1번 … · 2번 …」 — 서버가 맞춘 지하철역만 (그 밖엔 null)</summary>
    public static string Intro(FacilityData f)
    {
        if (f == null) return null;
        var sb = new System.Text.StringBuilder();
        if (f.lines != null && f.lines.Count > 0) sb.Append("노선: ").Append(string.Join(" · ", f.lines));
        if (!string.IsNullOrEmpty(f.exits)) sb.Append(sb.Length > 0 ? "\n" : "").Append("출구: ").Append(f.exits);
        if (sb.Length == 0) return null;
        sb.Append("\n출처: 국토교통부 TAGO 지하철정보");
        return sb.ToString();
    }

    /// <summary>정보 창 사진 — 첫차·막차 · 출구 안내 카드 (없으면 빈 목록)</summary>
    public static List<string> Photos(FacilityData f) =>
        f?.photos == null ? new List<string>() : f.photos.FindAll(u => !string.IsNullOrEmpty(u));
}
