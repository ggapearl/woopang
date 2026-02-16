"""
WOOPANG P2P User Tracking Server
실시간 사용자 위치 추적 및 동기화 WebSocket 서버

Author: Claude (Anthropic AI)
Date: 2026-01-01
"""

from flask import Flask, request
from flask_socketio import SocketIO, emit, join_room, leave_room
from flask_cors import CORS
import psycopg2
from psycopg2.extras import RealDictCursor
import math
from datetime import datetime, timedelta
from threading import Timer
import logging
import os
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

# Logging setup
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = Flask(__name__)
app.config['SECRET_KEY'] = os.getenv('P2P_SECRET_KEY', 'woopang_p2p_secret_change_this')
CORS(app, resources={r"/*": {"origins": "*"}})
socketio = SocketIO(
    app,
    cors_allowed_origins="*",
    async_mode='threading',
    ping_timeout=60,
    ping_interval=25
)

# Database configuration (from environment variables)
DB_CONFIG = {
    'host': os.getenv('DB_HOST', '210.105.65.145'),
    'port': int(os.getenv('DB_PORT', 5432)),
    'database': 'ar_database',
    'user': 'postgres',
    'password': os.getenv('DB_PASSWORD', '')
}

def get_db():
    """Get database connection with RealDictCursor"""
    try:
        return psycopg2.connect(**DB_CONFIG, cursor_factory=RealDictCursor)
    except Exception as e:
        logger.error(f"Database connection error: {e}")
        return None

def calculate_distance(lat1, lon1, lat2, lon2):
    """
    Haversine formula for distance calculation
    Returns distance in meters
    Same implementation as Unity DataManager.cs
    """
    R = 6371000  # Earth radius in meters
    d_lat = math.radians(lat2 - lat1)
    d_lon = math.radians(lon2 - lon1)

    a = (math.sin(d_lat / 2) ** 2 +
         math.cos(math.radians(lat1)) * math.cos(math.radians(lat2)) *
         math.sin(d_lon / 2) ** 2)
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))

    return R * c

# ===== WebSocket Event Handlers =====

@socketio.on('connect')
def handle_connect():
    """Client connected"""
    logger.info(f"[CONNECT] Client connected: {request.sid}")
    emit('connection_response', {
        'status': 'connected',
        'socket_id': request.sid,
        'server_time': datetime.now().isoformat()
    })

@socketio.on('disconnect')
def handle_disconnect():
    """Client disconnected - mark user as offline"""
    logger.info(f"[DISCONNECT] Client disconnected: {request.sid}")

    conn = get_db()
    if conn:
        try:
            cur = conn.cursor()
            cur.execute("""
                UPDATE user_sessions
                SET status = 'offline', socket_id = NULL
                WHERE socket_id = %s
                RETURNING user_id, username
            """, (request.sid,))

            result = cur.fetchone()
            if result:
                logger.info(f"[DISCONNECT] User {result['username']} ({result['user_id']}) went offline")

            conn.commit()
            cur.close()
        except Exception as e:
            logger.error(f"[DISCONNECT] Error: {e}")
            conn.rollback()
        finally:
            conn.close()

@socketio.on('register_user')
def handle_register_user(data):
    """
    Register user session
    Data: {user_id, username, avatar_url, bio}
    """
    user_id = data.get('user_id')
    username = data.get('username')
    avatar_url = data.get('avatar_url', '')
    bio = data.get('bio', '')

    logger.info(f"[REGISTER] User {username} ({user_id}) registering...")

    conn = get_db()
    if not conn:
        emit('register_response', {'status': 'error', 'message': 'Database connection failed'})
        return

    try:
        cur = conn.cursor()

        # Upsert user session
        cur.execute("""
            INSERT INTO user_sessions (
                user_id, username, socket_id, avatar_url, bio,
                latitude, longitude, altitude, status, last_heartbeat
            )
            VALUES (%s, %s, %s, %s, %s, 0, 0, 0, 'active', NOW())
            ON CONFLICT (user_id)
            DO UPDATE SET
                socket_id = EXCLUDED.socket_id,
                avatar_url = EXCLUDED.avatar_url,
                bio = EXCLUDED.bio,
                status = 'active',
                last_heartbeat = NOW()
            RETURNING id
        """, (user_id, username, request.sid, avatar_url, bio))

        result = cur.fetchone()
        conn.commit()

        emit('register_response', {
            'status': 'success',
            'user_id': user_id,
            'session_id': result['id']
        })

        logger.info(f"[REGISTER] User {username} registered successfully")

        cur.close()
    except Exception as e:
        logger.error(f"[REGISTER] Error: {e}")
        conn.rollback()
        emit('register_response', {'status': 'error', 'message': str(e)})
    finally:
        conn.close()

