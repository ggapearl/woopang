using UnityEngine;

/// <summary>
/// iOS WGS84 타원체 고도 → MSL(Mean Sea Level) 고도 변환 유틸리티
/// Android는 MSL 고도를 직접 제공하지만, iOS는 WGS84 타원체 고도를 제공하므로
/// Geoid 높이 오프셋을 더해 Android와 동일한 기준(MSL)으로 통일
/// </summary>
public static class GeoidHelper
{
    /// <summary>
    /// iOS에서 받은 WGS84 타원체 고도를 MSL 고도로 변환
    /// Android에서는 그대로 반환
    /// </summary>
    public static float NormalizeAltitude(float rawAltitude, float latitude, float longitude)
    {
#if UNITY_IOS && !UNITY_EDITOR
        return rawAltitude + GetGeoidOffset(latitude, longitude);
#else
        return rawAltitude;
#endif
    }

    /// <summary>
    /// double 오버로드
    /// </summary>
    public static double NormalizeAltitude(double rawAltitude, double latitude, double longitude)
    {
#if UNITY_IOS && !UNITY_EDITOR
        return rawAltitude + (double)GetGeoidOffset((float)latitude, (float)longitude);
#else
        return rawAltitude;
#endif
    }

    /// <summary>
    /// 위도/경도 기반 Geoid 높이 오프셋 반환 (iOS WGS84 → MSL 변환용)
    /// 전세계 주요 지역별 평균 Geoid 높이 적용
    /// </summary>
    public static float GetGeoidOffset(float lat, float lon)
    {
        // 동아시아
        if (lat >= 30f && lat <= 45f && lon >= 120f && lon <= 145f)
        {
            // 한국, 일본, 중국 동부
            if (lat >= 33f && lat <= 43f && lon >= 126f && lon <= 142f)
                return 30f; // 일본: ~35-40m, 한국: ~20-25m, 평균 30m
            else
                return 25f; // 중국 동부
        }
        // 동남아시아
        else if (lat >= -10f && lat <= 25f && lon >= 95f && lon <= 140f)
        {
            return 15f; // 태국, 베트남, 필리핀, 인도네시아
        }
        // 남아시아 (인도)
        else if (lat >= 8f && lat <= 35f && lon >= 68f && lon <= 97f)
        {
            return 20f; // 인도
        }
        // 북미 서부
        else if (lat >= 30f && lat <= 60f && lon >= -130f && lon <= -110f)
        {
            return -25f; // 미국 서부, 캐나다 서부
        }
        // 북미 동부
        else if (lat >= 25f && lat <= 50f && lon >= -100f && lon <= -65f)
        {
            return -30f; // 미국 동부, 캐나다 동부
        }
        // 유럽
        else if (lat >= 35f && lat <= 70f && lon >= -10f && lon <= 40f)
        {
            return 45f; // 서유럽/동유럽 평균
        }
        // 호주
        else if (lat >= -45f && lat <= -10f && lon >= 110f && lon <= 155f)
        {
            return 10f; // 호주
        }
        // 남미
        else if (lat >= -55f && lat <= 13f && lon >= -82f && lon <= -34f)
        {
            return 5f; // 브라질, 아르헨티나 등
        }
        // 아프리카
        else if (lat >= -35f && lat <= 37f && lon >= -20f && lon <= 52f)
        {
            return 15f; // 아프리카 대륙
        }
        // 중동
        else if (lat >= 12f && lat <= 42f && lon >= 35f && lon <= 65f)
        {
            return 25f; // 중동 지역
        }
        // 기본값 (기타 지역)
        else
        {
            return 20f; // 전세계 평균 약 20m
        }
    }
}
