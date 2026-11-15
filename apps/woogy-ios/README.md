# 우기 (Woogy) iOS

「우리동네 기부천사, 우기」 — 가까운 이웃의 필요를 동네 가게에서 대신 결제해 주는 앱의 **아이폰 앱**.
지금은 **TestFlight 내부 테스트**(대표님)로 받는다. 올리는 순서: **[TESTFLIGHT.md](TESTFLIGHT.md)**.

> 2026-11-15 대표님: 「woopang.com 내에서 라우팅하는 걸로 해서 ex. 'woopang.com/woogy' 내의 서버를 이용하는 걸로 해서 당장 도메인 사지 않더라도
> 직접 다 기능 작동하도록 해서 아이폰용으로 testflight 할 수 있도록 … 모바일에서 직접 테스트할테니까 해당 앱 아이폰에서 사용할 수 있도록 해줘」

## 「포장지」 앱

- **화면은 웹이다** — `https://woopang.com/woogy/` (우기 서버: Windows PC 의 Flask 6690 · nginx `/woogy/` · PostgreSQL 5655).
  화면 · 기능을 고치면 **앱을 다시 빌드하지 않아도** 그 자리에서 바뀐다. 서버 코드는 이 저장소에 없다(로컬 전용).
- **앱이 하는 일**: 화면을 꽉 채운 WKWebView + 웹의 알림창(alert · confirm · prompt) + 새 창(아래에서 올라오는 창) +
  바깥 링크는 사파리 · 전화 · 카드 앱으로 + **알림(APNs)** + **간편로그인 다리**(시스템 브라우저 창) + 인터넷이 안 될 때 안내 화면.
