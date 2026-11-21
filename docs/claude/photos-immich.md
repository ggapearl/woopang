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
`E:\immich\tools\extract_all.py` (이어하기 · zip 은 읽기만 · 삭제 코드 없음). 숨김 창으로 `C:\Python312\python.exe -I E:\immich\tools\extract_all.py`.
- 상태·로그: `E:\immich\logs\extract_state.json`(zip 별 확인 끝났는지) · `extract_all.log`. `status` · `audit` 하위 명령. 멈추기: `E:\immich\logs\STOP` 파일.
- 파일마다 `.part` 로 풀고 CRC·크기를 맞춘 뒤 이름을 바꾼다. 이름이 겹쳐 크기가 다르면 `E:\구글포토다운로드\충돌\<zip>\` 에 따로 둔다. 열리지 않는 zip(받다 만 것)은 건너뛰고 상태에 기록.
- 디스크 여유가 (풀 양 + 예비 80GB) 보다 모자라면 **그 zip 앞에서 멈춘다**. zip 은 **대표님이 「풀린 걸 확인한 zip 은 지워도 돼」 하시기 전엔 지우지 않는다**(`verified` 가 참인 zip 만 후보).
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

## 알아 둘 것
- Immich 작업 대기열: 썸네일·메타데이터는 켜 두고, 영상 변환·얼굴·스마트검색은 일부러 멈춰 있다. 썸네일 대기열이 멈춰 있으면 격자가 빈 칸으로 보인다(`/api/jobs/thumbnailGeneration` `resume`).
- 장소 이름은 Immich 지명 데이터라 영어(Chūō · Japan)로 나온다.
- 공유는 파일째(10장·80MB까지), 저장은 폰 사진첩으로(30장까지). 큰 영상은 저장을 쓴다.
- 앱 검증은 Edge 실브라우저(422·390·375·360px, 라이트·다크)로 했다. 폰 안의 「사진첩에 저장」·공유 창은 앱 기능이라 브라우저로는 못 본다 — 실기기에서 한 번 눌러 확인할 것.
