"""효딩쓰 휴대폰 화면 — 실브라우저(Edge headless) 시험. harness.py 가 8765 에 떠 있어야 한다(python harness.py 8765).
  ⚠ live 는 읽기만 한다 — 실제 대화에 글을 보내지 않는다.
  python ui_test.py mock   : 흉내 낸 대화로 화면·버튼·사건 처리
  python ui_test.py live <token> : 실제 woopang.com → PC 효딩쓰 (읽기만)
"""
import json
import os
import sys
import time

from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.edge.options import Options

sys.stdout.reconfigure(encoding='utf-8', errors='replace')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'shots')
os.makedirs(OUT, exist_ok=True)
URL = 'http://127.0.0.1:8765/AI/desk-app/'
MODE = sys.argv[1] if len(sys.argv) > 1 else 'mock'
TOKEN = 'mock' if MODE == 'mock' else sys.argv[2]
results = []


def check(name, ok, detail=''):
    results.append((name, bool(ok), detail))
    print(('PASS ' if ok else 'FAIL ') + name + (('  — ' + str(detail)) if detail and not ok else ''))


def driver(width, height=880, dark=False):
    o = Options()
    o.add_argument('--headless=new')
    o.add_argument('--force-device-scale-factor=2')
    o.add_argument('--window-size=%d,%d' % (width, height))
    if dark:
        o.add_argument('--force-dark-mode')
        o.add_argument('--blink-settings=preferredColorScheme=0')
    o.add_experimental_option('mobileEmulation', {'deviceMetrics': {'width': width, 'height': height, 'pixelRatio': 2.0}})
    d = webdriver.Edge(options=o)
    d.set_window_size(width, height)
    return d


def overflow(d):
    """페이지가 가로로 넘치는지 + 화면 밖으로 삐져나간 요소(의도된 가로 레일 .chips / pre 제외)"""
    return d.execute_script("""
      const W = document.documentElement.clientWidth;
      const bad = [];
      document.querySelectorAll('#app *').forEach(el => {
        if (el.closest('.chips') || el.closest('pre') || el.closest('.drawer') || el.closest('.page:not(.on)')) return;
        const r = el.getBoundingClientRect();
        if (r.width && (r.right > W + 1 || r.left < -1)) bad.push(el.tagName + '.' + el.className + ' ' + Math.round(r.left) + '..' + Math.round(r.right));
      });
      return {scroll: document.documentElement.scrollWidth, W, bad: bad.slice(0, 8)};
    """)


def logs(d):
    try:
        return [l for l in d.get_log('browser') if l['level'] in ('SEVERE',)]
    except Exception:
        return []


def shot(d, name):
    d.save_screenshot(os.path.join(OUT, name + '.png'))


