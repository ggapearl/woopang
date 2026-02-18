using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Globalization;

#if UNITY_ANDROID
using Firebase.Messaging;
using Firebase.Extensions;
using UnityEngine.Android;
using Unity.Notifications.Android;
#endif

#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

[System.Serializable]
public class MessageData
{
    public string title;
    public string body;
    public string message;
    public bool isRead;
    public string timestampString;
    public string messageId;
    public string receivedAtString;
    public float messageLat;
    public float messageLon;
    public float messageRadius;
    public string currentDistance;
}

[System.Serializable]
public class PendingMessagesWrapper
{
    public List<MessageData> messages;
}

[System.Serializable]
public class MessageDataList
{
    public List<MessageData> messages;
}

[System.Serializable]
public class TokenResponse
{
    public string message;
    public bool location_processed;
    public int success_count;
    public int failure_count;
}

public static class TimeLocalization
{
    private static Dictionary<string, Dictionary<string, string>> localizations = new Dictionary<string, Dictionary<string, string>>()
    {
        ["en"] = new Dictionary<string, string>()
        {
            ["just_now"] = "Just now",
            ["minutes_ago"] = "{0} minutes ago",
            ["minute_ago"] = "1 minute ago",
            ["hours_ago"] = "{0} hours ago",
            ["hour_ago"] = "1 hour ago",
            ["location_service_off"] = "Location off"
        },
        ["ko"] = new Dictionary<string, string>()
        {
            ["just_now"] = "방금",
            ["minutes_ago"] = "{0}분 전",
            ["minute_ago"] = "1분 전",
            ["hours_ago"] = "{0}시간 전",
            ["hour_ago"] = "1시간 전",
            ["location_service_off"] = "위치 꺼짐"
        }
    };

    public static string GetLocalizedTime(string key, string langCode, params object[] args)
    {
        if (!localizations.ContainsKey(langCode))
            langCode = "en";

        if (localizations[langCode].ContainsKey(key))
            return string.Format(localizations[langCode][key], args);

        return key;
    }
}

public class FirebaseNotification : MonoBehaviour
{
    public static FirebaseNotification Instance;

    [Header("UI References")]
    [Tooltip("안읽은 알림 인디케이터 (선택사항)")]
    public Image UnreadIndicatorIcon;

    [Header("=== 인앱 알림 배너 ===")]
    [Tooltip("알림 배너 패널 (자동 생성됨)")]
    public GameObject notificationBanner;
    public Text bannerTitleText;
    public Text bannerBodyText;
    public Button bannerButton;

    [Header("알림 배너 설정")]
    public float bannerDisplayDuration = 4f;
    public float bannerSlideSpeed = 800f;

    private RectTransform bannerRectTransform;

    // Cached font to avoid repeated Resources.Load calls
    private static Font _cachedFont;
    private static Font CachedFont => _cachedFont ?? (_cachedFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM"));
    private Coroutine bannerCoroutine;
    private float bannerHeight = 120f;

    // 알림 관리를 위한 변수들
    private const int MAX_ACTIVE_NOTIFICATIONS = 5;
    private List<int> activeNotificationIds = new List<int>();

    private string currentFCMToken = "";

    private HashSet<string> processedMessageIds = new HashSet<string>();
    private Dictionary<string, DateTime> recentMessageTracker = new Dictionary<string, DateTime>();
    private const int DuplicateMessageTimeWindow = 600;

    private bool isLocationServiceEnabled = false;
    private Vector2 currentUserLocation = Vector2.zero;

    // 클릭 시 열 대화 정보
    private string pendingOpenUserId;
    private string pendingOpenUsername;

    public static FirebaseNotification GetInstance()
    {
        if (Instance == null)
        {
            Instance = GameObject.Find("FirebaseManager")?.GetComponent<FirebaseNotification>();
        }
        return Instance;
    }

    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            string savedToken = PlayerPrefs.GetString("FCMToken", "");
            if (!string.IsNullOrEmpty(savedToken) && string.IsNullOrEmpty(Instance.currentFCMToken))
            {
                Instance.currentFCMToken = savedToken;
            }
            Destroy(gameObject);
            return;
        }

        Time.timeScale = 1f;
        InitializeLocationService();
        LoadProcessedMessageIds();
        InitializeBannerRect();

#if UNITY_ANDROID
        RequestNotificationPermission();
        InitializeAndroidNotificationChannel();
        StartNotificationCleanup();

        // Firebase는 Android에서만 사용
        FirebaseMessaging.TokenReceived += OnTokenReceived;
        FirebaseMessaging.MessageReceived += OnMessageReceived;
        StartCoroutine(InitializeFirebaseCoroutine());
#elif UNITY_IOS
        // iOS는 네이티브 APNs 사용 (UnityAppController.mm에서 처리)
        // OnNativeTokenReceived와 OnNativeMessageReceived 콜백 사용
        LoadTokenFromPlayerPrefs();

        // iOS 앱 실행 시 백그라운드 알림 처리
        var notification = Unity.Notifications.iOS.iOSNotificationCenter.GetLastRespondedNotification();
        if (notification != null)
        {
            string title = notification.Title;
            string body = notification.Body;
            float targetLat = 0f;
            float targetLon = 0f;
            float radius = 0f;

            string msgType = "";
            string senderId = "";
            string senderUsername = "";

            if (notification.UserInfo != null)
            {
                if (notification.UserInfo.ContainsKey("latitude"))
                    float.TryParse(notification.UserInfo["latitude"], out targetLat);
                if (notification.UserInfo.ContainsKey("longitude"))
                    float.TryParse(notification.UserInfo["longitude"], out targetLon);
                if (notification.UserInfo.ContainsKey("radius"))
                    float.TryParse(notification.UserInfo["radius"], out radius);

                if (notification.UserInfo.ContainsKey("msg_type"))
                    msgType = notification.UserInfo["msg_type"];
                else if (notification.UserInfo.ContainsKey("type"))
                    msgType = notification.UserInfo["type"];

                if (notification.UserInfo.ContainsKey("sender_id"))
                    senderId = notification.UserInfo["sender_id"];

                if (notification.UserInfo.ContainsKey("sender_username"))
                    senderUsername = notification.UserInfo["sender_username"];
                else if (notification.UserInfo.ContainsKey("sender"))
                    senderUsername = notification.UserInfo["sender"];
            }

            ProcessNativeMessage(title, body, targetLat, targetLon, radius, msgType, senderId, senderUsername);
        }
#endif

