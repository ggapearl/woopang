import os
import threading
import uuid
import time
import re
import binascii
import sys
import subprocess
import shutil
from Crypto.Cipher import AES
from Crypto.Util.Padding import unpad
import requests
from flask import Blueprint, request, render_template, jsonify, send_file
import yt_dlp

# Cloudflare 우회를 위한 curl_cffi
try:
    from curl_cffi import requests as cf_requests
    HAS_CURL_CFFI = True
except ImportError:
    HAS_CURL_CFFI = False
    print("[WARNING] curl_cffi not installed. Cloudflare bypass disabled.")

# JavaScript Cloudflare 우회를 위한 undetected-chromedriver
try:
    import undetected_chromedriver as uc
    HAS_UC = True
except ImportError:
    HAS_UC = False
    print("[WARNING] undetected-chromedriver not installed. JS Cloudflare bypass disabled.")

# FFmpeg 확인
HAS_FFMPEG = shutil.which('ffmpeg') is not None
if not HAS_FFMPEG:
    print("[WARNING] FFmpeg not found. Subtitle embedding will be disabled.")

vdown_bp = Blueprint('vdown', __name__, template_folder='templates', static_folder='static', url_prefix='/vdown')
UPLOAD_FOLDER = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'downloads')
if not os.path.exists(UPLOAD_FOLDER): os.makedirs(UPLOAD_FOLDER, exist_ok=True)

vdown_tasks = {}
cancel_flags = {}

def strip_ansi(text):
    if not text: return ""
    return re.compile(r'(?:\x1B[@-_][0-?]*[ -/]*[@-~])').sub('', text)


class CloudflareProtectedError(Exception):
    """Cloudflare JS 챌린지로 보호된 사이트 에러"""
    pass


def is_cloudflare_challenge(html):
    """Cloudflare 챌린지 페이지인지 감지"""
    cf_indicators = [
        'Just a moment',
        'cf-browser-verification',
        'Checking your browser',
        'DDoS protection by Cloudflare',
        'Enable JavaScript and cookies to continue',
        'challenge-platform',
        '_cf_chl_opt'
    ]
    return any(indicator in html for indicator in cf_indicators)


def smart_request(url, headers=None, referer=None, timeout=15, raise_on_cloudflare=False):
    """
    Cloudflare 우회를 지원하는 스마트 HTTP 요청
    """
    if headers is None:
        headers = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"}
    if referer:
        headers["Referer"] = referer

    try:
        r = requests.get(url, headers=headers, timeout=timeout)
        if not is_cloudflare_challenge(r.text):
            return r.text, False
    except:
        pass

    if HAS_CURL_CFFI:
        try:
            print("[CF-BYPASS] Cloudflare 감지됨, curl_cffi로 우회 시도...")
            r = cf_requests.get(url, headers=headers, timeout=timeout, impersonate="chrome")
            if not is_cloudflare_challenge(r.text):
                return r.text, True
        except Exception as e:
            print(f"[CF-BYPASS] curl_cffi 실패: {e}")

    if HAS_UC:
        driver = None
        try:
            print("[CF-BYPASS] JS Cloudflare 감지됨, undetected-chromedriver로 우회 시도...")
            options = uc.ChromeOptions()
            options.add_argument('--no-sandbox')
            options.add_argument('--disable-dev-shm-usage')
            options.add_argument('--disable-gpu')
            options.add_argument('--window-size=1920,1080')
            options.add_argument('--disable-blink-features=AutomationControlled')

            driver = uc.Chrome(options=options, headless=True, use_subprocess=False)
            driver.set_page_load_timeout(30)
            driver.get(url)

            for _ in range(20):
                time.sleep(1)
                if not is_cloudflare_challenge(driver.page_source) and len(driver.page_source) > 10000:
                    break
            html = driver.page_source
            if not is_cloudflare_challenge(html):
                print("[CF-BYPASS] undetected-chromedriver 성공!")
                return html, True
        except Exception as e:
            print(f"[CF-BYPASS] undetected-chromedriver 실패: {e}")
        finally:
            if driver:
                try:
                    driver.quit()
                except:
                    pass

    r = requests.get(url, headers=headers, timeout=timeout)

    if raise_on_cloudflare and is_cloudflare_challenge(r.text):
        raise CloudflareProtectedError(
            "이 사이트는 Cloudflare 보호가 적용되어 있어 자동 다운로드가 불가능합니다. "
            "브라우저에서 직접 접속하여 다운로드해 주세요."
        )

    return r.text, False


def get_user_friendly_error(error_str, url):
    """에러 메시지를 사용자 친화적으로 변환"""
    error_lower = error_str.lower()

    if 'cloudflare' in error_lower or 'just a moment' in error_lower:
        return "이 사이트는 Cloudflare 보호가 적용되어 있어 자동 다운로드가 불가능합니다. 브라우저에서 직접 접속하여 다운로드해 주세요."
    if '403' in error_str or 'forbidden' in error_lower:
        return "접근이 차단되었습니다. 이 사이트는 자동 다운로드를 허용하지 않거나 Cloudflare 보호가 적용되어 있습니다."
    if '404' in error_str or 'not found' in error_lower:
        return "영상을 찾을 수 없습니다. URL을 확인하거나 영상이 삭제되었는지 확인해 주세요."
    if 'ssl' in error_lower or 'certificate' in error_lower:
        return "보안 연결 오류가 발생했습니다. 사이트의 SSL 인증서에 문제가 있을 수 있습니다."
    if 'timeout' in error_lower or 'timed out' in error_lower:
        return "연결 시간이 초과되었습니다. 네트워크 상태를 확인하거나 나중에 다시 시도해 주세요."
    if 'connection' in error_lower or 'connect' in error_lower:
        return "사이트에 연결할 수 없습니다. 네트워크 상태를 확인하거나 사이트가 일시적으로 다운되었을 수 있습니다."
    if 'unsupported' in error_lower or 'no suitable' in error_lower:
        return "이 사이트는 현재 지원되지 않습니다. YouTube, Instagram 등 주요 사이트를 이용해 주세요."
    if 'no video' in error_lower or 'unable to extract' in error_lower:
        return "페이지에서 영상을 찾을 수 없습니다. URL이 올바른지 확인해 주세요."

    if len(error_str) > 200:
        return f"다운로드 실패: {error_str[:150]}..."
    return f"다운로드 실패: {error_str}"


def get_safe_filename(title, ext, upload_folder):
    """제목을 안전한 파일명으로 변환하고 중복 시 숫자 추가"""
    safe_title = re.sub(r'[<>:"/\\|?*]', '', title).strip()
    if not safe_title:
        safe_title = 'video'
    base_path = os.path.join(upload_folder, f"{safe_title}{ext}")
    if not os.path.exists(base_path):
        return base_path, f"{safe_title}{ext}"
    counter = 1
    while True:
        new_name = f"{safe_title}_{counter}{ext}"
        new_path = os.path.join(upload_folder, new_name)
        if not os.path.exists(new_path):
            return new_path, new_name
        counter += 1


def get_language_name(lang_code):
    """언어 코드를 한국어 이름으로 변환"""
    lang_map = {
        'ko': '한국어', 'kr': '한국어', 'kor': '한국어', 'korean': '한국어',
        'en': '영어', 'eng': '영어', 'english': '영어',
        'ja': '일본어', 'jp': '일본어', 'jpn': '일본어', 'japanese': '일본어',
        'zh': '중국어', 'cn': '중국어', 'chi': '중국어', 'chinese': '중국어',
        'es': '스페인어', 'spa': '스페인어', 'spanish': '스페인어',
        'fr': '프랑스어', 'fra': '프랑스어', 'french': '프랑스어',
        'de': '독일어', 'ger': '독일어', 'german': '독일어',
        'pt': '포르투갈어', 'por': '포르투갈어',
        'ru': '러시아어', 'rus': '러시아어',
        'vi': '베트남어', 'vie': '베트남어',
        'th': '태국어', 'tha': '태국어',
        'id': '인도네시아어', 'ind': '인도네시아어',
    }
    code_lower = lang_code.lower().split('-')[0]  # en-US -> en
    return lang_map.get(code_lower, lang_code)


