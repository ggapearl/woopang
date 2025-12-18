# 디버그 로그 정리 계획

## 현재 상황
- DataManager.cs: 60개 이상의 Debug 로그
- 대부분 [DebugPerf], [DEBUG_DATA], [DebugStart] 등 임시 디버깅용

## 정리 기준

### ✅ 유지할 로그 (에러/경고만)
```csharp
Debug.LogError() - 치명적 오류
Debug.LogWarning() - 풀 부족 등 경고
```

### ❌ 삭제할 로그 (일반 정보)
```csharp
Debug.Log($"[DebugPerf] ...") - 성능 측정
Debug.Log($"[DEBUG_DATA] ...") - 데이터 파싱
Debug.Log($"[DebugStart] ...") - 시작 시점
Debug.Log($"[DataManager] 점진적 로딩...") - 일반 정보
```

## 파일별 정리 계획

### 1. DataManager.cs
- 제거: 모든 [DebugPerf], [DEBUG_DATA], [DebugStart] 로그 (40개+)
- 유지: LogError, LogWarning (필수 에러만)
- 추가: 오브젝트 생성 완료 로그 1개 (선택)

### 2. ImageDisplayController.cs
- 제거: 모든 일반 Log
- 유지: 이미지 로딩 실패 에러만

### 3. DoubleTap3D.cs
- 제거: 모든 일반 Log
- 유지: UI 요소 누락 에러만

### 4. PlaceListManager.cs
- 제거: 모든 디버그 로그
- 유지: 에러만

### 5. TourAPIImageController.cs, TourAPIManager.cs
- 제거: 모든 일반 Log
- 유지: API 요청 실패 에러만

## SystemUIManager 깜빡임 문제

**현상**: SystemUIManager 활성화 시 깜빡임 발생
**원인**: DelayedCanvasAdjustment()에서 FindObjectOfType 반복 호출

**해결책**: SimpleSafeAreaManager + AndroidSystemUIController 사용
- 기존 SystemUIManager는 비활성화 유지
- 새 스크립트 2개 사용 (이미 생성됨)

## Release Build 권장

Development Build는 성능이 30-50% 저하됩니다.
디버깅 완료 후 **반드시 Release Build로 최종 테스트**하세요.

## 작업 순서

1. DataManager.cs 디버그 로그 정리
2. ImageDisplayController.cs 디버그 로그 정리
3. DoubleTap3D.cs 디버그 로그 정리
4. 기타 파일 디버그 로그 정리
5. Release Build로 테스트
