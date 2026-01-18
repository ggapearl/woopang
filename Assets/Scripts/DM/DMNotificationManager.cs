using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// DM 알림 팝업 매니저
/// 메시지 수신 시 토스트 스타일 알림 표시
/// Unity Editor와 런타임 모두에서 동작
/// </summary>
public class DMNotificationManager : MonoBehaviour
{
    public static DMNotificationManager Instance { get; private set; }

    [Header("알림 설정")]
    [Tooltip("알림 표시 시간 (초)")]
    public float notificationDuration = 3f;

    [Tooltip("알림 슬라이드 애니메이션 시간")]
    public float slideAnimDuration = 0.3f;

    [Tooltip("최대 동시 표시 알림 수")]
    public int maxNotifications = 3;

    [Tooltip("알림 간 간격")]
    public float notificationSpacing = 10f;

    [Header("알림 UI")]
    public GameObject notificationPrefab;
    public Transform notificationContainer;

    [Header("사운드")]
    public AudioClip notificationSound;
    private AudioSource audioSource;

    // 활성 알림 목록
    private List<GameObject> activeNotifications = new List<GameObject>();

    // 알림 큐 (최대 수 초과 시)
    private Queue<NotificationData> pendingNotifications = new Queue<NotificationData>();

    // 알림 데이터 구조
    public struct NotificationData
    {
        public string senderName;
        public string message;
        public string senderId;
        public Sprite avatar;
    }

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

