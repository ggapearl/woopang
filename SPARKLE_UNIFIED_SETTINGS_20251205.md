# Sparkle 효과 통합 설정 (2025-12-05)

## 🎯 작업 내용

### Sparkle 효과 설정 통합 ✅

**목적:** IndicatorSparkleManager 하나로 모든 Sparkle 효과 조절

**변경 사항:**
- SparkleEffect.cs → IndicatorSparkleHelper의 설정값 사용
- 화살표 UI Sparkle + 3D 오브젝트 Sparkle 통합 관리
- 하나의 GameObject에서 모든 Sparkle 효과 조절 가능

---

## 📊 Before & After

### Before (분리된 시스템)

```
화살표 UI Sparkle:
├─ IndicatorSparkleHelper (Singleton)
├─ 인스펙터에서 조절 가능 ✅
└─ 10개 설정값

3D 오브젝트 Sparkle:
├─ SparkleEffect (auto-generated)
├─ 하드코딩된 설정값 ❌
└─ 인스펙터에서 조절 불가
```

**문제:**
- 두 시스템이 따로 놀음
- 3D 오브젝트 Sparkle은 코드 수정해야 조절 가능
- 일관성 없음

### After (통합된 시스템) ✅

```
IndicatorSparkleManager GameObject:
└─ IndicatorSparkleHelper 컴포넌트
   ├─ 화살표 UI Sparkle 제어 ✅
   └─ 3D 오브젝트 Sparkle 제어 ✅

모든 Sparkle 효과:
├─ 하나의 인스펙터에서 조절
├─ 동일한 설정값 사용
└─ 일관성 있는 애니메이션
```

**효과:**
- 하나의 GameObject에서 모든 Sparkle 조절
- 설정값 변경 → 모든 Sparkle에 즉시 적용
- 통합 관리로 일관성 유지

---

## 🔧 구현 상세

### 1. IndicatorSparkleHelper.cs 수정

#### SparkleSettings 클래스 추가

```csharp
/// <summary>
/// Sparkle 설정을 담는 클래스
/// SparkleEffect에서 사용
/// </summary>
public class SparkleSettings
{
    public Vector2 sparkleSize;
    public float spawnDelay;
    public float fadeInDuration;
    public float fadeOutDuration;
    public float startScale;
    public float maxScale;
    public Color sparkleColor;

    // 기본값 생성자
    public SparkleSettings()
    {
        sparkleSize = new Vector2(80f, 80f);
        spawnDelay = 0.5f;
        fadeInDuration = 0.3f;
        fadeOutDuration = 1.7f;
        startScale = 0.5f;
        maxScale = 2.0f;
        sparkleColor = new Color(1f, 1f, 1f, 0.8f);
    }
}
```

#### GetSettings() 메서드 추가

```csharp
/// <summary>
/// 현재 설정값을 반환 (3D 오브젝트 Sparkle용)
/// </summary>
public static SparkleSettings GetSettings()
{
    if (instance == null) return null;

    return new SparkleSettings
    {
        sparkleSize = instance.sparkleSize,
        spawnDelay = instance.spawnDelay,
        fadeInDuration = instance.fadeInDuration,
        fadeOutDuration = instance.fadeOutDuration,
        startScale = instance.startScale,
        maxScale = instance.maxScale,
        sparkleColor = instance.sparkleColor
    };
}
```

---

### 2. SparkleEffect.cs 수정

#### Before: 하드코딩된 설정값

```csharp
[Header("Sparkle Settings")]
public float spawnDelay = 0.5f;
public float fadeInDuration = 0.3f;
public float fadeOutDuration = 1.7f;
public float maxScaleMultiplier = 2.0f;
public float startScaleMultiplier = 0.5f;
public Color sparkleColor = new Color(1f, 1f, 1f, 1f);
```

#### After: IndicatorSparkleHelper 설정 사용

```csharp
[Header("Sparkle Settings")]
[Tooltip("반짝임 이미지 (circle.png) - 비워두면 자동 로드")]
public Sprite sparkleSprite;

// 설정값은 IndicatorSparkleHelper에서 가져옴
```

#### SparkleAnimation3D() 수정

```csharp
private IEnumerator SparkleAnimation3D()
{
    isPlaying = true;

    // IndicatorSparkleHelper 설정 가져오기
    var settings = IndicatorSparkleHelper.GetSettings();
    if (settings == null)
    {
        Debug.LogWarning("[SparkleEffect] IndicatorSparkleHelper가 없습니다. 기본값 사용.");
        settings = new IndicatorSparkleHelper.SparkleSettings();
    }

    // 딜레이 (IndicatorSparkleHelper 설정 사용)
    yield return new WaitForSeconds(settings.spawnDelay);

    // Sparkle 오브젝트 생성 (설정 전달)
    CreateSparkleObject(settings);

    // ... (나머지 애니메이션도 settings 사용)
}
```

