# Unity 설정 체크리스트 (WP_1129.unity)

## 🎯 목적
PlaceListManager와 DistanceSliderUI가 정상 작동하도록 Unity Inspector 설정 확인

## 📋 필수 확인사항

### 1. PlaceListManager 설정

**Hierarchy에서 찾기:** `PlaceListManager`

**Inspector 확인:**
```
✅ List Text: ListPanel/Text 연결
✅ Data Manager: DownloadCube_쾌 연결
✅ Tour API Manager: DownloadCube_TourAPI_Petfriendly 연결

UI Update Settings:
✅ List Panel: Canvas/ListPanel 연결
✅ Update Interval: 10

AR Object Distance Filter:
✅ Distance Slider: DistanceSliderUI/DistanceSlider 연결
✅ Max Display Distance: 144
✅ Distance Value Text: DistanceSliderUI/DistanceValueText 연결
```

**중요:**
- `distanceSlider`와 `distanceValueText`가 모두 연결되어야 슬라이더가 작동합니다
- 연결되지 않으면 Console에 경고 표시됨

### 2. DistanceSliderUI 확인

**Hierarchy에서 찾기:** `Canvas/DistanceSliderUI`

**Inspector 확인:**
```
Rect Transform:
- Anchors: (0, 1) to (1, 1)
- Anchored Position: (0, -50)
- Size Delta: (-20, 80)

Image Component:
- Color: (0.1, 0.1, 0.1, 0.8)
```

**자식 오브젝트:**
```
DistanceSliderUI
├── DistanceSlider (Slider 컴포넌트)
│   ├── Min Value: 50
│   ├── Max Value: 200
│   └── Value: 144 ✅
│
└── DistanceValueText (Text 컴포넌트)
    └── Text: "144m" ✅
```

### 3. FilterButtonPanel 설정

**Hierarchy에서 찾기:** `Canvas/FilterButtonPanel`

**Inspector - FilterManager 컴포넌트:**
```
Filter Toggles: ✅ (모두 연결됨)
- Pet Friendly Toggle
- Public Data Toggle
- Subway Toggle
- Bus Toggle
- Alcohol Toggle
- Woopang Data Toggle

Control Buttons: ✅
- Select All Button
- Deselect All Button

References: ⚠️ 연결 필요!
- Place List Manager: PlaceListManager 드래그
- Data Manager: DownloadCube_쾌 드래그
- Tour API Manager: DownloadCube_TourAPI_Petfriendly 드래그

Long Press Settings:
- Long Press Duration: 0.8
```

### 4. DataManager 설정

**Hierarchy에서 찾기:** `DownloadCube_쾌`

**Inspector 확인:**
```
Update Settings:
✅ Update Interval: 600
✅ Update Distance Threshold: 50

Progressive Loading Settings:
✅ Load Radii: [25, 50, 75, 100, 150, 200]
✅ Tier Delay: 1
✅ Object Spawn Delay: 0.5

Distance Filter:
✅ Place List Manager: PlaceListManager 드래그
```

### 5. TourAPIManager 설정

**Hierarchy에서 찾기:** `DownloadCube_TourAPI_Petfriendly`

**Inspector 확인:**
```
Update Settings:
✅ Update Interval: 600
✅ Update Distance Threshold: 50

Progressive Loading Settings:
✅ Load Radii: [25, 50, 75, 100, 150, 200]

References:
✅ Place List Manager: PlaceListManager 드래그
```

## 🔧 문제 해결

### 문제 1: "1.0km"로 표시됨 (144m 대신)

**원인:**
- `distanceValueText`가 PlaceListManager에 연결되지 않음
- 다른 Text 컴포넌트를 참조하고 있음

**해결:**
1. PlaceListManager Inspector 열기
2. Distance Value Text 필드 찾기
3. DistanceSliderUI의 DistanceValueText를 드래그

**Console 확인:**
```
[PlaceListManager] 슬라이더 초기화 완료: value=144m
[PlaceListManager] 거리 텍스트 업데이트: 144m (maxDisplayDistance=144)
```

만약 `distanceValueText가 null입니다!` 경고가 나오면 연결 안 됨.

### 문제 2: ListPanel에 리스트가 표시되지 않음

**Console 확인:**
```
[PlaceListManager] Start() 호출 - listText=True, dataManager=True, tourAPIManager=True
[PlaceListManager] 데이터 로딩 대기 시작...
[PlaceListManager] 데이터 로딩 완료! 첫 UI 업데이트 시작
[PlaceListManager] 데이터 개수 - 우팡: X, TourAPI: Y
[PlaceListManager] 리스트 업데이트 - 전체 데이터: 우팡=X, TourAPI=Y, 필터링 후 표시=Z, 거리제한=144m
```

