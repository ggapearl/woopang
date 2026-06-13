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
| **앳하트(AtHeart) 콘텐츠 / 촬영구성안 / 멤버 분석** | **[docs/claude/atheart-contents.md](docs/claude/atheart-contents.md)** |
| **3D 콘텐츠(GLB) 생성·자동화 (Blender·Mixamo·Hunyuan3D·리깅·안무)** | **[docs/claude/3d-generation.md](docs/claude/3d-generation.md)** |
| 서버 포트/Nginx/배포 | [docs/claude/server-infra.md](docs/claude/server-infra.md) |
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

`C:\woopang\server` 의 Flask 서버 — 메인 `app_improved.py`(포트 443) · 농민.com `nongmin/nongmin_server.py`(포트 6688) — 작업 시:

- **시크릿 하드코딩 절대 금지** — DB 비밀번호·관리자 비밀번호·세션키·API키는 반드시 `os.getenv()` 로 읽고 `.env`(`C:\woopang\server\.env`)에 둔다. 소스·git 에 평문으로 넣지 말 것.
- 과거 `Dnvkddl011$` 가 DB·관리자 비밀번호로 하드코딩돼 git 에 노출돼 있었음 → 환경변수로 이전함. **재발 금지** (소스에 남은 폴백 기본값도 .env 설정 후 제거 대상).
- 세션키는 미설정 시 랜덤 폴백하도록 돼 있음. Flask `debug=True` 금지. 세션쿠키는 HttpOnly·SameSite·Secure 적용.
- DB는 `postgres` 슈퍼유저로 접속 중 → 장기적으로 앱 전용 제한권한 롤 권장. 원격 DB SSL(`sslmode`) 미적용 상태.
- 농민.com 서버 작업 상세·미완료·보안 조치 항목은 **`server/nongmin/WORK_NOTES.md`**, 개발 로드맵은 **`server/nongmin/ROADMAP.md`**, 웹뷰앱은 **`server/nongmin/APP_BUILD_GUIDE.md`** 참조.

---

*최종 업데이트: 2026-06-12 (4·5번 섹션 상세를 docs/claude/unity-upgrade.md · data-managers.md 로 분리하고 요약만 유지 — 섹션 번호는 기존 참조 호환 위해 유지)*