#### CreateSparkleObject() 수정

```csharp
private void CreateSparkleObject(IndicatorSparkleHelper.SparkleSettings settings)
{
    // Sparkle GameObject 생성
    sparkleObject = new GameObject("Sparkle_Effect");
    sparkleObject.transform.SetParent(effectCanvas.transform, false);

    // Image 컴포넌트 추가
    sparkleImage = sparkleObject.AddComponent<Image>();
    sparkleImage.sprite = sparkleSprite;
    sparkleImage.color = settings.sparkleColor;

    // RectTransform 설정 (IndicatorSparkleHelper 크기 사용)
    sparkleRect = sparkleObject.GetComponent<RectTransform>();
    sparkleRect.sizeDelta = settings.sparkleSize;
}
```

---

## 🎨 Unity 설정 방법

### 1. IndicatorSparkleManager GameObject 생성 (한 번만)

**Hierarchy:**
```
우클릭 → Create Empty
이름: "IndicatorSparkleManager"
```

**컴포넌트 추가:**
```
IndicatorSparkleManager 선택
→ Add Component
→ IndicatorSparkleHelper
```

### 2. 인스펙터에서 설정 조절

**Sparkle Settings (9개):**

| 설정 | 기본값 | 적용 대상 |
|------|--------|-----------|
| **Enable Sparkle** | ✅ true | 화살표 UI + 3D 오브젝트 (전체) |
| **Sparkle Sprite** | null | circle.png (자동 로드됨) |
| **Sparkle Size** | (80, 80) | 화살표 UI + 3D 오브젝트 |
| **Spawn Delay** | 0.5초 | 화살표 UI + 3D 오브젝트 |
| **Fade In Duration** | 0.3초 | 화살표 UI + 3D 오브젝트 |
| **Fade Out Duration** | 1.7초 | 화살표 UI + 3D 오브젝트 |
| **Start Scale** | 0.5 | 화살표 UI + 3D 오브젝트 |
| **Max Scale** | 2.0 | 화살표 UI + 3D 오브젝트 |
| **Sparkle Color** | 흰색 (0.8 alpha) | 화살표 UI + 3D 오브젝트 |

**Filter Settings (1개):**

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Arrow Only** | ✅ true | 화살표 인디케이터만 적용 (박스 제외) |

**주의:** Arrow Only는 화살표 UI에만 적용됩니다. 3D 오브젝트 Sparkle은 항상 재생됩니다.

---

## 📋 테스트 방법

### 에디터에서 테스트

#### 1. 화살표 UI Sparkle 테스트

**방법:**
```
1. Unity 재생 버튼 클릭
2. 카메라를 돌려서 장소가 화면 밖으로 나가게 함
3. 화살표 인디케이터가 나타나면서 Sparkle 효과 확인
```

**확인 사항:**
- 0.5초 페이드인 후 화살표 나타남
- 0.5초 딜레이 후 Sparkle 시작
- 설정한 크기/색상/타이밍대로 재생

#### 2. 3D 오브젝트 Sparkle 테스트

**방법:**
```
1. Hierarchy에서 기본프리팹 선택
2. Inspector에서 비활성화 (체크박스 해제)
3. 다시 활성화 (체크박스 선택)
4. Scene View 또는 Game View에서 Sparkle 효과 확인
```

**확인 사항:**
- 활성화 직후 Sparkle 효과 발생
- IndicatorSparkleManager 설정값대로 재생
- 크기/색상/타이밍이 화살표 UI와 동일

#### 3. 설정 변경 테스트

**방법:**
```
1. Unity 재생 중
2. IndicatorSparkleManager → Inspector
3. Sparkle Size를 (150, 150)으로 변경
4. 화살표 UI 또는 3D 오브젝트 Sparkle 발생시킴
5. 더 큰 Sparkle 확인
```

**확인 사항:**
- 런타임 중 설정 변경 가능
- 즉시 적용됨
- 화살표 UI + 3D 오브젝트 모두 동일하게 적용

---

## 🎯 통합된 설정 예시

### 예시 1: Sparkle 완전 비활성화

**설정:**
```
Enable Sparkle: ☐ (체크 해제)
```

**효과:**
- 화살표 UI Sparkle 없음 ✅
- 3D 오브젝트 Sparkle 없음 ✅
- 모든 Sparkle 효과 제거

---

### 예시 2: 빠른 Sparkle (1초)

