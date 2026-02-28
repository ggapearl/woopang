/**
 * P2PUserListPanel.cs
 * 좌측 하단 사용자 목록 패널 (PlaceListManager와 탭 전환)
 * - 장소/사용자 탭 전환
 * - 거리 슬라이더 (0~10000m)
 * - 프라이버시 설정 토글
 * - 사용자 목록 동적 업데이트
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-01
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class P2PUserListPanel : MonoBehaviour
{
    public static P2PUserListPanel Instance { get; private set; }

    [Header("Tab Switching")]
    public Button placeTabButton;
    public Button userTabButton;
    public GameObject placeListContent;   // PlaceListManager의 Content
    public GameObject userListContent;    // P2P 전용 Content

    [Header("Distance Slider")]
    public Slider distanceSlider;
    public TextMeshProUGUI distanceValueText;
    [SerializeField] private float minDistance = 100f;
    [SerializeField] private float maxDistance = 10000f;

    [Header("Privacy Toggles")]
    public Toggle publicToggle;
    public Toggle privateToggle;
    public Toggle followingOnlyToggle;
    public Toggle followersOnlyToggle;
    public ToggleGroup privacyToggleGroup;

    [Header("User List")]
    public ScrollRect userScrollView;
    public GameObject userListItemPrefab;
    public Transform userListContainer;
    public TextMeshProUGUI emptyListText;

    [Header("Settings")]
    [SerializeField] private float updateInterval = 5f; // 5초마다 업데이트
    [SerializeField] private Color activeTabColor = new Color(0.3f, 0.7f, 1f);
    [SerializeField] private Color inactiveTabColor = Color.gray;

    private string currentVisibilityMode = "public";
    private float currentDistance = 1000f;
    private Coroutine updateListCoroutine;

    private Dictionary<string, GameObject> userListItems = new Dictionary<string, GameObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        InitializeUI();
        LoadSavedSettings();

        // 기본적으로 장소 탭 활성화
        SwitchTab("place");
    }

    private void InitializeUI()
    {
        // 탭 버튼 리스너
        if (placeTabButton != null)
            placeTabButton.onClick.AddListener(() => SwitchTab("place"));

        if (userTabButton != null)
            userTabButton.onClick.AddListener(() => SwitchTab("user"));

        // 거리 슬라이더 설정
        if (distanceSlider != null)
        {
            distanceSlider.minValue = minDistance;
            distanceSlider.maxValue = maxDistance;
            distanceSlider.value = currentDistance;
            distanceSlider.onValueChanged.AddListener(OnDistanceChanged);
            UpdateDistanceText();
        }

        // 프라이버시 토글 리스너 (하나만 선택 가능)
        if (publicToggle != null)
        {
            publicToggle.onValueChanged.AddListener((val) => {
                if (val) SetVisibilityMode("public");
            });
        }

        if (privateToggle != null)
        {
            privateToggle.onValueChanged.AddListener((val) => {
                if (val) SetVisibilityMode("private");
            });
        }

        if (followingOnlyToggle != null)
        {
            followingOnlyToggle.onValueChanged.AddListener((val) => {
                if (val) SetVisibilityMode("following_only");
            });
        }

        if (followersOnlyToggle != null)
        {
            followersOnlyToggle.onValueChanged.AddListener((val) => {
                if (val) SetVisibilityMode("followers_only");
            });
        }

        // 기본값 설정
        if (publicToggle != null)
            publicToggle.isOn = true;
    }

    private void LoadSavedSettings()
    {
        // 저장된 거리 설정 로드
        currentDistance = PlayerPrefs.GetFloat("P2PMaxDistance", 1000f);
        if (distanceSlider != null)
            distanceSlider.value = currentDistance;

        // 저장된 프라이버시 설정 로드
        currentVisibilityMode = PlayerPrefs.GetString("P2PVisibilityMode", "public");
        UpdatePrivacyToggles();
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("P2PMaxDistance", currentDistance);
        PlayerPrefs.SetString("P2PVisibilityMode", currentVisibilityMode);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 탭 전환 (장소 ↔ 사용자)
    /// </summary>
    public void SwitchTab(string tabName)
    {
        if (tabName == "place")
        {
            // 장소 탭 활성화
            if (placeListContent != null) placeListContent.SetActive(true);
            if (userListContent != null) userListContent.SetActive(false);

            // 탭 버튼 색상 변경
            if (placeTabButton != null)
                placeTabButton.GetComponent<Image>().color = activeTabColor;
            if (userTabButton != null)
                userTabButton.GetComponent<Image>().color = inactiveTabColor;

            // 사용자 목록 업데이트 중지
            if (updateListCoroutine != null)
            {
                StopCoroutine(updateListCoroutine);
                updateListCoroutine = null;
            }
        }
        else if (tabName == "user")
        {
            // 사용자 탭 활성화
            if (placeListContent != null) placeListContent.SetActive(false);
            if (userListContent != null) userListContent.SetActive(true);

            // 탭 버튼 색상 변경
            if (placeTabButton != null)
                placeTabButton.GetComponent<Image>().color = inactiveTabColor;
            if (userTabButton != null)
                userTabButton.GetComponent<Image>().color = activeTabColor;

            // 사용자 목록 업데이트 시작
            RefreshUserList();
            if (updateListCoroutine == null)
            {
                updateListCoroutine = StartCoroutine(UpdateUserListPeriodically());
            }
        }
    }

    /// <summary>
    /// 거리 슬라이더 변경 시
    /// </summary>
    private void OnDistanceChanged(float value)
    {
        currentDistance = value;
        UpdateDistanceText();

        // P2PManager에 거리 설정 전달
        if (P2PManager.Instance != null)
        {
            P2PManager.Instance.SetMaxTrackingDistance(value);
        }

        SaveSettings();

        // 목록 즉시 업데이트
        RefreshUserList();
    }

    private void UpdateDistanceText()
    {
        if (distanceValueText == null) return;

        if (currentDistance < 1000f)
        {
            distanceValueText.text = $"{Mathf.RoundToInt(currentDistance)} m";
        }
        else
        {
            distanceValueText.text = $"{(currentDistance / 1000f):F1} km";
        }
    }

    /// <summary>
    /// 프라이버시 모드 설정
    /// </summary>
    private void SetVisibilityMode(string mode)
    {
        currentVisibilityMode = mode;
        UpdatePrivacyToggles();

        // 서버에 프라이버시 설정 전송
        if (P2PManager.Instance != null)
        {
            P2PManager.Instance.UpdatePrivacySettings(mode, Mathf.RoundToInt(currentDistance));
        }

        SaveSettings();
    }

    private void UpdatePrivacyToggles()
    {
        if (publicToggle != null)
            publicToggle.SetIsOnWithoutNotify(currentVisibilityMode == "public");
        if (privateToggle != null)
            privateToggle.SetIsOnWithoutNotify(currentVisibilityMode == "private");
        if (followingOnlyToggle != null)
            followingOnlyToggle.SetIsOnWithoutNotify(currentVisibilityMode == "following_only");
        if (followersOnlyToggle != null)
            followersOnlyToggle.SetIsOnWithoutNotify(currentVisibilityMode == "followers_only");
    }

    /// <summary>
    /// 주기적으로 사용자 목록 업데이트
    /// </summary>
    private IEnumerator UpdateUserListPeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            RefreshUserList();
        }
    }

    /// <summary>
    /// 사용자 목록 갱신
    /// </summary>
    public void RefreshUserList()
    {
        if (P2PManager.Instance == null || userListContainer == null)
            return;

        // P2PManager에서 근처 사용자 데이터 가져오기
        var nearbyUsers = P2PManager.Instance.GetNearbyUsers();

        // 현재 거리 내의 사용자만 필터링
        var filteredUsers = nearbyUsers
            .Where(u => u.distance <= currentDistance)
            .OrderBy(u => u.distance)
            .ToList();

        // 기존 아이템 제거
        foreach (var item in userListItems.Values)
        {
            if (item != null)
                Destroy(item);
        }
        userListItems.Clear();

        // 빈 목록 처리
        if (filteredUsers.Count == 0)
        {
            if (emptyListText != null)
            {
                emptyListText.gameObject.SetActive(true);
                emptyListText.text = currentVisibilityMode == "private"
                    ? "비공개 모드입니다"
                    : "근처에 사용자가 없습니다";
            }
            return;
        }

        if (emptyListText != null)
            emptyListText.gameObject.SetActive(false);

        // 새로운 아이템 생성
        foreach (var userData in filteredUsers)
        {
            CreateUserListItem(userData);
        }
    }

    /// <summary>
    /// 사용자 목록 아이템 생성
    /// </summary>
    private void CreateUserListItem(NearbyUserData userData)
    {
        if (userListItemPrefab == null || userListContainer == null)
            return;

        GameObject item = Instantiate(userListItemPrefab, userListContainer);

        // UI 컴포넌트 찾기
        TextMeshProUGUI usernameText = item.transform.Find("UsernameText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI distanceText = item.transform.Find("DistanceText")?.GetComponent<TextMeshProUGUI>();
        Image avatarIcon = item.transform.Find("AvatarIcon")?.GetComponent<Image>();
        Button itemButton = item.GetComponent<Button>();

        // 데이터 설정
        if (usernameText != null)
            usernameText.text = userData.username;

        if (distanceText != null)
        {
            if (userData.distance < 1000f)
                distanceText.text = $"{Mathf.RoundToInt(userData.distance)}m";
            else
                distanceText.text = $"{(userData.distance / 1000f):F1}km";
        }

        // 아바타 이미지 로드 (향후 구현)
        // if (avatarIcon != null && !string.IsNullOrEmpty(userData.avatar_url))
        // {
        //     StartCoroutine(LoadAvatarImage(avatarIcon, userData.avatar_url));
        // }

        // 클릭 이벤트: 해당 사용자 아바타로 카메라 이동
        if (itemButton != null)
        {
            itemButton.onClick.AddListener(() => OnUserListItemClicked(userData.user_id));
        }

        userListItems[userData.user_id] = item;
    }

    /// <summary>
    /// 사용자 목록 아이템 클릭 시
    /// </summary>
    private void OnUserListItemClicked(string userId)
    {
        // P2PManager를 통해 해당 사용자 아바타 위치로 카메라 이동
        if (P2PManager.Instance != null)
        {
            P2PManager.Instance.FocusOnUser(userId);
        }

    }

    void OnDestroy()
    {
        if (updateListCoroutine != null)
        {
            StopCoroutine(updateListCoroutine);
        }
    }
}
