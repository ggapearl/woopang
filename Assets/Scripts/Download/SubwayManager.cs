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

public class SubwayManager : MonoBehaviour
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

    private bool isDataLoaded = false;
    private Coroutine fetchCoroutine;
    private Vector2 lastPosition;

    private static readonly WaitForSeconds waitUpdateInterval = new WaitForSeconds(600f);

    void Start()
    {
        // PlayerPrefs에서 필터 상태 초기화 (DataManager 패턴)
        currentFilters = new Dictionary<string, bool>();
        currentFilters["subway"] = PlayerPrefs.GetInt("Filter_Subway_V2", 1) == 1;

        InitializeObjectPool();
        StartCoroutine(StartLocationServiceAndFetchData());
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
                lastPosition = currentPos;
            }
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
                Debug.LogError($"[SubwayManager] API request failed: {request.error}");
            }
        }
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

        if (facilities == null || facilities.Count == 0) yield break;

        foreach (var data in facilities)
        {
            string uniqueId = data.name + "_" + data.latitude + "_" + data.longitude;
            if (!spawnedObjects.ContainsKey(uniqueId))
            {
                GameObject newObj = GetFromPool();
                if (newObj != null)
                {
                    // 필터 + 거리 체크
                    bool shouldShow = currentFilters == null || !currentFilters.ContainsKey("subway") || currentFilters["subway"];
                    if (shouldShow)
                    {
                        float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
                        float dist = CalculateDistance(latitude, longitude, (float)data.latitude, (float)data.longitude);
                        if (dist > maxDist) shouldShow = false;
                    }

                    // Target 플래시 방지: 비활성 상태에서 Target 끄기
                    Target targetComp = newObj.GetComponentInChildren<Target>(true);
                    if (targetComp != null && !shouldShow) targetComp.enabled = false;

                    SetupObject(newObj, data);
                    newObj.SetActive(shouldShow);

                    if (!shouldShow && targetComp != null) targetComp.enabled = true;

                    spawnedObjects[uniqueId] = newObj;
                    placeDataMap[uniqueId] = data;
                }
            }
            else
            {
                GameObject existing = spawnedObjects[uniqueId];
                bool shouldShow = currentFilters == null || !currentFilters.ContainsKey("subway") || currentFilters["subway"];
                if (shouldShow)
                {
                    float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
                    float dist = CalculateDistance(latitude, longitude, (float)data.latitude, (float)data.longitude);
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
            Renderer[] renderers = kvp.Value.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = visible;
        }
    }

    public void UpdateDistanceFilter(float maxDistance, float currentLat, float currentLon)
    {
        foreach (var kvp in spawnedObjects)
        {
            string id = kvp.Key;
            GameObject obj = kvp.Value;
            if (obj == null) continue;

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

    /// <summary>
    /// 백그라운드 복귀 시 모든 스폰된 오브젝트의 Geospatial 앵커를 재생성
    /// </summary>
    public void RecreateAllAnchors()
    {
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value == null) continue;
            CustomARGeospatialCreatorAnchor anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor != null)
            {
                Renderer[] renderers = kvp.Value.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers) r.enabled = false;

                kvp.Value.SetActive(true);
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
}
