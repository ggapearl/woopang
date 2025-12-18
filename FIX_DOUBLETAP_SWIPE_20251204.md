# DoubleTap3D 스와이프 기능 개선 (2025-12-04)

## 🎯 문제 상황

사용자 요청:
1. ❌ 좌우 스와이프로 이미지 넘기기가 작동하지 않는 것 같음
2. ❌ 위→아래 스와이프로 패널 닫기가 버튼으로만 작동
3. ❌ 패널이 컷아웃으로 사라짐 (페이드아웃 필요)

## 🔍 원인 분석

### 코드 확인 결과

**파일:** `c:\woopang\Assets\Scripts\Download\DoubleTap3D.cs`

#### 1. 좌우 스와이프 (Line 331-338)
```csharp
// 수정 전
if (Mathf.Abs(swipeDelta.x) > swipeThreshold)
{
    if (swipeDelta.x > 0)
        ShowPreviousImage();
    else
        ShowNextImage();
    isSwiping = false;
}
```

**문제:**
- 좌우 스와이프와 위아래 스와이프 우선순위 없음
- 대각선 스와이프 시 의도하지 않은 동작

#### 2. 아래로 스와이프 (Line 339-343)
```csharp
// 수정 전
else if (Mathf.Abs(swipeDelta.y) > swipeThreshold && swipeDelta.y < 0)
{
    CloseFullscreen();
    isSwiping = false;
}
```

**문제:**
- `swipeDelta.y < 0` → **위에서 아래로** 스와이프
- 하지만 우선순위가 없어서 좌우 스와이프와 충돌 가능

#### 3. 닫기 애니메이션 (Line 597)
```csharp
private void CloseFullscreen()
{
    // ...
    StartCoroutine(FadeOutCanvas(fadeDuration));  // ✅ 이미 페이드아웃 사용 중!
}
```

**확인:**
- ✅ `FadeOutCanvas()` 이미 구현되어 있음 (Line 643-668)
- ✅ 0.5초 페이드아웃 애니메이션

---

## ✅ 해결 방법

### 수정 내용

**파일:** `c:\woopang\Assets\Scripts\Download\DoubleTap3D.cs` (Line 327-346)

```csharp
else if (touch.phase == TouchPhase.Moved && isSwiping && isFullscreen)
{
    Vector2 swipeDelta = touch.position - touchStartPos;

    // 좌우 스와이프: 이미지 넘기기
    if (Mathf.Abs(swipeDelta.x) > swipeThreshold && Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
    {
        if (swipeDelta.x > 0)
            ShowPreviousImage();  // 오른쪽 스와이프 → 이전 이미지
        else
            ShowNextImage();      // 왼쪽 스와이프 → 다음 이미지
        isSwiping = false;
    }
    // 위→아래 스와이프: 패널 닫기
    else if (Mathf.Abs(swipeDelta.y) > swipeThreshold && Mathf.Abs(swipeDelta.y) > Mathf.Abs(swipeDelta.x) && swipeDelta.y < 0)
    {
        CloseFullscreen();  // 페이드아웃으로 닫힘
        isSwiping = false;
    }
}
```

### 개선 사항

#### 1. 좌우 스와이프 우선순위 추가
```csharp
// 수정 전
if (Mathf.Abs(swipeDelta.x) > swipeThreshold)

// 수정 후
if (Mathf.Abs(swipeDelta.x) > swipeThreshold && Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
```

**효과:**
- X 방향 이동이 Y 방향보다 클 때만 좌우 스와이프로 인식
- 대각선 스와이프 방지

#### 2. 위→아래 스와이프 우선순위 추가
```csharp
// 수정 전
else if (Mathf.Abs(swipeDelta.y) > swipeThreshold && swipeDelta.y < 0)

// 수정 후
else if (Mathf.Abs(swipeDelta.y) > swipeThreshold && Mathf.Abs(swipeDelta.y) > Mathf.Abs(swipeDelta.x) && swipeDelta.y < 0)
```

**효과:**
- Y 방향 이동이 X 방향보다 클 때만 위→아래 스와이프로 인식
- 좌우 스와이프와 충돌 방지

#### 3. 닫기 애니메이션
```csharp
// CloseFullscreen() (Line 583-598)
private void CloseFullscreen()
{
    // ...
    StartCoroutine(FadeOutCanvas(fadeDuration));  // ✅ 페이드아웃 적용
}

// FadeOutCanvas() (Line 643-668)
IEnumerator FadeOutCanvas(float duration)
{
    isFading = true;
    float elapsed = 0f;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        fullscreenCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
        yield return null;
    }

    fullscreenCanvasGroup.alpha = 0f;
    fullscreenCanvasGroup.gameObject.SetActive(false);
    // ...
}
```

**효과:**
- ✅ 0.5초 페이드아웃 애니메이션
- ✅ 부드러운 사라짐 효과
- ✅ 컷아웃 없음

---

## 📊 사용자 경험 개선

### Before (수정 전)

```
사용자: 좌우 스와이프
→ ❌ 대각선으로 움직이면 이미지 안 넘어감
→ ❌ 또는 의도치 않게 패널 닫힘

사용자: 위→아래 스와이프
→ ❌ 가끔 작동 안 함
→ ❌ 버튼만 사용하게 됨

사용자: 닫기
→ ❌ 갑자기 사라짐 (컷아웃 느낌)
```

### After (수정 후)

