"""효딩쓰 휴대폰 화면 시험대 (2026-09-28) — web/ 을 디스크에서 내주고, API 는 mock 또는 실제 woopang.com 으로.
  python harness.py [port]
  mock: 요청 헤더 Authorization 이 'Bearer mock' 이면 흉내 낸 대화로 답한다.
"""
import json
import mimetypes
import os
import sys
import threading
import time
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

WEB = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'web'))
UPSTREAM = 'https://woopang.com/AI/api/desk/'
LOG = []

WORKERS = [
    {'id': 'hyo_ding', 'name': '김효딩', 'team_label': '비서실', 'role': '비서실장', 'color': '#E91E63', 'task': '보고서 정리', 'busy': True},
    {'id': 'dev_park', 'name': '박개발', 'team_label': '개발팀', 'role': '백엔드', 'color': '#2E4FCB', 'task': '', 'busy': False},
]
MD = ('서버 상태 확인했습니다.\n\n## 요약\n- **8080** 메인 · 정상\n- `6688` 농민닷컴 · 정상\n- 문서: https://woopang.com/AI/doc/test?e=1&s=abc에서 보세요\n\n'
      '```\nGet-NetTCPConnection -State Listen\n```\n자세한 건 [대시보드](https://woopang.com/bookmark) 참고.')


class Mock:
    def __init__(self):
        self.lock = threading.Lock()
        self.pending = []
        self.seq = 3
        self.calls = 0

    def state(self):
        return {'name': '효딩쓰', 'host': 'gui', 'boot': 'mock1', 'seq': 3, 'state': 'idle', 'model': 'opus',
                'models': [{'key': 'opus', 'label': 'Opus 5.5'}], 'speed': 1.2, 'office': WORKERS,
                'events': [{'type': 'user', 'text': 'PC 에서 친 말', 'origin': 'typed'},
                           {'type': 'text_start'}, {'type': 'text', 'text': '지난 대화의 답입니다.'},
                           {'type': 'result', 'ms': 2100, 'steps': 1}]}

    def events(self, since):
        with self.lock:
            self.calls += 1
            if self.calls == 1:
                evs = [
                    {'type': 'user', 'text': '서버 상태 어때?', 'origin': 'app'},
                    {'type': 'state', 'state': 'thinking'},
                    {'type': 'text_start'}, {'type': 'delta', 'text': '확인해 '}, {'type': 'delta', 'text': '볼게요'},
                    {'type': 'tool', 'id': 't1', 'label': 'Bash', 'detail': 'Get-NetTCPConnection -State Listen | Where-Object LocalPort -in 8080,6688,8000,47834'},
                    {'type': 'tool', 'id': 't2', 'label': 'Read', 'detail': 'C:\\woopang\\server\\logs\\ai_office.log', 'sub': True},
                    {'type': 'tool_done', 'id': 't1', 'ok': True}, {'type': 'tool_done', 'id': 't2', 'ok': False},
                    {'type': 'text', 'text': '확인해 볼게요'},
                    {'type': 'text_start'}, {'type': 'delta', 'text': '서버 상태 '},
                    {'type': 'text', 'text': MD},
                    {'type': 'result', 'ms': 5320, 'steps': 3},
                    {'type': 'state', 'state': 'waiting'},
                    {'type': 'permission', 'id': 'p1', 'title': '명령 실행: 서버 재시작', 'note': 'AI Office 를 다시 켭니다',
                     'body': 'python ai_office_server.py\n' * 12, 'can_always': True},
                    {'type': 'question', 'id': 'q1', 'questions': [{'question': '어느 쪽으로 할까요?', 'multiSelect': False, 'options': [
                        {'label': '지금', 'description': '바로 재시작'}, {'label': '밤에', 'description': '23시 이후'}]}]},
                    {'type': 'incoming', 'kind': 'auto', 'text': '09:00 아침 점검 — 이상 없음'},
                    {'type': 'incoming', 'kind': 'peer', 'from': '농민닷컴 세션', 'text': '카드뉴스 수정본 넘깁니다'},
                    {'type': 'note', 'text': '대화를 정리했습니다'},
                    {'type': 'error', 'text': '한도에 걸렸습니다 — 19:00 에 풀립니다'},
                ]
            else:
                evs = list(self.pending)
                self.pending.clear()
        if not evs:
            time.sleep(1.5)
        with self.lock:
            out = []
            for e in evs:
                self.seq += 1
                out.append(dict(e, seq=self.seq))
            return {'boot': 'mock1', 'seq': self.seq, 'state': 'idle' if self.calls > 1 else 'waiting', 'events': out}

    def post(self, sub, data):
        LOG.append((sub, data))
        with self.lock:
            if sub == 'decide':
                self.pending.append({'type': 'permission_closed', 'id': data.get('id'), 'decision': data.get('decision')})
            elif sub == 'answer':
                self.pending.append({'type': 'permission_closed', 'id': data.get('id'), 'decision': 'answer'})
            elif sub == 'send':
                self.pending += [{'type': 'user', 'text': data.get('text'), 'origin': 'app'}, {'type': 'text_start'},
                                 {'type': 'text', 'text': '받았습니다: ' + str(data.get('text'))}, {'type': 'result', 'ms': 900}]
            elif sub == 'new':
                self.pending.append({'type': 'cleared'})
        return {'ok': True}