        StartCoroutine(CheckBackgroundNotificationOnStartup());
    }

    private void LoadTokenFromPlayerPrefs()
    {
        string savedToken = PlayerPrefs.GetString("FCMToken", "");
        if (!string.IsNullOrEmpty(savedToken))
        {
            currentFCMToken = savedToken;
        }

        // APNs 토큰도 확인
        string apnsToken = PlayerPrefs.GetString("APNSDeviceToken", "");
        if (!string.IsNullOrEmpty(apnsToken))
        {
            currentFCMToken = apnsToken;
        }
    }


    private void InitializeLocationService()
    {
        if (Input.location.isEnabledByUser)
        {
            isLocationServiceEnabled = true;
            Input.location.Start(10f, 1f);
            StartCoroutine(UpdateLocationPeriodically());
        }
        else
        {
            isLocationServiceEnabled = false;
        }
    }

    private IEnumerator UpdateLocationPeriodically()
    {
        while (isLocationServiceEnabled)
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                currentUserLocation = new Vector2(
                    Input.location.lastData.latitude,
                    Input.location.lastData.longitude
                );
                SaveLocationToAndroidPrefs(currentUserLocation.x, currentUserLocation.y);

                // [FIX] 주기적으로 서버에 위치 정보 업데이트 전송
                if (!string.IsNullOrEmpty(currentFCMToken))
                {
                    StartCoroutine(SendTokenToServer(currentFCMToken));
                }
            }
            yield return new WaitForSeconds(30f);
        }
    }

    private void SaveLocationToAndroidPrefs(float latitude, float longitude)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject sharedPrefs = currentActivity.Call<AndroidJavaObject>("getSharedPreferences", "firebase_messages", 0))
            using (AndroidJavaObject editor = sharedPrefs.Call<AndroidJavaObject>("edit"))
            {
                editor.Call<AndroidJavaObject>("putString", "last_latitude", latitude.ToString("F6"));
                editor.Call<AndroidJavaObject>("putString", "last_longitude", longitude.ToString("F6"));
                editor.Call("apply");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save location to Android prefs: {ex.Message}");
        }
#endif
    }

    private IEnumerator InitializeFirebaseCoroutine()
    {
#if UNITY_ANDROID
        bool initializationComplete = false;
        Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;

        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            dependencyStatus = task.Result;
            initializationComplete = true;
        });

        float timeout = 30f;
        while (!initializationComplete && timeout > 0)
        {
            yield return new WaitForSeconds(1f);
            timeout -= 1f;
        }

        if (initializationComplete && dependencyStatus == Firebase.DependencyStatus.Available)
        {
            // 이벤트 핸들러는 Start()에서 이미 등록됨 - 중복 등록 제거
            StartCoroutine(CheckBackgroundNotification());
        }
#else
        yield break;
#endif
    }

    private string DateTimeToString(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private DateTime StringToDateTime(string timeString)
    {
        if (string.IsNullOrEmpty(timeString))
            return DateTime.Now;
            
        if (DateTime.TryParse(timeString, out DateTime result))
            return result;
            
        return DateTime.Now;
    }

    private void LoadProcessedMessageIds()
    {
        string processedIds = PlayerPrefs.GetString("ProcessedMessageIds", "");
        if (!string.IsNullOrEmpty(processedIds))
        {
            try
            {
                string[] ids = processedIds.Split('|');
                processedMessageIds = new HashSet<string>(ids);
            }
            catch (System.Exception)
            {
                processedMessageIds = new HashSet<string>();
            }
        }
    }

    private void SaveProcessedMessageIds()
    {
        try
        {
            string[] ids = new string[processedMessageIds.Count];
            processedMessageIds.CopyTo(ids);
            string processedIds = string.Join("|", ids);
            PlayerPrefs.SetString("ProcessedMessageIds", processedIds);
            PlayerPrefs.Save();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save processed message IDs: {ex.Message}");
        }
    }

    private string GenerateMessageId(string title, string body, DateTime timestamp)
    {
        string combined = $"{title}_{body}_{timestamp:yyyyMMddHHmmss}";
        return combined.GetHashCode().ToString();
    }

#if UNITY_ANDROID
    private DateTime ParseServerTimestamp(Firebase.Messaging.FirebaseMessage message)
    {
        DateTime serverTime = DateTime.Now;
        
        if (message.Data != null)
        {
            string[] timestampKeys = { "timestamp", "sent_time", "server_time", "created_at" };
            
            foreach (string key in timestampKeys)
            {
                if (message.Data.ContainsKey(key))
                {
                    string timestampStr = message.Data[key];
                    
                    if (long.TryParse(timestampStr, out long unixTimestamp))
                    {
                        try
                        {
                            serverTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
                            return serverTime;
                        }
                        catch
                        {
                            try
                            {
                                serverTime = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestamp).DateTime;
                                return serverTime;
                            }
                            catch { }
                        }
                    }
                    
                    if (DateTime.TryParse(timestampStr, out DateTime parsedTime))
                    {
                        serverTime = parsedTime;
                        return serverTime;
                    }
                }
            }
        }
        
        return serverTime;
    }
#endif

    /// <summary>
    /// 메시지 ID를 처리됨으로 표시 (중복 방지)
    /// </summary>
    private bool MarkMessageAsProcessed(string messageId, string title, string body, DateTime serverTimestamp)
    {
        if (string.IsNullOrEmpty(messageId))
        {
            messageId = GenerateMessageId(title, body, serverTimestamp);
        }

        if (processedMessageIds.Contains(messageId))
        {
            return false;
        }

        processedMessageIds.Add(messageId);
        SaveProcessedMessageIds();

        return true;
    }

    private string GetCurrentLanguageCode()
    {
        string langCode = Application.systemLanguage switch
        {
            SystemLanguage.Korean => "ko",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.Chinese => "zh",
            SystemLanguage.ChineseSimplified => "zh",
            SystemLanguage.ChineseTraditional => "zh",
            SystemLanguage.Spanish => "es",
            _ => "en"
        };
        return langCode;
    }


    private void LoadTokenFromAndroidPrefs()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject sharedPrefs = currentActivity.Call<AndroidJavaObject>("getSharedPreferences", "firebase_messages", 0))
            {
                string token = sharedPrefs.Call<string>("getString", "FCMToken", "");

                if (!string.IsNullOrEmpty(token))
                {
                    currentFCMToken = token;

                    PlayerPrefs.SetString("FCMToken", token);
                    PlayerPrefs.Save();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to load token from Android: {ex.Message}");
        }
#endif
    }

    private IEnumerator SendTokenToServer(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            yield break;
        }

        float latitude = 0f;
        float longitude = 0f;
        bool locationConsent = false;
        
        if (Input.location.isEnabledByUser)
        {
            Input.location.Start(10f, 1f);
            int maxWait = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
            {
                yield return new WaitForSeconds(1);
                maxWait--;
            }
            
            if (Input.location.status == LocationServiceStatus.Running)
            {
                latitude = Input.location.lastData.latitude;
                longitude = Input.location.lastData.longitude;
                locationConsent = true;
            }
            
            Input.location.Stop();
        }
        
        WWWForm form = new WWWForm();
        form.AddField("token", token);
        form.AddField("device_id", SystemInfo.deviceUniqueIdentifier);
        form.AddField("device_name", SystemInfo.deviceName);
        form.AddField("device_model", SystemInfo.deviceModel);
        form.AddField("os_version", SystemInfo.operatingSystem);
        form.AddField("app_version", Application.version);

        // user_id 추가 (로그인된 경우)
        if (LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn)
        {
            string userId = LoginManager.Instance.CurrentUserId.ToString();
            if (!string.IsNullOrEmpty(userId) && userId != "0")
            {
                form.AddField("user_id", userId);
            }
        }

