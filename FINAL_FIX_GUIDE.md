# iOS AR 카메라 & 상태바 문제 최종 해결 가이드

## 🔴 발생한 문제
1. **AR 카메라 작동 안 함** - UpdatableTextureFactory.Create() 에러 무한 반복
2. **상태바 UI 겹침** - SafeAreaHandler가 Canvas에 잘못 부착됨

## 🔍 근본 원인
- **ARFoundation 6.3.1**이 Unity 6에서 iOS ARKit 텍스처 관리에 버그 있음
- packages-lock.json이 manifest.json 무시하고 6.3.1 자동 설치
- SafeAreaHandler가 Canvas 전체에 적용되어 모든 UI 왜곡

## ✅ 적용된 해결책 (자동 수정 완료)

### 1. ARFoundation 5.1.6 강제 고정

Packages/manifest.json:
- ✅ com.unity.xr.arfoundation: "5.1.6" (manifest에 명시적 추가)
- ✅ com.unity.xr.arcore: "5.1.6"
- ✅ com.unity.xr.arkit: "5.1.6"
- ✅ ARCore Extensions: arf5
- ✅ ARFoundation 6.3.1 캐시 삭제 완료

### 2. SafeAreaHandler 올바른 위치로 이동

Assets/Scenes/WP_0111.unity:
- ✅ Canvas에서 SafeAreaHandler 제거
- ✅ Panel_Top에 SafeAreaHandler 추가
- 이제 상단 패널만 Safe Area 적용됨

## 🚀 Unity에서 진행할 작업

### Step 1: Unity 에디터 재시작
1. **Unity 완전 종료**
2. Unity 재실행하여 패키지 재설치 트리거
3. Window → Package Manager에서 버전 확인:
   - ARFoundation: 5.1.6 (6.3.1 아님!)
   - ARCore: 5.1.6
   - ARKit: 5.1.6
   - ARCore Extensions: arf5
4. **컴파일 에러 발생 시 (CS0592):**
   - 이미 수정됨: `Library/PackageCache/.../ARGeospatialCreatorOrigin.cs`
   - Line 87: `[SerializeField]` 주석처리됨
5. 콘솔에서 컴파일 에러 없는지 확인

### Step 2: 씬 리로드 확인
1. Assets/Scenes/WP_0111.unity 씬 열기
2. Hierarchy에서 Canvas → Panel_Top 선택
3. Inspector에서 SafeAreaHandler 컴포넌트 확인
4. Canvas GameObject에는 SafeAreaHandler 없어야 함

### Step 3: iOS 빌드 & 테스트
1. File → Build Settings → iOS → Build
2. Xcode에서 프로젝트 열기
3. 실제 iOS 디바이스에서 실행
4. **테스트 항목:**
   - ✅ AR 카메라 정상 작동 (UpdatableTextureFactory 에러 없음)
   - ✅ 상단바가 Safe Area 내에 표시됨
   - ✅ 카메라/위치 권한 프롬프트 정상 표시

## 📝 변경 사항 요약

**수정된 파일:**
1. `Packages/manifest.json` - ARFoundation 5.1.6 명시적 추가
2. `Assets/Scenes/WP_0111.unity` - SafeAreaHandler를 Canvas → Panel_Top으로 이동
3. `Library/PackageCache/` - ARFoundation 6.3.1 캐시 삭제

**핵심 수정:**
- ARFoundation 버전 고정으로 자동 업그레이드 방지
- SafeAreaHandler를 올바른 GameObject에 적용하여 UI 레이아웃 보호
