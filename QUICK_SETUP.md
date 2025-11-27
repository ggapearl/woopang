# ⚡ 빠른 설정 가이드

## ✅ 작동하는 기능

### 1️⃣ FilterButtonPanel (필터 버튼) - 작동함 ✅

**Unity Editor에서:**
1. Hierarchy: `Canvas` > `ListPanel` 선택
2. Project: `Assets/Prefabs/FilterButtonPanel.prefab` 드래그
3. **ListPanel에 드롭**
4. FilterButtonPanel 선택 > Inspector > **FilterManager 컴포넌트** 찾기:
   - `Place List Manager` → **PlaceListManager** GameObject 드래그
   - `Data Manager` → **DownloadCube_쾌** GameObject 드래그
   - `Tour API Manager` → **DownloadCube_TourAPI_Petfriendly** GameObject 드래그

**필터링 작동 방식:**
- 토글 OFF → 해당 데이터 숨김 (UI 리스트 + AR 큐브 동시 적용) ✅
- 토글 ON → 데이터 다시 표시 (SetActive(true) 처리) ✅ **[2025-11-27 수정완료]**
- 단일 선택 모드: Inspector에서 `Single Select Mode` 체크 시 하나만 선택 가능
- 설정 저장: 앱 재시작 시 필터 상태 유지 (PlayerPrefs)

---

### 2️⃣ AR Zoom 기능 - 🔄 신규 구현 중

**이전 시도 (작동 안함):**
- ❌ **ARObjectZoomController** - 디바이스에서 터치 입력 감지 안됨
- ❌ **PinchZoomController** - AR Foundation이 FOV 덮어씀

**신규 구현 (ARDigitalZoomController):**
- 🆕 **LateUpdate()에서 FOV 강제 조절 방식**
- AR Foundation이 FOV 설정 후 → LateUpdate()에서 다시 조절
- 핀치 제스처 로직 개선 (isPinching 플래그 사용)
- 상세 디버그 로그 추가로 터치 입력 추적 가능
- Canvas UI는 영향받지 않음 (같은 카메라 사용)

**Unity Editor 설정:**
1. Hierarchy: 빈 GameObject 생성 (이름: `ARZoomController`)
2. Inspector: `Add Component` > `ARDigitalZoomController` 추가
3. Inspector 설정:
   - **Default Zoom**: `1.0`
   - **Min Zoom**: `0.5` (축소 - FOV 증가)
   - **Max Zoom**: `3.0` (확대 - FOV 감소)
   - **Zoom Speed**: `0.01`
   - **Smooth Speed**: `5.0` (부드러운 전환)
   - **AR Camera**: `AR Camera` 드래그 (또는 자동 검색)
   - **AR Camera Manager**: `AR Session Origin` 의 `ARCameraManager` 드래그 (또는 자동 검색)
   - **Zoom Indicator Object**: `ZoomIndicator` 드래그 (선택사항)

**테스트 방법:**
1. 디바이스 빌드 후 실행
2. 두 손가락 핀치 제스처 시도
3. LogCat (Android) 또는 Xcode Console (iOS)에서 로그 확인:
   - `[ARDigitalZoomController] 핀치 시작` - 터치 감지됨
   - `[ARDigitalZoomController] Zoom: X.XXx` - 줌 레벨 변경
   - 로그가 없으면 터치 입력 차단 문제 (EventSystem 등)

**향후 대안 (필요시):**
- RenderTexture 기반 디지털 줌 (품질 저하 있지만 확실히 작동)

---

## 📝 체크리스트

### 작동하는 기능:
- [x] FilterButtonPanel 프리팹
- [x] FilterManager (단일/다중 선택, 설정 저장)
- [x] PlaceListManager 필터링 (UI 리스트)
- [x] DataManager.ApplyFilters (우팡 AR 큐브) - **[2025-11-27 토글 ON 버그 수정]**
- [x] TourAPIManager.ApplyFilters (공공데이터 AR 큐브)

