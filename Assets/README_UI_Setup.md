# UI 설정 간단 가이드 (프리팹 사용)

## ✅ 생성된 파일들

### 스크립트:
- `Assets/Scripts/using/PinchZoomController.cs` - 핀치 줌 기능
- `Assets/Scripts/using/ZoomIndicator.cs` - 줌 인디케이터 UI
- `Assets/Scripts/UI/FilterManager.cs` - 필터 관리 (이미 존재)

### 프리팹:
- `Assets/Prefabs/FilterButtonPanel.prefab` - 필터 버튼 패널
- `Assets/Prefabs/ZoomIndicator.prefab` - 줌 인디케이터

## 🚀 빠른 설정 (Unity Editor)

### 1. FilterButtonPanel 추가 (왼쪽 하단 필터 버튼)

1. Hierarchy에서 `Canvas` > `ListPanel` 선택
2. Project 창에서 `Assets/Prefabs/FilterButtonPanel.prefab` 찾기
3. FilterButtonPanel을 ListPanel에 드래그 앤 드롭
4. FilterButtonPanel 선택 후 Inspector에서:
   - `Place List Manager` 필드에 PlaceListManager GameObject 드래그
   - 위치 확인: 왼쪽 하단 (20, 250)
   - 크기: 200x120

**토글 레이블 텍스트 추가 (선택사항):**
- PetFriendlyToggle 하위에 Text 생성: "🐕 애견동반"
- PublicDataToggle 하위에 Text 생성: "🏛️ 공공데이터"

### 2. ZoomIndicator 추가 (우측 상단 줌 표시)

1. Hierarchy에서 `Canvas` 선택
2. Project 창에서 `Assets/Prefabs/ZoomIndicator.prefab` 찾기
3. ZoomIndicator를 Canvas에 드래그 앤 드롭
4. 위치 확인: 우측 상단 (-20, -20)
5. 크기: 150x80

### 3. PinchZoomController 추가 (핀치 줌 기능)

1. Hierarchy에서 `AR Session Origin` > `AR Camera` 선택
2. Inspector에서 `Add Component` 클릭
3. `PinchZoomController` 검색 후 추가
4. Inspector에서 설정:
   - **AR Camera**: AR Camera 자신을 드래그 (또는 자동 설정됨)
   - **Default FOV**: 144
   - **Min FOV**: 60
   - **Max FOV**: 144
   - **Zoom Speed**: 0.5
   - **Zoom Indicator**: Hierarchy에서 ZoomIndicator GameObject를 드래그

완료! 이제 앱을 빌드하고 테스트하세요.

## 🎮 테스트 방법

### 핀치 줌:
- 두 손가락으로 화면을 터치
- 손가락을 벌리면 확대 (멀리 있는 객체 가까이)
- 손가락을 오므리면 축소 (기본 시야각)
- 우측 상단에 줌 레벨 표시 (1.0x ~ 2.4x)
- **에디터 테스트**: 마우스 스크롤로 확대/축소

### 필터 버튼:
- 왼쪽 하단에 필터 패널 표시
- "애견동반" 토글: 애견동반 장소 표시/숨김
- "공공데이터" 토글: TourAPI 데이터 표시/숨김
- 토글 클릭 시 리스트 즉시 업데이트

## ⚙️ 커스터마이징

### 줌 범위 변경:
```
Min FOV: 40 → 더 많이 확대 (최대 3.6배)
Max FOV: 160 → 더 넓은 시야각
```

### 줌 속도 변경:
```
Zoom Speed: 1.0 → 빠른 줌
Zoom Speed: 0.3 → 부드러운 줌
```

### 필터 패널 위치 변경:
- FilterButtonPanel의 Anchored Position 조정
- 예: (20, 250) → 왼쪽 하단에서 위로

### 줌 인디케이터 위치 변경:
- ZoomIndicator의 Anchored Position 조정
- 예: (-20, -20) → 우측 상단
- 예: (20, -20) → 좌측 상단

## 🔧 수동 설정 (필요시)

프리팹이 작동하지 않을 경우, 이전 가이드 파일 참조:
- `Assets/Scenes/Setup_PinchZoom_and_FilterButtons.md`

## 📝 주의사항

1. **FilterManager의 PlaceListManager 연결 필수!**
   - FilterButtonPanel > FilterManager > Place List Manager 필드에 반드시 할당

2. **PinchZoomController의 ZoomIndicator 연결 필수!**
   - AR Camera > PinchZoomController > Zoom Indicator 필드에 반드시 할당

3. **빌드 전 씬 저장!**
   - File > Save Scene (Ctrl+S)

4. **Unity 버전 호환성:**
   - Unity 2020.3 이상 권장
   - URP (Universal Render Pipeline) 필요
