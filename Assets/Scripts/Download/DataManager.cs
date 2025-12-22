using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Google.XR.ARCoreExtensions;
using Google.XR.ARCoreExtensions.GeospatialCreator;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class DataManager : MonoBehaviour
{
    // Singleton pattern
    private static DataManager instance;
    public static DataManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<DataManager>();
                if (instance == null)
                {
                    Debug.LogError("[DataManager] Instance not found in scene!");
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        // Singleton 체크
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[DataManager] Duplicate instance detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private string baseServerUrl = "https://woopang.com/locations?status=approved";
    
    [Header("UI")]
    [Tooltip("오브젝트 개수 표시 UI")]
    public ObjectCountUI objectCountUI;

    // [Tooltip("PlaceList 매니저 (Tier별 업데이트용)")]
    // public PlaceListManager placeListManager; // 삭제

    [Header("Prefabs")]
    public GameObject cubePrefab;
    public GameObject glbPrefab;

    [Header("GLB Settings")]
    [SerializeField] private int maxConcurrentGLBLoads = 3;
    [SerializeField] private float glbLoadTimeout = 30f;
    [SerializeField] private bool fallbackToCube = true;
    
    private Dictionary<int, GameObject> spawnedObjects = new Dictionary<int, GameObject>();
    private Dictionary<int, PlaceData> placeDataMap = new Dictionary<int, PlaceData>();
    private Queue<GameObject> cubeObjectPool = new Queue<GameObject>();
    private Queue<GameObject> glbObjectPool = new Queue<GameObject>();
    private HashSet<int> currentlyLoadingGLB = new HashSet<int>();
    
    [SerializeField] public int poolSize = 50;
    [SerializeField] private float updateInterval = 600f;
    
    [Header("Progressive Loading Settings")]
    [Tooltip("거리별 로딩 단계 (미터)")]
    public float[] loadRadii = new float[] { 25f, 50f, 75f, 100f, 150f, 200f, 500f, 1000f, 2000f, 5000f, 10000f };

    [Tooltip("각 거리 단계 사이의 딜레이 (초)")]
    public float tierDelay = 0.5f;

    [Tooltip("같은 단계 내 오브젝트 사이의 딜레이 (초)")]
    public float objectSpawnDelay = 0.1f;

    [SerializeField] private float updateDistanceThreshold = 50f;
    private bool isDataLoaded = false;
    private Coroutine fetchCoroutine;
    private Vector2 lastPosition;

    void OnEnable()
    {
        ARSession.stateChanged += OnARSessionStateChanged;
    }

    void OnDisable()
    {
        ARSession.stateChanged -= OnARSessionStateChanged;
    }

    void Start()
    {
        StartCoroutine(InitializeObjectPoolsAsync());
        StartCoroutine(StartLocationServiceAndFetchData());
    }

    private IEnumerator InitializeObjectPoolsAsync()
    {
        if (cubePrefab == null || glbPrefab == null)
        {
            yield break;
        }

        // Cube 오브젝트 풀 초기화 (5개씩 생성하고 프레임 양보)
        for (int i = 0; i < poolSize; i++)
        {
            GameObject cubeObj = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);
            cubeObj.SetActive(false);
            cubeObjectPool.Enqueue(cubeObj);

            if (i % 5 == 4) yield return null; // 5개마다 프레임 양보
        }

        // GLB 오브젝트 풀 초기화 (5개씩 생성하고 프레임 양보)
        for (int i = 0; i < poolSize; i++)
        {
            GameObject glbObj = Instantiate(glbPrefab, Vector3.zero, Quaternion.identity);
            glbObj.SetActive(false);
            glbObjectPool.Enqueue(glbObj);

            if (i % 5 == 4) yield return null; // 5개마다 프레임 양보
        }
    }

    private IEnumerator StartLocationServiceAndFetchData()
    {
#if UNITY_EDITOR
        Debug.Log("[DataManager] 에디터 환경 감지: 위치 서비스 및 AR 세션 체크를 건너뛰고 시뮬레이션 좌표를 사용합니다. (Lat: 36.6361, Lon: 126.8280, Alt: 80)");
        float lat = 36.6361f;
        float lon = 126.8280f;
        // 고도 시뮬레이션은 API 호출 파라미터나 로직에 직접 반영해야 하지만, 
        // 현재 DataManager 구조상 lat/lon을 주로 사용하므로 로그에만 명시하고 
        // 필요한 경우 해당 변수를 참조하는 로직에서 80을 사용하도록 해야 합니다.
        // 현재 구조에서는 lat/lon 위주로 처리되고 있음.
        
        lastPosition = new Vector2(lat, lon);
        
        fetchCoroutine = StartCoroutine(FetchDataPeriodically());
        StartCoroutine(CheckPositionAndFetchData());
        yield break;
#endif

        if (!Input.location.isEnabledByUser)
        {
            ShowErrorMessage("위치 서비스를 활성화해 주세요.");
            yield break;
        }
        
        Input.location.Start();
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }
        
        if (Input.location.status == LocationServiceStatus.Failed)
        {
            ShowErrorMessage("위치 서비스를 시작할 수 없습니다. 기본 위치로 시작합니다.");
            // 실패해도 계속 진행 (기본값 사용)
        }
        
        // 위치 데이터가 없으면 기본값 사용
        float latitude = 37.5665f;
        float longitude = 126.9780f;
        
        if (Input.location.status == LocationServiceStatus.Running)
        {
            latitude = Input.location.lastData.latitude;
            longitude = Input.location.lastData.longitude;
        }
        
        lastPosition = new Vector2(latitude, longitude);
        
        fetchCoroutine = StartCoroutine(FetchDataPeriodically());
        StartCoroutine(CheckPositionAndFetchData());
    }

    private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
