# 기획안 / 제안서 (HTML 페이퍼) 작업 가이드

> 기획안, 제안서, HTML 페이퍼 작성 시 참조

---

## 기본 원칙
```
- 모든 기획안/제안서는 단일 HTML 파일로 작성 (외부 의존성 최소화)
- 최종 산출물은 PDF 배포 → 인쇄 시 레이아웃이 깨지지 않아야 함
- 서버 라우팅도 함께 등록하여 웹에서도 열람 가능하게 (woopang.com/경로)
- 파일 위치: documents/ 폴더
```

## 디자인 톤
```
- 기획안별 컨셉에 맞는 색감 사용 (다크/라이트 자유)
- 폰트: Noto Sans KR (웹), 다국어 지원 필수
- 분량: 스와핑게임 기획안 수준 (10+ 섹션, 에피소드 20개, 수익/제작/글로벌 전략 포함)
- 부족한 내용은 인터넷 검색으로 트렌드/데이터 보충하여 채울 것
```

---

## PDF 인쇄 대응 (필수)
```
⚠️ 중요: @media print 스타일 반드시 포함

필수 규칙:
- 다크 배경 유지: -webkit-print-color-adjust: exact; print-color-adjust: exact;
- 애니메이션 비활성: animation: none !important;
- 그림자/글로우 단순화 (인쇄 시 깨짐 방지)
- page-break-inside: avoid; (섹션 중간 잘림 방지)
- page-break-before: always; (주요 섹션마다 페이지 넘김)
- 배경색/배경이미지 인쇄 허용 설정 포함

@media print 기본 템플릿:
@media print {
    * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }
    body { background: #0a0a0a !important; }
    .section { page-break-inside: avoid; page-break-before: always; }
    .animated, .particle, .shimmer { animation: none !important; display: none !important; }
    .no-print { display: none !important; }
}
```

---

## 다국어 지원 (필수)
```
⚠️ 중요: 글로벌 OTT/플랫폼 대상 제안서는 반드시 다국어 전환 포함

기본 언어 세트: 한국어(KO) / 영어(EN) / 일본어(JA) / 중국어(ZH) / 스페인어(ES)

구현 방식:
- 상단 중앙에 언어 선택 버튼 배치
- data-lang 속성으로 텍스트 관리
- JavaScript로 즉시 전환 (페이지 리로드 X)
- 기본값: 영어 (EN) — 글로벌 플랫폼 대상이므로

예시:
<span data-lang="ko">프로그램 개요</span>
<span data-lang="en">Program Overview</span>
<span data-lang="ja">番組概要</span>
```

---

## 시각 효과 (웹 전용, 인쇄 시 비활성)
```
웹 열람 시 적용할 효과:
- 계절감 파티클 (꽃잎, 눈, 낙엽 등 — CSS/JS 애니메이션)
- 배경 쉬머/펄스 효과 (은은한 빛 움직임)
- 스크롤 연동 페이드인 (IntersectionObserver)
- 호버 시 카드 글로우 효과

모든 효과에 .animated 또는 .web-only 클래스 부여
→ @media print에서 일괄 숨김 처리
```

---

## 파비콘 (자동 생성 필수)
```
⚠️ 기획안 작업 시 반드시 아래 3곳 모두 처리:

1. SVG 파비콘 파일 생성
   - 파일명: {프로젝트명}_favicon.svg
   - 위치: documents/ 폴더 (HTML과 같은 경로)
   - 디자인: 기획안 분위기/색감에 맞는 심볼 + 그라디언트 + 글로우

2. HTML <head>에 연결
   <link rel="icon" type="image/svg+xml" href="{프로젝트명}_favicon.svg">

3. 서버 라우트 등록 (app_improved.py)
   @app.route('/plan/{프로젝트명}_favicon.svg')
   @app.route('/{프로젝트명}_favicon.svg')  ← 레거시 경로 호환
   def {프로젝트명}_favicon():
       return send_from_directory(r'C:\woopang\documents', '{프로젝트명}_favicon.svg', mimetype='image/svg+xml')

4. 북마크 페이지 파비콘 매핑 (server/bookmark/bookmark.html — documents/ 아님 주의)
   getFaviconUrl() 함수 내 planFavicons 객체에 추가:
   '{프로젝트명}': '/plan/{프로젝트명}_favicon.svg'
```

---

