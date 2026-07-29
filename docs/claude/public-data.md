# 공공 장소 데이터 일괄 등록 가이드

> 공공데이터 DB INSERT 작업 시 참조

---

## 카테고리 분류
```
⚠️ 모든 공공데이터 색상: NULL (DB에서 color 컬럼 비워둠, 앱 기본색 white로 처리)
⚠️ 공공데이터 카테고리는 publicData 토글로 필터링 (TourAPI와 동일)
⚠️ 공공화장실(toilet)만 별도 카테고리 토글 (보라색)

[publicData 토글 — 공공데이터]
gov        - 시청, 군청, 구청, 경찰서, 소방서, 읍면동 행정복지센터
edu        - 대학교, 도서관, 교육청, 교육지원청
park       - 산 정상(고도 필수), 강/호수, 국립공원, 해수욕장, 공원
utility    - 농협, 우체국
landmark   - 역사 유적지, 다리, 지역 특화 명소, 온천
medical    - 보건소, 종합병원, 보건지소
culture    - 박물관, 미술관, 문화예술회관
sport      - 종합운동장, 체육관
religious  - 주요 사찰, 성당/성지, 향교/서원 (역사적 건축물)
welfare    - 종합사회복지관, 노인복지관, 장애인복지관

[별도 카테고리 토글]
toilet     - 공공화장실 (보라색, CategoryFilterState.Toilet)

[기존 카테고리 토글 — 사용자 데이터]
shop       - 샵/매장 (파란색)
food       - 음식점 (주황색)
cafe       - 카페 (핑크색)

[기존 별도 토글]
hub(터미널) - 고속도로 휴게소는 terminal 토글에 포함 (이미 추가 완료)
```

---

## 이미지 파일 규칙 (필수)
```
⚠️ 중요: 폴더 내에는 아래 파일명만 허용. 다른 파일은 저장하지 않는다.

허용 파일명:
   main.jpg      ← 로고 또는 대표 이미지 (필수, 최소 1장)
   sub_01.jpg     ← 보조 사진 1 (선택)
   sub_02.jpg     ← 보조 사진 2 (선택)
   sub_03.jpg     ← 보조 사진 3 (선택)
   sub_04.jpg     ← 보조 사진 4 (선택, 최대)

규칙:
- 최소 1장(main.jpg), 최대 5장(main + sub 4장)
- 원본 파일명(logo.png, visual_1.jpg 등)으로 저장 금지
- 다운로드 후 반드시 main.jpg / sub_XX.jpg로 변환하여 저장
- 불필요한 원본 파일은 삭제

main.jpg 1:1 비율 규칙:
- main.jpg는 반드시 가로세로 1:1 비율이어야 함
- 최종 저장 크기: 444x444px (JPEG, quality=95)

방법 1: 고해상도 로고/사진이 있는 경우
  from PIL import Image
  logo = Image.open('원본.png')
  size = max(logo.size)
  canvas = Image.new('RGB', (size, size), (255, 255, 255))
  canvas.paste(logo, ((size-logo.width)//2, (size-logo.height)//2))
  canvas = canvas.resize((444, 444), Image.LANCZOS)
  canvas.save('main.jpg', 'JPEG', quality=95)

방법 2: 로고가 저해상도(250px 미만)인 경우 → 상급기관 심볼마크 SVG 활용
  ⚠️ 저해상도 로고를 확대하면 픽셀화 발생 → SVG 벡터 활용 필수
  1. 나무위키/공식사이트에서 상급기관 SVG 로고 검색
  2. SVG에서 심볼 path만 추출 (fill 색상으로 구분: #e6280f, #004799, #0f9e33 등)
  3. 심볼만 포함하는 새 SVG 생성 (흰색 배경, 적절한 viewBox)
  4. Chrome Headless로 800x800 PNG 렌더링:
     chrome --headless --disable-gpu --screenshot=output.png --window-size=800,800 render.html
  5. Pillow로 444x444 JPEG 변환:
     img = Image.open('output.png').convert('RGB')
     img = img.resize((444, 444), Image.LANCZOS)
     img.save('main.jpg', 'JPEG', quality=95)
  6. 임시 파일(svg, html, png) 정리

방법 3: 로고를 구할 수 없는 경우
  → sub 사진(건물/내부) 중 대표적인 것을 1:1 중앙 크롭하여 main.jpg로 사용
  from PIL import Image
  img = Image.open('sub_01.jpg')
  w, h = img.size
  size = min(w, h)
  left, top = (w - size) // 2, (h - size) // 2
  cropped = img.crop((left, top, left + size, top + size))
  cropped = cropped.resize((444, 444), Image.LANCZOS)
  cropped.save('main.jpg', 'JPEG', quality=95)
```

