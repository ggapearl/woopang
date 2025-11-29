# 맥북에서 iOS 빌드 설정 가이드

## 📱 맥북에서 pull 받고 iOS 빌드하기

### 1️⃣ 맥북에서 Git Pull

터미널 열고:

```bash
# 프로젝트 디렉토리로 이동
cd /path/to/woopang

# 최신 변경사항 가져오기
git fetch origin

# main 브랜치로 전환 (이미 main이면 생략)
git checkout main

# pull 받기
git pull origin main
```

### ⚠️ Pull 시 주의사항

**Force push를 했기 때문에 충돌 가능성:**

만약 에러가 나면:
```bash
# 로컬 변경사항 백업 (있다면)
git stash

# 강제로 원격과 동기화
git reset --hard origin/main

# 백업한 변경사항 복원 (필요하면)
git stash pop
```

### 2️⃣ Unity에서 씬 열기

1. **Unity Hub 실행**
2. **woopang 프로젝트 열기**
3. **Assets/Scenes/WP_1129.unity 열기**

### 3️⃣ Unity Inspector 설정 (필수!)

#### A. DataManager 수정

**Hierarchy** → `DownloadCube_쾌` 선택 → **Inspector**

```
✅ 수정해야 할 값:
- Update Interval: 1800 → 600
- Update Distance Threshold: 5000 → 50
- Place List Manager: PlaceListManager 드래그
- Loading Indicator: LoadingIndicator 프리팹 드래그 (선택)
```

#### B. TourAPIManager 수정

**Hierarchy** → `DownloadCube_TourAPI_Petfriendly` 선택 → **Inspector**

```
✅ 수정해야 할 값:
- Update Interval: 3600 → 600
- Update Distance Threshold: 10000 → 50
- Load Radii: Size=6, 값=[25, 50, 75, 100, 150, 200]
- Place List Manager: PlaceListManager 드래그
- Loading Indicator: LoadingIndicator 프리팹 드래그 (선택)
```

#### C. PlaceListManager 수정

**Hierarchy** → `PlaceListManager` 선택 → **Inspector**

```
✅ 수정해야 할 값:
- List Panel: Canvas/ListPanel 드래그
- Max Display Distance: 1000 → 200
- Distance Slider: (UI 생성 후 연결)
- Distance Value Text: (UI 생성 후 연결)
```

#### D. Distance Slider UI 생성 (선택사항)

**ListPanel 안에 Slider 추가:**
1. Hierarchy → Canvas → ListPanel 선택
2. 우클릭 → UI → Slider
3. 이름: `DistanceSlider`
4. Inspector:
   - Min Value: 50
   - Max Value: 200
   - Value: 200

**Text 추가:**
1. Hierarchy → Canvas → ListPanel 선택
2. 우클릭 → UI → Text
3. 이름: `DistanceValueText`
4. Inspector:
   - Text: "200m"
   - Font Size: 50

**PlaceListManager에 연결:**
- Distance Slider: DistanceSlider 드래그
- Distance Value Text: DistanceValueText 드래그

### 4️⃣ 씬 저장

- **File → Save** (⌘S)

### 5️⃣ iOS 빌드 설정

#### A. Build Settings

**File → Build Settings** (⌘⇧B)

```
1. Platform: iOS 선택 → Switch Platform
2. Scenes In Build:
   ✅ Assets/Scenes/SplashScene.unity
   ✅ Assets/Scenes/WP_1129.unity
3. Player Settings... 클릭
```

#### B. Player Settings 확인

**iOS 탭 선택:**

```
Company Name: (확인)
Product Name: woopang

Other Settings:
- Camera Usage Description: "AR 기능을 위해 카메라 접근이 필요합니다"
- Location Usage Description: "주변 장소를 찾기 위해 위치 정보가 필요합니다"
- Target minimum iOS Version: 14.0 이상

Architecture: ARM64

Identification:
- Bundle Identifier: com.yourcompany.woopang
- Signing Team ID: (개발자 계정 팀 ID)

ARKit:
- ARKit Required: ✅ 체크
```

### 6️⃣ Xcode 빌드

#### A. Unity에서 Xcode 프로젝트 생성

**Build Settings → Build**

