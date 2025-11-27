# Comment System Database Schema & API Design

## 📋 Overview
AR 오브젝트에 대한 익명 댓글 시스템
- 익명 댓글 작성 (아이디 없이)
- 좋아요 기능
- 정렬: 최신순 / 인기순 (좋아요)
- Instagram 스타일 UI

---

## 🗄️ Database Schema (PostgreSQL)

### 1. `ar_object_comments` 테이블
```sql
CREATE TABLE IF NOT EXISTS ar_object_comments (
    id BIGSERIAL PRIMARY KEY,
    place_id INTEGER NOT NULL,                -- AR 오브젝트 ID (DataManager의 placeId)
    content_id VARCHAR(50),                   -- TourAPI contentId (공공데이터용, nullable)
    comment_text TEXT NOT NULL,               -- 댓글 내용
    like_count INTEGER DEFAULT 0,             -- 좋아요 수
    is_anonymous BOOLEAN DEFAULT true,        -- 익명 여부 (현재는 모두 true)
    device_id VARCHAR(255),                   -- 디바이스 식별자 (추후 중복 방지용)
    created_at TIMESTAMP DEFAULT NOW(),       -- 생성 시간
    updated_at TIMESTAMP DEFAULT NOW(),       -- 수정 시간
    is_deleted BOOLEAN DEFAULT false          -- 소프트 삭제 플래그
);

-- 인덱스
CREATE INDEX idx_comments_place_id ON ar_object_comments(place_id);
CREATE INDEX idx_comments_content_id ON ar_object_comments(content_id);
CREATE INDEX idx_comments_created_at ON ar_object_comments(created_at DESC);
CREATE INDEX idx_comments_like_count ON ar_object_comments(like_count DESC);
CREATE INDEX idx_comments_not_deleted ON ar_object_comments(is_deleted) WHERE is_deleted = false;

-- 복합 인덱스 (place_id + 정렬용)
CREATE INDEX idx_comments_place_latest ON ar_object_comments(place_id, created_at DESC) WHERE is_deleted = false;
CREATE INDEX idx_comments_place_popular ON ar_object_comments(place_id, like_count DESC) WHERE is_deleted = false;
```

### 2. `ar_comment_likes` 테이블 (좋아요 중복 방지용)
```sql
CREATE TABLE IF NOT EXISTS ar_comment_likes (
    id BIGSERIAL PRIMARY KEY,
    comment_id BIGINT NOT NULL REFERENCES ar_object_comments(id) ON DELETE CASCADE,
    device_id VARCHAR(255) NOT NULL,          -- 디바이스 식별자
    created_at TIMESTAMP DEFAULT NOW(),
    UNIQUE(comment_id, device_id)            -- 같은 디바이스는 하나의 댓글에 1번만 좋아요
);

-- 인덱스
CREATE INDEX idx_comment_likes_comment_id ON ar_comment_likes(comment_id);
CREATE INDEX idx_comment_likes_device_id ON ar_comment_likes(device_id);
```

---

## 🔌 REST API Endpoints

### 1. 댓글 목록 조회 (GET)
```
GET /api/comments?place_id={placeId}&sort={latest|popular}&page={page}&limit={limit}

또는

GET /api/comments?content_id={contentId}&sort={latest|popular}&page={page}&limit={limit}
```

**Query Parameters:**
- `place_id` (optional): 우팡 데이터 장소 ID
- `content_id` (optional): TourAPI 공공데이터 contentId
- `sort` (optional, default: "latest"): 정렬 방식
  - `latest`: 최신순
  - `popular`: 인기순 (좋아요 많은 순)