### 테스트 필요:
- [ ] **ARDigitalZoomController** - LateUpdate FOV 조절 방식 (신규 구현, 디바이스 테스트 필요)

### 제거/대체된 기능:
- [x] ~~PinchZoomController~~ → AR Foundation이 FOV 덮어써서 작동 안함
- [x] ~~ARObjectZoomController~~ → 터치 입력 감지 안됨, ARDigitalZoomController로 대체

---

## 🔄 Git 설정

### 포함되는 폴더 (Assets):
- `Assets/sou/` - 소스 파일
- `Assets/sound/` - 사운드 파일
- `Assets/Scripts/` - 모든 C# 스크립트
- `Assets/Scenes/` - Unity 씬 파일
- `Assets/Prefab/` & `Assets/Prefabs/` - 프리팹 파일
- `Assets/Menu/` - 메뉴 관련 파일

### 제외되는 폴더:
- `Assets/GeneratedLocalRepo/` (Firebase)
- `Assets/Plugins/` (플러그인)
- 기타 모든 폴더

**다른 컴퓨터에서 pull 시:**
- ✅ 위 폴더들의 내용이 추가/수정됨
- ✅ 기존 파일과 폴더는 유지됨
- ✅ 삭제되는 것 없음 (제외된 폴더들은 그대로 유지)

---

## 📚 파일 구조

```
Assets/Scripts/
├── UI/
│   ├── FilterManager.cs               ✅ 작동 (단일 선택, PlayerPrefs)
│   └── ZoomIndicator.cs               ✅ 작동 (Zoom UI 표시)
├── using/
│   ├── PinchZoomController.cs         ❌ 삭제 예정 (FOV 덮어쓰임)
│   ├── ARObjectZoomController.cs      ❌ 삭제 예정 (터치 입력 안됨)
│   └── ARDigitalZoomController.cs     🆕 신규 (LateUpdate FOV 조절)
└── Download/
    ├── DataManager.cs                 ✅ ApplyFilters 수정완료
    ├── TourAPIManager.cs              ✅ ApplyFilters 작동
    └── PlaceListManager.cs            ✅ ApplyFilters 작동
```

---

## 🎯 AR Zoom 작동 원리

### PinchZoomController (실패):
- ❌ Update()에서 카메라 FOV 변경
- ❌ AR Foundation이 LateUpdate()에서 FOV 덮어씀
- ❌ AR 환경에서 작동 안 함

### ARObjectZoomController (실패):
- ❌ AR 오브젝트 스케일 조절 방식
- ❌ Unity Editor에서는 작동하나 디바이스에서 터치 입력 감지 안됨
- 원인: 핀치 감지 로직 오류 (TouchPhase.Began 조건 문제)

### ARDigitalZoomController (신규 - 테스트 필요):
- ✅ **LateUpdate()에서 FOV 강제 조절**
- ✅ AR Foundation보다 나중에 실행되어 FOV 유지
- ✅ 개선된 핀치 감지 로직 (isPinching 플래그)
- ✅ 상세 디버그 로그로 문제 추적 가능
- ✅ 부드러운 줌 전환 (Lerp 사용)
- ⚠️ AR Foundation이 다시 덮어쓸 가능성 있음 (디바이스 테스트 필요)

---

## 💡 사용 팁

**모델 선택:**
- Claude Code는 기본적으로 Sonnet 4.5 사용
- Opus 사용 가능 (Task tool의 model 파라미터로 지정)
- 간단한 작업: `model: "haiku"` (빠르고 저렴)
- 복잡한 작업: `model: "opus"` (강력하지만 느림)

**예시:**
```python
# Task tool 호출 시
Task(
    subagent_type="general-purpose",
    model="opus",  # 또는 "sonnet", "haiku"
    prompt="복잡한 리팩토링 작업..."
)
```
