from flask import Blueprint, render_template, request, redirect, url_for, jsonify, session
import psycopg2
import os
import uuid
import re
import smtplib
import random
import string
import time
import bcrypt
import requests
import jwt
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from dotenv import load_dotenv

web_auth_bp = Blueprint('web_auth', __name__, template_folder='templates', static_folder='static')

# Load DB Config
env_path = r"C:\woopang\server\.env"
load_dotenv(dotenv_path=env_path)
DB_PASSWORD = os.getenv("DB_PASSWORD")
SMTP_SERVER = os.getenv("SMTP_SERVER", "smtp.gmail.com")
SMTP_PORT = int(os.getenv("SMTP_PORT", 587))
SMTP_EMAIL = os.getenv("SMTP_EMAIL")
SMTP_PASSWORD = os.getenv("SMTP_PASSWORD")

# Social Login Keys
KAKAO_REST_API_KEY = os.getenv("KAKAO_REST_API_KEY")
GOOGLE_CLIENT_ID = os.getenv("GOOGLE_CLIENT_ID")
APPLE_CLIENT_ID = os.getenv("APPLE_CLIENT_ID")
APPLE_TEAM_ID = os.getenv("APPLE_TEAM_ID")
APPLE_KEY_ID = os.getenv("APPLE_KEY_ID")
APPLE_PRIVATE_KEY = os.getenv("APPLE_PRIVATE_KEY", "").replace("\\n", "\n")

# JWT Secret for auth tokens
JWT_SECRET = os.getenv("JWT_SECRET", "fallback_secret_key_change_in_production")

# Memory storage
auth_cache = {}

def generate_auth_token(user_id, username, provider):
    """
    로그인 성공 시 JWT 토큰 생성
    - 토큰에 user_id, username, provider 포함
    - 1시간 만료
    """
    payload = {
        'user_id': user_id,
        'username': username,
        'provider': provider,
        'exp': time.time() + 3600,  # 1시간 만료
        'iat': time.time()
    }
    return jwt.encode(payload, JWT_SECRET, algorithm='HS256')

def verify_auth_token(token):
    """
    JWT 토큰 검증
    - 유효하면 user_id, username, provider 반환
    - 무효하면 None 반환
    """
    try:
        payload = jwt.decode(token, JWT_SECRET, algorithms=['HS256'])
        # 만료 체크
        if payload.get('exp', 0) < time.time():
            return None
        return {
            'user_id': payload.get('user_id'),
            'username': payload.get('username'),
            'provider': payload.get('provider')
        }
    except jwt.InvalidTokenError:
        return None

