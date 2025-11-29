# 3D 구형 로딩 인디케이터 설정 가이드

## 개요

데이터 로딩 중 화면에 표시되는 **작은 3D 구형 인디케이터**를 설정하는 방법입니다.
- 위아래로 천천히 떠다니는 애니메이션
- 부드럽게 회전
- 크기가 살짝 커졌다 작아지는 펄스 효과

## Unity에서 설정하는 방법

### 1. 저해상도 텍스처 확인

다음 텍스처 파일이 생성되어 있습니다:
```
Assets/Resources/Sprites/loading_spinner_64.png   (추천 - 가장 가벼움)
Assets/Resources/Sprites/loading_spinner_128.png  (더 선명)
Assets/Resources/Sprites/loading_dots_64.png      (점 스타일)
```

**Unity에서 Import Settings 최적화**:
1. Project 창에서 `loading_spinner_64.png` 선택
2. Inspector에서 다음 설정:
   ```
   Texture Type: Default
   Max Size: 64 (또는 128)
   Compression: Normal Quality
   Generate Mip Maps: 체크 해제 (더 가벼움)
   ```
3. **Apply** 클릭

### 2. 3D 구형 GameObject 생성

#### Hierarchy에서 생성
1. **Hierarchy** 창에서 우클릭
2. **3D Object** → **Sphere** 선택
3. 생성된 Sphere 이름을 `LoadingIndicator`로 변경

#### Transform 설정
```
Position: (0, 1.5, 2)      // 카메라 정면 2m, 눈높이 1.5m
Rotation: (0, 0, 0)
Scale: (0.12, 0.12, 0.12)  // 작은 크기 (12cm)
```

**추천 위치**:
- **카메라 앞 2~3m**: 너무 가까우면 시야 방해, 너무 멀면 안 보임
- **눈높이 1.5m**: 사용자가 자연스럽게 볼 수 있는 높이
- **작은 크기 0.1~0.15m**: 눈에 잘 띄지만 방해되지 않는 크기

### 3. Material 설정 (텍스처 적용)

#### 텍스처 Material 생성 (추천)
1. **Project** 창에서 우클릭 → **Create** → **Material**
2. 이름을 `LoadingSpinnerMaterial`로 변경
3. Inspector에서 다음 설정:

**저해상도 텍스처 적용**:
```
Shader: Unlit/Transparent (가장 가벼움) 또는 Standard
Rendering Mode: Transparent
Albedo:
  └─ Texture: loading_spinner_64.png (드래그 앤 드롭)
  └─ Color: 흰색
Tiling: (1, 1)
Offset: (0, 0)
```

**발광 효과 추가 (선택)**:
```
Emission: 체크 활성화
  └─ Color: 밝은 파란색 (RGB: 150, 200, 255)
  └─ Intensity: 0.3
```

4. Sphere에 Material 드래그 앤 드롭

**최적화 팁**:
- Shader는 **Unlit/Transparent** 추천 (조명 계산 없음 = 더 가벼움)
- Mip Maps 끄기 (텍스처 크기 64x64면 불필요)
- Emission은 선택사항 (없어도 충분히 보임)

### 4. LoadingIndicator3D 스크립트 추가

1. **Hierarchy**에서 `LoadingIndicator` 선택
2. **Inspector** 하단의 **Add Component** 클릭
3. **LoadingIndicator3D** 검색 후 선택

#### Inspector 설정값

**Float Animation (떠다니기)**
```
Float Speed: 1.0      // 위아래 속도 (느리게: 0.5, 빠르게: 2.0)
Float Height: 0.3     // 위아래 높이 (미터)
```

**Rotation Animation (회전)**
```
Rotation Speed: 90    // 회전 속도 (도/초) - 4초에 한 바퀴
Rotation Axis: (0.3, 1, 0.2)  // 비스듬하게 회전
```

**Pulse Animation (크기 변화)**
```
Enable Pulse: ✓       // 체크
Pulse Speed: 2.0      // 펄스 속도
Min Scale: 0.9        // 최소 크기 (90%)
Max Scale: 1.1        // 최대 크기 (110%)
```

#### 추천 프리셋

**천천히 떠다니기 (편안함)**
```
Float Speed: 0.8
Float Height: 0.2
Rotation Speed: 60
Enable Pulse: ✓
```

**활발하게 움직이기 (역동적)**
```
Float Speed: 1.5
Float Height: 0.4
Rotation Speed: 120
Enable Pulse: ✓
Pulse Speed: 3.0
```

**부드럽게 회전만**
```
Float Speed: 0.5
Float Height: 0.1
Rotation Speed: 45
Enable Pulse: ✗ (체크 해제)
```

### 5. GameObject 비활성화

1. **Hierarchy**에서 `LoadingIndicator` 선택
2. **Inspector** 상단의 체크박스 **해제** (비활성화)
   - 또는 GameObject 이름 왼쪽의 체크박스 해제

**중요**: 처음에는 꺼져있어야 합니다! DataManager가 필요할 때만 켭니다.

### 6. DataManager에 연결

