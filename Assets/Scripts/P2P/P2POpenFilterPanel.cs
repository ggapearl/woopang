/**
 * P2POpenFilterPanel.cs
 * 내 위치 공개 설정 UI 컨트롤러
 * - FilterButtonPanel.prefab 내 P2POpenToggle에 부착
 * - 하나의 토글로 3가지 상태 순환:
 *   1. 체크 안 됨 (흰색 배경): 비공개 (Private)
 *   2. 체크됨 (기본): 전체공개 (Public)
 *   3. 체크됨 + 진한 핑크색: 팔로잉에게만 공개 (FollowingOnly)
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-16
 */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 내 위치 공개 모드
/// </summary>
public enum LocationVisibilityMode
{
    Public,         // 전체공개
    FollowingOnly,  // 내가 팔로우하는 사람들에게만 공개
    Private         // 비공개
}

/// <summary>
/// P2P 내 위치 공개 설정 토글
/// 토글을 반복 클릭하면 3가지 상태가 순환됨
/// </summary>
public class P2POpenFilterPanel : MonoBehaviour
{
    [Header("P2P Open Filter Toggle")]
    [Tooltip("내 위치 공개 설정 토글 (FilterButtonPanel 내 P2POpenToggle)")]
    [SerializeField] private Toggle p2pOpenToggle;

