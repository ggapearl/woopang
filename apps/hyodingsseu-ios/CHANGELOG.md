# 효딩쓰 iOS — 바뀐 것

빌드 요청마다 맨 위에 한 덩어리씩 적는다. 빌드 세션은 여기서 「이번에 확인할 것」을 본다.

## 2.0.0 (5) — 2026-10-01 · **「포장지」 앱으로 바꿈** (올리는 법: `TESTFLIGHT.md`)

대표님 결정: 「농민닷컴처럼 포장지를 만들어 놓고 실제 개발은 웹으로 — 매번 새로 빌드하지 않게(폰 기능 개발할 때만 빼고)」.
- 화면은 **웹** `https://woopang.com/hyodingsseu/` — 안드로이드 앱과 같은 화면(`apps/hyodingsseu-android/web`). 화면을 고쳐도 다시 빌드하지 않는다.
- 이 앱은 WKWebView + 폰 기능 다리 `DeskNative`(아이폰 목소리 읽기·진동·바깥 링크·기기 이름) + 마이크 허락 + 오프라인 안내 화면만 (README 「파일」).
- 1.x 의 SwiftUI 화면 코드는 지웠다(커밋 `d4a47df` 에 있다). 번들 ID 그대로 → TestFlight 에서 업데이트로 덮어쓴다.
- 상태 표시줄 글자는 늘 흰색(화면 맨 위 막대가 남색) — `UIStatusBarStyleLightContent`.
- ⚠ 1.x 의 연결(Keychain)은 넘어오지 않는다 → 처음 한 번 PC 창 ☰ → 휴대폰 앱 → 새 코드로 다시 연결.
- 1.0.3 (4) 가 2026-10-01 08:20 TestFlight 에 올라가 있다(아래 결과) → **앱 기록·내부 테스트 그룹·자동 배포가 이미 있으니**
  `TESTFLIGHT.md` 1절은 건너뛰고 2절(빌드·업로드)만. 대표님 아이폰은 TestFlight 에서 「업데이트」.

Windows 에서 미리 확인한 것: 다리 JS(`ShellPages.bridgeJS`)를 실제 `woopang.com/hyodingsseu/` 에 넣고 아이폰 크기(390×844) Edge 로 열어 —
웹이 폰 기능을 알아보고(`getPlatform()=ios`, 시작하자마자 `setBars` 를 다리로 부름) 연결 화면이 오류·가로 넘침 없이 뜸. Swift 는 컴파일 못 함.

**이번에 확인할 것**:
1. 컴파일 — 새로 쓴 곳: `WebShell`(UIViewRepresentable · `@MainActor` Coordinator · async `decidePolicyFor` · async `requestMediaCapturePermissionFor`)·
   `DeskNative`(`WKScriptMessageHandlerWithReply` 의 async `userContentController(_:didReceive:)` · `AVSpeechSynthesizerDelegate` nonisolated)·`ShellPages`
2. TestFlight 업로드 → 대표님 아이폰에서 설치(1.x 위에 덮어쓰기)
3. 대표님이 쓰시면서: 연결 코드로 짝 짓기 · 마이크로 말해서 시키기(처음에 마이크 허락 한 번) · 답 「읽기」(아이폰 목소리) ·
   답 속 문서 경로·기사 링크가 사파리로 열리는지 · 키보드가 입력칸을 가리지 않는지 · 비행기 모드에서 안내 화면 → 다시 시도

**결과** — 2026-10-01 Mac 빌드 세션 (Xcode 26.3 · iOS 26.2 SDK · xcodegen 2.46.0):
- **컴파일 성공** — 오류 0. 처음 빌드에서 경고 1건이 **마이크 허락 함수가 불리지 않는다**는 뜻이라 고쳤다(아래). 고친 뒤 경고 0.
  `decidePolicyFor`·`createWebViewWith`·`DeskNative` 의 async `didReceive`·nonisolated `didFinish`·`ShellPages`·`.ignoresSafeArea(.container)` 는 그대로 통과
  (바이너리에 ObjC 셀렉터가 들어간 것까지 확인).
