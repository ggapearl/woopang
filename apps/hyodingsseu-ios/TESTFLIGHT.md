# 효딩쓰 iOS — TestFlight 로 대표님 아이폰에 올리기

> **읽는 사람**: 대표님 Mac 의 Claude 빌드 세션.
> **목표**: `apps/hyodingsseu-ios` 를 빌드해 **TestFlight 내부 테스트**로 올리고, 대표님이 아이폰의 TestFlight 앱에서 받아 쓰게 한다.
> 2026-09-28 대표님 결정: 「아이폰을 사용해야 해서 TestFlight 에서 다운받아서」.

## ⚠ 지킬 것

1. **내부 테스트만.** 테스터는 대표님 Apple ID 한 명. **외부 테스터·공개 링크·앱스토어 심사 제출 금지.**
   효딩쓰는 대표님 개인 Claude 구독으로 도는 도구라 남에게 나눠 주면 안 된다(구독 약관).
   `ExportOptions.plist` 의 `testFlightInternalTestingOnly` 가 켜져 있어 이 빌드는 외부로 넘길 수도 없다.
2. **이 폴더(`apps/hyodingsseu-ios/`)만** 고친다. 서버 주소·API 모양은 바꾸지 않는다(서버는 Windows PC 에서 고친다).
3. 컴파일 오류는 **뜻을 바꾸지 않는 최소 수정**. 동작을 바꿔야 할 것 같으면 고치지 말고 보고.
4. `.xcodeproj` · 인증서 · 프로비저닝 프로파일 · `build/` 는 커밋하지 않는다(`.gitignore`).
5. 빌드 번호(`CURRENT_PROJECT_VERSION`)는 올릴 때마다 1 씩 올린다 — 같은 번호는 App Store Connect 가 거절한다.

## 0. 필요한 것

- macOS · **Xcode 16 이상**(Settings › Accounts 에 대표님 Apple ID 로그인, 팀 `DDX8R79VU2`)
- Homebrew · `brew install xcodegen`
- App Store Connect 에 들어갈 수 있는 대표님 계정(계정 소유자 = Admin)
- 대표님 아이폰에 **TestFlight 앱**(App Store 에서 무료 설치)

## 1. 처음 한 번 — App Store Connect 에 앱 만들기

TestFlight 는 App Store Connect 의 앱 기록이 있어야 올라간다. (Xcode 가 번들 ID(App ID)는 알아서 만들지만 이 기록은 사람이 만든다.)

1. 먼저 아래 2절의 **2-3(archive)까지 한 번** 해서 번들 ID `com.que.hyodingsseu` 가 개발자 계정에 등록되게 한다
   (`-allowProvisioningUpdates` 가 App ID·프로파일을 만든다).
2. https://appstoreconnect.apple.com → **앱** → 왼쪽 위 **＋** → **신규 앱**
   - 플랫폼: iOS
   - 이름: `효딩쓰` (이미 쓰는 이름이라고 나오면 `효딩쓰 QUE`) — 앱스토어에 내지 않으므로 이름은 상관없다
   - 기본 언어: 한국어
   - 번들 ID: `com.que.hyodingsseu` 고르기
   - SKU: `hyodingsseu`
   - 사용자 액세스: 전체 액세스
3. 만들고 나면 **TestFlight** 탭 → **내부 테스트** 옆 ＋ → 그룹 이름 `대표님` → 테스터에 대표님 Apple ID(계정 소유자) 추가
   → 「자동 배포」 켜기(새 빌드가 처리되면 저절로 이 그룹에 들어간다).

Xcode Organizer 의 업로드 화면(Distribute App)이 **앱 기록 만들기를 제안하면 그걸로 해도 된다.**
그것도 안 되면 브라우저 일이라 — 빌드 세션이 직접 못 하면 **대표님께 위 순서를 그대로 보여 드리고** 기다린다.

## 앞으로 새 빌드를 올릴 때 (꾸준히 — 2026-09-28 대표님 결정: 개발 설치 말고 TestFlight 로만)

> 2.0 부터는 화면이 웹이라 **화면을 고쳤다고 빌드하지 않는다.** 빌드는 폰 기능(`DeskNative`)을 바꿨을 때와 90일 만료 전에만.

1. `project.yml` 의 `CURRENT_PROJECT_VERSION` 을 1 올린다(기능이 바뀌었으면 `MARKETING_VERSION` 도). `CHANGELOG.md` 맨 위에 새 덩어리.
2. 아래 2절 전체(xcodegen → 컴파일 확인 → archive → 업로드) → 3절(처리 확인·그룹) → 결과 기록·커밋·푸시.
3. 대표님 아이폰은 TestFlight 앱에서 「업데이트」만 누르면 된다. 1절은 다시 하지 않는다.

## 2. 매번 — 빌드해서 올리기

