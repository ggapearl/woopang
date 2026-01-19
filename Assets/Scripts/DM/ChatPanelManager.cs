using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 모던 채팅 패널 매니저
/// 2025 UI 트렌드 적용: 다크 테마, 부드러운 애니메이션, 미니멀 디자인
/// </summary>
public class ChatPanelManager : MonoBehaviour
{
    public static ChatPanelManager Instance { get; private set; }

    // ============================================================
    // 색상 설정 (2025 다크 테마 트렌드)
    // ============================================================
    [Header("=== Color Scheme ===")]
    public Color backgroundColor = new Color(0.07f, 0.07f, 0.09f, 1f);      // #121217
    public Color headerColor = new Color(0.1f, 0.1f, 0.12f, 1f);            // #1A1A1F
    public Color inputAreaColor = new Color(0.1f, 0.1f, 0.12f, 1f);         // #1A1A1F
    public Color myBubbleColor = new Color(0.2f, 0.5f, 1f, 1f);             // 파란색 그라데이션
    public Color otherBubbleColor = new Color(0.15f, 0.15f, 0.18f, 1f);     // #262630
    public Color textColor = new Color(0.94f, 0.94f, 0.96f, 1f);            // #F0F0F5
    public Color subTextColor = new Color(0.5f, 0.5f, 0.55f, 1f);           // #808090
    public Color accentColor = new Color(0.4f, 0.6f, 1f, 1f);               // 밝은 파랑
    public Color inputFieldColor = new Color(0.12f, 0.12f, 0.15f, 1f);      // #1F1F26
    public Color sendButtonColor = new Color(0.3f, 0.55f, 1f, 1f);          // 전송 버튼

    // ============================================================
    // UI 참조
    // ============================================================
    [Header("=== UI References ===")]
    public GameObject chatPanel;
    public RectTransform headerArea;
    public RectTransform messageScrollArea;
    public RectTransform inputArea;
    public ScrollRect scrollRect;
    public RectTransform contentTransform;
    public InputField messageInputField;
    public Button sendButton;
    public Button backButton;
    public Text headerTitle;
    public Image headerAvatar;
    public Text onlineStatus;

    [Header("=== Prefabs ===")]
    public GameObject myMessageBubblePrefab;
    public GameObject otherMessageBubblePrefab;
    public GameObject dateSeparatorPrefab;
    public GameObject typingIndicatorPrefab;

