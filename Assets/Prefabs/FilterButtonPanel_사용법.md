# FilterButtonPanel 사용 가이드

## 📦 구성 요소

### 1. FilterButtonPanel (메인 패널)
- **FilterManager**: 필터 로직 관리
- **PetFriendlyToggle**: 애견동반 필터
- **PublicDataToggle**: 공공데이터 필터

### 2. 각 Toggle 구조
```
PetFriendlyToggle
├── [Toggle] - Unity 기본 Toggle 컴포넌트
├── [ToggleImageController] - 이미지 자동 전환
├── Background - 체크박스 배경 이미지
├── Checkmark - 체크마크 이미지
└── Label - "애견동반" 텍스트
```

---

## 🎯 Unity Editor에서 설정

### 1️⃣ 씬에 추가
```
1. Hierarchy: Canvas > ListPanel 선택
2. FilterButtonPanel.prefab 드래그 & 드롭
3. FilterButtonPanel 선택 > Inspector
4. FilterManager > Place List Manager: PlaceListManager 드래그
```

### 2️⃣ 이미지 커스터마이징 (2가지 이미지만!)

**PetFriendlyToggle 선택 > ToggleImageController:**
- ✅ **Unchecked Sprite**: 체크 안됨 이미지 드래그
- ✅ **Checked Sprite**: 체크됨 이미지 드래그
- ✅ **Background Image**: 자동 연결 (수정 불필요)

**PublicDataToggle도 동일하게 설정**

---

## 🔧 작동 원리

### 이미지 전환 흐름
```
사용자 토글 클릭
    ↓
Toggle.isOn 값 변경 (true/false)
    ↓
ToggleImageController.UpdateImage(bool isOn) 호출
    ↓
backgroundImage.sprite = isOn ? checkedSprite : uncheckedSprite
    ↓
체크박스 배경 이미지 즉시 변경!
```

### 필터 적용 흐름
```
Toggle.onValueChanged 이벤트 발생
    ↓
FilterManager.OnPetFriendlyToggleChanged(bool isOn)
    ↓
FilterManager.GetActiveFilters() → Dictionary 생성
    ↓
PlaceListManager.ApplyFilters(filters)
    ↓
PlaceListManager.UpdateUI() → 필터링된 장소만 표시
```

---

## 💡 중요 포인트

### ✅ 해결된 문제
**이전**: Toggle의 Graphic 필드에 GameObject만 연결 가능 → 이미지 직접 설정 불가

**현재**: ToggleImageController 컴포넌트 추가
- **Unchecked Sprite**: 체크 안됨 이미지
- **Checked Sprite**: 체크됨 이미지
- 토글 상태에 따라 **자동으로 이미지 전환**

### 🎨 이미지 설정 방법
1. **가장 간단**: ToggleImageController에 2개 이미지만 드래그
2. **수동 설정**: Background와 Checkmark의 Source Image 직접 교체
3. **Sprite Swap**: Toggle Transition을 Sprite Swap으로 변경 후 상태별 스프라이트 설정

---

## 📂 관련 파일

### 스크립트
- `Assets/Scripts/UI/FilterManager.cs` - 필터 로직
- `Assets/Scripts/UI/ToggleImageController.cs` - 이미지 자동 전환
- `Assets/Scripts/Download/PlaceListManager.cs` - 장소 리스트 관리

### 프리팹
- `Assets/Prefabs/FilterButtonPanel.prefab` - 필터 UI 프리팹

### 가이드
- `QUICK_SETUP.md` - 빠른 설정 가이드
- `Assets/README_UI_Setup.md` - 상세 UI 설정 가이드

---

## 🔍 디버깅

### 이미지가 변경되지 않을 때
1. **Console 확인**: `[ToggleImageController] 이미지 변경: 체크됨/체크 안됨` 로그 확인
2. **Background Image 연결 확인**: Inspector > ToggleImageController > Background Image
3. **Sprite 연결 확인**: Unchecked/Checked Sprite가 비어있지 않은지 확인

### 필터가 작동하지 않을 때
1. **FilterManager 연결 확인**: Place List Manager가 올바르게 연결되었는지
2. **Console 확인**: FilterManager 로그 확인
3. **PlaceListManager.ApplyFilters() 확인**: 메서드가 호출되는지 Debug.Log 추가

---

## 🎮 테스트

### Unity Editor에서
1. Play 모드 진입
2. FilterButtonPanel > PetFriendlyToggle 클릭
3. Console에서 로그 확인:
   ```
   [ToggleImageController] PetFriendlyToggle 이미지 변경: 체크됨
   ```
4. Background 이미지가 변경되는지 Scene 뷰에서 확인

### 실제 디바이스에서
1. 빌드 후 실행
2. 왼쪽 하단 필터 패널에서 토글 클릭
3. 체크박스 이미지 변경 확인
4. 장소 리스트 필터링 확인

---

## 🚀 확장 가능성

### 더 많은 필터 추가
```csharp
// FilterManager.cs에 추가
[SerializeField] private Toggle subwayToggle;  // 지하철
[SerializeField] private Toggle busToggle;     // 버스
[SerializeField] private Toggle alcoholToggle; // 주류

// Start()에 리스너 추가
subwayToggle.onValueChanged.AddListener(OnSubwayToggleChanged);
```

### 애니메이션 효과 추가
```csharp
// ToggleImageController.cs > UpdateImage()에 추가
StartCoroutine(AnimateImageChange(isOn));
```

### 커스텀 스타일 적용
- Background 색상 변경
- Label 폰트 변경
- Panel 크기/위치 조정
