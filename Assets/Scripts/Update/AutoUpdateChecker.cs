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
    
    [Header("강제 업데이트 설정")]
    [SerializeField] private float redirectDelay = 3f; // 리디렉션 지연시간
    [SerializeField] private float startDelay = 15f; // 앱 시작 후 업데이트 체크 지연시간 (초)

    private string latestVersion;
    private bool forceUpdate;
    private string currentLanguage;
    private string currentPlatform; // 현재 플랫폼 저장

    // 강화된 다국어 메시지
    private Dictionary<string, LocalizedText> localizedTexts = new Dictionary<string, LocalizedText>()
    {
        ["en"] = new LocalizedText
        {
            // 일반 업데이트
            message = "A new version ({0}) is available. Would you like to update?",
            updateButton = "Update Now",
            cancelButton = "Later",
            
            // 강제 업데이트 (기분좋은 메시지)
            forceUpdateTitle = "🎉 Better Service Update! 🎉",
            forceUpdateMessage = "We've prepared an amazing update ({0}) for a better experience!\n\nRedirecting to store in {1} seconds...",
            forceUpdateMessageNoCountdown = "We've prepared an amazing update ({0}) for a better experience!\n\nTaking you to the store now..."
        },
        ["ko"] = new LocalizedText
        {
            // 일반 업데이트
            message = "새로운 버전({0})이 있습니다. 업데이트하시겠습니까?",
            updateButton = "지금 업데이트",
            cancelButton = "나중에",
            
            // 강제 업데이트 (기분좋은 메시지)
            forceUpdateTitle = "🎉 더 나은 서비스를 위한 업데이트! 🎉",
            forceUpdateMessage = "더욱 좋아진 우팡({0})을 준비했습니다!\n\n{1}초 후 스토어로 이동합니다...",
            forceUpdateMessageNoCountdown = "더욱 좋아진 우팡({0})을 준비했습니다!\n\n스토어로 이동합니다..."
        },
        ["ja"] = new LocalizedText
        {
            // 일반 업데이트
            message = "新しいバージョン({0})があります。アップデートしますか？",
            updateButton = "今すぐ更新",
            cancelButton = "後で",
            
            // 강제 업데이트
            forceUpdateTitle = "🎉 より良いサービスのためのアップデート! 🎉",
            forceUpdateMessage = "より良いエクスペリエンスのために素晴らしいアップデート({0})を準備しました！\n\n{1}秒後にストアに移動します...",
            forceUpdateMessageNoCountdown = "より良いエクスペリエンスのために素晴らしいアップデート({0})を準備しました！\n\nストアに移動します..."
        },
        ["zh"] = new LocalizedText
        {
            // 일반 업데이트
            message = "有新版本({0})可用。您要更新吗？",
            updateButton = "立即更新",
            cancelButton = "稍后",
            
            // 강제 업데이트
            forceUpdateTitle = "🎉 为了更好的服务更新! 🎉",
            forceUpdateMessage = "我们为您准备了精彩的更新({0})以获得更好的体验！\n\n{1}秒后跳转到商店...",
            forceUpdateMessageNoCountdown = "我们为您准备了精彩的更新({0})以获得更好的体验！\n\n正在跳转到商店..."
        },
        ["es"] = new LocalizedText
        {
            // 일반 업데이트
            message = "Una nueva versión ({0}) está disponible. ¿Desea actualizar?",
            updateButton = "Actualizar Ahora",
            cancelButton = "Más Tarde",
            
            // 강제 업데이트
            forceUpdateTitle = "🎉 ¡Actualización para un Mejor Servicio! 🎉",
            forceUpdateMessage = "¡Hemos preparado una actualización increíble ({0}) para una mejor experiencia!\n\nRedirigiendo a la tienda en {1} segundos...",
            forceUpdateMessageNoCountdown = "¡Hemos preparado una actualización increíble ({0}) para una mejor experiencia!\n\nLlevándote a la tienda ahora..."
        }
    };

    void Start()
    {
        // 플랫폼별 서버 URL 설정
        SetPlatformSpecificUrl();
        
        currentVersion = Application.version;
        Debug.Log($"Current App Version: {currentVersion}");
        Debug.Log($"Platform: {Application.platform} ({currentPlatform})");
        Debug.Log($"Server URL: {serverVersionUrl}");
        
        DetectDeviceLanguage();
        
        // UI 초기 설정
        if (updatePanel == null || updateButton == null || cancelButton == null || updateMessageText == null)
        {
            Debug.LogError("UI 요소가 연결되지 않았습니다! Inspector에서 확인해주세요.");
            return;
        }
        
        updatePanel.SetActive(false);
        
        // 일반 업데이트 버튼 설정
        updateButton.onClick.AddListener(OnUpdateButtonClicked);
        cancelButton.onClick.AddListener(OnCancelButtonClicked);

        // 앱 초기화 후 업데이트 체크 시작
        Debug.Log($"업데이트 체크를 {startDelay}초 후에 시작합니다...");
        StartCoroutine(DelayedUpdateCheck());
    }

    void SetPlatformSpecificUrl()
    {
        // 플랫폼별 URL 설정
#if UNITY_ANDROID
        currentPlatform = "android";
        serverVersionUrl = "https://woopang.com/version?platform=android";
#elif UNITY_IOS
        currentPlatform = "ios";
        serverVersionUrl = "https://woopang.com/version?platform=ios";
#else
        currentPlatform = "unknown";
        serverVersionUrl = "https://woopang.com/version"; // 기본 URL
#endif
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
        
        Debug.Log($"Detected language: {currentLanguage} (Device: {deviceLanguage})");
    }

    IEnumerator DelayedUpdateCheck()
    {
        // 앱 초기화 완료까지 대기
        yield return new WaitForSecondsRealtime(startDelay);
        
        Debug.Log("지연 시간 완료, 업데이트 체크 시작!");
        StartCoroutine(CheckForUpdates());
    }

    IEnumerator CheckForUpdates()
    {
        Debug.Log($"서버 버전 체크 시작: {serverVersionUrl}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(serverVersionUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log($"서버 응답: {jsonResponse}");
                
                try
                {
                    VersionResponse versionData = JsonUtility.FromJson<VersionResponse>(jsonResponse);
                    latestVersion = versionData.version;
                    forceUpdate = versionData.forceUpdate;
                    
                    Debug.Log($"서버 버전: {latestVersion}, 강제 업데이트: {forceUpdate} (플랫폼: {currentPlatform})");
                    Debug.Log($"현재 버전: {currentVersion}");
                    
                    bool updateRequired = IsUpdateRequired(currentVersion, latestVersion);
                    Debug.Log($"업데이트 필요?: {updateRequired}");

                    if (updateRequired)
                    {
                        Debug.Log($"새 버전({latestVersion})이 있습니다! 강제 업데이트: {forceUpdate}");
                        
                        if (forceUpdate)
                        {
                            Debug.Log("강제 업데이트 시작");
                            StartCoroutine(ShowForceUpdateAndRedirect());
                        }
                        else
                        {
                            Debug.Log("일반 업데이트 패널 표시");
                            ShowNormalUpdatePanel();
                        }
                    }
                    else
                    {
                        Debug.Log("최신 버전입니다.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"JSON 파싱 오류: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"버전 체크 실패: {request.error}");
                Debug.LogError($"Response Code: {request.responseCode}");
            }
        }
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
            updateMessageText.text = string.Format(texts.message, latestVersion);
        }
        
        // 버튼들 표시
        if (updateButton != null) updateButton.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);
        
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
                        string.Format(texts.forceUpdateMessage, latestVersion, countdownNumber));
                    updateMessageText.text = titleMessage;
                }
                
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
                    string.Format(texts.forceUpdateMessageNoCountdown, latestVersion));
                updateMessageText.text = titleMessage;
            }
            
            // 잠깐 대기 (메시지 읽을 시간)
            yield return new WaitForSecondsRealtime(1.5f);
        }

        // 스토어로 리디렉션
        RedirectToStore();
    }

    void RedirectToStore()
    {
        Debug.Log($"자동으로 스토어로 이동합니다... (플랫폼: {currentPlatform})");
        
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
        Application.OpenURL("https://apps.apple.com/us/app/%EC%9A%B0%ED%8C%A1-woopang/id6746787478");
#endif

        // 스토어 이동 후 앱 종료 (선택사항)
        // Application.Quit();
    }

    void OnUpdateButtonClicked()
    {
        Debug.Log("수동 업데이트를 진행합니다...");
        RedirectToStore();
        
        if (updatePanel != null)
        {
            updatePanel.SetActive(false);
        }
    }

    void OnCancelButtonClicked()
    {
        Debug.Log("업데이트를 취소했습니다.");
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