    [Header("=== Animation Settings ===")]
    public float panelSlideSpeed = 0.3f;
    public float bubbleAnimationSpeed = 0.2f;
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ============================================================
    // 상태
    // ============================================================
    private string currentChatUserId;
    private string currentChatUsername;
    private bool isOpen = false;
    private List<GameObject> messageItems = new List<GameObject>();
    private Coroutine typingCoroutine;

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
        SetupButtonListeners();
        if (chatPanel != null)
            chatPanel.SetActive(false);
    }

    // ============================================================
    // Public Methods
    // ============================================================

    /// <summary>
    /// 채팅 패널 열기
    /// </summary>
    public void OpenChat(string userId, string username, string avatarUrl = null)
    {
        currentChatUserId = userId;
        currentChatUsername = username;

        if (headerTitle != null)
            headerTitle.text = username;

        if (onlineStatus != null)
            onlineStatus.text = "온라인";

        if (headerAvatar != null && !string.IsNullOrEmpty(avatarUrl))
            StartCoroutine(LoadAvatar(avatarUrl));

        ClearMessages();
        LoadChatHistory();

        if (chatPanel != null)
        {
            chatPanel.SetActive(true);
            StartCoroutine(AnimatePanelOpen());
        }

        isOpen = true;

        // 입력 필드 포커스
        if (messageInputField != null)
        {
            messageInputField.Select();
            messageInputField.ActivateInputField();
        }
    }

    /// <summary>
    /// 채팅 패널 닫기
    /// </summary>
    public void CloseChat()
    {
        StartCoroutine(AnimatePanelClose());
        isOpen = false;
        currentChatUserId = null;
        currentChatUsername = null;
    }

    /// <summary>
    /// 메시지 추가 (내 메시지)
    /// </summary>
    public void AddMyMessage(string content, string time = null)
    {
        if (myMessageBubblePrefab == null || contentTransform == null) return;

        GameObject bubble = Instantiate(myMessageBubblePrefab, contentTransform);
        SetupMessageBubble(bubble, content, time ?? DateTime.Now.ToString("HH:mm"), true);
        messageItems.Add(bubble);

        StartCoroutine(AnimateBubbleIn(bubble));
        ScrollToBottom();
    }

    /// <summary>
    /// 메시지 추가 (상대방 메시지)
    /// </summary>
    public void AddOtherMessage(string content, string time = null, string avatarUrl = null)
    {
        if (otherMessageBubblePrefab == null || contentTransform == null) return;

        GameObject bubble = Instantiate(otherMessageBubblePrefab, contentTransform);
        SetupMessageBubble(bubble, content, time ?? DateTime.Now.ToString("HH:mm"), false, avatarUrl);
        messageItems.Add(bubble);

        StartCoroutine(AnimateBubbleIn(bubble));
        ScrollToBottom();
    }

    /// <summary>
    /// 날짜 구분선 추가
    /// </summary>
    public void AddDateSeparator(string dateText)
    {
        if (dateSeparatorPrefab == null || contentTransform == null) return;

        GameObject separator = Instantiate(dateSeparatorPrefab, contentTransform);
        Text text = separator.GetComponentInChildren<Text>();
        if (text != null)
            text.text = dateText;

        messageItems.Add(separator);
    }

    /// <summary>
    /// 타이핑 인디케이터 표시
    /// </summary>
    public void ShowTypingIndicator()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(ShowTypingAnimation());
    }

    /// <summary>
    /// 타이핑 인디케이터 숨기기
    /// </summary>
    public void HideTypingIndicator()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        // 타이핑 인디케이터 오브젝트 제거
    }

    // ============================================================
    // Private Methods
    // ============================================================

    private void SetupButtonListeners()
    {
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendButtonClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClicked);

        if (messageInputField != null)
        {
            messageInputField.onEndEdit.AddListener(OnInputEndEdit);
            messageInputField.onValueChanged.AddListener(OnInputValueChanged);
        }
    }

    private void OnSendButtonClicked()
    {
        SendMessage();
    }

    private void OnBackButtonClicked()
    {
        CloseChat();
    }

    private void OnInputEndEdit(string text)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendMessage();
        }
    }

    private void OnInputValueChanged(string text)
    {
        // 전송 버튼 활성화/비활성화
        if (sendButton != null)
        {
            sendButton.interactable = !string.IsNullOrWhiteSpace(text);
        }
    }

    private void SendMessage()
    {
        if (messageInputField == null) return;

        string content = messageInputField.text?.Trim();
        if (string.IsNullOrEmpty(content)) return;

        // UI에 내 메시지 추가
        AddMyMessage(content);

        // 입력 필드 초기화 및 포커스 유지
        messageInputField.text = "";

        // 모바일에서 키보드 유지를 위해 다음 프레임에 활성화
        StartCoroutine(KeepInputFieldActive());

        // DirectMessageManager를 통해 실제 전송
        if (DirectMessageManager.Instance != null && !string.IsNullOrEmpty(currentChatUserId))
        {
            DirectMessageManager.Instance.SendMessage(currentChatUserId, content, (success) =>
            {
                if (!success)
                {
                    Debug.LogWarning("[ChatPanel] Failed to send message");
                    // 실패 시 메시지 버블에 실패 표시 추가 가능
                    ShowSendFailedIndicator();
                }
            });
        }

        // 햅틱 피드백
        TriggerHaptic();
    }

    private IEnumerator KeepInputFieldActive()
    {
        yield return null;
        if (messageInputField != null && isOpen)
        {
            messageInputField.Select();
            messageInputField.ActivateInputField();
        }
    }

    private void ShowSendFailedIndicator()
    {
        // 마지막 메시지에 전송 실패 표시 추가
        if (messageItems.Count > 0)
        {
            var lastMessage = messageItems[messageItems.Count - 1];
            if (lastMessage != null)
            {
                // 실패 표시 이미지 또는 텍스트 추가
                Transform failedIcon = lastMessage.transform.Find("FailedIcon");
                if (failedIcon != null)
                {
                    failedIcon.gameObject.SetActive(true);
                }
                else
                {
                    // FailedIcon이 없으면 동적으로 생성
                    CreateFailedIndicator(lastMessage.transform);
                }

                // 상태 텍스트 업데이트
                Text statusText = lastMessage.transform.Find("StatusText")?.GetComponent<Text>();
                if (statusText != null)
                {
                    statusText.text = "전송 실패 ⚠";
                    statusText.color = new Color(1f, 0.4f, 0.4f);
                }
            }
        }
    }

    private void CreateFailedIndicator(Transform parent)
    {
        // 실패 아이콘 동적 생성
        GameObject failedIcon = new GameObject("FailedIcon");
        failedIcon.transform.SetParent(parent, false);

        RectTransform rect = failedIcon.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(-5, 0);
        rect.sizeDelta = new Vector2(20, 20);

        Text failedText = failedIcon.AddComponent<Text>();
        failedText.text = "⚠";
        failedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        failedText.fontSize = 16;
        failedText.color = new Color(1f, 0.4f, 0.4f);
        failedText.alignment = TextAnchor.MiddleCenter;
    }

    /// <summary>
    /// 메시지 전송 상태 표시 업데이트
    /// </summary>
    private void UpdateMessageStatus(GameObject bubble, string status)
    {
        Text statusText = bubble.transform.Find("StatusText")?.GetComponent<Text>();
        if (statusText == null)
        {
            // StatusText 없으면 생성
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(bubble.transform, false);

            RectTransform rect = statusObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-5, 5);
            rect.sizeDelta = new Vector2(50, 15);

            statusText = statusObj.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 10;
            statusText.alignment = TextAnchor.MiddleRight;
        }

        switch (status)
        {
            case "sending":
                statusText.text = "전송중...";
                statusText.color = new Color(0.6f, 0.6f, 0.6f);
                break;
            case "sent":
                statusText.text = "✓";
                statusText.color = new Color(0.6f, 0.6f, 0.6f);
                break;
            case "read":
                statusText.text = "✓✓";
                statusText.color = new Color(0.3f, 0.7f, 1f); // 파란색
                break;
            case "failed":
                statusText.text = "⚠ 실패";
                statusText.color = new Color(1f, 0.4f, 0.4f);
                break;
        }
    }

    private void SetupMessageBubble(GameObject bubble, string content, string time, bool isMine, string avatarUrl = null)
    {
        // 메시지 내용
        Text contentText = bubble.transform.Find("Bubble/ContentText")?.GetComponent<Text>();
        if (contentText == null)
            contentText = bubble.GetComponentInChildren<Text>();
        if (contentText != null)
        {
            contentText.text = content;
            // 폰트 크기 대폭 증가
            contentText.fontSize = ChatBubbleLayoutHelper.FONT_SIZE;
            contentText.lineSpacing = 1.2f;

            // LayoutElement로 최소 높이 보장
            LayoutElement le = contentText.GetComponent<LayoutElement>();
            if (le == null) le = contentText.gameObject.AddComponent<LayoutElement>();
            le.minHeight = ChatBubbleLayoutHelper.MIN_BUBBLE_HEIGHT;
        }

        // 버블 자체 최소 높이
        LayoutElement bubbleLE = bubble.GetComponent<LayoutElement>();
        if (bubbleLE == null) bubbleLE = bubble.AddComponent<LayoutElement>();
        bubbleLE.minHeight = ChatBubbleLayoutHelper.MIN_BUBBLE_HEIGHT;

        // 시간
        Text timeText = bubble.transform.Find("TimeText")?.GetComponent<Text>();
        if (timeText != null)
        {
            timeText.text = time;
            timeText.fontSize = 22; // 시간 폰트도 증가
        }

        // 아바타 (상대방 메시지만)
        if (!isMine && !string.IsNullOrEmpty(avatarUrl))
        {
            Image avatar = bubble.transform.Find("Avatar")?.GetComponent<Image>();
            if (avatar != null)
                StartCoroutine(LoadAvatarForBubble(avatarUrl, avatar));
        }

        // 버블 색상 적용
        Image bubbleImage = bubble.transform.Find("Bubble")?.GetComponent<Image>();
        if (bubbleImage != null)
        {
            bubbleImage.color = isMine ? myBubbleColor : otherBubbleColor;
        }
    }

    private void ClearMessages()
    {
        foreach (var item in messageItems)
        {
            if (item != null)
                Destroy(item);
        }
        messageItems.Clear();
    }

    private void LoadChatHistory()
    {
        // DirectMessageManager에서 대화 기록 로드
        if (DirectMessageManager.Instance != null && !string.IsNullOrEmpty(currentChatUserId))
        {
            DirectMessageManager.Instance.OpenConversation(currentChatUserId, currentChatUsername);
        }
    }

    private void ScrollToBottom()
    {
        StartCoroutine(ScrollToBottomCoroutine());
    }

    private IEnumerator ScrollToBottomCoroutine()
    {
        // 여러 프레임 대기하여 레이아웃이 완전히 계산되도록 함
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;

        if (scrollRect != null)
        {
            // 부드러운 스크롤 애니메이션
            float duration = 0.2f;
            float elapsed = 0f;
            float startPos = scrollRect.verticalNormalizedPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = easeCurve.Evaluate(elapsed / duration);
                scrollRect.verticalNormalizedPosition = Mathf.Lerp(startPos, 0f, t);
                yield return null;
            }
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // ============================================================
    // Animations
    // ============================================================

    private IEnumerator AnimatePanelOpen()
    {
        if (chatPanel == null) yield break;

        CanvasGroup canvasGroup = chatPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = chatPanel.AddComponent<CanvasGroup>();

        RectTransform rect = chatPanel.GetComponent<RectTransform>();
        Vector2 startPos = new Vector2(rect.anchoredPosition.x, rect.anchoredPosition.y - 100);
        Vector2 endPos = rect.anchoredPosition;

        float elapsed = 0f;
        while (elapsed < panelSlideSpeed)
        {
            elapsed += Time.deltaTime;
            float t = easeCurve.Evaluate(elapsed / panelSlideSpeed);

            canvasGroup.alpha = t;
            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            yield return null;
        }

        canvasGroup.alpha = 1f;
        rect.anchoredPosition = endPos;
    }

    private IEnumerator AnimatePanelClose()
    {
        if (chatPanel == null) yield break;

        CanvasGroup canvasGroup = chatPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = chatPanel.AddComponent<CanvasGroup>();

        float elapsed = 0f;
        while (elapsed < panelSlideSpeed)
        {
            elapsed += Time.deltaTime;
            float t = 1f - easeCurve.Evaluate(elapsed / panelSlideSpeed);
            canvasGroup.alpha = t;
            yield return null;
        }

        canvasGroup.alpha = 0f;
        chatPanel.SetActive(false);
    }

    private IEnumerator AnimateBubbleIn(GameObject bubble)
    {
        if (bubble == null) yield break;

        CanvasGroup canvasGroup = bubble.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = bubble.AddComponent<CanvasGroup>();

        RectTransform rect = bubble.GetComponent<RectTransform>();
        Vector3 startScale = new Vector3(0.8f, 0.8f, 1f);
        Vector3 endScale = Vector3.one;

        canvasGroup.alpha = 0f;
        rect.localScale = startScale;

        float elapsed = 0f;
        while (elapsed < bubbleAnimationSpeed)
        {
            elapsed += Time.deltaTime;
            float t = easeCurve.Evaluate(elapsed / bubbleAnimationSpeed);

            canvasGroup.alpha = t;
            rect.localScale = Vector3.Lerp(startScale, endScale, t);

            yield return null;
        }

        canvasGroup.alpha = 1f;
        rect.localScale = endScale;
    }

    private GameObject typingIndicatorInstance;

    private IEnumerator ShowTypingAnimation()
    {
        // 타이핑 인디케이터 생성
        if (typingIndicatorInstance == null && contentTransform != null)
        {
            if (typingIndicatorPrefab != null)
            {
                typingIndicatorInstance = Instantiate(typingIndicatorPrefab, contentTransform);
            }
            else
            {
                // 프리팹이 없으면 동적 생성
                typingIndicatorInstance = CreateTypingIndicator();
            }
        }

        if (typingIndicatorInstance == null) yield break;

        typingIndicatorInstance.SetActive(true);
        ScrollToBottom();

        // 애니메이션 (... 점들이 깜빡이는 효과)
        Text dotsText = typingIndicatorInstance.GetComponentInChildren<Text>();
        if (dotsText != null)
        {
            string[] dots = { ".", "..", "..." };
            int index = 0;
            while (typingCoroutine != null)
            {
                dotsText.text = dots[index];
                index = (index + 1) % dots.Length;
                yield return new WaitForSeconds(0.4f);
            }
        }

        // 타이핑 종료 시 숨김
        if (typingIndicatorInstance != null)
            typingIndicatorInstance.SetActive(false);
    }

    private GameObject CreateTypingIndicator()
    {
        if (contentTransform == null) return null;

        // 타이핑 인디케이터 동적 생성
        GameObject indicator = new GameObject("TypingIndicator");
        indicator.transform.SetParent(contentTransform, false);

        RectTransform rect = indicator.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0, 0);
        rect.sizeDelta = new Vector2(0, 40);

        HorizontalLayoutGroup hlg = indicator.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 8, 8);
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        // 버블 배경
        GameObject bubble = new GameObject("Bubble");
        bubble.transform.SetParent(indicator.transform, false);

        RectTransform bubbleRect = bubble.AddComponent<RectTransform>();
        bubbleRect.sizeDelta = new Vector2(60, 30);

        Image bubbleImg = bubble.AddComponent<Image>();
        bubbleImg.color = otherBubbleColor;

        // 점 텍스트
        GameObject dotsObj = new GameObject("DotsText");
        dotsObj.transform.SetParent(bubble.transform, false);

        RectTransform dotsRect = dotsObj.AddComponent<RectTransform>();
        dotsRect.anchorMin = Vector2.zero;
        dotsRect.anchorMax = Vector2.one;
        dotsRect.sizeDelta = Vector2.zero;

        Text dotsText = dotsObj.AddComponent<Text>();
        dotsText.text = "...";
        dotsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dotsText.fontSize = 20;
        dotsText.color = subTextColor;
        dotsText.alignment = TextAnchor.MiddleCenter;

        return indicator;
    }

    // ============================================================
    // Helpers
    // ============================================================

    private IEnumerator LoadAvatar(string url)
    {
        using (var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                if (texture != null && headerAvatar != null)
                {
                    headerAvatar.sprite = Sprite.Create(texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
                }
            }
        }
    }

    private IEnumerator LoadAvatarForBubble(string url, Image targetImage)
    {
        using (var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                if (texture != null && targetImage != null)
                {
                    targetImage.sprite = Sprite.Create(texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
                }
            }
        }
    }

    private void TriggerHaptic()
    {
        // UIFeedbackManager.Instance 사용 (deprecated FindObjectOfType 대체)
        if (UIFeedbackManager.Instance != null)
        {
            UIFeedbackManager.Instance.TriggerLightHaptic();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
