# AR 오브젝트 가시성 최종 수정 (2025-12-04)

## 🎯 최종 해결 방법

### 문제
- ImageDisplayController가 **모든 Renderer를 끄고** 있었음
- `GetComponentsInChildren<Renderer>()` 사용 → Pulse 등 다른 렌더러도 영향받음
- GameObject가 비활성화되지는 않았지만, 여전히 AR 환경에서 보이지 않음

### 최종 수정
**cubeRenderer만 정확하게 제어**하도록 변경

#### ShowSpinner() 메서드 (최종)

```csharp
private void ShowSpinner(bool show)
{
    Debug.Log($"[DEBUG_SPINNER] ShowSpinner({show}) - cubeRenderer={cubeRenderer != null}");

    // 스피너 생성
    if (show && currentSpinner == null && loadingSpinnerPrefab != null)
    {
        currentSpinner = Instantiate(loadingSpinnerPrefab, transform);
        currentSpinner.transform.localPosition = Vector3.zero;
        Debug.Log($"[DEBUG_SPINNER] 스피너 생성 완료");
    }

    // ⭐ cubeRenderer만 제어 (Pulse 등 다른 렌더러는 그대로 유지)
    if (cubeRenderer != null)
    {
        cubeRenderer.enabled = !show; // show=true면 끔, show=false면 켬
        Debug.Log($"[DEBUG_SPINNER] cubeRenderer.enabled = {cubeRenderer.enabled} (GameObject.active={cubeRenderer.gameObject.activeSelf})");
    }
    else
    {
        // cubeRenderer가 없으면 Cube 자식 찾기
        Transform cubeChild = transform.Find("Cube");
        if (cubeChild != null)
        {
            MeshRenderer meshRenderer = cubeChild.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = !show;
                Debug.Log($"[DEBUG_SPINNER] Cube MeshRenderer.enabled = {meshRenderer.enabled}");
            }
        }
    }

    // 스피너 켜기/끄기
    if (currentSpinner != null)
    {
        currentSpinner.SetActive(show);
        Debug.Log($"[DEBUG_SPINNER] 스피너 활성 상태 = {show}");
    }

    // 로딩 완료 시 등장 애니메이션
    if (!show)
    {
        Debug.Log($"[DEBUG_SPINNER] 팝업 애니메이션 시작");
        StartCoroutine(PopUpAnimation());
    }
}
```

## 📊 변경 사항 비교

### 이전 버전 (문제)
```csharp
Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
foreach (var r in renderers)
{
    if (currentSpinner != null && r.transform.IsChildOf(currentSpinner.transform)) continue;
    r.enabled = !show; // ❌ 모든 Renderer 제어
}
```

**문제점:**
- Pulse의 Renderer도 꺼짐
- 예상치 못한 다른 Renderer도 영향받음
- 디버깅 어려움

### 최종 버전 (해결)
```csharp
if (cubeRenderer != null)
{
    cubeRenderer.enabled = !show; // ✅ Cube Renderer만 제어
}
else
{
    // Fallback: Cube 자식 직접 찾기
    Transform cubeChild = transform.Find("Cube");
    if (cubeChild != null)
    {
        MeshRenderer meshRenderer = cubeChild.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = !show;
        }
    }
}
```

**장점:**
- ✅ Cube MeshRenderer만 정확하게 제어
- ✅ Pulse 등 다른 효과는 계속 표시
- ✅ GameObject는 활성 상태 유지 → Collider, 스크립트 모두 작동
- ✅ 명확한 디버그 로그

## 🔄 동작 흐름

### 1. 오브젝트 생성 시작
```
[DEBUG_DATA] CreateObjectFromData 호출: ID=219, Name=카페, model_type=cube
[DEBUG_SETUP] SetupObjectComponents 시작: ID=219
[DEBUG_CUBE] SetBaseMap 호출 시도: ID=219
```

### 2. 스피너 활성화 (로딩 시작)
```
[DEBUG_SPINNER] ShowSpinner(true) - cubeRenderer=True
[DEBUG_SPINNER] 스피너 생성 완료
[DEBUG_SPINNER] cubeRenderer.enabled = False (GameObject.active=True)
[DEBUG_SPINNER] 스피너 활성 상태 = True
```

**상태:**
- GameObject: ✅ 활성
- Cube MeshRenderer: ❌ 꺼짐 (시각적으로만 숨김)
- Pulse Renderer: ✅ 계속 작동 (맥박 효과 표시)
- Collider: ✅ 활성 (레이캐스트 가능)
- DoubleTap3D: ✅ 작동
- Target: ✅ 작동
- 스피너: ✅ 표시

### 3. 텍스처 로딩 (spinnerDuration 동안)
```
로딩 중... (백그라운드)
```

**사용자에게 보이는 것:**
- 로딩 스피너만 표시
- Pulse 맥박 효과 (선택적)
- Offscreen Indicator (화면 밖이면)

### 4. 로딩 완료 (spinnerDuration 후)
```
[DEBUG_SPINNER] 로딩 완료. 경과: 1.23s, 목표: 10s, 추가 대기: 8.77s
[DEBUG_SPINNER] 로딩 코루틴 종료 -> 스피너 끔 (finally)
[DEBUG_SPINNER] ShowSpinner(false) - cubeRenderer=True
[DEBUG_SPINNER] cubeRenderer.enabled = True (GameObject.active=True)
[DEBUG_SPINNER] 스피너 활성 상태 = False
[DEBUG_SPINNER] 팝업 애니메이션 시작
```

**상태:**
- GameObject: ✅ 활성
- Cube MeshRenderer: ✅ 켜짐 (텍스처 표시)
- Pulse Renderer: ✅ 계속 작동
- Collider: ✅ 활성
- 모든 컴포넌트: ✅ 정상 작동
- 스피너: ❌ 숨김

