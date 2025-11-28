# 필터 시스템 업데이트 요약

## 📅 업데이트 날짜
2025-11-28

## 🎯 사용자 요청사항
1. **토글 크기**: 현재의 2배 이상 증가 (30x30 → 60x60) ✅
2. **위치**: 왼쪽 상단 정렬, 간격 축소 ✅
3. **모든 토글 추가**: 애견동반, 공공데이터, 지하철, 버스, 주류, 우팡데이터 (6개) ⚠️
4. **컨트롤 버튼**: 전체 선택, 전체 해제 버튼 추가 ⚠️
5. **동작 변경**: 길게 누르기 = 단독 활성화, 일반 클릭 = ON/OFF 토글 ✅
6. **버그 수정**: 토글 재활성화 시 AR 오브젝트 복원 🔍

## ✅ 완료된 작업

### 1. FilterManager.cs 완전 재작성
📁 `Assets/Scripts/UI/FilterManager.cs`

#### 주요 변경사항
- **Long Press 구현**: `LongPressHandler` 컴포넌트 추가
  - 0.8초 이상 누르면 해당 토글만 활성화, 나머지 비활성화
  - `IPointerDownHandler`, `IPointerUpHandler` 인터페이스 사용

- **Single-Select 모드 제거**
  - 기존: 하나 클릭 시 자동으로 다른 것 비활성화
  - 변경: 일반 클릭은 단순 ON/OFF, 길게 누르기만 single-select

- **6개 필터 모두 지원**
  ```csharp
  private bool filterPetFriendly = true;     // 애견동반
  private bool filterPublicData = true;      // 공공데이터
  private bool filterSubway = true;          // 지하철
  private bool filterBus = true;             // 버스
  private bool filterAlcohol = true;         // 주류판매
  private bool filterWoopangData = true;     // 우팡데이터
  ```

- **전체 선택/해제 기능**
  ```csharp
  public void SelectAll()      // 모든 필터 ON
  public void DeselectAll()    // 모든 필터 OFF
  ```

#### Long Press 코드 예시
```csharp
public class LongPressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public float longPressDuration = 0.8f;
    public System.Action onLongPress;

    void Update()
    {
        if (isPressed && !longPressTriggered)
        {
            pressedTime += Time.deltaTime;
            if (pressedTime >= longPressDuration)
            {
                longPressTriggered = true;
                onLongPress?.Invoke();
            }
        }
    }
}
```

### 2. DataManager.cs 디버깅 강화
📁 `Assets/Scripts/Download/DataManager.cs`

#### 추가된 로깅
```csharp
public void ApplyFilters(Dictionary<string, bool> filters)
{
    Debug.Log($"[DataManager] ApplyFilters - woopangData={showWoopangData}, " +
              $"petFriendly={showPetFriendly}, alcohol={showAlcohol}, " +
              $"spawnedObjects 개수={spawnedObjects.Count}");

    // ... 오브젝트별 상태 변경 로그

    if (wasActive != shouldShow)
    {
        Debug.Log($"[DataManager] placeId={placeId} '{place.name}' - " +
                  $"{(wasActive ? "활성" : "비활성")} → {(shouldShow ? "활성" : "비활성")}");
    }

    Debug.Log($"[DataManager] 필터 적용 완료 - 표시: {shownCount}개, 숨김: {hiddenCount}개");
}
```

**목적**: AR 오브젝트 재활성화 버그 추적

### 3. WoopangSceneSetupHelper.cs 업데이트
📁 `Assets/Scripts/Editor/WoopangSceneSetupHelper.cs`

#### 추가 기능
- **프리팹 완성도 체크**: 누락된 토글/버튼 자동 감지
- **경고 메시지**: 미완성 프리팹 사용 시 경고창 표시
- **가이드 참조**: 완성 가이드 문서 안내

```csharp
// 토글 완성도 체크 코드
if (missingToggles > 0)
{
    EditorUtility.DisplayDialog(
        "⚠️ 프리팹 미완성",
        $"FilterButtonPanel 프리팹에 {missingToggles}개의 UI 요소가 누락되어 있습니다:\n\n" +
        missingList.ToString() + "\n" +
        "Assets/Prefabs/FilterButtonPanel_완성가이드.md 파일을 참고하여\n" +
        "Unity Editor에서 수동으로 추가하세요.",
        "확인"
    );
}
```

