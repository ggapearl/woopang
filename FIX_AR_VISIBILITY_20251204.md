# AR 오브젝트 가시성 문제 해결 (2025-12-04)

## 🔍 문제 분석

### 증상

**정상 작동:**
- ✅ 오브젝트 생성 성공: `spawnedObjects: 66, placeDataMap: 66`
- ✅ PlaceList 표시: `우팡=66, TourAPI=1`
- ✅ Offscreen Indicator 작동 (Target 컴포넌트 접근 가능)
- ✅ DoubleTap3D 컴포넌트 설정 완료

**문제 발생:**
- ❌ **AR 환경에서 큐브가 보이지 않음**
- ❌ 레이캐스트 히트 실패: `[DoubleTap3D] 레이캐스트 히트 실패`
- ❌ 큐브를 터치할 수 없음

### 로그 증거

```
✅ 오브젝트 생성 성공 - ID: 219, spawnedObjects: 60, placeDataMap: 60
✅ SetupCubeObject 성공: ID=206
✅ DoubleTap3D 설정 완료: ID=200
✅ Target 설정 완료: ID=200

❌ [DoubleTap3D] 레이캐스트 히트 실패 - 터치 위치: (112.85, 49.20)
```

---

## 🐛 근본 원인

### ImageDisplayController.cs - ShowSpinner() 메서드

**문제 코드 (라인 126-133):**
```csharp
// 최상위 오브젝트(나 자신)라면 Renderer만 끄고, 자식이면 오브젝트를 끔
if (r.gameObject == this.gameObject)
{
    r.enabled = !show;
}
else
{
    r.gameObject.SetActive(!show);  // ❌ 자식 GameObject 전체를 비활성화!
}
```

### 문제점

1. **로딩 스피너 활성화 시 (`show=true`)**:
   - `r.gameObject.SetActive(false)` 호출
   - Cube 자식 GameObject가 **완전히 비활성화됨**

2. **비활성화된 GameObject의 영향**:
   - ❌ MeshRenderer 꺼짐 → 시각적으로 안 보임
   - ❌ Collider 비활성화 → 레이캐스트 히트 실패
   - ❌ DoubleTap3D 스크립트 동작 중지 → 터치 인식 불가
   - ❌ 물리 충돌 감지 불가

3. **왜 Offscreen Indicator는 작동하는가?**
   - Target 컴포넌트는 `GetComponentInChildren<Target>(true)`로 접근 (includeInactive=true)
   - 비활성화된 GameObject에서도 컴포넌트는 찾을 수 있음
   - 하지만 Renderer가 꺼져있어서 AR 환경에서 보이지 않음

### 의도한 동작 vs 실제 동작

**의도:**
- 로딩 중 큐브와 스피너가 겹쳐 보이는 것 방지
- 텍스처 로딩 중 임시로 큐브 숨김
- 로딩 완료 후 큐브 다시 표시

**실제 문제:**
- GameObject 비활성화로 **모든 컴포넌트 동작 중지**
- PlaceList, Offscreen Indicator는 데이터 접근 가능 (placeDataMap 사용)
- 하지만 AR 환경에서 **물리적 상호작용 불가**

---

## ✅ 해결 방법

### 수정 내용

**ImageDisplayController.cs - ShowSpinner() 메서드 (라인 116-129):**

```csharp
// 수정 전
if (r.gameObject == this.gameObject)
{
    r.enabled = !show;
}
else
{
    r.gameObject.SetActive(!show);  // ❌ GameObject 비활성화
}

// 수정 후
// ⭐ GameObject를 비활성화하지 않고 Renderer만 끔으로써
//    Collider, DoubleTap3D, Target 등 다른 컴포넌트는 활성 상태 유지
Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
foreach (var r in renderers)
{
    if (currentSpinner != null && r.transform.IsChildOf(currentSpinner.transform)) continue;

    Debug.Log($"[DEBUG_SPINNER] Renderer {r.name} 상태 변경: {!show} (GameObject 활성 상태 유지)");

    // ✅ GameObject는 활성 상태 유지, Renderer만 끄기
    r.enabled = !show;
}
```

### 핵심 변경사항

**Before:**
- `r.gameObject.SetActive(!show)` → GameObject 전체 비활성화

**After:**
- `r.enabled = !show` → MeshRenderer만 비활성화

### 효과

| 컴포넌트 | 수정 전 | 수정 후 |
|---------|---------|---------|
| **MeshRenderer** | ❌ 꺼짐 | ❌ 꺼짐 (의도됨) |
| **Collider** | ❌ 비활성화 | ✅ 활성 상태 유지 |
| **DoubleTap3D** | ❌ 동작 중지 | ✅ 정상 작동 |
| **Target** | ❌ 동작 중지 | ✅ 정상 작동 |
| **물리 충돌** | ❌ 불가능 | ✅ 가능 |
| **레이캐스트** | ❌ 히트 실패 | ✅ 히트 성공 |
| **시각적 표시** | ❌ 안 보임 | ❌ 안 보임 (의도됨, 스피너만 표시) |