#if UNITY_IOS
        form.AddField("platform", "ios");
#elif UNITY_ANDROID
        form.AddField("platform", "android");
#else
        form.AddField("platform", "unknown");
#endif
        
        if (locationConsent && latitude != 0f && longitude != 0f)
        {
            form.AddField("latitude", latitude.ToString("F6"));
            form.AddField("longitude", longitude.ToString("F6"));
            form.AddField("location_consent", "true");
        }
        else
        {
            form.AddField("location_consent", "false");
        }
        
        UnityWebRequest request = UnityWebRequest.Post(ApiConfig.REGISTER_TOKEN, form);
        request.timeout = 10;
        
        yield return request.SendWebRequest();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            StartCoroutine(CheckBackgroundNotification());
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            StartCoroutine(CheckBackgroundNotification());

            // [FIX] 포그라운드 복귀 시 위치 정보 즉시 업데이트
            if (!string.IsNullOrEmpty(currentFCMToken))
            {
                StartCoroutine(SendTokenToServer(currentFCMToken));
            }
        }
    }

#if UNITY_ANDROID
    void RequestNotificationPermission()
    {
        if (!Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
        {
            Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
            StartCoroutine(CheckPermissionStatus());
        }
    }

    private IEnumerator CheckPermissionStatus()
    {
        yield return new WaitForSeconds(1);
    }

    void InitializeAndroidNotificationChannel()
    {
        var channel = new AndroidNotificationChannel()
        {
            Id = "woopang_channel_high",
            Name = "Default Channel",
            Importance = Importance.High,
            Description = "Generic notifications",
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
    }
#endif

#if UNITY_ANDROID
    void OnTokenReceived(object sender, TokenReceivedEventArgs token)
    {
        if (Instance == null || Instance != this)
        {
            Instance = this;
        }

        if (string.IsNullOrEmpty(token.Token))
        {
            return;
        }

        StartCoroutine(UpdateFirebaseStatusOnMainThread(token.Token));

        PlayerPrefs.SetString("FCMToken", token.Token);
        PlayerPrefs.Save();

        // [FIX] 'all' 주제 구독 (전체 알림 수신용)
        FirebaseMessaging.SubscribeAsync("all");

        StartCoroutine(SendTokenToServer(token.Token));
    }

    private IEnumerator UpdateFirebaseStatusOnMainThread(string token)
    {
        yield return null;

        currentFCMToken = token;
    }
#endif

#if UNITY_ANDROID
    void OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        string title = "";
        string body = "";

        if (e.Message.Notification != null)
        {
            title = e.Message.Notification.Title ?? "";
            body = e.Message.Notification.Body ?? "";
        }

        if (e.Message.Data != null)
        {
            if (string.IsNullOrEmpty(title) && e.Message.Data.ContainsKey("title"))
                title = e.Message.Data["title"];
            if (string.IsNullOrEmpty(body) && e.Message.Data.ContainsKey("body"))
                body = e.Message.Data["body"];
        }

        if (string.IsNullOrEmpty(title)) title = "알림";
        if (string.IsNullOrEmpty(body)) body = "새 메시지가 도착했습니다.";

        string originalTitle = title;
        string distanceString = CalculateMessageDistance(e.Message);
        if (!string.IsNullOrEmpty(distanceString))
        {
            title = $"{originalTitle} - {distanceString}";
        }

        DateTime serverTimestamp = ParseServerTimestamp(e.Message);

        string messageId = null;
        if (e.Message.Data != null && e.Message.Data.ContainsKey("message_id"))
        {
            messageId = e.Message.Data["message_id"];
        }
        else
        {
            messageId = GenerateMessageId(originalTitle, body, serverTimestamp);
        }

        // 중복 메시지 스킵
        if (processedMessageIds.Contains(messageId))
        {
            return;
        }

        if (Application.isFocused)
        {
            HandleForegroundMessage(title, body, serverTimestamp, messageId, e);
        }
        else
        {
            // 백그라운드에서는 코루틴이 돌지 않으므로 직접 호출 (void로 변경 예정)
            HandleBackgroundMessage(title, body, serverTimestamp, messageId, e);
        }
    }
#endif

#if UNITY_ANDROID
    private void HandleForegroundMessage(string title, string body, DateTime serverTimestamp, string messageId, MessageReceivedEventArgs e)
    {
        // 메시지 타입 확인 (type 또는 notification_type 키 모두 확인)
        string messageType = "";
        if (e.Message.Data != null)
        {
            if (e.Message.Data.ContainsKey("type"))
                messageType = e.Message.Data["type"];
            else if (e.Message.Data.ContainsKey("notification_type"))
                messageType = e.Message.Data["notification_type"];
        }


        // === 업로드 완료/승인 알림 처리 ===
        if (messageType == "upload_complete" || messageType == "upload_approved")
        {
            string contentName = "AR 콘텐츠";
            if (e.Message.Data != null && e.Message.Data.ContainsKey("content_name"))
            {
                contentName = e.Message.Data["content_name"];
            }

            // MessagePanelManager에 시스템 알림으로 추가
            var manager = GetMessagePanelManager();
            if (manager != null)
            {
                manager.AddLocationNotificationFromPush(title, body, messageId);
            }

            // 중복 방지용 메시지 ID 저장
            MarkMessageAsProcessed(messageId, title, body, serverTimestamp);

#if UNITY_ANDROID
            var uploadNotif = new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = System.DateTime.Now,
                SmallIcon = "icon_0",
                LargeIcon = "icon_1",
                ShouldAutoCancel = true
            };
            AndroidNotificationCenter.SendNotification(uploadNotif, "woopang_channel_high");
#endif

            return;
        }

        // === DM 메시지 처리 ===
        bool isDMMessage = false;
        if (e.Message.Data != null)
        {
            if (messageType == "dm")
                isDMMessage = true;
            if (e.Message.Data.ContainsKey("msg_type") && e.Message.Data["msg_type"] == "dm")
                isDMMessage = true;
        }

        // DM 메시지면 MessagePanelManager에 즉시 추가
        if (isDMMessage)
        {
            // FCM 데이터에서 발신자 정보 추출
            string senderId = "";
            string senderUsername = "";
            string messageContent = body;

            if (e.Message.Data != null)
            {
                if (e.Message.Data.ContainsKey("sender_id"))
                    senderId = e.Message.Data["sender_id"];
                if (e.Message.Data.ContainsKey("sender_username"))
                    senderUsername = e.Message.Data["sender_username"];
                else if (e.Message.Data.ContainsKey("sender"))
                    senderUsername = e.Message.Data["sender"];

                // body에서 "username: " 접두사 제거
                if (!string.IsNullOrEmpty(senderUsername) && messageContent.StartsWith(senderUsername + ": "))
                {
                    messageContent = messageContent.Substring(senderUsername.Length + 2);
                }
            }

            // MessagePanelManager를 안전하게 찾기
            var manager = GetMessagePanelManager();

            if (manager != null && !string.IsNullOrEmpty(senderId))
            {
                try
                {
                    manager.AddOrUpdateConversationFromPush(senderId, senderUsername, messageContent);
                }
                catch (System.Exception)
                {
                    // 예외 무시 - DM 추가 실패해도 앱 동작에 영향 없음
                }
            }

#if UNITY_ANDROID
            try
            {
                var channel = new AndroidNotificationChannel()
                {
                    Id = "woopang_channel_high",
                    Name = "Default Channel",
                    Importance = Importance.High,
                    Description = "Generic notifications",
                };
                AndroidNotificationCenter.RegisterNotificationChannel(channel);

                // DM 메시지도 포그라운드에서 시스템 알림 표시
                var dmNotification = new AndroidNotification
                {
                    Title = title,
                    Text = body,
                    FireTime = System.DateTime.Now.AddSeconds(0.5f),
                    SmallIcon = "icon_0",
                    LargeIcon = "icon_1",
                    ShouldAutoCancel = true,
                    Group = "woopang_dm"
                };
                
                int dmNotificationId = (int)(System.DateTime.Now.Ticks % int.MaxValue);
                AndroidNotificationCenter.SendNotification(dmNotification, "woopang_channel_high");
                activeNotificationIds.Add(dmNotificationId);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FirebaseNotification] Failed to send foreground notification: {ex.Message}");
            }
#endif

            return; // DM은 위치 기반 알림 저장 안 함
        }

        // 위치 기반 알림 처리

        float targetLat = 0f, targetLon = 0f, radius = 0f;
        string currentDistance = "";

        if (e.Message.Data != null)
        {
            if (e.Message.Data.ContainsKey("latitude"))
                float.TryParse(e.Message.Data["latitude"], out targetLat);
            if (e.Message.Data.ContainsKey("longitude"))
                float.TryParse(e.Message.Data["longitude"], out targetLon);
            if (e.Message.Data.ContainsKey("radius"))
                float.TryParse(e.Message.Data["radius"], out radius);
        }

        currentDistance = ExtractDistanceFromTitle(title);

        // MessagePanelManager에 위치 알림 추가 (로그인 없이도 저장됨)
        if (MessagePanelManager.Instance != null)
        {
            MessagePanelManager.Instance.AddLocationNotificationFromPush(
                title, body, messageId,
                targetLat, targetLon, radius, currentDistance
            );
        }

        // 중복 방지용 메시지 ID 저장
        MarkMessageAsProcessed(messageId, title, body, serverTimestamp);

        // 스마트 알림 관리
        ManageActiveNotifications();

