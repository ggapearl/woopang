# SwipePanelController 스타일 스와이프 네비게이션 구현

## 개요
DoubleTap3D.cs를 수정하여 기존 페이드 트랜지션 방식에서 SwipePanelController 스타일의 실시간 스와이프 네비게이션으로 전환했습니다.

## 주요 변경사항

### 1. 새로운 필드 추가
```csharp
// SwipePanelController-style photo navigation
private RectTransform photoContainer;
private List<Image> photoImages = new List<Image>();
private Vector2 containerBasePos;
private Vector2 containerTargetPos;
private bool isDragging = false;
private Vector2 dragStartPos;
private float slideSpeed = 12f;
```

### 2. 컨테이너 시스템 구현

#### InitializePhotoContainer()
- 기존 fullscreenImage의 부모 아래에 PhotoContainer GameObject 생성
- fullscreenImage를 컨테이너 아래로 이동
- 모든 사진들이 이 컨테이너 내에서 수평으로 배치됨

#### ArrangePhotos()
- 풀스크린 열릴 때 호출되어 모든 사진들을 수평으로 배치
- placeInfoTextPanel이 있으면 첫 번째 슬롯(x=0)에 배치
- 이후 각 사진들을 Screen.width 간격으로 배치 (x=0, Screen.width, Screen.width*2, ...)
- 각 사진에 RoundedImage와 T5EdgeLineEffect 자동 적용

#### UpdatePhotoSprites()
- 모든 photoImages에 해당하는 sprite 할당
- 이미지 색상 및 활성화 상태 업데이트

### 3. Update() 메서드 - 실시간 드래그 추적

#### Lerp 보간 (드래그 중이 아닐 때)
```csharp
if (!isDragging && photoContainer != null && isFullscreen)
{
    photoContainer.anchoredPosition = Vector2.Lerp(
        photoContainer.anchoredPosition,
        containerTargetPos,
        Time.deltaTime * slideSpeed
    );
}
```

#### 실시간 드래그 (TouchPhase.Moved)
```csharp
isDragging = true;
Vector2 currentPos = touch.position;
float deltaX = currentPos.x - dragStartPos.x;

// 컨테이너를 실시간으로 드래그에 따라 이동
photoContainer.anchoredPosition = containerTargetPos + new Vector2(deltaX, 0);
```

#### 드래그 종료 시 스냅 (TouchPhase.Ended)
```csharp
float swipeDistance = endPos.x - dragStartPos.x;

if (Mathf.Abs(swipeDistance) > swipeThreshold)
{
    // 충분한 스와이프 거리 -> 다음/이전 이미지로 이동
    if (swipeDistance > 0)
        ShowPreviousImage();
    else
        ShowNextImage();
}
else
{
    // 스와이프 거리 부족 -> 원래 위치로 복원
    photoContainer.anchoredPosition = containerTargetPos;
}
```

### 4. ShowNextImage / ShowPreviousImage 변경

#### 기존 방식 (제거됨)
```csharp
StartCoroutine(CrossFadeImage(fadeDuration));
```

#### 새로운 방식
```csharp
// SwipePanelController 스타일: 컨테이너 목표 위치 업데이트
UpdateContainerTargetPosition();
```

#### UpdateContainerTargetPosition()
```csharp
int totalIndex = isPlaceInfoPage ? 0 : (placeInfoTextPanel != null ? imageIndex + 1 : imageIndex);
float screenWidth = Screen.width;
containerTargetPos = new Vector2(-screenWidth * totalIndex, 0);
```

- placeInfoPage면 x=0 (첫 번째 슬롯)
- 첫 번째 사진이면 x=-Screen.width (두 번째 슬롯)
- 두 번째 사진이면 x=-Screen.width*2 (세 번째 슬롯)
- Update()의 Lerp가 자동으로 부드럽게 이동

### 5. OnDoubleTapCube() 수정

```csharp
// SwipePanelController 스타일: 사진 배치
ArrangePhotos();

// 컨테이너 위치 초기화
if (photoContainer != null)
{
    containerTargetPos = Vector2.zero;
    photoContainer.anchoredPosition = Vector2.zero;
}
```

풀스크린 열릴 때 ArrangePhotos()를 호출하여 모든 사진 배치 완료

## SwipePanelController 패턴 적용 완료

### 동작 방식
1. **풀스크린 열림**: ArrangePhotos()로 모든 사진을 수평 배치
2. **드래그 시작**: isDragging = true, dragStartPos 저장
3. **드래그 중**: 컨테이너가 손가락을 실시간으로 따라 이동
4. **드래그 종료**:
   - swipeThreshold 이상: ShowNextImage/ShowPreviousImage 호출 → UpdateContainerTargetPosition()으로 목표 위치 설정
   - swipeThreshold 미만: 원래 위치로 복원
5. **Lerp 보간**: Update()에서 매 프레임 containerTargetPos를 향해 부드럽게 이동

### 제거된 기능
- `CrossFadeImage()` 코루틴 - 더 이상 사용되지 않음 (페이드 효과 제거)
- `isFading` 플래그 체크 - 더 이상 필요 없음

### 유지된 기능
- 더블 터치로 풀스크린 열기/닫기
- 세로 스와이프로 풀스크린 닫기
- placeInfoTextPanel 지원 (첫 번째 슬롯)
- iOS 이미지 캐싱 시스템
- 인스타그램 버튼 등 기존 UI 기능

## 주요 개선점

1. **자연스러운 전환**: 페이드 대신 실시간 드래그로 사진이 옆으로 슬라이드
2. **실시간 피드백**: 손가락 움직임에 따라 사진이 즉시 반응
3. **부드러운 스냅**: Lerp 보간으로 목표 위치까지 자연스럽게 이동
4. **일관된 UX**: SwipePanelController(UploadPage)와 동일한 패턴 적용

## 파일 위치
- 수정된 파일: `Assets/Scripts/Download/DoubleTap3D.cs`
- 참조 파일: `Assets/Scripts/UI/SwipePanelController.cs`
