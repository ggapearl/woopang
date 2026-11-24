# 서버 인프라 구조 (절대 함부로 변경 금지)

> 최종 갱신: 2026-11-15
> 서버 포트, Nginx, 배포, 환경변수 관련 작업 시 참조

---

## 포트 구성
```
⚠️ 중요: 아래 포트 구성은 프로덕션 환경의 핵심. 변경 시 전체 서비스 장애 발생.

[Nginx - 리버스 프록시] 포트 443 (HTTPS)
├── /                → 127.0.0.1:8080  (메인 서버, Waitress)
├── /yougotdog       → 127.0.0.1:8080  (내샛기지망생 — 메인 blueprint. 별도 포트·WT 탭 없음)
├── /hyodingsseu/    → 127.0.0.1:8000/AI/desk-app/  (효딩쓰 휴대폰 앱 화면 — 아이폰·안드로이드 포장지가 연다. 2026-10-01 nginx 추가,
│                      서빙은 배포본 server/hyodingsseu_web ← launchers\deploy_hyodingsseu_web.py 가 origin/main 에서 꺼내 넣음)
├── /cloud           → 127.0.0.1:8080  (쾌클라우드 파일서버 — 메인의 프록시 경유 → 5010)
├── /preview/        → 127.0.0.1:5555  (Preview 영상분석 서버)
├── /nongmin         → 127.0.0.1:6688  (농민.com 농산물 직거래)
├── /api/p2p/        → 127.0.0.1:4395  (P2P 실시간 위치 서버)
├── /woogy/          → 127.0.0.1:6690  (우기 시험 서버 — C:\woogy-app, DB 는 우기 전용 5655. 2026-11-15 추가.
│                      출시 전이라 입장 코드(.env WOOGY_TEST_GATE)를 먼저 묻고 noindex. 아이폰 포장지 apps/woogy-ios 가 연다.
│                      옛 주소 /woogi, /woogi/… 는 같은 자리의 /woogy/… 로 301 — 같은 날 영문 이름을 woogy 로 바꿈)
├── /live            → 127.0.0.1:7000  (라이브커머스 웹앱 - FastAPI)
├── /live/rtc        → 127.0.0.1:7880  (LiveKit 시그널링 WebSocket)
└── SSL 인증서: win-acme 관리 PEM (아래 'SSL 인증서' 절 참조)

[농민.com 전용 도메인] xn--980bx1r.com (+www) — 2026-07-13 연결
├── nginx 별도 server 블록(443) → 127.0.0.1:6688 루트 직결 (/live·/live/rtc 미러 포함)
├── 기존 woopang.com/nongmin 경로도 그대로 병행 서빙
├── DNS: GoDaddy A @→210.105.65.145, CNAME www→@
└── .env: NONGMIN_SITE_URL=https://xn--980bx1r.com ·
    NONGMIN_OAUTH_BASE=https://woopang.com/nongmin (카카오 콘솔에 새 URI 등록 후 삭제)

※ 2026-07 경로 개편 (2026-07-17 nginx 실반영 완료): 기존 /dongdong → **/cloud** 로 변경.
  nginx `location /cloud` 에 client_max_body_size 10G + 600s 타임아웃 + proxy_request_buffering off
  적용 (그 전까지는 10G 설정이 죽은 /dongdong 블록에 남아 있어 /cloud 업로드가 기본 500M 에 걸렸음).
  /dongdong 은 `rewrite ... permanent` 로 /cloud 301 (기존 공유 링크·북마크 보존).
  서버 파일도 dongdong.py → **cloud_server.py**, 메인 프록시 함수도 cloud_proxy 로 개명 (2026-07-17).
  ⚠ /cloud 도 메인(8080) 경유라 영상 스트리밍이 메인 Waitress 스레드(20개)를 점유함 —
    동시 시청 급증 시 nginx→5010 직결(메인 우회)로 최적화 여지 있음.

[메인 서버] app_improved.py → Waitress 포트 8080
- .env에 USE_NGINX=true 필수 (없으면 Flask가 443 직접 점유 → Nginx와 충돌)
- /chang · /yougotdog 같은 가벼운 웹앱은 메인에 blueprint 로 붙인다 — 새 포트·새 WT 탭·런처 등록 불필요
  (2026-09-28 대표님 지시). 코드: server/<앱>/<앱>_server.py + app_improved.py 에 import·register 2줄.
- /yougotdog 의 사진 분석·합성은 기본이 Gemini API(.env YGD_GEMINI_API_KEY, YGD_DAILY_CAP) — 메인 스레드풀에서 호출.
  YGD_ENGINE=local 일 때만 로컬 GPU 작업자(C:\ygd-gen\.venv + yougotdog/gen/gen_worker.py)를 메인이 창 없이 띄우고,
  10분 유휴·메인 종료 시 스스로 끝나 VRAM 을 비운다. 감시기·런처 등록 대상 아님.
- ⚠ 메인 .env 의 GEMINI_API_KEY 는 2026-09-28 기준 "API key not valid" (Preview .env 키만 유효). 이 키를 쓰는 기능이 있으면 확인 필요.
- DB, 인증, DM, 댓글, 팔로우, 기획안 라우팅 등 모든 API 처리
- /preview 경로 프록시 로직도 있지만, Nginx가 직접 5555로 보냄

[Preview 서버] preview_run.py → Flask 포트 5555
- 영상 업로드, 비용 예측, 분석 결과 조회
- preview_worker.py: RQ Worker (Redis 큐 기반 비동기 영상 분석)

[P2P 서버] 포트 4395
- 실시간 위치 추적 + SpeechBubble

[농민.com] nongmin_server.py → 포트 6688
- 농산물 직거래 플랫폼 (판매자 상품 등록 / 구매자 주문)
- 자동시작: Startup `QUE_Core.bat` → `launchers\que_core.bat` (WT "que-core" 창 탭, 2026-07-16 개편)
- 수동실행: C:\woopang\server\nongmin\run_nongmin_server.bat (Flask 개발서버)
- DB: ar_database 인스턴스, nongmin_ 접두사 테이블 4종
```

