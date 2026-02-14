using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// SSL 인증서 검증 우회 (개발/테스트 환경용)
/// 프로덕션에서는 서버 인증서가 유효하면 자동으로 통과
/// </summary>
public class BypassCertificateHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}

/// <summary>
/// 메시지 패널 매니저 - 대화 목록, 검색, 스와이프 삭제 등
/// 모든 메시지 (DM, 시스템 알림, 관리자 공지)가 최신순으로 통합 정렬
/// </summary>
public class MessagePanelManager : MonoBehaviour
{
    public static MessagePanelManager Instance { get; private set; }

    [Header("=== 메인 패널 ===")]
    public GameObject messagePanel;
    public Button closeButton;
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
    [Tooltip("시스템 메시지 아바타 스프라이트 (WOOPANG 로고)")]
    public Sprite systemAvatarSprite;
    public InputField chatInput;
    public Button sendButton;
    [Tooltip("시스템 알림에서 숨길 입력 영역 (전체 InputArea GameObject)")]
    public GameObject chatInputArea;
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
    [Tooltip("메시지가 없을 때 표시할 텍스트 (fallback)")]
    public string emptyInboxMessage = "아직 대화가 없습니다.\n근처에 있는 친구를 팔로우해서\n대화를 시작해보세요\u2665";
    public string emptySearchMessage = "검색 결과가 없습니다.";
    [Tooltip("채팅방에 대화가 없을 때 표시할 텍스트")]
    public string emptyChatMessage = "아직 대화가 없습니다.\n첫 메시지를 보내보세요!";

    [Tooltip("빈 상태 메시지 폰트 크기")]
    public int emptyStateFontSize = 42;

    [Tooltip("빈 상태 메시지 텍스트 색상")]
    public Color emptyStateTextColor = new Color(0.45f, 0.45f, 0.5f, 1f);

    [Header("=== 읽음 표시 색상 설정 ===")]
    [Tooltip("읽음 완료 표시 색상 (✓✓)")]
    public Color readIndicatorColorRead = new Color(0.3f, 0.7f, 1f, 1f);

    [Tooltip("전송 완료 표시 색상 (✓)")]
    public Color readIndicatorColorUnread = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Header("=== 하트 아이콘 설정 ===")]
    [Tooltip("하트 아이콘 크기 (픽셀)")]
    public Vector2 heartIconSize = new Vector2(24, 24);

    private GameObject emptyStateObject;
    private GameObject chatEmptyStateObject;


    [Header("=== 안읽음 인디케이터 ===")]
    public GameObject globalUnreadIndicator;
    private int totalUnreadCount = 0;

    [Header("=== 시스템 메시지 설정 ===")]
    [Tooltip("시스템 메시지 배경색 (어두운 색)")]
    public Color systemMessageBgColor = new Color(0.12f, 0.12f, 0.15f, 1f);
    [Tooltip("일반 DM 배경색")]
    public Color normalMessageBgColor = new Color(0.18f, 0.18f, 0.22f, 1f);
    private const string SystemNotificationKey = "SystemNotifications";

    [Header("=== 메시지 폴링 ===")]
    [Tooltip("포그라운드 폴링 간격 (초)")]
    public float pollIntervalForeground = 10f;
    [Tooltip("백그라운드 폴링 간격 (초)")]
    public float pollIntervalBackground = 60f;
    private Coroutine pollingCoroutine;
    private bool isAppFocused = true;

    [Header("=== 폰트 설정 ===")]
    [Tooltip("채팅용 커스텀 폰트 (AppleSDGothicNeoM) - 프리팹에 폰트 없을 때 사용")]
    public Font chatFont;

    [Header("=== 채팅 버블 최대 너비 설정 ===")]
    [Tooltip("화면 너비 대비 최대 버블 너비 비율 (0.5 ~ 1.0)")]
    [Range(0.5f, 1.0f)]
    public float maxBubbleWidthRatio = 0.82f;

    [Tooltip("버블 최대 너비 (픽셀) - 이 값과 화면비율 중 작은 값 적용")]
    public float maxBubbleWidthPixels = 800f;

    [Tooltip("버블 최소 너비 (픽셀)")]
    public float minBubbleWidth = 120f;

    [Tooltip("기본 화면 너비 (에디터에서 Screen.width가 0일 때 사용)")]
    public float defaultScreenWidth = 1080f;

    [Header("=== 대화 목록 설정 ===")]
    [Tooltip("대화 목록 아이템 높이")]
    public float conversationItemHeight = 140f;

    [Tooltip("대화 목록 아이템 간격 (픽셀)")]
    public float conversationListSpacing = 2f;

    // 내부용 상수 (프리팹 값 유지, 너비 계산용)
    private const float DEFAULT_BUBBLE_PADDING = 24f;
    private const float DEFAULT_MIN_TEXT_WIDTH = 50f;
    private const float DEFAULT_BUBBLE_INNER_SPACING = 4f;
    private const bool TIME_INSIDE_BUBBLE = true;
    private const float TIME_INSIDE_MARGIN_RIGHT = 8f;
    private const float TIME_INSIDE_MARGIN_BOTTOM = 4f;
    private const float TIME_AREA_WIDTH = 150f;
    private const float TIME_AREA_MIN_WIDTH = 100f;
    // 날짜 구분선 fallback 기본값 (프리팹 없을 때만 사용)
    private const int DEFAULT_DATE_SEPARATOR_FONT_SIZE = 28;
    private const float DEFAULT_DATE_SEPARATOR_MARGIN = 20f;
    private static readonly Color DEFAULT_DATE_SEPARATOR_COLOR = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Header("=== 날짜 구분선 설정 ===")]
    [Tooltip("날짜 구분선 사용 (24시간 기준)")]
    public bool useDateSeparator = true;

    [Tooltip("날짜 구분선 프리팹 (Text 컴포넌트 포함). 없으면 코드로 동적 생성")]
    public GameObject dateSeparatorPrefab;

    [Tooltip("날짜 구분선 사용 시 버블 내 시간 숨김")]
    public bool hideTimeInBubbleWhenSeparator = true;

    // 마지막 메시지 시간 (날짜 구분선용)
    private DateTime lastMessageTime = DateTime.MinValue;

    // 현재 대화 상대
    private string currentChatUserId;
    private string currentChatUsername;
    private string currentChatAvatarUrl;
    private bool isAdminChat;

    // 대화 목록 캐시
    private List<ConversationSummary> conversations = new List<ConversationSummary>();
    private List<AdminBroadcast> adminBroadcasts = new List<AdminBroadcast>();

    // 스와이프 추적
    private Dictionary<GameObject, Vector2> swipeStartPositions = new Dictionary<GameObject, Vector2>();
    private Dictionary<GameObject, bool> isSwipingItem = new Dictionary<GameObject, bool>();

    // 현재 표시 중인 메시지 버블 목록 (실시간 반영용)
    private List<GameObject> displayedBubbles = new List<GameObject>();

#if UNITY_EDITOR
    /// <summary>
    /// Inspector에서 값 변경 시 실시간 반영
    /// </summary>
    private void OnValidate()
    {
        // Play 모드에서만 실시간 반영
        if (!Application.isPlaying) return;
        if (chatMessageContent == null) return;

        // 기존 버블들의 최대 너비 업데이트
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || chatMessageContent == null) return;
            RefreshBubbleWidths();
        };
    }

    /// <summary>
    /// 모든 버블의 최대 너비 업데이트
    /// </summary>
    private void RefreshBubbleWidths()
    {
        float screenWidth = Screen.width > 0 ? Screen.width : defaultScreenWidth;
        float maxWidth = Mathf.Min(maxBubbleWidthPixels, screenWidth * maxBubbleWidthRatio);
        float maxTextWidth = maxWidth - (DEFAULT_BUBBLE_PADDING * 2);

        foreach (Transform child in chatMessageContent)
        {
            if (child == null) continue;

            // BubbleContainer 찾기
            Transform bubbleContainer = child.Find("BubbleContainer");
            if (bubbleContainer == null) bubbleContainer = child.Find("Bubble");
            if (bubbleContainer == null) continue;

            // ContentText의 LayoutElement 업데이트
            Text contentText = bubbleContainer.GetComponentInChildren<Text>();
            if (contentText == null) continue;

            LayoutElement textLE = contentText.GetComponent<LayoutElement>();
            if (textLE != null)
            {
                float actualTextWidth = contentText.preferredWidth;
                float finalTextWidth = Mathf.Clamp(actualTextWidth, DEFAULT_MIN_TEXT_WIDTH, maxTextWidth);
                textLE.preferredWidth = finalTextWidth;
            }
        }

        // 레이아웃 리빌드
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(chatMessageContent as RectTransform);
    }
