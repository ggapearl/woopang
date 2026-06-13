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