def rename_subtitle_files(upload_folder, task_id, new_base_name):
    """자막 파일들의 이름을 영상 파일 이름에 맞게 변경하고 정보 반환"""
    renamed_files = []

    for filename in os.listdir(upload_folder):
        if filename.startswith(task_id):
            ext = os.path.splitext(filename)[1].lower()
            if ext in ['.srt', '.vtt', '.ass', '.ssa', '.sub', '.smi']:
                # 언어 코드 추출 (예: task_id.ko.srt -> ko)
                parts = filename.replace(task_id, '').lstrip('.').split('.')
                lang_code = ''
                for part in parts:
                    if part and part != ext[1:] and len(part) <= 10:
                        lang_code = part
                        break

                if lang_code:
                    new_filename = f"{new_base_name}.{lang_code}{ext}"
                else:
                    new_filename = f"{new_base_name}{ext}"

                old_path = os.path.join(upload_folder, filename)
                new_path = os.path.join(upload_folder, new_filename)

                # 중복 방지
                if os.path.exists(new_path) and old_path != new_path:
                    counter = 1
                    base, ext_part = os.path.splitext(new_filename)
                    while os.path.exists(new_path):
                        new_filename = f"{base}_{counter}{ext_part}"
                        new_path = os.path.join(upload_folder, new_filename)
                        counter += 1

                if old_path != new_path:
                    os.rename(old_path, new_path)

                renamed_files.append({
                    'filename': new_filename,
                    'lang_code': lang_code or 'unknown',
                    'lang_name': get_language_name(lang_code) if lang_code else '알 수 없음',
                    'format': ext[1:].upper()
                })

    return renamed_files


def decrypt_bunny_frame_url(enc_url):
    """bunny-frame의 암호화된 비디오 URL을 복호화"""
    try:
        # bunny-frame에서 사용하는 고정 키와 IV
        HEX_KEY = "0123456789abcdef0123456789abcdef"
        HEX_IV = "abcdef9876543210abcdef9876543210"

        key = bytes.fromhex(HEX_KEY)
        iv = bytes.fromhex(HEX_IV)
        ciphertext = bytes.fromhex(enc_url)

        cipher = AES.new(key, AES.MODE_CBC, iv)
        decrypted = unpad(cipher.decrypt(ciphertext), AES.block_size)

        return decrypted.decode('utf-8')
    except Exception as e:
        print(f"[DECRYPT] bunny-frame URL 복호화 실패: {e}")
        return None


def auto_extract_video_url(url, original_html=None):
    """범용 비디오 URL 자동 탐지 시스템"""
    results = {
        'title': None,
        'video_url': None,
        'subtitle_url': None,
        'headers': {'User-Agent': 'Mozilla/5.0'},
        'debug_info': [],
        'is_cloudflare': False
    }

    try:
        if original_html is None:
            html, cf_bypassed = smart_request(url)
            if cf_bypassed:
                results['debug_info'].append("[AUTO] Cloudflare 우회 성공")
            if is_cloudflare_challenge(html):
                results['debug_info'].append("[AUTO] Cloudflare 보호 감지됨 - 자동 다운로드 불가")
                results['is_cloudflare'] = True
                return results
        else:
            html = original_html

        results['debug_info'].append(f"[AUTO] 페이지 크기: {len(html)} bytes")

        title_patterns = [
            (r'<meta\s+property=["\']og:title["\']\s+content=["\']([^"\']+)["\']', 'og:title'),
            (r'<meta\s+name=["\']title["\']\s+content=["\']([^"\']+)["\']', 'meta title'),
            (r'<title>([^<]+)</title>', 'title tag'),
        ]
        for pattern, name in title_patterns:
            m = re.search(pattern, html, re.IGNORECASE)
            if m:
                results['title'] = m.group(1).strip()
                results['debug_info'].append(f"[AUTO] 제목 발견 ({name}): {results['title'][:50]}...")
                break

        video_patterns = [
            (r'(?:src|href|file|url|source)["\s:=]+["\']?(https?://[^\s"\'<>]+\.(?:mp4|m3u8|webm|mkv|avi|mov)(?:\?[^\s"\'<>]*)?)["\']?', 'direct video URL'),
            (r'["\']?(https?://[^\s"\'<>]+\.(?:mp4|m3u8|webm)(?:\?[^\s"\'<>]*)?)["\']?', 'any video URL'),
        ]

        for pattern, name in video_patterns:
            matches = re.findall(pattern, html, re.IGNORECASE)
            if matches:
                valid_urls = [u for u in matches if not any(x in u.lower() for x in ['ads', 'tracking', 'analytics', 'pixel', 'beacon'])]
                if valid_urls:
                    results['video_url'] = valid_urls[0].replace("\r", "").replace("\n", "")
                    results['debug_info'].append(f"[AUTO] 비디오 URL 발견 ({name}): {results['video_url'][:80]}...")

                    subtitle_patterns = [
                        r'["\']?(https?://[^\s"\'<>]+\.(?:vtt|srt|ass|smi)(?:\?[^\s"\'<>]*)?)["\']?',
                    ]
                    for sub_pattern in subtitle_patterns:
                        sub_matches = re.findall(sub_pattern, html, re.IGNORECASE)
                        if sub_matches:
                            results['subtitle_url'] = sub_matches[0].replace("\r", "").replace("\n", "")
                            results['debug_info'].append(f"[AUTO] 자막 URL 발견: {results['subtitle_url'][:80]}...")
                            break
                    return results

        iframe_patterns = [r'<iframe[^>]+src=["\']([^"\']+)["\']', r'<embed[^>]+src=["\']([^"\']+)["\']']
        iframes = []
        for pattern in iframe_patterns:
            iframes.extend(re.findall(pattern, html, re.IGNORECASE))

        results['debug_info'].append(f"[AUTO] iframe/embed 발견: {len(iframes)}개")

        for iframe_url in iframes[:5]:
            iframe_url = iframe_url.replace("&amp;", "&").replace("\r", "").replace("\n", "").strip()
            if not iframe_url.startswith('http'):
                continue
            if any(x in iframe_url.lower() for x in ['ads', 'doubleclick', 'googlesyndication', 'facebook.com/plugins']):
                continue

            results['debug_info'].append(f"[AUTO] iframe 분석 중: {iframe_url[:60]}...")

            try:
                r2 = requests.get(iframe_url, headers={"Referer": url, "User-Agent": "Mozilla/5.0"}, timeout=10)
                iframe_html = r2.text

                for pattern, name in video_patterns:
                    matches = re.findall(pattern, iframe_html, re.IGNORECASE)
                    valid_urls = [u for u in matches if not any(x in u.lower() for x in ['ads', 'tracking', 'analytics'])]
                    if valid_urls:
                        results['video_url'] = valid_urls[0].replace("\r", "").replace("\n", "")
                        results['headers'] = {'Referer': iframe_url, 'User-Agent': 'Mozilla/5.0'}
                        results['debug_info'].append(f"[AUTO] iframe 내 비디오 발견: {results['video_url'][:80]}...")
                        return results
            except Exception as e:
                results['debug_info'].append(f"[AUTO] iframe 분석 실패: {str(e)[:50]}")

        json_patterns = [
            r'"(?:video_?url|file|source|stream_?url|hls_?url|dash_?url)":\s*"([^"]+)"',
        ]
        for pattern in json_patterns:
            matches = re.findall(pattern, html, re.IGNORECASE)
            if matches:
                for match in matches:
                    if any(ext in match.lower() for ext in ['.mp4', '.m3u8', '.webm']):
                        results['video_url'] = match.replace("\\/", "/").replace("\r", "").replace("\n", "")
                        results['debug_info'].append(f"[AUTO] JSON 내 비디오 발견: {results['video_url'][:80]}...")
                        return results

        results['debug_info'].append("[AUTO] 자동 탐지 실패 - 비디오 URL을 찾지 못함")

    except Exception as e:
        results['debug_info'].append(f"[AUTO] 오류 발생: {str(e)}")

    return results


