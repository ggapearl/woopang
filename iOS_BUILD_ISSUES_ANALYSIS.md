# iOS Build Issues - Root Cause Analysis

## 📋 Summary
모든 iOS 빌드 문제의 근본 원인을 발견했습니다: **ARFoundation 버전 불일치**

---

## 🔴 Critical Issue: ARFoundation Version Mismatch

### 현재 상태
```
ARCore Extensions 1.51.0 요구사항:
  - com.unity.xr.arfoundation: 4.1.5
  - com.unity.xr.arcore: 4.1.5
  - com.unity.xr.arkit: 4.1.5

실제 설치된 버전:
  - com.unity.xr.arfoundation: 6.3.1 ❌ (MISMATCH!)
  - com.unity.xr.arcore: 5.1.6 ❌ (MISMATCH!)
  - com.unity.xr.arkit: 5.1.6 ❌ (MISMATCH!)
```

### 왜 문제가 되는가?
1. **ARFoundation 6.x는 4.x와 완전히 다른 텍스처 관리 시스템 사용**
2. ARCore Extensions 1.51.0은 ARFoundation 4.1.5 API만 지원
3. Unity 6으로 업그레이드하면서 ARFoundation도 자동으로 6.x로 업데이트됨
4. **ARCore Extensions가 ARFoundation 6.x API를 호출하지 못함**

---

## 🐛 이 버전 불일치가 유발하는 문제들

### 1. AR 카메라 작동 안 함 (iOS)
**Xcode 콘솔 에러:**
```
ARCameraManager.Update()
  → NoSwapchainStrategy.TryUpdateTexturesForFrame()
  → UpdatableTextureFactory.Create()
  → 텍스처 생성 실패
```

**원인:**
- ARFoundation 6.x는 새로운 텍스처 생성 방식 사용
- ARCore Extensions 1.51.0은 ARFoundation 4.x 텍스처 API 호출
- iOS ARKit에서 텍스처 생성 완전 실패

### 2. 권한 프롬프트 없음
**문제:** 앱 설치 후 카메라/위치 권한 요청 팝업이 나타나지 않음

**원인 1 - 권한 요청 코드 누락 (iOS):**
```csharp
// PermissionRequester.cs 라인 15-38
void RequestPermissions()
{
    #if UNITY_ANDROID  // ❌ iOS에서는 실행 안 됨!
    if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
    {
        Permission.RequestUserPermission(Permission.FineLocation);
    }

    if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
    {
        Permission.RequestUserPermission(Permission.Camera);
    }
    #endif
}
```

**원인 2 - ARSession 초기화 실패:**
- ARSession이 제대로 시작되지 않아 카메라 권한 자동 요청도 안 됨
- ARFoundation 버전 불일치로 ARSession.state가 SessionTracking에 도달 못함

### 3. 안드로이드는 작동하는데 iOS만 안 됨
**왜?**
- ARCore Extensions가 ARCore 5.x와 부분 호환성 있음
- ARKit는 ARFoundation 6.x 변경사항에 더 민감
- iOS는 Metal 렌더링 파이프라인이 더 엄격함

---

## ✅ 해결 방법

### Option 1: ARCore Extensions 최신 버전으로 업그레이드 (권장)

Unity 6 + ARFoundation 6.x를 지원하는 ARCore Extensions로 업데이트:

1. **ARCore Extensions for AR Foundation 다운로드**
   - URL: https://github.com/google-ar/arcore-unity-extensions/releases
   - Unity 6 지원 버전 확인 필요

2. **설치 방법:**
   ```bash
   # 기존 ARCore Extensions 제거
   rm -rf Packages/com.google.ar.core.arfoundation.extensions

   # 새 버전 다운로드 & 설치
   # (GitHub Releases에서 .tgz 파일 다운로드 후 Package Manager에서 설치)
   ```

3. **장점:**
   - Unity 6 기능 활용 가능
   - ARFoundation 6.x 신규 기능 사용
   - 장기적으로 안정적

### Option 2: ARFoundation 다운그레이드 (임시 방편)

ARCore Extensions 1.51.0과 호환되도록 다운그레이드:

