# WOOPANG 프로젝트 작업 가이드

## 한눈에 보기 — 자주 쓰는 명령 · 경계 (아래 규칙에서 뽑은 요약 — 상세는 괄호 안 번호)
- **명령**: 문서 점검 `python -I docs/claude/docs_lint.py` · 효딩쓰 폰 화면 반영 `python server\launchers\deploy_hyodingsseu_web.py`(순서는 2.0.3) · PC 효딩쓰 다시 켜기 = 바탕화면 「효딩쓰 다시 켜기.bat」 · 앱 강제 업데이트 `server/release_gate.py`
- **항상**: 대표님께 하는 말은 한글(2.0.5) · 하던 작업을 끝낸 뒤 새 요청 처리(2.0) · 고친 뒤 화면에 뜬 것까지 확인(2.0.3) · 시크릿은 `.env` → `os.getenv()` 로만(6.1)
- **먼저 묻기**: 기존 코드·문서 삭제(2.1) · 서버·감시기·nginx 를 껐다 켜기(`docs/claude/server-ops.md` 먼저) · 메일·게시처럼 밖으로 나가는 것 · 프로덕션 출시·앱 강제 업데이트(대표님이 올리라고 하실 때만)
- **절대 금지**: 시크릿·키 파일 커밋(6.1) · 이미지명 단위 kill(`/IM python.exe`)·서버가 든 WT 탭 강제 종료 · `nginx.conf` 덮어쓰기 · Claude API(종량 과금) 켜기 · 효딩쓰를 Claude Code 세션 안에서 직접 켜기 · `server/voice` 를 공개 `woopang` 에 올리기 · 기획안·콘텐츠·PSST·레이아웃·앱빌드·서버 로직 문서 삭제(줄이기만)

## 1. 프로젝트 개요
- **프로젝트**: WOOPANG - AR 기반 SNS 앱 (Unity 6000.4.6f1)
- **회사명**: 주식회사 쾌 (QUE Co., Ltd.)
- **타겟**: 100만~1000만 사용자 규모, 모바일 (iOS/Android)
- **상태**: 개발 중 (앱서비스 활성화 전)

### 회사명 표기 · 메일 양식 (필수 — 전문·경위: [docs/claude/company-mail.md](docs/claude/company-mail.md))
- 한글 **「주식회사 쾌」**(직함 「주식회사 쾌 대표」) · 영문 **「QUE Co., Ltd.」** · 자리가 좁으면 **「QUE」**. 서비스 파생형은 QUE 접두(「QUE Cloud」). 한글 메일·푸터·화면은 한글 이름, 영어 메일은 영문 이름.
- ❌ 금지: 「쾌엔터테인먼트」「쾌엔터」 · 「QUE.ENT」「QUE. ENT」 · **「KWAE」「Kwae」「KWEA」 등 로마자 표기 일체** · 「쾌ENT」 · 「QUE Entertainment」. 새로 보이면 고칠 것.
- ⚠ 사업자등록번호는 현재 개인사업자 번호(값은 로컬 전용 `docs/private/company-info.md` — 공개 저장소 문서엔 적지 않는다) — 법인 설립(11/23~27 예정) 뒤 대표님이 새 번호를 주시면 전수 교체.
- **한글 메일**: 「안녕하세요,」 + 호칭은 아랫줄 · 제목 「[협업제안] …」((광고) 안 붙임) · 마지막 요청은 한 가지만 간결하게 · 끝 「감사합니다.」 한 번 + 「김영훈 드림.」(직함 없이). 양식 전문은 위 문서.
- **우팡 해외(영어) 새 제안 메일은 보내지 않는다**(2026-11-19 — 코드 `outreach.NO_OVERSEAS`). 이미 온 영어 메일에 답하는 회신만 「Re: …」로.


---

## 2. 핵심 작업 원칙

