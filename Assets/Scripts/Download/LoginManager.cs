using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System;

public class LoginManager : MonoBehaviour
{
    public static LoginManager Instance { get; private set; }

    public event Action<bool> OnLoginStateChanged;

    [Header("Login Prompt Popup")]
    public GameObject loginPromptPanel;
    public Text promptText;
    public Button webLoginButton;
    public Button cancelButton;

    [Header("Profile Display")]
    public Text profileUsernameText;
    public Transform uiCanvasRoot;

    [Header("Settings")]
    public float loginPromptDelay = 30f;

    // User State
    public UserData CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser != null && !IsGuest;
    public bool IsGuest { get; private set; }
    public int CurrentUserId => int.TryParse(CurrentUser?.id, out int id) ? id : 0;
    public string CurrentUsername => CurrentUser?.username;

    private string currentLang = "en";

#if UNITY_EDITOR
    [Header("=== 에디터 로그인 ===")]
    [SerializeField] private string editorUserId = "3";
    [SerializeField] private string editorUsername = "woopang";

    [ContextMenu("Editor Login")]
    public void EditorLogin()
    {
        if (string.IsNullOrEmpty(editorUserId) || string.IsNullOrEmpty(editorUsername))
        {
            Debug.LogError("[LoginManager] editorUserId 또는 editorUsername이 비어있습니다.");
            return;
        }

        CurrentUser = new UserData
        {
            id = editorUserId,
            username = editorUsername
        };
        IsGuest = false;

        PlayerPrefs.SetString("SavedUserId", editorUserId);
        PlayerPrefs.SetString("SavedUsername", editorUsername);
        PlayerPrefs.DeleteKey("IsGuest");
        PlayerPrefs.Save();

        // FCM 토큰에 user_id 연결
        RegisterFCMTokenWithUserId(editorUserId);

        OnLoginStateChanged?.Invoke(true);
        UpdateProfileUI();

    }

    [ContextMenu("Editor Logout")]
    public void EditorLogout()
    {
        Logout();
    }

    [Header("=== 딥링크 테스트 ===")]
    [SerializeField] private string testDeepLinkUrl = "woopang://auth?token=3&username=woopang&provider=email";

    [ContextMenu("Test Deep Link")]
    public void TestDeepLinkInEditor()
    {
        if (string.IsNullOrEmpty(testDeepLinkUrl))
        {
            Debug.LogError("[LoginManager] testDeepLinkUrl이 비어있습니다.");
            return;
        }
        OnDeepLinkActivated(testDeepLinkUrl);
    }

    public void SimulateDeepLink(string url)
    {
        OnDeepLinkActivated(url);
    }
#endif

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

        SetLanguage();