### 4. FilterButtonPanel.prefab 구조 변경
📁 `Assets/Prefabs/FilterButtonPanel.prefab`

#### 완료된 변경
- ✅ **RectTransform**: 왼쪽 상단 앵커 (AnchorMin: {x:0, y:1}, AnchorMax: {x:0, y:1})
- ✅ **위치**: x:10, y:-10 (왼쪽 상단에서 10픽셀 오프셋)
- ✅ **VerticalLayoutGroup 추가**: Spacing 5, Padding 10
- ✅ **토글 크기**: Background 60x60, Checkmark 50x50 (2배 증가)
- ✅ **폰트 크기**: 18 (가독성 향상)

#### ⚠️ 수동 작업 필요
현재 프리팹에는 **2개 토글만** 포함되어 있습니다:
- ✅ PetFriendlyToggle (애견동반)
- ✅ PublicDataToggle (공공데이터)

**누락된 요소 (Unity Editor에서 수동 추가 필요)**:
- ❌ SubwayToggle (지하철)
- ❌ BusToggle (버스)
- ❌ AlcoholToggle (주류판매)
- ❌ WoopangDataToggle (우팡데이터)
- ❌ SelectAllButton (전체 선택)
- ❌ DeselectAllButton (전체 해제)

## ⚠️ 사용자가 해야 할 작업

### 1단계: 프리팹 완성 (필수)
📖 **가이드 문서**: `Assets/Prefabs/FilterButtonPanel_완성가이드.md`

#### 빠른 요약
1. `Assets/Prefabs/FilterButtonPanel.prefab` 더블클릭
2. **PetFriendlyToggle 복제 (Ctrl+D) x4회**
   - 이름: SubwayToggle, Label: "지하철"
   - 이름: BusToggle, Label: "버스"
   - 이름: AlcoholToggle, Label: "주류판매"
   - 이름: WoopangDataToggle, Label: "우팡데이터"
3. **버튼 2개 생성 (우클릭 > UI > Button)**
   - 이름: SelectAllButton, Text: "전체 선택", 색: 파란색
   - 이름: DeselectAllButton, Text: "전체 해제", 색: 회색
4. **FilterManager 컴포넌트**에서 모든 필드 연결
5. **저장** (상단 Save 버튼)

### 2단계: 씬에 적용
Unity 메뉴:
```
Tools > Woopang > Setup Filter Button Panel
```

또는

```
Tools > Woopang > Setup All (Recommended)
```

### 3단계: AR 재활성화 버그 테스트
1. **Play Mode** 실행
2. **Console 창** 열기 (Ctrl+Shift+C)
3. 테스트 시나리오:
   ```
   1. 우팡데이터 토글 OFF
      → 콘솔: "[DataManager] ... 숨김: N개"
      → AR 오브젝트 사라짐 ✅

   2. 우팡데이터 토글 ON
      → 콘솔: "[DataManager] ... 표시: N개"
      → AR 오브젝트 나타남 ❓ (버그 확인!)
   ```

4. **버그 발생 시**: 콘솔 로그 전체를 복사하여 공유

## 📋 파일 변경 목록

### 수정된 파일
1. ✅ `Assets/Scripts/UI/FilterManager.cs` (완전 재작성)
2. ✅ `Assets/Scripts/Download/DataManager.cs` (로깅 추가)
3. ✅ `Assets/Scripts/Editor/WoopangSceneSetupHelper.cs` (검증 추가)
4. ✅ `Assets/Prefabs/FilterButtonPanel.prefab` (부분 완성)

### 새로 생성된 파일
5. ✅ `Assets/Prefabs/FilterButtonPanel_완성가이드.md` (상세 가이드)
6. ✅ `FILTER_SYSTEM_UPDATE_SUMMARY.md` (본 문서)

## 🔍 디버깅 정보

### 콘솔에서 확인할 로그
```
[FilterManager] Long Press 감지: petFriendly
[FilterManager] 전체 선택
[FilterManager] 필터 적용 - PetFriendly: True, PublicData: True, ...

[DataManager] ApplyFilters - woopangData=True, petFriendly=True, ...
[DataManager] placeId=123 '카페 이름' - 비활성 → 활성
[DataManager] 필터 적용 완료 - 표시: 15개, 숨김: 3개

[TourAPIManager] ApplyFilters 호출 - publicData=True
```

