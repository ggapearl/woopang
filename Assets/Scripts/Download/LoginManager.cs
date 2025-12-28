using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class LoginManager : MonoBehaviour
{
    public static LoginManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject authPanel;
    public InputField emailInput;
    public InputField passwordInput;
    public InputField usernameInput; // Register only

    [Header("Animation")]
    public CanvasGroup authPanelCanvasGroup;
    public float fadeInDelay = 30f; // 30초 딜레이
    public float fadeInDuration = 0.5f; // 페이드 인 시간
    public float fadeOutDuration = 0.5f; // 페이드 아웃 시간

    [Header("Loading")]
    public GameObject loadingSpinner; // 로딩 스피너
    private bool isProcessing = false;
    
    public Button loginButton;
    public Button registerButton;
    public Button guestLoginButton;
    public Button googleLoginButton;
    public Button appleLoginButton;
    public Button kakaoLoginButton;
    public Button findIdButton;
    public Button resetPwButton;
    public Button toggleModeButton; 
    
    public Text messageText;
    public Text toggleButtonText;
    
    // Localization Text References
    public Text titleText;
    public Text emailPlaceholder;
    public Text passwordPlaceholder;
    public Text usernamePlaceholder;
    public Text loginButtonText;
    public Text registerButtonText;
    public Text guestButtonText;
    public Text findIdButtonText;
    public Text resetPwButtonText;
    public Text googleLoginText;
    public Text appleLoginText;
    public Text kakaoLoginText;

    [Header("Popup References") ]
    public GameObject loginPromptPanel;
    public Text promptText;
    public Button promptYesButton;
    public Button promptNoButton;

    private bool isLoginMode = true;
    private const string BASE_URL = "https://woopang.com";

    public UserData CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser != null && !IsGuest;
    public bool IsGuest { get; private set; }

    private Dictionary<string, Dictionary<string, string>> authTranslations = new Dictionary<string, Dictionary<string, string>>
    {
        { "en", new Dictionary<string, string> {
            { "email", "Email" }, { "password", "Password" }, { "username", "Username" },
            { "login", "Log In" }, { "register", "Sign Up" }, { "guest", "Continue as Guest" },
            { "findId", "Find ID" }, { "resetPw", "Reset PW" },
            { "toggleToReg", "Create Account" }, { "toggleToLog", "Back to Login" },
            { "google", "Sign in with Google" }, { "apple", "Sign in with Apple" }, { "kakao", "Login with Kakao" },
            { "reqLogin", "Login is required for this feature.\nDo you want to log in?" }
        }},
        { "ko", new Dictionary<string, string> {
            { "email", "이메일" }, { "password", "비밀번호" }, { "username", "닉네임" },
            { "login", "로그인" }, { "register", "회원가입 완료" }, { "guest", "로그인 없이 이용하기" },
            { "findId", "아이디 찾기" }, { "resetPw", "비번 재설정" },
            { "toggleToReg", "회원가입하기" }, { "toggleToLog", "로그인하기" },
            { "google", "구글 로그인" }, { "apple", "Apple 로그인" }, { "kakao", "카카오 로그인" },
            { "reqLogin", "로그인이 필요한 기능입니다.\n로그인 하시겠습니까?" }
        }},
        { "ja", new Dictionary<string, string> {
            { "email", "メール" }, { "password", "パスワード" }, { "username", "ニックネーム" },
            { "login", "ログイン" }, { "register", "登録完了" }, { "guest", "ゲストとして利用" },
            { "findId", "ID検索" }, { "resetPw", "PWリセット" },
            { "toggleToReg", "新規登録" }, { "toggleToLog", "ログインへ" },
            { "google", "Googleでログイン" }, { "apple", "Appleでログイン" }, { "kakao", "Kakaoログイン" },
            { "reqLogin", "この機能にはログインが必要です。\nログインしますか？" }
        }},
        { "zh", new Dictionary<string, string> {
            { "email", "邮箱" }, { "password", "密码" }, { "username", "昵称" },
            { "login", "登录" }, { "register", "注册" }, { "guest", "游客访问" },
            { "findId", "找回ID" }, { "resetPw", "重置密码" },
            { "toggleToReg", "去注册" }, { "toggleToLog", "去登录" },
            { "google", "Google登录" }, { "apple", "Apple登录" }, { "kakao", "Kakao登录" },
            { "reqLogin", "此功能需要登录。\n你要登录吗？" }
        }},
        { "es", new Dictionary<string, string> {
            { "email", "Correo" }, { "password", "Contraseña" }, { "username", "Nombre de usuario" },
            { "login", "Iniciar sesión" }, { "register", "Registrarse" }, { "guest", "Entrar como invitado" },
            { "findId", "Buscar ID" }, { "resetPw", "Restablecer PW" },
            { "toggleToReg", "Crear cuenta" }, { "toggleToLog", "Volver al inicio" },
            { "google", "Entrar con Google" }, { "apple", "Entrar con Apple" }, { "kakao", "Entrar con Kakao" },
            { "reqLogin", "Se requiere inicio de sesión.\n¿Quieres iniciar sesión?" }
        }}
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Popup Listeners
        if (promptYesButton != null) promptYesButton.onClick.AddListener(OnPromptYes);
        if (promptNoButton != null) promptNoButton.onClick.AddListener(OnPromptNo);

        // Language specific UI
        UpdateAuthUILanguage();

        if (kakaoLoginButton != null)
        {
            kakaoLoginButton.gameObject.SetActive(Application.systemLanguage == SystemLanguage.Korean);
        }

        // Try auto-login if saved
        if (PlayerPrefs.GetInt("IsGuest", 0) == 1)
        {
            GuestLogin();
        }
        else
        {
            string savedId = PlayerPrefs.GetString("SavedUserId", "");
            string savedName = PlayerPrefs.GetString("SavedUsername", "");
            
            if (!string.IsNullOrEmpty(savedId))
            {
                CurrentUser = new UserData { id = savedId, username = savedName };
                if (authPanel != null) authPanel.SetActive(false);
            }
            else
            {
                if (authPanel != null)
                {
                    authPanel.SetActive(false); // 처음에는 비활성화
                    StartCoroutine(ShowAuthPanelWithDelay());
                }
            }
        }

        if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
        if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);
        if (guestLoginButton != null) guestLoginButton.onClick.AddListener(OnGuestLoginClicked);
        if (toggleModeButton != null) toggleModeButton.onClick.AddListener(ToggleAuthMode);
        
        if (googleLoginButton != null) googleLoginButton.onClick.AddListener(() => OnSocialLoginClick("google"));
        if (appleLoginButton != null) appleLoginButton.onClick.AddListener(() => OnSocialLoginClick("apple"));
        if (kakaoLoginButton != null) kakaoLoginButton.onClick.AddListener(() => OnSocialLoginClick("kakao"));
        
        if (findIdButton != null) findIdButton.onClick.AddListener(OnFindIdClick);
        if (resetPwButton != null) resetPwButton.onClick.AddListener(OnResetPwClick);

        UpdateUIState();
    }

    /// <summary>
    /// 10초 딜레이 후 페이드인 효과로 AuthPanel 표시
    /// </summary>
    private IEnumerator ShowAuthPanelWithDelay()
    {
        // 10초 대기
        yield return new WaitForSeconds(fadeInDelay);

        // AuthPanel 활성화
        if (authPanel != null)
        {
            authPanel.SetActive(true);

            // CanvasGroup이 있으면 페이드인 효과
            if (authPanelCanvasGroup != null)
            {
                yield return StartCoroutine(FadeIn(authPanelCanvasGroup, fadeInDuration));
            }
        }
    }

    /// <summary>
    /// 페이드인 애니메이션
    /// </summary>
    private IEnumerator FadeIn(CanvasGroup canvasGroup, float duration)
    {
        float elapsed = 0f;
        canvasGroup.alpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// 페이드아웃 애니메이션
    /// </summary>
    private IEnumerator FadeOut(CanvasGroup canvasGroup, float duration)
    {
        float elapsed = 0f;
        canvasGroup.alpha = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    public void ShowLoginRequirementPopup()
    {
        if (loginPromptPanel != null)
        {
            string lang = "en";
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Korean: lang = "ko"; break;
                case SystemLanguage.Japanese: lang = "ja"; break;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: lang = "zh"; break;
                case SystemLanguage.Spanish: lang = "es"; break;
            }
            if (promptText != null) promptText.text = authTranslations[lang]["reqLogin"];
            loginPromptPanel.SetActive(true);
        }
    }

    private void OnPromptYes()
    {
        if (loginPromptPanel != null) loginPromptPanel.SetActive(false);
        if (authPanel != null) 
        {
            authPanel.SetActive(true);
            isLoginMode = true;
            UpdateUIState();
        }
    }

    private void OnPromptNo()
    {
        if (loginPromptPanel != null) loginPromptPanel.SetActive(false);
    }

    private void UpdateAuthUILanguage()
    {
        string lang = "en";
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: lang = "ko"; break;
            case SystemLanguage.Japanese: lang = "ja"; break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: lang = "zh"; break;
            case SystemLanguage.Spanish: lang = "es"; break;
        }

        var texts = authTranslations[lang];

        if (emailPlaceholder != null) emailPlaceholder.text = texts["email"];
        if (passwordPlaceholder != null) passwordPlaceholder.text = texts["password"];
        if (usernamePlaceholder != null) usernamePlaceholder.text = texts["username"];
        if (loginButtonText != null) loginButtonText.text = texts["login"];
        if (registerButtonText != null) registerButtonText.text = texts["register"];
        if (guestButtonText != null) guestButtonText.text = texts["guest"];
        if (findIdButtonText != null) findIdButtonText.text = texts["findId"];
        if (resetPwButtonText != null) resetPwButtonText.text = texts["resetPw"];
        if (googleLoginText != null) googleLoginText.text = texts["google"];
        if (appleLoginText != null) appleLoginText.text = texts["apple"];
        if (kakaoLoginText != null) kakaoLoginText.text = texts["kakao"];
    }

    private void UpdateUIState()
    {
        if (usernameInput != null) usernameInput.gameObject.SetActive(!isLoginMode);
        if (loginButton != null) loginButton.gameObject.SetActive(isLoginMode);
        if (registerButton != null) registerButton.gameObject.SetActive(!isLoginMode);
        
        if (toggleButtonText != null)
        {
            string lang = "en";
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Korean: lang = "ko"; break;
                case SystemLanguage.Japanese: lang = "ja"; break;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: lang = "zh"; break;
                case SystemLanguage.Spanish: lang = "es"; break;
            }
            toggleButtonText.text = isLoginMode ? authTranslations[lang]["toggleToReg"] : authTranslations[lang]["toggleToLog"];
        }
        
        if (messageText != null) messageText.text = "";
    }

    private void ToggleAuthMode()
    {
        isLoginMode = !isLoginMode;
        UpdateUIState();
    }

    private void OnGuestLoginClicked()
    {
        GuestLogin();
    }

    private void GuestLogin()
    {
        string deviceId = SystemInfo.deviceUniqueIdentifier;
        CurrentUser = new UserData
        {
            id = deviceId,
            username = "Guest_" + deviceId.Substring(0, 4)
        };
        IsGuest = true;

        PlayerPrefs.SetInt("IsGuest", 1);
        PlayerPrefs.Save();

        StartCoroutine(CloseAuthPanelWithFadeOut());
        Debug.Log("Logged in as Guest");
    }

    private IEnumerator CloseAuthPanelWithFadeOut()
    {
        if (authPanelCanvasGroup != null)
        {
            yield return StartCoroutine(FadeOut(authPanelCanvasGroup, fadeOutDuration));
        }

        if (authPanel != null) authPanel.SetActive(false);
    }

    private void OnLoginClicked()
    {
        if (isProcessing) return;

        // 입력 검증
        if (string.IsNullOrEmpty(emailInput.text))
        {
            ShowMessage("이메일을 입력해주세요.");
            return;
        }

        if (string.IsNullOrEmpty(passwordInput.text))
        {
            ShowMessage("비밀번호를 입력해주세요.");
            return;
        }

        if (!IsValidEmail(emailInput.text))
        {
            ShowMessage("올바른 이메일 형식이 아닙니다.");
            return;
        }

        StartCoroutine(LoginCoroutine(emailInput.text, passwordInput.text));
    }

    private void OnRegisterClicked()
    {
        if (isProcessing) return;

        // 입력 검증
        if (string.IsNullOrEmpty(emailInput.text))
        {
            ShowMessage("이메일을 입력해주세요.");
            return;
        }

        if (string.IsNullOrEmpty(passwordInput.text))
        {
            ShowMessage("비밀번호를 입력해주세요.");
            return;
        }

        if (string.IsNullOrEmpty(usernameInput.text))
        {
            ShowMessage("닉네임을 입력해주세요.");
            return;
        }

        if (!IsValidEmail(emailInput.text))
        {
            ShowMessage("올바른 이메일 형식이 아닙니다.");
            return;
        }

        if (passwordInput.text.Length < 6)
        {
            ShowMessage("비밀번호는 최소 6자 이상이어야 합니다.");
            return;
        }

        StartCoroutine(RegisterCoroutine(emailInput.text, passwordInput.text, usernameInput.text));
    }

    /// <summary>
    /// 이메일 형식 검증
    /// </summary>
    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 메시지 표시 헬퍼
    /// </summary>
    private void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
        Debug.LogWarning($"[LoginManager] {message}");
    }

    /// <summary>
    /// 버튼 상태 제어
    /// </summary>
    private void SetButtonsInteractable(bool interactable)
    {
        if (loginButton != null) loginButton.interactable = interactable;
        if (registerButton != null) registerButton.interactable = interactable;
        if (guestLoginButton != null) guestLoginButton.interactable = interactable;
        if (googleLoginButton != null) googleLoginButton.interactable = interactable;
        if (appleLoginButton != null) appleLoginButton.interactable = interactable;
        if (kakaoLoginButton != null) kakaoLoginButton.interactable = interactable;
        if (toggleModeButton != null) toggleModeButton.interactable = interactable;
    }

    private IEnumerator LoginCoroutine(string email, string password)
    {
        isProcessing = true;
        SetButtonsInteractable(false);
        if (loadingSpinner != null) loadingSpinner.SetActive(true);

        string url = $"{BASE_URL}/login";
        var data = new LoginData { email = email, password = password };
        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (loadingSpinner != null) loadingSpinner.SetActive(false);
            isProcessing = false;
            SetButtonsInteractable(true);

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                CurrentUser = new UserData { id = response.user_id, username = response.username };
                IsGuest = false;

                PlayerPrefs.SetString("SavedUserId", response.user_id);
                PlayerPrefs.SetString("SavedUsername", response.username);
                PlayerPrefs.DeleteKey("IsGuest");

                // 페이드아웃 후 닫기
                if (authPanelCanvasGroup != null)
                {
                    yield return StartCoroutine(FadeOut(authPanelCanvasGroup, fadeInDuration));
                }

                if (authPanel != null) authPanel.SetActive(false);
                Debug.Log($"Login Success: {CurrentUser.username}");
            }
            else
            {
                ShowMessage("로그인 실패: 이메일 또는 비밀번호를 확인하세요.");
                Debug.LogError($"Login failed: {request.error}");
            }
        }
    }

    private IEnumerator RegisterCoroutine(string email, string password, string username)
    {
        isProcessing = true;
        SetButtonsInteractable(false);
        if (loadingSpinner != null) loadingSpinner.SetActive(true);

        string url = $"{BASE_URL}/register";
        var data = new RegisterData { email = email, password = password, username = username };
        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (loadingSpinner != null) loadingSpinner.SetActive(false);
            isProcessing = false;
            SetButtonsInteractable(true);

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                CurrentUser = new UserData { id = response.user_id, username = response.username };
                IsGuest = false;

                PlayerPrefs.SetString("SavedUserId", response.user_id);
                PlayerPrefs.SetString("SavedUsername", response.username);
                PlayerPrefs.DeleteKey("IsGuest");

                // 페이드아웃 후 닫기
                if (authPanelCanvasGroup != null)
                {
                    yield return StartCoroutine(FadeOut(authPanelCanvasGroup, fadeInDuration));
                }

                if (authPanel != null) authPanel.SetActive(false);
                Debug.Log($"Register Success: {CurrentUser.username}");
            }
            else
            {
                ShowMessage("회원가입 실패: 이미 존재하는 이메일입니다.");
                Debug.LogError($"Register failed: {request.error}");
            }
        }
    }

    private void OnSocialLoginClick(string provider)
    {
        // TODO: Integrate actual SDKs here (Google Sign-In, Apple Auth, Kakao SDK).
        // Get the 'social_id' and 'email' from the SDK callback.
        string mockSocialId = provider + "_" + SystemInfo.deviceUniqueIdentifier;
        string mockEmail = $"{provider}_user@test.com"; 
        string mockName = $"{provider} User";

        StartCoroutine(SocialLoginCoroutine(provider, mockSocialId, mockEmail, mockName));
    }

    private IEnumerator SocialLoginCoroutine(string provider, string socialId, string email, string username)
    {
        string url = $"{BASE_URL}/login/social";
        var data = new SocialLoginData { provider = provider, social_id = socialId, email = email, username = username };
        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                CurrentUser = new UserData { id = response.user_id, username = response.username };
                IsGuest = false;
                
                PlayerPrefs.SetString("SavedUserId", response.user_id);
                PlayerPrefs.SetString("SavedUsername", response.username);
                PlayerPrefs.DeleteKey("IsGuest");

                if (authPanel != null) authPanel.SetActive(false);
                Debug.Log($"Social Login Success ({provider}): {CurrentUser.username}");
            }
            else
            {
                if (messageText != null) messageText.text = $"{provider} 로그인 실패: {request.error}";
                Debug.LogError($"Social Login failed: {request.error}");
            }
        }
    }

    private void OnFindIdClick()
    {
        if (string.IsNullOrEmpty(emailInput.text))
        {
            if (messageText != null) messageText.text = "이메일을 입력해주세요.";
            return;
        }
        StartCoroutine(FindIdCoroutine(emailInput.text));
    }

    private IEnumerator FindIdCoroutine(string email)
    {
        string url = $"{BASE_URL}/find-id";
        var data = new FindIdData { email = email };
        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<FindIdResponse>(request.downloadHandler.text);
                if (messageText != null) messageText.text = $"ID found: {response.username} ({response.provider})";
            }
            else
            {
                if (messageText != null) messageText.text = "사용자를 찾을 수 없습니다.";
            }
        }
    }

    private void OnResetPwClick()
    {
        if (string.IsNullOrEmpty(emailInput.text) || string.IsNullOrEmpty(usernameInput.text))
        {
            if (messageText != null) messageText.text = "이메일과 닉네임(ID)을 모두 입력해주세요.";
            return;
        }
        StartCoroutine(ResetPwCoroutine(emailInput.text, usernameInput.text));
    }

    private IEnumerator ResetPwCoroutine(string email, string username)
    {
        string url = $"{BASE_URL}/reset-password";
        var data = new ResetPwData { email = email, username = username };
        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (messageText != null) messageText.text = "임시 비밀번호가 발송되었습니다. (서버 로그 확인)";
            }
            else
            {
                if (messageText != null) messageText.text = "정보가 일치하지 않습니다.";
            }
        }
    }

    public void Logout()
    {
        CurrentUser = null;
        IsGuest = false;
        PlayerPrefs.DeleteKey("SavedUserId");
        PlayerPrefs.DeleteKey("SavedUsername");
        PlayerPrefs.DeleteKey("IsGuest");
        
        if (authPanel != null)
        {
            authPanel.SetActive(true);
            isLoginMode = true;
            UpdateUIState();
        }
    }
}

public class UserData
{
    public string id;
    public string username;
}

[System.Serializable]
public class LoginData
{
    public string email;
    public string password;
}

[System.Serializable]
public class RegisterData
{
    public string email;
    public string password;
    public string username;
}

[System.Serializable]
public class AuthResponse
{
    public string message;
    public string user_id;
    public string username;
}

[System.Serializable]
public class SocialLoginData
{
    public string provider;
    public string social_id;
    public string email;
    public string username;
}

[System.Serializable]
public class FindIdData
{
    public string email;
}

[System.Serializable]
public class FindIdResponse
{
    public string username;
    public string provider;
}

[System.Serializable]
public class ResetPwData
{
    public string email;
    public string username;
}