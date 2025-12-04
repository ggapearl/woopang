# 0000_Cube.prefab 오브젝트 생성 안 되는 문제 디버깅 가이드

**날짜:** 2025-12-04
**이슈:** 0000_Cube.prefab (DataManager, Woopang Data)로 생성되는 오브젝트만 AR 공간에 나타나지 않음
**정상 작동:** 0001_GLB.prefab, 0002_Cube_TourAPI.prefab은 정상 생성됨

---

## 🔍 문제 상황

### 증상
- **PlaceList**: 정상 표시됨 (데이터 수신 확인)
- **AR 오브젝트**:
  - ✅ 0001_GLB.prefab (custom, GLB 모델) - 정상 생성
  - ✅ 0002_Cube_TourAPI.prefab (TourAPIManager) - 정상 생성
  - ❌ **0000_Cube.prefab (cube, DataManager)** - **생성 안 됨**

### 파일 위치
- `Assets/Scripts/Download/0000_Cube.prefab` - DataManager용 큐브
- `Assets/Scripts/Download/0001_GLB.prefab` - DataManager용 GLB 모델
- `Assets/Scripts/Download/0002_Cube_TourAPI.prefab` - TourAPIManager용 큐브

---

## 🛠️ 디버깅 로그 추가 완료

### 추가된 디버그 태그

#### 1. **[DEBUG_POOL]** - 오브젝트 풀 관련
- 풀 초기화 (InitializeObjectPools)
- 풀에서 오브젝트 가져오기 (GetFromPool)
- 풀로 오브젝트 반환 (ReturnToPool)

#### 2. **[DEBUG_DATA]** - 데이터 처리 관련
- CreateObjectFromData 호출
- 오브젝트 활성화 전/후 상태
- SetupObjectComponents 결과
- spawnedObjects, placeDataMap 추가 성공/실패

#### 3. **[DEBUG_SETUP]** - 컴포넌트 설정 관련
- SetupObjectComponents 시작/완료
- GPS 앵커 설정
- 서브사진 설정
- model_type별 분기 처리

#### 4. **[DEBUG_CUBE]** - 큐브 설정 관련
- SetupCubeObject 시작/완료
- ImageDisplayController 설정
- DoubleTap3D 설정
- Target 설정
- 각 컴포넌트 존재 여부 체크

---

## 📋 로그캣 필터링 키워드

### 핵심 디버깅 키워드 (우선순위 순)

```bash
# 1. 풀 초기화 확인
adb logcat | grep "DEBUG_POOL"

# 2. Cube 오브젝트 생성 추적
adb logcat | grep "DEBUG_CUBE"

# 3. 데이터 처리 전체 흐름
adb logcat | grep "DEBUG_DATA"

# 4. 컴포넌트 설정 상세
adb logcat | grep "DEBUG_SETUP"

# 5. 전체 디버그 로그
adb logcat | grep -E "DEBUG_POOL|DEBUG_DATA|DEBUG_SETUP|DEBUG_CUBE"
```

### 문제별 필터링

#### ✅ 풀이 제대로 초기화되었는가?
```bash
adb logcat | grep -E "DEBUG_POOL.*풀 초기화|DEBUG_POOL.*초기화 완료"
```
**기대 출력:**
```
[DEBUG_POOL] 풀 초기화 시작 - cubePrefab: 0000_Cube, glbPrefab: 0001_GLB
[DEBUG_POOL] Cube 풀 초기화 완료: 50개
[DEBUG_POOL] GLB 풀 초기화 완료: 50개
```

#### ✅ Cube 풀에서 오브젝트를 가져오는가?
```bash
adb logcat | grep -E "DEBUG_POOL.*GetFromPool.*cube"
```
**기대 출력:**
```
[DEBUG_POOL] GetFromPool 호출: modelType=cube, poolName=Cube, 풀 크기: 50
[DEBUG_POOL] 풀에서 오브젝트 가져옴 (Dequeue 전): name=0000_Cube, active=False
[DEBUG_POOL] 풀에서 오브젝트 가져옴 (활성화 후): name=Place_ID_cube, active=True
```

#### ✅ CreateObjectFromData가 호출되는가?
```bash
adb logcat | grep -E "DEBUG_DATA.*CreateObjectFromData.*model_type=cube"
```
**기대 출력:**
```
[DEBUG_DATA] CreateObjectFromData 호출: ID=123, Name=카페이름, model_type=cube
[DEBUG_DATA] 오브젝트 활성화 전: name=Place_ID_cube, active=True
[DEBUG_DATA] 오브젝트 활성화 후: name=Place_123_cube, active=True
```

#### ✅ SetupObjectComponents가 성공하는가?
```bash
adb logcat | grep -E "DEBUG_DATA.*SetupObjectComponents.*결과"
```
**기대 출력:**
```
[DEBUG_DATA] SetupObjectComponents 결과: success=True, ID=123
[DEBUG_DATA] ✅ 오브젝트 생성 성공 - ID: 123, model_type: cube, spawnedObjects: 1, placeDataMap: 1
```