# Translations (EN, KO, JA, ZH, ES)
TRANSLATIONS = {
    'en': {
        'title': 'Premium Login', 'email_label': 'Email Address', 'email_or_username': 'Email or Username', 'password': 'Password', 'login_btn': 'Login',
        'or': 'OR', 'kakao_login': 'Login with Kakao', 'google_login': 'Login with Google', 'apple_login': 'Login with Apple',
        'no_account': "Don't have an account?", 'join_link': 'Join', 'join_btn': 'Join',
        'have_account': 'Already have an account?', 'login_link': 'Login', 'send_btn': 'Send', 'verify_code': 'Verification Code',
        'verify_btn': 'Verify', 'resend_btn': 'Resend', 'username': 'Username', 'confirm_password': 'Confirm Password',
        're_enter_password': 'Re-enter Password', 'min_chars': 'Min 8 characters', 'sending': 'Sending',
        'username_hint': 'Max 20 characters', 'username_label': 'Username',
        'pw_strength': 'Password Strength', 'pw_weak': 'Weak', 'pw_medium': 'Medium', 'pw_strong': 'Strong',
        'pw_mismatch_hint': 'Passwords do not match.',
        'msg_email_required': 'Email is required.', 'msg_code_sent': 'Verification code sent!',
        'msg_send_failed': 'Failed to send email.', 'msg_verified': 'Email verified!',
        'msg_invalid_code': 'Invalid or expired code.', 'msg_invalid_login': 'Account does not exist or password is incorrect.',
        'msg_server_error': 'Server Error.', 'msg_invalid_username': 'Invalid username format or length.',
        'msg_weak_password': 'Password must be at least 8 characters.', 'msg_email_exists': 'Email already exists.',
        'msg_username_exists': 'Username already exists.',
        'msg_reg_failed': 'Registration failed.', 'msg_mismatch': 'Passwords do not match!',
        'msg_verify_expired': 'Verification expired. Please verify again.',
        'success': 'Success', 'error': 'Error', 'failed': 'Failed', 'verified': 'Verified',
        'mail_subject': '[Woopang] Your Verification Code',
        'mail_hi': 'Hello!',
        'mail_body': 'To complete your registration, please use the verification code below:',
        'mail_expire': 'This code will expire in 5 minutes.',
        'mail_copy': 'Copy Code',
        'mail_footer': 'If you did not request this, please ignore this email.',
        'login_success_title': 'LOGIN SUCCESS',
        'login_success': 'Returning to app in...',
        'moving_to_app': 'Moving to app in seconds',
        'return_btn': 'Return to App Manually',
        'set_username_title': 'One Last Step',
        'set_username_msg': 'Please choose your username to complete registration.',
        # Profile Edit Translations
        'profile_title': 'Edit Profile', 'save': 'Save', 'saving': 'Saving...', 'likes': 'Likes', 'followers': 'Followers', 'following': 'Following',
        'basic_info': 'Basic Information', 'phone_placeholder': 'Phone number', 'bio_placeholder': 'Write something about yourself...',
        'social_links': 'Social Links', 'security': 'Security', 'change_password': 'Change Password',
        'current_password': 'Current Password', 'new_password': 'New Password', 'password_hint': 'Password must be at least 8 characters',
        'pw_mismatch': 'Passwords do not match', 'logout': 'Log Out', 'logout_confirm': 'Are you sure you want to log out?',
        'unsaved_changes': 'You have unsaved changes. Discard?', 'file_too_large': 'File is too large (max 5MB)',
        'invalid_username': 'Invalid username format', 'enter_current_password': 'Please enter current password',
        'password_too_short': 'Password must be at least 8 characters', 'profile_updated': 'Profile updated successfully!',
        'update_failed': 'Failed to update profile', 'network_error': 'Network error. Please try again.',
        'invalid_token': 'Invalid or expired session', 'wrong_password': 'Current password is incorrect',
        'username_change_limit': 'Username can only be changed once per month',
        'username_change_confirm': 'Are you sure you want to change your username? You can only change it once per month.',
        'username_lowercase_hint': 'Usernames are automatically converted to lowercase',
        'email': 'Email',
        'delete_account': 'Delete Account',
        'delete_confirm': 'Are you sure you want to delete your account? This action cannot be undone.',
        'delete_final_confirm': 'Type DELETE to confirm account deletion:',
        'delete_cancelled': 'Account deletion cancelled.',
        'account_deleted': 'Your account has been deleted.',
        'delete_failed': 'Failed to delete account.',
        # Terms & Conditions
        'terms_title': 'Terms of Service',
        'terms_scroll_to_read': 'Please scroll down to read all terms',
        'terms_read_complete': 'Terms fully read',
        'terms_agree_all': 'Agree to all',
        'terms_agree_all_desc': 'Agree to all terms and conditions below',
        'terms_agree_terms': 'Terms of Service (Required)',
        'terms_agree_privacy': 'Privacy Policy (Required)',
        'terms_agree_location': 'Location Information Collection (Required)',
        'terms_agree_marketing': 'Marketing Information (Optional)',
        'terms_required': 'Required',
        'terms_optional': 'Optional',
        'terms_continue': 'Agree and Continue',
        'terms_article1_title': 'Article 1 (Purpose)',
        'terms_article1_content': 'These Terms of Service are intended to define the rights, obligations, and responsibilities between WOOPANG (hereinafter referred to as the "Company") and users in the use of the WOOPANG augmented reality (AR) social platform service.',
        'terms_article2_title': 'Article 2 (Definition of Terms)',
        'terms_article2_content': '"Service" refers to the augmented reality-based social networking service provided by the Company. "User" refers to a member who accesses the Service and uses it in accordance with these Terms. "Content" refers to all information, including text, photos, videos, and AR content created or uploaded by users.',
        'terms_article3_title': 'Article 3 (Service Description)',
        'terms_article3_content': 'WOOPANG is an AR-based social platform that allows users to share their location and interact with other users in real-time. The service enables virtual content placement in the real world and social interaction with nearby users.',
        'terms_p2p_warning_title': 'Important Notice: Real-time Location Sharing',
        'terms_p2p_warning_content': 'This service uses P2P (peer-to-peer) technology to enable real-time interactions with other users. When the P2P feature is enabled, your current location may be shared in real-time with other nearby users. Please be aware that your location information may be visible to others when using this feature.',
        'terms_article4_title': 'Article 4 (User Obligations)',
        'terms_article4_content': 'Users must not engage in the following activities:',
        'terms_article4_item1': 'Using false information during registration or impersonating others',
        'terms_article4_item2': 'Posting illegal, harmful, or offensive content',
        'terms_article4_item3': 'Infringing on the rights of other users or third parties',
        'terms_article4_item4': 'Violating laws or these Terms of Service',
        'terms_article5_title': 'Article 5 (Collection and Use of Location Information)',
        'terms_article5_content': 'The Company collects user location information for service provision. This information is used to provide AR features and connect with nearby users. Location information may be shared with other users when the P2P feature is enabled.',
        'terms_article6_title': 'Article 6 (Privacy Protection)',
        'terms_article6_content': 'The Company collects and uses personal information in accordance with its Privacy Policy. Users can review their data collection settings and opt out of certain data collection at any time through the app settings.',
        'terms_article7_title': 'Article 7 (Limitation of Liability)',
        'terms_article7_content': 'The Company is not responsible for any damages caused by user-generated content or interactions between users. Users are responsible for their own actions and the content they create on the platform.',
        'terms_article8_title': 'Article 8 (Governing Law)',
        'terms_article8_content': 'These Terms are governed by the laws of the Republic of Korea. Any disputes arising from the use of the Service shall be subject to the exclusive jurisdiction of the Seoul Central District Court.',
        'company_name': 'Kwae Entertainment',
        'company_address': 'Seoul, Republic of Korea'
    },
    'ko': {
        'title': '로그인', 'email_label': '이메일 주소', 'email_or_username': '이메일 또는 사용자명', 'password': '비밀번호', 'login_btn': '로그인',
        'or': '또는', 'kakao_login': '카카오 로그인', 'google_login': 'Google 로그인', 'apple_login': 'Apple 로그인',
        'no_account': '계정이 없으신가요?', 'join_link': '가입하기', 'join_btn': '가입하기',
        'have_account': '이미 계정이 있으신가요?', 'login_link': '로그인', 'send_btn': '전송', 'verify_code': '인증번호',
        'verify_btn': '확인', 'resend_btn': '재전송', 'username': '사용자명', 'confirm_password': '비밀번호 확인',
        're_enter_password': '비밀번호 재입력', 'min_chars': '최소 8자 이상', 'sending': '전송 중',
        'username_hint': '최대 20글자', 'username_label': '사용자명',
        'pw_strength': '비밀번호 안전성', 'pw_weak': '약함', 'pw_medium': '보통', 'pw_strong': '강함',
        'pw_mismatch_hint': '비밀번호가 일치하지 않습니다.',
        'msg_email_required': '이메일을 입력해주세요.', 'msg_code_sent': '인증번호가 발송되었습니다.',
        'msg_send_failed': '메일 발송 실패.', 'msg_verified': '이메일 인증 완료!',
        'msg_invalid_code': '번호가 틀렸거나 만료되었습니다.', 'msg_invalid_login': '존재하지 않는 계정이거나 비밀번호가 틀렸습니다.',
        'msg_server_error': '서버 오류 발생.', 'msg_invalid_username': '사용자명 형식이 맞지 않습니다.',
        'msg_weak_password': '비밀번호는 8자 이상이어야 합니다.', 'msg_email_exists': '이미 가입된 이메일입니다.',
        'msg_username_exists': '이미 사용 중인 사용자명입니다.',
        'msg_reg_failed': '가입에 실패했습니다.', 'msg_mismatch': '비밀번호가 일치하지 않습니다!',
        'msg_verify_expired': '인증 시간이 만료되었습니다. 다시 인증해주세요.',
        'success': '성공', 'error': '오류', 'failed': '실패', 'verified': '인증됨',
        'mail_subject': '[우팡] 이메일 인증 번호 안내',
        'mail_hi': '안녕하세요!',
        'mail_body': '회원가입을 완료하려면 아래의 인증 번호를 입력해 주세요:',
        'mail_expire': '이 코드는 5분 후에 만료됩니다.',
        'mail_copy': '번호 복사하기',
        'mail_footer': '본인이 요청하지 않은 경우 이 메일을 무시하셔도 됩니다.',
        'login_success_title': '로그인 성공',
        'login_success': '앱으로 이동합니다...',
        'moving_to_app': '초 뒤 앱으로 자동 이동합니다.',
        'return_btn': '앱으로 수동 돌아가기',
        'set_username_title': '마지막 단계',
        'set_username_msg': '가입을 완료하기 위해 사용자명을 설정해주세요.',
        # Profile Edit Translations
        'profile_title': '프로필 편집', 'save': '저장', 'saving': '저장 중...', 'likes': '좋아요', 'followers': '팔로워', 'following': '팔로잉',
        'basic_info': '기본 정보', 'phone_placeholder': '전화번호', 'bio_placeholder': '자기소개를 작성해주세요...',
        'social_links': '소셜 링크', 'security': '보안', 'change_password': '비밀번호 변경',
        'current_password': '현재 비밀번호', 'new_password': '새 비밀번호', 'password_hint': '비밀번호는 8자 이상이어야 합니다',
        'pw_mismatch': '비밀번호가 일치하지 않습니다', 'logout': '로그아웃', 'logout_confirm': '로그아웃 하시겠습니까?',
        'unsaved_changes': '저장하지 않은 변경사항이 있습니다. 취소하시겠습니까?', 'file_too_large': '파일 크기가 너무 큽니다 (최대 5MB)',
        'invalid_username': '사용자명 형식이 올바르지 않습니다', 'enter_current_password': '현재 비밀번호를 입력해주세요',
        'password_too_short': '비밀번호는 8자 이상이어야 합니다', 'profile_updated': '프로필이 업데이트되었습니다!',
        'update_failed': '프로필 업데이트 실패', 'network_error': '네트워크 오류입니다. 다시 시도해주세요.',
        'invalid_token': '세션이 유효하지 않습니다', 'wrong_password': '현재 비밀번호가 틀렸습니다',
        'username_change_limit': '사용자명은 한 달에 한 번만 변경할 수 있습니다',
        'username_change_confirm': '사용자명을 변경하시겠습니까? 한 달에 한 번만 변경할 수 있습니다.',
        'username_lowercase_hint': '사용자명은 자동으로 소문자로 변환됩니다',
        'email': '이메일',
        'delete_account': '회원탈퇴',
        'delete_confirm': '정말 탈퇴하시겠습니까? 이 작업은 되돌릴 수 없습니다.',
        'delete_final_confirm': '탈퇴를 확인하려면 "삭제"를 입력하세요:',
        'delete_cancelled': '탈퇴가 취소되었습니다.',
        'account_deleted': '계정이 삭제되었습니다.',
        'delete_failed': '계정 삭제에 실패했습니다.',
        # Terms & Conditions
        'terms_title': '이용약관',
        'terms_scroll_to_read': '약관을 끝까지 읽어주세요',
        'terms_read_complete': '약관을 모두 읽었습니다',
        'terms_agree_all': '전체 동의',
        'terms_agree_all_desc': '아래 모든 약관에 동의합니다',
        'terms_agree_terms': '이용약관 동의 (필수)',
        'terms_agree_privacy': '개인정보 처리방침 동의 (필수)',
        'terms_agree_location': '위치정보 수집 동의 (필수)',
        'terms_agree_marketing': '마케팅 정보 수신 동의 (선택)',
        'terms_required': '필수',
        'terms_optional': '선택',
        'terms_continue': '동의하고 계속하기',
        'terms_article1_title': '제1조 (목적)',
        'terms_article1_content': '본 약관은 쾌엔터테인먼트(이하 "회사")가 제공하는 WOOPANG 증강현실(AR) 소셜 플랫폼 서비스의 이용과 관련하여 회사와 이용자 간의 권리, 의무 및 책임 사항을 규정함을 목적으로 합니다.',
        'terms_article2_title': '제2조 (용어의 정의)',
        'terms_article2_content': '"서비스"란 회사가 제공하는 증강현실 기반 소셜 네트워킹 서비스를 의미합니다. "이용자"란 본 약관에 따라 서비스에 접속하여 이용하는 회원을 말합니다. "콘텐츠"란 이용자가 작성하거나 업로드한 텍스트, 사진, 영상, AR 콘텐츠 등 모든 정보를 의미합니다.',
        'terms_article3_title': '제3조 (서비스 내용)',
        'terms_article3_content': 'WOOPANG은 이용자가 자신의 위치를 공유하고 다른 이용자와 실시간으로 상호작용할 수 있는 AR 기반 소셜 플랫폼입니다. 서비스는 현실 세계에 가상 콘텐츠를 배치하고 주변 이용자와의 소셜 상호작용을 가능하게 합니다.',
        'terms_p2p_warning_title': '중요 안내: 실시간 위치 공유',
        'terms_p2p_warning_content': '본 서비스는 P2P(피어투피어) 기술을 사용하여 다른 이용자와 실시간 상호작용을 가능하게 합니다. P2P 기능이 활성화되면 현재 위치가 주변의 다른 이용자에게 실시간으로 공유될 수 있습니다. 이 기능을 사용할 때 위치 정보가 다른 사용자에게 노출될 수 있음을 인지하시기 바랍니다.',
        'terms_article4_title': '제4조 (이용자의 의무)',
        'terms_article4_content': '이용자는 다음 행위를 하여서는 안 됩니다:',
        'terms_article4_item1': '허위 정보를 사용하여 가입하거나 타인을 사칭하는 행위',
        'terms_article4_item2': '불법적, 유해한 또는 불쾌한 콘텐츠를 게시하는 행위',
        'terms_article4_item3': '다른 이용자 또는 제3자의 권리를 침해하는 행위',
        'terms_article4_item4': '법령 또는 본 약관을 위반하는 행위',
        'terms_article5_title': '제5조 (위치정보의 수집 및 이용)',
        'terms_article5_content': '회사는 서비스 제공을 위해 이용자의 위치정보를 수집합니다. 이 정보는 AR 기능 제공 및 주변 이용자와의 연결에 사용됩니다. P2P 기능이 활성화되면 위치정보가 다른 이용자와 공유될 수 있습니다.',
        'terms_article6_title': '제6조 (개인정보 보호)',
        'terms_article6_content': '회사는 개인정보처리방침에 따라 개인정보를 수집하고 이용합니다. 이용자는 앱 설정을 통해 언제든지 데이터 수집 설정을 확인하고 특정 데이터 수집을 거부할 수 있습니다.',
        'terms_article7_title': '제7조 (책임의 제한)',
        'terms_article7_content': '회사는 이용자가 생성한 콘텐츠 또는 이용자 간의 상호작용으로 인한 손해에 대해 책임지지 않습니다. 이용자는 플랫폼에서의 자신의 행동과 생성한 콘텐츠에 대해 스스로 책임을 집니다.',
        'terms_article8_title': '제8조 (준거법)',
        'terms_article8_content': '본 약관은 대한민국 법률에 따라 규율됩니다. 서비스 이용으로 인한 분쟁은 서울중앙지방법원을 전속 관할 법원으로 합니다.',
        'company_name': '쾌엔터테인먼트',
        'company_address': '대한민국 서울'
    },
    'ja': {
        'title': 'ログイン', 'email_label': 'メールアドレス', 'email_or_username': 'メールまたはユーザー名', 'password': 'パスワード', 'login_btn': 'ログイン',
        'or': 'または', 'kakao_login': 'Kakaoでログイン', 'google_login': 'Googleでログイン', 'apple_login': 'Appleでログイン',
        'no_account': 'アカウントをお持ちでないですか？', 'join_link': '登録', 'join_btn': '登録',
        'have_account': 'すでにアカウントをお持ちですか？', 'login_link': 'ログイン', 'send_btn': '送信', 'verify_code': '認証コード',
        'verify_btn': '確認', 'resend_btn': '再送信', 'username': 'ユーザー名', 'confirm_password': 'パスワード確認',
        're_enter_password': 'パスワード再入力', 'min_chars': '8文字以上', 'sending': '送信中',
        'username_hint': '最大20文字', 'username_label': 'ユーザー名',
        'pw_strength': 'パスワード強度', 'pw_weak': '弱い', 'pw_medium': '普通', 'pw_strong': '強い',
        'pw_mismatch_hint': 'パスワードが一致しません。',
        'msg_email_required': 'メールアドレスが必要です。', 'msg_code_sent': '認証コードを送信しました。',
        'msg_send_failed': '送信に失敗しました。', 'msg_verified': '認証が完了しました！',
        'msg_invalid_code': 'コードが正しくないか、期限切れです。', 'msg_invalid_login': 'アカウントが存在しないか、パスワードが間違っています。',
        'msg_server_error': 'サーバーエラーが発生しました。', 'msg_invalid_username': '形式が正しくありません。',
        'msg_weak_password': 'パスワードは8文字以上必要です。', 'msg_email_exists': '既に登録されています。',
        'msg_username_exists': 'このユーザー名は既に使用されています。',
        'msg_reg_failed': '登録に失敗しました。', 'msg_mismatch': 'パスワードが一致しません！',
        'msg_verify_expired': '期限が切れました。再試行してください。',
        'success': '成功', 'error': 'エラー', 'failed': '失敗', 'verified': '認証済み',
        'mail_subject': '[WOOPANG] 認証コードのご案内',
        'mail_hi': 'こんにちは！',
        'mail_body': '登録を完了するには、以下の認証コードを使用してください:',
        'mail_expire': 'このコードは5分間有効です。',
        'mail_copy': 'コードをコピー',
        'mail_footer': '心当たりがない場合は、このメールを破棄してください。',
        'login_success_title': 'ログイン成功',
        'login_success': 'アプリに戻っています...',
        'moving_to_app': '秒後にアプリへ移動します。',
        'return_btn': '手動でアプリに戻る',
        'set_username_title': '最後のステップ',
        'set_username_msg': '登録を完了するためにユーザー名を選択してください。',
        # Profile Edit Translations
        'profile_title': 'プロフィール編集', 'save': '保存', 'saving': '保存中...', 'likes': 'いいね', 'followers': 'フォロワー', 'following': 'フォロー中',
        'basic_info': '基本情報', 'phone_placeholder': '電話番号', 'bio_placeholder': '自己紹介を書いてください...',
        'social_links': 'ソーシャルリンク', 'security': 'セキュリティ', 'change_password': 'パスワード変更',
        'current_password': '現在のパスワード', 'new_password': '新しいパスワード', 'password_hint': 'パスワードは8文字以上必要です',
        'pw_mismatch': 'パスワードが一致しません', 'logout': 'ログアウト', 'logout_confirm': 'ログアウトしますか？',
        'unsaved_changes': '保存していない変更があります。破棄しますか？', 'file_too_large': 'ファイルサイズが大きすぎます（最大5MB）',
        'invalid_username': 'ユーザー名の形式が正しくありません', 'enter_current_password': '現在のパスワードを入力してください',
        'password_too_short': 'パスワードは8文字以上必要です', 'profile_updated': 'プロフィールが更新されました！',
        'update_failed': 'プロフィールの更新に失敗しました', 'network_error': 'ネットワークエラーです。もう一度お試しください。',
        'invalid_token': 'セッションが無効です', 'wrong_password': '現在のパスワードが間違っています',
        'username_change_limit': 'ユーザー名は月に1回のみ変更可能です',
        'username_change_confirm': 'ユーザー名を変更しますか？月に1回のみ変更可能です。',
        'username_lowercase_hint': 'ユーザー名は自動的に小文字に変換されます',
        'email': 'メールアドレス',
        'delete_account': 'アカウント削除',
        'delete_confirm': 'アカウントを削除してもよろしいですか？この操作は取り消せません。',
        'delete_final_confirm': 'アカウント削除を確認するには「削除」と入力してください:',
        'delete_cancelled': 'アカウント削除がキャンセルされました。',
        'account_deleted': 'アカウントが削除されました。',
        'delete_failed': 'アカウントの削除に失敗しました。',
        # Terms & Conditions
        'terms_title': '利用規約',
        'terms_scroll_to_read': '最後までスクロールしてお読みください',
        'terms_read_complete': '規約を全て読みました',
        'terms_agree_all': '全てに同意',
        'terms_agree_all_desc': '以下の全ての規約に同意します',
        'terms_agree_terms': '利用規約に同意 (必須)',
        'terms_agree_privacy': 'プライバシーポリシーに同意 (必須)',
        'terms_agree_location': '位置情報の収集に同意 (必須)',
        'terms_agree_marketing': 'マーケティング情報の受信に同意 (任意)',
        'terms_required': '必須',
        'terms_optional': '任意',
        'terms_continue': '同意して続ける',
        'terms_article1_title': '第1条（目的）',
        'terms_article1_content': '本規約は、WOOPANG（以下「当社」）が提供するWOOPANG拡張現実（AR）ソーシャルプラットフォームサービスの利用に関して、当社とユーザー間の権利、義務、責任を規定することを目的とします。',
        'terms_article2_title': '第2条（用語の定義）',
        'terms_article2_content': '「サービス」とは、当社が提供する拡張現実ベースのソーシャルネットワーキングサービスを意味します。「ユーザー」とは、本規約に従ってサービスにアクセスし利用する会員を意味します。「コンテンツ」とは、ユーザーが作成またはアップロードしたテキスト、写真、動画、ARコンテンツなどの全ての情報を意味します。',
        'terms_article3_title': '第3条（サービス内容）',
        'terms_article3_content': 'WOOPANGは、ユーザーが自分の位置を共有し、他のユーザーとリアルタイムで交流できるARベースのソーシャルプラットフォームです。サービスは現実世界に仮想コンテンツを配置し、近くのユーザーとのソーシャルインタラクションを可能にします。',
        'terms_p2p_warning_title': '重要なお知らせ：リアルタイム位置共有',
        'terms_p2p_warning_content': '本サービスはP2P（ピアツーピア）技術を使用して、他のユーザーとのリアルタイムインタラクションを可能にします。P2P機能が有効な場合、現在地が近くの他のユーザーにリアルタイムで共有される可能性があります。この機能を使用する際は、位置情報が他のユーザーに表示される可能性があることをご了承ください。',
        'terms_article4_title': '第4条（ユーザーの義務）',
        'terms_article4_content': 'ユーザーは以下の行為を行ってはなりません：',
        'terms_article4_item1': '虚偽の情報を使用した登録または他人へのなりすまし',
        'terms_article4_item2': '違法、有害、または不快なコンテンツの投稿',
        'terms_article4_item3': '他のユーザーまたは第三者の権利を侵害する行為',
        'terms_article4_item4': '法令または本規約に違反する行為',
        'terms_article5_title': '第5条（位置情報の収集と利用）',
        'terms_article5_content': '当社はサービス提供のためにユーザーの位置情報を収集します。この情報はAR機能の提供と近くのユーザーとの接続に使用されます。P2P機能が有効な場合、位置情報は他のユーザーと共有される可能性があります。',
        'terms_article6_title': '第6条（プライバシー保護）',
        'terms_article6_content': '当社はプライバシーポリシーに従って個人情報を収集・利用します。ユーザーはアプリ設定からいつでもデータ収集設定を確認し、特定のデータ収集をオプトアウトすることができます。',
        'terms_article7_title': '第7条（責任の制限）',
        'terms_article7_content': '当社は、ユーザー生成コンテンツまたはユーザー間のインタラクションによって生じた損害について責任を負いません。ユーザーは、プラットフォーム上での自身の行動と作成したコンテンツについて自己責任を負います。',
        'terms_article8_title': '第8条（準拠法）',
        'terms_article8_content': '本規約は大韓民国の法律に準拠します。サービスの利用から生じる紛争は、ソウル中央地方裁判所を専属管轄裁判所とします。',
        'company_name': 'Kwae Entertainment',
        'company_address': '大韓民国 ソウル'
    },
    'zh': {
        'title': '高级登录', 'email_label': '电子邮件地址', 'email_or_username': '电子邮件或用户名', 'password': '密码', 'login_btn': '登录',
        'or': '或', 'kakao_login': '使用 Kakao 登录', 'google_login': '使用 Google 登录', 'apple_login': '使用 Apple 登录',
        'no_account': '还没有账号？', 'join_link': '注册', 'join_btn': '注册',
        'have_account': '已经有账号了？', 'login_link': '登录', 'send_btn': '发送', 'verify_code': '验证码',
        'verify_btn': '验证', 'resend_btn': '重新发送', 'username': '用户名', 'confirm_password': '确认密码',
        're_enter_password': '重新输入密码', 'min_chars': '至少8个字符', 'sending': '发送中',
        'username_hint': '最多20个字符', 'username_label': '用户名',
        'pw_strength': '密码强度', 'pw_weak': '弱', 'pw_medium': '中', 'pw_strong': '强',
        'pw_mismatch_hint': '密码不匹配。',
        'msg_email_required': '必须填写电子邮件。', 'msg_code_sent': '验证码已发送！',
        'msg_send_failed': '发送邮件失败。', 'msg_verified': '电子邮件已验证！',
        'msg_invalid_code': '验证码无效或已过期。', 'msg_invalid_login': '账号不存在或密码错误。',
        'msg_server_error': '服务器错误。', 'msg_invalid_username': '用户名格式或长度无效。',
        'msg_weak_password': '密码必须至少8个字符。', 'msg_email_exists': '电子邮件已存在。',
        'msg_username_exists': '该用户名已被使用。',
        'msg_reg_failed': '注册失败。', 'msg_mismatch': '密码不匹配！',
        'msg_verify_expired': '验证已过期。请重新验证。',
        'success': '成功', 'error': '错误', 'failed': '失败', 'verified': '已验证',
        'mail_subject': '[Woopang] 您的验证码',
        'mail_hi': '您好！',
        'mail_body': '要完成您的注册，请使用以下验证码：',
        'mail_expire': '此验证码将在5分钟后过期。',
        'mail_copy': '复制验证码',
        'mail_footer': '如果您没有请求此代码，请忽略此邮件。',
        'login_success_title': '登录成功',
        'login_success': '正在返回应用...',
        'moving_to_app': '秒后自动跳转应用',
        'return_btn': '手动返回应用',
        'set_username_title': '最后一步',
        'set_username_msg': '请选择您的用户名以完成注册。',
        # Profile Edit Translations
        'profile_title': '编辑个人资料', 'save': '保存', 'saving': '保存中...', 'likes': '喜欢', 'followers': '粉丝', 'following': '关注',
        'basic_info': '基本信息', 'phone_placeholder': '电话号码', 'bio_placeholder': '写一些关于你自己的介绍...',
        'social_links': '社交链接', 'security': '安全', 'change_password': '更改密码',
        'current_password': '当前密码', 'new_password': '新密码', 'password_hint': '密码必须至少8个字符',
        'pw_mismatch': '密码不匹配', 'logout': '退出登录', 'logout_confirm': '确定要退出登录吗？',
        'unsaved_changes': '您有未保存的更改。要放弃吗？', 'file_too_large': '文件太大（最大5MB）',
        'invalid_username': '用户名格式无效', 'enter_current_password': '请输入当前密码',
        'password_too_short': '密码必须至少8个字符', 'profile_updated': '个人资料已更新！',
        'update_failed': '更新个人资料失败', 'network_error': '网络错误，请重试。',
        'invalid_token': '会话无效', 'wrong_password': '当前密码不正确',
        'username_change_limit': '用户名每月只能更改一次',
        'username_change_confirm': '确定要更改用户名吗？每月只能更改一次。',
        'username_lowercase_hint': '用户名将自动转换为小写',
        'email': '电子邮件',
        'delete_account': '删除账户',
        'delete_confirm': '确定要删除账户吗？此操作无法撤销。',
        'delete_final_confirm': '输入"删除"以确认删除账户:',
        'delete_cancelled': '账户删除已取消。',
        'account_deleted': '您的账户已被删除。',
        'delete_failed': '删除账户失败。',
        # Terms & Conditions
        'terms_title': '服务条款',
        'terms_scroll_to_read': '请滚动阅读完整条款',
        'terms_read_complete': '条款已阅读完毕',
        'terms_agree_all': '全部同意',
        'terms_agree_all_desc': '同意以下所有条款',
        'terms_agree_terms': '服务条款同意 (必需)',
        'terms_agree_privacy': '隐私政策同意 (必需)',
        'terms_agree_location': '位置信息收集同意 (必需)',
        'terms_agree_marketing': '营销信息接收同意 (可选)',
        'terms_required': '必需',
        'terms_optional': '可选',
        'terms_continue': '同意并继续',
        'terms_article1_title': '第一条（目的）',
        'terms_article1_content': '本条款旨在规定WOOPANG（以下简称"公司"）与用户在使用WOOPANG增强现实（AR）社交平台服务过程中的权利、义务和责任。',
        'terms_article2_title': '第二条（术语定义）',
        'terms_article2_content': '"服务"指公司提供的基于增强现实的社交网络服务。"用户"指按照本条款访问并使用服务的会员。"内容"指用户创建或上传的文字、照片、视频、AR内容等所有信息。',
        'terms_article3_title': '第三条（服务内容）',
        'terms_article3_content': 'WOOPANG是一个基于AR的社交平台，用户可以分享位置并与其他用户实时互动。服务可在现实世界中放置虚拟内容，并与附近用户进行社交互动。',
        'terms_p2p_warning_title': '重要提示：实时位置共享',
        'terms_p2p_warning_content': '本服务使用P2P（点对点）技术实现与其他用户的实时互动。当P2P功能启用时，您的当前位置可能会实时共享给附近的其他用户。请注意，使用此功能时，您的位置信息可能会被其他用户看到。',
        'terms_article4_title': '第四条（用户义务）',
        'terms_article4_content': '用户不得从事以下行为：',
        'terms_article4_item1': '使用虚假信息注册或冒充他人',
        'terms_article4_item2': '发布非法、有害或令人不快的内容',
        'terms_article4_item3': '侵犯其他用户或第三方的权利',
        'terms_article4_item4': '违反法律或本条款',
        'terms_article5_title': '第五条（位置信息的收集和使用）',
        'terms_article5_content': '公司为提供服务而收集用户的位置信息。该信息用于提供AR功能和连接附近用户。当P2P功能启用时，位置信息可能会与其他用户共享。',
        'terms_article6_title': '第六条（隐私保护）',
        'terms_article6_content': '公司按照隐私政策收集和使用个人信息。用户可以随时通过应用设置查看数据收集设置并选择退出某些数据收集。',
        'terms_article7_title': '第七条（责任限制）',
        'terms_article7_content': '公司不对用户生成的内容或用户之间的互动造成的损害承担责任。用户对自己在平台上的行为和创建的内容负责。',
        'terms_article8_title': '第八条（适用法律）',
        'terms_article8_content': '本条款受大韩民国法律管辖。因使用服务而产生的争议，由首尔中央地方法院专属管辖。',
        'company_name': 'Kwae Entertainment',
        'company_address': '韩国首尔'
    },
    'es': {
        'title': 'Inicio de Sesión Premium', 'email_label': 'Correo Electrónico', 'email_or_username': 'Correo o Usuario', 'password': 'Contraseña', 'login_btn': 'Iniciar Sesión',
        'or': 'O', 'kakao_login': 'Iniciar con Kakao', 'google_login': 'Iniciar con Google', 'apple_login': 'Iniciar con Apple',
        'no_account': '¿No tienes una cuenta?', 'join_link': 'Regístrate', 'join_btn': 'Regístrate',
        'have_account': '¿Ya tienes una cuenta?', 'login_link': 'Inicia Sesión', 'send_btn': 'Enviar', 'verify_code': 'Código de Verificación',
        'verify_btn': 'Verificar', 'resend_btn': 'Reenviar', 'username': 'Nombre de Usuario', 'confirm_password': 'Confirmar Contraseña',
        're_enter_password': 'Reingrese Contraseña', 'min_chars': 'Mín. 8 caracteres', 'sending': 'Enviando',
        'username_hint': 'Máx. 20 caracteres', 'username_label': 'Usuario',
        'pw_strength': 'Seguridad de Contraseña', 'pw_weak': 'Débil', 'pw_medium': 'Media', 'pw_strong': 'Fuerte',
        'pw_mismatch_hint': 'Las contraseñas no coinciden.',
        'msg_email_required': 'El correo es obligatorio.', 'msg_code_sent': '¡Código enviado!',
        'msg_send_failed': 'Error al enviar correo.', 'msg_verified': '¡Correo verificado!',
        'msg_invalid_code': 'Código inválido o expirado.', 'msg_invalid_login': 'La cuenta no existe o la contraseña es incorrecta.',
        'msg_server_error': 'Error del servidor.', 'msg_invalid_username': 'Formato o longitud de usuario no válido.',
        'msg_weak_password': 'La contraseña debe tener al menos 8 caracteres.', 'msg_email_exists': 'El correo ya existe.',
        'msg_username_exists': 'Este nombre de usuario ya está en uso.',
        'msg_reg_failed': 'Error en el registro.', 'msg_mismatch': '¡Las contraseñas no coinciden!',
        'msg_verify_expired': 'Verificación expirada. Por favor, verifique de nuevo.',
        'success': 'Éxito', 'error': 'Error', 'failed': 'Fallido', 'verified': 'Verificado',
        'mail_subject': '[Woopang] Tu Código de Verificación',
        'mail_hi': '¡Hola!',
        'mail_body': 'Para completar tu registro, utiliza el siguiente código de verificación:',
        'mail_expire': 'Este código expirará en 5 minutos.',
        'mail_copy': 'Copiar Código',
        'mail_footer': 'Si no solicitaste esto, ignora este correo.',
        'login_success_title': 'INICIO EXITOSO',
        'login_success': 'Volviendo a la aplicación en...',
        'moving_to_app': 'Redirigiendo en segundos',
        'return_btn': 'Volver a la App Manualmente',
        'set_username_title': 'Último Paso',
        'set_username_msg': 'Elige tu nombre de usuario para completar el registro.',
        # Profile Edit Translations
        'profile_title': 'Editar Perfil', 'save': 'Guardar', 'saving': 'Guardando...', 'likes': 'Me gusta', 'followers': 'Seguidores', 'following': 'Siguiendo',
        'basic_info': 'Información Básica', 'phone_placeholder': 'Número de teléfono', 'bio_placeholder': 'Escribe algo sobre ti...',
        'social_links': 'Enlaces Sociales', 'security': 'Seguridad', 'change_password': 'Cambiar Contraseña',
        'current_password': 'Contraseña Actual', 'new_password': 'Nueva Contraseña', 'password_hint': 'La contraseña debe tener al menos 8 caracteres',
        'pw_mismatch': 'Las contraseñas no coinciden', 'logout': 'Cerrar Sesión', 'logout_confirm': '¿Estás seguro de que quieres cerrar sesión?',
        'unsaved_changes': 'Tienes cambios sin guardar. ¿Descartar?', 'file_too_large': 'El archivo es muy grande (máx. 5MB)',
        'invalid_username': 'Formato de usuario inválido', 'enter_current_password': 'Por favor ingresa la contraseña actual',
        'password_too_short': 'La contraseña debe tener al menos 8 caracteres', 'profile_updated': '¡Perfil actualizado!',
        'update_failed': 'Error al actualizar el perfil', 'network_error': 'Error de red. Por favor intenta de nuevo.',
        'invalid_token': 'Sesión inválida', 'wrong_password': 'La contraseña actual es incorrecta',
        'username_change_limit': 'El nombre de usuario solo se puede cambiar una vez al mes',
        'username_change_confirm': '¿Estás seguro de cambiar tu nombre de usuario? Solo puedes cambiarlo una vez al mes.',
        'username_lowercase_hint': 'Los nombres de usuario se convierten automáticamente a minúsculas',
        'email': 'Correo Electrónico',
        'delete_account': 'Eliminar Cuenta',
        'delete_confirm': '¿Estás seguro de eliminar tu cuenta? Esta acción no se puede deshacer.',
        'delete_final_confirm': 'Escribe ELIMINAR para confirmar la eliminación de cuenta:',
        'delete_cancelled': 'Eliminación de cuenta cancelada.',
        'account_deleted': 'Tu cuenta ha sido eliminada.',
        'delete_failed': 'Error al eliminar la cuenta.',
        # Terms & Conditions
        'terms_title': 'Términos de Servicio',
        'terms_scroll_to_read': 'Desplázate hacia abajo para leer todos los términos',
        'terms_read_complete': 'Términos leídos completamente',
        'terms_agree_all': 'Aceptar todo',
        'terms_agree_all_desc': 'Acepto todos los términos y condiciones',
        'terms_agree_terms': 'Términos de Servicio (Obligatorio)',
        'terms_agree_privacy': 'Política de Privacidad (Obligatorio)',
        'terms_agree_location': 'Recopilación de Ubicación (Obligatorio)',
        'terms_agree_marketing': 'Información de Marketing (Opcional)',
        'terms_required': 'Obligatorio',
        'terms_optional': 'Opcional',
        'terms_continue': 'Aceptar y Continuar',
        'terms_article1_title': 'Artículo 1 (Propósito)',
        'terms_article1_content': 'Estos Términos de Servicio tienen como objetivo definir los derechos, obligaciones y responsabilidades entre WOOPANG (en adelante "la Empresa") y los usuarios en el uso del servicio de plataforma social de realidad aumentada (AR) WOOPANG.',
        'terms_article2_title': 'Artículo 2 (Definición de Términos)',
        'terms_article2_content': '"Servicio" se refiere al servicio de redes sociales basado en realidad aumentada proporcionado por la Empresa. "Usuario" se refiere a un miembro que accede y utiliza el Servicio de acuerdo con estos Términos. "Contenido" se refiere a toda la información, incluyendo texto, fotos, videos y contenido AR creado o subido por los usuarios.',
        'terms_article3_title': 'Artículo 3 (Descripción del Servicio)',
        'terms_article3_content': 'WOOPANG es una plataforma social basada en AR que permite a los usuarios compartir su ubicación e interactuar con otros usuarios en tiempo real. El servicio permite colocar contenido virtual en el mundo real e interacción social con usuarios cercanos.',
        'terms_p2p_warning_title': 'Aviso Importante: Compartir Ubicación en Tiempo Real',
        'terms_p2p_warning_content': 'Este servicio utiliza tecnología P2P (peer-to-peer) para permitir interacciones en tiempo real con otros usuarios. Cuando la función P2P está habilitada, tu ubicación actual puede compartirse en tiempo real con otros usuarios cercanos. Ten en cuenta que tu información de ubicación puede ser visible para otros al usar esta función.',
        'terms_article4_title': 'Artículo 4 (Obligaciones del Usuario)',
        'terms_article4_content': 'Los usuarios no deben realizar las siguientes actividades:',
        'terms_article4_item1': 'Usar información falsa durante el registro o hacerse pasar por otros',
        'terms_article4_item2': 'Publicar contenido ilegal, dañino u ofensivo',
        'terms_article4_item3': 'Infringir los derechos de otros usuarios o terceros',
        'terms_article4_item4': 'Violar las leyes o estos Términos de Servicio',
        'terms_article5_title': 'Artículo 5 (Recopilación y Uso de Información de Ubicación)',
        'terms_article5_content': 'La Empresa recopila información de ubicación del usuario para la prestación del servicio. Esta información se utiliza para proporcionar funciones AR y conectar con usuarios cercanos. La información de ubicación puede compartirse con otros usuarios cuando la función P2P está habilitada.',
        'terms_article6_title': 'Artículo 6 (Protección de Privacidad)',
        'terms_article6_content': 'La Empresa recopila y utiliza información personal de acuerdo con su Política de Privacidad. Los usuarios pueden revisar su configuración de recopilación de datos y optar por no participar en ciertas recopilaciones de datos en cualquier momento a través de la configuración de la aplicación.',
        'terms_article7_title': 'Artículo 7 (Limitación de Responsabilidad)',
        'terms_article7_content': 'La Empresa no es responsable de los daños causados por el contenido generado por usuarios o las interacciones entre usuarios. Los usuarios son responsables de sus propias acciones y del contenido que crean en la plataforma.',
        'terms_article8_title': 'Artículo 8 (Ley Aplicable)',
        'terms_article8_content': 'Estos Términos se rigen por las leyes de la República de Corea. Cualquier disputa derivada del uso del Servicio estará sujeta a la jurisdicción exclusiva del Tribunal del Distrito Central de Seúl.',
        'company_name': 'Kwae Entertainment',
        'company_address': 'Seúl, República de Corea'
    }
}

