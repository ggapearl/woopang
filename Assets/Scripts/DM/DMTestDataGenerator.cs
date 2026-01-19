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
    [Tooltip("Play 모드 시작 시 자동으로 테스트 데이터 생성")]
    public bool autoGenerateOnStart = true;

    [Tooltip("테스트 대화 수")]
    [Range(1, 10)]
    public int conversationCount = 5;

    [Header("테스트 버튼")]
    [Tooltip("런타임에 테스트 패널 열기 버튼 표시")]
    public bool showTestButton = true;
    public KeyCode openPanelKey = KeyCode.M;

    [Header("자동 메시지 시뮬레이션")]
    [Tooltip("자동 메시지 시뮬레이션 활성화")]
    public bool enableAutoMessages = false;

    [Tooltip("시뮬레이션 지속 시간 (초)")]
    public float simulationDuration = 600f; // 10분

    [Tooltip("총 메시지 수")]
    [Range(10, 20)]
    public int totalMessages = 13;

    [Tooltip("시뮬레이션 시작 시 알림 표시")]
    public bool showNotifications = true;

    private MessagePanelManager messagePanelManager;
    private DMNotificationManager notificationManager;
    private bool isGenerated = false;
    private bool isSimulationRunning = false;
    private Coroutine simulationCoroutine;

    // 현재 열린 채팅방 정보
    private string currentChatUserId = "";
    private string currentChatUsername = "";

    // 읽음 확인 추적
    private Dictionary<string, bool> messageReadStatus = new Dictionary<string, bool>();
    private List<GameObject> currentChatMessages = new List<GameObject>();

    // 테스트 사용자 데이터
    private readonly string[] testUsernames = new string[]
    {
        "김민지", "이준호", "박서연", "최영수", "정하늘",
        "WOOPANG_Official", "AR_Master", "여행러버", "맛집탐험가", "사진작가"
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
        "WOOPANG에 오신 것을 환영합니다! 새로운 AR 세상을 경험해보세요.",
        "새로운 업데이트가 있습니다. 지금 확인해보세요!",
        "이번 주 인기 AR 콘텐츠를 소개합니다.",
        "피드백을 남겨주시면 서비스 개선에 반영됩니다."
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
        messagePanelManager = GetComponent<MessagePanelManager>();
        if (messagePanelManager == null)
            messagePanelManager = FindFirstObjectByType<MessagePanelManager>();

        // 알림 매니저 찾기 또는 생성
        notificationManager = FindFirstObjectByType<DMNotificationManager>();
        if (notificationManager == null)
        {
            GameObject notifObj = new GameObject("DMNotificationManager");
            notificationManager = notifObj.AddComponent<DMNotificationManager>();
        }

        // 에디터에서 자동 시작
#if UNITY_EDITOR
        if (autoGenerateOnStart && messagePanelManager != null)
        {
            StartCoroutine(AutoStartInEditor());
        }
#else
        if (autoGenerateOnStart && messagePanelManager != null)
        {
            StartCoroutine(GenerateTestDataDelayed());
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 Play 모드 시작 시 자동 실행
    /// </summary>
    private IEnumerator AutoStartInEditor()
    {
        yield return new WaitForSeconds(0.5f);

        // 1. 메시지 패널 자동 열기
        OpenTestMessagePanel();
        Debug.Log("[DMTestData] 에디터: 메시지 패널 자동 열기");

        yield return new WaitForSeconds(1f);

        // 2. 자동 메시지 시뮬레이션 시작
        StartAutoMessageSimulation();
        Debug.Log("[DMTestData] 에디터: 자동 메시지 시뮬레이션 시작");
    }
#endif

    void Update()
    {
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
                    if (!isGenerated)
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
    void OnGUI()
    {
        if (!showTestButton) return;

        // 화면 우측 상단에 테스트 버튼 표시
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 14;
        buttonStyle.fontStyle = FontStyle.Bold;

        float buttonWidth = 180;
        float buttonHeight = 40;
        float margin = 10;

        // 메시지 패널 열기 버튼
        Rect buttonRect = new Rect(
            Screen.width - buttonWidth - margin,
            margin,
            buttonWidth,
            buttonHeight
        );

        if (GUI.Button(buttonRect, "메시지 패널 열기 (M)", buttonStyle))
        {
            OpenTestMessagePanel();
        }

        // 테스트 대화 생성 버튼
        Rect generateRect = new Rect(
            Screen.width - buttonWidth - margin,
            margin + buttonHeight + 5,
            buttonWidth,
            buttonHeight
        );

        if (GUI.Button(generateRect, "테스트 대화 생성", buttonStyle))
        {
            StartCoroutine(PopulateTestConversations());
        }

        // 자동 메시지 시뮬레이션 버튼
        Rect simRect = new Rect(
            Screen.width - buttonWidth - margin,
            margin + (buttonHeight + 5) * 2,
            buttonWidth,
            buttonHeight
        );

        string simButtonText = isSimulationRunning ? "시뮬레이션 중지" : "자동 메시지 시작 (Shift+S)";
        GUI.backgroundColor = isSimulationRunning ? Color.red : Color.green;

        if (GUI.Button(simRect, simButtonText, buttonStyle))
        {
            if (isSimulationRunning)
                StopAutoMessageSimulation();
            else
                StartAutoMessageSimulation();
        }

        GUI.backgroundColor = Color.white;

        // 시뮬레이션 상태 표시
        if (isSimulationRunning)
        {
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.yellow;

            Rect statusRect = new Rect(
                Screen.width - buttonWidth - margin,
                margin + (buttonHeight + 5) * 3,
                buttonWidth,
                30
            );

            GUI.Label(statusRect, $"메시지 수신 시뮬레이션 진행 중...", labelStyle);
        }
    }
#endif

    private IEnumerator GenerateTestDataDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        if (!isGenerated)
        {
            Debug.Log("[DMTestData] 테스트 데이터 자동 생성 준비 완료");
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

            if (!isGenerated)
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
        if (messagePanelManager == null || messagePanelManager.conversationListContent == null)
        {
            Debug.LogError("[DMTestData] conversationListContent가 null입니다!");
            yield break;
        }

        // 기존 항목 삭제 (안전한 패턴)
        var childrenToDestroy = new List<GameObject>();
        foreach (Transform child in messagePanelManager.conversationListContent)
            childrenToDestroy.Add(child.gameObject);
        foreach (var child in childrenToDestroy)
            Destroy(child);

        yield return null;

        // 1. WOOPANG 공지 (최상단)
        if (messagePanelManager.adminNoticePrefab != null)
        {
            GameObject adminItem = Instantiate(messagePanelManager.adminNoticePrefab, messagePanelManager.conversationListContent);
            SetupAdminNoticeItem(adminItem, adminMessages[UnityEngine.Random.Range(0, adminMessages.Length)]);
        }

        yield return null;

        // 2. 테스트 대화 생성
        for (int i = 0; i < conversationCount; i++)
        {
            if (messagePanelManager.conversationItemPrefab == null) continue;

            GameObject item = Instantiate(messagePanelManager.conversationItemPrefab, messagePanelManager.conversationListContent);

            string username = testUsernames[i % testUsernames.Length];
            string lastMessage = testMessages[UnityEngine.Random.Range(0, testMessages.Length)];
            int unreadCount = UnityEngine.Random.Range(0, 5);
            DateTime time = DateTime.Now.AddMinutes(-UnityEngine.Random.Range(1, 1440)); // 1분 ~ 24시간 전

            SetupTestConversationItem(item, $"test_user_{i}", username, lastMessage, time, unreadCount);

            yield return null;
        }

        isGenerated = true;
        Debug.Log($"[DMTestData] 테스트 대화 {conversationCount}개 생성 완료");
    }

    private void SetupAdminNoticeItem(GameObject item, string message)
    {
        // TitleText
        Text titleText = item.transform.Find("TitleText")?.GetComponent<Text>();
        if (titleText != null)
            titleText.text = "WOOPANG";

        // PreviewText
        Text previewText = item.transform.Find("PreviewText")?.GetComponent<Text>();
        if (previewText != null)
        {
            string preview = message.Length > 30 ? message.Substring(0, 30) + "..." : message;
            previewText.text = preview;
        }

        // TimeText
        Text timeText = item.transform.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
            timeText.text = "오전 10:00";

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

    private void SetupTestConversationItem(GameObject item, string oderId, string username, string lastMessage, DateTime time, int unreadCount)
    {
        // Content 영역 찾기
        Transform content = item.transform.Find("Content");
        if (content == null)
            content = item.transform;

        // UsernameText
        Text usernameText = content.Find("UsernameText")?.GetComponent<Text>();
        if (usernameText != null)
            usernameText.text = username;

        // PreviewText
        Text previewText = content.Find("PreviewText")?.GetComponent<Text>();
        if (previewText != null)
        {
            string preview = lastMessage.Length > 30 ? lastMessage.Substring(0, 30) + "..." : lastMessage;
            previewText.text = preview;
        }

        // TimeText
        Text timeText = content.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
            timeText.text = GetRelativeTime(time);

        // UnreadBadge
        GameObject unreadBadge = content.Find("UnreadBadge")?.gameObject;
        if (unreadBadge != null)
        {
            unreadBadge.SetActive(unreadCount > 0);

            Text unreadText = unreadBadge.transform.Find("UnreadCount")?.GetComponent<Text>();
            if (unreadText != null)
                unreadText.text = unreadCount.ToString();
        }

        // 클릭 이벤트
        Button btn = content.GetComponent<Button>();
        if (btn == null)
            btn = content.gameObject.AddComponent<Button>();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            OpenTestChatRoom(oderId, username, false);
        });
    }

    /// <summary>
    /// 테스트 채팅방 열기
    /// </summary>
    public void OpenTestChatRoom(string oderId, string username, bool isAdmin)
    {
        if (messagePanelManager == null) return;

        currentChatUserId = oderId;
        currentChatUsername = username;

        // ChatRoomPanel 열기
        if (messagePanelManager.chatRoomPanel != null)
        {
            messagePanelManager.chatRoomPanel.SetActive(true);
        }

        // 타이틀 설정
        if (messagePanelManager.chatRoomTitle != null)
        {
            messagePanelManager.chatRoomTitle.text = username;
        }

        // 메시지 입력 필드 설정
        SetupMessageInput();

        // 테스트 메시지 생성
        StartCoroutine(PopulateTestMessages(oderId, username, isAdmin));
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

        Debug.Log($"[DMTestData] 메시지 전송: {message}");
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
                readText.color = new Color(0.5f, 0.8f, 1f); // 파란색 톤
            }
        }

        messageReadStatus[msgId] = true;
        Debug.Log($"[DMTestData] 메시지 읽음 확인: {msgId}");
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

        Debug.Log($"[DMTestData] 자동 응답: {replyMessage}");
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

        yield return null;

        if (isAdmin)
        {
            // 관리자 메시지
            foreach (string msg in adminMessages)
            {
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
                yield return null;
            }
        }
        else
        {
            // 일반 대화 (번갈아가며)
            string[] sampleConversation = new string[]
            {
                "other:안녕하세요!",
                "mine:네, 안녕하세요 :)",
                "other:어제 올린 AR 콘텐츠 봤어요. 정말 멋있었어요!",
                "mine:감사합니다! 시간 많이 들였어요",
                "other:어디서 찍으신 건가요?",
                "mine:홍대 거리에서요. 다음에 같이 가실래요?",
                "other:좋아요! 언제가 좋을까요?"
            };

            foreach (string line in sampleConversation)
            {
                bool isMine = line.StartsWith("mine:");
                string content = line.Substring(line.IndexOf(':') + 1);

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

        if (contentText != null)
        {
            contentText.text = content;
        }

        // TimeText
        Text timeText = null;
        Transform timeArea = item.transform.Find("TimeArea");
        if (timeArea != null)
        {
            timeText = timeArea.Find("TimeText")?.GetComponent<Text>();
        }
        if (timeText == null)
        {
            timeText = item.transform.Find("TimeText")?.GetComponent<Text>();
        }

        if (timeText != null)
        {
            DateTime now = DateTime.Now.AddMinutes(-UnityEngine.Random.Range(1, 60));
            timeText.text = GetShortTime(now);
        }

        // ReadText (내 메시지만)
        if (isMine && timeArea != null)
        {
            Text readText = timeArea.Find("ReadText")?.GetComponent<Text>();
            if (readText != null)
            {
                readText.text = UnityEngine.Random.value > 0.3f ? "읽음" : "";
            }
        }
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
            Debug.LogWarning("[DMTestData] 시뮬레이션이 이미 실행 중입니다.");
            return;
        }

        simulationCoroutine = StartCoroutine(AutoMessageSimulationRoutine());
        Debug.Log($"[DMTestData] 자동 메시지 시뮬레이션 시작 - {simulationDuration}초 동안 {totalMessages}개 메시지");
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
        Debug.Log("[DMTestData] 자동 메시지 시뮬레이션 중지");
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

        Debug.Log($"[DMTestData] 시뮬레이션 시작 - 평균 간격: {averageInterval:F1}초");

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

            // 알림 표시
            if (showNotifications && notificationManager != null)
            {
                notificationManager.ShowNotification(senderName, message, senderId);

#if UNITY_EDITOR
                notificationManager.ShowEditorNotification(senderName, message);
#endif
            }

            // 현재 열린 채팅방이면 메시지 직접 추가
            if (currentChatUserId == senderId && messagePanelManager?.chatMessageContent != null)
            {
                AddIncomingMessage(message);
            }

            // 대화 목록 업데이트
            UpdateConversationListPreview(senderId, senderName, message);

            messagesSent++;
            Debug.Log($"[DMTestData] 시뮬레이션 메시지 #{messagesSent}: {senderName} - {message}");
        }

        isSimulationRunning = false;
        Debug.Log($"[DMTestData] 자동 메시지 시뮬레이션 완료 - 총 {messagesSent}개 메시지 전송됨");
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
}