def direct_download_with_progress(url, output_path, task_id, headers=None):
    """직접 다운로드 (폴백 메서드)"""
    if headers is None:
        headers = {'User-Agent': 'Mozilla/5.0'}

    try:
        response = requests.get(url, headers=headers, stream=True, timeout=30)
        response.raise_for_status()

        total_size = int(response.headers.get('content-length', 0))
        downloaded = 0

        with open(output_path, 'wb') as f:
            for chunk in response.iter_content(chunk_size=8192):
                if cancel_flags.get(task_id):
                    raise Exception("Cancelled by user")
                if chunk:
                    f.write(chunk)
                    downloaded += len(chunk)
                    if total_size > 0:
                        percent = (downloaded / total_size) * 100
                        vdown_tasks[task_id]['progress'] = {
                            'percent': percent,
                            'speed': 'N/A',
                            'total': f'{total_size/(1024*1024):.1f}MB',
                            'downloaded': f'{downloaded/(1024*1024):.1f}MB'
                        }
        return True
    except Exception as e:
        print(f"Direct download failed: {e}")
        return False


def process_vdown_task(task_id, url, upload_folder, format_type='video'):
    print(f"[VDOWN] 다운로드 시작: task_id={task_id}, url={url}, format={format_type}")

    def progress_hook(d):
        if cancel_flags.get(task_id):
            raise Exception("Cancelled by user")

        if d['status'] == 'downloading':
            p_raw = strip_ansi(d.get('_percent_str', '0%')).replace('%', '').strip()
            s_raw = strip_ansi(d.get('_speed_str', '0B/s')).strip().replace('iB', 'B')
            try: p_val = float(p_raw)
            except: p_val = 0
            vdown_tasks[task_id]['progress'] = {
                'percent': p_val,
                'speed': s_raw,
                'total': strip_ansi(d.get('_total_bytes_str') or d.get('_total_bytes_estimate_str', 'N/A')).replace('iB', 'B'),
                'downloaded': strip_ansi(d.get('_downloaded_bytes_str', '0B')).replace('iB', 'B')
            }

    try:
        actual_url = url
        ydl_headers = {}
        extracted_title = None
        direct_download_url = None
        subtitle_url = None

        # --- TVWiki / TVMon Logic ---
        if 'tvwiki' in url or 'tvmon' in url:
            try:
                r = requests.get(url, headers={"User-Agent": "Mozilla/5.0"}, timeout=10)
                print(f"[DEBUG] TVWiki page length: {len(r.text)}")

                try:
                    m_title = re.search(r'<meta\s+property=["\']og:title["\']\s+content=["\']([^"\"]+)["\"]', r.text)
                    if m_title:
                        extracted_title = m_title.group(1).replace(" - 티비위키", "").replace(" - 티비몬", "").strip()
                    else:
                        m_title = re.search(r'<title>(.*?)</title>', r.text)
                        if m_title:
                            extracted_title = m_title.group(1).replace(" - 티비위키", "").replace(" - 티비몬", "").strip()
                except: pass
                print(f"[DEBUG] Title: {extracted_title}")

                # 제목을 task에 먼저 설정 (자막 다운로드 전에 표시용)
                if extracted_title:
                    vdown_tasks[task_id]['title'] = extracted_title

                # bunny-frame URL 찾기 (여러 패턴 시도)
                iframe_url = None

                # 패턴 1: data-player1 또는 data-player2 속성에서 찾기 (새로운 방식)
                m = re.search(r'data-player[12]\s*=\s*["\']([^"\']+)["\']', r.text)
                if m:
                    iframe_url = m.group(1).replace("&amp;", "&").replace("\r", "").replace("\n", "").strip()
                    if iframe_url.startswith('//'):
                        iframe_url = 'https:' + iframe_url
                    print(f"[DEBUG] data-player에서 찾음: {iframe_url}")

                # 패턴 2: src 속성에서 bunny-frame URL
                if not iframe_url:
                    m = re.search(r'src=(["\'])(https://player\.bunny-frame\.online/[^"\']+)\1', r.text)
                    if m:
                        iframe_url = m.group(2).replace("&amp;", "&").replace("\r", "").replace("\n", "").strip()

                # 패턴 3: src= 없이 URL만 있는 경우
                if not iframe_url:
                    m = re.search(r'["\']?(https?://player\.bunny-frame\.online/[^\s"\'<>]+)["\']?', r.text)
                    if m:
                        iframe_url = m.group(1).replace("&amp;", "&").replace("\r", "").replace("\n", "").strip()

                # 패턴 4: //로 시작하는 경우
                if not iframe_url:
                    m = re.search(r'["\']?(//player\.bunny-frame\.online/[^\s"\'<>]+)["\']?', r.text)
                    if m:
                        iframe_url = 'https:' + m.group(1).replace("&amp;", "&").replace("\r", "").replace("\n", "").strip()

                if iframe_url:
                    print(f"[DEBUG] Cleaned iframe URL: {iframe_url}")

                    # SRT 자막 먼저 확인 (더 신뢰성 높음)
                    from urllib.parse import unquote
                    srt_match = re.search(r'[?&]srt=([^&]+)', iframe_url)
                    if srt_match:
                        subtitle_url = unquote(srt_match.group(1))  # URL 디코딩
                        if subtitle_url.startswith('/'):
                            # 상대 경로인 경우 iframe 호스트 기준으로 절대 경로 생성
                            iframe_host_match = re.match(r'(https?://[^/]+)', iframe_url)
                            if iframe_host_match:
                                subtitle_url = iframe_host_match.group(1) + subtitle_url
                        elif not subtitle_url.startswith('http'):
                            subtitle_url = 'https://' + subtitle_url.lstrip('/')
                        print(f"[DEBUG] Subtitle URL from srt param: {subtitle_url}")
                    else:
                        # VTT 자막 확인
                        vtt_match = re.search(r'[?&]vtt=([^&]+)', iframe_url)
                        if vtt_match:
                            subtitle_url = unquote(vtt_match.group(1))  # URL 디코딩
                            if subtitle_url.startswith('/'):
                                iframe_host_match = re.match(r'(https?://[^/]+)', iframe_url)
                                if iframe_host_match:
                                    subtitle_url = iframe_host_match.group(1) + subtitle_url
                            elif not subtitle_url.startswith('http'):
                                subtitle_url = 'https://' + subtitle_url.lstrip('/')
                            print(f"[DEBUG] Subtitle URL from vtt param: {subtitle_url}")

                    r2 = requests.get(iframe_url, headers={"Referer": url}, timeout=10)
                    print(f"[DEBUG] iframe 응답 크기: {len(r2.text)} bytes")

                    # iframe HTML에서 자막 관련 모든 패턴 찾기
                    print(f"[DEBUG] iframe HTML에서 'srt' 또는 'subtitle' 포함하는 부분 찾기...")
                    srt_lines = [line.strip() for line in r2.text.split('\n') if ('srt' in line.lower() or 'subtitle' in line.lower()) and not line.strip().startswith('//')]
                    # CSS나 주석이 아닌 라인만 필터링
                    meaningful_lines = [line for line in srt_lines if not any(x in line.lower() for x in ['#customsubtitle', '/*', '*/', 'netflix-style'])]
                    for i, line in enumerate(meaningful_lines[:10]):  # 처음 10개만 출력
                        print(f"[DEBUG] iframe line {i+1}: {line[:300]}")

                    # SUB_URL 변수에서 직접 추출
                    sub_url_match = re.search(r'const\s+SUB_URL\s*=\s*["\']([^"\']+)["\']', r2.text)
                    if sub_url_match:
                        extracted_sub_url = sub_url_match.group(1)
                        print(f"[DEBUG] SUB_URL 변수에서 추출한 자막 URL: {extracted_sub_url}")
                        # 이 URL을 직접 사용해보기
                        if extracted_sub_url.startswith('http'):
                            subtitle_url = extracted_sub_url
                            print(f"[DEBUG] SUB_URL 변수의 자막 URL로 업데이트: {subtitle_url}")

                    # iframe 내에서 자막 URL이 다시 정의되는지 확인
                    iframe_subtitle_patterns = [
                        r'subtitle["\s:=]+["\']?([^"\'<>\s]+\.(?:srt|vtt)(?:\?[^"\'<>\s]*)?)["\']?',
                        r'["\']?(https?://[^\s"\'<>]+\.(?:srt|vtt)(?:\?[^\s"\'<>]*)?)["\']?',
                    ]
                    for sp in iframe_subtitle_patterns:
                        iframe_sub_match = re.search(sp, r2.text, re.IGNORECASE)
                        if iframe_sub_match:
                            potential_sub = iframe_sub_match.group(1).strip()
                            if 'thumbnail' not in potential_sub.lower():
                                print(f"[DEBUG] iframe 내 자막 URL 후보: {potential_sub}")
                                print(f"[DEBUG] potential_sub type: {type(potential_sub)}, starts with http: {potential_sub.startswith('http')}")
                                # iframe 내부에서 찾은 완전한 URL이 있으면 이걸 사용
                                if potential_sub.startswith('http'):
                                    subtitle_url = potential_sub
                                    print(f"[DEBUG] ✅ iframe 내부의 자막 URL로 업데이트: {subtitle_url}")
                                    break

                    # 방법 1: 기존 hls_url 패턴
                    m2 = re.search(r'var\s+hls_url\s*=\s*my\(["\']([0-9a-fA-F]+)["\']\)', r2.text)
                    if m2:
                        print(f"[DEBUG] hls_url 패턴 발견: {m2.group(1)[:50]}...")
                        decrypted_url = decrypt_bunny_frame_url(m2.group(1))
                        if decrypted_url:
                            actual_url = decrypted_url
                            direct_download_url = decrypted_url
                            ydl_headers = {'Referer': 'https://player.bunny-frame.online/', 'User-Agent': 'Mozilla/5.0'}
                            print(f"[DEBUG] ✅ 복호화된 URL: {decrypted_url}")

                    # 방법 2: ENC_URL 패턴 (새로운 bunny-frame 방식)
                    if not direct_download_url:
                        m2 = re.search(r'const\s+ENC_URL\s*=\s*["\']([0-9a-fA-F]+)["\']', r2.text)
                        if m2:
                            print(f"[DEBUG] ENC_URL 패턴 발견: {m2.group(1)[:50]}...")
                            decrypted_url = decrypt_bunny_frame_url(m2.group(1))
                            if decrypted_url:
                                print(f"[DEBUG] ✅ 복호화된 URL: {decrypted_url}")

                                # 복호화된 URL이 c.html 페이지인 경우 (poorcdn 등), 실제 m3u8 URL 추출 필요
                                if '/c.html' in decrypted_url or 'poorcdn' in decrypted_url:
                                    print(f"[DEBUG] c.html 페이지 - 2차 추출 시도...")
                                    try:
                                        mobile_headers = {
                                            'User-Agent': 'Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1',
                                            'Referer': 'https://player.bunny-frame.online/',
                                            'Origin': 'https://player.bunny-frame.online',
                                            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
                                            'Accept-Language': 'ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7',
                                        }
                                        r3 = requests.get(decrypted_url, headers=mobile_headers, timeout=15)
                                        print(f"[DEBUG] c.html 응답: status={r3.status_code}, size={len(r3.text)}")

                                        if r3.status_code == 200:
                                            # c.html 자체가 m3u8 콘텐츠인지 확인
                                            if r3.text.strip().startswith('#EXTM3U'):
                                                # c.html URL 자체가 m3u8 플레이리스트!
                                                actual_url = decrypted_url
                                                direct_download_url = decrypted_url
                                                print(f"[DEBUG] ✅ c.html 자체가 m3u8 플레이리스트!")
                                                print(f"[DEBUG] ✅ actual_url 설정됨: {actual_url}")
                                            else:
                                                # m3u8 URL 추출
                                                m3u8_match = re.search(r'(https?://[^\s\'"<>]+\.m3u8[^\s\'"<>]*)', r3.text)
                                                if m3u8_match:
                                                    actual_url = m3u8_match.group(1)
                                                    direct_download_url = actual_url
                                                    print(f"[DEBUG] ✅ c.html에서 m3u8 추출: {actual_url}")
                                                else:
                                                    # 다른 암호화된 URL이 있을 수 있음
                                                    enc_match2 = re.search(r'["\']([0-9a-fA-F]{64,})["\']', r3.text)
                                                    if enc_match2:
                                                        decrypted_url2 = decrypt_bunny_frame_url(enc_match2.group(1))
                                                        if decrypted_url2 and ('m3u8' in decrypted_url2 or 'mp4' in decrypted_url2):
                                                            actual_url = decrypted_url2
                                                            direct_download_url = decrypted_url2
                                                            print(f"[DEBUG] ✅ c.html 2차 복호화: {decrypted_url2}")
                                                    else:
                                                        # 못 찾으면 원본 URL이라도 사용
                                                        actual_url = decrypted_url
                                                        direct_download_url = decrypted_url
                                                        print(f"[DEBUG] c.html 내부 URL 미발견 - 원본 URL 사용: {actual_url}")
                                        else:
                                            print(f"[DEBUG] c.html 접근 실패 (status={r3.status_code}) - 원본 URL 사용")
                                            actual_url = decrypted_url
                                            direct_download_url = decrypted_url
                                    except Exception as e:
                                        print(f"[DEBUG] c.html 2차 추출 실패: {e} - 원본 URL 사용")
                                        actual_url = decrypted_url
                                        direct_download_url = decrypted_url
                                else:
                                    actual_url = decrypted_url
                                    direct_download_url = decrypted_url

                                ydl_headers = {'Referer': 'https://player.bunny-frame.online/', 'User-Agent': 'Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15'}

                    # 방법 3: 직접 m3u8/mp4 URL
                    if not direct_download_url:
                        m3 = re.search(r'(https?://[^\s\'"<>]+\.(?:m3u8|mp4)[^\s\'"<>]*)', r2.text)
                        if m3:
                            actual_url = m3.group(1)
                            direct_download_url = m3.group(1)
                            ydl_headers = {'Referer': 'https://player.bunny-frame.online/', 'User-Agent': 'Mozilla/5.0'}
                            print(f"[DEBUG] 직접 URL 발견: {actual_url}")

                    if not subtitle_url:
                        vtt_in_page = re.search(r'["\']?(https?://[^\s"\'<>]+\.vtt(?:\?[^\s"\'<>]*)?)["\']?', r2.text)
                        if vtt_in_page:
                            subtitle_url = vtt_in_page.group(1)
                            print(f"[DEBUG] Subtitle URL from page: {subtitle_url}")

                    # ========== 자막 먼저 다운로드 (영상 다운로드 전에) ==========
                    if subtitle_url and extracted_title:
                        try:
                            print(f"[SUBTITLE-FIRST] 자막 우선 다운로드 시도: {subtitle_url}")
                            safe_title = re.sub(r'[<>:"/\\|?*]', '', extracted_title).strip()
                            sub_ext = '.srt' if '.srt' in subtitle_url else '.vtt'
                            sub_filename = f"{safe_title}.ko{sub_ext}"
                            sub_path = os.path.join(upload_folder, sub_filename)

                            # 여러 가능한 URL 패턴 시도
                            subtitle_url_variants = [subtitle_url]

                            # tvwiki URL인 경우 bunny-frame URL도 시도
                            if 'tvwiki' in subtitle_url:
                                # /movie/5419/data/... -> /data/...로 변경
                                alt_url = subtitle_url.replace('/movie/5419/data/', '/data/')
                                subtitle_url_variants.append(alt_url)
                                # bunny-frame 호스트로 변경
                                bunny_url = subtitle_url.replace('https://tvwiki4.net/movie/5419/', 'https://player.bunny-frame.online/')
                                subtitle_url_variants.append(bunny_url)

                            print(f"[SUBTITLE-FIRST] 시도할 URL 목록: {subtitle_url_variants}")

                            sub_response = None
                            successful_url = None

                            for attempt_url in subtitle_url_variants:
                                # 자막 URL의 호스트에 맞는 Referer 설정
                                if 'tvwiki' in attempt_url:
                                    sub_headers = {
                                        'Referer': iframe_url,
                                        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
                                        'Accept': '*/*',
                                        'Accept-Language': 'ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7',
                                        'Sec-Fetch-Dest': 'empty',
                                        'Sec-Fetch-Mode': 'cors',
                                        'Sec-Fetch-Site': 'cross-site'
                                    }
                                else:
                                    sub_headers = {'Referer': iframe_url, 'User-Agent': 'Mozilla/5.0'}

                                print(f"[SUBTITLE-FIRST] 시도 URL: {attempt_url}")
                                try:
                                    sub_response = requests.get(attempt_url, headers=sub_headers, timeout=10)
                                    print(f"[SUBTITLE-FIRST] 응답: status={sub_response.status_code}, size={len(sub_response.content)}")

                                    if sub_response.status_code == 200 and len(sub_response.content) > 10:
                                        successful_url = attempt_url
                                        print(f"[SUBTITLE-FIRST] ✅ 성공한 URL: {successful_url}")
                                        break
                                except Exception as e:
                                    print(f"[SUBTITLE-FIRST] URL 시도 실패: {attempt_url} - {e}")
                                    continue

                            if not successful_url:
                                print(f"[SUBTITLE-FIRST] 모든 URL 시도 실패")
                            print(f"[SUBTITLE-FIRST] 응답: status={sub_response.status_code}, size={len(sub_response.content)}, content-type={sub_response.headers.get('content-type')}")

                            if sub_response.status_code == 200 and len(sub_response.content) > 10:
                                with open(sub_path, 'wb') as f:
                                    f.write(sub_response.content)
                                vdown_tasks[task_id]['subtitle_files'] = [{
                                    'filename': sub_filename,
                                    'lang_code': 'ko',
                                    'lang_name': '한국어',
                                    'format': sub_ext[1:].upper()
                                }]
                                print(f"[SUBTITLE-FIRST] ✅ 자막 다운로드 성공: {sub_filename} ({len(sub_response.content)} bytes)")
                                # 자막 내용 일부 미리보기 (첫 200자)
                                try:
                                    preview = sub_response.content[:200].decode('utf-8', errors='ignore')
                                    print(f"[SUBTITLE-FIRST] 자막 미리보기: {preview[:100]}...")
                                except:
                                    pass
                            else:
                                print(f"[SUBTITLE-FIRST] ❌ 자막 다운로드 실패: status={sub_response.status_code}, size={len(sub_response.content)}")
                                if sub_response.status_code != 200:
                                    print(f"[SUBTITLE-FIRST] 응답 본문 (처음 500자): {sub_response.text[:500]}")
                                # 자막 다운로드 실패 시 - subtitle_url을 None으로 설정하고 계속 진행
                                # (사용자가 "자막 병합" 옵션을 선택했지만 자막이 없는 경우, 영상만 다운로드됨)
                                print(f"[SUBTITLE-FIRST] ⚠️ 자막 파일을 찾을 수 없어 영상만 다운로드합니다")
                                subtitle_url = None
                                vdown_tasks[task_id]['subtitle_files'] = []
                        except Exception as sub_error:
                            print(f"[SUBTITLE-FIRST] ❌ 자막 다운로드 오류: {sub_error}")
                            import traceback
                            traceback.print_exc()
                            # 자막 다운로드 오류 시 - 자막 없이 계속 진행
                            print(f"[SUBTITLE-FIRST] ⚠️ 자막 다운로드 실패, 영상만 다운로드합니다")
                            subtitle_url = None
                            vdown_tasks[task_id]['subtitle_files'] = []

            except Exception as e:
                print(f"TVWiki extraction error: {e}")

        # --- Dailymotion Logic (yt-dlp 버그 우회) ---
        elif 'dailymotion.com' in url or 'dai.ly' in url:
            try:
                print(f"[DAILYMOTION] ========== 직접 추출 시작 ==========")
                # video ID 추출
                dm_match = re.search(r'(?:dailymotion\.com/video/|dai\.ly/)([a-zA-Z0-9]+)', url)
                if dm_match:
                    video_id = dm_match.group(1)
                    print(f"[DAILYMOTION] Video ID: {video_id}")

                    dm_headers = {
                        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36',
                        'Accept': '*/*',
                        'Accept-Language': 'en-US,en;q=0.9,ko;q=0.8',
                        'Referer': 'https://www.dailymotion.com/',
                        'Origin': 'https://www.dailymotion.com',
                    }

                    # 방법 1: embed 페이지에서 config 추출
                    print(f"[DAILYMOTION] 방법 1: embed 페이지 분석...")
                    embed_url = f'https://www.dailymotion.com/embed/video/{video_id}'
                    embed_response = requests.get(embed_url, headers=dm_headers, timeout=15)
                    print(f"[DAILYMOTION] embed 응답: {embed_response.status_code}, size={len(embed_response.text)}")

                    if embed_response.status_code == 200:
                        embed_html = embed_response.text

                        # 제목 추출
                        title_match = re.search(r'"title"\s*:\s*"([^"]+)"', embed_html)
                        if title_match:
                            extracted_title = title_match.group(1).encode().decode('unicode_escape')
                            print(f"[DAILYMOTION] 제목: {extracted_title}")

                        # __PLAYER_CONFIG__ 또는 config 객체에서 URL 찾기
                        # HLS manifest URL 패턴들
                        hls_patterns = [
                            r'"url"\s*:\s*"(https?://[^"]+\.m3u8[^"]*)"',
                            r'"hls"\s*:\s*"(https?://[^"]+)"',
                            r'"stream_url"\s*:\s*"(https?://[^"]+\.m3u8[^"]*)"',
                            r'(https?://www\.dailymotion\.com/cdn/manifest/video/[^"\'\\]+)',
                        ]

                        for pattern in hls_patterns:
                            m = re.search(pattern, embed_html)
                            if m:
                                found_url = m.group(1).replace('\\/', '/').replace('\\u0026', '&')
                                print(f"[DAILYMOTION] HLS URL 후보: {found_url[:100]}...")
                                if 'm3u8' in found_url or 'manifest' in found_url:
                                    actual_url = found_url
                                    direct_download_url = found_url
                                    ydl_headers = dm_headers
                                    print(f"[DAILYMOTION] ✅ HLS URL 발견!")
                                    break

                        # MP4 직접 URL 찾기
                        if not direct_download_url:
                            mp4_patterns = [
                                r'"(https?://[^"]+/video/[^"]+\.mp4[^"]*)"',
                                r'"url"\s*:\s*"(https?://[^"]+\.mp4[^"]*)"',
                            ]
                            for pattern in mp4_patterns:
                                m = re.search(pattern, embed_html)
                                if m:
                                    found_url = m.group(1).replace('\\/', '/').replace('\\u0026', '&')
                                    if '.mp4' in found_url and 'sprite' not in found_url:
                                        actual_url = found_url
                                        direct_download_url = found_url
                                        ydl_headers = dm_headers
                                        print(f"[DAILYMOTION] ✅ MP4 URL 발견: {found_url[:100]}...")
                                        break

                    # 방법 2: Player Metadata API 시도
                    if not direct_download_url:
                        print(f"[DAILYMOTION] 방법 2: Player API 시도...")
                        api_url = f'https://www.dailymotion.com/player/metadata/video/{video_id}'
                        try:
                            api_response = requests.get(api_url, headers=dm_headers, timeout=15)
                            print(f"[DAILYMOTION] API 응답: {api_response.status_code}")

                            if api_response.status_code == 200:
                                api_data = api_response.json()
                                if not extracted_title:
                                    extracted_title = api_data.get('title', 'Dailymotion Video')

                                # qualities에서 URL 찾기
                                qualities = api_data.get('qualities', {})
                                print(f"[DAILYMOTION] qualities 키: {list(qualities.keys())}")

                                # auto 품질 먼저 시도
                                if 'auto' in qualities:
                                    for q in qualities['auto']:
                                        q_url = q.get('url', '')
                                        q_type = q.get('type', '')
                                        print(f"[DAILYMOTION] auto 품질 - type: {q_type}, url: {q_url[:80] if q_url else 'N/A'}...")
                                        if 'm3u8' in q_type or 'mpegURL' in q_type or 'm3u8' in q_url:
                                            actual_url = q_url
                                            direct_download_url = q_url
                                            ydl_headers = dm_headers
                                            print(f"[DAILYMOTION] ✅ API에서 HLS URL 발견!")
                                            break

                                # 다른 품질 시도
                                if not direct_download_url:
                                    for quality in ['1080', '720', '480', '380', '240']:
                                        if quality in qualities:
                                            for stream in qualities[quality]:
                                                stream_url = stream.get('url', '')
                                                if stream_url:
                                                    actual_url = stream_url
                                                    direct_download_url = stream_url
                                                    ydl_headers = dm_headers
                                                    print(f"[DAILYMOTION] ✅ API에서 {quality}p URL 발견!")
                                                    break
                                            if direct_download_url:
                                                break
                        except Exception as api_err:
                            print(f"[DAILYMOTION] API 호출 실패: {api_err}")

                    # 방법 3: 직접 비디오 페이지 파싱
                    if not direct_download_url:
                        print(f"[DAILYMOTION] 방법 3: 직접 페이지 분석...")
                        try:
                            video_url = f'https://www.dailymotion.com/video/{video_id}'
                            page_response = requests.get(video_url, headers=dm_headers, timeout=15)
                            if page_response.status_code == 200:
                                page_html = page_response.text

                                # __PLAYER_CONFIG__ JSON 찾기
                                config_match = re.search(r'var\s+__PLAYER_CONFIG__\s*=\s*(\{.+?\});', page_html, re.DOTALL)
                                if config_match:
                                    print(f"[DAILYMOTION] __PLAYER_CONFIG__ 발견")
                                    import json
                                    try:
                                        config = json.loads(config_match.group(1))
                                        metadata = config.get('metadata', {})
                                        qualities = metadata.get('qualities', {})
                                        if qualities:
                                            for q in qualities.get('auto', []):
                                                if 'm3u8' in q.get('type', ''):
                                                    actual_url = q.get('url')
                                                    direct_download_url = actual_url
                                                    ydl_headers = dm_headers
                                                    print(f"[DAILYMOTION] ✅ Config에서 URL 발견!")
                                                    break
                                    except json.JSONDecodeError:
                                        pass
                        except Exception as page_err:
                            print(f"[DAILYMOTION] 페이지 분석 실패: {page_err}")

                    if direct_download_url:
                        print(f"[DAILYMOTION] ========== 추출 성공 ==========")
                        print(f"[DAILYMOTION] actual_url: {actual_url[:100]}...")
                    else:
                        print(f"[DAILYMOTION] ========== 추출 실패 - yt-dlp로 폴백 ==========")

            except Exception as e:
                print(f"[DAILYMOTION] 추출 오류: {e}")
                import traceback
                traceback.print_exc()

        # --- Nooo (Noonoo) Logic ---
        elif 'nooo' in url or 'noonoo' in url:
            try:
                r = requests.get(url, headers={"User-Agent": "Mozilla/5.0"}, timeout=10)

                try:
                    m_title = re.search(r'<meta\s+property=["\']og:title["\']\s+content=["\']([^"\"]+)["\"]', r.text)
                    if m_title:
                        extracted_title = m_title.group(1).replace(" - 누누티비", "").replace(" - nooo", "").strip()
                    else:
                        m_title = re.search(r'<title>(.*?)</title>', r.text)
                        if m_title:
                            extracted_title = m_title.group(1).replace(" - 누누티비", "").replace(" - nooo", "").strip()
                except: pass

                m_iframe = re.search(r'<iframe[^>]+src=(["\"])([^"\"]+)\1', r.text)
                if m_iframe:
                    iframe_url = m_iframe.group(2)
                    r2 = requests.get(iframe_url, headers={"Referer": url, "User-Agent": "Mozilla/5.0"}, timeout=10)

                    m_mp4 = re.search(r'var mp4_url\s*=\s*["\"]([^"\"]+)["\"]', r2.text)
                    if m_mp4:
                        actual_url = m_mp4.group(1)
                        direct_download_url = m_mp4.group(1)
                        iframe_host = re.match(r'(https?://[^/]+)', iframe_url).group(1)
                        ydl_headers = {'Referer': iframe_host + '/', 'User-Agent': 'Mozilla/5.0'}
            except Exception as e:
                print(f"Nooo extraction error: {e}")

        # 자막 옵션 확인
        subtitle_option = vdown_tasks[task_id].get('subtitle_option', 'none')

        # Dailymotion, Vimeo 등 추가 사이트 지원을 위한 강화된 옵션
        is_dailymotion = 'dailymotion.com' in url or 'dai.ly' in url
        is_vimeo = 'vimeo.com' in url

        ydl_opts = {
            'outtmpl': os.path.join(upload_folder, f"{task_id}.%(ext)s"),
            'quiet': True,
            'no_warnings': True,
            'noprogress': True,
            'progress_hooks': [progress_hook],
            'encoding': 'utf-8',
            'http_headers': ydl_headers,
            'noplaylist': True,  # 플레이리스트가 아닌 단일 영상만 다운로드
            'socket_timeout': 30,
            'retries': 5,
            'fragment_retries': 5,
        }

        # YouTube 전용 옵션 (403 에러 우회)
        is_youtube = 'youtube.com' in url or 'youtu.be' in url
        if is_youtube:
            print(f"[VDOWN] YouTube 감지됨 - 403 우회 옵션 적용")
            ydl_opts.update({
                'http_headers': {
                    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36',
                    'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
                    'Accept-Language': 'en-US,en;q=0.9',
                    'Accept-Encoding': 'gzip, deflate',
                },
                # YouTube 403 우회를 위한 핵심 옵션
                'extractor_args': {
                    'youtube': {
                        'player_client': ['web', 'android', 'ios'],  # 여러 클라이언트 시도
                        'player_skip': ['webpage', 'configs'],  # 불필요한 요청 스킵
                    }
                },
                'sleep_interval_requests': 1,  # 요청 간 딜레이
            })

        # Dailymotion 전용 옵션
        if is_dailymotion:
            print(f"[VDOWN] Dailymotion 감지됨 - 전용 옵션 적용")

            # 직접 추출에서 URL을 찾지 못한 경우에만 embed URL로 폴백
            if not direct_download_url:
                dm_match = re.search(r'dailymotion\.com/video/([a-zA-Z0-9]+)', url)
                if dm_match:
                    video_id = dm_match.group(1)
                    actual_url = f'https://www.dailymotion.com/embed/video/{video_id}'
                    print(f"[VDOWN] 직접 추출 실패 - embed URL로 폴백: {actual_url}")
            else:
                print(f"[VDOWN] 직접 추출 URL 사용: {actual_url[:80]}...")

            ydl_opts.update({
                'http_headers': {
                    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36',
                    'Accept': '*/*',
                    'Accept-Language': 'en-US,en;q=0.9',
                    'Referer': 'https://www.dailymotion.com/',
                    'Origin': 'https://www.dailymotion.com',
                },
                'format': 'best[ext=mp4]/best',
                'extractor_args': {'dailymotion': {'embed': ['1']}},
            })

        # 자막 다운로드는 'separate' 옵션일 때만 + TVWiki/TVMon이 아닐 때만
        is_tvwiki = 'tvwiki' in url or 'tvmon' in url
        if subtitle_option == 'separate' and not is_tvwiki:
            ydl_opts.update({
                'writesubtitles': True,
                'writeautomaticsub': True,
                'subtitleslangs': ['ko', 'en', 'ja'],
                'subtitlesformat': 'srt/vtt/best',
            })

        # curl_cffi 설정 (YouTube, Dailymotion 등 특수 설정이 있는 경우 덮어쓰지 않음)
        if HAS_CURL_CFFI and not is_youtube and not is_dailymotion:
            ydl_opts['extractor_args'] = {'generic': {'impersonate': ['chrome']}}

        if format_type == 'audio':
            ydl_opts.update({'format': 'bestaudio/best', 'postprocessors': [{'key': 'FFmpegExtractAudio', 'preferredcodec': 'mp3', 'preferredquality': '320'}]})
        else:
            ydl_opts.update({'format': 'bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best'})

        if extracted_title:
            vdown_tasks[task_id]['title'] = extracted_title

        download_success = False
        final_path = None
        subtitle_files = []

        # Method 1: Try yt-dlp first
        print(f"[VDOWN] ===== 최종 URL 확인 =====")
        print(f"[VDOWN] actual_url: {actual_url}")
        print(f"[VDOWN] direct_download_url: {direct_download_url}")
        print(f"[VDOWN] yt-dlp 시도 중: {actual_url}")
        try:
            with yt_dlp.YoutubeDL(ydl_opts) as ydl:
                if cancel_flags.get(task_id): raise Exception("Cancelled by user")

                if not extracted_title:
                    try:
                        print(f"[VDOWN] 메타데이터 추출 중...")
                        meta = ydl.extract_info(actual_url, download=False)
                        vdown_tasks[task_id]['title'] = meta.get('title', 'Media')
                        print(f"[VDOWN] 제목: {meta.get('title', 'Media')}")
                    except Exception as e:
                        print(f"[VDOWN] 메타데이터 추출 실패: {e}")

                if cancel_flags.get(task_id): raise Exception("Cancelled by user")

                print(f"[VDOWN] 다운로드 실행 중...")
                info = ydl.extract_info(actual_url, download=True)
                print(f"[VDOWN] 다운로드 완료, info: {info is not None}")

                if info is None:
                    raise Exception("yt-dlp returned None - download may have failed")

                if cancel_flags.get(task_id): raise Exception("Cancelled by user")

                if extracted_title:
                    current_title = extracted_title
                else:
                    current_title = info.get('title', 'Media')

                generic_patterns = [r'^c$', r'^video$', r'^index$', r'^master$', r'^playlist$', r'^stream$', r'^manifest$', r'^videoplayback$', r'^output$', r'^[a-zA-Z0-9]{1,4}$']
                is_generic = any(re.match(p, current_title, re.IGNORECASE) for p in generic_patterns)

                if is_generic and not extracted_title:
                    try:
                        page_r = requests.get(url, headers={"User-Agent": "Mozilla/5.0"}, timeout=5)
                        m_og = re.search(r'<meta\s+property=["\']og:title["\']\s+content=["\']([^"\"]+)["\"]', page_r.text)
                        if m_og:
                            current_title = m_og.group(1).strip()
                        else:
                            m_tag = re.search(r'<title>(.*?)</title>', page_r.text)
                            if m_tag:
                                current_title = m_tag.group(1).strip()
                        current_title = re.sub(r'\s*[-|]\s*.*$', '', current_title)
                    except:
                        pass

                vdown_tasks[task_id]['title'] = current_title

                final_path = ydl.prepare_filename(info)
                if format_type == 'audio': final_path = os.path.splitext(final_path)[0] + ".mp3"

                ext = os.path.splitext(final_path)[1]
                safe_title = re.sub(r'[<>:"/\\|?*]', '', current_title).strip()
                new_path, new_filename = get_safe_filename(current_title, ext, upload_folder)
                if os.path.exists(final_path):
                    os.rename(final_path, new_path)
                    final_path = new_path
                vdown_tasks[task_id]['server_file'] = new_filename

                # 자막 파일 이름 변경 및 정보 저장
                subtitle_files = rename_subtitle_files(upload_folder, task_id, safe_title)
                if subtitle_files:
                    vdown_tasks[task_id]['subtitle_files'] = subtitle_files
                    print(f"[DEBUG] Subtitles renamed: {subtitle_files}")

                # TVWiki 등에서 추출한 subtitle_url이 있으면 무조건 직접 다운로드 시도
                if subtitle_url and not subtitle_files:
                    try:
                        print(f"[DEBUG] yt-dlp 자막 없음, 직접 다운로드 시도: {subtitle_url}...")
                        sub_ext = '.srt' if '.srt' in subtitle_url else '.vtt'
                        sub_filename = f"{safe_title}.ko{sub_ext}"
                        sub_path = os.path.join(upload_folder, sub_filename)
                        print(f"[DEBUG] 자막 다운로드 요청: URL={subtitle_url}, Headers={ydl_headers}")
                        sub_response = requests.get(subtitle_url, headers=ydl_headers or {"User-Agent": "Mozilla/5.0"}, timeout=10)
                        print(f"[DEBUG] 자막 응답: status={sub_response.status_code}, size={len(sub_response.content)}, content_type={sub_response.headers.get('content-type')}")
                        if sub_response.status_code == 200 and len(sub_response.content) > 10:
                            with open(sub_path, 'wb') as f:
                                f.write(sub_response.content)
                            vdown_tasks[task_id]['subtitle_files'] = [{
                                'filename': sub_filename,
                                'lang_code': 'ko',
                                'lang_name': '한국어',
                                'format': sub_ext[1:].upper()
                            }]
                            print(f"[DEBUG] 자막 직접 다운로드 성공: {sub_filename}")
                        else:
                            print(f"[DEBUG] 자막 응답 실패: status={sub_response.status_code}, size={len(sub_response.content)}")
                    except Exception as sub_error:
                        print(f"[DEBUG] 자막 직접 다운로드 실패: {sub_error}")

                download_success = True

        except Exception as ydl_error:
            print(f"[FALLBACK] yt-dlp 실패: {ydl_error}")

            if direct_download_url and format_type == 'video':
                print(f"[FALLBACK] 직접 다운로드 시도: {direct_download_url[:80]}...")
                current_title = extracted_title or 'Video'
                vdown_tasks[task_id]['title'] = current_title
                output_file = os.path.join(upload_folder, f"{task_id}.mp4")

                if direct_download_with_progress(direct_download_url, output_file, task_id, ydl_headers):
                    safe_title = re.sub(r'[<>:"/\\|?*]', '', current_title).strip()
                    new_path, new_filename = get_safe_filename(current_title, ".mp4", upload_folder)
                    os.rename(output_file, new_path)
                    final_path = new_path
                    vdown_tasks[task_id]['server_file'] = new_filename
                    download_success = True
                    print(f"[FALLBACK] 직접 다운로드 성공: {new_filename}")

                    # 자막도 다운로드 시도
                    if subtitle_url:
                        try:
                            print(f"[FALLBACK] 자막 직접 다운로드 시도: {subtitle_url[:80]}...")
                            sub_ext = '.srt' if '.srt' in subtitle_url else '.vtt'
                            sub_filename = f"{safe_title}.ko{sub_ext}"
                            sub_path = os.path.join(upload_folder, sub_filename)
                            sub_response = requests.get(subtitle_url, headers=ydl_headers, timeout=10)
                            if sub_response.status_code == 200 and len(sub_response.content) > 10:
                                with open(sub_path, 'wb') as f:
                                    f.write(sub_response.content)
                                vdown_tasks[task_id]['subtitle_files'] = [{
                                    'filename': sub_filename,
                                    'lang_code': 'ko',
                                    'lang_name': '한국어',
                                    'format': sub_ext[1:].upper()
                                }]
                                print(f"[FALLBACK] 자막 다운로드 성공: {sub_filename}")
                            else:
                                print(f"[FALLBACK] 자막 응답 실패: status={sub_response.status_code}, size={len(sub_response.content)}")
                        except Exception as sub_error:
                            print(f"[FALLBACK] 자막 다운로드 실패: {sub_error}")

            if not download_success:
                print(f"[FALLBACK] 자동 패턴 탐지 시작...")
                auto_result = auto_extract_video_url(url)

                for debug_line in auto_result['debug_info']:
                    print(debug_line)

                if auto_result.get('is_cloudflare'):
                    raise CloudflareProtectedError(
                        "이 사이트는 Cloudflare 보호가 적용되어 있어 자동 다운로드가 불가능합니다. "
                        "브라우저에서 직접 접속하여 다운로드해 주세요."
                    )

                if auto_result['video_url']:
                    auto_url = auto_result['video_url']
                    auto_headers = auto_result['headers']
                    current_title = auto_result['title'] or extracted_title or 'Video'
                    vdown_tasks[task_id]['title'] = current_title

                    print(f"[FALLBACK] 자동 탐지 URL로 yt-dlp 재시도: {auto_url[:80]}...")
                    ydl_opts['http_headers'] = auto_headers
                    try:
                        with yt_dlp.YoutubeDL(ydl_opts) as ydl:
                            info = ydl.extract_info(auto_url, download=True)
                            final_path = ydl.prepare_filename(info)
                            if format_type == 'audio':
                                final_path = os.path.splitext(final_path)[0] + ".mp3"
                            ext = os.path.splitext(final_path)[1]
                            safe_title = re.sub(r'[<>:"/\\|?*]', '', current_title).strip()
                            new_path, new_filename = get_safe_filename(current_title, ext, upload_folder)
                            if os.path.exists(final_path):
                                os.rename(final_path, new_path)
                            vdown_tasks[task_id]['server_file'] = new_filename

                            subtitle_files = rename_subtitle_files(upload_folder, task_id, safe_title)
                            if subtitle_files:
                                vdown_tasks[task_id]['subtitle_files'] = subtitle_files

                            download_success = True
                            print(f"[FALLBACK] 자동 탐지 yt-dlp 성공: {new_filename}")
                    except Exception as auto_ydl_error:
                        print(f"[FALLBACK] 자동 탐지 yt-dlp 실패: {auto_ydl_error}")

                        if '.mp4' in auto_url or '.webm' in auto_url:
                            output_file = os.path.join(upload_folder, f"{task_id}.mp4")
                            if direct_download_with_progress(auto_url, output_file, task_id, auto_headers):
                                safe_title = re.sub(r'[<>:"/\\|?*]', '', current_title).strip()
                                new_path, new_filename = get_safe_filename(current_title, ".mp4", upload_folder)
                                os.rename(output_file, new_path)
                                vdown_tasks[task_id]['server_file'] = new_filename
                                download_success = True
                                print(f"[FALLBACK] 자동 탐지 직접 다운로드 성공: {new_filename}")

            if not download_success:
                raise Exception(f"모든 방법 실패. 원본 에러: {ydl_error}")

        if download_success:
            vdown_tasks[task_id]['status'] = 'completed'
        else:
            raise Exception("Download failed with all methods")

    except CloudflareProtectedError as cf_error:
        vdown_tasks[task_id]['status'] = 'error'
        vdown_tasks[task_id]['message'] = str(cf_error)
    except Exception as e:
        if cancel_flags.get(task_id):
            vdown_tasks[task_id]['status'] = 'cancelled'
            vdown_tasks[task_id]['message'] = 'Cancelled by user'
        else:
            vdown_tasks[task_id]['status'] = 'error'
            vdown_tasks[task_id]['message'] = get_user_friendly_error(str(e), url)
    finally:
        if task_id in cancel_flags:
            del cancel_flags[task_id]


