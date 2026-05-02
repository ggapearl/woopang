# AR 오브젝트 스폰 / 백그라운드 복귀 로직 (필독)

iOS에서 백그라운드 복귀 시 풀 오브젝트(`0000_Cube.prefab` 등)가 화면에 안 나타나는 문제를 디버깅하면서 발견한 핵심 규칙들. **이 문서의 규칙을 어기면 즉시 "오브젝트가 발생할 때도 있고 안 할 때도 있다"는 들쭉날쭉한 버그가 발생함.**

---

## 1. 핵심 컴포넌트 구조

### 가시성 결정 흐름
```
GameObject.SetActive(true/false)         ← 1단계: 오브젝트 활성/비활성
  └─ Renderer.enabled = true/false       ← 2단계: 렌더러 켜기/끄기
       └─ _forceHideRenderers (bool)     ← fallback/BG 복구 중 외부 강제 숨김 플래그
```

**이 3개가 모두 만족되어야 화면에 보임:**
- `gameObject.activeInHierarchy == true`
- `Renderer.enabled == true`
- `_forceHideRenderers == false`

### 핵심 파일
| 파일 | 역할 |
|------|------|
| [CustomARGeospatialCreatorAnchor.cs](../../Assets/Scripts/Prefab/CustomARGeospatialCreatorAnchor.cs) | ARGeospatialAnchor 생성/유지/재시도. trackingState 감시. SetVisible/SetForceHideRenderers |
| [LoadingManager.cs](../../Assets/Scripts/UI/LoadingManager.cs) | 백그라운드 복귀 흐름 (`HandleBackgroundRecovery`). fallback 모드 ON/OFF |
| [FilterManager.cs](../../Assets/Scripts/UI/FilterManager.cs) | `AllocationLoop`(주기적) → `RetryFailedAnchors` 호출. 속도 기반 SlowdownRefresh |
| [DataManager.cs](../../Assets/Scripts/Download/DataManager.cs) + Tour/Subway/Terminal/TrainStation | `RetryFailedAnchorsIn`로 실패 앵커 선별 재시도. `SetAllRenderersVisible`(forceHide 플래그 토글) |
| [OffScreenIndicator.cs](../../Assets/Scripts/OffScreenIndicator/OffScreenIndicator.cs) | fallback 모드 (특징점 부족 시 화살표 분산). `IsFallbackMode` |

---

## 2. 절대 어기면 안 되는 규칙

### 2.1 `RecreateAnchor()`/`RetryFailedAnchorsIn` 가드
이미 정상 동작 중이거나 재시도 중인 앵커를 **다시 RecreateAnchor 하면 안 됨**.

```csharp
// 모든 RetryFailedAnchorsIn / CreateAllInitialAnchors 패턴에 필수
if (anchor.IsAnchorCreated) continue;   // 이미 생성됨 → 건드리지 않음
if (anchor.IsRetrying) continue;        // 재시도 코루틴 중 → 건드리지 않음
anchor.RecreateAnchor();
```

**왜:** `RecreateAnchor()`는 내부에서 기존 anchor를 Destroy하고 새로 시작 + `_hasBeenTracking=false` 리셋. 정상 동작 중인 걸 매 2초 tick마다 깨뜨리면 화면에서 깜빡이거나 영영 안 보임.

**해당 위치 (모두 동일 가드 필요):**
- [DataManager.cs](../../Assets/Scripts/Download/DataManager.cs) `RetryFailedAnchorsIn`, `CreateAllInitialAnchors`
- [SubwayManager.cs](../../Assets/Scripts/Download/SubwayManager.cs) `RetryFailedAnchorsIn`
- [TerminalManager.cs](../../Assets/Scripts/Download/TerminalManager.cs) `RetryFailedAnchorsIn`
- [TourAPIManager.cs](../../Assets/Scripts/Download/TourAPIManager.cs) `RetryFailedAnchorsIn`
- [TrainStationManager.cs](../../Assets/Scripts/Download/TrainStationManager.cs) `RetryFailedAnchorsIn`

---

### 2.2 백그라운드 복귀 — 경량/풀 복구 양쪽 모두 복원 로직 필요
[LoadingManager.HandleBackgroundRecovery](../../Assets/Scripts/UI/LoadingManager.cs)는 두 경로로 분기:

#### 경량 복구 (Tracking 유지)
```csharp
// 반드시 호출해야 함:
SetAllManagerRenderersVisible(true);   // forceHide 해제
RestoreAllManagerObjects();            // SetActive(true) + 거리/카테고리 필터
ForceRetryAllAnchors();                // 망가진 앵커만 선별 재시도
osi.EnableFallbackMode(false, ...);    // fallback 진행 중이었다면 해제
dataManager.RestartFetchingAfterResume();
```

**경량복구 경로에 위 4개가 빠지면 백그라운드 복귀 후 풀 오브젝트가 영영 안 보이는 버그 발생.** ("RestartFetchingAfterResume only" 안 됨!)

#### 풀 복구 (Tracking Lost)
```csharp
// 호출 순서가 매우 중요:
RestoreAllManagerObjects();            // 1. 먼저 SetActive(true)
SetAllManagerRenderersVisible(true);   // 2. 그 다음 Renderer 켜기
ForceRetryAllAnchors();                // 3. 앵커 재시도
```

