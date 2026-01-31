using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 메시지 패널 매니저 - 대화 목록, 검색, 스와이프 삭제 등
/// WOOPANG 공식 공지가 항상 최상단에 표시됨
/// </summary>
public class MessagePanelManager : MonoBehaviour
{
    public static MessagePanelManager Instance { get; private set; }

    [Header("=== 메인 패널 ===")]
    public GameObject messagePanel;
    public Transform conversationListContent;
    public GameObject conversationItemPrefab;
    public GameObject adminNoticePrefab;

    [Header("=== 검색 UI ===")]
    public InputField searchInput;
    public Button searchButton;
    public GameObject searchResultPanel;
    public Transform searchResultContent;
    public GameObject searchResultItemPrefab;

    [Header("=== 대화방 UI ===")]
    public GameObject chatRoomPanel;
    public Transform chatMessageContent;
    public GameObject myMessageBubblePrefab;
    public GameObject otherMessageBubblePrefab;
    public GameObject adminMessageBubblePrefab;
    public InputField chatInput;
    public Button sendButton;
    public Text chatRoomTitle;
    public Image chatRoomAvatar;
    public Button chatRoomBackButton;

    [Header("=== 네비게이션 버튼 연결 ===")]
    [Tooltip("하단 네비게이션의 Message_Button (뒤로가기 시 이 버튼 클릭 트리거)")]
    public Button navigationMessageButton;

    [Header("=== 스와이프 삭제 설정 ===")]
    public float swipeThreshold = 100f;
    public Color deleteButtonColor = new Color(0.91f, 0.33f, 0.63f, 1f); // 핑크색 #E854A1

    [Header("=== 좋아요 하트 ===")]
    public GameObject heartAnimationPrefab;
    public Color heartColor = new Color(1f, 0.4f, 0.6f, 1f);

    [Header("=== 빈 상태 UI ===")]
    [Tooltip("메시지가 없을 때 표시할 텍스트")]
    public string emptyInboxMessage = "아직 메시지가 없습니다.\n친구에게 첫 메시지를 보내보세요!";
    public string emptySearchMessage = "검색 결과가 없습니다.";
    [Tooltip("채팅방에 대화가 없을 때 표시할 텍스트")]
    public string emptyChatMessage = "아직 대화가 없습니다.\n첫 메시지를 보내보세요!";
    private GameObject emptyStateObject;
    private GameObject chatEmptyStateObject;


    [Header("=== 안읽음 인디케이터 ===")]
    public GameObject globalUnreadIndicator;
    private int totalUnreadCount = 0;

    [Header("=== 메시지 폴링 ===")]
    [Tooltip("포그라운드 폴링 간격 (초)")]
    public float pollIntervalForeground = 10f;
    [Tooltip("백그라운드 폴링 간격 (초)")]
    public float pollIntervalBackground = 60f;
    private Coroutine pollingCoroutine;
    private bool isAppFocused = true;

    [Header("=== 테스트 모드 ===")]
    [Tooltip("에디터에서 테스트용 더미 메시지 생성")]
    public bool enableTestMode = true;  // 에디터에서 기본 활성화
    public float testMessageDelay = 2f;  // 빠른 로드

    [Header("=== 폰트 설정 ===")]
    [Tooltip("채팅용 커스텀 폰트 (AppleSDGothicNeoM)")]
    public Font chatFont;
    [Tooltip("채팅 메시지 폰트 크기")]
    public int chatFontSize = 60;

    // 더미 데이터 보존용 플래그
    private bool hasDummyDataLoaded = false;

    [Header("=== 채팅 버블 설정 ===")]
    [Tooltip("화면 너비 대비 최대 버블 너비 비율 (0.5 ~ 1.0)")]
    [Range(0.5f, 1.0f)]
    public float maxBubbleWidthRatio = 0.82f;

    [Tooltip("버블 최소 너비 (픽셀)")]
    public float minBubbleWidth = 120f;

    [Tooltip("버블 좌우 패딩 (픽셀)")]
    public float bubblePaddingH = 24f;

    [Tooltip("버블 상하 패딩 (픽셀)")]
    public float bubblePaddingV = 18f;

    [Tooltip("메시지 폰트 크기")]
    public int bubbleFontSize = 60;

    [Tooltip("줄 높이 (픽셀)")]
    public float bubbleLineHeight = 42f;

    [Tooltip("버블 최소 높이 (픽셀)")]
    public float minBubbleHeight = 60f;

    // 현재 대화 상대
    private string currentChatUserId;
    private string currentChatUsername;
    private bool isAdminChat;

    // 대화 목록 캐시
    private List<ConversationSummary> conversations = new List<ConversationSummary>();
    private List<AdminBroadcast> adminBroadcasts = new List<AdminBroadcast>();

    // 스와이프 추적
    private Dictionary<GameObject, Vector2> swipeStartPositions = new Dictionary<GameObject, Vector2>();
    private Dictionary<GameObject, bool> isSwipingItem = new Dictionary<GameObject, bool>();

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