### 5. AR 환경에서 상호작용
```
[DoubleTap3D] 레이캐스트 히트 성공 - GameObject: Cube
[DoubleTap3D] 더블탭 감지 - ID: 219
```

## 🧪 테스트 가이드

### 빌드 및 실행
```bash
# 1. Unity 빌드
# 2. Android 설치
# 3. 앱 실행
```

### 로그 확인
```bash
adb logcat | grep -E "DEBUG_SPINNER|cubeRenderer"
```

### 예상 로그 (성공 시나리오)

#### 로딩 시작
```
ShowSpinner(true) - cubeRenderer=True
스피너 생성 완료
cubeRenderer.enabled = False (GameObject.active=True)  ← ✅ Renderer만 끔
스피너 활성 상태 = True
```

#### 로딩 완료
```
ShowSpinner(false) - cubeRenderer=True
cubeRenderer.enabled = True (GameObject.active=True)  ← ✅ Renderer 다시 켬
스피너 활성 상태 = False
팝업 애니메이션 시작
```

#### AR 환경 확인
- [ ] 로딩 중 스피너만 표시
- [ ] 로딩 후 큐브가 팝업 애니메이션과 함께 나타남
- [ ] 큐브가 AR 환경에 보임
- [ ] 큐브를 터치하면 반응함
- [ ] 더블탭으로 상세 정보 열림
- [ ] PlaceList와 Offscreen Indicator 정상 작동

### 문제 발생 시 체크

#### 큐브가 여전히 안 보이면
```bash
# cubeRenderer가 null인지 확인
adb logcat | grep "cubeRenderer=False"
```

→ **null이면**: Unity Inspector에서 cubeRenderer 할당 필요
→ **할당되어 있으면**: Fallback 로직이 작동하는지 확인

#### 스피너가 안 보이면
```bash
adb logcat | grep "스피너 생성"
```

→ **로그 없으면**: loadingSpinnerPrefab이 null
→ **Unity Inspector에서 prefab 할당 필요**

#### Renderer가 안 켜지면
```bash
adb logcat | grep "cubeRenderer.enabled"
```

→ **enabled = False로 계속 남아있으면**: ShowSpinner(false) 호출 안 됨
→ **LoadBaseMapTexture의 finally 블록 확인**

## 🎯 사용자 요구사항 최종 확인

### ✅ 로딩 중 자연스러운 딜레이
- spinnerDuration (기본 10초)으로 제어
- 텍스처 로딩과 무관하게 일정 시간 스피너 표시
- AR 환경 끊김 방지

### ✅ Offscreen Indicator + PlaceList 먼저 표시
- GameObject 활성 상태 유지 → Target 컴포넌트 접근 가능
- placeDataMap에 즉시 추가 → PlaceList 표시
- 로딩과 무관하게 UI 먼저 작동

### ✅ AR 오브젝트 정상 표시 및 터치
- MeshRenderer만 제어 → Collider 활성 유지
- 레이캐스트 히트 성공 → 터치 인식 가능
- DoubleTap3D, Target 등 모든 컴포넌트 정상 작동

### ✅ Pulse 등 다른 효과 유지
- cubeRenderer만 제어 → 다른 Renderer 영향 없음
- Pulse 맥박 효과 계속 작동 (선택적)

## 🔧 추가 개선 사항 (선택)

### spinnerDuration 조정
```csharp
// 현재: 10초
public float spinnerDuration = 10f;

// 제안: 3-5초로 단축
public float spinnerDuration = 3f;
```

### 로딩 중 터치 방지 (선택)
```csharp
// DoubleTap3D.cs - Update()
public bool IsLoading()
{
    ImageDisplayController display = GetComponent<ImageDisplayController>();
    return display != null && display.isLoadingTexture;
}

void Update()
{
    if (IsLoading()) return; // 로딩 중이면 터치 무시
    // ... 정상 터치 처리
}
```

### Pulse만 항상 표시 (선택)
현재는 cubeRenderer만 제어하므로 Pulse는 자동으로 계속 표시됩니다.

## 📝 체크리스트

- [x] ShowSpinner()를 cubeRenderer만 제어하도록 수정
- [x] Fallback 로직 추가 (cubeRenderer가 null일 때)
- [x] 상세 디버그 로그 추가
- [ ] Unity 빌드
- [ ] Android 테스트
- [ ] AR 환경에서 큐브 표시 확인
- [ ] 레이캐스트 히트 성공 확인
- [ ] PlaceList + Offscreen Indicator 동시 작동 확인

## 📚 관련 파일

- [ImageDisplayController.cs](c:\woopang\Assets\Scripts\Download\ImageDisplayController.cs) - 최종 수정
- [DataManager.cs](c:\woopang\Assets\Scripts\Download\DataManager.cs) - GetComponentInChildren(true) 추가
- [0000_Cube.prefab](c:\woopang\Assets\Scripts\Download\0000_Cube.prefab)

## 이전 시도 문서

- [FIX_AR_VISIBILITY_20251204.md](c:\woopang\FIX_AR_VISIBILITY_20251204.md) - GetComponentsInChildren 방식
- [FIX_CUBE_SPAWN_20251204.md](c:\woopang\FIX_CUBE_SPAWN_20251204.md) - includeInactive=true
- [ISSUE_FIX_20251204.md](c:\woopang\ISSUE_FIX_20251204.md) - PlaceList 수정

---

**작성일:** 2025-12-04
**수정 파일:** `Assets/Scripts/Download/ImageDisplayController.cs`
**핵심 변경:** `GetComponentsInChildren<Renderer>()` → `cubeRenderer` 직접 제어
**예상 효과:** AR 환경에서 큐브 정상 표시, 모든 상호작용 가능
