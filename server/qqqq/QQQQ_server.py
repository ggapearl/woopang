from flask import Flask, render_template, request, jsonify, Response, make_response, send_from_directory, send_file
import subprocess
import os
import threading
import time
import sys
from collections import deque
import logging
import json
import uuid
import io
from werkzeug.utils import secure_filename
from PIL import Image, ImageDraw, ImageFont

app = Flask(__name__)
log = logging.getLogger('werkzeug')
log.setLevel(logging.ERROR)

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
LOGS_DIR = os.path.join(BASE_DIR, "logs")
UPLOAD_DIR = os.path.join(BASE_DIR, "uploads", "tmp")
SESSIONS_FILE = os.path.join(BASE_DIR, "sessions.json")
os.makedirs(LOGS_DIR, exist_ok=True)
os.makedirs(UPLOAD_DIR, exist_ok=True)

sessions = {}
session_lock = threading.Lock()

def get_ascii_logo():
    """진짜 컬러 ASCII 로고 문자열을 생성하여 반환합니다."""
    TEXT = "잼민이"
    HEIGHT_TARGET = 15
    COLOR_START = (220, 0, 90)
    COLOR_END = (25, 25, 180)
    BLOCK_MAIN = "█"
    BLOCK_EDGE = "▓"

    try:
        font_path = "C:/Windows/Fonts/malgunbd.ttf"
        if not os.path.exists(font_path): font_path = "C:/Windows/Fonts/malgun.ttf"
        font = ImageFont.truetype(font_path, size=100)
        dummy = ImageDraw.Draw(Image.new('RGB', (1, 1)))
        bbox = dummy.textbbox((0, 0), TEXT, font=font)
        w, h = bbox[2]-bbox[0], bbox[3]-bbox[1]
        image_hi = Image.new('RGB', (w + 20, h + 20), (0, 0, 0))
        draw = ImageDraw.Draw(image_hi)
        draw.text((10, 10), TEXT, font=font, fill=(255, 255, 255))
        new_width = int((image_hi.width / image_hi.height) * HEIGHT_TARGET * 2.0)
        image_lo = image_hi.resize((new_width, HEIGHT_TARGET), Image.Resampling.BILINEAR)
        pixels = image_lo.convert('L').load()

        output = "\n"
        for y in range(HEIGHT_TARGET):
            for x in range(new_width):
                brightness = pixels[x, y]
                ratio = x / new_width
                r = int(COLOR_START[0] * (1-ratio) + COLOR_END[0] * ratio)
                g = int(COLOR_START[1] * (1-ratio) + COLOR_END[1] * ratio)
                b = int(COLOR_START[2] * (1-ratio) + COLOR_END[2] * ratio)
                if brightness > 128: output += f"\033[38;2;{r};{g};{b}m{BLOCK_MAIN}"
                elif brightness > 50: output += f"\033[38;2;{int(r*0.6)};{int(g*0.6)};{int(b*0.6)}m{BLOCK_EDGE}"
                else: output += " "
            output += "\033[0m\n"
        
        output += "\n\033[95m" + "="*50 + "\033[0m\n"
        output += "  \033[93mTips for 잼민이♥\033[0m\n"
        output += "  1. 욕하지 말 것! / 2. 열심히 할 것! / 3. 잼민이 존 잼!\n"
        output += "\033[95m" + "="*50 + "\033[0m\n\n"
        return output
    except:
        return "\n ✨ 잼민이 Dev Agent ✨ \n"

class Session:
    def __init__(self, session_id, name=None):
        self.id = session_id
        self.name = name if name else "새 대화"
        self.proc = None
        self.is_running = False
        self.log_buffer = deque(maxlen=3000)
        self.log_id_counter = 0
        self.lock = threading.Lock()
        self.log_path = os.path.join(LOGS_DIR, f"{self.id}.log")
        self.context_path = os.path.join(LOGS_DIR, f"{self.id}.context")
        self.load_history()

    def load_history(self):
        if os.path.exists(self.log_path):
            try:
                with open(self.log_path, 'r', encoding='utf-8') as f:
                    for line in f:
                        if "[System: Connection Closed]" in line: continue
                        self.log_id_counter += 1
                        self.log_buffer.append({'id': self.log_id_counter, 'text': line})
            except: pass

    def add_log(self, text):
        if "[System: Connection Closed]" in text: return
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
        env["PYTHONIOENCODING"] = "utf-8"
        try:
            cmd_args = ['python', '-u', os.path.join(BASE_DIR, 'my_gemini.py'), '--history', self.log_path]
            if os.path.exists(self.context_path): cmd_args.extend(['--context', self.context_path])
            self.proc = subprocess.Popen(cmd_args, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, cwd=r"C:\woopang", shell=False, env=env, bufsize=0)
            self.stdout_reader = io.TextIOWrapper(self.proc.stdout, encoding='utf-8', errors='replace')
            self.is_running = True
            threading.Thread(target=self.read_output, daemon=True).start()
        except Exception as e: self.add_log(f"에이전트 시작 실패: {e}\n")

    def read_output(self):
        try:
            buffer = ""
            while self.proc.poll() is None:
                decoded = self.stdout_reader.read(1)
                if not decoded: break
                buffer += decoded
                if decoded == '\n' or len(buffer) >= 50:
                    self.add_log(buffer)
                    buffer = ""
            if buffer: self.add_log(buffer)
        except: pass
        finally: self.is_running = False

    def send(self, cmd):
        if cmd == 'cancel':
            if self.proc:
                try:
                    self.proc.stdin.write(b'\x03')
                    self.proc.stdin.flush()
                except: pass
            return 'cancelled'
        
        if not self.is_running or not self.proc or self.proc.poll() is not None:
            self.start()
            for _ in range(10):
                if self.is_running: break
                time.sleep(0.5)
            time.sleep(1.0)

        try:
            self.proc.stdin.write((cmd + "\n").encode('utf-8'))
            self.proc.stdin.flush()
            return 'sent'
        except Exception as e:
            self.add_log(f"\n[System Error] 전송 실패: {e}\n")
            return 'error'

