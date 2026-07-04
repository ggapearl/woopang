# 서버 인프라 구조 (절대 함부로 변경 금지)

> 서버 포트, Nginx, 배포, 환경변수 관련 작업 시 참조

---

## 포트 구성
```
⚠️ 중요: 아래 포트 구성은 프로덕션 환경의 핵심. 변경 시 전체 서비스 장애 발생.

[Nginx - 리버스 프록시] 포트 443 (HTTPS)
├── /                → 127.0.0.1:8080  (메인 서버, Waitress)
├── /preview/        → 127.0.0.1:5555  (Preview 영상분석 서버)
├── /nongmin         → 127.0.0.1:6688  (농민.com 농산물 직거래)
├── /api/p2p/        → 127.0.0.1:4395  (P2P 실시간 위치 서버)
├── /live            → 127.0.0.1:7000  (라이브커머스 웹앱 - FastAPI)
├── /live/rtc        → 127.0.0.1:7880  (LiveKit 시그널링 WebSocket)
└── SSL 인증서: C:/woopang/server/woopang.com-fullchain.pem

[메인 서버] app_improved.py → Waitress 포트 8080
- .env에 USE_NGINX=true 필수 (없으면 Flask가 443 직접 점유 → Nginx와 충돌)
- DB, 인증, DM, 댓글, 팔로우, 기획안 라우팅 등 모든 API 처리
- /preview 경로 프록시 로직도 있지만, Nginx가 직접 5555로 보냄

[Preview 서버] preview_run.py → Flask 포트 5555
- 영상 업로드, 비용 예측, 분석 결과 조회
- preview_worker.py: RQ Worker (Redis 큐 기반 비동기 영상 분석)

[P2P 서버] 포트 4395
- 실시간 위치 추적 + SpeechBubble

[농민.com] nongmin_server.py → 포트 6688
- 농산물 직거래 플랫폼 (판매자 상품 등록 / 구매자 주문)
- 자동시작: Startup 폴더 nongmin_server.bat (waitress, Windows Terminal 탭)
- 수동실행: C:\woopang\server\nongmin\run_nongmin_server.bat (Flask 개발서버)
- DB: ar_database 인스턴스, nongmin_ 접두사 테이블 4종
```

> 포트 단독 기록 (점유 현황):
> 443 nginx · 8080 메인 · 5555 preview · 6688 nongmin · 4395 p2p · 5002 apple(구수한농장, Node)
> · 5010 dongdong(쾌클라우드) · 5020 board-monitor · 7788 portpolio · 5001 sogogi
> · 7000 livecommerce(FastAPI) · 7880 livekit-signal · 7881·7882 livekit-RTC(미디어, 방화벽 인바운드 개방 필요)
> 라이브커머스: livecommerce/start.bat 또는 Startup의 nongmin_server.bat 가 함께 기동 (LiveKit + FastAPI)
>
> ⚠️ 2026-07 제거: `tire`(타이어 거래소, 포트 2684)·`vrompt`(포트 8976) 서비스는 미사용으로
>    라우팅 전면 삭제 (app_improved.py 라우트 + nginx location + bookmark 등록 모두 제거).
>    `/api/<path>` vrompt catch-all 프록시도 함께 삭제됨. `vdown`(5005)은 계속 사용 중.

---

## Nginx 설정 변경 시 주의사항
```
⚠️ 절대 금지: 기존 location 블록의 proxy_pass 포트 변경
⚠️ 절대 금지: client_max_body_size를 줄이는 것 (현재 http 블록 500M, /preview/ 6G)

설정 파일: C:\nginx\conf\nginx.conf

변경 후 반드시:
1. nginx -t  (문법 검사)
2. nginx -s reload  (적용)

사고 사례 (2026-03-25):
- /preview/ location 추가 시 USE_NGINX=true 미설정 상태에서
  Flask가 443 직접 점유 + Nginx도 443 점유 → 포트 충돌 → woopang.com 접속 불가
- 원인: 메인 서버 .env에 USE_NGINX=true가 없어서 Waitress(8080) 대신
  Flask SSL 모드(443)로 실행됨
- 해결: .env에 USE_NGINX=true 추가 → Waitress(8080)로 전환
```