def get_db_connection():
    return psycopg2.connect(database="ar_database", user="postgres", password=DB_PASSWORD, host="210.105.65.145", port="5432")

def validate_username(username):
    """
    Username 검증: 1~20글자, 영문/숫자/./_ 만 허용
    """
    if not username or len(username) > 20:
        return False
    # 공백만 있거나 빈 문자열 금지
    if not username.strip():
        return False
    # 영문, 숫자, ., _ 만 허용
    if not re.match(r'^[a-zA-Z0-9._]+$', username):
        return False
    return True

def normalize_username(username):
    """
    Username을 소문자로 정규화
    """
    if username:
        return username.lower().strip()
    return username

def get_country_from_ip(ip_address):
    """
    IP 주소로부터 국가 코드(ISO 3166-1 alpha-2) 가져오기
    무료 API 사용: ip-api.com
    """
    try:
        # localhost나 프라이빗 IP인 경우
        if ip_address in ['127.0.0.1', 'localhost', '::1'] or ip_address.startswith('192.168.') or ip_address.startswith('10.'):
            return 'KR'  # 기본값

        response = requests.get(f'http://ip-api.com/json/{ip_address}?fields=countryCode', timeout=3)
        if response.status_code == 200:
            data = response.json()
            return data.get('countryCode', 'XX')
    except Exception as e:
        print(f"[Auth] IP 국가 조회 오류: {e}")
    return 'XX'  # Unknown

