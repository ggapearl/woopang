# 우기 iOS — 바뀐 것

빌드 요청마다 맨 위에 한 덩어리씩 적는다. 빌드 세션은 여기서 「이번에 확인할 것」을 본다.

## 1.0.0 (1) — 2026-11-15 · **첫 업로드** (올리는 법: `TESTFLIGHT.md` — 1절 처음 한 번 포함)

우기 아이폰 앱. 화면은 웹(`https://woopang.com/woogi/`), 앱은 「포장지」(README).
서버 쪽(우기 서버 6690 · nginx `/woogi/` · 입장 코드 · 휴대폰 가입 · 동네 · 사연 · 가게 · 모의 결제 · 기기 토큰 저장 `/api/device-token`)은
Windows 에서 만들고 시험했다(흐름 시험 297/0 · 화면 시험 197/0 · 실제 주소 시험 5/0). 결제는 지금 **모의 결제**(`sim` — 돈이 오가지 않는다).

**알림(서버 쪽, 2026-11-15 확인)**: 우기 서버가 팀 키로 애플에 직접 보낸다(`wg_push.py`, topic `com.que.woogi` — 효딩쓰와 같은 방식).
없는 기기 토큰으로 두 환경에 보내 봐서 **키 인증 통과**(운영 · 개발 키가 따로 — 서버가 환경마다 맞는 키를 골라 씀, 둘 다 `BadDeviceToken` 으로 답함)와
살아 있는 6690 의 발송 스레드(5초마다 → 애플 → 없는 기기라 토큰을 지움)를 확인했다. 번들 ID 가 아직 등록 전이라 `TopicDisallowed` 는
진짜 토큰이 생긴 뒤에야 알 수 있다 — 1절에서 `com.que.woogi` 를 등록하면 된다. 웹은 로그인하면 `pushRegister` 를 한 번 부르고
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
   - `WoogiNative.swift`: async `userContentController(_:didReceive:) -> (Any?, String?)` · `frameInfo.securityOrigin` · `frameInfo.request.url`
   - `WoogiApp.swift`: `.onOpenURL` · nonisolated `didReceive` 에서 `userInfo["link"] as? String`
   - **경고 0 인지** — 특히 `nearly matches optional requirement` 가 하나라도 있으면 그 함수는 불리지 않는다.
     바이너리에 셀렉터가 들어갔는지 확인: `webView:runJavaScriptAlertPanelWithMessage:initiatedByFrame:completionHandler:` ·
     `webView:runJavaScriptConfirmPanelWithMessage:initiatedByFrame:completionHandler:` · `webView:runJavaScriptTextInputPanelWithPrompt:defaultText:initiatedByFrame:completionHandler:` ·
     `webView:createWebViewWithConfiguration:forNavigationAction:windowFeatures:` · `webViewDidClose:` · `webView:decidePolicyForNavigationAction:decisionHandler:` ·
     `userContentController:didReceiveScriptMessage:replyHandler:` · `userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:` ·
     `application:didRegisterForRemoteNotificationsWithDeviceToken:` · `presentationAnchorForWebAuthenticationSession:`
2. **archive Info.plist**: `CFBundleDisplayName = 우기` · `UIUserInterfaceStyle = Light` · 세로만 · `ITSAppUsesNonExemptEncryption = NO` ·
   위치 · 카메라 · 사진 문구 3개 · **`CFBundleURLTypes` 에 `com.que.woogi`**(`Woogi/Info.plist` 가 합쳐졌는지) · `PrivacyInfo.xcprivacy` 가 .app 안에
3. 첫 업로드라 `TESTFLIGHT.md` 1절 — 번들 ID `com.que.woogi` 등록 + **Push Notifications** → **대표님께 앱 기록 만들기 부탁**(이름 `우기` · SKU `woogi`) → 그룹 `대표님` · 자동 배포
4. `TESTFLIGHT.md` 3-2 — export 의 `aps-environment = production` · 3-3 — `https://woopang.com/woogi/gate` 가 200 인지
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
