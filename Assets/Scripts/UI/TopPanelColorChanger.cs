using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TopPanelColorChanger : MonoBehaviour, IPointerClickHandler
{
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

            // 0번(원본)일 때 miniUsernameText 흰색
            UpdateMiniUsernameTextColor(Color.white);
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
