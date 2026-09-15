# 웹앱 E2E 기능 테스트 가이드

> 웹앱/페이지의 전 기능을 **실제로 실행해서** 검증하는 절차.
> 사용자가 "전체 기능 테스트해줘 / 잘 작동하는지 검토해줘"라고 하면 이 문서대로 수행한다.
> 농민.com에서 실전 검증된 방법론 (2026-06: 총 123개 항목, 실버그 5건 발견·수정).

---

## 0. 원칙

1. **코드 리뷰 ≠ 테스트.** 코드만 읽고 "잘 될 것"이라 판단하지 말 것. 실제 HTTP 요청과
   실브라우저로 직접 실행해 응답 코드·내용·DB 상태를 확인한다.
2. **흐름**: 라우트 지도 → 시나리오 스크립트 작성 → 실행 → FAIL 분석 → 코드 수정 →
   서버 재시작 → **전체 회귀 재실행** → 테스트 데이터 정리 → 보고.
3. 테스트 데이터는 식별 가능한 접두사(`e2etest_`)로 만들고 **끝나면 반드시 DB에서 삭제**.
4. 비밀번호·API키는 `.env`에서 런타임에 읽는다. 스크립트에 하드코딩 금지.
5. 수정 후에는 고친 항목만이 아니라 **전체 스위트를 다시 돌려** 회귀를 확인한다.

---

## 1. 사전 준비

| 단계 | 방법 |
|------|------|
| 라우트 전체 지도 | `grep -n "@app.route(" server.py` (Flask) / 라우터 정의 파일 grep |
| 떠 있는 서버가 최신 코드인지 | **최근에 추가된 라우트**를 curl로 프로브 — 존재하면 200/302, 구버전이면 404 |
| 폼 필드명 파악 | 각 POST 라우트 구현부를 읽고 `request.form.get(...)` 키 수집 |
| 관리자 비번 등 | `.env`에서 읽기 (예: `NONGMIN_ADMIN_PW`) |
| Windows 콘솔 인코딩 | 스크립트 첫머리에 `sys.stdout.reconfigure(encoding='utf-8', errors='replace')` — cp949 크래시 방지 |

---

## 2. 테스트 카테고리 체크리스트

### Phase 1 — HTTP 기능 테스트 (requests)

| 카테고리 | 확인 항목 |
|----------|-----------|
| **A. 공개 페이지** | 메인·검색·약관 등 전 페이지 200 / 검색에 SQLi 프로브(`' OR 1=1--`) 넣어도 500 없음 / 없는 ID(`/product/999999`)는 404 or redirect (500 금지) |
| **B. 접근 통제** | 비로그인으로 판매자·관리자·마이페이지 전부 → redirect 확인 / 관리자 비번 오답 거부 / 세션쿠키 HttpOnly·SameSite |
| **C. 가입/로그인** | 필수값 누락·비번 정책 위반·비번확인 불일치·약관 미동의 각각 거부 / 정상 가입 → redirect+세션 / 중복 이메일 거부 / 로그아웃 후 차단 / 비번 변경(현재비번 오답 거부 → 변경 → 새 비번 재로그인) |
| **D. 콘텐츠 등록(판매자 등)** | 필수값 검증 / 사진 포함 multipart 등록 / 등록 직후 목록에 표시 + **심사중 배지** / 미승인 상태가 메인·검색에 안 나옴 |
| **E. 승인 게이트 (중요)** | **미승인·반려 콘텐츠를 직링크(`/product/<id>`)로 열어본다** — 공개면 결함. 본인·관리자 미리보기는 허용돼야 함 / 승인 후에만 공개·주문 가능 |
| **F. 주문/결제 흐름** | 주문폼 → 제출 → 완료페이지 / **재고 초과 주문 거부** / 재고 차감 확인 / 주문내역 표시 |
| **G. 상태 머신** | 허용되지 않은 상태 전환 차단(pending→done 등) / 필수 조건 강제(운송장 없이 배송중 전환 차단) / 취소 시 재고 원복 / 구매자 화면에 송장·배송조회 링크 |
| **H. 리뷰/댓글** | 자격 검증(배송완료만) / 중복 차단 / 등록 후 노출 |
| **I. 알림** | 행위 발생 시 상대방(판매자/구매자) 알림 생성 |
| **J. 권한 교차 침범** | 구매자 세션→판매자 페이지, 판매자 세션→관리자, **타인 리소스 ID로 수정 시도** 전부 차단 |