        // 커스텀 폰트 로드 (AppleSDGothicNeoM)
        if (chatFont == null)
        {
            chatFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
            if (chatFont == null)
            {
                chatFont = Resources.Load<Font>("AppleSDGothicNeoM");
            }
        }
    }

    void Start()
    {
        SetupButtons();
        HideAllPanels();

        // 입력창 자동 확장 설정
        if (chatInput != null)
            AutoExpandInputField.Setup(chatInput, 50f, 150f);

        // 테스트 모드 (에디터에서만)
#if UNITY_EDITOR
        if (enableTestMode)
        {
            StartCoroutine(GenerateTestMessages());
        }
#endif
    }

    private void SetupButtons()
    {
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendButtonClicked);

        if (chatRoomBackButton != null)
            chatRoomBackButton.onClick.AddListener(CloseChatRoom);

        if (searchButton != null)
            searchButton.onClick.AddListener(OnSearchButtonClicked);

        if (searchInput != null)
        {
            searchInput.onEndEdit.AddListener(OnSearchSubmit);
        }
    }

    void OnDestroy()
    {
        StopPolling();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        isAppFocused = hasFocus;
    }

    void OnApplicationPause(bool pauseStatus)
    {
        isAppFocused = !pauseStatus;
    }

    #region Public Methods

    /// <summary>
    /// 메시지 패널 열기
    /// </summary>
    public void OpenMessagePanel()
    {
        HideAllPanels();
        if (messagePanel != null)
            messagePanel.SetActive(true);

        StartCoroutine(LoadConversationList());
        StartPolling();
    }

    /// <summary>
    /// 안 읽은 메시지 수 새로고침
    /// </summary>
    public void RefreshUnreadCount()
    {
        if (CheckLogin())
            StartCoroutine(FetchUnreadCount());
    }

    /// <summary>
    /// 현재 안읽음 카운트 반환
    /// </summary>
    public int GetUnreadCount()
    {
        return totalUnreadCount;
    }

    /// <summary>
    /// 메시지 패널 닫기
    /// </summary>
    public void CloseMessagePanel()
    {
        HideAllPanels();
    }

    // 채팅룸에서 돌아갈 때 메시지 패널 다시 열기 위한 플래그
    private bool openedFromMessagePanel = false;

    /// <summary>
    /// 특정 사용자와의 대화방 열기
    /// </summary>
    public void OpenChatRoom(string userId, string username, string avatarUrl = null, bool isAdmin = false)
    {
        currentChatUserId = userId;
        currentChatUsername = username;
        isAdminChat = isAdmin;

        // 메시지 패널에서 채팅룸으로 이동 시 메시지 패널 닫기
        if (messagePanel != null && messagePanel.activeSelf)
        {
            openedFromMessagePanel = true;
            messagePanel.SetActive(false);
        }
        else
        {
            openedFromMessagePanel = false;
        }

        if (chatRoomPanel != null)
            chatRoomPanel.SetActive(true);

        if (chatRoomTitle != null)
        {
            // 다국어 제목 적용: "사용자와의 대화" 형식
            if (LocalizationManager.Instance != null)
                chatRoomTitle.text = LocalizationManager.Instance.GetText("chat_with_user", username);
            else
                chatRoomTitle.text = $"{username}와의 대화";
        }

        if (chatRoomAvatar != null && !string.IsNullOrEmpty(avatarUrl))
            StartCoroutine(LoadAvatar(avatarUrl, chatRoomAvatar));

        StartCoroutine(LoadChatMessages(userId, isAdmin));

        // 읽음 처리
        if (!isAdmin)
            StartCoroutine(MarkMessagesAsRead(userId));
    }

    /// <summary>
    /// 대화방 닫기 (뒤로가기 - Message_Button 클릭 시뮬레이션)
    /// </summary>
    public void CloseChatRoom()
    {
        if (chatRoomPanel != null)
            chatRoomPanel.SetActive(false);

        // 채팅방 빈 상태 UI 정리
        ShowChatEmptyState(false);

        currentChatUserId = null;
        currentChatUsername = null;
        isAdminChat = false;
        openedFromMessagePanel = false;

        // Message_Button 클릭 트리거 (모든 관련 로직 실행)
        if (navigationMessageButton != null)
        {
            navigationMessageButton.onClick.Invoke();
        }
        else
        {
            // 폴백: 직접 메시지 패널 열기
            Debug.LogWarning("[MessagePanelManager] navigationMessageButton이 연결되지 않음. 직접 열기.");
            OpenMessagePanel();
        }
    }

    /// <summary>
    /// 대화 삭제 (스와이프로 호출)
    /// </summary>
    public void DeleteConversation(string otherUserId)
    {
        StartCoroutine(DeleteConversationCoroutine(otherUserId));
    }

    /// <summary>
    /// 메시지에 좋아요 (더블터치)
    /// </summary>
    public void LikeMessage(int messageId, RectTransform messageRect)
    {
        StartCoroutine(LikeMessageCoroutine(messageId, messageRect, true));
    }

    /// <summary>
    /// 메시지 좋아요 토글 (하트 아이콘 탭)
    /// </summary>
    public void ToggleLikeMessage(int messageId, bool setLiked, HeartLikeButtonHandler handler)
    {
        StartCoroutine(ToggleLikeCoroutine(messageId, setLiked, handler));
    }

    #endregion

    #region Conversation List

    private IEnumerator LoadConversationList()
    {
        ClearContent(conversationListContent);

#if UNITY_EDITOR
        // 에디터에서 더미 데이터가 이미 로드되어 있으면 캐시된 데이터 재사용
        if (enableTestMode && hasDummyDataLoaded && conversations.Count > 0)
        {
            // 관리자 공지 UI 생성
            foreach (var broadcast in adminBroadcasts)
            {
                CreateAdminNoticeItem(broadcast);
            }

            // 캐시된 대화 목록 UI 생성
            foreach (var conv in conversations)
            {
                CreateConversationItem(conv);
            }

            yield break;
        }
#endif

        // 1. 관리자 공지 로드 (항상 최상단)
        yield return StartCoroutine(LoadAdminBroadcasts());

        // 관리자 공지 UI 생성
        foreach (var broadcast in adminBroadcasts)
        {
            CreateAdminNoticeItem(broadcast);
        }

        // 2. 일반 대화 목록 로드
        if (!CheckLogin())
        {
#if UNITY_EDITOR
            // 에디터에서 로그인 안 된 경우 더미 데이터 표시
            if (enableTestMode)
            {
                LoadDummyConversations();
            }
#endif
            yield break;
        }

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.DM_INBOX}?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DMInboxResponse>(request.downloadHandler.text);

                // 대화 요약 생성
                conversations.Clear();
                var grouped = GroupMessagesByUser(response.messages);

                foreach (var conv in grouped)
                {
                    CreateConversationItem(conv);
                    conversations.Add(conv);
                }

                // 빈 상태 UI 처리
                bool isEmpty = conversations.Count == 0 && adminBroadcasts.Count == 0;
                ShowEmptyState(conversationListContent, emptyInboxMessage, isEmpty);
            }
            else
            {
#if UNITY_EDITOR
                // 에디터에서 서버 오류 시 더미 데이터 표시
                if (enableTestMode)
                {
                    LoadDummyConversations();
                }
                else
                {
                    ShowEmptyState(conversationListContent, "메시지를 불러올 수 없습니다.", true);
                }
#else
                // 에러 시 빈 상태 표시
                ShowEmptyState(conversationListContent, "메시지를 불러올 수 없습니다.", true);
#endif
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터용 더미 대화 목록 로드
    /// </summary>
    private void LoadDummyConversations()
    {
        if (hasDummyDataLoaded && conversations.Count > 0)
        {
            // 이미 더미 데이터가 있으면 UI만 다시 생성
            foreach (var conv in conversations)
            {
                CreateConversationItem(conv);
            }
            return;
        }

        conversations.Clear();

        // 더미 대화 데이터
        conversations.Add(new ConversationSummary
        {
            userId = "test_user_1",
            username = "김민지",
            lastMessage = "안녕하세요! 오늘 AR 콘텐츠 봤어요 👀",
            lastMessageTime = DateTime.Now.AddMinutes(-5).ToString("yyyy-MM-dd HH:mm:ss"),
            unreadCount = 2
        });

        conversations.Add(new ConversationSummary
        {
            userId = "test_user_2",
            username = "이준호",
            lastMessage = "주말에 같이 우팡 촬영 어때요?",
            lastMessageTime = DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss"),
            unreadCount = 0
        });

        conversations.Add(new ConversationSummary
        {
            userId = "test_user_3",
            username = "박서연",
            lastMessage = "저도 AR 아티스트예요! 반가워요 ✨",
            lastMessageTime = DateTime.Now.AddHours(-3).ToString("yyyy-MM-dd HH:mm:ss"),
            unreadCount = 1
        });

        conversations.Add(new ConversationSummary
        {
            userId = "test_user_4",
            username = "최영수",
            lastMessage = "사진 잘 찍으셨네요!",
            lastMessageTime = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"),
            unreadCount = 0
        });

        conversations.Add(new ConversationSummary
        {
            userId = "test_user_5",
            username = "WOOPANG_크리에이터",
            lastMessage = "다음 업데이트 기대해주세요 🚀",
            lastMessageTime = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd HH:mm:ss"),
            unreadCount = 0
        });

        foreach (var conv in conversations)
        {
            CreateConversationItem(conv);
        }

        hasDummyDataLoaded = true;
    }
#endif

    private IEnumerator LoadAdminBroadcasts()
    {
        string url = $"{ApiConfig.MAIN_SERVER}/api/broadcast/list?limit=5";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<AdminBroadcastListResponse>(request.downloadHandler.text);
                adminBroadcasts = response.broadcasts ?? new List<AdminBroadcast>();
            }
            else
            {
                // 테스트용 더미 데이터
                adminBroadcasts = new List<AdminBroadcast>
                {
                    new AdminBroadcast
                    {
                        id = 1,
                        title = "WOOPANG",
                        content = "새로운 업데이트가 있습니다! 지금 확인해보세요.",
                        created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    }
                };
            }
        }
    }

    private List<ConversationSummary> GroupMessagesByUser(List<DMMessage> messages)
    {
        var result = new List<ConversationSummary>();
        var userMap = new Dictionary<string, ConversationSummary>();
        string myUserId = LoginManager.Instance?.CurrentUser?.id ?? "";

        foreach (var msg in messages)
        {
            string otherUserId = msg.sender_id == myUserId ? msg.recipient_id : msg.sender_id;
            string otherUsername = msg.sender_id == myUserId ? msg.recipient_username : msg.sender_username;
            string otherAvatar = msg.sender_id == myUserId ? msg.recipient_avatar_url : msg.sender_avatar_url;

            if (!userMap.ContainsKey(otherUserId))
            {
                userMap[otherUserId] = new ConversationSummary
                {
                    userId = otherUserId,
                    username = otherUsername,
                    avatarUrl = otherAvatar,
                    lastMessage = msg.content,
                    lastMessageTime = msg.created_at,
                    unreadCount = 0
                };
            }

            // 안 읽은 메시지 카운트
            if (!msg.is_read && msg.sender_id != myUserId)
            {
                userMap[otherUserId].unreadCount++;
            }
        }

        result.AddRange(userMap.Values);
        return result;
    }

    private void CreateAdminNoticeItem(AdminBroadcast broadcast)
    {
        if (adminNoticePrefab == null || conversationListContent == null) return;

        GameObject item = Instantiate(adminNoticePrefab, conversationListContent);
        SetupAdminNoticeItem(item, broadcast);
    }

    private void SetupAdminNoticeItem(GameObject item, AdminBroadcast broadcast)
    {
        // 제목
        Text titleText = item.transform.Find("TitleText")?.GetComponent<Text>();
        if (titleText != null)
            titleText.text = "WOOPANG";

        // 미리보기
        Text previewText = item.transform.Find("PreviewText")?.GetComponent<Text>();
        if (previewText != null)
        {
            string preview = broadcast.content;
            if (preview.Length > 30)
                preview = preview.Substring(0, 30) + "...";
            previewText.text = preview;
        }

        // 시간
        Text timeText = item.transform.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
            timeText.text = GetRelativeTime(broadcast.created_at);

        // 클릭 이벤트
        Button btn = item.GetComponent<Button>();
        if (btn == null)
            btn = item.AddComponent<Button>();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            OpenChatRoom("woopang", "WOOPANG", null, true);
        });
    }

    private void CreateConversationItem(ConversationSummary conv)
    {
        if (conversationItemPrefab == null || conversationListContent == null) return;

        GameObject item = Instantiate(conversationItemPrefab, conversationListContent);
        SetupConversationItem(item, conv);
        SetupSwipeDelete(item, conv.userId);
    }

    private void SetupConversationItem(GameObject item, ConversationSummary conv)
    {
        // 아이템 높이 설정 - 프리팹에 LayoutElement가 있으면 그 값 존중
        LayoutElement itemLE = item.GetComponent<LayoutElement>();
        if (itemLE == null)
        {
            itemLE = item.AddComponent<LayoutElement>();
            itemLE.minHeight = 120f;
            itemLE.preferredHeight = 120f;
        }
        // 프리팹에 이미 설정된 값이 있으면 그대로 사용

        // 사용자명
        Text usernameText = item.transform.Find("UsernameText")?.GetComponent<Text>();
        if (usernameText != null)
        {
            usernameText.text = conv.username ?? "Unknown";
            // fontSize는 프리팹 값 사용
        }

        // 미리보기 - 영역 크기에 맞게 자동 ellipsis 처리
        Text previewText = item.transform.Find("PreviewText")?.GetComponent<Text>();
        if (previewText != null)
        {
            SetTextWithEllipsis(previewText, conv.lastMessage);
        }

        // 시간
        Text timeText = item.transform.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
        {
            timeText.text = GetRelativeTime(conv.lastMessageTime);
            // fontSize는 프리팹 값 사용
        }

        // 안 읽음 표시
        GameObject unreadBadge = item.transform.Find("UnreadBadge")?.gameObject;
        Text unreadText = item.transform.Find("UnreadBadge/UnreadCount")?.GetComponent<Text>();
        if (unreadBadge != null)
        {
            unreadBadge.SetActive(conv.unreadCount > 0);
            if (unreadText != null)
            {
                unreadText.text = conv.unreadCount.ToString();
                // fontSize는 프리팹 값 사용
            }
        }

        // 아바타
        Image avatar = item.transform.Find("Avatar")?.GetComponent<Image>();
        if (avatar != null && !string.IsNullOrEmpty(conv.avatarUrl))
            StartCoroutine(LoadAvatar(conv.avatarUrl, avatar));

        // 클릭 이벤트 (Content 영역에)
        Transform contentArea = item.transform.Find("Content");
        if (contentArea != null)
        {
            Button btn = contentArea.GetComponent<Button>();
            if (btn == null)
                btn = contentArea.gameObject.AddComponent<Button>();

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                OpenChatRoom(conv.userId, conv.username, conv.avatarUrl);
            });
        }
    }

    #endregion

    #region Swipe Delete

    private void SetupSwipeDelete(GameObject item, string userId)
    {
        SwipeToDeleteHandler handler = item.GetComponent<SwipeToDeleteHandler>();
        if (handler == null)
            handler = item.AddComponent<SwipeToDeleteHandler>();

        handler.Initialize(userId, swipeThreshold, () =>
        {
            DeleteConversation(userId);
        });
    }

    private IEnumerator DeleteConversationCoroutine(string otherUserId)
    {
        if (!CheckLogin()) yield break;

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.MAIN_SERVER}/api/dm/conversation?user_id={userId}&other_id={otherUserId}";

        using (UnityWebRequest request = UnityWebRequest.Delete(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[MessagePanel] Conversation deleted: {otherUserId}");

                // UI에서 제거
                foreach (Transform child in conversationListContent)
                {
                    var handler = child.GetComponent<SwipeToDeleteHandler>();
                    if (handler != null && handler.userId == otherUserId)
                    {
                        Destroy(child.gameObject);
                        break;
                    }
                }

                // 햅틱 피드백
                if (UIFeedbackManager.Instance != null)
                    UIFeedbackManager.Instance.TriggerMediumHaptic();
            }
        }
    }

    #endregion

    #region Chat Room

    private IEnumerator LoadChatMessages(string otherUserId, bool isAdmin)
    {
        ClearContent(chatMessageContent);
        ShowChatEmptyState(false); // 기존 빈 상태 제거

        if (isAdmin)
        {
            // 관리자 공지 메시지 표시
            foreach (var broadcast in adminBroadcasts)
            {
                CreateAdminMessageBubble(broadcast);
            }
            yield break;
        }

        if (!CheckLogin())
        {
#if UNITY_EDITOR
            // 에디터에서 로그인 안 된 경우 테스트 메시지 표시
            if (enableTestMode)
            {
                CreateTestChatMessages(otherUserId);
                yield return null;
                ScrollToBottom();
            }
            else
            {
                // 테스트 모드가 아니면 빈 상태 표시
                ShowChatEmptyState(true);
            }
#else
            ShowChatEmptyState(true);
#endif
            yield break;
        }

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.DM_CONVERSATION}?user_id={userId}&other_id={otherUserId}";

        bool hasMessages = false;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DMConversationResponse>(request.downloadHandler.text);

                if (response.messages != null && response.messages.Count > 0)
                {
                    hasMessages = true;
                    foreach (var msg in response.messages)
                    {
                        bool isMine = msg.sender_id == userId || msg.is_mine;
                        CreateMessageBubble(msg, isMine);
                    }
                }

                // 스크롤 맨 아래로
                yield return null;
                ScrollToBottom();
            }
        }

        // 메시지가 없으면 빈 상태 표시
        if (!hasMessages)
        {
#if UNITY_EDITOR
            // 에디터 테스트 모드: 메시지가 없으면 테스트 메시지 표시
            if (enableTestMode)
            {
                CreateTestChatMessages(otherUserId);
                yield return null;
                ScrollToBottom();
            }
            else
            {
                ShowChatEmptyState(true);
            }
#else
            ShowChatEmptyState(true);
#endif
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터 테스트용 채팅 메시지 생성
    /// </summary>
    private void CreateTestChatMessages(string otherUserId)
    {
        string myId = LoginManager.Instance?.CurrentUser?.id ?? "my_test_id";

        // 테스트 메시지들
        var testMessages = new DMMessage[]
        {
            new DMMessage
            {
                sender_id = otherUserId,
                content = "안녕하세요! 👋",
                created_at = DateTime.Now.AddMinutes(-30).ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = true
            },
            new DMMessage
            {
                sender_id = myId,
                content = "안녕하세요! 반갑습니다 😊",
                created_at = DateTime.Now.AddMinutes(-28).ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = true,
                is_mine = true
            },
            new DMMessage
            {
                sender_id = otherUserId,
                content = "우팡 AR 앱 정말 재미있네요! 어디서 만들 수 있나요?",
                created_at = DateTime.Now.AddMinutes(-25).ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = true
            },
            new DMMessage
            {
                sender_id = myId,
                content = "감사합니다! 앱에서 + 버튼을 눌러서 새로운 장소를 추가할 수 있어요.",
                created_at = DateTime.Now.AddMinutes(-20).ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = true,
                is_mine = true
            },
            new DMMessage
            {
                sender_id = otherUserId,
                content = "오 정말요? 한번 해볼게요! 🎉",
                created_at = DateTime.Now.AddMinutes(-15).ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = false
            }
        };

        foreach (var msg in testMessages)
        {
            bool isMine = msg.sender_id == myId || msg.is_mine;
            CreateMessageBubble(msg, isMine);
        }
    }
#endif

    private void CreateMessageBubble(DMMessage msg, bool isMine)
    {
        GameObject prefab = isMine ? myMessageBubblePrefab : otherMessageBubblePrefab;
        if (prefab == null || chatMessageContent == null) return;

        GameObject item = Instantiate(prefab, chatMessageContent);
        SetupMessageBubble(item, msg, isMine);
    }

    private void SetupMessageBubble(GameObject item, DMMessage msg, bool isMine)
    {
        // 버블 최소 높이 설정
        LayoutElement itemLE = item.GetComponent<LayoutElement>();
        if (itemLE == null) itemLE = item.AddComponent<LayoutElement>();
        itemLE.minHeight = ChatBubbleLayoutHelper.MIN_BUBBLE_HEIGHT;

        // 내용
        Text contentText = item.transform.Find("ContentText")?.GetComponent<Text>();
        if (contentText != null)
        {
            contentText.text = msg.content;
            contentText.fontSize = ChatBubbleLayoutHelper.FONT_SIZE;
            contentText.lineSpacing = 1.2f;
            // 텍스트 자동 줄바꿈 및 높이 확장 설정
            contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contentText.verticalOverflow = VerticalWrapMode.Overflow;
            // 커스텀 폰트 적용 (AppleSDGothicNeoM)
            if (chatFont != null)
                contentText.font = chatFont;

            // ContentSizeFitter로 텍스트에 맞게 버블 높이 자동 조절
            ContentSizeFitter textCsf = contentText.GetComponent<ContentSizeFitter>();
            if (textCsf == null)
                textCsf = contentText.gameObject.AddComponent<ContentSizeFitter>();
            textCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            textCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 텍스트 LayoutElement 설정
            LayoutElement textLE = contentText.GetComponent<LayoutElement>();
            if (textLE == null)
                textLE = contentText.gameObject.AddComponent<LayoutElement>();
            textLE.minHeight = ChatBubbleLayoutHelper.MIN_BUBBLE_HEIGHT;
        }

        // 버블 전체에도 ContentSizeFitter 적용
        ContentSizeFitter itemCsf = item.GetComponent<ContentSizeFitter>();
        if (itemCsf == null)
            itemCsf = item.AddComponent<ContentSizeFitter>();
        itemCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        itemCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 시간
        Text timeText = item.transform.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
        {
            timeText.text = GetShortTime(msg.created_at);
            timeText.fontSize = 22;
            // 커스텀 폰트 적용
            if (chatFont != null)
                timeText.font = chatFont;
        }

        // 읽음 표시 (내 메시지) - 체크마크 스타일
        if (isMine)
        {
            Text readText = item.transform.Find("ReadText")?.GetComponent<Text>();
            if (readText != null)
            {
                // ✓ 단일 체크 = 전송됨, ✓✓ 더블 체크 = 읽음
                readText.text = msg.is_read ? "✓✓" : "✓";
                readText.color = msg.is_read ? new Color(0.3f, 0.7f, 1f) : new Color(0.6f, 0.6f, 0.6f);
                readText.fontSize = 20;
            }
        }

        // 좋아요 하트 표시 및 핸들러 설정
        Transform heartIconTr = item.transform.Find("HeartIcon");
        if (heartIconTr != null)
        {
            heartIconTr.gameObject.SetActive(msg.is_liked);

            // 하트 아이콘에 클릭 핸들러 추가 (좋아요 취소용)
            if (msg.is_liked)
            {
                HeartLikeButtonHandler heartHandler = heartIconTr.GetComponent<HeartLikeButtonHandler>();
                if (heartHandler == null)
                    heartHandler = heartIconTr.gameObject.AddComponent<HeartLikeButtonHandler>();

                heartHandler.Initialize(msg.id, true, (msgId, newLiked) =>
                {
                    ToggleLikeMessage(msgId, newLiked, heartHandler);
                });
            }
        }

        // 더블터치 좋아요 핸들러 (메시지 버블 전체)
        DoubleTouchLikeHandler likeHandler = item.GetComponent<DoubleTouchLikeHandler>();
        if (likeHandler == null)
            likeHandler = item.AddComponent<DoubleTouchLikeHandler>();

        likeHandler.Initialize(msg.id, item.GetComponent<RectTransform>(), (messageId, rect) =>
        {
            LikeMessage(messageId, rect);
        });
    }

    private void CreateAdminMessageBubble(AdminBroadcast broadcast)
    {
        GameObject prefab = adminMessageBubblePrefab ?? otherMessageBubblePrefab;
        if (prefab == null || chatMessageContent == null) return;

        GameObject item = Instantiate(prefab, chatMessageContent);

        // 버블 최소 높이 설정
        LayoutElement itemLE = item.GetComponent<LayoutElement>();
        if (itemLE == null) itemLE = item.AddComponent<LayoutElement>();
        itemLE.minHeight = ChatBubbleLayoutHelper.MIN_BUBBLE_HEIGHT;

        // 내용
        Text contentText = item.transform.Find("ContentText")?.GetComponent<Text>();
        if (contentText != null)
        {
            contentText.text = broadcast.content;
            contentText.fontSize = ChatBubbleLayoutHelper.FONT_SIZE;
            contentText.lineSpacing = 1.2f;
            // 텍스트 자동 줄바꿈 및 높이 확장 설정
            contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contentText.verticalOverflow = VerticalWrapMode.Overflow;
            // 커스텀 폰트 적용 (AppleSDGothicNeoM)
            if (chatFont != null)
                contentText.font = chatFont;

            // ContentSizeFitter로 텍스트에 맞게 버블 높이 자동 조절
            ContentSizeFitter textCsf = contentText.GetComponent<ContentSizeFitter>();
            if (textCsf == null)
                textCsf = contentText.gameObject.AddComponent<ContentSizeFitter>();
            textCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            textCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // 버블 전체에도 ContentSizeFitter 적용
        ContentSizeFitter itemCsf = item.GetComponent<ContentSizeFitter>();
        if (itemCsf == null)
            itemCsf = item.AddComponent<ContentSizeFitter>();
        itemCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        itemCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 시간
        Text timeText = item.transform.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
        {
            timeText.text = GetShortTime(broadcast.created_at);
            timeText.fontSize = 22;
            // 커스텀 폰트 적용
            if (chatFont != null)
                timeText.font = chatFont;
        }
    }

    private void OnSendButtonClicked()
    {
        if (string.IsNullOrEmpty(currentChatUserId) || isAdminChat) return;

        string content = chatInput?.text?.Trim();
        if (string.IsNullOrEmpty(content)) return;

        // 입력 초기화
        if (chatInput != null)
            chatInput.text = "";

        StartCoroutine(SendMessageCoroutine(currentChatUserId, content));
    }

    private IEnumerator SendMessageCoroutine(string recipientId, string content)
    {
        if (!CheckLogin()) yield break;

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

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 대화 새로고침
                StartCoroutine(LoadChatMessages(recipientId, false));

                // 햅틱 피드백
                if (UIFeedbackManager.Instance != null)
                    UIFeedbackManager.Instance.TriggerLightHaptic();
            }
            else
            {
                Debug.LogError($"[MessagePanel] Failed to send: {request.error}");
            }
        }
    }

    /// <summary>
    /// 외부에서 메시지 전송 (ChatPanelManager 등에서 사용)
    /// </summary>
    /// <param name="recipientId">수신자 ID</param>
    /// <param name="content">메시지 내용</param>
    /// <param name="callback">전송 결과 콜백 (성공/실패)</param>
    public void SendMessageToUser(string recipientId, string content, System.Action<bool> callback = null)
    {
        StartCoroutine(SendMessageWithCallbackCoroutine(recipientId, content, callback));
    }

    private IEnumerator SendMessageWithCallbackCoroutine(string recipientId, string content, System.Action<bool> callback)
    {
        if (!CheckLogin())
        {
            callback?.Invoke(false);
            yield break;
        }

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
                Debug.Log($"[MessagePanel] Message sent to {recipientId}");
            }
            else
            {
                Debug.LogError($"[MessagePanel] Failed to send: {request.error}");
            }

            callback?.Invoke(success);
        }
    }

    private IEnumerator MarkMessagesAsRead(string senderId)
    {
        if (!CheckLogin()) yield break;

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
        }
    }

    #endregion

    #region Search

    /// <summary>
    /// X 버튼 클릭 - 검색창 내용 삭제
    /// </summary>
    private void OnSearchButtonClicked()
    {
        ClearSearchInput();
    }

    /// <summary>
    /// 검색창 내용 삭제 및 드롭다운 숨김
    /// </summary>
    private void ClearSearchInput()
    {
        if (searchInput != null)
        {
            searchInput.text = "";
        }

        // 검색 결과 패널 숨김
        if (searchResultPanel != null)
            searchResultPanel.SetActive(false);
    }

    private void OnSearchSubmit(string query)
    {
        if (!string.IsNullOrEmpty(query))
        {
            StartCoroutine(SearchUsers(query));
        }
    }

    private IEnumerator SearchUsers(string query)
    {
        if (!CheckLogin()) yield break;

        // 검색 결과 패널 표시
        if (searchResultPanel != null)
            searchResultPanel.SetActive(true);

        ClearContent(searchResultContent);

        // 팔로잉 유저만 검색 (following_only=true)
        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.MAIN_SERVER}/api/users/search?q={UnityWebRequest.EscapeURL(query)}&user_id={userId}&following_only=true";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<UserSearchResponse>(request.downloadHandler.text);

                // 팔로잉 유저만 필터링 (서버에서 처리 안 될 경우 클라이언트에서 필터)
                int displayCount = 0;
                foreach (var user in response.users)
                {
                    if (user.is_following)
                    {
                        CreateSearchResultItem(user);
                        displayCount++;
                    }
                }

                // 검색 결과가 없을 때 메시지 표시
                if (displayCount == 0)
                {
                    ShowEmptyState(searchResultContent, GetLocalizedSearchEmptyMessage(), true);
                }
            }
        }
    }

    private void CreateSearchResultItem(SearchedUser user)
    {
        if (searchResultItemPrefab == null || searchResultContent == null) return;

        GameObject item = Instantiate(searchResultItemPrefab, searchResultContent);

        // 사용자명
        Text usernameText = item.transform.Find("UsernameText")?.GetComponent<Text>();
        if (usernameText != null)
            usernameText.text = user.username;

        // 클릭 이벤트 - 바로 채팅방 열기 (팔로잉 유저만 표시되므로)
        Button btn = item.GetComponent<Button>();
        if (btn == null)
            btn = item.AddComponent<Button>();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            if (searchResultPanel != null)
                searchResultPanel.SetActive(false);
            OpenChatRoom(user.id, user.username, user.avatar_url);
        });

        // 아바타
        Image avatar = item.transform.Find("Avatar")?.GetComponent<Image>();
        if (avatar != null && !string.IsNullOrEmpty(user.avatar_url))
            StartCoroutine(LoadAvatar(user.avatar_url, avatar));
    }

    /// <summary>
    /// 검색 결과 없음 메시지 다국어
    /// </summary>
    private string GetLocalizedSearchEmptyMessage()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return "팔로잉 중인 사용자 중 검색 결과가 없습니다.";
            case SystemLanguage.Japanese:
                return "フォロー中のユーザーに該当する結果がありません。";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return "在关注的用户中没有搜索结果。";
            case SystemLanguage.Spanish:
                return "No hay resultados entre los usuarios que sigues.";
            default:
                return "No results found among users you follow.";
        }
    }

    #endregion

    #region Like Message

    private IEnumerator LikeMessageCoroutine(int messageId, RectTransform messageRect, bool setLiked)
    {
        if (!CheckLogin()) yield break;

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.MAIN_SERVER}/api/dm/{messageId}/like";

        var postData = new LikeRequest { user_id = userId, set_liked = setLiked };
        string json = JsonUtility.ToJson(postData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (setLiked)
                {
                    // 하트 애니메이션 표시 (좋아요 할 때만)
                    ShowHeartAnimation(messageRect);

                    // 하트 아이콘 영구 표시
                    ShowPersistentHeart(messageRect, messageId);
                }
                else
                {
                    // 좋아요 취소 시 하트 아이콘 숨김
                    HidePersistentHeart(messageRect);
                }

                // 햅틱 피드백
                if (UIFeedbackManager.Instance != null)
                    UIFeedbackManager.Instance.TriggerLightHaptic();
            }
        }
    }

    private IEnumerator ToggleLikeCoroutine(int messageId, bool setLiked, HeartLikeButtonHandler handler)
    {
        if (!CheckLogin())
        {
            handler?.OnRequestComplete();
            yield break;
        }

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.MAIN_SERVER}/api/dm/{messageId}/like";

        var postData = new LikeRequest { user_id = userId, set_liked = setLiked };
        string json = JsonUtility.ToJson(postData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 핸들러 상태 업데이트
                handler?.UpdateLikedState(setLiked);

                // 하트 아이콘 표시/숨김
                Transform heartIcon = handler?.transform.parent?.Find("HeartIcon");
                if (heartIcon != null)
                {
                    heartIcon.gameObject.SetActive(setLiked);
                }

                // 햅틱 피드백
                if (UIFeedbackManager.Instance != null)
                    UIFeedbackManager.Instance.TriggerLightHaptic();

                Debug.Log($"[MessagePanel] Message {messageId} like status: {setLiked}");
            }
            else
            {
                Debug.LogError($"[MessagePanel] Failed to toggle like: {request.error}");
            }

            handler?.OnRequestComplete();
        }
    }

    private void ShowPersistentHeart(RectTransform messageRect, int messageId)
    {
        if (messageRect == null) return;

        // 기존 HeartIcon 찾기 또는 생성
        Transform existingHeart = messageRect.Find("HeartIcon");
        if (existingHeart != null)
        {
            existingHeart.gameObject.SetActive(true);
            return;
        }

        // 새 하트 아이콘 생성
        GameObject heartObj = new GameObject("HeartIcon");
        heartObj.transform.SetParent(messageRect, false);

        RectTransform heartRect = heartObj.AddComponent<RectTransform>();
        heartRect.anchorMin = new Vector2(1, 0);
        heartRect.anchorMax = new Vector2(1, 0);
        heartRect.pivot = new Vector2(1, 0);
        heartRect.sizeDelta = new Vector2(24, 24);
        heartRect.anchoredPosition = new Vector2(-5, 5);

        Image heartImg = heartObj.AddComponent<Image>();
        heartImg.color = heartColor;

        // 하트 아이콘 버튼 핸들러 추가
        HeartLikeButtonHandler handler = heartObj.AddComponent<HeartLikeButtonHandler>();
        handler.Initialize(messageId, true, (msgId, newLiked) =>
        {
            ToggleLikeMessage(msgId, newLiked, handler);
        });
    }

    private void HidePersistentHeart(RectTransform messageRect)
    {
        if (messageRect == null) return;

        Transform existingHeart = messageRect.Find("HeartIcon");
        if (existingHeart != null)
        {
            existingHeart.gameObject.SetActive(false);
        }
    }

    private void ShowHeartAnimation(RectTransform targetRect)
    {
        if (heartAnimationPrefab == null || targetRect == null) return;

        GameObject heart = Instantiate(heartAnimationPrefab, targetRect);
        RectTransform heartRect = heart.GetComponent<RectTransform>();

        // 우측 하단에 배치
        heartRect.anchorMin = new Vector2(1, 0);
        heartRect.anchorMax = new Vector2(1, 0);
        heartRect.pivot = new Vector2(1, 0);
        heartRect.anchoredPosition = new Vector2(-5, 5);

        // 색상 설정
        Image heartImg = heart.GetComponent<Image>();
        if (heartImg != null)
            heartImg.color = heartColor;

        // 애니메이션 후 제거
        StartCoroutine(HeartAnimationCoroutine(heart));
    }

    private IEnumerator HeartAnimationCoroutine(GameObject heart)
    {
        if (heart == null) yield break;

        // 스케일 애니메이션
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one;
        float duration = 0.3f;
        float elapsed = 0f;

        Transform heartTransform = heart.transform;
        heartTransform.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Elastic ease out
            float p = 0.4f;
            heartTransform.localScale = Vector3.Lerp(startScale, endScale,
                Mathf.Pow(2, -10 * t) * Mathf.Sin((t - p / 4) * (2 * Mathf.PI) / p) + 1);
            yield return null;
        }

        heartTransform.localScale = endScale;

        // 잠시 유지
        yield return new WaitForSeconds(1f);

        // 페이드 아웃
        Image img = heart.GetComponent<Image>();
        if (img != null)
        {
            Color startColor = img.color;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
            elapsed = 0f;
            duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                img.color = Color.Lerp(startColor, endColor, elapsed / duration);
                yield return null;
            }
        }

        Destroy(heart);
    }

    #endregion

    #region Test Mode

    private IEnumerator GenerateTestMessages()
    {
        yield return new WaitForSeconds(testMessageDelay);

        Debug.Log("[MessagePanel] Generating test messages...");

        // 테스트 대화 데이터 생성
        if (conversations.Count == 0)
        {
            conversations.Add(new ConversationSummary
            {
                userId = "test_user_1",
                username = "테스트유저1",
                lastMessage = "안녕하세요! 반갑습니다.",
                lastMessageTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                unreadCount = 1
            });

            conversations.Add(new ConversationSummary
            {
                userId = "test_user_2",
                username = "테스트유저2",
                lastMessage = "어제 올린 AR 콘텐츠 정말 멋있었어요!",
                lastMessageTime = DateTime.Now.AddHours(-2).ToString("yyyy-MM-dd HH:mm:ss"),
                unreadCount = 0
            });
        }

        // 알림 표시
        Debug.Log("[MessagePanel] Test messages generated. Open message panel to see.");
    }

    #endregion

    #region Helpers

    private bool CheckLogin()
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[MessagePanel] Login required");
            return false;
        }
        return true;
    }

    private void HideAllPanels()
    {
        if (messagePanel != null) messagePanel.SetActive(false);
        if (chatRoomPanel != null) chatRoomPanel.SetActive(false);
        if (searchResultPanel != null) searchResultPanel.SetActive(false);
    }

    private void ClearContent(Transform content)
    {
        if (content == null) return;

        // 자식 목록을 먼저 수집 (반복 중 수정 방지)
        var children = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in content)
            children.Add(child.gameObject);

        foreach (var child in children)
            Destroy(child);
    }

    private void ScrollToBottom()
    {
        if (chatMessageContent == null) return;

        Canvas.ForceUpdateCanvases();

        ScrollRect scrollRect = chatMessageContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
            scrollRect.normalizedPosition = Vector2.zero;
    }

    /// <summary>
    /// 빈 상태 UI 표시/숨김
    /// </summary>
    private void ShowEmptyState(Transform parent, string message, bool show)
    {
        // 기존 빈 상태 UI 제거
        if (emptyStateObject != null)
        {
            Destroy(emptyStateObject);
            emptyStateObject = null;
        }

        if (!show || parent == null) return;

        // 빈 상태 UI 생성
        emptyStateObject = new GameObject("EmptyState");
        emptyStateObject.transform.SetParent(parent, false);

        RectTransform rect = emptyStateObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0, 100);

        Text emptyText = emptyStateObject.AddComponent<Text>();
        emptyText.text = message;
        emptyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        emptyText.fontSize = 16;
        emptyText.color = new Color(0.5f, 0.5f, 0.55f);
        emptyText.alignment = TextAnchor.MiddleCenter;
        emptyText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    /// <summary>
    /// 채팅방 빈 상태 UI 표시/숨김
    /// </summary>
    private void ShowChatEmptyState(bool show)
    {
        // 기존 빈 상태 UI 제거
        if (chatEmptyStateObject != null)
        {
            Destroy(chatEmptyStateObject);
            chatEmptyStateObject = null;
        }

        if (!show || chatMessageContent == null) return;

        // 다국어 메시지 가져오기
        string message = emptyChatMessage;
        if (LocalizationManager.Instance != null)
        {
            string localizedMsg = LocalizationManager.Instance.GetText("empty_chat_message");
            if (!string.IsNullOrEmpty(localizedMsg) && localizedMsg != "empty_chat_message")
                message = localizedMsg;
        }

        // 빈 상태 UI 생성
        chatEmptyStateObject = new GameObject("ChatEmptyState");
        chatEmptyStateObject.transform.SetParent(chatMessageContent, false);

        RectTransform rect = chatEmptyStateObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, 50);
        rect.sizeDelta = new Vector2(-40, 120);

        Text emptyText = chatEmptyStateObject.AddComponent<Text>();
        emptyText.text = message;
        emptyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        emptyText.fontSize = 24;
        emptyText.color = new Color(0.5f, 0.5f, 0.55f);
        emptyText.alignment = TextAnchor.MiddleCenter;
        emptyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        emptyText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private IEnumerator LoadAvatar(string url, Image targetImage)
    {
        if (string.IsNullOrEmpty(url) || targetImage == null) yield break;

        string fullUrl = url.StartsWith("http") ? url : ApiConfig.MAIN_SERVER + "/" + url;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
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
        if (string.IsNullOrEmpty(dateStr)) return "";
        if (!DateTime.TryParse(dateStr, out DateTime date)) return dateStr;

        TimeSpan diff = DateTime.Now - date;

        if (diff.TotalMinutes < 1) return "방금";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}분 전";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}시간 전";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}일 전";
        return date.ToString("M월 d일");
    }

    private string GetShortTime(string dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return "";
        if (!DateTime.TryParse(dateStr, out DateTime date)) return "";

        bool isAM = date.Hour < 12;
        int hour12 = date.Hour % 12;
        if (hour12 == 0) hour12 = 12;

        return $"{(isAM ? "오전" : "오후")} {hour12}:{date.Minute:D2}";
    }

    #endregion

    #region Polling (새 메시지 확인)

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

    private IEnumerator PollForNewMessages()
    {
        while (true)
        {
            // 포그라운드/백그라운드에 따라 폴링 간격 조절
            float interval = isAppFocused ? pollIntervalForeground : pollIntervalBackground;
            yield return new WaitForSeconds(interval);

            if (LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn)
            {
                yield return FetchUnreadCount();
            }
        }
    }

    #endregion

    #region Unread Count (안읽음 카운트)

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
                totalUnreadCount = response.unread_count;
                UpdateUnreadUI();
            }
        }
    }

    private void UpdateUnreadUI()
    {
        if (globalUnreadIndicator != null)
            globalUnreadIndicator.SetActive(totalUnreadCount > 0);
    }

    #endregion

    #region Text Ellipsis

    /// <summary>
    /// 텍스트가 영역을 넘어가면 "..."으로 잘라서 표시
    /// </summary>
    private void SetTextWithEllipsis(Text textComponent, string fullText)
    {
        if (textComponent == null) return;

        textComponent.text = fullText ?? "";

        if (string.IsNullOrEmpty(fullText)) return;

        StartCoroutine(ApplyEllipsisNextFrame(textComponent, fullText));
    }

    private IEnumerator ApplyEllipsisNextFrame(Text textComponent, string fullText)
    {
        yield return null;

        if (textComponent == null) yield break;

        RectTransform rt = textComponent.rectTransform;
        float availableWidth = rt.rect.width;

        if (availableWidth <= 0) yield break;

        TextGenerator generator = textComponent.cachedTextGenerator;
        TextGenerationSettings settings = textComponent.GetGenerationSettings(rt.rect.size);

        float preferredWidth = generator.GetPreferredWidth(fullText, settings);

        if (preferredWidth <= availableWidth) yield break;

        // 이진 탐색으로 최적 길이 찾기
        string ellipsis = "..";
        int left = 0, right = fullText.Length;

        while (left < right)
        {
            int mid = (left + right + 1) / 2;
            string truncated = fullText.Substring(0, mid) + ellipsis;
            float truncatedWidth = generator.GetPreferredWidth(truncated, settings);

            if (truncatedWidth <= availableWidth)
                left = mid;
            else
                right = mid - 1;
        }

        textComponent.text = left > 0 ? fullText.Substring(0, left) + ellipsis : ellipsis;
    }

    #endregion
}

#region Data Classes

[Serializable]
public class ConversationSummary
{
    public string userId;
    public string username;
    public string avatarUrl;
    public string lastMessage;
    public string lastMessageTime;
    public int unreadCount;
}

[Serializable]
public class AdminBroadcast
{
    public int id;
    public string title;
    public string content;
    public string created_at;
    public string broadcast_type;
}

[Serializable]
public class AdminBroadcastListResponse
{
    public List<AdminBroadcast> broadcasts;
    public int count;
}

[Serializable]
public class SearchedUser
{
    public string id;
    public string username;
    public string avatar_url;
    public bool is_following;
}

[Serializable]
public class UserSearchResponse
{
    public List<SearchedUser> users;
    public int count;
}

[Serializable]
public class LikeRequest
{
    public string user_id;
    public bool set_liked;
}

[Serializable]
public class DMUnreadCountResponse
{
    public int unread_count;
}

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

#endregion