### 2.0 작업 중 추가 요청이 오면 — 하던 것부터 끝낸다 (필수)
대표님은 **생각나는 대로 수시로 추가 요청**을 보낸다. 이때:
- **진행 중이던 작업을 멈추고 새 요청으로 갈아타지 말 것.** 하던 작업을 검증까지 끝낸 뒤 처리한다.
- 새 요청은 **대기열에 적어두고**(TodoWrite) 순서대로 소화한다. 접수했다는 사실만 짧게 알리면 된다.
- 예외: 새 요청이 **지금 하던 작업을 무효화**하거나(방향 자체가 바뀜), **서비스 장애·보안 사고**인 경우에만 즉시 전환.
- 작업을 마칠 때 **완료한 것 / 남은 대기열**을 구분해 보고한다. 못 한 것을 한 것처럼 뭉뚱그리지 말 것.

### 2.0.1 생성물·시안 저장 위치 (필수)
대표님이 **검토·재확인할 결과물**(시안/목업 HTML, 생성 이미지, 데모 파일 등)은 시스템 임시 스크래치패드(깊은 temp 경로)에 두지 말고 **`C:\nongmin-app\mockups\`** 에 저장한다(폴더 없으면 생성). 찾기 쉬운 곳에 둘 것. 순수 일회성 진단 스크립트·중간 스크린샷만 스크래치패드 사용 가능. (2026-07 대표님 지시)
- **우팡(WOOPANG) 앱 시안·목업·시뮬레이터는 `C:\woopang-app\mockups\`** 에 따로 둔다 (2026-10-01 대표님 지시 — 그동안의 우팡 시안 `우팡_*.html`·`우팡_시작화면_0929\`·`woopang-rebuild-plan.html`·`splash_before_after_20260926.png` 를 이리로 옮김). 농민닷컴·효딩쓰 등 나머지는 계속 `C:\nongmin-app\mockups\`.

### 2.0.2 업로드 문안(제목·쇼츠·태그)은 파일 말고 **채팅으로** (2026-08-31 지시)
유튜브·인스타 **제목 / 쇼츠 문안 / 태그 / 설명문**은 md 파일로 만들지 말고 **이 세션 채팅창에 바로** 보낸다(복사해 쓰시므로). 태그는 **줄이지 말고 전부**. 양식·금지 사항(챕터 6~7개·전언체 금지·채널 소개 고정 2줄 등)은 **[docs/claude/upload-copy.md](docs/claude/upload-copy.md)** — 문안을 쓸 때 반드시 읽을 것(또는 `upload-copy` 에이전트).

### 2.0.3 고친 뒤 「반영」까지가 내 일이다 (2026-10-18 대표님 「커밋한 걸 불러오는 건 네가 작업 끝나면 매번 해두면 되는 거 아니야?」)
커밋·푸시로 끝내지 말고 **대표님 화면에 실제로 떴는지**까지 확인하고 보고한다. 「화면 다시 불러와」를 대표님이 말하게 만들지 말 것.
**하찮은 편의 기능은 만들지 않는다** — 반영은 「껐다 켜기」면 충분하다(대표님 10-18: 「이러한 하찮은 것들은 굳이 디벨롭 안 해도 돼」).
- **효딩쓰 휴대폰 화면**(`apps/hyodingsseu-*/web`) — 순서대로:
  1. 전용 worktree `C:\tmp\webedit` 에서 고쳐 `WEB_VERSION` 을 올린다.
  2. 커밋·푸시.
  3. `python server\launchers\deploy_hyodingsseu_web.py` (경고가 뜨면 버전을 안 올린 것).
  4. `https://woopang.com/hyodingsseu/web_version.txt` 로 확인. 폰은 앱으로 돌아올 때 이 파일을 보고 스스로 새로 고친다.
- **PC 효딩쓰 창·두뇌**: 비공개 저장소에 커밋한 뒤 **필요하면 내가 직접 껐다 켠다**(대표님 10-18 승인 — 일하는 중이면 끝난 뒤).
  - 방법: `Start-Process explorer.exe '"C:\Users\pdnom\Desktop\효딩쓰 다시 켜기.bat"'` (restart.py 가 감시자 stop-all → 깨끗한 환경으로 다시 켠다).
  - ❌ 효딩쓰에게 「화면 다시 불러와」(`reload_self`)를 시키지 말 것 — 에이전트 한 턴이라 토큰만 쓴다.
  - 상세: [server/voice/docs/desk-guide.md](server/voice/docs/desk-guide.md) 첫머리.

