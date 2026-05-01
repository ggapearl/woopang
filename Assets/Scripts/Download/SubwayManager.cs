using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Google.XR.ARCoreExtensions;
using Google.XR.ARCoreExtensions.GeospatialCreator;
using UnityEngine.UI;
using System.Text;
using System.Linq;
using UnityEngine.XR.ARFoundation;

public class SubwayManager : MonoBehaviour, IPlaceCacheProvider
{
    private static SubwayManager instance;
    public static SubwayManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindFirstObjectByType<SubwayManager>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private string BASE_URL => ApiConfig.NEARBY_FACILITIES;
    private const string SERVICE_KEY = "teLNDctkJ9YFlMFaPWTqqwgtgxvewuaqm53dhSOiNpfOV1Q4z8NxyhhvpW4ifx3eKhI8RgodlQ05pxVHAeh1sA==";
    private readonly string apiUrlTemplate = "{0}?lat={1}&lon={2}&radius={3}&type=subway";

    public GameObject samplePrefab;

    private Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>(100);
    private Dictionary<string, FacilityData> placeDataMap = new Dictionary<string, FacilityData>(100);
    private Dictionary<string, bool> currentFilters;
    private Queue<GameObject> objectPool = new Queue<GameObject>(20);

    [SerializeField] public int poolSize = 50;

    [Header("Progressive Loading Settings")]
    [Tooltip("거리별 로딩 단계 (미터)")]
    public float[] loadRadii = new float[] { 1000f, 5000f, 10000f };

    [Tooltip("각 거리 단계 사이의 딜레이 (초)")]
    public float tierDelay = 0.5f;

    [Tooltip("같은 단계 내 오브젝트 사이의 딜레이 (초)")]
    public float objectSpawnDelay = 0.1f;

    [SerializeField] private float updateDistanceThreshold = 50f;

    [Header("Object Spawn Radius")]
    [Tooltip("이 거리(m) 이내만 3D 오브젝트 생성. 밖은 좌표만 저장")]
    [SerializeField] private float objectSpawnRadius = 400f;

    private bool isDataLoaded = false;
    private Coroutine fetchCoroutine;
    private Vector2 lastPosition;

    // Light Cache (FilterManager 중앙 배분용)
    private List<CachedPlaceData> lightCache = new List<CachedPlaceData>();
    private bool isCacheReady = false;
    [Header("IndicatorOnly (FilterManager 배분 전용)")]
    [Tooltip("IndicatorOnly 프리팹 — FilterManager가 중앙 배분")]
    [SerializeField] private GameObject indicatorOnlyPrefab;
    private Dictionary<string, GameObject> indicatorOnlyObjects = new Dictionary<string, GameObject>();
    private Queue<GameObject> indicatorOnlyPool = new Queue<GameObject>(20);

    void Start()
    {
        // PlayerPrefs에서 필터 상태 초기화 (DataManager 패턴)
        currentFilters = new Dictionary<string, bool>();
        currentFilters["subway"] = PlayerPrefs.GetInt("Filter_Subway_V2", 1) == 1;

        InitializeObjectPool();
        StartCoroutine(StartLocationServiceAndFetchData());

        FilterManager filterMgr = Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null) filterMgr.RegisterCacheProvider(this);
    }

    private void InitializeObjectPool()
    {
        if (samplePrefab == null) return;
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
            obj.SetActive(false);
            objectPool.Enqueue(obj);
        }
    }

    private IEnumerator StartLocationServiceAndFetchData()
    {
        // 초기 DB 로드는 FilterManager.RefreshAllCaches()가 중앙 처리 (중복 페치 방지)
        // 여기서는 위치 서비스만 준비
#if UNITY_EDITOR
        lastPosition = new Vector2(VirtualLocation.Instance.Latitude, VirtualLocation.Instance.Longitude);
        StartCoroutine(TrackPosition());
        yield break;
#else
        if (!Input.location.isEnabledByUser) yield break;
        Input.location.Start();
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1f);
            maxWait--;
        }
        if (Input.location.status == LocationServiceStatus.Failed) yield break;

        lastPosition = new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        StartCoroutine(TrackPosition());