```
1. 폴더 선택: ~/Desktop/woopang_ios_build
2. Build 클릭 → 대기 (5-10분)
```

#### B. Xcode에서 열기

```bash
# 빌드 완료 후
open ~/Desktop/woopang_ios_build/Unity-iPhone.xcodeproj
```

#### C. Xcode 설정

**Signing & Capabilities:**
```
- Team: (개발자 계정 선택)
- Automatically manage signing: ✅ 체크
- Bundle Identifier: 고유한 ID (예: com.yourname.woopang)
```

**Build Settings → Architectures:**
```
- Architectures: arm64
- Valid Architectures: arm64
```

#### D. 디바이스 연결 및 빌드

```
1. iPhone/iPad를 Mac에 USB 연결
2. Xcode 상단 디바이스 선택
3. Product → Run (⌘R)
```

### 7️⃣ 테스트 체크리스트

디바이스에서 확인:

- [ ] 앱 실행 확인
- [ ] 카메라 권한 요청 확인
- [ ] 위치 권한 요청 확인
- [ ] AR 세션 시작 확인
- [ ] 데이터 로딩 확인 (LoadingIndicator 표시)
- [ ] AR 오브젝트 생성 확인 (25m부터 Progressive Loading)
- [ ] ListPanel 열기/닫기 확인
- [ ] Distance Slider 동작 확인 (생성한 경우)
- [ ] AR 오브젝트 거리 필터링 확인

## 🔍 문제 해결

### 문제 1: Xcode 빌드 에러 "Signing for ... requires a development team"

**해결:**
```
Xcode → Signing & Capabilities → Team 선택
개발자 계정이 없으면 Apple ID 추가:
Xcode → Preferences → Accounts → + → Apple ID 추가
```

### 문제 2: "Library not loaded: @rpath/UnityFramework.framework"

**해결:**
```
Build Settings 검색:
- Runpath Search Paths: @executable_path/Frameworks 확인
```

### 문제 3: ARSession 초기화 실패

**해결:**
```
1. Info.plist 확인:
   - NSCameraUsageDescription 있는지
   - NSLocationWhenInUseUsageDescription 있는지
2. iPhone 설정 → 개인정보 보호 → 카메라/위치 → 앱 권한 확인
```

### 문제 4: Progressive Loading이 작동하지 않음

**해결:**
```
Unity Inspector 확인:
- TourAPIManager → Load Radii 배열이 [25, 50, 75, 100, 150, 200]으로 설정되었는지
- Console에서 "[TourAPIManager] Progressive Loading 시작" 로그 확인
```

## 📚 참고 문서

프로젝트 루트의 마크다운 파일들 참고:

- **UNITY_INSPECTOR_SETUP_CHECKLIST.md** - Inspector 설정 체크리스트
- **MANAGER_SETTINGS_GUIDE.md** - 설정값 상세 가이드
- **DATA_LOADING_OPTIMIZATION_SUMMARY.md** - 최적화 설명
- **DISTANCE_FILTER_COMPLETE_GUIDE.md** - 거리 필터 가이드

## 🎯 빠른 체크리스트

맥북에서 할 일:

- [ ] `git pull origin main` (또는 `git reset --hard origin/main`)
- [ ] Unity에서 WP_1129.unity 열기
- [ ] DataManager Inspector 수정
- [ ] TourAPIManager Inspector 수정
- [ ] PlaceListManager Inspector 수정
- [ ] Distance Slider UI 생성 (선택)
- [ ] 씬 저장 (⌘S)
- [ ] iOS 빌드 (Build Settings → Build)
- [ ] Xcode에서 Signing 설정
- [ ] 디바이스 연결 및 실행

## 💡 팁

### Unity Cloud Build (선택사항)
자주 iOS 빌드하려면 Unity Cloud Build 사용 추천:
```
1. Unity Dashboard → Cloud Build 활성화
2. GitHub 연동
3. iOS 빌드 설정
4. 자동 빌드 활성화
```

### TestFlight 배포 (선택사항)
내부 테스터에게 배포:
```
1. Xcode → Product → Archive
2. Distribute App → App Store Connect
3. TestFlight → 내부 테스터 추가
```

---

## 수정 날짜
2025-11-29
