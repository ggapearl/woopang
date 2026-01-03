from flask import Flask, render_template, request, jsonify, Response, make_response
import subprocess
import os
import threading
import time
import sys
from collections import deque
import logging
import json
import uuid

app = Flask(__name__)
log = logging.getLogger('werkzeug')
log.setLevel(logging.ERROR)

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
LOGS_DIR = os.path.join(BASE_DIR, "logs")
SESSIONS_FILE = os.path.join(BASE_DIR, "sessions.json")
os.makedirs(LOGS_DIR, exist_ok=True)

sessions = {}
session_lock = threading.Lock()

class Session:
    def __init__(self, session_id, name="새 대화"):
        self.id = session_id
        self.name = name
        self.proc = None
        self.is_running = False
        self.log_buffer = deque(maxlen=3000)
        self.log_id_counter = 0
        self.lock = threading.Lock()
        self.log_path = os.path.join(LOGS_DIR, f"{self.id}.log")
        self.load_history()

    def load_history(self):
        if os.path.exists(self.log_path):
            try:
                with open(self.log_path, 'r', encoding='utf-8') as f:
                    for line in f:
                        self.log_id_counter += 1
                        self.log_buffer.append({'id': self.log_id_counter, 'text': line})
            except: pass

    def add_log(self, text):
        with self.lock:
            self.log_id_counter += 1
            self.log_buffer.append({'id': self.log_id_counter, 'text': text})
            try:
                with open(self.log_path, 'a', encoding='utf-8') as f: f.write(text)
            except: pass

    def start(self):
        if self.proc and self.proc.poll() is None: return
        env = os.environ.copy()
        env["PYTHONUNBUFFERED"] = "1"
        try:
            self.proc = subprocess.Popen(
                ['python', '-u', os.path.join(BASE_DIR, 'my_gemini.py'), '--history', self.log_path],
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                cwd=r"C:\woopang", shell=False, env=env, bufsize=0
            )
            self.is_running = True
            threading.Thread(target=self.read_output, daemon=True).start()
        except Exception as e:
            self.add_log(f"실행 실패: {e}\n")

    def read_output(self):
        try:
            while self.proc.poll() is None:
                line = self.proc.stdout.readline()
                if not line: break
                try: decoded = line.decode('utf-8', errors='replace')
                except: decoded = line.decode('cp949', errors='replace')
                self.add_log(decoded)
        except: pass
        finally: self.is_running = False

    def send(self, cmd):
        if cmd == 'cancel':
            if self.proc: self.proc.stdin.write(b'\x03'); self.proc.stdin.flush()
            return 'cancelled'
        if not self.is_running: self.start(); time.sleep(0.5)
        try:
            self.proc.stdin.write((cmd + "\n").encode('utf-8'))
            self.proc.stdin.flush()
            return 'sent'
        except: return 'error'

def save_meta():
    with session_lock:
        data = {sid: s.name for sid, s in sessions.items()}
        with open(SESSIONS_FILE, 'w', encoding='utf-8') as f: json.dump(data, f, ensure_ascii=False)

def load_meta():
    if os.path.exists(SESSIONS_FILE):
        try:
            with open(SESSIONS_FILE, 'r', encoding='utf-8') as f:
                data = json.load(f)
                for sid, name in data.items(): sessions[sid] = Session(sid, name)
        except: pass

@app.route('/')
def index():
    resp = make_response(render_template('web_cmd.html'))
    resp.headers['Cache-Control'] = 'no-cache, no-store, must-revalidate'
    resp.headers['Pragma'] = 'no-cache'
    resp.headers['Expires'] = '0'
    return resp

@app.route('/api/sessions', methods=['GET', 'POST'])
def handle_sessions():
    if request.method == 'POST':
        sid = str(uuid.uuid4())[:8]
        with session_lock: sessions[sid] = Session(sid); save_meta()
        return jsonify({'id': sid, 'name': sessions[sid].name})
    return jsonify([{'id': s.id, 'name': s.name} for s in sessions.values()])

@app.route('/api/sessions/<sid>', methods=['PATCH', 'DELETE'])
def manage_session(sid):
    if sid not in sessions: return "Not Found", 404
    if request.method == 'DELETE':
        with session_lock:
            s = sessions.pop(sid)
            if s.proc: s.proc.terminate()
            if os.path.exists(s.log_path): os.remove(s.log_path)
            save_meta()
        return jsonify({'status': 'ok'})
    sessions[sid].name = request.json.get('name', sessions[sid].name)
    save_meta(); return jsonify({'status': 'ok'})

@app.route('/api/logs')
def get_logs():
    sid = request.args.get('sid', 'default')
    last_id = request.args.get('last_id', -1, type=int)
    if sid not in sessions:
        with session_lock: sessions[sid] = Session(sid); save_meta()
    s = sessions[sid]
    if not s.is_running: s.start()
    with s.lock: new_logs = [l for l in s.log_buffer if l['id'] > last_id]
    return jsonify({'logs': new_logs, 'running': s.is_running})

@app.route('/api/input', methods=['POST'])
def send_input():
    data = request.json
    sid = data.get('sid', 'default')
    return jsonify({'status': sessions.get(sid).send(data.get('command'))})

if __name__ == '__main__':
    load_meta()
    app.run(host='0.0.0.0', port=5030, debug=False)
