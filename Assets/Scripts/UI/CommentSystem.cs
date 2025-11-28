using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Linq;

/// <summary>
/// 댓글 시스템 - 인스타그램 스타일의 댓글 기능
/// 좋아요 순/최신순 정렬, 댓글 입력 지원
/// </summary>
public class CommentSystem : MonoBehaviour
{
    [Header("UI Elements - Top Comment (하단 프리뷰)")]
    [SerializeField] private GameObject topCommentPanel;       // 인기 댓글 1개 표시 패널
    [SerializeField] private Text topCommentUsername;          // 작성자 이름
    [SerializeField] private Text topCommentText;              // 댓글 내용
    [SerializeField] private Text topCommentLikes;             // 좋아요 수

    [Header("UI Elements - Full Comment Panel")]
    [SerializeField] private GameObject fullCommentPanel;      // 전체 댓글 패널
    [SerializeField] private CanvasGroup fullCommentCanvasGroup;
    [SerializeField] private ScrollRect commentScrollRect;     // 댓글 스크롤
    [SerializeField] private Transform commentContainer;       // 댓글 아이템 부모
    [SerializeField] private GameObject commentItemPrefab;     // 댓글 아이템 프리팹

    [Header("UI Elements - Comment Input")]
    [SerializeField] private InputField commentInput;          // 댓글 입력창
    [SerializeField] private Button submitButton;              // 댓글 등록 버튼

    [Header("UI Elements - Sort Buttons")]
    [SerializeField] private Button sortByLikesButton;         // 좋아요순 정렬
    [SerializeField] private Button sortByNewestButton;        // 최신순 정렬
    [SerializeField] private Text sortByLikesText;
    [SerializeField] private Text sortByNewestText;

    [Header("Settings")]
    [SerializeField] private float panelSlideSpeed = 0.3f;
    [SerializeField] private Color activeButtonColor = new Color(1f, 0.95f, 0.8f, 1f);
    [SerializeField] private Color inactiveButtonColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    // 댓글 데이터
    private List<CommentData> comments = new List<CommentData>();
    private int currentPlaceId = -1;
    private bool isPanelOpen = false;
    private SortMode currentSortMode = SortMode.Likes;

    private enum SortMode
    {
        Likes,
        Newest
    }

    // 댓글 아이템 풀
    private List<GameObject> commentItemPool = new List<GameObject>();

    void Awake()
    {
        // 초기 UI 설정
        if (topCommentPanel != null)
        {
            topCommentPanel.SetActive(false);
        }
        if (fullCommentPanel != null)
        {
            fullCommentPanel.SetActive(false);
            if (fullCommentCanvasGroup == null)
            {
                fullCommentCanvasGroup = fullCommentPanel.GetComponent<CanvasGroup>();
                if (fullCommentCanvasGroup == null)
                {
                    fullCommentCanvasGroup = fullCommentPanel.AddComponent<CanvasGroup>();
                }
            }
            fullCommentCanvasGroup.alpha = 0f;
        }

        // 버튼 이벤트 등록
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(OnSubmitComment);
        }
        if (sortByLikesButton != null)
        {
            sortByLikesButton.onClick.AddListener(() => SetSortMode(SortMode.Likes));
        }
        if (sortByNewestButton != null)
        {
            sortByNewestButton.onClick.AddListener(() => SetSortMode(SortMode.Newest));
        }

