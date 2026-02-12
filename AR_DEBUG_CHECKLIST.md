# iOS AR 카메라 디버깅 체크리스트

## ✅ Unity Editor에서 확인

### 1. XR Plug-in Management 설정
**Edit → Project Settings → XR Plug-in Management**

**iOS 탭에서 확인:**
- ✅ **ARKit** 체크박스가 켜져 있어야 함!
- ✅ **ARCore** 체크박스는 꺼야 함 (iOS에서는 안 씀)

**Android 탭에서 확인:**
- ✅ **ARCore** 체크박스가 켜져 있어야 함
- ✅ **ARKit** 체크박스는 꺼야 함

### 2. Player Settings 확인
**Edit → Project Settings → Player → iOS**

**Other Settings:**
- ✅ Target minimum iOS Version: **11.0 이상**
- ✅ Architecture: **ARM64** (Simulator X)
- ✅ Camera Usage Description: "For AR" (이미 설정됨)
- ✅ Location Usage Description: "For Geospatial" (이미 설정됨)

**Identification:**
- ✅ Bundle Identifier 설정되어 있는지 확인

### 3. Scene 확인
**Hierarchy에서:**
- ✅ AR Session GameObject 있는지
- ✅ XR Origin (또는 AR Session Origin) 있는지
- ✅ ARCore Extensions GameObject 있는지

## 🔍 Xcode에서 디버깅

### 1. 빌드 후 Xcode에서 확인

**Info.plist:**
```xml
<key>NSCameraUsageDescription</key>
<string>For AR</string>
<key>NSLocationWhenInUseUsageDescription</key>
<string>For Geospatial</string>
```

**General → Frameworks:**
- ARKit.framework 포함되어 있는지

### 2. 실제 디바이스에서 실행

**Console에서 확인:**
```
- "ARSession started" 로그 나오는지
- "Camera permission granted" 나오는지
- UpdatableTextureFactory 에러 나오는지 (ARFoundation 6.3.1 문제)
```

### 3. 권한 프롬프트 확인
- 앱 실행 시 카메라 권한 요청 팝업 나오는지
- 위치 권한 요청 팝업 나오는지

## 🎯 디버깅 순서

1. **Unity Editor 로그 무시**
   - "No active XRSessionSubsystem" → **정상!**
   - Editor에서는 AR 작동 안 함

2. **iOS 빌드**
   - File → Build Settings → iOS
   - Build (또는 Build and Run)

3. **Xcode에서 실제 디바이스 선택**
   - Simulator 아님!
   - iPhone/iPad 실제 기기

4. **Run 후 Console 확인**
   - Xcode → View → Debug Area → Show Debug Area
   - 로그 확인

## 🔴 예상되는 문제

### ARFoundation 5.1.6이 제대로 설치 안 됨
**확인:** Window → Package Manager → AR Foundation
- 버전이 5.1.6인지 확인
- 6.3.1이면 다시 수정 필요

### XR Plug-in Management 설정 안 됨
**해결:**
1. Edit → Project Settings → XR Plug-in Management
2. iOS 탭에서 ARKit 활성화
3. Install XR Plugin Management 버튼 클릭 (있으면)

### 권한 설정 누락
**확인:** ProjectSettings.asset에서
```
cameraUsageDescription: For AR
locationUsageDescription: For Geospatial
```

## 📱 정상 작동 시 나타날 현상

1. 앱 실행 → 카메라 권한 요청 팝업
2. 허용 → 위치 권한 요청 팝업
3. 허용 → AR 카메라 피드 표시됨
4. GPS/고도 정보로 오브젝트 배치

## ❌ ARFoundation 6.3.1 문제 (현재 해결됨)

증상:
```
UpdatableTextureFactory.Create() failed
Texture is null
```

해결:
- ✅ ARFoundation 5.1.6으로 다운그레이드 완료
- ✅ packages-lock.json 확인됨