1. **Hierarchy**에서 `DataManager` GameObject 선택
2. **Inspector**에서 **DataManager (Script)** 컴포넌트 찾기
3. **Loading Indicator** 섹션:
   - **Loading Indicator** 필드에 `LoadingIndicator` GameObject를 **드래그 앤 드롭**

## 추가 설정 필요사항

### Inspector에서 확인해야 할 설정

**DataManager 컴포넌트**:
```
Progressive Loading Settings:
  Load Radii: [25, 50, 75, 100, 150, 200]
  Tier Delay: 1.0
  Object Spawn Delay: 0.5

Loading Indicator:
  Loading Indicator: LoadingIndicator (GameObject)
```

**LoadingIndicator3D 컴포넌트**:
```
Float Animation:
  Float Speed: 0.8 (천천히 추천)
  Float Height: 0.2

Rotation Animation:
  Rotation Speed: 60 (느리게 회전)
  Rotation Axis: (0.3, 1, 0.2)

Pulse Animation:
  Enable Pulse: ✓
  Pulse Speed: 2.0
  Min Scale: 0.95
  Max Scale: 1.05
```

## 대안: Canvas UI로 만들기 (2D)

3D 구형 대신 2D UI로 만들고 싶다면:

### Canvas에 Image 생성
1. **Hierarchy** → 우클릭 → **UI** → **Image**
2. 이름을 `LoadingIndicatorUI`로 변경
3. RectTransform 설정:
   ```
   Anchor: Center
   Position: (0, 0, 0)
   Width: 100
   Height: 100
   ```

### 스피너 이미지 적용
1. Project에 원형 스피너 PNG 이미지 추가
2. Image 컴포넌트의 **Source Image**에 할당
3. **Color**: 흰색 또는 파란색

### 회전 애니메이션 스크립트
```csharp
using UnityEngine;

public class SimpleSpinner : MonoBehaviour
{
    public float rotationSpeed = 360f;

    void Update()
    {
        transform.Rotate(0, 0, -rotationSpeed * Time.deltaTime);
    }
}
```

## 테스트

### Play Mode에서 확인
1. Unity에서 **Play** 버튼 클릭
2. 앱 시작 후 AR 세션이 시작되면:
   - 로딩 인디케이터가 자동으로 **표시**됨
   - 위아래로 떠다니고 회전하는 애니메이션 확인
   - 데이터 로딩 완료 후 자동으로 **사라짐**

### Debug 로그 확인
Console에서 다음 로그 확인:
```
[DataManager] 로딩 인디케이터: 표시
[DataManager] ===== 점진적 로딩 시작 =====
...
[DataManager] ===== 점진적 로딩 완료 - 총 X개 오브젝트 =====
[DataManager] 로딩 인디케이터: 숨김
```

## 트러블슈팅

### 문제: 인디케이터가 안 보임
**원인 1**: GameObject가 처음부터 활성화되어 있음
- **해결**: Hierarchy에서 LoadingIndicator 체크 해제

**원인 2**: 카메라 시야 밖에 위치
- **해결**: Position을 (0, 1.5, 2)로 설정

**원인 3**: 너무 작거나 투명함
- **해결**: Scale을 (0.2, 0.2, 0.2)로 키우거나 Material의 Alpha 확인

### 문제: 회전이 안 됨
**원인**: LoadingIndicator3D 스크립트가 없음
- **해결**: Add Component로 LoadingIndicator3D 추가

### 문제: 애니메이션이 너무 빠름/느림
**해결**: Inspector에서 속도 값 조절
- Float Speed: 떠다니는 속도
- Rotation Speed: 회전 속도
- Pulse Speed: 크기 변화 속도

### 문제: 로딩이 끝났는데 안 사라짐
**원인**: DataManager에 연결 안 됨
- **해결**: DataManager Inspector의 Loading Indicator 필드에 연결 확인

## 추가 커스터마이징

### Light 추가 (발광 효과)
1. LoadingIndicator 선택
2. Add Component → **Light** → **Point Light**
3. 설정:
   ```
   Color: 하늘색
   Range: 3
   Intensity: 0.5
   ```

### Particle System 추가 (반짝임)
1. LoadingIndicator 선택
2. Add Component → **Effects** → **Particle System**
3. 설정:
   ```
   Duration: 2
   Start Lifetime: 1
   Start Speed: 0.5
   Start Size: 0.05
   Emission Rate: 10
   ```

### Trail Renderer 추가 (궤적)
1. LoadingIndicator 선택
2. Add Component → **Effects** → **Trail Renderer**
3. 설정:
   ```
   Time: 0.5
   Width: 0.02
   Color: 흰색 → 투명
   ```

## 요약

1. ✅ Hierarchy에서 3D Sphere 생성
2. ✅ Scale을 (0.15, 0.15, 0.15)로 작게
3. ✅ Position을 (0, 1.5, 2)로 카메라 앞에 배치
4. ✅ LoadingIndicator3D 스크립트 추가
5. ✅ GameObject 비활성화
6. ✅ DataManager에 연결

완료! 이제 데이터 로딩 중 작은 구가 떠다니며 회전하는 인디케이터가 표시됩니다.
