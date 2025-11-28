# 핀치 줌 & 필터 버튼 설정 가이드

## 1. 핀치 줌 기능 설정

### 1-1. AR Camera에 PinchZoomController 추가
1. Hierarchy에서 `AR Session Origin` > `AR Camera` 선택
2. Inspector에서 `Add Component` 클릭
3. `PinchZoomController` 검색 후 추가
4. 다음 값 설정:
   - **AR Camera**: AR Camera 자신을 드래그 (자동으로 설정됨)
   - **Default FOV**: 144
   - **Min FOV**: 60 (최대 확대)
   - **Max FOV**: 144 (기본/축소)
   - **Zoom Speed**: 0.5
   - **Zoom Indicator**: 다음 단계에서 생성할 ZoomIndicator GameObject를 드래그

### 1-2. 줌 인디케이터 UI 생성
1. Canvas 선택 > 우클릭 > `Create Empty`
2. 이름을 `ZoomIndicator`로 변경
3. RectTransform 설정:
   - **Anchor**: Top-Right (우측 상단)
   - **Anchor Min**: (1, 1)
   - **Anchor Max**: (1, 1)
   - **Pivot**: (1, 1)
   - **Anchored Position**: (-20, -20) (우측 상단에서 약간 안쪽)
   - **Width**: 150
   - **Height**: 80

4. ZoomIndicator에 `CanvasGroup` 컴포넌트 추가

5. ZoomIndicator 하위에 `Image` 생성 (이름: `ZoomIcon`):
   - Width: 60, Height: 60
   - 확대/축소 아이콘 Sprite 할당 (돋보기 아이콘 권장)

6. ZoomIndicator 하위에 `Text` 생성 (이름: `ZoomText`):
   - Anchored Position: (0, -40)
   - Width: 150, Height: 40
   - Font Size: 40
   - Alignment: Center-Middle
   - Text: "1.0x"

7. ZoomIndicator GameObject에 `ZoomIndicator` 스크립트 추가:
   - **Canvas Group**: 자동으로 찾아지거나 수동으로 드래그
   - **Zoom Text**: ZoomText GameObject 드래그
   - **Zoom Icon**: ZoomIcon Image 드래그
   - **Zoom In Icon**: 확대 아이콘 Sprite 할당 (선택사항)
   - **Zoom Out Icon**: 축소 아이콘 Sprite 할당 (선택사항)
   - **Zoom Reset Icon**: 기본 아이콘 Sprite 할당 (선택사항)

8. PinchZoomController의 **Zoom Indicator**에 ZoomIndicator GameObject 드래그

## 2. 필터 버튼 패널 설정

### 2-1. FilterButtonPanel 생성
1. Hierarchy에서 `Canvas` > `ListPanel` 선택
2. 우클릭 > `UI` > `Panel` 생성
3. 이름을 `FilterButtonPanel`로 변경
4. RectTransform 설정:
   - **Anchor**: Bottom-Left (왼쪽 하단)
   - **Anchor Min**: (0, 0)
   - **Anchor Max**: (0, 0)
   - **Pivot**: (0, 0)
   - **Anchored Position**: (20, 250) (왼쪽 하단에서 약간 위쪽)
   - **Width**: 200
   - **Height**: 300
5. Image 컴포넌트 Color: 반투명 검정 (0, 0, 0, 180)

### 2-2. 필터 토글 생성
FilterButtonPanel 하위에 다음 토글들을 생성:

#### A. 공공데이터 토글 (petFriendlyToggle)
1. FilterButtonPanel 우클릭 > `UI` > `Toggle` 생성
2. 이름: `PetFriendlyToggle`
3. RectTransform:
   - Anchored Position: (10, -30)
   - Width: 180, Height: 40
4. Label Text: "🐕 애견동반"

#### B. 공공데이터 토글 (publicDataToggle)
1. FilterButtonPanel 우클릭 > `UI` > `Toggle` 생성
2. 이름: `PublicDataToggle`
3. RectTransform:
   - Anchored Position: (10, -80)
   - Width: 180, Height: 40
4. Label Text: "🏛️ 공공데이터"

#### C. 지하철 토글 (subwayToggle) - 선택사항
1. Anchored Position: (10, -130)
2. Label Text: "🚇 지하철"

#### D. 버스 토글 (busToggle) - 선택사항
1. Anchored Position: (10, -180)
2. Label Text: "🚌 버스"

#### E. 주류 토글 (alcoholToggle) - 선택사항
1. Anchored Position: (10, -230)
2. Label Text: "🍺 주류"

### 2-3. FilterManager 스크립트 추가
1. FilterButtonPanel 선택
2. `Add Component` > `FilterManager` 추가
3. Inspector에서 다음 설정:
   - **Pet Friendly Toggle**: PetFriendlyToggle 드래그
   - **Public Data Toggle**: PublicDataToggle 드래그
   - **Subway Toggle**: SubwayToggle 드래그 (있는 경우)
   - **Bus Toggle**: BusToggle 드래그 (있는 경우)
   - **Alcohol Toggle**: AlcoholToggle 드래그 (있는 경우)
   - **Place List Manager**: PlaceListManager GameObject 찾아서 드래그

## 3. 테스트

### 핀치 줌 테스트:
- 앱 실행 후 두 손가락으로 화면을 터치
- 손가락을 벌리면 확대 (FOV 감소, 줌 레벨 증가)
- 손가락을 오므리면 축소 (FOV 증가, 줌 레벨 감소)
- 우측 상단에 "1.0x", "1.5x", "2.0x" 등 줌 레벨 표시
- 에디터에서는 마우스 스크롤로 테스트 가능

### 필터 버튼 테스트:
- 왼쪽 하단의 FilterButtonPanel이 표시되는지 확인
- 각 토글을 클릭하여 리스트가 필터링되는지 확인
- "공공데이터" 토글을 끄면 TourAPI 데이터가 숨겨짐
- "애견동반" 토글을 끄면 애견동반 장소가 숨겨짐

## 4. 추가 커스터마이징

### 줌 범위 조정:
- **Min FOV**를 더 작게 (예: 40) → 더 많이 확대 가능
- **Max FOV**를 더 크게 (예: 160) → 더 넓은 시야각

### 줌 속도 조정:
- **Zoom Speed**를 크게 (예: 1.0) → 빠른 줌
- **Zoom Speed**를 작게 (예: 0.3) → 부드러운 줌

### 필터 패널 위치 변경:
- FilterButtonPanel의 Anchored Position을 조정하여 원하는 위치로 이동

## 주요 기능

### PinchZoomController:
- ✅ 두 손가락 핀치 제스처로 확대/축소
- ✅ 기본 FOV 144 유지
- ✅ 최소 FOV 60 (2.4배 확대)
- ✅ 실시간 줌 인디케이터 표시
- ✅ 에디터에서 마우스 스크롤 테스트 지원

### ZoomIndicator:
- ✅ 우측 상단에 줌 레벨 표시
- ✅ 자동 페이드인/페이드아웃
- ✅ 확대/축소 아이콘 변경 (선택사항)
- ✅ 2초 후 자동 숨김

### FilterManager:
- ✅ 애견동반 필터링
- ✅ 공공데이터(TourAPI) 필터링
- ✅ 실시간 리스트 업데이트
- ✅ 필터 상태 저장 (세션 유지)
