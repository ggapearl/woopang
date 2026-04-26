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

    [Header("카테고리 설정")]
    [SerializeField] private Toggle categoryToggle;
    [SerializeField] private Text categoryToggleLabel;

    [Tooltip("카테고리별 토글 배경색 (순서: shop, food, cafe, park)")]
    [SerializeField] private Color categoryColorShop = new Color(0.25f, 0.5f, 0.95f, 1f); // 파란색
    [SerializeField] private Color categoryColorFood = new Color(0.984f, 0.757f, 0.365f, 1f); // #fbc15d 노란색
    [SerializeField] private Color categoryColorCafe = new Color(0.91f, 0.33f, 0.63f, 1f);    // 핑크색
    [SerializeField] private Color categoryColorPark = new Color(0.3f, 0.85f, 0.5f, 1f);      // 초록색
    [SerializeField] private Color categoryColorToilet = new Color(0.68f, 0.33f, 0.77f, 1f); // 보라색 (공공화장실)
    [SerializeField] private Color categoryColorSport = new Color(0.95f, 0.45f, 0.25f, 1f); // 주황색 (스포츠)
    [SerializeField] private Color categoryColorLandmark = new Color(0.2f, 0.7f, 0.9f, 1f); // 하늘색 (랜드마크)
    [SerializeField] private Color categoryColorEtc = new Color(0.6f, 0.6f, 0.6f, 1f); // 회색 (기타)

    [SerializeField] private GameObject warningObj;
    [SerializeField] private GameObject uploadPage;
    [SerializeField] private GameObject disableObject;
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

    // 카테고리 순환 (none → shop → food → cafe → park → toilet → sport → landmark → etc → none)
    private static readonly string[] categoryValues = { "", "shop", "food", "cafe", "park", "toilet", "sport", "landmark", "etc" };
    private int currentCategoryIndex = 0;
    private string selectedCategory = "";
    private string locationText;
    private const int MAX_SUB_PHOTOS = 10;
    private bool isProcessing = false;
    private float elapsedTime = 0f;

    // 스와이프 패널 상태 저장용
    private SwipePanelController swipePanelController;
    private int savedCurrentPanel = -1;

    // 연속 촬영 모드용
    private List<string> continuousCapturedPaths = new List<string>();

    public void ShowUploadPage()
    {
        if (uploadPage != null) uploadPage.SetActive(true);
        if (locationInput != null) locationInput.text = GetLocalizedText("loading_location");
        StartCoroutine(InitializeLocationService());

        var modelManager = FindFirstObjectByType<ModelUploadManager>();
        if (modelManager != null)
        {
            modelManager.ShowUploadPage();
        }

        // 캐시 무효화 후 항상 새로 찾기
        swipePanelController = FindFirstObjectByType<SwipePanelController>();
        if (swipePanelController != null)
        {
            swipePanelController.ResetToFirstPanel();
        }
    }

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


    private void AutoConnectFields()
    {
        if (categoryToggle == null && petFriendlyToggle != null)
        {
            // CategoryToggle은 PetFriendlyToggle과 같은 부모 패널에 있음
            Transform panel = petFriendlyToggle.transform.parent;
            if (panel != null)
            {
                Transform ct = panel.Find("CategoryToggle");
                if (ct != null) categoryToggle = ct.GetComponent<Toggle>();
            }
        }
        if (categoryToggleLabel == null && categoryToggle != null)
        {
            categoryToggleLabel = categoryToggle.GetComponentInChildren<Text>(true);
        }
    }

    private void InitializeComponents()
    {
        AutoConnectFields();

        // AREarthManager 자동 연결 (Inspector 미연결 시)
        if (earthManager == null)
        {
            earthManager = FindFirstObjectByType<AREarthManager>();
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
        if (mainPhotoButton != null) mainPhotoButton.onClick.AddListener(() => StartCoroutine(SelectAndCropMainPhoto()));
        if (subPhotosButton != null) subPhotosButton.onClick.AddListener(() => StartCoroutine(SelectSubPhotos()));
        if (resetPhotosButton != null) resetPhotosButton.onClick.AddListener(ResetSubPhotos);
        if (submitButton != null) submitButton.onClick.AddListener(() => StartCoroutine(ValidateAndSubmit()));

        // 카테고리 토글 초기화
        if (categoryToggle != null)
        {
            categoryToggle.isOn = false;
            categoryToggle.onValueChanged.AddListener(OnCategoryToggleChanged);
            UpdateCategoryToggleUI();
        }

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

        var cropper = ImageCropper.Instance;
        if (cropper == null)
        {
            onComplete?.Invoke();
            return;
        }

        // ImageCropper Canvas를 메인 UI 위에 표시되도록 설정
        ConfigureImageCropperCanvas(cropper);

        // 크로퍼가 sortingOrder 30000으로 앞에 렌더링되므로
        // uploadPage는 크로퍼 표시 후 숨김 (깜빡임 방지)
        StartCoroutine(HideUploadPageDelayed());

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
        // iOS PHPickerViewController는 비동기 처리로 선택 순서가 보장되지 않음
        // 임시 파일명에 포함된 인덱스 번호로 정렬하여 선택 순서 복원
        SortPathsByFileIndex(paths);

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
    /// iOS PHPickerViewController 비동기 반환 경로를 선택 순서대로 정렬
    /// 임시 파일명 패턴: {basePath}{index}.{ext} (예: /tmp/ngallery1.jpg, /tmp/ngallery2.png)
    /// </summary>
    /// <summary>
    /// ImageCropper Canvas를 메인 UI보다 앞에 표시되도록 설정 (sortingOrder 30000)
    /// 다른 매니저에서도 동일하게 호출 가능
    /// </summary>
    public static void ConfigureImageCropperCanvas(ImageCropper cropper)
    {
        if (cropper == null) return;

        Canvas cropperCanvas = cropper.GetComponent<Canvas>();
        if (cropperCanvas == null) cropperCanvas = cropper.GetComponentInChildren<Canvas>();

        if (cropperCanvas != null)
        {
            cropperCanvas.overrideSorting = true;
            cropperCanvas.sortingOrder = 30000;
        }
    }

    private void SortPathsByFileIndex(string[] paths)
    {
        if (paths == null || paths.Length <= 1) return;
        if (Application.platform != RuntimePlatform.IPhonePlayer) return;

        System.Array.Sort(paths, (a, b) =>
        {
            int indexA = ExtractFileIndex(a);
            int indexB = ExtractFileIndex(b);
            return indexA.CompareTo(indexB);
        });
    }

    private int ExtractFileIndex(string path)
    {
        if (string.IsNullOrEmpty(path)) return int.MaxValue;
        string fileName = Path.GetFileNameWithoutExtension(path);
        // 파일명 끝의 숫자 추출 (예: "ngallery3" → 3)
        var match = Regex.Match(fileName, @"(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
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
            float lat = Input.location.lastData.latitude;
            float lon = Input.location.lastData.longitude;
            float normalizedAltitude = GeoidHelper.NormalizeAltitude(
                Input.location.lastData.altitude, lat, lon);

            gpsData = new Vector3(
                lat,
                lon,
                normalizedAltitude  // Android 기준으로 통일된 고도 (iOS는 GeoidHelper로 보정)
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

    // GetGeoidOffset → GeoidHelper.GetGeoidOffset() 으로 통합됨

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

    // ============================================================
    // 카테고리 토글 — 누를 때마다 순환 (none → shop → food → cafe → park → none)
    // ============================================================

    private void OnCategoryToggleChanged(bool value)
    {
        // 토글 isOn 상태와 무관하게 클릭할 때마다 순환
        currentCategoryIndex = (currentCategoryIndex + 1) % categoryValues.Length;
        selectedCategory = categoryValues[currentCategoryIndex];
        UpdateCategoryToggleUI();

        // 토글을 항상 On 상태로 유지 (0번 인덱스 = 미선택일 때만 Off)
        if (categoryToggle != null)
        {
            categoryToggle.onValueChanged.RemoveListener(OnCategoryToggleChanged);
            categoryToggle.isOn = currentCategoryIndex != 0;
            categoryToggle.onValueChanged.AddListener(OnCategoryToggleChanged);
        }
    }

    private void UpdateCategoryToggleUI()
    {
        Color bgColor = GetCategoryColor(selectedCategory);

        // 토글 Background + Checkmark 색상 변경
        if (categoryToggle != null)
        {
            // Background 자식 오브젝트의 Image 찾기
            Transform bgTf = categoryToggle.transform.Find("Background");
            if (bgTf != null)
            {
                Image bgImg = bgTf.GetComponent<Image>();
                if (bgImg != null) bgImg.color = bgColor;

                // Checkmark도 동일 색상
                Transform checkTf = bgTf.Find("Checkmark");
                if (checkTf != null)
                {
                    Image checkImg = checkTf.GetComponent<Image>();
                    if (checkImg != null) checkImg.color = bgColor;
                }
            }

            // Graphic (toggle.graphic) 색상도 동기화
            if (categoryToggle.graphic != null)
                categoryToggle.graphic.color = bgColor;
        }

        // 라벨 텍스트 + 색상 업데이트
        if (categoryToggleLabel != null)
        {
            if (string.IsNullOrEmpty(selectedCategory))
                categoryToggleLabel.text = LocalizationManager.Instance.GetText("category_select");
            else
                categoryToggleLabel.text = LocalizationManager.Instance.GetText("category_" + selectedCategory);

            // 라벨 색상도 카테고리 색상으로 변경 (미선택 시 흰색)
            categoryToggleLabel.color = string.IsNullOrEmpty(selectedCategory) ? Color.white : bgColor;
        }
    }

    /// <summary>
    /// 카테고리 값에 따른 색상 반환 (Inspector에서 조절 가능)
    /// </summary>
    public Color GetCategoryColor(string category)
    {
        switch (category)
        {
            case "shop": return categoryColorShop;
            case "food": return categoryColorFood;
            case "cafe": return categoryColorCafe;
            case "park": return categoryColorPark;
            case "toilet": return categoryColorToilet;
            case "sport": return categoryColorSport;
            case "landmark": return categoryColorLandmark;
            case "etc": return categoryColorEtc;
            default: return Color.white;
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

        if (string.IsNullOrEmpty(selectedCategory))
        {
            ShowWarning(LocalizationManager.Instance.GetText("category_required"));
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
        formData.AddField("category", selectedCategory);

        // 카테고리 색상을 HEX로 직접 전송 (서버가 locations.color에 저장)
        // → 업로드 직후부터 OffScreenIndicator/PlaceList에 즉시 카테고리 색상 반영
        Color catColor = GetCategoryColor(selectedCategory);
        string colorHex = ColorUtility.ToHtmlStringRGB(catColor); // 6자리 RGB (#없이)
        formData.AddField("color", colorHex);

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

                    // FullReset 먼저 수행 (StopAllCoroutines + uploadPage.SetActive(false) 포함)
                    FullReset();

                    // ★ 중요: FullReset 후 uploadPage가 비활성화되면 CubeUploadManager의
                    //   StartCoroutine이 InvalidOperationException으로 실패함.
                    //   → 영속 싱글톤 DataManager.Instance 위에서 코루틴 실행
                    //   (업로드 완료 후 간헐적 미리프레쉬 버그 근본 수정)
                    MonoBehaviour host = DataManager.Instance != null
                        ? (MonoBehaviour)DataManager.Instance
                        : this;
                    host.StartCoroutine(RefreshDataAfterUpload(1f));
                    host.StartCoroutine(SendUploadNotificationDelayed(10f));

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
    /// + 캐시 수신 후 FilterManager 즉시 재배분 트리거 1회 (AllocationLoop가 이후 자동 처리)
    /// </summary>
    private IEnumerator RefreshDataAfterUpload(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (DataManager.Instance != null)
            DataManager.Instance.RefreshData();

        PlaceListManager plm = FindFirstObjectByType<PlaceListManager>();
        if (plm != null)
            plm.UpdateUI();

        // 라이트 캐시 수신 후 1회만 즉시 재배분 (이후는 AllocationLoop가 자동 처리)
        yield return new WaitForSeconds(2f);
        TriggerImmediateAllocation();
    }

    /// <summary>
    /// FilterManager 즉시 재배분 — 업로드 직후 / 백그라운드 복귀 / 필터 변경 시 사용
    /// </summary>
    private void TriggerImmediateAllocation()
    {
        FilterManager fm = FindFirstObjectByType<FilterManager>();
        if (fm != null) fm.TriggerReallocation();
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

        // PlusButton 다시 활성화 (OnClick에서 자기 자신을 SetActive(false)로 숨기므로)
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

        // 카테고리 초기화
        currentCategoryIndex = 0;
        selectedCategory = "";
        if (categoryToggle != null)
        {
            categoryToggle.onValueChanged.RemoveListener(OnCategoryToggleChanged);
            categoryToggle.isOn = false;
            categoryToggle.onValueChanged.AddListener(OnCategoryToggleChanged);
        }
        UpdateCategoryToggleUI();

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
    /// 크로퍼가 렌더링된 후 uploadPage를 숨김 (깜빡임 방지)
    /// </summary>
    private IEnumerator HideUploadPageDelayed()
    {
        // 2프레임 대기: 크로퍼 Canvas가 완전히 렌더링된 후 숨김
        yield return null;
        yield return null;
        if (uploadPage != null)
            uploadPage.SetActive(false);
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

            case "loading_main_photo":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "메인 사진 로딩 중...";
                    case SystemLanguage.Japanese: return "メイン写真を読み込み中...";
                    case SystemLanguage.Chinese: return "正在加载主照片...";
                    case SystemLanguage.ChineseSimplified: return "正在加载主照片...";
                    case SystemLanguage.Spanish: return "Cargando foto principal...";
                    default: return "Loading main photo...";
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

            case "upload_logo_photo":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "대표 사진을 업로드해주세요";
                    case SystemLanguage.Japanese: return "代表写真をアップロードしてください";
                    case SystemLanguage.Chinese: return "请上传代表照片";
                    case SystemLanguage.ChineseSimplified: return "请上传代表照片";
                    case SystemLanguage.Spanish: return "Sube una foto representativa";
                    default: return "Please upload a representative photo";
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

            case "main_photo_crop_failed":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "사진 크롭에 실패했습니다";
                    case SystemLanguage.Japanese: return "写真のクロップに失敗しました";
                    case SystemLanguage.Chinese: return "照片裁剪失败";
                    case SystemLanguage.ChineseSimplified: return "照片裁剪失败";
                    case SystemLanguage.Spanish: return "Error al recortar la foto";
                    default: return "Photo crop failed";
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
                    case SystemLanguage.Korean: return "업로드 성공!";
                    case SystemLanguage.Japanese: return "アップロード成功！";
                    case SystemLanguage.Chinese: return "上传成功！";
                    case SystemLanguage.ChineseSimplified: return "上传成功！";
                    case SystemLanguage.Spanish: return "¡Subida exitosa!";
                    default: return "Upload successful!";
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

            case "main_photo_upload_failed":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "메인 사진 업로드에 실패했습니다";
                    case SystemLanguage.Japanese: return "メイン写真のアップロードに失敗しました";
                    case SystemLanguage.Chinese: return "主照片上传失败";
                    case SystemLanguage.ChineseSimplified: return "主照片上传失败";
                    case SystemLanguage.Spanish: return "Falló la carga de la foto principal";
                    default: return "Main photo upload failed";
                }

            case "sub_photo_upload_failed":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "서브 사진 업로드에 실패했습니다";
                    case SystemLanguage.Japanese: return "サブ写真のアップロードに失敗しました";
                    case SystemLanguage.Chinese: return "子照片上传失败";
                    case SystemLanguage.ChineseSimplified: return "子照片上传失败";
                    case SystemLanguage.Spanish: return "Falló la carga de la foto secundaria";
                    default: return "Sub photo upload failed";
                }

            default:
                return key;
        }
    }

    #endregion
}