### Phase 2 — 디테일·경합 테스트 (requests)

| 항목 | 방법 |
|------|------|
| **같은 초 더블 제출** | 주문/등록을 sleep 없이 2연속 POST → **둘 다 저장**되고 ID가 달라야 함. 타임스탬프 기반 유니크 ID는 충돌해 한 건이 조용히 유실됨 (실제 발견 버그) |
| **캐시 헤더** | 개인정보 페이지 응답에 `Cache-Control: no-store` 있는지 / 정적 파일(css·이미지)은 no-store가 **아니어야** 함 |
| **완료 랜딩페이지 내용** | 주문번호·상품명·금액·받는분 표시 / 버튼이 의미 있게 분기(로그인: 주문내역 보기, 비회원: 계속 쇼핑) / 가짜 ID로 접근해도 500 없음 |
| **redirect 파라미터 처리** | `?m=signup` 같은 완료 메시지 파라미터를 **받는 쪽 템플릿이 실제로 처리하는지** (안내 없이 무시되면 결함) |
| **실패 시 흐름** | 저장 실패(except) 시 성공 화면으로 보내지 않고 에러 안내로 돌려보내는지 |
| **PRG 전수 점검** | 모든 POST 라우트의 성공 경로가 `redirect()`로 끝나는지 grep으로 전수 확인 (JSON API 제외) — 새로고침 중복 제출 방지 |

### Phase 3 — 실브라우저 테스트 (selenium + Edge headless)

| 항목 | 확인 |
|------|------|
| 가입 → 환영 토스트 | 토스트 실표시 + `history.replaceState`로 URL 정리 → **새로고침 시 재표시 없음** |
| 완료페이지 F5 | "다시 제출하시겠습니까?" 경고 없이 같은 페이지, **중복 데이터 생성 없음** |
| 뒤로/앞으로 가기 | 완료→뒤로→폼→앞으로→완료, 검색→상세→뒤로→검색결과 유지, 전 구간 에러 없음 |
| **로그아웃 후 뒤로가기** | 개인정보 페이지(주문내역 등)가 캐시에서 보이면 결함 → no-store로 해결 |
| 폼 실제 제출 | 실브라우저로 폼 채워 제출 → 완료페이지 도달 |

### Phase 4 — 전수 링크 크롤 + 앱(웹뷰) 변환 호환성

사이트를 Capacitor 웹뷰 앱으로 감쌀 때(농민.com 앱 등) 반드시 추가 점검:

