# 우기 iOS — TestFlight 로 대표님 아이폰에 올리기

> **읽는 사람**: 대표님 Mac 의 Claude 빌드 세션.
> **목표**: `apps/woogy-ios` 를 빌드해 **TestFlight 내부 테스트**로 올리고, 대표님이 아이폰의 TestFlight 앱에서 받아 「우기」를 직접 써 보게 한다.
> 효딩쓰(`apps/hyodingsseu-ios`) · 우팡M(`apps/woopangm-ios`)과 같은 「포장지」 방식 — 막히면 효딩쓰 `CHANGELOG.md` 의 「결과」 덩어리(1.0.3 · 2.0.0 · 2.0.1 · 2.2.0)에 이미 겪은 일이 적혀 있다.

## ⚠ 지킬 것

1. **이 앱(`Woogy`)만 빌드한다.** 우팡 Unity 앱 · 농민닷컴 앱은 대표님이 따로 요청할 때만.
2. **이번엔 내부 테스트만.** 테스터는 대표님 Apple ID(계정 소유자) 한 명. **외부 테스터 · 공개 링크 · 앱스토어 심사 제출은 대표님이 따로 요청할 때만**
   (`ExportOptions.plist` 는 나중을 위해 `testFlightInternalTestingOnly = false` — 외부로 넘길 수는 있게 해 두었을 뿐이다).
3. **이 폴더(`apps/woogy-ios/`)만** 고친다. 서버 주소 · API 모양은 바꾸지 않는다(서버는 Windows PC 에 있다 — 이 저장소에 없음).
4. 컴파일 오류는 **뜻을 바꾸지 않는 최소 수정**. 동작을 바꿔야 할 것 같으면 고치지 말고 보고.
5. `.xcodeproj` · 인증서 · 프로비저닝 프로파일 · API 키 · `build/` 는 커밋하지 않는다(`.gitignore`).
6. 빌드 번호(`CURRENT_PROJECT_VERSION`)는 올릴 때마다 1 씩 올린다 — 같은 번호는 App Store Connect 가 거절한다.

## 0. 필요한 것

- macOS · Xcode 26 이상(팀 `DDX8R79VU2`) · `brew install xcodegen`
- 효딩쓰 업로드에 쓰는 **App Store Connect API 키** 그대로 — export(2-4)에 `-authenticationKeyPath` · `-authenticationKeyID` · `-authenticationKeyIssuerID` 를 붙인다(없으면 `No Accounts`, 효딩쓰 2.2.0 결과)
- 대표님 아이폰에 **TestFlight 앱**

## 1. 처음 한 번 — 번들 ID · 알림 · 앱 기록 · 그룹

| 할 일 | 값 | 누가 |
|---|---|---|
| 번들 ID 등록 | `com.que.woogy` (이름 `Woogy`, 플랫폼 iOS) | 빌드 세션 — App Store Connect API `POST /v1/bundleIds`. 이미 있으면 건너뜀 |
| 알림 켜기 | 그 번들 ID 에 **Push Notifications** | 빌드 세션 — `POST /v1/bundleIdCapabilities` (`capabilityType: PUSH_NOTIFICATIONS`). 자동 서명 + `-allowProvisioningUpdates` 가 켜 주기도 한다(효딩쓰 2.0.1). **APNs 인증서(.p12)는 만들지 않는다** — PC 서버가 팀 키(.p8)로 보낸다 |
| 링크로 앱 열기 켜기 (1.0.0 (3)부터) | 그 번들 ID 에 **Associated Domains** | 빌드 세션 — `POST /v1/bundleIdCapabilities` (`capabilityType: ASSOCIATED_DOMAINS`). 자동 서명 + `-allowProvisioningUpdates` 가 켜 주기도 한다. 서버 쪽 확인 파일 `https://woopang.com/.well-known/apple-app-site-association` 은 Windows 가 이미 걸었다 |
| 앱 기록 만들기 | 플랫폼 iOS · 이름 `우기`(이미 있으면 `우기 - 우리동네 기부천사`, 그것도 있으면 `우기 QUE`) · 기본 언어 한국어 · 번들 ID `com.que.woogy` · SKU `woogy` · 사용자 액세스 전체 | **대표님(브라우저)** — API 로 만들 수 없다. 번들 ID 를 등록한 뒤 대표님께 이 줄을 그대로 보여 드리고 기다린다. 앱 기록 없이 2-4 를 돌리면 `missingApp` 으로 실패. 홈 화면 이름은 앱 기록 이름과 상관없이 `우기` |
| 내부 테스트 그룹 | 이름 `대표님` · 테스터 대표님 Apple ID · 자동 배포 켬 · 공개 링크 끔 | 빌드 세션(API) |

