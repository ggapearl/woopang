# 서버 인프라 구조 (절대 함부로 변경 금지)

> 서버 포트, Nginx, 배포, 환경변수 관련 작업 시 참조

---

## 포트 구성
```
⚠️ 중요: 아래 포트 구성은 프로덕션 환경의 핵심. 변경 시 전체 서비스 장애 발생.

[Nginx - 리버스 프록시] 포트 443 (HTTPS)
├── /                → 127.0.0.1:8080  (메인 서버, Waitress)
├── /preview/        → 127.0.0.1:5555  (Preview 영상분석 서버)
├── /tire            → 127.0.0.1:2684  (타이어 거래소 / 오복상사)
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

[타이어 거래소] tire_server.py → 포트 2684

[농민.com] nongmin_server.py → 포트 6688
- 농산물 직거래 플랫폼 (판매자 상품 등록 / 구매자 주문)
- 자동시작: Startup 폴더 nongmin_server.bat (waitress, Windows Terminal 탭)
- 수동실행: C:\woopang\server\nongmin\run_nongmin_server.bat (Flask 개발서버)
- DB: ar_database 인스턴스, nongmin_ 접두사 테이블 4종
```

> 포트 단독 기록 (점유 현황):
> 443 nginx · 8080 메인 · 5555 preview · 2684 tire · 6688 nongmin · 4395 p2p · 5002 apple(구수한농장, Node)
> · 7000 livecommerce(FastAPI) · 7880 livekit-signal · 7881·7882 livekit-RTC(미디어, 방화벽 인바운드 개방 필요)
> 라이브커머스: livecommerce/start.bat 또는 Startup의 nongmin_server.bat 가 함께 기동 (LiveKit + FastAPI)

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
