using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Text;

/// <summary>
/// DM 테스트 에디터 윈도우
/// 서버에 직접 DM을 전송하여 푸시 알림 테스트 가능
/// </summary>
public class DMTestEditor : EditorWindow
{
    // ============================================================
    // UI 테스트용 더미 데이터 로드 메뉴
    // ============================================================

    [MenuItem("WOOPANG/UI Test/더미 대화 목록 로드")]
    public static void LoadDummyConversations()
    {
        var manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null)
        {
            Debug.LogError("[DMTest] MessagePanelManager를 찾을 수 없습니다.");
            return;
        }

        // MessagePanel 활성화
        if (manager.messagePanel != null && !manager.messagePanel.activeSelf)
        {
            manager.messagePanel.SetActive(true);
        }

        manager.LoadDummyConversations();
        Debug.Log("[DMTest] 더미 대화 목록 로드 완료 - MessagePanel에서 UI 확인 가능");
    }

    [MenuItem("WOOPANG/UI Test/더미 시스템 채팅 로드 (AdminMessageBubble)")]
    public static void LoadDummySystemChat()
    {
        var manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null)
        {
            Debug.LogError("[DMTest] MessagePanelManager를 찾을 수 없습니다.");
            return;
        }

        // ChatRoomPanel 활성화
        if (manager.chatRoomPanel != null)
        {
            manager.chatRoomPanel.SetActive(true);
        }

        manager.LoadDummyChatMessages();
        Debug.Log("[DMTest] 더미 시스템 채팅 로드 완료 - ChatRoomPanel에서 AdminMessageBubble UI 확인 가능");
    }

    [MenuItem("WOOPANG/UI Test/더미 DM 채팅 로드 (My/Other Bubble)")]
    public static void LoadDummyDMChat()
    {
        var manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null)
        {
            Debug.LogError("[DMTest] MessagePanelManager를 찾을 수 없습니다.");
            return;
        }

        // ChatRoomPanel 활성화
        if (manager.chatRoomPanel != null)
        {
            manager.chatRoomPanel.SetActive(true);
        }

        manager.LoadDummyDMChatMessages();
        Debug.Log("[DMTest] 더미 DM 채팅 로드 완료 - ChatRoomPanel에서 My/Other Bubble UI 확인 가능");
    }

    [MenuItem("WOOPANG/UI Test/업로드 완료 알림 테스트")]
    public static void TestUploadNotification()
    {
        var manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null)
        {
            Debug.LogError("[DMTest] MessagePanelManager를 찾을 수 없습니다.");
            return;
        }

        // MessagePanel 활성화
        if (manager.messagePanel != null && !manager.messagePanel.activeSelf)
        {
            manager.messagePanel.SetActive(true);
        }

        // 업로드 완료 알림 시뮬레이션 (AddLocationNotificationFromPush 호출)
        string title = "🎉 콘텐츠 업로드 완료!";
        string body = "테스트 AR 콘텐츠이(가) 성공적으로 업로드되었습니다. 다른 사용자들이 볼 수 있어요!";
        string messageId = $"test_upload_{System.DateTime.Now.Ticks}";

        manager.AddLocationNotificationFromPush(title, body, messageId);

        Debug.Log($"[DMTest] 업로드 완료 알림 시뮬레이션 완료!\n" +
                  $"Title: {title}\n" +
                  $"Body: {body}\n" +
                  $"MessagePanel에서 시스템 알림이 추가되었는지 확인하세요.");
    }

    [MenuItem("WOOPANG/UI Test/빈 대화목록 테스트 (다국어)")]
    public static void TestEmptyInbox()
    {
        var manager = Object.FindFirstObjectByType<MessagePanelManager>();
        if (manager == null)
        {
            Debug.LogError("[DMTest] MessagePanelManager를 찾을 수 없습니다.");
            return;
        }

        // MessagePanel 활성화
        if (manager.messagePanel != null && !manager.messagePanel.activeSelf)
        {
            manager.messagePanel.SetActive(true);
        }

        // 대화 목록 비우고 빈 상태 표시
        manager.ClearAllConversations();

        string currentLang = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetCurrentLanguage()
            : "unknown";

        Debug.Log($"[DMTest] 빈 대화목록 테스트!\n" +
                  $"현재 언어: {currentLang}\n" +
                  $"디바이스 언어: {Application.systemLanguage}\n" +
                  $"MessagePanel에서 빈 상태 메시지가 표시되는지 확인하세요.");
    }

    [MenuItem("WOOPANG/UI Test/더미 대화 목록 로드", true)]
    [MenuItem("WOOPANG/UI Test/더미 시스템 채팅 로드 (AdminMessageBubble)", true)]
    [MenuItem("WOOPANG/UI Test/더미 DM 채팅 로드 (My/Other Bubble)", true)]
    [MenuItem("WOOPANG/UI Test/업로드 완료 알림 테스트", true)]
    [MenuItem("WOOPANG/UI Test/빈 대화목록 테스트 (다국어)", true)]
    public static bool ValidateDummyLoad()
    {
        return Application.isPlaying;
    }

    private string senderUserId = "3";
    private string senderUsername = "woopang";
    private string recipientUserId = "6";
    private string recipientUsername = "gogle";
    private string messageContent = "테스트 메시지입니다.";
    private string serverUrl = "";
    private string lastResult = "";
    private bool isRequesting = false;

    [MenuItem("WOOPANG/DM Test Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<DMTestEditor>("DM Test");
        window.minSize = new Vector2(400, 500);
    }

    void OnEnable()
    {
        // ApiConfig에서 서버 URL 가져오기
        serverUrl = "http://210.105.65.145:5050";
    }

    void OnGUI()
    {
        GUILayout.Label("DM 테스트 에디터", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // 서버 URL
        EditorGUILayout.LabelField("서버 설정", EditorStyles.boldLabel);
        serverUrl = EditorGUILayout.TextField("서버 URL", serverUrl);
        EditorGUILayout.Space(5);

        // 발신자 정보
        EditorGUILayout.LabelField("발신자 (From)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        senderUserId = EditorGUILayout.TextField("User ID", senderUserId, GUILayout.Width(200));
        senderUsername = EditorGUILayout.TextField("Username", senderUsername);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        // 수신자 정보
        EditorGUILayout.LabelField("수신자 (To)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        recipientUserId = EditorGUILayout.TextField("User ID", recipientUserId, GUILayout.Width(200));
        recipientUsername = EditorGUILayout.TextField("Username", recipientUsername);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        // 메시지 내용
        EditorGUILayout.LabelField("메시지 내용", EditorStyles.boldLabel);
        messageContent = EditorGUILayout.TextArea(messageContent, GUILayout.Height(80));
        EditorGUILayout.Space(10);

        // 프리셋 버튼
        EditorGUILayout.LabelField("빠른 설정", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("woopang → gogle"))
        {
            senderUserId = "3";
            senderUsername = "woopang";
            recipientUserId = "6";
            recipientUsername = "gogle";
        }

        if (GUILayout.Button("gogle → woopang"))
        {
            senderUserId = "6";
            senderUsername = "gogle";
            recipientUserId = "3";
            recipientUsername = "woopang";
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(15);

        // 전송 버튼
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
        GUI.enabled = !isRequesting;

        if (GUILayout.Button(isRequesting ? "전송 중..." : "📤 DM 전송", GUILayout.Height(40)))
        {
            SendTestDM();
        }

        GUI.enabled = true;
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);

        // 결과 표시
        if (!string.IsNullOrEmpty(lastResult))
        {
            EditorGUILayout.LabelField("결과", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(lastResult,
                lastResult.Contains("성공") ? MessageType.Info : MessageType.Warning);
        }

        EditorGUILayout.Space(20);

        // 도움말
        EditorGUILayout.LabelField("📋 Logcat 검색 키워드", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "FCM_DEBUG - FCM 메시지 수신 확인\n" +
            "DM_NOTIFY - 서버 푸시 알림 전송 로그\n" +
            "메시지 수신됨 - 클라이언트 수신 확인",
            MessageType.None);

        EditorGUILayout.Space(10);

        // ADB 명령어
        EditorGUILayout.LabelField("🔧 ADB 명령어", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Logcat 복사"))
        {
            EditorGUIUtility.systemCopyBuffer = "adb logcat | grep \"FCM_DEBUG\\|DM_NOTIFY\"";
            Debug.Log("Logcat 명령어가 클립보드에 복사되었습니다.");
        }

        if (GUILayout.Button("Clear Logcat"))
        {
            EditorGUIUtility.systemCopyBuffer = "adb logcat -c";
            Debug.Log("Clear Logcat 명령어가 클립보드에 복사되었습니다.");
        }

        EditorGUILayout.EndHorizontal();
    }

    private void SendTestDM()
    {
        if (string.IsNullOrEmpty(senderUserId) || string.IsNullOrEmpty(recipientUserId))
        {
            lastResult = "❌ 발신자/수신자 ID를 입력하세요.";
            return;
        }

        if (string.IsNullOrEmpty(messageContent))
        {
            lastResult = "❌ 메시지 내용을 입력하세요.";
            return;
        }

        isRequesting = true;
        lastResult = "전송 중...";

        // 현재 시간 포함한 메시지
        string timestampedMessage = $"{messageContent} [{System.DateTime.Now:HH:mm:ss}]";

        EditorCoroutineHelper.StartCoroutine(SendDMCoroutine(timestampedMessage));
    }

    private System.Collections.IEnumerator SendDMCoroutine(string message)
    {
        string url = $"{serverUrl}/api/dm/send";

        var jsonData = new DMSendData
        {
            sender_id = senderUserId,
            recipient_id = recipientUserId,
            content = message
        };

        string json = JsonUtility.ToJson(jsonData);
        Debug.Log($"[DMTest] 전송 요청: {url}\n{json}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            isRequesting = false;

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DMSendResponse>(request.downloadHandler.text);
                if (response.success)
                {
                    lastResult = $"✅ 전송 성공!\n" +
                                 $"Message ID: {response.message_id}\n" +
                                 $"From: {senderUsername} ({senderUserId})\n" +
                                 $"To: {recipientUsername} ({recipientUserId})\n" +
                                 $"Time: {response.created_at}";
                    Debug.Log($"[DMTest] {lastResult}");
                }
                else
                {
                    lastResult = $"❌ 전송 실패: {request.downloadHandler.text}";
                    Debug.LogWarning($"[DMTest] {lastResult}");
                }
            }
            else
            {
                lastResult = $"❌ 요청 실패: {request.error}\n" +
                             $"Response Code: {request.responseCode}\n" +
                             $"Response: {request.downloadHandler?.text}";
                Debug.LogError($"[DMTest] {lastResult}");
            }

            Repaint();
        }
    }

    [System.Serializable]
    private class DMSendData
    {
        public string sender_id;
        public string recipient_id;
        public string content;
    }

    [System.Serializable]
    private class DMSendResponse
    {
        public bool success;
        public int message_id;
        public string created_at;
        public string error;
    }
}

/// <summary>
/// 에디터 코루틴 헬퍼
/// </summary>
public static class EditorCoroutineHelper
{
    public static void StartCoroutine(System.Collections.IEnumerator routine)
    {
        EditorApplication.CallbackFunction callback = null;
        callback = () =>
        {
            try
            {
                if (!routine.MoveNext())
                {
                    EditorApplication.update -= callback;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"EditorCoroutine Error: {e}");
                EditorApplication.update -= callback;
            }
        };
        EditorApplication.update += callback;
    }
}
