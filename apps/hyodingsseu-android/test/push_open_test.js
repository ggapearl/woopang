// 효딩쓰 휴대폰 화면 — 누른 알림의 그 말로 가는지 시험 (2026-10-19)
// 가짜 PC(효딩쓰 창구)와 가짜 아이폰 포장지(window.Capacitor.Plugins.DeskNative — pushOpened·pushOpen 사건)를 띄우고 헤드리스 Edge 로 web/ 을 연다.
// 실제 대화·서버·기기에는 아무것도 보내지 않는다.
//   node push_open_test.js            (Node 22+ · Edge 필요)   SHOTS=<폴더> 면 그림도 남긴다
const { spawn } = require('child_process');
const fs = require('fs');
const http = require('http');
const os = require('os');
const path = require('path');

const WEB = path.resolve(__dirname, '..', 'web');
const SHOTS = process.env.SHOTS || '';
const EDGE = ['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', 'C:/Program Files/Microsoft/Edge/Application/msedge.exe'].find(p => fs.existsSync(p));
const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.png': 'image/png', '.svg': 'image/svg+xml', '.ico': 'image/x-icon', '.webmanifest': 'application/manifest+json' };
const sleep = ms => new Promise(r => setTimeout(r, ms));
let pass = 0, total = 0;
const check = (label, ok, extra) => { total++; if (ok) pass++; console.log((ok ? 'PASS ' : 'FAIL ') + label + (ok ? '' : ' — ' + JSON.stringify(extra))); };

