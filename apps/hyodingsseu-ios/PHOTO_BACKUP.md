# 효딩쓰 iOS — 폰 사진 자동 백업 · 정리 (2.4.0)

대표님 2026-11-21: 「구글 포토처럼 폰 사진·영상을 알아서 백업하고, 올라간 건 ✓ 로 보이게. LTE 든 Wi-Fi 든 상관없이. 올라간 동영상·사진은 폰 사진첩에서 정리(삭제)할 수 있게.」
화면(웹 `2026-11-21a`)과 PC 서버(`server/voice/desk/photo_backup.py`)는 이미 배포됐다. 이 앱 업데이트(2.4.0)가 있어야 폰 안의 사진첩을 읽고 올린다. 옛 앱에서는 백업 카드가 「앱을 업데이트해 주세요」로 보인다.

## 어떤 파일이 무엇을 하나
| 파일 | 역할 |
|---|---|
| `Shell/PhotoBackup.swift` (새로 만듦) | 엔진 전부 — PhotoKit 읽기 · 조각 이어 올리기 · 기록(앱 안 JSON) · 백그라운드 작업 · 정리 |
| `Shell/DeskNative.swift` | 웹 → 앱 호출 9개(`backup…`) + 앱 → 웹 사건 `'backup'` (`emitData`) |
| `Shell/ShellPages.swift` | 웹 다리(bridgeJS)에 같은 이름 9개 · 앱이 돌려준 오류 글을 `Error` 로 감싸 화면의 `e.message` 가 읽게 함 |
| `App/HyodingsseuApp.swift` | `didFinishLaunching` 에서 `PhotoBackup.registerBackgroundTask()` (시작이 끝나기 전에 등록해야 한다) |
| `Shell/WebShell.swift` | 앱이 앞으로 나올 때 `appBecameActive()` · 두 손가락 확대 끔(`pinchGestureRecognizer`) |
| `Hyodingsseu/Info.plist` | `UIBackgroundModes = processing` · `BGTaskSchedulerPermittedIdentifiers = com.que.hyodingsseu.photobackup` |
| `project.yml` | 2.4.0 (11) · `NSPhotoLibraryUsageDescription`(새로 읽기 허락 문구) · 쓰기 문구에서 「읽지는 않습니다」 삭제 |

## 웹 ↔ 앱 약속 (`app.js` 「Native.backup…」 주석과 같다)
- `backupStatus()` → `{enabled, since, auth: 'granted'|'limited'|'denied'|'undetermined', done, pending, uploading: {lid,name,sent,total}|null, wifiOnly: false, error}`
- `backupConfigure({base, token})` — 서버 주소와 기기 열쇠를 앱이 기억한다(웹이 열 때마다 넘긴다). 주소·열쇠·켠 시각·올린 사진 번호 기록은 앱 지원 폴더의 `PhotoBackup/state.json` 하나에 둔다(앱 샌드박스 안 — 백그라운드에서 깨어나도 같은 값).
- `backupEnable({base, token})` → 사진첩 읽기 허락을 묻고, 허락되면 **켠 시각(`since`)** 을 찍는다. 그 뒤에 만들어진(`creationDate >= since`) 사진·영상만 올린다. 이미 폰에 있던 옛 사진은 올리지 않는다(대표님 「기존 사진은 무시」). 다시 켜면 `since` 가 새로 찍힌다.
- `backupDisable()` · `backupRun()`(지금 올리기) · `backupList({before, limit})`(올릴 대상·올린 것 목록) · `backupThumb({lid, size})`(작은 그림, base64) · `backupCleanupPlan()`(지울 수 있는 장수) · `backupCleanup({scope: 'video'|'all'})`.
- 사건 `'backup'` — 데이터는 `backupStatus()` 와 같다(올리는 중 · 장수 변화 · 오류).
- `wifiOnly` 는 없다 — 늘 LTE·Wi-Fi 구분 없이 올린다(요금제 무제한).

