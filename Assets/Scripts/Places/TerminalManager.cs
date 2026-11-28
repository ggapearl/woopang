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

public class TerminalManager : MonoBehaviour, IPlaceCacheProvider, IPlaceCacheLoadTime
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
    public event System.Action CacheBecameReady;
    private bool cacheReadyEventFired = false;
    private void MarkCacheReady()
    {
        isCacheReady = true;
        LastLoadedAt = Time.realtimeSinceStartup;
        if (cacheReadyEventFired) return;
        cacheReadyEventFired = true;
        CacheBecameReady?.Invoke();
    }

    [Header("IndicatorOnly (FilterManager 배분 전용)")]
    [Tooltip("IndicatorOnly 프리팹 — FilterManager가 중앙 배분")]
    [SerializeField] private GameObject indicatorOnlyPrefab;
    private Dictionary<string, GameObject> indicatorOnlyObjects = new Dictionary<string, GameObject>();
    private Queue<GameObject> indicatorOnlyPool = new Queue<GameObject>(20);

    void Start()
    {
        currentFilters = new Dictionary<string, bool>();
        currentFilters["terminal"] = PlayerPrefs.GetInt("Filter_Terminal_V2", 1) == 1;

        StartCoroutine(InitializeObjectPool());   // 오프닝이 걷힌 뒤 한가한 프레임에 하나씩 (11-15 ④ — 예전엔 첫 화면 전에 교통 3종 40개를 한꺼번에)
        StartCoroutine(StartLocationServiceAndFetchData());

        FilterManager filterMgr = Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
        if (filterMgr != null) filterMgr.RegisterCacheProvider(this);
    }

    private IEnumerator InitializeObjectPool()
    {
        if (samplePrefab == null) yield break;
        while (objectPool.Count + spawnedObjects.Count < poolSize)   // 그 사이 터미널이 떠서 만든 것도 센다 — 예전과 같은 수만큼만
        {
            yield return null;
            if (!SpawnPace.Idle) continue;
            GameObject obj = Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
            obj.SetActive(false);
            objectPool.Enqueue(obj);
            SpawnPace.Took();
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
            MarkCacheReady();
            yield break;
        }

        // Light 캐시 갱신
        lightCache.Clear();
        foreach (var f in facilities)
        {
            string uid = "terminal_" + f.name + "_" + f.latitude + "_" + f.longitude;
            // 역 정보는 목록 준비를 알리기 전에 다 담는다 — 배분이 바로 3D 로 띄울 수 있게.
            // 예전엔 아래 생성 루프가 0.1초에 하나씩 담아, 그 전에 배분된 역은 임시 화살표로 15초를 기다렸다
            placeDataMap[f.name + "_" + f.latitude + "_" + f.longitude] = f;
            lightCache.Add(new CachedPlaceData
            {
                uniqueId = uid,
                rawId = f.name + "_" + f.latitude + "_" + f.longitude,
                displayName = f.name ?? "",
                latitude = f.latitude,
                longitude = f.longitude,
                altitude = f.altitude,
                category = "terminal",
                sourceManager = "Terminal",
                modelType = "cube",
                petFriendly = false,
                filterKey = "terminal"
            });
        }
        if (lightCache.Count > MaxCacheSize) lightCache.RemoveRange(MaxCacheSize, lightCache.Count - MaxCacheSize);
        MarkCacheReady();

        foreach (var data in facilities)
        {
            string uniqueId = data.name + "_" + data.latitude + "_" + data.longitude;

            placeDataMap[uniqueId] = data;
            // 애견동반을 켜면 만들지도 다시 켜지도 않는다 (11-14 대표님 「애견동반에서는 지하철, 기차, 터미널 모두 그냥 발생하지 않으면」 · 11-15 공항도).
            // 예전엔 여기 생성 · 다시 켜기가 애견동반을 보지 않아, 켜 둔 채 움직여 목록을 다시 받으면 역이 떴다 (배분이 모르는 오브젝트라 그대로 남았다)
            bool petHidden = FilterManager.PetOnlyNow;

            float dist = CalculateDistance(latitude, longitude, (float)data.latitude, (float)data.longitude);

            if (!spawnedObjects.ContainsKey(uniqueId) && !petHidden && !HiddenPlaces.IsHidden("terminal_" + uniqueId))
            {
                // objectSpawnRadius 이내만 오브젝트 생성
                if (dist <= objectSpawnRadius)
                {
                    while (!SpawnPace.Ready) yield return null;   // 한 프레임에 하나씩 (SpawnPace · FilterManager 배분과 같은 문)
                    GameObject newObj = spawnedObjects.ContainsKey(uniqueId) ? null : GetFromPool();   // 기다리는 사이 배분이 띄웠으면 그대로
                    if (newObj != null)
                    {
                        SpawnPace.Took();
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

                        ARObjectZoomController.Fit(newObj);   // 핀치 줌 중에 뜨거나 풀에서 다시 꺼내도 지금 배율로
                        spawnedObjects[uniqueId] = newObj;
                    }
                }
            }
            else if (spawnedObjects.TryGetValue(uniqueId, out GameObject existing))   // 숨긴 곳(X)은 떠 있지 않다 — 예전엔 여기서 KeyNotFound 로 나머지 목록 처리가 멈췄다
            {
                bool shouldShow = !petHidden && (currentFilters == null || !currentFilters.ContainsKey("terminal") || currentFilters["terminal"]);
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

        // 풀의 오브젝트는 맨 위가 꺼져 있다 — 자식(Cube)의 DoubleTap3D · Target 은 DataManager · StoreManager 처럼 true 로 찾는다 (11-27)
        FacilityInfo.Configure(obj.GetComponentInChildren<DoubleTap3D>(true), data);   // 지하철 · 기차역과 같은 정보 창

        Target target = obj.GetComponentInChildren<Target>(true);
        if (target != null)
        {
            target.PlaceName = data.name;
            target.gpsLatitude = data.latitude;   // 거리 · fallback 화살표 목록 (Target.GetGPSDistance) — 앵커가 아직 없어도 (11-26 18 · 33번 · data-managers 7번)
            target.gpsLongitude = data.longitude;
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
        bool petOnly = currentFilters != null && currentFilters.TryGetValue("petFriendlyOnly", out bool po) && po;

        foreach (var kvp in spawnedObjects)
        {
            string id = kvp.Key;
            GameObject obj = kvp.Value;
            if (obj == null) continue;

            if (!show || petOnly)
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
        bool petOnly = filters.TryGetValue("petFriendlyOnly", out bool po) && po;
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value != null) kvp.Value.SetActive(show && !petOnly);   // 애견동반을 켜면 공항까지 다 숨긴다 (11-15 · FilterManager.PetAllowed)
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
    public Dictionary<string, GameObject> GetIndicatorOnlyObjects() => indicatorOnlyObjects;
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

    public string FilterKey => "terminal";
    public int MaxCacheSize => 20;
    public bool IsCacheReady => isCacheReady;
    public float LastLoadedAt { get; private set; } = -1f;

    public List<CachedPlaceData> GetCachedPlaces() => lightCache;

    public bool SpawnFullObject(string rawId)
    {
        if (HiddenPlaces.IsHidden("terminal_" + rawId)) return false;   // 이 기기에서 X 로 숨긴 장소
        if (spawnedObjects.ContainsKey(rawId)) return true;
        if (!placeDataMap.ContainsKey(rawId)) return false;

        GameObject newObj = GetFromPool();
        if (newObj == null) return false;

        FacilityData data = placeDataMap[rawId];
        SetupObject(newObj, data);
        newObj.SetActive(true);
        ARObjectZoomController.Fit(newObj);
        spawnedObjects[rawId] = newObj;
        return true;
    }

    public bool SpawnIndicatorOnly(string rawId)
    {
        if (HiddenPlaces.IsHidden("terminal_" + rawId)) return false;   // 이 기기에서 X 로 숨긴 장소
        if (indicatorOnlyObjects.ContainsKey(rawId)) return true;
        if (indicatorOnlyPrefab == null) return false;

        CachedPlaceData cached = lightCache.Find(c => c.rawId == rawId);
        if (cached == null) return false;

        GameObject obj = GetIndicatorFromPool();
        if (obj == null) return false;

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
