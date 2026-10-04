/* 효딩쓰 휴대폰 화면 — https://woopang.com/AI/desk-app/
 * 아이폰 앱(apps/hyodingsseu-ios)의 DeskStore·Audio·UI 를 그대로 옮긴 것.
 * PC 효딩쓰 원격 API: /AI/api/desk/<sub> (ai_office/routes.py desk_proxy → server/voice/desk/remote_api.py)
 * 안드로이드 앱(Capacitor)에서는 window.Capacitor.Plugins.DeskNative 로 폰 목소리·진동·링크 열기를 쓴다.
 * 브라우저에서 열어도 동작한다(폰 목소리는 브라우저 음성이 있으면 그것, 없으면 PC 소희 목소리).
 */
'use strict';
(function () {
  const WEB_VERSION = '2026-10-04';
  const Cap = window.Capacitor;
  const Native = (Cap && Cap.Plugins && Cap.Plugins.DeskNative) || null;
  const AppPlugin = (Cap && Cap.Plugins && Cap.Plugins.App) || null;

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
  const QUICK = [['서버 상태', '서버 상태 어때?'], ['승인 대기', 'AI Office 승인 대기 뭐 있어?'],
    ['오늘 할 일', '오늘 뭐부터 하면 좋을까? 급한 것부터 3개만.'], ['농민닷컴 현황', '농민닷컴 오늘 주문이랑 배송 현황 짧게 알려줘'],
    ['홍보 뭐 올려', '오늘 올릴 홍보물 하나 만들어서 보내줘']];
  const SUGGEST = ['서버 상태 어때?', 'AI Office 직원들 지금 뭐 하고 있어?', '농민닷컴 오늘 주문 현황 알려줘', '오늘 뭐부터 하면 좋을까?'];
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
    set token(v) { if (v) local.set('desk-token', v); else local.del('desk-token'); },

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
  const haptic = kind => { if (Native) Native.haptic({ kind }).catch(() => {}); };

  /** PC 문서 경로 → PC 효딩쓰가 서명한 링크(30일)를 받아 앱 밖(브라우저)에서 연다 */
  async function openDoc(path) {
    const w = Native ? null : window.open('', '_blank');           // 브라우저: 누른 순간 창을 열어 두어야 막히지 않는다
    try {
      const r = await api.get('doc', { path });
      if (w) w.location = r.url; else openLink(r.url);
    } catch (e) {
      if (w) w.close();
      show(e);
    }
  }

  function openLink(url) {
    if (Native) Native.openExternal({ url }).catch(e => toast(e.message || '열 수 없어요'));
    else window.open(url, '_blank', 'noopener');
  }

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

  async function pollLoop(ctl) {
    let delay = 1;
    boot = '';
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
        const r = await api.get('events', { since: seq, boot, wait: 25 }, 40000, ctl.signal);
        if (ctl.signal.aborted) return;
        setLink({ k: 'online' });
        delay = 1;
        if (r.reset) { boot = ''; continue; }
        const stick = nearBottom();
        let force = false;
        (r.events || []).forEach(e => {
          if (apply(e)) force = true;
          if (typeof e.seq === 'number') seq = Math.max(seq, e.seq);
        });
        if (typeof r.seq === 'number') seq = Math.max(seq, r.seq);
        if (typeof r.state === 'string') S.agentState = r.state;
        renderStatus();
        afterChange(stick || force, (r.events || []).length > 0);
      } catch (e) {
        if (ctl.signal.aborted) return;
        if (e.status === 401) { unpair(e.message, false); return; }
        setLink({ k: 'offline', msg: e.message || 'PC 효딩쓰에 닿지 않아요' });
        await sleep(delay * 1000, ctl.signal);
        delay = Math.min(delay * 2, 20);
        boot = '';
      }
    }
  }

  function setLink(l) {
    S.link = l;
    renderStatus();
  }

  function appVisible(active) {
    if (active) {
      if (S.paired) startPolling();
    } else {
      stopPolling();                                    // 45초 넘게 안 보이면 PC 가 텔레그램으로도 보낸다
      if (recorder.recording) { recorder.cancel(); renderStatus(); }
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

  async function toggleRecording() {
    if (recorder.recording) { finishRecording(); return; }
    speaker.stop();
    if (await recorder.start()) {
      haptic('tap');
    } else {
      toast('마이크를 쓸 수 없어요 — 설정 › 애플리케이션 › 효딩쓰 › 권한 › 마이크를 켜 주세요.');
    }
    renderStatus();
  }

  async function finishRecording() {
    if (!recorder.recording) return;
    const blob = await recorder.stop();
    renderStatus();
    if (!blob || blob.size <= 3000) {
      toast('너무 짧아요. 누르고 말씀한 뒤 다시 누르세요.');
      return;
    }
    S.uploadingVoice = true;
    renderStatus();
    try {
      const r = await api.request('voice', { method: 'POST', body: blob, contentType: blob.type || 'audio/webm', timeout: 100000 });
      if (!String(r.text || '').trim()) toast('잘 들리지 않았어요. 다시 말씀해 주세요.');
      else speakNext = true;
    } catch (e) {
      show(e);
    } finally {
      S.uploadingVoice = false;
      renderStatus();
    }
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

    /** 읽던 것·준비하던 것(PC 목소리 받는 중) 모두 멈춘다 */
    stop() {
      this.gen++;
      pace.run = null;                                      // 멈춘 건 빠르기 재기에서 뺀다
      if (this.via === 'native' && Native) Native.stop().catch(() => {});
      if (this.via === 'web' && window.speechSynthesis) speechSynthesis.cancel();
      if (this.audio) { this.audio.onended = this.audio.onerror = null; this.audio.pause(); this.audio = null; }
      if (this.url) { URL.revokeObjectURL(this.url); this.url = null; }
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
    const viaPC = async () => {
      if (full) toast('소희 목소리를 만드는 중이에요 — 긴 글은 조금 걸려요');
      const blob = await api.request('tts', { method: 'POST', body: JSON.stringify({ text }), timeout: 100000, as: 'blob' });
      if (alive()) await speaker.play(blob);
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
  function toast(text) {
    const el = $('#toast');
    el.textContent = text;
    el.classList.add('on');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => el.classList.remove('on'), 3200);
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

    S.items = [];
    openAI = [];
    toolOwner = {};
    cardOwner = {};
    lastAI = null;
    replaying = true;
    (st.events || []).forEach(apply);
    replaying = false;
    if (S.agentState === 'idle') finishOpen();
    renderAllItems();
    renderStatus();
    renderDrawer();
    scrollToBottom(false);
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
        append({ kind: 'user', text: e.text || '', origin, images: e.images || [], files: e.files || [], ts: e.ts });
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
          update(id, it => { it.kind = 'ai'; it.text = text; it.streaming = false; it.local = loc; it.meta = null; it.ts = e.ts; });
          lastAI = id;
        } else {
          lastAI = append({ kind: 'ai', text, streaming: false, local: loc, meta: null, ts: e.ts }).id;
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
        cardOwner[card.id] = append({ kind: 'permission', card }).id;
        if (!replaying) haptic('alert');
        return true;
      }
      case 'question': {
        const qs = (e.questions || []).map(q => ({
          question: q.question || '', multi: !!q.multiSelect,
          options: (q.options || []).map(o => ({ label: o.label || '', desc: o.description || '' })),
        }));
        const card = { id: e.id || '', questions: qs, closed: null };
        cardOwner[card.id] = append({ kind: 'question', card }).id;
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
          images: e.images || [], files: e.files || [], buttons, btnState: buttons && buttons.length ? { busy: false, doneIndex: null } : null, ts: e.ts });
        lastAI = null;
        return false;
      }
      case 'note':
        append({ kind: 'note', text: e.text || '' });
        return false;
      case 'error':
        append({ kind: 'error', text: e.text || '' });
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
    const ts = raw.ts;
    switch (raw.type) {
      case 'user':
        return { kind: 'user', text: raw.text || '', origin: raw.origin || 'typed', images: raw.images || [], files: raw.files || [], ts };
      case 'text':
        return { kind: 'ai', text: raw.text || '', streaming: false, local: false, meta: null, ts };
      case 'incoming': {
        const kind = raw.kind || '';
        const buttons = raw.buttons || null;
        return { kind: 'incoming', source: INCOMING_SOURCE(kind, raw.from), text: raw.text || '',
          auto: kind === 'auto' || kind === 'system', out: kind === 'phone_out',
          images: raw.images || [], files: raw.files || [], buttons, btnState: buttons && buttons.length ? { busy: false, doneIndex: null } : null, ts };
      }
      case 'note':
        return { kind: 'note', text: raw.text || '', ts };
      case 'error':
        return { kind: 'error', text: raw.text || '', ts };
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
    } catch (e) {
      historyState = 'idle';
      show(e);
    }
    renderHistoryBtn();
  }

  // ── 화면 도구 ──────────────────────────
  const $ = sel => document.querySelector(sel);

  function h(tag, attrs) {
    const el = document.createElement(tag);
    if (attrs) {
      Object.keys(attrs).forEach(k => {
        const v = attrs[k];
        if (v == null || v === false) return;
        if (k === 'class') el.className = v;
        else if (k === 'text') el.textContent = v;
        else if (k === 'html') el.innerHTML = v;
        else if (k.startsWith('on')) el.addEventListener(k.slice(2), v);
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
    folder: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7a2 2 0 0 1 2-2h4l2 2h6a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7z"/></svg>',
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
    switch (it.kind) {
      case 'user': return userBubble(it);
      case 'ai': return aiMessage(it);
      case 'tools': return h('div', { class: 'tools' }, it.list.map(toolRow));
      case 'permission': return permissionCard(it.card);
      case 'question': return questionCard(it.card);
      case 'incoming':
        return h('div', { class: 'inbox' + (it.auto ? ' auto' : '') + (it.out ? ' out' : '') },
          h('b', { text: it.source }), it.text ? h('div', { text: it.text }) : null, attachments(it), stamp(it), buttonRow(it));
      case 'note': return h('div', { class: 'note', text: it.text });
      case 'error': return h('div', { class: 'err', text: it.text });
      default: return h('div');
    }
  }

  function userBubble(it) {
    // 이 폰(앱)에서 보낸 건 표시하지 않고, 다른 곳에서 온 것만 어디서인지 붙인다
    const via = { typed: ['PC 에서', 'pc'], voice: ['PC 에서 말로', 'micSm'], phone: ['텔레그램', 'phone'] }[it.origin];
    return h('div', { class: 'u-row' },
      h('div', { class: 'u-bubble' }, via ? h('div', { class: 'via' }, svg(via[1]), via[0]) : null, document.createTextNode(it.text), attachments(it), stamp(it)));
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

  /** 알림에 달린 버튼(제안 승인·메일 보내기 등) — 누르면 그 항목의 버튼을 모두 잠근다 */
  function buttonRow(it) {
    if (!it.buttons || !it.buttons.length) return null;
    const st = it.btnState;
    return h('div', { class: 'nbtns' }, it.buttons.map(([label, data], i) => {
      const done = st.doneIndex === i;
      return h('button', {
        class: 'nbtn' + (done ? ' done' : ''),
        disabled: st.busy || st.doneIndex != null,
        text: done ? '✓ 눌렀어요' : label,
        onclick: () => pressButton(it, i, data),
      });
    }));
  }

  async function pressButton(it, i, data) {
    const st = it.btnState;
    if (!st || st.busy || st.doneIndex != null) return;
    st.busy = true;
    paint(it);
    try {
      await api.post('button', { data });      // 서버는 {data: '…'} 를 받는다
      st.doneIndex = i;
    } catch (e) {
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
    const body = it.streaming ? h('div', { class: 'ai-body stream', text: it.text + ' ▍' }) : h('div', { class: 'ai-body md', html: markdown(it.text) });
    return h('div', { class: 'ai' }, head, body, it.meta ? h('div', { class: 'meta', text: it.meta }) : null);
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

  function scrollToBottom(smooth) {
    if (!chatEl) return;
    requestAnimationFrame(() => {
      chatEl.scrollTo({ top: chatEl.scrollHeight, behavior: smooth ? 'smooth' : 'auto' });
      if (jumpEl) jumpEl.hidden = true;
    });
  }

  function afterChange(stick, changed) {
    if (stick) scrollToBottom(true);
    else if (changed && jumpEl) jumpEl.hidden = false;
  }

  // ── 제목줄 · 상태 ──────────────────────
  function orbState() {
    if (recorder.recording) return 'recording';
    if (speaker.speaking) return 'speaking';
    if (offlineMessage()) return 'offline';
    return S.agentState;
  }

  function statusText() {
    if (recorder.recording) return '듣고 있어요 — 다시 누르면 보내요';
    if (S.uploadingVoice) return '받아적는 중';
    if (speaker.speaking) return '말하는 중';
    const st = { thinking: '생각하는 중', working: '일하는 중', waiting: '대표님 허락을 기다려요' }[S.agentState];
    if (st) return st;
    if (S.link.k === 'online') return S.host === 'brain' ? 'PC 뒤의 두뇌와 연결됨' : 'PC 창과 연결됨';
    return S.link.k === 'connecting' ? '연결하는 중' : 'PC 에 닿지 않아요';
  }

  const modelLabel = () => (S.models.find(m => m.key === S.model) || {}).label || '보통';

  function renderStatus() {
    const main = $('#main');
    if (!main) return;
    main.querySelector('.bar .orb').dataset.state = orbState();
    main.querySelector('.who b').textContent = S.name;
    main.querySelector('.who small').textContent = statusText();
    main.querySelector('.pill').textContent = modelLabel();
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
    renderSend();
    renderDrawerFooter();
  }

  function renderLevel() {
    const lvl = String(recorder.level.toFixed(3));
    document.querySelectorAll('.bar .orb, .mic').forEach(el => el.style.setProperty('--lvl', lvl));
  }

  function renderSend() {
    const main = $('#main');
    if (!main) return;
    const ta = main.querySelector('textarea');
    const btn = main.querySelector('.send');
    const empty = !ta.value.trim();
    const stopMode = busy() && empty;
    btn.className = 'round send' + (stopMode ? ' stop' : '');
    btn.disabled = !stopMode && empty;
    btn.innerHTML = '';
    btn.append(svg(stopMode ? 'stop' : 'up'));
    btn.setAttribute('aria-label', stopMode ? '멈추기' : '보내기');
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
      rows: '1', placeholder: '무엇이든 시키세요', 'aria-label': '효딩쓰에게 보낼 말', enterkeyhint: 'enter',
      oninput: () => { grow(ta); renderSend(); },
    });
    const sendBtn = h('button', {
      class: 'round send',
      onclick: () => {
        if (busy() && !ta.value.trim()) { stop(); return; }
        send(ta.value);
        ta.value = '';
        grow(ta);
        renderSend();
      },
    });
    const mic = h('button', { class: 'round mic', onclick: toggleRecording });

    chatEl = h('div', { class: 'chat', onscroll: () => {
      if (nearBottom() && jumpEl) jumpEl.hidden = true;
      if (chatEl.scrollTop <= 24) loadMoreHistory();
    } });
    listEl = h('div', { class: 'items' });
    helloEl = helloView();
    historyBtnEl = h('button', { class: 'hist-btn', onclick: loadMoreHistory, text: '지난 대화 보기' });
    renderHistoryBtn();
    const pullEl = h('div', { class: 'pull' });
    chatEl.append(pullEl, h('div', { class: 'chat-inner' }, historyBtnEl, helloEl, listEl));
    jumpEl = h('button', { class: 'jump', hidden: true, 'aria-label': '맨 아래로', onclick: () => scrollToBottom(true) }, svg('down'));
    pullToRefresh(chatEl, pullEl);

    const bar = h('div', { class: 'bar' },
      h('button', { class: 'icon', 'aria-label': '메뉴 — 대화 상대 고르기', onclick: openDrawer, html: ICON.menu }),
      orb(orbState(), 26),
      h('div', { class: 'who' }, h('b'), h('small')),
      h('button', { class: 'pill', 'aria-label': '생각의 깊이', onclick: ev => modelMenu(ev.currentTarget) }));

    app.append(h('div', { class: 'screen', id: 'main' },
      bar,
      h('div', { class: 'banner', hidden: true }, h('i', { class: 'spinner' }), h('span')),
      h('div', { class: 'chat-wrap' }, chatEl, jumpEl),
      h('div', { class: 'composer' },
        h('div', { class: 'chips' }, QUICK.map(([t, say]) => h('button', { class: 'chip', text: t, onclick: () => send(say) }))),
        h('div', { class: 'row' }, ta, mic, sendBtn))),
      h('div', { class: 'toast', id: 'toast', role: 'status', 'aria-live': 'polite' }));

    renderAllItems();
    renderStatus();
    scrollToBottom(false);
  }

  function helloView() {
    return h('div', { class: 'hello' },
      orb('idle', 68),
      h('h2', { text: '대표님, 부르시면 바로 옵니다' }),
      h('p', { html: '아래에 적거나 마이크를 눌러 말씀하세요.<br>PC 의 효딩쓰와 같은 대화예요.' }),
      h('div', { class: 'list' }, SUGGEST.map(s => h('button', { class: 'suggest', text: s, onclick: () => send(s) }))));
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

  function modelMenu(anchor) {
    const r = anchor.getBoundingClientRect();
    const menu = h('div', { class: 'menu', role: 'menu', style: 'top:' + (r.bottom + 6) + 'px;right:' + Math.max(8, innerWidth - r.right) + 'px' },
      h('h4', { text: '생각의 깊이' }),
      S.models.map(m => h('button', { role: 'menuitemradio', 'aria-checked': m.key === S.model ? 'true' : 'false', onclick: () => { close(); setModel(m.key); } },
        h('i', { text: m.key === S.model ? '✓' : '' }), m.label)));
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
      drawerRow('문서함', '영상 · 사진 · 문서 · 음성', false, h('span', { class: 'badge-ic' }, svg('folder')), () => { close(); openDocs(); }),
      drawerRow('설정', '목소리 · 말 빠르기 · 화면 · 연결', false, h('span', { class: 'badge-ic' }, svg('gear')), () => { close(); openSettings(); }),
      h('div', { class: 'dsep' }),
      drawerRow(S.name, '비서실장 · 지금 이 대화', true, orb('idle', 26), close),
      h('div', { class: 'sec', text: 'AI OFFICE 직원' }),
      S.workers.length ? null : h('div', { class: 'dnote', text: '명단을 불러오는 중이거나 AI Office 가 꺼져 있어요.' }),
      S.workers.map(w => drawerRow(w.role ? w.name + ' · ' + w.role : w.name, w.statusLine, false, avatar(w, 32), () => { close(); openWorker(w); })),
      h('div', { class: 'sec', text: '더 보기' }),
      drawerRow('새 대화', '효딩쓰와 처음부터 다시', false, h('span', { class: 'badge-ic' }, svg('pencil')), () => { newChat(); close(); }));
    d.append(
      h('header', null, h('h2', { text: '대화 상대' }), h('button', { 'aria-label': '닫기', onclick: close, html: ICON.close })),
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
        h('button', { class: 'icon', 'aria-label': opts.close ? '닫기' : '뒤로', onclick: () => popPage(), html: opts.close ? ICON.close : ICON.back }),
        h('div', { class: 'who' }, h('b', { text: title })),
        h('span', { class: 'spacer' })),
      content);
    page.onClose = onClose;
    pages.push(page);
    $('#app').append(page);
    requestAnimationFrame(() => requestAnimationFrame(() => page.classList.add('on')));
    return page;
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
    const ta = h('textarea', { rows: '1', placeholder: w.name + ' 님에게 지시', 'aria-label': w.name + ' 님에게 지시', oninput: () => { grow(ta); sync(); } });
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
    let kind = '', q = '', items = [], more = false, total = 0, loading = false, loadedOnce = false, alive = true, searchTimer = 0;

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

    function docRow(it) {
      const files = it.files || [];
      return h('div', { class: 'drow2', onclick: () => rowTap(it) },
        it.thumb ? h('img', { class: 'dthumb', src: it.thumb, alt: '', loading: 'lazy' }) : h('div', { class: 'dthumb ic', text: DOC_ICON[it.kind] || '📄' }),
        h('div', { class: 'dtx' },
          h('b', { text: it.title || '(제목 없음)' }),
          h('small', { text: timeOnly(it.ts) + (countLabel(it) ? ' · ' + countLabel(it) : '') })),
        files.length > 1 ? svg('chev') : null);
    }

    /** 항목의 파일 전부를 앱 안 보기 화면으로 — 첫 장부터 */
    function rowTap(it) {
      const files = (it.files || []).filter(f => f && f.url);
      if (!files.length) return;
      openViewer(files.map(f => ({ url: f.url, name: f.name || '', kind: fileKind(f.name, f.url, it.kind) })), 0, it.title || '');
    }

    function draw() {
      totalEl.textContent = loadedOnce ? '전체 ' + total + '개' : '';
      listEl2.textContent = '';
      if (loading && !items.length) { listEl2.append(h('div', { class: 'empty' }, h('i', { class: 'spinner', style: 'display:inline-block' }))); return; }
      if (!items.length) { listEl2.append(h('div', { class: 'doc-empty', text: '아직 자료가 없어요 — 효딩쓰가 만든 영상·카드뉴스·캡처가 여기 모입니다' })); return; }
      let lastDay = null;
      items.forEach(it => {
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

  // ── 사진·영상 보기 (앱 안에서 — 2026-10-04) ──
  const EXT_KIND = [['image', /\.(jpe?g|png|gif|webp|avif|heic|heif|bmp|svg)$/i], ['video', /\.(mp4|m4v|mov|webm|mkv)$/i],
    ['audio', /\.(mp3|m4a|aac|wav|ogg|oga|opus|flac)$/i], ['doc', /\.(html?|md|markdown|pdf|txt|csv|json)$/i]];

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

  function viewerSlide(e) {
    const kind = e.kind || fileKind(e.name, e.url, '');
    if (kind === 'image') {
      const img = h('img', { src: e.url, alt: e.name || '', loading: 'lazy', draggable: 'false' });
      img.addEventListener('error', () => img.replaceWith(h('div', { class: 'vw-msg', text: '사진을 불러오지 못했어요' })));
      return h('div', { class: 'vw-slide' }, img);
    }
    if (kind === 'video') return h('div', { class: 'vw-slide' }, h('video', { src: e.url, controls: true, playsinline: true, 'webkit-playsinline': true, preload: 'metadata' }));
    if (kind === 'audio') {
      return h('div', { class: 'vw-slide' }, h('div', { class: 'vw-audio' },
        h('div', { class: 'vw-msg', text: '🔊 ' + (e.name || '음성') }), h('audio', { src: e.url, controls: true, preload: 'metadata' })));
    }
    // 문서 — 처음 볼 때 주소를 넣는다(넘기지 않은 문서는 받지 않음)
    // sandbox — 같은 woopang.com 이라도 문서 속 스크립트가 앱의 기기 토큰(localStorage)에 못 닿게 allow-same-origin 은 뺀다(10/4)
    return h('div', { class: 'vw-slide' }, h('div', { class: 'vw-doc' }, h('iframe', { 'data-src': e.url, title: e.name || '문서', referrerpolicy: 'no-referrer',
      sandbox: 'allow-scripts allow-popups allow-popups-to-escape-sandbox allow-downloads' })));
  }

  /** 전체 화면 보기 — entries: [{url, name, kind}], start: 처음 보일 장 */
  function openViewer(entries, start, title) {
    const list = (entries || []).filter(e => e && e.url);
    if (!list.length) return;
    let idx = Math.max(0, Math.min(start || 0, list.length - 1)), raf = 0;
    const many = list.length > 1;

    const counter = h('div', { class: 'vw-count', 'aria-live': 'polite' });
    const nameEl = h('span', { class: 'vw-name' });
    const track = h('div', { class: 'vw-track' }, list.map(viewerSlide));
    const prev = many ? h('button', { class: 'vw-arrow prev', 'aria-label': '앞 장', html: ICON.back, onclick: () => go(idx - 1) }) : null;
    const next = many ? h('button', { class: 'vw-arrow next', 'aria-label': '다음 장', html: ICON.back, onclick: () => go(idx + 1) }) : null;
    const el = h('div', { class: 'viewer', role: 'dialog', 'aria-modal': 'true', 'aria-label': title || '보기' },
      h('div', { class: 'vw-top' },
        h('button', { class: 'vw-btn', 'aria-label': '닫기', html: ICON.close, onclick: () => close() }),
        counter,
        h('button', { class: 'vw-btn text', text: '공유', onclick: share })),
      h('div', { class: 'vw-stage' }, track, prev, next),
      h('div', { class: 'vw-foot' }, nameEl, h('button', { class: 'vw-open', text: '브라우저로 열기', onclick: () => openLink(list[idx].url) })));

    function sync() {
      counter.textContent = many ? (idx + 1) + ' / ' + list.length : '';
      nameEl.textContent = list[idx].name || title || '';
      if (prev) prev.hidden = idx === 0;
      if (next) next.hidden = idx === list.length - 1;
      Array.from(track.children).forEach((s, i) => {
        if (i !== idx) s.querySelectorAll('video,audio').forEach(m => { try { m.pause(); } catch (e) { /* 이미 멈춤 */ } });
        const f = s.querySelector('iframe[data-src]');
        if (f && i === idx) { f.src = f.dataset.src; f.removeAttribute('data-src'); }
      });
    }

    function go(i) {
      i = Math.max(0, Math.min(i, list.length - 1));
      track.scrollTo({ left: i * track.clientWidth, behavior: 'smooth' });
    }

    function jump() {
      track.scrollLeft = idx * track.clientWidth;
    }

    track.addEventListener('scroll', () => {
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(() => {
        const w = track.clientWidth || 1;
        const i = Math.max(0, Math.min(Math.round(track.scrollLeft / w), list.length - 1));
        if (i !== idx) { idx = i; sync(); }
      });
    }, { passive: true });

    async function share() {
      const e = list[idx];
      const data = { url: e.url, title: e.name || title || '효딩쓰' };
      if (navigator.share) {
        try { await navigator.share(data); return; } catch (err) { if (err && err.name === 'AbortError') return; }
      }
      openLink(e.url);
    }

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
      section('효딩쓰', [row('생각의 깊이', sel(S.models.map(m => [m.key, m.label]), S.model, setModel, '생각의 깊이'))],
        '모델은 PC 효딩쓰에서 정합니다. 깊게 생각할수록 구독 사용량을 많이 씁니다.'),
      section('화면', [h('div', { class: 'frow col' }, themeSeg)], null),
      section('연결', [
        row('붙은 곳', h('span', { class: 'v', text: S.host === 'brain' ? 'PC 뒤의 두뇌' : 'PC 효딩쓰 창' })),
        row('서버', h('span', { class: 'v', style: 'font-size:12px', text: api.base })),
        h('div', { class: 'frow' }, h('button', { class: 'link danger', text: '이 폰 연결 끊기', onclick: confirmUnpair })),
      ], '끊으면 이 폰의 열쇠가 PC 에서도 지워집니다. 다시 쓰려면 PC 창 ☰ 에서 새 코드를 받으세요.'),
      section(null, [h('div', { class: 'frow' }, h('span', { class: 'k', style: 'font-size:13px;color:var(--ink-3)', text: '효딩쓰 ' + (appVersion || '') + (Native ? ' · 안드로이드' : ' · 웹') + ' · 화면 ' + WEB_VERSION + ' · QUE. ENT · 대표님 개인용' }))], null)));
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

  document.addEventListener('click', ev => {
    const a = ev.target.closest && ev.target.closest('a[href]');
    if (!a) return;
    ev.preventDefault();
    if (a.dataset.doc) openDoc(a.dataset.doc);
    else openLink(a.getAttribute('href'));
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

  applyTheme();
  S.paired = !!api.token;
  if (S.paired) { showMain(); startPolling(); } else { showPair(); }
})();