## 서버 라우팅 연동 (필독 — app_improved.py 라우팅 구조)
```
기획안 접속 경로: /plan/{프로젝트명}
기획안 모음("기획안 대잔치") 인덱스: /plan  (= woopang.com/plan)

⚠️ 라우팅 코드 위치: C:\woopang\server\app_improved.py 의 "기획안" 섹션
   (`# 기획안 인덱스 페이지` ~ `# AI 워커가 생성한 기획안 자동 서빙` 주석 구간)

────────────────────────────────────────────────────
[ A ] "기획안 대잔치" = /plan 인덱스 페이지
────────────────────────────────────────────────────
- 라우트: @app.route('/plan') → documents/plan_index.html
- 이 페이지가 모든 기획안을 카드로 모아놓은 곳 (<title>기획안 대잔치 — QUE. ENT</title>)
- 새 기획안을 만들면 여기에 카드를 추가해야 /plan 목록에 노출됨

────────────────────────────────────────────────────
[ B ] AI 자동 서빙 (catch-all) — 명시 라우트 없이도 동작
────────────────────────────────────────────────────
app_improved.py 하단에 catch-all 라우트가 이미 있음:

  @app.route('/plan/<plan_name>')        → documents/{name}_proposal.html → 없으면 {name}.html
  @app.route('/plan/<plan_name>_favicon.svg') → documents/{name}_favicon.svg
  @app.route('/plan/img/<filename>')     → documents/img/{filename}

➡️ 따라서 네이밍 규칙만 지키면 app_improved.py 를 수정하지 않아도 자동 서빙됨:
   - HTML:   documents/{프로젝트명}_proposal.html
   - 파비콘: documents/{프로젝트명}_favicon.svg
   - 이미지: documents/img/{파일명}
   이 규칙대로 두면 /plan/{프로젝트명} 으로 바로 열린다 (별도 라우트 등록 불필요).

⚠️ 단, 경로가 /plan/{name} 형태가 아니거나(예: /polar_marathon, /minsickssem),
   파일명이 _proposal.html / .html 규칙을 벗어나거나, EP별 OG 메타 동적 치환 등
   특수 로직이 필요하면 → 그때만 app_improved.py 에 명시 라우트를 추가한다.
   (기존 swapping/dogting/3chon 등은 명시 라우트로 등록돼 있으나, 신규는 catch-all 로 충분)

────────────────────────────────────────────────────
[ C ] 신규 기획안 추가 시 실제로 해야 할 일 (최소 셋업)
────────────────────────────────────────────────────
1. documents/{name}_proposal.html  생성  (필수)
2. documents/{name}_favicon.svg    생성  (필수)
3. documents/img/                  이미지 배치 (있으면)
4. plan_index.html 에 카드 추가     (필수 — 이거 빠지면 /plan 목록에 안 보임)
5. server/bookmark/bookmark.html 의 planFavicons 에 '{name}': '/plan/{name}_favicon.svg' 추가 (선택)
   → 1·2·3·4 만 하면 app_improved.py 손 안 대도 /plan/{name} 로 열림

⚠️ 중요: plan_index.html (woopang.com/plan) 카드 등록은 절대 빠뜨리지 말 것
   - 기획안을 만들었는데 /plan 목록에 안 보이면 의미 없음
   - 카드 스타일(.card-{프로젝트명})도 CSS에 추가
   - 그라데이션 색상은 기획안 톤에 맞게 설정

plan_index.html 카드 추가 위치:
- </section> 바로 위에 새 <a> 카드 추가
- 카드 구조: .card-accent + .card-body(.card-badge + .card-title + .card-desc) + .card-footer

현재 등록된 기획안 (plan_index.html 카드 기준):
- /plan/swapping         → swapping_game_proposal.html (스와핑게임)
- /plan/dogting          → dogting_proposal.html (독팅)
- /plan/3chon            → 3chon_proposal.html (삼촌교육대)
- /plan/yumddajudge      → yumdda_proposal.html (염따대왕)
- /plan/works            → works_proposal.html (AI Works 투자유치)
- /plan/younghero        → younghero_proposal.html (영웅 프로젝트)
- /plan/<파트너>_contents → 콘텐츠 파트너 제안서 (슬러그·상세는 로컬 전용 docs/private/)
- /plan/twentyclass      → twentyclass_proposal.html (Twenty Class)
- /plan/angrylawyer      → angrylawyer_proposal.html (성난 변호사)
- /plan/nongmin          → nongmin_proposal.html (농민닷컴 — 농어민 1% 직거래·라이브커머스)
- /plan/nongmin_pitch    → nongmin_pitch_proposal.html (농민닷컴 IR 피치덱 — Seed 라운드 10페이지)
- /polar_marathon        → polar_marathon_proposal.html  (출연자 섭외용 — 명시 라우트)
- /north_marathon        → north_marathon_proposal.html  (후원사 섭외용 — 명시 라우트)
```

---

## ⚠️ 흔한 실수 (재발 방지)

```
❌ 절대 하지 말 것:
- 기획안 HTML을 C:\woopang\guide\ 하위에 만들기 (라우팅 안 됨)
- "기획안 대잔치" 라는 폴더를 새로 만들기 ("기획안 대잔치"는 폴더가 아니라 /plan 페이지의 타이틀)
- plan_index.html 카드 추가 없이 documents/ 에 파일만 두기 (목록 노출 안 됨)