        // AudioSource 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
    }

    void Start()
    {
        // 알림 컨테이너 없으면 생성
        if (notificationContainer == null)
        {
            CreateNotificationContainer();
        }

        // 알림 프리팹 없으면 생성
        if (notificationPrefab == null)
        {
            CreateNotificationPrefab();
        }
    }

    /// <summary>
    /// 알림 컨테이너 생성
    /// </summary>
    private void CreateNotificationContainer()
    {
        // Canvas 찾기 또는 생성
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("NotificationCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // 최상위
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 컨테이너 생성 (화면 상단)
        GameObject container = new GameObject("NotificationContainer");
        container.transform.SetParent(canvas.transform, false);

        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -50); // 상단에서 50px 아래
        rect.sizeDelta = new Vector2(0, 300);

        // VerticalLayoutGroup
        VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = notificationSpacing;
        vlg.padding = new RectOffset(20, 20, 10, 10);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        // ContentSizeFitter
        ContentSizeFitter csf = container.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        notificationContainer = container.transform;
    }

    /// <summary>
    /// 알림 프리팹 동적 생성
    /// </summary>
    private void CreateNotificationPrefab()
    {
        // 프리팹 오브젝트 생성
        GameObject prefab = new GameObject("NotificationItem");

        RectTransform prefabRect = prefab.AddComponent<RectTransform>();
        prefabRect.sizeDelta = new Vector2(350, 80);

        // 배경 이미지
        Image bgImage = prefab.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        // CanvasGroup (페이드 애니메이션용)
        prefab.AddComponent<CanvasGroup>();

        // HorizontalLayoutGroup
        HorizontalLayoutGroup hlg = prefab.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.padding = new RectOffset(12, 12, 10, 10);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // 아바타 이미지
        GameObject avatarObj = new GameObject("Avatar");
        avatarObj.transform.SetParent(prefab.transform, false);
        RectTransform avatarRect = avatarObj.AddComponent<RectTransform>();
        avatarRect.sizeDelta = new Vector2(50, 50);
        Image avatarImg = avatarObj.AddComponent<Image>();
        avatarImg.color = new Color(0.3f, 0.3f, 0.3f);

        // 마스크 (원형)
        Mask mask = avatarObj.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // 텍스트 컨테이너
        GameObject textContainer = new GameObject("TextContainer");
        textContainer.transform.SetParent(prefab.transform, false);
        RectTransform textRect = textContainer.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(260, 60);

        VerticalLayoutGroup textVlg = textContainer.AddComponent<VerticalLayoutGroup>();
        textVlg.spacing = 4f;
        textVlg.childAlignment = TextAnchor.MiddleLeft;
        textVlg.childForceExpandWidth = true;
        textVlg.childForceExpandHeight = false;
        textVlg.childControlWidth = true;
        textVlg.childControlHeight = true;

        // 발신자 이름
        GameObject senderObj = new GameObject("SenderText");
        senderObj.transform.SetParent(textContainer.transform, false);
        Text senderText = senderObj.AddComponent<Text>();
        senderText.text = "발신자";
        senderText.fontSize = 14;
        senderText.fontStyle = FontStyle.Bold;
        senderText.color = Color.white;
        senderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        LayoutElement senderLE = senderObj.AddComponent<LayoutElement>();
        senderLE.preferredHeight = 20;

        // 메시지 내용
        GameObject messageObj = new GameObject("MessageText");
        messageObj.transform.SetParent(textContainer.transform, false);
        Text messageText = messageObj.AddComponent<Text>();
        messageText.text = "메시지 내용";
        messageText.fontSize = 13;
        messageText.color = new Color(0.8f, 0.8f, 0.8f);
        messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        LayoutElement messageLE = messageObj.AddComponent<LayoutElement>();
        messageLE.preferredHeight = 36;
        messageLE.flexibleWidth = 1;

        // 버튼 컴포넌트 (클릭으로 채팅방 열기)
        Button btn = prefab.AddComponent<Button>();
        btn.targetGraphic = bgImage;

        // 프리팹 비활성화 후 저장
        prefab.SetActive(false);
        notificationPrefab = prefab;
    }

    /// <summary>
    /// 알림 표시
    /// </summary>
    public void ShowNotification(string senderName, string message, string senderId = "", Sprite avatar = null)
    {
        NotificationData data = new NotificationData
        {
            senderName = senderName,
            message = message,
            senderId = senderId,
            avatar = avatar
        };

        // 최대 수 초과 시 큐에 추가
        if (activeNotifications.Count >= maxNotifications)
        {
            pendingNotifications.Enqueue(data);
            return;
        }

        CreateNotificationUI(data);
    }

    /// <summary>
    /// 알림 UI 생성
    /// </summary>
    private void CreateNotificationUI(NotificationData data)
    {
        if (notificationContainer == null || notificationPrefab == null)
        {
            Debug.LogWarning("[DMNotification] Container 또는 Prefab이 없습니다.");
            return;
        }

        // 인스턴스 생성
        GameObject notification = Instantiate(notificationPrefab, notificationContainer);
        notification.SetActive(true);

        // 텍스트 설정
        Text senderText = notification.transform.Find("TextContainer/SenderText")?.GetComponent<Text>();
        if (senderText != null)
            senderText.text = data.senderName;

        Text messageText = notification.transform.Find("TextContainer/MessageText")?.GetComponent<Text>();
        if (messageText != null)
        {
            string preview = data.message.Length > 50 ? data.message.Substring(0, 50) + "..." : data.message;
            messageText.text = preview;
        }

        // 아바타 설정
        if (data.avatar != null)
        {
            Image avatarImg = notification.transform.Find("Avatar")?.GetComponent<Image>();
            if (avatarImg != null)
                avatarImg.sprite = data.avatar;
        }

        // 클릭 이벤트
        Button btn = notification.GetComponent<Button>();
        if (btn != null)
        {
            string senderId = data.senderId;
            string senderName = data.senderName;
            btn.onClick.AddListener(() =>
            {
                OnNotificationClicked(senderId, senderName);
                RemoveNotification(notification);
            });
        }

        activeNotifications.Add(notification);

        // 사운드 재생
        if (notificationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(notificationSound);
        }

        // 애니메이션 시작
        StartCoroutine(AnimateNotification(notification));

        Debug.Log($"[DMNotification] 알림: {data.senderName} - {data.message}");
    }

    /// <summary>
    /// 알림 애니메이션 (슬라이드 인 -> 대기 -> 페이드 아웃)
    /// </summary>
    private IEnumerator AnimateNotification(GameObject notification)
    {
        if (notification == null) yield break;

        RectTransform rect = notification.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = notification.GetComponent<CanvasGroup>();

        if (rect == null || canvasGroup == null) yield break;

        // 초기 상태: 위에서 슬라이드
        Vector2 startPos = rect.anchoredPosition + new Vector2(0, 50);
        Vector2 endPos = rect.anchoredPosition;
        canvasGroup.alpha = 0f;

        // 슬라이드 인
        float elapsed = 0f;
        while (elapsed < slideAnimDuration)
        {
            if (notification == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / slideAnimDuration;
            float easeT = EaseOutBack(t);

            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, easeT);
            canvasGroup.alpha = t;

            yield return null;
        }

        rect.anchoredPosition = endPos;
        canvasGroup.alpha = 1f;

        // 대기
        yield return new WaitForSeconds(notificationDuration);

        // 페이드 아웃
        elapsed = 0f;
        while (elapsed < slideAnimDuration)
        {
            if (notification == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / slideAnimDuration;

            canvasGroup.alpha = 1f - t;
            rect.anchoredPosition = endPos + new Vector2(0, -20 * t);

            yield return null;
        }

        // 제거
        RemoveNotification(notification);
    }

    /// <summary>
    /// 알림 제거
    /// </summary>
    private void RemoveNotification(GameObject notification)
    {
        if (notification == null) return;

        activeNotifications.Remove(notification);
        Destroy(notification);

        // 대기 중인 알림 처리
        if (pendingNotifications.Count > 0)
        {
            NotificationData nextData = pendingNotifications.Dequeue();
            CreateNotificationUI(nextData);
        }
    }

    /// <summary>
    /// 알림 클릭 시 처리
    /// </summary>
    private void OnNotificationClicked(string senderId, string senderName)
    {
        Debug.Log($"[DMNotification] 알림 클릭: {senderName} ({senderId})");

        // MessagePanelManager 찾아서 채팅방 열기
        MessagePanelManager panelManager = FindFirstObjectByType<MessagePanelManager>();
        if (panelManager != null)
        {
            // 메시지 패널 열기
            if (panelManager.messagePanel != null)
                panelManager.messagePanel.SetActive(true);

            // 채팅방 열기
            if (panelManager.chatRoomPanel != null)
                panelManager.chatRoomPanel.SetActive(true);

            // 타이틀 설정
            if (panelManager.chatRoomTitle != null)
                panelManager.chatRoomTitle.text = senderName;
        }

        // DMTestDataGenerator에게 채팅방 열기 요청
        DMTestDataGenerator testGen = FindFirstObjectByType<DMTestDataGenerator>();
        if (testGen != null)
        {
            testGen.OpenTestChatRoom(senderId, senderName, false);
        }
    }

    /// <summary>
    /// 모든 알림 제거
    /// </summary>
    public void ClearAllNotifications()
    {
        foreach (var notification in activeNotifications)
        {
            if (notification != null)
                Destroy(notification);
        }
        activeNotifications.Clear();
        pendingNotifications.Clear();
    }

    /// <summary>
    /// EaseOutBack 이징 함수
    /// </summary>
    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Unity Editor에서도 알림 표시 (OnGUI 사용)
    /// </summary>
    private List<EditorNotification> editorNotifications = new List<EditorNotification>();

    private class EditorNotification
    {
        public string senderName;
        public string message;
        public float startTime;
        public float duration;
    }

    /// <summary>
    /// Editor 전용 알림 추가
    /// </summary>
    public void ShowEditorNotification(string senderName, string message)
    {
        editorNotifications.Add(new EditorNotification
        {
            senderName = senderName,
            message = message,
            startTime = Time.time,
            duration = notificationDuration
        });

        Debug.Log($"<color=cyan>[DM 알림]</color> <color=yellow>{senderName}</color>: {message}");
    }

    void OnGUI()
    {
        if (editorNotifications.Count == 0) return;

        // 만료된 알림 제거
        editorNotifications.RemoveAll(n => Time.time - n.startTime > n.duration);

        // 알림 스타일
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTexture(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.9f));
        boxStyle.padding = new RectOffset(15, 15, 10, 10);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;
        titleStyle.fontSize = 14;

        GUIStyle messageStyle = new GUIStyle(GUI.skin.label);
        messageStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        messageStyle.fontSize = 12;
        messageStyle.wordWrap = true;

        float yOffset = 60;
        float boxWidth = 320;
        float boxHeight = 70;

        for (int i = 0; i < Mathf.Min(editorNotifications.Count, maxNotifications); i++)
        {
            var notif = editorNotifications[i];
            float elapsed = Time.time - notif.startTime;
            float alpha = 1f;

            // 페이드 인/아웃
            if (elapsed < 0.3f)
                alpha = elapsed / 0.3f;
            else if (elapsed > notif.duration - 0.3f)
                alpha = (notif.duration - elapsed) / 0.3f;

            GUI.color = new Color(1, 1, 1, alpha);

            Rect boxRect = new Rect(Screen.width - boxWidth - 20, yOffset + i * (boxHeight + 10), boxWidth, boxHeight);

            GUI.Box(boxRect, "", boxStyle);

            GUI.Label(new Rect(boxRect.x + 15, boxRect.y + 8, boxRect.width - 30, 20), notif.senderName, titleStyle);

            string preview = notif.message.Length > 40 ? notif.message.Substring(0, 40) + "..." : notif.message;
            GUI.Label(new Rect(boxRect.x + 15, boxRect.y + 30, boxRect.width - 30, 35), preview, messageStyle);

            GUI.color = Color.white;
        }
    }

    private Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = color;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
#endif
}
