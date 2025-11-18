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

public class TourAPIManager : MonoBehaviour
{
    private const string BASE_URL = "https://woopang.com/proxy";
    private const string SERVICE_KEY = "teLNDctkJ9YFlMFaPWTqqwgtgxvewuaqm53dhSOiNpfOV1Q4z8NxyhhvpW4ifx3eKhI8RgodlQ05pxVHAeh1sA==";
    private readonly string tourApiUrlTemplate = "{0}/locationBasedList?serviceKey={1}&pageNo=1&numOfRows=100&mapX={2}&mapY={3}&radius={4}&listYN=Y&arrange=A&MobileOS=ETC&MobileApp=AppTest&_type=json";
    private readonly string detailImageUrlTemplate = "{0}/detailImage?serviceKey={1}&contentId={2}&imageYN=Y&numOfRows=10&MobileOS=ETC&MobileApp=AppTest&_type=json";
    private readonly string detailCommonUrlTemplate = "{0}/detailCommon?serviceKey={1}&contentId={2}&defaultYN=Y&addrinfoYN=Y&overviewYN=Y&MobileOS=ETC&MobileApp=AppTest&_type=json&lang=KO";
    private readonly string detailPetTourUrlTemplate = "{0}/detailPetTour?serviceKey={1}&contentId={2}&MobileOS=ETC&MobileApp=AppTest&_type=json";

    public GameObject samplePrefab;
    [SerializeField] private Text debugText;
    private StringBuilder debugMessages = new StringBuilder(10000);
    private Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>(100);
    private Dictionary<string, TourPlaceData> placeDataMap = new Dictionary<string, TourPlaceData>(100);
    private Dictionary<string, TourPlaceData> cachedPlaceDetails = new Dictionary<string, TourPlaceData>(50);
    private Queue<GameObject> objectPool = new Queue<GameObject>(20);
    [SerializeField] public int poolSize = 20;
    [SerializeField] private float updateInterval = 600f;
    public float loadRadius = 1000f;
    [SerializeField] private float updateDistanceThreshold = 50f;
    private bool isDataLoaded = false;
    private Coroutine fetchCoroutine;
    private Vector2 lastPosition;

    private static readonly WaitForSeconds waitOneSecond = new WaitForSeconds(1f);
    private static readonly WaitForSeconds waitFiveSeconds = new WaitForSeconds(5f);
    private static readonly WaitForSeconds waitUpdateInterval = new WaitForSeconds(600f);

    private static readonly Dictionary<SystemLanguage, string> SourceInfoMessages = new Dictionary<SystemLanguage, string>
    {
        { SystemLanguage.Korean, "(한국관광공사에서 제공한 정보입니다)" },
        { SystemLanguage.English, "(Provided by Korea Tourism Organization)" },
        { SystemLanguage.Japanese, "(韓国観光公社가 제공한情報입니다)" },
        { SystemLanguage.ChineseSimplified, "(韩国观光公社提供的信息)" },
        { SystemLanguage.Spanish, "(Por Korea Tourism Organization)" }
    };

    private string GetSourceInfoMessage()
    {
        SystemLanguage lang = Application.systemLanguage;
        return SourceInfoMessages.ContainsKey(lang) ? SourceInfoMessages[lang] : SourceInfoMessages[SystemLanguage.English];
    }

    void Start()
    {
        LogDebug("[TourAPIManager] Start() 호출됨");
        if (debugText == null)
        {
            LogDebug("[TourAPIManager] 디버그 텍스트 필드가 설정되지 않음! UI 로그 표시 불가");
        }
        InitializeObjectPool();
        StartCoroutine(StartLocationServiceAndFetchData());
    }

    private void LogDebug(string message)
    {
        Debug.Log(message);
        debugMessages.AppendLine(message);
        if (debugText != null)
        {
            debugText.text = debugMessages.ToString();
            if (debugMessages.Length > 10000)
            {
                debugMessages.Remove(0, debugMessages.Length - 5000);
            }
            Canvas.ForceUpdateCanvases();
        }
    }

