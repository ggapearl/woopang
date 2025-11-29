# 거리 슬라이더 UI 수동 생성 가이드

프리팹 파일이 Unity에서 인식되지 않으므로, Unity Editor에서 직접 만드는 방법입니다.

## 1단계: ListPanel 찾기 (10초)

1. Unity Editor 열기
2. Hierarchy 창에서 **ListPanel** 검색 또는 찾기
3. ListPanel 선택

## 2단계: Panel 배경 만들기 (30초)

1. **Hierarchy에서 ListPanel 우클릭**
2. **UI → Panel** 선택
3. 생성된 Panel 이름을 **DistanceSliderPanel**로 변경

### DistanceSliderPanel 설정

**Rect Transform:**
```
Anchor: Top Stretch (상단 중앙, 좌우 늘림)
  - Anchor Presets 클릭 → 상단 줄 가운데 선택 (Shift+Alt 누른 상태에서)
Pos Y: -30
Height: 40
Left: 10
Right: 10
```

**Image 컴포넌트:**
```
Color: 검은색 (R:0, G:0, B:0, A:200)
```

## 3단계: Slider 만들기 (1분)

1. **Hierarchy에서 DistanceSliderPanel 우클릭**
2. **UI → Slider** 선택
3. 생성된 Slider 이름을 **DistanceSlider**로 변경

### DistanceSlider 설정

**Rect Transform:**
```
Anchor: Left Stretch (왼쪽 늘림)
  - Anchor Presets → 왼쪽 줄 가운데
Pos X: 10
Pos Y: 0
Width: 원하는 크기 (패널의 75% 정도)
Height: 20
```

**Slider 컴포넌트:**
```
Min Value: 50
Max Value: 2000
Whole Numbers: ✓ (체크)
Value: 1000
```

### Slider 색상 꾸미기 (선택사항)

**Background:**
- Hierarchy: `DistanceSlider/Background`
- Image Color: 회색 (R:77, G:77, B:77, A:200)

**Fill:**
- Hierarchy: `DistanceSlider/Fill Area/Fill`
- Image Color: 파란색 (R:51, G:153, B:255, A:255)

**Handle:**
- Hierarchy: `DistanceSlider/Handle Slide Area/Handle`
- Image Color: 흰색 (R:255, G:255, B:255, A:255)

## 4단계: 거리 표시 Text 만들기 (30초)

1. **Hierarchy에서 DistanceSliderPanel 우클릭**
2. **UI → Text** 선택
3. 생성된 Text 이름을 **DistanceValueText**로 변경

### DistanceValueText 설정

**Rect Transform:**
```
Anchor: Right (오른쪽)
  - Anchor Presets → 오른쪽 줄 가운데
Pos X: -50
Pos Y: 0
Width: 80
Height: 30
```

**Text 컴포넌트:**
```
Text: "1.0km"
Font Size: 18
Font Style: Bold
Alignment: Center, Middle
Color: 흰색 (R:255, G:255, B:255, A:255)
```

## 5단계: PlaceListManager에 연결 (1분)

1. **Hierarchy에서 PlaceListManager가 연결된 오브젝트 선택**
   - 보통 "Managers" 또는 "GameController" 같은 오브젝트

2. **Inspector의 PlaceListManager (Script) 찾기**

3. **Distance Filter 섹션:**

   **Distance Slider 필드:**
   - Hierarchy에서 `DistanceSliderPanel/DistanceSlider` 선택
   - Inspector의 Distance Slider 필드로 드래그

   **Distance Value Text 필드:**
   - Hierarchy에서 `DistanceSliderPanel/DistanceValueText` 선택
   - Inspector의 Distance Value Text 필드로 드래그

## 6단계: 완료! 테스트 (10초)

1. **Play 버튼** 클릭
2. 슬라이더를 좌우로 드래그
3. 거리 값이 업데이트되는지 확인
4. 리스트 항목이 거리에 따라 필터링되는지 확인

## 최종 Hierarchy 구조

```
ListPanel
└── DistanceSliderPanel (Panel - 배경)
    ├── DistanceSlider (Slider)
    │   ├── Background
    │   ├── Fill Area
    │   │   └── Fill
    │   └── Handle Slide Area
    │       └── Handle
    └── DistanceValueText (Text)
```

## 시각적 레이아웃

```
┌─────────────────────────────────────────┐
│ DistanceSliderPanel                     │
│ ┌──────────────────────┐  ┌──────────┐ │
│ │ DistanceSlider       │  │ 1.0km    │ │
│ │ ──────●──────────────│  │          │ │
│ └──────────────────────┘  └──────────┘ │
└─────────────────────────────────────────┘
```

## 문제 해결

### Slider가 작동하지 않아요
- PlaceListManager Inspector에서 Distance Slider 필드가 연결되었는지 확인
- DistanceSlider의 Slider 컴포넌트가 활성화되어 있는지 확인

### 거리 텍스트가 업데이트되지 않아요
- Distance Value Text 필드가 올바르게 연결되었는지 확인
- DistanceValueText의 Text 컴포넌트가 있는지 확인

### UI가 보이지 않아요
- Canvas가 Scene에 있는지 확인
- DistanceSliderPanel이 Canvas 하위에 있는지 확인
- Camera에 Canvas가 보이는지 확인 (Canvas Render Mode 확인)

## 빠른 참조

### Anchor Presets 위치
- Top Stretch: Shift+Alt 누르고 → 상단 중간 클릭
- Left: 왼쪽 줄 중간 클릭
- Right: 오른쪽 줄 중간 클릭

### 권장 색상
- Panel 배경: RGBA(0, 0, 0, 200) - 반투명 검은색
- Slider Background: RGBA(77, 77, 77, 200) - 회색
- Slider Fill: RGBA(51, 153, 255, 255) - 파란색
- Text: RGBA(255, 255, 255, 255) - 흰색

### 소요 시간
- 전체 작업 시간: **약 3-4분**
- Panel + Slider + Text 생성: 2분
- 연결 및 설정: 1-2분
