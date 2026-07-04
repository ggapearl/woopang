# WOOPANG 프로젝트 작업 가이드

## 1. 프로젝트 개요
- **프로젝트**: WOOPANG - AR 기반 SNS 앱 (Unity 6000.4.6f1)
- **회사명**: 쾌엔터테인먼트 (QUE. ENT)
- **타겟**: 100만~1000만 사용자 규모, 모바일 (iOS/Android)
- **상태**: 개발 중 (앱서비스 활성화 전)

### 회사명 표기 규칙 (필수)
- 본문/상단: **"QUE. ENT"** (한국어/영어 모두)
- 푸터(저작권): **"쾌엔터테인먼트"** (한국어), **"KWAE Entertainment"** (영어)
- ❌ 금지: "쾌ENT", "KWAE ENT", "QUE Entertainment"

---

## 2. 핵심 작업 원칙

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

### 2.4 웹페이지 서빙(라우팅) 시 — 파비콘·북마크 기본 처리 (요청 없어도 자동)
woopang.com 에 **새 페이지/서비스를 만들고 라우팅**할 때는 사용자가 따로 말하지 않아도 기본으로 함께 처리한다:
1. 레이아웃 팔레트에 맞는 **파비콘 세트**(`favicon.svg`+`.ico`+png 16/32/180/192/512+`site.webmanifest`)와 적절한 `<title>`·`theme-color`·OG 메타를 주입.
2. **`/bookmark` 대시보드 연동** — `server/bookmark/bookmark.html` 의 `faviconPaths` 에 `'<service>': '/<경로>/favicon.svg'` 한 줄 등록(없으면 기본 아이콘으로 뜸).
3. 상세 절차·체크리스트: **[server/bookmark/README.md](server/bookmark/README.md)** · 예시 [server/jetcity/README.md](server/jetcity/README.md)

---

## 3. 작업별 상세 가이드 (해당 작업 시 반드시 읽을 것)

| 작업 유형 | 참조 파일 |
|-----------|-----------|
| Unity 앱 개발 (UI/에디터/프리팹/매니저/스타일) | [docs/claude/unity-app.md](docs/claude/unity-app.md) |
| **AR 오브젝트 스폰 / 백그라운드 복귀 / 가시성 (필독)** | **[docs/claude/ar-object-spawn.md](docs/claude/ar-object-spawn.md)** |
| **데이터 매니저 추가·수정 (연결 체크리스트 — 필독)** | **[docs/claude/data-managers.md](docs/claude/data-managers.md)** |
| **Unity 버전 업그레이드 / 새 머신 셋업 / 빌드 에러 / embed 패키지 패치 (필독)** | **[docs/claude/unity-upgrade.md](docs/claude/unity-upgrade.md)** |
| Git 커밋/푸시 | [docs/claude/git-rules.md](docs/claude/git-rules.md) |
| 마케팅/캠페인/광고 | [docs/claude/marketing.md](docs/claude/marketing.md) |
| 공공데이터 DB INSERT | [docs/claude/public-data.md](docs/claude/public-data.md) |
| 기획안/제안서 HTML | [docs/claude/proposals.md](docs/claude/proposals.md) |
| **콘텐츠 파트너 자료 (촬영구성안 / 멤버 분석) — ⚠️ 로컬 전용** | **`docs/private/` (git 미추적 · 민감자료라 커밋 금지)** |
| **콘텐츠 파트너 말자막 자동화 (MOGRT 자동자막 / 자막 패널 / 화자별 재배치) — 로컬 전용** | **`docs/private/`** |
| **3D 콘텐츠(GLB) 생성·자동화 (Blender·Mixamo·Hunyuan3D·리깅·안무)** | **[docs/claude/3d-generation.md](docs/claude/3d-generation.md)** |
| 서버 포트/Nginx/배포 | [docs/claude/server-infra.md](docs/claude/server-infra.md) |
| **웹앱 전체 기능 테스트 ("전부 테스트해줘"/"잘 작동하는지 검토") — E2E·실브라우저** | **[docs/claude/web-testing.md](docs/claude/web-testing.md)** |
| MCP Unity (Claude ↔ Unity Editor) | [docs/claude/mcp-unity.md](docs/claude/mcp-unity.md) |
| Claude Code 개선점 제보 (GitHub 이슈 자동화 — "제보해줘"/"이슈 올려줘") | [docs/claude/github-issue.md](docs/claude/github-issue.md) |

⚠️ 해당 작업을 할 때만 관련 파일을 읽을 것. 불필요한 파일은 읽지 않는다.

---

## 4. Unity 버전 업그레이드 / 새 머신 셋업 / embed 패키지 (요약)

상세 절차·에러 패턴·.aar 재압축 방법은 **[docs/claude/unity-upgrade.md](docs/claude/unity-upgrade.md)** 참조 (해당 작업 시 필독). 항상 기억할 핵심만:

- `Packages/`에 **embed된 패키지 6종** 존재 (`com.google.ar.core.arfoundation.extensions`, `com.unity.xr.arcore`, yasirkula 4종) — Unity 6 + AGP 9 호환 패치 중. **절대 원래 registry/git URL로 되돌리지 말 것** (각 패키지의 PATCH_NOTES.md 참조)
- 사용자가 "Unity 업데이트했어"라고 고지하면 → unity-upgrade.md의 AI 워크플로우를 자동 수행
- `Library/`, `Temp/`, `obj/`, `Logs/`는 캐시 (통삭 가능) / `Assets/`, `ProjectSettings/`, `Packages/`는 절대 삭제 금지

---

## 5. 데이터 매니저 (AR 오브젝트 스폰) — 요약

우팡 앱은 **5개 데이터 매니저**가 AR 오브젝트를 스폰: `DataManager`(자체 DB) · `TourAPIManager`(공공데이터) · `SubwayManager`/`TrainStationManager`/`TerminalManager`(공공교통). 모두 [Assets/Scripts/Download/](Assets/Scripts/Download/)에 위치.

- ⚠️ "공공데이터(TourAPI)" ≠ "공공교통(Subway/Train/Terminal)" — 별도 카테고리. 한국어로 둘 다 "공공"이라 헷갈리기 쉬우니 명확히 구분할 것.
- 매니저 추가·수정 시 zoom·리스트·visibility 등 **6개 연결 시스템 체크리스트**를 반드시 점검 → **[docs/claude/data-managers.md](docs/claude/data-managers.md)** (필독). 한 곳이라도 빠지면 그 매니저 데이터만 기능 누락 (공공교통 zoom 누락 사례).

---

## 6. 서버 보안 · 시크릿 관리 (서버 코드 작업 시 필독)

`C:\woopang\server` 의 Flask 서버 — 메인 `app_improved.py`(Waitress 8080, nginx(443) 뒤) · 농민.com `nongmin/nongmin_server.py`(포트 6688) — 작업 시:

- **시크릿 하드코딩 절대 금지** — DB 비밀번호·관리자 비밀번호·세션키·API키는 반드시 `os.getenv()` 로 읽고 `.env`(`C:\woopang\server\.env`)에 둔다. 소스·git 에 평문으로 넣지 말 것.
- 🔴 **미완료 P0 (2026-07 발견):** 과거 소스에 하드코딩됐던 DB/관리자 비밀번호(구 11자 값)가 **아직 실서버 DB 비밀번호(`DB_PASSWORD`)로 그대로 사용 중**이고, 이 값이 **public GitHub(`ggapearl/woopang`)의 `app_improved.py` 및 과거 커밋에 노출**돼 있었음. 게다가 PostgreSQL 이 **`0.0.0.0:5432`(공인 IP 210.105.65.145)로 인터넷에 열려** 있어 슈퍼유저 접속 위험. → **PostgreSQL 비밀번호 즉시 교체 + 5432 외부 차단(listen_addresses/방화벽) + git 히스토리 세탁** 필요. (대표 승인·DB 재시작 타이밍 필요해 코드로 자동 처리 안 함.) ⚠ 이 문서에도 비번 실값을 절대 다시 적지 말 것.
- ✅ **2026-07 조치 완료:** `app_improved.py` 를 git 추적 해제(`/server/` 이미 ignore) → GitHub HEAD 에서 서버 소스·하드코딩 시크릿 제거. `/admin`·`/dbadmin` 의 공개 하드코딩 폴백 제거 → `.env` 의 `WOOPANG_ADMIN_PW` 전용(미설정 시 fail-closed). admin_server.py DB 설정도 env 화. **재발 금지.**
- 세션키는 미설정 시 랜덤 폴백하도록 돼 있음. Flask `debug=True` 금지. 세션쿠키는 HttpOnly·SameSite·Secure 적용.
- DB는 `postgres` 슈퍼유저로 접속 중 → 장기적으로 앱 전용 제한권한 롤 권장. 원격 DB SSL(`sslmode`) 미적용 상태.
- 농민.com 서버 작업 상세·미완료·보안 조치 항목은 **`server/nongmin/WORK_NOTES.md`**, 개발 로드맵은 **`server/nongmin/ROADMAP.md`**, 웹뷰앱은 **`server/nongmin/APP_BUILD_GUIDE.md`** 참조.

---

> ⚠️ **콘텐츠 파트너(연예/아티스트) 자료는 git 추적 금지.** 촬영구성안·멤버 분석·자막자동화 등 민감 자료는 `docs/private/`(=`/docs/*` 규칙으로 자동 제외, 파일명도 비노출)에만 둔다. 공개 저장소(추적 파일·문서·주석)에 파트너 실명이나 멤버 신상 정보를 적지 말 것. pre-commit 훅이 관련 문구·파일을 자동 차단한다.

*최종 업데이트: 2026-07-04 (6번 서버보안 섹션 갱신: DB 비밀번호 공개노출·5432 인터넷개방 P0 명시, /admin·/dbadmin 공개폴백 제거 완료. tire·vrompt 라우팅 전면 삭제, 자동복구 몰살버그 수정 — 상세 docs/claude/server-infra.md. 루트 구식 스크래치 md 68개를 `docs/archive/` 로 이관. 콘텐츠 파트너 민감자료를 `docs/private/` 로 격리하고 공개 추적 파일에서 실명 스크럽 — 현행 가이드는 이 파일과 docs/claude/ 만 참조)*
