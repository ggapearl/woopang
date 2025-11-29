# 거리 슬라이더 빠른 설정 가이드

## 1단계: Unity에서 프리팹 추가 (30초)

1. **Unity에서 `Ctrl + R` (Assets 새로고침)**

2. **Hierarchy에서 ListPanel 찾기**

3. **Project 창에서 `Assets/Prefabs/DistanceSliderPanel.prefab` 드래그**
   - Hierarchy의 **ListPanel** 위로 드롭
   - ListPanel 하위에 자식으로 추가됨

## 2단계: PlaceListManager 연결 (1분)

1. **Hierarchy에서 PlaceListManager가 연결된 오브젝트 선택**

2. **Inspector의 Distance Filter 섹션에서:**

   **Distance Slider 필드:**
   - Hierarchy에서 `ListPanel/DistanceSliderPanel/DistanceSlider` 찾기
   - Inspector의 Distance Slider 필드로 드래그

   **Distance Value Text 필드:**
   - Hierarchy에서 `ListPanel/DistanceSliderPanel/DistanceValueText` 찾기
   - Inspector의 Distance Value Text 필드로 드래그

## 3단계: 완료!

Play 버튼을 눌러서 테스트:
- 슬라이더를 좌우로 드래그
- 거리 값이 50m ~ 2000m 범위에서 변경됨
- 리스트 항목이 실시간으로 필터링됨
- 거리 텍스트가 자동 업데이트 ("500m" 또는 "1.5km")

## UI 레이아웃

```
ListPanel
└── DistanceSliderPanel ← 이 프리팹을 추가했습니다
    ├── DistanceSlider (슬라이더)
    └── DistanceValueText (거리 값)
```

## 시각적 디자인

- **배경**: 반투명 검은색 패널
- **슬라이더**: 파란색 Fill, 흰색 Handle
- **텍스트**: 흰색, 굵은 글씨, 중앙 정렬
- **위치**: ListPanel 상단 (-30px)

## 문제 해결

### 슬라이더가 보이지 않아요
- Hierarchy에서 DistanceSliderPanel이 ListPanel의 자식인지 확인
- DistanceSliderPanel의 Active 체크박스가 켜져있는지 확인

### 슬라이더가 작동하지 않아요
- PlaceListManager Inspector에서 두 필드가 모두 연결되었는지 확인:
  - Distance Slider
  - Distance Value Text

### 거리 값이 업데이트되지 않아요
- Distance Value Text 필드가 올바르게 연결되었는지 확인
- Console에서 에러 메시지 확인

## 추가 커스터마이징

### 슬라이더 색상 변경
1. Hierarchy에서 `DistanceSliderPanel/DistanceSlider/Fill Area/Fill` 선택
2. Inspector의 Image 컴포넌트에서 Color 변경

### 배경 투명도 조정
1. Hierarchy에서 `DistanceSliderPanel` 선택
2. Inspector의 Image 컴포넌트에서 Color의 Alpha 값 조정

### 텍스트 크기 변경
1. Hierarchy에서 `DistanceSliderPanel/DistanceValueText` 선택
2. Inspector의 Text 컴포넌트에서 Font Size 조정

### 위치 조정
1. Hierarchy에서 `DistanceSliderPanel` 선택
2. Inspector의 Rect Transform에서 Anchored Position Y 값 변경
   - 현재: -30 (상단에서 30px 아래)
   - 더 아래로: -50, -70 등
   - 더 위로: -10, 0 등
