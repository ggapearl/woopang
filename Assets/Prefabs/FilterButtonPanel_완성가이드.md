# FilterButtonPanel 완성 가이드

## 📋 개요
이 문서는 FilterButtonPanel 프리팹에 누락된 4개의 토글과 2개의 버튼을 추가하는 방법을 설명합니다.

## ✅ 현재 상태
- **완료**: PetFriendlyToggle, PublicDataToggle (2개)
- **누락**: SubwayToggle, BusToggle, AlcoholToggle, WoopangDataToggle (4개)
- **누락**: SelectAllButton, DeselectAllButton (2개)

## 🎯 목표 UI 구성
```
FilterButtonPanel (250x600, 왼쪽 상단)
├─ PetFriendlyToggle (60x60) - 애견동반
├─ PublicDataToggle (60x60) - 공공데이터
├─ SubwayToggle (60x60) - 지하철 ⬅ 추가 필요
├─ BusToggle (60x60) - 버스 ⬅ 추가 필요
├─ AlcoholToggle (60x60) - 주류판매 ⬅ 추가 필요
├─ WoopangDataToggle (60x60) - 우팡데이터 ⬅ 추가 필요
├─ SelectAllButton (250x50) - 전체 선택 ⬅ 추가 필요
└─ DeselectAllButton (250x50) - 전체 해제 ⬅ 추가 필요
```

## 🛠 Unity Editor에서 수동 추가 방법

### 1단계: 프리팹 열기
1. `Assets/Prefabs/FilterButtonPanel.prefab` 더블클릭
2. Prefab Mode로 진입

### 2단계: Vertical Layout Group 확인
FilterButtonPanel GameObject에 이미 VerticalLayoutGroup 컴포넌트가 추가되어 있습니다:
- Spacing: 5
- Child Force Expand Width: ✓
- Child Control Height: ✓
- Padding: Left 10, Right 10, Top 10, Bottom 10

### 3단계: 토글 복사 및 수정
#### SubwayToggle 추가
1. **PetFriendlyToggle 복제** (우클릭 > Duplicate 또는 Ctrl+D)
2. **이름 변경**: `SubwayToggle`
3. **Label 텍스트 변경**:
   - SubwayToggle > Label > Text 컴포넌트
   - Text 필드를 `지하철`로 변경

#### BusToggle 추가
1. **PetFriendlyToggle 복제**
2. **이름 변경**: `BusToggle`
3. **Label 텍스트 변경**: `버스`

#### AlcoholToggle 추가
1. **PetFriendlyToggle 복제**
2. **이름 변경**: `AlcoholToggle`
3. **Label 텍스트 변경**: `주류판매`

#### WoopangDataToggle 추가
1. **PetFriendlyToggle 복제**
2. **이름 변경**: `WoopangDataToggle`
3. **Label 텍스트 변경**: `우팡데이터`

### 4단계: 버튼 추가
#### SelectAllButton 추가
1. FilterButtonPanel에서 **우클릭** > UI > Button
2. **이름 변경**: `SelectAllButton`
3. **RectTransform 설정**:
   - Anchors: Stretch Horizontal (Min X: 0, Max X: 1)
   - Height: 50
4. **Image 컴포넌트 색상**:
   - Color: R:51, G:153, B:255, A:204 (파란색)
5. **자식 Text 수정**:
   - Text: `전체 선택`
   - Font Size: 16
   - Alignment: Center

#### DeselectAllButton 추가
1. FilterButtonPanel에서 **우클릭** > UI > Button
2. **이름 변경**: `DeselectAllButton`
3. **RectTransform 설정**:
   - Anchors: Stretch Horizontal
   - Height: 50
4. **Image 컴포넌트 색상**:
   - Color: R:200, G:200, B:200, A:204 (회색)
5. **자식 Text 수정**:
   - Text: `전체 해제`
   - Font Size: 16
   - Alignment: Center