# ===== Routes =====

@vdown_bp.route('/', methods=['GET', 'POST'])
def vdown_index():
    if request.method == 'GET': return render_template('vdown.html')
    data = request.get_json(silent=True) or request.form
    task_id = str(uuid.uuid4())

    # 자막 옵션 저장
    subtitle_option = data.get('subtitle_option', 'none')  # 'none' or 'separate'

    vdown_tasks[task_id] = {
        'status': 'pending',
        'title': 'Fetching...',
        'progress': {'percent': 0, 'speed': '0B/s'},
        'subtitle_option': subtitle_option
    }
    url = data.get('url')
    format_type = data.get('format', 'video')
    print(f"[VDOWN-ROUTE] 새 다운로드 요청: task_id={task_id}, url={url}, format={format_type}, subtitle={subtitle_option}")
    threading.Thread(target=process_vdown_task, args=(task_id, url, UPLOAD_FOLDER, format_type), daemon=True).start()
    return jsonify({'status': 'started', 'task_id': task_id})


@vdown_bp.route('/status/<task_id>')
def vdown_status(task_id):
    task = vdown_tasks.get(task_id, {'status': 'missing'})
    return jsonify(task)


@vdown_bp.route('/check-url', methods=['POST'])
def vdown_check_url():
    """URL에서 영상 정보와 자막 가용성을 먼저 확인"""
    data = request.get_json(silent=True) or request.form
    url = data.get('url', '')

    result = {
        'has_subtitle': False,
        'subtitle_url': None,
        'title': None
    }

    try:
        # YouTube / 일반 사이트는 자막 선택 모달 표시 안 함 (바로 다운로드)
        if 'youtube.com' in url or 'youtu.be' in url:
            # YouTube는 자막 모달 없이 바로 다운로드
            return jsonify(result)

        # TVWiki / TVMon
        if 'tvwiki' in url or 'tvmon' in url:
            r = requests.get(url, headers={"User-Agent": "Mozilla/5.0"}, timeout=10)

            # 제목 추출
            m_title = re.search(r'<meta\s+property=["\']og:title["\']\s+content=["\']([^"\"]+)["\"]', r.text)
            if m_title:
                result['title'] = m_title.group(1).replace(" - 티비위키", "").replace(" - 티비몬", "").strip()
            else:
                m_title = re.search(r'<title>(.*?)</title>', r.text)
                if m_title:
                    result['title'] = m_title.group(1).replace(" - 티비위키", "").replace(" - 티비몬", "").strip()

            # iframe에서 자막 URL 확인
            m = re.search(r'src=(["\'])(https://player.bunny-frame.online/[^"\"]+)\1', r.text)
            if m:
                iframe_url = m.group(2).replace("&amp;", "&").replace("\r", "").replace("\n", "").strip()
                from urllib.parse import unquote

                # SRT 자막 확인
                srt_match = re.search(r'[?&]srt=([^&]+)', iframe_url)
                if srt_match:
                    subtitle_path = unquote(srt_match.group(1))
                    if subtitle_path.startswith('/'):
                        iframe_host_match = re.match(r'(https?://[^/]+)', iframe_url)
                        if iframe_host_match:
                            result['subtitle_url'] = iframe_host_match.group(1) + subtitle_path
                            result['has_subtitle'] = True
                else:
                    # VTT 자막 확인 (thumbnails.vtt는 제외)
                    vtt_match = re.search(r'[?&]vtt=([^&]+)', iframe_url)
                    if vtt_match:
                        vtt_path = unquote(vtt_match.group(1))
                        if 'thumbnails' not in vtt_path.lower():
                            if vtt_path.startswith('/'):
                                iframe_host_match = re.match(r'(https?://[^/]+)', iframe_url)
                                if iframe_host_match:
                                    result['subtitle_url'] = iframe_host_match.group(1) + vtt_path
                                    result['has_subtitle'] = True
        else:
            # 다른 사이트는 yt-dlp로 정보 확인
            ydl_opts = {
                'quiet': True,
                'no_warnings': True,
                'skip_download': True,
            }
            try:
                with yt_dlp.YoutubeDL(ydl_opts) as ydl:
                    info = ydl.extract_info(url, download=False)
                    result['title'] = info.get('title', '')
                    # 자막 정보 확인
                    subtitles = info.get('subtitles', {})
                    auto_subs = info.get('automatic_captions', {})
                    if subtitles or auto_subs:
                        result['has_subtitle'] = True
            except:
                pass

    except Exception as e:
        print(f"[CHECK-URL] Error: {e}")

    return jsonify(result)


