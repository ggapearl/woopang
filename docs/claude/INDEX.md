# 문서 지도 — 어떤 일을 할 때 무엇을 읽나 (2026-11-24, 이 파일은 `docs/claude/INDEX.md`)

> 사람·AI 어느 쪽이든 **먼저 루트 `CLAUDE.md`(짧은 규칙)를 읽고**, 아래 표에서 지금 할 일 한 줄만 골라 그 문서 **하나**만 더 연다. 나머지는 읽지 않는다.
> 문서가 어긋나면 **더 구체적인 문서가 이긴다**. 같은 내용을 두 곳에 쓰지 말고 한 곳에만 쓰고 링크한다.

## 규칙 파일 (항상 로드되는 것)
| 파일 | 내용 |
|---|---|
| `CLAUDE.md` | 프로젝트 개요 · 작업 원칙 · 보안·서버 사고 방지 핵심 · 이 지도의 요약판 |
| `.claude/rules/*.md` | 해당 폴더 파일을 건드릴 때만 붙는 짧은 안내(Unity · 서버 · 효딩쓰 · 문서 고치기) |
| `.claude/agents/*.md` | 농민.com 서버 · 서버 장애 · 업로드 문안 · Unity 전담 에이전트 |

## 일 → 문서
| 하는 일 | 읽을 문서 |
|---|---|
| **우팡 Unity 앱** 화면·에디터·프리팹·스타일 | `docs/claude/unity-app.md` |
| AR 오브젝트 스폰·백그라운드 복귀·가시성 | `docs/claude/ar-object-spawn.md` |
| 데이터 매니저 추가·수정 | `docs/claude/data-managers.md` |
| Unity 버전 업그레이드·새 머신·빌드 에러·embed 패키지 | `docs/claude/unity-upgrade.md` |
| 3D(GLB) 생성·리깅·안무 | `docs/claude/3d-generation.md` |
| MCP Unity | `docs/claude/mcp-unity.md` |
| 0930 디자인 개편 인수인계(브랜치 `wip/0930-redesign`) | `docs/claude/handoff-0930.md` |
| **앱 빌드·스토어 업로드**(안드로이드 Play · iOS Mac 세션 요청서) | `server/voice/desk/APP_RELEASE.md` (이 PC 전용) · 지난 출시 기록(09-29~11-14) `server/voice/desk/RELEASE_LOG_0929-1114.md` |
| 웹 레이아웃(422px) · 전체 기능 테스트 | `CLAUDE.md` 2.3 · `docs/claude/web-testing.md` |
| 새 웹페이지 라우팅 · 파비콘·북마크 | `CLAUDE.md` 2.4 · `server/bookmark/README.md` |
| **기획안·제안서 HTML** | `docs/claude/proposals.md` |
| **IR · PSST 사업계획서 · 발표 PPT·스크립트** | `docs/claude/IR_PSST.md` (로컬 전용 — 공개 저장소에 안 올림) |
| 마케팅·캠페인·광고 | `docs/claude/marketing.md` |
| **콘텐츠 제작**: 유튜브·인스타 문안 | `docs/claude/upload-copy.md` |
| **콘텐츠 제작**: 파트너 촬영구성안·자막 자동화 | `docs/private/` (로컬 전용·커밋 금지) |
| 메일 쓰기 · 회사명 표기 | `docs/claude/company-mail.md` |
| 사진(구글 포토 → Immich → 효딩쓰) | `docs/claude/photos-immich.md` |
| 공공데이터 DB INSERT | `docs/claude/public-data.md` |
| 정산계좌·실명조회(돈이 오가는 로직) | `docs/claude/account-verification.md` |
| 서버 포트·Nginx·배포 | `docs/claude/server-infra.md` |
| **서버 껐다 켜기·감시기·443·AI Office·농민.com 보안** | `docs/claude/server-ops.md` (로컬 전용 — 공개 저장소에 안 올림) |
| 날씨 API(기상청 연동) | `docs/claude/weather_kma.py` 머리 주석 |
| **효딩쓰(데스크 비서)** 전부 — 구조·반영 순서·휴대폰·실패 사례 | `server/voice/docs/desk-guide.md` (정본) · 옛 이름 안내 `docs/claude/ai-office-local.md` · 날짜별 경위(왜·언제) `server/voice/docs/desk-guide-history-oct.md`·`-sep.md` |
| 효딩쓰 지금 하던 일 | `server/voice/docs/handoff.md` → `server/voice/desk/ROADMAP.md` |
| vdown(영상 다운로드) | `server/vdown/README.md` |
| Git 커밋·푸시 | `docs/claude/git-rules.md` |
| Claude Code 개선점 제보 | `docs/claude/github-issue.md` |

## 안 쓰는 문서
`docs/archive/` 한 곳에 모았다(목록 `docs/archive/README.md` — git 미추적이라 이 PC 에만 있음). 평소엔 읽지 않는다.

## 문서를 고칠 때
- 새 규칙은 **해당 작업 문서**에 쓰고, CLAUDE.md 에는 한 줄 요약 + 링크만 둔다(CLAUDE.md 는 200줄 안쪽 유지).
- 효딩쓰 이야기는 `desk-guide.md` 한 곳에만. 새 사본을 만들지 말 것.
- 옛 내용은 지우지 말고 `docs/archive/` 로 옮기고 README 에 한 줄 적는다. (기획안·콘텐츠·PSST·레이아웃·앱빌드·서버 로직 문서는 줄이는 것만 가능, 삭제 금지 — 대표님 2026-11-23)
- 다른 문서를 가리킬 땐 「CLAUDE.md 6.3 절」 같은 **절 번호 말고 문서 이름·경로**로 쓴다(내용이 옮겨 가도 안 깨진다). 규칙을 여러 파일에 복사하지 말고 한 곳에 쓰고 링크한다(에이전트·규칙 파일엔 한 줄 + 링크).
- `docs/claude/` 문서를 고치면 첫머리 「> 최종 갱신: 날짜」 도 같이 올린다. 문서가 커져 60KB 가 넘으면 현행 규칙(정본)과 날짜별 경위(`…-history`·`RELEASE_LOG…`)로 쪼갠다 — 글자는 지우지 않고 옮기기만.
- 고친 뒤 `python -I docs/claude/docs_lint.py` 로 점검한다(깨진 링크 · 옛 회사명 표기 · 절 번호 참조 · 지도 누락 · CLAUDE.md 표와 지도의 어긋남 · 날짜 · 크기).
