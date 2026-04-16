/// <summary>
/// 모든 매니저가 공유하는 경량 캐시 데이터
/// 좌표 + 메타데이터만 저장 (이미지/모델 URL 없음)
/// FilterManager가 이 데이터를 기반으로 Full/IndicatorOnly 배분 결정
/// </summary>
[System.Serializable]
public class CachedPlaceData
{
    /// <summary>매니저 간 고유 식별자 (예: "dm_123", "subway_강남역", "tour_12345")</summary>
    public string uniqueId;

    /// <summary>원본 ID (DataManager=int, 나머지=string). 매니저에 스폰 요청 시 사용</summary>
    public string rawId;

    /// <summary>표시 이름</summary>
    public string displayName;

    public float latitude;
    public float longitude;
    public float altitude;

    /// <summary>카테고리 (shop, food, cafe, park, gov, edu 등)</summary>
    public string category;

    /// <summary>데이터 소스 매니저 ("DataManager", "TourAPI", "Subway", "Train", "Terminal")</summary>
    public string sourceManager;

    /// <summary>모델 타입 (cube, custom 등) — DataManager 전용</summary>
    public string modelType;

    /// <summary>애견동반 여부</summary>
    public bool petFriendly;

    /// <summary>필터 키 (publicData, subway, train, terminal 등)</summary>
    public string filterKey;
}