### 공공데이터 이미지 자동 생성 규칙 (필수)
```
⚠️ 중요: 공공데이터 INSERT 시 이미지를 반드시 함께 생성해야 함
   main_photo, folder, sub_photos가 비어있으면 앱에서 오브젝트 생성 실패

카테고리별 이미지 생성 전략:
┌─────────────┬──────────────────────────────────────────────┐
│ 카테고리    │ 이미지 생성 방법                             │
├─────────────┼──────────────────────────────────────────────┤
│ 주요 거점   │ 공식 사이트에서 로고/사진 수집               │
│ (landmark,  │ → main.jpg (444x444) + sub_01~04.jpg         │
│  culture,   │                                              │
│  religious) │                                              │
├─────────────┼──────────────────────────────────────────────┤
│ 일반 공공   │ 카테고리별 심볼 아이콘 자동 생성 (Pillow)    │
│ (gov, edu,  │ → main.jpg = sub_01.jpg (동일 파일)          │
│  medical,   │ 실제 사진 구할 수 없는 경우 심볼로 대체      │
│  sport,     │                                              │
│  welfare,   │                                              │
│  utility)   │                                              │
├─────────────┼──────────────────────────────────────────────┤
│ toilet      │ SVG 심볼 아이콘 직접 생성                    │
│ (공공화장실)│ → _shared_toilet/ 공용 폴더 1개 사용         │
│             │ → 모든 화장실이 동일 폴더/파일 공유          │
│             │ → 각 폴더에 파일 복제 불필요                 │
└─────────────┴──────────────────────────────────────────────┘

sub 사진 규칙:
- 주요 거점(박물관, 종합운동장, 랜드마크 등): 실제 사진 sub_01~04 수집
- 일반 공공(행정복지센터, 보건소 등): main.jpg를 sub_01.jpg로 복제 (동일 파일)
- 화장실: 절대 실사 sub 없음 → main.jpg = sub_01.jpg 동일 SVG 아이콘

카테고리별 심볼 아이콘 색상:
- gov:       (45, 85, 130)  — 파란 계열 (관공서)
- medical:   (40, 110, 80)  — 녹색 계열 (보건)
- edu:       (55, 90, 140)  — 파란 계열 (교육)
- sport:     (50, 120, 90)  — 녹색 계열 (체육)
- religious: (100, 75, 55)  — 갈색 계열 (전통)
- landmark:  (70, 130, 180) — 하늘색 계열 (명소)
- toilet:    (88, 55, 140)  — 보라색 계열 (화장실)

생성 스크립트: server/generate_public_icons.py
실행: python server/generate_public_icons.py
```

---

## 출처 추적 (username 필드)
```
⚠️ 중요: username 필드에 이미지 출처를 기록한다. 'admin' 사용 금지.

출처 표기 규칙:
- 공식 홈페이지: 도메인 대문자 (예: YESAN.GO.KR, CNE.GO.KR)
- Wikimedia Commons: WIKIMEDIA
- 공공누리: GONURI
- 공공데이터포털: DATA.GO.KR
- 직접 촬영: WOOPANG (자체 촬영)

DB INSERT 시:
   username = '{출처}'    ← 'admin' 대신 실제 이미지 출처 기록
```

---

