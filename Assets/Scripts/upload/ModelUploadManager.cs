using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.IO;
using System;
using System.Text.RegularExpressions;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARSubsystems;

public class ModelUploadManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject uploadPage;
    [SerializeField] private InputField nameInput;
    [SerializeField] private InputField locationInput;

    [Header("File Selection")]
    [SerializeField] private Button selectFileButton;
    [SerializeField] private Button selectedFileButton;
    [SerializeField] private Button uploadButton;

    [Header("Photo Selection")]
    [SerializeField] private Button subPhotosButton;
    [SerializeField] private GridLayoutGroup subPhotoGrid;
    [SerializeField] private Button resetPhotosButton;

    [Header("Toggle Options")]
    [SerializeField] private Toggle petFriendlyToggle;
    [SerializeField] private Toggle separateRestroomToggle;
    [SerializeField] private Toggle instagramToggle;
    [SerializeField] private InputField instagramIDInput;

    [Header("Progress UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Text loadingText;
    [SerializeField] private Image loadingSpinner;

    [Header("Warning System")]
    [SerializeField] private GameObject warningObj;

    [Header("Geospatial API")]
    [SerializeField] private AREarthManager earthManager;
    [SerializeField] private bool useGeospatialAPI = true;

    [Header("Upload Settings")]
    [SerializeField] private float uploadTimeoutSeconds = 30f;
    [SerializeField] private int countdownSeconds = 30;

    private string selectedFilePath;
    private byte[] selectedFileData;
    private List<Texture2D> subPhotos = new List<Texture2D>();
    private List<Image> subPhotoDisplays = new List<Image>();
    private string serverUrl => ApiConfig.CREATE_LOCATION_WITH_MODEL;
    private string locationText;
    private string instagramID;
    private bool showInstagram;
    private Vector3 gpsData = Vector3.zero;
    private bool isProcessing = false;
    private float elapsedTime = 0f;
    private const int MAX_SUB_PHOTOS = 10;
    private bool canUploadToday = true; // 하루 1회 업로드 제한

    private void Awake()
    {
        if (FindObjectsByType<ModelUploadManager>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
        }
        else
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    void Start()
    {
        InitializeComponents();

#if !UNITY_EDITOR
        StartCoroutine(InitializeLocationService());
#endif
    }

    private void InitializeComponents()
    {
        // AREarthManager 자동 연결 (Inspector 미연결 시)
        if (earthManager == null)
        {
            earthManager = FindFirstObjectByType<AREarthManager>();
        }

        if (selectFileButton != null)
        {
            selectFileButton.onClick.AddListener(SelectModelFile);
        }
        else 
        {
            Debug.LogError("selectFileButton이 할당되지 않았습니다!");
        }

        if (selectedFileButton != null) 
        {
            selectedFileButton.onClick.AddListener(SelectModelFile);
        }
        else 
        {
            Debug.LogError("selectedFileButton이 할당되지 않았습니다!");
        }

        if (uploadButton != null) 
        {
            uploadButton.onClick.AddListener(() => StartCoroutine(ValidateAndSubmit()));
        }
        else 
        {
            Debug.LogError("uploadButton이 할당되지 않았습니다!");
        }

        if (subPhotosButton != null) 
        {
            subPhotosButton.onClick.AddListener(() => StartCoroutine(SelectSubPhotos()));
        }
        else 
        {
            Debug.LogError("subPhotosButton이 할당되지 않았습니다!");
        }

        if (resetPhotosButton != null) 
        {
            resetPhotosButton.onClick.AddListener(ResetSubPhotos);
        }
        else 
        {
            Debug.LogError("resetPhotosButton이 할당되지 않았습니다!");
        }

        if (instagramToggle != null)
        {
            instagramToggle.onValueChanged.AddListener(OnInstagramToggleChanged);
            // 초기 상태: 토글 Off, 입력 필드 숨김
            instagramToggle.isOn = false;
            if (instagramIDInput != null)
            {
                instagramIDInput.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogError("instagramToggle이 할당되지 않았습니다!");
        }

        if (uploadButton != null) uploadButton.interactable = true;

        if (warningObj != null) warningObj.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);

        // 파일 선택 버튼 초기 상태 설정
        if (selectFileButton != null) selectFileButton.gameObject.SetActive(true);
        if (selectedFileButton != null) selectedFileButton.gameObject.SetActive(false);

        if (locationInput != null)
        {
            locationInput.interactable = true;
            locationInput.image.color = Color.white;
        }

        InitializeObjectPool();

        if (uploadPage == null) Debug.LogError("uploadPage가 할당되지 않았습니다!");
    }

    #region HEIC Processing Methods

    /// <summary>
    /// iOS에서 HEIC 변환이 필요한지 확인
    /// </summary>
    private bool NeedsHEICConversion(string filePath)
    {
        if (Application.platform != RuntimePlatform.IPhonePlayer) return false;
        
        string extension = Path.GetExtension(filePath).ToLower();
        return extension == ".heic" || extension == ".heif";
    }

    /// <summary>
    /// 수동 이미지 변환 (HEIC → JPG)
    /// </summary>
    private IEnumerator LoadImageWithConversion(string imagePath, System.Action<Texture2D> onComplete)
    {
        if (!NeedsHEICConversion(imagePath))
        {
            // HEIC가 아니면 일반 로드 시도
            yield return StartCoroutine(LoadImageDirect(imagePath, onComplete));
            yield break;
        }

#if UNITY_IOS && !UNITY_EDITOR
        // iOS에서 네이티브 변환 시도
        yield return StartCoroutine(ConvertHEICToJPG(imagePath, onComplete));
#else
        // 에디터나 다른 플랫폼에서는 직접 로드 시도
        yield return StartCoroutine(LoadImageDirect(imagePath, onComplete));
#endif
    }

    /// <summary>
    /// 직접 이미지 로드 (JPG, PNG 등)
    /// </summary>
    private IEnumerator LoadImageDirect(string imagePath, System.Action<Texture2D> onComplete)
    {
        try
        {
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            
            if (texture.LoadImage(imageBytes))
            {
                onComplete?.Invoke(texture);
            }
            else
            {
                Debug.LogError("[HEIC] 직접 로드 실패");
                Destroy(texture);
                onComplete?.Invoke(null);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HEIC] 직접 로드 예외: {e.Message}");
            onComplete?.Invoke(null);
        }
        
        yield return null;
    }

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>
    /// iOS 네이티브 HEIC → JPG 변환
    /// </summary>
    private IEnumerator ConvertHEICToJPG(string heicPath, System.Action<Texture2D> onComplete)
    {
        try
        {
            // 원본 HEIC 파일 읽기
            byte[] heicBytes = File.ReadAllBytes(heicPath);
            Texture2D tempTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            
            // Unity로 직접 로드 시도 (실패할 가능성 높음)
            if (tempTexture.LoadImage(heicBytes))
            {
                onComplete?.Invoke(tempTexture);
                yield break;
            }
            
            // 실패 시 JPG 변환
            Destroy(tempTexture);
            
            // 변환된 JPG 파일 경로 생성
            string directory = Path.GetDirectoryName(heicPath);
            string fileName = Path.GetFileNameWithoutExtension(heicPath);
            string jpgPath = Path.Combine(directory, $"{fileName}_converted.jpg");
            
            // 실제 변환은 NativeGallery 내부 로직에 의존
            // 여기서는 기본 변환 시도
            byte[] jpgBytes = ConvertToJPGBytes(heicBytes);
            
            if (jpgBytes != null && jpgBytes.Length > 0)
            {
                File.WriteAllBytes(jpgPath, jpgBytes);
                
                Texture2D convertedTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (convertedTexture.LoadImage(jpgBytes))
                {
                    // 임시 파일 삭제
                    try { File.Delete(jpgPath); } catch { }
                    
                    onComplete?.Invoke(convertedTexture);
                    yield break;
                }
                else
                {
                    Destroy(convertedTexture);
                }
            }
            
            Debug.LogError("[HEIC] JPG 변환 실패");
            onComplete?.Invoke(null);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HEIC] 변환 중 예외: {e.Message}");
            onComplete?.Invoke(null);
        }
        
        yield return null;
    }

    /// <summary>
    /// HEIC 바이트를 JPG 바이트로 변환 (iOS 네이티브 구현 필요)
    /// </summary>
    private byte[] ConvertToJPGBytes(byte[] heicBytes)
    {
        // 실제 구현에서는 iOS 네이티브 플러그인 호출 필요
        // 현재는 기본 Unity 변환 시도
        try
        {
            Texture2D tempTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (tempTexture.LoadImage(heicBytes))
            {
                byte[] jpgBytes = tempTexture.EncodeToJPG(90);
                Destroy(tempTexture);
                return jpgBytes;
            }
            Destroy(tempTexture);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HEIC] 바이트 변환 실패: {e.Message}");
        }
        
        return null;
    }
#endif

    #endregion

    #region Photo Processing Methods

    /// <summary>
    /// 서브 사진 선택 및 처리 (2단계 HEIC 처리 포함)
    /// </summary>
    private IEnumerator SelectSubPhotos()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        ShowSpinner(GetLocalizedText("loading_sub_photos"));
        bool isLoading = true;

        try
        {
            NativeGallery.GetImagesFromGallery((paths) =>
            {
                if (paths != null && paths.Length > 0)
                {
                    if (subPhotos.Count + paths.Length > MAX_SUB_PHOTOS)
                    {
                        ShowWarning(GetLocalizedText("max_sub_photos_exceeded"));
                        isLoading = false;
                    }
                    else
                    {
                        StartCoroutine(LoadSubPhotosWithFallback(paths, () => { isLoading = false; }));
                    }
                }
                else
                {
                    ShowWarning(GetLocalizedText("photo_selection_failed"));
                    isLoading = false;
                }
            }, "image/*");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ModelUploadManager] NativeGallery 호출 오류: {e}");
            ShowWarning(GetLocalizedText("photo_selection_failed"));
            isLoading = false;
        }

        yield return new WaitUntil(() => !isLoading);
        HideSpinner();
        UpdateSubPhotoGrid();
        isProcessing = false;
    }

    /// <summary>
    /// 다중 이미지 로드 (2단계 HEIC 처리)
    /// </summary>
    private IEnumerator LoadSubPhotosWithFallback(string[] paths, System.Action onComplete)
    {
        foreach (string path in paths)
        {
            if (subPhotos.Count >= MAX_SUB_PHOTOS)
            {
                ShowWarning(GetLocalizedText("max_sub_photos_exceeded"));
                break;
            }

            // 1단계: NativeGallery.LoadImageAtPath 시도
            Texture2D texture = NativeGallery.LoadImageAtPath(path, 
                maxSize: 1024,  // 서브사진은 더 작게
                markTextureNonReadable: false,
                generateMipmaps: false);
            
            if (texture != null)
            {
                subPhotos.Add(texture);
            }
            else
            {
                // 2단계: 수동 변환 시도
                bool conversionComplete = false;
                Texture2D convertedTexture = null;

                yield return StartCoroutine(LoadImageWithConversion(path, (result) =>
                {
                    convertedTexture = result;
                    conversionComplete = true;
                }));

                yield return new WaitUntil(() => conversionComplete);

                if (convertedTexture != null)
                {
                    subPhotos.Add(convertedTexture);
                }
                else
                {
                    Debug.LogError($"[HEIC] 서브사진 모든 단계 실패: {path}");
                    ShowWarning(GetLocalizedText("photo_selection_failed"));
                }
            }

            yield return null; // 한 프레임 대기
        }

        UpdateSubPhotoGrid();
        onComplete?.Invoke();
    }

    #endregion

    #region UI and Component Management

    private void OnInstagramToggleChanged(bool value)
    {
        showInstagram = value;
        if (instagramIDInput != null)
        {
            // 토글 상태에 따라 입력 필드 표시/숨김
            instagramIDInput.gameObject.SetActive(value);
            if (!value) instagramIDInput.text = "";
        }
    }

    private void InitializeObjectPool()
    {
        if (subPhotoGrid == null)
        {
            Debug.LogError("subPhotoGrid가 할당되지 않았습니다!");
            return;
        }

        subPhotoDisplays = new List<Image>();
        for (int i = 0; i < MAX_SUB_PHOTOS; i++)
        {
            GameObject imageObj = new GameObject($"SubPhoto_{i}");
            imageObj.transform.SetParent(subPhotoGrid.transform, false);
            Image img = imageObj.AddComponent<Image>();
            img.preserveAspect = true;
            subPhotoDisplays.Add(img);
            imageObj.SetActive(false);
        }
    }

    public void ShowWarning(string message)
    {
        Text warningText = warningObj?.GetComponentInChildren<Text>();
        if (warningText != null)
        {
            warningText.text = message;
        }
        if (warningObj != null) warningObj.SetActive(true);
        CancelInvoke("HideWarning");
        Invoke("HideWarning", 2f);
    }

    private void HideWarning()
    {
        if (warningObj != null) warningObj.SetActive(false);
    }

    public void ShowUploadPage()
    {
        if (uploadPage != null) uploadPage.SetActive(true);
        if (locationInput != null) locationInput.text = GetLocalizedText("loading_location");
        StartCoroutine(InitializeLocationService());
    }

    public void HideUploadPage()
    {
        if (uploadPage != null) uploadPage.SetActive(false);
        ResetToInitialState();
    }

    private void ShowSpinner(string message)
    {
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (loadingText != null) loadingText.text = message;
        if (loadingSpinner != null) StartCoroutine(SpinnerAnimation());
    }

    private void HideSpinner()
    {
        if (loadingPanel != null) loadingPanel.SetActive(false);
        
        // 스피너 애니메이션 코루틴 안전하게 정지
        try
        {
            StopCoroutine(SpinnerAnimation());
        }
        catch (System.Exception)
        {
            // 코루틴이 이미 정지된 경우 무시
        }
    }

    private IEnumerator SpinnerAnimation()
    {
        while (loadingPanel != null && loadingPanel.activeInHierarchy && loadingSpinner != null)
        {
            loadingSpinner.transform.Rotate(0, 0, -90 * Time.deltaTime);
            yield return null;
        }
    }

    #endregion

    #region File Management

    private void SelectModelFile()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel(
            "3D Model File Selection",
            "",
            "glb,gltf,fbx,obj"
        );
        if (!string.IsNullOrEmpty(path))
        {
            ProcessSelectedFile(path);
        }
