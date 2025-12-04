# Sparkle Effect 구현 (2025-12-05)

## 🎯 작업 내용

### 1. Sparkle Effect 시스템 구현 ✅

**파일:**
- `c:\woopang\Assets\Scripts\UI\SparkleEffect.cs` - 메인 Sparkle 효과 컴포넌트
- `c:\woopang\Assets\Scripts\UI\SparkleOnSpawn.cs` - 3D 오브젝트용 자동 재생
- `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs` - UI 인디케이터용 헬퍼

### 2. Offscreen Indicator 통합 ✅

**파일:** `c:\woopang\Assets\Scripts\OffScreenIndicator\OffScreenIndicator.cs`

---

## 📋 Sparkle Effect 특징

### 사용 이미지
- **경로:** `C:\woopang\Assets\sou\UI\circle.png`
- **타입:** Sprite (Unity Image 컴포넌트에서 사용)

### 애니메이션 타이밍
```
T=0.0s: 오브젝트 생성
T=0.5s: Sparkle 시작 (0.5초 딜레이)
T=0.8s: 페이드인 완료 (0.3초 페이드인)
T=2.5s: 페이드아웃 완료 (1.7초 페이드아웃)
```

**총 시간:** 2.5초 (딜레이 0.5초 + 페이드인 0.3초 + 페이드아웃 1.7초)

### 스케일 애니메이션
```
시작: 0.5배 (작은 크기)
페이드인 중: 0.5배 → 2.0배 (커지면서 나타남)
페이드아웃 중: 2.0배 유지 (크기 유지하며 사라짐)
```

### 색상 및 투명도
```
시작: alpha = 0.0 (완전 투명)
페이드인 완료: alpha = 1.0 (불투명) 또는 0.8 (인디케이터용)
페이드아웃 완료: alpha = 0.0 (완전 투명)
```

---

## 🔧 구현 상세

### 1. SparkleEffect.cs

**핵심 기능:**
- 3D 오브젝트용 Sparkle 효과 (`PlaySparkle3D()`)
- UI 인디케이터용 Sparkle 효과 (`PlaySparkleUI()`)
- Canvas 자동 탐색 (Offscreen Indicator Canvas 우선)

**주요 설정값:**
```csharp
public float spawnDelay = 0.5f;           // 생성 후 딜레이
public float fadeInDuration = 0.3f;       // 페이드인 시간
public float fadeOutDuration = 1.7f;      // 페이드아웃 시간
public float maxScaleMultiplier = 2.0f;   // 최종 스케일 배율
public float startScaleMultiplier = 0.5f; // 시작 스케일 배율
public Color sparkleColor = Color.white;  // 반짝임 색상
```

**3D 오브젝트용 로직:**
```csharp
public void PlaySparkle3D()
{
    // 1. 0.5초 딜레이
    yield return new WaitForSeconds(spawnDelay);

    // 2. Sparkle 오브젝트 생성 (Canvas에 Image로 생성)
    CreateSparkleObject();

    // 3. 3D 오브젝트의 월드 좌표 → 스크린 좌표 변환
    Vector3 screenPos = mainCamera.WorldToScreenPoint(transform.position);

    // 4. 스크린 좌표 → Canvas 로컬 좌표 변환
    RectTransformUtility.ScreenPointToLocalPointInRectangle(...);

    // 5. 페이드인 + 스케일 업 (0.3초)
    // alpha: 0.0 → 1.0
    // scale: 0.5배 → 2.0배

    // 6. 페이드아웃 (1.7초, 스케일 유지)
    // alpha: 1.0 → 0.0

    // 7. 자동 정리 (GameObject 삭제)
}
```

**UI 인디케이터용 로직:**
```csharp
public void PlaySparkleUI(Vector3 screenPosition)
{
    // 3D와 동일한 로직, 단 스크린 좌표를 직접 받음
    // Canvas 좌표 변환만 다름
}
```

---

### 2. SparkleOnSpawn.cs

**용도:** 3D 오브젝트 (Sample_Prefab, GLB_Prefab) 자동 재생

**사용 방법:**
1. Prefab에 `SparkleOnSpawn` 컴포넌트 추가
2. `playOnEnable = true` 설정 (기본값)
3. 오브젝트 활성화 시 자동으로 Sparkle 효과 재생

**코드:**
```csharp
void OnEnable()
{
    if (playOnEnable)
    {
        PlaySparkle();
    }
}

public void PlaySparkle()
{
    // SparkleEffect가 없으면 자동 추가
    if (sparkleEffect == null)
    {
        sparkleEffect = gameObject.AddComponent<SparkleEffect>();

        // circle.png 로드 (Resources 폴더에서)
        Sprite circleSprite = Resources.Load<Sprite>("UI/circle");
        // 또는
        circleSprite = Resources.Load<Sprite>("sou/UI/circle");
    }

    // 반짝임 재생
    sparkleEffect.PlaySparkle3D();
}
```

