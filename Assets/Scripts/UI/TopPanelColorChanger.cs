using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class TopPanelColorChanger : MonoBehaviour, IPointerClickHandler
{
#if UNITY_IOS && !UNITY_EDITOR
    // iOS 네이티브: Status Bar 아이콘 색상 변경 (true=흰색, false=검정색)
    [DllImport("__Internal")]
    private static extern void _SetStatusBarStyle(bool lightContent);
#endif

    [Header("References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image logoImage;

    [Header("Settings")]
    [SerializeField] private int currentColorIndex = 0;

    private const string PREF_COLOR_INDEX = "TopPanel_ColorIndex";

    // 씬 원본 색상 저장용
    private Color originalBackgroundColor;
    private Color originalLogoColor;
    private Sprite originalSprite;
    private Image.Type originalImageType;
    private bool originalColorsSaved = false;

    private static readonly ColorPair[] colorPairs = new ColorPair[]
    {
        new ColorPair(Color.clear, Color.clear, "Original (씬 원본)"),  // 0번: 씬 원본 색상 사용
        new ColorPair(new Color(0.85f, 0.92f, 0.98f, 1f), new Color(0.70f, 0.20f, 0.40f), "Sky Pink"),
        new ColorPair(new Color(0.95f, 0.75f, 0.85f, 1f), new Color(0.25f, 0.35f, 0.60f), "Blush Blue"),
        new ColorPair(new Color(0.08f, 0.28f, 0.35f, 1f), new Color(0.75f, 0.95f, 0.92f), "Mermaidcore"),
        new ColorPair(new Color(0.98f, 0.95f, 0.75f, 1f), new Color(0.30f, 0.25f, 0.10f), "Lemon Yellow"),
        new ColorPair(new Color(0.22f, 0.15f, 0.32f, 1f), new Color(0.92f, 0.88f, 0.98f), "Midnight Purple"),
        new ColorPair(new Color(0.95f, 0.55f, 0.25f, 1f), new Color(0.35f, 0.18f, 0.08f), "Tangerine"),
        new ColorPair(new Color(0.18f, 0.18f, 0.18f, 1f), new Color(0.96f, 0.84f, 0.55f), "Charcoal Gold"),
        new ColorPair(new Color(0.05f, 0.15f, 0.28f, 1f), new Color(0.70f, 0.88f, 0.98f), "Deep Ocean"),
        new ColorPair(new Color(0.32f, 0.48f, 0.45f, 1f), new Color(0.98f, 0.95f, 0.85f), "Smoky Jade"),
        new ColorPair(new Color(0.38f, 0.26f, 0.20f, 1f), new Color(0.98f, 0.92f, 0.78f), "Mocha Mousse"),
        new ColorPair(new Color(0.92f, 0.30f, 0.30f, 1f), new Color(0.98f, 0.95f, 0.95f), "Cherry Red"),
        new ColorPair(new Color(0.22f, 0.24f, 0.12f, 1f), new Color(0.96f, 0.98f, 0.88f), "Dark Olive"),
        new ColorPair(new Color(0.90f, 0.98f, 0.95f, 1f), new Color(0.12f, 0.35f, 0.30f), "Mint Cream"),
        new ColorPair(new Color(0.98f, 0.88f, 0.80f, 1f), new Color(0.50f, 0.25f, 0.15f), "Peach Blush"),
        new ColorPair(new Color(0.92f, 0.88f, 0.96f, 1f), new Color(0.30f, 0.20f, 0.40f), "Lavender Mist"),
        new ColorPair(new Color(0.18f, 0.38f, 0.22f, 1f), new Color(0.75f, 0.60f, 0.30f), "Forest Clay")
    };

    // 밝은 배경 인덱스 (miniUsernameText 검정색 적용)
    private static readonly int[] lightBackgroundIndices = { 1, 2, 4, 13, 14, 15 };

    private void Awake()
    {
        AutoConnectReferences();
        SaveOriginalColors();
    }

    private void Start()
    {
        LoadSavedColorIndex();
        ApplyCurrentColor();

#if UNITY_ANDROID && !UNITY_EDITOR
        // SystemUIManager보다 늦게 실행되도록 딜레이 후 재적용
        // (SystemUIManager가 상태바 투명 설정 후 아이콘 색상이 리셋될 수 있음)
        StartCoroutine(ReapplyStatusBarStyleDelayed());
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private System.Collections.IEnumerator ReapplyStatusBarStyleDelayed()
    {
        yield return new WaitForSeconds(1.0f);
        ApplyStatusBarIconColor();
    }
#endif

    /// <summary>
    /// 현재 배경색에 맞게 상태바 아이콘 색상 재적용
    /// </summary>
    private void ApplyStatusBarIconColor()
    {
        if (currentColorIndex == 0)
        {
            // 원본 색상일 때 배경 밝기 자동 감지
            if (backgroundImage != null)
            {
                Color bgColor = backgroundImage.color;
                float luminance = 0.299f * bgColor.r + 0.587f * bgColor.g + 0.114f * bgColor.b;
                bool isLight = luminance > 0.5f;
                UpdateStatusBarStyle(!isLight); // 밝은 배경 → 검정 아이콘
            }
            else
            {
                UpdateStatusBarStyle(true); // 기본: 흰색 아이콘
            }
        }
        else
        {
            bool isLightBackground = System.Array.IndexOf(lightBackgroundIndices, currentColorIndex) >= 0;
            UpdateStatusBarStyle(!isLightBackground);
        }
    }

    /// <summary>
    /// 씬 원본 색상 저장 (0번 선택 시 복원용)
    /// </summary>
    private void SaveOriginalColors()
    {
        if (originalColorsSaved) return;

        if (backgroundImage != null)
        {
            originalBackgroundColor = backgroundImage.color;
            originalSprite = backgroundImage.sprite;
            originalImageType = backgroundImage.type;
        }

        if (logoImage != null)
        {
            originalLogoColor = logoImage.color;
        }

        originalColorsSaved = true;
    }

    private void AutoConnectReferences()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (logoImage == null)
        {
            Image[] childImages = GetComponentsInChildren<Image>();
            foreach (var img in childImages)
            {
                if (img != backgroundImage && img.transform != transform)
                {
                    logoImage = img;
                    break;
                }
            }
        }
    }

    private void LoadSavedColorIndex()
    {
        currentColorIndex = PlayerPrefs.GetInt(PREF_COLOR_INDEX, 0);
        if (currentColorIndex < 0 || currentColorIndex >= colorPairs.Length)
            currentColorIndex = 0;
    }

    private void SaveColorIndex()
    {
        PlayerPrefs.SetInt(PREF_COLOR_INDEX, currentColorIndex);
        PlayerPrefs.Save();
    }

    private void ApplyCurrentColor()
    {
        if (currentColorIndex < 0 || currentColorIndex >= colorPairs.Length)
            return;

        // 0번: 씬 원본 색상 복원
        if (currentColorIndex == 0)
        {
            if (backgroundImage != null && originalColorsSaved)
            {
                backgroundImage.sprite = originalSprite;
                backgroundImage.type = originalImageType;
                backgroundImage.color = originalBackgroundColor;
            }

            if (logoImage != null && originalColorsSaved)
                logoImage.color = originalLogoColor;

            // 0번(원본)일 때 miniUsernameText 흰색, status bar 흰색 아이콘
            UpdateMiniUsernameTextColor(Color.white);
            UpdateStatusBarStyle(true);
            return;
        }

        // 1-16번: 코드에서 정의한 색상 적용
        ColorPair pair = colorPairs[currentColorIndex];

        if (backgroundImage != null)
        {
            // 스프라이트 제거하고 순수 색상만 사용 (반투명 스프라이트 문제 방지)
            backgroundImage.sprite = null;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.color = pair.backgroundColor;
        }

        if (logoImage != null)
            logoImage.color = pair.textColor;

        // 밝은 배경일 때 miniUsernameText 검정색, 아니면 흰색
        bool isLightBackground = System.Array.IndexOf(lightBackgroundIndices, currentColorIndex) >= 0;
        UpdateMiniUsernameTextColor(isLightBackground ? Color.black : Color.white);
        // 밝은 배경 → 검정 아이콘, 어두운 배경 → 흰색 아이콘 (iOS + Android)
        UpdateStatusBarStyle(!isLightBackground);
    }

    /// <summary>
    /// miniUsernameText 색상 업데이트 (Login 텍스트)
    /// </summary>
    private void UpdateMiniUsernameTextColor(Color color)
    {
        if (ProfileManager.Instance != null && ProfileManager.Instance.miniUsernameText != null)
        {
            ProfileManager.Instance.miniUsernameText.color = color;
        }
    }

    /// <summary>
    /// Status Bar 아이콘 색상 업데이트 (iOS + Android)
    /// </summary>
    /// <param name="lightContent">true=흰색 아이콘(어두운 배경), false=검정 아이콘(밝은 배경)</param>
    private void UpdateStatusBarStyle(bool lightContent)
    {
#if UNITY_IOS && !UNITY_EDITOR
        _SetStatusBarStyle(lightContent);
#elif UNITY_ANDROID && !UNITY_EDITOR
        SetAndroidStatusBarStyle(lightContent);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private const int SYSTEM_UI_FLAG_LIGHT_STATUS_BAR = 0x2000;
    private const int APPEARANCE_LIGHT_STATUS_BARS = 8;

    private void SetAndroidStatusBarStyle(bool lightContent)
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var window = activity.Call<AndroidJavaObject>("getWindow");
                var decorView = window.Call<AndroidJavaObject>("getDecorView");

                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    // API 30+ (Android 11): WindowInsetsController 사용
                    AndroidJavaClass buildClass = new AndroidJavaClass("android.os.Build$VERSION");
                    int sdkInt = buildClass.GetStatic<int>("SDK_INT");

                    if (sdkInt >= 30)
                    {
                        try
                        {
                            AndroidJavaObject insetsController = window.Call<AndroidJavaObject>("getInsetsController");
                            if (insetsController != null)
                            {
                                // 상태바 강제 표시
                                AndroidJavaClass insetsType = new AndroidJavaClass("android.view.WindowInsets$Type");
                                int statusBarsType = insetsType.CallStatic<int>("statusBars");
                                insetsController.Call("show", statusBarsType);

                                if (lightContent)
                                {
                                    // 흰색 아이콘 (어두운 배경) - LIGHT 플래그 제거
                                    insetsController.Call("setSystemBarsAppearance", 0, APPEARANCE_LIGHT_STATUS_BARS);
                                }
                                else
                                {
                                    // 검정 아이콘 (밝은 배경) - LIGHT 플래그 추가
                                    insetsController.Call("setSystemBarsAppearance", APPEARANCE_LIGHT_STATUS_BARS, APPEARANCE_LIGHT_STATUS_BARS);
                                }
                            }
                        }
                        catch (System.Exception) { /* fallback to legacy */ }
                    }

                    // API 29+: 대비 강제 scrim 비활성화 (블랙바 원인 제거)
                    if (sdkInt >= 29)
                    {
                        try
                        {
                            window.Call("setStatusBarContrastEnforced", false);
                        }
                        catch (System.Exception) { /* API 29 미만 폴백 */ }
                    }

                    // 레거시 API (호환성 유지)
                    int currentFlags = decorView.Call<int>("getSystemUiVisibility");

                    if (lightContent)
                    {
                        currentFlags &= ~SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
                    }
                    else
                    {
                        currentFlags |= SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
                    }

                    decorView.Call("setSystemUiVisibility", currentFlags);
                }));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TopPanelColorChanger] Android status bar 색상 변경 실패: {e.Message}");
        }
    }