@socketio.on('update_position')
def handle_update_position(data):
    """
    Update user position and broadcast to nearby users
    Data: {user_id, latitude, longitude, altitude}
    """
    user_id = data.get('user_id')
    lat = float(data.get('latitude'))
    lon = float(data.get('longitude'))
    alt = float(data.get('altitude', 0))

    # Validate coordinates
    if not (-90 <= lat <= 90) or not (-180 <= lon <= 180):
        logger.warning(f"[UPDATE_POS] Invalid coordinates from {user_id}: ({lat}, {lon})")
        return

    conn = get_db()
    if not conn:
        return

    try:
        cur = conn.cursor()

        # Update user position
        cur.execute("""
            UPDATE user_sessions
            SET latitude = %s, longitude = %s, altitude = %s, last_heartbeat = NOW()
            WHERE user_id = %s AND status = 'active'
            RETURNING username, avatar_url, bio, is_visible, share_radius
        """, (lat, lon, alt, user_id))

        user_info = cur.fetchone()

        if not user_info or not user_info['is_visible']:
            conn.commit()
            cur.close()
            conn.close()
            return

        # Find nearby users using PostGIS (if available) or Haversine
        # Using Haversine for simplicity (can switch to PostGIS for better performance)
        cur.execute("""
            SELECT
                user_id, username, latitude, longitude, altitude,
                avatar_url, bio, socket_id
            FROM user_sessions
            WHERE user_id != %s
                AND status = 'active'
                AND is_visible = TRUE
                AND latitude != 0 AND longitude != 0
        """, (user_id,))

        all_users = cur.fetchall()
        nearby_users = []

        for other_user in all_users:
            distance = calculate_distance(
                lat, lon,
                other_user['latitude'], other_user['longitude']
            )

            # Within share radius (default 1000m)
            if distance <= user_info['share_radius']:
                user_dict = dict(other_user)
                user_dict['distance'] = round(distance, 1)
                nearby_users.append(user_dict)

        # Sort by distance
        nearby_users.sort(key=lambda x: x['distance'])

        # Limit to 50 nearest users (performance)
        nearby_users = nearby_users[:50]

        # Broadcast current user's position to nearby users
        broadcast_count = 0
        for nearby in nearby_users:
            if nearby['socket_id']:
                try:
                    socketio.emit('nearby_user_update', {
                        'user_id': user_id,
                        'username': user_info['username'],
                        'latitude': lat,
                        'longitude': lon,
                        'altitude': alt,
                        'avatar_url': user_info['avatar_url'],
                        'bio': user_info['bio'],
                        'distance': nearby['distance']
                    }, room=nearby['socket_id'])
                    broadcast_count += 1
                except Exception as e:
                    logger.error(f"[UPDATE_POS] Broadcast error to {nearby['username']}: {e}")

        # Send nearby users list back to current user
        nearby_data = []
        for u in nearby_users:
            nearby_data.append({
                'user_id': u['user_id'],
                'username': u['username'],
                'latitude': u['latitude'],
                'longitude': u['longitude'],
                'altitude': u['altitude'],
                'avatar_url': u['avatar_url'],
                'bio': u['bio'],
                'distance': u['distance']
            })

        emit('nearby_users_list', {'users': nearby_data})

        logger.debug(f"[UPDATE_POS] {user_info['username']}: {len(nearby_users)} nearby, {broadcast_count} broadcasts")

        conn.commit()
        cur.close()
    except Exception as e:
        logger.error(f"[UPDATE_POS] Error: {e}")
        conn.rollback()
    finally:
        conn.close()

@socketio.on('heartbeat')
def handle_heartbeat(data):
    """Keep-alive signal"""
    user_id = data.get('user_id')

    conn = get_db()
    if not conn:
        return

    try:
        cur = conn.cursor()
        cur.execute("""
            UPDATE user_sessions
            SET last_heartbeat = NOW()
            WHERE user_id = %s
        """, (user_id,))
        conn.commit()
        cur.close()
    except Exception as e:
        logger.error(f"[HEARTBEAT] Error: {e}")
        conn.rollback()
    finally:
        conn.close()

