# -*- coding: utf-8 -*-
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
import re
from PIL import Image, ImageDraw, ImageFont
from dotenv import load_dotenv

# Set UTF-8 for Windows console
if sys.platform == 'win32':
    import io
    try:
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
        sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')
        sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8')
    except Exception:
        pass

# --- Load Environment ---
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
        if os.path.dirname(path):
            os.makedirs(os.path.dirname(path), exist_ok=True)
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
        result = subprocess.run(command, shell=True, capture_output=True)
        try:
            stdout = result.stdout.decode('cp949')
        except:
            stdout = result.stdout.decode('utf-8', errors='replace')
        try:
            stderr = result.stderr.decode('cp949')
        except:
            stderr = result.stderr.decode('utf-8', errors='replace')
        return f"Out: {stdout}\nErr: {stderr}"
    except Exception as e: return str(e)

tools = [list_files, read_file, write_file, delete_file, run_command]

current_model_name = "gemini-3-flash-preview"
generation_config = {"temperature": 0.7, "top_p": 0.95, "top_k": 40, "max_output_tokens": 8192}
system_instruction = "You are 'Jammin-i', a genius AI dev agent. Speak Korean."

def main():
    model = genai.GenerativeModel(model_name=current_model_name, generation_config=generation_config, tools=tools, system_instruction=system_instruction)
    chat = model.start_chat(history=[], enable_automatic_function_calling=True)

    print("AI_START\nJammin-i Ready.\nAI_END")
    sys.stdout.flush()

    while True:
        try:
            line = sys.stdin.readline()
            if not line: break
            u_in = line.strip()
            if not u_in: continue

            print(f"USER_START: {u_in}")
            print("AI_START")
            sys.stdout.flush()

            res = chat.send_message(u_in, stream=False)
            if res.text: print(res.text)

            print("AI_END")
            sys.stdout.flush()
        except Exception as e:
            print(f"\n[Error] {str(e)}\nAI_END")
            sys.stdout.flush()

if __name__ == "__main__":
    main()