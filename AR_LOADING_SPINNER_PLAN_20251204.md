# AR 로딩 스피너 최종 구현 계획 (2025-12-04)

## 🎯 목표

**핵심 요구사항:**
- 오브젝트 생성 전 로딩 스피너를 통해 **준비 시간 확보**
- AR 환경에서 **사진 로딩 과부하 방지**
- 디바이스 내에서 가볍게 딜레이 제공
- PlaceList와 Offscreen Indicator는 먼저 표시

## 🔍 현재 상황 분석 (로그 기반)

### ✅ 정상 작동하는 부분

```
[DEBUG_DATA] ✅ 오브젝트 생성 성공 - ID: 206, spawnedObjects: 66, placeDataMap: 66
[DEBUG_CUBE] SetBaseMap 호출 시도: ID=200
[DEBUG_CUBE] ✅ SetupCubeObject 성공: ID=200
```

**결론:**
- ✅ 오브젝트 생성 로직 완벽 작동 (66개 생성)
- ✅ DataManager.cs 디버그 로그 정상
- ✅ SetBaseMap() 호출 성공
- ✅ PlaceList 정상 표시 (우팡=66, TourAPI=1)

### ❌ 문제점

**로그에 스피너 관련 내용이 전혀 없음:**
```bash
# 검색 결과: 스피너 로그 0개
adb logcat -d | grep -i "spinner"
```

**원인 발견:**
```yaml
# 0000_Cube.prefab - ImageDisplayController
cubeRenderer: {fileID: 8025986680963324023}  # ✅ 할당됨
doubleTap3DScript: {fileID: 146804131848832087}  # ✅ 할당됨
loadingSpinnerPrefab: ???  # ❌ 할당 안 됨
spinnerDuration: ???  # ❌ 설정 안 됨

# 0002_Cube_TourAPI.prefab - ImageDisplayController (정상)
loadingSpinnerPrefab: {fileID: 812358606491578410, guid: e5cd5b569ba59624793d7fec55949790}  # ✅
spinnerDuration: 2  # ✅
```

**핵심 문제:**
→ **0000_Cube.prefab의 ImageDisplayController에 loadingSpinnerPrefab이 할당되지 않음**

## 📋 해결 계획

### Phase 1: 프리팹 설정 수정 (Unity Editor)

#### 1.1 LoadingSpinner Prefab 확인
```
Assets/Prefabs/LoadingSpinner.prefab 존재 여부 확인
또는 0002_Cube_TourAPI.prefab이 참조하는 프리팹 위치 확인
```

#### 1.2 0000_Cube.prefab 수정
```
1. Unity Editor에서 0000_Cube.prefab 열기
2. 루트 GameObject 선택 (0000_Cube)
3. Inspector에서 ImageDisplayController 컴포넌트 찾기
4. 다음 필드 할당:
   - Loading Spinner Prefab: LoadingSpinner 프리팹 드래그 앤 드롭
   - Spinner Duration: 3 (기본값 10초는 너무 길음)
5. Prefab 저장
```

#### 1.3 값 제안
```csharp
spinnerDuration: 3  // 3초 (적절한 딜레이)
// 0002는 2초, 우리는 3초로 약간 여유 있게
```

**이유:**
- 10초는 너무 길어서 사용자 경험 저하
- 3초면 충분히 사진 로딩 준비 시간 확보
- 0002_Cube_TourAPI.prefab의 2초도 참고

### Phase 2: 코드 동작 확인

#### 2.1 ImageDisplayController.cs 로직 검증