> 포트 단독 기록 (점유 현황):
> 443 nginx · 8080 메인 · 5555 preview · 6688 nongmin · 4395 p2p · 5002 apple(구수한농장, Node)
> · 5010 쾌클라우드(구 dongdong, 웹경로는 /cloud) · 5020 board-monitor · 7788 portpolio · 5001 sogogi
> · 7000 livecommerce(FastAPI) · 7880 livekit-signal · 7881·7882 livekit-RTC(미디어, 방화벽 인바운드 개방 필요)
> 라이브커머스: livecommerce/start.bat 또는 Startup 의 QUE_Core.bat 가 함께 기동 (LiveKit + FastAPI)
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

## SSL 인증서 (win-acme 자동갱신 — 2026-07-13 정비)
- 도구: C:\win-acme\wacs.exe (Let's Encrypt, http-01 SelfHosting — http.sys 80 공존 가능, 발급/설정변경은 관리자 필요)
- PEM 출력: C:\woopang\server\<도메인>-chain.pem(풀체인)/-key.pem — nginx 는 반드시 이 파일들을 참조
  ⚠ 구 -fullchain.pem/-privkey.pem 은 수동 복사본이라 갱신 반영 안 됨 (7/23 만료위기 원인이었음 — 사용 금지)
- 갱신 renewal: 아래 「IIS 가 443 을 채가는 사고」 절의 「현재 win-acme 갱신 4개」 참고 (IIS 설치 갱신은 2026-09-29 에 없앰)
- 자동화 3중: ① win-acme 자체 스케줄 태스크(SYSTEM) ② "win-acme renew + nginx reload" 03:40(SYSTEM,
  C:\win-acme
enew-and-reload.bat) ③ "nginx daily reload (cert apply)" 03:50(사용자) — 갱신분 반영용
- 수동 갱신: 관리자 cmd에서 `C:\win-acme\wacs.exe --renew` 후 `C:
ginx
ginx.exe -p C:/nginx -s reload`

## www → non-www 통일 (canonical, 2026-07-13)
- 우팡·농민 모두 **www 없는 주소로 통일** (http/https × www 8가지 경우 전부 → https://<apex>)
- http: IIS 리다이렉트 사이트(redir-woopang / redir-nongmin, 포트 80)가 처리 —
  Windows IIS가 80을 점유(adminer 서빙) 중이라 nginx가 80을 못 받아 IIS로 대체.
  ⚠ 한글 도메인은 IIS 바인딩을 **유니코드('농민.com')로** 넣어야 시작됨(퓨니코드는 start 실패).
  재생성 스크립트: C:\woopang\server\iis-redirect\RUN-AS-ADMIN.bat (관리자)
- https: nginx에 www 전용 server 블록(각 도메인) → `return 301 https://<apex>$request_uri`
- ⚠ woopang.com 인증서는 원래 www 미포함이라 www https가 깨졌음 → win-acme로 www 포함 재발급
  (`--host "woopang.com,www.woopang.com"`). **www 없는 구 renewal(N_VwW9…)은 삭제** —
  같은 PemFiles 경로를 덮어써 www가 다시 깨질 위험이 있었음. 현재 유효 renewal: KyB3…(woopang+www).

## IIS 가 443 을 채가는 사고 — 재발 경로와 막은 방법 (2026-09-29)
- 원인: win-acme 의 **[IIS] 설치 갱신 2개**(`0U20hPTb…` any site · `52Cdq…` Default Web Site). IIS 에 호스트 바인딩이 있는 사이트가
  켜져 있으면 갱신이 성공해 **IIS 에 https:443 바인딩을 심는다** → nginx 가 재시작하는 순간 IIS 가 443 을 뺏어 전체 다운.
- 이력: 07-14 복사만 해서 안 꺼짐 → 09-15 사고 후 `--cancel` 했지만 백업을 원래 이름으로 둬 되살아남 → **09-22 03:40 에 다시 443 바인딩을 심음**
  (09-24 부터 `http://woopang.com` 이 농민.com 으로 301) → **09-29 `server/iis-redirect/FIX-WOOPANG-HTTP-RUN-AS-ADMIN.bat`** 로 조치.
- 조치 순서(바꾸지 말 것): IIS 갱신 끄기(백업은 `.DISABLED` 이름) → IIS https 바인딩·http.sys `:443` sslcert 삭제 → 그다음 `redir-woopang` 을
  http 80 전용으로 켜기 → 443=nginx 확인. 로그: `server/iis-redirect/logs/fix-woopang-http-*.log` · `fix-woopang-http-result.json`.
- **자동 감시(2026-09-29)**: `server/launchers/guard443.py` — 헬스체크(작업 스케줄러 30분, 이상하면 텔레그램 「443 지킴이」) ·
  que-core 첫 탭 STATUS 의 `[GUARD 443]`(1분) · 효딩쓰 `server_status` 가 같은 검사를 쓴다. 빨간 줄이면 아래 bat.
- 재발 확인(관리자 권한 없이): 활성 폴더에 `0U20hPTb…`/`52Cdq…` 파일이 있는지 · `netsh http show sslcert` 에 `:443` 이 있는지 ·
  `netsh http show servicestate view=requestq` 에 `HTTPS://` 가 있는지. 하나라도 있으면 위 bat 을 다시(여러 번 돌려도 안전).
- 현재 win-acme 갱신 4개: `KyB3NPNh…` woopang.com+www(→ `C:\woopang\server` PEM, nginx 가 씀) · `_bUhheX2…` 농민.com(同) ·
  `hQByeTj3…`/`wONxve7Y…` 「pdnom」 woopang.com(→ `C:\` 에 따로 저장 — nginx PEM 은 안 건드림). 모두 SelfHosting 검증 + 설치 단계 없음.
- `redir-woopang` web.config: `destination="https://woopang.com$S$Q" exactDestination="true" childOnly="false"` — `/AI/` 같은 하위 경로·질의까지 유지.
  ⚠ PowerShell 로 쓸 때는 작은따옴표 here-string(`@' … '@`) — 큰따옴표면 `$S$Q` 가 빈 변수로 사라진다.

## 농민.com 서버 자동복구 (2026-07-13)
- smart_monitoring_system.py 가 woopang.com 과 함께 농민.com(https://xn--980bx1r.com/health)을 10초 주기 감시
- 외부 실패 시 로컬 6688 재확인으로 '서버 다운 vs nginx/DNS/SSL 문제'를 구분 —
  서버 다운(연속 3회)일 때만 Startup bat 과 동일한 waitress 커맨드로 자동 재기동 (시도 3회 제한)
- 모니터 코드 수정 후에는 smart_monitoring 실행 창을 재시작해야 반영됨

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
        로 머신의 모든 파이썬 서버(nongmin·preview·cloud(구 dongdong)·p2p·worker)를 몰살시켜,
        메인서버 자동복구 1회에 다른 서비스가 전부 죽고 되살아나지 않는 장애가 있었음.
        → cmdline 에 app_improved.py 가 있는 프로세스만 psutil 로 정확히 종료하도록 변경
          (다른 파이썬 서버와 nginx(443) 은 보존).

[미해결 - 별도 검토 필요] single_server_restart.py 는 아직 구(舊) '443 단독 SSL' 아키텍처
        가정으로 작성돼 있음(main_port=443, is_port_healthy(443)). 현재는 nginx(443)+Waitress(8080)
        구조라, start_main_server() 의 kill_port_processes(443) 이 nginx 를 죽일 수 있음.
        모니터를 현재 구조(8080 헬스체크)에 맞추는 리팩터링은 별도 작업으로 진행 권장.
```

### 🎥 cloud(쾌클라우드) 영상 스트리밍 라우팅 — 개선 권고 (미적용)
```
현재: 클라이언트 → nginx(443) /cloud → 메인 app_improved.py(8080) cloud_proxy → cloud_server.py(5010) → 디스크
문제: 영상 바이트가 메인서버를 한 번 더 경유하며, 재생 내내 메인 Waitress 스레드(20개)를
      1개씩 점유 → 동시 시청 늘면 우팡 앱 본체(위치·DM·로그인)까지 스레드 고갈로 느려짐.
권고(택1):
 (a) nginx location /cloud 를 8080 대신 5010 직결 (nongmin·preview 처럼). 단, cloud_server.py
     라우트가 /cloud 프리픽스를 기대하지 않으므로 `proxy_pass http://127.0.0.1:5010/;`
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

**2026-07-16 개편: WT 창 2개 체제.** Startup 폴더에는 얇은 호출 bat 2개만 있고
(`call ... startup` 로 인자를 넘겨 부팅 대기 로직도 정상 작동), 실제 로직은
`C:\woopang\server\launchers\` 에 있다. 각 런처는 Windows Terminal **이름 있는 창**
(`wt -w que-core` / `-w que-sub`)에 색상 탭으로 서버를 모은다. 런처 창은 자동으로
닫히고, 서버는 WT 탭에서 돈다.

| Startup bat | 실제 로직 (launchers\) | WT 창 | 탭 |
|-------------|------------------------|-------|-----|
| `QUE_Core.bat` | `que_core.bat` | `que-core` | STATUS(상태판) · Main 8080 · Monitor · Nongmin 6688 · LiveKit 7880 · LiveCommerce 7000 |
| `QUE_sub.bat` | `que_sub.bat` | `que-sub` | Apple 5002 · Portfolio 7788 · Cloud 5010 · BoardMon 5020 · Preview 5555 · Worker ×4 |

- ⚠️ **bat 파일은 ASCII 전용 — 한글 절대 금지.** 2026-07-17 부팅 전서버 다운 사고: bat 안의
  한글(UTF-8)이 cmd 의 줄 파싱을 어긋나게 해("eKit"·"ssApplicationTitle" 식으로 명령이 중간에 잘림)
  런처가 STEP 1 도 못 가고 중단 → 부팅 시 서버 0개 기동. 메시지·주석 전부 영문. `chcp 65001` 불필요.
  지연은 `timeout /t` 대신 **`ping -n N 127.0.0.1 >nul`** (timeout.exe 는 stdin 리다이렉트 시 즉시 실패).
- **관리자 승격 없음** — 전 포트 ≥1024. 구 bat(woopang_sub/preview)의 UAC 승격은 불필요했음이
  확인되어 제거 (승격 탓에 WT 창이 관리자/일반으로 갈라져 합칠 수 없었음).
- 구 bat 4개(woopang_server/nongmin_server/woopang_sub/preview_server)는
  `launchers\old_startup_backup\` 에 백업.
- 프로세스 정리는 전부 **포트 리스너 PID 단위** (`:killport`) + preview_worker 는 커맨드라인 매칭.
  `taskkill /IM python.exe·node.exe` 금지 (2026-07-14 몰살사고 재발 방지).
- Monitor 탭은 8080·6688 헬스체크 **통과 후** 추가 (smart_monitoring 자동재시작과의 이중 기동 경합 방지).
- 기동 실패 시(`:manual_help`) que_core 런처 창은 `pause` 로 멈춰 원인을 보여줌. 성공 시에만 자동닫힘.
- 서버 창(WT 탭)을 닫으면 서버가 죽으므로 닫지 말 것. STATUS 탭(`server_status.ps1`)이
  전 서버(코어+서브) 포트를 10초마다 OK/DOWN 표시.
- STATUS 는 que-core **첫 탭 하나뿐** (2026-09-29). 새 상태판이 `logs\status_board.pid` 에 자기 PID 를
  적으면 예전 상태판은 스스로 exit 0 → WT 가 탭을 닫는다. 탭 속 프로세스를 강제로 끄면 WT 가
  「종료됨」 빈 탭을 남긴다 — 09-21~29 사이 que-core 에 46탭(빈 탭 40)이 쌓였던 원인.
- Supervisor 는 켜진 뒤 120초(`BOOT_GRACE`) 동안 되살리지 않는다 — 부팅 때 런처가 띄운 PoToken 이
  포트를 열기 전에 하나를 더 띄워 4416 이 두 개였다(2026-09-29 수정).
- 수동 재기동: 해당 WT 창을 닫고 `launchers\` 의 bat 실행 (안 닫고 재실행하면 죽은 탭이 남음).
```
