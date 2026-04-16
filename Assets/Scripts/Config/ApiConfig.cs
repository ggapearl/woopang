/// <summary>
/// WOOPANG API URL 중앙 관리
/// 모든 API 호출은 이 클래스의 상수를 사용할 것
/// </summary>
public static class ApiConfig
{
    // 메인 서버 (댓글, 팔로우, 좋아요, 사용자 등)
#if UNITY_EDITOR
    public const string MAIN_SERVER = "https://woopang.com";
#else
    public const string MAIN_SERVER = "https://woopang.com";
#endif

    // P2P 서버 (실시간 위치 추적) - nginx 리버스 프록시를 통해 HTTPS 사용
    public const string P2P_SERVER = MAIN_SERVER;

    // === 메인 서버 API 엔드포인트 ===

    // 댓글
    public const string COMMENTS = MAIN_SERVER + "/comments";
    public const string COMMENTS_LIKE = MAIN_SERVER + "/comments/like";

    // 위치/장소
    public const string LOCATIONS = MAIN_SERVER + "/locations";
    public const string LOCATIONS_LIGHT = MAIN_SERVER + "/locations_light";
    public const string LOCATIONS_DETAIL = MAIN_SERVER + "/locations_detail";
    public const string LOCATIONS_LIKE = MAIN_SERVER + "/locations/like";
    public const string LOCATIONS_LIKES = MAIN_SERVER + "/locations/likes";

    // 사용자
    public const string LOGIN = MAIN_SERVER + "/login";
    public const string LOGIN_VIEW = MAIN_SERVER + "/login_view";
    public const string REGISTER = MAIN_SERVER + "/register";
    public const string USER_PROFILE = MAIN_SERVER + "/api/user/profile";
    public const string USER_AVATAR = MAIN_SERVER + "/api/user/avatar";
    public const string PROFILE_UPDATE = MAIN_SERVER + "/api/profile/update";

    // 팔로우
    public const string FOLLOW = MAIN_SERVER + "/api/follow";
    public const string UNFOLLOW = MAIN_SERVER + "/api/unfollow";
    public const string FOLLOWERS = MAIN_SERVER + "/api/followers";
    public const string FOLLOWING = MAIN_SERVER + "/api/following";
    public const string IS_FOLLOWING = MAIN_SERVER + "/api/is_following";

    // 좋아요 (사용자)
    public const string LIKE_USER = MAIN_SERVER + "/api/like";
    public const string UNLIKE_USER = MAIN_SERVER + "/api/unlike";
    public const string IS_LIKED = MAIN_SERVER + "/api/is_liked";
    public const string LIKERS = MAIN_SERVER + "/api/likers";

    // 다이렉트 메시지 (DM)
    public const string DM_SEND = MAIN_SERVER + "/api/dm/send";
    public const string DM_INBOX = MAIN_SERVER + "/api/dm/inbox";
    public const string DM_SENT = MAIN_SERVER + "/api/dm/sent";
    public const string DM_CONVERSATION = MAIN_SERVER + "/api/dm/conversation";
    public const string DM_CONVERSATIONS = MAIN_SERVER + "/api/dm/conversations";
    public const string DM_READ = MAIN_SERVER + "/api/dm/read";
    public const string DM_READ_ALL = MAIN_SERVER + "/api/dm/read-all";
    public const string DM_UNREAD_COUNT = MAIN_SERVER + "/api/dm/unread-count";

    // 업로드
    public const string UPLOAD = MAIN_SERVER + "/upload";
    public const string UPLOADS_PATH = MAIN_SERVER + "/uploads";
    public const string CREATE_LOCATION_WITH_MODEL = MAIN_SERVER + "/create-location-with-model";
    public const string FIX_UPLOAD = MAIN_SERVER + "/fix_upload";

    // 버전 체크
    public const string VERSION = MAIN_SERVER + "/version";
    public const string VERSION_ANDROID = VERSION + "?platform=android";
    public const string VERSION_IOS = VERSION + "?platform=ios";

    // 푸시 알림
    public const string REGISTER_TOKEN = MAIN_SERVER + "/register-token";

    // TourAPI 프록시
    public const string TOUR_API_PROXY = MAIN_SERVER + "/proxy";

    // 시설 정보
    public const string NEARBY_FACILITIES = MAIN_SERVER + "/api/nearby-facilities";

    // 디바이스 추적
    public const string DEVICE_LOCATION = MAIN_SERVER + "/device-location";
    public const string DEVICE_LOCATIONS = MAIN_SERVER + "/device-locations";

    // 기본 이미지
    public const string DEFAULT_DEVICE_IMAGE = UPLOADS_PATH + "/default_device.jpg";

    // === P2P 서버 API 엔드포인트 ===

    public const string P2P_REGISTER = P2P_SERVER + "/api/p2p/register";
    public const string P2P_UNREGISTER = P2P_SERVER + "/api/p2p/unregister";
    public const string P2P_UPDATE_POSITION = P2P_SERVER + "/api/p2p/update_position";
    public const string P2P_SEND_GESTURE = P2P_SERVER + "/api/p2p/send_gesture";
    public const string P2P_UPDATE_PRIVACY = P2P_SERVER + "/api/p2p/update_privacy";
    public const string P2P_STATUS = P2P_SERVER + "/api/p2p/status";

    // SpeechBubble 서버 - nginx 리버스 프록시를 통해 HTTPS 사용
    public const string SPEECH_BUBBLE_SERVER = MAIN_SERVER;
}
