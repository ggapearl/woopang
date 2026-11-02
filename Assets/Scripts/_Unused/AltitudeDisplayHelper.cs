using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사용자에게 고도를 표시할 때 지오이드(해수면) 기준으로 변환
/// AR 내부적으로는 WGS84 사용, 사용자 UI에는 친숙한 해발 고도 표시
/// </summary>
public class AltitudeDisplayHelper : MonoBehaviour
{
    /// <summary>
    /// WGS84 고도를 사용자 친화적인 해발 고도로 변환
    /// </summary>
    public static string FormatAltitudeForDisplay(float wgs84Altitude, float latitude, float longitude)
    {
        // WGS84를 지오이드(해발 고도)로 변환
        float geoidAltitude = GeoidHeightCalculator.WGS84ToGeoid(wgs84Altitude, latitude, longitude);
        return $"{geoidAltitude:F1}m";
    }

    /// <summary>
    /// 좌표 정보를 사용자 친화적 형식으로 포맷
    /// </summary>
    public static string FormatLocationForDisplay(float latitude, float longitude, float wgs84Altitude)
    {
        float geoidAltitude = GeoidHeightCalculator.WGS84ToGeoid(wgs84Altitude, latitude, longitude);
        return $"위도: {latitude:F6}\n경도: {longitude:F6}\n고도: {geoidAltitude:F1}m (해발)";
    }
}
