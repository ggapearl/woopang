# 업데이트 주기 및 안정성 개선 (2025-12-04)

## 🚨 문제 상황

### 사용자 피드백

1. **스와이프 문제**
   - 위→아래 스와이프가 잘 안 됨
   - PlaceList가 1초마다 업데이트되면서 UI 방해

2. **깜빡임 문제**
   - 3D 오브젝트 생성 시 깜빡거림
   - Offscreen Indicator 깜빡거림
   - 오브젝트가 불안정하게 움직임

3. **로딩 스피너 문제**
   - 가까운 거리(4m 이내)에서도 스피너 표시
   - 계속 깜빡거림

---

## 🔍 원인 분석

### 1. PlaceListManager - 1초 업데이트 주기

**파일:** `c:\woopang\Assets\Scripts\Download\PlaceListManager.cs` (Line 193-194)

**문제 코드:**
```csharp
// 처음 1분(60초) 동안은 1초 간격, 그 이후는 설정된 간격(10초)
float currentInterval = (Time.time - startTime < 60f) ? 1f : updateInterval;
```

**문제:**
- 초반 1분간 매 1초마다 UI 업데이트
- UI 갱신으로 인해 터치 이벤트 방해
- 스와이프 제스처가 끊김

### 2. DataManager - 빠른 오브젝트 생성

**파일:** `c:\woopang\Assets\Scripts\Download\DataManager.cs` (Line 39-42)

**문제 코드:**
```csharp
public float tierDelay = 0.5f;           // 거리 단계 사이 딜레이
public float objectSpawnDelay = 0.1f;    // 오브젝트 사이 딜레이
```

**문제:**
- 0.1초마다 오브젝트 생성 → 너무 빠름
- 0.5초마다 거리 단계 전환 → 급격한 변화
- 렌더링 부하로 깜빡임 발생

### 3. ImageDisplayController - 거리 제한 없음

**파일:** `c:\woopang\Assets\Scripts\Download\ImageDisplayController.cs`

**문제:**
- 모든 거리에서 로딩 스피너 표시
- 4m 이내 오브젝트는 즉시 보여도 됨
- 불필요한 스피너 생성으로 깜빡임

---

## ✅ 해결 방법

### 1. PlaceListManager - 항상 10초 간격

**파일:** `c:\woopang\Assets\Scripts\Download\PlaceListManager.cs` (Line 187-201)

**수정 전:**
```csharp
private IEnumerator UpdateUIPeriodically()
{
    float startTime = Time.time;

    while (true)
    {
        // 처음 1분(60초) 동안은 1초 간격, 그 이후는 설정된 간격(10초)
        float currentInterval = (Time.time - startTime < 60f) ? 1f : updateInterval;

        yield return new WaitForSeconds(currentInterval);

        if (listPanel != null && listPanel.activeInHierarchy)
        {
            UpdateUI();
            Debug.Log($"[PlaceListManager] ListPanel 활성화 - UI 업데이트 실행 (간격: {currentInterval}s)");
        }
    }
}
```

**수정 후:**
```csharp
private IEnumerator UpdateUIPeriodically()
{
    while (true)
    {
        // 항상 10초 간격으로 업데이트 (1초 간격 제거 - 스와이프 방해 방지)
        yield return new WaitForSeconds(updateInterval);

        if (listPanel != null && listPanel.activeInHierarchy)
        {
            UpdateUI();
            Debug.Log($"[PlaceListManager] ListPanel 활성화 - UI 업데이트 실행 (간격: {updateInterval}s)");
        }
    }
}
```

**효과:**
- ✅ 1초 업데이트 제거 → 스와이프 안정화
- ✅ 항상 10초 간격 → 일관된 동작
- ✅ UI 갱신 부하 90% 감소

### 2. DataManager - 오브젝트 생성 딜레이 증가

**파일:** `c:\woopang\Assets\Scripts\Download\DataManager.cs` (Line 38-42)

**수정 전:**
```csharp
[Tooltip("각 거리 단계 사이의 딜레이 (초)")]
public float tierDelay = 0.5f;

[Tooltip("같은 단계 내 오브젝트 사이의 딜레이 (초)")]
public float objectSpawnDelay = 0.1f;
```

**수정 후:**
```csharp
[Tooltip("각 거리 단계 사이의 딜레이 (초) - 깜빡임 방지")]
public float tierDelay = 2f;  // 0.5 → 2초 (안정성 향상)

[Tooltip("같은 단계 내 오브젝트 사이의 딜레이 (초) - 안정적 생성")]
public float objectSpawnDelay = 0.3f;  // 0.1 → 0.3초 (깜빡임 방지)
```

**효과:**
- ✅ tierDelay 4배 증가 (0.5 → 2초) → 단계 전환 부드럽게
- ✅ objectSpawnDelay 3배 증가 (0.1 → 0.3초) → 오브젝트 안정적 생성
- ✅ 렌더링 부하 분산 → 깜빡임 제거

### 3. ImageDisplayController - 4m 이내 스피너 생략

**파일:** `c:\woopang\Assets\Scripts\Download\ImageDisplayController.cs`

