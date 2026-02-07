using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// DM 테스트 데이터 생성기
/// 개발/테스트 용도로 샘플 대화 데이터를 생성
/// 자동 메시지 시뮬레이션, 알림 팝업, 읽음 확인 테스트 지원
/// </summary>
public class DMTestDataGenerator : MonoBehaviour
{
    [Header("테스트 설정")]
    [Tooltip("Play 모드 시작 시 자동으로 테스트 데이터 생성 (MessagePanel 열 때 일반 DM 대화 생성)")]
    public bool autoGenerateOnStart = true;  // 기본 활성화 (UI 테스트용) - Inspector에서 False로 바꾸지 마세요!

    [Tooltip("시작 시 자동으로 시스템 알림 대화 생성")]
    public bool autoLoadSystemNotification = true;

    [Tooltip("시작 시 자동으로 ChatRoomPanel 열고 테스트 버블 표시")]
    public bool autoOpenChatRoom = false;

    [Tooltip("테스트 대화 수")]
    [Range(1, 10)]
    public int conversationCount = 5;

    [Header("단축키")]
    public KeyCode openPanelKey = KeyCode.M;

    [Header("자동 메시지 시뮬레이션")]
    [Tooltip("자동 메시지 시뮬레이션 활성화")]
    public bool enableAutoMessages = false;  // 기본 비활성화


    [Header("=== 레이아웃 설정 (Inspector에서 조절 가능) ===")]
    [Tooltip("대화 아이템 최소 높이")]
    public float conversationItemHeight = 140f;

    [Tooltip("채팅 메시지 폰트 크기 (0이면 ChatBubbleLayoutHelper 사용)")]
    public int chatFontSize = 0;

    [Tooltip("시간 텍스트 폰트 크기")]
    public int timeFontSize = 22;

    [Tooltip("읽음 표시 폰트 크기")]
    public int readFontSize = 20;

    [Tooltip("읽음 표시 텍스트 색상 (읽음 확인 시 파란색)")]
    public Color readTextColor = new Color(0.5f, 0.8f, 1f, 1f);

    [Tooltip("기본 아바타 배경 색상")]
    public Color defaultAvatarColor = new Color(0.9f, 0.9f, 0.9f, 1f);

    [Tooltip("Content 레이아웃 간격")]
    public float contentSpacing = 8f;

    [Tooltip("Content 레이아웃 패딩 (좌, 우, 상, 하)")]
    public int contentPadding = 10;

    [Header("=== 날짜 구분선 설정 ===")]
    [Tooltip("날짜 구분선 사용 (24시간 기준)")]
    public bool useDateSeparator = true;

    [Tooltip("날짜 구분선 폰트 크기")]
    public int dateSeparatorFontSize = 28;

    [Tooltip("날짜 구분선 텍스트 색상")]
    public Color dateSeparatorColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Tooltip("날짜 구분선 상하 마진")]
    public float dateSeparatorMargin = 20f;

    // 마지막 메시지 시간 (날짜 구분선용)
    private DateTime lastMessageTime = DateTime.MinValue;


    [Tooltip("시뮬레이션 지속 시간 (초)")]
    public float simulationDuration = 600f; // 10분

    [Tooltip("총 메시지 수")]
    [Range(10, 20)]
    public int totalMessages = 13;

    private MessagePanelManager messagePanelManager;
    private bool isGenerated = false;
    private bool isSimulationRunning = false;
    private Coroutine simulationCoroutine;
    private Coroutine populateMessagesCoroutine;

    // 현재 열린 채팅방 정보
    private string currentChatUserId = "";
    private string currentChatUsername = "";
    private string currentChatAvatarEmoji = "";

    // 코루틴 실행 중 플래그 (중복 실행 방지)
    private bool isPopulatingConversations = false;

    // 읽음 확인 추적
    private Dictionary<string, bool> messageReadStatus = new Dictionary<string, bool>();
    private List<GameObject> currentChatMessages = new List<GameObject>();

    // 테스트 사용자 데이터
    private readonly string[] testUsernames = new string[]
    {
        "김민지", "이준호", "박서연", "최영수", "정하늘",
        "WOOPANG_Official", "AR_Master", "여행러버", "맛집탐험가", "사진작가"
    };

    // 테스트 사용자 이모지 아바타 (대화 목록 + 채팅방에서 사용)
    private readonly string[] testAvatarEmojis = new string[]
    {
        "👧", "🧑", "👩", "👨", "🧒",
        "🌟", "🎯", "✈️", "🍜", "📸"
    };

    private readonly string[] testMessages = new string[]
    {
        "안녕하세요! 반갑습니다 :)",
        "어제 올린 AR 콘텐츠 정말 멋있었어요!",
        "혹시 그 장소가 어디인가요?",
        "나중에 같이 AR 콘텐츠 만들어볼까요?",
        "좋은 하루 되세요!",
        "새로운 업데이트 확인하셨나요?",
        "이번 주말에 모임 있어요!",
        "사진 공유해주셔서 감사합니다",
        "저도 그 장소 가봤어요. 정말 좋았어요!",
        "WOOPANG 앱 최고입니다!"
    };

    private readonly string[] adminMessages = new string[]
    {
        // === 1줄 메시지 (짧음) ===
        "안녕!",
        "공지입니다",
        "업데이트 완료!",

        // === 1줄 메시지 (중간) ===
        "WOOPANG에 오신 것을 환영합니다!",
        "새로운 업데이트가 있습니다. 지금 확인해보세요!",
        "이번 주 인기 AR 콘텐츠를 소개합니다.",

        // === 2줄 메시지 ===
        "WOOPANG v2.0 업데이트 안내\n새로운 AR 필터와 이펙트가 추가되었습니다!",
        "주변 500m 이내에 새로운 AR 콘텐츠가 등록되었습니다.\n지금 바로 확인해보세요!",

        // === 3줄 메시지 ===
        "🎉 이벤트 당첨을 축하드립니다!\n당첨된 포인트가 지급되었습니다.\n마이페이지에서 확인해주세요.",
        "WOOPANG 서비스 점검 안내\n2월 10일(토) 02:00 ~ 06:00\n서비스 이용이 일시 중단됩니다.",

        // === 4줄 메시지 ===
        "🚀 WOOPANG 대규모 업데이트!\n\n새로운 기능:\n- AR 필터 30종 추가\n- 실시간 채팅 기능\n- 위치 기반 콘텐츠 추천",
        "📢 커뮤니티 가이드라인 안내\n\n모든 사용자가 즐거운 경험을 할 수 있도록\n타인을 존중하는 콘텐츠를 공유해주세요.\n부적절한 콘텐츠는 삭제될 수 있습니다."
    };

    // 자동 응답 메시지 (대화 시뮬레이션용)
    private readonly string[] autoReplyMessages = new string[]
    {
        "네, 맞아요!",
        "그렇군요 ㅎㅎ",
        "저도 그렇게 생각해요",
        "오~ 좋은 아이디어네요!",
        "언제 시간 되세요?",
        "사진 보내주실 수 있나요?",
        "정말요? 대단하시네요!",
        "감사합니다 :)",
        "ㅋㅋㅋ 재밌네요",
        "그거 저도 해봤어요!",
        "내일 만나서 얘기해요",
        "좋아요! 약속했어요~",
        "와 진짜 멋있어요!",
        "그래서 어떻게 됐어요?",
        "궁금해요 더 알려주세요"
    };

    void Start()
    {
        // Play 모드 시작 시 플래그 초기화 (이전 세션의 상태 제거)
        isGenerated = false;
        isPopulatingConversations = false;
        wasPanelActive = false;

        // 에디터에서 테스트용으로 autoGenerateOnStart 자동 활성화
#if UNITY_EDITOR
        if (!autoGenerateOnStart && autoLoadSystemNotification)
        {
            autoGenerateOnStart = true;
        }
#endif

        messagePanelManager = GetComponent<MessagePanelManager>();
        if (messagePanelManager == null)
            messagePanelManager = FindFirstObjectByType<MessagePanelManager>();

        // 테스트 모드일 때 이전 대화 데이터 클리어 (중복 방지)
        if (messagePanelManager != null && (autoGenerateOnStart || autoLoadSystemNotification))
        {
            messagePanelManager.ClearAllConversations();
        }

        // 에디터에서 자동 시작
#if UNITY_EDITOR
        if (autoGenerateOnStart && messagePanelManager != null)
        {
            StartCoroutine(AutoStartInEditor());
        }

        // 시스템 알림 대화 자동 생성
        if (autoLoadSystemNotification && messagePanelManager != null)
        {
            StartCoroutine(AutoLoadSystemNotificationDelayed());
        }

        // MessagePanel이 이미 열려있으면 바로 대화 생성
        if (autoGenerateOnStart && messagePanelManager != null &&
            messagePanelManager.messagePanel != null &&
            messagePanelManager.messagePanel.activeSelf)
        {
            StartCoroutine(PopulateTestConversationsDelayed());
        }

        // ChatRoomPanel 자동 열기 (버블 테스트용)
        if (autoOpenChatRoom && messagePanelManager != null)
        {
            StartCoroutine(AutoOpenChatRoomDelayed());
        }
#else
        if (autoGenerateOnStart && messagePanelManager != null)
        {
            StartCoroutine(GenerateTestDataDelayed());
        }
#endif
    }

    /// <summary>
    /// ChatRoomPanel 자동 열기 (버블 테스트용)
    /// </summary>
    private IEnumerator AutoOpenChatRoomDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        if (messagePanelManager == null) yield break;

        // ChatRoomPanel 열고 테스트 메시지 로드
        OpenTestChatRoom("test_user", "테스트유저", false);
