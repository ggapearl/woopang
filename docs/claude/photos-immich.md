# 사진 📷 (구글 포토 → Immich → 효딩쓰 앱) — 운영 메모

> 최종 갱신: 2026-11-27
> 2026-11-20 대표님: 구글 포토 Takeout 사진을 구글 포토처럼 보고 싶다. 밖에 공개하지 않고, **효딩쓰 휴대폰 앱 오른쪽 위 📷 로만**.
> 얼굴 인식은 하지 않는다(대표님 지시). 사람 등록 대신 사진첩(앨범)으로 묶는다.

## 구성
```
E:\구글포토다운로드\takeout-*.zip   ← 원본 zip (대표님 허락 없이 지우지 않는다 — 아래 「압축 풀기」)
E:\구글포토다운로드\사진\            ← 풀린 사진·영상 (Immich 가 읽기 전용으로 붙임 — 복사 없음)
E:\immich\                         ← docker-compose.yml · .env · immich-api.env(키, 커밋 금지) · library(썸네일) · tools\
Docker Desktop(WSL2): immich_server(2283, 내부 전용) · immich_postgres · immich_redis   (머신러닝 없음·영상 변환 없음)
```
- 사진 위치는 **그 자리 그대로** 읽는다(External Library `/mnt/photos`). 다른 곳으로 복사하지 않는다 — 디스크가 SMR HDD(약 40MB/s)라 복사하면 며칠이 걸린다.
- 시크릿은 `E:\immich\.env` · `E:\immich\immich-api.env` · `C:\woopang\server\.env`(`IMMICH_URL`·`IMMICH_API_KEY`) 에만. 문서·코드·커밋에 쓰지 않는다.
- Immich 웹(2283)은 밖에 열지 않는다. 휴대폰은 효딩쓰를 통해서만 본다.

## 길 (휴대폰 → 사진)
| 무엇 | 길 | 인증 |
|---|---|---|
| 목록·장소·사진첩·폴더·사진첩에 담기 | 앱 → `/AI/api/desk/photos…` → ai_office `_DESK_API` → PC 효딩쓰 창구(47834/47835) → `server/voice/desk/photos.py` → Immich | 기기 열쇠(맥북 열쇠는 막힘) |
| 사진·영상 내용 | 앱 → `/AI/photo/<id>/<thumb\|preview\|orig\|video>?e=&s=` → ai_office `desk_photo` → Immich | 서명 링크(HMAC, 7일, `DESK_DOC_LINK_KEY`) · Range 지원 · `?dl=1&n=` 은 내려받기 |
- 서명은 `photos.sign()`(종류·만료까지 묶임) — 썸네일 서명으로 원본은 못 연다. 시험 결과: 서명 틀림 403 · 종류 바꿔치기 403 · 만료 410 · 이상한 id 404 · Range 206.
- 화면: `apps/hyodingsseu-android/web/app.js` 의 `openPhotos()` · `app.css` 의 「사진 (2026-11-20)」 블록. 제목줄 📷(`.bar .photos`) + 서랍 「사진」.
- 읽기·앨범 담기만 한다. 사진 삭제·수정 기능은 없다(원본 폴더는 읽기 전용 마운트).
- 고친 뒤 반영: `ai_office/routes.py` 를 고치면 8000 리스너 PID 만 죽여 감시기가 살리게 하고, `photos.py`·`remote_api.py` 는 「효딩쓰 다시 켜기.bat」(CLAUDE.md 2.0.3), 화면은 `WEB_VERSION` 올리고 `deploy_hyodingsseu_web.py`.

## 날짜·위치 (Takeout JSON → Immich)
Takeout 은 날짜·위치를 사진 옆 `*.supplemental-metadata.json` 에 둔다. `E:\immich\tools\takeout_extract.py`:
- `meta <폴더>` — JSON 을 읽어 파일 수정시각을 찍은 날짜로 맞추고, 위치는 `<파일>.xmp` 로 옆에 만든다(Immich 가 읽음). 이름 잘림·`(1)` 중복·`-edited` 변형을 맞춘다. 28개 zip 이름 목록으로 짝 맞추기 시뮬레이션 완료: 43,162개 중 38,435개 짝(나머지는 주로 아직 덜 받은 zip 2개).
- `extract` / `run` — zip 풀기(한 번에 하나씩 이어하기용). 일괄은 아래 `extract_all.py`.
- 옛 사진이 「11월 14~16일」로 뜨는 까닭: EXIF 가 없고 짝 JSON 도 아직 안 풀려(부분 압축 해제) Immich 가 파일 수정시각(= 풀린 날)을 쓴 것. JSON 을 풀어 `meta` 로 mtime 을 맞춘 뒤 스캔하면 바로잡힌다(`run`/`extract_all.py` 는 zip 하나마다 이 순서로 한다).