✅ 올바른 위치 단 하나:
- HTML / 파비콘 / 이미지 모두 → C:\woopang\documents\ (또는 documents/img/)

✅ 신규 기획안 추가 시 체크리스트 (이 순서대로):
[ ] 1. C:\woopang\documents\{name}_proposal.html  생성 (모바일 가로스크롤 가드 포함)
[ ] 2. C:\woopang\documents\{name}_favicon.svg    생성 (해당 기획안 톤에 맞는 디자인)
[ ] 3. plan_index.html <style> 에 .card-{name} CSS 추가 (accent·hover·badge·title 4개)
[ ] 4. plan_index.html <section class="cards"> 맨 아래 또는 적절한 위치에 카드 추가
       (5개 언어 라벨 모두 — KO/EN/JA/ZH/ES)
[ ] 5. (선택) server/bookmark/bookmark.html planFavicons 에 '{name}': '/plan/{name}_favicon.svg' 추가
[ ] 6. 서버 재시작 불필요 — catch-all 라우트가 자동 서빙
       (단, app_improved.py 에 명시 라우트를 새로 추가했다면 재시작 필수)

서버 재시작이 필요한 경우 / 불필요한 경우:
- 정적 HTML/SVG/이미지 파일 추가·수정 → ❌ 재시작 불필요
- plan_index.html 카드 추가 → ❌ 재시작 불필요
- app_improved.py 에 새 @app.route 추가 → ✅ 재시작 필수
- app_improved.py 기존 라우트 로직 변경 → ✅ 재시작 필수
```

---

## 카드 → 모달 인터랙션 패턴 (필수)
```
⚠️ 기획안 내 콘텐츠 카드(예: '아린의 댄스공장', 'Twenty Class', '고향에서 온 택배' 등)는
   클릭 시 상세 모달이 열리는 구조로 통일

기본 구조:
1. 카드(.q-chip / .mp-card / .strategy-card 등)에 onclick="openModal('id')" 부착
2. 같은 페이지 하단에 <div class="modal-overlay" id="modal-id">...</div> 정의
3. 공통 JS:
   function openModal(id){document.getElementById('modal-'+id).classList.add('open');document.body.style.overflow='hidden';}
   function closeModal(e){if(e.target.classList.contains('modal-overlay')){e.target.classList.remove('open');document.body.style.overflow='';}}
   function closeModalById(id){document.getElementById(id).classList.remove('open');document.body.style.overflow='';}
   document.addEventListener('keydown',e=>{
     if(e.key==='Escape'){
       document.querySelectorAll('.modal-overlay.open').forEach(m=>m.classList.remove('open'));
       document.body.style.overflow='';
     }
   });

모달 안에 들어가야 할 요소(권장):
- 모달 번호(.modal-num) + 제목(.modal-title-block h2) + 부제(.modal-subtitle)
- 기획의도 / 예상 에피소드 / 인터뷰 근거 / 멤버 분석(member-profile) / 킬링포인트
- 닫기 버튼 — 우상단 X + 오버레이 클릭 + ESC 키 3가지 모두 지원

같은 카드를 여러 섹션(콘텐츠 후보군 / 콘텐츠 운영 등)에서 재사용할 때:
- 모달 ID는 한 번만 정의, openModal('id') 호출로만 재사용
- 카드는 .strategy-clickable 등 hover 효과 클래스 부착 + cursor:pointer

유튜브 등 외부 영상 모달:
- iframe은 open 시 동적으로 src 삽입, close 시 innerHTML='' 으로 정지
- ESC 키 핸들러에도 영상 정지 함수 호출 추가
```

---

## 모바일 가로 스크롤 절대 금지 (필수)
```
⚠️ 모든 기획안/제안서 HTML — 모바일(특히 핸드폰)에서
   좌우로 드래그·스와이프해도 절대 가로 스크롤이 발생하지 않도록 작성

