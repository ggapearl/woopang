using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ModelUploadLocalizer : MonoBehaviour
{
    [Header("UI Text Fields")]
    [SerializeField] private Text addModelTitle;           // "Add 3D Model" 제목
    [SerializeField] private Text nameLabel;               // "Name" 라벨
    [SerializeField] private Text coordinateLabel;         // "Coordinate" 라벨
    [SerializeField] private Text coordinateSubtext;       // "You need to enable location service" 설명
    [SerializeField] private Text addFileLabel;            // "Add File" 라벨
    [SerializeField] private Text addPlaceImgsLabel;       // "Add Place Imgs" 라벨
    [SerializeField] private Text petFriendlyLabel;        // "Pet Friendly" 라벨
    [SerializeField] private Text separatedRestroomLabel;  // "Separated Restroom" 라벨
    [SerializeField] private Text instagramIdLabel;       // "Instagram ID" 라벨
    [SerializeField] private Text categoryLabel;          // "Select Category" 라벨

    private string currentLanguage;

    // 다국어 텍스트 딕셔너리
    private Dictionary<string, LocalizedModelUploadTexts> localizedTexts = new Dictionary<string, LocalizedModelUploadTexts>()
    {
        ["en"] = new LocalizedModelUploadTexts
        {
            addModelTitle = "Add 3D Model",
            nameLabel = "Name",
            coordinateLabel = "Coordinate",
            coordinateSubtext = "You need to enable location service",
            addFileLabel = "Add File",
            addPlaceImgsLabel = "Add Place Imgs",
            petFriendlyLabel = "Pet Friendly",
            separatedRestroomLabel = "Separated Restroom",
            instagramIdLabel = "Instagram ID",
            categoryLabel = "Select Category"
        },
        ["ko"] = new LocalizedModelUploadTexts
        {
            addModelTitle = "3D 모델 추가",
            nameLabel = "이름",
            coordinateLabel = "좌표",
            coordinateSubtext = "위치 서비스를 활성화해야 합니다",
            addFileLabel = "파일추가",
            addPlaceImgsLabel = "장소 이미지 추가",
            petFriendlyLabel = "반려동물 동반 가능",
            separatedRestroomLabel = "분리된 화장실",
            instagramIdLabel = "인스타그램 ID",
            categoryLabel = "분류 선택"
        },
        ["ja"] = new LocalizedModelUploadTexts
        {
            addModelTitle = "3Dモデルを追加",
            nameLabel = "名前",
            coordinateLabel = "座標",
            coordinateSubtext = "位置情報サービスを有効にする必要があります",
            addFileLabel = "ファイル",
            addPlaceImgsLabel = "場所の画像を追加",
            petFriendlyLabel = "ペット可",
            separatedRestroomLabel = "分離されたトイレ",
            instagramIdLabel = "インスタグラム ID",
            categoryLabel = "分類選択"
        },
        ["zh"] = new LocalizedModelUploadTexts
        {
            addModelTitle = "添加3D模型",
            nameLabel = "名称",
            coordinateLabel = "坐标",
            coordinateSubtext = "您需要启用位置服务",
            addFileLabel = "文件",
            addPlaceImgsLabel = "添加地点图片",
            petFriendlyLabel = "宠物友好",
            separatedRestroomLabel = "独立洗手间",
            instagramIdLabel = "Instagram ID",
            categoryLabel = "选择分类"
        },
        ["es"] = new LocalizedModelUploadTexts
        {
            addModelTitle = "Agregar Modelo 3D",
            nameLabel = "Nombre",
            coordinateLabel = "Coordenada",
            coordinateSubtext = "Necesita habilitar el servicio de ubicación",
            addFileLabel = "Archivo",
            addPlaceImgsLabel = "Agregar Imágenes del Lugar",
            petFriendlyLabel = "Acepta Mascotas",
            separatedRestroomLabel = "Baño Separado",
            instagramIdLabel = "ID de Instagram",
            categoryLabel = "Elegir categoría"
        }
    };

    void Start()
    {
        // 현재 디바이스 언어 감지
        DetectDeviceLanguage();
        
        // UI 요소 검증
        if (!ValidateUIElements())
        {
            Debug.LogError("ModelUploadLocalizer: 일부 UI 요소가 연결되지 않았습니다!");
            return;
        }

        // 텍스트 업데이트
        UpdateUI();
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
        
    }

    bool ValidateUIElements()
    {
        bool isValid = true;
        
        if (addModelTitle == null) isValid = false;
        if (nameLabel == null) isValid = false;
        if (coordinateLabel == null) isValid = false;
        if (coordinateSubtext == null) isValid = false;
        if (addFileLabel == null) isValid = false;
        if (addPlaceImgsLabel == null) isValid = false;
        if (petFriendlyLabel == null) isValid = false;
        if (separatedRestroomLabel == null) isValid = false;
        if (instagramIdLabel == null) isValid = false;
        
        return isValid;
    }

    void UpdateUI()
    {
        if (!localizedTexts.ContainsKey(currentLanguage))
        {
            currentLanguage = "en"; // 지원하지 않는 언어면 영어로 fallback
        }

        LocalizedModelUploadTexts texts = localizedTexts[currentLanguage];
        
        SafeSetText(addModelTitle, texts.addModelTitle);
        SafeSetText(nameLabel, texts.nameLabel);
        SafeSetText(coordinateLabel, texts.coordinateLabel);
        SafeSetText(coordinateSubtext, texts.coordinateSubtext);
        SafeSetText(addFileLabel, texts.addFileLabel);
        SafeSetText(addPlaceImgsLabel, texts.addPlaceImgsLabel);
        SafeSetText(petFriendlyLabel, texts.petFriendlyLabel);
        SafeSetText(separatedRestroomLabel, texts.separatedRestroomLabel);
        SafeSetText(instagramIdLabel, texts.instagramIdLabel);
        SafeSetText(categoryLabel, texts.categoryLabel);
    }
    
    private void SafeSetText(Text textComponent, string text)
    {
        if (textComponent != null)
        {
            textComponent.text = text;
        }
    }

    // 언어를 수동으로 변경하고 싶을 때 사용 (선택사항)
    public void SetLanguage(string languageCode)
    {
        if (localizedTexts.ContainsKey(languageCode))
        {
            currentLanguage = languageCode;
            UpdateUI();
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
                SafeSetText(addModelTitle, customText);
                break;
            case "name":
                SafeSetText(nameLabel, customText);
                break;
            case "coordinate":
                SafeSetText(coordinateLabel, customText);
                break;
            case "coordinatesubtext":
                SafeSetText(coordinateSubtext, customText);
                break;
            case "file":
                SafeSetText(addFileLabel, customText);
                break;
            case "images":
                SafeSetText(addPlaceImgsLabel, customText);
                break;
            case "petfriendly":
                SafeSetText(petFriendlyLabel, customText);
                break;
            case "restroom":
                SafeSetText(separatedRestroomLabel, customText);
                break;
            case "instagram":
                SafeSetText(instagramIdLabel, customText);
                break;
            case "category":
                SafeSetText(categoryLabel, customText);
                break;
            default:
                break;
        }
    }
}

[System.Serializable]
public class LocalizedModelUploadTexts
{
    [Header("Labels")]
    public string addModelTitle;
    public string nameLabel;
    public string coordinateLabel;
    public string coordinateSubtext;
    public string addFileLabel;
    public string addPlaceImgsLabel;
    public string petFriendlyLabel;
    public string separatedRestroomLabel;
    public string instagramIdLabel;
    public string categoryLabel;
}