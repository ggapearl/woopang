import warnings
warnings.simplefilter(action='ignore', category=FutureWarning)

import google.generativeai as genai
import os
import sys
import shutil
import random
import subprocess
import time
import argparse
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from dotenv import load_dotenv

load_dotenv(r"C:\\woopang\\server\\.env")
api_key = os.getenv("GEMINI_API_KEY")
if not api_key:
    print("[Error] GEMINI_API_KEY not found")
    sys.exit(1)

genai.configure(api_key=api_key)

def list_files(path='.'):
    print(f"TASK_START: Listing files in {path}...")
    try: return os.listdir(path)
    except Exception as e: return str(e)

def read_file(path):
    print(f"TASK_START: Reading file {path}...")
    try:
        with open(path, 'r', encoding='utf-8') as f: return f.read()
    except Exception as e: return str(e)

def write_file(path, content):
    print(f"TASK_START: Writing to {path}...")
    try:
        os.makedirs(os.path.dirname(path), exist_ok=True) if os.path.dirname(path) else None
        with open(path, 'w', encoding='utf-8') as f: f.write(content)
        return f"Saved to {path}"
    except Exception as e: return str(e)

def delete_file(path):
    print(f"TASK_START: Deleting {path}...")
    try:
        if os.path.isfile(path): os.remove(path); return f"Deleted {path}"
        return "Not a file"
    except Exception as e: return str(e)

def run_command(command):
    print(f"TASK_START: Executing command: {command}...")
    try:
        result = subprocess.run(command, shell=True, capture_output=True, text=True, encoding='cp949', errors='replace')
        return f"Out: {result.stdout}\nErr: {result.stderr}"
    except Exception as e: return str(e)

tools = [list_files, read_file, write_file, delete_file, run_command]
generation_config = {"temperature": 0.3, "top_p": 0.95, "top_k": 40, "max_output_tokens": 8192}
current_model_name = "gemini-3-pro-preview"
system_instruction = "당신은 강력한 AI '잼민이'입니다. 마크다운으로 답변하세요. 본론만 말하세요."

def get_gradient_color(ratio, start_rgb, end_rgb, fade_factor=1.0):
    r = int(start_rgb[0] * (1 - ratio) + end_rgb[0] * ratio)
    g = int(start_rgb[1] * (1 - ratio) + end_rgb[1] * ratio)
    b = int(start_rgb[2] * (1 - ratio) + end_rgb[2] * ratio)
    return (int(r * fade_factor), int(g * fade_factor), int(b * fade_factor))   

def print_logo():
    TEXT = "잼민이"
    COLOR_START, COLOR_END = (220, 0, 90), (25, 25, 180)
    BG_STAR_COLOR = (80, 40, 130)
    SHADE_CHARS = ["░", "▒", "▓", "█"]
    SPARKLE_CHARS = ["✧", "✦", "✫", "✬", "✭"]
    BG_CHARS = ["*", ".", " ", " ", " "]
    
    try:
        font_path = "C:/Windows/Fonts/malgunbd.ttf"
        if not os.path.exists(font_path): font_path = "C:/Windows/Fonts/malgun.ttf" 
        # Increase font size for better clarity in '잼'
        font = ImageFont.truetype(font_path, size=180) 
        
        dummy = ImageDraw.Draw(Image.new('RGB', (1, 1)))
        bbox = dummy.textbbox((0, 0), TEXT, font=font)
        w, h = bbox[2]-bbox[0], bbox[3]-bbox[1]
        pad_x, pad_y = int(w*0.15), int(h*0.3)

        image_hi = Image.new('RGB', (w + pad_x*2, h + pad_y*2), (0, 0, 0))
        draw = ImageDraw.Draw(image_hi)
        draw.text((pad_x, pad_y), TEXT, font=font, fill=(255, 255, 255))
        # Light blur as in LOGO_01.py
        image_hi = image_hi.filter(ImageFilter.GaussianBlur(radius=1.5)) 

        aspect = image_hi.width / image_hi.height
        new_height = 20
        new_width = int(aspect * new_height * 2.1)

        try:
            term_w = shutil.get_terminal_size().columns
            if new_width > term_w - 2:
                new_width = term_w - 2
                new_height = int(new_width / (aspect * 2.1))
        except: pass

        image_lo = image_hi.resize((new_width, new_height), Image.Resampling.BILINEAR)
        pixels = image_lo.convert('L').load()

        final_output = []
        for y in range(new_height):
            line = ""
            for x in range(new_width):
                brightness = pixels[x, y]
                grad_ratio = x / new_width
                color = (0,0,0)
                char = " "

                if brightness < 40: # Adjusted threshold for background
                    if random.random() > 0.93:
                        char = random.choice(BG_CHARS)
                        color = BG_STAR_COLOR
                elif brightness < 110: # Refined thresholds for clarity
                    char = SHADE_CHARS[int((brightness - 40) / 70 * 2)]
                    color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 0.7)
                elif brightness < 210:
                    char = SHADE_CHARS[2]
                    color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 1.0)
                else:
                    if random.random() > 0.94:
                        char = random.choice(SPARKLE_CHARS)
                        c = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 1.0)
                        color = (min(255, c[0]+60), min(255, c[1]+60), min(255, c[2]+60))
                    else:
                        char = SHADE_CHARS[3]
                        color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 1.0)

                if char == " ": line += " "
                else: line += f"\033[38;2;{color[0]};{color[1]};{color[2]}m{char}"
            final_output.append(line + "\033[0m")
        print("\n".join(final_output))
    except Exception as e: 
        print(f"\n   [Logo Error] {e}\033[0m\n")

    # 2 blank lines as requested
    print("\n\n")
    print(f"\033[1;95mTips for 잼민이♥\033[0m")
    print(f"\033[93m1.욕하지 말 것!\033[0m")
    print(f"\033[92m2.열심히 할 것!\033[0m")
    print(f"\033[96m3.잼민이 존 잼!\033[0m\n")
    sys.stdout.flush()

