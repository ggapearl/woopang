# 스크롤 & 깜빡임 문제 진단 가이드

## 솔직한 상황

지금까지 추측으로 수정했지만 실제 작동하지 않았습니다.
**이제 정확한 원인을 찾기 위해 디버깅을 시작합니다.**

---

## 1단계: 디버거 설치

### Unity에서 설정:

**ListPanel/Scroll View 오브젝트에:**
1. `Add Component` → `Scroll Debugger` 추가
2. Inspector 설정:
   - Enable Debug: ✅ ON
   - Log To File: ✅ ON
   - Max Log Count: `200`

**이미 있는 SmoothScrollRect 컴포넌트:**
- Enable Debug Log: ✅ ON

---

## 2단계: 빌드 및 테스트

### 빌드:
```bash
# Unity에서 Android 빌드
# Development Build 체크
# Script Debugging 체크
```

### 테스트 시나리오:
1. **앱 시작** → 스플래시 확인 (깜빡임 체크)
2. **ListPanel 열기** → 버튼 클릭
3. **천천히 스와이프** (5cm 정도) → 반응 확인
4. **빠르게 스와이프** → 관성 확인
5. **연속 스와이프** (3-4번) → 멈춤 현상 확인

---

## 3단계: 로그 수집

### Logcat으로 실시간 확인:
```bash
# 터미널에서 실행
adb logcat -s Unity

# 또는 특정 태그만:
adb logcat | grep -E "SmoothScroll|ScrollDebug|LAG"
```

### 찾아야 할 패턴:

#### 🔴 **스크롤 멈춤 문제:**
```
[SmoothScroll] LAG! FrameTime=50.2ms, Delta=(0.0, -25.3)
```
→ **원인**: 프레임 시간이 16ms 이상 (60fps 미만)

#### 🔴 **이벤트 전달 안됨:**
```
[SmoothScroll] BeginDrag - Pos=(540, 960)
[SmoothScroll] ScrollRect is NULL!
```
→ **원인**: ScrollRect 컴포넌트 누락

#### 🔴 **EventSystem 충돌:**
```
[ScrollDebug] EventSystem 개수: 2
```
→ **원인**: EventSystem이 2개 이상

#### 🔴 **메모리 부족:**
```
[ScrollDebug] Memory=450.2MB
```
→ **원인**: 메모리 300MB 이상이면 위험

---

## 4단계: 파일 로그 확인

### 디바이스에서 로그 파일 가져오기:
```bash
# 로그 파일 위치 확인
adb shell ls /storage/emulated/0/Android/data/com.yourcompany.woopang/files/

# 로그 파일 다운로드
adb pull /storage/emulated/0/Android/data/com.yourcompany.woopang/files/scroll_debug.log
```

### 로그 분석:

**정상적인 스크롤:**
```
[2.453] [BEGIN_DRAG] Pos=(540, 1200), Delta=(0, 0)
[2.468] [DRAG] FrameTime=15.2ms, Delta=(0, -12)
[2.484] [DRAG] FrameTime=16.1ms, Delta=(0, -18)
[2.500] [DRAG] FrameTime=15.8ms, Delta=(0, -15)
[2.620] [END_DRAG] TotalTime=0.167s, Frames=8, AvgFrame=15.4ms, FPS=64.9
```

**문제 있는 스크롤 (멈춤):**
```
[3.123] [BEGIN_DRAG] Pos=(540, 1200)
[3.138] [DRAG] FrameTime=15.1ms, Delta=(0, -10)
[3.195] [DRAG_LAG] FrameTime=57.2ms (>16ms!), Delta=(0, -5) ← 문제!
[3.212] [DRAG] FrameTime=17.1ms, Delta=(0, -8)
```
→ 57ms = **프레임 드롭 발생**

---

## 5단계: 원인별 해결책

### 원인 1: EventSystem 중복
**증상**: 터치 반응 없음 또는 간헐적
**해결**:
```bash
# Logcat 확인
[ScrollDebug] EventSystem 개수: 2 ← 문제!

# Unity Hierarchy에서 확인
- EventSystem (Main)
- EventSystem (1) ← 삭제!
```

### 원인 2: Raycaster 과다
**증상**: 터치 시 프레임 드롭
**해결**:
```bash
# Logcat 확인
[ScrollDebug] Raycasters=5 ← 많음!

# 필요 없는 Canvas의 GraphicRaycaster 제거
- ListPanel Canvas: ✅ 유지
- AR Camera Canvas: ❌ 제거
- MainCanvas: ✅ 유지
```

### 원인 3: FindObjectOfType 남용
**증상**: 스크롤 중 멈춤 (프레임 50ms+)
**해결**:
```bash
# Logcat 확인
[DRAG_LAG] FrameTime=87.3ms ← 매우 느림!

# SimpleSafeAreaManager.cs 수정 필요
- FindObjectOfType → 캐싱으로 변경
```

### 원인 4: Layout Rebuild
**증상**: 스크롤 시작할 때 멈춤
**해결**:
```bash
# Content의 Vertical Layout Group 설정
- Child Force Expand Height: OFF ✅
- Child Control Size Height: OFF ← 추가 확인
```

### 원인 5: AR 카메라 간섭
**증상**: AR 화면에서만 느림
**해결**:
```bash
# ListPanel Canvas 설정
- Sort Order: 100 (높게 설정)
- Blocking Mask: Everything

# AR Camera에 Physics Raycaster 있으면 제거
```

---

## 6단계: 로그 공유

로그를 확인한 후 다음을 알려주세요:

1. **스크롤 평균 프레임 시간**: `[END_DRAG]`에서 `AvgFrame` 값
   - ✅ 정상: 15-16ms
   - ⚠️ 경고: 17-25ms
   - 🔴 문제: 25ms 이상

2. **LAG 발생 빈도**: `[DRAG_LAG]` 로그 개수
   - ✅ 정상: 0개
   - ⚠️ 경고: 1-2개
   - 🔴 문제: 3개 이상

3. **EventSystem 상태**: `[STATS]`에서 `EventSystem` 및 `Raycasters` 값

4. **메모리 사용량**: `[STATS]`에서 `Memory` 값
   - ✅ 정상: 150MB 이하
   - ⚠️ 경고: 150-300MB
   - 🔴 문제: 300MB 이상

---

## 빠른 체크리스트

빌드 전에 확인:
- [ ] ScrollDebugger 컴포넌트 추가됨
- [ ] SmoothScrollRect의 Enable Debug Log = ON
- [ ] Development Build 체크됨
- [ ] Script Debugging 체크됨

테스트 중 확인:
- [ ] Logcat 실행 중 (`adb logcat -s Unity`)
- [ ] 스크롤 3번 이상 테스트
- [ ] 로그 파일 생성 확인

---

## 정확한 원인을 찾으면 즉시 고칠 수 있습니다

**이제 추측이 아닌 데이터로 접근합니다.**

로그 결과를 공유해주시면:
1. 정확한 병목 지점 파악
2. 맞춤형 해결책 제시
3. 확실한 수정 보장

**디버깅 없이는 더 이상 진전이 없습니다. 로그를 봐야 합니다.**
