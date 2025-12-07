using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using System;
using System.Text.RegularExpressions;

public class CubeUploadManager : MonoBehaviour
{
    [SerializeField] private InputField nameInput;
    [SerializeField] private InputField locationInput;
    [SerializeField] private Button mainPhotoButton;
    [SerializeField] private Image mainPhotoDisplay;
    [SerializeField] private Button subPhotosButton;
    [SerializeField] private GridLayoutGroup subPhotoGrid;
    [SerializeField] private Button resetPhotosButton;
    [SerializeField] private Toggle petFriendlyToggle;
    [SerializeField] private Toggle separateRestroomToggle;
    [SerializeField] private Toggle instagramToggle;
    [SerializeField] private InputField instagramIDInput;
    [SerializeField] private Button submitButton;

    [SerializeField] private GameObject warningObj;
    [SerializeField] private GameObject uploadPage;
    [SerializeField] private GameObject disableObject;
    
    [Header("Progress UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Text loadingText;
    [SerializeField] private Image loadingSpinner;

    [SerializeField] private string serverUrl = "https://woopang.com:5000/upload/";

    private Texture2D mainPhoto;
    private List<Texture2D> subPhotos = new List<Texture2D>();
    private List<Image> subPhotoDisplays = new List<Image>();
    private string userName;
    private string instagramID;
    private bool showInstagram;
    private Vector3 gpsData = Vector3.zero;
    private string locationText;
    private const int MAX_SUB_PHOTOS = 10;
    private bool isProcessing = false;
    private float elapsedTime = 0f;
    
    // 스와이프 패널 상태 저장용
    private SwipePanelController swipePanelController;
    private int savedCurrentPanel = -1;

    private void Awake()
    {
        if (FindObjectsOfType<CubeUploadManager>().Length > 1)
        {
            Destroy(gameObject);
        }
        else
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        InitializeComponents();
        ResetToInitialState();
        StartCoroutine(InitializeLocationService());
        
        // SwipePanelController 찾기
        swipePanelController = FindObjectOfType<SwipePanelController>();
        if (swipePanelController == null)
        {
            Debug.LogWarning("[CubeUploadManager] SwipePanelController를 찾을 수 없습니다.");
        }
    }

    private void InitializeComponents()
    {
        if (instagramToggle != null) instagramToggle.onValueChanged.AddListener(OnInstagramToggleChanged);
        if (mainPhotoButton != null) mainPhotoButton.onClick.AddListener(() => StartCoroutine(SelectAndCropMainPhoto()));
        if (subPhotosButton != null) subPhotosButton.onClick.AddListener(() => StartCoroutine(SelectSubPhotos()));
        if (resetPhotosButton != null) resetPhotosButton.onClick.AddListener(ResetSubPhotos);
        if (submitButton != null) submitButton.onClick.AddListener(() => StartCoroutine(ValidateAndSubmit()));

        if (locationInput != null)
        {
            locationInput.interactable = true;
            locationInput.image.color = Color.white;
        }

        SetUIActive(warningObj, false);
        SetUIActive(loadingPanel, false);

        InitializeObjectPool();

        if (uploadPage == null) Debug.LogError("UploadPage가 할당되지 않았습니다! Inspector에서 설정해주세요.");
        if (disableObject == null) Debug.LogError("DisableObject가 할당되지 않았습니다! Inspector에서 설정해주세요.");
    }

    #region HEIC Processing Methods

    /// <summary>
    /// 메인 사진 선택 및 처리 (2단계 HEIC 처리 포함)
    /// </summary>
    private IEnumerator SelectAndCropMainPhoto()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        ShowSpinner(LocalizationManager.Instance.GetText("loading_main_photo"));
        bool isLoading = true;

        try
        {
            NativeGallery.GetImageFromGallery((path) =>
            {
                if (!string.IsNullOrEmpty(path))
                {
                    StartCoroutine(ProcessMainPhotoWithFallback(path, () => { isLoading = false; }));
                }
                else
                {
                    ShowWarning(LocalizationManager.Instance.GetText("photo_selection_failed"));
                    SetMainPhotoUIState(false);
                    isLoading = false;
                }
            }, "image/*");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CubeUploadManager] NativeGallery 호출 오류: {e}");
            ShowWarning(LocalizationManager.Instance.GetText("photo_selection_failed"));
            SetMainPhotoUIState(false);
            isLoading = false;
        }

        yield return new WaitUntil(() => !isLoading);
        HideSpinner();
        isProcessing = false;
    }

