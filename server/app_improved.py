from flask import Flask, jsonify, send_from_directory, request, render_template, session, redirect, url_for, send_file, make_response, Response, stream_with_context
from werkzeug.utils import secure_filename
import time
from datetime import datetime, timedelta, timezone  # timezone 명시
import psycopg2
from psycopg2 import Error as Psycopg2Error
from psycopg2 import pool as psycopg2_pool
from collections import OrderedDict
import os
import json
import shutil
import ssl
import socket
from dotenv import load_dotenv
import firebase_admin
from firebase_admin import credentials, messaging
import requests
import traceback
import logging
from logging.handlers import RotatingFileHandler  # 로그 관리 추가
import sys
import jwt
import uuid
from cryptography.hazmat.primitives.serialization import load_pem_private_key
import threading
import random
from cryptography.hazmat.primitives import serialization
from cryptography import x509
from cryptography.hazmat.backends import default_backend
import hashlib
from functools import wraps
from vdown.vdown_server import vdown_bp
from admin.admin_server import admin_bp
from majang.majang_server import majang_bp
from email_service.email_server import email_bp

# Rate Limiting (보안)
try:
    from flask_limiter import Limiter
    from flask_limiter.util import get_remote_address
    RATE_LIMITER_AVAILABLE = True
except ImportError:
    RATE_LIMITER_AVAILABLE = False

def get_ssl_days_remaining(cert_path=r'C:\woopang\server\woopang.com-fullchain.pem'):
    try:
        with open(cert_path, 'rb') as f:
            cert_data = f.read()
            cert = x509.load_pem_x509_certificate(cert_data, default_backend())
            # Use not_valid_after_utc if available (cryptography >= 42.0.0), else fallback
            try:
                expiry_date = cert.not_valid_after_utc.replace(tzinfo=None)
            except AttributeError:
                 expiry_date = cert.not_valid_after
            
            days_remaining = (expiry_date - datetime.now()).days
            return days_remaining
    except Exception as e:
        safe_print(f"[Warning] SSL Expiry Check Failed: {e}")
        return None

def run_auto_renewal_check():
    try:
        # Run the auto renewal script logic
        import auto_renew_ssl
        auto_renew_ssl.check_and_renew()
    except Exception as e:
        safe_print(f"[Error] Auto Renewal Check Failed: {e}")

import aiohttp
import asyncio
import httpx
import math
from decimal import Decimal
import bcrypt

# Windows CMD에서 ANSI 색상 활성화
if os.name == 'nt':  # Windows
    import ctypes
    kernel32 = ctypes.windll.kernel32
    kernel32.SetConsoleMode(kernel32.GetStdHandle(-11), 7)

# ANSI 색상 코드 (Windows CMD 지원)
PINK = '\033[95m'           # 짙은 핑크 (Magenta)
PASTEL_GREEN = '\033[92m'   # 파스텔 초록 (Bright Green)
DARK_YELLOW = '\033[33m'    # 짙은 노란색
DARK_BROWN = '\033[38;5;94m'  # 진한 갈색
RESET = '\033[0m'
BOLD = '\033[1m'

def print_woopang_logo():
    """우팡 서버 로고 출력 - 티베탄 마스티프와 함께"""
    logo = f"""
{PINK}{BOLD}
████████████████████████████████████████████████████████████████████████████████
███                                                                          ███
███      {PASTEL_GREEN}██╗    ██╗ ██████╗  ██████╗ ██████╗  █████╗ ███╗   ██╗ ██████╗ {PINK}      ███
███      {PASTEL_GREEN}██║    ██║██╔═══██╗██╔═══██╗██╔══██╗██╔══██╗████╗  ██║██╔════╝ {PINK}      ███
███      {PASTEL_GREEN}██║ █╗ ██║██║   ██║██║   ██║██████╔╝███████║██╔██╗ ██║██║  ███╗{PINK}      ███
███      {PASTEL_GREEN}██║███╗██║██║   ██║██║   ██║██╔═══╝ ██╔══██║██║╚██╗██║██║   ██║{PINK}      ███
███      {PASTEL_GREEN}╚███╔███╔╝╚██████╔╝╚██████╔╝██║     ██║  ██║██║ ╚████║╚██████╔╝{PINK}      ███
███       {PASTEL_GREEN}╚══╝╚══╝  ╚═════╝  ╚═════╝ ╚═╝     ╚═╝  ╚═╝╚═╝  ╚═══╝ ╚═════╝ {PINK}      ███
███                                                                          ███
███                                                                          ███
███                                                                          ███
███      {DARK_YELLOW}Walking the Earth with Woopang (Love!) 💛{PINK}              {DARK_BROWN}{BOLD}▄▀▀▀▀▀▄{PINK}     ███
███                                                             {DARK_BROWN}█ ● ● █{PINK}     ███
███                                                             {DARK_BROWN}█  ▼  █{PINK}     ███
███                                                             {DARK_BROWN}█ ▀▀▀ █{PINK}     ███
███                                                             {DARK_BROWN}█▄▄█▄▄█{PINK}     ███
███                                                             {DARK_BROWN}▐█ █ █▌{PINK}     ███
███                                                             {DARK_BROWN}▐▌ ▌ ▐▌{PINK}     ███
███                                                                          ███
████████████████████████████████████████████████████████████████████████████████
{RESET}
"""
    print(logo)

def print_server_health_check():
    """서버 상태 체크 메시지 (10분마다 출력) - 간결하게"""
    current_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    cache_count = len(tour_api_cache)
    ssl_days = get_ssl_days_remaining()
    ssl_status = f"{ssl_days}일 남음" if ssl_days is not None else "확인 불가"
    
    message = f"""
{PASTEL_GREEN}[우팡 서버 상태]{RESET}
✔ 측정 시각:  {current_time}
✔ 서버 상태:  정상 작동 (Normal Operation)
✔ SSL 인증서:  {ssl_status} (자동 갱신 대기중)
✔ 캐시 항목:  {cache_count}개
  - Android FCM 푸시 알림:  대기
  - iOS APNs 푸시 알림:     대기
  - TourAPI 외부 데이터:    대기 (5분 TTL)
  - 위치 기반 알림 시스템:    대기
"""
    safe_print(message)

def start_health_check_thread():
    """10분마다 서버 상태 체크 스레드 시작"""
    def health_check_loop():
        while True:
            time.sleep(600)  # 10분 대기
            print_server_health_check()

    health_thread = threading.Thread(target=health_check_loop, daemon=True)
    health_thread.start()

def start_auto_renewal_thread():
    """SSL 만료 7일 전부터만 자동 갱신 스크립트 실행"""
    def renewal_check_loop():
        # 첫 실행 시 잠시 대기 (서버 부하 방지)
        time.sleep(10)
        
        while True:
            try:
                days_remaining = get_ssl_days_remaining()
                
                if days_remaining is not None and days_remaining > 7:
                    # 7일보다 많이 남았으면, (남은기간 - 7일) 만큼 대기
                    sleep_days = days_remaining - 7
                    # 안전을 위해 최대 1주일 단위로만 대기 (중간에 인증서가 바뀔 수도 있으므로)
                    if sleep_days > 7:
                        sleep_days = 7
                        
                    safe_print(f"[Info] SSL 인증서 유효기간 넉넉함 ({days_remaining}일). {sleep_days}일 뒤 다시 확인합니다.")
                    time.sleep(sleep_days * 24 * 60 * 60)
                else:
                    # 7일 이하로 남았거나 확인 불가 시 갱신 시도
                    safe_print(f"[Info] SSL 갱신 기간 도래 ({days_remaining}일 남음). 갱신을 시도합니다.")
                    run_auto_renewal_check()
                    # 갱신 시도 후에는 하루 뒤 재확인
                    time.sleep(24 * 60 * 60)
            except Exception as e:
                safe_print(f"[Error] SSL 스케줄러 오류: {e}")
                time.sleep(3600) # 오류 시 1시간 뒤 재시도

    renewal_thread = threading.Thread(target=renewal_check_loop, daemon=True)
    renewal_thread.start()


app = Flask(__name__)

# ==================== Rate Limiting 설정 (보안) ====================
if RATE_LIMITER_AVAILABLE:
    limiter = Limiter(
        app=app,
        key_func=get_remote_address,
        default_limits=["200 per minute", "5000 per hour"],  # 기본 제한
        storage_uri="memory://",
        strategy="fixed-window"
    )
    print("[Info] Rate Limiter 활성화됨")
else:
    limiter = None
    print("[Warning] flask-limiter 미설치 - Rate Limiting 비활성화")

# ==================== CORS 허용 도메인 설정 (보안) ====================
ALLOWED_ORIGINS = [
    "https://woopang.com",
    "https://www.woopang.com",
    "https://api.woopang.com",
    "http://localhost:3000",  # 개발용
    "http://127.0.0.1:3000",  # 개발용
]

def get_cors_origin(request_origin):
    """요청 Origin이 허용 목록에 있으면 반환, 아니면 None"""
    if request_origin in ALLOWED_ORIGINS:
        return request_origin
    # 개발 환경에서는 모든 localhost 허용
    if request_origin and ('localhost' in request_origin or '127.0.0.1' in request_origin):
        return request_origin
    return None

def add_cors_headers(response, allow_credentials=False):
    """CORS 헤더 추가 (허용된 Origin만)"""
    origin = request.headers.get('Origin')
    allowed_origin = get_cors_origin(origin)
    if allowed_origin:
        response.headers['Access-Control-Allow-Origin'] = allowed_origin
        response.headers['Access-Control-Allow-Methods'] = 'GET, POST, PUT, DELETE, OPTIONS'
        response.headers['Access-Control-Allow-Headers'] = 'Content-Type, Authorization'
        if allow_credentials:
            response.headers['Access-Control-Allow-Credentials'] = 'true'
    return response

# ==================== QQQQ Proxy (New Web CMD) ====================
@app.route('/QQQQ', defaults={'path': ''})
@app.route('/QQQQ/<path:path>', methods=['GET', 'POST', 'PATCH', 'DELETE'])
def qqqq_proxy(path):
    target_url = f"http://127.0.0.1:5099/{path}"
    try:
        headers = {k: v for k, v in request.headers if k.lower() != 'host'}
        if request.method == 'GET':
            resp = requests.get(target_url, headers=headers, params=request.args, timeout=30, stream=True)
        elif request.method == 'POST':
            if request.is_json:
                resp = requests.post(target_url, json=request.get_json(), headers=headers, timeout=60, stream=True)
            else:
                # Handle file uploads
                files = {}
                for key, file in request.files.items():
                    files[key] = (file.filename, file.read(), file.content_type)

                # Filter out content-type to let requests handle boundary
                upload_headers = {k: v for k, v in headers.items() if k.lower() != 'content-type'}
                resp = requests.post(target_url, data=request.form, files=files, headers=upload_headers, timeout=60, stream=True)
        elif request.method == 'PATCH':
            resp = requests.patch(target_url, json=request.get_json(), headers=headers, timeout=30)
        elif request.method == 'DELETE':
            resp = requests.delete(target_url, headers=headers, timeout=30)

        # Preserve cache control headers from QQQQ server
        excluded_headers = ['content-encoding', 'content-length', 'transfer-encoding', 'connection']
        response_headers = [(name, value) for (name, value) in resp.headers.items() if name.lower() not in excluded_headers]

        # Ensure cache busting headers are present
        cache_headers = {
            'Cache-Control': 'no-store, no-cache, must-revalidate, post-check=0, pre-check=0, max-age=0',
            'Pragma': 'no-cache',
            'Expires': '-1'
        }
        for name, value in cache_headers.items():
            if not any(h[0].lower() == name.lower() for h in response_headers):
                response_headers.append((name, value))

        return Response(resp.content, resp.status_code, headers=response_headers)
    except requests.exceptions.ConnectionError:
        return "QQQQ Server is not running on port 5099.", 503
    except Exception as e:
        safe_print(f"[QQQQ Proxy Error] {e}")
        traceback.print_exc()
        return f"QQQQ Proxy Error: {str(e)}", 500

# --- WEB AUTH BLUEPRINT ---
from web_auth_routes import web_auth_bp
app.register_blueprint(web_auth_bp)
app.register_blueprint(vdown_bp, url_prefix='/vdown')
app.register_blueprint(admin_bp)
app.register_blueprint(majang_bp, url_prefix='/majang')
app.register_blueprint(email_bp)

# ==================== Monitor Proxy ====================
@app.route('/monitor', defaults={'path': ''})
@app.route('/monitor/<path:path>', methods=['GET', 'POST'])
def monitor_proxy(path):
    monitor_url = f"http://127.0.0.1:5020/{path}" if path else "http://127.0.0.1:5020/"
    try:
        headers = {k: v for k, v in request.headers if k.lower() != 'host'}
        if request.method == 'GET':
            resp = requests.get(monitor_url, headers=headers, params=request.args, timeout=10)
        elif request.method == 'POST':
            if request.is_json:
                resp = requests.post(monitor_url, json=request.get_json(), headers=headers, timeout=60)
            else:
                resp = requests.post(monitor_url, data=request.form, headers=headers, timeout=60)
        
        excluded_headers = ['content-encoding', 'content-length', 'transfer-encoding', 'connection']
        response_headers = [(name, value) for (name, value) in resp.headers.items() if name.lower() not in excluded_headers]
        return Response(resp.content, resp.status_code, headers=response_headers)
    except Exception as e:
        return f"Monitor Proxy Error: {str(e)}", 500


# ==================== QCLI Proxy (Web CMD) ====================
@app.route('/qcli', defaults={'path': ''})
@app.route('/qcli/<path:path>')
def qcli_redirect(path):
    target = f"/QCLI/{path}" if path else "/QCLI"
    return redirect(target, code=301)

# ==================== Hiro (당근 채팅 증거자료) ====================
@app.route('/hiro')
def hiro_index():
    return send_from_directory(r'C:\woopang\guide\danggun', 'daangn_chat.html')

@app.route('/hiro/full')
def hiro_full():
    return send_from_directory(r'C:\woopang\guide\danggun', 'daangn_chat_full.html')

app.secret_key = os.getenv("FLASK_SECRET_KEY", "your-secret-key-here")

# Set 10GB limit for large file uploads (e.g., DongDong)
app.config['MAX_CONTENT_LENGTH'] = 10 * 1024 * 1024 * 1024 

env_path = r"C:\woopang\server\.env"

# APNs 설정 - .env 파일에서 읽기 (인코딩 처리)
try:
    load_dotenv(dotenv_path=env_path, encoding='cp949')
except:
    try:
        load_dotenv(dotenv_path=env_path, encoding='utf-8')
    except:
        pass  # 실패 시 기본값 사용
APNS_KEY_ID = os.getenv("APNS_KEY_ID")
APNS_TEAM_ID = os.getenv("APNS_TEAM_ID")
APNS_BUNDLE_ID = os.getenv("APNS_BUNDLE_ID")
APNS_KEY_FILE = os.getenv("APNS_KEY_FILE")
APNS_URL = "https://api.development.push.apple.com"
APNS_ENV = os.getenv("APNS_ENV", "development")

def generate_apns_token():
    if not os.path.exists(APNS_KEY_FILE):
        safe_print(f"[Error] APNs 키 파일을 찾을 수 없습니다: {APNS_KEY_FILE}")
        return None
    with open(APNS_KEY_FILE, 'r') as f:
        private_key = f.read()
    token = jwt.encode(
        {
            'iss': APNS_TEAM_ID,
            'iat': int(datetime.now(timezone.utc).timestamp()),
            'exp': int((datetime.now(timezone.utc) + timedelta(hours=1)).timestamp())
        },
        private_key,
        algorithm='ES256',
        headers={'kid': APNS_KEY_ID}
    )
    return token

def safe_print(message, force_flush=True):
    """안전한 출력 함수 - 버퍼링 방지"""
    try:
        print(message)
        if force_flush:
            sys.stdout.flush()
    except:
        pass

# 로깅 설정
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('server.log', encoding='utf-8'),
        logging.StreamHandler(sys.stdout)
    ]
)

for handler in logging.root.handlers:
    if isinstance(handler, logging.StreamHandler):
        handler.setStream(sys.stdout)

werkzeug_logger = logging.getLogger('werkzeug')
werkzeug_logger.setLevel(logging.WARNING)
app.logger.setLevel(logging.WARNING)

health_check_count = 0
health_check_log_interval = 60

# .env 파일 로딩
try:
    safe_print("[Info] .env 파일 로딩 시작...")
    encodings = ['utf-8', 'cp949', 'euc-kr', 'utf-8-sig']

    for encoding in encodings:
        try:
            load_dotenv(dotenv_path=env_path, encoding=encoding, override=True)
            safe_print(f"[Info] .env 파일 로딩 성공: {encoding}")
            break
        except UnicodeDecodeError:
            continue
    else:
        safe_print("[Warning] 모든 인코딩 시도 실패, 기본값 사용")
        
except Exception as e:
    safe_print(f"[Error] .env 파일 로딩 실패: {e}")

# ============================================================
# 업로드 자동 승인 설정 (.env에서 읽기)
# True: 업로드 즉시 approved (마케팅 기간)
# False: pending 상태로 업로드, 관리자 승인 필요
# ============================================================
AUTO_APPROVE_UPLOADS = os.getenv('AUTO_APPROVE_UPLOADS', 'true').lower() == 'true'
safe_print(f"[Info] 자동 승인 설정: {AUTO_APPROVE_UPLOADS}")

# ============================================================
# Slack 알림 설정
# ============================================================
SLACK_WEBHOOK_URL = os.getenv('SLACK_WEBHOOK_URL')
if SLACK_WEBHOOK_URL:
    safe_print(f"[Info] Slack 알림 설정됨: {SLACK_WEBHOOK_URL[:50]}...")
else:
    safe_print("[Warning] SLACK_WEBHOOK_URL 미설정 - 알림이 전송되지 않습니다")

def send_slack_upload_notification(data):
    """업로드 완료 시 Slack 알림 전송"""
    if not SLACK_WEBHOOK_URL:
        safe_print("[Slack] Webhook URL 미설정 - 알림 스킵")
        return False

    try:
        # 모델 타입 정보
        model_type = data.get('model_type', 'cube')
        model_type_info = "🎨 커스텀 3D 모델" if model_type == 'custom' else "🧊 기본 큐브"

        payload = {
            "channel": "#admin-notifications",
            "text": "새로운 AR데이터가 업로드되었습니다!",
            "icon_emoji": ":rocket:",
            "username": "ARUploadBot",
            "blocks": [
                {
                    "type": "section",
                    "text": {
                        "type": "mrkdwn",
                        "text": f"*새로운 AR데이터가 업로드되었습니다!* {model_type_info}"
                    }
                },
                {
                    "type": "section",
                    "fields": [
                        {"type": "mrkdwn", "text": f"*ID*\n{data.get('id', 'N/A')}"},
                        {"type": "mrkdwn", "text": f"*이름*\n{data.get('name', 'N/A')}"},
                        {"type": "mrkdwn", "text": f"*사용자*\n{data.get('username', '익명')}"},
                        {"type": "mrkdwn", "text": f"*좌표*\n{data.get('latitude', 0)}, {data.get('longitude', 0)}"},
                        {"type": "mrkdwn", "text": f"*폴더*\n{data.get('folder', 'N/A')}"},
                        {"type": "mrkdwn", "text": f"*상태*\n{data.get('status', 'pending')}"}
                    ]
                }
            ]
        }

        response = requests.post(SLACK_WEBHOOK_URL, json=payload, timeout=10)

        if response.status_code == 200:
            safe_print(f"[Slack] ✅ 업로드 알림 전송 성공 - ID: {data.get('id')}")
            return True
        else:
            safe_print(f"[Slack] ❌ 알림 전송 실패 - 상태코드: {response.status_code}")
            return False

    except Exception as e:
        safe_print(f"[Slack] ❌ 알림 전송 오류: {e}")
        return False

def send_slack_delete_notification(data):
    """Location 삭제 시 Slack 알림 전송"""
    if not SLACK_WEBHOOK_URL:
        return False

    try:
        payload = {
            "channel": "#admin-notifications",
            "text": "Location 데이터가 삭제되었습니다!",
            "icon_emoji": ":wastebasket:",
            "username": "ARDeleteBot",
            "blocks": [
                {
                    "type": "section",
                    "text": {"type": "mrkdwn", "text": "*Location 데이터가 삭제되었습니다!*"}
                },
                {
                    "type": "section",
                    "fields": [
                        {"type": "mrkdwn", "text": f"*삭제된 ID*\n{data.get('id', 'N/A')}"},
                        {"type": "mrkdwn", "text": f"*이름*\n{data.get('name', '없음')}"},
                        {"type": "mrkdwn", "text": f"*사용자*\n{data.get('username', '익명')}"},
                        {"type": "mrkdwn", "text": f"*좌표*\n{data.get('latitude', 0)}, {data.get('longitude', 0)}"},
                        {"type": "mrkdwn", "text": f"*폴더*\n{data.get('folder', '없음')}"},
                        {"type": "mrkdwn", "text": f"*상태*\n{data.get('status', 'N/A')}"}
                    ]
                }
            ]
        }

        response = requests.post(SLACK_WEBHOOK_URL, json=payload, timeout=10)
        return response.status_code == 200

    except Exception as e:
        safe_print(f"[Slack] 삭제 알림 오류: {e}")
        return False

def send_slack_fix_request_notification(data):
    """수정/삭제 요청 시 Slack 알림 전송"""
    if not SLACK_WEBHOOK_URL:
        return False

    try:
        request_type = "삭제 요청" if data.get('remove_request') else "수정 요청"
        target_info = f"대상 ID: {data.get('target_id')}" if data.get('target_id', -1) > 0 else "새로운 데이터"
        emoji = ":x:" if data.get('remove_request') else ":pencil2:"

        payload = {
            "channel": "#admin-notifications",
            "text": f"{request_type}이 접수되었습니다!",
            "icon_emoji": emoji,
            "username": "ARFixBot",
            "blocks": [
                {
                    "type": "section",
                    "text": {"type": "mrkdwn", "text": f"*{request_type}이 접수되었습니다!*"}
                },
                {
                    "type": "section",
                    "fields": [
                        {"type": "mrkdwn", "text": f"*요청 ID*\n{data.get('id', 'N/A')}"},
                        {"type": "mrkdwn", "text": f"*대상*\n{target_info}"},
                        {"type": "mrkdwn", "text": f"*사용자*\n{data.get('username', '익명')}"},
                        {"type": "mrkdwn", "text": f"*장소명*\n{data.get('name', '미제공')}"},
                        {"type": "mrkdwn", "text": f"*반려동물*\n{'O' if data.get('pet_friendly') else 'X'}"},
                        {"type": "mrkdwn", "text": f"*화장실 분리*\n{'O' if data.get('separate_restroom') else 'X'}"},
                        {"type": "mrkdwn", "text": f"*인스타그램*\n{data.get('instagram_id') or 'X'}"},
                        {"type": "mrkdwn", "text": f"*설명*\n{data.get('description') or '없음'}"}
                    ]
                }
            ]
        }

        response = requests.post(SLACK_WEBHOOK_URL, json=payload, timeout=10)
        return response.status_code == 200

    except Exception as e:
        safe_print(f"[Slack] 수정요청 알림 오류: {e}")
        return False

# 데이터베이스 설정
try:
    db_password = os.getenv("DB_PASSWORD")
    if not db_password:
        raise ValueError("환경변수 누락: DB_PASSWORD")

    DB_CONFIG = {
        "database": "ar_database",
        "user": "postgres",
        "password": db_password,
        "host": "210.105.65.145",
        "port": "5432"
    }
    safe_print("[Info] 데이터베이스 설정 완료")

    # DB 연결 풀 생성 (멀티 스레드 환경용)
    # minconn: 최소 연결 수, maxconn: 최대 연결 수
    DB_POOL = psycopg2_pool.ThreadedConnectionPool(
        minconn=10,
        maxconn=100,
        **DB_CONFIG
    )
    safe_print("[Info] DB 연결 풀 생성 완료 (10-100 connections)")

except Exception as e:
    safe_print(f"[Error] 데이터베이스 설정 실패: {e}")
    DB_POOL = None

UPLOAD_FOLDER = r"C:\woopang\server\uploads"
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
app.config['UPLOAD_FOLDER'] = UPLOAD_FOLDER

HOME_DIR = r"C:\woopang\server\home"
app.static_folder = os.path.join(HOME_DIR, 'static')
app.template_folder = os.path.join(HOME_DIR, 'templates')

safe_print(f"[Info] Static folder: {app.static_folder}")
safe_print(f"[Info] Templates folder: {app.template_folder}")

COLOR_MAP = {
    "pink": "e95383",
    "yellow": "fbc15d",
    "green": "3da29c",
    "purple": "ae53c5",
    "blue": "44619b",
    "dark": "6a493c",
    "black": "202020"
    # "pink": "d92898"  # DEPRECATED: PINk 기능 제거됨
}
# 기존 설정 섹션에 추가
try:
    redis_conn = redis.Redis(host='localhost', port=6379, db=0)
    job_queue = Queue(connection=redis_conn)
    genai.configure(api_key=os.getenv("GEMINI_API_KEY"))
    safe_print("[Info] Preview 서비스 구성 요소 초기화 완료")
except Exception as e:
    safe_print(f"[Warning] Preview 서비스 초기화 실패: {e}")

# 사용자 인증 관련 함수들 추가
def login_required(f):
    @wraps(f)
    def decorated_function(*args, **kwargs):
        if 'user_id' not in session:
            return jsonify({'error': 'Login required'}), 401
        return f(*args, **kwargs)
    return decorated_function


# Firebase 초기화
try:
    safe_print("[Info] Firebase 초기화 시작...")
    firebase_key_path = 'C:/woopang/server/serviceAccountKey.json'
    
    if not os.path.exists(firebase_key_path):
        safe_print(f"[Warning] Firebase 키 파일이 없습니다: {firebase_key_path}")
        safe_print("[Info] Firebase 없이 계속 진행...")
    else:
        if not firebase_admin._apps:
            cred = credentials.Certificate(firebase_key_path)
            firebase_admin.initialize_app(cred)
            safe_print("[Info] Firebase 초기화 완료!")
        else:
            safe_print("[Info] Firebase 이미 초기화됨")
            
except Exception as e:
    safe_print(f"[Error] Firebase 초기화 실패: {e}")
    safe_print("[Info] Firebase 없이 계속 진행...")

# APNs JWT 토큰 생성 함수
def create_apns_jwt_token():
    try:
        if not os.path.exists(APNS_KEY_FILE):
            safe_print(f"[Error] APNs 키 파일이 없습니다: {APNS_KEY_FILE}")
            return None
        
        with open(APNS_KEY_FILE, 'rb') as f:
            private_key_data = f.read()
        
        from cryptography.hazmat.primitives import serialization
        private_key = serialization.load_pem_private_key(
            private_key_data,
            password=None,
        )
        
        current_time = int(time.time())
        
        payload = {
            'iss': APNS_TEAM_ID,
            'iat': current_time,
            'exp': current_time + 3600
        }
        
        headers = {
            'alg': 'ES256',
            'kid': APNS_KEY_ID,
            'typ': 'JWT'
        }
        
        token = jwt.encode(
            payload,
            private_key,
            algorithm='ES256',
            headers=headers
        )
        
        if isinstance(token, bytes):
            token = token.decode('utf-8')
        
        safe_print(f"[Info] APNs JWT 토큰 생성 성공")
        return token
        
    except ImportError as ie:
        safe_print(f"[Error] 필요한 라이브러리 없음: {ie}")
        return None
    except Exception as e:
        safe_print(f"[Error] APNs JWT 토큰 생성 실패: {e}")
        return None

def cleanup_old_data_on_startup():
    try:
        conn = get_db_connection()
        cursor = conn.cursor()
        
        six_months_ago = datetime.now(timezone.utc) - timedelta(days=180)
        cursor.execute("""
            DELETE FROM tokens 
            WHERE updated_at < %s 
            AND location_consent = 1
        """, (six_months_ago,))
        
        conn.commit()
        deleted_count = cursor.rowcount
        cursor.close()
        conn.close()
        
        safe_print(f"[Info] 오래된 좌표 데이터 정리 완료: {deleted_count}개 레코드 삭제")
    except Exception as e:
        safe_print(f"[Error] 오래된 데이터 정리 실패: {e}")

