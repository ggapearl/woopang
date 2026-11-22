/* 효딩쓰 휴대폰 화면 — https://woopang.com/AI/desk-app/
 * 아이폰 앱(apps/hyodingsseu-ios)의 DeskStore·Audio·UI 를 그대로 옮긴 것.
 * PC 효딩쓰 원격 API: /AI/api/desk/<sub> (ai_office/routes.py desk_proxy → server/voice/desk/remote_api.py)
 * 안드로이드 앱(Capacitor)에서는 window.Capacitor.Plugins.DeskNative 로 폰 목소리·진동·링크 열기를 쓴다.
 * 브라우저에서 열어도 동작한다(폰 목소리는 브라우저 음성이 있으면 그것, 없으면 PC 소희 목소리).
 */
'use strict';
(function () {
  const WEB_VERSION = '2026-11-22c';
  const Cap = window.Capacitor;
  const Native = (Cap && Cap.Plugins && Cap.Plugins.DeskNative) || null;
  const AppPlugin = (Cap && Cap.Plugins && Cap.Plugins.App) || null;

  // 핀치로 확대·축소되지 않게 (2026-11-20 대표님) — index.html 뷰포트(user-scalable=no) · app.css touch-action 에 더해,
  // 아이폰 웹뷰가 뷰포트 설정을 무시해도 제스처 사건·두 손가락 터치를 여기서 막는다.
  ['gesturestart', 'gesturechange', 'gestureend'].forEach(t => document.addEventListener(t, ev => ev.preventDefault(), { passive: false }));
  document.addEventListener('touchmove', ev => { if (ev.touches && ev.touches.length > 1) ev.preventDefault(); }, { passive: false });

  // ── 저장 (이 폰의 웹뷰 안에만 — 앱은 백업·기기 옮기기에서 빠진다) ──
  const local = {
    get(k, d) { try { const v = localStorage.getItem(k); return v === null ? d : v; } catch (e) { return d; } },
    set(k, v) { try { localStorage.setItem(k, v); } catch (e) { /* 저장 못 해도 화면은 돈다 */ } },
    del(k) { try { localStorage.removeItem(k); } catch (e) { /* 위와 같음 */ } },
  };

  const DEFAULT_BASE = /^https?:$/.test(location.protocol) && location.hostname !== 'localhost'
    ? location.origin + '/AI/api/desk' : 'https://woopang.com/AI/api/desk';

  // ── 모양 (Models.swift) ────────────────
  const MODEL_DEFAULTS = [{ key: 'fast', label: '빠르게' }, { key: 'normal', label: '보통' }, { key: 'deep', label: '깊게' }];
  const REPLY_VOICE = [['off', '듣지 않기'], ['voiceOnly', '말로 물었을 때만'], ['always', '항상']];
  const VOICE_ENGINE = [['phone', '폰 목소리 · 바로'], ['pc', 'PC 소희 목소리 · 조금 늦음']];
  const THEMES = [['system', '폰 설정대로'], ['light', '라이트'], ['dark', '다크']];
  const CLOSED = { allow: '허락했습니다', always: '이번 대화에선 계속 허락했습니다', deny: '거절했습니다',
    timeout: '답이 없어 하지 않았습니다', answer: '답했습니다' };
  const INCOMING = { peer: '다른 세션', 'task-notification': '작업 알림', channel: '채널', auto: '자율 점검', system: '시스템',
    phone_out: '🔔 알림' };   // 휴대폰에서 보면 「휴대폰으로 보냄」이 어색하다 — 서버 알림·AI Office 알림도 여기로 온다(2026-10-04)

  function worker(j) {
    if (!j || typeof j.id !== 'string') return null;
    const name = j.name || j.id;
    const chars = Array.from(name);
    return {
      id: j.id, name, team: j.team_label || '', role: j.role || '', color: j.color || '#8D92AE', task: j.task || '', busy: !!j.busy,
      initial: chars.length >= 2 ? chars[chars.length - 2] : (chars[0] || '?'),        // 「김효딩」 → 「효」
      get statusLine() { return [this.team, this.busy ? '일하는 중' : this.task].filter(Boolean).join(' · '); },
    };
  }

  function officeMessage(j) {
    if (!j || typeof j.content !== 'string') return null;
    return { sender: j.sender || '', name: j.sender_name || '', content: j.content, time: j.timestamp || '', get mine() { return this.sender === 'ceo'; } };
  }

  // ── 상태 (DeskStore) ───────────────────
  const S = {
    items: [], agentState: 'idle', link: { k: 'connecting' }, name: '효딩쓰', host: 'gui', model: 'normal',
    models: MODEL_DEFAULTS, workers: [], uploadingVoice: false, paired: false, pairMessage: null,
    docsNew: 0,                              // 문서함 새 자료 수 — PC 가 state·events 에 붙여 준다
    appr: null,                              // 승인함에서 기다리는 제안 수 — PC 가 state·events 에 붙여 준다(모르면 null)
    speed: parseFloat(local.get('speed', '1.2')) || 1.2,
    replyVoice: local.get('replyVoice', 'voiceOnly'),
    voiceEngine: local.get('voiceEngine', 'phone'),
    theme: local.get('theme', 'system'),
  };
  let boot = '', seq = 0, pollCtl = null, nextId = 1;
  let openAI = [], toolOwner = {}, cardOwner = {}, lastAI = null, lastAIText = '', lastUserOrigin = '';
  let speakNext = false, replaying = false;
  const picks = {};                          // 선택 카드에서 고른 것 — 카드 id → { 질문: [고른 것] }

  const busy = () => S.agentState !== 'idle';
  const offlineMessage = () => (S.link.k === 'offline' ? S.link.msg : null);
  const hostLabel = () => (S.host === 'brain' ? 'PC 뒤의 두뇌 (창이 닫혀 있어요)' : 'PC 효딩쓰 창');

  // ── PC 효딩쓰 창구 (DeskAPI) ────────────
  class DeskError extends Error {
    constructor(status, message) { super(message); this.status = status; }
  }

  const api = {
    get base() { return local.get('base', DEFAULT_BASE); },
    set base(v) { local.set('base', String(v || '').trim() || DEFAULT_BASE); },
    get token() { return local.get('desk-token', null); },
    set token(v) { if (v) local.set('desk-token', v); else local.del('desk-token'); setTimeout(syncBackupAuth, 0); },

    async request(path, o) {
      o = o || {};
      const method = o.method || 'GET';
      let url;
      try {
        url = new URL(this.base.replace(/\/+$/, '') + '/' + path);
      } catch (e) {
        throw new DeskError(0, '서버 주소가 올바르지 않아요.');
      }
      if (o.query) Object.keys(o.query).forEach(k => { const v = o.query[k]; if (v != null) url.searchParams.set(k, String(v)); });
      const headers = { Accept: 'application/json' };
      if (o.body != null) headers['Content-Type'] = o.contentType || 'application/json';
      const t = this.token;
      if (o.auth !== false && t) headers.Authorization = 'Bearer ' + t;

      const ctl = new AbortController();
      let timedOut = false;
      const timer = setTimeout(() => { timedOut = true; ctl.abort(); }, o.timeout || 30000);
      const outer = o.signal;
      const onAbort = () => ctl.abort();
      if (outer) { if (outer.aborted) ctl.abort(); else outer.addEventListener('abort', onAbort); }
      try {
        const res = await fetch(url.toString(), { method, headers, body: o.body, cache: 'no-store', credentials: 'omit', signal: ctl.signal });
        if (!res.ok) {
          let msg = null;
          try { msg = (await res.json()).error; } catch (e) { /* 모양이 JSON 이 아니면 번호만 */ }
          throw new DeskError(res.status, msg || '서버 오류 (' + res.status + ')');
        }
        return o.as === 'blob' ? await res.blob() : await res.json();
      } catch (e) {
        if (e instanceof DeskError) throw e;
        if (outer && outer.aborted) throw new DeskError(-1, '취소했어요');
        if (e && e.name === 'SyntaxError') throw new DeskError(0, '서버 답을 읽지 못했어요.');
        if (timedOut) throw new DeskError(0, 'PC 효딩쓰가 답이 늦어요.');
        if (navigator.onLine === false) throw new DeskError(0, '인터넷에 연결돼 있지 않아요.');
        throw new DeskError(0, '서버에 닿지 않아요.');
      } finally {
        clearTimeout(timer);
        if (outer) outer.removeEventListener('abort', onAbort);
      }
    },
    get(path, query, timeout, signal) { return this.request(path, { query, timeout, signal }); },
    post(path, obj, o) { return this.request(path, Object.assign({ method: 'POST', body: JSON.stringify(obj || {}) }, o || {})); },
  };

  // ── 폰 기능 ────────────────────────────
  let hapticAt = 0, sfxAt = 0;                                       // 마지막으로 울린 때 — 터치 한 번에 소리·진동이 겹치지 않게 (아래 「모든 터치」)
  const haptic = kind => { hapticAt = performance.now(); if (Native) Native.haptic({ kind }).catch(() => {}); };

  // 버튼 효과음 (2026-10-06) — 파일 없이 짧은 음 몇 개. 설정 › 화면 › 버튼 효과음 으로 끈다. [주파수, 시작(초), 길이, 크기]
  const SFX = {
    tap: [[1150, 0, 0.035, 0.045]],
    start: [[660, 0, 0.07, 0.08], [990, 0.08, 0.09, 0.08]],
    stop: [[990, 0, 0.07, 0.07], [660, 0.08, 0.1, 0.07]],
    send: [[740, 0, 0.05, 0.06], [1180, 0.05, 0.08, 0.05]],
    menu: [[880, 0, 0.03, 0.035]],
    del: [[520, 0, 0.06, 0.05], [390, 0.06, 0.08, 0.045]],
    error: [[220, 0, 0.16, 0.06]],
  };
  const sfx = {
    ctx: null,
    play(kind) {
      sfxAt = performance.now();
      if (local.get('sfx', '1') !== '1') return;
      try {
        const AC = window.AudioContext || window.webkitAudioContext;
        if (!AC) return;
        if (!this.ctx) this.ctx = new AC();
        if (this.ctx.state === 'suspended') this.ctx.resume();
        const t0 = this.ctx.currentTime + 0.01;
        (SFX[kind] || SFX.tap).forEach(([f, at, dur, vol]) => {
          const o = this.ctx.createOscillator(), g = this.ctx.createGain();
          o.type = 'sine';
          o.frequency.value = f;
          g.gain.setValueAtTime(0.0001, t0 + at);
          g.gain.exponentialRampToValueAtTime(vol, t0 + at + 0.008);
          g.gain.exponentialRampToValueAtTime(0.0001, t0 + at + dur);
          o.connect(g).connect(this.ctx.destination);
          o.start(t0 + at);
          o.stop(t0 + at + dur + 0.02);
        });
      } catch (e) { /* 소리가 안 나도 버튼은 그대로 */ }
    },
  };

  // 이 폰 안 보관함(IndexedDB) — 보내지 못한 녹음을 앱을 다시 열어도 남겨 둔다 (2026-10-06)
  const idb = {
    db: null,
    open() {
      if (this.db) return Promise.resolve(this.db);
      return new Promise((res, rej) => {
        const r = indexedDB.open('hyodingsseu', 1);
        r.onupgradeneeded = () => r.result.createObjectStore('kv');
        r.onsuccess = () => { this.db = r.result; res(r.result); };
        r.onerror = () => rej(r.error);
      });
    },
    async run(mode, fn) {
      try {
        const db = await this.open();
        return await new Promise((res, rej) => {
          const tx = db.transaction('kv', mode), q = fn(tx.objectStore('kv'));
          tx.oncomplete = () => res(q && q.result);
          tx.onerror = () => rej(tx.error);
        });
      } catch (e) { return undefined; }
    },
    get(k) { return this.run('readonly', s => s.get(k)); },
    put(k, v) { return this.run('readwrite', s => s.put(v, k)); },
    del(k) { return this.run('readwrite', s => s.delete(k)); },
  };

  /** PC 문서 경로 → PC 효딩쓰가 서명한 링크(30일)를 받아 앱 밖(브라우저)에서 연다 */
  // 글·그림·소리처럼 간단한 파일은 앱 안 보기로 연다 (2026-11-02 대표님: 앱 밖 브라우저로 나가지 말고 앱 안에서). 폴더·PDF 같은 건 예전처럼 밖으로.
  const INAPP_DOC = /\.(txt|md|markdown|csv|json|log|html?|pdf|docx|xlsx|pptx|hwpx?|hml|png|jpe?g|gif|webp|mp4|m4v|mov|mp3|m4a|wav)$/i;
  const baseName = p => String(p || '').split(/[?#]/)[0].split(/[\\/]/).pop();

  async function openDoc(path) {
    const inApp = INAPP_DOC.test(String(path).split(/[?#]/)[0]);
    const w = Native || inApp ? null : window.open('', '_blank');   // 브라우저: 누른 순간 창을 열어 두어야 막히지 않는다
    try {
      const r = await api.get('doc', { path });
      if (inApp) openViewer([{ url: r.url, name: baseName(path) }], 0, baseName(path));
      else if (w) w.location = r.url; else openLink(r.url);
    } catch (e) {
      if (w) w.close();
      show(e);
    }
  }

  function openLink(url) {
    if (Native) Native.openExternal({ url }).catch(e => toast(e.message || '열 수 없어요'));
    else window.open(url, '_blank', 'noopener');
  }

  /** 내려받기 (2026-10-26) — 서버가 Content-Disposition: attachment 로 주는 주소(?dl=1)를 기기 브라우저가 받는다.
   *  앱 안(웹뷰)은 파일 저장을 못 해서 기본 브라우저로 넘기고, 브라우저는 <a download> 로 바로 받는다. 효딩쓰 문서 주소(/AI/doc/)만. */
  const MEDIA_EXT = /\.(jpe?g|png|gif|webp|heic|heif|mp4|mov|m4v)$/i;
  const PHOTO_OR_DOC = /\/AI\/(doc|photo)\//, PHOTO_URL = /\/AI\/photo\//;                  // 사진첩 주소(/AI/photo/)도 문서 주소처럼 저장·공유한다 (2026-11-20)
  const isMediaFile = (url, name) => MEDIA_EXT.test(String(name || nameFromUrl(url)).split(/[?#]/)[0]);

  /** 사진·영상 저장 (2026-11-16 대표님: 「파일함으로 가지 않고 다운로드 누르면 핸드폰 갤러리에」) — 앱의 폰 기능(saveToPhotos)이 사진첩에 바로 넣는다.
   *  앱이 그 기능을 아직 모르는 옛 빌드면 공유 창(파일만)으로 넘겨 「이미지 저장」을 누르게 한다. 사진·영상이 아니면 false(아래 내려받기로). */
  /** 앱의 폰 기능(saveToPhotos)은 /AI/doc/ 가 든 주소만 받는다 — 사진첩 주소(/AI/photo/)는 같은 내용을 주는 /AI/doc/photo/ 로 바꿔 넘긴다.
   *  (그대로 넘기면 「허용되지 않는 주소」로 조용히 거절돼 저장이 안 됐다 — 2026-11-22) */
  const nativeSaveUrl = url => new URL(url, location.href).href.replace('/AI/photo/', '/AI/doc/photo/');

  async function saveToGallery(url, name) {
    if (!Native || !PHOTO_OR_DOC.test(url) || !isMediaFile(url, name)) return false;
    if (typeof Native.saveToPhotos === 'function') {
      try {
        await Native.saveToPhotos({ url: nativeSaveUrl(url), name: name || nameFromUrl(url) });
        toast('폰에 저장했어요');
      } catch (e) { toast((e && e.message) || '저장하지 못했어요'); }
      return true;
    }
    toast('공유 창에서 「이미지 저장」을 눌러 주세요');
    shareFiles([{ url, name }]);
    return true;
  }

  /** 글 문서(html·md·txt·csv)는 저장·공유할 때 PDF 로 (2026-11-18 대표님: 「문서는 PDF, 사진은 사진으로 — 브라우저로 html 공유하지 말 것」).
   *  서버가 ?pdf=1 로 PDF 를 만들어 준다(크롬 인쇄 — 처음엔 몇 초 걸리고 그 뒤엔 저장본). 사진·영상·음성·pdf 는 그대로. */
  const TEXT_DOC = /\.(html?|md|markdown|txt|csv|docx|xlsx|pptx|hwpx?|hml)$/i;
  function docAsPdf(f) {
    const nm = f.name || nameFromUrl(f.url);
    if (!/\/AI\/doc\//.test(f.url) || !TEXT_DOC.test(String(nm).split(/[?#]/)[0])) return f;
    return { url: f.url + (f.url.includes('?') ? '&' : '?') + 'pdf=1', name: nm.replace(TEXT_DOC, '') + '.pdf' };
  }

  /** 파일을 받아 둔다 — 같은 파일은 한 번만(PDF 는 만드는 데 몇 초 걸려서, 열어 볼 때 미리 받아 두면 공유 단추가 바로 먹는다) */
  const filePromises = new Map();
  function prepFile(f) {
    const key = f.url;
    if (!filePromises.has(key)) {
      const p = (async () => {
        const res = await fetch(f.url);
        if (!res.ok || Number(res.headers.get('content-length') || 0) > 80 * 1024 * 1024) { if (res.body) res.body.cancel(); throw new Error('size'); }
        const b = await res.blob();
        return new File([b], f.name || nameFromUrl(f.url), { type: b.type || 'application/octet-stream' });
      })();
      p.catch(() => filePromises.delete(key));
      filePromises.set(key, p);
      while (filePromises.size > 6) filePromises.delete(filePromises.keys().next().value);      // 사진을 넘겨 볼 때마다 쌓이지 않게 — 최근 몇 개만
    }
    return filePromises.get(key);
  }
  const warmDoc = f => { const p = docAsPdf(f); if (p !== f) prepFile(p).catch(() => {}); };

  function downloadFile(url, name) {
    if (!url) return;
    if (!PHOTO_OR_DOC.test(url)) { openLink(url); return; }
    if (Native && isMediaFile(url, name)) { saveToGallery(url, name); return; }
    if (Native || (!PHOTO_URL.test(url) && navigator.canShare && /Android|iPhone|iPad/i.test(navigator.userAgent))) {                  // 폰 — 브라우저로 넘기지 않고 파일만 공유 창으로(「파일에 저장」·카톡·메일)
      toast('공유 창에서 「파일에 저장」을 눌러 주세요');
      shareFiles([{ url, name }]);
      return;
    }
    const f = docAsPdf({ url, name });
    const u = f.url + (f.url.includes('?') ? '&' : '?') + 'dl=1' + (PHOTO_URL.test(f.url) && f.name ? '&n=' + encodeURIComponent(f.name) : '');
    toast('내려받는 중이에요');
    const a = h('a', { href: u, download: f.name || '', rel: 'noopener', style: 'display:none' });
    document.body.append(a);
    a.click();
    a.remove();
  }

  /** 공유 (2026-10-29) — 사진·영상·PDF 파일 자체를 기기의 공유 창으로(카톡·인스타·메일 등). 링크나 브라우저로는 보내지 않는다(2026-11-18). */
  async function shareFiles(list, title) {
    list = (list || []).filter(f => f && f.url).slice(0, 10).map(docAsPdf);
    if (!list.length) return;
    const files = [];
    try {
      if (list.some(f => /\.pdf$/i.test(f.name || ''))) toast('PDF 만드는 중이에요 — 잠시만요');      // 사진·영상은 안내 없이 바로 공유 창 (2026-11-22 대표님)
      for (const f of list) files.push(await prepFile(f));
    } catch (err) { toast(err && err.message === 'size' ? '파일이 커서(80MB 넘음) 바로 공유하지 못해요 — 저장을 눌러 주세요' : '파일을 준비하지 못했어요'); return; }
    // 제목·글을 같이 넘기면 받는 앱에 사진과 글이 함께 간다(2026-11-16 대표님) — 파일만 넘긴다
    if (navigator.share && navigator.canShare && navigator.canShare({ files })) {
      try { await navigator.share({ files }); }
      catch (err) {
        if (err && err.name === 'AbortError') return;
        toast(err && err.name === 'NotAllowedError' ? '준비됐어요 — 한 번 더 눌러 주세요' : '공유하지 못했어요');   // 만드는 동안 누른 시간이 지났다 — 다시 누르면 바로
      }
      return;
    }
    if (Native) { toast('이 기기에서는 파일을 공유할 수 없어요'); return; }
    files.forEach((file, i) => setTimeout(() => {         // PC 브라우저 — 파일로 받는다
      const a = h('a', { href: URL.createObjectURL(file), download: file.name, style: 'display:none' });
      document.body.append(a); a.click(); a.remove(); setTimeout(() => URL.revokeObjectURL(a.href), 4000);
    }, i * 600));
  }

  const nameFromUrl = u => { try { return decodeURIComponent(String(u).split(/[?#]/)[0].split('/').pop()) || 'file'; } catch (e) { return 'file'; } };

  const sleep = (ms, signal) => new Promise(res => {
    const t = setTimeout(res, ms);
    if (signal) signal.addEventListener('abort', () => { clearTimeout(t); res(); }, { once: true });
  });

  // ── 켜고 끄기 ──────────────────────────
  function startPolling() {
    if (pollCtl || !S.paired) return;
    pollCtl = new AbortController();
    pollLoop(pollCtl);
    registerPush();
  }

  // 아이폰 앱 알림 (2026-10-01) — 포장지(2.0.1+)가 pushRegister 를 주면 알림 허락을 받고 기기 토큰을 PC 에 맡긴다
  // (PC 가 애플에 직접 보낸다). 안드로이드·브라우저·예전 포장지엔 없어서 그냥 지나간다. 켤 때마다 한 번 — 토큰이 바뀌어도 따라간다.
  let pushAsked = false;
  async function registerPush() {
    if (pushAsked || !Native || typeof Native.pushRegister !== 'function') return;
    pushAsked = true;
    try {
      const r = await Native.pushRegister();
      if (r && r.token) await api.post('push', { token: r.token, platform: r.platform || 'ios' });
    } catch (e) { /* 알림이 없어도 앱은 그대로 */ }
  }

  function stopPolling() {
    if (pollCtl) { pollCtl.abort(); pollCtl = null; }
  }

  function reload() {
    stopPolling();
    boot = '';
    startPolling();
  }

  // 앱으로 돌아오면 처음부터 다시 받지 않고 멈춘 곳(boot·seq)부터 이어 받는다 — 목록을 통째로 다시 그리면 아이폰이
  // 맨 아래보다 더 내려간 빈 곳을 보여 줄 때가 있었다(10/12 대표님: 알림을 눌러 들어가면 대화가 안 보이고 위로 올려야 보임).
  // PC 쪽이 바뀌었거나(다시 켬·창↔두뇌) 놓친 게 너무 많으면 PC 가 reset 을 주고, 그때만 처음부터 받는다.
  async function pollLoop(ctl) {
    let delay = 1;
    let wait = 0;                                       // 이어 받을 때 첫 번은 바로 — 승인함 숫자·바로 누르기를 곧장 맞춘다
    while (!ctl.signal.aborted) {
      try {
        if (!boot) {
          if (S.link.k !== 'online') setLink({ k: 'connecting' });
          const st = await api.get('state', null, 30000, ctl.signal);
          if (ctl.signal.aborted) return;
          ingestState(st);
          setLink({ k: 'online' });                     // 지금까지를 받았으면 연결된 것 — 첫 긴 폴링(최대 25초)을 기다리지 않는다
          if (!S.workers.length) refreshWorkers();
        }
        const r = await api.get('events', { since: seq, boot, wait }, 40000, ctl.signal);
        if (ctl.signal.aborted) return;
        setLink({ k: 'online' });
        delay = 1;
        wait = 25;
        if (r.reset) { boot = ''; continue; }
        const stick = nearBottom();
        const land = landAfterAway;
        let force = false;
        (r.events || []).forEach(e => {
          if (apply(e)) force = true;
          if (typeof e.seq === 'number') seq = Math.max(seq, e.seq);
        });
        awayBatch = false;                              // 앱으로 돌아와 이어 받은 첫 응답만 「자리를 비운 사이」 — 그 뒤는 지금 오는 글
        landAfterAway = false;
        if (typeof r.seq === 'number') seq = Math.max(seq, r.seq);
        if (typeof r.state === 'string') S.agentState = r.state;
            takeAppr(r.appr);
        takeDocs(r.docs_new);
        renderStatus();
        if (pushLand(true)) scanSeen();                 // 누른 알림의 그 말이 이제 왔다 (10/19)
        else if (!(land && Date.now() > pushHeldUntil && landUnreadAfterAway())) afterChange(stick || force, (r.events || []).length > 0);
      } catch (e) {
        if (ctl.signal.aborted) return;
        if (e.status === 401) { unpair(e.message, false); return; }
        setLink({ k: 'offline', msg: e.message || 'PC 효딩쓰에 닿지 않아요' });
        await sleep(delay * 1000, ctl.signal);
        delay = Math.min(delay * 2, 20);
        if (e.status > 0) boot = '';                    // PC 가 오류로 답했으면 처음부터 — 인터넷이 잠깐 끊긴 것(0)은 멈춘 곳부터 이어서
      }
    }
  }

  function setLink(l) {
    S.link = l;
    renderStatus();
  }

  // ── 새 화면 받기 (2026-10-18) ──
  // 알림을 눌러 돌아오는 앱은 껐다 켠 게 아니라 옛 화면 코드를 쥔 채라서, 배포한 새 화면이 안 보였다.
  // 돌아올 때 서버의 web_version.txt(deploy_hyodingsseu_web.py 가 WEB_VERSION 을 적는다)를 보고 다르면 스스로 새로 고친다.
  // 쓰던 것(적던 글·녹음·열어 둔 화면)이 있으면 건드리지 않고 다음에 돌아올 때로 미룬다.
  const UPDATE_KEY = 'webUpdateTry';
  const UPDATE_RETRY_MS = 120000;
  let updateChecking = false;

  async function checkWebUpdate() {
    if (updateChecking) return;
    updateChecking = true;
    try {
      const r = await fetch('web_version.txt?t=' + Date.now(), { cache: 'no-store' });
      if (!r.ok) return;
      const v = (await r.text()).trim();
      if (!/^[\w.+-]{3,40}$/.test(v) || v === WEB_VERSION) return;
      const ta = document.querySelector('#app .row textarea');
      if (recorder.recording || voice.pending || pages.length || layers.length || (ta && ta.value.trim())) return;
      let tried = null;
      try { tried = JSON.parse(sessionStorage.getItem(UPDATE_KEY) || 'null'); } catch (e) { /* 처음 */ }
      if (tried && tried.v === v && Date.now() - tried.at < UPDATE_RETRY_MS) return;     // 새로 고쳤는데도 그대로면 되풀이하지 않는다
      try { sessionStorage.setItem(UPDATE_KEY, JSON.stringify({ v, at: Date.now() })); } catch (e) { /* 못 적어도 한 번은 */ }
      location.reload();
    } catch (e) { /* 연결이 없으면 다음에 돌아올 때 */ } finally { updateChecking = false; }
  }

  function appVisible(active) {
    if (active) {
      checkWebUpdate();
      onAppShown();                                     // 자리를 비운 사이 쌓인 글이 오면 안 읽은 곳으로 (10/18)
      takePushOpen();                                   // 알림을 눌러 왔으면 그 말로 (10/19)
      if (S.paired) startPolling();
      if (atBottom) settleBottom();                     // 보던 맨 아래 그대로 — 아이폰이 돌아올 때 빈 곳을 보여 주던 것(10/12)
      scanSeen();
    } else {
      onAppHidden();
      stopPolling();                                    // 45초 넘게 안 보이면 PC 가 텔레그램으로도 보낸다
      if (recorder.recording) interruptRecording();     // 버리지 않고 남겨 둔다 — 돌아와서 보내기 (10/6)
    }
  }

  // ── 짝 짓기 ────────────────────────────
  async function pair(code) {
    try {
      let device = '안드로이드';
      if (Native) {
        try { device = (await Native.deviceName()).name || device; } catch (e) { /* 기본 이름 */ }
      } else {
        device = '휴대폰 브라우저';
      }
      const r = await api.post('pair', { code, device: device.slice(0, 40) }, { auth: false });
      if (!r.token) return '연결하지 못했어요.';
      api.token = r.token;
      S.pairMessage = null;
      S.items = [];
      seenTs = 0;                                       // 새로 짝 지으면 읽은 자리도 처음부터 — 첫 화면은 전부 읽은 것으로 시작
      local.del(SEEN_KEY);
      holes.clear();
      local.del(HOLES_KEY);
      S.paired = true;
      historyState = 'idle';
      historyAutoTried = false;
      showMain();
      startPolling();
      return null;
    } catch (e) {
      return e.message;
    }
  }

  function unpair(message, tellServer) {
    if (tellServer !== false) api.post('unpair').catch(() => {});      // PC 쪽 열쇠도 지운다
    stopPolling();
    speaker.stop();
    if (recorder.recording) recorder.cancel();
    setTimeout(() => { api.token = null; }, 400);
    S.paired = false;
    S.items = [];
    replyTo = null;
    boot = '';
    seq = 0;
    historyState = 'idle';
    historyAutoTried = false;
    S.pairMessage = message || null;
    showPair();
  }

  // ── 보내기 ─────────────────────────────
  function send(text) {
    const t = String(text || '').trim();
    if (!t) return;
    speaker.stop();
    haptic('tap');
    fire('send', { text: t });
  }

  // ── 사진·파일 첨부 (2026-10-29 대표님 「모바일에서도 사진 붙여 넣기 · 입력칸 왼쪽 + 로 사진·파일 첨부」) ──
  // 고른 것은 PC 에 올려 두고(upload) 그 경로를 말과 함께 보낸다(send 의 files) — 효딩쓰가 그림을 직접 본다.
  const ATT_MAX = 25 * 1024 * 1024, ATT_COUNT = 8, ATT_SMALL = 3 * 1024 * 1024, ATT_SIDE = 2048;
  const pending = [];                                   // [{ blob, name, url(그림 미리보기), kind: 'image'|'file' }]
  let attUploading = false;

  const hhmmss = () => { const d = new Date(), z = n => String(n).padStart(2, '0'); return z(d.getHours()) + z(d.getMinutes()) + z(d.getSeconds()); };

  /** 큰 사진은 긴 변 2048px 로 줄여 JPEG 로 — 올리는 시간·PC 가 읽는 부담을 줄인다. 못 읽는 모양(HEIC 등)은 원본 그대로. */
  async function prepAttach(file) {
    let blob = file, name = file.name || '', type = file.type || '';
    const isImg = /^image\//i.test(type) || /\.(png|jpe?g|webp|gif|heic|heif)$/i.test(name);
    if (!name || /^image\.\w+$/.test(name)) name = '사진_' + hhmmss() + (type === 'image/png' ? '.png' : '.jpg');
    if (isImg && !/gif/i.test(type) && window.createImageBitmap) {
      try {
        const bmp = await createImageBitmap(file);
        const k = Math.min(1, ATT_SIDE / Math.max(bmp.width, bmp.height));
        if (k < 1 || file.size > ATT_SMALL) {
          const c = document.createElement('canvas');
          c.width = Math.max(1, Math.round(bmp.width * k)); c.height = Math.max(1, Math.round(bmp.height * k));
          const g = c.getContext('2d');
          g.fillStyle = '#fff'; g.fillRect(0, 0, c.width, c.height);
          g.drawImage(bmp, 0, 0, c.width, c.height);
          const out = await new Promise(res => c.toBlob(res, 'image/jpeg', 0.88));
          if (out && out.size < file.size) { blob = out; name = name.replace(/\.[^.]+$/, '') + '.jpg'; }
        }
        if (bmp.close) bmp.close();
      } catch (e) { /* 원본 그대로 */ }
    }
    const kind = isImg ? 'image' : 'file';
    return { blob, name, kind, url: kind === 'image' ? URL.createObjectURL(blob) : '' };
  }

  async function addAttach(files) {
    for (const f of Array.from(files || [])) {
      if (pending.length >= ATT_COUNT) { toast('한 번에 ' + ATT_COUNT + '개까지 보낼 수 있어요'); break; }
      const p = await prepAttach(f);
      if (p.blob.size > ATT_MAX) { toast(p.name + ' — 25MB 가 넘어 보낼 수 없어요'); if (p.url) URL.revokeObjectURL(p.url); continue; }
      pending.push(p);
    }
    renderAttach();
    const ta = $('#main textarea');
    if (ta) ta.focus();
  }

  function dropAttach(i) {
    const [p] = pending.splice(i, 1);
    if (p && p.url) URL.revokeObjectURL(p.url);
    renderAttach();
  }

  /** 입력칸 위의 첨부 띠 — 그림은 작은 미리보기, 파일은 이름. ✕ 로 뺀다 */
  function renderAttach() {
    const strip = $('#main .att-strip');
    if (!strip) return;
    strip.hidden = !pending.length;
    strip.textContent = '';
    pending.forEach((p, i) => strip.append(h('div', { class: 'att-chip' + (p.kind === 'image' ? '' : ' file') },
      p.kind === 'image' ? h('img', { src: p.url, alt: p.name }) : h('span', { text: '📎 ' + p.name }),
      h('button', { class: 'x', 'aria-label': '빼기', html: ICON.close, disabled: attUploading, onclick: () => dropAttach(i) }))));
    if (attUploading) strip.append(h('i', { class: 'spinner att-spin' }));
    renderSend();
  }

  /** 첨부가 있는 말 보내기 — 한 개씩 올린 뒤 경로를 말과 함께. 성공하면 true */
  async function sendAttached(text) {
    if (attUploading || !pending.length) return false;
    attUploading = true;
    renderAttach();
    haptic('tap');
    try {
      const paths = [];
      for (const p of pending) {
        const r = await api.request('upload', { method: 'POST', body: p.blob, contentType: p.blob.type || 'application/octet-stream', query: { name: p.name }, timeout: 110000 });
        paths.push(r.path);
      }
      await api.post('send', { text: String(text || '').trim(), files: paths });
      pending.splice(0).forEach(p => { if (p.url) URL.revokeObjectURL(p.url); });
      return true;
    } catch (e) {
      show(e);
      return false;
    } finally {
      attUploading = false;
      renderAttach();
    }
  }

  /** 입력칸 왼쪽 + — 사진·동영상 / 카메라 / 파일. 고르면 첨부 띠에 올라온다 */
  function attachMenu(anchor) {
    const r = anchor.getBoundingClientRect();
    const pick = (accept, camera) => {
      close();
      const inp = h('input', { type: 'file', accept, style: 'display:none' });
      inp.multiple = !camera;
      if (camera) inp.setAttribute('capture', 'environment');
      inp.addEventListener('change', () => { addAttach(inp.files); inp.remove(); });
      document.body.append(inp);
      inp.click();
    };
    const menu = h('div', { class: 'menu att-menu', role: 'menu', style: 'left:12px;bottom:' + Math.max(8, innerHeight - r.top + 6) + 'px' },
      h('button', { role: 'menuitem', onclick: () => pick('image/*,video/*', false) }, h('i', { text: '🖼' }), '사진·동영상'),
      h('button', { role: 'menuitem', onclick: () => pick('image/*', true) }, h('i', { text: '📷' }), '카메라로 찍기'),
      h('button', { role: 'menuitem', onclick: () => pick('*/*', false) }, h('i', { text: '📎' }), '파일'));
    const scrim = h('div', { class: 'menu-scrim', onclick: () => close() });
    const close = layer(() => { scrim.remove(); menu.remove(); });
    $('#app').append(scrim, menu);
  }

  /** 복사해 둔 사진 붙여 넣기 — 입력칸에 붙이면 첨부로 (글 붙여 넣기는 그대로) */
  function pasteAttach(ev) {
    const cd = ev.clipboardData;
    if (!cd) return;
    const files = Array.from(cd.files || []).filter(f => f && f.size);
    const items = files.length ? files : Array.from(cd.items || []).filter(i => i.kind === 'file').map(i => i.getAsFile()).filter(Boolean);
    if (!items.length) return;
    ev.preventDefault();
    addAttach(items);
  }

  function decide(id, decision) {
    haptic('tap');
    fire('decide', { id, decision });
  }

  const answer = (id, answers) => fire('answer', { id, answers });
  function stop() { speaker.stop(); fire('stop'); }
  const newChat = () => fire('new');

  function setModel(key) {
    S.model = key;
    renderStatus();
    fire('model', { key });
  }

  function commitSpeed() {
    local.set('speed', String(S.speed));
    fire('speed', { speed: S.speed });
  }

  async function fire(path, body) {
    try {
      await api.post(path, body || {});
    } catch (e) {
      show(e);
    }
  }

  // ── 말로 ───────────────────────────────
  const recorder = {
    recording: false, level: 0, mr: null, stream: null, chunks: [], ctx: null, timer: 0, meter: 0,

    async start() {
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia || !window.MediaRecorder) return false;
      let stream;
      try {
        stream = await navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true } });
      } catch (e) {
        return false;
      }
      const type = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4', 'audio/ogg;codecs=opus'].find(t => MediaRecorder.isTypeSupported(t)) || '';
      let mr;
      try {
        mr = new MediaRecorder(stream, type ? { mimeType: type, audioBitsPerSecond: 32000 } : undefined);
      } catch (e) {
        stream.getTracks().forEach(t => t.stop());
        return false;
      }
      this.stream = stream;
      this.mr = mr;
      this.chunks = [];
      mr.ondataavailable = e => { if (e.data && e.data.size) this.chunks.push(e.data); };
      mr.start(250);
      this.recording = true;
      this.watchLevel(stream);
      this.timer = setTimeout(() => finishRecording(), 60000);        // 60초가 지나면 저절로 보낸다
      return true;
    },

    watchLevel(stream) {
      try {
        const AC = window.AudioContext || window.webkitAudioContext;
        this.ctx = new AC();
        const an = this.ctx.createAnalyser();
        an.fftSize = 1024;
        this.ctx.createMediaStreamSource(stream).connect(an);
        const buf = new Float32Array(an.fftSize);
        this.meter = setInterval(() => {
          an.getFloatTimeDomainData(buf);
          let sum = 0;
          for (let i = 0; i < buf.length; i++) sum += buf[i] * buf[i];
          const db = 20 * Math.log10(Math.sqrt(sum / buf.length) + 1e-6);
          this.level = Math.max(0, Math.min(1, (db + 50) / 45));
          renderLevel();
        }, 80);
      } catch (e) {
        this.level = 0;
      }
    },

    /** 녹음을 끝내고 소리를 돌려준다 */
    stop() {
      return new Promise(resolve => {
        const mr = this.mr;
        if (!mr) { resolve(null); return; }
        mr.onstop = () => {
          const blob = new Blob(this.chunks, { type: mr.mimeType || 'audio/webm' });
          this.finish();
          resolve(blob);
        };
        try { mr.stop(); } catch (e) { this.finish(); resolve(null); }
      });
    },

    cancel() {
      if (this.mr) {
        this.mr.onstop = null;
        try { this.mr.stop(); } catch (e) { /* 이미 멈춤 */ }
      }
      this.finish();
    },

    finish() {
      clearTimeout(this.timer);
      clearInterval(this.meter);
      if (this.stream) this.stream.getTracks().forEach(t => t.stop());      // 마이크 표시등을 끈다
      if (this.ctx) this.ctx.close().catch(() => {});
      this.mr = null;
      this.stream = null;
      this.ctx = null;
      this.chunks = [];
      this.recording = false;
      this.level = 0;
      renderLevel();
    },
  };

  // 녹음 (2026-10-06) — 녹음 중엔 입력칸 자리에 붉은 띠(● 시간·소리 막대·✕). 마이크나 보내기를 누르면 보낸다.
  // 앱이 뒤로 가거나(잠금·앱 바꾸기) 올리다 끊기면 녹음을 이 폰에 남겨 두고, 보내기를 누르면 다시 보낸다(앱을 다시 열어도).
  const voice = { pending: null, startedAt: 0, tick: 0 };
  const clock = ms => { const s = Math.max(0, Math.round(ms / 1000)); return Math.floor(s / 60) + ':' + String(s % 60).padStart(2, '0'); };

  function startTick() {
    clearInterval(voice.tick);
    voice.tick = setInterval(renderRecStrip, 500);
  }
  function stopTick() { clearInterval(voice.tick); voice.tick = 0; }

  async function toggleRecording() {
    if (recorder.recording) { finishRecording(); return; }
    if (S.uploadingVoice) return;
    if (voice.pending) { toast('보내지 못한 녹음이 있어요 — 보내기를 누르거나 ✕ 로 버려 주세요.'); return; }
    speaker.stop();
    sfx.play('start');
    await sleep(180);                                       // 시작음이 녹음에 섞이지 않게
    if (await recorder.start()) {
      haptic('tap');
      voice.startedAt = Date.now();
      startTick();
    } else {
      sfx.play('error');
      toast('마이크를 쓸 수 없어요 — 설정 › 애플리케이션 › 효딩쓰 › 권한 › 마이크를 켜 주세요.');
    }
    renderStatus();
  }

  /** 녹음을 끝낸다 — 짧으면 버리고, 아니면 녹음본(보관할 것)을 돌려준다 */
  async function stopRecording() {
    const ms = Date.now() - voice.startedAt;
    const blob = await recorder.stop();
    stopTick();
    renderStatus();
    if (!blob || blob.size <= 3000) return null;
    return { blob, ms, ts: Date.now() };
  }

  async function finishRecording() {
    if (!recorder.recording) return;
    sfx.play('stop');
    const v = await stopRecording();
    if (!v) { toast('너무 짧아요. 누르고 말씀한 뒤 다시 누르세요.'); return; }
    await uploadVoice(v);
  }

  async function uploadVoice(v) {
    if (S.uploadingVoice) return;
    voice.pending = v;
    idb.put('voice', v);                                    // 올리는 사이 앱이 닫혀도 남게
    S.uploadingVoice = true;
    renderStatus();
    try {
      const r = await api.request('voice', { method: 'POST', body: v.blob, contentType: v.blob.type || 'audio/webm', timeout: 100000 });
      voice.pending = null;
      idb.del('voice');
      if (!String(r.text || '').trim()) toast('잘 들리지 않았어요. 다시 말씀해 주세요.');
      else { speakNext = true; sfx.play('send'); }
    } catch (e) {
      sfx.play('error');
      if (e && e.status === 401) { show(e); return; }
      toast('녹음을 보내지 못했어요 — 보내기를 누르면 다시 보내요.');
    } finally {
      S.uploadingVoice = false;
      renderStatus();
    }
  }

  async function interruptRecording() {
    const v = await stopRecording();
    if (v) { voice.pending = v; idb.put('voice', v); }
    renderStatus();
  }

  function cancelRecording() {
    recorder.cancel();
    stopTick();
    sfx.play('del');
    renderStatus();
  }

  function discardVoice() {
    voice.pending = null;
    idb.del('voice');
    sfx.play('del');
    renderStatus();
  }

  async function loadPendingVoice() {
    const v = await idb.get('voice');
    if (v && v.blob && !voice.pending && !recorder.recording) { voice.pending = v; renderStatus(); }
  }

  // ── 답을 소리로 (Speaker) ───────────────
  // 폰 목소리 빠르기를 PC 목소리에 맞춘다 (2026-10-02) — 목소리마다 1배속 빠르기가 달라 같은 「1.1배」라도
  // 아이폰 목소리가 PC 소희보다 두 배쯤 빨랐다. PC 가 알려 준 제 빠르기(초당 음절)에 맞춰 배속을 고르고,
  // 끝까지 읽을 때마다 실제로 걸린 시간을 재서 이 폰 목소리의 빠르기를 바로잡는다(음절이 적은 짧은 말은 안 잰다).
  const pace = { pc: 0, phone: parseFloat(local.get('phonePace', '0')) || 0, run: null };
  const syllables = (t) => (String(t).match(/[가-힣]/g) || []).length;

  function phoneRate(speed) {
    const r = pace.pc && pace.phone ? speed * pace.pc / pace.phone : speed;
    return Math.max(0.5, Math.min(1.6, r));               // 안드로이드 TTS 가 0.5 아래는 받지 않는다
  }

  function paceDone() {
    const run = pace.run;
    pace.run = null;
    if (!run) return;
    const secs = (Date.now() - run.t0) / 1000;
    if (run.syl < 12 || secs < 2.5) return;
    const natural = run.syl / secs / run.rate;             // 이 폰 목소리의 1배속 빠르기
    pace.phone = pace.phone ? pace.phone * 0.6 + natural * 0.4 : natural;
    local.set('phonePace', pace.phone.toFixed(3));
  }

  const speaker = {
    speaking: false, via: null, audio: null, url: null, itemId: null, gen: 0,
    spare: null,                                            // 다음 묶음을 미리 올려 둔 재생기 { a, url, blob }

    set(on, via) {
      this.speaking = on;
      this.via = on ? via : null;
      if (!on) this.itemId = null;
      renderStatus();
      renderReadButtons();
    },

    async phone(text, speed) {
      const rate = phoneRate(speed);
      if (Native) {
        pace.run = { t0: Date.now(), syl: syllables(text), rate };
        await Native.speak({ text, rate });
        this.set(true, 'native');
        return;
      }
      if (window.speechSynthesis && window.SpeechSynthesisUtterance) {
        const u = new SpeechSynthesisUtterance(text);
        u.lang = 'ko-KR';
        u.rate = rate;
        u.onstart = () => { pace.run = { t0: Date.now(), syl: syllables(text), rate }; };
        u.onend = () => { if (this.via === 'web') { paceDone(); this.set(false); } };
        u.onerror = () => { pace.run = null; if (this.via === 'web') this.set(false); };
        speechSynthesis.cancel();
        speechSynthesis.speak(u);
        this.set(true, 'web');
        return;
      }
      throw new Error('폰 목소리를 쓸 수 없어요');
    },

    play(blob) {
      if (this.audio) { this.audio.onended = this.audio.onerror = null; this.audio.pause(); }
      if (this.url) URL.revokeObjectURL(this.url);
      this.url = URL.createObjectURL(blob);
      const a = this.audio = new Audio(this.url);
      a.onended = a.onerror = () => { if (this.audio === a) this.stop(); };
      return a.play().then(() => this.set(true, 'audio'));
    },

    /** 읽는 동안 받은 다음 묶음을 두 번째 재생기에 미리 올려 둔다 — 앞 묶음이 끝나자마자 곧바로 이어진다(10/25) */
    preload(blob) {
      if (!blob || /json/.test(blob.type)) return;
      this.dropSpare();
      const a = new Audio();
      a.preload = 'auto';
      const url = URL.createObjectURL(blob);
      a.src = url;
      a.load();
      this.spare = { a, url, blob };
    },

    dropSpare() {
      if (!this.spare) return;
      this.spare.a.removeAttribute('src');
      URL.revokeObjectURL(this.spare.url);
      this.spare = null;
    },

    /** PC 소희 「읽기」 묶음 하나를 끝까지 읽고 풀린다 — 다음 묶음이 이어진다. last 면 다 읽은 뒤 읽기를 끝낸다.
     *  미리 올려 둔 재생기가 있으면 그것을 틀고, 못 틀면(브라우저가 막으면) 쓰던 재생기를 다시 쓴다(iOS 는 한 번 튼 재생기를 계속 틀게 해 준다). */
    playPart(blob, last) {
      return new Promise((resolve, reject) => {
        const old = this.audio;
        const start = (a, url) => {
          if (this.url && this.url !== url) URL.revokeObjectURL(this.url);
          this.url = url;
          this.audio = a;
          a.onended = () => { if (this.audio !== a) return; if (last) this.stop(); resolve(); };
          a.onerror = () => { if (this.audio === a) this.stop(); reject(new Error('소리를 틀지 못했어요')); };
          return a.play().then(() => { if (!this.speaking) this.set(true, 'audio'); });
        };
        const fresh = () => {
          const a = old || new Audio();
          const url = URL.createObjectURL(blob);
          a.src = url;
          return start(a, url);
        };
        const sp = this.spare && this.spare.blob === blob ? this.spare : null;
        let run;
        if (sp) {
          this.spare = null;
          if (old) { old.onended = old.onerror = null; old.pause(); }
          run = start(sp.a, sp.url).catch(() => { sp.a.onended = sp.a.onerror = null; return fresh(); });
        } else run = fresh();
        run.catch(e => { this.stop(); reject(e); });
      });
    },

    /** 읽던 것·준비하던 것(PC 목소리 받는 중) 모두 멈춘다 */
    stop() {
      this.gen++;
      pace.run = null;                                      // 멈춘 건 빠르기 재기에서 뺀다
      if (this.via === 'native' && Native) Native.stop().catch(() => {});
      if (this.via === 'web' && window.speechSynthesis) speechSynthesis.cancel();
      if (this.audio) { this.audio.onended = this.audio.onerror = null; this.audio.pause(); this.audio = null; }
      if (this.url) { URL.revokeObjectURL(this.url); this.url = null; }
      this.dropSpare();
      if (this.speaking || this.itemId != null) this.set(false);
    },
  };
  if (Native) Native.addListener('speechDone', () => { if (speaker.via === 'native') { paceDone(); speaker.set(false); } });

  function testVoice() { speakReply('대표님, 이 목소리로 말씀드릴게요.'); }

  function maybeSpeak() {
    const next = speakNext;
    speakNext = false;
    if (lastUserOrigin !== 'app' || !lastAIText) return;
    if (S.replyVoice === 'off') return;
    if (S.replyVoice === 'voiceOnly' && !next) return;
    speakReply(lastAIText);
  }

  /** 답 옆 「읽기」 — 끝까지 읽는다. 읽는 중에 누르면 멈춘다. */
  function readAloud(item) {
    if (speaker.speaking || speaker.itemId != null) { speaker.stop(); return; }
    speakReply(item.text, true, item.id);
  }

  async function speakReply(text, full, itemId) {
    const clean = speechText(text, full ? 6000 : 300);
    if (!clean) return;
    speaker.stop();
    const gen = speaker.gen;
    const alive = () => speaker.gen === gen;                           // 기다리는 사이 멈춤·다른 읽기를 눌렀는지
    speaker.itemId = itemId || null;
    renderReadButtons();
    // PC 소희 — 앞 묶음부터 받아 읽고, 읽는 동안 다음 묶음을 받는다 (2026-10-21 — 예전엔 글 전체가 다 만들어질 때까지 1분 넘게 기다렸다)
    const viaPC = async () => {
      if (full) toast('소희 목소리를 만드는 중이에요 — 앞부분부터 읽어 드려요');
      const first = await api.request('tts', { method: 'POST', body: JSON.stringify({ text, full: !!full, parts: true }), timeout: 100000, as: 'blob' });
      if (!alive()) return;
      if (!/json/.test(first.type)) return speaker.play(first);       // 예전 PC 효딩쓰 — 글 전체 소리가 한 번에 왔다
      const plan = JSON.parse(await first.text());
      const part = async i => {
        for (;;) {                                                     // 아직 만드는 중이면(202) 다시 묻는다
          const t0 = Date.now();
          const b = await api.request('tts', { method: 'POST', body: JSON.stringify({ id: plan.id, part: i }), timeout: 60000, as: 'blob' });
          if (!/json/.test(b.type) || !alive()) return b;
          if (Date.now() - t0 < 300) await sleep(300);                // 곧바로 「아직」이면 잠깐 쉬고 — 쉼 없이 물으면 소리 만드는 PC 를 붙잡는다
        }
      };
      // 받기는 읽기와 따로 — 한 묶음을 받자마자 다음 묶음을 묻는다. PC 는 폰이 받아 간 것보다 하나 앞까지만
      // 만들어서(READ_AHEAD), 재생이 끝날 때까지 기다렸다 물으면 그동안 PC 가 쉬어 빈틈이 생겼다 (10/26 PC 실측)
      const parts = [];
      for (let i = 0; i < plan.n; i++) {
        parts.push(i === 0 ? part(0) : parts[i - 1].catch(() => {}).then(() => alive() ? part(i) : null));
        parts[i].catch(() => {});
      }
      for (let i = 0; i < plan.n; i++) {
        let blob = null;
        try { blob = await parts[i]; } catch (e) { if (i === 0) throw e; }   // 첫 묶음이 안 되면 폰 목소리로 · 중간 묶음은 건너뛴다
        if (!alive()) return;
        const last = i === plan.n - 1;
        if (!blob) { if (last) speaker.stop(); continue; }
        const playing = speaker.playPart(blob, last);                   // 미리 올려 둔 재생기는 여기서 바로 가져간다
        if (!last) parts[i + 1].then(b => { if (alive()) speaker.preload(b); }, () => {});
        await playing;
      }
    };
    const viaPhone = async () => { if (alive()) await speaker.phone(clean, S.speed); };
    try {
      if (S.voiceEngine === 'pc') {
        try { await viaPC(); } catch (e) { await viaPhone(); }      // PC 목소리가 안 되면 폰 목소리로
      } else {
        try { await viaPhone(); } catch (e) { await viaPC(); }      // 폰에 한국어 목소리가 없으면 PC 로
      }
    } catch (e) {
      if (!alive()) return;
      speaker.itemId = null;
      renderReadButtons();
      toast('소리로 읽지 못했어요 — ' + (e.message || '목소리를 쓸 수 없어요'));
    }
  }

  /** 화면용 글(마크다운)을 귀로 듣기 좋은 짧은 말로 — PC 의 for_speech 와 같은 규칙 */
  function speechText(text, limit) {
    let t = String(text || '');
    // 괄호 안(승인함 번호·경로·함수 이름)·#번호·6자리 번호·주소·경로는 읽지 않는다, 9/29 → 9월 29일 (대표님 지시 2026-09-28)
    const paren = /\s*[(（[][^()（）[\]]*[)）\]]/g;
    [[/```[\s\S]*?```/g, ' '], [/^\s*\|.*\|\s*$/gm, ' '], [/\[([^\]]+)\]\([^)]+\)/g, '$1'], [/https?:\/\/\S+/g, ' '],
      [/[A-Za-z]:[\\/][^\s,)）]*/g, ' '], [paren, ''], [paren, ''], [/#\d+/g, ' '],
      [/(^|[^0-9A-Za-z])(?=[0-9A-Za-z]*\d)(?=[0-9A-Za-z]*[A-Za-z])[0-9A-Za-z]{6}(?![0-9A-Za-z])/g, '$1 '],
      [/(^|[^\d])(\d{1,2})\/(\d{1,2})(?!\d)/g, (m, p, a, b) => (+a >= 1 && +a <= 12 && +b >= 1 && +b <= 31 ? p + (+a) + '월 ' + (+b) + '일' : m)],
      [/`([^`]+)`/g, '$1'], [/^[#>\-*+\s]+/gm, ''], [/[*_~#|]/g, ''], [/\s+/g, ' ']].forEach(([re, to]) => { t = t.replace(re, to); });
    t = t.trim();
    const chars = Array.from(t);
    if (chars.length <= limit) return t;
    let out = '', sentence = '';
    for (const ch of chars) {
      sentence += ch;
      if ('.!?。'.includes(ch)) {
        if (Array.from(out).length + Array.from(sentence).length > limit) break;
        out += sentence;
        sentence = '';
      }
    }
    if (!out) out = chars.slice(0, limit).join('');
    return out.trim() + ' 자세한 건 화면에 있어요.';
  }

  // ── AI Office ──────────────────────────
  async function refreshWorkers() {
    try {
      const r = await api.get('office/workers');
      const ws = (r.workers || []).map(worker).filter(Boolean);
      if (ws.length) { S.workers = ws; renderDrawer(); }
    } catch (e) { /* 사무실이 꺼져 있으면 명단만 비어 있다 */ }
  }

  async function officeHistory(id) {
    const r = await api.get('office/history', { id });
    return (r.messages || []).map(officeMessage).filter(Boolean);
  }

  const officeChat = (id, text) => api.post('office/chat', { id, text });

  // ── 알림 ───────────────────────────────
  let toastTimer = 0;
  /** action = { label, run } 이면 글 옆에 누를 수 있는 단추(예: 「되돌리기」)가 붙고, 그동안은 화면 아래쪽에 떠서 사진을 가리지 않는다 */
  function toast(text, action) {
    const el = $('#toast');
    el.textContent = '';
    el.classList.toggle('act', !!action);
    if (action) {
      el.append(h('span', { text }), h('button', { class: 'toast-act', text: action.label, onclick: () => { el.classList.remove('on'); clearTimeout(toastTimer); action.run(); } }));
    } else el.textContent = text;
    el.classList.add('on');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => el.classList.remove('on'), action ? 6500 : 3200);
  }

  function show(e) {
    if (e && e.status === 401) { unpair(e.message, false); return; }
    if (e && e.status === -1) return;
    toast((e && e.message) || '문제가 생겼어요');
  }

  // ── 사건 → 화면 ────────────────────────
  function ingestState(st) {
    boot = st.boot || '';
    seq = typeof st.seq === 'number' ? st.seq : 0;
    S.name = st.name || S.name;
    S.host = st.host || S.host;
    S.model = st.model || S.model;
    const ms = (st.models || []).filter(m => m && m.key && m.label).map(m => ({ key: m.key, label: m.label }));
    if (ms.length) S.models = ms;
    if (typeof st.speed === 'number') S.speed = st.speed;
    if (typeof st.pace === 'number') pace.pc = st.pace;
    S.agentState = st.state || 'idle';
    const ws = (st.office || []).map(worker).filter(Boolean);
    if (ws.length) S.workers = ws;
    takeAppr(st.appr);
    takeDocs(st.docs_new);

    S.items = [];
    openAI = [];
    toolOwner = {};
    cardOwner = {};
    lastAI = null;
    replaying = true;
    (st.events || []).forEach(apply);
    replaying = false;
    if (S.agentState === 'idle') finishOpen();
    S.items = S.items.filter(it => it.kind !== 'ai' || it.streaming || it.text);   // 답을 지웠으면 빈 말풍선이 남지 않게
    initSeen();
    S.items.forEach(flagUnread);
    awayBatch = false;
    landAfterAway = false;
    renderAllItems();
    renderStatus();
    renderDrawer();
    if (!pushLand(true) && !landOnUnread()) settleBottom();    // 누른 알림의 말 → 안 읽은 첫 글 → 맨 아래 (10/18·10/19)
    scanSeen();
    if (!historyAutoTried) {
      historyAutoTried = true;
      if (S.items.length < 20) loadMoreHistory();
    }
  }

  /** 사건 하나를 반영한다. 꼭 보여야 하는 것(카드·내 말)이면 true — 맨 아래로 내린다. */
  function apply(e) {
    const type = e && e.type;
    switch (type) {
      case 'user': {
        const origin = e.origin || 'typed';
        append({ kind: 'user', text: e.text || '', origin, images: e.images || [], files: e.files || [], ts: e.ts, mid: e.mid });
        lastAI = null;
        lastAIText = '';
        lastUserOrigin = origin;
        return true;
      }
      case 'text_start': {
        const it = append({ kind: 'ai', text: '', streaming: true, local: false, meta: null });
        openAI.push(it.id);
        return false;
      }
      case 'delta': {
        const piece = e.text || '';
        if (!openAI.length) openAI.push(append({ kind: 'ai', text: '', streaming: true, local: false, meta: null }).id);
        const it = find(openAI[openAI.length - 1]);
        if (it && it.kind === 'ai') { it.text += piece; it.streaming = true; paintStream(it); }
        return false;
      }
      case 'text': {
        const text = e.text || '';
        const loc = !!e.local;
        if (openAI.length) {
          const id = openAI.shift();
          update(id, it => { it.kind = 'ai'; it.text = text; it.streaming = false; it.local = loc; it.meta = null; it.ts = e.ts; it.mid = e.mid; noteArrival(it); });
          lastAI = id;
        } else {
          lastAI = append({ kind: 'ai', text, streaming: false, local: loc, meta: null, ts: e.ts, mid: e.mid }).id;
        }
        lastAIText = text;
        return false;
      }
      case 'state':
        S.agentState = e.state || S.agentState;
        if (S.agentState === 'idle') finishOpen();
        return false;
      case 'tool': {
        const id = e.id || ('t' + (nextId++));
        const entry = { id, label: e.label || '', detail: e.detail || '', sub: !!e.sub, status: 'running' };
        const last = S.items[S.items.length - 1];
        if (last && last.kind === 'tools') {
          last.list.push(entry);
          toolOwner[id] = last.id;
          paint(last);
        } else {
          toolOwner[id] = append({ kind: 'tools', list: [entry] }).id;
        }
        return false;
      }
      case 'tool_done': {
        const owner = toolOwner[e.id];
        if (!owner) return false;
        update(owner, it => {
          const t = it.list.find(x => x.id === e.id);
          if (t) t.status = e.ok === false ? 'fail' : 'ok';
        });
        return false;
      }
      case 'permission': {
        const card = { id: e.id || '', title: e.title || '허락이 필요합니다', note: e.note || '', body: e.body || '', canAlways: !!e.can_always, closed: null };
        cardOwner[card.id] = append({ kind: 'permission', card, cardTs: e.ts }).id;     // cardTs — 누른 알림 찾기용(ts 를 달면 시각이 찍힌다)
        if (!replaying) haptic('alert');
        return true;
      }
      case 'question': {
        const qs = (e.questions || []).map(q => ({
          question: q.question || '', multi: !!q.multiSelect,
          options: (q.options || []).map(o => ({ label: o.label || '', desc: o.description || '' })),
        }));
        const card = { id: e.id || '', questions: qs, closed: null };
        cardOwner[card.id] = append({ kind: 'question', card, cardTs: e.ts }).id;
        if (!replaying) haptic('alert');
        return true;
      }
      case 'permission_closed': {
        const owner = cardOwner[e.id];
        if (!owner) return false;
        update(owner, it => { if (it.card) it.card.closed = e.decision || 'closed'; });
        return false;
      }
      case 'incoming': {
        const kind = e.kind || '';
        let source = INCOMING[kind] || kind;
        if (e.from) source += ' · ' + e.from;
        const buttons = e.buttons || null;
        append({ kind: 'incoming', source, text: e.text || '', auto: kind === 'auto' || kind === 'system', out: kind === 'phone_out',
          images: e.images || [], files: e.files || [], buttons, btnState: buttons && buttons.length ? { busy: false, doneIndex: null, done: [] } : null, ts: e.ts, mid: e.mid });
        lastAI = null;
        return false;
      }
      case 'note':
        append({ kind: 'note', text: e.text || '', ts: e.ts, mid: e.mid });
        return false;
      case 'error':
        append({ kind: 'error', text: e.text || '', ts: e.ts, mid: e.mid });
        return false;
      case 'deleted':                      // 꾹 눌러 지운 말 — 다른 폰·창에서 지운 것도 (10/5)
        removeByMid(e.mid);
        return false;
      case 'result': {
        if (lastAI && typeof e.ms === 'number') {
          const steps = typeof e.steps === 'number' ? e.steps : 1;
          const meta = (e.ms / 1000).toFixed(1) + '초' + (steps > 1 ? ' · ' + steps + '단계' : '');
          update(lastAI, it => { it.meta = meta; });
        }
        if (!replaying) maybeSpeak();
        return false;
      }
      case 'cleared':
      case 'wiped':                        // 대화 전체 삭제 (10/5)
        S.items = [];
        openAI = [];
        toolOwner = {};
        cardOwner = {};
        lastAI = null;
        historyState = 'idle';
        if (!replaying) { renderAllItems(); renderHistoryBtn(); }
        return false;
      case 'model':
        S.model = e.key || S.model;
        if (!replaying) renderStatus();
        return false;
      case 'office': {
        const ws = (e.workers || []).map(worker).filter(Boolean);
        if (ws.length) { S.workers = ws; renderDrawer(); }
        return false;
      }
      default:
        return false;
    }
  }

  function finishOpen() {
    openAI.forEach(id => update(id, it => { it.streaming = false; }));
    openAI = [];
  }

  function append(it) {
    it.id = nextId++;
    S.items.push(it);
    noteArrival(it);
    if (!replaying) mountItem(it);
    if (S.items.length > 400) {
      S.items.splice(0, S.items.length - 400).forEach(old => { if (old.el) old.el.remove(); });
    }
    return it;
  }

  function find(id) {
    for (let i = S.items.length - 1; i >= 0; i--) if (S.items[i].id === id) return S.items[i];
    return null;
  }

  function update(id, change) {
    const it = find(id);
    if (!it) return;
    change(it);
    if (!replaying) paint(it);
  }

  // ── 지난 대화 보기 (history) ────────────
  let historyState = 'idle';             // idle | loading | done
  let historyAutoTried = false;
  let historyBtnEl = null;

  const INCOMING_SOURCE = (kind, from) => {
    let source = INCOMING[kind] || kind;
    if (from) source += ' · ' + from;
    return source;
  };

  /** PC 기록 한 항목 → 지금 렌더러가 쓰는 항목 모양 */
  function historyToItem(raw) {
    if (!raw || typeof raw.ts !== 'number') return null;
    const ts = raw.ts, mid = raw.mid;
    switch (raw.type) {
      case 'user':
        return { kind: 'user', text: raw.text || '', origin: raw.origin || 'typed', images: raw.images || [], files: raw.files || [], ts, mid };
      case 'text':
        return { kind: 'ai', text: raw.text || '', streaming: false, local: false, meta: null, ts, mid };
      case 'incoming': {
        const kind = raw.kind || '';
        const buttons = raw.buttons || null;
        return { kind: 'incoming', source: INCOMING_SOURCE(kind, raw.from), text: raw.text || '',
          auto: kind === 'auto' || kind === 'system', out: kind === 'phone_out',
          images: raw.images || [], files: raw.files || [], buttons, btnState: buttons && buttons.length ? { busy: false, doneIndex: null, done: [] } : null, ts, mid };
      }
      case 'note':
        return { kind: 'note', text: raw.text || '', ts, mid };
      case 'error':
        return { kind: 'error', text: raw.text || '', ts, mid };
      default:
        return null;
    }
  }

  /** 지금 화면의 가장 오래된 항목 시각(없으면 지금) — 다음 history 쪽 넘기기 기준 */
  function earliestTs() {
    for (let i = 0; i < S.items.length; i++) if (S.items[i].ts) return S.items[i].ts;
    return Math.floor(Date.now() / 1000);
  }

  /** history 로 받은 항목(새것부터)을 대화 맨 위에 붙인다 — 스크롤 위치는 그대로 */
  function prependHistoryItems(itemsDesc) {
    const asc = itemsDesc.slice().reverse();
    asc.forEach(flagUnread);
    if (!listEl) { S.items = asc.concat(S.items); return; }
    const prevHeight = chatEl.scrollHeight;
    const prevTop = chatEl.scrollTop;
    const frag = document.createDocumentFragment();
    asc.forEach(it => {
      it.id = nextId++;
      it.el = itemView(it);
      frag.append(it.el);
    });
    S.items = asc.concat(S.items);
    listEl.insertBefore(frag, listEl.firstChild);
    renderHello();
    requestAnimationFrame(() => { chatEl.scrollTop = prevTop + (chatEl.scrollHeight - prevHeight); });
  }

  function renderHistoryBtn() {
    if (!historyBtnEl) return;
    historyBtnEl.textContent = '';
    if (historyState === 'done') {
      historyBtnEl.textContent = '처음입니다';
      historyBtnEl.disabled = true;
    } else if (historyState === 'loading') {
      historyBtnEl.append(h('i', { class: 'spinner' }), document.createTextNode(' 불러오는 중'));
      historyBtnEl.disabled = true;
    } else {
      historyBtnEl.textContent = '지난 대화 보기';
      historyBtnEl.disabled = false;
    }
  }

  async function loadMoreHistory() {
    if (!S.paired || historyState === 'loading' || historyState === 'done') return;
    historyState = 'loading';
    renderHistoryBtn();
    try {
      const before = earliestTs();
      const r = await api.get('history', { before, n: 50 });
      const items = (r.items || []).map(historyToItem).filter(Boolean);
      if (items.length) prependHistoryItems(items);
      historyState = (!r.more || !items.length) ? 'done' : 'idle';
      if (pushLand(true)) scanSeen();                   // 예전 알림을 눌러 왔으면 받아 온 데서 찾는다 (10/19)
    } catch (e) {
      historyState = 'idle';
      show(e);
    }
    renderHistoryBtn();
  }

  // ── 화면 도구 ──────────────────────────
  const $ = sel => document.querySelector(sel);

  const tappable = new WeakSet();                                     // onclick 을 단 칸(div 줄 등) — 「모든 터치」가 알아보게

  function h(tag, attrs) {
    const el = document.createElement(tag);
    if (attrs) {
      Object.keys(attrs).forEach(k => {
        const v = attrs[k];
        if (v == null || v === false) return;
        if (k === 'class') el.className = v;
        else if (k === 'text') el.textContent = v;
        else if (k === 'html') el.innerHTML = v;
        else if (k.startsWith('on')) { el.addEventListener(k.slice(2), v); if (k === 'onclick') tappable.add(el); }
        else if (k === 'style') el.style.cssText = v;
        else el.setAttribute(k, v === true ? '' : v);
      });
    }
    for (let i = 2; i < arguments.length; i++) {
      const c = arguments[i];
      if (c == null || c === false) continue;
      if (Array.isArray(c)) c.forEach(x => { if (x != null && x !== false) el.append(x); });
      else el.append(c);
    }
    return el;
  }

  const ICON = {
    menu: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M4 7h16M4 12h16M4 17h16"/></svg>',
    back: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M15 5l-7 7 7 7"/></svg>',
    save: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 4v11M7.5 10.5L12 15l4.5-4.5M5 19.5h14"/></svg>',
    share: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 15V4M7.5 8.5L12 4l4.5 4.5M6 12.5V19h12v-6.5"/></svg>',
    close: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18"/></svg>',
    up: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.8" stroke-linecap="round" stroke-linejoin="round"><path d="M12 19V5M5 12l7-7 7 7"/></svg>',
    down: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14M5 12l7 7 7-7"/></svg>',
    stop: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="6" width="12" height="12" rx="2"/></svg>',
    mic: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="9" y="3" width="6" height="11" rx="3"/><path d="M6 11a6 6 0 0 0 12 0" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"/><path d="M12 17v4" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>',
    speaker: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 9h3l5-4v14l-5-4H4z" fill="currentColor"/><path d="M16 9a4 4 0 0 1 0 6M18.5 6.5a8 8 0 0 1 0 11"/></svg>',
    check: '<svg class="ok" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3.2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg>',
    chev: '<svg class="chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="M9 5l7 7-7 7"/></svg>',
    gear: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M19.4 13a7.6 7.6 0 0 0 0-2l2-1.6-2-3.4-2.4 1a7.4 7.4 0 0 0-1.7-1L15 3.5h-4l-.3 2.5a7.4 7.4 0 0 0-1.7 1l-2.4-1-2 3.4L6.6 11a7.6 7.6 0 0 0 0 2l-2 1.6 2 3.4 2.4-1a7.4 7.4 0 0 0 1.7 1l.3 2.5h4l.3-2.5a7.4 7.4 0 0 0 1.7-1l2.4 1 2-3.4zM12 15.5a3.5 3.5 0 1 1 0-7 3.5 3.5 0 0 1 0 7z"/></svg>',
    pencil: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 20h4L19 9l-4-4L4 16z"/><path d="M13.5 6.5l4 4"/></svg>',
    phone: '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2"><rect x="6" y="2.5" width="8" height="15" rx="2"/><path d="M9 14.5h2"/></svg>',
    pc: '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2"><rect x="2.5" y="3.5" width="15" height="10" rx="1.5"/><path d="M7 17h6M10 13.5V17"/></svg>',
    micSm: '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><rect x="7.5" y="2.5" width="5" height="9" rx="2.5"/><path d="M5 9.5a5 5 0 0 0 10 0M10 14.5v3"/></svg>',
    plus: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M12 5v14M5 12h14"/></svg>',
    share: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"><path d="M12 15V4"/><path d="m8 8 4-4 4 4"/><path d="M5 12v6a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-6"/></svg>',
    download: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"><path d="M12 4v11"/><path d="m7 11 5 5 5-5"/><path d="M5 19h14"/></svg>',
    folder: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7a2 2 0 0 1 2-2h4l2 2h6a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7z"/></svg>',
    inbox: '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M3 11.5l1.8-6.2A1.5 1.5 0 0 1 6.2 4.2h7.6a1.5 1.5 0 0 1 1.4 1.1L17 11.5V15a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 3 15z"/><path d="M3 11.5h4l1 2h4l1-2h4"/><path d="M7.8 7.7l1.6 1.6 2.9-2.9"/></svg>',
    copy: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="11" height="11" rx="2"/><path d="M5 15V6a2 2 0 0 1 2-2h8"/></svg>',
    reply: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"><path d="M9 14 4 9l5-5"/><path d="M4 9h10a6 6 0 0 1 6 6v3"/></svg>',
    trash: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7h16M10 11v6M14 11v6M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12M9 7V4h6v3"/></svg>',
    photo: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3.5" y="4.5" width="17" height="15" rx="3"/><circle cx="9" cy="10" r="1.7"/><path d="M4 17l4.6-4.4a1.6 1.6 0 0 1 2.2 0L15 16.5l1.8-1.7a1.6 1.6 0 0 1 2.2 0l1.5 1.5"/></svg>',
    play: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M8 5.5v13a1 1 0 0 0 1.5.9l10.5-6.5a1 1 0 0 0 0-1.8L9.5 4.6A1 1 0 0 0 8 5.5z"/></svg>',
    up: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M12 19V5M6 11l6-6 6 6"/></svg>',
    broom: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.5 3.5l-3.2 6.4M9.2 10.2l5.6 2.8c.9.5 1.3 1.6.8 2.5l-1.6 3.4c-.2.4-.7.6-1.1.4L5.5 16.2c-.4-.2-.6-.7-.4-1.1l1.4-3c.5-1 1.6-1.4 2.7-.9z"/><path d="M9.6 14.4l-1 2.1M12.5 15.7l-1 2.1"/></svg>',
    cloud: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M7.5 18.5a4.2 4.2 0 0 1-.7-8.34A5.6 5.6 0 0 1 17.4 9a4.8 4.8 0 0 1-.4 9.5z"/><path d="M12 16v-5M9.8 12.9L12 10.7l2.2 2.2"/></svg>',
    albumAdd: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3.5" y="7.5" width="13" height="13" rx="2.5"/><path d="M7.5 7.5V6a2 2 0 0 1 2-2H18a2.5 2.5 0 0 1 2.5 2.5V15a2 2 0 0 1-2 2h-1.5"/><path d="M10 11.5v6M7 14.5h6"/></svg>',
    minusCircle: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="8.5"/><path d="M8.5 12h7"/></svg>',
  };
  const svg = name => { const t = document.createElement('template'); t.innerHTML = ICON[name]; return t.content.firstChild; };

  function orb(state, size) {
    const el = h('div', { class: 'orb', 'data-state': state, style: '--s:' + size + 'px', 'aria-hidden': 'true' }, h('img', { src: 'cat.png', alt: '' }));
    return el;
  }

  function avatar(w, size) {
    return h('div', { class: 'avatar' + (w.busy ? ' busy' : ''), style: '--s:' + size + 'px;background:' + safeColor(w.color) }, w.initial);
  }

  const safeColor = c => (/^#[0-9a-f]{3,8}$/i.test(c) ? c : '#8D92AE');

  const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  /** 마크다운을 가볍게 — 제목은 굵게, 목록은 •, 굵게·기울임·코드·링크는 살린다(아이폰 MarkdownText 와 같은 규칙).
   *  코드 블록은 고정폭 상자로, 맨 주소는 누를 수 있게. */
  function markdown(src) {
    const blocks = [];
    let lines = [];
    let code = null;
    const flush = () => { if (lines.length) { blocks.push(lines.map(inline).join('\n')); lines = []; } };
    String(src || '').split('\n').forEach(raw => {
      if (raw.trim().startsWith('```')) {
        if (code) { flush(); blocks.push('<pre>' + esc(code.join('\n')) + '</pre>'); code = null; } else { code = []; }
        return;
      }
      if (code) { code.push(raw); return; }
      let m;
      if ((m = raw.match(/^#{1,4}\s+(.*)$/))) lines.push('**' + m[1] + '**');
      else if ((m = raw.match(/^(\s*)[-*+]\s+(.*)$/))) lines.push(m[1] + '• ' + m[2]);
      else lines.push(raw);
    });
    if (code) { flush(); blocks.push('<pre>' + esc(code.join('\n')) + '</pre>'); }
    flush();
    return blocks.join('');
  }

  const DOC = /[Cc]:[\\/]+que-desk(?:[\\/][^\s`'"<>|*?]*)?/g;       // PC 의 효딩쓰 문서 — 누르면 폰에서 열리는 링크로
  const docLink = p => '<a href="#" data-doc="' + esc(p) + '">' + esc(p) + '</a>';

  function inline(line) {
    const keep = [];
    const hold = html => '\u0000' + (keep.push(html) - 1) + '\u0000';
    let s = line.replace(/`([^`]+)`/g, (_, c) => hold('<code>' + (/^[Cc]:[\\/]+que-desk/.test(c) ? docLink(c) : esc(c)) + '</code>'));
    s = s.replace(/\[([^\]]+)\]\(((?:https?:\/\/|mailto:|tel:)[^)\s]+)\)/g, (_, t, u) => hold('<a href="' + esc(u) + '">' + esc(t) + '</a>'));
    s = s.replace(/https?:\/\/[^\s<>"')\]\u3131-\uD7A3]+[^\s<>"')\].,!?;:\u3131-\uD7A3]/g, u => hold('<a href="' + esc(u) + '">' + esc(u) + '</a>'));
    s = s.replace(DOC, p => hold(docLink(p)));
    s = esc(s);
    s = s.replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>').replace(/__([^_\n]+)__/g, '<strong>$1</strong>');
    s = s.replace(/(^|[^*\w])\*([^*\s][^*\n]*?)\*(?!\*)/g, '$1<em>$2</em>');
    s = s.replace(/(^|[\s(])_([^_\n]+)_(?=[\s).,!?]|$)/g, '$1<em>$2</em>');
    return s.replace(/\u0000(\d+)\u0000/g, (_, i) => keep[+i]);
  }

  // ── 대화 목록 ──────────────────────────
  let chatEl = null, listEl = null, helloEl = null, jumpEl = null;

  function renderAllItems() {
    if (!listEl) return;
    listEl.textContent = '';
    dividerEl = null;                                       // 목록을 비우며 같이 사라졌다
    S.items.forEach(it => { it.el = null; mountItem(it); });
    renderHello();
  }

  function mountItem(it) {
    if (!listEl) return;
    it.el = itemView(it);
    listEl.append(it.el);
    renderHello();
  }

  function paint(it) {
    if (!it.el) return;
    const el = itemView(it);
    it.el.replaceWith(el);
    it.el = el;
  }

  function paintStream(it) {
    if (replaying || !it.el) return;
    const body = it.el.querySelector('.ai-body.stream');
    if (body) body.textContent = it.text + ' ▍';
    else paint(it);
  }

  function renderHello() {
    if (helloEl) helloEl.hidden = S.items.length > 0;
  }

  function itemView(it) {
    const el = itemBody(it);
    if (MSG_KINDS[it.kind]) el.dataset.iid = it.id;        // 꾹 누르면 이 번호로 항목을 찾는다
    if (it.unread) el.classList.add('new');                // 안 읽은 글 — 왼쪽에 분홍 막대 (읽으면 서서히 사라진다)
    return el;
  }

  function itemBody(it) {
    switch (it.kind) {
      case 'user': return userBubble(it);
      case 'ai': return aiMessage(it);
      case 'tools': return h('div', { class: 'tools' }, it.list.map(toolRow));
      case 'permission': return permissionCard(it.card);
      case 'question': return questionCard(it.card);
      case 'incoming':
        return h('div', { class: 'inbox' + (it.auto ? ' auto' : '') + (it.out ? ' out' : '') },
          h('b', { text: it.source }), it.text ? h('div', { class: foldCls(it) || null }, linkified(it.text)) : null, foldBtn(it),
          attachments(it), stamp(it), buttonRow(it));
      case 'note': return h('div', { class: 'note', text: it.text });
      case 'error': return h('div', { class: 'err', text: it.text });
      default: return h('div');
    }
  }

  /** 알림 글 → [텍스트, {text, url}, …]. [제목](주소)(제목 안 대괄호 한 겹 허용)와 맨 주소만 링크, http/https 만. */
  const LINK_RE = /\[((?:[^\[\]\n]|\[[^\[\]\n]*\])+)\]\((https?:\/\/[^\s)]+)\)|(https?:\/\/[^\s<>"'\])\u3131-\uD7A3]+)/g;
  function splitLinks(text) {
    const out = [];
    let at = 0, m;
    LINK_RE.lastIndex = 0;
    while ((m = LINK_RE.exec(text))) {
      let url = m[2] || m[3], label = m[1], end = LINK_RE.lastIndex;
      if (!label) {
        const tail = url.match(/[.,!?;:]+$/);                        // 문장 끝 문장부호는 주소가 아니다
        if (tail) { url = url.slice(0, -tail[0].length); end -= tail[0].length; LINK_RE.lastIndex = end; }
        if (!/^https?:\/\/[^/?#]/.test(url)) continue;
      }
      if (m.index > at) out.push(text.slice(at, m.index));
      out.push({ text: label || url, url });
      at = end;
    }
    if (at < text.length) out.push(text.slice(at));
    return out;
  }
  // 누르면 맨 아래 문서 클릭 처리기가 openLink(href) 로 연다
  const linkified = text => splitLinks(text).map(p => typeof p === 'string' ? document.createTextNode(p) : h('a', { href: p.url, class: 'lnk', text: p.text }));

  // 긴 글은 접어 두고 「전체보기」로 편다 (2026-10-10 대표님 — 자율 작업 글이 길어 끝이 안 보였다)
  const LONG_CHARS = 600, LONG_LINES = 12;
  const isLong = it => !it.streaming && !!it.text && (it.text.length > LONG_CHARS || it.text.split('\n').length > LONG_LINES);
  const foldCls = it => isLong(it) ? (it.expanded ? 'fold' : 'fold folded') : '';
  function foldBtn(it) {
    if (!isLong(it)) return null;
    return h('button', {
      class: 'more', 'aria-expanded': it.expanded ? 'true' : 'false', text: it.expanded ? '접기' : '전체보기',
      onclick: ev => {
        ev.stopPropagation();
        it.expanded = !it.expanded;
        sfx.play('tap');
        paint(it);
        if (!it.expanded && it.el) it.el.scrollIntoView({ block: 'nearest' });
      },
    });
  }

  function userBubble(it) {
    // 이 폰(앱)에서 보낸 건 표시하지 않고, 다른 곳에서 온 것만 어디서인지 붙인다
    const via = { typed: ['PC 에서', 'pc'], voice: ['PC 에서 말로', 'micSm'], phone: ['텔레그램', 'phone'] }[it.origin];
    const rp = splitReply(it.text);
    return h('div', { class: 'u-row' },
      h('div', { class: 'u-bubble' }, via ? h('div', { class: 'via' }, svg(via[1]), via[0]) : null,
        rp.quote ? h('div', { class: 'rquote', text: rp.quote }) : null,
        isLong(it) ? h('div', { class: foldCls(it), text: rp.body }) : document.createTextNode(rp.body), foldBtn(it), attachments(it), stamp(it)));
  }

  /** 받은 시각 — 텔레그램처럼 작게. 읽기(소리)에는 들어가지 않는다(본문 it.text 만 읽음). PC 가 사건에 붙여 준 시각(ts). */
  function hhmm(ts) {
    const d = ts ? new Date(ts * 1000) : new Date(), now = new Date();
    const t = d.toLocaleTimeString('ko-KR', { hour: 'numeric', minute: '2-digit' });
    return d.toDateString() === now.toDateString() ? t : (d.getMonth() + 1) + '/' + d.getDate() + ' ' + t;
  }
  function stamp(it) {
    return it.ts ? h('span', { class: 'ts', text: hhmm(it.ts), 'aria-hidden': 'true' }) : null;
  }

  /** 문서함 — 날짜 묶음 제목과 줄 시각 */
  function dayLabel(ts) {
    const d = ts ? new Date(ts * 1000) : new Date(), now = new Date();
    const startOf = x => new Date(x.getFullYear(), x.getMonth(), x.getDate()).getTime();
    const diff = Math.round((startOf(now) - startOf(d)) / 86400000);
    if (diff === 0) return '오늘';
    if (diff === 1) return '어제';
    return (d.getMonth() + 1) + '월 ' + d.getDate() + '일';
  }
  const timeOnly = ts => (ts ? new Date(ts * 1000) : new Date()).toLocaleTimeString('ko-KR', { hour: 'numeric', minute: '2-digit' });

  /** 휴대폰으로 보낸 카드뉴스·대표님이 보낸 사진 — 서명 링크(PC 효딩쓰가 만든 것). 누르면 앱 안 보기 화면으로 (2026-10-04) */
  function attachments(it) {
    const imgs = it.images || [], files = it.files || [];
    if (!imgs.length && !files.length) return null;
    const photos = imgs.map(u => ({ url: u, name: '', kind: 'image' }));
    return h('div', { class: 'att' },
      imgs.map((u, i) => h('button', { class: 'att-img', 'aria-label': '사진 크게 보기', onclick: () => openViewer(photos, i, '') },
        h('img', { src: u, alt: '', loading: 'lazy' }))),
      files.map(f => h('button', { class: 'file', text: '📄 ' + f.name, onclick: () => openViewer([{ url: f.url, name: f.name }], 0, f.name) })));
  }

  /** 알림에 달린 버튼 — 제안 승인·메일 보내기처럼 한 번 정하면 끝인 것은 하나 누르면 나머지를 잠그고,
      AI Office 승인함(office:)은 줄마다 따로라 누른 것만 「✓ 눌렀어요」 · 나머지는 계속 누를 수 있다 (2026-10-31) */
  const indep = it => (it.buttons || []).every(([, data]) => /^office:/.test(data));
  function buttonRow(it) {
    if (!it.buttons || !it.buttons.length) return null;
    const st = it.btnState, one = !indep(it);
    return h('div', { class: 'nbtns' }, it.buttons.map(([label, data], i) => {
      const done = (st.done || []).includes(i);
      return h('button', {
        class: 'nbtn' + (done ? ' done' : ''),
        disabled: st.busy || done || (one && st.doneIndex != null),
        text: done ? '✓ 눌렀어요' : label,
        onclick: () => pressButton(it, i, data),
      });
    }));
  }

  async function pressButton(it, i, data) {
    const st = it.btnState, one = !indep(it);
    if (!st || st.busy || (st.done || []).includes(i) || (one && st.doneIndex != null)) return;
    st.busy = true;
    paint(it);
    try {
      await api.post('button', { data });      // 서버는 {data: '…'} 를 받는다
      st.doneIndex = i;
      (st.done = st.done || []).push(i);
      sfx.play('send');
      haptic('tap');
    } catch (e) {
      sfx.play('error');
      haptic('alert');
      show(e);
    } finally {
      st.busy = false;
      paint(it);
    }
  }

  function aiMessage(it) {
    const reading = speaker.itemId === it.id;
    const head = h('div', { class: 'ai-head' },
      h('i', { class: 'dot' }), h('span', { class: 'nm', text: S.name }), it.streaming ? null : stamp(it),
      it.local ? h('span', { class: 'badge', text: '로컬 LLM' }) : null,
      h('span', { class: 'grow' }),
      !it.streaming && it.text ? h('button', {
        class: 'read' + (reading ? ' on' : ''), 'data-read': it.id, 'aria-label': reading ? '읽기 멈추기' : '이 답을 소리로 읽기',
        onclick: () => readAloud(it),
      }, reading ? svg('stop') : svg('speaker'), reading ? '멈춤' : '읽기') : null);
    const body = it.streaming ? h('div', { class: 'ai-body stream', text: it.text + ' ▍' })
      : h('div', { class: 'ai-body md' + (isLong(it) ? ' ' + foldCls(it) : ''), html: markdown(it.text) });
    return h('div', { class: 'ai' }, head, body, foldBtn(it), it.meta ? h('div', { class: 'meta', text: it.meta }) : null);
  }

  function renderReadButtons() {
    if (!listEl) return;
    listEl.querySelectorAll('.read').forEach(b => {
      const on = String(speaker.itemId) === b.dataset.read;
      if (b.classList.contains('on') === on) return;
      b.classList.toggle('on', on);
      b.textContent = '';
      b.append(svg(on ? 'stop' : 'speaker'), on ? '멈춤' : '읽기');
      b.setAttribute('aria-label', on ? '읽기 멈추기' : '이 답을 소리로 읽기');
    });
  }

  function toolRow(t) {
    const st = t.status === 'ok' ? svg('check') : t.status === 'fail' ? h('i', { class: 'fail' }) : h('i', { class: 'spinner' });
    return h('div', { class: 'tool' + (t.sub ? ' sub' : '') }, h('span', { class: 'st' }, st), h('span', { class: 'lb', text: t.label }), h('span', { class: 'dt', text: t.detail }));
  }

  function permissionCard(c) {
    const open = !c.closed;
    return h('div', { class: 'card' + (open ? '' : ' closed') },
      h('div', { class: 'kick', text: open ? '허락이 필요합니다' : (CLOSED[c.closed] || '닫혔습니다') }),
      h('div', { class: 'ttl', text: c.title }),
      open && c.note ? h('div', { class: 'nt', text: c.note }) : null,
      open && c.body ? h('div', { class: 'body', text: c.body }) : null,
      open ? h('div', { class: 'acts' },
        h('button', { class: 'btn', text: '허락', onclick: () => decide(c.id, 'allow') }),
        c.canAlways ? h('button', { class: 'btn ghost', text: '계속 허락', onclick: () => decide(c.id, 'always') }) : null,
        h('button', { class: 'btn no', text: '거절', onclick: () => decide(c.id, 'deny') })) : null,
      open ? h('div', { class: 'hint', text: '아래에 다르게 하라고 적으셔도 됩니다.' }) : null);
  }

  function questionCard(c) {
    const open = !c.closed;
    const mine = picks[c.id] || (picks[c.id] = {});
    const needsConfirm = c.questions.length > 1 || (c.questions[0] && c.questions[0].multi);
    const submit = () => {
      const answers = {};
      Object.keys(mine).forEach(k => { answers[k] = mine[k].join(', '); });
      answer(c.id, answers);
    };
    const wrap = h('div', { class: 'card q' + (open ? '' : ' closed') }, h('div', { class: 'kick', text: open ? '골라 주세요' : (CLOSED[c.closed] || '닫혔습니다') }));
    c.questions.forEach(q => {
      wrap.append(h('div', { class: 'ttl', text: q.question }));
      if (!open) return;
      q.options.forEach(o => {
        const on = (mine[q.question] || []).includes(o.label);
        wrap.append(h('button', {
          class: 'opt' + (on ? ' on' : ''),
          onclick: () => {
            if (q.multi) {
              const cur = mine[q.question] || [];
              const i = cur.indexOf(o.label);
              if (i >= 0) cur.splice(i, 1); else cur.push(o.label);
              mine[q.question] = cur;
            } else {
              mine[q.question] = [o.label];
              if (!needsConfirm) { submit(); return; }
            }
            const owner = cardOwner[c.id];
            if (owner) paint(find(owner));
          },
        }, h('b', { text: o.label }), o.desc ? h('small', { text: o.desc }) : null));
      });
    });
    if (open) {
      wrap.append(h('div', { class: 'acts' },
        needsConfirm ? h('button', { class: 'btn', text: '이대로 답하기', onclick: submit }) : null,
        h('button', { class: 'btn no', text: '답하지 않기', onclick: () => decide(c.id, 'deny') })));
    }
    return wrap;
  }

  // 스크롤 — 맨 아래를 보고 있을 때만 따라 내려간다. 위를 읽는 중이면 「↓」 단추.
  const nearBottom = () => !chatEl || chatEl.scrollHeight - chatEl.scrollTop - chatEl.clientHeight < 90;
  let atBottom = true;                     // 마지막으로 본 자리가 맨 아래인가 — 칸 크기가 바뀌거나(키보드·띠) 앱으로 돌아올 때 붙여 둔다
  let touchedAt = 0;

  function scrollToBottom(smooth) {
    if (!chatEl) return;
    atBottom = true;
    requestAnimationFrame(() => {
      chatEl.scrollTo({ top: chatEl.scrollHeight, behavior: smooth ? 'smooth' : 'auto' });
      if (jumpEl) jumpEl.hidden = true;
    });
  }

  /** 지금 바로 맨 아래로. 아이폰(WKWebView)은 목록을 다시 그리거나 앱으로 돌아온 뒤 실제 위치가 화면과 어긋나 맨 아래보다
   *  더 내려간 빈 곳을 보여 줄 때가 있다(10/12). 같은 값으로 다시 내리면 무시되니 1px 위로 갔다가 맨 아래로 — 위치가 실제로
   *  바뀌어야 화면 쪽이 따라 맞춘다. */
  function pinBottom() {
    if (!chatEl) return;
    atBottom = true;
    const max = chatEl.scrollHeight - chatEl.clientHeight;
    if (max > 1) chatEl.scrollTop = max - 1;
    chatEl.scrollTop = chatEl.scrollHeight;
    if (jumpEl) jumpEl.hidden = true;
  }

  /** 지금 한 번, 화면이 자리 잡은 뒤(다음 프레임·0.35초) 한 번 더 — 그 사이 대표님이 대화를 만지셨으면 그대로 둔다 */
  function settleBottom() {
    const t0 = Date.now();
    pinBottom();
    const again = () => { if (touchedAt < t0 && atBottom) pinBottom(); };
    requestAnimationFrame(again);
    setTimeout(again, 350);
  }

  function afterChange(stick, changed) {
    if (stick) scrollToBottom(true);
    else if (changed && jumpEl) {
      jumpEl.hidden = false;
      if (!dividerEl) placeDivider(true);                 // 위를 읽는 중에 새 글이 오면 어디서부터 새 글인지 줄을 놓아 둔다
    }
    scanSeen();
  }

  // ── 읽음 · 안 읽음 (2026-10-18) ────────────────────────────────
  // 대표님: 「알림을 누르면 무조건 맨 아래로 간다 — 그 알림으로 가고, 읽은 것과 안 읽은 것을 구분해 달라」.
  // 푸시에는 항목 번호가 없고 포장지 앱도 알림 탭을 화면에 넘기지 않아서, 화면은 어떤 알림으로 들어왔는지 모른 채 늘 맨 아래로 붙었다.
  // 그래서 화면이 「여기까지 봤다」(seenTs — PC 가 말마다 붙인 시각)를 이 폰에 적어 두고, 열 때 그 뒤의 첫 글로 앉는다.
  // 앱을 다시 빌드할 필요가 없다(웹만). 어느 알림을 눌렀는지까지 알려면 포장지가 푸시의 번호를 넘겨야 한다 — 아이폰 앱 다음 빌드.
  const SEEN_KEY = 'seenTs';
  const HOLES_KEY = 'seenHoles';
  const UNREAD_KINDS = { ai: 1, incoming: 1, error: 1 };      // 내 말·도구 줄·가운데 작은 안내(note)는 안 읽음에서 뺀다
  const SEEN_MS = 800;                                        // 화면에 이만큼 머물러야 읽은 것
  const AWAY_FRESH_MS = 120000;                               // 이만큼 비웠다 돌아오면 보던 자리 대신 안 읽은 곳으로
  let seenTs = parseFloat(local.get(SEEN_KEY, '0')) || 0;
  // 누른 알림의 말로 건너뛰면 읽은 자리가 그 위의 안 읽은 글을 넘어간다 — 그 글들(시각)은 따로 적어 두고, 화면에 머물 때 지운다(10-19b).
  // 막대만 남긴다: 다시 열 때 이 글로 끌어올리거나 「여기부터 안 읽음」 줄을 놓지는 않는다(맨 아래까지 읽고 나갔다 오면 맨 아래 그대로).
  const holes = new Set((() => { try { const a = JSON.parse(local.get(HOLES_KEY, '[]')); return Array.isArray(a) ? a : []; } catch (e) { return []; } })()
    .filter(x => typeof x === 'number'));
  let seenTimer = 0, scanRaf = 0, dividerEl = null;
  let hiddenAt = 0, leftAtBottom = true, awayBatch = false, landAfterAway = false;

  const unreadable = it => !!it.ts && !it.streaming && UNREAD_KINDS[it.kind] === 1;
  const flagUnread = it => { it.unread = unreadable(it) && (it.ts > seenTs || holes.has(it.ts)); };
  const unreadCount = () => S.items.reduce((n, it) => n + (it.unread ? 1 : 0), 0);
  const chatCovered = () => pages.length > 0 || layers.length > 0;      // 설정·승인함·서랍이 대화를 덮고 있으면 본 게 아니다
  const hasOpenCard = () => S.items.some(it => (it.kind === 'permission' || it.kind === 'question') && it.card && !it.card.closed);

  /** 적어 둔 게 없으면(새로 깔거나 짝 지은 폰) 지금까지는 읽은 것으로 — 전부 안 읽음으로 뜨지 않게 */
  function initSeen() {
    if (seenTs) return;
    seenTs = S.items.reduce((m, it) => Math.max(m, it.ts || 0), 0);
    if (seenTs) local.set(SEEN_KEY, String(seenTs));
  }

  /** ts 까지는 읽었다 — 그 앞의 안 읽음 표시를 걷는다(남겨 둔 글은 빼고) */
  function readUpTo(ts) {
    if (!(ts > seenTs)) return;
    seenTs = ts;
    local.set(SEEN_KEY, String(ts));
    S.items.forEach(it => {
      if (it.unread && it.ts <= seenTs && !holes.has(it.ts)) { it.unread = false; if (it.el) it.el.classList.remove('new'); }
    });
    renderJump();
  }

  function saveHoles() {
    if (holes.size > 300) [...holes].sort((a, b) => a - b).slice(0, holes.size - 300).forEach(x => holes.delete(x));
    if (holes.size) local.set(HOLES_KEY, JSON.stringify([...holes])); else local.del(HOLES_KEY);
  }

  /** 화면에 머문 글들 — 남겨 둔 글이면 지우고, 읽은 자리를 그중 가장 늦은 글까지 올린다 */
  function markSeen(list) {
    let gone = false;
    list.forEach(it => { if (holes.delete(it.ts)) { gone = true; it.unread = false; if (it.el) it.el.classList.remove('new'); } });
    if (gone) saveHoles();
    readUpTo(Math.max.apply(null, list.map(it => it.ts)));
    if (gone) renderJump();
  }

  /** 누른 알림의 말보다 위에 있던 안 읽은 글은 그 말을 읽어도 안 읽은 채로 둔다 */
  function holdAbove(target) {
    const ts = target.ts || target.cardTs, n = holes.size;
    S.items.forEach(it => { if (it.unread && it.ts < ts) holes.add(it.ts); });
    if (holes.size !== n) saveHoles();
  }

  /** 새 글이 들어왔다 — 안 읽음으로 두되, 켜 둔 화면의 맨 아래를 보던 중에 온 글은 바로 읽은 것으로 */
  function noteArrival(it) {
    flagUnread(it);
    if (it.unread && !replaying && !awayBatch && !document.hidden && atBottom && !chatCovered()) readUpTo(it.ts);
  }

  function removeDivider() {
    if (dividerEl) { dividerEl.remove(); dividerEl = null; }
  }

  /** 안 읽은 첫 글 앞에 「여기부터 안 읽음」 줄을 놓는다(읽은 자리 뒤의 글만 — 남겨 둔 글은 막대로만). belowTop 이면 그 글이 화면 위쪽 밖에 있을 때는 놓지 않는다(읽던 자리가 밀리지 않게) */
  function placeDivider(belowTop) {
    removeDivider();
    if (!listEl || !chatEl) return null;
    const first = S.items.find(it => it.unread && it.ts > seenTs && it.el && it.el.isConnected);
    if (!first) return null;
    if (belowTop && first.el.getBoundingClientRect().top < chatEl.getBoundingClientRect().top) return null;
    dividerEl = h('div', { class: 'unread-div', role: 'separator' }, h('span', { text: '여기부터 안 읽음' }));
    first.el.before(dividerEl);
    return dividerEl;
  }

  /** el 을 대화 윗머리에 앉힌다 */
  function seatAt(el) {
    const t0 = Date.now();
    const go = () => {
      if (!el.isConnected) return;
      const top = el.getBoundingClientRect().top - chatEl.getBoundingClientRect().top + chatEl.scrollTop - 10;
      if (top >= chatEl.scrollHeight - chatEl.clientHeight - 4) { pinBottom(); return; }   // 한 화면이 안 되는 새 글 — 맨 아래와 같다
      chatEl.scrollTop = Math.max(0, top);
      atBottom = nearBottom();
      renderJump();
    };
    go();
    const again = () => { if (touchedAt < t0) go(); };      // 그림·글꼴이 자리 잡은 뒤 한 번 더 — 그 사이 만지셨으면 그대로
    requestAnimationFrame(again);
    setTimeout(again, 350);
  }

  /** 안 읽은 첫 글에 앉는다 — 그 줄이 대화 윗머리에 오게. 안 읽은 게 없으면 false(호출한 쪽이 맨 아래로) */
  function landOnUnread() {
    if (!chatEl || !placeDivider(false)) return false;
    seatAt(dividerEl);
    return true;
  }

  /** 앱을 열었다 — 끊겼던 사이 쌓인 글이 있으면 안 읽은 곳으로. 허락·선택 카드가 열려 있으면 그쪽(맨 아래)이 먼저 */
  function landUnreadAfterAway() {
    if (hasOpenCard() || !unreadCount()) return false;
    const ok = landOnUnread();
    scanSeen();
    return ok;
  }

  function onAppHidden() {
    hiddenAt = Date.now();
    leftAtBottom = atBottom;
    clearTimeout(seenTimer);
    seenTimer = 0;
    removeDivider();                                        // 돌아올 때 새로 놓는다
  }

  function onAppShown() {
    if (!hiddenAt) return;                                  // 숨은 적 없이 온 두 번째 신호
    awayBatch = true;                                       // 이어 받는 첫 응답은 자리를 비운 사이 쌓인 글
    landAfterAway = leftAtBottom || Date.now() - hiddenAt > AWAY_FRESH_MS;
    hiddenAt = 0;
  }

  /** 화면에서 읽은 만큼 본 안 읽음 글 — 높이의 60% 이상(긴 글은 화면의 60% 이상)이 보이는 것 */
  function visibleUnread() {
    if (!chatEl || document.hidden || chatCovered()) return [];
    const v = chatEl.getBoundingClientRect();
    return S.items.filter(it => {
      if (!it.unread || !it.el || !it.el.isConnected) return false;
      const r = it.el.getBoundingClientRect();
      const shown = Math.min(r.bottom, v.bottom) - Math.max(r.top, v.top);
      return shown > 0 && shown >= Math.min(r.height, v.height) * 0.6;
    });
  }

  /** 스크롤·새 글·돌아옴마다 부른다 — 안 읽은 글이 SEEN_MS 동안 계속 보이면 거기까지 읽은 것 */
  function scanSeen() {
    if (scanRaf) return;
    scanRaf = requestAnimationFrame(() => {
      scanRaf = 0;
      renderJump();
      const seen = visibleUnread();
      if (!seen.length) { clearTimeout(seenTimer); seenTimer = 0; return; }
      if (seenTimer) return;
      const ids = new Set(seen.map(it => it.id));
      seenTimer = setTimeout(() => {
        seenTimer = 0;
        const still = visibleUnread().filter(it => ids.has(it.id));
        if (still.length) markSeen(still);
        if (visibleUnread().length) scanSeen();
      }, SEEN_MS);
    });
  }

  /** 안 읽은 수를 「↓」 단추에 — 누르면 맨 아래로. 화면 위로 지나간 남겨 둔 글은 세지 않는다(↓ 로 못 간다) */
  function renderJump() {
    if (!jumpEl) return;
    const top = holes.size && chatEl ? chatEl.getBoundingClientRect().top + 24 : -Infinity;
    const n = S.items.reduce((c, it) => c + (it.unread && !(holes.has(it.ts) && it.el && it.el.isConnected && it.el.getBoundingClientRect().bottom <= top) ? 1 : 0), 0);
    jumpEl.classList.toggle('has-n', n > 0);
    const jn = jumpEl.querySelector('.jn');
    if (jn) jn.textContent = n > 99 ? '99+' : n ? String(n) : '';
    jumpEl.setAttribute('aria-label', n ? '안 읽은 글 ' + n + '개 — 맨 아래로' : '맨 아래로');
    if (n > 0 && !nearBottom()) jumpEl.hidden = false;
  }

  /** 「↓」 단추 — 누르면 언제나 맨 아래로 (2026-11-15 대표님: 「내려가는 버튼 누르면 가장 아래로」). 예전엔 안 읽은 다음 글로 앉았다. */
  function jumpNext() {
    scrollToBottom(true);
  }

  // ── 누른 알림의 그 말로 (2026-10-19) ───────────────────────────
  // 대표님: 「알림을 누르면 그 알림의 정확한 화면으로」 — 위 「안 읽은 곳으로」는 어느 알림을 눌렀는지 몰라 첫 안 읽은 글에 앉았다.
  // 아이폰 앱(2.1+)이 누른 알림의 글과 보낸 시각(at — PC push.py 가 싣는다)을 넘겨 준다. 말은 PC 가 내보낸 순간의 시각을 달고 있어서,
  // 글 머리가 알림에 들어 있는 말 중 그 시각에 가장 가까운 것에 앉아 잠깐 비춘다(그런 말이 없으면 2분 안의 가장 가까운 말).
  // 앱이 잠든 사이 끊긴 긴 폴링을 다시 잇는 중이면 그 말이 아직 없어서, 받아 올 때마다 다시 찾는다(15초까지). 예전 알림이면 지난 대화를 더 받아 온다.
  // 예전 앱·안드로이드·브라우저엔 pushOpened 가 없어서 그냥 지나간다(안 읽은 곳으로 앉기는 그대로).
  const PUSH_WAIT_MS = 15000;
  const PUSH_KINDS = { ai: 1, incoming: 1, error: 1, permission: 1, question: 1 };
  let pushTarget = null, pushTook = 0, pushHeldUntil = 0;

  /** 알림 미리보기와 같은 꼴로 — PC push._clean 처럼 태그를 빼고 [제목](주소)는 제목만, 빈칸은 하나로 */
  const pushPlain = s => String(s || '').replace(/<[^>]+>/g, '')
    .replace(/\[((?:[^[\]\n]|\[[^[\]\n]*\])+)\]\(https?:\/\/[^\s)]+\)/g, '$1').replace(/\s+/g, ' ').trim();
  const itemHead = it => pushPlain(it.text || (it.card && (it.card.title || ((it.card.questions || [])[0] || {}).question))).slice(0, 16);

  async function takePushOpen() {
    if (!Native || typeof Native.pushOpened !== 'function') return;
    let p = null;
    try { p = await Native.pushOpened(); } catch (e) { return; }
    if (!p || typeof p.at !== 'number' || p.n === pushTook) return;       // 같은 알림은 한 번만 — 화면이 새로 고쳐지면 새 화면이 다시 가져간다
    pushTook = p.n;
    // 문서함·승인함·서랍 같은 창이 열린 채 알림으로 들어오면 창을 걷어 내고 그 말로 간다 (2026-11-16 대표님)
    try {
      document.querySelectorAll('.viewer .vw-top .vw-btn[aria-label="닫기"]').forEach(b => b.click());
      closeAllLayers();
    } catch (e) { /* 창이 없으면 그대로 */ }
    pushTarget = { at: p.at, body: pushPlain(p.body), until: Date.now() + PUSH_WAIT_MS, older: 0 };
    // 대화를 받기 전·이어 받기 전이면 글 머리가 같은 말만 — 2분 안의 엉뚱한 말에 먼저 앉지 않게. 나머지는 받아 온 뒤 pollLoop·ingestState 가
    if (pushLand(!!boot && !awayBatch && !hiddenAt)) scanSeen();
  }

  /** 누른 알림의 말. settled(받을 것을 다 받음)가 아니면 글 머리가 같은 말만 */
  function pushMatch(t, settled) {
    let best = null, bestScore = Infinity;
    S.items.forEach(it => {
      const ts = it.ts || it.cardTs;
      if (!ts || !PUSH_KINDS[it.kind] || !it.el || !it.el.isConnected) return;
      const d = Math.abs(t.at - ts);
      const head = itemHead(it);
      const same = head.length >= 4 && t.body.includes(head);
      if (same ? d > 900 : (!settled || d > 120)) return;
      const score = d + (same ? 0 : 1000);
      if (score < bestScore) { bestScore = score; best = it; }
    });
    return best;
  }

  /** 찾았으면 그 말에 앉아 잠깐 비추고 true. 곧이어 오는 「안 읽은 곳으로」는 5초 쉰다 */
  function pushLand(settled) {
    const t = pushTarget;
    if (!t || !chatEl) return false;
    if (Date.now() > t.until) { pushTarget = null; return false; }
    const it = pushMatch(t, settled);
    if (!it) {
      if (settled && S.items.length && t.at < earliestTs() && t.older < 3 && historyState === 'idle') {
        t.older += 1;                                       // 지금 화면보다 예전 알림 — 지난 대화를 50개씩 세 번까지 더 받아 와서 찾는다(받으면 loadMoreHistory 가 다시 부른다)
        t.until = Date.now() + PUSH_WAIT_MS;
        loadMoreHistory();
      }
      return false;
    }
    pushTarget = null;
    pushHeldUntil = Date.now() + 5000;
    landAfterAway = false;
    holdAbove(it);
    placeDivider(false);
    seatAt(dividerEl && dividerEl.nextSibling === it.el ? dividerEl : it.el);     // 그 말이 첫 안 읽은 글이면 「여기부터 안 읽음」 줄까지 보이게
    const el = it.el;
    el.classList.add('push-hit');
    setTimeout(() => el.classList.remove('push-hit'), 2400);
    return true;
  }

  // ── 꾹 눌러 메뉴 — 카카오톡처럼 복사 · 이 메시지 삭제 · 대화 전체 삭제 (2026-10-05) ──
  // 지운 말은 PC 효딩쓰에도 지운 표시가 남아, 다시 열어도·지난 대화에서도 나오지 않는다(remote_api chat/delete·chat/wipe).
  const MSG_KINDS = { user: 1, ai: 1, incoming: 1, note: 1, error: 1 };
  const PRESS_MS = 480;
  let pressOpenedAt = 0, pressHeld = false, swallowUntil = 0, heldTimer = 0;

  function bindLongPress(root) {
    let timer = 0, x0 = 0, y0 = 0, target = null;
    const cancel = () => { clearTimeout(timer); timer = 0; target = null; };
    root.addEventListener('pointerdown', ev => {
      if (ev.button > 0) return;                                   // 마우스 오른쪽은 contextmenu 로
      const el = ev.target.closest('[data-iid]');
      if (!el || (el.classList.contains('u-row') && !ev.target.closest('.u-bubble'))) return;
      target = el; x0 = ev.clientX; y0 = ev.clientY;
      clearTimeout(timer);
      timer = setTimeout(() => {
        const t = target;
        cancel();
        if (t && openMsgMenu(t)) {                                 // 손가락을 뗄 때까지 클릭은 받지 않는다
          pressHeld = true;
          clearTimeout(heldTimer);
          heldTimer = setTimeout(() => { pressHeld = false; }, 4000);   // 뗀 소식을 못 받아도 화면이 굳지 않게
        }
      }, PRESS_MS);
    });
    root.addEventListener('pointermove', ev => { if (timer && Math.hypot(ev.clientX - x0, ev.clientY - y0) > 10) cancel(); });
    ['pointerup', 'pointercancel', 'pointerleave'].forEach(n => root.addEventListener(n, cancel));
    if (chatEl) chatEl.addEventListener('scroll', cancel, { passive: true });
    root.addEventListener('contextmenu', ev => {                  // 안드로이드 꾹 누르기 · PC 오른쪽 단추
      const el = ev.target.closest('[data-iid]');
      if (!el) return;
      ev.preventDefault();
      if (!pressHeld && Date.now() - pressOpenedAt > 800) openMsgMenu(el);
    });
  }

  // 메뉴를 연 손가락을 뗄 때 생기는 클릭 — 손가락 밑에 깔린 메뉴 바탕(닫기)·링크·메뉴 줄을 누르지 않게 화면 전체에서 한 번 버린다
  const releasePress = () => { if (pressHeld) { pressHeld = false; clearTimeout(heldTimer); swallowUntil = Date.now() + 450; } };
  document.addEventListener('pointerup', releasePress, true);
  document.addEventListener('pointercancel', releasePress, true);
  document.addEventListener('click', ev => {
    if (pressHeld || Date.now() < swallowUntil) { ev.preventDefault(); ev.stopPropagation(); }
  }, true);

  // ── 답장하기 (2026-11-09 대표님 「꾹 누르면 복사 옆에 답장하기 — 그 말에 답장으로 칠 수 있게」) ──
  // 고른 말을 입력칸 위에 띠로 걸어 두고, 보낼 때 「↩ 답장 「그 말」」 한 줄을 앞에 붙여 보낸다.
  // 글자만으로 되어 있어 PC 효딩쓰·지난 대화에서도 같은 모양(인용 막대)으로 보이고, 효딩쓰도 어느 말에 답하는지 안다.
  const REPLY_RE = /^↩ 답장 「([^\n」]*)」\n?/;
  const REPLY_MAX = 140;
  let replyTo = null;

  const splitReply = text => {
    const m = REPLY_RE.exec(text || '');
    return m ? { quote: m[1], body: text.slice(m[0].length) } : { quote: '', body: text || '' };
  };
  const replyPrefix = () => replyTo ? '↩ 답장 「' + replyTo.quote + '」\n' : '';

  function startReply(it) {
    const q = plainText(it).replace(/\s+/g, ' ').replace(/」/g, '』').trim()
      || ((it.images || []).length ? '사진' : (it.files || []).length ? '파일' : '');
    if (!q) return;
    replyTo = { quote: q.length > REPLY_MAX ? q.slice(0, REPLY_MAX) + '…' : q, who: it.kind === 'user' ? '내 말' : '효딩쓰의 말' };
    renderReply();
    const ta = $('#main textarea');
    if (ta) ta.focus();
  }

  function clearReply() { replyTo = null; renderReply(); }

  function renderReply() {
    const strip = $('#main .reply-strip');
    if (!strip) return;
    strip.hidden = !replyTo;
    strip.textContent = '';
    if (!replyTo) return;
    strip.append(svg('reply'),
      h('div', { class: 'rq' }, h('b', { text: '답장 · ' + replyTo.who }), h('span', { text: replyTo.quote })),
      h('button', { class: 'x', 'aria-label': '답장 취소', html: ICON.close, onclick: () => { sfx.play('tap'); clearReply(); } }));
  }

  function itemOf(el) {
    const it = el && find(Number(el.dataset.iid));
    return it && MSG_KINDS[it.kind] && !it.streaming ? it : null;
  }

  /** 복사할 글 — [제목](주소)는 「제목 주소」로, 굵게·코드 표시는 뺀다 */
  function plainText(it) {
    return splitReply(it.text).body
      .replace(/\[((?:[^\[\]\n]|\[[^\[\]\n]*\])+)\]\((https?:\/\/[^\s)]+)\)/g, '$1 $2')
      .replace(/\*\*([^*\n]+)\*\*/g, '$1').replace(/`([^`\n]+)`/g, '$1').replace(/^#{1,6}\s+/gm, '');
  }

  function openMsgMenu(el) {
    const it = itemOf(el);
    if (!it || document.querySelector('.msg-menu')) return false;
    pressOpenedAt = Date.now();
    haptic('tap');
    sfx.play('menu');
    el.classList.add('pressed');
    const peek = plainText(it).replace(/\s+/g, ' ').trim() || ((it.images || []).length ? '사진' : (it.files || []).length ? '파일' : '');
    const row = (icon, label, fn, cls) => h('button', { class: 'mrow' + (cls ? ' ' + cls : ''), onclick: () => { close(); fn(); } }, svg(icon), label);
    const scrim = h('div', { class: 'sheet-scrim', onclick: ev => { if (ev.target === scrim) close(); } },
      h('div', { class: 'sheet msg-menu', role: 'dialog', 'aria-modal': 'true', 'aria-label': '메시지 메뉴' },
        peek ? h('p', { class: 'peek', text: peek.length > 90 ? peek.slice(0, 90) + '…' : peek }) : null,
        row('reply', '답장하기', () => startReply(it)),
        row('copy', '복사', () => copyMsg(it)),
        row('trash', '이 메시지 삭제', () => deleteMsg(it)),
        h('button', { class: 'btn ghost wide', text: '닫기', onclick: () => close() })));
    const close = layer(() => { scrim.remove(); el.classList.remove('pressed'); });
    $('#app').append(scrim);
    return true;
  }

  async function copyMsg(it) {
    const text = plainText(it);
    if (!text) { toast('복사할 글이 없어요'); return; }
    let ok = false;
    try {
      if (navigator.clipboard && window.isSecureContext) { await navigator.clipboard.writeText(text); ok = true; }
    } catch (e) { /* 웹뷰가 막으면 아래 방법으로 */ }
    if (!ok) {
      const ta = h('textarea', { readonly: true, style: 'position:fixed;top:-200px;left:0;opacity:0;user-select:text;-webkit-user-select:text' });
      ta.value = text;
      document.body.append(ta);
      ta.select();
      ta.setSelectionRange(0, text.length);
      try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
      ta.remove();
    }
    if (ok) sfx.play('tap');
    toast(ok ? '복사했어요' : '복사하지 못했어요');
  }

  function removeItem(it) {
    S.items = S.items.filter(x => x !== it);
    if (it.el) it.el.remove();
    if (lastAI === it.id) lastAI = null;
    renderHello();
  }

  function removeByMid(mid) {
    if (!mid) return;
    S.items.filter(x => x.mid === mid).forEach(removeItem);
  }

  async function deleteMsg(it) {
    if (!it.mid) { removeItem(it); toast('화면에서만 지웠어요'); return; }
    if (it.el) it.el.classList.add('pressed');
    try {
      await api.post('chat/delete', { mid: it.mid });
      removeItem(it);
      sfx.play('del');
      toast('지웠어요');
    } catch (e) {
      if (it.el) it.el.classList.remove('pressed');
      show(e);
    }
  }

  function confirmWipe() {
    const scrim = h('div', { class: 'sheet-scrim', onclick: ev => { if (ev.target === scrim) close(); } },
      h('div', { class: 'sheet', role: 'dialog', 'aria-modal': 'true' },
        h('h3', { text: '정말 지울까요?' }),
        h('p', { text: '대화가 모두 지워지고, 지난 대화 보기에서도 나오지 않아요.' }),
        h('button', { class: 'btn danger wide', text: '모두 지우기', onclick: () => { close(); wipeAll(); } }),
        h('button', { class: 'btn ghost wide', text: '그대로 두기', onclick: () => close() })));
    const close = layer(() => scrim.remove());
    $('#app').append(scrim);
  }

  async function wipeAll() {
    try {
      await api.post('chat/wipe', {});
      apply({ type: 'wiped' });
      sfx.play('del');
      toast('대화를 모두 지웠어요');
    } catch (e) {
      show(e);
    }
  }

  // ── 제목줄 · 상태 ──────────────────────
  function orbState() {
    if (recorder.recording) return 'recording';
    if (speaker.speaking) return 'speaking';
    if (offlineMessage()) return 'offline';
    return S.agentState;
  }

  function statusText() {
    if (recorder.recording) return '녹음 중 — 마이크나 보내기를 누르면 보내요';
    if (S.uploadingVoice) return '받아적는 중';
    if (voice.pending) return '보내지 못한 녹음이 있어요';
    if (speaker.speaking) return '말하는 중';
    const st = { thinking: '생각하는 중', working: '일하는 중', waiting: '대표님 허락을 기다려요' }[S.agentState];
    if (st) return st;
    if (S.link.k === 'online') return S.host === 'brain' ? 'PC 뒤의 두뇌와 연결됨' : 'PC 창과 연결됨';
    return S.link.k === 'connecting' ? '연결하는 중' : 'PC 에 닿지 않아요';
  }

  const modelLabel = () => (S.models.find(m => m.key === S.model) || {}).label || '보통';
  const modelPill = () => modelLabel().replace(/^SONNET 5\.5 /i, 'S5.5 ').replace(/^OPUS 5\.5 /i, 'O5.5 ');    // 제목줄은 좁다 — 고르는 메뉴엔 전체 이름

  function renderStatus() {
    const main = $('#main');
    if (!main) return;
    main.querySelector('.bar .orb').dataset.state = orbState();
    main.querySelector('.who b').textContent = S.name;
    main.querySelector('.who small').textContent = statusText();
    main.querySelector('.pill').textContent = modelPill();
    const banner = main.querySelector('.banner');
    const off = offlineMessage();
    banner.hidden = !off;
    if (off) banner.querySelector('span').textContent = off + ' · 다시 잇는 중';
    const mic = main.querySelector('.mic');
    mic.className = 'round mic' + (recorder.recording ? ' rec' : '');
    mic.disabled = S.uploadingVoice;
    mic.innerHTML = '';
    mic.append(S.uploadingVoice ? h('i', { class: 'spinner' }) : svg(recorder.recording ? 'stop' : 'mic'));
    mic.setAttribute('aria-label', recorder.recording ? '녹음 끝내고 보내기' : '말로 시키기');
    renderRecStrip();
    renderSend();
    renderDrawerFooter();
  }

  /** 녹음 띠 — 녹음 중(● 시간·소리 막대·✕) · 받아적는 중 · 보내지 못한 녹음(✕ 버리기). 띠가 있으면 입력칸을 감춘다. */
  function renderRecStrip() {
    const main = $('#main');
    const strip = main && main.querySelector('.rec-strip');
    if (!strip) return;
    const mode = recorder.recording ? 'rec' : S.uploadingVoice ? 'up' : voice.pending ? 'pend' : '';
    main.querySelector('.row textarea').hidden = !!mode;
    strip.hidden = !mode;
    if (strip.dataset.mode === mode && mode !== 'rec') return;
    if (mode === 'rec' && strip.dataset.mode === 'rec') {                 // 0.5초마다 — 시간만 바꾼다
      strip.querySelector('b').textContent = '녹음 중 ' + clock(Date.now() - voice.startedAt);
      return;
    }
    strip.dataset.mode = mode;
    strip.className = 'rec-strip ' + mode;
    strip.textContent = '';
    if (mode === 'rec') {
      strip.append(h('i', { class: 'rdot' }), h('b', { text: '녹음 중 ' + clock(Date.now() - voice.startedAt) }),
        h('span', { class: 'lv', 'aria-hidden': 'true' }, [0.7, 1.15, 1.5, 1.05, 0.8].map(k => h('i', { style: '--k:' + k }))),
        h('button', { class: 'x', 'aria-label': '녹음 버리기', onclick: cancelRecording, html: ICON.close }));
    } else if (mode === 'up') {
      strip.append(h('i', { class: 'spinner' }), h('b', { text: '받아적는 중' }), h('small', { text: voice.pending ? clock(voice.pending.ms) : '' }));
    } else if (mode === 'pend') {
      strip.append(svg('micSm'), h('b', { text: '녹음 ' + clock(voice.pending.ms) }), h('small', { text: '보내지 못했어요 — 보내기 ▶' }),
        h('button', { class: 'x', 'aria-label': '녹음 버리기', onclick: discardVoice, html: ICON.close }));
    }
  }

  function renderLevel() {
    const lvl = String(recorder.level.toFixed(3));
    document.querySelectorAll('.bar .orb, .mic, .rec-strip').forEach(el => el.style.setProperty('--lvl', lvl));
  }

  function renderSend() {
    const main = $('#main');
    if (!main) return;
    const ta = main.querySelector('textarea');
    const btn = main.querySelector('.send');
    const empty = !ta.value.trim() && !pending.length;
    const voiceMode = recorder.recording || (!!voice.pending && !S.uploadingVoice);   // 녹음을 보낼 수 있다
    const stopMode = !voiceMode && busy() && empty;
    btn.className = 'round send' + (stopMode ? ' stop' : '') + (voiceMode ? ' voice' : '');
    btn.disabled = S.uploadingVoice || attUploading || (!voiceMode && !stopMode && empty);
    btn.innerHTML = '';
    btn.append(svg(stopMode ? 'stop' : 'up'));
    btn.setAttribute('aria-label', voiceMode ? '녹음 보내기' : stopMode ? '멈추기' : '보내기');
  }

  /** 승인함 숫자 — 예전 PC(숫자를 안 주는)면 그대로 둔다 */
  function takeAppr(n) {
    if (typeof n !== 'number' || n === S.appr) return;
    S.appr = n;
    renderApprBadge();
    if (drawerEl) renderDrawer();
  }

  function takeDocs(n) {
    if (typeof n !== 'number' || n === S.docsNew) return;
    S.docsNew = n;
    renderDocsBadge();
  }

  function renderDocsBadge() {
    const btn = $('#main .bar .docs');
    if (!btn) return;
    const n = S.docsNew || 0, b = btn.querySelector('.nbadge');
    b.hidden = !n;
    b.textContent = n > 99 ? '99+' : String(n);
    btn.setAttribute('aria-label', '문서함' + (n ? ' — 새 자료 ' + n + '개' : ''));
  }

  const apprSub = () => (S.appr ? '기다리는 제안·메일 ' + S.appr + '건' : '효딩쓰가 올린 제안·메일 · 승인 · 보류');

  function renderApprBadge() {
    const btn = $('#main .bar .appr');
    if (!btn) return;
    const n = S.appr || 0, b = btn.querySelector('.nbadge');
    b.hidden = !n;
    b.textContent = n > 99 ? '99+' : String(n);
    btn.setAttribute('aria-label', '승인함' + (n ? ' — 기다리는 제안·메일 ' + n + '건' : ''));
  }

  // ── 화면: 연결 ─────────────────────────
  function showPair() {
    closeAllLayers();
    const app = $('#app');
    app.textContent = '';
    let busyPair = false;
    const msg = h('div', { class: 'msg' });
    const setMsg = (text, bad) => { msg.textContent = text || ''; msg.className = 'msg' + (bad ? ' bad' : ''); };
    setMsg(S.pairMessage, false);
    const o = orb('idle', 78);
    const server = h('input', { type: 'url', value: api.base, autocapitalize: 'off', autocomplete: 'off', spellcheck: 'false', 'aria-label': '서버 주소' });
    const go = h('button', { class: 'btn wide', text: '연결하기', disabled: true });
    const code = h('input', {
      class: 'code', inputmode: 'numeric', autocomplete: 'one-time-code', maxlength: '6', placeholder: '000000', 'aria-label': '연결 코드 6자리',
      oninput: () => {
        const d = code.value.replace(/\D/g, '').slice(0, 6);
        if (d !== code.value) code.value = d;
        go.disabled = d.length !== 6 || busyPair;
        if (d.length === 6) doPair();
      },
    });
    async function doPair() {
      if (code.value.length !== 6 || busyPair) return;
      busyPair = true;
      go.disabled = true;
      go.textContent = '';
      go.append(h('i', { class: 'spinner' }));
      o.dataset.state = 'thinking';
      setMsg('', false);
      api.base = server.value;
      const err = await pair(code.value);
      if (err) {
        busyPair = false;
        o.dataset.state = 'idle';
        go.textContent = '연결하기';
        setMsg(err, true);
        code.value = '';
        go.disabled = true;
        code.focus();
      }
    }
    go.addEventListener('click', doPair);
    app.append(h('div', { class: 'screen pair', id: 'pair' },
      h('div', { class: 'in' }, o, h('h1', { text: '효딩쓰와 연결' }),
        h('p', { html: 'PC 효딩쓰 창의 ☰ → 휴대폰 앱 →<br>「연결 코드 받기」를 누르고 6자리 숫자를 넣어 주세요.' }),
        code, msg, go,
        h('details', null, h('summary', { text: '서버 주소' }), server))),
      h('div', { class: 'toast', id: 'toast', role: 'status', 'aria-live': 'polite' }));
    setTimeout(() => code.focus(), 300);
    chatEl = listEl = helloEl = jumpEl = historyBtnEl = null;
  }

  // ── 화면: 대화 ─────────────────────────
  function showMain() {
    closeAllLayers();
    const app = $('#app');
    app.textContent = '';

    const ta = h('textarea', {
      rows: '1', 'aria-label': '효딩쓰에게 보낼 말', enterkeyhint: 'enter',
      oninput: () => { grow(ta); renderSend(); },
      onpaste: pasteAttach,
    });
    const sendBtn = h('button', {
      class: 'round send',
      onclick: () => {
        if (recorder.recording) { finishRecording(); return; }               // 녹음 중 — 끝내고 보낸다
        if (voice.pending && !S.uploadingVoice) { uploadVoice(voice.pending); return; }   // 끊겼던 녹음을 다시 보낸다
        if (pending.length) {
          const t = ta.value;
          sendAttached(replyPrefix() + t).then(ok => { if (ok) { clearReply(); if (ta.value === t) ta.value = ''; grow(ta); renderSend(); sfx.play('send'); } });
          return;
        }
        if (busy() && !ta.value.trim()) { stop(); return; }
        const typed = ta.value.trim();
        if (typed) sfx.play('send');
        send(typed ? replyPrefix() + ta.value : ta.value);
        if (typed) clearReply();
        ta.value = '';
        grow(ta);
        renderSend();
      },
    });
    const mic = h('button', { class: 'round mic', onclick: toggleRecording });
    const plusBtn = h('button', { class: 'round plus', 'aria-label': '사진·파일 붙이기', html: ICON.plus, onclick: ev => { sfx.play('tap'); attachMenu(ev.currentTarget); } });

    chatEl = h('div', { class: 'chat', onscroll: () => {
      atBottom = nearBottom();
      if (atBottom && jumpEl) jumpEl.hidden = true;
      if (chatEl.scrollTop <= 24) loadMoreHistory();
      scanSeen();
    } });
    ['touchstart', 'pointerdown', 'wheel'].forEach(t => chatEl.addEventListener(t, () => { touchedAt = Date.now(); }, { passive: true }));
    // 칸 크기가 바뀌면(키보드·입력칸 늘어남·연결 띠) 맨 아래를 보던 중이면 맨 아래에 붙여 둔다
    if (window.ResizeObserver) new ResizeObserver(() => { if (atBottom) pinBottom(); }).observe(chatEl);
    listEl = h('div', { class: 'items' });
    helloEl = helloView();
    historyBtnEl = h('button', { class: 'hist-btn', onclick: loadMoreHistory, text: '지난 대화 보기' });
    renderHistoryBtn();
    const pullEl = h('div', { class: 'pull' });
    chatEl.append(pullEl, h('div', { class: 'chat-inner' }, historyBtnEl, helloEl, listEl));
    jumpEl = h('button', { class: 'jump', hidden: true, 'aria-label': '맨 아래로', onclick: jumpNext }, svg('down'), h('span', { class: 'jn' }));
    pullToRefresh(chatEl, pullEl);
    bindLongPress(listEl);

    const bar = h('div', { class: 'bar' },
      h('button', { class: 'icon', 'aria-label': '메뉴 — 대화 상대 고르기', onclick: openDrawer, html: ICON.menu }),
      orb(orbState(), 26),
      h('div', { class: 'who' }, h('div', { class: 'wname' }, h('b'), h('button', { class: 'pill', 'aria-label': '생각의 깊이', onclick: ev => modelMenu(ev.currentTarget) })), h('small')),
      h('button', { class: 'icon photos', 'aria-label': '사진', onclick: () => { sfx.play('tap'); openPhotos(); }, html: ICON.photo }),
      h('button', { class: 'icon docs', 'aria-label': '문서함', onclick: () => { sfx.play('tap'); openDocs(); } },
        svg('folder'), h('span', { class: 'nbadge', hidden: true })),
      h('button', { class: 'icon appr', 'aria-label': '승인함', onclick: () => { sfx.play('tap'); openAppr(); } },
        svg('inbox'), h('span', { class: 'nbadge', hidden: true })));

    app.append(h('div', { class: 'screen', id: 'main' },
      bar,
      h('div', { class: 'banner', hidden: true }, h('i', { class: 'spinner' }), h('span')),
      h('div', { class: 'chat-wrap' }, chatEl, jumpEl),
      h('div', { class: 'composer' },
        h('div', { class: 'reply-strip', hidden: true }),
        h('div', { class: 'att-strip', hidden: true }),
        h('div', { class: 'row' }, plusBtn, h('div', { class: 'rec-strip', hidden: true, role: 'status', 'aria-live': 'polite' }), ta, mic, sendBtn))),
      h('div', { class: 'toast', id: 'toast', role: 'status', 'aria-live': 'polite' }));

    renderAllItems();
    renderStatus();
    renderApprBadge();
    renderAttach();
    renderReply();
    renderDocsBadge();
    scrollToBottom(false);
    loadPendingVoice();
  }

  function helloView() {
    return h('div', { class: 'hello' },
      orb('idle', 68),
      h('h2', { text: '대표님, 부르시면 바로 옵니다' }),
      h('p', { html: '아래에 적거나 마이크를 눌러 말씀하세요.<br>PC 의 효딩쓰와 같은 대화예요.' }));
  }

  function grow(ta) {
    ta.style.height = 'auto';
    const want = ta.scrollHeight + 3;
    ta.style.height = Math.max(44, Math.min(want, 132)) + 'px';
    ta.style.overflowY = want > 132 ? 'auto' : 'hidden';
  }

  /** 대화 맨 위에서 아래로 당기면 처음부터 다시 받는다(아이폰의 당겨서 새로 고침) */
  function pullToRefresh(scroller, ind) {
    let y0 = null, dy = 0;
    scroller.addEventListener('touchstart', e => { y0 = scroller.scrollTop <= 0 ? e.touches[0].clientY : null; dy = 0; }, { passive: true });
    scroller.addEventListener('touchmove', e => {
      if (y0 == null) return;
      dy = e.touches[0].clientY - y0;
      if (dy <= 0) { ind.style.height = '0'; return; }
      ind.classList.add('on');
      ind.style.height = Math.min(dy * 0.5, 60) + 'px';
      ind.textContent = dy > 120 ? '놓으면 새로 받아요' : '당겨서 새로 받기';
    }, { passive: true });
    scroller.addEventListener('touchend', () => {
      ind.classList.remove('on');
      ind.style.height = '0';
      if (y0 != null && dy > 120) { reload(); toast('처음부터 다시 받는 중'); }
      y0 = null;
    });
  }

  const modelNote = l => /^opus/i.test(l) ? '가장 깊이 생각 · 사용량 가장 많음' : /ultracode/i.test(l) ? '여러 에이전트로 꼼꼼히 · 사용량 많음' : '';

  function modelMenu(anchor) {
    const r = anchor.getBoundingClientRect();
    const menu = h('div', { class: 'menu', role: 'menu', style: 'top:' + (r.bottom + 6) + 'px;left:' + Math.max(8, Math.min(r.left, innerWidth - 250)) + 'px' },
      h('h4', { text: '모델' }),
      S.models.map(m => h('button', { role: 'menuitemradio', 'aria-checked': m.key === S.model ? 'true' : 'false', onclick: () => { close(); sfx.play('tap'); setModel(m.key); } },
        h('i', { text: m.key === S.model ? '✓' : '' }),
        h('span', { class: 'ml' }, m.label, modelNote(m.label) ? h('small', { text: modelNote(m.label) }) : null))));
    const scrim = h('div', { class: 'menu-scrim', onclick: () => close() });
    const close = layer(() => { scrim.remove(); menu.remove(); });
    $('#app').append(scrim, menu);
  }

  // ── ☰ 대화 상대 ────────────────────────
  let drawerEl = null;

  function openDrawer() {
    if (drawerEl) return;
    const scrim = h('div', { class: 'scrim', onclick: () => close() });
    drawerEl = h('nav', { class: 'drawer', 'aria-label': '대화 상대' });
    const d = drawerEl;
    const close = layer(() => {
      scrim.classList.remove('on');
      d.classList.remove('on');
      if (drawerEl === d) drawerEl = null;
      setTimeout(() => { scrim.remove(); d.remove(); }, 230);
    });
    d.close = close;
    let x0 = null;
    d.addEventListener('touchstart', e => { x0 = e.touches[0].clientX; }, { passive: true });
    d.addEventListener('touchend', e => { if (x0 != null && e.changedTouches[0].clientX - x0 < -60) close(); x0 = null; });
    $('#app').append(scrim, d);
    renderDrawer();
    requestAnimationFrame(() => { scrim.classList.add('on'); d.classList.add('on'); });
    refreshWorkers();
  }

  function drawerRow(title, subtitle, current, av, action) {
    return h('button', { class: 'drow' + (current ? ' cur' : ''), onclick: action },
      h('span', { class: 'av' }, av),
      h('span', { class: 'tx' }, h('b', { text: title }), subtitle ? h('small', { text: subtitle }) : null),
      current ? null : svg('chev'));
  }

  function renderDrawer() {
    const d = drawerEl;
    if (!d) return;
    const top = d.querySelector('.scroll') ? d.querySelector('.scroll').scrollTop : 0;
    d.textContent = '';
    const close = d.close;
    const scroll = h('div', { class: 'scroll' },
      drawerRow('승인함', apprSub(), false, h('span', { class: 'badge-ic appr' }, svg('inbox')), () => { close(); openAppr(); }),
      drawerRow('문서함', '영상 · 사진 · 문서 · 음성', false, h('span', { class: 'badge-ic' }, svg('folder')), () => { close(); openDocs(); }),
      drawerRow('사진', '구글 포토에서 옮긴 사진 · 영상', false, h('span', { class: 'badge-ic' }, svg('photo')), () => { close(); openPhotos(); }),
      drawerRow('설정', '목소리 · 말 빠르기 · 화면 · 연결', false, h('span', { class: 'badge-ic' }, svg('gear')), () => { close(); openSettings(); }),
      h('div', { class: 'dsep' }),
      drawerRow(S.name, '비서실장 · 지금 이 대화', true, orb('idle', 26), close),
      h('div', { class: 'sec', text: 'AI OFFICE 직원' }),
      S.workers.length ? null : h('div', { class: 'dnote', text: '명단을 불러오는 중이거나 AI Office 가 꺼져 있어요.' }),
      S.workers.map(w => drawerRow(w.role ? w.name + ' · ' + w.role : w.name, w.statusLine, false, avatar(w, 32), () => { close(); openWorker(w); })),
      h('div', { class: 'sec', text: '더 보기' }),
      drawerRow('새 대화', '효딩쓰와 처음부터 다시', false, h('span', { class: 'badge-ic' }, svg('pencil')), () => { newChat(); close(); }));
    d.append(
      h('header', null, h('button', { 'aria-label': '닫기', onclick: close, html: ICON.close }), h('h2', { text: '대화 상대' })),
      scroll,
      h('footer', null, h('i'), h('span')));
    scroll.scrollTop = top;
    renderDrawerFooter();
  }

  function renderDrawerFooter() {
    if (!drawerEl) return;
    const off = offlineMessage();
    const f = drawerEl.querySelector('footer');
    if (!f) return;
    f.querySelector('i').className = off ? 'off' : '';
    f.querySelector('span').textContent = off || hostLabel();
  }

  // ── 겹쳐 여는 화면 ─────────────────────
  const pages = [];
  const layers = [];                  // 뒤로 가기로 닫을 것들(메뉴·서랍·확인 창) — 맨 뒤가 먼저

  /** 닫는 함수를 뒤로 가기 목록에 올린다. 여러 번 불려도 한 번만 닫힌다. */
  function layer(closeFn) {
    let done = false;
    const close = () => {
      if (done) return;
      done = true;
      const i = layers.indexOf(close);
      if (i >= 0) layers.splice(i, 1);
      closeFn();
    };
    layers.push(close);
    return close;
  }

  function pushPage(title, content, onClose, opts) {
    opts = opts || {};
    const page = h('div', { class: 'screen page' },
      h('div', { class: 'bar sub' },
        ...(() => {
          const btn = h('button', { class: 'icon', 'aria-label': opts.close ? '닫기' : '뒤로', onclick: () => popPage(), html: opts.close ? ICON.close : ICON.back });
          const who = h('div', { class: 'who' }, h('b', { text: title }));
          const gap = h('span', { class: 'spacer' });
          return opts.close ? [gap, who, btn] : [btn, who, gap];   // 문서함·승인함은 X 가 오른쪽 끝 (2026-11-06 대표님)
        })()),
      content);
    page.onClose = onClose;
    pages.push(page);
    bindSwipeClose(page);
    $('#app').append(page);
    requestAnimationFrame(() => requestAnimationFrame(() => page.classList.add('on')));
    return page;
  }

  /** 왼쪽에서 오른쪽으로 밀면 이 화면(문서함·승인함 등)이 닫힌다 (2026-11-02 대표님) — 오른쪽 끝에서 밀려 들어온 화면이니 온 길로 돌려보낸다.
      글 쓰는 칸·가로로 밀리는 줄(칩·사진 줄)·열린 창 위에서 시작한 손짓은 그쪽 몫이라 건드리지 않는다. */
  function bindSwipeClose(page) {
    let x0 = 0, y0 = 0, t0 = 0, on = false;
    page.addEventListener('touchstart', ev => {
      on = false;
      if (ev.touches.length !== 1 || pages[pages.length - 1] !== page || layers.length) return;
      const t = ev.target;
      if (t.closest && t.closest('input,textarea,select,[contenteditable]')) return;
      for (let n = t; n && n !== page; n = n.parentElement) {
        if (n.scrollWidth > n.clientWidth + 2 && /auto|scroll/.test(getComputedStyle(n).overflowX)) return;
      }
      x0 = ev.touches[0].clientX; y0 = ev.touches[0].clientY; t0 = Date.now(); on = true;
    }, { passive: true });
    page.addEventListener('touchend', ev => {
      if (!on) return;
      on = false;
      const c = ev.changedTouches[0], dx = c.clientX - x0, dy = c.clientY - y0;
      if (dx > 80 && Math.abs(dy) < dx * 0.5 && Date.now() - t0 < 800 && pages[pages.length - 1] === page) {
        haptic('tap');
        sfx.play('tap');
        popPage();
      }
    }, { passive: true });
  }

  function popPage() {
    const page = pages.pop();
    if (!page) return false;
    if (page.onClose) page.onClose();
    page.classList.remove('on');
    setTimeout(() => page.remove(), 260);
    return true;
  }

  function closeAllLayers() {
    while (layers.length) layers[layers.length - 1]();
    while (pages.length) { const p = pages.pop(); if (p.onClose) p.onClose(); p.remove(); }
  }

  // ── 직원과 따로 대화 (WorkerChatView) ────
  function openWorker(w) {
    let messages = [], waiting = false, loading = true, alive = true;
    const list = h('div', { style: 'display:flex;flex-direction:column;gap:10px;padding:16px' });
    const content = h('div', { class: 'content' }, list);
    const ta = h('textarea', { rows: '1', 'aria-label': w.name + ' 님에게 지시', oninput: () => { grow(ta); sync(); } });
    const btn = h('button', { class: 'round send', 'aria-label': '보내기', onclick: sendIt }, svg('up'));
    const composer = h('div', { class: 'composer' }, h('div', { class: 'row' }, ta, btn));
    const wrap = h('div', { style: 'flex:1;min-height:0;display:flex;flex-direction:column' }, content, composer);
    pushPage(w.name, wrap, () => { alive = false; });

    function sync() {
      btn.disabled = waiting || !ta.value.trim();
    }

    function draw() {
      const stick = content.scrollHeight - content.scrollTop - content.clientHeight < 90;
      list.textContent = '';
      list.append(h('div', { class: 'wk-head' }, avatar(w, 46),
        h('div', { style: 'min-width:0' },
          h('b', { text: w.role ? w.name + ' · ' + w.role : w.name }),
          h('small', { text: w.team }),
          w.task ? h('div', { class: 'now', text: '지금: ' + w.task }) : null)));
      if (loading) list.append(h('div', { class: 'empty' }, h('i', { class: 'spinner', style: 'display:inline-block' })));
      else if (!messages.length) list.append(h('div', { class: 'empty', text: '아직 나눈 이야기가 없어요. 아래에 지시를 적어 보세요.' }));
      messages.forEach(m => list.append(officeBubble(m, w)));
      if (waiting) list.append(h('div', { class: 'typing' }, avatar(w, 28), h('i', { class: 'spinner' }), w.name + ' 님이 답을 쓰는 중'));
      sync();
      if (stick) requestAnimationFrame(() => { content.scrollTop = content.scrollHeight; });
    }

    async function load() {
      try {
        messages = await officeHistory(w.id);
      } catch (e) {
        show(e);
      }
      loading = false;
      if (alive) draw();
    }

    /** 지시를 보내고, 직원 답이 올 때까지 3초마다 기록을 다시 본다 (최대 4분) */
    async function sendIt() {
      const t = ta.value.trim();
      if (!t || waiting) return;
      ta.value = '';
      grow(ta);
      const lastReply = (messages.slice().reverse().find(m => !m.mine) || {}).content;
      messages.push({ sender: 'ceo', name: '', content: t, time: '', mine: true });
      waiting = true;
      draw();
      content.scrollTop = content.scrollHeight;
      try {
        await officeChat(w.id, t);
        for (let i = 0; i < 80 && alive; i++) {
          await sleep(3000);
          if (!alive) return;
          const got = await officeHistory(w.id);
          const last = got[got.length - 1];
          if (last && !last.mine && last.content !== lastReply) {
            messages = got;
            waiting = false;
            draw();
            return;
          }
        }
        waiting = false;
        if (alive) { draw(); toast(w.name + ' 님이 아직 답하지 않았어요. 나중에 다시 열어 보세요.'); }
      } catch (e) {
        waiting = false;
        if (alive) draw();
        show(e);
      }
    }

    draw();
    load();
  }

  function officeBubble(m, w) {
    if (m.mine) {
      return h('div', { class: 'u-row' }, h('div', { class: 'u-bubble', text: m.content }));
    }
    return h('div', { class: 'o-row' }, avatar(w, 28),
      h('div', { class: 'col' },
        h('div', { class: 'who2' }, m.name || w.name, m.time ? h('span', { text: m.time }) : null),
        h('div', { class: 'o-bubble md', html: markdown(m.content) })));
  }

  // ── 문서함 (DocsView) ───────────────────
  const DOC_KINDS = [['', '전체'], ['video', '영상'], ['image', '사진'], ['doc', '문서'], ['audio', '음성']];
  const DOC_ICON = { video: '🎬', image: '🖼', doc: '📄', audio: '🔊' };
  const DOC_WORD = { video: '영상', image: '사진', audio: '음성' };

  function openDocs() {
    S.docsNew = 0;
    renderDocsBadge();
    let kind = '', q = '', items = [], pinned = [], more = false, total = 0, loading = false, loadedOnce = false, alive = true, searchTimer = 0;

    const totalEl = h('div', { class: 'doc-total' });
    const chipsEl = h('div', { class: 'doc-chips' });
    const searchEl = h('input', {
      type: 'search', placeholder: '제목으로 찾기', 'aria-label': '문서함 검색', inputmode: 'search',
      oninput: () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => { q = searchEl.value.trim(); reload(); }, 400); },
    });
    const listEl2 = h('div', { class: 'doc-list' });
    const content = h('div', { class: 'content doc-content' }, h('div', { class: 'doc-head' }, totalEl, searchEl), chipsEl, listEl2);

    function drawChips() {
      chipsEl.textContent = '';
      DOC_KINDS.forEach(([k, label]) => chipsEl.append(h('button', {
        class: 'dchip' + (kind === k ? ' on' : ''), text: label,
        onclick: () => { if (kind === k) return; kind = k; drawChips(); reload(); },
      })));
    }
    drawChips();

    function countLabel(it) {
      const n = (it.files || []).length;
      if (n <= 1) return '';
      const word = DOC_WORD[it.kind] || '파일';
      return word + ' ' + n + (it.kind === 'image' ? '장' : '개');
    }

    const isPinned = it => pinned.some(p => p.id === it.id);

    /** 카드 한 장 — 오른쪽→왼쪽으로 밀면 「고정 · 삭제」 단추가 나온다 (2026-11-21 대표님).
        꾹 누르기(0.5초): 이미 고정된 카드면 「고정 해제」 메뉴, 아니면 예전처럼 바로 고정 (2026-11-02). */
    const ACT_W = 144;
    let openWrap = null;
    const closeSwipe = except => { if (openWrap && openWrap !== except) { openWrap.classList.remove('open'); openWrap = null; } };
    content.addEventListener('touchstart', ev => { if (openWrap && !openWrap.contains(ev.target)) closeSwipe(); }, { passive: true });

    function bindPin(wrap, row, it) {
      let timer = 0, x0 = 0, y0 = 0, base = 0, live = false, drag = false, dxNow = 0;
      const cancel = () => { clearTimeout(timer); timer = 0; };
      const clampX = v => Math.max(-ACT_W, Math.min(0, v));
      row.addEventListener('touchstart', ev => {
        live = ev.touches.length === 1 && !ev.target.closest('.dl-btn');
        if (!live) return;
        x0 = ev.touches[0].clientX; y0 = ev.touches[0].clientY; drag = false; dxNow = 0;
        base = wrap.classList.contains('open') ? -ACT_W : 0;
        closeSwipe(wrap);
        cancel();
        if (base) return;                                            // 열려 있는 카드는 꾹 누르기 없음(닫기만)
        timer = setTimeout(() => {                                   // 꾹 누르기 — 손가락을 뗄 때 생기는 클릭은 열기로 새지 않게 막는다
          timer = 0; live = false;
          pressHeld = true; clearTimeout(heldTimer); heldTimer = setTimeout(() => { pressHeld = false; }, 4000);
          if (isPinned(it)) openPinMenu(it); else togglePin(it);
        }, PRESS_MS);
      }, { passive: true });
      row.addEventListener('touchmove', ev => {
        if (!live) return;
        const dx = ev.touches[0].clientX - x0, dy = ev.touches[0].clientY - y0;
        if (!drag) {
          if (Math.abs(dx) > 10 && Math.abs(dx) > Math.abs(dy) * 1.5) {      // 옆으로 미는 중 — 카드를 따라 움직인다
            drag = true; cancel();
            wrap.classList.add('drag');
          } else if (Math.hypot(dx, dy) > 10) { cancel(); return; }
        }
        if (drag) { dxNow = dx; row.style.transform = 'translateX(' + clampX(base + dx) + 'px)'; }
      }, { passive: true });
      row.addEventListener('touchend', () => {
        cancel();
        if (!drag) { live = false; return; }
        live = false; drag = false;
        const open = clampX(base + dxNow) < -ACT_W / 2;
        wrap.classList.remove('drag');
        row.style.transform = '';
        wrap.classList.toggle('open', open);
        openWrap = open ? wrap : (openWrap === wrap ? null : openWrap);
        swallowUntil = Date.now() + 450;
        if (open && !base) haptic('tap');
      }, { passive: true });
      row.addEventListener('touchcancel', () => {
        cancel(); live = false;
        if (drag) { drag = false; wrap.classList.remove('drag'); row.style.transform = ''; }
      }, { passive: true });
      row.addEventListener('contextmenu', ev => { ev.preventDefault(); });   // 안드로이드 꾹 누르기 메뉴는 위 타이머가 대신한다
    }

    function togglePin(it) {
      const on = !isPinned(it), before = pinned.slice();
      pinned = on ? [it].concat(pinned.filter(p => p.id !== it.id)) : pinned.filter(p => p.id !== it.id);
      haptic('tap'); sfx.play('tap');
      toast(on ? '맨 위에 고정했어요' : '고정을 풀었어요');
      closeSwipe();
      draw();
      api.post('docs/pin', { id: it.id, on }).catch(e => { pinned = before; if (alive) { draw(); show(e); } });
    }

    /** 목록에서만 뺀다 — 파일은 PC 에 남고, 뺀 기록은 docs/deleted.jsonl 에 있다 */
    function deleteDoc(it) {
      const b = { items: items.slice(), pinned: pinned.slice(), total };
      items = items.filter(x => x.id !== it.id);
      pinned = pinned.filter(x => x.id !== it.id);
      total = Math.max(0, total - 1);
      haptic('tap'); sfx.play('del');
      toast('지웠어요');
      closeSwipe();
      draw();
      api.post('docs/delete', { id: it.id }).catch(e => { items = b.items; pinned = b.pinned; total = b.total; if (alive) { draw(); show(e); } });
    }

    /** 고정된 카드를 꾹 누르면 — 「고정 해제」 (삭제도 같이) */
    function openPinMenu(it) {
      if (document.querySelector('.msg-menu')) return;
      haptic('tap'); sfx.play('menu');
      const mrow = (icon, label, fn, cls) => h('button', { class: 'mrow' + (cls ? ' ' + cls : ''), onclick: () => { close(); fn(); } }, icon, label);
      const scrim = h('div', { class: 'sheet-scrim', onclick: ev => { if (ev.target === scrim) close(); } },
        h('div', { class: 'sheet msg-menu', role: 'dialog', 'aria-modal': 'true', 'aria-label': '고정 메뉴' },
          h('p', { class: 'peek', text: it.title || '(제목 없음)' }),
          mrow(h('span', { class: 'pin-ic', text: '📌' }), '고정 해제', () => togglePin(it)),
          mrow(svg('trash'), '이 자료 삭제', () => deleteDoc(it), 'danger'),
          h('button', { class: 'btn ghost wide', text: '닫기', onclick: () => close() })));
      const close = layer(() => scrim.remove());
      $('#app').append(scrim);
    }

    function docRow(it) {
      const files = it.files || [];
      const pin = isPinned(it);
      const wrap = h('div', { class: 'drow-wrap' });
      const row = h('div', { class: 'drow2' + (pin ? ' pinned' : ''), onclick: () => { if (openWrap === wrap) { closeSwipe(); return; } rowTap(it); } },
        it.thumb ? h('img', { class: 'dthumb', src: it.thumb, alt: '', loading: 'lazy' }) : h('div', { class: 'dthumb ic', text: DOC_ICON[it.kind] || '📄' }),
        h('div', { class: 'dtx' },
          h('b', { text: (pin ? '📌 ' : '') + (it.title || '(제목 없음)') }),
          h('small', { text: timeOnly(it.ts) + (countLabel(it) ? ' · ' + countLabel(it) : '') })),
        files.some(f => f.url)
          ? h('button', { class: 'dl-btn', 'aria-label': '내려받기', html: ICON.download,
              onclick: ev => { ev.stopPropagation(); sfx.play('tap'); downloadRow(files); } })
          : null,
        files.some(f => f.url)
          ? h('button', { class: 'dl-btn', 'aria-label': '공유', html: ICON.share,
              onclick: ev => { ev.stopPropagation(); sfx.play('tap'); shareFiles(files, it.title || ''); } })
          : null);
      if (!it.id) { wrap.append(row); return wrap; }
      wrap.append(
        h('div', { class: 'drow-act' },
          h('button', { class: 'da pin', onclick: () => togglePin(it) }, h('span', { class: 'da-ic', text: '📌' }), pin ? '고정 해제' : '고정'),
          h('button', { class: 'da del', onclick: () => deleteDoc(it) }, svg('trash'), '삭제')),
        row);
      bindPin(wrap, row, it);
      return wrap;
    }

    /** 내려받기 — 한 파일이면 바로, 여러 장은 브라우저에서 차례로(앱 안에서는 열어서 한 장씩) */
    function downloadRow(files) {
      const fs = files.filter(f => f && f.url);
      if (fs.length === 1) { downloadFile(fs[0].url, fs[0].name); return; }
      if (Native) {
        if (typeof Native.saveToPhotos === 'function' && fs.every(f => isMediaFile(f.url, f.name))) {
          toast('사진첩에 저장하는 중이에요');
          (async () => {
            let ok = 0;
            for (const f of fs) { try { await Native.saveToPhotos({ url: new URL(f.url, location.href).href, name: f.name || nameFromUrl(f.url) }); ok++; } catch (e) { /* 한 장 실패해도 나머지는 */ } }
            toast(ok === fs.length ? ok + '장을 사진첩에 저장했어요' : fs.length + '장 중 ' + ok + '장만 저장했어요');
          })();
          return;
        }
        toast('여러 장은 열어서 한 장씩 저장해 주세요'); rowTap({ files: fs, title: '' }); return;
      }
      fs.forEach((f, i) => setTimeout(() => downloadFile(f.url, f.name), i * 700));
    }

    /** 항목의 파일 전부를 앱 안 보기 화면으로 — 첫 장부터 */
    function rowTap(it) {
      const files = (it.files || []).filter(f => f && f.url);
      if (!files.length) return;
      openViewer(files.map(f => ({ url: f.url, name: f.name || '', kind: fileKind(f.name, f.url, it.kind) })), 0, it.title || '');
    }

    function draw() {
      totalEl.textContent = loadedOnce ? '전체 ' + total + '개 · 왼쪽으로 밀면 고정·삭제' : '';
      listEl2.textContent = '';
      if (loading && !items.length) { listEl2.append(h('div', { class: 'empty' }, h('i', { class: 'spinner', style: 'display:inline-block' }))); return; }
      if (!items.length && !pinned.length) { listEl2.append(h('div', { class: 'doc-empty', text: '아직 자료가 없어요 — 효딩쓰가 만든 영상·카드뉴스·캡처가 여기 모입니다' })); return; }
      if (pinned.length) {
        listEl2.append(h('div', { class: 'doc-day pin', text: '📌 고정' }));
        pinned.forEach(it => listEl2.append(docRow(it)));
      }
      let lastDay = null;
      items.forEach(it => {
        if (isPinned(it)) return;
        const day = dayLabel(it.ts);
        if (day !== lastDay) { listEl2.append(h('div', { class: 'doc-day', text: day })); lastDay = day; }
        listEl2.append(docRow(it));
      });
      if (more) listEl2.append(h('button', { class: 'doc-more', disabled: loading, text: loading ? '불러오는 중' : '더 보기', onclick: loadMore }));
    }

    async function load(before) {
      loading = true;
      draw();
      try {
        const query = { kind, q, n: 30 };
        if (before) query.before = before;
        const r = await api.get('docs', query);
        if (!alive) return;
        const got = r.items || [];
        if (!before) pinned = r.pinned || [];
        items = before ? items.concat(got) : got;
        more = !!r.more;
        total = typeof r.total === 'number' ? r.total : items.length;
        loadedOnce = true;
      } catch (e) {
        if (alive) show(e);
      } finally {
        loading = false;
        if (alive) draw();
      }
    }

    function reload() {
      items = [];
      more = false;
      load();
    }

    function loadMore() {
      if (!items.length || loading) return;
      load(items[items.length - 1].ts);
    }

    pushPage('문서함', content, () => { alive = false; }, { close: true });
    load();
  }

  // ── 사진 (PhotosView — 2026-11-20 대표님) ──
  // 구글 포토에서 내려받은 사진·영상을 PC 의 Immich 가 그 자리에서 읽고(복사 없음), 이 앱의 📷 화면으로만 보여 준다. 밖에 공개하지 않는다.
  // 목록·앨범 담기는 기기 열쇠(photos…), 사진·영상 내용은 서명 링크(/AI/photo/…)로 — 얼굴 인식은 쓰지 않는다.
  // 사진첩(앨범) 칩·「담기」는 숨겼다 (2026-11-20 대표님 「상단의 사진첩은 없어도 될 것 같아」) — 코드는 그대로, true 로 바꾸면 돌아온다.
  const PH_ALBUMS = false;
  const PH_TABS = [['all', '전체'], ['image', '사진'], ['video', '영상'], ['place', '장소'], ['album', '사진첩'], ['folder', '폴더']].filter(t => PH_ALBUMS || t[0] !== 'album');
  const PH_LIST = { place: 'places', album: 'albums', folder: 'folders' };
  const OM = 1e6;                                   // 순서값(_o) = 묶음 번호 × OM + 묶음 안 번호 — 한 묶음(한 달)에 100만 장 미만
  const phYM = day => { const m = DAY_RE.exec(day || ''); return m ? m[1] + '년 ' + (+m[2]) + '월' : ''; };
  const phMonthLabel = m => phYM(m + '-01');
  /** 달 m('2019-03')의 다음 달 1일 — 「이 날 전」(before)으로 쓰면 그 달이 맨 위에 온다 */
  const phNextMonth = m => { const y = +m.slice(0, 4), mo = +m.slice(5, 7); return mo === 12 ? (y + 1) + '-01-01' : y + '-' + String(mo + 1).padStart(2, '0') + '-01'; };
  const WEEKDAY = ['일', '월', '화', '수', '목', '금', '토'];
  const DAY_RE = /^(\d{4})-(\d\d)-(\d\d)$/;

  function phDay(day, withYear) {
    const m = DAY_RE.exec(day || '');
    if (!m) return '날짜 없음';
    const d = new Date(+m[1], +m[2] - 1, +m[3]);
    return (withYear || d.getFullYear() !== new Date().getFullYear() ? m[1] + '년 ' : '') + (+m[2]) + '월 ' + (+m[3]) + '일 ' + WEEKDAY[d.getDay()] + '요일';
  }

  function phDur(sec) {
    sec = Math.max(0, Math.round(sec || 0));
    const hr = Math.floor(sec / 3600), mn = Math.floor(sec % 3600 / 60), s = String(sec % 60).padStart(2, '0');
    return hr ? hr + ':' + String(mn).padStart(2, '0') + ':' + s : mn + ':' + s;
  }

  /** 사진 가로세로 비율 — 줄 맞춤(구글 포토처럼 사진 비율 그대로)에 쓴다. 너무 길쭉한 것은 잘라 보이게 0.56~2 로 */
  const phAr = it => (it.w > 0 && it.h > 0) ? Math.round(Math.min(2, Math.max(0.56, it.w / it.h)) * 1000) / 1000 : 1;
  /** 검색창에 「동영상」「사진」을 치면 이름이 아니라 종류로 찾는다 (구글 포토와 같게) */
  const PH_TYPEWORD = { '동영상': 'video', '영상': 'video', '비디오': 'video', 'video': 'video', 'videos': 'video', '사진': 'image', '이미지': 'image', 'photo': 'image', 'photos': 'image' };

  const phCap = it => [phDay(it.day, true) + (it.time ? ' ' + it.time : '') + (it.city ? ' · ' + it.city : ''), it.name];
  const phEntry = it => ({ url: it.view, name: it.name, kind: it.kind, dl: it.orig, thumb: it.thumb, poster: it.kind === 'video' ? it.thumb : '', id: it.id, cap: phCap(it) });

  function openPhotos() {
    let tab = 'all', q = '', filt = null, items = [], page = 0, more = false, loading = false, failed = '', alive = true, searchTimer = 0, gen = 0, listBusy = false, listFail = '';
    let stats = null, sel = null, endSelLayer = null, albums = null, sentinel = null;
    let before = '', months = null, monthPos = null, monthsTotal = 0, dayGrids = [], whenTimer = 0, scrubDrag = false, whenRaf = 0, shownMonth = '';
    let anchor = -1, drag = null;                 // 고르기 — 마지막으로 누른 자리(범위 고르기 기준) · 꾹 누른 채 끄는 중
    // 시간표 방식(전체 탭 · 검색·분류 없음): 연월마다 칸(groups)을 먼저 깔아 두고 가까운 달부터 채운다 — 건너뛰어도 이미 불러온 달은 그대로 남는다
    let tl = false, groups = [], g0 = null, io = null, inflight = 0, domTiles = 0, perItem = 0, scrollAt = 0, touching = false, idleTimer = 0, bgTimer = 0;
    const store = new Map(), gByM = new Map(), want = new Set(), later = [];   // 달 → 불러온 묶음 · 화면 가까이 온 묶음 · 위쪽이라 스크롤이 멎은 뒤에 반영할 일들
    const dayHdr = new Map(), pos = new Map();   // 날짜 머리글의 동그라미 · 사진 번호 → 순서값(_o)
    const lists = {};
    const tiles = new Map();
    const deleted = new Set();                    // 이 화면에서 지우거나 옮긴 사진 번호 — 되돌리면 뺀다
    g0 = mkGroup(0, '', 0, null);

    const statEl = h('div', { class: 'doc-total' });
    const searchEl = h('input', {
      type: 'search', placeholder: '날짜(2023-5) · 이름 · 「동영상」으로 찾기', 'aria-label': '사진 검색', inputmode: 'search',
      oninput: () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => { q = searchEl.value.trim(); clearBefore(); if (isList()) draw(); else reload(); }, 400); },
    });
    const cloudBtn = Native && typeof Native.backupStatus === 'function'
      ? h('button', { class: 'ph-pick ph-cloud', 'aria-label': '폰 사진 자동 백업', html: ICON.cloud, onclick: () => openPhoneBackup() }) : null;
    const chipsEl = h('div', { class: 'doc-chips' });
    const filtEl = h('div', { class: 'ph-filt', hidden: true });
    const bodyEl = h('div', { class: 'ph-body' });
    const content = h('div', { class: 'content doc-content ph-content' },
      h('div', { class: 'doc-head' }, h('div', { class: 'ph-search' }, searchEl, cloudBtn), statEl), chipsEl, filtEl, bodyEl);
    g0.el = bodyEl;
    const selBar = h('div', { class: 'ph-selbar', hidden: true });
    // 스크롤하는 동안 보이는 연월 · 오른쪽 가장자리의 빠른 스크롤 막대 · 「최신으로」
    const whenEl = h('div', { class: 'ph-when', hidden: true, 'aria-hidden': 'true' });
    const knobEl = h('div', { class: 'ph-knob' });
    const trackEl = h('div', { class: 'ph-scrub', hidden: true }, knobEl);
    const latestEl = h('button', { class: 'ph-latest', hidden: true, onclick: () => { clearBefore(); endSel(); reload(); content.scrollTop = 0; } }, svg('up'), '최신으로');

    const isList = () => !filt && !!PH_LIST[tab];
    const scrubOK = () => !isList() && !filt && !q && !!months && monthsTotal > 150;   // 막대는 전체 시간표를 볼 때만 — 작은 묶음(검색·폴더)은 연월 표시만
    const hasMon = () => !isList() && tab === 'all' && !filt && !q && !!months && months.length > 0;   // 시간표 방식으로 보일 수 있는 화면

    function drawChips() {
      chipsEl.textContent = '';
      PH_TABS.forEach(([k, label]) => chipsEl.append(h('button', {
        class: 'dchip' + (tab === k ? ' on' : ''), text: label,
        onclick: () => { if (tab === k && !filt) return; tab = k; filt = null; clearBefore(); endSel(); gen++; loading = false; drawChips(); if (isList()) openList(); else reload(); },
      })));
      // 폰 앱에서만 — 백업이 끝난 사진·영상을 아이폰 사진첩에서 치운다 (분류 칩이 아니라 동작이라 모양을 다르게)
      if (BK_ON && typeof Native.backupCleanupPlan === 'function') {
        chipsEl.append(h('button', { class: 'dchip act', 'aria-label': '폰 사진첩 정리', onclick: () => openCleanup() }, h('span', { class: 'ic', html: ICON.broom }), '정리'));
      }
    }

    function drawFilt() {
      filtEl.hidden = !filt;
      filtEl.textContent = '';
      if (filt) filtEl.append(h('button', { class: 'dchip on', 'aria-label': filt.label + ' 풀기', onclick: () => { filt = null; clearBefore(); endSel(); draw(); if (!isList()) reload(); } },
        filt.label, h('span', { class: 'x', html: ICON.close })));
    }

    function drawStat() {
      if (isList()) statEl.textContent = '';
      else if (!filt && !q && tab === 'all' && stats) statEl.textContent = '사진 ' + stats.photos.toLocaleString('ko-KR') + ' · 영상 ' + stats.videos.toLocaleString('ko-KR') + ' · 꾹 눌러 고르기';
      else statEl.textContent = PH_ALBUMS ? '꾹 눌러 끌면 여러 장 · 날짜 옆 동그라미는 그날 전부 — 골라서 담기·공유·저장해요' : '꾹 눌러 끌면 여러 장 · 날짜 옆 동그라미는 그날 전부 — 골라서 공유·저장해요';
    }

    function note(text, retry) {
      bodyEl.append(h('div', { class: 'doc-empty' }, text, retry ? h('div', null, h('button', { class: 'doc-more', text: '다시 시도', onclick: retry })) : null));
    }

    // ── 장소 · 사진첩 · 폴더 목록 ──
    async function openList() {
      draw();
      const kind = tab;
      if (lists[kind]) return;
      listBusy = true; listFail = ''; draw();
      try { lists[kind] = (await api.get('photos/' + PH_LIST[kind])).items || []; }
      catch (e) { listFail = (e && e.message) || '불러오지 못했어요'; if (e && e.status === 401) show(e); }
      listBusy = false;
      if (alive && kind === tab && isList()) draw();
    }

    function drawList() {
      if (listBusy && !lists[tab]) { bodyEl.append(h('div', { class: 'empty' }, h('i', { class: 'spinner', style: 'display:inline-block' }))); return; }
      if (listFail && !lists[tab]) { note(listFail, () => { listFail = ''; openList(); }); return; }
      const key = q.toLowerCase();
      const rows = (lists[tab] || []).filter(r => !key || [r.name, r.city, r.country, r.path].join(' ').toLowerCase().includes(key));
      if (!rows.length) {
        note(key ? '찾는 이름이 없어요' : tab === 'album' ? '아직 사진첩이 없어요 — 사진을 꾹 눌러 고른 뒤 「사진첩에 담기」로 만들어 보세요'
          : tab === 'place' ? '아직 장소 정보가 있는 사진이 없어요' : '폴더가 없어요');
        return;
      }
      if (tab === 'folder') {
        bodyEl.append(h('div', { class: 'ph-folders' }, rows.map(r => h('button', {
          class: 'drow2', style: 'padding-left:' + (10 + Math.min(r.depth, 4) * 14) + 'px',
          onclick: () => enter({ label: r.name, query: { path: r.path } }),
        }, h('span', { class: 'dthumb ic ph-fic', html: ICON.folder }),
          h('div', { class: 'dtx' }, h('b', { text: r.name }), r.depth ? h('small', { text: r.path }) : null), svg('chev')))));
        return;
      }
      bodyEl.append(h('div', { class: 'ph-cards' }, rows.map(r => {
        const place = tab === 'place';
        return h('button', {
          class: 'ph-card',
          onclick: () => enter(place ? { label: r.city, query: { city: r.city, country: r.country } } : { label: r.name, query: { album: r.id } }),
        }, r.thumb ? h('img', { src: r.thumb, alt: '', loading: 'lazy', decoding: 'async', draggable: 'false' }) : h('span', { class: 'ph-card-ph', html: ICON.photo }),
          h('span', { class: 'cap' }, h('b', { text: place ? r.city : r.name }),
            h('small', { text: place ? r.country : r.count + '장' + (r.start ? ' · ' + r.start.slice(0, 7).replace('-', '.') : '') })));
      })));
    }

    function enter(f) {
      filt = f; q = ''; searchEl.value = ''; clearBefore();
      drawChips(); reload();
    }

    // ── 날짜별 사진 ──
    function resetGrid() {
      tlOff();
      bodyEl.textContent = '';
      tiles.clear();
      dayGrids = [];
      dayHdr.clear(); pos.clear(); anchor = -1;
      g0.lastDay = g0.lastGrid = g0.lastCity = null;
      sentinel = null;
    }

    function draw() {
      drawChips(); drawFilt(); drawStat();
      resetGrid();
      if (isList()) { drawList(); return; }
      if (failed && !items.length) { note(failed, () => reload()); return; }
      if (loading && !items.length) { bodyEl.append(h('div', { class: 'empty' }, h('i', { class: 'spinner', style: 'display:inline-block' }))); return; }
      if (!items.length) { note(q || filt || tab !== 'all' ? '조건에 맞는 사진이 없어요' : '아직 사진이 없어요 — PC 가 사진을 읽는 중일 수 있어요'); return; }
      addItems(items, g0);
      drawTail();
    }

    // ── 묶음(group) — 연월 하나(시간표 방식) 또는 목록 전체(검색·분류·종류별) ──
    //  _o(순서값) = 묶음 번호 × OM + 묶음 안 번호. 이 값의 크기로 「어디부터 어디까지」를 따진다(범위 고르기).
    function mkGroup(i, m, n, el) {
      return { i, m, n, el, items: [], days: new Map(), st: 'idle', page: 0, more: true, dom: false, full: false, p: null, rendered: 0, h: 0, failAt: 0, lastDay: null, lastGrid: null, lastCity: null, dirty: false };
    }

    /** 받아 온 사진을 묶음의 자료로 — 순서값을 매기고 날짜별 번호를 모은다(그림 그리기와 별개) */
    function pushData(g, list) {
      if (deleted.size) list = list.filter(it => !deleted.has(it.id));          // 방금 지운·옮긴 사진이 오는 길에 끼어 있었어도 다시 나타나지 않게
      if (g.dirty && g.items.length) { const have = new Set(g.items.map(x => x.id)); list = list.filter(it => !have.has(it.id)); }   // 지운 뒤 이어 받으면 겹치는 쪽이 생길 수 있다
      list.forEach(it => {
        it._o = g.i * OM + g.items.length;
        g.items.push(it);
        const a = g.days.get(it.day);
        if (a) a.push(it.id); else g.days.set(it.day, [it.id]);
      });
      return list;
    }

    function reindex(g) {
      g.days.clear();
      const list = g.items; g.items = [];
      pushData(g, list);
    }

    const itemAt = o => { const g = tl ? groups[Math.floor(o / OM)] : g0; return g ? g.items[o % OM] : null; };
    const allItems = () => tl ? groups.reduce((a, g) => a.concat(g.items), []) : items;
    const gOf = day => tl ? gByM.get((day || '').slice(0, 7)) : g0;

    /** 그날 사진 번호들 */
    function idsOf(day) {
      const g = gOf(day);
      return (g && g.days.get(day)) || [];
    }

    function addItems(list, g) {
      const days = new Set();
      list.forEach(it => {
        pos.set(it.id, it._o);
        if (it.day !== g.lastDay) {
          g.lastDay = it.day; g.lastCity = h('small', { text: it.city || '' });
          const dc = dayCheck(it.day);
          dayHdr.set(it.day, dc);
          g.el.append(h('div', { class: 'ph-day' }, dc, h('b', { text: phDay(it.day) }), g.lastCity));
          g.lastGrid = h('div', { class: 'ph-grid jr' });
          g.el.append(g.lastGrid);
          if (!tl) dayGrids.push({ el: g.lastGrid, day: it.day });
        } else if (it.city && g.lastCity && !g.lastCity.textContent) g.lastCity.textContent = it.city;
        days.add(it.day);
        const t = tile(it);
        tiles.set(it.id, t);
        g.lastGrid.append(t);
        if (sel && sel.has(it.id)) t.setAttribute('aria-pressed', 'true');
      });
      if (sel) days.forEach(paintDay);      // 고르는 중에 더 불러왔을 때 그 날의 동그라미 상태를 다시
    }

    /** 날짜 머리글 왼쪽 동그라미 — 누르면 그날 사진·영상을 전부 고르고(다시 누르면 전부 풀고), 일부만 골랐으면 반쯤 찬 모양 */
    function dayCheck(day) {
      return h('button', { class: 'ph-dchk', 'aria-label': phDay(day, true) + ' 전부 고르기', onclick: ev => { ev.stopPropagation(); toggleDay(day); } });
    }

    function drawTail() {
      if (sentinel) sentinel.remove();
      sentinel = more || loading ? h('div', { class: 'ph-tail' }, loading ? h('i', { class: 'spinner', style: 'display:inline-block' })
        : h('button', { class: 'doc-more', text: '더 보기', onclick: () => load() })) : null;
      if (sentinel) bodyEl.append(sentinel);
    }

    function tile(it) {
      const img = h('img', { src: it.thumb, alt: '', loading: 'lazy', decoding: 'async', draggable: 'false' });
      const t = h('button', { class: 'ph-tile' + (it.kind === 'video' ? ' vid' : '') + (sel && sel.has(it.id) ? ' sel' : ''),
        'aria-label': (it.kind === 'video' ? '영상 ' : '사진 ') + phDay(it.day, true) + (it.time ? ' ' + it.time : ''), onclick: ev => tapTile(it, ev) },
        img,
        it.kind === 'video' ? h('span', { class: 'ph-dur' }, it.dur ? phDur(it.dur) : null, svg('play')) : null,
        it.up ? h('span', { class: 'ph-up', title: '이 폰에서 올린 사진', html: ICON.check }) : null,
        h('span', { class: 'ph-chk', 'aria-hidden': 'true' }));
      img.addEventListener('error', () => t.classList.add('bad'));
      t._it = it;
      t.style.setProperty('--ar', phAr(it));
      bindHold(t, it);
      return t;
    }

    /** 꾹 누르기(0.5초) = 고르기 시작 — 손가락을 뗄 때 생기는 클릭이 열기로 새지 않게 막는다(문서함 고정과 같은 방식) */
    function bindHold(t, it) {
      let timer = 0, x0 = 0, y0 = 0;
      const cancel = () => { clearTimeout(timer); timer = 0; };
      t.addEventListener('pointerdown', ev => {
        if (ev.button > 0) return;
        x0 = ev.clientX; y0 = ev.clientY; cancel();
        timer = setTimeout(() => {
          timer = 0;
          pressHeld = true; clearTimeout(heldTimer); heldTimer = setTimeout(() => { pressHeld = false; }, 4000);
          haptic('tap'); sfx.play('tap');
          if (!sel) startSel();
          // 꾹 누른 그 사진을 고르고(이미 골라져 있으면 풀고), 손가락을 뗄 때까지 끌고 가는 사진들을 같은 방식으로 — 구글 포토의 끌어서 고르기
          const i = pos.get(it.id);
          const mode = sel.has(it.id) ? 'remove' : 'add';
          drag = { a: i, cur: i, mode, base: new Set(sel), lo: i, hi: i, x: x0, y: y0, v: 0, raf: 0 };
          anchor = i;
          setSel(it, mode === 'add');
          paintDay(it.day); paintSel();
        }, PRESS_MS);
      });
      t.addEventListener('pointermove', ev => { if (timer && Math.hypot(ev.clientX - x0, ev.clientY - y0) > 10) cancel(); });
      ['pointerup', 'pointercancel', 'pointerleave'].forEach(n => t.addEventListener(n, cancel));
      t.addEventListener('contextmenu', ev => ev.preventDefault());
    }

    function tapTile(it, ev) {
      if (sel) { toggle(it, ev); return; }
      const run = around(it), from = Math.max(0, run.i - 40);
      // 지우기·이동은 확인 없이 — 보던 사진이 바로 사라지고 다음 사진이 보인다(viewer 가 처리). 서버 일은 뒤에서
      openViewer(run.list.slice(from, run.i + 80).map(phEntry), run.i - from, '사진', {
        photo: true,
        album: PH_ALBUMS ? e => pickAlbum([e.id], '이 사진') : null,
        del: e => trashIds([e.id]),
        move: e => moveIds([e.id]),
      });
    }

    /** 사진 보기에서 넘겨 볼 앞뒤 사진들 — 끝까지 불러온 앞·뒤 달까지 이어서 */
    function around(it) {
      if (!tl) return { list: items, i: items.indexOf(it) };
      const gi = Math.floor(it._o / OM), g = groups[gi], prev = groups[gi - 1], next = groups[gi + 1];
      let list = [], off = 0;
      if (prev && prev.st === 'ready') { list = prev.items; off = list.length; }
      list = list.concat(g.items);
      if (next && next.st === 'ready') list = list.concat(next.items);
      return { list, i: off + (it._o % OM) };
    }

    // ── 여러 장 고르기 ──
    function startSel() {
      if (sel) return;
      sel = new Set();
      endSelLayer = layer(() => { sel = null; endSelLayer = null; drag = null; anchor = -1; paintSel(); });
      paintSel();
    }

    function endSel() { if (endSelLayer) endSelLayer(); }

    function setSel(it, on) {
      if (on) sel.add(it.id); else sel.delete(it.id);
      const t = tiles.get(it.id);
      if (t) { t.classList.toggle('sel', on); t.setAttribute('aria-pressed', on ? 'true' : 'false'); }
    }

    /** 누르면 한 장 고르기/풀기 · (마우스) Shift 를 누른 채 누르면 마지막으로 누른 사진부터 여기까지 한꺼번에 */
    function toggle(it, ev) {
      if (!sel) return;
      const i = pos.get(it.id), a = anchor, from = a >= 0 ? itemAt(a) : null;
      if (ev && ev.shiftKey && from && i != null) {
        const mode = sel.has(from.id) ? 'add' : 'remove', base = new Set(sel);
        applyRange(a, i, mode, base, a, a);
        paintSel();
        if (tl && Math.floor(a / OM) !== Math.floor(i / OM)) settle(a, i, mode, base);     // 달을 건너뛰면 사이의 달도 불러와서
        return;
      }
      setSel(it, !sel.has(it.id));
      if (i != null) anchor = i;
      paintDay(it.day);
      paintSel();
    }

    /** 목록 자리 a~b 를 mode('add'|'remove') 로 — 그 밖은 base(범위를 적용하기 전 상태) 그대로. prevLo~prevHi 는 지난번 범위(줄어든 쪽을 되돌리려고) */
    function applyRange(a, b, mode, base, prevLo, prevHi) {
      const lo = Math.min(a, b), hi = Math.max(a, b), days = new Set();
      const from = Math.min(lo, prevLo), to = Math.max(hi, prevHi), g1 = Math.floor(from / OM), g2 = Math.floor(to / OM);
      for (let gi = g1; gi <= g2; gi++) {
        const g = tl ? groups[gi] : g0;
        if (!g) continue;
        const k1 = gi === g1 ? from % OM : 0, k2 = gi === g2 ? Math.min(to % OM, g.items.length - 1) : g.items.length - 1;
        for (let k = k1; k <= k2; k++) {
          const it = g.items[k];
          if (!it) continue;
          const o = gi * OM + k, want = o >= lo && o <= hi ? mode === 'add' : base.has(it.id);
          if (sel.has(it.id) !== want) { setSel(it, want); days.add(it.day); }
        }
      }
      days.forEach(paintDay);
      return [lo, hi];
    }

    /** 범위가 여러 달에 걸치면 사이의 달을 끝까지 불러온 뒤 다시 한 번 적용 — 아직 안 불러온 달의 사진도 빠짐없이 */
    async function settle(a, b, mode, base) {
      const lo = Math.min(a, b), hi = Math.max(a, b), g1 = Math.floor(lo / OM), g2 = Math.floor(hi / OM);
      const need = [];
      for (let gi = g1; gi <= g2; gi++) if (groups[gi] && groups[gi].st !== 'ready') need.push(groups[gi]);
      if (!need.length) return;
      toast('사이의 사진을 불러오는 중이에요');
      for (const g of need) { if (!alive || !sel || !tl) return; await loadAll(g); }
      if (!alive || !sel) return;
      applyRange(a, b, mode, base, lo, hi);
      paintSel();
    }

    function paintDay(day) {
      const b = dayHdr.get(day);
      if (!b) return;
      const ids = idsOf(day), g = gOf(day);
      let n = 0;
      if (sel) for (const id of ids) if (sel.has(id)) n++;
      const last = g && g.items.length ? g.items[g.items.length - 1].day : '', partial = tl ? !!g && g.st !== 'ready' : more;
      const all = n > 0 && n === ids.length && !(partial && day === last);   // 아직 덜 불러온 쪽의 마지막 날은 「전부」가 아닐 수 있다
      b.classList.toggle('on', all);
      b.classList.toggle('part', n > 0 && !all);
      if (sel) b.setAttribute('aria-pressed', all ? 'true' : n ? 'mixed' : 'false'); else b.removeAttribute('aria-pressed');
    }

    /** 그날 전부 고르기/풀기. 그날이 목록 맨 끝이고 더 불러올 게 남았으면 그날이 끝날 때까지 이어서 불러온 뒤에 고른다 */
    async function toggleDay(day) {
      haptic('tap');
      if (!sel) startSel();
      if (tl) {
        const g = gOf(day);
        if (g && g.st !== 'ready') {                    // 그 달을 끝까지 불러와야 그날 전부를 알 수 있다
          toast('그날 사진을 모두 불러오는 중이에요');
          await loadAll(g);
          if (!alive || !sel) return;
        }
      } else {
        const last = items[items.length - 1];
        if (more && last && last.day === day) {
          toast('그날 사진을 모두 불러오는 중이에요');
          for (let g = 0; g < 40 && alive && more && items.length && items[items.length - 1].day === day; g++) {
            if (loading) await new Promise(r => setTimeout(r, 250)); else await load();
          }
          if (!alive || !sel) return;
        }
      }
      const list = (tl ? (gOf(day) || { items: [] }).items : items).filter(it => it.day === day);
      const all = list.length > 0 && list.every(it => sel.has(it.id));
      list.forEach(it => setSel(it, !all));
      const i = pos.get((list[0] || {}).id);
      if (i != null) anchor = i;
      paintDay(day);
      paintSel();
    }

    // ── 꾹 누른 채 끌어서 고르기 ──
    function dragMove(x, y) {
      if (!drag) return;
      drag.x = x; drag.y = y;
      const el = document.elementFromPoint(x, y), t = el && el.closest ? el.closest('.ph-tile') : null, it = t && t._it;
      const i = it ? pos.get(it.id) : null;
      if (i != null && i !== drag.cur) {
        drag.cur = i;
        const r = applyRange(drag.a, i, drag.mode, drag.base, drag.lo, drag.hi);
        drag.lo = r[0]; drag.hi = r[1];
        paintSel();
      }
      // 위·아래 가장자리 가까이 가면 화면이 알아서 넘어간다 (아래는 고르기 막대 높이만큼 더 일찍)
      const box = content.getBoundingClientRect(), up = 84, down = 128;
      const v = y < box.top + up ? -(1 - Math.max(0, y - box.top) / up) : y > box.bottom - down ? (1 - Math.max(0, box.bottom - y) / down) : 0;
      drag.v = v ? Math.sign(v) * (3 + Math.min(1, Math.abs(v)) * 15) : 0;
      if (drag.v && !drag.raf) drag.raf = requestAnimationFrame(autoScroll);
    }

    function autoScroll() {
      if (!drag) return;
      drag.raf = 0;
      if (!drag.v) return;
      content.scrollTop += drag.v;
      dragMove(drag.x, drag.y);
      if (drag && drag.v && !drag.raf) drag.raf = requestAnimationFrame(autoScroll);
    }

    function endDrag() {
      if (!drag) return;
      const d = drag;
      cancelAnimationFrame(d.raf);
      drag = null;
      swallowUntil = Date.now() + 450;               // 손가락을 뗄 때 생기는 클릭이 방금 고른 사진을 다시 뒤집지 않게
      if (tl && Math.floor(d.a / OM) !== Math.floor(d.cur / OM)) settle(d.a, d.cur, d.mode, d.base);   // 빠르게 지나친 달까지 빠짐없이
    }

    function chosen() { return allItems().filter(it => sel && sel.has(it.id)); }

    function paintSel() {
      content.classList.toggle('selecting', !!sel);
      selBar.hidden = !sel;
      if (!sel) {
        tiles.forEach(t => { t.classList.remove('sel'); t.removeAttribute('aria-pressed'); });
        dayHdr.forEach(b => { b.classList.remove('on', 'part'); b.removeAttribute('aria-pressed'); });
        return;
      }
      const n = sel.size, inAlbum = PH_ALBUMS && filt && filt.query.album;
      selBar.textContent = '';
      [h('button', { class: 'ph-x', 'aria-label': '고르기 끝내기', html: ICON.close, onclick: endSel }),
        h('div', { class: 'cnt', text: n ? n + '개 선택됨' : '사진을 눌러 고르세요' }),
        PH_ALBUMS ? act('albumAdd', '담기', n, () => pickAlbum(chosen().map(it => it.id), n + '장')) : null,
        act('share', '공유', n, shareSel),
        act('save', '저장', n, saveSel),
        act('folder', '이동', n, moveSel),
        inAlbum ? act('minusCircle', '빼기', n, removeSel) : null,
        act('trash', '삭제', n, trashSel)].forEach(x => { if (x) selBar.append(x); });
    }

    // ── 지우기 · 옮기기 (2026-11-22 대표님) ──
    // 지우면 확인 없이 바로 그 폴더 옆 「휴지통」으로 — 30일 뒤 완전히 지워진다. 화면에서는 먼저 치우고(빠르게) PC 가 파일을 옮긴다.
    function trashSel() {
      const ids = chosen().map(it => it.id);
      if (!ids.length) return;
      endSel();
      trashIds(ids);
    }

    function moveSel() {
      const ids = chosen().map(it => it.id);
      if (!ids.length) return;
      moveIds(ids).then(ok => { if (ok) endSel(); });
    }

    /** 번호들에 해당하는 사진 — 지금까지 불러온 것 중에서 */
    function findItems(idset) {
      const out = [];
      (tl ? groups : [g0]).forEach(g => g.items.forEach(it => { if (idset.has(it.id)) out.push(it); }));
      return out;
    }

    const monthsOf = its => [...new Set(its.map(it => (it.day || '').slice(0, 7)).filter(Boolean))];

    /** 달별 장수(막대·칸 높이)를 서버 값으로 다시 맞춘다 */
    function recount() {
      monthsTotal = months ? months.reduce((s, m) => s + m.n, 0) : 0;
      monthPos = {};
      let cum = 0;
      (months || []).forEach(m => { monthPos[m.m] = { cum, n: m.n }; cum += m.n; });
    }

    /** 지운·옮긴 사진을 지금 화면에서 뺀다 — 시간표는 해당 달 칸만 다시 그리고(보던 자리 그대로), 검색·분류 목록은 그대로 다시 그린다 */
    function dropLocal(its) {
      if (!its.length) return;
      const gone = new Set(its.map(it => it.id));
      gone.forEach(id => deleted.add(id));
      if (sel) gone.forEach(id => sel.delete(id));
      if (stats) { its.forEach(it => { if (it.kind === 'video') stats.videos = Math.max(0, stats.videos - 1); else stats.photos = Math.max(0, stats.photos - 1); }); drawStat(); }
      if (!tl) {
        const top = content.scrollTop;
        g0.items = g0.items.filter(it => !gone.has(it.id));
        reindex(g0);
        items = g0.items;
        g0.dirty = true;
        page = Math.floor(items.length / 60);
        draw();
        content.scrollTop = top;
        return;
      }
      groups.forEach(g => {
        if (!g.items.some(it => gone.has(it.id))) return;
        const old = g.items, left = old.filter(it => !gone.has(it.id));
        old.forEach(it => { tiles.delete(it.id); pos.delete(it.id); });
        g.days.forEach((_ids, day) => dayHdr.delete(day));
        const was = g.dom;
        if (g.dom) domTiles -= g.rendered;
        g.items = []; g.days.clear();
        pushData(g, left);
        g.n = Math.max(0, g.n - (old.length - left.length));
        g.dirty = true;
        g.page = Math.floor(left.length / 100);
        g.dom = false; g.rendered = 0; g.lastDay = g.lastGrid = g.lastCity = null;
        const mm = months && months.find(x => x.m === g.m);
        if (mm) mm.n = g.n;
        if (was && (left.length || g.st === 'ready')) paintGroup(g); else { g.h = 0; waitBox(g); }
      });
      recount();
    }

    /** 되돌리기·옮기기 뒤 — 서버(Immich 가 훑고 나면)의 그 달 목록과 맞춘다. 달라진 게 없으면 화면은 그대로 둔다. 달라졌으면 true */
    async function syncMonth(m) {
      const g = gByM.get(m);
      if (!g || !alive || !tl || g.p || (g.st === 'idle' && !g.items.length)) return false;
      const got = [];
      for (let p = 1; p <= 80; p++) {
        const r = await api.get('photos', { q: m, page: p, n: 100 });
        const it = r.items || [];
        got.push(...it);
        if (!r.more || !it.length) break;
      }
      if (!alive || !tl || g.p || gByM.get(m) !== g) return false;
      const fresh = got.filter(it => !deleted.has(it.id));
      if (g.st === 'ready' && g.items.length === fresh.length && g.items.every((it, i) => it.id === fresh[i].id)) return false;
      g.items.forEach(it => { tiles.delete(it.id); pos.delete(it.id); });
      g.days.forEach((_ids, day) => dayHdr.delete(day));
      const was = g.dom;
      if (g.dom) domTiles -= g.rendered;
      g.items = []; g.days.clear();
      pushData(g, fresh);
      g.n = fresh.length; g.page = Math.ceil(fresh.length / 100); g.more = false; g.st = 'ready'; g.dirty = false;
      g.dom = false; g.rendered = 0; g.lastDay = g.lastGrid = g.lastCity = null;
      if (was) paintGroup(g); else { g.h = 0; waitBox(g); }
      const mm = months && months.find(x => x.m === m);
      if (mm) { mm.n = g.n; recount(); }
      return true;
    }

    /** 달들을 몇 번에 나눠 맞춘다 — PC 가 폴더를 훑는 데 걸리는 시간만큼 간격을 두고, 맞춰졌으면 그만 */
    function syncMonths(ms, delays) {
      if (!tl || !ms.length) return;
      let k = 0;
      const step = async () => {
        if (!alive || !tl) return;
        let changed = false;
        for (const m of ms) { try { if (await syncMonth(m)) changed = true; } catch (e) { /* 다음 차례에 */ } }
        if (!changed && ++k < delays.length) setTimeout(step, delays[k]);
      };
      setTimeout(step, delays[0]);
    }

    /** 사진·영상 지우기(휴지통으로). 확인 창 없이 바로 — 되돌리기는 알림의 「되돌리기」. 화면은 먼저 치워 두고 서버 결과로 맞춘다. */
    async function trashIds(ids) {
      const its = findItems(new Set(ids));
      if (!its.length) return false;
      ids = its.map(it => it.id);
      const ms = monthsOf(its);
      dropLocal(its);
      if (ids.length > 30) toast('휴지통으로 옮기는 중이에요 (' + ids.length + '장)');
      const runs = [];
      let lastErr = null, ok = new Set();
      for (let i = 0; i < ids.length; i += 100) {
        const part = ids.slice(i, i + 100);
        try {
          const r = await api.post('photos/trash', { ids: part }, { timeout: 60000 });
          const done = Array.isArray(r.ids) ? r.ids : (r.failed ? [] : part);
          done.forEach(id => ok.add(id));
          if (done.length) runs.push({ batch: r.batch, ids: done });
        } catch (e) { lastErr = e; }
      }
      const bad = its.filter(it => !ok.has(it.id));
      if (bad.length) {                                      // 못 지운 것은 다시 보이게
        bad.forEach(it => deleted.delete(it.id));
        syncMonths(monthsOf(bad), [300, 3000]);
      }
      if (!ok.size) { if (lastErr) show(lastErr); else toast('지우지 못했어요'); return false; }
      const msg = ok.size + '장을 휴지통으로 옮겼어요' + (bad.length ? ' (' + bad.length + '장은 못 지웠어요)' : '');
      toast(msg, {
        label: '되돌리기',
        run: async () => {
          try {
            for (const r of runs) await api.post('photos/restore', { batch: r.batch, ids: r.ids }, { timeout: 60000 });
            ok.forEach(id => deleted.delete(id));
            toast('되돌렸어요 — 사진첩에 곧 다시 나타나요');
            syncMonths(ms, [4000, 15000, 40000]);
          } catch (e) { show(e); }
        },
      });
      return true;
    }

    /** 폴더로 옮기기 — 폴더 고르는 창을 띄우고, 옮기면 true 를 돌려준다 */
    function moveIds(ids) {
      const its = findItems(new Set(ids));
      if (!its.length) return Promise.resolve(false);
      return new Promise(resolve => pickFolder(its, resolve));
    }

    async function pickFolder(its, done) {
      let finished = false;
      const rowsEl = h('div', { class: 'ph-pickrows' });
      const findEl = h('input', { type: 'search', class: 'ph-find', placeholder: '폴더 이름으로 찾기', 'aria-label': '폴더 찾기', oninput: () => drawRows() });
      const nameEl = h('input', { type: 'text', maxlength: '80', placeholder: '새 폴더 이름', 'aria-label': '새 폴더 이름', onkeydown: ev => { if (ev.key === 'Enter') make(); } });
      const newBox = h('div', { class: 'ph-new', hidden: true }, nameEl, h('button', { class: 'btn', text: '만들고 옮기기', onclick: make }));
      const sheet = h('div', { class: 'sheet ph-sheet', role: 'dialog', 'aria-modal': 'true', 'aria-label': '폴더로 이동' },
        h('h3', { text: '폴더로 이동' }),
        h('p', { text: its.length + '장을 옮길 폴더를 골라 주세요' }),
        findEl,
        h('button', { class: 'ph-row new', onclick: () => { newBox.hidden = false; nameEl.focus(); } },
          h('span', { class: 'ph-rthumb', html: ICON.plus }), h('div', { class: 'dtx' }, h('b', { text: '새 폴더 만들기' }))),
        newBox, rowsEl,
        h('button', { class: 'btn ghost wide', text: '닫기', onclick: () => close() }));
      const scrim = h('div', { class: 'sheet-scrim over', onclick: ev => { if (ev.target === scrim) close(); } }, sheet);
      const close = layer(() => { scrim.remove(); if (!finished) done(false); });
      $('#app').append(scrim);

      function drawRows() {
        const key = findEl.value.trim().toLowerCase(), all = lists.folder || [];
        const rows = all.filter(r => !key || (r.name + ' ' + r.path).toLowerCase().includes(key)).slice(0, 300);
        rowsEl.textContent = '';
        if (!lists.folder) { rowsEl.append(h('div', { class: 'doc-empty' }, h('i', { class: 'spinner', style: 'display:inline-block' }))); return; }
        if (!rows.length) { rowsEl.append(h('div', { class: 'doc-empty', text: key ? '찾는 폴더가 없어요 — 위에서 새로 만들 수 있어요' : '폴더가 없어요 — 위에서 새로 만들어 보세요' })); return; }
        rows.forEach(r => rowsEl.append(h('button', { class: 'ph-row', style: key ? '' : 'padding-left:' + (8 + Math.min(r.depth, 4) * 14) + 'px', onclick: () => go(r.path, r.name) },
          h('span', { class: 'ph-rthumb', html: ICON.folder }), h('div', { class: 'dtx' }, h('b', { text: r.name }), r.depth || key ? h('small', { text: r.path }) : null))));
      }

      function make() {
        const name = nameEl.value.trim().replace(/[\\/]+/g, ' ');
        if (!name) { nameEl.focus(); return; }
        go(name, name);
      }

      /** 고른 폴더로 — 창은 바로 닫고 화면에서 먼저 치운다. 서버가 못 옮긴 것(폰에서 올린 사진 등)은 다시 보인다 */
      async function go(path, name) {
        if (finished) return;
        finished = true;
        close();
        done(true);
        const ids = its.map(it => it.id), ms = monthsOf(its);
        dropLocal(its);
        if (ids.length > 30) toast('옮기는 중이에요 (' + ids.length + '장)');
        const ok = new Set();
        let skipped = 0, lastErr = null;
        for (let i = 0; i < ids.length; i += 100) {
          const part = ids.slice(i, i + 100);
          try {
            const r = await api.post('photos/move', { ids: part, to: path }, { timeout: 90000 });
            (Array.isArray(r.ids) ? r.ids : (r.done === part.length ? part : [])).forEach(id => ok.add(id));
            skipped += r.skipped || 0;
          } catch (e) { lastErr = e; }
        }
        const bad = its.filter(it => !ok.has(it.id));
        if (bad.length) { bad.forEach(it => deleted.delete(it.id)); syncMonths(monthsOf(bad), [300, 3000]); }
        if (ok.size) {
          delete lists.folder;
          toast('「' + name + '」로 ' + ok.size + '장을 옮겼어요' + (skipped ? ' (' + skipped + '장은 옮길 수 없는 사진이에요)' : ''));
          syncMonths(ms, [6000, 20000, 50000, 120000, 300000]);     // 새 칸은 Immich 가 훑고 정보(메타데이터)를 읽은 뒤에야 보인다 — 대기열이 밀려 있으면 오래 걸린다
        } else if (lastErr) show(lastErr);
        else toast(skipped ? '옮길 수 없는 사진이에요 (이미 그 폴더에 있거나 폰에서 올린 사진)' : '옮기지 못했어요');
      }

      drawRows();
      if (!lists.folder) {
        try { lists.folder = (await api.get('photos/folders')).items || []; }
        catch (e) { rowsEl.textContent = ''; rowsEl.append(h('div', { class: 'doc-empty', text: (e && e.message) || '폴더 목록을 불러오지 못했어요' })); return; }
        drawRows();
      }
    }

    function act(icon, label, n, fn) {
      return h('button', { class: 'ph-act', disabled: !n, 'aria-label': label, onclick: fn }, svg(icon), h('span', { text: label }));
    }

    function shareSel() {
      const list = chosen();
      if (list.length > 10) { toast('공유는 한 번에 10장까지예요 — 나눠서 보내 주세요'); return; }
      shareFiles(list.map(it => ({ url: it.orig, name: it.name })), '사진');
    }

    /** 저장 — 앱 안은 폰 사진첩에 한 장씩 바로, 브라우저는 파일로 차례로 받는다 */
    async function saveSel() {
      const list = chosen();
      if (list.length > 30) { toast('저장은 한 번에 30장까지예요 — 나눠서 받아 주세요'); return; }
      if (Native && typeof Native.saveToPhotos === 'function') {
        if (list.length > 3) toast('폰에 저장하는 중이에요 (' + list.length + '장)');
        let ok = 0;
        for (const it of list) {
          try { await Native.saveToPhotos({ url: nativeSaveUrl(it.orig), name: it.name }); ok++; } catch (e) { /* 한 장 실패해도 나머지는 */ }
        }
        toast(ok === list.length ? (ok > 1 ? ok + '장을 ' : '') + '폰에 저장했어요' : list.length + '장 중 ' + ok + '장만 저장했어요');
        return;
      }
      if (list.length === 1) { downloadFile(list[0].orig, list[0].name); return; }
      toast('내려받는 중이에요 (' + list.length + '장)');
      list.forEach((it, i) => setTimeout(() => downloadFile(it.orig, it.name), i * 700));
    }

    async function removeSel() {
      const ids = chosen().map(it => it.id), album = filt && filt.query.album;
      if (!ids.length || !album) return;
      try {
        await api.post('photos/album', { op: 'remove', album, ids });
        const gone = new Set(ids);
        g0.items = items.filter(it => !gone.has(it.id));
        reindex(g0);
        items = g0.items;
        delete lists.album;
        toast(ids.length + '장을 이 사진첩에서 뺐어요 (사진 파일은 그대로예요)');
        endSel(); draw();
      } catch (e) { show(e); }
    }

    // ── 사진첩에 담기 (이름표만 붙인다 — 파일은 옮기지 않고, 한 장이 여러 사진첩에 들어갈 수 있다) ──
    async function pickAlbum(ids, label) {
      if (!ids.length) return;
      const listEl3 = h('div', { class: 'ph-pickrows' });
      const nameEl = h('input', { type: 'text', maxlength: '60', placeholder: '새 사진첩 이름', 'aria-label': '새 사진첩 이름',
        onkeydown: ev => { if (ev.key === 'Enter') make(); } });
      const makeBtn = h('button', { class: 'btn', text: '만들고 담기', onclick: make });
      const newBox = h('div', { class: 'ph-new', hidden: true }, nameEl, makeBtn);
      const sheet = h('div', { class: 'sheet ph-sheet', role: 'dialog', 'aria-modal': 'true', 'aria-label': '사진첩에 담기' },
        h('h3', { text: '사진첩에 담기' }),
        h('p', { text: label + ' · 파일은 그대로 두고 사진첩에 이름표만 붙여요' }),
        h('button', { class: 'ph-row new', onclick: () => { newBox.hidden = false; nameEl.focus(); } },
          h('span', { class: 'ph-rthumb', html: ICON.plus }), h('div', { class: 'dtx' }, h('b', { text: '새 사진첩 만들기' }))),
        newBox, listEl3,
        h('button', { class: 'btn ghost wide', text: '닫기', onclick: () => close() }));
      const scrim = h('div', { class: 'sheet-scrim over', onclick: ev => { if (ev.target === scrim) close(); } }, sheet);
      const close = layer(() => scrim.remove());
      $('#app').append(scrim);
      let busyAdd = false;

      async function done(r, name) {
        delete lists.album; albums = null;
        close();
        if (r.done === 0 && r.count) toast('이미 「' + name + '」에 들어 있어요');
        else toast('「' + name + '」에 ' + (r.done != null ? r.done : r.count) + '장을 담았어요');
        endSel();
        if (filt && filt.query.album) reload();
      }

      async function make() {
        const name = nameEl.value.trim();
        if (!name) { nameEl.focus(); return; }
        if (busyAdd) return;
        busyAdd = true; makeBtn.disabled = true;
        try { done(await api.post('photos/album', { op: 'create', name, ids }), name); }
        catch (e) { busyAdd = false; makeBtn.disabled = false; show(e); }
      }

      async function add(a) {
        if (busyAdd) return;
        busyAdd = true;
        try { done(await api.post('photos/album', { op: 'add', album: a.id, ids }), a.name); }
        catch (e) { busyAdd = false; show(e); }
      }

      try {
        if (!albums) albums = (await api.get('photos/albums')).items || [];
        listEl3.append(albums.map(a => h('button', { class: 'ph-row', onclick: () => add(a) },
          a.thumb ? h('img', { class: 'ph-rthumb', src: a.thumb, alt: '', loading: 'lazy' }) : h('span', { class: 'ph-rthumb', html: ICON.photo }),
          h('div', { class: 'dtx' }, h('b', { text: a.name }), h('small', { text: a.count + '장' })))));
      } catch (e) { listEl3.append(h('div', { class: 'doc-empty', text: (e && e.message) || '사진첩 목록을 불러오지 못했어요' })); }
    }

    // ── 불러오기 ──
    async function load() {
      if (loading || !more && page) return;
      const my = gen;
      loading = true; failed = '';
      if (!page) draw(); else drawTail();
      try {
        const o = { page: page + 1, n: 60 };
        if (tab === 'image' || tab === 'video') o.type = tab;
        const tw = PH_TYPEWORD[q.toLowerCase()];
        if (tw) o.type = tw;                       // 「동영상」을 치면 동영상만 (구글 포토처럼)
        else if (q) o.q = q;
        if (filt) Object.assign(o, filt.query);
        if (before && !q && !filt) o.before = before;
        const r = await api.get('photos', o);
        if (!alive || my !== gen) return;
        if (r.stats) stats = r.stats;
        const first = !page;
        page++;
        const got = pushData(g0, r.items || []);
        more = !!r.more;
        loading = false;
        if (first) draw(); else { addItems(got, g0); drawTail(); }
      } catch (e) {
        if (!alive || my !== gen) return;
        failed = (e && e.message) || '불러오지 못했어요';
        loading = false;
        if (e && e.status === 401) show(e);
        if (!page) draw(); else { drawTail(); toast(failed); }
      }
      if (alive && my === gen) requestAnimationFrame(fill);
    }

    function reload() {
      gen++;
      page = 0; more = false; loading = false; failed = '';
      g0 = mkGroup(0, '', 0, bodyEl); items = g0.items;
      if (hasMon()) { buildTL(); return; }
      tlOff();
      load();
    }

    /** 아래로 내려 끝이 가까우면 다음 쪽 — 화면이 덜 찼을 때도 (시간표 방식은 달마다 따로 채우므로 해당 없음) */
    function fill() {
      if (!alive || tl || loading || !more || isList()) return;
      if (content.scrollHeight - content.scrollTop - content.clientHeight < 900) load();
    }

    // ── 시간표 방식 ──
    // 전체 탭(검색·분류 없음)은 연월마다 「칸」을 먼저 모두 깔고(높이는 장수로 어림), 화면에 가까운 달부터 채운다.
    // 막대로 멀리 건너뛰어도 지금까지 불러온 달은 그대로 남고, 위로도 아래로도 이어서 스크롤된다. 한가하면 가까운 달부터 미리 불러 둔다.
    // 채우면서 높이가 어림과 달라져도 보던 자리가 밀리지 않게, 화면 위쪽 칸이 바뀔 땐 스크롤 위치를 같이 보정한다(손가락이 닿았거나 스크롤 중이면 멎은 뒤에).
    const DOM_CAP = 6000, PRE_CAP = 3500;

    function tlOff() {
      if (io) { io.disconnect(); io = null; }
      want.clear(); later.length = 0;
      clearTimeout(bgTimer); bgTimer = 0; clearTimeout(idleTimer); idleTimer = 0;
      tl = false; groups = []; gByM.clear(); domTiles = 0;
    }

    function buildTL() {
      tlOff();
      bodyEl.textContent = '';
      tiles.clear(); dayHdr.clear(); pos.clear(); dayGrids = []; anchor = -1; sentinel = null;
      tl = true;
      drawChips(); drawFilt(); drawStat();
      if (!perItem) perItem = 16638 / Math.max(280, content.clientWidth || 422) + 5;
      io = new IntersectionObserver(es => {
        es.forEach(e => { const g = e.target._g; if (!g) return; if (e.isIntersecting) want.add(g); else want.delete(g); });
        pump();
      }, { root: content, rootMargin: '1400px 0px 1400px 0px' });
      months.forEach((mm, i) => {
        let g = store.get(mm.m);
        if (!g) { g = mkGroup(i, mm.m, mm.n, null); store.set(mm.m, g); }
        g.n = mm.n; g.dom = false; g.rendered = 0; g.lastDay = g.lastGrid = g.lastCity = null;
        g.el = h('section', { class: 'ph-mon' });
        g.el._g = g;
        waitBox(g);
        groups.push(g); gByM.set(g.m, g);
        bodyEl.append(g.el);
        io.observe(g.el);
      });
      content.scrollTop = 0;
      if (!stats) api.get('photos', { page: 1, n: 1 }).then(r => { if (alive && r.stats) { stats = r.stats; drawStat(); } }).catch(() => {});
    }

    /** 아직 안 채운 달의 칸 — 연월 이름과 장수, 높이는 어림(한 번 그려 본 달은 그때 높이 그대로) */
    function waitBox(g) {
      g.el.textContent = '';
      g.el.classList.add('wait');
      g.el.style.height = (g.h || Math.round(30 + g.n * perItem)) + 'px';
      g.el.append(h('div', { class: 'ph-mon-lab' }, h('b', { text: phMonthLabel(g.m) }), h('small', { text: g.n.toLocaleString('ko-KR') + '장' })));
    }

    /** 칸 y(화면 좌표) 에 걸친 칸의 번호 */
    function groupAt(y) {
      let lo = 0, hi = groups.length - 1;
      while (lo < hi) {
        const mid = (lo + hi + 1) >> 1;
        if (groups[mid].el.getBoundingClientRect().top <= y) lo = mid; else hi = mid - 1;
      }
      return lo;
    }

    const focusIdx = () => groups.length ? groupAt(content.getBoundingClientRect().top + content.clientHeight * 0.4) : 0;
    const busyScroll = () => Date.now() - scrollAt < (touching ? 2500 : 160);     // 손가락이 닿은 채 오래 가만히 있으면(또는 터치 끝 신호를 놓쳤으면) 멎은 것으로 본다
    const needs = g => (g.st === 'idle' || g.st === 'partial') ? Date.now() - g.failAt > 4000 : g.st === 'ready' && !g.dom;

    /** 해야 할 일을 가까운 달부터 — 화면 근처(want) 먼저, 그 뒤 한가할 때 미리 */
    function pump() {
      if (!tl || !alive) return;
      const f = focusIdx();
      const near = [...want].filter(needs).sort((a, b) => Math.abs(a.i - f) - Math.abs(b.i - f) || b.i - a.i);
      for (const g of near) {
        if (g.st === 'ready') { paintGroup(g); continue; }            // 자료는 있고 그림만 지워졌던 달 — 바로 다시 그린다
        if (inflight >= 3) break;
        run(g);
      }
      if (!bgTimer && inflight < 2) bgTimer = setTimeout(() => { bgTimer = 0; prefetch(); }, 220);
    }

    function prefetch() {
      if (!tl || !alive || inflight >= 2 || domTiles > PRE_CAP) return;
      const f = focusIdx();
      let best = null, bd = 1e9;
      for (const g of groups) {
        if (!needs(g) || g.st === 'ready') continue;
        const d = Math.abs(g.i - f) - (g.i > f ? 0.5 : 0);
        if (d < bd) { bd = d; best = g; }
      }
      if (best && bd <= 36) run(best);
    }

    function run(g) {
      if (g.p) return g.p;
      g.st = 'loading'; inflight++;
      const my = gen;
      g.p = (async () => {
        try {
          for (;;) {
            const r = await api.get('photos', { q: g.m, page: g.page + 1, n: 100 });
            if (!alive || my !== gen) { g.st = g.page ? 'partial' : 'idle'; return; }
            const got = r.items || [];
            g.page++;
            pushData(g, got);
            g.more = !!r.more && got.length > 0;
            paintGroup(g);
            if (!g.more) { g.st = 'ready'; break; }
            if (!g.full && !want.has(g) && Math.abs(g.i - focusIdx()) > 1) { g.st = 'partial'; break; }   // 멀어졌으면 잠깐 멈추고(다음에 이어서)
          }
          paintGroup(g);
        } catch (e) {
          g.st = g.page ? 'partial' : 'idle'; g.failAt = Date.now();
          if (e && e.status === 401) show(e);
        } finally {
          g.p = null; inflight--;
          if (alive) pump();
        }
      })();
      return g.p;
    }

    /** 달 하나를 끝까지(고르기·범위용) — 이미 진행 중이면 그 끝을 기다린다 */
    async function loadAll(g) {
      g.full = true;
      for (let k = 0; k < 400 && alive && tl && g.st !== 'ready'; k++) {
        if (g.p) await g.p; else await run(g);
        if (g.st !== 'ready' && g.failAt && Date.now() - g.failAt < 4000) await new Promise(r => setTimeout(r, 600));
      }
    }

    function paintGroup(g) { resize(g, () => syncDom(g)); }

    /** 자료와 그림을 맞춘다 — 아직 안 그린 사진만 덧붙이고, 칸 높이를 어림에서 진짜로 */
    function syncDom(g) {
      if (!tl || !g.el || !g.el.isConnected) return;
      if (!g.dom) {
        if (!g.items.length && g.st !== 'ready') return;
        g.el.textContent = ''; g.el.classList.remove('wait'); g.el.style.height = '';
        g.dom = true; g.rendered = 0; g.lastDay = g.lastGrid = g.lastCity = null;
      }
      const part = g.items.slice(g.rendered);
      if (part.length) { g.rendered = g.items.length; domTiles += part.length; addItems(part, g); }
      if (g.st === 'ready' && !g.items.length) g.el.style.height = '0px';
      if (drag) applyRange(drag.a, drag.cur, drag.mode, drag.base, drag.lo, drag.hi);   // 끌어서 고르는 중에 새로 그려진 달까지 범위에
      if (g.st === 'ready' && g.dom && g.items.length >= 12) learn(g);
      if (domTiles > DOM_CAP) evict();
    }

    /** 칸의 진짜 높이로 장수당 높이를 다시 어림 — 아직 안 채운 아래쪽 칸들에만 반영(위쪽은 보정 없이는 못 바꾼다) */
    function learn(g) {
      const hh = g.el.getBoundingClientRect().height;
      if (!(hh > 0) || g.learned) return;
      g.learned = true;
      perItem = Math.min(90, Math.max(18, perItem * 0.6 + (hh / g.items.length) * 0.4));
      const f = focusIdx();
      groups.forEach(x => { if (x.i > f && !x.dom && !x.h && x.st !== 'loading') x.el.style.height = Math.round(30 + x.n * perItem) + 'px'; });
    }

    /** 칸이 바뀌어 높이가 달라질 때 — 화면 위쪽 칸이면 보던 자리가 밀리지 않게 스크롤을 같은 만큼 옮긴다.
     *  스크롤 중(관성 포함)에 스크롤 위치를 건드리면 멈춰 버리므로, 위쪽 칸의 변경은 멎은 뒤로 미룬다. */
    function resize(g, fn) {
      const top = content.getBoundingClientRect().top, r0 = g.el.getBoundingClientRect(), above = r0.bottom <= top + 1;
      if (above && busyScroll()) {
        later.push([g, fn]);
        if (!idleTimer) idleTimer = setTimeout(flushLater, 180);
        return;
      }
      fn();
      if (above) { const d = g.el.getBoundingClientRect().height - r0.height; if (d) content.scrollTop += d; }
    }

    function flushLater() {
      idleTimer = 0;
      if (!tl) { later.length = 0; return; }
      if (busyScroll()) { idleTimer = setTimeout(flushLater, 140); return; }
      later.splice(0).forEach(([g, fn]) => resize(g, fn));
    }

    /** 그려 둔 사진이 너무 많으면 화면에서 먼 달부터 그림만 걷어낸다(자료는 남아서 다시 오면 바로 그려진다) — 높이는 그대로 붙잡아 둔다 */
    function evict() {
      const f = focusIdx();
      const far = groups.filter(g => g.dom && Math.abs(g.i - f) > 3).sort((a, b) => Math.abs(b.i - f) - Math.abs(a.i - f));
      for (const g of far) {
        if (domTiles <= DOM_CAP * 0.75) break;
        g.h = g.el.getBoundingClientRect().height;
        g.items.forEach(it => tiles.delete(it.id));
        g.days.forEach((_ids, day) => dayHdr.delete(day));
        domTiles -= g.rendered; g.rendered = 0; g.dom = false;
        g.lastDay = g.lastGrid = g.lastCity = null;
        waitBox(g);
      }
    }
    // ── 스크롤 연월 표시 + 빠른 스크롤 막대 (2026-11-20 대표님 「스크롤할 때 연월이 보여서 어느 연도 몇 월인지 찾을 수 있게」) ──
    // 스크롤하는 동안 지금 보이는 날의 「2019년 3월」이 오른쪽에 뜨고, 막대(손잡이)를 잡아 끌면 그 달로 건너뛴다.
    // 달별 장수는 서버 photos/timeline (Immich 시간표) — 그 장수로 손잡이 위치와 건너뛸 달을 정한다.
    function clearBefore() {
      if (!before) return;
      before = '';
      latestEl.hidden = true;
    }

    async function loadMonths() {
      try {
        const r = await api.get('photos/timeline');
        if (!alive) return;
        months = (r.months || []).filter(m => /^\d{4}-\d\d$/.test(m.m));
        monthsTotal = months.reduce((s, m) => s + m.n, 0);
        monthPos = {};
        let cum = 0;
        months.forEach(m => { monthPos[m.m] = { cum, n: m.n }; cum += m.n; });
      } catch (e) { months = monthPos = null; }
    }

    /** 지금 날짜 머리글 밑에 보이는 날 — 격자(sticky 가 아님)의 위치를 이진 탐색 */
    function topDay() {
      if (!dayGrids.length) return '';
      const edge = content.getBoundingClientRect().top + 44;
      let lo = 0, hi = dayGrids.length - 1;
      while (lo < hi) {
        const mid = (lo + hi + 1) >> 1;
        if (dayGrids[mid].el.getBoundingClientRect().top <= edge) lo = mid; else hi = mid - 1;
      }
      return dayGrids[lo].day;
    }

    /** 달 안의 위치까지 따져 0~1 (맨 위 = 0) */
    function fracOf(day) {
      const m = (day || '').slice(0, 7), p = monthPos && monthPos[m];
      if (!p || !monthsTotal) return null;
      const d = +day.slice(8, 10) || 1;
      return Math.min(1, (p.cum + p.n * (31 - d) / 31) / monthsTotal);
    }

    function monthAt(frac) {
      const target = Math.min(monthsTotal - 1, Math.max(0, frac * monthsTotal));
      return months.find(m => target < monthPos[m.m].cum + m.n) || months[months.length - 1];
    }

    function trackBox() {
      const c = content.getBoundingClientRect(), p = pg.getBoundingClientRect();
      trackEl.style.top = (c.top - p.top + 8) + 'px';
      trackEl.style.height = Math.max(80, c.height - 16) + 'px';
      return trackEl.getBoundingClientRect();
    }

    function placeKnob(frac) {
      const box = trackEl.getBoundingClientRect(), room = Math.max(1, box.height - 44);
      const y = Math.min(room, Math.max(0, frac * room));
      knobEl.style.transform = 'translateY(' + y + 'px)';
      whenEl.style.top = (box.top - pg.getBoundingClientRect().top + y + 4) + 'px';
    }

    function showWhen(text) {
      whenEl.textContent = text;
      whenEl.hidden = !text;
    }

    function keepWhen() {
      clearTimeout(whenTimer);
      whenTimer = setTimeout(() => { if (scrubDrag) return; whenEl.hidden = true; trackEl.classList.remove('on'); }, 1400);
    }

    /** 지금 화면 맨 위에 걸린 때 — 보여 줄 연월과 손잡이 위치(0~1). 시간표 방식은 칸(달)과 그 안에서 얼마나 내려왔는지로 */
    function whereNow() {
      const edge = content.getBoundingClientRect().top + 44;
      if (tl) {
        const g = groups[groupAt(edge)];
        if (!g) return null;
        const r = g.el.getBoundingClientRect(), p = monthPos && monthPos[g.m];
        const prog = r.height > 0 ? Math.min(1, Math.max(0, (edge - r.top) / r.height)) : 0;
        return { label: phMonthLabel(g.m), frac: p && monthsTotal ? Math.min(1, (p.cum + p.n * prog) / monthsTotal) : null };
      }
      const day = topDay();
      return { label: phYM(day), frac: fracOf(day) };
    }

    function updateWhen() {
      whenRaf = 0;
      if (!alive || isList() || (tl ? !groups.length : !dayGrids.length) || content.scrollTop < 4 && !before) { if (!scrubDrag) { whenEl.hidden = true; trackEl.classList.remove('on'); } return; }
      const w = whereNow();
      if (!w) return;
      showWhen(w.label);
      if (scrubOK()) {
        trackEl.hidden = false;
        trackBox();
        trackEl.classList.add('on');
        if (w.frac != null && !scrubDrag) placeKnob(w.frac);
      } else { whenEl.style.top = ''; trackEl.hidden = true; }
      keepWhen();
    }

    function onScroll() {
      scrollAt = Date.now();
      fill();
      if (!whenRaf) whenRaf = requestAnimationFrame(updateWhen);
    }
    content.addEventListener('scroll', onScroll, { passive: true });
    // 손가락이 닿은 동안·스크롤 관성 중에는 스크롤 위치를 보정하지 않는다(멈춰 버리므로) — 멎은 뒤에 한꺼번에
    content.addEventListener('touchstart', () => { touching = true; scrollAt = Date.now(); }, { passive: true });
    ['touchend', 'touchcancel'].forEach(n => content.addEventListener(n, () => { touching = false; scrollAt = Date.now(); }, { passive: true }));
    // 꾹 누른 채 끌 때는 화면이 같이 밀리지 않게 막고, 손가락 밑의 사진까지 범위로 고른다
    content.addEventListener('touchmove', ev => { if (drag && ev.cancelable) ev.preventDefault(); }, { passive: false });
    content.addEventListener('pointermove', ev => { if (drag) dragMove(ev.clientX, ev.clientY); });
    ['pointerup', 'pointercancel'].forEach(n => { content.addEventListener(n, endDrag); window.addEventListener(n, endDrag); });

    // 손잡이 끌기 — 손을 떼면 그 달로 건너뛴다(시간표 방식은 그 달 칸이 맨 위에 오고, 지금까지 불러온 달은 그대로 남아 위아래로 이어서 스크롤된다)
    function dragTo(ev) {
      if (!months) return;
      const box = trackEl.getBoundingClientRect(), room = Math.max(1, box.height - 44);
      const frac = Math.min(1, Math.max(0, (ev.clientY - box.top - 22) / room));
      const m = monthAt(frac);
      if (m.m !== shownMonth) { shownMonth = m.m; haptic('tap'); }
      placeKnob(frac);
      showWhen(phMonthLabel(m.m));
      return m;
    }

    let dragMonth = null;
    trackEl.addEventListener('pointerdown', ev => {
      if (!months || ev.button > 0) return;
      scrubDrag = true; shownMonth = ''; clearTimeout(whenTimer);
      trackEl.classList.add('drag');
      try { trackEl.setPointerCapture(ev.pointerId); } catch (e) { /* 캡처 못 해도 끌린다 */ }
      dragMonth = dragTo(ev);
      ev.preventDefault();
    });
    trackEl.addEventListener('pointermove', ev => { if (scrubDrag) dragMonth = dragTo(ev); });
    const endDragScrub = () => {
      if (!scrubDrag) return;
      scrubDrag = false;
      trackEl.classList.remove('drag');
      const m = dragMonth; dragMonth = null;
      if (m) jumpMonth(m.m);
      keepWhen();
    };
    ['pointerup', 'pointercancel'].forEach(n => trackEl.addEventListener(n, endDragScrub));

    function jumpMonth(m) {
      if (tl) {
        const g = gByM.get(m);
        if (!g) return;
        if (g.i === 0) content.scrollTop = 0;
        else content.scrollTop += g.el.getBoundingClientRect().top - content.getBoundingClientRect().top;
        pump();
        return;
      }
      endSel();
      if (months && months[0] && m === months[0].m) before = '';
      else before = phNextMonth(m);
      latestEl.hidden = !before;
      reload();
      content.scrollTop = 0;
    }

    const pg = pushPage('사진', content, () => { alive = false; endSel(); clearTimeout(whenTimer); ['pointerup', 'pointercancel'].forEach(n => window.removeEventListener(n, endDrag)); }, { close: true });
    pg.append(selBar, whenEl, trackEl, latestEl);
    drawChips(); drawStat();
    bodyEl.append(h('div', { class: 'empty' }, h('i', { class: 'spinner', style: 'display:inline-block' })));
    loadMonths().then(() => { if (alive) reload(); });      // 달별 장수를 먼저 받아 칸을 깔고(실패하면 예전처럼 목록으로)
  }

  // ── 폰 사진 자동 백업 (2026-11-20 대표님 「핸드폰 사진첩에 접속해서 백업되지 않은 사진·영상은 여기에 백업, 구글처럼」) ──
  // 켠 뒤에 이 폰에 새로 생긴 사진·영상만 PC 로 올린다 — 지금 폰에 있는 옛 사진은 건드리지 않는다. 올라간 것은 오른쪽 아래에 ✓.
  // 폰 사진첩을 읽고 올리는 일은 앱(DeskNative)이 한다 — apps/hyodingsseu-ios/PHOTO_BACKUP.md 에 약속(이름·모양)이 있다:
  //   backupStatus() → {enabled, since, auth:'granted'|'limited'|'denied'|'undetermined', done, pending, uploading:{lid,name,sent,total}|null, wifiOnly:false, error}
  //   backupEnable({base, token}) · backupDisable() · backupRun() · backupConfigure({base, token}) → 모두 위 상태를 돌려준다
  //   LTE·Wi-Fi 를 가리지 않고 올린다(2026-11-21 대표님 「요금제 무제한이라 상관없이 다 백업」) — wifiOnly 는 늘 false.
  //   backupList({before, limit}) → {items:[{lid, kind:'image'|'video', ts(초), dur(초), name, up}], more}  (새것부터, before = 이 시각(초) 전)
  //   backupThumb({lid, size}) → {data:'data:image/jpeg;base64,…'}
  //   정리 (2026-11-21 대표님 「동영상 정리 / 전체 정리 → 백업된 것은 아이폰 사진첩에서 삭제」):
  //   backupCleanupPlan() → {video:{n, bytes}, image:{n, bytes}}   백업이 끝났고 폰 사진첩에 아직 남아 있는 것(앱이 기억하는 기준)
  //   backupCleanup({scope:'video'|'all'}) → {video, image, pending, skipped, cancelled}   지운 동영상·사진 수 / 서버 확인이 덜 끝나 남긴 수 / 서버에 없어 남긴 수 / 사용자가 iOS 확인 창에서 취소
  //     앱은 지우기 직전에 서버(photos/up/verify)에 다시 묻고 「서버에 있다」고 한 것만 지운다. 지운 것은 iOS 「최근 삭제된 항목」에 30일 남는다.
  //   사건 'backup' (addListener) — 데이터는 backupStatus() 와 같은 모양
  const BK_ON = !!(Native && typeof Native.backupStatus === 'function');

  /** 서버 주소·열쇠를 앱에 알려 준다 — 앱이 직접 올리기 때문. 짝을 맺거나 끊을 때, 화면이 켜질 때마다. */
  function syncBackupAuth() {
    if (!BK_ON || typeof Native.backupConfigure !== 'function') return;
    Native.backupConfigure({ base: api.base, token: api.token || '', wifiOnly: false }).catch(() => {});
  }

  /** 폰 사진첩 정리 — 백업이 끝난 것만 아이폰 사진첩에서 지운다. 지우기 직전에 앱이 서버에 다시 확인하고, iOS 가 확인 창을 한 번 더 띄운다. */
  function openCleanup() {
    let plan = null, busy = false, failed = '';
    const bodyEl = h('div', { class: 'cl-body' });
    const scrim = h('div', { class: 'sheet-scrim over', onclick: ev => { if (ev.target === scrim && !busy) close(); } },
      h('div', { class: 'sheet cl-sheet', role: 'dialog', 'aria-modal': 'true', 'aria-label': '폰 사진첩 정리' }, bodyEl));
    const close = layer(() => scrim.remove());
    $('#app').append(scrim);

    const size = b => (b >= 1024 ** 3 ? (b / 1024 ** 3).toFixed(1) + 'GB' : Math.max(1, Math.round((b || 0) / 1024 ** 2)) + 'MB');
    const part = (kind, p, unit) => (kind + ' ' + (p.n || 0).toLocaleString('ko-KR') + unit + (p.n ? ' (' + size(p.bytes) + ')' : ''));

    function draw() {
      bodyEl.textContent = '';
      bodyEl.append(h('h3', { text: '폰 사진첩 정리' }),
        h('p', { text: 'PC 로 백업이 끝난 사진·영상만 아이폰 사진첩에서 지워요. 지우기 전에 PC 에 잘 있는지 한 번 더 확인해요. 지운 것은 아이폰 「최근 삭제된 항목」에 30일 동안 남아요.' }));
      if (failed) { bodyEl.append(h('p', { class: 'bk-err', text: failed })); }
      else if (!plan) { bodyEl.append(h('div', { class: 'bk-note' }, h('i', { class: 'spinner', style: 'display:inline-block' }))); }
      else {
        const v = plan.video || { n: 0 }, im = plan.image || { n: 0 }, all = (v.n || 0) + (im.n || 0);
        bodyEl.append(h('p', { class: 'cl-count', text: '백업 끝난 것 · ' + part('동영상', v, '개') + ' · ' + part('사진', im, '장') }));
        bodyEl.append(h('button', { class: 'btn wide cl-video', disabled: busy || !v.n, text: busy ? '정리하는 중…' : '동영상 정리' + (v.n ? ' (' + v.n.toLocaleString('ko-KR') + '개)' : ''), onclick: () => run('video') }));
        bodyEl.append(h('button', { class: 'btn danger wide cl-all', disabled: busy || !all, text: '전체 정리' + (all ? ' (사진 ' + (im.n || 0).toLocaleString('ko-KR') + '장 · 동영상 ' + (v.n || 0).toLocaleString('ko-KR') + '개)' : ''), onclick: () => run('all') }));
        if (!all) bodyEl.append(h('p', { class: 'note-hint', text: '아직 백업이 끝난 사진·영상이 없어요 — 자동 백업을 켜고 올라가면 여기서 정리할 수 있어요' }));
      }
      bodyEl.append(h('button', { class: 'btn ghost wide cl-close', disabled: busy, text: '닫기', onclick: () => close() }));
    }

    function doneText(r, scope) {
      const v = (r && r.video) || 0, im = (r && r.image) || 0, pend = (r && r.pending) || 0;
      const what = scope === 'video' ? '동영상 ' + v + '개' : '사진 ' + im + '장 · 동영상 ' + v + '개';
      const left = pend ? ' — 서버 확인이 덜 끝난 ' + pend + '개는 남겨 뒀어요' : '';
      if (v + im) return what + '를 폰 사진첩에서 지웠어요' + left;
      if (r && r.cancelled) return '취소했어요 — 아무것도 지우지 않았어요';
      if (pend) return '서버 확인이 아직 끝나지 않아 남겨 뒀어요 — 조금 뒤에 다시 눌러 주세요';
      return '지울 것이 없어요';
    }

    async function run(scope) {
      busy = true; draw();
      try {
        const r = await Native.backupCleanup({ scope });
        close();
        toast(doneText(r, scope));
      } catch (e) {
        busy = false; draw();
        toast((e && e.message) || '정리하지 못했어요');
      }
    }

    draw();
    Native.backupCleanupPlan().then(p => { plan = p || { video: { n: 0 }, image: { n: 0 } }; draw(); })
      .catch(e => { failed = (e && e.message) || '백업 현황을 읽지 못했어요'; draw(); });
  }

  function openPhoneBackup() {
    let st = null, items = [], more = false, loading = false, alive = true, failed = '', refresh = 0, lastSig = '';
    const thumbs = new Map();                          // 사진 번호 → 작은 그림(data 주소) — 다시 그려도 앱에 또 묻지 않게
    let thumbBusy = 0;
    const thumbQueue = [];
    const cardEl = h('div', { class: 'bk-card' });
    const bodyEl = h('div', { class: 'ph-body' });
    const content = h('div', { class: 'content doc-content ph-content bk-content' }, cardEl, bodyEl);
    const seen = 'IntersectionObserver' in window ? new IntersectionObserver(es => es.forEach(e => {
      if (e.isIntersecting) { seen.unobserve(e.target); wantThumb(e.target); }
    }), { root: content, rootMargin: '400px' }) : null;

    function wantThumb(img) {
      const lid = img.dataset.lid;
      if (thumbs.has(lid)) { img.src = thumbs.get(lid); return; }
      thumbQueue.push(img);
      pumpThumbs();
    }

    function pumpThumbs() {
      while (thumbBusy < 4 && thumbQueue.length) {
        const img = thumbQueue.shift(), lid = img.dataset.lid;
        if (!img.isConnected) continue;
        thumbBusy++;
        Native.backupThumb({ lid, size: 240 }).then(r => { if (r && r.data) { thumbs.set(lid, r.data); img.src = r.data; } })
          .catch(() => img.classList.add('bad')).finally(() => { thumbBusy--; pumpThumbs(); });
      }
    }

    const pct = u => (u && u.total ? Math.min(100, Math.round(u.sent / u.total * 100)) : 0);

    function drawCard() {
      cardEl.textContent = '';
      if (!st) { cardEl.append(h('div', { class: 'bk-note' }, h('i', { class: 'spinner', style: 'display:inline-block' }))); return; }
      const on = !!st.enabled;
      const since = st.since ? new Date(st.since * 1000) : null;
      const sinceText = since ? (since.getMonth() + 1) + '월 ' + since.getDate() + '일 ' + String(since.getHours()).padStart(2, '0') + ':' + String(since.getMinutes()).padStart(2, '0') : '';
      cardEl.append(h('div', { class: 'bk-head' },
        h('span', { class: 'bk-ic' + (on ? ' on' : ''), html: ICON.cloud }),
        h('div', { class: 'bk-tx' }, h('b', { text: on ? '자동 백업 켜짐' : '자동 백업 꺼짐' }),
          h('small', { text: on ? sinceText + ' 이후의 새 사진·영상을 LTE·Wi-Fi 구분 없이 PC 로 올려요' : '켜면 이 폰에 새로 생기는 사진·영상이 PC 로 올라가요' })),
        h('button', { class: 'bk-sw' + (on ? ' on' : ''), role: 'switch', 'aria-checked': on ? 'true' : 'false', 'aria-label': '자동 백업', onclick: toggleBackup }, h('i'))));
      if (!on) {
        cardEl.append(h('p', { class: 'bk-help', text: '켠 다음부터 찍거나 받은 사진·영상만 올려요(LTE·Wi-Fi 모두). 지금 폰에 들어 있는 옛 사진은 올리지 않아요.' }));
      } else {
        cardEl.append(h('div', { class: 'bk-stat' },
          h('span', { text: '올림 ' + (st.done || 0).toLocaleString('ko-KR') + '장' }),
          h('span', { class: st.pending ? 'wait' : '', text: st.pending ? '기다리는 중 ' + st.pending.toLocaleString('ko-KR') + '장' : '모두 올렸어요' })));
        if (st.uploading) {
          cardEl.append(h('div', { class: 'bk-prog' }, h('div', { class: 'bar' }, h('i', { style: 'width:' + pct(st.uploading) + '%' })),
            h('small', { text: '올리는 중 · ' + (st.uploading.name || '') + ' ' + pct(st.uploading) + '%' })));
        }
        if (st.pending && !st.uploading) cardEl.append(h('button', { class: 'btn wide bk-run', text: '지금 올리기', onclick: runNow }));
      }
      if (st.auth === 'denied') cardEl.append(h('p', { class: 'bk-err', text: '사진 접근이 막혀 있어요 — 폰 설정 › 효딩쓰 › 사진 에서 「모든 사진」으로 바꿔 주세요.' }));
      else if (on && st.auth === 'limited') cardEl.append(h('p', { class: 'bk-err', text: '일부 사진만 허용돼 있어요 — 새 사진이 빠질 수 있어요. 설정 › 효딩쓰 › 사진 에서 「모든 사진」으로 바꿔 주세요.' }));
      if (st.error) cardEl.append(h('p', { class: 'bk-err', text: st.error }));
    }

    const sigOf = s => (s.done || 0) + '/' + (s.pending || 0) + '/' + (s.enabled ? 1 : 0) + '/' + (s.uploading ? s.uploading.lid : '');

    function setState(s, first) {
      if (!alive || !s) return;
      st = s;
      drawCard();
      const sig = sigOf(s);
      if (sig === lastSig) return;
      lastSig = sig;
      if (!first) { clearTimeout(refresh); refresh = setTimeout(() => load(true), 800); }   // 올림·대기가 바뀌면 격자의 ✓ 를 새로 (처음 한 번은 아래에서 바로 불러온다)
    }

    async function toggleBackup() {
      if (!st) return;
      try {
        if (st.enabled) { confirmOff(); return; }
        const ns = await Native.backupEnable({ base: api.base, token: api.token || '', wifiOnly: false });
        setState(ns);
        if (ns && ns.enabled) toast('켠 뒤에 생기는 사진·영상부터 올려요');
        else if (ns && ns.error) toast(ns.error);
      } catch (e) { toast((e && e.message) || '켜지 못했어요'); }
    }

    function confirmOff() {
      const scrim = h('div', { class: 'sheet-scrim over', onclick: ev => { if (ev.target === scrim) close(); } },
        h('div', { class: 'sheet', role: 'dialog', 'aria-modal': 'true' },
          h('h3', { text: '자동 백업을 끌까요?' }),
          h('p', { text: '끄면 새 사진·영상을 더는 올리지 않아요. 이미 올린 사진은 그대로 남아요.' }),
          h('button', { class: 'btn danger wide', text: '끄기', onclick: async () => { close(); try { setState(await Native.backupDisable()); } catch (e) { toast('끄지 못했어요'); } } }),
          h('button', { class: 'btn ghost wide', text: '그대로 두기', onclick: () => close() })));
      const close = layer(() => scrim.remove());
      $('#app').append(scrim);
    }

    async function runNow() {
      try { setState(await Native.backupRun()); } catch (e) { toast((e && e.message) || '지금은 올릴 수 없어요'); }
    }

    // ── 폰 사진 격자 (새것부터, 올라간 것엔 ✓) ──
    const dayOf = ts => { const d = new Date(ts * 1000); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); };

    function tile(it) {
      const sending = !!(st && st.uploading && st.uploading.lid === it.lid);
      const img = h('img', { alt: '', draggable: 'false', decoding: 'async', 'data-lid': it.lid });
      const t = h('button', { class: 'ph-tile bk-tile' + (it.kind === 'video' ? ' vid' : '') + (sending ? ' sending' : ''),
        'aria-label': (it.kind === 'video' ? '영상' : '사진') + (it.up ? ' · 올림' : ' · 아직 안 올림'),
        onclick: () => toast(it.up ? '이미 PC 에 올라간 ' + (it.kind === 'video' ? '영상' : '사진') + '이에요' : st && st.enabled ? '곧 올라가요 — 기다리는 중이에요' : '자동 백업이 꺼져 있어요') },
        img,
        it.kind === 'video' ? h('span', { class: 'ph-dur' }, svg('play'), it.dur ? phDur(it.dur) : null) : null,
        it.up ? h('span', { class: 'ph-up', html: ICON.check }) : sending ? h('span', { class: 'ph-up wait' }, h('i', { class: 'spinner' })) : null);
      if (thumbs.has(it.lid)) img.src = thumbs.get(it.lid);
      else if (seen) seen.observe(img); else wantThumb(img);
      return t;
    }

    function draw() {
      const top = content.scrollTop;
      bodyEl.textContent = '';
      if (failed && !items.length) { bodyEl.append(h('div', { class: 'doc-empty' }, failed, h('div', null, h('button', { class: 'doc-more', text: '다시 시도', onclick: () => load() })))); return; }
      if (!items.length) {
        bodyEl.append(h('div', { class: 'doc-empty', text: loading ? '' : st && st.enabled ? '켠 뒤에 찍은 사진·영상이 여기에 나타나요' : '자동 백업을 켜면, 켠 뒤에 생기는 사진·영상이 여기에 나타나요' }));
        if (loading) bodyEl.append(h('div', { class: 'empty' }, h('i', { class: 'spinner', style: 'display:inline-block' })));
        return;
      }
      let day = null, grid = null;
      items.forEach(it => {
        const d = dayOf(it.ts);
        if (d !== day) { day = d; bodyEl.append(h('div', { class: 'ph-day' }, h('b', { text: phDay(d) }))); grid = h('div', { class: 'ph-grid' }); bodyEl.append(grid); }
        grid.append(tile(it));
      });
      if (more) bodyEl.append(h('div', { class: 'ph-tail' }, h('button', { class: 'doc-more', text: '더 보기', onclick: () => loadMore() })));
      content.scrollTop = top;
    }

    async function load(soft) {
      if (loading) return;
      loading = true; failed = '';
      try {
        const r = await Native.backupList({ before: 0, limit: soft ? Math.max(60, items.length) : 60 });
        if (!alive) return;
        items = r.items || []; more = !!r.more;
      } catch (e) { failed = (e && e.message) || '폰 사진을 읽지 못했어요'; }
      loading = false;
      draw();
    }

    async function loadMore() {
      if (loading || !more || !items.length) return;
      loading = true;
      try {
        const r = await Native.backupList({ before: items[items.length - 1].ts, limit: 60 });
        if (!alive) return;
        items = items.concat(r.items || []); more = !!r.more;
      } catch (e) { toast((e && e.message) || '더 불러오지 못했어요'); }
      loading = false;
      draw();
    }

    content.addEventListener('scroll', () => {
      if (more && !loading && content.scrollHeight - content.scrollTop - content.clientHeight < 900) loadMore();
    }, { passive: true });

    let sub = null;
    Native.addListener('backup', setState).then(h2 => { sub = h2; if (!alive && h2) h2.remove(); }).catch(() => {});
    pushPage('폰 사진 백업', content, () => { alive = false; clearTimeout(refresh); if (sub) sub.remove(); }, { close: true });
    drawCard();
    Native.backupStatus().then(s => { setState(s, true); load(); }).catch(() => { st = { enabled: false }; drawCard(); load(); });
  }

  // ── 승인함 (ApprView — 2026-10-12, PC 창 승인함과 같은 틀. 제안을 펼쳐 읽고 그 자리에서 결정) ──
  // 「의견」 칩은 의견 반영 중인 건이 있을 때만 보인다 (2026-10-22 — 의견은 승인도 보류도 거절도 아니라 따로 둔다)
  const APPR_ST = [['pending', '대기'], ['revising', '의견'], ['hold', '보류'], ['approved', '승인'], ['rejected', '거절'], ['all', '전체']];
  const APPR_WORD = { pending: '대기', hold: '보류', approved: '승인', rejected: '거절', revised: '수정', revising: '의견 반영 중' };
  const APPR_DONE = { approved: '승인했어요 — 할 일 보드에 올리고 효딩쓰가 대화에서 바로 시작합니다',
    hold: '보류해 두었어요 — 「보류」에서 다시 볼 수 있어요', rejected: '거절했어요',
    revised: '대표님 의견대로 고친 제안으로 바뀌었어요 — 새 제안에서 결정해 주세요',
    revising: '의견을 반영하는 중이에요 — 고친 안이 올라오면 거기서 결정해 주세요' };

  function openAppr() {
    let status = 'pending', q = '', items = [], counts = {}, total = 0, open = '', loading = false, loaded = false, alive = true, seqN = 0, searchTimer = 0;
    const drafts = {};                 // 제안별로 적다 만 의견 — 창을 닫았다 열어도 남게

    const totalEl = h('div', { class: 'doc-total' });
    const chipsEl = h('div', { class: 'doc-chips' });
    const searchEl = h('input', {
      type: 'search', placeholder: '제목·내용으로 찾기', 'aria-label': '승인함 검색', inputmode: 'search',
      oninput: () => {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(() => { const v = searchEl.value.trim(); if (v !== q) { q = v; open = ''; load(); } }, 400);
      },
    });
    const listEl2 = h('div', { class: 'doc-list' });
    const content = h('div', { class: 'content doc-content' }, h('div', { class: 'doc-head' }, totalEl, searchEl), chipsEl, listEl2);

    function drawChips() {
      chipsEl.textContent = '';
      APPR_ST.forEach(([k, label]) => {
        const n = k === 'all' ? total : (counts[k] || 0);
        if (k === 'revising' && !n && status !== 'revising') return;
        chipsEl.append(h('button', {
          class: 'dchip' + (status === k ? ' on' : ''), text: label + (loaded && n ? ' ' + n : ''), 'aria-pressed': status === k ? 'true' : 'false',
          onclick: () => { if (status === k) return; status = k; open = ''; load(); },
        }));
      });
    }

    function row(it) {
      const st = it.status || 'pending', isOpen = open === it.id, noun = it.noun || '제안';
      // 메일 초안(kind 'mail', 2026-10-21)은 thumb·status_word·sub·fields·acts·labels·done 을 스스로 준다 — 제안은 없으면 예전 그대로
      const sub = it.sub || (it.horizon ? it.horizon + ' 단위' : '');
      const head = h('button', { class: 'drow2 prow', 'aria-expanded': isOpen ? 'true' : 'false', onclick: () => { open = isOpen ? '' : it.id; draw(); } },
        h('div', { class: 'dthumb ic ph', text: it.thumb || (it.horizon || '').slice(0, 2) || '제안' }),
        h('div', { class: 'dtx' },
          h('b', { text: it.title || '(제목 없음)' }),
          h('small', null, h('span', { class: 'pst', text: it.status_word || APPR_WORD[st] || st }), ' · ' + timeOnly(it.ts) + (sub ? ' · ' + sub : ''))),
        svg('chev'));
      const wrap = h('div', { class: 'pitem ' + st + (isOpen ? ' open' : '') }, head);
      if (!isOpen) return wrap;
      const body = h('div', { class: 'pbody' });
      (it.fields || [['왜 (근거)', it.why], ['무엇을', it.how], ['대표님이 하실 것', it.needs], ['메모', it.note]]).forEach(([t, v]) => {
        if (v) body.append(h('h4', { text: t }), h('p', null, linkified(String(v))));      // 제안 글 속 주소도 눌러 열린다 — 문서 전체 누름 처리기가 openLink 로 연다
      });
      if ((it.images || []).length) {
        body.append(h('div', { class: 'pimgs' }, it.images.map((im, i) => h('button', { class: 'pimg', 'aria-label': im.name, onclick: () => openViewer(it.images, i, it.title) }, h('img', { src: im.url, alt: im.name, loading: 'lazy' })))));
      }
      if ((it.comments || []).length) {
        body.append(h('h4', { text: '대표님 의견' }));
        it.comments.forEach(c => body.append(h('div', { class: 'pcmt' }, h('small', { text: dayLabel(c.ts) + ' ' + timeOnly(c.ts) }), String(c.text || ''))));
      }
      const acts = it.acts || (st === 'pending' ? ['ok', 'note', 'hold', 'no'] : st === 'hold' ? ['ok', 'note', 'no'] : []);
      const lab = Object.assign({ ok: '승인', note: '의견', hold: '보류', no: '거절' }, it.labels);
      if (acts.length) {
        const act = h('div', { class: 'pact' });
        [['ok', 'btn'], ['note', 'btn ghost'], ['hold', 'btn ghost'], ['no', 'btn no']].forEach(([dec, cls]) => {
          if (!acts.includes(dec)) return;
          act.append(h('button', { class: cls, text: lab[dec], onclick: () => (dec === 'note' ? noteSheet(it) : decide(it, dec, act)) }));
        });
        body.append(act);
      } else if (it.done) {
        body.append(h('div', { class: 'pdone', text: it.done + (it.done_ts ? ' · ' + dayLabel(it.done_ts) + ' ' + timeOnly(it.done_ts) : '') }));
      } else if (st === 'revised') {
        body.append(h('div', { class: 'pdone', text: '대표님 의견대로 고친 제안' + (it.revised_to ? ' ' + it.revised_to : '') + ' 로 바뀌었어요 — 새 제안에서 결정해 주세요' }));
      } else {
        body.append(h('div', { class: 'pdone', text: (APPR_WORD[st] || st) + '했습니다' + (it.decided ? ' · ' + dayLabel(it.decided) + ' ' + timeOnly(it.decided) : '')
          + (st === 'approved' ? (it.decided_by === '의견' ? ' — 대표님 의견대로 효딩쓰가 승인 처리, 할 일 보드에 올라 있어요' : ' — 할 일 보드에 올라 있어요') : '') }));
      }
      if (it.replaces) body.append(h('div', { class: 'pdone', text: noun + ' ' + it.replaces + ' 을 대표님 의견대로 고친 ' + noun + '이에요' }));
      body.append(h('div', { class: 'pdone', text: noun + ' 번호 ' + it.id }));
      wrap.append(body);
      return wrap;
    }

    function draw() {
      totalEl.textContent = loaded ? '대기 ' + (counts.pending || 0) + '건' + (counts.revising ? ' · 의견 반영 중 ' + counts.revising + '건' : '') + ' · 전체 ' + total + '건' : '';
      listEl2.textContent = '';
      if (loading && !items.length) { listEl2.append(h('div', { class: 'empty' }, h('i', { class: 'spinner', style: 'display:inline-block' }))); return; }
      if (!items.length) {
        listEl2.append(h('div', { class: 'doc-empty', text: q || status !== 'pending' ? '찾는 제안이 없어요' : '기다리는 제안·메일이 없어요 — 효딩쓰가 승인이 필요한 일(제안·메일 초안)을 올리면 여기 모입니다' }));
        return;
      }
      let lastDay = null;
      items.forEach(it => {
        const day = dayLabel(it.ts);
        if (day !== lastDay) { listEl2.append(h('div', { class: 'doc-day', text: day })); lastDay = day; }
        listEl2.append(row(it));
      });
    }

    // silent — 보고 계신 화면을 건드리지 않고 목록만 조용히 새로 받는다 (보내기 결과·늦은 답 뒤에)
    async function load(silent) {
      const my = ++seqN;
      if (!silent) {
        loading = true;
        drawChips();
        draw();
      }
      try {
        const r = await api.get('proposals', { status, q });
        if (!alive || my !== seqN) return;
        items = (r.items || []).filter(x => !sending.has(x.id));      // 보내는 중인 메일은 서버가 끝내기 전엔 다시 나오지 않는다
        counts = r.counts || {};
        if (counts.pending && sending.size) counts.pending = Math.max(0, counts.pending - sending.size);
        total = typeof r.total === 'number' ? r.total : items.length;
        loaded = true;
        takeAppr(counts.pending || 0);
      } catch (e) {
        if (alive && my === seqN) show(e);
      } finally {
        if (alive && my === seqN) { loading = false; drawChips(); draw(); }
      }
    }

    const sending = new Set();         // 보내는 중인 메일 id

    /** 메일 [보내기] — 누르면 바로 카드를 치우고 「보내기 요청하였습니다」. 메일 서버가 몇 초 걸려도 기다리지 않고, 못 보냈을 때만 승인함에 다시 올린다 (2026-11-02 대표님).
        보는 중이던 다른 카드·스크롤은 건드리지 않는다 — 결과 안내(토스트)만 뜬다. */
    function sendMail(it) {
      sending.add(it.id);
      items = items.filter(x => x.id !== it.id);
      if (counts.pending) counts.pending -= 1;
      takeAppr(counts.pending || 0);
      if (open === it.id) open = '';
      drawChips();
      draw();
      sfx.play('send');
      toast('보내기 요청하였습니다');
      const done = () => { sending.delete(it.id); if (alive) load(true); };
      api.post('proposals/decide', { id: it.id, dec: 'ok', kind: 'mail' }, { timeout: 90000 }).then(r => {
        if (r && r.counts && typeof r.counts.pending === 'number') takeAppr(Math.max(0, r.counts.pending - (sending.size - 1)));
        if (r && r.ok === false) { sfx.play('error'); haptic('alert'); toast('보내지 못했어요 — 승인함에 다시 올렸어요'); done(); return; }
        if (r && r.sending) { toast(r.msg || '계속 보내는 중입니다'); setTimeout(done, 30000); return; }    // 메일 서버가 느리다 — 끝나기를 기다렸다 실제 상태를 본다
        toast((r && r.msg) || '보냈어요');
        done();
      }).catch(e => {
        sfx.play('error');
        haptic('alert');
        show(e);                                                       // 늦게 답해도 메일은 이미 나갔을 수 있다 — 실제 상태를 다시 보고, 남아 있으면 다시 나온다
        done();
      });
    }

    /** 승인·보류·거절 — 알림 버튼과 같은 결정. 승인이면 PC 효딩쓰가 대화에서 바로 시작한다 */
    async function decide(it, dec, act) {
      haptic('tap');
      if (it.kind === 'mail' && dec === 'ok') { sendMail(it); return; }
      act.querySelectorAll('button').forEach(b => { b.disabled = true; });
      const mail = it.kind === 'mail';
      let keep = false;
      try {
        // 메일 보내기는 메일 서버에 접속해 몇 초 걸린다 — 기다려 준다
        const r = await api.post('proposals/decide', { id: it.id, dec, kind: it.kind || 'prop' }, mail ? { timeout: 60000 } : undefined);
        if (r && r.counts && typeof r.counts.pending === 'number') takeAppr(r.counts.pending);
        if (mail) {
          keep = !!r && r.ok === false;              // 보내지 못했으면 카드를 펼친 채로 — 까닭이 맨 위에 적힌다
          toast(keep ? '보내지 못했어요 — 까닭을 카드 맨 위에 적어 두었어요' : (r && r.msg) || '이미 처리된 초안이에요');
        } else {
          toast(APPR_DONE[r && r.status] || '저장했어요');
        }
        if (!keep && open === it.id) open = '';      // 그 사이 다른 카드를 보고 계시면 그대로 둔다
      } catch (e) {
        show(e);
        act.querySelectorAll('button').forEach(b => { b.disabled = false; });
        if (mail && alive) load(true);               // 서버가 늦게 답해도 메일은 이미 나갔을 수 있다 — 실제 상태를 다시 본다
        return;
      }
      if (alive) load(true);
    }

    /** 의견 — 이 제안에 대한 대표님 말을 효딩쓰에게(10-19). 효딩쓰가 읽고 고친 제안을 다시 올리거나 말씀대로 진행한다 */
    function noteSheet(it) {
      haptic('tap');
      const ta = h('textarea', { class: 'note-ta', rows: '3', maxlength: '2000', 'aria-label': '의견',
        placeholder: it.kind === 'mail' ? '예) 제목을 더 짧게 / 둘째 문단은 빼고 다시 올려 줘' : '예) 이 부분은 이렇게 바꿔서 다시 올려 줘 / 좋아, 이대로 진행해',
        oninput: () => { drafts[it.id] = ta.value; sendBtn.disabled = !ta.value.trim(); grow(ta); } });
      ta.value = drafts[it.id] || '';
      const sendBtn = h('button', { class: 'btn wide', text: '효딩쓰에게 보내기', onclick: () => submit() });
      sendBtn.disabled = !ta.value.trim();
      const scrim = h('div', { class: 'sheet-scrim', onclick: ev => { if (ev.target === scrim) close(); } },
        h('div', { class: 'sheet note-sheet', role: 'dialog', 'aria-modal': 'true', 'aria-label': '의견' },
          h('h3', { text: '의견' }),
          h('p', { class: 'note-of', text: it.title || '' }),
          ta,
          h('p', { class: 'note-hint', text: it.kind === 'mail' ? '효딩쓰가 읽고 고친 초안을 다시 올립니다 — 메일은 [보내기]를 누를 때만 나갑니다'
            : '효딩쓰가 읽고 고친 제안을 다시 올리거나, 말씀대로 진행합니다' }),
          sendBtn,
          h('button', { class: 'btn ghost wide', text: '닫기', onclick: () => close() })));
      const close = layer(() => scrim.remove());
      $('#app').append(scrim);
      grow(ta);
      ta.focus();                      // 누른 그 자리에서 — 아이폰은 손가락으로 누른 동안에만 키보드를 띄운다

      async function submit() {
        const text = ta.value.trim();
        if (!text) return;
        sendBtn.disabled = true;
        ta.disabled = true;
        haptic('tap');
        let msg = '';
        try {
          const r = await api.post('proposals/comment', { id: it.id, text, kind: it.kind || 'prop' });
          if (r && r.counts && typeof r.counts.pending === 'number') takeAppr(r.counts.pending);
          msg = (r && r.msg) || '';               // 서버가 상태에 맞게 적어 준다 — 의견 반영 중으로 옮겨졌는지, 한도로 늦는지
        } catch (e) {
          show(e);
          sendBtn.disabled = false;
          ta.disabled = false;
          return;
        }
        delete drafts[it.id];
        close();
        toast(msg || '의견을 보냈어요 — 효딩쓰가 대화에서 답합니다');
        if (alive) load();
      }
    }

    pushPage('승인함', content, () => { alive = false; }, { close: true });
    load();
  }

  // ── 사진·영상 보기 (앱 안에서 — 2026-10-04) ──
  const EXT_KIND = [['image', /\.(jpe?g|png|gif|webp|avif|heic|heif|bmp|svg)$/i], ['video', /\.(mp4|m4v|mov|webm|mkv)$/i],
    ['audio', /\.(mp3|m4a|aac|wav|ogg|oga|opus|flac)$/i], ['doc', /\.(html?|md|markdown|pdf|txt|csv|json|docx|xlsx|pptx|hwpx?|hml)$/i]];

  /** 파일 이름(없으면 주소)의 확장자로 종류를 고른다 — 모르면 문서함 항목의 종류 */
  function fileKind(name, url, fallback) {
    let path = '';
    try { path = decodeURIComponent(new URL(url, location.href).pathname); } catch (e) { path = String(url || '').split(/[?#]/)[0]; }
    for (const s of [String(name || ''), path]) {
      const hit = EXT_KIND.find(([, re]) => re.test(s));
      if (hit) return hit[0] === 'video' && /\.webm$/i.test(s) && fallback === 'audio' ? 'audio' : hit[0];
    }
    return fallback || 'doc';
  }

  /** 사진첩 사진 한 장 — 가까운 장만 받는다(viewer 의 sync 가 _near/_wake 를 부른다). 열자마자 격자에서 이미 받아 둔 작은 그림이 바로 보이고,
   *  큰 그림이 다 받아지면 갈아 끼운다(2026-11-22 대표님 「파일 하나 열고 닫을 때도 로딩이 너무 길다」 — 120장을 한꺼번에 받던 것을 현재 ±1장만으로). */
  function photoSlide(e) {
    const img = h('img', { alt: e.name || '', draggable: 'false', decoding: 'async' });
    const slide = h('div', { class: 'vw-slide' }, img);
    let stage = 0;                                             // 0 아직 · 1 작은 그림 · 2 큰 그림 받기 시작
    slide._near = () => { if (stage < 1) { stage = 1; img.classList.add('lo'); img.src = e.thumb; } };
    slide._wake = () => {
      slide._near();
      if (stage >= 2) return;
      stage = 2;
      const fetchBig = (url, last) => {
        const big = new Image();
        big.onload = () => { img.src = url; img.classList.remove('lo'); };
        big.onerror = () => {
          if (!last && e.dl && e.dl !== url) { fetchBig(e.dl, true); return; }     // 큰 그림이 아직 안 만들어졌으면 원본으로 한 번 더
          if (!img.naturalWidth) img.replaceWith(h('div', { class: 'vw-msg', text: '사진을 불러오지 못했어요' }));
        };
        big.src = url;
      };
      fetchBig(e.url, false);
    };
    return slide;
  }

  function viewerSlide(e) {
    const kind = e.kind || fileKind(e.name, e.url, '');
    if (kind === 'image' && e.id && e.thumb) return photoSlide(e);
    if (kind === 'image') {
      const img = h('img', { src: e.url, alt: e.name || '', loading: 'lazy', draggable: 'false' });
      img.addEventListener('error', () => {
        if (e.dl && !img.dataset.fb) { img.dataset.fb = '1'; img.src = e.dl; return; }          // 사진첩 — 큰 그림이 아직 안 만들어졌으면 원본으로 한 번 더
        img.replaceWith(h('div', { class: 'vw-msg', text: '사진을 불러오지 못했어요' }));
      });
      return h('div', { class: 'vw-slide' }, img);
    }
    // 사진첩 영상은 겉그림(poster)만 두고 누를 때 받는다 — 한 번에 수십 개를 미리 받지 않게
    if (kind === 'video') {
      const v = h('video', { src: e.url, controls: true, playsinline: true, 'webkit-playsinline': true, preload: e.poster ? 'none' : 'metadata' });
      const slide = h('div', { class: 'vw-slide' }, v);
      if (e.poster) slide._near = () => { if (!v.poster) v.poster = e.poster; };       // 겉그림도 가까운 장만
      return slide;
    }
    if (kind === 'audio') {
      return h('div', { class: 'vw-slide' }, h('div', { class: 'vw-audio' },
        h('div', { class: 'vw-msg', text: '🔊 ' + (e.name || '음성') }), h('audio', { src: e.url, controls: true, preload: 'metadata' })));
    }
    // 문서 — 처음 볼 때 주소를 넣는다(넘기지 않은 문서는 받지 않음)
    // sandbox — 같은 woopang.com 이라도 문서 속 스크립트가 앱의 기기 토큰(localStorage)에 못 닿게 allow-same-origin 은 뺀다(10/4)
    // 불러오는 동안은 흰 칸 대신 스피너(2026-11-18 대표님) — 문서가 다 그려진 뒤(load)에야 보인다
    const frame = h('iframe', { 'data-src': e.url, title: e.name || '문서', referrerpolicy: 'no-referrer',
      sandbox: 'allow-scripts allow-popups allow-popups-to-escape-sandbox allow-downloads' });
    const doc = h('div', { class: 'vw-doc loading' }, frame, h('div', { class: 'vw-load', 'aria-label': '불러오는 중' }, h('i', { class: 'spinner' })));
    // 빈 iframe 이 넣어질 때 나는 load 는 무시 — 주소·내용을 실제로 넣은 뒤(dataset.go)의 load 만 진짜
    frame.addEventListener('load', () => { if (frame.dataset.go) doc.classList.remove('loading'); });
    frame.docFail = () => { doc.classList.remove('loading'); doc.replaceChildren(h('div', { class: 'vw-msg', text: '문서를 불러오지 못했어요' })); };
    return h('div', { class: 'vw-slide' }, doc);
  }

  /** 문서 한 장을 그린다. woopang.com 은 X-Frame-Options DENY 라 iframe 에 주소를 넣으면 앱 안에서는 빈 화면이었다
      (브라우저로 열면 보임 — 10/6 「우팡앱 제휴」). 같은 사이트 문서는 글을 받아 srcdoc 으로 그린다 — <base> 로 그림·상대 주소를 살리고,
      sandbox(allow-same-origin 없음)는 그대로라 문서 속 스크립트는 앱 저장소에 못 닿는다. 링크는 앱이 브라우저로 연다(DOC_LINK). */
  const DOC_LINK = '<script>document.addEventListener("click",function(e){var a=e.target.closest&&e.target.closest("a[href]");if(!a)return;'
    + 'var raw=a.getAttribute("href")||"";if(raw.charAt(0)==="#"){var id=decodeURIComponent(raw.slice(1)),t=document.getElementById(id)||document.getElementsByName(id)[0];'
    + 'e.preventDefault();if(t)t.scrollIntoView();return;}if(!/^https?:/i.test(a.href))return;e.preventDefault();parent.postMessage({hyoDocOpen:a.href},"*");},true);'
    + 'var X0=0,Y0=0,sw=0,hs=function(n){while(n&&n.nodeType===1){var cs=getComputedStyle(n);if(/(auto|scroll)/.test(cs.overflowX)&&n.scrollWidth>n.clientWidth+2)return 1;n=n.parentNode}return 0};'
    + 'addEventListener("touchstart",function(e){sw=e.touches.length===1&&!hs(e.target)?1:0;X0=e.touches[0].clientX;Y0=e.touches[0].clientY},{passive:true});'
    + 'var fire=function(c){if(!sw)return;var dx=c.clientX-X0,dy=c.clientY-Y0;if(Math.abs(dx)>64&&Math.abs(dx)>Math.abs(dy)*1.8){sw=0;parent.postMessage({hyoDocSwipe:dx<0?1:-1},"*")}};'
    + 'addEventListener("touchmove",function(e){fire(e.touches[0])},{passive:true});addEventListener("touchend",function(e){fire(e.changedTouches[0]);sw=0},{passive:true});'
    + 'var g=function(n){var c=getComputedStyle(n).backgroundColor,m=c.match(/[\\d.]+/g);return m&&(m.length<4||+m[3]>0.05)?m:0},'
    + 'th=function(){var m=g(document.body)||g(document.documentElement)||[255,255,255],L=.299*m[0]+.587*m[1]+.114*m[2];'
    + 'parent.postMessage({hyoDocTheme:{bg:"rgb("+(+m[0])+","+(+m[1])+","+(+m[2])+")",fg:L>150?"#1b1f2a":"#fff"}},"*")};'
    + 'addEventListener("DOMContentLoaded",function(){var s=document.createElement("div");s.style.cssText="height:76px;flex:none";document.body.appendChild(s);th()});addEventListener("load",th);'
    + 'var rh=function(){var b=document.body;parent.postMessage({hyoDocH:Math.max(document.documentElement.scrollHeight,b?b.scrollHeight:0)},"*")};'
    + 'addEventListener("DOMContentLoaded",function(){rh();if(window.ResizeObserver)new ResizeObserver(rh).observe(document.documentElement)});addEventListener("load",rh);<\/script>';
  const DOC_PAGE = body => '<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
    + '<style>body{margin:0;padding:18px 16px 48px;font:15px/1.65 -apple-system,BlinkMacSystemFont,"Apple SD Gothic Neo",system-ui,sans-serif;color:#1b1f2a;background:#fff;'
    + 'overflow-wrap:anywhere;word-break:keep-all}pre{white-space:pre-wrap}pre.txt{font:inherit}table{border-collapse:collapse;display:block;overflow-x:auto}td,th{border:1px solid #ddd;padding:4px 8px}'
    + 'img{max-width:100%}@media (prefers-color-scheme:dark){body{background:#14161c;color:#e8eaf0}td,th{border-color:#333}}</style></head><body>' + body + '</body></html>';

  /** PDF 는 앱 안에서 pdf.js 로 한 장씩 그려 바로 읽는다(2026-11-19 대표님). 아이폰 앱 속 iframe 의 PDF 는 첫 쪽만 보이고 안 내려가서 —
      PDF 파일을 받아 문서 칸(sandbox) 안으로 넘기고, 그 안에서 쪽마다 캔버스로 그린다. 쪽 크기를 먼저 정해 두어 높이가 흔들리지 않는다. */
  const PDF_JS = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.min.js';
  const PDF_JS_SRI = 'sha512-q+4liFwdPC/bNdhUpZx6aXDx/h77yEQtn4I1slHydcbZK34nLaR3cAeYSJshoxIOq3mjEf7xJE8YWIUHMn+oCQ==';
  const PDF_WORKER = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
  const PDF_RUN = '(function(){var u8=Uint8Array.from(atob(document.getElementById("pdfb").textContent),function(c){return c.charCodeAt(0)}),host=document.getElementById("p");'
    + 'var fail=function(m){host.innerHTML="<div class=msg>"+m+"</div>"};'
    + 'if(!window.pdfjsLib){fail("PDF 를 여는 도구를 불러오지 못했어요 — 저장해서 보세요");return}'
    + 'fetch("' + PDF_WORKER + '").then(function(r){return r.text()}).then(function(t){'
    + 'pdfjsLib.GlobalWorkerOptions.workerSrc=URL.createObjectURL(new Blob([t],{type:"text/javascript"}));return pdfjsLib.getDocument({data:u8,isEvalSupported:false}).promise}).then(function(pdf){'
    + 'var W=document.documentElement.clientWidth,n=Math.min(pdf.numPages,300),dpr=Math.min(window.devicePixelRatio||1,2),jobs=[];for(var i=1;i<=n;i++)jobs.push(pdf.getPage(i));'
    + 'return Promise.all(jobs).then(function(ps){host.textContent="";var items=ps.map(function(pg){var s=W/pg.getViewport({scale:1}).width,v=pg.getViewport({scale:s}),c=document.createElement("canvas");'
    + 'c.className="pg";c.style.width=v.width+"px";c.style.height=v.height+"px";host.appendChild(c);return[pg,c,s]});'
    + 'return items.reduce(function(ch,x){return ch.then(function(){var vp=x[0].getViewport({scale:x[2]*dpr});x[1].width=vp.width;x[1].height=vp.height;return x[0].render({canvasContext:x[1].getContext("2d"),viewport:vp}).promise})},Promise.resolve())})'
    + '}).catch(function(){fail("PDF 를 열지 못했어요 — 저장해서 보세요")})})();';
  const PDF_PAGE = b64 => '<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
    + '<style>html,body{margin:0;background:#e4e6ec}#p{padding:8px 0 96px}.pg{display:block;margin:0 auto 8px;background:#fff;box-shadow:0 1px 4px rgba(0,0,0,.18)}'
    + '.msg{padding:28px 20px;font:15px/1.6 -apple-system,"Apple SD Gothic Neo",sans-serif;color:#444;text-align:center}</style></head>'
    + '<body><div id="p"><div class="msg">PDF 여는 중…</div></div><script id="pdfb" type="text/plain">' + b64 + '<\/script>'
    + '<script src="' + PDF_JS + '" integrity="' + PDF_JS_SRI + '" crossorigin="anonymous"><\/script><script>' + PDF_RUN + '<\/script>' + DOC_LINK + '</body></html>';
  const toB64 = buf => { const a = new Uint8Array(buf); let s = ''; for (let i = 0; i < a.length; i += 0x8000) s += String.fromCharCode.apply(null, a.subarray(i, i + 0x8000)); return btoa(s); };

  async function loadDoc(f) {
    const url = f.dataset.src;
    f.removeAttribute('data-src');
    let abs = null;
    try { abs = new URL(url, location.href); } catch (e) { /* 아래 주소로 */ }
    if (abs && abs.origin === location.origin) {
      try {
        const r = await fetch(abs.href, { cache: 'no-store' });
        const type = (r.headers.get('content-type') || '').toLowerCase();
        if (r.ok && type.includes('text/html')) {
          const page = await r.text();
          const head = '<base href="' + esc(abs.href) + '">' + DOC_LINK;
          f.dataset.go = '1';
          f.srcdoc = /<head[^>]*>/i.test(page) ? page.replace(/<head[^>]*>/i, m => m + head) : head + page;
          return;
        }
        if (r.ok && /^text\/(markdown|x-markdown)/.test(type)) { f.dataset.go = '1'; f.srcdoc = DOC_PAGE(markdown(await r.text()) + DOC_LINK); return; }
        if (r.ok && /^text\/|json/.test(type)) { f.dataset.go = '1'; f.srcdoc = DOC_PAGE('<pre class="txt">' + esc(await r.text()) + '</pre>' + DOC_LINK); return; }
        if (r.ok && /pdf/.test(type)) {
          const buf = await r.arrayBuffer();
          if (buf.byteLength <= 14 * 1024 * 1024) { f.dataset.go = '1'; f.srcdoc = PDF_PAGE(toB64(buf)); return; }
        }
        if (!r.ok && f.docFail) { f.docFail(); return; }    // 기한 지남·없는 문서 — 스피너를 계속 돌리지 않는다
      } catch (e) { /* 아래 주소로 */ }
    }
    f.dataset.go = '1';
    f.src = url;                                            // PDF·다른 사이트 — 예전처럼 주소로
  }

  let docOpenAt = 0;
  window.addEventListener('message', ev => {
    const mine = Array.from(document.querySelectorAll('.vw-doc iframe')).find(x => x.contentWindow === ev.source);
    if (mine && ev.data && ev.data.hyoDocSwipe) {           // 글 문서 안에서 좌우로 쓸어 — 앞·뒤 장으로
      const vw = mine.closest('.viewer');
      if (vw && vw.stepViewer) vw.stepViewer(ev.data.hyoDocSwipe > 0 ? 1 : -1);
      return;
    }
    if (mine && ev.data && ev.data.hyoDocH) {               // 글 문서는 내용 높이만큼 늘려, 스크롤을 문서 칸(.vw-doc) 하나가 맡게 한다 — 칸 속 칸(iframe 안 스크롤)은 아이폰에서 안 내려가는 일이 있었다
      const h = Math.ceil(+ev.data.hyoDocH) || 0, prev = mine._h || 0;
      if (mine._off || h <= 0 || Math.abs(h - prev) <= 2) return;
      if (prev && h > prev) mine._g = (mine._g || 0) + 1;
      if (mine._g > 4 || h >= 30000) {                                      // 늘려 줄수록 또 늘어나는 문서(vh 단위를 쓰는 것) — 예전처럼 문서 안 스크롤로 돌린다
        mine._off = true; mine.style.height = ''; return;
      }
      mine._h = h;
      mine.style.height = Math.max(h, mine.parentNode.clientHeight) + 'px';
      return;
    }
    if (mine && ev.data && ev.data.hyoDocTheme) {           // 문서 바탕색 — 위 줄·바탕을 같은 색으로
      const t = ev.data.hyoDocTheme;
      if (/^rgb\(\d+,\d+,\d+\)$/.test(t.bg) && /^#[0-9a-f]{3,6}$/i.test(t.fg)) {
        mine.dataset.bg = t.bg; mine.dataset.fg = t.fg;
        const vw = mine.closest('.viewer');
        if (vw && vw.paintViewer) vw.paintViewer();
      }
      return;
    }
    const u = ev.data && ev.data.hyoDocOpen;
    if (typeof u !== 'string' || !/^https?:\/\//i.test(u) || Date.now() - docOpenAt < 800) return;
    if (!Array.from(document.querySelectorAll('.vw-doc iframe')).some(fr => fr.contentWindow === ev.source)) return;
    docOpenAt = Date.now();
    openLink(u);
  });

  /** 전체 화면 보기 — entries: [{url, name, kind}], start: 처음 보일 장.
   *  사진첩(2026-11-20)은 entries 에 dl(저장·공유할 원본 주소) · poster(영상 겉그림) · cap([윗줄, 아랫줄] 설명) · id 를 더 넣고,
   *  opts.album(entry) 을 주면 위 줄에 「사진첩에 담기」 단추가 생긴다. */
  function openViewer(entries, start, title, opts) {
    const list = (entries || []).filter(e => e && e.url);
    if (!list.length) return;
    opts = opts || {};
    let idx = Math.max(0, Math.min(start || 0, list.length - 1)), raf = 0, many = list.length > 1;
    const pv = !!(opts.photo && opts.del);                     // 사진첩 사진·영상 보기 — 앱 바탕색 · 앞뒤 단추 없음 · 아래 줄에 설명(왼쪽)과 이동·삭제(오른쪽)

    const counter = h('div', { class: 'vw-count', 'aria-live': 'polite' });
    const capEl = h('div', { class: 'vw-cap', hidden: true });
    const track = h('div', { class: 'vw-track' }, list.map(viewerSlide));
    const prev = many && !pv ? h('button', { class: 'vw-arrow prev', 'aria-label': '앞 장', html: ICON.back, onclick: () => go(idx - 1) }) : null;
    const next = many && !pv ? h('button', { class: 'vw-arrow next', 'aria-label': '다음 장', html: ICON.back, onclick: () => go(idx + 1) }) : null;
    const pbar = pv ? h('div', { class: 'vw-pbar' }, capEl,
      h('div', { class: 'vw-pacts' },
        opts.move ? h('button', { class: 'vw-move', 'aria-label': '폴더로 이동', onclick: () => moveCur() }, h('span', { html: ICON.folder }), '이동') : null,
        h('button', { class: 'vw-btn', 'aria-label': '삭제', html: ICON.trash, onclick: () => trashCur() }))) : null;
    const el = h('div', { class: 'viewer' + (pv ? ' pv' : ''), role: 'dialog', 'aria-modal': 'true', 'aria-label': title || '보기' },
      h('div', { class: 'vw-top' },
        h('button', { class: 'vw-btn', 'aria-label': '닫기', html: ICON.close, onclick: () => close() }),
        counter,
        h('div', { class: 'vw-acts' },
          opts.album ? h('button', { class: 'vw-btn', 'aria-label': '사진첩에 담기', html: ICON.albumAdd, onclick: () => opts.album(list[idx]) }) : null,
          h('button', { class: 'vw-btn', 'aria-label': '저장', html: ICON.save, onclick: () => downloadFile(list[idx].dl || list[idx].url, list[idx].name) }),
          h('button', { class: 'vw-btn', 'aria-label': '공유', html: ICON.share, onclick: share }))),
      h('div', { class: 'vw-stage' }, track),
      pbar || capEl,
      many && !pv ? h('div', { class: 'vw-bot' }, prev, next) : null);

    /** 보던 장을 목록에서 빼고 이웃 장(없으면 앞 장)을 보여 준다 — 다 비면 닫는다 */
    function dropSlide(i) {
      const slide = track.children[i];
      if (slide) { slide.querySelectorAll('video,audio').forEach(m => { try { m.pause(); } catch (e) { /* 이미 멈춤 */ } }); slide.remove(); }
      list.splice(i, 1);
      if (!list.length) { close(); return; }
      many = list.length > 1;
      idx = Math.min(i, list.length - 1);
      sync();
      jump();
    }

    function trashCur() {
      const i = idx, e = list[i];
      if (!e || !opts.del) return;
      haptic('tap');
      opts.del(e);
      dropSlide(i);
    }

    function moveCur() {
      const e = list[idx];
      if (!e || !opts.move) return;
      const p = opts.move(e);
      if (p && p.then) p.then(ok => { if (ok) { const i = list.indexOf(e); if (i >= 0) dropSlide(i); } });
    }

    /** 글 문서가 보이는 동안은 트랙의 가로 스크롤을 잠근다. 넘기는 중(스크롤 중)에 잠그면 중간에 멈추니 멈춘 뒤에 */
    let lockTimer = 0;
    function applyLock() {
      const cur = track.children[idx];
      track.classList.toggle('lock', !!(cur && cur.querySelector('.vw-doc')));
    }
    function lockSoon() { clearTimeout(lockTimer); lockTimer = setTimeout(applyLock, 160); }

    /** 글 문서를 보는 동안: 위 줄·바탕을 문서 바탕색으로, 앞·뒤 장 단추는 문서 위에 떠 있게 */
    function paintViewer() {
      const cur = track.children[idx], fr = cur && cur.querySelector('.vw-doc iframe');
      el.classList.toggle('docmode', !!fr);
      if (fr && fr.dataset.bg) { el.style.setProperty('--vw-bg', fr.dataset.bg); el.style.setProperty('--vw-fg', fr.dataset.fg || '#fff'); }
      else { el.style.removeProperty('--vw-bg'); el.style.removeProperty('--vw-fg'); }
    }
    el.paintViewer = paintViewer;

    function sync() {
      paintViewer();
      counter.textContent = many ? (idx + 1) + ' / ' + list.length : '';
      const cap = list[idx].cap;
      capEl.hidden = !cap;
      if (cap) capEl.replaceChildren(h('b', { text: cap[0] }), cap[1] ? h('small', { text: cap[1] }) : null);
      if (prev) prev.hidden = idx === 0;
      if (next) next.hidden = idx === list.length - 1;
      track.classList.remove('lock');                      // 넘기는 동안은 풀어 두고 — 멈춘 뒤 문서면 다시 잠근다
      lockSoon();
      Array.from(track.children).forEach((s, i) => {
        if (i !== idx) s.querySelectorAll('video,audio').forEach(m => { try { m.pause(); } catch (e) { /* 이미 멈춤 */ } });
        const f = s.querySelector('iframe[data-src]');
        if (f && Math.abs(i - idx) <= 1) loadDoc(f);        // 보는 장과 이웃 장을 미리 — 넘길 때 기다리지 않게
        if (i === idx && s.querySelector('.vw-doc')) warmDoc(list[i]);   // 저장·공유할 PDF 도 보는 동안 미리(같은 파일은 한 번만)
      });
      // 사진첩 사진 — 보는 장의 큰 그림을 제일 먼저, 이웃 장은 큰 그림(±1)·작은 그림(±3)까지만
      const curS = track.children[idx];
      if (curS && curS._wake) curS._wake(); else if (curS && curS._near) curS._near();
      for (let d = 1; d <= 3; d++) [idx - d, idx + d].forEach(i => {
        const s = track.children[i];
        if (!s) return;
        if (s._near) s._near();
        if (d === 1 && s._wake) s._wake();
      });
    }

    function go(i) {
      i = Math.max(0, Math.min(i, list.length - 1));
      track.classList.remove('lock');
      track.scrollTo({ left: i * track.clientWidth, behavior: 'smooth' });
    }

    function jump() {
      track.scrollLeft = idx * track.clientWidth;
    }

    track.addEventListener('scroll', () => {
      if (!track.classList.contains('lock')) lockSoon();   // 스크롤이 멈추고 160ms 뒤에 잠근다(넘기는 도중엔 안 잠긴다)
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(() => {
        const w = track.clientWidth || 1;
        const i = Math.max(0, Math.min(Math.round(track.scrollLeft / w), list.length - 1));
        if (i !== idx) { idx = i; sync(); }
      });
    }, { passive: true });

    function share() {
      const e = list[idx];
      return shareFiles([e.dl ? { url: e.dl, name: e.name } : e], e.name || title || '효딩쓰');
    }

    /** 아래로 쓸어내리면 닫힌다 (2026-11-02 대표님) — 사진·영상·상단·하단에서. 가로로 넘기기·확대·글 문서(안쪽이 따로 스크롤)는 건드리지 않는다. */
    let sy = 0, sx = 0, st = 0, drag = false;
    // 영상은 맨 아래 조절줄(재생·시간 막대)만 빼고 어디서나 쓸어내려 닫는다(2026-11-22 대표님 「영상은 아래로 스와이프해도 닫히지 않는다」)
    const onVideoBar = ev => {
      const v = ev.target.closest && ev.target.closest('video');
      return !!v && ev.touches[0].clientY > v.getBoundingClientRect().bottom - 96;
    };
    el.addEventListener('touchstart', ev => {
      drag = ev.touches.length === 1 && !el.classList.contains('docmode') && !(window.visualViewport && window.visualViewport.scale > 1.05)
        && !ev.target.closest('iframe,.vw-btn,.vw-move,.vw-open,.vw-arrow,audio') && !onVideoBar(ev);
      if (!drag) return;
      sx = ev.touches[0].clientX; sy = ev.touches[0].clientY; st = Date.now();
    }, { passive: true });
    el.addEventListener('touchmove', ev => {
      if (!drag) return;
      const dx = ev.touches[0].clientX - sx, dy = ev.touches[0].clientY - sy;
      if (dy > 12 && dy > Math.abs(dx) * 1.5) {
        el.style.transition = 'none';
        el.style.transform = 'translateY(' + dy + 'px)';
        el.style.opacity = String(Math.max(0.3, 1 - dy / 500));
      } else if (Math.abs(dx) > 24) { drag = false; el.style.transform = ''; el.style.opacity = ''; }
    }, { passive: true });
    const endDrag = ev => {
      if (!drag) return;
      drag = false;
      const t = ev.changedTouches && ev.changedTouches[0], dy = t ? t.clientY - sy : 0, dx = t ? t.clientX - sx : 0;
      el.style.transition = 'transform .18s ease, opacity .18s ease';
      if (dy > 110 && dy > Math.abs(dx) * 1.5 && (dy > 180 || Date.now() - st < 600)) {
        haptic('tap'); sfx.play('tap');
        el.style.transform = 'translateY(100%)'; el.style.opacity = '0';
        setTimeout(() => close(), 120);
      } else { el.style.transform = ''; el.style.opacity = ''; }
    };
    el.addEventListener('touchend', endDrag, { passive: true });
    el.addEventListener('touchcancel', endDrag, { passive: true });

    const onKey = ev => {
      if (ev.key === 'Escape') close();
      else if (ev.key === 'ArrowLeft') go(idx - 1);
      else if (ev.key === 'ArrowRight') go(idx + 1);
    };
    const close = layer(() => {
      document.removeEventListener('keydown', onKey);
      removeEventListener('resize', jump);
      cancelAnimationFrame(raf);
      el.querySelectorAll('video,audio').forEach(m => { try { m.pause(); } catch (e) { /* 이미 멈춤 */ } });
      el.remove();
    });
    el.closeViewer = close;
    el.stepViewer = d => go(idx + d);
    document.addEventListener('keydown', onKey);
    addEventListener('resize', jump);                      // 가로·세로 돌려도 보던 장 그대로
    $('#app').append(el);
    sync();
    jump();
    requestAnimationFrame(jump);
    return el;
  }

  // ── 설정 (SettingsView) ────────────────
  function openSettings() {
    const sel = (options, value, onChange, label) => {
      const s = h('select', { 'aria-label': label, onchange: () => onChange(s.value) },
        options.map(([k, l]) => h('option', { value: k, selected: k === value, text: l })));
      return s;
    };
    const speedVal = h('span', { text: S.speed.toFixed(2) + '×' });
    const slider = h('input', { type: 'range', min: '0.8', max: '1.6', step: '0.05', value: String(S.speed), 'aria-label': '말 빠르기',
      oninput: () => { S.speed = parseFloat(slider.value); speedVal.textContent = S.speed.toFixed(2) + '×'; },
      onchange: () => commitSpeed() });
    const themeSeg = h('div', { class: 'seg', role: 'radiogroup', 'aria-label': '화면 색' });
    const drawSeg = () => {
      themeSeg.textContent = '';
      THEMES.forEach(([k, l]) => themeSeg.append(h('button', { class: S.theme === k ? 'on' : '', role: 'radio', 'aria-checked': S.theme === k ? 'true' : 'false', text: l,
        onclick: () => { setTheme(k); drawSeg(); } })));
    };
    drawSeg();
    const section = (title, rows, foot) => h('div', { class: 'fsec' }, title ? h('h3', { text: title }) : null, h('div', { class: 'box' }, rows), foot ? h('p', { text: foot }) : null);
    const row = (k, v) => h('div', { class: 'frow' }, h('span', { class: 'k', text: k }), v);

    const content = h('div', { class: 'content' }, h('div', { class: 'form' },
      section('답을 소리로 듣기', [
        row('언제', sel(REPLY_VOICE, S.replyVoice, v => { S.replyVoice = v; local.set('replyVoice', v); }, '언제')),
        row('목소리', sel(VOICE_ENGINE, S.voiceEngine, v => { S.voiceEngine = v; local.set('voiceEngine', v); }, '목소리')),
        h('div', { class: 'frow col' }, h('div', { class: 'top' }, '말 빠르기', speedVal), slider),
        h('div', { class: 'frow' }, h('button', { class: 'link', text: '들어 보기', onclick: testVoice })),
      ], '말 빠르기는 PC 스피커·텔레그램 음성 메시지의 소희 목소리에도 함께 적용돼요.'),
      section('효딩쓰', [row('모델', sel(S.models.map(m => [m.key, m.label]), S.model, setModel, '모델'))],
        'SONNET 5.5 HIGH 가 기본이에요. ULTRACODE 는 여러 에이전트를 묶어 더 꼼꼼히 하지만 구독 사용량을 훨씬 많이 씁니다. '
        + 'OPUS 는 SONNET 보다 깊이 생각하는 대신 사용량이 더 듭니다.'),
      section('화면', [h('div', { class: 'frow col' }, themeSeg),
        row('버튼 효과음', sel([['1', '켜기'], ['0', '끄기']], local.get('sfx', '1'), v => { local.set('sfx', v); sfx.play('tap'); }, '버튼 효과음'))], null),
      section('연결', [
        row('붙은 곳', h('span', { class: 'v', text: S.host === 'brain' ? 'PC 뒤의 두뇌' : 'PC 효딩쓰 창' })),
        row('서버', h('span', { class: 'v', style: 'font-size:12px', text: api.base })),
        h('div', { class: 'frow' }, h('button', { class: 'link danger', text: '이 폰 연결 끊기', onclick: confirmUnpair })),
      ], '끊으면 이 폰의 열쇠가 PC 에서도 지워집니다. 다시 쓰려면 PC 창 ☰ 에서 새 코드를 받으세요.'),
      section(null, [h('div', { class: 'frow' }, h('span', { class: 'k', style: 'font-size:13px;color:var(--ink-3)', text: appVersion ? '버전 ' + appVersion + ' (' + WEB_VERSION + ')' : '버전 ' + WEB_VERSION }))], null)));
    pushPage('설정', content);
  }

  function confirmUnpair() {
    const scrim = h('div', { class: 'sheet-scrim', onclick: ev => { if (ev.target === scrim) close(); } },
      h('div', { class: 'sheet', role: 'dialog', 'aria-modal': 'true' },
        h('h3', { text: '이 폰의 연결을 끊을까요?' }),
        h('p', { text: '다시 쓰려면 PC 창에서 새 코드를 받아야 해요.' }),
        h('button', { class: 'btn danger wide', text: '연결 끊기', onclick: () => { close(); unpair('연결을 끊었습니다. 다시 쓰려면 PC 창에서 새 코드를 받아 주세요.'); } }),
        h('button', { class: 'btn ghost wide', text: '그대로 두기', onclick: () => close() })));
    const close = layer(() => scrim.remove());
    $('#app').append(scrim);
  }

  // ── 라이트 · 다크 ──────────────────────
  const darkQuery = window.matchMedia ? matchMedia('(prefers-color-scheme: dark)') : null;

  function setTheme(k) {
    S.theme = k;
    local.set('theme', k);
    applyTheme();
  }

  function applyTheme() {
    const root = document.documentElement;
    if (S.theme === 'system') root.removeAttribute('data-theme'); else root.setAttribute('data-theme', S.theme);
    const dark = S.theme === 'dark' || (S.theme === 'system' && darkQuery && darkQuery.matches);
    const bar = dark ? '#0A0F27' : '#1B2A6B';
    document.querySelectorAll('meta[name=theme-color]').forEach(m => m.setAttribute('content', bar));
    if (Native) Native.setBars({ status: bar, nav: dark ? '#141B3D' : '#FFFFFF', lightNav: !dark }).catch(() => {});
  }
  if (darkQuery && darkQuery.addEventListener) darkQuery.addEventListener('change', applyTheme);

  // ── 시작 ───────────────────────────────
  let appVersion = '';

  // 모든 터치 — 누를 수 있는 것(버튼·줄·칩·스위치·바깥 어두운 곳)을 누르면 진동 + 짧은 소리 (2026-11-02 대표님: 승인함 열기·줄 펼치기 포함 전부).
  // 그 칸의 처리기가 이미 소리·진동을 냈으면(보내기·삭제·메뉴 등 제 소리) 겹치지 않게 빠진 쪽만 채운다. 막힌 버튼·글 쓰는 칸은 조용히.
  const TAP_SEL = 'button,a[href],summary,label,select,input[type=checkbox],input[type=radio],[role=button],[role=menuitem],[role=menuitemradio],[role=tab],[role=switch]';
  document.addEventListener('click', ev => {
    if (pressHeld || Date.now() < swallowUntil) return;
    let el = null;
    for (let n = ev.target; n && n.nodeType === 1; n = n.parentElement) {
      if (tappable.has(n) || (n.matches && n.matches(TAP_SEL))) { el = n; break; }
    }
    if (!el || el.disabled || el.getAttribute('aria-disabled') === 'true') return;
    if (/scrim/.test(el.className) && ev.target !== el) return;       // 열린 창 안쪽을 눌렀을 뿐 — 바깥 어두운 곳을 눌렀을 때만
    if (hapticAt < ev.timeStamp) haptic('tap');
    if (sfxAt < ev.timeStamp) sfx.play('tap');
  });

  document.addEventListener('click', ev => {
    const a = ev.target.closest && ev.target.closest('a[href]');
    if (!a) return;
    ev.preventDefault();
    const href = a.getAttribute('href');
    if (a.dataset.doc) openDoc(a.dataset.doc);
    else if (/\/AI\/doc\//.test(href) && INAPP_DOC.test(href.split(/[?#]/)[0])) openViewer([{ url: href, name: nameFromUrl(href) }], 0, nameFromUrl(href));
    else openLink(href);
  });

  document.addEventListener('visibilitychange', () => appVisible(!document.hidden));

  if (AppPlugin) {
    AppPlugin.addListener('appStateChange', s => appVisible(!!s.isActive));
    AppPlugin.addListener('backButton', () => {
      if (layers.length) { layers[layers.length - 1](); return; }
      if (popPage()) return;
      AppPlugin.minimizeApp();
    });
    AppPlugin.getInfo().then(i => { appVersion = i.version || ''; }).catch(() => {});
  }

  if (Native) Native.addListener('pushOpen', takePushOpen);        // 떠 있는 화면에서 알림을 눌렀다 (아이폰 2.1+)
  syncBackupAuth();                                                // 폰 사진 자동 백업: 서버 주소·열쇠를 앱에 (앱이 직접 올린다)
  applyTheme();
  S.paired = !!api.token;
  if (S.paired) { showMain(); startPolling(); takePushOpen(); } else { showPair(); }
})();
