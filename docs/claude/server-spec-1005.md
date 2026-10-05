# 서버 명세 — 2026-10-05 (앱 v19 작업분과 짝)

> 서버(`C:\woopang\server`, Flask)는 git 에 없다 → 이 문서대로 **Windows 세션이 만든다**.
> 시크릿은 전부 `.env` + `os.getenv()` (CLAUDE.md 6). 이 문서·코드·커밋에 키 실값을 쓰지 말 것.

| # | 무엇 | 앱 쪽 상태 | 배포 순서 |
|---|---|---|---|
| 1 | 기상청 '지금 날씨' (`KMA_SERVICE_KEY`) | 이미 읽음 (pty·rn1·humidity) | 언제든 |
| 2 | `/api/weather` 에 `humidity` | 이미 읽음 | 언제든 |
| 3 | `upload_id` 중복 거르기 | v19 부터 '같은 내용 = 같은 키, 다른 장소 = 새 키' | 언제든 (먼저 켜도 예전 앱은 안 보냄) |
| 4 | TourAPI 키를 서버가 붙이기 (`/proxy`) | **v19 앱은 키를 안 보낸다** | **서버 먼저 → 그다음 앱 출시** |

---

## 1. 기상청 '지금 날씨' — `docs/claude/weather_kma.py`

1. `docs/claude/weather_kma.py` 를 `server/weather_kma.py` 로 복사 (10-05 판: 하늘상태 조회만 실패하면 `code` 를 덮지 않는다)
2. `.env` 에 `KMA_SERVICE_KEY=<공공데이터포털 '기상청_단기예보 조회서비스' 일반 인증키(Decoding)>`
3. `/api/weather` 가 응답 dict(`out`)을 돌려주기 직전에
   ```python
   from weather_kma import apply_kma_now
   apply_kma_now(out, lat, lon)
   ```
4. 서버 재시작 → 확인: `python weather_kma.py` 가 `ok (57, 106)` · 비 오는 지역 좌표로 `/api/weather` 에 `pty`·`rn1` 이 오는지

동작 (파일 머리말과 같음): 실황(초단기실황)을 못 받으면 아무것도 안 바꾼다 · 하늘상태(초단기예보)만 못 받았고 비·눈이 없으면 `code` 는 Open-Meteo 값 그대로 (단 Open-Meteo 가 비·눈인데 실황이 '없음'이면 흐림 3) · 하늘상태를 못 받은 결과는 2분만 캐시.

## 2. `/api/weather` 습도

- 응답에 `humidity`(정수 %, 0~100). **null 로 보내지 말 것** — 값이 없으면 키를 빼면 앱이 줄을 숨긴다.
- 기상청 연동(1)을 하면 `REH` 로 자동으로 채워진다. 연동 전이면 Open-Meteo 요청의 `current=` 에 `relative_humidity_2m` 를 더해 `out['humidity'] = round(current['relative_humidity_2m'])`.

## 3. `upload_id` 중복 거르기 — `/upload` · `/create-location-with-model`

앱(v19)이 보내는 `upload_id`(32자 hex):
- 같은 내용(이름·분류·대표 사진/모델 파일 등)을 **다시** 보내면 같은 값 — 시간 초과·실패 뒤 '등록하기'를 다시 누른 경우. 앞 요청이 실제로는 저장됐을 수 있다.
- 내용이 바뀌거나(다른 장소) 성공·입력 초기화 뒤엔 새 값. (v18 은 실패 뒤 다른 장소에도 같은 값을 보냈다 — v19 에서 고침)
- 예전 앱은 `upload_id` 를 안 보낸다 → 없으면 지금처럼 그냥 저장.

서버가 할 일:
1. 테이블 (한 번만)
   ```sql
   CREATE TABLE IF NOT EXISTS upload_dedup (
       upload_id   VARCHAR(64) PRIMARY KEY,
       location_id INTEGER,                 -- 저장이 끝나면 채운다
       response    TEXT,                    -- 처음 성공 응답 본문 그대로
       created_at  TIMESTAMPTZ DEFAULT now()
   );
   ```
