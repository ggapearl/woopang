using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class AddPlaceLocalizer : MonoBehaviour
{
    [Header("UI Text Fields")]
    [SerializeField] private Text addPlaceTitle;        // "Add Place" 제목
    [SerializeField] private Text nameLabel;            // "Name" 라벨
    [SerializeField] private Text coordinateLabel;      // "Coordinate" 라벨
    [SerializeField] private Text coordinateSubtext;    // "You need to enable location service" 설명
    [SerializeField] private Text addPlaceLogoLabel;    // "Add Place Logo" 라벨
    [SerializeField] private Text addPlaceImgsLabel;    // "Add Place Imgs" 라벨
    [SerializeField] private Text petFriendlyLabel;     // "Pet Friendly" 라벨
    [SerializeField] private Text separatedRestroomLabel; // "Separated Restroom" 라벨
    [SerializeField] private Text instagramIdLabel;     // "Instagram ID" 라벨

    private string currentLanguage;

    // 다국어 텍스트 딕셔너리
    private Dictionary<string, LocalizedAddPlaceTexts> localizedTexts = new Dictionary<string, LocalizedAddPlaceTexts>()
    {
        ["en"] = new LocalizedAddPlaceTexts
        {
            addPlaceTitle = "Add Place",
            nameLabel = "Name",
            coordinateLabel = "Coordinate",
            coordinateSubtext = "You need to enable location service",
            addPlaceLogoLabel = "Add Place Logo",
            addPlaceImgsLabel = "Add Place Imgs",
            petFriendlyLabel = "Pet Friendly",
            separatedRestroomLabel = "Separated Restroom",
            instagramIdLabel = "Instagram ID"
        },
        ["ko"] = new LocalizedAddPlaceTexts
        {
            addPlaceTitle = "장소 추가",
            nameLabel = "이름",
            coordinateLabel = "좌표",
            coordinateSubtext = "위치 서비스를 활성화해야 합니다",
            addPlaceLogoLabel = "장소 로고 추가",
            addPlaceImgsLabel = "장소 이미지 추가",
            petFriendlyLabel = "반려동물 동반 가능",
            separatedRestroomLabel = "분리된 화장실",
            instagramIdLabel = "인스타그램 ID"
        },
        ["ja"] = new LocalizedAddPlaceTexts
        {
            addPlaceTitle = "場所を追加",
            nameLabel = "名前",
            coordinateLabel = "座標",
            coordinateSubtext = "位置情報サービスを有効にする必要があります",
            addPlaceLogoLabel = "場所のロゴを追加",
            addPlaceImgsLabel = "場所の画像を追加",
            petFriendlyLabel = "ペット可",
            separatedRestroomLabel = "分離されたトイレ",
            instagramIdLabel = "インスタグラム ID"
        },
        ["zh"] = new LocalizedAddPlaceTexts
        {
            addPlaceTitle = "添加地点",
            nameLabel = "名称",
            coordinateLabel = "坐标",
            coordinateSubtext = "您需要启用位置服务",
            addPlaceLogoLabel = "添加地点标志",
            addPlaceImgsLabel = "添加地点图片",
            petFriendlyLabel = "宠物友好",
            separatedRestroomLabel = "独立洗手间",
            instagramIdLabel = "Instagram ID"
        },
        ["es"] = new LocalizedAddPlaceTexts
        {
            addPlaceTitle = "Agregar Lugar",
            nameLabel = "Nombre",
            coordinateLabel = "Coordenada",
            coordinateSubtext = "Necesita habilitar el servicio de ubicación",
            addPlaceLogoLabel = "Agregar Logo del Lugar",
            addPlaceImgsLabel = "Agregar Imágenes del Lugar",
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
            Debug.LogError("AddPlaceLocalizer: 일부 UI 요소가 연결되지 않았습니다!");
            return;
        }

        // 텍스트 업데이트
        UpdateUI();
        
        Debug.Log($"AddPlaceLocalizer: 언어 설정 완료 - {currentLanguage}");
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
        
        if (addPlaceTitle == null) { Debug.LogWarning("addPlaceTitle is not assigned"); isValid = false; }
        if (nameLabel == null) { Debug.LogWarning("nameLabel is not assigned"); isValid = false; }
        if (coordinateLabel == null) { Debug.LogWarning("coordinateLabel is not assigned"); isValid = false; }
        if (coordinateSubtext == null) { Debug.LogWarning("coordinateSubtext is not assigned"); isValid = false; }
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

        LocalizedAddPlaceTexts texts = localizedTexts[currentLanguage];
        
        // 각 UI 텍스트 업데이트
        if (addPlaceTitle != null)
            addPlaceTitle.text = texts.addPlaceTitle;
            
        if (nameLabel != null)
            nameLabel.text = texts.nameLabel;
            
        if (coordinateLabel != null)
            coordinateLabel.text = texts.coordinateLabel;
            
        if (coordinateSubtext != null)
            coordinateSubtext.text = texts.coordinateSubtext;
            
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
                if (addPlaceTitle != null) addPlaceTitle.text = customText;
                break;
            case "name":
                if (nameLabel != null) nameLabel.text = customText;
                break;
            case "coordinate":
                if (coordinateLabel != null) coordinateLabel.text = customText;
                break;
            case "coordinatesubtext":
                if (coordinateSubtext != null) coordinateSubtext.text = customText;
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
public class LocalizedAddPlaceTexts
{
    public string addPlaceTitle;
    public string nameLabel;
    public string coordinateLabel;
    public string coordinateSubtext;
    public string addPlaceLogoLabel;
    public string addPlaceImgsLabel;
    public string petFriendlyLabel;
    public string separatedRestroomLabel;
    public string instagramIdLabel;
}