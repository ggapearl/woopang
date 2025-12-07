# 해결 방법 가이드

## 1. SystemUIManager 깜빡임 해결 ✅

### 변경 사항
- **디버그 로그 비활성화**: `showDebugInfo = false`로 설정
- **이벤트 기반 대기**: 고정 딜레이 대신 SplashImagePlayer 오브젝트가 실제로 파괴될 때까지 대기
- **불필요한 로그 제거**: 모든 Debug.Log 제거

### 작동 방식
```csharp
IEnumerator DelayedCanvasAdjustment()
{
    // 0.1초마다 스플래시 플레이어가 존재하는지 확인
    // 스플래시가 완전히 파괴되면 Canvas 조정 시작
    // 최대 5초 대기 (안전장치)
}
```

이제 스플래시가 **실제로 사라진 후**에만 Canvas 조정이 실행되므로 깜빡임이 없어집니다.

---

## 2. Skeleton Loader 설정 방법 📋

### Inspector 연결 가이드

PlaceListManager 오브젝트의 **Skeleton Loader** 필드에 연결할 것:

1. **Hierarchy**에서 `PlaceListManager` 오브젝트 선택
2. **Inspector**에서 `PlaceListSkeletonLoader` 컴포넌트 찾기
3. 다음 필드들을 연결:

#### PlaceListSkeletonLoader 컴포넌트:
- **Content Parent**: `ListPanel/Scroll View/Viewport/Content` 오브젝트
- **List Text**: `ListPanel/Scroll View/Viewport/Content/ListText` 오브젝트

#### PlaceListManager 컴포넌트:
- **Skeleton Loader**: PlaceListManager 자신 (같은 오브젝트에 있는 PlaceListSkeletonLoader 컴포넌트가 자동으로 찾아짐)

### 작동 방식
```
데이터 로딩 시작
→ ShowSkeletonLoader() 호출
→ 회색 둥근 박스 5개 표시 (shimmer 효과)
→ 데이터 로딩 완료
→ HideSkeletonAndShowText() 호출
→ 스켈레톤 페이드아웃 + 텍스트 페이드인
```

---

## 3. 스크롤 민감도 개선 🖱️

### 현재 상황
- `SmoothScrollRect.cs` 스크립트가 이미 존재함
- ListPanel에 해당 컴포넌트가 추가되어 있는지 확인 필요

### 확인 사항
1. **ListPanel** 오브젝트에 `SmoothScrollRect` 컴포넌트가 있는지 확인
2. 없다면 추가: `Add Component → Smooth Scroll Rect`
3. Inspector 설정:
   - **Scroll Sensitivity**: `2.5` ~ `4.0` (높을수록 민감)
   - **Inertia Mult**: `1.5` ~ `2.0` (높을수록 부드러운 관성)
   - **Min Drag Distance**: `3` ~ `5` (낮을수록 민감)

### Vertical Layout Group 설정
ListPanel의 `Scroll View/Viewport/Content` 오브젝트:
- **Vertical Layout Group**의 **Spacing**: `10` ~ `20`
- **Padding (Top/Bottom)**: `10` ~ `15`
- **Child Force Expand (Height)**: `OFF` (체크 해제)

### ScrollRect 자체 설정 (Scroll View 오브젝트)
- **Scroll Sensitivity**: `100` ~ `120`
- **Deceleration Rate**: `0.2` ~ `0.3`
- **Inertia**: `ON` (체크)
- **Elasticity**: `0.1`

---

## 4. 빠른 업데이트 주기 ✅ (이미 적용됨)

PlaceListManager.cs에 이미 적용된 기능:
- **처음 10초간**: 1초마다 업데이트
- **이후**: 10초마다 업데이트
- **포그라운드 복귀 시**: 빠른 업데이트 주기 재시작

---

## 5. 리스트 카운트 ✅ (이미 수정됨)

PlaceListManager.cs에 이미 수정된 내용:
- 전체 서버 데이터 개수가 아닌 **현재 화면에 표시된 데이터만 카운트**
- 필터링/거리 제한 적용 후의 실제 표시 개수

---

## 테스트 체크리스트 ✓

### SystemUIManager
- [ ] 앱 시작 시 깜빡임 없음
- [ ] 포그라운드 복귀 시 깜빡임 없음
- [ ] 디버그 로그가 콘솔에 표시되지 않음

### Skeleton Loader
- [ ] 리스트 업데이트 시 회색 박스가 표시됨
- [ ] Shimmer 효과가 작동함
- [ ] 데이터 로드 후 부드럽게 페이드아웃됨

### 스크롤 민감도
- [ ] 가볍게 스와이프해도 스크롤이 잘 됨
- [ ] 관성이 부드럽게 작동함
- [ ] 바운스 효과가 과하지 않음

### 업데이트 주기
- [ ] 앱 시작 후 10초간 1초마다 업데이트됨
- [ ] 10초 후에는 10초마다 업데이트됨
- [ ] 백그라운드 복귀 시 빠른 업데이트가 다시 시작됨

### 리스트 카운트
- [ ] 필터 적용 시 카운트가 정확함
- [ ] 거리 제한 적용 시 카운트가 정확함
- [ ] 전체 서버 데이터가 아닌 표시된 데이터 개수만 표시됨

---

## 추가 권장 사항

### SplashImagePlayer_V2 사용 고려
현재 `SplashImagePlayer.cs`를 사용 중이지만, 더 개선된 `SplashImagePlayer_V2.cs`가 준비되어 있습니다:

**장점:**
- Cover/Fit 모드 지원
- 더 간단한 구조
- 깜빡임 완전 제거 보장

**사용 방법:**
1. SplashImagePlayer 컴포넌트 비활성화
2. SplashImagePlayer_V2 컴포넌트 추가
3. Splash Sprite 또는 Splash Texture 할당
4. Fill Mode를 `Cover` (화면 꽉 채움) 또는 `Fit` (전체 보임)으로 선택

---

## 문제 발생 시 확인 사항

### 여전히 깜빡임이 있다면:
1. SystemUIManager의 `showDebugInfo`가 `false`인지 확인
2. 콘솔에 "Canvas 조정 완료" 로그가 언제 나타나는지 확인
3. 스플래시 시간을 더 길게 설정 (displayDuration = 4초)

### 스크롤이 여전히 느리다면:
1. SmoothScrollRect 컴포넌트가 추가되었는지 확인
2. Scroll Sensitivity를 `4.0`까지 올려보기
3. Min Drag Distance를 `1.0`까지 낮춰보기

### Skeleton Loader가 표시되지 않는다면:
1. PlaceListSkeletonLoader 컴포넌트의 Content Parent와 List Text가 연결되었는지 확인
2. Skeleton Count가 0이 아닌지 확인 (기본값: 5)
3. Skeleton Color의 Alpha가 0이 아닌지 확인