- `page` (optional, default: 1): 페이지 번호
- `limit` (optional, default: 20): 페이지당 댓글 수

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "comments": [
      {
        "id": 123,
        "place_id": 45,
        "content_id": null,
        "comment_text": "여기 정말 좋아요!",
        "like_count": 15,
        "is_liked_by_me": false,
        "created_at": "2025-11-27T12:34:56Z",
        "updated_at": "2025-11-27T12:34:56Z"
      },
      // ... more comments
    ],
    "pagination": {
      "current_page": 1,
      "total_pages": 5,
      "total_comments": 98,
      "has_more": true
    }
  }
}
```

**Error Response (400 Bad Request):**
```json
{
  "success": false,
  "error": "place_id 또는 content_id가 필요합니다"
}
```

---

### 2. 댓글 작성 (POST)
```
POST /api/comments
Content-Type: application/json
```

**Request Body:**
```json
{
  "place_id": 45,              // 우팡 데이터용 (optional)
  "content_id": "123456",      // TourAPI용 (optional)
  "comment_text": "여기 정말 좋아요!",
  "device_id": "unique-device-identifier"
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "comment": {
      "id": 124,
      "place_id": 45,
      "content_id": null,
      "comment_text": "여기 정말 좋아요!",
      "like_count": 0,
      "is_liked_by_me": false,
      "created_at": "2025-11-27T12:35:00Z"
    }
  }
}
```

**Error Response (400 Bad Request):**
```json
{
  "success": false,
  "error": "댓글 내용이 비어있습니다"
}
```

---

### 3. 댓글 좋아요 토글 (POST)
```
POST /api/comments/{comment_id}/like
Content-Type: application/json
```

**Request Body:**
```json
{
  "device_id": "unique-device-identifier"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "comment_id": 124,
    "is_liked": true,           // 좋아요 추가됨
    "like_count": 16            // 새로운 좋아요 수
  }
}
```

**Response (좋아요 취소 시):**
```json
{
  "success": true,
  "data": {
    "comment_id": 124,
    "is_liked": false,          // 좋아요 취소됨
    "like_count": 15            // 새로운 좋아요 수
  }
}
```

---

### 4. 댓글 삭제 (DELETE)
```
DELETE /api/comments/{comment_id}
Content-Type: application/json
```

**Request Body:**
```json
{
  "device_id": "unique-device-identifier"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "댓글이 삭제되었습니다"
}
```

**Error Response (403 Forbidden):**
```json
{
  "success": false,
  "error": "본인이 작성한 댓글만 삭제할 수 있습니다"
}
```

---

### 5. 인기 댓글 조회 (GET)
```
GET /api/comments/top?place_id={placeId}

또는

GET /api/comments/top?content_id={contentId}
```

**Query Parameters:**
- `place_id` (optional): 우팡 데이터 장소 ID
- `content_id` (optional): TourAPI 공공데이터 contentId

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "top_comment": {
      "id": 123,
      "place_id": 45,
      "comment_text": "여기 정말 좋아요!",
      "like_count": 15,
      "is_liked_by_me": false,
      "created_at": "2025-11-27T12:34:56Z"
    }
  }
}
```

**Response (댓글 없을 때):**
```json
{
  "success": true,
  "data": {
    "top_comment": null
  }
}
```

---

## 📱 Unity C# Data Models

### CommentData.cs
```csharp
using System;

[Serializable]
public class CommentData
{
    public long id;
    public int? place_id;
    public string content_id;
    public string comment_text;
    public int like_count;
    public bool is_liked_by_me;
    public string created_at;
    public string updated_at;
}

[Serializable]
public class CommentListResponse
{
    public bool success;
    public CommentListData data;
    public string error;
}

[Serializable]
public class CommentListData
{
    public CommentData[] comments;
    public PaginationData pagination;
}

[Serializable]
public class PaginationData
{
    public int current_page;
    public int total_pages;
    public int total_comments;
    public bool has_more;
}

[Serializable]
public class CreateCommentRequest
{
    public int? place_id;
    public string content_id;
    public string comment_text;
    public string device_id;
}

[Serializable]
public class CreateCommentResponse
{
    public bool success;
    public CreateCommentData data;
    public string error;
}

[Serializable]
public class CreateCommentData
{
    public CommentData comment;
}

[Serializable]
public class LikeToggleRequest
{
    public string device_id;
}

[Serializable]
public class LikeToggleResponse
{
    public bool success;
    public LikeToggleData data;
    public string error;
}

[Serializable]
public class LikeToggleData
{
    public long comment_id;
    public bool is_liked;
    public int like_count;
}

[Serializable]
public class TopCommentResponse
{
    public bool success;
    public TopCommentData data;
    public string error;
}

[Serializable]
public class TopCommentData
{
    public CommentData top_comment;
}
```

