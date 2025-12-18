# Sparkle 효과 Target별 1회 재생 + 스프라이트 통합 (2025-12-05)

## 🎯 작업 내용

### 문제 1: Offscreen Indicator Sparkle이 너무 자주 발생

**문제:**
- 화면 안 → 밖 → 안 → 밖으로 이동할 때마다 Sparkle 발생
- 사용자 경험상 혼잡스럽고 방해됨

**해결:**
- Target별로 `hasPlayedSparkle` 플래그 추가
- 처음 화살표 인디케이터 생성 시에만 Sparkle 재생
- Target이 완전히 비활성화되면 플래그 리셋

### 문제 2: circle.png 스프라이트가 제대로 안 나옴

**문제:**
- IndicatorSparkleManager에 circle.png 연결해도 네모박스만 나옴
- 3D 오브젝트는 SparkleEffect에 직접 연결해야 작동

**해결:**
- IndicatorSparkleHelper.sparkleSprite를 모든 Sparkle에서 사용
- 우선순위: instance.sparkleSprite → 로컬 할당 → Resources 로드

---

## 📊 Sparkle 발생 조건

### Offscreen Indicator (화살표)

```
Target 생성 (처음):
├─ 화면 밖에 있음
├─ 화살표 인디케이터 생성
├─ hasPlayedSparkle = false
└─ ✅ Sparkle 재생 (처음만)

화면 안 → 밖 이동 (2번째):
├─ 화살표 인디케이터 다시 생성
├─ hasPlayedSparkle = true
└─ ❌ Sparkle 재생 안 함

Target 완전히 사라짐:
├─ OnDisable() 호출
└─ hasPlayedSparkle = false (리셋)

Target 다시 생성:
├─ 화살표 인디케이터 생성
├─ hasPlayedSparkle = false (리셋됨)
└─ ✅ Sparkle 재생 (다시 처음)
```

### 3D Object (기본프리팹)

```
프리팹 활성화:
├─ OnEnable() 호출
├─ SparkleOnSpawn.PlaySparkle()
└─ ✅ Sparkle 재생 (매번)
```

**차이점:**
- **화살표 UI**: Target별로 1회만 재생 (화면 안팎 이동 시 재생 안 함)
- **3D 오브젝트**: 활성화될 때마다 재생

---

## 🎨 스프라이트 설정 우선순위

### UI Sparkle (화살표 인디케이터)

```
1순위: IndicatorSparkleHelper.instance.sparkleSprite ✅
2순위: PlaySparkleForIndicator(sprite) 매개변수
3순위: Resources.Load<Sprite>("sou/UI/circle")
```

### 3D Object Sparkle (기본프리팹)

```
1순위: IndicatorSparkleHelper.instance.sparkleSprite ✅
2순위: SparkleEffect.sparkleSprite (로컬 할당)
3순위: Resources.Load<Sprite>("sou/UI/circle")
```

**통합 결과:**
- IndicatorSparkleManager에 circle.png 연결 → 모든 Sparkle에 적용 ✅
- 각 오브젝트에 개별 연결 불필요

---

## 🔧 코드 변경 사항

### Target.cs

**추가:**
```csharp
// Sparkle 효과를 한 번만 재생하기 위한 플래그
[HideInInspector] public bool hasPlayedSparkle = false;

private void OnDisable()
{
    if (OffScreenIndicator.TargetStateChanged != null)
    {
        OffScreenIndicator.TargetStateChanged.Invoke(this, false);
    }

    // Target이 완전히 비활성화되면 Sparkle 플래그 리셋
    hasPlayedSparkle = false;
}
```

**효과:**
- Target별로 Sparkle 재생 여부 추적
- Target 완전히 사라지면 리셋 → 다시 생성 시 Sparkle 재생

---

### OffScreenIndicator.cs

