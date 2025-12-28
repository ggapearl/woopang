using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.InputSystem; // 추가
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;

public class CommentManager : MonoBehaviour
{
    public static CommentManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject commentPanel;
    public RectTransform panelRect;
    public InputField commentInputField;
    public Button sendButton;
    public Transform commentContent;
    public GameObject commentItemPrefab;
    public GameObject skeletonPrefab; // 스켈레톤 프리팹
    public Button closeButton;
    public Text titleText;
    public GameObject buttonSpinner; // 로딩 스피너

    [Header("Animation")]
    public float slideDuration = 0.3f;
    public float expandedHeightRatio = 0.85f; // 최대 높이 비율 (인스펙터 조절 가능)
    public CanvasGroup panelCanvasGroup;

    private int currentLocationId = -1;
    public bool IsPanelOpen { get; private set; } = false;
    private bool isExpanded = false; // 패널 확장 상태 유지
    
    private const string BASE_URL = "https://woopang.com";

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        if (commentPanel != null)
        {
            commentPanel.SetActive(false);
            if (panelRect != null)
                panelRect.anchoredPosition = new Vector2(0, -Screen.height);
        }

        if (sendButton != null) sendButton.onClick.AddListener(PostComment);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);

        if (commentInputField != null)
        {
            commentInputField.onValueChanged.AddListener(OnInputValueChanged);
        }

        // Add Swipe to Close capability
        if (commentPanel != null)
        {
            SwipeToClose swipeHandler = commentPanel.GetComponent<SwipeToClose>();
            if (swipeHandler == null) swipeHandler = commentPanel.AddComponent<SwipeToClose>();
            
            swipeHandler.panelRect = panelRect;
            swipeHandler.onClose = ClosePanel;
        }
    }

    void Update()
    {
        // Android Back Button support (New Input System)
        if (IsPanelOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClosePanel();
        }

        // Panel Expansion Animation
        if (IsPanelOpen && panelRect != null)
        {
            // 입력창 포커스 시 확장 상태로 전환
            if (commentInputField != null && commentInputField.isFocused)
            {
                isExpanded = true;
            }

            float targetY = isExpanded ? expandedHeightRatio : 0.7f;
            Vector2 currentMax = panelRect.anchorMax;
            
            // 부드러운 슬라이드 애니메이션
            if (Mathf.Abs(currentMax.y - targetY) > 0.001f)
            {
                float newY = Mathf.Lerp(currentMax.y, targetY, Time.deltaTime * 10f);
                panelRect.anchorMax = new Vector2(1, newY);
                panelRect.offsetMax = Vector2.zero;
            }
        }
    }

    private void OnInputValueChanged(string text)
    {
        if (sendButton != null)
        {
            Image btnImg = sendButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = string.IsNullOrEmpty(text) ? Color.gray : new Color(0, 0.58f, 0.96f);
            }
            sendButton.interactable = !string.IsNullOrEmpty(text);
        }
    }

    private void GenerateMockComments()
    {
        foreach (Transform child in commentContent) Destroy(child.gameObject);

        for (int i = 0; i < 4; i++)
        {
            CommentData mock = new CommentData
            {
                id = i,
                username = $"User_{i}",
                content = i % 2 == 0 ? "정말 멋진 장소네요!" : "다음에 꼭 가보고 싶어요. 정보 감사합니다. 긴 글 테스트 긴 글 테스트 긴 글 테스트",
                created_at = System.DateTime.Now.AddHours(-i * 5).ToString(),
                like_count = i * 10,
                is_liked = false
            };
            CreateCommentItem(mock);
        }
    }

    private void CreateCommentItem(CommentData data)
    {
        if (commentItemPrefab == null) return;

        GameObject itemObj = Instantiate(commentItemPrefab, commentContent);
        itemObj.SetActive(true); // 템플릿이 꺼져있으므로 켜줌
        CommentItem itemScript = itemObj.GetComponent<CommentItem>();
        if (itemScript != null)
        {
            itemScript.Setup(data);
        }
    }

    public void OpenCommentPanel(int locationId, string objectName = null)
    {
        currentLocationId = locationId;
        if (commentPanel != null)
        {
            commentPanel.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(SlidePanel(true));
            
            // Placeholder 업데이트
            if (!string.IsNullOrEmpty(objectName) && commentInputField != null && commentInputField.placeholder != null)
            {
                Text placeText = commentInputField.placeholder.GetComponent<Text>();
                if (placeText != null) placeText.text = $"{objectName}에 댓글 달기...";
            }
            
            string currentUserId = (LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn) 
                                   ? LoginManager.Instance.CurrentUser.id 
                                   : "";
            
            #if UNITY_EDITOR
            GenerateMockComments(); 
            // StartCoroutine(FetchComments(locationId, currentUserId)); // 실제 서버 테스트 시 주석 해제
            #else
            StartCoroutine(FetchComments(locationId, currentUserId));
            #endif
        }
        IsPanelOpen = true;
    }

    public void ClosePanel()
    {
        if (IsPanelOpen)
        {
            StopAllCoroutines();
            StartCoroutine(SlidePanel(false));
            IsPanelOpen = false;
            isExpanded = false; // 확장 상태 초기화
        }
    }

    private IEnumerator SlidePanel(bool open)
    {
        float timer = 0f;
        Vector2 startPos = panelRect.anchoredPosition;
        Vector2 targetPos = open ? Vector2.zero : new Vector2(0, -Screen.height);
        
        float startAlpha = panelCanvasGroup.alpha;
        float targetAlpha = open ? 1f : 0f;

        while (timer < slideDuration)
        {
            timer += Time.deltaTime;
            float t = timer / slideDuration;
            t = t * t * (3f - 2f * t);
            
            panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            
            yield return null;
        }

        panelRect.anchoredPosition = targetPos;
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = targetAlpha;
        
        if (!open) commentPanel.SetActive(false);
    }

    private IEnumerator FetchComments(int locationId, string currentUserId)
    {
        // Clear existing comments (Real & Skeleton)
        foreach (Transform child in commentContent)
        {
            Destroy(child.gameObject);
        }

        ShowSkeleton(); // 로딩 시작 시 스켈레톤 표시

        string url = $"{BASE_URL}/comments?location_id={locationId}&user_id={currentUserId}";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            HideSkeleton(); // 로딩 완료 시 제거

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                List<CommentData> comments = ParseComments(json); 
                
                foreach (var comment in comments)
                {
                    CreateCommentItem(comment);
                }
            }
            else
            {
                Debug.LogError($"Failed to fetch comments: {request.error}");
            }
        }
    }

    private void ShowSkeleton()
    {
        if (skeletonPrefab == null) return;
        
        for(int i=0; i<5; i++)
        {
            GameObject skel = Instantiate(skeletonPrefab, commentContent);
            skel.SetActive(true);
        }
    }

    private void HideSkeleton()
    {
        foreach (Transform child in commentContent)
        {
            if (child.name.Contains("Skeleton")) Destroy(child.gameObject);
        }
    }

    public void PostComment()
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("로그인이 필요합니다.");
            if (LoginManager.Instance != null) LoginManager.Instance.Logout(); 
            return;
        }

        if (string.IsNullOrEmpty(commentInputField.text)) return;
        StartCoroutine(PostCommentCoroutine(commentInputField.text));
    }

    private IEnumerator PostCommentCoroutine(string content)
    {
        // 로딩 시작
        Text btnText = sendButton.GetComponentInChildren<Text>();
        if (buttonSpinner != null) buttonSpinner.SetActive(true);
        if (btnText != null) btnText.enabled = false;
        
        Coroutine spinRoutine = StartCoroutine(SpinButton());

        string url = $"{BASE_URL}/comments";
        
        CommentPostData postData = new CommentPostData
        {
            location_id = currentLocationId,
            user_id = LoginManager.Instance.CurrentUser.id,
            username = LoginManager.Instance.CurrentUser.username,
            content = content
        };

        string json = JsonUtility.ToJson(postData);
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            // 로딩 종료
            if (buttonSpinner != null) buttonSpinner.SetActive(false);
            if (btnText != null) btnText.enabled = true;
            if (spinRoutine != null) StopCoroutine(spinRoutine);

            if (request.result == UnityWebRequest.Result.Success)
            {
                commentInputField.text = "";
                isExpanded = false; // 전송 후 축소
                StartCoroutine(FetchComments(currentLocationId, LoginManager.Instance.CurrentUser.id));
            }
            else
            {
                Debug.LogError($"Failed to post comment: {request.error}");
            }
        }
    }

    private IEnumerator SpinButton()
    {
        if (buttonSpinner == null) yield break;
        RectTransform rect = buttonSpinner.GetComponent<RectTransform>();
        while (true)
        {
            rect.Rotate(0, 0, -360 * Time.deltaTime); // 1초에 한바퀴
            yield return null;
        }
    }

    private List<CommentData> ParseComments(string json)
    {
        string wrappedJson = "{\"items\":" + json + "}";
        return JsonUtility.FromJson<CommentListWrapper>(wrappedJson).items;
    }

    public void ToggleLocationLike(int locationId, System.Action<int, bool> callback)
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup();
            return;
        }
        StartCoroutine(ToggleLocationLikeCoroutine(locationId, callback));
    }

    public void GetBestComment(int locationId, System.Action<CommentData> callback)
    {
        #if UNITY_EDITOR
        // 에디터 목업 데이터: 좋아요가 가장 많은 댓글 반환
        List<CommentData> mocks = new List<CommentData>();
        for (int i = 0; i < 4; i++)
        {
            mocks.Add(new CommentData
            {
                id = i,
                username = $"MockUser_{i}",
                content = i == 3 ? "와! 여기가 거기인가요? 정말 멋지네요. 좋아요 꾹 누르고 갑니다!" : $"댓글 테스트 {i}",
                like_count = i * 15 + 5, // 0:5, 1:20, 2:35, 3:50 (3번이 베스트)
                created_at = System.DateTime.Now.AddMinutes(-i * 10).ToString(),
                is_liked = false
            });
        }
        // 정렬
        mocks.Sort((a, b) => b.like_count.CompareTo(a.like_count));
        
        callback?.Invoke(mocks[0]);
        return;
        #endif

        // 로그인 여부 상관없이 댓글 조회 가능
        string currentUserId = (LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn) 
                                ? LoginManager.Instance.CurrentUser.id 
                                : "";
        StartCoroutine(FetchBestCommentCoroutine(locationId, currentUserId, callback));
    }

    private IEnumerator FetchBestCommentCoroutine(int locationId, string userId, System.Action<CommentData> callback)
    {
        string url = $"{BASE_URL}/comments?location_id={locationId}&user_id={userId}";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                List<CommentData> comments = ParseComments(json); 
                
                if (comments != null && comments.Count > 0)
                {
                    // 좋아요 순 내림차순, 그 다음 최신순
                    comments.Sort((a, b) => {
                        int likeCompare = b.like_count.CompareTo(a.like_count);
                        if (likeCompare != 0) return likeCompare;
                        return string.Compare(b.created_at, a.created_at);
                    });
                    callback?.Invoke(comments[0]);
                }
                else
                {
                    callback?.Invoke(null);
                }
            }
            else
            {
                // 에러 시 null 반환
                callback?.Invoke(null);
            }
        }
    }

    private IEnumerator ToggleLocationLikeCoroutine(int locationId, System.Action<int, bool> callback)
    {
        string url = $"{BASE_URL}/locations/like";
        LikePostData data = new LikePostData { location_id = locationId, user_id = LoginManager.Instance.CurrentUser.id };
        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<LocationLikeResponse>(request.downloadHandler.text);
                callback?.Invoke(response.total_likes, response.action == "liked");
            }
        }
    }
}

[System.Serializable]
public class CommentData
{
    public int id;
    public string user_id;
    public string username;
    public string content;
    public string created_at;
    public int like_count;
    public bool is_liked;
}

[System.Serializable]
public class CommentListWrapper
{
    public List<CommentData> items;
}

[System.Serializable]
public class CommentPostData
{
    public int location_id;
    public string user_id;
    public string username;
    public string content;
}

[System.Serializable]
public class LikePostData
{
    public int location_id;
    public int comment_id;
    public string user_id;
}

[System.Serializable]
public class LocationLikeResponse
{
    public string action;
    public int total_likes;
}