#if UNITY_ANDROID
        // Android 시스템 알림 (잠금화면/상태바용)
        int badgeCount = MessagePanelManager.Instance != null ? MessagePanelManager.Instance.GetUnreadCount() : 1;
        var notification = new AndroidNotification
        {
            Title = title,
            Text = body,
            FireTime = System.DateTime.Now,
            SmallIcon = "icon_0",
            LargeIcon = "icon_1",
            ShouldAutoCancel = true,
            Number = badgeCount
        };

        int newNotificationId = (int)System.DateTime.Now.Ticks;
        AndroidNotificationCenter.SendNotification(notification, "woopang_channel_high");

        // 활성 알림 ID 추가
        activeNotificationIds.Add(newNotificationId);
#endif
    }
#endif

#if UNITY_ANDROID
    private void HandleBackgroundMessage(string title, string body, DateTime serverTimestamp, string messageId, MessageReceivedEventArgs e)
    {
        // 메시지 타입 확인 (type 또는 notification_type 키 모두 확인)
        string messageType = "";
        if (e.Message.Data != null)
        {
            if (e.Message.Data.ContainsKey("type"))
                messageType = e.Message.Data["type"];
            else if (e.Message.Data.ContainsKey("notification_type"))
                messageType = e.Message.Data["notification_type"];
        }

        // === 업로드 완료/승인 알림 처리 ===
        if (messageType == "upload_complete" || messageType == "upload_approved")
        {
            string contentName = "AR 콘텐츠";
            if (e.Message.Data != null && e.Message.Data.ContainsKey("content_name"))
            {
                contentName = e.Message.Data["content_name"];
            }

            // MessagePanelManager에 시스템 알림으로 추가
            var manager = GetMessagePanelManager();
            if (manager != null)
            {
                manager.AddLocationNotificationFromPush(title, body, messageId);
            }

            // 중복 방지용 메시지 ID 저장
            MarkMessageAsProcessed(messageId, title, body, serverTimestamp);

#if UNITY_ANDROID
            var uploadNotif = new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = System.DateTime.Now.AddSeconds(0.5f),
                SmallIcon = "icon_0",
                LargeIcon = "icon_1",
                ShouldAutoCancel = true,
                Group = "woopang_system"
            };
            int uploadNotifId = (int)(System.DateTime.Now.Ticks % int.MaxValue);
            AndroidNotificationCenter.SendNotification(uploadNotif, "woopang_channel_high");
            activeNotificationIds.Add(uploadNotifId);
#endif

            return;
        }

        // === DM 메시지 처리 ===
        bool isDMMessage = false;
        if (e.Message.Data != null)
        {
            if (messageType == "dm")
                isDMMessage = true;
            if (e.Message.Data.ContainsKey("msg_type") && e.Message.Data["msg_type"] == "dm")
                isDMMessage = true;
        }

        // DM 메시지면 MessagePanelManager에 즉시 추가
        if (isDMMessage)
        {
            // FCM 데이터에서 발신자 정보 추출
            string senderId = "";
            string senderUsername = "";
            string messageContent = body;

            if (e.Message.Data != null)
            {
                if (e.Message.Data.ContainsKey("sender_id"))
                    senderId = e.Message.Data["sender_id"];
                if (e.Message.Data.ContainsKey("sender_username"))
                    senderUsername = e.Message.Data["sender_username"];
                else if (e.Message.Data.ContainsKey("sender"))
                    senderUsername = e.Message.Data["sender"];

                // body에서 "username: " 접두사 제거
                if (!string.IsNullOrEmpty(senderUsername) && messageContent.StartsWith(senderUsername + ": "))
                {
                    messageContent = messageContent.Substring(senderUsername.Length + 2);
                }
            }

            // MessagePanelManager를 안전하게 찾기
            var manager = GetMessagePanelManager();

            if (manager != null && !string.IsNullOrEmpty(senderId))
            {
                try
                {
                    manager.AddOrUpdateConversationFromPush(senderId, senderUsername, messageContent);
                }
                catch (System.Exception)
                {
                    // 예외 무시
                }
            }

#if UNITY_ANDROID
            try
            {
                // 채널이 없는 경우를 대비해 다시 등록
                var channel = new AndroidNotificationChannel()
                {
                    Id = "woopang_channel_high",
                    Name = "Default Channel",
                    Importance = Importance.High,
                    Description = "Generic notifications",
                };
                AndroidNotificationCenter.RegisterNotificationChannel(channel);

                // DM 메시지도 백그라운드에서 시스템 알림 표시
                var dmNotification = new AndroidNotification
                {
                    Title = title,
                    Text = body,
                    FireTime = System.DateTime.Now.AddSeconds(0.5f), // 0.5초 뒤 발송
                    SmallIcon = "icon_0",
                    LargeIcon = "icon_1",
                    ShouldAutoCancel = true,
                    Group = "woopang_dm"
                };
                
                int dmNotificationId = (int)(System.DateTime.Now.Ticks % int.MaxValue);
                AndroidNotificationCenter.SendNotification(dmNotification, "woopang_channel_high");
                activeNotificationIds.Add(dmNotificationId);
                
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FirebaseNotification] Failed to send background notification: {ex.Message}");
            }
