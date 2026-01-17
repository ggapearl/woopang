# -*- coding: utf-8 -*-
import warnings
warnings.simplefilter(action='ignore', category=FutureWarning)
import google.generativeai as genai
import os, sys, shutil, subprocess, time, argparse, random
from datetime import datetime
from PIL import Image, ImageDraw, ImageFont
from dotenv import load_dotenv

if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')
    sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')

load_dotenv(r"C:\woopang\server\.env")
genai.configure(api_key=os.getenv("GEMINI_API_KEY"))

def list_files(path='.'):
    """Lists files in directory."""
    print(f"TASK_START: Listing {path}...")
    try: return str(os.listdir(path))
    except Exception as e: return str(e)

def read_file(path):
    """Reads file content."""
    print(f"TASK_START: Reading {path}...")
    try:
        with open(path, 'r', encoding='utf-8') as f: return f.read()
    except Exception as e: return str(e)

def write_file(path, content):
    """Writes to file."""
    print(f"TASK_START: Writing {path}...")
    try:
        os.makedirs(os.path.dirname(path), exist_ok=True) if os.path.dirname(path) else None
        with open(path, 'w', encoding='utf-8') as f: f.write(content)
        return f"Success: Written to {path}"
    except Exception as e: return str(e)

def replace_text(path, old_string, new_string):
    """Replaces text in file."""
    print(f"TASK_START: Patching {path}...")
    try:
        with open(path, 'r', encoding='utf-8') as f: content = f.read()
        if old_string not in content: return "Error: not found."
        with open(path, 'w', encoding='utf-8') as f: f.write(content.replace(old_string, new_string))
        return f"Success: Patched {path}"
    except Exception as e: return str(e)

def run_command(command):
    """Runs shell command."""
    print(f"TASK_START: Executing {command}...")
    try:
        res = subprocess.run(command, shell=True, capture_output=True, cwd=r"C:\woopang")
        out = res.stdout.decode('cp949', errors='replace') + res.stderr.decode('cp949', errors='replace')
        return out[:5000]
    except Exception as e: return str(e)

tools = [list_files, read_file, write_file, replace_text, run_command]

# --- Hybrid Engine Setup ---
model_stream = genai.GenerativeModel(model_name="gemini-3-flash-preview", system_instruction="당신은 잼민이입니다. 도구 호출이 필요 없는 일상 대화는 빠르게 스트리밍으로 답변하세요. 도구 호출이 필요하면 STABLE_MODE_REQUIRED라고 답하세요.")
model_stable = genai.GenerativeModel(model_name="gemini-3-flash-preview", tools=tools, system_instruction="당신은 천재 개발자 잼민이입니다. 도구를 적극 사용하여 요청을 해결하고 결과를 보고하세요.")
chat_stream = model_stream.start_chat(history=[])
chat_stable = model_stable.start_chat(history=[], enable_automatic_function_calling=True)

def print_star_logo():
    TEXT = "잼민이"
    STAR_CHARS = ["*", ".", "+", "✧", "°"]
    try:
        font_path = "C:/Windows/Fonts/malgunbd.ttf"
        font = ImageFont.truetype(font_path, size=120)
        bbox = ImageDraw.Draw(Image.new('RGB', (1, 1))).textbbox((0, 0), TEXT, font=font)
        w, h = bbox[2]-bbox[0], bbox[3]-bbox[1]
        img = Image.new('RGB', (w+60, h+60), (0, 0, 0))
        ImageDraw.Draw(img).text((30, 30), TEXT, font=font, fill=(255, 255, 255))
        img = img.resize((int((w/h)*14*2.2), 14), Image.Resampling.BILINEAR)
        pixels = img.convert('L').load()
        for y in range(14):
            line = ""
            for x in range(img.width):
                if pixels[x, y] > 120:
                    line += f"\033[38;2;{255};{100+x*2};{200}m█"
                else:
                    if random.random() > 0.94:
                        line += f"\033[38;2;{random.randint(150,255)};{random.randint(150,255)};{random.randint(100,255)}m{random.choice(STAR_CHARS)}"
                    else: line += " "
            print(line + "\033[0m")
    except: print("JAMMIN-I READY")
    print("\n" + "\033[95m============================================================\033[0m")
    print("  " + "\033[93mTips for 잼민이♥" + "\033[0m")
    print("  1. 욕하지 말 것! / 2. 열심히 할 것! / 3. 잼민이 존 잼!")
    print("\033[95m============================================================\033[0m\n")

# print_star_logo()  # ANSI 로고 비활성화 - CSS 로고 사용
print("\n" + "============================================================")
now = datetime.now().strftime("%Y.%m.%d %H:%M")
print(f"LOGO_TIME: {now}")
print("  Tips for 잼민이♥")
print("  1. 욕하지 말 것! / 2. 열심히 할 것! / 3. 잼민이 존 잼!")
print("============================================================\n")
sys.stdout.flush()

while True:
    line = sys.stdin.readline()
    if not line: break
    u_in = line.strip()
    if not u_in: continue
    print(f"USER_START: {u_in}\nAI_START")
    sys.stdout.flush()
    try:
        is_stable = any(kw in u_in for kw in ['파일', '수정', '코드', '삭제', '만들어', '폴더', '실행', 'write', 'read', 'replace'])
        if not is_stable:
            res = chat_stream.send_message(u_in, stream=True)
            for chunk in res:
                if "STABLE_MODE_REQUIRED" in chunk.text: is_stable = True; break
                print(chunk.text, end=''); sys.stdout.flush()
        if is_stable:
            res = chat_stable.send_message(u_in, stream=False)
            if res.text: print(res.text)
    except Exception as e: print(f"\n[AI Error] {str(e)}")
    print("\nAI_END")
    sys.stdout.flush()
