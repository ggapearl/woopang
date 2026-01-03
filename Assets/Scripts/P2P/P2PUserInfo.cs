/**
 * P2PUserInfo.cs
 * Displays user information on P2P user avatars
 * Shows username, distance, avatar image, and status
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-01
 */

using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class P2PUserInfo : MonoBehaviour
{
    [Header("User Data")]
    public string userId;
    public string username;
    public string avatarUrl;
    public string bio;
    public float distance; // Distance in meters

    [Header("UI References")]
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI distanceText;
    public Image avatarImage;
    public GameObject statusIndicator; // Green dot for online status
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public float maxVisibleDistance = 500f; // Fade out after this distance
    public bool alwaysShowUsername = true;
    public bool showDistance = true;

    [Header("Billboard Settings")]
    public bool enableBillboard = true; // Always face camera
    public Vector3 billboardOffset = new Vector3(0, 2.5f, 0); // Above avatar

    private Camera mainCamera;
    private Transform billboardTransform;
    private bool isInitialized = false;

    void Awake()
    {
        mainCamera = Camera.main;

        // Create billboard parent for UI elements
        GameObject billboardObj = new GameObject("Billboard");
        billboardObj.transform.SetParent(transform);
        billboardObj.transform.localPosition = billboardOffset;
        billboardTransform = billboardObj.transform;

        // Move UI elements to billboard
        if (usernameText) usernameText.transform.SetParent(billboardTransform, false);
        if (distanceText) distanceText.transform.SetParent(billboardTransform, false);
        if (avatarImage) avatarImage.transform.SetParent(billboardTransform, false);
    }

    void Update()
    {
        if (!isInitialized) return;

        // Billboard effect - always face camera
        if (enableBillboard && mainCamera != null && billboardTransform != null)
        {
            billboardTransform.rotation = Quaternion.LookRotation(
                billboardTransform.position - mainCamera.transform.position
            );
        }

        // Update distance-based alpha
        UpdateDistanceBasedAlpha();
    }

    /// <summary>
    /// Initialize user information display
    /// </summary>
    public void Initialize(string uid, string uname, string avatar, string userBio, float dist)
    {
        userId = uid;
        username = uname;
        avatarUrl = avatar;
        bio = userBio;
        distance = dist;

        UpdateUI();
        isInitialized = true;

        // Load avatar image if URL provided
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            StartCoroutine(LoadAvatarImage(avatarUrl));
        }
    }

    /// <summary>
    /// Update distance value (called by P2PManager during position updates)
    /// </summary>
    public void UpdateDistance(float newDistance)
    {
        distance = newDistance;
        UpdateDistanceText();
        UpdateDistanceBasedAlpha();
    }

    /// <summary>
    /// Update all UI elements
    /// </summary>
    private void UpdateUI()
    {
        // Update username
        if (usernameText != null)
        {
            usernameText.text = username;
            usernameText.gameObject.SetActive(alwaysShowUsername);
        }

        // Update distance
        UpdateDistanceText();

        // Show online status indicator
        if (statusIndicator != null)
        {
            statusIndicator.SetActive(true);
        }
    }

    /// <summary>
    /// Update distance text with formatted string
    /// </summary>
    private void UpdateDistanceText()
    {
        if (distanceText != null && showDistance)
        {
            if (distance < 1000f)
            {
                distanceText.text = $"{Mathf.RoundToInt(distance)}m";
            }
            else
            {
                distanceText.text = $"{(distance / 1000f):F1}km";
            }
            distanceText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Fade UI based on distance (distant users become more transparent)
    /// </summary>
    private void UpdateDistanceBasedAlpha()
    {
        if (canvasGroup == null) return;

        float alpha = 1f;

        // Start fading at 70% of max distance
        float fadeStartDistance = maxVisibleDistance * 0.7f;

        if (distance > fadeStartDistance)
        {
            float fadeRange = maxVisibleDistance - fadeStartDistance;
            float fadeProgress = (distance - fadeStartDistance) / fadeRange;
            alpha = Mathf.Lerp(1f, 0.3f, fadeProgress); // Fade to 30% opacity
        }

        canvasGroup.alpha = alpha;
    }

    /// <summary>
    /// Load avatar image from URL (supports PNG/JPG)
    /// </summary>
    private System.Collections.IEnumerator LoadAvatarImage(string url)
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
            else
            {
                Debug.LogWarning($"[P2PUserInfo] Failed to load avatar image: {url}");
                // Use default avatar sprite if available
                // avatarImage.sprite = defaultAvatarSprite;
            }
        }
    }

    /// <summary>
    /// Show/hide user information
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Highlight user (e.g., when selected or hovered)
    /// </summary>
    public void SetHighlight(bool highlighted)
    {
        if (usernameText != null)
        {
            usernameText.color = highlighted ? Color.yellow : Color.white;
            usernameText.fontStyle = highlighted ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    /// <summary>
    /// Get user info for profile display
    /// </summary>
    public (string userId, string username, string bio, float distance) GetUserInfo()
    {
        return (userId, username, bio, distance);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Draw distance sphere in editor
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, maxVisibleDistance);
    }
#endif
}
