using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 위치 권한 거부 시 안내 패널 표시 + 앱 설정으로 이동
/// iOS / Android 각각 처리
/// LoginManager 오브젝트에 자동으로 추가됨 (LocationPermissionSetup 에디터 스크립트)
/// </summary>
public class LocationPermissionManager : MonoBehaviour
{
    public static LocationPermissionManager Instance { get; private set; }

    [Header("Location Permission Panel")]
    public GameObject locationPermissionPanel;
    public Text promptText;
    public Button goToSettingsButton;
    public Button cancelButton;

    private string currentLang = "en";

    private CanvasGroup panelCanvasGroup;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        SetLanguage();
        AutoConnectFields();

        // CanvasGroup으로 레이캐스트 완전 차단 (SetActive보다 확실)
        if (locationPermissionPanel != null)
        {
            panelCanvasGroup = locationPermissionPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = locationPermissionPanel.AddComponent<CanvasGroup>();

            // 초기 상태: 보이지 않고 레이캐스트 차단하지 않음
            SetPanelVisible(false);
        }

        if (goToSettingsButton != null)
            goToSettingsButton.onClick.AddListener(OpenAppSettings);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(ClosePanel);
    }

    private void SetPanelVisible(bool visible)
    {
        if (locationPermissionPanel == null) return;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.blocksRaycasts = visible;
            panelCanvasGroup.interactable = visible;
        }
        locationPermissionPanel.SetActive(visible);
    }

    private void AutoConnectFields()
    {
        if (locationPermissionPanel == null)
        {
            Transform t = transform.root.Find("LocationPermissionPanel");
            if (t == null)
            {
                // Canvas 하위에서 찾기
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform found = canvas.transform.Find("LocationPermissionPanel");
                    if (found != null) locationPermissionPanel = found.gameObject;
                }
            }
            else
            {
                locationPermissionPanel = t.gameObject;
            }
        }

        if (locationPermissionPanel != null)
        {
            if (promptText == null)
                promptText = locationPermissionPanel.GetComponentInChildren<Text>();
            if (goToSettingsButton == null)
            {
                Transform btn = locationPermissionPanel.transform.Find("Box/Buttons/Btn_Settings");
                if (btn != null) goToSettingsButton = btn.GetComponent<Button>();
            }
            if (cancelButton == null)
            {
                Transform btn = locationPermissionPanel.transform.Find("Box/Buttons/Btn_Close");
                if (btn != null) cancelButton = btn.GetComponent<Button>();
            }
        }
    }

    private void SetLanguage()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:   currentLang = "ko"; break;
            case SystemLanguage.Japanese: currentLang = "ja"; break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: currentLang = "zh"; break;
            case SystemLanguage.Spanish:  currentLang = "es"; break;
            default: currentLang = "en"; break;
        }
    }

    private string GetPromptText()
    {
        switch (currentLang)
        {
            case "ko": return "주변 콘텐츠를 보려면\n위치 접근 권한이 필요합니다.\n설정에서 허용해 주세요.";
            case "ja": return "周辺コンテンツを見るには\n位置情報の許可が必要です。\n設定から許可してください。";
            case "zh": return "查看周边内容\n需要位置访问权限。\n请在设置中开启。";
            case "es": return "Para ver contenido cercano\nnecesitamos tu ubicación.\nActívala en Ajustes.";
            default:   return "To view nearby content,\nlocation access is required.\nPlease allow it in Settings.";
        }
    }

    private string GetSettingsButtonText()
    {
        switch (currentLang)
        {
            case "ko": return "설정으로 이동";
            case "ja": return "設定を開く";
            case "zh": return "前往设置";
            case "es": return "Ir a Ajustes";
            default:   return "Open Settings";
        }
    }

    private string GetCancelButtonText()
    {
        switch (currentLang)
        {
            case "ko": return "닫기";
            case "ja": return "閉じる";
            case "zh": return "关闭";
            case "es": return "Cerrar";
            default:   return "Close";
        }
    }

    public void ShowPanel()
    {
        if (locationPermissionPanel == null)
        {
            AutoConnectFields();
            if (locationPermissionPanel == null) return;

            // CanvasGroup 재초기화
            panelCanvasGroup = locationPermissionPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = locationPermissionPanel.AddComponent<CanvasGroup>();
        }

        if (promptText != null)
            promptText.text = GetPromptText();

        if (goToSettingsButton != null)
        {
            Text btnText = goToSettingsButton.GetComponentInChildren<Text>();
            if (btnText != null) btnText.text = GetSettingsButtonText();
        }
        if (cancelButton != null)
        {
            Text btnText = cancelButton.GetComponentInChildren<Text>();
            if (btnText != null) btnText.text = GetCancelButtonText();
        }

        locationPermissionPanel.transform.SetAsLastSibling();
        SetPanelVisible(true);
    }

    public void ClosePanel()
    {
        SetPanelVisible(false);
    }

    private void OpenAppSettings()
    {
        ClosePanel();

#if UNITY_IOS
        Application.OpenURL("app-settings:");
#elif UNITY_ANDROID
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = new AndroidJavaObject("android.content.Intent",
                "android.settings.APPLICATION_DETAILS_SETTINGS"))
            {
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                {
                    var parsedUri = uriClass.CallStatic<AndroidJavaObject>("parse",
                        "package:" + Application.identifier);
                    intent.Call<AndroidJavaObject>("setData", parsedUri);
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000);
                    activity.Call("startActivity", intent);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LocationPermissionManager] 설정 열기 실패: {e.Message}");
        }
#endif
    }
}