def get_client_ip():
    """클라이언트 실제 IP 주소 가져오기 (프록시 고려)"""
    # X-Forwarded-For 헤더 확인 (프록시/로드밸런서 뒤에 있는 경우)
    if request.headers.get('X-Forwarded-For'):
        return request.headers.get('X-Forwarded-For').split(',')[0].strip()
    # X-Real-IP 헤더 확인
    if request.headers.get('X-Real-IP'):
        return request.headers.get('X-Real-IP')
    # 직접 연결인 경우
    return request.remote_addr

def send_styled_email(to_email, code, lang):
    t = TRANSLATIONS.get(lang, TRANSLATIONS['en'])
    try:
        msg = MIMEMultipart('alternative'); msg['Subject'] = t['mail_subject']; msg['From'] = f"WOOPANG <{SMTP_EMAIL}>"; msg['To'] = to_email
        html_content = f"""
        <html><body style="font-family: 'Poppins', sans-serif; background-color: #0a0a0a; color: #ffffff; padding: 40px; margin: 0;">
            <div style="max-width: 600px; margin: 0 auto; background-color: #1a1a1a; border: 1px solid #ffd70066; border-radius: 24px; overflow: hidden; box-shadow: 0 20px 50px rgba(0,0,0,0.5);">
                <div style="padding: 40px; text-align: center; border-bottom: 1px solid #ffd70022;">
                    <h1 style="margin: 0; font-size: 32px; font-weight: 800; background: linear-gradient(135deg, #FFD700 0%, #DAA520 100%); -webkit-background-clip: text; -webkit-text-fill-color: transparent; letter-spacing: 4px;">WOOPANG</h1>
                    <p style="color: #ffd700; font-size: 14px; margin-top: 10px; letter-spacing: 2px;">SEE THE UNSEEN</p>
                </div>
                <div style="padding: 40px;">
                    <h2 style="color: #ffffff; margin-bottom: 20px;">{t['mail_hi']}</h2>
                    <p style="color: #a0a0a0; line-height: 1.6; font-size: 16px;">{t['mail_body']}</p>
                    <div style="margin: 40px 0; padding: 30px; background: rgba(255,215,0,0.05); border-radius: 16px; border: 1px dashed #ffd70044; text-align: center;">
                        <div style="font-size: 48px; font-weight: 700; color: #FFD700; letter-spacing: 10px; margin-bottom: 20px;">{code}</div>
                        <p style="color: #ef4444; font-size: 13px; margin: 0;">{t['mail_expire']}</p>
                    </div>
                    <div style="text-align: center;"><p style="color: #a0a0a0; font-size: 14px; margin-bottom: 30px;">{t['mail_footer']}</p><div style="border-top: 1px solid #333; padding-top: 30px; color: #666; font-size: 12px;">© 2025 WOOPANG. Creating things that never existed.</div></div>
                </div>
            </div>
        </body></html>"""
        msg.attach(MIMEText(html_content, 'html')); server = smtplib.SMTP(SMTP_SERVER, SMTP_PORT); server.starttls(); server.login(SMTP_EMAIL, SMTP_PASSWORD)
        server.send_message(msg); server.quit()
        return True
    except Exception as e: print(f"SMTP Error: {e}"); return False

