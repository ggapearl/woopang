# 우팡M (woopangM) iOS

대표님 전용 **우팡 관리자 앱**. 앱스토어에 내지 않는다 — **TestFlight 내부 테스트**(테스터는 대표님 한 명)로만 받는다. 올리는 순서: **[TESTFLIGHT.md](TESTFLIGHT.md)**.

> 2026-11-02 대표님: 「우팡앱 관리자 연결하는 바에 있어서도 '앱' 으로 iOS용으로 빌드하고 testflight 에 올려서 … '효딩쓰' 앱처럼 간편히 … 앱 이름은 'woopangM'」
> 2026-11-14 대표님: 「따로 오프닝 발생하는 거 없이 … 우팡M → 검정색배경에 흰색 강아지 … 비밀번호나 이런 거 없어도 접속할 수 있도록」

## 「포장지」 앱

- **화면은 웹이다** — `https://woopang.com/admin` (관리자 웹: 수정·삭제 요청 · 신고 · 알림 보내기 · 광고/DB 관리 링크). 관리자 화면을 고치면 **앱을 다시 빌드하지 않아도** 그 자리에서 바뀐다.
- **앱이 하는 일**: 화면을 꽉 채운 WKWebView + **기기 열쇠**(비밀번호 대신) + 바깥 링크는 사파리/카카오맵으로 + 웹의 알림창(alert·confirm·prompt) + 인터넷이 안 될 때 안내 화면.
- **다시 빌드할 때**: ① 폰 기능(`Shell/`)을 바꿀 때 ② TestFlight 90일 만료 전(빌드 번호만 올려서 — Windows 의 `ios_expiry.py` 가 14·7·3·1일 전에 효딩쓰로 알린다).
- **오프닝 없음**: 실행 화면은 빈 검정(자동 생성) → 바로 관리자 화면. 아이콘은 우팡 아이콘을 뒤집은 것(검정 바탕 · 흰 강아지).

```
우팡M (포장지) ── WKWebView ──▶ https://woopang.com/admin/m/   연결 화면 — 기기 열쇠로 들어간다
     │                                └─▶ https://woopang.com/admin   관리자 화면 (같은 30일 관리자 세션)
     └─ WoopangMNative: deviceKey → {key, name}    (Keychain 의 무작위 32바이트 · 연결 화면에서만 준다)
nginx(443) → app_improved.py(8080) 의 admin_m 블루프린트 → 허용 요청은 que_outbox → 대표님 효딩쓰 앱
```

서버 쪽 코드는 **이 저장소에 없다**(`server/` 는 Windows 개발 PC 에만 있는 로컬 전용). Mac 에서 손댈 서버는 없다.

## 비밀번호 없이 들어가는 법

1. 처음 켜면 앱이 Keychain 에 **기기 열쇠**(무작위 32바이트, base64url 43자)를 만든다. 이 아이폰에만 남는다(`ThisDeviceOnly` — 백업으로 다른 기기에 옮겨 가지 않는다).
2. 연결 화면(`/admin/m/`)이 `window.webkit.messageHandlers.woopangM.postMessage({m:'deviceKey'})` 로 열쇠를 받아 `POST /admin/m/session` 으로 보낸다. **서버는 열쇠의 해시(sha256)만** 갖는다.
3. 처음 보는 기기면 — 아이폰에 **4자리 코드**가 뜨고, 대표님 **효딩쓰 앱**으로 「우팡M 새 기기 연결 요청」 링크가 온다. 링크를 열어 코드가 같은지 보고 **[허용]** → 아이폰은 3초 안에 관리자 화면으로 넘어간다.
   - 링크는 한 번만 쓰인다. 열기만 해서는(GET) 아무것도 바뀌지 않고, [허용]/[거절] 버튼(POST)만 결정한다. 30분 지나면 사라진다.
   - 링크가 안 오면 PC 에서: `python server\launchers\woopangm_pair.py approve <코드>`
