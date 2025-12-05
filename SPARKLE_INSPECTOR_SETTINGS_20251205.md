# Sparkle 효과 인스펙터 설정 추가 (2025-12-05)

## 🎯 작업 내용

### IndicatorSparkleHelper 인스펙터 설정 추가 ✅

**파일:** `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs`

**변경 사항:**
- Singleton 패턴으로 변경 (단일 인스턴스)
- 모든 Sparkle 설정을 인스펙터에서 조절 가능
- 런타임 중 설정 변경 가능

---

## 🔧 Unity에서 설정하는 방법

### 1. GameObject 생성

**Hierarchy:**
```
우클릭 → Create Empty
이름: "IndicatorSparkleManager"
```

### 2. 컴포넌트 추가

```
IndicatorSparkleManager 선택
→ Add Component
→ IndicatorSparkleHelper
```

### 3. 인스펙터 설정

#### **Sparkle Settings**

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Enable Sparkle** | ✅ true | Sparkle 효과 전체 활성화/비활성화 |
| **Sparkle Sprite** | null | circle.png 할당 (선택사항, 자동 로드됨) |
| **Sparkle Size** | (80, 80) | Sparkle 이미지 크기 (픽셀) |
| **Spawn Delay** | 0.5초 | Indicator 생성 후 Sparkle 시작까지 딜레이 |
| **Fade In Duration** | 0.3초 | 페이드인 시간 |
| **Fade Out Duration** | 1.7초 | 페이드아웃 시간 |
| **Start Scale** | 0.5 | 시작 스케일 배율 |
| **Max Scale** | 2.0 | 최대 스케일 배율 |
| **Sparkle Color** | 흰색 (0.8 alpha) | Sparkle 색상 및 투명도 |

#### **Filter Settings**

| 설정 | 기본값 | 설명 |
|------|--------|------|
| **Arrow Only** | ✅ true | 화살표 인디케이터만 적용 (박스 제외) |

---

## 📊 인스펙터 설정 예시

### 예시 1: Sparkle 완전 비활성화

```
Enable Sparkle: ☐ (체크 해제)
```

**효과:**
- Sparkle 효과 완전 제거
- Indicator는 0.5초 페이드인만 적용

---

### 예시 2: 박스 인디케이터에도 적용

```
Arrow Only: ☐ (체크 해제)
```

**효과:**
- 화살표 + 박스 인디케이터 모두 Sparkle 적용

---

### 예시 3: 빠른 Sparkle (1초만)

```
Spawn Delay: 0.2초
Fade In Duration: 0.2초
Fade Out Duration: 0.6초
```

**효과:**
- 총 1초 애니메이션 (0.2 + 0.2 + 0.6)
- 빠르게 반짝이고 사라짐

---

### 예시 4: 큰 Sparkle

```
Sparkle Size: (150, 150)
Max Scale: 3.0
```

**효과:**
- 더 큰 Sparkle 이미지
- 3배까지 스케일 업

---

### 예시 5: 파란색 Sparkle

```
Sparkle Color: 파란색 (RGB: 0.5, 0.8, 1.0, Alpha: 1.0)
```

**효과:**
- 하늘색 Sparkle 효과

---

## 🎨 설정 조합 예시

### 기본 설정 (현재)

```
Enable Sparkle: ✅
Sparkle Size: (80, 80)
Spawn Delay: 0.5초
Fade In Duration: 0.3초
Fade Out Duration: 1.7초
Start Scale: 0.5
Max Scale: 2.0
Sparkle Color: 흰색 (0.8 alpha)
Arrow Only: ✅
```

**타이밍:**
```
T=0.0s: Indicator 생성
T=0.0~0.5s: Indicator 페이드인
T=0.5s: Sparkle 시작 (0.5초 딜레이)
T=0.5~0.8s: Sparkle 페이드인 + 스케일 업
T=0.8~2.5s: Sparkle 페이드아웃
→ 총 2.5초
```

---

### 빠른 설정 (짧고 강렬)

```
Enable Sparkle: ✅
Sparkle Size: (100, 100)
Spawn Delay: 0.2초
Fade In Duration: 0.1초
Fade Out Duration: 0.4초
Start Scale: 0.3
Max Scale: 2.5
Sparkle Color: 노란색 (1.0 alpha)
Arrow Only: ✅
```

**타이밍:**
```
T=0.0~0.5s: Indicator 페이드인
T=0.2s: Sparkle 시작
T=0.2~0.3s: Sparkle 빠르게 나타남
T=0.3~0.7s: Sparkle 사라짐
→ 총 0.5초 (빠르고 강렬)
```

---

### 느린 설정 (부드럽고 우아)

```
Enable Sparkle: ✅
Sparkle Size: (120, 120)
Spawn Delay: 0.8초
Fade In Duration: 0.5초
Fade Out Duration: 2.5초
Start Scale: 0.7
Max Scale: 1.5
Sparkle Color: 연한 파란색 (0.5 alpha)
Arrow Only: ✅
```

