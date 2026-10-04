using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class AutoUpdateChecker : MonoBehaviour
{
    private string currentVersion;
    private string serverVersionUrl; // 동적으로 설정됨

    [Header("업데이트 UI")]
    [SerializeField] private GameObject updatePanel; // 기존 패널 공통 사용
    [SerializeField] private Button updateButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Text updateMessageText; // 기존 텍스트 공통 사용
    [SerializeField] private R0926UpdateCard card;     // 새 디자인 카드 — 있으면 글·버전·남은 시간을 여기서 그린다
    
    [Header("강제 업데이트 설정")]
    [SerializeField] private float redirectDelay = 3f; // 리디렉션 지연시간
    [SerializeField] private float startDelay = 15f; // 앱 시작 후 업데이트 체크 지연시간 (초)

    private string latestVersion;   // 스토어에 올라온 새 버전 — 안드로이드는 Play 가 이름을 주지 않아 비어 있다
    private bool forceUpdate;
    private bool storeHasUpdate;

    private const string IosAppId = "6746787478";
    private const float StoreCheckTimeout = 15f;

    // 강제로 스토어에 보낸 기록 — 다녀와서도 그대로면 같은 강제 안내로 다시 밀어내지 않는다 (안내 → 스토어 → 앱 → 또 안내 되풀이 방지)
    private const string RedirVerKey = "UpdRedirVer", RedirFromKey = "UpdRedirFrom", RedirAtKey = "UpdRedirAt";
    private const double ForceCooldownHours = 6;
    private bool redirectedThisRun;
    private string currentLanguage;

    // 강화된 다국어 메시지
    private Dictionary<string, LocalizedText> localizedTexts = new Dictionary<string, LocalizedText>()
    {
        ["en"] = new LocalizedText
        {
            // 일반 업데이트
            message = "A new version{0} is available. Would you like to update?",
            updateButton = "Update Now",
            cancelButton = "Later",
            
            // 강제 업데이트 (기분좋은 메시지)
            forceUpdateTitle = "Better Service Update!",
            forceUpdateMessage = "We've prepared an amazing update{0} for a better experience!\n\nRedirecting to store in {1} seconds...",
            forceUpdateMessageNoCountdown = "We've prepared an amazing update{0} for a better experience!\n\nTaking you to the store now..."
        },
        ["ko"] = new LocalizedText
        {
            // 일반 업데이트
            message = "새로운 버전{0}이 있습니다. 업데이트하시겠습니까?",
            updateButton = "지금 업데이트",
            cancelButton = "나중에",
            
            // 강제 업데이트 (기분좋은 메시지)
            forceUpdateTitle = "더 나은 서비스를 위한 업데이트!",
            forceUpdateMessage = "더욱 좋아진 우팡{0}을 준비했습니다!\n\n{1}초 후 스토어로 이동합니다...",
            forceUpdateMessageNoCountdown = "더욱 좋아진 우팡{0}을 준비했습니다!\n\n스토어로 이동합니다..."
        },
        ["ja"] = new LocalizedText
        {
            // 일반 업데이트
            message = "新しいバージョン{0}があります。アップデートしますか？",
            updateButton = "今すぐ更新",
            cancelButton = "後で",
            
            // 강제 업데이트
            forceUpdateTitle = "より良いサービスのためのアップデート!",
            forceUpdateMessage = "より良いエクスペリエンスのために素晴らしいアップデート{0}を準備しました！\n\n{1}秒後にストアに移動します...",
            forceUpdateMessageNoCountdown = "より良いエクスペリエンスのために素晴らしいアップデート{0}を準備しました！\n\nストアに移動します..."
        },
        ["zh"] = new LocalizedText
        {
            // 일반 업데이트
            message = "有新版本{0}可用。您要更新吗？",
            updateButton = "立即更新",
            cancelButton = "稍后",
            
            // 강제 업데이트
            forceUpdateTitle = "为了更好的服务更新!",
            forceUpdateMessage = "我们为您准备了精彩的更新{0}以获得更好的体验！\n\n{1}秒后跳转到商店...",
            forceUpdateMessageNoCountdown = "我们为您准备了精彩的更新{0}以获得更好的体验！\n\n正在跳转到商店..."
        },
        ["es"] = new LocalizedText
        {
            // 일반 업데이트
            message = "Una nueva versión{0} está disponible. ¿Desea actualizar?",
            updateButton = "Actualizar Ahora",
            cancelButton = "Más Tarde",
            
            // 강제 업데이트
            forceUpdateTitle = "¡Actualización para un Mejor Servicio!",
            forceUpdateMessage = "¡Hemos preparado una actualización increíble{0} para una mejor experiencia!\n\nRedirigiendo a la tienda en {1} segundos...",
            forceUpdateMessageNoCountdown = "¡Hemos preparado una actualización increíble{0} para una mejor experiencia!\n\nLlevándote a la tienda ahora..."
        }
    };

    void Start()
    {
        SetPlatformSpecificUrl();
        currentVersion = Application.version;
        DetectDeviceLanguage();

        if (updatePanel == null || updateButton == null || cancelButton == null || updateMessageText == null)
        {
            Debug.LogError("UI 요소가 연결되지 않았습니다! Inspector에서 확인해주세요.");
            return;
        }

        updatePanel.SetActive(false);

        updateButton.onClick.AddListener(OnUpdateButtonClicked);
        cancelButton.onClick.AddListener(OnCancelButtonClicked);

        StartCoroutine(DelayedUpdateCheck());
    }

    void SetPlatformSpecificUrl()
    {
        // 플랫폼별 URL 설정
#if UNITY_ANDROID
        serverVersionUrl = ApiConfig.VERSION_ANDROID;
#elif UNITY_IOS
        serverVersionUrl = ApiConfig.VERSION_IOS;
#else
        serverVersionUrl = ApiConfig.VERSION;
#endif
        // 서버가 내 버전을 보고 정확히 가른다 (출시 관문 — server/release_gate.py)
        serverVersionUrl += (serverVersionUrl.Contains("?") ? "&" : "?") + "v=" + UnityWebRequest.EscapeURL(Application.version);
    }

    void DetectDeviceLanguage()
    {
        SystemLanguage deviceLanguage = Application.systemLanguage;
        
        switch (deviceLanguage)
        {
            case SystemLanguage.Korean:
                currentLanguage = "ko";
                break;
            case SystemLanguage.Japanese:
                currentLanguage = "ja";
                break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                currentLanguage = "zh";
                break;
            case SystemLanguage.Spanish:
                currentLanguage = "es";
                break;
            default:
                currentLanguage = "en";
                break;
        }
    }

    IEnumerator DelayedUpdateCheck()
    {
        yield return new WaitForSecondsRealtime(startDelay);
        StartCoroutine(CheckForUpdates());
    }

    // 스토어에 새 버전이 실제로 올라와 있을 때만 안내한다 — 서버 버전만 보면 스토어 반영 전에 떠서
    // 받을 게 없는 스토어로 보내게 된다. 기기에서 스토어를 먼저 확인하고, 서버에서는 '강제인지' 만 받는다.
    IEnumerator CheckForUpdates()
    {
        storeHasUpdate = false;
        latestVersion = null;
#if UNITY_ANDROID && !UNITY_EDITOR
        yield return StartCoroutine(CheckPlayStore());
#elif UNITY_IOS && !UNITY_EDITOR
        yield return StartCoroutine(CheckAppStore());
#endif
        if (!storeHasUpdate) yield break;   // 에디터·그 밖의 플랫폼도 안내 없음

        // 강제인지는 서버 출시 관문이 내 버전(v=)을 보고 정한다 — 실패하면 일반 안내
        forceUpdate = false;
        using (UnityWebRequest request = UnityWebRequest.Get(serverVersionUrl))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    VersionResponse versionData = JsonUtility.FromJson<VersionResponse>(request.downloadHandler.text);
                    forceUpdate = versionData != null && versionData.forceUpdate
                                  && !string.IsNullOrEmpty(versionData.version)
                                  && IsUpdateRequired(currentVersion, versionData.version);
                }
                catch (Exception e)
                {
                    Debug.LogError($"JSON 파싱 오류: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"버전 체크 실패: {request.error} (Code: {request.responseCode})");
            }
        }

        // 방금 강제로 스토어에 다녀왔는데 그대로면 같은 강제로 다시 밀어내지 않고 일반 안내
        if (forceUpdate && !RecentlyRedirected()) StartCoroutine(ShowForceUpdateAndRedirect());
        else ShowNormalUpdatePanel();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private const int PlayUpdateAvailable = 2;   // UpdateAvailability.UPDATE_AVAILABLE

    // 자바 쪽 스레드에서 불린다 — 끝났다는 표시만 하고 결과는 Unity 쪽에서 읽는다
    private class PlayTaskListener : AndroidJavaProxy
    {
        public volatile bool done;
        public PlayTaskListener() : base("com.google.android.gms.tasks.OnCompleteListener") { }
        public void onComplete(AndroidJavaObject task) { done = true; }
    }

    // Play 인앱 업데이트 API — 이 기기에 Play 가 새 버전을 실제로 내려줄 수 있을 때만 UPDATE_AVAILABLE
    // (심사 중 · 단계적 출시에 아직 안 들어간 기기 · Play 밖에서 설치한 앱은 안내 없음)
    IEnumerator CheckPlayStore()
    {
        var listener = new PlayTaskListener();
        AndroidJavaObject manager = null, task = null;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var factory = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateManagerFactory"))
            {
                manager = factory.CallStatic<AndroidJavaObject>("create", activity);
                task = manager.Call<AndroidJavaObject>("getAppUpdateInfo");
                using (task.Call<AndroidJavaObject>("addOnCompleteListener", listener)) { }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Play 업데이트 확인 시작 실패: {e.Message}");
            task?.Dispose();
            task = null;
        }

        float waited = 0f;
        while (task != null && !listener.done && waited < StoreCheckTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (task != null && listener.done)
        {
            try
            {
                if (task.Call<bool>("isSuccessful"))
                {
                    using (var info = task.Call<AndroidJavaObject>("getResult"))
                        storeHasUpdate = info.Call<int>("updateAvailability") == PlayUpdateAvailable;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Play 업데이트 확인 실패: {e.Message}");
            }
        }

        task?.Dispose();
        manager?.Dispose();
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [Serializable] private class AppStoreLookup { public AppStoreApp[] results; }
    [Serializable] private class AppStoreApp { public string version; }

    // 앱스토어에 지금 올라가 있는 버전 — 심사 통과 후 출시돼야 바뀐다 (한국 스토어 → 없으면 미국)
    IEnumerator CheckAppStore()
    {
        foreach (string country in new[] { "kr", "us" })
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"https://itunes.apple.com/lookup?id={IosAppId}&country={country}"))
            {
                request.timeout = (int)StoreCheckTimeout;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) continue;

                string storeVersion = ParseAppStoreVersion(request.downloadHandler.text);
                if (string.IsNullOrEmpty(storeVersion)) continue;
                latestVersion = storeVersion;
                storeHasUpdate = IsUpdateRequired(currentVersion, storeVersion);
                yield break;
            }
        }
    }

    static string ParseAppStoreVersion(string json)
    {
        try
        {
            var lookup = JsonUtility.FromJson<AppStoreLookup>(json);
            return lookup != null && lookup.results != null && lookup.results.Length > 0 ? lookup.results[0].version : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"앱스토어 조회 파싱 실패: {e.Message}");
            return null;
        }
    }