    private void InitializeObjectPool()
    {
        LogDebug("[TourAPIManager] 오브젝트 풀 초기화 시작");
        if (samplePrefab == null)
        {
            LogDebug("[TourAPIManager] samplePrefab이 설정되지 않음! 오브젝트 풀 초기화 실패");
            return;
        }
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
            obj.SetActive(false);
            objectPool.Enqueue(obj);
            LogDebug($"[TourAPIManager] 풀에 오브젝트 추가됨: {obj.name}, 현재 풀 크기: {objectPool.Count}/{poolSize}");
        }
        LogDebug("[TourAPIManager] 오브젝트 풀 초기화 완료");
    }

    private IEnumerator StartLocationServiceAndFetchData()
    {
        LogDebug("[TourAPIManager] 위치 서비스 시작 시도");
        if (!Input.location.isEnabledByUser)
        {
            LogDebug("[TourAPIManager] 위치 서비스 비활성화됨 - 데이터 로드 중단");
            yield break;
        }
        Input.location.Start();
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            LogDebug("[TourAPIManager] 위치 서비스 초기화 대기 중... 남은 대기 시간: " + maxWait + "초");
            yield return waitOneSecond;
            maxWait--;
        }
        if (Input.location.status == LocationServiceStatus.Failed)
        {
            LogDebug("[TourAPIManager] 위치 서비스 시작 실패 - 데이터 로드 중단");
            yield break;
        }
        LogDebug("[TourAPIManager] 위치 서비스 초기화 성공");
        lastPosition = new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        LogDebug("[TourAPIManager] 초기 위치 설정 완료");
        fetchCoroutine = StartCoroutine(FetchDataPeriodically());
        StartCoroutine(CheckPositionAndFetchData());
    }

    private IEnumerator FetchDataPeriodically()
    {
        while (true)
        {
            LocationInfo currentLocation = Input.location.lastData;
            string tourApiUrl = string.Format(tourApiUrlTemplate, BASE_URL, SERVICE_KEY, currentLocation.longitude, currentLocation.latitude, loadRadius);
            yield return StartCoroutine(FetchDataFromTourAPI(tourApiUrl, currentLocation));
            isDataLoaded = true;
            LogDebug($"[TourAPIManager] 데이터 로드 완료, placeDataMap 크기: {placeDataMap.Count}, spawnedObjects 크기: {spawnedObjects.Count}");
            yield return waitUpdateInterval;
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
                LogDebug($"[TourAPIManager] {distanceMoved:F2}m 이동 감지, 데이터 갱신 시작");
                string tourApiUrl = string.Format(tourApiUrlTemplate, BASE_URL, SERVICE_KEY, currentLocation.longitude, currentLocation.latitude, loadRadius);
                yield return StartCoroutine(FetchDataFromTourAPI(tourApiUrl, currentLocation));
                lastPosition = currentPos;
                LogDebug("[TourAPIManager] 마지막 위치 갱신 완료");
            }
            yield return waitOneSecond;
        }
    }

    private IEnumerator FetchDataFromTourAPI(string url, LocationInfo currentLocation)
    {
        LogDebug($"[TourAPIManager] 요청: {url}");
        int retryCount = 3;
        for (int i = 0; i < retryCount; i++)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Accept-Encoding", "");
                request.timeout = 10;
                LogDebug($"[TourAPIManager] 요청 시도 {i + 1}/{retryCount}: {url}, Accept-Encoding 헤더: {request.GetRequestHeader("Accept-Encoding")}");
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    LogDebug($"[TourAPIManager] 응답 수신 성공: {json.Length} 바이트, 상태 코드: {request.responseCode}, 응답 본문: {json.Substring(0, Mathf.Min(json.Length, 500))}...");
                    yield return StartCoroutine(ProcessTourData(json, currentLocation));
                    break;
                }
                else
                {
                    LogDebug($"[TourAPIManager] 요청 실패 (시도 {i + 1}/{retryCount}): {request.error}, 응답 코드: {request.responseCode}");
                    if (i < retryCount - 1)
                    {
                        LogDebug("[TourAPIManager] 5초 후 재시도...");
                        yield return waitFiveSeconds;
                    }
                }
            }
        }
    }

    private IEnumerator ProcessTourData(string json, LocationInfo currentLocation)
    {
        LogDebug("[TourAPIManager] ProcessTourData 시작");
        List<TourPlaceData> places = null;

        try
        {
            TourAPIResponse response = JsonConvert.DeserializeObject<TourAPIResponse>(json);
            places = response?.response?.body?.items?.item;
            LogDebug($"[TourAPIManager] 파싱된 장소 수: {places?.Count ?? 0}");
        }
        catch (JsonException ex)
        {
            LogDebug($"[TourAPIManager] JSON 파싱 실패: {ex.Message}\nJSON: {json}");
            yield break;
        }

        if (places == null || places.Count == 0)
        {
            LogDebug("[TourAPIManager] TourAPI에서 반환된 장소 데이터가 없음");
            yield break;
        }

        // Sort places by distance without LINQ
        for (int i = 0; i < places.Count - 1; i++)
        {
            for (int j = i + 1; j < places.Count; j++)
            {
                float distA = CalculateDistance(currentLocation.latitude, currentLocation.longitude, places[i].mapy, places[i].mapx);
                float distB = CalculateDistance(currentLocation.latitude, currentLocation.longitude, places[j].mapy, places[j].mapx);
                if (distA > distB)
                {
                    var temp = places[i];
                    places[i] = places[j];
                    places[j] = temp;
                }
            }
        }
        LogDebug("[TourAPIManager] 장소 데이터를 거리순으로 정렬 완료");

        // Limit places to poolSize * 2 without LINQ
        if (places.Count > poolSize * 2)
        {
            LogDebug($"[TourAPIManager] 장소 데이터가 풀 크기({poolSize * 2})를 초과: {places.Count}. 상위 {poolSize * 2}개만 처리");
            while (places.Count > poolSize * 2)
            {
                places.RemoveAt(places.Count - 1);
            }
        }

        const int CHUNK_SIZE = 10;
        for (int i = 0; i < places.Count; i += CHUNK_SIZE)
        {
            int endIndex = Mathf.Min(i + CHUNK_SIZE, places.Count);
            LogDebug($"[TourAPIManager] 청크 처리 시작: {i + 1}~{endIndex}/{places.Count}");
            for (int j = i; j < endIndex; j++)
            {
                var place = places[j];
                float distance = CalculateDistance(currentLocation.latitude, currentLocation.longitude, place.mapy, place.mapx);
                place.color = "FBC15D";
                LogDebug($"[TourAPIManager] 장소 세부 정보 요청 시작: ID={place.contentid}, Title={place.title}");
                yield return StartCoroutine(FetchDetailImages(place));
                yield return StartCoroutine(FetchDetailCommon(place));
                yield return StartCoroutine(FetchDetailPetTour(place));
                LogDebug($"[TourAPIManager] 정렬된 장소: ID={place.contentid}, Title={place.title}, Tel={place.tel}, Address={place.addr1}, Images={place.imageUrls.Count}, Distance={distance}m");
            }
            yield return null;
        }

        foreach (TourPlaceData place in places)
        {
            float distance = CalculateDistance(currentLocation.latitude, currentLocation.longitude, place.mapy, place.mapx);
            LogDebug($"[TourAPIManager] 처리 중 - ID: {place.contentid}, Distance: {distance}m");
            if (!spawnedObjects.ContainsKey(place.contentid))
            {
                LogDebug($"[TourAPIManager] 새 오브젝트 생성 시도: ID={place.contentid}");
                GameObject newObj = CreateObjectFromData(place);
                if (newObj != null)
                {
                    LogDebug($"[TourAPIManager] 오브젝트 생성 성공: ID={place.contentid}, 이름={newObj.name}");
                    ImageDisplayController display = newObj.GetComponentInChildren<ImageDisplayController>();
                    if (display != null && !string.IsNullOrEmpty(place.firstimage))
                    {
                        LogDebug($"[TourAPIManager] ImageDisplayController 이미지 설정: {place.firstimage}");
                        display.SetBaseMap(place.firstimage);
                    }
                    else
                    {
                        LogDebug($"[TourAPIManager] ImageDisplayController 없음 또는 firstimage 비어 있음: ID={place.contentid}");
                    }
                }
                else
                {
                    LogDebug($"[TourAPIManager] 오브젝트 생성 실패: ID={place.contentid}");
                }
            }
            else
            {
                GameObject existingObj = spawnedObjects[place.contentid];
                LogDebug($"[TourAPIManager] 기존 오브젝트 업데이트 시도: ID={place.contentid}, 이름={existingObj.name}");
                ImageDisplayController display = existingObj.GetComponentInChildren<ImageDisplayController>();
                if (display != null && !string.IsNullOrEmpty(place.firstimage))
                {
                    LogDebug($"[TourAPIManager] 기존 오브젝트 이미지 업데이트: {place.firstimage}");
                    display.SetBaseMap(place.firstimage);
                }
                UpdateExistingObject(place, existingObj);
                placeDataMap[place.contentid] = place;
                LogDebug($"[TourAPIManager] 기존 오브젝트 업데이트 완료 - ID: {place.contentid}");
            }
        }

        // Remove outdated objects without LINQ
        List<string> toRemove = new List<string>(spawnedObjects.Count);
        HashSet<string> receivedIds = new HashSet<string>(places.Count);
        foreach (var place in places)
        {
            receivedIds.Add(place.contentid);
        }
        foreach (var id in spawnedObjects.Keys)
        {
            if (!receivedIds.Contains(id))
            {
                toRemove.Add(id);
            }
        }
        LogDebug($"[TourAPIManager] 제거할 오브젝트 수: {toRemove.Count}");
        foreach (var id in toRemove)
        {
            GameObject obj = spawnedObjects[id];
            spawnedObjects.Remove(id);
            placeDataMap.Remove(id);
            ReturnToPool(obj);
            LogDebug($"[TourAPIManager] 제거된 오브젝트 - ID: {id}, 이름={obj.name}, 현재 spawnedObjects 크기: {spawnedObjects.Count}");
        }
        LogDebug($"[TourAPIManager] ProcessTourData 완료, placeDataMap 크기: {placeDataMap.Count}, spawnedObjects 크기: {spawnedObjects.Count}");
    }

    private GameObject CreateObjectFromData(TourPlaceData place)
    {
        if (samplePrefab == null)
        {
            LogDebug("[TourAPIManager] samplePrefab이 설정되지 않음! 오브젝트 생성 실패");
            return null;
        }
        GameObject newObj = GetFromPool();
        if (newObj == null)
        {
            LogDebug("[TourAPIManager] 풀에서 오브젝트를 가져오지 못함, 새 오브젝트 인스턴스 생성");
            newObj = Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            LogDebug($"[TourAPIManager] 풀에서 오브젝트 가져옴: {newObj.name}");
        }
        newObj.SetActive(true);
        newObj.name = $"Place_{place.contentid}";
        LogDebug($"[TourAPIManager] 오브젝트 이름 설정: {newObj.name}, 활성화 상태: {newObj.activeSelf}");
        if (SetupObjectComponents(newObj, place))
        {
            spawnedObjects[place.contentid] = newObj;
            placeDataMap[place.contentid] = place;
            LogDebug($"[TourAPIManager] 새 오브젝트 생성 성공 - ID: {place.contentid}, 이름={newObj.name}, spawnedObjects 크기: {spawnedObjects.Count}");
            return newObj;
        }
        LogDebug($"[TourAPIManager] SetupObjectComponents 실패, 오브젝트 풀에 반환: {newObj.name}");
        ReturnToPool(newObj);
        return null;
    }

    private void UpdateExistingObject(TourPlaceData place, GameObject existingObj)
    {
        LogDebug($"[TourAPIManager] UpdateExistingObject 시작 - ID: {place.contentid}, 이름={existingObj.name}");
        if (!SetupObjectComponents(existingObj, place))
        {
            LogDebug($"[TourAPIManager] 오브젝트 업데이트 실패 - ID: {place.contentid}");
        }
        else
        {
            LogDebug($"[TourAPIManager] 오브젝트 업데이트 완료 - ID: {place.contentid}, 이름={existingObj.name}");
        }
    }

    private bool SetupObjectComponents(GameObject obj, TourPlaceData place)
    {
        LogDebug($"[TourAPIManager] SetupObjectComponents 시작 - ID: {place.contentid}, Title: {place.title ?? "null"}");
        DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>();
        if (doubleTap == null)
        {
            LogDebug($"[TourAPIManager] ID {place.contentid} 오브젝트에 DoubleTap3D 없음. 컴포넌트 설정 실패");
            return false;
        }
        Sprite petFriendlySprite = Resources.Load<Sprite>("Sprites/pet_friendly_icon") ?? Resources.Load<Sprite>("Sprites/default_icon");
        Sprite restroomSprite = Resources.Load<Sprite>("Sprites/separate_restroom_icon") ?? Resources.Load<Sprite>("Sprites/default_icon");
        if (petFriendlySprite == null) LogDebug("[TourAPIManager] pet_friendly_icon 로드 실패!");
        if (restroomSprite == null) LogDebug("[TourAPIManager] separate_restroom_icon 로드 실패!");

        StringBuilder addressBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(place.addr1)) addressBuilder.Append(place.addr1);
        if (!string.IsNullOrEmpty(place.addr2)) addressBuilder.Append(" ").Append(place.addr2);
        string address = addressBuilder.ToString();
        string overview = place.overview ?? "";
        string petInfo = place.petInfo ?? "";

        int placeId;
        if (!int.TryParse(place.contentid, out placeId))
        {
            LogDebug($"[TourAPIManager] ID {place.contentid}를 int로 변환 실패, 기본값 -1 사용");
            placeId = -1;
        }

        doubleTap.SetInfoImages(petFriendlySprite, restroomSprite, true, true, place.description, place.title, 
            placeId, tel: place.tel, address: address, overview: overview, petInfo: petInfo);
        LogDebug($"[TourAPIManager] DoubleTap3D 컴포넌트 설정 완료: ID={place.contentid}, Title={place.title}");

        TourAPIImageController tourImageController = obj.GetComponentInChildren<TourAPIImageController>();
        if (tourImageController != null && place.imageUrls.Count > 0)
        {
            tourImageController.SetTourImages(place.imageUrls);
            LogDebug($"[TourAPIManager] TourAPIImageController 이미지 설정: ID={place.contentid}, 이미지 수={place.imageUrls.Count}");
        }
        else
        {
            LogDebug($"[TourAPIManager] TourAPIImageController 없음 또는 imageUrls 비어 있음: ID={place.contentid}, imageUrls.Count={place.imageUrls.Count}");
        }

        CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>();
        if (anchor == null)
        {
            LogDebug($"[TourAPIManager] ID {place.contentid} 오브젝트에 CustomARGeospatialCreatorAnchor 없음. 컴포넌트 설정 실패");
            return false;
        }
        anchor.SetCoordinatesAndCreateAnchor(place.mapy, place.mapx, 0);
        LogDebug($"[TourAPIManager] Anchor 설정 완료 - ID: {place.contentid}");

        Target target = obj.GetComponentInChildren<Target>();
        if (target == null)
        {
            LogDebug($"[TourAPIManager] ID {place.contentid} 오브젝트에 Target 컴포넌트 없음. 컴포넌트 설정 실패");
            return false;
        }
        Color placeColor;
        string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;
        if (ColorUtility.TryParseHtmlString($"#{colorHex}", out placeColor))
        {
            target.TargetColor = placeColor;
            LogDebug($"[TourAPIManager] ID {place.contentid} 오브젝트의 색상 설정: {colorHex}");
        }
        else
        {
            LogDebug($"[TourAPIManager] ID {place.contentid}의 색상 파싱 실패: {colorHex}, 기본 색상(흰색)으로 설정");
            target.TargetColor = Color.white;
        }
        if (string.IsNullOrEmpty(place.title))
        {
            LogDebug($"[TourAPIManager] ID {place.contentid}의 title이 비어 있음. 기본 이름 설정.");
            target.PlaceName = "알 수 없는 장소";
        }
        else
        {
            target.PlaceName = place.title;
            LogDebug($"[TourAPIManager] ID {place.contentid} 오브젝트에 장소 이름 설정: {place.title}");
        }
        LogDebug($"[TourAPIManager] SetupObjectComponents 완료: ID={place.contentid}");
        return true;
    }

    private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000;
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(Mathf.Deg2Rad * lat1) * Mathf.Cos(Mathf.Deg2Rad * lat2) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
        return R * c;
    }

    private GameObject GetFromPool()
    {
        LogDebug($"[TourAPIManager] GetFromPool 호출, 풀 크기: {objectPool.Count}");
        if (objectPool.Count > 0)
        {
            GameObject obj = objectPool.Dequeue();
            DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>();
            if (doubleTap != null)
            {
                doubleTap.ResetData();
                LogDebug("[TourAPIManager] DoubleTap3D 데이터 리셋 완료");
            }
            obj.SetActive(true);
            return obj;
        }
        else if (spawnedObjects.Count < poolSize * 2)
        {
            LogDebug("[TourAPIManager] 풀에 오브젝트 없음, 새 오브젝트 생성");
            GameObject obj = Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
            return obj;
        }
        LogDebug("[TourAPIManager] 오브젝트 풀 한계 초과, 새 오브젝트 생성 불가");
        return null;
    }

    private void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        objectPool.Enqueue(obj);
        LogDebug($"[TourAPIManager] 오브젝트 풀에 반환됨: {obj.name}, 풀 크기: {objectPool.Count}");
    }

    private IEnumerator FetchDetailImages(TourPlaceData place)
    {
        if (cachedPlaceDetails.ContainsKey(place.contentid))
        {
            var cached = cachedPlaceDetails[place.contentid];
            place.imageUrls = cached.imageUrls;
            LogDebug($"[TourAPIManager] detailImage 캐시 사용: ID={place.contentid}, 이미지 수={place.imageUrls.Count}");
            yield break;
        }

        string url = string.Format(detailImageUrlTemplate, BASE_URL, SERVICE_KEY, place.contentid);
        LogDebug($"[TourAPIManager] detailImage 요청: {url}");
        int retryCount = 3;
        for (int i = 0; i < retryCount; i++)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Accept-Encoding", "");
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<DetailImageResponse>(request.downloadHandler.text);
                        var items = response?.response?.body?.items?.item;
                        if (items != null)
                        {
                            place.imageUrls = new List<string>(items.Count);
                            foreach (var item in items)
                            {
                                if (!string.IsNullOrEmpty(item.originimgurl))
                                    place.imageUrls.Add(item.originimgurl);
                            }
                            cachedPlaceDetails[place.contentid] = place;
                            LogDebug($"[TourAPIManager] detailImage 성공: ID={place.contentid}, 이미지 수={place.imageUrls.Count}");
                        }
                        else
                        {
                            LogDebug($"[TourAPIManager] detailImage 응답에 이미지 데이터 없음: ID={place.contentid}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        LogDebug($"[TourAPIManager] detailImage JSON 파싱 실패: ID={place.contentid}, 에러: {ex.Message}");
                    }
                    break;
                }
                else
                {
                    LogDebug($"[TourAPIManager] detailImage 요청 실패 (시도 {i + 1}/{retryCount}): ID={place.contentid}, 에러: {request.error}");
                    if (i < retryCount - 1) yield return waitFiveSeconds;
                }
            }
        }
    }

    private IEnumerator FetchDetailCommon(TourPlaceData place)
    {
        if (cachedPlaceDetails.ContainsKey(place.contentid))
        {
            var cached = cachedPlaceDetails[place.contentid];
            place.tel = cached.tel;
            place.addr1 = cached.addr1;
            place.addr2 = cached.addr2;
            place.overview = cached.overview;
            LogDebug($"[TourAPIManager] detailCommon 캐시 사용: ID={place.contentid}");
            yield break;
        }

        string url = string.Format(detailCommonUrlTemplate, BASE_URL, SERVICE_KEY, place.contentid);
        LogDebug($"[TourAPIManager] detailCommon 요청 (국문): {url}");
        int retryCount = 3;
        UnityWebRequest request = null;

        for (int i = 0; i < retryCount; i++)
        {
            request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Accept-Encoding", "");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                break;
            }
            else
            {
                LogDebug($"[TourAPIManager] detailCommon 요청 실패 (시도 {i + 1}/{retryCount}, 국문): ID={place.contentid}, 에러: {request.error}");
                if (i < retryCount - 1) yield return waitFiveSeconds;
            }
        }

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<DetailCommonResponse>(request.downloadHandler.text);
                var item = response?.response?.body?.items?.item?.FirstOrDefault();
                if (item != null)
                {
                    place.tel = item.tel;
                    place.addr1 = item.addr1;
                    place.addr2 = item.addr2;
                    place.overview = item.overview;
                    cachedPlaceDetails[place.contentid] = place;
                    LogDebug($"[TourAPIManager] detailCommon 성공 (국문): ID={place.contentid}, Tel={place.tel}, Addr1={place.addr1}, Overview={item.overview}");
                }
                else
                {
                    LogDebug($"[TourAPIManager] detailCommon 응답에 데이터 없음 (국문): ID={place.contentid}");
                }
            }
            catch (JsonException ex)
            {
                LogDebug($"[TourAPIManager] detailCommon JSON 파싱 실패 (국문): ID={place.contentid}, 에러: {ex.Message}");
            }
        }
    }

    private IEnumerator FetchDetailPetTour(TourPlaceData place)
    {
        if (cachedPlaceDetails.ContainsKey(place.contentid))
        {
            var cached = cachedPlaceDetails[place.contentid];
            place.petInfo = cached.petInfo;
            LogDebug($"[TourAPIManager] detailPetTour 캐시 사용: ID={place.contentid}");
            yield break;
        }

        string url = string.Format(detailPetTourUrlTemplate, BASE_URL, SERVICE_KEY, place.contentid);
        LogDebug($"[TourAPIManager] detailPetTour 요청: {url}");
        int retryCount = 3;
        UnityWebRequest request = null;

        for (int i = 0; i < retryCount; i++)
        {
            request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Accept-Encoding", "");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                break;
            }
            else
            {
                LogDebug($"[TourAPIManager] detailPetTour 요청 실패 (시도 {i + 1}/{retryCount}): ID={place.contentid}, 에러: {request.error}");
                if (i < retryCount - 1) yield return waitFiveSeconds;
            }
        }

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<DetailPetTourResponse>(request.downloadHandler.text);
                var item = response?.response?.body?.items?.item?.FirstOrDefault();
                if (item != null)
                {
                    StringBuilder petInfoBuilder = new StringBuilder();
                    if (!string.IsNullOrEmpty(item.acmpyPsblCpam)) petInfoBuilder.AppendLine(item.acmpyPsblCpam);
                    if (!string.IsNullOrEmpty(item.acmpyNeedMtr)) petInfoBuilder.AppendLine(item.acmpyNeedMtr);
                    if (!string.IsNullOrEmpty(item.etcAcmpyInfo)) petInfoBuilder.AppendLine(item.etcAcmpyInfo);
                    place.petInfo = petInfoBuilder.ToString();
                    cachedPlaceDetails[place.contentid] = place;
                    LogDebug($"[TourAPIManager] detailPetTour 성공: ID={place.contentid}, PetInfo={place.petInfo}");
                }
                else
                {
                    LogDebug($"[TourAPIManager] detailPetTour 응답에 데이터 없음: ID={place.contentid}");
                }
            }
            catch (JsonException ex)
            {
                LogDebug($"[TourAPIManager] detailPetTour JSON 파싱 실패: ID={place.contentid}, 에러: {ex.Message}");
            }
        }
    }

    public Dictionary<string, TourPlaceData> GetPlaceDataMap()
    {
        LogDebug($"[TourAPIManager] GetPlaceDataMap 호출, 반환된 데이터 크기: {placeDataMap.Count}");
        return placeDataMap;
    }

    public int GetSpawnedObjectsCount()
    {
        LogDebug($"[TourAPIManager] GetSpawnedObjectsCount 호출, 반환된 오브젝트 수: {spawnedObjects.Count}");
        return spawnedObjects.Count;
    }

    public bool IsDataLoaded()
    {
        LogDebug($"[TourAPIManager] IsDataLoaded 호출, 데이터 로드 상태: {isDataLoaded}");
        return isDataLoaded;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isDataLoaded)
        {
            LogDebug("[TourAPIManager] 앱 포그라운드 복귀 - 즉시 데이터 업데이트");
            if (fetchCoroutine != null)
            {
                StopCoroutine(fetchCoroutine);
                fetchCoroutine = null;
                LogDebug("[TourAPIManager] 기존 fetchCoroutine 중지");
            }
            LocationInfo currentLocation = Input.location.lastData;
            string tourApiUrl = string.Format(tourApiUrlTemplate, BASE_URL, SERVICE_KEY, currentLocation.longitude, currentLocation.latitude, loadRadius);
            fetchCoroutine = StartCoroutine(FetchDataImmediately(tourApiUrl, currentLocation));
        }
    }

    private IEnumerator FetchDataImmediately(string url, LocationInfo currentLocation)
    {
        LogDebug("[TourAPIManager] FetchDataImmediately 시작");
        yield return StartCoroutine(FetchDataFromTourAPI(url, currentLocation));
        fetchCoroutine = StartCoroutine(FetchDataPeriodically());
        LogDebug("[TourAPIManager] FetchDataImmediately 완료, FetchDataPeriodically 시작");
    }

    void OnDestroy()
    {
        Input.location.Stop();
        LogDebug("[TourAPIManager] 위치 서비스 중지");
    }
}