**Resources 폴더 요구사항:**
- `circle.png`를 `Assets/Resources/UI/circle.png` 또는
- `Assets/Resources/sou/UI/circle.png`에 위치
- Unity에서 Import Settings → Texture Type: Sprite (2D and UI)

---

### 3. IndicatorSparkleHelper.cs

**용도:** Offscreen Indicator 화살표에만 Sparkle 효과 (박스 제외)

**핵심 메서드:**
```csharp
public static void PlaySparkleForIndicator(Vector3 screenPosition, IndicatorType type, Sprite sprite = null)
{
    // BOX 인디케이터는 제외 (화살표만)
    if (type == IndicatorType.BOX) return;

    // Canvas 찾기
    Canvas canvas = FindIndicatorCanvas();

    // Sparkle 오브젝트 생성
    GameObject sparkleObj = new GameObject("Indicator_Sparkle");
    sparkleObj.transform.SetParent(canvas.transform, false);

    // Image 컴포넌트 추가
    Image sparkleImage = sparkleObj.AddComponent<Image>();
    sparkleImage.sprite = sprite ?? LoadCircleSprite();

    // SparkleAnimator 추가 (자동 애니메이션 + 자동 삭제)
    SparkleAnimator animator = sparkleObj.AddComponent<SparkleAnimator>();
    animator.StartAnimation(sparkleImage, sparkleRect);
}
```

**SparkleAnimator 클래스:**
- Sparkle 애니메이션 전용 컴포넌트
- 애니메이션 완료 후 자동 삭제 (`Destroy(gameObject)`)
- 설정값 내장 (0.5초 딜레이, 0.3초 페이드인, 1.7초 페이드아웃)

```csharp
private System.Collections.IEnumerator AnimateSparkle()
{
    // 0.5초 딜레이
    yield return new WaitForSeconds(0.5f);

    // 0.3초 페이드인 + 스케일 업
    // 0.5배 → 2.0배
    // alpha: 0.0 → 0.8

    // 1.7초 페이드아웃 (스케일 유지)
    // alpha: 0.8 → 0.0

    // 자동 삭제
    Destroy(gameObject);
}
```

---

### 4. OffScreenIndicator.cs 통합

**변경 사항:**
```csharp
// Before (Line 163-180)
private Indicator GetIndicator(ref Indicator indicator, IndicatorType type)
{
    if (indicator != null)
    {
        if (indicator.Type != type)
        {
            indicator.Activate(false);
            indicator = type == IndicatorType.BOX ? BoxObjectPool.current.GetPooledObject() : ArrowObjectPool.current.GetPooledObject();
            indicator.Activate(true);
        }
    }
    else
    {
        indicator = type == IndicatorType.BOX ? BoxObjectPool.current.GetPooledObject() : ArrowObjectPool.current.GetPooledObject();
        indicator.Activate(true);
    }
    return indicator;
}

// After (Line 163-192)
private Indicator GetIndicator(ref Indicator indicator, IndicatorType type)
{
    bool isNewlyActivated = false;

    if (indicator != null)
    {
        if (indicator.Type != type)
        {
            indicator.Activate(false);
            indicator = type == IndicatorType.BOX ? BoxObjectPool.current.GetPooledObject() : ArrowObjectPool.current.GetPooledObject();
            indicator.Activate(true);
            isNewlyActivated = true;  // ✅ 새로 활성화됨
        }
    }
    else
    {
        indicator = type == IndicatorType.BOX ? BoxObjectPool.current.GetPooledObject() : ArrowObjectPool.current.GetPooledObject();
        indicator.Activate(true);
        isNewlyActivated = true;  // ✅ 새로 활성화됨
    }

    // ✅ 화살표 인디케이터가 새로 활성화되면 Sparkle 효과 재생
    if (isNewlyActivated && type == IndicatorType.ARROW)
    {
        Vector3 screenPos = indicator.transform.position;
        IndicatorSparkleHelper.PlaySparkleForIndicator(screenPos, type);
    }

    return indicator;
}
```

**효과:**
- 화살표 인디케이터가 처음 나타날 때만 Sparkle 효과
- 박스 인디케이터는 제외 (요청사항)
- 이미 활성화된 인디케이터가 위치만 변경될 때는 Sparkle 없음

---

## 🎨 사용자 경험

### 1. 3D 오브젝트 생성 시

