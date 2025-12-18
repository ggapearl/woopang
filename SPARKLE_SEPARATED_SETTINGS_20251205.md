# Sparkle 효과 설정 분리 (2025-12-05)

## 🎯 작업 내용

### Sparkle 효과 UI/3D 오브젝트 분리 ✅

**문제:**
- Offscreen Indicator (화살표 UI)와 3D 오브젝트는 크기가 다름
- 하나의 설정으로 둘 다 적절하게 조절 불가능
- 각각 다른 설정이 필요함

**해결:**
- IndicatorSparkleHelper에 **UI 설정**과 **3D 오브젝트 설정** 분리
- 하나의 GameObject에서 모두 관리하지만 설정은 독립적
- 각각의 크기/타이밍/색상을 개별 조절 가능

---

## 📊 설정 구조

### IndicatorSparkleManager (하나의 GameObject)

```
IndicatorSparkleHelper 컴포넌트:
├─ General Settings (공통)
│  ├─ Enable Sparkle (전체 활성화/비활성화)
│  └─ Sparkle Sprite (circle.png)
│
├─ UI Sparkle Settings (화살표 인디케이터용)
│  ├─ Arrow Only (박스 제외)
│  ├─ UI Sparkle Size (80x80)
│  ├─ UI Spawn Delay (0.5초)
│  ├─ UI Fade In Duration (0.3초)
│  ├─ UI Fade Out Duration (1.7초)
│  ├─ UI Start Scale (0.5)
│  ├─ UI Max Scale (2.0)
│  └─ UI Sparkle Color (흰색)
│
└─ 3D Object Sparkle Settings (기본프리팹용)
   ├─ Object Sparkle Size (120x120)
   ├─ Object Spawn Delay (0.3초)
   ├─ Object Fade In Duration (0.4초)
   ├─ Object Fade Out Duration (1.5초)
   ├─ Object Start Scale (0.3)
   ├─ Object Max Scale (2.5)
   └─ Object Sparkle Color (연한 노란색)
```

---

## 🔧 Unity 인스펙터 설정

### General Settings (2개)

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Enable Sparkle** | ✅ true | 모든 Sparkle 효과 활성화/비활성화 |
| **Sparkle Sprite** | null | circle.png (비워두면 자동 로드) |

### UI Sparkle Settings (8개) - Offscreen Indicator Arrow

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Arrow Only** | ✅ true | 화살표만 적용 (박스 제외) |
| **UI Sparkle Size** | (80, 80) | 화살표 UI Sparkle 크기 (픽셀) |
| **UI Spawn Delay** | 0.5초 | 화살표 생성 후 Sparkle 시작 딜레이 |
| **UI Fade In Duration** | 0.3초 | 화살표 Sparkle 페이드인 시간 |
| **UI Fade Out Duration** | 1.7초 | 화살표 Sparkle 페이드아웃 시간 |
| **UI Start Scale** | 0.5 | 화살표 Sparkle 시작 스케일 |
| **UI Max Scale** | 2.0 | 화살표 Sparkle 최대 스케일 |
| **UI Sparkle Color** | 흰색 (0.8 alpha) | 화살표 Sparkle 색상 |

### 3D Object Sparkle Settings (7개) - Sample_Prefab, GLB_Prefab

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Object Sparkle Size** | (120, 120) | 3D 오브젝트 Sparkle 크기 (픽셀) |
| **Object Spawn Delay** | 0.3초 | 오브젝트 생성 후 Sparkle 시작 딜레이 |
| **Object Fade In Duration** | 0.4초 | 오브젝트 Sparkle 페이드인 시간 |
| **Object Fade Out Duration** | 1.5초 | 오브젝트 Sparkle 페이드아웃 시간 |
| **Object Start Scale** | 0.3 | 오브젝트 Sparkle 시작 스케일 |
| **Object Max Scale** | 2.5 | 오브젝트 Sparkle 최대 스케일 |
| **Object Sparkle Color** | 연한 노란색 (1.0 alpha) | 오브젝트 Sparkle 색상 |

**총 17개 설정 (General 2 + UI 8 + 3D Object 7)**

---

## 🎨 기본값 차이점

