# Claude Code 팀에 개선점 제보 (GitHub 이슈 자동화)

사용자가 작업 중 Claude Code 자체의 버그·UX 개선점을 발견하고 다음과 같은 의도를 표시하면 — 예: **"클로드에 제보해줘"**, **"이거 GitHub에 올려줘"**, **"Anthropic에 이슈로 올려줘"**, **"Claude Code 개선요청 올려줘"** 등 — AI는 자동으로 다음 절차를 수행.

## 대상 채널 (Anthropic 공식)

`https://github.com/anthropics/claude-code/issues` — Claude Code의 `/help`가 안내하는 공식 피드백 채널. Anthropic 엔지니어들이 직접 triage. **이메일/디스코드/별도 폼 아님.** 이 리포는 `has_discussions: false`라 Discussions 스레드 불가능 — 이슈만 가능.

## 절차

1. **사용자 의도 확인** — 제보할 내용·재현 단계·환경을 사용자와 같이 정리 (특히 스크린샷 있으면 첨부 권장)
2. **본문 초안 작성** — 반드시 **영문 본문 먼저 + 그 아래 한국어 번역 병기** (Anthropic 엔지니어 가독성 + 사용자 본인 검증 둘 다 목적). 구조: Summary / Steps to Reproduce / Expected / Actual / Impact / Suggested Improvements / Environment / Related Issues
3. **사용자에게 본문 보여주고 승인 받기** — 공개 저장소에 사용자 계정으로 올라가는 작업이므로 절대 무단 게시 금지
4. **유사 이슈 검색** — GitHub Search API로 키워드 검색해 관련 기존 이슈 찾고 본문 "Related Issues" 섹션에 링크
5. **이슈 생성** — REST API (`POST /repos/anthropics/claude-code/issues`) 사용. 라벨 후보: `bug`/`enhancement`/`feature request`, `area:auth`/`area:ui`/`area:cli` 등, `platform:windows`/`platform:vscode`, `has repro` — 외부 컨트리뷰터도 일부는 통과함 (전부 안 통과해도 무시하고 진행)
6. **관련 이슈에 cross-reference 코멘트** — 4번에서 찾은 기존 이슈 중 가장 관련 있는 곳에 새 이슈 번호 링크하는 짧은 코멘트 (스레드 효과)
7. **결과 보고** — 생성된 이슈 URL · 적용된 라벨 · cross-reference한 이슈 URL 모두 사용자에게 알려줌

## 인증

PAT는 **반드시 `c:\woopang\server\.env`의 `GITHUB_PAT` 환경변수에서 읽기** (또는 `gh` CLI 인증). `gemini_origin` 리모트 URL에 박혀있던 토큰(`ghp_F0ypNw...`)은 2026-05-27 revoke 완료 — **다시 리모트 URL에 토큰을 박지 말 것** (CLAUDE.md 6번 섹션 정책과 동일). git credential helper(`wincred`)가 이미 자동 처리.

PowerShell에서 읽는 패턴:

```powershell
$envPath = "c:\woopang\server\.env"
$token = (Get-Content $envPath | Where-Object { $_ -match '^GITHUB_PAT=' } | ForEach-Object { ($_ -split '=', 2)[1].Trim() })
if(-not $token){ Write-Output "GITHUB_PAT not found in .env"; exit }
$headers = @{
  Authorization = "Bearer $token"
  Accept = "application/vnd.github+json"
  "X-GitHub-Api-Version" = "2022-11-28"
  "User-Agent" = "ggapearl-claude-code"
}
```

## 본문 템플릿 (이슈 생성 페이로드)

