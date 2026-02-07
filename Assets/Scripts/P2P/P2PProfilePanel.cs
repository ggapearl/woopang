/**
 * P2PProfilePanel.cs
 * Displays user profile panel when double-tapping on P2P user avatars
 * Shows detailed user information, send gesture buttons, and action options
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-01
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class P2PProfilePanel : MonoBehaviour
{
    [Header("Panel References")]
    public CanvasGroup panelCanvasGroup;
    public GameObject panelContainer;

    [Header("User Info UI")]
    public Image avatarImage;
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI bioText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI lastSeenText;

    [Header("Action Buttons")]
    public Button waveButton;
    public Button pointButton;
    public Button thumbsUpButton;
    public Button heartButton;
    public Button celebrateButton;
    public Button closeButton;

    [Header("Additional Actions")]
    public Button addFriendButton;
    public Button blockUserButton;
    public Button reportUserButton;

    [Header("Animation Settings")]
    public float fadeDuration = 0.3f;
    public AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Audio")]
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip gestureSendSound;

    private AudioSource audioSource;
    private string currentUserId;
    private string currentUsername;
    private Coroutine fadeCoroutine;
    private bool isShowing = false;

    void Awake()
    {
        // Setup audio source
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Setup button listeners
        if (waveButton) waveButton.onClick.AddListener(() => SendGesture("wave"));
        if (pointButton) pointButton.onClick.AddListener(() => SendGesture("point"));
        if (thumbsUpButton) thumbsUpButton.onClick.AddListener(() => SendGesture("thumbsup"));
        if (heartButton) heartButton.onClick.AddListener(() => SendGesture("heart"));
        if (celebrateButton) celebrateButton.onClick.AddListener(() => SendGesture("celebrate"));
        if (closeButton) closeButton.onClick.AddListener(HidePanel);

        // Additional action buttons
        if (addFriendButton) addFriendButton.onClick.AddListener(OnAddFriend);
        if (blockUserButton) blockUserButton.onClick.AddListener(OnBlockUser);
        if (reportUserButton) reportUserButton.onClick.AddListener(OnReportUser);

        // Start hidden
        if (panelCanvasGroup)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
        }
        if (panelContainer) panelContainer.SetActive(false);
    }

    /// <summary>
    /// Show profile panel with user information
    /// Called by DoubleTap3D when user double-taps on P2P avatar
    /// </summary>
    public void ShowProfile(string userId, string username, string avatarUrl, string bio, float distance)
    {
        currentUserId = userId;
        currentUsername = username;

        // Update UI
        if (usernameText) usernameText.text = username;
        if (bioText) bioText.text = string.IsNullOrEmpty(bio) ? "No bio available" : bio;

        // Format distance
        if (distanceText)
        {
            if (distance < 1000f)
            {
                distanceText.text = $"{Mathf.RoundToInt(distance)}m away";
            }
            else
            {
                distanceText.text = $"{(distance / 1000f):F1}km away";
            }
        }

        if (lastSeenText) lastSeenText.text = "Now";

        // Load avatar image
        if (!string.IsNullOrEmpty(avatarUrl) && avatarImage != null)
        {
            StartCoroutine(LoadAvatarImage(avatarUrl));
        }

        // Show panel with animation
        ShowPanel();

        // Play sound
        if (openSound && audioSource) audioSource.PlayOneShot(openSound);
    }

    /// <summary>
    /// Show panel with fade animation
    /// </summary>
    private void ShowPanel()
    {
        if (isShowing) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        if (panelContainer) panelContainer.SetActive(true);
        fadeCoroutine = StartCoroutine(FadePanel(true));
        isShowing = true;
    }

    /// <summary>
    /// Hide panel with fade animation
    /// </summary>
    public void HidePanel()
    {
        if (!isShowing) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadePanel(false));
        isShowing = false;

        // Play sound
        if (closeSound && audioSource) audioSource.PlayOneShot(closeSound);
    }

    /// <summary>
    /// Fade panel in/out
    /// </summary>
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

        if (!fadeIn && panelContainer)
        {
            panelContainer.SetActive(false);
        }

        fadeCoroutine = null;
    }

    /// <summary>
    /// Send gesture to the currently viewed user
    /// </summary>
    private void SendGesture(string gestureType)
    {
        if (string.IsNullOrEmpty(currentUserId))
            return;

        // Call P2PManager to send gesture
        P2PManager p2pManager = P2PManager.Instance;
        if (p2pManager != null)
        {
            p2pManager.SendGesture(currentUserId, gestureType);

            // Visual feedback
            ShowGestureSentFeedback(gestureType);

            // Play sound
            if (gestureSendSound && audioSource) audioSource.PlayOneShot(gestureSendSound);
        }
        else
        {
            Debug.LogError("[P2PProfilePanel] P2PManager instance not found!");
        }
    }

    /// <summary>
    /// Show visual feedback when gesture is sent
    /// </summary>
    private void ShowGestureSentFeedback(string gestureType)
    {
        // TODO: Implement visual feedback (e.g., button animation, toast message)
        // Simple example: Flash the button
        // You can enhance this with better animations
    }

    /// <summary>
    /// Load avatar image from URL
    /// </summary>
    private IEnumerator LoadAvatarImage(string url)
    {
        if (avatarImage == null) yield break;

        using (UnityEngine.Networking.UnityWebRequest www =
            UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(www);
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
                avatarImage.sprite = sprite;
            }
        }
    }

    /// <summary>
    /// Add user as friend
    /// </summary>
    private void OnAddFriend()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;

        // TODO: Implement friend system API call
        // Example:
        // P2PManager.Instance.AddFriend(currentUserId, (success) => {
        //     if (success) {
        //         ShowMessage("Friend request sent!");
        //     }
        // });

        ShowTemporaryMessage("Friend request sent!");
    }

    /// <summary>
    /// Block user
    /// </summary>
    private void OnBlockUser()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;

        // TODO: Implement block user API call
        // P2PManager.Instance.BlockUser(currentUserId);

        ShowTemporaryMessage($"Blocked {currentUsername}");
        HidePanel();
    }

    /// <summary>
    /// Report user
    /// </summary>
    private void OnReportUser()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;

        // TODO: Open report dialog with reason selection
        // For now, just show confirmation

        ShowTemporaryMessage("Report submitted");
    }

    /// <summary>
    /// Show temporary message (toast-like notification)
    /// </summary>
    private void ShowTemporaryMessage(string message)
    {
        // TODO: Implement proper toast notification system
        // Simple example using bioText temporarily
        if (bioText)
        {
            string originalBio = bioText.text;
            bioText.text = message;
            bioText.color = Color.green;

            StartCoroutine(ResetMessageAfterDelay(originalBio, 2f));
        }
    }

    private IEnumerator ResetMessageAfterDelay(string originalText, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bioText)
        {
            bioText.text = originalText;
            bioText.color = Color.white;
        }
    }

    /// <summary>
    /// Check if panel is currently showing
    /// </summary>
    public bool IsShowing()
    {
        return isShowing;
    }

    /// <summary>
    /// Get currently viewed user ID
    /// </summary>
    public string GetCurrentUserId()
    {
        return currentUserId;
    }

    void Update()
    {
        // Allow closing with back button or ESC key
        if (isShowing && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)))
        {
            HidePanel();
        }
    }
}