### 2.0.4 claude.ai 커넥터 「인증 필요」 안내는 대표님께 옮기지 않는다 (2026-10-19 대표님 「이거 매번 나한테 물어보는데 왜 자꾸 연결하라고 하는거냐」)
세션이 뜨거나 도구가 새로 고쳐질 때 나오는 Gmail·Google Calendar·Google Drive 「needs authentication」 안내는 Claude Code 가 자동으로 띄우는 것이다 — 매번 전하지 말 것(10-19 에 한 번 설명드림).
커넥터는 **로그인한 Claude 계정마다 따로**라, PC 계정이 바뀌면 다른 계정에서 연결해 둔 것도 「연결 안 됨」으로 뜬다. 커넥터가 실제로 필요한 일이 생겼을 때만 그 일과 함께 한 번 여쭌다.

### 2.0.5 대표님께 하는 설명은 전부 한글로 — 작업 과정 포함 (2026-11-17 대표님 「한글로 설명해줘」)
- 결과 보고 · 질문 · 작업 중간에 남기는 짧은 진행 상황(「지금 ○○ 하는 중」) · 오류 설명까지 **대표님이 읽는 글은 모두 한글**로 쓴다. 영어로 보고하지 말 것.
- 코드 · 파일 경로 · 명령어 · 커밋 해시 같은 고유한 이름은 그대로 두되, 그걸 설명하는 말은 한글로.
- 영어 메일(1번 회사명 · 메일 양식) · 해외용 문서처럼 **받는 사람이 영어권인 결과물**은 예외 — 그 결과물만 영어, 대표님께 드리는 설명은 한글.

### 2.0.6 새 문서는 HTML 한 파일로 (2026-11-19 대표님 「전부 html 로 — 지금까지 있던 건 그대로, 앞으로 작업할 것만」)
- 앞으로 새로 만드는 **문서**(보고서·정리·안내·기획·미팅 자료)는 md 가 아니라 **HTML 한 파일**로 만든다(효딩쓰 문서함에 올리는 것 포함 — 효딩쓰 지침 `desk/agent.py` 에도 같은 규칙).
- 폭 **422px** 우선·가로 넘침 0 · `prefers-color-scheme` 다크 모드 · **`vh` 단위 금지**(문서함 휴대폰 화면이 문서 높이에 맞춰 iframe 을 늘리는데 vh 가 있으면 한없이 길어진다) · 바탕색은 `body` 에 직접 지정(문서함 위 줄이 그 색을 따른다) · `<style>` 인라인, 외부 파일 없이 한 파일 · 맨 아래 여백 80px 이상.
- **이미 있는 md 는 그대로** 두고 바꿔 쓰지 않는다. 그 문서에 이어 적는 일은 md 로 계속한다. 코드·설정·`docs/claude/` 가이드처럼 저장소에 남는 개발 문서는 이 규칙 대상이 아니다.

### 2.0.7 번역은 대표님이 「번역해줘」 하신 것만 (2026-11-22 대표님 「앞으로는 내가 번역해달라고 하는 것만 번역해서 반영하도록 하자」)
- 새 페이지·새 문구·새 카드를 만들 때 **영어·일본어·중국어·스페인어 번역을 자동으로 붙이지 않는다.** 한국어만 쓴다.
- 이미 번역돼 있는 기존 문구는 그대로 둔다(지우지 않는다). 번역이 필요하면 대표님이 요청하신 그 부분만 번역해 반영한다.
- 번역이 없는 새 문구가 언어 선택 화면에서 한국어로 보이는 건 정상이다 — 빠진 것으로 보고하지 말 것.

### 2.1 기존 코드 존중 (중요)
- 기존에 잘 작동하는 로직은 함부로 삭제/변경 금지
- 삭제가 필요하면 **반드시 먼저 제안**
- 수정 시 기존 기능이 초기화되지 않도록 주의

