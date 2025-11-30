# 서버 데이터 로딩 및 표시 로직 설명

## 📊 전체 데이터 흐름

```
서버 → DataManager/TourAPIManager → placeDataMap → PlaceListManager → ListPanel UI
```

## 1️⃣ 서버에서 데이터 가져오기 (DataManager)

### DataManager.cs 로직

```csharp
// 위치: Assets/Scripts/Download/DataManager.cs

void Start()
{
    InitializeObjectPools();
    StartCoroutine(StartLocationServiceAndFetchData());
}
```

**단계 1: GPS 위치 서비스 시작**
```csharp
IEnumerator StartLocationServiceAndFetchData()
{
    // GPS 권한 확인
    if (!Input.location.isEnabledByUser)
    {
        ShowErrorMessage("위치 서비스를 활성화해 주세요.");
        yield break;  // ❌ 여기서 멈추면 데이터 안 불러옴
    }

    // GPS 시작 (최대 20초 대기)
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
        yield break;  // ❌ 여기서 멈추면 데이터 안 불러옴
    }

    // ✅ GPS 성공 → 데이터 불러오기 시작
    lastPosition = new Vector2(Input.location.lastData.latitude,
                              Input.location.lastData.longitude);
    fetchCoroutine = StartCoroutine(FetchDataPeriodically());
}
```

**단계 2: AR 세션 추적 시작까지 대기**
```csharp
IEnumerator FetchDataPeriodically()
{
    while (true)
    {
        // AR 세션이 추적 중일 때까지 대기
        yield return new WaitUntil(() => ARSession.state == ARSessionState.SessionTracking);

        LocationInfo currentLocation = Input.location.lastData;

        // ✅ 여기서 실제로 서버에 요청
        yield return StartCoroutine(FetchDataProgressively(currentLocation));

        isDataLoaded = true;  // ✅ 데이터 로딩 완료 플래그
        yield return new WaitForSeconds(updateInterval);  // 600초(10분)마다 갱신
    }
}
```

**단계 3: 서버 API 호출**
```csharp
IEnumerator FetchDataProgressively(LocationInfo location)
{
    string url = $"{baseServerUrl}";  // https://woopang.com/locations?status=approved

    Debug.Log($"[DataManager] 서버 요청: {url}");

    using (UnityWebRequest request = UnityWebRequest.Get(url))
    {
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            // ✅ 서버 응답 성공
            string jsonData = request.downloadHandler.text;

            // JSON 파싱
            LocationResponse response = JsonConvert.DeserializeObject<LocationResponse>(jsonData);

            if (response?.locations != null && response.locations.Count > 0)
            {
                Debug.Log($"[DataManager] 서버에서 {response.locations.Count}개 위치 데이터 수신");

                // placeDataMap에 저장
                foreach (var place in response.locations)
                {
                    if (!placeDataMap.ContainsKey(place.id))
                    {
                        placeDataMap[place.id] = place;
                    }
                    else
                    {
                        placeDataMap[place.id] = place;  // 업데이트
                    }
                }

                // ✅ AR 오브젝트 생성
                yield return StartCoroutine(SpawnObjectsProgressively(location));
            }
            else
            {
                Debug.LogWarning("[DataManager] 서버 응답에 데이터가 없음");
            }
        }
        else
        {
            Debug.LogError($"[DataManager] 서버 요청 실패: {request.error}");
        }
    }
}
```

**데이터 저장 구조:**
```csharp
// placeDataMap: 모든 장소 데이터를 저장하는 Dictionary
private Dictionary<int, PlaceData> placeDataMap = new Dictionary<int, PlaceData>();

// 외부에서 접근하는 메서드
public Dictionary<int, PlaceData> GetPlaceDataMap()
{
    return placeDataMap;
}

public bool IsDataLoaded()
{
    return isDataLoaded;
}
```

## 2️⃣ PlaceListManager가 데이터 가져와서 표시

