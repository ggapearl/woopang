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
    public Button followedButton;  // 팔로우 중 상태 버튼 (다른 배경)
    public Text followButtonText;  // 팔로우 버튼 텍스트
    public Button editProfileButton;  // 내 프로필: 웹으로 이동, 다른 사람: DM 보내기
    public Text editProfileButtonText;  // 버튼 텍스트 (동적 변경용)
    public Button closeButton;

    [Header("Logout Button (내 프로필에서만 표시)")]
    [Tooltip("로그아웃 버튼 - 내 프로필에서만 표시")]
    public Button logoutButton;
    [Tooltip("로그아웃 버튼 Y 위치 (편집 버튼 아래)")]
    public float logoutButtonYPosition = -520f;
    [Tooltip("로그아웃 버튼 사이즈")]
    public Vector2 logoutButtonSize = new Vector2(200f, 50f);

    [Header("Logout Confirm Dialog")]
    [Tooltip("로그아웃 확인 다이얼로그 패널")]
    public GameObject logoutConfirmDialog;
    [Tooltip("로그아웃 확인 메시지 텍스트")]
    public Text logoutConfirmText;
    [Tooltip("로그아웃 확인 버튼")]
    public Button logoutConfirmButton;
    [Tooltip("로그아웃 취소 버튼")]
    public Button logoutCancelButton;

    [Header("Logout Dialog Settings (Inspector에서 조절)")]
    [Tooltip("다이얼로그 패널 사이즈")]
    public Vector2 logoutDialogSize = new Vector2(300f, 180f);
    [Tooltip("다이얼로그 배경 색상")]
    public Color logoutDialogBgColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [Tooltip("다이얼로그 오버레이 색상")]
    public Color logoutDialogOverlayColor = new Color(0f, 0f, 0f, 0.7f);
    [Tooltip("확인 버튼 색상")]
    public Color logoutConfirmButtonColor = new Color(0.9f, 0.3f, 0.3f, 1f);
    [Tooltip("취소 버튼 색상")]
    public Color logoutCancelButtonColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [Tooltip("버튼 사이즈")]
    public Vector2 logoutDialogButtonSize = new Vector2(100f, 40f);

    [Header("SNS Icons (팔로우 버튼 영역에 표시)")]
    [Tooltip("Instagram 아이콘 버튼")]
    public Button instagramButton;
    [Tooltip("X (Twitter) 아이콘 버튼")]
    public Button xButton;
    [Tooltip("Facebook 아이콘 버튼")]
    public Button facebookButton;
    [Tooltip("SNS 아이콘 부모 오브젝트 (내 프로필에서 팔로우 버튼 대신 표시)")]
    public GameObject snsIconsContainer;
    [Tooltip("SNS 아이콘 크기 (가로, 세로)")]
    public Vector2 snsIconSize = new Vector2(50f, 50f);
    [Tooltip("SNS 아이콘 간격")]
    public float snsIconSpacing = 20f;
    [Tooltip("SNS 컨테이너 Y 위치 (내 프로필)")]
    public float snsContainerYPositionMine = -300f;
    [Tooltip("SNS 컨테이너 Y 위치 (타인 프로필)")]
    public float snsContainerYPositionOther = -440f;

    [Header("Follow List Panel (Instagram Style)")]
    public GameObject followListPanel;
    public Text followListTitleText;           // 상단 사용자명
    public Button followListBackButton;        // 뒤로가기 버튼

    [Header("Follow List - Tab Bar")]
    public Button followersTabButton;          // "XXX 팔로워" 탭
    public Button followingTabButton;          // "XXX 팔로잉" 탭
    public Text followersTabText;              // 팔로워 탭 텍스트
    public Text followingTabText;              // 팔로잉 탭 텍스트
    public GameObject followersTabIndicator;   // 팔로워 탭 언더라인
    public GameObject followingTabIndicator;   // 팔로잉 탭 언더라인

    [Header("Follow List - Search")]
    public InputField followListSearchInput;   // 검색 입력 필드

    [Header("Follow List - Content")]
    public Transform followersListContent;     // 팔로워 목록 Content
    public Transform followingListContent;     // 팔로잉 목록 Content
    public ScrollRect followListSlideScrollRect; // 좌우 슬라이드 ScrollRect
    public GameObject followersPage;           // 팔로워 페이지
    public GameObject followingPage;           // 팔로잉 페이지
    public SwipePageHandler swipePageHandler;  // 스와이프 페이지 핸들러

    [Header("Follow List - Item Prefabs")]
    public GameObject followingItemPrefab;     // 팔로잉 아이템 (메시지 보내기 버튼)
    public GameObject followerItemPrefab;      // 팔로워 아이템 (맞팔로우/팔로우 버튼)

    [Header("Visibility Settings (내 프로필에서만 표시)")]
    [Tooltip("위치공개 상태 텍스트")]
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

    // FollowPanel에서 열렸는지 여부
    public static bool openedFromFollowPanel = false;

    // 프로필 패널 닫힐 때 콜백 (ChatRoomPanel 등에서 사용)
    private Action onCloseCallback;

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
        // avatarButton 런타임 fallback (에디터에서 연결 안 된 경우)
        if (avatarButton == null && fullProfilePanel != null)
        {
            Transform fpContent = fullProfilePanel.transform.Find("Content");
            if (fpContent != null)
            {
                Transform topSection = fpContent.Find("TopSection");
                if (topSection != null)
                {
                    Transform avatarMask = topSection.Find("AvatarMask");
                    if (avatarMask != null)
                        avatarButton = avatarMask.GetComponent<Button>();
                }
            }
        }
        if (avatarButton != null)
            avatarButton.onClick.AddListener(OnAvatarClicked);
        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutButtonClicked);
        if (logoutConfirmButton != null)
            logoutConfirmButton.onClick.AddListener(OnLogoutConfirmed);
        if (logoutCancelButton != null)
            logoutCancelButton.onClick.AddListener(OnLogoutCancelled);

        // SNS 아이콘 버튼 리스너
        if (instagramButton != null)
            instagramButton.onClick.AddListener(OnInstagramClicked);
        if (xButton != null)
            xButton.onClick.AddListener(OnXClicked);
        if (facebookButton != null)
            facebookButton.onClick.AddListener(OnFacebookClicked);

        // 인스타그램 스타일 Follow List 버튼 리스너
        // 탭 순서: 팔로잉(왼쪽/페이지0), 팔로워(오른쪽/페이지1)
        SetupFollowListButtonListeners();
    }

    /// <summary>
    /// 팔로우 리스트 버튼 리스너 설정 (Awake 또는 지연 초기화에서 호출)
    /// </summary>
    private void SetupFollowListButtonListeners()
    {
        // followListPanel이 null이면 런타임에서 찾기
        if (followListPanel == null)
        {
            // 씬에서 FollowListPanel 찾기
            GameObject foundPanel = GameObject.Find("FollowListPanel");
            if (foundPanel != null)
            {
                followListPanel = foundPanel;
            }
            else
            {
                // Canvas 아래에서 찾기
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var canvas in canvases)
                {
                    Transform found = canvas.transform.Find("FollowListPanel");
                    if (found != null)
                    {
                        followListPanel = found.gameObject;
                        break;
                    }
                }
            }
        }

        // 버튼이 연결 안 되어 있으면 런타임에서 찾기
        if (followListPanel != null)
        {
            if (followingTabButton == null)
                followingTabButton = followListPanel.transform.Find("TabBar/FollowingTab")?.GetComponent<Button>();
            if (followersTabButton == null)
                followersTabButton = followListPanel.transform.Find("TabBar/FollowersTab")?.GetComponent<Button>();
            if (followListBackButton == null)
                followListBackButton = followListPanel.transform.Find("Header/BackButton")?.GetComponent<Button>();
            if (swipePageHandler == null)
                swipePageHandler = followListPanel.transform.Find("ContentArea")?.GetComponent<SwipePageHandler>();
            if (followListSearchInput == null)
                followListSearchInput = followListPanel.transform.Find("SearchBar/InputField")?.GetComponent<InputField>();
            if (followersListContent == null)
                followersListContent = followListPanel.transform.Find("ContentArea/SwipeViewport/SwipeContent/FollowersPage/Viewport/Content");
            if (followingListContent == null)
                followingListContent = followListPanel.transform.Find("ContentArea/SwipeViewport/SwipeContent/FollowingPage/Viewport/Content");
            if (followersTabText == null)
                followersTabText = followListPanel.transform.Find("TabBar/FollowersTab/Text")?.GetComponent<Text>();
            if (followingTabText == null)
                followingTabText = followListPanel.transform.Find("TabBar/FollowingTab/Text")?.GetComponent<Text>();
            if (followersTabIndicator == null)
                followersTabIndicator = followListPanel.transform.Find("TabBar/FollowersTab/Indicator")?.gameObject;
            if (followingTabIndicator == null)
                followingTabIndicator = followListPanel.transform.Find("TabBar/FollowingTab/Indicator")?.gameObject;
            if (followListTitleText == null)
                followListTitleText = followListPanel.transform.Find("Header/TitleText")?.GetComponent<Text>();

            // 런타임 프리팹 로딩 (Resources 폴더에서)
            if (followingItemPrefab == null)
                followingItemPrefab = Resources.Load<GameObject>("Prefabs/Profile/FollowingItem");
            if (followerItemPrefab == null)
                followerItemPrefab = Resources.Load<GameObject>("Prefabs/Profile/FollowerItem");
        }

        // 탭 버튼 리스너
        if (followingTabButton != null)
        {
            followingTabButton.onClick.RemoveAllListeners();
            followingTabButton.onClick.AddListener(() =>
            {
                Debug.Log("[ProfileManager] 팔로잉 탭 클릭됨");
                SwitchFollowListTab(false);
            });
        }
        else
        {
            Debug.LogWarning("[ProfileManager] followingTabButton이 null입니다!");
        }

        if (followersTabButton != null)
        {
            followersTabButton.onClick.RemoveAllListeners();
            followersTabButton.onClick.AddListener(() =>
            {
                Debug.Log("[ProfileManager] 팔로워 탭 클릭됨");
                SwitchFollowListTab(true);
            });
        }
        else
        {
            Debug.LogWarning("[ProfileManager] followersTabButton이 null입니다!");
        }

        // 뒤로가기 버튼 (새 UI와 레거시 모두 지원)
        if (followListBackButton != null)
        {
            followListBackButton.onClick.RemoveAllListeners();
            followListBackButton.onClick.AddListener(() =>
            {
                Debug.Log("[ProfileManager] 팔로우 리스트 뒤로가기 클릭됨");
                CloseFollowList();
            });
        }

        // 스와이프 페이지 핸들러 이벤트 리스너
        if (swipePageHandler != null)
        {
            swipePageHandler.OnPageChanged -= OnSwipePageChanged;
            swipePageHandler.OnPageChanged += OnSwipePageChanged;
        }

        // 검색 입력 필드 이벤트
        if (followListSearchInput != null)
        {
            followListSearchInput.onValueChanged.RemoveAllListeners();
            followListSearchInput.onValueChanged.AddListener(OnFollowListSearchChanged);
        }

    }

    void Start()
    {
        // 로그인 상태 변경 이벤트 구독
        if (LoginManager.Instance != null)
        {
            LoginManager.Instance.OnLoginStateChanged += OnLoginStateChanged;

            // 이미 로그인 되어있으면 프로필 로드, 아니면 Login 표시
            if (LoginManager.Instance.IsLoggedIn)
            {
                LoadMyProfile();
            }
            else
            {
                ClearMiniProfile();
            }
        }

        // 앱 시작 시 미니 프로필 아웃라인 색상 초기화
        StartCoroutine(InitializeMiniProfileOutlineDelayed());

        // SNS 아이콘 컨테이너 초기화 (Inspector에서 연결 안 되어 있으면 동적 생성)
        StartCoroutine(InitializeSnsContainerDelayed());

        // SwipeViewport 투명화 수정 (잘못된 이미지가 설정되어 있을 수 있음)
        FixSwipeViewportTransparency();

#if UNITY_EDITOR
        // 에디터 모드에서 FollowListPanel 자동 감지 및 더미 데이터 로드
        StartCoroutine(EditorAutoTestRoutine());
#endif
    }

    /// <summary>
    /// SwipeViewport와 내부 Viewport들을 투명하게 설정
    /// </summary>
    private void FixSwipeViewportTransparency()
    {
        if (followListPanel == null) return;

        // SwipeViewport 찾기
        Transform swipeViewport = followListPanel.transform.Find("ContentArea/SwipeViewport");
        if (swipeViewport != null)
        {
            Image vpImage = swipeViewport.GetComponent<Image>();
            if (vpImage != null)
            {
                vpImage.sprite = null;
                vpImage.color = Color.clear;
            }

            Mask vpMask = swipeViewport.GetComponent<Mask>();
            if (vpMask != null)
            {
                vpMask.showMaskGraphic = false;
            }
        }

        // 각 페이지의 Viewport도 수정
        string[] paths = {
            "ContentArea/SwipeViewport/SwipeContent/FollowersPage/Viewport",
            "ContentArea/SwipeViewport/SwipeContent/FollowingPage/Viewport"
        };

        foreach (string path in paths)
        {
            Transform viewport = followListPanel.transform.Find(path);
            if (viewport != null)
            {
                Image img = viewport.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = null;
                    img.color = Color.clear;
                }

                Mask mask = viewport.GetComponent<Mask>();
                if (mask != null)
                {
                    mask.showMaskGraphic = false;
                }
            }
        }
    }