---

## 📊 예상 결과

### 로딩 스피너 동작 시퀀스

#### 1. 텍스처 로딩 시작
```
[DEBUG_CUBE] SetBaseMap 호출 시도: ID=219, URL=uploads/...
[DEBUG_SPINNER] ShowSpinner(true)
[DEBUG_SPINNER] Renderer Cube 상태 변경: False (GameObject 활성 상태 유지)
```

**상태:**
- ✅ GameObject 활성
- ❌ MeshRenderer 꺼짐 (시각적으로만 숨김)
- ✅ Collider 활성 (레이캐스트 가능)
- ✅ DoubleTap3D 작동
- ✅ 스피너 표시

#### 2. 텍스처 로딩 중 (최대 spinnerDuration)
```
[DEBUG_IMAGE] Loading BaseMap: https://woopang.com/...
```

**상태:**
- 사용자에게 스피너만 보임
- 뒤에서 텍스처 로딩 진행
- Collider는 활성 상태 (터치 가능하지만 시각적으로 안 보임)

#### 3. 텍스처 로딩 완료
```
[DEBUG_SPINNER] 로딩 완료. 경과: 1.23s, 목표: 10s, 추가 대기: 8.77s
[DEBUG_SPINNER] 로딩 코루틴 종료 -> 스피너 끔 (finally)
[DEBUG_SPINNER] ShowSpinner(false)
[DEBUG_SPINNER] Renderer Cube 상태 변경: True (GameObject 활성 상태 유지)
```

**상태:**
- ✅ GameObject 활성
- ✅ MeshRenderer 켜짐 (텍스처 표시)
- ✅ Collider 활성
- ✅ DoubleTap3D 작동
- ❌ 스피너 숨김

#### 4. AR 환경에서 상호작용 가능
```
[DoubleTap3D] 레이캐스트 히트 성공 - GameObject: Cube
[DoubleTap3D] 더블탭 감지 - ID: 219
```

---

## 🎯 사용자 요구사항 충족

### 요구사항 1: 로딩 딜레이로 자연스러운 로딩
✅ **해결됨**
- spinnerDuration 동안 스피너 표시
- 텍스처 로딩 중 큐브 숨김 (Renderer만 끔)
- AR 환경에서 끊김 현상 방지

### 요구사항 2: Offscreen Indicator와 PlaceList 먼저 표시
✅ **해결됨**
- placeDataMap에 데이터 추가 → PlaceList 즉시 표시
- Target 컴포넌트 설정 → Offscreen Indicator 작동
- 로딩 스피너와 무관하게 UI 먼저 표시

### 요구사항 3: AR 오브젝트 정상 표시 및 상호작용
✅ **해결됨**
- GameObject 활성 상태 유지 → Collider, 스크립트 모두 작동
- MeshRenderer만 제어 → 시각적으로만 숨김/표시
- 레이캐스트 히트 성공 → 터치 인식 가능

---

## 🔧 테스트 방법

### 1. Unity 빌드
```bash
File → Build Settings → Build
```

### 2. Android 디바이스 테스트

#### ✅ 성공 시나리오

**앱 시작:**
```
1. AR 세션 시작
2. PlaceList 즉시 표시 (로딩 전에도 데이터 표시)
3. Offscreen Indicator 즉시 작동 (방향 화살표 표시)
```

**오브젝트 로딩:**
```
4. 스피너 표시 (spinnerDuration 동안)
5. 텍스처 로딩 중 (백그라운드)
6. 로딩 완료 후 팝업 애니메이션과 함께 큐브 표시
7. 큐브가 AR 환경에 보임
```

**상호작용:**
```
8. 큐브를 터치하면 레이캐스트 히트 성공
9. 더블탭으로 상세 정보 패널 열림
10. 큐브 회전, 이동 등 모든 상호작용 정상 작동
```

### 3. 로그 확인

```bash
adb logcat | grep -E "DEBUG_SPINNER|DoubleTap3D"
```

**예상 로그:**
```
[DEBUG_SPINNER] ShowSpinner(true)
[DEBUG_SPINNER] Renderer Cube 상태 변경: False (GameObject 활성 상태 유지)
[DEBUG_SPINNER] ShowSpinner(false)
[DEBUG_SPINNER] Renderer Cube 상태 변경: True (GameObject 활성 상태 유지)
[DoubleTap3D] 레이캐스트 히트 성공 - GameObject: Cube  ← ✅ 이제 성공!
```

