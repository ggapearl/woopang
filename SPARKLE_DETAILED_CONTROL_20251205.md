# Sparkle 효과 세밀한 제어 + 3D 오브젝트 효과 제거 (2025-12-05)

## 🎯 작업 내용

### 1. 3D 오브젝트 Sparkle 효과 완전 제거 ✅

**이유:**
- 사용자는 Offscreen Indicator를 통해 장소를 먼저 확인
- 3D 오브젝트는 이미 발생해있는 경우가 많음
- Sparkle 효과가 의미 없음

**삭제된 파일:**
- `SparkleOnSpawn.cs` - 3D 오브젝트 자동 재생 스크립트
- `SparkleEffect.cs` - 3D 오브젝트 Sparkle 애니메이션

**삭제된 설정:**
- IndicatorSparkleHelper에서 3D Object Sparkle Settings 섹션 제거

---

### 2. UI Sparkle 효과 세밀한 제어 ✅

**문제:**
- 기존: fadeIn, fadeOut 2개 설정만으로 자연스러운 효과 구현 어려움
- 빠르게 확대되다가 서서히 확대 + 자연스러운 페이드아웃 구현 불가

**해결:**
- **5단계 애니메이션** 구현
- 구간별 세밀한 시간 조절 가능
- Ease-out 커브 적용

---

## 📊 새로운 인스펙터 구조

### General Settings (3개)

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Enable Sparkle** | ✅ true | Sparkle 효과 활성화 |
| **Sparkle Sprite** | null | circle.png (비워두면 자동 로드) |
| **Arrow Only** | ✅ true | 화살표만 적용 (박스 제외) |

### Sparkle Size & Timing (2개)

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Sparkle Size** | (80, 80) | Sparkle 크기 (픽셀) |
| **Spawn Delay** | 0.5초 | 생성 후 딜레이 |

### Scale Animation (5개) - 확대 세밀 제어

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Start Scale** | 0.5 | 시작 스케일 배율 |
| **Rapid Expand Scale** | 1.5 | 빠른 확대 구간 최종 스케일 |
| **Rapid Expand Duration** | 0.15초 | 빠른 확대 시간 |
| **Slow Expand Scale** | 2.0 | 느린 확대 구간 최종 스케일 |
| **Slow Expand Duration** | 0.35초 | 느린 확대 시간 (ease-out) |

### Fade Animation (4개) - 페이드 세밀 제어

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Fade In Duration** | 0.2초 | 페이드인 시간 |
| **Full Opacity Duration** | 0.1초 | 최대 불투명도 유지 시간 |
| **Rapid Fade Out Duration** | 0.3초 | 빠른 페이드아웃 (30%까지) |
| **Slow Fade Out Duration** | 0.8초 | 느린 페이드아웃 (완전 사라짐, ease-out) |

### Color (1개)

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Sparkle Color** | 흰색 (0.9 alpha) | 반짝임 색상 |

**총 15개 설정**

---

## 🎨 5단계 애니메이션 흐름

### 타임라인 (기본값 기준)

```
T=0.0s: Indicator 생성 + 0.5초 페이드인 시작
T=0.5s: Sparkle 시작 (Spawn Delay)

[1단계] T=0.5s ~ 0.65s (0.15초) - 빠른 확대 + 페이드인
├─ 스케일: 0.5 → 1.5 (선형)
└─ 불투명도: 0% → 90% (선형)

[2단계] T=0.65s ~ 1.0s (0.35초) - 느린 확대
├─ 스케일: 1.5 → 2.0 (ease-out quadratic)
└─ 불투명도: 90% (유지)

[3단계] T=1.0s ~ 1.1s (0.1초) - 최대 불투명도 유지
├─ 스케일: 2.0 (유지)
└─ 불투명도: 90% (유지)

[4단계] T=1.1s ~ 1.4s (0.3초) - 빠른 페이드아웃
├─ 스케일: 2.0 (유지)
└─ 불투명도: 90% → 27% (선형)

[5단계] T=1.4s ~ 2.2s (0.8초) - 느린 페이드아웃
├─ 스케일: 2.0 (유지)
└─ 불투명도: 27% → 0% (ease-out cubic)

→ 총 1.7초 애니메이션
```

