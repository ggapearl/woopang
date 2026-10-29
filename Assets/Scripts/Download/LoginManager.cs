using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System;

public class LoginManager : MonoBehaviour
{
    public static LoginManager Instance { get; private set; }

    /// <summary>
    /// 로그인 토큰이 저장되는 PlayerPrefs 키. 이 이름 하나로 통일한다.
    /// (예전엔 일부 코드가 "auth_token"(소문자)을 읽어 항상 빈 값이었다 — FollowManager 등)
    /// </summary>
    public const string AUTH_TOKEN_KEY = "AuthToken";

    /// <summary>
    /// 민감 API 요청에 Authorization: Bearer 토큰을 붙인다.
    /// 서버가 이 토큰에서 신원을 확정하므로, 요청에 실은 user_id 를 위조해도 무력화된다.
    /// 토큰이 없으면(비로그인) 아무것도 하지 않는다 — 서버가 판단한다.
    /// </summary>
    public static void ApplyAuth(UnityEngine.Networking.UnityWebRequest request)
    {
        if (request == null) return;
        string stored = PlayerPrefs.GetString(AUTH_TOKEN_KEY, "");
        if (!string.IsNullOrEmpty(stored))
            request.SetRequestHeader("Authorization", "Bearer " + stored);
    }

    /// <summary>
    /// 토큰을 붙인 요청이 끝난 뒤 부른다 — 401 이면 true. 로그인 중이었으면 서버에 토큰을 한 번 확인해
    /// 정말 죽었을 때만 로그아웃하고 다시 로그인하라고 알린다 (401 한 번으로 내보내면 서버 한 곳의 일시 오류에도 로그인이 풀린다)
    /// </summary>
    public static bool CheckUnauthorized(UnityWebRequest request)
    {
        if (request == null || request.responseCode != 401) return false;
        if (Instance != null && Instance.IsLoggedIn) Instance.RecheckToken();
        return true;
    }

    public event Action<bool> OnLoginStateChanged;
    /// <summary>로그아웃 직전 — 토큰이 아직 있을 때 그 계정으로 서버에 남긴 것을 내린다 (P2PManager 의 근처 사람 위치)</summary>
    public event Action OnLoggingOut;

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

    private bool recheckRunning;
    private string recheckToken;   // 마지막으로 서버에 물어본 토큰 — 같은 토큰은 1분에 한 번까지 (다시 로그인한 새 토큰은 바로)
    private float recheckAt = -999f;
    private Coroutine expiredPromptCo;

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