### UI Sparkle (Offscreen Indicator)
```
크기: 80x80 (작음)
딜레이: 0.5초 (Indicator 페이드인과 동시)
페이드인: 0.3초
페이드아웃: 1.7초
스케일: 0.5 → 2.0 (2배)
색상: 흰색 (0.8 alpha) - 은은함
→ 총 2.5초 애니메이션
```

### 3D Object Sparkle (Sample_Prefab, GLB_Prefab)
```
크기: 120x120 (큼)
딜레이: 0.3초 (빠르게 시작)
페이드인: 0.4초
페이드아웃: 1.5초
스케일: 0.3 → 2.5 (2.5배)
색상: 연한 노란색 (1.0 alpha) - 강렬함
→ 총 2.2초 애니메이션
```

**차이 이유:**
- UI는 은은하고 부드럽게 (사용자 방향 안내)
- 3D 오브젝트는 크고 강렬하게 (새로운 오브젝트 강조)

---

## 📋 테스트 방법

### 1. UI Sparkle 테스트 (화살표 인디케이터)

**방법:**
```
1. Unity 재생
2. 카메라 돌려서 장소가 화면 밖으로 나가게 함
3. 화살표 인디케이터 + Sparkle 확인
```

**확인 사항:**
- 화살표 0.5초 페이드인
- 0.5초 후 작은 흰색 Sparkle 시작
- 2배 스케일까지 커지면서 페이드아웃

**설정 변경 예시:**
```
UI Sparkle Size: (150, 150) → 더 큰 Sparkle
UI Sparkle Color: 파란색 → 파란색 Sparkle
```

### 2. 3D Object Sparkle 테스트 (기본프리팹)

**방법:**
```
1. Hierarchy에서 기본프리팹 선택
2. Inspector에서 비활성화 → 활성화
3. Scene View 또는 Game View에서 Sparkle 확인
```

**확인 사항:**
- 활성화 직후 0.3초 딜레이
- 큰 연한 노란색 Sparkle 시작
- 2.5배 스케일까지 커지면서 페이드아웃

**설정 변경 예시:**
```
Object Sparkle Size: (200, 200) → 더 큰 Sparkle
Object Max Scale: 3.5 → 3.5배까지 스케일 업
Object Sparkle Color: 분홍색 → 분홍색 Sparkle
```

### 3. 전체 비활성화 테스트

**방법:**
```
1. IndicatorSparkleManager 선택
2. Enable Sparkle 체크 해제
3. 화살표 UI + 3D 오브젝트 확인
```

**확인 사항:**
- 화살표 Sparkle 없음 ✅
- 3D 오브젝트 Sparkle 없음 ✅
- 모든 Sparkle 효과 제거

---

## 💡 설정 예시

### 예시 1: UI는 유지, 3D 오브젝트만 크게

**설정:**
```
[UI Sparkle Settings]
UI Sparkle Size: (80, 80) - 기본값 유지
UI Sparkle Color: 흰색 - 기본값 유지

[3D Object Sparkle Settings]
Object Sparkle Size: (200, 200) - 크게
Object Max Scale: 3.5 - 더 크게
Object Sparkle Color: 금색 (RGB: 1.0, 0.9, 0.3, Alpha: 1.0)
```

**효과:**
- 화살표 UI: 작고 은은한 흰색 Sparkle (기본)
- 3D 오브젝트: 크고 강렬한 금색 Sparkle

---

### 예시 2: UI는 파란색, 3D는 빨간색

**설정:**
```
[UI Sparkle Settings]
UI Sparkle Color: 파란색 (RGB: 0.3, 0.6, 1.0, Alpha: 0.8)

[3D Object Sparkle Settings]
Object Sparkle Color: 빨간색 (RGB: 1.0, 0.3, 0.3, Alpha: 1.0)
```

**효과:**
- 화살표 UI: 파란색 Sparkle
- 3D 오브젝트: 빨간색 Sparkle
- 색상으로 구분 가능

---

### 예시 3: UI는 빠르게, 3D는 느리게

**설정:**
```
[UI Sparkle Settings]
UI Spawn Delay: 0.2초
UI Fade In Duration: 0.2초
UI Fade Out Duration: 0.6초
→ 총 1.0초

[3D Object Sparkle Settings]
Object Spawn Delay: 0.8초
Object Fade In Duration: 0.8초
Object Fade Out Duration: 3.0초
→ 총 4.6초
```