    /// <summary>
    /// 2단계 이미지 처리: 1차 네이티브 시도 → 2차 수동 변환
    /// </summary>
    private IEnumerator ProcessMainPhotoWithFallback(string imagePath, System.Action onComplete)
    {
        Debug.Log($"[HEIC] 메인사진 처리 시작: {imagePath}");
        Debug.Log($"[HEIC] 파일 확장자: {Path.GetExtension(imagePath).ToLower()}");

        Texture2D loadedTexture = null;
        bool processingComplete = false;
        bool step1Success = false;

        // 1단계: NativeGallery의 LoadImageAtPath 시도 (가장 빠름)
        try
        {
            loadedTexture = NativeGallery.LoadImageAtPath(imagePath, 
                maxSize: 2048, 
                markTextureNonReadable: false, 
                generateMipmaps: false);

            if (loadedTexture != null)
            {
                Debug.Log($"[HEIC] ✅ 1단계 성공 - NativeGallery 로드: {loadedTexture.width}x{loadedTexture.height}");
                step1Success = true;
            }
            else
            {
                Debug.LogWarning("[HEIC] ⚠️ 1단계 실패 - 2단계 수동 변환 시도");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[HEIC] ⚠️ 1단계 예외 - 2단계 시도: {e.Message}");
        }

        if (step1Success)
        {
            ProcessCropAndDisplay(loadedTexture, () => {
                processingComplete = true;
                onComplete?.Invoke();
            });
            
            yield return new WaitUntil(() => processingComplete);
            yield break;
        }

        // 2단계: 수동 HEIC 변환 (안전장치)
        yield return StartCoroutine(LoadImageWithConversion(imagePath, (texture) =>
        {
            if (texture != null)
            {
                Debug.Log($"[HEIC] ✅ 2단계 성공 - 수동 변환: {texture.width}x{texture.height}");
                ProcessCropAndDisplay(texture, () => {
                    processingComplete = true;
                    onComplete?.Invoke();
                });
            }
            else
            {
                Debug.LogError("[HEIC] ❌ 모든 단계 실패");
                ShowWarning(LocalizationManager.Instance.GetText("photo_selection_failed"));
                SetMainPhotoUIState(false);
                processingComplete = true;
                onComplete?.Invoke();
            }
        }));

        yield return new WaitUntil(() => processingComplete);
    }

    /// <summary>
    /// 크롭 및 UI 표시 처리
    /// </summary>
    private void ProcessCropAndDisplay(Texture2D texture, System.Action onComplete)
    {
        // 크롭 시작 전에 현재 패널 상태 저장
        SaveCurrentPanelState();
        
        ImageCropper.Instance.Show(texture, (success, original, cropped) =>
        {
            try
            {
                if (success && cropped is Texture2D croppedTexture)
                {
                    if (mainPhoto != null) Destroy(mainPhoto);
                    mainPhoto = croppedTexture;
                    if (mainPhotoDisplay != null) mainPhotoDisplay.sprite = GetOrCreateSprite(mainPhoto);
                    SetMainPhotoUIState(true);
                    Debug.Log("[HEIC] 메인 사진 크롭 완료");
                }
                else
                {
                    ShowWarning(LocalizationManager.Instance.GetText("main_photo_crop_failed"));
                    SetMainPhotoUIState(false);
                }
                
                // 원본 텍스처 정리 (크롭된 버전만 유지)
                if (texture != cropped && texture != null) Destroy(texture);
            }
            finally
            {
                // 크롭 완료 후 패널 상태 복원
                RestoreCurrentPanelState();
                
                // 반드시 콜백 호출하여 로딩 상태 해제
                onComplete?.Invoke();
            }
        }, new ImageCropper.Settings
        {
            autoZoomEnabled = true,
            selectionMinAspectRatio = 1.0f,
            selectionMaxAspectRatio = 1.0f
        });
    }

    /// <summary>
    /// 서브 사진 선택 및 처리
    /// </summary>
    private IEnumerator SelectSubPhotos()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        ShowSpinner(LocalizationManager.Instance.GetText("loading_sub_photos"));
        bool isLoading = true;

        try
        {
            NativeGallery.GetImagesFromGallery((paths) =>
            {
                if (paths != null && paths.Length > 0)
                {
                    if (subPhotos.Count + paths.Length > MAX_SUB_PHOTOS)
                    {
                        ShowWarning(LocalizationManager.Instance.GetText("max_sub_photos_exceeded"));
                        isLoading = false;
                    }
                    else
                    {
                        StartCoroutine(LoadMultipleImagesWithFallback(paths, () => { isLoading = false; }));
                    }
                }
                else
                {
                    ShowWarning(LocalizationManager.Instance.GetText("photo_selection_failed"));
                    isLoading = false;
                }
            }, "image/*");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CubeUploadManager] NativeGallery 다중 선택 오류: {e}");
            ShowWarning(LocalizationManager.Instance.GetText("photo_selection_failed"));
            isLoading = false;
        }