def get_locale():
    lang = request.args.get('lang') or session.get('lang')
    if lang and lang in TRANSLATIONS: return lang
    return request.accept_languages.best_match(TRANSLATIONS.keys()) or 'en'

@web_auth_bp.route('/investment')
def investment_page():
    """투자자 IR 페이지"""
    return render_template('investment.html')

@web_auth_bp.route('/terms')
def terms_page():
    """약관 동의 페이지"""
    lang = get_locale()
    session['lang'] = lang
    t = TRANSLATIONS.get(lang, TRANSLATIONS['en'])
    return render_template('terms.html', t=t, lang=lang)

@web_auth_bp.route('/register')
def register_page():
    """회원가입 페이지 (약관 동의 후)"""
    lang = get_locale()
    session['lang'] = lang
    t = TRANSLATIONS.get(lang, TRANSLATIONS['en'])

    # 약관 동의 여부 확인
    terms_agreed = request.args.get('terms_agreed') == 'true'
    if not terms_agreed:
        # 약관 동의하지 않았으면 약관 페이지로 리다이렉트
        return redirect(url_for('web_auth.terms_page', lang=lang))

    # 마케팅 동의 여부 세션에 저장
    session['marketing_agreed'] = request.args.get('marketing') == 'true'

    return render_template('login_v2.html', show_register=True, t=t, lang=lang)

@web_auth_bp.route('/login_view')
def login_view():
    lang = get_locale(); session['lang'] = lang
    t = TRANSLATIONS.get(lang, TRANSLATIONS['en'])
    redirect_url = request.args.get('redirect', '')
    return render_template('login_v2.html', kakao_key=KAKAO_REST_API_KEY, google_id=GOOGLE_CLIENT_ID, apple_id=APPLE_CLIENT_ID, t=t, lang=lang, redirect_url=redirect_url)

@web_auth_bp.route('/send_verification', methods=['POST'])
def send_verification():
    data = request.json; email = data.get('email'); lang = get_locale(); t = TRANSLATIONS[lang]
    if not email: return jsonify({'success': False, 'message': t['msg_email_required']})
    code = ''.join(random.choices(string.digits, k=6))
    auth_cache[email] = {'code': code, 'expiry': time.time() + 300, 'verified': False}
    if send_styled_email(email, code, lang): return jsonify({'success': True, 'message': t['msg_code_sent']})
    return jsonify({'success': False, 'message': t['msg_send_failed']})

@web_auth_bp.route('/verify_code', methods=['POST'])
def verify_code():
    data = request.json; email = data.get('email'); code = data.get('code'); lang = get_locale(); t = TRANSLATIONS[lang]
    entry = auth_cache.get(email)
    if entry and entry['code'] == code and time.time() < entry['expiry']:
        entry['verified'] = True; entry['verified_at'] = time.time()
        return jsonify({'success': True, 'message': t['msg_verified']})
    return jsonify({'success': False, 'message': t['msg_invalid_code']})