---

## 🔐 Device ID 생성 방법 (Unity)

```csharp
using UnityEngine;

public static class DeviceIDHelper
{
    private const string DEVICE_ID_KEY = "woopang_device_id";

    public static string GetDeviceID()
    {
        // PlayerPrefs에 저장된 디바이스 ID 확인
        if (PlayerPrefs.HasKey(DEVICE_ID_KEY))
        {
            return PlayerPrefs.GetString(DEVICE_ID_KEY);
        }

        // 없으면 생성
        string deviceId = GenerateDeviceID();
        PlayerPrefs.SetString(DEVICE_ID_KEY, deviceId);
        PlayerPrefs.Save();

        return deviceId;
    }

    private static string GenerateDeviceID()
    {
        // SystemInfo를 조합하여 고유 ID 생성
        string uniqueString = $"{SystemInfo.deviceUniqueIdentifier}_{Application.version}_{SystemInfo.deviceModel}";

        // MD5 해시로 변환
        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(uniqueString);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
```

---

## 💡 구현 노트

### 1. place_id vs content_id
- `place_id`: 우팡 서버 데이터 (DataManager)
- `content_id`: 공공데이터 TourAPI (TourAPIManager)
- 둘 중 하나만 사용 (NOT NULL 제약 없음)
- API 호출 시 하나만 전달

### 2. 익명 댓글 시스템
- 현재는 user_id 없이 익명으로만 운영
- `device_id`로 작성자 본인 확인 (삭제, 좋아요 중복 방지)
- 추후 로그인 시스템 추가 시 `user_id` 컬럼 활용 가능

### 3. 좋아요 중복 방지
- `ar_comment_likes` 테이블의 UNIQUE 제약으로 처리
- 같은 `device_id`는 같은 댓글에 1번만 좋아요 가능
- 좋아요 토글 시 존재하면 삭제, 없으면 생성

### 4. 소프트 삭제
- `is_deleted` 플래그로 댓글 숨김 처리
- 실제 데이터는 유지 (통계, 감사 목적)
- 조회 시 `WHERE is_deleted = false` 조건 필수

### 5. 페이지네이션
- 기본 20개씩 로딩
- 무한 스크롤 구현 시 `page` 증가
- `has_more` 플래그로 추가 로딩 여부 판단

### 6. 인기 댓글 (Top Comment)
- 좋아요 가장 많은 댓글 1개
- 하단 패널에 기본 표시
- 좋아요 0인 경우 최신 댓글 표시 (선택사항)

---

## 🚀 Flask API 구현 예시 (app_improved.py에 추가)