---

## 서버 실행 방법
```
메인 서버: python app_improved.py (USE_NGINX=true → Waitress 8080)
Preview 서버: python preview_run.py (Flask 5555)
Preview Worker: python preview_worker.py (RQ Worker, Redis 큐 리스닝)
  - bat 파일 (C:\woopang\server\옛스크립트\preview_server.bat)은 Python312 사용
  - 수동 실행 시 PATH의 Python 사용 (Python314)
  - 패키지 설치 시 두 Python 환경 모두 확인 필요

Nginx: C:\nginx\nginx.exe
  - 시작: nginx
  - 리로드: nginx -s reload
  - 중지: nginx -s stop
```

---

## 🔄 서버 재시작 방식 (중요)

```
⚠️ 기본 원칙: app_improved.py를 "재시작"할 일이 있으면 — 재실행하지 말고
    그냥 현재 실행 중인 터미널 창을 닫기만 한다.

로컬에 자동 감시/복구 로직이 이미 돌고 있어서, app_improved.py 터미널이
닫히면 자동으로 새 터미널에서 다시 실행된다. 즉 "껐다 켜기" = "터미널 창 닫기".

✅ 올바른 방식:
- 로컬에서 app_improved.py가 돌고 있는 "터미널 창 자체"를 X버튼 또는
  Ctrl+C 후 창 닫기로 종료 → 자동 복구 감시가 새 창을 띄우며 재기동
- 사용자(대표)가 직접 창을 닫는 방식이 표준 (이미 feedback 메모에 기록됨)

❌ 하지 말 것:
- taskkill, kill, Stop-Process 같은 커맨드로 프로세스만 죽이기
- 백그라운드로 python app_improved.py & 재실행 (자동 복구 로직과 충돌)
- 새 터미널에서 서버 또 띄우기 (포트 충돌)
- Claude가 임의로 서버 프로세스 종료 시도 (터미널 창 제어 불가 → 사용자에게 부탁)

Claude의 역할:
- "서버 꺼줘" 요청을 받으면 직접 종료 시도하지 말고
  사용자에게 "해당 app_improved.py 터미널 창을 닫아주세요"라고 안내할 것
- 코드 변경 후 서버 반영이 필요하면 동일하게 창 닫기 안내

서버 껐다 켜는 로직을 가장 잘 아는 AI worker: 박광태 (기획팀)
```

### ⚠️ 자동복구 동작 수정 (2026-07)
```
[FIXED] single_server_restart.py 의 kill_all_servers() 가 과거 `taskkill /IM python.exe`
        로 머신의 모든 파이썬 서버(nongmin·preview·dongdong·p2p·worker)를 몰살시켜,
        메인서버 자동복구 1회에 다른 서비스가 전부 죽고 되살아나지 않는 장애가 있었음.
        → cmdline 에 app_improved.py 가 있는 프로세스만 psutil 로 정확히 종료하도록 변경
          (다른 파이썬 서버와 nginx(443) 은 보존).

[미해결 - 별도 검토 필요] single_server_restart.py 는 아직 구(舊) '443 단독 SSL' 아키텍처
        가정으로 작성돼 있음(main_port=443, is_port_healthy(443)). 현재는 nginx(443)+Waitress(8080)
        구조라, start_main_server() 의 kill_port_processes(443) 이 nginx 를 죽일 수 있음.
        모니터를 현재 구조(8080 헬스체크)에 맞추는 리팩터링은 별도 작업으로 진행 권장.
```

