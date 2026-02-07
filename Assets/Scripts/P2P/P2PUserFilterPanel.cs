/**
 * P2PUserFilterPanel.cs
 * P2P 사용자 필터 UI 컨트롤러
 * - FilterButtonPanel.prefab 내 P2PUserToggle 에 부착
 * - 하나의 토글로 3가지 상태 순환:
 *   1. P2P(팔로잉) - 핑크색: 팔로잉만 표시 (FollowingOnly)
 *   2. P2P(전체) - 흰색: 모든 사용자 표시 (All)
 *   3. P2P(제외) - 회색: 아바타 숨김 (None)
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-11
 * Modified: 2026-01-27 - 색상 및 순서 변경 (팔로잉=핑크, 전체=흰색, 제외=회색)
 * Modified: 2026-02-03 - 로그인 필수 기능으로 변경 (비로그인 시 LoginPrompt 표시)
 */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// P2P 사용자 필터 토글 - PetFriendlyToggle과 동일한 구조
/// 토글을 반복 클릭하면 3가지 상태가 순환됨
/// ※ P2P 추적 기능은 로그인 필수 - 비로그인 시 LoginPrompt 표시
/// </summary>
public class P2PUserFilterPanel : MonoBehaviour
{
    [Header("P2P User Filter Toggle")]
    [Tooltip("P2P 사용자 필터 토글 (FilterButtonPanel 내 P2PUserToggle)")]
    [SerializeField] private Toggle p2pUserToggle;

