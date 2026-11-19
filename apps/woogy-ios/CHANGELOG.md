# 우기 iOS — 바뀐 것

빌드 요청마다 맨 위에 한 덩어리씩 적는다. 빌드 세션은 여기서 「이번에 확인할 것」을 본다.

## 1.0.0 (5) — 2026-11-19 · 카드를 아래로 밀면 바로 아이폰 공유 창 + 공유 화면의 「앱 열기」 (빌드 번호는 `project.yml` 에 5 로 올려 두었다 — 더 올리지 않는다) · **아직 빌드 전(코드만 준비)**

**공유 화면(웹)의 「앱 열기」를 눌러도 사파리에서 열리던 것** — 2026-11-19 대표님 「앱이 이미 설치된 경우에는 바로 앱으로 연결되어야 하는데 …」.
원인: 유니버설 링크는 사파리에서 **같은 도메인의 링크를 눌러도 앱이 안 열린다**(주소창에 치거나 다른 앱에서 눌러야 열림). 그래서 웹이 **전용 앱 주소 `com.que.woogy://c/<번호>`** 로 부른다.
- `WebShell.swift` — `Shell.shareLink(_:)` 가 `com.que.woogy://c/S…`(호스트 `c` · 번호 1개 · `S`+16진 10자리)도 받는다. 앱은 기존 `.onOpenURL` 그대로 `https://woopang.com/woogy/#share=<번호>` 로 연다.
- 웹(`card.html` · `site.js`): 아이폰이면 `com.que.woogy://c/<번호>` 를 열고 1.6초 안에 화면이 숨지 않으면(= 앱이 없음) 앱스토어(`WOOGY_IOS_STORE_URL`, 아직 비어 있으면 웹 화면)로 넘어간다.
- 옛 앱(4 이하)은 앱만 열리고 카드는 안 열린다(앱 주소가 `auth` 만 알아서) — 그래도 깨지지는 않는다.

**공유 화면의 「도와주기」 — 앱이 열리고 그 카드를 오른쪽으로 민 것처럼 바로 결제 창** — 2026-11-19 대표님 「도와주기 버튼을 누르면 … 결제하는 모드로 연결되어야 하는데 앱만 열리거든」.
- 웹이 `com.que.woogy://c/<번호>?help=1` 로 부른다. `WebShell.swift` 의 `Shell.shareLink(_:)` 가 `help=1` 을 읽어 `https://woopang.com/woogy/#share=<번호>&help=1` 로 넘긴다(「앱 열기」는 `help` 없이 그대로 카드만 맨 앞). 나머지는 웹 앱(`app.js`)이 하고 서버에 이미 올라가 있다.
- 새로 쓴 곳: `shareLink` 의 `help` 변수 · `URLComponents` 로 쿼리 읽기 2줄.
- 확인: 앱이 깔린 아이폰에서 공유 화면의 **「도와주기」 → 앱이 열리고 카드가 오른쪽으로 날아가며 「대신 결제하기」 창**이 바로 뜨는지. 이미 도왔거나 마감된 카드면 결제 창 대신 「이미 마감됐거나 도운 사연이에요」 안내.

**이번에 확인할 것 (추가)**:
- 앱이 깔린 아이폰에서 카톡으로 받은 링크 → 공유 화면 오른쪽 위 **「앱 열기」 → 앱이 열리고 그 카드가 맨 앞**. 앱을 지운 뒤 같은 단추 → 사파리 안내창 없이 웹 화면(또는 앱스토어)으로 가는지(앱이 없을 때 사파리가 「주소가 올바르지 않아」 안내창을 띄울 수 있음 — 뜨면 알려 달라).
- 카카오톡 인앱 브라우저에서 「앱 열기」가 앱을 여는지(카톡이 외부 앱 주소를 막으면 안 열린다 — 이때는 우측 상단 「Safari 로 열기」를 쓰게 된다).

