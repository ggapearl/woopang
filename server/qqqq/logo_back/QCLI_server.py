from flask import Flask, render_template, request, jsonify
import subprocess
import os
import threading
import time
import sys
from collections import deque

app = Flask(__name__)

# Suppress Flask (Werkzeug) access logs
import logging
log = logging.getLogger('werkzeug')
log.setLevel(logging.ERROR)

# Global state
CURRENT_DIR = r"C:\woopang"
proc = None
is_running = False

# Log storage
# 각 로그에 ID를 부여하여 클라이언트가 "마지막으로 받은 로그 이후"의 데이터만 요청하도록 함
log_buffer = deque(maxlen=500) 
log_id_counter = 0
log_lock = threading.Lock()

def add_log(text):
    """로그를 버퍼에 추가합니다."""
    global log_id_counter
    with log_lock:
        log_id_counter += 1
        log_buffer.append({
            'id': log_id_counter,
            'text': text
        })

def read_output(process):
    """서버 프로세스의 출력을 읽어 로그 버퍼에 저장합니다."""
    global is_running
    try:
        while process.poll() is None:
            # 한 줄씩 읽기
            line = process.stdout.readline()
            if not line:
                break

            try:
                # Try UTF-8 first (since my_gemini.py forces UTF-8)
                decoded_line = line.decode('utf-8', errors='strict')
            except:
                try:
                    # Fallback to CP949 if UTF-8 fails
                    decoded_line = line.decode('cp949', errors='replace')
                except:
                    decoded_line = line.decode('utf-8', errors='replace')

            add_log(decoded_line)

    except Exception as e:
        add_log(f"\n[System Error reading output: {e}]\n")
    finally:
        is_running = False
        add_log("\n[System: Process terminated]\n")

def start_shell():
    """Shell 프로세스를 시작합니다."""
    global proc, is_running

    # 기존 프로세스 종료
    if proc and proc.poll() is None:
        proc.terminate()

    env = os.environ.copy()
    env["PYTHONUNBUFFERED"] = "1"

    try:
        # Run the custom python gemini script directly
        # -u option forces unbuffered binary stdout/stderr
        proc = subprocess.Popen(
            ['python', '-u', r'server\qcli\my_gemini.py'],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            cwd=CURRENT_DIR,
            shell=False,
            env=env,
            bufsize=0
        )

        is_running = True

        # 출력 읽기 스레드 시작
        t = threading.Thread(target=read_output, args=(proc,))
        t.daemon = True
        t.start()

        # add_log(f"[Woopang Gemini Agent Connected]\n") # Removed as per request (header shows status)

    except Exception as e:
        add_log(f"Failed to start shell: {e}\n")

@app.route('/')
def index():
    return render_template('web_cmd.html')

@app.route('/api/logs', methods=['GET'])
def get_logs():
    """
    클라이언트가 마지막으로 받은 last_id 이후의 로그를 반환합니다.
    """
    global is_running
    last_id = request.args.get('last_id', -1, type=int)
    
    # 프로세스가 없으면 시작
    if not is_running:
        start_shell()

    with log_lock:
        # last_id보다 큰 id를 가진 로그만 필터링
        new_logs = [log for log in log_buffer if log['id'] > last_id]
        
    return jsonify({
        'logs': new_logs,
        'running': is_running
    })

@app.route('/api/input', methods=['POST'])
def send_input():
    """클라이언트로부터 명령어를 받아 Shell에 입력합니다."""
    global proc, is_running

    data = request.get_json()
    command = data.get('command', '')

    if command == '\x03': # Ctrl+C
        if proc:
            proc.terminate()
            is_running = False
        start_shell()
        return jsonify({'status': 'restarted'})

    if command.strip().lower() == 'restart_shell':
        start_shell()
        return jsonify({'status': 'restarted'})

    if proc and proc.poll() is None:
        try:
            # Use UTF-8 for communication with Python script
            cmd_bytes = (command + "\n").encode('utf-8')
            proc.stdin.write(cmd_bytes)
            proc.stdin.flush()
            return jsonify({'status': 'sent'})
        except Exception as e:
            add_log(f"\n[Error writing to shell: {e}]\n")
            start_shell()
            return jsonify({'status': 'error', 'message': str(e)})
    else:
        start_shell()
        return jsonify({'status': 'restarted_and_ignored'})

if __name__ == '__main__':
    print(f"Starting QCLI REST Server on port 5030")
    if not is_running:
        start_shell()
    # Disable debug reloader to prevent double execution
    app.run(host='0.0.0.0', port=5030, debug=False)