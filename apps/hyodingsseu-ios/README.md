# 효딩쓰 iOS

대표님 PC 의 데스크 비서 「효딩쓰」를 아이폰에서 쓰는 앱. **대표님 개인용 — 앱스토어·TestFlight 배포 안 함.**
PC 창과 **같은 대화**가 보이고, 글·말로 시키고, 허락 카드에 답하고, AI Office 직원 15명과 따로 대화한다.

## 구조

```
아이폰 앱 ──HTTPS──▶ https://woopang.com/AI/api/desk/<주소>
                      (nginx → AI Office 8000 의 프록시, 정해진 주소만 넘김)
                   ──▶ PC 효딩쓰 127.0.0.1:47834 (창)  · 창이 없으면 47835 (뒤의 두뇌)
```

- 서버 쪽 코드는 **이 저장소에 없다** (`server/` 는 Windows 개발 PC 에만 있는 로컬 전용). Mac 에서 손댈 서버는 없다.
- 인증: PC 창 ☰ → 아이폰 앱 → **「연결 코드 받기」** 로 나온 6자리를 앱에 넣으면 기기 토큰을 받는다.
  토큰은 아이폰 **Keychain 에만** 있고, PC 에는 해시만 남는다. 코드는 5분·5번 틀리면 끝.
- 앱이 45초 넘게 안 보이면(잠금·백그라운드) 허락 카드와 답은 예전처럼 텔레그램으로도 간다.

## Mac 에서 빌드 (빌드 세션용)

필요: macOS · **Xcode 16 이상** · Homebrew · 대표님 Apple 개발자 계정(팀 `DDX8R79VU2`)으로 Xcode 로그인 · 아이폰 15 mini (iOS 17 이상, 개발자 모드 켜짐)

```bash
cd <woopang 저장소>
git pull
cd apps/hyodingsseu-ios
brew install xcodegen          # 처음 한 번
xcodegen generate              # project.yml → Hyodingsseu.xcodeproj (커밋하지 않는다)

# 1) 컴파일 확인 (서명 없이 — 시뮬레이터 없어도 된다)
xcodebuild -project Hyodingsseu.xcodeproj -scheme Hyodingsseu \
  -destination 'generic/platform=iOS' CODE_SIGNING_ALLOWED=NO build

# 2) 아이폰에 설치 — Xcode 로 여는 게 가장 쉽다
open Hyodingsseu.xcodeproj      # 기기 선택 → ▶ Run
```

- 서명: Automatic, 팀 `DDX8R79VU2`, 번들 ID `com.que.hyodingsseu` (처음 Run 때 Xcode 가 App ID·프로파일을 알아서 만든다).
- 아이폰에서 처음 열 때 「신뢰하지 않는 개발자」가 뜨면: 설정 › 일반 › VPN 및 기기 관리 › 개발자 앱 › 신뢰.
- 개발 설치는 유료 개발자 계정이라 1년 유지된다. 만료되면 다시 Run.

### 빌드 세션이 지킬 것

1. **이 폴더(`apps/hyodingsseu-ios/`)만** 고친다. Unity(`Assets/` 등)·다른 폴더는 건드리지 않는다.
2. 컴파일 오류는 **뜻을 바꾸지 않는 최소 수정**으로 고친다. 동작을 바꿔야 할 것 같으면 고치지 말고 보고한다.
3. 고쳤으면 `git pull --rebase` → 커밋(`fix(ios): 무엇을 왜`) → `git push`. **이 폴더의 파일만** 스테이징한다.
4. `.xcodeproj` · 토큰 · 인증서 · 프로비저닝 프로파일은 커밋하지 않는다 (`.gitignore` 에 있음).
5. 서버 주소·API 모양은 바꾸지 않는다 — 서버는 Windows 개발 PC 에서 고친다. 필요하면 보고만.
6. 끝나면 보고: 빌드 성공/실패 · 고친 파일과 이유 · 아이폰 설치 여부 · 화면에서 이상한 점.

## 처음 쓰기

1. PC 에서 효딩쓰 창 ☰ → 아이폰 앱 → 「연결 코드 받기」
2. 아이폰 앱에 6자리 입력 → 바로 대화 화면
3. ☰ 로 AI Office 직원 고르기 · 설정(답을 소리로 듣기 · 목소리 · 말 빠르기 · 생각의 깊이 · 연결 끊기)

## API (앱이 부르는 것)

| 주소 | 하는 일 |
|---|---|
| `POST pair` `{code, device}` | 짝 짓기 → `{token}` (인증 없음, IP 당 10분 10번) |
| `GET state` | 지금까지의 대화·상태·모델·말 빠르기 |
| `GET events?since=&boot=&wait=25` | 긴 폴링 — 새 사건. `boot` 가 바뀌면 `{reset:true}` |
| `POST send` `{text}` | 글로 시키기 |
| `POST voice` (m4a 본문) | 말로 시키기 → PC 가 받아적고 `{text}` |
| `POST tts` `{text}` | 소희 목소리 m4a |
| `POST decide` `{id, decision}` | 허락 카드: allow · always · deny |
| `POST answer` `{id, answers}` | 선택 카드 답 |
| `POST stop` · `new` · `model {key}` · `speed {speed}` · `unpair` | 멈추기 · 새 대화 · 깊이 · 말 빠르기 · 이 기기 열쇠 지우기 |
| `GET office/workers` · `GET office/history?id=` · `POST office/chat {id, text}` | AI Office 직원 명단 · 기록 · 지시 |

사건 모양은 PC 창(`ui.html`)이 받는 것과 같다: `user · text_start · delta · text · tool · tool_done · permission · question · permission_closed · incoming · note · error · result · state · model · office · cleared`.
