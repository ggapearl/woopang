using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class CommentItem : MonoBehaviour
{
    public Text usernameText;
    public Text contentText;
    public Text dateText;
    public Text likeCountText;
    public Button likeButton;
    public Image likeIcon;
    public Button contentButton; // For "Read More"

    // Assign these in prefab inspector (e.g., Red heart for liked, Outline for unliked)
    public Sprite likedSprite;
    public Sprite unlikedSprite;

    private int commentId;
    private bool isLiked;
    private int likeCount;
    // private string userId; // LoginManager 사용

    private const string BASE_URL = "https://woopang.com"; // Match CommentManager
    private bool isExpanded = false;

    public void Setup(CommentData data)
    {
        commentId = data.id;
        // userId = SystemInfo.deviceUniqueIdentifier; // LoginManager 사용
        
        if (usernameText != null) usernameText.text = data.username;
        
        if (contentText != null) 
        {
            contentText.text = data.content;
            // 4줄 제한 설정
            contentText.verticalOverflow = VerticalWrapMode.Truncate;
            // Note: In a real scenario, you'd check line count via TextGenerator to decide if button is needed
        }

        if (contentButton != null)
        {
            contentButton.onClick.RemoveAllListeners();
            contentButton.onClick.AddListener(ToggleExpand);
        }

        if (dateText != null) dateText.text = GetRelativeTime(data.created_at);
        
        likeCount = data.like_count;
        isLiked = data.is_liked;
        
        UpdateLikeUI();

        if (likeButton != null)
        {
            likeButton.onClick.RemoveAllListeners();
            likeButton.onClick.AddListener(OnLikeClicked);
        }
    }

    private void ToggleExpand()
    {
        isExpanded = !isExpanded;
        if (contentText != null)
        {
            contentText.verticalOverflow = isExpanded ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
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
        
        if (likeIcon != null)
        {
            // 좋아요 상태면 채워진 하트, 아니면 빈 하트
            likeIcon.sprite = isLiked ? likedSprite : unlikedSprite;
            // 색상은 스프라이트 자체 색상을 따라가거나, 필요시 조정 (여기서는 흰색/회색 유지)
            likeIcon.color = isLiked ? Color.red : Color.white; 
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
        string url = $"{BASE_URL}/comments/like";
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
}

[System.Serializable]
public class CommentLikeResponse
{
    public string action;
    public int like_count;
}
