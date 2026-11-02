using UnityEngine;

/// <summary>
/// EGM96 지오이드 높이를 계산하는 유틸리티
/// WGS84 타원체 고도 <-> 지오이드(해수면) 고도 변환
/// </summary>
public static class GeoidHeightCalculator
{
    // 한국 지역별 EGM96 지오이드 높이 (WGS84 타원체 - EGM96 지오이드)
    private static readonly GeoidRegion[] koreanGeoidHeights = new GeoidRegion[]
    {
        // 서울 및 수도권
        new GeoidRegion(37.0f, 38.0f, 126.5f, 127.5f, 25.0f),

        // 강원도
        new GeoidRegion(37.0f, 38.5f, 127.5f, 129.5f, 26.0f),

        // 충청도
        new GeoidRegion(36.0f, 37.0f, 126.0f, 128.0f, 24.0f),

        // 경상도
        new GeoidRegion(35.0f, 37.0f, 128.0f, 129.5f, 24.0f),

        // 부산/경남
        new GeoidRegion(34.5f, 36.0f, 128.0f, 129.5f, 23.0f),

        // 전라도
        new GeoidRegion(34.5f, 36.5f, 126.0f, 127.5f, 23.0f),

        // 제주도
        new GeoidRegion(33.0f, 34.0f, 126.0f, 127.0f, 20.0f),
    };

    /// <summary>
    /// 위도/경도에 따른 EGM96 지오이드 높이 반환
    /// </summary>
    /// <param name="latitude">위도 (degrees)</param>
    /// <param name="longitude">경도 (degrees)</param>
    /// <returns>지오이드 높이 (m) - WGS84 타원체가 지오이드보다 이 값만큼 높음</returns>
    public static float GetGeoidHeight(float latitude, float longitude)
    {
        // 한국 지역에서 해당하는 지오이드 높이 찾기
        foreach (var region in koreanGeoidHeights)
        {
            if (region.Contains(latitude, longitude))
            {
                return region.geoidHeight;
            }
        }

        // 기본값 (한국 평균)
        return 25.0f;
    }

    /// <summary>
    /// iOS CoreLocation 고도(지오이드 기준)를 WGS84 타원체 고도로 변환
    /// </summary>
    public static float GeoidToWGS84(float geoidAltitude, float latitude, float longitude)
    {
        float geoidHeight = GetGeoidHeight(latitude, longitude);
        return geoidAltitude + geoidHeight;
    }

    /// <summary>
    /// WGS84 타원체 고도를 지오이드 고도(해수면 기준)로 변환
    /// </summary>
    public static float WGS84ToGeoid(float wgs84Altitude, float latitude, float longitude)
    {
        float geoidHeight = GetGeoidHeight(latitude, longitude);
        return wgs84Altitude - geoidHeight;
    }

    private struct GeoidRegion
    {
        public float minLat;
        public float maxLat;
        public float minLon;
        public float maxLon;
        public float geoidHeight;

        public GeoidRegion(float minLat, float maxLat, float minLon, float maxLon, float geoidHeight)
        {
            this.minLat = minLat;
            this.maxLat = maxLat;
            this.minLon = minLon;
            this.maxLon = maxLon;
            this.geoidHeight = geoidHeight;
        }

        public bool Contains(float lat, float lon)
        {
            return lat >= minLat && lat <= maxLat && lon >= minLon && lon <= maxLon;
        }
    }
}
