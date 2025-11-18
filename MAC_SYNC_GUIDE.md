# 🍎 Mac에서 woopang 프로젝트 동기화 가이드

## ✅ 완료된 작업 (Windows)

### 생성/수정된 파일들:
1. **URP 셰이더** (신규)
   - `Assets/Scripts/Prefab/T5EdgeGlow_URP.shader`
   - `Assets/Scripts/Prefab/T5EdgeGlow_URP.shader.meta`

2. **업데이트된 머티리얼** (기존 파일 수정)
   - `Assets/sou/Materials/0000_Cube.mat` → T5 조명 효과 적용

3. **업데이트된 프리팹** (기존 파일 수정)
   - `Assets/Scripts/Download/0000_Cube.prefab` → T5 머티리얼 연결

---

## 📦 Mac으로 동기화하는 3가지 방법

### 방법 1: Git 사용 (권장) ⭐
```bash
# Windows에서 (현재 작업)
cd C:\woopang
git init
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader*
git add Assets/sou/Materials/0000_Cube.mat
git add Assets/Scripts/Download/0000_Cube.prefab
git commit -m "Add T5 edge glow effect for AR cube"
git push origin main

# Mac에서
cd ~/woopang  # 프로젝트 경로
git pull origin main
```

### 방법 2: 클라우드 동기화 (Google Drive, Dropbox, iCloud)
1. Windows에서 다음 파일들을 클라우드 폴더에 복사:
   ```
   Assets/Scripts/Prefab/T5EdgeGlow_URP.shader
   Assets/Scripts/Prefab/T5EdgeGlow_URP.shader.meta
   Assets/sou/Materials/0000_Cube.mat
   Assets/Scripts/Download/0000_Cube.prefab
   ```

2. Mac에서 클라우드 폴더에서 동일한 경로로 파일 복사
   ⚠️ **중요**: .meta 파일도 함께 복사해야 Unity가 제대로 인식합니다!

### 방법 3: Unity Collaborate / Plastic SCM
Unity Hub에서 제공하는 버전 관리 시스템 사용

---

## 🔧 Mac에서 Unity 프로젝트 열기

### 1. Unity 버전 확인
- Windows와 **동일한 Unity 버전** 사용 필요
- 현재 프로젝트: Unity 2022.x (URP 14.0.12 사용)

### 2. Mac에서 필요한 패키지 설치
```bash
# Cesium Unity 패키지 (경로 수정 필요)
# manifest.json에서 다음 경로 확인:
# "com.cesium.unity": "file:C:/tools/com.cesium.unity-1.15.4.tgz"
# Mac에서는 절대 경로를 Mac 경로로 변경:
# "com.cesium.unity": "file:/Users/YOUR_NAME/tools/com.cesium.unity-1.15.4.tgz"
```

### 3. Unity에서 프로젝트 열기
```bash
# Mac Terminal에서
cd ~/woopang
open -a Unity
```

---

## ✨ 변경사항 자동 적용 확인

Unity에서 프로젝트를 열면 다음이 자동으로 적용됩니다:

### ✅ iOS/Android 모두 동일하게 작동
- **셰이더**: URP 기반이므로 iOS/Android 모두 호환
- **머티리얼**: 플랫폼 독립적
- **프리팹**: Unity 크로스 플랫폼

### 🎨 T5 조명 효과 확인
1. Unity Editor에서 프로젝트 열기
2. `Assets/Scenes/` 에서 씬 열기
3. Play 버튼으로 테스트
4. AR Cube에 T5 조명 효과 확인

---

## 📱 iOS 빌드 설정 (Mac에서만 가능)

### Build Settings 확인:
```
File > Build Settings > iOS
- Switch Platform to iOS
- XCode Project 생성
```

### Player Settings:
```
Edit > Project Settings > Player > iOS
✅ Camera Usage Description
✅ Location Usage Description
✅ Requires ARKit Support
✅ Minimum iOS Version: 13.0+
```

### XCode 설정:
```
1. Unity에서 XCode 프로젝트 생성
2. XCode에서 열기
3. Signing & Capabilities 설정
4. 실제 iPhone/iPad에서 테스트
```

---

## 🤖 Android 빌드는 어디서나 가능

Windows와 Mac 모두에서 Android 빌드 가능:
```
File > Build Settings > Android
- Switch Platform to Android
- Build APK or AAB
```

---

## 🔍 동기화 확인 체크리스트

Mac에서 Unity 프로젝트를 연 후:

- [ ] `Assets/Scripts/Prefab/T5EdgeGlow_URP.shader` 존재
- [ ] `Assets/sou/Materials/0000_Cube.mat` 이 업데이트됨
- [ ] Material Inspector에서 "Universal Render Pipeline/T5EdgeGlowMobile" 셰이더 확인
- [ ] Cube 프리팹의 Material이 0000_Cube로 설정됨 (기존 GUID 유지)
- [ ] Play 모드에서 큐브 모서리에 T5 조명 효과 보임
- [ ] Console에 GUID 오류가 없음

---

## 💡 주요 포인트

### ✅ 자동으로 양쪽 플랫폼에 적용되는 것:
- 셰이더 코드 (URP 기반)
- 머티리얼 설정
- 프리팹 구조
- 스크립트 로직

### ⚠️ 플랫폼별로 다른 것:
- 빌드 설정 (iOS는 Mac, Android는 양쪽)
- XCode 프로젝트 (iOS만)
- 플랫폼 특정 플러그인 경로

### 🎯 T5 조명 효과 특징:
- **모서리 발광**: Fresnel + Edge Detection
- **부드러운 펄스**: 미세한 깜빡임 효과
- **모바일 최적화**: URP의 경량 렌더링
- **크로스 플랫폼**: iOS/Android 동일 작동

---

## 📞 문제 해결

### 셰이더가 핑크색으로 보이는 경우:
```
1. Edit > Project Settings > Graphics
2. Scriptable Render Pipeline Settings 확인
3. URP Asset이 할당되어 있는지 확인
```

### 패키지 경로 오류:
```
Packages/manifest.json 에서
Windows 경로(C:/)를 Mac 경로(/Users/)로 수정
```

### Unity 버전 불일치:
```
Windows와 Mac에서 동일한 Unity 버전 사용
LTS 버전 권장 (예: 2022.3 LTS)
```

---

## ✨ 결론

**Windows에서 수정한 T5 조명 효과는 Mac에서 파일만 동기화하면 자동으로 iOS/Android 양쪽에서 동일하게 작동합니다!**

Unity의 크로스 플랫폼 특성 덕분에 별도 수정 없이 바로 사용 가능합니다. 🎉
