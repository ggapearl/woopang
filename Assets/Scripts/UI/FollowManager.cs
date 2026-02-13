using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 팔로우/팔로워 목록 관리 (독립 스크립트)
/// ProfileManager와 분리하여 단순하고 확실한 동작 보장
/// </summary>
public class FollowManager : MonoBehaviour
{
    public static FollowManager Instance { get; private set; }

    [Header("=== Main Panel ===")]
    public GameObject panel;
    public Text titleText;
    public Button closeButton;

    [Header("=== Tabs ===")]
    public Button followersTab;
    public Button followingTab;
    public Image followersTabLine;
    public Image followingTabLine;

    [Header("=== Search ===")]
    public InputField searchInput;
    public Button searchClearButton;

    [Header("=== Content ===")]
    public ScrollRect scrollRect;
    public Transform contentParent;

    [Header("=== Item Template ===")]
    public GameObject itemTemplate;

    [Header("=== Item Prefab (프리팹 수정 시 레이아웃 변경 가능) ===")]
    [Tooltip("Assets/Prefabs/FollowListItem 프리팹 - 에디터에서 수정 후 저장하면 레이아웃 반영")]
    public GameObject itemPrefab;

    [Header("=== Button Colors ===")]
    public Color followButtonColor = new Color(0.35f, 0.45f, 0.95f, 1f);
    public Color followingButtonColor = new Color(0.3f, 0.3f, 0.35f, 1f);

    [Header("=== Font Sizes ===")]
    public int titleFontSize = 60;
    public int tabFontSize = 50;

    [Header("=== Font Settings ===")]
    [Tooltip("기본 폰트 (AppleSDGothicNeoM)")]
    public Font customFont;
    [Tooltip("기본 폰트 사이즈")]
    public int defaultFontSize = 60;

    [Header("=== Colors ===")]
    public Color activeTabColor = Color.white;
    public Color inactiveTabColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color activeLineColor = new Color(0.91f, 0.33f, 0.51f, 1f); // 핑크

    [Header("=== Unfollow Confirmation Dialog ===")]
    public GameObject unfollowConfirmDialog;
    public Text unfollowConfirmText;
    public Button unfollowConfirmButton;
    public Button unfollowCancelButton;

    // 언팔로우 확인 대기 중인 데이터
    private string pendingUnfollowUserId;
    private string pendingUnfollowUsername;
    private Button pendingUnfollowButton;

    // 현재 상태
    private bool showingFollowers = true;
    private string currentUserId;
    private string currentUsername;
    private string currentSearchQuery = "";

    // 데이터
    private List<FollowUserData> followersList = new List<FollowUserData>();
    private List<FollowUserData> followingList = new List<FollowUserData>();

    // 생성된 아이템들
    private List<GameObject> createdItems = new List<GameObject>();

    // 스와이프 핸들러
    private SwipeHandler swipeHandler;

    // 프로필에서 돌아올 때 FollowPanel 재표시를 위한 플래그
    public static bool returnToFollowPanel = false;
    private static bool wasShowingFollowers = true;
    private static string savedUserId;
    private static string savedUsername;

    private string BASE_URL => ApiConfig.MAIN_SERVER;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 필드 자동 연결
        AutoConnectFields();

        // 커스텀 폰트 로드 (AppleSDGothicNeoM)
        if (customFont == null)
        {
            customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
            if (customFont == null)
            {
                customFont = Resources.Load<Font>("AppleSDGothicNeoM");
            }
        }

        // 버튼 리스너
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (followersTab != null)
            followersTab.onClick.AddListener(() => SwitchTab(true));
        if (followingTab != null)
            followingTab.onClick.AddListener(() => SwitchTab(false));