#if UNITY_EDITOR
        // 에디터에서는 AR 세션 상태 변화 무시 (이미 Start에서 시작함)
        return;
#endif
        if (args.state == ARSessionState.SessionTracking && !isDataLoaded)
        {
            float lat = 37.5665f;
            float lon = 126.9780f;
            if (Input.location.status == LocationServiceStatus.Running)
            {
                lat = Input.location.lastData.latitude;
                lon = Input.location.lastData.longitude;
            }

            if (fetchCoroutine != null)
            {
                StopCoroutine(fetchCoroutine);
            }
            fetchCoroutine = StartCoroutine(FetchDataProgressively(lat, lon));
        }
    }

    private IEnumerator FetchDataPeriodically()
    {
        while (true)
        {
#if UNITY_EDITOR
            // 에디터에서는 AR 세션 추적 대기 생략
            float lat = 36.6361f;
            float lon = 126.8280f;
#else
            yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking);
            
            float lat = 37.5665f;
            float lon = 126.9780f;
            if (Input.location.status == LocationServiceStatus.Running)
            {
                lat = Input.location.lastData.latitude;
                lon = Input.location.lastData.longitude;
            }
#endif

            yield return StartCoroutine(FetchDataProgressively(lat, lon));
            isDataLoaded = true;
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private IEnumerator CheckPositionAndFetchData()
    {
        while (true)
        {
#if UNITY_EDITOR
            float lat = 36.6361f;
            float lon = 126.8280f;
#else
            float lat = 37.5665f;
            float lon = 126.9780f;
            if (Input.location.status == LocationServiceStatus.Running)
            {
                lat = Input.location.lastData.latitude;
                lon = Input.location.lastData.longitude;
            }
#endif
            
            Vector2 currentPos = new Vector2(lat, lon);
            float distanceMoved = CalculateDistance(lastPosition.x, lastPosition.y, currentPos.x, currentPos.y);
            
            if (distanceMoved > updateDistanceThreshold)
            {
                yield return StartCoroutine(FetchDataProgressively(lat, lon));
                lastPosition = currentPos;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator FetchDataProgressively(float lat, float lon)
    {
        HashSet<int> loadedIds = new HashSet<int>(spawnedObjects.Keys);

        // UI 리셋 (새로운 로드 시작)
        if (objectCountUI != null)
        {
            objectCountUI.ResetUI();
        }

        int currentTierCount = 0; // 현재 Tier까지의 누적 개수

        for (int tierIndex = 0; tierIndex < loadRadii.Length; tierIndex++)
        {
            float radius = loadRadii[tierIndex];
            string serverUrl = $"{baseServerUrl}&lat={lat}&lon={lon}&radius={radius}";

            List<PlaceData> newPlaces = new List<PlaceData>();
            yield return StartCoroutine(FetchDataFromServerForTier(serverUrl, lat, lon, loadedIds, newPlaces));

            // 새로운 오브젝트를 하나씩 스폰
            foreach (PlaceData place in newPlaces)
            {
                CreateObjectFromData(place);
                loadedIds.Add(place.id);
                currentTierCount++; // 현재 Tier 카운트 증가

                // UI 업데이트 (현재 Tier까지의 개수만 표시)
                if (objectCountUI != null)
                {
                    objectCountUI.UpdateObjectCount(currentTierCount, false);
                }

                if (objectSpawnDelay > 0)
                {
                    yield return new WaitForSeconds(objectSpawnDelay);
                }
            }

            // 마지막 Tier 완료 시 최종 업데이트
            if (tierIndex == loadRadii.Length - 1 && objectCountUI != null)
            {
                objectCountUI.UpdateObjectCount(currentTierCount, true);
            }

            if (tierIndex < loadRadii.Length - 1 && tierDelay > 0) yield return new WaitForSeconds(tierDelay);
        }

        isDataLoaded = true;
    }

    private IEnumerator FetchDataFromServerForTier(string url, float lat, float lon, HashSet<int> loadedIds, List<PlaceData> outNewPlaces)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;

                try
                {
                    List<PlaceData> places = JsonConvert.DeserializeObject<List<PlaceData>>(json);
                    if (places != null)
                    {
                        // ... 정렬 및 필터링 ...
                        // 거리순 정렬
                        places.Sort((a, b) =>
                        {
                            float distA = CalculateDistance(lat, lon, a.latitude, a.longitude);
                            float distB = CalculateDistance(lat, lon, b.latitude, b.longitude);
                            return distA.CompareTo(distB);
                        });

                        foreach (var place in places)
                        {
                            if (!loadedIds.Contains(place.id)) outNewPlaces.Add(place);
                        }
                    }
                    else
                    {
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DataManager] Error parsing JSON: {e.Message}");
                }
            }
            else
            {
            }
        }
    }

    private IEnumerator FetchDataFromServer(string url, LocationInfo currentLocation)
    {
        int retryCount = 3;
        for (int i = 0; i < retryCount; i++)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    yield return StartCoroutine(ProcessData(json, currentLocation));
                    break;
                }
                else
                {
                    if (i < retryCount - 1) 
                        yield return new WaitForSeconds(5f);
                    else 
                        ShowErrorMessage("서버에서 데이터를 받아오지 못했습니다.");
                }
            }
        }
    }

    private IEnumerator ProcessData(string json, LocationInfo currentLocation)
    {
        List<PlaceData> places = null;
        try
        {
            places = JsonConvert.DeserializeObject<List<PlaceData>>(json);
        }
        catch (JsonException ex)
        {
            ShowErrorMessage("데이터 파싱에 실패했습니다.");
            yield break;
        }

        if (places == null || places.Count == 0)
        {
            ShowErrorMessage("서버에서 데이터를 받아오지 못했습니다.");
            yield break;
        }

        // 거리 순으로 정렬하고 개수 제한
        places.Sort((a, b) => CalculateDistance(currentLocation.latitude, currentLocation.longitude, a.latitude, a.longitude)
            .CompareTo(CalculateDistance(currentLocation.latitude, currentLocation.longitude, b.latitude, b.longitude)));
        places = places.Take(poolSize * 2).ToList();

        // 청크 단위로 처리
        const int CHUNK_SIZE = 5; // 청크 크기 줄임
        for (int i = 0; i < places.Count; i += CHUNK_SIZE)
        {
            var chunk = places.Skip(i).Take(CHUNK_SIZE).ToList();
            foreach (PlaceData place in chunk)
            {
                // 서버 데이터 상세 로그 제거 (lat, lon, distance, type 등)
                // Debug.Log($"[DataManager] 데이터 처리: ID={place.id}, 이름={place.name}"); // 디버깅용
                
                if (!spawnedObjects.ContainsKey(place.id))
                {
                    CreateObjectFromData(place);
                    if (spawnedObjects.ContainsKey(place.id))
                    {
                        placeDataMap[place.id] = place;
                        // Debug.Log($"[DataManager] 맵에 추가됨: ID={place.id}");
                    }
                }
                else
                {
                    UpdateExistingObject(place, spawnedObjects[place.id]);
                    placeDataMap[place.id] = place;
                    // Debug.Log($"[DataManager] 맵 업데이트됨: ID={place.id}");
                }
            }
            yield return null; // 프레임 양보
        }

        // 범위 밖 오브젝트 제거
        HashSet<int> receivedIds = new HashSet<int>(places.Select(p => p.id));
        List<int> toRemove = spawnedObjects.Keys.Where(id => !receivedIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            GameObject obj = spawnedObjects[id];
            PlaceData placeData = placeDataMap.ContainsKey(id) ? placeDataMap[id] : null;
            string modelType = placeData?.model_type ?? "cube";
            spawnedObjects.Remove(id);
            placeDataMap.Remove(id);
            currentlyLoadingGLB.Remove(id);
            ReturnToPool(obj, modelType);
        }
    }

    private void CreateObjectFromData(PlaceData place)
    {

        // GLB 동시 로딩 제한
        if (place.model_type == "custom" && currentlyLoadingGLB.Count >= maxConcurrentGLBLoads)
        {
            if (fallbackToCube)
            {
                place.model_type = "cube"; // 큐브로 fallback
            }
            else
            {
                return; // 로딩 제한으로 건너뛰기
            }
        }

        GameObject newObj = GetFromPool(place.model_type);
        if (newObj == null)
        {
            return;
        }

        newObj.SetActive(true);
        newObj.name = $"Place_{place.id}_{place.model_type}";

        bool setupSuccess = SetupObjectComponents(newObj, place);

        if (setupSuccess)
        {
            spawnedObjects[place.id] = newObj;
            placeDataMap[place.id] = place; // ⭐ PlaceListManager가 사용하는 데이터 맵에 추가
        }
        else
        {
            ReturnToPool(newObj, place.model_type);
        }
    }

    private void UpdateExistingObject(PlaceData place, GameObject existingObj)
    {
        SetupObjectComponents(existingObj, place);
        placeDataMap[place.id] = place; // ⭐ 업데이트된 데이터도 맵에 반영
    }

    private bool SetupObjectComponents(GameObject obj, PlaceData place)
    {

        // GPS 앵커 설정
        CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true); // includeInactive=true
        if (anchor == null)
        {
            return false;
        }
        anchor.SetCoordinatesAndCreateAnchor(place.latitude, place.longitude, place.altitude);

        // 서브사진 설정
        ImageDisplayController displayCtrl = obj.GetComponentInChildren<ImageDisplayController>(true); // includeInactive=true
        if (displayCtrl != null && place.sub_photos != null && place.sub_photos.Count > 0)
        {
            List<string> allSubPhotos = new List<string>();
            foreach (var photoGroup in place.sub_photos)
            {
                if (photoGroup != null)
                {
                    foreach (var photo in photoGroup)
                    {
                        if (!string.IsNullOrEmpty(photo)) allSubPhotos.Add(photo);
                    }
                }
            }
            displayCtrl.SetSubPhotos(allSubPhotos);
        }

        // model_type에 따른 분기 처리
        bool result;
        if (place.model_type == "cube")
        {
            result = SetupCubeObject(obj, place);
        }
        else if (place.model_type == "custom")
        {
            result = SetupGLBObject(obj, place);
        }
        else
        {
            result = SetupCubeObject(obj, place); // 기본값으로 cube 처리
        }

        return result;
    }

    private bool SetupCubeObject(GameObject obj, PlaceData place)
    {

        // 큐브 텍스처 설정
        ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>(true); // includeInactive=true
        if (display != null && !string.IsNullOrEmpty(place.main_photo))
        {
            display.SetBaseMap(place.main_photo);
        }
        else
        {
        }

        // DoubleTap3D 설정
        DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>(true); // includeInactive=true
        if (doubleTap == null)
        {
            return false;
        }
        SetupDoubleTapInfo(doubleTap, place);

        // Target 설정
        Target target = obj.GetComponentInChildren<Target>(true); // includeInactive=true
        if (target == null)
        {
            return false;
        }
        SetupTargetInfo(target, place);

        return true;
    }

    private bool SetupGLBObject(GameObject obj, PlaceData place)
    {
        if (string.IsNullOrEmpty(place.model_url))
        {
            // GLB URL이 없으면 큐브로 fallback
            if (fallbackToCube)
            {
                place.model_type = "cube";
                return SetupCubeObject(obj, place);
            }
            return false;
        }

        GLBModelLoader glbLoader = obj.GetComponent<GLBModelLoader>();
        if (glbLoader == null)
        {
            glbLoader = obj.AddComponent<GLBModelLoader>();
        }
        
        glbLoader.ClearModel();
        
        // GLB 로딩 시작
        string fullUrl = "https://woopang.com/" + place.model_url;
        float scale = place.model_scale > 0 ? place.model_scale : 1.0f;
        
        // 로딩 중인 GLB 추가
        currentlyLoadingGLB.Add(place.id);
        
        StartCoroutine(LoadGLBAsync(glbLoader, fullUrl, scale, place.id, obj, place));

        return true;
    }

    private void SetupDoubleTapInfo(DoubleTap3D doubleTap, PlaceData place)
    {
        Sprite petFriendlySprite = Resources.Load<Sprite>("Sprites/pet_friendly_icon") ?? Resources.Load<Sprite>("Sprites/default_icon");
        Sprite restroomSprite = Resources.Load<Sprite>("Sprites/separate_restroom_icon") ?? Resources.Load<Sprite>("Sprites/default_icon");
        
        doubleTap.SetInfoImages(petFriendlySprite, restroomSprite, place.pet_friendly, place.separate_restroom, place.instagram_id, place.name, place.id, place.username, place.instagram_id);
    }

    private void SetupTargetInfo(Target target, PlaceData place)
    {
        Color placeColor;
        string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;
        if (ColorUtility.TryParseHtmlString($"#{colorHex}", out placeColor))
        {
            target.TargetColor = placeColor;
        }
        else
        {
            target.TargetColor = Color.white;
        }
        target.PlaceName = place.name;
    }

    private IEnumerator LoadGLBAsync(GLBModelLoader loader, string url, float scale, int placeId, GameObject glbObj, PlaceData place)
    {
        
        bool loadCompleted = false;
        bool loadSuccess = false;
        
        // 타임아웃 처리
        float startTime = Time.time;
        
        // GLB 로딩을 여러번 시도
        int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            
            loadCompleted = false;
            loadSuccess = false;
            
            StartCoroutine(LoadGLBCoroutine(loader, url, scale, (success) => {
                loadSuccess = success;
                loadCompleted = true;
            }));
            
            // 로딩 완료 또는 타임아웃까지 대기
            while (!loadCompleted && (Time.time - startTime) < glbLoadTimeout)
            {
                yield return null;
            }
            
            if (loadCompleted && loadSuccess)
            {
                break;
            }
            else
            {
                
                // 마지막 시도가 아니면 잠시 대기 후 재시도
                if (attempt < maxAttempts)
                {
                    yield return new WaitForSeconds(attempt + 1);
                    
                    // GLBModelLoader 리셋
                    loader.ClearModel();
                }
            }
        }
        
        // 로딩 상태에서 제거
        currentlyLoadingGLB.Remove(placeId);
        
        if (loadCompleted && loadSuccess)
        {
            
            // GLB 로딩 성공 시 UI 컴포넌트 설정
            DoubleTap3D doubleTap = glbObj.GetComponentInChildren<DoubleTap3D>();
            if (doubleTap != null)
            {
                SetupDoubleTapInfo(doubleTap, place);
            }

            Target target = glbObj.GetComponentInChildren<Target>();
            if (target != null)
            {
                SetupTargetInfo(target, place);
            }
        }
        else
        {
            
            // GLB 로딩 실패 시 처리
            if (fallbackToCube && spawnedObjects.ContainsKey(placeId))
            {
                
                // 큐브로 대체
                ReturnToPool(glbObj, "custom");
                spawnedObjects.Remove(placeId);
                
                place.model_type = "cube";
                CreateObjectFromData(place);
            }
            else
            {
                glbObj.SetActive(false);
            }
        }
    }

    private IEnumerator LoadGLBCoroutine(GLBModelLoader loader, string url, float scale, System.Action<bool> onComplete)
    {
        yield return StartCoroutine(loader.LoadGLBModelCoroutine(url, scale, onComplete));
    }

    private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000;
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(Mathf.Deg2Rad * (lat1)) * Mathf.Cos(Mathf.Deg2Rad * (lat2)) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
        return R * c;
    }

    private GameObject GetFromPool(string modelType)
    {
        Queue<GameObject> targetPool = modelType == "cube" ? cubeObjectPool : glbObjectPool;
        string poolName = modelType == "cube" ? "Cube" : "GLB";


        if (targetPool.Count > 0)
        {
            GameObject obj = targetPool.Dequeue();
            ResetObjectState(obj, modelType);
            obj.SetActive(true);
            obj.name = $"Place_ID_{modelType}";
            return obj;
        }
        else if (spawnedObjects.Count < poolSize * 4)
        {
            GameObject prefab = modelType == "cube" ? cubePrefab : glbPrefab;
            GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            ResetObjectState(obj, modelType);
            obj.name = $"Place_ID_{modelType}";
            return obj;
        }

        ShowErrorMessage("너무 많은 장소가 로드되었습니다.");
        return null;
    }

    private void ResetObjectState(GameObject obj, string modelType)
    {
        // DoubleTap3D 리셋
        DoubleTap3D[] doubleTaps = obj.GetComponentsInChildren<DoubleTap3D>(true);
        foreach (var doubleTap in doubleTaps)
        {
            doubleTap.ResetData();
        }
        
        // GLB 타입인 경우에만 GLBModelLoader 정리
        if (modelType == "custom")
        {
            GLBModelLoader glbLoader = obj.GetComponent<GLBModelLoader>();
            if (glbLoader != null)
            {
                glbLoader.ClearModel();
            }
        }
    }

    private void ReturnToPool(GameObject obj, string modelType)
    {
        Queue<GameObject> targetPool = modelType == "cube" ? cubeObjectPool : glbObjectPool;
        
        // GLB 타입인 경우에만 모델 정리
        if (modelType == "custom")
        {
            GLBModelLoader glbLoader = obj.GetComponent<GLBModelLoader>();
            if (glbLoader != null)
            {
                glbLoader.ClearModel();
            }
        }
        
        obj.SetActive(false);
        targetPool.Enqueue(obj);
    }

    public Dictionary<int, GameObject> GetSpawnedObjects() => spawnedObjects;
    public int GetSpawnedObjectsCount() => spawnedObjects.Count;
    public Dictionary<int, PlaceData> GetPlaceDataMap() => placeDataMap;
    public bool IsDataLoaded() => isDataLoaded;

    public GameObject GetSpawnedObject(int placeId)
    {
        return spawnedObjects.ContainsKey(placeId) ? spawnedObjects[placeId] : null;
    }

    public void ApplyFilters(Dictionary<string, bool> filters)
    {
        if (filters == null) return;

        bool showPetFriendly = filters.ContainsKey("petFriendly") && filters["petFriendly"];
        bool showAlcohol = filters.ContainsKey("alcohol") && filters["alcohol"];
        bool showWoopangData = filters.ContainsKey("woopangData") && filters["woopangData"];
        bool showObject3D = !filters.ContainsKey("object3D") || filters["object3D"];

        foreach (var kvp in spawnedObjects)
        {
            int placeId = kvp.Key;
            GameObject obj = kvp.Value;
            if (obj == null) continue;

            if (!showObject3D)
            {
                obj.SetActive(false);
                continue;
            }

            if (placeDataMap.ContainsKey(placeId))
            {
                PlaceData place = placeDataMap[placeId];
                bool shouldShow = showWoopangData;

                if (shouldShow)
                {
                    if (place.pet_friendly && !showPetFriendly) shouldShow = false;
                    else if (place.alcohol_available && !showAlcohol) shouldShow = false;
                }
                obj.SetActive(shouldShow);
            }
        }
    }

    public void UpdateDistanceFilter(float maxDistance, float currentLat, float currentLon)
    {
        foreach (var kvp in spawnedObjects)
        {
            int id = kvp.Key;
            GameObject obj = kvp.Value;
            if (obj == null) continue;

            if (placeDataMap.ContainsKey(id))
            {
                PlaceData place = placeDataMap[id];
                float dist = CalculateDistance(currentLat, currentLon, place.latitude, place.longitude);
                bool inRange = dist <= maxDistance;
                if (!inRange) 
                {
                    if (obj.activeSelf) obj.SetActive(false);
                }
                else
                {
                    if (!obj.activeSelf) obj.SetActive(true);
                }
            }
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && Input.location.isEnabledByUser)
        {
            StartCoroutine(WaitForARSessionAndFetchData());
        }
    }

    private IEnumerator WaitForARSessionAndFetchData()
    {
        yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking || Time.unscaledTime > 5f);
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            ShowErrorMessage("AR 세션을 복구할 수 없습니다.");
            yield break;
        }

        if (fetchCoroutine != null)
        {
            StopCoroutine(fetchCoroutine);
        }
        
        float lat = 37.5665f;
        float lon = 126.9780f;
        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }
        
        fetchCoroutine = StartCoroutine(FetchDataProgressively(lat, lon));
    }

    private IEnumerator FetchDataImmediately(string url, LocationInfo currentLocation)
    {
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            yield break;
        }
        yield return StartCoroutine(FetchDataFromServer(url, currentLocation));
        fetchCoroutine = StartCoroutine(FetchDataPeriodically());
    }

    private void ShowErrorMessage(string message)
    {
        var errorPanel = GameObject.Find("ErrorPanel")?.GetComponent<Text>();
        if (errorPanel != null)
        {
            errorPanel.text = message;
            errorPanel.gameObject.SetActive(true);
        }
    }

    void OnDestroy()
    {
        Input.location.Stop();
    }
}

[System.Serializable]
public class PlaceData
{
    public int id { get; set; }
    public string name { get; set; }
    public string main_photo { get; set; }
    public List<List<string>> sub_photos { get; set; }
    public bool pet_friendly { get; set; }
    public bool separate_restroom { get; set; }
    public bool alcohol_available { get; set; } // 주류 판매 여부
    public string instagram_id { get; set; }
    public float latitude { get; set; }
    public float longitude { get; set; }
    public float altitude { get; set; }
    public string color { get; set; }
    public string username { get; set; }
    public string model_type { get; set; } = "cube";
    public string model_url { get; set; }
    public float model_scale { get; set; } = 1f;
}