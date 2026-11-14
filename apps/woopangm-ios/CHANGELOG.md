# 우팡M iOS — 바뀐 것

빌드 요청마다 맨 위에 한 덩어리씩 적는다. 빌드 세션은 여기서 「이번에 확인할 것」을 본다.

## 1.0.0 (1) — 2026-11-15 · **첫 업로드** (올리는 법: `TESTFLIGHT.md` — 1절 처음 한 번 포함)

대표님 전용 우팡 관리자 앱. 화면은 웹(`https://woopang.com/admin`), 앱은 「포장지」 — 기기 열쇠로 비밀번호 없이 들어간다(README).
서버 쪽(`/admin/m/` 연결 화면 · `POST /admin/m/session` · 효딩쓰 허용 링크 · 기기 관리 CLI)은 Windows 에서 만들고 시험했다(33/33 · 실제 브라우저 E2E).

**이번에 확인할 것**:
1. 컴파일 — Windows 에서 컴파일하지 못했다. 효딩쓰 2.x 의 `WebShell`·`DeskNative` 와 같은 모양으로 썼고, 그 밖에 새로 쓴 곳:
   - `WebShell.swift`: async `runJavaScriptAlertPanel`/`ConfirmPanel`/`TextInputPanel`(`withCheckedContinuation` + `UIAlertController`) ·
     `createWebViewWith` → `PopupController`(아래에서 올라오는 창) · `webViewDidClose` · `Shell.redirect(for:)`/`connect(next:out:)`(`URLComponents`)
   - `WoopangMNative`: `message.frameInfo.securityOrigin.protocol`/`.host` · `frameInfo.request.url?.path`
   - `DeviceKey.swift`: Keychain(`SecItemCopyMatching`/`SecItemAdd`, `kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly`) · `SecRandomCopyBytes` · `utsname` 기기 모델
   - **경고 0 인지** — 특히 `nearly matches optional requirement` 경고가 하나라도 있으면 그 함수는 WebKit 에 **불리지 않는다**(효딩쓰 2.0.0 마이크 함수처럼).
     async alert/confirm/prompt 세 함수가 불려야 알림 보내기 화면의 「전송 완료」 같은 alert 가 뜬다 → 경고가 나면 이름표만 SDK 에 맞추고(본문 그대로) 바이너리에 셀렉터가 들어갔는지 확인
2. archive Info.plist: `CFBundleDisplayName = woopangM` · `UIUserInterfaceStyle = Dark` · 세로만 · `ITSAppUsesNonExemptEncryption = NO` · 위치/카메라/사진 저장 문구 3개 · `PrivacyInfo.xcprivacy` 가 .app 안에
3. 첫 업로드라 `TESTFLIGHT.md` 1절 — 번들 ID `com.que.woopangm` 등록 → **대표님께 앱 기록 만들기 부탁**(이름 `woopangM` · SKU `woopangm`) → 그룹 `대표님`·자동 배포
4. `TESTFLIGHT.md` 3-2 — `https://woopang.com/admin/m/` 가 200 인지(404 면 Windows 서버 재시작 전 — 보고에 적기)
5. 대표님이 설치한 뒤: 아이콘이 검정 바탕 흰 강아지 · 켜면 오프닝 없이 바로 연결 화면 → 4자리 코드 → 효딩쓰 링크 [허용] → 3초 안에 관리자 화면 · 껐다 켜면 바로 관리자 화면
   · 알림 보내기 화면의 「사진 넣기」 → 사진 보관함·카메라 고르기가 뜨는지 (WKWebView 기본 `<input type=file>` — 앱 코드 없음, 카메라 문구는 Info.plist 에 있음)