---

## 🔧 애니메이션 커브 적용

### Ease-out Quadratic (느린 확대)

```csharp
float easeT = 1f - Mathf.Pow(1f - t, 2f);
```

**효과:**
- 처음에는 빠르게, 끝으로 갈수록 천천히
- 자연스러운 감속 효과

### Ease-out Cubic (느린 페이드아웃)

```csharp
float easeT = 1f - Mathf.Pow(1f - t, 3f);
```

**효과:**
- 더욱 부드러운 사라짐 효과
- 마지막 순간까지 천천히 사라짐

---

## 💡 설정 조합 예시

### 예시 1: 매우 빠르고 강렬한 Sparkle

```
Spawn Delay: 0.3초
Rapid Expand Duration: 0.1초
Slow Expand Duration: 0.2초
Full Opacity Duration: 0.05초
Rapid Fade Out Duration: 0.2초
Slow Fade Out Duration: 0.4초
→ 총 0.95초
```

**효과:**
- 빠르게 나타나서 빠르게 사라짐
- 강렬한 임팩트

---

### 예시 2: 부드럽고 우아한 Sparkle

```
Spawn Delay: 0.7초
Rapid Expand Duration: 0.2초
Slow Expand Duration: 0.5초
Full Opacity Duration: 0.2초
Rapid Fade Out Duration: 0.5초
Slow Fade Out Duration: 1.2초
→ 총 2.6초
```

**효과:**
- 천천히 나타나서 오래 지속
- 우아하고 부드러운 느낌

---

### 예시 3: 확대 강조 + 빠른 사라짐

```
Rapid Expand Scale: 2.0
Slow Expand Scale: 3.0
Rapid Expand Duration: 0.1초
Slow Expand Duration: 0.4초
Rapid Fade Out Duration: 0.2초
Slow Fade Out Duration: 0.5초
```

**효과:**
- 크게 확대되어 강조
- 빠르게 사라져 깔끔함

---

## 📝 코드 변경 사항

### IndicatorSparkleHelper.cs

**Before:**
```csharp
[Header("UI Sparkle Settings")]
public float uiFadeInDuration = 0.3f;
public float uiFadeOutDuration = 1.7f;
public float uiStartScale = 0.5f;
public float uiMaxScale = 2.0f;

[Header("3D Object Sparkle Settings")]
public float objectFadeInDuration = 0.4f;
// ... (7개 설정)
```

**After:**
```csharp
[Header("Scale Animation")]
public float startScale = 0.5f;
public float rapidExpandScale = 1.5f;
public float rapidExpandDuration = 0.15f;
public float slowExpandScale = 2.0f;
public float slowExpandDuration = 0.35f;

[Header("Fade Animation")]
public float fadeInDuration = 0.2f;
public float fullOpacityDuration = 0.1f;
public float rapidFadeOutDuration = 0.3f;
public float slowFadeOutDuration = 0.8f;
```

---

### SparkleAnimator.cs

**Before:**
```csharp
// 페이드인 + 스케일 업 (단순 선형)
while (elapsed < fadeInDuration)
{
    float t = elapsed / fadeInDuration;
    color.a = Mathf.Lerp(0f, sparkleColor.a, t);
    float scale = Mathf.Lerp(startScale, maxScale, t);
}

// 페이드아웃 (단순 선형)
while (elapsed < fadeOutDuration)
{
    float t = elapsed / fadeOutDuration;
    color.a = Mathf.Lerp(sparkleColor.a, 0f, t);
}
```

