# WOOPANG 개발 가이드

## 프로젝트 개요

**WOOPANG**은 AR(증강현실) 기반 소셜 플랫폼입니다.
- P2P 기술을 활용한 실시간 사용자 상호작용
- OAuth 로그인 (Kakao, Google, Apple) 및 이메일 회원가입 지원
- 위치 기반 소셜 기능

**회사명**: 쾌엔터테인먼트 (Kwae Entertainment)

---

## 기술 스택

### 클라이언트 (Unity)
- **Unity 2022.3 LTS**
- **ARCore / ARFoundation** - AR 기능
- **Photon PUN2** - P2P 실시간 통신
- **UnityWebRequest** - 서버 API 통신

### 서버 (Python Flask)
- **Flask** - 웹 프레임워크
- **PostgreSQL** - 데이터베이스
- **psycopg2** - DB 연결
- **bcrypt** - 비밀번호 암호화
- **PyJWT** - JWT 토큰 인증
- **Pillow (PIL)** - 이미지 처리

---

## 주요 파일 구조

```
woopang/
├── Assets/
│   └── Scripts/
│       ├── Download/
│       │   └── LoginManager.cs      # 로그인 관리 (JWT 토큰, 딥링크 처리)
│       └── UI/
│           └── ProfileManager.cs    # 프로필 관리 (조회, 편집)
├── server/
│   ├── web_auth_routes.py          # 인증 라우트 (로그인, 회원가입, 프로필 편집)
│   ├── templates/
│   │   ├── login_v2.html           # 로그인/회원가입 페이지
│   │   ├── profile_edit.html       # 프로필 편집 페이지
│   │   └── terms.html              # 이용약관 페이지
│   └── home/
│       └── static/
│           └── images/             # SVG 아이콘 파일들
│               ├── instagram_icon.svg
│               ├── facebook_icon.svg
│               ├── x_icon.svg
│               └── default_avatar.svg
└── guide/
    └── WOOPANG_DEV_GUIDE.md        # 이 파일
```

---

## 인증 시스템

### JWT 토큰 흐름
1. 사용자가 로그인/회원가입 완료
2. 서버에서 JWT 토큰 생성 (1시간 만료)
3. 딥링크로 앱에 토큰 전달: `woopang://auth?token=<JWT>`
4. 앱에서 토큰 검증 API 호출: `POST /api/auth/verify`
5. 사용자 정보 반환 및 로그인 상태 유지

### 딥링크 스키마
```
woopang://auth?token=<JWT>     # 로그인 성공
woopang://logout               # 로그아웃
woopang://profile/close        # 프로필 편집 종료
```

---

## 다국어 지원 (i18n)

**지원 언어**: 영어(en), 한국어(ko), 일본어(ja), 중국어(zh), 스페인어(es)

### 번역 추가 방법
`web_auth_routes.py`의 `TRANSLATIONS` 딕셔너리에 각 언어별로 키-값 추가:

```python
TRANSLATIONS = {
    'en': {
        'new_key': 'English text',
        ...
    },
    'ko': {
        'new_key': '한국어 텍스트',
        ...
    },
    # ja, zh, es도 동일하게 추가
}
```

### 템플릿에서 사용
```html
{{ t.new_key }}
```

---

## 프로필 편집 기능

### URL
```
/profile/edit?token=<user_id>&lang=<lang>
```

### 기능 목록
- 프로필 사진 변경 (200x200 크롭)
- 사용자명 변경 (월 1회 제한)
- 전화번호 (국가코드 선택 포함)
- 자기소개
- 소셜 링크 (Instagram, Facebook, X)
- 비밀번호 변경 (이메일 가입 사용자만)
- 로그아웃
- 회원탈퇴

### 사용자명 규칙
- 1~20자
- 영문 소문자, 숫자, `.`, `_` 만 허용
- 자동 소문자 변환
- 월 1회만 변경 가능 (username_changed_at 컬럼 추적)

---

## 회원가입 약관

### 약관 페이지 URL
```
/terms?lang=<lang>
```