**실패 시 출력:**
```
[DEBUG_DATA] SetupObjectComponents 결과: success=False, ID=123
[DEBUG_DATA] ❌ SetupObjectComponents 실패 - 풀로 반환: ID=123
```

#### ❌ SetupCubeObject에서 실패하는가?
```bash
adb logcat | grep -E "DEBUG_CUBE"
```
**컴포넌트 누락 체크:**
```
[DEBUG_CUBE] ❌ DoubleTap3D 컴포넌트 없음: ID=123
[DEBUG_CUBE] ❌ Target 컴포넌트 없음: ID=123
[DEBUG_CUBE] ❌ ImageDisplayController 없음 또는 main_photo 없음: ID=123
```

---

## 🧪 테스트 시나리오

### 1단계: 풀 초기화 확인
```bash
adb logcat -c  # 로그 클리어
adb logcat | grep "DEBUG_POOL.*초기화"
```
**예상 결과:** Cube 풀 50개, GLB 풀 50개 생성

### 2단계: model_type 확인
```bash
adb logcat | grep -E "CreateObjectFromData.*model_type"
```
**체크 포인트:**
- `model_type=cube` 로그가 나오는가?
- `model_type=custom` (GLB) 로그만 나오고 cube는 없는가?

### 3단계: GetFromPool 호출 확인
```bash
adb logcat | grep -E "GetFromPool.*cube"
```
**체크 포인트:**
- Cube 풀에서 오브젝트를 가져오는가?
- 풀 크기가 줄어드는가? (50 → 49 → 48 ...)

### 4단계: SetupCubeObject 실패 원인 추적
```bash
adb logcat | grep -E "DEBUG_CUBE.*❌"
```
**가능한 실패 원인:**
1. **DoubleTap3D 컴포넌트 없음**
   - 0000_Cube.prefab에 DoubleTap3D 스크립트 누락
   - Cube 자식 오브젝트에 컴포넌트 없음

2. **Target 컴포넌트 없음**
   - 0000_Cube.prefab에 Target 스크립트 누락
   - Cube 자식 오브젝트에 컴포넌트 없음

3. **CustomARGeospatialCreatorAnchor 없음**
   - 루트 오브젝트에 앵커 컴포넌트 누락

### 5단계: 오브젝트 활성화 상태 확인
```bash
adb logcat | grep -E "DEBUG_DATA.*active="
```
**체크 포인트:**
- `active=True`로 변경되는가?
- 활성화 후에도 `active=False`로 남아있는가?

---

## 🔧 예상 원인 및 해결 방법

### 원인 1: 프리팹에 필수 컴포넌트 누락
**증상:**
```
[DEBUG_CUBE] ❌ DoubleTap3D 컴포넌트 없음: ID=123
[DEBUG_CUBE] ❌ Target 컴포넌트 없음: ID=123
```

**해결:**
1. Unity Editor에서 `0000_Cube.prefab` 열기
2. Cube 자식 오브젝트에 다음 컴포넌트 확인:
   - `DoubleTap3D.cs`
   - `Target.cs`
   - `ImageDisplayController.cs`
3. 없으면 추가

**비교 대상:** `0002_Cube_TourAPI.prefab` (정상 작동)

---

### 원인 2: WP_1201.unity 씬에서 cubePrefab 참조 오류
**증상:**
```
[DEBUG_POOL] 풀 초기화 시작 - cubePrefab: null
[DataManager] Prefab이 설정되지 않음!
```

**해결:**
1. Unity Editor에서 `WP_1201.unity` 씬 열기
2. `DataManager` GameObject 선택
3. Inspector에서 `Cube Prefab` 필드 확인
4. `Assets/Scripts/Download/0000_Cube.prefab` 할당되어 있는지 확인
5. 없거나 잘못되면 재할당

**현재 설정 (WP_1201.unity Line 15429):**
```yaml
cubePrefab: {fileID: 2389623711366131577}  # 이것이 0000_Cube를 가리켜야 함
```

---

### 원인 3: model_type이 "cube"가 아님
**증상:**
```
[DEBUG_DATA] CreateObjectFromData 호출: ID=123, Name=카페, model_type=custom
[DEBUG_DATA] GLB 로딩 제한 - cube로 fallback: ID=123
```

**가능성:**
- 서버에서 `model_type`이 잘못 전송됨
- `model_type`이 "custom"인데 `model_url`이 비어있어서 cube로 fallback
- fallback 과정에서 GLB 풀의 오브젝트를 사용하려고 시도

**확인:**
```bash
adb logcat | grep -E "model_type"
```

---

### 원인 4: GetComponentInChildren<> 실패
**증상:**
```
[DEBUG_CUBE] ❌ DoubleTap3D 컴포넌트 없음: ID=123
```

**가능성:**
- Cube 오브젝트가 비활성화 상태여서 `GetComponentInChildren<>` 실패
- 자식 오브젝트 구조가 0001/0002와 다름