#endif

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
        AutoConnectFields();
        SetupButtons();
        HideAllPanels();

        // 시스템 알림 로드
        LoadSystemNotifications();
        UpdateUnreadUI();

        // 입력창 자동 확장 설정
        if (chatInput != null)
            AutoExpandInputField.Setup(chatInput, 50f, 150f);
    }

    /// <summary>
    /// Inspector에서 연결되지 않은 필드 자동 연결
    /// </summary>
    private void AutoConnectFields()
    {
        // AdminNoticeItem 프리팹 자동 로드
        if (adminNoticePrefab == null)
        {
            adminNoticePrefab = Resources.Load<GameObject>("Prefabs/DM/AdminNoticeItem");
            if (adminNoticePrefab == null)
            {
                // Assets/Prefabs/DM 폴더에서도 시도
                var prefabs = Resources.LoadAll<GameObject>("Prefabs");
                foreach (var p in prefabs)
                {
                    if (p.name == "AdminNoticeItem")
                    {
                        adminNoticePrefab = p;
                        break;
                    }
                }
            }
        }

        // AdminMessageBubble 프리팹 자동 로드
        if (adminMessageBubblePrefab == null)
        {
            adminMessageBubblePrefab = Resources.Load<GameObject>("Prefabs/DM/AdminMessageBubble");
            if (adminMessageBubblePrefab == null)
                adminMessageBubblePrefab = otherMessageBubblePrefab; // fallback
        }

        // DateSeparator 프리팹 자동 로드
        if (dateSeparatorPrefab == null)
        {
            dateSeparatorPrefab = Resources.Load<GameObject>("Prefabs/DM/DateSeparator");
        }

        // closeButton 자동 연결
        if (closeButton == null && messagePanel != null)
        {
            // 닫기 버튼 이름 패턴들을 찾아봄
            string[] closeButtonNames = { "CloseButton", "Close_Button", "X_Button", "BackButton", "Back_Button", "닫기" };
            foreach (string buttonName in closeButtonNames)
            {
                Transform found = messagePanel.transform.Find(buttonName);
                if (found != null)
                {
                    closeButton = found.GetComponent<Button>();
                    if (closeButton != null)
                    {
                        break;
                    }
                }
            }

            // 직접 자식에서 못 찾으면 재귀 검색
            if (closeButton == null)
            {
                closeButton = FindButtonInChildren(messagePanel.transform, closeButtonNames);
            }
        }

        // searchResultPanel 자동 연결
        if (searchResultPanel == null)
            searchResultPanel = transform.Find("SearchResultPanel")?.gameObject;

        // searchResultContent 자동 연결
        if (searchResultContent == null)
        {
            if (searchResultPanel != null)
                searchResultContent = searchResultPanel.transform.Find("Content");
        }

        // navigationMessageButton 자동 연결
        if (navigationMessageButton == null)
        {
            Transform navBtn = transform.Find("NavigationMessageButton");
            if (navBtn == null) navBtn = transform.parent?.Find("NavigationMessageButton");
            if (navBtn != null) navigationMessageButton = navBtn.GetComponent<Button>();
        }

        // globalUnreadIndicator 자동 연결 (Message_Button의 자식 UnreadMessageButtonImage)
        if (globalUnreadIndicator == null)
        {
            // Message_Button > UnreadMessageButtonImage 구조 탐색
            GameObject msgBtn = GameObject.Find("Message_Button");
            if (msgBtn != null)
            {
                Transform indicator = msgBtn.transform.Find("UnreadMessageButtonImage");
                if (indicator != null) globalUnreadIndicator = indicator.gameObject;
            }

            // fallback: 기존 이름으로도 검색
            if (globalUnreadIndicator == null)
            {
                Transform indicator = transform.Find("UnreadIndicator");
                if (indicator == null) indicator = transform.parent?.Find("GlobalUnreadIndicator");
                if (indicator != null) globalUnreadIndicator = indicator.gameObject;
            }
        }

        // chatRoomPanel이 null이면 자동으로 찾기
        if (chatRoomPanel == null)
        {
            GameObject chatPanelObj = GameObject.Find("ChatRoomPanel");
            if (chatPanelObj != null)
            {
                chatRoomPanel = chatPanelObj;
            }
        }

        // chatRoomPanel UI 자동 연결
        if (chatRoomPanel != null)
        {
            // chatRoomTitle 자동 연결
            // 실제 씬 구조: ChatRoomPanel > Background > Header > ChatTitle
            // ★ null이 아니어도 chatRoomPanel의 자식이 아니면 재연결 (잘못된 참조 수정)
            bool titleNeedsReconnect = (chatRoomTitle == null);
            if (!titleNeedsReconnect && chatRoomTitle != null)
            {
                titleNeedsReconnect = !chatRoomTitle.transform.IsChildOf(chatRoomPanel.transform);
            }

            if (titleNeedsReconnect)
            {
                Transform titleTr = chatRoomPanel.transform.Find("Background/Header/ChatTitle");
                if (titleTr == null)
                    titleTr = chatRoomPanel.transform.Find("Header/ChatTitle");
                if (titleTr == null)
                    titleTr = chatRoomPanel.transform.Find("Background/Header/TitleText");
                if (titleTr == null)
                    titleTr = FindChildByName(chatRoomPanel.transform, "ChatTitle");

                if (titleTr != null)
                {
                    chatRoomTitle = titleTr.GetComponent<Text>();
                }
            }

            // chatInputArea 자동 연결
            // 실제 씬 구조: ChatRoomPanel > Background > InputArea
            if (chatInputArea == null)
            {
                Transform inputAreaTr = chatRoomPanel.transform.Find("Background/InputArea");
                if (inputAreaTr == null)
                    inputAreaTr = chatRoomPanel.transform.Find("InputArea");
                if (inputAreaTr == null)
                    inputAreaTr = FindChildByName(chatRoomPanel.transform, "InputArea");

                if (inputAreaTr != null)
                {
                    chatInputArea = inputAreaTr.gameObject;
                }
            }
        }
    }

    /// <summary>
    /// 이름으로 자식 Transform 찾기 (재귀)
    /// </summary>
    private Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform found = FindChildByName(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private Button FindButtonInChildren(Transform parent, string[] names)
    {
        foreach (Transform child in parent)
        {
            foreach (string name in names)
            {
                if (child.name.Contains(name) || child.name.ToLower().Contains("close") || child.name.ToLower().Contains("back"))
                {
                    Button btn = child.GetComponent<Button>();
                    if (btn != null)
                    {
                        return btn;
                    }
                }
            }

            // 자식의 자식도 검색
            Button found = FindButtonInChildren(child, names);
            if (found != null) return found;
        }
        return null;
    }

    private void SetupButtons()
    {
        // 메시지 패널 닫기 버튼
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseMessagePanel);

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
        // 로그인 체크 - 로그인 안 되어 있으면 로그인 팝업 표시
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            if (LoginManager.Instance != null)
            {
                LoginManager.Instance.ShowLoginRequirementPopup();
            }
            return;
        }

        HideAllPanels();
        if (messagePanel != null)
        {
            messagePanel.SetActive(true);

            // CloseButton도 명시적으로 활성화
            if (closeButton != null)
            {
                closeButton.gameObject.SetActive(true);
            }
            else
            {
                Transform closeBtnTransform = messagePanel.transform.Find("CloseButton");
                if (closeBtnTransform != null)
                {
                    closeBtnTransform.gameObject.SetActive(true);
                }
            }
        }

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
    /// FCM 푸시 수신 시 대화 목록 새로고침 (외부 호출용)
    /// </summary>
    public void RefreshConversations()
    {
        if (!CheckLogin()) return;

        // 메시지 패널이 열려있으면 대화 목록 새로고침
        if (messagePanel != null && messagePanel.activeInHierarchy)
        {
            StartCoroutine(LoadConversationList());
        }

        // 안읽음 카운트 항상 업데이트
        StartCoroutine(FetchUnreadCount());
    }

    /// <summary>
    /// FCM 푸시로 수신된 DM을 즉시 대화 목록에 추가/업데이트
    /// </summary>
    /// <param name="senderId">발신자 ID</param>
    /// <param name="senderUsername">발신자 이름</param>
    /// <param name="messageContent">메시지 내용</param>
    /// <param name="avatarUrl">발신자 아바타 URL (선택)</param>
    public void AddOrUpdateConversationFromPush(string senderId, string senderUsername, string messageContent, string avatarUrl = null)
    {
        if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(senderUsername))
            return;

        // 자신이 보낸 메시지는 무시
        if (LoginManager.Instance != null && senderId == LoginManager.Instance.CurrentUserId.ToString())
            return;

        // WOOPANG/관리자 메시지 확인 (sender_id=3 또는 username에 woopang 포함)
        bool isWoopangMessage = senderId == "3" ||
                                senderId.ToLower() == "woopang" ||
                                senderUsername.ToLower().Contains("woopang");

        // WOOPANG 메시지는 시스템 알림으로 처리
        if (isWoopangMessage)
        {
            string notificationId = $"woopang_{DateTime.Now.Ticks}";
            AddLocationNotificationFromPush("WOOPANG", messageContent, notificationId);
            return;
        }

        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 기존 대화 찾기
        int existingIndex = -1;
        for (int i = 0; i < conversations.Count; i++)
        {
            if (conversations[i].userId == senderId)
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            // 기존 대화 업데이트
            var existingConv = conversations[existingIndex];
            existingConv.lastMessage = messageContent;
            existingConv.lastMessageTime = currentTime;
            existingConv.unreadCount++;
        }
        else
        {
            // 새 대화 추가
            var newConv = new ConversationSummary
            {
                userId = senderId,
                username = senderUsername,
                avatarUrl = avatarUrl ?? "",
                lastMessage = messageContent,
                lastMessageTime = currentTime,
                unreadCount = 1,
                isSystemMessage = false
            };

            conversations.Add(newConv);
        }

        SortConversationsByTime();
        totalUnreadCount++;
        UpdateUnreadUI();

        // 메시지 패널이 열려있으면 UI 즉시 갱신
        if (messagePanel != null && messagePanel.activeInHierarchy)
        {
            RefreshConversationUI();
        }
    }

    /// <summary>
    /// 대화 목록 UI 갱신 (conversations 리스트 기반)
    /// </summary>
    private void RefreshConversationUI()
    {
        if (conversationListContent == null) return;

        SortConversationsByTime();

        // 모든 기존 아이템 삭제
        ClearContent(conversationListContent);

        // 대화 목록 통합 렌더링 (DM + 시스템 알림 + 관리자 공지, 시간순)
        foreach (var conv in conversations)
        {
            CreateConversationItem(conv);
        }

        // 빈 상태 체크
        bool isEmpty = conversations.Count == 0;
        ShowEmptyState(conversationListContent, GetLocalizedEmptyInboxMessage(), isEmpty);
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

    #region System Notification (시스템 알림 - DM과 통합)

    /// <summary>
    /// FCM 푸시로 수신된 시스템 알림 추가 (DM 목록에 통합, 로그인 없이도 저장)
    /// </summary>
    public void AddLocationNotificationFromPush(string title, string body, string notificationId,
        float latitude = 0f, float longitude = 0f, float radius = 0f, string distance = "")
    {
        if (string.IsNullOrEmpty(notificationId))
        {
            notificationId = $"sys_{DateTime.Now.Ticks}";
        }

        // 중복 체크
        foreach (var existing in conversations)
        {
            if (existing.notificationId == notificationId)
            {
                return;
            }
        }

        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 시스템 메시지로 conversations에 추가
        var systemMessage = new ConversationSummary
        {
            userId = $"system_{notificationId}",
            username = title,
            avatarUrl = "",
            lastMessage = body,
            lastMessageTime = currentTime,
            unreadCount = 1,
            isSystemMessage = true,
            isRead = false,
            notificationId = notificationId,
            latitude = latitude,
            longitude = longitude,
            radius = radius,
            distance = distance
        };

        conversations.Insert(0, systemMessage);
        SortConversationsByTime();
        SaveSystemNotifications();

        // 메시지 패널이 열려있으면 UI 갱신
        if (messagePanel != null && messagePanel.activeInHierarchy)
        {
            RefreshConversationUI();
        }

        // 안읽음 카운트 업데이트
        UpdateUnreadUI();
    }

    /// <summary>
    /// 시스템 알림만 저장 (PlayerPrefs)
    /// </summary>
    private void SaveSystemNotifications()
    {
        try
        {
            var systemMessages = conversations.FindAll(c => c.isSystemMessage);
            var list = new ConversationSummaryList { conversations = systemMessages };
            string json = JsonUtility.ToJson(list);
            PlayerPrefs.SetString(SystemNotificationKey, json);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MessagePanel] 시스템 알림 저장 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 시스템 알림 로드 (PlayerPrefs)
    /// </summary>
    private void LoadSystemNotifications()
    {
        try
        {
            string json = PlayerPrefs.GetString(SystemNotificationKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                var list = JsonUtility.FromJson<ConversationSummaryList>(json);
                if (list != null && list.conversations != null)
                {
                    // 기존 시스템 메시지 제거 후 로드
                    conversations.RemoveAll(c => c.isSystemMessage);
                    conversations.AddRange(list.conversations);
                    SortConversationsByTime();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MessagePanel] 시스템 알림 로드 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 대화 목록을 시간순으로 정렬 (최신순)
    /// </summary>
    private void SortConversationsByTime()
    {
        conversations.Sort((a, b) =>
        {
            DateTime timeA, timeB;
            DateTime.TryParse(a.lastMessageTime, out timeA);
            DateTime.TryParse(b.lastMessageTime, out timeB);
            return timeB.CompareTo(timeA); // 최신순
        });
    }

    /// <summary>
    /// 시스템 알림 안읽음 개수
    /// </summary>
    public int GetUnreadLocationNotificationCount()
    {
        int count = 0;
        foreach (var c in conversations)
        {
            if (c.isSystemMessage && !c.isRead) count++;
        }
        return count;
    }

    /// <summary>
    /// 시스템 메시지 읽음 처리
    /// </summary>
    private void MarkSystemMessageAsRead(ConversationSummary conv)
    {
        if (conv == null || !conv.isSystemMessage || conv.isRead) return;

        conv.isRead = true;
        conv.unreadCount = 0;
        SaveSystemNotifications();
        UpdateUnreadUI();

        // 메시지 패널이 열려있으면 UI 갱신 (배지 숨기기)
        if (messagePanel != null && messagePanel.activeInHierarchy)
        {
            RefreshConversationUI();
        }
    }

    #endregion

    /// <summary>
    /// 특정 사용자와의 대화방 열기
    /// </summary>
    public void OpenChatRoom(string userId, string username, string avatarUrl = null, bool isAdmin = false)
    {
        currentChatUserId = userId;
        currentChatUsername = username;
        currentChatAvatarUrl = avatarUrl;
        isAdminChat = isAdmin;

        // 메시지 패널에서 채팅룸으로 이동 시 메시지 패널 닫기
        if (messagePanel != null && messagePanel.activeSelf)
        {

            messagePanel.SetActive(false);
        }
        else
        {

        }

        if (chatRoomPanel != null)
            chatRoomPanel.SetActive(true);

        // chatRoomTitle 유효성 검증 + 재연결
        ValidateAndReconnectChatRoomTitle();

        if (chatRoomTitle != null)
        {
            chatRoomTitle.text = username;
            chatRoomTitle.color = isAdmin
                ? new Color(1f, 0.84f, 0f, 1f)
                : Color.white;
            Canvas.ForceUpdateCanvases();
        }
        // 채팅방 아바타 - 중앙 캐시 시스템 사용
        if (chatRoomAvatar != null)
        {
            ProfileManager.LoadAvatarWithMaskAsync(userId, avatarUrl, chatRoomAvatar.transform, username);
        }

        // 채팅방 아바타 터치 → 프로필 열기 (Admin이 아닌 경우만)
        if (chatRoomAvatar != null && !isAdmin)
        {
            // AvatarMask(부모) 또는 chatRoomAvatar 자체에 핸들러 추가
            GameObject targetObj = chatRoomAvatar.transform.parent != null
                ? chatRoomAvatar.transform.parent.gameObject
                : chatRoomAvatar.gameObject;

            // 부모에 Image 없으면 투명 Image 추가 (IPointerClickHandler에 필요)
            Image parentImg = targetObj.GetComponent<Image>();
            if (parentImg == null)
            {
                parentImg = targetObj.AddComponent<Image>();
                parentImg.color = new Color(0, 0, 0, 0);
            }
            parentImg.raycastTarget = true;

            AvatarTapHandler avatarHandler = targetObj.GetComponent<AvatarTapHandler>();
            if (avatarHandler == null)
                avatarHandler = targetObj.AddComponent<AvatarTapHandler>();

            avatarHandler.Initialize(userId, username, OpenProfileFromChatRoom);
        }

        // Admin/시스템 메시지인 경우 입력창 비활성화
        SetChatInputEnabled(!isAdmin);

        StartCoroutine(LoadChatMessages(userId, isAdmin));

        // 읽음 처리
        if (!isAdmin)
        {
            // 로컬 unreadCount 즉시 0으로 설정 (UI 즉시 반영)
            var conv = conversations.Find(c => c.userId == userId);
            if (conv != null && conv.unreadCount > 0)
            {
                totalUnreadCount = Mathf.Max(0, totalUnreadCount - conv.unreadCount);
                conv.unreadCount = 0;
                UpdateUnreadUI();
            }

            // 서버에도 읽음 처리 요청
            StartCoroutine(MarkMessagesAsRead(userId));
        }
    }

    /// <summary>
    /// 시스템 알림 채팅방 열기 (FCM으로 받은 시스템 메시지)
    /// </summary>
    /// <param name="notificationId">알림 ID</param>
    /// <param name="messageContent">메시지 내용</param>
    public void OpenSystemChatRoom(string notificationId, string messageContent)
    {
        currentChatUserId = "woopang";
        currentChatUsername = "WOOPANG";
        currentChatAvatarUrl = null;
        isAdminChat = true;
        currentSystemNotificationId = notificationId;
        currentSystemMessageContent = messageContent;

        // 메시지 패널에서 채팅룸으로 이동
        if (messagePanel != null && messagePanel.activeSelf)
        {

            messagePanel.SetActive(false);
        }
        else
        {

        }

        // chatRoomPanel이 null이면 자동으로 찾기
        if (chatRoomPanel == null)
        {
            GameObject chatPanelObj = GameObject.Find("ChatRoomPanel");
            if (chatPanelObj != null)
            {
                chatRoomPanel = chatPanelObj;
            }
        }

        if (chatRoomPanel != null)
        {
            chatRoomPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[MessagePanel] chatRoomPanel이 null! ChatRoomPanel 오브젝트가 씬에 있는지 확인 필요");
            return;
        }

        // chatRoomTitle 유효성 검증 + 재연결
        ValidateAndReconnectChatRoomTitle();

        if (chatRoomTitle != null)
        {
            chatRoomTitle.text = "WOOPANG";
            chatRoomTitle.color = new Color(1f, 0.84f, 0f, 1f); // 골드색
        }
        // 입력창 비활성화 (읽기 전용)
        SetChatInputEnabled(false);

        // 시스템 메시지 표시
        StartCoroutine(LoadSystemChatMessages(notificationId, messageContent));
    }

    // 현재 시스템 메시지 정보 저장
    private string currentSystemNotificationId;
    private string currentSystemMessageContent;

    /// <summary>
    /// chatRoomTitle이 chatRoomPanel의 자식인지 검증하고, 아니면 재연결
    /// </summary>
    private void ValidateAndReconnectChatRoomTitle()
    {
        if (chatRoomPanel == null) return;

        bool needsReconnect = (chatRoomTitle == null);
        if (!needsReconnect && chatRoomTitle != null)
        {
            needsReconnect = !chatRoomTitle.transform.IsChildOf(chatRoomPanel.transform);
        }

        if (needsReconnect)
        {
            Transform titleTr = chatRoomPanel.transform.Find("Background/Header/ChatTitle");
            if (titleTr == null) titleTr = chatRoomPanel.transform.Find("Header/ChatTitle");
            if (titleTr == null) titleTr = chatRoomPanel.transform.Find("Background/Header/TitleText");
            if (titleTr == null) titleTr = FindChildByName(chatRoomPanel.transform, "ChatTitle");
            if (titleTr != null)
            {
                chatRoomTitle = titleTr.GetComponent<Text>();
            }
        }
    }

    /// <summary>
    /// 채팅 입력창 활성화/비활성화
    /// Admin/WOOPANG: InputArea 표시 + 입력 비활성화 + placeholder 안내
    /// 일반 DM: InputArea 표시 + 입력 활성화 + "메시지 입력" placeholder
    /// </summary>
    private void SetChatInputEnabled(bool enabled)
    {
        // InputArea는 항상 표시 (레이아웃 유지, 비활성화만 처리)
        if (chatInputArea != null)
        {
            chatInputArea.SetActive(true);
        }

        if (chatInput != null)
        {
            chatInput.interactable = enabled;
            if (!enabled) chatInput.text = "";

            // placeholder 설정 (다국어 지원)
            if (chatInput.placeholder != null)
            {
                Text placeholderText = chatInput.placeholder.GetComponent<Text>();
                if (placeholderText != null)
                {
                    if (enabled)
                    {
                        placeholderText.text = LocalizationManager.Instance != null
                            ? LocalizationManager.Instance.GetText("message_placeholder")
                            : "메시지 입력";
                    }
                    else
                    {
                        placeholderText.text = LocalizationManager.Instance != null
                            ? LocalizationManager.Instance.GetText("chat_readonly_placeholder")
                            : "대화를 할 수 없는 채팅방입니다";
                    }
                }
            }
        }

        if (sendButton != null)
        {
            sendButton.interactable = enabled;
        }
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

        // 입력창 다시 활성화 (다음 대화를 위해)
        SetChatInputEnabled(true);

        currentChatUserId = null;
        currentChatUsername = null;
        currentChatAvatarUrl = null;
        isAdminChat = false;
        profileOpenedFromChatRoom = false;

        // Message_Button 클릭 트리거 (모든 관련 로직 실행)
        if (navigationMessageButton != null)
        {
            navigationMessageButton.onClick.Invoke();
        }
        else
        {
            // 폴백: 직접 메시지 패널 열기
            OpenMessagePanel();
        }
    }

    // ============================================================
    // 아바타 탭 → 프로필 열기 & 패널 전환
    // ============================================================

    private bool profileOpenedFromChatRoom = false;
    private string savedChatUserId;
    private string savedChatUsername;

    /// <summary>
    /// 채팅방에서 아바타 탭 시 프로필 열기
    /// ChatRoomPanel을 숨기고 FullProfilePanel을 표시
    /// </summary>
    public void OpenProfileFromChatRoom(string userId, string username)
    {
        if (ProfileManager.Instance == null) return;

        // 현재 채팅방 정보 저장 (나중에 돌아오기 위해)
        savedChatUserId = currentChatUserId;
        savedChatUsername = currentChatUsername;
        profileOpenedFromChatRoom = true;

        // ChatRoomPanel 숨기기
        if (chatRoomPanel != null)
            chatRoomPanel.SetActive(false);

        // API로 프로필 로드 시도 → 실패 시 더미 프로필 표시
        StartCoroutine(OpenProfileWithFallback(userId, username));
    }

    /// <summary>
    /// 채팅방에서 프로필 열기 (API 호출)
    /// </summary>
    private IEnumerator OpenProfileWithFallback(string userId, string username)
    {
        yield return null;

        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.ShowProfile(userId);
            ProfileManager.Instance.SetOnCloseCallback(OnProfileClosedReturnToChatRoom);
        }
    }

    /// <summary>
    /// 프로필 패널이 닫힐 때 채팅방으로 돌아가기
    /// </summary>
    private void OnProfileClosedReturnToChatRoom()
    {
        if (!profileOpenedFromChatRoom) return;

        profileOpenedFromChatRoom = false;

        // ChatRoomPanel 다시 표시
        if (chatRoomPanel != null && !string.IsNullOrEmpty(savedChatUserId))
        {
            chatRoomPanel.SetActive(true);
        }

        savedChatUserId = null;
        savedChatUsername = null;
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

        // 1. 관리자 공지 로드
        yield return StartCoroutine(LoadAdminBroadcasts());

        // 관리자 공지를 conversations에 통합 (기존 항목 제거 후 재추가)
        MergeAdminBroadcastsIntoConversations();

        // 2. 로그인 안 된 경우: 기존 대화 시간순 렌더링 후 종료
        if (!CheckLogin())
        {
            SortConversationsByTime();
            foreach (var conv in conversations)
            {
                CreateConversationItem(conv);
            }

            bool isEmpty = conversations.Count == 0;
            ShowEmptyState(conversationListContent, GetLocalizedEmptyInboxMessage(), isEmpty);
            ForceLayoutUpdate();
            yield break;
        }

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.DM_CONVERSATIONS}?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.certificateHandler = new BypassCertificateHandler();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DMConversationsResponse>(request.downloadHandler.text);

                // 서버 응답에 있는 userId 목록 수집
                var serverUserIds = new HashSet<string>();
                if (response.conversations != null)
                {
                    foreach (var conv in response.conversations)
                    {
                        serverUserIds.Add(conv.partner_id);
                    }
                }

                // 서버에 있는 DM만 제거 (로컬 전용 DM, 시스템 알림, 관리자 공지는 유지)
                conversations.RemoveAll(c => !c.isSystemMessage && !c.isAdminBroadcast && serverUserIds.Contains(c.userId));

                // 서버 대화 추가
                if (response.conversations != null)
                {
                    foreach (var conv in response.conversations)
                    {
                        var summary = new ConversationSummary
                        {
                            userId = conv.partner_id,
                            username = conv.partner_username,
                            avatarUrl = conv.partner_avatar_url,
                            lastMessage = conv.last_message,
                            lastMessageTime = conv.last_message_time,
                            unreadCount = conv.unread_count
                        };

                        conversations.Add(summary);
                    }
                }

                // 전체 안 읽음 수 업데이트
                totalUnreadCount = response.total_unread;
                UpdateUnreadUI();
            }

            // 시간순 정렬 후 모든 대화 (DM + 시스템 알림 + 관리자 공지) 통합 렌더링
            SortConversationsByTime();
            foreach (var conv in conversations)
            {
                CreateConversationItem(conv);
            }

            // 전체 콘텐츠가 없을 때만 빈 상태 표시
            bool isEmpty = conversations.Count == 0;
            ShowEmptyState(conversationListContent, GetLocalizedEmptyInboxMessage(), isEmpty);
        }

        // 레이아웃 강제 업데이트
        ForceLayoutUpdate();
    }

    /// <summary>
    /// 관리자 공지(AdminBroadcast)를 ConversationSummary로 변환하여 conversations 리스트에 통합
    /// </summary>
    private void MergeAdminBroadcastsIntoConversations()
    {
        // 기존 관리자 공지 항목 제거
        conversations.RemoveAll(c => c.isAdminBroadcast);

        // 새로운 관리자 공지를 ConversationSummary로 변환하여 추가
        foreach (var broadcast in adminBroadcasts)
        {
            var summary = new ConversationSummary
            {
                userId = "woopang",
                username = "WOOPANG",
                avatarUrl = "",
                lastMessage = broadcast.content,
                lastMessageTime = broadcast.created_at,
                unreadCount = 0,
                isAdminBroadcast = true,
                notificationId = $"broadcast_{broadcast.id}"
            };
            conversations.Add(summary);
        }
    }

    /// <summary>
    /// 레이아웃 강제 업데이트 - 동적으로 추가된 아이템이 즉시 표시되도록
    /// </summary>
    private void ForceLayoutUpdate()
    {
        if (conversationListContent == null) return;

        // VerticalLayoutGroup 체크
        VerticalLayoutGroup vlg = conversationListContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = conversationListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = conversationListSpacing; // Inspector에서 조절 가능
        }

        // ContentSizeFitter 체크
        ContentSizeFitter csf = conversationListContent.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = conversationListContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // 강제 레이아웃 리빌드
        Canvas.ForceUpdateCanvases();
        RectTransform contentRectTransform = conversationListContent as RectTransform;
        if (contentRectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRectTransform);
        }
    }

    private IEnumerator LoadAdminBroadcasts()
    {
        string url = $"{ApiConfig.MAIN_SERVER}/api/broadcast/list?limit=5";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.certificateHandler = new BypassCertificateHandler();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<AdminBroadcastListResponse>(request.downloadHandler.text);
                adminBroadcasts = response.broadcasts ?? new List<AdminBroadcast>();
            }
            else
            {
                adminBroadcasts = new List<AdminBroadcast>();
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

    /// <summary>
    /// 관리자 공지 아이템 설정 (ConversationSummary 기반, 통합 정렬용)
    /// </summary>
    private void SetupAdminBroadcastItem(GameObject item, ConversationSummary conv)
    {
        Transform content = item.transform.Find("Content");
        Transform searchRoot = content != null ? content : item.transform;

        Text titleText = searchRoot.Find("TitleText")?.GetComponent<Text>();
        if (titleText != null)
            titleText.text = "WOOPANG";

        Text previewText = searchRoot.Find("PreviewText")?.GetComponent<Text>();
        if (previewText != null)
        {
            string preview = conv.lastMessage;
            if (!string.IsNullOrEmpty(preview) && preview.Length > 30)
                preview = preview.Substring(0, 30) + "...";
            previewText.text = preview ?? "";
        }

        Text timeText = searchRoot.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
            timeText.text = GetRelativeTime(conv.lastMessageTime);

        Transform clickTarget = content != null ? content : item.transform;
        Button btn = clickTarget.GetComponent<Button>();
        if (btn == null)
            btn = clickTarget.gameObject.AddComponent<Button>();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            OpenChatRoom("woopang", "WOOPANG", null, true);
        });
    }

    private void CreateConversationItem(ConversationSummary conv)
    {
        if (conversationListContent == null) return;

        // 관리자 공지 (AdminBroadcast)
        if (conv.isAdminBroadcast)
        {
            if (adminNoticePrefab != null)
            {
                GameObject item = Instantiate(adminNoticePrefab, conversationListContent);
                SetLayerRecursively(item, 5);
                SetupAdminBroadcastItem(item, conv);
                return;
            }
        }

        // 시스템 메시지는 AdminNoticeItem 프리팹 사용
        if (conv.isSystemMessage)
        {
            if (adminNoticePrefab != null)
            {
                GameObject item = Instantiate(adminNoticePrefab, conversationListContent);
                SetLayerRecursively(item, 5);
                SetupSystemNoticeItem(item, conv);
                SetupSwipeDeleteForSystemMessage(item, conv.notificationId);
                return;
            }
            else if (conversationItemPrefab != null)
            {
                GameObject item = Instantiate(conversationItemPrefab, conversationListContent);
                SetLayerRecursively(item, 5);
                conv.username = "WOOPANG";
                SetupConversationItem(item, conv);
                SetupSwipeDeleteForSystemMessage(item, conv.notificationId);
                return;
            }
        }

        // 일반 DM은 ConversationItem 프리팹 사용
        if (conversationItemPrefab == null) return;

        GameObject dmItem = Instantiate(conversationItemPrefab, conversationListContent);
        SetLayerRecursively(dmItem, 5);
        SetupConversationItem(dmItem, conv);
        SetupSwipeDelete(dmItem, conv.userId);
    }

    /// <summary>
    /// 시스템 알림 아이템 설정 (AdminNoticeItem 프리팹 사용)
    /// </summary>
    private void SetupSystemNoticeItem(GameObject item, ConversationSummary conv)
    {
        // Content 하위에서 찾기 (ConversationItem과 동일 구조)
        Transform content = item.transform.Find("Content");
        Transform searchRoot = content != null ? content : item.transform;

        // 제목 - 항상 "WOOPANG"
        Text titleText = searchRoot.Find("TitleText")?.GetComponent<Text>();
        if (titleText != null)
            titleText.text = "WOOPANG";

        // 미리보기
        Text previewText = searchRoot.Find("PreviewText")?.GetComponent<Text>();
        if (previewText != null)
        {
            string preview = conv.lastMessage;
            if (!string.IsNullOrEmpty(preview) && preview.Length > 30)
                preview = preview.Substring(0, 30) + "...";
            previewText.text = preview ?? "";
        }

        // 시간
        Text timeText = searchRoot.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
            timeText.text = GetRelativeTime(conv.lastMessageTime);

        // 안읽음 배지
        bool hasUnread = !conv.isRead && conv.unreadCount > 0;
        GameObject unreadBadge = searchRoot.Find("UnreadBadge")?.gameObject;

        if (unreadBadge != null)
        {
            unreadBadge.SetActive(hasUnread);

            if (hasUnread)
            {
                Text countText = unreadBadge.GetComponentInChildren<Text>();
                if (countText != null)
                    countText.text = conv.unreadCount.ToString();
            }
        }

        // 클릭 이벤트 - Content에 연결 (ConversationItem과 동일)
        Transform clickTarget = content != null ? content : item.transform;
        Button btn = clickTarget.GetComponent<Button>();
        if (btn == null)
            btn = clickTarget.gameObject.AddComponent<Button>();

        btn.onClick.RemoveAllListeners();
        string notificationId = conv.notificationId;
        string messageContent = conv.lastMessage;
        btn.onClick.AddListener(() =>
        {
            MarkSystemMessageAsRead(conv);
            OpenSystemChatRoom(notificationId, messageContent);
        });
    }

    private void SetupConversationItem(GameObject item, ConversationSummary conv)
    {
        // CanvasGroup 확인 (alpha가 0이면 보이지 않음)
        CanvasGroup cg = item.GetComponent<CanvasGroup>();
        if (cg != null && cg.alpha < 1f)
        {
            cg.alpha = 1f;
        }

        // 아이템이 활성화 상태인지 확인
        if (!item.activeSelf)
        {
            item.SetActive(true);
        }

        // 아이템 높이 설정 - 프리팹에 LayoutElement가 있으면 그 값 존중
        LayoutElement itemLE = item.GetComponent<LayoutElement>();
        if (itemLE == null)
        {
            itemLE = item.AddComponent<LayoutElement>();
            itemLE.minHeight = conversationItemHeight;
            itemLE.preferredHeight = conversationItemHeight;
        }

        // 프리팹의 Image color를 그대로 사용 (하드코딩 제거)

        // 시스템 메시지 읽음 처리 (클릭 시)
        if (conv.isSystemMessage && !conv.isRead)
        {
            Button itemBtn = item.GetComponent<Button>();
            if (itemBtn == null)
                itemBtn = item.AddComponent<Button>();

            itemBtn.onClick.AddListener(() => MarkSystemMessageAsRead(conv));
        }

        // 사용자명 (Content 아래에 있음)
        Text usernameText = item.transform.Find("Content/UsernameText")?.GetComponent<Text>();
        if (usernameText != null)
        {
            usernameText.text = conv.username ?? "Unknown";
        }

        // 미리보기 - 영역 크기에 맞게 자동 ellipsis 처리 (Content 아래에 있음)
        Text previewText = item.transform.Find("Content/PreviewText")?.GetComponent<Text>();
        if (previewText != null)
        {
            SetTextWithEllipsis(previewText, conv.lastMessage);
        }

        // 시간 (Content 아래에 있음)
        Text timeText = item.transform.Find("Content/TimeText")?.GetComponent<Text>();
        if (timeText != null)
        {
            timeText.text = GetRelativeTime(conv.lastMessageTime);
        }

        // 안 읽음 표시 (Content 아래에 있음)
        GameObject unreadBadge = item.transform.Find("Content/UnreadBadge")?.gameObject;
        Text unreadText = item.transform.Find("Content/UnreadBadge/UnreadCount")?.GetComponent<Text>();
        if (unreadBadge != null)
        {
            unreadBadge.SetActive(conv.unreadCount > 0);

            if (unreadText != null)
                unreadText.text = conv.unreadCount.ToString();
        }

        // 아바타 (Content 아래에 있음) - 중앙 캐시 시스템 사용
        Transform avatarTransform = item.transform.Find("Content/Avatar");
        if (avatarTransform != null)
        {
            ProfileManager.LoadAvatarWithMaskAsync(conv.userId, conv.avatarUrl, avatarTransform, conv.username);
        }

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
                // 시스템 메시지면 isAdmin=true로 전달 (타이틀 색상 및 입력창 처리)
                OpenChatRoom(conv.userId, conv.username, conv.avatarUrl, conv.isSystemMessage);
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

    /// <summary>
    /// 시스템 알림 전용 스와이프 삭제 설정 (로컬 삭제)
    /// </summary>
    private void SetupSwipeDeleteForSystemMessage(GameObject item, string notificationId)
    {
        SwipeToDeleteHandler handler = item.GetComponent<SwipeToDeleteHandler>();
        if (handler == null)
            handler = item.AddComponent<SwipeToDeleteHandler>();

        handler.Initialize(notificationId ?? "", swipeThreshold, () =>
        {
            DeleteSystemNotification(notificationId, item);
        });
    }

    /// <summary>
    /// 시스템 알림 로컬 삭제 (PlayerPrefs에서 제거)
    /// </summary>
    private void DeleteSystemNotification(string notificationId, GameObject item)
    {
        // conversations 리스트에서 해당 시스템 메시지 제거
        int removed = conversations.RemoveAll(c =>
            c.isSystemMessage && c.notificationId == notificationId);

        if (removed > 0)
        {
            // PlayerPrefs 업데이트
            SaveSystemNotifications();
        }

        // UI에서 제거
        if (item != null)
            Destroy(item);

        // 안읽음 카운트 업데이트
        UpdateUnreadUI();

        // 햅틱 피드백
        if (UIFeedbackManager.Instance != null)
            UIFeedbackManager.Instance.TriggerMediumHaptic();
    }

    private IEnumerator DeleteConversationCoroutine(string otherUserId)
    {
        if (!CheckLogin()) yield break;

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.MAIN_SERVER}/api/dm/conversation?user_id={userId}&other_id={otherUserId}";

        using (UnityWebRequest request = UnityWebRequest.Delete(url))
        {
            request.certificateHandler = new BypassCertificateHandler();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
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

    /// <summary>
    /// 시스템 알림 메시지 로드 (conversations에서 해당 시스템 메시지 찾아서 표시)
    /// </summary>
    private IEnumerator LoadSystemChatMessages(string notificationId, string messageContent)
    {
        ClearContent(chatMessageContent);
        ShowChatEmptyState(false);

        // 날짜 구분선 추적 초기화
        ResetDateSeparatorTracking();

        // conversations에서 해당 시스템 메시지 찾기
        ConversationSummary systemMsg = null;
        foreach (var conv in conversations)
        {
            if (conv.isSystemMessage && conv.notificationId == notificationId)
            {
                systemMsg = conv;
                break;
            }
        }

        // 메시지 버블 생성 (AdminNoticeItem 스타일)
        if (systemMsg != null || !string.IsNullOrEmpty(messageContent))
        {
            string content = systemMsg != null ? systemMsg.lastMessage : messageContent;
            string time = systemMsg != null ? systemMsg.lastMessageTime : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 날짜 구분선 생성 (대화 메시지 위 가운데에 표시)
            CheckAndCreateDateSeparator(time);

            CreateSystemMessageBubble("WOOPANG", content, time);
        }
        else
        {
            ShowChatEmptyState(true);
        }

        yield return null;
        ScrollToBottom();
    }

    /// <summary>
    /// 날짜 구분선 생성 (24시간 이상 차이날 때 표시)
    /// 형식: "11월 3일 오후 7:41"
    /// </summary>
    private void CreateDateSeparator(DateTime messageTime)
    {
        if (chatMessageContent == null || !useDateSeparator) return;

        string dateStr = FormatDateSeparator(messageTime);

        // 프리팹이 있으면 프리팹 사용 (프리팹에서 스타일 수정 가능)
        if (dateSeparatorPrefab != null)
        {
            GameObject separator = Instantiate(dateSeparatorPrefab, chatMessageContent);
            SetLayerRecursively(separator, 5);
            separator.name = "DateSeparator";

            // 프리팹 내 Text 컴포넌트 찾아서 텍스트만 설정
            Text dateText = separator.GetComponentInChildren<Text>();
            if (dateText != null)
                dateText.text = dateStr;

            return;
        }

        // Fallback: 프리팹 없을 때 동적 생성
        GameObject dynamicSeparator = new GameObject("DateSeparator");
        dynamicSeparator.transform.SetParent(chatMessageContent, false);
        SetLayerRecursively(dynamicSeparator, 5);

        RectTransform separatorRect = dynamicSeparator.AddComponent<RectTransform>();
        separatorRect.anchorMin = new Vector2(0, 1);
        separatorRect.anchorMax = new Vector2(1, 1);
        separatorRect.pivot = new Vector2(0.5f, 1);

        LayoutElement separatorLE = dynamicSeparator.AddComponent<LayoutElement>();
        separatorLE.flexibleWidth = 1;
        separatorLE.preferredHeight = DEFAULT_DATE_SEPARATOR_FONT_SIZE + (DEFAULT_DATE_SEPARATOR_MARGIN * 2);

        GameObject textObj = new GameObject("DateText");
        textObj.transform.SetParent(dynamicSeparator.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text fallbackText = textObj.AddComponent<Text>();
        fallbackText.text = dateStr;
        fallbackText.fontSize = DEFAULT_DATE_SEPARATOR_FONT_SIZE;
        fallbackText.color = DEFAULT_DATE_SEPARATOR_COLOR;
        fallbackText.alignment = TextAnchor.MiddleCenter;
        if (chatFont != null)
            fallbackText.font = chatFont;
    }

    /// <summary>
    /// 날짜 구분선 형식 지정 - 디바이스 언어에 따라 다국어 지원
    /// 한국어: "11월 3일 오후 7:41"
    /// 영어: "Nov 3, 7:41 PM"
    /// 일본어: "11月3日 午後7:41"
    /// 중국어: "11月3日 下午7:41"
    /// 스페인어: "3 nov. 19:41"
    /// </summary>
    private string FormatDateSeparator(DateTime dateTime)
    {
        string langCode = Application.systemLanguage.ToString();

        int hour12 = dateTime.Hour % 12;
        if (hour12 == 0) hour12 = 12;
        bool isPM = dateTime.Hour >= 12;

        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return $"{dateTime.Month}월 {dateTime.Day}일 {(isPM ? "오후" : "오전")} {hour12}:{dateTime.Minute:D2}";

            case SystemLanguage.Japanese:
                return $"{dateTime.Month}月{dateTime.Day}日 {(isPM ? "午後" : "午前")}{hour12}:{dateTime.Minute:D2}";

            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return $"{dateTime.Month}月{dateTime.Day}日 {(isPM ? "下午" : "上午")}{hour12}:{dateTime.Minute:D2}";

            case SystemLanguage.Spanish:
                string[] monthsEs = { "", "ene.", "feb.", "mar.", "abr.", "may.", "jun.", "jul.", "ago.", "sep.", "oct.", "nov.", "dic." };
                return $"{dateTime.Day} {monthsEs[dateTime.Month]} {dateTime.Hour:D2}:{dateTime.Minute:D2}";

            default: // English and others
                string[] monthsEn = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                return $"{monthsEn[dateTime.Month]} {dateTime.Day}, {hour12}:{dateTime.Minute:D2} {(isPM ? "PM" : "AM")}";
        }
    }

    /// <summary>
    /// 24시간 이상 차이나는지 확인하고 필요시 날짜 구분선 생성
    /// </summary>
    private void CheckAndCreateDateSeparator(string timeStr)
    {
        if (!useDateSeparator) return;

        DateTime messageTime;
        if (!DateTime.TryParse(timeStr, out messageTime))
            messageTime = DateTime.Now;

        // 24시간 이상 차이나면 날짜 구분선 생성
        if (lastMessageTime == DateTime.MinValue ||
            (messageTime - lastMessageTime).TotalHours >= 24)
        {
            CreateDateSeparator(messageTime);
        }

        lastMessageTime = messageTime;
    }

    /// <summary>
    /// 채팅방 열 때 마지막 메시지 시간 초기화
    /// </summary>
    private void ResetDateSeparatorTracking()
    {
        lastMessageTime = DateTime.MinValue;
    }

    /// <summary>
    /// 시스템 메시지 버블 생성 (채팅방 내에서 AdminMessageBubble 프리팹 사용)
    /// </summary>
    private void CreateSystemMessageBubble(string senderName, string content, string time)
    {
        if (chatMessageContent == null) return;

        // adminMessageBubblePrefab 사용
        GameObject prefab = adminMessageBubblePrefab ?? otherMessageBubblePrefab;
        if (prefab == null) return;

        GameObject bubble = Instantiate(prefab, chatMessageContent);
        SetLayerRecursively(bubble, 5);

        // === 1. TimeArea 완전히 제거 (DateSeparator가 날짜/시간 표시하므로 불필요) ===
        Transform timeAreaTr = bubble.transform.Find("TimeArea");
        if (timeAreaTr != null)
        {
            timeAreaTr.gameObject.SetActive(false);
        }

        // === 2. 아바타 설정 (WOOPANG 로고) - 먼저 처리 ===
        Transform avatarTr = bubble.transform.Find("AvatarContainer");
        if (avatarTr == null)
            avatarTr = bubble.transform.Find("Avatar");
        if (avatarTr != null)
        {
            avatarTr.gameObject.SetActive(true);

            // AvatarContainer에 직접 이미지 설정 (간단한 구조)
            Image avatarImage = avatarTr.GetComponent<Image>();
            if (avatarImage == null)
                avatarImage = avatarTr.gameObject.AddComponent<Image>();

            // 프리팹에 스프라이트가 이미 설정되어 있으면 유지
            if (avatarImage.sprite == null)
            {
                // WOOPANG 로고 설정 (Inspector에서 설정 우선)
                Sprite logoSprite = systemAvatarSprite;

                // Inspector에 없으면 Resources에서 로드 시도
                if (logoSprite == null)
                    logoSprite = Resources.Load<Sprite>("Textures/woopang_logo");
                if (logoSprite == null)
                    logoSprite = Resources.Load<Sprite>("UI/woopang_logo");
                if (logoSprite == null)
                    logoSprite = Resources.Load<Sprite>("woopang_logo");

                if (logoSprite != null)
                {
                    avatarImage.sprite = logoSprite;
                    avatarImage.preserveAspect = true;
                    avatarImage.type = Image.Type.Simple;
                    avatarImage.color = Color.white;
                }
            }

            // 시스템 메시지 아바타: 프로필 열기 비활성화 (WOOPANG 시스템이므로)
            AvatarTapHandler avatarHandler = avatarTr.GetComponent<AvatarTapHandler>();
            if (avatarHandler != null)
                Destroy(avatarHandler);
        }

        // === 3. LabelText 비활성화 (ChatTitle로 대체됨) ===
        Transform labelText = bubble.transform.Find("BubbleContainer/LabelText");
        if (labelText != null)
            labelText.gameObject.SetActive(false);

        // === 4. BubbleContainer 레이아웃 설정 ===
        Transform bubbleContainerTr = bubble.transform.Find("BubbleContainer");
        if (bubbleContainerTr != null)
        {
            // 기존 LayoutElement의 고정값 초기화
            LayoutElement bubbleLE = bubbleContainerTr.GetComponent<LayoutElement>();
            if (bubbleLE != null)
            {
                bubbleLE.preferredWidth = -1;
                bubbleLE.minWidth = -1;
                bubbleLE.flexibleWidth = 0;
            }

            // VerticalLayoutGroup 설정 (프리팹에 없으면 추가, 있으면 프리팹 값 유지)
            VerticalLayoutGroup bubbleVLG = bubbleContainerTr.GetComponent<VerticalLayoutGroup>();
            if (bubbleVLG == null)
            {
                bubbleVLG = bubbleContainerTr.gameObject.AddComponent<VerticalLayoutGroup>();
                bubbleVLG.padding = new RectOffset((int)DEFAULT_BUBBLE_PADDING, (int)DEFAULT_BUBBLE_PADDING, (int)DEFAULT_BUBBLE_PADDING, (int)DEFAULT_BUBBLE_PADDING);
                bubbleVLG.spacing = DEFAULT_BUBBLE_INNER_SPACING;
                bubbleVLG.childAlignment = TextAnchor.UpperLeft;
            }
            // childControl은 레이아웃 동작에 필수이므로 항상 설정
            bubbleVLG.childControlWidth = true;
            bubbleVLG.childControlHeight = true;
            bubbleVLG.childForceExpandWidth = false;
            bubbleVLG.childForceExpandHeight = false;

            // ContentSizeFitter - 가로/세로 모두 컨텐츠에 맞춤 (프리팹에 없으면 추가)
            ContentSizeFitter bubbleCSF = bubbleContainerTr.GetComponent<ContentSizeFitter>();
            if (bubbleCSF == null)
            {
                bubbleCSF = bubbleContainerTr.gameObject.AddComponent<ContentSizeFitter>();
                bubbleCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                bubbleCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        // === 5. 메시지 내용 설정 ===
        Text contentText = bubble.transform.Find("BubbleContainer/ContentText")?.GetComponent<Text>();
        if (contentText == null)
            contentText = bubble.GetComponentInChildren<Text>();
        if (contentText != null)
        {
            // 텍스트 설정 (폰트 크기/색상은 프리팹 값 유지)
            contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contentText.verticalOverflow = VerticalWrapMode.Overflow;
            contentText.text = content;
            // 폰트만 설정 (프리팹에 없을 경우)
            if (chatFont != null && contentText.font == null)
                contentText.font = chatFont;

            // 강제 캔버스 업데이트
            Canvas.ForceUpdateCanvases();

            // 버블 최대/최소 너비 계산
            float screenWidth = Screen.width > 0 ? Screen.width : defaultScreenWidth;
            float maxWidth = Mathf.Min(maxBubbleWidthPixels, screenWidth * maxBubbleWidthRatio);
            float maxTextWidth = maxWidth - (DEFAULT_BUBBLE_PADDING * 2);
            float DEFAULT_MIN_TEXT_WIDTHCalc = Mathf.Max(DEFAULT_MIN_TEXT_WIDTH, minBubbleWidth - (DEFAULT_BUBBLE_PADDING * 2));

            // 실제 텍스트 너비 (줄바꿈 전)
            float actualTextWidth = contentText.preferredWidth;

            // 텍스트 너비 결정: 최소 ~ 최대 사이로 제한
            float finalTextWidth = Mathf.Clamp(actualTextWidth, DEFAULT_MIN_TEXT_WIDTHCalc, maxTextWidth);

            // LayoutElement - 최대 너비만 제한 (프리팹에 없으면만 추가)
            LayoutElement textLE = contentText.GetComponent<LayoutElement>();
            if (textLE == null)
                textLE = contentText.gameObject.AddComponent<LayoutElement>();
            textLE.preferredWidth = finalTextWidth;

            // ContentSizeFitter (프리팹에 없으면만 추가)
            ContentSizeFitter textCSF = contentText.GetComponent<ContentSizeFitter>();
            if (textCSF == null)
            {
                textCSF = contentText.gameObject.AddComponent<ContentSizeFitter>();
                textCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                textCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // 레이아웃 강제 리빌드
            RectTransform textRect = contentText.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            if (textRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        }

        // === 6. 행 레이아웃 적용 ===
        ChatBubbleLayoutHelper.SetupMessageRow(bubble, false); // 시스템 메시지는 왼쪽 정렬

        // === 7. 최종 레이아웃 리빌드 ===
        Canvas.ForceUpdateCanvases();
        RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
        if (bubbleRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);
    }

    private IEnumerator LoadChatMessages(string otherUserId, bool isAdmin)
    {
        ClearContent(chatMessageContent);
        ShowChatEmptyState(false); // 기존 빈 상태 제거
        ResetDateSeparatorTracking(); // 날짜 구분선 추적 초기화

        if (isAdmin)
        {
            // 관리자 공지 메시지 표시
            foreach (var broadcast in adminBroadcasts)
            {
                // 날짜 구분선 체크 (관리자 메시지는 useDateSeparator가 false면 스킵)
                if (useDateSeparator)
                    CheckAndCreateDateSeparator(broadcast.created_at);
                CreateAdminMessageBubble(broadcast);
            }
            yield break;
        }

        if (!CheckLogin())
        {
            ShowChatEmptyState(true);
            yield break;
        }

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.DM_CONVERSATION}?user_id={userId}&other_id={otherUserId}";

        bool hasMessages = false;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.certificateHandler = new BypassCertificateHandler();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DMConversationResponse>(request.downloadHandler.text);

                if (response.messages != null && response.messages.Count > 0)
                {
                    hasMessages = true;
                    foreach (var msg in response.messages)
                    {
                        // 날짜 구분선 체크 (24시간 기준)
                        CheckAndCreateDateSeparator(msg.created_at);

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
            ShowChatEmptyState(true);
        }
    }

    private void CreateMessageBubble(DMMessage msg, bool isMine)
    {
        GameObject prefab = isMine ? myMessageBubblePrefab : otherMessageBubblePrefab;
        if (prefab == null || chatMessageContent == null) return;

        GameObject item = Instantiate(prefab, chatMessageContent);
        SetupMessageBubble(item, msg, isMine);
    }

    private void SetupMessageBubble(GameObject item, DMMessage msg, bool isMine)
    {
        SetLayerRecursively(item, 5);

        // BubbleContainer 또는 Bubble 찾기
        Transform bubbleContainerTr = item.transform.Find("BubbleContainer");
        if (bubbleContainerTr == null)
            bubbleContainerTr = item.transform.Find("Bubble");

        // BubbleContainer 레이아웃 설정
        if (bubbleContainerTr != null)
        {
            // 기존 LayoutElement 고정값 초기화
            LayoutElement existingLE = bubbleContainerTr.GetComponent<LayoutElement>();
            if (existingLE != null)
            {
                existingLE.preferredWidth = -1;
                existingLE.minWidth = -1;
                existingLE.flexibleWidth = 0;
            }

            // VerticalLayoutGroup 설정 (프리팹에 없으면 추가, 있으면 프리팹 값 유지)
            VerticalLayoutGroup bubbleVLG = bubbleContainerTr.GetComponent<VerticalLayoutGroup>();
            if (bubbleVLG == null)
            {
                bubbleVLG = bubbleContainerTr.gameObject.AddComponent<VerticalLayoutGroup>();
                bubbleVLG.padding = new RectOffset((int)DEFAULT_BUBBLE_PADDING, (int)DEFAULT_BUBBLE_PADDING, (int)DEFAULT_BUBBLE_PADDING, (int)DEFAULT_BUBBLE_PADDING);
                bubbleVLG.spacing = DEFAULT_BUBBLE_INNER_SPACING;
                bubbleVLG.childAlignment = isMine ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            }
            // childControl은 레이아웃 동작에 필수이므로 항상 설정
            bubbleVLG.childControlWidth = true;
            bubbleVLG.childControlHeight = true;
            bubbleVLG.childForceExpandWidth = false;
            bubbleVLG.childForceExpandHeight = false;

            // ContentSizeFitter - 가로/세로 모두 컨텐츠에 맞춤 (프리팹에 없으면 추가)
            ContentSizeFitter bubbleCSF = bubbleContainerTr.GetComponent<ContentSizeFitter>();
            if (bubbleCSF == null)
            {
                bubbleCSF = bubbleContainerTr.gameObject.AddComponent<ContentSizeFitter>();
                bubbleCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                bubbleCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

        }

        // 내용 설정
        Text contentText = item.transform.Find("ContentText")?.GetComponent<Text>();
        if (contentText == null)
            contentText = bubbleContainerTr?.Find("ContentText")?.GetComponent<Text>();
        if (contentText == null)
            contentText = item.GetComponentInChildren<Text>();

        if (contentText != null)
        {
            // 텍스트 설정 (폰트 크기/색상은 프리팹 값 유지)
            contentText.text = msg.content;
            contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contentText.verticalOverflow = VerticalWrapMode.Overflow;
            // 폰트만 설정 (프리팹에 없을 경우)
            if (chatFont != null && contentText.font == null)
                contentText.font = chatFont;

            // 캔버스 업데이트 후 버블 너비 계산
            Canvas.ForceUpdateCanvases();

            // 버블 최대 너비 계산 (최대 800 또는 화면의 maxBubbleWidthRatio)
            float screenWidth = Screen.width > 0 ? Screen.width : defaultScreenWidth;
            float maxWidth = Mathf.Min(maxBubbleWidthPixels, screenWidth * maxBubbleWidthRatio);
            float maxTextWidth = maxWidth - (DEFAULT_BUBBLE_PADDING * 2);
            float DEFAULT_MIN_TEXT_WIDTHCalc = Mathf.Max(DEFAULT_MIN_TEXT_WIDTH, minBubbleWidth - (DEFAULT_BUBBLE_PADDING * 2));

            // 실제 텍스트 너비
            float actualTextWidth = contentText.preferredWidth;

            // === 동적 버블 너비: min/max 범위 내에서 텍스트 양에 따라 조절 ===
            // 한 줄이면 텍스트 크기에 맞게, 여러 줄이면 최대 너비 사용
            float finalTextWidth = Mathf.Clamp(actualTextWidth, DEFAULT_MIN_TEXT_WIDTHCalc, maxTextWidth);

            // ContentSizeFitter 추가 - 텍스트 크기에 맞춤
            ContentSizeFitter textCSF = contentText.GetComponent<ContentSizeFitter>();
            if (textCSF == null)
                textCSF = contentText.gameObject.AddComponent<ContentSizeFitter>();
            textCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            textCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // LayoutElement 설정 - min/max 너비 모두 설정
            LayoutElement textLE = contentText.GetComponent<LayoutElement>();
            if (textLE == null)
                textLE = contentText.gameObject.AddComponent<LayoutElement>();
            textLE.preferredWidth = finalTextWidth;
            textLE.minWidth = DEFAULT_MIN_TEXT_WIDTHCalc;  // 최소 너비 설정
            textLE.flexibleWidth = 0;
            textLE.preferredHeight = -1;

            // RectTransform 설정 - 내 메시지는 우측 정렬, 상대방은 좌측 정렬
            RectTransform textRect = contentText.GetComponent<RectTransform>();
            if (textRect != null)
            {
                if (isMine)
                {
                    // 내 메시지: 우측 고정, 좌측으로 확장
                    textRect.anchorMin = new Vector2(1, 1);
                    textRect.anchorMax = new Vector2(1, 1);
                    textRect.pivot = new Vector2(1, 1);
                }
                else
                {
                    // 상대방 메시지: 좌측 고정, 우측으로 확장
                    textRect.anchorMin = new Vector2(0, 1);
                    textRect.anchorMax = new Vector2(0, 1);
                    textRect.pivot = new Vector2(0, 1);
                }
                textRect.sizeDelta = new Vector2(finalTextWidth, textRect.sizeDelta.y);
            }
        }

        // TimeArea 처리
        Transform timeAreaTr = item.transform.Find("TimeArea");
        Text timeText = timeAreaTr?.Find("TimeText")?.GetComponent<Text>();
        if (timeText == null)
            timeText = item.transform.Find("TimeText")?.GetComponent<Text>();

        // 날짜 구분선 사용 시 버블 내 시간 숨김
        if (useDateSeparator && hideTimeInBubbleWhenSeparator && timeAreaTr != null)
        {
            timeAreaTr.gameObject.SetActive(false);
        }
        else if (TIME_INSIDE_BUBBLE && timeAreaTr != null && bubbleContainerTr != null)
        {
            // === Instagram DM 스타일: TimeArea를 BubbleContainer 안으로 이동 ===
            timeAreaTr.SetParent(bubbleContainerTr, false);
            timeAreaTr.SetAsLastSibling();

            // 기존 VLG 제거
            VerticalLayoutGroup timeVLG = timeAreaTr.GetComponent<VerticalLayoutGroup>();
            if (timeVLG != null) DestroyImmediate(timeVLG);

            // ContentSizeFitter 제거 (버블 너비에 영향 주지 않도록)
            ContentSizeFitter timeAreaCSF = timeAreaTr.GetComponent<ContentSizeFitter>();
            if (timeAreaCSF != null) DestroyImmediate(timeAreaCSF);

            // LayoutElement 설정 - 버블 너비에 영향 없음
            LayoutElement timeLE = timeAreaTr.GetComponent<LayoutElement>();
            if (timeLE == null)
                timeLE = timeAreaTr.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredWidth = -1;
            timeLE.minWidth = -1;
            timeLE.flexibleWidth = 0;

            // HorizontalLayoutGroup으로 오른쪽 정렬
            HorizontalLayoutGroup timeHLG = timeAreaTr.GetComponent<HorizontalLayoutGroup>();
            if (timeHLG == null)
                timeHLG = timeAreaTr.gameObject.AddComponent<HorizontalLayoutGroup>();
            timeHLG.childAlignment = TextAnchor.MiddleRight;
            timeHLG.padding = new RectOffset(0, (int)TIME_INSIDE_MARGIN_RIGHT, 0, (int)TIME_INSIDE_MARGIN_BOTTOM);
            timeHLG.childForceExpandWidth = false;
            timeHLG.childForceExpandHeight = false;

            // TimeText 설정 (폰트 크기/색상은 프리팹 값 유지)
            if (timeText != null)
            {
                timeText.text = GetShortTime(msg.created_at);
                timeText.alignment = TextAnchor.MiddleRight;
                if (chatFont != null && timeText.font == null)
                    timeText.font = chatFont;

                ContentSizeFitter timeCSF = timeText.GetComponent<ContentSizeFitter>();
                if (timeCSF == null)
                    timeCSF = timeText.gameObject.AddComponent<ContentSizeFitter>();
                timeCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                timeCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
        else if (timeText != null)
        {
            // 기존 스타일: TimeArea 버블 옆에 위치 (폰트 크기/색상은 프리팹 값 유지)
            timeText.text = GetShortTime(msg.created_at);
            if (chatFont != null && timeText.font == null)
                timeText.font = chatFont;
        }

        // 레이아웃 리빌드
        if (bubbleContainerTr != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleContainerTr as RectTransform);
        }

        // 읽음 표시 (내 메시지) - 체크마크 스타일
        if (isMine)
        {
            Text readText = item.transform.Find("ReadText")?.GetComponent<Text>();
            if (readText != null)
            {
                // ✓ 단일 체크 = 전송됨, ✓✓ 더블 체크 = 읽음
                readText.text = msg.is_read ? "✓✓" : "✓";
                readText.color = msg.is_read ? readIndicatorColorRead : readIndicatorColorUnread;
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

        // 아바타 처리 (상대방 버블: 표시 + 프로필 열기 / 내 버블: 숨김)
        Transform avatarTr = item.transform.Find("AvatarContainer");
        if (avatarTr == null)
            avatarTr = item.transform.Find("Avatar");

        if (avatarTr != null)
        {
            if (!isMine)
            {
                // 상대방 메시지: 아바타 명시적 활성화 + 이미지 설정
                avatarTr.gameObject.SetActive(true);

                string senderId = msg.sender_id;
                string senderName = msg.sender_username ?? currentChatUsername;

                // 아바타 탭 핸들러 설정
                AvatarTapHandler avatarHandler = avatarTr.GetComponent<AvatarTapHandler>();
                if (avatarHandler == null)
                    avatarHandler = avatarTr.gameObject.AddComponent<AvatarTapHandler>();

                avatarHandler.Initialize(senderId, senderName, OpenProfileFromChatRoom);

                // 아바타 이미지 로드 - 중앙 캐시 시스템 사용
                string avatarUrl = msg.sender_avatar_url;
                if (string.IsNullOrEmpty(avatarUrl))
                    avatarUrl = currentChatAvatarUrl;

                ProfileManager.LoadAvatarWithMaskAsync(senderId, avatarUrl, avatarTr, senderName);
            }
            else
            {
                // 내 메시지: 아바타 숨김 (MyMessageBubble 프리팹에 없을 수도 있지만 안전 처리)
                avatarTr.gameObject.SetActive(false);
            }
        }
    }

    private void CreateAdminMessageBubble(AdminBroadcast broadcast)
    {
        GameObject prefab = adminMessageBubblePrefab ?? otherMessageBubblePrefab;
        if (prefab == null || chatMessageContent == null) return;

        GameObject item = Instantiate(prefab, chatMessageContent);
        SetLayerRecursively(item, 5);

        // LabelText 비활성화 (ChatTitle로 대체됨)
        Transform labelText = item.transform.Find("BubbleContainer/LabelText");
        if (labelText != null)
            labelText.gameObject.SetActive(false);

        // BubbleContainer 설정 (프리팹 값 유지, 없으면만 추가)
        Transform bubbleContainerTr = item.transform.Find("BubbleContainer");
        if (bubbleContainerTr != null)
        {
            // ContentSizeFitter (프리팹에 없으면만 추가)
            ContentSizeFitter bubbleCSF = bubbleContainerTr.GetComponent<ContentSizeFitter>();
            if (bubbleCSF == null)
            {
                bubbleCSF = bubbleContainerTr.gameObject.AddComponent<ContentSizeFitter>();
                bubbleCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                bubbleCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // VerticalLayoutGroup (프리팹에 없으면만 추가, 있으면 프리팹 값 유지)
            VerticalLayoutGroup bubbleVLG = bubbleContainerTr.GetComponent<VerticalLayoutGroup>();
            if (bubbleVLG == null)
            {
                bubbleVLG = bubbleContainerTr.gameObject.AddComponent<VerticalLayoutGroup>();
                bubbleVLG.padding = new RectOffset((int)DEFAULT_BUBBLE_PADDING, (int)DEFAULT_BUBBLE_PADDING, (int)DEFAULT_BUBBLE_PADDING, (int)DEFAULT_BUBBLE_PADDING);
                bubbleVLG.spacing = DEFAULT_BUBBLE_INNER_SPACING;
                bubbleVLG.childAlignment = TextAnchor.MiddleLeft;
            }
            // childControl은 레이아웃 동작에 필수이므로 항상 설정
            bubbleVLG.childControlWidth = true;
            bubbleVLG.childControlHeight = true;
            bubbleVLG.childForceExpandWidth = false;
            bubbleVLG.childForceExpandHeight = false;
        }

        // 내용 설정 (프리팹의 폰트/색상/크기 유지)
        Text contentText = item.transform.Find("ContentText")?.GetComponent<Text>();
        if (contentText == null)
            contentText = item.transform.Find("BubbleContainer/ContentText")?.GetComponent<Text>();
        if (contentText != null)
        {
            // 텍스트 내용만 설정 (폰트 크기/색상은 프리팹 값 유지)
            contentText.text = broadcast.content;
            contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contentText.verticalOverflow = VerticalWrapMode.Overflow;
            // 폰트만 설정 (프리팹에 없을 경우)
            if (chatFont != null && contentText.font == null)
                contentText.font = chatFont;

            Canvas.ForceUpdateCanvases();

            // 버블 최대 너비 계산 (Inspector에서 조절 가능)
            // Admin 버블은 아바타 + HLG 패딩 + spacing 공간을 고려해야 함
            float screenWidth = Screen.width > 0 ? Screen.width : defaultScreenWidth;
            float avatarSpace = 80f + 32f + 8f; // 아바타(80) + HLG패딩(16*2) + spacing(8)
            float maxWidth = Mathf.Min(maxBubbleWidthPixels, screenWidth * maxBubbleWidthRatio) - avatarSpace;
            float maxTextWidth = maxWidth - (DEFAULT_BUBBLE_PADDING * 2);
            float DEFAULT_MIN_TEXT_WIDTHCalc = Mathf.Max(DEFAULT_MIN_TEXT_WIDTH, minBubbleWidth - (DEFAULT_BUBBLE_PADDING * 2));

            // 실제 텍스트 너비
            float actualTextWidth = contentText.preferredWidth;

            // 텍스트가 짧으면 텍스트 크기에 맞춤, 길면 최대 너비로 제한
            float finalTextWidth = Mathf.Clamp(actualTextWidth, DEFAULT_MIN_TEXT_WIDTHCalc, maxTextWidth);

            // ContentSizeFitter (프리팹에 없으면만 추가)
            ContentSizeFitter textCSF = contentText.GetComponent<ContentSizeFitter>();
            if (textCSF == null)
            {
                textCSF = contentText.gameObject.AddComponent<ContentSizeFitter>();
                textCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                textCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // LayoutElement - 최대 너비 제한 (OtherMessageBubble과 동일하게)
            LayoutElement textLE = contentText.GetComponent<LayoutElement>();
            if (textLE == null)
                textLE = contentText.gameObject.AddComponent<LayoutElement>();
            textLE.preferredWidth = finalTextWidth;
            textLE.minWidth = DEFAULT_MIN_TEXT_WIDTHCalc;
            textLE.flexibleWidth = 0;
            textLE.preferredHeight = -1;

            // RectTransform 설정 - 좌측 정렬 (Admin 메시지는 항상 좌측)
            RectTransform textRect = contentText.GetComponent<RectTransform>();
            if (textRect != null)
            {
                textRect.anchorMin = new Vector2(0, 1);
                textRect.anchorMax = new Vector2(0, 1);
                textRect.pivot = new Vector2(0, 1);
                textRect.sizeDelta = new Vector2(finalTextWidth, textRect.sizeDelta.y);
            }
        }

        // TimeArea 처리
        Transform timeAreaTr = item.transform.Find("TimeArea");
        Text timeText = timeAreaTr?.Find("TimeText")?.GetComponent<Text>();
        if (timeText == null)
            timeText = item.transform.Find("TimeText")?.GetComponent<Text>();

        // 날짜 구분선 사용 시 버블 내 시간 숨김
        if (useDateSeparator && hideTimeInBubbleWhenSeparator && timeAreaTr != null)
        {
            timeAreaTr.gameObject.SetActive(false);
        }
        else if (TIME_INSIDE_BUBBLE && timeAreaTr != null && bubbleContainerTr != null)
        {
            // Instagram DM 스타일: TimeArea를 BubbleContainer 안으로 이동
            timeAreaTr.SetParent(bubbleContainerTr, false);
            timeAreaTr.SetAsLastSibling();

            // 기존 VLG 제거
            VerticalLayoutGroup timeVLG = timeAreaTr.GetComponent<VerticalLayoutGroup>();
            if (timeVLG != null) DestroyImmediate(timeVLG);

            // TimeArea ContentSizeFitter 제거 (버블 너비에 영향 주지 않도록!)
            ContentSizeFitter timeAreaCSF = timeAreaTr.GetComponent<ContentSizeFitter>();
            if (timeAreaCSF != null)
                DestroyImmediate(timeAreaCSF);

            // TimeArea LayoutElement 설정 - preferredWidth 없이, 버블 너비 계산에 영향 X
            LayoutElement timeLE = timeAreaTr.GetComponent<LayoutElement>();
            if (timeLE == null)
                timeLE = timeAreaTr.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredWidth = -1;
            timeLE.minWidth = -1;
            timeLE.flexibleWidth = 0;
            timeLE.preferredHeight = -1;
            timeLE.minHeight = -1;

            // HorizontalLayoutGroup으로 오른쪽 정렬
            HorizontalLayoutGroup timeHLG = timeAreaTr.GetComponent<HorizontalLayoutGroup>();
            if (timeHLG == null)
                timeHLG = timeAreaTr.gameObject.AddComponent<HorizontalLayoutGroup>();
            timeHLG.childAlignment = TextAnchor.MiddleRight;
            timeHLG.padding = new RectOffset(0, (int)TIME_INSIDE_MARGIN_RIGHT, 0, (int)TIME_INSIDE_MARGIN_BOTTOM);
            timeHLG.childForceExpandWidth = false;
            timeHLG.childForceExpandHeight = false;
            timeHLG.childControlWidth = false;
            timeHLG.childControlHeight = false;

            // TimeText 설정 (폰트 크기/색상은 프리팹 값 유지)
            if (timeText != null)
            {
                timeText.text = GetShortTime(broadcast.created_at);
                timeText.alignment = TextAnchor.MiddleRight;
                if (chatFont != null && timeText.font == null)
                    timeText.font = chatFont;

                ContentSizeFitter timeCSF = timeText.GetComponent<ContentSizeFitter>();
                if (timeCSF == null)
                    timeCSF = timeText.gameObject.AddComponent<ContentSizeFitter>();
                timeCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                timeCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
        else if (timeText != null)
        {
            // 기존 스타일 (폰트 크기/색상은 프리팹 값 유지)
            timeText.text = GetShortTime(broadcast.created_at);
            if (chatFont != null && timeText.font == null)
                timeText.font = chatFont;
        }

        // 행 레이아웃 적용
        ChatBubbleLayoutHelper.SetupMessageRow(item, false);

        // 레이아웃 강제 리빌드
        if (bubbleContainerTr != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleContainerTr as RectTransform);
        }

        // 아바타 설정 (프리팹 값 유지, Inspector의 systemAvatarSprite 있으면 적용)
        Transform avatarTr = item.transform.Find("AvatarContainer");
        if (avatarTr == null)
            avatarTr = item.transform.Find("Avatar");
        if (avatarTr != null)
        {
            // 프리팹에 스프라이트가 없고, systemAvatarSprite가 설정되어 있으면 적용
            Image avatarImage = avatarTr.GetComponent<Image>();
            if (avatarImage != null && avatarImage.sprite == null && systemAvatarSprite != null)
            {
                avatarImage.sprite = systemAvatarSprite;
                avatarImage.preserveAspect = true;
            }

            // 관리자 메시지 아바타: 프로필 열기 비활성화 (WOOPANG 시스템이므로)
            AvatarTapHandler avatarHandler = avatarTr.GetComponent<AvatarTapHandler>();
            if (avatarHandler != null)
                Destroy(avatarHandler);
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
            request.certificateHandler = new BypassCertificateHandler();
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
            request.certificateHandler = new BypassCertificateHandler();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
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
            request.certificateHandler = new BypassCertificateHandler();
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
            request.certificateHandler = new BypassCertificateHandler();
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

        // 아바타 - 중앙 캐시 시스템 사용
        Transform avatarTransform = item.transform.Find("Avatar");
        if (avatarTransform != null)
            ProfileManager.LoadAvatarWithMaskAsync(user.id, user.avatar_url, avatarTransform, user.username);
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
            request.certificateHandler = new BypassCertificateHandler();
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
            request.certificateHandler = new BypassCertificateHandler();
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
        heartRect.sizeDelta = heartIconSize; // Inspector에서 조절 가능
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

    #region Helpers

    private bool CheckLogin()
    {
        return LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn;
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
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child);
            else
                Destroy(child);
#else
            Destroy(child);
#endif
        }
    }

    /// <summary>
    /// 모든 대화 목록 및 데이터 초기화 (빈 상태 테스트용)
    /// </summary>
    public void ClearAllConversations()
    {
        conversations.Clear();
        adminBroadcasts.Clear();

        if (conversationListContent != null)
            ClearContent(conversationListContent);

        // 빈 상태 표시
        ShowEmptyState(conversationListContent, GetLocalizedEmptyInboxMessage(), true);
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
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, -50); // 살짝 위로
        rect.sizeDelta = new Vector2(-40, 0); // 좌우 여백

        Text emptyText = emptyStateObject.AddComponent<Text>();
        emptyText.text = message;

        // AppleSDGothicNeoM 폰트 사용
        if (chatFont != null)
            emptyText.font = chatFont;
        else
            emptyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        emptyText.fontSize = emptyStateFontSize;
        emptyText.color = emptyStateTextColor;
        emptyText.alignment = TextAnchor.MiddleCenter;
        emptyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        emptyText.verticalOverflow = VerticalWrapMode.Overflow;
        emptyText.lineSpacing = 1.2f;
    }

    /// <summary>
    /// 다국어 빈 대화목록 메시지 가져오기
    /// </summary>
    private string GetLocalizedEmptyInboxMessage()
    {
        if (LocalizationManager.Instance != null)
        {
            string localizedMsg = LocalizationManager.Instance.GetText("empty_inbox_message");
            if (!string.IsNullOrEmpty(localizedMsg) && localizedMsg != "empty_inbox_message")
            {
                return localizedMsg;
            }
        }
        return emptyInboxMessage; // fallback
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

        // AppleSDGothicNeoM 폰트 사용
        if (chatFont != null)
            emptyText.font = chatFont;
        else
            emptyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        emptyText.fontSize = emptyStateFontSize; // Inspector에서 조절 가능
        emptyText.color = emptyStateTextColor;   // Inspector에서 조절 가능
        emptyText.alignment = TextAnchor.MiddleCenter;
        emptyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        emptyText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    /// <summary>
    /// 아바타를 원형 마스크 구조로 설정하고 이미지를 로드할 Image 컴포넌트 반환
    /// FullProfilePanel 패턴 준수: AvatarMask(Mask+Knob) → AvatarImage(실제 이미지)
    /// </summary>
    private Image SetupCircularAvatarStructure(Transform avatarContainer)
    {
        if (avatarContainer == null) return null;

        // 1. 컨테이너 Image (프리팹에 없으면만 추가)
        Image containerImage = avatarContainer.GetComponent<Image>();
        bool containerImageWasNull = containerImage == null;
        if (containerImageWasNull)
        {
            containerImage = avatarContainer.gameObject.AddComponent<Image>();
            containerImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            containerImage.type = Image.Type.Simple;
            containerImage.preserveAspect = true;
        }

        // 2. Mask 컴포넌트 추가 (없으면)
        Mask mask = avatarContainer.GetComponent<Mask>();
        if (mask == null)
        {
            mask = avatarContainer.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        // 3. 자식 AvatarImage 찾기 또는 생성
        Transform avatarImageTransform = avatarContainer.Find("AvatarImage");
        if (avatarImageTransform == null)
        {
            // 프리팹에 AvatarImage 없으면 새로 생성
            GameObject avatarImageObj = new GameObject("AvatarImage");
            avatarImageObj.transform.SetParent(avatarContainer, false);
            avatarImageObj.layer = 5; // UI Layer

            RectTransform avatarImageRect = avatarImageObj.AddComponent<RectTransform>();
            avatarImageRect.anchorMin = Vector2.zero;
            avatarImageRect.anchorMax = Vector2.one;
            avatarImageRect.offsetMin = Vector2.zero;
            avatarImageRect.offsetMax = Vector2.zero;

            Image avatarImage = avatarImageObj.AddComponent<Image>();
            avatarImage.color = Color.white; // 투명하게 시작 (URL 로드 후 적용됨)
            avatarImage.raycastTarget = false;

            return avatarImage;
        }

        // 프리팹에 AvatarImage 있으면 그대로 반환 (색상/스프라이트 유지)
        Image existingImage = avatarImageTransform.GetComponent<Image>();
        if (existingImage == null)
        {
            existingImage = avatarImageTransform.gameObject.AddComponent<Image>();
            existingImage.raycastTarget = false;
        }

        return existingImage;
    }

    private IEnumerator LoadAvatar(string url, Image targetImage)
    {
        if (string.IsNullOrEmpty(url) || targetImage == null) yield break;

        string fullUrl = url.StartsWith("http") ? url : ApiConfig.MAIN_SERVER + "/" + url;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
        {
            request.certificateHandler = new BypassCertificateHandler();
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

    /// <summary>
    /// 아바타 URL이 없을 때 유저네임 기반 컬러 원형 아바타 텍스처 생성
    /// 실제 Texture2D → Sprite로 변환하여 기존 아바타 로드와 동일한 방식 적용
    /// </summary>
    private void SetDefaultAvatar(Transform avatarContainer, string username)
    {
        if (avatarContainer == null) return;

        // 기존 LoadAvatarWithMask와 동일한 원형 마스크 구조 설정
        Image targetImage = SetupCircularAvatarStructure(avatarContainer);
        if (targetImage == null) return;

        // 유저네임 기반 파스텔 컬러 텍스처 생성
        Texture2D tex = GenerateAvatarTexture(username, 128);
        Sprite sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f));
        targetImage.sprite = sprite;
        targetImage.color = Color.white;
    }

    /// <summary>
    /// 유저네임 기반 그라데이션 원형 아바타 텍스처 생성
    /// </summary>
    private Texture2D GenerateAvatarTexture(string username, int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        int hash = string.IsNullOrEmpty(username) ? 0 : username.GetHashCode();
        float hue1 = Mathf.Abs(hash % 360) / 360f;
        float hue2 = (hue1 + 0.15f) % 1f;
        Color color1 = Color.HSVToRGB(hue1, 0.5f, 0.9f);
        Color color2 = Color.HSVToRGB(hue2, 0.4f, 0.75f);

        float center = size / 2f;
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= radius)
                {
                    // 대각선 그라데이션
                    float t = ((float)x + y) / (size * 2f);
                    Color c = Color.Lerp(color1, color2, t);

                    // 가장자리 안티앨리어싱
                    if (dist > radius - 1.5f)
                        c.a = Mathf.Clamp01((radius - dist) / 1.5f);

                    tex.SetPixel(x, y, c);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 아바타를 원형 마스크 구조로 로드 (구조 설정 + 이미지 로드)
    /// </summary>
    private IEnumerator LoadAvatarWithMask(string url, Transform avatarContainer)
    {
        if (string.IsNullOrEmpty(url) || avatarContainer == null) yield break;

        // 원형 마스크 구조 설정 및 AvatarImage 가져오기
        Image targetImage = SetupCircularAvatarStructure(avatarContainer);
        if (targetImage == null) yield break;

        yield return LoadAvatar(url, targetImage);
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

    /// <summary>
    /// 오브젝트와 모든 자식의 레이어를 재귀적으로 설정
    /// </summary>
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
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
            request.certificateHandler = new BypassCertificateHandler();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DMUnreadCountResponse>(request.downloadHandler.text);
                int previousCount = totalUnreadCount;
                totalUnreadCount = response.unread_count;
                UpdateUnreadUI();

                // 안읽음 수가 변경되었고, 메시지 패널이 열려있으면 대화 목록 갱신
                if (previousCount != totalUnreadCount && messagePanel != null && messagePanel.activeInHierarchy)
                {
                    yield return StartCoroutine(LoadConversationList());
                }
            }
        }
    }

    private void UpdateUnreadUI()
    {
        // DM 안읽음 + 위치 알림 안읽음 합산
        int locationUnread = GetUnreadLocationNotificationCount();
        int totalUnread = totalUnreadCount + locationUnread;

        if (globalUnreadIndicator != null)
            globalUnreadIndicator.SetActive(totalUnread > 0);
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

    /// <summary>
    /// 업로드 완료 안내를 WarningText로 표시하고 패널을 닫는 메서드 (런타임용)
    /// </summary>
    public void ShowUploadCompleteAndClose(string locationName = "")
    {
        string message = string.IsNullOrEmpty(locationName)
            ? "큐브가 승인되었습니다!"
            : $"'{locationName}' 큐브가 승인되었습니다!";

        // CubeUploadManager의 WarningText로 토스트 표시
        var uploadManager = FindFirstObjectByType<CubeUploadManager>();
        if (uploadManager != null)
        {
            uploadManager.ShowWarning(message);
        }

        // 패널이 열려있으면 닫기
        if (messagePanel != null && messagePanel.activeInHierarchy)
        {
            StartCoroutine(CloseAfterDelay(1.5f));
        }
    }

    /// <summary>
    /// 지정 시간 후 메시지 패널 닫기
    /// </summary>
    private IEnumerator CloseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        CloseMessagePanel();
    }
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

    // 시스템 메시지 (위치 알림, 관리자 메시지 등) 구분용
    public bool isSystemMessage;    // true면 어두운 배경
    public bool isRead;             // 읽음 여부
    public string notificationId;   // 시스템 알림용 고유 ID
    public bool isAdminBroadcast;   // 관리자 공지 (broadcast API)

    // 위치 정보 (위치 알림용)
    public float latitude;
    public float longitude;
    public float radius;
    public string distance;
}

[Serializable]
public class ConversationSummaryList
{
    public List<ConversationSummary> conversations;
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
public class DMConversationsResponse
{
    public List<DMConversationItem> conversations;
    public int count;
    public int total_unread;
}

[Serializable]
public class DMConversationItem
{
    public string partner_id;
    public int message_id;
    public string sender_id;
    public string recipient_id;
    public string last_message;
    public bool is_read;
    public string last_message_time;
    public string partner_username;
    public string partner_avatar_url;
    public int unread_count;
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
public class LocationNotification
{
    public string id;
    public string title;
    public string body;
    public string receivedAt;
    public bool isRead;
    public float latitude;
    public float longitude;
    public float radius;
    public string currentDistance;
}

[Serializable]
public class LocationNotificationList
{
    public List<LocationNotification> notifications;
}

#endregion