**현재 코드 (간소화 완료):**
```csharp
public void SetBaseMap(string imageUrl)
{
    if (!enabled) return;
    ShowSpinner(true);  // 스피너 활성화
    StartCoroutine(LoadBaseMapTexture(imageUrl));
}

private void ShowSpinner(bool show)
{
    // 스피너 생성
    if (show && currentSpinner == null && loadingSpinnerPrefab != null)
    {
        currentSpinner = Instantiate(loadingSpinnerPrefab, transform);
        currentSpinner.transform.localPosition = Vector3.zero;
    }

    // cubeRenderer 제어
    if (cubeRenderer != null)
    {
        cubeRenderer.enabled = !show;  // 로딩 중엔 큐브 숨김
    }

    // 스피너 표시
    if (currentSpinner != null)
    {
        currentSpinner.SetActive(show);
    }

    // 로딩 완료 시 팝업 애니메이션
    if (!show)
    {
        StartCoroutine(PopUpAnimation());
    }
}
```

**동작 흐름:**
```
1. SetBaseMap() 호출
2. ShowSpinner(true) → 스피너 생성 및 표시, 큐브 숨김
3. LoadBaseMapTexture() 시작
4. spinnerDuration(3초) 동안 대기
5. 텍스처 로딩 완료
6. finally 블록에서 ShowSpinner(false) → 스피너 숨김, 큐브 표시
7. PopUpAnimation() → 큐브 등장 애니메이션
```

#### 2.2 타이밍 분석

**목표 타이밍:**
```
T=0s:   오브젝트 생성 시작
T=0s:   PlaceList 업데이트 (즉시)
T=0s:   Offscreen Indicator 활성화 (즉시)
T=0s:   SetBaseMap() 호출 → ShowSpinner(true)
T=0s:   스피너 표시 시작
T=0~3s: 스피너 표시 (사용자는 로딩 중임을 인지)
T=0~3s: 백그라운드에서 텍스처 로딩
T=3s:   ShowSpinner(false)
T=3s:   큐브 팝업 애니메이션 (0.4초)
T=3.4s: 큐브 완전히 표시, 상호작용 가능
```

**현재 문제:**
```
T=0s:   오브젝트 생성 시작
T=0s:   PlaceList 업데이트 ✅
T=0s:   SetBaseMap() 호출 ✅
T=0s:   ShowSpinner(true) 호출 ✅
T=0s:   ❌ loadingSpinnerPrefab == null → 아무 일도 안 일어남
T=0~?s: 텍스처 로딩 (백그라운드) ✅
T=?s:   ShowSpinner(false) 호출 (하지만 스피너 없음)
T=?s:   PopUpAnimation() 시작하지만 사용자에게 보이지 않음
```

### Phase 3: 테스트 및 검증

#### 3.1 빌드 전 Unity Editor 체크리스트

```
□ 0000_Cube.prefab 열기
□ ImageDisplayController 컴포넌트 확인
□ loadingSpinnerPrefab 할당 확인 (null이 아님)
□ spinnerDuration = 3 확인
□ cubeRenderer 할당 확인 (Cube 자식의 MeshRenderer)
□ doubleTap3DScript 할당 확인
□ Prefab 저장
```

#### 3.2 빌드 후 로그 확인

**예상 로그 (성공 시나리오):**
```
[DEBUG_CUBE] SetBaseMap 호출 시도: ID=200, URL=uploads/...
[ImageDisplayController] ShowSpinner(true)
[ImageDisplayController] 스피너 생성 완료
[ImageDisplayController] cubeRenderer.enabled = False
[ImageDisplayController] 스피너 활성 상태 = True
... (3초 대기)
[ImageDisplayController] ShowSpinner(false)
[ImageDisplayController] cubeRenderer.enabled = True
[ImageDisplayController] 스피너 활성 상태 = False
[ImageDisplayController] 팝업 애니메이션 시작
```

**로그캣 명령어:**
```bash
# 전체 흐름 확인
adb logcat | grep -E "DEBUG_CUBE|ImageDisplayController"

# 스피너만 확인
adb logcat | grep -i "spinner"

# SetBaseMap 호출 확인
adb logcat | grep "SetBaseMap"
```

#### 3.3 AR 환경 확인

