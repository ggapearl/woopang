# 효딩쓰 iOS

대표님 PC 의 데스크 비서 「효딩쓰」를 아이폰에서 쓰는 앱. **대표님 개인용 — 앱스토어 심사·외부 배포 안 함.**
대표님 아이폰에는 **TestFlight 내부 테스트**(테스터는 대표님 한 명)로 받는다 — 2026-09-28 결정, 순서는 **[TESTFLIGHT.md](TESTFLIGHT.md)**.

## 2.0 부터 「포장지」 앱 (2026-10-01 대표님 결정)

> 「농민닷컴처럼 포장지를 iOS 용으로 만들어 놓고, 실제 개발은 웹으로 운용해서 매번 새로 빌드하지 않게 (폰 기능 개발할 때만 빼고)」

- **화면은 웹이다**: `https://woopang.com/hyodingsseu/` — 안드로이드 앱(`apps/hyodingsseu-android`)과 **같은 화면**(저장소 `apps/hyodingsseu-android/web`).
  Windows PC 에서 그 폴더를 고치면 **아이폰·안드로이드 둘 다 그 자리에서 바뀐다.** 앱을 다시 빌드하지 않는다.
- **앱(이 폴더)이 하는 일은 폰 기능만**: 화면을 꽉 채운 WKWebView + 폰 기능 다리 `DeskNative`
  (아이폰 목소리로 읽기 · 진동 · 바깥 링크 열기 · 기기 이름) + 마이크 허락 + 인터넷 안 될 때 안내 화면.
  다리 이름은 안드로이드 Capacitor 플러그인과 같다(`window.Capacitor.Plugins.DeskNative`) — 웹 코드는 폰을 가리지 않는다.
- **다시 빌드할 때**: ① 폰 기능을 더하거나 바꿀 때(`Shell/DeskNative.swift` · `ShellPages.bridgeJS`) ② TestFlight 90일 만료 전(빌드 번호만 올려서).
- 1.x 의 SwiftUI 화면(DeskStore·MainView 등 — 화면을 고칠 때마다 빌드해야 했다)은 커밋 `d4a47df` 에 있다.

```
아이폰 앱(포장지) ── WKWebView ──▶ https://woopang.com/hyodingsseu/   (nginx → AI Office 8000 의 /AI/desk-app/ = 웹 화면 파일)
        │                              └─ 화면이 부르는 API: https://woopang.com/AI/api/desk/<주소>
        └─ DeskNative (목소리·진동·링크·기기 이름)              (nginx → AI Office 8000 → PC 효딩쓰 127.0.0.1:47834 창 / 47835 뒤의 두뇌)
```

- 서버 쪽 코드는 **이 저장소에 없다** (`server/` 는 Windows 개발 PC 에만 있는 로컬 전용). Mac 에서 손댈 서버는 없다.
- 인증: PC 창 ☰ → 휴대폰 앱 → **「연결 코드 받기」** 로 나온 6자리를 넣으면 기기 토큰을 받는다(웹 화면의 저장소에 남는다).
  1.x 에서 쓰던 연결은 Keychain 에 있어 2.0 으로 넘어오지 않는다 — **처음 한 번 다시 연결**.
- 말로 시키기는 웹 화면이 녹음(MediaRecorder, 아이폰은 m4a)해서 PC 로 보내 받아적는다 — 서버가 m4a 를 받는다.

## 파일

| 파일 | 하는 일 |
|---|---|
| `Hyodingsseu/App/HyodingsseuApp.swift` | 앱 시작 · 소리 설정(무음 스위치와 상관없이 스피커, 마이크 함께) · 화면 꽉 채우기(키보드는 비켜 감) |
| `Hyodingsseu/Shell/WebShell.swift` | WKWebView · 바깥 주소는 사파리로 · 마이크 허락 · 인터넷 안 될 때 안내 · 웹 프로세스 죽으면 다시 |
| `Hyodingsseu/Shell/DeskNative.swift` | 폰 기능 다리 — `haptic` · `openExternal` · `deviceName` · `speak`/`stop`(+`speechDone` 사건) · `setBars` |
| `Hyodingsseu/Shell/ShellPages.swift` | 웹에 넣는 다리 JS(`bridgeJS`) · 안내 화면 HTML |

## Mac 에서 빌드 (빌드 세션용)

필요: macOS · **Xcode 16 이상** · Homebrew · 대표님 Apple 개발자 계정(팀 `DDX8R79VU2`)으로 Xcode 로그인

