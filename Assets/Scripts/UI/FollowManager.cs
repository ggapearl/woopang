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
            Debug.LogWarning("[FollowManager] FollowPanel을 찾을 수 없습니다. Tools > Woopang > Create FollowManager UI 실행 필요");
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
            titleText.fontSize = 60;
        }

        // 탭 텍스트 폰트 크기
        if (followersTab != null)
        {
            Text txt = followersTab.GetComponentInChildren<Text>();
            if (txt != null)
            {
                if (customFont != null) txt.font = customFont;
                txt.fontSize = 50;
            }
        }
        if (followingTab != null)
        {
            Text txt = followingTab.GetComponentInChildren<Text>();
            if (txt != null)
            {
                if (customFont != null) txt.font = customFont;
                txt.fontSize = 50;
            }
        }

        // 아이템 템플릿 크기 조정
        AdjustItemTemplateSize();
    }

    /// <summary>
    /// 아이템 템플릿 크기를 폰트에 맞게 조정 + ActionButton 생성
    /// </summary>
    private void AdjustItemTemplateSize()
    {
        if (itemTemplate == null) return;

        // 아이템 높이 증가 (200px)
        RectTransform itemRect = itemTemplate.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, 200);
        }

        LayoutElement itemLE = itemTemplate.GetComponent<LayoutElement>();
        if (itemLE != null)
        {
            itemLE.minHeight = 200;
            itemLE.preferredHeight = 200;
        }

        // 아바타 크기 증가 (120px)
        Transform avatar = itemTemplate.transform.Find("Avatar");
        if (avatar != null)
        {
            RectTransform avatarRect = avatar.GetComponent<RectTransform>();
            if (avatarRect != null)
            {
                avatarRect.sizeDelta = new Vector2(120, 120);
                avatarRect.anchoredPosition = new Vector2(25, 0);
            }
        }

        // Username 위치 조정
        Transform username = itemTemplate.transform.Find("Username");
        if (username != null)
        {
            RectTransform usernameRect = username.GetComponent<RectTransform>();
            if (usernameRect != null)
            {
                usernameRect.offsetMin = new Vector2(170, 0); // 아바타 오른쪽
                usernameRect.offsetMax = new Vector2(-250, 0); // 버튼 왼쪽
            }
        }

        // ActionButton 생성 (없으면)
        Transform actionBtn = itemTemplate.transform.Find("ActionButton");
        if (actionBtn == null)
        {
            CreateActionButton(itemTemplate);
        }
        else
        {
            RectTransform actionRect = actionBtn.GetComponent<RectTransform>();
            if (actionRect != null)
            {
                actionRect.sizeDelta = new Vector2(200, 70);
                actionRect.anchoredPosition = new Vector2(-25, 0);
            }
        }
    }

    /// <summary>
    /// ActionButton을 아이템에 동적으로 생성
    /// </summary>
    private void CreateActionButton(GameObject item)
    {
        // ActionButton 컨테이너
        GameObject actionBtn = new GameObject("ActionButton");
        actionBtn.transform.SetParent(item.transform, false);

        RectTransform actionRect = actionBtn.AddComponent<RectTransform>();
        actionRect.anchorMin = new Vector2(1, 0.5f);
        actionRect.anchorMax = new Vector2(1, 0.5f);
        actionRect.pivot = new Vector2(1, 0.5f);
        actionRect.anchoredPosition = new Vector2(-25, 0);
        actionRect.sizeDelta = new Vector2(200, 70);

        Image actionImg = actionBtn.AddComponent<Image>();
        actionImg.color = new Color(0.35f, 0.45f, 0.95f, 1f); // 파란색

        Button btn = actionBtn.AddComponent<Button>();
        btn.targetGraphic = actionImg;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.45f, 0.55f, 1f, 1f);
        colors.pressedColor = new Color(0.25f, 0.35f, 0.85f, 1f);
        btn.colors = colors;

        // 터치 피드백 추가
        actionBtn.AddComponent<UITouchForwarder>();

        // 버튼 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(actionBtn.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text btnText = textObj.AddComponent<Text>();
        btnText.text = "액션";
        btnText.fontSize = 36;
        btnText.color = Color.white;
        btnText.alignment = TextAnchor.MiddleCenter;
        if (customFont != null)
            btnText.font = customFont;
        else
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    /// <summary>
    /// 메시지 버튼을 아이템에 동적으로 생성 (팔로워 목록용)
    /// </summary>
    private void CreateMessageButton(GameObject item, string userId, string username)
    {
        // 기존 MessageButton이 있으면 제거
        Transform existingMsgBtn = item.transform.Find("MessageButton");
        if (existingMsgBtn != null)
            Destroy(existingMsgBtn.gameObject);

        // MessageButton 생성
        GameObject msgBtn = new GameObject("MessageButton");
        msgBtn.transform.SetParent(item.transform, false);

        RectTransform msgRect = msgBtn.AddComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(1, 0.5f);
        msgRect.anchorMax = new Vector2(1, 0.5f);
        msgRect.pivot = new Vector2(1, 0.5f);
        msgRect.anchoredPosition = new Vector2(-25, 0);
        msgRect.sizeDelta = new Vector2(100, 60);

        Image msgImg = msgBtn.AddComponent<Image>();
        msgImg.color = new Color(0.2f, 0.6f, 0.9f, 1f); // 파란색

        Button btn = msgBtn.AddComponent<Button>();
        btn.targetGraphic = msgImg;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.7f, 1f, 1f);
        colors.pressedColor = new Color(0.1f, 0.5f, 0.8f, 1f);
        btn.colors = colors;

        // 터치 피드백 추가
        msgBtn.AddComponent<UITouchForwarder>();

        // 버튼 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(msgBtn.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text btnText = textObj.AddComponent<Text>();
        btnText.text = "DM";
        btnText.fontSize = 32;
        btnText.color = Color.white;
        btnText.alignment = TextAnchor.MiddleCenter;
        if (customFont != null)
            btnText.font = customFont;
        else
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 클릭 리스너
        string capturedUserId = userId;
        string capturedUsername = username;
        btn.onClick.AddListener(() => OnMessageButtonClicked(capturedUserId, capturedUsername));
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
            Debug.Log("[FollowManager] 프로필 패널로 돌아감");
        }
        else
        {
            Debug.LogWarning("[FollowManager] ProfileManager 또는 fullProfilePanel을 찾을 수 없습니다.");
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
#if UNITY_EDITOR
        // 에디터에서는 더미 데이터 사용
        LoadDummyData();
#else
        // 빌드에서는 API 호출
        if (showingFollowers)
            StartCoroutine(FetchFollowers());
        else
            StartCoroutine(FetchFollowing());
#endif
    }

    /// <summary>
    /// 더미 데이터 로드 (에디터 테스트용)
    /// </summary>
    private void LoadDummyData()
    {
        followersList.Clear();
        followingList.Clear();

        // 팔로워 더미 데이터 (일부는 맞팔로우, 일부는 아님)
        // 정렬 테스트: 맞팔로우 + 팔로워 많은 순으로 정렬되어야 함
        string[] followerNames = {
            "김민지", "이준호", "박서연", "최영수", "정하늘",
            "한소희", "강다니엘", "송지은", "유재석", "아이유",
            "손흥민", "김연아", "BTS_RM", "블랙핑크제니", "뉴진스하니",
            "이도현", "전지현", "공유", "박보검", "김수현"
        };
        int[] followerCounts = {
            1500, 50, 3000, 200, 800,
            100, 5000, 30, 12000, 25000,
            8500, 15000, 45000, 38000, 22000,
            6000, 9500, 11000, 18000, 28000
        };
        bool[] isFollowings = {
            true, false, true, false, true,
            false, false, false, true, true,
            false, true, false, true, false,
            true, false, false, true, false
        };

        for (int i = 0; i < followerNames.Length; i++)
        {
            followersList.Add(new FollowUserData
            {
                userId = $"follower_{i}",
                username = followerNames[i],
                avatarUrl = null,
                isFollowing = isFollowings[i],
                followerCount = followerCounts[i]
            });
        }

        // 팔로잉 더미 데이터 (내가 팔로우하는 사람들)
        string[] followingNames = {
            "WOOPANG_Official", "AR크리에이터", "여행작가", "사진가민수", "개발자현우",
            "디자이너수진", "맛집탐방러", "피트니스코치", "게임스트리머", "음악프로듀서",
            "패션인플루언서", "테크리뷰어", "독서모임", "펫스타그램", "캠핑마스터",
            "바리스타민호", "요리연구가", "풍경사진가"
        };
        int[] followingFollowerCounts = {
            50000, 1200, 8000, 450, 300,
            2500, 15000, 8900, 32000, 5600,
            28000, 12000, 800, 6500, 4200,
            1800, 9800, 7300
        };

        for (int i = 0; i < followingNames.Length; i++)
        {
            followingList.Add(new FollowUserData
            {
                userId = $"following_{i}",
                username = followingNames[i],
                avatarUrl = null,
                isFollowing = true, // 팔로잉 목록이니까 당연히 팔로우 중
                followerCount = followingFollowerCounts[i]
            });
        }

        DisplayCurrentList();
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

        if (itemTemplate == null)
        {
            Debug.LogError("[FollowManager] itemTemplate이 null입니다!");
            return;
        }

        foreach (var user in sortedList)
        {
            GameObject item = Instantiate(itemTemplate, contentParent);
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
        // Avatar 찾기
        Transform avatarTr = item.transform.Find("Avatar");
        if (avatarTr != null)
        {
            Image avatarImg = avatarTr.GetComponent<Image>();
            if (avatarImg != null)
            {
                if (!string.IsNullOrEmpty(user.avatarUrl))
                {
                    StartCoroutine(LoadAvatar(user.avatarUrl, avatarImg));
                }
                else
                {
                    // 기본 아바타 색상 생성 (유저이름 기반 해시로 일관된 색상)
                    avatarImg.color = GetAvatarColorFromName(user.username);
                }
            }
        }

        // Username 찾기 + 폰트 적용
        Transform usernameTr = item.transform.Find("Username");
        if (usernameTr != null)
        {
            Text usernameText = usernameTr.GetComponent<Text>();
            if (usernameText != null)
            {
                usernameText.text = user.username;
                usernameText.fontSize = defaultFontSize; // 60
                if (customFont != null) usernameText.font = customFont;
            }
        }

        // ActionButton 설정 (팔로워/팔로잉에 따라 다름)
        Transform actionBtnTr = item.transform.Find("ActionButton");
        if (actionBtnTr != null)
        {
            Button actionBtn = actionBtnTr.GetComponent<Button>();
            Text actionText = actionBtnTr.GetComponentInChildren<Text>();
            Image actionBtnImg = actionBtnTr.GetComponent<Image>();
            RectTransform actionRect = actionBtnTr.GetComponent<RectTransform>();

            if (actionText != null)
            {
                // 폰트 적용
                actionText.fontSize = 36; // 버튼 텍스트는 약간 작게
                if (customFont != null) actionText.font = customFont;

                if (showingFollowers)
                {
                    // 팔로워 목록: 맞팔로우/팔로우 버튼
                    actionText.text = user.isFollowing ? "팔로잉" : "팔로우";

                    // 이미 팔로우 중이면 회색 배경
                    if (actionBtnImg != null)
                    {
                        actionBtnImg.color = user.isFollowing
                            ? new Color(0.3f, 0.3f, 0.35f, 1f)  // 회색
                            : new Color(0.35f, 0.45f, 0.95f, 1f); // 파란색
                    }

                    // ActionButton을 왼쪽으로 이동 (메시지 버튼 공간 확보)
                    if (actionRect != null)
                    {
                        actionRect.anchoredPosition = new Vector2(-130, 0);
                        actionRect.sizeDelta = new Vector2(100, 60);
                    }
                }
                else
                {
                    // 팔로잉 목록: 메시지 버튼
                    actionText.text = "메시지";
                    if (actionBtnImg != null)
                    {
                        actionBtnImg.color = new Color(0.3f, 0.3f, 0.35f, 1f); // 회색
                    }
                }
            }

            // 액션 버튼 클릭 리스너
            if (actionBtn != null)
            {
                actionBtn.onClick.RemoveAllListeners();
                string capturedUserId = user.userId;
                string capturedUsername = user.username;
                bool capturedIsFollowing = user.isFollowing;

                if (showingFollowers)
                {
                    // 팔로우/언팔로우 토글
                    actionBtn.onClick.AddListener(() => OnFollowButtonClicked(capturedUserId, capturedUsername, capturedIsFollowing, actionBtn));
                }
                else
                {
                    // 메시지 보내기
                    actionBtn.onClick.AddListener(() => OnMessageButtonClicked(capturedUserId, capturedUsername));
                }
            }
        }

        // 팔로워 목록에서는 메시지 버튼 추가
        if (showingFollowers)
        {
            CreateMessageButton(item, user.userId, user.username);
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
        Debug.Log($"[FollowManager] 아이템 클릭: {userId} ({username})");

        // FollowPanel 숨기기 (Close 대신 hide만)
        if (panel != null)
            panel.SetActive(false);

        // ProfileManager에게 FollowPanel에서 왔다고 알림
        ProfileManager.openedFromFollowPanel = true;

        // ProfileManager를 통해 프로필 열기
        if (ProfileManager.Instance != null)
        {
#if UNITY_EDITOR
            // 에디터에서는 테스트 프로필 사용 (API 호출 없이)
            ProfileManager.Instance.ShowTestProfile(userId, username);
#else
            // 빌드에서는 실제 API 호출
            ProfileManager.Instance.ShowProfile(userId);
#endif
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
        Debug.Log($"[FollowManager] ★ 메시지 버튼 클릭: {userId} ({username})");

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
#if UNITY_EDITOR
        // 에디터에서는 즉시 UI 업데이트만
        yield return null;
        UpdateFollowButton(button, userId, true);
        Debug.Log($"[FollowManager] (에디터) {userId} 팔로우 완료");
#else
        string url = $"{BASE_URL}/api/users/{userId}/follow";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            string token = PlayerPrefs.GetString("auth_token", "");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                UpdateFollowButton(button, userId, true);
                Debug.Log($"[FollowManager] {userId} 팔로우 성공");
            }
            else
            {
                Debug.LogError($"[FollowManager] 팔로우 실패: {request.error}");
            }
        }
#endif
    }

    /// <summary>
    /// 언팔로우 API 호출
    /// </summary>
    private IEnumerator UnfollowUser(string userId, Button button)
    {
#if UNITY_EDITOR
        // 에디터에서는 즉시 UI 업데이트만
        yield return null;
        UpdateFollowButton(button, userId, false);
        Debug.Log($"[FollowManager] (에디터) {userId} 언팔로우 완료");
#else
        string url = $"{BASE_URL}/api/users/{userId}/unfollow";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            string token = PlayerPrefs.GetString("auth_token", "");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                UpdateFollowButton(button, userId, false);
                Debug.Log($"[FollowManager] {userId} 언팔로우 성공");
            }
            else
            {
                Debug.LogError($"[FollowManager] 언팔로우 실패: {request.error}");
            }
        }