@socketio.on('send_gesture')
def handle_send_gesture(data):
    """
    Send AR gesture to another user
    Data: {from_user_id, from_username, target_user_id, gesture_type}
    """
    from_user_id = data.get('from_user_id')
    from_username = data.get('from_username')
    target_user_id = data.get('target_user_id')
    gesture_type = data.get('gesture_type', 'wave')

    logger.info(f"[GESTURE] {from_username} → {target_user_id}: {gesture_type}")

    conn = get_db()
    if not conn:
        return

    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT socket_id FROM user_sessions
            WHERE user_id = %s AND status = 'active'
        """, (target_user_id,))

        result = cur.fetchone()
        if result and result['socket_id']:
            socketio.emit('receive_gesture', {
                'from_user_id': from_user_id,
                'from_username': from_username,
                'gesture_type': gesture_type,
                'timestamp': datetime.now().isoformat()
            }, room=result['socket_id'])

            emit('gesture_sent', {'status': 'success'})
        else:
            emit('gesture_sent', {'status': 'error', 'message': 'User not online'})

        cur.close()
    except Exception as e:
        logger.error(f"[GESTURE] Error: {e}")
        emit('gesture_sent', {'status': 'error', 'message': str(e)})
    finally:
        conn.close()

@socketio.on('update_privacy')
def handle_update_privacy(data):
    """
    Update user privacy settings
    Data: {user_id, is_visible, share_radius}
    """
    user_id = data.get('user_id')
    is_visible = data.get('is_visible', True)
    share_radius = int(data.get('share_radius', 1000))

    conn = get_db()
    if not conn:
        return

    try:
        cur = conn.cursor()
        cur.execute("""
            UPDATE user_sessions
            SET is_visible = %s, share_radius = %s
            WHERE user_id = %s
        """, (is_visible, share_radius, user_id))
        conn.commit()

        emit('privacy_updated', {'status': 'success'})
        logger.info(f"[PRIVACY] User {user_id} privacy updated")

        cur.close()
    except Exception as e:
        logger.error(f"[PRIVACY] Error: {e}")
        conn.rollback()
        emit('privacy_updated', {'status': 'error', 'message': str(e)})
    finally:
        conn.close()

# ===== Periodic Cleanup =====

def cleanup_inactive_sessions():
    """Remove sessions inactive for >5 minutes"""
    conn = get_db()
    if not conn:
        return

    try:
        cur = conn.cursor()
        cur.execute("""
            UPDATE user_sessions
            SET status = 'offline', socket_id = NULL
            WHERE last_heartbeat < NOW() - INTERVAL '5 minutes'
                AND status = 'active'
            RETURNING user_id, username
        """)

        cleaned = cur.fetchall()
        if cleaned:
            logger.info(f"[CLEANUP] Cleaned up {len(cleaned)} inactive sessions")
            for user in cleaned:
                logger.debug(f"  - {user['username']} ({user['user_id']})")

        conn.commit()
        cur.close()
    except Exception as e:
        logger.error(f"[CLEANUP] Error: {e}")
        conn.rollback()
    finally:
        conn.close()

def schedule_cleanup():
    """Schedule cleanup every 60 seconds"""
    cleanup_inactive_sessions()
    Timer(60.0, schedule_cleanup).start()

# ===== REST API Endpoints (Optional) =====

@app.route('/api/p2p/status', methods=['GET'])
def get_p2p_status():
    """Get P2P server status"""
    conn = get_db()
    if not conn:
        return {'status': 'error', 'message': 'Database unavailable'}, 500

    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT COUNT(*) as active_users
            FROM user_sessions
            WHERE status = 'active' AND last_heartbeat > NOW() - INTERVAL '5 minutes'
        """)
        result = cur.fetchone()
        cur.close()
        conn.close()

        return {
            'status': 'online',
            'active_users': result['active_users'],
            'server_time': datetime.now().isoformat()
        }
    except Exception as e:
        logger.error(f"[STATUS] Error: {e}")
        conn.close()
        return {'status': 'error', 'message': str(e)}, 500