1. **Packages/manifest.json 수정:**
   ```json
   {
     "dependencies": {
       "com.unity.xr.arcore": "4.1.5",
       "com.unity.xr.arkit": "4.1.5",
       "com.unity.xr.arfoundation": "4.1.5",
       // ... 나머지 동일
     }
   }
   ```

2. **단점:**
   - Unity 6의 새로운 AR 기능 사용 불가
   - 향후 업데이트 어려움
   - Unity 6와 ARFoundation 4.x 호환성 이슈 가능

---

## 🔧 추가 수정 필요 사항

### 1. iOS 권한 요청 코드 추가

**PermissionRequester.cs 수정:**

```csharp
void RequestPermissions()
{
    #if UNITY_ANDROID
    // Android 권한 요청 (기존 코드)
    if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        Permission.RequestUserPermission(Permission.FineLocation);

    if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        Permission.RequestUserPermission(Permission.Camera);

    #elif UNITY_IOS
    // iOS 권한은 Info.plist 설정 + 기능 사용 시 자동 요청됨
    // 위치 서비스 시작 → 자동으로 위치 권한 요청
    StartCoroutine(RequestIOSLocationPermission());

    // 카메라 권한은 ARSession 시작 시 자동 요청됨
    // (ARFoundation이 정상 작동하면 자동으로 처리)
    #endif
}

#if UNITY_IOS
private IEnumerator RequestIOSLocationPermission()
{
    if (!Input.location.isEnabledByUser)
    {
        Debug.LogWarning("Location services disabled by user");
        yield break;
    }

    Input.location.Start();
    int maxWait = 20;
    while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
    {
        yield return new WaitForSeconds(1);
        maxWait--;
    }

    if (Input.location.status == LocationServiceStatus.Running)
        Debug.Log("iOS Location Permission Granted");
    else
        Debug.LogError("iOS Location Permission Denied");
}
#endif
```

### 2. ARCameraOptimizer.cs 문제점 제거

**현재 문제:**
- `ARCameraOptimizer.cs` 라인 72-81에서 ARCameraManager를 껐다 켰다 반복
- 이게 텍스처 생성 에러를 악화시킴

**임시 해결책:**
```csharp
// OptimizeForIOS() 메서드를 완전히 비활성화
void OptimizeForIOS()
{
    // ARFoundation 버전 문제 해결 전까지 iOS 최적화 비활성화
    return;
}
```

### 3. Safe Area 처리

**이미 생성된 SafeAreaHandler.cs를 Canvas에 추가:**
1. Unity에서 Canvas 또는 최상위 Panel GameObject 선택
2. Add Component → SafeAreaHandler
3. 재빌드

---

## 📝 권장 작업 순서

### 즉시 실행:
1. ✅ **ARCore Extensions 최신 버전 확인 & 다운로드**
   - https://github.com/google-ar/arcore-unity-extensions/releases
   - Unity 6 + ARFoundation 6.x 지원 버전 찾기

2. ✅ **ARCameraOptimizer.cs 임시 비활성화**
   ```csharp
   void OptimizeForIOS() { return; }
   ```

3. ✅ **SafeAreaHandler 추가**
   - Canvas에 SafeAreaHandler 컴포넌트 추가

### ARCore Extensions 업데이트 후:
4. ✅ **PermissionRequester.cs iOS 권한 로직 추가**
5. ✅ **Unity에서 iOS 프로젝트 재빌드**
6. ✅ **Xcode에서 테스트**

---

## 🎯 예상 결과

위 수정사항 적용 후:
- ✅ AR 카메라 정상 작동 (텍스처 생성 성공)
- ✅ 앱 설치 시 카메라/위치 권한 프롬프트 표시
- ✅ 상태바가 UI와 겹치지 않음 (Safe Area 적용)
- ✅ Android와 iOS 동일하게 작동

---

## 📚 참고 자료

- ARCore Extensions GitHub: https://github.com/google-ar/arcore-unity-extensions
- Unity ARFoundation 6.x 마이그레이션 가이드: https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/migration-guide-6.html
- Unity 6 + ARKit 호환성: https://docs.unity3d.com/Packages/com.unity.xr.arkit@6.0/manual/index.html