전역 가드 (style 최상단):
html { overflow-x: hidden; max-width: 100%; }
body { overflow-x: hidden; max-width: 100%; width: 100%; }
*, *::before, *::after { box-sizing: border-box; }
img, iframe, video, svg { max-width: 100%; height: auto; }
.section, .hero, .footer { max-width: 100vw; }

흔한 가로 스크롤 유발 원인 (작성 중 점검):
1. white-space:nowrap 텍스트 — 한국어 긴 어구가 모바일에서 폭 초과
   → 모바일 미디어쿼리에서 white-space:normal + word-break:keep-all + overflow-wrap:break-word 강제
2. <strong> 태그가 감싼 한국어 어구가 끊기지 못해 가로 초과
   → word-break:keep-all 적용
3. 절대 위치(position:absolute) chip이 부모 폭을 넘어 배치
   → 부모에 overflow:hidden 추가
4. 큰 padding(좌우 32px+) 카드/박스
   → 모바일에서 12~14px로 축소
5. 4열·5열 grid가 모바일에서 그대로 유지
   → repeat(2,1fr) 또는 1fr로 강제 변경
6. <table> 가로폭 초과
   → table-layout:fixed; width:100%; word-break:break-all; 강제
7. iframe·이미지 고정 폭
   → max-width:100%; height:auto; 전역 적용
8. ::before / ::after 장식용 큰 글리프 (따옴표 \201C, \201D / 화살표 / 별표 등)
   → 데스크탑에서 font-size 100px+ 글리프를 left:-Npx 처럼 박스 바깥으로 빼는 디자인은 OK
   → 그러나 모바일에서 그대로 두면 viewport 좌·우 경계를 침범 → 좌우 잘림 / 가로 스크롤 발생
   → 모바일 미디어쿼리에서 반드시 다음 4가지 처리:
     ① font-size를 박스 폭에 맞춰 축소 (데스크탑 108px → 모바일 52px → 폰 40px)
        — 글리프가 박스 밖으로 살짝 나가는 디자인은 유지 (박스 안에 가두지 않아도 됨)
     ② left/right 음수 옵셋은 -2~-4px 정도의 작은 값으로만 유지
        — -6px 이상 큰 음수는 폰 화면 침범 위험
     ③ 상하 따옴표 사이즈는 ::before와 ::after를 항상 동일하게 — 한쪽만 줄이는 실수 금지
     ④ 박스 자체에 overflow:hidden 은 절대 적용하지 말 것 — 디자인 의도(박스 바깥 따옴표) 깨짐
        대신 부모 컨테이너(.hero / body / html)에 overflow-x:hidden 으로 viewport 가드
   → 부모가 max-width:920px 같은 좁은 컨테이너 안에 있어도
     viewport 좌·우 끝까지 펼쳐지는 띠형 요소가 가로 스크롤을 만드는 흔한 원인이므로
     html / body 레벨의 overflow-x:hidden 가드는 항상 유지

미디어쿼리 우선순위 함정:
- @media (max-width:780px) 규칙은 480px 화면에도 적용됨
- 작성 순서가 480px → 780px 순으로 되어 있으면, 480px 화면에서
  나중에 등장한 780px 규칙이 480px 규칙을 덮어씌움
- 해결: 480px 강제 규칙은 파일 가장 마지막에 두거나 !important 사용

권장 미디어쿼리 구조:
@media (max-width:900px) { ... 일반 태블릿 }
@media (max-width:780px) { ... 통합 모바일 가드 — 신규 요소 1열화/축소 }
@media (max-width:480px) { ... 작은 폰 강제 (!important로 우선 적용) }
@media (max-width:380px) { ... 초소형 폰 미세 조정 }

테스트 체크리스트:
[ ] iPhone SE(375px) 폭에서 좌우 스와이프 시 화면 이동 없는가?
[ ] hero 영역 텍스트가 박스 밖으로 잘리지 않는가?
[ ] 표(table) 가 화면 폭 안에 들어오는가?
[ ] 카드/그리드가 1열로 자연스럽게 떨어지는가?
[ ] 절대 위치 요소(chip, badge)가 화면 밖으로 나가지 않는가?
```

---

## 마퀴 / 무한 슬라이드 / 캐러셀 — 절대 다른 컨테이너에 합치지 말 것 (필수)
```
⚠️ 가장 빈번하게 반복된 실수 — 같은 문제 여러 번 발생함. 다시는 같은 실수 금지