## DB INSERT 절차
```
1. 장소 정보 수집
   - 이름, 주소, 좌표(위도/경도/고도), 카테고리
   - 공식 홈페이지에서 로고/건물사진 다운로드

2. 이미지 저장 구조 (필수)
   uploads/
   └── {YYYYMMDD}_{장소명}/
       ├── main.jpg      ← 로고 또는 대표 이미지 (1:1 비율 필수)
       ├── sub_01.jpg     ← 내부/외관 사진 1
       └── sub_02.jpg     ← 내부/외관 사진 2
   ※ 위 파일 외 다른 파일 저장 금지

3. DB INSERT 양식
   INSERT INTO locations (
       username, name, latitude, longitude, altitude,
       pet_friendly, separate_restroom, instagram_id,
       color, category, status, folder, main_photo, sub_photos,
       model_type, model_scale, created_at
   ) VALUES (
       '{출처}', '{이름}', {위도}, {경도}, {고도},
       {pet_friendly}, {separate_restroom}, '',
       '{색상}', '{카테고리}', 'approved',
       'uploads/{폴더명}', 'uploads/{폴더명}/main.jpg',
       '["uploads/{폴더명}/sub_01.jpg","uploads/{폴더명}/sub_02.jpg"]',
       'cube', 1.0, CURRENT_TIMESTAMP
   )

4. 색상 규칙
   gov (시청/군청/구청/경찰서/소방서/행정복지센터): color='blue'
   toilet (공공화장실): color='purple'
   나머지 (edu/park/medical/culture/sport/utility/landmark/religious/welfare): color=NULL
   ※ color='white' 사용 금지 → NULL로 설정

5. 토글 규칙 (pet_friendly / separate_restroom)
   ⚠️ 중요: DB INSERT 시 반드시 적용

   separate_restroom (화장실분리):
   - 한국 공공기관 전체: true (gov/medical/edu/culture/sport/welfare/utility)
   - 일본 공공기관: false (기본값, 화장실 분리가 보편적이지 않음)
   - 공원/산/해수욕장: false (별도 공중화장실 등록으로 대체)

   pet_friendly (애견동반):
   - 반려견 놀이터/전용 공간: true
   - 한강공원/근린공원/체육공원/산책로/숲길: true (일반적으로 가능)
   - 공원이지만 반려견 금지인 곳: false (예: 신주쿠교엔)
   - 국립공원/자연휴양림: true (외부 산책로 가능)
   - 공공기관(gov/medical/edu 등): false

6. 지하철/기차역/터미널 등록 규칙
   ⚠️ 지하철역, 기차역, 터미널은 locations가 아닌 public_facilities 테이블에 관리
   - public_facilities 테이블 (type: subway/train/terminal/airport)
   - SubwayManager, TrainStationManager, TerminalManager가 이 테이블에서 데이터 조회
   - 해당 데이터는 각 매니저의 카테고리 토글로 표시/숨김 제어

   locations 테이블에는 역 자체를 INSERT하지 않음 (중복 방지)
   단, public_facilities 테이블의 기존 데이터는 적극 업데이트:
   - DB 추가 작업 중 해당 지역 역/터미널 정보가 확인되면 public_facilities UPDATE
   - 좌표 보정, 주소 수정, 노선 정보(extra_info) 갱신 등
   - 신설역 추가, 폐역 삭제 반영
   - 기존 main_photo는 수정 금지 (sub 사진 추가는 가능)
   - 역 근처 공중화장실은 locations 테이블에 toilet 카테고리로 등록 가능
```

---

## 이미지 소스 우선순위
```
1순위: 해당 기관 공식 홈페이지 (go.kr 등) - 공공기관 로고/배너
2순위: 공공누리 1유형 (출처 표기만으로 상업적 이용 가능)
3순위: 공공데이터포털 (data.go.kr)
4순위: Wikimedia Commons CC0 / Public Domain
⚠️ 주의: 저작권 미확인 이미지 사용 금지
⚠️ 주의: 서비스 전 반드시 라이선스 재확인
```

---

## 삭제 시 FK 순서 (필수)
```
DELETE 순서 (반드시 이 순서대로):
1. comment_likes  (WHERE comment_id IN (SELECT id FROM comments WHERE location_id = %s))
2. comments       (WHERE location_id = %s)
3. location_likes (WHERE location_id = %s)
4. fix_requests   (WHERE target_id = %s)
5. locations_fix  (WHERE target_id = %s)
6. locations      (WHERE id = %s)
```

---

## 고도(altitude) 처리 — 필독 (2026-07-28 정립)