- `PrivacyInfo.xcprivacy` 가 `.app` 안에 들어감.
- **TestFlight 업로드 성공** — 2.0.0 (5), 2026-10-01 18:56 KST. 처리 끝(VALID) · 수출 규정 자동 면제 ·
  자동 배포로 그룹 「대표님」 에 들어감(`IN_BETA_TESTING`) · 외부 상태 `NOT_APPLICABLE` · 그룹은 「대표님」(내부) 하나 · 심사 제출 안 함.
- **고친 파일 2곳** (뜻은 그대로, 적어 둔 의도대로 실제 동작하게):
  1. `Shell/WebShell.swift` — 마이크 허락 함수의 이름표.
     async 판 Swift 이름은 `webView(_:decideMediaCapturePermissionsFor:initiatedBy:type:)` 인데(SDK 의 `WK_SWIFT_ASYNC_NAME`, iOS 15+),
     콜백 판 이름표(`requestMediaCapturePermissionFor:initiatedByFrame:`)로 적혀 있어 컴파일러가 "nearly matches" 경고를 냈다.
     이대로면 WebKit 이 이 함수를 **부르지 않아** 「구현 안 함 = 매번 묻기(Prompt)」가 된다 → 주석의 의도(웹이 한 번 더 묻지 않게)와 반대.
     이름표 두 개만 바꿨다. 본문(woopang.com 만 `.grant`, 그 밖은 `.deny`)은 그대로.
  2. `project.yml` + 새 파일 `Hyodingsseu/Info.plist` — 상태 표시줄 흰 글자.
     `INFOPLIST_KEY_UIViewControllerBasedStatusBarAppearance: NO` 는 Xcode 가 자동 생성을 **지원하지 않는 키**라
     (Xcode 26 의 `CoreBuildSystem.xcspec` 에는 `UIStatusBarHidden`·`UIStatusBarStyle` 만 있다) 앱 Info.plist 에서 **조용히 빠졌다.**
     빠지면 `UIStatusBarStyleLightContent` 가 무시되고, `setBars` 는 아이폰에서 아무 일도 안 하므로 라이트 모드에서 남색 막대 위 시계·배터리가 검게 나온다.
     → 그 키 하나만 담은 `Hyodingsseu/Info.plist` 를 두고 `INFOPLIST_FILE` 로 연결(`GENERATE_INFOPLIST_FILE: YES` 는 그대로 — Xcode 가 빌드 때 합친다).
     합친 결과에 자동 생성 키(이름·버전·마이크 문구·실행 화면·씬·세로 고정·수출 규정)가 모두 남아 있는 것 확인. 앞으로 INFOPLIST_KEY_ 로 안 되는 키는 이 파일에.
- 대표님 확인 남음: TestFlight 에서 「업데이트」 → 위 「이번에 확인할 것」 3번 + 상태 표시줄 글자가 라이트·다크 모두 흰색인지 · 마이크 첫 사용 때 허락을 한 번만 묻는지.

## 1.0.3 (4) — 2026-09-30 · **TestFlight 첫 업로드** (2026-10-01 08:20 올라감 — 1.x 마지막 SwiftUI 판)

1.0.2 (3) 는 준비만 하고 올린 기록이 없다 → **이번이 첫 업로드**라 `TESTFLIGHT.md` 1절(App Store Connect 앱 기록 · 내부 테스트 그룹)이 필요하다.
1.0.2 (3) 의 바뀐 것(아래 덩어리)도 이번 빌드에 모두 들어 있다.

2026-09-30 Windows 세션이 고침:
- **말풍선마다 받은 시각** — 텔레그램처럼 작게: 오늘이면 「오후 10:51」, 아니면 「9/28 오후 10:51」(`StampText`).
  PC 가 사건(`user`·`text`·`incoming`)마다 붙여 주는 `ts`(유닉스 초)를 `ChatItem.time` 에 담는다(`DeskStore.time`). `ts` 가 없는 예전 사건은 시각을 안 그린다.
  대표님 말은 말풍선 오른쪽 아래(흰색 66%), 효딩쓰 답은 이름 옆, 알림 말풍선은 오른쪽 아래. 「읽기」(소리)에는 안 들어간다(본문만 읽음).
  PC 창·안드로이드 앱은 이미 같은 모양으로 나간다(서버 쪽 반영 끝).