- **다시 빌드할 때**: ① 폰 기능(`Woogy/Shell/`)을 바꿀 때 ② TestFlight 90일 만료 전(빌드 번호만 올려서 — Windows 의 `ios_expiry.py` 가 알린다).
- 실행 화면은 우기 크림색(#FFFBEA) 빈 화면(자동 생성) → 바로 우기 화면.
- **아이콘은 원본 로고(광택)** — 노란 광택 바탕 · 초록 날개 하트. `design/logo_gloss_original.png`(원본)를 `design/make_icon.py` 가
  테두리 안쪽만 잘라 `icon-1024.png`(RGB 1024 · 투명 없음)로 만든다. 2026-11-16 대표님 지시 — **평면 아이콘(`woogy_icon_square.png`)으로 되돌리지 말 것.**

```
우기 (포장지) ── WKWebView ──▶ https://woopang.com/woogy/          입장 코드(시험 기간, 쿠키 180일) → 우기 화면(로그인 90일)
     ├─ WoogyNative ◀── window.WoogyApp.call(…)                  우기 주소의 맨 위 화면에서만
     │    pushRegister  → 알림 허락 · 기기 토큰 → 웹이 POST /woogy/api/device-token {token, platform:'ios'}
     │    auth {url}    → 시스템 브라우저 창 → … → com.que.woogy://auth?t=<1회용> → 웹 화면 /woogy/auth/app?t=…
     │    openExternal  → 사파리 · 전화 · 문자
     └─ 알림을 누르면 → 서버가 실은 link(https://woopang.com/woogy/#…)로
nginx(443) → 우기 서버(127.0.0.1:6690) → wg_push.py 가 APNs 로 직접 보낸다(팀 키 .p8 · topic com.que.woogy)
```

## 웹이 쓰는 다리 — `window.WoogyApp`

앱 안에서 우기 주소를 열 때만 생긴다(`ShellPages.bridgeJS`, 문서 시작 때). 없으면 그냥 웹(사파리)이다.

| 부르는 법 | 답(Promise) | 하는 일 |
|---|---|---|
| `WoogyApp.call('pushRegister')` | `{granted, token?, platform:'ios', error?}` | 알림 허락을 **처음 한 번만** 묻고 기기 토큰을 준다. 그 뒤엔 묻지 않고 지금 상태만 |
| `WoogyApp.call('auth', {url})` | `{ok:true}` · `{ok:false, canceled:true}` | `url`(우기 주소)을 시스템 브라우저 창으로 연다. 서버가 `com.que.woogy://auth?t=…` 로 보내면 창이 닫히고 웹 화면이 `/woogy/auth/app?t=…` 로 가 로그인된다 |
| `WoogyApp.call('openExternal', {url})` | `{ok}` | https · tel · sms · mailto 만 |

`WoogyApp.platform`(`'ios'`) · `WoogyApp.version` · `WoogyApp.build` 도 있다. 사용자 에이전트 끝에 `WoogyApp/<버전>` 이 붙는다(서버가 앱인지 알 수 있게).

## 앱이 정하는 주소

| 주소 | 앱이 하는 일 |
|---|---|
| `https://woopang.com/woogy…` | 앱 안에서 연다 |
| 우기 화면에서 **누른** 바깥 링크 | 사파리로 |
| 화면이 스스로 넘어가는 바깥 주소(결제창 · 카드사 인증) | 앱 안에서 — 결제가 끝나면 우기(successUrl)로 돌아와야 해서 |
| `target=_blank` · `window.open` (약관 · 가게 영수증 · 결제 팝업) | 아래에서 올라오는 창(같은 로그인). 아래로 밀거나 「닫기」 |
| `tel:` · `sms:` · 카드/결제 앱(`ispmobile://` 등) · 앱스토어 | 그 앱으로 |
| `com.que.woogy://auth?t=…` | 웹 화면에서 `/woogy/auth/app?t=…` 를 연다(토큰 16~200자 · 영문 숫자 `-` `_` 만) |

밀어서 뒤로 가기는 껐다 — 둘러보기 카드(좌우로 넘기기)와 겹친다. 뒤로는 화면 안의 버튼으로.

## 지키는 것 (보안)

- 폰 기능은 **https://woopang.com 의 `/woogy…` 맨 위 화면**에서만 준다(`WoogyNative` 가 frame · origin · 경로를 본다). 결제창 · 다른 사이트 · 틀(iframe)은 못 부른다.
- 간편로그인 복귀 토큰은 서버가 만드는 **1회용 · 5분**짜리다. 앱은 모양만 보고 그대로 서버에 넘긴다(앱에 비밀 없음).
- 앱에는 **비밀(키 · 입장 코드 · 비밀번호)이 하나도 없다** — 이 저장소는 공개다. 알림 키(.p8)는 Windows PC 에만 있다.

## 앱스토어 출시 전에 할 일

- `WebShell.swift` 의 `isInspectable = true` 끄기(시험 기간에만 Mac 사파리로 들여다보려고 켜 둠)
- App Store Connect 「앱 개인정보」: 웹이 받는 정보(휴대폰 번호 · 대략 위치 · 사진 · 결제 기록) 적기
- 입장 코드(시험 기간 문)를 서버에서 끄기 — 앱은 고칠 것 없음

## 파일

| 파일 | 하는 일 |
|---|---|
| `Woogy/App/WoogyApp.swift` | 앱 시작 · 간편로그인 복귀(`onOpenURL`) · `AppDelegate`(알림 토큰 · 앱을 보고 있을 때도 알림 띄움 · 알림을 누르면 그 화면으로) |
| `Woogy/Shell/WebShell.swift` | `Shell`(첫 주소 · 우기 주소 판별 · 복귀 주소 바꾸기) · `Router`(알림 · 로그인 복귀가 화면을 움직이는 한 길) · WKWebView · 주소 결정 · 새 창 · 알림창 · 오프라인 · 웹 프로세스 죽으면 다시 |
| `Woogy/Shell/WoogyNative.swift` | 웹 → 폰 기능 다리(`pushRegister` · `auth` · `openExternal`) |
| `Woogy/Shell/Auth.swift` | 간편로그인 시스템 브라우저 창(`ASWebAuthenticationSession`, scheme `com.que.woogy`) |
| `Woogy/Shell/Push.swift` | 알림 허락 · 기기 토큰(효딩쓰 2.0.1 과 같은 코드) · 알림을 눌러 들어옴 |
| `Woogy/Shell/ShellPages.swift` | `window.WoogyApp` 다리 JS · 인터넷이 안 될 때 안내 화면 |
| `Woogy/Info.plist` | `INFOPLIST_KEY_` 로 못 만드는 키만 — 간편로그인 복귀 주소 `com.que.woogy://` |
| `project.yml` | XcodeGen 설정(정본) — `.xcodeproj` 는 커밋하지 않는다 |
| `design/logo_gloss_original.png` · `design/make_icon.py` | 앱 아이콘 원본(광택 로고)과 `icon-1024.png` 를 만드는 스크립트 — 타깃(`Woogy/`) 밖이라 앱에 들어가지 않는다 |
| `ExportOptions.plist` | TestFlight 업로드 |
