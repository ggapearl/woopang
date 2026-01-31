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
            // Toggle 컴포넌트가 없으면 조용히 스킵 (씬 구성에 따라 선택적으로 사용)
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

        // 클릭 카운트 설정 (순환: 팔로잉 -> 전체 -> 제외)
        switch (currentMode)
        {
            case UserFilterMode.FollowingOnly:
                clickCount = 0;
                break;
            case UserFilterMode.All:
                clickCount = 1;
                break;
            case UserFilterMode.None:
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
        // 3단계 순환: 팔로잉(핑크색) -> 전체(흰색) -> 제외(회색)
        switch (currentMode)
        {
            case UserFilterMode.FollowingOnly:
                currentMode = UserFilterMode.All;
                clickCount = 1;
                break;
            case UserFilterMode.All:
                currentMode = UserFilterMode.None;
                clickCount = 2;
                break;
            case UserFilterMode.None:
                currentMode = UserFilterMode.FollowingOnly;
                clickCount = 0;
                break;
        }

        SaveFilterMode();
        UpdateVisualState();
        ApplyFilterMode();
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

        // 클릭 카운트 설정 (순환: 팔로잉 -> 전체 -> 제외)
        switch (mode)
        {
            case UserFilterMode.FollowingOnly:
                clickCount = 0;
                break;
            case UserFilterMode.All:
                clickCount = 1;
                break;
            case UserFilterMode.None:
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
