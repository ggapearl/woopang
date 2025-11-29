# 사진 크기 인스펙터 설정 가이드

## 변경 사항 요약

### 1. 인스펙터 노출 필드 추가 ✅

`DoubleTap3D.cs`에 다음 필드들이 Inspector에서 조절 가능하도록 추가되었습니다:

```csharp
[Header("Photo Layout Settings")]
[SerializeField] private float photoWidth = 1080f;           // 사진 너비 (픽셀)
[SerializeField] private float photoHeight = 0f;             // 사진 높이 (0 = 화면 높이에 맞춤)
[SerializeField] private float photoMarginHorizontal = 0f;   // 좌우 여백 (픽셀)
[SerializeField] private float photoMarginVertical = 0f;     // 상하 여백 (픽셀)
[SerializeField] private float photoSpacing = 40f;           // 사진 간 간격 (픽셀)
```

### 2. Debug Text 기능 삭제 ✅

- `public Text debugText;` 필드 제거
- `debugText.text = ...` 할당 코드 모두 제거
- 총 3곳에서 debugText 관련 코드 삭제됨

### 3. 레이아웃 로직 변경 ✅

이전 코드 (문제):
```csharp
// 모든 사진이 부모 컨테이너를 100% 채움 → 겹침 발생
rect.anchorMin = new Vector2(0, 0);
rect.anchorMax = new Vector2(1, 1);  // ← 전체 너비
rect.sizeDelta = Vector2.zero;       // ← 크기 제약 없음
```

새 코드 (해결):
```csharp
// 고정 크기 + 중앙 앵커 → 겹침 방지
rect.anchorMin = new Vector2(0, 0.5f);
rect.anchorMax = new Vector2(0, 0.5f);  // ← 좌측 앵커만 사용
rect.sizeDelta = new Vector2(
    actualPhotoWidth - photoMarginHorizontal * 2,
    actualPhotoHeight - photoMarginVertical * 2
);
rect.anchoredPosition = new Vector2(
    slotWidth * currentSlot + actualPhotoWidth * 0.5f,
    0
);
```

## Unity Inspector 사용 방법

### 1. DoubleTap3D 컴포넌트 찾기

1. Unity 에디터에서 Hierarchy 창을 엽니다
2. `DoubleTap3D` 스크립트가 붙어있는 GameObject를 선택합니다
3. Inspector 창에서 "Photo Layout Settings" 섹션을 찾습니다

### 2. 각 파라미터 설명

#### Photo Width (사진 너비)
- **기본값**: `1080` (픽셀)
- **설명**: 각 사진의 가로 너비
- **0일 때**: 화면 너비(`Screen.width`)를 사용
- **추천값**:
  - 화면 전체: `1080` (대부분의 모바일)
  - 약간 작게: `1000` (좌우 여백 40px씩)
  - 더 작게: `900` (좌우 여백 90px씩)

#### Photo Height (사진 높이)
- **기본값**: `0` (화면 높이 자동)
- **설명**: 각 사진의 세로 높이
- **0일 때**: 화면 높이(`Screen.height`)를 사용
- **추천값**:
  - 화면 전체: `0` (자동)
  - 상단 바 제외: `1800` (예시)

#### Photo Margin Horizontal (좌우 여백)
- **기본값**: `0` (픽셀)
- **설명**: 사진 좌우 각각의 여백 (총 여백 = 값 × 2)
- **추천값**:
  - 여백 없음: `0`
  - 작은 여백: `10` (총 20px)
  - 중간 여백: `20` (총 40px)

#### Photo Margin Vertical (상하 여백)
- **기본값**: `0` (픽셀)
- **설명**: 사진 상하 각각의 여백 (총 여백 = 값 × 2)
- **추천값**:
  - 여백 없음: `0`
  - 작은 여백: `10` (총 20px)
  - 중간 여백: `20` (총 40px)

#### Photo Spacing (사진 간 간격)
- **기본값**: `40` (픽셀)
- **설명**: 사진과 사진 사이의 수평 간격
- **추천값**:
  - 좁은 간격: `20`
  - 중간 간격: `40` (현재)
  - 넓은 간격: `60`

## 예시 설정

### 예시 1: 화면 전체 사진 (겹침 없음)
```
Photo Width: 1080
Photo Height: 0
Photo Margin Horizontal: 0
Photo Margin Vertical: 0
Photo Spacing: 40
```
→ 사진이 화면 너비만큼 표시되고, 40px 간격으로 배치됨

### 예시 2: 좌우 여백 있는 사진
```
Photo Width: 1000
Photo Height: 0
Photo Margin Horizontal: 20
Photo Margin Vertical: 0
Photo Spacing: 40
```
→ 사진이 960px 너비(1000 - 20*2)로 표시되고, 양옆에 60px(20+40) 공간 생김

### 예시 3: 작은 사진 카드 스타일
```
Photo Width: 800
Photo Height: 1600
Photo Margin Horizontal: 40
Photo Margin Vertical: 100
Photo Spacing: 60
```
→ 작은 카드 모양으로 사진 표시, 사이에 넓은 간격

## 트러블슈팅

### 문제: 사진이 여전히 겹쳐 보임
**원인**: `Photo Width` 값이 너무 큼
**해결**: `Photo Width`를 `900` 또는 `800`으로 낮춰보세요

### 문제: 사진이 왼쪽으로 쏠림
**원인**: 앵커가 중앙이 아닌 좌측에 설정됨 (코드로 자동 수정됨)
**해결**: 이미 수정된 코드에서는 발생하지 않습니다

### 문제: 사진 간격이 너무 좁음
**원인**: `Photo Spacing` 값이 작음
**해결**: `Photo Spacing`을 `60` 또는 `80`으로 증가시켜보세요

### 문제: 사진이 화면을 벗어남
**원인**: `Photo Width` + `Photo Margin Horizontal * 2` > 화면 너비
**해결**:
- `Photo Width`를 화면 너비(1080)에 맞추거나
- `Photo Margin Horizontal`을 `0`으로 설정

## 적용된 파일

- **[DoubleTap3D.cs](Assets/Scripts/Download/DoubleTap3D.cs)**: 사진 스와이프 시스템
  - Line 28-36: Inspector 필드 추가
  - Line 56-57: photoSpacing을 Inspector 필드로 변경
  - Line 320-351: ArrangePhotos() 메서드 수정 (크기 계산 로직)
  - Line 400-404: 추가 사진 RectTransform 설정 수정
  - debugText 관련 코드 전부 삭제

## 참고 사항

- **실시간 조정 가능**: Play Mode에서도 Inspector 값을 변경하면 즉시 반영됩니다 (단, 풀스크린을 다시 열어야 함)
- **기본값 복원**: 값을 잘못 설정했다면 스크립트에서 기본값을 확인할 수 있습니다
- **화면 크기 자동 계산**: `Photo Width = 0` 또는 `Photo Height = 0`일 때 화면 크기에 맞춰 자동 계산됩니다