#endif

            return; // DM은 위치 기반 알림 저장 안 함
        }

        // 위치 기반 알림 처리 (백그라운드)

        float targetLat = 0f, targetLon = 0f, radius = 0f;
        string currentDistance = "";

        if (e.Message.Data != null)
        {
            if (e.Message.Data.ContainsKey("latitude"))
                float.TryParse(e.Message.Data["latitude"], out targetLat);
            if (e.Message.Data.ContainsKey("longitude"))
                float.TryParse(e.Message.Data["longitude"], out targetLon);
            if (e.Message.Data.ContainsKey("radius"))
                float.TryParse(e.Message.Data["radius"], out radius);
        }

        currentDistance = ExtractDistanceFromTitle(title);

        // MessagePanelManager에 위치 알림 추가 (로그인 없이도 저장됨)
        if (MessagePanelManager.Instance != null)
        {
            MessagePanelManager.Instance.AddLocationNotificationFromPush(
                title, body, messageId,
                targetLat, targetLon, radius, currentDistance
            );
        }

        // 중복 방지용 메시지 ID 저장
        MarkMessageAsProcessed(messageId, title, body, serverTimestamp);

        // 스마트 알림 관리
        ManageActiveNotifications();

#if UNITY_ANDROID
        // Android 시스템 알림 (잠금화면/상태바용)
        int badgeCount = MessagePanelManager.Instance != null ? MessagePanelManager.Instance.GetUnreadCount() : 1;
        var notification = new AndroidNotification
        {
            Title = title,
            Text = body,
            FireTime = System.DateTime.Now.AddSeconds(0.5f),
            SmallIcon = "icon_0",
            LargeIcon = "icon_1",
            ShouldAutoCancel = true,
            Number = badgeCount,
            Group = "woopang_messages"
        };

        int newNotificationId = (int)System.DateTime.Now.Ticks;
        AndroidNotificationCenter.SendNotification(notification, "woopang_channel_high");

        // 활성 알림 ID 추가
        activeNotificationIds.Add(newNotificationId);
#endif
    }
#endif

    private IEnumerator CheckBackgroundNotification()
    {
#if UNITY_ANDROID
        var notificationIntent = AndroidNotificationCenter.GetLastNotificationIntent();
        if (notificationIntent != null)
        {
            string title = notificationIntent.Notification.Title ?? "알림";
            string body = notificationIntent.Notification.Text ?? "";

            if (IsDuplicateMessage(title, body))
            {
                yield break;
            }

            DateTime timestamp = DateTime.Now;
            string messageId = GenerateMessageId(title, body, timestamp);

            // MessagePanelManager에 시스템 알림 추가
            if (MessagePanelManager.Instance != null)
            {
                MessagePanelManager.Instance.AddLocationNotificationFromPush(title, body, messageId);
            }

            // 중복 방지용 메시지 ID 저장
            MarkMessageAsProcessed(messageId, title, body, timestamp);
        }
#elif UNITY_IOS
        // iOS는 시스템 알림 센터가 백그라운드 알림을 자동 처리
        // 앱 활성화 시 FCM OnMessageReceived로 처리됨
#endif
        yield break;
    }

    // 활성 알림 스마트 관리
    private void ManageActiveNotifications()
    {
#if UNITY_ANDROID
        // 만료된 알림 ID 정리
        CleanExpiredNotificationIds();

        // 최대 개수 초과 시 오래된 알림 삭제
        while (activeNotificationIds.Count >= MAX_ACTIVE_NOTIFICATIONS)
        {
            int oldestId = activeNotificationIds[0];
            AndroidNotificationCenter.CancelNotification(oldestId);
            activeNotificationIds.RemoveAt(0);
        }
#endif
    }

    // 만료된 알림 ID 정리
    private void CleanExpiredNotificationIds()
    {
        // Android에서 이미 만료된 알림들은 자동으로 사라지므로
        // 너무 오래된 ID들은 리스트에서 제거
        if (activeNotificationIds.Count > MAX_ACTIVE_NOTIFICATIONS * 2)
        {
            // 절반만 남기고 나머지 제거
            int removeCount = activeNotificationIds.Count - MAX_ACTIVE_NOTIFICATIONS;
            activeNotificationIds.RemoveRange(0, removeCount);
        }
    }

    // 모든 알림 정리 (앱 종료 시 호출)
    public void ClearAllNotifications()
    {
#if UNITY_ANDROID
        foreach (int notificationId in activeNotificationIds)
        {
            AndroidNotificationCenter.CancelNotification(notificationId);
        }
#endif
        activeNotificationIds.Clear();
    }