### PlaceListManager.cs 로직

**단계 1: 초기화 및 데이터 로딩 대기**
```csharp
void Start()
{
    Debug.Log($"[PlaceListManager] Start() 호출 - listText={listText != null},
               dataManager={dataManager != null}, tourAPIManager={tourAPIManager != null}");

    // ❌ null 체크 - 하나라도 null이면 초기화 중단
    if (listText == null || dataManager == null || tourAPIManager == null)
    {
        Debug.LogError("[PlaceListManager] 필수 컴포넌트가 null이어서 초기화를 건너뜁니다.");
        return;
    }

    // ✅ 데이터 로딩 대기 시작
    StartCoroutine(InitializeAndUpdateUI());
}
```

**단계 2: 데이터 로딩 완료까지 대기 (최대 30초)**
```csharp
IEnumerator InitializeAndUpdateUI()
{
    Debug.Log("[PlaceListManager] 데이터 로딩 대기 시작...");
    float waitTime = 0f;

    // DataManager와 TourAPIManager 모두 데이터 로딩 완료까지 대기
    while ((dataManager != null && !dataManager.IsDataLoaded()) ||
           (tourAPIManager != null && !tourAPIManager.IsDataLoaded()))
    {
        waitTime += 1f;
        Debug.Log($"[PlaceListManager] 데이터 대기 중... {waitTime}초 -
                   DataManager={dataManager?.IsDataLoaded()},
                   TourAPI={tourAPIManager?.IsDataLoaded()}");
        yield return new WaitForSeconds(1f);

        // 30초 타임아웃
        if (waitTime >= 30f)
        {
            Debug.LogWarning("[PlaceListManager] 데이터 로딩 타임아웃 (30초) - 강제로 UI 업데이트 시도");
            break;
        }
    }

    Debug.Log("[PlaceListManager] 데이터 로딩 완료! 첫 UI 업데이트 시작");

    // ✅ 첫 UI 업데이트
    UpdateUI();

    // 10초마다 자동 업데이트 시작
    StartCoroutine(UpdateUIPeriodically());
}
```

