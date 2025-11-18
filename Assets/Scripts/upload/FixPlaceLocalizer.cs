using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class FixPlaceLocalizer : MonoBehaviour
{
    [Header("UI Text Fields")]
    [SerializeField] private Text fixPlaceTitle;        // "Fix Place" 제목
    [SerializeField] private Text changedNameLabel;     // "Changed Name" 라벨
    [SerializeField] private Text descriptionLabel;     // "Description" 라벨
    [SerializeField] private Text addPlaceLogoLabel;    // "Add Place Logo" 라벨
    [SerializeField] private Text addPlaceImgsLabel;    // "Add Place Imgs" 라벨
    [SerializeField] private Text petFriendlyLabel;     // "Pet Friendly" 라벨
    [SerializeField] private Text separatedRestroomLabel; // "Separated Restroom" 라벨
    [SerializeField] private Text instagramIdLabel;     // "Instagram ID" 라벨

    private string currentLanguage;

    // 다국어 텍스트 딕셔너리
    private Dictionary<string, LocalizedFixPlaceTexts> localizedTexts = new Dictionary<string, LocalizedFixPlaceTexts>()
    {
        ["en"] = new LocalizedFixPlaceTexts
        {
            fixPlaceTitle = "Fix Place",
            changedNameLabel = "Changing Name",
            descriptionLabel = "Description",
            addPlaceLogoLabel = "Add Place Logo",
            addPlaceImgsLabel = "Add Place Imgs",
            petFriendlyLabel = "Pet Friendly",
            separatedRestroomLabel = "Separated Restroom",
            instagramIdLabel = "Instagram ID"
        },
        ["ko"] = new LocalizedFixPlaceTexts
        {
            fixPlaceTitle = "장소 수정",
            changedNameLabel = "수정할 이름",
            descriptionLabel = "수정할 내용",
            addPlaceLogoLabel = "장소 로고 추가",
            addPlaceImgsLabel = "장소 이미지 추가",
            petFriendlyLabel = "반려동물 동반 가능",
            separatedRestroomLabel = "분리된 화장실",
            instagramIdLabel = "인스타그램 ID"
        },
        ["ja"] = new LocalizedFixPlaceTexts
        {
            fixPlaceTitle = "場所を修正",
            changedNameLabel = "修正する名前",
            descriptionLabel = "修正する内容",
            addPlaceLogoLabel = "場所のロゴを追加",
            addPlaceImgsLabel = "場所の画像を追加",
            petFriendlyLabel = "ペット可",
            separatedRestroomLabel = "分離されたトイレ",
            instagramIdLabel = "インスタグラム ID"
        },
        ["zh"] = new LocalizedFixPlaceTexts
        {
            fixPlaceTitle = "修改地点",
            changedNameLabel = "修改名称",
            descriptionLabel = "修改内容",
            addPlaceLogoLabel = "添加地点标志",
            addPlaceImgsLabel = "添加地点图片",
            petFriendlyLabel = "宠物友好",
            separatedRestroomLabel = "独立洗手间",
            instagramIdLabel = "Instagram ID"
        },
        ["es"] = new LocalizedFixPlaceTexts
        {
            fixPlaceTitle = "Editar Lugar",
            changedNameLabel = "Cambiar Nombre",
            descriptionLabel = "Cambiar Descripción",
            addPlaceLogoLabel = "Agregar Logo",
            addPlaceImgsLabel = "Agregar Fotos",
            petFriendlyLabel = "Acepta Mascotas",
            separatedRestroomLabel = "Baño Separado",
            instagramIdLabel = "ID de Instagram"
        }
    };

    void Start()
    {
        // 현재 디바이스 언어 감지
        DetectDeviceLanguage();
        
        // UI 요소 검증
        if (!ValidateUIElements())
        {
            Debug.LogError("FixPlaceLocalizer: 일부 UI 요소가 연결되지 않았습니다!");
            return;
        }

        // 텍스트 업데이트
        UpdateUI();
        
        Debug.Log($"FixPlaceLocalizer: 언어 설정 완료 - {currentLanguage}");
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
                currentLanguage = "en"; // 기본값은 영어
                break;
        }
        
        Debug.Log($"Detected language: {currentLanguage} (Device: {deviceLanguage})");
    }

    bool ValidateUIElements()
    {
        bool isValid = true;
        
        if (fixPlaceTitle == null) { Debug.LogWarning("fixPlaceTitle is not assigned"); isValid = false; }
        if (changedNameLabel == null) { Debug.LogWarning("changedNameLabel is not assigned"); isValid = false; }
        if (descriptionLabel == null) { Debug.LogWarning("descriptionLabel is not assigned"); isValid = false; }
        if (addPlaceLogoLabel == null) { Debug.LogWarning("addPlaceLogoLabel is not assigned"); isValid = false; }
        if (addPlaceImgsLabel == null) { Debug.LogWarning("addPlaceImgsLabel is not assigned"); isValid = false; }
        if (petFriendlyLabel == null) { Debug.LogWarning("petFriendlyLabel is not assigned"); isValid = false; }
        if (separatedRestroomLabel == null) { Debug.LogWarning("separatedRestroomLabel is not assigned"); isValid = false; }
        if (instagramIdLabel == null) { Debug.LogWarning("instagramIdLabel is not assigned"); isValid = false; }
        
        return isValid;
    }

    void UpdateUI()
    {
        if (!localizedTexts.ContainsKey(currentLanguage))
        {
            currentLanguage = "en"; // 지원하지 않는 언어면 영어로 fallback
        }

        LocalizedFixPlaceTexts texts = localizedTexts[currentLanguage];
        
        // 각 UI 텍스트 업데이트
        if (fixPlaceTitle != null)
            fixPlaceTitle.text = texts.fixPlaceTitle;
            
        if (changedNameLabel != null)
            changedNameLabel.text = texts.changedNameLabel;
            
        if (descriptionLabel != null)
            descriptionLabel.text = texts.descriptionLabel;
            
        if (addPlaceLogoLabel != null)
            addPlaceLogoLabel.text = texts.addPlaceLogoLabel;
            
        if (addPlaceImgsLabel != null)
            addPlaceImgsLabel.text = texts.addPlaceImgsLabel;
            
        if (petFriendlyLabel != null)
            petFriendlyLabel.text = texts.petFriendlyLabel;
            
        if (separatedRestroomLabel != null)
            separatedRestroomLabel.text = texts.separatedRestroomLabel;
            
        if (instagramIdLabel != null)
            instagramIdLabel.text = texts.instagramIdLabel;
    }

    // 언어를 수동으로 변경하고 싶을 때 사용 (선택사항)
    public void SetLanguage(string languageCode)
    {
        if (localizedTexts.ContainsKey(languageCode))
        {
            currentLanguage = languageCode;
            UpdateUI();
            Debug.Log($"Language changed to: {languageCode}");
        }
        else
        {
            Debug.LogWarning($"Language '{languageCode}' is not supported");
        }
    }

    // 현재 설정된 언어 반환
    public string GetCurrentLanguage()
    {
        return currentLanguage;
    }

    // 특정 텍스트만 업데이트하고 싶을 때 사용
    public void UpdateSpecificText(string textKey, string customText)
    {
        switch (textKey.ToLower())
        {
            case "title":
                if (fixPlaceTitle != null) fixPlaceTitle.text = customText;
                break;
            case "changedname":
                if (changedNameLabel != null) changedNameLabel.text = customText;
                break;
            case "description":
                if (descriptionLabel != null) descriptionLabel.text = customText;
                break;
            case "logo":
                if (addPlaceLogoLabel != null) addPlaceLogoLabel.text = customText;
                break;
            case "images":
                if (addPlaceImgsLabel != null) addPlaceImgsLabel.text = customText;
                break;
            case "petfriendly":
                if (petFriendlyLabel != null) petFriendlyLabel.text = customText;
                break;
            case "restroom":
                if (separatedRestroomLabel != null) separatedRestroomLabel.text = customText;
                break;
            case "instagram":
                if (instagramIdLabel != null) instagramIdLabel.text = customText;
                break;
            default:
                Debug.LogWarning($"Unknown text key: {textKey}");
                break;
        }
    }
}

[System.Serializable]
public class LocalizedFixPlaceTexts
{
    public string fixPlaceTitle;
    public string changedNameLabel;
    public string descriptionLabel;
    public string addPlaceLogoLabel;
    public string addPlaceImgsLabel;
    public string petFriendlyLabel;
    public string separatedRestroomLabel;
    public string instagramIdLabel;
}