### 왜 중요한가
앱은 고도를 **MSL(해발) 기준**으로 사용한다. iOS는 WGS84 타원체 고도를 주므로
[GeoidHelper.cs](../../Assets/Scripts/Utils/GeoidHelper.cs)가 지역별 오프셋(한국 ~30m)을 더해
Android(MSL)와 기준을 통일한다. 최종적으로 이 값이 그대로
`CustomARGeospatialCreatorAnchor.SetCoordinatesAndCreateAnchor()` → `anchorManager.AddAnchor(lat, lon, alt, ...)`로 전달된다.

⚠️ **ARCore의 지형 자동 안착 기능(`ResolveAnchorOnTerrain` / `ResolveAnchorOnRooftop`)은 이 프로젝트에서 사용하지 않는다.**
따라서 **altitude 값이 곧 오브젝트가 떠 있는 절대 높이**이며, 자동 보정은 일어나지 않는다.
`altitude=0`은 "지면"이 아니라 **"해발 0m = 바닷물 높이"** 를 의미하므로,
내륙(지면 해발 30~50m)에서는 오브젝트가 **땅속에 묻힌다.**

### 고도 입력 규칙 (신규 INSERT 시 필수)
```
❌ 금지: altitude = 0  으로 넣고 넘어가기
✅ 필수: INSERT 전에 좌표로 표고를 조회해서 채울 것
```
표고 조회는 **Open-Elevation**(오픈소스·무료·인증키 불필요) 사용:
```python
# 여러 좌표 일괄 조회 (POST, 배치 100건 권장)
import json, urllib.request
payload = json.dumps({"locations": [{"latitude": lat, "longitude": lon}, ...]}).encode()
req = urllib.request.Request("https://api.open-elevation.com/api/v1/lookup",
                             data=payload, headers={"Content-Type": "application/json"})
res = json.loads(urllib.request.urlopen(req, timeout=90).read())
elevations = [r["elevation"] for r in res["results"]]   # MSL 기준 — 앱 기준과 일치
```
- 반환값이 이미 **해발(MSL)** 이라 앱 기준과 그대로 맞는다. 별도 변환 불필요.
- ⚠️ **런타임(앱)에서 호출 금지.** 공개 서버는 rate limit이 있고, 스폰마다 네트워크 왕복이
  생겨 화면 끊김이 악화된다. 반드시 **INSERT 시점에 DB에 채워 넣는 방식**으로 처리한다.

### ⚠️ 공공데이터 vs 사용자 업로드 구분 (일괄 작업 시 생명줄)
고도 일괄 갱신 같은 대량 UPDATE는 **반드시 공공데이터만** 대상으로 한다.
사용자 업로드분은 앱에서 등록할 때 **폰 GPS 실측 고도**가 들어가 있어 이미 정상이며,
덮어쓰면 오히려 망가진다.

| 구분 | category | username | 건수(2026-07 기준) |
|------|----------|----------|--------------------|
| **공공데이터** | `gov` `edu` `park` `utility` `landmark` `culture` `religious` `medical` `welfare` `sport` `toilet` | 기관 도메인 (`GOV.KR`, `EDU.GO.KR`, `SEOUL.GO.KR` 등) | 32,710 |
| **사용자 업로드** | `food` `cafe` `shop` 또는 **빈 문자열 `''`** | 계정명(`pdnom`, `sigorpd`) 또는 **장소명 그대로**(`우팡이쉼터`, `홍대정문` 등) | 101 |

두 기준(category / username)이 서로 교차 오염 없이 일치함을 검증 완료.
**`category = ANY(공공 11종)` 조건만으로 안전하게 분리된다.**

```sql
-- 안전한 대상 선택
WHERE category = ANY(ARRAY['gov','edu','park','utility','landmark','culture',
                           'religious','medical','welfare','sport','toilet'])
```

⚠️ 지하철·기차역·터미널·공항은 **`public_facilities` 테이블**로 완전히 분리되어 있어
사용자 데이터가 섞일 여지가 없다 (고도도 대부분 이미 입력됨).

