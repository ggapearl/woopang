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
from werkzeug.utils import secure_filename

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
                    content = f.read()
                    content = content.replace("[System: Connection Closed]", "")
                    lines = content.splitlines()

                    skip_next = False
                    for line in lines:
                        # Skip ASCII logo lines completely (all decorative characters)
                        if any(char in line for char in ['█', '▓', '✨', '★', '▒', '░']) and not line.strip().startswith('PDNOM'):
                            skip_next = True
                            continue
                        # Skip Tips message and all tip content
                        if any(text in line for text in ['Tips for 잼민이', '욕하지 말 것', '열심히 할 것', '잼민이 존 잼']):
                            skip_next = True
                            continue
                        # Skip separator lines
                        if line.strip().startswith('===') or line.strip() == '=' * 60:
                            skip_next = True
                            continue
                        # Skip empty lines right after logo/tips
                        if skip_next and not line.strip():
                            continue

                        skip_next = False
                        if line.strip():
                            self.log_id_counter += 1
                            self.log_buffer.append({'id': self.log_id_counter, 'text': line + "\n"})
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
        try:
            cmd_args = ['python', '-u', os.path.join(BASE_DIR, 'my_gemini.py'), '--history', self.log_path]
            if os.path.exists(self.context_path):
                cmd_args.extend(['--context', self.context_path])
            
            self.proc = subprocess.Popen(
                cmd_args,
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                cwd=r"C:\woopang", shell=False, env=env, bufsize=0
            )
            self.is_running = True
            threading.Thread(target=self.read_output, daemon=True).start()
        except Exception as e:
            self.add_log(f"에이전트 시작 실패: {e}\n")

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
    global sessions
    with session_lock:
        data = {sid: s.name for sid, s in sessions.items()}
        try:
            with open(SESSIONS_FILE, 'w', encoding='utf-8') as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
        except Exception as e:
            print(f"Save Meta Error: {e}")

def load_meta():
    global sessions
    if os.path.exists(SESSIONS_FILE):
        try:
            with open(SESSIONS_FILE, 'r', encoding='utf-8') as f:
                data = json.load(f)
                for sid, name in data.items():
                    if sid not in sessions:
                        sessions[sid] = Session(sid, name)
                    else:
                        sessions[sid].name = name
        except Exception as e:
            print(f"Load Meta Error: {e}")

@app.route('/')
def index():
    import time
    # Use brand new template to bypass all caching
    resp = make_response(render_template('web_cmd_v2.html'))
    resp.headers['Cache-Control'] = 'no-store, no-cache, must-revalidate, max-age=0'
    resp.headers['Pragma'] = 'no-cache'
    resp.headers['Expires'] = '-1'
    return resp

@app.route('/api/upload', methods=['POST'])
def upload_file():
    if 'file' not in request.files: return jsonify({'error': 'No file'}), 400
    file = request.files['file']
    if file.filename == '': return jsonify({'error': 'No filename'}), 400
    filename = secure_filename(f"{int(time.time())}_{file.filename}")
    save_path = os.path.join(UPLOAD_DIR, filename)
    file.save(save_path)
    return jsonify({
        'path': save_path,
        'url': f'/QQQQ/uploads/tmp/{filename}'
    })

@app.route('/uploads/tmp/<path:filename>')
def serve_tmp_file(filename):
    try:
        file_path = os.path.normpath(os.path.join(UPLOAD_DIR, filename))
        if not file_path.startswith(os.path.abspath(UPLOAD_DIR)):
            return "Unauthorized", 403
        if not os.path.exists(file_path):
            return "File Not Found", 404
        return send_file(file_path)
    except Exception as e:
        return f"File serve error: {str(e)}", 500

@app.route('/api/sessions', methods=['GET', 'POST'])
def handle_sessions():
    global sessions
    if request.method == 'POST':
        sid = str(uuid.uuid4())[:8]
        with session_lock:
            sessions[sid] = Session(sid)
        save_meta()
        return jsonify({'id': sid, 'name': sessions[sid].name})

    if not sessions: load_meta()
    with session_lock:
        session_list = [{'id': s.id, 'name': s.name} for s in sessions.values()]
        return jsonify(session_list)

@app.route('/api/sessions/<sid>', methods=['PATCH', 'DELETE'])
def manage_session(sid):
    global sessions
    if sid not in sessions: return "Not Found", 404
    if request.method == 'DELETE':
        with session_lock:
            s = sessions.pop(sid)
            if s.proc: s.proc.terminate()
            if os.path.exists(s.log_path): os.remove(s.log_path)
            if os.path.exists(s.context_path): os.remove(s.context_path)
        save_meta()
        return jsonify({'status': 'ok'})
    sessions[sid].name = request.json.get('name', sessions[sid].name)
    save_meta(); return jsonify({'status': 'ok'})

