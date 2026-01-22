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
    public Button followersButton;
    public Button followingButton;
    public Button followButton;
    public Text followButtonText;  // 팔로우 버튼 텍스트
    public Button editProfileButton;  // 내 프로필: 웹으로 이동, 다른 사람: DM 보내기
    public Text editProfileButtonText;  // 버튼 텍스트 (동적 변경용)
    public Button closeButton;

    [Header("SNS Icons (팔로우 버튼 영역에 표시)")]
    [Tooltip("Instagram 아이콘 버튼")]
    public Button instagramButton;
    [Tooltip("X (Twitter) 아이콘 버튼")]
    public Button xButton;
    [Tooltip("Facebook 아이콘 버튼")]
    public Button facebookButton;
    [Tooltip("SNS 아이콘 부모 오브젝트 (내 프로필에서 팔로우 버튼 대신 표시)")]
    public GameObject snsIconsContainer;

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
    [Tooltip("다른 유저 프로필 테스트용 - user_id 입력")]
    public string editorTestUserId = "";
    [Tooltip("체크하면 위 user_id의 프로필 열기")]
    public bool editorOpenTestProfile = false;

    [Header("SNS 테스트")]
    [Tooltip("테스트용 Instagram ID")]
    public string testInstagramId = "";
    [Tooltip("테스트용 X (Twitter) ID")]
    public string testXId = "";
    [Tooltip("테스트용 Facebook ID")]
    public string testFacebookId = "";
    [Tooltip("체크하면 테스트 SNS 아이콘 표시")]
    public bool useTestSnsData = false;
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
        if (avatarButton != null)
            avatarButton.onClick.AddListener(OnAvatarClicked);

        // SNS 아이콘 버튼 리스너
        if (instagramButton != null)
            instagramButton.onClick.AddListener(OnInstagramClicked);
        if (xButton != null)
            xButton.onClick.AddListener(OnXClicked);
        if (facebookButton != null)
            facebookButton.onClick.AddListener(OnFacebookClicked);
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

        // SNS 아이콘 컨테이너 초기화 (Inspector에서 연결 안 되어 있으면 동적 생성)
        StartCoroutine(InitializeSnsContainerDelayed());
    }

    /// <summary>
    /// SNS 컨테이너 초기화 (FullProfilePanel 로드 후)
    /// </summary>
    private IEnumerator InitializeSnsContainerDelayed()
    {
        yield return new WaitForSeconds(0.3f);

        // snsIconsContainer가 없으면 미리 생성
        if (snsIconsContainer == null && fullProfilePanel != null)
        {
            CreateSnsIconsContainer();
            Debug.Log("[ProfileManager] SNS 컨테이너 사전 초기화 완료");
        }

        // 버튼들도 미리 생성 (비활성화 상태로)
        if (snsIconsContainer != null)
        {
            if (instagramButton == null)
            {
                instagramButton = CreateSnsIconButton("Instagram", new Color(0.88f, 0.19f, 0.42f), 0);
                if (instagramButton != null) instagramButton.gameObject.SetActive(false);
            }
            if (xButton == null)
            {
                xButton = CreateSnsIconButton("X", Color.black, 1);
                if (xButton != null) xButton.gameObject.SetActive(false);
            }
            if (facebookButton == null)
            {
                facebookButton = CreateSnsIconButton("Facebook", new Color(0.23f, 0.35f, 0.60f), 2);
                if (facebookButton != null) facebookButton.gameObject.SetActive(false);
            }

            // 컨테이너 숨김 (프로필 열 때 표시)
            snsIconsContainer.SetActive(false);
        }
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

        // 에디터에서 다른 유저 프로필 열기
        if (editorOpenTestProfile)
        {
            editorOpenTestProfile = false;
            if (!string.IsNullOrEmpty(editorTestUserId))
            {
                Debug.Log($"[ProfileManager] Editor: Opening test profile for user_id: {editorTestUserId}");
                ShowProfile(editorTestUserId);
            }
            else
            {
                Debug.LogWarning("[ProfileManager] Editor: editorTestUserId is empty!");
            }
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
        Debug.Log("[ProfileManager] OnMiniProfileClicked called");

        if (LoginManager.Instance == null)
        {
            Debug.LogError("[ProfileManager] LoginManager.Instance is NULL!");
            return;
        }

        if (LoginManager.Instance.CurrentUser == null)
        {
            Debug.LogError("[ProfileManager] CurrentUser is NULL! IsLoggedIn=" + LoginManager.Instance.IsLoggedIn);
            return;
        }

        Debug.Log($"[ProfileManager] Opening profile for user: {LoginManager.Instance.CurrentUser.id}");
        ShowProfile(LoginManager.Instance.CurrentUser.id);
    }

    private void ShowProfilePanel(ProfileData profile)
    {
        Debug.Log($"[ProfileManager] ShowProfilePanel called for: {profile?.username ?? "NULL"}");

        if (profile == null)
        {
            Debug.LogError("[ProfileManager] profile is NULL!");
            return;
        }

        currentProfile = profile;

        // 내 프로필인지 확인
        isMyProfile = LoginManager.Instance != null &&
                      LoginManager.Instance.CurrentUser != null &&
                      LoginManager.Instance.CurrentUser.id == profile.id;

        Debug.Log($"[ProfileManager] isMyProfile={isMyProfile}, fullProfilePanel={fullProfilePanel != null}");

        // UI 업데이트
        if (usernameText != null) usernameText.text = profile.username;
        if (bioText != null) bioText.text = string.IsNullOrEmpty(profile.bio) ? "" : profile.bio;
        if (followersCountText != null) followersCountText.text = profile.followers_count.ToString();
        if (followingCountText != null) followingCountText.text = profile.following_count.ToString();

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

        // 조건부 버튼 상태 설정
        SetupConditionalUI();

        // 아바타 공개상태 UI 초기화
        InitializeVisibilityUI();

        // 패널 표시
        if (fullProfilePanel != null)
        {
            Debug.Log("[ProfileManager] Activating fullProfilePanel");
            fullProfilePanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[ProfileManager] fullProfilePanel is NULL! Cannot show profile panel.");
        }
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
            // 팔로우 버튼 텍스트 설정
            Text btnText = followButtonText ?? followButton.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                // "팔로우" / "팔로우 중" (팔로우 취소 대신)
                btnText.text = isFollowing ? GetLocalizedText("following") : GetLocalizedText("follow");
            }

            // 버튼 색상 변경 (팔로우 중일 때 연한 색상)
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
        // 기존 항목 제거 (반복 중 수정 방지를 위해 리스트에 먼저 수집)
        if (followListContent != null)
        {
            var children = new List<GameObject>();
            foreach (Transform child in followListContent)
                children.Add(child.gameObject);
            foreach (var child in children)
                Destroy(child);
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

    #region Conditional UI Setup

    /// <summary>
    /// 본인/타인 프로필 여부에 따른 조건부 UI 설정
    /// - 본인: 팔로우 버튼 숨김, SNS 아이콘 표시, "프로필 편집" 버튼
    /// - 타인: 팔로우 버튼 표시, SNS 아이콘 표시 (등록된 경우), "DM 보내기" 버튼
    /// </summary>
    private void SetupConditionalUI()
    {
        bool isGuest = LoginManager.Instance?.IsGuest ?? true;

        if (isMyProfile)
        {
            // === 내 프로필 ===
            // 팔로우 버튼 숨김
            if (followButton != null)
                followButton.gameObject.SetActive(false);

            // SNS 아이콘 표시 (등록된 것만)
            SetupSnsIcons(currentProfile);

            // "프로필 편집" 버튼 표시
            if (editProfileButton != null)
            {
                editProfileButton.gameObject.SetActive(true);
                if (editProfileButtonText != null)
                    editProfileButtonText.text = GetLocalizedText("edit_profile");
            }
        }
        else
        {
            // === 다른 사람 프로필 ===
            // 팔로우 버튼 표시 (게스트가 아닐 때)
            if (followButton != null)
            {
                followButton.gameObject.SetActive(!isGuest);
                if (!isGuest)
                {
                    UpdateFollowButtonState();
                }
            }

            // SNS 아이콘 표시 (등록된 것만)
            SetupSnsIcons(currentProfile);

            // "DM 보내기" 버튼 표시
            if (editProfileButton != null)
            {
                editProfileButton.gameObject.SetActive(!isGuest);
                if (editProfileButtonText != null)
                    editProfileButtonText.text = GetLocalizedText("send_dm");
            }
        }
    }

    /// <summary>
    /// SNS 아이콘 설정 (등록된 SNS만 표시)
    /// Inspector에서 연결 안 되어 있으면 동적 생성
    /// </summary>
    private void SetupSnsIcons(ProfileData profile)
    {
        if (profile == null) return;

        // 프로필 데이터에서 SNS 정보 확인
        string instagramId = profile.instagram_id;
        string xId = profile.x_id;
        string facebookId = profile.facebook_id;

#if UNITY_EDITOR
        // 에디터에서 테스트 SNS 데이터 사용
        if (useTestSnsData)
        {
            if (!string.IsNullOrEmpty(testInstagramId)) instagramId = testInstagramId;
            if (!string.IsNullOrEmpty(testXId)) xId = testXId;
            if (!string.IsNullOrEmpty(testFacebookId)) facebookId = testFacebookId;
            Debug.Log($"[ProfileManager] Using test SNS data - IG: {testInstagramId}, X: {testXId}, FB: {testFacebookId}");
        }
#endif

        bool hasInstagram = !string.IsNullOrEmpty(instagramId);
        bool hasX = !string.IsNullOrEmpty(xId);
        bool hasFacebook = !string.IsNullOrEmpty(facebookId);

        bool hasAnySns = hasInstagram || hasX || hasFacebook;

        Debug.Log($"[ProfileManager] SNS Check - Instagram: {hasInstagram} ({instagramId}), X: {hasX} ({xId}), Facebook: {hasFacebook} ({facebookId})");

        if (!hasAnySns)
        {
            // SNS 없으면 컨테이너 숨기기
            if (snsIconsContainer != null)
                snsIconsContainer.SetActive(false);
            return;
        }

        // SNS 컨테이너가 없으면 동적 생성
        if (snsIconsContainer == null)
        {
            CreateSnsIconsContainer();
        }

        if (snsIconsContainer != null)
        {
            snsIconsContainer.SetActive(true);

            // Instagram 버튼
            if (instagramButton != null)
            {
                instagramButton.gameObject.SetActive(hasInstagram);
            }
            else if (hasInstagram)
            {
                instagramButton = CreateSnsIconButton("Instagram", new Color(0.88f, 0.19f, 0.42f), 0);
            }

            // X (Twitter) 버튼
            if (xButton != null)
            {
                xButton.gameObject.SetActive(hasX);
            }
            else if (hasX)
            {
                xButton = CreateSnsIconButton("X", Color.black, 1);
            }

            // Facebook 버튼
            if (facebookButton != null)
            {
                facebookButton.gameObject.SetActive(hasFacebook);
            }
            else if (hasFacebook)
            {
                facebookButton = CreateSnsIconButton("Facebook", new Color(0.23f, 0.35f, 0.60f), 2);
            }
        }

        Debug.Log($"[ProfileManager] SNS Icons setup complete - Container: {snsIconsContainer != null}");
    }

    /// <summary>
    /// SNS 아이콘 컨테이너 동적 생성
    /// followButton 또는 editProfileButton 위치 참조
    /// </summary>
    private void CreateSnsIconsContainer()
    {
        // 참조할 부모 찾기 (followButton 또는 editProfileButton의 부모)
        Transform parent = null;
        Vector2 referencePosition = Vector2.zero;

        if (followButton != null)
        {
            parent = followButton.transform.parent;
            RectTransform followRect = followButton.GetComponent<RectTransform>();
            if (followRect != null)
                referencePosition = followRect.anchoredPosition + new Vector2(0, -80f); // 팔로우 버튼 아래
        }
        else if (editProfileButton != null)
        {
            parent = editProfileButton.transform.parent;
            RectTransform editRect = editProfileButton.GetComponent<RectTransform>();
            if (editRect != null)
                referencePosition = editRect.anchoredPosition + new Vector2(0, 80f); // 편집 버튼 위
        }
        else if (fullProfilePanel != null)
        {
            parent = fullProfilePanel.transform;
            referencePosition = new Vector2(0, -200f);
        }

        if (parent == null)
        {
            Debug.LogWarning("[ProfileManager] Cannot create SNS container - no parent found");
            return;
        }

        // 컨테이너 생성
        GameObject containerObj = new GameObject("SnsIconsContainer");
        containerObj.transform.SetParent(parent, false);

        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(200f, 60f);
        containerRect.anchoredPosition = referencePosition;

        // HorizontalLayoutGroup 추가
        HorizontalLayoutGroup hlg = containerObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        snsIconsContainer = containerObj;

        Debug.Log($"[ProfileManager] SNS container created at position: {referencePosition}");
    }

    /// <summary>
    /// SNS 아이콘 버튼 동적 생성
    /// Resources 폴더에서 아이콘 로드
    /// </summary>
    private Button CreateSnsIconButton(string snsName, Color bgColor, int index)
    {
        if (snsIconsContainer == null) return null;

        GameObject btnObj = new GameObject($"{snsName}Button");
        btnObj.transform.SetParent(snsIconsContainer.transform, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(50f, 50f);

        // 배경 이미지
        Image btnImage = btnObj.AddComponent<Image>();

        // Resources에서 아이콘 로드
        string iconPath = $"SNS/{snsName.ToLower()}_icon";
        Sprite iconSprite = Resources.Load<Sprite>(iconPath);

        if (iconSprite != null)
        {
            btnImage.sprite = iconSprite;
            btnImage.color = Color.white;
            Debug.Log($"[ProfileManager] Loaded {snsName} icon from Resources: {iconPath}");
        }
        else
        {
            // 폴백: 색상 배경 + 텍스트
            btnImage.color = bgColor;
            CreateFallbackLabel(btnImage.transform, snsName);
            Debug.LogWarning($"[ProfileManager] Icon not found: {iconPath}, using fallback");
        }

        // 버튼 컴포넌트
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        // 클릭 이벤트 연결
        switch (snsName)
        {
            case "Instagram":
                btn.onClick.AddListener(OnInstagramClicked);
                break;
            case "X":
                btn.onClick.AddListener(OnXClicked);
                break;
            case "Facebook":
                btn.onClick.AddListener(OnFacebookClicked);
                break;
        }

        return btn;
    }

    /// <summary>
    /// 아이콘 로드 실패 시 텍스트 라벨 생성
    /// </summary>
    private void CreateFallbackLabel(Transform parent, string snsName)
    {
        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(parent, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textObj.AddComponent<Text>();
        label.text = snsName == "Instagram" ? "IG" : snsName == "X" ? "X" : "FB";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
    }

    #endregion

    #region SNS Button Handlers

    private void OnInstagramClicked()
    {
        if (currentProfile == null || string.IsNullOrEmpty(currentProfile.instagram_id)) return;

        string instagramUrl = $"https://www.instagram.com/{currentProfile.instagram_id}";
        Debug.Log($"[ProfileManager] Opening Instagram: {instagramUrl}");
        Application.OpenURL(instagramUrl);
    }

    private void OnXClicked()
    {
        if (currentProfile == null || string.IsNullOrEmpty(currentProfile.x_id)) return;

        string xUrl = $"https://x.com/{currentProfile.x_id}";
        Debug.Log($"[ProfileManager] Opening X: {xUrl}");
        Application.OpenURL(xUrl);
    }

    private void OnFacebookClicked()
    {
        if (currentProfile == null || string.IsNullOrEmpty(currentProfile.facebook_id)) return;

        string facebookUrl = $"https://www.facebook.com/{currentProfile.facebook_id}";
        Debug.Log($"[ProfileManager] Opening Facebook: {facebookUrl}");
        Application.OpenURL(facebookUrl);
    }

    #endregion

    #region Avatar Visibility Toggle

    /// <summary>
    /// 아바타 클릭 시
    /// - 내 프로필: 공개상태 변경
    /// - 다른 사람 프로필: DM 열기
    /// </summary>
    private void OnAvatarClicked()
    {
        if (isMyProfile)
        {
            // 내 프로필: 공개상태 순환
            ToggleVisibilityMode();
        }
        else
        {
            // 다른 사람 프로필: DM 열기
            OpenDMWithUser();
        }
    }

    /// <summary>
    /// 공개상태 순환 변경 (전체 -> 팔로잉에게만 -> 비공개 -> 전체)
    /// </summary>
    private void ToggleVisibilityMode()
    {
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
                switch (lang)
                {
                    case "ko": return "내 아바타 공개상태 : 전체";
                    case "ja": return "アバター公開: 全員";
                    case "zh": return "头像可见性: 全部";
                    case "es": return "Visibilidad del Avatar: Público";
                    default: return "Avatar Visibility: Public";
                }
            case "followingonly":
                switch (lang)
                {
                    case "ko": return "내 아바타 공개상태 : 팔로잉에게만";
                    case "ja": return "アバター公開: フォロー中のみ";
                    case "zh": return "头像可见性: 仅关注者";
                    case "es": return "Visibilidad del Avatar: Solo Siguiendo";
                    default: return "Avatar Visibility: Following Only";
                }
            case "private":
                switch (lang)
                {
                    case "ko": return "내 아바타 공개상태 : 비공개";
                    case "ja": return "アバター公開: 非公開";
                    case "zh": return "头像可见性: 私密";
                    case "es": return "Visibilidad del Avatar: Privado";
                    default: return "Avatar Visibility: Private";
                }
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

    #region DM Integration

    /// <summary>
    /// 현재 프로필 유저에게 DM 열기
    /// </summary>
    private void OpenDMWithUser()
    {
        if (currentProfile == null)
        {
            Debug.LogWarning("[ProfileManager] Cannot open DM - no profile selected");
            return;
        }

        if (LoginManager.Instance == null || LoginManager.Instance.CurrentUser == null)
        {
            Debug.LogWarning("[ProfileManager] Cannot open DM - not logged in");
            return;
        }

        Debug.Log($"[ProfileManager] Opening DM with user: {currentProfile.username} ({currentProfile.id})");

        // MessagePanelManager를 통해 DM 열기
        if (MessagePanelManager.Instance != null)
        {
            // 프로필 패널 닫기
            CloseFullProfile();

            // DM 채팅 열기
            MessagePanelManager.Instance.OpenChatRoom(
                currentProfile.id,
                currentProfile.username,
                currentProfile.avatar_url
            );
        }
        else
        {
            Debug.LogWarning("[ProfileManager] MessagePanelManager not found");
        }
    }

    #endregion

    #region Edit Profile

    /// <summary>
    /// 편집/DM 버튼 클릭 핸들러
    /// - 내 프로필: 프로필 편집 웹페이지 열기
    /// - 다른 사람 프로필: DM 보내기
    /// </summary>
    private void OpenEditProfileWeb()
    {
        if (isMyProfile)
        {
            // 내 프로필: 편집 페이지 열기
            OpenEditProfileWebPage();
        }
        else
        {
            // 다른 사람 프로필: DM 보내기
            OpenDMWithUser();
        }
    }

    /// <summary>
    /// 프로필 편집 웹페이지 열기 (내 프로필에서만)
    /// </summary>
    private void OpenEditProfileWebPage()
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
        string lang = GetCurrentLanguageCode();

        switch (key)
        {
            case "follow":
                switch (lang)
                {
                    case "ko": return "팔로우";
                    case "ja": return "フォロー";
                    case "zh": return "关注";
                    case "es": return "Seguir";
                    default: return "Follow";
                }
            case "following":  // 팔로우 중 상태
                switch (lang)
                {
                    case "ko": return "팔로우 중";
                    case "ja": return "フォロー中";
                    case "zh": return "已关注";
                    case "es": return "Siguiendo";
                    default: return "Following";
                }
            case "unfollow":
                switch (lang)
                {
                    case "ko": return "팔로우 취소";
                    case "ja": return "フォロー解除";
                    case "zh": return "取消关注";
                    case "es": return "Dejar de seguir";
                    default: return "Unfollow";
                }
            case "followers_title":
                switch (lang)
                {
                    case "ko": return "팔로워";
                    case "ja": return "フォロワー";
                    case "zh": return "粉丝";
                    case "es": return "Seguidores";
                    default: return "Followers";
                }
            case "following_title":
                switch (lang)
                {
                    case "ko": return "팔로잉";
                    case "ja": return "フォロー中";
                    case "zh": return "关注";
                    case "es": return "Siguiendo";
                    default: return "Following";
                }
            case "edit_profile":
                switch (lang)
                {
                    case "ko": return "프로필 편집";
                    case "ja": return "プロフィール編集";
                    case "zh": return "编辑资料";
                    case "es": return "Editar perfil";
                    default: return "Edit Profile";
                }
            case "send_dm":
                switch (lang)
                {
                    case "ko": return "DM 보내기";
                    case "ja": return "DMを送る";
                    case "zh": return "发送私信";
                    case "es": return "Enviar DM";
                    default: return "Send DM";
                }
            case "like":
                switch (lang)
                {
                    case "ko": return "좋아요";
                    case "ja": return "いいね";
                    case "zh": return "点赞";
                    case "es": return "Me gusta";
                    default: return "Like";
                }
            case "liked":
                switch (lang)
                {
                    case "ko": return "좋아요 취소";
                    case "ja": return "いいね解除";
                    case "zh": return "取消点赞";
                    case "es": return "Ya no me gusta";
                    default: return "Unlike";
                }
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
    public string created_at;
}

[System.Serializable]
public class IsFollowingResponse
{
    public bool is_following;
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