@app.route('/api/p2p/register', methods=['POST'])
def register_user_rest():
    """Register user via REST API (polling mode)"""
    data = request.get_json()
    user_id = data.get('user_id')
    username = data.get('username')
    avatar_url = data.get('avatar_url', '')
    bio = data.get('bio', '')

    logger.info(f"[REST REGISTER] User {username} ({user_id}) registering...")

    conn = get_db()
    if not conn:
        return {'status': 'error', 'message': 'Database connection failed'}, 500

    try:
        cur = conn.cursor()

        # Upsert user session
        cur.execute("""
            INSERT INTO user_sessions (
                user_id, username, avatar_url, bio,
                latitude, longitude, altitude, status, last_heartbeat
            )
            VALUES (%s, %s, %s, %s, 0, 0, 0, 'active', NOW())
            ON CONFLICT (user_id)
            DO UPDATE SET
                avatar_url = EXCLUDED.avatar_url,
                bio = EXCLUDED.bio,
                status = 'active',
                last_heartbeat = NOW()
            RETURNING id
        """, (user_id, username, avatar_url, bio))

        result = cur.fetchone()
        conn.commit()
        cur.close()
        conn.close()

        logger.info(f"[REST REGISTER] User {username} registered successfully")
        return {'status': 'success', 'user_id': user_id, 'session_id': result['id']}

    except Exception as e:
        logger.error(f"[REST REGISTER] Error: {e}")
        conn.rollback()
        conn.close()
        return {'status': 'error', 'message': str(e)}, 500

@app.route('/api/p2p/update_position', methods=['POST'])
def update_position_rest():
    """Update position and get nearby users (REST API)"""
    data = request.get_json()
    user_id = data.get('user_id')
    lat = float(data.get('latitude'))
    lon = float(data.get('longitude'))
    alt = float(data.get('altitude', 0))
    visibility_mode = data.get('visibility_mode', 'public')  # public, followingonly, private

    # Validate coordinates
    if not (-90 <= lat <= 90) or not (-180 <= lon <= 180):
        logger.warning(f"[REST UPDATE] Invalid coordinates from {user_id}: ({lat}, {lon})")
        return {'status': 'error', 'message': 'Invalid coordinates'}, 400

    conn = get_db()
    if not conn:
        return {'status': 'error', 'message': 'Database unavailable'}, 500

    try:
        cur = conn.cursor()

        # Update user position and visibility_mode
        cur.execute("""
            UPDATE user_sessions
            SET latitude = %s, longitude = %s, altitude = %s,
                visibility_mode = %s, last_heartbeat = NOW()
            WHERE user_id = %s AND status = 'active'
            RETURNING username, avatar_url, bio, is_visible, share_radius, visibility_mode
        """, (lat, lon, alt, visibility_mode, user_id))

        user_info = cur.fetchone()

        if not user_info:
            conn.commit()
            cur.close()
            conn.close()
            return {'status': 'success', 'users': []}

        # Find nearby users using Haversine
        cur.execute("""
            SELECT
                user_id, username, latitude, longitude, altitude,
                avatar_url, bio, visibility_mode
            FROM user_sessions
            WHERE user_id != %s
                AND status = 'active'
                AND latitude != 0 AND longitude != 0
                AND visibility_mode != 'private'
        """, (user_id,))

        all_users = cur.fetchall()
        nearby_users = []

        for other_user in all_users:
            # 상대방의 visibility_mode 체크
            other_visibility = other_user.get('visibility_mode', 'public')

            # followingonly인 경우: 상대방이 나를 팔로우하는지 체크
            if other_visibility == 'followingonly':
                # 상대방의 팔로잉 목록에 내가 있는지 확인
                cur.execute("""
                    SELECT 1 FROM follows
                    WHERE follower_id = %s AND following_id = %s
                """, (other_user['user_id'], user_id))
                if not cur.fetchone():
                    # 상대방이 나를 팔로우하지 않으면 스킵
                    continue

            distance = calculate_distance(
                lat, lon,
                other_user['latitude'], other_user['longitude']
            )

            # Within share radius (default 1000m)
            if distance <= user_info['share_radius']:
                user_dict = dict(other_user)
                user_dict['distance'] = round(distance, 1)
                # visibility_mode는 클라이언트에 전송하지 않음
                if 'visibility_mode' in user_dict:
                    del user_dict['visibility_mode']
                nearby_users.append(user_dict)

        # Sort by distance
        nearby_users.sort(key=lambda x: x['distance'])

        # Limit to 50 nearest users
        nearby_users = nearby_users[:50]

        conn.commit()
        cur.close()
        conn.close()

        logger.debug(f"[REST UPDATE] {user_info['username']}: {len(nearby_users)} nearby users (mode: {visibility_mode})")

        return {'status': 'success', 'users': nearby_users}

    except Exception as e:
        logger.error(f"[REST UPDATE] Error: {e}")
        conn.rollback()
        conn.close()
        return {'status': 'error', 'message': str(e)}, 500

