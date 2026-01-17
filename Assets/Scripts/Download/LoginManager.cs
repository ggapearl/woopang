using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System;

/// <summary>
/// 웹 로그인 방식 LoginManager
/// - 로그인 버튼 클릭 → 웹 브라우저에서 로그인
/// - Deep Link로 앱에 토큰 전달
/// - 토큰으로 프로필 조회
/// </summary>
public class LoginManager : MonoBehaviour
{
    public static LoginManager Instance { get; private set; }

    // Event for login state changes
    public event Action<bool> OnLoginStateChanged;

    [Header("Login Prompt Popup")]
    [Tooltip("로그인이 필요할 때 표시되는 팝업")]
    public GameObject loginPromptPanel;
    public Text promptText;
    public Button webLoginButton;      // "로그인하기" → 웹 열기
    public Button guestLoginButton;    // "로그인 없이 이용"
    public Button cancelButton;        // "취소" (팝업 닫기)

    [Header("Profile Display")]
    public Text profileUsernameText;
    public Transform uiCanvasRoot;

    [Header("Settings")]
    public float loginPromptDelay = 30f;  // 앱 시작 후 로그인 팝업 표시 딜레이

    // URLs (ApiConfig 사용)

    // User State
    public UserData CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser != null && !IsGuest;
    public bool IsGuest { get; private set; }
    public int CurrentUserId => int.TryParse(CurrentUser?.id, out int id) ? id : 0;
    public string CurrentUsername => CurrentUser?.username;

    // Localization
    private string currentLang = "en";

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

        // 언어 설정
        SetLanguage();

        // 버튼 리스너
        if (webLoginButton != null)
            webLoginButton.onClick.AddListener(OpenWebLogin);
        if (guestLoginButton != null)
            guestLoginButton.onClick.AddListener(GuestLogin);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CloseLoginPrompt);

        // 자동 로그인 시도
        TryAutoLogin();
    }

    void Start()
    {
        // Deep Link 핸들러 등록
        Application.deepLinkActivated += OnDeepLinkActivated;

        // 앱 시작 시 Deep Link 확인 (콜드 스타트)
        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            OnDeepLinkActivated(Application.absoluteURL);
        }
    }

#if UNITY_EDITOR
    [Header("=== 에디터 테스트용 ===")]
    [Tooltip("웹에서 복사한 딥링크 URL을 여기에 붙여넣기 하세요")]
    [SerializeField] private string testDeepLinkUrl = "woopang://auth?token=3&username=woopang&provider=email";

    /// <summary>
    /// 에디터에서 딥링크 테스트용 메서드
    /// Inspector에서 Context Menu로 호출 가능
    /// </summary>
    [ContextMenu("Test Deep Link (Editor Only)")]
    public void TestDeepLinkInEditor()
    {
        if (string.IsNullOrEmpty(testDeepLinkUrl))
        {
            Debug.LogError("[LoginManager] testDeepLinkUrl이 비어있습니다. Inspector에서 URL을 입력하세요.");
            return;
        }
        Debug.Log($"[LoginManager] 에디터 딥링크 테스트: {testDeepLinkUrl}");
        OnDeepLinkActivated(testDeepLinkUrl);
    }

    /// <summary>
    /// 커스텀 딥링크 테스트
    /// </summary>
    public void SimulateDeepLink(string url)
    {
        Debug.Log($"[LoginManager] 딥링크 시뮬레이션: {url}");
        OnDeepLinkActivated(url);
    }
