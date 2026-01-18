/*
 * WOOPANG P2P User Tracking Manager
 * 실시간 사용자 위치 추적 및 AR 아바타 생성
 * REST API 기반 폴링 방식 (WebSocket 대체)
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-01
 */

using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Google.XR.ARCoreExtensions;
using Google.XR.ARCoreExtensions.GeospatialCreator;

/// <summary>
/// 사용자 필터 모드
/// </summary>
public enum UserFilterMode
{
    All,            // 모든 사용자
    FollowingOnly,  // 내가 팔로우하는 사람만
    None            // 숨기기
}

/// <summary>
/// 내 위치 공개 모드
/// </summary>
public enum LocationVisibilityMode
{
    Public,         // 전체공개
    FollowingOnly,  // 내가 팔로우하는 사람들에게만 공개
    Private         // 비공개
}

[Serializable]
public class NearbyUserData
{
    public string user_id;
    public string username;
    public double latitude;
    public double longitude;
    public double altitude;
    public string avatar_url;
    public string bio;
    public float distance;
}

[Serializable]
public class UserPositionUpdate
{
    public string user_id;
    public double latitude;
    public double longitude;
    public double altitude;
}

[Serializable]
public class UserRegistration
{
    public string user_id;
    public string username;
    public string avatar_url;
    public string bio;
}

public class P2PManager : MonoBehaviour
{
    // Singleton
    public static P2PManager Instance { get; private set; }

    [Header("Server Configuration")]
    [SerializeField] private float positionUpdateInterval = 5f;  // 5초마다 위치 업데이트
    [SerializeField] private bool autoConnect = true;            // 자동 연결

    [Header("User Avatar Settings")]
    [SerializeField] private GameObject userAvatarPrefab;        // P2P_User.prefab
    [SerializeField] private int maxVisibleUsers = 20;           // 최대 표시 사용자 수
    [SerializeField] private float maxTrackingDistance = 1000f;  // 1km
    [SerializeField] private int initialPoolSize = 10;           // 초기 풀 크기

    [Header("User Filter Settings")]
    [SerializeField] private UserFilterMode userFilterMode = UserFilterMode.All;  // 사용자 필터 모드

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // User tracking
    private Dictionary<string, GameObject> activeUserAvatars = new Dictionary<string, GameObject>();
    private Dictionary<string, NearbyUserData> nearbyUsersData = new Dictionary<string, NearbyUserData>();
    private Queue<GameObject> avatarPool = new Queue<GameObject>();

    // Current user data
    private string currentUserId;
    private string currentUsername;
    private double currentLatitude;
    private double currentLongitude;
    private double currentAltitude;

    // Geospatial tracking
    private AREarthManager earthManager;

    // Coroutines
    private Coroutine positionUpdateCoroutine;

    // State
    private bool isRegistered = false;