#endif

    // 안내 글의 버전 표기 — 모르면(안드로이드) 빼고 "새로운 버전이 있습니다"
    string VersionLabel()
    {
        if (string.IsNullOrEmpty(latestVersion)) return "";
        return currentLanguage == "en" || currentLanguage == "es" ? $" ({latestVersion})" : $"({latestVersion})";
    }

    void ShowNormalUpdatePanel()
    {
        if (!localizedTexts.ContainsKey(currentLanguage))
        {
            currentLanguage = "en";
        }

        LocalizedText texts = localizedTexts[currentLanguage];
        
        // 일반 업데이트 UI 업데이트
        if (updateMessageText != null)
        {
            updateMessageText.text = string.Format(texts.message, VersionLabel());
        }
        
        // 버튼들 표시
        if (updateButton != null) updateButton.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);
        if (card != null) card.ShowNormal(currentVersion, latestVersion);
        
        if (updatePanel != null)
        {
            updatePanel.SetActive(true);
        }
    }

    IEnumerator ShowForceUpdateAndRedirect()
    {
        if (!localizedTexts.ContainsKey(currentLanguage))
        {
            currentLanguage = "en";
        }

        LocalizedText texts = localizedTexts[currentLanguage];
        
        // 기존 패널 사용하여 강제 업데이트 표시
        if (updatePanel != null)
        {
            updatePanel.SetActive(true);
        }
        
        // 버튼들 숨김
        if (updateButton != null) updateButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);

        // 햅틱 피드백
        if (SystemInfo.deviceType == DeviceType.Handheld)
        {
            Handheld.Vibrate();
        }

        // 카운트다운이 있는 경우
        if (redirectDelay > 0)
        {
            float remainingTime = redirectDelay;
            
            while (remainingTime > 0)
            {
                int countdownNumber = Mathf.CeilToInt(remainingTime);
                
                // 메시지 업데이트 (제목 + 카운트다운 한 줄로 표시)
                if (updateMessageText != null)
                {
                    string titleMessage = string.Format("{0}\n\n{1}", 
                        texts.forceUpdateTitle,
                        string.Format(texts.forceUpdateMessage, VersionLabel(), countdownNumber));
                    updateMessageText.text = titleMessage;
                }
                if (card != null) card.ShowForce(currentVersion, latestVersion, remainingTime / redirectDelay);
                
                remainingTime -= Time.unscaledDeltaTime; // unscaledDeltaTime 사용 (timeScale 영향 받지 않음)
                yield return null;
            }
        }
        else
        {
            // 카운트다운 없이 바로 메시지 표시
            if (updateMessageText != null)
            {
                string titleMessage = string.Format("{0}\n\n{1}",
                    texts.forceUpdateTitle,
                    string.Format(texts.forceUpdateMessageNoCountdown, VersionLabel()));
                updateMessageText.text = titleMessage;
            }
            if (card != null) card.ShowForce(currentVersion, latestVersion, 0f);
            
            // 잠깐 대기 (메시지 읽을 시간)
            yield return new WaitForSecondsRealtime(1.5f);
        }

        // 스토어로 리디렉션
        RememberRedirect();
        redirectedThisRun = true;
        RedirectToStore();
    }

    // 강제로 보낸 스토어에서 업데이트 없이 돌아왔다(업데이트했다면 앱이 새로 켜진다) — 강제 안내에 갇히지 않게 일반 안내로
    void OnApplicationPause(bool paused)
    {
        if (!paused) OnReturnFromStore();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) OnReturnFromStore();
    }

    void OnReturnFromStore()
    {
        if (!redirectedThisRun || updatePanel == null || !updatePanel.activeSelf) return;
        redirectedThisRun = false;
        ShowNormalUpdatePanel();
    }

    bool RecentlyRedirected()
    {
        if (PlayerPrefs.GetString(RedirVerKey, "") != (latestVersion ?? "")) return false;
        if (PlayerPrefs.GetString(RedirFromKey, "") != currentVersion) return false;
        if (!long.TryParse(PlayerPrefs.GetString(RedirAtKey, "0"), out long at)) return false;
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() - at < ForceCooldownHours * 3600;
    }

    void RememberRedirect()
    {
        PlayerPrefs.SetString(RedirVerKey, latestVersion ?? "");
        PlayerPrefs.SetString(RedirFromKey, currentVersion ?? "");
        PlayerPrefs.SetString(RedirAtKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        PlayerPrefs.Save();
    }

    void RedirectToStore()
    {
#if UNITY_ANDROID
        try
        {
            Application.OpenURL("market://details?id=com.que.woopang");
        }
        catch (Exception)
        {
            Application.OpenURL("https://play.google.com/store/apps/details?id=com.que.woopang&hl=ko");
        }
#elif UNITY_IOS
        Application.OpenURL("https://apps.apple.com/us/app/%EC%9A%B0%ED%8C%A1-woopang/id" + IosAppId);
#endif

        // 스토어 이동 후 앱 종료 (선택사항)
        // Application.Quit();
    }

    void OnUpdateButtonClicked()
    {
        RedirectToStore();
        
        if (updatePanel != null)
        {
            updatePanel.SetActive(false);
        }
    }

    void OnCancelButtonClicked()
    {
        if (updatePanel != null)
        {
            updatePanel.SetActive(false);
        }
    }

    bool IsUpdateRequired(string current, string server)
    {
        try
        {
            var currParts = current.Split('.').Select(int.Parse).ToArray();
            var servParts = server.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Min(currParts.Length, servParts.Length); i++)
            {
                if (currParts[i] < servParts[i]) return true;
                if (currParts[i] > servParts[i]) return false;
            }
            return servParts.Length > currParts.Length;
        }
        catch (Exception e)
        {
            Debug.LogError($"버전 비교 오류: {e.Message}");
            return false;
        }
    }

    public void SetLanguage(string languageCode)
    {
        if (localizedTexts.ContainsKey(languageCode))
        {
            currentLanguage = languageCode;
        }
    }
}

[System.Serializable]
public class VersionResponse
{
    public string version;
    public bool forceUpdate;
}

[System.Serializable]
public class LocalizedText
{
    // 일반 업데이트
    public string message;
    public string updateButton;
    public string cancelButton;
    
    // 강제 업데이트 (기분좋은 메시지)
    public string forceUpdateTitle;
    public string forceUpdateMessage; // 카운트다운 있는 버전
    public string forceUpdateMessageNoCountdown; // 카운트다운 없는 버전
}