def run_mock():
    d = driver(422)
    try:
        d.get(URL)
        time.sleep(1.2)
        check('연결 화면이 뜬다', d.find_elements(By.CSS_SELECTOR, '#pair .code'))
        shot(d, 'm01_pair')
        code = d.find_element(By.CSS_SELECTOR, '#pair .code')
        code.send_keys('12ab34')
        check('숫자만 남는다', code.get_attribute('value') == '1234', code.get_attribute('value'))

        d.execute_script("localStorage.setItem('desk-token','mock'); localStorage.setItem('base', location.origin + '/AI/api/desk');")
        d.get(URL)
        time.sleep(4.5)
        shot(d, 'm02_main')
        n_user = len(d.find_elements(By.CSS_SELECTOR, '.u-bubble'))
        n_ai = len(d.find_elements(By.CSS_SELECTOR, '.ai'))
        check('지난 대화 + 새 사건 (사용자 말 2)', n_user == 2, n_user)
        check('답 3개 (지난 1 + 새 2)', n_ai == 3, n_ai)
        via = [e.text for e in d.find_elements(By.CSS_SELECTOR, '.via')]
        check('PC 에서 친 말에만 「PC 에서」', via == ['PC 에서'], via)
        check('도구 줄 2개', len(d.find_elements(By.CSS_SELECTOR, '.tool')) == 2)
        check('도구 성공 ✓', len(d.find_elements(By.CSS_SELECTOR, '.tool .ok')) == 1)
        check('도구 실패 점', len(d.find_elements(By.CSS_SELECTOR, '.tool .fail')) == 1)
        check('하위 도구 들여쓰기', len(d.find_elements(By.CSS_SELECTOR, '.tool.sub')) == 1)
        md = d.find_elements(By.CSS_SELECTOR, '.ai-body.md')[-1]
        html = md.get_attribute('innerHTML')
        check('마크다운 제목→굵게', '<strong>요약</strong>' in html, html[:200])
        check('목록 •', '• <strong>8080</strong>' in html, html[:300])
        check('코드 블록', '<pre>' in html)
        check('링크(마크다운)', 'href="https://woopang.com/bookmark"' in html)
        links = [a.get_attribute('href') for a in md.find_elements(By.TAG_NAME, 'a')]
        check('맨 주소 뒤 한글이 주소에 안 붙음', 'https://woopang.com/AI/doc/test?e=1&s=abc' in links, links)
        check('스트리밍 커서 없음(다 끝남)', '▍' not in d.find_element(By.CSS_SELECTOR, '.items').text)
        metas = [e.text for e in d.find_elements(By.CSS_SELECTOR, '.meta')]
        check('걸린 시간·단계', '5.3초 · 3단계' in metas, metas)
        check('읽기 버튼 (답마다)', len(d.find_elements(By.CSS_SELECTOR, '.read')) == 3)
        check('허락 카드 3버튼', [b.text for b in d.find_elements(By.CSS_SELECTOR, '.card:not(.q) .btn')] == ['허락', '계속 허락', '거절'])
        check('선택 카드', len(d.find_elements(By.CSS_SELECTOR, '.card.q .opt')) == 2)
        inbox = [e.text.split('\n')[0] for e in d.find_elements(By.CSS_SELECTOR, '.inbox b')]
        check('들어온 말 출처', inbox == ['자율 점검', '다른 세션 · 농민닷컴 세션'], inbox)
        check('알림 줄·오류 줄', d.find_elements(By.CSS_SELECTOR, '.note') and d.find_elements(By.CSS_SELECTOR, '.err'))
        status = d.find_element(By.CSS_SELECTOR, '.who small').text
        check('상태줄', status in ('PC 창과 연결됨', '대표님 허락을 기다려요'), status)
        check('생각의 깊이 표시', d.find_element(By.CSS_SELECTOR, '.pill').text == 'Opus 5.5')
        o = overflow(d)
        check('422 가로 넘침 없음', o['scroll'] <= o['W'] and not o['bad'], o)

        # 허락 → 서버에 decide, 다음 사건에 닫힘
        d.find_elements(By.CSS_SELECTOR, '.card:not(.q) .btn')[0].click()
        time.sleep(2.5)
        kick = d.find_element(By.CSS_SELECTOR, '.card:not(.q) .kick').text
        check('허락 누르면 「허락했습니다」로 닫힘', kick == '허락했습니다', kick)
        d.find_elements(By.CSS_SELECTOR, '.card.q .opt')[1].click()
        time.sleep(2.5)
        kick = d.find_element(By.CSS_SELECTOR, '.card.q .kick').text
        check('선택 카드 답 → 「답했습니다」', kick == '답했습니다', kick)
        log = json.loads(d.execute_script("return fetch('/AI/api/desk/_log').then(r=>r.text())"))['log']
        check('decide 요청 모양', ['decide', {'id': 'p1', 'decision': 'allow'}] in log, log)
        check('answer 요청 모양', ['answer', {'id': 'q1', 'answers': {'어느 쪽으로 할까요?': '밤에'}}] in log, log)

        # 보내기 → 멈추기 전환, 보내면 새 말
        ta = d.find_element(By.CSS_SELECTOR, '#main textarea')
        send = d.find_element(By.CSS_SELECTOR, '#main .send')
        check('빈 입력이면 보내기 꺼짐', send.get_attribute('disabled') is not None)
        ta.send_keys('테스트 한 줄')
        send.click()
        time.sleep(2.5)
        check('보낸 말이 대화에', '테스트 한 줄' in d.find_element(By.CSS_SELECTOR, '.items').text)
        check('입력칸 비움', ta.get_attribute('value') == '')
        d.find_elements(By.CSS_SELECTOR, '.chip')[0].click()
        time.sleep(2.2)
        check('빠른 칩 → 보냄', '서버 상태 어때?' in json.dumps(json.loads(d.execute_script("return fetch('/AI/api/desk/_log').then(r=>r.text())")), ensure_ascii=False))
        shot(d, 'm03_after')

        # 읽기 — 브라우저에 한국어 목소리가 없으면 PC(tts 503) → 알림
        d.find_elements(By.CSS_SELECTOR, '.read')[-1].click()
        time.sleep(1.2)
        shot(d, 'm04_read')

        # ☰
        d.find_element(By.CSS_SELECTOR, '.bar .icon').click()
        time.sleep(0.8)
        rows = [e.text for e in d.find_elements(By.CSS_SELECTOR, '.drawer .drow b')]
        check('서랍: 효딩쓰·직원 2·설정·새 대화', rows == ['효딩쓰', '김효딩 · 비서실장', '박개발 · 백엔드', '설정', '새 대화'], rows)
        check('일하는 직원 고리', len(d.find_elements(By.CSS_SELECTOR, '.drawer .avatar.busy')) == 1)
        check('머리글자 「효」', d.find_elements(By.CSS_SELECTOR, '.drawer .avatar')[0].text == '효')
        shot(d, 'm05_drawer')
        d.find_elements(By.CSS_SELECTOR, '.drawer .drow')[1].click()
        time.sleep(1.5)
        check('직원 대화 화면', d.find_elements(By.CSS_SELECTOR, '.page.on .o-bubble'))
        check('직원 기록: 내 말 1', len(d.find_elements(By.CSS_SELECTOR, '.page.on .u-bubble')) == 1)
        shot(d, 'm06_worker')
        o = overflow(d)
        check('직원 화면 가로 넘침 없음', o['scroll'] <= o['W'] and not o['bad'], o)
        d.find_element(By.CSS_SELECTOR, '.page.on .bar .icon').click()
        time.sleep(0.6)
        check('뒤로 → 페이지 닫힘', not d.find_elements(By.CSS_SELECTOR, '.page'))

        # 설정
        d.find_element(By.CSS_SELECTOR, '.bar .icon').click()
        time.sleep(0.6)
        d.find_elements(By.CSS_SELECTOR, '.drawer .drow')[3].click()
        time.sleep(0.8)
        shot(d, 'm07_settings')
        labels = [e.text for e in d.find_elements(By.CSS_SELECTOR, '.page.on .fsec h3')]
        check('설정 섹션', labels == ['답을 소리로 듣기', '효딩쓰', '화면', '연결'], labels)
        o = overflow(d)
        check('설정 가로 넘침 없음', o['scroll'] <= o['W'] and not o['bad'], o)
        d.find_elements(By.CSS_SELECTOR, '.page.on .seg button')[2].click()
        time.sleep(0.5)
        check('다크로 바꾸기', d.execute_script("return document.documentElement.dataset.theme") == 'dark')
        bg = d.execute_script("return getComputedStyle(document.body).backgroundColor")
        check('다크 배경', bg == 'rgb(13, 19, 48)', bg)
        shot(d, 'm08_settings_dark')
        d.find_element(By.CSS_SELECTOR, '.page.on .bar .icon').click()
        time.sleep(0.6)
        shot(d, 'm09_main_dark')
        d.execute_script("localStorage.setItem('theme','system')")

        # 모델 메뉴
        d.find_element(By.CSS_SELECTOR, '.pill').click()
        time.sleep(0.4)
        check('모델 메뉴', d.find_elements(By.CSS_SELECTOR, '.menu button'))
        d.find_element(By.CSS_SELECTOR, '.menu-scrim').click()

        # 연결 끊기 확인 창
        d.find_element(By.CSS_SELECTOR, '.bar .icon').click()
        time.sleep(0.6)
        d.find_elements(By.CSS_SELECTOR, '.drawer .drow')[3].click()
        time.sleep(0.8)
        d.find_element(By.CSS_SELECTOR, '.page.on .link.danger').click()
        time.sleep(0.4)
        check('연결 끊기 확인 창', d.find_elements(By.CSS_SELECTOR, '.sheet'))
        d.find_element(By.CSS_SELECTOR, '.sheet .btn.danger').click()
        time.sleep(1)
        check('끊으면 연결 화면 + 안내', d.find_elements(By.CSS_SELECTOR, '#pair') and '연결을 끊었습니다' in d.find_element(By.CSS_SELECTOR, '#pair .msg').text)
        log = json.loads(d.execute_script("return fetch('/AI/api/desk/_log').then(r=>r.text())"))['log']
        check('PC 에 unpair 알림', any(x[0] == 'unpair' for x in log), log[-3:])
        time.sleep(0.6)
        check('토큰 지움', d.execute_script("return localStorage.getItem('desk-token')") is None)
        sev = logs(d)
        check('콘솔 오류 없음', not sev, sev[:3])
    finally:
        d.quit()

    for w in (390, 375, 360):
        d = driver(w)
        try:
            d.get(URL)
            d.execute_script("localStorage.setItem('desk-token','mock'); localStorage.setItem('base', location.origin + '/AI/api/desk');")
            d.get(URL)
            time.sleep(4)
            o = overflow(d)
            check('%d 가로 넘침 없음' % w, o['scroll'] <= o['W'] and not o['bad'], o)
            if w == 360:
                shot(d, 'm10_360')
        finally:
            d.quit()


