# Offscreen Indicator 페이드인 효과 추가 (2025-12-05)

## 🎯 작업 내용

### 1. Indicator 페이드인 효과 구현 ✅

**파일:** `c:\woopang\Assets\Scripts\OffScreenIndicator\Indicator.cs`

**변경 사항:**
- CanvasGroup 컴포넌트 자동 추가
- 처음 활성화될 때만 0.5초 페이드인
- Sparkle 효과와 동시에 재생

---

## 📋 Before & After

### Before (수정 전)

```
Offscreen Indicator 생성:
├─ OffScreenIndicator.cs가 화살표/박스 생성
├─ Indicator.Activate(true) 호출
└─ "띡!" 하고 즉시 나타남 ❌

문제:
- 갑자기 나타나서 부자연스러움
- 사용자가 놀랄 수 있음
```

### After (수정 후)

```
Offscreen Indicator 생성:
├─ OffScreenIndicator.cs가 화살표/박스 생성
├─ Indicator.Activate(true) 호출
├─ CanvasGroup alpha: 0.0 → 1.0 (0.5초 페이드인) ✅
└─ Sparkle 효과 동시 재생 (0.5초 딜레이 후 시작) ✨

효과:
- 부드럽게 나타남
- Sparkle 효과와 함께 시너지
- 사용자 경험 향상
```

---

## 🔧 구현 상세

### Activate() 메서드 수정

**Before:**
```csharp
public void Activate(bool value)
{
    transform.gameObject.SetActive(value);
}
```

**After:**
```csharp
public void Activate(bool value)
{
    transform.gameObject.SetActive(value);

    if (value && isFirstActivation)
    {
        // 처음 활성화될 때만 페이드인
        isFirstActivation = false;
        StartFadeIn();
    }
    else if (!value)
    {
        // 비활성화될 때는 다음 활성화를 위해 플래그 리셋
        isFirstActivation = true;

        // 페이드인 중이었다면 중단
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        // 알파값 리셋
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
}
```

### 페이드인 애니메이션

```csharp
private IEnumerator FadeInCoroutine()
{
    // 시작 알파값 0
    canvasGroup.alpha = 0f;

    float elapsed = 0f;
    while (elapsed < fadeInDuration)  // 0.5초
    {
        elapsed += Time.deltaTime;
        float t = elapsed / fadeInDuration;

        // 페이드인
        canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

        yield return null;
    }

    // 최종 알파값 1
    canvasGroup.alpha = 1f;
    fadeCoroutine = null;
}
```

---

## 🎨 타이밍 다이어그램

### Offscreen Indicator (화살표) 생성 시

```
T=0.0s: Indicator.Activate(true) 호출
        ├─ GameObject.SetActive(true)
        ├─ CanvasGroup.alpha = 0.0 (투명)
        └─ FadeInCoroutine 시작

T=0.0s~0.5s: 페이드인 진행
        ├─ alpha: 0.0 → 1.0
        └─ 부드럽게 나타남

T=0.5s: 페이드인 완료
        └─ alpha = 1.0 (완전 불투명)

동시에 Sparkle 효과:
T=0.0s: IndicatorSparkleHelper.PlaySparkleForIndicator() 호출
T=0.5s: Sparkle 시작 (0.5초 딜레이)
T=0.8s: Sparkle 페이드인 완료 (0.3초)
T=2.5s: Sparkle 페이드아웃 완료 (1.7초)
```

**타이밍 시너지:**
- Indicator 페이드인: 0.0s ~ 0.5s
- Sparkle 시작: 0.5s (Indicator 페이드인 완료 직후!)
- Sparkle 페이드인: 0.5s ~ 0.8s

→ **완벽한 타이밍!** Indicator가 나타난 직후 Sparkle이 반짝임 ✨

---

## 🎯 주요 특징

### 1. isFirstActivation 플래그

**목적:** 처음 활성화될 때만 페이드인

**동작:**
```csharp
// 처음 활성화
isFirstActivation = true
Activate(true) → 페이드인 재생 → isFirstActivation = false

// 이미 활성화된 상태에서 위치만 변경
isFirstActivation = false
Activate(true) → 페이드인 없음 (이미 보이는 상태)

// 비활성화 후 재활성화
Activate(false) → isFirstActivation = true (리셋)
Activate(true) → 페이드인 재생
```

**효과:**
- 새로운 Indicator만 페이드인
- 이미 표시 중인 Indicator는 깜빡임 없음
- 자연스러운 UX

### 2. CanvasGroup 자동 추가

**Awake()에서 자동 추가:**
```csharp
void Awake()
{
    indicatorImage = transform.GetComponent<Image>();
    distanceText = transform.GetComponentInChildren<Text>();

    // CanvasGroup 추가 (페이드인용)
    canvasGroup = GetComponent<CanvasGroup>();
    if (canvasGroup == null)
    {
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
```

