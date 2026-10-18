// 효딩쓰 휴대폰 화면 — 읽음/안 읽음 · 알림 눌러 들어갔을 때 앉는 자리 시험 (2026-10-18)
// 가짜 PC(효딩쓰 창구)를 직접 띄우고 헤드리스 Edge 로 web/ 을 연다. 실제 대화·서버에는 아무것도 보내지 않는다.
//   node unread_test.js            (Node 22+ · Edge 필요)
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
const desk = { boot: 'b0', log: [], webver: WEB_VERSION };           // webver: 서버가 알려 주는 화면 버전(배포가 web_version.txt 에 적는 것)
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
    if (u.pathname === '/__ctl/reset') reset(d.events || []);
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
    if (sub === 'history') return json(res, { items: [], more: false });
    if (sub === 'office/workers') return json(res, { workers: [] });
    return json(res, { ok: true });
  }
  let rel = u.pathname.replace(/^\/AI\/desk-app\/?/, '') || 'index.html';
  if (rel === 'web_version.txt') {
    if (desk.webver == null) { res.writeHead(404); return res.end('no'); }
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
const READ = [];
for (let i = 1; i <= 16; i++) READ.push(i % 3 === 0 ? E.inc(T0 + i * 10, 'R' + i + ' 지난 알림') : i % 2 ? E.user(T0 + i * 10, 'R' + i + ' 내가 한 말') : E.ai(T0 + i * 10, 'R' + i + ' 효딩쓰 답'));
const LAST_READ = T0 + 160;
const UNREAD = [E.inc(T0 + 1010, longText('U1 제휴 제안'), { buttons: [['승인', 'prop:abc123:ok'], ['보류', 'prop:abc123:hold']] }),
  E.ai(T0 + 1020, longText('U2 효딩쓰 답')), E.inc(T0 + 1030, 'U3 짧은 알림'), E.inc(T0 + 1040, longText('U4 깃허브 추천')),
  E.ai(T0 + 1050, 'U5 짧은 답'), E.inc(T0 + 1060, longText('U6 마지막 알림'))];
const LAST_TS = T0 + 1060;

// ── 브라우저 ────────────────────────────
async function main() {
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const sport = server.address().port;
  const base = 'http://127.0.0.1:' + sport;
  const port = 9300 + Math.floor(Math.random() * 90);
  const prof = fs.mkdtempSync(path.join(os.tmpdir(), 'unread-'));
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
    // 앱이 보이는지(document.hidden)와 시계를 시험이 쥔다
    await send('Page.addScriptToEvaluateOnNewDocument', { source: `window.__hidden = false; window.__skew = 0;
      Object.defineProperty(document, 'hidden', { configurable: true, get: () => window.__hidden });
      const _now = Date.now.bind(Date); Date.now = () => _now() + window.__skew;` });
    const ctl = (p, body) => fetch(base + '/__ctl/' + p, { method: 'POST', body: JSON.stringify(body || {}) });
    const view = (w, h, dark) => Promise.all([
      send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 2, mobile: false }),
      send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: dark ? 'dark' : 'light' }] })]);
    const shot = async name => { if (!SHOTS) return; const r = await send('Page.captureScreenshot', { format: 'png' }); fs.mkdirSync(SHOTS, { recursive: true }); fs.writeFileSync(path.join(SHOTS, name + '.png'), Buffer.from(r.result.data, 'base64')); };

    /** 폰에 적어 둔 「여기까지 봤다」(seen: null 이면 적지 않음 = 처음 켬)로 앱을 연다 */
    async function openApp(events, seen) {
      await ctl('reset', { events });
      await send('Page.navigate', { url: base + '/blank' }); await sleep(250);
      await ev(`localStorage.clear(); localStorage.setItem('desk-token', 'mock'); ${seen == null ? '' : `localStorage.setItem('seenTs', '${seen}');`} sessionStorage.clear();`);
      await ev('window.__hidden = false; window.__skew = 0; 0');
      await send('Page.navigate', { url: base + '/AI/desk-app/' });
      await sleep(1300);
    }
    const info = () => ev(`(() => {
      const chat = document.querySelector('.chat'); if (!chat) return { noChat: true };
      const cr = chat.getBoundingClientRect(), div = document.querySelector('.unread-div'), jump = document.querySelector('.jump');
      const marks = [...document.querySelectorAll('[data-iid].new')].map(e => (e.textContent.match(/[UR]\\d+/) || [''])[0]);
      const nxt = div && div.nextElementSibling ? (div.nextElementSibling.textContent.match(/[UR]\\d+/) || [''])[0] : null;
      return { hasDiv: !!div, divTop: div ? Math.round(div.getBoundingClientRect().top - cr.top) : null, next: nxt,
        nearBottom: chat.scrollHeight - chat.scrollTop - chat.clientHeight < 90, scrollTop: Math.round(chat.scrollTop), max: Math.round(chat.scrollHeight - chat.clientHeight),
        marks, jumpOn: !!jump && !jump.hidden && getComputedStyle(jump).display !== 'none', jumpN: jump ? (jump.querySelector('.jn') || {}).textContent : null,
        seen: localStorage.getItem('seenTs'), items: document.querySelectorAll('[data-iid]').length,
        sw: document.documentElement.scrollWidth, cw: document.documentElement.clientWidth };
    })()`);
    const hide = async () => { await ev(`window.__hidden = true; document.dispatchEvent(new Event('visibilitychange')); 0`); await sleep(150); };
    const show = async (skewMs) => { await ev(`window.__skew += ${skewMs || 0}; window.__hidden = false; document.dispatchEvent(new Event('visibilitychange')); 0`); await sleep(1300); };
    const topOf = marker => ev(`(() => { const chat = document.querySelector('.chat'); const el = [...document.querySelectorAll('[data-iid]')].find(e => e.textContent.includes('${marker}')); return el ? Math.round(el.getBoundingClientRect().top - chat.getBoundingClientRect().top) : null; })()`);

    await view(422, 800, false);

    // ── 1. 알림이 쌓인 채로 앱을 연다 (알림을 눌러 들어온 것과 같다) ──
    console.log('--- 1. 안 읽은 알림 6개가 쌓인 채 열기');
    await openApp(READ.concat(UNREAD), LAST_READ);
    let i1 = await info();
    check('앱이 대화를 그렸다(항목 22개)', i1.items === 22, i1);
    check('맨 아래로 가지 않고 안 읽은 쪽에 앉았다', i1.nearBottom === false, i1);
    check('「여기부터 안 읽음」 줄이 있다', i1.hasDiv === true, i1);
    check('줄 바로 뒤가 첫 안 읽은 글(U1)이다', i1.next === 'U1', i1);
    check('줄이 대화 윗머리에 와 있다(0~40px)', i1.divTop !== null && i1.divTop >= 0 && i1.divTop <= 40, i1);
    check('안 읽은 6개 중 처음엔 막대가 남아 있다(읽는 중)', i1.marks.length >= 1 && i1.marks.length <= 6, i1);
    check('「↓」 단추에 안 읽은 수가 뜬다', i1.jumpOn === true && /^\d+$/.test(i1.jumpN || ''), i1);
    await shot('01_unread_light_422');
    await sleep(1200);                                           // 머문 시간(0.8초) 지나면 보이는 글은 읽은 것
    const i1b = await info();
    check('화면에 보이던 첫 글(U1)이 읽음 처리됨', !i1b.marks.includes('U1'), i1b);
    check('아래 못 본 글(U6)은 아직 안 읽음', i1b.marks.includes('U6'), i1b);
    check('읽은 자리가 폰에 적혔다(seenTs 가 앞섬)', parseFloat(i1b.seen) > LAST_READ && parseFloat(i1b.seen) < LAST_TS, i1b);
    check('단추의 수가 남은 막대 수와 같다', String(i1b.marks.length) === i1b.jumpN, i1b);

    console.log('--- 2. 「↓」 단추 → 다음 안 읽은 글로');
    const nextBefore = i1b.marks[0];
    await ev(`document.querySelector('.jump').click(); 0`);
    await sleep(1000);
    const t2 = await topOf(nextBefore);
    check('단추가 다음 안 읽은 글(' + nextBefore + ')을 윗머리로 가져온다', t2 !== null && t2 >= -5 && t2 <= 70, { t2, nextBefore });

    console.log('--- 3. 끝까지 내려가면 전부 읽음');
    await ev(`(() => { const c = document.querySelector('.chat'); c.scrollTop = c.scrollHeight; })()`);
    await sleep(1500);
    const i3 = await info();
    check('막대가 모두 사라졌다', i3.marks.length === 0, i3);
    check('seenTs 가 마지막 글 시각이다', Math.abs(parseFloat(i3.seen) - LAST_TS) < 0.01, i3);
    check('단추가 숨는다', i3.jumpOn === false, i3);

    console.log('--- 4. 앱을 껐다 켜기(다시 열기) — 읽은 건 다시 안 읽음으로 안 뜬다');
    await send('Page.navigate', { url: base + '/AI/desk-app/' }); await sleep(1300);
    const i4 = await info();
    check('줄이 없고 맨 아래에 있다', i4.hasDiv === false && i4.nearBottom === true && i4.marks.length === 0, i4);

    console.log('--- 5. 중간까지만 읽고 다시 열면 남은 첫 글에 앉는다');
    await openApp(READ.concat(UNREAD), T0 + 1030);                // U1~U3 까지 읽음
    const i5 = await info();
    check('줄 바로 뒤가 U4 다', i5.hasDiv && i5.next === 'U4', i5);
    check('U1~U3 는 막대가 없다', !i5.marks.some(m => ['U1', 'U2', 'U3'].includes(m)), i5);

    console.log('--- 6. 읽을 게 없으면 예전처럼 맨 아래');
    await openApp(READ.concat(UNREAD), LAST_TS);
    const i6 = await info();
    check('줄 없음·맨 아래·막대 없음', !i6.hasDiv && i6.nearBottom && i6.marks.length === 0, i6);

    console.log('--- 7. 처음 켠 폰(적어 둔 게 없음) — 전부 안 읽음으로 뜨지 않는다');
    await openApp(READ.concat(UNREAD), null);
    const i7 = await info();
    check('줄 없음·맨 아래·막대 없음', !i7.hasDiv && i7.nearBottom && i7.marks.length === 0, i7);
    check('지금까지를 읽은 것으로 적어 뒀다', Math.abs(parseFloat(i7.seen) - LAST_TS) < 0.01, i7);

    console.log('--- 8. 보고 있는 중(맨 아래)에 온 글은 바로 읽은 것');
    await openApp(READ, LAST_READ);
    await ctl('emit', { events: [E.inc(T0 + 2000, 'U7 지금 온 알림')] });
    await sleep(1500);
    const i8 = await info();
    check('막대·줄 없이 맨 아래', !i8.hasDiv && i8.nearBottom && i8.marks.length === 0, i8);
    check('seenTs 가 따라 올랐다', Math.abs(parseFloat(i8.seen) - (T0 + 2000)) < 0.01, i8);

    console.log('--- 9. 위를 읽는 중에 온 글은 끌어내리지 않고 표시만');
    await openApp(READ.concat(UNREAD), LAST_TS);
    await ev(`document.querySelector('.chat').scrollTop = 200; 0`);
    await sleep(500);
    const before9 = await info();
    await ctl('emit', { events: [E.inc(T0 + 3000, longText('U8 새 알림')), E.inc(T0 + 3010, 'U9 또 새 알림')] });
    await sleep(1600);
    const i9 = await info();
    check('읽던 자리가 그대로다(±3px)', Math.abs(i9.scrollTop - before9.scrollTop) <= 3, { before: before9.scrollTop, after: i9.scrollTop });
    check('새 글 두 개에 막대', i9.marks.includes('U8') && i9.marks.includes('U9'), i9);
    check('「여기부터 안 읽음」 줄이 새 글 앞에', i9.hasDiv && i9.next === 'U8', i9);
    check('단추에 2', i9.jumpOn && i9.jumpN === '2', i9);
    check('seenTs 는 그대로', Math.abs(parseFloat(i9.seen) - LAST_TS) < 0.01, i9);

    console.log('--- 10. 앱을 내렸다 올림(맨 아래에서 떠남) → 쌓인 알림으로');
    await openApp(READ, LAST_READ);
    await hide();
    await ctl('emit', { events: UNREAD.slice(0, 4).map((e, i) => Object.assign({}, e, { ts: T0 + 4000 + i * 10, mid: mid() })) });
    await show(5000);
    const i10 = await info();
    check('맨 아래가 아니라 안 읽은 첫 글에 앉았다', i10.hasDiv && i10.nearBottom === false, i10);
    check('줄 바로 뒤가 새로 쌓인 첫 글(U1)', i10.next === 'U1', i10);
    check('줄이 윗머리에', i10.divTop !== null && i10.divTop >= 0 && i10.divTop <= 40, i10);

    console.log('--- 11. 위를 읽다가 잠깐(2분 미만) 나갔다 옴 → 보던 자리 그대로');
    await openApp(READ.concat(UNREAD), LAST_TS);
    await ev(`document.querySelector('.chat').scrollTop = 200; 0`);
    await sleep(500);
    const b11 = await info();
    await hide();
    await ctl('emit', { events: [E.inc(T0 + 5000, longText('U10 자리 비운 사이'))] });
    await show(20000);
    const i11 = await info();
    check('보던 자리를 지켰다(±3px)', Math.abs(i11.scrollTop - b11.scrollTop) <= 3, { before: b11.scrollTop, after: i11.scrollTop });
    check('새 글은 막대와 단추로 알린다', i11.marks.includes('U10') && i11.jumpOn && i11.jumpN === '1', i11);

    console.log('--- 12. 위를 읽다가 오래(2분 넘게) 나갔다 옴 → 안 읽은 곳으로');
    await openApp(READ.concat(UNREAD), LAST_TS);
    await ev(`document.querySelector('.chat').scrollTop = 200; 0`);
    await sleep(500);
    await hide();
    await ctl('emit', { events: [E.inc(T0 + 6000, longText('U11 오래 비운 사이')), E.inc(T0 + 6010, longText('U12 하나 더')), E.inc(T0 + 6020, longText('U13 하나 더')), E.inc(T0 + 6030, longText('U14 하나 더'))] });
    await show(200000);
    const i12 = await info();
    check('안 읽은 첫 글(U11)에 앉았다', i12.hasDiv && i12.next === 'U11' && i12.divTop >= 0 && i12.divTop <= 40, i12);

    console.log('--- 13. 허락 카드가 열려 있으면 그쪽(맨 아래)이 먼저');
    await openApp(READ, LAST_READ);
    await hide();
    await ctl('emit', { events: [E.inc(T0 + 7000, longText('U15 알림')), E.inc(T0 + 7010, longText('U16 알림')), E.inc(T0 + 7020, longText('U17 알림')),
      { type: 'permission', id: 'p1', title: '명령 실행: 서버 재시작', note: '', body: '', can_always: true }] });
    await show(5000);
    const i13 = await info();
    check('맨 아래(카드 쪽)로 갔다', i13.nearBottom === true, i13);

    console.log('--- 14. 설정 같은 화면이 대화를 덮고 있으면 읽은 걸로 치지 않는다');
    await openApp(READ.concat(UNREAD), LAST_READ);
    await ev(`document.querySelector('.chat').scrollTop = 0; 0`);                 // 안 읽은 글이 아래에 있는 자리
    await sleep(300);
    const before14 = (await info()).seen;
    await ev(`document.querySelector('.drawer, .icon[aria-label^="메뉴"]') ; document.querySelector('.icon[aria-label^="메뉴"]').click(); 0`);
    await sleep(1800);
    const i14 = await info();
    check('서랍을 연 채로는 seenTs 가 오르지 않는다', i14.seen === before14, { before: before14, after: i14.seen });

    console.log('--- 15. 새 화면 받기 — 앱으로 돌아올 때 서버의 화면 버전이 다르면 스스로 새로 고침');
    const webver = v => ctl('webver', { v });
    const mark = () => ev(`window.__mark = 1; 0`);
    const marked = () => ev(`window.__mark === 1`);
    await openApp(READ, LAST_READ);
    await mark(); await hide(); await show(1000);
    check('버전이 같으면 새로 고치지 않는다', await marked());

    await webver(null);                                            // web_version.txt 가 아직 없는(배포 전) 서버
    await hide(); await show(1000);
    check('버전 파일이 없으면 새로 고치지 않는다', await marked());

    await webver('2099-01-01z');
    await openApp(READ, LAST_READ);
    await ev(`document.querySelector('#app .row textarea').value = '쓰던 글'; document.querySelector('#app .row textarea').dispatchEvent(new Event('input')); 0`);
    await mark(); await hide(); await show(1000);
    check('적던 글이 있으면 새로 고치지 않는다(글을 지키려고)', await marked());

    await openApp(READ, LAST_READ);
    await ev(`document.querySelector('.icon[aria-label^="메뉴"]').click(); 0`); await sleep(500);
    await mark(); await hide(); await show(1000);
    check('서랍이 열려 있으면 새로 고치지 않는다', await marked());

    // 실제 흐름 — 알림만 눌러 돌아왔는데 그사이 새 화면이 배포됨: 새로 고침 + 안 읽은 첫 글로
    await openApp(READ, LAST_READ);
    await mark(); await hide();
    await ctl('emit', { events: UNREAD.slice(0, 4).map((e, i) => Object.assign({}, e, { ts: T0 + 8000 + i * 10, mid: mid() })) });
    await show(5000); await sleep(1500);
    const i16 = await info();
    check('조용할 때는 스스로 새로 고쳤다', !(await marked()));
    check('새로 고친 뒤에도 안 읽은 첫 글(U1)에 앉았다', i16.hasDiv && i16.next === 'U1' && i16.nearBottom === false, i16);
    check('시도한 기록이 남았다(되풀이 막이)', await ev(`!!sessionStorage.getItem('webUpdateTry')`));

    await mark(); await hide(); await show(1000);
    check('서버가 계속 다른 버전을 말해도 2분 안엔 되풀이하지 않는다', await marked());
    await webver(WEB_VERSION);

    console.log('--- 16. 모양 — 폭 422/390/375/360 · 라이트/다크, 가로 넘침 없음');
    for (const [w, dark] of [[422, false], [390, false], [375, false], [360, false], [422, true], [360, true]]) {
      await view(w, 800, dark);
      await openApp(READ.concat(UNREAD), LAST_READ);
      const i = await info();
      check(w + 'px ' + (dark ? '다크' : '라이트') + ': 가로 넘침 없음·줄과 단추 보임', i.sw <= i.cw && i.hasDiv && i.jumpOn, i);
      if (w === 422) await shot('15_unread_' + (dark ? 'dark' : 'light') + '_' + w);
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
