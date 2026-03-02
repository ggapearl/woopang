using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

/// <summary>
/// 댓글 아이템.
/// - 좋아요: LikeButton(Button.onClick) + ContentArea 더블탭(IPointerClickHandler)
/// - 스와이프 삭제: SwipeToDeleteHandler를 CommentManager에서 런타임 부착
/// - 스크롤: 드래그 이벤트를 부모 ScrollRect로 전파
/// </summary>
public class CommentItem : MonoBehaviour
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

    private int commentId;
    private string commentUserId;
    private bool isLiked;
    private int likeCount;
    private bool isExpanded = false;
    private bool isMyComment = false;

    // 더블터치 감지는 CommentDoubleTapHandler에 위임

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (likeButton != null && likeIcon != null)
        {
            Image heartIcon = likeButton.GetComponentInChildren<Image>();
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
        // 프리팹의 기존 DeleteButton 제거 (SwipeToDeleteHandler가 자체 생성)
        Transform oldDeleteBtn = transform.Find("DeleteButton");
        if (oldDeleteBtn != null)
            Destroy(oldDeleteBtn.gameObject);

        EnsureLayoutComponents();
        SetupDoubleTapOnContent();
    }

    /// <summary>
    /// 루트/자식 레이아웃 컴포넌트의 설정을 런타임에 보장.
    /// </summary>
    private void EnsureLayoutComponents()
    {
        // 루트 HorizontalLayoutGroup
        HorizontalLayoutGroup hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = false;
        }

        // ★ 루트 Image의 raycastTarget을 false로 — LikeButton 클릭이 가로채지 않도록
        Image rootImg = GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.raycastTarget = false;
        }

        // ContentArea의 VerticalLayoutGroup
        Transform contentArea = transform.Find("ContentArea");
        if (contentArea != null)
        {
            VerticalLayoutGroup vlg = contentArea.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.childControlHeight = true;
                vlg.childControlWidth = true;
            }
        }

        // 루트에 ContentSizeFitter가 있는지 확인
        ContentSizeFitter csf = GetComponent<ContentSizeFitter>();
        if (csf != null)
        {
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // LikeButton에 Image(raycast 수신용)와 TargetGraphic 보장
        if (likeButton != null)
        {
            Image likeImg = likeButton.GetComponent<Image>();
            if (likeImg == null)
            {
                likeImg = likeButton.gameObject.AddComponent<Image>();
                likeImg.color = new Color(0, 0, 0, 0);
            }
            likeImg.raycastTarget = true;
            if (likeButton.targetGraphic == null)
                likeButton.targetGraphic = likeImg;

            // LikeButton을 레이아웃에서 분리 → 우측 중앙 고정
            LayoutElement likeLe = likeButton.GetComponent<LayoutElement>();
            if (likeLe != null) likeLe.ignoreLayout = true;

            RectTransform likeRect = likeButton.GetComponent<RectTransform>();
            if (likeRect != null)
            {
                likeRect.anchorMin = new Vector2(1, 0.5f);
                likeRect.anchorMax = new Vector2(1, 0.5f);
                likeRect.pivot = new Vector2(1, 0.5f);
                likeRect.anchoredPosition = new Vector2(-5, 0);
                likeRect.sizeDelta = new Vector2(80, 100);
            }
        }
    }

    /// <summary>
    /// ContentArea에 더블탭 감지용 핸들러를 설치.
    /// CommentDoubleTapHandler(IPointerClickHandler만 구현)를 사용하여
    /// 드래그 이벤트가 부모(SwipeToDeleteHandler/ScrollRect)로 정상 전파됨.
    /// </summary>
    private void SetupDoubleTapOnContent()
    {
        Transform contentArea = transform.Find("ContentArea");
        if (contentArea == null) return;

        // ContentArea에 Image (raycast target) 보장
        Image caImg = contentArea.GetComponent<Image>();
        if (caImg == null)
        {
            caImg = contentArea.gameObject.AddComponent<Image>();
            caImg.color = new Color(0, 0, 0, 0);
        }
        caImg.raycastTarget = true;

        // CommentDoubleTapHandler 부착 (IPointerClickHandler만 — 드래그 버블링 OK)
        CommentDoubleTapHandler doubleTap = contentArea.GetComponent<CommentDoubleTapHandler>();
        if (doubleTap == null)
            doubleTap = contentArea.gameObject.AddComponent<CommentDoubleTapHandler>();

        doubleTap.Initialize(() => OnLikeClicked());
    }

    public void Setup(CommentData data)
    {
        commentId = data.id;
        commentUserId = data.user_id;

        if (LoginManager.Instance != null && LoginManager.Instance.CurrentUser != null)
        {
            isMyComment = LoginManager.Instance.CurrentUser.id == data.user_id;
        }

        if (usernameText != null)
        {
            usernameText.text = data.username;
        }

        if (contentText != null)
        {
            contentText.text = data.content;
            contentText.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform contentRect = contentText.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                LayoutElement layoutElement = contentText.GetComponent<LayoutElement>();
                if (layoutElement == null)
                    layoutElement = contentText.gameObject.AddComponent<LayoutElement>();

                layoutElement.preferredHeight = -1;
                layoutElement.minHeight = 20;
            }
        }

        if (contentButton != null)
        {
            contentButton.onClick.RemoveAllListeners();
            contentButton.onClick.AddListener(ToggleExpand);
        }

        if (dateText != null)
        {
            dateText.text = GetRelativeTime(data.created_at);
        }

        likeCount = data.like_count;
        isLiked = data.is_liked;

        UpdateLikeUI();

        if (likeButton != null)
        {
            likeButton.onClick.RemoveAllListeners();
            likeButton.onClick.AddListener(OnLikeClicked);
        }

        StartCoroutine(ForceLayoutUpdate());
    }

    /// <summary>
    /// 내 댓글인지 여부 반환 (SwipeToDeleteHandler 설정용)
    /// </summary>
    public bool IsMyComment => isMyComment;

    /// <summary>
    /// 댓글 ID 반환 (삭제 API용)
    /// </summary>
    public int CommentId => commentId;

    private IEnumerator ForceLayoutUpdate()
    {
        yield return null;
        RebuildAllLayouts();
        yield return null;
        yield return null;
        RebuildAllLayouts();
    }

    private void RebuildAllLayouts()
    {
        var fitters = GetComponentsInChildren<ContentSizeFitter>(true);
        foreach (var fitter in fitters)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(fitter.GetComponent<RectTransform>());
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);

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

            LayoutElement layoutElement = contentText.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
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

        if (likeButton != null)
        {
            Transform heartIconTransform = likeButton.transform.Find("HeartIcon");
            Image likeIconImage = null;

            if (heartIconTransform != null)
            {
                likeIconImage = heartIconTransform.GetComponent<Image>();
            }
            else
            {
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
            return count.ToString();
        }
        else if (lang == SystemLanguage.Chinese || lang == SystemLanguage.ChineseSimplified || lang == SystemLanguage.ChineseTraditional)
        {
            if (count >= 100000000) return $"{(count / 100000000f):0.#}亿";
            if (count >= 10000) return $"{(count / 10000f):0.#}万";
            return count.ToString();
        }
        else
        {
            if (count >= 1000000) return $"{(count / 1000000f):0.#}m";
            return $"{(count / 1000f):0.#}k";
        }
    }

    public void OnLikeClicked()
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup();
            return;
        }

        // Optimistic UI Update
        isLiked = !isLiked;
        likeCount += isLiked ? 1 : -1;
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
                var response = JsonUtility.FromJson<CommentLikeResponse>(request.downloadHandler.text);
                likeCount = response.like_count;
                UpdateLikeUI();
            }
            else
            {
                isLiked = !isLiked;
                likeCount += isLiked ? 1 : -1;
                UpdateLikeUI();
                Debug.LogError($"Like failed: {request.error}");
            }
        }
    }

    /// <summary>
    /// 댓글 삭제 API 호출 후 애니메이션 삭제
    /// </summary>
    public void DeleteThisComment()
    {
        if (!isMyComment) return;
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
                StartCoroutine(AnimateAndDestroy());
            }
            else
            {
                Debug.LogError($"[CommentItem] 댓글 삭제 실패: {request.error}");
                // SwipeToDeleteHandler 복원
                var handler = GetComponent<SwipeToDeleteHandler>();
                if (handler != null) handler.ResetPosition();
            }
        }
    }

    private IEnumerator AnimateAndDestroy()
    {
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
}

[System.Serializable]
public class CommentLikeResponse
{
    public string action;
    public int like_count;
}