#endif

    public void NextColor()
    {
        currentColorIndex = (currentColorIndex + 1) % colorPairs.Length;
        ApplyCurrentColor();
        SaveColorIndex();
    }

    public void PreviousColor()
    {
        currentColorIndex = (currentColorIndex - 1 + colorPairs.Length) % colorPairs.Length;
        ApplyCurrentColor();
        SaveColorIndex();
    }

    public void SetColorIndex(int index)
    {
        if (index < 0 || index >= colorPairs.Length)
            return;

        currentColorIndex = index;
        ApplyCurrentColor();
        SaveColorIndex();
    }

    public void ResetToDefault()
    {
        currentColorIndex = 0;
        ApplyCurrentColor();
        SaveColorIndex();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        NextColor();
    }

    public int GetColorCount() => colorPairs.Length;
    public int GetCurrentIndex() => currentColorIndex;

    public string GetCurrentColorName()
    {
        if (currentColorIndex >= 0 && currentColorIndex < colorPairs.Length)
            return colorPairs[currentColorIndex].name;
        return "Unknown";
    }
}

[System.Serializable]
public struct ColorPair
{
    public Color backgroundColor;
    public Color textColor;
    public string name;

    public ColorPair(Color bg, Color text, string colorName)
    {
        backgroundColor = bg;
        textColor = text;
        name = colorName;
    }
}