## 2. 매번 — 빌드해서 올리기

```bash
cd <woopang 저장소>
git fetch origin && git checkout <요청서에 적힌 커밋>   # 브랜치 wip/0930-redesign — 요청서의 커밋 그대로
cd apps/woogy-ios
xcodegen generate                     # project.yml → Woogy.xcodeproj

# 2-1) 컴파일 확인 (서명 없이)
xcodebuild -project Woogy.xcodeproj -scheme Woogy \
  -destination 'generic/platform=iOS' CODE_SIGNING_ALLOWED=NO build

# 2-2) 개인정보 매니페스트가 앱에 들어갔는지
find ~/Library/Developer/Xcode/DerivedData -path '*Woogy.app/PrivacyInfo.xcprivacy' | head -1

# 2-3) 아카이브 (자동 서명)
xcodebuild -project Woogy.xcodeproj -scheme Woogy -configuration Release \
  -destination 'generic/platform=iOS' -archivePath build/Woogy.xcarchive \
  -allowProvisioningUpdates archive

# 2-4) TestFlight 로 올리기 — 효딩쓰와 같은 API 키 옵션 3개를 붙인다
xcodebuild -exportArchive -archivePath build/Woogy.xcarchive \
  -exportOptionsPlist ExportOptions.plist -exportPath build/export -allowProvisioningUpdates \
  -authenticationKeyPath <키 .p8> -authenticationKeyID <키 ID> -authenticationKeyIssuerID <발급자 ID>
```

`** EXPORT SUCCEEDED **` 로 끝나면 업로드된 것이다.

## 3. 올린 뒤

1. 처리 확인(5~30분) · 수출 규정 질문 없음(`ITSAppUsesNonExemptEncryption = NO`) · 그룹 `대표님` 에 들어갔는지.
2. **알림 서명** — `build/export/DistributionSummary.plist` 의 entitlements 에 `aps-environment = production` 인지(archive 는 development, TestFlight 용 export 에서 production 으로 바뀐다. PC 는 production 부터 보낸다).
3. **서버가 켜져 있는지** — `curl -s -o /dev/null -w '%{http_code}\n' https://woopang.com/woogy/gate`
   - `200` = 준비됨(입장 코드 화면). 그 밖이면 Windows 우기 서버(6690)가 꺼진 것 → 업로드는 그대로 두고 **보고에 적는다**.
4. `CHANGELOG.md` 맨 위 덩어리에 결과를 적고 `git pull --rebase` → 커밋(`build(ios): 우기 1.0.0 (1) TestFlight 업로드`) → `git push`. **이 폴더의 파일만** 스테이징.
5. 90일 만료 기록은 **Windows 세션**이 한다(`ios_expiry.py record woogy "<버전 (빌드)>"`) — 보고에 업로드 날짜 · 빌드 번호를 꼭 적는다.

## 4. 막히면

| 증상 | 할 일 |
|---|---|
| `No profiles for 'com.que.woogy'` · 서명 오류 | 팀 `DDX8R79VU2` · 2-3 에 `-allowProvisioningUpdates` · 1절 번들 ID 등록 |
| `aps-environment` · `doesn't include the Push Notifications capability` | 1절 「알림 켜기」 뒤 2-3 부터 다시 |
| `associated-domains` · `doesn't include the Associated Domains capability` | 1절 「링크로 앱 열기 켜기」 뒤 2-3 부터 다시 |
| `Error Downloading App Information` (`missingApp`) | 1절 앱 기록(대표님 브라우저)이 아직 없다 |
| `No Accounts` (2-4) | API 키 옵션 3개를 붙였는지 |
| `The bundle version must be higher …` | `CURRENT_PROJECT_VERSION` 을 올리고 `xcodegen generate` 부터. 올린 번호는 커밋 |
| ITMS-91053 `Missing API declaration` | 메일에 적힌 API 분류를 `PrivacyInfo.xcprivacy` 에 사유와 함께 더한다 |
| 컴파일 오류 | 뜻을 바꾸지 않는 최소 수정 → 고친 파일 · 이유를 CHANGELOG 와 보고에 |
| 아이콘 거절(알파 채널) | `icon-1024.png` 는 RGB 1024×1024(투명 없음)로 확인해 두었다 — 다시 나면 보고 |

## 5. 끝나면 대표님께 보고

- 업로드 성공/실패 · 버전 · 빌드 번호 · 날짜 · 2-2 · 3-2 · 3-3 결과
- 고친 파일과 이유(있으면)
- 대표님이 해 주셔야 할 것: 1절 앱 기록 · TestFlight 에서 설치 · 처음 켜면 뜨는 **입장 코드**(PC 의 우기 서버 설정에 있다 — Windows 세션이 안내)