4. 허용된 기기는 그 뒤로 **켜자마자 관리자 화면**이다. 세션은 `/admin` 비밀번호 로그인과 같은 30일 관리자 세션(`login_guard.admin_login`)이고, 끝나면 열쇠로 저절로 다시 들어간다.
5. 앱 안에서 「비밀번호로 들어가기」(`/admin/login?pw=1`)도 된다 — 비밀번호로 들어간 세션이면 그 기기를 바로 등록한다(링크 없이).

## 기기 관리 (Windows PC)

```
python server\launchers\woopangm_pair.py list            등록된 기기 · 허용 기다리는 요청
python server\launchers\woopangm_pair.py approve 4821    코드로 허용 (효딩쓰 링크 대신)
python server\launchers\woopangm_pair.py deny 4821       거절 — 그 기기는 한 시간 동안 다시 요청하지 못한다
python server\launchers\woopangm_pair.py revoke a1b2c3   기기 해제 — 그 기기로 연 관리자 세션도 5초 안에 끝난다
```

기기 목록은 `server/logs/woopangm_devices.json`(열쇠·링크는 해시만). 서버를 다시 켜지 않아도 된다.

## 지키는 것 (보안)

- 열쇠를 주는 곳은 **https://woopang.com 의 `/admin/m…` 본 화면**뿐이다(`WoopangMNative` 가 frame·origin·경로를 본다). 다른 사이트·틀(iframe)은 못 받는다.
- 새 기기 요청은 `login_guard` 의 관리자 잠금(`admin` 범위)으로 센다 — 틀린 열쇠·링크가 쌓이면 그 IP 가 잠긴다. 허용 기다림은 최대 5건, 효딩쓰 알림은 10분에 3건까지(넘치면 한 번 경고).
- 응답은 `Cache-Control: no-store` · `Referrer-Policy: no-referrer` · `X-Robots-Tag: noindex`.
- 아이폰을 잃어버리면 `revoke` — 그 기기 열쇠와 그 기기로 연 세션이 바로 끝난다.

## 앱이 정하는 주소

| 주소 | 앱이 하는 일 |
|---|---|
| `https://woopang.com/…` | 앱 안에서 연다 (밀어서 뒤로 됨) |
| `/admin/login` (GET, `pw=1` 아님) | 연결 화면으로 바꿔 연다 — 세션이 끝났을 때 비밀번호 화면 대신 열쇠로 |
| `/` (GET) | 「로그아웃했어요」 연결 화면 — 관리자 웹의 로그아웃은 `/` 로 보낸다 |
| `target=_blank` 의 우팡 주소 (사진 크게 · 광고/DB 관리) | 아래에서 올라오는 창(같은 로그인). 아래로 밀거나 「닫기」 |
| 그 밖(카카오맵 · 전화 · 메일 등) | 사파리·해당 앱으로 |

## 파일

| 파일 | 하는 일 |
|---|---|
| `WoopangM/App/WoopangMApp.swift` | 앱 시작 · 위는 상태 표시줄을 비켜 앉아 머리 막대 색(#14171A)으로, 아래는 화면(CSS)이 비켜 앉는다 · 키보드는 비켜 감 |
| `WoopangM/Shell/WebShell.swift` | `Shell`(첫 주소 · 우팡 주소 판별 · 대신 정하는 주소) · WKWebView · 새 창 · 알림창 · 오프라인 · 웹 프로세스 죽으면 다시 · `WoopangMNative`(기기 열쇠 다리) |
| `WoopangM/Shell/DeviceKey.swift` | Keychain 기기 열쇠 만들기·읽기 · 허용 링크에 보일 기기 이름(모델·iOS 버전) |
| `WoopangM/Shell/ShellPages.swift` | 인터넷이 안 될 때 안내 화면 |
| `project.yml` | XcodeGen 설정(정본) — `.xcodeproj` 는 커밋하지 않는다 |
| `ExportOptions.plist` | TestFlight **내부 테스트 전용** 업로드 |
