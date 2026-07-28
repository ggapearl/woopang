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

### 🔴 미해결 과제 — 좌표 정밀도
고도와 별개로 더 심각한 문제가 있다. `locations` 좌표 정밀도 분포:

| 소수점 자릿수 | 건수 | 실제 위치 오차 |
|---|---|---|
| 0~1자리 | 149 | 11km ~ 111km |
| 2자리 | 1,795 | 약 1.1km |
| 3자리 | 3,193 | 약 110m |
| 6~7자리(정상) | 약 26,000 | 1m 미만 |

약 **5,100건이 좌표 자체가 뭉개져 있어**, 고도를 고쳐도 AR 오브젝트가 실제 건물에서
수백 m~1km 떨어진 곳에 뜬다. 주로 `GOV.KR` 출처의 면 단위 행정복지센터.
→ 해결하려면 **주소 기반 지오코딩으로 좌표 재작성** 필요
   (VWorld 지오코더 — vworld.kr 회원가입 후 [오픈API > 인증키 > 인증키 발급], data.go.kr 키와 별개.
    일 40,000건 무료. 단 "결과 DB 저장 금지" 약관 조항 확인 필요).