@app.route('/api/p2p/unregister', methods=['POST'])
def unregister_user_rest():
    """Unregister user (set to private/offline)"""
    data = request.get_json()
    user_id = data.get('user_id')

    logger.info(f"[REST UNREGISTER] User {user_id} unregistering...")

    conn = get_db()
    if not conn:
        return {'status': 'error', 'message': 'Database unavailable'}, 500

    try:
        cur = conn.cursor()

        # Set user to offline and clear location
        cur.execute("""
            UPDATE user_sessions
            SET status = 'offline', visibility_mode = 'private',
                latitude = 0, longitude = 0, altitude = 0
            WHERE user_id = %s
        """, (user_id,))

        conn.commit()
        cur.close()
        conn.close()

        logger.info(f"[REST UNREGISTER] User {user_id} unregistered successfully")
        return {'status': 'success'}

    except Exception as e:
        logger.error(f"[REST UNREGISTER] Error: {e}")
        conn.rollback()
        conn.close()
        return {'status': 'error', 'message': str(e)}, 500

@app.route('/api/p2p/update_privacy', methods=['POST'])
def update_privacy_rest():
    """Update user privacy settings (REST API)"""
    data = request.get_json()
    user_id = data.get('user_id')
    visibility_mode = data.get('visibility_mode', 'public')  # public, followingonly, private
    share_radius = int(data.get('share_radius', 1000))

    logger.info(f"[REST PRIVACY] User {user_id} updating privacy: {visibility_mode}, {share_radius}m")

    conn = get_db()
    if not conn:
        return {'status': 'error', 'message': 'Database unavailable'}, 500

    try:
        cur = conn.cursor()

        # is_visible은 visibility_mode가 private이 아니면 true
        is_visible = visibility_mode != 'private'

        cur.execute("""
            UPDATE user_sessions
            SET visibility_mode = %s, is_visible = %s, share_radius = %s
            WHERE user_id = %s
        """, (visibility_mode, is_visible, share_radius, user_id))

        conn.commit()
        cur.close()
        conn.close()

        logger.info(f"[REST PRIVACY] User {user_id} privacy updated successfully")
        return {'status': 'success'}

    except Exception as e:
        logger.error(f"[REST PRIVACY] Error: {e}")
        conn.rollback()
        conn.close()
        return {'status': 'error', 'message': str(e)}, 500

@app.route('/api/p2p/send_gesture', methods=['POST'])
def send_gesture_rest():
    """Send gesture to another user (REST API - stores in DB for polling)"""
    data = request.get_json()
    from_user_id = data.get('from_user_id')
    from_username = data.get('from_username')
    target_user_id = data.get('target_user_id')
    gesture_type = data.get('gesture_type', 'wave')

    logger.info(f"[REST GESTURE] {from_username} → {target_user_id}: {gesture_type}")

    conn = get_db()
    if not conn:
        return {'status': 'error', 'message': 'Database unavailable'}, 500

    try:
        cur = conn.cursor()

        # For REST API version, you could store gestures in a queue table
        # For now, just log it
        logger.info(f"[REST GESTURE] Gesture '{gesture_type}' from {from_username} to {target_user_id}")

        cur.close()
        conn.close()

        return {'status': 'success', 'message': 'Gesture sent'}

    except Exception as e:
        logger.error(f"[REST GESTURE] Error: {e}")
        conn.close()
        return {'status': 'error', 'message': str(e)}, 500

