# WOOPANG 프로젝트 작업 가이드

## 1. 프로젝트 개요
- **프로젝트**: WOOPANG - AR 기반 SNS 앱 (Unity 6000.3.13f1)
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
| Git 커밋/푸시 | [docs/claude/git-rules.md](docs/claude/git-rules.md) |
| 마케팅/캠페인/광고 | [docs/claude/marketing.md](docs/claude/marketing.md) |
| 공공데이터 DB INSERT | [docs/claude/public-data.md](docs/claude/public-data.md) |
| 기획안/제안서 HTML | [docs/claude/proposals.md](docs/claude/proposals.md) |
| 서버 포트/Nginx/배포 | [docs/claude/server-infra.md](docs/claude/server-infra.md) |
| MCP Unity (Claude ↔ Unity Editor) | [docs/claude/mcp-unity.md](docs/claude/mcp-unity.md) |

⚠️ 해당 작업을 할 때만 관련 파일을 읽을 것. 불필요한 파일은 읽지 않는다.

---

*최종 업데이트: 2026-05-01 (슬림화 — UI/디자인/코드스타일/MCP는 docs/claude/로 이동)*
