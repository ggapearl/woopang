/**
 * P2PUserProfile.cs
 * P2P 사용자 프로필 팝업 (FullscreenGuide와 다른 레이아웃)
 * - 사용자 정보 표시
 * - 팔로우/언팔로우
 * - 말풍선 달기
 * - 차단/신고
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-01
 */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class P2PUserProfile : MonoBehaviour
{
    public static P2PUserProfile Instance { get; private set; }

    [Header("Panel References")]
    public CanvasGroup panelCanvasGroup;
    public GameObject panelContainer;
    public Button closeButton;

    [Header("Profile Header")]
    public Image avatarImage;
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI distanceText;

    [Header("Bio Section")]
    public TextMeshProUGUI bioText;
    public ScrollRect bioScrollRect;

    [Header("Action Buttons")]
    public Button followButton;
    public TextMeshProUGUI followButtonText;
    public Button messageButton;
    public Button speechBubbleButton;

    [Header("Bottom Actions")]
    public Button blockButton;
    public Button reportButton;

    [Header("Animation Settings")]
    public float fadeDuration = 0.3f;
    public AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Audio")]
    public AudioClip openSound;
    public AudioClip closeSound;

    private AudioSource audioSource;
    private string currentUserId;
    private string currentUsername;
    private bool isFollowing = false;
    private Coroutine fadeCoroutine;

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

        // Setup audio source
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void Start()
    {
        SetupButtonListeners();

        // 시작 시 숨김
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
        }
        if (panelContainer != null)
            panelContainer.SetActive(false);
    }

    private void SetupButtonListeners()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(HideProfile);

        if (followButton != null)
            followButton.onClick.AddListener(OnFollowButtonClicked);

        if (messageButton != null)
            messageButton.onClick.AddListener(OnMessageButtonClicked);

        if (speechBubbleButton != null)
            speechBubbleButton.onClick.AddListener(OnSpeechBubbleButtonClicked);

        if (blockButton != null)
            blockButton.onClick.AddListener(OnBlockButtonClicked);

        if (reportButton != null)
            reportButton.onClick.AddListener(OnReportButtonClicked);
    }

    /// <summary>
    /// P2PUserInfo 컴포넌트로부터 프로필 표시
    /// </summary>
    public void ShowProfile(P2PUserInfo userInfo)
    {
        if (userInfo == null) return;

        var (userId, username, bio, distance) = userInfo.GetUserInfo();
        ShowProfile(userId, username, userInfo.avatarUrl, bio, distance);
    }

    /// <summary>
    /// 프로필 표시 (직접 데이터 전달)
    /// </summary>
    public void ShowProfile(string userId, string username, string avatarUrl, string bio, float distance)
    {
        currentUserId = userId;
        currentUsername = username;

        // UI 업데이트
        if (usernameText != null)
            usernameText.text = username;

        if (distanceText != null)
        {
            if (distance < 1000f)
                distanceText.text = $"{Mathf.RoundToInt(distance)}m away";
            else
                distanceText.text = $"{(distance / 1000f):F1}km away";
        }

        if (bioText != null)
            bioText.text = string.IsNullOrEmpty(bio) ? "소개가 없습니다." : bio;

        // 아바타 이미지 로드 - 중앙 캐시 시스템 사용
        if (avatarImage != null)
        {
            ProfileManager.LoadAvatarAsync(currentUserId, avatarUrl, avatarImage, username);
        }

        // 팔로우 상태 확인
        CheckFollowStatus();

        // 패널 표시
        ShowPanel();

        // 사운드 재생
        if (openSound != null && audioSource != null)
            audioSource.PlayOneShot(openSound);
    }

    private void ShowPanel()
    {
        if (panelContainer != null)
            panelContainer.SetActive(true);

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadePanel(true));
    }

    /// <summary>
    /// 프로필 숨기기
    /// </summary>
    public void HideProfile()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadePanel(false));

        if (closeSound != null && audioSource != null)
            audioSource.PlayOneShot(closeSound);
    }

    private IEnumerator FadePanel(bool fadeIn)
    {
        if (panelCanvasGroup == null) yield break;

        float elapsed = 0f;
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        AnimationCurve curve = fadeIn ? fadeInCurve : fadeOutCurve;

        panelCanvasGroup.blocksRaycasts = fadeIn;
        panelCanvasGroup.interactable = fadeIn;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, curve.Evaluate(t));
            yield return null;
        }

        panelCanvasGroup.alpha = endAlpha;

        if (!fadeIn && panelContainer != null)
        {
            panelContainer.SetActive(false);
        }

        fadeCoroutine = null;
    }

    /// <summary>
    /// 아바타 이미지 로드
    /// </summary>
    private IEnumerator LoadAvatarImage(string url)
    {
        if (avatarImage == null) yield break;

        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
                avatarImage.sprite = sprite;
            }
            else
            {
                }
        }
    }

    /// <summary>
    /// 팔로우 상태 확인
    /// </summary>
    private void CheckFollowStatus()
    {
        // TODO: 서버에서 팔로우 상태 확인
        // 임시로 false 설정
        isFollowing = false;
        UpdateFollowButtonText();
    }

    private void UpdateFollowButtonText()
    {
        if (followButtonText != null)
        {
            followButtonText.text = isFollowing ? "언팔로우" : "팔로우";
        }
    }

    /// <summary>
    /// 팔로우/언팔로우 버튼 클릭
    /// </summary>
    private void OnFollowButtonClicked()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;

        string action = isFollowing ? "unfollow" : "follow";
        StartCoroutine(SendFollowRequest(action));
    }

    private IEnumerator SendFollowRequest(string action)
    {
        if (P2PManager.Instance == null) yield break;

        // TODO: 서버 API 호출

        // 임시로 상태 토글
        isFollowing = !isFollowing;
        UpdateFollowButtonText();

        yield return null;
    }

    /// <summary>
    /// 메시지 버튼 클릭 (향후 구현)
    /// </summary>
    private void OnMessageButtonClicked()
    {
        // TODO: 메시지 시스템 구현
    }

    /// <summary>
    /// 말풍선 달기 버튼 클릭
    /// </summary>
    private void OnSpeechBubbleButtonClicked()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;

        // SpeechBubbleManager를 통해 말풍선 생성 UI 표시
        if (SpeechBubbleManager.Instance != null)
        {
            SpeechBubbleManager.Instance.ShowCreateBubbleUI("user", currentUserId);
        }

    }

    /// <summary>
    /// 차단 버튼 클릭
    /// </summary>
    private void OnBlockButtonClicked()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;

        // 확인 다이얼로그 표시 (향후 구현)

        // TODO: 서버 API 호출
        // StartCoroutine(SendBlockRequest());

        HideProfile();
    }

    /// <summary>
    /// 신고 버튼 클릭
    /// </summary>
    private void OnReportButtonClicked()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;

        // 신고 사유 선택 UI 표시 (향후 구현)

        // TODO: 신고 UI 표시
    }

    void Update()
    {
        // ESC 키로 닫기
        if (Input.GetKeyDown(KeyCode.Escape) && panelContainer != null && panelContainer.activeSelf)
        {
            HideProfile();
        }
    }
}