    /// <summary>웹 로그인을 열기 직전 — 에디터가 복사된 '앱으로 돌아가기' 링크를 기다린다 (Editor/EditorLoginWatcher)</summary>
    public static Action EditorBeforeWebLogin;
    /// <summary>플레이 시험이 로그인 확인을 시험 서버로 돌릴 때 (비어 있으면 실제 서버)</summary>
    public static string EditorAuthBase;
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
        // 켜 둔 채로도 토큰은 만료된다 — 돌아올 때 확인. 하루 넘은 토큰이면 서버에 물어 새 토큰을 받아 둔다 (껐다 켜지 않고 며칠을 써도 풀리지 않게)
        if (hasFocus && IsLoggedIn)
        {
            if (StoredTokenDead())
            {
                ExpireSession();
                return;
            }
            if (StoredTokenStale()) RecheckToken();
        }
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
            case "session_expired":
                switch (currentLang)
                {
                    case "ko": return "로그인이 만료됐어요.\n다시 로그인할까요?";
                    case "ja": return "ログインの有効期限が切れました。\nもう一度ログインしますか？";
                    case "zh": return "登录已过期。\n要重新登录吗？";
                    case "es": return "Tu sesión ha caducado.\n¿Quieres iniciar sesión de nuevo?";
                    default: return "Your login has expired.\nLog in again?";
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
            // 토큰이 없거나 만료됐으면 로그인된 척하지 않는다 — 화면은 로그인 상태인데 토큰이 필요한 기능(친구 지도·DM·보물찾기·위치 공유)이
            // 모두 401 이라 다시 로그인할 길도 안 보였다 (2026-10-25 goosoon56 — 안드로이드 백업이 옛 토큰을 되살려 앱을 지웠다 깔아도 그대로)
            if (StoredTokenDead())
            {
                ExpireSession();
                return;
            }

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

            // 서명·탈퇴 여부는 서버만 안다 — 켤 때 한 번 확인 (서버가 오래된 토큰을 새 토큰으로 바꿔 주면 그것도 받는다)
            RecheckToken();
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

    [Serializable]
    private class JwtClaims { public double exp, iat; }

    /// <summary>JWT 의 만료 · 발급 시각(유닉스 초) — JWT 가 아니거나 읽을 수 없으면 null (서명은 서버가 본다)</summary>
    private static JwtClaims TokenClaims(string token)
    {
        string[] parts = token.Split('.');
        if (parts.Length != 3) return null;
        try
        {
            string b64 = parts[1].Replace('-', '+').Replace('_', '/');
            b64 += new string('=', (4 - b64.Length % 4) % 4);
            return JsonUtility.FromJson<JwtClaims>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 저장된 토큰을 더는 못 쓰는가 — 없거나(실기기) 만료된 JWT.
    /// 에디터 로그인(EditorLogin)은 토큰 없이 쓰고, 플레이 시험의 시험 토큰은 JWT 가 아니라 여기서 판단하지 않는다
    /// </summary>
    private static bool StoredTokenDead()
    {
        string jwt = PlayerPrefs.GetString(AUTH_TOKEN_KEY, "");
        if (string.IsNullOrEmpty(jwt)) return !Application.isEditor;
        double exp = TokenClaims(jwt)?.exp ?? 0;
        return exp > 0 && exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
    }

    /// <summary>하루 넘은 토큰 — 서버에 물으면 새 토큰으로 바꿔 준다. 이번에 이미 물어본 토큰이면 다시 묻지 않는다 (180일이 지나 서버가 더 늘려 주지 않는 토큰)</summary>
    private bool StoredTokenStale()
    {
        string jwt = PlayerPrefs.GetString(AUTH_TOKEN_KEY, "");
        if (string.IsNullOrEmpty(jwt) || jwt == recheckToken) return false;
        double iat = TokenClaims(jwt)?.iat ?? 0;
        return iat > 0 && iat < DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86400;
    }

    /// <summary>저장된 토큰을 서버에 확인한다 — 서버가 분명히 거절할 때만 로그아웃 (연결이 안 되거나 서버 오류면 그대로). 같은 토큰은 1분에 한 번까지</summary>
    private void RecheckToken()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(EditorAuthBase)) return;   // 에디터는 시험 서버로 돌릴 때만 — 시험 토큰을 운영 서버에 묻지 않게
#endif
        string jwt = PlayerPrefs.GetString(AUTH_TOKEN_KEY, "");
        if (string.IsNullOrEmpty(jwt) || recheckRunning || (jwt == recheckToken && Time.unscaledTime - recheckAt < 60f)) return;
        StartCoroutine(RecheckTokenCo(jwt));
    }

    private IEnumerator RecheckTokenCo(string token)
    {
        recheckRunning = true;
        recheckToken = token;
        recheckAt = Time.unscaledTime;
        using (UnityWebRequest request = VerifyRequest(token))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            recheckRunning = false;
            if (request.result != UnityWebRequest.Result.Success)
            {
                recheckToken = null;   // 연결이 안 됐다 — 다음에 돌아올 때 다시 묻는다
                yield break;
            }

            AuthVerifyResponse response = null;
            try { response = JsonUtility.FromJson<AuthVerifyResponse>(request.downloadHandler.text); }
            catch (Exception) { }
            // 그 사이 로그아웃했거나 다른 토큰으로 새로 로그인했으면 이 결과는 버린다
            if (response == null || !IsLoggedIn || PlayerPrefs.GetString(AUTH_TOKEN_KEY, "") != token) yield break;

            if (response.success)
            {
                if (!string.IsNullOrEmpty(response.token) && response.token != token)
                {
                    PlayerPrefs.SetString(AUTH_TOKEN_KEY, response.token);   // 쓰는 동안은 만료되지 않게 서버가 늘려 준 토큰
                    PlayerPrefs.Save();
                }
                yield break;
            }
            if (response.message != "Invalid or expired token" && response.message != "User not found") yield break;   // 'Server error' 등은 그대로 둔다
            Debug.LogWarning($"[LoginManager] 서버가 저장된 로그인을 거절해 로그아웃합니다: {response.message}");
            ExpireSession();
        }
    }