[System.Serializable]
public class TourPlaceData
{
    public string contentid { get; set; }
    public string title { get; set; }
    public float mapx { get; set; }
    public float mapy { get; set; }
    public string firstimage { get; set; }
    public string description { get; set; }
    public string color { get; set; }
    public List<string> imageUrls { get; set; }
    public string tel { get; set; }
    public string addr1 { get; set; }
    public string addr2 { get; set; }
    public string overview { get; set; }
    public string petInfo { get; set; }

    public TourPlaceData()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                description = "(한국관광공사에서 제공한 정보입니다)";
                break;
            case SystemLanguage.Japanese:
                description = "(韓国観光公社가 제공한情報입니다)";
                break;
            case SystemLanguage.ChineseSimplified:
                description = "(韩国观光公社提供的信息)";
                break;
            case SystemLanguage.Spanish:
                description = "(Por Korea Tourism Organization)";
                break;
            default:
                description = "(Provided by Korea Tourism Organization)";
                break;
        }
        imageUrls = new List<string>();
    }
}

[System.Serializable]
public class TourAPIResponse
{
    public TourAPIResponseBody response { get; set; }
}

[System.Serializable]
public class TourAPIResponseBody
{
    public TourAPIResponseBodyContent body { get; set; }
}

[System.Serializable]
public class TourAPIResponseBodyContent
{
    public TourAPIItems items { get; set; }
}

