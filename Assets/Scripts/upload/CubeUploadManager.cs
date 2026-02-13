using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using System;
using System.Text.RegularExpressions;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARSubsystems;

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

    [Header("업로드 완료 후 복원")]
    [Tooltip("UploadPage를 여는 버튼 (업로드 완료 시 활성화)")]
    [SerializeField] private GameObject plusButton;

    [Header("Progress UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Text loadingText;
    [SerializeField] private Image loadingSpinner;

    [Header("ARCore Geospatial")]
    [SerializeField] private AREarthManager earthManager; // AREarthManager 컴포넌트 참조
    [SerializeField] private bool useGeospatialAPI = true; // Inspector에서 ON/OFF 가능

    [Header("Photo Dialogs")]
    [SerializeField] private PhotoSourceDialog photoSourceDialog; // 사진 선택 다이얼로그
    [SerializeField] private ContinueCaptureDialog continueCaptureDialog; // 연속 촬영 다이얼로그

    [Header("AR Preview")]
    [SerializeField] private ARPreviewController arPreviewController; // AR 미리보기 컨트롤러 (cubePrefab은 ARPreviewController에서 직접 연결)

    private string serverUrl => ApiConfig.UPLOAD;

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

    // 연속 촬영 모드용
    private List<string> continuousCapturedPaths = new List<string>();

    private void Awake()
    {
        int count = FindObjectsByType<CubeUploadManager>(FindObjectsSortMode.None).Length;

        if (count > 1)
        {
            Destroy(gameObject);
            return;
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
    /// 카메라 촬영 또는 갤러리 선택 다이얼로그 표시
    /// </summary>
    private IEnumerator SelectAndCropMainPhoto()
    {
        if (isProcessing)
        {
            yield break;
        }

        // PhotoSourceDialog 표시
        if (photoSourceDialog != null)
        {
            photoSourceDialog.Show(
                LocalizationManager.Instance?.GetText("select_main_photo") ?? "Select Photo",
                onCamera: () => StartCoroutine(CaptureMainPhotoFromCamera()),
                onGallery: () => StartCoroutine(SelectMainPhotoFromGallery())
            );
        }
        else
        {
            // Fallback: 다이얼로그 없으면 갤러리만 사용
            yield return StartCoroutine(SelectMainPhotoFromGallery());
        }

        yield break;
    }

    /// <summary>
    /// 카메라로 메인 사진 촬영
    /// </summary>
    private IEnumerator CaptureMainPhotoFromCamera()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        ShowSpinner(LocalizationManager.Instance.GetText("loading_main_photo"));
        bool isLoading = true;
        string capturedPath = null;
        bool permissionDenied = false;

        // NativeCamera로 사진 촬영 (내부적으로 권한 처리)
        NativeCamera.TakePicture((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                // 권한 거부 또는 취소
                permissionDenied = true;
            }
            capturedPath = path;
            isLoading = false;
        }, maxSize: 2048);

        yield return new WaitUntil(() => !isLoading);

        if (permissionDenied)
        {
            ShowWarning("카메라 권한이 필요합니다.");
            HideSpinner();
            isProcessing = false;
            yield break;
        }

        if (!string.IsNullOrEmpty(capturedPath))
        {
            yield return StartCoroutine(ProcessMainPhotoWithFallback(capturedPath, () => { }));
        }
        else
        {
            ShowWarning(LocalizationManager.Instance.GetText("photo_selection_failed"));
            SetMainPhotoUIState(false);
        }

        HideSpinner();
        isProcessing = false;
    }

    /// <summary>
    /// 갤러리에서 메인 사진 선택
    /// </summary>
    private IEnumerator SelectMainPhotoFromGallery()
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
            }, LocalizationManager.Instance.GetText("select_main_photo"), "image/*");
        }
        catch (System.Exception)
        {
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
                step1Success = true;
            }
        }
        catch (System.Exception)
        {
            // 1단계 실패 시 2단계 수동 변환 시도
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
                ProcessCropAndDisplay(texture, () => {
                    processingComplete = true;
                    onComplete?.Invoke();
                });
            }
            else
            {
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
        if (texture == null)
        {
            onComplete?.Invoke();
            return;
        }

        // 크롭 시작 전에 현재 패널 상태 저장
        SaveCurrentPanelState();

        // 크롭 화면이 열릴 때 uploadPage 숨기기 (크롭 화면만 보이도록)
        if (uploadPage != null)
            uploadPage.SetActive(false);

        var cropper = ImageCropper.Instance;
        if (cropper == null)
        {
            onComplete?.Invoke();
            return;
        }

        cropper.Show(texture, (success, original, cropped) =>
        {
            bool arPreviewStarted = false;

            try
            {
                if (success && cropped is Texture2D croppedTexture)
                {
                    if (mainPhoto != null) Destroy(mainPhoto);
                    mainPhoto = croppedTexture;
                    if (mainPhotoDisplay != null) mainPhotoDisplay.sprite = GetOrCreateSprite(mainPhoto);
                    SetMainPhotoUIState(true);

                    // AR Preview 모드 시작 (메인 사진 크롭 직후)
                    if (arPreviewController != null)
                    {
                        StartARPreview(croppedTexture);
                        arPreviewStarted = true;
                    }
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
                // AR Preview가 시작되지 않았을 때만 uploadPage 복원
                if (!arPreviewStarted)
                {
                    if (uploadPage != null)
                        uploadPage.SetActive(true);

                    RestoreCurrentPanelState();
                }

                onComplete?.Invoke();
            }
        }, new ImageCropper.Settings
        {
            autoZoomEnabled = false,
            selectionMinAspectRatio = 1.0f,
            selectionMaxAspectRatio = 1.0f
        });
    }

    /// <summary>
    /// AR Preview 모드 시작 (메인 사진 크롭 직후)
    /// "이곳에 오브젝트를 추가하시겠습니까?" 메시지와 함께 AR Preview 표시
    /// </summary>
    private void StartARPreview(Texture2D mainPhotoTexture)
    {
        if (arPreviewController == null)
        {
            return;
        }

        // UploadPage 비활성화
        if (uploadPage != null)
            uploadPage.SetActive(false);

        // AR Preview 시작 (확인/취소 콜백 포함)
        arPreviewController.StartPreview(
            mainPhotoTexture,
            onConfirmCallback: () =>
            {
                // 확인 버튼 클릭 → UploadPage 복귀하여 서브 사진 추가 진행
                if (uploadPage != null)
                    uploadPage.SetActive(true);
            },
            onCancelCallback: () =>
            {
                // 취소 버튼 클릭 → 메인 사진 초기화 후 UploadPage 복귀
                if (mainPhoto != null)
                {
                    Destroy(mainPhoto);
                    mainPhoto = null;
                }
                if (mainPhotoDisplay != null)
                    mainPhotoDisplay.sprite = null;
                SetMainPhotoUIState(false);

                if (uploadPage != null)
                    uploadPage.SetActive(true);
            }
        );
    }

    /// <summary>
    /// 서브 사진 선택 및 처리
    /// 카메라 연속 촬영 또는 갤러리 다중 선택 다이얼로그 표시
    /// </summary>
    private IEnumerator SelectSubPhotos()
    {
        if (isProcessing) yield break;

        // PhotoSourceDialog 표시
        if (photoSourceDialog != null)
        {
            photoSourceDialog.Show(
                LocalizationManager.Instance.GetText("select_sub_photos"),
                onCamera: () => StartCoroutine(StartContinuousCaptureMode()),
                onGallery: () => StartCoroutine(SelectSubPhotosFromGallery())
            );
        }
        else
        {
            // Fallback: 다이얼로그 없으면 갤러리만 사용
            yield return StartCoroutine(SelectSubPhotosFromGallery());
        }

        yield break;
    }

    /// <summary>
    /// 연속 촬영 모드 시작 (Sub 사진들을 여러 장 연속 촬영)
    /// </summary>
    private IEnumerator StartContinuousCaptureMode()
    {
        continuousCapturedPaths.Clear();

        yield return StartCoroutine(CaptureNextSubPhoto());
    }

    /// <summary>
    /// 다음 Sub 사진 촬영 (연속 촬영 모드)
    /// 촬영 즉시 리스트에 반영하고 그리드 업데이트
    /// </summary>
    private IEnumerator CaptureNextSubPhoto()
    {
        int remainingSlots = MAX_SUB_PHOTOS - subPhotos.Count;

        if (remainingSlots <= 0)
        {
            // 최대 개수 도달 → 촬영 종료
            ShowWarning($"최대 {MAX_SUB_PHOTOS}장까지만 추가할 수 있습니다.");
            yield break;
        }

        bool captureDone = false;
        string capturedPath = null;
        bool permissionDenied = false;

        // NativeCamera로 사진 촬영 (내부적으로 권한 처리)
        NativeCamera.TakePicture((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                // 권한 거부 또는 취소
                permissionDenied = true;
            }
            capturedPath = path;
            captureDone = true;
        }, maxSize: 2048);

        // 촬영 완료 대기
        yield return new WaitUntil(() => captureDone);

        if (permissionDenied)
        {
            ShowWarning("카메라 권한이 필요합니다.");
            yield break;
        }

        if (!string.IsNullOrEmpty(capturedPath))
        {
            // 촬영 성공 → 즉시 로드하여 리스트에 추가
            Texture2D texture = NativeGallery.LoadImageAtPath(capturedPath,
                maxSize: 1024,
                markTextureNonReadable: false,
                generateMipmaps: false);

            if (texture != null)
            {
                subPhotos.Add(texture);
                UpdateSubPhotoGrid();
            }
            else
            {
                // 1단계 실패 시 2단계 수동 변환 시도
                bool conversionComplete = false;
                Texture2D convertedTexture = null;

                yield return StartCoroutine(LoadImageWithConversion(capturedPath, (result) =>
                {
                    convertedTexture = result;
                    conversionComplete = true;
                }));

                yield return new WaitUntil(() => conversionComplete);

                if (convertedTexture != null)
                {
                    subPhotos.Add(convertedTexture);
                    UpdateSubPhotoGrid();
                }
            }

            // "계속 촬영하시겠습니까?" 다이얼로그 표시
            int currentCount = subPhotos.Count;
            string message = $"현재 {currentCount}/{MAX_SUB_PHOTOS}장\n계속 촬영하시겠습니까?";

            if (continueCaptureDialog != null)
            {
                continueCaptureDialog.Show(
                    message,
                    onYes: () => StartCoroutine(CaptureNextSubPhoto()), // 다시 촬영
                    onNo: () => {
                        ShowWarning($"Sub 사진 촬영 완료! (총 {subPhotos.Count}/{MAX_SUB_PHOTOS}장)");
                    }
                );
            }
            else
            {
                // Fallback: 다이얼로그 없으면 자동 종료
            }
        }
        else
        {
            // 촬영 취소
            if (subPhotos.Count > 0)
                ShowWarning($"Sub 사진 촬영 완료! (총 {subPhotos.Count}/{MAX_SUB_PHOTOS}장)");
        }
    }

    /// <summary>
    /// 연속 촬영한 모든 사진을 Sub Photos에 추가
    /// </summary>
    private IEnumerator LoadContinuousCapturedPhotos()
    {
        if (continuousCapturedPaths.Count == 0)
        {
            yield break;
        }

        isProcessing = true;
        ShowSpinner($"사진 {continuousCapturedPaths.Count}장 로딩 중...");

        // 연속 촬영한 모든 사진을 Sub Photos에 추가
        yield return StartCoroutine(LoadMultipleImagesWithFallback(continuousCapturedPaths.ToArray(), () => { }));

        HideSpinner();
        ShowWarning($"Sub 사진 {continuousCapturedPaths.Count}장 추가 완료! (총 {subPhotos.Count}/{MAX_SUB_PHOTOS}장)");

        continuousCapturedPaths.Clear();
        isProcessing = false;
    }

    /// <summary>
    /// 갤러리에서 Sub 사진들 선택 (여러 장 동시 선택)
    /// </summary>
    private IEnumerator SelectSubPhotosFromGallery()
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
            }, LocalizationManager.Instance.GetText("select_sub_photos"), "image/*");
        }
        catch (System.Exception)
        {
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
        foreach (string path in paths)
        {
            if (subPhotos.Count >= MAX_SUB_PHOTOS)
            {
                ShowWarning(LocalizationManager.Instance.GetText("max_sub_photos_exceeded"));
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
                    ShowWarning($"이미지 로드 실패: {Path.GetFileName(path)}");
                }
            }

            yield return null; // 한 프레임 대기
        }

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

        // iOS HEIC 변환 시작

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

        try
        {
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            texture = new Texture2D(2, 2, TextureFormat.RGB24, false);

            if (!texture.LoadImage(imageBytes))
            {
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
                        result = convertedTexture;
                        
                        // 임시 파일 삭제
                        try { File.Delete(jpgPath); } catch { }
                    }
                    else
                    {
                        Destroy(convertedTexture);
                    }
                }
                
                // result remains null if conversion failed
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

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
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
        bool locationObtained = false;

        // ARCore Geospatial API 우선 사용
        if (useGeospatialAPI && earthManager != null)
        {
            if (earthManager.EarthTrackingState == TrackingState.Tracking)
            {
                var pose = earthManager.CameraGeospatialPose;

                // Geospatial API는 자동으로 MSL 고도 제공 (iOS/Android 통일)
                gpsData = new Vector3(
                    (float)pose.Latitude,
                    (float)pose.Longitude,
                    (float)pose.Altitude  // 이미 MSL 기준, Geoid 보정 완료
                );

                locationText = $"Lat:{gpsData.x:F4},Lon:{gpsData.y:F4},Alt:{gpsData.z:F2}";
                locationObtained = true;
            }
        }

        // Fallback: 기본 GPS 사용 (ARCore 사용 안 함 또는 실패 시)
        if (!locationObtained && Input.location.status == LocationServiceStatus.Running)
        {
            float rawAltitude = Input.location.lastData.altitude;
            float normalizedAltitude = rawAltitude;

#if UNITY_IOS && !UNITY_EDITOR
            // iOS WGS84 → MSL 보정 (지역별 Geoid 높이)
            float latitude = Input.location.lastData.latitude;
            float longitude = Input.location.lastData.longitude;
            float geoidOffset = GetGeoidOffset(latitude, longitude);

            normalizedAltitude = rawAltitude + geoidOffset;
#endif

            gpsData = new Vector3(
                Input.location.lastData.latitude,
                Input.location.lastData.longitude,
                normalizedAltitude  // Android 기준으로 통일된 고도
            );

            locationText = $"Lat:{gpsData.x:F4},Lon:{gpsData.y:F4},Alt:{gpsData.z:F2}";
            locationObtained = true;
        }

        // 위치 정보를 얻지 못한 경우
        if (!locationObtained)
        {
            gpsData = Vector3.zero;
            locationText = LocalizationManager.Instance.GetText("no_location_data");
        }

        if (locationInput != null) locationInput.text = locationText;
    }

    /// <summary>
    /// 위도/경도 기반 Geoid 높이 오프셋 반환 (iOS WGS84 → MSL 변환용)
    /// 전세계 주요 지역별 평균 Geoid 높이 적용
    /// </summary>
    private float GetGeoidOffset(float lat, float lon)
    {
        // 동아시아
        if (lat >= 30f && lat <= 45f && lon >= 120f && lon <= 145f)
        {
            // 한국, 일본, 중국 동부
            if (lat >= 33f && lat <= 43f && lon >= 126f && lon <= 142f)
                return 30f; // 일본: ~35-40m, 한국: ~20-25m, 평균 30m
            else
                return 25f; // 중국 동부
        }
        // 동남아시아
        else if (lat >= -10f && lat <= 25f && lon >= 95f && lon <= 140f)
        {
            return 15f; // 태국, 베트남, 필리핀, 인도네시아
        }
        // 남아시아 (인도)
        else if (lat >= 8f && lat <= 35f && lon >= 68f && lon <= 97f)
        {
            return 20f; // 인도
        }
        // 북미 서부
        else if (lat >= 30f && lat <= 60f && lon >= -130f && lon <= -110f)
        {
            return -25f; // 미국 서부, 캐나다 서부 (음수: 해수면보다 낮음)
        }
        // 북미 동부
        else if (lat >= 25f && lat <= 50f && lon >= -100f && lon <= -65f)
        {
            return -30f; // 미국 동부, 캐나다 동부
        }
        // 유럽
        else if (lat >= 35f && lat <= 70f && lon >= -10f && lon <= 40f)
        {
            return 45f; // 서유럽/동유럽 평균
        }
        // 호주
        else if (lat >= -45f && lat <= -10f && lon >= 110f && lon <= 155f)
        {
            return 10f; // 호주
        }
        // 남미
        else if (lat >= -55f && lat <= 13f && lon >= -82f && lon <= -34f)
        {
            return 5f; // 브라질, 아르헨티나 등
        }
        // 아프리카
        else if (lat >= -35f && lat <= 37f && lon >= -20f && lon <= 52f)
        {
            return 15f; // 아프리카 대륙
        }
        // 중동
        else if (lat >= 12f && lat <= 42f && lon >= 35f && lon <= 65f)
        {
            return 25f; // 중동 지역
        }
        // 기본값 (기타 지역)
        else
        {
            return 20f; // 전세계 평균 약 20m
        }
    }

    #endregion

    #region Instagram and UI Events

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

        // 에디터에서는 위치 서비스 검증 스킵 (테스트 좌표 사용)