**타이밍:**
```
T=0.0~0.5s: Indicator 페이드인
T=0.8s: Sparkle 시작
T=0.8~1.3s: Sparkle 천천히 나타남
T=1.3~3.8s: Sparkle 천천히 사라짐
→ 총 3.0초 (부드럽고 우아)
```

---

## 🔧 코드 변경 사항

### Singleton 패턴 추가

**Before:**
```csharp
public class IndicatorSparkleHelper : MonoBehaviour
{
    // 하드코딩된 설정값
    private static GameObject sparklePool;
}
```

**After:**
```csharp
public class IndicatorSparkleHelper : MonoBehaviour
{
    private static IndicatorSparkleHelper instance;

    [Header("Sparkle Settings")]
    public bool enableSparkle = true;
    public Vector2 sparkleSize = new Vector2(80f, 80f);
    public float spawnDelay = 0.5f;
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 1.7f;
    public float startScale = 0.5f;
    public float maxScale = 2.0f;
    public Color sparkleColor = new Color(1f, 1f, 1f, 0.8f);

    [Header("Filter Settings")]
    public bool arrowOnly = true;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject); // 중복 방지
        }
    }
}
```

---

### PlaySparkleForIndicator() 수정

**Before:**
```csharp
public static void PlaySparkleForIndicator(Vector3 screenPosition, IndicatorType type, Sprite sprite = null)
{
    // 하드코딩된 설정값 사용
    if (type == IndicatorType.BOX) return;

    // ...
}
```

**After:**
```csharp
public static void PlaySparkleForIndicator(Vector3 screenPosition, IndicatorType type, Sprite sprite = null)
{
    // 인스턴스 체크
    if (instance == null) return;

    // 인스펙터 설정값 체크
    if (!instance.enableSparkle) return;
    if (instance.arrowOnly && type == IndicatorType.BOX) return;

    // 인스펙터 설정값 사용
    sparkleRect.sizeDelta = instance.sparkleSize;
    animator.StartAnimation(
        sparkleImage,
        sparkleRect,
        instance.spawnDelay,
        instance.fadeInDuration,
        instance.fadeOutDuration,
        instance.startScale,
        instance.maxScale,
        instance.sparkleColor
    );
}
```

---

### SparkleAnimator 수정

**Before:**
```csharp
public class SparkleAnimator : MonoBehaviour
{
    private System.Collections.IEnumerator AnimateSparkle()
    {
        // 하드코딩된 설정값
        float spawnDelay = 0.5f;
        float fadeInDuration = 0.3f;
        float fadeOutDuration = 1.7f;
        float startScale = 0.5f;
        float maxScale = 2.0f;
        Color sparkleColor = new Color(1f, 1f, 1f, 0.8f);

        // ...
    }
}
```

**After:**
```csharp
public class SparkleAnimator : MonoBehaviour
{
    private float spawnDelay;
    private float fadeInDuration;
    private float fadeOutDuration;
    private float startScale;
    private float maxScale;
    private Color sparkleColor;

    public void StartAnimation(
        Image img,
        RectTransform rect,
        float delay,
        float fadeIn,
        float fadeOut,
        float scaleStart,
        float scaleMax,
        Color color)
    {
        // 인스펙터 설정값 전달받음
        spawnDelay = delay;
        fadeInDuration = fadeIn;
        fadeOutDuration = fadeOut;
        startScale = scaleStart;
        maxScale = scaleMax;
        sparkleColor = color;
        StartCoroutine(AnimateSparkle());
    }
}
```

---

## 📝 체크리스트

### 완료 ✅
- [x] Singleton 패턴 구현
- [x] 인스펙터 필드 추가 (10개 설정)
- [x] enableSparkle로 전체 활성화/비활성화
- [x] arrowOnly로 화살표/박스 필터링
- [x] PlaySparkleForIndicator() 수정
- [x] SparkleAnimator 파라미터 전달
- [x] 문서 작성

### Unity에서 설정 필요
- [ ] Hierarchy에 "IndicatorSparkleManager" GameObject 생성
- [ ] IndicatorSparkleHelper 컴포넌트 추가
- [ ] 인스펙터에서 설정 조정
- [ ] Unity 빌드
- [ ] 디바이스 테스트

---

## 💡 핵심 요약

### 변경 사항
**파일:** `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs`

**주요 개선:**
1. Singleton 패턴으로 단일 인스턴스 보장
2. 모든 설정을 public 필드로 변경 (인스펙터 노출)
3. 런타임 중 설정 변경 가능
4. enableSparkle로 간편한 활성화/비활성화

### Unity 설정 방법
1. Hierarchy에 GameObject 생성
2. IndicatorSparkleHelper 컴포넌트 추가
3. 인스펙터에서 10개 설정 조절

### 인스펙터 설정 항목
- **Sparkle Settings** (8개): 활성화, 크기, 타이밍, 스케일, 색상
- **Filter Settings** (1개): 화살표만/전체 적용

---

**작성일:** 2025-12-05
**수정 파일:** `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs`
**핵심 개선:** Singleton + 인스펙터 설정으로 런타임 조절 가능