**순서 바꾸면 안 됨.** `SetVisible(true)`를 먼저 호출하면 GameObject가 비활성 상태에서 Renderer만 켜는 의미 없는 호출이 됨 (`active=False` 상태로 로그 찍힘).

---

### 2.3 fallback 모드 OFF 시 forceHide 자동 해제 (워치독)
`OffScreenIndicator.EnableFallbackMode(false)`를 호출하는 곳이 LoadingManager에 17곳 이상 있고, 그 중 일부는 `SetAllManagerRenderersVisible(true)`를 빼먹기 쉽다.

**보호책:** [CustomARGeospatialCreatorAnchor.Update()](../../Assets/Scripts/Prefab/CustomARGeospatialCreatorAnchor.cs)에 워치독 존재 — `_forceHideRenderers=true`인데 OSI가 `IsFallbackMode==false`이면 자동 복원.

```csharp
if (_forceHideRenderers && _anchorCreated && _hasBeenTracking)
{
    var osi = GetOSI();
    if (osi != null && !osi.IsFallbackMode)
    {
        _forceHideRenderers = false;
        SetVisible(true);
    }
}
```

**이 워치독을 절대 제거하지 말 것.** 이게 모든 누락 케이스의 안전망.

---

### 2.4 SlowdownRefresh — 백그라운드 복귀 직후 차단
[FilterManager.TriggerSlowdownRefresh](../../Assets/Scripts/UI/FilterManager.cs)는 25km/h 이상 → 5km/h 미만 감속 시 `RestartFetchingAfterResume`을 한 번 호출.

**문제 시나리오:** 백그라운드 진입 중 GPS가 끊김 → 복귀 시 "고속에서 정지"로 잘못 감지 → 막 생성된 앵커와 충돌.

**필수 가드 (이미 적용됨):**
- 복귀 후 30초 이내 스킵: `loadingMgr.TimeSinceLastBackgroundRecovery < 30f`
- 복귀 시 GPS 샘플 큐/peak 속도 리셋: `LoadingManager.HandleBackgroundRecovery` 진입 시 `fm.ResetSpeedTracking()` 호출

---

## 3. 디버그 진단 패턴

`[BG-iOS-DBG]` 로그가 [CustomARGeospatialCreatorAnchor.cs](../../Assets/Scripts/Prefab/CustomARGeospatialCreatorAnchor.cs)에 깔려 있음. 가까운 오브젝트 (`Place_1`, `Place_665`, `Place_98` 등)만 추적하도록 `ShouldLog()` 필터링 적용됨 — 로그 폭주 방지.

### 정상 흐름 (로그 시퀀스)
```
OnEnable anchorCreated=False → SetVisible(False) → AddAnchor 성공
→ TrackingState 변경 None→Tracking → 최초 Tracking 도달 → SetVisible(True) renderers=N active=True
```

### 비정상 패턴 (이 패턴 보이면 즉시 디버깅)
- `SetVisible(True) ... active=False` ← GameObject 비활성 상태에서 Renderer만 켜기 (순서 버그)
- `forceHide=True`가 여러 사이클 지속 ← 워치독 미작동 또는 fallback OFF 누락
- `RecreateAnchor 호출 (anchorCreated=True, ...)` 반복 ← `IsAnchorCreated` 가드 누락
- `최초 Tracking 도달` 로그가 한 오브젝트에 여러 번 ← `_hasBeenTracking` 리셋 발생 (RecreateAnchor 중복)

---

## 4. 수정 시 체크리스트

AR 오브젝트 스폰/가시성 관련 코드를 수정할 때 반드시 확인:

- [ ] `RetryFailedAnchorsIn` / `CreateAllInitialAnchors` 패턴에서 `IsAnchorCreated` + `IsRetrying` 둘 다 체크?
- [ ] `RecreateAnchor()`를 호출하기 전에 위 두 플래그를 확인했는지?
- [ ] 백그라운드 복귀 경량/풀 양쪽 경로에서 `RestoreObjects → SetVisible(true) → RetryAnchors` 순서 보장?
- [ ] fallback OFF 호출 시 `SetAllManagerRenderersVisible(true)`가 함께 호출되는지? (또는 워치독에 의존)
- [ ] `_forceHideRenderers` 플래그를 외부에서 토글할 때 `_anchorCreated`/`_hasBeenTracking` 상태 함께 고려?
- [ ] 새로운 fallback 트리거 추가 시 `EnableFallbackMode(true)` 직후 `SetAllManagerRenderersVisible(false)` 호출?
- [ ] 새로운 SlowdownRefresh류 트리거 추가 시 BG 복귀 직후 차단 가드 포함?

---

## 5. 관련 커밋 (역사적 기록)

- `b715116` — `IsRetrying` 체크 추가 (RecreateAnchor 무한 루프 차단)
- `f0ae7f9` — `CreateAllInitialAnchors` 가드 + 폰트 Dynamic 원복
- `12b3d54` — SlowdownRefresh BG 복귀 직후 차단
- `cdcfe43` — 경량 BG 복구 경로에 SetVisible/RestoreObjects/RetryAnchors 추가
- `276feef` — forceHide 영구 박힘 방지 워치독
- `aa1122c` — 풀 복구 경로 SetActive/Renderer 호출 순서 수정

각 커밋의 메시지에 어떤 로그 패턴을 보고 어떻게 수정했는지 상세히 기록되어 있으니 회귀 발생 시 참고할 것.

---

*최종 업데이트: 2026-05-03*