**원인 확인:**
- `필터링 후 표시=0`이면 → 144m 이내에 데이터가 없음
- 데이터 로딩이 안 되면 → DataManager/TourAPIManager 연결 확인
- `listText가 null입니다!` 에러 → ListPanel/Text 연결 확인

**해결:**
1. GPS가 켜져 있는지 확인
2. 서버 데이터가 있는지 확인 (https://woopang.com/locations?status=approved)
3. Max Display Distance를 200m로 늘려보기

### 문제 3: 슬라이더가 작동하지 않음

**원인:**
- `distanceSlider`가 PlaceListManager에 연결되지 않음

**해결:**
1. PlaceListManager Inspector
2. Distance Slider 필드에 DistanceSliderUI/DistanceSlider 드래그

**Console 확인:**
```
[PlaceListManager] 슬라이더 초기화 완료: value=144m
```

### 문제 4: 필터 토글이 작동하지 않음

**원인:**
- FilterManager의 Manager 참조가 연결되지 않음

**해결:**
1. FilterButtonPanel Inspector
2. References 섹션에서 3개 Manager 모두 연결

## ✅ 최종 테스트

### Play 모드에서 확인:

1. **초기화 로그:**
   ```
   [PlaceListManager] Start() 호출 - listText=True, dataManager=True, tourAPIManager=True
   [PlaceListManager] 슬라이더 초기화 완료: value=144m
   ```

2. **데이터 로딩:**
   ```
   [PlaceListManager] 데이터 로딩 완료! 첫 UI 업데이트 시작
   [PlaceListManager] 데이터 개수 - 우팡: 5, TourAPI: 10
   ```

3. **리스트 표시:**
   - ListPanel 열기
   - 144m 이내 장소들이 리스트로 표시됨
   - 거리순으로 정렬됨

4. **슬라이더 조절:**
   - 슬라이더를 50m로 조정
   - Console: `[PlaceListManager] 거리 텍스트 업데이트: 50m (maxDisplayDistance=50)`
   - 리스트가 즉시 업데이트됨
   - 텍스트가 "50m"로 변경됨

5. **필터 토글:**
   - 애견동반 필터 OFF
   - 리스트에서 애견동반 장소 제거됨
   - Console: `[FilterManager] 필터 적용 - PetFriendly: False...`

## 📊 현재 상태

### 코드 상태 ✅
- PlaceListManager.cs: 144m 기본값 설정 완료 + 상세 디버깅 추가
- FilterManager.cs: Long Press + 토글 수정 완료
- DistanceSliderUI.prefab: 144m 기본값 설정 완료
- WP_1129.unity: 씬 파일 144m 오버라이드 확인됨
- DATA_LOADING_EXPLANATION.md: 전체 데이터 흐름 분석 문서 작성됨

### 추가된 디버그 로그 ✅
```
[PlaceListManager] 필터 상태 - woopangData=true, petFriendly=true, alcohol=true, publicData=true
[PlaceListManager] 우팡데이터 처리 - 전체: 25, 필터링됨: 3, 추가됨: 22
[PlaceListManager] TourAPI데이터 처리 - 전체: 10, 필터링됨: 0, 추가됨: 10
```

### Unity Inspector 설정 필요 ⚠️
- PlaceListManager의 참조 연결
- FilterButtonPanel의 Manager 연결
- 모든 연결은 Unity Editor에서 수동으로 해야 함

## 🚀 다음 단계

Inspector 설정 완료 후:
1. Play 모드로 테스트
2. Console 로그 확인
3. 문제 발생 시 로그 공유

---

**작성일:** 2025-12-01
**최종 커밋:** 320a1d0 - Fix default distance to 144m and add comprehensive debugging

## 🔍 리스트가 표시되지 않는 문제 해결

자세한 데이터 로딩 및 표시 로직은 [DATA_LOADING_EXPLANATION.md](DATA_LOADING_EXPLANATION.md) 참고.

**디버그 체크리스트:**
1. Console에서 `[PlaceListManager] Start() 호출` 로그 확인
2. `listText=True, dataManager=True, tourAPIManager=True` 확인
3. `[DataManager] 서버에서 X개 위치 데이터 수신` 로그 확인 (X > 0)
4. `[PlaceListManager] 필터 상태` 로그에서 woopangData=true 확인
5. `필터링 후 표시=X` 에서 X > 0 확인