2. 요청 처리 (두 경로 같은 방식)
   ```python
   uid = (request.form.get('upload_id') or '').strip()[:64]
   if uid:
       cur.execute("INSERT INTO upload_dedup (upload_id) VALUES (%s) ON CONFLICT DO NOTHING RETURNING upload_id", (uid,))
       if cur.fetchone() is None:                      # 이미 받은 키
           cur.execute("SELECT response FROM upload_dedup WHERE upload_id=%s", (uid,))
           prev = cur.fetchone()[0]
           conn.commit()
           if prev:                                    # 처음 것이 성공했다 → 같은 성공 응답 (새로 저장하지 않음)
               return prev, 200
           return jsonify(error='in_progress'), 409    # 처음 것이 아직 처리 중 (같은 순간 두 번) — 앱은 실패로 보고 다시 누를 수 있다
       conn.commit()
   # ... 지금 저장 로직 그대로 ...
   # 성공 응답을 만든 뒤:
   if uid:
       cur.execute("UPDATE upload_dedup SET location_id=%s, response=%s WHERE upload_id=%s", (new_id, body, uid))
   # 저장이 실패(예외)하면 그 키 줄을 지운다 → 같은 키로 다시 보내면 새로 저장된다
   #   cur.execute("DELETE FROM upload_dedup WHERE upload_id=%s AND response IS NULL", (uid,))
   ```
   - 성공 응답은 지금과 **똑같은 본문·200** — 앱은 `"Upload Succeeded!"` 또는 200 을 성공으로 본다.
   - 오래된 줄 정리(선택): 30일 지난 줄 삭제 (`created_at < now() - interval '30 days'`).

## 4. TourAPI(관광공사) 키를 서버가 붙이기 — `/proxy` 🔴 보안

지금: 앱 코드에 관광공사 인증키가 평문으로 있었고(공개 저장소·앱 파일로 노출), 앱이 `serviceKey=` 를 붙여 `MAIN_SERVER/proxy/<API>?...` 로 보냈다.
v19 앱: **`serviceKey` 를 보내지 않는다**. 지하철·기차·터미널 매니저에 남아 있던 같은 키(안 쓰던 상수)도 지웠다.

서버가 할 일:
1. `.env` 에 `TOUR_API_KEY=<관광공사 인증키(Decoding)>` — 가능하면 **재발급한 새 키** (예전 키는 git 기록·배포된 앱 파일에 남아 있다)
2. `/proxy/<endpoint>` 처리에서 들어온 쿼리의 `serviceKey` 는 **지우고(있어도 무시)** `.env` 키를 붙여 보낸다
   ```python
   TOUR_ENDPOINTS = {'locationBasedList', 'detailImage', 'detailCommon', 'detailPetTour'}   # 앱이 쓰는 것만

   @app.route('/proxy/<endpoint>')
   def tour_proxy(endpoint):
       if endpoint not in TOUR_ENDPOINTS:
           return jsonify(error='not allowed'), 404
       key = os.getenv('TOUR_API_KEY')
       if not key:
           return jsonify(error='server not configured'), 503
       params = {k: v for k, v in request.args.items() if k.lower() != 'servicekey'}
       params['serviceKey'] = key                       # requests 가 알맞게 인코딩한다 (Decoding 키를 넣을 것)
       r = requests.get(f'{TOUR_BASE}/{endpoint}', params=params, timeout=8)   # TOUR_BASE = 지금 /proxy 가 쓰는 관광공사 주소 그대로
       return Response(r.content, status=r.status_code, content_type=r.headers.get('Content-Type', 'application/json'))
   ```
   - 지금 `/proxy` 가 엔드포인트 이름을 바꿔 부르고 있으면(예: `…2` 접미사) 그 부분은 그대로 둔다 — 바꾸는 건 **키를 어디서 붙이느냐**뿐.
   - 예전 앱은 계속 옛 키를 붙여 오지만 서버가 지우고 새 키를 쓰므로 **키를 재발급해도 예전 앱이 계속 동작**한다.
3. (권장) 같은 쿼리 10분 캐시 · 기기당 분당 호출 제한 — 키 한도(일 호출 수) 보호
4. **배포 순서: 서버 먼저.** 서버가 키를 붙이기 전에 v19 앱이 나가면 공공데이터(관광지) 오브젝트가 안 뜬다.
   확인: 브라우저로 `https://<서버>/proxy/locationBasedList?pageNo=1&numOfRows=5&mapX=126.978&mapY=37.5665&radius=1000&listYN=Y&arrange=A&MobileOS=ETC&MobileApp=AppTest&_type=json` (키 없이) → 관광지 JSON 이 오면 됨
5. 다 바뀐 뒤 공공데이터포털에서 **옛 키 폐기/재발급** (대표님 확인 후)