```
사용자가 새로운 장소로 이동:
├─ DataManager가 3D 오브젝트 생성
├─ Sample_Prefab 활성화
├─ 0.5초 대기 (사용자가 오브젝트 인식)
├─ circle.png 반짝임 효과 시작
│   ├─ 작은 크기에서 시작 (0.5배)
│   ├─ 0.3초 동안 커지며 나타남 (→ 2.0배)
│   └─ 1.7초 동안 천천히 사라짐
└─ 총 2.5초 반짝임 애니메이션

효과:
✨ 새로 생성된 오브젝트를 시각적으로 강조
✨ 사용자의 주의를 끌어 장소 발견 유도
```

### 2. Offscreen Indicator (화살표) 생성 시

```
사용자가 카메라를 돌려서 장소가 화면 밖으로:
├─ OffScreenIndicator가 화살표 생성
├─ 화살표 인디케이터 활성화
├─ 0.5초 대기
├─ circle.png 반짝임 효과 시작 (화살표 위치에)
│   ├─ 작은 크기에서 시작 (0.5배)
│   ├─ 0.3초 동안 커지며 나타남 (→ 2.0배)
│   └─ 1.7초 동안 천천히 사라짐
└─ 총 2.5초 반짝임 애니메이션

효과:
✨ 화면 밖 장소의 방향을 시각적으로 강조
✨ 사용자가 화살표를 발견하도록 유도
✨ 박스 인디케이터는 제외 (이미 화면 안에 있어서 불필요)
```

---

## 📊 Before & After

### Before (Sparkle 효과 없음)

```
오브젝트 생성:
├─ DataManager → 3D 오브젝트 생성
└─ 오브젝트 즉시 표시 (PopUpAnimation만)

문제:
- 오브젝트가 갑자기 나타남
- 사용자가 새로운 장소를 인지하기 어려움
- 화살표 인디케이터도 조용히 나타남
```

### After (Sparkle 효과 적용)

```
오브젝트 생성:
├─ DataManager → 3D 오브젝트 생성
├─ PopUpAnimation (0.6초, 통통 튀는 효과)
└─ Sparkle 효과 (2.5초, 반짝임)
    ├─ 0.5초 딜레이 (오브젝트 안정화)
    ├─ 0.3초 페이드인 + 스케일 업 (주목!)
    └─ 1.7초 페이드아웃 (천천히 사라짐)

효과:
✨ 오브젝트가 반짝이며 등장 → 눈에 띔
✨ 사용자가 새로운 장소를 즉시 인지
✨ 화살표 인디케이터도 반짝이며 → 방향 안내 명확
```

---

## 🔧 구현 기술 상세

### Canvas 좌표 변환

**3D 오브젝트 → Canvas 좌표:**
```csharp
// 1. 3D 월드 좌표 → 스크린 좌표
Vector3 screenPos = mainCamera.WorldToScreenPoint(transform.position);

// 2. 스크린 좌표 → Canvas 로컬 좌표
RectTransformUtility.ScreenPointToLocalPointInRectangle(
    canvas.GetComponent<RectTransform>(),
    screenPos,
    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
    out Vector2 canvasPos
);

// 3. Canvas 좌표 적용
sparkleRect.anchoredPosition = canvasPos;
```

**이유:**
- Sparkle은 UI Image로 구현 (Canvas에 존재)
- 3D 오브젝트는 월드 공간에 존재
- 좌표계 변환 필요

### 애니메이션 Coroutine

**페이드인 + 스케일 업:**
```csharp
float elapsed = 0f;
while (elapsed < fadeInDuration)  // 0.3초
{
    elapsed += Time.deltaTime;
    float t = elapsed / fadeInDuration;  // 0.0 → 1.0

    // 페이드인
    Color color = sparkleColor;
    color.a = Mathf.Lerp(0f, 1f, t);  // alpha: 0.0 → 1.0
    image.color = color;

    // 스케일 업
    float scale = Mathf.Lerp(0.5f, 2.0f, t);  // 0.5배 → 2.0배
    rectTransform.localScale = baseScale * scale;

    yield return null;  // 다음 프레임까지 대기
}
```

**페이드아웃:**
```csharp
float elapsed = 0f;
while (elapsed < fadeOutDuration)  // 1.7초
{
    elapsed += Time.deltaTime;
    float t = elapsed / fadeOutDuration;  // 0.0 → 1.0

    // 페이드아웃 (스케일은 2.0배 유지)
    Color color = sparkleColor;
    color.a = Mathf.Lerp(1f, 0f, t);  // alpha: 1.0 → 0.0
    image.color = color;

    yield return null;
}
```

### 자동 정리

**3D 오브젝트용 (SparkleEffect.cs):**
```csharp
void Cleanup()
{
    if (sparkleObject != null)
    {
        Destroy(sparkleObject);  // Sparkle GameObject 삭제
    }
    isPlaying = false;
}

void OnDestroy()
{
    Cleanup();  // 오브젝트 파괴 시 자동 정리
}
```