#endif

    void OnDestroy()
    {
        Application.deepLinkActivated -= OnDeepLinkActivated;
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
        // 게스트 로그인 상태 확인
        if (PlayerPrefs.GetInt("IsGuest", 0) == 1)
        {
            RestoreGuestLogin();
            return;
        }

        // 저장된 토큰으로 자동 로그인
        string savedToken = PlayerPrefs.GetString("AuthToken", "");
        string savedUserId = PlayerPrefs.GetString("SavedUserId", "");
        string savedUsername = PlayerPrefs.GetString("SavedUsername", "");

        if (!string.IsNullOrEmpty(savedToken) && !string.IsNullOrEmpty(savedUserId))
        {
            // 저장된 정보로 로그인 상태 복원
            CurrentUser = new UserData
            {
                id = savedUserId,
                username = savedUsername
            };
            IsGuest = false;
            OnLoginStateChanged?.Invoke(true);
            UpdateProfileUI();
            Debug.Log($"[LoginManager] 자동 로그인: {savedUsername}");
        }
        else
        {
            // 로그인 안됨 → 일정 시간 후 로그인 팝업 표시
            StartCoroutine(ShowLoginPromptAfterDelay());
        }
    }

    private IEnumerator ShowLoginPromptAfterDelay()
    {
        yield return new WaitForSeconds(loginPromptDelay);

        // 여전히 로그인 안됐으면 팝업 표시
        if (!IsLoggedIn && !IsGuest)
        {
            ShowLoginRequirementPopup();
        }
    }

    #endregion

    #region Web Login

    /// <summary>
    /// 웹 브라우저에서 로그인 페이지 열기
    /// </summary>
    public void OpenWebLogin()
    {
        string url = $"{ApiConfig.LOGIN_VIEW}?lang={currentLang}";
        Application.OpenURL(url);
        CloseLoginPrompt();
        Debug.Log($"[LoginManager] 웹 로그인 페이지 열기: {url}");
    }

    /// <summary>
    /// Deep Link 수신 처리
    /// 형식: woopang://auth?token=xxx&username=xxx&provider=email
    /// </summary>
    private void OnDeepLinkActivated(string url)
    {
        Debug.Log($"[LoginManager] Deep Link 수신: {url}");

        if (string.IsNullOrEmpty(url)) return;

        // woopang://auth?token=xxx&username=xxx&provider=email
        if (url.StartsWith("woopang://auth"))
        {
            ParseAuthDeepLink(url);
        }
    }

    private void ParseAuthDeepLink(string url)
    {
        try
        {
            // URL 파싱
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
                // 로그인 성공 처리
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
        // JWT 토큰을 서버에서 검증
        StartCoroutine(VerifyTokenAndLogin(token));
    }

    private IEnumerator VerifyTokenAndLogin(string jwtToken)
    {
        Debug.Log("[LoginManager] JWT 토큰 검증 시작...");

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
                        // 검증 성공 - 사용자 정보로 로그인 완료
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

                        // 저장 (JWT 토큰과 user_id 모두 저장)
                        PlayerPrefs.SetString("AuthToken", jwtToken);
                        PlayerPrefs.SetString("SavedUserId", response.data.id.ToString());
                        PlayerPrefs.SetString("SavedUsername", response.data.username);
                        PlayerPrefs.SetString("LoginProvider", response.data.provider);
                        PlayerPrefs.DeleteKey("IsGuest");
                        PlayerPrefs.Save();

                        OnLoginStateChanged?.Invoke(true);
                        UpdateProfileUI();
                        CloseLoginPrompt();

                        Debug.Log($"[LoginManager] JWT 검증 성공 - 로그인 완료: {response.data.username}");
                    }
                    else
                    {
                        Debug.LogError($"[LoginManager] JWT 검증 실패: {response.message}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LoginManager] 응답 파싱 오류: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[LoginManager] JWT 검증 요청 실패: {request.error}");
            }
        }
    }

    #endregion

    #region Guest Login

    public void GuestLogin()
    {
        string deviceId = SystemInfo.deviceUniqueIdentifier;
        CurrentUser = new UserData
        {
            id = deviceId,
            username = "Guest_" + deviceId.Substring(0, 4)
        };
        IsGuest = true;

        PlayerPrefs.SetInt("IsGuest", 1);
        PlayerPrefs.SetString("SavedUserId", deviceId);
        PlayerPrefs.SetString("SavedUsername", CurrentUser.username);
        PlayerPrefs.Save();

        OnLoginStateChanged?.Invoke(true);
        UpdateProfileUI();
        CloseLoginPrompt();

        Debug.Log($"[LoginManager] 게스트 로그인: {CurrentUser.username}");
    }

    private void RestoreGuestLogin()
    {
        string deviceId = PlayerPrefs.GetString("SavedUserId", SystemInfo.deviceUniqueIdentifier);
        string username = PlayerPrefs.GetString("SavedUsername", "Guest_" + deviceId.Substring(0, 4));

        CurrentUser = new UserData
        {
            id = deviceId,
            username = username
        };
        IsGuest = true;

        OnLoginStateChanged?.Invoke(true);
        UpdateProfileUI();

        Debug.Log($"[LoginManager] 게스트 상태 복원: {username}");
    }

    #endregion

    #region Logout

    public void Logout()
    {
        CurrentUser = null;
        IsGuest = false;

        PlayerPrefs.DeleteKey("AuthToken");
        PlayerPrefs.DeleteKey("SavedUserId");
        PlayerPrefs.DeleteKey("SavedUsername");
        PlayerPrefs.DeleteKey("LoginProvider");
        PlayerPrefs.DeleteKey("IsGuest");
        PlayerPrefs.Save();

        OnLoginStateChanged?.Invoke(false);
        UpdateProfileUI();

        Debug.Log("[LoginManager] 로그아웃 완료");
    }

    #endregion

    #region Login Prompt Popup

    /// <summary>
    /// 로그인이 필요한 기능 사용 시 팝업 표시
    /// </summary>
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

    /// <summary>
    /// 서버에서 최신 프로필 정보 가져오기
    /// </summary>
    public IEnumerator FetchProfileCoroutine(string userId)
    {
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
                        CurrentUser = profile;
                        UpdateProfileUI();
                        Debug.Log($"[LoginManager] 프로필 조회 성공: {profile.username}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LoginManager] 프로필 파싱 오류: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[LoginManager] 프로필 조회 실패: {request.error}");
            }
        }
    }

    #endregion
}

/// <summary>
/// 사용자 데이터
/// </summary>
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

/// <summary>
/// JWT 토큰 검증 API 응답
/// </summary>
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