**이번에 확인할 것**:
1. 컴파일 — Windows 에서 컴파일하지 못했다. 새로 쓴 곳: `StampText`(View · `if let` 본문 · DateFormatter `a h:mm`)·
   `ChatItem.time`/`init(time:)`·`DeskStore.time(_:)`·`text` 사건의 `update` 클로저(`item.time = at`)·
   `UserBubble`/`AIMessage`/`InboxBubble` 의 `time` 인자(기본값 nil 이라 다른 호출은 그대로)
2. 1.0.2 (3) 의 「이번에 확인할 것」 1~4 도 같이 (아래)
3. TestFlight 업로드 → 대표님 아이폰에서 설치 → 말풍선에 시각이 보이는지

**결과** — 2026-10-01 Mac 빌드 세션 (Xcode 26.3 · iOS 26.2 SDK · xcodegen 2.46.0):
- **컴파일 성공, 고친 파일 없음.** 서명 없이 build 한 번에 통과 — 이 앱 소스에서 오류 0 · 경고 0.
  위 확인 대상(`StampText` · `ChatItem.time`/`init(time:)` · `DeskStore.time(_:)` · `text` 사건 `update` 클로저 ·
  `UserBubble`/`AIMessage`/`InboxBubble` 의 `time` 인자)과 1.0.2 (3) 의 `DocLinks` · `AttachmentStrip` · `handleLink`(nonisolated) · `FileLink` 모두 그대로.
- `PrivacyInfo.xcprivacy` 가 `.app` 안에 들어감 (UserDefaults · CA92.1). 아이콘 1024 · 알파 없음.
- **TestFlight 업로드 성공** — 1.0.3 (4), 2026-10-01 08:20 KST. 처리 끝(VALID) · 수출 규정 질문 없음(자동 면제).
- 내부 테스트 그룹 **「대표님」**: 자동 배포 켬 · 공개 링크 꺼짐 · 테스터 `ggapearl@gmail.com`(계정 소유자, 초대 메일 나감) · 이 빌드 들어감(`IN_BETA_TESTING`).
  외부 그룹 없음 · 외부 상태 `NOT_APPLICABLE`(내부 테스트 전용 업로드라 외부로 못 넘김) · 심사 제출 안 함.
- 첫 업로드라 생긴 일 (`TESTFLIGHT.md` 1절):
  - 이 앱은 capability(푸시 등)가 없어 archive 가 **와일드카드(`*`) 팀 프로파일**로 서명됐고, 그래서 1-1 의 전제와 달리
    App ID 가 자동 등록되지 않았다 → App Store Connect API 로 `com.que.hyodingsseu` 를 직접 등록.
  - 앱 기록은 API 로 만들 수 없어 대표님이 브라우저에서 만듦(효딩쓰 · SKU `hyodingsseu`).
    앱 기록 없이 2-4 를 돌리면 `Error Downloading App Information`(`missingApp`)으로 실패한다.
  - 그룹·테스터·자동 배포는 브라우저 대신 API 로 만들었다.
- 대표님 확인 남음: 아이폰 TestFlight 에서 설치 · 말풍선에 시각이 보이는지 · 1.0.2 (3) 「이번에 확인할 것」 4번.

## 1.0.2 (3) — 2026-09-28 · TestFlight 준비 (올리지 않음 — 1.0.3 (4) 로 올린다)

2026-09-28 Windows 세션이 고침 — 안드로이드 앱(`apps/hyodingsseu-android`)과 기능을 맞췄다. 서버 쪽은 이미 반영돼 있다.

- **TestFlight 준비**: 버전 1.0.2 (3) · `ITSAppUsesNonExemptEncryption = NO`(수출 규정 질문 없음) ·
  `PrivacyInfo.xcprivacy`(UserDefaults 사유 CA92.1 — `project.yml` 에서 resources 로 명시) · `ExportOptions.plist`(내부 테스트 전용 업로드)