### 2.2 완전한 작업 수행
로직 작업 시 UI 오브젝트까지 생성하고 연결 완료해야 함:
1. 스크립트 생성/수정
2. 에디터 스크립트로 UI 오브젝트 생성 (`Assets/Scripts/Editor/`)
3. 컴포넌트 연결, Inspector 필드 연결
4. 누락된 참조 체크 (AutoConnectFields 패턴)

### 2.3 코드 품질
- 지양: 불필요한 Debug.Log, 과도한 주석, 미사용 코드
- 지향: 깔끔한 코드, 필수 에러 로그만, 재사용 가능 구조
- 폰트: `AppleSDGothicNeoM.ttf` (모든 UI)
- **웹 모바일 레이아웃 기준 폭: 422px** (대표님 실기기 아이폰 실측 뷰포트) — 모바일 UI는 422px에서 우선 디자인·검증하고 390/375/360px까지 가로 넘침 0 유지. 페이지 좌우 스크롤 절대 금지(의도된 내부 가로 레일 제외)

### 2.4 웹페이지 서빙(라우팅) 시 — 파비콘·북마크 기본 처리 (요청 없어도 자동)
woopang.com 에 **새 페이지/서비스를 만들고 라우팅**할 때는 사용자가 따로 말하지 않아도 기본으로 함께 처리한다:
1. 레이아웃 팔레트에 맞는 **파비콘 세트**(`favicon.svg`+`.ico`+png 16/32/180/192/512+`site.webmanifest`)와 적절한 `<title>`·`theme-color`·OG 메타를 주입.
2. **`/bookmark` 대시보드 연동** — `server/bookmark/bookmark.html` 의 `faviconPaths` 에 `'<service>': '/<경로>/favicon.svg'` 한 줄 등록(없으면 기본 아이콘으로 뜸).
3. 상세 절차·체크리스트: **[server/bookmark/README.md](server/bookmark/README.md)** · 예시 [server/jetcity/README.md](server/jetcity/README.md)

---

## 3. 작업별 상세 가이드 (해당 작업 시 반드시 읽을 것)