@app.route('/api/sessions/<sid>/context', methods=['GET', 'POST'])
def handle_session_context(sid):
    global sessions
    if sid not in sessions: return "Not Found", 404
    s = sessions[sid]
    if request.method == 'POST':
        context_data = request.json.get('context', '')
        with open(s.context_path, 'w', encoding='utf-8') as f:
            f.write(context_data)
        # If agent is running, we might need to restart it to pick up new context
        # But for simplicity, we'll let the next interaction start or the user manual restart
        return jsonify({'status': 'ok'})
    
    context_data = ''
    if os.path.exists(s.context_path):
        with open(s.context_path, 'r', encoding='utf-8') as f:
            context_data = f.read()
    return jsonify({'context': context_data})

@app.route('/api/logs')
def get_logs():
    sid = request.args.get('sid', 'default')
    last_id = request.args.get('last_id', -1, type=int)
    global sessions
    if sid not in sessions:
        load_meta()
        if sid not in sessions:
            with session_lock: sessions[sid] = Session(sid)
            save_meta()
    s = sessions[sid]
    if not s.is_running: s.start()

    with s.lock:
        if last_id > s.log_id_counter:
            return jsonify({'logs': [], 'running': s.is_running, 'reset': True})
        new_logs = [l for l in s.log_buffer if l['id'] > last_id]
    return jsonify({'logs': new_logs, 'running': s.is_running})

@app.route('/api/input', methods=['POST'])
def send_input():
    global sessions
    data = request.json
    sid = data.get('sid', 'default')
    s = sessions.get(sid)
    if not s: return jsonify({'status': 'error', 'message': 'Session not found'}), 404
    return jsonify({'status': s.send(data.get('command'))})

def print_logo():
    """Print JAMMINI logo on server startup"""
    import sys
    from PIL import Image, ImageDraw, ImageFont

    TEXT = "잼민이"
    HEIGHT_TARGET = 20
    COLOR_START = (220, 0, 90)
    COLOR_END = (25, 25, 180)
    BLOCK_MAIN = "█"
    BLOCK_EDGE = "▓"
    SPARKLE_CHARS = ["✨", "★"]

    def get_gradient_color(ratio, start_rgb, end_rgb, fade_factor=1.0):
        r = int(start_rgb[0] * (1 - ratio) + end_rgb[0] * ratio)
        g = int(start_rgb[1] * (1 - ratio) + end_rgb[1] * ratio)
        b = int(start_rgb[2] * (1 - ratio) + end_rgb[2] * ratio)
        return (int(r * fade_factor), int(g * fade_factor), int(b * fade_factor))

    try:
        font_path = "C:/Windows/Fonts/malgunbd.ttf"
        if not os.path.exists(font_path):
            font_path = "C:/Windows/Fonts/malgun.ttf"
        font = ImageFont.truetype(font_path, size=120)

        dummy = ImageDraw.Draw(Image.new('RGB', (1, 1)))
        bbox = dummy.textbbox((0, 0), TEXT, font=font)
        w, h = bbox[2]-bbox[0], bbox[3]-bbox[1]

        pad_x, pad_y = int(w*0.1), int(h*0.2)
        image_hi = Image.new('RGB', (w + pad_x*2, h + pad_y*2), (0, 0, 0))
        draw = ImageDraw.Draw(image_hi)
        draw.text((pad_x, pad_y), TEXT, font=font, fill=(255, 255, 255))

        aspect = image_hi.width / image_hi.height
        new_height = HEIGHT_TARGET
        new_width = int(aspect * new_height * 2.0)

        image_lo = image_hi.resize((new_width, new_height), Image.Resampling.BILINEAR)
        pixels = image_lo.convert('L').load()

        final_output = []
        for y in range(new_height):
            line_buffer = ""
            for x in range(new_width):
                brightness = pixels[x, y]
                grad_ratio = x / new_width

                if brightness > 128:
                    char_to_print = BLOCK_MAIN
                    color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 1.0)
                elif brightness > 50:
                    char_to_print = BLOCK_EDGE
                    color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 0.6)
                else:
                    char_to_print = " "
                    color = (0, 0, 0)

                if char_to_print == " ":
                    line_buffer += " "
                else:
                    line_buffer += f"\033[38;2;{color[0]};{color[1]};{color[2]}m{char_to_print}"

            final_output.append(line_buffer + "\033[0m")

        print("\n")
        print("\n".join(final_output))
        print("\033[0m")
        print("\n" + "="*60)
        print("  Tips for 잼민이♥")
        print("  1. 욕하지 말 것!")
        print("  2. 열심히 할 것!")
        print("  3. 잼민이 존 잼!")
        print("="*60 + "\n")

    except Exception as e:
        print(f"\n{'='*60}")
        print("  ✨ 잼민이 Dev Agent ✨")
        print(f"{'='*60}\n")

if __name__ == '__main__':
    print_logo()
    load_meta()
    print(f"🚀 QQQQ Server starting on http://0.0.0.0:5099")
    print(f"📱 Mobile access: https://woopang.com/QQQQ\n")
    # Allow external access for mobile devices
    app.run(host='0.0.0.0', port=5099, debug=False, threaded=True)