# Unity Inspector 설정 체크리스트

## ⚠️ 필수 수정 사항

현재 씬(WP_1119.unity)에서 **반드시 수정해야 할 항목**들입니다.

---

## 1️⃣ DataManager (DownloadCube_쾌)

**Hierarchy 위치**: `DownloadCube_쾌`

### ❌ 현재 잘못된 설정값:
```
updateInterval: 1800 (30분) ❌
updateDistanceThreshold: 5000 (5km) ❌
placeListManager: None ❌
loadingIndicator: None ⚠️
```

### ✅ 수정해야 할 값:

1. **Update Interval**: `1800` → **`600`** (10분)
   - 현재: 30분마다 업데이트
   - 권장: 10분마다 업데이트

2. **Update Distance Threshold**: `5000` → **`50`** (50m)
   - 현재: 5km 이동 시 업데이트 (너무 큼!)
   - 권장: 50m 이동 시 즉시 업데이트

3. **Place List Manager**: `None` → **`PlaceListManager` 오브젝트 드래그**
   - Hierarchy에서 `PlaceListManager` 찾아서 드래그

4. **Loading Indicator**: `None` → **`LoadingIndicator` 프리팹 드래그** (선택사항)
   - Assets/Prefabs/LoadingIndicator.prefab을 드래그

### ✅ 올바른 설정값 (수정 후):
```
Pool Size: 20 ✅ (유지)
Update Interval: 600 ✅ (10분)
Load Radii: [25, 50, 75, 100, 150, 200] ✅ (유지)
Tier Delay: 1 ✅ (유지)
Object Spawn Delay: 0.5 ✅ (유지)
Place List Manager: PlaceListManager ✅ (연결 필요)
Update Distance Threshold: 50 ✅ (수정 필요)
Loading Indicator: LoadingIndicator 프리팹 ✅ (선택사항)
```

---

## 2️⃣ TourAPIManager (DownloadCube_TourAPI_Petfriendly)

**Hierarchy 위치**: `DownloadCube_TourAPI_Petfriendly`

### ❌ 현재 잘못된 설정값:
```
updateInterval: 3600 (1시간) ❌
updateDistanceThreshold: 10000 (10km) ❌
loadRadius: 20000 (20km) ❌ (이 필드는 삭제됨)
placeListManager: None ❌
loadingIndicator: None ⚠️
loadRadii: 없음 ❌ (Progressive Loading 설정 필요)
```

### ✅ 수정해야 할 값:

1. **Update Interval**: `3600` → **`600`** (10분)
   - 현재: 1시간마다 업데이트
   - 권장: 10분마다 업데이트

2. **Update Distance Threshold**: `10000` → **`50`** (50m)
   - 현재: 10km 이동 시 업데이트 (너무 큼!)
   - 권장: 50m 이동 시 즉시 업데이트

3. **⚠️ Load Radius 필드 삭제됨** - 이제 `loadRadii` 배열 사용
   - Unity가 자동으로 필드를 업데이트할 것임
   - 만약 Inspector에 여전히 보이면 무시

4. **Load Radii**: Progressive Loading 배열 **추가 필요**
   - 값: `[25, 50, 75, 100, 150, 200]`
   - Inspector에서 직접 입력

5. **Place List Manager**: `None` → **`PlaceListManager` 오브젝트 드래그**
   - Hierarchy에서 `PlaceListManager` 찾아서 드래그

6. **Loading Indicator**: `None` → **`LoadingIndicator` 프리팹 드래그** (선택사항)
   - Assets/Prefabs/LoadingIndicator.prefab을 드래그

### ✅ 올바른 설정값 (수정 후):
```
Pool Size: 20 ✅ (유지)
Update Interval: 600 ✅ (수정 필요)
Progressive Loading Settings:
  Load Radii: [25, 50, 75, 100, 150, 200] ✅ (추가 필요)
Update Distance Threshold: 50 ✅ (수정 필요)
Distance Filter:
  Place List Manager: PlaceListManager ✅ (연결 필요)
Loading Indicator: LoadingIndicator 프리팹 ✅ (선택사항)
```

---

## 3️⃣ PlaceListManager