```text
> _Filed via Claude Code by ggapearl — bilingual report (English first, Korean below)._

## Summary
<한 문단으로 핵심 증상 요약>

## Steps to Reproduce
1. ...
2. ...

## Expected Behavior
<바람직한 동작 — 가능하면 우선순위별로 여러 옵션 제시>

## Actual Behavior
<실제로 일어나는 일 — 가능하면 데이터/스크린샷>

## Impact
- <누가 어떻게 영향받는지>
- <Anthropic 입장에서 우선순위 가늠할 수 있는 비용·신뢰성·금전 측면 한두 줄>

## Suggested Improvements
1. ...
2. ...

## Environment
- OS: ...
- Claude Code: ...
- Plan: ...
- Repro setup: ...

## Related Issues
- #xxxxx — <간단 설명>

---

# 한국어 (Korean translation)

## 요약
<위 Summary 한국어 번역>

## 재현 방법
...

## 기대 동작
...

## 실제 동작
...

## 영향
...

## 개선 제안
...

## 환경
...

## 관련 이슈
...
```

## 라벨 가이드

라벨 적용은 외부 컨트리뷰터 권한 한계로 일부만 통과하지만, 요청 시 페이로드에 포함해두면 GitHub가 매칭되는 것만 적용함. 자주 쓰는 조합:

| 카테고리 | 라벨 후보 |
|---------|-----------|
| 버그 류 | `bug`, `has repro` (재현단계 있을 때) |
| 기능 요청 | `enhancement`, `feature request` |
| 영역 | `area:auth`, `area:ui`, `area:cli`, `area:tools`, `area:mcp`, `area:hooks`, `area:ide`, `area:agent-sdk`, `area:agents`, `area:claude-code-web` 등 |
| 플랫폼 | `platform:windows`, `platform:vscode`, `platform:macos`, `platform:linux`, `platform:wsl`, `platform:ios`, `platform:android` |
| 데이터 손실 위험 | `data-loss` |
| 성능 | `perf:cpu`, `perf:memory`, `perf:reliability`, `performance` |

전체 목록은 `GET /repos/anthropics/claude-code/labels?per_page=100`로 한 번 fetch해 캐싱.

## 유사 이슈 검색 패턴

```powershell
Add-Type -AssemblyName System.Web
$q = 'repo:anthropics/claude-code is:issue <키워드>'
$encoded = [System.Web.HttpUtility]::UrlEncode($q)
$r = Invoke-RestMethod -Uri "https://api.github.com/search/issues?q=$encoded&per_page=5" -Headers $headers
$r.items | ForEach-Object { Write-Output "  #$($_.number) [$($_.state)] $($_.title)" }
```

키워드는 증상의 핵심 동사·명사 위주로 2~4개 변형해서 검색. 결과 중 같은 영역(area:auth 등) 라벨이 붙은 open 이슈를 우선순위로 cross-reference 후보로 고려.

## cross-reference 코멘트 패턴

```text
Cross-referencing a narrower, complementary issue I just filed: **#<신규번호>**.

<한 문단으로: 신규 이슈가 이 broader 이슈의 어느 한 부분 / 어떤 specific failure mode인지 설명>

Filed with full repro steps and a bilingual (EN / KR) body in #<신규번호>.
```

## 선례

- **2026-05-27 · #62790** — `[BUG] /account shows 'Not authenticated' in older windows after another window logs in/out, while Usage bars still populate`
  - 영문+한글 병기 본문 (~6900자)
  - 라벨 5개 자동 적용: `bug`, `has repro`, `platform:windows`, `area:auth`, `platform:vscode`
  - #22872에 cross-reference 코멘트 완료 ([링크](https://github.com/anthropics/claude-code/issues/22872#issuecomment-4554003042))
  - **향후 같은 류 제보의 본문/라벨 구조 템플릿으로 참고**

## 절대 금지

- 사용자 승인 없이 이슈 자동 게시
- 토큰을 출력/로그/커밋에 노출 (절대 토큰값을 stdout이나 채팅 응답에 출력 금지 — `Length: 40`처럼 메타데이터만 표시)
- 영문 단독 또는 한국어 단독 본문 (둘 다 필수)
- Discussions API 호출 (해당 리포는 비활성화 상태)
- `gemini_origin` 같은 리모트 URL에 PAT 박기 (CLAUDE.md 6번 섹션 위배)