**효과:**
- 화살표 UI: 빠르게 반짝이고 사라짐
- 3D 오브젝트: 천천히 우아하게 나타남

---

## 🔧 코드 구조

### UI Sparkle 흐름

```
1. OffScreenIndicator.cs에서 화살표 생성
   ↓
2. Indicator.Activate(true)
   ├─ 0.5초 페이드인 (CanvasGroup)
   └─ isFirstActivation 체크
   ↓
3. IndicatorSparkleHelper.PlaySparkleForIndicator()
   ├─ instance.enableSparkle 체크
   ├─ instance.arrowOnly 체크
   └─ instance.uiSparkleSize, uiSpawnDelay 등 사용 ✅
   ↓
4. SparkleAnimator.StartAnimation()
   └─ UI 설정값으로 애니메이션 재생
```

### 3D Object Sparkle 흐름

```
1. 기본프리팹 GameObject 활성화
   ↓
2. SparkleOnSpawn.OnEnable()
   └─ PlaySparkle() 호출
   ↓
3. SparkleEffect 자동 생성
   └─ AddComponent<SparkleEffect>()
   ↓
4. SparkleEffect.PlaySparkle3D()
   ├─ IndicatorSparkleHelper.GetSettings() 호출 ✅
   └─ objectSparkleSize, objectSpawnDelay 등 가져옴
   ↓
5. SparkleAnimation3D() 코루틴
   └─ 3D 오브젝트 설정값으로 애니메이션 재생
```

---

## 📝 체크리스트

### 완료 ✅
- [x] IndicatorSparkleHelper에 UI/3D 설정 분리
- [x] UI 설정: uiSparkleSize, uiSpawnDelay 등 (8개)
- [x] 3D 오브젝트 설정: objectSparkleSize, objectSpawnDelay 등 (7개)
- [x] GetSettings() 메서드 → 3D 오브젝트 설정 반환
- [x] PlaySparkleForIndicator() → UI 설정 사용
- [x] SparkleEffect.SparkleAnimation3D() → 3D 설정 사용
- [x] SparkleAnimationUI() 제거 (사용 안 함)
- [x] 컴파일 에러 수정
- [x] 문서 작성

### Unity에서 설정 필요
- [ ] IndicatorSparkleManager GameObject 생성 (이미 있으면 그대로)
- [ ] 인스펙터에서 17개 설정 조정
- [ ] UI Sparkle 테스트 (화살표)
- [ ] 3D Object Sparkle 테스트 (기본프리팹)
- [ ] Unity 빌드
- [ ] 디바이스 테스트

---

## 💡 핵심 요약

### 변경 사항
**파일:**
- `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs`
- `c:\woopang\Assets\Scripts\UI\SparkleEffect.cs`

**주요 개선:**
1. **설정 분리**: UI용 8개 + 3D 오브젝트용 7개
2. **독립적 조절**: 각각 크기/타이밍/색상 개별 설정
3. **하나의 GameObject**: IndicatorSparkleManager에서 모두 관리
4. **기본값 최적화**: UI는 은은하게, 3D는 강렬하게

### Unity 설정 방법
1. Hierarchy에 "IndicatorSparkleManager" GameObject 생성
2. IndicatorSparkleHelper 컴포넌트 추가
3. 인스펙터에서 17개 설정 조절
   - General Settings (2개)
   - UI Sparkle Settings (8개)
   - 3D Object Sparkle Settings (7개)

### 테스트 방법
- **UI Sparkle:** 카메라 돌려서 화면 밖으로 내보냄
- **3D Object Sparkle:** Hierarchy에서 비활성화 → 활성화
- **설정 변경:** 런타임 중 인스펙터에서 즉시 변경 가능

### 기본값 차이
- **UI (화살표):** 작고 은은한 흰색 (80x80, 2.5초)
- **3D 오브젝트:** 크고 강렬한 노란색 (120x120, 2.2초)

---

**작성일:** 2025-12-05
**수정 파일:**
- `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs`
- `c:\woopang\Assets\Scripts\UI\SparkleEffect.cs`

**핵심 개선:** UI/3D 오브젝트 Sparkle 설정 완전 분리, 각각 독립적으로 조절 가능