**Hierarchy 위치**: `PlaceListManager`

### ❌ 현재 잘못된 설정값:
```
updateInterval: 10 ✅ (올바름)
maxDisplayDistance: 1000 ❌ (너무 큼)
distanceSlider: None ❌
distanceValueText: None ❌
listPanel: 없음 ❌ (필드 자체가 Inspector에 없을 수 있음)
```

### ✅ 수정해야 할 값:

1. **List Panel**: **`ListPanel` GameObject 드래그**
   - Hierarchy에서 `Canvas > ListPanel` 찾아서 드래그
   - 이 필드가 Inspector에 보여야 함

2. **Update Interval**: `10` ✅ (올바름 - 유지)

3. **Max Display Distance**: `1000` → **`200`** (200m)
   - 현재: 1000m 범위
   - 권장: 200m 범위 (AR 오브젝트 기본 최대 거리)

4. **Distance Slider**: `None` → **`DistanceSlider` UI 드래그**
   - ⚠️ 먼저 DistanceSlider UI를 생성해야 함 (아래 참고)

5. **Distance Value Text**: `None` → **`DistanceValueText` UI 드래그**
   - ⚠️ 먼저 DistanceSlider UI를 생성해야 함 (아래 참고)

### ✅ 올바른 설정값 (수정 후):
```
Data Manager: DownloadCube_쾌 ✅ (이미 연결됨)
Tour API Manager: DownloadCube_TourAPI_Petfriendly ✅ (이미 연결됨)
List Text: (이미 연결됨) ✅

UI Update Settings:
  List Panel: Canvas/ListPanel ✅ (연결 필요)
  Update Interval: 10 ✅ (유지)

AR Object Distance Filter:
  Distance Slider: ListPanel/DistanceSliderUI/DistanceSlider ✅ (UI 생성 후 연결)
  Max Display Distance: 200 ✅ (수정 필요)
  Distance Value Text: ListPanel/DistanceSliderUI/DistanceValueText ✅ (UI 생성 후 연결)
```

---

## 4️⃣ DistanceSlider UI 생성 (아직 안 만들어진 경우)

### 방법 1: Unity Editor에서 수동 생성 (권장)

1. **Hierarchy에서 ListPanel 선택**
2. **우클릭 → UI → Slider 생성**
   - 이름: `DistanceSlider`
3. **우클릭 → UI → Text 생성**
   - 이름: `DistanceValueText`
4. **Slider 설정**:
   - Min Value: 50
   - Max Value: 200
   - Value: 200
5. **Text 설정**:
   - 텍스트: "200m"
   - Font Size: 50

자세한 방법: [CREATE_DISTANCE_SLIDER_MANUAL.md](CREATE_DISTANCE_SLIDER_MANUAL.md) 참고

### 방법 2: 프리팹 사용 (실험적)

Python 스크립트로 프리팹 생성:
```bash
python create_working_slider_prefab.py
```

생성 후 Unity에서:
1. Assets → Refresh (Ctrl+R)
2. Project 창에서 `Assets/Prefabs/DistanceSliderUI.prefab` 찾기
3. Hierarchy의 `ListPanel` 위로 드래그 앤 드롭

---

## 📋 전체 수정 순서

### 1단계: DataManager 수정
1. Hierarchy에서 `DownloadCube_쾌` 선택
2. Inspector에서 수정:
   - Update Interval: `600`
   - Update Distance Threshold: `50`
   - Place List Manager: `PlaceListManager` 드래그
3. 저장 (Ctrl+S)

### 2단계: TourAPIManager 수정
1. Hierarchy에서 `DownloadCube_TourAPI_Petfriendly` 선택
2. Inspector에서 수정:
   - Update Interval: `600`
   - Update Distance Threshold: `50`
   - Load Radii: Size=6, 값=[25, 50, 75, 100, 150, 200]
   - Place List Manager: `PlaceListManager` 드래그
3. 저장 (Ctrl+S)

### 3단계: DistanceSlider UI 생성
- 방법 1(수동) 또는 방법 2(프리팹) 선택하여 생성