| 작업 유형 | 참조 파일 |
|-----------|-----------|
| Unity 앱 개발 (UI/에디터/프리팹/매니저/스타일) — 저장 화면은 「수정 → 저장」 핑크 표시 필수 · 알림 팝업 통일 규칙 포함 | [docs/claude/unity-app.md](docs/claude/unity-app.md) |
| AR 오브젝트 스폰 / 백그라운드 복귀 / 가시성 | [docs/claude/ar-object-spawn.md](docs/claude/ar-object-spawn.md) |
| 데이터 매니저 추가·수정 (연결 체크리스트) | [docs/claude/data-managers.md](docs/claude/data-managers.md) |
| Unity 버전 업그레이드 / 새 머신 셋업 / 빌드 에러 / embed 패키지 패치 | [docs/claude/unity-upgrade.md](docs/claude/unity-upgrade.md) |
| Git 커밋/푸시 | [docs/claude/git-rules.md](docs/claude/git-rules.md) |
| 마케팅/캠페인/광고 | [docs/claude/marketing.md](docs/claude/marketing.md) |
| 한글/영어 메일 쓰기 · 회사명 표기 점검 (양식 전문 · 옛 이름 목록) | [docs/claude/company-mail.md](docs/claude/company-mail.md) |
| 유튜브·인스타 업로드 문안 (제목·설명문·챕터·태그 양식) | [docs/claude/upload-copy.md](docs/claude/upload-copy.md) |
| 정산계좌 검증 (계좌·은행코드·실명조회) — 돈이 직접 오가는 로직 | [docs/claude/account-verification.md](docs/claude/account-verification.md) |
| 공공데이터 DB INSERT | [docs/claude/public-data.md](docs/claude/public-data.md) |
| 기획안/제안서 HTML | [docs/claude/proposals.md](docs/claude/proposals.md) |
| IR 자료 · PSST 사업계획서 · 발표 PPT · 발표 스크립트/예상 질문 (우기 한 벌이 기준) | [docs/claude/IR_PSST.md](docs/claude/IR_PSST.md) (로컬 전용 — 사업계획 수치가 있어 공개 저장소에 안 올림) |
| 콘텐츠 파트너 자료 (촬영구성안 / 멤버 분석) — ⚠️ 로컬 전용 | `docs/private/` (git 미추적 · 민감자료라 커밋 금지) |
| 콘텐츠 파트너 말자막 자동화 (MOGRT 자동자막 / 자막 패널 / 화자별 재배치) — 로컬 전용 | `docs/private/` |
| 3D 콘텐츠(GLB) 생성·자동화 (Blender·Mixamo·Hunyuan3D·리깅·안무) | [docs/claude/3d-generation.md](docs/claude/3d-generation.md) |
| 서버 포트/Nginx/배포 | [docs/claude/server-infra.md](docs/claude/server-infra.md) |
| 서버 껐다 켜기·런처/감시기/WT 탭·443·AI Office·효딩쓰·농민.com 보안 (서버 구성 6.2 · 기동 구조 6.3) | [docs/claude/server-ops.md](docs/claude/server-ops.md) (로컬 전용 — 서버 보안 점검 내용이라 공개 저장소에 안 올림) |
| 하늘 날씨 서버 `/api/weather` — 기상청 초단기실황 연동 | [docs/claude/weather_kma.py](docs/claude/weather_kma.py) (파일 머리 주석에 붙이는 법) |
| 웹앱 전체 기능 테스트 ("전부 테스트해줘"/"잘 작동하는지 검토") — E2E·실브라우저 | [docs/claude/web-testing.md](docs/claude/web-testing.md) |
| 데스크 비서 「효딩쓰」(스스로 고치기·뒤의 두뇌·휴대폰·09·18시 일과·AI Office 직원 창)·목소리·로컬 LLM·Claude API 끄기 | [server/voice/docs/desk-guide.md](server/voice/docs/desk-guide.md) (안내 [docs/claude/ai-office-local.md](docs/claude/ai-office-local.md)) |
| 사진 📷 (구글 포토 Takeout → Immich → 효딩쓰 앱) — 압축 풀기(`extract_all.py`, 2026-11-21 허락됨 · zip 은 대표님 허락 없이 지우지 않는다) · 날짜·위치 맞추기 · 서명 링크 · 폰 자동 백업·정리 | [docs/claude/photos-immich.md](docs/claude/photos-immich.md) |
| 앱 빌드·스토어 업로드 (농민닷컴·우팡 안드로이드 → Play 직접 업로드 / iOS → Mac 세션 요청서) | `server/voice/desk/APP_RELEASE.md` (로컬 전용) · 버전 이력 `C:\nongmin-app\VERSIONS.md` |
| vdown (영상 다운로드) — 403/다운로드 실패·쿠키 세션·주간 자가점검 | `server/vdown/README.md` (로컬 전용) |
| MCP Unity (Claude ↔ Unity Editor) | [docs/claude/mcp-unity.md](docs/claude/mcp-unity.md) |
| Claude Code 개선점 제보 (GitHub 이슈 자동화 — "제보해줘"/"이슈 올려줘") | [docs/claude/github-issue.md](docs/claude/github-issue.md) |

⚠️ 해당 작업을 할 때만 관련 파일을 읽을 것. 불필요한 파일은 읽지 않는다. 어느 문서를 볼지 모르겠으면 **[docs/claude/INDEX.md](docs/claude/INDEX.md)**(작업→문서 지도). 안 쓰는 옛 문서는 전부 `docs/archive/`(목록 `docs/archive/README.md`) — 평소엔 읽지 않는다.

---

## 4. Unity 버전 업그레이드 / 새 머신 셋업 / embed 패키지 (요약)

상세 절차·에러 패턴·.aar 재압축 방법은 **[docs/claude/unity-upgrade.md](docs/claude/unity-upgrade.md)** 참조 (해당 작업 시 필독). 항상 기억할 핵심만:

- `Packages/`에 **embed된 패키지 6종** 존재 (`com.google.ar.core.arfoundation.extensions`, `com.unity.xr.arcore`, yasirkula 4종) — Unity 6 + AGP 9 호환 패치 중. **절대 원래 registry/git URL로 되돌리지 말 것** (각 패키지의 PATCH_NOTES.md 참조)
- 사용자가 "Unity 업데이트했어"라고 고지하면 → unity-upgrade.md의 AI 워크플로우를 자동 수행
- `Library/`, `Temp/`, `obj/`, `Logs/`는 캐시 (통삭 가능) / `Assets/`, `ProjectSettings/`, `Packages/`는 절대 삭제 금지

---

## 5. 데이터 매니저 (AR 오브젝트 스폰) — 요약

우팡 앱은 **6개 데이터 매니저**가 AR 오브젝트를 스폰한다(모두 [Assets/Scripts/Places/](Assets/Scripts/Places/)에 위치):
- `DataManager` — 자체 DB
- `TourAPIManager` — 공공데이터(화면 칩 이름은 「공공기관」)
- `SubwayManager` / `TrainStationManager` / `TerminalManager` — 공공교통
- `StoreManager` — 근처 가게(화면 칩 이름은 「공공데이터」, 2026-11-20)

- ⚠️ "공공데이터(TourAPI)" ≠ "공공교통(Subway/Train/Terminal)" — 별도 카테고리. 한국어로 둘 다 "공공"이라 헷갈리기 쉬우니 명확히 구분할 것.
- 매니저 추가·수정 시 zoom·리스트·visibility 등 **6개 연결 시스템 체크리스트**를 반드시 점검 → **[docs/claude/data-managers.md](docs/claude/data-managers.md)** (필독). 한 곳이라도 빠지면 그 매니저 데이터만 기능 누락 (공공교통 zoom 누락 사례).

---

## 6. 서버 보안 · 시크릿 관리 (서버 코드 작업 시 필독)

`C:\woopang\server` 의 Flask 서버 — 메인 `app_improved.py`(Waitress 8080, nginx(443) 뒤) · 농민.com `nongmin/nongmin_server.py`(포트 6688) — 작업 시:

### 6.1 시크릿 취급 규칙 (예외 없음)
1. **시크릿은 `.env`(`C:\woopang\server\.env`)에만 존재한다.** DB 비밀번호·관리자 비밀번호·세션키·API키·서비스계정 경로 전부. 코드에서는 오직 `os.getenv()` / `os.environ.get()` 로 읽는다.
2. **하드코딩 폴백 금지 (fail-closed).** `os.getenv('X') or '실제비번'` 같은 폴백을 절대 만들지 말 것. 미설정이면 기능이 꺼지거나 기동 실패하게 두는 게 맞다 (조용히 약한 기본값으로 도는 게 더 위험).
3. **문서·주석·커밋 메시지에도 비번 실값 금지.** WORK_NOTES·CLAUDE.md 포함. 값이 필요하면 "`.env` 의 `X` 참조" 라고만 쓴다.
4. **키·인증서 파일 커밋 금지** — `*.pem *.key *.keystore *.jks`, `google-services.json`, `fcm-service-account.json`, `keystore.properties`. (앱 keystore 는 분실 시 앱 갱신 영구 불가 → 별도 백업 필수)
5. **pre-commit 훅이 위 전부를 자동 차단한다** (`.git/hooks/pre-commit`, git 미추적). 파일명·추가된 코드 라인 양쪽을 검사. ⚠ 2026-07-14 에 훅의 내용검사가 `grep -E '^\+'` 오작동으로 **죽어 있던 것을 발견·수정**(`grep '^+'`) — 훅 수정 시 반드시 차단 테스트 후 사용할 것.

### 6.2~6.3 서버 구성 · 기동 구조 → **[docs/claude/server-ops.md](docs/claude/server-ops.md)** (서버를 껐다 켜거나 런처·감시기·nginx·IIS·443·AI Office·효딩쓰·농민.com 보안을 만질 때 필독, 2026-11-12 분리)