        yield return new WaitUntil(() => !isLoading);
        HideSpinner();
        UpdateSubPhotoGrid();
        isProcessing = false;
    }

    /// <summary>
    /// 다중 이미지 로드 (2단계 처리)
    /// </summary>
    private IEnumerator LoadMultipleImagesWithFallback(string[] paths, System.Action onComplete)
    {
        Debug.Log($"[HEIC] 서브사진 처리 시작: {paths.Length}장");
        
        foreach (string path in paths)
        {
            if (subPhotos.Count >= MAX_SUB_PHOTOS)
            {
                ShowWarning(LocalizationManager.Instance.GetText("max_sub_photos_exceeded"));
                break;
            }

            Debug.Log($"[HEIC] 서브사진 처리: {path}");
            
            // 1단계: NativeGallery.LoadImageAtPath 시도
            Texture2D texture = NativeGallery.LoadImageAtPath(path, 
                maxSize: 1024,  // 서브사진은 더 작게
                markTextureNonReadable: false,
                generateMipmaps: false);
            
            if (texture != null)
            {
                subPhotos.Add(texture);
                Debug.Log($"[HEIC] ✅ 서브사진 1단계 성공: {path} (총 {subPhotos.Count}장)");
            }
            else
            {
                // 2단계: 수동 변환 시도
                Debug.LogWarning($"[HEIC] ⚠️ 서브사진 1단계 실패, 2단계 시도: {path}");
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
                    Debug.Log($"[HEIC] ✅ 서브사진 2단계 성공: {path} (총 {subPhotos.Count}장)");
                }
                else
                {
                    Debug.LogError($"[HEIC] ❌ 서브사진 모든 단계 실패: {path}");
                    ShowWarning($"이미지 로드 실패: {Path.GetFileName(path)}");
                }
            }

            yield return null; // 한 프레임 대기
        }

        Debug.Log($"[HEIC] 서브사진 처리 완료: 총 {subPhotos.Count}장 로드됨");
        UpdateSubPhotoGrid();
        onComplete?.Invoke();
    }

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

        Debug.Log("[HEIC] iOS HEIC 변환 시작");

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
        Texture2D texture = null;
        bool loadSuccess = false;
        
        try
        {
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            
            if (texture.LoadImage(imageBytes))
            {
                Debug.Log($"[HEIC] 직접 로드 성공: {texture.width}x{texture.height}");
                loadSuccess = true;
            }
            else
            {
                Debug.LogError("[HEIC] 직접 로드 실패");
                if (texture != null) Destroy(texture);
                texture = null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HEIC] 직접 로드 예외: {e.Message}");
            if (texture != null) Destroy(texture);
            texture = null;
        }
        
        onComplete?.Invoke(texture);
        yield return null;
    }

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>
    /// iOS 네이티브 HEIC → JPG 변환
    /// </summary>
    private IEnumerator ConvertHEICToJPG(string heicPath, System.Action<Texture2D> onComplete)
    {
        Texture2D result = null;
        
        try
        {
            // 원본 HEIC 파일 읽기
            byte[] heicBytes = File.ReadAllBytes(heicPath);
            Texture2D tempTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            
            // Unity로 직접 로드 시도 (실패할 가능성 높음)
            if (tempTexture.LoadImage(heicBytes))
            {
                Debug.Log("[HEIC] 예상외로 Unity 직접 로드 성공");
                result = tempTexture;
            }
            else
            {
                // 실패 시 JPG 변환
                Destroy(tempTexture);
                
                // 실제 변환은 NativeGallery 내부 로직에 의존
                // 여기서는 기본 변환 시도
                byte[] jpgBytes = ConvertToJPGBytes(heicBytes);
                
                if (jpgBytes != null && jpgBytes.Length > 0)
                {
                    string directory = Path.GetDirectoryName(heicPath);
                    string fileName = Path.GetFileNameWithoutExtension(heicPath);
                    string jpgPath = Path.Combine(directory, $"{fileName}_converted.jpg");
                    
                    File.WriteAllBytes(jpgPath, jpgBytes);
                    
                    Texture2D convertedTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    if (convertedTexture.LoadImage(jpgBytes))
                    {
                        Debug.Log($"[HEIC] JPG 변환 성공: {convertedTexture.width}x{convertedTexture.height}");
                        result = convertedTexture;
                        
                        // 임시 파일 삭제
                        try { File.Delete(jpgPath); } catch { }
                    }
                    else
                    {
                        Destroy(convertedTexture);
                    }
                }
                
                if (result == null)
                {
                    Debug.LogError("[HEIC] JPG 변환 실패");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HEIC] 변환 중 예외: {e.Message}");
            result = null;
        }
        
        onComplete?.Invoke(result);
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

    #region UI State Management

    private void SetMainPhotoUIState(bool hasPhoto)
    {
        if (mainPhotoDisplay != null) mainPhotoDisplay.gameObject.SetActive(hasPhoto);
    }

    private void SetUIActive(GameObject uiElement, bool active)
    {
        if (uiElement != null) uiElement.SetActive(active);
    }

    private void SetUIText(Text uiText, string text)
    {
        if (uiText != null) uiText.text = text;
    }

    public void ShowWarning(string message)
    {
        Text warningText = warningObj?.GetComponentInChildren<Text>();
        if (warningText != null)
        {
            warningText.text = message;
        }
        SetUIActive(warningObj, true);
        CancelInvoke("HideWarning");
        Invoke("HideWarning", 2f);
    }

    private void HideWarning()
    {
        SetUIActive(warningObj, false);
    }

    private void ShowSpinner(string message)
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }
        
        if (loadingText != null)
        {
            loadingText.text = message;
        }
        
        if (loadingSpinner != null)
        {
            StartCoroutine(SpinnerAnimation());
        }
        
        Debug.Log($"[CubeUploadManager] {message}");
    }
    
    private void HideSpinner()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
        
        // 스피너 애니메이션 코루틴 안전하게 정지
        try
        {
            StopCoroutine(SpinnerAnimation());
        }
        catch (System.Exception)
        {
            // 코루틴이 이미 정지된 경우 무시
        }
        
        Debug.Log("[CubeUploadManager] 스피너 숨김 완료");
    }
    
    private IEnumerator SpinnerAnimation()
    {
        while (loadingPanel && loadingPanel.activeInHierarchy && loadingSpinner)
        {
            loadingSpinner.transform.Rotate(0, 0, -90 * Time.deltaTime);
            yield return null;
        }
    }

    #endregion

    #region Location Services

    private IEnumerator InitializeLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("위치 서비스가 꺼져 있습니다! 설정에서 활성화해주세요.");
            UpdateLocationDisplay();
            yield break;
        }

        Input.location.Start();
        int maxWait = 10;
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
            Debug.Log("위치 서비스 초기화 성공!");
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
                Debug.LogWarning("위치 서비스가 실행 중이 아님 - 재시작 시도");
                StartCoroutine(InitializeLocationService());
            }
            yield return new WaitForSeconds(interval);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Debug.Log("앱이 포그라운드로 전환됨 - 위치 서비스 재시작");
            if (locationInput != null) locationInput.text = LocalizationManager.Instance.GetText("loading_location");
            StartCoroutine(InitializeLocationService());
        }
    }

    private void OnDestroy()
    {
        Input.location.Stop();
    }

    private void UpdateLocationDisplay()
    {
        if (Input.location.status == LocationServiceStatus.Running)
        {
            gpsData = new Vector3(
                Input.location.lastData.latitude,
                Input.location.lastData.longitude,
                Input.location.lastData.altitude
            );
            // UI 표시용은 F4 (고도는 F2 유지)
            locationText = $"Lat:{gpsData.x:F4},Lon:{gpsData.y:F4},Alt:{gpsData.z:F2}";
            
            Debug.Log($"GPS updated: {locationText}");
        }
        else
        {
            gpsData = Vector3.zero;
            locationText = LocalizationManager.Instance.GetText("no_location_data");
            Debug.LogWarning($"위치 서비스가 실행 중이 아님 - 상태: {Input.location.status}, isEnabledByUser: {Input.location.isEnabledByUser}");
        }
        if (locationInput != null) locationInput.text = locationText;
    }

    #endregion

    #region Instagram and UI Events

    private void OnInstagramToggleChanged(bool value)
    {
        showInstagram = value;
        if (instagramIDInput != null)
        {
            instagramIDInput.interactable = value;
            instagramIDInput.image.color = value ? Color.white : Color.gray;
            if (!value) instagramIDInput.text = "";
        }
    }

    #endregion

    #region Photo Management

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

    private void ResetSubPhotos()
    {
        foreach (var photo in subPhotos)
        {
            Destroy(photo);
        }
        subPhotos.Clear();
        UpdateSubPhotoGrid();
        ShowWarning(LocalizationManager.Instance.GetText("sub_photos_reset"));
        CancelInvoke("HideWarning");
        Invoke("HideWarning", 2f);
    }

    private void UpdateSubPhotoGrid()
    {
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
        Debug.Log($"[CubeUploadManager] 서브사진 그리드 업데이트: {subPhotos.Count}장");
    }

    private Sprite GetOrCreateSprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    #endregion

    #region Validation and Upload

    private IEnumerator ValidateAndSubmit()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        userName = nameInput?.text.Trim() ?? "";
        instagramID = showInstagram ? instagramIDInput?.text.Trim() ?? "" : "";
        locationText = locationInput?.text.Trim() ?? "";

        if (string.IsNullOrEmpty(userName))
        {
            ShowWarning(LocalizationManager.Instance.GetText("enter_name"));
            isProcessing = false;
            yield break;
        }

        if (Input.location.status != LocationServiceStatus.Running || gpsData == Vector3.zero)
        {
            ShowWarning(LocalizationManager.Instance.GetText("enable_location_service"));
            isProcessing = false;
            yield break;
        }

        if (mainPhoto == null)
        {
            ShowWarning(LocalizationManager.Instance.GetText("upload_logo_photo"));
            isProcessing = false;
            yield break;
        }

        if (subPhotos.Count == 0)
        {
            ShowWarning(LocalizationManager.Instance.GetText("upload_min_one_photo"));
            isProcessing = false;
            yield break;
        }

        if (showInstagram && string.IsNullOrEmpty(instagramID))
        {
            ShowWarning(LocalizationManager.Instance.GetText("enter_instagram_id"));
            isProcessing = false;
            yield break;
        }

        if (showInstagram && !Regex.IsMatch(instagramID, @"^[a-zA-Z0-9_.]+$"))
        {
            ShowWarning(LocalizationManager.Instance.GetText("instagram_id_invalid"));
            isProcessing = false;
            yield break;
        }

        Coroutine countdownCoroutine = StartCoroutine(ShowCountdownWarning(10));
        yield return StartCoroutine(SendWithTimeout(
            ProcessAndUploadImages(
                this, userName, instagramID, showInstagram, gpsData, locationText,
                petFriendlyToggle?.isOn ?? false, separateRestroomToggle?.isOn ?? false,
                countdownCoroutine)));

        if (!isProcessing)
        {
            Debug.Log("[CubeUploadManager] 업로드 완료 후 추가 대기");
            yield return new WaitForSeconds(2f);
        }
        else
        {
            Debug.Log("[CubeUploadManager] 업로드 실패로 인해 추가 대기 없음");
        }
        isProcessing = false;
    }

    private IEnumerator ShowCountdownWarning(int seconds)
    {
        for (int i = seconds; i >= 1; i--)
        {
            ShowWarning(LocalizationManager.Instance.GetText("submitting_countdown").Replace("{0}", i.ToString()));
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator SendWithTimeout(IEnumerator routine)
    {
        float timeout = 10f;
        elapsedTime = 0f;
        bool isCompleted = false;

        Coroutine co = StartCoroutine(routine);
        yield return StartCoroutine(WaitForRoutine(co, timeout, () => isCompleted));

        if (!isProcessing)
        {
            isCompleted = true;
            Debug.Log("[CubeUploadManager] SendWithTimeout: 업로드 성공으로 완료 플래그 설정");
        }
        else
        {
            Debug.Log("[CubeUploadManager] SendWithTimeout: 업로드 실패로 완료 플래그 미설정");
        }
    }

    private IEnumerator WaitForRoutine(Coroutine routine, float timeout, Func<bool> isCompleted)
    {
        while (routine != null && elapsedTime < timeout && !isCompleted() && isProcessing)
        {
            elapsedTime += Time.deltaTime;
            Debug.Log($"[CubeUploadManager] WaitForRoutine: 대기 중... Elapsed: {elapsedTime:F2}초 / Timeout: {timeout}초");
            yield return null;
        }

        if (routine != null && !isCompleted() && elapsedTime >= timeout)
        {
            Debug.LogWarning($"[CubeUploadManager] 타임아웃 발생: Elapsed {elapsedTime:F2}초, Timeout: {timeout}초");
            StopCoroutine(routine);
            isProcessing = true;
            ShowWarning(LocalizationManager.Instance.GetText("request_timeout"));
        }
        else if (isCompleted())
        {
            Debug.Log("[CubeUploadManager] WaitForRoutine: 업로드 성공으로 루프 종료");
        }
        else
        {
            Debug.Log("[CubeUploadManager] WaitForRoutine: 루프 종료 (기타 이유)");
        }
    }

    private IEnumerator ProcessAndUploadImages(
        CubeUploadManager form, string userName, string instagramID, bool showInstagram,
        Vector3 gpsData, string locationText, bool petFriendly, bool separateRestroom,
        Coroutine countdownCoroutine)
    {
        ShowSpinner(LocalizationManager.Instance.GetText("uploading_object"));
        
        WWWForm formData = new WWWForm();

        formData.AddField("username", string.IsNullOrEmpty(userName) ? "" : userName);
        formData.AddField("name", userName);
        formData.AddField("latitude", gpsData.x.ToString("F6"));
        formData.AddField("longitude", gpsData.y.ToString("F6"));
        formData.AddField("altitude", gpsData.z.ToString("F2"));
        formData.AddField("pet_friendly", petFriendly ? "true" : "false");
        formData.AddField("separate_restroom", separateRestroom ? "true" : "false");
        formData.AddField("instagram_id", showInstagram ? instagramID : "");
        formData.AddField("status", "approved");
        
        formData.AddField("timezone", GetTimezone());
        formData.AddField("timezone_offset", GetTimezoneOffset());
        
        string folder = $"{DateTime.Now:yyyyMMdd_HHmmss}_{userName}";
        Debug.Log($"[CubeUploadManager] Folder value before sending: {folder}");
        formData.AddField("folder", folder);

        Texture2D mainPhoto = GetMainPhoto();
        if (mainPhoto != null)
        {
            Debug.Log($"[CubeUploadManager] 메인 사진 데이터 확인: 크기 {mainPhoto.width}x{mainPhoto.height}, 포맷 {mainPhoto.format}");
            Texture2D resizedMainPhoto = ResizeTextureWithRenderTexture(mainPhoto, 444, 444);
            byte[] mainPhotoBytes = resizedMainPhoto.EncodeToJPG(50);
            Debug.Log($"[CubeUploadManager] 메인 사진 바이트 크기: {mainPhotoBytes.Length} bytes");
            if (mainPhotoBytes.Length == 0)
            {
                Debug.LogError("[CubeUploadManager] 메인 사진 데이터가 비어 있습니다!");
                ShowWarning(LocalizationManager.Instance.GetText("main_photo_upload_failed"));
                HideSpinner();
                yield break;
            }
            string mainPath = "main.jpg";
            formData.AddBinaryData("main_photo", mainPhotoBytes, mainPath, "image/jpeg");
            Destroy(resizedMainPhoto);
        }
        else
        {
            Debug.LogWarning("[CubeUploadManager] 메인 사진이 없습니다!");
            ShowWarning(LocalizationManager.Instance.GetText("upload_logo_photo"));
            HideSpinner();
            yield break;
        }

        List<Texture2D> subPhotos = GetSubPhotos();
        Debug.Log($"[CubeUploadManager] 서브 사진 개수: {subPhotos.Count}");
        for (int i = 1; i <= subPhotos.Count; i++)
        {
            if (i > MAX_SUB_PHOTOS) break;
            Texture2D subPhoto = subPhotos[i - 1];
            Debug.Log($"[CubeUploadManager] 서브 사진 #{i} 데이터 확인: 크기 {subPhoto.width}x{subPhoto.height}, 포맷 {subPhoto.format}");
            Texture2D resizedSubPhoto = ResizeTextureKeepAspectWithRenderTexture(subPhoto, 800, 800);
            byte[] subPhotoBytes = resizedSubPhoto.EncodeToJPG(50);
            Debug.Log($"[CubeUploadManager] 서브 사진 #{i} 바이트 크기: {subPhotoBytes.Length} bytes");
            if (subPhotoBytes.Length == 0)
            {
                Debug.LogError($"[CubeUploadManager] 서브 사진 #{i} 데이터가 비어 있습니다!");
                ShowWarning(LocalizationManager.Instance.GetText("sub_photo_upload_failed"));
                Destroy(resizedSubPhoto);
                continue;
            }
            string subPath = $"sub_{i}.jpg";
            formData.AddBinaryData("sub_photos", subPhotoBytes, subPath, "image/jpeg");
            Destroy(resizedSubPhoto);
        }

        Debug.Log($"[CubeUploadManager] 서버로 요청 전송: {serverUrl}");
        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, formData))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();

            HideSpinner();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;
                Debug.Log($"[CubeUploadManager] 업로드 응답: {responseText} (응답 코드: {www.responseCode})");

                if (responseText.Contains("Upload Succeeded!") || www.responseCode == 200)
                {
                    isProcessing = false;
                    StopCoroutine(countdownCoroutine);
                    ShowWarning(LocalizationManager.Instance.GetText("upload_success"));
                    Debug.Log("[CubeUploadManager] 업로드 성공: 루틴 종료");

                    FullReset();
                    yield break;
                }
                else
                {
                    Debug.LogWarning($"[CubeUploadManager] 서버 응답이 성공으로 간주되지 않음: {responseText}");
                    isProcessing = true;
                    ShowWarning(LocalizationManager.Instance.GetText("server_error"));
                }
            }
            else
            {
                Debug.LogError($"[CubeUploadManager] 업로드 실패: {www.error} (응답 코드: {www.responseCode})");
                isProcessing = true;
                ShowWarning(LocalizationManager.Instance.GetText("server_error"));
            }
        }
    }

    #endregion

    #region Utility Methods

    private string GetTimezone()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return "Asia/Seoul";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
                return "Asia/Shanghai";
            case SystemLanguage.Japanese:
                return "Asia/Tokyo";
            case SystemLanguage.Spanish:
                return "America/Madrid";
            case SystemLanguage.English:
            default:
                return "UTC";
        }
    }

    private string GetTimezoneOffset()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return "+09:00";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
                return "+08:00";
            case SystemLanguage.Japanese:
                return "+09:00";
            case SystemLanguage.Spanish:
                return "+01:00";
            case SystemLanguage.English:
            default:
                return "+00:00";
        }
    }

    private void FullReset()
    {
        if (mainPhoto != null)
        {
            Destroy(mainPhoto);
            mainPhoto = null;
        }
        foreach (var photo in subPhotos)
        {
            Destroy(photo);
        }
        subPhotos.Clear();

        ResetToInitialState();

        StopAllCoroutines();
        StartCoroutine(InitializeLocationService());

        Debug.Log("[CubeUploadManager] 애플리케이션 완전 초기화 완료 (어플 재시작 상태)");
    }

    private void ResetToInitialState()
    {
        if (uploadPage != null)
        {
            uploadPage.SetActive(false);
            Debug.Log("[CubeUploadManager] UploadPage 비활성화 완료");
        }
        else
        {
            Debug.LogError("[CubeUploadManager] UploadPage가 null입니다. 비활성화 실패!");
        }

        if (disableObject != null)
        {
            disableObject.SetActive(false);
            Debug.Log("[CubeUploadManager] DisableObject 비활성화 완료");
        }
        else
        {
            Debug.LogError("[CubeUploadManager] DisableObject가 null입니다. 비활성화 실패!");
        }

        SetMainPhotoUIState(false);
        if (mainPhotoDisplay != null) mainPhotoDisplay.sprite = null;
        
        if (subPhotoGrid != null)
        {
            foreach (var display in subPhotoDisplays)
            {
                display.sprite = null;
                display.gameObject.SetActive(false);
            }
        }

        if (nameInput != null) nameInput.text = "";
        if (instagramIDInput != null) instagramIDInput.text = "";
        if (locationInput != null)
        {
            locationInput.text = LocalizationManager.Instance.GetText("loading_location");
            locationInput.interactable = true;
            locationInput.image.color = Color.white;
        }

        if (petFriendlyToggle != null) petFriendlyToggle.isOn = false;
        if (separateRestroomToggle != null) separateRestroomToggle.isOn = false;
        if (instagramToggle != null)
        {
            instagramToggle.isOn = false;
            showInstagram = false;
            if (instagramIDInput != null)
            {
                instagramIDInput.interactable = false;
                instagramIDInput.image.color = Color.gray;
            }
        }

        userName = "";
        instagramID = "";
        gpsData = Vector3.zero;
        isProcessing = false;
        elapsedTime = 0f;

        Debug.Log("[CubeUploadManager] UI 및 상태 초기화 완료");
    }

    private Texture2D ResizeTextureWithRenderTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

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

    #endregion

    #region Public Getters

    public Texture2D GetMainPhoto() => mainPhoto;
    public List<Texture2D> GetSubPhotos() => subPhotos;
    public string GetLocationText() => locationText;
    public Vector3 GetGpsData() => gpsData;

    #endregion
    
    #region Panel State Management
    
    /// <summary>
    /// 크롭 시작 전 현재 패널 상태 저장
    /// </summary>
    private void SaveCurrentPanelState()
    {
        if (swipePanelController != null)
        {
            savedCurrentPanel = swipePanelController.GetCurrentPanel();
            Debug.Log($"[CubeUploadManager] 패널 상태 저장: {savedCurrentPanel}");
        }
    }
    
    /// <summary>
    /// 크롭 완료 후 저장된 패널 상태 복원
    /// </summary>
    private void RestoreCurrentPanelState()
    {
        if (swipePanelController != null && savedCurrentPanel >= 0)
        {
            // 약간의 딜레이 후 복원 (UI 업데이트 대기)
            StartCoroutine(RestoreCurrentPanelStateDelayed());
        }
    }
    
    /// <summary>
    /// 딜레이를 두고 패널 상태 복원
    /// </summary>
    private IEnumerator RestoreCurrentPanelStateDelayed()
    {
        yield return new WaitForSeconds(0.1f); // 짧은 딜레이
        
        if (swipePanelController != null && savedCurrentPanel >= 0)
        {
            swipePanelController.SetCurrentPanel(savedCurrentPanel);
            Debug.Log($"[CubeUploadManager] 패널 상태 복원: {savedCurrentPanel}");
            savedCurrentPanel = -1; // 복원 후 초기화
        }
    }
    
    #endregion
}