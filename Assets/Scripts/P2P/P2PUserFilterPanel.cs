/**
 * P2PUserFilterPanel.cs
 * P2P 사용자 필터 UI 컨트롤러
 * - FilterButtonPanel.prefab 내 P2PUserToggle 에 부착
 * - 하나의 토글로 3가지 상태 순환:
 *   1. 체크 안 됨 (흰색 배경): 아바타 숨김 (None)
 *   2. 체크됨 (기본): 모든 사용자 표시 (All)
 *   3. 체크됨 + 진한 핑크색: 팔로잉만 표시 (FollowingOnly)
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-11
 * Modified: 2026-01-12 - 3상태 순환 토글로 변경
 */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// P2P 사용자 필터 토글 - PetFriendlyToggle과 동일한 구조
/// 토글을 반복 클릭하면 3가지 상태가 순환됨
/// </summary>
public class P2PUserFilterPanel : MonoBehaviour
{
    [Header("P2P User Filter Toggle")]
    [Tooltip("P2P 사용자 필터 토글 (FilterButtonPanel 내 P2PUserToggle)")]
    [SerializeField] private Toggle p2pUserToggle;

    [Header("Visual Settings")]
    [Tooltip("기본 배경색 (모든 사용자)")]
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("팔로잉만 모드 배경색 (#e95383)")]
    [SerializeField] private Color followingOnlyColor = new Color(0.914f, 0.325f, 0.514f, 1f); // #e95383
    [Tooltip("체크마크 기본 색상")]
    [SerializeField] private Color checkmarkNormalColor = new Color(0.196f, 0.196f, 0.196f, 1f);
    [Tooltip("체크마크 팔로잉 모드 색상")]
    [SerializeField] private Color checkmarkFollowingColor = Color.white;

    [Header("Label Settings")]
    [Tooltip("토글 옆 레이블 텍스트 (TMPro)")]
    [SerializeField] private TMPro.TextMeshProUGUI labelText;
    [Tooltip("기본 레이블 (모든 사용자 / 해제)")]
    [SerializeField] private string defaultLabel = "P2P 사용자";
    [Tooltip("팔로잉만 모드 레이블")]
    [SerializeField] private string followingOnlyLabel = "팔로잉 사용자";

    private UserFilterMode currentMode = UserFilterMode.All;
    private const string FILTER_MODE_KEY = "P2PUserFilterMode";

    private Image backgroundImage;
    private Image checkmarkImage;
    private int clickCount = 0;

    void Start()
    {
        // 토글 참조가 없으면 자신에게서 찾기
        if (p2pUserToggle == null)
        {
            p2pUserToggle = GetComponent<Toggle>();
        }

        if (p2pUserToggle == null)
        {
            Debug.LogError("[P2PUserFilterPanel] Toggle component not found!");
            return;
        }

        // Background와 Checkmark 이미지 찾기
        FindVisualComponents();

        // 저장된 필터 모드 로드
        LoadFilterMode();

        // 토글 이벤트 설정
        SetupToggle();

        // 초기 상태 반영
        UpdateVisualState();

        // P2PManager에 현재 모드 설정
        ApplyFilterMode();
    }

    private void FindVisualComponents()
    {
        // Background 찾기 (토글의 targetGraphic)
        backgroundImage = p2pUserToggle.targetGraphic as Image;

        // Checkmark 찾기 (토글의 graphic)
        checkmarkImage = p2pUserToggle.graphic as Image;

        // 또는 자식에서 찾기
        if (backgroundImage == null)
        {
            Transform bg = transform.Find("Background");
            if (bg != null) backgroundImage = bg.GetComponent<Image>();
        }

        if (checkmarkImage == null)
        {
            Transform bg = transform.Find("Background");
            if (bg != null)
            {
                Transform check = bg.Find("Checkmark");
                if (check != null) checkmarkImage = check.GetComponent<Image>();
            }
        }
    }

    private void LoadFilterMode()
    {
        // 기본값: All (1) - 모든 사용자 표시
        // FollowingOnly (2)는 팔로잉 유저가 없으면 아무도 안 보임
        int savedMode = PlayerPrefs.GetInt(FILTER_MODE_KEY, 1); // 기본값: All (1)

        // 에디터에서 테스트 시 항상 All 모드로 시작 (팔로잉 데이터 없음)
#if UNITY_EDITOR
        savedMode = 1; // All 모드 강제
        Debug.Log("[P2PUserFilterPanel] 에디터 모드: All 필터로 강제 설정");
#endif

        currentMode = (UserFilterMode)savedMode;

        // 클릭 카운트 설정
        switch (currentMode)
        {
            case UserFilterMode.None:
                clickCount = 0;
                break;
            case UserFilterMode.All:
                clickCount = 1;
                break;
            case UserFilterMode.FollowingOnly:
                clickCount = 2;
                break;
        }
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
        // 클릭할 때마다 상태 순환
        clickCount++;
        if (clickCount > 2) clickCount = 0;

        switch (clickCount)
        {
            case 0: // 체크 안 됨 - 숨기기
                currentMode = UserFilterMode.None;
                p2pUserToggle.SetIsOnWithoutNotify(false);
                break;

            case 1: // 체크됨 - 모든 사용자
                currentMode = UserFilterMode.All;
                p2pUserToggle.SetIsOnWithoutNotify(true);
                break;

            case 2: // 체크됨 + 핑크색 - 팔로잉만
                currentMode = UserFilterMode.FollowingOnly;
                p2pUserToggle.SetIsOnWithoutNotify(true);
                break;
        }

        SaveFilterMode();
        UpdateVisualState();
        ApplyFilterMode();

        Debug.Log($"[P2PUserFilterPanel] Filter mode changed to: {currentMode}");
    }

    private void UpdateVisualState()
    {
        switch (currentMode)
        {
            case UserFilterMode.None:
                // 체크 안 됨 상태
                if (p2pUserToggle != null)
                    p2pUserToggle.SetIsOnWithoutNotify(false);
                if (backgroundImage != null)
                    backgroundImage.color = normalColor;
                if (checkmarkImage != null)
                    checkmarkImage.color = checkmarkNormalColor;
                // 레이블 텍스트 변경 (색상은 유지)
                if (labelText != null)
                    labelText.text = defaultLabel;
                break;

            case UserFilterMode.All:
                // 체크됨 - 기본 상태 (흰색 배경, 모든 사용자)
                if (p2pUserToggle != null)
                    p2pUserToggle.SetIsOnWithoutNotify(true);
                if (backgroundImage != null)
                    backgroundImage.color = normalColor;
                if (checkmarkImage != null)
                    checkmarkImage.color = checkmarkNormalColor;
                // 레이블 텍스트 변경 (색상은 유지)
                if (labelText != null)
                    labelText.text = defaultLabel;
                break;

            case UserFilterMode.FollowingOnly:
                // 체크됨 - 핑크색 (#e95383)
                if (p2pUserToggle != null)
                    p2pUserToggle.SetIsOnWithoutNotify(true);
                if (backgroundImage != null)
                    backgroundImage.color = followingOnlyColor;
                if (checkmarkImage != null)
                    checkmarkImage.color = checkmarkFollowingColor;
                // 레이블 텍스트 변경 (색상은 유지)
                if (labelText != null)
                    labelText.text = followingOnlyLabel;
                break;
        }
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

        switch (mode)
        {
            case UserFilterMode.None:
                clickCount = 0;
                break;
            case UserFilterMode.All:
                clickCount = 1;
                break;
            case UserFilterMode.FollowingOnly:
                clickCount = 2;
                break;
        }

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
