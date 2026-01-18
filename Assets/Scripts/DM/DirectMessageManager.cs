using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 다이렉트 메시지(DM) 관리자
/// 팔로잉하는 사용자에게 메시지를 보내고 받는 기능
/// </summary>
public class DirectMessageManager : MonoBehaviour
{
    public static DirectMessageManager Instance { get; private set; }

    [Header("=== UI 패널 ===")]
    [Tooltip("DM 메인 패널 (받은/보낸 메시지 리스트)")]
    public GameObject dmPanel;

    [Tooltip("대화 패널 (특정 사용자와의 대화)")]
    public GameObject conversationPanel;

    [Tooltip("팔로잉 리스트 선택 패널")]
    public GameObject followingSelectPanel;

    [Header("=== 받은 메시지 UI ===")]
    public Transform inboxContent;
    public GameObject dmItemPrefab;
    public Text unreadCountText;
    public GameObject unreadIndicator;

    [Header("=== 대화 UI ===")]
    public Transform messageContent;
    public GameObject myMessagePrefab;
    public GameObject otherMessagePrefab;
    public InputField messageInput;
    public Button sendButton;
    public Text conversationTitle;
    public Image conversationAvatar;

    [Header("=== 팔로잉 선택 UI ===")]
    public Transform followingListContent;
    public GameObject followingItemPrefab;

    [Header("=== 공통 ===")]
    public Button backButton;
    public ScrollRect scrollRect;

    // 현재 대화 중인 상대
    private string currentConversationUserId;
    private string currentConversationUsername;

    // 캐시
    private List<DMMessage> inboxMessages = new List<DMMessage>();
    private List<DMMessage> conversationMessages = new List<DMMessage>();
    private int unreadCount = 0;