### 버그 증상 체크리스트
- [ ] 토글 OFF 시 AR 오브젝트가 사라지는가?
- [ ] 콘솔에 "[DataManager] ... 숨김: N개" 로그가 표시되는가?
- [ ] 토글 ON 시 AR 오브젝트가 다시 나타나는가?
- [ ] 콘솔에 "[DataManager] ... 표시: N개" 로그가 표시되는가?
- [ ] 로그에 "비활성 → 활성" 전환이 기록되는가?

## 💡 주요 기능 설명

### 일반 클릭 (Normal Click)
```
사용자 행동: 토글 빠르게 클릭
결과: 해당 토글만 ON ↔ OFF 전환
다른 토글: 영향 없음
```

### 길게 누르기 (Long Press, 0.8초 이상)
```
사용자 행동: 토글을 0.8초 이상 누르고 있기
결과: 해당 토글 ON, 나머지 모든 토글 OFF
예: 애견동반만 보고 싶을 때
```

### 전체 선택/해제
```
전체 선택 버튼: 모든 필터 ON (모든 장소 표시)
전체 해제 버튼: 모든 필터 OFF (모든 장소 숨김)
```

## 🐛 알려진 이슈

### 1. AR 오브젝트 재활성화 버그 (조사 중)
**증상**: 토글을 OFF → ON으로 변경했을 때 AR 오브젝트가 다시 나타나지 않음

**상태**: 🔍 디버깅 로그 추가 완료, 테스트 필요

**추적 방법**:
- DataManager.ApplyFilters() 메서드에 상세 로그 추가
- GameObject.SetActive(true) 호출 확인
- 상태 전환 추적 (비활성 → 활성)

**다음 단계**: 사용자가 Play Mode에서 테스트 후 콘솔 로그 확인

### 2. 프리팹 미완성
**증상**: FilterButtonPanel.prefab에 6개 중 4개 토글 누락

**상태**: ⚠️ 사용자 수동 작업 필요

**해결 방법**: `FilterButtonPanel_완성가이드.md` 참조

## 📚 참고 문서

1. **Unity UI Toggle 문서**: https://docs.unity3d.com/Manual/script-Toggle.html
2. **Unity VerticalLayoutGroup**: https://docs.unity3d.com/Manual/script-VerticalLayoutGroup.html
3. **Unity Event Interfaces**: `IPointerDownHandler`, `IPointerUpHandler`
4. **PlayerPrefs 영속성**: 필터 설정 자동 저장

## ✨ 추가 개선 가능 사항 (향후)

1. **시각적 피드백**
   - Long Press 진행도 표시 (원형 프로그레스 바)
   - 토글 전환 애니메이션 (Fade, Scale)

2. **터치 영역 확대**
   - 현재: 60x60 체크박스만 터치 가능
   - 개선: 라벨까지 포함한 전체 영역 터치 가능

3. **필터 조합 프리셋**
   - "애견카페만", "관광지만", "전통시장만" 등 사전 설정
   - 사용자 커스텀 프리셋 저장/불러오기

4. **필터 카운트 표시**
   - 각 토글 옆에 해당하는 장소 개수 표시
   - 예: "애견동반 (15)"

## 🎓 코드 아키텍처

```
FilterButtonPanel (프리팹)
├─ FilterManager.cs (메인 컨트롤러)
│  ├─ 토글 상태 관리 (6개 bool 변수)
│  ├─ LongPressHandler 동적 추가
│  ├─ PlayerPrefs 저장/로드
│  └─ 3개 매니저에 필터 적용
├─ PetFriendlyToggle
│  └─ LongPressHandler 컴포넌트 (런타임 추가)
├─ PublicDataToggle
│  └─ LongPressHandler 컴포넌트
├─ ... (4개 더 추가 필요)
├─ SelectAllButton
└─ DeselectAllButton

필터 적용 흐름:
FilterManager.ApplyAllFilters()
  ├─ PlaceListManager.ApplyFilters()  // UI 리스트 필터링
  ├─ DataManager.ApplyFilters()       // 우팡 AR 큐브 필터링
  └─ TourAPIManager.ApplyFilters()    // 공공 AR 큐브 필터링
```

## 🔗 관련 Unity 씬
- **WP_1119.unity**: 메인 AR 씬
- **Canvas > ListPanel > FilterButtonPanel**: 필터 패널 위치

## 📞 문의사항
이 업데이트에 대한 질문이나 버그 리포트는 FilterButtonPanel_완성가이드.md 하단의 체크리스트를 작성하여 공유해주세요.