#if UNITY_EDITOR
        if (gpsData == Vector3.zero)
        {
            // 테스트용 기본 좌표 (서울)
            gpsData = new Vector3(37.5665f, 126.9780f, 30f);
            locationText = $"Lat:{gpsData.x:F4},Lon:{gpsData.y:F4},Alt:{gpsData.z:F2}";
        }
#else
        if (Input.location.status != LocationServiceStatus.Running || gpsData == Vector3.zero)
        {
            ShowWarning(LocalizationManager.Instance.GetText("enable_location_service"));
            isProcessing = false;
            yield break;
        }
#endif

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

        // 검증 통과 → 바로 업로드 진행
        Coroutine countdownCoroutine = StartCoroutine(ShowCountdownWarning(10));
        yield return StartCoroutine(SendWithTimeout(
            ProcessAndUploadImages(
                this, userName, instagramID, showInstagram, gpsData, locationText,
                petFriendlyToggle?.isOn ?? false, separateRestroomToggle?.isOn ?? false,
                countdownCoroutine)));

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
            ShowWarning(LocalizationManager.Instance.GetText("request_timeout"));
        }
    }

    private IEnumerator ProcessAndUploadImages(
        CubeUploadManager form, string placeName, string instagramID, bool showInstagram,
        Vector3 gpsData, string locationText, bool petFriendly, bool separateRestroom,
        Coroutine countdownCoroutine)
    {
        ShowSpinner(LocalizationManager.Instance.GetText("uploading_object"));

        WWWForm formData = new WWWForm();

        // 로그인된 사용자의 username 사용 (로그인 안됐으면 빈 문자열)
        string loggedInUsername = "";
        if (LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn)
        {
            loggedInUsername = LoginManager.Instance.CurrentUsername ?? "";
        }

        formData.AddField("username", loggedInUsername);
        formData.AddField("name", placeName);  // 장소명
        formData.AddField("latitude", gpsData.x.ToString("F6"));
        formData.AddField("longitude", gpsData.y.ToString("F6"));
        formData.AddField("altitude", gpsData.z.ToString("F2"));  // iOS는 이미 +20m 보정됨 (Android 기준 통일)
        formData.AddField("pet_friendly", petFriendly ? "true" : "false");
        formData.AddField("separate_restroom", separateRestroom ? "true" : "false");
        formData.AddField("instagram_id", showInstagram ? instagramID : "");
        // status는 서버에서 AUTO_APPROVE 설정에 따라 결정
        formData.AddField("device_id", SystemInfo.deviceUniqueIdentifier);  // 업로더 추적용

        formData.AddField("timezone", GetTimezone());
        formData.AddField("timezone_offset", GetTimezoneOffset());

        // 폴더명: 날짜_시간_사용자명 (로그인 안됐으면 장소명 사용)
        string folderName = !string.IsNullOrEmpty(loggedInUsername) ? loggedInUsername : placeName;
        string folder = $"{DateTime.Now:yyyyMMdd_HHmmss}_{folderName}";
        formData.AddField("folder", folder);

        Texture2D mainPhoto = GetMainPhoto();
        if (mainPhoto != null)
        {
            Texture2D resizedMainPhoto = ResizeTextureWithRenderTexture(mainPhoto, 444, 444);
            byte[] mainPhotoBytes = resizedMainPhoto.EncodeToJPG(50);
            if (mainPhotoBytes.Length == 0)
            {
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
            ShowWarning(LocalizationManager.Instance.GetText("upload_logo_photo"));
            HideSpinner();
            yield break;
        }

        List<Texture2D> subPhotos = GetSubPhotos();
        for (int i = 1; i <= subPhotos.Count; i++)
        {
            if (i > MAX_SUB_PHOTOS) break;
            Texture2D subPhoto = subPhotos[i - 1];
            Texture2D resizedSubPhoto = ResizeTextureKeepAspectWithRenderTexture(subPhoto, 800, 800);
            byte[] subPhotoBytes = resizedSubPhoto.EncodeToJPG(50);
            if (subPhotoBytes.Length == 0)
            {
                ShowWarning(LocalizationManager.Instance.GetText("sub_photo_upload_failed"));
                Destroy(resizedSubPhoto);
                continue;
            }
            string subPath = $"sub_{i}.jpg";
            formData.AddBinaryData("sub_photos", subPhotoBytes, subPath, "image/jpeg");
            Destroy(resizedSubPhoto);
        }

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, formData))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();

            HideSpinner();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;

                if (responseText.Contains("Upload Succeeded!") || www.responseCode == 200)
                {
                    isProcessing = false;
                    StopCoroutine(countdownCoroutine);
                    ShowWarning(LocalizationManager.Instance.GetText("upload_success"));

                    // 업로드 성공 10초 후 FCM 알림 발송 (주변 사용자에게 새 콘텐츠 알림)
                    StartCoroutine(SendUploadNotificationDelayed(10f));

                    // 업로드 성공 1초 후 AR 오브젝트 + 리스트 즉시 새로고침
                    StartCoroutine(RefreshDataAfterUpload(1f));

                    FullReset();
                    yield break;
                }
                else
                {
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

    /// <summary>
    /// 업로드 성공 후 AR 오브젝트 + PlaceList 즉시 새로고침
    /// </summary>
    private IEnumerator RefreshDataAfterUpload(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (DataManager.Instance != null)
            DataManager.Instance.RefreshData();

        PlaceListManager plm = FindFirstObjectByType<PlaceListManager>();
        if (plm != null)
            plm.UpdateUI();
    }

    /// <summary>
    /// 업로드 성공 후 지정된 시간(초) 뒤에 업로더에게 FCM 알림 발송
    /// </summary>
    private IEnumerator SendUploadNotificationDelayed(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        // device_id 가져오기 (로그인 여부와 관계없이 알림 가능)
        string deviceId = SystemInfo.deviceUniqueIdentifier;

        // 현재 위치 가져오기
        float latitude = 0f, longitude = 0f;
        if (Input.location.status == LocationServiceStatus.Running)
        {
            latitude = Input.location.lastData.latitude;
            longitude = Input.location.lastData.longitude;
        }

        // 콘텐츠 이름
        string contentName = "AR 콘텐츠";

        // JSON body 생성
        var requestData = new System.Collections.Generic.Dictionary<string, object>
        {
            { "device_id", deviceId },
            { "latitude", latitude },
            { "longitude", longitude },
            { "content_name", contentName }
        };

        string jsonBody = JsonUtility.ToJson(new UploadNotificationRequest
        {
            device_id = deviceId,
            latitude = latitude,
            longitude = longitude,
            content_name = contentName
        });

        // 서버에 푸시 알림 요청
        string url = $"{ApiConfig.MAIN_SERVER}/api/upload-notification";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();
            // 실패해도 무시 - 업로드 자체는 이미 성공
        }
    }

    [System.Serializable]
    private class UploadNotificationRequest
    {
        public string device_id;
        public float latitude;
        public float longitude;
        public string content_name;
    }

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
    }

    private void ResetToInitialState()
    {
        if (uploadPage != null)
        {
            uploadPage.SetActive(false);
        }

        if (disableObject != null)
        {
            disableObject.SetActive(false);
        }

        // UploadPage가 닫히면 PlusButton을 다시 활성화하여 재진입 가능하게
        if (plusButton != null)
        {
            plusButton.SetActive(true);
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
                instagramIDInput.gameObject.SetActive(false);
                instagramIDInput.text = "";
            }
        }

        userName = "";
        instagramID = "";
        gpsData = Vector3.zero;
        isProcessing = false;
        elapsedTime = 0f;
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
            savedCurrentPanel = -1; // 복원 후 초기화
        }
    }
    
    #endregion
}