## 올리는 방식
1. `PHAsset` 을 `since` 이후, 최신 것부터 훑어 `lid`(= `PHAsset.localIdentifier`)로 서버에 `photos/up/check` 로 이미 올렸는지 묻는다.
2. 원본 파일(`PHAssetResource`)을 앱 임시 폴더로 꺼내(iCloud 에만 있으면 받아 온다) 6MB 조각으로 `POST photos/up?lid&off&total&name&ts&mod&kind` — 어긋나면 서버가 409 `{have}` 로 알려 주니 거기서 이어 올린다. 끝나면 서버가 Immich 에 넣는다.
3. 올린 `lid` 는 앱 안 기록에 남긴다. 사진첩이 바뀌면(`PHPhotoLibraryChangeObserver`) 다시 훑는다.
4. 동시에 하나씩만 올린다(폰 배터리·발열). 네트워크가 끊기면 다음 기회에 이어서 한다.

## 정리(삭제)
- 「동영상 정리」 = 올라간 동영상만 · 「전체 정리」 = 올라간 사진·동영상 전부.
- 지울 후보의 `lid` 를 **서버에 다시 묻는다**(`photos/up/verify`, 150개씩): 서버에 있고(휴지통·오프라인 아님) **크기가 같은 것만** 지운다. 앱 안 기록만 믿고 지우지 않는다.
- 실제 삭제는 `PHAssetChangeRequest.deleteAssets` — **iOS 가 자기 확인 창을 띄운다**(앱이 건너뛸 수 없다). 지워진 것은 사진 앱 「최근 삭제된 항목」에 30일 남는다. iCloud 사진을 쓰면 다른 기기에서도 같이 지워진다.

## iOS 한계 (정직하게)
- **앱이 꺼져 있으면 계속 올릴 수 없다.** 앱을 열었을 때 · 앱이 화면에 있는 동안 · 사진첩이 바뀔 때(앞에 있으면) · 시스템이 `BGProcessingTask` 로 주는 시간(보통 밤에 충전 중 · 시각은 iOS 가 정한다)에 올린다. 구글 포토처럼 「찍자마자 항상」은 안 된다.
- 앱을 강제 종료(위로 밀어 닫기)하면 iOS 가 백그라운드 작업을 막는다.
- 큰 영상은 조각 이어 올리기라 중간에 끊겨도 이어진다. 폰 저장 공간이 부족하고 iCloud 에만 있는 영상은 받아 오는 데 오래 걸릴 수 있다.

## 실기기에서 확인할 것 (TestFlight 2.4.0 (11))
1. 사진첩(📷) 칩 줄에 백업 카드가 뜨고 「켜기」 → iOS 사진 읽기 허락 창 → 「모든 사진」 허용.
2. 켠 뒤 사진 한 장 찍고 앱 열기 → 몇 초 안에 「올라감」 · 사진첩 격자 오른쪽 아래 ✓.
3. 영상(30초 이상) 한 개 찍고 올라가는지 · 올리는 중 앱을 홈으로 보냈다 다시 열어 이어지는지.
4. LTE(와이파이 끄고)에서도 올라가는지.
5. 「정리」 → 「동영상 정리」: 올라간 영상만 후보에 나오는지 · iOS 확인 창 · 사진 앱 「최근 삭제된 항목」에 들어가는지. 켜기 전에 있던 옛 사진·영상은 후보에 **없어야** 한다.
6. 사진첩 화면에서 두 손가락으로 벌려도 화면이 커지지 않는지(사진 보기 화면의 확대도 같이 막힌다 — 필요하면 거기만 따로 만든다).
7. 껐다 켜기 · 설정에서 사진 권한을 「없음」으로 바꿨을 때 안내 문구가 뜨는지.

## 컴파일 안내
Windows 에서 쓰고 **컴파일은 못 해 봤다**. Mac 에서 `xcodegen generate` → 빌드. 오류는 `PhotoBackup.swift` 안에서 날 가능성이 가장 크다(동시성 경고 · `Sendable`). 고치는 건 그 파일만으로 끝나게 만들었다. `PhotoBackup.shared.start()` 는 `DeskNative.init` 에서 불린다.