#if UNITY_EDITOR
    private bool wasFollowListPanelActive = false;

    /// <summary>
    /// 에디터 모드에서 패널 활성화 감지 및 자동 더미 데이터 로드
    /// </summary>
    private IEnumerator EditorAutoTestRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            // FollowListPanel 활성화 감지
            if (followListPanel != null)
            {
                bool isActive = followListPanel.activeSelf;

                // 비활성 → 활성 전환 시 (처음 열렸을 때)
                if (isActive && !wasFollowListPanelActive)
                {
                    Debug.Log("[ProfileManager] 에디터 모드: FollowListPanel 활성화 감지 - 더미 데이터 로드");

                    // 버튼 연결 확인
                    SetupFollowListButtonListeners();

                    // 더미 데이터가 없으면 로드
                    if (currentFollowersList == null || currentFollowingList == null)
                    {
                        // 테스트 프로필 설정
                        if (currentProfile == null)
                        {
                            currentProfile = new ProfileData
                            {
                                id = "test_user",
                                username = "테스트유저",
                                followers_count = 8,
                                following_count = 6
                            };
                        }

                        // 타이틀 설정
                        if (followListTitleText != null)
                            followListTitleText.text = currentProfile.username;

                        // 탭 텍스트 업데이트
                        UpdateFollowListTabTexts();

                        // 양쪽 모두 더미 데이터 로드
                        LoadBothListsWithDummyData();
                    }
                }

                wasFollowListPanelActive = isActive;
            }

            yield return new WaitForSeconds(0.3f);
        }
    }