### 일괄 갱신 절차 (대량 작업 시)
1. **백업 먼저.** `CREATE TABLE locations_altitude_backup_YYYYMMDD AS SELECT id, altitude, ... FROM locations;`
   (2026-07-28 백업본: `locations_altitude_backup_20260728`, 32,811건)
2. 대상 조회 → Open-Elevation 배치 호출 → **체크포인트 JSON에 중간 저장**
   (수만 건이면 수십 분 소요. 중단돼도 이어서 실행 가능하게 할 것)
3. 체크포인트 기준으로 UPDATE → 건수·분포 검증
4. 실기기 확인

#### ✅ 2026-07-28 실제 수행 기록
| 항목 | 결과 |
|------|------|
| 백업 | `locations_altitude_backup_20260728` (32,811건 전체) |
| 조회 | Open-Elevation 27,164건 전부 성공 (배치 100건, 504 오류 2회는 재시도로 복구) |
| 제외 | 음수 표고 58건(데이터 오류) |
| **UPDATE** | **27,106건 실행 → 실제 변경 26,733건** (나머지 373건은 해안·수면이라 조회값도 0m) |
| 검증 | 사용자 업로드 변경 **0건** / 기존 수기값 덮어쓰기 **0건** / 1950m 초과 이상치 **0건** |

갱신 후 카테고리별 평균 고도 — 지형과 부합함을 확인:
`park 176.6m` · `religious 183.3m`(산중 사찰) · `landmark 89.9m` ·
`sport 77.5m` · `culture 67.9m` · `medical 67.5m` · `edu 60.8m` · `gov 56.8m` · `utility 57.2m`

원복이 필요하면:
```sql
UPDATE locations l SET altitude = b.altitude
FROM locations_altitude_backup_20260728 b WHERE l.id = b.id;
```

### 기존 수기 입력값은 덮어쓰지 말 것
2026-07 이전 작업분 중 gov(90%)·medical(59%)·toilet(100%)·public_facilities는
**사람이 장소별로 추정해 넣은 값**이 이미 있다(5, 20, 40, 140, 200처럼 딱 떨어지는 숫자가 특징).
100건 표본 비교 결과 API와 중앙값 +4m 차이로 대체로 일치하지만, **15%는 50m 이상 벌어진다.**
차이가 큰 쪽은 대개 수기값이 더 정확하므로(예: 예당호 출렁다리 수기 40m vs API 135m),
**`altitude = 0 OR altitude IS NULL` 인 행만 채우고 기존 값은 보존한다.**

---

## 좌표 정밀도 보정 — 카카오 로컬 API (2026-07-29 정립)

### 문제
`locations` 좌표 중 **5,136건이 소수점 3자리 이하**로 뭉개져 있었다(2자리=약 1.1km 오차).
게다가 단순 반올림이 아니라 **읍·면 중심부 좌표를 대충 넣은 경우**가 많아 실제 오차가
7km를 넘기도 했다(예: `예산군 대흥면 행정복지센터` 7.3km). 고도를 고쳐도 오브젝트가
엉뚱한 곳에 뜨므로 별도 보정이 필요하다.

### 도구 선택 — 카카오 로컬 API 한 개로 통일
⚠️ `locations` 테이블에 **주소 컬럼이 없다.** 이름과 부정확한 좌표뿐이므로
"주소→좌표" 지오코더(VWorld 등)로는 해결 불가. **"이름으로 장소 검색"** 이 필요하다.

| 후보 | 결과 |
|------|------|
| Nominatim(OSM) | ❌ 한국 기관명 5건 전부 검색 실패 |
| Overpass(OSM) | ❌ 반경 검색 타임아웃 |
| **카카오 로컬 API** | ✅ 채택 — 장소검색·주소변환 둘 다 제공 |

```
키:    .env 의 KAKAO_REST_API_KEY (developers.kakao.com > 앱 > 앱 키 > REST API 키)
헤더:  Authorization: KakaoAK {키}
장소:  GET https://dapi.kakao.com/v2/local/search/keyword.json?query={이름}
주소:  GET https://dapi.kakao.com/v2/local/search/address.json?query={주소}
```
⚠️ **앱에서 [카카오맵] > [사용 설정] ON 필수.** 안 켜면 `disabled OPEN_MAP_AND_LOCAL service` 오류.
⚠️ **Windows 셸에서 curl 로 한글 쿼리 금지** — CP949로 깨져 0건 반환. Python `urllib`로 UTF-8 인코딩할 것.

