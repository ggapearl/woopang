# 사진 📷 (구글 포토 → Immich → 효딩쓰 앱) — 운영 메모

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
- **iOS 앱 2.4.0 (11)**: 코드는 완성(브랜치 `ios/photo-backup` · 커밋 `87eb569`, 컴파일은 Mac 빌드 세션에서 처음 해 본다). 맥 빌드 요청은 `C:\nongmin-app\IOS_BUILD_HANDOFF.md` 맨 위에 「⏸ 대기」로 적어 뒀다 — 대표님이 올리라고 하시면 「🔔 지금 요청」으로 바꾼다. 웹은 `2026-11-21c`.
- **올리는 쪽은 네이티브 앱**이다(웹뷰는 폰 사진첩을 못 읽는다). 웹은 `backupConfigure({base, token})` 로 주소·열쇠만 넘기고, 화면(백업 카드·✓·정리)만 그린다. 네이티브 계약은 `app.js` 의 「Native.backup…」 주석에 있다(`backupStatus/Enable/Disable/Run/Configure/List/Thumb`, `backupCleanupPlan/backupCleanup`, 이벤트 `'backup'`).
- 서버: `POST photos/up?lid&off&total&name&ts&mod&kind` — 6MB 조각 이어 올리기(어긋나면 409 `{have}`) → `E:\immich\incoming` 에 모아 끝나면 Immich 업로드(`/data/upload`, libraryId 없음) · `photos/up/check`(이미 올린 것) · `photos/up/state`(올린 장수) · `photos/up/verify`(정리 전 재확인). 기록 `E:\immich\phone_backup.db`(sqlite).
- **켠 시각(`since`) 이후의 새 사진·영상만** 올린다(대표님 「기존 사진은 무시」). LTE·Wi-Fi 구분 없음(Wi-Fi 전용 스위치 삭제 — 요금제 무제한).
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
- 시험: `viewer_ui_test.py` — 시험 폴더(`_시험`)의 가짜 사진 6장만 지우고 옮기고 되돌린다(진짜 사진은 건드리지 않음). 시험 전 휴지통 기록이 비어 있어야 시작하고 끝나면 시험 흔적만 정리한다. 시험 사진은 정보 읽기가 안 끝나 목록에서 빠지므로 시험 하네스(`photo_harness2.py`)를 `HARNESS_NO_EXIF=1` 로 켜 시험 서버에서만 `withExif` 를 뺀다(실서버 코드는 그대로).

## Docker(Immich)를 가볍게 (2026-11-22 대표님 「docker 가 CPU 를 많이 먹는 것 같은데 … 컴퓨터 쓰는 데 불편 없도록, 꼭 필요한 거면 그냥 둬」)
- **원인**: `immich_server` 가 한때 CPU 450%(4.5코어)였다 — 압축 풀기·시험으로 새 파일이 계속 들어오면 정보 읽기(metadataExtraction 5개 동시)·썸네일(3개)·훑기(5개)·sidecar(5개)가 한꺼번에 돈다. 가상머신(WSL)에도 코어·메모리 상한이 없어 16코어·31GB 전부가 열려 있었다. 대기열이 비면 1% 안팎.
- **조정(꼭 필요한 건 그대로 둠)**: ① Immich 설정 작업 동시 수 — thumbnailGeneration 3→2 · metadataExtraction 5→2 · sidecar 5→2 · library 5→2 · backgroundTask 5→2 · migration 5→1 · search 5→2, 영상 썸네일 ffmpeg 스레드 0(전부)→2 (`PUT /api/system-config`) ② 컨테이너 상한 — `immich_server` 4코어·6GB, `immich_postgres` 2코어 (`E:\immich\docker-compose.yml` 에 `cpus`·`mem_limit` + `docker update` 로 즉시 적용, 다시 만들어도 유지) ③ WSL 가상머신 `C:\Users\pdnom\.wslconfig` `processors=8` `memory=12GB` — **다음 WSL 재시작(재부팅)부터** 적용(지금 돌리던 것을 끊지 않으려 일부러 안 껐다) ④ 압축 풀기(`extract_all.py`)는 `BelowNormal` 우선순위로 띄운다.
- 이미 꺼 둔 것: 머신러닝(얼굴·스마트검색·CLIP), 영상 변환(transcode). 얼굴인식·이름 입력은 쓰지 않는다(대표님 11-22).
- 되돌리려면: 설정 값을 위 「→」 앞 숫자로, compose 의 `cpus`·`mem_limit` 줄을 지우고 `docker compose up -d`, `.wslconfig` 삭제 후 `wsl --shutdown`.
- 압축 풀기 현황(11-22 18시): 46개 중 확인 끝 14개 → 13개 삭제(1개 `2-001` 은 예전부터 있던 JSON 25건이 zip 과 달라 일부러 남김), 나머지 32개는 `DELETE_AFTER` 켠 채 이어서(`MAX_ZIPS` 지움) — 풀고 확인되면 바로 지우므로 디스크 여유 ≈ 1.4TB 가 거의 안 줄어든다.

## 알아 둘 것
- Immich 작업 대기열: 썸네일·메타데이터는 켜 두고, 영상 변환·얼굴·스마트검색은 일부러 멈춰 있다. 썸네일 대기열이 멈춰 있으면 격자가 빈 칸으로 보인다(`/api/jobs/thumbnailGeneration` `resume`).
- 장소 이름은 Immich 지명 데이터라 영어(Chūō · Japan)로 나온다.
- 공유는 파일째(10장·80MB까지), 저장은 폰 사진첩으로(30장까지). 큰 영상은 저장을 쓴다.
- 앱 검증은 Edge 실브라우저(422·390·375·360px, 라이트·다크)로 했다. 폰 안의 「사진첩에 저장」·공유 창은 앱 기능이라 브라우저로는 못 본다 — 실기기에서 한 번 눌러 확인할 것.
