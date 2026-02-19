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
    public float initialHeightRatio = 0.55f; // 초기 높이 비율 (입력 전, 기존 0.7보다 더 높게)
    public float expandedHeightRatio = 0.85f; // 최대 높이 비율 (입력 시)
    public CanvasGroup panelCanvasGroup;

    [Header("Heart Sprites")]
    [Tooltip("빈 하트 스프라이트 (좋아요 안 누른 상태)")]
    public Sprite unlikeSprite; // heart_unlike 스프라이트 연결
    [Tooltip("채워진 하트 스프라이트 (좋아요 누른 상태)")]
    public Sprite likedSprite; // heart_pink 또는 heart 스프라이트 연결

    [Header("Input Settings")]
    public int maxCommentLength = 500; // 댓글 최대 글자 수

    private int currentLocationId = -1;
    public bool IsPanelOpen { get; private set; } = false;
    private bool isExpanded = false; // 패널 확장 상태 유지

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

        // closeButton 자동 연결 (Inspector에서 연결되지 않은 경우 fallback)
        if (closeButton == null)
        {
            Transform found = transform.Find("CloseButton");
            if (found == null) found = transform.Find("closeButton");
            if (found == null) found = transform.Find("Close");
            if (found != null) closeButton = found.GetComponent<Button>();
        }

        if (sendButton != null) sendButton.onClick.AddListener(PostComment);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);

        if (commentInputField != null)
        {
            commentInputField.onValueChanged.AddListener(OnInputValueChanged);
            commentInputField.characterLimit = maxCommentLength; // 글자 수 제한 설정
            // 입력창 자동 확장 설정
            AutoExpandInputField.Setup(commentInputField, 50f, 120f);
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

            float targetY = isExpanded ? expandedHeightRatio : initialHeightRatio;
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
        // 반복 중 수정 방지를 위해 리스트에 먼저 수집
        var children = new List<GameObject>();
        foreach (Transform child in commentContent)
            children.Add(child.gameObject);
        foreach (var child in children)
            Destroy(child);

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
        if (commentItemPrefab == null)
        {
            Debug.LogError("[CommentManager] CreateCommentItem - commentItemPrefab이 null입니다! 인스펙터에서 연결 필요");
            return;
        }

        GameObject itemObj = Instantiate(commentItemPrefab, commentContent);
        itemObj.SetActive(true); // 템플릿이 꺼져있으므로 켜줌

        CommentItem itemScript = itemObj.GetComponent<CommentItem>();
        if (itemScript != null)
        {
            // 하트 스프라이트 주입 (프리팹에 설정되어 있지 않으면 CommentManager에서 연결)
            if (itemScript.likeIcon == null && unlikeSprite != null)
                itemScript.likeIcon = unlikeSprite;
            if (itemScript.likedSprite == null && likedSprite != null)
                itemScript.likedSprite = likedSprite;

            itemScript.Setup(data);
        }
        else
        {
            Debug.LogError($"[CommentManager] CreateCommentItem - CommentItem 컴포넌트가 없습니다! 프리팹: {commentItemPrefab.name}");
        }

        // 레이아웃은 CommentItem.ForceLayoutUpdate()에서 처리됨
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
            
            StartCoroutine(FetchComments(locationId, currentUserId));
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
        // commentContent null 체크
        if (commentContent == null)
        {
            Debug.LogError("[CommentManager] commentContent가 null입니다! 인스펙터에서 할당 필요");
            yield break;
        }

        // Clear existing comments (Real & Skeleton) - 반복 중 수정 방지
        var existingChildren = new List<GameObject>();
        foreach (Transform child in commentContent)
            existingChildren.Add(child.gameObject);
        foreach (var child in existingChildren)
            Destroy(child);

        ShowSkeleton(); // 로딩 시작 시 스켈레톤 표시
        float skeletonStartTime = Time.time;

        string url = $"{ApiConfig.MAIN_SERVER}/comments?location_id={locationId}&user_id={currentUserId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            // 최소 0.8초 스켈레톤 표시 (너무 빨리 사라지지 않도록)
            float elapsed = Time.time - skeletonStartTime;
            if (elapsed < 0.8f)
                yield return new WaitForSeconds(0.8f - elapsed);

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
                Debug.LogError($"[CommentManager] Failed to fetch comments: {request.error}");
            }
        }
    }

    private void ShowSkeleton()
    {
        if (commentContent == null) return;

        // skeletonPrefab이 있으면 프리팹 사용, 없으면 동적 생성
        int count = 4;
        for (int i = 0; i < count; i++)
        {
            GameObject skel;
            if (skeletonPrefab != null)
            {
                skel = Instantiate(skeletonPrefab, commentContent);
            }
            else
            {
                skel = CreateCommentSkeletonItem(commentContent);
            }
            skel.name = "SkeletonItem";
            skel.SetActive(true);
        }
    }

    private GameObject CreateCommentSkeletonItem(Transform parent)
    {
        Color bgColor = new Color(0.15f, 0.15f, 0.18f, 1f);
        Color contentColor = new Color(0.22f, 0.22f, 0.26f, 1f);
        float itemHeight = 80f;

        GameObject item = new GameObject("SkeletonItem");
        item.transform.SetParent(parent, false);

        RectTransform itemRect = item.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0, itemHeight);

        LayoutElement le = item.AddComponent<LayoutElement>();
        le.preferredHeight = itemHeight;
        le.minHeight = itemHeight;

        Image itemBg = item.AddComponent<Image>();
        itemBg.color = bgColor;

        // 아바타 (둥근 원)
        CreateSkeletonBlock(item.transform, "Avatar",
            new Vector2(30f, 0f), new Vector2(40f, 40f), contentColor);

        // 유저명 바
        CreateSkeletonBlock(item.transform, "NameLine",
            new Vector2(80f, 12f), new Vector2(100f, 14f), contentColor);

        // 댓글 내용 바 (넓게)
        CreateSkeletonBlock(item.transform, "ContentLine",
            new Vector2(80f, -10f), new Vector2(250f, 12f),
            new Color(contentColor.r, contentColor.g, contentColor.b, 0.6f));

        // 쉬머 효과
        item.AddComponent<ShimmerEffect>();

        return item;
    }

    private static void CreateSkeletonBlock(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        GameObject block = new GameObject(name);
        block.transform.SetParent(parent, false);

        RectTransform rect = block.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = block.AddComponent<Image>();
        img.color = color;
    }

    private void HideSkeleton()
    {
        // 반복 중 수정 방지를 위해 삭제할 스켈레톤 먼저 수집
        var skeletonsToRemove = new List<GameObject>();
        foreach (Transform child in commentContent)
        {
            if (child.name.Contains("Skeleton"))
                skeletonsToRemove.Add(child.gameObject);
        }
        foreach (var skel in skeletonsToRemove)
            Destroy(skel);
    }

    public void PostComment()
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup();
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

        string url = $"{ApiConfig.MAIN_SERVER}/comments";

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
                Debug.LogError($"[CommentManager] PostComment failed: {request.error}");
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
        // 로그인 여부 상관없이 댓글 조회 가능
        string currentUserId = (LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn)
                                ? LoginManager.Instance.CurrentUser.id
                                : "";
        StartCoroutine(FetchBestCommentCoroutine(locationId, currentUserId, callback));
    }

    private IEnumerator FetchBestCommentCoroutine(int locationId, string userId, System.Action<CommentData> callback)
    {
        string url = $"{ApiConfig.MAIN_SERVER}/comments?location_id={locationId}&user_id={userId}";

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
                Debug.LogError($"[CommentManager] FetchBestComment failed: {request.error}");
                callback?.Invoke(null);
            }
        }
    }

    private IEnumerator ToggleLocationLikeCoroutine(int locationId, System.Action<int, bool> callback)
    {
        string url = $"{ApiConfig.MAIN_SERVER}/locations/like";
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

    void OnDestroy()
    {
        // 메모리 누수 방지: 이벤트 리스너 정리
        if (sendButton != null) sendButton.onClick.RemoveListener(PostComment);
        if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
        if (commentInputField != null) commentInputField.onValueChanged.RemoveListener(OnInputValueChanged);
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