**효과:**
- Prefab 수정 불필요
- 런타임에 자동으로 CanvasGroup 추가
- 기존 Indicator도 자동 적용

### 3. 페이드인 중단 처리

**비활성화 시:**
```csharp
if (!value)
{
    // 페이드인 중이었다면 중단
    if (fadeCoroutine != null)
    {
        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }

    // 알파값 리셋
    if (canvasGroup != null)
    {
        canvasGroup.alpha = 0f;
    }
}
```

**효과:**
- 페이드인 도중 비활성화되어도 에러 없음
- 다음 활성화를 위해 깔끔하게 리셋

---

## 📊 사용자 경험 개선

### Before (페이드인 없음)

```
사용자가 카메라를 돌림:
├─ 장소가 화면 밖으로 나감
├─ "띡!" 화살표 인디케이터 즉시 나타남 ❌
└─ 갑작스러운 느낌

문제:
- 부자연스러운 등장
- 사용자가 놀랄 수 있음
- 산만한 느낌
```

### After (페이드인 적용)

```
사용자가 카메라를 돌림:
├─ 장소가 화면 밖으로 나감
├─ 화살표 인디케이터가 부드럽게 나타남 (0.5초 페이드인) ✅
├─ 페이드인 완료 직후 Sparkle 효과 시작 ✨
└─ 자연스럽고 우아한 느낌

효과:
- 부드러운 전환
- Sparkle 효과와 타이밍 시너지
- 고급스러운 UX
```

---

## 🎨 전체 통합 효과

### Offscreen Indicator (화살표) 생성 흐름

```
Step 1: OffScreenIndicator.cs에서 화살표 필요 감지
├─ GetIndicator() 호출
└─ ArrowObjectPool.GetPooledObject()

Step 2: Indicator.Activate(true) 호출
├─ GameObject.SetActive(true)
├─ isFirstActivation 체크 → true
├─ StartFadeIn() 호출
└─ FadeInCoroutine 시작

Step 3: 페이드인 애니메이션 (0.5초)
├─ T=0.0s: alpha = 0.0 (투명)
├─ T=0.25s: alpha = 0.5 (반투명)
└─ T=0.5s: alpha = 1.0 (불투명)

Step 4: OffScreenIndicator.cs에서 Sparkle 호출
├─ IndicatorSparkleHelper.PlaySparkleForIndicator()
└─ 0.5초 딜레이 후 Sparkle 시작

Step 5: Sparkle 애니메이션 (2.0초)
├─ T=0.5s~0.8s: 페이드인 + 스케일 업
└─ T=0.8s~2.5s: 페이드아웃

최종 결과:
✅ 0.0s~0.5s: Indicator 부드럽게 나타남
✨ 0.5s~2.5s: Sparkle 반짝임 효과
→ 완벽한 시너지!
```

---

## 📝 체크리스트

### 완료 ✅
- [x] Indicator.cs에 CanvasGroup 추가
- [x] Activate() 메서드 수정
- [x] FadeInCoroutine 구현 (0.5초)
- [x] isFirstActivation 플래그 추가
- [x] 페이드인 중단 처리
- [x] 알파값 리셋 로직
- [x] circle.png를 Resources 폴더로 복사

### 테스트 필요
- [ ] Unity 빌드
- [ ] 디바이스 설치
- [ ] 화살표 인디케이터 페이드인 확인
- [ ] 박스 인디케이터 페이드인 확인
- [ ] Sparkle 효과와 타이밍 확인

---

## 💡 핵심 요약

### 변경 사항
**파일:** `c:\woopang\Assets\Scripts\OffScreenIndicator\Indicator.cs`

**추가된 기능:**
1. CanvasGroup 자동 추가 (Awake)
2. 처음 활성화 시 0.5초 페이드인
3. isFirstActivation 플래그로 중복 페이드인 방지
4. 비활성화 시 자동 리셋

### 사용자 경험
**Before:** "띡!" 하고 즉시 나타남 (부자연스러움)
**After:** 0.5초 페이드인으로 부드럽게 나타남 ✅

### Sparkle 효과와 시너지
- Indicator 페이드인: 0.0s ~ 0.5s
- Sparkle 시작: 0.5s (페이드인 완료 직후)
- **완벽한 타이밍!** 부드러운 등장 → 반짝임 효과 ✨

---

**작성일:** 2025-12-05
**수정 파일:** `c:\woopang\Assets\Scripts\OffScreenIndicator\Indicator.cs`
**핵심 개선:** 0.5초 페이드인으로 부드러운 등장, Sparkle 효과 시너지