**카드를 아래로 밀었다 놓으면 공유 창이 안 뜨고 「이웃에게 보낼까요?」 단추가 나오던 것** — 2026-11-19 대표님 「그냥 공유버튼을 눌렀을 때처럼 바로 디바이스의 공유 창이 실행되도록」.
원인: 웹의 `navigator.share` 는 「손으로 누른 동작」 안에서만 열리는데, 아이폰은 카드를 밀었다 놓은 동작을 손 동작으로 안 쳐서 거절한다(`NotAllowedError`). 웹만으로는 풀 수 없다.
고침: 앱이 공유 창(`UIActivityViewController`)을 직접 연다 — 손 동작이 필요 없다.
- `WoogyNative.swift` — `WoogyApp.call('share', {url, text?})` 추가(우기 주소만 · 끝까지 보냈는지 `ok` 로 답). `{probe: true}` 는 창 없이 「된다」만 알려 준다 — 웹이 앱 빌드가 새것인지 알아내는 데 쓴다.
- 웹(`server/static/app.js`)은 이미 고쳐서 서버에 올라가 있다 — 새 빌드면 `share` 로, 옛 빌드(4)면 지금처럼 웹 방식(+ 막히면 「공유하기」 단추)으로 돈다. 그래서 옛 앱과 새 웹이 같이 돌아도 안 깨진다.

**이번에 확인할 것**:
1. **컴파일 · 경고 0** — 새로 쓴 곳: `WoogyNative.swift` 의 `case "share"` · `topController`. Windows 에서 컴파일하지 못했다 — 오류가 나면 동작은 그대로 두고 형만 맞춘다.
2. 대표님 확인: 둘러보기에서 **카드를 아래로 밀었다 놓으면 날개가 파닥이고 0.5초 뒤 아이폰 공유 창이 바로 뜬다**(「이웃에게 보낼까요?」 단추 없음) → 카카오톡 · 문자로 끝까지 보내면 추천 숫자가 1 오르고, 창을 닫으면 오르지 않는다. 가운데 단추를 눌러도 같다. 아이패드 · 아래에서 올라오는 창이 떠 있는 상태에서도 공유 창이 그 위에 뜨는지.

**1.0.0 (5) 결과 (2026-11-19, Mac 빌드 세션)** — 업로드 성공, TestFlight 내부 테스트 `IN_BETA_TESTING`
- 버전 1.0.0 · 빌드 5 · 업로드 2026-11-19 18:42 KST · 앱 ID `6835099990` · 커밋 `4a483a2`
- `xcodegen generate` 다시 → 컴파일(서명 없이) `BUILD SUCCEEDED` · 경고 0(AppIntents 안내 1줄뿐) · `case "share"` · `topController` · `shareLink` 포함 코드 고친 곳 없음
- export entitlements: `applinks:woopang.com` · `aps-environment = production` 확인 · 그룹 `대표님` 에 들어감 · 수출 규정 질문 없음
- **TestFlight 앱에서는 설치하지 못한다**: 대표님 Apple ID 메일함이 가득 차 TestFlight 초대 메일을 받을 수 없고, 우기 · 우팡M 의 테스터가 `INVITED` 로 남아 있다(효딩쓰 · 우팡은 `INSTALLED`). 대표님 지시(11-19 「그냥 바로 모바일에 설치」)로 같은 빌드를 Ad Hoc 서명으로 대표님 아이폰(pdnom)에 케이블 설치했다(앱에서 1.0.0 (5) 확인).

## 1.0.0 (4) — 2026-11-17 · 기부 스캐너 카메라 허락이 매번 뜨던 것 (빌드 번호는 `project.yml` 에 4 로 올려 두었다 — 더 올리지 않는다)

