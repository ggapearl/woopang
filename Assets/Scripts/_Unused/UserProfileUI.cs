using UnityEngine;
using UnityEngine.UI;

public class UserProfileUI : MonoBehaviour
{
    [Header("UI Components")]
    public GameObject contentRoot; // To show/hide entire profile area
    public Text usernameText;
    public Button logoutButton;
    public Image avatarImage; // Optional placeholder

    void Start()
    {
        // Subscribe to login events
        if (LoginManager.Instance != null)
        {
            LoginManager.Instance.OnLoginStateChanged += HandleLoginStateChanged;
            // Initial state check
            HandleLoginStateChanged(LoginManager.Instance.IsLoggedIn);
        }
        
        if (logoutButton != null)
        {
            logoutButton.onClick.AddListener(OnLogoutClicked);
        }
    }

    void OnDestroy()
    {
        if (LoginManager.Instance != null)
        {
            LoginManager.Instance.OnLoginStateChanged -= HandleLoginStateChanged;
        }
    }

    private void HandleLoginStateChanged(bool isLoggedIn)
    {
        if (contentRoot != null) contentRoot.SetActive(isLoggedIn);

        if (isLoggedIn && LoginManager.Instance.CurrentUser != null)
        {
            if (usernameText != null)
            {
                usernameText.text = LoginManager.Instance.CurrentUser.username;
            }
        }
    }

    private void OnLogoutClicked()
    {
        if (LoginManager.Instance != null)
        {
            LoginManager.Instance.Logout();
        }
    }
}
