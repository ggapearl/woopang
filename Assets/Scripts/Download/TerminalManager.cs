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

public class TerminalManager : MonoBehaviour, IPlaceCacheProvider
{
    private static TerminalManager instance;
    public static TerminalManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindFirstObjectByType<TerminalManager>();
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
    private readonly string apiUrlTemplate = "{0}?lat={1}&lon={2}&radius={3}&type=terminal";

    public GameObject samplePrefab;

    private Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>(100);
    private Dictionary<string, FacilityData> placeDataMap = new Dictionary<string, FacilityData>(100);
    private Dictionary<string, bool> currentFilters;
    private Queue<GameObject> objectPool = new Queue<GameObject>(20);

    [SerializeField] public int poolSize = 10;

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

    private List<CachedPlaceData> lightCache = new List<CachedPlaceData>();
    private bool isCacheReady = false;

    [Header("IndicatorOnly (FilterManager 배분 전용)")]
    [Tooltip("IndicatorOnly 프리팹 — FilterManager가 중앙 배분")]
    [SerializeField] private GameObject indicatorOnlyPrefab;
    private Dictionary<string, GameObject> indicatorOnlyObjects = new Dictionary<string, GameObject>();

    void Start()
    {
        currentFilters = new Dictionary<string, bool>();
        currentFilters["terminal"] = PlayerPrefs.GetInt("Filter_Terminal_V2", 1) == 1;

        InitializeObjectPool();
        StartCoroutine(StartLocationServiceAndFetchData());

        FilterManager filterMgr = Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        Debug.LogWarning($"[dbg] TerminalManager.Start: FilterManager={(filterMgr != null ? "찾음" : "NULL!")}");
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
#if UNITY_EDITOR
        lastPosition = new Vector2(VirtualLocation.Instance.Latitude, VirtualLocation.Instance.Longitude);
        StartCoroutine(TrackPosition());
        yield break;
#else
        // 권한이 없으면 DataManager의 이벤트를 기다린 후 진행
        if (!Input.location.isEnabledByUser)
        {
            bool permissionGranted = false;
            System.Action onGranted = () => permissionGranted = true;
            DataManager.OnLocationPermissionGranted += onGranted;
            yield return new WaitUntil(() => permissionGranted);
            DataManager.OnLocationPermissionGranted -= onGranted;
        }

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
                if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
                    FileLogger.Instance.LogGPS(lat, lon, $"[Terminal] 이동감지 dist={distanceMoved:F0}m");
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
                Debug.LogError($"[TerminalManager] API request failed: {request.error}");
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
            Debug.LogError($"[TerminalManager] JSON parsing failed: {ex.Message}");
            yield break;
        }

        if (facilities == null || facilities.Count == 0)
        {
            lightCache.Clear();
            isCacheReady = true;
            Debug.LogWarning($"[dbg][TerminalManager][CACHE] 0개 반환 — isCacheReady=true 설정");
            yield break;
        }

        // Light 캐시 갱신
        lightCache.Clear();
        foreach (var f in facilities)
        {
            string uid = "terminal_" + f.name + "_" + f.latitude + "_" + f.longitude;
            lightCache.Add(new CachedPlaceData
            {
                uniqueId = uid,
                rawId = f.name + "_" + f.latitude + "_" + f.longitude,
                displayName = f.name ?? "",
                latitude = (float)f.latitude,
                longitude = (float)f.longitude,
                altitude = f.altitude,
                category = "terminal",
                sourceManager = "Terminal",
                modelType = "cube",
                petFriendly = false,
                filterKey = "terminal"
            });
        }
        if (lightCache.Count > MaxCacheSize) lightCache.RemoveRange(MaxCacheSize, lightCache.Count - MaxCacheSize);
        isCacheReady = true;