@app.route('/api/p2p/init_db', methods=['POST'])
def init_database():
    """Initialize database tables (admin only)"""
    # TODO: Add authentication
    conn = get_db()
    if not conn:
        return {'status': 'error', 'message': 'Database unavailable'}, 500

    try:
        cur = conn.cursor()

        # Create user_sessions table
        cur.execute("""
            CREATE TABLE IF NOT EXISTS user_sessions (
                id SERIAL PRIMARY KEY,
                user_id TEXT NOT NULL,
                username TEXT NOT NULL,

                latitude DOUBLE PRECISION NOT NULL DEFAULT 0,
                longitude DOUBLE PRECISION NOT NULL DEFAULT 0,
                altitude DOUBLE PRECISION DEFAULT 0,

                socket_id TEXT UNIQUE,
                status VARCHAR(20) DEFAULT 'active',
                last_heartbeat TIMESTAMP DEFAULT NOW(),

                avatar_url TEXT,
                bio TEXT,

                is_visible BOOLEAN DEFAULT TRUE,
                share_radius INTEGER DEFAULT 1000,

                session_start TIMESTAMP DEFAULT NOW(),
                total_encounters INTEGER DEFAULT 0,

                created_at TIMESTAMP DEFAULT NOW(),
                updated_at TIMESTAMP DEFAULT NOW(),

                CONSTRAINT unique_user_session UNIQUE (user_id)
            )
        """)

        # Create indexes for user_sessions
        cur.execute("""
            CREATE INDEX IF NOT EXISTS idx_user_sessions_socket ON user_sessions(socket_id);
            CREATE INDEX IF NOT EXISTS idx_user_sessions_status ON user_sessions(status);
            CREATE INDEX IF NOT EXISTS idx_user_sessions_location ON user_sessions(latitude, longitude);
        """)

        # Create speech_bubbles table
        cur.execute("""
            CREATE TABLE IF NOT EXISTS speech_bubbles (
                id SERIAL PRIMARY KEY,
                owner_id TEXT NOT NULL,
                target_type VARCHAR(20) NOT NULL,
                target_id TEXT NOT NULL,
                content TEXT NOT NULL,
                latitude DOUBLE PRECISION NOT NULL DEFAULT 0,
                longitude DOUBLE PRECISION NOT NULL DEFAULT 0,
                altitude DOUBLE PRECISION DEFAULT 0,
                background_color VARCHAR(20) DEFAULT '#FFFFFF',
                text_color VARCHAR(20) DEFAULT '#000000',
                created_at TIMESTAMP DEFAULT NOW(),
                expires_at TIMESTAMP DEFAULT (NOW() + INTERVAL '7 days')
            )
        """)

        # Create indexes for speech_bubbles
        cur.execute("""
            CREATE INDEX IF NOT EXISTS idx_speech_bubbles_target ON speech_bubbles(target_type, target_id);
            CREATE INDEX IF NOT EXISTS idx_speech_bubbles_expires ON speech_bubbles(expires_at);
            CREATE INDEX IF NOT EXISTS idx_speech_bubbles_owner ON speech_bubbles(owner_id);
        """)

        conn.commit()
        cur.close()
        conn.close()

        logger.info("[INIT_DB] Database tables created successfully")
        return {'status': 'success', 'message': 'Database initialized'}
    except Exception as e:
        logger.error(f"[INIT_DB] Error: {e}")
        conn.rollback()
        conn.close()
        return {'status': 'error', 'message': str(e)}, 500

# ============================================================
# SpeechBubble API (말풍선 시스템)
# ============================================================

@app.route('/api/p2p/speech_bubble', methods=['POST'])
def create_speech_bubble():
    """Create a new speech bubble"""
    data = request.get_json()
    if not data:
        return {'status': 'error', 'message': 'No data provided'}, 400

    required_fields = ['owner_id', 'target_type', 'target_id', 'content', 'latitude', 'longitude']
    for field in required_fields:
        if field not in data:
            return {'status': 'error', 'message': f'Missing field: {field}'}, 400

    # Content length validation
    content = data['content'].strip()
    if not content or len(content) > 200:
        return {'status': 'error', 'message': 'Content must be 1-200 characters'}, 400

    conn = get_db()
    if not conn:
        return {'status': 'error', 'message': 'Database unavailable'}, 500

    try:
        cur = conn.cursor()
        cur.execute("""
            INSERT INTO speech_bubbles
                (owner_id, target_type, target_id, content, latitude, longitude, altitude,
                 background_color, text_color, expires_at)
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, NOW() + INTERVAL '7 days')
            RETURNING id
        """, (
            data['owner_id'],
            data['target_type'],
            data['target_id'],
            content,
            data['latitude'],
            data['longitude'],
            data.get('altitude', 0),
            data.get('background_color', '#FFFFFF'),
            data.get('text_color', '#000000')
        ))

        bubble_id = cur.fetchone()['id']
        conn.commit()
        cur.close()
        conn.close()

        return {'status': 'success', 'bubble_id': str(bubble_id)}
    except Exception as e:
        logger.error(f"[SPEECH_BUBBLE] Create error: {e}")
        conn.rollback()
        conn.close()
        return {'status': 'error', 'message': str(e)}, 500