@web_auth_bp.route('/login_process', methods=['POST'])
def login_process():
    target = request.form.get('email', '').strip()
    password = request.form.get('password', '')
    lang = get_locale()
    t = TRANSLATIONS[lang]

    # 빈 값 체크 (새로고침으로 인한 빈 POST 방지)
    if not target or not password:
        print(f"[Auth] Login attempt with empty credentials - target: '{target}', password: '{bool(password)}'")
        return render_template('login_v2.html', t=t, lang=lang)  # 에러 메시지 없이 로그인 폼만 표시

    conn = None
    try:
        conn = get_db_connection()
        cur = conn.cursor()
        cur.execute("SELECT id, username, password, email FROM users WHERE (email = %s OR username = %s)", (target, target))
        user = cur.fetchone()

        print(f"[Auth] Login attempt for: {target}, User found: {user is not None}")

        if user and user[2]:
            try:
                password_match = bcrypt.checkpw(password.encode('utf-8'), user[2].encode('utf-8'))
            except Exception as bcrypt_err:
                print(f"[Auth] Bcrypt error: {bcrypt_err}, trying plain text match")
                password_match = (password == user[2])

            print(f"[Auth] Password match: {password_match}")

            if password_match:
                # JWT 토큰 생성
                auth_token = generate_auth_token(user[0], user[1], 'email')
                deep_link = f"woopang://auth?token={auth_token}"
                print(f"[Auth] Login SUCCESS for user: {user[1]}")

                # 웹 세션에도 저장 (이메일 서비스 등 웹 전용 기능용)
                session['user_id'] = str(user[0])
                session['username'] = user[1]
                session['auth_token'] = auth_token

                # redirect 파라미터가 있으면 해당 경로로 이동
                redirect_url = request.form.get('redirect') or request.args.get('redirect')
                if redirect_url and redirect_url.startswith('/'):
                    response = redirect(redirect_url)
                    response.set_cookie('auth_token', auth_token, httponly=True, secure=True, samesite='Lax', max_age=3600)
                    return response

                return render_template('login_v2.html', success_link=deep_link, t=t, lang=lang)
            else:
                print(f"[Auth] Password mismatch for user: {target}")
        else:
            print(f"[Auth] User not found or no password: {target}")

        return render_template('login_v2.html', error=t['msg_invalid_login'], t=t, lang=lang)
    except Exception as e:
        print(f"[Auth] Login error: {e}")
        return render_template('login_v2.html', error=t['msg_server_error'], t=t, lang=lang)
    finally:
        if conn: conn.close()