MOCK = Mock()


class H(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'

    def log_message(self, *a):
        pass

    def out(self, code, body, ctype):
        self.send_response(code)
        self.send_header('Content-Type', ctype)
        self.send_header('Content-Length', str(len(body)))
        self.send_header('Cache-Control', 'no-store')
        self.end_headers()
        self.wfile.write(body)

    def js(self, code, obj):
        self.out(code, json.dumps(obj, ensure_ascii=False).encode('utf-8'), 'application/json; charset=utf-8')

    def do_GET(self):
        self.route('GET')

    def do_POST(self):
        self.route('POST')

    def route(self, method):
        u = urlparse(self.path)
        n = int(self.headers.get('Content-Length') or 0)
        raw = self.rfile.read(n) if n else b''
        if u.path.startswith('/AI/desk-app/'):
            rel = u.path[len('/AI/desk-app/'):] or 'index.html'
            fp = os.path.normpath(os.path.join(WEB, rel))
            if not fp.startswith(WEB) or not os.path.isfile(fp):
                return self.out(404, b'no', 'text/plain')
            with open(fp, 'rb') as f:
                return self.out(200, f.read(), mimetypes.guess_type(fp)[0] or 'application/octet-stream')
        if not u.path.startswith('/AI/api/desk/'):
            return self.out(404, b'no', 'text/plain')
        sub = u.path[len('/AI/api/desk/'):]
        auth = self.headers.get('Authorization') or ''
        if auth == 'Bearer mock':
            q = parse_qs(u.query)
            data = json.loads(raw or b'{}') if 'json' in (self.headers.get('Content-Type') or '') and raw else {}
            if sub == 'state':
                return self.js(200, MOCK.state())
            if sub == 'events':
                return self.js(200, MOCK.events(int((q.get('since') or ['0'])[0])))
            if sub == 'office/workers':
                return self.js(200, {'workers': WORKERS})
            if sub == 'office/history':
                return self.js(200, {'messages': [{'sender': 'ceo', 'sender_name': '대표', 'content': '오늘 보고 부탁해', 'timestamp': '09:01'},
                                                  {'sender': 'hyo_ding', 'sender_name': '김효딩', 'content': '**오늘 보고**\n- 승인 대기 2건', 'timestamp': '09:02'}]})
            if sub == 'tts':
                return self.js(503, {'error': '목소리를 만들지 못했습니다'})
            if sub == 'voice':
                LOG.append(('voice', {'bytes': len(raw), 'ctype': self.headers.get('Content-Type')}))
                return self.js(200, {'text': ''})
            if method == 'POST':
                return self.js(200, MOCK.post(sub, data))
            return self.js(404, {'error': '없는 주소입니다'})
        if sub == '_log':
            return self.js(200, {'log': LOG})
        headers = {k: self.headers[k] for k in ('Authorization', 'Content-Type') if self.headers.get(k)}
        headers['User-Agent'] = 'hyoding-harness'
        url = UPSTREAM + sub + (('?' + u.query) if u.query else '')
        try:
            with urllib.request.urlopen(urllib.request.Request(url, data=raw if method == 'POST' else None, headers=headers, method=method), timeout=60) as r:
                return self.out(r.status, r.read(), r.headers.get('Content-Type') or 'application/json')
        except urllib.error.HTTPError as e:
            return self.out(e.code, e.read(), e.headers.get('Content-Type') or 'application/json')
        except Exception as e:
            return self.js(502, {'error': 'harness: %s' % e})


if __name__ == '__main__':
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8765
    ThreadingHTTPServer(('127.0.0.1', port), H).serve_forever()