    // 폴링 (새 메시지 확인)
    private Coroutine pollingCoroutine;
    private const float POLL_INTERVAL = 10f; // 10초마다 확인

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 버튼 리스너 설정
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendButtonClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClicked);

        // 초기 상태
        HideAllPanels();

        // 폴링 시작
        StartPolling();
    }

    void OnDestroy()
    {
        StopPolling();
    }

    #region Public Methods

    /// <summary>
    /// DM 패널 열기 (받은 메시지 표시)
    /// </summary>
    public void OpenDMPanel()
    {
        if (!CheckLogin()) return;

        HideAllPanels();
        if (dmPanel != null)
            dmPanel.SetActive(true);

        StartCoroutine(LoadInbox());
    }

    /// <summary>
    /// 팔로잉 리스트에서 메시지 보낼 사용자 선택
    /// </summary>
    public void OpenFollowingSelect()
    {
        if (!CheckLogin()) return;

        HideAllPanels();
        if (followingSelectPanel != null)
            followingSelectPanel.SetActive(true);

        StartCoroutine(LoadFollowingList());
    }

    /// <summary>
    /// 특정 사용자와의 대화 열기
    /// </summary>
    public void OpenConversation(string userId, string username, string avatarUrl = null)
    {
        if (!CheckLogin()) return;

        currentConversationUserId = userId;
        currentConversationUsername = username;

        HideAllPanels();
        if (conversationPanel != null)
            conversationPanel.SetActive(true);

        // 대화 상대 정보 표시
        if (conversationTitle != null)
            conversationTitle.text = username;

        if (conversationAvatar != null && !string.IsNullOrEmpty(avatarUrl))
            StartCoroutine(LoadAvatar(avatarUrl, conversationAvatar));

        StartCoroutine(LoadConversation(userId));

        // 해당 사용자의 메시지 읽음 처리
        StartCoroutine(MarkConversationAsRead(userId));
    }

    /// <summary>
    /// DM 패널 닫기
    /// </summary>
    public void CloseDMPanel()
    {
        HideAllPanels();
    }

    /// <summary>
    /// 안 읽은 메시지 수 가져오기
    /// </summary>
    public void RefreshUnreadCount()
    {
        if (CheckLogin())
            StartCoroutine(FetchUnreadCount());
    }

    /// <summary>
    /// 메시지 보내기 (외부에서 호출 가능)
    /// </summary>
    public void SendMessage(string recipientId, string content, Action<bool> callback = null)
    {
        if (!CheckLogin())
        {
            callback?.Invoke(false);
            return;
        }

        StartCoroutine(SendMessageCoroutine(recipientId, content, callback));
    }

    #endregion

    #region UI Event Handlers

    private void OnSendButtonClicked()
    {
        if (string.IsNullOrEmpty(currentConversationUserId)) return;

        string content = messageInput?.text?.Trim();
        if (string.IsNullOrEmpty(content)) return;

        // 입력 초기화
        if (messageInput != null)
            messageInput.text = "";

        SendMessage(currentConversationUserId, content, (success) =>
        {
            if (success)
            {
                // 대화 새로고침
                StartCoroutine(LoadConversation(currentConversationUserId));
            }
        });
    }

    private void OnBackButtonClicked()
    {
        if (conversationPanel != null && conversationPanel.activeSelf)
        {
            // 대화 -> 받은 메시지
            OpenDMPanel();
        }
        else if (followingSelectPanel != null && followingSelectPanel.activeSelf)
        {
            // 팔로잉 선택 -> 받은 메시지
            OpenDMPanel();
        }
        else
        {
            // 전체 닫기
            HideAllPanels();
        }
    }

    #endregion

    #region API Calls

    private IEnumerator LoadInbox()
    {
        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.DM_INBOX}?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DMInboxResponse>(request.downloadHandler.text);
                inboxMessages = response.messages ?? new List<DMMessage>();
                unreadCount = response.unread_count;

                PopulateInbox();
                UpdateUnreadUI();
            }
            else
            {
                Debug.LogError($"[DM] Failed to load inbox: {request.error}");
            }
        }
    }

    private IEnumerator LoadConversation(string otherUserId)
    {
        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.DM_CONVERSATION}?user_id={userId}&other_id={otherUserId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DMConversationResponse>(request.downloadHandler.text);
                conversationMessages = response.messages ?? new List<DMMessage>();

                PopulateConversation();
                ScrollToBottom();
            }
            else
            {
                Debug.LogError($"[DM] Failed to load conversation: {request.error}");
            }
        }
    }

    private IEnumerator LoadFollowingList()
    {
        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.FOLLOWING}?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<FollowingListResponse>(request.downloadHandler.text);
                PopulateFollowingList(response.following);
            }
            else
            {
                Debug.LogError($"[DM] Failed to load following: {request.error}");
            }
        }
    }

    private IEnumerator SendMessageCoroutine(string recipientId, string content, Action<bool> callback)
    {
        string userId = LoginManager.Instance.CurrentUser.id;

        var postData = new DMSendRequest
        {
            sender_id = userId,
            recipient_id = recipientId,
            content = content
        };

        string json = JsonUtility.ToJson(postData);

        using (UnityWebRequest request = new UnityWebRequest(ApiConfig.DM_SEND, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;

            if (success)
            {
                Debug.Log($"[DM] Message sent to {recipientId}");

                // 햅틱 피드백
                if (UIFeedbackManager.Instance != null)
                    UIFeedbackManager.Instance.TriggerLightHaptic();
            }
            else
            {
                Debug.LogError($"[DM] Failed to send message: {request.error}");

                // 사용자에게 토스트 피드백 표시
                if (ToastManager.Instance != null)
                {
                    ToastManager.Instance.ShowError("메시지 전송에 실패했습니다. 다시 시도해주세요.");
                }

                // 에러 메시지 확인
                if (request.downloadHandler != null)
                {
                    var errorResponse = JsonUtility.FromJson<DMErrorResponse>(request.downloadHandler.text);
                    if (!string.IsNullOrEmpty(errorResponse?.error))
                    {
                        Debug.LogWarning($"[DM] Server error: {errorResponse.error}");
                    }
                }
            }

            callback?.Invoke(success);
        }
    }

    private IEnumerator MarkConversationAsRead(string senderId)
    {
        string userId = LoginManager.Instance.CurrentUser.id;

        var postData = new DMReadAllRequest
        {
            user_id = userId,
            sender_id = senderId
        };

        string json = JsonUtility.ToJson(postData);

        using (UnityWebRequest request = new UnityWebRequest(ApiConfig.DM_READ_ALL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 안 읽은 수 갱신
                StartCoroutine(FetchUnreadCount());
            }
        }
    }

    private IEnumerator FetchUnreadCount()
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn) yield break;

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.DM_UNREAD_COUNT}?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DMUnreadCountResponse>(request.downloadHandler.text);
                unreadCount = response.unread_count;
                UpdateUnreadUI();
            }
        }
    }

    private IEnumerator DeleteMessage(int messageId, Action<bool> callback)
    {
        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.MAIN_SERVER}/api/dm/{messageId}?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Delete(url))
        {
            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
            callback?.Invoke(success);
        }
    }

    #endregion

    #region UI Population

    private void PopulateInbox()
    {
        // 기존 아이템 삭제
        if (inboxContent != null)
        {
            foreach (Transform child in inboxContent)
                Destroy(child.gameObject);
        }

        if (dmItemPrefab == null || inboxContent == null) return;

        foreach (var msg in inboxMessages)
        {
            GameObject item = Instantiate(dmItemPrefab, inboxContent);
            SetupDMItem(item, msg);
        }
    }

    private void SetupDMItem(GameObject item, DMMessage msg)
    {
        // 사용자명
        Text usernameText = item.transform.Find("UsernameText")?.GetComponent<Text>();
        if (usernameText != null)
            usernameText.text = msg.sender_username ?? "Unknown";

        // 메시지 미리보기
        Text contentText = item.transform.Find("ContentText")?.GetComponent<Text>();
        if (contentText != null)
        {
            string preview = msg.content;
            if (preview.Length > 30)
                preview = preview.Substring(0, 30) + "...";
            contentText.text = preview;
        }

        // 시간
        Text timeText = item.transform.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
            timeText.text = GetRelativeTime(msg.created_at);

        // 읽지 않음 표시
        GameObject unreadDot = item.transform.Find("UnreadDot")?.gameObject;
        if (unreadDot != null)
            unreadDot.SetActive(!msg.is_read);

        // 아바타
        Image avatar = item.transform.Find("Avatar")?.GetComponent<Image>();
        if (avatar != null && !string.IsNullOrEmpty(msg.sender_avatar_url))
            StartCoroutine(LoadAvatar(msg.sender_avatar_url, avatar));

        // 클릭 이벤트
        Button button = item.GetComponent<Button>();
        if (button == null)
            button = item.AddComponent<Button>();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            OpenConversation(msg.sender_id, msg.sender_username, msg.sender_avatar_url);
        });
    }

    private void PopulateConversation()
    {
        // 기존 메시지 삭제
        if (messageContent != null)
        {
            foreach (Transform child in messageContent)
                Destroy(child.gameObject);
        }

        string myUserId = LoginManager.Instance.CurrentUser.id;

        foreach (var msg in conversationMessages)
        {
            bool isMine = msg.sender_id == myUserId || msg.is_mine;
            GameObject prefab = isMine ? myMessagePrefab : otherMessagePrefab;

            if (prefab == null || messageContent == null) continue;

            GameObject item = Instantiate(prefab, messageContent);
            SetupMessageBubble(item, msg, isMine);
        }
    }

    private void SetupMessageBubble(GameObject item, DMMessage msg, bool isMine)
    {
        // 메시지 내용
        Text contentText = item.transform.Find("ContentText")?.GetComponent<Text>();
        if (contentText != null)
            contentText.text = msg.content;

        // 시간
        Text timeText = item.transform.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
            timeText.text = GetShortTime(msg.created_at);

        // 읽음 표시 (내 메시지만)
        if (isMine)
        {
            Text readText = item.transform.Find("ReadText")?.GetComponent<Text>();
            if (readText != null)
                readText.text = msg.is_read ? "읽음" : "";
        }
    }

    private void PopulateFollowingList(List<FollowUser> following)
    {
        // 기존 아이템 삭제
        if (followingListContent != null)
        {
            foreach (Transform child in followingListContent)
                Destroy(child.gameObject);
        }

        if (followingItemPrefab == null || followingListContent == null) return;

        foreach (var user in following)
        {
            GameObject item = Instantiate(followingItemPrefab, followingListContent);
            SetupFollowingItem(item, user);
        }
    }

    private void SetupFollowingItem(GameObject item, FollowUser user)
    {
        // 사용자명
        Text usernameText = item.transform.Find("UsernameText")?.GetComponent<Text>();
        if (usernameText != null)
            usernameText.text = user.username;

        // 아바타
        Image avatar = item.transform.Find("Avatar")?.GetComponent<Image>();
        if (avatar != null && !string.IsNullOrEmpty(user.avatar_url))
            StartCoroutine(LoadAvatar(user.avatar_url, avatar));

        // 클릭 이벤트
        Button button = item.GetComponent<Button>();
        if (button == null)
            button = item.AddComponent<Button>();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            OpenConversation(user.id, user.username, user.avatar_url);
        });
    }

    private void UpdateUnreadUI()
    {
        if (unreadCountText != null)
            unreadCountText.text = unreadCount > 0 ? unreadCount.ToString() : "";

        if (unreadIndicator != null)
            unreadIndicator.SetActive(unreadCount > 0);
    }

    #endregion

    #region Helpers

    private bool CheckLogin()
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[DM] Login required");
            LoginManager.Instance?.ShowLoginRequirementPopup();
            return false;
        }
        return true;
    }

    private void HideAllPanels()
    {
        if (dmPanel != null) dmPanel.SetActive(false);
        if (conversationPanel != null) conversationPanel.SetActive(false);
        if (followingSelectPanel != null) followingSelectPanel.SetActive(false);
    }

    private void ScrollToBottom()
    {
        if (scrollRect != null)
            StartCoroutine(ScrollToBottomCoroutine());
    }

    private IEnumerator ScrollToBottomCoroutine()
    {
        yield return null; // 레이아웃 업데이트 대기
        if (scrollRect != null)
            scrollRect.normalizedPosition = Vector2.zero;
    }

    private IEnumerator LoadAvatar(string url, Image targetImage)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture != null && targetImage != null)
                {
                    Sprite sprite = Sprite.Create(texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
                    targetImage.sprite = sprite;
                }
            }
        }
    }

    private string GetRelativeTime(string dateStr)
    {
        if (!DateTime.TryParse(dateStr, out DateTime date)) return dateStr ?? "";

        TimeSpan diff = DateTime.Now - date;
        double minutes = diff.TotalMinutes;
        double hours = diff.TotalHours;
        double days = diff.TotalDays;

        if (minutes < 1) return "방금";
        if (minutes < 60) return $"{(int)minutes}분 전";
        if (hours < 24) return $"{(int)hours}시간 전";
        if (days < 7) return $"{(int)days}일 전";
        return date.ToString("MM/dd");
    }

    private string GetShortTime(string dateStr)
    {
        if (!DateTime.TryParse(dateStr, out DateTime date)) return "";
        return date.ToString("HH:mm");
    }

    private void StartPolling()
    {
        if (pollingCoroutine != null)
            StopCoroutine(pollingCoroutine);

        pollingCoroutine = StartCoroutine(PollForNewMessages());
    }

    private void StopPolling()
    {
        if (pollingCoroutine != null)
        {
            StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }
    }

    private bool isAppFocused = true;
    private const float BACKGROUND_POLL_INTERVAL = 60f; // 백그라운드: 60초

    void OnApplicationFocus(bool hasFocus)
    {
        isAppFocused = hasFocus;
        Debug.Log($"[DM] App focus changed: {hasFocus}");
    }

    void OnApplicationPause(bool pauseStatus)
    {
        isAppFocused = !pauseStatus;
        Debug.Log($"[DM] App pause changed: {pauseStatus}");
    }

    private IEnumerator PollForNewMessages()
    {
        while (true)
        {
            // 포그라운드/백그라운드에 따라 폴링 간격 조절
            float interval = isAppFocused ? POLL_INTERVAL : BACKGROUND_POLL_INTERVAL;
            yield return new WaitForSeconds(interval);

            if (LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn)
            {
                yield return FetchUnreadCount();
            }
        }
    }

    #endregion
}

#region Data Classes

[Serializable]
public class DMMessage
{
    public int id;
    public string sender_id;
    public string recipient_id;
    public string content;
    public bool is_read;
    public bool is_liked;
    public string created_at;
    public string sender_username;
    public string sender_avatar_url;
    public string recipient_username;
    public string recipient_avatar_url;
    public bool is_mine;
}

[Serializable]
public class DMInboxResponse
{
    public List<DMMessage> messages;
    public int unread_count;
    public int count;
}

[Serializable]
public class DMConversationResponse
{
    public List<DMMessage> messages;
    public int count;
    public DMOtherUser other_user;
}

[Serializable]
public class DMOtherUser
{
    public string id;
    public string username;
    public string avatar_url;
}

[Serializable]
public class DMSendRequest
{
    public string sender_id;
    public string recipient_id;
    public string content;
}

[Serializable]
public class DMReadAllRequest
{
    public string user_id;
    public string sender_id;
}

[Serializable]
public class DMUnreadCountResponse
{
    public int unread_count;
}

[Serializable]
public class DMErrorResponse
{
    public string error;
}

[Serializable]
public class FollowingListResponse
{
    public List<FollowUser> following;
    public int count;
}

#endregion