```
□ 앱 시작 후 AR 세션 초기화
□ PlaceList에 데이터 표시 확인
□ Offscreen Indicator 작동 확인
□ 오브젝트 생성 시 스피너 표시 확인 (AR 공간에서)
□ 3초 후 스피너 사라지고 큐브 팝업 애니메이션 확인
□ 큐브를 터치해서 상호작용 확인
□ 더블탭으로 상세 정보 열림 확인
```

## 🔧 대안 계획 (Plan B)

### 만약 LoadingSpinner Prefab을 찾을 수 없다면

#### Option 1: 간단한 스피너 프리팹 생성

```
1. Unity에서 GameObject → 3D Object → Sphere 생성
2. Sphere에 회전 애니메이션 추가
3. Material 설정 (투명 배경 + 밝은 색상)
4. Prefab으로 저장: Assets/Prefabs/SimpleLoadingSpinner.prefab
5. 0000_Cube.prefab에 할당
```

#### Option 2: 0002_Cube_TourAPI.prefab 기반 복사

```
1. 0002_Cube_TourAPI.prefab의 ImageDisplayController 설정 복사
2. 0000_Cube.prefab에 적용
3. 또는 아예 0002_Cube_TourAPI.prefab을 DataManager의 cubePrefab으로 사용
```

#### Option 3: 스피너 없이 딜레이만 적용

```csharp
// ImageDisplayController.cs
private void ShowSpinner(bool show)
{
    // 스피너 없이 cubeRenderer만 제어
    if (cubeRenderer != null)
    {
        cubeRenderer.enabled = !show;
    }

    if (!show)
    {
        StartCoroutine(PopUpAnimation());
    }
}
```

**효과:**
- 사용자는 스피너를 보지 못하지만
- spinnerDuration 동안 큐브가 숨겨져 있음
- 백그라운드에서 텍스처 로딩
- 로딩 완료 후 팝업 애니메이션으로 등장

## 📊 성능 최적화 고려사항

### 현재 로딩 부하

```
오브젝트 66개 동시 생성 시:
- 각 오브젝트마다 main.jpg 로딩
- 각 오브젝트마다 sub_photos 로딩
- 동시 네트워크 요청 66개 발생
```

**과부하 원인:**
- UnityWebRequest가 동시에 너무 많이 실행
- 메모리 부족
- 네트워크 대역폭 초과

### spinnerDuration의 역할

**spinnerDuration = 3초의 효과:**
```
1. 사용자에게 로딩 중임을 시각적으로 알림
2. 텍스처 로딩 시간 확보 (캐싱 전에 준비)
3. AR 환경에서 갑작스러운 오브젝트 등장 방지
4. 부드러운 사용자 경험 제공
```

**실제 로딩 시간 vs spinnerDuration:**
```
실제 로딩 시간: 0.5~2초 (네트워크 상태에 따라)
spinnerDuration: 3초 (고정)

→ 항상 최소 3초 동안 스피너 표시
→ 일관된 사용자 경험
```

### 추가 최적화 제안

#### 1. 순차 로딩 (Progressive Loading)

**현재:**
```csharp
// 66개 오브젝트 동시 로딩
foreach (var place in places)
{
    CreateObjectFromData(place);  // 즉시 실행
}
```

**개선안:**
```csharp
// 시간차를 두고 로딩
for (int i = 0; i < places.Count; i++)
{
    CreateObjectFromData(places[i]);
    yield return new WaitForSeconds(0.1f);  // 100ms 간격
}
```

#### 2. Distance-Based Staggering

**현재:**
- Tier 기반으로 나누지만 Tier 내에서는 동시 로딩

**개선안:**
```csharp
// 가까운 오브젝트부터 순차적으로
var sortedPlaces = places.OrderBy(p => CalculateDistance(p)).ToList();
foreach (var place in sortedPlaces)
{
    CreateObjectFromData(place);
    yield return new WaitForSeconds(0.05f);
}
```

#### 3. Texture Pooling