### 동의 항목
1. **이용약관 동의** (필수)
2. **개인정보 처리방침 동의** (필수)
3. **위치정보 수집 동의** (필수)
4. **마케팅 정보 수신 동의** (선택)

### P2P 위치 노출 경고
약관에 P2P 기능 사용 시 실시간 위치가 다른 사용자에게 노출될 수 있음을 명시

---

## API 엔드포인트

### 인증
| 메서드 | 경로 | 설명 |
|--------|------|------|
| GET | `/login_view` | 로그인 페이지 |
| GET | `/terms` | 약관 동의 페이지 |
| GET | `/register` | 회원가입 페이지 |
| POST | `/send_verification` | 이메일 인증코드 발송 |
| POST | `/verify_code` | 인증코드 확인 |
| POST | `/login_process` | 로그인 처리 |
| POST | `/register_process` | 회원가입 처리 |
| GET | `/login/<provider>` | 소셜 로그인 시작 |
| GET/POST | `/login/<provider>/callback` | 소셜 로그인 콜백 |
| POST | `/api/auth/verify` | JWT 토큰 검증 |

### 프로필
| 메서드 | 경로 | 설명 |
|--------|------|------|
| GET | `/profile/edit` | 프로필 편집 페이지 |
| POST | `/api/profile/edit` | 프로필 업데이트 |
| DELETE | `/api/account/delete` | 회원탈퇴 |

---

## 데이터베이스 스키마 (users 테이블)

```sql
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(20) UNIQUE NOT NULL,
    email VARCHAR(255) UNIQUE,
    password VARCHAR(255),
    phone VARCHAR(20),
    bio TEXT,
    avatar_url VARCHAR(255),
    provider VARCHAR(20),  -- 'email', 'kakao', 'google', 'apple'
    social_id VARCHAR(255),
    instagram_id VARCHAR(100),
    facebook_id VARCHAR(100),
    x_id VARCHAR(100),
    country_code VARCHAR(10),
    likes_count INT DEFAULT 0,
    followers_count INT DEFAULT 0,
    following_count INT DEFAULT 0,
    username_changed_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW()
);
```

---

## 환경 변수 (.env)

```env
DB_PASSWORD=<PostgreSQL 비밀번호>
SMTP_SERVER=smtp.gmail.com
SMTP_PORT=587
SMTP_EMAIL=<발신 이메일>
SMTP_PASSWORD=<앱 비밀번호>
JWT_SECRET=<JWT 시크릿 키>
KAKAO_REST_API_KEY=<카카오 REST API 키>
GOOGLE_CLIENT_ID=<Google OAuth 클라이언트 ID>
GOOGLE_CLIENT_SECRET=<Google OAuth 시크릿>
APPLE_CLIENT_ID=<Apple 서비스 ID>
APPLE_TEAM_ID=<Apple 팀 ID>
APPLE_KEY_ID=<Apple 키 ID>
APPLE_PRIVATE_KEY=<Apple 개인 키>
```

---

## 주의사항

### 보안
- 비밀번호는 반드시 bcrypt로 해시
- JWT 시크릿은 프로덕션에서 강력한 랜덤 문자열 사용
- 프로필 편집 토큰은 1시간 후 만료

### 이미지 처리
- 아바타는 200x200 정사각형으로 자동 크롭
- 최대 5MB 제한
- JPEG 형식으로 저장

### Static 파일 경로
- Flask Blueprint의 static_folder: `home/static`
- 아이콘 경로: `/static/images/<icon>.svg`

---

## 변경 이력

### 2025.01.13
- 프로필 편집 페이지 개선
  - 전화번호 국가코드 선택 필드 추가 (40+ 국가)
  - 소셜 아이콘 추가 (Instagram, Facebook, X)
  - 기본 아바타 SVG 추가
  - 플레이스홀더 폰트 통일
- 회원탈퇴 기능 추가
- 이용약관 페이지 추가 (P2P 위치 노출 경고 포함)
- ProfileManager.cs 최적화 (프로필 편집 후에만 새로고침)
- 사용자명 소문자 자동 변환