**단계 3: 데이터 가져오기 및 필터링**
```csharp
private void UpdateUI()
{
    Debug.Log("[PlaceListManager] UpdateUI() 호출됨");

    // GPS 위치 가져오기
    float latitude, longitude;
    if (Input.location.status == LocationServiceStatus.Running)
    {
        LocationInfo currentLocation = Input.location.lastData;
        latitude = currentLocation.latitude;
        longitude = currentLocation.longitude;
        Debug.Log($"[PlaceListManager] GPS 위치: {latitude}, {longitude}");
    }
    else
    {
        // GPS 비활성화 시 서울시청 좌표 사용
        latitude = 37.5665f;
        longitude = 126.9780f;
        Debug.LogWarning($"[PlaceListManager] GPS 비활성화 - 기본 위치 사용: {latitude}, {longitude}");
    }

    // ✅ DataManager/TourAPIManager에서 데이터 가져오기
    var woopangPlaces = dataManager != null ? dataManager.GetPlaceDataMap().Values.ToList()
                                            : new List<PlaceData>();
    var tourPlaces = tourAPIManager != null ? tourAPIManager.GetPlaceDataMap().Values.ToList()
                                             : new List<TourPlaceData>();

    Debug.Log($"[PlaceListManager] 데이터 개수 - 우팡: {woopangPlaces.Count}, TourAPI: {tourPlaces.Count}");

    // ❌ listText null 체크
    if (listText == null)
    {
        Debug.LogError("[PlaceListManager] listText가 null입니다!");
        return;
    }

    listText.text = "";  // 리스트 초기화
    combinedPlaces.Clear();

    // 📌 필터 상태 확인
    bool showWoopangData = activeFilters.ContainsKey("woopangData") && activeFilters["woopangData"];
    bool showPetFriendly = activeFilters.ContainsKey("petFriendly") && activeFilters["petFriendly"];
    bool showAlcohol = activeFilters.ContainsKey("alcohol") && activeFilters["alcohol"];

    Debug.Log($"[PlaceListManager] 필터 상태 - woopangData={showWoopangData},
               petFriendly={showPetFriendly}, alcohol={showAlcohol}");

    // ✅ 우팡 데이터 처리
    if (showWoopangData)
    {
        foreach (var place in woopangPlaces)
        {
            // 애견동반 필터 체크
            if (place.pet_friendly && !showPetFriendly)
            {
                continue;  // 건너뛰기
            }

            // 주류 판매 필터 체크
            if (place.alcohol_available && !showAlcohol)
            {
                continue;  // 건너뛰기
            }

            // 거리 계산 (Haversine 공식)
            float distance = CalculateDistance(latitude, longitude,
                                              place.latitude, place.longitude);

            // ⚠️ 여기서 거리 필터링 하지 않음! (원래 로직)
            // 모든 데이터를 리스트에 추가

            string distanceText = $"{Mathf.FloorToInt(distance)}m";
            string displayText = place.pet_friendly
                ? $"{place.name} - {distanceText} [애견동반]"
                : $"{place.name} - {distanceText}";
            string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;

            combinedPlaces.Add((place, distance, place.id.ToString(), displayText, colorHex));
        }
    }

    // ✅ 공공데이터(TourAPI) 처리
    if (activeFilters["publicData"])
    {
        foreach (var place in tourPlaces)
        {
            // 애견동반 필터 체크 (TourAPI는 모두 애견동반)
            if (!activeFilters["petFriendly"])
            {
                continue;
            }

            float distance = CalculateDistance(latitude, longitude, place.mapy, place.mapx);
            string distanceText = $"{Mathf.FloorToInt(distance)}m";
            string displayText = string.IsNullOrEmpty(place.firstimage)
                ? $"{place.title} - {distanceText} [이미지없음] [애견동반]"
                : $"{place.title} - {distanceText} [애견동반]";
            string colorHex = string.IsNullOrEmpty(place.color) ? "FFFFFF" : place.color;

            combinedPlaces.Add((place, distance, place.contentid, displayText, colorHex));
        }
    }

    // 거리순 정렬
    combinedPlaces = combinedPlaces.OrderBy(x => x.distance).ToList();

    Debug.Log($"[PlaceListManager] 리스트 업데이트 - 전체 데이터: 우팡={woopangPlaces.Count},
               TourAPI={tourPlaces.Count}, 필터링 후 표시={combinedPlaces.Count}");

    // ✅ UI에 표시
    foreach (var (place, distance, id, displayText, colorHex) in combinedPlaces)
    {
        string coloredText = $"<color=#{colorHex}>{displayText}</color>";
        listText.text += coloredText + "\n";
    }
}
```

## 🔍 리스트가 표시되지 않는 원인 분석

### 가능한 원인 1: GPS 권한/서비스 문제
```
❌ GPS 비활성화 → StartLocationServiceAndFetchData() 중단
❌ AR 세션 추적 실패 → FetchDataPeriodically() 대기 상태
```

**확인 방법:**
- Console 로그: `[DataManager] 서버 요청:` 로그가 있는가?
- 없으면 GPS 또는 AR 세션 문제

### 가능한 원인 2: 서버 응답 없음
```
❌ 서버에서 데이터 0개 → placeDataMap 비어있음
❌ 네트워크 오류 → request.result != Success
```

**확인 방법:**
- Console 로그: `[DataManager] 서버에서 X개 위치 데이터 수신`
- X가 0이면 서버에 데이터가 없음

### 가능한 원인 3: PlaceListManager 컴포넌트 연결 문제
```
❌ listText == null → 초기화 중단
❌ dataManager == null → woopangPlaces.Count = 0
❌ tourAPIManager == null → tourPlaces.Count = 0
```

