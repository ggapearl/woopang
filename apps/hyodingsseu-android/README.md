# 효딩쓰 안드로이드

대표님 PC 의 데스크 비서 「효딩쓰」를 안드로이드 폰에서 쓰는 앱. **대표님 개인용 — Play 스토어에 올리지 않는다**(구독 약관).
서명한 APK 를 폰에 직접 깐다. 기능은 아이폰 앱(`apps/hyodingsseu-ios`, CHANGELOG 1.0.1)과 같다.

## 구조

```
안드로이드 앱(Capacitor 껍데기) ──띄움──▶ https://woopang.com/AI/desk-app/   ← 화면: 이 폴더의 web/
                                          (AI Office 8000 의 ai_office/routes.py desk_app 이 web/ 을 그대로 내준다)
화면(web/app.js) ──같은 출처 fetch──▶ https://woopang.com/AI/api/desk/<주소>
                                          (routes.py desk_proxy → PC 효딩쓰 127.0.0.1:47834 창 · 없으면 47835 뒤의 두뇌)
```

- **화면을 고치면 APK 를 다시 깔 필요가 없다.** `web/` 을 고치고 앱을 껐다 켜면(또는 대화 맨 위에서 아래로 당기면) 바로 바뀐다.
  `web/` 은 `Cache-Control: no-cache` 로 나가서 매번 바뀌었는지 확인한다.
- APK 를 다시 빌드해야 하는 경우: 폰 기능(`DeskNativePlugin.java`)·권한·아이콘·서버 주소(`capacitor.config.json`)를 바꿀 때만.
- 화면과 API 가 같은 출처(woopang.com)라 CORS·CapacitorHttp 가 필요 없다. 녹음(webm)·소희 목소리(m4a)도 보통 fetch 로 오간다.
- 브라우저로 열어도 돈다(`https://woopang.com/AI/desk-app/`) — 폰 목소리만 브라우저 음성으로 바뀐다.
- 서버 쪽 코드(`server/`)는 git 이 추적하지 않는 로컬 전용이다. 이 폴더는 추적한다.

| 파일 | 하는 일 |
|---|---|
| `web/app.js` | 아이폰 `DeskStore`(상태·긴 폴링·사건 처리) · `Audio`(녹음·읽기) · 각 화면을 그대로 옮긴 것 |
| `web/app.css` | PC 창(`ui.html`)·아이폰 `Palette` 와 같은 색 · 라이트/다크 · 기준 폭 422px(360px 까지 가로 넘침 없음) |
| `android/.../DeskNativePlugin.java` | 폰 목소리(TextToSpeech) · 진동 · 링크를 앱 밖에서 열기 · 기기 이름 · 상태바 색 |
| `www/offline.html` | 서버에 닿지 않을 때(인터넷 끊김·502) 앱 안에 들어 있는 안내 — 20초마다 다시 시도 |
| `capacitor.config.json` | `server.url` = woopang.com/AI/desk-app/ · `errorPath` = offline.html |

## 인증

PC 효딩쓰 창 ☰ → 휴대폰 앱 → 「연결 코드 받기」의 6자리를 넣으면 기기 토큰을 받는다(아이폰과 같은 `pair`).
토큰은 앱 웹뷰 저장소에만 있고, 앱은 **백업·새 폰 옮기기에서 빠진다**(`allowBackup=false` + `data_extraction_rules.xml`) —
아이폰 Keychain(ThisDeviceOnly)과 같은 뜻. 새 폰에서는 코드로 다시 짝 짓는다. PC 에는 해시만 남는다.

앱이 45초 넘게 안 보이면(백그라운드·잠금) 긴 폴링을 멈춘다 → PC 가 허락 카드와 답을 텔레그램으로도 보낸다(아이폰과 같음).

## 빌드 (이 PC)

필요: Node · JDK 17 · Android SDK (`C:\Users\pdnom\AppData\Local\Android\Sdk`) — 농민닷컴 앱과 같다.

```powershell
cd C:\woopang\apps\hyodingsseu-android
npm install                      # 처음 한 번
npx cap sync android             # www/ · capacitor.config.json 을 android 로
cd android
.\gradlew.bat assembleRelease    # → %USERPROFILE%\Desktop\woopang_build\hyodingsseu-<버전>.apk
```