CLAUDE.md 에 남기는 **어겨서 사고가 난 핵심만** (상세·경위는 위 문서):
- **자동복구 = 대표님이 보는 WT 창(que-core/que-sub)에 탭 하나 더 (2026-11-12 대표님 지시 — 서버를 껐다 켜도 서버 상태를 볼 수 있게).**
  - 감시기(`smart_monitoring_system.py`·`launchers/server_supervisor.py`)는 `launchers/hidden.py` 의 `spawn_visible()` 한 곳으로 되살린다. 새 창이 따로 뜨지 않는다. 새 자동복구 코드도 `spawn_visible()` 을 쓸 것.
  - **AI Office 8000 만 예외(창 없이)**. 서버가 끝나면 탭이 20초 뒤 스스로 닫힌다.
- **프로세스를 죽일 땐 포트 리스너 PID 단위로만.** 이미지명 단위 kill(`/IM python.exe`·`/IM node.exe`) 금지, 커맨드라인으로 죽일 땐 이미지명 화이트리스트(python·node) 필수 — `WindowsTerminal.exe` 가 걸리면 창(코어 서버 전부)째 죽는다(2026-09-07 두 번 전멸). **서버가 든 WT 탭을 강제로 끄지 말 것.**
- **`C:\nginx\conf\nginx.conf` 가 유일한 정본** — 어떤 스크립트도 덮어쓰면 안 된다. **443 은 nginx 전용**: win-acme 에 IIS 원천/설치 갱신을 만들지 말 것(IIS 가 443 을 채감), 옛 백업을 원래 이름으로 되돌리지 말 것. 재발 감시 `launchers/guard443.py`.
- **bat 파일은 ASCII 전용(한글 금지)**, 지연은 `ping -n N 127.0.0.1`(timeout.exe 금지).
- **AI Office(8000)는 반드시 하나만**(잠금 파일 `logs/ai_office.lock`) — 블루프린트를 `app_improved.py` 에 다시 등록하지 말 것(텔레그램 이중 발송). **Claude API(종량 과금)는 쓰지 않는다**(`CLAUDE_API_ENABLED=1` 일 때만). 알림은 `que_outbox.send()` 로(텔레그램 API 직접 호출 금지).
- **농민.com 세션 쿠키에 인증번호·토큰 등 비밀을 넣지 말 것**(서명만 되고 암호화 안 됨). 새 로그인 화면엔 `login_guard` 의 `_lg.check/fail/ok` 를 붙일 것.
- **앱 강제 업데이트는 `server/release_gate.py`** 로만 — `.env` 의 `ANDROID_VERSION`·`*_FORCE_UPDATE` 를 직접 올리지 말 것.
- **효딩쓰를 Claude Code 세션 안에서 직접 켜지 말 것**(환경값이 물려 들어감) — 바탕화면 「효딩쓰 다시 켜기.bat」. `server/voice` 는 비공개 저장소 — 공개 `woopang` 에 올리지 말 것.
- **Firebase 설정 3종은 새 프로젝트 `woopang-390b4` 값이라 커밋하지 않는다**(pre-commit 이 막음). `git checkout`·stash 로 되돌리지 말 것.

---

> ⚠️ **콘텐츠 파트너(연예/아티스트) 자료는 git 추적 금지.** 촬영구성안·멤버 분석·자막자동화 등 민감 자료는 `docs/private/`(=`/docs/*` 규칙으로 자동 제외, 파일명도 비노출)에만 둔다. 공개 저장소(추적 파일·문서·주석)에 파트너 실명이나 멤버 신상 정보를 적지 말 것. pre-commit 훅이 관련 문구·파일을 자동 차단한다.

*최종 정리: 2026-11-24 — 이 파일은 짧게 유지하고(공식 권장 200줄 미만), 자세한 것은 위 3번 표의 `docs/claude/` 문서로 보낸다. 사람용 메모는 `<!-- -->` 블록 주석에 쓰면 Claude 문맥에 들어가지 않는다.*
