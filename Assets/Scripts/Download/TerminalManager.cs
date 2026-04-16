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
#pragma warning disable CS0414 // Inspector 설정용 필드
    [SerializeField] private float updateInterval = 600f;
#pragma warning restore CS0414

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
    private Dictionary<string, GameObject> indicatorOnlyObjects = new Dictionary<string, GameObject>();
    [SerializeField] private GameObject indicatorOnlyPrefab;

    private static readonly WaitForSeconds waitUpdateInterval = new WaitForSeconds(600f);

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
#if UNITY_EDITOR
        lastPosition = new Vector2(VirtualLocation.Instance.Latitude, VirtualLocation.Instance.Longitude);
        fetchCoroutine = StartCoroutine(FetchDataPeriodically());
        StartCoroutine(CheckPositionAndFetchData());
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
        fetchCoroutine = StartCoroutine(FetchDataPeriodically());
        StartCoroutine(CheckPositionAndFetchData());
#endif
    }

    private IEnumerator FetchDataPeriodically()
    {
        while (true)
        {
#if UNITY_EDITOR
            float lat = VirtualLocation.Instance.Latitude;
            float lon = VirtualLocation.Instance.Longitude;
#else
            yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking);
            float lat = Input.location.lastData.latitude;
            float lon = Input.location.lastData.longitude;
#endif
            foreach (float radius in loadRadii)
            {
                yield return StartCoroutine(FetchFacilityData(lat, lon, radius));
                if (tierDelay > 0) yield return new WaitForSeconds(tierDelay);
            }
            yield return waitUpdateInterval;
        }
    }

    private IEnumerator CheckPositionAndFetchData()
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
                foreach (float radius in loadRadii)
                {
                    yield return StartCoroutine(FetchFacilityData(lat, lon, radius));
                    if (tierDelay > 0) yield return new WaitForSeconds(tierDelay);
                }
                CleanupStaleObjects(lat, lon);
                lastPosition = currentPos;
            }

            // objectSpawnRadius 기반 스폰/정리 (서버 재요청 없이)
            SpawnNearbyUnspawnedObjects(lat, lon);

            yield return new WaitForSeconds(1f);
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

    /// <summary>
    /// 듀얼 레인지 정리: objectSpawnRadius*1.5 밖은 오브젝트만 풀 반환,
    /// MaxDisplayDistance*1.5 밖은 데이터도 제거
    /// </summary>
    private void CleanupStaleObjects(float lat, float lon)
    {
        float objCleanupRange = objectSpawnRadius * 1.5f;
        float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
        float dataCleanupRange = maxDist * 1.5f;

        // 1) objectSpawnRadius*1.5 밖: 오브젝트만 풀 반환 (데이터 유지)
        List<string> objToRemove = new List<string>();
        foreach (var kvp in spawnedObjects)
        {
            string id = kvp.Key;
            if (!placeDataMap.ContainsKey(id)) continue;
            var data = placeDataMap[id];
            float dist = CalculateDistance(lat, lon, (float)data.latitude, (float)data.longitude);
            if (dist > objCleanupRange)
            {
                objToRemove.Add(id);
            }
        }
        foreach (string id in objToRemove)
        {
            ReturnToPool(spawnedObjects[id]);
            spawnedObjects.Remove(id);
        }

        // 2) MaxDisplayDistance*1.5 밖: 데이터도 제거
        List<string> dataToRemove = new List<string>();
        foreach (var kvp in placeDataMap)
        {
            float dist = CalculateDistance(lat, lon, (float)kvp.Value.latitude, (float)kvp.Value.longitude);
            if (dist > dataCleanupRange)
            {
                dataToRemove.Add(kvp.Key);
            }
        }
        foreach (string id in dataToRemove)
        {
            if (spawnedObjects.ContainsKey(id))
            {
                ReturnToPool(spawnedObjects[id]);
                spawnedObjects.Remove(id);
            }
            placeDataMap.Remove(id);
        }
    }

    private void SpawnNearbyUnspawnedObjects(float lat, float lon)
    {
        float cleanupRange = objectSpawnRadius * 1.5f;
        List<string> toRemove = new List<string>();
        foreach (var kvp in spawnedObjects)
        {
            if (!placeDataMap.ContainsKey(kvp.Key)) continue;
            var data = placeDataMap[kvp.Key];
            float dist = CalculateDistance(lat, lon, (float)data.latitude, (float)data.longitude);
            if (dist > cleanupRange) toRemove.Add(kvp.Key);
        }
        foreach (string id in toRemove)
        {
            ReturnToPool(spawnedObjects[id]);
            spawnedObjects.Remove(id);
        }
        foreach (var kvp in placeDataMap)
        {
            if (spawnedObjects.ContainsKey(kvp.Key)) continue;
            var data = kvp.Value;
            float dist = CalculateDistance(lat, lon, (float)data.latitude, (float)data.longitude);
            if (dist <= objectSpawnRadius)
            {
                GameObject newObj = GetFromPool();
                if (newObj != null)
                {
                    SetupObject(newObj, data);
                    bool shouldShow = currentFilters == null || !currentFilters.ContainsKey("terminal") || currentFilters["terminal"];
                    newObj.SetActive(shouldShow);
                    spawnedObjects[kvp.Key] = newObj;
                }
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

        if (facilities == null || facilities.Count == 0) yield break;

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

        SetupObject(newObj, placeDataMap[rawId]);
        newObj.SetActive(true);
        spawnedObjects[rawId] = newObj;

        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
            FileLogger.Instance.LogSpawn("Terminal_Full", rawId, placeDataMap[rawId].name, true);

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

        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
            FileLogger.Instance.LogSpawn("Terminal_Indicator", rawId, cached.displayName, true);

        return true;
    }

    public void DespawnFullObject(string rawId)
    {
        if (!spawnedObjects.ContainsKey(rawId)) return;

        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
        {
            string name = placeDataMap.ContainsKey(rawId) ? placeDataMap[rawId].name : "";
            FileLogger.Instance.LogSpawn("Terminal_Full", rawId, name, false);
        }

        ReturnToPool(spawnedObjects[rawId]);
        spawnedObjects.Remove(rawId);
    }

    public void DespawnIndicatorOnly(string rawId)
    {
        if (!indicatorOnlyObjects.ContainsKey(rawId)) return;

        if (FileLogger.Instance != null && FileLogger.Instance.IsLogging)
        {
            var cached = lightCache.Find(c => c.rawId == rawId);
            FileLogger.Instance.LogSpawn("Terminal_Indicator", rawId, cached?.displayName ?? "", false);
        }

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
        StartCoroutine(FetchFacilityData(lat, lon, 10000f));
    }
}