#endif

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
            ClearAvatarCache();
            LoadMyProfile();
        }

        // 에디터에서 다른 유저 프로필 열기
        if (editorOpenTestProfile)
        {
            editorOpenTestProfile = false;
            if (!string.IsNullOrEmpty(editorTestUserId))
            {
                ShowProfile(editorTestUserId);
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
            miniUsernameText.text = "Login";  // 로그인 전 기본 텍스트 (번역 안 함)

        if (miniAvatarImage != null && defaultAvatarSprite != null)
            miniAvatarImage.sprite = defaultAvatarSprite;

        // 미니 프로필 패널은 로그아웃 후에도 유지 (비활성화하지 않음)
        // 패널 내용만 초기화하고 UI 구조는 유지
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
    /// 테스트용 프로필 표시 (API 호출 없이 더미 데이터 사용)
    /// </summary>
    public void ShowTestProfile(string userId, string username)
    {
        // SNS ID 랜덤 생성 (50% 확률로 각각 존재)
        string[] sampleInstaIds = { "insta_user", "photo_lover", "travel_gram", "daily_life", "" };
        string[] sampleXIds = { "x_user", "tweeter123", "social_butterfly", "" };
        string[] sampleFbIds = { "fb_user", "facebook_friend", "" };

        string randomInsta = UnityEngine.Random.value > 0.5f ? sampleInstaIds[UnityEngine.Random.Range(0, sampleInstaIds.Length - 1)] : "";
        string randomX = UnityEngine.Random.value > 0.5f ? sampleXIds[UnityEngine.Random.Range(0, sampleXIds.Length - 1)] : "";
        string randomFb = UnityEngine.Random.value > 0.5f ? sampleFbIds[UnityEngine.Random.Range(0, sampleFbIds.Length - 1)] : "";

        ProfileData testProfile = new ProfileData
        {
            id = userId,
            username = username,
            bio = "테스트 사용자입니다. 안녕하세요!",
            avatar_url = "",
            followers_count = UnityEngine.Random.Range(10, 500),
            following_count = UnityEngine.Random.Range(5, 200),
            instagram_id = randomInsta,
            x_id = randomX,
            facebook_id = randomFb
        };

        Debug.Log($"[ProfileManager] Showing test profile: {username} ({userId}) - SNS: Insta={randomInsta}, X={randomX}, FB={randomFb}");
        ShowProfilePanel(testProfile);
    }

    /// <summary>
    /// 미니 프로필 클릭 시 (내 프로필 열기)
    /// </summary>
    private void OnMiniProfileClicked()
    {
        // 로그인 여부 확인
        if (LoginManager.Instance == null)
        {
            Debug.LogError("[ProfileManager] LoginManager.Instance가 NULL입니다!");
            return;
        }

        // 로그인되지 않았고 게스트도 아니면 로그인 팝업 표시
        if (!LoginManager.Instance.IsLoggedIn && !LoginManager.Instance.IsGuest)
        {
            LoginManager.Instance.ShowLoginRequirementPopup();
            return;
        }

        // 게스트 모드인 경우에도 로그인 팝업 표시 (프로필 기능은 로그인 필요)
        if (LoginManager.Instance.IsGuest)
        {
            LoginManager.Instance.ShowLoginRequirementPopup();
            return;
        }

        // 로그인된 경우 프로필 표시
        if (LoginManager.Instance.CurrentUser == null)
        {
            Debug.LogError("[ProfileManager] CurrentUser가 NULL입니다!");
            return;
        }

        ShowProfile(LoginManager.Instance.CurrentUser.id);
    }

    private void ShowProfilePanel(ProfileData profile)
    {
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

        // UI 업데이트
        if (usernameText != null) usernameText.text = profile.username;
        if (bioText != null) bioText.text = string.IsNullOrEmpty(profile.bio) ? "" : profile.bio;
        if (followersCountText != null)
            followersCountText.text = $"{profile.followers_count}\n{GetLocalizedText("followers_label")}";
        if (followingCountText != null)
            followingCountText.text = $"{profile.following_count}\n{GetLocalizedText("following_label")}";

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
            else
            {
                // 기본 스프라이트가 없으면 유저이름 기반 색상 생성
                avatarImage.color = GetAvatarColorFromName(profile.username);
            }
        }

        // 조건부 버튼 상태 설정
        SetupConditionalUI();

        // 아바타 공개상태 UI 초기화
        InitializeVisibilityUI();

        // 패널 표시 (빌드에서 참조 손실 방지를 위한 fallback 추가)
        if (fullProfilePanel == null)
        {
            // 런타임에서 FullProfilePanel 찾기 시도
            fullProfilePanel = GameObject.Find("FullProfilePanel");
            if (fullProfilePanel == null)
            {
                // Canvas 아래에서 찾기
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var canvas in canvases)
                {
                    Transform found = canvas.transform.Find("FullProfilePanel");
                    if (found != null)
                    {
                        fullProfilePanel = found.gameObject;
                        break;
                    }
                }
            }
        }

        if (fullProfilePanel != null)
        {
            // 부모 오브젝트들도 활성화 확인
            Transform parent = fullProfilePanel.transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    Debug.LogWarning($"[ProfileManager] 부모 오브젝트 '{parent.name}'가 비활성화 상태입니다. 활성화합니다.");
                    parent.gameObject.SetActive(true);
                }
                parent = parent.parent;
            }

            fullProfilePanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[ProfileManager] fullProfilePanel을 찾을 수 없습니다!");
        }
    }

    public void CloseFullProfile()
    {
        if (fullProfilePanel != null)
            fullProfilePanel.SetActive(false);

        currentProfile = null;

        // FollowPanel에서 열렸으면 돌아가기
        if (openedFromFollowPanel)
        {
            openedFromFollowPanel = false;
            FollowManager.returnToFollowPanel = true;
            Debug.Log("[ProfileManager] FollowPanel로 돌아갑니다");
        }

        // 등록된 닫기 콜백 호출 (ChatRoomPanel 복원 등)
        if (onCloseCallback != null)
        {
            onCloseCallback.Invoke();
            onCloseCallback = null;
        }
    }

    /// <summary>
    /// 프로필 패널이 닫힐 때 호출할 콜백 설정
    /// 일회용: 콜백 호출 후 자동 제거
    /// </summary>
    public void SetOnCloseCallback(Action callback)
    {
        onCloseCallback = callback;
    }

    #endregion

    #region Follow System

    private void UpdateFollowButtonState()
    {
        if (followButton == null || currentProfile == null) return;

        string myId = LoginManager.Instance?.CurrentUser?.id;
        if (string.IsNullOrEmpty(myId)) return;

        // 내가 상대방을 팔로우하는지 확인
        StartCoroutine(CheckIsFollowing(myId, currentProfile.id, (iFollowThem) =>
        {
            // 상대방이 나를 팔로우하는지도 확인 (맞팔로우 여부)
            StartCoroutine(CheckIsFollowing(currentProfile.id, myId, (theyFollowMe) =>
            {
                UpdateFollowButtonUI(iFollowThem, theyFollowMe);
            }));
        }));
    }

    /// <summary>
    /// 팔로우 버튼 UI 업데이트
    /// </summary>
    /// <param name="iFollowThem">내가 상대방을 팔로우 중인지</param>
    /// <param name="theyFollowMe">상대방이 나를 팔로우 중인지</param>
    private void UpdateFollowButtonUI(bool iFollowThem, bool theyFollowMe)
    {
        // 팔로우 중: followedButton 표시, followButton 숨김
        // 팔로우 전: followButton 표시, followedButton 숨김
        if (followButton != null)
            followButton.gameObject.SetActive(!iFollowThem);

        if (followedButton != null)
        {
            followedButton.gameObject.SetActive(iFollowThem);

            // followedButton 클릭 이벤트 연결 (한 번만)
            followedButton.onClick.RemoveAllListeners();
            followedButton.onClick.AddListener(OnFollowButtonClicked);
        }

        // followButton 텍스트 설정 (맞팔로우 여부에 따라)
        // 상대방이 나를 팔로우하면 "맞팔로우 하기", 아니면 "팔로우 하기"
        if (!iFollowThem)
        {
            Text btnText = followButtonText ?? followButton?.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = theyFollowMe ? GetLocalizedText("follow_back") : GetLocalizedText("follow");
            }
        }

        Debug.Log($"[ProfileManager] FollowButton updated - iFollowThem: {iFollowThem}, theyFollowMe: {theyFollowMe}");
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
                        followersCountText.text = $"{currentProfile.followers_count}\n{GetLocalizedText("followers_label")}";
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
                        followersCountText.text = $"{currentProfile.followers_count}\n{GetLocalizedText("followers_label")}";
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

    // 현재 표시 중인 탭 (true: 팔로워, false: 팔로잉)
    private bool isShowingFollowers = true;

    // 현재 로드된 목록 (검색 필터링용)
    private FollowUser[] currentFollowersList;
    private FollowUser[] currentFollowingList;

    // 팔로우 리스트에서 돌아갈 때 프로필 패널 다시 열기용
    private ProfileData profileToReturnTo;

    private void ShowFollowList(string type)
    {
        if (currentProfile == null) return;

        // 내 프로필에서만 팔로워/팔로잉 목록 볼 수 있음
        if (!isMyProfile)
        {
            Debug.Log("[ProfileManager] 다른 사용자의 팔로워/팔로잉 목록은 볼 수 없습니다.");
            return;
        }

        // FollowManager가 있으면 새 시스템 사용
        if (FollowManager.Instance != null)
        {
            // 프로필 패널 닫기
            profileToReturnTo = currentProfile;
            if (fullProfilePanel != null)
                fullProfilePanel.SetActive(false);

            // FollowManager로 열기
            if (type == "followers")
                FollowManager.Instance.ShowFollowers(currentProfile.id, currentProfile.username);
            else
                FollowManager.Instance.ShowFollowing(currentProfile.id, currentProfile.username);

            return;
        }

        // 기존 시스템 (FollowManager 없을 때 폴백)
        // 버튼 연결 확인 (런타임 초기화 안 됐을 수 있음)
        SetupFollowListButtonListeners();

        // 프로필 패널에서 팔로우 리스트로 이동 시 프로필 패널 닫기
        // 뒤로가기 버튼을 눌렀을 때 다시 열기 위해 저장
        profileToReturnTo = currentProfile;
        if (fullProfilePanel != null)
            fullProfilePanel.SetActive(false);

        // 타이틀을 사용자명으로 설정 (인스타그램 스타일)
        if (followListTitleText != null)
            followListTitleText.text = currentProfile.username;

        // 탭 텍스트에 숫자 표시
        UpdateFollowListTabTexts();

        bool showFollowers = (type == "followers");
        isShowingFollowers = showFollowers;

        // 탭 전환
        SwitchFollowListTab(showFollowers);

#if UNITY_EDITOR
        // 에디터에서는 더미 데이터 사용
        if (Application.isPlaying)
        {
            LoadBothListsWithDummyData();
        }
#else
        // 데이터 로드
        if (showFollowers)
        {
            StartCoroutine(FetchFollowersNew(currentProfile.id));
        }
        else
        {
            StartCoroutine(FetchFollowingNew(currentProfile.id));
        }
#endif

        // 패널 표시
        if (followListPanel != null)
            followListPanel.SetActive(true);
    }

    /// <summary>
    /// 탭 텍스트 업데이트 (숫자 없이 레이블만)
    /// </summary>
    private void UpdateFollowListTabTexts()
    {
        // 숫자 없이 "팔로잉", "팔로워"만 표시
        if (followingTabText != null)
            followingTabText.text = GetLocalizedText("following_label");

        if (followersTabText != null)
            followersTabText.text = GetLocalizedText("followers_label");
    }

    /// <summary>
    /// 팔로워/팔로잉 탭 전환
    /// 탭 순서: 팔로잉(왼쪽/페이지0), 팔로워(오른쪽/페이지1)
    /// </summary>
    private void SwitchFollowListTab(bool showFollowers)
    {
        isShowingFollowers = showFollowers;

        // 스와이프 핸들러로 페이지 이동
        // 팔로워 = 페이지 1, 팔로잉 = 페이지 0
        if (swipePageHandler != null)
        {
            int targetPage = showFollowers ? 1 : 0;
            swipePageHandler.SetPage(targetPage, true);
        }

        // 탭 UI 업데이트
        UpdateFollowListTabUI(showFollowers);

        // 해당 탭의 데이터가 없으면 로드
#if !UNITY_EDITOR
        // 빌드에서만 API 호출 (에디터에서는 더미 데이터 사용)
        if (showFollowers && currentFollowersList == null && currentProfile != null)
        {
            StartCoroutine(FetchFollowersNew(currentProfile.id));
        }
        else if (!showFollowers && currentFollowingList == null && currentProfile != null)
        {
            StartCoroutine(FetchFollowingNew(currentProfile.id));
        }
#endif

        Debug.Log($"[ProfileManager] Tab switched to: {(showFollowers ? "Followers" : "Following")}");
    }

    /// <summary>
    /// 스와이프로 페이지 변경 시 호출
    /// 페이지 0 = 팔로잉, 페이지 1 = 팔로워
    /// </summary>
    private void OnSwipePageChanged(int pageIndex)
    {
        bool showFollowers = (pageIndex == 1);
        isShowingFollowers = showFollowers;

        // 탭 UI 업데이트
        UpdateFollowListTabUI(showFollowers);

        // 해당 탭의 데이터가 없으면 로드
#if !UNITY_EDITOR
        // 빌드에서만 API 호출 (에디터에서는 더미 데이터 사용)
        if (showFollowers && currentFollowersList == null && currentProfile != null)
        {
            StartCoroutine(FetchFollowersNew(currentProfile.id));
        }
        else if (!showFollowers && currentFollowingList == null && currentProfile != null)
        {
            StartCoroutine(FetchFollowingNew(currentProfile.id));
        }
#endif

        Debug.Log($"[ProfileManager] Swiped to: {(showFollowers ? "Followers" : "Following")}");
    }

    /// <summary>
    /// 팔로우 리스트 탭 UI 업데이트 (인디케이터, 텍스트 스타일)
    /// </summary>
    private void UpdateFollowListTabUI(bool showFollowers)
    {
        // 탭 인디케이터 업데이트 (팔로잉=왼쪽, 팔로워=오른쪽)
        if (followingTabIndicator != null)
        {
            Image indicator = followingTabIndicator.GetComponent<Image>();
            if (indicator != null)
                indicator.color = showFollowers ? Color.clear : Color.white;
        }

        if (followersTabIndicator != null)
        {
            Image indicator = followersTabIndicator.GetComponent<Image>();
            if (indicator != null)
                indicator.color = showFollowers ? Color.white : Color.clear;
        }

        // 탭 텍스트 스타일 업데이트
        if (followingTabText != null)
        {
            followingTabText.fontStyle = showFollowers ? FontStyle.Normal : FontStyle.Bold;
            followingTabText.color = showFollowers ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
        }

        if (followersTabText != null)
        {
            followersTabText.fontStyle = showFollowers ? FontStyle.Bold : FontStyle.Normal;
            followersTabText.color = showFollowers ? Color.white : new Color(0.6f, 0.6f, 0.6f);
        }
    }

    /// <summary>
    /// 검색어 변경 시 필터링
    /// </summary>
    private void OnFollowListSearchChanged(string searchText)
    {
        if (isShowingFollowers)
        {
            FilterAndDisplayFollowers(searchText);
        }
        else
        {
            FilterAndDisplayFollowing(searchText);
        }
    }

    /// <summary>
    /// 팔로워 목록 필터링 및 표시
    /// </summary>
    private void FilterAndDisplayFollowers(string searchText)
    {
        if (currentFollowersList == null) return;

        FollowUser[] filtered = currentFollowersList;

        if (!string.IsNullOrEmpty(searchText))
        {
            searchText = searchText.ToLower();
            filtered = System.Array.FindAll(currentFollowersList,
                u => u.username.ToLower().Contains(searchText));
        }

        PopulateFollowersList(filtered);
    }

    /// <summary>
    /// 팔로잉 목록 필터링 및 표시
    /// </summary>
    private void FilterAndDisplayFollowing(string searchText)
    {
        if (currentFollowingList == null) return;

        FollowUser[] filtered = currentFollowingList;

        if (!string.IsNullOrEmpty(searchText))
        {
            searchText = searchText.ToLower();
            filtered = System.Array.FindAll(currentFollowingList,
                u => u.username.ToLower().Contains(searchText));
        }

        PopulateFollowingList(filtered);
    }

    /// <summary>
    /// 팔로워 목록 API 호출 (새 UI용)
    /// </summary>
    private IEnumerator FetchFollowersNew(string userId)
    {
#if UNITY_EDITOR
        // 에디터에서는 더미 데이터가 이미 로드되어 있으면 API 호출 스킵
        if (currentFollowersList != null && currentFollowersList.Length > 0)
        {
            Debug.Log("[ProfileManager] 에디터 모드: 이미 팔로워 데이터 있음 - API 호출 스킵");
            yield break;
        }
#endif
        string url = $"{BASE_URL}/api/followers?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<FollowListResponse>(request.downloadHandler.text);
                // 빈 데이터가 반환되면 기존 더미 데이터 유지
                if (response.followers != null && response.followers.Length > 0)
                {
                    currentFollowersList = response.followers;
                    PopulateFollowersList(response.followers);
                }
                else
                {
                    Debug.Log("[ProfileManager] API가 빈 팔로워 리스트 반환 - 기존 데이터 유지");
                }
            }
            else
            {
                Debug.LogWarning($"[ProfileManager] Failed to fetch followers: {request.error}");
            }
        }
    }

    /// <summary>
    /// 팔로잉 목록 API 호출 (새 UI용)
    /// </summary>
    private IEnumerator FetchFollowingNew(string userId)
    {
#if UNITY_EDITOR
        // 에디터에서는 더미 데이터가 이미 로드되어 있으면 API 호출 스킵
        if (currentFollowingList != null && currentFollowingList.Length > 0)
        {
            Debug.Log("[ProfileManager] 에디터 모드: 이미 팔로잉 데이터 있음 - API 호출 스킵");
            yield break;
        }
#endif
        string url = $"{BASE_URL}/api/following?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<FollowListResponse>(request.downloadHandler.text);
                // 빈 데이터가 반환되면 기존 더미 데이터 유지
                if (response.following != null && response.following.Length > 0)
                {
                    currentFollowingList = response.following;
                    PopulateFollowingList(response.following);
                }
                else
                {
                    Debug.Log("[ProfileManager] API가 빈 팔로잉 리스트 반환 - 기존 데이터 유지");
                }
            }
            else
            {
                Debug.LogWarning($"[ProfileManager] Failed to fetch following: {request.error}");
            }
        }
    }

    /// <summary>
    /// 팔로워 목록 UI 표시 (FollowerItem 프리팹 사용)
    /// </summary>
    private void PopulateFollowersList(FollowUser[] users)
    {
        Debug.Log($"[ProfileManager] PopulateFollowersList 시작 - users:{users?.Length ?? 0}, " +
            $"prefab:{(followerItemPrefab != null ? "OK" : "NULL")}, " +
            $"content:{(followersListContent != null ? "OK" : "NULL")}");

        // 기존 항목 제거
        if (followersListContent != null)
        {
            var children = new List<GameObject>();
            foreach (Transform child in followersListContent)
                children.Add(child.gameObject);
            foreach (var child in children)
                Destroy(child);
        }
        else
        {
            Debug.LogWarning("[ProfileManager] followersListContent가 null입니다! 패널 경로를 확인하세요.");
        }

        // 항목 추가
        if (users != null && followerItemPrefab != null && followersListContent != null)
        {
            foreach (var user in users)
            {
                GameObject item = Instantiate(followerItemPrefab, followersListContent);

                // RectTransform 앵커 수정 (레이아웃 그룹 호환)
                RectTransform itemRect = item.GetComponent<RectTransform>();
                if (itemRect != null)
                {
                    itemRect.anchorMin = new Vector2(0, 1);
                    itemRect.anchorMax = new Vector2(1, 1);
                    itemRect.pivot = new Vector2(0.5f, 1);
                    // 스트레치 앵커 사용 시 SizeDelta는 패딩을 의미
                    // x: 좌우 패딩 합계 (0 = 부모 너비에 맞춤)
                    // y: 높이 (80px)
                    itemRect.sizeDelta = new Vector2(0, 80);
                    itemRect.anchoredPosition = Vector2.zero;
                }

                // LayoutElement 추가 (없으면)
                var layoutElement = item.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = item.AddComponent<UnityEngine.UI.LayoutElement>();
                }
                layoutElement.minHeight = 80;
                layoutElement.preferredHeight = 80;
                layoutElement.flexibleWidth = 1;

                SetupFollowerItem(item, user);
            }

            // 레이아웃 강제 업데이트
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(followersListContent as RectTransform);

            Debug.Log($"[ProfileManager] FollowerItem {users.Length}개 생성 완료");
        }
        else
        {
            if (followerItemPrefab == null)
                Debug.LogWarning("[ProfileManager] followerItemPrefab이 null입니다!");
        }
    }

    /// <summary>
    /// 팔로잉 목록 UI 표시 (FollowingItem 프리팹 사용)
    /// </summary>
    private void PopulateFollowingList(FollowUser[] users)
    {
        Debug.Log($"[ProfileManager] PopulateFollowingList 시작 - users:{users?.Length ?? 0}, " +
            $"prefab:{(followingItemPrefab != null ? "OK" : "NULL")}, " +
            $"content:{(followingListContent != null ? "OK" : "NULL")}");

        // 기존 항목 제거
        if (followingListContent != null)
        {
            var children = new List<GameObject>();
            foreach (Transform child in followingListContent)
                children.Add(child.gameObject);
            foreach (var child in children)
                Destroy(child);
        }
        else
        {
            Debug.LogWarning("[ProfileManager] followingListContent가 null입니다! 패널 경로를 확인하세요.");
        }

        // 항목 추가
        if (users != null && followingItemPrefab != null && followingListContent != null)
        {
            foreach (var user in users)
            {
                GameObject item = Instantiate(followingItemPrefab, followingListContent);

                // RectTransform 앵커 수정 (레이아웃 그룹 호환)
                RectTransform itemRect = item.GetComponent<RectTransform>();
                if (itemRect != null)
                {
                    itemRect.anchorMin = new Vector2(0, 1);
                    itemRect.anchorMax = new Vector2(1, 1);
                    itemRect.pivot = new Vector2(0.5f, 1);
                    // 스트레치 앵커 사용 시 SizeDelta는 패딩을 의미
                    // x: 좌우 패딩 합계 (0 = 부모 너비에 맞춤)
                    // y: 높이 (80px)
                    itemRect.sizeDelta = new Vector2(0, 80);
                    itemRect.anchoredPosition = Vector2.zero;
                }

                // LayoutElement 추가 (없으면)
                var layoutElement = item.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = item.AddComponent<UnityEngine.UI.LayoutElement>();
                }
                layoutElement.minHeight = 80;
                layoutElement.preferredHeight = 80;
                layoutElement.flexibleWidth = 1;

                SetupFollowingItem(item, user);
            }

            // 레이아웃 강제 업데이트
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(followingListContent as RectTransform);

            Debug.Log($"[ProfileManager] FollowingItem {users.Length}개 생성 완료");
        }
        else
        {
            if (followingItemPrefab == null)
                Debug.LogWarning("[ProfileManager] followingItemPrefab이 null입니다!");
        }
    }

    /// <summary>
    /// 팔로워 아이템 설정 (맞팔로우 버튼 + X 버튼)
    /// </summary>
    private void SetupFollowerItem(GameObject item, FollowUser user)
    {
        // 아바타
        Transform avatarTr = item.transform.Find("Avatar");
        if (avatarTr != null)
        {
            Image avatar = avatarTr.GetComponent<Image>();
            if (avatar != null && !string.IsNullOrEmpty(user.avatar_url))
            {
                StartCoroutine(LoadAvatarImage(user.avatar_url, avatar));
            }
        }

        // 사용자명 (TextArea/Username 또는 직접 Username 경로 지원)
        Transform usernameTr = item.transform.Find("TextArea/Username");
        if (usernameTr == null)
            usernameTr = item.transform.Find("Username"); // 대체 경로
        if (usernameTr != null)
        {
            Text usernameText = usernameTr.GetComponent<Text>();
            if (usernameText != null)
                usernameText.text = user.username;
        }

        // 표시 이름 (지금은 사용자명과 동일하게)
        Transform displayNameTr = item.transform.Find("TextArea/DisplayName");
        if (displayNameTr == null)
            displayNameTr = item.transform.Find("DisplayName"); // 대체 경로
        if (displayNameTr != null)
        {
            Text displayNameText = displayNameTr.GetComponent<Text>();
            if (displayNameText != null)
                displayNameText.text = user.username;
        }

        // 전체 아이템 클릭 - 프로필 열기
        Button itemBtn = item.GetComponent<Button>();
        if (itemBtn != null)
        {
            string userId = user.id;
            string username = user.username;
            itemBtn.onClick.AddListener(() =>
            {
                CloseFollowList();
                ShowProfile(userId);
            });
        }

        // 팔로우/맞팔로우 버튼
        Transform followBtnTr = item.transform.Find("FollowButton");
        if (followBtnTr != null)
        {
            Button followBtn = followBtnTr.GetComponent<Button>();
            Text followBtnText = followBtnTr.Find("Text")?.GetComponent<Text>();

            if (followBtn != null)
            {
                string targetId = user.id;

                // 내가 상대방을 이미 팔로우하는지 확인
                string myId = LoginManager.Instance?.CurrentUser?.id;
                if (!string.IsNullOrEmpty(myId))
                {
                    StartCoroutine(CheckIsFollowing(myId, targetId, (iFollowThem) =>
                    {
                        if (followBtnText != null)
                        {
                            followBtnText.text = iFollowThem ?
                                GetLocalizedText("following") :
                                GetLocalizedText("follow_back");
                        }

                        // 버튼 배경색 변경
                        Image btnBg = followBtnTr.GetComponent<Image>();
                        if (btnBg != null)
                        {
                            btnBg.color = iFollowThem ?
                                new Color(0.25f, 0.25f, 0.28f, 1f) : // 팔로우 중: 회색
                                new Color(0.35f, 0.45f, 0.95f, 1f);  // 팔로우 전: 파란색
                        }

                        followBtn.onClick.RemoveAllListeners();
                        followBtn.onClick.AddListener(() =>
                        {
                            OnFollowerItemFollowClicked(targetId, followBtnTr, iFollowThem);
                        });
                    }));
                }
            }
        }

        // 삭제(X) 버튼 (팔로워 삭제)
        Transform removeBtnTr = item.transform.Find("RemoveButton");
        if (removeBtnTr != null)
        {
            Button removeBtn = removeBtnTr.GetComponent<Button>();
            if (removeBtn != null)
            {
                string targetId = user.id;
                removeBtn.onClick.AddListener(() =>
                {
                    OnRemoveFollowerClicked(targetId, item);
                });
            }
        }
    }

    /// <summary>
    /// 팔로잉 아이템 설정 (메시지 보내기 버튼)
    /// </summary>
    private void SetupFollowingItem(GameObject item, FollowUser user)
    {
        // 아바타
        Transform avatarTr = item.transform.Find("Avatar");
        if (avatarTr != null)
        {
            Image avatar = avatarTr.GetComponent<Image>();
            if (avatar != null && !string.IsNullOrEmpty(user.avatar_url))
            {
                StartCoroutine(LoadAvatarImage(user.avatar_url, avatar));
            }
        }

        // 사용자명 (TextArea/Username 또는 직접 Username 경로 지원)
        Transform usernameTr = item.transform.Find("TextArea/Username");
        if (usernameTr == null)
            usernameTr = item.transform.Find("Username"); // 대체 경로
        if (usernameTr != null)
        {
            Text usernameText = usernameTr.GetComponent<Text>();
            if (usernameText != null)
                usernameText.text = user.username;
        }

        // 표시 이름
        Transform displayNameTr = item.transform.Find("TextArea/DisplayName");
        if (displayNameTr == null)
            displayNameTr = item.transform.Find("DisplayName"); // 대체 경로
        if (displayNameTr != null)
        {
            Text displayNameText = displayNameTr.GetComponent<Text>();
            if (displayNameText != null)
                displayNameText.text = user.username;
        }

        // 전체 아이템 클릭 - 프로필 열기
        Button itemBtn = item.GetComponent<Button>();
        if (itemBtn != null)
        {
            string userId = user.id;
            itemBtn.onClick.AddListener(() =>
            {
                CloseFollowList();
                ShowProfile(userId);
            });
        }

        // 메시지 보내기 버튼
        Transform msgBtnTr = item.transform.Find("MessageButton");
        if (msgBtnTr != null)
        {
            Button msgBtn = msgBtnTr.GetComponent<Button>();
            if (msgBtn != null)
            {
                string oderId = user.id;
                string otherUsername = user.username;
                string otherAvatarUrl = user.avatar_url;

                msgBtn.onClick.AddListener(() =>
                {
                    CloseFollowList();
                    CloseFullProfile();
                    OpenChatRoomWithUser(oderId, otherUsername, otherAvatarUrl);
                });
            }
        }

        // 더보기(...) 버튼
        Transform moreBtnTr = item.transform.Find("MoreButton");
        if (moreBtnTr != null)
        {
            Button moreBtn = moreBtnTr.GetComponent<Button>();
            if (moreBtn != null)
            {
                string targetId = user.id;
                moreBtn.onClick.AddListener(() =>
                {
                    OnFollowingMoreClicked(targetId, item);
                });
            }
        }
    }

    /// <summary>
    /// 팔로워 아이템에서 팔로우/언팔로우 버튼 클릭
    /// </summary>
    private void OnFollowerItemFollowClicked(string targetId, Transform buttonTr, bool wasFollowing)
    {
        string myId = LoginManager.Instance?.CurrentUser?.id;
        if (string.IsNullOrEmpty(myId)) return;

        if (wasFollowing)
        {
            // 언팔로우
            StartCoroutine(UnfollowAndUpdateButton(myId, targetId, buttonTr));
        }
        else
        {
            // 팔로우 (맞팔로우)
            StartCoroutine(FollowAndUpdateButton(myId, targetId, buttonTr));
        }
    }

    private IEnumerator FollowAndUpdateButton(string myId, string targetId, Transform buttonTr)
    {
        string url = $"{BASE_URL}/api/follow";
        string json = $"{{\"follower_id\":\"{myId}\",\"following_id\":\"{targetId}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 버튼 UI 업데이트
                Text btnText = buttonTr.Find("Text")?.GetComponent<Text>();
                if (btnText != null)
                    btnText.text = GetLocalizedText("following");

                Image btnBg = buttonTr.GetComponent<Image>();
                if (btnBg != null)
                    btnBg.color = new Color(0.25f, 0.25f, 0.28f, 1f);

                Button btn = buttonTr.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        OnFollowerItemFollowClicked(targetId, buttonTr, true);
                    });
                }

                Debug.Log($"[ProfileManager] Followed: {targetId}");
            }
        }
    }

    private IEnumerator UnfollowAndUpdateButton(string myId, string targetId, Transform buttonTr)
    {
        string url = $"{BASE_URL}/api/unfollow";
        string json = $"{{\"follower_id\":\"{myId}\",\"following_id\":\"{targetId}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 버튼 UI 업데이트
                Text btnText = buttonTr.Find("Text")?.GetComponent<Text>();
                if (btnText != null)
                    btnText.text = GetLocalizedText("follow_back");

                Image btnBg = buttonTr.GetComponent<Image>();
                if (btnBg != null)
                    btnBg.color = new Color(0.35f, 0.45f, 0.95f, 1f);

                Button btn = buttonTr.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        OnFollowerItemFollowClicked(targetId, buttonTr, false);
                    });
                }

                Debug.Log($"[ProfileManager] Unfollowed: {targetId}");
            }
        }
    }

    /// <summary>
    /// 팔로워 삭제 (X 버튼)
    /// </summary>
    private void OnRemoveFollowerClicked(string followerId, GameObject itemObj)
    {
        // 팔로워 삭제 = 상대방이 나를 팔로우하는 것을 취소
        // 실제로는 서버 API가 필요하지만, 일단 UI에서만 제거
        Debug.Log($"[ProfileManager] Remove follower requested: {followerId}");

        // UI에서 제거
        Destroy(itemObj);

        // TODO: 서버 API 호출 (remove_follower 등)
    }

    /// <summary>
    /// 팔로잉 더보기(...) 버튼 클릭
    /// </summary>
    private void OnFollowingMoreClicked(string userId, GameObject itemObj)
    {
        Debug.Log($"[ProfileManager] More options for following: {userId}");
        // TODO: 팝업 메뉴 표시 (언팔로우, 프로필 보기 등)
    }

    /// <summary>
    /// 지정된 사용자와의 ChatRoom 열기
    /// </summary>
    private void OpenChatRoomWithUser(string oderId, string username, string avatarUrl)
    {
        if (MessagePanelManager.Instance != null)
        {
            MessagePanelManager.Instance.OpenChatRoom(oderId, username, avatarUrl);
            Debug.Log($"[ProfileManager] Opened ChatRoom with: {username} ({oderId})");
        }
        else
        {
            Debug.LogWarning("[ProfileManager] MessagePanelManager not found - cannot open ChatRoom");
        }
    }

    public void CloseFollowList()
    {
        if (followListPanel != null)
            followListPanel.SetActive(false);

        // 검색어 초기화
        if (followListSearchInput != null)
            followListSearchInput.text = "";

        // 캐시된 목록 초기화
        currentFollowersList = null;
        currentFollowingList = null;

        // 프로필 패널로 돌아가기 (저장된 프로필이 있으면)
        if (profileToReturnTo != null)
        {
            ShowProfilePanel(profileToReturnTo);
            profileToReturnTo = null;
        }
    }

    /// <summary>
    /// 테스트용 팔로워 목록 표시 (API 호출 없이 더미 데이터 사용)
    /// 양쪽 리스트 모두 채워서 스와이프 가능하게 함
    /// </summary>
    public void ShowTestFollowersList()
    {
        Debug.Log("[ProfileManager] ShowTestFollowersList 호출됨");

        // 버튼 연결 확인 (런타임 초기화 안 됐을 수 있음)
        SetupFollowListButtonListeners();

        // 테스트용 프로필 설정
        if (currentProfile == null)
        {
            currentProfile = new ProfileData
            {
                id = "test_user",
                username = "테스트유저",
                followers_count = 8,
                following_count = 6
            };
        }

        // 타이틀 설정
        if (followListTitleText != null)
            followListTitleText.text = currentProfile.username;

        // 탭 텍스트 업데이트
        UpdateFollowListTabTexts();

        // 양쪽 모두 더미 데이터 생성 (스와이프 뷰용)
        LoadBothListsWithDummyData();

        // 팔로워 탭으로 전환
        isShowingFollowers = true;
        SwitchFollowListTab(true);

        // 패널 표시
        if (followListPanel != null)
            followListPanel.SetActive(true);

        Debug.Log($"[ProfileManager] 테스트 팔로워 목록 표시 (양쪽 모두 로드됨)");
    }

    /// <summary>
    /// 테스트용 팔로잉 목록 표시 (API 호출 없이 더미 데이터 사용)
    /// 양쪽 리스트 모두 채워서 스와이프 가능하게 함
    /// </summary>
    public void ShowTestFollowingList()
    {
        Debug.Log("[ProfileManager] ShowTestFollowingList 호출됨");

        // 버튼 연결 확인 (런타임 초기화 안 됐을 수 있음)
        SetupFollowListButtonListeners();

        // 테스트용 프로필 설정
        if (currentProfile == null)
        {
            currentProfile = new ProfileData
            {
                id = "test_user",
                username = "테스트유저",
                followers_count = 8,
                following_count = 6
            };
        }

        // 타이틀 설정
        if (followListTitleText != null)
            followListTitleText.text = currentProfile.username;

        // 탭 텍스트 업데이트
        UpdateFollowListTabTexts();

        // 양쪽 모두 더미 데이터 생성 (스와이프 뷰용)
        LoadBothListsWithDummyData();

        // 팔로잉 탭으로 전환
        isShowingFollowers = false;
        SwitchFollowListTab(false);

        // 패널 표시
        if (followListPanel != null)
            followListPanel.SetActive(true);

        Debug.Log($"[ProfileManager] 테스트 팔로잉 목록 표시 (양쪽 모두 로드됨)");
    }

    /// <summary>
    /// 테스트용 팔로우 사용자 데이터 생성
    /// </summary>
    private FollowUser[] GenerateTestFollowUsers(int count, string prefix)
    {
        string[] testNames = new string[]
        {
            "김민지", "이준호", "박서연", "최영수", "정하늘",
            "AR_Master", "여행러버", "맛집탐험가", "사진작가", "우팡러버",
            "서울탐험", "부산여행", "제주도민", "카페투어", "맛집헌터"
        };

        FollowUser[] users = new FollowUser[count];
        for (int i = 0; i < count; i++)
        {
            users[i] = new FollowUser
            {
                id = $"test_{prefix}_{i}",
                username = testNames[i % testNames.Length],
                avatar_url = ""
            };
        }
        return users;
    }

    /// <summary>
    /// 양쪽 리스트 모두에 더미 데이터 로드 (에디터 테스트용)
    /// 스와이프 뷰에서 양쪽 모두 데이터가 필요함
    /// </summary>
    private void LoadBothListsWithDummyData()
    {
        Debug.Log("[ProfileManager] 에디터 모드: 양쪽 리스트에 더미 데이터 로드");

        // 팔로워 더미 데이터 생성 및 표시
        FollowUser[] testFollowers = GenerateTestFollowUsers(8, "follower");
        currentFollowersList = testFollowers;
        PopulateFollowersList(testFollowers);

        // 팔로잉 더미 데이터 생성 및 표시
        FollowUser[] testFollowing = GenerateTestFollowUsers(6, "following");
        currentFollowingList = testFollowing;
        PopulateFollowingList(testFollowing);

        Debug.Log($"[ProfileManager] 더미 데이터 로드 완료 - 팔로워: {testFollowers.Length}, 팔로잉: {testFollowing.Length}");
    }

    #endregion

    #region API Calls

    private IEnumerator FetchProfile(string userId, Action<ProfileData> callback)
    {
        // 캐시 방지를 위해 타임스탬프 추가
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string url = $"{BASE_URL}/api/user/profile?user_id={userId}&_t={timestamp}";

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

    /// <summary>
    /// 유저이름 기반으로 일관된 아바타 색상 생성
    /// </summary>
    private Color GetAvatarColorFromName(string username)
    {
        if (string.IsNullOrEmpty(username))
            return new Color(0.5f, 0.5f, 0.6f, 1f); // 기본 회색

        // 유저이름 해시값으로 색상 생성 (같은 이름은 항상 같은 색상)
        int hash = username.GetHashCode();

        // 파스텔 톤 색상 (채도 낮게, 밝기 높게)
        float hue = Mathf.Abs(hash % 360) / 360f;
        float saturation = 0.4f + (Mathf.Abs((hash >> 8) % 30) / 100f); // 0.4 ~ 0.7
        float value = 0.7f + (Mathf.Abs((hash >> 16) % 20) / 100f);      // 0.7 ~ 0.9

        return Color.HSVToRGB(hue, saturation, value);
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
            }
            else if (defaultAvatarSprite != null)
            {
                targetImage.sprite = defaultAvatarSprite;
            }
        }
    }

    /// <summary>
    /// 아바타 캐시 클리어 (프로필 업데이트 후 새로고침용)
    /// </summary>
    public void ClearAvatarCache()
    {
        avatarCache.Clear();
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
            if (followedButton != null)
                followedButton.gameObject.SetActive(false);

            // SNS 아이콘 표시 - 내 프로필: FollowButton 위치에 배치
            SetupSnsIcons(currentProfile, useFollowButtonPosition: true);

            // "프로필 편집" 버튼 표시
            if (editProfileButton != null)
            {
                editProfileButton.gameObject.SetActive(true);
                if (editProfileButtonText != null)
                    editProfileButtonText.text = GetLocalizedText("edit_profile");
            }

            // 로그아웃 버튼 표시 (내 프로필에서만)
            if (logoutButton != null)
            {
                logoutButton.gameObject.SetActive(true);
            }
        }
        else
        {
            // === 다른 사람 프로필 ===
            // 팔로우 버튼 표시 (게스트가 아닐 때)
            // followedButton은 UpdateFollowButtonState에서 상태에 따라 토글됨
            if (followButton != null)
            {
                // 초기에는 followButton만 표시, followedButton은 숨김
                followButton.gameObject.SetActive(!isGuest);
                if (followedButton != null)
                    followedButton.gameObject.SetActive(false);

                if (!isGuest)
                {
                    UpdateFollowButtonState();
                }
            }

            // "프로필 편집" 버튼 숨김 (타인 프로필)
            if (editProfileButton != null)
                editProfileButton.gameObject.SetActive(false);

            // 로그아웃 버튼 숨김 (타인 프로필)
            if (logoutButton != null)
                logoutButton.gameObject.SetActive(false);

            // SNS 아이콘 표시 - 타인 프로필: EditProfileButton 위치에 배치
            SetupSnsIcons(currentProfile, useFollowButtonPosition: false);
        }
    }

    /// <summary>
    /// SNS 아이콘 설정 (등록된 SNS만 표시)
    /// Inspector에서 연결 안 되어 있으면 동적 생성
    /// </summary>
    /// <param name="profile">프로필 데이터</param>
    /// <param name="useFollowButtonPosition">true: FollowButton 위치 (내 프로필), false: EditProfileButton 위치 (타인 프로필)</param>
    private void SetupSnsIcons(ProfileData profile, bool useFollowButtonPosition = true)
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

            // SNS 컨테이너 위치 설정
            RectTransform containerRect = snsIconsContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                if (useFollowButtonPosition)
                {
                    // 내 프로필: FollowButton 위치 (y=-300)
                    if (followButton != null)
                    {
                        RectTransform followRect = followButton.GetComponent<RectTransform>();
                        if (followRect != null)
                            containerRect.anchoredPosition = followRect.anchoredPosition;
                    }
                    else
                    {
                        containerRect.anchoredPosition = new Vector2(0, -300f);
                    }
                }
                else
                {
                    // 타인 프로필: EditProfileButton 위치 (y=-440)
                    if (editProfileButton != null)
                    {
                        RectTransform editRect = editProfileButton.GetComponent<RectTransform>();
                        if (editRect != null)
                            containerRect.anchoredPosition = editRect.anchoredPosition;
                    }
                    else
                    {
                        containerRect.anchoredPosition = new Vector2(0, -440f);
                    }
                }
            }

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
    }

    /// <summary>
    /// SNS 아이콘 컨테이너 동적 생성
    /// FullProfilePanel > Content 아래에 생성
    /// </summary>
    private void CreateSnsIconsContainer()
    {
        // 참조할 부모 찾기 (FullProfilePanel > Content)
        Transform containerParent = GetSnsContainerParent();

        if (containerParent == null)
        {
            Debug.LogWarning("[ProfileManager] Cannot create SNS container - no parent found");
            return;
        }

        // 기본 위치 (snsContainerYPositionMine과 snsContainerYPositionOther 사이)
        Vector2 referencePosition = new Vector2(0, (snsContainerYPositionMine + snsContainerYPositionOther) / 2f);

        // 컨테이너 생성
        GameObject containerObj = new GameObject("SnsIconsContainer");
        containerObj.transform.SetParent(containerParent, false);

        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(200f, snsIconSize.y + 10f);
        containerRect.anchoredPosition = referencePosition;

        // HorizontalLayoutGroup 추가
        HorizontalLayoutGroup hlg = containerObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = snsIconSpacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        snsIconsContainer = containerObj;

        Debug.Log($"[ProfileManager] SNS container created at position: {referencePosition}");
    }

    /// <summary>
    /// SNS 컨테이너 부모 찾기
    /// </summary>
    private Transform GetSnsContainerParent()
    {
        // 1. FullProfilePanel > Content 찾기
        if (fullProfilePanel != null)
        {
            Transform content = fullProfilePanel.transform.Find("Content");
            if (content != null)
            {
                return content;
            }
        }

        // 2. Content 못 찾으면 followButton/editProfileButton의 부모 사용
        if (followButton != null)
        {
            return followButton.transform.parent;
        }
        else if (editProfileButton != null)
        {
            return editProfileButton.transform.parent;
        }
        else if (fullProfilePanel != null)
        {
            return fullProfilePanel.transform;
        }

        return null;
    }

    /// <summary>
    /// SNS 아이콘 버튼 동적 생성
    /// Resources에서 아이콘 로드하여 생성
    /// </summary>
    private Button CreateSnsIconButton(string snsName, Color bgColor, int index)
    {
        if (snsIconsContainer == null) return null;

        // 동적 생성
        GameObject newBtnObj = new GameObject($"{snsName}Button");
        newBtnObj.transform.SetParent(snsIconsContainer.transform, false);

        RectTransform btnRect = newBtnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = snsIconSize;

        // LayoutElement 추가
        LayoutElement layout = newBtnObj.AddComponent<LayoutElement>();
        layout.preferredWidth = snsIconSize.x;
        layout.preferredHeight = snsIconSize.y;

        // 배경 이미지
        Image btnImage = newBtnObj.AddComponent<Image>();

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
        Button newBtn = newBtnObj.AddComponent<Button>();
        newBtn.targetGraphic = btnImage;

        // 클릭 이벤트 연결
        switch (snsName)
        {
            case "Instagram":
                newBtn.onClick.AddListener(OnInstagramClicked);
                break;
            case "X":
                newBtn.onClick.AddListener(OnXClicked);
                break;
            case "Facebook":
                newBtn.onClick.AddListener(OnFacebookClicked);
                break;
        }

        return newBtn;
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
                    case "ko": return "내 위치공개 : 전체";
                    case "ja": return "位置公開 : 全員";
                    case "zh": return "位置公开 : 全部";
                    case "es": return "Mi Ubicación : Público";
                    default: return "My Location : Public";
                }
            case "followingonly":
                switch (lang)
                {
                    case "ko": return "내 위치공개 : 팔로잉에게만";
                    case "ja": return "位置公開 : フォロー中のみ";
                    case "zh": return "位置公开 : 仅关注者";
                    case "es": return "Mi Ubicación : Solo Siguiendo";
                    default: return "My Location : Following Only";
                }
            case "private":
                switch (lang)
                {
                    case "ko": return "내 위치공개 : 비공개";
                    case "ja": return "位置公開 : 非公開";
                    case "zh": return "位置公开 : 私密";
                    case "es": return "Mi Ubicación : Privado";
                    default: return "My Location : Private";
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
                    case "ko": return "팔로우 하기";
                    case "ja": return "フォローする";
                    case "zh": return "关注";
                    case "es": return "Seguir";
                    default: return "Follow";
                }
            case "follow_back":  // 맞팔로우 하기 (상대방이 나를 이미 팔로우 중)
                switch (lang)
                {
                    case "ko": return "맞팔로우 하기";
                    case "ja": return "フォローバック";
                    case "zh": return "回关";
                    case "es": return "Seguir de vuelta";
                    default: return "Follow Back";
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
            case "followers_label":  // 팔로워 / 숫자 표시용
                switch (lang)
                {
                    case "ko": return "팔로워";
                    case "ja": return "フォロワー";
                    case "zh": return "粉丝";
                    case "es": return "Seguidores";
                    default: return "Followers";
                }
            case "following_label":  // 팔로잉 / 숫자 표시용
                switch (lang)
                {
                    case "ko": return "팔로잉";
                    case "ja": return "フォロー中";
                    case "zh": return "关注";
                    case "es": return "Siguiendo";
                    default: return "Following";
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
            case "logout":
                switch (lang)
                {
                    case "ko": return "로그아웃";
                    case "ja": return "ログアウト";
                    case "zh": return "退出登录";
                    case "es": return "Cerrar sesión";
                    default: return "Logout";
                }
            default:
                return key;
        }
    }

    #endregion

    #region Logout

    /// <summary>
    /// 로그아웃 버튼 클릭 핸들러 - 확인 다이얼로그 표시
    /// </summary>
    private void OnLogoutButtonClicked()
    {
        Debug.Log("[ProfileManager] 로그아웃 버튼 클릭 - 확인 다이얼로그 표시");
        ShowLogoutConfirmDialog();
    }

    /// <summary>
    /// 로그아웃 확인 다이얼로그 표시
    /// </summary>
    private void ShowLogoutConfirmDialog()
    {
        if (logoutConfirmDialog != null)
        {
            // 확인 메시지 설정 (다국어)
            if (logoutConfirmText != null)
            {
                logoutConfirmText.text = GetLocalizedLogoutMessage();
            }

            // 버튼 텍스트 다국어 설정
            ApplyLocalizedButtonTexts();

            logoutConfirmDialog.SetActive(true);
        }
        else
        {
            // 다이얼로그가 없으면 바로 로그아웃 (fallback)
            Debug.LogWarning("[ProfileManager] 로그아웃 확인 다이얼로그가 없습니다. 바로 로그아웃합니다.");
            OnLogoutConfirmed();
        }
    }

    /// <summary>
    /// 로그아웃 다이얼로그 버튼 텍스트 다국어 적용
    /// </summary>
    private void ApplyLocalizedButtonTexts()
    {
        if (LocalizationManager.Instance == null) return;

        // 확인 버튼 텍스트
        if (logoutConfirmButton != null)
        {
            Text confirmText = logoutConfirmButton.GetComponentInChildren<Text>();
            if (confirmText != null)
            {
                confirmText.text = LocalizationManager.Instance.GetText("confirm");
            }
        }

        // 취소 버튼 텍스트
        if (logoutCancelButton != null)
        {
            Text cancelText = logoutCancelButton.GetComponentInChildren<Text>();
            if (cancelText != null)
            {
                cancelText.text = LocalizationManager.Instance.GetText("cancel");
            }
        }
    }

    /// <summary>
    /// 로그아웃 확인 다이얼로그 숨기기
    /// </summary>
    private void HideLogoutConfirmDialog()
    {
        if (logoutConfirmDialog != null)
        {
            logoutConfirmDialog.SetActive(false);
        }
    }

    /// <summary>
    /// 로그아웃 확인 버튼 클릭 - 실제 로그아웃 수행
    /// </summary>
    private void OnLogoutConfirmed()
    {
        Debug.Log("[ProfileManager] 로그아웃 확인됨");
        HideLogoutConfirmDialog();

        if (LoginManager.Instance != null)
        {
            LoginManager.Instance.Logout();

            // 프로필 패널 닫기
            CloseFullProfile();

            // 미니 프로필 UI 초기화 (패널은 유지)
            if (miniUsernameText != null)
                miniUsernameText.text = "";
            if (miniAvatarImage != null && defaultAvatarSprite != null)
                miniAvatarImage.sprite = defaultAvatarSprite;

            Debug.Log("[ProfileManager] 로그아웃 완료 - UI 초기화됨 (미니 프로필 패널은 유지)");
        }
        else
        {
            Debug.LogWarning("[ProfileManager] LoginManager가 null입니다");
        }
    }

    /// <summary>
    /// 로그아웃 취소 버튼 클릭
    /// </summary>
    private void OnLogoutCancelled()
    {
        Debug.Log("[ProfileManager] 로그아웃 취소됨");
        HideLogoutConfirmDialog();
    }

    /// <summary>
    /// 로그아웃 확인 메시지 가져오기 (다국어)
    /// </summary>
    private string GetLocalizedLogoutMessage()
    {
        // LocalizationManager 사용 (5개 언어 지원: en, ko, zh, ja, es)
        if (LocalizationManager.Instance != null)
        {
            return LocalizationManager.Instance.GetText("logout_confirm_message");
        }

        // Fallback: LocalizationManager가 없을 경우
        string lang = Application.systemLanguage == SystemLanguage.Korean ? "ko" : "en";
        switch (lang)
        {
            case "ko": return "로그아웃 하시겠습니까?";
            default: return "Are you sure you want to logout?";
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