def run_live():
    d = driver(422)
    try:
        d.get(URL)
        d.execute_script("localStorage.setItem('desk-token', arguments[0]); localStorage.setItem('base', location.origin + '/AI/api/desk');", TOKEN)
        d.get(URL)
        time.sleep(6)
        status = d.find_element(By.CSS_SELECTOR, '.who small').text
        check('실제 PC 와 연결', 'PC' in status and '닿지' not in status, status)
        n = len(d.find_elements(By.CSS_SELECTOR, '.items > *'))
        check('지난 대화 받아옴', n > 0, n)
        shot(d, 'l01_main')
        d.find_element(By.CSS_SELECTOR, '.bar .icon').click()
        time.sleep(2.5)
        ws = [e.text for e in d.find_elements(By.CSS_SELECTOR, '.drawer .drow b')]
        check('직원 명단 (15명 안팎)', len(ws) >= 10, len(ws))
        shot(d, 'l02_drawer')
        d.find_elements(By.CSS_SELECTOR, '.drawer .drow')[1].click()
        time.sleep(3)
        check('직원 기록 열림', d.find_elements(By.CSS_SELECTOR, '.page.on .wk-head'))
        shot(d, 'l03_worker')
        o = overflow(d)
        check('실데이터 가로 넘침 없음', o['scroll'] <= o['W'] and not o['bad'], o)
        d.find_element(By.CSS_SELECTOR, '.page.on .bar .icon').click()
        time.sleep(0.5)
        o = overflow(d)
        check('실대화 가로 넘침 없음', o['scroll'] <= o['W'] and not o['bad'], o)
        sev = logs(d)
        check('콘솔 오류 없음', not sev, sev[:3])
    finally:
        d.quit()


if __name__ == '__main__':
    run_mock() if MODE == 'mock' else run_live()
    bad = [r for r in results if not r[1]]
    print('\n%d/%d PASS' % (len(results) - len(bad), len(results)))