        if (webLoginButton != null)
            webLoginButton.onClick.AddListener(OpenWebLogin);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CloseLoginPrompt);

        TryAutoLogin();
    }

    void Start()
    {
        Application.deepLinkActivated += OnDeepLinkActivated;

        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            OnDeepLinkActivated(Application.absoluteURL);
        }
    }

    void OnDestroy()
    {
        Application.deepLinkActivated -= OnDeepLinkActivated;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && !IsLoggedIn)
        {
            // 웹 로그인 후 앱 복귀 시 딥링크가 누락된 경우 대비
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                OnDeepLinkActivated(Application.absoluteURL);
            }
            else
            {
                // 저장된 토큰이 있으면 자동 로그인 재시도
                TryAutoLogin();
            }
        }
    }

    #region Language

    private void SetLanguage()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                currentLang = "ko";
                break;
            case SystemLanguage.Japanese:
                currentLang = "ja";
                break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                currentLang = "zh";
                break;
            case SystemLanguage.Spanish:
                currentLang = "es";
                break;
            default:
                currentLang = "en";
                break;
        }
    }

    private string GetLocalizedText(string key)
    {
        switch (key)
        {
            case "login_required":
                switch (currentLang)
                {
                    case "ko": return "로그인이 필요한 기능입니다.\n로그인 하시겠습니까?";
                    case "ja": return "この機能にはログインが必要です。\nログインしますか？";
                    case "zh": return "此功能需要登录。\n你要登录吗？";
                    case "es": return "Se requiere inicio de sesión.\n¿Quieres iniciar sesión?";
                    default: return "Login is required for this feature.\nDo you want to log in?";
                }
            default:
                return key;
        }
    }

    #endregion

    #region Auto Login

    private void TryAutoLogin()
    {
        string savedUserId = PlayerPrefs.GetString("SavedUserId", "");
        string savedUsername = PlayerPrefs.GetString("SavedUsername", "");
        string savedProvider = PlayerPrefs.GetString("LoginProvider", "");

        if (!string.IsNullOrEmpty(savedUserId) && !string.IsNullOrEmpty(savedUsername))
        {
            CurrentUser = new UserData
            {
                id = savedUserId,
                username = savedUsername
            };
            IsGuest = false;

            OnLoginStateChanged?.Invoke(true);
            UpdateProfileUI();

            // 자동 로그인 시에도 FCM 토큰에 user_id 연결 (앱 재시작 시)
            StartCoroutine(DelayedTokenRegistration(savedUserId));
        }
        else
        {
            // 저장된 로그인 정보 없음
        }
    }

    private IEnumerator DelayedTokenRegistration(string userId)
    {
        // Firebase 초기화 대기
        yield return new WaitForSeconds(3f);
        RegisterFCMTokenWithUserId(userId);
    }

    #endregion

    #region Web Login

    public void OpenWebLogin()
    {
        string url = $"{ApiConfig.LOGIN_VIEW}?lang={currentLang}";
        Application.OpenURL(url);
        CloseLoginPrompt();
    }

    private void OnDeepLinkActivated(string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        if (url.StartsWith("woopang://auth"))
        {
            ParseAuthDeepLink(url);
        }
    }

    private void ParseAuthDeepLink(string url)
    {
        try
        {
            Uri uri = new Uri(url);
            string query = uri.Query.TrimStart('?');

            string token = "";
            string username = "";
            string provider = "";

            foreach (string param in query.Split('&'))
            {
                string[] kv = param.Split('=');
                if (kv.Length == 2)
                {
                    string key = kv[0];
                    string value = UnityWebRequest.UnEscapeURL(kv[1]);

                    switch (key)
                    {
                        case "token": token = value; break;
                        case "username": username = value; break;
                        case "provider": provider = value; break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(token))
            {
                HandleLoginSuccess(token, username, provider);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoginManager] Deep Link 파싱 오류: {e.Message}");
        }
    }

    private void HandleLoginSuccess(string token, string username, string provider)
    {
        StartCoroutine(VerifyTokenAndLogin(token));
    }

    private IEnumerator VerifyTokenAndLogin(string jwtToken)
    {
        string verifyUrl = $"{ApiConfig.MAIN_SERVER}/api/auth/verify";
        string jsonBody = $"{{\"token\":\"{jwtToken}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(verifyUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<AuthVerifyResponse>(request.downloadHandler.text);

                    if (response.success && response.data != null)
                    {
                        CurrentUser = new UserData
                        {
                            id = response.data.id.ToString(),
                            username = response.data.username,
                            email = response.data.email,
                            phone = response.data.phone,
                            avatar_url = response.data.avatar_url,
                            bio = response.data.bio,
                            instagram_id = response.data.instagram_id,
                            facebook_id = response.data.facebook_id,
                            x_id = response.data.x_id
                        };
                        IsGuest = false;

                        PlayerPrefs.SetString("AuthToken", jwtToken);
                        PlayerPrefs.SetString("SavedUserId", response.data.id.ToString());
                        PlayerPrefs.SetString("SavedUsername", response.data.username);
                        PlayerPrefs.SetString("LoginProvider", response.data.provider);
                        PlayerPrefs.DeleteKey("IsGuest");
                        PlayerPrefs.Save();

                        // FCM 토큰에 user_id 연결
                        RegisterFCMTokenWithUserId(response.data.id.ToString());

                        OnLoginStateChanged?.Invoke(true);
                        UpdateProfileUI();
                        CloseLoginPrompt();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LoginManager] 응답 파싱 오류: {e.Message}");
                }
            }
        }
    }

    #endregion

    #region Logout

    public void Logout()
    {
        // 로그아웃 전에 user_id 저장 (토큰 해제에 사용)
        string previousUserId = CurrentUser?.id;

        CurrentUser = null;
        IsGuest = false;

        PlayerPrefs.DeleteKey("AuthToken");
        PlayerPrefs.DeleteKey("SavedUserId");
        PlayerPrefs.DeleteKey("SavedUsername");
        PlayerPrefs.DeleteKey("LoginProvider");
        PlayerPrefs.DeleteKey("IsGuest");
        PlayerPrefs.Save();

        // FCM 토큰에서 user_id 제거 (푸시 알림 수신 중지)
        UnregisterFCMToken(previousUserId);

        OnLoginStateChanged?.Invoke(false);
        UpdateProfileUI();
    }

    #endregion

    #region FCM Token Management

    /// <summary>
    /// FCM 토큰에 user_id 연결 (로그인 시 호출)
    /// </summary>
    private void RegisterFCMTokenWithUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId) || userId == "0") return;

        var fcm = FirebaseNotification.GetInstance();
        if (fcm != null)
        {
            fcm.RegisterTokenWithUserId(userId);
        }
    }

    /// <summary>
    /// FCM 토큰에서 user_id 제거 (로그아웃 시 호출)
    /// </summary>
    private void UnregisterFCMToken(string userId = null)
    {
        var fcm = FirebaseNotification.GetInstance();
        if (fcm != null)
        {
            fcm.UnregisterUserFromToken(userId);
        }
    }

    #endregion

    #region Login Prompt Popup

    public void ShowLoginRequirementPopup()
    {
        if (loginPromptPanel != null)
        {
            if (promptText != null)
            {
                promptText.text = GetLocalizedText("login_required");
            }
            loginPromptPanel.SetActive(true);
        }
    }

    public void CloseLoginPrompt()
    {
        if (loginPromptPanel != null)
        {
            loginPromptPanel.SetActive(false);
        }
    }

    #endregion

    #region Profile

    private void UpdateProfileUI()
    {
        if (profileUsernameText != null && CurrentUser != null)
        {
            profileUsernameText.text = CurrentUser.username;
        }
    }

    public IEnumerator FetchProfileCoroutine(string userId)
    {
        // 안전 가드: 현재 로그인 유저의 프로필만 업데이트 허용
        if (CurrentUser == null || CurrentUser.id != userId)
        {
            yield break;
        }

        string url = $"{ApiConfig.USER_PROFILE}?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            string token = PlayerPrefs.GetString("AuthToken", "");
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", $"Bearer {token}");
            }

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    UserData profile = JsonUtility.FromJson<UserData>(request.downloadHandler.text);
                    if (profile != null)
                    {
                        // 이중 안전 가드: 서버 응답의 ID도 현재 유저와 일치하는지 확인
                        if (CurrentUser != null && CurrentUser.id == profile.id)
                        {
                            CurrentUser = profile;
                            UpdateProfileUI();
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LoginManager] 프로필 파싱 오류: {e.Message}");
                }
            }
        }
    }

    #endregion
}

[System.Serializable]
public class UserData
{
    public string id;
    public string username;
    public string email;
    public string phone;
    public string avatar_url;
    public string bio;
    public string instagram_id;
    public string facebook_id;
    public string x_id;
}

[System.Serializable]
public class AuthVerifyResponse
{
    public bool success;
    public string message;
    public AuthVerifyData data;
}

[System.Serializable]
public class AuthVerifyData
{
    public int id;
    public string username;
    public string email;
    public string phone;
    public string avatar_url;
    public string bio;
    public string instagram_id;
    public string facebook_id;
    public string x_id;
    public string provider;
}