```csharp
// 동일한 텍스처 재사용
private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

private IEnumerator LoadBaseMapTexture(string imageUrl)
{
    if (textureCache.ContainsKey(imageUrl))
    {
        // 캐시된 텍스처 사용
        baseMapTexture = textureCache[imageUrl];
        ApplyTexture();
        yield break;
    }

    // 새로 로딩
    // ...
    textureCache[imageUrl] = newTexture;
}
```

## 🎯 최종 실행 계획

### Step 1: Unity Editor 수정 (5분)

```
1. Unity 실행
2. 0000_Cube.prefab 열기
3. ImageDisplayController 설정:
   - loadingSpinnerPrefab: 0002에서 참조하는 것과 동일하게 할당
   - spinnerDuration: 3
4. Prefab Apply
5. 저장
```

### Step 2: 빌드 (10분)

```
1. File → Build Settings
2. Build
3. APK 생성 확인
```

### Step 3: 디바이스 테스트 (10분)

```
1. APK 설치
2. 앱 실행
3. 로그캣 모니터링:
   adb logcat | grep -E "ImageDisplayController|DEBUG_CUBE|spinner"
4. AR 환경에서 시각적 확인:
   - 스피너 표시 확인
   - 3초 후 큐브 등장 확인
   - 팝업 애니메이션 확인
```

### Step 4: 문제 발생 시

**스피너 프리팹을 찾을 수 없으면:**
```
→ Plan B Option 2 실행: 0002_Cube_TourAPI.prefab을 cubePrefab으로 사용
```

**스피너는 보이지만 큐브가 안 보이면:**
```
→ cubeRenderer 할당 확인
→ ShowSpinner(false) 호출 확인 (로그)
→ PopUpAnimation() 실행 확인 (로그)
```

**여전히 안 되면:**
```
→ Plan B Option 3: 스피너 없이 딜레이만 적용
```

## 📝 체크리스트

### Unity Editor
- [ ] 0000_Cube.prefab 열기
- [ ] ImageDisplayController 확인
- [ ] loadingSpinnerPrefab 할당 (0002와 동일)
- [ ] spinnerDuration = 3 설정
- [ ] Prefab Apply & 저장

### 빌드
- [ ] Unity 빌드 성공
- [ ] APK 생성 확인

### 테스트
- [ ] APK 설치
- [ ] 로그캣 실행
- [ ] 앱 실행
- [ ] PlaceList 표시 확인
- [ ] Offscreen Indicator 확인
- [ ] 스피너 로그 확인
- [ ] AR 환경에서 스피너 시각 확인
- [ ] 큐브 팝업 애니메이션 확인
- [ ] 터치 상호작용 확인

## 🎓 예상 결과

### 성공 시나리오

**로그:**
```
18:30:00 [DEBUG_CUBE] SetBaseMap 호출 시도: ID=200
18:30:00 [ImageDisplayController] ShowSpinner(true)
18:30:00 [ImageDisplayController] 스피너 생성 완료
18:30:00 [ImageDisplayController] cubeRenderer.enabled = False
18:30:03 [ImageDisplayController] ShowSpinner(false)
18:30:03 [ImageDisplayController] cubeRenderer.enabled = True
18:30:03 [ImageDisplayController] 팝업 애니메이션 시작
```

**사용자 관점:**
```
1. 앱 실행 → AR 세션 시작
2. PlaceList 즉시 표시 (66개 항목)
3. Offscreen Indicator 작동
4. AR 공간에 스피너들이 나타남 (각 오브젝트 위치에)
5. 3초 후 스피너 사라지고 큐브들이 팝업 애니메이션으로 등장
6. 큐브들이 AR 공간에 배치됨
7. 터치 가능, 더블탭으로 상세 정보 확인 가능
```

---

**작성일:** 2025-12-04
**핵심 문제:** 0000_Cube.prefab의 ImageDisplayController에 loadingSpinnerPrefab 미할당
**해결 방법:** Unity Editor에서 프리팹 설정 수정 (0002_Cube_TourAPI.prefab 참조)
**예상 소요 시간:** 25분 (설정 5분 + 빌드 10분 + 테스트 10분)
