# ⚡ 빠른 설정 가이드

## ✅ 작동하는 기능

### 1️⃣ FilterButtonPanel (필터 버튼) - 작동함

**Unity Editor에서:**
1. Hierarchy: `Canvas` > `ListPanel` 선택
2. Project: `Assets/Prefabs/FilterButtonPanel.prefab` 드래그
3. **ListPanel에 드롭**
4. FilterButtonPanel 선택 > Inspector > **FilterManager 컴포넌트** 찾기:
   - `Place List Manager` → **PlaceListManager** GameObject 드래그
   - `Data Manager` → **DownloadCube_쾌** GameObject 드래그
   - `Tour API Manager` → **DownloadCube_TourAPI_Petfriendly** GameObject 드래그

**필터링 작동 방식:**
- 토글 OFF → 해당 데이터 숨김 (UI 리스트 + AR 큐브 동시 적용)
- 단일 선택 모드: Inspector에서 `Single Select Mode` 체크 시 하나만 선택 가능
- 설정 저장: 앱 재시작 시 필터 상태 유지 (PlayerPrefs)

---

## ⚠️ 수정 필요한 기능

### 2️⃣ PinchZoomController (핀치 줌) - 작동 안 함

**문제점:**
AR Foundation 카메라는 **디바이스 카메라 FOV를 직접 변경할 수 없습니다.**
- AR 카메라의 FOV는 물리적 카메라 하드웨어에 의해 결정됨
- `Camera.fieldOfView` 변경해도 AR Foundation이 매 프레임 덮어씀
- 현재 코드의 `transform.localScale` 변경은 카메라 뷰에 영향 없음

**해결 방법 (구현 필요):**

| 방법 | 설명 | 장단점 |
|-----|------|-------|
| **디지털 줌** | AR 카메라 → RenderTexture → 중앙 크롭 확대 | 실제 줌 효과, 품질 저하 |
| **오브젝트 스케일** | AR 큐브들의 스케일 조절 | 간단, 실제 줌은 아님 |
| **Post-Processing** | URP Lens Distortion 사용 | 왜곡으로 줌 흉내 |

**현재 상태:**
- `PinchZoomController.cs` 파일 존재
- 씬에 추가되지 않음
- 추가해도 AR 환경에서 작동하지 않음

---

## 📝 체크리스트

### 작동하는 기능:
- [x] FilterButtonPanel 프리팹
- [x] FilterManager (단일/다중 선택, 설정 저장)
- [x] PlaceListManager 필터링 (UI 리스트)
- [x] DataManager.ApplyFilters (우팡 AR 큐브)
- [x] TourAPIManager.ApplyFilters (공공데이터 AR 큐브)

### 구현 필요:
- [ ] 핀치 줌 기능 (AR 환경에 맞게 재구현)
- [ ] ZoomIndicator UI (줌 기능 구현 후 연동)

---

## 📚 파일 구조

```
Assets/Scripts/
├── UI/
│   ├── FilterManager.cs        ✅ 작동
│   └── ZoomIndicator.cs        (줌 구현 후 사용)
├── using/
│   └── PinchZoomController.cs  ❌ AR에서 작동 안 함
└── Download/
    ├── DataManager.cs          ✅ ApplyFilters 추가됨
    ├── TourAPIManager.cs       ✅ ApplyFilters 추가됨
    └── PlaceListManager.cs     ✅ ApplyFilters 있음
```