@vdown_bp.route('/cancel/<task_id>', methods=['POST'])
def vdown_cancel(task_id):
    if task_id in vdown_tasks:
        cancel_flags[task_id] = True
        return jsonify({'status': 'cancelling'})
    return jsonify({'status': 'not found'}), 404


@vdown_bp.route('/subtitle-options/<task_id>')
def vdown_subtitle_options(task_id):
    """자막 옵션 정보 반환 (자막 있는지, 어떤 언어인지 등)"""
    task = vdown_tasks.get(task_id)
    if not task:
        return jsonify({'error': 'Task not found'}), 404

    subtitle_files = task.get('subtitle_files', [])
    has_subtitles = len(subtitle_files) > 0

    return jsonify({
        'has_subtitles': has_subtitles,
        'subtitles': subtitle_files,
        'title': task.get('title', 'Video'),
        'video_file': task.get('server_file', '')
    })


@vdown_bp.route('/download/<task_id>')
def vdown_download(task_id):
    """비디오 다운로드 (subtitle_option에 따라 처리)"""
    task = vdown_tasks.get(task_id)
    if not task or 'server_file' not in task:
        return "Not Found", 404

    video_path = os.path.join(UPLOAD_FOLDER, task['server_file'])
    if not os.path.exists(video_path):
        return "File Not Found", 404

    safe_title = re.sub(r'[<>:"/\\|?*]', '', task.get('title', 'download'))

    # 영상 파일 다운로드
    ext = os.path.splitext(task['server_file'])[1]
    return send_file(video_path, as_attachment=True, download_name=f"{safe_title}{ext}")


@vdown_bp.route('/download/<task_id>/subtitle/<int:index>')
def vdown_download_subtitle(task_id, index):
    """특정 자막 파일만 다운로드"""
    task = vdown_tasks.get(task_id)
    if not task:
        return "Not Found", 404

    subtitle_files = task.get('subtitle_files', [])
    if index >= len(subtitle_files):
        return "Subtitle Not Found", 404

    sub_info = subtitle_files[index]
    sub_path = os.path.join(UPLOAD_FOLDER, sub_info['filename'])

    if os.path.exists(sub_path):
        return send_file(sub_path, as_attachment=True, download_name=sub_info['filename'])
    return "Subtitle File Not Found", 404