        foreach (var data in facilities)
        {
            string uniqueId = data.name + "_" + data.latitude + "_" + data.longitude;

            placeDataMap[uniqueId] = data;

            float dist = CalculateDistance(latitude, longitude, (float)data.latitude, (float)data.longitude);

            if (!spawnedObjects.ContainsKey(uniqueId))
            {
                // objectSpawnRadius 이내만 오브젝트 생성
                if (dist <= objectSpawnRadius)
                {
                    GameObject newObj = GetFromPool();
                    if (newObj != null)
                    {
                        // 필터 + 거리 체크
                        bool shouldShow = currentFilters == null || !currentFilters.ContainsKey("terminal") || currentFilters["terminal"];
                        if (shouldShow)
                        {
                            float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
                            if (dist > maxDist) shouldShow = false;
                        }

                        // Target 플래시 방지: 비활성 상태에서 Target 끄기
                        Target targetComp = newObj.GetComponentInChildren<Target>(true);
                        if (targetComp != null && !shouldShow) targetComp.enabled = false;

                        SetupObject(newObj, data);
                        newObj.SetActive(shouldShow);

                        if (!shouldShow && targetComp != null) targetComp.enabled = true;

                        spawnedObjects[uniqueId] = newObj;
                    }
                }
            }
            else
            {
                GameObject existing = spawnedObjects[uniqueId];
                bool shouldShow = currentFilters == null || !currentFilters.ContainsKey("terminal") || currentFilters["terminal"];
                if (shouldShow)
                {
                    float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
                    if (dist > maxDist) shouldShow = false;
                }
                if (shouldShow && !existing.activeSelf) existing.SetActive(true);
                else if (!shouldShow && existing.activeSelf) existing.SetActive(false);
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
        obj.name = "Terminal_" + data.name;
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
            target.TargetColor = Color.green;
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
        bool show = currentFilters == null || !currentFilters.ContainsKey("terminal") || currentFilters["terminal"];

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
        bool show = !filters.ContainsKey("terminal") || filters["terminal"];
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value != null) kvp.Value.SetActive(show);
        }
    }

    public void SetAllObjectsVisible(bool visible)
    {
        if (visible)
        {
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
    /// 백그라운드 복귀 시 모든 스폰된 오브젝트의 Geospatial 앵커를 재생성
    /// </summary>
    public void RecreateAllAnchors()
    {
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value == null) continue;
            if (!kvp.Value.activeSelf) continue;

            CustomARGeospatialCreatorAnchor anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor != null)
            {
                anchor.RecreateAnchor();
            }
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

    public string FilterKey => "terminal";
    public int MaxCacheSize => 20;
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

        Debug.LogWarning($"[dbg][TerminalManager][SPAWN] Full id={rawId} name={data.name} total={spawnedObjects.Count}");
        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
            FileLogger.Instance.LogSpawn("Terminal_Full", rawId, data.name, true);

        return true;
    }

    public bool SpawnIndicatorOnly(string rawId)
    {
        if (indicatorOnlyObjects.ContainsKey(rawId)) return true;
        if (indicatorOnlyPrefab == null) return false;

        CachedPlaceData cached = lightCache.Find(c => c.rawId == rawId);
        if (cached == null) return false;

        GameObject obj = Instantiate(indicatorOnlyPrefab);
        obj.name = $"Indicator_Terminal_{cached.displayName}";
        obj.SetActive(true);

        var target = obj.GetComponentInChildren<Target>(true);
        if (target != null)
        {
            target.PlaceName = cached.displayName;
            target.gpsLatitude = cached.latitude;
            target.gpsLongitude = cached.longitude;
            Color catColor = DataManager.GetCategoryColor(cached.category);
            target.TargetColor = catColor != Color.white ? catColor : Color.green;
        }

        var anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
        if (anchor != null)
            anchor.SetCoordinatesAndCreateAnchor(cached.latitude, cached.longitude, cached.altitude);

        indicatorOnlyObjects[rawId] = obj;

        Debug.LogWarning($"[dbg][TerminalManager][SPAWN] Indicator id={rawId} name={cached.displayName} total={indicatorOnlyObjects.Count}");
        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
            FileLogger.Instance.LogSpawn("Terminal_Indicator", rawId, cached.displayName, true);

        return true;
    }

    public void DespawnFullObject(string rawId)
    {
        if (!spawnedObjects.ContainsKey(rawId)) return;

        string objName = placeDataMap.ContainsKey(rawId) ? placeDataMap[rawId].name : "";
        Debug.LogWarning($"[dbg][TerminalManager][DESPAWN] Full id={rawId} name={objName} remaining={spawnedObjects.Count - 1}");
        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
            FileLogger.Instance.LogSpawn("Terminal_Full", rawId, objName, false);

        ReturnToPool(spawnedObjects[rawId]);
        spawnedObjects.Remove(rawId);
    }

    public void DespawnIndicatorOnly(string rawId)
    {
        if (!indicatorOnlyObjects.ContainsKey(rawId)) return;

        var cached = lightCache.Find(c => c.rawId == rawId);
        string displayName = cached?.displayName ?? "";
        Debug.LogWarning($"[dbg][TerminalManager][DESPAWN] Indicator id={rawId} name={displayName} remaining={indicatorOnlyObjects.Count - 1}");
        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
            FileLogger.Instance.LogSpawn("Terminal_Indicator", rawId, displayName, false);

        if (indicatorOnlyObjects[rawId] != null) Destroy(indicatorOnlyObjects[rawId]);
        indicatorOnlyObjects.Remove(rawId);
    }

    public HashSet<string> GetSpawnedFullIds()
    {
        var result = new HashSet<string>();
        foreach (string id in spawnedObjects.Keys) result.Add("terminal_" + id);
        return result;
    }

    public HashSet<string> GetSpawnedIndicatorIds()
    {
        var result = new HashSet<string>();
        foreach (string id in indicatorOnlyObjects.Keys) result.Add("terminal_" + id);
        return result;
    }

    public void RefreshCache(float lat, float lon)
    {
        Debug.LogWarning($"[dbg][TerminalManager][CACHE] RefreshCache 시작 lat={lat:F6} lon={lon:F6}");
        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
            FileLogger.Instance.Log("CACHE", $"[Terminal] RefreshCache lat={lat:F6} lon={lon:F6}");

        StartCoroutine(FetchFacilityData(lat, lon, 10000f));
    }

    public void RefreshAnchors()
    {
        int full = 0, indicator = 0;
        foreach (var kv in spawnedObjects)
        {
            if (kv.Value == null) continue;
            if (!placeDataMap.TryGetValue(kv.Key, out var data)) continue;
            var anchor = kv.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor != null)
            {
                anchor.SetCoordinatesAndCreateAnchor(data.latitude, data.longitude, data.altitude);
                full++;
            }
        }
        foreach (var kv in indicatorOnlyObjects)
        {
            if (kv.Value == null) continue;
            var cached = lightCache.Find(c => c.rawId == kv.Key);
            if (cached == null) continue;
            var anchor = kv.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor != null)
            {
                anchor.SetCoordinatesAndCreateAnchor(cached.latitude, cached.longitude, cached.altitude);
                indicator++;
            }
        }
        Debug.LogWarning($"[dbg][TerminalManager][ANCHOR] RefreshAnchors Full={full} Indicator={indicator}");
        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
            FileLogger.Instance.Log("ANCHOR", $"[Terminal] RefreshAnchors Full={full} Indicator={indicator}");
    }
}
