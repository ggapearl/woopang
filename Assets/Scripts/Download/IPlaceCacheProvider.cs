using System.Collections.Generic;

/// <summary>
/// FilterManager가 각 매니저에서 캐시 데이터를 수집하고
/// Full/IndicatorOnly 스폰을 지시하기 위한 인터페이스
/// DataManager, TourAPIManager, SubwayManager, TrainStationManager, TerminalManager가 구현
/// </summary>
public interface IPlaceCacheProvider
{
    /// <summary>현재 캐시된 경량 데이터 목록 반환</summary>
    List<CachedPlaceData> GetCachedPlaces();

    /// <summary>Full 3D 오브젝트 스폰 (이미지 다운로드 포함). 이미 스폰된 경우 무시</summary>
    bool SpawnFullObject(string rawId);

    /// <summary>IndicatorOnly 경량 오브젝트 스폰 (Target + Anchor만). 이미 스폰된 경우 무시</summary>
    bool SpawnIndicatorOnly(string rawId);

    /// <summary>Full 오브젝트 디스폰 (풀로 반환)</summary>
    void DespawnFullObject(string rawId);

    /// <summary>IndicatorOnly 오브젝트 디스폰</summary>
    void DespawnIndicatorOnly(string rawId);

    /// <summary>현재 스폰된 Full 오브젝트 ID 목록</summary>
    HashSet<string> GetSpawnedFullIds();

    /// <summary>현재 스폰된 IndicatorOnly ID 목록</summary>
    HashSet<string> GetSpawnedIndicatorIds();

    /// <summary>캐시 갱신 요청 (5km 이동 등)</summary>
    void RefreshCache(float lat, float lon);

    /// <summary>이 매니저의 필터 키 ("publicData", "subway", "train", "terminal")</summary>
    string FilterKey { get; }

    /// <summary>최대 캐시 개수</summary>
    int MaxCacheSize { get; }

    /// <summary>캐시 데이터가 준비되었는지 여부</summary>
    bool IsCacheReady { get; }
}