#endif
    }

    /// <summary>
    /// 팔로우 버튼 UI 업데이트
    /// </summary>
    private void UpdateFollowButton(Button button, string userId, bool isFollowing)
    {
        if (button == null) return;

        Text btnText = button.GetComponentInChildren<Text>();
        Image btnImage = button.GetComponent<Image>();

        if (btnText != null)
        {
            btnText.text = isFollowing ? "팔로잉" : "팔로우";
        }

        if (btnImage != null)
        {
            btnImage.color = isFollowing
                ? new Color(0.3f, 0.3f, 0.35f, 1f)  // 회색
                : new Color(0.35f, 0.45f, 0.95f, 1f); // 파란색
        }

        // 버튼 클릭 리스너 업데이트 (토글)
        button.onClick.RemoveAllListeners();
        string capturedUserId = userId;
        button.onClick.AddListener(() =>
        {
            if (isFollowing)
                StartCoroutine(UnfollowUser(capturedUserId, button));
            else
                StartCoroutine(FollowUser(capturedUserId, button));
        });
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
    /// 아바타 이미지 로드
    /// </summary>
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

    private IEnumerator LoadAvatar(string url, Image targetImage)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                targetImage.sprite = sprite;
            }
        }
    }

    #region API Calls

    private IEnumerator FetchFollowers()
    {
        // 로그인된 사용자 ID (맞팔 여부 확인용)
        string requesterId = LoginManager.Instance?.CurrentUser?.id ?? currentUserId;
        string url = $"{BASE_URL}/api/followers?user_id={currentUserId}&requester_id={requesterId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
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

            DisplayCurrentList();
        }
    }

    private IEnumerator FetchFollowing()
    {
        string url = $"{BASE_URL}/api/following?user_id={currentUserId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
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
                                isFollowing = true, // 팔로잉 목록이므로 당연히 팔로우 중
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
