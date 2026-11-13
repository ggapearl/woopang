using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 목록 줄 오른쪽 사진을 누르면 여는 장소 정보 창 — 그 장소를 AR 에서 두 번 누른 것과 같은 창 (DoubleTap3D).
/// 11-12 대표님 「그걸 터치하면 방향이 나오는게 아니라 해당 더블터치했을 때의 정보값이 발생하도록 하는거지(Doubletap3d 작동하도록)」.
/// AR 에 떠 있는 장소는 그 오브젝트의 창을 그대로 열고, 아직 없는 장소(멀거나 불러오는 중)는 화면 밖 대리 오브젝트(PlaceInfo0926)에
/// 그 장소를 채워 연다 — 같은 창 · 같은 글 · 같은 상세 사진 · 고치기·지우기 요청까지.
/// </summary>
public static class R0926PlaceInfo
{
    private static DoubleTap3D host;

    /// <summary>AR 에 떠 있는 그 장소의 창 — 준비를 마친 것만 (없으면 null)</summary>
    public static DoubleTap3D Spawned(PlaceListManager places, PlaceListManager.ListEntry e)
    {
        Target t = places != null ? places.FindTarget(e) : null;
        if (t == null) return null;
        var dt = t.GetComponentInParent<DoubleTap3D>();
        return dt != null && dt.isActiveAndEnabled && dt.IsReady ? dt : null;
    }

    /// <summary>AR 에 없는 장소의 창을 띄울 대리 오브젝트 — 처음 쓸 때 만든다. 장면에 창 연결을 가진 것이 없으면 null</summary>
    public static DoubleTap3D Host()
    {
        if (host == null) host = DoubleTap3D.CreateInfoHost("PlaceInfo0926");
        return host;
    }

    /// <summary>대리 오브젝트를 그 장소로 채운다 — 매니저가 오브젝트를 띄울 때와 같은 값. 모르는 자료면 false</summary>
    public static bool Fill(DoubleTap3D dt, object data)
    {
        if (dt == null || data == null) return false;
        dt.ResetData();
        var dm = DataManager.Instance;
        if (dm != null) dm.ConfigureInfo(dt, null);   // 앞서 채운 우팡 장소를 잊는다 (고치기·지우기 요청이 엉뚱한 장소로 가지 않게)
        switch (data)
        {
            case PlaceData p:
                if (dm == null) return false;
                dm.ConfigureInfo(dt, p);
                return true;
            case TourPlaceData tp:
                var tm = TourAPIManager.Instance;
                if (tm == null) return false;
                tm.ConfigureInfo(dt, tp);
                dt.SetImageUrls(tp.imageUrls ?? new List<string>(), false);   // 관광공사 번호는 우팡 장소 번호가 아니다 — 우팡 서버에서 찾지 않는다
                return true;
            case FacilityData f:
                // 지하철·기차역·터미널 — 매니저가 띄울 때와 같은 글 (상세 사진 없음)
                dt.SetInfoImages(null, null, false, false, null, f.name, -1, "WOOPANG", null, null, f.address, f.extra_info, null);
                dt.SetImageUrls(new List<string>(), false);
                return true;
        }
        return false;
    }
}