### ⚠️ 매칭 규칙 — 그냥 1등 결과를 쓰면 안 된다
카카오 키워드 검색은 **유사어 매칭**이라 비슷하지만 다른 장소를 돌려준다. 실측 사례:

| 우리 이름 | 카카오 1등 | 판정 |
|---|---|---|
| 인천**가정중**학교 | 가정**여자중**학교 | ❌ |
| 구의**3동**행정복지센터 | 구의**2동**주민센터 | ❌ |
| **군포** 보건지소 | **군포시**보건소 | ❌ (지소≠보건소) |
| 영동고등학교 | 영동고등학교 | ❌ **163km 떨어진 동명 학교** |

그대로 반영하면 지금보다 나빠진다. **3중 검증**을 반드시 걸 것:

1. **이름 정규화 일치** — 공백 제거 + `행정복지센터|주민센터|면사무소|동사무소|읍사무소` 동의어 치환 후
   완전일치 또는 한쪽이 다른쪽으로 끝남(`서울서초중학교` ⊃ `서초중학교` 같은 접두 지역명 흡수)
2. **시·군·구 교차검증** — 이름에 `예산군` 등 지역 토큰이 있으면 카카오 결과 주소에도 있어야 함
   (동명이인 방어의 핵심)
3. **거리 상한** — 지역 토큰이 **없으면 5km**, 있으면 30km까지 허용
   (지역 검증이 통과했으면 원좌표가 많이 틀렸어도 안전)

동급 후보가 여럿이면 **채택하지 않고 보류**한다. 틀린 값을 넣느니 그대로 두는 게 낫다.

### ✅ 2026-07-29 수행 기록
| 항목 | 결과 |
|------|------|
| 백업 | `locations_coord_backup_20260729` (32,811건 전체) |
| 1차 패스 | 5,136건 조회 → 채택 2,725건(53%) / 미매칭 2,393 / 모호 18 |
| **1차 적용** | **2,725건 UPDATE** (이동 중앙값 1,137m, 최대 4,992m) |
| 검증 | 비공공(사용자) 좌표 변경 **0건** |
| 2차 패스 | 1차 미매칭 중 시·군·구 단서 보유 1,371건 재시도 → 채택 480건(35%) |
| **2차 적용** | **480건 UPDATE** (이동 중앙값 9,013m — 원좌표가 군 중심부로 뭉뚱그려져 있던 케이스) |
| **최종** | 저정밀 좌표 **5,136건 → 1,931건** (3,205건 보정 완료) |

⚠️ **2차 패스가 필요했던 이유**: 1차의 5km 거리제한이 정답을 버리고 있었다.
`예산군 대흥면 행정복지센터`는 카카오가 정확히 찾았는데도 원좌표가 7.3km 틀려 있어 제외됐다.
군 단위 일괄 등록분은 **면사무소 전체에 군청 좌표를 넣은 것으로 보이는 패턴**이 있어,
시·군·구가 교차검증되면 거리제한을 풀어야 한다.

원복:
```sql
UPDATE locations l SET latitude=b.latitude, longitude=b.longitude
FROM locations_coord_backup_20260729 b WHERE l.id=b.id;
```

### ✅ 중복 등록 정리 (2026-07-29 완료)
같은 장소가 이름만 다르게 두 번 등록돼 있었다(이름 정규화 동일 + 5km 이내 기준 **397쌍**).
서로 다른 시기·출처(`POLICE.GO.KR`/`GOV.KR`/`WIKIMEDIA`)로 넣으며 생긴 것으로,
AR에서 같은 건물에 오브젝트가 2개 떴다.
- `종로경찰서`(878) ↔ `서울종로경찰서`(1855) — 208m
- `예산경찰서`(257) ↔ `충남 예산경찰서`(9985) — 10m
- `대흥면행정복지센터`(671) ↔ `예산군 대흥면 행정복지센터`(4163) — 좌표보정 후 5m로 겹침