**기부 스캐너(가맹점 모드)에 들어갈 때마다 카메라 허락을 또 묻던 것** — 2026-11-17 대표님 「승인을 해도 계속 기부스캐너 들어갈 때마다 권한요청」.
원인: 웹 화면이 카메라를 열 때(`getUserMedia`) WKWebView 가 앱에 「이 화면에 카메라를 줄까요」를 묻는데, 앱에 그 질문을 받는 코드가 없어 열 때마다 새로 물었다
(기기 카메라 허락은 이미 해 둔 상태에서도). 고침:
- `WebShell.swift` — `requestMediaCapturePermissionFor` 추가: **우기 화면(https://woopang.com)의 카메라만 바로 허락**, 마이크 등 그 밖은 그대로 묻는다.
  기기 허락(설정 › 우기 › 카메라)은 iOS 가 그대로 지킨다 — 설정에서 끄면 막힌다.
- `WoogyNative.swift` — `WoogyApp.call('cameraEnsure')`(아직 안 물었으면 그때 한 번 묻고, 정했으면 묻지 않고 결과만) ·
  `WoogyApp.call('openSettings')`(카메라를 껐을 때 설정으로 안내). 웹은 로그인 직후 한 번 `cameraEnsure` 를 불러 알림 허락과 같은 때 한 번에 끝낸다.
- 허락 창 문구(6개 언어 · `project.yml`)에 「가게에서 받는 분 QR 코드를 읽으려면」 추가.
웹(`server/static/app.js`)도 같이 고쳤다 — 앱 없이 사파리에서 쓸 때도 스캐너 화면을 오갈 때 카메라 스트림을 잠시 붙들어 다시 묻지 않게.

**이번에 확인할 것**:
1. **컴파일 · 경고 0** — 새로 쓴 곳: `WebShell.swift` 의 `requestMediaCapturePermissionFor`(이름표 `webView:requestMediaCapturePermissionForOrigin:initiatedByFrame:type:decisionHandler:`) ·
   `WoogyNative.swift` 의 `cameraEnsure` · `openSettings`(`import AVFoundation` 추가). Windows 에서 컴파일하지 못했다 — 오류가 나면 동작은 그대로 두고 형만 맞춘다.
2. 대표님이 설치한 뒤(대표님 확인): **앱을 처음 열고 로그인하면 카메라 허락이 알림 허락과 함께 한 번 뜬다 → 「허용」 → 가맹점 모드 › 기부 스캐너를 몇 번을 들락거려도 다시 묻지 않는지**
   (앱을 껐다 켜도 · 다른 탭에 갔다 와도). 설정에서 카메라를 끄면 안내 문구가 뜨고 「번호로 확인」이 되는지.
3. 이 빌드에는 (3) 의 공유 카드 링크(유니버설 링크)도 그대로 들어 있다 — (3) 확인 항목도 같이.

**결과 (2026-11-17, Mac 빌드 세션)** — 업로드 성공, TestFlight 내부 테스트 `IN_BETA_TESTING`
- 버전 1.0.0 · 빌드 4 · 업로드 2026-11-17 21:55 KST · 앱 ID `6835099990` · 커밋 `b18a321`
- `xcodegen generate` 다시 → 컴파일(서명 없이) `BUILD SUCCEEDED` · 경고 0(AppIntents 안내 1줄뿐) · `requestMediaCapturePermissionFor` · `cameraEnsure` · `openSettings` 포함 코드 고친 곳 없음
- archive Info.plist: `NSCameraUsageDescription` 에 「가게에서 받는 분 QR 코드를 읽으려면」 들어감
- export entitlements: `applinks:woopang.com`(Associated Domains) · `aps-environment = production` 둘 다 확인
- 그룹 `대표님` 에 들어감 · 수출 규정 질문 없음
- TestFlight 앱에 우기가 안 보이는 문제(테스터 `INVITED`)가 그대로라 같은 빌드를 Ad Hoc 서명으로 대표님 아이폰(pdnom)에 케이블 설치

## 1.0.0 (3) — 2026-11-17 · 공유 카드 링크를 앱으로 바로 열기 (빌드 번호는 `project.yml` 에 3 으로 올려 두었다 — 더 올리지 않는다)

**카톡 등에서 받은 기부카드 링크를 누르면, 앱이 깔려 있으면 앱이 바로 연다** — 2026-11-17 대표님 승인 「그렇게해」.
링크 `https://woopang.com/woogy/c/<번호>`(번호 = `S` + 16진 10자리) → 앱 화면 `https://woopang.com/woogy/#share=<번호>` → 받은 기부카드가 맨 앞
(웹 쪽 `#share=` 처리는 서버에 이미 반영 — 멀어도 · 패스했어도 맨 앞, 「공유받은 사연」 표시).
- 서버: `https://woopang.com/.well-known/apple-app-site-association` (Windows 가 2026-11-17 nginx 에 걸었다 — `application/json`, 넘김 없음,
  `appIDs: DDX8R79VU2.com.que.woogy` · `components: /woogy/c/*`)
- 앱: entitlements 에 `com.apple.developer.associated-domains = [applinks:woopang.com]`(`project.yml` · `Woogy.entitlements` 둘 다) ·
  `Shell.shareLink(_:)`(`WebShell.swift`, 새 함수) · `WoogyApp.swift` 의 `.onOpenURL` 이 ① 공유 링크 ② 간편로그인 복귀 ③ 그 밖의 우기 주소 순으로 처리
- 그 밖은 그대로(아이콘 · 허락 창 6개 언어 · 위치는 1.0.0 (2) 와 같다)

**이번에 확인할 것**:
1. **번들 ID `com.que.woogy` 에 Associated Domains** — `TESTFLIGHT.md` 1절 새 줄. 없으면 서명이 `associated-domains` 로 실패한다.
2. **컴파일 · 경고 0** — 새로 쓴 곳: `Shell.shareLink`(`("0"..."9").contains(_:)` · `("A"..."F").contains(_:)` 는 `ClosedRange<Character>`) · `.onOpenURL` 분기.
   Windows 에서 컴파일하지 못했다 — 오류가 나면 동작은 그대로 두고 형만 맞춘다.
3. **export entitlements** 에 `com.apple.developer.associated-domains` = `applinks:woopang.com` 이 남아 있는지(`aps-environment = production` 과 함께)
4. 서버 확인: `curl -sI https://woopang.com/.well-known/apple-app-site-association` → `200` · `Content-Type: application/json`
5. 대표님이 설치한 뒤(대표님 확인):
   - 메모 앱이나 문자에 공유 링크를 붙여 누르면 **사파리가 아니라 우기 앱**이 열리고 그 기부카드가 맨 앞에 뜨는지 (가장 확실한 확인.
     앱을 새로 깐 직후엔 애플이 확인 파일을 받아 가는 데 몇 분 걸릴 수 있다)
   - 카카오톡: 카톡은 링크를 **카톡 안 브라우저**로 여는 게 기본이라 거기서는 앱이 바로 안 열릴 수 있다 —
     그때 카톡 브라우저 메뉴의 「다른 브라우저로 열기」를 누르면 우기 앱이 열리는지
   - 앱이 꺼진 상태 · 켜진 상태 두 번 다

**결과 (2026-11-17, Mac 빌드 세션)** — 업로드 성공, TestFlight 내부 테스트 `IN_BETA_TESTING`
- 버전 1.0.0 · 빌드 3 · 업로드 2026-11-17 01:20 KST · 앱 ID `6835099990` · 커밋 `b818f2c`
- 번들 ID `com.que.woogy` 에 **Associated Domains** 켬(`ASSOCIATED_DOMAINS` 201) — 지금 켜진 것: 인앱결제 · Push · Associated Domains
- `xcodegen generate` 다시 → 컴파일(서명 없이) `BUILD SUCCEEDED` · 경고 0(AppIntents 안내 1줄뿐) · `Shell.shareLink` · `.onOpenURL` 포함 코드 고친 곳 없음
- export entitlements: `com.apple.developer.associated-domains = applinks:woopang.com` · `aps-environment = production` (둘 다 확인)
- 서버: `/.well-known/apple-app-site-association` → 200 · `application/json` · `woopang.com/woogy/gate` → 200
- 그룹 `대표님` 에 들어감 · 수출 규정 질문 없음
- TestFlight 앱에 우기·우팡M 이 안 보이는 문제가 그대로(테스터 상태 `INVITED`, 효딩쓰·우팡은 `INSTALLED`) → 같은 빌드를 Ad Hoc 서명으로 대표님 아이폰(pdnom)에 케이블 설치

## 1.0.0 (2) — 2026-11-16 · 광택 아이콘 · 허락 창 6개 언어 · 위치를 앱이 직접 묻기 (빌드 번호는 `project.yml` 에 2 로 올려 두었다 — 더 올리지 않는다)

**앱 아이콘을 원본 로고(광택)로** — 2026-11-16 대표님 「향후 빌드할 때 로고를 지금로고 사용하지 말고 원본로고(광택) 을 사용해서 빌드하도록」.
`Woogy/Assets.xcassets/AppIcon.appiconset/icon-1024.png` 를 `design/logo_gloss_original.png`(원본)로 다시 만들었다(`design/make_icon.py` —
테두리 안쪽만 잘라 1024 · RGB · 투명 없음). 평면 아이콘(1.0.0 (1)의 겨자색 바탕 · 평면 하트)으로 되돌리지 말 것. 코드 · 설정은 그대로.

**허락 창을 폰 언어로 (6개)** — 2026-11-16 대표님 「위치관리 동의할 때 영어로 발생하던데 각 나라 언어에 맞게 … 우팡앱이 이런 건 잘 정리되어 있거든」.
원인: `project.yml` 에 기본 언어가 없어 앱이 영어(en) 앱으로 잡혔다 → 한국어 아이폰에서도 허락 창의 제목 · 버튼이 영어.
고침: `developmentLanguage: ko` + `Woogy/<언어>.lproj/InfoPlist.strings`(우팡과 같은 ko · en · ja · zh-Hans · zh-Hant · es — 위치 · 카메라 · 사진 3줄).
위치 문구도 지금 쓰임새(동네 인증 · 근처 사연)에 맞게 고쳤다.

**위치를 앱이 직접 묻는다** — `WoogyApp.call('location')` → `Woogy/Shell/Locator.swift`(CoreLocation, 새 파일) · `WoogyNative.swift` 에 `case "location"`.
웹 위치(`navigator.geolocation`)를 쓰면 아이폰 허락 뒤에 「woopang.com 이(가) 현재 위치를…」 창이 한 번 더 뜨고, 앱을 켤 때마다 또 묻는다.
웹(서버 쪽, 이미 반영)은 다리가 있으면 이걸 쓰고, `ok:false` + `code`(1 허락 안 함 · 2 못 찾음 · 3 15초)면 그 안내를, 「모르는 기능」(1.0.0 (1))이면 웹 위치로 돌아간다.

**이번에 확인할 것**:
1. **컴파일 · 경고 0** — 새로 쓴 곳: `Locator.swift`(`@MainActor` 싱글턴 · `CheckedContinuation` 모아 두었다가 한 번에 답 · `Task.sleep(for: .seconds(15))` ·
   `CLLocationManagerDelegate` 는 `Relay`(NSObject)가 받아 숫자만 메인으로 넘김 · `locationManagerDidChangeAuthorization`) · `WoogyNative.swift` 의 `case "location"`.
   Windows 에서 컴파일하지 못했다 — 오류가 나면 동작은 그대로 두고 형만 맞춘다.
2. **archive**: 앱 아이콘이 광택 로고인지 · 업로드가 아이콘(알파 채널) 때문에 거절되지 않는지 · `.app` 안에 `ko.lproj` 등 6개 `InfoPlist.strings` ·
   Info.plist 의 `CFBundleDevelopmentRegion = ko`
3. 대표님이 설치한 뒤(대표님 확인):
   - 홈 화면 아이콘이 광택 로고로 바뀌었는지
   - 내 동네 정하기 › 「지금 위치 찾기」 → **아이폰 허락 창이 한국어로(제목 · 버튼 · 문구)** 한 번만 뜨는지 · 「woopang.com 이…」 창이 더 뜨지 않는지 ·
     앱을 껐다 켜고 다시 찾아도 묻지 않는지
   - (되면) 폰 언어를 English · 日本語 로 바꿨을 때 그 언어로 뜨는지
   - 사연 쓰기 › 사진 올리기를 눌렀을 때 뜨는 메뉴(사진 보관함 · 사진 찍기 · 파일 선택)가 **한국어**인지 — 2026-11-16 대표님
     「Take a photo -> 사진 찍기」. 같은 원인(앱이 영어 앱으로 잡힘)이라 이번 `developmentLanguage: ko` + `ko.lproj` 로 함께 바뀌어야 한다.
     그래도 영어면 `project.yml` 의 `CFBundleLocalizations`(ko 먼저)를 넣어 본다.

**결과 (2026-11-16, Mac 빌드 세션)** — 업로드 성공, TestFlight 내부 테스트 `IN_BETA_TESTING`
- 버전 1.0.0 · 빌드 2 · 업로드 2026-11-16 19:09 KST · 앱 ID `6835099990` · 커밋 `5343140`
- 컴파일(서명 없이) `BUILD SUCCEEDED` · 경고 0(AppIntents 안내 1줄뿐) · 코드 고친 곳 없음
- archive: 아이콘 `AppIcon` · 알파 채널 없음 · `ko`/`en`/`ja`/`zh-Hans`/`zh-Hant`/`es` `.lproj` 6개 · `CFBundleDevelopmentRegion = ko` · `PrivacyInfo.xcprivacy` 있음 · `ITSAppUsesNonExemptEncryption = NO`
- 그룹 `대표님`(내부 · 자동 배포) 에 들어감 · 3-3 `https://woopang.com/woogy/gate` → 200
- TestFlight 에서 앱이 보이지 않는 문제(1.0.0 (1) 때부터 우기 · 우팡M 둘 다)가 있어, 대표님 아이폰(pdnom)에 같은 빌드를 Ad Hoc 서명으로 케이블 설치도 했다(푸시는 production)

## 1.0.0 (1) — 2026-11-15 · **첫 업로드** (올리는 법: `TESTFLIGHT.md` — 1절 처음 한 번 포함)

> **영문 이름 woogi → woogy (2026-11-15 대표님 「영어 이름으로 사용할 걸 woogi 가 아닌 woogy 로」)** — 폴더 `apps/woogy-ios` · 타깃 `Woogy` ·
> 번들 ID **`com.que.woogy`** · SKU `woogy` · 주소 `https://woopang.com/woogy/` · 복귀 주소 `com.que.woogy://` · 웹 다리 `window.WoogyApp`(메시지 이름 `woogy`).
> 앞선 요청(커밋 `66b9026`, 번들 ID `com.que.woogi`)은 **취소** — 그 이름으로 이미 만든 것(번들 ID · 앱 기록 · 업로드)이 있으면 보고에 적는다.

우기 아이폰 앱. 화면은 웹(`https://woopang.com/woogy/`), 앱은 「포장지」(README).
서버 쪽(우기 서버 6690 · nginx `/woogy/` · 입장 코드 · 휴대폰 가입 · 동네 · 사연 · 가게 · 모의 결제 · 기기 토큰 저장 `/api/device-token`)은
Windows 에서 만들고 시험했다(흐름 시험 297/0 · 화면 시험 197/0 · 실제 주소 시험 5/0). 결제는 지금 **모의 결제**(`sim` — 돈이 오가지 않는다).

**알림(서버 쪽, 2026-11-15 확인)**: 우기 서버가 팀 키로 애플에 직접 보낸다(`wg_push.py`, topic `com.que.woogy` — 효딩쓰와 같은 방식).
없는 기기 토큰으로 두 환경에 보내 봐서 **키 인증 통과**(운영 · 개발 키가 따로 — 서버가 환경마다 맞는 키를 골라 씀, 둘 다 `BadDeviceToken` 으로 답함)와
살아 있는 6690 의 발송 스레드(5초마다 → 애플 → 없는 기기라 토큰을 지움)를 확인했다. 번들 ID 가 아직 등록 전이라 `TopicDisallowed` 는
진짜 토큰이 생긴 뒤에야 알 수 있다 — 1절에서 `com.que.woogy` 를 등록하면 된다. 웹은 로그인하면 `pushRegister` 를 한 번 부르고
토큰을 `platform: 'ios'` 로 올리며, 로그아웃 때 이 폰 토큰을 함께 보내 서버가 그 사람 것만 지운다(흐름 · 화면 시험에 넣음).

**이번에 확인할 것**:
1. **컴파일** — Windows 에서 컴파일하지 못했다. 효딩쓰 2.x(검증됨)의 `WebShell` · `Push` · `AppDelegate` · 다리와 같은 모양으로 썼고, 새로 쓴 곳:
   - `WebShell.swift`: 콜백 판 `runJavaScriptAlertPanel` / `ConfirmPanel` / `TextInputPanel` 에 **`@objc(…)` 이름표**를 붙였다 —
     이름이 어긋나 WebKit 이 조용히 안 부르는 일(효딩쓰 2.0.0 마이크)을 막으려고. 이름표 때문에 `conflicts with optional requirement` 같은
     **컴파일 오류**가 나면: 클로저 형을 SDK 가 선언한 그대로 맞춘다(예: `@escaping @MainActor @Sendable (Bool) -> Void`) — 이름표 · 본문은 그대로.
     · `Answer<T>`(답을 꼭 한 번 — deinit 에서 기본값) · `createWebViewWith` → `PopupController` · `webViewDidClose` · `Router`(`@MainActor` 싱글턴)
     · `decidePolicyFor` 안의 `Task { @MainActor in Router.shared.open(to) }` · `cfg.applicationNameForUserAgent` · `wv.isInspectable`(iOS 16.4+, 배포 17)
   - `Auth.swift`: `ASWebAuthenticationSession(url:callbackURLScheme:completionHandler:)`(17.4 에서 새 이름이 생겼지만 배포 17.0 이라 경고 없음) ·
     `withCheckedContinuation` · `ASWebAuthenticationSessionError.canceledLogin` · `Anchor: ASWebAuthenticationPresentationContextProviding`(nonisolated)
   - `WoogyNative.swift`: async `userContentController(_:didReceive:) -> (Any?, String?)` · `frameInfo.securityOrigin` · `frameInfo.request.url`
   - `WoogyApp.swift`: `.onOpenURL` · nonisolated `didReceive` 에서 `userInfo["link"] as? String`
   - **경고 0 인지** — 특히 `nearly matches optional requirement` 가 하나라도 있으면 그 함수는 불리지 않는다.
     바이너리에 셀렉터가 들어갔는지 확인: `webView:runJavaScriptAlertPanelWithMessage:initiatedByFrame:completionHandler:` ·
     `webView:runJavaScriptConfirmPanelWithMessage:initiatedByFrame:completionHandler:` · `webView:runJavaScriptTextInputPanelWithPrompt:defaultText:initiatedByFrame:completionHandler:` ·
     `webView:createWebViewWithConfiguration:forNavigationAction:windowFeatures:` · `webViewDidClose:` · `webView:decidePolicyForNavigationAction:decisionHandler:` ·
     `userContentController:didReceiveScriptMessage:replyHandler:` · `userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:` ·
     `application:didRegisterForRemoteNotificationsWithDeviceToken:` · `presentationAnchorForWebAuthenticationSession:`
2. **archive Info.plist**: `CFBundleDisplayName = 우기` · `UIUserInterfaceStyle = Light` · 세로만 · `ITSAppUsesNonExemptEncryption = NO` ·
   위치 · 카메라 · 사진 문구 3개 · **`CFBundleURLTypes` 에 `com.que.woogy`**(`Woogy/Info.plist` 가 합쳐졌는지) · `PrivacyInfo.xcprivacy` 가 .app 안에
3. 첫 업로드라 `TESTFLIGHT.md` 1절 — 번들 ID `com.que.woogy` 등록 + **Push Notifications** → **대표님께 앱 기록 만들기 부탁**(이름 `우기` · SKU `woogy`) → 그룹 `대표님` · 자동 배포
4. `TESTFLIGHT.md` 3-2 — export 의 `aps-environment = production` · 3-3 — `https://woopang.com/woogy/gate` 가 200 인지
5. 대표님이 설치한 뒤(대표님 확인):
   - 아이콘이 겨자색 바탕 초록 날개 하트 · 켜면 크림색 → **입장 코드** 한 번(그 뒤 180일은 안 묻는다) → 우기 화면
   - 휴대폰 번호 가입(번호가 `010-1234-5678` 모양으로 저절로 나뉘는지) · 내 동네 정하기 → 위치 허락이 뜨는지(아이폰 허락 + 「woopang.com 이 위치를…」 한 번 더 뜰 수 있다)
   - 로그인한 뒤 **알림 허락**을 한 번 묻는지 → 허락 → 설정 › 알림 에 「우기」가 보이는지 →
     시험 결제로 기부를 끝내면 몇 초 안에 「결제가 끝났어요 · 고마워요」 알림이 화면 위에 뜨는지 · 알림을 누르면 우기의 그 화면(내 활동)이 열리는지
     (알림은 그 폰에 지금 로그인한 계정 것만 온다 — 「시험 계정 바꾸기」로 바꾸면 그 계정 것으로 옮겨 간다)
   - 사연 · 프로필 사진 올리기 → 사진 보관함 · 카메라 고르기가 뜨는지
   - 「기부 취소」 · 「사연 내리기」 같은 확인 창이 **취소/확인 버튼과 함께** 뜨는지(안 뜨면 1번의 이름표 문제)
   - 약관 · 가맹점 가입 안내 → 아래에서 올라오는 창 · 「닫기」
   - 앱을 완전히 껐다 켜도 로그인이 남아 있는지

**결과 (2026-11-15, Mac 빌드 세션)** — 업로드 성공, TestFlight 내부 테스트 `IN_BETA_TESTING`
- 버전 1.0.0 · 빌드 1 · 업로드 2026-11-15 22:51 KST · App Store Connect 앱 ID `6835099990` (앱 이름 `우기 : 우리동네 기부천사`)
- 컴파일(서명 없이) `BUILD SUCCEEDED` · 경고 0(AppIntents 메타데이터 안내 1줄뿐) · 코드 고친 곳 없음 · `PrivacyInfo.xcprivacy` 가 .app 안에 있음
- 번들 ID `com.que.woogy` 등록 + Push Notifications 켬 → 대표님이 앱 기록 생성 → archive(자동 서명) → export `EXPORT SUCCEEDED`(업로드까지)
- 알림 서명: 업로드용 export 는 `DistributionSummary.plist` 를 남기지 않는다 → `xcdistributionlogs` 의 재서명 entitlements 에서 `aps-environment = production` 확인(archive 는 development)
- 그룹 `대표님`(내부 · 모든 빌드 자동 배포 · 공개 링크 끔) + 테스터 대표님 Apple ID(기존 betaTester 가 409 → 이 그룹에 새로 생성)
- 3-3 `https://woopang.com/woogy/gate` → **404** (Windows 가 서버 주소를 `/woogy` 로 옮기기 전 — 옮긴 뒤 200 이어야 한다. 참고: `/woogi/gate` 는 502)
- 앞선 요청(`com.que.woogi`)으로 한 일: 번들 ID `com.que.woogi`(+Push) 등록만 — 앱 기록·빌드·업로드 없음. 번들 ID 는 지우지 않고 그대로 둠