| 항목 | 방법·기준 |
|------|-----------|
| **전수 링크 크롤** | 4개 역할 세션(비로그인/구매자/판매자/관리자)으로 모든 `<a href>`를 재귀 방문 → 404/500 사냥. **form action은 POST 전용이라 GET 방문하면 405 오탐** — href만 방문 |
| **경로 프리픽스 일관성** | 앱은 `woopang.com/<prefix>`로 접속 → `/<prefix>`로 시작해 크롤하며 프리픽스 이탈 링크 검출 + 서버 `redirect('/...')` 중 prefix 변수 안 쓴 곳 grep + 템플릿 JS의 `fetch('/`·`location.href='/` 중 `{{ base }}` 없는 곳 grep |
| **가로 오버플로우** | **모바일 기준 폭 422px**(대표님 실기기 아이폰 실측 뷰포트, 2026-07)에서 우선 검사 후 390·375·360px까지 스윕 — 주요 페이지 열어 `scrollWidth > clientWidth` == 0 확인 |
| viewport 메타 | standalone 템플릿(`{% extends` 없는 것) 전수에 viewport 존재 확인 |
| **팝업류 API** | `daum.Postcode(...).open()` 등 **팝업창 방식은 웹뷰에서 안 열림** → `.embed(레이어)` 방식으로 교체 (실제 사례: 주소검색이 앱에서 먹통 → 주문 불가였을 결함) |
| **소셜 로그인** | **구글 OAuth는 웹뷰에서 정책 차단**(403 disallowed_useragent) → `window.Capacitor` 감지 시 구글 버튼 숨김+안내 (카카오는 웹뷰 허용). 테스트는 CDP `Page.addScriptToEvaluateOnNewDocument`로 `window.Capacitor={}` 주입해 시뮬레이션 |
| target=_blank / window.open | 인벤토리 조사 — 외부 사이트(택배조회 등)는 외부 브라우저로 열리는 게 정상, 내부 페이지 _blank는 UX 저하 검토 |
| mixed content | `src="http://` grep — HTTPS 페이지에서 http 리소스는 차단됨 |
| **앱 권한 매니페스트** | 사이트 기능에 필요한 권한이 앱 AndroidManifest에 있는지: 사진업로드→CAMERA, 라이브방송(WebRTC)→RECORD_AUDIO·MODIFY_AUDIO_SETTINGS, 푸시→POST_NOTIFICATIONS |

---

## 3. 자주 발견되는 버그 패턴 (실제 사례)

이번 농민.com 테스트에서 **모두 실제로 발견된** 패턴. 새 앱 테스트 시 우선 의심할 것:

1. **승인 게이트 직링크 우회** — 목록 쿼리에는 `status='active'` 필터가 있지만
   단건 조회(`fetch_product`)에는 없어서 미승인·반려 콘텐츠가 URL 직접 입력으로 열림.
   ID가 연속 정수면 추측도 쉬움. → 단건 조회 라우트에 상태 검사 추가 (본인·관리자는 미리보기 허용).
2. **타임스탬프 유니크 ID 충돌** — `'NM' + strftime('%y%m%d%H%M%S')` 같은 초 단위 ID에
   UNIQUE 제약 → 같은 초 두 요청 중 하나가 INSERT 실패로 조용히 유실. → 랜덤 서픽스
   (`secrets.token_hex(2)`) 부가.
3. **except 후 성공 화면 redirect** — 저장 실패를 잡고도 완료 페이지로 보내 사용자가
   "접수됐다"고 착각. → 실패 시 입력폼으로 `?err=` redirect + 안내 메시지.
4. **캐시 헤더 부재** — 로그아웃 후 뒤로가기로 개인정보 노출(공용 PC 위험).
   → `after_request`로 HTML 응답에 `no-store` (정적 파일 제외).
5. **redirect 파라미터 미처리** — 가입 후 `/?m=signup`으로 보내는데 메인이 `m`을 무시 →
   완료 피드백 없음. → 받는 쪽에 토스트 추가 + URL 정리.
6. **완료페이지 동선 부실** — 버튼 2개가 같은 곳으로 가는 등. 다음 자연 동선
   (주문내역 보기)으로 분기.

---

## 4. 스크립트 작성 요령

### 구조
```python
import requests, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')   # Windows 필수

pub    = requests.Session()   # 비로그인
buyer  = requests.Session()   # 역할별 세션 분리 — 권한 교차 테스트의 핵심
seller = requests.Session()
admin  = requests.Session()

results = []
def check(name, ok, detail=''):
    results.append((name, bool(ok)))
    print(f"[{'PASS' if ok else 'FAIL'}] {name}" + (f'  -- {detail}' if detail and not ok else ''))
```
- redirect 검증은 `allow_redirects=False`로 `status_code` + `Location` 헤더 확인.
- 파일 업로드는 1×1 PNG base64를 디코드해 `files={'photos': ('a.png', img, 'image/png')}`.
- 마지막에 PASS/FAIL 집계 출력.