**해결:**
0000_Cube.prefab의 계층 구조를 0002_Cube_TourAPI.prefab과 비교:
```
0000_Cube (root)
├─ Cube (MeshRenderer, DoubleTap3D, Target, ImageDisplayController)
├─ Pulse
└─ CustomARGeospatialCreatorAnchor
```

---

### 원인 5: ResetObjectState에서 컴포넌트 비활성화
**증상:**
```
[DEBUG_POOL] 풀에서 오브젝트 가져옴 (활성화 후): name=Place_ID_cube, active=True
[DEBUG_CUBE] ❌ DoubleTap3D 컴포넌트 없음
```

**가능성:**
- `ResetObjectState()` 메서드에서 컴포넌트를 비활성화하거나 제거
- Cube 전용 리셋 로직이 컴포넌트를 손상시킴

**확인:**
```csharp
// DataManager.cs - ResetObjectState 메서드 확인 필요
private void ResetObjectState(GameObject obj, string modelType)
{
    // 여기서 컴포넌트를 비활성화하거나 제거하는지 확인
}
```

---

## 📊 정상 작동 시 예상 로그 흐름

```
1. [DEBUG_POOL] 풀 초기화 시작 - cubePrefab: 0000_Cube, glbPrefab: 0001_GLB
2. [DEBUG_POOL] Cube 풀 초기화 완료: 50개
3. [DEBUG_POOL] GLB 풀 초기화 완료: 50개

... (서버 데이터 수신)

4. [DEBUG_DATA] CreateObjectFromData 호출: ID=123, Name=카페, model_type=cube
5. [DEBUG_POOL] GetFromPool 호출: modelType=cube, poolName=Cube, 풀 크기: 50
6. [DEBUG_POOL] 풀에서 오브젝트 가져옴 (Dequeue 전): name=0000_Cube, active=False
7. [DEBUG_POOL] 풀에서 오브젝트 가져옴 (활성화 후): name=Place_ID_cube, active=True
8. [DEBUG_DATA] 오브젝트 활성화 전: name=Place_ID_cube, active=True
9. [DEBUG_DATA] 오브젝트 활성화 후: name=Place_123_cube, active=True
10. [DEBUG_SETUP] SetupObjectComponents 시작: ID=123, model_type=cube
11. [DEBUG_SETUP] ✅ GPS 앵커 설정 완료: ID=123, Lat=37.422, Lon=126.931
12. [DEBUG_SETUP] SetupCubeObject 호출: ID=123
13. [DEBUG_CUBE] SetupCubeObject 시작: ID=123, obj.name=Place_123_cube
14. [DEBUG_CUBE] SetBaseMap 호출 시도: ID=123, URL=https://woopang.com/...
15. [DEBUG_CUBE] ✅ DoubleTap3D 설정 완료: ID=123
16. [DEBUG_CUBE] ✅ Target 설정 완료: ID=123
17. [DEBUG_CUBE] ✅ SetupCubeObject 성공: ID=123
18. [DEBUG_SETUP] SetupObjectComponents 완료: ID=123, result=True
19. [DEBUG_DATA] SetupObjectComponents 결과: success=True, ID=123
20. [DEBUG_DATA] ✅ 오브젝트 생성 성공 - ID: 123, model_type: cube, spawnedObjects: 1, placeDataMap: 1
```

---

## 🚨 실패 시 로그 패턴

### 패턴 1: 풀 초기화 실패
```
[DataManager] Prefab이 설정되지 않음!
```
→ **해결:** WP_1201.unity에서 cubePrefab 재할당

### 패턴 2: GetFromPool 호출 안 됨
```
[DEBUG_DATA] CreateObjectFromData 호출: ID=123, model_type=custom
```
→ **해결:** 서버 데이터의 model_type 확인

### 패턴 3: 컴포넌트 누락
```
[DEBUG_CUBE] ❌ DoubleTap3D 컴포넌트 없음: ID=123
[DEBUG_DATA] ❌ SetupObjectComponents 실패 - 풀로 반환: ID=123
```
→ **해결:** 0000_Cube.prefab에 컴포넌트 추가

### 패턴 4: 오브젝트 활성화 안 됨
```
[DEBUG_DATA] 오브젝트 활성화 후: name=Place_123_cube, active=False
```
→ **해결:** SetActive() 호출 타이밍 또는 부모 오브젝트 활성화 상태 확인

---

## ✅ 다음 단계

1. **Unity에서 빌드**
2. **Android 디바이스에 설치**
3. **adb logcat 실행:**
   ```bash
   adb logcat -c
   adb logcat | grep -E "DEBUG_POOL|DEBUG_DATA|DEBUG_SETUP|DEBUG_CUBE"
   ```
4. **앱 실행 후 로그 관찰**
5. **위의 "정상 작동 시 예상 로그 흐름"과 비교**
6. **실패 지점 확인 후 해당 원인 섹션 참고**

---

**작성일:** 2025-12-04
**수정된 파일:** `Assets/Scripts/Download/DataManager.cs`
**추가된 디버그 태그:** DEBUG_POOL, DEBUG_DATA, DEBUG_SETUP, DEBUG_CUBE