```python
# 댓글 목록 조회
@app.route('/api/comments', methods=['GET'])
def get_comments():
    place_id = request.args.get('place_id', type=int)
    content_id = request.args.get('content_id')
    sort = request.args.get('sort', 'latest')  # latest or popular
    page = request.args.get('page', 1, type=int)
    limit = request.args.get('limit', 20, type=int)
    device_id = request.args.get('device_id')  # 좋아요 여부 확인용

    if not place_id and not content_id:
        return jsonify({'success': False, 'error': 'place_id 또는 content_id가 필요합니다'}), 400

    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        # 정렬 방식
        order_by = "created_at DESC" if sort == "latest" else "like_count DESC, created_at DESC"
        offset = (page - 1) * limit

        # 조건
        where_clause = "place_id = %s" if place_id else "content_id = %s"
        where_value = place_id if place_id else content_id

        # 댓글 조회
        query = f"""
            SELECT id, place_id, content_id, comment_text, like_count, created_at, updated_at
            FROM ar_object_comments
            WHERE {where_clause} AND is_deleted = false
            ORDER BY {order_by}
            LIMIT %s OFFSET %s
        """
        cursor.execute(query, (where_value, limit, offset))
        comments = cursor.fetchall()

        # 전체 개수 조회
        count_query = f"""
            SELECT COUNT(*) FROM ar_object_comments
            WHERE {where_clause} AND is_deleted = false
        """
        cursor.execute(count_query, (where_value,))
        total_count = cursor.fetchone()[0]

        # 좋아요 여부 확인 (device_id 있을 경우)
        comments_data = []
        for comment in comments:
            is_liked = False
            if device_id:
                cursor.execute("""
                    SELECT EXISTS(
                        SELECT 1 FROM ar_comment_likes
                        WHERE comment_id = %s AND device_id = %s
                    )
                """, (comment[0], device_id))
                is_liked = cursor.fetchone()[0]

            comments_data.append({
                'id': comment[0],
                'place_id': comment[1],
                'content_id': comment[2],
                'comment_text': comment[3],
                'like_count': comment[4],
                'is_liked_by_me': is_liked,
                'created_at': comment[5].isoformat(),
                'updated_at': comment[6].isoformat()
            })

        total_pages = math.ceil(total_count / limit)

        cursor.close()
        conn.close()

        return jsonify({
            'success': True,
            'data': {
                'comments': comments_data,
                'pagination': {
                    'current_page': page,
                    'total_pages': total_pages,
                    'total_comments': total_count,
                    'has_more': page < total_pages
                }
            }
        })

    except Exception as e:
        safe_print(f"[Error] 댓글 조회 실패: {e}")
        return jsonify({'success': False, 'error': str(e)}), 500

# 댓글 작성
@app.route('/api/comments', methods=['POST'])
def create_comment():
    data = request.get_json()
    place_id = data.get('place_id')
    content_id = data.get('content_id')
    comment_text = data.get('comment_text', '').strip()
    device_id = data.get('device_id')

    if not place_id and not content_id:
        return jsonify({'success': False, 'error': 'place_id 또는 content_id가 필요합니다'}), 400

    if not comment_text:
        return jsonify({'success': False, 'error': '댓글 내용이 비어있습니다'}), 400

    if not device_id:
        return jsonify({'success': False, 'error': 'device_id가 필요합니다'}), 400

    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        cursor.execute("""
            INSERT INTO ar_object_comments (place_id, content_id, comment_text, device_id)
            VALUES (%s, %s, %s, %s)
            RETURNING id, place_id, content_id, comment_text, like_count, created_at
        """, (place_id, content_id, comment_text, device_id))

        comment = cursor.fetchone()
        conn.commit()
        cursor.close()
        conn.close()

        return jsonify({
            'success': True,
            'data': {
                'comment': {
                    'id': comment[0],
                    'place_id': comment[1],
                    'content_id': comment[2],
                    'comment_text': comment[3],
                    'like_count': comment[4],
                    'is_liked_by_me': False,
                    'created_at': comment[5].isoformat()
                }
            }
        }), 201

    except Exception as e:
        safe_print(f"[Error] 댓글 작성 실패: {e}")
        return jsonify({'success': False, 'error': str(e)}), 500

# 댓글 좋아요 토글
@app.route('/api/comments/<int:comment_id>/like', methods=['POST'])
def toggle_like(comment_id):
    data = request.get_json()
    device_id = data.get('device_id')

    if not device_id:
        return jsonify({'success': False, 'error': 'device_id가 필요합니다'}), 400

    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        # 좋아요 존재 여부 확인
        cursor.execute("""
            SELECT id FROM ar_comment_likes
            WHERE comment_id = %s AND device_id = %s
        """, (comment_id, device_id))
        existing_like = cursor.fetchone()

        if existing_like:
            # 좋아요 취소
            cursor.execute("""
                DELETE FROM ar_comment_likes
                WHERE comment_id = %s AND device_id = %s
            """, (comment_id, device_id))

            cursor.execute("""
                UPDATE ar_object_comments
                SET like_count = like_count - 1
                WHERE id = %s
                RETURNING like_count
            """, (comment_id,))
            is_liked = False
        else:
            # 좋아요 추가
            cursor.execute("""
                INSERT INTO ar_comment_likes (comment_id, device_id)
                VALUES (%s, %s)
            """, (comment_id, device_id))

            cursor.execute("""
                UPDATE ar_object_comments
                SET like_count = like_count + 1
                WHERE id = %s
                RETURNING like_count
            """, (comment_id,))
            is_liked = True

        new_like_count = cursor.fetchone()[0]
        conn.commit()
        cursor.close()
        conn.close()

        return jsonify({
            'success': True,
            'data': {
                'comment_id': comment_id,
                'is_liked': is_liked,
                'like_count': new_like_count
            }
        })

    except Exception as e:
        safe_print(f"[Error] 좋아요 토글 실패: {e}")
        return jsonify({'success': False, 'error': str(e)}), 500

# 인기 댓글 조회
@app.route('/api/comments/top', methods=['GET'])
def get_top_comment():
    place_id = request.args.get('place_id', type=int)
    content_id = request.args.get('content_id')
    device_id = request.args.get('device_id')

    if not place_id and not content_id:
        return jsonify({'success': False, 'error': 'place_id 또는 content_id가 필요합니다'}), 400

    try:
        conn = get_db_connection()
        cursor = conn.cursor()

        where_clause = "place_id = %s" if place_id else "content_id = %s"
        where_value = place_id if place_id else content_id

        cursor.execute(f"""
            SELECT id, place_id, content_id, comment_text, like_count, created_at, updated_at
            FROM ar_object_comments
            WHERE {where_clause} AND is_deleted = false
            ORDER BY like_count DESC, created_at DESC
            LIMIT 1
        """, (where_value,))

        comment = cursor.fetchone()

        if not comment:
            cursor.close()
            conn.close()
            return jsonify({
                'success': True,
                'data': {'top_comment': None}
            })

        # 좋아요 여부 확인
        is_liked = False
        if device_id:
            cursor.execute("""
                SELECT EXISTS(
                    SELECT 1 FROM ar_comment_likes
                    WHERE comment_id = %s AND device_id = %s
                )
            """, (comment[0], device_id))
            is_liked = cursor.fetchone()[0]

        cursor.close()
        conn.close()

        return jsonify({
            'success': True,
            'data': {
                'top_comment': {
                    'id': comment[0],
                    'place_id': comment[1],
                    'content_id': comment[2],
                    'comment_text': comment[3],
                    'like_count': comment[4],
                    'is_liked_by_me': is_liked,
                    'created_at': comment[5].isoformat(),
                    'updated_at': comment[6].isoformat()
                }
            }
        })

    except Exception as e:
        safe_print(f"[Error] 인기 댓글 조회 실패: {e}")
        return jsonify({'success': False, 'error': str(e)}), 500
```

---

## ✅ Summary

### Database Tables:
1. **ar_object_comments** - 댓글 본문, 좋아요 수, 작성 시간
2. **ar_comment_likes** - 좋아요 중복 방지 (device_id 기반)

### API Endpoints:
1. **GET /api/comments** - 댓글 목록 (최신순/인기순, 페이지네이션)
2. **POST /api/comments** - 댓글 작성
3. **POST /api/comments/:id/like** - 좋아요 토글
4. **DELETE /api/comments/:id** - 댓글 삭제
5. **GET /api/comments/top** - 인기 댓글 1개

### Key Features:
- 익명 댓글 시스템 (device_id 기반)
- 좋아요 중복 방지 (UNIQUE 제약)
- 소프트 삭제 (is_deleted 플래그)
- 최신순/인기순 정렬
- 페이지네이션 (무한 스크롤 지원)
- 인기 댓글 (하단 패널용)

---

**Created:** 2025-11-27
**Author:** Claude Code (Anthropic)
