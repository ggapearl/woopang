using System.Globalization;
using Google.XR.ARCoreExtensions;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// 지금 이 기기의 위치 — 장소·3D 올리기, 정보 수정 '여기로 옮기기', 보물찾기, 좌표 줄이 같이 쓴다 (10-26).
/// 차례: ARCore Geospatial(VPS — AR 앵커와 같은 기준, double) → 휴대폰 위치 + 고도 보정 → 에디터는 가상 위치.
/// 휴대폰 위치는 Unity 가 float 로만 줘서 위도 약 0.4m · 경도 약 0.7m 단위로 끊긴다 (GPS 오차 3~10m 보다 작다).
/// 저장·전송은 소수 6자리(약 0.1m), 화면은 소수 4자리(약 10m).
/// </summary>
public static class GeoHere
{
    private static AREarthManager earth;

    public static bool TryGet(out double lat, out double lon, out double alt, AREarthManager earthManager = null, bool geospatial = true)
        => TryGet(out lat, out lon, out alt, out _, earthManager, geospatial);

    /// <param name="acc">수평 정확도(m) — 모르면 -1</param>
    /// <param name="geospatial">false 면 휴대폰 위치만 (업로드 매니저의 useGeospatialAPI)</param>
    public static bool TryGet(out double lat, out double lon, out double alt, out float acc, AREarthManager earthManager = null, bool geospatial = true)
    {
#if UNITY_EDITOR
        VirtualLocation v = VirtualLocation.Instance;
        lat = v.Latitude; lon = v.Longitude; alt = v.Altitude; acc = 5f;
        return true;
#else
        AREarthManager e = !geospatial ? null : earthManager != null ? earthManager : Earth();
        if (e != null && e.EarthTrackingState == TrackingState.Tracking)
        {
            var pose = e.CameraGeospatialPose;
            lat = pose.Latitude; lon = pose.Longitude; alt = pose.Altitude; acc = (float)pose.HorizontalAccuracy;
            return true;
        }
        if (Input.location.status == LocationServiceStatus.Running)
        {
            // 켜진 직후 첫 위치가 오기 전엔 0,0 이 온다 — '아직 없음'
            LocationInfo d = Input.location.lastData;
            lat = d.latitude; lon = d.longitude; acc = d.horizontalAccuracy;
            alt = GeoidHelper.NormalizeAltitude((double)d.altitude, lat, lon);
            return !(lat == 0 && lon == 0);
        }
        lat = lon = alt = 0; acc = -1f;
        return false;
#endif
    }

    /// <summary>저장·전송 — 소수 6자리</summary>
    public static string Save(double deg) => deg.ToString("F6", CultureInfo.InvariantCulture);

    /// <summary>화면 — 소수 4자리</summary>
    public static string Show(double deg) => deg.ToString("F4", CultureInfo.InvariantCulture);

    /// <summary>두 점 사이 거리(m, Haversine) — double 로 계산한다 (float 은 36.6° 끼리 빼면 0.4m 단위로 끊긴다)</summary>
    public static double Meters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0, Rad = System.Math.PI / 180.0;
        double dLat = (lat2 - lat1) * Rad, dLon = (lon2 - lon1) * Rad;
        double a = System.Math.Sin(dLat / 2) * System.Math.Sin(dLat / 2)
                 + System.Math.Cos(lat1 * Rad) * System.Math.Cos(lat2 * Rad) * System.Math.Sin(dLon / 2) * System.Math.Sin(dLon / 2);
        return 2 * R * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1 - a));
    }

    private static AREarthManager Earth()
    {
        if (earth == null) earth = Object.FindAnyObjectByType<AREarthManager>();
        return earth;
    }
}