        // 검색창 리스너
        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(OnSearchInputChanged);
        }
        if (searchClearButton != null)
        {
            searchClearButton.onClick.AddListener(ClearSearch);
        }

        // 초기 상태: 패널 숨김
        if (panel != null)
            panel.SetActive(false);

        // 템플릿 숨김
        if (itemTemplate != null)
            itemTemplate.SetActive(false);

        // 언팔로우 확인 다이얼로그 버튼 리스너
        if (unfollowConfirmButton != null)
            unfollowConfirmButton.onClick.AddListener(OnUnfollowConfirmed);
        if (unfollowCancelButton != null)
            unfollowCancelButton.onClick.AddListener(HideUnfollowConfirmDialog);

        // 언팔로우 확인 다이얼로그 숨김
        if (unfollowConfirmDialog != null)
            unfollowConfirmDialog.SetActive(false);
    }

    /// <summary>
    /// Inspector에서 연결 안 된 필드들을 자동으로 찾아서 연결
    /// </summary>
    private void AutoConnectFields()
    {
        // Panel 찾기 - 씬 전체에서 검색
        if (panel == null)
        {
            // 이름으로 찾기
            GameObject found = GameObject.Find("FollowPanel");
            if (found != null)
                panel = found;
        }

        if (panel == null)
        {
            Debug.LogError("[FollowManager] FollowPanel을 찾을 수 없습니다.");
            return;
        }

        // 재귀적으로 자식 찾기 헬퍼
        Transform FindChildRecursive(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null) return found;

            foreach (Transform child in parent)
            {
                found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // Header 관련 - 재귀 검색
        if (closeButton == null)
        {
            Transform closeBtn = FindChildRecursive(panel.transform, "CloseButton");
            if (closeBtn != null)
                closeButton = closeBtn.GetComponent<Button>();
        }

        if (titleText == null)
        {
            Transform title = FindChildRecursive(panel.transform, "Title");
            if (title != null)
                titleText = title.GetComponent<Text>();
        }

        // TabBar 관련 - 재귀 검색
        if (followersTab == null)
        {
            Transform tab = FindChildRecursive(panel.transform, "FollowersTab");
            if (tab != null)
                followersTab = tab.GetComponent<Button>();
        }

        if (followingTab == null)
        {
            Transform tab = FindChildRecursive(panel.transform, "FollowingTab");
            if (tab != null)
                followingTab = tab.GetComponent<Button>();
        }

        if (followersTabLine == null && followersTab != null)
        {
            Transform line = followersTab.transform.Find("Line");
            if (line != null)
                followersTabLine = line.GetComponent<Image>();
        }

        if (followingTabLine == null && followingTab != null)
        {
            Transform line = followingTab.transform.Find("Line");
            if (line != null)
                followingTabLine = line.GetComponent<Image>();
        }

        // SearchInput 찾기
        if (searchInput == null)
        {
            Transform searchTr = FindChildRecursive(panel.transform, "SearchInput");
            if (searchTr != null)
                searchInput = searchTr.GetComponent<InputField>();
        }

        // SearchClearButton 찾기
        if (searchClearButton == null)
        {
            Transform clearBtn = FindChildRecursive(panel.transform, "SearchClearButton");
            if (clearBtn != null)
                searchClearButton = clearBtn.GetComponent<Button>();
        }

        // ScrollRect 찾기
        if (scrollRect == null)
        {
            Transform scrollArea = FindChildRecursive(panel.transform, "ScrollArea");
            if (scrollArea != null)
                scrollRect = scrollArea.GetComponent<ScrollRect>();
        }

        // Content 찾기
        if (contentParent == null)
        {
            Transform content = FindChildRecursive(panel.transform, "Content");
            if (content != null)
                contentParent = content;
        }

        // ItemTemplate 찾기
        if (itemTemplate == null)
        {
            Transform template = FindChildRecursive(panel.transform, "ItemTemplate");
            if (template != null)
                itemTemplate = template.gameObject;
        }

        // 언팔로우 확인 다이얼로그 찾기
        if (unfollowConfirmDialog == null)
        {
            Transform dialog = FindChildRecursive(panel.transform, "UnfollowConfirmDialog");
            if (dialog != null)
                unfollowConfirmDialog = dialog.gameObject;
        }

        if (unfollowConfirmDialog != null)
        {
            if (unfollowConfirmText == null)
            {
                Transform txt = FindChildRecursive(unfollowConfirmDialog.transform, "ConfirmText");
                if (txt != null)
                    unfollowConfirmText = txt.GetComponent<Text>();
            }

            if (unfollowConfirmButton == null)
            {
                Transform btn = FindChildRecursive(unfollowConfirmDialog.transform, "ConfirmButton");
                if (btn != null)
                    unfollowConfirmButton = btn.GetComponent<Button>();
            }

            if (unfollowCancelButton == null)
            {
                Transform btn = FindChildRecursive(unfollowConfirmDialog.transform, "CancelButton");
                if (btn != null)
                    unfollowCancelButton = btn.GetComponent<Button>();
            }
        }
    }

    void Start()
    {
        // 초기화 코드 (필요시 추가)
    }

    void Update()
    {
        // 프로필에서 돌아올 때 FollowPanel 재표시
        if (returnToFollowPanel)
        {
            returnToFollowPanel = false;

            if (wasShowingFollowers)
                ShowFollowers(savedUserId, savedUsername);
            else
                ShowFollowing(savedUserId, savedUsername);
        }
    }

    /// <summary>
    /// 스와이프 핸들러 초기화
    /// </summary>
    private void SetupSwipeHandler()
    {
        if (scrollRect == null) return;

        // 기존 핸들러 제거
        swipeHandler = scrollRect.GetComponent<SwipeHandler>();
        if (swipeHandler == null)
        {
            swipeHandler = scrollRect.gameObject.AddComponent<SwipeHandler>();
        }

        // 이벤트 연결
        swipeHandler.OnSwipeLeft -= OnSwipeLeft;
        swipeHandler.OnSwipeRight -= OnSwipeRight;
        swipeHandler.OnSwipeLeft += OnSwipeLeft;
        swipeHandler.OnSwipeRight += OnSwipeRight;
    }

    private void OnSwipeLeft()
    {
        // 왼쪽 스와이프: 팔로잉(왼쪽) → 팔로워(오른쪽)
        if (!showingFollowers)
        {
            SwitchTab(true);
        }
    }

    private void OnSwipeRight()
    {
        // 오른쪽 스와이프: 팔로워(오른쪽) → 팔로잉(왼쪽)
        if (showingFollowers)
        {
            SwitchTab(false);
        }
    }

    /// <summary>
    /// 패널의 모든 폰트를 커스텀 폰트로 변경
    /// </summary>
    private void ApplyCustomFonts()
    {
        if (panel == null) return;

        // 패널 내 모든 Text 컴포넌트 찾아서 폰트 적용
        if (customFont != null)
        {
            Text[] allTexts = panel.GetComponentsInChildren<Text>(true);
            foreach (Text txt in allTexts)
            {
                txt.font = customFont;
            }
        }

        // 타이틀 폰트 크기
        if (titleText != null)
        {
            if (customFont != null) titleText.font = customFont;
            titleText.fontSize = titleFontSize;
        }

        // 탭 텍스트 폰트 크기
        if (followersTab != null)
        {
            Text txt = followersTab.GetComponentInChildren<Text>();
            if (txt != null)
            {
                if (customFont != null) txt.font = customFont;
                txt.fontSize = tabFontSize;
            }
        }
        if (followingTab != null)
        {
            Text txt = followingTab.GetComponentInChildren<Text>();
            if (txt != null)
            {
                if (customFont != null) txt.font = customFont;
                txt.fontSize = tabFontSize;
            }
        }

        // 프리팹 사용 시에는 프리팹의 레이아웃을 유지 (하드코딩 사이즈 변경 없음)
    }

    /// <summary>
    /// 버튼 텍스트 다국어 지원
    /// </summary>
    private string GetLocalizedText(string key, string fallbackKo)
    {
        if (LocalizationManager.Instance != null)
        {
            string text = LocalizationManager.Instance.GetText(key);
            if (!string.IsNullOrEmpty(text) && text != key)
                return text;
        }

        string langCode = Application.systemLanguage switch
        {
            SystemLanguage.Korean => "ko",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.Chinese or SystemLanguage.ChineseSimplified or SystemLanguage.ChineseTraditional => "zh",
            SystemLanguage.Spanish => "es",
            _ => "en"
        };

        return key switch
        {
            "follow_btn" => langCode switch { "ko" => "팔로우", "ja" => "フォロー", "zh" => "关注", "es" => "Seguir", _ => "Follow" },
            "following_btn" => langCode switch { "ko" => "팔로잉", "ja" => "フォロー中", "zh" => "已关注", "es" => "Siguiendo", _ => "Following" },
            "message_btn" => langCode switch { "ko" => "메시지", "ja" => "メッセージ", "zh" => "消息", "es" => "Mensaje", _ => "Message" },
            "dm_btn" => langCode switch { "ko" => "메시지", "ja" => "メッセージ", "zh" => "消息", "es" => "Mensaje", _ => "Message" },
            _ => fallbackKo
        };
    }

    /// <summary>
    /// 팔로워 목록 열기
    /// </summary>
    public void ShowFollowers(string userId, string username)
    {
        currentUserId = userId;
        currentUsername = username;
        showingFollowers = true;
        currentSearchQuery = ""; // 검색어 초기화

        // 프로필에서 돌아올 때를 위해 상태 저장
        savedUserId = userId;
        savedUsername = username;
        wasShowingFollowers = true;

        if (titleText != null)
            titleText.text = username;

        if (panel != null)
            panel.SetActive(true);

        // 검색창 초기화 + 다국어 플레이스홀더 적용
        if (searchInput != null)
        {
            searchInput.text = "";
            ApplySearchPlaceholderLocalization();
        }

        // 커스텀 폰트 적용
        ApplyCustomFonts();

        // 스와이프 핸들러 설정
        SetupSwipeHandler();

        UpdateTabUI();
        LoadData();
    }

    /// <summary>
    /// 팔로잉 목록 열기
    /// </summary>
    public void ShowFollowing(string userId, string username)
    {
        currentUserId = userId;
        currentUsername = username;
        showingFollowers = false;
        currentSearchQuery = ""; // 검색어 초기화

        // 프로필에서 돌아올 때를 위해 상태 저장
        savedUserId = userId;
        savedUsername = username;
        wasShowingFollowers = false;

        if (titleText != null)
            titleText.text = username;

        if (panel != null)
            panel.SetActive(true);

        // 검색창 초기화 + 다국어 플레이스홀더 적용
        if (searchInput != null)
        {
            searchInput.text = "";
            ApplySearchPlaceholderLocalization();
        }

        // 커스텀 폰트 적용
        ApplyCustomFonts();

        // 스와이프 핸들러 설정
        SetupSwipeHandler();

        UpdateTabUI();
        LoadData();
    }

    /// <summary>
    /// 검색 플레이스홀더에 다국어 적용
    /// </summary>
    private void ApplySearchPlaceholderLocalization()
    {
        if (searchInput != null && searchInput.placeholder != null)
        {
            Text placeholderText = searchInput.placeholder as Text;
            if (placeholderText != null && LocalizationManager.Instance != null)
            {
                placeholderText.text = LocalizationManager.Instance.GetText("search_id_placeholder");
            }
        }
    }

    /// <summary>
    /// 패널 닫기 (프로필 패널로 돌아가기)
    /// </summary>
    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);

        ClearItems();

        // 프로필 패널로 돌아가기
        ReturnToProfile();
    }

    /// <summary>
    /// 프로필 패널로 돌아가기
    /// </summary>
    private void ReturnToProfile()
    {
        // ProfileManager의 fullProfilePanel 표시
        if (ProfileManager.Instance != null && ProfileManager.Instance.fullProfilePanel != null)
        {
            ProfileManager.Instance.fullProfilePanel.SetActive(true);
        }
    }

    /// <summary>
    /// 탭 전환
    /// </summary>
    private void SwitchTab(bool followers)
    {
        if (showingFollowers == followers) return;

        // 슬라이드 방향 결정:
        // - 팔로워(오른쪽 탭)로 가면 → 새 콘텐츠가 오른쪽에서 들어옴 (slideFromLeft = false)
        // - 팔로잉(왼쪽 탭)으로 가면 → 새 콘텐츠가 왼쪽에서 들어옴 (slideFromLeft = true)
        bool slideFromLeft = !followers;

        showingFollowers = followers;
        UpdateTabUI();

        // 슬라이드 애니메이션과 함께 탭 전환
        StartCoroutine(SwitchTabWithSlide(slideFromLeft));
    }

    /// <summary>
    /// 슬라이드 애니메이션과 함께 탭 전환
    /// </summary>
    private IEnumerator SwitchTabWithSlide(bool slideFromLeft)
    {
        if (contentParent == null) yield break;

        RectTransform contentRect = contentParent.GetComponent<RectTransform>();
        if (contentRect == null) yield break;

        float duration = 0.2f;
        float slideDistance = 300f;

        // 현재 위치 저장
        Vector2 originalPos = contentRect.anchoredPosition;

        // 1. 슬라이드 아웃 (현재 방향 반대로)
        float elapsed = 0f;
        Vector2 outTarget = originalPos + new Vector2(slideFromLeft ? slideDistance : -slideDistance, 0);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // SmoothStep
            contentRect.anchoredPosition = Vector2.Lerp(originalPos, outTarget, t);
            yield return null;
        }

        // 2. 데이터 변경
        DisplayCurrentList();

        // 3. 반대쪽에서 시작
        Vector2 inStart = originalPos + new Vector2(slideFromLeft ? -slideDistance : slideDistance, 0);
        contentRect.anchoredPosition = inStart;

        // 4. 슬라이드 인
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // SmoothStep
            contentRect.anchoredPosition = Vector2.Lerp(inStart, originalPos, t);
            yield return null;
        }

        contentRect.anchoredPosition = originalPos;
    }

    #region Search

    /// <summary>
    /// 검색어 변경 시 호출
    /// </summary>
    private void OnSearchInputChanged(string query)
    {
        currentSearchQuery = query.Trim().ToLower();
        DisplayCurrentList();
    }

    /// <summary>
    /// 검색 초기화
    /// </summary>
    private void ClearSearch()
    {
        if (searchInput != null)
            searchInput.text = "";
        currentSearchQuery = "";
        DisplayCurrentList();
    }

    #endregion

    /// <summary>
    /// 탭 UI 업데이트
    /// </summary>
    private void UpdateTabUI()
    {
        // 팔로워 탭
        if (followersTab != null)
        {
            Text txt = followersTab.GetComponentInChildren<Text>();
            if (txt != null)
                txt.color = showingFollowers ? activeTabColor : inactiveTabColor;
        }
        if (followersTabLine != null)
            followersTabLine.color = showingFollowers ? activeLineColor : Color.clear;

        // 팔로잉 탭
        if (followingTab != null)
        {
            Text txt = followingTab.GetComponentInChildren<Text>();
            if (txt != null)
                txt.color = !showingFollowers ? activeTabColor : inactiveTabColor;
        }
        if (followingTabLine != null)
            followingTabLine.color = !showingFollowers ? activeLineColor : Color.clear;
    }

    /// <summary>
    /// 데이터 로드
    /// </summary>
    private void LoadData()
    {
        if (showingFollowers)
            StartCoroutine(FetchFollowers());
        else
            StartCoroutine(FetchFollowing());
    }

    /// <summary>
    /// 현재 목록 표시 (검색 필터링 + 정렬 적용)
    /// </summary>
    private void DisplayCurrentList()
    {
        ClearItems();

        List<FollowUserData> dataList = showingFollowers ? followersList : followingList;
        string listType = showingFollowers ? "팔로워" : "팔로잉";

        // 1. 검색 필터링
        List<FollowUserData> filteredList = FilterList(dataList, currentSearchQuery);

        // 2. 정렬 (내가 팔로우하는 유저 우선 → 팔로워 많은 순)
        List<FollowUserData> sortedList = SortList(filteredList);

        if (contentParent == null)
        {
            Debug.LogError("[FollowManager] contentParent가 null입니다!");
            return;
        }

        // 프리팹 우선, 없으면 씬 템플릿 사용
        GameObject template = itemPrefab != null ? itemPrefab : itemTemplate;
        if (template == null)
        {
            Debug.LogError("[FollowManager] itemPrefab/itemTemplate이 null입니다!");
            return;
        }

        foreach (var user in sortedList)
        {
            GameObject item = Instantiate(template, contentParent);
            item.SetActive(true);
            item.name = $"Item_{user.username}";

            // 아이템 설정
            SetupItem(item, user);
            createdItems.Add(item);
        }

        // 스크롤 맨 위로
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    /// <summary>
    /// 리스트 검색 필터링
    /// </summary>
    private List<FollowUserData> FilterList(List<FollowUserData> list, string query)
    {
        if (string.IsNullOrEmpty(query))
            return new List<FollowUserData>(list);

        return list.FindAll(user =>
            user.username != null && user.username.ToLower().Contains(query)
        );
    }

    /// <summary>
    /// 리스트 정렬
    /// 1순위: 내가 팔로우하는 유저 우선 (isFollowing = true)
    /// 2순위: 팔로워 수가 많은 순 (followerCount DESC)
    /// </summary>
    private List<FollowUserData> SortList(List<FollowUserData> list)
    {
        List<FollowUserData> sorted = new List<FollowUserData>(list);
        sorted.Sort((a, b) =>
        {
            // 1순위: isFollowing (true가 먼저)
            if (a.isFollowing != b.isFollowing)
                return b.isFollowing.CompareTo(a.isFollowing); // true > false

            // 2순위: followerCount (높은 순)
            return b.followerCount.CompareTo(a.followerCount);
        });
        return sorted;
    }

    /// <summary>
    /// 아이템 설정
    /// </summary>
    private void SetupItem(GameObject item, FollowUserData user)
    {
        // Avatar 설정 - 중앙 캐시 시스템 사용
        Transform avatarTr = item.transform.Find("Avatar");
        if (avatarTr != null)
        {
            ProfileManager.LoadAvatarWithMaskAsync(user.userId, user.avatarUrl, avatarTr, user.username);
        }

        // Username 설정
        Transform usernameTr = item.transform.Find("Username");
        if (usernameTr != null)
        {
            Text usernameText = usernameTr.GetComponent<Text>();
            if (usernameText != null)
            {
                usernameText.text = user.username;
                if (customFont != null) usernameText.font = customFont;
            }
        }

        // ActionButton 설정
        Transform actionBtnTr = item.transform.Find("ActionButton");
        Transform msgBtnTr = item.transform.Find("MessageButton");
        if (actionBtnTr != null)
        {
            Button actionBtn = actionBtnTr.GetComponent<Button>();
            Text actionText = actionBtnTr.GetComponentInChildren<Text>();
            Image actionBtnImg = actionBtnTr.GetComponent<Image>();
            RectTransform actionRect = actionBtnTr as RectTransform;

            if (showingFollowers)
            {
                // 팔로워 탭: 팔로우/언팔로우 버튼 (원래 위치 유지)
                if (actionText != null)
                {
                    actionText.text = user.isFollowing
                        ? GetLocalizedText("following_btn", "팔로잉")
                        : GetLocalizedText("follow_btn", "팔로우");
                    if (customFont != null) actionText.font = customFont;
                }
                if (actionBtnImg != null)
                    actionBtnImg.color = user.isFollowing ? followingButtonColor : followButtonColor;

                if (actionBtn != null)
                {
                    actionBtn.onClick.RemoveAllListeners();
                    string cUserId = user.userId;
                    string cUsername = user.username;
                    bool cIsFollowing = user.isFollowing;
                    actionBtn.onClick.AddListener(() => OnFollowButtonClicked(cUserId, cUsername, cIsFollowing, actionBtn));
                }
            }
            else
            {
                // 팔로잉 탭: 메시지 버튼 역할 → MessageButton 위치(우측 끝)로 이동
                if (actionRect != null && msgBtnTr != null)
                {
                    RectTransform msgRect = msgBtnTr as RectTransform;
                    if (msgRect != null)
                        actionRect.anchoredPosition = msgRect.anchoredPosition;
                }

                if (actionText != null)
                {
                    actionText.text = GetLocalizedText("message_btn", "메시지");
                    if (customFont != null) actionText.font = customFont;
                }
                // 색상도 프리팹 MessageButton의 색상을 그대로 사용
                if (actionBtnImg != null && msgBtnTr != null)
                {
                    Image msgImg = msgBtnTr.GetComponent<Image>();
                    if (msgImg != null)
                        actionBtnImg.color = msgImg.color;
                }

                if (actionBtn != null)
                {
                    actionBtn.onClick.RemoveAllListeners();
                    string cUserId = user.userId;
                    string cUsername = user.username;
                    actionBtn.onClick.AddListener(() => OnMessageButtonClicked(cUserId, cUsername));
                }
            }
        }

        // MessageButton 설정 (프리팹에 포함, 팔로워 탭에서만 표시)
        if (msgBtnTr != null)
        {
            if (showingFollowers)
            {
                msgBtnTr.gameObject.SetActive(true);

                Text msgText = msgBtnTr.GetComponentInChildren<Text>();
                if (msgText != null)
                {
                    msgText.text = GetLocalizedText("message_btn", "메시지");
                    if (customFont != null) msgText.font = customFont;
                }

                // 색상은 프리팹에서 설정한 값 그대로 사용

                Button msgBtn = msgBtnTr.GetComponent<Button>();
                if (msgBtn != null)
                {
                    msgBtn.onClick.RemoveAllListeners();
                    string cUserId = user.userId;
                    string cUsername = user.username;
                    msgBtn.onClick.AddListener(() => OnMessageButtonClicked(cUserId, cUsername));
                }
            }
            else
            {
                // 팔로잉 탭에서는 MessageButton 숨김
                msgBtnTr.gameObject.SetActive(false);
            }
        }

        // 아이템 클릭 시 프로필 열기
        Button btn = item.GetComponent<Button>();
        if (btn != null)
        {
            string capturedUserId = user.userId;
            string capturedUsername = user.username;
            btn.onClick.AddListener(() => OnItemClicked(capturedUserId, capturedUsername));
        }
    }

    /// <summary>
    /// 아이템 클릭
    /// </summary>
    private void OnItemClicked(string userId, string username)
    {
        // FollowPanel 숨기기 (Close 대신 hide만)
        if (panel != null)
            panel.SetActive(false);

        // ProfileManager에게 FollowPanel에서 왔다고 알림
        ProfileManager.openedFromFollowPanel = true;

        // ProfileManager를 통해 프로필 열기
        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.ShowProfile(userId);
        }
    }

    /// <summary>
    /// 팔로우 버튼 클릭 (팔로워 목록에서)
    /// </summary>
    private void OnFollowButtonClicked(string userId, string username, bool isCurrentlyFollowing, Button button)
    {
        if (isCurrentlyFollowing)
        {
            // 언팔로우 - 확인 다이얼로그 표시
            ShowUnfollowConfirmDialog(userId, username, button);
        }
        else
        {
            // 팔로우 - 바로 실행
            StartCoroutine(FollowUser(userId, button));
        }
    }

    /// <summary>
    /// 언팔로우 확인 다이얼로그 표시
    /// </summary>
    private void ShowUnfollowConfirmDialog(string userId, string username, Button button)
    {
        pendingUnfollowUserId = userId;
        pendingUnfollowUsername = username;
        pendingUnfollowButton = button;

        if (unfollowConfirmDialog != null)
        {
            // 다이얼로그 텍스트 설정
            if (unfollowConfirmText != null)
            {
                string confirmMessage = GetUnfollowConfirmMessage();
                unfollowConfirmText.text = $"{username}\n{confirmMessage}";
            }

            unfollowConfirmDialog.SetActive(true);
        }
        else
        {
            // 다이얼로그가 없으면 바로 언팔로우 실행
            StartCoroutine(UnfollowUser(userId, button));
        }
    }

    /// <summary>
    /// 언팔로우 확인 메시지 (다국어)
    /// </summary>
    private string GetUnfollowConfirmMessage()
    {
        string langCode = Application.systemLanguage switch
        {
            SystemLanguage.Korean => "ko",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.Chinese => "zh",
            SystemLanguage.ChineseSimplified => "zh",
            SystemLanguage.ChineseTraditional => "zh",
            SystemLanguage.Spanish => "es",
            _ => "en"
        };

        return langCode switch
        {
            "ko" => "팔로우를 취소하시겠습니까?",
            "ja" => "フォローを解除しますか？",
            "zh" => "确定取消关注吗？",
            "es" => "¿Dejar de seguir?",
            _ => "Unfollow this user?"
        };
    }

    /// <summary>
    /// 언팔로우 확인 다이얼로그 숨기기
    /// </summary>
    private void HideUnfollowConfirmDialog()
    {
        if (unfollowConfirmDialog != null)
            unfollowConfirmDialog.SetActive(false);

        pendingUnfollowUserId = null;
        pendingUnfollowUsername = null;
        pendingUnfollowButton = null;
    }

    /// <summary>
    /// 언팔로우 확인 버튼 클릭
    /// </summary>
    private void OnUnfollowConfirmed()
    {
        if (!string.IsNullOrEmpty(pendingUnfollowUserId) && pendingUnfollowButton != null)
        {
            StartCoroutine(UnfollowUser(pendingUnfollowUserId, pendingUnfollowButton));
        }

        HideUnfollowConfirmDialog();
    }

    /// <summary>
    /// 메시지 버튼 클릭 (팔로잉 목록에서)
    /// </summary>
    private void OnMessageButtonClicked(string userId, string username)
    {
        // 뒤에 있는 FullProfilePanel 닫기
        if (ProfileManager.Instance != null && ProfileManager.Instance.fullProfilePanel != null)
        {
            ProfileManager.Instance.fullProfilePanel.SetActive(false);
        }

        // MessagePanelManager를 통해 대화방 열기
        if (MessagePanelManager.Instance != null)
        {
            MessagePanelManager.Instance.OpenChatRoom(userId, username);
        }

        // 팔로우 패널만 닫기 (프로필 패널로 돌아가지 않음)
        CloseWithoutReturnToProfile();
    }

    /// <summary>
    /// 팔로우 패널만 닫기 (프로필 패널로 돌아가지 않음)
    /// DM 버튼 클릭 등 다른 화면으로 이동할 때 사용
    /// </summary>
    private void CloseWithoutReturnToProfile()
    {
        if (panel != null)
            panel.SetActive(false);

        ClearItems();
    }

    /// <summary>
    /// 팔로우 API 호출
    /// </summary>
    private IEnumerator FollowUser(string userId, Button button)
    {
        string url = $"{BASE_URL}/api/users/{userId}/follow";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.certificateHandler = new BypassCertificateHandler();

            string token = PlayerPrefs.GetString("auth_token", "");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                UpdateFollowButton(button, userId, true);
            }
            else
            {
                Debug.LogError($"[FollowManager] 팔로우 실패: {request.error}");
            }
        }
    }

    /// <summary>
    /// 언팔로우 API 호출
    /// </summary>
    private IEnumerator UnfollowUser(string userId, Button button)
    {
        string url = $"{BASE_URL}/api/users/{userId}/unfollow";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.certificateHandler = new BypassCertificateHandler();

            string token = PlayerPrefs.GetString("auth_token", "");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                UpdateFollowButton(button, userId, false);
            }
            else
            {
                Debug.LogError($"[FollowManager] 언팔로우 실패: {request.error}");
            }
        }
    }

    /// <summary>
    /// 팔로우 버튼 UI 업데이트 + 리스너 재등록
    /// </summary>
    private void UpdateFollowButton(Button button, string userId, bool isFollowing)
    {
        if (button == null) return;

        Text btnText = button.GetComponentInChildren<Text>();
        Image btnImage = button.GetComponent<Image>();

        if (btnText != null)
        {
            btnText.text = isFollowing
                ? GetLocalizedText("following_btn", "팔로잉")
                : GetLocalizedText("follow_btn", "팔로우");
        }

        if (btnImage != null)
        {
            btnImage.color = isFollowing ? followingButtonColor : followButtonColor;
        }

        // 로컬 데이터도 업데이트
        var userData = followersList.Find(u => u.userId == userId);
        if (userData != null)
            userData.isFollowing = isFollowing;

        // 버튼 클릭 리스너 업데이트 (OnFollowButtonClicked 경유 → 언팔로우 확인 다이얼로그 보장)
        button.onClick.RemoveAllListeners();
        string capturedUserId = userId;
        string capturedUsername = userData?.username ?? "";
        bool capturedIsFollowing = isFollowing;
        button.onClick.AddListener(() => OnFollowButtonClicked(capturedUserId, capturedUsername, capturedIsFollowing, button));
    }

    /// <summary>
    /// 생성된 아이템 모두 삭제
    /// </summary>
    private void ClearItems()
    {
        foreach (var item in createdItems)
        {
            if (item != null)
                Destroy(item);
        }
        createdItems.Clear();
    }

    /// <summary>
    /// 아바타를 원형 마스크 구조로 설정하고 이미지를 로드할 Image 컴포넌트 반환
    /// 프리팹에 설정된 마스크/스프라이트를 존중하고, 없을 때만 fallback 적용
    /// </summary>
    private Image SetupCircularAvatarStructure(Transform avatarContainer)
    {
        if (avatarContainer == null) return null;

        // 1. 컨테이너 Image - 프리팹에 스프라이트가 설정되어 있으면 그대로 사용
        Image containerImage = avatarContainer.GetComponent<Image>();
        if (containerImage != null && containerImage.sprite == null)
        {
            // 프리팹에 스프라이트가 없을 때만 Knob fallback 적용
            containerImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            containerImage.type = Image.Type.Simple;
            containerImage.preserveAspect = true;
        }

        // 2. Mask 컴포넌트 (없으면 추가, 있으면 프리팹 설정 유지)
        Mask mask = avatarContainer.GetComponent<Mask>();
        if (mask == null)
        {
            mask = avatarContainer.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        // 3. 자식 AvatarImage 찾기 또는 생성
        Transform avatarImageTransform = avatarContainer.Find("AvatarImage");
        if (avatarImageTransform == null)
        {
            GameObject avatarImageObj = new GameObject("AvatarImage");
            avatarImageObj.transform.SetParent(avatarContainer, false);
            avatarImageObj.layer = 5; // UI Layer

            RectTransform avatarImageRect = avatarImageObj.AddComponent<RectTransform>();
            avatarImageRect.anchorMin = Vector2.zero;
            avatarImageRect.anchorMax = Vector2.one;
            avatarImageRect.offsetMin = Vector2.zero;
            avatarImageRect.offsetMax = Vector2.zero;

            Image avatarImage = avatarImageObj.AddComponent<Image>();
            avatarImage.color = Color.white;
            avatarImage.raycastTarget = false;

            return avatarImage;
        }

        Image existingImage = avatarImageTransform.GetComponent<Image>();
        if (existingImage == null)
        {
            existingImage = avatarImageTransform.gameObject.AddComponent<Image>();
            existingImage.raycastTarget = false;
        }

        return existingImage;
    }

    /// <summary>
    /// 아바타 URL이 없을 때 유저네임 기반 원형 그라데이션 아바타 생성
    /// </summary>
    private void SetDefaultAvatar(Transform avatarContainer, string username)
    {
        if (avatarContainer == null) return;

        Image targetImage = SetupCircularAvatarStructure(avatarContainer);
        if (targetImage == null) return;

        Texture2D tex = GenerateAvatarTexture(username, 128);
        Sprite sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f));
        targetImage.sprite = sprite;
        targetImage.color = Color.white;
    }

    /// <summary>
    /// 유저네임 기반 그라데이션 원형 아바타 텍스처 생성
    /// </summary>
    private Texture2D GenerateAvatarTexture(string username, int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        int hash = string.IsNullOrEmpty(username) ? 0 : username.GetHashCode();
        float hue1 = Mathf.Abs(hash % 360) / 360f;
        float hue2 = (hue1 + 0.15f) % 1f;
        Color color1 = Color.HSVToRGB(hue1, 0.5f, 0.9f);
        Color color2 = Color.HSVToRGB(hue2, 0.4f, 0.75f);

        float center = size / 2f;
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= radius)
                {
                    float t = ((float)x + y) / (size * 2f);
                    Color c = Color.Lerp(color1, color2, t);

                    if (dist > radius - 1.5f)
                        c.a = Mathf.Clamp01((radius - dist) / 1.5f);

                    tex.SetPixel(x, y, c);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 아바타 이미지 로드 (URL 구성 + SSL 우회 + null 체크)
    /// </summary>
    private IEnumerator LoadAvatar(string url, Image targetImage)
    {
        if (string.IsNullOrEmpty(url) || targetImage == null) yield break;

        string fullUrl = url.StartsWith("http") ? url : ApiConfig.MAIN_SERVER + "/" + url;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
        {
            request.certificateHandler = new BypassCertificateHandler();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture != null && targetImage != null)
                {
                    Sprite sprite = Sprite.Create(texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
                    targetImage.sprite = sprite;
                    targetImage.color = Color.white;
                }
            }
            else
            {
                Debug.LogWarning($"[FollowManager] 아바타 로드 실패: {request.error}");
            }
        }
    }

    #region API Calls

    private IEnumerator FetchFollowers()
    {
        string requesterId = LoginManager.Instance?.CurrentUser?.id ?? currentUserId;
        string url = $"{BASE_URL}/api/followers?user_id={currentUserId}&requester_id={requesterId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.certificateHandler = new BypassCertificateHandler();

            string token = PlayerPrefs.GetString("auth_token", "");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<FollowListResponse>(request.downloadHandler.text);
                    followersList.Clear();

                    if (response.followers != null)
                    {
                        foreach (var f in response.followers)
                        {
                            followersList.Add(new FollowUserData
                            {
                                userId = f.user_id,
                                username = f.username,
                                avatarUrl = f.avatar_url,
                                isFollowing = f.is_following,
                                followerCount = f.follower_count
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FollowManager] 팔로워 파싱 오류: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[FollowManager] FetchFollowers 실패: {request.error}");
            }

            DisplayCurrentList();
        }
    }

    private IEnumerator FetchFollowing()
    {
        string url = $"{BASE_URL}/api/following?user_id={currentUserId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.certificateHandler = new BypassCertificateHandler();

            string token = PlayerPrefs.GetString("auth_token", "");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<FollowingListResponse>(request.downloadHandler.text);
                    followingList.Clear();

                    if (response.following != null)
                    {
                        foreach (var f in response.following)
                        {
                            followingList.Add(new FollowUserData
                            {
                                userId = f.user_id,
                                username = f.username,
                                avatarUrl = f.avatar_url,
                                isFollowing = true,
                                followerCount = f.follower_count
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FollowManager] 팔로잉 파싱 오류: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[FollowManager] FetchFollowing 실패: {request.error}");
            }

            DisplayCurrentList();
        }
    }

    #endregion

    #region Data Classes

    [Serializable]
    public class FollowUserData
    {
        public string userId;
        public string username;
        public string avatarUrl;
        public bool isFollowing; // 내가 이 유저를 팔로우하고 있는지
        public int followerCount; // 이 유저의 팔로워 수 (정렬용)
    }

    [Serializable]
    private class FollowListResponse
    {
        public List<FollowerData> followers;
    }

    [Serializable]
    private class FollowingListResponse
    {
        public List<FollowingData> following;
    }

    [Serializable]
    private class FollowerData
    {
        public string user_id;
        public string username;
        public string avatar_url;
        public bool is_following; // 내가 이 팔로워를 팔로우하고 있는지 (맞팔 여부)
        public int follower_count; // 이 유저의 팔로워 수
    }

    [Serializable]
    private class FollowingData
    {
        public string user_id;
        public string username;
        public string avatar_url;
        public int follower_count; // 이 유저의 팔로워 수
    }

    #endregion
}