        UpdateSortButtonUI();
    }

    /// <summary>
    /// 특정 장소의 댓글 로드
    /// </summary>
    public void LoadComments(int placeId)
    {
        currentPlaceId = placeId;
        StartCoroutine(FetchComments(placeId));
    }

    /// <summary>
    /// 서버에서 댓글 가져오기
    /// </summary>
    private IEnumerator FetchComments(int placeId)
    {
        string url = $"https://woopang.com/api/comments?place_id={placeId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    comments = JsonConvert.DeserializeObject<List<CommentData>>(request.downloadHandler.text);
                    Debug.Log($"[CommentSystem] {comments.Count}개 댓글 로드됨");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CommentSystem] 댓글 파싱 실패: {e.Message}");
                    comments = new List<CommentData>();
                }
            }
            else
            {
                Debug.LogWarning($"[CommentSystem] 댓글 로드 실패: {request.error}");
                comments = new List<CommentData>();
            }

            UpdateTopComment();
            if (isPanelOpen)
            {
                UpdateCommentList();
            }
        }
    }

    /// <summary>
    /// 인기 댓글 1개 업데이트 (하단 프리뷰)
    /// </summary>
    private void UpdateTopComment()
    {
        if (topCommentPanel == null) return;

        if (comments.Count == 0)
        {
            topCommentPanel.SetActive(false);
            return;
        }

        // 좋아요가 가장 많은 댓글 찾기
        CommentData topComment = comments.OrderByDescending(c => c.likes).FirstOrDefault();

        if (topComment != null)
        {
            topCommentPanel.SetActive(true);
            if (topCommentUsername != null)
            {
                topCommentUsername.text = topComment.username ?? "익명";
            }
            if (topCommentText != null)
            {
                // 댓글 내용 줄이기 (너무 길면 잘라내기)
                string displayText = topComment.content;
                if (displayText.Length > 50)
                {
                    displayText = displayText.Substring(0, 47) + "...";
                }
                topCommentText.text = displayText;
            }
            if (topCommentLikes != null)
            {
                topCommentLikes.text = topComment.likes > 0 ? $"{topComment.likes}" : "";
            }
        }
        else
        {
            topCommentPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 전체 댓글 패널 열기/닫기
    /// </summary>
    public void ToggleFullCommentPanel()
    {
        if (isPanelOpen)
        {
            CloseFullCommentPanel();
        }
        else
        {
            OpenFullCommentPanel();
        }
    }

    /// <summary>
    /// 전체 댓글 패널 열기
    /// </summary>
    public void OpenFullCommentPanel()
    {
        if (fullCommentPanel == null) return;

        isPanelOpen = true;
        fullCommentPanel.SetActive(true);
        UpdateCommentList();
        StartCoroutine(AnimatePanelOpen());
    }

    /// <summary>
    /// 전체 댓글 패널 닫기
    /// </summary>
    public void CloseFullCommentPanel()
    {
        if (fullCommentPanel == null) return;

        isPanelOpen = false;
        StartCoroutine(AnimatePanelClose());
    }

    private IEnumerator AnimatePanelOpen()
    {
        float elapsed = 0f;
        while (elapsed < panelSlideSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / panelSlideSpeed;
            fullCommentCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }
        fullCommentCanvasGroup.alpha = 1f;
    }

    private IEnumerator AnimatePanelClose()
    {
        float elapsed = 0f;
        while (elapsed < panelSlideSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / panelSlideSpeed;
            fullCommentCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }
        fullCommentCanvasGroup.alpha = 0f;
        fullCommentPanel.SetActive(false);
    }

    /// <summary>
    /// 정렬 모드 설정
    /// </summary>
    private void SetSortMode(SortMode mode)
    {
        currentSortMode = mode;
        UpdateSortButtonUI();
        UpdateCommentList();
    }

    private void UpdateSortButtonUI()
    {
        if (sortByLikesText != null)
        {
            sortByLikesText.color = currentSortMode == SortMode.Likes ? activeButtonColor : inactiveButtonColor;
        }
        if (sortByNewestText != null)
        {
            sortByNewestText.color = currentSortMode == SortMode.Newest ? activeButtonColor : inactiveButtonColor;
        }
    }

    /// <summary>
    /// 댓글 리스트 업데이트
    /// </summary>
    private void UpdateCommentList()
    {
        if (commentContainer == null || commentItemPrefab == null) return;

        // 기존 아이템 풀로 반환
        foreach (var item in commentItemPool)
        {
            if (item != null)
            {
                item.SetActive(false);
            }
        }

        // 정렬
        List<CommentData> sortedComments;
        if (currentSortMode == SortMode.Likes)
        {
            sortedComments = comments.OrderByDescending(c => c.likes).ToList();
        }
        else
        {
            sortedComments = comments.OrderByDescending(c => c.created_at).ToList();
        }

        // 댓글 아이템 생성/재사용
        for (int i = 0; i < sortedComments.Count; i++)
        {
            GameObject itemObj;
            if (i < commentItemPool.Count)
            {
                itemObj = commentItemPool[i];
                itemObj.SetActive(true);
            }
            else
            {
                itemObj = Instantiate(commentItemPrefab, commentContainer);
                commentItemPool.Add(itemObj);
            }

            SetupCommentItem(itemObj, sortedComments[i]);
        }
    }

    /// <summary>
    /// 개별 댓글 아이템 설정
    /// </summary>
    private void SetupCommentItem(GameObject itemObj, CommentData comment)
    {
        // Username
        Transform usernameTransform = itemObj.transform.Find("Username");
        if (usernameTransform != null)
        {
            Text usernameText = usernameTransform.GetComponent<Text>();
            if (usernameText != null)
            {
                usernameText.text = comment.username ?? "익명";
            }
        }

        // Content
        Transform contentTransform = itemObj.transform.Find("Content");
        if (contentTransform != null)
        {
            Text contentText = contentTransform.GetComponent<Text>();
            if (contentText != null)
            {
                contentText.text = comment.content ?? "";
            }
        }

        // Likes
        Transform likesTransform = itemObj.transform.Find("Likes");
        if (likesTransform != null)
        {
            Text likesText = likesTransform.GetComponent<Text>();
            if (likesText != null)
            {
                likesText.text = comment.likes > 0 ? $"{comment.likes}" : "";
            }
        }

        // Timestamp
        Transform timestampTransform = itemObj.transform.Find("Timestamp");
        if (timestampTransform != null)
        {
            Text timestampText = timestampTransform.GetComponent<Text>();
            if (timestampText != null)
            {
                timestampText.text = FormatTimestamp(comment.created_at);
            }
        }

        // Like Button
        Transform likeButtonTransform = itemObj.transform.Find("LikeButton");
        if (likeButtonTransform != null)
        {
            Button likeButton = likeButtonTransform.GetComponent<Button>();
            if (likeButton != null)
            {
                likeButton.onClick.RemoveAllListeners();
                int commentId = comment.id;
                likeButton.onClick.AddListener(() => OnLikeComment(commentId));
            }
        }
    }

    /// <summary>
    /// 타임스탬프 포맷
    /// </summary>
    private string FormatTimestamp(string timestamp)
    {
        if (string.IsNullOrEmpty(timestamp)) return "";

        try
        {
            DateTime dt = DateTime.Parse(timestamp);
            TimeSpan diff = DateTime.Now - dt;

            if (diff.TotalMinutes < 1) return "방금";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}분 전";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}시간 전";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}일 전";
            return dt.ToString("yyyy.MM.dd");
        }
        catch
        {
            return timestamp;
        }
    }

    /// <summary>
    /// 댓글 제출
    /// </summary>
    private void OnSubmitComment()
    {
        if (commentInput == null || string.IsNullOrEmpty(commentInput.text)) return;
        if (currentPlaceId < 0) return;

        string content = commentInput.text.Trim();
        if (string.IsNullOrEmpty(content)) return;

        StartCoroutine(SubmitComment(content));
    }

    private IEnumerator SubmitComment(string content)
    {
        string url = "https://woopang.com/api/comments";

        var commentData = new
        {
            place_id = currentPlaceId,
            content = content,
            username = PlayerPrefs.GetString("Username", "익명")
        };

        string jsonData = JsonConvert.SerializeObject(commentData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[CommentSystem] 댓글 등록 성공");
                commentInput.text = "";

                // 댓글 새로고침
                LoadComments(currentPlaceId);
            }
            else
            {
                Debug.LogWarning($"[CommentSystem] 댓글 등록 실패: {request.error}");
            }
        }
    }

    /// <summary>
    /// 댓글 좋아요
    /// </summary>
    private void OnLikeComment(int commentId)
    {
        StartCoroutine(LikeComment(commentId));
    }

    private IEnumerator LikeComment(int commentId)
    {
        string url = $"https://woopang.com/api/comments/{commentId}/like";

        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[CommentSystem] 댓글 {commentId} 좋아요 성공");

                // 로컬에서 좋아요 수 증가
                var comment = comments.FirstOrDefault(c => c.id == commentId);
                if (comment != null)
                {
                    comment.likes++;
                    UpdateTopComment();
                    if (isPanelOpen)
                    {
                        UpdateCommentList();
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[CommentSystem] 좋아요 실패: {request.error}");
            }
        }
    }

    /// <summary>
    /// 시스템 초기화/정리
    /// </summary>
    public void Clear()
    {
        comments.Clear();
        currentPlaceId = -1;
        isPanelOpen = false;

        if (topCommentPanel != null)
        {
            topCommentPanel.SetActive(false);
        }
        if (fullCommentPanel != null)
        {
            fullCommentPanel.SetActive(false);
            fullCommentCanvasGroup.alpha = 0f;
        }
        if (commentInput != null)
        {
            commentInput.text = "";
        }
    }

    /// <summary>
    /// 패널 상태 확인
    /// </summary>
    public bool IsPanelOpen => isPanelOpen;
}

/// <summary>
/// 댓글 데이터 모델
/// </summary>
[Serializable]
public class CommentData
{
    public int id { get; set; }
    public int place_id { get; set; }
    public string username { get; set; }
    public string content { get; set; }
    public int likes { get; set; }
    public string created_at { get; set; }
}
