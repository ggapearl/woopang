# 거리 필터 완전 가이드

## 개요

거리 슬라이더 UI로 **리스트 표시**와 **AR 오브젝트 생성/표시**를 동시에 제어합니다.

## 동작 방식

### 1. 리스트 필터링 (PlaceListManager)
- 슬라이더 값에 따라 리스트 항목 필터링
- 거리를 초과하는 장소는 리스트에 표시하지 않음

### 2. AR 오브젝트 생성 제어 (DataManager, TourAPIManager)
- **신규 오브젝트**: 최대 거리를 초과하면 생성하지 않음
- **기존 오브젝트**: 슬라이더 변경 시 활성화/비활성화

## 구현된 기능

### PlaceListManager.cs

#### 추가된 필드
```csharp
[Header("Distance Filter")]
[Tooltip("거리 조정 슬라이더 (UI)")]
[SerializeField] private Slider distanceSlider;

[Tooltip("최대 표시 거리 (미터)")]
[SerializeField] private float maxDisplayDistance = 1000f;

[Tooltip("슬라이더 값을 표시할 텍스트 (선택사항)")]
[SerializeField] private Text distanceValueText;
```

#### 추가된 메서드
- `OnDistanceSliderChanged()`: 슬라이더 값 변경 시 호출
- `ApplyDistanceFilterToARObjects()`: 기존 AR 오브젝트 필터링
- `GetMaxDisplayDistance()`: 현재 최대 거리 반환 (공개 메서드)

### DataManager.cs

#### 추가된 필드
```csharp
[Header("Distance Filter")]
[Tooltip("PlaceListManager 참조 (거리 필터 동기화)")]
[SerializeField] private PlaceListManager placeListManager;
```

#### 수정된 메서드
- `CreateObjectFromData()`: 오브젝트 생성 전 거리 체크 추가
- `GetSpawnedObject(int placeId)`: 특정 오브젝트 조회 메서드 추가

### TourAPIManager.cs

#### 추가된 필드
```csharp
[Header("Distance Filter")]
[Tooltip("PlaceListManager 참조 (거리 필터 동기화)")]
[SerializeField] private PlaceListManager placeListManager;
```

#### 수정된 메서드
- `CreateObjectFromData()`: 오브젝트 생성 전 거리 체크 추가
- `GetSpawnedObject(string placeId)`: 특정 오브젝트 조회 메서드 추가

## Unity 설정 가이드

### 1단계: UI 생성 (수동)

#### 방법 1: Unity Editor에서 직접 생성 (권장)
[CREATE_DISTANCE_SLIDER_MANUAL.md](CREATE_DISTANCE_SLIDER_MANUAL.md) 참고

#### 방법 2: 프리팹 사용 (실험적)
[DISTANCE_SLIDER_IMPLEMENTATION.md](DISTANCE_SLIDER_IMPLEMENTATION.md) 참고

### 2단계: PlaceListManager 연결

1. **Hierarchy에서 PlaceListManager가 있는 오브젝트 선택**

2. **Inspector의 Distance Filter 섹션:**
   - **Distance Slider**: UI Slider 연결
   - **Max Display Distance**: 1000 (기본값)
   - **Distance Value Text**: UI Text 연결 (선택)

### 3단계: DataManager 연결

1. **Hierarchy에서 DataManager가 있는 오브젝트 선택**

2. **Inspector의 Distance Filter 섹션:**
   - **Place List Manager**: PlaceListManager 오브젝트 드래그

### 4단계: TourAPIManager 연결

1. **Hierarchy에서 TourAPIManager가 있는 오브젝트 선택**

2. **Inspector의 Distance Filter 섹션:**
   - **Place List Manager**: PlaceListManager 오브젝트 드래그

## 작동 흐름

### 슬라이더 값 변경 시
```
1. 사용자가 슬라이더 드래그
   ↓
2. OnDistanceSliderChanged() 호출
   ↓
3. maxDisplayDistance 업데이트
   ↓
4. UpdateDistanceValueText() - 거리 텍스트 업데이트
   ↓
5. UpdateUI() - 리스트 필터링
   ↓
6. ApplyDistanceFilterToARObjects() - 기존 AR 오브젝트 활성화/비활성화
```