@web_auth_bp.route('/register_process', methods=['POST'])
def register_process():
    username = normalize_username(request.form.get('username', ''))
    email = request.form.get('email', '').strip()
    password = request.form.get('password', '')
    lang = get_locale()
    t = TRANSLATIONS[lang]

    print(f"[Auth] Register attempt - username: '{username}', email: '{email}'")

    entry = auth_cache.get(email)
    if not entry or not entry.get('verified'):
        print(f"[Auth] Email not verified: {email}, cache entry: {entry}")
        return render_template('login_v2.html', error=t.get('msg_verify_expired', 'Please verify your email first'), t=t, lang=lang)

    if not validate_username(username):
        print(f"[Auth] Invalid username: '{username}'")
        return render_template('login_v2.html', error=t['msg_invalid_username'], t=t, lang=lang)

    if len(password) < 8:
        print(f"[Auth] Password too short")
        return render_template('login_v2.html', error=t['msg_weak_password'], t=t, lang=lang)

    hashed_pw = bcrypt.hashpw(password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')
    conn = None
    try:
        conn = get_db_connection()
        cur = conn.cursor()

        # 이메일/사용자명 중복 체크
        cur.execute("SELECT email, username FROM users WHERE email = %s OR username = %s", (email, username))
        existing = cur.fetchone()
        if existing:
            if existing[0] == email:
                print(f"[Auth] Email already exists: {email}")
                return render_template('login_v2.html', error=t['msg_email_exists'], t=t, lang=lang)
            else:
                print(f"[Auth] Username already taken: {username}")
                return render_template('login_v2.html', error=t['msg_email_exists'].replace('이메일', '사용자명').replace('Email', 'Username'), t=t, lang=lang)

        # IP 기반 국가 코드 가져오기
        client_ip = get_client_ip()
        country_code = get_country_from_ip(client_ip)
        print(f"[Auth] Client IP: {client_ip}, Country: {country_code}")

        # id 컬럼은 SERIAL이므로 자동 생성되도록 제외
        cur.execute("INSERT INTO users (username, email, password, provider, country_code) VALUES (%s, %s, %s, 'email', %s) RETURNING id", (username, email, hashed_pw, country_code))
        uid = cur.fetchone()[0]
        conn.commit()

        print(f"[Auth] Registration SUCCESS - user_id: {uid}, username: {username}, country: {country_code}")

        # 인증 캐시 정리
        if email in auth_cache:
            del auth_cache[email]

        # JWT 토큰 생성
        auth_token = generate_auth_token(uid, username, 'email')
        return render_template('login_v2.html', success_link=f"woopang://auth?token={auth_token}", t=t, lang=lang)
    except Exception as e:
        print(f"[Auth] Registration error: {e}")
        return render_template('login_v2.html', error=t['msg_reg_failed'], t=t, lang=lang)
    finally:
        if conn: conn.close()

@web_auth_bp.route('/login/<provider>')
def social_login_redirect(provider):
    if provider == 'kakao':
        return redirect(f"https://kauth.kakao.com/oauth/authorize?client_id={KAKAO_REST_API_KEY}&redirect_uri={url_for('web_auth.social_callback', provider='kakao', _external=True)}&response_type=code")
    elif provider == 'google':
        scope = "https://www.googleapis.com/auth/userinfo.email https://www.googleapis.com/auth/userinfo.profile"
        return redirect(f"https://accounts.google.com/o/oauth2/v2/auth?client_id={GOOGLE_CLIENT_ID}&redirect_uri={url_for('web_auth.social_callback', provider='google', _external=True)}&response_type=code&scope={scope}")
    elif provider == 'apple':
        # Apple Sign In 리다이렉트
        redirect_uri = url_for('web_auth.apple_callback', _external=True)
        scope = "name email"
        state = str(uuid.uuid4())
        session['apple_state'] = state
        apple_auth_url = f"https://appleid.apple.com/auth/authorize?client_id={APPLE_CLIENT_ID}&redirect_uri={redirect_uri}&response_type=code id_token&response_mode=form_post&scope={scope}&state={state}"
        return redirect(apple_auth_url)
    return redirect(url_for('web_auth.login_view'))

@web_auth_bp.route('/login/<provider>/callback')
def social_callback(provider):
    code = request.args.get('code')
    conn = None
    try:
        email = None
        social_id = None

        if provider == 'kakao' and code:
            # 카카오 토큰 교환
            token_resp = requests.post('https://kauth.kakao.com/oauth/token', data={
                'grant_type': 'authorization_code',
                'client_id': KAKAO_REST_API_KEY,
                'redirect_uri': url_for('web_auth.social_callback', provider='kakao', _external=True),
                'code': code
            })
            token_data = token_resp.json()
            access_token = token_data.get('access_token')
            if access_token:
                user_resp = requests.get('https://kapi.kakao.com/v2/user/me', headers={'Authorization': f'Bearer {access_token}'})
                user_data = user_resp.json()
                social_id = str(user_data.get('id'))
                email = user_data.get('kakao_account', {}).get('email', f'kakao_{social_id}@kakao.local')

        elif provider == 'google' and code:
            # 구글 토큰 교환
            token_resp = requests.post('https://oauth2.googleapis.com/token', data={
                'grant_type': 'authorization_code',
                'client_id': GOOGLE_CLIENT_ID,
                'client_secret': os.getenv('GOOGLE_CLIENT_SECRET', ''),
                'redirect_uri': url_for('web_auth.social_callback', provider='google', _external=True),
                'code': code
            })
            token_data = token_resp.json()
            id_token_str = token_data.get('id_token')
            if id_token_str:
                decoded = jwt.decode(id_token_str, options={"verify_signature": False})
                social_id = decoded.get('sub')
                email = decoded.get('email', f'google_{social_id}@google.local')

        if not social_id:
            social_id = str(uuid.uuid4())
            email = f"social_{provider}_{random.randint(100,999)}@test.com"

        conn = get_db_connection(); cur = conn.cursor()
        cur.execute("SELECT id, username FROM users WHERE social_id = %s AND provider = %s", (social_id, provider))
        user = cur.fetchone()
        if user:
            auth_token = generate_auth_token(user[0], user[1], provider)
            return render_template('login_v2.html', success_link=f"woopang://auth?token={auth_token}", t=TRANSLATIONS[get_locale()], lang=get_locale())

        # 이메일로도 체크 (기존 회원 연동)
        if email:
            cur.execute("SELECT id, username FROM users WHERE email = %s", (email,))
            existing = cur.fetchone()
            if existing:
                cur.execute("UPDATE users SET social_id = %s, provider = %s WHERE id = %s", (social_id, provider, existing[0]))
                conn.commit()
                auth_token = generate_auth_token(existing[0], existing[1], provider)
                return render_template('login_v2.html', success_link=f"woopang://auth?token={auth_token}", t=TRANSLATIONS[get_locale()], lang=get_locale())

        session['social_reg'] = {'email': email, 'social_id': social_id, 'provider': provider}
        return render_template('login_v2.html', show_set_username=True, t=TRANSLATIONS[get_locale()], lang=get_locale())
    except Exception as e:
        print(f"[Auth] Social callback error: {e}")
        return redirect(url_for('web_auth.login_view'))
    finally:
        if conn: conn.close()

@web_auth_bp.route('/login/apple/callback', methods=['POST'])
def apple_callback():
    """Apple Sign In 콜백 (form_post 방식)"""
    conn = None
    try:
        # Apple은 form_post로 데이터 전송
        id_token_str = request.form.get('id_token')
        code = request.form.get('code')
        state = request.form.get('state')
        user_data = request.form.get('user')  # 첫 로그인시에만 제공

        print(f"[Auth] Apple callback received - code: {bool(code)}, id_token: {bool(id_token_str)}")

        if not id_token_str:
            print("[Auth] Apple: No id_token received")
            return redirect(url_for('web_auth.login_view'))

        # ID 토큰 디코드 (서명 검증 생략 - 프로덕션에서는 검증 권장)
        decoded = jwt.decode(id_token_str, options={"verify_signature": False})

        apple_id = decoded.get('sub')
        email = decoded.get('email')

        # 첫 로그인시 user 정보에서 이름 추출
        name = None
        if user_data:
            try:
                import json
                user_info = json.loads(user_data)
                name_info = user_info.get('name', {})
                first_name = name_info.get('firstName', '')
                last_name = name_info.get('lastName', '')
                name = f"{first_name} {last_name}".strip()
            except:
                pass

        print(f"[Auth] Apple user - id: {apple_id}, email: {email}, name: {name}")

        conn = get_db_connection(); cur = conn.cursor()

        # 기존 사용자 확인
        cur.execute("SELECT id, username FROM users WHERE social_id = %s AND provider = 'apple'", (apple_id,))
        user = cur.fetchone()

        if user:
            auth_token = generate_auth_token(user[0], user[1], 'apple')
            return render_template('login_v2.html', success_link=f"woopang://auth?token={auth_token}", t=TRANSLATIONS[get_locale()], lang=get_locale())

        # 이메일로 기존 계정 확인
        if email:
            cur.execute("SELECT id, username FROM users WHERE email = %s", (email,))
            existing = cur.fetchone()
            if existing:
                cur.execute("UPDATE users SET social_id = %s, provider = 'apple' WHERE id = %s", (apple_id, existing[0]))
                conn.commit()
                auth_token = generate_auth_token(existing[0], existing[1], 'apple')
                return render_template('login_v2.html', success_link=f"woopang://auth?token={auth_token}", t=TRANSLATIONS[get_locale()], lang=get_locale())

        # 신규 사용자 - 사용자명 입력 필요
        session['social_reg'] = {
            'email': email or f'apple_{apple_id}@apple.local',
            'social_id': apple_id,
            'provider': 'apple'
        }
        return render_template('login_v2.html', show_set_username=True, t=TRANSLATIONS[get_locale()], lang=get_locale())

    except Exception as e:
        print(f"[Auth] Apple callback error: {e}")
        import traceback
        traceback.print_exc()
        return redirect(url_for('web_auth.login_view'))
    finally:
        if conn: conn.close()

@web_auth_bp.route('/complete_social_reg', methods=['POST'])
def complete_social_reg():
    username = normalize_username(request.form.get('username', ''))
    reg_info = session.get('social_reg')
    lang = get_locale(); t = TRANSLATIONS[lang]
    if not reg_info or not validate_username(username): return render_template('login_v2.html', error=t['msg_invalid_username'], show_set_username=True, t=t, lang=lang)
    conn = None
    try:
        conn = get_db_connection(); cur = conn.cursor()

        # 사용자명 중복 체크
        cur.execute("SELECT id FROM users WHERE username = %s", (username,))
        if cur.fetchone():
            error_msg = t.get('msg_username_exists', 'Username already exists' if lang == 'en' else '이미 사용 중인 사용자명입니다')
            return render_template('login_v2.html', error=error_msg, show_set_username=True, t=t, lang=lang)

        # social_id로 이미 가입된 사용자인지 다시 확인 (동시 요청 방지)
        cur.execute("SELECT id, username FROM users WHERE social_id = %s AND provider = %s", (reg_info['social_id'], reg_info['provider']))
        existing_social = cur.fetchone()
        if existing_social:
            session.pop('social_reg', None)
            auth_token = generate_auth_token(existing_social[0], existing_social[1], reg_info['provider'])
            return render_template('login_v2.html', success_link=f"woopang://auth?token={auth_token}", t=t, lang=lang)

        # IP 기반 국가 코드 가져오기
        client_ip = get_client_ip()
        country_code = get_country_from_ip(client_ip)
        # id 컬럼은 SERIAL이므로 자동 생성
        cur.execute("INSERT INTO users (username, email, provider, social_id, country_code) VALUES (%s, %s, %s, %s, %s) RETURNING id", (username, reg_info['email'], reg_info['provider'], reg_info['social_id'], country_code))
        uid = cur.fetchone()[0]
        conn.commit(); session.pop('social_reg', None)
        print(f"[Auth] Social registration SUCCESS - user_id: {uid}, username: {username}, provider: {reg_info['provider']}, country: {country_code}")
        auth_token = generate_auth_token(uid, username, reg_info['provider'])
        return render_template('login_v2.html', success_link=f"woopang://auth?token={auth_token}", t=t, lang=lang)
    except Exception as e:
        print(f"[Auth] Social registration error: {e}")
        return render_template('login_v2.html', error=t['msg_reg_failed'], t=t, lang=lang)
    finally:
        if conn: conn.close()

@web_auth_bp.route('/api/auth/verify', methods=['POST'])
def verify_auth():
    """
    JWT 토큰 검증 API
    앱에서 딥링크로 받은 토큰을 서버에서 검증
    """
    data = request.json or {}
    token = data.get('token', '')

    if not token:
        return jsonify({'success': False, 'message': 'Token required'})

    # JWT 토큰 검증
    payload = verify_auth_token(token)
    if not payload:
        return jsonify({'success': False, 'message': 'Invalid or expired token'})

    user_id = payload['user_id']

    # DB에서 사용자 정보 조회
    conn = None
    try:
        conn = get_db_connection()
        cur = conn.cursor()
        cur.execute("""
            SELECT id, username, email, phone, avatar_url, bio,
                   instagram_id, facebook_id, x_id, provider
            FROM users WHERE id = %s
        """, (user_id,))
        user = cur.fetchone()

        if not user:
            return jsonify({'success': False, 'message': 'User not found'})

        return jsonify({
            'success': True,
            'data': {
                'id': user[0],
                'username': user[1],
                'email': user[2],
                'phone': user[3] or '',
                'avatar_url': user[4] or '',
                'bio': user[5] or '',
                'instagram_id': user[6] or '',
                'facebook_id': user[7] or '',
                'x_id': user[8] or '',
                'provider': user[9]
            }
        })
    except Exception as e:
        print(f"[Auth] Token verify error: {e}")
        return jsonify({'success': False, 'message': 'Server error'})
    finally:
        if conn: conn.close()

@web_auth_bp.route('/api/profile', methods=['GET'])
def get_profile():
    uid = request.args.get('user_id')
    if not uid: return jsonify({'success': False})
    conn = None
    try:
        conn = get_db_connection(); cur = conn.cursor()
        cur.execute("SELECT username, email, phone, instagram_id, facebook_id, x_id FROM users WHERE id = %s", (uid,))
        u = cur.fetchone()
        if u: return jsonify({'success': True, 'data': {'username': u[0], 'email': u[1], 'phone': u[2] or '', 'instagram_id': u[3] or '', 'facebook_id': u[4] or '', 'x_id': u[5] or ''}})
        return jsonify({'success': False})
    finally:
        if conn: conn.close()

@web_auth_bp.route('/api/profile/update', methods=['POST'])
def update_profile():
    d = request.json; uid = d.get('user_id')
    conn = None
    try:
        conn = get_db_connection(); cur = conn.cursor()
        cur.execute("UPDATE users SET phone=%s, instagram_id=%s, facebook_id=%s, x_id=%s WHERE id=%s", (d.get('phone'), d.get('instagram_id'), d.get('facebook_id'), d.get('x_id'), uid))
        conn.commit(); return jsonify({'success': True})
    finally:
        if conn: conn.close()

# ========== Profile Edit Page Routes ==========

# Token cache for profile edit sessions (user_id -> token)
profile_edit_tokens = {}

@web_auth_bp.route('/profile/edit')
def profile_edit_page():
    """
    Profile edit page - only accessible via app with valid token
    URL: /profile/edit?token=<user_id>&lang=<lang>
    """
    token = request.args.get('token')
    lang = request.args.get('lang') or get_locale()
    t = TRANSLATIONS.get(lang, TRANSLATIONS['en'])

    if not token:
        return "Access denied. Please open this page from the WOOPANG app.", 403

    conn = None
    try:
        conn = get_db_connection()
        cur = conn.cursor()

        # Get user data (including username_changed_at for rate limiting)
        cur.execute("""
            SELECT id, username, email, phone, bio, avatar_url, provider,
                   instagram_id, facebook_id, x_id,
                   likes_count, followers_count, following_count, username_changed_at
            FROM users WHERE id = %s
        """, (token,))
        user_row = cur.fetchone()

        if not user_row:
            return "User not found. Invalid session.", 404

        # Check if username can be changed (once per month)
        username_changed_at = user_row[13]
        can_change_username = True
        days_until_change = 0
        if username_changed_at:
            from datetime import datetime, timedelta
            if isinstance(username_changed_at, str):
                username_changed_at = datetime.fromisoformat(username_changed_at)
            days_since_change = (datetime.now() - username_changed_at).days
            can_change_username = days_since_change >= 30
            if not can_change_username:
                days_until_change = 30 - days_since_change

        # Create user dict
        user = {
            'id': user_row[0],
            'username': user_row[1],
            'email': user_row[2],
            'phone': user_row[3],
            'bio': user_row[4],
            'avatar_url': user_row[5],
            'provider': user_row[6],
            'instagram_id': user_row[7],
            'facebook_id': user_row[8],
            'x_id': user_row[9],
            'likes_count': user_row[10] or 0,
            'followers_count': user_row[11] or 0,
            'following_count': user_row[12] or 0,
            'can_change_username': can_change_username,
            'days_until_change': days_until_change
        }

        # Store token for API auth
        session_token = str(uuid.uuid4())
        profile_edit_tokens[session_token] = {'user_id': token, 'created': time.time()}

        return render_template('profile_edit.html', user=user, token=session_token, t=t, lang=lang)

    except Exception as e:
        print(f"Profile edit error: {e}")
        return "Server error", 500
    finally:
        if conn: conn.close()

@web_auth_bp.route('/api/profile/edit', methods=['POST'])
def update_profile_edit():
    """
    Profile update API with full support for:
    - Username change
    - Phone, Bio update
    - Social links update
    - Avatar upload
    - Password change (for email provider only)
    """
    lang = get_locale()
    t = TRANSLATIONS.get(lang, TRANSLATIONS['en'])

    # Get auth token from header
    auth_header = request.headers.get('Authorization', '')
    if not auth_header.startswith('Bearer '):
        return jsonify({'success': False, 'message': t['invalid_token']})

    token = auth_header.replace('Bearer ', '')
    token_data = profile_edit_tokens.get(token)

    if not token_data:
        return jsonify({'success': False, 'message': t['invalid_token']})

    # Check token expiry (1 hour)
    if time.time() - token_data['created'] > 3600:
        del profile_edit_tokens[token]
        return jsonify({'success': False, 'message': t['invalid_token']})

    user_id = token_data['user_id']

    # Get form data (can be multipart with avatar file)
    username = normalize_username(request.form.get('username', ''))
    phone = request.form.get('phone', '').strip()
    bio = request.form.get('bio', '').strip()
    instagram_id = request.form.get('instagram_id', '').strip()
    facebook_id = request.form.get('facebook_id', '').strip()
    x_id = request.form.get('x_id', '').strip()
    current_password = request.form.get('current_password', '')
    new_password = request.form.get('new_password', '')
    avatar_file = request.files.get('avatar')

    # Validate username
    if not validate_username(username):
        return jsonify({'success': False, 'message': t['invalid_username']})

    conn = None
    try:
        conn = get_db_connection()
        cur = conn.cursor()

        # DEBUG: 현재 사용자 정보 확인
        print(f"[DEBUG] Profile Update - user_id: {user_id}, type: {type(user_id)}")
        print(f"[DEBUG] Profile Update - username to save: '{username}'")

        # 먼저 현재 사용자의 정보를 가져옴
        cur.execute("SELECT id, username FROM users WHERE id = %s", (int(user_id),))
        current_user = cur.fetchone()
        print(f"[DEBUG] Current user from DB: {current_user}")

        if not current_user:
            return jsonify({'success': False, 'message': 'User not found'})

        current_db_username = current_user[1]
        print(f"[DEBUG] Current DB username: '{current_db_username}', New username: '{username}'")

        # Check if username is taken by another user (case-insensitive)
        # 단, 현재 사용자 본인의 사용자명은 제외
        cur.execute("SELECT id, username FROM users WHERE LOWER(username) = LOWER(%s) AND id != %s", (username, int(user_id)))
        existing_user = cur.fetchone()
        print(f"[DEBUG] Existing user with same username: {existing_user}")

        if existing_user:
            print(f"[DEBUG] Username '{username}' is taken by user id {existing_user[0]}")
            return jsonify({'success': False, 'message': t.get('msg_username_exists', 'Username already exists')})

        # Check if username is being changed and if rate limiting applies
        cur.execute("SELECT username, username_changed_at FROM users WHERE id = %s", (int(user_id),))
        old_data = cur.fetchone()
        old_username = old_data[0] if old_data else None
        username_changed_at = old_data[1] if old_data else None
        # Case-insensitive comparison for username change detection
        username_is_changing = old_username and old_username.lower() != username.lower()

        if username_is_changing and username_changed_at:
            from datetime import datetime, timedelta
            if isinstance(username_changed_at, str):
                username_changed_at = datetime.fromisoformat(username_changed_at)
            days_since_change = (datetime.now() - username_changed_at).days
            if days_since_change < 30:
                days_remaining = 30 - days_since_change
                # 다국어 메시지
                if lang == 'ko':
                    msg = f'{days_remaining}일 후에 사용자명을 변경할 수 있습니다.'
                elif lang == 'ja':
                    msg = f'{days_remaining}日後にユーザー名を変更できます。'
                elif lang == 'zh':
                    msg = f'{days_remaining}天后可以更改用户名。'
                elif lang == 'es':
                    msg = f'Puede cambiar el nombre de usuario en {days_remaining} días.'
                else:
                    msg = f'You can change your username in {days_remaining} days.'
                return jsonify({'success': False, 'message': msg})

        # Handle password change if requested
        if new_password:
            if len(new_password) < 8:
                return jsonify({'success': False, 'message': t['password_too_short']})

            # Verify current password
            cur.execute("SELECT password, provider FROM users WHERE id = %s", (user_id,))
            user_data = cur.fetchone()

            if user_data[1] != 'email':
                return jsonify({'success': False, 'message': 'Social login accounts cannot change password'})

            stored_hash = user_data[0]
            try:
                if not bcrypt.checkpw(current_password.encode('utf-8'), stored_hash.encode('utf-8')):
                    return jsonify({'success': False, 'message': t['wrong_password']})
            except:
                if current_password != stored_hash:
                    return jsonify({'success': False, 'message': t['wrong_password']})

            # Hash new password
            new_hash = bcrypt.hashpw(new_password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')
            cur.execute("UPDATE users SET password = %s WHERE id = %s", (new_hash, user_id))

        # Handle avatar upload
        avatar_url = None
        if avatar_file and avatar_file.filename:
            from PIL import Image
            import io

            # Read image
            img = Image.open(avatar_file)

            # Convert to RGB if necessary (for PNG with transparency)
            if img.mode in ('RGBA', 'LA', 'P'):
                background = Image.new('RGB', img.size, (255, 255, 255))
                if img.mode == 'P':
                    img = img.convert('RGBA')
                background.paste(img, mask=img.split()[-1] if img.mode == 'RGBA' else None)
                img = background

            # Create 200x200 centered crop
            width, height = img.size
            min_dim = min(width, height)

            # Center crop to square
            left = (width - min_dim) // 2
            top = (height - min_dim) // 2
            right = left + min_dim
            bottom = top + min_dim
            img = img.crop((left, top, right, bottom))

            # Resize to 200x200
            img = img.resize((200, 200), Image.Resampling.LANCZOS)

            # Save avatar file
            filename = f"avatar_{user_id}_{int(time.time())}.jpg"
            upload_dir = os.path.join(os.path.dirname(__file__), 'uploads', 'avatars')
            os.makedirs(upload_dir, exist_ok=True)
            filepath = os.path.join(upload_dir, filename)
            img.save(filepath, 'JPEG', quality=90)
            avatar_url = f"/uploads/avatars/{filename}"

            # [Added] Delete old avatar if exists
            try:
                cur.execute("SELECT avatar_url FROM users WHERE id = %s", (int(user_id),))
                old_avatar_row = cur.fetchone()
                if old_avatar_row and old_avatar_row[0] and old_avatar_row[0].startswith('/uploads/avatars/'):
                    old_path = os.path.join(os.path.dirname(__file__), old_avatar_row[0].lstrip('/'))
                    if os.path.exists(old_path):
                        os.remove(old_path)
                        print(f"[Auth] Deleted old avatar: {old_path}")
            except Exception as cleanup_err:
                print(f"[Auth] Failed to clean up old avatar: {cleanup_err}")

        # Build update query - include username_changed_at if username is changing
        from datetime import datetime
        if avatar_url:
            if username_is_changing:
                cur.execute("""
                    UPDATE users SET
                        username = %s, phone = %s, bio = %s,
                        instagram_id = %s, facebook_id = %s, x_id = %s,
                        avatar_url = %s, username_changed_at = %s
                    WHERE id = %s
                """, (username, phone, bio, instagram_id, facebook_id, x_id, avatar_url, datetime.now(), user_id))
            else:
                cur.execute("""
                    UPDATE users SET
                        username = %s, phone = %s, bio = %s,
                        instagram_id = %s, facebook_id = %s, x_id = %s,
                        avatar_url = %s
                    WHERE id = %s
                """, (username, phone, bio, instagram_id, facebook_id, x_id, avatar_url, user_id))
        else:
            if username_is_changing:
                cur.execute("""
                    UPDATE users SET
                        username = %s, phone = %s, bio = %s,
                        instagram_id = %s, facebook_id = %s, x_id = %s,
                        username_changed_at = %s
                    WHERE id = %s
                """, (username, phone, bio, instagram_id, facebook_id, x_id, datetime.now(), user_id))
            else:
                cur.execute("""
                    UPDATE users SET
                        username = %s, phone = %s, bio = %s,
                        instagram_id = %s, facebook_id = %s, x_id = %s
                    WHERE id = %s
                """, (username, phone, bio, instagram_id, facebook_id, x_id, user_id))

        conn.commit()
        return jsonify({'success': True, 'message': t['profile_updated'], 'avatar_url': avatar_url})

    except Exception as e:
        print(f"Profile update error: {e}")
        return jsonify({'success': False, 'message': t['update_failed']})
    finally:
        if conn: conn.close()

@web_auth_bp.route('/api/account/delete', methods=['DELETE'])
def delete_account():
    """
    회원탈퇴 API
    - 사용자 계정을 완전히 삭제 (soft delete 아님)
    - 관련 데이터도 함께 삭제
    """
    lang = get_locale()
    t = TRANSLATIONS.get(lang, TRANSLATIONS['en'])

    # Get auth token from header
    auth_header = request.headers.get('Authorization', '')
    if not auth_header.startswith('Bearer '):
        return jsonify({'success': False, 'message': t['invalid_token']})

    token = auth_header.replace('Bearer ', '')
    token_data = profile_edit_tokens.get(token)

    if not token_data:
        return jsonify({'success': False, 'message': t['invalid_token']})

    # Check token expiry (1 hour)
    if time.time() - token_data['created'] > 3600:
        del profile_edit_tokens[token]
        return jsonify({'success': False, 'message': t['invalid_token']})

    user_id = token_data['user_id']

    conn = None
    try:
        conn = get_db_connection()
        cur = conn.cursor()

        # 사용자 존재 여부 확인
        cur.execute("SELECT id, username FROM users WHERE id = %s", (user_id,))
        user = cur.fetchone()
        if not user:
            return jsonify({'success': False, 'message': 'User not found'})

        print(f"[Auth] Deleting account - user_id: {user_id}, username: {user[1]}")

        # 관련 데이터 삭제 (외래 키 종속성에 따라 순서 조정 필요)
        # 팔로우/팔로워 관계 삭제
        cur.execute("DELETE FROM user_follows WHERE follower_id = %s OR following_id = %s", (user_id, user_id))

        # 좋아요 삭제
        cur.execute("DELETE FROM user_likes WHERE user_id = %s OR liked_user_id = %s", (user_id, user_id))

        # 사용자가 작성한 콘텐츠 삭제 (있을 경우)
        # cur.execute("DELETE FROM posts WHERE user_id = %s", (user_id,))
        # cur.execute("DELETE FROM comments WHERE user_id = %s", (user_id,))

        # 아바타 파일 삭제 (있을 경우)
        cur.execute("SELECT avatar_url FROM users WHERE id = %s", (user_id,))
        avatar_row = cur.fetchone()
        if avatar_row and avatar_row[0] and avatar_row[0].startswith('/uploads/avatars/'):
            avatar_path = os.path.join(os.path.dirname(__file__), avatar_row[0].lstrip('/'))
            if os.path.exists(avatar_path):
                try:
                    os.remove(avatar_path)
                    print(f"[Auth] Deleted avatar file: {avatar_path}")
                except Exception as file_err:
                    print(f"[Auth] Failed to delete avatar file: {file_err}")

        # 최종적으로 사용자 삭제
        cur.execute("DELETE FROM users WHERE id = %s", (user_id,))
        conn.commit()

        # 토큰 삭제
        if token in profile_edit_tokens:
            del profile_edit_tokens[token]

        print(f"[Auth] Account deleted successfully - user_id: {user_id}")
        return jsonify({'success': True, 'message': t['account_deleted']})

    except Exception as e:
        print(f"[Auth] Delete account error: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({'success': False, 'message': t['delete_failed']})
    finally:
        if conn: conn.close()


# Clean up expired tokens periodically
def cleanup_expired_tokens():
    current_time = time.time()
    expired = [k for k, v in profile_edit_tokens.items() if current_time - v['created'] > 3600]
    for k in expired:
        del profile_edit_tokens[k]