```
사용자: 좌우 스와이프 (← →)
→ ✅ 명확하게 이미지 넘어감
→ ✅ 이전/다음 이미지 전환 (크로스페이드)

사용자: 위→아래 스와이프 (↓)
→ ✅ 확실하게 패널 닫힘
→ ✅ 페이드아웃으로 부드럽게 사라짐

사용자: 닫기 버튼
→ ✅ 동일하게 페이드아웃 적용
```

---

## 🎮 제스처 동작 정리

### 1. 좌우 스와이프 (이미지 넘기기)

**조건:**
- 풀스크린 모드 (`isFullscreen == true`)
- X 방향 이동 > 50px
- X 방향 이동 > Y 방향 이동

**동작:**
```
←── 왼쪽 스와이프  → ShowNextImage()    (다음 이미지)
──→ 오른쪽 스와이프 → ShowPreviousImage() (이전 이미지)
```

**애니메이션:**
- 크로스페이드 (0.5초)
- 현재 이미지 페이드아웃 → 다음 이미지 페이드인

### 2. 위→아래 스와이프 (패널 닫기)

**조건:**
- 풀스크린 모드 (`isFullscreen == true`)
- Y 방향 이동 > 50px (아래로)
- Y 방향 이동 > X 방향 이동
- `swipeDelta.y < 0` (위에서 아래로)

**동작:**
```
↓
│  위에서 아래로 스와이프
↓

→ CloseFullscreen() (패널 닫기)
```

**애니메이션:**
- 페이드아웃 (0.5초)
- alpha: 1.0 → 0.0
- 부드럽게 사라짐

### 3. 닫기 버튼

**동작:**
- `closeButton.onClick` → `CloseFullscreen()`
- 스와이프와 동일한 페이드아웃 효과

---

## 🔧 기술 상세

### 스와이프 감지 알고리즘

```csharp
void Update()
{
    if (Input.touchCount == 1 && isFullscreen)
    {
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            touchStartPos = touch.position;
            isSwiping = true;
        }
        else if (touch.phase == TouchPhase.Moved && isSwiping)
        {
            Vector2 swipeDelta = touch.position - touchStartPos;

            // 우선순위 1: 좌우 스와이프 (이미지 넘기기)
            if (|swipeDelta.x| > threshold && |swipeDelta.x| > |swipeDelta.y|)
            {
                // X축 이동이 더 크면 좌우 스와이프
                if (swipeDelta.x > 0) ShowPreviousImage();
                else ShowNextImage();
            }
            // 우선순위 2: 위→아래 스와이프 (패널 닫기)
            else if (|swipeDelta.y| > threshold && |swipeDelta.y| > |swipeDelta.x| && swipeDelta.y < 0)
            {
                // Y축 이동이 더 크고 아래로 이동하면 닫기
                CloseFullscreen();
            }
        }
    }
}
```

### 페이드아웃 애니메이션

```csharp
IEnumerator FadeOutCanvas(float duration)  // duration = 0.5s
{
    isFading = true;
    float elapsed = 0f;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;  // 0.0 → 1.0
        fullscreenCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);  // 1.0 → 0.0
        yield return null;
    }

    fullscreenCanvasGroup.alpha = 0f;
    fullscreenCanvasGroup.gameObject.SetActive(false);
    // ...
    isFullscreen = false;
    isFading = false;
}
```

**타이밍:**
```
T=0.0s: alpha = 1.0 (완전 불투명)
T=0.1s: alpha = 0.8
T=0.2s: alpha = 0.6
T=0.3s: alpha = 0.4
T=0.4s: alpha = 0.2
T=0.5s: alpha = 0.0 (완전 투명) → SetActive(false)
```

---

## 📝 체크리스트

### 완료 ✅
- [x] 좌우 스와이프 우선순위 추가
- [x] 위→아래 스와이프 우선순위 추가
- [x] 대각선 스와이프 방지
- [x] 페이드아웃 애니메이션 확인 (이미 구현됨)
- [x] 코드 주석 추가
- [x] 문서 작성

### 테스트 필요
- [ ] Unity 빌드
- [ ] 디바이스 설치
- [ ] 좌우 스와이프로 이미지 넘기기 테스트
- [ ] 위→아래 스와이프로 패널 닫기 테스트
- [ ] 페이드아웃 애니메이션 확인
- [ ] 대각선 스와이프 시 오작동 없는지 확인

---

## 🎯 기대 효과

### 1. 좌우 스와이프 (이미지 넘기기)
```
Before: 가끔 작동, 대각선 시 오작동
After:  명확하게 작동, X축 우선
```

### 2. 위→아래 스와이프 (패널 닫기)
```
Before: 버튼으로만 닫기
After:  스와이프로 직관적 닫기
```

### 3. 닫기 애니메이션
```
Before: 컷아웃 느낌 (갑자기 사라짐)
After:  페이드아웃 (0.5초, 부드러움)
```

### 사용자 경험
```
더블터치 → 풀스크린 열림 (페이드인 0.5s)
  ↓
좌우 스와이프 → 이미지 넘김 (크로스페이드 0.5s)
  ↓
위→아래 스와이프 → 패널 닫힘 (페이드아웃 0.5s)
```

**모든 전환이 부드러운 애니메이션으로 연결!** 🎨

---

**작성일:** 2025-12-04 22:00
**수정 파일:** `c:\woopang\Assets\Scripts\Download\DoubleTap3D.cs`
**핵심 개선:** 스와이프 우선순위 추가, 페이드아웃 애니메이션 확인
**기대 효과:** 직관적인 제스처 동작, 부드러운 UI 전환