- **PC 문서 경로 누르면 열기**: 답 속 `C:\que-desk\…` 경로가 링크로 보이고(`DocLinks` → hyodoc://), 누르면 PC 에 서명 링크(`GET doc?path=`)를 받아
  사파리로 연다(`DeskStore.handleLink`/`openDoc`, `HyodingsseuApp` 의 openURL). 폰에서 md 는 읽기 좋게, 폴더는 그림 목록으로 열린다
- **그림·파일 붙임**: `user`·`incoming` 사건의 `images`(서명 링크 배열)·`files`([{name,url}]) 를 말풍선 아래 작은 그림·파일 링크로(`AttachmentStrip`)
  — 대표님이 텔레그램으로 보낸 사진, 휴대폰으로 나간 카드뉴스가 대화에 보인다
- 새 사건 종류 `incoming` kind `phone_out` 이름 「📱 휴대폰으로 보냄」 — 텔레그램으로 나간 것(효딩쓰·AI Office)이 대화에도 한 줄로
- 「읽기」(아이폰 목소리): 괄호 안·#번호·60d563 같은 6자리 번호·파일 경로는 읽지 않고, 9/29 는 「9월 29일」로 (`Audio.swift` SpeechText —
  대표님 지시: 기계용 내용은 괄호 안에, 소리로는 건너뛴다. PC `voice.for_speech` 와 같은 규칙)
- 켜자마자 「PC 창과 연결됨」 — 예전엔 지금까지 대화를 받고도 첫 긴 폴링(최대 25초)이 끝날 때까지 「연결하는 중」이었다
- 연결 화면 안내: 「☰ → 아이폰 앱」 → 「☰ → 휴대폰 앱」 (PC 창 메뉴 이름이 바뀜 — 안드로이드 앱도 같은 창구를 쓴다)

**이번에 확인할 것**:
1. 컴파일 — 새로 쓴 곳: `DocLinks`(raw 문자열 정규식)·`AttachmentStrip`·`handleLink`(nonisolated, `OpenURLAction` — HyodingsseuApp 에서 store 를 잡아 부름)·`FileLink`
2. `.app` 안에 `PrivacyInfo.xcprivacy` 가 들었는지 (`TESTFLIGHT.md` 2-2)
3. TestFlight 업로드 성공 → 대표님 아이폰에 설치
4. 대표님이 쓰시면서: 연결 코드로 짝 짓기 · 말로 시키기 · 「읽기」로 괄호가 든 답을 읽혀 괄호 안이 빠지는지 · 답 속 `C:\que-desk\…` 경로를 누르면 사파리에서 열리는지

## 1.0.1 (2) — 2026-09-26

- 앱 아이콘·머리 구슬을 **빨간 고양이(큰 눈망울)** 로 — `Assets.xcassets/Cat.imageset`, `AppIcon` 교체
- 답마다 **「읽기」 버튼** — 누르면 그 답을 끝까지 읽고, 읽는 중에 누르면 멈춤 (`DeskStore.readAloud`)
- 서버 쪽(이미 반영): 모델은 Opus 5.5 하나로 고정 — 설정의 「생각의 깊이」 선택지는 하나만 보인다(정상)

**이번에 확인할 것**: 컴파일 · 새 아이콘이 홈 화면에 보이는지 · 상단 고양이 · 「읽기」 버튼으로 소리 나는지

## 1.0.0 (1) — 2026-09-26 첫 버전

- PC 효딩쓰와 같은 대화 (지금까지 대화 받아오기 + 긴 폴링으로 실시간 · 글자 단위로 흘러나옴)
- 글로 시키기 · 말로 시키기(누르고 말하고 다시 누르기 → PC 가 받아적음)
- 허락 카드(허락 / 계속 허락 / 거절) · 선택 카드
- ☰ 대화 상대: 효딩쓰 · AI Office 직원 15명(일하는 중이면 분홍 고리) · 설정 · 새 대화
- 직원과 따로 대화 (지시 → 답 올 때까지 3초마다 확인)
- 답을 소리로: 아이폰 목소리(바로) 또는 PC 소희 목소리 · 말 빠르기 0.8~1.6배
- 생각의 깊이(빠르게·보통·깊게) · 연결 끊기(PC 쪽 열쇠도 지움)
- 라이트·다크 둘 다 (PC 창과 같은 농핑크·남색)

**이번에 확인할 것**: 컴파일 · 연결 코드로 짝 짓기 · 말로 시키기(마이크 권한) · 허락 카드 버튼 · ☰ 에서 직원 대화
