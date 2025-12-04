# 0000_Cube.prefab 오브젝트 생성 실패 문제 해결 (2025-12-04)

## 🔍 문제 증상

**정상 작동:**
- ✅ 0001_GLB.prefab (DataManager, custom GLB 모델) - AR 공간에 정상 생성
- ✅ 0002_Cube_TourAPI.prefab (TourAPIManager) - AR 공간에 정상 생성
- ✅ PlaceList UI - 데이터 정상 표시 (우팡=2, TourAPI=1)

**문제 발생:**
- ❌ 0000_Cube.prefab (DataManager, Woopang Data) - **AR 공간에 생성 안 됨**

---

## 🐛 근본 원인

### 로그 분석 결과

```
[DEBUG_DATA] CreateObjectFromData 호출: ID=132, Name=테스트, model_type=cube
[DEBUG_SETUP] SetupObjectComponents 시작: ID=132, model_type=cube
[DEBUG_SETUP] SetupCubeObject 호출: ID=132
[DEBUG_CUBE] SetupCubeObject 시작: ID=132, obj.name=Place_132_cube
[DEBUG_CUBE] ❌ DoubleTap3D 컴포넌트 없음: ID=132
```

### 문제 발견

**DataManager.cs의 SetupCubeObject() 메서드:**
```csharp
// 라인 510 (수정 전)
DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>();
```

**문제점:**
- `GetComponentInChildren<T>()` 메서드는 **기본적으로 비활성화된 자식 오브젝트를 검색하지 않음**
- 0000_Cube.prefab의 DoubleTap3D 컴포넌트는 "Cube" 자식 GameObject에 위치
- 풀에서 가져온 오브젝트가 비활성화 상태이거나 자식이 비활성화 상태일 때 컴포넌트를 찾을 수 없음

### 왜 GLB는 작동했는가?

0001_GLB.prefab의 경우:
- SetupGLBObject()는 DoubleTap3D, Target 등을 찾지 않음
- GLBModelLoader 컴포넌트만 사용하며, 이는 루트에 위치
- 따라서 비활성화 상태 영향을 받지 않음

### 왜 0002_Cube_TourAPI는 작동하는가?

TourAPIManager.cs의 경우:
- 별도의 코드로 오브젝트를 관리
- 풀 시스템을 사용하지 않거나 다른 방식으로 활성화
- 또는 TourAPI 큐브 프리팹의 구조가 다를 수 있음

---

## ✅ 해결 방법

### 수정 내용

**DataManager.cs - SetupCubeObject() 메서드:**

```csharp
// 수정 전 (라인 498, 510, 520)
ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>();
DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>();
Target target = obj.GetComponentInChildren<Target>();

// 수정 후
ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>(true); // includeInactive=true
DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>(true); // includeInactive=true
Target target = obj.GetComponentInChildren<Target>(true); // includeInactive=true
```

**DataManager.cs - SetupObjectComponents() 메서드:**

```csharp
// 수정 전 (라인 443, 453)
CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>();
ImageDisplayController displayCtrl = obj.GetComponentInChildren<ImageDisplayController>();

// 수정 후
CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true); // includeInactive=true
ImageDisplayController displayCtrl = obj.GetComponentInChildren<ImageDisplayController>(true); // includeInactive=true
```

### 핵심 변경사항

**`GetComponentInChildren<T>()` → `GetComponentInChildren<T>(true)`**

- **매개변수 `true`**: `includeInactive` 플래그 활성화
- **효과**: 비활성화된 자식 GameObject에서도 컴포넌트 검색 가능

---

## 📊 예상 결과

### Before (수정 전)

```
[DEBUG_CUBE] SetupCubeObject 시작: ID=132, obj.name=Place_132_cube
[DEBUG_CUBE] ❌ DoubleTap3D 컴포넌트 없음: ID=132
[DEBUG_DATA] ❌ SetupObjectComponents 실패 - 풀로 반환: ID=132
```

**결과:**
- 오브젝트 생성 실패
- 풀로 반환됨
- AR 공간에 나타나지 않음

### After (수정 후, 예상)

```
[DEBUG_CUBE] SetupCubeObject 시작: ID=132, obj.name=Place_132_cube
[DEBUG_CUBE] SetBaseMap 호출 시도: ID=132, URL=https://woopang.com/...
[DEBUG_CUBE] ✅ DoubleTap3D 설정 완료: ID=132
[DEBUG_CUBE] ✅ Target 설정 완료: ID=132
[DEBUG_CUBE] ✅ SetupCubeObject 성공: ID=132
[DEBUG_SETUP] SetupObjectComponents 완료: ID=132, result=True
[DEBUG_DATA] ✅ 오브젝트 생성 성공 - ID: 132, model_type: cube, spawnedObjects: 1, placeDataMap: 1
```

**결과:**
- 오브젝트 생성 성공
- spawnedObjects, placeDataMap에 추가
- AR 공간에 정상 표시

---

## 🔧 테스트 방법

### 1. Unity 빌드
```bash
# Unity에서 WP_1201 씬 빌드
File → Build Settings → Build
```

### 2. Android 디바이스 설치 및 실행

### 3. adb logcat 모니터링
```bash
adb logcat -c
adb logcat | grep -E "DEBUG_CUBE|DEBUG_DATA|DEBUG_SETUP"
```

### 4. 확인 사항

