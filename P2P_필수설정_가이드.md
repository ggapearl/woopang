# WOOPANG P2P 시스템 - 필수 설정 가이드

## 🎯 개요

P2P 서버를 **메인 서버(app_improved.py)에 통합**하여 하나의 서버로 운영합니다.
- 기존: 메인 서버(5000) + P2P 서버(5001) 따로 실행 ❌
- 변경: 메인 서버(5000)에 WebSocket 통합 ✅

---

## ✅ 필수 설정 순서

### **1단계: 서버 통합 및 패키지 설치 (5분)**

```bash
# 1. 서버 폴더로 이동
cd c:\woopang\server

# 2. 필수 패키지 설치
pip install flask-socketio python-socketio

# 3. P2P 기능을 메인 서버에 통합
python integrate_p2p.py
```

**예상 출력:**
```
✅ 백업 완료: app_improved_backup_before_p2p.py
✅ P2P WebSocket 기능이 app_improved.py에 통합되었습니다!

필수 패키지 설치:
  pip install flask-socketio python-socketio

DB 초기화:
  curl -X POST http://210.105.65.145:5000/api/p2p/init_db
```

---

### **2단계: 기존 서버 종료 후 재시작 (2분)**

```bash
# 1. 기존 서버 프로세스 종료
taskkill /F /PID 129252

# (또는 Ctrl+C로 수동 종료)

# 2. 통합 서버 재시작
python app_improved.py
```

**정상 실행 시 로고 + 다음 메시지 확인:**
```
WOOPANG 좌표 기반 푸시 서버 시작 중...
[Info] 백그라운드 데이터 정리 스케줄러 시작됨 (24시간마다)
[P2P] Database initialized successfully  ← 이 메시지 확인!
```

---

### **3단계: DB 초기화 (1분)**

```bash
# user_sessions 테이블 생성
curl -X POST http://210.105.65.145:5000/api/p2p/init_db
```

**응답 확인:**
```json
{
  "status": "success",
  "message": "Database initialized"
}
```

---

### **4단계: Unity 자동 설정 (3분)**

Unity Editor에서:

1. **메뉴 실행**
   ```
   Unity 메뉴 → WOOPANG → Setup P2P System
   ```

2. **확인 대화상자에서 "설정 시작" 클릭**

3. **Console 로그 확인**
   ```
   [P2P Setup] =====시작=====
   [P2P Setup] ✓ 기본 폰트 로드: AppleSDGothicNeoM
   [P2P Setup] ✓ P2PManager 생성 완료
   [P2P Setup] ✓ P2P_User 프리팹 연결
   [P2P Setup] ✓ LoginPromptPanel 프리팹 연결
   [P2P Setup] ✓ UI Canvas 연결
   [P2P Setup] ✓ P2PProfilePanel 생성 완료
   [P2P Setup] =====완료=====
   ```

4. **완료 대화상자 확인**

---

### **5단계: SocketIO Unity 패키지 설치 (3분)**

Unity Package Manager에서:

1. **Window → Package Manager**
2. **"+" 버튼 → Add package from git URL**
3. **입력:**
   ```
   https://github.com/itisnajim/SocketIOUnity.git
   ```
4. **Add 클릭**

---

## 🧪 테스트

### **서버 상태 확인**

```bash
# P2P 서버 상태 확인
curl http://210.105.65.145:5000/api/p2p/status
```

**예상 응답:**
```json
{
  "status": "online",
  "active_users": 0,
  "total_sessions": 0,
  "server_time": "2026-01-02T..."
}
```

---

### **Unity 설정 상태 확인**

Unity 메뉴:
```
WOOPANG → Check P2P Setup Status
```

**예상 출력:**
```
=== P2P 시스템 상태 ===

✓ P2PManager: 존재
✓ LoginManager: 존재
✓ P2PProfilePanel: 존재
✓ P2P_User 프리팹: 존재
✓ 폰트: AppleSDGothicNeoM
```

---

### **실제 연결 테스트**

1. **Unity Play 버튼 클릭**
2. **로그인 진행**
3. **Console에서 다음 로그 확인:**
   ```
   [P2PManager] Connecting to WebSocket: ws://210.105.65.145:5000
   [P2PManager] Connected to P2P server (socket: abc123xyz)
   [P2PManager] User registered: [사용자명]
   ```

4. **서버 로그 확인:**
   ```
   [P2P] Client connected: abc123xyz
   [P2P] User registered: [사용자명] (user_xxx)
   ```

---

## 📝 작업 후 정리

### **불필요한 파일 정리**