### PRG 전수 점검 (POST 라우트가 redirect로 끝나는지)
```python
funcs = re.findall(r"@app\.route\('([^']+)'[^)]*POST[^)]*\)\s*(?:@app\.route\([^)]+\)\s*)?(?:@\w+\s*)*def (\w+)", src)
# 각 함수 본문에 redirect( 존재 확인 — 없으면 JSON API인지 직접 판단
```

### selenium (Edge headless) 팁
- `Options()`에 `--headless=new --window-size=1280,900` / driver는 selenium-manager가 자동 다운로드.
- **토스트 등 애니메이션 요소는 `.text`가 빈 값** (가시성 판정) → `get_attribute('textContent')` 사용.
- **readonly 필드**(우편번호 등)는 `send_keys` 불가 → `execute_script`로 value 주입.
- onsubmit 가로채기 있는 폼은 `HTMLFormElement.prototype.submit.call(form)`으로 제출.
- 페이지 전환 후 `time.sleep(0.8~1.5)` 필요.

### 테스트 데이터 정리 (cleanup)
- 가입 이메일을 `e2etest_%` LIKE로 찾아 **FK 역순으로 삭제**:
  리뷰 → 알림 → 토큰 → 카드 → 주문 → 옵션 → 상품 → 신청서 → 계정.
- DB 접속 정보는 서버 코드와 동일하게 (".env의 DB_NAME이 실제와 다를 수 있음" —
  서버 소스의 `DB_CONFIG`를 먼저 확인). `os.environ['PGCLIENTENCODING']='UTF8'` 설정
  (한글 Windows에서 psycopg2 에러 메시지 디코드 크래시 방지).
- **테스트 전후로 실행** (이전 잔여 데이터로 중복가입 거부 등 오탐 방지).

### 서버 재시작 (수정 검증 시)
```powershell
# 포트 점유 프로세스 확인 → 종료 → 분리된 프로세스로 재기동
(Get-NetTCPConnection -LocalPort 6688 -State Listen).OwningProcess
Stop-Process -Id <pid> -Force
Start-Process python -ArgumentList "-m","waitress","--host=0.0.0.0","--port=6688","--threads=10","nongmin_server:app" -WorkingDirectory "C:\woopang\server\nongmin" -WindowStyle Minimized
```
- 재시작 전 `python -m py_compile` + Jinja 템플릿 파싱 검증 필수.
- 보고 시 "내가 별도 창으로 재기동했으니 평소처럼 bat로 켜도 된다"고 안내.

---

## 5. 농민.com 기준 — 바로 실행

스크립트 위치: `C:\woopang\server\nongmin\tests\`

```bash
cd C:\woopang\server\nongmin\tests
python nongmin_e2e_cleanup.py     # 잔여 테스트 데이터 정리 (전후 실행)
python nongmin_e2e.py             # Phase 1: 85항목 (가입~주문~운송장~리뷰~보안)
python nongmin_phase2.py          # Phase 2: 21항목 (더블제출·캐시·완료페이지)
python nongmin_browser_test.py    # Phase 3: 17항목 (실브라우저 뒤로가기/새로고침)
python nongmin_crawler.py         # Phase 4a: 전수 링크 크롤 (4역할) + 프리픽스 감사
python nongmin_mobile_test.py     # Phase 4b: 모바일 422px(기준)·375px + 웹뷰(Capacitor) 시뮬레이션
python nongmin_e2e_cleanup.py     # 최종 정리
```

- 대상: `http://127.0.0.1:6688` (떠 있어야 함)
- 새 기능을 추가했으면 **해당 시나리오를 e2e 스크립트에 추가**한 뒤 전체를 돌린다.
- 다른 앱에 적용할 때는 이 스크립트들을 복사해 라우트·필드명만 교체하면 됨.

---

*작성: 2026-06-13 — 농민.com 전 기능 테스트(123항목, 버그 5건 수정) 실전 결과를 일반화.*