// ── 가짜 PC ─────────────────────────────
const WEB_VERSION = (fs.readFileSync(path.join(WEB, 'app.js'), 'utf8').match(/WEB_VERSION\s*=\s*'([^']+)'/) || [])[1];
const desk = { boot: 'b0', log: [], hist: [], webver: WEB_VERSION };
const reset = events => { desk.boot = 'b' + Date.now(); desk.log = events.map((e, i) => ({ seq: i + 1, evt: e })); };
const emit = events => events.forEach(e => desk.log.push({ seq: desk.log.length + 1, evt: e }));
const json = (res, obj, code) => { res.writeHead(code || 200, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' }); res.end(JSON.stringify(obj)); };

const server = http.createServer(async (req, res) => {
  const u = new URL(req.url, 'http://x');
  if (u.pathname === '/blank') { res.writeHead(200, { 'Content-Type': 'text/html' }); return res.end('<!doctype html><title>b</title>'); }
  if (u.pathname.startsWith('/__ctl/')) {
    let body = '';
    for await (const c of req) body += c;
    const d = body ? JSON.parse(body) : {};
    if (u.pathname === '/__ctl/reset') { reset(d.events || []); desk.hist = d.hist || []; }
    if (u.pathname === '/__ctl/emit') emit(d.events || []);
    if (u.pathname === '/__ctl/webver') desk.webver = d.v;
    return json(res, { ok: true, seq: desk.log.length });
  }
  if (u.pathname.startsWith('/AI/api/desk/')) {
    const sub = u.pathname.slice('/AI/api/desk/'.length);
    const seq = desk.log.length;
    if (sub === 'state') return json(res, { name: '효딩쓰', host: 'gui', boot: desk.boot, seq, state: 'idle', model: 'normal', models: [{ key: 'normal', label: '보통' }], speed: 1.2, office: [], events: desk.log.map(x => x.evt), quick: [], appr: 0 });
    if (sub === 'events') {
      const since = parseInt(u.searchParams.get('since') || '0', 10);
      const wait = Math.min(2, parseFloat(u.searchParams.get('wait') || '0'));
      if (u.searchParams.get('boot') !== desk.boot) return json(res, { reset: true, boot: desk.boot });
      const end = Date.now() + wait * 1000;
      while (desk.log.length <= since && Date.now() < end) await sleep(80);
      const got = desk.log.filter(x => x.seq > since).map(x => Object.assign({}, x.evt, { seq: x.seq }));
      return json(res, { boot: desk.boot, seq: desk.log.length, state: 'idle', events: got, quick: [], appr: 0 });
    }
    if (sub === 'history') {                                       // 지난 대화 — before 보다 앞선 것을 새것부터 n 개
      const before = parseFloat(u.searchParams.get('before') || '0'), n = parseInt(u.searchParams.get('n') || '50', 10);
      const older = desk.hist.filter(x => x.ts < before).sort((a, b) => b.ts - a.ts);
      return json(res, { items: older.slice(0, n), more: older.length > n });
    }
    if (sub === 'office/workers') return json(res, { workers: [] });
    return json(res, { ok: true });
  }
  let rel = u.pathname.replace(/^\/AI\/desk-app\/?/, '') || 'index.html';
  if (rel === 'web_version.txt') {
    res.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8', 'Cache-Control': 'no-cache' });
    return res.end(desk.webver + '\n');
  }
  const file = path.join(WEB, rel);
  if (!file.startsWith(WEB) || !fs.existsSync(file)) { res.writeHead(404); return res.end('no'); }
  res.writeHead(200, { 'Content-Type': MIME[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
  res.end(fs.readFileSync(file));
});

// ── 말 만들기 ───────────────────────────
const T0 = 1760700000;
let midN = 0;
const mid = () => ('000000000000' + (++midN).toString(16)).slice(-12);
const longText = tag => tag + '\n' + Array.from({ length: 14 }, (_, i) => '줄 ' + (i + 1) + ' — 알림 내용이 이어집니다. 자세한 설명이 한 줄씩 들어갑니다.').join('\n');
const E = {
  user: (ts, text) => ({ type: 'user', text, origin: 'typed', ts, mid: mid() }),
  ai: (ts, text) => ({ type: 'text', text, ts, mid: mid() }),
  inc: (ts, text, extra) => Object.assign({ type: 'incoming', kind: 'phone_out', from: '제휴 제안', text, buttons: [], ts, mid: mid() }, extra || {}),
};
/** PC push._clean 과 같게 — 알림에 실리는 글 */
const pushBody = t => {
  let s = String(t).replace(/<[^>]+>/g, '').replace(/\[((?:[^[\]\n]|\[[^[\]\n]*\])+)\]\(https?:\/\/[^\s)]+\)/g, '$1').replace(/\s+/g, ' ').trim();
  return s.length <= 180 ? s : s.slice(0, 179) + '…';
};
const READ = [];
for (let i = 1; i <= 16; i++) READ.push(i % 3 === 0 ? E.inc(T0 + i * 10, 'R' + i + ' 지난 알림') : i % 2 ? E.user(T0 + i * 10, 'R' + i + ' 내가 한 말') : E.ai(T0 + i * 10, 'R' + i + ' 효딩쓰 답'));
const LAST_READ = T0 + 160;
const UNREAD = [E.inc(T0 + 1010, longText('U1 제휴 제안')), E.ai(T0 + 1020, longText('U2 효딩쓰 답')), E.inc(T0 + 1030, 'U3 짧은 알림'),
  E.inc(T0 + 1040, longText('U4 깃허브 추천')), E.ai(T0 + 1050, 'U5 짧은 답'), E.inc(T0 + 1060, longText('U6 마지막 알림'))];
const batch = (tag, t0) => [1, 2, 3, 4].map(i => E.inc(t0 + (i - 1) * 10, longText(tag + i + ' 자리 비운 사이 온 알림')));

// ── 브라우저 ────────────────────────────
async function main() {
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const base = 'http://127.0.0.1:' + server.address().port;
  const port = 9400 + Math.floor(Math.random() * 90);
  const prof = fs.mkdtempSync(path.join(os.tmpdir(), 'pushopen-'));
  const edge = spawn(EDGE, ['--headless=new', '--remote-debugging-port=' + port, '--user-data-dir=' + prof, '--no-first-run', 'about:blank'], { stdio: 'ignore' });
  let ws, id = 0;
  const pending = {}, errors = [];
  try {
    for (let i = 0; i < 80 && !ws; i++) {
      try { const pg = (await (await fetch('http://127.0.0.1:' + port + '/json')).json()).find(t => t.type === 'page'); if (pg) ws = new WebSocket(pg.webSocketDebuggerUrl); } catch (e) { /* 아직 안 떴다 */ }
      await sleep(250);
    }
    if (ws.readyState !== 1) await new Promise(r => ws.addEventListener('open', r));
    ws.addEventListener('message', m => {
      const d = JSON.parse(m.data);
      if (d.id && pending[d.id]) { pending[d.id](d); delete pending[d.id]; }
      if (d.method === 'Runtime.exceptionThrown') { const x = d.params.exceptionDetails; errors.push(((x.exception || {}).description || x.text || '').split('\n')[0].slice(0, 200)); }
    });
    const send = (method, params = {}) => new Promise(r => { const i = ++id; pending[i] = r; ws.send(JSON.stringify({ id: i, method, params })); });
    const ev = async expr => { const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true }); if (r.result.exceptionDetails) throw new Error(JSON.stringify(r.result.exceptionDetails).slice(0, 300)); return r.result.result.value; };
    await send('Page.enable'); await send('Runtime.enable');
    // 앱이 보이는지·시계를 시험이 쥐고, 아이폰 포장지(ShellPages.bridgeJS 와 같은 이름)를 흉내 낸다.
    // 누른 알림은 포장지처럼 화면이 새로 고쳐져도 남는다(sessionStorage) — 1분 안이면 pushOpened 가 {at, body, n} 을 돌려준다.
    await send('Page.addScriptToEvaluateOnNewDocument', { source: `window.__hidden = false; window.__skew = 0;
      Object.defineProperty(document, 'hidden', { configurable: true, get: () => window.__hidden });
      const _now = Date.now.bind(Date); Date.now = () => _now() + window.__skew;
      (function () {
        const L = {};
        const load = () => { try { return JSON.parse(sessionStorage.getItem('__push') || 'null'); } catch (e) { return null; } };
        const ok = v => Promise.resolve(v || {});
        const N = { haptic: ok, openExternal: ok, deviceName: () => ok({ name: '시험폰' }), speak: ok, stop: ok, setBars: ok,
          pushOpened: () => { const p = load(); return ok(p && _now() - p.got < 60000 ? p.data : {}); },
          addListener: (ev, cb) => { (L[ev] = L[ev] || []).push(cb); return ok({ remove() {} }); } };
        if (sessionStorage.getItem('__oldShell')) delete N.pushOpened;          // 예전 포장지(2.0.x)
        window.Capacitor = { isNativePlatform: () => true, getPlatform: () => 'ios', Plugins: { DeskNative: N } };
        window.__tap = (at, body, signal) => {                                  // 알림을 눌렀다 — signal 이면 떠 있는 화면에 pushOpen
          const prev = load();
          const n = ((prev && prev.data && prev.data.n) || 0) + 1;
          sessionStorage.setItem('__push', JSON.stringify({ got: _now(), data: { at, body, n } }));
          if (signal) (L.pushOpen || []).forEach(f => f({}));
          return n;
        };
      })();` });
    const ctl = (p, body) => fetch(base + '/__ctl/' + p, { method: 'POST', body: JSON.stringify(body || {}) });
    const view = (w, h, dark) => Promise.all([
      send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 2, mobile: false }),
      send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: dark ? 'dark' : 'light' }] })]);
    const shot = async name => { if (!SHOTS) return; const r = await send('Page.captureScreenshot', { format: 'png' }); fs.mkdirSync(SHOTS, { recursive: true }); fs.writeFileSync(path.join(SHOTS, name + '.png'), Buffer.from(r.result.data, 'base64')); };

    /** 앱을 연다 — push 를 주면 「그 알림을 눌러 앱을 켰다」(포장지에 이미 들어 있음). opt.old 면 예전 포장지 */
    async function openApp(events, seen, push, opt) {
      opt = opt || {};
      await ctl('reset', { events, hist: opt.hist || [] });
      await send('Page.navigate', { url: base + '/blank' }); await sleep(250);
      await ev(`localStorage.clear(); localStorage.setItem('desk-token', 'mock'); localStorage.setItem('seenTs', '${seen}'); sessionStorage.clear();
        ${opt.old ? `sessionStorage.setItem('__oldShell', '1');` : ''}
        ${push ? `sessionStorage.setItem('__push', JSON.stringify({ got: Date.now(), data: { at: ${push.at}, body: ${JSON.stringify(push.body)}, n: 1 } }));` : ''} 0`);
      await ev('window.__hidden = false; window.__skew = 0; 0');
      await send('Page.navigate', { url: base + '/AI/desk-app/' });
      await sleep(opt.wait || 1400);
    }
    const info = () => ev(`(() => {
      const chat = document.querySelector('.chat'); if (!chat) return { noChat: true };
      const div = document.querySelector('.unread-div');
      const nxt = div && div.nextElementSibling ? (div.nextElementSibling.textContent.match(/[URWXHP]\\d+/) || [''])[0] : null;
      return { hasDiv: !!div, next: nxt, nearBottom: chat.scrollHeight - chat.scrollTop - chat.clientHeight < 90,
        sw: document.documentElement.scrollWidth, cw: document.documentElement.clientWidth };
    })()`);
    const topOf = marker => ev(`(() => { const chat = document.querySelector('.chat'); const el = [...document.querySelectorAll('[data-iid]')].find(e => e.textContent.includes('${marker}')); return el ? Math.round(el.getBoundingClientRect().top - chat.getBoundingClientRect().top) : null; })()`);
    const hit = () => ev(`(() => { const e = document.querySelector('.push-hit'); return e ? e.textContent.slice(0, 40) : ''; })()`);
    const tap = (at, body, signal) => ev(`window.__tap(${at}, ${JSON.stringify(body)}, ${!!signal})`);
    const hide = async () => { await ev(`window.__hidden = true; document.dispatchEvent(new Event('visibilitychange')); 0`); await sleep(150); };
    const show = async (skewMs, wait) => { await ev(`window.__skew += ${skewMs || 0}; window.__hidden = false; document.dispatchEvent(new Event('visibilitychange')); 0`); await sleep(wait || 1400); };
    // 대화 윗머리에 앉았다 — 그 말이 첫 안 읽은 글이면 「여기부터 안 읽음」 줄이 맨 위, 그 말은 줄 바로 아래(40px 안팎)
    const seated = t => t !== null && t >= -5 && t <= 70;

    await view(422, 800, false);

    console.log('--- 1. 앱이 꺼져 있을 때 4번째 알림(U4)을 눌러 켬');
    await openApp(READ.concat(UNREAD), LAST_READ, { at: T0 + 1040.4, body: pushBody(UNREAD[3].text) });
    const t1 = await topOf('U4'), h1 = await hit(), i1 = await info();
    check('첫 안 읽은 글(U1)이 아니라 누른 알림 U4 가 윗머리에 앉았다', seated(t1), { t1 });
    check('U4 가 잠깐 비친다', h1.includes('U4'), { h1 });
    check('맨 아래로 가지 않았다', i1.nearBottom === false, i1);
    check('「여기부터 안 읽음」 줄은 그대로 U1 앞(위쪽)', i1.hasDiv && i1.next === 'U1', i1);
    await shot('01_push_cold_light_422');
    await sleep(2700);
    check('비침이 사라졌다', (await hit()) === '');

    console.log('--- 2. 글이 다른 알림 — 2분 안의 가장 가까운 말(U2)로');
    await openApp(READ.concat(UNREAD), LAST_READ, { at: T0 + 1022, body: '전혀 다른 알림 글' });
    const t2 = await topOf('U2');
    check('시각이 가장 가까운 U2 가 윗머리에', seated(t2), { t2 });

    console.log('--- 3. 시각이 더 가까운 말보다 글이 같은 말이 먼저');
    const X = [E.inc(T0 + 1500, longText('X1 서버 점검 결과')), E.inc(T0 + 1500.5, longText('X2 배송 현황')),
      E.inc(T0 + 1510, longText('X3 다음 알림')), E.inc(T0 + 1520, longText('X4 다음 알림'))];
    await openApp(READ.concat(X), LAST_READ, { at: T0 + 1500.6, body: pushBody(X[0].text) });
    const t3 = await topOf('X1');
    check('0.1초 더 가까운 X2 가 아니라 글이 같은 X1 으로', seated(t3), { t3, x2: await topOf('X2') });

    console.log('--- 4. 앱을 내려 둔 사이 알림 4개 → 잠금 화면에서 3번째(W3) 누름 (누름 신호가 화면보다 먼저)');
    await openApp(READ, LAST_READ);
    await hide();
    const W = batch('W', T0 + 4000);
    await ctl('emit', { events: W });
    await tap(T0 + 4020.3, pushBody(W[2].text), true);
    await show(5000);
    const t4 = await topOf('W3');
    check('이어 받은 뒤 W3 에 앉았다(첫 안 읽은 W1 이 아니라)', seated(t4), { t4, w1: await topOf('W1') });
    check('W3 가 비친다', (await hit()).includes('W3'));

    console.log('--- 5. 같은 알림은 한 번만 — 끝까지 읽고 나갔다 오면 다시 끌려가지 않는다');
    await ev(`(() => { const c = document.querySelector('.chat'); c.scrollTop = c.scrollHeight; })()`);
    await sleep(1500);
    await hide(); await show(3000);
    const i5 = await info();
    check('맨 아래 그대로·비침 없음', i5.nearBottom === true && (await hit()) === '', i5);

    console.log('--- 6. 화면이 먼저 돌아와 안 읽은 첫 글(W5)에 앉은 뒤 누름 신호가 옴 → W7 로');
    await openApp(READ, LAST_READ);
    await hide();
    const W2 = batch('W', T0 + 4100).map((e, i) => Object.assign(e, { text: e.text.replace(/^W\d/, 'W' + (5 + i)) }));
    await ctl('emit', { events: W2 });
    await show(5000);
    const i6a = await info();
    check('(먼저) 안 읽은 첫 글 W5 앞에 줄', i6a.hasDiv && i6a.next === 'W5', i6a);
    await tap(T0 + 4120.3, pushBody(W2[2].text), true);
    await sleep(900);
    const t6 = await topOf('W7');
    check('누른 알림 W7 이 윗머리로', seated(t6), { t6 });

    console.log('--- 7. 허락 카드 알림을 누름 → 카드(맨 아래)로');
    await openApp(READ, LAST_READ);
    await hide();
    await ctl('emit', { events: [E.inc(T0 + 4200, longText('P1 알림')), E.inc(T0 + 4210, longText('P2 알림')),
      { type: 'permission', id: 'p9', title: '명령 실행: 서버 재시작', note: '', body: '', can_always: true, ts: T0 + 4230 }] });
    await tap(T0 + 4230.4, '🔐 명령 실행: 서버 재시작', true);
    await show(5000);
    const i7 = await info();
    check('맨 아래(카드)로 갔다', i7.nearBottom === true, i7);
    check('카드가 비친다', (await hit()).includes('서버 재시작'));

    console.log('--- 8. 화면에 없는 예전 알림 → 지난 대화를 더 받아 와서 그 말로');
    const RECENT = Array.from({ length: 12 }, (_, i) => E.inc(T0 + 9000 + i * 10, 'N' + (i + 1) + ' 최근 알림'));
    const HIST = Array.from({ length: 8 }, (_, i) => ({ ts: T0 + 100 + i * 10, type: 'incoming', kind: 'phone_out', from: '서버 감시', text: longText('H' + (i + 1) + ' 지난밤 알림'), mid: mid() }));
    await openApp(RECENT, T0 + 9110, { at: T0 + 120.4, body: pushBody(HIST[2].text) }, { hist: HIST, wait: 2200 });
    const t8 = await topOf('H3');
    check('예전 알림 H3 이 윗머리에', seated(t8), { t8 });

    console.log('--- 9. 알림을 눌러 돌아왔는데 새 화면이 배포돼 있음 → 새로 고친 화면도 그 말로');
    await openApp(READ, LAST_READ);
    await ctl('webver', { v: '2099-01-01z' });
    await ev('window.__mark = 1; 0');
    await hide();
    const W3 = batch('W', T0 + 4300).map((e, i) => Object.assign(e, { text: e.text.replace(/^W\d/, 'W' + (9 + i)) }));
    await ctl('emit', { events: W3 });
    await tap(T0 + 4320.3, pushBody(W3[2].text), true);
    await show(5000, 2600);
    check('새로 고쳤다', !(await ev('window.__mark === 1')));
    const t9 = await topOf('W11');
    check('새로 고친 화면도 W11 에 앉았다', seated(t9), { t9 });
    await ctl('webver', { v: WEB_VERSION });

    console.log('--- 10. 예전 포장지(pushOpened 없음) — 예전처럼 안 읽은 첫 글로');
    await openApp(READ.concat(UNREAD), LAST_READ, { at: T0 + 1040.4, body: pushBody(UNREAD[3].text) }, { old: true });
    const i10 = await info();
    check('안 읽은 첫 글(U1) 앞 줄에 앉았다', i10.hasDiv && i10.next === 'U1' && i10.nearBottom === false, i10);
    check('비침 없음', (await hit()) === '');

    console.log('--- 11. 모양 — 폭 422/390/375/360 · 라이트/다크, 가로 넘침 없음');
    for (const [w, dark] of [[422, false], [390, false], [375, false], [360, false], [422, true], [360, true]]) {
      await view(w, 800, dark);
      await openApp(READ.concat(UNREAD), LAST_READ, { at: T0 + 1040.4, body: pushBody(UNREAD[3].text) });
      const i = await info(), t = await topOf('U4');
      check(w + 'px ' + (dark ? '다크' : '라이트') + ': U4 에 앉음·가로 넘침 없음', seated(t) && i.sw <= i.cw, { t, i });
      if (w === 422) await shot('11_push_' + (dark ? 'dark' : 'light') + '_' + w);
    }
    await view(422, 800, false);

    check('화면 오류 없음', errors.length === 0, errors);
  } catch (e) {
    total++; console.log('ERROR ' + (e && e.stack || e));
  }
  console.log('RESULT ' + pass + '/' + total);
  try { ws.close(); } catch (e) { /* 이미 닫힘 */ }
  edge.kill(); server.close();
  setTimeout(() => { try { fs.rmSync(prof, { recursive: true, force: true }); } catch (e) { /* 프로필 잠김 — 임시 폴더라 무시 */ } process.exit(pass === total ? 0 : 1); }, 1500);
}
main();