**UI 인디케이터용 (SparkleAnimator.cs):**
```csharp
private System.Collections.IEnumerator AnimateSparkle()
{
    // ... 애니메이션 ...

    // 애니메이션 완료 후 자동 삭제
    Destroy(gameObject);
}
```

---

## 📝 체크리스트

### 완료 ✅
- [x] SparkleEffect.cs 구현 (3D 오브젝트용 + UI용)
- [x] SparkleOnSpawn.cs 구현 (3D 오브젝트 자동 재생)
- [x] IndicatorSparkleHelper.cs 구현 (화살표 인디케이터용)
- [x] OffScreenIndicator.cs 통합 (화살표만)
- [x] Canvas 자동 탐색 기능
- [x] 좌표 변환 로직 (월드 → 스크린 → Canvas)
- [x] 페이드인/아웃 애니메이션
- [x] 스케일 애니메이션 (0.5배 → 2.0배)
- [x] 자동 정리 (메모리 누수 방지)

### 테스트 필요
- [ ] Unity 빌드
- [ ] circle.png Resources 폴더 배치
  - [ ] Assets/Resources/UI/circle.png 또는
  - [ ] Assets/Resources/sou/UI/circle.png
- [ ] Sample_Prefab에 SparkleOnSpawn 컴포넌트 추가
- [ ] GLB_Prefab에 SparkleOnSpawn 컴포넌트 추가
- [ ] 디바이스 설치
- [ ] 3D 오브젝트 생성 시 Sparkle 효과 확인
- [ ] 화살표 인디케이터 생성 시 Sparkle 효과 확인
- [ ] 박스 인디케이터에는 Sparkle 없는지 확인

### 수동 설정 필요
1. **circle.png를 Resources 폴더로 이동:**
   ```
   현재 위치: C:\woopang\Assets\sou\UI\circle.png
   이동할 위치: C:\woopang\Assets\Resources\sou\UI\circle.png
   ```

2. **Sample_Prefab에 SparkleOnSpawn 추가:**
   - Unity 에디터에서 Sample_Prefab 열기
   - Add Component → SparkleOnSpawn
   - Play On Enable: ✅ (체크)
   - Apply

3. **GLB_Prefab에 SparkleOnSpawn 추가:**
   - Unity 에디터에서 GLB_Prefab 열기
   - Add Component → SparkleOnSpawn
   - Play On Enable: ✅ (체크)
   - Apply

---

## 💡 핵심 요약

### 1. Sparkle Effect 시스템
**구현:**
- SparkleEffect.cs: 메인 컴포넌트
- SparkleOnSpawn.cs: 3D 오브젝트 자동 재생
- IndicatorSparkleHelper.cs: UI 인디케이터 헬퍼

**효과:**
- 0.5초 딜레이 → 0.3초 페이드인 + 스케일 업 → 1.7초 페이드아웃
- 총 2.5초 애니메이션
- circle.png 사용

### 2. 적용 위치
**3D 오브젝트:**
- Sample_Prefab (SparkleOnSpawn 추가 필요)
- GLB_Prefab (SparkleOnSpawn 추가 필요)

**UI 인디케이터:**
- 화살표 인디케이터만 (OffScreenIndicator.cs에 통합됨)
- 박스 인디케이터 제외 (요청사항)

### 3. 기대 효과
**사용자 경험:**
- ✨ 새로운 오브젝트/장소를 시각적으로 강조
- ✨ 반짝임으로 주의를 끌어 발견 유도
- ✨ 화살표 방향 안내 명확

**기술적 구현:**
- Canvas 좌표 변환 (월드 → 스크린 → Canvas)
- Coroutine 애니메이션
- 자동 정리 (메모리 누수 방지)

---

**작성일:** 2025-12-05
**수정 파일:**
1. `c:\woopang\Assets\Scripts\UI\SparkleEffect.cs` - 메인 Sparkle 시스템
2. `c:\woopang\Assets\Scripts\UI\SparkleOnSpawn.cs` - 3D 오브젝트 자동 재생
3. `c:\woopang\Assets\Scripts\UI\IndicatorSparkleHelper.cs` - UI 인디케이터 헬퍼
4. `c:\woopang\Assets\Scripts\OffScreenIndicator\OffScreenIndicator.cs` - 화살표 통합

**핵심 개선:**
- 반짝임 효과로 오브젝트/인디케이터 강조
- 부드러운 페이드인/아웃 애니메이션
- 스케일 업 효과로 시선 유도

**다음 작업:**
- circle.png를 Resources 폴더로 이동
- Prefab에 SparkleOnSpawn 컴포넌트 추가
- Unity 빌드 및 테스트
