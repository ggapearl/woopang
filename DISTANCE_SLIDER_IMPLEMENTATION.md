# 거리 슬라이더 구현 가이드

## 개요
PlaceListManager에 거리 조정 슬라이더를 추가하여 사용자가 AR 오브젝트 및 리스트 항목의 표시 거리를 동적으로 제어할 수 있습니다.

## 구현된 기능

### 1. PlaceListManager.cs 수정사항

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

#### 슬라이더 초기화 (Start 메서드)
- 최소값: 50m
- 최대값: 2000m
- 기본값: 1000m
- 슬라이더 값 변경 시 자동으로 UI 업데이트

#### 거리 필터링 로직
- UpdateUI() 메서드에서 모든 장소(우팡 데이터 + TourAPI 데이터)에 대해 거리 체크
- `maxDisplayDistance`를 초과하는 장소는 리스트에서 제외
- 실시간으로 슬라이더 값에 따라 표시되는 오브젝트 수 변경

#### 추가된 공개 메서드
```csharp
public void SetMaxDisplayDistance(float distance)  // 외부에서 거리 설정
public float GetMaxDisplayDistance()                // 현재 거리 값 조회
```

### 2. TourAPIManager.cs 수정사항

#### 추가된 필드
```csharp
[Header("Loading Indicator")]
[Tooltip("로딩 중 표시할 3D 구형 인디케이터")]
[SerializeField] private GameObject loadingIndicator;
```

#### Debug Text 필드
- `debugText`를 private으로 변경하여 Inspector에서 숨김
- 내부 로직은 그대로 유지 (기존 코드와 호환성 유지)

## Unity 설정 가이드 (자동 프리팹 사용)

### 1. Unity에서 프리팹 추가

1. **Unity에서 Assets 새로고침**
   - 메뉴: Assets → Refresh
   - 또는 단축키: `Ctrl + R`

2. **Hierarchy에서 ListPanel 찾기**
   - Scene에서 ListPanel GameObject 선택

3. **DistanceSliderPanel 프리팹을 ListPanel의 자식으로 추가**
   - Project 창에서 `Assets/Prefabs/DistanceSliderPanel.prefab` 찾기
   - Hierarchy의 **ListPanel** 위로 드래그 앤 드롭
   - ListPanel 하위에 자식으로 추가됨

### 2. PlaceListManager 연결

1. **Hierarchy에서 PlaceListManager가 연결된 오브젝트 선택**

2. **Inspector에서 Distance Filter 섹션 설정:**
   - **Distance Slider**:
     - Hierarchy에서 `ListPanel/DistanceSliderPanel/DistanceSlider` 찾기
     - Inspector의 Distance Slider 필드로 드래그 앤 드롭

   - **Max Display Distance**: 초기 최대 거리 (기본값: 1000m)

   - **Distance Value Text**:
     - Hierarchy에서 `ListPanel/DistanceSliderPanel/DistanceValueText` 찾기
     - Inspector의 Distance Value Text 필드로 드래그 앤 드롭

### 3. UI 레이아웃 확인

생성된 프리팹 구조:
```
DistanceSliderPanel (Panel - 반투명 검은 배경)
├── DistanceSlider (Slider)
│   ├── Background (회색)
│   ├── Fill Area
│   │   └── Fill (파란색)
│   └── Handle Slide Area
│       └── Handle (흰색 원)
└── DistanceValueText (Text - "1.0km")
```

**레이아웃 특징:**
- Panel 배경: 반투명 검은색 (Alpha 0.8)
- Slider: 왼쪽 75% 차지
- Text: 오른쪽 25% 차지 (거리 값 표시)
- 높이: 40px
- ListPanel 상단에 배치 (Anchor: Top, Y=-30)

### TourAPIManager 설정

1. **Hierarchy에서 TourAPIManager가 연결된 오브젝트 선택**

2. **Inspector에서 Loading Indicator 섹션:**
   - **Loading Indicator**: Assets/Prefabs/LoadingIndicator.prefab을 드래그 앤 드롭

3. **Debug Text:**
   - Inspector에서 더 이상 표시되지 않음
   - 필요한 경우 코드에서 수동으로 설정 가능

## 사용 예시

### 슬라이더로 거리 조정
1. 앱 실행 중 슬라이더를 좌우로 드래그
2. 50m ~ 2000m 범위에서 표시 거리 조정
3. 리스트와 AR 오브젝트가 실시간으로 업데이트됨

### 거리 값 표시
- Distance Value Text를 연결한 경우:
  - 1000m 미만: "500m" 형식으로 표시
  - 1000m 이상: "1.5km" 형식으로 표시

### 코드에서 제어
```csharp
// PlaceListManager 참조 가져오기
PlaceListManager placeListManager = FindObjectOfType<PlaceListManager>();

// 최대 표시 거리를 500m로 설정
placeListManager.SetMaxDisplayDistance(500f);

// 현재 최대 표시 거리 조회
float currentDistance = placeListManager.GetMaxDisplayDistance();
Debug.Log($"현재 최대 표시 거리: {currentDistance}m");
```

## 동작 원리

### 필터링 프로세스
1. 사용자가 슬라이더 값을 변경
2. `OnDistanceSliderChanged()` 호출
3. `maxDisplayDistance` 값 업데이트
4. `UpdateUI()` 호출
5. 모든 장소에 대해 거리 계산
6. `maxDisplayDistance`를 초과하는 장소는 `continue`로 건너뜀
7. 조건을 만족하는 장소만 `combinedPlaces` 리스트에 추가
8. 거리순으로 정렬 후 UI에 표시

### 성능 최적화
- 슬라이더 값 변경 시에만 UI 업데이트 (과도한 업데이트 방지)
- 거리 계산은 이미 필터링된 장소에 대해서만 수행
- 주기적인 UI 업데이트(`updateInterval`)는 그대로 유지

## 테스트 체크리스트

- [ ] Unity Editor에서 PlaceListManager Inspector 확인
- [ ] Distance Slider 필드에 Slider UI 연결
- [ ] Distance Value Text 필드에 Text UI 연결 (선택사항)
- [ ] Play 모드에서 슬라이더 동작 확인
- [ ] 슬라이더 값에 따라 리스트 항목 수가 변경되는지 확인
- [ ] 거리 값 텍스트가 올바르게 표시되는지 확인 (연결한 경우)
- [ ] TourAPIManager에 LoadingIndicator 연결
- [ ] TourAPIManager의 Debug Text가 Inspector에서 숨겨졌는지 확인

## 파일 위치

- **PlaceListManager.cs**: `Assets/Scripts/Download/PlaceListManager.cs`
- **TourAPIManager.cs**: `Assets/Scripts/Download/TourAPIManager.cs`

## 수정 날짜
2025-11-28
