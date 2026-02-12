# ARCore Extensions 업그레이드 가이드 (ARFoundation 6 지원)

## 📦 현재 상태
- ARCore Extensions: **1.51.0** (ARFoundation 4.1.5만 지원)
- 설치 필요: **ARCore Extensions arf6** (ARFoundation 6.x 지원)

---

## 🔧 업그레이드 단계

### 1. 기존 ARCore Extensions 제거

**Unity 에디터에서:**
1. Window → Package Manager 열기
2. Packages: In Project 선택
3. "ARCore Extensions" 찾기
4. Remove 클릭

**또는 수동 제거:**
```bash
cd /Users/pdnom/Desktop/woopang
rm -rf Packages/com.google.ar.core.arfoundation.extensions
```

### 2. 새 ARCore Extensions 설치

**Unity 에디터에서:**

**Option A: Git URL로 설치 (권장)**
1. Window → Package Manager 열기
2. 좌측 상단 **+** 버튼 클릭
3. "Add package from git URL..." 선택
4. 다음 URL 입력:
   ```
   https://github.com/google-ar/arcore-unity-extensions.git#arf6
   ```
5. Add 클릭

**Option B: Tarball로 설치**
1. https://github.com/google-ar/arcore-unity-extensions/releases 방문
2. **arf6** 태그가 있는 최신 릴리스 찾기
3. `.tgz` 파일 다운로드
4. Unity → Window → Package Manager
5. 좌측 상단 **+** 버튼 클릭
6. "Add package from tarball..." 선택
7. 다운로드한 `.tgz` 파일 선택

### 3. 컴파일 에러 확인

설치 후 Unity 콘솔 확인:
- ⚠️ Obsolete 경고는 정상 (ARCore Extensions가 일부 구버전 심볼 사용)
- ❌ 컴파일 에러가 있으면 해결 필요

### 4. ARCore Extensions 설정 확인

**Unity 에디터에서:**
1. Edit → Project Settings → XR Plug-in Management
2. Android 탭:
   - ✅ ARCore 체크
3. iOS 탭:
   - ✅ ARKit 체크
4. ARCore Extensions 탭:
   - Android Authentication Strategy: API Key (또는 Keyless)
   - iOS Authentication Strategy: API Key (또는 Authentication Token)

### 5. iOS 프로젝트 재빌드

```bash
# Unity에서:
# File → Build Settings → iOS
# Build 클릭 (또는 Build And Run)
```

### 6. Xcode에서 테스트

빌드 완료 후 Xcode에서:
1. 프로젝트 열기
2. 실제 iOS 디바이스 연결
3. Run (⌘R)
4. 확인 사항:
   - ✅ 카메라 권한 프롬프트 표시
   - ✅ 위치 권한 프롬프트 표시
   - ✅ AR 카메라 정상 작동 (텍스처 생성 성공)
   - ✅ 상태바와 UI 겹침 없음 (SafeAreaHandler 추가 후)

---

## 🔍 예상되는 경고 메시지

ARCore Extensions arf6 설치 후 다음 경고가 나타날 수 있습니다:

```
CS0618: 'SomeARFoundationAPI' is obsolete
```

**이것은 정상입니다:**
- ARCore Extensions가 ARFoundation 5의 deprecated 심볼을 일부 사용
- 경고일 뿐 에러 아님
- Google이 향후 업데이트에서 수정 예정

---

## ⚠️ 문제 해결

### "Package already exists" 에러
```bash
# Packages/manifest.json에서 ARCore Extensions 라인 수동 제거
# Library/PackageCache 폴더 삭제 후 Unity 재시작
rm -rf Library/PackageCache
```

### Git URL 추가 실패
- Unity Hub에서 Git 설치 확인
- 또는 Option B (Tarball 설치) 사용

### 컴파일 에러 발생
- Console에서 에러 메시지 전체 복사
- 특정 스크립트 수정 필요할 수 있음

---

## 📚 참고 자료

- [ARCore Extensions GitHub](https://github.com/google-ar/arcore-unity-extensions)
- [ARCore Extensions Releases](https://github.com/google-ar/arcore-unity-extensions/releases)
- [Upgrade to AR Foundation 6 공식 가이드](https://developers.google.com/ar/develop/unity-arf/upgrade-to-ar-foundation-6)
- [ARCore Extensions 기능 목록](https://developers.google.com/ar/develop/unity-arf/features)

---

## ✅ 성공 확인 체크리스트

업그레이드 후 다음 사항 확인:

- [ ] Unity 콘솔에 컴파일 에러 없음 (경고는 OK)
- [ ] Package Manager에서 ARCore Extensions 버전이 arf6 브랜치로 표시됨
- [ ] iOS 빌드 성공
- [ ] Xcode에서 실행 시 카메라 권한 프롬프트 표시
- [ ] AR 카메라가 정상적으로 작동 (화면에 카메라 뷰 표시)
- [ ] Xcode 콘솔에 텍스처 생성 에러 없음
- [ ] Geospatial API 정상 작동 (위치 정보 표시)