def parse_history(file_path):
    history = []
    if not os.path.exists(file_path): return history
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        parts = content.split('USER_START: ')
        for p in parts[1:]:
            if not p.strip(): continue
            user_msg_end = p.find('\n')
            user_msg = p[:user_msg_end].strip()
            ai_part_start = p.find('AI_START')
            ai_part_end = p.find('AI_END')
            if ai_part_start != -1 and ai_part_end != -1:
                ai_msg = p[ai_part_start+8:ai_part_end].strip()
                ai_msg_lines = [l for l in ai_msg.split('\n') if not l.startswith('TASK_START: ')]
                ai_msg = '\n'.join(ai_msg_lines).strip()
                history.append({"role": "user", "parts": [user_msg]})
                history.append({"role": "model", "parts": [ai_msg]})
    except: pass
    return history

def main():
    global current_model_name, model, chat
    parser = argparse.ArgumentParser()
    parser.add_argument('--history', type=str, help='Path to history log file')
    args = parser.parse_args()

    initial_history = []
    already_has_logo = False
    if args.history and os.path.exists(args.history):
        try:
            with open(args.history, 'r', encoding='utf-8') as f:
                content = f.read()
                # Check for logo to avoid double printing
                if "Tips for 잼민이" in content:
                    already_has_logo = True
            initial_history = parse_history(args.history)
        except: pass

    if not already_has_logo:
        print_logo()
    
    if initial_history:
        sys.stdout.write(f"\033[94m[시스템: {len(initial_history)//2}개의 이전 대화 맥락을 복원했습니다]\033[0m\n")
    sys.stdout.flush()
    
    model = genai.GenerativeModel(model_name=current_model_name, generation_config=generation_config, tools=tools, system_instruction=system_instruction)
    chat = model.start_chat(history=initial_history, enable_automatic_function_calling=True)
    
    while True:
        try:
            user_input = input("").strip()
            if not user_input: continue
            if user_input.startswith("/model "):
                new_m = user_input.split(" ")[1]
                current_model_name = new_m
                model = genai.GenerativeModel(model_name=current_model_name, generation_config=generation_config, tools=tools, system_instruction=system_instruction)
                chat = model.start_chat(history=chat.history, enable_automatic_function_calling=True)
                print(f"[시스템: 모델이 {new_m}으로 변경되었습니다]"); sys.stdout.flush(); continue

            print(f"USER_START: {user_input}"); sys.stdout.flush()
            print("AI_START"); sys.stdout.flush()
            print("TASK_START: Thinking about your request...")
            response = chat.send_message(user_input, stream=False)
            if response.text: print(response.text)
            print("AI_END"); sys.stdout.flush()
        except EOFError: break
        except KeyboardInterrupt:
            print("\n\033[91m[KeyboardInterrupt]\033[0m\n"); break
        except Exception as e:
            print(f"\n[Error] {e}\n"); sys.stdout.flush()

if __name__ == "__main__":
    sys.stdin.reconfigure(encoding='utf-8')
    sys.stdout.reconfigure(encoding='utf-8')
    main()