- `android/local.properties`(git 무시): `sdk.dir=C:/Users/pdnom/AppData/Local/Android/Sdk`
- 버전은 `android/app/build.gradle` 의 `appVersion` 한 곳 (versionCode 는 1.0.1 → 10001 로 자동). APK 를 새로 줄 때만 올린다.
- AGP 8.7.3 · Gradle 8.11.1 (농민닷컴 앱과 같은 조합) · compileSdk 35 · **targetSdk 34**
  (스토어에 안 올리니 타깃 요구가 없다. 34 면 안드로이드 15+ 강제 엣지투엣지가 안 걸려 상태바·키보드가 예전대로 동작).

### 서명 키 — 저장소 밖

- `%USERPROFILE%\.android-signing\hyodingsseu\hyodingsseu-release.jks` + `signing.properties` (비밀번호는 이 파일에만)
- 농민닷컴 키와 따로다. `build.gradle` 이 이 경로를 읽는다(다른 곳에 두면 환경 변수 `HYODING_SIGNING` 에 properties 경로).
- **두 파일을 함께 백업할 것.** 잃으면 새 키로 서명한 APK 는 기존 앱 위에 덮어 깔리지 않는다 → 지우고 새로 깔고 다시 짝 지으면 되긴 한다(스토어 앱이 아니라 치명적이진 않다).
- 서명 인증서 SHA-256: `7f:b2:2d:e8:be:b7:1e:8b:85:2a:07:35:c9:29:30:da:ca:48:24:19:f4:58:b1:91:24:94:de:8e:5f:b4:98:ef`

## 폰에 설치

1. APK 를 폰으로 옮긴다 — 셋 중 편한 것
   - **USB**: 폰을 PC 에 꽂고 알림에서 「파일 전송」 → `내 PC › 폰 › Download` 에 APK 복사
   - **텔레그램**: PC 텔레그램 「저장한 메시지(나에게)」에 APK 파일을 보낸다 → 폰에서 그 파일을 누른다
   - **adb**(개발자 옵션 › USB 디버깅 켠 폰): `adb install -r hyodingsseu-1.0.1.apk`
2. 폰에서 APK 를 누른다 → 「출처를 알 수 없는 앱」 허용(내 파일·텔레그램 중 연 앱에 한 번) → 설치
   - Play 프로텍트가 「안전하지 않은 앱」이라 막으면: **세부정보 → 무시하고 설치** (스토어 밖 앱이라 뜨는 것)
3. 효딩쓰를 열고 PC 창 ☰ → 휴대폰 앱 → 「연결 코드 받기」의 6자리 입력
4. 처음 마이크를 누를 때 녹음 권한 허용

업데이트: 새 APK 를 같은 방법으로 깔면 연결·설정이 유지된다(같은 서명 키일 때).

## 시험 (빌드 세션이 할 것)

- 화면: Edge headless 로 422·390·375·360px — 흉내 낸 대화(스트리밍·도구·허락/선택 카드·들어온 말·오류)와 실제 PC 연결(읽기만).
  시험대 스크립트는 남기지 않았다 — 필요하면 `web/` 을 디스크에서 내주고 `/AI/api/desk/*` 를 woopang.com 으로 넘기는 작은 프록시로 연다
  (`location.hostname` 이 localhost 가 아니면 `DEFAULT_BASE` 가 같은 출처라 그대로 붙는다).
- 앱: 에뮬레이터에 debug APK 를 깔고 `chrome://inspect`(웹뷰 디버깅은 debug 빌드만)로 브리지·플러그인 확인.
- ⚠ 실제 대화에 시험 글을 보내지 말 것 — PC 효딩쓰와 **같은 대화**라 대표님 대화에 섞이고 구독 사용량을 쓴다.

## API

아이폰 앱과 같다 → `apps/hyodingsseu-ios/README.md` 의 API 표. 녹음은 `POST voice` 에 `audio/webm;codecs=opus`
(서버 `remote_api.py` 가 `.webm` 으로 받아 faster-whisper 로 받아적는다).