#if UNITY_ANDROID
    // 자동 알림 정리 시스템 (시간 기반)
    private void StartNotificationCleanup()
    {
        StartCoroutine(NotificationCleanupLoop());
    }

    private IEnumerator NotificationCleanupLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(180); // 3분마다 정리

            // 오래된 알림들 자동 삭제 (포그라운드: 5분, 백그라운드: 3분)
            AutoCleanOldNotifications();
        }
    }

    // 시간 기반 알림 자동 정리
    private void AutoCleanOldNotifications()
    {
        if (activeNotificationIds.Count > 0)
        {
            // 가장 오래된 알림부터 삭제 (새로운 알림을 위한 공간 확보)
            int notificationsToRemove = Mathf.Max(0, activeNotificationIds.Count - MAX_ACTIVE_NOTIFICATIONS + 1);

            for (int i = 0; i < notificationsToRemove; i++)
            {
                if (activeNotificationIds.Count > 0)
                {
                    int oldestId = activeNotificationIds[0];
                    AndroidNotificationCenter.CancelNotification(oldestId);
                    activeNotificationIds.RemoveAt(0);
                }
            }
        }
    }
#endif

    private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000;
        float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
        float dLon = (lon2 - lon1) * Mathf.Deg2Rad;
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
        return R * c;
    }

    [Serializable]
    private class NativeMessageData
    {
        public string title;
        public string body;
        public string latitude;
        public string longitude;
        public string radius;
        public string msg_type;
        public string type;
        public string sender_id;
        public string sender_username;
        public string sender;
    }

    public void OnNativeMessageReceived(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        string title = "";
        string body = "";
        float targetLat = 0f;
        float targetLon = 0f;
        float radius = 0f;
        string msgType = "";
        string senderId = "";
        string senderUsername = "";

        // 1. Try JSON parsing
        if (message.Trim().StartsWith("{"))
        {
            try
            {
                var data = JsonUtility.FromJson<NativeMessageData>(message);
                if (data != null)
                {
                    title = data.title;
                    body = data.body;
                    float.TryParse(data.latitude, out targetLat);
                    float.TryParse(data.longitude, out targetLon);
                    float.TryParse(data.radius, out radius);
                    
                    msgType = !string.IsNullOrEmpty(data.msg_type) ? data.msg_type : data.type;
                    senderId = data.sender_id;
                    senderUsername = !string.IsNullOrEmpty(data.sender_username) ? data.sender_username : data.sender;

                    ProcessNativeMessage(title, body, targetLat, targetLon, radius, msgType, senderId, senderUsername);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirebaseNotification] JSON parse error: {ex.Message}");
            }
        }

        // 2. Try Pipe-separated parsing (Legacy)
        if (message.Contains("|"))
        {
            string[] parts = message.Split('|');
            if (parts.Length >= 2)
            {
                title = parts[0];
                body = parts[1];

                if (parts.Length >= 5)
                {
                    if (float.TryParse(parts[2], out targetLat) &&
                        float.TryParse(parts[3], out targetLon))
                    {
                        float.TryParse(parts[4], out radius);
                    }
                }
                
                ProcessNativeMessage(title, body, targetLat, targetLon, radius);
            }
        }
    }

    private void ProcessNativeMessage(string title, string body, float targetLat, float targetLon, float radius, string msgType = "", string senderId = "", string senderUsername = "")
    {
        // DM Handling
        if (msgType == "dm" || msgType == "DM")
        {
            var manager = FindFirstObjectByType<MessagePanelManager>();
            if (manager != null && !string.IsNullOrEmpty(senderId))
            {
                string messageContent = body;
                if (!string.IsNullOrEmpty(senderUsername) && messageContent.StartsWith(senderUsername + ": "))
                {
                    messageContent = messageContent.Substring(senderUsername.Length + 2);
                }
                
                manager.AddOrUpdateConversationFromPush(senderId, senderUsername, messageContent);

                // iOS 포그라운드에서는 시스템 알림이 안 뜨므로 인앱 배너 표시
                ShowInAppNotification(title, body, senderId, senderUsername);
            }
            return;
        }

        string distanceString = "";

        if (targetLat != 0 && targetLon != 0)
        {
            bool alreadyHasDistance = title.Contains(" - ") && (title.EndsWith("m") || title.EndsWith("km"));

            if (alreadyHasDistance)
            {
                int lastDashIndex = title.LastIndexOf(" - ");
                if (lastDashIndex > 0)
                {
                    distanceString = title.Substring(lastDashIndex + 3);
                }
            }
            else
            {
                if (isLocationServiceEnabled && currentUserLocation != Vector2.zero)
                {
                    float distance = CalculateDistanceInMeters(
                        currentUserLocation.x, currentUserLocation.y,
                        targetLat, targetLon
                    );

                    distanceString = FormatDistance(distance);
                    title = $"{title} - {distanceString}";
                }
            }
        }

        DateTime timestamp = DateTime.Now;
        string messageId = GenerateMessageId(title, body, timestamp);

        // MessagePanelManager에 시스템 알림 추가
        if (MessagePanelManager.Instance != null)
        {
            MessagePanelManager.Instance.AddLocationNotificationFromPush(
                title, body, messageId, targetLat, targetLon, radius, distanceString);
        }

        // 중복 방지용 메시지 ID 저장
        MarkMessageAsProcessed(messageId, title, body, timestamp);
    }

    public void OnNativeTokenReceived(string token)
    {
        currentFCMToken = token;

        PlayerPrefs.SetString("FCMToken", token);
        PlayerPrefs.Save();
    
        StartCoroutine(SendTokenToServer(token));
    }

    private IEnumerator CheckBackgroundNotificationOnStartup()
    {
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(CheckBackgroundNotification());
    }

    private bool IsDuplicateMessage(string title, string body)
    {
        string messageKey = $"{title}_{body}";
        DateTime currentTime = DateTime.Now;

        var expiredKeys = new List<string>();
        foreach (var kvp in recentMessageTracker)
        {
            if ((currentTime - kvp.Value).TotalSeconds > DuplicateMessageTimeWindow)
            {
                expiredKeys.Add(kvp.Key);
            }
        }
        foreach (var key in expiredKeys)
        {
            recentMessageTracker.Remove(key);
        }

        if (recentMessageTracker.ContainsKey(messageKey))
        {
            return true;
        }

        recentMessageTracker[messageKey] = currentTime;
        return false;
    }

    private string GetCurrentDistance(float messageLat, float messageLon)
    {
        if (!Input.location.isEnabledByUser || !isLocationServiceEnabled)
        {
            string langCode = GetCurrentLanguageCode();
            return TimeLocalization.GetLocalizedTime("location_service_off", langCode);
        }

        if (messageLat == 0f && messageLon == 0f)
        {
            return "";
        }

        if (currentUserLocation == Vector2.zero)
        {
            return "";
        }

        float distance = CalculateDistanceInMeters(
            currentUserLocation.x, currentUserLocation.y,
            messageLat, messageLon
        );

        return FormatDistance(distance);
    }

    private float CalculateDistanceInMeters(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000f;

        float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
        float dLon = (lon2 - lon1) * Mathf.Deg2Rad;

        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);

        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

        return R * c;
    }

    private string FormatDistance(float distanceInMeters)
    {
        if (distanceInMeters < 1000f)
        {
            return $"{Mathf.RoundToInt(distanceInMeters)}m";
        }
        else if (distanceInMeters < 10000f)
        {
            return $"{(distanceInMeters / 1000f):F1}km";
        }
        else
        {
            return $"{Mathf.RoundToInt(distanceInMeters / 1000f)}km";
        }
    }

    private string ExtractDistanceFromTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return "";
    
        int dashIndex = title.LastIndexOf(" - ");
        if (dashIndex > 0 && dashIndex < title.Length - 3)
        {
            return title.Substring(dashIndex + 3);
        }
    
        return "";
    }


    /// <summary>
    /// 로그인 시 토큰에 user_id 연결하여 재등록
    /// </summary>
    public void RegisterTokenWithUserId(string userId)
    {
        if (string.IsNullOrEmpty(currentFCMToken))
        {
            string savedToken = PlayerPrefs.GetString("FCMToken", "");
            if (!string.IsNullOrEmpty(savedToken))
            {
                currentFCMToken = savedToken;
            }
        }

        if (!string.IsNullOrEmpty(currentFCMToken))
        {
            StartCoroutine(SendTokenToServerWithUserId(currentFCMToken, userId));
        }
    }

    /// <summary>
    /// 로그아웃 시 토큰에서 user_id 제거
    /// </summary>
    public void UnregisterUserFromToken(string userId = null)
    {
        StartCoroutine(UnregisterUserFromTokenCoroutine(userId));
    }

    private IEnumerator SendTokenToServerWithUserId(string token, string userId)
    {
        if (string.IsNullOrEmpty(token))
        {
            yield break;
        }

        float latitude = 0f;
        float longitude = 0f;
        bool locationConsent = false;

        if (Input.location.isEnabledByUser && Input.location.status == LocationServiceStatus.Running)
        {
            latitude = Input.location.lastData.latitude;
            longitude = Input.location.lastData.longitude;
            locationConsent = true;
        }

        WWWForm form = new WWWForm();
        form.AddField("token", token);
        form.AddField("device_id", SystemInfo.deviceUniqueIdentifier);
        form.AddField("device_name", SystemInfo.deviceName);
        form.AddField("device_model", SystemInfo.deviceModel);
        form.AddField("os_version", SystemInfo.operatingSystem);
        form.AddField("app_version", Application.version);

        if (!string.IsNullOrEmpty(userId) && userId != "0")
        {
            form.AddField("user_id", userId);
        }

#if UNITY_IOS
        form.AddField("platform", "ios");
#elif UNITY_ANDROID
        form.AddField("platform", "android");
#else
        form.AddField("platform", "unknown");
#endif

        if (locationConsent && latitude != 0f && longitude != 0f)
        {
            form.AddField("latitude", latitude.ToString("F6"));
            form.AddField("longitude", longitude.ToString("F6"));
            form.AddField("location_consent", "true");
        }
        else
        {
            form.AddField("location_consent", "false");
        }

        UnityWebRequest request = UnityWebRequest.Post(ApiConfig.REGISTER_TOKEN, form);
        request.timeout = 10;

        yield return request.SendWebRequest();

    }

    private IEnumerator UnregisterUserFromTokenCoroutine(string userId)
    {
        WWWForm form = new WWWForm();
        form.AddField("device_id", SystemInfo.deviceUniqueIdentifier);

        if (!string.IsNullOrEmpty(userId))
        {
            form.AddField("user_id", userId);
        }

        string unregisterUrl = ApiConfig.MAIN_SERVER + "/unregister-user-token";
        UnityWebRequest request = UnityWebRequest.Post(unregisterUrl, form);
        request.timeout = 10;

        yield return request.SendWebRequest();
    }

