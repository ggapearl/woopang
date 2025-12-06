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
    private string baseServerUrl = "https://woopang.com/locations?status=approved";
    
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
    
    [Header("Object Pool Settings")]
    [Tooltip("큐브 프리팹 풀 사이즈")]
    [SerializeField] public int cubePoolSize = 30;

    [Tooltip("GLB 프리팹 풀 사이즈 (큐브보다 덜 필요)")]
    [SerializeField] public int glbPoolSize = 15;

    [SerializeField] private float updateInterval = 600f;

    [Header("Progressive Loading Settings")]
    [Tooltip("거리별 로딩 단계 (미터)")]
    public float[] loadRadii = new float[] { 25f, 50f, 75f, 100f, 150f, 200f, 500f, 1000f, 2000f, 5000f, 10000f };

    [Tooltip("각 거리 단계 사이의 딜레이 (초) - 깜빡임 방지")]
    public float tierDelay = 0.1f;  // 가까운 거리는 즉시 로드

    [Tooltip("같은 단계 내 오브젝트 사이의 딜레이 (초) - 안정적 생성")]
    public float objectSpawnDelay = 0.05f;  // 빠른 생성

    [Header("Pool Initialization Settings")]
    [Tooltip("Pool 초기화 시 프레임당 생성할 오브젝트 수")]
    [SerializeField] private int objectsPerFrame = 2;  // 프레임당 2개씩 생성 (부드러운 초기화)

    [SerializeField] private float updateDistanceThreshold = 50f;
    private bool isDataLoaded = false;
    private bool isDataLoading = false; // 중복 로딩 방지 플래그
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
        // ✅ 분산 초기화로 변경 (프레임 끊김 방지)
        StartCoroutine(InitializeObjectPoolsAsync());
        StartCoroutine(StartLocationServiceAndFetchData());
    }

    /// <summary>
    /// Object Pool을 여러 프레임에 걸쳐 초기화 (부드러운 앱 시작)
    /// </summary>
    private IEnumerator InitializeObjectPoolsAsync()
    {
        if (cubePrefab == null || glbPrefab == null)
        {
            Debug.LogError("[DataManager] Prefab이 설정되지 않음!");
            yield break;
        }

        float startTime = Time.realtimeSinceStartup;

        // Cube 오브젝트 풀 초기화 (프레임당 objectsPerFrame개씩)
        for (int i = 0; i < cubePoolSize; i++)
        {
            GameObject cubeObj = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);
            cubeObj.SetActive(false);
            cubeObjectPool.Enqueue(cubeObj);

            // 프레임당 objectsPerFrame개씩 생성 후 다음 프레임으로
            if ((i + 1) % objectsPerFrame == 0)
            {
                yield return null;
            }
        }
        // GLB 오브젝트 풀 초기화 (프레임당 objectsPerFrame개씩)
        for (int i = 0; i < glbPoolSize; i++)
        {
            GameObject glbObj = Instantiate(glbPrefab, Vector3.zero, Quaternion.identity);
            glbObj.SetActive(false);
            glbObjectPool.Enqueue(glbObj);

            // 프레임당 objectsPerFrame개씩 생성 후 다음 프레임으로
            if ((i + 1) % objectsPerFrame == 0)
            {
                yield return null;
            }
        }

        float elapsedTime = Time.realtimeSinceStartup - startTime;
        Debug.Log($"[DataManager] 풀 초기화 완료 - 총 {cubePoolSize + glbPoolSize}개");
    }

    private IEnumerator StartLocationServiceAndFetchData()
    {
        if (!Input.location.isEnabledByUser)
        {
            ShowErrorMessage("위치 서비스를 활성화해 주세요.");
            Debug.LogError("[DataManager] GPS 권한 없음!");
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
            Debug.LogWarning("[DataManager] GPS 실패 - 기본 위치 사용");
        }

        // 위치 데이터가 없으면 기본값 사용
        float lat = 37.5665f;
        float lon = 126.9780f;

        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }
        
        lastPosition = new Vector2(lat, lon);
        
        // 바로 로딩 시작
        LocationInfo fakeLocation = new LocationInfo();
        // LocationInfo는 struct라 프로퍼티 설정 불가하므로 별도 변수 사용하거나
        // FetchDataProgressively가 LocationInfo 대신 lat, lon을 받도록 수정해야 함.
        // 하지만 구조 변경을 최소화하기 위해, FetchDataProgressively 호출 전에
        // Input.location.lastData를 쓰지 않고 직접 값을 넘기거나 해야 함.
        
        // 가장 쉬운 방법: FetchDataProgressively는 LocationInfo를 받지만, 
        // 내부에서 lat/lon을 쓸 때 Input.location.status를 체크해서 쓰도록 수정되어 있음?
        // 아니요, currentLocation.latitude를 씁니다.
        
        // 따라서 LocationInfo를 가짜로 만들어서 넘겨야 함. (하지만 readonly라 불가능)
        // 해결책: FetchDataProgressively의 파라미터를 (float lat, float lon)으로 변경.
        
        fetchCoroutine = StartCoroutine(FetchDataPeriodically());
        StartCoroutine(CheckPositionAndFetchData());
    }

    private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
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
            yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking);
            
            float lat = 37.5665f;
            float lon = 126.9780f;
            if (Input.location.status == LocationServiceStatus.Running)
            {
                lat = Input.location.lastData.latitude;
                lon = Input.location.lastData.longitude;
            }

            yield return StartCoroutine(FetchDataProgressively(lat, lon));
            isDataLoaded = true;
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private IEnumerator CheckPositionAndFetchData()
    {
        while (true)
        {
            float lat = 37.5665f;
            float lon = 126.9780f;
            if (Input.location.status == LocationServiceStatus.Running)
            {
                lat = Input.location.lastData.latitude;
                lon = Input.location.lastData.longitude;
            }
            
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
        Debug.Log($"[DataManager] FetchDataProgressively 시작 - isDataLoading={isDataLoading}");

        // 이미 로딩 중이면 중복 실행 방지
        if (isDataLoading)
        {
            Debug.LogWarning("[DataManager] 이미 데이터 로딩 중입니다. 중복 실행 방지.");
            yield break;
        }

        isDataLoading = true;

        // 로딩 표시 시작
        if (LoadingIndicator.Instance != null)
        {
            LoadingIndicator.Instance.Show("주변 장소를 불러오는 중...");
        }

        // 발견 알림 시작
        if (PlaceDiscoveryNotification.Instance != null)
        {
            Debug.Log("[DataManager] PlaceDiscoveryNotification.StartDiscovery() 호출");
            PlaceDiscoveryNotification.Instance.StartDiscovery();
        }
        else
        {
            Debug.LogWarning("[DataManager] PlaceDiscoveryNotification.Instance가 null입니다!");
        }

        HashSet<int> loadedIds = new HashSet<int>(spawnedObjects.Keys);

        // DistanceSliderUI에서 설정한 최대 거리 가져오기
        float maxDistance = GetUserMaxDistance();

        for (int tierIndex = 0; tierIndex < loadRadii.Length; tierIndex++)
        {
            float radius = loadRadii[tierIndex];

            // 사용자 설정 범위를 초과하면 중단
            if (radius > maxDistance)
            {
                break;
            }

            string serverUrl = $"{baseServerUrl}&lat={lat}&lon={lon}&radius={radius}";

            List<PlaceData> newPlaces = new List<PlaceData>();
            yield return StartCoroutine(FetchDataFromServerForTier(serverUrl, lat, lon, loadedIds, newPlaces));

            // 새로 발견한 장소 개수 알림 업데이트
            if (newPlaces.Count > 0 && PlaceDiscoveryNotification.Instance != null)
            {
                PlaceDiscoveryNotification.Instance.UpdateDiscoveredCount(newPlaces.Count);
            }

            foreach (PlaceData place in newPlaces)
            {
                CreateObjectFromData(place);
                loadedIds.Add(place.id);

                if (objectSpawnDelay > 0)
                {
                    yield return new WaitForSeconds(objectSpawnDelay);
                }
            }

            if (tierIndex < loadRadii.Length - 1 && tierDelay > 0) yield return new WaitForSeconds(tierDelay);
        }

        Debug.Log($"[DataManager] 로딩 완료 - 총 {spawnedObjects.Count}개 오브젝트");

        // 발견 알림 완료
        if (PlaceDiscoveryNotification.Instance != null)
        {
            Debug.Log("[DataManager] PlaceDiscoveryNotification.CompleteDiscovery() 호출");
            PlaceDiscoveryNotification.Instance.CompleteDiscovery();
        }

        // 로딩 표시 종료
        if (LoadingIndicator.Instance != null)
        {
            LoadingIndicator.Instance.Hide();
        }

        isDataLoaded = true;
        isDataLoading = false; // 로딩 완료

        Debug.Log("[DataManager] FetchDataProgressively 완료");
    }

    /// <summary>
    /// PlaceListManager에서 설정한 최대 거리 가져오기
    /// </summary>
    private float GetUserMaxDistance()
    {
        PlaceListManager placeListManager = FindObjectOfType<PlaceListManager>();
        if (placeListManager != null)
        {
            return placeListManager.GetMaxDisplayDistance();
        }
        // 기본값: 모든 tier 처리 (10000m)
        return 10000f;
    }

    /// <summary>
    /// 백그라운드에서 포그라운드로 전환 시 데이터 재로딩
    /// </summary>
    public void ReloadDataOnForeground()
    {
        Debug.Log("[DataManager] ReloadDataOnForeground 호출 - 데이터 재로딩 시작");

        // 현재 위치 다시 가져오기
        if (Input.location.status == LocationServiceStatus.Running)
        {
            float lat = Input.location.lastData.latitude;
            float lon = Input.location.lastData.longitude;

            // 기존 로딩 중단
            if (fetchCoroutine != null)
            {
                StopCoroutine(fetchCoroutine);
            }

            // 재로딩 시작
            isDataLoaded = false;
            isDataLoading = false;
            fetchCoroutine = StartCoroutine(FetchDataProgressively(lat, lon));

            Debug.Log($"[DataManager] 재로딩 시작 - 위치: {lat}, {lon}");
        }
        else
        {
            Debug.LogWarning("[DataManager] 위치 서비스가 실행 중이 아닙니다.");
        }
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
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DataManager] JSON 파싱 실패: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[DataManager] 서버 요청 실패: {request.error}");
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
            Debug.LogError($"[DataManager] JSON 파싱 실패: {ex.Message}");
            ShowErrorMessage("데이터 파싱에 실패했습니다.");
            yield break;
        }

        if (places == null || places.Count == 0)
        {
            Debug.LogWarning("[DataManager] 파싱된 장소 데이터 없음");
            ShowErrorMessage("서버에서 데이터를 받아오지 못했습니다.");
            yield break;
        }

        // 거리 순으로 정렬하고 개수 제한
        places.Sort((a, b) => CalculateDistance(currentLocation.latitude, currentLocation.longitude, a.latitude, a.longitude)
            .CompareTo(CalculateDistance(currentLocation.latitude, currentLocation.longitude, b.latitude, b.longitude)));
        places = places.Take(cubePoolSize + glbPoolSize).ToList();

        // 청크 단위로 처리
        const int CHUNK_SIZE = 5; // 청크 크기 줄임
        for (int i = 0; i < places.Count; i += CHUNK_SIZE)
        {
            var chunk = places.Skip(i).Take(CHUNK_SIZE).ToList();
            foreach (PlaceData place in chunk)
            {
                if (!spawnedObjects.ContainsKey(place.id))
                {
                    CreateObjectFromData(place);
                    if (spawnedObjects.ContainsKey(place.id))
                    {
                        placeDataMap[place.id] = place;
                    }
                }
                else
                {
                    UpdateExistingObject(place, spawnedObjects[place.id]);
                    placeDataMap[place.id] = place;
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
                place.model_type = "cube";
            }
            else
            {
                return;
            }
        }

        GameObject newObj = GetFromPool(place.model_type);
        if (newObj == null)
        {
            Debug.LogWarning($"[DataManager] 풀 부족 - ID={place.id}");
            return;
        }

        newObj.SetActive(true);
        newObj.name = $"Place_{place.id}_{place.model_type}";

        bool setupSuccess = SetupObjectComponents(newObj, place);

        if (setupSuccess)
        {
            spawnedObjects[place.id] = newObj;
            placeDataMap[place.id] = place;
        }
        else
        {
            Debug.LogWarning($"[DataManager] 오브젝트 설정 실패 - ID={place.id}");
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
        CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
        if (anchor == null)
        {
            Debug.LogError($"[DataManager] CustomARGeospatialCreatorAnchor 없음: ID={place.id}");
            return false;
        }
        anchor.SetCoordinatesAndCreateAnchor(place.latitude, place.longitude, place.altitude);

        // 서브사진 설정
        ImageDisplayController displayCtrl = obj.GetComponentInChildren<ImageDisplayController>(true);
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
        ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>(true);
        if (display != null && !string.IsNullOrEmpty(place.main_photo))
        {
            display.SetBaseMap(place.main_photo);
        }

        // DoubleTap3D 설정
        DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>(true);
        if (doubleTap == null)
        {
            Debug.LogError($"[DataManager] DoubleTap3D 컴포넌트 없음: ID={place.id}");
            return false;
        }
        SetupDoubleTapInfo(doubleTap, place);

        // Target 설정
        Target target = obj.GetComponentInChildren<Target>(true);
        if (target == null)
        {
            Debug.LogError($"[DataManager] Target 컴포넌트 없음: ID={place.id}");
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
            Debug.LogWarning($"[DataManager] GLB 로딩 실패 - ID: {placeId}");

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
        else if (spawnedObjects.Count < (cubePoolSize + glbPoolSize) * 2)
        {
            Debug.LogWarning($"[DataManager] {poolName} 풀 부족 - 동적 생성");
            GameObject prefab = modelType == "cube" ? cubePrefab : glbPrefab;
            GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            ResetObjectState(obj, modelType);
            obj.name = $"Place_ID_{modelType}";
            return obj;
        }

        Debug.LogError($"[DataManager] {poolName} 풀 한계 초과");
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
        Debug.LogError($"[DataManager] 에러: {message}");
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