[System.Serializable]
public class TourAPIItems
{
    public List<TourPlaceData> item { get; set; }
}

[System.Serializable]
public class DetailImageResponse
{
    public DetailImageResponseBody response { get; set; }
}

[System.Serializable]
public class DetailImageResponseBody
{
    public DetailImageResponseBodyContent body { get; set; }
}

[System.Serializable]
public class DetailImageResponseBodyContent
{
    public DetailImageItems items { get; set; }
    public int numOfRows { get; set; }
    public int pageNo { get; set; }
    public int totalCount { get; set; }
}

[System.Serializable]
public class DetailImageItems
{
    public List<DetailImageItem> item { get; set; }
}

[System.Serializable]
public class DetailImageItem
{
    public string contentid { get; set; }
    public string originimgurl { get; set; }
    public string imgname { get; set; }
    public string smallimageurl { get; set; }
    public string cpyrhtDivCd { get; set; }
    public string serialnum { get; set; }
}

[System.Serializable]
public class DetailCommonResponse
{
    public DetailCommonResponseBody response { get; set; }
}

[System.Serializable]
public class DetailCommonResponseBody
{
    public DetailCommonResponseBodyContent body { get; set; }
}

[System.Serializable]
public class DetailCommonResponseBodyContent
{
    public DetailCommonItems items { get; set; }
    public int numOfRows { get; set; }
    public int pageNo { get; set; }
    public int totalCount { get; set; }
}

[System.Serializable]
public class DetailCommonItems
{
    public List<DetailCommonItem> item { get; set; }
}

[System.Serializable]
public class DetailCommonItem
{
    public string contentid { get; set; }
    public string tel { get; set; }
    public string addr1 { get; set; }
    public string addr2 { get; set; }
    public string overview { get; set; }
}

[System.Serializable]
public class DetailPetTourResponse
{
    public DetailPetTourResponseBody response { get; set; }
}

[System.Serializable]
public class DetailPetTourResponseBody
{
    public DetailPetTourResponseBodyContent body { get; set; }
}

[System.Serializable]
public class DetailPetTourResponseBodyContent
{
    public DetailPetTourItems items { get; set; }
    public int numOfRows { get; set; }
    public int pageNo { get; set; }
    public int totalCount { get; set; }
}

[System.Serializable]
public class DetailPetTourItems
{
    public List<DetailPetTourItem> item { get; set; }
}

[System.Serializable]
public class DetailPetTourItem
{
    public string contentid { get; set; }
    public string acmpyPsblCpam { get; set; }
    public string acmpyNeedMtr { get; set; }
    public string etcAcmpyInfo { get; set; }
}