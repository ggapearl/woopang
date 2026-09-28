# 효딩쓰 iOS — 바뀐 것

빌드 요청마다 맨 위에 한 덩어리씩 적는다. 빌드 세션은 여기서 「이번에 확인할 것」을 본다.

## 1.0.2 (3) — 2026-09-28 · **TestFlight 첫 빌드** (올리는 법: `TESTFLIGHT.md`)

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