### 5단계: FilterManager 연결
FilterButtonPanel의 **FilterManager 컴포넌트**에서 새로 만든 UI 요소들을 연결:

1. **Subway Toggle** 필드 ➜ SubwayToggle 드래그
2. **Bus Toggle** 필드 ➜ BusToggle 드래그
3. **Alcohol Toggle** 필드 ➜ AlcoholToggle 드래그
4. **Woopang Data Toggle** 필드 ➜ WoopangDataToggle 드래그
5. **Select All Button** 필드 ➜ SelectAllButton 드래그
6. **Deselect All Button** 필드 ➜ DeselectAllButton 드래그

### 6단계: 배치 순서 확인
Hierarchy에서 다음 순서로 정렬 (VerticalLayoutGroup이 자동 정렬):
1. PetFriendlyToggle
2. PublicDataToggle
3. SubwayToggle
4. BusToggle
5. AlcoholToggle
6. WoopangDataToggle
7. SelectAllButton
8. DeselectAllButton

### 7단계: 저장 및 적용
1. Prefab Mode 상단의 **Save** 버튼 클릭
2. Scene으로 돌아오기 (상단 `<` 버튼)
3. WP_1119 씬 저장 (Ctrl+S)

## 🎨 디자인 사양

### 토글 (6개)
- **크기**: 60x60 (Background 체크박스)
- **라벨 폰트 크기**: 18
- **체크마크 크기**: 50x50
- **배경색**: 흰색 (r:1, g:1, b:1)
- **체크마크 색**: 어두운 회색 (r:0.196, g:0.196, b:0.196)

### 버튼 (2개)
- **크기**: 250x50 (전체 너비)
- **폰트 크기**: 16
- **전체 선택 버튼**: 파란색 배경
- **전체 해제 버튼**: 회색 배경

### 패널
- **위치**: 왼쪽 상단 (Anchor: x:0, y:1)
- **오프셋**: x:10, y:-10
- **크기**: 250x600
- **간격**: 5px (Vertical Layout Group Spacing)
- **배경색**: 반투명 검정 (r:0, g:0, b:0, a:0.7)

## 🚀 빠른 자동 설정 (대안)

Unity 에디터 메뉴에서:
```
Tools > Woopang > Setup Filter Button Panel
```

이 메뉴는 프리팹을 자동으로 인스턴스화하고 필요한 모든 매니저를 연결합니다.
**주의**: 수동으로 토글/버튼을 추가한 후에 실행하세요!

## ✨ 기능 설명

### 일반 클릭
각 토글을 클릭하면 ON/OFF 전환

### 길게 누르기 (Long Press)
토글을 0.8초 이상 길게 누르면:
- 해당 토글만 ON
- 나머지 모든 토글 OFF

### 전체 선택/해제
- **전체 선택**: 모든 토글 ON
- **전체 해제**: 모든 토글 OFF

## 🐛 AR 오브젝트 재활성화 버그 테스트

필터를 OFF → ON으로 변경했을 때 AR 오브젝트가 다시 나타나는지 확인:

1. Unity Play Mode 실행
2. 콘솔 창 열기 (Ctrl+Shift+C)
3. 토글을 OFF로 변경
   - 콘솔에서 `[DataManager]` 로그 확인
   - AR 오브젝트가 사라지는지 확인
4. 같은 토글을 다시 ON으로 변경
   - 콘솔에서 `비활성 → 활성` 로그 확인
   - **AR 오브젝트가 다시 나타나야 함** ⬅ 여기가 버그!

## 📝 참고 파일
- `Assets/Scripts/UI/FilterManager.cs` - 필터 로직 및 Long Press 구현
- `Assets/Scripts/Download/DataManager.cs` - AR 큐브 필터링 (우팡 데이터)
- `Assets/Scripts/Download/TourAPIManager.cs` - AR 큐브 필터링 (공공데이터)
- `Assets/Scripts/Editor/WoopangSceneSetupHelper.cs` - 자동 설정 스크립트