@app.route('/api/p2p/speech_bubble/<bubble_id>', methods=['GET'])
def get_speech_bubble(bubble_id):
    """Get a single speech bubble by ID"""
    conn = get_db()
    if not conn:
        return {'status': 'error', 'message': 'Database unavailable'}, 500

    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT id, owner_id, target_type, target_id, content,
                   latitude, longitude, altitude,
                   background_color, text_color,
                   created_at::text, expires_at::text
            FROM speech_bubbles
            WHERE id = %s AND expires_at > NOW()
        """, (bubble_id,))

        row = cur.fetchone()
        cur.close()
        conn.close()

        if not row:
            return {'status': 'error', 'message': 'Bubble not found or expired'}, 404

        return {
            'id': str(row['id']),
            'owner_id': row['owner_id'],
            'target_type': row['target_type'],
            'target_id': row['target_id'],
            'content': row['content'],
            'latitude': float(row['latitude']),
            'longitude': float(row['longitude']),
            'altitude': float(row['altitude']),
            'background_color': row['background_color'],
            'text_color': row['text_color'],
            'created_at': row['created_at'],
            'expires_at': row['expires_at']
        }
    except Exception as e:
        logger.error(f"[SPEECH_BUBBLE] Get error: {e}")
        conn.close()
        return {'status': 'error', 'message': str(e)}, 500


@app.route('/api/p2p/speech_bubbles', methods=['GET'])
def get_speech_bubbles():
    """Get speech bubbles for a target (place or user)"""
    target_type = request.args.get('target_type')
    target_id = request.args.get('target_id')

    if not target_type or not target_id:
        return {'status': 'error', 'message': 'target_type and target_id required'}, 400

    conn = get_db()
    if not conn:
        return {'status': 'error', 'message': 'Database unavailable'}, 500

    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT id, owner_id, target_type, target_id, content,
                   latitude, longitude, altitude,
                   background_color, text_color,
                   created_at::text, expires_at::text
            FROM speech_bubbles
            WHERE target_type = %s AND target_id = %s AND expires_at > NOW()
            ORDER BY created_at DESC
            LIMIT 50
        """, (target_type, target_id))

        rows = cur.fetchall()
        cur.close()
        conn.close()

        bubbles = []
        for row in rows:
            bubbles.append({
                'id': str(row['id']),
                'owner_id': row['owner_id'],
                'target_type': row['target_type'],
                'target_id': row['target_id'],
                'content': row['content'],
                'latitude': float(row['latitude']),
                'longitude': float(row['longitude']),
                'altitude': float(row['altitude']),
                'background_color': row['background_color'],
                'text_color': row['text_color'],
                'created_at': row['created_at'],
                'expires_at': row['expires_at']
            })

        return {'status': 'success', 'bubbles': bubbles}
    except Exception as e:
        logger.error(f"[SPEECH_BUBBLE] List error: {e}")
        conn.close()
        return {'status': 'error', 'message': str(e)}, 500


# ===== Main =====

if __name__ == '__main__':
    logger.info("="*60)
    logger.info("WOOPANG P2P Server Starting...")
    logger.info("="*60)

    # Test database connection
    conn = get_db()
    if conn:
        logger.info("[OK] Database connection successful")
        
        # Auto-migration: Add visibility_mode column if missing
        try:
            cur = conn.cursor()
            cur.execute("""
                ALTER TABLE user_sessions 
                ADD COLUMN IF NOT EXISTS visibility_mode VARCHAR(20) DEFAULT 'public'
            """)
            conn.commit()
            cur.close()
            logger.info("[OK] Database schema updated (visibility_mode)")
        except Exception as e:
            logger.warning(f"[WARN] Database schema update failed: {e}")
            conn.rollback()
            
        conn.close()
    else:
        logger.error("[ERROR] Database connection failed!")

    # Start cleanup scheduler
    logger.info("[OK] Starting cleanup scheduler")
    schedule_cleanup()

    # Run server
    logger.info("[OK] Starting WebSocket server on 0.0.0.0:5001")
    logger.info("="*60)
    socketio.run(app, host='0.0.0.0', port=5001, debug=False, allow_unsafe_werkzeug=True)