#endif
    }

    // 위치 추적 전용 (DB 재요청/스폰은 FilterManager가 중앙 처리)
    private IEnumerator TrackPosition()
    {
        while (true)
        {
#if UNITY_EDITOR
            float lat = VirtualLocation.Instance.Latitude;
            float lon = VirtualLocation.Instance.Longitude;
#else
            LocationInfo currentLocation = Input.location.lastData;
            float lat = currentLocation.latitude;
            float lon = currentLocation.longitude;
#endif
            Vector2 currentPos = new Vector2(lat, lon);
            float distanceMoved = Vector2.Distance(lastPosition, currentPos) * 111000f;

            if (distanceMoved > updateDistanceThreshold)
            {
                lastPosition = currentPos;
            }

            yield return new WaitForSeconds(5f);
        }
    }

    private IEnumerator FetchFacilityData(float latitude, float longitude, float radius)
    {
        string url = string.Format(apiUrlTemplate, BASE_URL, latitude, longitude, radius);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                yield return StartCoroutine(ProcessFacilityData(json, latitude, longitude));
            }
            else
            {
                Debug.LogError($"[SubwayManager] API request failed: {request.error}");
            }
        }
    }

    private void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        objectPool.Enqueue(obj);
    }

    private IEnumerator ProcessFacilityData(string json, float latitude, float longitude)
    {
        List<FacilityData> facilities = null;
        try
        {
            facilities = JsonConvert.DeserializeObject<List<FacilityData>>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SubwayManager] JSON parsing failed: {ex.Message}");
            yield break;
        }

        if (facilities == null || facilities.Count == 0)
        {
            // 빈 결과여도 캐시 준비 완료로 표시 (로딩 중 vs 0개 구분)
            lightCache.Clear();
            isCacheReady = true;
            yield break;
        }

        // Light 캐시 갱신
        lightCache.Clear();
        foreach (var f in facilities)
        {
            string uid = "subway_" + f.name + "_" + f.latitude + "_" + f.longitude;
            lightCache.Add(new CachedPlaceData
            {
                uniqueId = uid,
                rawId = f.name + "_" + f.latitude + "_" + f.longitude,
                displayName = f.name ?? "",
                latitude = (float)f.latitude,
                longitude = (float)f.longitude,
                altitude = f.altitude,
                category = "subway",
                sourceManager = "Subway",
                modelType = "cube",
                petFriendly = false,
                filterKey = "subway"
            });
        }
        // 최대 100개로 제한 (거리순 정렬 상태)
        if (lightCache.Count > MaxCacheSize) lightCache.RemoveRange(MaxCacheSize, lightCache.Count - MaxCacheSize);
        isCacheReady = true;

        foreach (var data in facilities)
        {
            string uniqueId = data.name + "_" + data.latitude + "_" + data.longitude;
            float dist = CalculateDistance(latitude, longitude, (float)data.latitude, (float)data.longitude);

            // 모든 데이터는 placeDataMap에 저장 (좌표/메타데이터)
            placeDataMap[uniqueId] = data;

            if (spawnedObjects.ContainsKey(uniqueId))
            {
                // 기존 오브젝트: 필터/거리에 따라 활성화 토글
                GameObject existing = spawnedObjects[uniqueId];
                bool shouldShow = currentFilters == null || !currentFilters.ContainsKey("subway") || currentFilters["subway"];
                if (shouldShow)
                {
                    float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
                    if (dist > maxDist) shouldShow = false;
                }
                if (shouldShow && !existing.activeSelf) existing.SetActive(true);
                else if (!shouldShow && existing.activeSelf) existing.SetActive(false);
            }
            else if (dist <= objectSpawnRadius)
            {
                // objectSpawnRadius 이내만 3D 오브젝트 생성
                GameObject newObj = GetFromPool();
                if (newObj != null)
                {
                    bool shouldShow = currentFilters == null || !currentFilters.ContainsKey("subway") || currentFilters["subway"];
                    if (shouldShow)
                    {
                        float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
                        if (dist > maxDist) shouldShow = false;
                    }

                    Target targetComp = newObj.GetComponentInChildren<Target>(true);
                    if (targetComp != null && !shouldShow) targetComp.enabled = false;

                    SetupObject(newObj, data);
                    newObj.SetActive(shouldShow);

                    if (!shouldShow && targetComp != null) targetComp.enabled = true;

                    spawnedObjects[uniqueId] = newObj;
                }
            }

            if (objectSpawnDelay > 0)
            {
                yield return new WaitForSeconds(objectSpawnDelay);
            }
        }
        isDataLoaded = true;
    }

    private void SetupObject(GameObject obj, FacilityData data)
    {
        obj.name = "Subway_" + data.name;
        CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>();
        if (anchor != null)
        {
            anchor.SetCoordinatesAndCreateAnchor(data.latitude, data.longitude, data.altitude);
        }

        DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>();
        if (doubleTap != null)
        {
            // [UI Update] Description 제거, PlaceInfoText로 통합
            doubleTap.SetInfoImages(
                sprite1: null, 
                sprite2: null, 
                petFriendly: false, 
                separateRestroom: false, 
                description: null, // 상단 텍스트 제거
                name: data.name, 
                id: -1, 
                username: "WOOPANG", // Created By WOOPANG
                instagramId: null, 
                tel: null, 
                address: data.address, 
                overview: data.extra_info, // 개요에 extra_info 표시
                petInfo: null
            );
        }

        Target target = obj.GetComponentInChildren<Target>();
        if (target != null)
        {
            target.PlaceName = data.name;
            // 지하철 인디케이터 색상: #3da29c
            Color subwayColor;
            if (ColorUtility.TryParseHtmlString("#3da29c", out subwayColor))
            {
                target.TargetColor = subwayColor;
            }
            else
            {
                target.TargetColor = Color.green; // fallback
            }
        }
    }

    private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000;
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(Mathf.Deg2Rad * (lat1)) * Mathf.Cos(Mathf.Deg2Rad * (lat2)) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        return R * 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
    }

    public void SetAllRenderersVisible(bool visible)
    {
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value == null) continue;
            CustomARGeospatialCreatorAnchor anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor != null)
                anchor.SetForceHideRenderers(!visible);
        }
    }

    public void UpdateDistanceFilter(float maxDistance, float currentLat, float currentLon)
    {
        // 카테고리 토글 OFF면 거리와 무관하게 전부 숨김
        bool show = currentFilters == null || !currentFilters.ContainsKey("subway") || currentFilters["subway"];

        foreach (var kvp in spawnedObjects)
        {
            string id = kvp.Key;
            GameObject obj = kvp.Value;
            if (obj == null) continue;

            if (!show)
            {
                if (obj.activeSelf) obj.SetActive(false);
                continue;
            }

            if (placeDataMap.ContainsKey(id))
            {
                var data = placeDataMap[id];
                float dist = CalculateDistance(currentLat, currentLon, (float)data.latitude, (float)data.longitude);
                bool inRange = dist <= maxDistance;
                obj.SetActive(inRange);
            }
        }
    }

    public void ApplyFilters(Dictionary<string, bool> filters)
    {
        if (filters == null) return;
        currentFilters = new Dictionary<string, bool>(filters);
        bool show = !filters.ContainsKey("subway") || filters["subway"];
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value != null) kvp.Value.SetActive(show);
        }
    }

    public void SetAllObjectsVisible(bool visible)
    {
        if (visible)
        {
            // 복원 시 거리 필터 적용
            float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
            float lat = 0f, lon = 0f;
#if UNITY_EDITOR
            if (VirtualLocation.Instance != null) { lat = VirtualLocation.Instance.Latitude; lon = VirtualLocation.Instance.Longitude; }
#else
            if (Input.location.status == LocationServiceStatus.Running) { lat = Input.location.lastData.latitude; lon = Input.location.lastData.longitude; }
#endif
            UpdateDistanceFilter(maxDist, lat, lon);
        }
        else
        {
            foreach (var kvp in spawnedObjects)
            {
                if (kvp.Value != null) kvp.Value.SetActive(false);
            }
        }
    }

    public Dictionary<string, FacilityData> GetPlaceDataMap() => placeDataMap;
    public bool IsDataLoaded() => isDataLoaded;
    public Dictionary<string, GameObject> GetSpawnedObjects() => spawnedObjects;
    public int GetSpawnedObjectsCount() => spawnedObjects.Count;

    public int GetVisibleObjectCount()
    {
        int count = 0;
        foreach (var kvp in spawnedObjects)
            if (kvp.Value != null && kvp.Value.activeSelf) count++;
        return count;
    }

    /// <summary>
    /// 앵커 생성에 실패한 오브젝트만 선별하여 재시도
    /// FilterManager.AllocationLoop가 매 tick(2s) 호출 — AR Limited 구간에서 실패한 항목을 자동 복원
    /// </summary>
    public void RetryFailedAnchors()
    {
        RetryFailedAnchorsIn(spawnedObjects);
        RetryFailedAnchorsIn(indicatorOnlyObjects);
    }

    private static void RetryFailedAnchorsIn(Dictionary<string, GameObject> objects)
    {
        if (objects == null) return;
        foreach (var kvp in objects)
        {
            if (kvp.Value == null) continue;
            if (!kvp.Value.activeSelf) continue;

            var anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor == null) continue;
            if (anchor.IsAnchorCreated) continue;
            if (anchor.IsRetrying) continue;

            anchor.RecreateAnchor();
        }
    }

    private GameObject GetFromPool()
    {
        if (objectPool.Count > 0)
        {
            GameObject obj = objectPool.Dequeue();
            return obj;
        }
        else if (spawnedObjects.Count < poolSize * 2)
        {
            return Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
        }
        return null;
    }

    // ============================================================
    // IPlaceCacheProvider 구현
    // ============================================================

    public string FilterKey => "subway";
    public int MaxCacheSize => 100;
    public bool IsCacheReady => isCacheReady;

    public List<CachedPlaceData> GetCachedPlaces() => lightCache;

    public bool SpawnFullObject(string rawId)
    {
        if (spawnedObjects.ContainsKey(rawId)) return true;
        if (!placeDataMap.ContainsKey(rawId)) return false;

        GameObject newObj = GetFromPool();
        if (newObj == null) return false;

        FacilityData data = placeDataMap[rawId];
        SetupObject(newObj, data);
        newObj.SetActive(true);
        spawnedObjects[rawId] = newObj;
        return true;
    }

    public bool SpawnIndicatorOnly(string rawId)
    {
        if (indicatorOnlyObjects.ContainsKey(rawId)) return true;
        if (indicatorOnlyPrefab == null) return false;

        CachedPlaceData cached = lightCache.Find(c => c.rawId == rawId);
        if (cached == null) return false;

        GameObject obj = GetIndicatorFromPool();
        if (obj == null) return false;

        obj.name = $"Indicator_Subway_{cached.displayName}";
        obj.SetActive(true);

        var target = obj.GetComponentInChildren<Target>(true);
        if (target != null)
        {
            target.PlaceName = cached.displayName;
            target.gpsLatitude = cached.latitude;
            target.gpsLongitude = cached.longitude;
            Color subwayColor;
            if (ColorUtility.TryParseHtmlString("#3da29c", out subwayColor))
                target.TargetColor = subwayColor;
        }

        var anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
        if (anchor != null)
            anchor.SetCoordinatesAndCreateAnchor(cached.latitude, cached.longitude, cached.altitude);

        indicatorOnlyObjects[rawId] = obj;
        return true;
    }

    private GameObject GetIndicatorFromPool()
    {
        while (indicatorOnlyPool.Count > 0)
        {
            GameObject pooled = indicatorOnlyPool.Dequeue();
            if (pooled != null) return pooled;
        }
        return Instantiate(indicatorOnlyPrefab);
    }

    private void ReturnIndicatorToPool(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        indicatorOnlyPool.Enqueue(obj);
    }

    public void DespawnFullObject(string rawId)
    {
        if (!spawnedObjects.ContainsKey(rawId)) return;

        ReturnToPool(spawnedObjects[rawId]);
        spawnedObjects.Remove(rawId);
    }

    public void DespawnIndicatorOnly(string rawId)
    {
        if (!indicatorOnlyObjects.ContainsKey(rawId)) return;

        GameObject obj = indicatorOnlyObjects[rawId];
        indicatorOnlyObjects.Remove(rawId);
        ReturnIndicatorToPool(obj);
    }

    public HashSet<string> GetSpawnedFullIds()
    {
        var result = new HashSet<string>();
        foreach (string id in spawnedObjects.Keys)
            result.Add("subway_" + id);
        return result;
    }

    public HashSet<string> GetSpawnedIndicatorIds()
    {
        var result = new HashSet<string>();
        foreach (string id in indicatorOnlyObjects.Keys)
            result.Add("subway_" + id);
        return result;
    }

    public void RefreshCache(float lat, float lon)
    {
        StartCoroutine(FetchFacilityData(lat, lon, 10000f));
    }
}