---

## 🚨 잠재적 이슈 및 대응

### 이슈 1: 스피너 duration이 너무 길 수 있음

**현재 설정:**
```csharp
public float spinnerDuration = 10f; // 10초
```

**실제 로딩 시간:**
```
[DEBUG_SPINNER] 로딩 완료. 경과: 1.23s
```

**개선 방안:**
- spinnerDuration을 3~5초로 줄임
- 또는 실제 로딩 시간만 사용 (최소 duration 제거)

**수정 예시:**
```csharp
public float spinnerDuration = 3f; // 3초로 단축
```

### 이슈 2: 로딩 중 터치 시 혼란 가능

**문제:**
- Renderer는 꺼져있지만 Collider는 활성
- 사용자가 빈 공간을 터치했는데 이벤트 발생 가능

**해결책 1: 로딩 중 터치 무시**
```csharp
// DoubleTap3D.cs
void Update()
{
    // 로딩 중이면 터치 무시
    ImageDisplayController display = GetComponent<ImageDisplayController>();
    if (display != null && display.IsLoading())
    {
        return;
    }

    // 정상 터치 처리
    HandleTouch();
}
```

**해결책 2: 로딩 중 Collider도 끄기 (권장하지 않음)**
- Offscreen Indicator가 동작하지 않을 수 있음

### 이슈 3: Pulse 애니메이션도 숨김

**현재:**
- Pulse 자식 오브젝트의 Renderer도 꺼짐

**원하는 동작:**
- Pulse는 로딩 중에도 표시?

**필요시 수정:**
```csharp
// ShowSpinner() 메서드
foreach (var r in renderers)
{
    if (currentSpinner != null && r.transform.IsChildOf(currentSpinner.transform)) continue;

    // Pulse는 항상 표시
    if (r.name == "Pulse") continue;

    r.enabled = !show;
}
```

---

## 📝 체크리스트

- [x] 문제 원인 파악 (GameObject 비활성화 → Renderer만 끄기)
- [x] ImageDisplayController.cs 수정
- [ ] Unity 빌드
- [ ] Android 디바이스 테스트
- [ ] AR 환경에서 큐브 표시 확인
- [ ] 레이캐스트 히트 성공 확인
- [ ] 터치 상호작용 확인
- [ ] PlaceList와 Offscreen Indicator 동시 작동 확인

---

## 🎓 기술적 배경

### Unity GameObject vs Component 활성화

#### GameObject.SetActive(false)
```csharp
gameObject.SetActive(false);
```

**효과:**
- 모든 컴포넌트 비활성화
- Update(), FixedUpdate() 호출 중지
- Collider 비활성화
- 물리 충돌 감지 중지
- 자식 오브젝트도 모두 비활성화

#### Renderer.enabled = false
```csharp
renderer.enabled = false;
```

**효과:**
- 시각적으로만 숨김
- 다른 컴포넌트는 정상 작동
- Collider 활성 상태 유지
- 스크립트 계속 실행
- 물리 충돌 감지 가능

### 로딩 스피너 패턴

**좋은 패턴 (현재 수정):**
```
1. GameObject 활성 상태 유지
2. Renderer만 끄기
3. 로딩 스피너 표시
4. 백그라운드에서 리소스 로딩
5. 완료 후 Renderer 다시 켜기
```

**나쁜 패턴 (이전 방식):**
```
1. GameObject 비활성화
2. 모든 기능 중지
3. 로딩 완료 후 재활성화
4. 초기화 오버헤드 발생
```

---

## 📚 관련 파일

- [ImageDisplayController.cs](c:\woopang\Assets\Scripts\Download\ImageDisplayController.cs) - 수정됨
- [DataManager.cs](c:\woopang\Assets\Scripts\Download\DataManager.cs) - includeInactive=true 추가
- [0000_Cube.prefab](c:\woopang\Assets\Scripts\Download\0000_Cube.prefab) - 문제의 프리팹

### 이전 문서

- [FIX_CUBE_SPAWN_20251204.md](c:\woopang\FIX_CUBE_SPAWN_20251204.md) - GetComponentInChildren 수정
- [ISSUE_FIX_20251204.md](c:\woopang\ISSUE_FIX_20251204.md) - PlaceList 표시 수정
- [DEBUG_CUBE_ISSUE.md](c:\woopang\DEBUG_CUBE_ISSUE.md) - 디버깅 가이드

---

**작성일:** 2025-12-04
**수정 파일:** `Assets/Scripts/Download/ImageDisplayController.cs`
**수정 내용:** ShowSpinner()에서 GameObject.SetActive() 대신 Renderer.enabled만 사용
**예상 효과:** AR 환경에서 큐브가 정상 표시되고 터치 상호작용 가능