def save_meta():
    with session_lock:
        data = {sid: s.name for sid, s in sessions.items()}
        with open(SESSIONS_FILE, 'w', encoding='utf-8') as f: json.dump(data, f, ensure_ascii=False, indent=2)

def load_meta():
    if os.path.exists(SESSIONS_FILE):
        try:
            with open(SESSIONS_FILE, 'r', encoding='utf-8') as f:
                data = json.load(f)
                for sid, name in data.items():
                    if sid not in sessions: sessions[sid] = Session(sid, name)
        except: pass

@app.route('/uploads/tmp/<path:filename>')
def serve_uploaded_file(filename):
    return send_from_directory(UPLOAD_DIR, filename)

@app.route('/')
def index():
    response = make_response(render_template('web_cmd_v2.html'))
    response.headers['Cache-Control'] = 'no-store, no-cache, must-revalidate, post-check=0, pre-check=0, max-age=0'
    response.headers['Pragma'] = 'no-cache'
    response.headers['Expires'] = '-1'
    return response

@app.route('/api/upload', methods=['POST'])
def upload_file():
    if 'file' not in request.files: return jsonify({'error': 'No file'}), 400
    file = request.files['file']
    filename = secure_filename(f"{int(time.time())}_{file.filename}")
    save_path = os.path.join(UPLOAD_DIR, filename)
    file.save(save_path)
    return jsonify({'path': save_path, 'url': f'/QQQQ/uploads/tmp/{filename}'})

@app.route('/api/sessions', methods=['GET', 'POST'])
def handle_sessions():
    if request.method == 'POST':
        sid = str(uuid.uuid4())[:8]
        with session_lock: sessions[sid] = Session(sid)
        save_meta()
        return jsonify({'id': sid, 'name': sessions[sid].name})
    if not sessions: load_meta()
    return jsonify([{'id': s.id, 'name': s.name} for s in sessions.values()])

@app.route('/api/sessions/<sid>', methods=['PATCH', 'DELETE'])
def manage_session(sid):
    if sid not in sessions: return "Not Found", 404
    if request.method == 'DELETE':
        with session_lock:
            s = sessions.pop(sid)
            if s.proc: s.proc.terminate()
            if os.path.exists(s.log_path): os.remove(s.log_path)
        save_meta(); return jsonify({'status': 'ok'})
    sessions[sid].name = request.json.get('name', sessions[sid].name)
    save_meta(); return jsonify({'status': 'ok'})

@app.route('/api/sessions/<sid>/context', methods=['GET', 'POST'])
def handle_session_context(sid):
    if sid not in sessions: return "Not Found", 404
    s = sessions[sid]
    if request.method == 'POST':
        with open(s.context_path, 'w', encoding='utf-8') as f: f.write(request.json.get('context', ''))
        return jsonify({'status': 'ok'})
    context_data = ''
    if os.path.exists(s.context_path):
        with open(s.context_path, 'r', encoding='utf-8') as f: context_data = f.read()
    return jsonify({'context': context_data})

@app.route('/api/logs')
def get_logs():
    sid = request.args.get('sid', 'default')
    last_id = request.args.get('last_id', -1, type=int)
    if sid not in sessions:
        load_meta()
        if sid not in sessions:
            with session_lock: sessions[sid] = Session(sid)
            save_meta()
    s = sessions[sid]
    if not s.is_running: s.start()
    with s.lock:
        new_logs = [l for l in s.log_buffer if l['id'] > last_id]
    return jsonify({'logs': new_logs, 'running': s.is_running})

@app.route('/api/input', methods=['POST'])
def send_input():
    data = request.json
    s = sessions.get(data.get('sid', 'default'))
    if not s: return jsonify({'status': 'error'}), 404
    return jsonify({'status': s.send(data.get('command'))})

if __name__ == '__main__':
    load_meta()
    app.run(host='0.0.0.0', port=5099, debug=False, threaded=True)
