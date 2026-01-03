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
using UnityEngine.XR.ARFoundation;
using Google.XR.ARCoreExtensions.GeospatialCreator;

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
    [SerializeField] private string serverUrl = "http://210.105.65.145:5001";
    [SerializeField] private float positionUpdateInterval = 5f;  // 5초마다 위치 업데이트
    [SerializeField] private bool autoConnect = true;            // 자동 연결

    [Header("User Avatar Settings")]
    [SerializeField] private GameObject userAvatarPrefab;        // P2P_User.prefab
    [SerializeField] private int maxVisibleUsers = 20;           // 최대 표시 사용자 수
    [SerializeField] private float maxTrackingDistance = 1000f;  // 1km
    [SerializeField] private int initialPoolSize = 10;           // 초기 풀 크기

    [Header("References")]
    [SerializeField] private ARAnchorManager anchorManager;

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
        // Find components
        if (anchorManager == null)
        {
            anchorManager = FindObjectOfType<ARAnchorManager>();
        }

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

        UnityWebRequest request = new UnityWebRequest($"{serverUrl}/api/p2p/register", "POST");
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
        UserPositionUpdate posUpdate = new UserPositionUpdate
        {
            user_id = currentUserId,
            latitude = currentLatitude,
            longitude = currentLongitude,
            altitude = currentAltitude
        };

        string json = JsonConvert.SerializeObject(posUpdate);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest($"{serverUrl}/api/p2p/update_position", "POST");
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

                if (userData.distance <= maxTrackingDistance)
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

        // Setup geospatial anchor
        var anchor = avatarObj.GetComponent<ARGeospatialCreatorAnchor>();
        if (anchor != null)
        {
            anchor.Latitude = userData.latitude;
            anchor.Longitude = userData.longitude;
            anchor.Altitude = userData.altitude;
            anchor.AltitudeType = AnchorAltitudeType.WGS84;
        }

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

        // Update position
        var anchor = avatarObj.GetComponent<ARGeospatialCreatorAnchor>();
        if (anchor != null)
        {
            anchor.Latitude = userData.latitude;
            anchor.Longitude = userData.longitude;
            anchor.Altitude = userData.altitude;
        }

        // Update user info
        P2PUserInfo userInfo = avatarObj.GetComponent<P2PUserInfo>();
        if (userInfo != null)
        {
            userInfo.UpdateDistance(userData.distance);
        }

        nearbyUsersData[userData.user_id] = userData;
    }

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

        UnityWebRequest request = new UnityWebRequest($"{serverUrl}/api/p2p/send_gesture", "POST");
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

        UnityWebRequest request = new UnityWebRequest($"{serverUrl}/api/p2p/update_privacy", "POST");
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
    /// 거리에 따라 아바타 표시/숨김 처리
    /// </summary>
    private void UpdateVisibleAvatars()
    {
        foreach (var kvp in activeUserAvatars)
        {
            string userId = kvp.Key;
            GameObject avatarObj = kvp.Value;

            if (nearbyUsersData.ContainsKey(userId))
            {
                NearbyUserData userData = nearbyUsersData[userId];

                // 200m 이상은 3D 오브젝트 숨김, 거리 설정 밖도 숨김
                bool shouldShow = userData.distance <= 200f && userData.distance <= maxTrackingDistance;

                // MeshRenderer만 제어 (Target 컴포넌트는 유지)
                MeshRenderer[] renderers = avatarObj.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    renderer.enabled = shouldShow;
                }
            }
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
}