**확인 방법:**
- Console 로그: `[PlaceListManager] Start() 호출 - listText=False`
- False가 있으면 Unity Inspector 연결 안 됨

### 가능한 원인 4: 필터 상태 문제
```
❌ activeFilters["woopangData"] = false → 우팡 데이터 필터링됨
❌ activeFilters["publicData"] = false → 공공데이터 필터링됨
```

**확인 방법:**
- Console 로그: `[PlaceListManager] 필터 상태 - woopangData=false`
- false면 FilterManager가 필터를 끈 것

### 가능한 원인 5: 데이터 로딩 타임아웃
```
❌ 30초 내에 IsDataLoaded() = true 안 됨
→ "데이터 로딩 타임아웃" 경고 후 강제 업데이트
→ 빈 데이터로 UI 업데이트
```

**확인 방법:**
- Console 로그: `[PlaceListManager] 데이터 로딩 타임아웃 (30초)`
- 이 로그가 있으면 데이터가 늦게 도착

## 📋 정상 작동 시 Console 로그 순서

```
1. [PlaceListManager] Start() 호출 - listText=True, dataManager=True, tourAPIManager=True
2. [PlaceListManager] 슬라이더 초기화 완료: value=144m
3. [PlaceListManager] 데이터 로딩 대기 시작...
4. [DataManager] 서버 요청: https://woopang.com/locations?status=approved
5. [DataManager] 서버에서 25개 위치 데이터 수신
6. [PlaceListManager] 데이터 대기 중... 3초 - DataManager=True, TourAPI=True
7. [PlaceListManager] 데이터 로딩 완료! 첫 UI 업데이트 시작
8. [PlaceListManager] UpdateUI() 호출됨
9. [PlaceListManager] GPS 위치: 37.5665, 126.9780
10. [PlaceListManager] 데이터 개수 - 우팡: 25, TourAPI: 10
11. [PlaceListManager] 필터 상태 - woopangData=true, petFriendly=true, alcohol=true
12. [PlaceListManager] 리스트 업데이트 - 전체 데이터: 우팡=25, TourAPI=10, 필터링 후 표시=35
```

## ✅ 해결 방법

### 1. Unity Inspector 설정 확인
- `PlaceListManager` 오브젝트 선택
- Inspector에서 다음 확인:
  - `List Text`: ListPanel/Text 연결되어 있는가?
  - `Data Manager`: DownloadCube_쾌 연결되어 있는가?
  - `Tour API Manager`: DownloadCube_TourAPI_Petfriendly 연결되어 있는가?

### 2. FilterManager 필터 상태 확인
- `FilterButtonPanel` 오브젝트 선택
- Inspector에서 다음 확인:
  - 모든 토글이 ON 상태인가?
  - `Place List Manager` 참조가 연결되어 있는가?

### 3. 디바이스 설정 확인
- GPS 권한이 허용되어 있는가?
- AR Core가 정상 작동하는가?
- 네트워크 연결이 되어 있는가?

### 4. 서버 데이터 확인
- 브라우저에서 https://woopang.com/locations?status=approved 접속
- JSON 응답에 데이터가 있는가?

## 🛠️ 디버깅 체크리스트

Play 모드에서 Console 확인:

- [ ] `[PlaceListManager] Start() 호출` 로그가 있는가?
- [ ] `listText=True, dataManager=True, tourAPIManager=True` 모두 True인가?
- [ ] `[DataManager] 서버 요청:` 로그가 있는가?
- [ ] `서버에서 X개 위치 데이터 수신` X가 0보다 큰가?
- [ ] `[PlaceListManager] 데이터 로딩 완료!` 로그가 있는가?
- [ ] `데이터 개수 - 우팡: X` X가 0보다 큰가?
- [ ] `필터 상태 - woopangData=true` 인가?
- [ ] `필터링 후 표시=X` X가 0보다 큰가?

**모두 체크되면 정상 작동!**
**하나라도 안 되면 해당 단계에 문제가 있음**