def init_tables():
    """Initialize database tables for comments, likes, and users."""
    try:
        conn = get_db_connection()
        cursor = conn.cursor()
        
        # Comments Table
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS comments (
                id SERIAL PRIMARY KEY,
                location_id INTEGER NOT NULL,
                user_id TEXT NOT NULL,
                username TEXT,
                content TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY(location_id) REFERENCES locations(id) ON DELETE CASCADE
            );
        """)

        # Comment Likes Table
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS comment_likes (
                comment_id INTEGER NOT NULL,
                user_id TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (comment_id, user_id),
                FOREIGN KEY(comment_id) REFERENCES comments(id) ON DELETE CASCADE
            );
        """)

        # Location Likes Table
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS location_likes (
                location_id INTEGER NOT NULL,
                user_id TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (location_id, user_id),
                FOREIGN KEY(location_id) REFERENCES locations(id) ON DELETE CASCADE
            );
        """)

        # Users Table
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS users (
                id SERIAL PRIMARY KEY,
                email TEXT UNIQUE NOT NULL,
                password_hash TEXT,
                username TEXT NOT NULL,
                provider TEXT DEFAULT 'email',
                social_id TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
        """)
        
        # Migration: Add columns if they don't exist (Idempotent)
        try:
            cursor.execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS provider TEXT DEFAULT 'email';")
            cursor.execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS social_id TEXT;")
            cursor.execute("ALTER TABLE users ALTER COLUMN password_hash DROP NOT NULL;")
        except Exception as e:
            safe_print(f"[Info] Migration note: {e}")

        conn.commit()
        cursor.close()
        conn.close()
        safe_print("[Info] Comment, Like, and User tables initialized successfully.")

        # User Profile Extended + Follow System
        conn = get_db_connection()
        cursor = conn.cursor()

        # users 테이블에 프로필 컬럼 추가
        try:
            cursor.execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar_url TEXT;")
            cursor.execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS bio TEXT;")
            cursor.execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS phone TEXT;")
            cursor.execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS instagram_id TEXT;")
            cursor.execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS facebook_id TEXT;")
            cursor.execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS x_id TEXT;")
            cursor.execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS followers_count INTEGER DEFAULT 0;")
            cursor.execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS following_count INTEGER DEFAULT 0;")
        except Exception as e:
            safe_print(f"[Info] User profile migration: {e}")

        # locations 테이블에 device_id 컬럼 추가 (업로더 추적용)
        try:
            cursor.execute("ALTER TABLE locations ADD COLUMN IF NOT EXISTS device_id TEXT;")
            cursor.execute("CREATE INDEX IF NOT EXISTS idx_locations_device_id ON locations(device_id);")
            safe_print("[Info] locations 테이블에 device_id 컬럼 추가됨")
        except Exception as e:
            safe_print(f"[Info] Locations migration note: {e}")

        # 팔로우 테이블 생성
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS user_follows (
                id SERIAL PRIMARY KEY,
                follower_id TEXT NOT NULL,
                following_id TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(follower_id, following_id)
            );
            CREATE INDEX IF NOT EXISTS idx_follows_follower ON user_follows(follower_id);
            CREATE INDEX IF NOT EXISTS idx_follows_following ON user_follows(following_id);
        """)

        conn.commit()
        cursor.close()
        conn.close()
        safe_print("[Info] User profile and follow system initialized.")

        # 좋아요 테이블 (DEPRECATED - 기능 제거됨)
        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            CREATE TABLE IF NOT EXISTS user_likes (
                id SERIAL PRIMARY KEY,
                liker_id TEXT NOT NULL,
                liked_id TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(liker_id, liked_id)
            );
            CREATE INDEX IF NOT EXISTS idx_likes_liker ON user_likes(liker_id);
            CREATE INDEX IF NOT EXISTS idx_likes_liked ON user_likes(liked_id);
        """)

        conn.commit()
        cursor.close()
        conn.close()
        safe_print("[Info] User likes system initialized.")

        # P2P 다이렉트 메시지 테이블 추가
        conn = get_db_connection()
        cursor = conn.cursor()
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS direct_messages (
                id SERIAL PRIMARY KEY,
                sender_id TEXT NOT NULL,
                recipient_id TEXT NOT NULL,
                content TEXT NOT NULL,
                is_read BOOLEAN DEFAULT FALSE,
                is_liked BOOLEAN DEFAULT FALSE,
                read_at TIMESTAMP,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
            -- is_liked 컬럼 추가 (기존 테이블 마이그레이션)
            ALTER TABLE direct_messages ADD COLUMN IF NOT EXISTS is_liked BOOLEAN DEFAULT FALSE;
            CREATE INDEX IF NOT EXISTS idx_dm_sender ON direct_messages(sender_id);
            CREATE INDEX IF NOT EXISTS idx_dm_recipient ON direct_messages(recipient_id);
            CREATE INDEX IF NOT EXISTS idx_dm_created ON direct_messages(created_at DESC);
        """)
        conn.commit()
        cursor.close()
        conn.close()
        safe_print("[Info] Direct messages table initialized.")

        # 관리자 공지 테이블 추가
        conn = get_db_connection()
        cursor = conn.cursor()
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS admin_broadcasts (
                id SERIAL PRIMARY KEY,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                sender_name TEXT DEFAULT 'WOOPANG',
                priority INTEGER DEFAULT 0,
                target_lang TEXT,
                latitude DOUBLE PRECISION,
                longitude DOUBLE PRECISION,
                radius DOUBLE PRECISION,
                is_active BOOLEAN DEFAULT TRUE,
                expires_at TIMESTAMP,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_broadcast_active ON admin_broadcasts(is_active, priority DESC);
        """)
        conn.commit()
        cursor.close()
        conn.close()
        safe_print("[Info] Admin broadcasts table initialized.")

        # 사용자 차단 테이블 추가
        conn = get_db_connection()
        cursor = conn.cursor()
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS user_blocks (
                id SERIAL PRIMARY KEY,
                blocker_id TEXT NOT NULL,
                blocked_id TEXT NOT NULL,
                reason TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(blocker_id, blocked_id)
            );
            CREATE INDEX IF NOT EXISTS idx_blocks_blocker ON user_blocks(blocker_id);
            CREATE INDEX IF NOT EXISTS idx_blocks_blocked ON user_blocks(blocked_id);
        """)
        conn.commit()
        cursor.close()
        conn.close()
        safe_print("[Info] User blocks table initialized.")

        # 업로드 기록 테이블 추가 (하루 1회 제한용)
        conn = get_db_connection()
        cursor = conn.cursor()
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS user_uploads (
                id SERIAL PRIMARY KEY,
                user_id TEXT NOT NULL,
                location_id INTEGER,
                upload_date DATE DEFAULT CURRENT_DATE,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(user_id, upload_date)
            );
            CREATE INDEX IF NOT EXISTS idx_uploads_user ON user_uploads(user_id);
            CREATE INDEX IF NOT EXISTS idx_uploads_date ON user_uploads(upload_date);
        """)
        conn.commit()
        cursor.close()
        conn.close()
        safe_print("[Info] User uploads table initialized.")

        # 공공시설(버스, 기차, 지하철 등) 통합 테이블 추가
        conn = get_db_connection()
        cursor = conn.cursor()
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS public_facilities (
                id SERIAL PRIMARY KEY,
                type TEXT NOT NULL, -- bus, train, subway, terminal
                name TEXT NOT NULL,
                latitude DOUBLE PRECISION NOT NULL,
                longitude DOUBLE PRECISION NOT NULL,
                address TEXT,
                extra_info TEXT, -- 노선 번호나 기타 정보
                main_photo TEXT, -- 시설 대표 사진 URL
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                altitude DOUBLE PRECISION DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_facilities_coords ON public_facilities (latitude, longitude);
        """)
        conn.commit()
        cursor.close()
        conn.close()
        safe_print("[Info] Public facilities table initialized.")

    except Exception as e:
        safe_print(f"[Error] Failed to initialize tables: {e}")

# --- 공공시설 검색 API (DB 기반) ---
@app.route('/api/nearby-facilities', methods=['GET'])
def get_nearby_facilities():
    conn = None
    try:
        lat = request.args.get('lat', type=float)
        lon = request.args.get('lon', type=float)
        radius = request.args.get('radius', type=float, default=1000) # 기본 1km
        facility_type = request.args.get('type', type=str) # optional: bus, train 등

        if lat is None or lon is None:
            return jsonify({"error": "lat and lon are required"}), 400

        # --- [최적화 추가] Bounding Box 필터링 ---
        # 1도당 약 111km (단순 계산을 통한 사각형 영역 생성)
        lat_range = radius / 111000.0
        lon_range = radius / (111000.0 * math.cos(math.radians(lat)))

        conn = get_db_connection()
        cursor = conn.cursor()

        # SQL 최적화: 인덱스가 걸린 latitude, longitude를 먼저 비교하여 후보군을 좁힘
        query = """
            SELECT * FROM (
                SELECT type, name, latitude, longitude, address, extra_info, altitude, main_photo,
                       (6371000 * acos(cos(radians(%s)) * cos(radians(latitude)) * cos(radians(longitude) - radians(%s)) + sin(radians(%s)) * sin(radians(latitude)))) AS distance
                FROM public_facilities
                WHERE latitude BETWEEN %s AND %s
                  AND longitude BETWEEN %s AND %s
        """
        params = [lat, lon, lat, lat - lat_range, lat + lat_range, lon - lon_range, lon + lon_range]

        if facility_type:
            query += " AND type = %s"
            params.append(facility_type)

        query += """
            ) AS calculated_distance
            WHERE distance <= %s
            ORDER BY distance ASC
            LIMIT 100
        """
        params.append(radius)

        cursor.execute(query, params)
        rows = cursor.fetchall()

        results = []
        for row in rows:
            results.append({
                "type": row[0],
                "name": row[1],
                "latitude": float(row[2]),
                "longitude": float(row[3]),
                "address": row[4],
                "extra_info": row[5],
                "altitude": float(row[6]) if row[6] else 0.0,
                "main_photo": row[7]
            })

        cursor.close()
        return jsonify(results)

    except Exception as e:
        safe_print(f"[Error] get_nearby_facilities failed: {e}")
        return jsonify({"error": str(e)}), 500
    finally:
        if conn:
            conn.close()

@app.route('/login/social', methods=['POST'])
def login_social():
    try:
        data = request.get_json()
        provider = data.get('provider') # google, kakao, apple
        social_id = data.get('social_id') # Unique ID from provider
        email = data.get('email')
        username = data.get('username')

        if not all([provider, social_id]):
            return jsonify({"error": "Missing provider or social_id"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 1. Try to find by social_id AND provider
        cursor.execute("SELECT id, username FROM users WHERE social_id = %s AND provider = %s", (social_id, provider))
        user = cursor.fetchone()

        # 2. If not found, try to find by email (Link account)
        if not user and email:
            cursor.execute("SELECT id, username FROM users WHERE email = %s", (email,))
            user = cursor.fetchone()
            if user:
                # Link account
                cursor.execute("UPDATE users SET social_id = %s, provider = %s WHERE id = %s", (social_id, provider, user[0]))
                conn.commit()

        # 3. If still not found, Register new user
        if not user:
            # Generate unique username if taken
            base_username = username if username else f"{provider}_user"
            final_username = base_username
            counter = 1
            while True:
                cursor.execute("SELECT 1 FROM users WHERE username = %s", (final_username,))
                if not cursor.fetchone():
                    break
                final_username = f"{base_username}{counter}"
                counter += 1
            
            # Insert
            # If email is missing (e.g. Kakao sometimes), create a dummy one or allow null if schema permits.
            # Here we assume email might be "social_id@provider.com" if missing.
            final_email = email if email else f"{social_id}@{provider}.com"
            
            cursor.execute("""
                INSERT INTO users (email, password_hash, username, provider, social_id)
                VALUES (%s, NULL, %s, %s, %s)
                RETURNING id, username
            """, (final_email, final_username, provider, social_id))
            
            user = cursor.fetchone()
            conn.commit()

        cursor.close()
        conn.close()

        return jsonify({
            "message": "Login successful",
            "user_id": str(user[0]),
            "username": user[1]
        }), 200

    except Exception as e:
        safe_print(f"[Error] Social Login failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/find-id', methods=['POST'])
def find_id():
    try:
        data = request.get_json()
        email = data.get('email')
        
        conn = get_db_connection()
        cursor = conn.cursor()
        cursor.execute("SELECT username, provider FROM users WHERE email = %s", (email,))
        user = cursor.fetchone()
        cursor.close()
        conn.close()

        if user:
            # In a real app, send email. For now, return it (INSECURE for prod, ok for dev/mock).
            # send_email(email, "Found ID", f"Your username is: {user[0]} (Provider: {user[1]})")
            return jsonify({"message": "User found", "username": user[0], "provider": user[1]}), 200
        else:
            return jsonify({"error": "User not found"}), 404
    except Exception as e:
        return jsonify({"error": str(e)}), 500

@app.route('/reset-password', methods=['POST'])
def reset_password():
    conn = None
    try:
        data = request.get_json()
        email = data.get('email')
        username = data.get('username')

        if not email or not username:
            return jsonify({"error": "Email and username are required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()
        cursor.execute("SELECT id FROM users WHERE email = %s AND username = %s", (email, username))
        user = cursor.fetchone()

        if user:
            # Generate temp password
            temp_pw = str(uuid.uuid4())[:8]
            hashed = bcrypt.hashpw(temp_pw.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')

            cursor.execute("UPDATE users SET password_hash = %s WHERE id = %s", (hashed, user[0]))
            conn.commit()
            cursor.close()

            # TODO: 실제 이메일 발송 구현 필요
            # send_email(email, "Password Reset", f"Your new password is: {temp_pw}")
            safe_print(f"[Info] Password reset requested for {email}")

            return jsonify({"message": "Password reset successful. Check your email."}), 200
        else:
            cursor.close()
            # 보안: 사용자 존재 여부를 노출하지 않음
            return jsonify({"message": "If the account exists, a reset email has been sent."}), 200
    except Exception as e:
        safe_print(f"[Error] Password reset failed: {e}")
        return jsonify({"error": "Password reset failed"}), 500
    finally:
        if conn:
            conn.close()

def check_system_time():
    from datetime import datetime as dt, timezone as tz
    now_utc = dt.now(tz.utc)
    now_local = dt.now()
    now_timestamp = int(now_utc.timestamp())
    
    if 1704067200 <= now_timestamp <= 1861920000:
        return True
    else:
        safe_print(f"시간이 비정상입니다!")
        return False

def validate_apns_config():
    safe_print("[Debug] APNs 설정 검증 시작...")
    
    issues = []
    
    if not check_system_time():
        issues.append("시스템 시간이 비정상입니다")
    
    if not os.path.exists(APNS_KEY_FILE):
        issues.append(f"APNs 키 파일이 존재하지 않습니다: {APNS_KEY_FILE}")
        safe_print("[Error] APNs 설정 문제 발견:")
        for issue in issues:
            safe_print(f"  - {issue}")
        return False
    
    try:
        with open(APNS_KEY_FILE, 'r', encoding='utf-8') as f:
            key_content = f.read().strip()
        
        if not key_content.startswith('-----BEGIN PRIVATE KEY-----'):
            issues.append("키 파일 형식이 올바르지 않습니다 (PEM 헤더 없음)")
        
        if not key_content.endswith('-----END PRIVATE KEY-----'):
            issues.append("키 파일 형식이 올바르지 않습니다 (PEM 푸터 없음)")
            
    except Exception as e:
        issues.append(f"키 파일 읽기 실패: {e}")
    
    if not APNS_KEY_ID or len(APNS_KEY_ID) != 10:
        issues.append(f"Key ID 형식 오류: '{APNS_KEY_ID}' (10자리여야 함)")
    
    if not APNS_TEAM_ID or len(APNS_TEAM_ID) != 10:
        issues.append(f"Team ID 형식 오류: '{APNS_TEAM_ID}' (10자리여야 함)")
    
    if not APNS_BUNDLE_ID or not APNS_BUNDLE_ID.startswith('com.'):
        issues.append(f"Bundle ID 형식 오류: '{APNS_BUNDLE_ID}'")
    
    try:
        import jwt
        from cryptography.hazmat.primitives import serialization
    except ImportError as e:
        issues.append(f"필요한 라이브러리 없음: {e}")
    
    try:
        test_token = create_apns_jwt_token()
        if not test_token:
            issues.append("JWT 토큰 생성 실패")
    except Exception as e:
        issues.append(f"JWT 토큰 생성 오류: {e}")
    
    if issues:
        safe_print("[Error] APNs 설정 문제 발견:")
        for issue in issues:
            safe_print(f"  - {issue}")
        return False
    else:
        safe_print("[Info] APNs 설정 검증 완료")
        return True

def send_apns_notification_http2(device_token, title, body, environment=None, data=None):
    try:
        jwt_token = create_apns_jwt_token()
        if not jwt_token:
            return False
        
        if environment is None or not isinstance(environment, str):
            environment = "development"
        
        if environment == 'development':
            url = f"https://api.development.push.apple.com/3/device/{device_token}"
        else:
            url = f"https://api.push.apple.com/3/device/{device_token}"
            
        headers = {
            'authorization': f'bearer {jwt_token}',
            'apns-topic': APNS_BUNDLE_ID,
            'apns-push-type': 'alert',
            'apns-priority': '10',
            'apns-id': str(uuid.uuid4()),
            'content-type': 'application/json'
        }
        
        payload = {
            "aps": {
                "alert": {"title": title, "body": body},
                "sound": "default",
                "badge": 1,
                "content-available": 1,
                "mutable-content": 1
            }
        }
        
        if data:
            for key, value in data.items():
                payload[key] = value
        
        payload["title"] = title
        payload["body"] = body
        payload["click_action"] = "OPEN_MESSAGE_PANEL"
        
        with httpx.Client(http2=True, verify=True, timeout=30.0) as client:
            response = client.post(url, headers=headers, json=payload)
        
        if response.status_code == 200:
            safe_print(f"[Info] APNs {environment} HTTP/2 전송 성공: {device_token[:20]}...")
            return True
        else:
            safe_print(f"[Error] APNs 전송 실패: {response.status_code}")
            return False
            
    except Exception as e:
        safe_print(f"[Error] APNs {environment} HTTP/2 예상치 못한 오류: {str(e)}")
        return False

def send_social_notification(to_user_id, from_username, notification_type):
    """
    팔로우/좋아요 알림 전송
    notification_type: 'follow' or 'like'
    """
    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        # 알림 받을 사용자의 device_token 조회
        cursor.execute("SELECT device_token FROM users WHERE id::text = %s", (to_user_id,))
        result = cursor.fetchone()

        if result and result[0]:
            device_token = result[0]

            if notification_type == 'follow':
                title = "새로운 팔로워"
                body = f"{from_username}님이 회원님을 팔로우했습니다"
            elif notification_type == 'like':
                title = "좋아요"
                body = f"{from_username}님이 회원님을 좋아합니다"
            else:
                cursor.close()
                conn.close()
                return False

            # APNs 알림 전송
            success = send_apns_notification_http2(
                device_token, title, body, APNS_ENV,
                {"notification_type": notification_type, "from_username": from_username}
            )

            cursor.close()
            conn.close()
            return success

        cursor.close()
        conn.close()
        return False

    except Exception as e:
        safe_print(f"[Error] Social notification failed: {e}")
        return False


def send_fcm_notification(fcm_token, title, body, data=None):
    """
    Android FCM 푸시 알림 전송
    """
    try:
        if not firebase_admin._apps:
            safe_print("[Warning] Firebase가 초기화되지 않았습니다")
            return False

        message_data = {
            'title': title,
            'body': body,
            'click_action': 'OPEN_MESSAGE_PANEL'
        }

        if data:
            message_data.update({k: str(v) for k, v in data.items()})

        message = messaging.Message(
            data=message_data,
            android=messaging.AndroidConfig(
                priority='high',
                notification=messaging.AndroidNotification(
                    title=title,
                    body=body,
                    sound='default',
                    click_action='OPEN_MESSAGE_PANEL'
                )
            ),
            token=fcm_token
        )

        response = messaging.send(message)
        safe_print(f"[Info] FCM 전송 성공: {response}")
        return True

    except Exception as e:
        safe_print(f"[Error] FCM 전송 실패: {e}")
        return False


def send_dm_notification(recipient_id, sender_username, message_preview, sender_id=None, message_id=None):
    """
    DM 수신 시 푸시 알림 전송 (Android FCM + iOS APNs)

    Args:
        recipient_id: 수신자 ID
        sender_username: 발신자 username
        message_preview: 메시지 미리보기
        sender_id: 발신자 ID (선택)
        message_id: 메시지 ID (선택)
    """
    safe_print(f"[DM_NOTIFY] 푸시 알림 전송 시작: recipient={recipient_id}, sender={sender_username}")

    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        # tokens 테이블에서 user_id로 직접 토큰 조회 (최신 토큰 우선)
        cursor.execute("""
            SELECT fcm_token, apns_token, platform, device_id
            FROM tokens
            WHERE user_id = %s
            ORDER BY updated_at DESC NULLS LAST, created_at DESC NULLS LAST
            LIMIT 1
        """, (str(recipient_id),))
        token_row = cursor.fetchone()

        fcm_token = None
        apns_token = None
        platform = 'unknown'

        if token_row:
            fcm_token = token_row[0]
            apns_token = token_row[1]
            platform = token_row[2] or 'unknown'
            device_id = token_row[3]
            safe_print(f"[DM_NOTIFY] 토큰 조회 성공: user_id={recipient_id}, device_id={device_id}")
        else:
            safe_print(f"[DM_NOTIFY] tokens 테이블에서 user_id={recipient_id}에 해당하는 토큰 없음")

        cursor.close()
        conn.close()

        if not fcm_token and not apns_token:
            safe_print(f"[DM_NOTIFY] 수신자 {recipient_id}의 푸시 토큰 없음 - 알림 전송 불가")
            return False

        safe_print(f"[DM_NOTIFY] 토큰 조회 완료: fcm={bool(fcm_token)}, apns={bool(apns_token)}, platform={platform}")

        title = "새 메시지"
        body = f"{sender_username}: {message_preview[:50]}..." if len(message_preview) > 50 else f"{sender_username}: {message_preview}"

        success = False

        # Android FCM 알림
        if fcm_token:
            try:
                # 확장된 데이터 페이로드
                fcm_data = {
                    "msg_type": "dm",
                    "type": "dm",
                    "sender": sender_username,
                    "sender_username": sender_username
                }
                if sender_id:
                    fcm_data["sender_id"] = str(sender_id)
                if message_id:
                    fcm_data["message_id"] = str(message_id)
                    fcm_data["conversation_id"] = str(message_id)

                safe_print(f"[DM_NOTIFY] FCM 데이터: {fcm_data}")

                fcm_success = send_fcm_notification(fcm_token, title, body, fcm_data)
                if fcm_success:
                    success = True
                    safe_print(f"[DM_NOTIFY] FCM 알림 전송 성공: recipient={recipient_id}, token={fcm_token[:20]}...")
                else:
                    safe_print(f"[DM_NOTIFY] FCM 알림 전송 실패: recipient={recipient_id}")
            except Exception as e:
                safe_print(f"[DM_NOTIFY] FCM 전송 예외: {e}")

        # iOS APNs 알림
        if apns_token:
            try:
                # 확장된 데이터 페이로드 (iOS용)
                apns_data = {
                    "msg_type": "dm",
                    "type": "dm",
                    "sender": sender_username,
                    "sender_username": sender_username
                }
                if sender_id:
                    apns_data["sender_id"] = str(sender_id)
                if message_id:
                    apns_data["message_id"] = str(message_id)
                    apns_data["conversation_id"] = str(message_id)

                safe_print(f"[DM_NOTIFY] APNs 데이터: {apns_data}")

                apns_success = send_apns_notification_http2(
                    apns_token, title, body, APNS_ENV, apns_data
                )
                if apns_success:
                    success = True
                    safe_print(f"[DM_NOTIFY] APNs 알림 전송 성공: recipient={recipient_id}")
                else:
                    safe_print(f"[DM_NOTIFY] APNs 알림 전송 실패: recipient={recipient_id}")
            except Exception as e:
                safe_print(f"[DM_NOTIFY] APNs 전송 예외: {e}")

        return success

    except Exception as e:
        safe_print(f"[Error] DM notification failed: {e}")
        return False


def get_address_from_coordinates(latitude, longitude):
    try:
        lat = round(float(latitude), 2)
        lon = round(float(longitude), 2)
        
        url = f"https://nominatim.openstreetmap.org/reverse"
        params = {
            'format': 'json',
            'lat': lat,
            'lon': lon,
            'zoom': 12,
            'addressdetails': 1,
            'accept-language': 'ko'
        }
        
        headers = {
            'User-Agent': 'WoopangServer/1.0'
        }
        
        response = requests.get(url, params=params, headers=headers, timeout=5)
        
        if response.status_code == 200:
            data = response.json()
            
            if 'display_name' in data:
                address = data['display_name']
                
                import re
                
                address = re.sub(r', \d{5},', ',', address)
                address = re.sub(r', \d{5}$', '', address)
                address = re.sub(r', 대한민국$', '', address)
                address = re.sub(r', South Korea$', '', address)
                address = re.sub(r', Republic of Korea$', '', address)
                address = re.sub(r',\s*,', ',', address)
                address = address.strip(', ')
                
                return address
            else:
                return f"위도 {lat}, 경도 {lon}"
        else:
            return f"위도 {lat}, 경도 {lon}"
            
    except Exception as e:
        safe_print(f"주소 변환 실패: {e}")
        return f"위도 {latitude}, 경도 {longitude}"

def save_token_with_coordinates(device_id, platform, fcm_token=None, apns_token=None,
                               device_name=None, device_model=None, os_version=None, app_version=None,
                               latitude=None, longitude=None, location_consent=False, user_id=None):
    conn = None
    try:
        safe_print(f"[Info] 토큰 저장 시작: {platform} - {device_id[:20]}...")
        
        location_address = ""
        if latitude is not None and longitude is not None and location_consent:
            try:
                location_address = get_address_from_coordinates(latitude, longitude)
                time.sleep(1)
            except Exception as addr_error:
                safe_print(f"주소 변환 실패, 계속 진행: {addr_error}")
                location_address = f"위도 {latitude}, 경도 {longitude}"
        
        conn = get_db_connection()
        cursor = conn.cursor()
        
        location_consent_int = 1 if location_consent else 0
        
        if latitude is not None and longitude is not None and location_consent:
            upsert_query = """
                INSERT INTO tokens (
                    device_id, platform, fcm_token, apns_token, device_name, device_model,
                    os_version, app_version, location_consent, latitude, longitude,
                    user_id, last_active, created_at, updated_at
                )
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, NOW(), NOW(), NOW())
                ON CONFLICT (device_id) DO UPDATE SET
                    platform = EXCLUDED.platform,
                    fcm_token = COALESCE(EXCLUDED.fcm_token, tokens.fcm_token),
                    apns_token = COALESCE(EXCLUDED.apns_token, tokens.apns_token),
                    device_name = COALESCE(EXCLUDED.device_name, tokens.device_name),
                    device_model = COALESCE(EXCLUDED.device_model, tokens.device_model),
                    os_version = COALESCE(EXCLUDED.os_version, tokens.os_version),
                    app_version = COALESCE(EXCLUDED.app_version, tokens.app_version),
                    location_consent = EXCLUDED.location_consent,
                    latitude = EXCLUDED.latitude,
                    longitude = EXCLUDED.longitude,
                    user_id = COALESCE(EXCLUDED.user_id, tokens.user_id),
                    last_active = NOW(),
                    updated_at = NOW()
            """

            cursor.execute(upsert_query, (
                device_id, platform, fcm_token, apns_token, device_name, device_model,
                os_version, app_version, location_consent_int, latitude, longitude, user_id
            ))
            
            if location_address:
                try:
                    cursor.execute("""
                        UPDATE tokens 
                        SET location_address = %s, updated_at = NOW()
                        WHERE device_id = %s
                    """, (location_address, device_id))
                except Exception as update_error:
                    safe_print(f"[Warning] 주소 업데이트 실패: {update_error}")
        else:
            upsert_query = """
                INSERT INTO tokens (
                    device_id, platform, fcm_token, apns_token, device_name, device_model,
                    os_version, app_version, location_consent, user_id,
                    last_active, created_at, updated_at
                )
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, NOW(), NOW(), NOW())
                ON CONFLICT (device_id) DO UPDATE SET
                    platform = EXCLUDED.platform,
                    fcm_token = COALESCE(EXCLUDED.fcm_token, tokens.fcm_token),
                    apns_token = COALESCE(EXCLUDED.apns_token, tokens.apns_token),
                    device_name = COALESCE(EXCLUDED.device_name, tokens.device_name),
                    device_model = COALESCE(EXCLUDED.device_model, tokens.device_model),
                    os_version = COALESCE(EXCLUDED.os_version, tokens.os_version),
                    app_version = COALESCE(EXCLUDED.app_version, tokens.app_version),
                    location_consent = EXCLUDED.location_consent,
                    user_id = COALESCE(EXCLUDED.user_id, tokens.user_id),
                    last_active = NOW(),
                    updated_at = NOW()
            """

            cursor.execute(upsert_query, (
                device_id, platform, fcm_token, apns_token, device_name, device_model,
                os_version, app_version, location_consent_int, user_id
            ))
        
        conn.commit()
        cursor.close()

        location_info = f"({latitude}, {longitude})" if latitude and longitude else "없음"
        safe_print(f"[Info] 토큰 저장 완료: {platform} - {device_id[:20]}... (위치: {location_info})")
        return True

    except Exception as e:
        safe_print(f"[Error] 토큰 저장 실패: {e}")
        return False
    finally:
        if conn:
            conn.close()

def calculate_distance(lat1, lon1, lat2, lon2):
    lat1 = float(lat1)
    lon1 = float(lon1)
    lat2 = float(lat2)
    lon2 = float(lon2)
    
    R = 6371000
    phi1 = math.radians(lat1)
    phi2 = math.radians(lat2)
    delta_phi = math.radians(lat2 - lat1)
    delta_lambda = math.radians(lon2 - lon1)
    
    a = math.sin(delta_phi / 2) ** 2 + math.cos(phi1) * math.cos(phi2) * math.sin(delta_lambda / 2) ** 2
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))
    distance = R * c
    
    return distance

def get_tokens_by_location_radius(target_lat, target_lon, radius_meters):
    try:
        conn = get_db_connection()
        cursor = conn.cursor()
        
        target_lat = Decimal(str(target_lat))
        target_lon = Decimal(str(target_lon))
        radius_meters = Decimal(str(radius_meters))
        
        lat_range = radius_meters / Decimal('111000')
        lon_range = radius_meters / (Decimal('111000') * Decimal(str(math.cos(math.radians(float(target_lat))))))
        
        cursor.execute("""
            SELECT device_id, platform, fcm_token, apns_token, latitude, longitude 
            FROM tokens 
            WHERE location_consent = 1
            AND latitude IS NOT NULL 
            AND longitude IS NOT NULL
            AND latitude BETWEEN %s AND %s
            AND longitude BETWEEN %s AND %s
        """, (
            float(target_lat - lat_range), float(target_lat + lat_range),
            float(target_lon - lon_range), float(target_lon + lon_range)
        ))
        
        results = cursor.fetchall()
        cursor.close()
        conn.close()
        
        android_tokens = []
        ios_tokens = []
        
        for row in results:
            device_id, platform, fcm_token, apns_token, lat, lon = row
            lat = float(lat)
            lon = float(lon)
            distance = calculate_distance(float(target_lat), float(target_lon), lat, lon)
            if distance <= float(radius_meters):
                if platform == 'android' and fcm_token:
                    android_tokens.append(fcm_token)
                elif platform == 'ios' and apns_token:
                    ios_tokens.append(apns_token)
        
        safe_print(f"[Info] 좌표 기반 토큰 조회: ({float(target_lat)}, {float(target_lon)}) 반경 {float(radius_meters)}m → Android: {len(android_tokens)}, iOS: {len(ios_tokens)}")
        return {
            'android_tokens': android_tokens,
            'ios_tokens': ios_tokens
        }
    
    except Exception as e:
        safe_print(f"[Error] 좌표 기반 토큰 조회 실패: {e}")
        return {'android_tokens': [], 'ios_tokens': []}

def get_tokens_by_platform(platform):
    try:
        conn = get_db_connection()
        cursor = conn.cursor()
        
        if platform == 'ios':
            cursor.execute("SELECT apns_token FROM tokens WHERE platform = 'ios' AND apns_token IS NOT NULL")
            tokens = [row[0] for row in cursor.fetchall()]
        elif platform == 'android':
            cursor.execute("SELECT fcm_token FROM tokens WHERE platform = 'android' AND fcm_token IS NOT NULL")
            tokens = [row[0] for row in cursor.fetchall()]
        else:
            tokens = []
        
        cursor.close()
        conn.close()
        
        safe_print(f"[Info] {platform} 토큰 조회: {len(tokens)}개")
        return tokens
        
    except Exception as e:
        safe_print(f"[Error] {platform} 토큰 조회 실패: {e}")
        return []

def background_cleanup_scheduler():
    while True:
        try:
            time.sleep(24 * 60 * 60)
            
            safe_print("[Info] 정기 위치 데이터 정리 시작...")
            
            conn = get_db_connection()
            cursor = conn.cursor()
            
            cursor.execute("""
                UPDATE tokens 
                SET latitude = NULL, longitude = NULL
                WHERE updated_at < NOW() - INTERVAL '6 months'
                AND latitude IS NOT NULL
            """)
            
            cleaned_count = cursor.rowcount
            conn.commit()
            cursor.close()
            conn.close()
            
            if cleaned_count > 0:
                safe_print(f"[Info] 정기 위치 데이터 정리 완료: {cleaned_count}개")
            else:
                safe_print("[Info] 정기 정리: 삭제할 오래된 위치 데이터 없음")
            
        except Exception as e:
            safe_print(f"[Error] 정기 데이터 정리 실패: {e}")

# 연결 풀 래퍼 클래스 - close() 호출 시 풀에 자동 반환
class PooledConnection:
    """DB 연결 풀 래퍼 - close() 호출 시 풀에 반환"""
    def __init__(self, conn, pool):
        self._conn = conn
        self._pool = pool
        self._closed = False

    def close(self):
        """연결을 닫는 대신 풀에 반환"""
        if not self._closed and self._pool and self._conn:
            try:
                self._pool.putconn(self._conn)
                self._closed = True
            except Exception as e:
                safe_print(f"[Error] DB 연결 반환 실패: {e}")

    def __del__(self):
        """GC 시 연결 자동 반환 (안전장치)"""
        if not self._closed:
            self.close()

    def cursor(self, *args, **kwargs):
        return self._conn.cursor(*args, **kwargs)

    def commit(self):
        return self._conn.commit()

    def rollback(self):
        return self._conn.rollback()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()
        return False

# 데이터베이스 연결 함수 (연결 풀 사용)
def get_db_connection(max_retries=3, retry_delay=0.5):
    """
    DB 연결 풀에서 연결 가져오기 (재시도 로직 포함)
    close() 호출 시 자동으로 풀에 반환됨
    """
    import time

    for attempt in range(max_retries):
        try:
            if DB_POOL:
                conn = DB_POOL.getconn()
                # 래퍼로 감싸서 close() 시 풀에 반환되도록
                return PooledConnection(conn, DB_POOL)
            else:
                # 풀이 없으면 직접 연결 (폴백)
                conn = psycopg2.connect(**DB_CONFIG)
                return conn
        except psycopg2_pool.PoolError as e:
            # 풀 고갈 시 재시도
            if attempt < max_retries - 1:
                safe_print(f"[Warning] DB 풀 고갈, 재시도 {attempt + 1}/{max_retries}")
                time.sleep(retry_delay * (attempt + 1))
            else:
                safe_print(f"[Error] DB 연결 실패: {e}")
                raise
        except Psycopg2Error as e:
            safe_print(f"[Error] DB 연결 실패: {e}")
            raise

def return_db_connection(conn):
    """연결 풀에 연결 반환 (하위 호환성 유지)"""
    try:
        if conn:
            conn.close()  # PooledConnection.close()가 풀에 반환함
    except Exception as e:
        safe_print(f"[Error] DB 연결 반환 실패: {e}")

# 모니터링용 헬스체크 엔드포인트
@app.route('/health', methods=['GET'])
def health_check():
    global health_check_count
    health_check_count += 1

    request.is_health_check = True
    conn = None

    try:
        conn = get_db_connection()
        cursor = conn.cursor()
        cursor.execute("SELECT 1")
        cursor.fetchone()
        cursor.close()

        if health_check_count % health_check_log_interval == 0:
            safe_print(f"[Health] 정상 (총 {health_check_count}회)")

        return jsonify({
            "status": "healthy",
            "timestamp": datetime.now().isoformat(),
            "database": "connected",
            "mode": "coordinate_based_push",
            "port": request.environ.get('SERVER_PORT', '443'),
            "check_count": health_check_count
        })
    except Exception as e:
        safe_print(f"[Error] Health check failed: {e}")
        return jsonify({
            "status": "unhealthy",
            "error": str(e),
            "timestamp": datetime.now().isoformat(),
            "check_count": health_check_count
        }), 500
    finally:
        if conn:
            conn.close()

# ==================== 전역 에러 핸들러 (보안) ====================
@app.errorhandler(Exception)
def handle_exception(e):
    """모든 예외를 일반화된 메시지로 처리 (내부 정보 노출 방지)"""
    # 로그에는 상세 정보 기록
    safe_print(f"[Error] Unhandled exception: {type(e).__name__}: {e}")

    # 클라이언트에는 일반화된 메시지만 반환
    if isinstance(e, ValueError):
        return jsonify({"error": "Invalid input"}), 400
    elif isinstance(e, PermissionError):
        return jsonify({"error": "Access denied"}), 403
    elif isinstance(e, FileNotFoundError):
        return jsonify({"error": "Resource not found"}), 404
    else:
        return jsonify({"error": "Internal server error"}), 500

@app.errorhandler(429)
def ratelimit_handler(e):
    """Rate Limit 초과 시 처리"""
    return jsonify({"error": "Too many requests. Please try again later."}), 429

# 보안 헤더 및 CORS
@app.after_request
def apply_security_headers(response):
    # 보안 헤더
    response.headers['Strict-Transport-Security'] = 'max-age=31536000; includeSubDomains'
    response.headers['X-Content-Type-Options'] = 'nosniff'
    response.headers['X-Frame-Options'] = 'SAMEORIGIN'  # DENY에서 변경 (iframe 임베드 허용)

    # CORS 헤더 (허용된 Origin만)
    origin = request.headers.get('Origin')
    if origin:
        allowed_origin = get_cors_origin(origin)
        if allowed_origin:
            response.headers['Access-Control-Allow-Origin'] = allowed_origin
            response.headers['Access-Control-Allow-Methods'] = 'GET, POST, PUT, DELETE, OPTIONS'
            response.headers['Access-Control-Allow-Headers'] = 'Content-Type, Authorization, X-Requested-With'
        # 허용되지 않은 Origin은 CORS 헤더 없음 (브라우저가 차단)

    return response

# 이미지 요청 그룹화용 캐시 (IP별로 최근 요청 시간 추적)
image_request_tracker = {}
image_request_count = {}

# 요청 로깅 미들웨어
@app.before_request
def before_request():
    if not getattr(request, 'is_health_check', False) and request.endpoint != 'health_check':
        if request.endpoint != 'home':
            client_ip = request.environ.get('HTTP_X_FORWARDED_FOR', request.environ.get('REMOTE_ADDR', 'Unknown'))

            # 일반 요청 로그는 출력하지 않음 (에러만 after_request에서 출력)
            pass

@app.after_request
def after_request(response):
    if not getattr(request, 'is_health_check', False) and request.endpoint not in ['health_check', 'home']:
        # 이미지 요청 성공/실패 처리
        if request.path.startswith('/uploads/'):
            client_ip = request.environ.get('HTTP_X_FORWARDED_FOR', request.environ.get('REMOTE_ADDR', 'Unknown'))
            if response.status_code >= 400:
                safe_print(f"[Error] {client_ip} → Image request failed: {response.status_code}")
            # 성공 시에는 로그 출력 안함 (before_request에서 그룹화)
        elif response.status_code >= 400:
            client_ip = request.environ.get('HTTP_X_FORWARDED_FOR', request.environ.get('REMOTE_ADDR', 'Unknown'))
            safe_print(f"[Error] {client_ip} ← {response.status_code} for {request.path}")
    return response

# TourAPI 응답 캐시 (메모리)
# 구조: {cache_key: {'data': response_content, 'headers': headers, 'timestamp': time.time()}}
tour_api_cache = {}
TOUR_API_CACHE_TTL = 1800  # 30분 캐시 유지 (위치 기반 데이터는 자주 안 바뀜)
TOUR_API_ERROR_TTL = 60    # 에러 시 1분간 재시도 방지
tour_api_last_request = 0  # 마지막 요청 시간 (rate limiting)
TOUR_API_MIN_INTERVAL = 0.5  # 요청 간 최소 간격 (초)

def get_tour_api_cache_key(path, params):
    """TourAPI 요청에 대한 캐시 키 생성"""
    # params를 정렬하여 일관된 키 생성
    sorted_params = sorted(params.items())
    param_str = '&'.join([f"{k}={v}" for k, v in sorted_params])
    return f"{path}?{param_str}"

def get_cached_response(cache_key):
    """캐시에서 응답 조회"""
    if cache_key in tour_api_cache:
        cached = tour_api_cache[cache_key]
        age = time.time() - cached['timestamp']
        ttl = cached.get('ttl', TOUR_API_CACHE_TTL)
        if age < ttl:
            return cached['data'], cached['headers']
        else:
            # 만료된 캐시 삭제
            del tour_api_cache[cache_key]
    return None, None

def cache_response(cache_key, data, headers, ttl=None):
    """응답을 캐시에 저장"""
    tour_api_cache[cache_key] = {
        'data': data,
        'headers': headers,
        'timestamp': time.time(),
        'ttl': ttl if ttl else TOUR_API_CACHE_TTL
    }

# 새로운 프록시 엔드포인트 추가 (캐싱 적용)
@app.route('/proxy/<path:path>', methods=['GET'])
def proxy(path):
    global tour_api_last_request
    try:
        # 캐시 키 생성
        cache_key = get_tour_api_cache_key(path, request.args)

        # 캐시 확인
        cached_data, cached_headers = get_cached_response(cache_key)
        if cached_data is not None:
            # 캐시된 응답 반환
            response_headers = dict(cached_headers)
            response_headers['X-Cache'] = 'HIT'
            response_headers['Access-Control-Allow-Origin'] = '*'
            return cached_data, 200, response_headers

        # Rate limiting - 최소 요청 간격 유지
        now = time.time()
        time_since_last = now - tour_api_last_request
        if time_since_last < TOUR_API_MIN_INTERVAL:
            time.sleep(TOUR_API_MIN_INTERVAL - time_since_last)
        tour_api_last_request = time.time()

        # 캐시 미스 - TourAPI 호출
        tour_api_url = f"http://apis.data.go.kr/B551011/KorPetTourService/{path}"
        params = request.args
        headers = {'Accept-Encoding': ''}

        response = requests.get(tour_api_url, params=params, headers=headers, timeout=10)

        # 429 에러 처리 - 빈 결과를 임시 캐싱하여 재시도 방지
        if response.status_code == 429:
            safe_print(f"[Warning] TourAPI rate limited (429) for {path}")
            error_data = json.dumps({"response": {"body": {"items": [], "totalCount": 0}, "header": {"resultCode": "0000", "resultMsg": "Rate limited - cached empty"}}}).encode()
            error_headers = {'Content-Type': 'application/json', 'X-Cache': 'RATE_LIMITED', 'Access-Control-Allow-Origin': '*'}
            cache_response(cache_key, error_data, error_headers, ttl=TOUR_API_ERROR_TTL)
            return error_data, 200, error_headers

        response.raise_for_status()

        # 응답 헤더 준비
        response_headers = dict(response.headers)
        response_headers.pop('Content-Encoding', None)
        response_headers['Content-Type'] = 'application/json'
        response_headers['X-Cache'] = 'MISS'
        response_headers['Access-Control-Allow-Origin'] = '*'

        # 캐시 저장
        cache_response(cache_key, response.content, response_headers)

        return response.content, response.status_code, response_headers

    except requests.exceptions.RequestException as e:
        safe_print(f"[Error] Proxy request to TourAPI failed: {e}")
        # 에러 시에도 임시 캐싱하여 무한 재시도 방지
        error_data = json.dumps({"response": {"body": {"items": [], "totalCount": 0}, "header": {"resultCode": "9999", "resultMsg": str(e)}}}).encode()
        error_headers = {'Content-Type': 'application/json', 'X-Cache': 'ERROR', 'Access-Control-Allow-Origin': '*'}
        cache_response(cache_key, error_data, error_headers, ttl=TOUR_API_ERROR_TTL)
        return error_data, 200, error_headers
    except Exception as e:
        safe_print(f"[Error] Proxy request failed: {e}")
        return jsonify({"error": f"Proxy error: {str(e)}"}), 500

# 버스정류장 API 프록시 (캐싱 포함)
@app.route('/proxy/bus/<path:path>', methods=['GET'])
def bus_proxy(path):
    try:
        # 캐시 키 생성 (Prefix 'BUS_' 추가)
        cache_key = "BUS_" + get_tour_api_cache_key(path, request.args)

        # 캐시 확인
        cached_data, cached_headers = get_cached_response(cache_key)
        if cached_data is not None:
            response_headers = dict(cached_headers)
            response_headers['X-Cache'] = 'HIT'
            response_headers['Access-Control-Allow-Origin'] = '*'
            return cached_data, 200, response_headers

        # Bus API URL
        bus_api_url = f"http://apis.data.go.kr/1613000/BusSttnInfoInqireService/{path}"
        params = request.args
        headers = {'Accept-Encoding': ''}

        safe_print(f"[BusAPI] Request - {bus_api_url}?{request.query_string.decode()}")
        response = requests.get(bus_api_url, params=params, headers=headers, timeout=10)
        response.raise_for_status()

        # 헤더 정리
        response_headers = dict(response.headers)
        response_headers.pop('Content-Encoding', None)
        response_headers['Content-Type'] = 'application/json' # JSON 강제
        response_headers['X-Cache'] = 'MISS'
        response_headers['Access-Control-Allow-Origin'] = '*'

        # 캐시 저장
        cache_response(cache_key, response.content, response_headers)

        return response.content, response.status_code, response_headers

    except requests.exceptions.RequestException as e:
        safe_print(f"[Error] Proxy request to BusAPI failed: {e}")
        return jsonify({"error": f"Failed to fetch data from BusAPI: {str(e)}"}), 500
    except Exception as e:
        safe_print(f"[Error] Bus Proxy error: {e}")
        return jsonify({"error": f"Bus Proxy error: {str(e)}"}), 500

# 터미널(고속버스) API 프록시
@app.route('/proxy/terminal/<path:path>', methods=['GET'])
def terminal_proxy(path):
    try:
        cache_key = "TERM_" + get_tour_api_cache_key(path, request.args)
        cached_data, cached_headers = get_cached_response(cache_key)
        if cached_data is not None:
            return cached_data, 200, dict(cached_headers)

        api_url = f"http://apis.data.go.kr/1613000/ExpBusInfoService/{path}"
        params = request.args
        headers = {'Accept-Encoding': ''}

        safe_print(f"[woopangdebug] [TerminalAPI] Request - {api_url}?{request.query_string.decode()}")
        response = requests.get(api_url, params=params, headers=headers, timeout=10)
        safe_print(f"[woopangdebug] [TerminalAPI] Response Status: {response.status_code}")
        response.raise_for_status()

        response_headers = dict(response.headers)
        response_headers.pop('Content-Encoding', None)
        response_headers['Content-Type'] = 'application/json'
        response_headers['Access-Control-Allow-Origin'] = '*'

        cache_response(cache_key, response.content, response_headers)
        return response.content, response.status_code, response_headers
    except Exception as e:
        safe_print(f"[Error] Terminal Proxy error: {e}")
        return jsonify({"error": str(e)}), 500

# 기차역 API 프록시
@app.route('/proxy/train/<path:path>', methods=['GET'])
def train_proxy(path):
    try:
        cache_key = "TRAIN_" + get_tour_api_cache_key(path, request.args)
        cached_data, cached_headers = get_cached_response(cache_key)
        if cached_data is not None:
            return cached_data, 200, dict(cached_headers)

        api_url = f"http://apis.data.go.kr/1613000/TrainInfoService/{path}"
        params = request.args
        headers = {'Accept-Encoding': ''}

        safe_print(f"[woopangdebug] [TrainAPI] Request - {api_url}?{request.query_string.decode()}")
        response = requests.get(api_url, params=params, headers=headers, timeout=10)
        safe_print(f"[woopangdebug] [TrainAPI] Response Status: {response.status_code}")
        response.raise_for_status()

        response_headers = dict(response.headers)
        response_headers.pop('Content-Encoding', None)
        response_headers['Content-Type'] = 'application/json'
        response_headers['Access-Control-Allow-Origin'] = '*'

        cache_response(cache_key, response.content, response_headers)
        return response.content, response.status_code, response_headers
    except Exception as e:
        safe_print(f"[woopangdebug] [Error] Train Proxy error: {e}")
        return jsonify({"error": str(e)}), 500

# 지하철역 API 프록시
@app.route('/proxy/subway/<path:path>', methods=['GET'])
def subway_proxy(path):
    try:
        cache_key = "SUB_" + get_tour_api_cache_key(path, request.args)
        cached_data, cached_headers = get_cached_response(cache_key)
        if cached_data is not None:
            return cached_data, 200, dict(cached_headers)

        api_url = f"http://apis.data.go.kr/1613000/SubwayInfoService/{path}"
        params = request.args
        headers = {'Accept-Encoding': ''}

        safe_print(f"[woopangdebug] [SubwayAPI] Request - {api_url}?{request.query_string.decode()}")
        response = requests.get(api_url, params=params, headers=headers, timeout=10)
        safe_print(f"[woopangdebug] [SubwayAPI] Response Status: {response.status_code}")
        response.raise_for_status()

        response_headers = dict(response.headers)
        response_headers.pop('Content-Encoding', None)
        response_headers['Content-Type'] = 'application/json'
        response_headers['Access-Control-Allow-Origin'] = '*'

        cache_response(cache_key, response.content, response_headers)
        return response.content, response.status_code, response_headers
    except Exception as e:
        safe_print(f"[Error] Subway Proxy error: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/create-location-with-model', methods=['POST'])
def create_location_with_model():
    try:
        safe_print("[Debug] 3D 모델 업로드 요청 받음...")
        
        name = request.form.get('name')
        latitude = float(request.form.get('latitude'))
        longitude = float(request.form.get('longitude'))
        altitude = float(request.form.get('altitude', 0))
        model_type = request.form.get('model_type', 'custom')
        # AUTO_APPROVE_UPLOADS 설정에 따라 status 결정
        status = 'approved' if AUTO_APPROVE_UPLOADS else 'pending'
        pet_friendly = request.form.get('pet_friendly') == 'true'
        separate_restroom = request.form.get('separate_restroom') == 'true'
        instagram_id = request.form.get('instagram_id', '')
        folder = request.form.get('folder', 'default_folder')
        timezone = request.form.get('timezone', 'UTC')
        timezone_offset = request.form.get('timezone_offset', '+00:00')
        device_id = request.form.get('device_id', '')  # 업로더 추적용

        safe_print(f"[Info] 3D 모델 업로드 - 이름: {name}, 위치: ({latitude}, {longitude}), 상태: {status}")
        
        upload_folder = os.path.join(UPLOAD_FOLDER, folder)
        os.makedirs(upload_folder, exist_ok=True)
        safe_print(f"[Info] 업로드 폴더 생성: {upload_folder}")
        
        model_file = request.files.get('model_file')
        if model_file:
            model_filename = model_file.filename
            model_path = os.path.join(upload_folder, model_filename)
            model_file.save(model_path)
            safe_print(f"[Info] 3D 모델 파일 저장 완료: {model_filename}")
            model_url = f"uploads/{folder}/{model_filename}"
        else:
            safe_print("[Warning] 3D 모델 파일이 제공되지 않음")
            model_url = None
        
        sub_photos_list = []
        for i, sub in enumerate(request.files.getlist('sub_photos'), 1):
            sub_filename = f'sub_{i}.jpg'
            sub.save(os.path.join(upload_folder, sub_filename))
            sub_photos_list.append(sub_filename)
            safe_print(f"[Info] 서브 사진 저장: {sub_filename}")
        
        try:
            safe_print("[Debug] 데이터베이스 저장 시작...")
            conn = get_db_connection()
            cursor = conn.cursor()
            
            model_scale = float(request.form.get('model_scale', 1.0))
            model_rotation_x = float(request.form.get('model_rotation_x', 0.0))
            model_rotation_y = float(request.form.get('model_rotation_y', 0.0))
            model_rotation_z = float(request.form.get('model_rotation_z', 0.0))
            animation_name = request.form.get('animation_name', None)
            animation_speed = float(request.form.get('animation_speed', 1.0))
            animation_loop = request.form.get('animation_loop') == 'true'
            animation_auto_play = request.form.get('animation_auto_play') == 'true'
            model_format = 'glb'
            has_animation = bool(animation_name)
            
            insert_query = """
                INSERT INTO locations (name, latitude, longitude, altitude, pet_friendly,
                                     separate_restroom, instagram_id, status, folder,
                                     model_url, sub_photos, model_type, timezone, timezone_offset,
                                     model_scale, model_rotation_x, model_rotation_y, model_rotation_z,
                                     animation_name, animation_speed, animation_loop, animation_auto_play,
                                     model_format, has_animation, device_id)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                RETURNING id
            """

            cursor.execute(insert_query, (
                name, latitude, longitude, altitude, pet_friendly,
                separate_restroom, instagram_id, status, folder,
                model_url, json.dumps(sub_photos_list), model_type, timezone, timezone_offset,
                model_scale, model_rotation_x, model_rotation_y, model_rotation_z,
                animation_name, animation_speed, animation_loop, animation_auto_play,
                model_format, has_animation, device_id
            ))
            
            location_id = cursor.fetchone()[0]
            conn.commit()
            cursor.close()
            conn.close()
            
            safe_print(f"[Info] 데이터베이스 저장 완료 - ID: {location_id}")
            safe_print(f"[Info] 모델 URL: {model_url}")

            # Slack 알림 전송 (3D 모델)
            send_slack_upload_notification({
                'id': location_id,
                'name': name,
                'username': '익명',
                'latitude': latitude,
                'longitude': longitude,
                'folder': folder,
                'status': status,
                'model_type': 'custom'
            })

        except Exception as db_error:
            safe_print(f"[Error] 데이터베이스 저장 실패: {db_error}")
            return jsonify({"error": "Database save failed"}), 500

        safe_print("[Info] 3D 모델 업로드 완료!")
        return jsonify({"message": "3D Model Upload Succeeded!", "model_url": model_url})
        
    except Exception as e:
        safe_print(f"[Error] 3D 모델 업로드 실패: {e}")
        traceback.print_exc()
        return jsonify({"error": "3D Model upload failed"}), 500

@app.route('/upload', methods=['POST'])
def upload():
    try:
        safe_print("[Debug] 완전한 업로드 요청 받음...")
        
        name = request.form.get('name')
        username = request.form.get('username', '')
        latitude = request.form.get('latitude', type=float)
        longitude = request.form.get('longitude', type=float)
        altitude = request.form.get('altitude', type=float, default=0.0)
        pet_friendly = request.form.get('pet_friendly') == 'true'
        separate_restroom = request.form.get('separate_restroom') == 'true'
        instagram_id = request.form.get('instagram_id', '')
        timezone_offset = request.form.get('timezone_offset', '+0000')
        device_id = request.form.get('device_id', '')  # 업로더 추적용
        # AUTO_APPROVE_UPLOADS 설정에 따라 status 결정
        upload_status = 'approved' if AUTO_APPROVE_UPLOADS else 'pending'

        if not name or latitude is None or longitude is None:
            return jsonify({"error": "이름, 위도, 경도는 필수입니다"}), 400
        
        safe_print(f"[Info] 위치 데이터: {name} at ({latitude}, {longitude})")
        
        from datetime import datetime
        now = datetime.now()
        korea_time = now
        timestamp = korea_time.strftime('%Y%m%d_%H%M%S')
        safe_name = (name or 'location').replace(' ', '_')
        folder = f"{timestamp}_{safe_name}_{timezone_offset.replace(':', '')}"
        
        upload_folder = os.path.join(UPLOAD_FOLDER, folder)
        os.makedirs(upload_folder, exist_ok=True)
        safe_print(f"[Info] 업로드 폴더 생성: {upload_folder}")
        
        main_photo_url = None
        if 'main_photo' in request.files:
            main_photo = request.files['main_photo']
            if main_photo.filename:
                main_photo.save(os.path.join(upload_folder, 'main.jpg'))
                main_photo_url = f"uploads/{folder}/main.jpg"
                safe_print("[Info] 메인 사진 저장 완료")
        
        sub_photos_list = []
        if 'sub_photos' in request.files:
            for i, sub in enumerate(request.files.getlist('sub_photos'), 1):
                if sub.filename:
                    sub_filename = f'sub_{i}.jpg'
                    sub.save(os.path.join(upload_folder, sub_filename))
                    sub_photos_list.append(f"uploads/{folder}/{sub_filename}")
                    safe_print(f"[Info] 서브 사진 저장: {sub_filename}")
        
        conn = None
        try:
            safe_print("[Debug] 데이터베이스 저장 시작...")
            conn = get_db_connection()
            cursor = conn.cursor()

            cursor.execute("SELECT COALESCE(MAX(id), 0) + 1 AS next_id FROM locations")
            next_id = cursor.fetchone()[0]

            insert_query = """
                INSERT INTO locations (
                    id, username, name, latitude, longitude, altitude,
                    pet_friendly, separate_restroom, instagram_id,
                    status, folder, main_photo, sub_photos, model_type,
                    animation_loop, animation_auto_play, created_at, device_id
                ) VALUES (
                    %s, %s, %s, %s, %s, %s, %s, %s, %s,
                    %s, %s, %s, %s, 'cube', false, false, CURRENT_TIMESTAMP, %s
                )
                RETURNING id
            """

            cursor.execute(insert_query, (
                next_id, username, name, latitude, longitude, altitude,
                pet_friendly, separate_restroom, instagram_id,
                upload_status, folder, main_photo_url, json.dumps(sub_photos_list), device_id
            ))

            location_id = cursor.fetchone()[0]
            conn.commit()
            cursor.close()

            safe_print(f"[Info] 데이터베이스 저장 완료 - ID: {location_id}")
            safe_print(f"[Info] 저장된 데이터: {name} ({latitude}, {longitude})")

            # Slack 알림 전송
            send_slack_upload_notification({
                'id': location_id,
                'name': name,
                'username': username or '익명',
                'latitude': latitude,
                'longitude': longitude,
                'folder': folder,
                'status': upload_status,
                'model_type': 'cube'
            })

            # 자동승인 ON일 때 5초 후 승인 알림 전송
            if AUTO_APPROVE_UPLOADS and device_id:
                def delayed_notification():
                    time.sleep(5)
                    send_upload_approved_notification(device_id, name, username, latitude, longitude)
                    safe_print(f"[Info] 5초 딜레이 승인 알림 전송 완료: {name}")

                notification_thread = threading.Thread(target=delayed_notification, daemon=True)
                notification_thread.start()

            return jsonify({
                "message": "Upload Succeeded!",
                "location_id": location_id,
                "folder": folder
            })

        except Exception as db_error:
            safe_print(f"[Error] 데이터베이스 저장 실패: {db_error}")
            return jsonify({"error": "데이터베이스 저장 실패"}), 500
        finally:
            if conn:
                conn.close()

    except Exception as e:
        safe_print(f"[Error] 업로드 실패: {e}")
        return jsonify({"error": "업로드 실패"}), 500

@app.route('/locations', methods=['GET'])
def get_locations():
    conn = None
    try:
        lat = request.args.get('lat', type=float)
        lon = request.args.get('lon', type=float)
        radius = request.args.get('radius', type=float, default=10000)

        if lat is None or lon is None:
            return jsonify({"error": "lat and lon parameters are required"}), 400

        # --- [최적화] Bounding Box 필터링 추가 ---
        lat_range = radius / 111000.0
        lon_range = radius / (111000.0 * math.cos(math.radians(lat)))

        conn = get_db_connection()
        cursor = conn.cursor()

        query = """
            SELECT id, name, latitude, longitude, altitude, pet_friendly,
                   separate_restroom, instagram_id, status, folder, main_photo, sub_photos, color,
                   model_type, model_url, model_scale, model_rotation_x, model_rotation_y, model_rotation_z,
                   animation_name, animation_speed, animation_loop, animation_auto_play,
                   model_format, has_animation, username
            FROM locations
            WHERE status = 'approved'
              AND latitude BETWEEN %s AND %s
              AND longitude BETWEEN %s AND %s
              AND 6371000 * ACOS(COS(RADIANS(%s)) * COS(RADIANS(latitude)) *
                   COS(RADIANS(longitude) - RADIANS(%s)) +
                   SIN(RADIANS(%s)) * SIN(RADIANS(latitude))) <= %s
        """
        params = [lat - lat_range, lat + lat_range, lon - lon_range, lon + lon_range, lat, lon, lat, radius]

        cursor.execute(query, params)
        rows = cursor.fetchall()
        # ... (이후 로직 동일) ---

        results = []
        for row in rows:
            sub_photos = row[11]
            if isinstance(sub_photos, str):
                try:
                    sub_photos = json.loads(sub_photos)
                    sub_photos = [sub_photos]
                except:
                    sub_photos = []
            
            raw_color = row[12]
            mapped_color = COLOR_MAP.get(raw_color.lower() if raw_color else None, None)
            
            model_type = row[13] if len(row) > 13 and row[13] else 'cube'
            model_url = row[14] if len(row) > 14 and row[14] else None
            model_scale = float(row[15]) if len(row) > 15 and row[15] else 1.0
            model_rotation_x = float(row[16]) if len(row) > 16 and row[16] else 0.0
            model_rotation_y = float(row[17]) if len(row) > 17 and row[17] else 0.0
            model_rotation_z = float(row[18]) if len(row) > 18 and row[18] else 0.0
            animation_name = row[19] if len(row) > 19 and row[19] else None
            animation_speed = float(row[20]) if len(row) > 20 and row[20] else 1.0
            animation_loop = bool(row[21]) if len(row) > 21 and row[21] is not None else True
            animation_auto_play = bool(row[22]) if len(row) > 22 and row[22] is not None else True
            model_format = row[23] if len(row) > 23 and row[23] else 'glb'
            has_animation = bool(row[24]) if len(row) > 24 and row[24] is not None else False
            username = row[25] if len(row) > 25 and row[25] else None

            result_item = OrderedDict({
                "id": row[0], 
                "name": row[1], 
                "instagram_id": row[7], 
                "status": row[8],
                "latitude": row[2], 
                "longitude": row[3], 
                "altitude": row[4],
                "pet_friendly": row[5], 
                "separate_restroom": row[6],
                "folder": row[9], 
                "main_photo": row[10], 
                "sub_photos": sub_photos,
                "color": mapped_color,
                "model_type": model_type,
                "model_url": model_url,
                "model_scale": model_scale,
                "model_rotation_x": model_rotation_x,
                "model_rotation_y": model_rotation_y,
                "model_rotation_z": model_rotation_z,
                "animation_name": animation_name,
                "animation_speed": animation_speed,
                "animation_loop": animation_loop,
                "animation_auto_play": animation_auto_play,
                "model_format": model_format,
                "has_animation": has_animation,
                "username": username
            })
            
            results.append(result_item)
                
        safe_print(f"[Info] /locations → {len(results)}개 위치 데이터 응답")
        return jsonify(results)

    except Exception as e:
        safe_print(f"[Error] /locations 실패: {e}")
        return jsonify({"error": "Failed to fetch locations"}), 500
    finally:
        if conn:
            conn.close()

@app.route('/locations/<int:location_id>', methods=['DELETE'])
def delete_location(location_id):
    """Location 삭제"""
    conn = None
    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("SELECT * FROM locations WHERE id = %s", (location_id,))
        location_data = cursor.fetchone()

        if not location_data:
            return jsonify({"error": "Location not found"}), 404

        columns = [desc[0] for desc in cursor.description]
        location_dict = dict(zip(columns, location_data))

        cursor.execute("DELETE FROM locations WHERE id = %s", (location_id,))
        conn.commit()

        if location_dict.get('folder'):
            folder_path = os.path.join(UPLOAD_FOLDER, location_dict['folder'])
            if os.path.exists(folder_path):
                try:
                    shutil.rmtree(folder_path)
                except Exception as folder_error:
                    safe_print(f"[Warning] 폴더 삭제 실패: {folder_error}")

        send_slack_delete_notification(location_dict)

        return jsonify({
            "message": "Location deleted successfully",
            "deletedLocation": {
                "id": location_dict.get('id'),
                "name": location_dict.get('name'),
                "folder": location_dict.get('folder')
            }
        })

    except Exception as e:
        safe_print(f"[Error] Delete location failed: {e}")
        return jsonify({"error": "Delete failed", "details": str(e)}), 500
    finally:
        if conn:
            conn.close()

@app.route('/uploads/<path:folder>', methods=['DELETE'])
def delete_uploads_folder(folder):
    """업로드 폴더 삭제"""
    try:
        folder_path = os.path.join(UPLOAD_FOLDER, folder)

        if not os.path.exists(folder_path):
            return jsonify({"error": "Folder not found"}), 404

        shutil.rmtree(folder_path)

        if SLACK_WEBHOOK_URL:
            try:
                payload = {
                    "channel": "#admin-notifications",
                    "text": f"📁 폴더 삭제됨: {folder}",
                    "icon_emoji": ":file_folder:",
                    "username": "ARDeleteBot"
                }
                requests.post(SLACK_WEBHOOK_URL, json=payload, timeout=10)
            except:
                pass

        return jsonify({"message": "Folder deleted successfully", "folder": folder})

    except Exception as e:
        safe_print(f"[Error] Delete folder failed: {e}")
        return jsonify({"error": "Delete failed", "details": str(e)}), 500

@app.route('/fix_upload', methods=['POST'])
def fix_upload():
    """수정/삭제 요청 처리"""
    conn = None
    try:
        target_id = request.form.get('target_id', -1, type=int)
        remove_request = request.form.get('remove_request', 'false').lower() == 'true'
        username = request.form.get('username', '').strip() or None
        name = request.form.get('name', '').strip() or None
        pet_friendly = request.form.get('pet_friendly', 'false').lower() == 'true'
        separate_restroom = request.form.get('separate_restroom', 'false').lower() == 'true'
        instagram_id = request.form.get('instagram_id', '').strip() or None
        description = request.form.get('description', '').strip() or None
        folder = request.form.get('folder', '').strip() or f"fix_{int(datetime.now().timestamp())}"

        base_folder = "locations_fix"
        full_folder = os.path.join(base_folder, folder)
        folder_path = os.path.join(UPLOAD_FOLDER, full_folder)
        os.makedirs(folder_path, exist_ok=True)

        main_photo = None
        if 'main_photo' in request.files:
            file = request.files['main_photo']
            if file and file.filename:
                filename = secure_filename(file.filename)
                file.save(os.path.join(folder_path, filename))
                main_photo = f"uploads/{full_folder}/{filename}"

        sub_photos = []
        if 'sub_photos' in request.files:
            files = request.files.getlist('sub_photos')
            for idx, file in enumerate(files):
                if file and file.filename:
                    filename = f"sub_{idx + 1}.jpg"
                    file.save(os.path.join(folder_path, filename))
                    sub_photos.append(f"uploads/{full_folder}/{filename}")

        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            INSERT INTO fix_requests (
                target_id, remove_request, username, name, pet_friendly,
                separate_restroom, instagram_id, description, folder,
                main_photo, sub_photos, created_at
            ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, CURRENT_TIMESTAMP)
            RETURNING id
        """, (
            target_id, remove_request, username, name, pet_friendly,
            separate_restroom, instagram_id, description, full_folder,
            main_photo, json.dumps(sub_photos) if sub_photos else None
        ))

        fix_id = cursor.fetchone()[0]
        conn.commit()

        send_slack_fix_request_notification({
            'id': fix_id,
            'target_id': target_id,
            'remove_request': remove_request,
            'username': username,
            'name': name,
            'pet_friendly': pet_friendly,
            'separate_restroom': separate_restroom,
            'instagram_id': instagram_id,
            'description': description,
            'folder': full_folder
        })

        return jsonify({"message": "Fix Upload Succeeded!", "fixId": fix_id})

    except Exception as e:
        safe_print(f"[Error] Fix upload failed: {e}")
        return jsonify({"message": "서버 오류 발생!"}), 500
    finally:
        if conn:
            conn.close()