```bash
cd <woopang 저장소>
git pull
cd apps/hyodingsseu-ios
brew install xcodegen          # 처음 한 번
xcodegen generate              # project.yml → Hyodingsseu.xcodeproj (커밋하지 않는다)

# 컴파일 확인 (서명 없이 — 시뮬레이터 없어도 된다)
xcodebuild -project Hyodingsseu.xcodeproj -scheme Hyodingsseu \
  -destination 'generic/platform=iOS' CODE_SIGNING_ALLOWED=NO build
```

- **TestFlight 로 올리기(대표님이 쓰시는 방법)**: [TESTFLIGHT.md](TESTFLIGHT.md) — archive → `ExportOptions.plist` 로 업로드 → 내부 테스트.
- 서명: Automatic, 팀 `DDX8R79VU2`, 번들 ID `com.que.hyodingsseu` (1.x 와 같은 앱 — TestFlight 에서 업데이트로 덮어쓴다).
- 화면을 들여다보려면: 아이폰 설정 › 앱 › Safari › 고급 › 웹 속성 검사기 켜기 → Mac 사파리 › 개발 › (아이폰) › 효딩쓰.

### 빌드 세션이 지킬 것

1. **이 폴더(`apps/hyodingsseu-ios/`)만** 고친다. Unity(`Assets/` 등)·다른 폴더는 건드리지 않는다.
   **화면(웹)은 여기서 고치지 않는다** — `apps/hyodingsseu-android/web` 은 Windows PC 가 고치고, 고치면 바로 반영된다.
2. 컴파일 오류는 **뜻을 바꾸지 않는 최소 수정**으로 고친다. 동작을 바꿔야 할 것 같으면 고치지 말고 보고한다.
3. 고쳤으면 `git pull --rebase` → 커밋(`fix(ios): 무엇을 왜`) → `git push`. **이 폴더의 파일만** 스테이징한다.
4. `.xcodeproj` · 토큰 · 인증서 · 프로비저닝 프로파일은 커밋하지 않는다 (`.gitignore` 에 있음).
5. 서버 주소·API 모양은 바꾸지 않는다 — 서버는 Windows 개발 PC 에서 고친다. 필요하면 보고만.
6. 끝나면 보고: 빌드 성공/실패 · 고친 파일과 이유 · 아이폰 설치 여부 · 화면에서 이상한 점.

## 처음 쓰기

1. PC 에서 효딩쓰 창 ☰ → 휴대폰 앱 → 「연결 코드 받기」
2. 아이폰 앱에 6자리 입력 → 바로 대화 화면
3. ☰ 로 AI Office 직원 고르기 · 설정(답을 소리로 듣기 · 목소리 · 말 빠르기 · 연결 끊기)

## API (웹 화면이 부르는 것)

| 주소 | 하는 일 |
|---|---|
| `POST pair` `{code, device}` | 짝 짓기 → `{token}` (인증 없음, IP 당 10분 10번) |
| `GET state` | 지금까지의 대화·상태·모델·말 빠르기 |
| `GET events?since=&boot=&wait=25` | 긴 폴링 — 새 사건. `boot` 가 바뀌면 `{reset:true}` |
| `POST send` `{text}` | 글로 시키기 |
| `POST voice` (m4a·webm 본문) | 말로 시키기 → PC 가 받아적고 `{text}` |
| `POST tts` `{text}` | 소희 목소리 m4a |
| `POST decide` `{id, decision}` | 허락 카드: allow · always · deny |
| `POST answer` `{id, answers}` | 선택 카드 답 |
| `POST stop` · `new` · `model {key}` · `speed {speed}` · `unpair` | 멈추기 · 새 대화 · 깊이 · 말 빠르기 · 이 기기 열쇠 지우기 |
| `GET office/workers` · `GET office/history?id=` · `POST office/chat {id, text}` | AI Office 직원 명단 · 기록 · 지시 |
| `GET doc?path=` | 답 속 PC 문서 경로 → 폰에서 열리는 서명 링크 `{url}` (30일, 2026-09-28) |

사건 모양은 PC 창(`ui.html`)이 받는 것과 같다: `user · text_start · delta · text · tool · tool_done · permission · question · permission_closed · incoming · note · error · result · state · model · office · cleared`.
`user`·`incoming`·`text` 에는 받은 시각 `ts`(유닉스 초, 2026-09-30), `user`·`incoming` 에는 `images`·`files` 가 붙을 수 있다.