    /// <summary>토큰이 죽었다 — 로그아웃 상태로 돌리고, 시작화면이 끝나면 다시 로그인하라고 알린다</summary>
    private void ExpireSession()
    {
        if (CurrentUser != null)
        {
            Logout();
        }
        else
        {
            // 앱을 켜는 중 (Awake) — 아직 아무도 로그인 상태를 받지 않았다. 푸시 연결은 Firebase 가 뜬 뒤에 끊는다
            string userId = PlayerPrefs.GetString("SavedUserId", "");
            ClearSavedLogin();
            StartCoroutine(DelayedUnregister(userId));
        }
        if (expiredPromptCo == null) expiredPromptCo = StartCoroutine(ShowExpiredPrompt());
    }

    private IEnumerator DelayedUnregister(string userId)
    {
        yield return new WaitForSeconds(3f);
        UnregisterFCMToken(userId);
    }

    private IEnumerator ShowExpiredPrompt()
    {
        yield return null;   // 시작화면이 이번 프레임 Awake 에서 켜진다
        while (FindFirstObjectByType<R0926Splash>() != null) yield return new WaitForSecondsRealtime(0.25f);
        yield return new WaitForSecondsRealtime(0.5f);
        expiredPromptCo = null;
        if (!IsLoggedIn) ShowLoginPrompt(GetLocalizedText("session_expired"));
    }

    #endregion

    #region Web Login

    public void OpenWebLogin()
    {
        string url = $"{ApiConfig.LOGIN_VIEW}?lang={currentLang}";
#if UNITY_EDITOR
        EditorBeforeWebLogin?.Invoke();
#endif
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

    private static UnityWebRequest VerifyRequest(string jwtToken)
    {
        string verifyUrl = $"{ApiConfig.MAIN_SERVER}/api/auth/verify";
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(EditorAuthBase)) verifyUrl = EditorAuthBase + "/api/auth/verify";
#endif
        string jsonBody = $"{{\"token\":\"{jwtToken}\"}}";
        var request = new UnityWebRequest(verifyUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private IEnumerator VerifyTokenAndLogin(string jwtToken)
    {
        using (UnityWebRequest request = VerifyRequest(jwtToken))
        {
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

                        PlayerPrefs.SetString(AUTH_TOKEN_KEY, jwtToken);
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
                    else
                    {
                        Debug.LogWarning($"[LoginManager] 로그인 확인 실패: {response?.message}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LoginManager] 응답 파싱 오류: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[LoginManager] 로그인 확인 요청 실패: {request.error}");
            }
        }
    }

    #endregion

    #region Logout

    public void Logout()
    {
        // 로그아웃 전에 user_id 저장 (토큰 해제에 사용)
        string previousUserId = CurrentUser?.id;
        if (CurrentUser != null) OnLoggingOut?.Invoke();

        CurrentUser = null;
        IsGuest = false;

        ClearSavedLogin();

        // FCM 토큰에서 user_id 제거 (푸시 알림 수신 중지)
        UnregisterFCMToken(previousUserId);

        OnLoginStateChanged?.Invoke(false);
        UpdateProfileUI();
    }

    private static void ClearSavedLogin()
    {
        PlayerPrefs.DeleteKey(AUTH_TOKEN_KEY);
        PlayerPrefs.DeleteKey("SavedUserId");
        PlayerPrefs.DeleteKey("SavedUsername");
        PlayerPrefs.DeleteKey("LoginProvider");
        PlayerPrefs.DeleteKey("IsGuest");
        PlayerPrefs.Save();
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

    public void ShowLoginRequirementPopup() => ShowLoginPrompt(GetLocalizedText("login_required"));

    private void ShowLoginPrompt(string message)
    {
        if (loginPromptPanel == null) return;
        if (promptText != null) promptText.text = message;
        loginPromptPanel.SetActive(true);
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

    /// <summary>앱 '프로필 편집'에서 아이디를 바꿨다 (R0926ProfileEdit) — 다음에 켤 때도 새 아이디로</summary>
    public void SetUsername(string username)
    {
        if (CurrentUser == null || string.IsNullOrEmpty(username)) return;
        CurrentUser.username = username;
        PlayerPrefs.SetString("SavedUsername", username);
        PlayerPrefs.Save();
        UpdateProfileUI();
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
    public string token;   // 서버가 오래된 토큰 대신 새로 준 토큰 (없으면 빈 값)
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
