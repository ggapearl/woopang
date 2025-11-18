using System.Collections.Generic;
using UnityEngine;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }
    
    private Dictionary<string, Dictionary<string, string>> localizedTexts;
    private string currentLanguage;
    
    // 지원하는 언어 목록
    private readonly string[] supportedLanguages = { "en", "ko", "zh", "ja", "es" };
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Initialize()
    {
        currentLanguage = GetDeviceLanguage();
        Debug.Log($"[LocalizationManager] Current language: {currentLanguage}");
        InitializeTexts();
    }
    
    private string GetDeviceLanguage()
    {
        string deviceLang = "en"; // 기본값: 영어
        
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                deviceLang = "ko";
                break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                deviceLang = "zh";
                break;
            case SystemLanguage.Japanese:
                deviceLang = "ja";
                break;
            case SystemLanguage.Spanish:
                deviceLang = "es";
                break;
            case SystemLanguage.English:
            default:
                deviceLang = "en";
                break;
        }
        
        // 지원하는 언어인지 확인
        bool isSupported = false;
        foreach (string lang in supportedLanguages)
        {
            if (lang == deviceLang)
            {
                isSupported = true;
                break;
            }
        }
        
        return isSupported ? deviceLang : "en"; // 지원하지 않는 언어면 영어로 설정
    }
    
    private void InitializeTexts()
    {
        localizedTexts = new Dictionary<string, Dictionary<string, string>>()
        {
            // 공통 메시지
            ["photo_selection_failed"] = new Dictionary<string, string>()
            {
                ["en"] = "Photo selection failed!",
                ["ko"] = "사진 선택에 실패했습니다!",
                ["zh"] = "照片选择失败！",
                ["ja"] = "写真の選択に失敗しました！",
                ["es"] = "¡Error al seleccionar la foto!"
            },
            ["loading_main_photo"] = new Dictionary<string, string>()
            {
                ["en"] = "Loading main photo...",
                ["ko"] = "메인 사진 로딩 중...",
                ["zh"] = "正在加载主照片...",
                ["ja"] = "メイン写真を読み込み中...",
                ["es"] = "Cargando foto principal..."
            },
            ["loading_sub_photos"] = new Dictionary<string, string>()
            {
                ["en"] = "Loading sub photos...",
                ["ko"] = "서브 사진 로딩 중...",
                ["zh"] = "正在加载子照片...",
                ["ja"] = "サブ写真を読み込み中...",
                ["es"] = "Cargando fotos secundarias..."
            },
            ["main_photo_crop_failed"] = new Dictionary<string, string>()
            {
                ["en"] = "⚠ Main photo cropping failed!",
                ["ko"] = "⚠ 메인 사진 자르기 실패!",
                ["zh"] = "⚠ 主照片裁剪失败！",
                ["ja"] = "⚠ メイン写真のトリミングに失敗しました！",
                ["es"] = "⚠ ¡Error al recortar la foto principal!"
            },
            ["sub_photo_load_failed"] = new Dictionary<string, string>()
            {
                ["en"] = "⚠ Sub photo loading failed!",
                ["ko"] = "⚠ 서브 사진 로드 실패!",
                ["zh"] = "⚠ 子照片加载失败！",
                ["ja"] = "⚠ サブ写真の読み込みに失敗しました！",
                ["es"] = "⚠ ¡Error al cargar la foto secundaria!"
            },
            ["max_sub_photos_exceeded"] = new Dictionary<string, string>()
            {
                ["en"] = "Maximum 10 sub photos allowed!",
                ["ko"] = "서브 사진은 최대 10장까지 가능합니다!",
                ["zh"] = "最多允许10张子照片！",
                ["ja"] = "サブ写真は最大10枚まで可能です！",
                ["es"] = "¡Máximo 10 fotos secundarias permitidas!"
            },
            ["sub_photos_reset"] = new Dictionary<string, string>()
            {
                ["en"] = "Sub photos have been reset!",
                ["ko"] = "서브 사진이 초기화되었습니다!",
                ["zh"] = "子照片已重置！",
                ["ja"] = "サブ写真がリセットされました！",
                ["es"] = "¡Las fotos secundarias han sido reiniciadas!"
            },
            ["submitting_countdown"] = new Dictionary<string, string>()
            {
                ["en"] = "Submitting... {0}",
                ["ko"] = "제출 중... {0}",
                ["zh"] = "提交中... {0}",
                ["ja"] = "送信中... {0}",
                ["es"] = "Enviando... {0}"
            },
            ["request_timeout"] = new Dictionary<string, string>()
            {
                ["en"] = "Request failed: Timeout",
                ["ko"] = "요청 실패: 시간 초과",
                ["zh"] = "请求失败：超时",
                ["ja"] = "リクエスト失敗：タイムアウト",
                ["es"] = "Solicitud fallida: Tiempo agotado"
            },
            ["server_error"] = new Dictionary<string, string>()
            {
                ["en"] = "Request failed: Server error",
                ["ko"] = "요청 실패: 서버 오류",
                ["zh"] = "请求失败：服务器错误",
                ["ja"] = "リクエスト失敗：サーバーエラー",
                ["es"] = "Solicitud fallida: Error del servidor"
            },
            ["main_photo_upload_failed"] = new Dictionary<string, string>()
            {
                ["en"] = "Main photo upload failed: Data corrupted",
                ["ko"] = "메인 사진 업로드 실패: 데이터 손상",
                ["zh"] = "主照片上传失败：数据损坏",
                ["ja"] = "メイン写真のアップロードに失敗：データ破損",
                ["es"] = "Error al subir la foto principal: Datos corruptos"
            },
            ["sub_photo_upload_failed"] = new Dictionary<string, string>()
            {
                ["en"] = "Sub photo #{0} upload failed: Data corrupted",
                ["ko"] = "서브 사진 #{0} 업로드 실패: 데이터 손상",
                ["zh"] = "子照片#{0}上传失败：数据损坏",
                ["ja"] = "サブ写真#{0}のアップロードに失敗：データ破損",
                ["es"] = "Error al subir la foto secundaria #{0}: Datos corruptos"
            },
            
            // CubeUploadManager 전용 메시지
            ["enter_name"] = new Dictionary<string, string>()
            {
                ["en"] = "Please enter your name!",
                ["ko"] = "이름을 입력해주세요!",
                ["zh"] = "请输入您的姓名！",
                ["ja"] = "お名前を入力してください！",
                ["es"] = "¡Por favor ingresa tu nombre!"
            },
            ["enter_instagram_id"] = new Dictionary<string, string>()
            {
                ["en"] = "Please enter Instagram ID!",
                ["ko"] = "Instagram ID를 입력해주세요!",
                ["zh"] = "请输入Instagram ID！",
                ["ja"] = "Instagram IDを入力してください！",
                ["es"] = "¡Por favor ingresa el ID de Instagram!"
            },
            ["upload_logo_photo"] = new Dictionary<string, string>()
            {
                ["en"] = "Please upload a logo photo!",
                ["ko"] = "로고 사진을 업로드해주세요!",
                ["zh"] = "请上传标志照片！",
                ["ja"] = "ロゴ写真をアップロードしてください！",
                ["es"] = "¡Por favor sube una foto del logo!"
            },
            ["upload_min_one_photo"] = new Dictionary<string, string>()
            {
                ["en"] = "Please upload at least 1 photo!",
                ["ko"] = "최소 1장의 사진을 업로드해주세요!",
                ["zh"] = "请至少上传1张照片！",
                ["ja"] = "最低1枚の写真をアップロードしてください！",
                ["es"] = "¡Por favor sube al menos 1 foto!"
            },
            ["instagram_id_invalid"] = new Dictionary<string, string>()
            {
                ["en"] = "Instagram ID cannot contain special characters!",
                ["ko"] = "Instagram ID에 특수문자는 사용할 수 없습니다!",
                ["zh"] = "Instagram ID不能包含特殊字符！",
                ["ja"] = "Instagram IDに特殊文字は使用できません！",
                ["es"] = "¡El ID de Instagram no puede contener caracteres especiales!"
            },
            ["enable_location_service"] = new Dictionary<string, string>()
            {
                ["en"] = "Please enable location service!",
                ["ko"] = "위치서비스를 활성화해주세요!",
                ["zh"] = "请启用位置服务！",
                ["ja"] = "位置サービスを有効にしてください！",
                ["es"] = "¡Por favor habilita el servicio de ubicación!"
            },
            ["upload_success"] = new Dictionary<string, string>()
            {
                ["en"] = "Upload Successful!",
                ["ko"] = "업로드 성공!",
                ["zh"] = "上传成功！",
                ["ja"] = "アップロード成功！",
                ["es"] = "¡Subida exitosa!"
            },
            ["loading_location"] = new Dictionary<string, string>()
            {
                ["en"] = "Loading location data...",
                ["ko"] = "위치 데이터 로드 중...",
                ["zh"] = "正在加载位置数据...",
                ["ja"] = "位置データを読み込み中...",
                ["es"] = "Cargando datos de ubicación..."
            },
            ["no_location_data"] = new Dictionary<string, string>()
            {
                ["en"] = "No location data",
                ["ko"] = "위치 데이터 없음",
                ["zh"] = "无位置数据",
                ["ja"] = "位置データなし",
                ["es"] = "Sin datos de ubicación"
            },
            
            // CubeDataFixManager 전용 메시지
            ["no_object_selected"] = new Dictionary<string, string>()
            {
                ["en"] = "No object selected! Please double-tap an object to select it.",
                ["ko"] = "오브젝트가 선택되지 않았습니다! 더블 터치로 오브젝트를 선택해주세요.",
                ["zh"] = "未选择对象！请双击选择一个对象。",
                ["ja"] = "オブジェクトが選択されていません！ダブルタップでオブジェクトを選択してください。",
                ["es"] = "¡Ningún objeto seleccionado! Toca dos veces para seleccionar un objeto."
            },
            ["valid_id_not_found"] = new Dictionary<string, string>()
            {
                ["en"] = "Valid ID not found!",
                ["ko"] = "유효한 ID를 찾을 수 없습니다!",
                ["zh"] = "找不到有效的ID！",
                ["ja"] = "有効なIDが見つかりません！",
                ["es"] = "¡No se encontró un ID válido!"
            },
            ["enter_description"] = new Dictionary<string, string>()
            {
                ["en"] = "Please enter a description!",
                ["ko"] = "설명을 입력해주세요!",
                ["zh"] = "请输入说明！",
                ["ja"] = "説明を入力してください！",
                ["es"] = "¡Por favor ingresa una descripción!"
            },
            ["enter_place_name"] = new Dictionary<string, string>()
            {
                ["en"] = "Please enter the place name!",
                ["ko"] = "장소 이름을 입력해주세요!",
                ["zh"] = "请输入地点名称！",
                ["ja"] = "場所名を入力してください！",
                ["es"] = "¡Por favor ingresa el nombre del lugar!"
            },
            ["upload_main_photo"] = new Dictionary<string, string>()
            {
                ["en"] = "Please upload a main photo!",
                ["ko"] = "메인 사진을 업로드해주세요!",
                ["zh"] = "请上传主照片！",
                ["ja"] = "メイン写真をアップロードしてください！",
                ["es"] = "¡Por favor sube una foto principal!"
            },
            ["fix_success"] = new Dictionary<string, string>()
            {
                ["en"] = "Update request submitted successfully!",
                ["ko"] = "수정 요청이 완료되었습니다!",
                ["zh"] = "修改请求已成功提交！",
                ["ja"] = "修正リクエストが正常に送信されました！",
                ["es"] = "¡Solicitud de actualización enviada exitosamente!"
            },
            
            // RemoveRequest 전용 메시지
            ["delete_success"] = new Dictionary<string, string>()
            {
                ["en"] = "Delete request submitted successfully!",
                ["ko"] = "삭제 요청이 완료되었습니다!",
                ["zh"] = "删除请求已成功提交！",
                ["ja"] = "削除リクエストが正常に送信されました！",
                ["es"] = "¡Solicitud de eliminación enviada exitosamente!"
            },
            
            // LoadingManager AR 환경 메시지들
            ["ar_environment_optimizing"] = new Dictionary<string, string>()
            {
                ["en"] = "Optimizing AR environment..",
                ["ko"] = "AR 환경 최적화 중..",
                ["zh"] = "正在优化AR环境..",
                ["ja"] = "AR環境を最適化中..",
                ["es"] = "Optimizando entorno AR.."
            },
            ["ar_environment_ready"] = new Dictionary<string, string>()
            {
                ["en"] = "AR environment ready!",
                ["ko"] = "AR 환경 준비 완료!",
                ["zh"] = "AR环境准备就绪！",
                ["ja"] = "AR環境準備完了！",
                ["es"] = "¡Entorno AR listo!"
            },
            ["ar_tracking_initializing"] = new Dictionary<string, string>()
            {
                ["en"] = "Initializing AR tracking..",
                ["ko"] = "AR 트래킹 초기화 중..",
                ["zh"] = "正在初始化AR跟踪..",
                ["ja"] = "ARトラッキングを初期化中..",
                ["es"] = "Inicializando seguimiento AR.."
            },
            ["ar_session_starting"] = new Dictionary<string, string>()
            {
                ["en"] = "Starting AR session..",
                ["ko"] = "AR 세션 시작 중..",
                ["zh"] = "正在启动AR会话..",
                ["ja"] = "ARセッションを開始中..",
                ["es"] = "Iniciando sesión AR.."
            },
            ["ar_detecting_surfaces"] = new Dictionary<string, string>()
            {
                ["en"] = "Detecting surfaces..",
                ["ko"] = "표면 감지 중..",
                ["zh"] = "正在检测表面..",
                ["ja"] = "表面を検出中..",
                ["es"] = "Detectando superficies.."
            },
            ["ar_calibrating"] = new Dictionary<string, string>()
            {
                ["en"] = "Calibrating AR..",
                ["ko"] = "AR 보정 중..",
                ["zh"] = "正在校准AR..",
                ["ja"] = "ARを校正中..",
                ["es"] = "Calibrando AR.."
            },
            ["ar_loading_content"] = new Dictionary<string, string>()
            {
                ["en"] = "Loading AR content..",
                ["ko"] = "AR 콘텐츠 로딩 중..",
                ["zh"] = "正在加载AR内容..",
                ["ja"] = "ARコンテンツを読み込み中..",
                ["es"] = "Cargando contenido AR.."
            },
            
            // AR 환경 문제별 메시지
            ["ar_too_dark"] = new Dictionary<string, string>()
            {
                ["en"] = "Please move to brighter area..",
                ["ko"] = "밝은 곳으로 이동해주세요..",
                ["zh"] = "请移动到明亮的地方..",
                ["ja"] = "明るい場所に移動してください..",
                ["es"] = "Por favor, muévete a un lugar más brillante.."
            },
            ["ar_no_features"] = new Dictionary<string, string>()
            {
                ["en"] = "Point at textured surfaces..",
                ["ko"] = "패턴이 있는 표면을 비춰주세요..",
                ["zh"] = "请对准有纹理的表面..",
                ["ja"] = "テクスチャのある表面を照らしてください..",
                ["es"] = "Apunta a superficies con textura.."
            },
            ["ar_insufficient_light"] = new Dictionary<string, string>()
            {
                ["en"] = "Turn on lights or move to bright area..",
                ["ko"] = "조명을 켜거나 밝은 곳으로..",
                ["zh"] = "打开灯光或移动到明亮处..",
                ["ja"] = "ライトをつけるか明るい場所へ..",
                ["es"] = "Enciende las luces o ve a un lugar brillante.."
            },
            ["ar_camera_covered"] = new Dictionary<string, string>()
            {
                ["en"] = "Please uncover the camera..",
                ["ko"] = "카메라에서 손을 떼주세요..",
                ["zh"] = "请移开遮挡相机的手..",
                ["ja"] = "カメラから手を離してください..",
                ["es"] = "Por favor, descubre la cámara.."
            },
            
            // AR 환경 가이드 메시지
            ["ar_guide_too_dark"] = new Dictionary<string, string>()
            {
                ["en"] = "The environment is too dark.\nPlease turn on lights or move to a brighter area.",
                ["ko"] = "환경이 너무 어둡습니다.\n조명을 켜시거나 밝은 곳으로 이동해주세요.",
                ["zh"] = "环境太暗了。\n请打开灯光或移动到明亮的地方。",
                ["ja"] = "環境が暗すぎます。\nライトをつけるか、明るい場所に移動してください。",
                ["es"] = "El ambiente está muy oscuro.\nPor favor, enciende las luces o muévete a un lugar más brillante."
            },
            ["ar_guide_no_features"] = new Dictionary<string, string>()
            {
                ["en"] = "Insufficient visual features.\nPlease point camera at surfaces with patterns or textures.",
                ["ko"] = "특징점이 부족합니다.\n패턴이나 텍스처가 있는 표면을 비춰주세요.",
                ["zh"] = "视觉特征不足。\n请将相机对准有图案或纹理的表面。",
                ["ja"] = "視覚的特徴が不足しています。\nパターンやテクスチャのある表面にカメラを向けてください。",
                ["es"] = "Características visuales insuficientes.\nPor favor, apunta la cámara a superficies con patrones o texturas."
            },
            ["ar_guide_insufficient_light"] = new Dictionary<string, string>()
            {
                ["en"] = "Insufficient lighting.\nPlease use in a brighter environment.",
                ["ko"] = "조명이 부족합니다.\n더 밝은 환경에서 사용해주세요.",
                ["zh"] = "光线不足。\n请在更明亮的环境中使用。",
                ["ja"] = "照明が不足しています。\nより明るい環境でご使用ください。",
                ["es"] = "Iluminación insuficiente.\nPor favor, úsalo en un ambiente más brillante."
            },
            ["ar_guide_camera_covered"] = new Dictionary<string, string>()
            {
                ["en"] = "Camera appears to be covered.\nPlease remove fingers or objects from camera.",
                ["ko"] = "카메라가 가려져 있습니다.\n손가락이나 물체를 치워주세요.",
                ["zh"] = "相机似乎被遮挡了。\n请移开手指或物体。",
                ["ja"] = "カメラが覆われているようです。\n指や物体をカメラから取り除いてください。",
                ["es"] = "La cámara parece estar cubierta.\nPor favor, retira los dedos u objetos de la cámara."
            }
        };
    }
    
    public string GetText(string key, params object[] args)
    {
        if (localizedTexts == null)
        {
            InitializeTexts();
        }
        
        if (localizedTexts.ContainsKey(key) && localizedTexts[key].ContainsKey(currentLanguage))
        {
            string text = localizedTexts[key][currentLanguage];
            return args.Length > 0 ? string.Format(text, args) : text;
        }
        
        // 현재 언어에 해당 키가 없으면 영어로 대체
        if (localizedTexts.ContainsKey(key) && localizedTexts[key].ContainsKey("en"))
        {
            string text = localizedTexts[key]["en"];
            Debug.LogWarning($"[LocalizationManager] Key '{key}' not found for language '{currentLanguage}', using English.");
            return args.Length > 0 ? string.Format(text, args) : text;
        }
        
        // 영어도 없으면 키 자체를 반환
        Debug.LogError($"[LocalizationManager] Key '{key}' not found in any language!");
        return key;
    }
    
    public string GetCurrentLanguage()
    {
        return currentLanguage;
    }
    
    public void SetLanguage(string languageCode)
    {
        bool isSupported = false;
        foreach (string lang in supportedLanguages)
        {
            if (lang == languageCode)
            {
                isSupported = true;
                break;
            }
        }
        
        currentLanguage = isSupported ? languageCode : "en";
        Debug.Log($"[LocalizationManager] Language changed to: {currentLanguage}");
    }
}