### 🎥 dongdong(쾌클라우드) 영상 스트리밍 라우팅 — 개선 권고 (미적용)
```
현재: 클라이언트 → nginx(443) /dongdong → 메인 app_improved.py(8080) 프록시 → dongdong.py(5010) → 디스크
문제: 영상 바이트가 메인서버를 한 번 더 경유하며, 재생 내내 메인 Waitress 스레드(20개)를
      1개씩 점유 → 동시 시청 늘면 우팡 앱 본체(위치·DM·로그인)까지 스레드 고갈로 느려짐.
권고(택1):
 (a) nginx location /dongdong 을 8080 대신 5010 직결 (nongmin·preview 처럼). 단, dongdong.py
     라우트가 /dongdong 프리픽스를 기대하지 않으므로 `proxy_pass http://127.0.0.1:5010/;`
     (trailing slash) 로 프리픽스 스트립 + `proxy_buffering off; client_max_body_size 10G;` 필요.
     ⚠ 실브라우저 재생/업로드/리다이렉트 검증 후 반영할 것 (server-infra 절대금지 규칙 관련).
 (b) 다운로드(재생)는 nginx alias 로 저장폴더 직접 서빙(sendfile+Range) → Python 완전 우회.
```

---

## 환경변수 (.env)
```
메인 서버: C:\woopang\server\.env
  - USE_NGINX=true (필수! 없으면 443 충돌)
  - DB_PASSWORD, FLASK_SECRET_KEY, SLACK_WEBHOOK_URL 등

Preview 서버: C:\woopang\server\preview\.env
  - GEMINI_API_KEY, GEMINI_MODEL (gemini-3-pro-preview)
  - CLAUDE_API_KEY, CLAUDE_MODEL
  - DB_PASSWORD
```

---

## 🚀 부팅 자동시작 bat (Startup 폴더)

```
위치: C:\Users\pdnom\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\
⚠️ git 추적 안 됨 (사용자 프로필 안). 수정 전 .bak 백업 권장.
```

부팅 시 **4개 런처 bat**이 각자 서버를 띄운다. 각 bat은 "런처 창"이고, 실제
서버는 별도 `wt.exe` 탭 / `cmd /k` 자식창에서 돈다 → **런처 창을 닫아도
서버는 계속 실행**된다 (자식 프로세스 분리).

| bat 파일 | 띄우는 서버 | 권한 | 서버 실행 위치 |
|----------|-------------|------|----------------|
| `woopang_server.bat` | nginx(443) + Waitress(8080) + smart_monitoring | (직접) | `start` 별도 창 + nginx.exe |
| `woopang_sub.bat` | Sogogi · Apple · Portfolio(7788) · DongDong(5010) · Board Monitor(5020) | 관리자(UAC) | wt.exe 5개 탭 |
| `nongmin_server.bat` | 농민(6688) · LiveKit(7880~7882) · LiveCommerce(7000) | (직접, 포트≥1024) | wt.exe 3개 탭 |
| `preview_server.bat` | Preview(5555) + Worker ×4 | 관리자(UAC) | wt.exe 5개 탭 |

### 런처 창 자동닫힘 (2026-06 적용)
- 부팅 후 4개 런처 창은 서버 기동 후 **5초 뒤 자동으로 닫힌다** (`timeout /t 5 → exit`). 사용자가 직접 닫을 필요 없음.
- **예외**: `woopang_server.bat`은 nginx/Waitress **기동 실패 시**(`:manual_help`) 창이 닫히지 않고 `pause`로 멈춰 원인을 보여줌. 성공 시에만 자동닫힘.
- 서버 창(wt 탭 / cmd /k)은 그대로 유지 — **닫으면 서버가 죽으므로 닫지 말 것.**

### ⚠️ 알려진 미해결 이슈 — `%1=="startup"` 부팅 대기 미작동
- 4개 bat에 `if "%1"=="startup" ( timeout 10~12초 )` 부팅 대기 + `if NOT "%1"=="startup" pause` 분기가 설계돼 있으나, **Startup 폴더는 bat을 인자 없이 실행**해서 `%1`이 비어 있음 → 부팅 대기 로직이 트리거되지 않는다.
- 자동닫힘은 위 분기와 무관하게 무조건 닫히도록 고쳤으므로 영향 없음. 다만 부팅 직후 서비스 기동 순서/타이밍 문제가 생기면 이 대기 로직을 살리는 작업이 필요 (Startup에 인자 전달하려면 .lnk 바로가기 또는 오케스트레이터 bat 필요).
```