**처리 원칙** — 유지할 쪽 선정 순서: ①좌표 정밀도 높음 ②사진 있음 ③고도 있음 ④id 작음(선등록).
⚠️ **삭제 전 반드시 사용자 활동 확인.** 댓글·좋아요가 붙은 행은 삭제 목록에서 제외할 것
(2026-07-29 작업분은 댓글·좋아요 0건이라 전량 삭제 가능했음).
백업 `locations_dup_deleted_20260729`(397건, 전체 컬럼) → 삭제는 위 "삭제 시 FK 순서" 준수.

---

## 공중화장실 데이터 (2026-07-29 구축)

### 원본 — API 없음, CSV 수동 다운로드만 가능
[전국공중화장실표준데이터](https://www.data.go.kr/data/15012892/standard.do) (행정안전부)
- **OpenAPI 미제공.** CSV 파일 배포만 하며, 인증키 자체가 필요 없다.
- ⚠️ **2025년 2월부터 위도·경도 컬럼이 제거**됐다. 주소만 있으므로 지오코딩 필수.
- 다운로드: data.go.kr 로그인 → 데이터셋 페이지 → 다운로드 버튼 클릭
  (`file.localdata.go.kr` 로 이동. **주소창 직접 입력은 403** — Referer 검사)
- **자동화는 권하지 않음.** `data-download-url` 속성 + JS 세션 기반이라 직접 URL 후보들이 모두
  302/500으로 막힌다. 억지로 세션을 흉내 낼 수는 있으나 사이트 개편 시 조용히 깨진다.
  화장실 위치는 자주 바뀌지 않으므로 **6개월~1년에 한 번 수동 다운로드**로 충분하다.

원본 규모: 53,547건 / 34개 컬럼 / **CP949 인코딩**(UTF-8 아님, 읽을 때 지정 필수).
주소는 100% 존재(도로명 48,171 + 지번 38,848, 최소 하나는 전부 보유).

### 구축 절차
1. CSV에서 대상 지역 필터 → `소재지도로명주소`(없으면 `소재지지번주소`)
2. **카카오 주소검색**(`search/address.json`)으로 좌표 변환
3. **Open-Elevation**으로 고도 조회 (음수는 0으로 클램프)
4. 기존 toilet 행과 **50m 이내 + 이름 유사** 이면 중복으로 스킵
5. INSERT — 아래 고정값 사용

```python
username='DATA.GO.KR', color='purple', category='toilet', status='approved',
folder='uploads/_shared_toilet',                       # 공용 폴더 (아이콘 1개 공유)
main_photo='uploads/_shared_toilet/main.jpg',
sub_photos='["uploads/_shared_toilet/sub_01.jpg"]',
model_type='cube', model_scale=1.0,
pet_friendly=False, separate_restroom=False,
id=MAX(id)+1                                            # 시퀀스 없음 — 명시 필수
```

### ✅ 수행 기록
| 범위 | CSV 대상 | 좌표변환 성공 | INSERT |
|------|---------|--------------|--------|
| 예산군 | 84 | 84 (100%) | 84 |
| 충청남도 | 2,640 | 2,395 (91%) | 2,395 |
| **누적 toilet** | | | **2,517건** |

실패 245건은 주소가 불완전하거나(`신방쉼터A 화장실` 등) 카카오가 못 찾은 경우 — 스킵 처리.
전국 나머지(약 5만 건)는 미적용. 확대 시 DB 구성 비율(현재 전체 34,893건 중 화장실 7%)과
카카오 API 일일 한도를 함께 검토할 것.

### ⚠️ Windows 실행 함정
- **CSV는 CP949** — `open(path, encoding='cp949')`. UTF-8로 읽으면 깨진다.
- **cmd.exe에서 `set VAR=%CD% && cmd` 금지** — `%CD%`와 `&&` 사이 공백까지 값에 포함되어
  경로가 `scratchpad \file` 처럼 깨진다. 스크립트는 `Path(__file__).parent` 로 자기 위치를
  스스로 찾게 만들고, 실행 플래그는 `--go` 같은 인자로 받을 것.
- **한글 쿼리를 curl로 보내지 말 것** — CP949로 깨져 API가 0건을 반환한다. Python `urllib` 사용.