```bash
# 서버 폴더
cd c:\woopang\server

# 독립 P2P 서버 파일 삭제 (이제 필요 없음)
del p2p_server.py

# 통합 스크립트 보관 (나중에 참고용)
# integrate_p2p.py는 그대로 두기
```

---

## 🔧 수동 설정이 필요한 경우

만약 자동 설정 스크립트가 실패하면:

### **Unity 수동 설정**

#### 1. P2PManager 수동 생성

1. Hierarchy 우클릭 → Create Empty
2. 이름: `P2PManager`
3. Inspector → Add Component → P2PManager
4. 설정:
   - Server URL: `ws://210.105.65.145:5000`
   - Enable P2P: ✓
   - Max Tracking Distance: `1000`
   - Position Update Interval: `5`
   - User Avatar Prefab: `Assets/Prefabs/P2P_User.prefab` 드래그

#### 2. LoginManager 수동 연결

1. Hierarchy에서 `LoginManager` 선택
2. Inspector 설정:
   - Login Prompt Panel Prefab: `Assets/Prefabs/LoginPromptPanel.prefab` 드래그
   - UI Canvas Root: Canvas GameObject 드래그

---

## 🚀 배포 체크리스트

프로덕션 배포 전:

- [ ] 서버 통합 완료 (`app_improved.py`에 P2P 기능 포함)
- [ ] DB 테이블 생성 (`user_sessions`)
- [ ] Unity P2PManager 설정 완료
- [ ] SocketIO 패키지 설치
- [ ] 테스트 계정으로 연결 테스트 완료
- [ ] 서버 로그에서 WebSocket 연결 확인

---

## ❓ 문제 해결

### **문제: "Module 'flask_socketio' not found"**

**해결:**
```bash
pip install flask-socketio python-socketio
```

---

### **문제: Unity에서 "P2PManager not found"**

**해결:**
```
Unity 메뉴 → WOOPANG → Setup P2P System 재실행
```

---

### **문제: WebSocket 연결 실패**

**원인 1: 서버가 실행되지 않음**
```bash
# 서버 상태 확인
curl http://210.105.65.145:5000/api/p2p/status

# 서버 재시작
cd c:\woopang\server
python app_improved.py
```

**원인 2: 방화벽 차단**
- Windows 방화벽에서 포트 5000 개방 확인

**원인 3: URL 오타**
- Unity P2PManager의 Server URL 확인: `ws://210.105.65.145:5000`
- `http://` 가 아니라 `ws://` 사용!

---

## 📊 시스템 아키텍처

```
Unity Client (WP_1218.unity)
├─ LoginManager ────────────┐
├─ P2PManager ──────────────┼─── WebSocket (ws://210.105.65.145:5000)
├─ P2PProfilePanel         │
└─ P2P_User (동적 생성)     │
                           │
                           ↓
Flask Server (app_improved.py:5000)
├─ REST API (기존 기능)
├─ WebSocket (P2P 실시간 통신)
└─ PostgreSQL (user_sessions 테이블)
```

---

## 📚 관련 파일

### **서버**
- `c:\woopang\server\app_improved.py` - 통합 메인 서버 (REST + WebSocket)
- `c:\woopang\server\integrate_p2p.py` - P2P 통합 스크립트

### **Unity 스크립트**
- `Assets/Scripts/P2P/P2PManager.cs` - P2P 매니저 (WebSocket 클라이언트)
- `Assets/Scripts/P2P/P2PUserInfo.cs` - 사용자 정보 표시
- `Assets/Scripts/P2P/P2PProfilePanel.cs` - 프로필 패널
- `Assets/Scripts/Editor/P2PAutoSetup.cs` - 자동 설정 에디터 스크립트

### **프리팹**
- `Assets/Prefabs/P2P_User.prefab` - P2P 사용자 아바타
- `Assets/Prefabs/LoginPromptPanel.prefab` - 로그인 프롬프트

---

## 🎓 핵심 포인트

1. **P2P 서버 = 메인 서버에 통합됨** (포트 5000 하나만 사용)
2. **WebSocket URL: `ws://210.105.65.145:5000`** (http가 아니라 ws!)
3. **Unity 자동 설정: `WOOPANG → Setup P2P System`** (메뉴 한 번 클릭)
4. **DB 초기화: 서버 재시작 후 자동 실행** (또는 API 호출)

---

**문제가 발생하면:**
```
Unity 메뉴 → WOOPANG → Check P2P Setup Status
```
로 현재 상태를 먼저 확인하세요!

**작성자:** Claude (Anthropic AI)
**최종 업데이트:** 2026-01-02
**버전:** 1.0.0 (통합 서버 버전)
