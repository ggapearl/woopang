import os
import threading
import uuid
import time
import re
import binascii
import sys
from Crypto.Cipher import AES
from Crypto.Util.Padding import unpad
import requests
from flask import Blueprint, request, render_template, jsonify, send_file
import yt_dlp

vdown_bp = Blueprint('vdown', __name__, template_folder='templates', static_folder='static', url_prefix='/vdown')
UPLOAD_FOLDER = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'downloads')
if not os.path.exists(UPLOAD_FOLDER): os.makedirs(UPLOAD_FOLDER, exist_ok=True)

vdown_tasks = {}
cancel_flags = {}

def strip_ansi(text):
    if not text: return ""
    return re.compile(r'(?:\x1B[@-_][0-?]*[ -/]*[@-~])').sub('', text)

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
        direct_download_url = None  # 직접 다운로드용 URL 저장

        # --- TVWiki / TVMon Logic ---
        if 'tvwiki' in url or 'tvmon' in url:
            try:
                r = requests.get(url, headers={"User-Agent": "Mozilla/5.0"}, timeout=10)

                # Title Extraction
                try:
                    m_title = re.search(r'<meta\s+property=["\']og:title["\']\s+content=["\']([^"\"]+)["\"]', r.text)
                    if m_title:
                        extracted_title = m_title.group(1).replace(" - 티비위키", "").replace(" - 티비몬", "").strip()
                    else:
                        m_title = re.search(r'<title>(.*?)</title>', r.text)
                        if m_title:
                            extracted_title = m_title.group(1).replace(" - 티비위키", "").replace(" - 티비몬", "").strip()
                except: pass

                m = re.search(r'src=(["\'])(https://player.bunny-frame.online/[^"\"]+)\1', r.text)
                if m:
                    r2 = requests.get(m.group(2).replace("&amp;", "&"), headers={"Referer": url}, timeout=10)

                    # Method 1: Try new my() function pattern (var hls_url = my("..."))
                    m2 = re.search(r'var\s+hls_url\s*=\s*my\(["\']([0-9a-fA-F]+)["\']\)', r2.text)
                    if m2:
                        decrypted_url = unpad(AES.new(binascii.unhexlify("0123456789abcdef0123456789abcdef"), AES.MODE_CBC, binascii.unhexlify("abcdef9876543210abcdef9876543210")).decrypt(binascii.unhexlify(m2.group(1))), AES.block_size).decode('utf-8')
                        actual_url = decrypted_url
                        direct_download_url = decrypted_url  # 직접 다운로드용으로도 저장
                        ydl_headers = {'Referer': 'https://player.bunny-frame.online/', 'User-Agent': 'Mozilla/5.0'}
                    else:
                        # Method 2: Fallback to old ENC_URL pattern
                        m2 = re.search(r'const ENC_URL\s*=\s*["\"]([0-9a-fA-F]+)["\"]', r2.text)
                        if m2:
                            decrypted_url = unpad(AES.new(binascii.unhexlify("0123456789abcdef0123456789abcdef"), AES.MODE_CBC, binascii.unhexlify("abcdef9876543210abcdef9876543210")).decrypt(binascii.unhexlify(m2.group(1))), AES.block_size).decode('utf-8')
                            actual_url = decrypted_url
                            direct_download_url = decrypted_url  # 직접 다운로드용으로도 저장
                            ydl_headers = {'Referer': 'https://player.bunny-frame.online/', 'User-Agent': 'Mozilla/5.0'}
                        else:
                            # Method 3: Try to find any .m3u8 or .mp4 URL directly in the page
                            m3 = re.search(r'(https?://[^\s\'"<>]+\.(?:m3u8|mp4)[^\s\'"<>]*)', r2.text)
                            if m3:
                                actual_url = m3.group(1)
                                direct_download_url = m3.group(1)
                                ydl_headers = {'Referer': 'https://player.bunny-frame.online/', 'User-Agent': 'Mozilla/5.0'}
            except Exception as e:
                print(f"TVWiki extraction error: {e}")
                pass

        # --- Nooo (Noonoo) Logic ---
        elif 'nooo' in url or 'noonoo' in url:
            try:
                r = requests.get(url, headers={"User-Agent": "Mozilla/5.0"}, timeout=10)
                
                # Title Extraction
                try:
                    m_title = re.search(r'<meta\s+property=["\']og:title["\']\s+content=["\']([^"\"]+)["\"]', r.text)
                    if m_title:
                        extracted_title = m_title.group(1).replace(" - 누누티비", "").replace(" - nooo", "").strip()
                    else:
                        m_title = re.search(r'<title>(.*?)</title>', r.text)
                        if m_title:
                            extracted_title = m_title.group(1).replace(" - 누누티비", "").replace(" - nooo", "").strip()
                except: pass

                # Iframe Extraction (fvideostream)
                m_iframe = re.search(r'<iframe[^>]+src=(["\"])([^"\"]+)\1', r.text)
                if m_iframe:
                    iframe_url = m_iframe.group(2)
                    r2 = requests.get(iframe_url, headers={"Referer": url, "User-Agent": "Mozilla/5.0"}, timeout=10)

                    # Look for mp4_url variable
                    m_mp4 = re.search(r'var mp4_url\s*=\s*["\"]([^"\"]+)["\"]', r2.text)
                    if m_mp4:
                        actual_url = m_mp4.group(1)
                        direct_download_url = m_mp4.group(1)  # 직접 다운로드용으로도 저장
                        # The iframe host is usually fvideostream.com or similar
                        # We extract the base domain for Referer
                        iframe_host = re.match(r'(https?://[^/]+)', iframe_url).group(1)
                        ydl_headers = {'Referer': iframe_host + '/', 'User-Agent': 'Mozilla/5.0'}
            except Exception as e:
                print(f"Nooo extraction error: {e}")
                pass


        ydl_opts = {
            'outtmpl': os.path.join(upload_folder, f"{task_id}.%(ext)s"),
            'quiet': True,        # 로그 억제
            'no_warnings': True,  # 경고 억제
            'noprogress': True,   # 터미널 프로그레스바 억제 (핵심)
            'progress_hooks': [progress_hook],
            'encoding': 'utf-8',
            'http_headers': ydl_headers
        }

        if format_type == 'audio':
            ydl_opts.update({'format': 'bestaudio/best', 'postprocessors': [{'key': 'FFmpegExtractAudio', 'preferredcodec': 'mp3', 'preferredquality': '320'}]})
        else:
            ydl_opts.update({'format': 'bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best'})

        # Update title immediately if available from manual extraction
        if extracted_title:
            vdown_tasks[task_id]['title'] = extracted_title

        # Try multiple download methods
        download_success = False
        final_path = None

        # Method 1: Try yt-dlp first (works for most sites including YouTube, m3u8, etc.)
        try:
            with yt_dlp.YoutubeDL(ydl_opts) as ydl:
                if cancel_flags.get(task_id): raise Exception("Cancelled by user")

                # If title wasn't found manually, try to fetch metadata first (without downloading)
                if not extracted_title:
                    try:
                        meta = ydl.extract_info(actual_url, download=False)
                        vdown_tasks[task_id]['title'] = meta.get('title', 'Media')
                    except: pass

                if cancel_flags.get(task_id): raise Exception("Cancelled by user")

                info = ydl.extract_info(actual_url, download=True)

                if cancel_flags.get(task_id): raise Exception("Cancelled by user")

                # Determine the initial title (redundant check but keeps logic safe)
                if extracted_title:
                    current_title = extracted_title
                else:
                    current_title = info.get('title', 'Media')

                # --- Universal Fallback for Generic Titles ---
                # If the title looks like a generic filename, try to scrape the page title
                generic_patterns = [r'^c$', r'^video$', r'^index$', r'^master$', r'^playlist$', r'^stream$', r'^manifest$', r'^videoplayback$', r'^output$', r'^[a-zA-Z0-9]{1,4}$']
                is_generic = any(re.match(p, current_title, re.IGNORECASE) for p in generic_patterns)

                if is_generic and not extracted_title:
                    try:
                        # Fetch the original URL to get the page title
                        page_r = requests.get(url, headers={"User-Agent": "Mozilla/5.0"}, timeout=5)
                        # Try og:title first
                        m_og = re.search(r'<meta\s+property=["\']og:title["\']\s+content=["\']([^"\"]+)["\"]', page_r.text)
                        if m_og:
                            current_title = m_og.group(1).strip()
                        else:
                            # Try <title>
                            m_tag = re.search(r'<title>(.*?)</title>', page_r.text)
                            if m_tag:
                                current_title = m_tag.group(1).strip()

                        # Cleanup common suffixes (optional but helpful)
                        current_title = re.sub(r'\s*[-|]\s*.*$', '', current_title) # Remove " - SiteName" or " | SiteName"
                    except:
                        pass

                vdown_tasks[task_id]['title'] = current_title

                final_path = ydl.prepare_filename(info)
                if format_type == 'audio': final_path = os.path.splitext(final_path)[0] + ".mp3"
                vdown_tasks[task_id]['server_file'] = os.path.basename(final_path)
                download_success = True

        except Exception as ydl_error:
            print(f"yt-dlp download failed: {ydl_error}")

            # Method 2: If yt-dlp failed and we have a direct download URL (mp4, etc.), try direct download
            if direct_download_url and direct_download_url.endswith('.mp4') and format_type == 'video':
                print(f"Attempting direct download from: {direct_download_url}")
                vdown_tasks[task_id]['title'] = extracted_title or 'Video'
                output_file = os.path.join(upload_folder, f"{task_id}.mp4")

                if direct_download_with_progress(direct_download_url, output_file, task_id, ydl_headers):
                    final_path = output_file
                    vdown_tasks[task_id]['server_file'] = os.path.basename(output_file)
                    download_success = True
                    print("Direct download succeeded")
                else:
                    raise Exception(f"Both yt-dlp and direct download failed. Original error: {ydl_error}")
            else:
                # No fallback available, re-raise the original error
                raise ydl_error

        if download_success:
            vdown_tasks[task_id]['status'] = 'completed'
        else:
            raise Exception("Download failed with all methods")
    except Exception as e:
        if cancel_flags.get(task_id):
            vdown_tasks[task_id]['status'] = 'cancelled'
            vdown_tasks[task_id]['message'] = 'Cancelled by user'
        else:
            vdown_tasks[task_id]['status'] = 'error'
            vdown_tasks[task_id]['message'] = str(e)
    finally:
        # Cleanup cancellation flag
        if task_id in cancel_flags:
            del cancel_flags[task_id]

