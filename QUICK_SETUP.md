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

### 2️⃣ ARObjectZoomController (AR 오브젝트 줌) - ⚠️ 디바이스에서 작동 안함

**현재 상태:**
- ❌ **디바이스에서 두 손가락 핀치 제스처 반응 없음**
- ✅ Unity Editor에서 마우스 스크롤은 정상 작동
- 🔍 **원인 조사 필요** (터치 입력 감지 안됨 추정)

**대안 검토 중:**
- 디지털 줌 방식 (RenderTexture 사용)
- AR Camera → RenderTexture → Secondary Camera로 확대
- Canvas UI는 줌 영향 받지 않도록 설정
- 품질 저하 허용 가능

**Unity Editor 설정 (참고용):**
1. Hierarchy: 빈 GameObject 생성 (이름: `ARZoomController`)
2. Inspector: `Add Component` > `ARObjectZoomController` 추가
3. Inspector 설정:
   - **Default Zoom**: `1.0`
   - **Min Zoom**: `0.5` (오브젝트 50% 크기)
   - **Max Zoom**: `3.0` (오브젝트 300% 크기)
   - **Zoom Speed**: `0.01`
   - **Data Manager**: `DownloadCube_쾌` 드래그 (또는 자동 검색)
   - **Tour API Manager**: `DownloadCube_TourAPI_Petfriendly` 드래그 (또는 자동 검색)
   - **Zoom Indicator Object**: `ZoomIndicator` 드래그 (선택사항)

---

## 📝 체크리스트

### 작동하는 기능:
- [x] FilterButtonPanel 프리팹
- [x] FilterManager (단일/다중 선택, 설정 저장)
- [x] PlaceListManager 필터링 (UI 리스트)
- [x] DataManager.ApplyFilters (우팡 AR 큐브) - **[2025-11-27 토글 ON 버그 수정]**
- [x] TourAPIManager.ApplyFilters (공공데이터 AR 큐브)

### 작동 안하는 기능 (수정 필요):
- [ ] ARObjectZoomController - **디바이스에서 터치 입력 감지 안됨 (대안 필요)**

### 제거된 기능:
- [x] ~~PinchZoomController~~ → **ARObjectZoomController로 대체 시도했으나 작동 안함**

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
│   ├── FilterManager.cs        ✅ 작동 (단일 선택, PlayerPrefs)
│   └── ZoomIndicator.cs        ✅ 작동 (ARObjectZoomController와 연동)
├── using/
│   ├── PinchZoomController.cs  ❌ 삭제 예정 (AR에서 작동 안 함)
│   └── ARObjectZoomController.cs ✅ 신규 (오브젝트 스케일 기반)
└── Download/
    ├── DataManager.cs          ✅ ApplyFilters 추가됨
    ├── TourAPIManager.cs       ✅ ApplyFilters 추가됨
    └── PlaceListManager.cs     ✅ ApplyFilters 있음
```

---

## 🎯 AR Zoom 작동 원리

### PinchZoomController (삭제 예정):
- ❌ 카메라 FOV 변경 시도
- ❌ AR Foundation이 매 프레임 FOV 덮어씀
- ❌ AR 환경에서 작동 안 함

### ARObjectZoomController (신규 - 작동 안함):
- ❌ Unity Editor에서는 작동하나 디바이스에서 터치 입력 감지 안됨
- ❓ 원인: 터치 이벤트 처리 문제 또는 설정 누락 추정
- 🔄 대안 필요: 디지털 줌 방식 (RenderTexture) 검토 중

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
