from flask import Flask, render_template, request, jsonify
import subprocess
import os
import threading
import time
import sys
from collections import deque
import logging
import signal

import json

app = Flask(__name__)
log = logging.getLogger('werkzeug')
log.setLevel(logging.ERROR)

CURRENT_DIR = r"C:\woopang"
SESSIONS_FILE = os.path.join(os.path.dirname(__file__), "sessions.json")
LOGS_DIR = os.path.join(os.path.dirname(__file__), "logs")
os.makedirs(LOGS_DIR, exist_ok=True)

# Session management
sessions = {}
session_lock = threading.Lock()

class Session:
    def __init__(self, session_id, name="New Chat"):
        self.id = session_id
        self.name = name
        self.proc = None
        self.is_running = False
        self.log_buffer = deque(maxlen=2000)
        self.log_id_counter = 0
        self.lock = threading.Lock()
        self.log_path = os.path.join(LOGS_DIR, f"{self.id}.log")
        self.load_log_from_file()

    def load_log_from_file(self):
        if os.path.exists(self.log_path):
            try:
                with open(self.log_path, 'r', encoding='utf-8') as f:
                    lines = f.readlines()
                    for line in lines:
                        self.log_id_counter += 1
                        self.log_buffer.append({'id': self.log_id_counter, 'text': line})
            except: pass

    def add_log(self, text):
        with self.lock:
            self.log_id_counter += 1
            self.log_buffer.append({'id': self.log_id_counter, 'text': text})
            # Append to file for persistence
            try:
                with open(self.log_path, 'a', encoding='utf-8') as f:
                    f.write(text)
            except: pass

    def start(self):
        if self.proc and self.proc.poll() is None:
            try: self.proc.terminate()
            except: pass
        
        env = os.environ.copy()
        env["PYTHONUNBUFFERED"] = "1"
        try:
            # Pass history path to Gemini agent
            cmd = ['python', '-u', r'server\qcli\my_gemini.py', '--history', self.log_path]
            self.proc = subprocess.Popen(
                cmd,
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                cwd=CURRENT_DIR, shell=False, env=env, bufsize=0
            )
            self.is_running = True
            t = threading.Thread(target=self.read_output)
            t.daemon = True
            t.start()
        except Exception as e:
            self.add_log(f"Failed to start: {e}\n")

    def read_output(self):
        try:
            while self.proc.poll() is None:
                line = self.proc.stdout.readline()
                if not line: break
                try: decoded_line = line.decode('utf-8', errors='strict')
                except:
                    try: decoded_line = line.decode('cp949', errors='replace')
                    except: decoded_line = line.decode('utf-8', errors='replace')
                
                # Check if it's a marker we don't want to double-log or just pass it
                # For simplicity, we log everything the agent outputs back to the file
                self.add_log(decoded_line)
        except: pass
        finally:
            self.is_running = False
            # Removed [System: Connection Closed] as requested
            # self.add_log("\n[System: Connection Closed]\n")

    def send_input(self, command):
        if command == 'restart_shell':
            if os.path.exists(self.log_path): os.remove(self.log_path)
            with self.lock:
                self.log_buffer.clear()
                self.log_id_counter = 0
            self.start()
            return 'restarted'
        
        if command == 'cancel':
            if self.proc and self.proc.poll() is None:
                self.proc.stdin.write(b'\x03')
                self.proc.stdin.flush()
                return 'cancelled'
            return 'not_running'

        if self.proc and self.proc.poll() is None:
            try:
                cmd_bytes = (command + "\n").encode('utf-8')
                self.proc.stdin.write(cmd_bytes)
                self.proc.stdin.flush()
                return 'sent'
            except:
                self.start()
                return 'error'
        else:
            self.start()
            # If it was restarted, we need to send the command again after startup?
            # For now, let's just re-send
            time.sleep(1) # Wait for start
            if self.proc and self.proc.poll() is None:
                try:
                    cmd_bytes = (command + "\n").encode('utf-8')
                    self.proc.stdin.write(cmd_bytes)
                    self.proc.stdin.flush()
                    return 'sent'
                except: pass
            return 'restarted'

    def to_dict(self):
        return {
            'id': self.id,
            'name': self.name,
            'running': self.is_running
        }

def save_sessions_to_disk():
    with session_lock:
        data = {sid: s.name for sid, s in sessions.items()}
    try:
        with open(SESSIONS_FILE, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
    except: pass

def load_sessions_from_disk():
    global sessions
    if os.path.exists(SESSIONS_FILE):
        try:
            with open(SESSIONS_FILE, 'r', encoding='utf-8') as f:
                data = json.load(f)
                for sid, name in data.items():
                    sessions[sid] = Session(sid, name=name)
        except: pass

@app.route('/')
def index(): return render_template('web_cmd.html')

@app.route('/api/sessions', methods=['GET'])
def list_sessions():
    with session_lock:
        if not sessions: load_sessions_from_disk()
        return jsonify([s.to_dict() for s in sessions.values()])

@app.route('/api/sessions', methods=['POST'])
def create_session():
    import uuid
    session_id = str(uuid.uuid4())[:8]
    with session_lock:
        sessions[session_id] = Session(session_id, name=f"Chat {len(sessions)+1}")
    save_sessions_to_disk()
    return jsonify(sessions[session_id].to_dict())

@app.route('/api/sessions/<session_id>', methods=['PATCH'])
def rename_session(session_id):
    data = request.get_json()
    new_name = data.get('name')
    with session_lock:
        if session_id in sessions:
            sessions[session_id].name = new_name
            save_sessions_to_disk()
            return jsonify({'status': 'ok'})
    return jsonify({'error': 'not found'}), 404

@app.route('/api/sessions/<session_id>', methods=['DELETE'])
def delete_session(session_id):
    with session_lock:
        if session_id in sessions:
            s = sessions.pop(session_id)
            if s.proc:
                try: s.proc.terminate()
                except: pass
            if os.path.exists(s.log_path): os.remove(s.log_path)
            save_sessions_to_disk()
            return jsonify({'status': 'ok'})
    return jsonify({'error': 'not found'}), 404

@app.route('/api/logs', methods=['GET'])
def get_logs():
    session_id = request.args.get('session_id', 'default')
    last_id = request.args.get('last_id', -1, type=int)
    
    s = get_or_create_session(session_id)
    if not s.is_running and not s.proc:
        s.start()
        
    with s.lock:
        new_logs = [log for log in s.log_buffer if log['id'] > last_id]
    return jsonify({'logs': new_logs, 'running': s.is_running})

def get_or_create_session(session_id):
    with session_lock:
        if not sessions: load_sessions_from_disk()
        if session_id not in sessions:
            sessions[session_id] = Session(session_id)
            save_sessions_to_disk()
        return sessions[session_id]

@app.route('/api/input', methods=['POST'])