## 압축 풀기 (2026-11-21 대표님 「이제 다 다운받았으니까 다 압축풀어서 정리해줘」 — 풀기 금지는 풀렸다)
`E:\immich\tools\extract_all.py` (이어하기 · zip 은 기본 읽기만 — **2026-11-22 대표님 「압축파일 다 풀면 지우면서 해야될 걸? 안그러면 용량 부족할텐데」 로 `E:\immich\logs\DELETE_AFTER` 파일이 있을 때만 확인 끝난 zip 을 지운다**, 아래). 숨김 창으로 `C:\Python312\python.exe -I E:\immich\tools\extract_all.py`.
- **zip 지우기(`drop_zip`)**: 「확인 끝」(항목 전부 제 자리에 같은 크기 + 쓸 때 CRC 확인)이 난 zip 만, 지우기 직전에 한 번 더(모든 항목이 풀린 자리 또는 `충돌\<zip>\` 에 같은 크기로 있는지, 예전부터 있던 파일은 CRC 까지) 보고 지운다. 열리지 않는 zip(7-001)·확인 실패·빠진 항목이 있는 zip 은 절대 안 지운다. 상태에 `deleted` 시각이 남는다. `extract_all.py purge [dry]` 는 이미 확인 끝난 zip 을 같은 기준으로 점검해 지운다(⚠ 풀기와 **동시에 돌리지 말 것** — 둘 다 state 파일을 통째로 쓴다).
- 상태·로그: `E:\immich\logs\extract_state.json`(zip 별 확인 끝났는지) · `extract_all.log`. `status` · `audit` 하위 명령. 멈추기: `E:\immich\logs\STOP` 파일.
- 파일마다 `.part` 로 풀고 CRC·크기를 맞춘 뒤 이름을 바꾼다. 이름이 겹쳐 크기가 다르면 `E:\구글포토다운로드\충돌\<zip>\` 에 따로 둔다. 열리지 않는 zip(받다 만 것)은 건너뛰고 상태에 기록.
- 디스크 여유가 (풀 양 + 예비 80GB) 보다 모자라면 **그 zip 앞에서 멈춘다**. 11-22 부터는 확인 끝난 zip 을 하나씩 지우므로 여유가 거의 줄지 않는다(zip 하나 풀면 ≈ 그만큼 zip 이 사라짐). `MAX_ZIPS` 파일(숫자)이 있으면 확인 끝난 zip 이 그 개수가 되는 즉시 zip 사이에서 멈춘다 — 전부 풀 땐 지운다.
- 2026-11-21 조사(`zip_survey`): zip 46개 2,109.9GB(압축이 거의 없어 풀어도 같은 크기) · 항목 131,458개(사진·영상 68,883 · JSON 62,537) · 같은 내용 복사본(앨범 폴더) 1,019개 3.9GB. E: 여유 1,447GB 라 29번째(6-002)까지만 들어간다 → 약 730GB 부족, 대표님 결정 대기.
- `takeout-…-7-001.zip` 은 47.2GB 에서 끊긴 받다 만 파일(다른 것은 ~49.6GB, 끝 목록 없음 → 열리지 않음). 다시 받아야 한다 — Takeout 링크는 보통 7일(11-15 23:52 만들어짐 → 11-22 밤). 4-005 가 49.40GB 로 거의 꽉 차 있어 4-006 이 빠졌을 수도 있다(Takeout 화면에서 4번 묶음 파일 수 확인).
- 다 풀리면: `extract_all.py audit`(사진 없는 JSON = 빠진 zip 가늠 · JSON 없는 사진) → 전체 `meta` → Immich 스캔 → 처음 들어간 사진(날짜가 풀린 날로 찍힌 것)은 메타데이터 다시 읽기(`PUT /api/jobs/metadataExtraction` `{command:'start', force:true}` — 버전에 따라 경로가 다르면 `GET /api/jobs` 로 확인).
- 앨범 폴더 복사본은 같은 사진이 타임라인에 두 번 나온다(Immich External Library 는 경로로 구분). 필요하면 나중에 크기·CRC 로 걸러낸다.

## 폰 사진 자동 백업 · 정리 (2026-11-21, 구글 포토처럼)
화면(웹 `2026-11-21a`)·서버(`server/voice/desk/photo_backup.py`, 비공개 저장소)는 끝났고 시험도 했다. **아이폰 안(PhotoKit)은 앱 업데이트가 필요하다** — `apps/hyodingsseu-ios/PHOTO_BACKUP.md` 참고.
- **iOS 앱 2.4.0 (13)**: 코드는 완성(브랜치 `ios/photo-backup` · 커밋 `9a4c103` — (12) 는 `7f0b3a8`, 11-27 에 첫 기준 시각을 바꾸며 번호를 13 으로 올림. 컴파일은 Mac 빌드 세션에서 처음 해 본다). 요청서는 `C:\nongmin-app\IOS_BUILD_HANDOFF.md` 의 「🔔 지금 요청 — 효딩쓰 iOS 2.4.0 (13)」(`nongmin-app` 커밋 `57928de` — (12) 요청을 대신한다는 문구 포함). Mac 은 `C:\` 를 못 읽으므로 효딩쓰 `mac_send`(우편함 `C:\que-desk\mac\`)로 「iOS빌드」 세션에 말을 전한다. 업로드 성공이 확인되면 `python -X utf8 -I server\launchers\ios_expiry.py record hyodingsseu "2.4.0 (13)"` 로 90일 만료 기록. 웹은 `2026-11-27a`.
- **(11-27 확인) 폰에서 서버로 올라온 적이 아직 없다** — `E:\immich\phone_backup.db` 가 없다(첫 업로드 때 만들어진다). `origin/ios/photo-backup` 에 Mac 빌드 결과 커밋도 없다. **원인: Mac 이 요청을 받은 적이 없다** — 효딩쓰가 02:26 에 보낸 (12) 요청은 「맥북 Claude 계정이 사용 한도 중(03:26 에 풀림)」이라 `results.jsonl` 에 `ok:false` 로 끝났다(`mac_send` 는 한도 중이면 다시 보내 주지 않는다 — 풀린 뒤 다시 보내야 한다). 그래서 (13) 요청은 03:28 에 같은 우편함(`desk/macq.py`)으로 다시 보내도록 걸었다. 폰에 2.3.1 이하가 깔려 있으면 오늘 찍은 사진도, 격자 ✓ 도 안 보이는 게 정상이다. 코드 점검: 원본은 임시 파일로 꺼내(`writeData`) 5MB 씩만 읽어 보내고(메모리 5MB 안팎) 보낸 뒤 임시 파일을 지운다 · `allowsCellularAccess = true`(LTE·5G 가능) · 올린 기록은 폰 `PhotoBackup/state.json` + 서버 `phone_backup.db`, 앱을 다시 깔면 `photos/up/check` 로 복구.
- ⚠ **새로 올린 사진이 목록에 뜨는 시간 = 정보 읽기 대기열 길이**: 목록은 `search/metadata`(`withExif`)라 정보 읽기가 끝나야 나온다. 11-27 02시 대기 약 3.5만 건이 시간당 5천~1만 건씩 줄고 있어(수 시간) 그동안 올린 사진은 늦게 뜬다. 대기열이 비면 몇 초.
- **올리는 쪽은 네이티브 앱**이다(웹뷰는 폰 사진첩을 못 읽는다). 웹은 `backupConfigure({base, token})` 로 주소·열쇠만 넘기고, 화면(백업 카드·✓·정리)만 그린다. 네이티브 계약은 `app.js` 의 「Native.backup…」 주석에 있다(`backupStatus/Enable/Disable/Run/Configure/List/Thumb`, `backupCleanupPlan/backupCleanup`, 이벤트 `'backup'`).
- 서버: `POST photos/up?lid&off&total&name&ts&mod&kind` — 6MB 조각 이어 올리기(어긋나면 409 `{have}`) → `E:\immich\incoming` 에 모아 끝나면 Immich 업로드(`/data/upload`, libraryId 없음) · `photos/up/check`(이미 올린 것) · `photos/up/state`(올린 장수) · `photos/up/verify`(정리 전 재확인). 기록 `E:\immich\phone_backup.db`(sqlite).
- **기준 시각(`since`) 이후의 사진·영상을 올리고, 그 뒤에 찍는 것도 계속 올린다.** 처음 켜면 기준은 **2026-11-27 0시**(대표님 11-27 「오늘 0시부터, 내일 사진도 계속」 — 11-21 「기존 사진은 무시」를 이 날짜로 바꿈), 껐다 다시 켜면 그때 시각(꺼 둔 동안 찍은 건 안 올림). LTE·Wi-Fi 구분 없음(Wi-Fi 전용 스위치 삭제 — 요금제 무제한).
- ✓(오른쪽 아래)는 **폰에서 올린 것**(Immich 에서 `libraryId` 가 없는 것 — 3.x 는 `deviceId` 를 돌려주지 않는다)에만 붙는다. 구글 포토에서 가져온 옛 사진엔 없다.
- **정리**: 사진첩 칩 줄 맨 끝 「정리」(폰 앱에서만) → 「동영상 정리」 / 「전체 정리」. 네이티브가 지울 후보를 `photos/up/verify` 로 서버에 다시 묻고(≤150개씩), **서버에 있고(휴지통·오프라인 아님) 크기가 맞는 것만** 폰에서 지운다. 폰 안 삭제는 iOS 가 자기 확인 창을 띄우고, 지워진 것은 「최근 삭제된 항목」에 30일 남는다. iCloud 사진을 쓰면 다른 기기에서도 같이 지워진다.
- iOS 는 앱이 꺼져 있으면 계속 올릴 수 없다 — 앱을 열었을 때·사진첩이 바뀔 때·시스템이 주는 백그라운드 시간에 올린다.
- 사진첩(앨범) 칩은 `PH_ALBUMS=false` 로 숨겼다. 핀치 확대는 막았다(`viewport user-scalable=no`·gesture 차단) — 사진 보기 화면의 확대도 같이 막힌 것이니 필요하면 거기만 확대를 따로 만든다.
- 스크롤 연월 라벨·빠른 스크롤 막대: `GET photos/timeline`(달별 장수) + `before`(이 날 전). 
- **시험 방법**: Selenium + Edge(headless)로 진짜 Immich 에 붙은 임시 하네스(`photo_harness2.py`, 임시 DB·inbox)와 가짜 네이티브 다리(`bk_mock_native.js`)를 쓴다. 시험으로 만든 자산은 `/data/upload/` 아래 시험 이름인 것만 지운다(구글 포토 사진은 절대 건드리지 않는다).

## 사진 고르기 (2026-11-22 대표님 구글 포토 화면 보여 주시며 「이런 식으로 고르게」, 웹 `2026-11-22a`)
- **줄 맞춤**: `.ph-grid.jr` — 줄마다 높이는 같고 사진마다 폭이 다르다(서버가 주는 `w`/`h` 로 비율 `--ar`, 0.56~2 로 제한 · 없으면 정사각형). 순수 CSS flex(`flex-grow` ∝ 비율). 폰 백업 목록(`openPhoneBackup`)은 옛 4열 격자 그대로.
- **날짜 동그라미**(`.ph-dchk`): 누르면 그날 전부 고르고, 다시 누르면 전부 푼다. 일부만 골랐으면 반쯤(가운데 줄), 전부면 파란 체크. 그날이 목록 맨 끝이고 더 불러올 게 있으면 그날이 끝날 때까지 이어서 불러온 뒤 고른다(`toggleDay`).
- **끌어서 고르기**: 사진을 꾹(0.48초) 누른 채 끌면 처음 사진~손가락 밑 사진까지가 범위(처음 사진이 이미 골라져 있었으면 「풀기」 범위). 되돌아오면 범위가 줄고, 위·아래 가장자리에 가면 화면이 자동으로 넘어간다(`dragMove`/`autoScroll`). 끄는 동안 `touchmove` 를 막아 화면이 같이 밀리지 않게 한다. 마우스는 Shift+클릭으로 마지막으로 누른 사진부터 범위.
- 영상 길이는 오른쪽 위(`길이 ▶`), 막대 글은 「N개 선택됨」. 검색창에 「동영상」·「사진」을 치면 종류로 찾는다(`PH_TYPEWORD`).
- 시험: Edge(headless)+CDP 터치로 422·360px 라이트·다크 65개 — 줄 높이 일치 · 썸네일 실제 비율과 `--ar` 일치 · 동그라미 3상태 · 끌어서 늘림/줄임/풀기 · Shift · 「동영상」 검색 · 맨 끝 날짜. ⚠ iPhone 안(WKWebView)에서 `touchmove` 차단과 끌기는 실기기에서 한 번 확인할 것.

## 시간표 방식 (2026-11-22 대표님 「스와이프하면 위에서 있던 것들이 없어지는데 이미 불러온 건 계속 보이게」, 웹 `2026-11-22b`)
- **전체 연월을 달 칸(`.ph-mon`)으로 한 번에 깐다**(`buildTL`) — 칸 높이는 처음엔 `photos/timeline` 의 달별 장수로 어림(`perItem`)하고 사진을 채우면 진짜 높이로 바뀐다. 스크롤한 자리의 달을 먼저 불러오고(`pump`), 한가하면 앞뒤 36달까지 미리 불러온다(`prefetch`). 동시 3개.
- **보던 자리가 안 밀리게**: 브라우저 자동 보정은 꺼 두고(`.ph-content{overflow-anchor:none}` — 켜 두면 크롬에서 이중 보정으로 수천 px 튄다) 위쪽 칸이 바뀔 때 스크롤을 같은 만큼 직접 옮긴다(`resize`). 스크롤·관성 중에는 위쪽 변경을 멎을 때까지 미룬다(`later`/`flushLater`, 터치 중 2.5초·그 밖 160ms).
- **걷어내기**: 그려 둔 사진이 `DOM_CAP`(6000)을 넘으면 화면에서 3달 넘게 먼 달부터 그림만 걷는다(`evict`) — 높이는 붙잡고(`g.h`) 자료는 남겨 돌아오면 바로 다시 그린다. 선택은 걷어낸 달 것도 유지(막대 개수는 화면 수 이상일 수 있다).
- **예전 목록 방식은 그대로**: 검색(`q`)·장소·폴더·영상/사진 탭·앨범은 `hasMon()` 이 거짓이라 앞에서부터 이어 붙이는 목록. 시간표는 「전체 · 필터 없음 · 달 목록을 받음」일 때만.
- **검색·분류 칩은 맨 위에만**(스크롤하면 같이 올라간다). 「선택」 버튼은 없앴고 사진을 꾹 눌러 고른다(통계 줄에 「꾹 눌러 고르기」).
- 시험(임시 하네스 + Edge): `tl_ui_test.py` 83개(422·360 라이트·다크, 범위·날짜 동그라미) + 걷어내기 7개 — 상한을 일부러 250 으로 낮춰 돌렸다. ⚠ iPhone 안에서 관성 스크롤 중 칸이 바뀌는 느낌은 실기기로 한 번 확인할 것.

## 지우기 · 옮기기 · 상세 보기 (2026-11-22 대표님, 웹 `2026-11-22c`)
- **삭제는 확인 창 없이 바로 휴지통**(`photos/trash`) — 사진·영상 + 사이드카(.xmp·.supplemental-metadata.json)를 `E:\구글포토다운로드\휴지통\files\<날짜>\<원래 폴더>\` 로 옮기고 `manifest.jsonl` 에 적는다. **30일 뒤 완전 삭제**(`purge_trash`, 요청이 들어올 때 하루 한 번). 화면에는 알림 「N장을 휴지통으로 옮겼어요 · 되돌리기」(6.5초) — 되돌리기는 `photos/restore {batch, ids}` (파일을 제자리로 + Immich 훑기 → 같은 경로라 같은 번호로 되살아난다). 화면은 먼저 치우고(optimistic) 서버가 못 한 것만 다시 보이게(`syncMonths`).
- **폴더 이동**(`photos/move {ids, to}`): 상세 보기·고르기 막대의 「이동」 → 폴더 고르는 창(`photos/folders` 목록 + 이름 찾기 + 「새 폴더 만들기」, 새 폴더는 사진 폴더 바로 아래). 파일을 옮기면 Immich 외부 라이브러리는 경로로 번호를 만들어서 **새 번호**가 되고 옛 번호는 휴지통 처리 → 6초·20초·50초·2분·5분 뒤 그 달을 서버와 맞춰 새 칸이 나타난다. ⚠ 목록은 `withExif: true`(정보가 읽힌 사진만 — exif 칸이 없으면 목록에서 빠진다)라서, Immich 정보 읽기(metadataExtraction) 대기열이 수천 장 밀려 있으면(압축을 푸는 동안) 옮긴 사진이 한참 뒤에야 새 자리에 보인다(되돌리기는 같은 번호라 바로 보인다). 대기열이 비어 있으면 몇 초. ⚠ 폰에서 올린 사진(`libraryId` 없음)은 Immich 가 보관하는 곳이라 폴더 이동이 안 된다(「옮길 수 없는 사진」으로 알림) — 지우기는 Immich 휴지통(30일)만.
- **상세 보기**(`.viewer.pv`): 앞·뒤 ◀▶ 단추 없음 · 바탕은 앱 바탕색(확대해도) · 맨 아래 줄 왼쪽에 설명(날짜·이름, 아래로 내려앉게) · 오른쪽 끝에 「이동」·휴지통 아이콘. 영상도 맨 아래 조절줄(96px)만 빼고 아래로 쓸면 닫힌다(예전엔 `video` 위 터치를 통째로 제외해서 안 닫혔다).
- **열고 닫기가 느리던 것**: 보기 창이 한 번에 120장의 큰 그림(+영상 겉그림)을 받기 시작하던 것을 **보는 장 ±1장만 큰 그림, ±3장까지 작은 그림**으로(`photoSlide`의 `_near`/`_wake`). 열자마자 격자에서 이미 받아 둔 작은 그림이 보이고 큰 그림이 오면 갈아 끼운다.
- **저장이 안 되던 원인**: 앱의 `saveToPhotos` 가 `/AI/doc/` 가 든 주소만 받는데 사진첩 주소는 `/AI/photo/` 였다 → 서버에 같은 내용을 주는 별칭 `/AI/doc/photo/<id>/<종류>`(`ai_office/routes.py desk_photo`) 를 만들고 웹이 저장할 때 주소를 바꿔 넘긴다(`nativeSaveUrl`). **앱을 다시 빌드할 필요 없음**. 저장·공유 안내 알림은 없앴다(공유는 바로 공유 창, 저장은 끝났을 때 「폰에 저장했어요」 한 줄).
- **여러 장 고르기 막대**: 공유 · 저장 · 이동 · 삭제(삭제도 확인 없이, 「되돌리기」 알림).
- **아이폰 라이브 포토의 짧은 영상**(IMG_1.HEIC 옆 IMG_1.MP4)은 Immich 가 사진에 붙이고 숨겨(`visibility: hidden`) 두는데 이름이 같은 영상이 목록에 새서 안 열렸다 → 목록은 `visibility: timeline` 만. 이미 쌓인 것은 `python photos.py live`(세기) · `python photos.py live go`(휴지통으로 — 30일 뒤 완전 삭제) 로 치운다. 압축을 더 풀면 새로 생기므로 풀기가 끝난 뒤 한 번 더.
- 시험: `viewer_ui_test.py` — 시험 폴더(`_시험`)의 가짜 사진 6장만 지우고 옮기고 되돌린다(진짜 사진은 건드리지 않음). 진짜 휴지통 기록이 있어도 돌릴 수 있다 — 시작 전에 기록 파일을 통째로 백업(`manifest_backup_before_test.jsonl`)하고, 끝나면 시험 사진(`zz_vt_…`) 줄만 걷어낸다(라이브 포토 영상 1,914개가 휴지통에 든 뒤로 예전의 「기록이 비어 있어야 시작」 점검은 뺐다). 49/49. 시험 사진은 정보 읽기가 안 끝나 목록에서 빠지므로 시험 하네스(`photo_harness2.py`)를 `HARNESS_NO_EXIF=1` 로 켜 시험 서버에서만 `withExif` 를 뺀다(실서버 코드는 그대로).

## 구글 포토식 분류 (2026-11-22 대표님 「폴더를 '앨범' 으로 · 즐겨찾기 / 문서 / 장소 / 동영상 / 최근 추가 등을 상단에서 선택」, 웹 `2026-11-22d`)
- 위쪽 칸: **전체 · 앨범 · 즐겨찾기 · 문서 · 장소 · 동영상 · 사진 · 스크린샷 · 최근 추가** (폰 앱에서만 뒤에 「정리」). 얼굴 인식·이름 입력은 안 한다(대표님 지시).
- **앨범 = 예전 「폴더」**(`photos/folders`). 표지(가장 최근 사진)·이름·「항목 N개」 두 줄 카드. 구글 포토가 해마다 만든 「○○○○년의 사진」은 앨범이 아니라 빼고, 장수·표지는 `/view/folder` 를 폴더마다 불러 5분 기억(`_folder_info` — 옮기거나 지우면 `_drop_folders` 가 비운다). 이동 창도 같은 목록(표지 + 항목 수), 「새 앨범 만들기」는 구글 포토 폴더들과 같은 윗 폴더(`base`) 밑에 만든다. 안 쓰는 Immich 앨범(사진첩, `PH_ALBUMS=false`)은 그대로 숨겨 둠.
- **즐겨찾기** = Immich `isFavorite`. 사진을 열면 위 줄의 ♡(눌리면 분홍) — `photos/mark {ids, fav|doc: true|false}`. 구글 포토에서 ♡ 였던 사진은 Takeout JSON 의 `"favorited": true` 에 있으니 `python photos.py favs`(세기만) · `python photos.py favs go`(Immich 에 켜기) — **압축 풀기가 다 끝난 뒤 한 번**, 그리고 사진을 옮긴 직후엔 새 번호가 생긴 다음에.
- **문서** = Immich 태그 「문서」(`DOC_TAG`). **자동 인식은 안 한다** — 알아보려면 AI(머신러닝 컨테이너)를 돌려야 해서 CPU 를 많이 먹는다(위 Docker 항목). 대신 사진을 열어 위 줄의 문서 아이콘으로 직접 표시, 「문서」 칸에 모인다. 나중에 자동으로 채우고 싶으면 ML 컨테이너를 잠깐 켜 한 번 돌리는 방법이 있다(CPU 큼).
- **스크린샷** = 파일 이름으로(`SHOT_NAMES`: 아이폰 스크린샷 `IMG_####.PNG`, `ScreenRecording…`, 안드로이드 `Screenshot…`). Immich v3.2+ 의 `filter.or` + `orderBy` 검색을 쓰고, 이 검색은 `page` 대신 이어받기 표(`cursor` = `{"offset":N}` 을 base64) 라 서버가 쪽 번호를 바꿔 준다. 이름 규칙이라 아이폰에서 PNG 로 저장된 다른 그림도 섞일 수 있다.
- **최근 추가** = Immich 에 들어온 지 3일 이내(`createdAfter`, `RECENT_DAYS`) — 찍은 날 순서. 구글 포토의 「최근 추가」는 구글에 올린 날이지만 Takeout JSON 의 `creationTime` 은 Immich 가 안 읽어서, 지금은 압축 풀기가 끝날 때까지 새로 풀린 사진이 많이 보인다.
- **옮기면 번호가 새로 생겨 ♡·문서 표시가 사라지는 문제**: `move_photos` 가 옮기는 사진의 표시를 `E:\구글포토다운로드\marks_pending.json` 에 적어 두고, 새 번호가 생기면(20초마다 살핌, 일주일 넘으면 포기) `_apply_pending` 이 다시 붙인다. 지우기→되돌리기는 같은 경로라 같은 번호로 돌아와 그대로.
- 라우트: `photos/mark`(POST) 를 `remote_api.py` 와 `ai_office/routes.py` 의 `_DESK_API` 양쪽에 올렸다. 시험: `cats_ui_test.py` — 진짜 Immich 를 읽고 한 장의 ♡·문서를 켰다 끈다(시작·끝에 그 한 장의 표시를 지움 — 진짜 즐겨찾기가 생긴 뒤엔 이 정리 줄을 빼고 돌릴 것).
- 반영 (2026-11-22): 비공개 저장소 `a58c153`(photos.py·remote_api.py) · 웹 `4ce1a41`/`2026-11-22d` 배포 · AI Office(8000 PID 만 종료 → 감시기가 살림, `photos/mark` 404→401) · 효딩쓰 「다시 켜기.bat」. 시험 `cats_ui_test.py` 66/66 · `viewer_ui_test.py` 49/49.
- 11-22 밤에 남겨 둔 것(휴지통 보기·보관함)은 11-23 에 했다 — 아래 「사진첩 개편」.

## 사진첩 개편 (2026-11-23 대표님 「사진→사진첩 · ☰ 메뉴 · 휴지통 · 쓸어 고르기 · 막대 끌면 그 달 사진이 바로」)
- **제목 「사진첩」**. 위 통계 줄(「사진·동영상 N개」)과 「꾹 눌러 고르기」는 없앴다. 왼쪽 끝 **☰**(오른쪽 X 는 그대로) → 왼쪽에서 미끄러져 나오는 메뉴(`openMenu`)에 분류(전체·앨범·즐겨찾기·문서·장소·동영상·사진·스크린샷·최근 추가·휴지통·보관함). 위쪽 칩 줄은 폭이 좁아 메뉴로 옮겼다. 폰 앱이면 「정리」도 메뉴에.
- **스켈레톤**: 아직 안 불러온 달 칸(`.ph-mon.wait`)과 불러오는 동안의 타일이 회색 반짝이(`.ph-skel`)로 보인다. 그림은 받으면 서서히 나타난다(`.ld`).
- **휴지통 보기** = `photos/trashed`(manifest 의 trash 에서 restore·purge 를 뺀 것, 최신순 · 30일 남은 날수) + 사진 `tthumb`/`tview` 서명 링크(휴지통 폴더의 `thumbs/<번호>.t|.p` — 지울 때 Immich 썸네일을 미리 떠 둔다. Immich 가 이미 지운 번호라서). 열면 맨 위 막대에 「비우기」(확인 창), 사진을 고르면 막대에 「복원」·「영구 삭제」(확인 창 — 이것만 되돌릴 수 없다), 사진을 열어도 같은 두 동작. 복원은 같은 번호로 돌아온다. `photos/purge {ids|all}`.
- **보관함**(구글 포토의 「보관함」) = Immich `visibility: archive`. 사진을 꾹 눌러 고른 뒤 막대의 「보관」(보관함 칸에서는 「해제」) — `photos/mark {archive}`. 보관한 사진은 타임라인·검색에서 빠지고 보관함 칸에만 모인다.
- **쓸어 고르기**: 사진을 꾹 눌러 고른 뒤 손가락을 끌면 범위로 늘어난다(고르는 중엔 오른쪽 빠른 스크롤 막대·「뒤로 쓸어 닫기」가 손에 걸리지 않게 막았다 — `bindSwipeClose` 가 `.selecting`/`.ph-scrub`/`.ph-selbar` 위 터치는 무시).
- **「서버가 느린가」 = 아니다**: 서버는 100장에 0.05~0.12초. 느렸던 건 화면 쪽 — ① 한 달을 100장씩 **차례로** 받던 것 → 쪽을 3~5개 동시에(`PAR`/`PAR_FULL`, 첫 쪽은 혼자) ② 고를 때마다 막대를 다시 만들던 것 → 키(`selKey`)가 같으면 한 번만 만들고 숫자만 바꿈 ③ 장수가 수천인 달(압축 풀기 중엔 2026-11 에 날짜 없는 1만 장이 몰려 있다)을 한꺼번에 그리던 것 → **600장까지만 먼저 그리고**(`LAZY`) 화면 끝 2600px 안에 들어올 때 이어서 받는다(`nearEnd`/`lazyStop`, 스크롤 멈추면 200ms 뒤 `pump`).
- **막대를 끌면 그 달 사진이 바로 보인다**: 끄는 동안 연월 라벨만 바뀌던 것을 `liveJump` 가 그 달 칸으로 같이 스크롤하고 그 달부터 불러온다(손을 떼면 `jumpMonth` 로 정확히 맞춤).
- 시험(임시 하네스 + Edge, 진짜 Immich): `new_ui_test.py` 82/82(스켈레톤·제목·메뉴 폭 390/422/360·쓸어 고르기·보관·휴지통 되돌리기/비우기/취소·막대 미리보기·쪽 동시 요청) · `viewer_ui_test.py` · `cats_ui_test.py`. ⚠ **폰에서만 확인 가능**: 손가락 쓸어 고르기 감각·막대 끌 때 미리보기 감·스크롤 감·♡/문서·공유 창.

## PC 효딩쓰 창의 사진첩 (2026-11-23 대표님 「데스크탑에는 사진첩이 아예 없는데? 모바일만 하면 어떡해」)
- PC 창(`server/voice/desk/ui.html`, 비공개 저장소)에 📷 단추(제목줄)·서랍 「사진첩」·전체 화면 칸(`#photos` + `<iframe id="photosFrame">`)을 더했다. **화면은 휴대폰 앱과 같은 웹 화면을 그대로 쓴다**(따로 또 만들지 않는다 — 한 번 고치면 양쪽에 반영): AI Office 8000 이 내주는 `http://127.0.0.1:8000/AI/desk-app/index.html?embed=photos` 를 iframe 으로 띄우고, 웹 쪽은 `?embed=photos` 면 짝짓기·알림·로그인 화면을 건너뛰고 곧바로 `openPhotos()`.
- **열쇠**: 창이 켜질 때 `remote_api.PC_TOKEN`(`secrets.token_urlsafe`, 디스크에 안 씀)을 만들고 `Api.photo_embed()` 가 주소와 같이 준다. 주소 끝 `#tk=…`(# 뒤라 서버로 안 간다)로 웹이 읽어 바로 지운다. 이 열쇠는 **사진 길(`photos`, `photos/…` 단 `photos/up*`(폰 백업 올리기) 제외)만** 열린다 — 그 밖은 403. 창을 다시 켜면 열쇠도 바뀐다. embed 안에서는 열쇠를 저장소(localStorage)에 쓰지 않고 메모리에만 둔다(같은 주소를 브라우저로 열었을 때 진짜 폰 짝 열쇠를 덮지 않게) · 열쇠가 없거나 401 이면 휴대폰용 「연결 코드」 화면 대신 「열쇠가 맞지 않아요 — 효딩쓰 창을 껐다 켜 주세요」 안내만 · 대화·알림·새 화면 받기(`checkWebUpdate`)는 하지 않는다 · 폭은 창 전체(가운데 780px 칸 없음, `html.embed`). 열쇠 창구가 둘(창 47834 · 두뇌 47835)이고 열쇠는 프로세스마다 달라서, 사진첩은 **창이 켜져 있을 때만** 열린다(8000 이 창 → 두뇌 순서로 넘기므로 창이 열려 있으면 창 열쇠가 통한다).
- **저장**: iframe 안에서는 PC 의 「다운로드」를 직접 못 쓰므로 웹이 `postMessage {desk:'photos-save', files:[{url,name}]}` 를 부모에 보내고 `Api.photo_save(files)` 가 **서명 원본 주소 모양(`/AI/photo/<번호>/orig?e=&s=`)만** 받아 `127.0.0.1:8000` 으로 내려받는다(30개까지 · 이름 겹치면 `(2)` · 경로 문자 제거 — 역슬래시도) → 다운로드 폴더 + 탐색기로 그 파일 선택. 결과는 `photos-saved` 로 돌려 알림. ⚠ 메시지는 `ev.origin`·`ev.source` 가 우리 iframe 일 때만 받는다.
- 시험(2026-11-23): ① `photo_save` 단위 9개(나쁜 주소·`..` + 역슬래시 이름·겹침·30개 제한 — 이 시험에서 **역슬래시가 안 걸러지던 결함**을 찾아 고침) ② 진짜 `remote_api.serve` 를 다른 포트에 띄운 열쇠 범위 27개(photos 밖·`photos/up*` 403, 열쇠 없음·틀림·짧음·너무 긺 401, photos·timeline·folders·trashed 열림) ③ embed 화면 43개 — 가짜 부모 창(다른 출처, `ui.html` 과 같은 메시지 규칙)이 iframe 으로 띄운 화면을 Edge 로 468·1000px 에서(열쇠 메모리만·#tk 지움·폭·☰·Esc·저장→`photos-save`·공유도 저장·X→`photos-close`·알림 되돌림·최상위 창의 `?embed` 무시). **진짜 pywebview 창 안에서 iframe 이 뜨는지·다운로드 폴더에 저장되는지는 창을 껐다 켜서 확인한다**(CLAUDE.md 2.0.3: 바탕화면 「효딩쓰 다시 켜기.bat」).

## 썸네일이 늦게 뜨는 문제 — SSD 보관소 (2026-11-23 대표님 「원본을 바로 보여주니까 딜레이가 있는 거 아닐까? 아주 저용량 썸네일로 만들어 두고 그것만 보이게 하면 …」)
- **가설은 아니었다**: 격자는 처음부터 원본이 아니라 Immich 가 만든 **작은 썸네일**(webp 250px, 8~29KB, 평균 약 13KB)만 쓴다. 원본(`orig`)·큰 그림(`preview`)은 사진을 열었을 때만 부른다. 이미 「저용량 썸네일만 목록에」 구조였다.
- **진짜 원인 = 썸네일 파일이 있는 E:(HDD)**: E: 는 WD60EZAZ 6TB SMR 하드 — 압축 풀기(약 24MB/s 쓰기)와 Immich 정보 읽기(metadataExtraction 대기 20만 건)가 디스크를 물고 있어 **대기열이 6~100**, 파일 하나 읽는 데 0.18~0.47초(최대 4.3초). 한 달 100장이 한꺼번에 오면 그 줄에 서서 기다린다. 게다가 AI Office(8000)는 waitress 스레드가 12개뿐이고 긴 폴링이 몇 개를 잡고 있어, 썸네일 요청이 스레드를 쥐고 기다리는 동안 **목록 요청까지 밀렸다**.
- **고친 것**: ① `ai_office/routes.py` `desk_photo` — kind `thumb` 만 **C:(NVMe SSD)의 `C:\que-desk\cache\photo_thumbs\<번호 앞2자>\<번호>.img`** 를 먼저 본다(환경값 `PHOTO_THUMB_CACHE` 로 위치 변경). 있으면 SSD 에서 바로(중앙값 3.9ms, 동시 11장 2.4초→0.05초), 없으면 Immich 에서 받아 2MB 이하 이미지일 때만 저장. 서명(HMAC)·만료·종류 바꿔치기 검사는 캐시가 있어도 **그대로 먼저** 한다(403/410), 304 지원, 404 는 저장 안 함. TTL 45일. 응답 머리말(noindex·nosniff·Referrer-Policy)·`Cache-Control: private, max-age=86400` 은 예전과 같다. ② `ai_office_server.py` waitress `threads` 12 → **24**. ③ `E:\immich\tools\warm_thumbs.py`(git 밖) — 목록에 나오는 사진 전부(타임라인에 보이는 것·정보가 읽힌 것)를 **최신순(스크롤 맨 위부터)** 으로 미리 채운다.
- **미리 채우기 쓰는 법**: `C:\Python312\python.exe -I -X utf8 E:\immich\tools\warm_thumbs.py` (한 번 훑기) · `... --loop`(끝나면 20분 뒤 다시 — 압축 풀기로 사진이 계속 늘 때) · `... status`(장수·크기). 멈추기: `E:\immich\logs\WARM_STOP` 파일을 만든다. 일꾼 8개 · BelowNormal · 한 장이 4초 넘게 걸리면 쉰다(압축 풀기에 양보). 이어하기(있으면 건너뜀) · 서버(8000)가 꺼져 있어도 Immich 에서 직접 받는다. 로그 `E:\immich\logs\warm_thumbs.log`(UTF-8). 11-23 11시 기준 8,600장·114MB(초당 약 28장) — 전부 차면 약 2~3GB(C: 여유 107GB).
- ⚠ **압축 풀기·정보 읽기가 끝나면 한 번 더 돌릴 것**: 아직 정보가 안 읽힌 사진은 목록에 안 나오므로 그때그때 못 채운다. 새로 들어온 사진의 썸네일은 처음 한 번만 Immich(HDD)에서 받고 그 뒤부터 SSD.
- 되돌리려면: `routes.py` 의 `if kind == 'thumb':` 캐시 분기를 지우면 예전 경로(Immich 직접)로 돌아가고, 보관소 폴더는 지워도 된다(다시 채워짐).
- ⚠ 이 코드(`server/ai_office/*`·`ai_office_server.py`)는 git 에 안 올라간다(`server/` 제외) — 다른 기계로 옮기면 이 절을 보고 다시 넣을 것. AI Office 는 포트 8000 리스너 PID 만 죽이면 감시기가 약 9초 뒤 되살린다.

## Docker(Immich)를 가볍게 (2026-11-22 대표님 「docker 가 CPU 를 많이 먹는 것 같은데 … 컴퓨터 쓰는 데 불편 없도록, 꼭 필요한 거면 그냥 둬」)
- **원인**: `immich_server` 가 한때 CPU 450%(4.5코어)였다 — 압축 풀기·시험으로 새 파일이 계속 들어오면 정보 읽기(metadataExtraction 5개 동시)·썸네일(3개)·훑기(5개)·sidecar(5개)가 한꺼번에 돈다. 가상머신(WSL)에도 코어·메모리 상한이 없어 16코어·31GB 전부가 열려 있었다. 대기열이 비면 1% 안팎.
- **조정(꼭 필요한 건 그대로 둠)**: ① Immich 설정 작업 동시 수 — thumbnailGeneration 3→2 · metadataExtraction 5→2 · sidecar 5→2 · library 5→2 · backgroundTask 5→2 · migration 5→1 · search 5→2, 영상 썸네일 ffmpeg 스레드 0(전부)→2 (`PUT /api/system-config`) ② 컨테이너 상한 — `immich_server` 4코어·6GB, `immich_postgres` 2코어 (`E:\immich\docker-compose.yml` 에 `cpus`·`mem_limit` + `docker update` 로 즉시 적용, 다시 만들어도 유지) ③ WSL 가상머신 `C:\Users\pdnom\.wslconfig` `processors=8` `memory=12GB` — **다음 WSL 재시작(재부팅)부터** 적용(지금 돌리던 것을 끊지 않으려 일부러 안 껐다) ④ 압축 풀기(`extract_all.py`)는 `BelowNormal` 우선순위로 띄운다.
- 이미 꺼 둔 것: 머신러닝(얼굴·스마트검색·CLIP), 영상 변환(transcode). 얼굴인식·이름 입력은 쓰지 않는다(대표님 11-22).
- 되돌리려면: 설정 값을 위 「→」 앞 숫자로, compose 의 `cpus`·`mem_limit` 줄을 지우고 `docker compose up -d`, `.wslconfig` 삭제 후 `wsl --shutdown`.
- 압축 풀기 현황(11-22 18시): 46개 중 확인 끝 14개 → 13개 삭제(1개 `2-001` 은 예전부터 있던 JSON 25건이 zip 과 달라 일부러 남김), 나머지 32개는 `DELETE_AFTER` 켠 채 이어서(`MAX_ZIPS` 지움) — 풀고 확인되면 바로 지우므로 디스크 여유 ≈ 1.4TB 가 거의 안 줄어든다.

## C: 가 갑자기 찼을 때 — Docker 가상 디스크(vhdx) 줄이기 (2026-11-24 대표님 「도커 꺼졌는데 C드라이브가 갑자기 왜 다 찼지?」)
- **증상**: Docker Desktop 「Disk full」(`Docker\log\vm\init.log` 쓰기 실패) — 11-24 13:09 에 멈췄고 C: 여유 17.9GB. 사진첩(Immich)도 같이 꺼진다.
- **원인(실측)**: ① `C:\Users\pdnom\AppData\Local\Docker\wsl\disk\docker_data.vhdx` **115.6GB** — 그런데 VM 안 실제 내용은 **6.5GB**(이미지 3.7 · 컨테이너 1.4 · DB 볼륨 0.4). vhdx 는 늘기만 하고 안 줄어든다. ② Immich DB 는 이름 있는 볼륨 `pgdata` 라 E: 가 아니라 이 vhdx 안(C:). ③ **valkey(immich_redis) 큐**가 폭증했다 — `AssetExtractMetadata` 대기 **423만 건**(DB 사진은 6.5만 개뿐 → 같은 사진 작업이 수십 번 중복, 압축 풀기 중 스캔·새로 고침이 반복 접수된 결과로 추정). 큐가 1.3GB 라 valkey 가 **스냅샷(`dump.rdb`)을 쓸 때마다 vhdx 가 ≈1.07GB 씩 계단식으로 커졌다**(저장 규칙 `60초 1만 건`). 컨테이너 안(`/data`)에 쓰므로 vhdx 안이다.
- **Docker 와 무관한 C: 점유**: `pagefile.sys` 62GB(11-16 부터, 실제 사용 최대 7.5GB — 시스템 관리라 재부팅 전엔 안 줄어듦) · `hiberfil.sys` 27.5GB · pip/uv/npm 캐시 약 35GB · Riot Games 54GB(TFT·LoL 오늘 자동 업데이트 17GB) · Unity 빌드 산출물(`C:\woopang\Library\Bee`) 8GB.
- **한 일(11-24)**: pip·uv·npm 캐시 비움(+22GB) → `powercfg -h off`(관리자, 대표님이 실행, +27.5GB) → **vhdx 압축 115.2GB → 8.1GB**(C: 여유 65GB → 173GB) → DB 는 압축 전후 같음(65,109건·275MB). 백업: Immich 자동(`E:\immich\library\backups`, 매일 02:00) + 수동 `E:\immich\backup_manual\immich_before_compact_*.dump`.
- **vhdx 압축 절차(다음에도 같다, 약 10초)**: ① `docker stop immich_server immich_postgres immich_redis` ② `wsl -d docker-desktop -e sh -c "sync; fstrim -v /mnt/docker-desktop-disk"` ③ `docker desktop stop` → `wsl --shutdown` ④ **관리자 PowerShell** 에서 `diskpart` — `select vdisk file="<위 vhdx>"` · `attach vdisk readonly` · `compact vdisk` · `detach vdisk` ⑤ Docker Desktop 다시 켜기(컨테이너는 `restart: always` 라 저절로). ⚠ `docker` 는 Git Bash PATH 에 없다 — `C:\Program Files\Docker\Docker\resources\bin\docker.exe`. ⚠ 압축 전에 `pg_dump -Fc` 로 DB 를 한 번 더 받아 둘 것. ⚠ vhdx 를 지우거나 새로 만들지 말 것(DB 가 들어 있다).
- **재발 방지**: 11-24 에 런타임으로 껐던 valkey 스냅샷(`config set save ""`)은 컨테이너가 다시 뜨면 풀린다 — 실제로 풀려서 11-26 에 다시 32GB 가 됐다. **11-26 부터 compose 의 `redis` 에 `command: ["valkey-server", "--save", ""]` 로 고정**했다. 큐는 Redis 안에만 있어 컨테이너를 재생성하면 큐와 일시정지 상태가 비워진다(사진·DB 는 그대로) — 재생성 뒤 복원법은 아래 「알아 둘 것」 첫 항목.
- **감시**: `docker_data.vhdx` 크기가 20GB 를 넘거나 C: 여유가 50GB 아래면 위 절차로 다시 줄인다.
- **11-26 후속(대표님 「1,2,3번 수정하고 4번은 정리해도 될 것 같아」)**: ① 큐 정리·compose 고정 → 아래 「알아 둘 것」 첫 항목. ② **vhdx 다시 압축 30.75GB → 6.43GB**(11-24 임시 조치가 컨테이너 재기동으로 풀려 2일 새 9.4 → 32.5GB 로 다시 컸었다). 이번엔 `fstrim` 이 38MB 밖에 안 비웠는데도 압축이 됐다 — 압축은 VM 이 이미 비운 블록을 쓴다. ③ **`pagefile.sys` 16GB 고정**(관리자, `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management` 의 `PagingFiles` = `C:\pagefile.sys 16384 16384`, 자동관리 끔) — **재부팅해야 적용**(그때 59GB → 16GB, 실사용 최대 7.5GB·RAM 64GB). ④ 캐시 정리(+약 62GB): 허깅페이스는 **코드가 안 쓰는 모델 6개만**(m2m100·mbart·opus-mt-ko-en·xlsr-53-japanese·nb-wav2vec2 nynorsk·Qwen3-TTS 1.7B-CustomVoice, 약 17GB) — 쓰는 것(Qwen3-TTS 0.6B·VoiceDesign=효딩쓰 목소리 `tts_server.py`, faster-whisper 계열·ForcedAligner·xlsr-korean·wespeaker=자막 작업 `server/preview`, realisticVision=`server/yougotdog/gen`)은 남겼다 · Android AVD 3개는 데이터·스냅샷만(`userdata-qemu.img*`·`snapshots`, 약 26GB — 설정·SDK 시스템 이미지는 그대로, 에뮬레이터는 처음 켤 때 초기화 상태로 뜬다) · Unity 다운로드 캐시 `AppData\Local\Unity\cache\{npm,packages}`(약 18GB — 열린 에디터가 쓰는 `upm` 은 남김, 다음에 패키지를 열 때 다시 받는다).

## 알아 둘 것
- **(11-26 처리됨) Immich 중복 큐 423만 건 → 34,399건**: 대표님 승인(11-26)으로 `docker compose up -d redis` 로 valkey 를 다시 만들어 큐를 비우고(1.3GB → 12MB) 정보 읽기만 「누락분」으로 다시 접수했다(`PUT /api/jobs/metadataExtraction {command:'start'}` — DB 에 읽힌 기록이 없는 34,393장과 일치). compose(`E:\immich\docker-compose.yml`, 백업 `.bak_20261126`)의 `redis` 에 `command: ["valkey-server", "--save", ""]` 를 넣어 **스냅샷 저장을 영구히 껐다** — 컨테이너를 다시 만들어도 유지된다. ⚠ **저장을 껐으므로 valkey 를 다시 만들거나 Docker·PC 를 껐다 켜면 큐와 「일시정지」 상태가 같이 사라진다**(11-26 에 Docker 를 껐다 켜서 실제로 한 번 더 비워졌다). 사진·DB 는 그대로지만 ① 정보가 안 읽힌 사진은 사진첩 목록에 안 나오고 ② 일부러 멈춰 둔 영상 변환·얼굴·스마트검색이 풀린다 → **재기동 뒤엔 `C:\Python312\python.exe -I -X utf8 E:\immich\tools\requeue.py`** 를 한 번 돌린다(3개를 다시 `pause` → 정보 읽기 누락분 접수 → 3개는 「누락분 찾기」 1건씩만 쌓고 멈춘 채 대기). 이 3개는 대표님이 켜실 때 `PUT /api/jobs/<이름> {command:'resume'}` 하면 그때 목록이 만들어진다. (재기동에도 큐를 남기고 싶으면 compose 를 `--save 21600 1` 같은 드문 저장으로 바꾸면 된다 — 큐가 작을 땐 스냅샷이 수십 MB 라 vhdx 가 커지지 않는다. 대표님 결정 사항.)
- ⚠ **(11-26 확인) 11-24 의 임시 조치(`valkey-cli config set save ""`)는 컨테이너가 다시 뜨면서 풀려 있었다** — 그 사이 vhdx 가 9.4GB → 32.5GB 로 다시 커졌다(2일). 그래서 런타임 설정에 기대지 말고 compose 로 고정한다(위).
- Immich 작업 대기열: 썸네일·메타데이터는 켜 두고, 영상 변환·얼굴·스마트검색은 일부러 멈춰 있다. 썸네일 대기열이 멈춰 있으면 격자가 빈 칸으로 보인다(`/api/jobs/thumbnailGeneration` `resume`).
- 장소 이름은 Immich 지명 데이터라 영어(Chūō · Japan)로 나온다.
- 공유는 파일째(10장·80MB까지), 저장은 폰 사진첩으로(30장까지). 큰 영상은 저장을 쓴다.
- 앱 검증은 Edge 실브라우저(422·390·375·360px, 라이트·다크)로 했다. 폰 안의 「사진첩에 저장」·공유 창은 앱 기능이라 브라우저로는 못 본다 — 실기기에서 한 번 눌러 확인할 것.