@app.route('/model-processing-status/<int:location_id>', methods=['GET'])
def get_model_processing_status(location_id):
    """모델 처리 상태 조회"""
    conn = None
    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            SELECT id, name, model_url, model_scale, model_format, created_at
            FROM locations WHERE id = %s
        """, (location_id,))

        row = cursor.fetchone()
        if not row:
            return jsonify({"error": "Location not found"}), 404

        return jsonify({
            "success": True,
            "data": {
                "id": row[0],
                "name": row[1],
                "model_url": row[2],
                "model_scale": row[3],
                "model_format": row[4],
                "created_at": row[5].isoformat() if row[5] else None
            }
        })

    except Exception as e:
        safe_print(f"[Error] Model processing status failed: {e}")
        return jsonify({"error": "Failed to get processing status", "details": str(e)}), 500
    finally:
        if conn:
            conn.close()

@app.route('/test-slack', methods=['POST'])
def test_slack():
    """Slack 연결 테스트"""
    try:
        if not SLACK_WEBHOOK_URL:
            return jsonify({
                "success": False,
                "message": "Slack이 설정되지 않음",
                "webhook_url": "설정 안됨"
            }), 400

        payload = {
            "text": "🧪 Slack 연결 테스트 메시지",
            "channel": "#admin-notifications",
            "icon_emoji": ":robot_face:",
            "username": "TestBot"
        }

        response = requests.post(SLACK_WEBHOOK_URL, json=payload, timeout=10)

        if response.status_code == 200:
            return jsonify({"success": True, "message": "Slack 테스트 성공!"})
        else:
            return jsonify({
                "success": False,
                "message": "Slack 테스트 실패",
                "status_code": response.status_code
            }), 500

    except Exception as e:
        return jsonify({"success": False, "message": str(e)}), 500

# ==================== Comment & Like System APIs ====================

@app.route('/comments', methods=['GET'])
def get_comments():
    try:
        location_id = request.args.get('location_id', type=int)
        user_id = request.args.get('user_id', type=str)

        if not location_id:
            return jsonify({"error": "location_id is required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # Fetch comments with like count and user's like status
        # users 테이블과 JOIN하여 최신 username과 avatar_url 가져오기
        query = """
            SELECT c.id, c.user_id, COALESCE(u.username, c.username) as username, c.content, c.created_at,
                   (SELECT COUNT(*) FROM comment_likes cl WHERE cl.comment_id = c.id) as like_count,
                   (SELECT COUNT(*) > 0 FROM comment_likes cl WHERE cl.comment_id = c.id AND cl.user_id = %s) as is_liked,
                   u.avatar_url
            FROM comments c
            LEFT JOIN users u ON c.user_id = u.id::TEXT
            WHERE c.location_id = %s
            ORDER BY c.created_at DESC
        """
        cursor.execute(query, (user_id if user_id else '', location_id))
        rows = cursor.fetchall()

        comments = []
        for row in rows:
            comments.append({
                "id": row[0],
                "user_id": row[1],
                "username": row[2],
                "content": row[3],
                "created_at": row[4].isoformat() if row[4] else None,
                "like_count": row[5],
                "is_liked": row[6],
                "avatar_url": row[7] if len(row) > 7 else None
            })

        cursor.close()
        conn.close()
        return jsonify(comments), 200

    except Exception as e:
        safe_print(f"[Error] Failed to fetch comments: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/comments', methods=['POST'])
def add_comment():
    try:
        data = request.get_json()
        location_id = data.get('location_id')
        user_id = data.get('user_id')
        username = data.get('username')
        content = data.get('content')

        if not all([location_id, user_id, content]):
            return jsonify({"error": "Missing required fields"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        query = """
            INSERT INTO comments (location_id, user_id, username, content)
            VALUES (%s, %s, %s, %s)
            RETURNING id, created_at
        """
        cursor.execute(query, (location_id, user_id, username, content))
        new_comment = cursor.fetchone()
        conn.commit()
        
        cursor.close()
        conn.close()

        return jsonify({
            "message": "Comment added",
            "id": new_comment[0],
            "created_at": new_comment[1].isoformat()
        }), 201

    except Exception as e:
        safe_print(f"[Error] Failed to add comment: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/comments/<int:comment_id>', methods=['DELETE'])
def delete_comment(comment_id):
    """댓글 삭제 API - 본인 댓글만 삭제 가능"""
    try:
        user_id = request.args.get('user_id')

        if not user_id:
            return jsonify({"error": "user_id is required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 댓글이 존재하는지, 작성자가 맞는지 확인
        cursor.execute("SELECT user_id FROM comments WHERE id = %s", (comment_id,))
        result = cursor.fetchone()

        if not result:
            cursor.close()
            conn.close()
            return jsonify({"error": "Comment not found"}), 404

        comment_user_id = result[0]

        # 본인 댓글인지 확인
        if str(comment_user_id) != str(user_id):
            cursor.close()
            conn.close()
            return jsonify({"error": "You can only delete your own comments"}), 403

        # 댓글 좋아요 먼저 삭제
        cursor.execute("DELETE FROM comment_likes WHERE comment_id = %s", (comment_id,))

        # 댓글 삭제
        cursor.execute("DELETE FROM comments WHERE id = %s", (comment_id,))

        conn.commit()
        cursor.close()
        conn.close()

        safe_print(f"[Comment] Deleted comment {comment_id} by user {user_id}")
        return jsonify({"success": True, "message": "Comment deleted"}), 200

    except Exception as e:
        safe_print(f"[Error] Failed to delete comment: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/comments/like', methods=['POST'])
def toggle_comment_like():
    try:
        data = request.get_json()
        comment_id = data.get('comment_id')
        user_id = data.get('user_id')

        if not all([comment_id, user_id]):
            return jsonify({"error": "Missing required fields"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # Check if already liked
        cursor.execute("SELECT 1 FROM comment_likes WHERE comment_id = %s AND user_id = %s", (comment_id, user_id))
        exists = cursor.fetchone()

        if exists:
            # Unlike
            cursor.execute("DELETE FROM comment_likes WHERE comment_id = %s AND user_id = %s", (comment_id, user_id))
            action = "unliked"
        else:
            # Like
            cursor.execute("INSERT INTO comment_likes (comment_id, user_id) VALUES (%s, %s)", (comment_id, user_id))
            action = "liked"

        conn.commit()
        
        # Get updated count
        cursor.execute("SELECT COUNT(*) FROM comment_likes WHERE comment_id = %s", (comment_id,))
        new_count = cursor.fetchone()[0]

        cursor.close()
        conn.close()

        return jsonify({"action": action, "like_count": new_count}), 200

    except Exception as e:
        safe_print(f"[Error] Failed to toggle comment like: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/locations/likes', methods=['GET'])
def get_location_likes():
    try:
        location_id = request.args.get('location_id', type=int)
        user_id = request.args.get('user_id', type=str)

        if not location_id:
            return jsonify({"error": "location_id is required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("SELECT COUNT(*) FROM location_likes WHERE location_id = %s", (location_id,))
        total_likes = cursor.fetchone()[0]

        is_liked = False
        if user_id:
            cursor.execute("SELECT 1 FROM location_likes WHERE location_id = %s AND user_id = %s", (location_id, user_id))
            is_liked = cursor.fetchone() is not None

        cursor.close()
        conn.close()

        return jsonify({"total_likes": total_likes, "is_liked": is_liked}), 200

    except Exception as e:
        safe_print(f"[Error] Failed to get location likes: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/locations/like', methods=['POST'])
def toggle_location_like():
    try:
        data = request.get_json()
        location_id = data.get('location_id')
        user_id = data.get('user_id')

        if not all([location_id, user_id]):
            return jsonify({"error": "Missing required fields"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("SELECT 1 FROM location_likes WHERE location_id = %s AND user_id = %s", (location_id, user_id))
        exists = cursor.fetchone()

        if exists:
            cursor.execute("DELETE FROM location_likes WHERE location_id = %s AND user_id = %s", (location_id, user_id))
            action = "unliked"
        else:
            cursor.execute("INSERT INTO location_likes (location_id, user_id) VALUES (%s, %s)", (location_id, user_id))
            action = "liked"

        conn.commit()

        cursor.execute("SELECT COUNT(*) FROM location_likes WHERE location_id = %s", (location_id,))
        new_count = cursor.fetchone()[0]

        cursor.close()
        conn.close()

        return jsonify({"action": action, "total_likes": new_count}), 200

    except Exception as e:
        safe_print(f"[Error] Failed to toggle location like: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/register', methods=['POST'])
def register():
    try:
        data = request.get_json()
        email = data.get('email')
        password = data.get('password')
        username = data.get('username')

        if not all([email, password, username]):
            return jsonify({"error": "Missing fields"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # Check if email exists
        cursor.execute("SELECT 1 FROM users WHERE email = %s", (email,))
        if cursor.fetchone():
            cursor.close()
            conn.close()
            return jsonify({"error": "Email already exists"}), 409

        # Hash password
        hashed = bcrypt.hashpw(password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')

        cursor.execute("""
            INSERT INTO users (email, password_hash, username)
            VALUES (%s, %s, %s)
            RETURNING id
        """, (email, hashed, username))
        
        user_id = cursor.fetchone()[0]
        conn.commit()
        cursor.close()
        conn.close()

        return jsonify({"message": "User registered", "user_id": str(user_id), "username": username}), 201

    except Exception as e:
        safe_print(f"[Error] Registration failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/login', methods=['POST'])
def login():
    try:
        data = request.get_json()
        email = data.get('email')
        password = data.get('password')

        if not all([email, password]):
            return jsonify({"error": "Missing fields"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("SELECT id, password_hash, username FROM users WHERE email = %s", (email,))
        user = cursor.fetchone()
        cursor.close()
        conn.close()

        if user and bcrypt.checkpw(password.encode('utf-8'), user[1].encode('utf-8')):
            return jsonify({
                "message": "Login successful",
                "user_id": str(user[0]),
                "username": user[2]
            }), 200
        else:
            return jsonify({"error": "Invalid credentials"}), 401

    except Exception as e:
        safe_print(f"[Error] Login failed: {e}")
        return jsonify({"error": str(e)}), 500

# ============================================================
# PROFILE & FOLLOW SYSTEM API
# ============================================================

@app.route('/api/user/profile', methods=['GET'])
def get_user_profile():
    """사용자 프로필 조회"""
    conn = None
    try:
        user_id = request.args.get('user_id')
        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            SELECT id, username, email, avatar_url, bio, phone,
                   instagram_id, facebook_id, x_id,
                   followers_count, following_count, created_at
            FROM users WHERE id = %s OR id::text = %s
        """, (user_id, user_id))

        row = cursor.fetchone()
        cursor.close()

        if not row:
            return jsonify({"error": "User not found"}), 404

        profile = {
            "id": str(row[0]),
            "username": row[1],
            "email": row[2],
            "avatar_url": row[3] or "",
            "bio": row[4] or "",
            "phone": row[5] or "",
            "instagram_id": row[6] or "",
            "facebook_id": row[7] or "",
            "x_id": row[8] or "",
            "followers_count": row[9] or 0,
            "following_count": row[10] or 0,
            "created_at": row[11].isoformat() if row[11] else ""
        }
        return jsonify(profile)

    except Exception as e:
        safe_print(f"[Error] Get profile failed: {e}")
        return jsonify({"error": str(e)}), 500
    finally:
        if conn:
            conn.close()

