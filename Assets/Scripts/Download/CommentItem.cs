using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using System.Collections;
using System.Text;

public class CommentItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Text usernameText;
    public Text contentText;
    public Text dateText;
    public Text likeCountText;
    public Button likeButton;
    public Button contentButton; // For "Read More"

    [Header("Like Icon Settings")]
    [Tooltip("빈 하트 스프라이트 (좋아요 안 눌렀을 때 기본값)")]
    public Sprite likeIcon;

    [Tooltip("채워진 하트 스프라이트 (좋아요 눌렀을 때)")]
    public Sprite likedSprite;

    [Header("Swipe Delete Settings")]
    [Tooltip("삭제 버튼 (스와이프 시 표시)")]
    public GameObject deleteButton;
    [Tooltip("삭제 확인 텍스트")]
    public Text deleteText;

    private int commentId;
    private string commentUserId; // 댓글 작성자 ID
    private bool isLiked;
    private int likeCount;

    private bool isExpanded = false;

    // 더블터치 감지용
    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.3f; // 더블클릭 인식 시간

    // 스와이프 삭제용
    private RectTransform rectTransform;
    private Vector2 dragStartPos;
    private float originalX;
    private bool isMyComment = false;
    private bool isDeleteVisible = false;
    private const float SWIPE_THRESHOLD = 80f; // 스와이프 임계값
    private const float DELETE_BUTTON_WIDTH = 100f; // 삭제 버튼 너비

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 likeIcon 스프라이트를 HeartIcon Image에 미리보기로 표시
    /// </summary>
    private void OnValidate()
    {
        if (likeButton != null && likeIcon != null)
        {
            Image heartIcon = likeButton.GetComponentInChildren<Image>();
            // likeButton 자체의 Image가 아닌 자식 Image만 대상
            if (heartIcon != null && heartIcon.gameObject != likeButton.gameObject)
            {
                heartIcon.sprite = likeIcon;
                heartIcon.color = Color.white;
            }
        }
    }