**Before:**
```csharp
private Indicator GetIndicator(ref Indicator indicator, IndicatorType type)
{
    bool isNewlyActivated = false;

    if (indicator == null)
    {
        indicator = ArrowObjectPool.current.GetPooledObject();
        indicator.Activate(true);
        isNewlyActivated = true;
    }

    // 새로 활성화되면 항상 Sparkle 재생 ❌
    if (isNewlyActivated && type == IndicatorType.ARROW)
    {
        IndicatorSparkleHelper.PlaySparkleForIndicator(screenPos, type);
    }
}
```

**After:**
```csharp
private Indicator GetIndicator(ref Indicator indicator, IndicatorType type, Target target)
{
    bool isNewlyActivated = false;

    if (indicator == null)
    {
        indicator = ArrowObjectPool.current.GetPooledObject();
        indicator.Activate(true);
        isNewlyActivated = true;
    }

    // Target이 아직 Sparkle을 재생하지 않았으면 재생 ✅
    if (isNewlyActivated && type == IndicatorType.ARROW && !target.hasPlayedSparkle)
    {
        IndicatorSparkleHelper.PlaySparkleForIndicator(screenPos, type);
        target.hasPlayedSparkle = true;
    }
}
```

**효과:**
- Target별로 Sparkle 재생 여부 체크
- 화면 안팎 이동해도 한 번만 재생

---

### IndicatorSparkleHelper.cs

**Before:**
```csharp
// Sprite 설정
if (sprite != null)
{
    sparkleImage.sprite = sprite;
}
else
{
    Sprite circleSprite = Resources.Load<Sprite>("sou/UI/circle");
    sparkleImage.sprite = circleSprite;
}
```

**After:**
```csharp
// Sprite 설정 (우선순위: 1. instance.sparkleSprite, 2. 매개변수 sprite, 3. Resources 로드)
if (instance.sparkleSprite != null)
{
    sparkleImage.sprite = instance.sparkleSprite; // ✅ 인스펙터 설정 우선
}
else if (sprite != null)
{
    sparkleImage.sprite = sprite;
}
else
{
    Sprite circleSprite = Resources.Load<Sprite>("sou/UI/circle");
    sparkleImage.sprite = circleSprite;
}
```

**효과:**
- IndicatorSparkleManager에 연결한 스프라이트 사용
- 네모박스 아닌 실제 circle.png 표시

---

### SparkleEffect.cs

**Before:**
```csharp
// Sprite 자동 로드
if (sparkleSprite == null)
{
    sparkleSprite = Resources.Load<Sprite>("sou/UI/circle");
}
```

**After:**
```csharp
// Sprite 설정 (우선순위: 1. IndicatorSparkleHelper, 2. 로컬 할당, 3. Resources 로드)
var helperInstance = FindObjectOfType<IndicatorSparkleHelper>();
if (helperInstance != null && helperInstance.sparkleSprite != null)
{
    sparkleSprite = helperInstance.sparkleSprite; // ✅ 통합 관리
}

if (sparkleSprite == null)
{
    sparkleSprite = Resources.Load<Sprite>("sou/UI/circle");
}
```

**효과:**
- IndicatorSparkleManager 스프라이트 사용
- 3D 오브젝트에도 동일한 스프라이트 적용

---

## 📋 Unity 설정 방법

### 1. IndicatorSparkleManager 설정

```
Hierarchy:
├─ IndicatorSparkleManager (GameObject)
   └─ IndicatorSparkleHelper (Component)

Inspector:
├─ General Settings
│  ├─ Enable Sparkle: ✅
│  └─ Sparkle Sprite: circle.png 연결 ✅
│
├─ UI Sparkle Settings
│  └─ (크기, 타이밍, 색상 등)
│
└─ 3D Object Sparkle Settings
   └─ (크기, 타이밍, 색상 등)
```

**중요:**
- **Sparkle Sprite**에 circle.png 드래그 앤 드롭
- 이제 화살표 UI + 3D 오브젝트 모두 이 스프라이트 사용

---

### 2. 기본프리팹 설정