    // Statistics
    public int NearbyUsersCount => activeUserAvatars.Count;
    public bool IsActive => isRegistered;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize pool
        InitializeAvatarPool();
    }

    void Start()
    {
        earthManager = FindObjectOfType<AREarthManager>();

        if (autoConnect)
        {
            StartCoroutine(AutoConnectWhenReady());
        }
    }

    /// <summary>
    /// 초기 아바타 풀 생성
    /// </summary>
    private void InitializeAvatarPool()
    {
        if (userAvatarPrefab == null)
        {
            LogWarning("User avatar prefab not assigned!");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject avatar = Instantiate(userAvatarPrefab, Vector3.zero, Quaternion.identity);
            avatar.SetActive(false);
            avatar.transform.SetParent(transform);
            avatarPool.Enqueue(avatar);
        }

        Log($"Initialized avatar pool with {initialPoolSize} avatars");
    }

    /// <summary>
    /// GPS 준비되면 자동 연결
    /// </summary>
    private IEnumerator AutoConnectWhenReady()
    {
        // Wait for LoginManager to be ready
        LoginManager loginManager = null;
        while (loginManager == null)
        {
            loginManager = FindObjectOfType<LoginManager>();
            yield return new WaitForSeconds(0.5f);
        }

        // Wait for user login
        while (loginManager.CurrentUser == null || string.IsNullOrEmpty(loginManager.CurrentUser.id))
        {
            yield return new WaitForSeconds(0.5f);
        }

        currentUserId = loginManager.CurrentUser.id;
        currentUsername = loginManager.CurrentUser.username;

        Log($"User info loaded: {currentUsername} ({currentUserId})");

        // Wait for GPS to be ready
        while (earthManager == null || earthManager.EarthTrackingState != UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
        {
            if (earthManager == null) earthManager = FindObjectOfType<AREarthManager>();
            yield return new WaitForSeconds(1f);
        }

        Log("GPS tracking ready, registering user...");
        StartTracking();
    }

    /// <summary>
    /// P2P 추적 시작
    /// </summary>
    public void StartTracking()
    {
        if (string.IsNullOrEmpty(currentUserId))
        {
            LogWarning("User ID not set. Cannot start tracking.");
            return;
        }

        StartCoroutine(RegisterUser());
    }

    /// <summary>
    /// P2P 추적 중지
    /// </summary>
    public void StopTracking()
    {
        if (positionUpdateCoroutine != null)
        {
            StopCoroutine(positionUpdateCoroutine);
            positionUpdateCoroutine = null;
        }

        // Remove all avatars
        foreach (var kvp in activeUserAvatars.ToList())
        {
            RemoveUserAvatar(kvp.Key);
        }

        isRegistered = false;
        Log("P2P tracking stopped");
    }

    /// <summary>
    /// 사용자 등록
    /// </summary>
    private IEnumerator RegisterUser()
    {
        UserRegistration reg = new UserRegistration
        {
            user_id = currentUserId,
            username = currentUsername,
            avatar_url = "",
            bio = ""
        };

        string json = JsonConvert.SerializeObject(reg);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(ApiConfig.P2P_REGISTER, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Log($"User registered successfully");
            isRegistered = true;

            // Start position updates
            if (positionUpdateCoroutine == null)
            {
                positionUpdateCoroutine = StartCoroutine(SendPositionUpdates());
            }
        }
        else
        {
            LogError($"Registration failed: {request.error}");
        }

        request.Dispose();
    }

    /// <summary>
    /// 주기적으로 위치 업데이트 전송 및 근처 사용자 조회
    /// </summary>
    private IEnumerator SendPositionUpdates()
    {
        while (isRegistered)
        {
            // Get current GPS position
            if (earthManager != null && earthManager.EarthTrackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
            {
                var pose = earthManager.CameraGeospatialPose;
                currentLatitude = pose.Latitude;
                currentLongitude = pose.Longitude;
                currentAltitude = pose.Altitude;

                // Send position update
                yield return StartCoroutine(UpdatePosition());
            }

            yield return new WaitForSeconds(positionUpdateInterval);
        }
    }

    /// <summary>
    /// 위치 업데이트 전송
    /// </summary>
    private IEnumerator UpdatePosition()
    {
        // 비공개 모드면 위치 업데이트 전송 안함
        if (locationVisibilityMode == LocationVisibilityMode.Private)
        {
            yield break;
        }

        // visibility_mode 포함하여 전송
        JObject posUpdate = new JObject
        {
            ["user_id"] = currentUserId,
            ["latitude"] = currentLatitude,
            ["longitude"] = currentLongitude,
            ["altitude"] = currentAltitude,
            ["visibility_mode"] = locationVisibilityMode.ToString().ToLower()
        };

        string json = posUpdate.ToString();
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(ApiConfig.P2P_UPDATE_POSITION, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            ProcessNearbyUsers(responseText);
        }
        else
        {
            LogWarning($"Position update failed: {request.error}");
        }

        request.Dispose();
    }

    /// <summary>
    /// 근처 사용자 목록 처리
    /// </summary>
    private void ProcessNearbyUsers(string jsonResponse)
    {
        try
        {
            JObject response = JObject.Parse(jsonResponse);
            JArray usersArray = (JArray)response["users"];

            if (usersArray == null) return;

            List<string> currentNearbyUserIds = new List<string>();

            foreach (JObject userObj in usersArray)
            {
                NearbyUserData userData = new NearbyUserData
                {
                    user_id = userObj["user_id"]?.ToString(),
                    username = userObj["username"]?.ToString(),
                    latitude = (double)(userObj["latitude"] ?? 0),
                    longitude = (double)(userObj["longitude"] ?? 0),
                    altitude = (double)(userObj["altitude"] ?? 0),
                    avatar_url = userObj["avatar_url"]?.ToString() ?? "",
                    bio = userObj["bio"]?.ToString() ?? "",
                    distance = (float)(userObj["distance"] ?? 0)
                };

                // 거리 및 필터 조건 확인
                if (userData.distance <= maxTrackingDistance && ShouldShowUser(userData.user_id))
                {
                    currentNearbyUserIds.Add(userData.user_id);

                    if (activeUserAvatars.ContainsKey(userData.user_id))
                    {
                        // Update existing avatar
                        UpdateUserAvatar(userData);
                    }
                    else if (activeUserAvatars.Count < maxVisibleUsers)
                    {
                        // Create new avatar
                        CreateUserAvatar(userData);
                    }
                }
                else
                {
                    // 데이터는 저장 (나중에 필터 변경 시 사용)
                    nearbyUsersData[userData.user_id] = userData;
                }
            }

            // Remove avatars that are no longer nearby
            var usersToRemove = activeUserAvatars.Keys
                .Where(id => !currentNearbyUserIds.Contains(id))
                .ToList();

            foreach (string userId in usersToRemove)
            {
                RemoveUserAvatar(userId);
            }

            Log($"Nearby users: {activeUserAvatars.Count}");
        }
        catch (Exception e)
        {
            LogError($"Failed to process nearby users: {e.Message}");
        }
    }

    /// <summary>
    /// 사용자 아바타 생성
    /// </summary>
    private void CreateUserAvatar(NearbyUserData userData)
    {
        GameObject avatarObj = GetAvatarFromPool();
        if (avatarObj == null)
        {
            LogWarning("Failed to get avatar from pool");
            return;
        }

        // Setup geospatial anchor (Editor only - runtime uses ARCore Geospatial API)
#if UNITY_EDITOR
        var anchor = avatarObj.GetComponent<ARGeospatialCreatorAnchor>();
        if (anchor != null)
        {
            anchor.Latitude = userData.latitude;
            anchor.Longitude = userData.longitude;
            anchor.Altitude = userData.altitude;
            anchor.AltitudeType = AnchorAltitudeType.WGS84;
        }
#endif

        // Setup user info component
        P2PUserInfo userInfo = avatarObj.GetComponent<P2PUserInfo>();
        if (userInfo != null)
        {
            userInfo.Initialize(
                userData.user_id,
                userData.username,
                userData.avatar_url,
                userData.bio,
                userData.distance
            );
        }

#if UNITY_EDITOR
        // 에디터에서는 transform.position 직접 설정
        UpdateAvatarPositionInEditor(avatarObj, userData);
#endif

        avatarObj.SetActive(true);
        activeUserAvatars[userData.user_id] = avatarObj;
        nearbyUsersData[userData.user_id] = userData;

        Log($"Created avatar for user: {userData.username} ({userData.distance:F1}m away)");
    }

    /// <summary>
    /// 사용자 아바타 업데이트
    /// </summary>
    private void UpdateUserAvatar(NearbyUserData userData)
    {
        if (!activeUserAvatars.TryGetValue(userData.user_id, out GameObject avatarObj))
            return;

        // Update position (Editor only - runtime uses ARCore Geospatial API)
#if UNITY_EDITOR
        var anchor = avatarObj.GetComponent<ARGeospatialCreatorAnchor>();
        if (anchor != null)
        {
            anchor.Latitude = userData.latitude;
            anchor.Longitude = userData.longitude;
            anchor.Altitude = userData.altitude;
        }
        // 에디터에서는 transform.position 직접 업데이트 (ARGeospatialCreatorAnchor가 작동 안함)
        UpdateAvatarPositionInEditor(avatarObj, userData);
#endif

        // Update user info
        P2PUserInfo userInfo = avatarObj.GetComponent<P2PUserInfo>();
        if (userInfo != null)
        {
            userInfo.UpdateDistance(userData.distance);
        }

        nearbyUsersData[userData.user_id] = userData;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 GPS 좌표를 월드 좌표로 변환하여 아바타 위치 업데이트
    /// </summary>
    private void UpdateAvatarPositionInEditor(GameObject avatarObj, NearbyUserData userData)
    {
        // VirtualLocation 기준으로 상대 위치 계산
        double baseLat = currentLatitude;
        double baseLon = currentLongitude;

        if (VirtualLocation.Instance != null)
        {
            baseLat = VirtualLocation.Instance.Latitude;
            baseLon = VirtualLocation.Instance.Longitude;
        }

        // GPS 좌표 차이를 미터로 변환
        Vector3 relativePos = GpsToLocalPosition(baseLat, baseLon, userData.latitude, userData.longitude);

        // 카메라 기준 위치 계산
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 worldPos = mainCamera.transform.position + relativePos;
            worldPos.y = mainCamera.transform.position.y; // 같은 높이 유지

            avatarObj.transform.position = worldPos;
        }
    }

    /// <summary>
    /// GPS 좌표 차이를 로컬 미터 단위로 변환
    /// </summary>
    private Vector3 GpsToLocalPosition(double baseLat, double baseLon, double targetLat, double targetLon)
    {
        // 위도 1도 = 약 111,320m
        // 경도 1도 = 약 111,320m * cos(위도)
        const double metersPerDegreeLat = 111320.0;
        double metersPerDegreeLon = 111320.0 * Math.Cos(baseLat * Math.PI / 180.0);

        double deltaLat = targetLat - baseLat;
        double deltaLon = targetLon - baseLon;

        float x = (float)(deltaLon * metersPerDegreeLon); // 동서 방향
        float z = (float)(deltaLat * metersPerDegreeLat); // 남북 방향

        return new Vector3(x, 0, z);
    }
#endif

    /// <summary>
    /// 사용자 아바타 제거
    /// </summary>
    private void RemoveUserAvatar(string userId)
    {
        if (!activeUserAvatars.TryGetValue(userId, out GameObject avatarObj))
            return;

        avatarObj.SetActive(false);
        avatarPool.Enqueue(avatarObj);

        activeUserAvatars.Remove(userId);
        nearbyUsersData.Remove(userId);

        Log($"Removed avatar for user: {userId}");
    }

    /// <summary>
    /// 풀에서 아바타 가져오기
    /// </summary>
    private GameObject GetAvatarFromPool()
    {
        if (avatarPool.Count > 0)
        {
            return avatarPool.Dequeue();
        }

        // Pool is empty, create new avatar
        if (userAvatarPrefab != null)
        {
            GameObject newAvatar = Instantiate(userAvatarPrefab, Vector3.zero, Quaternion.identity);
            newAvatar.transform.SetParent(transform);
            return newAvatar;
        }

        return null;
    }

    /// <summary>
    /// 제스처 전송 (향후 구현)
    /// </summary>
    public void SendGesture(string targetUserId, string gestureType)
    {
        StartCoroutine(SendGestureRequest(targetUserId, gestureType));
    }

    private IEnumerator SendGestureRequest(string targetUserId, string gestureType)
    {
        JObject gestureData = new JObject
        {
            ["from_user_id"] = currentUserId,
            ["from_username"] = currentUsername,
            ["target_user_id"] = targetUserId,
            ["gesture_type"] = gestureType
        };

        string json = gestureData.ToString();
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(ApiConfig.P2P_SEND_GESTURE, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Log($"Gesture '{gestureType}' sent to {targetUserId}");
        }
        else
        {
            LogWarning($"Failed to send gesture: {request.error}");
        }

        request.Dispose();
    }

    /// <summary>
    /// 최대 추적 거리 설정 (P2PUserListPanel에서 호출)
    /// </summary>
    public void SetMaxTrackingDistance(float distance)
    {
        maxTrackingDistance = distance;
        Log($"Max tracking distance set to: {distance}m");

        // 200m 이상 아바타 숨김 처리
        UpdateVisibleAvatars();
    }

    /// <summary>
    /// 프라이버시 설정 업데이트
    /// </summary>
    public void UpdatePrivacySettings(string visibilityMode, int shareRadius)
    {
        StartCoroutine(SendPrivacyUpdate(visibilityMode, shareRadius));
    }

    // 현재 위치 공개 모드
    private LocationVisibilityMode locationVisibilityMode = LocationVisibilityMode.Public;

    /// <summary>
    /// 내 위치 공개 모드 설정 (P2POpenFilterPanel에서 호출)
    /// </summary>
    public void SetLocationVisibility(LocationVisibilityMode mode)
    {
        locationVisibilityMode = mode;
        Log($"Location visibility set to: {mode}");

        // 서버에 공개 설정 전송
        string visibilityString = mode.ToString().ToLower();
        StartCoroutine(SendPrivacyUpdate(visibilityString, (int)maxTrackingDistance));

        // 비공개 모드면 위치 업데이트 중지
        if (mode == LocationVisibilityMode.Private)
        {
            if (positionUpdateCoroutine != null)
            {
                StopCoroutine(positionUpdateCoroutine);
                positionUpdateCoroutine = null;
            }
            // 서버에서 자신의 위치 데이터 삭제 요청
            StartCoroutine(SendUnregisterRequest());
        }
        else if (isRegistered && positionUpdateCoroutine == null)
        {
            // 공개 모드로 변경 시 위치 업데이트 재개
            positionUpdateCoroutine = StartCoroutine(SendPositionUpdates());
        }
    }

    /// <summary>
    /// 현재 위치 공개 모드 가져오기
    /// </summary>
    public LocationVisibilityMode GetLocationVisibility()
    {
        return locationVisibilityMode;
    }

    /// <summary>
    /// 서버에서 자신의 위치 데이터 삭제 요청
    /// </summary>
    private IEnumerator SendUnregisterRequest()
    {
        JObject unregData = new JObject
        {
            ["user_id"] = currentUserId
        };

        string json = unregData.ToString();
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(ApiConfig.P2P_UNREGISTER, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Log("User location unregistered (private mode)");
        }
        else
        {
            LogWarning($"Failed to unregister: {request.error}");
        }

        request.Dispose();
    }

    private IEnumerator SendPrivacyUpdate(string visibilityMode, int shareRadius)
    {
        JObject privacyData = new JObject
        {
            ["user_id"] = currentUserId,
            ["visibility_mode"] = visibilityMode,
            ["share_radius"] = shareRadius
        };

        string json = privacyData.ToString();
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(ApiConfig.P2P_UPDATE_PRIVACY, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Log($"Privacy settings updated: {visibilityMode}, {shareRadius}m");
        }
        else
        {
            LogWarning($"Failed to update privacy settings: {request.error}");
        }

        request.Dispose();
    }

    /// <summary>
    /// 거리 및 필터에 따라 아바타 표시/숨김 처리
    /// </summary>
    private void UpdateVisibleAvatars()
    {
        // 필터 모드가 None이면 모든 아바타 숨김
        if (userFilterMode == UserFilterMode.None)
        {
            foreach (var avatarObj in activeUserAvatars.Values)
            {
                SetAvatarVisible(avatarObj, false);
            }
            return;
        }

        // 필터에 맞지 않는 아바타 제거
        var usersToRemove = activeUserAvatars.Keys
            .Where(id => !ShouldShowUser(id))
            .ToList();

        foreach (string userId in usersToRemove)
        {
            RemoveUserAvatar(userId);
        }

        // 기존 아바타 가시성 업데이트
        foreach (var kvp in activeUserAvatars)
        {
            string userId = kvp.Key;
            GameObject avatarObj = kvp.Value;

            if (nearbyUsersData.ContainsKey(userId))
            {
                NearbyUserData userData = nearbyUsersData[userId];

                // 200m 이상은 3D 오브젝트 숨김, 거리 설정 밖도 숨김, 필터 확인
                bool shouldShow = userData.distance <= 200f &&
                                  userData.distance <= maxTrackingDistance &&
                                  ShouldShowUser(userId);

                SetAvatarVisible(avatarObj, shouldShow);
            }
        }

        // 필터 모드 변경으로 새로 보여야 할 사용자 추가
        if (userFilterMode != UserFilterMode.None)
        {
            foreach (var kvp in nearbyUsersData)
            {
                string userId = kvp.Key;
                NearbyUserData userData = kvp.Value;

                if (!activeUserAvatars.ContainsKey(userId) &&
                    ShouldShowUser(userId) &&
                    userData.distance <= maxTrackingDistance &&
                    activeUserAvatars.Count < maxVisibleUsers)
                {
                    CreateUserAvatar(userData);
                }
            }
        }
    }

    /// <summary>
    /// 아바타 가시성 설정
    /// </summary>
    private void SetAvatarVisible(GameObject avatarObj, bool visible)
    {
        MeshRenderer[] renderers = avatarObj.GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = visible;
        }
    }

    /// <summary>
    /// 근처 사용자 데이터 가져오기 (P2PUserListPanel에서 호출)
    /// </summary>
    public List<NearbyUserData> GetNearbyUsers()
    {
        return nearbyUsersData.Values.ToList();
    }

    /// <summary>
    /// 특정 사용자에게 카메라 포커스 (P2PUserListPanel에서 호출)
    /// </summary>
    public void FocusOnUser(string userId)
    {
        if (!activeUserAvatars.ContainsKey(userId))
        {
            LogWarning($"User avatar not found: {userId}");
            return;
        }

        GameObject avatarObj = activeUserAvatars[userId];
        if (avatarObj == null) return;

        // 카메라를 해당 아바타 방향으로 회전
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 direction = (avatarObj.transform.position - mainCamera.transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(direction);

            // 부드러운 회전 (향후 Coroutine으로 개선 가능)
            mainCamera.transform.rotation = Quaternion.Slerp(
                mainCamera.transform.rotation,
                lookRotation,
                Time.deltaTime * 2f
            );
        }

        Log($"Focusing on user: {userId}");
    }

    #region User Filter Mode

    // 팔로잉 목록 캐시
    private HashSet<string> followingUserIds = new HashSet<string>();
    private float lastFollowingCacheTime = 0f;
    private const float FOLLOWING_CACHE_DURATION = 60f; // 60초마다 갱신

    /// <summary>
    /// 사용자 필터 모드 설정 (ListPanel에서 호출)
    /// </summary>
    public void SetUserFilterMode(UserFilterMode mode)
    {
        userFilterMode = mode;
        Log($"User filter mode set to: {mode}");

        if (mode == UserFilterMode.FollowingOnly)
        {
            // 팔로잉 목록 새로고침
            StartCoroutine(RefreshFollowingList());
        }

        UpdateVisibleAvatars();
    }

    /// <summary>
    /// 현재 필터 모드 가져오기
    /// </summary>
    public UserFilterMode GetUserFilterMode()
    {
        return userFilterMode;
    }

    /// <summary>
    /// 팔로잉 목록 새로고침
    /// </summary>
    private IEnumerator RefreshFollowingList()
    {
        if (string.IsNullOrEmpty(currentUserId)) yield break;

        string url = $"{ApiConfig.FOLLOWING}?user_id={currentUserId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    JObject response = JObject.Parse(request.downloadHandler.text);
                    JArray followingArray = (JArray)response["following"];

                    followingUserIds.Clear();
                    if (followingArray != null)
                    {
                        foreach (JObject user in followingArray)
                        {
                            string id = user["id"]?.ToString();
                            if (!string.IsNullOrEmpty(id))
                            {
                                followingUserIds.Add(id);
                            }
                        }
                    }

                    lastFollowingCacheTime = Time.time;
                    Log($"Following list refreshed: {followingUserIds.Count} users");
                }
                catch (Exception e)
                {
                    LogError($"Failed to parse following list: {e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 사용자가 필터에 맞는지 확인
    /// </summary>
    private bool ShouldShowUser(string userId)
    {
        switch (userFilterMode)
        {
            case UserFilterMode.None:
                return false;

            case UserFilterMode.FollowingOnly:
                return followingUserIds.Contains(userId);

            case UserFilterMode.All:
            default:
                return true;
        }
    }

    #endregion

    // Logging helpers
    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[P2PManager] {message}");
    }

    private void LogWarning(string message)
    {
        if (showDebugLogs)
            Debug.LogWarning($"[P2PManager] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[P2PManager] {message}");
    }

    void OnDestroy()
    {
        StopTracking();
    }

    #region Test Methods (Virtual Users)

    /// <summary>
    /// 테스트용: 가상 사용자 데이터 직접 주입
    /// P2PVirtualUserTester에서 호출
    /// </summary>
    public void InjectTestUsersData(string jsonResponse)
    {
        ProcessNearbyUsers(jsonResponse);
    }

    /// <summary>
    /// 테스트용: 특정 가상 사용자 아바타 제거
    /// </summary>
    public void RemoveTestUserAvatar(string userId)
    {
        RemoveUserAvatar(userId);
    }

    /// <summary>
    /// 테스트용: 현재 위치 수동 설정 (에디터에서 GPS 없이 테스트)
    /// </summary>
    public void SetTestPosition(double latitude, double longitude, double altitude)
    {
        currentLatitude = latitude;
        currentLongitude = longitude;
        currentAltitude = altitude;
        Log($"Test position set: ({latitude}, {longitude}, {altitude})");
    }

    /// <summary>
    /// 테스트용: 사용자 ID 수동 설정
    /// </summary>
    public void SetTestUserId(string userId, string username)
    {
        currentUserId = userId;
        currentUsername = username;
        Log($"Test user set: {username} ({userId})");
    }

    #endregion
}
