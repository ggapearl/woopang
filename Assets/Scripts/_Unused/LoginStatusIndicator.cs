using UnityEngine;
using UnityEngine.UI;

public class LoginStatusIndicator : MonoBehaviour {
    private Image indicatorImage;
    public Color loggedInColor = Color.green;
    public Color loggedOutColor = Color.red;

    void Awake() {
        indicatorImage = GetComponent<Image>();
    }

    void Start() {
        if (LoginManager.Instance != null) {
            LoginManager.Instance.OnLoginStateChanged += UpdateStatus;
            UpdateStatus(LoginManager.Instance.IsLoggedIn);
        }
    }

    private void UpdateStatus(bool isLoggedIn) {
        if (indicatorImage != null) {
            indicatorImage.color = isLoggedIn ? loggedInColor : loggedOutColor;
        }
    }

    void OnDestroy() {
        if (LoginManager.Instance != null) {
            LoginManager.Instance.OnLoginStateChanged -= UpdateStatus;
        }
    }
}