    [Header("Visual Settings")]
    [Tooltip("기본 배경색 (전체공개)")]
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("팔로잉만 모드 배경색 (#e95383)")]
    [SerializeField] private Color followingOnlyColor = new Color(0.914f, 0.325f, 0.514f, 1f); // #e95383
    [Tooltip("체크마크 기본 색상")]
    [SerializeField] private Color checkmarkNormalColor = new Color(0.196f, 0.196f, 0.196f, 1f);
    [Tooltip("체크마크 팔로잉만 모드 색상")]
    [SerializeField] private Color checkmarkFollowingColor = Color.white;

    [Header("Label Settings")]
    [Tooltip("토글 옆 레이블 텍스트 (TMPro)")]
    [SerializeField] private TMPro.TextMeshProUGUI labelText;
    [Tooltip("전체공개 레이블")]
    [SerializeField] private string publicLabel = "위치 전체공개";
    [Tooltip("팔로잉만 레이블")]
    [SerializeField] private string followingOnlyLabel = "팔로잉에게만";
    [Tooltip("비공개 레이블")]
    [SerializeField] private string privateLabel = "위치 비공개";

    private LocationVisibilityMode currentMode = LocationVisibilityMode.Public;
    private const string VISIBILITY_MODE_KEY = "P2PLocationVisibilityMode";

    private Image backgroundImage;
    private Image checkmarkImage;
    private int clickCount = 0;

    void Start()
    {
        // 토글 참조가 없으면 자신에게서 찾기
        if (p2pOpenToggle == null)
        {
            p2pOpenToggle = GetComponent<Toggle>();
        }

        if (p2pOpenToggle == null)
        {
            Debug.LogError("[P2POpenFilterPanel] Toggle component not found!");
            return;
        }

        // Background와 Checkmark 이미지 찾기
        FindVisualComponents();
        FindLegacyLabel();

        // 저장된 공개 모드 로드
        LoadVisibilityMode();

        // 토글 이벤트 설정
        SetupToggle();

        // 초기 상태 반영
        UpdateVisualState();

        // P2PManager에 현재 모드 설정
        ApplyVisibilityMode();
    }

    private void FindVisualComponents()
    {
        // Background 찾기 (토글의 targetGraphic)
        backgroundImage = p2pOpenToggle.targetGraphic as Image;

        // Checkmark 찾기 (토글의 graphic)
        checkmarkImage = p2pOpenToggle.graphic as Image;

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

        // Label 찾기 (자동)
        if (labelText == null)
        {
            Transform labelTransform = transform.Find("Label");
            if (labelTransform != null)
            {
                labelText = labelTransform.GetComponent<TMPro.TextMeshProUGUI>();
            }
        }
    }

    // Legacy Text 컴포넌트 참조 (TMPro가 없을 경우)
    private UnityEngine.UI.Text legacyLabelText;

    private void FindLegacyLabel()
    {
        if (labelText == null)
        {
            Transform labelTransform = transform.Find("Label");
            if (labelTransform != null)
            {
                legacyLabelText = labelTransform.GetComponent<UnityEngine.UI.Text>();
            }
        }
    }

    private void LoadVisibilityMode()
    {
        // 기본값: Public (0) - 전체공개
        int savedMode = PlayerPrefs.GetInt(VISIBILITY_MODE_KEY, 0);
        currentMode = (LocationVisibilityMode)savedMode;

        // 클릭 카운트 설정
        switch (currentMode)
        {
            case LocationVisibilityMode.Private:
                clickCount = 0;
                break;
            case LocationVisibilityMode.Public:
                clickCount = 1;
                break;
            case LocationVisibilityMode.FollowingOnly:
                clickCount = 2;
                break;
        }
    }

    private void SaveVisibilityMode()
    {
        PlayerPrefs.SetInt(VISIBILITY_MODE_KEY, (int)currentMode);
        PlayerPrefs.Save();
    }

    private void SetupToggle()
    {
        // 토글 값 변경 이벤트
        p2pOpenToggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnToggleValueChanged(bool isOn)
    {
        // 클릭할 때마다 상태 순환
        clickCount++;
        if (clickCount > 2) clickCount = 0;

        switch (clickCount)
        {
            case 0: // 체크 안 됨 - 비공개
                currentMode = LocationVisibilityMode.Private;
                p2pOpenToggle.SetIsOnWithoutNotify(false);
                break;

            case 1: // 체크됨 - 전체공개
                currentMode = LocationVisibilityMode.Public;
                p2pOpenToggle.SetIsOnWithoutNotify(true);
                break;

            case 2: // 체크됨 + 핑크색 - 팔로워만
                currentMode = LocationVisibilityMode.FollowingOnly;
                p2pOpenToggle.SetIsOnWithoutNotify(true);
                break;
        }

        SaveVisibilityMode();
        UpdateVisualState();
        ApplyVisibilityMode();

        Debug.Log($"[P2POpenFilterPanel] Location visibility mode changed to: {currentMode}");
    }

    private void UpdateVisualState()
    {
        switch (currentMode)
        {
            case LocationVisibilityMode.Private:
                // 체크 안 됨 상태 - 비공개
                if (p2pOpenToggle != null)
                    p2pOpenToggle.SetIsOnWithoutNotify(false);
                if (backgroundImage != null)
                    backgroundImage.color = normalColor;
                if (checkmarkImage != null)
                    checkmarkImage.color = checkmarkNormalColor;
                SetLabelText(privateLabel);
                break;

            case LocationVisibilityMode.Public:
                // 체크됨 - 기본 상태 (흰색 배경, 전체공개)
                if (p2pOpenToggle != null)
                    p2pOpenToggle.SetIsOnWithoutNotify(true);
                if (backgroundImage != null)
                    backgroundImage.color = normalColor;
                if (checkmarkImage != null)
                    checkmarkImage.color = checkmarkNormalColor;
                SetLabelText(publicLabel);
                break;

            case LocationVisibilityMode.FollowingOnly:
                // 체크됨 - 핑크색 (#e95383) - 팔로잉에게만
                if (p2pOpenToggle != null)
                    p2pOpenToggle.SetIsOnWithoutNotify(true);
                if (backgroundImage != null)
                    backgroundImage.color = followingOnlyColor;
                if (checkmarkImage != null)
                    checkmarkImage.color = checkmarkFollowingColor;
                SetLabelText(followingOnlyLabel);
                break;
        }
    }

    /// <summary>
    /// TMPro 또는 Legacy Text에 텍스트 설정
    /// </summary>
    private void SetLabelText(string text)
    {
        if (labelText != null)
        {
            labelText.text = text;
        }
        else if (legacyLabelText != null)
        {
            legacyLabelText.text = text;
        }
    }

    private void ApplyVisibilityMode()
    {
        if (P2PManager.Instance != null)
        {
            // 서버에 공개 설정 전송
            string visibilityString = currentMode.ToString().ToLower();
            P2PManager.Instance.SetLocationVisibility(currentMode);
        }
    }

    /// <summary>
    /// 현재 공개 모드 가져오기
    /// </summary>
    public LocationVisibilityMode GetCurrentVisibilityMode()
    {
        return currentMode;
    }

    /// <summary>
    /// 외부에서 공개 모드 설정
    /// </summary>
    public void SetVisibilityModeExternal(LocationVisibilityMode mode)
    {
        currentMode = mode;

        switch (mode)
        {
            case LocationVisibilityMode.Private:
                clickCount = 0;
                break;
            case LocationVisibilityMode.Public:
                clickCount = 1;
                break;
            case LocationVisibilityMode.FollowingOnly:
                clickCount = 2;
                break;
        }

        SaveVisibilityMode();
        UpdateVisualState();
        ApplyVisibilityMode();
    }

    /// <summary>
    /// 토글 참조 설정 (에디터에서 호출)
    /// </summary>
    public void SetToggle(Toggle toggle)
    {
        p2pOpenToggle = toggle;
    }
}