### 새 오브젝트 생성 시
```
1. DataManager 또는 TourAPIManager가 새 장소 데이터 수신
   ↓
2. CreateObjectFromData() 호출
   ↓
3. PlaceListManager.GetMaxDisplayDistance() 조회
   ↓
4. 현재 위치와 장소 사이 거리 계산
   ↓
5. 거리 > maxDisplayDistance?
   Yes → 생성 중단 (return)
   No → 오브젝트 생성 진행
```

## 코드 예시

### PlaceListManager에서 거리 필터링
```csharp
float distance = CalculateDistance(latitude, longitude, place.latitude, place.longitude);

// 거리 필터 체크
if (distance > maxDisplayDistance)
{
    continue; // 최대 표시 거리를 초과하면 건너뛰기
}
```

### DataManager에서 오브젝트 생성 제어
```csharp
// 거리 필터 체크 (PlaceListManager의 maxDisplayDistance 참조)
if (placeListManager != null)
{
    float maxDistance = placeListManager.GetMaxDisplayDistance();
    if (Input.location.status == LocationServiceStatus.Running)
    {
        LocationInfo currentLocation = Input.location.lastData;
        float distance = CalculateDistance(currentLocation.latitude, currentLocation.longitude, place.latitude, place.longitude);
        if (distance > maxDistance)
        {
            Debug.Log($"[DataManager] 거리 필터: {place.name} ({distance:F0}m) > {maxDistance:F0}m - 스킵");
            return; // 최대 표시 거리를 초과하면 생성하지 않음
        }
    }
}
```

### 기존 오브젝트 활성화/비활성화
```csharp
GameObject obj = dataManager.GetSpawnedObject(kvp.Key);
if (obj != null)
{
    obj.SetActive(distance <= maxDisplayDistance);
}
```

## 테스트 체크리스트

- [ ] Unity Editor에서 슬라이더 UI 생성 완료
- [ ] PlaceListManager에 Slider, Text 연결 완료
- [ ] DataManager에 PlaceListManager 연결 완료
- [ ] TourAPIManager에 PlaceListManager 연결 완료
- [ ] Play 모드에서 슬라이더 드래그 시 리스트 필터링 확인
- [ ] 슬라이더 드래그 시 AR 오브젝트 활성화/비활성화 확인
- [ ] 새로운 장소 로드 시 거리 필터 적용 확인
- [ ] 거리 텍스트 업데이트 확인 ("500m" / "1.5km")

## 성능 고려사항

### 최적화
- `GetMaxDisplayDistance()` 메서드는 간단한 값 반환 (성능 영향 최소)
- 슬라이더 변경 시에만 기존 오브젝트 필터링 실행
- 오브젝트 생성은 거리 체크로 조기 중단 (불필요한 리소스 로딩 방지)

### 메모리
- 오브젝트를 삭제하지 않고 비활성화 (재활성화 시 빠른 응답)
- Object Pooling과 호환 가능

## 문제 해결

### 슬라이더가 AR 오브젝트를 제어하지 않아요
1. DataManager Inspector에서 PlaceListManager 연결 확인
2. TourAPIManager Inspector에서 PlaceListManager 연결 확인
3. Console에서 "[DataManager] 거리 필터" 로그 확인

### 신규 오브젝트가 생성되지 않아요
1. Console에서 "거리 필터: ... 스킵" 로그 확인
2. 슬라이더 값을 더 크게 조정 (예: 2000m)
3. 현재 위치와 장소 사이 거리 확인

### 기존 오브젝트가 사라지지 않아요
1. PlaceListManager의 `ApplyDistanceFilterToARObjects()` 호출 확인
2. Console에서 "[PlaceListManager] 거리 필터 적용 완료" 로그 확인
3. GetSpawnedObject() 메서드 반환값 확인

## 파일 위치

- **PlaceListManager.cs**: `Assets/Scripts/Download/PlaceListManager.cs`
- **DataManager.cs**: `Assets/Scripts/Download/DataManager.cs`
- **TourAPIManager.cs**: `Assets/Scripts/Download/TourAPIManager.cs`

## 관련 문서

- [CREATE_DISTANCE_SLIDER_MANUAL.md](CREATE_DISTANCE_SLIDER_MANUAL.md) - UI 수동 생성 가이드
- [DISTANCE_SLIDER_IMPLEMENTATION.md](DISTANCE_SLIDER_IMPLEMENTATION.md) - 전체 구현 상세
- [DISTANCE_SLIDER_QUICK_SETUP.md](DISTANCE_SLIDER_QUICK_SETUP.md) - 빠른 설정 가이드

## 수정 날짜
2025-11-29
