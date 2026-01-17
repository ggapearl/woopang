using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// 프로필 관리 시스템
/// - 프로필 조회/표시
/// - 팔로우/언팔로우
/// - 팔로워/팔로잉 목록
/// </summary>
public class ProfileManager : MonoBehaviour
{
    public static ProfileManager Instance { get; private set; }

    [Header("Mini Profile (화면 상단)")]
    [Tooltip("화면 상단에 표시되는 미니 프로필")]
    public GameObject miniProfilePanel;
    public Image miniAvatarImage;
    public Text miniUsernameText;
    public Button miniProfileButton;  // 클릭 시 전체 프로필 열기

    [Header("Full Profile Panel")]
    [Tooltip("전체 프로필 패널")]
    public GameObject fullProfilePanel;
    public Image avatarImage;
    public Text usernameText;
    public Text bioText;
    public Text followersCountText;
    public Text followingCountText;
    public Text likesCountText;       // 좋아요 수
    public Button followersButton;
    public Button followingButton;
    public Button followButton;
    public Button likeButton;         // 좋아요 버튼
    public Button editProfileButton;  // 웹으로 이동
    public Button closeButton;

    [Header("Follow List Panel")]
    public GameObject followListPanel;
    public Text followListTitleText;
    public Transform followListContent;
    public GameObject followListItemPrefab;
    public Button followListCloseButton;

    [Header("Visibility Settings (내 프로필에서만 표시)")]
    [Tooltip("아바타 공개상태 텍스트")]
    public Text visibilityStatusText;
    [Tooltip("아바타 이미지 버튼 (터치로 공개상태 변경)")]
    public Button avatarButton;
    [Tooltip("아바타 테두리 이미지 (FullProfile)")]
    public Image avatarOutlineImage;
    [Tooltip("미니 프로필 아바타 테두리 이미지")]
    public Image miniAvatarOutlineImage;

    // 공개상태별 테두리 색상
    private static readonly Color PUBLIC_OUTLINE_COLOR = new Color(0.914f, 0.325f, 0.514f, 1f);      // #e95383 핑크
    private static readonly Color FOLLOWING_ONLY_OUTLINE_COLOR = new Color(1f, 0.843f, 0f, 1f);      // #FFD700 금색/노란색
    private static readonly Color PRIVATE_OUTLINE_COLOR = new Color(0.5f, 0.5f, 0.5f, 1f);           // #808080 회색

    [Header("Settings")]
    public Sprite defaultAvatarSprite;

#if UNITY_EDITOR
    [Header("=== 에디터 테스트용 ===")]
    [Tooltip("Inspector에서 체크하면 프로필 새로고침")]
    public bool editorRefreshProfile = false;
#endif

    private string BASE_URL => ApiConfig.MAIN_SERVER;

    // 현재 표시 중인 프로필
    private ProfileData currentProfile;
    private bool isMyProfile = false;

    // 이미지 캐시
    private Dictionary<string, Sprite> avatarCache = new Dictionary<string, Sprite>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 버튼 리스너
        if (miniProfileButton != null)
            miniProfileButton.onClick.AddListener(OnMiniProfileClicked);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseFullProfile);
        if (followersButton != null)
            followersButton.onClick.AddListener(() => ShowFollowList("followers"));
        if (followingButton != null)
            followingButton.onClick.AddListener(() => ShowFollowList("following"));
        if (followButton != null)
            followButton.onClick.AddListener(OnFollowButtonClicked);
        if (editProfileButton != null)
            editProfileButton.onClick.AddListener(OpenEditProfileWeb);
        if (followListCloseButton != null)
            followListCloseButton.onClick.AddListener(CloseFollowList);
        if (likeButton != null)
            likeButton.onClick.AddListener(OnLikeButtonClicked);
        if (avatarButton != null)
            avatarButton.onClick.AddListener(OnAvatarClicked);
    }

    void Start()
    {
        // 로그인 상태 변경 이벤트 구독
        if (LoginManager.Instance != null)
        {
            LoginManager.Instance.OnLoginStateChanged += OnLoginStateChanged;

            // 이미 로그인 되어있으면 프로필 로드
            if (LoginManager.Instance.IsLoggedIn)
            {
                LoadMyProfile();
            }
        }

        // 앱 시작 시 미니 프로필 아웃라인 색상 초기화
        StartCoroutine(InitializeMiniProfileOutlineDelayed());
    }

    /// <summary>
    /// P2PManager 초기화 대기 후 미니 프로필 아웃라인 설정
    /// </summary>
    private IEnumerator InitializeMiniProfileOutlineDelayed()
    {
        // P2PManager 초기화 대기
        yield return new WaitForSeconds(0.5f);

        RefreshMiniProfileOutline();
    }

    void OnDestroy()
    {
        if (LoginManager.Instance != null)
        {
            LoginManager.Instance.OnLoginStateChanged -= OnLoginStateChanged;
        }
    }

    // 프로필 편집 페이지를 열었는지 추적
    private bool openedProfileEdit = false;

    /// <summary>
    /// 앱이 포그라운드로 돌아올 때 프로필 새로고침
    /// (프로필 편집 웹페이지에서 돌아온 경우에만 변경사항 반영)
    /// </summary>
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && openedProfileEdit && LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn)
        {
            Debug.Log("[ProfileManager] Returned from profile edit - refreshing profile");
            ClearAvatarCache();
            LoadMyProfile();
            openedProfileEdit = false;
        }
    }

