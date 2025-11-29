# T5EdgeLine & 사진 중앙 정렬 문제 해결

## 🔴 문제 1: T5EdgeLine 셰이더가 사진에 적용되지 않음

### 원인 분석
1. **셰이더를 찾지 못함**: `Shader.Find("UI/T5EdgeLine")`이 `null`을 반환
2. **Unity Graphics Settings 누락**: 셰이더가 빌드에 포함되지 않음

### 해결 방법 ✅

#### 1단계: 셰이더가 프로젝트에 있는지 확인
- 파일 경로: `Assets/Scripts/UI/T5EdgeLine_UI.shader`
- 셰이더 이름: `"UI/T5EdgeLine"`

#### 2단계: Graphics Settings에 셰이더 추가 (필수!)
Unity 에디터에서:
1. **Edit > Project Settings > Graphics** 열기
2. **"Always Included Shaders"** 섹션 찾기
3. 리스트 크기를 **+1** 증가
4. 새로 생긴 슬롯에 `T5EdgeLine_UI.shader` 파일을 **드래그 앤 드롭**
5. **Apply** 또는 저장

**중요**: 이 단계를 반드시 해야 빌드/플레이 시 셰이더를 찾을 수 있습니다!

#### 3단계: 코드 수정 완료
[T5EdgeLineEffect.cs:42-83](Assets/Scripts/UI/T5EdgeLineEffect.cs#L42-L83)에서:
- 셰이더를 찾지 못하면 **명확한 에러 메시지** 출력
- Material 생성 후 **실제 적용 여부 확인**
- 디버그 로그로 성공/실패 명확히 표시

```csharp
// 셰이더 찾기 실패 시
Debug.LogError("[T5EdgeLineEffect] ❌ UI/T5EdgeLine shader를 찾을 수 없습니다!");
Debug.LogError("[T5EdgeLineEffect] Graphics Settings > Always Included Shaders에 'UI/T5EdgeLine' 추가 필요");

// 셰이더 적용 성공 시
Debug.Log($"[T5EdgeLineEffect] ✅ 셰이더 적용 성공: {gameObject.name}");
```

### 확인 방법
Unity Console에서 다음 로그를 확인:
- ✅ 성공: `[T5EdgeLineEffect] ✅ 셰이더 적용 성공: Photo_1`
- ❌ 실패: `[T5EdgeLineEffect] ❌ UI/T5EdgeLine shader를 찾을 수 없습니다!`

실패 시 → Graphics Settings에 셰이더 추가 필요

---

## 🔴 문제 2: 사진이 중앙에 있지 않고 왼쪽으로 쏠림

### 원인 분석
**앵커 포인트가 잘못 설정됨**:
```csharp
// 이전 (잘못된 코드)
rect.anchorMin = new Vector2(0, 0.5f);      // 좌측 중앙 앵커
rect.anchorMax = new Vector2(0, 0.5f);      // 좌측 중앙 앵커
rect.anchoredPosition = new Vector2(slotWidth * currentSlot + actualPhotoWidth * 0.5f, 0);
```

**문제점**:
- 앵커가 **왼쪽(0)**에 고정되어 있음
- `anchoredPosition`이 왼쪽 기준으로 계산되어 사진이 왼쪽으로 쏠림
- `actualPhotoWidth * 0.5f`를 더해도 중앙 정렬이 안됨

### 해결 방법 ✅

**앵커를 중앙(0.5)으로 변경**:
```csharp
// 수정 후 (올바른 코드)
rect.anchorMin = new Vector2(0.5f, 0.5f);  // 중앙 앵커
rect.anchorMax = new Vector2(0.5f, 0.5f);  // 중앙 앵커
rect.anchoredPosition = new Vector2(slotWidth * currentSlot, 0);  // 중앙 기준 위치
```

**수정 위치**:
1. [DoubleTap3D.cs:335-339](Assets/Scripts/Download/DoubleTap3D.cs#L335-L339) - placeInfoTextPanel
2. [DoubleTap3D.cs:347-351](Assets/Scripts/Download/DoubleTap3D.cs#L347-L351) - fullscreenImage
3. [DoubleTap3D.cs:400-404](Assets/Scripts/Download/DoubleTap3D.cs#L400-L404) - 추가 사진들

### 변경 사항 요약

| 항목 | 이전 값 | 수정 값 | 설명 |
|------|---------|---------|------|
| **anchorMin** | `(0, 0.5f)` | `(0.5f, 0.5f)` | 좌측 중앙 → 정중앙 |
| **anchorMax** | `(0, 0.5f)` | `(0.5f, 0.5f)` | 좌측 중앙 → 정중앙 |
| **anchoredPosition.x** | `slotWidth * slot + width * 0.5f` | `slotWidth * slot` | 보정값 제거 |

### Unity RectTransform 앵커 설명

```
앵커 (0, 0.5f):          앵커 (0.5f, 0.5f):
┌──────────────┐        ┌──────────────┐
│              │        │              │
●───[Image]    │        │   ●─[Image]  │  ← 중앙 정렬
│              │        │              │
└──────────────┘        └──────────────┘
↑ 왼쪽 정렬               ↑ 중앙 정렬
```

---

## 📋 테스트 체크리스트

### T5EdgeLine 셰이더
- [ ] Graphics Settings에 셰이더 추가 완료
- [ ] Unity 재시작 후 플레이
- [ ] Console에서 `✅ 셰이더 적용 성공` 로그 확인
- [ ] 사진 외곽선에 금색 발광 효과 확인
- [ ] 발광이 펄스처럼 깜빡이는지 확인

### 사진 중앙 정렬
- [ ] 첫 번째 사진이 화면 중앙에 정렬됨
- [ ] 좌우 스와이프 시 다음/이전 사진이 중앙에 정렬됨
- [ ] placeInfoPanel도 중앙에 정렬됨
- [ ] photoWidth를 900으로 변경 시 중앙 유지 확인
- [ ] photoSpacing을 60으로 변경 시 간격 증가 확인

---

## 🛠️ 추가 디버깅 팁

### T5EdgeLine이 여전히 안 보이면

1. **셰이더 재확인**:
   ```
   Unity Console → Debug.Log 필터링:
   "[T5EdgeLineEffect]" 로 검색
   ```

2. **Material Inspector 확인**:
   - Play Mode에서 Hierarchy에서 Photo_1 선택
   - Inspector > Image > Material 확인
   - Material 이름이 "T5EdgeLine_Material (Instance)" 인지 확인
   - Shader가 "UI/T5EdgeLine" 인지 확인

3. **셰이더 파라미터 확인**:
   Inspector에서 Material 펼치면:
   - `_EdgeColor`: (1, 0.95, 0.8, 1) - 금색
   - `_EdgeWidth`: 0.008 - 매우 얇음
   - `_EdgeIntensity`: 2.0
   - `_EdgeSharpness`: 2.0

4. **Canvas 렌더 모드 확인**:
   - Canvas > Render Mode: Screen Space - Overlay 또는 Camera
   - Sort Order: 100 이상 (다른 UI보다 위에 표시)

### 사진이 여전히 왼쪽으로 쏠리면

1. **photoContainer 확인**:
   Play Mode → Hierarchy → PhotoContainer 선택
   - anchoredPosition이 `(0, 0)`인지 확인
   - 스와이프 시 X 값만 변경되는지 확인

2. **Debug 로그 확인**:
   ```
   [DoubleTap3D] 컨테이너 목표 위치 업데이트: (-1120, 0), imageIndex=0, slotWidth=1120
   ```
   - `slotWidth = photoWidth + photoSpacing`
   - `containerTargetPos.x = -slotWidth * 인덱스`

3. **Inspector 값 조정 테스트**:
   - Photo Width: `800` 으로 변경 → 사진이 작아지며 중앙 유지
   - Photo Spacing: `100` 으로 변경 → 간격 넓어지며 중앙 유지

---

## 📝 변경된 파일

1. **[T5EdgeLineEffect.cs](Assets/Scripts/UI/T5EdgeLineEffect.cs)**
   - Line 42-83: `ApplyEffect()` 메서드 개선
   - 셰이더 찾기 실패 시 명확한 에러 메시지
   - Material 적용 성공/실패 로그 추가

2. **[DoubleTap3D.cs](Assets/Scripts/Download/DoubleTap3D.cs)**
   - Line 335-339: placeInfoTextPanel 앵커 중앙으로 변경
   - Line 347-351: fullscreenImage 앵커 중앙으로 변경
   - Line 400-404: 추가 사진 앵커 중앙으로 변경

---

## 💡 요약

### T5EdgeLine 적용 안됨 → Graphics Settings에 셰이더 추가!
**Edit > Project Settings > Graphics > Always Included Shaders**에 `T5EdgeLine_UI.shader` 추가 필수

### 사진 왼쪽 쏠림 → 앵커를 중앙으로 변경!
`anchorMin/Max = (0, 0.5f)` ❌ → `anchorMin/Max = (0.5f, 0.5f)` ✅

### 확인 방법
Play Mode 실행 → Console에서 `[T5EdgeLineEffect] ✅` 로그 확인
