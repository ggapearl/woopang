# 필터 토글 문제 해결 가이드

## 🔍 발견된 문제점

### 1. LongPressHandler가 Toggle 클릭을 방해
**문제**: LongPressHandler가 Toggle의 일반 클릭 이벤트를 제대로 처리하지 못함
**증상**: Toggle을 클릭해도 상태가 변경되지 않거나 해제가 안됨

**수정 내용** (FilterManager.cs):
- `OnPointerClick()` 메서드 추가
- Long Press 발생 시 Toggle 상태를 이전으로 되돌림
- `eventData.Use()`로 이벤트 소비

### 2. FilterButtonPanel 프리팹 연결 누락
**문제**: FilterManager의 Manager 참조가 연결되지 않음
```
placeListManager: {fileID: 0}  ❌
dataManager: {fileID: 0}       ❌
tourAPIManager: {fileID: 0}    ❌
```

**해결 방법**: Unity Inspector에서 수동으로 연결 필요

### 3. object3DToggle 누락
**문제**: FilterManager.cs에는 `object3DToggle` 필드가 있지만 프리팹에는 없음

## ✅ 수정 완료 사항

### FilterManager.cs 업데이트
```csharp
public class LongPressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    // ... 기존 코드 ...

    public void OnPointerClick(PointerEventData eventData)
    {
        // Long Press가 발생했으면 일반 클릭 무시
        if (longPressTriggered)
        {
            Debug.Log("[LongPressHandler] Long Press로 인해 클릭 무시");

            // Toggle 상태를 이전 상태로 되돌림
            if (cachedToggle != null)
            {
                cachedToggle.isOn = !cachedToggle.isOn;
            }

            longPressTriggered = false;
            eventData.Use(); // 이벤트 소비
        }
        else
        {
            // 일반 클릭은 Toggle이 정상 처리
            longPressTriggered = false;
        }
    }
}
```

## 🔧 Unity에서 수정해야 할 사항

### WP_1129.unity 씬에서:

#### 1. FilterButtonPanel 찾기
- Hierarchy → Canvas → FilterButtonPanel

#### 2. FilterManager 컴포넌트 설정

**Inspector에서 연결:**
```
Filter Toggles: (이미 연결됨) ✅
- Pet Friendly Toggle
- Public Data Toggle
- Subway Toggle
- Bus Toggle
- Alcohol Toggle
- Woopang Data Toggle
- Object3D Toggle (누락 가능성)

Control Buttons: (이미 연결됨) ✅
- Select All Button
- Deselect All Button

References: (연결 필요!) ❌
- Place List Manager: PlaceListManager 드래그
- Data Manager: DownloadCube_쾌 드래그
- Tour API Manager: DownloadCube_TourAPI_Petfriendly 드래그

Long Press Settings:
- Long Press Duration: 0.8 ✅
```

#### 3. Object3D Toggle 확인
FilterButtonPanel에 Object3D Toggle이 있는지 확인
- 없으면: 추가 불필요 (필수 아님)
- 있으면: FilterManager Inspector에 연결

## 📋 테스트 체크리스트

Unity Editor에서 Play 모드로 테스트:

- [ ] FilterButtonPanel이 화면에 보임
- [ ] 각 Toggle 클릭 시 상태 변경 확인
- [ ] Toggle을 클릭했을 때 ON/OFF 제대로 토글되는지
- [ ] Console에서 "[FilterManager] 필터 적용" 로그 확인
- [ ] Long Press (0.8초 누름) 시 해당 필터만 활성화, 나머지 비활성화
- [ ] "전체 선택" 버튼 클릭 시 모든 Toggle ON
- [ ] "전체 해제" 버튼 클릭 시 모든 Toggle OFF
- [ ] AR 오브젝트가 필터에 따라 표시/숨김 되는지

## 🐛 디버깅 팁

### Console 로그 확인
정상 작동 시 표시되는 로그:
```
[LongPressHandler] Press 시작
[LongPressHandler] 일반 클릭 (0.12초)
[FilterManager] 필터 적용 - PetFriendly: True, PublicData: True, Alcohol: True, WoopangData: True
[FilterManager] DataManager.ApplyFilters 호출 - woopangData=True
[FilterManager] TourAPIManager.ApplyFilters 호출 - publicData=True
```

### Toggle이 클릭되지 않을 때
1. **LongPressHandler 확인**
   - FilterButtonPanel의 각 Toggle에 LongPressHandler 컴포넌트 확인
   - 있으면: 정상 (코드에서 자동 추가됨)

2. **EventSystem 확인**
   - Hierarchy에 EventSystem 오브젝트 있는지
   - 없으면: UI → Event System 추가

3. **Canvas Raycaster 확인**
   - Canvas에 Graphic Raycaster 컴포넌트 있는지
   - 없으면: Add Component → Graphic Raycaster

### Toggle이 해제되지 않을 때
1. **isUpdatingToggles 플래그 확인**
   - Console에서 로그 확인
   - "필터 적용" 로그가 중복으로 나타나면 무한 루프 가능성

2. **Manager 연결 확인**
   - FilterManager Inspector에서 3개 Manager 모두 연결되었는지
   - 연결 안 되면 ApplyFilters()가 제대로 작동하지 않음

## 🎯 최종 확인사항

WP_1129.unity 씬에서:

1. **FilterButtonPanel → FilterManager 컴포넌트**
   - Place List Manager 연결 ✅
   - Data Manager 연결 ✅
   - Tour API Manager 연결 ✅

2. **PlaceListManager**
   - List Panel 연결 ✅
   - Distance Slider 연결 (UI 생성 후)
   - Max Display Distance: 144 ✅

3. **코드 수정 완료**
   - FilterManager.cs 수정 ✅
   - LongPressHandler OnPointerClick 추가 ✅

4. **씬 저장**
   - File → Save (⌘S / Ctrl+S) ✅

---

## 수정 날짜
2025-11-29