@vdown_bp.route('/', methods=['GET', 'POST'])
def vdown_index():
    if request.method == 'GET': return render_template('vdown.html')
    data = request.get_json(silent=True) or request.form
    task_id = str(uuid.uuid4())
    vdown_tasks[task_id] = {'status': 'pending', 'title': 'Fetching...', 'progress': {'percent': 0, 'speed': '0B/s'}}
    threading.Thread(target=process_vdown_task, args=(task_id, data.get('url'), UPLOAD_FOLDER, data.get('format', 'video')), daemon=True).start()
    return jsonify({'status': 'started', 'task_id': task_id})

@vdown_bp.route('/status/<task_id>')
def vdown_status(task_id):
    return jsonify(vdown_tasks.get(task_id, {'status': 'missing'}))

@vdown_bp.route('/cancel/<task_id>', methods=['POST'])
def vdown_cancel(task_id):
    if task_id in vdown_tasks:
        cancel_flags[task_id] = True
        return jsonify({'status': 'cancelling'})
    return jsonify({'status': 'not found'}), 404

@vdown_bp.route('/download/<task_id>')
def vdown_download(task_id):
    task = vdown_tasks.get(task_id)
    if not task or 'server_file' not in task: return "Not Found", 404
    p = os.path.join(UPLOAD_FOLDER, task['server_file'])
    if os.path.exists(p):
        ext = os.path.splitext(task['server_file'])[1]
        safe_title = re.sub(r'[<>:"/\\|?*]', '', task.get('title', 'download'))
        return send_file(p, as_attachment=True, download_name=f"{safe_title}{ext}")
    return "File Not Found", 404