**Before (수정 전):**
```
기본프리팹:
├─ SparkleOnSpawn (Component)
│  └─ Play On Enable: ✅
└─ SparkleEffect (자동 생성)
   └─ Sparkle Sprite: circle.png 직접 연결 필요 ❌
```

**After (수정 후):**
```
기본프리팹:
├─ SparkleOnSpawn (Component)
│  └─ Play On Enable: ✅
└─ SparkleEffect (자동 생성)
   └─ Sparkle Sprite: IndicatorSparkleManager에서 자동 참조 ✅
```

**효과:**
- 각 프리팹에 스프라이트 연결 불필요
- IndicatorSparkleManager에서 통합 관리

---

## 🎯 테스트 시나리오

### 시나리오 1: 화살표 Sparkle (처음 생성)

**방법:**
```
1. Unity 재생
2. 장소가 화면 밖에 있음
3. 화살표 인디케이터 생성
4. Sparkle 효과 확인 ✅
```

**결과:**
- 처음 생성 시 Sparkle 재생
- hasPlayedSparkle = true

---

### 시나리오 2: 화살표 Sparkle (화면 안팎 이동)

**방법:**
```
1. 장소를 향해 이동 (화면 안)
2. 화살표 사라짐
3. 다시 돌아서 화면 밖으로 내보냄
4. 화살표 다시 나타남
```

**결과:**
- 화살표 나타남 ✅
- Sparkle 재생 안 함 ✅ (hasPlayedSparkle = true)

---

### 시나리오 3: Target 완전히 사라짐

**방법:**
```
1. 장소 GameObject 비활성화 (Hierarchy에서 체크 해제)
2. OnDisable() 호출
3. hasPlayedSparkle = false (리셋)
4. 다시 활성화
```

**결과:**
- 새로운 Target으로 간주
- 화살표 생성 시 Sparkle 재생 ✅

---

### 시나리오 4: 3D 오브젝트 Sparkle

**방법:**
```
1. Hierarchy에서 기본프리팹 선택
2. 비활성화 → 활성화
3. Sparkle 확인
```

**결과:**
- circle.png 스프라이트로 Sparkle 재생 ✅
- IndicatorSparkleManager 설정값 사용
- 네모박스 아님 ✅

---

## 💡 핵심 요약

### 변경 사항
**파일:**
- `c:\woopang\Assets\Scripts\OffScreenIndicator\Target.cs`
- `c:\woopang\Assets\Scripts\OffScreenIndicator\OffScreenIndicator.cs`
- `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs`
- `c:\woopang\Assets\Scripts\UI\SparkleEffect.cs`

**주요 개선:**
1. **Target별 Sparkle 1회 재생**: hasPlayedSparkle 플래그
2. **스프라이트 통합**: IndicatorSparkleManager에서 모든 스프라이트 관리
3. **사용자 경험 개선**: 화면 안팎 이동 시 Sparkle 없음

### Sparkle 발생 조건
- **화살표 UI**: Target별로 처음 1회만 (화면 안팎 이동 무시)
- **3D 오브젝트**: 활성화될 때마다 재생

### 스프라이트 설정
- **IndicatorSparkleManager**: Sparkle Sprite 필드에 circle.png 연결
- **모든 Sparkle**: 자동으로 이 스프라이트 사용
- **개별 설정 불필요**: 각 오브젝트마다 연결 안 해도 됨

### 리셋 조건
- Target GameObject 완전히 비활성화 (OnDisable)
- 다시 활성화되면 새로운 Target으로 간주
- 처음 화살표 생성 시 다시 Sparkle 재생

---

**작성일:** 2025-12-05
**수정 파일:**
- `c:\woopang\Assets\Scripts\OffScreenIndicator\Target.cs`
- `c:\woopang\Assets\Scripts\OffScreenIndicator\OffScreenIndicator.cs`
- `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs`
- `c:\woopang\Assets\Scripts\UI\SparkleEffect.cs`

**핵심 개선:** Target별 Sparkle 1회 재생 + 스프라이트 통합 관리
