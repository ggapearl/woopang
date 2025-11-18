using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Collections;
using Firebase.Messaging;
using Firebase.Extensions;
using System;
using System.Globalization;
using UnityEngine.Android;
using Unity.Notifications.Android;

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
    public Transform Content;
    public GameObject MessageDetailPopup;
    public Text MessageDetailText;
    public GameObject MessageDetailScrollView;
    public Button MessageDetailConfirmButton;
    public GameObject MessageTextPrefab;
    public Image UnreadIndicatorIcon;
    
    [Header("Message Delete UI")]
    [Tooltip("메시지 삭제 버튼을 연결하세요.")]
    public Button MessageDeleteButton;
    [Tooltip("삭제 확인 버튼을 연결하세요. (다른 색상으로 디자인된 확인 버튼)")]
    public Button DeleteConfirmButton;
    
    [Header("Message Panel Monitoring")]
    [Tooltip("메시지 패널을 연결하세요. 이 패널이 활성화되면 거리 업데이트가 시작됩니다.")]
    public GameObject MessagePanel; // 인스펙터에서 연결할 메시지 패널

    [Header("Time Display Settings")]
    public bool useRelativeTimeDisplay = true;
    public int maxRelativeHours = 24;

    private List<MessageData> receivedMessages = new List<MessageData>();
    private string latestMessage;
    private const int MaxMessages = 30;

    // 알림 관리를 위한 변수들
    private const int MAX_ACTIVE_NOTIFICATIONS = 5;
    private List<int> activeNotificationIds = new List<int>();

    private string currentFCMToken = "";
    private bool firebaseInitialized = false;

    private HashSet<string> processedMessageIds = new HashSet<string>();
    private Dictionary<string, DateTime> recentMessageTracker = new Dictionary<string, DateTime>();
    private const int DuplicateMessageTimeWindow = 600;

    private bool isLocationServiceEnabled = false;
    private Vector2 currentUserLocation = Vector2.zero;
    private MessageData selectedMessage = null;
    private bool isDeleteConfirmMode = false; // 삭제 확인 모드 상태

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
                Instance.firebaseInitialized = true;
            }
            Destroy(gameObject);
            return;
        }

        Time.timeScale = 1f;
        InitializeLocationService();
        CheckUIReferences();
        LoadMessages();
        LoadProcessedMessageIds();

        if (MessageDetailPopup != null)
        {
            MessageDetailPopup.SetActive(false);
        }
        if (MessageDetailConfirmButton != null)
        {
            MessageDetailConfirmButton.onClick.AddListener(OnMessageDetailConfirmButtonClicked);
        }
        
        // 삭제 관련 버튼 이벤트 연결
        SetupDeleteButtons();
        
        // 앱 시작 시 삭제 확인 모드 강제 리셋
        ResetDeleteConfirmMode();

        UpdateUnreadIndicator();
        UpdateSystemBadge();
        RequestNotificationPermission();
        InitializeAndroidNotificationChannel();

        // 알림 정리 시스템 시작
        StartNotificationCleanup();

        // 메시지 패널 모니터링 시작
        StartCoroutine(MonitorMessagePanel());

        FirebaseMessaging.TokenReceived += OnTokenReceived;
        FirebaseMessaging.MessageReceived += OnMessageReceived;
        StartCoroutine(InitializeFirebaseCoroutine());
        StartCoroutine(CheckBackgroundNotificationOnStartup());
    }

    // 삭제 관련 버튼들 설정
    private void SetupDeleteButtons()
    {
        // 삭제 버튼 이벤트 연결
        if (MessageDeleteButton != null)
        {
            MessageDeleteButton.onClick.AddListener(OnMessageDeleteButtonClicked);
        }
        
        // 삭제 확인 버튼 이벤트 연결
        if (DeleteConfirmButton != null)
        {
            DeleteConfirmButton.onClick.AddListener(OnDeleteConfirmButtonClicked);
        }
        
        // 초기 상태 설정: 삭제 버튼은 보이고, 확인 버튼은 숨김
        if (MessageDeleteButton != null)
        {
            MessageDeleteButton.gameObject.SetActive(true);
        }
        if (DeleteConfirmButton != null)
        {
            DeleteConfirmButton.gameObject.SetActive(false);
        }
    }

    // 삭제 버튼 클릭 시 (첫 번째 단계 - 확인 버튼으로 변경)
    private void OnMessageDeleteButtonClicked()
    {
        if (!isDeleteConfirmMode)
        {
            // 삭제 확인 모드로 전환
            isDeleteConfirmMode = true;
            
            // 삭제 버튼 숨기고 확인 버튼 표시
            if (MessageDeleteButton != null)
            {
                MessageDeleteButton.gameObject.SetActive(false);
            }
            if (DeleteConfirmButton != null)
            {
                DeleteConfirmButton.gameObject.SetActive(true);
            }
        }
    }

    // 삭제 확인 버튼 클릭 시 (두 번째 단계 - 실제 삭제)
    private void OnDeleteConfirmButtonClicked()
    {
        if (selectedMessage != null)
        {
            // 메시지 삭제 실행
            DeleteSelectedMessage();
            
            // 팝업 닫기
            CloseMessageDetailPopup();
        }
    }

    // 실제 메시지 삭제 로직
    private void DeleteSelectedMessage()
    {
        if (selectedMessage == null) return;
        
        // receivedMessages 리스트에서 제거
        for (int i = receivedMessages.Count - 1; i >= 0; i--)
        {
            if (receivedMessages[i].messageId == selectedMessage.messageId)
            {
                receivedMessages.RemoveAt(i);
                break;
            }
        }
        
        // processedMessageIds에서도 제거 (같은 메시지를 다시 받을 수 있도록)
        if (!string.IsNullOrEmpty(selectedMessage.messageId))
        {
            processedMessageIds.Remove(selectedMessage.messageId);
        }
        
        // 저장
        SaveMessages();
        SaveProcessedMessageIds();
        
        // UI 업데이트
        UpdateNotificationUI();
        UpdateUnreadIndicator();
        UpdateSystemBadge();
        
        // 선택된 메시지 초기화
        selectedMessage = null;
    }

    // 삭제 확인 모드 리셋
    private void ResetDeleteConfirmMode()
    {
        isDeleteConfirmMode = false;
        
        // 확인 버튼 숨기고 삭제 버튼 다시 표시
        if (DeleteConfirmButton != null)
        {
            DeleteConfirmButton.gameObject.SetActive(false);
        }
        if (MessageDeleteButton != null)
        {
            MessageDeleteButton.gameObject.SetActive(true);
        }
    }

    // 메시지 상세 팝업 닫기
    private void CloseMessageDetailPopup()
    {
        if (MessageDetailPopup != null)
        {
            MessageDetailPopup.SetActive(false);
        }
        
        // 삭제 확인 모드 리셋
        ResetDeleteConfirmMode();
    }

    // Update 메서드 추가 (팝업 상태 모니터링)
    void Update()
    {
        // 팝업이 비활성화되었는데 삭제 확인 모드가 활성화된 경우 리셋
        if (isDeleteConfirmMode && MessageDetailPopup != null && !MessageDetailPopup.activeInHierarchy)
        {
            ResetDeleteConfirmMode();
        }
    }

    // 메시지 패널 활성화 상태 모니터링 (간단한 방식)
    private IEnumerator MonitorMessagePanel()
    {
        bool wasActive = false;
        
        while (true)
        {
            yield return new WaitForSeconds(1f); // 1초마다 체크 (0.1초보다 효율적)
            
            if (MessagePanel != null)
            {
                bool isCurrentlyActive = MessagePanel.activeInHierarchy;
                
                // 패널이 방금 활성화된 경우
                if (isCurrentlyActive && !wasActive)
                {
                    OnMessagePanelActivated();
                }
                // 패널이 방금 비활성화된 경우  
                else if (!isCurrentlyActive && wasActive)
                {
                    OnMessagePanelDeactivated();
                }
                
                wasActive = isCurrentlyActive;
            }
        }
    }

    // 메시지 패널이 활성화될 때 호출
    private void OnMessagePanelActivated()
    {
        // 즉시 한 번 거리 업데이트
        if (selectedMessage != null)
        {
            UpdateSelectedMessageDistance();
        }
        
        // 1분마다 거리 업데이트 시작
        StartCoroutine(MessagePanelDistanceUpdateLoop());
    }

    // 메시지 패널이 비활성화될 때 호출
    private void OnMessagePanelDeactivated()
    {
        // 거리 업데이트 중지 (코루틴은 자동으로 중지됨)
    }

    // 메시지 패널이 활성화된 동안 1분마다 거리 업데이트
    private IEnumerator MessagePanelDistanceUpdateLoop()
    {
        while (MessagePanel != null && MessagePanel.activeInHierarchy)
        {
            yield return new WaitForSeconds(60f); // 1분 대기
            
            // 패널이 여전히 활성화되어 있으면 거리 업데이트
            if (MessagePanel != null && MessagePanel.activeInHierarchy && selectedMessage != null)
            {
                UpdateSelectedMessageDistance();
            }
        }
    }

    // 선택된 메시지의 거리 업데이트
    private void UpdateSelectedMessageDistance()
    {
        if (selectedMessage == null) return;
        
        // 위치 기반 메시지인지 확인
        if (selectedMessage.messageLat == 0f && selectedMessage.messageLon == 0f) return;

        // 현재 거리 계산
        string newDistance = GetCurrentDistance(selectedMessage.messageLat, selectedMessage.messageLon);
        
        // 거리가 변경되었으면 업데이트
        if (selectedMessage.currentDistance != newDistance)
        {
            // 메시지 데이터 업데이트
            selectedMessage.currentDistance = newDistance;
            
            // 제목에서 거리 부분 업데이트
            string originalTitle = selectedMessage.title;
            int dashIndex = originalTitle.LastIndexOf(" - ");
            if (dashIndex > 0)
            {
                string baseTitlePart = originalTitle.Substring(0, dashIndex);
                selectedMessage.title = $"{baseTitlePart} - {newDistance}";
            }
            
            // receivedMessages 리스트에서도 업데이트
            for (int i = 0; i < receivedMessages.Count; i++)
            {
                if (receivedMessages[i].messageId == selectedMessage.messageId)
                {
                    receivedMessages[i] = selectedMessage;
                    break;
                }
            }
            
            // 상세 패널 텍스트 업데이트 (MessageDetailPopup이 활성화된 경우)
            if (MessageDetailPopup != null && MessageDetailPopup.activeInHierarchy && MessageDetailText != null)
            {
                string timeString = GetTimeDisplayString(selectedMessage);
                string updatedTitle = selectedMessage.title;
                
                string displayText = $"<b>{updatedTitle}</b>\n\n{selectedMessage.body}";
                displayText += $"\n\n<size=40><color=#888888>{timeString}</color></size>";
                
                MessageDetailText.text = displayText;
            }
            
            // 메시지 리스트도 업데이트
            UpdateNotificationUI();
            SaveMessages();
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
            }
            yield return new WaitForSeconds(30f);
        }
    }

    private void SaveLocationToAndroidPrefs(float latitude, float longitude)
    {
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
    }

    private IEnumerator InitializeFirebaseCoroutine()
    {
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
            firebaseInitialized = true;
            FirebaseMessaging.TokenReceived += OnTokenReceived;
            FirebaseMessaging.MessageReceived += OnMessageReceived;
            UpdateNotificationUI();
            StartCoroutine(CheckBackgroundNotification());
        }
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
            catch (System.Exception ex)
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

    private bool SaveMessageSafelyWithLocation(string title, string body, string formattedMessage, DateTime serverTimestamp, string messageId, float lat = 0f, float lon = 0f, float radius = 0f, string preCalculatedDistance = "")
    {
        if (string.IsNullOrEmpty(messageId))
        {
            messageId = GenerateMessageId(title, body, serverTimestamp);
        }
        
        if (processedMessageIds.Contains(messageId))
        {
            return false;
        }
        
        foreach (var existingMessage in receivedMessages)
        {
            if (existingMessage.messageId == messageId)
            {
                return false;
            }
        }
        
        if (receivedMessages.Count >= MaxMessages)
        {
            var oldestMessage = receivedMessages[receivedMessages.Count - 1];
            receivedMessages.RemoveAt(receivedMessages.Count - 1);
            
            if (!string.IsNullOrEmpty(oldestMessage.messageId))
            {
                processedMessageIds.Remove(oldestMessage.messageId);
            }
        }

        string currentDistance = "";
        if (!string.IsNullOrEmpty(preCalculatedDistance))
        {
            currentDistance = preCalculatedDistance;
        }
        else
        {
            currentDistance = GetCurrentDistance(lat, lon);
        }

        var newMessage = new MessageData
        {
            title = title,
            body = body,
            message = formattedMessage,
            isRead = false,
            timestampString = DateTimeToString(DateTime.Now),
            receivedAtString = DateTimeToString(serverTimestamp),
            messageId = messageId,
            messageLat = lat,
            messageLon = lon,
            messageRadius = radius,
            currentDistance = currentDistance
        };
        
        receivedMessages.Insert(0, newMessage);
        processedMessageIds.Add(messageId);
        
        SaveMessages();
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

    private string GetTimeDisplayString(MessageData messageData)
    {
        DateTime displayTime;
        
        if (!string.IsNullOrEmpty(messageData.receivedAtString))
        {
            displayTime = StringToDateTime(messageData.receivedAtString);
        }
        else if (!string.IsNullOrEmpty(messageData.timestampString))
        {
            displayTime = StringToDateTime(messageData.timestampString);
        }
        else
        {
            displayTime = DateTime.Now;
        }
        
        DateTime now = DateTime.Now;
        TimeSpan timeDiff = now - displayTime;
        
        if (!useRelativeTimeDisplay || timeDiff.TotalHours > maxRelativeHours)
        {
            return displayTime.ToString("yyyy-MM-dd HH:mm");
        }
        
        string langCode = GetCurrentLanguageCode();

        if (timeDiff.TotalMinutes < 1)
        {
            return TimeLocalization.GetLocalizedTime("just_now", langCode);
        }
        else if (timeDiff.TotalHours < 1)
        {
            int minutes = (int)timeDiff.TotalMinutes;
            if (minutes == 1)
                return TimeLocalization.GetLocalizedTime("minute_ago", langCode);
            else
                return TimeLocalization.GetLocalizedTime("minutes_ago", langCode, minutes);
        }
        else if (timeDiff.TotalHours < maxRelativeHours)
        {
            int hours = (int)timeDiff.TotalHours;
            if (hours == 1)
                return TimeLocalization.GetLocalizedTime("hour_ago", langCode);
            else
                return TimeLocalization.GetLocalizedTime("hours_ago", langCode, hours);
        }
        else
        {
            return displayTime.ToString("yyyy-MM-dd HH:mm");
        }
    }

    public void SetRelativeTimeDisplay(bool useRelative)
    {
        useRelativeTimeDisplay = useRelative;
        UpdateNotificationUI();
    }
    
    public void SetMaxRelativeHours(int hours)
    {
        maxRelativeHours = Mathf.Max(1, hours);
        UpdateNotificationUI();
    }

    private void CheckUIReferences()
    {
        if (Content != null)
        {
            VerticalLayoutGroup vlg = Content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = Content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 10f;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
            }
            
            ContentSizeFitter csf = Content.GetComponent<ContentSizeFitter>();
            if (csf == null)
            {
                csf = Content.gameObject.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
    }

    private void LoadMessages()
    {
        string messagesJson = PlayerPrefs.GetString("ReceivedMessages", "");

        if (!string.IsNullOrEmpty(messagesJson))
        {
            try
            {
                MessageDataList dataList = JsonUtility.FromJson<MessageDataList>(messagesJson);
                if (dataList != null && dataList.messages != null)
                {
                    receivedMessages = new List<MessageData>(dataList.messages);
                }
                else
                {
                    receivedMessages = new List<MessageData>();
                }
            }
            catch (System.Exception ex)
            {
                receivedMessages = new List<MessageData>();
            }
        }
        else
        {
            receivedMessages = new List<MessageData>();
        }

        LoadPendingMessagesFromAndroid();
        LoadTokenFromAndroidPrefs();
    }

    private void LoadPendingMessagesFromAndroid()
    {
        if (Application.platform != RuntimePlatform.Android) return;
        
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject sharedPrefs = currentActivity.Call<AndroidJavaObject>("getSharedPreferences", "firebase_messages", 0))
            {
                string pendingMessagesJson = sharedPrefs.Call<string>("getString", "pending_messages", "[]");
                
                if (!string.IsNullOrEmpty(pendingMessagesJson) && pendingMessagesJson != "[]")
                {
                    try
                    {
                        var wrapper = JsonUtility.FromJson<PendingMessagesWrapper>("{\"messages\":" + pendingMessagesJson + "}");
                        
                        if (wrapper != null && wrapper.messages != null)
                        {
                            int addedCount = 0;
                            
                            foreach (var newMessage in wrapper.messages)
                            {
                                bool isDuplicate = false;
                                foreach (var existingMessage in receivedMessages)
                                {
                                    if (existingMessage.messageId == newMessage.messageId)
                                    {
                                        isDuplicate = true;
                                        break;
                                    }
                                }
                                
                                if (!isDuplicate)
                                {
                                    receivedMessages.Insert(0, newMessage);
                                    if (!string.IsNullOrEmpty(newMessage.messageId))
                                    {
                                        processedMessageIds.Add(newMessage.messageId);
                                    }
                                    addedCount++;
                                }
                            }
                            
                            while (receivedMessages.Count > MaxMessages)
                            {
                                var removedMessage = receivedMessages[receivedMessages.Count - 1];
                                receivedMessages.RemoveAt(receivedMessages.Count - 1);
                                if (!string.IsNullOrEmpty(removedMessage.messageId))
                                {
                                    processedMessageIds.Remove(removedMessage.messageId);
                                }
                            }
                            
                            if (addedCount > 0)
                            {
                                SaveMessages();
                                SaveProcessedMessageIds();
                                ClearAndroidPendingMessages();
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to parse Android pending messages: {ex.Message}");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to load pending messages from Android: {ex.Message}");
        }
    }
    
    private void ClearAndroidPendingMessages()
    {
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject sharedPrefs = currentActivity.Call<AndroidJavaObject>("getSharedPreferences", "firebase_messages", 0))
            using (AndroidJavaObject editor = sharedPrefs.Call<AndroidJavaObject>("edit"))
            {
                editor.Call<AndroidJavaObject>("remove", "pending_messages");
                editor.Call("apply");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to clear Android pending messages: {ex.Message}");
        }
    }
    
    private void LoadTokenFromAndroidPrefs()
    {
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
                    firebaseInitialized = true;
                    
                    PlayerPrefs.SetString("FCMToken", token);
                    PlayerPrefs.Save();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to load token from Android: {ex.Message}");
        }
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
        
        UnityWebRequest request = UnityWebRequest.Post("https://woopang.com/register-token", form);
        request.timeout = 10;
        
        yield return request.SendWebRequest();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            LoadMessages();
            StartCoroutine(CheckBackgroundNotification());
            UpdateNotificationUI();
            UpdateUnreadIndicator();
            UpdateSystemBadge();
        }
        else
        {
            SaveMessages();
            // 앱이 일시정지될 때 삭제 확인 모드 리셋
            ResetDeleteConfirmMode();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            LoadMessages();
            StartCoroutine(CheckBackgroundNotification());
            UpdateNotificationUI();
            UpdateUnreadIndicator();
            UpdateSystemBadge();
            StartCoroutine(DelayedBackgroundCheck());
        }
        else
        {
            SaveMessages();
            // 앱이 포커스를 잃을 때 삭제 확인 모드 리셋
            ResetDeleteConfirmMode();
        }
    }

    private IEnumerator DelayedBackgroundCheck()
    {
        yield return new WaitForSeconds(0.5f);
        LoadMessages();
        UpdateNotificationUI();
        UpdateUnreadIndicator();
        UpdateSystemBadge();
    }

    private void SaveMessages()
    {
        try
        {
            MessageDataList dataList = new MessageDataList { messages = receivedMessages };
            string messagesJson = JsonUtility.ToJson(dataList);
            PlayerPrefs.SetString("ReceivedMessages", messagesJson);
            PlayerPrefs.Save();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save messages: {ex.Message}");
        }
    }

    private void UpdateSystemBadge()
    {
        int unreadCount = GetUnreadCount();
    }

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
            Id = "default_channel",
            Name = "Default Channel",
            Importance = Importance.High,
            Description = "Generic notifications",
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
    }

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

        StartCoroutine(SendTokenToServer(token.Token));
    }

    private IEnumerator UpdateFirebaseStatusOnMainThread(string token)
    {
        yield return null;
        
        firebaseInitialized = true;
        currentFCMToken = token;
        
        UpdateNotificationUI();
        UpdateUnreadIndicator();
    }

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

        if (processedMessageIds.Contains(messageId))
        {
            return;
        }

        if (Application.isFocused)
        {
            StartCoroutine(HandleForegroundMessage(title, body, serverTimestamp, messageId, e));
        }
        else
        {
            StartCoroutine(HandleBackgroundMessage(title, body, serverTimestamp, messageId, e));
        }
    }

    private IEnumerator HandleForegroundMessage(string title, string body, DateTime serverTimestamp, string messageId, MessageReceivedEventArgs e)
    {
        string formattedMessage = $"[{title}] {body}";

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

        bool saved = SaveMessageSafelyWithLocation(title, body, formattedMessage, serverTimestamp, messageId, targetLat, targetLon, radius, currentDistance);
        if (!saved)
        {
            yield break;
        }

        // 스마트 알림 관리
        ManageActiveNotifications();

        int unreadCount = GetUnreadCount();

        // 알림 설정 (Unity에서 자동 삭제 관리)
        var notification = new AndroidNotification
        {
            Title = title,
            Text = body,
            FireTime = System.DateTime.Now,
            SmallIcon = "icon_0",
            LargeIcon = "icon_1",
            ShouldAutoCancel = true,
            Number = unreadCount
        };
        
        int newNotificationId = (int)System.DateTime.Now.Ticks;
        AndroidNotificationCenter.SendNotification(notification, "default_channel");
        
        // 활성 알림 ID 추가
        activeNotificationIds.Add(newNotificationId);

        UpdateNotificationUI();
        UpdateUnreadIndicator();
    }

    private IEnumerator HandleBackgroundMessage(string title, string body, DateTime serverTimestamp, string messageId, MessageReceivedEventArgs e)
    {
        string formattedMessage = $"[{title}] {body}";
        LoadMessages();

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

        bool saved = SaveMessageSafelyWithLocation(title, body, formattedMessage, serverTimestamp, messageId, targetLat, targetLon, radius, currentDistance);
        if (!saved)
        {
            yield break;
        }

        UpdateSystemBadge();

        // 스마트 알림 관리
        ManageActiveNotifications();

        int unreadCount = GetUnreadCount();

        // 알림 설정 (Unity에서 자동 삭제 관리)
        var notification = new AndroidNotification
        {
            Title = title,
            Text = body,
            FireTime = System.DateTime.Now,
            SmallIcon = "icon_0",
            LargeIcon = "icon_1",
            ShouldAutoCancel = true,
            Number = unreadCount,
            // 알림 그룹화
            Group = "woopang_messages"
        };

        int newNotificationId = (int)System.DateTime.Now.Ticks;
        AndroidNotificationCenter.SendNotification(notification, "default_channel");
        
        // 활성 알림 ID 추가
        activeNotificationIds.Add(newNotificationId);
    }

    private IEnumerator CheckBackgroundNotification()
    {
        var notificationIntent = AndroidNotificationCenter.GetLastNotificationIntent();
        if (notificationIntent != null)
        {
            string title = notificationIntent.Notification.Title ?? "알림";
            string body = notificationIntent.Notification.Text ?? "";
    
            if (IsDuplicateMessage(title, body))
            {
                yield break;
            }
    
            string formattedMessage = $"[{title}] {body}";
            DateTime timestamp = DateTime.Now;
            string messageId = GenerateMessageId(title, body, timestamp);

            LoadMessages();
    
            float targetLat = 0f;
            float targetLon = 0f;
            float radius = 0f;
            string distanceString = "";
        
            bool saved = SaveMessageSafelyWithLocation(title, body, formattedMessage, timestamp, messageId, targetLat, targetLon, radius, distanceString);

            if (saved)
            {
                UpdateNotificationUI();
                UpdateUnreadIndicator();
                UpdateSystemBadge();
            }
        }
        yield break;
    }

    // 활성 알림 스마트 관리
    private void ManageActiveNotifications()
    {
        // 만료된 알림 ID 정리
        CleanExpiredNotificationIds();
        
        // 최대 개수 초과 시 오래된 알림 삭제
        while (activeNotificationIds.Count >= MAX_ACTIVE_NOTIFICATIONS)
        {
            int oldestId = activeNotificationIds[0];
            AndroidNotificationCenter.CancelNotification(oldestId);
            activeNotificationIds.RemoveAt(0);
        }
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

    // 읽지 않은 메시지 수 계산
    private int GetUnreadCount()
    {
        int unreadCount = 0;
        foreach (var message in receivedMessages)
        {
            if (!message.isRead) unreadCount++;
        }
        return unreadCount;
    }

    // 모든 알림 정리 (앱 종료 시 호출)
    public void ClearAllNotifications()
    {
        foreach (int notificationId in activeNotificationIds)
        {
            AndroidNotificationCenter.CancelNotification(notificationId);
        }
        activeNotificationIds.Clear();
    }

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

    private void UpdateNotificationUI()
    {
        if (Content == null || MessageTextPrefab == null)
        {
            return;
        }

        foreach (Transform child in Content)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < receivedMessages.Count; i++)
        {
            int index = i;
            MessageData messageData = receivedMessages[i];
            GameObject messageObj = Instantiate(MessageTextPrefab, Content);
            messageObj.SetActive(true);

            RectTransform rect = messageObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, 200);

            Text text = messageObj.GetComponentInChildren<Text>();
            if (text != null)
            {
                string timeString = GetTimeDisplayString(messageData);
                string displayTitle = "";
                string displayBody = "";
            
                if (!string.IsNullOrEmpty(messageData.title) && !string.IsNullOrEmpty(messageData.body))
                {
                    displayTitle = messageData.title;
                    displayBody = messageData.body;
                }
                else
                {
                    displayTitle = messageData.message;
                    displayBody = "";
                }
            
                if (!string.IsNullOrEmpty(displayBody))
                {
                    text.text = $"[{displayTitle}] {displayBody}\n<size=40><color=#888888>{timeString}</color></size>";
                }
                else
                {
                    text.text = $"{displayTitle}\n<size=40><color=#888888>{timeString}</color></size>";
                }
            }

            Transform iconTransform = messageObj.transform.Find("Icon");
            if (iconTransform != null)
            {
                GameObject iconObj = iconTransform.gameObject;
                iconObj.SetActive(!messageData.isRead);
            }

            Button button = messageObj.GetComponent<Button>();
            if (button == null)
            {
                button = messageObj.AddComponent<Button>();
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnMessageClicked(index));
        }

        StartCoroutine(RefreshLayout());
    }

    private IEnumerator RefreshLayout()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return null;

        if (Content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content.GetComponent<RectTransform>());
        }
    }

    private void UpdateUnreadIndicator()
    {
        if (UnreadIndicatorIcon != null)
        {
            bool hasUnreadMessage = false;
            foreach (var messageData in receivedMessages)
            {
                if (!messageData.isRead)
                {
                    hasUnreadMessage = true;
                    break;
                }
            }
            UnreadIndicatorIcon.gameObject.SetActive(hasUnreadMessage);
        }
    }

    private void OnMessageClicked(int index)
    {
        selectedMessage = receivedMessages[index]; // 선택된 메시지를 직접 참조로 저장

        if (MessageDetailPopup != null && MessageDetailText != null)
        {
            string timeString = GetTimeDisplayString(selectedMessage);
            string displayText = "";
        
            if (!string.IsNullOrEmpty(selectedMessage.title) && !string.IsNullOrEmpty(selectedMessage.body))
            {
                string titleWithDistance = selectedMessage.title;
            
                displayText = $"<b>{titleWithDistance}</b>\n\n{selectedMessage.body}";
                displayText += $"\n\n<size=40><color=#888888>{timeString}</color></size>";
            
                if (!string.IsNullOrEmpty(selectedMessage.receivedAtString) && 
                    !string.IsNullOrEmpty(selectedMessage.timestampString))
                {
                    DateTime receivedTime = StringToDateTime(selectedMessage.receivedAtString);
                    DateTime processedTime = StringToDateTime(selectedMessage.timestampString);
                
                    if (Math.Abs((receivedTime - processedTime).TotalSeconds) > 60)
                    {
                        string serverTimeString = receivedTime.ToString("yyyy-MM-dd HH:mm:ss");
                        string deviceTimeString = processedTime.ToString("yyyy-MM-dd HH:mm:ss");
                        displayText += $"\n<size=30><color=#666666>수신: {serverTimeString}\n처리: {deviceTimeString}</color></size>";
                    }
                }
            }
            else
            {
                displayText = $"{selectedMessage.message}\n\n<size=40><color=#888888>{timeString}</color></size>";
            }
        
            MessageDetailText.text = displayText;
            MessageDetailPopup.SetActive(true);
            
            // 새 메시지 선택 시 삭제 확인 모드 리셋 (초기 화면으로 돌아가기)
            ResetDeleteConfirmMode();
        }
    }

    public void OnNativeMessageReceived(string message)
    {
        if (!string.IsNullOrEmpty(message) && message.Contains("|"))
        {
            string[] parts = message.Split('|');
            if (parts.Length >= 2)
            {
                string title = parts[0];
                string body = parts[1];

                float targetLat = 0f;
                float targetLon = 0f;
                float radius = 0f;
                string distanceString = "";

                if (parts.Length >= 5)
                {
                    if (float.TryParse(parts[2], out targetLat) && 
                        float.TryParse(parts[3], out targetLon))
                    {
                        float.TryParse(parts[4], out radius);
                
                        bool alreadyHasDistance = title.Contains(" - ") && 
                                                (title.EndsWith("m") || title.EndsWith("km"));
                    
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
                }

                string formattedMessage = $"[{title}] {body}";
                DateTime timestamp = DateTime.Now;
                string messageId = GenerateMessageId(title, body, timestamp);
        
                LoadMessages();
        
                bool saved = SaveMessageSafelyWithLocation(title, body, formattedMessage, timestamp, messageId, targetLat, targetLon, radius, distanceString);
                if (saved)
                {
                    StartCoroutine(UpdateUIAfterNativeMessage());
                }
            }
        }
    }

    private IEnumerator UpdateUIAfterNativeMessage()
    {
        yield return null;
        UpdateNotificationUI();
        UpdateUnreadIndicator();
        UpdateSystemBadge();
    }

    public void OnNativeTokenReceived(string token)
    {
        currentFCMToken = token;
        firebaseInitialized = true;
    
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

    private void OnMessageDetailConfirmButtonClicked()
    {
        if (selectedMessage != null)
        {
            selectedMessage.isRead = true;
            
            // receivedMessages 리스트에서도 업데이트
            for (int i = 0; i < receivedMessages.Count; i++)
            {
                if (receivedMessages[i].messageId == selectedMessage.messageId)
                {
                    receivedMessages[i].isRead = true;
                    break;
                }
            }
            
            SaveMessages();
        }

        // 팝업 닫기 (삭제 확인 모드도 함께 리셋)
        CloseMessageDetailPopup();

        UpdateNotificationUI();
        UpdateUnreadIndicator();
        UpdateSystemBadge();
    }

    public void ForceUpdateUI()
    {
        LoadMessages();
        UpdateNotificationUI();
        UpdateUnreadIndicator();
        UpdateSystemBadge();
    }

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
        catch (System.Exception ex)
        {
            return "";
        }
    }

    void OnDestroy()
    {
        Input.location.Stop();
        
        // 모든 활성 알림 정리
        ClearAllNotifications();
        
        try
        {
            FirebaseMessaging.TokenReceived -= OnTokenReceived;
            FirebaseMessaging.MessageReceived -= OnMessageReceived;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error unregistering Firebase events: {ex.Message}");
        }
    }
}