#if UNITY_EDITOR
    void Update()
    {
        // 에디터에서 Inspector 체크박스로 프로필 새로고침
        if (editorRefreshProfile)
        {
            editorRefreshProfile = false;
            Debug.Log("[ProfileManager] Editor: Manual profile refresh triggered");
            ClearAvatarCache();
            LoadMyProfile();
        }
    }
#endif

    private void OnLoginStateChanged(bool isLoggedIn)
    {
        if (isLoggedIn)
        {
            LoadMyProfile();
        }
        else
        {
            ClearMiniProfile();
        }
    }

    #region My Profile

    /// <summary>
    /// 내 프로필 로드 (로그인 후 호출)
    /// </summary>
    public void LoadMyProfile()
    {
        if (LoginManager.Instance == null || LoginManager.Instance.CurrentUser == null)
            return;

        string userId = LoginManager.Instance.CurrentUser.id;
        StartCoroutine(FetchProfile(userId, (profile) =>
        {
            if (profile != null)
            {
                UpdateMiniProfile(profile);
            }
        }));
    }

    private void UpdateMiniProfile(ProfileData profile)
    {
        if (miniUsernameText != null)
            miniUsernameText.text = profile.username;

        if (miniAvatarImage != null && !string.IsNullOrEmpty(profile.avatar_url))
        {
            StartCoroutine(LoadAvatarImage(profile.avatar_url, miniAvatarImage));
        }
        else if (miniAvatarImage != null && defaultAvatarSprite != null)
        {
            miniAvatarImage.sprite = defaultAvatarSprite;
        }

        if (miniProfilePanel != null)
            miniProfilePanel.SetActive(true);
    }

    private void ClearMiniProfile()
    {
        if (miniUsernameText != null)
            miniUsernameText.text = "";

        if (miniAvatarImage != null && defaultAvatarSprite != null)
            miniAvatarImage.sprite = defaultAvatarSprite;

        if (miniProfilePanel != null)
            miniProfilePanel.SetActive(false);
    }

    #endregion

    #region Show Profile

    /// <summary>
    /// 프로필 패널 열기 (user_id로)
    /// </summary>
    public void ShowProfile(string userId)
    {
        StartCoroutine(FetchProfile(userId, (profile) =>
        {
            if (profile != null)
            {
                ShowProfilePanel(profile);
            }
        }));
    }

    /// <summary>
    /// 미니 프로필 클릭 시 (내 프로필 열기)
    /// </summary>
    private void OnMiniProfileClicked()
    {
        if (LoginManager.Instance != null && LoginManager.Instance.CurrentUser != null)
        {
            ShowProfile(LoginManager.Instance.CurrentUser.id);
        }
    }

    private void ShowProfilePanel(ProfileData profile)
    {
        currentProfile = profile;

        // 내 프로필인지 확인
        isMyProfile = LoginManager.Instance != null &&
                      LoginManager.Instance.CurrentUser != null &&
                      LoginManager.Instance.CurrentUser.id == profile.id;

        // UI 업데이트
        if (usernameText != null) usernameText.text = profile.username;
        if (bioText != null) bioText.text = string.IsNullOrEmpty(profile.bio) ? "" : profile.bio;
        if (followersCountText != null) followersCountText.text = profile.followers_count.ToString();
        if (followingCountText != null) followingCountText.text = profile.following_count.ToString();
        if (likesCountText != null) likesCountText.text = profile.likes_count.ToString();

        // 아바타 이미지
        if (avatarImage != null)
        {
            if (!string.IsNullOrEmpty(profile.avatar_url))
            {
                StartCoroutine(LoadAvatarImage(profile.avatar_url, avatarImage));
            }
            else if (defaultAvatarSprite != null)
            {
                avatarImage.sprite = defaultAvatarSprite;
            }
        }

        // 버튼 상태
        if (editProfileButton != null) editProfileButton.gameObject.SetActive(isMyProfile);
        if (followButton != null)
        {
            followButton.gameObject.SetActive(!isMyProfile && !LoginManager.Instance.IsGuest);
            if (!isMyProfile)
            {
                UpdateFollowButtonState();
            }
        }

        // 좋아요 버튼 상태
        if (likeButton != null)
        {
            likeButton.gameObject.SetActive(!isMyProfile && !LoginManager.Instance.IsGuest);
            if (!isMyProfile)
            {
                UpdateLikeButtonState();
            }
        }

        // 아바타 공개상태 UI 초기화
        InitializeVisibilityUI();

        // 패널 표시
        if (fullProfilePanel != null)
            fullProfilePanel.SetActive(true);
    }

    public void CloseFullProfile()
    {
        if (fullProfilePanel != null)
            fullProfilePanel.SetActive(false);

        currentProfile = null;
    }

    #endregion

    #region Follow System

    private void UpdateFollowButtonState()
    {
        if (followButton == null || currentProfile == null) return;

        string myId = LoginManager.Instance?.CurrentUser?.id;
        if (string.IsNullOrEmpty(myId)) return;

        StartCoroutine(CheckIsFollowing(myId, currentProfile.id, (isFollowing) =>
        {
            Text btnText = followButton.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = isFollowing ? GetLocalizedText("unfollow") : GetLocalizedText("follow");
            }

            // 버튼 색상 변경
            Image btnImage = followButton.GetComponent<Image>();
            if (btnImage != null)
            {
                btnImage.color = isFollowing ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.6f, 1f);
            }
        }));
    }

    private void OnFollowButtonClicked()
    {
        if (currentProfile == null || LoginManager.Instance?.CurrentUser == null) return;

        string myId = LoginManager.Instance.CurrentUser.id;
        string targetId = currentProfile.id;

        StartCoroutine(CheckIsFollowing(myId, targetId, (isFollowing) =>
        {
            if (isFollowing)
            {
                StartCoroutine(UnfollowUser(myId, targetId));
            }
            else
            {
                StartCoroutine(FollowUser(myId, targetId));
            }
        }));
    }

    private IEnumerator FollowUser(string followerId, string followingId)
    {
        string url = $"{BASE_URL}/api/follow";
        string json = $"{{\"follower_id\":\"{followerId}\",\"following_id\":\"{followingId}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[ProfileManager] Followed: {followingId}");
                // 카운트 업데이트
                if (currentProfile != null)
                {
                    currentProfile.followers_count++;
                    if (followersCountText != null)
                        followersCountText.text = currentProfile.followers_count.ToString();
                }
                UpdateFollowButtonState();
            }
        }
    }

    private IEnumerator UnfollowUser(string followerId, string followingId)
    {
        string url = $"{BASE_URL}/api/unfollow";
        string json = $"{{\"follower_id\":\"{followerId}\",\"following_id\":\"{followingId}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[ProfileManager] Unfollowed: {followingId}");
                // 카운트 업데이트
                if (currentProfile != null)
                {
                    currentProfile.followers_count = Mathf.Max(0, currentProfile.followers_count - 1);
                    if (followersCountText != null)
                        followersCountText.text = currentProfile.followers_count.ToString();
                }
                UpdateFollowButtonState();
            }
        }
    }

    private IEnumerator CheckIsFollowing(string followerId, string followingId, Action<bool> callback)
    {
        string url = $"{BASE_URL}/api/is_following?follower_id={followerId}&following_id={followingId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            bool isFollowing = false;
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<IsFollowingResponse>(request.downloadHandler.text);
                    isFollowing = response.is_following;
                }
                catch { }
            }

            callback?.Invoke(isFollowing);
        }
    }

    #endregion

    #region Like System

    private void UpdateLikeButtonState()
    {
        if (likeButton == null || currentProfile == null) return;

        string myId = LoginManager.Instance?.CurrentUser?.id;
        if (string.IsNullOrEmpty(myId)) return;

        StartCoroutine(CheckIsLiked(myId, currentProfile.id, (isLiked) =>
        {
            Text btnText = likeButton.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = isLiked ? GetLocalizedText("liked") : GetLocalizedText("like");
            }

            // 버튼 색상 변경 (좋아요한 상태면 빨간색)
            Image btnImage = likeButton.GetComponent<Image>();
            if (btnImage != null)
            {
                btnImage.color = isLiked ? new Color(1f, 0.3f, 0.3f) : new Color(0.9f, 0.9f, 0.9f);
            }
        }));
    }

    private void OnLikeButtonClicked()
    {
        if (currentProfile == null || LoginManager.Instance?.CurrentUser == null) return;

        string myId = LoginManager.Instance.CurrentUser.id;
        string targetId = currentProfile.id;

        StartCoroutine(CheckIsLiked(myId, targetId, (isLiked) =>
        {
            if (isLiked)
            {
                StartCoroutine(UnlikeUser(myId, targetId));
            }
            else
            {
                StartCoroutine(LikeUser(myId, targetId));
            }
        }));
    }

    private IEnumerator LikeUser(string likerId, string likedId)
    {
        string url = $"{BASE_URL}/api/like";
        string json = $"{{\"liker_id\":\"{likerId}\",\"liked_id\":\"{likedId}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[ProfileManager] Liked: {likedId}");
                // 카운트 업데이트
                if (currentProfile != null)
                {
                    currentProfile.likes_count++;
                    if (likesCountText != null)
                        likesCountText.text = currentProfile.likes_count.ToString();
                }
                UpdateLikeButtonState();
            }
        }
    }

    private IEnumerator UnlikeUser(string likerId, string likedId)
    {
        string url = $"{BASE_URL}/api/unlike";
        string json = $"{{\"liker_id\":\"{likerId}\",\"liked_id\":\"{likedId}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[ProfileManager] Unliked: {likedId}");
                // 카운트 업데이트
                if (currentProfile != null)
                {
                    currentProfile.likes_count = Mathf.Max(0, currentProfile.likes_count - 1);
                    if (likesCountText != null)
                        likesCountText.text = currentProfile.likes_count.ToString();
                }
                UpdateLikeButtonState();
            }
        }
    }

    private IEnumerator CheckIsLiked(string likerId, string likedId, Action<bool> callback)
    {
        string url = $"{BASE_URL}/api/is_liked?liker_id={likerId}&liked_id={likedId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            bool isLiked = false;
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<IsLikedResponse>(request.downloadHandler.text);
                    isLiked = response.is_liked;
                }
                catch { }
            }

            callback?.Invoke(isLiked);
        }
    }

    #endregion

    #region Follow List

    private void ShowFollowList(string type)
    {
        if (currentProfile == null) return;

        if (type == "followers")
        {
            if (followListTitleText != null)
                followListTitleText.text = GetLocalizedText("followers_title");
            StartCoroutine(FetchFollowers(currentProfile.id));
        }
        else
        {
            if (followListTitleText != null)
                followListTitleText.text = GetLocalizedText("following_title");
            StartCoroutine(FetchFollowing(currentProfile.id));
        }
    }

    private IEnumerator FetchFollowers(string userId)
    {
        string url = $"{BASE_URL}/api/followers?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<FollowListResponse>(request.downloadHandler.text);
                PopulateFollowList(response.followers);
            }
        }
    }

    private IEnumerator FetchFollowing(string userId)
    {
        string url = $"{BASE_URL}/api/following?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<FollowListResponse>(request.downloadHandler.text);
                PopulateFollowList(response.following);
            }
        }
    }

    private void PopulateFollowList(FollowUser[] users)
    {
        // 기존 항목 제거
        if (followListContent != null)
        {
            foreach (Transform child in followListContent)
            {
                Destroy(child.gameObject);
            }
        }

        // 항목 추가
        if (users != null && followListItemPrefab != null && followListContent != null)
        {
            foreach (var user in users)
            {
                GameObject item = Instantiate(followListItemPrefab, followListContent);
                SetupFollowListItem(item, user);
            }
        }

        // 패널 표시
        if (followListPanel != null)
            followListPanel.SetActive(true);
    }

    private void SetupFollowListItem(GameObject item, FollowUser user)
    {
        // 아바타
        Image avatar = item.transform.Find("Avatar")?.GetComponent<Image>();
        if (avatar != null && !string.IsNullOrEmpty(user.avatar_url))
        {
            StartCoroutine(LoadAvatarImage(user.avatar_url, avatar));
        }

        // 이름
        Text nameText = item.transform.Find("Username")?.GetComponent<Text>();
        if (nameText != null)
        {
            nameText.text = user.username;
        }

        // 클릭 시 해당 프로필 열기
        Button btn = item.GetComponent<Button>();
        if (btn != null)
        {
            string userId = user.id;
            btn.onClick.AddListener(() =>
            {
                CloseFollowList();
                ShowProfile(userId);
            });
        }
    }

    public void CloseFollowList()
    {
        if (followListPanel != null)
            followListPanel.SetActive(false);
    }

    #endregion

    #region API Calls

    private IEnumerator FetchProfile(string userId, Action<ProfileData> callback)
    {
        // 캐시 방지를 위해 타임스탬프 추가
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string url = $"{BASE_URL}/api/user/profile?user_id={userId}&_t={timestamp}";
        Debug.Log($"[ProfileManager] Fetching profile: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            ProfileData profile = null;
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    profile = JsonUtility.FromJson<ProfileData>(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ProfileManager] Parse error: {e.Message}");
                }
            }

            callback?.Invoke(profile);
        }
    }

    private IEnumerator LoadAvatarImage(string url, Image targetImage)
    {
        // URL이 비어있거나 null이면 기본 아바타 사용
        if (string.IsNullOrEmpty(url))
        {
            if (defaultAvatarSprite != null)
                targetImage.sprite = defaultAvatarSprite;
            yield break;
        }

        // 상대 경로를 절대 경로로 변환
        string fullUrl = url;
        if (!url.StartsWith("http"))
        {
            fullUrl = BASE_URL + (url.StartsWith("/") ? url : "/" + url);
        }

        // 캐시 확인
        if (avatarCache.TryGetValue(fullUrl, out Sprite cached))
        {
            targetImage.sprite = cached;
            yield break;
        }

        Debug.Log($"[ProfileManager] Loading avatar: {fullUrl}");

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));

                avatarCache[fullUrl] = sprite;
                targetImage.sprite = sprite;
                Debug.Log($"[ProfileManager] Avatar loaded successfully: {fullUrl}");
            }
            else
            {
                Debug.LogWarning($"[ProfileManager] Failed to load avatar: {request.error}");
                if (defaultAvatarSprite != null)
                {
                    targetImage.sprite = defaultAvatarSprite;
                }
            }
        }
    }

    /// <summary>
    /// 아바타 캐시 클리어 (프로필 업데이트 후 새로고침용)
    /// </summary>
    public void ClearAvatarCache()
    {
        avatarCache.Clear();
        Debug.Log("[ProfileManager] Avatar cache cleared");
    }

    #endregion

    #region Avatar Visibility Toggle

    /// <summary>
    /// 아바타 클릭 시 공개상태 변경 (내 프로필에서만 동작)
    /// </summary>
    private void OnAvatarClicked()
    {
        if (!isMyProfile) return;

        // 현재 공개상태 순환: 전체 -> 팔로잉에게만 -> 비공개 -> 전체
        LocationVisibilityMode currentMode = LocationVisibilityMode.Public;
        if (P2PManager.Instance != null)
        {
            currentMode = P2PManager.Instance.GetLocationVisibility();
        }

        LocationVisibilityMode newMode;
        switch (currentMode)
        {
            case LocationVisibilityMode.Public:
                newMode = LocationVisibilityMode.FollowingOnly;
                break;
            case LocationVisibilityMode.FollowingOnly:
                newMode = LocationVisibilityMode.Private;
                break;
            default:
                newMode = LocationVisibilityMode.Public;
                break;
        }

        // P2PManager에 새 모드 설정
        if (P2PManager.Instance != null)
        {
            P2PManager.Instance.SetLocationVisibility(newMode);
        }

        // UI 업데이트
        UpdateVisibilityStatusText(newMode);
        UpdateAvatarOutlineColor(newMode);

        Debug.Log($"[ProfileManager] Avatar visibility changed to: {newMode}");
    }

    /// <summary>
    /// 공개상태 텍스트 업데이트
    /// </summary>
    private void UpdateVisibilityStatusText(LocationVisibilityMode mode)
    {
        if (visibilityStatusText == null) return;

        string statusText;
        Color textColor;

        switch (mode)
        {
            case LocationVisibilityMode.Public:
                statusText = GetLocalizedVisibilityText("public");
                textColor = PUBLIC_OUTLINE_COLOR; // 핑크 (테두리 색상과 동일)
                break;
            case LocationVisibilityMode.FollowingOnly:
                statusText = GetLocalizedVisibilityText("followingonly");
                textColor = FOLLOWING_ONLY_OUTLINE_COLOR; // 노란색
                break;
            default: // Private
                statusText = GetLocalizedVisibilityText("private");
                textColor = PRIVATE_OUTLINE_COLOR; // 회색
                break;
        }

        visibilityStatusText.text = statusText;
        visibilityStatusText.color = textColor;
    }

    /// <summary>
    /// 아바타 테두리 색상 업데이트 (FullProfile + MiniProfile)
    /// </summary>
    private void UpdateAvatarOutlineColor(LocationVisibilityMode mode)
    {
        Color outlineColor;

        switch (mode)
        {
            case LocationVisibilityMode.Public:
                outlineColor = PUBLIC_OUTLINE_COLOR; // 핑크 #e95383
                break;
            case LocationVisibilityMode.FollowingOnly:
                outlineColor = FOLLOWING_ONLY_OUTLINE_COLOR; // 노란색 #FFD700
                break;
            default: // Private
                outlineColor = PRIVATE_OUTLINE_COLOR; // 회색 #808080
                break;
        }

        // FullProfile 아바타 테두리
        if (avatarOutlineImage != null)
        {
            avatarOutlineImage.color = outlineColor;
        }

        // MiniProfile 아바타 테두리
        if (miniAvatarOutlineImage != null)
        {
            miniAvatarOutlineImage.color = outlineColor;
        }

        Debug.Log($"[ProfileManager] Avatar outline color updated to: {mode} ({ColorUtility.ToHtmlStringRGB(outlineColor)})");
    }

    /// <summary>
    /// 공개상태 텍스트 다국어 지원
    /// </summary>
    private string GetLocalizedVisibilityText(string mode)
    {
        string lang = GetCurrentLanguageCode();

        switch (mode)
        {
            case "public":
                return lang == "ko" ? "내 아바타 공개상태 : 전체" :
                       lang == "ja" ? "アバター公開: 全員" :
                       lang == "zh" ? "头像可见性: 全部" :
                       "Avatar Visibility: Public";
            case "followingonly":
                return lang == "ko" ? "내 아바타 공개상태 : 팔로잉에게만" :
                       lang == "ja" ? "アバター公開: フォロー中のみ" :
                       lang == "zh" ? "头像可见性: 仅关注者" :
                       "Avatar Visibility: Following Only";
            case "private":
                return lang == "ko" ? "내 아바타 공개상태 : 비공개" :
                       lang == "ja" ? "アバター公開: 非公開" :
                       lang == "zh" ? "头像可见性: 私密" :
                       "Avatar Visibility: Private";
            default:
                return mode;
        }
    }

    /// <summary>
    /// 프로필 패널 열 때 공개상태 UI 초기화
    /// </summary>
    private void InitializeVisibilityUI()
    {
        // 내 프로필일 때만 공개상태 텍스트 표시
        if (visibilityStatusText != null)
        {
            visibilityStatusText.gameObject.SetActive(isMyProfile);
        }

        if (isMyProfile && P2PManager.Instance != null)
        {
            LocationVisibilityMode currentMode = P2PManager.Instance.GetLocationVisibility();
            UpdateVisibilityStatusText(currentMode);
            UpdateAvatarOutlineColor(currentMode);
        }
    }

    /// <summary>
    /// 미니 프로필 초기화 시 아웃라인 색상도 업데이트
    /// </summary>
    public void RefreshMiniProfileOutline()
    {
        if (P2PManager.Instance != null && miniAvatarOutlineImage != null)
        {
            LocationVisibilityMode currentMode = P2PManager.Instance.GetLocationVisibility();
            UpdateAvatarOutlineColor(currentMode);
        }
    }

    #endregion

    #region Edit Profile

    /// <summary>
    /// 프로필 편집 웹페이지 열기
    /// 앱에서만 접근 가능하며, user_id와 언어 설정을 파라미터로 전달
    /// </summary>
    private void OpenEditProfileWeb()
    {
        if (LoginManager.Instance == null || LoginManager.Instance.CurrentUser == null)
        {
            Debug.LogWarning("[ProfileManager] Cannot open edit profile - not logged in");
            return;
        }

        string userId = LoginManager.Instance.CurrentUser.id;
        string lang = GetCurrentLanguageCode();

        // URL with token (user_id) and language
        string editUrl = $"{BASE_URL}/profile/edit?token={userId}&lang={lang}";
        Debug.Log($"[ProfileManager] Opening profile edit: {editUrl}");

        // 프로필 편집 페이지를 열었음을 표시 (돌아올 때 새로고침하기 위해)
        openedProfileEdit = true;

        Application.OpenURL(editUrl);
    }

    /// <summary>
    /// 현재 시스템 언어 코드 반환
    /// </summary>
    private string GetCurrentLanguageCode()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.Japanese: return "ja";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return "zh";
            case SystemLanguage.Spanish: return "es";
            default: return "en";
        }
    }

    #endregion

    #region Localization

    private string GetLocalizedText(string key)
    {
        string lang = "en";
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: lang = "ko"; break;
            case SystemLanguage.Japanese: lang = "ja"; break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: lang = "zh"; break;
        }

        switch (key)
        {
            case "follow":
                return lang == "ko" ? "팔로우" : lang == "ja" ? "フォロー" : lang == "zh" ? "关注" : "Follow";
            case "unfollow":
                return lang == "ko" ? "팔로우 취소" : lang == "ja" ? "フォロー解除" : lang == "zh" ? "取消关注" : "Unfollow";
            case "followers_title":
                return lang == "ko" ? "팔로워" : lang == "ja" ? "フォロワー" : lang == "zh" ? "粉丝" : "Followers";
            case "following_title":
                return lang == "ko" ? "팔로잉" : lang == "ja" ? "フォロー中" : lang == "zh" ? "关注" : "Following";
            case "like":
                return lang == "ko" ? "좋아요" : lang == "ja" ? "いいね" : lang == "zh" ? "点赞" : "Like";
            case "liked":
                return lang == "ko" ? "좋아요 취소" : lang == "ja" ? "いいね解除" : lang == "zh" ? "取消点赞" : "Unlike";
            default:
                return key;
        }
    }

    #endregion
}

#region Data Classes

[System.Serializable]
public class ProfileData
{
    public string id;
    public string username;
    public string email;
    public string avatar_url;
    public string bio;
    public string phone;
    public string instagram_id;
    public string facebook_id;
    public string x_id;
    public int followers_count;
    public int following_count;
    public int likes_count;
    public string created_at;
}

[System.Serializable]
public class IsFollowingResponse
{
    public bool is_following;
}

[System.Serializable]
public class IsLikedResponse
{
    public bool is_liked;
}

[System.Serializable]
public class FollowListResponse
{
    public FollowUser[] followers;
    public FollowUser[] following;
    public int count;
}

[System.Serializable]
public class FollowUser
{
    public string id;
    public string username;
    public string avatar_url;
}

#endregion