    [Header("Visual Settings")]
    [Tooltip("팔로잉 모드 배경색 (핑크색)")]
    [SerializeField] private Color followingColor = new Color(0.91f, 0.33f, 0.63f, 1f); // #E854A1
    [Tooltip("전체 모드 배경색 (흰색)")]
    [SerializeField] private Color allColor = Color.white;
    [Tooltip("제외 모드 배경색 (회색)")]
    [SerializeField] private Color noneColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("Label Settings")]
    [Tooltip("토글 옆 레이블 텍스트 (TMPro)")]
    [SerializeField] private TMPro.TextMeshProUGUI labelText;
    [Tooltip("토글 옆 레이블 텍스트 (Legacy)")]
    [SerializeField] private Text legacyLabelText;
    [Tooltip("팔로잉 모드 레이블")]
    [SerializeField] private string followingLabel = "P2P(팔로잉)";
    [Tooltip("전체 모드 레이블")]
    [SerializeField] private string allLabel = "P2P(전체)";
    [Tooltip("제외 모드 레이블")]
    [SerializeField] private string noneLabel = "P2P(제외)";

    private UserFilterMode currentMode = UserFilterMode.All;  // 기본값: 전체
    private const string FILTER_MODE_KEY = "P2PUserFilterMode";

    private Image backgroundImage;
    private Image checkmarkImage;
    // 로그인 관련
    private LoginManager loginManager;
    private bool pendingToggleAction = false;  // 로그인 후 토글 액션 대기

    void Start()
    {
        // 토글 참조가 없으면 자신 → 자식 "P2PUserToggle" 순서로 찾기
        if (p2pUserToggle == null)
        {
            p2pUserToggle = GetComponent<Toggle>();
        }
        if (p2pUserToggle == null)
        {
            Transform toggleChild = transform.Find("P2PUserToggle");
            if (toggleChild != null)
                p2pUserToggle = toggleChild.GetComponent<Toggle>();
        }

        if (p2pUserToggle == null)
        {
            return;
        }

        // LoginManager 참조
        loginManager = LoginManager.Instance;
        if (loginManager == null)
        {
            loginManager = FindFirstObjectByType<LoginManager>();
        }

        // 로그인 상태 변경 이벤트 구독
        if (loginManager != null)
        {
            loginManager.OnLoginStateChanged += OnLoginStateChanged;
        }

        // Background와 Checkmark 이미지 찾기
        FindVisualComponents();

        // 저장된 필터 모드 로드
        LoadFilterMode();

        // 로그인 여부 체크 - 비로그인 시 None 상태로 강제
        CheckLoginAndSetInitialState();

        // 토글 이벤트 설정
        SetupToggle();

        // 초기 상태 반영
        UpdateVisualState();

        // P2PManager에 현재 모드 설정
        ApplyFilterMode();
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (loginManager != null)
        {
            loginManager.OnLoginStateChanged -= OnLoginStateChanged;
        }
    }

    /// <summary>
    /// 로그인 상태에 따라 초기 상태 설정
    /// 비로그인 시 None(제외) 모드로 강제 설정
    /// </summary>
    private void CheckLoginAndSetInitialState()
    {
        bool isLoggedIn = IsUserLoggedIn();

        if (!isLoggedIn)
        {
            // 비로그인 시 None 상태로 강제
            currentMode = UserFilterMode.None;
        }
    }

    /// <summary>
    /// 로그인 여부 확인 (게스트 로그인 제외)
    /// </summary>
    private bool IsUserLoggedIn()
    {
        if (loginManager == null)
        {
            loginManager = LoginManager.Instance;
            if (loginManager == null)
            {
                loginManager = FindFirstObjectByType<LoginManager>();
            }
        }

        // 로그인 매니저가 없거나, 로그인 안됨, 또는 게스트인 경우 false
        if (loginManager == null) return false;
        if (!loginManager.IsLoggedIn) return false;
        if (loginManager.IsGuest) return false;

        return true;
    }

    /// <summary>
    /// 로그인 상태 변경 시 호출
    /// </summary>
    private void OnLoginStateChanged(bool isLoggedIn)
    {
        if (isLoggedIn && !loginManager.IsGuest)
        {
            // 로그인 완료 - 이전에 시도했던 토글 액션 수행
            if (pendingToggleAction)
            {
                pendingToggleAction = false;
                // 저장된 필터 모드 복원
                LoadFilterMode();
                UpdateVisualState();
                ApplyFilterMode();
            }
        }
        else
        {
            // 로그아웃 또는 게스트 - None 상태로 변경
            currentMode = UserFilterMode.None;
            SaveFilterMode();
            UpdateVisualState();
            ApplyFilterMode();
        }
    }

    private void FindVisualComponents()
    {
        // Background 찾기 (토글의 targetGraphic)
        backgroundImage = p2pUserToggle.targetGraphic as Image;

        // Checkmark 찾기 (토글의 graphic)
        checkmarkImage = p2pUserToggle.graphic as Image;

        // 또는 토글 자식에서 찾기 (P2PUserFilterPanel이 root에 있을 경우 대비)
        if (backgroundImage == null)
        {
            Transform bg = p2pUserToggle.transform.Find("Background");
            if (bg != null) backgroundImage = bg.GetComponent<Image>();
        }

        if (checkmarkImage == null)
        {
            Transform bg = p2pUserToggle.transform.Find("Background");
            if (bg != null)
            {
                Transform check = bg.Find("Checkmark");
                if (check != null) checkmarkImage = check.GetComponent<Image>();
            }
        }

        // 라벨 텍스트 자동 찾기 (Inspector에서 연결 안 된 경우)
        if (labelText == null && legacyLabelText == null)
        {
            // 1. 토글 자식에서 TMPro 찾기
            labelText = p2pUserToggle.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

            // 2. TMPro 없으면 Legacy Text 찾기
            if (labelText == null)
            {
                legacyLabelText = p2pUserToggle.GetComponentInChildren<Text>(true);
            }

            // 3. 부모에서 찾기
            if (labelText == null && legacyLabelText == null && transform.parent != null)
            {
                labelText = transform.parent.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (labelText == null)
                {
                    legacyLabelText = transform.parent.GetComponentInChildren<Text>(true);
                }
            }

            // 4. "Label" 이름으로 찾기
            if (labelText == null && legacyLabelText == null)
            {
                Transform labelTransform = transform.Find("Label");
                if (labelTransform != null)
                {
                    labelText = labelTransform.GetComponent<TMPro.TextMeshProUGUI>();
                    if (labelText == null)
                        legacyLabelText = labelTransform.GetComponent<Text>();
                }
            }
        }
    }

    private void LoadFilterMode()
    {
        // 기본값: All (1) - 모든 사용자 표시 (흰색)
        int savedMode = PlayerPrefs.GetInt(FILTER_MODE_KEY, 1);

        currentMode = (UserFilterMode)savedMode;
    }

    private void SaveFilterMode()
    {
        PlayerPrefs.SetInt(FILTER_MODE_KEY, (int)currentMode);
        PlayerPrefs.Save();
    }

    private void SetupToggle()
    {
        // 토글 값 변경 이벤트 대신 클릭 이벤트 사용
        // Toggle의 onValueChanged를 오버라이드하여 3상태 순환 구현
        p2pUserToggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnToggleValueChanged(bool isOn)
    {
        // 로그인 여부 체크
        bool isLoggedIn = IsUserLoggedIn();

        // 비로그인 상태에서 None이 아닌 상태로 변경하려는 경우
        if (!isLoggedIn)
        {
            // 현재 None 상태에서 벗어나려고 할 때 로그인 요구
            if (currentMode == UserFilterMode.None)
            {
                // 토글 상태를 원래대로 유지 (변경 안함)
                p2pUserToggle.SetIsOnWithoutNotify(true);
                UpdateVisualState();

                // 로그인 프롬프트 표시
                ShowLoginPrompt();
                return;
            }
        }

        // 3단계 순환: 팔로잉(핑크색) -> 전체(흰색) -> 제외(회색)
        switch (currentMode)
        {
            case UserFilterMode.FollowingOnly:
                currentMode = UserFilterMode.All;
                break;
            case UserFilterMode.All:
                currentMode = UserFilterMode.None;
                break;
            case UserFilterMode.None:
                // 비로그인 시 여기 도달 불가 (위에서 처리)
                currentMode = UserFilterMode.FollowingOnly;
                break;
        }

        SaveFilterMode();
        UpdateVisualState();
        ApplyFilterMode();
    }

    /// <summary>
    /// 로그인 프롬프트 표시
    /// ListPanel은 열린 상태 유지, LoginPrompt만 표시
    /// </summary>
    private void ShowLoginPrompt()
    {
        pendingToggleAction = true;  // 로그인 완료 후 토글 액션 대기

        if (loginManager != null)
        {
            // LoginManager의 ShowLoginRequirementPopup 사용
            loginManager.ShowLoginRequirementPopup();
        }
        else
        {
            Debug.LogWarning("[P2PUserFilterPanel] LoginManager를 찾을 수 없습니다.");
        }
    }

    private void UpdateVisualState()
    {
        // isOn은 항상 true로 유지 (Unity Toggle 색상 처리 방지)
        if (p2pUserToggle != null)
            p2pUserToggle.SetIsOnWithoutNotify(true);

        Color bgColor = followingColor;
        string label = followingLabel;

        switch (currentMode)
        {
            case UserFilterMode.FollowingOnly:
                // 팔로잉 - 핑크색
                bgColor = followingColor;
                label = followingLabel;
                break;

            case UserFilterMode.All:
                // 전체 - 흰색
                bgColor = allColor;
                label = allLabel;
                break;

            case UserFilterMode.None:
                // 제외 - 회색
                bgColor = noneColor;
                label = noneLabel;
                break;
        }

        // 배경과 체크마크 모두 같은 색상으로 설정
        if (backgroundImage != null)
            backgroundImage.color = bgColor;
        if (checkmarkImage != null)
            checkmarkImage.color = bgColor;

        // 레이블 텍스트 변경 (TMPro + Legacy 둘 다 지원)
        if (labelText != null)
            labelText.text = label;
        if (legacyLabelText != null)
            legacyLabelText.text = label;
    }

    private void ApplyFilterMode()
    {
        if (P2PManager.Instance != null)
        {
            P2PManager.Instance.SetUserFilterMode(currentMode);
        }
    }

    /// <summary>
    /// 현재 필터 모드 가져오기
    /// </summary>
    public UserFilterMode GetCurrentFilterMode()
    {
        return currentMode;
    }

    /// <summary>
    /// 외부에서 필터 모드 설정
    /// </summary>
    public void SetFilterModeExternal(UserFilterMode mode)
    {
        currentMode = mode;

        SaveFilterMode();
        UpdateVisualState();
        ApplyFilterMode();
    }

    /// <summary>
    /// 토글 참조 설정 (에디터에서 호출)
    /// </summary>
    public void SetToggle(Toggle toggle)
    {
        p2pUserToggle = toggle;
    }
}