#elif UNITY_ANDROID
        StartCoroutine(OpenAndroidFilePicker());
#elif UNITY_IOS
        StartCoroutine(OpenIOSFilePicker());
#else
        ShowWarning("File selection is only available on mobile devices or editor");
#endif
    }

#if UNITY_ANDROID
    IEnumerator OpenAndroidFilePicker()
    {
        // 권한 확인 및 요청
        if (!NativeFilePicker.CheckPermission())
        {
            yield return NativeFilePicker.RequestPermissionAsync();
            if (!NativeFilePicker.CheckPermission())
            {
                ShowWarning(GetLocalizedText("permission_denied"));
                yield break;
            }
        }

        bool isDone = false;
        string selectedPath = null;
        
        // Android에서 더 포괄적인 MIME 타입 사용
        NativeFilePicker.PickFile((path) =>
        {
            selectedPath = path;
            isDone = true;
        }, new string[] { 
            "model/gltf-binary", 
            "model/gltf+json", 
            "application/octet-stream",
            "*/*"  // 모든 파일 타입 허용
        });

        yield return new WaitUntil(() => isDone);

        if (selectedPath != null)
        {
            ProcessSelectedFile(selectedPath);
        }
    }
#endif

#if UNITY_IOS
    IEnumerator OpenIOSFilePicker()
    {

        bool isDone = false;
        string selectedPath = null;
        
        // iOS에서 더 포괄적인 UTI 사용
        NativeFilePicker.PickFile((path) =>
        {
            selectedPath = path;
            isDone = true;
        }, new string[] { 
            "com.khronos.glb",           // GLB 파일
            "org.khronos.gltf",          // GLTF 파일  
            "com.autodesk.fbx",          // FBX 파일
            "com.wavefront.obj",         // OBJ 파일
            "public.data",               // 일반 데이터 파일
            "public.item",               // 모든 아이템
            "public.content"             // 모든 콘텐츠
        });

        yield return new WaitUntil(() => isDone);

        if (selectedPath != null)
        {
            ProcessSelectedFile(selectedPath);
        }
    }
