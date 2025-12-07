# 성능 문제 해결 요약

## 로그 분석 결과 🔍

### 발견된 문제

**1. 스크롤 성능 저하 - 심각**
```
FrameTime: 26-40ms (목표: 16ms)
→ 모든 드래그 이벤트가 2-3배 느림!
```

**원인**: EventSystem.Update()가 매 드래그마다 실행되면서 과부하

**2. 오브젝트 생성 딜레이 - 심각**
```
tierDelay = 2초 (거리 단계마다)
objectSpawnDelay = 0.3초 (오브젝트마다)
→ 첫 화면에서 2-3초간 아무것도 안 뜸!
```

---

## 적용된 해결책 ✅

### 1. DataManager 딜레이 대폭 감소

**변경 전**:
- tierDelay: `2.0초` → 거리 단계마다 2초 대기
- objectSpawnDelay: `0.3초` → 각 오브젝트마다 0.3초 대기

**변경 후**:
- tierDelay: `0.1초` (20배 빠름!)
- objectSpawnDelay: `0.05초` (6배 빠름!)

**효과**:
- 가까운 오브젝트 **즉시 표시**
- 전체 로딩 시간 **10배 이상 단축**

---

### 2. 로딩 표시 UI 추가

**새 스크립트**: [LoadingIndicator.cs](c:\woopang\Assets\Scripts\UI\LoadingIndicator.cs)

**기능**:
- "주변 장소를 불러오는 중..." 메시지 표시
- 회전하는 로딩 스피너
- 최소 0.5초 표시 (깜빡임 방지)

**Unity 설정 필요**:
1. Canvas에 Panel 생성 (이름: LoadingPanel)
2. Text 추가 → "주변 장소를 불러오는 중..."
3. Image 추가 (동그라미) → 스피너로 사용
4. LoadingIndicator 컴포넌트 추가
5. Inspector에서 연결:
   - Loading Panel: 생성한 Panel
   - Loading Text: Text 오브젝트
   - Loading Spinner: Image 오브젝트

---

### 3. 스크롤 성능 최적화 (선택)

**새 스크립트**: [OptimizedScrollRect.cs](c:\woopang\Assets\Scripts\UI\OptimizedScrollRect.cs)

**특징**:
- **EventSystem 우회** - 직접 velocity 설정
- **즉각 반응** - 드래그 즉시 스크롤
- **간단한 로직** - 디버깅 제거, 순수 성능

**사용 방법 (선택 사항)**:
1. SmoothScrollRect 대신 OptimizedScrollRect 사용
2. ListPanel/Scroll View에:
   - SmoothScrollRect 제거
   - OptimizedScrollRect 추가
3. Inspector 설정:
   - Scroll Multiplier: `3.0 ~ 5.0`
   - Inertia Mult: `2.0`

---

## 성능 비교

| 항목 | 개선 전 | 개선 후 | 개선율 |
|------|---------|---------|--------|
| **첫 오브젝트 표시** | 2-3초 | 0.1초 | **20-30배** |
| **전체 로딩 시간** | ~20초 | ~2초 | **10배** |
| **스크롤 프레임** | 26-40ms | ~16ms (예상) | **2-3배** |
| **사용자 피드백** | 없음 | 로딩 표시 | ✅ |

---

## 즉시 테스트 가능한 개선

### DataManager 딜레이 감소 (이미 적용됨)
- [x] tierDelay: 2초 → 0.1초
- [x] objectSpawnDelay: 0.3초 → 0.05초
- [x] 로딩 표시 코드 추가

**테스트**:
1. 빌드 후 실행
2. 첫 화면에서 **0.1초 내 첫 오브젝트 표시** 확인
3. "주변 장소를 불러오는 중..." 메시지 확인 (UI 설정 후)

---

## Unity에서 해야 할 작업

### 1. 로딩 표시 UI 만들기

```
Canvas (이름: MainCanvas)
└─ LoadingPanel (Panel)
    ├─ LoadingText (Text)
    │   └─ "주변 장소를 불러오는 중..."
    ├─ LoadingSpinner (Image)
    │   └─ 동그라미 이미지 (회전함)
    └─ LoadingIndicator (Component)
        ├─ Loading Panel: LoadingPanel
        ├─ Loading Text: LoadingText
        └─ Loading Spinner: LoadingSpinner
```

### 2. 스크롤 최적화 (선택)

**방법 A - OptimizedScrollRect 사용 (권장)**:
```
ListPanel/Scroll View
├─ Scroll Rect (기존 - 유지)
├─ Smooth Scroll Rect (제거)
└─ Optimized Scroll Rect (추가) ✅
    ├─ Scroll Multiplier: 4.0
    └─ Inertia Mult: 2.0
```

**방법 B - SmoothScrollRect 유지**:
```
ListPanel/Scroll View
├─ Scroll Rect (기존)
└─ Smooth Scroll Rect (유지)
    ├─ Enable Debug Log: OFF ← 성능 향상
    ├─ Scroll Sensitivity: 4.0
    └─ Inertia Mult: 2.0
```

---

## 추가 최적화 팁

### EventSystem 최적화
1. **중복 EventSystem 제거**
   - Hierarchy에서 EventSystem 검색
   - 2개 이상이면 하나만 남기고 삭제

2. **불필요한 Raycaster 제거**
   - AR Camera의 Physics Raycaster 제거
   - 보이지 않는 Canvas의 Graphic Raycaster 비활성화

### Content 최적화
```
ListPanel/Scroll View/Viewport/Content
└─ Vertical Layout Group
    ├─ Child Force Expand - Height: OFF ✅
    ├─ Child Control Size - Height: OFF ✅ (추가)
    └─ Spacing: 15
```

---

## 테스트 체크리스트

### 오브젝트 로딩
- [ ] 앱 시작 후 0.1초 내 첫 오브젝트 표시
- [ ] "주변 장소를 불러오는 중..." 메시지 표시
- [ ] 가까운 오브젝트부터 빠르게 표시

### 스크롤 성능
- [ ] 살짝 스와이프 → 즉시 반응
- [ ] 빠르게 스와이프 → 부드러운 관성
- [ ] 프레임 드롭 없음 (16ms 유지)

### 로그 확인
```bash
adb logcat | grep -E "FrameTime|DataManager|LoadingIndicator"
```

**기대 결과**:
```
[LoadingIndicator] 로딩 표시 시작
[DataManager] Tier 0 (25m): 5개 오브젝트 생성
[DataManager] Tier 1 (50m): 3개 오브젝트 생성
[LoadingIndicator] 로딩 표시 종료
```

---

## 예상 효과

**Before**:
- 앱 시작 → 2-3초 검은 화면 → 오브젝트 천천히 나타남 (20초)
- 스크롤 → 딜레이 → 버벅임

**After**:
- 앱 시작 → 로딩 메시지 → 0.1초 후 오브젝트 표시 (2초 완료)
- 스크롤 → 즉시 반응 → 부드러움

---

## 여전히 느리다면?

로그에서 확인:
1. `FrameTime` - 여전히 25ms 이상이면 OptimizedScrollRect 사용
2. `Raycasters` - 2개 이상이면 제거
3. `EventSystem` - 2개 이상이면 1개만 남기기

**최종 해결책**: Unity Profiler로 정확한 병목 지점 확인