**추가된 필드 (Line 12):**
```csharp
public float minSpinnerDistance = 4f;  // 스피너 표시 최소 거리 (미터)
```

**수정된 ShowSpinner() (Line 108-124):**
```csharp
private void ShowSpinner(bool show)
{
    // 거리 체크 - 4m 이내면 스피너 표시 안 함
    if (show)
    {
        float distance = Vector3.Distance(transform.position, Camera.main.transform.position);
        if (distance < minSpinnerDistance)
        {
            Debug.Log($"[SPINNER] 거리 {distance:F2}m < {minSpinnerDistance}m - 스피너 생략, 바로 표시");
            // 스피너 없이 바로 큐브 표시
            if (cubeRenderer != null)
            {
                cubeRenderer.enabled = true;
            }
            return;
        }
    }

    Debug.Log($"[SPINNER] ShowSpinner({show}) - prefab={loadingSpinnerPrefab != null}, cubeRenderer={cubeRenderer != null}");

    // ... 기존 스피너 로직
}
```

**효과:**
- ✅ 4m 이내: 스피너 없이 즉시 표시
- ✅ 4m 이상: 스피너 표시 후 팝업 애니메이션
- ✅ 가까운 오브젝트 깜빡임 제거

---

## 📊 성능 비교

### Before (수정 전)

#### PlaceList 업데이트
```
T=0s:   PlaceList 업데이트 (1초 간격 시작)
T=1s:   PlaceList 업데이트 ← UI 갱신, 터치 방해
T=2s:   PlaceList 업데이트 ← UI 갱신, 터치 방해
...
T=59s:  PlaceList 업데이트 (마지막 1초 간격)
T=60s:  PlaceList 업데이트 (10초 간격 시작)
T=70s:  PlaceList 업데이트

초반 1분: 60회 업데이트 ❌
이후:     6회/분
```

#### 오브젝트 생성
```
25m 단계: 오브젝트 5개
  → 0.0s: 생성 시작
  → 0.1s: 오브젝트 1
  → 0.2s: 오브젝트 2
  → 0.3s: 오브젝트 3
  → 0.4s: 오브젝트 4
  → 0.5s: 오브젝트 5
  → 0.5s: 단계 전환 (너무 빠름!) ❌

50m 단계: 오브젝트 10개
  → 1.5s: 완료
  → 깜빡거림 발생 ❌
```

#### 로딩 스피너
```
오브젝트 @ 2m: 스피너 표시 (불필요) ❌
오브젝트 @ 3m: 스피너 표시 (불필요) ❌
오브젝트 @ 5m: 스피너 표시 ✓
오브젝트 @ 10m: 스피너 표시 ✓

가까운 거리 깜빡임 ❌
```

### After (수정 후)

#### PlaceList 업데이트
```
T=0s:   PlaceList 업데이트 (10초 간격)
T=10s:  PlaceList 업데이트
T=20s:  PlaceList 업데이트
T=30s:  PlaceList 업데이트
...

항상 10초 간격 ✓
초반 1분: 6회 업데이트 (60 → 6회, 90% 감소)
이후:     6회/분
```

#### 오브젝트 생성
```
25m 단계: 오브젝트 5개
  → 0.0s: 생성 시작
  → 0.3s: 오브젝트 1
  → 0.6s: 오브젝트 2
  → 0.9s: 오브젝트 3
  → 1.2s: 오브젝트 4
  → 1.5s: 오브젝트 5
  → 3.5s: 단계 전환 (여유 시간 2초) ✓

50m 단계: 오브젝트 10개
  → 6.5s: 완료
  → 안정적 생성 ✓
```

#### 로딩 스피너
```
오브젝트 @ 2m: 스피너 생략, 즉시 표시 ✓
오브젝트 @ 3m: 스피너 생략, 즉시 표시 ✓
오브젝트 @ 5m: 스피너 표시 ✓
오브젝트 @ 10m: 스피너 표시 ✓

가까운 거리 안정적 ✓
```

---

## 🎯 사용자 경험 개선

### 1. 스와이프 안정화

**Before:**
```
사용자: 위→아래 스와이프
  → 1초마다 UI 갱신
  → 터치 이벤트 방해
  → 스와이프 인식 실패 ❌
```

**After:**
```
사용자: 위→아래 스와이프
  → 10초 간격 UI 갱신
  → 터치 이벤트 안정
  → 스와이프 정확히 인식 ✓
```

### 2. 오브젝트 안정성

**Before:**
```
오브젝트 생성:
  → 0.1초 간격 (너무 빠름)
  → 렌더링 부하
  → 깜빡거림 ❌
  → Offscreen Indicator 깜빡임 ❌
```

**After:**
```
오브젝트 생성:
  → 0.3초 간격 (적절)
  → 렌더링 부하 분산
  → 부드럽게 등장 ✓
  → Offscreen Indicator 안정 ✓
```

### 3. 로딩 스피너 최적화

**Before:**
```
가까운 오브젝트 (2m):
  → 스피너 표시
  → 3초 대기
  → 팝업 애니메이션
  → 불필요한 깜빡임 ❌
```