#### ✅ 성공 로그 패턴
```
[DEBUG_CUBE] ✅ DoubleTap3D 설정 완료: ID=X
[DEBUG_CUBE] ✅ Target 설정 완료: ID=X
[DEBUG_CUBE] ✅ SetupCubeObject 성공: ID=X
[DEBUG_DATA] ✅ 오브젝트 생성 성공 - ID: X, model_type: cube
```

#### ❌ 실패 로그 패턴 (더 이상 나타나지 않아야 함)
```
[DEBUG_CUBE] ❌ DoubleTap3D 컴포넌트 없음: ID=X
[DEBUG_CUBE] ❌ Target 컴포넌트 없음: ID=X
[DEBUG_DATA] ❌ SetupObjectComponents 실패 - 풀로 반환: ID=X
```

### 5. AR 공간 확인
- 앱 실행 후 AR 세션 시작
- 0000_Cube.prefab 오브젝트들이 AR 공간에 표시되는지 확인
- PlaceList에서 해당 장소 선택 시 큐브가 보이는지 확인

---

## 🧩 기술적 배경

### Unity GetComponentInChildren 동작 방식

#### 기본 동작 (includeInactive=false, 기본값)
```csharp
GameObject parent = ...;
DoubleTap3D component = parent.GetComponentInChildren<DoubleTap3D>();
// ❌ 비활성화된 자식 GameObject는 검색하지 않음
```

#### includeInactive=true 사용
```csharp
GameObject parent = ...;
DoubleTap3D component = parent.GetComponentInChildren<DoubleTap3D>(true);
// ✅ 비활성화된 자식 GameObject도 검색함
```

### 오브젝트 풀링 시스템과의 관계

**GetFromPool() 메서드 흐름:**
```csharp
1. Queue에서 Dequeue (비활성화 상태)
2. ResetObjectState() 호출 (여전히 비활성화 상태)
3. obj.SetActive(true) 호출 (활성화)
4. 반환
```

**CreateObjectFromData() 메서드 흐름:**
```csharp
1. GetFromPool() 호출 (활성화된 오브젝트 반환)
2. obj.SetActive(true) 재호출
3. SetupObjectComponents() 호출
   └─ GetComponentInChildren<T>() 사용
      ❌ 자식이 비활성화 상태면 실패!
```

### 왜 자식이 비활성화될 수 있는가?

1. **프리팹 저장 상태**: Unity 에디터에서 프리팹 저장 시 일부 자식이 비활성화 상태로 저장될 수 있음
2. **ResetObjectState()**: 풀 초기화 시 일부 컴포넌트를 비활성화할 수 있음
3. **부모 활성화 타이밍**: 부모가 활성화되어도 자식은 즉시 활성화되지 않을 수 있음

---

## 📝 체크리스트

- [x] 문제 원인 파악 (GetComponentInChildren의 includeInactive 누락)
- [x] DataManager.cs 수정 (5곳에 `true` 파라미터 추가)
  - [x] SetupCubeObject - ImageDisplayController
  - [x] SetupCubeObject - DoubleTap3D
  - [x] SetupCubeObject - Target
  - [x] SetupObjectComponents - CustomARGeospatialCreatorAnchor
  - [x] SetupObjectComponents - ImageDisplayController
- [ ] Unity 빌드
- [ ] Android 디바이스 테스트
- [ ] 로그 확인 (DEBUG_CUBE 에러 없어야 함)
- [ ] AR 공간에서 0000_Cube 오브젝트 표시 확인
- [ ] PlaceList와 AR 오브젝트 연동 확인

---

## 🚨 주의사항

### 다른 GetComponentInChildren 호출도 확인 필요

**프로젝트 전체에서 검색:**
```bash
grep -r "GetComponentInChildren<" "Assets/Scripts/"
```

**비활성화된 오브젝트를 다룰 가능성이 있는 경우:**
- 오브젝트 풀링 시스템
- Instantiate 직후
- 동적으로 생성되는 UI
- 프리팹 초기화 단계

**이런 경우 `includeInactive=true` 추가를 고려:**
```csharp
component = obj.GetComponentInChildren<T>(true);
```

---

## 📚 참고 자료

### Unity API 문서
- [Component.GetComponentInChildren](https://docs.unity3d.com/ScriptReference/Component.GetComponentInChildren.html)
- Parameter: `includeInactive` (bool, default: false)

### 관련 파일
- [DataManager.cs](c:\woopang\Assets\Scripts\Download\DataManager.cs) - 수정됨
- [0000_Cube.prefab](c:\woopang\Assets\Scripts\Download\0000_Cube.prefab) - 문제의 프리팹
- [0001_GLB.prefab](c:\woopang\Assets\Scripts\Download\0001_GLB.prefab) - 정상 작동 참고
- [0002_Cube_TourAPI.prefab](c:\woopang\Assets\Scripts\Download\0002_Cube_TourAPI.prefab) - 정상 작동 참고

### 이전 문서
- [DEBUG_CUBE_ISSUE.md](c:\woopang\DEBUG_CUBE_ISSUE.md) - 디버깅 가이드
- [ISSUE_FIX_20251204.md](c:\woopang\ISSUE_FIX_20251204.md) - PlaceList 수정

---

**작성일:** 2025-12-04
**수정 파일:** `Assets/Scripts/Download/DataManager.cs`
**수정 내용:** `GetComponentInChildren` 호출 5곳에 `includeInactive=true` 파라미터 추가
**예상 효과:** 0000_Cube.prefab 오브젝트가 AR 공간에 정상 생성됨