문제 패턴:
- white-space:nowrap + width:max-content + display:flex 의 가로 슬라이드 요소
  (댓글 마퀴, 로고 띠, 무한 캐러셀, 하트 마퀴 등)를 hero 안 hero-content 형제로,
  또는 max-width 제한이 걸린 좁은 컨테이너 안 자식으로 넣으면
  → 자식의 본질 폭(intrinsic min-content) 5000px+ 이 부모 flex item의 min-width:auto 를
    그만큼 키워, 부모 컨테이너(hero-content max-width:920px 등)가 viewport 폭을 무시하고
    가로로 늘어나버림
  → 결과: 같은 부모 안의 다른 박스(hero-philosophy 등)도 끌려 늘어나
    모바일에서 좌우가 viewport 밖으로 잘려 보임

❌ 절대 하지 말 것:
- 가로 슬라이드 요소를 max-width 제한이 있는 카드·hero-content·grid item 안에 자식으로 박지 말 것
- 시각 위치 맞추려고 텍스트 카드와 마퀴를 같은 부모로 묶지 말 것
- calc(50% - 50vw) 같은 negative margin 트릭으로 viewport 폭으로 확장하는 방식도 금지
  (부모 layout 계산을 망가뜨려 형제 박스까지 viewport 폭이 됨)

✅ 올바른 방식 — 가로 슬라이드는 무조건 "별도 컨테이너 / 별도 섹션"으로 분리:

방식 1: 섹션 사이에 독립 띠로 배치 (권장)
  </section>
  <div class="comment-marquee-strip">    ← section 형제, max-width 제한 없는 풀 폭 띠
    <div class="comment-marquee">...</div>
  </div>
  <section>...

방식 2: 같은 섹션 안에 두되, 마퀴를 별도 row(섹션 직속 자식)로 분리
  <section class="hero">
    <div class="hero-content">...텍스트 카드들...</div>
    <div class="comment-marquee-row">    ← hero-content 와 형제, hero 직속 자식
      <div class="comment-marquee">...</div>
    </div>
  </section>
  + .hero { flex-direction:column; }   ← hero-content 와 마퀴를 세로로 쌓음

방식 3 (제한된 경우): 부득이 좁은 컨테이너 안에 둬야 하면 가로 폭 봉인 필수
  .marquee-wrap {
    overflow:hidden;
    width:100%; max-width:100%; min-width:0;
    contain:size;          ← 자식 max-content 가 부모 layout 에 영향 못 미치게 차단
    height:48px;           ← contain:size 사용 시 명시적 height 필수
  }
  + 부모 flex item 에 min-width:0; width:100% 추가

설계 원칙:
- 카드 사이 시각 위치를 맞추기 위해 같은 컨테이너로 묶을 필요 없음
- 마퀴는 시각적으로 두 카드 사이에 보이기만 하면 됨 — DOM 계층은 분리해도 됨
- 의심스러우면 무조건 방식 1(섹션 형제로 분리) 채택. 가장 안전·단순

체크리스트 (마퀴 / 가로 슬라이드 요소 추가 시):
[ ] 부모가 max-width 제한 있는 카드/hero-content/grid item 인가? → YES면 방식 1 또는 2 로 분리
[ ] 부모가 display:flex 인가? → YES면 마퀴 자체에 contain:size + 부모에 min-width:0 필수
[ ] calc(50% - 50vw) negative margin 사용 중인가? → 즉시 제거. 부모 layout 망가뜨림
[ ] 모바일에서 형제 박스(hero-philosophy 등)가 좌우로 잘리는가? → 마퀴 분리 즉시 시도

이 문제는 이미 두 번 이상 반복되었으므로, 마퀴/캐러셀 추가 작업 시
"같은 컨테이너 안 자식으로 박는 방식"은 처음부터 고려조차 하지 말 것.
```

---

## 섹션별 파티클 시스템 (배경 효과)
```
⚠️ 전체 화면 고정 canvas 1개 방식은 실제로 안 보이는 경우 多
   → 각 섹션에 개별 <canvas>를 absolute로 삽입하는 방식으로 통일

구현 원칙:
- 밝은 배경(section-light, section-white): 연파랑/하늘/중파랑 2-3색 계열 먼지
- 어두운 배경(hero, dark-wrap, quote-banner, cta): 흰색/연파랑/민트 별
- 각 섹션에 position:relative + overflow:hidden 필수 (canvas 삐져나옴 방지)
- IntersectionObserver로 화면 밖 섹션 RAF 정지 → 성능 확보