#endif

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (deleteButton != null)
            deleteButton.SetActive(false);
    }

    public void Setup(CommentData data)
    {
        commentId = data.id;
        commentUserId = data.user_id; // 댓글 작성자 ID 저장

        // 현재 로그인한 사용자의 댓글인지 확인
        if (LoginManager.Instance != null && LoginManager.Instance.CurrentUser != null)
        {
            isMyComment = LoginManager.Instance.CurrentUser.id == data.user_id;
        }

        Debug.Log($"[CommentItem] Setup - id: {data.id}, username: {data.username}, isMyComment: {isMyComment}");

        if (usernameText != null)
        {
            usernameText.text = data.username;
            Debug.Log($"[CommentItem] usernameText 설정됨: {data.username}");
        }
        else
        {
            Debug.LogWarning("[CommentItem] usernameText가 null입니다!");
        }

        if (contentText != null)
        {
            contentText.text = data.content;
            // 초기: 4줄까지만 표시 (truncate), 클릭시 확장
            contentText.verticalOverflow = VerticalWrapMode.Truncate;

            // RectTransform에서 최대 높이 제한 (4줄 기준 약 80px)
            RectTransform contentRect = contentText.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                LayoutElement layoutElement = contentText.GetComponent<LayoutElement>();
                if (layoutElement == null)
                    layoutElement = contentText.gameObject.AddComponent<LayoutElement>();

                // 축소 상태에서 최대 높이 제한 (긴 댓글이 템플릿과 겹치지 않도록)
                layoutElement.preferredHeight = -1; // 내용에 맞춤
                layoutElement.minHeight = 20;
            }
            Debug.Log($"[CommentItem] contentText 설정됨");
        }
        else
        {
            Debug.LogWarning("[CommentItem] contentText가 null입니다!");
        }

        if (contentButton != null)
        {
            contentButton.onClick.RemoveAllListeners();
            contentButton.onClick.AddListener(ToggleExpand);
        }

        if (dateText != null)
        {
            dateText.text = GetRelativeTime(data.created_at);
            Debug.Log($"[CommentItem] dateText 설정됨: {dateText.text}");
        }
        else
        {
            Debug.LogWarning("[CommentItem] dateText가 null입니다!");
        }

        likeCount = data.like_count;
        isLiked = data.is_liked;

        UpdateLikeUI();

        if (likeButton != null)
        {
            likeButton.onClick.RemoveAllListeners();
            likeButton.onClick.AddListener(OnLikeClicked);
            Debug.Log($"[CommentItem] likeButton 리스너 등록됨");
        }
        else
        {
            Debug.LogWarning("[CommentItem] likeButton이 null입니다!");
        }

        // 레이아웃 강제 재계산 (ContentSizeFitter가 제대로 동작하도록)
        StartCoroutine(ForceLayoutUpdate());
    }

    private IEnumerator ForceLayoutUpdate()
    {
        // Canvas가 업데이트될 때까지 대기
        yield return null;

        // 모든 ContentSizeFitter의 레이아웃 재계산 (자식부터 부모 순서로)
        var fitters = GetComponentsInChildren<ContentSizeFitter>(true);
        foreach (var fitter in fitters)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(fitter.GetComponent<RectTransform>());
        }

        // 자신의 레이아웃도 재계산
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);

        // 부모 Content의 레이아웃도 재계산
        if (transform.parent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent as RectTransform);
        }
    }

    private void ToggleExpand()
    {
        isExpanded = !isExpanded;
        if (contentText != null)
        {
            contentText.verticalOverflow = isExpanded ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;

            // LayoutElement의 높이 제한도 조정
            LayoutElement layoutElement = contentText.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                // 확장시 제한 해제, 축소시 최대 높이 제한
                layoutElement.preferredHeight = isExpanded ? -1 : -1;
            }

            StartCoroutine(ForceLayoutUpdate());
        }
    }

    private string GetRelativeTime(string dateStr)
    {
        if (!System.DateTime.TryParse(dateStr, out System.DateTime date)) return dateStr;

        System.TimeSpan diff = System.DateTime.Now - date;
        double minutes = diff.TotalMinutes;
        double hours = diff.TotalHours;
        double days = diff.TotalDays;

        if (minutes < 1) return "방금 전";
        if (minutes < 60) return $"{(int)minutes}분 전";
        if (hours < 24) return $"{(int)hours}시간 전";
        if (days < 7) return $"{(int)days}일 전";
        if (days < 30) return $"{(int)(days / 7)}주 전";
        if (days < 365) return $"{(int)(days / 30)}달 전";
        return $"{(int)(days / 365)}년 전";
    }

    private void UpdateLikeUI()
    {
        if (likeCountText != null)
        {
            likeCountText.text = likeCount > 0 ? FormatLikeCount(likeCount) : "";
        }

        // likeButton의 자식에서 HeartIcon Image 찾기
        if (likeButton != null)
        {
            // HeartIcon 이름으로 직접 찾기 (더 정확함)
            Transform heartIconTransform = likeButton.transform.Find("HeartIcon");
            Image likeIconImage = null;

            if (heartIconTransform != null)
            {
                likeIconImage = heartIconTransform.GetComponent<Image>();
            }
            else
            {
                // 폴백: 자식 중 Image가 있고 Button이 아닌 것 찾기
                foreach (Transform child in likeButton.transform)
                {
                    Image img = child.GetComponent<Image>();
                    if (img != null && child.GetComponent<Button>() == null)
                    {
                        likeIconImage = img;
                        break;
                    }
                }
            }

            if (likeIconImage != null)
            {
                // 좋아요 상태면 채워진 하트, 아니면 빈 하트
                likeIconImage.sprite = isLiked ? likedSprite : likeIcon;
                likeIconImage.color = Color.white;
            }
        }
    }

    private string FormatLikeCount(int count)
    {
        if (count < 1000) return count.ToString();

        SystemLanguage lang = Application.systemLanguage;

        if (lang == SystemLanguage.Korean)
        {
            if (count >= 100000000) return $"{(count / 100000000f):0.#}억";
            if (count >= 10000) return $"{(count / 10000f):0.#}만";
            return $"{(count / 1000f):0.#}천";
        }
        else if (lang == SystemLanguage.Japanese)
        {
            if (count >= 100000000) return $"{(count / 100000000f):0.#}億";
            if (count >= 10000) return $"{(count / 10000f):0.#}万";
            return count.ToString(); // 일본어는 천단위 표기 잘 안함 (보통 만단위)
        }
        else if (lang == SystemLanguage.Chinese || lang == SystemLanguage.ChineseSimplified || lang == SystemLanguage.ChineseTraditional)
        {
            if (count >= 100000000) return $"{(count / 100000000f):0.#}亿";
            if (count >= 10000) return $"{(count / 10000f):0.#}万";
            return count.ToString();
        }
        else // English, Spanish, etc.
        {
            if (count >= 1000000) return $"{(count / 1000000f):0.#}m";
            return $"{(count / 1000f):0.#}k";
        }
    }

    private void OnLikeClicked()
    {
        Debug.Log($"[CommentItem] OnLikeClicked 호출됨 - commentId: {commentId}, 현재 isLiked: {isLiked}");

        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            Debug.Log("[CommentItem] 로그인 필요 - 팝업 표시");
            if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup();
            return;
        }

        // Optimistic UI Update
        isLiked = !isLiked;
        likeCount += isLiked ? 1 : -1;
        Debug.Log($"[CommentItem] 좋아요 토글 - isLiked: {isLiked}, likeCount: {likeCount}");
        UpdateLikeUI();

        StartCoroutine(ToggleLike());
    }

    private IEnumerator ToggleLike()
    {
        string url = ApiConfig.COMMENTS_LIKE;
        LikePostData data = new LikePostData { comment_id = commentId, user_id = LoginManager.Instance.CurrentUser.id };
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
                // Server sync (optional, can just trust optimistic update or parse response)
                var response = JsonUtility.FromJson<CommentLikeResponse>(request.downloadHandler.text);
                likeCount = response.like_count;
                // isLiked is already toggled, but could verify action string
                UpdateLikeUI();
            }
            else
            {
                // Revert on failure
                isLiked = !isLiked;
                likeCount += isLiked ? 1 : -1;
                UpdateLikeUI();
                Debug.LogError($"Like failed: {request.error}");
            }
        }
    }

    private string FormatDate(string dateStr)
    {
        // Simple parsing, can be improved
        if (System.DateTime.TryParse(dateStr, out System.DateTime date))
        {
            return date.ToString("MM/dd HH:mm");
        }
        return dateStr;
    }

    /// <summary>
    /// 댓글 더블터치 시 좋아요 토글
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 삭제 버튼이 보이는 상태에서 클릭하면 닫기
        if (isDeleteVisible)
        {
            HideDeleteButton();
            return;
        }

        float timeSinceLastClick = Time.unscaledTime - lastClickTime;
        lastClickTime = Time.unscaledTime;

        if (timeSinceLastClick <= DOUBLE_CLICK_TIME && timeSinceLastClick > 0.05f)
        {
            // 더블터치 감지 - 좋아요 토글
            OnLikeClicked();
        }
    }

    #region Swipe Delete

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 본인 댓글이 아니면 스와이프 무시
        if (!isMyComment) return;

        dragStartPos = eventData.position;
        originalX = rectTransform.anchoredPosition.x;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 본인 댓글이 아니면 스와이프 무시
        if (!isMyComment) return;

        float deltaX = eventData.position.x - dragStartPos.x;

        // 왼쪽으로만 스와이프 가능 (삭제 버튼 표시)
        if (deltaX < 0)
        {
            float newX = Mathf.Max(originalX + deltaX, originalX - DELETE_BUTTON_WIDTH);
            rectTransform.anchoredPosition = new Vector2(newX, rectTransform.anchoredPosition.y);
        }
        else if (isDeleteVisible)
        {
            // 오른쪽으로 스와이프하면 닫기
            float newX = Mathf.Min(originalX - DELETE_BUTTON_WIDTH + deltaX, originalX);
            rectTransform.anchoredPosition = new Vector2(newX, rectTransform.anchoredPosition.y);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 본인 댓글이 아니면 스와이프 무시
        if (!isMyComment) return;

        float deltaX = eventData.position.x - dragStartPos.x;

        if (deltaX < -SWIPE_THRESHOLD && !isDeleteVisible)
        {
            // 삭제 버튼 표시
            ShowDeleteButton();
        }
        else if (deltaX > SWIPE_THRESHOLD && isDeleteVisible)
        {
            // 삭제 버튼 숨기기
            HideDeleteButton();
        }
        else
        {
            // 원래 위치로 복귀
            if (isDeleteVisible)
                rectTransform.anchoredPosition = new Vector2(originalX - DELETE_BUTTON_WIDTH, rectTransform.anchoredPosition.y);
            else
                rectTransform.anchoredPosition = new Vector2(originalX, rectTransform.anchoredPosition.y);
        }
    }

    private void ShowDeleteButton()
    {
        isDeleteVisible = true;
        StartCoroutine(AnimatePosition(originalX - DELETE_BUTTON_WIDTH));
        if (deleteButton != null)
            deleteButton.SetActive(true);
    }

    private void HideDeleteButton()
    {
        isDeleteVisible = false;
        StartCoroutine(AnimatePosition(originalX));
        if (deleteButton != null)
            deleteButton.SetActive(false);
    }

    private IEnumerator AnimatePosition(float targetX)
    {
        float startX = rectTransform.anchoredPosition.x;
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            float newX = Mathf.Lerp(startX, targetX, t);
            rectTransform.anchoredPosition = new Vector2(newX, rectTransform.anchoredPosition.y);
            yield return null;
        }

        rectTransform.anchoredPosition = new Vector2(targetX, rectTransform.anchoredPosition.y);
    }

    /// <summary>
    /// 삭제 버튼 클릭 시 호출 (Inspector에서 연결)
    /// </summary>
    public void OnDeleteClicked()
    {
        if (!isMyComment)
        {
            Debug.LogWarning("[CommentItem] 본인 댓글만 삭제할 수 있습니다.");
            return;
        }

        StartCoroutine(DeleteComment());
    }

    private IEnumerator DeleteComment()
    {
        string url = $"{ApiConfig.MAIN_SERVER}/comments/{commentId}";

        if (LoginManager.Instance == null || LoginManager.Instance.CurrentUser == null)
        {
            Debug.LogError("[CommentItem] 로그인이 필요합니다.");
            yield break;
        }

        string userId = LoginManager.Instance.CurrentUser.id;

        using (UnityWebRequest request = UnityWebRequest.Delete(url + $"?user_id={userId}"))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[CommentItem] 댓글 삭제 성공 - id: {commentId}");

                // 애니메이션 후 삭제
                StartCoroutine(AnimateAndDestroy());
            }
            else
            {
                Debug.LogError($"[CommentItem] 댓글 삭제 실패: {request.error}");
                HideDeleteButton();
            }
        }
    }

    private IEnumerator AnimateAndDestroy()
    {
        // 페이드아웃 또는 축소 애니메이션
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1 - (elapsed / duration);
            yield return null;
        }

        Destroy(gameObject);
    }

    #endregion
}

[System.Serializable]
public class CommentLikeResponse
{
    public string action;
    public int like_count;
}
