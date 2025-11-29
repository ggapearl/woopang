using System;
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

    [Header("Loading Indicator")]
    [Tooltip("로딩 중 표시할 3D 구형 인디케이터")]
    [SerializeField] private GameObject loadingIndicator;
    
    private Dictionary<int, GameObject> spawnedObjects = new Dictionary<int, GameObject>();
    private Dictionary<int, PlaceData> placeDataMap = new Dictionary<int, PlaceData>();
    private Queue<GameObject> cubeObjectPool = new Queue<GameObject>();
    private Queue<GameObject> glbObjectPool = new Queue<GameObject>();
    private HashSet<int> currentlyLoadingGLB = new HashSet<int>();
    
    [SerializeField] public int poolSize = 20;
    [SerializeField] private float updateInterval = 600f;

    [Header("Progressive Loading Settings")]
    [Tooltip("거리별 로딩 단계 (미터): 25m → 50m → 75m → 100m → 150m → 200m")]
    public float[] loadRadii = new float[] { 25f, 50f, 75f, 100f, 150f, 200f };

    [Tooltip("각 거리 단계 사이의 딜레이 (초)")]
    public float tierDelay = 1.0f;

    [Tooltip("같은 단계 내 오브젝트 사이의 딜레이 (초)")]
    public float objectSpawnDelay = 0.5f;

    [Header("Distance Filter")]
    [Tooltip("PlaceListManager 참조 (거리 필터 동기화)")]
    [SerializeField] private PlaceListManager placeListManager;

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
        InitializeObjectPools();
        StartCoroutine(StartLocationServiceAndFetchData());
    }

    private void InitializeObjectPools()
    {
        if (cubePrefab == null || glbPrefab == null)
        {
            Debug.LogError("[DataManager] Prefab이 설정되지 않음!");
            return;
        }
        
        // Cube 오브젝트 풀 초기화
        for (int i = 0; i < poolSize; i++)
        {
            GameObject cubeObj = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);
            cubeObj.SetActive(false);
            cubeObjectPool.Enqueue(cubeObj);
        }
        
        // GLB 오브젝트 풀 초기화
        for (int i = 0; i < poolSize; i++)
        {
            GameObject glbObj = Instantiate(glbPrefab, Vector3.zero, Quaternion.identity);
            glbObj.SetActive(false);
            glbObjectPool.Enqueue(glbObj);
        }
    }

    private IEnumerator StartLocationServiceAndFetchData()
    {
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
            ShowErrorMessage("위치 서비스를 시작할 수 없습니다.");
            yield break;
        }
        
        lastPosition = new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        fetchCoroutine = StartCoroutine(FetchDataPeriodically());
        StartCoroutine(CheckPositionAndFetchData());
    }

    private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
        if (args.state == ARSessionState.SessionTracking && !isDataLoaded)
        {
            LocationInfo currentLocation = Input.location.lastData;
            if (fetchCoroutine != null)
            {
                StopCoroutine(fetchCoroutine);
            }
            fetchCoroutine = StartCoroutine(FetchDataProgressively(currentLocation));
        }
    }

    private IEnumerator FetchDataPeriodically()
    {
        while (true)
        {
            yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking);
            LocationInfo currentLocation = Input.location.lastData;
            yield return StartCoroutine(FetchDataProgressively(currentLocation));
            isDataLoaded = true;
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private IEnumerator CheckPositionAndFetchData()
    {
        while (true)
        {
            LocationInfo currentLocation = Input.location.lastData;
            Vector2 currentPos = new Vector2(currentLocation.latitude, currentLocation.longitude);
            float distanceMoved = CalculateDistance(lastPosition.x, lastPosition.y, currentPos.x, currentPos.y);

            if (distanceMoved > updateDistanceThreshold)
            {
                yield return StartCoroutine(FetchDataProgressively(currentLocation));
                lastPosition = currentPos;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// 점진적 데이터 로딩 - 거리별 단계로 나누어 로딩하여 앱 끊김 방지
    /// 25m → 50m → 75m → 100m → 150m → 200m 순으로 로딩
    /// </summary>
    private IEnumerator FetchDataProgressively(LocationInfo currentLocation)
    {
        Debug.Log("[DataManager] ===== 점진적 로딩 시작 =====");

        // 로딩 인디케이터 표시
        ShowLoadingIndicator(true);

        // 이미 로드된 오브젝트 ID 추적 (중복 방지)
        HashSet<int> loadedIds = new HashSet<int>(spawnedObjects.Keys);

        // 각 거리 단계별로 데이터 로딩
        for (int tierIndex = 0; tierIndex < loadRadii.Length; tierIndex++)
        {
            float radius = loadRadii[tierIndex];
            Debug.Log($"[DataManager] 단계 {tierIndex + 1}/{loadRadii.Length}: {radius}m 이내 오브젝트 로딩 중...");

            // 서버에서 현재 반경 내 데이터 가져오기
            string serverUrl = $"{baseServerUrl}&lat={currentLocation.latitude}&lon={currentLocation.longitude}&radius={radius}";

            List<PlaceData> newPlaces = new List<PlaceData>();
            yield return StartCoroutine(FetchDataFromServerForTier(serverUrl, currentLocation, loadedIds, newPlaces));

            // 새로운 오브젝트를 하나씩 0.5초 간격으로 스폰
            foreach (PlaceData place in newPlaces)
            {
                CreateObjectFromData(place);
                loadedIds.Add(place.id);

                // 오브젝트 사이 딜레이
                if (objectSpawnDelay > 0)
                {
                    yield return new WaitForSeconds(objectSpawnDelay);
                }
            }

            Debug.Log($"[DataManager] 단계 {tierIndex + 1} 완료 - {newPlaces.Count}개 오브젝트 추가됨 (총 {loadedIds.Count}개)");

            // 다음 단계로 넘어가기 전 딜레이 (마지막 단계는 제외)
            if (tierIndex < loadRadii.Length - 1 && tierDelay > 0)
            {
                yield return new WaitForSeconds(tierDelay);
            }
        }

        Debug.Log($"[DataManager] ===== 점진적 로딩 완료 - 총 {loadedIds.Count}개 오브젝트 =====");

        // 로딩 인디케이터 숨김
        ShowLoadingIndicator(false);
    }

    /// <summary>
    /// 특정 거리 단계의 데이터를 서버에서 가져옴
    /// </summary>
    private IEnumerator FetchDataFromServerForTier(string url, LocationInfo currentLocation, HashSet<int> loadedIds, List<PlaceData> outNewPlaces)
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

                    // JSON 파싱
                    try
                    {
                        List<PlaceData> places = JsonConvert.DeserializeObject<List<PlaceData>>(json);
                        if (places == null) places = new List<PlaceData>();

                        // 거리순 정렬
                        places.Sort((a, b) =>
                        {
                            float distA = CalculateDistance(currentLocation.latitude, currentLocation.longitude, a.latitude, a.longitude);
                            float distB = CalculateDistance(currentLocation.latitude, currentLocation.longitude, b.latitude, b.longitude);
                            return distA.CompareTo(distB);
                        });

                        // 이미 로드된 오브젝트는 제외하고 새로운 것만 추가
                        foreach (PlaceData place in places)
                        {
                            if (!loadedIds.Contains(place.id))
                            {
                                outNewPlaces.Add(place);
                            }
                        }

                        // poolSize 제한 적용 (총 오브젝트 수 제한)
                        int maxNewObjects = (poolSize * 2) - loadedIds.Count;
                        if (outNewPlaces.Count > maxNewObjects && maxNewObjects > 0)
                        {
                            outNewPlaces.RemoveRange(maxNewObjects, outNewPlaces.Count - maxNewObjects);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[DataManager] JSON 파싱 실패: {e.Message}");
                    }

                    break;
                }
                else
                {
                    if (i < retryCount - 1)
                    {
                        Debug.LogWarning($"[DataManager] 데이터 로딩 실패 (재시도 {i + 1}/{retryCount})");
                        yield return new WaitForSeconds(2f);
                    }
                    else
                    {
                        Debug.LogError("[DataManager] 서버에서 데이터를 받아오지 못했습니다.");
                    }
                }
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
        Debug.Log("[DataManager] ProcessData 호출됨");
        List<PlaceData> places = null;
        try
        {
            places = JsonConvert.DeserializeObject<List<PlaceData>>(json);
            Debug.Log($"[DataManager] 파싱된 장소 수: {places?.Count ?? 0}");
        }
        catch (JsonException ex)
        {
            Debug.LogError($"[DataManager] JSON 파싱 실패: {ex.Message}");
            ShowErrorMessage("데이터 파싱에 실패했습니다.");
            yield break;
        }

        if (places == null || places.Count == 0)
        {
            Debug.LogError("[DataManager] 파싱된 장소 데이터가 없거나 비어 있습니다!");
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
                
                if (!spawnedObjects.ContainsKey(place.id))
                {
                    CreateObjectFromData(place);
                    if (spawnedObjects.ContainsKey(place.id))
                    {
                        placeDataMap[place.id] = place;
                        Debug.Log($"[DataManager] 새 오브젝트 생성 - ID: {place.id}, Type: {place.model_type}");
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
        // AR 오브젝트 생성 거리 필터 체크 (PlaceListManager의 AR 최대 거리 참조)
        if (placeListManager != null)
        {
            float maxDistance = placeListManager.GetMaxDisplayDistance();
            if (Input.location.status == LocationServiceStatus.Running)
            {
                LocationInfo currentLocation = Input.location.lastData;
                float distance = CalculateDistance(currentLocation.latitude, currentLocation.longitude, place.latitude, place.longitude);
                if (distance > maxDistance)
                {
                    Debug.Log($"[DataManager] AR 거리 필터: {place.name} ({distance:F0}m) > {maxDistance:F0}m - 생성 스킵");
                    return; // AR 최대 거리를 초과하면 생성하지 않음
                }
            }
        }

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
        
        if (SetupObjectComponents(newObj, place))
        {
            spawnedObjects[place.id] = newObj;
        }
        else
        {
            ReturnToPool(newObj, place.model_type);
        }
    }

    private void UpdateExistingObject(PlaceData place, GameObject existingObj)
    {
        SetupObjectComponents(existingObj, place);
    }

    private bool SetupObjectComponents(GameObject obj, PlaceData place)
    {
        // GPS 앵커 설정
        CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>();
        if (anchor == null)
        {
            return false;
        }
        anchor.SetCoordinatesAndCreateAnchor(place.latitude, place.longitude, place.altitude);

        // 서브사진 설정
        ImageDisplayController displayCtrl = obj.GetComponentInChildren<ImageDisplayController>();
        if (displayCtrl != null && place.sub_photos != null && place.sub_photos.Count > 0 && place.sub_photos[0] != null && place.sub_photos[0].Count > 0)
        {
            displayCtrl.SetSubPhotos(place.sub_photos[0]);
        }

        // model_type에 따른 분기 처리
        if (place.model_type == "cube")
        {
            return SetupCubeObject(obj, place);
        }
        else if (place.model_type == "custom")
        {
            return SetupGLBObject(obj, place);
        }
        else
        {
            return SetupCubeObject(obj, place); // 기본값으로 cube 처리
        }
    }

    private bool SetupCubeObject(GameObject obj, PlaceData place)
    {
        // 큐브 텍스처 설정
        ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>();
        if (display != null && !string.IsNullOrEmpty(place.main_photo))
        {
            display.SetBaseMap(place.main_photo);

            // 서브 사진 설정 (더블터치 시 표시)
            if (place.sub_photos != null && place.sub_photos.Count > 0)
            {
                List<string> subPhotoUrls = new List<string>();
                foreach (var photoGroup in place.sub_photos)
                {
                    if (photoGroup != null && photoGroup.Count > 0)
                    {
                        subPhotoUrls.Add(photoGroup[0]); // 각 그룹의 첫 번째 사진만 추가
                    }
                }
                display.SetSubPhotos(subPhotoUrls);
            }
        }

        // DoubleTap3D 설정
        DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>();
        if (doubleTap == null)
        {
            return false;
        }
        SetupDoubleTapInfo(doubleTap, place);

        // Target 설정
        Target target = obj.GetComponentInChildren<Target>();
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
        Debug.Log($"[DataManager] GLB 로딩 시작 - ID: {placeId}, URL: {url}");
        
        bool loadCompleted = false;
        bool loadSuccess = false;
        
        // 타임아웃 처리
        float startTime = Time.time;
        
        // GLB 로딩을 여러번 시도
        int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            Debug.Log($"[DataManager] GLB 로딩 시도 {attempt}/{maxAttempts} - ID: {placeId}");
            
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
                Debug.Log($"[DataManager] GLB 로딩 성공 (시도 {attempt}) - ID: {placeId}");
                break;
            }
            else
            {
                Debug.LogWarning($"[DataManager] GLB 로딩 실패 (시도 {attempt}) - ID: {placeId}");
                
                // 마지막 시도가 아니면 잠시 대기 후 재시도
                if (attempt < maxAttempts)
                {
                    Debug.Log($"[DataManager] {attempt + 1}초 대기 후 재시도 - ID: {placeId}");
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
            Debug.Log($"[DataManager] GLB 로딩 최종 성공 - ID: {placeId}");
            
            // GLB 로딩 성공 시 UI 컴포넌트 설정
            DoubleTap3D doubleTap = glbObj.GetComponentInChildren<DoubleTap3D>();
            if (doubleTap != null)
            {
                SetupDoubleTapInfo(doubleTap, place);
                Debug.Log($"[DataManager] GLB DoubleTap3D 설정 완료 - ID: {placeId}");
            }

            Target target = glbObj.GetComponentInChildren<Target>();
            if (target != null)
            {
                SetupTargetInfo(target, place);
                Debug.Log($"[DataManager] GLB Target 설정 완료 - ID: {placeId}");
            }
        }
        else
        {
            Debug.LogError($"[DataManager] GLB 로딩 최종 실패 (모든 시도 실패) - ID: {placeId}");
            
            // GLB 로딩 실패 시 처리
            if (fallbackToCube && spawnedObjects.ContainsKey(placeId))
            {
                Debug.Log($"[DataManager] GLB에서 Cube로 fallback - ID: {placeId}");
                
                // 큐브로 대체
                ReturnToPool(glbObj, "custom");
                spawnedObjects.Remove(placeId);
                
                place.model_type = "cube";
                CreateObjectFromData(place);
            }
            else
            {
                Debug.Log($"[DataManager] GLB 전용 프리팹이므로 실패 시 비활성화 - ID: {placeId}");
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
        
        Debug.Log($"[DataManager] {poolName} 풀에서 오브젝트 가져오기, 풀 크기: {targetPool.Count}");
        
        if (targetPool.Count > 0)
        {
            GameObject obj = targetPool.Dequeue();
            ResetObjectState(obj, modelType);
            obj.SetActive(true);
            obj.name = $"Place_ID_{modelType}";
            Debug.Log($"[DataManager] {poolName} 풀에서 오브젝트 가져옴: {obj.name}");
            return obj;
        }
        else if (spawnedObjects.Count < poolSize * 4)
        {
            Debug.LogWarning($"[DataManager] {poolName} 풀에 오브젝트 없음, 새 오브젝트 생성");
            GameObject prefab = modelType == "cube" ? cubePrefab : glbPrefab;
            GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            ResetObjectState(obj, modelType);
            obj.name = $"Place_ID_{modelType}";
            return obj;
        }
        
        Debug.LogError($"[DataManager] {poolName} 오브젝트 풀 한계 초과");
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

    /// <summary>
    /// 특정 ID의 스폰된 오브젝트 가져오기
    /// </summary>
    public GameObject GetSpawnedObject(int placeId)
    {
        return spawnedObjects.ContainsKey(placeId) ? spawnedObjects[placeId] : null;
    }

    /// <summary>
    /// 필터 적용 - 스폰된 AR 오브젝트의 표시/숨김 제어
    /// </summary>
    public void ApplyFilters(Dictionary<string, bool> filters)
    {
        if (filters == null)
        {
            Debug.LogWarning("[DataManager] ApplyFilters 호출되었으나 filters가 null입니다.");
            return;
        }

        bool showPetFriendly = filters.ContainsKey("petFriendly") && filters["petFriendly"];
        bool showAlcohol = filters.ContainsKey("alcohol") && filters["alcohol"];
        bool showWoopangData = filters.ContainsKey("woopangData") && filters["woopangData"];

        Debug.Log($"[DataManager] ApplyFilters - woopangData={showWoopangData}, petFriendly={showPetFriendly}, alcohol={showAlcohol}, spawnedObjects 개수={spawnedObjects.Count}");

        int shownCount = 0;
        int hiddenCount = 0;

        foreach (var kvp in spawnedObjects)
        {
            int placeId = kvp.Key;
            GameObject obj = kvp.Value;

            if (obj == null)
            {
                Debug.LogWarning($"[DataManager] placeId={placeId}의 GameObject가 null입니다.");
                continue;
            }

            if (placeDataMap.ContainsKey(placeId))
            {
                PlaceData place = placeDataMap[placeId];
                bool shouldShow = showWoopangData; // 기본값: 우팡 데이터 필터 상태

                if (shouldShow)
                {
                    // 애견동반 필터 체크
                    if (place.pet_friendly && !showPetFriendly)
                    {
                        shouldShow = false;
                    }
                    // 주류 판매 필터 체크
                    else if (place.alcohol_available && !showAlcohol)
                    {
                        shouldShow = false;
                    }
                }

                bool wasActive = obj.activeSelf;
                obj.SetActive(shouldShow);

                if (shouldShow) shownCount++;
                else hiddenCount++;

                // 상태 변경 로그
                if (wasActive != shouldShow)
                {
                    // 🔍 추가 디버깅: 부모 상태, 실제 visibility 확인
                    bool actuallyVisible = obj.activeInHierarchy;
                    Transform parent = obj.transform.parent;
                    bool parentActive = (parent != null) ? parent.gameObject.activeInHierarchy : true;

                    Debug.Log($"[DataManager] placeId={placeId} '{place.name}' - {(wasActive ? "활성" : "비활성")} → {(shouldShow ? "활성" : "비활성")}" +
                             $" | activeSelf={obj.activeSelf}, activeInHierarchy={actuallyVisible}, 부모활성={parentActive}");

                    // 🔍 재활성화했는데 실제로 안 보이는 경우 경고
                    if (shouldShow && !actuallyVisible)
                    {
                        Debug.LogWarning($"[DataManager] ⚠️ placeId={placeId} '{place.name}' SetActive(true) 했으나 activeInHierarchy=false! 부모가 비활성화되어 있을 수 있음. 부모: {(parent != null ? parent.name : "없음")}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[DataManager] placeId={placeId}가 placeDataMap에 없습니다.");
            }
        }

        Debug.Log($"[DataManager] 필터 적용 완료 - 표시: {shownCount}개, 숨김: {hiddenCount}개");
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
        LocationInfo currentLocation = Input.location.lastData;
        fetchCoroutine = StartCoroutine(FetchDataProgressively(currentLocation));
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

    /// <summary>
    /// 로딩 인디케이터 표시/숨김 제어
    /// </summary>
    private void ShowLoadingIndicator(bool show)
    {
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(show);
            Debug.Log($"[DataManager] 로딩 인디케이터: {(show ? "표시" : "숨김")}");
        }
    }

    // Update() 메서드 제거 - LoadingIndicator3D 컴포넌트가 자체적으로 애니메이션 처리

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

    /// <summary>
    /// 미리보기 모드: 모든 오브젝트 숨기기
    /// </summary>
    public void HideAllObjects()
    {
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value != null)
            {
                kvp.Value.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 미리보기 종료: 모든 오브젝트 다시 표시
    /// </summary>
    public void ShowAllObjects()
    {
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value != null)
            {
                kvp.Value.SetActive(true);
            }
        }
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
    public bool alcohol_available { get; set; }  // 주류 판매 여부
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