@app.route('/api/user/profile', methods=['POST'])
def update_user_profile():
    """사용자 프로필 수정"""
    try:
        data = request.get_json()
        user_id = data.get('user_id')
        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 수정 가능한 필드
        updates = []
        params = []
        for field in ['username', 'avatar_url', 'bio', 'phone', 'instagram_id', 'facebook_id', 'x_id']:
            if field in data:
                updates.append(f"{field} = %s")
                params.append(data[field])

        if not updates:
            return jsonify({"error": "No fields to update"}), 400

        params.append(user_id)
        cursor.execute(f"""
            UPDATE users SET {', '.join(updates)}
            WHERE id = %s OR id::text = %s
            RETURNING id, username
        """, params + [user_id])

        result = cursor.fetchone()
        conn.commit()
        cursor.close()
        conn.close()

        if not result:
            return jsonify({"error": "User not found"}), 404

        return jsonify({"success": True, "message": "Profile updated"})

    except Exception as e:
        safe_print(f"[Error] Update profile failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/user/avatar', methods=['POST'])
def upload_avatar():
    """아바타 이미지 업로드"""
    try:
        if 'avatar' not in request.files:
            return jsonify({"error": "No file uploaded"}), 400

        user_id = request.form.get('user_id')
        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        file = request.files['avatar']
        if file.filename == '':
            return jsonify({"error": "No file selected"}), 400

        # 파일 저장
        import uuid
        ext = file.filename.rsplit('.', 1)[-1].lower() if '.' in file.filename else 'jpg'
        filename = f"avatar_{user_id}_{uuid.uuid4().hex[:8]}.{ext}"

        avatar_dir = os.path.join(os.path.dirname(__file__), 'static', 'avatars')
        os.makedirs(avatar_dir, exist_ok=True)
        filepath = os.path.join(avatar_dir, filename)
        file.save(filepath)

        # DB 업데이트
        avatar_url = f"https://woopang.com/static/avatars/{filename}"
        conn = get_db_connection()
        cursor = conn.cursor()
        cursor.execute("""
            UPDATE users SET avatar_url = %s
            WHERE id = %s OR id::text = %s
        """, (avatar_url, user_id, user_id))
        conn.commit()
        cursor.close()
        conn.close()

        return jsonify({"success": True, "avatar_url": avatar_url})

    except Exception as e:
        safe_print(f"[Error] Avatar upload failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/follow', methods=['POST'])
def follow_user():
    """사용자 팔로우"""
    try:
        data = request.get_json()
        follower_id = data.get('follower_id')
        following_id = data.get('following_id')

        if not follower_id or not following_id:
            return jsonify({"error": "follower_id and following_id required"}), 400

        if follower_id == following_id:
            return jsonify({"error": "Cannot follow yourself"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 팔로우 추가
        cursor.execute("""
            INSERT INTO user_follows (follower_id, following_id)
            VALUES (%s, %s)
            ON CONFLICT (follower_id, following_id) DO NOTHING
            RETURNING id
        """, (follower_id, following_id))

        result = cursor.fetchone()
        follower_username = None
        if result:
            # 카운트 업데이트
            cursor.execute("UPDATE users SET following_count = following_count + 1 WHERE id::text = %s", (follower_id,))
            cursor.execute("UPDATE users SET followers_count = followers_count + 1 WHERE id::text = %s", (following_id,))

            # 팔로워 이름 가져오기 (알림용)
            cursor.execute("SELECT username FROM users WHERE id::text = %s", (follower_id,))
            username_result = cursor.fetchone()
            if username_result:
                follower_username = username_result[0]

        conn.commit()
        cursor.close()
        conn.close()

        # 알림 전송 (새 팔로우인 경우에만)
        if result and follower_username:
            send_social_notification(following_id, follower_username, 'follow')

        return jsonify({"success": True, "message": "Followed"})

    except Exception as e:
        safe_print(f"[Error] Follow failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/unfollow', methods=['POST'])
def unfollow_user():
    """사용자 언팔로우"""
    try:
        data = request.get_json()
        follower_id = data.get('follower_id')
        following_id = data.get('following_id')

        if not follower_id or not following_id:
            return jsonify({"error": "follower_id and following_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 팔로우 삭제
        cursor.execute("""
            DELETE FROM user_follows
            WHERE follower_id = %s AND following_id = %s
            RETURNING id
        """, (follower_id, following_id))

        result = cursor.fetchone()
        if result:
            # 카운트 업데이트
            cursor.execute("UPDATE users SET following_count = GREATEST(following_count - 1, 0) WHERE id::text = %s", (follower_id,))
            cursor.execute("UPDATE users SET followers_count = GREATEST(followers_count - 1, 0) WHERE id::text = %s", (following_id,))

        conn.commit()
        cursor.close()
        conn.close()

        return jsonify({"success": True, "message": "Unfollowed"})

    except Exception as e:
        safe_print(f"[Error] Unfollow failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/followers', methods=['GET'])
def get_followers():
    """팔로워 목록 조회
    - is_following: 요청자가 이 팔로워를 팔로우하고 있는지 (맞팔 여부)
    - follower_count: 이 유저의 팔로워 수 (정렬용)
    """
    try:
        user_id = request.args.get('user_id')
        requester_id = request.args.get('requester_id', user_id)  # 요청자 ID (기본값: user_id)
        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 팔로워 목록 + 맞팔 여부 + 팔로워 수
        cursor.execute("""
            SELECT u.id, u.username, u.avatar_url, u.followers_count,
                   EXISTS(SELECT 1 FROM user_follows WHERE follower_id = %s AND following_id = u.id::text) as is_following
            FROM user_follows f
            JOIN users u ON u.id::text = f.follower_id
            WHERE f.following_id = %s
            ORDER BY
                -- 1순위: 맞팔로우 (내가 팔로우하는 유저 우선)
                (CASE WHEN EXISTS(SELECT 1 FROM user_follows WHERE follower_id = %s AND following_id = u.id::text) THEN 1 ELSE 0 END) DESC,
                -- 2순위: 팔로워 많은 순
                COALESCE(u.followers_count, 0) DESC,
                -- 3순위: 최신 팔로우 순
                f.created_at DESC
        """, (requester_id, user_id, requester_id))

        followers = []
        for row in cursor.fetchall():
            followers.append({
                "user_id": str(row[0]),
                "username": row[1],
                "avatar_url": row[2] or "",
                "follower_count": row[3] or 0,
                "is_following": row[4]
            })

        cursor.close()
        conn.close()

        return jsonify({"followers": followers, "count": len(followers)})

    except Exception as e:
        safe_print(f"[Error] Get followers failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/following', methods=['GET'])
def get_following():
    """팔로잉 목록 조회
    - follower_count: 이 유저의 팔로워 수 (정렬용)
    """
    try:
        user_id = request.args.get('user_id')
        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 팔로잉 목록 + 팔로워 수 (팔로워 많은 순 정렬)
        cursor.execute("""
            SELECT u.id, u.username, u.avatar_url, u.followers_count
            FROM user_follows f
            JOIN users u ON u.id::text = f.following_id
            WHERE f.follower_id = %s
            ORDER BY
                -- 팔로워 많은 순
                COALESCE(u.followers_count, 0) DESC,
                -- 최신 팔로우 순
                f.created_at DESC
        """, (user_id,))

        following = []
        for row in cursor.fetchall():
            following.append({
                "user_id": str(row[0]),
                "username": row[1],
                "avatar_url": row[2] or "",
                "follower_count": row[3] or 0
            })

        cursor.close()
        conn.close()

        return jsonify({"following": following, "count": len(following)})

    except Exception as e:
        safe_print(f"[Error] Get following failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/is_following', methods=['GET'])
def check_is_following():
    """팔로우 여부 확인"""
    try:
        follower_id = request.args.get('follower_id')
        following_id = request.args.get('following_id')

        if not follower_id or not following_id:
            return jsonify({"error": "follower_id and following_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            SELECT 1 FROM user_follows
            WHERE follower_id = %s AND following_id = %s
        """, (follower_id, following_id))

        is_following = cursor.fetchone() is not None

        cursor.close()
        conn.close()

        return jsonify({"is_following": is_following})

    except Exception as e:
        safe_print(f"[Error] Check following failed: {e}")
        return jsonify({"error": str(e)}), 500

# ============================================================
# 다이렉트 메시지(DM) 시스템 API
# ============================================================

@app.route('/api/dm/send', methods=['POST'])
def send_direct_message():
    """다이렉트 메시지 전송 (팔로잉하는 사용자에게만 가능)"""
    try:
        data = request.get_json()
        sender_id = data.get('sender_id')
        recipient_id = data.get('recipient_id')
        content = data.get('content')

        if not all([sender_id, recipient_id, content]):
            return jsonify({"error": "sender_id, recipient_id, content required"}), 400

        if not content.strip():
            return jsonify({"error": "Message content cannot be empty"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 팔로잉 여부 확인 (팔로잉하는 사람에게만 메시지 전송 가능)
        cursor.execute("""
            SELECT 1 FROM user_follows
            WHERE follower_id = %s AND following_id = %s
        """, (sender_id, recipient_id))

        if not cursor.fetchone():
            cursor.close()
            conn.close()
            return jsonify({"error": "You can only send messages to users you follow"}), 403

        # 발신자 username 조회 (알림용)
        cursor.execute("SELECT username FROM users WHERE id::text = %s", (sender_id,))
        sender_row = cursor.fetchone()
        sender_username = sender_row[0] if sender_row else f"User {sender_id}"

        # 메시지 저장
        cursor.execute("""
            INSERT INTO direct_messages (sender_id, recipient_id, content)
            VALUES (%s, %s, %s)
            RETURNING id, created_at
        """, (sender_id, recipient_id, content.strip()))

        result = cursor.fetchone()
        message_id = result[0]
        created_at = result[1]

        conn.commit()
        cursor.close()
        conn.close()

        safe_print(f"[DM] Message sent: id={message_id}, from={sender_id}({sender_username}) to={recipient_id}")

        # 푸시 알림 전송 (비동기적으로 처리, 실패해도 메시지 전송은 성공)
        try:
            send_dm_notification(
                recipient_id=recipient_id,
                sender_username=sender_username,
                message_preview=content.strip(),
                sender_id=sender_id,
                message_id=message_id
            )
        except Exception as e:
            safe_print(f"[DM_NOTIFY] 푸시 알림 전송 실패 (메시지는 전송됨): {e}")

        return jsonify({
            "success": True,
            "message_id": message_id,
            "created_at": created_at.isoformat() if created_at else None
        }), 201

    except Exception as e:
        safe_print(f"[Error] Send DM failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/dm/inbox', methods=['GET'])
def get_dm_inbox():
    """받은 메시지함 조회"""
    try:
        user_id = request.args.get('user_id')
        limit = request.args.get('limit', type=int, default=50)
        offset = request.args.get('offset', type=int, default=0)

        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 받은 메시지 조회 (발신자 정보 포함)
        cursor.execute("""
            SELECT dm.id, dm.sender_id, dm.content, dm.is_read, dm.created_at,
                   u.username, u.avatar_url
            FROM direct_messages dm
            LEFT JOIN users u ON dm.sender_id = u.id::TEXT
            WHERE dm.recipient_id = %s
            ORDER BY dm.created_at DESC
            LIMIT %s OFFSET %s
        """, (user_id, limit, offset))

        messages = []
        for row in cursor.fetchall():
            messages.append({
                "id": row[0],
                "sender_id": row[1],
                "content": row[2],
                "is_read": row[3],
                "created_at": row[4].isoformat() if row[4] else None,
                "sender_username": row[5],
                "sender_avatar_url": row[6]
            })

        # 안 읽은 메시지 수
        cursor.execute("""
            SELECT COUNT(*) FROM direct_messages
            WHERE recipient_id = %s AND is_read = FALSE
        """, (user_id,))
        unread_count = cursor.fetchone()[0]

        cursor.close()
        conn.close()

        return jsonify({
            "messages": messages,
            "unread_count": unread_count,
            "count": len(messages)
        })

    except Exception as e:
        safe_print(f"[Error] Get DM inbox failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/dm/sent', methods=['GET'])
def get_dm_sent():
    """보낸 메시지함 조회"""
    try:
        user_id = request.args.get('user_id')
        limit = request.args.get('limit', type=int, default=50)
        offset = request.args.get('offset', type=int, default=0)

        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 보낸 메시지 조회 (수신자 정보 포함)
        cursor.execute("""
            SELECT dm.id, dm.recipient_id, dm.content, dm.is_read, dm.created_at,
                   u.username, u.avatar_url
            FROM direct_messages dm
            LEFT JOIN users u ON dm.recipient_id = u.id::TEXT
            WHERE dm.sender_id = %s
            ORDER BY dm.created_at DESC
            LIMIT %s OFFSET %s
        """, (user_id, limit, offset))

        messages = []
        for row in cursor.fetchall():
            messages.append({
                "id": row[0],
                "recipient_id": row[1],
                "content": row[2],
                "is_read": row[3],
                "created_at": row[4].isoformat() if row[4] else None,
                "recipient_username": row[5],
                "recipient_avatar_url": row[6]
            })

        cursor.close()
        conn.close()

        return jsonify({
            "messages": messages,
            "count": len(messages)
        })

    except Exception as e:
        safe_print(f"[Error] Get DM sent failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/dm/conversation', methods=['GET'])
def get_dm_conversation():
    """특정 사용자와의 대화 내역 조회"""
    try:
        user_id = request.args.get('user_id')
        other_id = request.args.get('other_id')
        limit = request.args.get('limit', type=int, default=50)
        offset = request.args.get('offset', type=int, default=0)

        if not user_id or not other_id:
            return jsonify({"error": "user_id and other_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 양방향 대화 조회
        cursor.execute("""
            SELECT dm.id, dm.sender_id, dm.recipient_id, dm.content, dm.is_read, dm.created_at
            FROM direct_messages dm
            WHERE (dm.sender_id = %s AND dm.recipient_id = %s)
               OR (dm.sender_id = %s AND dm.recipient_id = %s)
            ORDER BY dm.created_at ASC
            LIMIT %s OFFSET %s
        """, (user_id, other_id, other_id, user_id, limit, offset))

        messages = []
        for row in cursor.fetchall():
            messages.append({
                "id": row[0],
                "sender_id": row[1],
                "recipient_id": row[2],
                "content": row[3],
                "is_read": row[4],
                "created_at": row[5].isoformat() if row[5] else None,
                "is_mine": row[1] == user_id
            })

        # 상대방 정보
        cursor.execute("""
            SELECT username, avatar_url FROM users WHERE id::TEXT = %s
        """, (other_id,))
        other_user = cursor.fetchone()

        cursor.close()
        conn.close()

        return jsonify({
            "messages": messages,
            "count": len(messages),
            "other_user": {
                "id": other_id,
                "username": other_user[0] if other_user else None,
                "avatar_url": other_user[1] if other_user else None
            } if other_user else None
        })

    except Exception as e:
        safe_print(f"[Error] Get DM conversation failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/dm/read', methods=['POST'])
def mark_dm_as_read():
    """메시지 읽음 처리"""
    try:
        data = request.get_json()
        message_id = data.get('message_id')
        user_id = data.get('user_id')

        if not message_id or not user_id:
            return jsonify({"error": "message_id and user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 본인이 받은 메시지만 읽음 처리 가능
        cursor.execute("""
            UPDATE direct_messages
            SET is_read = TRUE, read_at = CURRENT_TIMESTAMP
            WHERE id = %s AND recipient_id = %s AND is_read = FALSE
            RETURNING id
        """, (message_id, user_id))

        updated = cursor.fetchone()
        conn.commit()
        cursor.close()
        conn.close()

        if updated:
            return jsonify({"success": True, "message": "Marked as read"})
        else:
            return jsonify({"success": False, "message": "Message not found or already read"})

    except Exception as e:
        safe_print(f"[Error] Mark DM read failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/dm/read-all', methods=['POST'])
def mark_all_dm_as_read():
    """특정 사용자의 모든 메시지 읽음 처리"""
    try:
        data = request.get_json()
        user_id = data.get('user_id')
        sender_id = data.get('sender_id')  # 선택: 특정 발신자의 메시지만

        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        if sender_id:
            # 특정 발신자의 메시지만 읽음 처리
            cursor.execute("""
                UPDATE direct_messages
                SET is_read = TRUE, read_at = CURRENT_TIMESTAMP
                WHERE recipient_id = %s AND sender_id = %s AND is_read = FALSE
            """, (user_id, sender_id))
        else:
            # 모든 받은 메시지 읽음 처리
            cursor.execute("""
                UPDATE direct_messages
                SET is_read = TRUE, read_at = CURRENT_TIMESTAMP
                WHERE recipient_id = %s AND is_read = FALSE
            """, (user_id,))

        updated_count = cursor.rowcount
        conn.commit()
        cursor.close()
        conn.close()

        return jsonify({"success": True, "updated_count": updated_count})

    except Exception as e:
        safe_print(f"[Error] Mark all DM read failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/dm/<int:message_id>', methods=['DELETE'])
def delete_dm(message_id):
    """메시지 삭제 (본인이 보내거나 받은 메시지만)"""
    try:
        user_id = request.args.get('user_id')

        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 본인이 보내거나 받은 메시지만 삭제 가능
        cursor.execute("""
            DELETE FROM direct_messages
            WHERE id = %s AND (sender_id = %s OR recipient_id = %s)
            RETURNING id
        """, (message_id, user_id, user_id))

        deleted = cursor.fetchone()
        conn.commit()
        cursor.close()
        conn.close()

        if deleted:
            return jsonify({"success": True, "message": "Message deleted"})
        else:
            return jsonify({"error": "Message not found or not authorized"}), 404

    except Exception as e:
        safe_print(f"[Error] Delete DM failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/dm/unread-count', methods=['GET'])
def get_dm_unread_count():
    """안 읽은 메시지 수 조회"""
    try:
        user_id = request.args.get('user_id')

        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            SELECT COUNT(*) FROM direct_messages
            WHERE recipient_id = %s AND is_read = FALSE
        """, (user_id,))

        unread_count = cursor.fetchone()[0]

        cursor.close()
        conn.close()

        return jsonify({"unread_count": unread_count})

    except Exception as e:
        safe_print(f"[Error] Get unread count failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/dm/conversations', methods=['GET'])
def get_dm_conversations():
    """모든 대화 목록 조회 (보낸/받은 메시지 모두 포함)"""
    try:
        user_id = request.args.get('user_id')
        limit = request.args.get('limit', type=int, default=50)

        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 나와 대화한 모든 사용자의 최신 메시지 조회
        # 보낸 메시지와 받은 메시지 모두 포함
        cursor.execute("""
            WITH conversation_partners AS (
                -- 내가 보낸 메시지의 수신자들
                SELECT DISTINCT recipient_id AS partner_id FROM direct_messages WHERE sender_id = %s
                UNION
                -- 내가 받은 메시지의 발신자들
                SELECT DISTINCT sender_id AS partner_id FROM direct_messages WHERE recipient_id = %s
            ),
            latest_messages AS (
                SELECT
                    cp.partner_id,
                    dm.id,
                    dm.sender_id,
                    dm.recipient_id,
                    dm.content,
                    dm.is_read,
                    dm.created_at,
                    ROW_NUMBER() OVER (PARTITION BY cp.partner_id ORDER BY dm.created_at DESC) as rn
                FROM conversation_partners cp
                JOIN direct_messages dm ON
                    (dm.sender_id = %s AND dm.recipient_id = cp.partner_id) OR
                    (dm.sender_id = cp.partner_id AND dm.recipient_id = %s)
            )
            SELECT
                lm.partner_id,
                lm.id as message_id,
                lm.sender_id,
                lm.recipient_id,
                lm.content,
                lm.is_read,
                lm.created_at,
                u.username,
                u.avatar_url,
                (SELECT COUNT(*) FROM direct_messages
                 WHERE sender_id = lm.partner_id AND recipient_id = %s AND is_read = FALSE) as unread_count
            FROM latest_messages lm
            LEFT JOIN users u ON lm.partner_id = u.id::TEXT
            WHERE lm.rn = 1
            ORDER BY lm.created_at DESC
            LIMIT %s
        """, (user_id, user_id, user_id, user_id, user_id, limit))

        conversations = []
        for row in cursor.fetchall():
            conversations.append({
                "partner_id": row[0],
                "message_id": row[1],
                "sender_id": row[2],
                "recipient_id": row[3],
                "last_message": row[4],
                "is_read": row[5],
                "last_message_time": row[6].isoformat() if row[6] else None,
                "partner_username": row[7],
                "partner_avatar_url": row[8],
                "unread_count": row[9] or 0
            })

        # 전체 안 읽은 메시지 수
        cursor.execute("""
            SELECT COUNT(*) FROM direct_messages
            WHERE recipient_id = %s AND is_read = FALSE
        """, (user_id,))
        total_unread = cursor.fetchone()[0]

        cursor.close()
        conn.close()

        return jsonify({
            "conversations": conversations,
            "count": len(conversations),
            "total_unread": total_unread
        })

    except Exception as e:
        safe_print(f"[Error] Get DM conversations failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/dm/<int:message_id>/like', methods=['POST'])
def like_dm_message(message_id):
    """DM 메시지에 좋아요 표시/취소"""
    try:
        data = request.json
        user_id = data.get('user_id')
        set_liked = data.get('set_liked')  # 명시적 좋아요 상태 지정 (없으면 토글)

        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 메시지 존재 및 권한 확인 (수신자만 좋아요 가능)
        cursor.execute("""
            SELECT id, sender_id, recipient_id, is_liked FROM direct_messages
            WHERE id = %s AND recipient_id = %s
        """, (message_id, user_id))

        message = cursor.fetchone()
        if not message:
            cursor.close()
            conn.close()
            return jsonify({"error": "Message not found or not authorized"}), 404

        # 좋아요 상태 결정 (set_liked가 있으면 명시적, 없으면 토글)
        if set_liked is not None:
            new_liked_status = bool(set_liked)
        else:
            new_liked_status = not message[3]

        cursor.execute("""
            UPDATE direct_messages SET is_liked = %s WHERE id = %s
        """, (new_liked_status, message_id))

        conn.commit()

        # 좋아요 알림 보내기 (sender에게)
        if new_liked_status:
            sender_id = message[1]
            # FCM 알림 전송 (선택)
            try:
                cursor.execute("SELECT fcm_token FROM users WHERE id = %s", (sender_id,))
                token_row = cursor.fetchone()
                if token_row and token_row[0]:
                    send_fcm_notification(
                        token_row[0],
                        "메시지 좋아요",
                        "상대방이 메시지를 좋아합니다 ♥",
                        {"type": "dm_like", "message_id": str(message_id)}
                    )
            except Exception as e:
                safe_print(f"[Warning] FCM notification failed: {e}")

        cursor.close()
        conn.close()

        return jsonify({
            "success": True,
            "message_id": message_id,
            "is_liked": new_liked_status
        })

    except Exception as e:
        safe_print(f"[Error] Like DM failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/dm/conversation', methods=['DELETE'])
def delete_dm_conversation():
    """특정 사용자와의 전체 대화 삭제"""
    try:
        user_id = request.args.get('user_id')
        other_id = request.args.get('other_id')

        if not user_id or not other_id:
            return jsonify({"error": "user_id and other_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 해당 사용자가 보내거나 받은 메시지 삭제
        cursor.execute("""
            DELETE FROM direct_messages
            WHERE (sender_id = %s AND recipient_id = %s)
               OR (sender_id = %s AND recipient_id = %s)
        """, (user_id, other_id, other_id, user_id))

        deleted_count = cursor.rowcount
        conn.commit()

        cursor.close()
        conn.close()

        return jsonify({
            "success": True,
            "deleted_count": deleted_count
        })

    except Exception as e:
        safe_print(f"[Error] Delete conversation failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/users/search', methods=['GET'])
def search_users():
    """사용자 검색 (아이디로 검색, 팔로잉 여부 포함)"""
    try:
        query = request.args.get('q', '')
        user_id = request.args.get('user_id')  # 현재 로그인 사용자 (팔로잉 체크용)
        limit = request.args.get('limit', type=int, default=20)

        if not query or len(query) < 2:
            return jsonify({"users": [], "count": 0})

        conn = get_db_connection()
        cursor = conn.cursor()

        # 사용자 검색 (username 또는 display_name에서)
        if user_id:
            cursor.execute("""
                SELECT u.id, u.username, u.avatar_url,
                       EXISTS(SELECT 1 FROM followers WHERE follower_id = %s AND following_id = u.id) as is_following
                FROM users u
                WHERE (u.username ILIKE %s OR u.display_name ILIKE %s)
                  AND u.id != %s
                LIMIT %s
            """, (user_id, f'%{query}%', f'%{query}%', user_id, limit))
        else:
            cursor.execute("""
                SELECT u.id, u.username, u.avatar_url, FALSE as is_following
                FROM users u
                WHERE u.username ILIKE %s OR u.display_name ILIKE %s
                LIMIT %s
            """, (f'%{query}%', f'%{query}%', limit))

        rows = cursor.fetchall()

        users = []
        for row in rows:
            users.append({
                "id": row[0],
                "username": row[1],
                "avatar_url": row[2],
                "is_following": row[3]
            })

        cursor.close()
        conn.close()

        return jsonify({
            "users": users,
            "count": len(users)
        })

    except Exception as e:
        safe_print(f"[Error] Search users failed: {e}")
        return jsonify({"error": str(e)}), 500

# ============================================================
# 관리자 공지 시스템 API
# ============================================================

@app.route('/api/broadcast/list', methods=['GET'])
def get_broadcast_list():
    """관리자 공지 목록 조회"""
    try:
        user_id = request.args.get('user_id')
        lang = request.args.get('lang')
        limit = request.args.get('limit', type=int, default=20)

        conn = get_db_connection()
        cursor = conn.cursor()

        # 활성화된 공지 조회 (우선순위 순, 만료되지 않은 것만)
        query = """
            SELECT id, title, content, sender_name, priority, target_lang, created_at
            FROM admin_broadcasts
            WHERE is_active = TRUE
              AND (expires_at IS NULL OR expires_at > CURRENT_TIMESTAMP)
        """
        params = []

        if lang:
            query += " AND (target_lang IS NULL OR target_lang = %s)"
            params.append(lang)

        query += " ORDER BY priority DESC, created_at DESC LIMIT %s"
        params.append(limit)

        cursor.execute(query, params)

        broadcasts = []
        for row in cursor.fetchall():
            broadcasts.append({
                "id": row[0],
                "title": row[1],
                "content": row[2],
                "sender_name": row[3],
                "priority": row[4],
                "target_lang": row[5],
                "created_at": row[6].isoformat() if row[6] else None,
                "is_admin": True
            })

        cursor.close()
        conn.close()

        return jsonify({"broadcasts": broadcasts, "count": len(broadcasts)})

    except Exception as e:
        safe_print(f"[Error] Get broadcast list failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/broadcast/create', methods=['POST'])
def create_broadcast():
    """관리자 공지 생성 (관리자 전용)"""
    try:
        data = request.get_json()
        title = data.get('title')
        content = data.get('content')
        sender_name = data.get('sender_name', 'WOOPANG')
        priority = data.get('priority', 0)
        target_lang = data.get('target_lang')
        expires_at = data.get('expires_at')

        if not title or not content:
            return jsonify({"error": "title and content required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            INSERT INTO admin_broadcasts (title, content, sender_name, priority, target_lang, expires_at)
            VALUES (%s, %s, %s, %s, %s, %s)
            RETURNING id, created_at
        """, (title, content, sender_name, priority, target_lang, expires_at))

        result = cursor.fetchone()
        broadcast_id = result[0]

        conn.commit()
        cursor.close()
        conn.close()

        safe_print(f"[Broadcast] Created broadcast {broadcast_id}: {title}")
        return jsonify({"success": True, "broadcast_id": broadcast_id}), 201

    except Exception as e:
        safe_print(f"[Error] Create broadcast failed: {e}")
        return jsonify({"error": str(e)}), 500

# ============================================================
# 사용자 차단 시스템 API
# ============================================================

@app.route('/api/block', methods=['POST'])
def block_user():
    """사용자 차단"""
    try:
        data = request.get_json()
        blocker_id = data.get('blocker_id')
        blocked_id = data.get('blocked_id')
        reason = data.get('reason')

        if not blocker_id or not blocked_id:
            return jsonify({"error": "blocker_id and blocked_id required"}), 400

        if blocker_id == blocked_id:
            return jsonify({"error": "Cannot block yourself"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 이미 차단되어 있는지 확인
        cursor.execute("""
            SELECT 1 FROM user_blocks WHERE blocker_id = %s AND blocked_id = %s
        """, (blocker_id, blocked_id))

        if cursor.fetchone():
            cursor.close()
            conn.close()
            return jsonify({"success": True, "message": "Already blocked"})

        cursor.execute("""
            INSERT INTO user_blocks (blocker_id, blocked_id, reason)
            VALUES (%s, %s, %s)
        """, (blocker_id, blocked_id, reason))

        conn.commit()
        cursor.close()
        conn.close()

        safe_print(f"[Block] User {blocker_id} blocked {blocked_id}")
        return jsonify({"success": True, "message": "User blocked"}), 201

    except Exception as e:
        safe_print(f"[Error] Block user failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/unblock', methods=['POST'])
def unblock_user():
    """사용자 차단 해제"""
    try:
        data = request.get_json()
        blocker_id = data.get('blocker_id')
        blocked_id = data.get('blocked_id')

        if not blocker_id or not blocked_id:
            return jsonify({"error": "blocker_id and blocked_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            DELETE FROM user_blocks WHERE blocker_id = %s AND blocked_id = %s
            RETURNING id
        """, (blocker_id, blocked_id))

        deleted = cursor.fetchone()
        conn.commit()
        cursor.close()
        conn.close()

        if deleted:
            safe_print(f"[Unblock] User {blocker_id} unblocked {blocked_id}")
            return jsonify({"success": True, "message": "User unblocked"})
        else:
            return jsonify({"success": False, "message": "Block not found"})

    except Exception as e:
        safe_print(f"[Error] Unblock user failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/blocked-users', methods=['GET'])
def get_blocked_users():
    """차단 목록 조회"""
    try:
        user_id = request.args.get('user_id')

        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            SELECT b.blocked_id, b.reason, b.created_at, u.username, u.avatar_url
            FROM user_blocks b
            LEFT JOIN users u ON b.blocked_id = u.id::TEXT
            WHERE b.blocker_id = %s
            ORDER BY b.created_at DESC
        """, (user_id,))

        blocked_users = []
        for row in cursor.fetchall():
            blocked_users.append({
                "id": row[0],
                "reason": row[1],
                "blocked_at": row[2].isoformat() if row[2] else None,
                "username": row[3],
                "avatar_url": row[4]
            })

        cursor.close()
        conn.close()

        return jsonify({"blocked_users": blocked_users, "count": len(blocked_users)})

    except Exception as e:
        safe_print(f"[Error] Get blocked users failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/is-blocked', methods=['GET'])
def check_is_blocked():
    """차단 여부 확인"""
    try:
        user_id = request.args.get('user_id')
        target_id = request.args.get('target_id')

        if not user_id or not target_id:
            return jsonify({"error": "user_id and target_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 내가 상대를 차단했는지
        cursor.execute("""
            SELECT 1 FROM user_blocks WHERE blocker_id = %s AND blocked_id = %s
        """, (user_id, target_id))
        i_blocked = cursor.fetchone() is not None

        # 상대가 나를 차단했는지
        cursor.execute("""
            SELECT 1 FROM user_blocks WHERE blocker_id = %s AND blocked_id = %s
        """, (target_id, user_id))
        blocked_by = cursor.fetchone() is not None

        cursor.close()
        conn.close()

        return jsonify({
            "i_blocked": i_blocked,
            "blocked_by": blocked_by,
            "can_message": not i_blocked and not blocked_by
        })

    except Exception as e:
        safe_print(f"[Error] Check blocked failed: {e}")
        return jsonify({"error": str(e)}), 500

# ============================================================
# 업로드 제한 시스템 API
# ============================================================

@app.route('/api/upload/can-upload', methods=['GET'])
def check_can_upload():
    """오늘 업로드 가능 여부 확인"""
    try:
        user_id = request.args.get('user_id')

        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 오늘 업로드 기록 확인
        cursor.execute("""
            SELECT id FROM user_uploads
            WHERE user_id = %s AND upload_date = CURRENT_DATE
        """, (user_id,))

        already_uploaded = cursor.fetchone() is not None

        cursor.close()
        conn.close()

        return jsonify({
            "can_upload": not already_uploaded,
            "already_uploaded_today": already_uploaded
        })

    except Exception as e:
        safe_print(f"[Error] Check can upload failed: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/upload/record', methods=['POST'])
def record_upload():
    """업로드 기록 저장"""
    try:
        data = request.get_json()
        user_id = data.get('user_id')
        location_id = data.get('location_id')

        if not user_id:
            return jsonify({"error": "user_id required"}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 오늘 이미 업로드했는지 확인
        cursor.execute("""
            SELECT id FROM user_uploads
            WHERE user_id = %s AND upload_date = CURRENT_DATE
        """, (user_id,))

        if cursor.fetchone():
            cursor.close()
            conn.close()
            return jsonify({"error": "Daily upload limit reached"}), 429

        # 업로드 기록 저장
        cursor.execute("""
            INSERT INTO user_uploads (user_id, location_id)
            VALUES (%s, %s)
            RETURNING id
        """, (user_id, location_id))

        result = cursor.fetchone()
        conn.commit()
        cursor.close()
        conn.close()

        safe_print(f"[Upload] User {user_id} uploaded location {location_id}")
        return jsonify({"success": True, "upload_id": result[0]}), 201

    except Exception as e:
        safe_print(f"[Error] Record upload failed: {e}")
        return jsonify({"error": str(e)}), 500

# ============================================================
# DEPRECATED: 좋아요 시스템 API (기능 제거됨 2026-01-22)
# ============================================================
# /api/like, /api/unlike, /api/is_liked, /api/likers 엔드포인트 제거됨

# ============================================================
# DEPRECATED: PINk 기능 제거됨 (2025-11-18)
# ============================================================
# @app.route('/pinks', methods=['GET'])
# def get_pinks():
#     # PINk 기능은 더 이상 사용하지 않음
#     return jsonify({"error": "PINk feature has been removed"}), 410

@app.route('/version', methods=['GET'])
def get_version():
    try:
        platform = request.args.get('platform', '').lower()
        
        if platform == 'android':
            latest_version = os.getenv("ANDROID_VERSION")
            force_update_str = os.getenv("ANDROID_FORCE_UPDATE")
            platform_name = "Android"
        elif platform == 'ios':
            latest_version = os.getenv("IOS_VERSION")
            force_update_str = os.getenv("IOS_FORCE_UPDATE")
            platform_name = "iOS"
        else:
            latest_version = os.getenv("APP_VERSION")
            force_update_str = os.getenv("FORCE_UPDATE")
            platform_name = "Default"
        
        if not latest_version:
            latest_version = "1.0.0"
        
        if not force_update_str:
            force_update = False
        else:
            force_update = force_update_str.lower() == "true"
        
        response = {
            "version": latest_version,
            "forceUpdate": force_update,
            "platform": platform_name.lower() if platform else "default"
        }
        
        return jsonify(response)
    except Exception as e:
        safe_print(f"[Error] /version 실패: {e}")
        return jsonify({"error": "Failed to fetch version"}), 500

@app.route('/uploads/<path:filename>')
def get_image(filename):
    """이미지 서빙 - rate limit 제외됨"""
    try:
        file_path = os.path.join(app.config['UPLOAD_FOLDER'], filename)
        if not os.path.exists(file_path):
            safe_print(f"[Warning] 이미지 파일 없음: {file_path}")
            return jsonify({"error": "Image not found"}), 404
        return send_from_directory(app.config['UPLOAD_FOLDER'], filename)
    except Exception as e:
        safe_print(f"[Error] 이미지 서빙 실패: {e}")
        return jsonify({"error": "Failed to serve image"}), 404

# 이미지 엔드포인트 rate limit 제외
if limiter:
    limiter.exempt(get_image)

@app.route('/api/ar-data', methods=['GET'])
def ar_data():
    try:
        return jsonify({"message": "This is a test response from Flask server"})
    except Exception as e:
        safe_print(f"[Error] 테스트 엔드포인트 실패: {e}")
        return jsonify({"error": "Test endpoint failed"}), 500

@app.route('/upload-page')
def upload_page():
    try:
        return render_template('upload.html')
    except Exception as e:
        safe_print(f"[Error] 업로드 페이지 렌더링 실패: {e}")
        return """
        <!DOCTYPE html>
        <html>
        <head><title>Upload Page</title></head>
        <body>
            <h1>업로드 페이지를 찾을 수 없습니다</h1>
            <p>upload.html 템플릿을 확인해주세요.</p>
        </body>
        </html>
        """, 404

@app.route('/')
def home():
    template_path = os.path.join(app.template_folder, 'index.html')

    if os.path.exists(template_path):
        try:
            # 템플릿 렌더링 (디버그 로그 제거)
            return render_template('index.html', title="WOOPANG", show_popup=True)
        except Exception as e:
            safe_print(f"[Error] 템플릿 렌더링 오류: {e}")
            return f"""
            <!DOCTYPE html>
            <html>
            <head><title>WOOPANG - 템플릿 오류</title></head>
            <body>
                <h1>템플릿 렌더링 오류</h1>
                <p>오류: {str(e)}</p>
            </body>
            </html>
            """
    else:
        safe_print(f"[Error] 템플릿 파일을 찾을 수 없습니다: {template_path}")
        return f"""
        <!DOCTYPE html>
        <html>
        <head><title>WOOPANG - 파일 없음</title></head>
        <body>
            <h1>템플릿 파일을 찾을 수 없습니다</h1>
            <p>경로: {template_path}</p>
        </body>
        </html>
        """

@app.route('/farm')
def farm_page():
    """3D 사과농장 체험 페이지"""
    template_path = os.path.join(app.template_folder, 'farm.html')

    if os.path.exists(template_path):
        try:
            return render_template('farm.html', title="구수한 농장 - 3D 체험")
        except Exception as e:
            safe_print(f"[Error] farm.html 렌더링 오류: {e}")
            return f"<h1>농장 체험 페이지 로딩 실패</h1><p>{str(e)}</p>", 500
    else:
        safe_print(f"[Error] farm.html을 찾을 수 없습니다: {template_path}")
        return "<h1>농장 체험 페이지를 찾을 수 없습니다</h1>", 404

@app.route('/sogogi')
def sogogi_page():
    try:
        # HTTP -> HTTPS로 변경, SSL 검증 비활성화
        response = requests.get('https://localhost:5001/', timeout=10, verify=False)
        if response.status_code == 200:
            # HTML 내용을 그대로 반환
            return response.content.decode('utf-8'), 200, {'Content-Type': 'text/html; charset=utf-8'}
        else:
            safe_print(f"[Error] Node.js 서버 응답 오류: {response.status_code}")
            return render_sogogi_fallback(), 503
            
    except requests.exceptions.ConnectionError:
        safe_print("[Error] Node.js 서버 연결 실패 - 서버가 실행되지 않았을 수 있습니다")
        return render_sogogi_fallback(), 503
    except requests.exceptions.Timeout:
        safe_print("[Error] Node.js 서버 응답 시간 초과")
        return render_sogogi_fallback(), 503
    except Exception as e:
        safe_print(f"[Error] 소고기 페이지 프록시 오류: {e}")
        return render_sogogi_fallback(), 503

@app.route('/api/order', methods=['POST'])
def order_proxy():
    """주문 API 프록시 - Node.js 서버로 주문 데이터 전달"""
    try:
        # 클라이언트에서 받은 JSON 데이터를 Node.js 서버로 전달
        order_data = request.get_json()
        
        safe_print(f"[Info] 주문 프록시: {order_data.get('customerName', '알수없음')} - {order_data.get('totalPrice', 0):,}원")
        
        response = requests.post(
            'https://localhost:5001/api/order',
            json=order_data,
            headers={'Content-Type': 'application/json'},
            timeout=15,
            verify=False  # SSL 인증서 검증 비활성화
        )       
        
        if response.status_code == 200:
            safe_print("[Info] Node.js 서버에서 주문 처리 완료")
            return response.json(), 200
        else:
            safe_print(f"[Error] Node.js 서버 주문 처리 오류: {response.status_code}")
            return {"success": False, "message": "주문 처리 중 오류가 발생했습니다."}, response.status_code
            
    except requests.exceptions.ConnectionError:
        safe_print("[Error] 주문 처리 서버 연결 실패")
        return {"success": False, "message": "주문 처리 서버에 연결할 수 없습니다. 잠시 후 다시 시도해주세요."}, 503
    except requests.exceptions.Timeout:
        safe_print("[Error] 주문 처리 시간 초과")
        return {"success": False, "message": "주문 처리 시간이 초과되었습니다. 다시 시도해주세요."}, 503
    except Exception as e:
        safe_print(f"[Error] 주문 프록시 오류: {e}")
        return {"success": False, "message": "주문 처리 중 예상치 못한 오류가 발생했습니다."}, 500

def render_sogogi_fallback():
    """Node.js 서버 연결 실패 시 대체 페이지"""
    return """
    <!DOCTYPE html>
    <html lang="ko">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>서비스 일시 중단</title>
        <style>
            body {
                font-family: 'Noto Sans KR', sans-serif;
                background: linear-gradient(135deg, #8B4513 0%, #A0522D 100%);
                color: white;
                margin: 0;
                padding: 20px;
                min-height: 100vh;
                display: flex;
                align-items: center;
                justify-content: center;
            }
            .container {
                text-align: center;
                background: rgba(0, 0, 0, 0.3);
                padding: 40px;
                border-radius: 15px;
                box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
                max-width: 500px;
            }
            .title {
                color: #FFD700;
                font-size: 28px;
                font-weight: bold;
                margin-bottom: 20px;
            }
            .message {
                font-size: 18px;
                margin-bottom: 30px;
                line-height: 1.6;
            }
            .contact {
                background: rgba(255, 255, 255, 0.1);
                padding: 20px;
                border-radius: 10px;
                margin: 20px 0;
            }
            .back-link {
                display: inline-block;
                color: #FFD700;
                text-decoration: none;
                font-size: 16px;
                margin-top: 20px;
                padding: 10px 20px;
                border: 2px solid #FFD700;
                border-radius: 10px;
                transition: all 0.3s ease;
            }
            .back-link:hover {
                background: #FFD700;
                color: #8B4513;
            }
        </style>
    </head>
    <body>
        <div class="container">
            <div class="title">서비스 일시 중단</div>
            <div class="message">
                죄송합니다. 현재 주문 시스템에 일시적인 문제가 발생했습니다.<br>
                빠른 시일 내에 복구하겠습니다.
            </div>
            <div class="contact">
                <div style="color: #FFD700; font-weight: bold; margin-bottom: 10px;">긴급 주문 문의</div>
                <div>📞 전화: 010-4444-4395</div>
                <div>📧 이메일: pdnom@naver.com</div>
            </div>
            <a href="/" class="back-link">홈으로 돌아가기</a>
        </div>
    </body>
    </html>
    """



# app_improved.py에 추가할 코드 (sogogi 관련 코드 다음에 추가)

# 사과 이미지 직접 서빙 (Node.js 서버 없이도 동작)
@app.route('/apple/images/<path:filename>')
def serve_apple_images(filename):
    try:
        apple_images_dir = os.path.join(r'C:\woopang\server\apple', 'images')
        return send_from_directory(apple_images_dir, filename)
    except Exception as e:
        safe_print(f"[Error] 사과 이미지 서빙 실패 {filename}: {e}")
        return jsonify({"error": "Failed to serve apple image"}), 404

@app.route('/apple', defaults={'path': ''})
@app.route('/apple/<path:path>')
def apple_page(path):
    # 이미지 요청은 직접 서빙
    if path.startswith('images/'):
        return serve_apple_images(path[7:])

    # apple.html 파일을 직접 서빙 (Node.js 서버 불필요)
    if path == '' or path == 'apple.html':
        try:
            apple_html_path = os.path.join(r'C:\woopang\server\apple', 'apple.html')
            with open(apple_html_path, 'r', encoding='utf-8') as f:
                html_content = f.read()
            return html_content, 200, {'Content-Type': 'text/html; charset=utf-8'}
        except Exception as e:
            safe_print(f"[Error] apple.html 파일 읽기 실패: {e}")
            return render_apple_fallback(), 503

    # 기타 요청은 Node.js 서버로 프록시
    try:
        response = requests.get(f'https://localhost:5002/{path}', timeout=10, verify=False)
        if response.status_code == 200:
            # Content-Type에 따라 처리
            content_type = response.headers.get('Content-Type', '')
            if 'text' in content_type or 'json' in content_type or 'html' in content_type:
                return response.content.decode('utf-8'), 200, {'Content-Type': content_type}
            else:
                # 바이너리 데이터는 그대로 반환
                return response.content, 200, {'Content-Type': content_type}
        else:
            safe_print(f"[Error] Node.js 서버 응답 오류: {response.status_code}")
            return render_apple_fallback(), 503

    except requests.exceptions.ConnectionError:
        safe_print("[Error] Node.js 서버 연결 실패 - 서버가 실행되지 않았을 수 있습니다")
        return render_apple_fallback(), 503
    except requests.exceptions.Timeout:
        safe_print("[Error] Node.js 서버 응답 시간 초과")
        return render_apple_fallback(), 503
    except Exception as e:
        safe_print(f"[Error] 사과 페이지 프록시 오류: {e}")
        return render_apple_fallback(), 503

@app.route('/api/apple-order', methods=['POST'])
def apple_order_proxy():
    """사과 주문 처리 - Node.js 서버(apple_server.js)로 프록시"""
    try:
        order_data = request.get_json()
        safe_print(f"[Info] 사과 주문 요청 → Node.js 서버로 프록시")

        # Node.js 서버(apple_server.js)로 프록시
        response = requests.post(
            'https://localhost:5002/api/apple-order',
            json=order_data,
            timeout=30,
            verify=False
        )

        result = response.json()
        if result.get('success'):
            safe_print(f"[Info] 사과 주문 프록시 성공: {result.get('orderId')}")
        else:
            safe_print(f"[Warning] 사과 주문 프록시 실패: {result.get('message')}")

        return jsonify(result), response.status_code

    except requests.exceptions.ConnectionError:
        safe_print("[Error] Node.js 사과 서버 연결 실패 - apple_server.js가 실행 중인지 확인하세요")
        return jsonify({
            'success': False,
            'message': '주문 서버에 연결할 수 없습니다. 잠시 후 다시 시도해주세요.'
        }), 503
    except requests.exceptions.Timeout:
        safe_print("[Error] Node.js 사과 서버 응답 시간 초과")
        return jsonify({
            'success': False,
            'message': '주문 처리 시간이 초과되었습니다. 잠시 후 다시 시도해주세요.'
        }), 504
    except Exception as e:
        safe_print(f"[Error] 사과 주문 프록시 오류: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({
            'success': False,
            'message': '주문 처리 중 서버 오류가 발생했습니다.'
        }), 500

@app.route('/api/orders', methods=['GET'])
def get_apple_orders():
    """사과 주문 목록 조회 (관리자용)"""
    try:
        # 날짜 파라미터 (기본값: 오늘)
        date_param = request.args.get('date', datetime.now().strftime('%Y-%m-%d'))
        orders_dir = os.path.join(r'C:\woopang\server\apple\orders', date_param)

        # 디렉토리가 없으면 빈 배열 반환
        if not os.path.exists(orders_dir):
            return jsonify({
                'success': True,
                'orders': [],
                'date': date_param,
                'count': 0
            }), 200

        # JSON 파일 읽기
        order_files = [f for f in os.listdir(orders_dir) if f.endswith('.json')]
        orders = []

        for file_name in order_files:
            file_path = os.path.join(orders_dir, file_name)
            with open(file_path, 'r', encoding='utf-8') as f:
                order_data = json.load(f)
                orders.append(order_data)

        # 주문 시간순으로 정렬 (최신순)
        orders.sort(key=lambda x: x.get('orderTime', ''), reverse=True)

        return jsonify({
            'success': True,
            'orders': orders,
            'date': date_param,
            'count': len(orders)
        }), 200

    except Exception as e:
        safe_print(f"[Error] 사과 주문 목록 조회 오류: {e}")
        return jsonify({
            'success': False,
            'message': '주문 목록 조회 중 오류가 발생했습니다.'
        }), 500

def render_apple_fallback():
    """Node.js 서버 연결 실패 시 대체 페이지"""
    return """
    <!DOCTYPE html>
    <html lang="ko">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>서비스 일시 중단</title>
        <style>
            body {
                font-family: 'Noto Sans KR', sans-serif;
                background: linear-gradient(135deg, #98D8C8 0%, #F7DC6F 100%);
                color: #2E7D32;
                margin: 0;
                padding: 20px;
                min-height: 100vh;
                display: flex;
                align-items: center;
                justify-content: center;
            }
            .container {
                text-align: center;
                background: rgba(255, 255, 255, 0.95);
                padding: 40px;
                border-radius: 20px;
                box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
                max-width: 500px;
            }
            .title {
                color: #388E3C;
                font-size: 28px;
                font-weight: bold;
                margin-bottom: 20px;
            }
            .message {
                font-size: 18px;
                margin-bottom: 30px;
                line-height: 1.6;
            }
            .contact {
                background: linear-gradient(135deg, #E8F5E8, #C8E6C9);
                padding: 20px;
                border-radius: 15px;
                margin: 20px 0;
                border: 2px solid #4CAF50;
            }
            .back-link {
                display: inline-block;
                color: #388E3C;
                text-decoration: none;
                font-size: 16px;
                margin-top: 20px;
                padding: 10px 20px;
                border: 2px solid #4CAF50;
                border-radius: 10px;
                transition: all 0.3s ease;
            }
            .back-link:hover {
                background: #4CAF50;
                color: white;
            }
        </style>
    </head>
    <body>
        <div class="container">
            <div class="title">🍎 서비스 일시 중단</div>
            <div class="message">
                죄송합니다. 현재 사과 주문 시스템에 일시적인 문제가 발생했습니다.<br>
                빠른 시일 내에 복구하겠습니다.
            </div>
            <div class="contact">
                <div style="color: #388E3C; font-weight: bold; margin-bottom: 10px;">긴급 주문 문의</div>
                <div>📞 전화: 010-4444-4395</div>
                <div>📧 이메일: pdnom@naver.com</div>
            </div>
            <a href="/" class="back-link">홈으로 돌아가기</a>
        </div>
    </body>
    </html>
    """

def render_vrompt_fallback():
    """Vrompt 서버 연결 실패 시 대체 페이지 - pdnom 가이드 스타일"""
    return """
    <!DOCTYPE html>
    <html lang="ko">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>VROMPT AI - 서비스 점검중</title>
        <link href="https://fonts.googleapis.com/css2?family=Noto+Sans+KR:wght@400;600;700&display=swap" rel="stylesheet">
        <style>
            * {
                margin: 0;
                padding: 0;
                box-sizing: border-box;
            }

            body {
                font-family: 'Noto Sans KR', -apple-system, BlinkMacSystemFont, sans-serif;
                min-height: 100vh;
                display: flex;
                align-items: center;
                justify-content: center;
                background: linear-gradient(135deg, #FFE4E9 0%, #E0F4FF 50%, #FFB6C1 100%);
                background-size: 400% 400%;
                animation: gradientShift 15s ease infinite;
                padding: 20px;
            }

            @keyframes gradientShift {
                0%, 100% { background-position: 0% 50%; }
                50% { background-position: 100% 50%; }
            }

            .container {
                text-align: center;
                background: rgba(255, 255, 255, 0.95);
                padding: 50px 40px;
                border-radius: 24px;
                box-shadow: 0 20px 60px rgba(255, 105, 180, 0.15),
                            0 8px 24px rgba(135, 206, 235, 0.12);
                max-width: 480px;
                width: 100%;
                position: relative;
                overflow: hidden;
                border: 1px solid rgba(255, 182, 193, 0.2);
            }

            /* 은은한 글로우 효과 */
            .container::before {
                content: '';
                position: absolute;
                top: -2px;
                left: -2px;
                right: -2px;
                bottom: -2px;
                background: linear-gradient(135deg, rgba(255, 182, 193, 0.3), rgba(135, 206, 235, 0.3));
                border-radius: 26px;
                z-index: -1;
                animation: subtleGlow 4s ease-in-out infinite;
            }

            @keyframes subtleGlow {
                0%, 100% { opacity: 0.5; }
                50% { opacity: 0.8; }
            }

            .icon {
                font-size: 72px;
                margin-bottom: 24px;
                animation: gentleBounce 3s ease-in-out infinite;
            }

            @keyframes gentleBounce {
                0%, 100% { transform: translateY(0); }
                50% { transform: translateY(-8px); }
            }

            .title {
                font-size: 28px;
                font-weight: 700;
                margin-bottom: 12px;
                background: linear-gradient(135deg, #FF69B4, #4682B4);
                -webkit-background-clip: text;
                -webkit-text-fill-color: transparent;
                background-clip: text;
            }

            .subtitle {
                font-size: 14px;
                color: #888;
                margin-bottom: 28px;
                letter-spacing: 1px;
            }

            .message {
                font-size: 16px;
                color: #555;
                line-height: 1.7;
                margin-bottom: 32px;
            }

            .status-card {
                background: linear-gradient(135deg, rgba(255, 182, 193, 0.15), rgba(135, 206, 235, 0.15));
                padding: 20px 24px;
                border-radius: 16px;
                margin-bottom: 28px;
                border: 1px solid rgba(255, 182, 193, 0.2);
            }

            .status-title {
                font-size: 14px;
                font-weight: 600;
                color: #FF69B4;
                margin-bottom: 12px;
                display: flex;
                align-items: center;
                justify-content: center;
                gap: 8px;
            }

            .status-title::before {
                content: '';
                width: 8px;
                height: 8px;
                background: #FFD700;
                border-radius: 50%;
                animation: pulse 2s ease-in-out infinite;
            }

            @keyframes pulse {
                0%, 100% { opacity: 1; transform: scale(1); }
                50% { opacity: 0.6; transform: scale(0.9); }
            }

            .contact-info {
                font-size: 14px;
                color: #666;
            }

            .contact-info div {
                margin: 6px 0;
            }

            .back-link {
                display: inline-block;
                padding: 14px 32px;
                background: linear-gradient(135deg, #FFB6C1, #87CEEB);
                color: white;
                text-decoration: none;
                border-radius: 12px;
                font-weight: 600;
                font-size: 15px;
                transition: all 0.35s cubic-bezier(0.4, 0, 0.2, 1);
                box-shadow: 0 4px 16px rgba(255, 182, 193, 0.3);
            }

            .back-link:hover {
                transform: translateY(-3px);
                box-shadow: 0 8px 28px rgba(255, 105, 180, 0.35);
                background: linear-gradient(135deg, #FF69B4, #4682B4);
            }

            .footer-text {
                margin-top: 28px;
                font-size: 12px;
                color: #aaa;
            }
        </style>
    </head>
    <body>
        <div class="container">
            <div class="icon">🎬</div>
            <h1 class="title">VROMPT AI</h1>
            <p class="subtitle">VIDEO PROMPT MAKER</p>
            <p class="message">
                서비스 점검 중입니다.<br>
                잠시 후 다시 접속해 주세요.
            </p>
            <div class="status-card">
                <div class="status-title">서버 점검 진행 중</div>
                <div class="contact-info">
                    <div>📧 문의: pdnom@naver.com</div>
                </div>
            </div>
            <a href="/" class="back-link">홈으로 돌아가기</a>
            <p class="footer-text">빠른 시일 내에 복구하겠습니다</p>
        </div>
    </body>
    </html>
    """

# Bookmark 서버________________________________________________________________________________________________________________

def serve_favicon_with_cors(directory, filename='favicon.ico'):
    """CORS 헤더를 포함한 favicon 서빙"""
    try:
        response = make_response(send_from_directory(directory, filename))
        response.headers['Access-Control-Allow-Origin'] = '*'
        response.headers['Access-Control-Allow-Methods'] = 'GET'
        response.headers['Cache-Control'] = 'public, max-age=86400'
        return response
    except:
        return '', 204

@app.route('/favicon.ico')
def serve_favicon():
    return serve_favicon_with_cors(app.static_folder)

# 각 서비스별 favicon 라우트
@app.route('/bookmark/favicon.ico')
def serve_bookmark_favicon():
    return serve_favicon_with_cors(r'C:\woopang\server\bookmark')

@app.route('/apple/favicon.ico')
def serve_apple_favicon():
    return serve_favicon_with_cors(r'C:\woopang\server\apple')

@app.route('/dongdong/static/favicon.ico')
def serve_dongdong_favicon():
    return serve_favicon_with_cors(r'C:\woopang\server\dongdong\static')

@app.route('/qqqq/static/favicon.ico')
@app.route('/qqqq/favicon.ico')
def serve_qqqq_favicon():
    return serve_favicon_with_cors(r'C:\woopang\server\qqqq')

@app.route('/vrompt/favicon.ico')
def serve_vrompt_favicon():
    return serve_favicon_with_cors(r'C:\woopang\server\vrompt')

@app.route('/home/static/favicon_investment.ico')
def serve_investment_favicon():
    return serve_favicon_with_cors(r'C:\woopang\server\home\static', 'favicon_investment.ico')

@app.route('/portpolio/favicon.ico')
def serve_portpolio_favicon():
    return serve_favicon_with_cors(r'C:\woopang\server\portpolio')

@app.route('/portpolio/favicon-16x16.png')
def serve_portpolio_favicon_16():
    return serve_favicon_with_cors(r'C:\woopang\server\portpolio', 'favicon-16x16.png')

@app.route('/portpolio/favicon-32x32.png')
def serve_portpolio_favicon_32():
    return serve_favicon_with_cors(r'C:\woopang\server\portpolio', 'favicon-32x32.png')

@app.route('/admin/favicon.svg')
@app.route('/static/favicon_message.svg')
def serve_admin_favicon():
    return serve_favicon_with_cors(r'C:\woopang\server\home\static', 'favicon_message.svg')

@app.route('/dbadmin/static/favicon_approve.svg')
def serve_dbadmin_favicon():
    return serve_favicon_with_cors(r'C:\woopang\server\admin\static', 'favicon_approve.svg')

@app.route('/portpolio/apple-touch-icon.png')
def serve_portpolio_apple_icon():
    return serve_favicon_with_cors(r'C:\woopang\server\portpolio', 'apple-touch-icon.png')

# 북마크 저장 디렉토리 설정
BOOKMARKS_BASE_DIR = r'C:\woopang\server\bookmark'
BOOKMARKS_DATA_DIR = r'C:\woopang\server\bookmark\data'
BOOKMARKS_FILE = r'C:\woopang\server\bookmark\data\bookmarks.json'
BOOKMARKS_HTML_FILE = r'C:\woopang\server\bookmark\bookmark.html'
BOOKMARKS_LOGO_FILE = r'C:\woopang\server\bookmark\쾌.png'

# 디렉토리 생성 (서버 시작 시)
if not os.path.exists(BOOKMARKS_DATA_DIR):
    os.makedirs(BOOKMARKS_DATA_DIR)

# 파일 쓰기 락 (동시성 문제 방지)
file_lock = threading.Lock()

@app.route('/bookmark')
def bookmark_page():
    try:
        bookmark_file = r'C:\woopang\server\bookmark\bookmark.html'
        if os.path.exists(bookmark_file):
            with open(bookmark_file, 'r', encoding='utf-8') as f:
                return f.read()
        else:
            return f"파일 없음: {bookmark_file}", 404
    except Exception as e:
        return f"오류: {str(e)}", 500

@app.route('/bookmark/<path:filename>')
def serve_bookmark_files(filename):
    try:
        return send_from_directory(r'C:\woopang\server\bookmark', filename)
    except Exception as e:
        return f"파일 서빙 실패: {filename} - {str(e)}", 404

# 로고 파일 전용 라우트 추가
@app.route('/bookmark/logo.png')
@app.route('/bookmark/쾌.png')
def serve_bookmark_logo():
    try:
        if os.path.exists(BOOKMARKS_LOGO_FILE):
            safe_print(f"[Info] 북마크 로고 서빙: {BOOKMARKS_LOGO_FILE}")
            return send_from_directory(BOOKMARKS_BASE_DIR, '쾌.png')
        else:
            safe_print(f"[Warning] 북마크 로고 파일 없음: {BOOKMARKS_LOGO_FILE}")
            # 투명한 1x1 PNG 반환 (빈 이미지)
            from flask import Response
            import base64
            transparent_png = base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChAGAHdZUAAAAAABJRU5ErkJggg==')
            return Response(transparent_png, mimetype='image/png')
    except Exception as e:
        safe_print(f"[Error] 북마크 로고 서빙 실패: {e}")
        return jsonify({"error": "Failed to serve logo"}), 500

@app.route('/api/bookmarks/save', methods=['POST'])
def save_bookmarks():
    try:
        data = request.get_json()
        if not data:
            return jsonify({'success': False, 'message': '데이터가 없습니다'}), 400
        
        # 데이터 폴더가 없으면 생성
        bookmark_data_dir = r'C:\woopang\server\bookmark\data'
        os.makedirs(bookmark_data_dir, exist_ok=True)
        
        bookmark_file = r'C:\woopang\server\bookmark\data\bookmarks.json'
        
        with open(bookmark_file, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
        
        safe_print(f"[Info] 북마크 저장 완료: {bookmark_file}")
        return jsonify({'success': True})
        
    except Exception as e:
        safe_print(f"[Error] 북마크 저장 오류: {str(e)}")
        return jsonify({'success': False, 'error': str(e)}), 500

@app.route('/api/bookmarks/load', methods=['GET'])
def load_bookmarks():
    # Load bookmarks from JSON file
    # Ensure indentation is correct
    try:
        bookmark_file = r'C:\woopang\server\bookmark\data\bookmarks.json'
        
        if os.path.exists(bookmark_file):
            with open(bookmark_file, 'r', encoding='utf-8') as f:
                bookmarks = json.load(f)
            
            safe_print(f"[Info] 북마크 로드 완료: {bookmark_file}")
            return jsonify({'success': True, 'bookmarks': bookmarks})
        else:
            safe_print(f"[Info] 북마크 파일 없음: {bookmark_file}")
            return jsonify({'success': False, 'message': '저장된 북마크 없음'}), 404
            
    except Exception as e:
        safe_print(f"[Error] 북마크 로드 오류: {str(e)}")
        return jsonify({'success': False, 'error': str(e)}), 500

# DongDong File Share Proxy (Port 5010)
@app.route('/dongdong', defaults={'path': ''})
@app.route('/dongdong/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'])
def dongdong_proxy(path):
    """DongDong service proxy - using Vrompt-style file handling"""
    target_url = f"http://localhost:5010/{path}"

    if request.query_string:
        target_url += f"?{request.query_string.decode('utf-8')}"

    print(f"[DongDong Proxy] {request.method} {request.path} → {target_url}")

    try:
        # Copy headers (exclude host and content-type for multipart)
        headers = {}
        for key, value in request.headers.items():
            if key.lower() not in ['host', 'content-type', 'content-length']:
                headers[key] = value

        if request.method == 'OPTIONS':
            response = Response('', 200)
            response.headers['Access-Control-Allow-Origin'] = '*'
            response.headers['Access-Control-Allow-Methods'] = 'GET, POST, PUT, DELETE, OPTIONS'
            response.headers['Access-Control-Allow-Headers'] = 'Content-Type, Authorization'
            return response

        elif request.method == 'POST':
            if request.files:
                # File upload - handle multiple files with HTTPX streaming
                print(f"[DongDong Proxy] File upload detected")
                print(f"[DongDong Proxy] request.files.keys(): {list(request.files.keys())}")

                # DongDong specific: 10GB per file limit
                MAX_FILE_SIZE = 10 * 1024 * 1024 * 1024  # 10GB

                import httpx

                files = []
                for key in request.files:
                    file_list = request.files.getlist(key)
                    print(f"[DongDong Proxy] Key '{key}' has {len(file_list)} file(s)")

                    for file in file_list:
                        # Get file size without reading entire content
                        file.stream.seek(0, 2)
                        file_size = file.stream.tell()
                        file.stream.seek(0)  # Reset to beginning

                        print(f"[DongDong Proxy] - File '{file.filename}': {file_size} bytes")

                        # Check file size limit (4GB for DongDong)
                        if file_size > MAX_FILE_SIZE:
                            error_msg = f"File '{file.filename}' exceeds 4GB limit ({file_size / (1024**3):.2f}GB)"
                            print(f"[DongDong Proxy] ERROR: {error_msg}")
                            return jsonify({'error': error_msg}), 413  # 413 Payload Too Large

                        # HTTPX automatically streams file uploads - no manual buffering needed!
                        # Just pass the stream directly - httpx handles chunking
                        files.append((key, (file.filename, file.stream, file.content_type)))

                print(f"[DongDong Proxy] Forwarding {len(files)} file(s) total using HTTPX streaming")
                print(f"[DongDong Proxy] Form data: {dict(request.form)}")

                # Use HTTPX for better streaming support
                with httpx.Client(timeout=3600.0) as client:
                    resp = client.post(
                        target_url,
                        data=request.form.to_dict(),
                        files=files,
                        headers=headers
                    )
            elif request.is_json:
                # JSON request
                resp = requests.post(target_url, json=request.get_json(), headers=headers, timeout=600)
            else:
                # Other POST data
                resp = requests.post(target_url, data=request.get_data(), headers=headers, timeout=600)

        elif request.method == 'GET':
            resp = requests.get(target_url, params=request.args, headers=headers, timeout=300, stream=True)
            
            # Stream the response back to client (Crucial for video/audio seeking)
            def generate():
                for chunk in resp.iter_content(chunk_size=1024 * 64): # 64KB chunks
                    if chunk:
                        yield chunk

            response = Response(stream_with_context(generate()), resp.status_code)

            # Copy response headers
            excluded_headers = ['content-encoding', 'content-length', 'transfer-encoding', 'connection']
            for key, value in resp.headers.items():
                if key.lower() not in excluded_headers:
                    response.headers[key] = value

            # Force no-cache for HTML responses (development mode)
            if path == '' and request.method == 'GET':
                response.headers['Cache-Control'] = 'no-store, no-cache, must-revalidate, post-check=0, pre-check=0, max-age=0'
                response.headers['Pragma'] = 'no-cache'
                response.headers['Expires'] = '-1'

            print(f"[DongDong Proxy] GET Stream ← {resp.status_code}")
            return response

        elif request.method == 'DELETE':
            resp = requests.delete(target_url, json=request.get_json() if request.is_json else None, headers=headers, timeout=30)

        else:
            resp = requests.request(
                method=request.method,
                url=target_url,
                headers=headers,
                data=request.get_data(),
                timeout=600
            )

        # Build response
        response = Response(resp.content, resp.status_code)

        # Copy response headers
        excluded_headers = ['content-encoding', 'content-length', 'transfer-encoding', 'connection']
        for key, value in resp.headers.items():
            if key.lower() not in excluded_headers:
                response.headers[key] = value

        # Force no-cache for HTML responses (development mode)
        if path == '' and request.method == 'GET':
            response.headers['Cache-Control'] = 'no-store, no-cache, must-revalidate, post-check=0, pre-check=0, max-age=0'
            response.headers['Pragma'] = 'no-cache'
            response.headers['Expires'] = '-1'

        print(f"[DongDong Proxy] ← {resp.status_code}")
        return response

    except Exception as e:
        safe_print(f"[Error] DongDong Proxy Failed: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({'error': 'DongDong Service Unavailable'}), 503

# Preview 서버________________________________________________________________________________________________________________
@app.route('/preview', defaults={'path': ''})
@app.route('/preview/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'])
def preview_proxy(path):
    """Preview 관련 모든 요청을 localhost:5555로 프록시"""
    try:
        preview_url = f"http://localhost:5555/{path}"
        
        headers = dict(request.headers)
        headers.pop('Host', None)
        
        if request.method == 'OPTIONS':
            response = make_response('', 200)
            response.headers['Access-Control-Allow-Origin'] = request.headers.get('Origin', '*')
            response.headers['Access-Control-Allow-Methods'] = 'GET, POST, PUT, DELETE, OPTIONS'
            response.headers['Access-Control-Allow-Headers'] = 'Content-Type, Authorization, X-Requested-With'
            response.headers['Access-Control-Allow-Credentials'] = 'true'
            return response
            
        elif request.method == 'GET':
            response = requests.get(preview_url, params=request.args, headers=headers, timeout=30)
            
        elif request.method == 'POST':
            if request.is_json:
                response = requests.post(preview_url, json=request.get_json(), headers=headers, timeout=300)
            else:
                files = {}
                for key, file in request.files.items():
                    files[key] = (file.filename, file.read(), file.content_type)
                
                upload_headers = {k: v for k, v in headers.items() if k.lower() != 'content-type'}
                
                response = requests.post(
                    preview_url, 
                    data=request.form.to_dict(),
                    files=files if files else None,
                    headers=upload_headers,
                    timeout=600
                )
                
        elif request.method == 'PUT':
            response = requests.put(preview_url, json=request.get_json(), headers=headers, timeout=300)
        elif request.method == 'DELETE':
            response = requests.delete(preview_url, headers=headers, timeout=30)
        
        if 'text/html' in response.headers.get('Content-Type', ''):
            return response.content.decode('utf-8'), response.status_code, {'Content-Type': 'text/html; charset=utf-8'}
        elif 'application/json' in response.headers.get('Content-Type', ''):
            return response.content, response.status_code, {'Content-Type': 'application/json'}
        elif 'attachment' in response.headers.get('Content-Disposition', ''):
            excluded_headers = ['content-encoding', 'content-length', 'transfer-encoding', 'connection']
            response_headers = [(name, value) for (name, value) in response.headers.items() 
                               if name.lower() not in excluded_headers]
            return response.content, response.status_code, response_headers
        else:
            content_type = response.headers.get('Content-Type', 'application/octet-stream')
            return response.content, response.status_code, [('Content-Type', content_type)]
        
    except requests.exceptions.ConnectionError:
        if path.endswith('.html') or 'api' not in path:
            return render_preview_error_page(), 503
        return jsonify({"error": "Preview 서비스를 사용할 수 없습니다."}), 503
    except Exception as e:
        safe_print(f"[Error] Preview 프록시 오류: {e}")
        return jsonify({"error": "서비스 오류가 발생했습니다."}), 500


@app.route('/share', defaults={'path': ''})
@app.route('/share/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'])
def share_proxy(path):
    """Preview 서비스의 공유 페이지 프록시"""
    
    full_path = request.path
    relative_path = full_path[len('/share'):].lstrip('/')
    
    if not relative_path:
        relative_path = ''
    
    preview_url = f"http://127.0.0.1:5555/share/{relative_path}"
    if request.query_string:
        preview_url += f"?{request.query_string.decode('utf-8')}"
    
    # [로그 수정] 요청 로그는 유지 (디버깅에 필요할 수 있음)
    print(f"[Share Proxy] {request.method} {full_path} → {preview_url}")
    
    try:
        headers = dict(request.headers)
        headers.pop('Host', None)
        
        # --- (OPTIONS, GET, POST, PUT, DELETE 요청 처리 부분은 그대로 유지) ---
        if request.method == 'OPTIONS':
            response = make_response('', 200)
            response.headers['Access-Control-Allow-Origin'] = '*'
            response.headers['Access-Control-Allow-Methods'] = 'GET, POST, PUT, DELETE, OPTIONS'
            response.headers['Access-Control-Allow-Headers'] = 'Content-Type'
            return response
        
        elif request.method == 'GET':
            resp = requests.get(preview_url, headers=headers, timeout=30)
        
        elif request.method == 'POST':
            if request.is_json:
                resp = requests.post(preview_url, json=request.get_json(), headers=headers, timeout=300)
            else:
                resp = requests.post(preview_url, data=request.form.to_dict(), headers=headers, timeout=300)
        
        elif request.method == 'PUT':
            resp = requests.put(preview_url, json=request.get_json(), headers=headers, timeout=300)
        
        elif request.method == 'DELETE':
            resp = requests.delete(preview_url, headers=headers, timeout=30)
        
        else:
            resp = requests.request(
                method=request.method,
                url=preview_url,
                headers=headers,
                data=request.get_data(),
                timeout=300
            )
        # --- (여기까지는 기존 코드와 동일) ---

        response = make_response(resp.content, resp.status_code)
        
        excluded_headers = ['content-encoding', 'content-length', 'transfer-encoding', 'connection']
        for key, value in resp.headers.items():
            if key.lower() not in excluded_headers:
                response.headers[key] = value
        
        # --- [로그 수정] ---
        # 성공(2xx) 응답 코드는 로그 출력 안 함, 오류(4xx, 5xx 등)만 출력
        if not (200 <= resp.status_code < 300):
            print(f"[Share Proxy] ← {resp.status_code}")
        # --- [로그 수정 끝] ---
            
        return response
        
    except requests.exceptions.ConnectionError:
        print(f"[Share Proxy Error] Preview service not reachable at localhost:5555")
        return "Preview service connection failed", 503
    except Exception as e:
        print(f"[Share Proxy Error] {e}")
        return "Share proxy error", 500

def render_preview_error_page():
    """Preview 서버 연결 실패 시 대체 페이지"""
    return """
    <!DOCTYPE html>
    <html lang="ko">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>WOOPANG AI - 서비스 점검 중</title>
        <style>
            body {
                font-family: 'Noto Sans KR', sans-serif;
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                color: white;
                margin: 0;
                padding: 20px;
                min-height: 100vh;
                display: flex;
                align-items: center;
                justify-content: center;
            }
            .container {
                text-align: center;
                background: rgba(0, 0, 0, 0.3);
                padding: 40px;
                border-radius: 15px;
                box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
                max-width: 500px;
            }
            .title {
                color: #FFD700;
                font-size: 28px;
                font-weight: bold;
                margin-bottom: 20px;
            }
            .message {
                font-size: 18px;
                margin-bottom: 30px;
                line-height: 1.6;
            }
            .contact {
                background: rgba(255, 255, 255, 0.1);
                padding: 20px;
                border-radius: 10px;
                margin: 20px 0;
            }
            .back-link {
                display: inline-block;
                color: #FFD700;
                text-decoration: none;
                font-size: 16px;
                margin-top: 20px;
                padding: 10px 20px;
                border: 2px solid #FFD700;
                border-radius: 10px;
                transition: all 0.3s ease;
            }
            .back-link:hover {
                background: #FFD700;
                color: #667eea;
            }
        </style>
    </head>
    <body>
        <div class="container">
            <div class="title">🎬 WOOPANG AI 서비스 점검 중</div>
            <div class="message">
                죄송합니다. 현재 영상 분석 서비스가 일시적으로 중단되었습니다.<br>
                빠른 시일 내에 복구하겠습니다.
            </div>
            <div class="contact">
                <div style="color: #FFD700; font-weight: bold; margin-bottom: 10px;">문의사항</div>
                <div>📧 이메일: pdnom@naver.com</div>
                <div>📞 전화: 010-4444-4395</div>
            </div>
            <a href="/" class="back-link">홈으로 돌아가기</a>
        </div>
    </body>
    </html>
    """



# Vrompt API (포트 8976) 프록시 추가
@app.route('/api/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'])
def api_proxy_handler(path):
    """Vrompt API 관련 모든 요청을 localhost:8976으로 프록시"""
    # apple 관련 엔드포인트는 직접 처리하므로 프록시하지 않음
    if path in ['apple-order', 'orders']:
        return jsonify({"error": "This endpoint should be handled directly"}), 500

    try:
        vrompt_url = f"http://localhost:8976/api/{path}"
        
        resp = requests.request(
            method=request.method,
            url=vrompt_url,
            headers={key: value for key, value in request.headers if key.lower() != 'host'},
            data=request.get_data(),
            params=request.args,
            stream=True,
            timeout=300.0
        )

        excluded_headers = ['content-encoding', 'transfer-encoding', 'connection']
        response_headers = [(name, value) for (name, value) in resp.raw.headers.items() if name.lower() not in excluded_headers]

        return Response(resp.iter_content(chunk_size=1024), status=resp.status_code, headers=response_headers)

    except requests.exceptions.ConnectionError:
        safe_print(f"[Error] Vrompt API 서비스 연결 실패: {vrompt_url}")
        return "Vrompt API 서비스를 사용할 수 없습니다.", 503
    except Exception as e:
        safe_print(f"[Error] Vrompt API 프록시 오류: {e}")
        return "Vrompt API 프록시 처리 중 오류가 발생했습니다.", 500

# Vrompt 서버 (포트 8976) 프록시 추가
@app.route('/vrompt', defaults={'path': ''})
@app.route('/vrompt/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'])
def vrompt_proxy_handler(path):
    """Vrompt 관련 모든 요청을 localhost:8976으로 프록시"""
    safe_print(f"[Debug] Vrompt Proxy entered for path: {path}") # 디버그 로그 추가
    try:
        vrompt_url = f"http://localhost:8976/{path}"
        
        # For consistency with other proxies in the file, use the 'requests' library.
        resp = requests.request(
            method=request.method,
            url=vrompt_url,
            headers={key: value for key, value in request.headers if key.lower() != 'host'},
            data=request.get_data(),
            params=request.args,
            stream=True,  # Enable streaming
            timeout=300.0
        )

        # Prepare headers for the Flask response, excluding certain headers.
        excluded_headers = ['content-encoding', 'transfer-encoding', 'connection']
        response_headers = [(name, value) for (name, value) in resp.raw.headers.items() if name.lower() not in excluded_headers]

        # Return a streaming response using the generator from requests.
        return Response(resp.iter_content(chunk_size=1024), status=resp.status_code, headers=response_headers)

    except requests.exceptions.ConnectionError:
        safe_print(f"[Error] Vrompt 서비스 연결 실패: {vrompt_url}")
        return render_vrompt_fallback(), 503
    except Exception as e:
        safe_print(f"[Error] Vrompt 프록시 오류: {e}")
        return render_vrompt_fallback(), 500


@app.route('/static/<path:filename>')
def serve_static(filename):
    try:
        return send_from_directory(app.static_folder, filename)
    except Exception as e:
        safe_print(f"[Error] 정적 파일 서빙 실패 {filename}: {e}")
        return jsonify({"error": "Failed to serve static file"}), 404



@app.route('/register-token', methods=['POST'])
def register_token():
    try:
        token = request.form.get('token')
        if not token:
            return jsonify({'error': 'Token is required'}), 400

        device_id = request.form.get('device_id', 'unknown')
        device_name = request.form.get('device_name', '')
        device_model = request.form.get('device_model', '')
        os_version = request.form.get('os_version', '')
        app_version = request.form.get('app_version', '')
        platform_hint = request.form.get('platform', '')
        user_id = request.form.get('user_id')  # 로그인된 사용자 ID

        if user_id:
            safe_print(f"[Info] 토큰에 user_id 연결: {user_id}")

        latitude_str = request.form.get('latitude')
        longitude_str = request.form.get('longitude')
        location_consent_str = request.form.get('location_consent')

        latitude = None
        longitude = None
        location_consent = False

        if latitude_str and longitude_str:
            try:
                latitude = float(latitude_str)
                longitude = float(longitude_str)
            except ValueError as ve:
                safe_print(f"위치 변환 실패: {ve}")

        if location_consent_str:
            location_consent = location_consent_str.lower() in ['true', '1', 'yes']

        # 플랫폼 자동 감지 (os_version 또는 device_model 기반)
        platform = 'android'  # 기본값
        if platform_hint:
            platform = platform_hint.lower()
        elif os_version:
            os_lower = os_version.lower()
            if 'ios' in os_lower or 'iphone' in os_lower or 'ipad' in os_lower:
                platform = 'ios'
        elif device_model:
            model_lower = device_model.lower()
            if 'iphone' in model_lower or 'ipad' in model_lower or 'ipod' in model_lower:
                platform = 'ios'

        # iOS인 경우 FCM 토큰도 작동하지만 apns_token으로도 저장
        fcm_token_value = token
        apns_token_value = token if platform == 'ios' else None

        safe_print(f"[Info] 토큰 등록: platform={platform}, device_model={device_model}, os_version={os_version}")

        success = save_token_with_coordinates(
            device_id=device_id,
            platform=platform,
            fcm_token=fcm_token_value,
            apns_token=apns_token_value,
            device_name=device_name,
            device_model=device_model,
            os_version=os_version,
            app_version=app_version,
            latitude=latitude,
            longitude=longitude,
            location_consent=location_consent,
            user_id=user_id
        )
        
        try:
            response = messaging.subscribe_to_topic(token, 'all')
        except Exception as fb_error:
            safe_print(f"Firebase 토픽 구독 실패: {fb_error}")
        
        return jsonify({
            'message': f'{platform.upper()} token registered successfully',
            'platform': platform,
            'success_count': 1,
            'failure_count': 0,
            'location_processed': location_consent and latitude and longitude
        })
        
    except Exception as e:
        safe_print(f"[Error] /register-token 오류: {e}")
        return jsonify({'error': str(e)}), 500


@app.route('/unregister-user-token', methods=['POST'])
def unregister_user_token():
    """로그아웃 시 토큰에서 user_id 제거 (토큰 자체는 유지)"""
    try:
        device_id = request.form.get('device_id')
        user_id = request.form.get('user_id')

        if not device_id:
            return jsonify({'error': 'device_id is required'}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # device_id로 토큰 찾아서 user_id만 NULL로 설정
        if user_id:
            # 특정 user_id가 있는 경우 해당 user_id만 제거
            cursor.execute("""
                UPDATE tokens
                SET user_id = NULL, updated_at = NOW()
                WHERE device_id = %s AND user_id = %s
            """, (device_id, user_id))
        else:
            # user_id 없이 호출된 경우 해당 device_id의 user_id를 제거
            cursor.execute("""
                UPDATE tokens
                SET user_id = NULL, updated_at = NOW()
                WHERE device_id = %s
            """, (device_id,))

        affected_rows = cursor.rowcount
        conn.commit()
        cursor.close()
        conn.close()

        if affected_rows > 0:
            safe_print(f"[Info] 토큰에서 user_id 제거 완료: device_id={device_id[:20]}...")
            return jsonify({
                'success': True,
                'message': 'User unregistered from token successfully'
            })
        else:
            return jsonify({
                'success': True,
                'message': 'No matching token found (already unregistered or device not registered)'
            })

    except Exception as e:
        safe_print(f"[Error] /unregister-user-token 오류: {e}")
        return jsonify({'error': str(e)}), 500


@app.route('/register-apns-token', methods=['POST'])
def register_apns_token():
    try:
        apns_token = request.form.get('token')
        fcm_token = request.form.get('fcm_token', '')
        device_id = request.form.get('device_id', 'unknown')
        device_name = request.form.get('device_name', '')
        device_model = request.form.get('device_model', '')
        os_version = request.form.get('os_version', '')
        app_version = request.form.get('app_version', '')
        latitude = request.form.get('latitude', type=float)
        longitude = request.form.get('longitude', type=float)
        location_consent = request.form.get('location_consent', 'false').lower() == 'true'
        
        if not apns_token:
            return jsonify({'error': 'APNs token is required'}), 400

        success = save_token_with_coordinates(
            device_id=device_id,
            platform='ios',
            fcm_token=None,
            apns_token=apns_token,
            device_name=device_name,
            device_model=device_model,
            os_version=os_version,
            app_version=app_version,
            latitude=latitude,
            longitude=longitude,
            location_consent=location_consent
        )
        
        if success:
            return jsonify({
                'message': 'iOS APNs token registered successfully',
                'location_processed': location_consent and latitude and longitude
            })
        else:
            return jsonify({'error': 'Failed to save iOS token'}), 500
            
    except Exception as e:
        safe_print(f"[Error] iOS APNs 토큰 등록 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/update-location', methods=['POST'])
def update_location():
    try:
        device_id = request.form.get('device_id')
        latitude_str = request.form.get('latitude')
        longitude_str = request.form.get('longitude')
        location_consent_str = request.form.get('location_consent')
        
        if not device_id:
            return jsonify({'error': 'device_id is required'}), 400
        
        try:
            if latitude_str and longitude_str:
                latitude = float(latitude_str)
                longitude = float(longitude_str)
            else:
                return jsonify({'error': 'latitude and longitude are required'}), 400
        except ValueError:
            return jsonify({'error': 'Invalid latitude or longitude format'}), 400
        
        location_consent = True
        
        safe_print(f"[Info] 위치 업데이트: {device_id[:20]}... → ({latitude}, {longitude})")
        
        location_address = ""
        try:
            location_address = get_address_from_coordinates(latitude, longitude)
            time.sleep(1)
        except Exception as addr_error:
            safe_print(f"주소 변환 실패: {addr_error}")
            location_address = f"위도 {latitude}, 경도 {longitude}"
        
        conn = get_db_connection()
        cursor = conn.cursor()
        
        cursor.execute("""
            UPDATE tokens 
            SET latitude = %s, longitude = %s, location_address = %s, 
                location_consent = 1, updated_at = NOW(), last_active = NOW()
            WHERE device_id = %s
        """, (latitude, longitude, location_address, device_id))
        
        if cursor.rowcount == 0:
            cursor.close()
            conn.close()
            return jsonify({'error': 'Device not found'}), 404
        
        conn.commit()
        cursor.close()
        conn.close()
        
        safe_print(f"[Info] 위치 업데이트 완료 (동의 상태도 변경): {device_id[:20]}...")
        
        return jsonify({
            'message': 'Location updated successfully',
            'latitude': latitude,
            'longitude': longitude,
            'address': location_address,
            'location_consent_updated': True
        })
        
    except Exception as e:
        safe_print(f"[Error] 위치 업데이트 실패: {e}")
        return jsonify({'error': str(e)}), 500

# Admin 인증 정보 (하드코딩)
ADMIN_USERNAME = 'pdnom'
ADMIN_PASSWORD_FIXED = 'Dnvkddl011$'

@app.route('/admin/login', methods=['GET', 'POST'])
def admin_login():
    if request.method == 'POST':
        username = request.form.get('username')
        password = request.form.get('password')
        if username == ADMIN_USERNAME and password == ADMIN_PASSWORD_FIXED:
            session['admin_logged_in'] = True
            return redirect(url_for('admin_send_message'))
        else:
            try:
                return render_template('admin_login.html', error='아이디 또는 비밀번호가 틀렸습니다.')
            except:
                return """
                <!DOCTYPE html>
                <html>
                <head><title>관리자 로그인</title><meta charset="utf-8"></head>
                <body>
                    <h2>관리자 로그인</h2>
                    <p style="color:red;">아이디 또는 비밀번호가 틀렸습니다.</p>
                    <form method="post">
                        <input type="text" name="username" placeholder="아이디" required>
                        <input type="password" name="password" placeholder="비밀번호" required>
                        <button type="submit">로그인</button>
                    </form>
                </body>
                </html>
                """

    try:
        return render_template('admin_login.html')
    except:
        return """
        <!DOCTYPE html>
        <html>
        <head><title>관리자 로그인</title><meta charset="utf-8"></head>
        <body>
            <h2>🔐 WOOPANG 관리자 로그인</h2>
            <form method="post">
                <p><input type="text" name="username" placeholder="아이디" required style="padding:10px; font-size:16px;"></p>
                <p><input type="password" name="password" placeholder="비밀번호" required style="padding:10px; font-size:16px;"></p>
                <p><button type="submit" style="padding:10px 20px; font-size:16px;">로그인</button></p>
            </form>
        </body>
        </html>
        """

@app.route('/admin/send-message', methods=['GET', 'POST'])
def admin_send_message():
    if not session.get('admin_logged_in'):
        return redirect(url_for('admin_login'))

    if request.method == 'POST':
        try:
            target_lat = request.form.get('targetLat', type=float)
            target_lon = request.form.get('targetLon', type=float)
            radius = request.form.get('radius', type=float)
            title = request.form.get('title')
            body = request.form.get('body')

            if not all([title, body]):
                return render_template('send_message.html', error='제목과 본문은 필수 입력 항목입니다.')

            android_success = 0
            ios_success = 0

            if all([target_lat, target_lon, radius]):
                safe_print(f"[Info] 관리자 좌표 기반 메시지 전송")
                safe_print(f"[Info] 타겟 좌표: ({target_lat}, {target_lon}), 반경: {radius}m")
                
                location_tokens = get_tokens_by_location_radius(target_lat, target_lon, radius)
                android_tokens = location_tokens['android_tokens']
                ios_tokens = location_tokens['ios_tokens']
                
                for token in android_tokens:
                    try:
                        message = messaging.Message(
                            data={
                                'title': title,
                                'body': body,
                                'latitude': str(target_lat),
                                'longitude': str(target_lon),
                                'radius': str(radius),
                                'click_action': 'OPEN_MESSAGE_PANEL'
                            },
                            android=messaging.AndroidConfig(
                                priority='high'
                            ),
                            token=token
                        )
                        response = messaging.send(message)
                        android_success += 1
                        safe_print(f"[Info] 관리자 Android 좌표 전송 성공: {android_success}")
                    except Exception as e:
                        safe_print(f"[Error] 관리자 Android 개별 전송 실패: {e}")

                for token in ios_tokens:
                    success = send_apns_notification_http2(token, title, body, APNS_ENV, {
                        'latitude': str(target_lat),
                        'longitude': str(target_lon),
                        'radius': str(radius)
                    })
                    if success:
                        ios_success += 1

                success_message = f'좌표 기반 메시지 전송 완료! 타겟: ({target_lat}, {target_lon}) 반경 {radius}m - Android: {android_success}, iOS: {ios_success}개 기기'
                
            else:
                safe_print(f"[Info] 관리자 전체 사용자 메시지 전송")
                
                try:
                    message = messaging.Message(
                        data={
                            'title': title,
                            'body': body,
                            'click_action': 'OPEN_MESSAGE_PANEL'
                        },
                        android=messaging.AndroidConfig(
                            priority='high'
                        ),
                        topic='all'
                    )
                    response = messaging.send(message)
                    android_success = 1
                    safe_print(f"[Info] 관리자 Android 토픽 전송 성공: {response}")
                except Exception as e:
                    safe_print(f"[Error] 관리자 Android Firebase 전송 실패: {e}")

                ios_tokens = get_tokens_by_platform('ios')
                for token in ios_tokens:
                    success = send_apns_notification_http2(token, title, body, APNS_ENV)
                    if success:
                        ios_success += 1

                success_message = f'전체 사용자 메시지 전송 완료! Android: {android_success}, iOS: {ios_success}개 기기'

            try:
                return render_template('send_message.html', success=success_message)
            except:
                return f"""
                <!DOCTYPE html>
                <html>
                <head><title>메시지 전송</title><meta charset="utf-8"></head>
                <body>
                    <h2>푸시 메시지 전송</h2>
                    <p style="color:green;">{success_message}</p>
                    <a href="/admin/send-message">다시 전송</a> | <a href="/admin/logout">로그아웃</a>
                </body>
                </html>
                """
            
        except Exception as e:
            safe_print(f"[Error] 관리자 하이브리드 전송 실패: {e}")
            try:
                return render_template('send_message.html', error=str(e))
            except:
                return f"""
                <!DOCTYPE html>
                <html>
                <head><title>메시지 전송</title><meta charset="utf-8"></head>
                <body>
                    <h2>푸시 메시지 전송</h2>
                    <p style="color:red;">오류: {str(e)}</p>
                    <a href="/admin/send-message">다시 시도</a>
                </body>
                </html>
                """

    try:
        return render_template('send_message.html', error=None, success=None)
    except:
        return """
        <!DOCTYPE html>
        <html>
        <head><title>메시지 전송</title><meta charset="utf-8"></head>
        <body>
            <h2>WOOPANG 푸시 메시지 전송</h2>
            <form method="post">
                <h3>좌표 기반 전송 (선택사항)</h3>
                <p>
                    위도: <input type="number" step="any" name="targetLat" placeholder="37.5665">
                    경도: <input type="number" step="any" name="targetLon" placeholder="126.9780">
                    반경(m): <input type="number" name="radius" placeholder="1000">
                </p>
                
                <h3>메시지 내용</h3>
                <p>제목: <input type="text" name="title" required placeholder="알림 제목" style="width:300px;"></p>
                <p>내용: <textarea name="body" required placeholder="알림 내용" style="width:300px; height:100px;"></textarea></p>
                
                <p><button type="submit" style="padding:15px 30px; font-size:16px; background:#007bff; color:white; border:none;">전송하기</button></p>
            </form>
            
            <p><a href="/admin/logout">로그아웃</a> | <a href="/admin/tokens">토큰 현황</a></p>
        </body>
        </html>
        """


@app.route('/api/test-system-message', methods=['POST'])
def test_system_message():
    """
    테스트용 시스템 메시지 전송 API (개발용)
    curl -X POST "https://woopang.com/api/test-system-message" \
         -H "Content-Type: application/json" \
         -d '{"user_id": "6", "title": "WOOPANG", "body": "테스트 메시지입니다"}'
    """
    try:
        data = request.get_json()
        user_id = data.get('user_id')
        title = data.get('title', 'WOOPANG')
        body = data.get('body', '시스템 알림 테스트')

        if not user_id:
            return jsonify({'success': False, 'error': 'user_id 필수'}), 400

        # 해당 사용자의 FCM 토큰 조회
        with get_db_connection() as conn:
            cursor = conn.cursor(dictionary=True)
            cursor.execute("""
                SELECT fcm_token, platform FROM user_fcm_tokens
                WHERE user_id = %s AND fcm_token IS NOT NULL
            """, (user_id,))
            tokens = cursor.fetchall()

        if not tokens:
            return jsonify({'success': False, 'error': f'user_id {user_id}의 FCM 토큰 없음'}), 404

        android_success = 0
        ios_success = 0

        for token_info in tokens:
            fcm_token = token_info['fcm_token']
            platform = token_info.get('platform', 'android')

            if platform == 'android':
                try:
                    message = messaging.Message(
                        data={
                            'title': title,
                            'body': body,
                            'sender_id': '3',  # WOOPANG 시스템 ID
                            'sender_username': 'WOOPANG',
                            'click_action': 'OPEN_MESSAGE_PANEL'
                        },
                        android=messaging.AndroidConfig(priority='high'),
                        token=fcm_token
                    )
                    response = messaging.send(message)
                    android_success += 1
                    safe_print(f"[Test] Android 시스템 메시지 전송 성공: {response}")
                except Exception as e:
                    safe_print(f"[Error] Android 시스템 메시지 전송 실패: {e}")
            else:
                # iOS
                success = send_apns_notification_http2(fcm_token, title, body, APNS_ENV, {
                    'sender_id': '3',
                    'sender_username': 'WOOPANG'
                })
                if success:
                    ios_success += 1

        return jsonify({
            'success': True,
            'message': f'시스템 메시지 전송 완료',
            'android_success': android_success,
            'ios_success': ios_success
        })

    except Exception as e:
        safe_print(f"[Error] 테스트 시스템 메시지 실패: {e}")
        return jsonify({'success': False, 'error': str(e)}), 500


@app.route('/edit')
def video_editor():
    """영상 분석 및 편집 제안 페이지"""
    try:
        edit_html_path = r'C:\woopang\server\edit\edit.html'
        
        if os.path.exists(edit_html_path):
            with open(edit_html_path, 'r', encoding='utf-8') as f:
                return f.read()
        else:
            safe_print(f"[Error] edit.html 파일을 찾을 수 없습니다: {edit_html_path}")
            return f"""
            <!DOCTYPE html>
            <html>
            <head><title>WOOPANG 영상 편집</title><meta charset="utf-8"></head>
            <body>
                <h1>영상 편집 페이지를 찾을 수 없습니다</h1>
                <p>파일 경로: {edit_html_path}</p>
                <a href="/">홈으로 돌아가기</a>
            </body>
            </html>
            """, 404
            
    except Exception as e:
        safe_print(f"[Error] edit.html 로딩 실패: {e}")
        return f"""
        <!DOCTYPE html>
        <html>
        <head><title>오류</title><meta charset="utf-8"></head>
        <body>
            <h1>페이지 로딩 오류</h1>
            <p>오류: {str(e)}</p>
            <a href="/">홈으로 돌아가기</a>
        </body>
        </html>
        """, 500

@app.route('/edit/<path:filename>')
def serve_edit_files(filename):
    """edit 폴더의 정적 파일들 서빙"""
    try:
        edit_folder = r'C:\woopang\server\edit'
        return send_from_directory(edit_folder, filename)
    except Exception as e:
        safe_print(f"[Error] edit 폴더 파일 서빙 실패 {filename}: {e}")
        return jsonify({"error": "Failed to serve edit file"}), 404










@app.route('/admin/logout', methods=['GET'])
def admin_logout():
    session.pop('admin_logged_in', None)
    return redirect(url_for('home'))

@app.route('/admin/tokens', methods=['GET'])
def admin_tokens():
    if not session.get('admin_logged_in'):
        return redirect(url_for('admin_login'))
    
    try:
        conn = get_db_connection()
        cursor = conn.cursor()
        
        cursor.execute("""
            SELECT 
                platform,
                COUNT(*) as total_devices,
                COUNT(CASE WHEN fcm_token IS NOT NULL THEN 1 END) as fcm_tokens,
                COUNT(CASE WHEN apns_token IS NOT NULL THEN 1 END) as apns_tokens,
                COUNT(CASE WHEN latitude IS NOT NULL AND longitude IS NOT NULL THEN 1 END) as with_location,
                COUNT(CASE WHEN location_consent = 1 THEN 1 END) as location_consent_granted,
                MAX(last_active) as last_activity
            FROM tokens 
            GROUP BY platform
            ORDER BY platform
        """)
        
        stats = cursor.fetchall()
        
        cursor.execute("""
            SELECT device_id, platform, device_name, device_model, os_version, app_version, 
                   latitude, longitude, location_consent, created_at, last_active
            FROM tokens 
            ORDER BY created_at DESC 
            LIMIT 20
        """)
        
        recent_tokens = cursor.fetchall()
        cursor.close()
        conn.close()
        
        return jsonify({
            'platform_stats': [
                {
                    'platform': row[0],
                    'total_devices': row[1],
                    'fcm_tokens': row[2],
                    'apns_tokens': row[3],
                    'with_location': row[4],
                    'location_consent_granted': row[5],
                    'last_activity': row[6].isoformat() if row[6] else None
                }
                for row in stats
            ],
            'recent_tokens': [
                {
                    'device_id': row[0][:20] + '...',
                    'platform': row[1],
                    'device_name': row[2],
                    'device_model': row[3],
                    'os_version': row[4],
                    'app_version': row[5],
                    'latitude': row[6],
                    'longitude': row[7],
                    'location_consent': row[8],
                    'created_at': row[9].isoformat() if row[9] else None,
                    'last_active': row[10].isoformat() if row[10] else None
                }
                for row in recent_tokens
            ]
        })
        
    except Exception as e:
        safe_print(f"[Error] 토큰 상태 조회 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/admin/ios-tokens', methods=['GET'])
def admin_ios_tokens():
    if not session.get('admin_logged_in'):
        return redirect(url_for('admin_login'))
    
    try:
        conn = get_db_connection()
        cursor = conn.cursor()
        
        cursor.execute("""
            SELECT device_id, apns_token, device_name, device_model, 
                   os_version, app_version, latitude, longitude, location_consent,
                   created_at, last_active, updated_at
            FROM tokens 
            WHERE platform = 'ios' AND apns_token IS NOT NULL
            ORDER BY created_at DESC
        """)
        
        ios_tokens = cursor.fetchall()
        cursor.close()
        conn.close()
        
        tokens_list = []
        for row in ios_tokens:
            tokens_list.append({
                'device_id': row[0][:20] + '...',
                'apns_token_preview': row[1][:20] + '...' if row[1] else None,
                'device_name': row[2],
                'device_model': row[3],
                'os_version': row[4],
                'app_version': row[5],
                'latitude': row[6],
                'longitude': row[7],
                'location_consent': row[8],
                'created_at': row[9].isoformat() if row[9] else None,
                'last_active': row[10].isoformat() if row[10] else None,
                'updated_at': row[11].isoformat() if row[11] else None,
                'full_token': row[1]
            })
        
        return jsonify({
            'total_ios_tokens': len(tokens_list),
            'tokens': tokens_list,
            'apns_environment': APNS_ENV
        })
        
    except Exception as e:
        safe_print(f"[Error] iOS 토큰 목록 조회 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/admin/apns-status', methods=['GET'])
def admin_apns_status():
    if not session.get('admin_logged_in'):
        return redirect(url_for('admin_login'))
    
    try:
        status = {
            'apns_key_file_exists': os.path.exists(APNS_KEY_FILE),
            'APNS_KEY_FILE': APNS_KEY_FILE,
            'apns_key_id': APNS_KEY_ID,
            'apns_team_id': APNS_TEAM_ID,
            'apns_bundle_id': APNS_BUNDLE_ID,
            'apns_environment': APNS_ENV,
            'development_url': "https://api.development.push.apple.com",
            'production_url': "https://api.push.apple.com"
        }
        
        if os.path.exists(APNS_KEY_FILE):
            try:
                file_stats = os.stat(APNS_KEY_FILE)
                status['key_file_size'] = file_stats.st_size
                status['key_file_modified'] = datetime.fromtimestamp(file_stats.st_mtime).isoformat()
            except Exception as e:
                status['key_file_error'] = str(e)
        
        try:
            jwt_token = create_apns_jwt_token()
            status['jwt_token_generation'] = jwt_token is not None
            if jwt_token:
                status['jwt_token_preview'] = jwt_token[:50] + '...'
        except Exception as e:
            status['jwt_token_generation'] = False
            status['jwt_token_error'] = str(e)
        
        try:
            conn = get_db_connection()
            cursor = conn.cursor()
            cursor.execute("SELECT COUNT(*) FROM tokens WHERE platform = 'ios' AND apns_token IS NOT NULL")
            ios_token_count = cursor.fetchone()[0]
            status['registered_ios_tokens'] = ios_token_count
            cursor.close()
            conn.close()
        except Exception as e:
            status['token_count_error'] = str(e)
        
        return jsonify(status)
        
    except Exception as e:
        safe_print(f"[Error] APNs 상태 확인 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/test-ios-push', methods=['POST'])
def test_ios_push():
    try:
        data = request.get_json()
        device_token = data.get('device_token')
        title = data.get('title', 'WOOPANG 테스트')
        body = data.get('body', 'iOS APNs 직접 전송 테스트 메시지입니다!')
        environment = data.get('environment', APNS_ENV)
        
        if not device_token:
            return jsonify({'error': 'device_token is required'}), 400
        
        success = send_apns_notification_http2(device_token, title, body, environment)

        return jsonify({
            'message': 'iOS APNs 테스트 완료',
            'device_token': device_token[:20] + '...',
            'environment': environment,
            'success': success,
            'apns_config': {
                'key_id': APNS_KEY_ID,
                'team_id': APNS_TEAM_ID,
                'bundle_id': APNS_BUNDLE_ID,
                'key_file': APNS_KEY_FILE,
                'env': APNS_ENV
            }
        })
        
    except Exception as e:
        safe_print(f"[Error] iOS APNs 테스트 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/manual-register-ios-token', methods=['POST'])
def manual_register_ios_token():
    try:
        data = request.get_json()
        apns_token = data.get('apns_token')
        device_name = data.get('device_name', 'Test iOS Device')
        device_model = data.get('device_model', 'iPhone')
        
        if not apns_token:
            return jsonify({'error': 'apns_token is required'}), 400
        
        device_id = hashlib.md5(apns_token.encode()).hexdigest()
        
        success = save_token_with_coordinates(
            device_id=device_id,
            platform='ios',
            apns_token=apns_token,
            device_name=device_name,
            device_model=device_model,
            os_version='iOS 17.0',
            app_version='1.0.0'
        )
        
        if success:
            safe_print(f"[Info] 테스트용 iOS 토큰 수동 등록 완료: {apns_token[:20]}...")
            return jsonify({
                'message': 'iOS 토큰 수동 등록 성공',
                'device_id': device_id,
                'apns_token': apns_token[:20] + '...',
                'environment': APNS_ENV
            })
        else:
            return jsonify({'error': 'Failed to save iOS token'}), 500
            
    except Exception as e:
        safe_print(f"[Error] iOS 토큰 수동 등록 실패: {e}")
        return jsonify({'error': str(e)}), 500

# ============================================================
# 관리자 업로드 승인 API
# ============================================================

@app.route('/admin/pending-locations', methods=['GET'])
def admin_pending_locations():
    """대기 중인 업로드 목록 조회"""
    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            SELECT id, name, username, latitude, longitude, created_at, device_id, model_type
            FROM locations
            WHERE status = 'pending'
            ORDER BY created_at DESC
            LIMIT 100
        """)

        rows = cursor.fetchall()
        cursor.close()
        conn.close()

        locations = []
        for row in rows:
            locations.append({
                'id': row[0],
                'name': row[1],
                'username': row[2],
                'latitude': row[3],
                'longitude': row[4],
                'created_at': row[5].isoformat() if row[5] else None,
                'device_id': row[6],
                'model_type': row[7]
            })

        return jsonify({
            'count': len(locations),
            'locations': locations,
            'auto_approve_enabled': AUTO_APPROVE_UPLOADS
        })

    except Exception as e:
        safe_print(f"[Error] Pending locations 조회 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/admin/locations-for-approval', methods=['GET'])
def admin_locations_for_approval():
    """업로드 관리 페이지용 전체 위치 목록 조회 (최근 7일)"""
    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            SELECT id, name, username, latitude, longitude, altitude, status, created_at, updated_at, device_id, model_type
            FROM locations
            WHERE created_at > NOW() - INTERVAL '7 days'
            ORDER BY created_at DESC
            LIMIT 500
        """)

        rows = cursor.fetchall()
        cursor.close()
        conn.close()

        locations = []
        for row in rows:
            locations.append({
                'id': row[0],
                'name': row[1],
                'username': row[2],
                'latitude': float(row[3]) if row[3] else None,
                'longitude': float(row[4]) if row[4] else None,
                'altitude': float(row[5]) if row[5] else None,
                'status': row[6] or 'approved',
                'created_at': row[7].isoformat() if row[7] else None,
                'updated_at': row[8].isoformat() if row[8] else None,
                'device_id': row[9],
                'model_type': row[10]
            })

        return jsonify({
            'count': len(locations),
            'locations': locations,
            'auto_approve_enabled': AUTO_APPROVE_UPLOADS
        })

    except Exception as e:
        safe_print(f"[Error] Locations for approval 조회 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/admin/approve-location', methods=['POST'])
def admin_approve_location():
    """업로드 승인 및 업로더에게 푸시 알림 전송"""
    try:
        data = request.get_json() or request.form
        location_id = data.get('location_id')

        if not location_id:
            return jsonify({'error': 'location_id is required'}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 업로드 정보 조회 (device_id, name, username, 좌표 포함)
        cursor.execute("""
            SELECT device_id, name, username, latitude, longitude FROM locations WHERE id = %s
        """, (location_id,))

        result = cursor.fetchone()
        if not result:
            cursor.close()
            conn.close()
            return jsonify({'error': 'Location not found'}), 404

        device_id, location_name, username, latitude, longitude = result

        # 상태를 approved로 변경
        cursor.execute("""
            UPDATE locations SET status = 'approved' WHERE id = %s
        """, (location_id,))

        conn.commit()
        cursor.close()
        conn.close()

        # 업로더에게 푸시 알림 + DM 전송 (좌표 포함)
        notification_sent = False
        if device_id or username:
            notification_sent = send_upload_approved_notification(device_id, location_name, username, latitude, longitude)

        safe_print(f"[Info] 업로드 승인 완료: ID={location_id}, 이름={location_name}, 사용자={username}, 좌표=({latitude}, {longitude}), 알림전송={notification_sent}")

        return jsonify({
            'message': 'Location approved successfully',
            'location_id': location_id,
            'location_name': location_name,
            'notification_sent': notification_sent
        })

    except Exception as e:
        safe_print(f"[Error] 업로드 승인 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/admin/reject-location', methods=['POST'])
def admin_reject_location():
    """업로드 거부"""
    try:
        data = request.get_json() or request.form
        location_id = data.get('location_id')
        reason = data.get('reason', '')

        if not location_id:
            return jsonify({'error': 'location_id is required'}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 업로드 정보 조회
        cursor.execute("""
            SELECT device_id, name FROM locations WHERE id = %s
        """, (location_id,))

        result = cursor.fetchone()
        if not result:
            cursor.close()
            conn.close()
            return jsonify({'error': 'Location not found'}), 404

        device_id, location_name = result

        # 상태를 rejected로 변경
        cursor.execute("""
            UPDATE locations SET status = 'rejected' WHERE id = %s
        """, (location_id,))

        conn.commit()
        cursor.close()
        conn.close()

        safe_print(f"[Info] 업로드 거부: ID={location_id}, 이름={location_name}, 사유={reason}")

        return jsonify({
            'message': 'Location rejected',
            'location_id': location_id,
            'location_name': location_name
        })

    except Exception as e:
        safe_print(f"[Error] 업로드 거부 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/admin/approve-all-pending', methods=['POST'])
def admin_approve_all_pending():
    """대기 중인 모든 업로드 일괄 승인"""
    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        # pending 상태인 모든 업로드 조회 (좌표 포함)
        cursor.execute("""
            SELECT id, name, username, device_id, latitude, longitude FROM locations
            WHERE status = 'pending'
            ORDER BY created_at ASC
        """)

        pending_locations = cursor.fetchall()

        if not pending_locations:
            cursor.close()
            conn.close()
            return jsonify({
                'message': 'No pending locations found',
                'approved_count': 0
            })

        # 모두 approved로 변경
        cursor.execute("""
            UPDATE locations SET status = 'approved' WHERE status = 'pending'
        """)

        conn.commit()
        cursor.close()
        conn.close()

        # 각 업로더에게 알림 전송
        approved_count = 0
        notification_sent_count = 0

        for loc in pending_locations:
            loc_id, loc_name, username, device_id, latitude, longitude = loc
            approved_count += 1

            if device_id or username:
                if send_upload_approved_notification(device_id, loc_name, username, latitude, longitude):
                    notification_sent_count += 1

        safe_print(f"[Info] 일괄 승인 완료: {approved_count}개 승인, {notification_sent_count}개 알림 전송")

        return jsonify({
            'message': 'All pending locations approved',
            'approved_count': approved_count,
            'notification_sent_count': notification_sent_count
        })

    except Exception as e:
        safe_print(f"[Error] 일괄 승인 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/admin/approve-by-period', methods=['POST'])
def admin_approve_by_period():
    """기간별 pending 데이터 일괄 승인 (오늘/일주일/한달)"""
    try:
        data = request.get_json() or request.form
        period = data.get('period', 'today')  # today, week, month

        # 기간에 따른 SQL 조건
        if period == 'today':
            date_condition = "created_at >= CURRENT_DATE"
            period_name = "오늘"
        elif period == 'week':
            date_condition = "created_at >= CURRENT_DATE - INTERVAL '7 days'"
            period_name = "일주일"
        elif period == 'month':
            date_condition = "created_at >= CURRENT_DATE - INTERVAL '30 days'"
            period_name = "한달"
        else:
            return jsonify({'error': 'Invalid period. Use: today, week, month'}), 400

        conn = get_db_connection()
        cursor = conn.cursor()

        # 해당 기간의 pending 상태인 업로드 조회
        cursor.execute(f"""
            SELECT id, name, username, device_id, latitude, longitude FROM locations
            WHERE status = 'pending' AND {date_condition}
            ORDER BY created_at ASC
        """)

        pending_locations = cursor.fetchall()

        if not pending_locations:
            cursor.close()
            conn.close()
            return jsonify({
                'message': f'{period_name} 동안 승인할 pending 데이터가 없습니다',
                'approved_count': 0,
                'notification_sent_count': 0
            })

        # 해당 기간의 pending만 approved로 변경
        location_ids = [loc[0] for loc in pending_locations]
        placeholders = ','.join(['%s'] * len(location_ids))
        cursor.execute(f"""
            UPDATE locations SET status = 'approved' WHERE id IN ({placeholders})
        """, location_ids)

        conn.commit()
        cursor.close()
        conn.close()

        # 각 업로더에게 알림 전송
        approved_count = 0
        notification_sent_count = 0

        for loc in pending_locations:
            loc_id, loc_name, username, device_id, latitude, longitude = loc
            approved_count += 1

            if device_id or username:
                if send_upload_approved_notification(device_id, loc_name, username, latitude, longitude):
                    notification_sent_count += 1

        safe_print(f"[Info] {period_name} 기간 일괄 승인 완료: {approved_count}개 승인, {notification_sent_count}개 알림 전송")

        return jsonify({
            'message': f'{period_name} 동안의 pending 데이터 승인 완료',
            'period': period,
            'approved_count': approved_count,
            'notification_sent_count': notification_sent_count
        })

    except Exception as e:
        safe_print(f"[Error] 기간별 일괄 승인 실패: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/admin/set-auto-approve', methods=['POST'])
def admin_set_auto_approve():
    """자동 승인 설정 변경 (메모리 + .env 파일 저장)"""
    global AUTO_APPROVE_UPLOADS
    try:
        data = request.get_json() or request.form
        enabled = data.get('enabled')

        if enabled is None:
            return jsonify({'error': 'enabled parameter is required'}), 400

        # 문자열 'true'/'false' 또는 불린값 처리
        if isinstance(enabled, str):
            AUTO_APPROVE_UPLOADS = enabled.lower() == 'true'
        else:
            AUTO_APPROVE_UPLOADS = bool(enabled)

        # .env 파일에 영구 저장
        env_saved = save_auto_approve_to_env(AUTO_APPROVE_UPLOADS)

        safe_print(f"[Info] 자동 승인 설정 변경: {AUTO_APPROVE_UPLOADS}, .env 저장: {env_saved}")

        return jsonify({
            'message': 'Auto approve setting updated',
            'auto_approve_enabled': AUTO_APPROVE_UPLOADS,
            'saved_to_env': env_saved
        })

    except Exception as e:
        safe_print(f"[Error] 자동 승인 설정 변경 실패: {e}")
        return jsonify({'error': str(e)}), 500

def save_auto_approve_to_env(enabled):
    """AUTO_APPROVE_UPLOADS 설정을 .env 파일에 저장"""
    try:
        env_file_path = os.path.join(os.path.dirname(__file__), '.env')

        # 기존 .env 파일 읽기
        lines = []
        found = False

        if os.path.exists(env_file_path):
            with open(env_file_path, 'r', encoding='utf-8') as f:
                lines = f.readlines()

            # AUTO_APPROVE_UPLOADS 라인 찾아서 수정
            for i, line in enumerate(lines):
                if line.strip().startswith('AUTO_APPROVE_UPLOADS'):
                    lines[i] = f"AUTO_APPROVE_UPLOADS={'true' if enabled else 'false'}\n"
                    found = True
                    break

        # 없으면 추가
        if not found:
            lines.append(f"\n# 업로드 자동 승인 설정\nAUTO_APPROVE_UPLOADS={'true' if enabled else 'false'}\n")

        # 파일에 쓰기
        with open(env_file_path, 'w', encoding='utf-8') as f:
            f.writelines(lines)

        return True

    except Exception as e:
        safe_print(f"[Error] .env 파일 저장 실패: {e}")
        return False

def send_upload_approved_notification(device_id, location_name, username=None, latitude=None, longitude=None):
    """업로드 승인 알림 전송 (푸시 + DM)"""
    push_sent = False
    dm_sent = False

    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        # 좌표 문자열 생성
        coord_str = ""
        if latitude is not None and longitude is not None:
            coord_str = f"({latitude:.2f}, {longitude:.2f})"

        # 메시지 내용 생성
        display_username = username if username else "회원"
        title = "업로드 등록 완료"
        body = f"{display_username}께서 요청하신 {location_name}이{coord_str} 등록되었습니다!"

        # 1. 푸시 알림 전송
        cursor.execute("""
            SELECT platform, fcm_token, apns_token FROM tokens WHERE device_id = %s
        """, (device_id,))
        result = cursor.fetchone()

        if result:
            platform, fcm_token, apns_token = result

            if platform == 'ios' and apns_token:
                push_sent = send_apns_notification_http2(
                    apns_token, title, body, APNS_ENV,
                    {"notification_type": "upload_approved", "location_name": location_name}
                )
            elif platform == 'android' and fcm_token:
                try:
                    message = messaging.Message(
                        notification=messaging.Notification(title=title, body=body),
                        data={"notification_type": "upload_approved", "location_name": location_name},
                        token=fcm_token
                    )
                    messaging.send(message)
                    push_sent = True
                except Exception as e:
                    safe_print(f"[Error] FCM 알림 전송 실패: {e}")
        else:
            safe_print(f"[Warning] 업로더 토큰을 찾을 수 없음: {device_id[:20] if device_id else 'None'}...")

        # DM 전송 제거: 푸시 알림이 클라이언트에서 시스템 알림으로 처리되므로
        # DM을 별도로 저장하면 중복 메시지가 발생함
        dm_sent = False

        cursor.close()
        conn.close()

        safe_print(f"[Info] 승인 알림 결과 - 푸시: {push_sent}, DM: {dm_sent}")
        return push_sent or dm_sent

    except Exception as e:
        safe_print(f"[Error] 승인 알림 전송 실패: {e}")
        return False

@app.route('/contact', methods=['POST'])
def contact_form():
    try:
        name = request.form.get('name', '').strip()
        email = request.form.get('email', '').strip()
        phone = request.form.get('phone', '').strip()
        message = request.form.get('message', '').strip()
        
        if not name or not email or not message:
            return jsonify({
                'success': False, 
                'error': 'Name, email, and message are required'
            }), 400
        
        if '@' not in email or '.' not in email:
            return jsonify({
                'success': False, 
                'error': 'Please enter a valid email address'
            }), 400
        
        safe_print(f"[Info] 연락처 폼 제출:")
        safe_print(f"  이름: {name}")
        safe_print(f"  이메일: {email}")
        safe_print(f"  전화번호: {phone}")
        safe_print(f"  메시지: {message[:100]}...")
        
        try:
            save_folder = r"C:\Users\pdnom\Desktop\WP_Email"
            os.makedirs(save_folder, exist_ok=True)
            
            file_path = os.path.join(save_folder, "contact_messages.txt")
            current_time = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
            
            contact_data = f"""
{'='*80}
접수 시간: {current_time}
이름: {name}
이메일: {email}
전화번호: {phone if phone else '미제공'}
메시지:
{message}
{'='*80}

"""
            
            with open(file_path, 'a', encoding='utf-8') as f:
                f.write(contact_data)
            
            safe_print(f"[Info] 연락처 메시지 저장 완료: {file_path}")
            
            date_str = datetime.now().strftime('%Y%m%d')
            individual_file = os.path.join(save_folder, f"contact_{date_str}_{name.replace(' ', '_')}.txt")
            
            with open(individual_file, 'w', encoding='utf-8') as f:
                f.write(f"WOOPANG 연락처 문의\n")
                f.write(f"접수일시: {current_time}\n\n")
                f.write(f"이름: {name}\n")
                f.write(f"이메일: {email}\n")
                f.write(f"전화번호: {phone if phone else '미제공'}\n\n")
                f.write(f"문의내용:\n{message}\n")
            
            safe_print(f"[Info] 개별 연락처 파일 저장: {individual_file}")
            
            return jsonify({
                'success': True,
                'message': 'Thank you for your message. We will get back to you soon!'
            })
            
        except Exception as file_error:
            safe_print(f"[Error] 연락처 메시지 저장 실패: {file_error}")
            
            return jsonify({
                'success': False,
                'error': 'An error occurred while saving your message. Please try again.'
            }), 500
        
    except Exception as e:
        safe_print(f"[Error] 연락처 폼 처리 실패: {e}")
        return jsonify({
            'success': False,
            'error': 'An error occurred. Please try again later.'
        }), 500

@app.route('/download')
def download_page():
    user_agent = request.headers.get('User-Agent', '').lower()
    
    is_mobile = any(mobile in user_agent for mobile in ['mobile', 'android', 'iphone', 'ipad'])
    is_android = 'android' in user_agent
    is_ios = any(ios in user_agent for ios in ['iphone', 'ipad', 'ios'])
    
    download_links = {
        'android': 'https://play.google.com/store/apps/details?id=com.woopang.app',  
        'ios': 'https://apps.apple.com/app/woopang/id1234567890',  
        'web': 'https://woopang.com'  
    }
    
    return render_template('download.html', 
                         is_mobile=is_mobile,
                         is_android=is_android, 
                         is_ios=is_ios,
                         download_links=download_links)

@app.route('/api/download-info')
def download_info_api():
    user_agent = request.headers.get('User-Agent', '').lower()
    
    is_mobile = any(mobile in user_agent for mobile in ['mobile', 'android', 'iphone', 'ipad'])
    is_android = 'android' in user_agent
    is_ios = any(ios in user_agent for ios in ['iphone', 'ipad', 'ios'])
    
    if is_android:
        recommended = 'android'
    elif is_ios:
        recommended = 'ios'
    else:
        recommended = 'web'
    
    return jsonify({
        'device_info': {
            'is_mobile': is_mobile,
            'is_android': is_android,
            'is_ios': is_ios,
            'user_agent': request.headers.get('User-Agent', '')
        },
        'recommended': recommended,
        'download_links': {
            'android': 'https://play.google.com/store/apps/details?id=com.woopang.app',
            'ios': 'https://apps.apple.com/app/woopang/id1234567890',
            'web': 'https://woopang.com'
        }
    })

@app.route('/download/<platform>')
def direct_download(platform):
    download_links = {
        'android': 'https://play.google.com/store/apps/details?id=com.woopang.app',
        'ios': 'https://apps.apple.com/app/woopang/id1234567890',
        'web': 'https://woopang.com'
    }
    
    if platform in download_links:
        return redirect(download_links[platform])
    else:
        return redirect(url_for('download_page'))




from flask import request, Response
import requests

@app.route('/ssa', defaults={'path': ''})
@app.route('/ssa/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE'])
@app.route('/ko/ssa', defaults={'path': ''})
@app.route('/ko/ssa/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE'])
def ssa_proxy(path):
    """Spotify 분석 서비스(/ssa, /ko/ssa) 요청을 localhost:3443으로 프록시"""
    # ✅ 들어온 전체 경로 기준으로 내부 경로 계산
    full_path = request.path
    if full_path.startswith('/ko/ssa'):
        relative_path = full_path[len('/ko/ssa'):].lstrip('/')
    elif full_path.startswith('/ssa'):
        relative_path = full_path[len('/ssa'):].lstrip('/')
    else:
        relative_path = path
    
    # ✅ SSA 내부 서버 주소 생성
    ssa_service_url = f"http://127.0.0.1:3443/{relative_path}"
    if request.query_string:
        ssa_service_url += f"?{request.query_string.decode('utf-8')}"
    
    # ✅ 요청 경로 매핑 로그 (디버그용)
    print(f"[SSA Proxy] {request.method} {full_path}  →  {ssa_service_url}")
    
    try:
        headers = {k: v for k, v in request.headers if k.lower() != 'host'}
        resp = requests.request(
            method=request.method,
            url=ssa_service_url,
            headers=headers,
            data=request.get_data(),
            cookies=request.cookies,
            allow_redirects=False,
            timeout=30
        )
        
        # ✅ CSP 완화 (Google Recaptcha, Spotify 허용)
        response = Response(resp.content, resp.status_code)
        for key, value in resp.headers.items():
            if key.lower() == 'content-security-policy':
                value = (
                    "default-src * 'unsafe-inline' 'unsafe-eval' data: blob:;"
                    "connect-src *;"
                    "img-src * data: blob:;"
                    "frame-src *;"
                    "script-src * 'unsafe-inline' 'unsafe-eval';"
                )
            response.headers[key] = value
        
        # ✅ 응답 상태 로그 (디버그용)
        print(f"[SSA Proxy] ← {resp.status_code} from SSA service ({ssa_service_url})")
        return response
        
    except requests.exceptions.ConnectionError:
        print("[SSA Proxy Error] SSA service not reachable at localhost:3443")
        return "SSA service connection failed", 503
    except Exception as e:
        print(f"[SSA Proxy Error] {e}")
        return "SSA proxy internal error", 500

# ==================== Vrompt 프록시 ====================

@app.route('/vrompt', defaults={'path': ''})
@app.route('/vrompt/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'])
def vrompt_proxy(path):
    """Vrompt 서비스 프록시 - Authorization 헤더 전달 수정"""
    
    full_path = request.path
    relative_path = full_path[len('/vrompt'):].lstrip('/')
    
    if not relative_path:
        relative_path = ''
    
    vrompt_url = f"http://127.0.0.1:8976/{relative_path}"
    if request.query_string:
        vrompt_url += f"?{request.query_string.decode('utf-8')}"
    
    # ✅ 로그 억제: job-status 요청은 로그 출력 안 함
    is_status_check = '/api/job-status/' in full_path
    
    if not is_status_check:
        print(f"[Vrompt Proxy] {request.method} {full_path} → {vrompt_url}")
    
    try:
        # ✅✅✅ 헤더 복사 - Authorization 헤더 포함하도록 수정! ✅✅✅
        headers = {}
        for key, value in request.headers.items():
            # Host 헤더만 제외하고 모두 복사 (Authorization 포함!)
            if key.lower() != 'host':
                headers[key] = value
        
        # 디버그: Authorization 헤더 확인
        if 'Authorization' in headers:
            print(f"[Vrompt Proxy] ✅ Authorization 헤더 전달: {headers['Authorization'][:50]}...")
        else:
            print(f"[Vrompt Proxy] ⚠️ Authorization 헤더 없음")
        
        if request.method == 'OPTIONS':
            response = Response('', 200)
            response.headers['Access-Control-Allow-Origin'] = '*'
            response.headers['Access-Control-Allow-Methods'] = 'GET, POST, PUT, DELETE, OPTIONS'
            response.headers['Access-Control-Allow-Headers'] = 'Content-Type, Authorization'
            response.headers['Access-Control-Allow-Credentials'] = 'true'
            return response
        
        elif request.method == 'POST':
            if request.is_json:
                # JSON 요청
                resp = requests.post(vrompt_url, json=request.get_json(), headers=headers, timeout=300)
            else:
                # 파일 업로드 처리
                files = {}
                for key, file in request.files.items():
                    files[key] = (file.filename, file.read(), file.content_type)
                
                # Content-Type 헤더만 제외 (multipart/form-data는 requests가 자동 처리)
                upload_headers = {k: v for k, v in headers.items() if k.lower() != 'content-type'}
                
                if not is_status_check:
                    print(f"[Vrompt Proxy] 파일: {list(files.keys())}, 폼: {list(request.form.keys())}")
                
                resp = requests.post(
                    vrompt_url,
                    data=request.form.to_dict(),
                    files=files if files else None,
                    headers=upload_headers,
                    timeout=600
                )
        
        elif request.method == 'GET':
            resp = requests.get(vrompt_url, params=request.args, headers=headers, timeout=30)
        
        elif request.method == 'PUT':
            resp = requests.put(vrompt_url, json=request.get_json(), headers=headers, timeout=300)
        
        elif request.method == 'DELETE':
            resp = requests.delete(vrompt_url, headers=headers, timeout=30)
        
        else:
            resp = requests.request(
                method=request.method,
                url=vrompt_url,
                headers=headers,
                data=request.get_data(),
                timeout=300
            )
        
        # 응답 반환
        response = Response(resp.content, resp.status_code)
        
        # 헤더 복사
        excluded_headers = ['content-encoding', 'content-length', 'transfer-encoding', 'connection']
        for key, value in resp.headers.items():
            if key.lower() not in excluded_headers:
                response.headers[key] = value
        
        # CORS 헤더 추가
        response.headers['Access-Control-Allow-Origin'] = request.headers.get('Origin', '*')
        response.headers['Access-Control-Allow-Credentials'] = 'true'
        
        if not is_status_check:
            print(f"[Vrompt Proxy] ← {resp.status_code}")
        
        return response
        
    except requests.exceptions.ConnectionError:
        print(f"[Error] Vrompt 연결 실패 (localhost:8976)")
        return "Vrompt service unavailable", 503
    except Exception as e:
        print(f"[Error] Vrompt 프록시 오류: {e}")
        import traceback
        traceback.print_exc()
        return "Proxy error", 500

# ==================== Portfolio 프록시 ====================

@app.route('/portpolio', defaults={'path': ''})
@app.route('/portpolio/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'])
def portpolio_proxy(path):
    """Portfolio 서비스 프록시"""

    full_path = request.path
    query_string = request.query_string.decode('utf-8')

    # /portpolio 또는 /portpolio/ 요청 시 index.html로 리다이렉트
    if path == '' or path == '/':
        path = 'index.html'

    portpolio_url = f"http://localhost:7788/{path}"
    if query_string:
        portpolio_url += f"?{query_string}"

    try:
        # OPTIONS 요청 처리 (CORS preflight)
        if request.method == 'OPTIONS':
            response = Response('', 200)
            response.headers['Access-Control-Allow-Origin'] = request.headers.get('Origin', '*')
            response.headers['Access-Control-Allow-Methods'] = 'GET, POST, PUT, DELETE, OPTIONS'
            response.headers['Access-Control-Allow-Headers'] = 'Content-Type, Authorization'
            response.headers['Access-Control-Allow-Credentials'] = 'true'
            return response

        # 실제 요청 프록시
        headers = {}
        for key, value in request.headers.items():
            if key.lower() not in ['host', 'connection']:
                headers[key] = value

        if request.method in ['POST', 'PUT']:
            resp = requests.request(
                method=request.method,
                url=portpolio_url,
                headers=headers,
                data=request.get_data(),
                timeout=30
            )
        else:
            resp = requests.request(
                method=request.method,
                url=portpolio_url,
                headers=headers,
                timeout=30
            )

        # 응답 반환
        response = Response(resp.content, resp.status_code)

        # 헤더 복사
        excluded_headers = ['content-encoding', 'content-length', 'transfer-encoding', 'connection']
        for key, value in resp.headers.items():
            if key.lower() not in excluded_headers:
                response.headers[key] = value

        # CORS 헤더 추가
        response.headers['Access-Control-Allow-Origin'] = request.headers.get('Origin', '*')
        response.headers['Access-Control-Allow-Credentials'] = 'true'

        return response

    except requests.exceptions.ConnectionError:
        print(f"[Error] Portfolio 연결 실패 (localhost:7788)")
        return "Portfolio service unavailable", 503
    except Exception as e:
        print(f"[Error] Portfolio 프록시 오류: {e}")
        import traceback
        traceback.print_exc()
        return "Proxy error", 500


# ==================== TIRE (타이어 거래소) 프록시 ====================
@app.route('/tire', defaults={'path': ''})
@app.route('/tire/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE'])
def tire_proxy(path):
    """TIRE 서비스 프록시 - localhost:5010으로 연결"""

    full_path = request.path
    relative_path = full_path[len('/tire'):].lstrip('/')

    tire_url = f"http://127.0.0.1:2684/{relative_path}"
    if request.query_string:
        tire_url += f"?{request.query_string.decode('utf-8')}"

    print(f"[TIRE Proxy] {request.method} {full_path} → {tire_url}")

    try:
        headers = {k: v for k, v in request.headers if k.lower() != 'host'}

        if request.method == 'GET':
            resp = requests.get(tire_url, headers=headers, timeout=30)
        elif request.method == 'POST':
            if request.is_json:
                resp = requests.post(tire_url, json=request.get_json(), headers=headers, timeout=60)
            else:
                resp = requests.post(tire_url, data=request.form, headers=headers, timeout=60)
        elif request.method == 'PUT':
            resp = requests.put(tire_url, json=request.get_json(), headers=headers, timeout=60)
        elif request.method == 'DELETE':
            resp = requests.delete(tire_url, headers=headers, timeout=30)
        else:
            resp = requests.request(
                method=request.method,
                url=tire_url,
                headers=headers,
                data=request.get_data(),
                timeout=60
            )

        excluded_headers = ['content-encoding', 'content-length', 'transfer-encoding', 'connection']
        response_headers = [(name, value) for (name, value) in resp.headers.items() if name.lower() not in excluded_headers]

        return Response(resp.content, resp.status_code, headers=response_headers)

    except requests.exceptions.ConnectionError:
        error_msg = "[TIRE Proxy Error] Could not connect to tire_server at localhost:2684. Is it running?"
        print(error_msg)
        return error_msg, 503
    except Exception as e:
        error_msg = f"[TIRE Proxy Error] Unexpected error: {str(e)}"
        print(error_msg)
        return error_msg, 500


# ============================================================
# Upload 완료 알림 API (앱에서 업로드 후 10초 뒤 호출)
# ============================================================
@app.route('/api/upload-notification', methods=['POST'])
def upload_notification():
    """
    업로드 완료 후 해당 사용자에게 FCM/APNs 알림 전송

    Request body:
        device_id: 디바이스 ID (필수)
        latitude: 업로드 위치 위도 (선택)
        longitude: 업로드 위치 경도 (선택)
        content_name: 업로드한 콘텐츠 이름 (선택)
    """
    try:
        data = request.get_json() or {}
        device_id = data.get('device_id')
        latitude = data.get('latitude')
        longitude = data.get('longitude')
        content_name = data.get('content_name', 'AR 콘텐츠')

        if not device_id:
            return jsonify({'success': False, 'error': 'device_id 필수'}), 400

        # 해당 디바이스의 토큰 조회
        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            SELECT fcm_token, apns_token, platform, user_id
            FROM tokens
            WHERE device_id = %s
            ORDER BY updated_at DESC NULLS LAST
            LIMIT 1
        """, (device_id,))

        token_row = cursor.fetchone()
        cursor.close()
        conn.close()

        if not token_row:
            safe_print(f"[Upload Notify] device_id={device_id} 토큰 없음")
            return jsonify({'success': False, 'error': '토큰 없음'}), 404

        fcm_token = token_row[0]
        apns_token = token_row[1]
        platform = token_row[2] or 'android'
        user_id = token_row[3]

        title = "🎉 콘텐츠 업로드 완료!"
        body = f"{content_name}이(가) 성공적으로 업로드되었습니다. 다른 사용자들이 볼 수 있어요!"

        notification_data = {
            'type': 'upload_complete',
            'content_name': content_name,
            'click_action': 'OPEN_MESSAGE_PANEL'
        }

        if latitude and longitude:
            notification_data['latitude'] = str(latitude)
            notification_data['longitude'] = str(longitude)

        android_success = False
        ios_success = False

        # FCM 전송 (Android)
        if fcm_token:
            try:
                message = messaging.Message(
                    data={
                        'title': title,
                        'body': body,
                        **{k: str(v) for k, v in notification_data.items()}
                    },
                    android=messaging.AndroidConfig(
                        priority='high',
                        notification=messaging.AndroidNotification(
                            title=title,
                            body=body,
                            sound='default'
                        )
                    ),
                    token=fcm_token
                )
                response = messaging.send(message)
                android_success = True
                safe_print(f"[Upload Notify] FCM 전송 성공: device={device_id}, user={user_id}")
            except Exception as e:
                safe_print(f"[Upload Notify] FCM 전송 실패: {e}")

        # APNs 전송 (iOS)
        if apns_token:
            ios_success = send_apns_notification_http2(
                apns_token, title, body, APNS_ENV, notification_data
            )
            if ios_success:
                safe_print(f"[Upload Notify] APNs 전송 성공: device={device_id}, user={user_id}")

        return jsonify({
            'success': android_success or ios_success,
            'android': android_success,
            'ios': ios_success,
            'device_id': device_id
        })

    except Exception as e:
        safe_print(f"[Upload Notify] Error: {e}")
        return jsonify({'success': False, 'error': str(e)}), 500


# 메인 실행 부분
if __name__ == '__main__':
    # 우팡 로고 출력
    print_woopang_logo()

    # 서버 헬스체크 스레드 시작 (10분 간격)
    start_health_check_thread()
    start_auto_renewal_thread()

    cleanup_old_data_on_startup()
    init_tables()  # Initialize new tables for comments and likes

    safe_print(f"{PINK}{BOLD}WOOPANG 좌표 기반 푸시 서버 시작 중...{RESET}")
    safe_print(f"{PASTEL_GREEN}Android: Firebase FCM (토픽 전송만)")
    safe_print(f"iOS: Apple APNs 직접 연동")
    safe_print(f"위치 정보: GPS 좌표 직접 저장 (latitude, longitude)")
    safe_print(f"메시지 전송: 관리자 웹페이지에서만 전송 가능")
    safe_print(f"실시간 위치 업데이트: /update-location 엔드포인트")
    safe_print(f"자동 정리: 6개월 후 좌표 데이터 자동 삭제{RESET}")
    safe_print("=" * 70)
    
    if os.path.exists(APNS_KEY_FILE):
        safe_print(f"[Info] APNs 키 파일 확인: {APNS_KEY_FILE}")
    else:
        safe_print(f"[Warning] APNs 키 파일 없음: {APNS_KEY_FILE}")
        safe_print("[Warning] iOS 푸시 알림이 작동하지 않을 수 있습니다!")
    
    safe_print("[Info] APNs 설정 검증 중...")
    validate_apns_config()
    
    safe_print("[Info] 서버 시작 시 오래된 좌표 데이터 정리 중...")
    cleanup_old_data_on_startup()
    
    cleanup_thread = threading.Thread(target=background_cleanup_scheduler, daemon=True)
    cleanup_thread.start()
    safe_print("[Info] 백그라운드 데이터 정리 스케줄러 시작됨 (24시간마다)")
    
    safe_print(f"[Debug] 환경변수 확인:")
    safe_print(f"  Android Version: {os.getenv('ANDROID_VERSION', 'Not Set')}")
    safe_print(f"  Android Force Update: {os.getenv('ANDROID_FORCE_UPDATE', 'Not Set')}")
    safe_print(f"  iOS Version: {os.getenv('IOS_VERSION', 'Not Set')}")
    safe_print(f"  iOS Force Update: {os.getenv('IOS_FORCE_UPDATE', 'Not Set')}")
    
    safe_print(f"[Debug] APNs 설정 확인:")
    safe_print(f"  키 ID: {APNS_KEY_ID}")
    safe_print(f"  팀 ID: {APNS_TEAM_ID}")
    safe_print(f"  Bundle ID: {APNS_BUNDLE_ID}")
    
    safe_print("[Info] 필요한 라이브러리 확인:")
    try:
        import jwt
        safe_print("  PyJWT (APNs 토큰용)")
    except ImportError:
        safe_print("  PyJWT 없음 - pip install PyJWT 필요")
    
    try:
        from cryptography.hazmat.primitives import serialization
        safe_print("  cryptography (APNs 키 처리용)")
    except ImportError:
        safe_print("  cryptography 없음 - pip install cryptography 필요")
    
    try:
        import httpx
        safe_print("  httpx (APNs HTTP/2용)")
    except ImportError:
        safe_print("  httpx 없음 - pip install httpx[http2] 필요")
    
    try:
        import math
        safe_print("  math (Haversine 거리 계산용)")
    except ImportError:
        safe_print("  math 라이브러리 오류")
    
    # P2P 서버 백그라운드 실행 (실시간 위치 + SpeechBubble)
    try:
        import subprocess
        import socket

        def is_port_in_use(port):
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                return s.connect_ex(('127.0.0.1', port)) == 0

        p2p_port = 5001
        if is_port_in_use(p2p_port):
            safe_print(f"[Info] P2P 서버 이미 실행 중 (포트 {p2p_port}) - 스킵")
        else:
            p2p_script = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'p2p_server.py')
            p2p_python = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'venv', 'Scripts', 'python.exe')
            if not os.path.exists(p2p_python):
                p2p_python = sys.executable
            if os.path.exists(p2p_script):
                p2p_process = subprocess.Popen(
                    [p2p_python, p2p_script],
                    cwd=os.path.dirname(os.path.abspath(__file__))
                    # stdout/stderr removed to show logs in same terminal
                )
                safe_print(f"[Info] P2P ?苡 獄?깆??? (PID: {p2p_process.pid}, ?: {p2p_port})")
            else:
                safe_print(f"[Warning] P2P 서버 스크립트 없음: {p2p_script}")
    except Exception as e:
        safe_print(f"[Warning] P2P 서버 시작 실패: {e}")

    # 서버 모드: USE_NGINX=true → Waitress (포트 8080), 아니면 Flask+SSL (포트 443)
    USE_NGINX = os.getenv('USE_NGINX', 'false').lower() == 'true'

    safe_print("[Debug] 좌표 기반 푸시 서버 시작...")
    safe_print("=" * 70)

    if USE_NGINX:
        # ===== nginx + Waitress 프로덕션 모드 =====
        safe_print("[Info] Waitress 프로덕션 서버 시작 (nginx 리버스 프록시 모드)")
        safe_print("[Info] 포트: 8080, 스레드: 20개")

        try:
            from waitress import serve
            serve(
                app,
                host='127.0.0.1',  # nginx만 접근 가능
                port=8080,
                threads=20,
                max_request_body_size=524288000,  # 500MB
                url_scheme='https',
                ident='WOOPANG'
            )
        except Exception as e:
            safe_print(f"[Error] Waitress 서버 시작 실패: {e}")
            traceback.print_exc()
    else:
        # ===== Flask + SSL 단독 모드 =====
        safe_print("[Info] Flask HTTPS 서버 시작 (단독 모드, 포트 443)")

        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        try:
            context.load_cert_chain(
                certfile='C:/woopang/server/woopang.com-fullchain.pem',
                keyfile='C:/woopang/server/woopang.com-privkey.pem'
            )
            safe_print("[Info] SSL 인증서 로딩 성공")
        except Exception as e:
            safe_print(f"[Error] SSL 인증서 로딩 실패: {e}")
            exit(1)

        try:
            test_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            test_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            test_socket.bind(('0.0.0.0', 443))
            test_socket.close()
            safe_print("[Info] 포트 443 사용 가능")
        except socket.error as e:
            safe_print(f"[Error] 포트 443 사용 불가: {e}")
            exit(1)

        try:
            app.run(
                host='0.0.0.0',
                port=443,
                ssl_context=context,
                debug=False,
                threaded=True,
                use_reloader=False
            )
        except Exception as e:
            safe_print(f"[Error] HTTPS 서버 시작 실패: {e}")
            traceback.print_exc()



# ==================== VDOWN Proxy ====================
@app.route('/vdown', defaults={'path': ''})
@app.route('/vdown/<path:path>', methods=['GET', 'POST'])
def vdown_proxy(path):
    vdown_url = f"http://127.0.0.1:5005/vdown/{path}" if path else "http://127.0.0.1:5005/vdown"

    # Handle root /vdown case
    if not path and request.path == '/vdown':
         vdown_url = "http://127.0.0.1:5005/vdown"

    print(f"[VDOWN Proxy] Request: {request.method} {request.path} -> Upstream: {vdown_url}")

    try:
        headers = {k: v for k, v in request.headers if k.lower() != 'host'}

        if request.method == 'GET':
            resp = requests.get(vdown_url, headers=headers, params=request.args, timeout=10)
        elif request.method == 'POST':
            if request.is_json:
                resp = requests.post(vdown_url, json=request.get_json(), headers=headers, timeout=60)
            else:
                resp = requests.post(vdown_url, data=request.form, headers=headers, timeout=60)
        
        print(f"[VDOWN Proxy] Upstream response: {resp.status_code}")

        excluded_headers = ['content-encoding', 'content-length', 'transfer-encoding', 'connection']
        response_headers = [(name, value) for (name, value) in resp.headers.items() if name.lower() not in excluded_headers]

        return Response(resp.content, resp.status_code, headers=response_headers)

    except requests.exceptions.ConnectionError:
        error_msg = f"[VDOWN Proxy Error] Could not connect to vdown_server at {vdown_url}. Is it running on port 5005?"
        print(error_msg)
        return error_msg, 503
    except Exception as e:
        error_msg = f"[VDOWN Proxy Error] Unexpected error: {str(e)}"
        print(error_msg)
        import traceback
        traceback.print_exc()
        return error_msg, 500