**설정:**
```
Spawn Delay: 0.2초
Fade In Duration: 0.2초
Fade Out Duration: 0.6초
```

**효과:**
- 화살표 UI: 0.2초 후 빠르게 반짝임
- 3D 오브젝트: 0.2초 후 빠르게 반짝임
- 총 1초 애니메이션 (0.2 + 0.2 + 0.6)

---

### 예시 3: 큰 파란색 Sparkle

**설정:**
```
Sparkle Size: (150, 150)
Max Scale: 3.0
Sparkle Color: 파란색 (RGB: 0.5, 0.8, 1.0, Alpha: 1.0)
```

**효과:**
- 화살표 UI: 큰 파란색 Sparkle
- 3D 오브젝트: 큰 파란색 Sparkle
- 3배까지 스케일 업

---

## 🔧 코드 흐름

### 화살표 UI Sparkle

```
1. OffScreenIndicator.cs에서 화살표 생성
   ↓
2. Indicator.Activate(true) 호출
   ├─ 0.5초 페이드인 (CanvasGroup)
   └─ isFirstActivation 체크
   ↓
3. IndicatorSparkleHelper.PlaySparkleForIndicator()
   ├─ instance.enableSparkle 체크
   ├─ instance.arrowOnly 체크
   └─ instance 설정값 사용하여 Sparkle 재생
   ↓
4. SparkleAnimator.StartAnimation()
   └─ 0.5초 딜레이 → 0.3초 페이드인 → 1.7초 페이드아웃
```

### 3D 오브젝트 Sparkle

```
1. 기본프리팹 GameObject 활성화
   ↓
2. SparkleOnSpawn.OnEnable()
   ├─ playOnEnable 체크 → true
   └─ PlaySparkle() 호출
   ↓
3. SparkleEffect 자동 생성 (없으면)
   ├─ AddComponent<SparkleEffect>()
   └─ circle.png 자동 로드
   ↓
4. SparkleEffect.PlaySparkle3D()
   ├─ IndicatorSparkleHelper.GetSettings() 호출 ✅
   ├─ settings 가져오기 (Singleton)
   └─ settings 사용하여 Sparkle 재생
   ↓
5. SparkleAnimation3D() 코루틴
   └─ settings.spawnDelay → fadeInDuration → fadeOutDuration
```

**핵심 차이:**
- **Before:** SparkleEffect 자체 설정값 사용 (하드코딩)
- **After:** IndicatorSparkleHelper.GetSettings() 사용 (통합)

---

## 📝 체크리스트

### 완료 ✅
- [x] IndicatorSparkleHelper.SparkleSettings 클래스 추가
- [x] IndicatorSparkleHelper.GetSettings() 메서드 추가
- [x] SparkleEffect.SparkleAnimation3D() 수정
- [x] SparkleEffect.CreateSparkleObject() 수정
- [x] 통합 문서 작성

### Unity에서 설정 필요
- [ ] IndicatorSparkleManager GameObject 생성
- [ ] IndicatorSparkleHelper 컴포넌트 추가
- [ ] 인스펙터에서 설정 조정
- [ ] Unity 빌드
- [ ] 디바이스 테스트

---

## 💡 핵심 요약

### 변경 사항
**파일:**
- `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs`
- `c:\woopang\Assets\Scripts\UI\SparkleEffect.cs`

**주요 개선:**
1. SparkleSettings 클래스로 설정값 공유
2. GetSettings() 메서드로 통합 접근
3. 하나의 GameObject에서 모든 Sparkle 조절
4. 화살표 UI + 3D 오브젝트 일관성 유지

### Unity 설정 방법
1. Hierarchy에 "IndicatorSparkleManager" GameObject 생성
2. IndicatorSparkleHelper 컴포넌트 추가
3. 인스펙터에서 9개 설정 조절
4. 화살표 UI + 3D 오브젝트 모두 적용 ✅

### 테스트 방법
- **화살표 UI:** 카메라 돌려서 화면 밖으로 내보냄
- **3D 오브젝트:** Hierarchy에서 비활성화 → 활성화
- **설정 변경:** 런타임 중 인스펙터에서 즉시 변경 가능

### 통합된 설정 항목
- **Sparkle Settings (9개):** 크기, 타이밍, 스케일, 색상 등
- **Filter Settings (1개):** 화살표만/전체 적용 (UI만)

---

**작성일:** 2025-12-05
**수정 파일:**
- `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs`
- `c:\woopang\Assets\Scripts\UI\SparkleEffect.cs`

**핵심 개선:** 하나의 IndicatorSparkleManager로 화살표 UI + 3D 오브젝트 Sparkle 통합 관리
