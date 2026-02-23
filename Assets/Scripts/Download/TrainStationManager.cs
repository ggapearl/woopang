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

public class TrainStationManager : MonoBehaviour
{
    private static TrainStationManager instance;
    public static TrainStationManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindFirstObjectByType<TrainStationManager>();
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
    private readonly string apiUrlTemplate = "{0}?lat={1}&lon={2}&radius={3}&type=train";

    public GameObject samplePrefab;

    private Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>(100);
    private Dictionary<string, FacilityData> placeDataMap = new Dictionary<string, FacilityData>(100);
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
                Debug.LogError($"[TrainStationManager] API request failed: {request.error}");
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
            Debug.LogError($"[TrainStationManager] JSON parsing failed: {ex.Message}");
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
                    SetupObject(newObj, data);
                    spawnedObjects[uniqueId] = newObj;
                    placeDataMap[uniqueId] = data;
                }
            }
            else
            {
                GameObject existing = spawnedObjects[uniqueId];
                if (!existing.activeSelf) existing.SetActive(true);
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
        obj.name = "Train_" + data.name;
        CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>();
        if (anchor != null)
        {
            anchor.SetCoordinatesAndCreateAnchor(data.latitude, data.longitude, data.altitude);
        }

        DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>();
        if (doubleTap != null)
        {
            // [UI Update] Description 제거, PlaceInfoText로 통합
            // desc (인자 5) -> null
            // username (인자 8) -> "WOOPANG"
            // overview (인자 12) -> data.extra_info
            
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

    public Dictionary<string, FacilityData> GetPlaceDataMap() => placeDataMap;
    public bool IsDataLoaded() => isDataLoaded;
    public int GetSpawnedObjectsCount() => spawnedObjects.Count;

    private GameObject GetFromPool()
    {
        if (objectPool.Count > 0)
        {
            GameObject obj = objectPool.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else if (spawnedObjects.Count < poolSize * 2)
        {
            return Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
        }
        return null;
    }
}
