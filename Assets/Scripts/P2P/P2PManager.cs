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

#if UNITY_EDITOR
    public static string EditorBase;   // 플레이 시험 — 위치 등록·팔로잉 목록을 내 PC 시험 서버로 (운영 서버에 시험 아이디로 묻지 않게)
    private static string Api(string url) => string.IsNullOrEmpty(EditorBase) ? url : EditorBase + url.Substring(ApiConfig.MAIN_SERVER.Length);
    private static bool EditorTesting => !string.IsNullOrEmpty(EditorBase);   // 에디터엔 위치 권한 알림이 없다 — 시험 서버로 돌릴 때만 권한을 기다리지 않는다
#else
    private static string Api(string url) => url;
    private const bool EditorTesting = false;
#endif

    [Header("Server Configuration")]
    [SerializeField] private float foregroundUpdateInterval = 5f;   // 포그라운드: 5초
    [SerializeField] private float backgroundUpdateInterval = 10f;  // 백그라운드: 10초
    [SerializeField] private bool autoConnect = true;               // 자동 연결

    [Header("User Avatar Settings")]
    [SerializeField] private GameObject userAvatarPrefab;        // P2P_User.prefab
    [SerializeField] private int maxVisibleUsers = 100;          // 최대 표시 사용자 수
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
    private bool connectReady;   // 위치 준비까지 끝났다 — 그 뒤 로그인이 바뀌면 바로 다시 등록한다
    private bool isInBackground = false;
    private float currentUpdateInterval => isInBackground ? backgroundUpdateInterval : foregroundUpdateInterval;

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

        locationVisibilityMode = LoadVisibility();

        // Initialize pool
        InitializeAvatarPool();
    }

    void Start()
    {
        earthManager = FindFirstObjectByType<AREarthManager>();

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
            loginManager = FindFirstObjectByType<LoginManager>();
            yield return new WaitForSeconds(0.5f);
        }

        // 로그인이 바뀌면 그 계정으로 다시 — 예전엔 아래에서 한 번 받은 아이디를 끝까지 써서, 로그아웃하고 다른 아이디로
        // 들어가도 위치·팔로잉 목록이 처음 아이디로 오갔다 (10-21 대표님 「다른 아이디로 로그인했는데 서로 멀리 보이고 보물찾기 메시지도 안 온다」)
        loginManager.OnLoginStateChanged -= OnLoginStateChanged;
        loginManager.OnLoginStateChanged += OnLoginStateChanged;
        loginManager.OnLoggingOut -= OnLoggingOut;
        loginManager.OnLoggingOut += OnLoggingOut;

        // Wait for user login
        while (loginManager.CurrentUser == null || string.IsNullOrEmpty(loginManager.CurrentUser.id))
        {
            yield return new WaitForSeconds(0.5f);
        }

        // 기다리는 사이 로그인했으면 OnLoginStateChanged 가 이미 그 계정으로 맞춰 두었다 (팔로잉 목록도 그때 받는다)
        // 사람 칩이 로그인보다 먼저 '팔로잉'을 정해 두면 그땐 내 아이디가 없어 팔로잉 목록을 못 받았다 — 앱을 켤 때마다 '팔로잉'이 비어 있었다
        if (currentUserId != loginManager.CurrentUser.id) SwitchUser(loginManager.CurrentUser.id, loginManager.CurrentUser.username);

        Log($"User info loaded: {currentUsername} ({currentUserId})");

        // 위치 권한이 없으면 DataManager의 이벤트를 기다린 후 진행
        if (!Input.location.isEnabledByUser)
        {
            Log("위치 권한 없음 - DataManager.OnLocationPermissionGranted 대기 중...");
            bool permissionGranted = false;
            System.Action onGranted = () => permissionGranted = true;
            DataManager.OnLocationPermissionGranted += onGranted;
            yield return new WaitUntil(() => permissionGranted || EditorTesting);
            DataManager.OnLocationPermissionGranted -= onGranted;
            Log("위치 권한 획득 - P2P 연결 재개");
        }

        // Input.location 직접 시작 (다른 스크립트에서 아직 시작 안 했을 수 있음)
        if (Input.location.status == LocationServiceStatus.Stopped)
        {
            Log("Starting Input.location service for P2P...");
            Input.location.Start(10f, 1f);
        }

        // Wait for GPS to be ready (AREarthManager 또는 Input.location 둘 중 하나)
        float gpsWaitTime = 0f;
        float gpsMaxWait = 15f; // AREarthManager 대기
        float absoluteMaxWait = 30f; // 절대 최대 대기 시간
        bool gpsReady = false;

        while (!gpsReady)
        {
            // AREarthManager 트래킹 확인
            if (earthManager == null) earthManager = FindFirstObjectByType<AREarthManager>();
            if (earthManager != null && earthManager.EarthTrackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
            {
                Log("AREarthManager GPS tracking ready");
                gpsReady = true;
                break;
            }

            // Fallback: Input.location이 작동 중이면 15초 후 진행
            if (gpsWaitTime >= gpsMaxWait && Input.location.status == LocationServiceStatus.Running)
            {
                Log($"AREarthManager not tracking after {gpsMaxWait}s, using Input.location fallback for P2P");
                gpsReady = true;
                break;
            }

            // 절대 타임아웃: GPS 없이도 등록 진행 (위치 업데이트는 나중에)
            if (gpsWaitTime >= absoluteMaxWait)
            {
                Log($"GPS not available after {absoluteMaxWait}s, proceeding with registration anyway (location status: {Input.location.status})");
                gpsReady = true;
                break;
            }

            gpsWaitTime += 1f;
            yield return new WaitForSeconds(1f);
        }

        Log("GPS ready, registering user...");
        connectReady = true;
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
        nearbyUsersData.Clear();   // 더 받지 않으니 근처 사람 목록도 비운다

        isRegistered = false;
        Log("P2P tracking stopped");
    }

    // 로그아웃하는 순간 — 토큰이 아직 있을 때 이 휴대폰이 보내던 그 계정의 위치를 서버에서 내리고 보내기를 멈춘다
    private void OnLoggingOut()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;
        if (isRegistered) StartCoroutine(SendUnregisterRequest());   // 요청은 지금 토큰을 붙여 만든다 (보내는 건 그 뒤)
        SwitchUser(null, null);
    }

    // 로그인이 바뀌었다 — 새 아이디로 다시 등록해 위치·근처 사람·팔로잉 목록이 그 계정으로 오가게
    private void OnLoginStateChanged(bool loggedIn)
    {
        var user = loggedIn && LoginManager.Instance != null ? LoginManager.Instance.CurrentUser : null;
        string id = user != null && !string.IsNullOrEmpty(user.id) ? user.id : null;
        if (id == currentUserId) return;
        SwitchUser(id, user?.username);
        if (id != null && connectReady) StartTracking();
    }

    private void SwitchUser(string id, string username)
    {
        StopTracking();
        followingUserIds.Clear();
        lastFollowingCacheTime = -FOLLOWING_CACHE_DURATION;
        currentUserId = id;
        currentUsername = username;
        if (id != null && userFilterMode == UserFilterMode.FollowingOnly) StartCoroutine(RefreshFollowingList());
        UserFilterChanged?.Invoke();
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

        UnityWebRequest request = new UnityWebRequest(Api(ApiConfig.P2P_REGISTER), "POST");
        LoginManager.ApplyAuth(request);   // P2P 서버가 토큰으로 사용자를 확인한다 (2026-09-27)
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        string forUser = currentUserId;
        yield return request.SendWebRequest();

        if (forUser != currentUserId)
        {
            request.Dispose();   // 그새 로그아웃·다른 아이디 — 앞 계정으로 위치를 보내기 시작하지 않는다
            yield break;
        }
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
            bool positionUpdated = false;

            // Get current GPS position (ARCore Geospatial)
            if (earthManager != null && earthManager.EarthTrackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
            {
                var pose = earthManager.CameraGeospatialPose;
                currentLatitude = pose.Latitude;
                currentLongitude = pose.Longitude;
                currentAltitude = pose.Altitude;
                positionUpdated = true;
            }
            // GPS fallback (AREarthManager가 작동 안할 때 - 포그라운드/백그라운드 모두)
            else if (Input.location.status == LocationServiceStatus.Running && UsableFix(Input.location.lastData))
            {
                var loc = Input.location.lastData;
                currentLatitude = loc.latitude;
                currentLongitude = loc.longitude;
                // iOS는 WGS84 타원체 고도 → MSL 보정 필요 (Android는 그대로)
                currentAltitude = GeoidHelper.NormalizeAltitude(loc.altitude, loc.latitude, loc.longitude);
                positionUpdated = true;
            }

            if (positionUpdated)
            {
                yield return StartCoroutine(UpdatePosition());
            }

            // 팔로잉만 보기 — 그새 팔로우·언팔로우했을 수 있다 (1분마다 다시 받는다)
            if (userFilterMode == UserFilterMode.FollowingOnly && Time.time - lastFollowingCacheTime > FOLLOWING_CACHE_DURATION)
                StartCoroutine(RefreshFollowingList());

            // 포그라운드/백그라운드에 따라 다른 간격 사용
            yield return new WaitForSeconds(currentUpdateInterval);
        }
    }

    // 앱을 켜거나 돌아온 직후의 GPS 값은 휴대폰에 남아 있던 예전 측정값일 수 있다 — 잠깐 켰다 끄면 그 옛 자리가
    // '방금'으로 저장돼 친구 지도에 24시간 남았다 (2026-10-11). 새 측정값을 본 뒤부터 보낸다.
    private const float MaxFixAccuracy = 500f;   // 이보다 흐린 값(대략 위치 권한·기지국 측위, 수 km)은 보내지 않는다
    private bool fixTrusted;
    private double fixFirstStamp;
    private float fixWaitFrom = -1f;

    private void ResetFixGate()
    {
        fixTrusted = false;
        fixWaitFrom = -1f;
    }

    private bool UsableFix(LocationInfo loc)
    {
        if (loc.latitude == 0f && loc.longitude == 0f) return false;   // 첫 측정 전
        if (loc.horizontalAccuracy <= 0f || loc.horizontalAccuracy > MaxFixAccuracy) return false;
        if (fixTrusted) return true;
        if (fixWaitFrom < 0f) { fixWaitFrom = Time.realtimeSinceStartup; fixFirstStamp = loc.timestamp; }
        double age = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds - loc.timestamp;
        // 처음 본 뒤 새 측정값이 왔거나 · 2분 안에 잰 값이거나 · 30초를 기다려도 새 값이 없으면(가만히 있어 휴대폰이 새로 안 잰다) 믿는다
        fixTrusted = loc.timestamp != fixFirstStamp || (age > -60.0 && age < 120.0) || Time.realtimeSinceStartup - fixWaitFrom > 30f;
        return fixTrusted;
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

        UnityWebRequest request = new UnityWebRequest(Api(ApiConfig.P2P_UPDATE_POSITION), "POST");
        LoginManager.ApplyAuth(request);   // P2P 서버가 토큰으로 사용자를 확인한다 (2026-09-27)
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();
        ServerHealth.Report(request);   // 5초 주기 요청 — 서버 응답 여부 판단에 같이 쓴다

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
            var inResponse = new HashSet<string>();

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
                inResponse.Add(userData.user_id);

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

            // 더 이상 근처에 없는 사람만 목록 자료에서 뺀다 (필터로 가린 사람은 남겨 둬야 필터를 바꾸면 바로 다시 보인다)
            foreach (string userId in nearbyUsersData.Keys.Where(id => !inResponse.Contains(id)).ToList())
                nearbyUsersData.Remove(userId);

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
        P2PUserInfo userInfo = avatarObj.GetComponentInChildren<P2PUserInfo>(true);
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
        P2PUserInfo userInfo = avatarObj.GetComponentInChildren<P2PUserInfo>(true);
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

        // 자료(nearbyUsersData)는 둔다 — 예전엔 같이 지워서, 사람 칩을 '팔로잉만'으로 바꾸는 순간(팔로잉 목록이 오기 전) 가려진
        // 사람들의 자료가 사라져 목록이 오고 나서도 다음 위치 갱신까지 아무도 안 나왔다 (10-21)
        activeUserAvatars.Remove(userId);

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

        UnityWebRequest request = new UnityWebRequest(Api(ApiConfig.P2P_SEND_GESTURE), "POST");
        LoginManager.ApplyAuth(request);   // P2P 서버가 토큰으로 사용자를 확인한다 (2026-09-27)
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

    // 현재 위치 공개 모드 — 저장해 둔다. 예전엔 앱을 다시 켜면 '비공개'로 둔 사람도 전체공개로 돌아갔다.
    private const string VisibilityPrefKey = "P2P_LocationVisibility";
    private LocationVisibilityMode locationVisibilityMode = LocationVisibilityMode.Public;   // Awake 에서 저장값으로 교체

    private static LocationVisibilityMode LoadVisibility()
    {
        int v = PlayerPrefs.GetInt(VisibilityPrefKey, (int)LocationVisibilityMode.Public);
        return Enum.IsDefined(typeof(LocationVisibilityMode), v) ? (LocationVisibilityMode)v : LocationVisibilityMode.Public;
    }

    /// <summary>
    /// 내 위치 공개 모드 설정 (P2POpenFilterPanel에서 호출)
    /// </summary>
    public void SetLocationVisibility(LocationVisibilityMode mode)
    {
        locationVisibilityMode = mode;
        PlayerPrefs.SetInt(VisibilityPrefKey, (int)mode);
        PlayerPrefs.Save();
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

        UnityWebRequest request = new UnityWebRequest(Api(ApiConfig.P2P_UNREGISTER), "POST");
        LoginManager.ApplyAuth(request);   // P2P 서버가 토큰으로 사용자를 확인한다 (2026-09-27)
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

        UnityWebRequest request = new UnityWebRequest(Api(ApiConfig.P2P_UPDATE_PRIVACY), "POST");
        LoginManager.ApplyAuth(request);   // P2P 서버가 토큰으로 사용자를 확인한다 (2026-09-27)
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
        // 필터 모드가 None이면 모든 아바타 완전히 숨김 (인디케이터 포함)
        if (userFilterMode == UserFilterMode.None)
        {
            // 사본으로 돈다 — 숨기는 중에 아바타가 들어오고 나가면 원본이 바뀌어 'Collection was modified' 로 꺼졌다 (2026-10-17 오류 보고)
            foreach (var avatarObj in activeUserAvatars.Values.ToList())
            {
                if (avatarObj != null) SetAvatarCompletelyHidden(avatarObj, true);
            }
            Log("P2P filter set to None - all avatars hidden completely");
            return;
        }

        // 필터 모드가 None에서 변경된 경우, 완전 숨김 해제 먼저 수행
        foreach (var avatarObj in activeUserAvatars.Values.ToList())
        {
            if (avatarObj != null) SetAvatarCompletelyHidden(avatarObj, false);
        }

        // 필터에 맞지 않는 아바타 제거
        var usersToRemove = activeUserAvatars.Keys
            .Where(id => !ShouldShowUser(id))
            .ToList();

        foreach (string userId in usersToRemove)
        {
            RemoveUserAvatar(userId);
        }

        // 기존 아바타 가시성 업데이트 (3D 렌더러만 - 거리 기준)
        foreach (var kvp in activeUserAvatars.ToList())
        {
            string userId = kvp.Key;
            GameObject avatarObj = kvp.Value;

            if (avatarObj != null && nearbyUsersData.ContainsKey(userId))
            {
                NearbyUserData userData = nearbyUsersData[userId];

                // 200m 이상은 3D 오브젝트 숨김, 거리 설정 밖도 숨김
                // (오프스크린 인디케이터는 계속 표시)
                bool shouldShowMesh = userData.distance <= 200f &&
                                      userData.distance <= maxTrackingDistance;

                SetAvatarVisible(avatarObj, shouldShowMesh);
            }
        }

        // 필터 모드 변경으로 새로 보여야 할 사용자 추가
        foreach (var kvp in nearbyUsersData.ToList())
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

        Log($"P2P avatars updated - filter: {userFilterMode}, active: {activeUserAvatars.Count}");
    }

    /// <summary>
    /// 아바타 가시성 설정 (3D 렌더러만)
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
    /// 아바타 완전 숨김/표시 (오프스크린 인디케이터 포함)
    /// </summary>
    private void SetAvatarCompletelyHidden(GameObject avatarObj, bool hidden)
    {
        P2PUserInfo userInfo = avatarObj.GetComponentInChildren<P2PUserInfo>(true);
        if (userInfo != null)
        {
            if (hidden)
            {
                userInfo.HideCompletely();
            }
            else
            {
                userInfo.ShowAgain();
            }
        }
    }

    /// <summary>
    /// 근처 사용자 데이터 가져오기 (필터 적용 안됨 - 전체 목록)
    /// </summary>
    public List<NearbyUserData> GetNearbyUsers()
    {
        return nearbyUsersData.Values.ToList();
    }

    /// <summary>
    /// 필터가 적용된 근처 사용자 데이터 가져오기 (PlaceListManager용)
    /// None: 빈 리스트, All: 전체, FollowingOnly: 팔로잉만
    /// </summary>
    public List<NearbyUserData> GetFilteredNearbyUsers()
    {
        if (userFilterMode == UserFilterMode.None)
        {
            return new List<NearbyUserData>();
        }

        return nearbyUsersData.Values
            .Where(user => ShouldShowUser(user.user_id))
            .ToList();
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

    /// <summary>사람 필터가 바뀌었거나 팔로잉 목록이 새로 왔다 — 장소 목록(PlaceListManager)이 바로 다시 그린다</summary>
    public event Action UserFilterChanged;

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
        UserFilterChanged?.Invoke();
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
        lastFollowingCacheTime = Time.time;   // 실패해도 1분 안엔 다시 묻지 않는다 (위치 보내기 주기마다 묻게 되지 않게)
        string forUser = currentUserId;

        string url = $"{Api(ApiConfig.FOLLOWING)}?user_id={currentUserId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            LoginManager.ApplyAuth(request);
            yield return request.SendWebRequest();
            if (forUser != currentUserId) yield break;   // 그새 로그인이 바뀌었다 — 앞 계정의 목록

            if (request.result == UnityWebRequest.Result.Success)
            {
                var before = new HashSet<string>(followingUserIds);
                try
                {
                    JObject response = JObject.Parse(request.downloadHandler.text);
                    JArray followingArray = (JArray)response["following"];

                    followingUserIds.Clear();
                    if (followingArray != null)
                    {
                        foreach (JObject user in followingArray)
                        {
                            // 서버는 user_id 로 준다 — 예전엔 id 를 읽어 목록이 늘 비어 '팔로잉'에 아무도 안 나왔다 (10-21)
                            string id = (user["user_id"] ?? user["id"])?.ToString();
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
                // 팔로잉만 보기 — 목록이 오기 전엔 아무도 안 맞아 비어 있었다. 바뀌었으면 아바타·장소 목록을 다시
                if (userFilterMode == UserFilterMode.FollowingOnly && !before.SetEquals(followingUserIds))
                {
                    UpdateVisibleAvatars();
                    UserFilterChanged?.Invoke();
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

    private void OnApplicationPause(bool pauseStatus)
    {
        isInBackground = pauseStatus;

        if (pauseStatus)
        {
            // 백그라운드 진입
            Log("Entering background mode");
            // Private 모드면 추적 중지
            if (locationVisibilityMode == LocationVisibilityMode.Private)
            {
                if (positionUpdateCoroutine != null)
                {
                    StopCoroutine(positionUpdateCoroutine);
                    positionUpdateCoroutine = null;
                }
            }
            // Public/FollowingOnly: SendPositionUpdates 코루틴이 계속 실행되며
            // 다음 루프에서 backgroundUpdateInterval(60초) 대기
        }
        else
        {
            // 포그라운드 복귀
            Log("Returning to foreground");
            ResetFixGate();
            if (isRegistered && positionUpdateCoroutine == null &&
                locationVisibilityMode != LocationVisibilityMode.Private)
            {
                positionUpdateCoroutine = StartCoroutine(SendPositionUpdates());
            }
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Android에서 OnApplicationPause와 함께 호출될 수 있음
        if (!hasFocus && !isInBackground)
        {
            isInBackground = true;
        }
        else if (hasFocus && isInBackground)
        {
            isInBackground = false;
            if (isRegistered && positionUpdateCoroutine == null &&
                locationVisibilityMode != LocationVisibilityMode.Private)
            {
                positionUpdateCoroutine = StartCoroutine(SendPositionUpdates());
            }
        }
    }

    void OnDestroy()
    {
        if (Instance == this && LoginManager.Instance != null)
        {
            LoginManager.Instance.OnLoginStateChanged -= OnLoginStateChanged;
            LoginManager.Instance.OnLoggingOut -= OnLoggingOut;
        }
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
        nearbyUsersData.Remove(userId);
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
