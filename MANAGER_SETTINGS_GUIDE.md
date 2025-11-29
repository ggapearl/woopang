# Manager 설정값 가이드

## 📊 설정값 역할 정리

### 중요: 설정값은 중복이 아닙니다!

각 Manager의 설정값은 서로 다른 역할을 하며, 중복되지 않습니다.

## DataManager & TourAPIManager 설정

### 1. Update Interval (updateInterval)
**역할**: 네트워크 요청 주기 (서버에서 데이터 받아오기)

```
권장값: 600초 (10분)
현재값: 600초 ✅

의미:
- 10분마다 서버에 데이터 요청
- AR 오브젝트 생성/업데이트 주기
- 네트워크 트래픽 발생
```

### 2. Update Distance Threshold (updateDistanceThreshold)
**역할**: 위치 이동 감지 거리

```
권장값: 50m ⚠️
현재값: DataManager 5000m (수정 필요!) ❌
        TourAPIManager 50m ✅

의미:
- 사용자가 이 거리만큼 이동하면 즉시 데이터 재요청
- 10분 주기를 기다리지 않고 즉시 업데이트
- 50m 이동 시 = 주변 환경이 바뀌었으므로 새 데이터 필요

⚠️ 5000m는 너무 큰 값!
- 사용자가 5km를 이동해야 업데이트
- AR 앱에서는 50m가 적절
```

### 3. Pool Size (poolSize)
**역할**: 오브젝트 풀 크기 (재사용 가능한 오브젝트 개수)

```
권장값: 20
현재값: 20 ✅

의미:
- 미리 생성해두는 오브젝트 개수
- 성능 최적화용 (매번 생성/삭제 대신 재사용)
- 업데이트 주기와는 무관
```

### 4. Load Radii (loadRadii)
**역할**: Progressive Loading 거리 단계

```
권장값: [25, 50, 75, 100, 150, 200]
현재값: [25, 50, 75, 100, 150, 200] ✅

의미:
- 25m → 50m → 75m → 100m → 150m → 200m 순서로 로딩
- 가까운 오브젝트 먼저 표시 (사용자 경험 향상)
- 한 번의 업데이트에서 여러 단계로 나눠서 로딩
```

## PlaceListManager 설정

### Update Interval (updateInterval)
**역할**: UI 리스트 업데이트 주기 (메모리 읽기만)

```
권장값: 10초
현재값: 10초 ✅

의미:
- ListPanel 활성화 시에만 10초마다 UI 업데이트
- 네트워크 요청 없음! (메모리 읽기만)
- DataManager와 TourAPIManager가 이미 로드한 데이터를 정렬/표시만
```

## 작동 흐름

### 네트워크 요청 (서버 부하 발생)
```
1. 앱 시작 → 즉시 데이터 로드
2. 이후:
   - 10분마다 자동 업데이트 (updateInterval)
   - OR 50m 이동 시 즉시 업데이트 (updateDistanceThreshold)
```

### UI 업데이트 (서버 부하 없음)
```
1. ListPanel 열림 → 즉시 UI 업데이트 (OnEnable)
2. ListPanel 활성화 중:
   - 10초마다 UI 업데이트 (메모리 읽기만)
3. ListPanel 닫힘:
   - 업데이트 중단 (CPU 절약)
```

## Unity Inspector 설정 방법

### DataManager Inspector
```
1. Hierarchy에서 DataManager가 있는 오브젝트 선택

2. Inspector 설정:
   - Pool Size: 20
   - Update Interval: 600
   - Load Radii: [25, 50, 75, 100, 150, 200]
   - Tier Delay: 1.0
   - Object Spawn Delay: 0.5
   - Distance Filter > Place List Manager: (PlaceListManager 오브젝트 드래그)
   - Update Distance Threshold: 50 ⚠️ (현재 5000이면 수정 필요!)
```

### TourAPIManager Inspector
```
1. Hierarchy에서 TourAPIManager가 있는 오브젝트 선택

2. Inspector 설정:
   - Pool Size: 20
   - Update Interval: 600
   - Progressive Loading Settings > Load Radii: [25, 50, 75, 100, 150, 200]
   - Update Distance Threshold: 50 ✅
   - Distance Filter > Place List Manager: (PlaceListManager 오브젝트 드래그)
```

### PlaceListManager Inspector
```
1. Hierarchy에서 PlaceListManager가 있는 오브젝트 선택

2. Inspector 설정:
   - UI Update Settings > List Panel: (ListPanel GameObject 드래그)
   - UI Update Settings > Update Interval: 10
   - AR Object Distance Filter > Distance Slider: (Slider UI 드래그)
   - AR Object Distance Filter > Max Display Distance: 200
   - AR Object Distance Filter > Distance Value Text: (Text UI 드래그)
```

## ⚠️ 현재 수정 필요 사항

### DataManager.updateDistanceThreshold = 5000m → 50m 변경

**문제:**
- 현재 5000m로 설정되어 있으면 사용자가 5km를 이동해야 업데이트
- AR 앱에서는 50m 이동 시 즉시 업데이트하는 것이 적절

**해결:**
1. Unity Editor 열기
2. Hierarchy에서 DataManager 선택
3. Inspector에서 "Update Distance Threshold" 찾기
4. 값을 **50**으로 변경
5. 씬 저장 (Ctrl+S)

## 성능 영향 요약

### 서버 부하 발생:
- **DataManager & TourAPIManager의 updateInterval (600초)**
- **updateDistanceThreshold (50m 이동 시)**
- 약 10분마다 OR 50m 이동 시 네트워크 요청

### 서버 부하 없음:
- **PlaceListManager의 updateInterval (10초)**
- 메모리 읽기만 (이미 로드된 데이터 정렬/표시)
- CPU 사용량 매우 낮음 (0.1% 미만)

## 결론

1. **DataManager & TourAPIManager**: 서버 데이터 로딩 (10분 OR 50m 이동)
2. **PlaceListManager**: UI 표시만 (10초, ListPanel 활성화 시)
3. **중복 없음**: 각자 다른 역할을 수행
4. **수정 필요**: DataManager의 updateDistanceThreshold를 5000m → 50m로 변경

## 수정 날짜
2025-11-29