**After:**
```csharp
// 1단계: 빠른 확대 + 페이드인
while (elapsed < rapidExpandDuration)
{
    float scale = Mathf.Lerp(startScale, rapidExpandScale, t);
    color.a = Mathf.Lerp(0f, sparkleColor.a, t);
}

// 2단계: 느린 확대 (ease-out)
float easeT = 1f - Mathf.Pow(1f - t, 2f);
float scale = Mathf.Lerp(rapidExpandScale, slowExpandScale, easeT);

// 3단계: 최대 불투명도 유지
yield return new WaitForSeconds(fullOpacityDuration);

// 4단계: 빠른 페이드아웃
color.a = Mathf.Lerp(sparkleColor.a, sparkleColor.a * 0.3f, t);

// 5단계: 느린 페이드아웃 (ease-out)
float easeT = 1f - Mathf.Pow(1f - t, 3f);
color.a = Mathf.Lerp(startAlpha, 0f, easeT);
```

---

## 🎯 사용자 경험 개선

### Before (3D 오브젝트 Sparkle 있음)

```
1. Offscreen Indicator로 장소 확인
2. 장소로 이동
3. 이미 발생해있는 3D 오브젝트 확인
4. 불필요한 Sparkle 효과 재생 ❌
```

### After (3D 오브젝트 Sparkle 제거)

```
1. Offscreen Indicator로 장소 확인
2. 처음 1회만 Sparkle 재생 (위치 강조) ✅
3. 장소로 이동
4. 3D 오브젝트 확인 (Sparkle 없음, 깔끔) ✅
```

---

## 📊 인스펙터 설정 예시

### 기본 설정 (추천)

```
[General Settings]
Enable Sparkle: ✅
Sparkle Sprite: circle.png
Arrow Only: ✅

[Sparkle Size & Timing]
Sparkle Size: (80, 80)
Spawn Delay: 0.5초

[Scale Animation]
Start Scale: 0.5
Rapid Expand Scale: 1.5
Rapid Expand Duration: 0.15초
Slow Expand Scale: 2.0
Slow Expand Duration: 0.35초

[Fade Animation]
Fade In Duration: 0.2초
Full Opacity Duration: 0.1초
Rapid Fade Out Duration: 0.3초
Slow Fade Out Duration: 0.8초

[Color]
Sparkle Color: 흰색 (0.9 alpha)
```

**애니메이션 흐름:**
```
빠르게 확대 (0.15초) → 천천히 확대 (0.35초) →
유지 (0.1초) → 빠르게 페이드 (0.3초) →
천천히 사라짐 (0.8초)
```

---

## 💡 핵심 요약

### 변경 사항
**파일:**
- 삭제: `Assets/Scripts/UI/SparkleOnSpawn.cs`
- 삭제: `Assets/Scripts/UI/SparkleEffect.cs`
- 수정: `Assets/Scripts/UI/IndicatorSparkleHelper.cs`

**주요 개선:**
1. **3D 오브젝트 Sparkle 완전 제거** - 불필요한 효과 제거
2. **5단계 애니메이션** - 세밀한 제어 가능
3. **Ease-out 커브** - 자연스러운 감속 효과
4. **15개 설정** - 구간별 독립 조절

### 5단계 애니메이션
1. 빠른 확대 + 페이드인 (선형)
2. 느린 확대 (ease-out quadratic)
3. 최대 불투명도 유지
4. 빠른 페이드아웃 (선형)
5. 느린 페이드아웃 (ease-out cubic)

### Unity 설정 방법
- Hierarchy에 "IndicatorSparkleManager" GameObject 생성
- IndicatorSparkleHelper 컴포넌트 추가
- 인스펙터에서 15개 설정 조절
- 구간별 시간 조절로 원하는 효과 구현

### 사용자 경험 개선
- Offscreen Indicator만 Sparkle 효과 유지
- 3D 오브젝트는 깔끔하게 표시
- 자연스러운 애니메이션으로 시각적 품질 향상

---

**작성일:** 2025-12-05
**수정 파일:**
- 삭제: SparkleOnSpawn.cs, SparkleEffect.cs
- 수정: IndicatorSparkleHelper.cs

**핵심 개선:** 3D Sparkle 제거 + 5단계 세밀한 제어 + ease-out 커브