```bash
cd <woopang 저장소>
git pull
cd apps/hyodingsseu-ios
xcodegen generate                     # project.yml → Hyodingsseu.xcodeproj

# 2-1) 컴파일 확인 (서명 없이)
xcodebuild -project Hyodingsseu.xcodeproj -scheme Hyodingsseu \
  -destination 'generic/platform=iOS' CODE_SIGNING_ALLOWED=NO build

# 2-2) 개인정보 매니페스트가 앱에 들어가는지 (없으면 업로드가 거절될 수 있다)
#      빌드 결과 .app 안에 PrivacyInfo.xcprivacy 가 있어야 한다
find ~/Library/Developer/Xcode/DerivedData -path '*Hyodingsseu.app/PrivacyInfo.xcprivacy' | head -1

# 2-3) 아카이브 (서명 — 자동. App ID·배포 인증서·프로파일이 없으면 만든다)
xcodebuild -project Hyodingsseu.xcodeproj -scheme Hyodingsseu -configuration Release \
  -destination 'generic/platform=iOS' -archivePath build/Hyodingsseu.xcarchive \
  -allowProvisioningUpdates archive

# 2-4) TestFlight 로 올리기 (ExportOptions.plist: app-store-connect · upload · 내부 테스트 전용)
xcodebuild -exportArchive -archivePath build/Hyodingsseu.xcarchive \
  -exportOptionsPlist ExportOptions.plist -exportPath build/export -allowProvisioningUpdates
```

- 2-4 가 `** EXPORT SUCCEEDED **` 로 끝나면 업로드된 것이다.
- **GUI 로 해도 된다**: `open Hyodingsseu.xcodeproj` → 기기 `Any iOS Device` → Product › Archive → Organizer › Distribute App
  → **TestFlight Internal Only** → Distribute.

## 3. 올린 뒤

1. App Store Connect › 효딩쓰 › **TestFlight** — 빌드가 「처리 중」→ 5~30분 뒤 사용 가능.
   수출 규정 질문은 나오지 않는다(`ITSAppUsesNonExemptEncryption = NO`).
2. 내부 테스트 그룹 `대표님` 에 이 빌드가 들어갔는지 확인(자동 배포가 꺼져 있으면 ＋ 로 넣는다).
3. 대표님 아이폰: 메일 초대 수락 또는 **TestFlight 앱**을 열면 효딩쓰가 보인다 → **설치**.
   - 예전에 Xcode 로 깐 효딩쓰가 있으면 덮어쓴다. 연결(토큰)은 같은 팀 Keychain 이라 대개 그대로 남는다 — 끊겼으면 PC 창 ☰ → 휴대폰 앱 → 새 코드.
   - TestFlight 빌드는 **90일** 뒤 만료된다 → 그 전에 빌드 번호만 올려 다시 올린다.
4. `CHANGELOG.md` 맨 위 덩어리에 결과(업로드 성공·빌드 번호·설치 여부·이상한 점)를 적고
   `git pull --rebase` → 커밋(`build(ios): 2.0.0 (5) TestFlight 업로드` 처럼) → `git push`. **이 폴더의 파일만** 스테이징.

## 4. 막히면

| 증상 | 할 일 |
|---|---|
| `No profiles for 'com.que.hyodingsseu'` · 서명 오류 | Xcode › Settings › Accounts 에 로그인됐는지, 팀이 `DDX8R79VU2` 인지. 2-3 에 `-allowProvisioningUpdates` 가 있는지 |
| `No suitable application records were found` | 1절(App Store Connect 에 앱 만들기)을 안 한 것 |
| (2.0.1 부터) archive·export 가 `aps-environment` · `... doesn't include the Push Notifications capability` · `... doesn't support the Push Notifications capability` 로 실패 | App ID `com.que.hyodingsseu` 에 **Push Notifications** 를 켠다 — 개발자 사이트(Certificates, IDs & Profiles › Identifiers › `com.que.hyodingsseu` › Push Notifications 체크) 또는 App Store Connect API `POST /v1/bundleIdCapabilities`(`capabilityType: PUSH_NOTIFICATIONS`). 켠 뒤 2-3 부터 다시(`-allowProvisioningUpdates` 가 프로파일을 새로 만든다). APNs 인증서(.p12)는 만들지 않는다 — PC 는 팀 키(.p8)로 보낸다 |
| `The bundle version must be higher than the previously uploaded version` | `project.yml` 의 `CURRENT_PROJECT_VERSION` 을 올리고 `xcodegen generate` 부터 다시. 올린 번호는 커밋 |
| ITMS-91053 `Missing API declaration` 메일·거절 | 2-2 로 PrivacyInfo.xcprivacy 가 .app 에 들었는지. 메일에 적힌 API 분류를 `PrivacyInfo.xcprivacy` 에 사유와 함께 더한다 |
| 앱 이름이 이미 있다 | `효딩쓰 QUE` 처럼 바꿔 만든다(홈 화면 이름은 앱 안의 `효딩쓰` 그대로) |
| 컴파일 오류 | 뜻을 바꾸지 않는 최소 수정 → 고친 파일·이유를 CHANGELOG 와 보고에 |
| 아이콘 거절(알파 채널) | `icon-1024.png` 는 RGB(투명 없음)로 확인해 두었다 — 다시 나면 보고 |

## 5. 끝나면 대표님께 보고

- 업로드 성공/실패 · 버전·빌드 번호 · TestFlight 에서 설치했는지
- 고친 파일과 이유(있으면)
- 대표님이 해 주셔야 할 것(1절 브라우저 일, 초대 수락 등)