### 4단계: PlaceListManager 수정
1. Hierarchy에서 `PlaceListManager` 선택
2. Inspector에서 수정:
   - List Panel: `Canvas/ListPanel` 드래그
   - Max Display Distance: `200`
   - Distance Slider: `ListPanel/DistanceSlider` 드래그
   - Distance Value Text: `ListPanel/DistanceValueText` 드래그
3. 저장 (Ctrl+S)

### 5단계: 씬 저장
- File → Save (Ctrl+S)
- 프로젝트 저장

---

## ✅ 최종 확인 체크리스트

체크해야 할 항목:

- [ ] DataManager - Update Interval = 600
- [ ] DataManager - Update Distance Threshold = 50
- [ ] DataManager - Place List Manager 연결됨
- [ ] TourAPIManager - Update Interval = 600
- [ ] TourAPIManager - Update Distance Threshold = 50
- [ ] TourAPIManager - Load Radii = [25, 50, 75, 100, 150, 200]
- [ ] TourAPIManager - Place List Manager 연결됨
- [ ] PlaceListManager - List Panel 연결됨
- [ ] PlaceListManager - Max Display Distance = 200
- [ ] PlaceListManager - Distance Slider 연결됨 (UI 생성 후)
- [ ] PlaceListManager - Distance Value Text 연결됨 (UI 생성 후)
- [ ] 컴파일 에러 없음
- [ ] 씬 저장 완료

---

## 🔍 Inspector에서 확인하는 방법

### DataManager 확인:
```
Hierarchy → DownloadCube_쾌 선택 → Inspector 확인

[Inspector 내용]
Cube Prefab: ✅
GLB Prefab: ✅
Max Concurrent GLB Loads: 10
GLB Load Timeout: 30
Fallback To Cube: ✅
Loading Indicator: (프리팹)
Pool Size: 20
Update Interval: 600 ← 확인!
Load Radii: Size 6
  [0]: 25
  [1]: 50
  [2]: 75
  [3]: 100
  [4]: 150
  [5]: 200
Tier Delay: 1
Object Spawn Delay: 0.5
Place List Manager: PlaceListManager ← 확인!
Update Distance Threshold: 50 ← 확인!
```

### TourAPIManager 확인:
```
Hierarchy → DownloadCube_TourAPI_Petfriendly 선택 → Inspector 확인

[Inspector 내용]
Sample Prefab: ✅
Loading Indicator: (프리팹)
Distance Filter:
  Place List Manager: PlaceListManager ← 확인!
Pool Size: 20
Update Interval: 600 ← 확인!
Progressive Loading Settings:
  Load Radii: Size 6 ← 확인!
    [0]: 25
    [1]: 50
    [2]: 75
    [3]: 100
    [4]: 150
    [5]: 200
Update Distance Threshold: 50 ← 확인!
```

### PlaceListManager 확인:
```
Hierarchy → PlaceListManager 선택 → Inspector 확인

[Inspector 내용]
Data Manager: DownloadCube_쾌 ✅
Tour API Manager: DownloadCube_TourAPI_Petfriendly ✅
List Text: ✅

UI Update Settings:
  List Panel: Canvas/ListPanel ← 확인!
  Update Interval: 10 ✅

AR Object Distance Filter:
  Distance Slider: ListPanel/DistanceSlider ← 확인!
  Max Display Distance: 200 ← 확인!
  Distance Value Text: ListPanel/DistanceValueText ← 확인!
```

---

## 📝 참고 문서

- [MANAGER_SETTINGS_GUIDE.md](MANAGER_SETTINGS_GUIDE.md) - 설정값 상세 설명
- [DATA_LOADING_OPTIMIZATION_SUMMARY.md](DATA_LOADING_OPTIMIZATION_SUMMARY.md) - 최적화 설명
- [DISTANCE_FILTER_COMPLETE_GUIDE.md](DISTANCE_FILTER_COMPLETE_GUIDE.md) - 거리 필터 가이드
- [CREATE_DISTANCE_SLIDER_MANUAL.md](CREATE_DISTANCE_SLIDER_MANUAL.md) - UI 수동 생성 가이드

---

## 수정 날짜
2025-11-29