#if UNITY_ANDROID
    private string CalculateMessageDistance(Firebase.Messaging.FirebaseMessage message)
    {
        try
        {
            if (!Input.location.isEnabledByUser || !isLocationServiceEnabled)
            {
                return "";
            }

            if (currentUserLocation == Vector2.zero)
            {
                return "";
            }

            if (message.Data == null)
            {
                return "";
            }

            if (!message.Data.ContainsKey("latitude") || !message.Data.ContainsKey("longitude"))
            {
                return "";
            }

            string latStr = message.Data["latitude"];
            string lonStr = message.Data["longitude"];

            if (!float.TryParse(latStr, out float targetLat) || !float.TryParse(lonStr, out float targetLon))
            {
                return "";
            }

            if (targetLat == 0f && targetLon == 0f)
            {
                return "";
            }

            float distance = CalculateDistanceInMeters(
                currentUserLocation.x, currentUserLocation.y,
                targetLat, targetLon
            );

            string formattedDistance = FormatDistance(distance);
            return formattedDistance;
        }
        catch (System.Exception)
        {
            return "";
        }
    }
#endif

    #region 인앱 알림 배너

    /// <summary>
    /// MessagePanelManager 인스턴스를 안전하게 가져옴
    /// </summary>
    private MessagePanelManager GetMessagePanelManager()
    {
        if (MessagePanelManager.Instance != null)
            return MessagePanelManager.Instance;

        // Instance가 null이면 FindFirstObjectByType으로 찾기
        return FindFirstObjectByType<MessagePanelManager>();
    }

    /// <summary>
    /// 인앱 알림 배너 표시 (iOS만 - Android는 시스템 알림으로 충분)
    /// </summary>
    public void ShowInAppNotification(string title, string body, string userId = "", string username = "")
    {
#if UNITY_IOS
        // iOS만 인앱 배너 표시 (Android는 포그라운드에서도 시스템 상단 알림이 표시되므로 불필요)
        if (notificationBanner == null)
        {
            CreateNotificationBannerUI();
        }

        if (notificationBanner == null)
        {
            return;
        }

        // 클릭 시 열 대화 정보 저장
        pendingOpenUserId = userId;
        pendingOpenUsername = username;

        // 텍스트 설정
        if (bannerTitleText != null)
            bannerTitleText.text = title;
        if (bannerBodyText != null)
            bannerBodyText.text = body;

        // 기존 코루틴 중지
        if (bannerCoroutine != null)
        {
            StopCoroutine(bannerCoroutine);
        }

        // 배너 표시 코루틴 시작
        bannerCoroutine = StartCoroutine(ShowBannerCoroutine());
#endif
    }

    private IEnumerator ShowBannerCoroutine()
    {
        if (notificationBanner == null || bannerRectTransform == null)
            yield break;

        // 배너 활성화 및 초기 위치 (화면 위로 숨김)
        notificationBanner.SetActive(true);
        bannerRectTransform.anchoredPosition = new Vector2(0, bannerHeight);

        // 슬라이드 다운
        float targetY = 0f;
        while (bannerRectTransform.anchoredPosition.y > targetY + 1f)
        {
            float newY = Mathf.MoveTowards(bannerRectTransform.anchoredPosition.y, targetY, bannerSlideSpeed * Time.deltaTime);
            bannerRectTransform.anchoredPosition = new Vector2(0, newY);
            yield return null;
        }
        bannerRectTransform.anchoredPosition = new Vector2(0, targetY);

        // 표시 유지
        yield return new WaitForSeconds(bannerDisplayDuration);

        // 슬라이드 업
        float hideY = bannerHeight;
        while (bannerRectTransform.anchoredPosition.y < hideY - 1f)
        {
            float newY = Mathf.MoveTowards(bannerRectTransform.anchoredPosition.y, hideY, bannerSlideSpeed * Time.deltaTime);
            bannerRectTransform.anchoredPosition = new Vector2(0, newY);
            yield return null;
        }

        notificationBanner.SetActive(false);
    }

    /// <summary>
    /// 배너 클릭 시 호출
    /// </summary>
    public void OnNotificationBannerClicked()
    {
        // 배너 숨기기
        if (bannerCoroutine != null)
        {
            StopCoroutine(bannerCoroutine);
        }
        if (notificationBanner != null)
        {
            notificationBanner.SetActive(false);
        }

        // MessagePanel 열기
        var manager = GetMessagePanelManager();
        if (manager != null)
        {
            if (!string.IsNullOrEmpty(pendingOpenUserId))
            {
                // 특정 대화방 열기
                manager.OpenChatRoom(pendingOpenUserId, pendingOpenUsername);
            }
            else
            {
                // MessagePanel 열기
                manager.OpenMessagePanel();
            }
        }
    }

    /// <summary>
    /// 알림 배너 UI 동적 생성
    /// </summary>
    private void CreateNotificationBannerUI()
    {
        // Canvas 찾기
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        // 배너 패널 생성
        notificationBanner = new GameObject("NotificationBanner");
        notificationBanner.transform.SetParent(canvas.transform, false);

        // RectTransform 설정 (화면 상단)
        bannerRectTransform = notificationBanner.AddComponent<RectTransform>();
        bannerRectTransform.anchorMin = new Vector2(0, 1);
        bannerRectTransform.anchorMax = new Vector2(1, 1);
        bannerRectTransform.pivot = new Vector2(0.5f, 1);
        bannerRectTransform.anchoredPosition = new Vector2(0, bannerHeight);
        bannerRectTransform.sizeDelta = new Vector2(0, bannerHeight);

        // 배경 이미지
        Image bgImage = notificationBanner.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        // 버튼 추가 (클릭 영역)
        bannerButton = notificationBanner.AddComponent<Button>();
        bannerButton.onClick.AddListener(OnNotificationBannerClicked);

        // 제목 텍스트
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(notificationBanner.transform, false);
        bannerTitleText = titleObj.AddComponent<Text>();
        bannerTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bannerTitleText.fontSize = 32;
        bannerTitleText.fontStyle = FontStyle.Bold;
        bannerTitleText.color = Color.white;
        bannerTitleText.alignment = TextAnchor.MiddleLeft;

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.5f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = new Vector2(20, 10);
        titleRect.offsetMax = new Vector2(-20, -10);

        // 내용 텍스트
        GameObject bodyObj = new GameObject("BodyText");
        bodyObj.transform.SetParent(notificationBanner.transform, false);
        bannerBodyText = bodyObj.AddComponent<Text>();
        bannerBodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bannerBodyText.fontSize = 26;
        bannerBodyText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        bannerBodyText.alignment = TextAnchor.MiddleLeft;

        RectTransform bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0, 0);
        bodyRect.anchorMax = new Vector2(1, 0.5f);
        bodyRect.offsetMin = new Vector2(20, 10);
        bodyRect.offsetMax = new Vector2(-20, -5);

        // 폰트 로드 시도 (AppleSDGothicNeoM) - cached static field
        Font customFont = CachedFont;
        if (customFont != null)
        {
            bannerTitleText.font = customFont;
            bannerBodyText.font = customFont;
        }

        // 초기에는 숨김
        notificationBanner.SetActive(false);

        // 가장 앞에 표시
        notificationBanner.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 배너 RectTransform 초기화
    /// </summary>
    private void InitializeBannerRect()
    {
        if (notificationBanner != null && bannerRectTransform == null)
        {
            bannerRectTransform = notificationBanner.GetComponent<RectTransform>();
        }
    }

    #endregion

    void OnDestroy()
    {
        Input.location.Stop();

        // 모든 활성 알림 정리
        ClearAllNotifications();

#if UNITY_ANDROID
        try
        {
            FirebaseMessaging.TokenReceived -= OnTokenReceived;
            FirebaseMessaging.MessageReceived -= OnMessageReceived;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error unregistering Firebase events: {ex.Message}");
        }
#endif
    }
}