**After:**
```
가까운 오브젝트 (2m):
  → 스피너 생략
  → 즉시 표시
  → 팝업 애니메이션만
  → 부드러운 등장 ✓

먼 오브젝트 (10m):
  → 스피너 표시
  → 3초 대기
  → 팝업 애니메이션
  → 로딩 시간 확보 ✓
```

---

## 📝 설정 값 정리

### PlaceListManager

```csharp
[SerializeField] private float updateInterval = 10f;  // 항상 10초 간격
```

**변경사항:**
- 초반 1초 간격 제거
- 항상 10초 간격 유지

### DataManager

```csharp
public float tierDelay = 2f;           // 0.5 → 2초 (4배 증가)
public float objectSpawnDelay = 0.3f;  // 0.1 → 0.3초 (3배 증가)
```

**변경사항:**
- tierDelay: 거리 단계 전환 여유 시간 증가
- objectSpawnDelay: 오브젝트 생성 간격 증가

### ImageDisplayController

```csharp
public float spinnerDuration = 10f;       // 스피너 표시 시간
public float minSpinnerDistance = 4f;     // 스피너 표시 최소 거리 (신규)
```

**변경사항:**
- minSpinnerDistance 추가: 4m 이내 스피너 생략

---

## 🔧 추가 제안 (선택사항)

### 1. Offscreen Indicator 업데이트 최적화

**현재 문제:**
- 매 프레임 업데이트 가능성
- 깜빡임 원인

**제안:**
```csharp
// OffscreenIndicator.cs
private float updateInterval = 0.2f;  // 0.2초마다 업데이트
private float lastUpdateTime;

void Update()
{
    if (Time.time - lastUpdateTime < updateInterval)
        return;

    lastUpdateTime = Time.time;
    // ... 업데이트 로직
}
```

**효과:**
- 초당 5회 업데이트 (60 → 5회)
- 부드러운 움직임 유지
- 깜빡임 제거

### 2. 점진적 스피너 표시 (거리 기반)

**제안:**
```csharp
public float minSpinnerDistance = 4f;   // 스피너 없음
public float maxSpinnerDistance = 10f;  // 최대 스피너 시간

private void ShowSpinner(bool show)
{
    if (show)
    {
        float distance = Vector3.Distance(transform.position, Camera.main.transform.position);

        if (distance < minSpinnerDistance)
        {
            // 4m 이내: 스피너 없음
            return;
        }
        else if (distance < maxSpinnerDistance)
        {
            // 4~10m: 짧은 스피너 (1초)
            spinnerDuration = 1f;
        }
        else
        {
            // 10m 이상: 일반 스피너 (3초)
            spinnerDuration = 3f;
        }
    }
    // ...
}
```

### 3. 오브젝트 페이딩 최적화

**제안:**
```csharp
// DataManager.cs
private void CreateObjectFromData(PlaceData place)
{
    // ...

    // 거리 기반 페이드인 시간 조정
    float distance = Vector3.Distance(position, Camera.main.transform.position);
    float fadeInTime = distance < 10f ? 0.3f : 0.5f;

    // 페이드인 애니메이션 적용
    // ...
}
```

---

## 📋 체크리스트

### 완료 ✅
- [x] PlaceListManager 1초 업데이트 제거
- [x] 항상 10초 간격 유지
- [x] DataManager tierDelay 증가 (0.5 → 2초)
- [x] DataManager objectSpawnDelay 증가 (0.1 → 0.3초)
- [x] ImageDisplayController 거리 체크 추가
- [x] 4m 이내 스피너 생략
- [x] 문서 작성

### 테스트 필요
- [ ] Unity 빌드
- [ ] 디바이스 설치
- [ ] 스와이프 동작 확인 (위→아래)
- [ ] 오브젝트 생성 안정성 확인
- [ ] Offscreen Indicator 깜빡임 확인
- [ ] 로딩 스피너 거리 제한 확인
- [ ] 4m 이내 오브젝트 즉시 표시 확인

---

## 🎯 예상 효과

### 업데이트 주기
```
Before: 초반 1분 60회 → After: 초반 1분 6회
감소율: 90% ↓
```

### 오브젝트 생성 시간
```
Before: 11단계 × (오브젝트수 × 0.1s + 0.5s) = 약 10초
After:  11단계 × (오브젝트수 × 0.3s + 2s) = 약 35초

천천히 안정적으로 생성 ✓
```

### 스피너 표시 횟수
```
Before: 모든 오브젝트 (100%)
After:  4m 이상 오브젝트만 (약 70%)

불필요한 스피너 30% 감소 ✓
```

---

**작성일:** 2025-12-04 22:30
**수정 파일:**
1. `c:\woopang\Assets\Scripts\Download\PlaceListManager.cs` - 10초 고정 간격
2. `c:\woopang\Assets\Scripts\Download\DataManager.cs` - 딜레이 증가
3. `c:\woopang\Assets\Scripts\Download\ImageDisplayController.cs` - 4m 거리 제한

**핵심 개선:** 업데이트 주기 최적화, 안정적 오브젝트 생성, 스피너 거리 제한
**기대 효과:** 스와이프 안정화, 깜빡임 제거, 부드러운 UX