#endif

    private void ProcessSelectedFile(string filePath)
    {
        bool processSuccess = false;
        
        try
        {
            // 파일 존재 여부 확인
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[ModelUploadManager] 파일이 존재하지 않음: {filePath}");
                ShowWarning("Selected file does not exist");
                return;
            }

            selectedFilePath = filePath;
            selectedFileData = File.ReadAllBytes(filePath);

            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath).ToLower();
            float fileSizeMB = selectedFileData.Length / (1024f * 1024f);

            // 지원되는 3D 모델 파일 확장자 확인
            string[] supportedExtensions = { ".glb", ".gltf", ".fbx", ".obj" };
            bool isSupportedFormat = Array.Exists(supportedExtensions, ext => ext == extension);

            if (!isSupportedFormat)
            {
                ShowWarning($"Unsupported file format: {extension}. Please select GLB, GLTF, FBX, or OBJ files.");
                ResetFileSelection();
                return;
            }

            if (fileSizeMB > 10f)
            {
                ShowWarning(GetLocalizedText("file_too_large"));
                ResetFileSelection();
                return;
            }

            // 파일 선택 완료 시 UI 상태 변경
            if (selectFileButton != null) selectFileButton.gameObject.SetActive(false);
            if (selectedFileButton != null) selectedFileButton.gameObject.SetActive(true);

            processSuccess = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ModelUploadManager] 파일 읽기 실패: {e}");
            processSuccess = false;
        }

        if (!processSuccess)
        {
            ShowWarning(GetLocalizedText("file_read_failed"));
            ResetFileSelection();
        }
    }

    private void ResetFileSelection()
    {
        selectedFilePath = null;
        selectedFileData = null;
        if (uploadButton != null) uploadButton.interactable = true;

        if (selectFileButton != null) selectFileButton.gameObject.SetActive(true);
        if (selectedFileButton != null) selectedFileButton.gameObject.SetActive(false);
    }

    private void ResetSubPhotos()
    {
        foreach (var photo in subPhotos)
        {
            Destroy(photo);
        }
        subPhotos.Clear();
        UpdateSubPhotoGrid();
        ShowWarning(GetLocalizedText("sub_photos_reset"));
        CancelInvoke("HideWarning");
        Invoke("HideWarning", 2f);
    }

    private void UpdateSubPhotoGrid()
    {
        if (subPhotoDisplays == null) return;

        for (int i = 0; i < subPhotoDisplays.Count; i++)
        {
            if (i < subPhotos.Count)
            {
                subPhotoDisplays[i].sprite = GetOrCreateSprite(subPhotos[i]);
                subPhotoDisplays[i].gameObject.SetActive(true);
            }
            else
            {
                subPhotoDisplays[i].sprite = null;
                subPhotoDisplays[i].gameObject.SetActive(false);
            }
        }

    }

    private Sprite GetOrCreateSprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    #endregion

    #region Location Services

    private IEnumerator InitializeLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            UpdateLocationDisplay();
            yield break;
        }

        Input.location.Start();
        int maxWait = 10; // 최대 10초 대기
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogError("위치 서비스 초기화 실패!");
            UpdateLocationDisplay();
            yield break;
        }
        else
        {
            UpdateLocationDisplay();
            StartCoroutine(UpdateLocation(10f));
        }
    }

    private IEnumerator UpdateLocation(float interval)
    {
        while (true)
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                UpdateLocationDisplay();
            }
            else
            {
                StartCoroutine(InitializeLocationService());
            }
            yield return new WaitForSeconds(interval);
        }
    }

    private void UpdateLocationDisplay()
    {
        bool locationObtained = false;

        // ARCore Geospatial API 우선 사용 (CubeUploadManager와 동일)
        if (useGeospatialAPI && earthManager != null)
        {
            if (earthManager.EarthTrackingState == TrackingState.Tracking)
            {
                var pose = earthManager.CameraGeospatialPose;

                // Geospatial API는 자동으로 MSL 고도 제공 (iOS/Android 통일)
                gpsData = new Vector3(
                    (float)pose.Latitude,
                    (float)pose.Longitude,
                    (float)pose.Altitude
                );

                locationText = $"Lat:{gpsData.x:F4},Lon:{gpsData.y:F4},Alt:{gpsData.z:F2}";
                locationObtained = true;
            }
        }

        // Fallback: 기본 GPS 사용 (ARCore 사용 안 함 또는 실패 시)
        if (!locationObtained && Input.location.status == LocationServiceStatus.Running)
        {
            float lat = Input.location.lastData.latitude;
            float lon = Input.location.lastData.longitude;
            float normalizedAltitude = GeoidHelper.NormalizeAltitude(
                Input.location.lastData.altitude, lat, lon);

            gpsData = new Vector3(lat, lon, normalizedAltitude);
            locationText = $"Lat:{gpsData.x:F4},Lon:{gpsData.y:F4},Alt:{gpsData.z:F2}";
            locationObtained = true;
        }

        if (!locationObtained)
        {
            gpsData = Vector3.zero;
            locationText = GetLocalizedText("no_location_data");
        }

        if (locationInput != null) locationInput.text = locationText;
    }

    #endregion

    #region Validation and Upload

    private IEnumerator ValidateAndSubmit()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        // 0. 로그인 체크
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            ShowWarning(GetLocalizedText("login_required_for_upload"));
            if (LoginManager.Instance != null)
                LoginManager.Instance.ShowLoginRequirementPopup();
            isProcessing = false;
            yield break;
        }

        // 0-1. 하루 1회 업로드 제한 체크
        yield return StartCoroutine(CheckDailyUploadLimit());
        if (!canUploadToday)
        {
            ShowWarning(GetLocalizedText("daily_upload_limit_reached"));
            isProcessing = false;
            yield break;
        }

        string modelName = nameInput?.text.Trim() ?? "";
        instagramID = showInstagram ? instagramIDInput?.text.Trim() ?? "" : "";

        // 1. 이름 체크
        if (string.IsNullOrEmpty(modelName))
        {
            ShowWarning(GetLocalizedText("enter_name"));
            isProcessing = false;
            yield break;
        }

        // 2. 위치 서비스 체크
        if (Input.location.status != LocationServiceStatus.Running || gpsData == Vector3.zero)
        {
            ShowWarning(GetLocalizedText("enable_location_service"));
            isProcessing = false;
            yield break;
        }

        // 3. 3D 모델 파일 체크
        if (selectedFileData == null)
        {
            ShowWarning(GetLocalizedText("file_not_selected"));
            isProcessing = false;
            yield break;
        }

        // 4. 서브 사진 체크
        if (subPhotos.Count == 0)
        {
            ShowWarning(GetLocalizedText("upload_min_one_photo"));
            isProcessing = false;
            yield break;
        }

        // 5. 인스타그램 ID 체크 (활성화된 경우)
        if (showInstagram && string.IsNullOrEmpty(instagramID))
        {
            ShowWarning(GetLocalizedText("enter_instagram_id"));
            isProcessing = false;
            yield break;
        }

        // 6. 인스타그램 ID 유효성 체크
        if (showInstagram && !Regex.IsMatch(instagramID, @"^[a-zA-Z0-9_.]+$"))
        {
            ShowWarning(GetLocalizedText("instagram_id_invalid"));
            isProcessing = false;
            yield break;
        }

        Coroutine countdownCoroutine = StartCoroutine(ShowCountdownWarning(countdownSeconds));
        yield return StartCoroutine(SendWithTimeout(ProcessAndUploadModel(), countdownCoroutine));

        if (!isProcessing)
        {
            yield return new WaitForSeconds(2f);
        }

        isProcessing = false;
    }

    private IEnumerator ShowCountdownWarning(int seconds)
    {
        for (int i = seconds; i >= 1; i--)
        {
            string message = GetLocalizedText("submitting_countdown").Replace("{0}", i.ToString());
            ShowWarning(message);
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator SendWithTimeout(IEnumerator routine, Coroutine countdownCoroutine)
    {
        float timeout = uploadTimeoutSeconds; // Inspector에서 설정 가능한 타임아웃 사용
        elapsedTime = 0f;
        bool isCompleted = false;

        Coroutine co = StartCoroutine(routine);
        yield return StartCoroutine(WaitForRoutine(co, timeout, () => isCompleted));

        if (!isProcessing)
        {
            isCompleted = true;
            StopCoroutine(countdownCoroutine);
        }
    }

    private IEnumerator WaitForRoutine(Coroutine routine, float timeout, Func<bool> isCompleted)
    {
        while (routine != null && elapsedTime < timeout && !isCompleted() && isProcessing)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (routine != null && !isCompleted() && elapsedTime >= timeout)
        {
            StopCoroutine(routine);
            isProcessing = true;
            ShowWarning(GetLocalizedText("request_timeout"));
        }
    }

    private IEnumerator ProcessAndUploadModel()
    {
        ShowSpinner(GetLocalizedText("uploading_object"));

        string modelName = nameInput.text.Trim();

        WWWForm formData = new WWWForm();

        formData.AddField("name", modelName);
        formData.AddField("latitude", gpsData.x.ToString("F6"));
        formData.AddField("longitude", gpsData.y.ToString("F6"));
        formData.AddField("altitude", gpsData.z.ToString("F2"));
        formData.AddField("model_type", "custom");
        // status는 서버에서 AUTO_APPROVE 설정에 따라 결정
        formData.AddField("device_id", SystemInfo.deviceUniqueIdentifier);  // 업로더 추적용
        formData.AddField("pet_friendly", petFriendlyToggle?.isOn ?? false ? "true" : "false");
        formData.AddField("separate_restroom", separateRestroomToggle?.isOn ?? false ? "true" : "false");
        formData.AddField("instagram_id", showInstagram ? instagramID : "");
        formData.AddField("timezone", GetTimezone());
        formData.AddField("timezone_offset", GetTimezoneOffset());
        formData.AddField("model_scale", "1.0");
        formData.AddField("model_rotation_x", "0.0");
        formData.AddField("model_rotation_y", "0.0");
        formData.AddField("model_rotation_z", "0.0");
        formData.AddField("animation_speed", "1.0");
        formData.AddField("animation_loop", "on");
        formData.AddField("animation_auto_play", "on");

        string folder = $"{DateTime.Now:yyyyMMdd_HHmmss}_{modelName}";
        formData.AddField("folder", folder);

        string fileName = Path.GetFileName(selectedFilePath);
        string mimeType = GetMimeType(fileName);
        formData.AddBinaryData("model_file", selectedFileData, fileName, mimeType);

        for (int i = 1; i <= subPhotos.Count; i++)
        {
            if (i > MAX_SUB_PHOTOS) break;
            Texture2D subPhoto = subPhotos[i - 1];
            Texture2D resizedSubPhoto = ResizeTextureKeepAspectWithRenderTexture(subPhoto, 800, 800);
            byte[] subPhotoBytes = resizedSubPhoto.EncodeToJPG(50);
            if (subPhotoBytes.Length == 0)
            {
                Debug.LogError($"[ModelUploadManager] 서브 사진 #{i} 데이터가 비어 있습니다!");
                ShowWarning(GetLocalizedText("sub_photo_upload_failed"));
                Destroy(resizedSubPhoto);
                continue;
            }
            string subPath = $"sub_{i}.jpg";
            formData.AddBinaryData("sub_photos", subPhotoBytes, subPath, "image/jpeg");
            Destroy(resizedSubPhoto);
        }

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, formData))
        {
            www.timeout = Mathf.RoundToInt(uploadTimeoutSeconds); // Inspector에서 설정 가능한 타임아웃 사용
            yield return www.SendWebRequest();

            HideSpinner();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;

                if (responseText.Contains("Upload Succeeded!") || responseText.Contains("success") || www.responseCode == 200)
                {
                    isProcessing = false;
                    ShowWarning(GetLocalizedText("upload_success"));

                    // 업로드 성공 기록 저장
                    int locationId = ParseLocationIdFromResponse(responseText);
                    if (locationId > 0)
                    {
                        StartCoroutine(RecordUploadSuccess(locationId));
                    }

                    FullReset();
                    yield break;
                }
                else
                {
                    isProcessing = true;
                    ShowWarning(GetLocalizedText("server_error"));
                }
            }
            else
            {
                Debug.LogError($"[ModelUploadManager] 업로드 실패: {www.error} (응답 코드: {www.responseCode})");
                isProcessing = true;
                ShowWarning(GetLocalizedText("server_error"));
            }
        }
    }

    #endregion

    #region Utility Methods

    private Texture2D ResizeTextureKeepAspectWithRenderTexture(Texture2D source, int maxWidth, int maxHeight)
    {
        int width = source.width;
        int height = source.height;
        float aspect = (float)width / height;

        int newWidth, newHeight;
        if (aspect > 1)
        {
            newWidth = Mathf.Min(maxWidth, width);
            newHeight = Mathf.RoundToInt(newWidth / aspect);
        }
        else
        {
            newHeight = Mathf.Min(maxHeight, height);
            newWidth = Mathf.RoundToInt(newHeight * aspect);
        }

        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    private string GetMimeType(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLower();
        switch (extension)
        {
            case ".glb": return "model/gltf-binary";
            case ".gltf": return "model/gltf+json";
            case ".fbx": return "application/octet-stream";
            case ".obj": return "application/octet-stream";
            default: return "application/octet-stream";
        }
    }

    private string GetTimezone()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: return "Asia/Seoul";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified: return "Asia/Shanghai";
            case SystemLanguage.Japanese: return "Asia/Tokyo";
            case SystemLanguage.Spanish: return "America/Madrid";
            default: return "UTC";
        }
    }

    private string GetTimezoneOffset()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: return "+09:00";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified: return "+08:00";
            case SystemLanguage.Japanese: return "+09:00";
            case SystemLanguage.Spanish: return "+01:00";
            default: return "+00:00";
        }
    }

    private void FullReset()
    {
        ResetToInitialState();
        StopAllCoroutines();
        StartCoroutine(InitializeLocationService());
    }

    private void ResetToInitialState()
    {
        if (uploadPage != null) uploadPage.SetActive(false);

        if (nameInput != null) nameInput.text = "";
        if (locationInput != null)
        {
            locationInput.text = GetLocalizedText("loading_location");
            locationInput.interactable = true;
            locationInput.image.color = Color.white;
        }
        if (instagramIDInput != null) instagramIDInput.text = "";

        if (petFriendlyToggle != null) petFriendlyToggle.isOn = false;
        if (separateRestroomToggle != null) separateRestroomToggle.isOn = false;
        if (instagramToggle != null)
        {
            instagramToggle.isOn = false;
            showInstagram = false;
            if (instagramIDInput != null)
            {
                instagramIDInput.gameObject.SetActive(false);
                instagramIDInput.text = "";
            }
        }

        ResetFileSelection();

        foreach (var photo in subPhotos)
        {
            Destroy(photo);
        }
        subPhotos.Clear();
        UpdateSubPhotoGrid();

        gpsData = Vector3.zero;
        isProcessing = false;
        elapsedTime = 0f;
        instagramID = "";
    }

    #endregion

    #region Localization

    private string GetLocalizedText(string key)
    {
        if (LocalizationManager.Instance != null)
        {
            return LocalizationManager.Instance.GetText(key);
        }

        SystemLanguage lang = Application.systemLanguage;
        switch (key)
        {
            case "loading_location":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "위치 정보 불러오는 중...";
                    case SystemLanguage.Japanese: return "位置情報を読み込み中...";
                    case SystemLanguage.Chinese: return "正在加载位置信息...";
                    case SystemLanguage.ChineseSimplified: return "正在加载位置信息...";
                    case SystemLanguage.Spanish: return "Cargando información de ubicación...";
                    default: return "Loading location...";
                }

            case "loading_sub_photos":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "서브 사진 로딩 중...";
                    case SystemLanguage.Japanese: return "サブ写真を読み込み中...";
                    case SystemLanguage.Chinese: return "正在加载子照片...";
                    case SystemLanguage.ChineseSimplified: return "正在加载子照片...";
                    case SystemLanguage.Spanish: return "Cargando sub fotos...";
                    default: return "Loading sub photos...";
                }

            case "uploading_object":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "오브젝트를 업로드 중입니다...";
                    case SystemLanguage.Japanese: return "オブジェクトをアップロード中です...";
                    case SystemLanguage.Chinese: return "正在上传对象...";
                    case SystemLanguage.ChineseSimplified: return "正在上传对象...";
                    case SystemLanguage.Spanish: return "Subiendo objeto...";
                    default: return "Uploading object...";
                }

            case "file_not_selected":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "3D 모델 파일을 먼저 선택해주세요";
                    case SystemLanguage.Japanese: return "3Dモデルファイルを先に選択してください";
                    case SystemLanguage.Chinese: return "请先选择3D模型文件";
                    case SystemLanguage.ChineseSimplified: return "请先选择3D模型文件";
                    case SystemLanguage.Spanish: return "Por favor seleccione primero el archivo del modelo 3D";
                    default: return "Please select a 3D model file first";
                }

            case "enter_name":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "모델 이름을 입력해주세요";
                    case SystemLanguage.Japanese: return "モデル名を入力してください";
                    case SystemLanguage.Chinese: return "请输入模型名称";
                    case SystemLanguage.ChineseSimplified: return "请输入模型名称";
                    case SystemLanguage.Spanish: return "Por favor ingrese el nombre del modelo";
                    default: return "Please enter model name";
                }

            case "enter_instagram_id":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "인스타그램 ID를 입력해주세요";
                    case SystemLanguage.Japanese: return "インスタグラムIDを入力してください";
                    case SystemLanguage.Chinese: return "请输入Instagram ID";
                    case SystemLanguage.ChineseSimplified: return "请输入Instagram ID";
                    case SystemLanguage.Spanish: return "Por favor ingrese el ID de Instagram";
                    default: return "Please enter Instagram ID";
                }

            case "instagram_id_invalid":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "유효하지 않은 인스타그램 ID입니다";
                    case SystemLanguage.Japanese: return "無効なインスタグラムIDです";
                    case SystemLanguage.Chinese: return "无效的Instagram ID";
                    case SystemLanguage.ChineseSimplified: return "无效的Instagram ID";
                    case SystemLanguage.Spanish: return "ID de Instagram inválido";
                    default: return "Invalid Instagram ID";
                }

            case "upload_min_one_photo":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "최소 1장의 사진을 업로드해주세요";
                    case SystemLanguage.Japanese: return "最低1枚の写真をアップロードしてください";
                    case SystemLanguage.Chinese: return "请至少上传1张照片";
                    case SystemLanguage.ChineseSimplified: return "请至少上传1张照片";
                    case SystemLanguage.Spanish: return "Por favor suba al menos 1 foto";
                    default: return "Please upload at least 1 photo";
                }

            case "max_sub_photos_exceeded":
                switch (lang)
                {
                    case SystemLanguage.Korean: return $"최대 {MAX_SUB_PHOTOS}장까지만 선택 가능합니다";
                    case SystemLanguage.Japanese: return $"最大{MAX_SUB_PHOTOS}枚まで選択可能です";
                    case SystemLanguage.Chinese: return $"最多只能选择{MAX_SUB_PHOTOS}张";
                    case SystemLanguage.ChineseSimplified: return $"最多只能选择{MAX_SUB_PHOTOS}张";
                    case SystemLanguage.Spanish: return $"Solo se pueden seleccionar hasta {MAX_SUB_PHOTOS} fotos";
                    default: return $"Maximum {MAX_SUB_PHOTOS} photos can be selected";
                }

            case "photo_selection_failed":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "사진 선택에 실패했습니다";
                    case SystemLanguage.Japanese: return "写真の選択に失敗しました";
                    case SystemLanguage.Chinese: return "照片选择失败";
                    case SystemLanguage.ChineseSimplified: return "照片选择失败";
                    case SystemLanguage.Spanish: return "Falló la selección de fotos";
                    default: return "Photo selection failed";
                }

            case "sub_photos_reset":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "서브 사진이 리셋되었습니다";
                    case SystemLanguage.Japanese: return "サブ写真がリセットされました";
                    case SystemLanguage.Chinese: return "子照片已重置";
                    case SystemLanguage.ChineseSimplified: return "子照片已重置";
                    case SystemLanguage.Spanish: return "Fotos secundarias restablecidas";
                    default: return "Sub photos reset";
                }

            case "enable_location_service":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "위치 서비스를 활성화해주세요";
                    case SystemLanguage.Japanese: return "位置サービスを有効にしてください";
                    case SystemLanguage.Chinese: return "请启用位置服务";
                    case SystemLanguage.ChineseSimplified: return "请启用位置服务";
                    case SystemLanguage.Spanish: return "Por favor active el servicio de ubicación";
                    default: return "Please enable location service";
                }

            case "file_too_large":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "파일 크기가 10MB를 초과합니다";
                    case SystemLanguage.Japanese: return "ファイルサイズが10MBを超えています";
                    case SystemLanguage.Chinese: return "文件大小超过10MB";
                    case SystemLanguage.ChineseSimplified: return "文件大小超过10MB";
                    case SystemLanguage.Spanish: return "El tamaño del archivo excede 10MB";
                    default: return "File size exceeds 10MB";
                }

            case "upload_success":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "3D 모델 업로드 성공!";
                    case SystemLanguage.Japanese: return "3Dモデルのアップロードが成功しました！";
                    case SystemLanguage.Chinese: return "3D模型上传成功！";
                    case SystemLanguage.ChineseSimplified: return "3D模型上传成功！";
                    case SystemLanguage.Spanish: return "¡Modelo 3D subido exitosamente!";
                    default: return "3D model uploaded successfully!";
                }

            case "server_error":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "서버 오류가 발생했습니다";
                    case SystemLanguage.Japanese: return "サーバーエラーが発生しました";
                    case SystemLanguage.Chinese: return "发生服务器错误";
                    case SystemLanguage.ChineseSimplified: return "发生服务器错误";
                    case SystemLanguage.Spanish: return "Error del servidor";
                    default: return "Server error occurred";
                }

            case "request_timeout":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "요청 시간이 초과되었습니다";
                    case SystemLanguage.Japanese: return "リクエストがタイムアウトしました";
                    case SystemLanguage.Chinese: return "请求超时";
                    case SystemLanguage.ChineseSimplified: return "请求超时";
                    case SystemLanguage.Spanish: return "Tiempo de espera agotado";
                    default: return "Request timeout";
                }

            case "no_location_data":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "위치 정보 없음";
                    case SystemLanguage.Japanese: return "位置情報がありません";
                    case SystemLanguage.Chinese: return "无位置信息";
                    case SystemLanguage.ChineseSimplified: return "无位置信息";
                    case SystemLanguage.Spanish: return "Sin información de ubicación";
                    default: return "No location data";
                }

            case "submitting_countdown":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "제출 중... {0}초 남음";
                    case SystemLanguage.Japanese: return "送信中... {0}秒残り";
                    case SystemLanguage.Chinese: return "提交中... 剩余{0}秒";
                    case SystemLanguage.ChineseSimplified: return "提交中... 剩余{0}秒";
                    case SystemLanguage.Spanish: return "Enviando... {0} segundos restantes";
                    default: return "Submitting... {0} seconds remaining";
                }

            case "permission_denied":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "저장소 권한이 거부되었습니다";
                    case SystemLanguage.Japanese: return "ストレージ権限が拒否されました";
                    case SystemLanguage.Chinese: return "存储权限被拒绝";
                    case SystemLanguage.ChineseSimplified: return "存储权限被拒绝";
                    case SystemLanguage.Spanish: return "Permiso de almacenamiento denegado";
                    default: return "Storage permission denied";
                }

            case "login_required_for_upload":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "업로드하려면 로그인이 필요합니다";
                    case SystemLanguage.Japanese: return "アップロードするにはログインが必要です";
                    case SystemLanguage.Chinese: return "上传需要登录";
                    case SystemLanguage.ChineseSimplified: return "上传需要登录";
                    case SystemLanguage.Spanish: return "Inicie sesión para subir";
                    default: return "Login required to upload";
                }

            case "daily_upload_limit_reached":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "오늘 업로드 횟수를 초과했습니다 (하루 1회)";
                    case SystemLanguage.Japanese: return "本日のアップロード回数を超過しました（1日1回）";
                    case SystemLanguage.Chinese: return "已超过今日上传次数（每日1次）";
                    case SystemLanguage.ChineseSimplified: return "已超过今日上传次数（每日1次）";
                    case SystemLanguage.Spanish: return "Límite de carga diaria alcanzado (1 vez al día)";
                    default: return "Daily upload limit reached (once per day)";
                }

            default:
                return key;
        }
    }

    #endregion

    #region Lifecycle Events

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            if (locationInput != null) locationInput.text = GetLocalizedText("loading_location");
            StartCoroutine(InitializeLocationService());
        }
    }

    private void OnDestroy()
    {
        if (Input.location.status == LocationServiceStatus.Running)
        {
            Input.location.Stop();
        }

        foreach (var photo in subPhotos)
        {
            if (photo != null) Destroy(photo);
        }
        subPhotos.Clear();

        if (subPhotoDisplays != null)
        {
            foreach (var display in subPhotoDisplays)
            {
                if (display != null)
                {
                    if (display.sprite != null) Destroy(display.sprite);
                    Destroy(display.gameObject);
                }
            }
            subPhotoDisplays.Clear();
        }
    }

    #endregion

    #region Daily Upload Limit

    /// <summary>
    /// 하루 1회 업로드 제한 체크
    /// </summary>
    private IEnumerator CheckDailyUploadLimit()
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            canUploadToday = false;
            yield break;
        }

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.MAIN_SERVER}/api/upload/can-upload?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<CanUploadResponse>(request.downloadHandler.text);
                    canUploadToday = response.can_upload;
                }
                catch
                {
                    canUploadToday = true; // 파싱 실패 시 기본값
                }
            }
            else
            {
                canUploadToday = true; // 네트워크 오류 시 업로드 허용
            }
        }
    }

    /// <summary>
    /// 업로드 성공 시 기록 저장
    /// </summary>
    private IEnumerator RecordUploadSuccess(int locationId)
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
            yield break;

        string userId = LoginManager.Instance.CurrentUser.id;
        string url = $"{ApiConfig.MAIN_SERVER}/api/upload/record";

        var postData = new UploadRecordRequest
        {
            user_id = userId,
            location_id = locationId
        };

        string json = JsonUtility.ToJson(postData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ModelUploadManager] 업로드 기록 저장 실패: {request.error}");
            }
        }
    }

    [System.Serializable]
    private class CanUploadResponse
    {
        public bool can_upload;
        public bool already_uploaded_today;
    }

    [System.Serializable]
    private class UploadRecordRequest
    {
        public string user_id;
        public int location_id;
    }

    [System.Serializable]
    private class UploadResponse
    {
        public string status;
        public int location_id;
        public string message;
    }

    /// <summary>
    /// 서버 응답에서 location_id 파싱
    /// </summary>
    private int ParseLocationIdFromResponse(string responseText)
    {
        try
        {
            var response = JsonUtility.FromJson<UploadResponse>(responseText);
            if (response != null && response.location_id > 0)
            {
                return response.location_id;
            }
        }
        catch (System.Exception)
        {
            // JSON 파싱 실패 시 정규식으로 추출 시도
        }

        var match = Regex.Match(responseText, @"""location_id""\s*:\s*(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int id))
        {
            return id;
        }

        return 0;
    }

    #endregion
}