파티클 가시성 설정 (너무 옅으면 안 보임):
- baseAlpha: dark 0.25~0.85 / light 0.18~0.63
- 밀도: dark 1300px²당 1개 / light 1000px²당 1개 (최대 420/560개)
- 크기: dark 0.8~3.0px / light 1.2~4.2px
- 반짝임: 단일 사인파 대신 두 사인파 합산 → 불규칙하고 자연스러운 점멸

배치 위치 (카드 산만함 방지):
- 80% 확률로 좌우 22% 가장자리 영역에만 생성
- 중앙부(22%~78%)는 20% 확률만 허용
- 생성 함수 edgeX() 로 분리해서 재사용

CSS 클래스:
.particle-canvas-light { position:absolute; inset:0; width:100%; height:100%; pointer-events:none; z-index:0; }
.particle-canvas-dark  { position:absolute; inset:0; width:100%; height:100%; pointer-events:none; z-index:0; }
```

---

## 모바일 전화 연결 모달 패턴
```
⚠️ 제안서 내 '파트너십 문의' 등 CTA 버튼 처리 방식:
   - 데스크탑(769px+): 버튼 자체를 display:none (모바일 전용)
   - 모바일(768px 이하): 버튼 탭 → 하단 슬라이드업 전화 모달

CSS:
#partnerBtn { display:none; }
@media(max-width:768px) { #partnerBtn { display:inline-block; } }

모달 구조:
<div id="phoneModal">
  <div class="phone-modal-bg" id="phoneModalBg"></div>  ← 배경 탭으로 닫기
  <div class="phone-modal-box">
    <h3>파트너십 문의</h3>
    <span class="phone-num">+82 10-XXXX-XXXX</span>
    <a href="tel:+8210XXXXXXXX" class="phone-modal-call">전화 연결</a>
    <button class="phone-modal-cancel" id="phoneModalCancel">닫기</button>
  </div>
</div>

JS 패턴:
- 버튼 클릭 → modal.classList.add('open') + body overflow:hidden
- 배경/닫기/ESC → classList.remove('open') + body overflow:''
- tel: 링크는 전화번호에서 하이픈·공백 모두 제거: +821053795655
```

---

## 외부 링크 카드 인터랙션 패턴
```
⚠️ 인물 소개·SNS·저서 등 외부 링크를 카드로 표시할 때
   "클릭 가능하다"는 것을 시각적으로 명확히 전달해야 함

핵심 원칙 (색/모양은 페이지 톤앤매너에 맞게 그때그때 결정):
- 오버레이 색상: 페이지 포인트 컬러 기반으로 선택 (민트, 골드, 레드 등)
- 호버 표시 기호: ▲ / ↗ / → / ✦ 등 분위기에 맞게 선택
- 오버레이 불투명도: 너무 진하면 텍스트 안 보임 → 0.12~0.22 범위 권장
- 기호 위치: 중앙 상단 / 우측 상단 / 우측 중앙 등 레이아웃에 맞게 결정

CSS 구조 (색·기호만 바꿔서 재사용):
.ext-link {
  position:relative; overflow:hidden; text-decoration:none;
  transition:transform .28s ease;
}
/* 컬러 오버레이 레이어 */
.ext-link::before {
  content:''; position:absolute; inset:0;
  background:rgba(R,G,B,0);           ← 페이지 포인트 컬러로 교체
  transition:background .28s ease; pointer-events:none; z-index:0;
}
/* 호버 표시 기호 */
.ext-link::after {
  content:'기호';                      ← ▲ / ↗ / → 등 페이지 톤에 맞게
  position:absolute; left:50%; top:6px;
  transform:translateX(-50%) translateY(-10px);
  font-size:15px; opacity:0;
  transition:opacity .24s ease, transform .24s ease;
  pointer-events:none; z-index:1;
}
.ext-link > * { position:relative; z-index:1; }
.ext-link:hover { transform:translateY(-2px); }
.ext-link:hover::before { background:rgba(R,G,B,.18); }
.ext-link:hover::after { opacity:1; transform:translateX(-50%) translateY(0); }

변형 클래스 패턴 (배경 톤에 따라 분리):
- .ext-link-dark: 다크 배경 카드용 (SNS 목록, 채널 카드 등)
- .ext-link-light: 밝은 배경 카드용 (저서, 링크 목록 등)
- .ext-link-block: display:block 블록형 링크용 (강연·콘텐츠 전체 카드)

적용 필수 속성 (색/기호와 무관하게 항상 동일):
- target="_blank" rel="noopener"
- <a> 태그 기본 보라색 방지: color:inherit 명시
- 내부 자식 요소에 position:relative; z-index:1 (오버레이에 안 덮이도록)
```

---

## 인물 소개 링크 리서치 방법
```
인물 소개 섹션 작성 시 Claude가 직접 웹 검색으로 확인할 것:

체크 항목:
1. 저서 목록 — 교보문고/Yes24/한빛비즈 등에서 실제 URL 확인
2. 유튜브 채널 — @핸들 확인 (구독자 수는 변동 多, 범위로 표기)
3. 인스타그램/Threads — @핸들 확인
4. Brunch/블로그 — 에세이 대표 제목 확인
5. 주요 강연 — 세바시 회차 번호 + 실제 유튜브 URL 확인

링크 품질 원칙:
- 교보문고 URL: product.kyobobook.co.kr/detail/S000...
- 한빛비즈 URL: hanbit.co.kr/store/books/look.php?p_code=...
- 유튜브: youtube.com/@핸들 (채널) / youtube.com/watch?v=... (특정 영상)
- 인스타: instagram.com/핸들/
- Threads: threads.net/@핸들
- Brunch: brunch.co.kr/@핸들

⚠️ 링크는 반드시 실제로 존재하는 URL만 넣을 것
   검색으로 확인 안 된 URL은 # 처리하거나 생략
```

---

## 이미지 삽입 규칙
```
이미지 파일 관리:
- 위치: documents/img/ 폴더 (HTML과 같은 폴더 기준 img/ 상대경로)
- HTML에서 참조: <img src="img/파일명.jpg" ...>

이미지 자리 표시 (파일 없을 때):
- placeholder div로 처리 (아이콘 + 텍스트)
- img 태그는 주석으로 보존: <!-- <img src="img/파일명.jpg" alt="설명"> -->
- 파일명 안내 텍스트는 placeholder 안에만 — 카드 외부로 노출 금지

이미지 레이아웃 규칙:
- 텍스트와 나란히 배치 시 그리드 비율: 텍스트 3fr / 이미지 2fr (1:1 금지)
- 이미지에 max-height 반드시 지정 (텍스트 높이 초과 방지)
- object-fit:cover + object-position:center top (얼굴 크롭 기준)
- 세로형 프로필 사진: aspect-ratio:3/4
- 가로형 현장 사진: max-height:320px

align-items:
- 텍스트 + 이미지 나란히: align-items:center (수직 중앙 정렬)
- 좌우 카드 높이 맞춤: align-items:stretch + 내부 flex:1
```

---

## 심장박동 타이틀 애니메이션
```
마라톤·스포츠·도전 테마 제안서에 적합한 타이틀 효과:
- 4초 주기로 이중 박동 (빠른 첫 박 + 약한 두 번째 박 + 긴 휴지)
- scale 1.0 → 1.045 → 1.0 → 1.028 → 1.0 구조

@keyframes titlePulse {
  0%       { transform: scale(1); }
  6%       { transform: scale(1.045); }
  12%      { transform: scale(1); }
  18%      { transform: scale(1.028); }
  24%,100% { transform: scale(1); }
}
적용: animation: titlePulse 4s ease-in-out 2s infinite;
      transform-origin: center center;
```

---

## 스폰서십 / PPL / 광고효과 섹션 작성 가이드
```
제안서에 스폰서십·후원·PPL 섹션 포함 시 아래 구조로 작성

─────────────────────────────────────
[ 1 ] 섹션 레이블 & 제목
─────────────────────────────────────
- 레이블: "07 · SPONSOR BENEFITS" 형식
- 제목: "후원기업 제안 — 현물 / 현금 / 콘텐츠" 형태
  (⚠️ "후원 기업이 얻는 것" 표현은 수동적 — "후원기업 제안"으로 통일)
- 부제: 세제 혜택, 정산 방식, 후원 구조 1~2줄 요약

─────────────────────────────────────
[ 2 ] 후원 방식 카드 (benefit-card)
─────────────────────────────────────
방송 트랙 / 유튜브 트랙을 분리해서 각각 카드로 구성:

방송 트랙 카드:
- 출연료 정산 가능 여부
- 방송사/OTT 크레딧 노출
- 로고 삽입 위치 (오프닝 / 엔딩 / 자막)
- 예상 시청자 수 / 도달 범위

유튜브 트랙 카드:
- PPL 방식 설명 (자연스러운 통합형 vs 명시형)
- 현물 협찬 우선 제안 (방한 의류·식품·장비 등)
- 채널 구독자 × 예상 조회수 기반 도달 추산
- 영상 내 노출 형태 예시 ("이 온도에서도 끄떡없는 제품은?" 패턴)

─────────────────────────────────────
[ 3 ] PPL 시나리오 예시 박스 (ppl-box)
─────────────────────────────────────
- 제목: "PPL 시나리오 예시"
- 부제: "— 채널 톤 안에서 자연스럽게 녹는 노출안"
- 3열 그리드로 시나리오 카드 3개 (ppl-examples)
  각 카드: .ex-tag(노출 유형) + 시나리오 설명 (상황 → 자연 등장 → 브랜드 노출)
  예시 유형: PRODUCT / FOOD·DRINK / EQUIPMENT 등

─────────────────────────────────────
[ 4 ] 비용 구조표 (budget-grid)
─────────────────────────────────────
4열 카드 구성 (각 스폰서십 티어):

| 티어명        | 금액 기준      | 주요 혜택 요약          |
|-------------|-------------|----------------------|
| TITLE       | 메인 타이틀 스폰서 | 로고 전면 노출, 전편 크레딧 |
| PRESENTING  | 공동 제작 수준  | 오프닝/엔딩 로고, PPL 1회 |
| SUPPORTING  | 현물 협찬     | 자연 PPL + SNS 노출     |
| SPECIAL     | 소액/현물     | 소셜 미디어 언급          |

카드 내 필수 요소:
- .b-pct: 금액 또는 비율 (크게)
- .b-label: 티어 이름
- .b-desc: 1~2줄 혜택 요약
- .b-items: ul 리스트로 세부 혜택 나열

─────────────────────────────────────
[ 5 ] 예상 광고 도달 효과 (reach-box)
─────────────────────────────────────
3열 카드 구성:

| 항목              | 내용 예시                      |
|-----------------|------------------------------|
| 유튜브 예상 조회      | 채널 평균 × 예상 바이럴 계수          |
| 방송 도달           | 방송사 평균 시청률 × 타깃 인구           |
| SNS 2차 확산       | 출연진 팔로워 합산 × 평균 도달률         |

.highlight 카드 (중요 지표): border-top-color:var(--mint-2); background:var(--mint-soft)
나머지 카드: border-top-color:var(--blue-3)

수치 근거 표시 방법:
- 정확한 수치는 "X만+" 또는 "약 X만" 형태
- 유튜브 채널 구독자·조회수는 가장 최근 데이터 기준 명시
- "※ 예상치이며 실제 성과는 콘텐츠 완성도·배포 전략에 따라 달라질 수 있습니다" 주석 필수

─────────────────────────────────────
[ 6 ] 세제 혜택 안내 (후원 구조가 재단일 때)
─────────────────────────────────────
- 비영리재단을 통한 후원 시 법인세 손금산입 가능 여부 언급
- "재단(ACT Foundation 등) 후원" 구조이면 세제 혜택 적용 가능 명시
- 출연료 정산 가능 트랙 vs 협찬만 가능 트랙 명확히 구분

─────────────────────────────────────
[ 7 ] 레이아웃 주의사항
─────────────────────────────────────
- benefit-grid: grid-template-columns:1fr 1fr; gap:24px
- budget-grid: grid-template-columns:repeat(4,1fr); gap:16px
- ppl-examples: grid-template-columns:1fr 1fr 1fr; gap:14px
- 모바일(980px): benefit-grid→1fr / ppl-examples→1fr / budget-grid→1fr 1fr
- 모바일(600px): budget-grid→1fr
```

---

## 하단 푸터 (고정 정보)
```
⚠️ 모든 기획안에 동일하게 적용:

© 2019. 쾌엔터테인먼트. All rights reserved. CONFIDENTIAL.
기획자 : 김영훈 | 연락처 : +82 10.4444.4395

다국어 번역 시에도 기획자명/연락처 포함:
- EN: Producer : Young-Hoon Kim | Contact : +82 10.4444.4395
- JA: 企画者 : キム・ヨンフン | 連絡先 : +82 10.4444.4395
- ZH: 策划人 : 金英勋 | 联系方式 : +82 10.4444.4395
- ES: Productor : Young-Hoon Kim | Contacto : +82 10.4444.4395
```