#if UNITY_EDITOR
        messagePanelManager.LoadDummyDMChatMessages();
#endif
    }

    /// <summary>
    /// 시스템 알림 대화 자동 로드 (Play 시작 시)
    /// </summary>
    private IEnumerator AutoLoadSystemNotificationDelayed()
    {
        yield return new WaitForSeconds(0.3f);

        if (messagePanelManager == null) yield break;

        // 시스템 알림 (업로드 완료, WOOPANG 공지 등) 더미 데이터 추가
        AddDummySystemNotifications();
    }

    /// <summary>
    /// 더미 시스템 알림 추가 (대화 목록에 표시됨)
    /// PopulateTestConversations에서 AdminNoticeItem + ConversationItem을 함께 생성
    /// </summary>
    private void AddDummySystemNotifications()
    {
        if (messagePanelManager == null) return;

        // MessagePanel이 열려있으면 바로 전체 대화 목록 생성
        if (messagePanelManager.messagePanel != null &&
            messagePanelManager.messagePanel.activeSelf &&
            !isGenerated && !isPopulatingConversations)
        {
            StartCoroutine(PopulateTestConversations());
        }
        else
        {
            // 패널이 닫혀있으면 나중에 열 때 Update()에서 PopulateTestConversations 호출됨
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 Play 모드 시작 시 자동 실행
    /// 패널은 자동으로 열지 않음 - 사용자가 직접 열어야 함
    /// </summary>
    private IEnumerator AutoStartInEditor()
    {
        yield return new WaitForSeconds(0.5f);

        // 자동 메시지 시뮬레이션만 시작 (패널은 열지 않음)
        if (enableAutoMessages)
        {
            StartAutoMessageSimulation();
        }
    }
#endif

    // 패널 활성화 상태 추적
    private bool wasPanelActive = false;

    void Update()
    {
        // autoGenerateOnStart 또는 autoLoadSystemNotification 중 하나라도 true면 실행
        if (!autoGenerateOnStart && !autoLoadSystemNotification) return;

        // 패널이 활성화되면 테스트 데이터 자동 생성
        if (messagePanelManager == null)
        {
            messagePanelManager = FindFirstObjectByType<MessagePanelManager>();
        }

        if (messagePanelManager != null && messagePanelManager.messagePanel != null)
        {
            bool isPanelActive = messagePanelManager.messagePanel.activeSelf;

            // 패널이 방금 열렸을 때 (비활성→활성) - 아직 생성 안됨 + 생성 중 아님
            if (isPanelActive && !wasPanelActive)
            {
                if (!isGenerated && !isPopulatingConversations)
                {
                    StartCoroutine(PopulateTestConversations());
                }
            }

            wasPanelActive = isPanelActive;
        }

        // 테스트 키 입력은 에디터에서만 사용 (Input System Package 호환성)
#if UNITY_EDITOR && ENABLE_LEGACY_INPUT_MANAGER
        // 테스트 키로 패널 열기
        if (Input.GetKeyDown(openPanelKey) && messagePanelManager != null)
        {
            if (messagePanelManager.messagePanel != null)
            {
                bool isActive = messagePanelManager.messagePanel.activeSelf;
                if (isActive)
                {
                    messagePanelManager.CloseMessagePanel();
                }
                else
                {
                    messagePanelManager.OpenMessagePanel();
                    // 중복 실행 방지 체크 추가
                    if (!isGenerated && !isPopulatingConversations)
                    {
                        StartCoroutine(PopulateTestConversations());
                    }
                }
            }
        }

        // S키로 시뮬레이션 시작/중지
        if (Input.GetKeyDown(KeyCode.S) && Input.GetKey(KeyCode.LeftShift))
        {
            if (isSimulationRunning)
                StopAutoMessageSimulation();
            else
                StartAutoMessageSimulation();
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// 테스트용 다른 사용자 프로필 열기
    /// </summary>
    private void OpenTestProfile()
    {
        ProfileManager profileManager = FindFirstObjectByType<ProfileManager>();
        if (profileManager == null)
        {
            Debug.LogError("[DMTestData] ProfileManager를 찾을 수 없습니다!");
            return;
        }

        if (profileManager.fullProfilePanel == null)
        {
            Debug.LogError("[DMTestData] ProfileManager.fullProfilePanel이 null입니다! Inspector에서 연결해주세요.");
            return;
        }

        // 랜덤 테스트 사용자 선택
        int userIndex = UnityEngine.Random.Range(0, testUsernames.Length);
        string testUserId = $"test_user_{userIndex}";
        string testUsername = testUsernames[userIndex];

        // 테스트 프로필 표시 (API 호출 없이 더미 데이터 사용)
        profileManager.ShowTestProfile(testUserId, testUsername);
    }
#endif

    private IEnumerator GenerateTestDataDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        if (!isGenerated)
        {
            // 테스트 데이터 자동 생성 준비 완료
        }
    }

    /// <summary>
    /// 딜레이 후 테스트 대화 생성 (씬 로드 직후 실행 방지)
    /// </summary>
    private IEnumerator PopulateTestConversationsDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        if (!isGenerated && !isPopulatingConversations)
        {
            yield return StartCoroutine(PopulateTestConversations());
        }
    }

    /// <summary>
    /// 테스트 메시지 패널 열기
    /// </summary>
    public void OpenTestMessagePanel()
    {
        if (messagePanelManager == null)
        {
            messagePanelManager = FindFirstObjectByType<MessagePanelManager>();
        }

        if (messagePanelManager != null && messagePanelManager.messagePanel != null)
        {
            messagePanelManager.messagePanel.SetActive(true);

            // 중복 실행 방지 체크 추가
            if (!isGenerated && !isPopulatingConversations)
            {
                StartCoroutine(PopulateTestConversations());
            }
        }
        else
        {
            Debug.LogError("[DMTestData] MessagePanelManager 또는 messagePanel이 없습니다!");
        }
    }

    /// <summary>
    /// 테스트 대화 목록 생성
    /// </summary>
    public IEnumerator PopulateTestConversations()
    {
        // 중복 실행 방지 - 이미 실행 중이면 스킵
        if (isPopulatingConversations || isGenerated)
        {
            yield break;
        }

        isPopulatingConversations = true;

        if (messagePanelManager == null)
        {
            isPopulatingConversations = false;
            Debug.LogError("[DMTestData] messagePanelManager가 null!");
            yield break;
        }

        // 런타임 프리팹 로딩 (null인 경우)
        LoadPrefabsIfNeeded();

        if (messagePanelManager.conversationListContent == null)
        {
            // conversationListContent를 자동으로 찾기 시도
            TryFindConversationListContent();

            if (messagePanelManager.conversationListContent == null)
            {
                isPopulatingConversations = false;
                Debug.LogError("[DMTestData] conversationListContent가 null! MessagePanelManager Inspector에서 연결 필요");
                yield break;
            }
        }

        // 기존 항목 삭제 (안전한 패턴)
        var childrenToDestroy = new List<GameObject>();
        foreach (Transform child in messagePanelManager.conversationListContent)
            childrenToDestroy.Add(child.gameObject);
        foreach (var child in childrenToDestroy)
            Destroy(child);

        yield return null;

        // Content 레이아웃 설정 (중요!)
        SetupContentLayout(messagePanelManager.conversationListContent);

        // 1. WOOPANG 공지 (최상단)
        if (messagePanelManager.adminNoticePrefab != null)
        {
            GameObject adminItem = Instantiate(messagePanelManager.adminNoticePrefab, messagePanelManager.conversationListContent);
            SetupAdminNoticeItem(adminItem, adminMessages[UnityEngine.Random.Range(0, adminMessages.Length)]);
        }

        yield return null;

        // 2. 테스트 대화 생성 (일반 DM: 김민지, 이준호 등)
        if (messagePanelManager.conversationItemPrefab == null)
        {
            LoadPrefabsIfNeeded();
        }

        if (messagePanelManager.conversationItemPrefab == null)
        {
            Debug.LogError("[DMTestData] conversationItemPrefab 로드 실패! Assets/Prefabs/DM/ConversationItem.prefab 확인 필요");
        }
        else
        {
            for (int i = 0; i < conversationCount; i++)
            {
                GameObject item = Instantiate(messagePanelManager.conversationItemPrefab, messagePanelManager.conversationListContent);
                item.SetActive(true);  // 명시적 활성화

                string username = testUsernames[i % testUsernames.Length];
                string emoji = testAvatarEmojis[i % testAvatarEmojis.Length];
                string lastMessage = testMessages[UnityEngine.Random.Range(0, testMessages.Length)];
                int unreadCount = UnityEngine.Random.Range(0, 5);
                DateTime time = DateTime.Now.AddMinutes(-UnityEngine.Random.Range(1, 1440)); // 1분 ~ 24시간 전

                SetupTestConversationItem(item, $"test_user_{i}", username, lastMessage, time, unreadCount, emoji);

                yield return null;
            }
        }

        isGenerated = true;
        isPopulatingConversations = false;
    }

    private void SetupAdminNoticeItem(GameObject item, string message)
    {
        // 아이템 높이 설정 - 프리팹에 LayoutElement가 있으면 그 값 존중
        LayoutElement itemLE = item.GetComponent<LayoutElement>();
        if (itemLE == null)
        {
            itemLE = item.AddComponent<LayoutElement>();
            itemLE.minHeight = conversationItemHeight;
            itemLE.preferredHeight = conversationItemHeight;
        }
        // 프리팹에 이미 설정된 값이 있으면 그대로 사용

        // TitleText
        Text titleText = item.transform.Find("TitleText")?.GetComponent<Text>();
        if (titleText != null)
        {
            titleText.text = "WOOPANG";
            // fontSize는 프리팹 값 사용
        }

        // PreviewText - 영역 크기에 맞게 자동 ellipsis 처리
        Text previewText = item.transform.Find("PreviewText")?.GetComponent<Text>();
        if (previewText != null)
        {
            SetTextWithEllipsis(previewText, message);
        }

        // TimeText
        Text timeText = item.transform.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
        {
            timeText.text = "오전 10:00";
            // fontSize는 프리팹 값 사용
        }

        // Avatar 원형 마스크 적용 (Admin은 자체 아이콘 사용 - 이모지 X)
        Transform avatar = item.transform.Find("Avatar");
        if (avatar != null)
        {
            SetupCircularAvatar(avatar.gameObject);
        }

        // 클릭 이벤트
        Button btn = item.GetComponent<Button>();
        if (btn == null)
            btn = item.AddComponent<Button>();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            OpenTestChatRoom("woopang", "WOOPANG", true);
        });
    }

    private void SetupTestConversationItem(GameObject item, string oderId, string username, string lastMessage, DateTime time, int unreadCount, string avatarEmoji = "")
    {
        if (item == null)
        {
            Debug.LogError("[DMTestData] SetupTestConversationItem: item이 null!");
            return;
        }

        // 아이템 높이 설정 - 프리팹에 LayoutElement가 있으면 그 값 존중
        LayoutElement itemLE = item.GetComponent<LayoutElement>();
        if (itemLE == null)
        {
            itemLE = item.AddComponent<LayoutElement>();
            itemLE.minHeight = conversationItemHeight;
            itemLE.preferredHeight = conversationItemHeight;
        }
        // 프리팹에 이미 설정된 값이 있으면 그대로 사용

        // Content 영역 찾기 - 여러 경로 시도
        Transform content = item.transform.Find("Content");
        if (content == null)
            content = item.transform.Find("Background/Content");
        if (content == null)
            content = item.transform;

        // UsernameText - 여러 경로 시도
        Text usernameText = content.Find("UsernameText")?.GetComponent<Text>();
        if (usernameText == null)
            usernameText = FindTextInChildren(content, "UsernameText");
        if (usernameText == null)
            usernameText = FindTextInChildren(content, "Username");
        if (usernameText == null)
            usernameText = FindTextInChildren(content, "NameText");

        if (usernameText != null)
        {
            usernameText.text = username;
        }

        // PreviewText - 영역 크기에 맞게 자동 ellipsis 처리
        Text previewText = content.Find("PreviewText")?.GetComponent<Text>();
        if (previewText == null)
            previewText = FindTextInChildren(content, "PreviewText");
        if (previewText == null)
            previewText = FindTextInChildren(content, "MessageText");
        if (previewText == null)
            previewText = FindTextInChildren(content, "LastMessage");

        if (previewText != null)
        {
            SetTextWithEllipsis(previewText, lastMessage);
        }

        // TimeText
        Text timeText = content.Find("TimeText")?.GetComponent<Text>();
        if (timeText == null)
            timeText = FindTextInChildren(content, "TimeText");
        if (timeText == null)
            timeText = FindTextInChildren(content, "Time");

        if (timeText != null)
        {
            timeText.text = GetRelativeTime(time);
        }

        // UnreadBadge
        GameObject unreadBadge = content.Find("UnreadBadge")?.gameObject;
        if (unreadBadge == null)
        {
            Transform badge = FindChildRecursive(content, "UnreadBadge");
            if (badge != null) unreadBadge = badge.gameObject;
        }

        if (unreadBadge != null)
        {
            unreadBadge.SetActive(unreadCount > 0);

            Text unreadText = unreadBadge.transform.Find("UnreadCount")?.GetComponent<Text>();
            if (unreadText == null)
                unreadText = unreadBadge.GetComponentInChildren<Text>();

            if (unreadText != null)
            {
                unreadText.text = unreadCount.ToString();
            }
        }

        // Avatar 원형 마스크 적용
        Transform avatar = content.Find("Avatar");
        if (avatar == null)
            avatar = FindChildRecursive(content, "Avatar");
        if (avatar == null)
            avatar = FindChildRecursive(content, "AvatarContainer");

        if (avatar != null)
        {
            SetupCircularAvatar(avatar.gameObject);
            // 이모지 아바타 표시
            if (!string.IsNullOrEmpty(avatarEmoji))
            {
                SetupAvatarEmoji(avatar.gameObject, avatarEmoji);
            }
        }

        // 클릭 이벤트 - 아이템 전체에 추가
        Button btn = item.GetComponent<Button>();
        if (btn == null)
            btn = item.AddComponent<Button>();

        // 버튼 전환 효과 설정
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = colors;

        // 로컬 변수 캡처 (클로저용)
        string capturedEmoji = avatarEmoji;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            OpenTestChatRoom(oderId, username, false, capturedEmoji);
        });
    }

    /// <summary>
    /// 자식에서 Text 컴포넌트 찾기 (재귀)
    /// </summary>
    private Text FindTextInChildren(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name || child.name.Contains(name))
            {
                Text text = child.GetComponent<Text>();
                if (text != null) return text;
            }

            Text found = FindTextInChildren(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 테스트 채팅방 열기
    /// </summary>
    public void OpenTestChatRoom(string oderId, string username, bool isAdmin, string avatarEmoji = "")
    {
        if (messagePanelManager == null) return;

        // ★ 이전 채팅방 코루틴 중지 (레이스 컨디션 방지)
        if (populateMessagesCoroutine != null)
        {
            StopCoroutine(populateMessagesCoroutine);
            populateMessagesCoroutine = null;
        }

        currentChatUserId = oderId;
        currentChatUsername = username;
        currentChatAvatarEmoji = avatarEmoji;

        // ★ 읽지않음 배지 클리어 (채팅방 열면 읽음 처리)
        ClearUnreadBadgeForUser(oderId, username);

        // MessagePanel 닫기 (ChatRoomPanel만 표시)
        if (messagePanelManager.messagePanel != null && messagePanelManager.messagePanel.activeSelf)
        {
            messagePanelManager.messagePanel.SetActive(false);
        }

        // ChatRoomPanel 열기
        if (messagePanelManager.chatRoomPanel != null)
        {
            messagePanelManager.chatRoomPanel.SetActive(true);
        }

        // ★ 타이틀 설정 (사용자명 + 색상) - 유효성 검증 + 자동 재연결 포함
        SetChatTitle(username, isAdmin);

        // InputArea 처리: Admin은 표시하되 비활성화, 일반 DM은 활성화
        SetupChatInputArea(isAdmin);

        // 테스트 메시지 생성 (코루틴 참조 저장)
        populateMessagesCoroutine = StartCoroutine(PopulateTestMessages(oderId, username, isAdmin));
    }

    /// <summary>
    /// 채팅방 타이틀 설정 (사용자명 + 색상)
    /// chatRoomTitle이 null이면 자동으로 찾기 시도
    /// </summary>
    private void SetChatTitle(string username, bool isAdmin)
    {
        if (messagePanelManager == null) return;

        // chatRoomTitle 유효성 검증: null이거나 chatRoomPanel의 자식이 아니면 재연결
        bool needsReconnect = (messagePanelManager.chatRoomTitle == null);
        if (!needsReconnect && messagePanelManager.chatRoomPanel != null && messagePanelManager.chatRoomTitle != null)
        {
            needsReconnect = !messagePanelManager.chatRoomTitle.transform.IsChildOf(messagePanelManager.chatRoomPanel.transform);
        }

        if (needsReconnect && messagePanelManager.chatRoomPanel != null)
        {
            Transform titleTr = messagePanelManager.chatRoomPanel.transform.Find("Background/Header/ChatTitle");
            if (titleTr == null) titleTr = messagePanelManager.chatRoomPanel.transform.Find("Header/ChatTitle");
            if (titleTr == null) titleTr = messagePanelManager.chatRoomPanel.transform.Find("Background/Header/TitleText");
            if (titleTr == null) titleTr = FindChildRecursive(messagePanelManager.chatRoomPanel.transform, "ChatTitle");
            if (titleTr == null) titleTr = FindChildRecursive(messagePanelManager.chatRoomPanel.transform, "TitleText");

            if (titleTr != null)
            {
                messagePanelManager.chatRoomTitle = titleTr.GetComponent<Text>();
            }
        }

        if (messagePanelManager.chatRoomTitle == null) return;

        messagePanelManager.chatRoomTitle.text = username;
        messagePanelManager.chatRoomTitle.color = isAdmin
            ? new Color(1f, 0.84f, 0f, 1f)  // 골드색 (시스템/관리자)
            : Color.white;                   // 흰색 (일반 DM)

        // 강제 캔버스 업데이트 (즉시 반영)
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// ChatInputArea 설정
    /// Admin: InputArea 표시 + 입력 비활성화 + "대화를 할 수 없는 채팅방입니다" placeholder
    /// 일반 DM: InputArea 표시 + 입력 활성화 + "메시지 입력" placeholder
    /// </summary>
    private void SetupChatInputArea(bool isAdmin)
    {
        if (messagePanelManager == null) return;

        // InputArea는 항상 표시 (레이아웃 유지)
        if (messagePanelManager.chatInputArea != null)
        {
            messagePanelManager.chatInputArea.SetActive(true);
        }

        // 입력 필드 활성화/비활성화
        if (messagePanelManager.chatInput != null)
        {
            messagePanelManager.chatInput.interactable = !isAdmin;
            messagePanelManager.chatInput.text = "";

            // placeholder 설정
            if (messagePanelManager.chatInput.placeholder != null)
            {
                Text placeholderText = messagePanelManager.chatInput.placeholder.GetComponent<Text>();
                if (placeholderText != null)
                {
                    if (isAdmin)
                    {
                        placeholderText.text = LocalizationManager.Instance != null
                            ? LocalizationManager.Instance.GetText("chat_readonly_placeholder")
                            : "대화를 할 수 없는 채팅방입니다";
                    }
                    else
                    {
                        placeholderText.text = LocalizationManager.Instance != null
                            ? LocalizationManager.Instance.GetText("message_placeholder")
                            : "메시지 입력";
                    }
                }
            }
        }

        // 전송 버튼 비활성화 (Admin일 때)
        if (messagePanelManager.sendButton != null)
        {
            messagePanelManager.sendButton.interactable = !isAdmin;
        }
    }

    /// <summary>
    /// 메시지 입력 필드 설정
    /// </summary>
    private void SetupMessageInput()
    {
        if (messagePanelManager.chatInput != null)
        {
            messagePanelManager.chatInput.text = "";
            messagePanelManager.chatInput.interactable = true;

            // 엔터키로 전송 설정
            messagePanelManager.chatInput.onEndEdit.RemoveAllListeners();
            messagePanelManager.chatInput.onEndEdit.AddListener((text) =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    SendTestMessage(text);
                }
            });
        }

        // 전송 버튼 설정
        if (messagePanelManager.sendButton != null)
        {
            messagePanelManager.sendButton.onClick.RemoveAllListeners();
            messagePanelManager.sendButton.onClick.AddListener(() =>
            {
                if (messagePanelManager.chatInput != null)
                {
                    SendTestMessage(messagePanelManager.chatInput.text);
                }
            });
        }
    }

    /// <summary>
    /// 테스트 메시지 전송
    /// </summary>
    public void SendTestMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (messagePanelManager == null || messagePanelManager.chatMessageContent == null) return;

        // 내 메시지 생성
        if (messagePanelManager.myMessageBubblePrefab != null)
        {
            GameObject myBubble = Instantiate(messagePanelManager.myMessageBubblePrefab, messagePanelManager.chatMessageContent);
            SetupMessageBubble(myBubble, message, true);
            currentChatMessages.Add(myBubble);

            // 메시지 ID 생성 및 읽음 상태 추적
            string msgId = $"msg_{DateTime.Now.Ticks}";
            messageReadStatus[msgId] = false;

            // 읽음 확인 시뮬레이션 (2-5초 후)
            StartCoroutine(SimulateReadConfirmation(myBubble, msgId));
        }

        // 입력 필드 초기화
        if (messagePanelManager.chatInput != null)
        {
            messagePanelManager.chatInput.text = "";
            messagePanelManager.chatInput.ActivateInputField();
        }

        // 스크롤 맨 아래로
        StartCoroutine(ScrollToBottomDelayed());

        // 자동 응답 시뮬레이션 (1-3초 후)
        StartCoroutine(SimulateAutoReply());

    }

    /// <summary>
    /// 읽음 확인 시뮬레이션
    /// </summary>
    private IEnumerator SimulateReadConfirmation(GameObject messageBubble, string msgId)
    {
        float delay = UnityEngine.Random.Range(2f, 5f);
        yield return new WaitForSeconds(delay);

        if (messageBubble == null) yield break;

        // ReadText 찾아서 업데이트
        Transform timeArea = messageBubble.transform.Find("TimeArea");
        if (timeArea != null)
        {
            Text readText = timeArea.Find("ReadText")?.GetComponent<Text>();
            if (readText != null)
            {
                readText.text = "읽음";
                readText.color = readTextColor; // Inspector에서 조절 가능
            }
        }

        messageReadStatus[msgId] = true;
    }

    /// <summary>
    /// 자동 응답 시뮬레이션
    /// </summary>
    private IEnumerator SimulateAutoReply()
    {
        float delay = UnityEngine.Random.Range(1.5f, 4f);
        yield return new WaitForSeconds(delay);

        if (messagePanelManager == null || messagePanelManager.chatMessageContent == null) yield break;

        // 상대방 메시지 생성
        string replyMessage = autoReplyMessages[UnityEngine.Random.Range(0, autoReplyMessages.Length)];

        if (messagePanelManager.otherMessageBubblePrefab != null)
        {
            GameObject otherBubble = Instantiate(messagePanelManager.otherMessageBubblePrefab, messagePanelManager.chatMessageContent);
            SetupMessageBubble(otherBubble, replyMessage, false);
            currentChatMessages.Add(otherBubble);
        }

        // 스크롤 맨 아래로
        StartCoroutine(ScrollToBottomDelayed());

    }

    private IEnumerator PopulateTestMessages(string oderId, string username, bool isAdmin)
    {
        if (messagePanelManager.chatMessageContent == null) yield break;

        // 기존 메시지 삭제 (안전한 패턴)
        var messagesToDestroy = new List<GameObject>();
        foreach (Transform child in messagePanelManager.chatMessageContent)
            messagesToDestroy.Add(child.gameObject);
        foreach (var msg in messagesToDestroy)
            Destroy(msg);
        currentChatMessages.Clear();

        // 날짜 구분선 추적 초기화
        ResetDateSeparatorTracking();

        yield return null;

        // ★ 타이틀 재확인 (yield 사이에 다른 코드가 덮어쓸 수 있으므로)
        SetChatTitle(username, isAdmin);

        if (isAdmin)
        {
            // 관리자 메시지 (날짜별로 구분)
            DateTime adminTime = DateTime.Now.AddDays(-3); // 3일 전 시작
            foreach (string msg in adminMessages)
            {
                // 날짜 구분선 체크
                CheckAndCreateDateSeparator(adminTime, messagePanelManager.chatMessageContent);

                if (messagePanelManager.adminMessageBubblePrefab != null)
                {
                    GameObject item = Instantiate(messagePanelManager.adminMessageBubblePrefab, messagePanelManager.chatMessageContent);
                    SetupMessageBubble(item, msg, false);
                    currentChatMessages.Add(item);
                }
                else if (messagePanelManager.otherMessageBubblePrefab != null)
                {
                    GameObject item = Instantiate(messagePanelManager.otherMessageBubblePrefab, messagePanelManager.chatMessageContent);
                    SetupMessageBubble(item, msg, false);
                    currentChatMessages.Add(item);
                }

                // 다음 메시지는 1일 후 (24시간 구분선 테스트)
                adminTime = adminTime.AddDays(1);
                yield return null;
            }
        }
        else
        {
            // 일반 대화 - 다양한 길이의 메시지로 UI 테스트
            // 1줄, 2줄, 3줄, 4줄 메시지를 번갈아 배치
            var sampleConversation = new (DateTime time, bool isMine, string content)[]
            {
                // === 1줄 메시지 (짧음) ===
                (DateTime.Now.AddDays(-5).AddHours(14).AddMinutes(30), false, "안녕!"),
                (DateTime.Now.AddDays(-5).AddHours(14).AddMinutes(31), true, "네 ㅎㅎ"),

                // === 1줄 메시지 (중간) ===
                (DateTime.Now.AddDays(-5).AddHours(14).AddMinutes(32), false, "오늘 날씨 좋다~"),
                (DateTime.Now.AddDays(-5).AddHours(14).AddMinutes(33), true, "완전 좋아요! 산책하기 딱이에요"),

                // === 2줄 메시지 ===
                (DateTime.Now.AddDays(-3).AddHours(19).AddMinutes(41), false, "어제 올린 AR 콘텐츠 봤어요.\n정말 멋있었어요!"),
                (DateTime.Now.AddDays(-3).AddHours(19).AddMinutes(45), true, "감사합니다! 시간 많이 들였어요.\n홍대에서 찍었어요 ㅎㅎ"),

                // === 3줄 메시지 ===
                (DateTime.Now.AddDays(-1).AddHours(10).AddMinutes(20), false, "다음에 같이 AR 콘텐츠 만들어볼까요?\n저도 요즘 AR에 관심이 많아서\n배워보고 싶었거든요!"),
                (DateTime.Now.AddDays(-1).AddHours(10).AddMinutes(25), true, "좋아요! 언제 시간 되세요?\n주말에 홍대에서 만나서\n같이 찍어봐요!"),

                // === 4줄 메시지 ===
                (DateTime.Now.AddHours(-2).AddMinutes(15), false, "이번 주말 토요일 오후 2시 어때요?\n홍대입구역 9번 출구에서 만나면 좋을 것 같아요.\n카메라랑 삼각대 가져갈게요!\n기대되네요 ㅎㅎ"),
                (DateTime.Now.AddMinutes(-30), true, "완전 좋아요!! 저도 카메라 챙겨갈게요.\n혹시 조명도 필요하면 말씀해주세요.\n제가 링라이트 있어서 가져갈 수 있어요.\n토요일에 봐요! 😊")
            };

            foreach (var (time, isMine, content) in sampleConversation)
            {
                // 날짜 구분선 체크 (24시간 기준)
                CheckAndCreateDateSeparator(time, messagePanelManager.chatMessageContent);

                GameObject prefab = isMine ? messagePanelManager.myMessageBubblePrefab : messagePanelManager.otherMessageBubblePrefab;
                if (prefab != null)
                {
                    GameObject item = Instantiate(prefab, messagePanelManager.chatMessageContent);
                    SetupMessageBubble(item, content, isMine);
                    currentChatMessages.Add(item);
                }

                yield return null;
            }
        }

        // 스크롤 맨 아래로
        yield return null;
        Canvas.ForceUpdateCanvases();

        ScrollRect scrollRect = messagePanelManager.chatMessageContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.normalizedPosition = Vector2.zero;
        }
    }

    private void SetupMessageBubble(GameObject item, string content, bool isMine)
    {
        // ContentText 찾기 (여러 경로 시도)
        Text contentText = null;

        // BubbleContainer/ContentText
        Transform bubble = item.transform.Find("BubbleContainer");
        if (bubble != null)
        {
            contentText = bubble.Find("ContentText")?.GetComponent<Text>();
        }

        // 직접 ContentText
        if (contentText == null)
        {
            contentText = item.transform.Find("ContentText")?.GetComponent<Text>();
        }

        // 재귀 검색
        if (contentText == null)
        {
            contentText = item.GetComponentInChildren<Text>();
        }

        // 텍스트 폭 계산용 (BubbleContainer 크기 설정에서도 사용)
        float finalTextWidth = 0f;

        if (contentText != null)
        {
            contentText.text = content;
            contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contentText.verticalOverflow = VerticalWrapMode.Overflow;

            // 최대 너비 계산 (Admin 버블은 아바타 공간 고려)
            bool isAdminBubble = item.name.Contains("Admin");
            float screenWidth = Screen.width > 0 ? Screen.width : 1080f;
            float avatarSpace = isAdminBubble ? 120f : 0f; // 아바타(80) + HLG패딩(32) + spacing(8)
            float maxBubbleWidth = messagePanelManager != null ? messagePanelManager.maxBubbleWidthPixels : 800f;
            float maxBubbleRatio = messagePanelManager != null ? messagePanelManager.maxBubbleWidthRatio : 0.82f;
            float maxWidth = Mathf.Min(maxBubbleWidth, screenWidth * maxBubbleRatio) - avatarSpace;
            float bubblePadding = 20f;
            float maxTextWidth = maxWidth - (bubblePadding * 2);
            float minTextWidth = 100f;

            Canvas.ForceUpdateCanvases();
            float actualTextWidth = contentText.preferredWidth;
            finalTextWidth = Mathf.Clamp(actualTextWidth, minTextWidth, maxTextWidth);

            // LayoutElement 설정 - 최대 너비 제한
            LayoutElement le = contentText.GetComponent<LayoutElement>();
            if (le == null)
                le = contentText.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = finalTextWidth;
            le.minWidth = minTextWidth;
            le.flexibleWidth = 0;
            le.minHeight = ChatBubbleLayoutHelper.MIN_BUBBLE_HEIGHT;

            // RectTransform 설정
            RectTransform textRect = contentText.GetComponent<RectTransform>();
            if (textRect != null)
            {
                textRect.anchorMin = new Vector2(0, 1);
                textRect.anchorMax = new Vector2(0, 1);
                textRect.pivot = new Vector2(0, 1);
                textRect.sizeDelta = new Vector2(finalTextWidth, textRect.sizeDelta.y);
            }
        }

        // 버블 자체 LayoutElement - 프리팹에 없을 때만 추가
        LayoutElement itemLE = item.GetComponent<LayoutElement>();
        if (itemLE == null)
        {
            itemLE = item.AddComponent<LayoutElement>();
            itemLE.minHeight = ChatBubbleLayoutHelper.MIN_BUBBLE_HEIGHT;
        }
        // 프리팹에 이미 있으면 그 값 사용

        // TimeText / TimeArea 처리
        Text timeText = null;
        Transform timeArea = item.transform.Find("TimeArea");
        if (timeArea != null)
        {
            // 날짜 구분선 사용 시 TimeArea 숨김
            if (useDateSeparator)
            {
                timeArea.gameObject.SetActive(false);
            }
            else
            {
                timeText = timeArea.Find("TimeText")?.GetComponent<Text>();
            }
        }
        if (timeText == null && !useDateSeparator)
        {
            timeText = item.transform.Find("TimeText")?.GetComponent<Text>();
        }

        if (timeText != null)
        {
            DateTime now = DateTime.Now.AddMinutes(-UnityEngine.Random.Range(1, 60));
            timeText.text = GetShortTime(now);
            timeText.fontSize = timeFontSize; // Inspector에서 조절 가능
        }

        // ReadText (내 메시지만)
        if (isMine && timeArea != null)
        {
            Text readText = timeArea.Find("ReadText")?.GetComponent<Text>();
            if (readText != null)
            {
                readText.text = UnityEngine.Random.value > 0.3f ? "읽음" : "";
                readText.fontSize = readFontSize; // Inspector에서 조절 가능
            }
        }

        // 아바타 처리 - 내 메시지는 아바타 없음, 상대방 메시지는 아바타 표시
        Transform avatarTr = item.transform.Find("AvatarContainer");
        if (avatarTr == null)
            avatarTr = item.transform.Find("Avatar");
        if (avatarTr != null)
        {
            if (isMine)
            {
                // 내 메시지: 아바타 컨테이너 비활성화
                avatarTr.gameObject.SetActive(false);
            }
            else
            {
                // 상대방 메시지: 아바타 표시 + 이모지
                avatarTr.gameObject.SetActive(true);
                SetupCircularAvatar(avatarTr.gameObject);
                if (!string.IsNullOrEmpty(currentChatAvatarEmoji))
                {
                    SetupAvatarEmoji(avatarTr.gameObject, currentChatAvatarEmoji);
                }
            }
        }

        // LabelText 비활성화 (시스템 버블에서도 사용 안함 - ChatTitle로 대체)
        Transform labelText = item.transform.Find("BubbleContainer/LabelText");
        if (labelText != null)
            labelText.gameObject.SetActive(false);

        // ============================================================
        // BubbleContainer 동적 크기 계산 (CSF 대체)
        // CSF가 HLG 자식일 때 높이를 잘못 계산하는 Unity 한계 우회
        // 텍스트 폭에 맞게 가로/세로 모두 동적으로 조절
        // ============================================================
        Transform bubbleContainerTr = item.transform.Find("BubbleContainer");
        if (bubbleContainerTr != null && contentText != null && finalTextWidth > 0f)
        {
            // CSF 비활성화 (코드에서 직접 크기 설정)
            ContentSizeFitter bubbleCSF = bubbleContainerTr.GetComponent<ContentSizeFitter>();
            if (bubbleCSF != null)
                bubbleCSF.enabled = false;

            // VLG padding 가져오기
            VerticalLayoutGroup vlg = bubbleContainerTr.GetComponent<VerticalLayoutGroup>();
            float padL = vlg != null ? vlg.padding.left : 16f;
            float padR = vlg != null ? vlg.padding.right : 16f;
            float padT = vlg != null ? vlg.padding.top : 8f;
            float padB = vlg != null ? vlg.padding.bottom : 8f;

            // 텍스트 높이를 올바른 폭 기준으로 계산
            // GetGenerationSettings가 scaleFactor = pixelsPerUnit로 자동 설정
            // (에디터: pixelsPerUnit=1, 런타임: Canvas scaleFactor 반영)
            TextGenerationSettings settings = contentText.GetGenerationSettings(
                new Vector2(finalTextWidth, 0f));
            float textHeight = contentText.cachedTextGenerator.GetPreferredHeight(
                contentText.text, settings) / contentText.pixelsPerUnit;

            // BubbleContainer 크기 = 텍스트 + 패딩
            float bubbleW = finalTextWidth + padL + padR;
            float bubbleH = textHeight + padT + padB;

            // LayoutElement로 크기 전달 (Root CSF가 참조하여 Root 높이 결정)
            LayoutElement bubbleLE = bubbleContainerTr.GetComponent<LayoutElement>();
            if (bubbleLE == null)
                bubbleLE = bubbleContainerTr.gameObject.AddComponent<LayoutElement>();
            bubbleLE.preferredWidth = bubbleW;
            bubbleLE.preferredHeight = bubbleH;

            // RectTransform 직접 설정 (즉시 반영)
            RectTransform bubbleRect = bubbleContainerTr.GetComponent<RectTransform>();
            if (bubbleRect != null)
                bubbleRect.sizeDelta = new Vector2(bubbleW, bubbleH);
        }

        // Root 레이아웃 재빌드 (Root CSF가 BubbleContainer LE 기반으로 높이 결정)
        Canvas.ForceUpdateCanvases();
        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(itemRect);
    }

    /// <summary>
    /// 스크롤 맨 아래로 (딜레이)
    /// </summary>
    private IEnumerator ScrollToBottomDelayed()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (messagePanelManager?.chatMessageContent != null)
        {
            ScrollRect scrollRect = messagePanelManager.chatMessageContent.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.normalizedPosition = Vector2.zero;
            }
        }
    }

    #region 자동 메시지 시뮬레이션

    /// <summary>
    /// 자동 메시지 시뮬레이션 시작
    /// 10분간 12-15개 메시지를 랜덤 간격으로 수신
    /// </summary>
    public void StartAutoMessageSimulation()
    {
        if (isSimulationRunning)
        {
            return;
        }

        simulationCoroutine = StartCoroutine(AutoMessageSimulationRoutine());
    }

    /// <summary>
    /// 자동 메시지 시뮬레이션 중지
    /// </summary>
    public void StopAutoMessageSimulation()
    {
        if (simulationCoroutine != null)
        {
            StopCoroutine(simulationCoroutine);
            simulationCoroutine = null;
        }

        isSimulationRunning = false;
    }

    /// <summary>
    /// 자동 메시지 시뮬레이션 코루틴
    /// </summary>
    private IEnumerator AutoMessageSimulationRoutine()
    {
        isSimulationRunning = true;

        // 평균 간격 계산 (약간의 랜덤 변동)
        float averageInterval = simulationDuration / totalMessages;
        int messagesSent = 0;

        while (messagesSent < totalMessages && isSimulationRunning)
        {
            // 랜덤 대기 (평균 간격의 0.5~1.5배)
            float randomInterval = averageInterval * UnityEngine.Random.Range(0.5f, 1.5f);
            yield return new WaitForSeconds(randomInterval);

            if (!isSimulationRunning) break;

            // 랜덤 사용자 선택
            int userIndex = UnityEngine.Random.Range(0, testUsernames.Length);
            string senderName = testUsernames[userIndex];
            string senderId = $"test_user_{userIndex}";

            // 랜덤 메시지 선택
            string[] allMessages = new string[testMessages.Length + autoReplyMessages.Length];
            testMessages.CopyTo(allMessages, 0);
            autoReplyMessages.CopyTo(allMessages, testMessages.Length);
            string message = allMessages[UnityEngine.Random.Range(0, allMessages.Length)];

            // 현재 열린 채팅방이면 메시지 직접 추가
            if (currentChatUserId == senderId && messagePanelManager?.chatMessageContent != null)
            {
                AddIncomingMessage(message);
            }

            // 대화 목록 업데이트
            UpdateConversationListPreview(senderId, senderName, message);

            messagesSent++;
        }

        isSimulationRunning = false;
    }

    /// <summary>
    /// 수신 메시지 추가 (현재 열린 채팅방)
    /// </summary>
    private void AddIncomingMessage(string message)
    {
        if (messagePanelManager?.otherMessageBubblePrefab != null && messagePanelManager.chatMessageContent != null)
        {
            GameObject bubble = Instantiate(messagePanelManager.otherMessageBubblePrefab, messagePanelManager.chatMessageContent);
            SetupMessageBubble(bubble, message, false);
            currentChatMessages.Add(bubble);

            StartCoroutine(ScrollToBottomDelayed());
        }
    }

    /// <summary>
    /// 대화 목록 미리보기 업데이트
    /// </summary>
    private void UpdateConversationListPreview(string userId, string username, string message)
    {
        if (messagePanelManager?.conversationListContent == null) return;

        foreach (Transform item in messagePanelManager.conversationListContent)
        {
            Transform content = item.Find("Content") ?? item;

            Text usernameText = content.Find("UsernameText")?.GetComponent<Text>();
            if (usernameText != null && usernameText.text == username)
            {
                // 미리보기 업데이트
                Text previewText = content.Find("PreviewText")?.GetComponent<Text>();
                if (previewText != null)
                {
                    string preview = message.Length > 30 ? message.Substring(0, 30) + "..." : message;
                    previewText.text = preview;
                }

                // 시간 업데이트
                Text timeText = content.Find("TimeText")?.GetComponent<Text>();
                if (timeText != null)
                {
                    timeText.text = "방금";
                }

                // 읽지 않은 메시지 배지 업데이트
                if (currentChatUserId != userId) // 현재 열린 채팅방이 아니면
                {
                    GameObject unreadBadge = content.Find("UnreadBadge")?.gameObject;
                    if (unreadBadge != null)
                    {
                        unreadBadge.SetActive(true);

                        Text unreadText = unreadBadge.transform.Find("UnreadCount")?.GetComponent<Text>();
                        if (unreadText != null)
                        {
                            int currentCount = 0;
                            int.TryParse(unreadText.text, out currentCount);
                            unreadText.text = (currentCount + 1).ToString();
                        }
                    }
                }

                // 목록 최상단으로 이동
                item.SetAsFirstSibling();
                break;
            }
        }
    }

    /// <summary>
    /// 채팅방 입장 시 해당 대화의 읽지않음 배지 클리어
    /// </summary>
    private void ClearUnreadBadgeForUser(string userId, string username)
    {
        if (messagePanelManager?.conversationListContent == null) return;

        foreach (Transform item in messagePanelManager.conversationListContent)
        {
            // Content 영역 찾기
            Transform content = item.Find("Content") ?? item;

            // UsernameText로 대화 아이템 식별
            Text usernameText = content.Find("UsernameText")?.GetComponent<Text>();
            if (usernameText == null)
                usernameText = FindTextInChildren(content, "UsernameText");
            if (usernameText == null)
                usernameText = FindTextInChildren(content, "Username");

            // WOOPANG (AdminNotice)의 경우 TitleText로 식별
            if (usernameText == null)
            {
                Text titleText = item.transform.Find("TitleText")?.GetComponent<Text>();
                if (titleText != null && titleText.text == username)
                {
                    ClearUnreadBadgeOnItem(item, content);
                    return;
                }
            }

            // 일반 대화 아이템
            if (usernameText != null && usernameText.text == username)
            {
                ClearUnreadBadgeOnItem(item, content);
                return;
            }
        }

    }

    /// <summary>
    /// 대화 아이템의 읽지않음 배지 숨기기
    /// </summary>
    private void ClearUnreadBadgeOnItem(Transform item, Transform content)
    {
        // UnreadBadge 찾기 (여러 경로 시도)
        GameObject unreadBadge = content.Find("UnreadBadge")?.gameObject;
        if (unreadBadge == null)
        {
            Transform badge = FindChildRecursive(content, "UnreadBadge");
            if (badge != null) unreadBadge = badge.gameObject;
        }
        if (unreadBadge == null)
        {
            Transform badge = FindChildRecursive(item, "UnreadBadge");
            if (badge != null) unreadBadge = badge.gameObject;
        }

        if (unreadBadge != null)
        {
            unreadBadge.SetActive(false);

            // UnreadCount 텍스트도 초기화
            Text unreadText = unreadBadge.transform.Find("UnreadCount")?.GetComponent<Text>();
            if (unreadText == null)
                unreadText = unreadBadge.GetComponentInChildren<Text>();
            if (unreadText != null)
                unreadText.text = "0";
        }
    }

    #endregion

    private string GetRelativeTime(DateTime date)
    {
        TimeSpan diff = DateTime.Now - date;

        if (diff.TotalMinutes < 1) return "방금";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}분 전";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}시간 전";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}일 전";
        return date.ToString("M월 d일");
    }

    private string GetShortTime(DateTime date)
    {
        bool isAM = date.Hour < 12;
        int hour12 = date.Hour % 12;
        if (hour12 == 0) hour12 = 12;
        return $"{(isAM ? "오전" : "오후")} {hour12}:{date.Minute:D2}";
    }

    /// <summary>
    /// Content 영역 레이아웃 설정 - 메시지 아이템이 적절한 크기로 표시되도록 함
    /// 이미 설정된 VerticalLayoutGroup이 있으면 그 값을 존중함
    /// </summary>
    private void SetupContentLayout(Transform content)
    {
        if (content == null) return;

        RectTransform rectTransform = content.GetComponent<RectTransform>();
        if (rectTransform == null) return;

        // VerticalLayoutGroup 설정 - 이미 있으면 그 값 존중
        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            // VLG가 없을 때만 추가하고 기본값 설정
            vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = contentSpacing; // Inspector에서 조절 가능
            vlg.padding = new RectOffset(contentPadding, contentPadding, contentPadding, contentPadding); // Inspector에서 조절 가능
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
        }
        // 이미 VLG가 있으면 기존 설정 유지

        // ContentSizeFitter 설정 - 이미 있으면 그 값 존중
        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        // 이미 CSF가 있으면 기존 설정 유지

        // 앵커 설정은 건드리지 않음 - 씬/프리팹에서 설정한 값 존중
    }

    /// <summary>
    /// 프리팹 참조가 없으면 Resources 또는 AssetDatabase에서 로드
    /// </summary>
    private void LoadPrefabsIfNeeded()
    {
        if (messagePanelManager == null) return;

#if UNITY_EDITOR
        // 에디터에서는 AssetDatabase로 로드
        if (messagePanelManager.conversationItemPrefab == null)
        {
            messagePanelManager.conversationItemPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DM/ConversationItem.prefab");
            if (messagePanelManager.conversationItemPrefab == null)
                Debug.LogError("[DMTestData] ConversationItem 프리팹 로드 실패! Assets/Prefabs/DM/ConversationItem.prefab 확인 필요");
        }

        if (messagePanelManager.adminNoticePrefab == null)
        {
            messagePanelManager.adminNoticePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DM/AdminNoticeItem.prefab");
            if (messagePanelManager.adminNoticePrefab == null)
                Debug.LogError("[DMTestData] AdminNoticeItem 프리팹 로드 실패!");
        }

        if (messagePanelManager.myMessageBubblePrefab == null)
        {
            messagePanelManager.myMessageBubblePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DM/MyMessageBubble.prefab");
        }

        if (messagePanelManager.otherMessageBubblePrefab == null)
        {
            messagePanelManager.otherMessageBubblePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DM/OtherMessageBubble.prefab");
        }

        if (messagePanelManager.adminMessageBubblePrefab == null)
        {
            messagePanelManager.adminMessageBubblePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DM/AdminMessageBubble.prefab");
        }
#endif
    }

    /// <summary>
    /// conversationListContent를 자동으로 찾기 시도
    /// </summary>
    private void TryFindConversationListContent()
    {
        if (messagePanelManager == null || messagePanelManager.messagePanel == null) return;

        // MessagePanel 하위에서 Content 찾기
        Transform panel = messagePanelManager.messagePanel.transform;

        // 일반적인 경로들 시도
        string[] contentPaths = new string[]
        {
            "ConversationList/Viewport/Content",
            "Scroll View/Viewport/Content",
            "ScrollView/Viewport/Content",
            "Viewport/Content",
            "Content"
        };

        foreach (string path in contentPaths)
        {
            Transform found = panel.Find(path);
            if (found != null)
            {
                messagePanelManager.conversationListContent = found;
                return;
            }
        }

        // 재귀적으로 "Content" 이름을 가진 Transform 찾기
        Transform content = FindChildRecursive(panel, "Content");
        if (content != null)
        {
            messagePanelManager.conversationListContent = content;
        }
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 아바타를 원형으로 표시 (마스크 적용)
    /// MiniProfile의 AvatarMask 구조 참고
    /// </summary>
    private void SetupCircularAvatar(GameObject avatarObj)
    {
        if (avatarObj == null) return;

        Image avatarImage = avatarObj.GetComponent<Image>();
        if (avatarImage == null) return;

        // 1. Mask 컴포넌트 추가 (없으면)
        Mask mask = avatarObj.GetComponent<Mask>();
        if (mask == null)
        {
            mask = avatarObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;  // 마스크 그래픽 숨김
        }

        // 2. 원형 스프라이트 설정 (Unity 내장 Knob 스프라이트 사용)
        // Knob은 원형이라 마스크로 적합
        if (avatarImage.sprite == null)
        {
            // Resources에서 원형 스프라이트 로드 시도
            Sprite circleSprite = Resources.Load<Sprite>("CircleMask");
            if (circleSprite != null)
            {
                avatarImage.sprite = circleSprite;
            }
            else
            {
                // 없으면 Unity 내장 Knob 사용 (원형)
                avatarImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            }
        }

        // 3. 이미지 타입을 Simple로 설정
        avatarImage.type = Image.Type.Simple;
        avatarImage.preserveAspect = true;

        // 4. 자식에 실제 아바타 이미지 추가 (없으면)
        // 프리팹에서는 "AvatarImage", 런타임 생성시에는 "AvatarContent"
        Transform avatarContent = avatarObj.transform.Find("AvatarImage");
        if (avatarContent == null)
            avatarContent = avatarObj.transform.Find("AvatarContent");

        if (avatarContent == null)
        {
            GameObject contentObj = new GameObject("AvatarImage");
            contentObj.transform.SetParent(avatarObj.transform, false);

            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            Image contentImage = contentObj.AddComponent<Image>();
            contentImage.color = defaultAvatarColor;  // Inspector에서 조절 가능
            contentImage.raycastTarget = false;

            avatarContent = contentObj.transform;
        }

        // 마스크가 적용되면 자식 이미지가 원형으로 클리핑됨
    }

    /// <summary>
    /// 아바타에 이모지 텍스트 표시
    /// 원형 아바타 위에 이모지를 중앙 정렬로 표시
    /// </summary>
    private void SetupAvatarEmoji(GameObject avatarObj, string emoji)
    {
        if (avatarObj == null || string.IsNullOrEmpty(emoji)) return;

        // 기존 EmojiText 찾기 또는 새로 생성
        Transform emojiTr = avatarObj.transform.Find("EmojiText");
        Text emojiText;

        if (emojiTr == null)
        {
            GameObject emojiObj = new GameObject("EmojiText");
            emojiObj.transform.SetParent(avatarObj.transform, false);

            RectTransform rect = emojiObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            emojiText = emojiObj.AddComponent<Text>();
            emojiText.alignment = TextAnchor.MiddleCenter;
            emojiText.raycastTarget = false;
            emojiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            emojiText.verticalOverflow = VerticalWrapMode.Overflow;

            // Arial 사용 (모바일에서 시스템 이모지 폰트 fallback 지원)
            emojiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        else
        {
            emojiText = emojiTr.GetComponent<Text>();
        }

        if (emojiText != null)
        {
            emojiText.text = emoji;

            // 아바타 크기에 맞게 폰트 크기 자동 조절
            RectTransform avatarRect = avatarObj.GetComponent<RectTransform>();
            float avatarSize = avatarRect != null
                ? Mathf.Min(avatarRect.rect.width, avatarRect.rect.height)
                : 80f;
            // 아바타의 약 60% 크기로 이모지 표시
            emojiText.fontSize = Mathf.Max(20, Mathf.RoundToInt(avatarSize * 0.6f));
        }
    }

    /// <summary>
    /// 텍스트가 영역을 넘어가면 "..."으로 잘라서 표시
    /// 코루틴으로 다음 프레임에 처리하여 레이아웃 계산 완료 후 적용
    /// </summary>
    private void SetTextWithEllipsis(Text textComponent, string fullText)
    {
        if (textComponent == null) return;

        // 일단 전체 텍스트 설정
        textComponent.text = fullText ?? "";

        if (string.IsNullOrEmpty(fullText)) return;

        // 레이아웃 계산 후 ellipsis 적용
        StartCoroutine(ApplyEllipsisNextFrame(textComponent, fullText));
    }

    private IEnumerator ApplyEllipsisNextFrame(Text textComponent, string fullText)
    {
        // 레이아웃 업데이트 대기
        yield return null;

        if (textComponent == null) yield break;

        RectTransform rt = textComponent.rectTransform;
        float availableWidth = rt.rect.width;

        if (availableWidth <= 0) yield break;

        // TextGenerator로 실제 필요한 너비 계산
        TextGenerator generator = textComponent.cachedTextGenerator;
        TextGenerationSettings settings = textComponent.GetGenerationSettings(rt.rect.size);

        float preferredWidth = generator.GetPreferredWidth(fullText, settings);

        if (preferredWidth <= availableWidth)
        {
            // 오버플로우 없음
            yield break;
        }

        // 오버플로우 - 이진 탐색으로 최적 길이 찾기
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

    #region 날짜 구분선

    /// <summary>
    /// 날짜 구분선 생성 (24시간 이상 차이날 때 표시)
    /// </summary>
    private void CreateDateSeparator(DateTime messageTime, Transform parent)
    {
        if (parent == null || !useDateSeparator) return;

        // 날짜 구분선 컨테이너 생성
        GameObject separator = new GameObject("DateSeparator");
        separator.transform.SetParent(parent, false);

        RectTransform separatorRect = separator.AddComponent<RectTransform>();
        separatorRect.anchorMin = new Vector2(0, 1);
        separatorRect.anchorMax = new Vector2(1, 1);
        separatorRect.pivot = new Vector2(0.5f, 1);

        // LayoutElement 추가
        LayoutElement separatorLE = separator.AddComponent<LayoutElement>();
        separatorLE.flexibleWidth = 1;
        separatorLE.preferredHeight = dateSeparatorFontSize + (dateSeparatorMargin * 2);

        // 텍스트 생성
        GameObject textObj = new GameObject("DateText");
        textObj.transform.SetParent(separator.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text dateText = textObj.AddComponent<Text>();
        dateText.text = FormatDateSeparator(messageTime);
        dateText.fontSize = dateSeparatorFontSize;
        dateText.color = dateSeparatorColor;
        dateText.alignment = TextAnchor.MiddleCenter;

        // 폰트 로드
        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (customFont != null)
            dateText.font = customFont;
    }

    /// <summary>
    /// 날짜 구분선 형식 지정 - 디바이스 언어에 따라 다국어 지원
    /// </summary>
    private string FormatDateSeparator(DateTime dateTime)
    {
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
    private void CheckAndCreateDateSeparator(DateTime messageTime, Transform parent)
    {
        if (!useDateSeparator) return;

        // 24시간 이상 차이나면 날짜 구분선 생성
        if (lastMessageTime == DateTime.MinValue ||
            (messageTime - lastMessageTime).TotalHours >= 24)
        {
            CreateDateSeparator(messageTime, parent);
        }

        lastMessageTime = messageTime;
    }

    /// <summary>
    /// 날짜 구분선 추적 초기화
    /// </summary>
    private void ResetDateSeparatorTracking()
    {
        lastMessageTime = DateTime.MinValue;
    }

    #endregion
}
