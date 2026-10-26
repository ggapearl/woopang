using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARSubsystems;

public class CubeDataFixManager : MonoBehaviour
{
    [SerializeField] private InputField descriptionInput;
    [SerializeField] private InputField nameInput;
    [SerializeField] private Button mainPhotoButton;
    [SerializeField] private Image mainPhotoDisplay;
    [SerializeField] private Button subPhotosButton;
    [SerializeField] private GridLayoutGroup subPhotoGrid;
    [SerializeField] private Button resetPhotosButton;
    [SerializeField] private Text loadingText;
    [SerializeField] private Toggle petFriendlyToggle;
    [SerializeField] private Toggle separateRestroomToggle;
    [SerializeField] private Toggle instagramToggle;
    [SerializeField] private InputField instagramIDInput;
    [SerializeField] private Button submitButton;
    [SerializeField] private GameObject warningObj;
    [SerializeField] private GameObject fixUIPanel;
    [SerializeField] private DoubleTap3D doubleTap;
    [SerializeField] private GameObject disableObject;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Image loadingSpinner;
    [SerializeField] private R0926PhotoLoading photoLoading; // 대표·추가 사진 칸에 빈 칸+원호 (Redesign0926Apply10 이 만들어 연결)
    [SerializeField] private R0926PhotoCrop photoCrop;       // 대표 사진 자르기 화면 (Redesign0926ApplyCrop 이 만들어 연결)
    [SerializeField] private R0926Busy busy;                 // 보내는 동안 화면 가운데 카드 — 원호 + '보내는 중… N%' + 막대

    [Header("내가 올린 장소 고치기 (2026-10-26) — Redesign0926Apply10 이 만들어 연결")]
    [SerializeField] private Button openButton;              // 장소 '···' 의 '정보 수정' 칸 — 누르면 그 장소의 지금 값으로 채운다
    [SerializeField] private Button mainPhotoRemove;         // 대표 사진 오른쪽 위 x
    [SerializeField] private Button subPhotoRemoveTemplate;  // 추가 사진 칸마다 복제해 넣는 x (꺼 둔 본)
    [SerializeField] private R0926PhotoReorder subReorder;   // 추가 사진을 꾹 눌러 끌면 보이는 순서가 바뀐다 (10-26)
    [SerializeField] private GameObject descriptionLabel;    // 수정 요청의 '수정할 내용' — 내 장소에선 숨긴다
    [SerializeField] private GameObject locationLabel;       // 내 장소에서만 — '위치'
    [SerializeField] private GameObject locationRow;
    [SerializeField] private Text locationCoords;
    [SerializeField] private Text locationNote;
    [SerializeField] private Toggle moveHereToggle;          // 켜고 저장하면 지금 내 위치로 옮긴다 (기본 꺼짐)
    [SerializeField] private Button deletePlaceButton;       // 내 장소에서만 — 카드 오른쪽 위 '삭제'
    [SerializeField] private FixPlaceLocalizer localizer;
    [SerializeField] private RemoveRequest removeRequest;
    [SerializeField] private AREarthManager earthManager;

    [Header("카테고리 설정")]
    [SerializeField] private Toggle categoryToggle;
    [SerializeField] private Text categoryToggleLabel;

    [Header("Photo Dialogs")]
    [SerializeField] private PhotoSourceDialog photoSourceDialog;
    [SerializeField] private ContinueCaptureDialog continueCaptureDialog;

    [Header("Upload Settings")]
    private string serverUrl => ApiConfig.FIX_UPLOAD;
    [Tooltip("요청을 보낸 순간부터 잰다. 넘으면 요청을 끊고 '시간 초과'")]
    [SerializeField] private float uploadTimeoutSeconds = 60f;

    // HEIC 처리용 변수들
    private readonly string[] iOSImageFormats = {
        ".heic", ".heif", ".png", ".jpg", ".jpeg",
        ".tiff", ".tif", ".bmp", ".gif"
    };

    private Texture2D mainPhoto;
    private Sprite mainSprite;   // 이 코드가 만든 대표 사진 스프라이트 — 장면에 놓인 기본 그림은 지우면 안 된다
    private List<Texture2D> subPhotos = new List<Texture2D>();
    private List<Image> subPhotoDisplays = new List<Image>();
    private bool showInstagram;
    private const int MAX_SUB_PHOTOS = 10;
    private bool isProcessing = false;

    // 카테고리 순환 (none → shop → food → cafe → park → toilet → sport → landmark → etc → none)
    private static readonly string[] categoryValues = { "", "shop", "food", "cafe", "park", "toilet", "sport", "landmark", "etc" };
    private int currentCategoryIndex = 0;
    private string selectedCategory = "";

    // 내 장소 고치기 — 서버에 있던 사진은 경로로 남기고(다시 올리지 않는다) 새로 고른 사진만 올린다
    private int editId;                // 지금 채워 둔 우팡 장소 (0 = 없음 — 공공데이터 등은 이름만 채운다)
    private bool ownerMode;            // 서버가 '내 장소'라고 확인했을 때만 — 아니면 예전처럼 수정 요청
    private string mainKeepPath;       // 대표 사진이 서버에 있던 그대로면 그 경로
    private readonly List<string> subKeepPaths = new List<string>();   // subPhotos 와 같은 순서 · 새로 고른 사진은 null
    private int loadedSubs;   // 불러온 장소에 있던 추가 사진 수 — 있었으면 0장으로는 저장하지 않는다 (10-26 대표님 「최소한 한장의 서브사진이 있도록 유지」)
    private int loadGen;
    private bool hasPlaceCoords;
    private double placeLat, placeLon;
    private string loadedCategory = "";
    private float nextNoteAt;
    private Color noteColor = Color.gray;
    private R0926LocalizedText submitLabel;

    /// <summary>플레이 시험 — 고치기·지우기와 그 사진을 내 PC 시험 서버로 (운영 서버의 장소를 시험으로 바꾸지 않게)</summary>
    public static string EditorBase;
    private static string Base => string.IsNullOrEmpty(EditorBase) ? ApiConfig.MAIN_SERVER : EditorBase;
    public static string PlaceUrl(int id, string tail = "") => Base + "/api/place/" + id + tail;

    private void Awake()
    {
        if (FindObjectsByType<CubeDataFixManager>(FindObjectsSortMode.None).Length > 1)
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

        DoubleTap3D.OnDoubleTapEvent += HandleDoubleTap;
        RemoveRequest.PlaceDeleted += OnPlaceDeleted;
    }

    private void Update()
    {
        // 위치 줄 — 걸으며 보면 거리가 바뀐다 (1초에 한 번)
        if (ownerMode && fixUIPanel != null && fixUIPanel.activeInHierarchy) RefreshLocation(false);
    }

    void OnDestroy()
    {
        DoubleTap3D.OnDoubleTapEvent -= HandleDoubleTap;
        RemoveRequest.PlaceDeleted -= OnPlaceDeleted;
    }

    private void HandleDoubleTap(DoubleTap3D tappedDoubleTap)
    {
        doubleTap = tappedDoubleTap;
    }

    #region Component Initialization
    private void AutoConnectFields()
    {
        if (categoryToggle == null && petFriendlyToggle != null)
        {
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
        if (photoSourceDialog == null)
        {
            photoSourceDialog = GetComponentInChildren<PhotoSourceDialog>(true);
            if (photoSourceDialog == null)
                photoSourceDialog = FindFirstObjectByType<PhotoSourceDialog>();
        }
        if (continueCaptureDialog == null)
        {
            continueCaptureDialog = GetComponentInChildren<ContinueCaptureDialog>(true);
            if (continueCaptureDialog == null)
                continueCaptureDialog = FindFirstObjectByType<ContinueCaptureDialog>();
        }
        if (photoLoading == null && mainPhotoButton != null)
        {
            photoLoading = mainPhotoButton.transform.parent.GetComponent<R0926PhotoLoading>();
        }
        if (photoCrop == null) photoCrop = FindAnyObjectByType<R0926PhotoCrop>(FindObjectsInactive.Include);
        if (busy == null) busy = FindAnyObjectByType<R0926Busy>(FindObjectsInactive.Include);
        if (localizer == null) localizer = FindAnyObjectByType<FixPlaceLocalizer>(FindObjectsInactive.Include);
        if (removeRequest == null) removeRequest = FindAnyObjectByType<RemoveRequest>(FindObjectsInactive.Include);
        if (earthManager == null) earthManager = FindAnyObjectByType<AREarthManager>(FindObjectsInactive.Include);
        if (submitLabel == null && submitButton != null)
        {
            Transform lb = submitButton.transform.Find("Label0926");
            if (lb != null) submitLabel = lb.GetComponent<R0926LocalizedText>();
        }
    }

    private void InitializeComponents()
    {
        AutoConnectFields();

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
        else Debug.LogError("[CubeDataFixManager] InstagramToggle이 할당되지 않았습니다!");

        if (mainPhotoButton != null) mainPhotoButton.onClick.AddListener(() => StartCoroutine(SelectAndCropMainPhoto()));
        else Debug.LogError("[CubeDataFixManager] MainPhotoButton이 할당되지 않았습니다!");

        if (subPhotosButton != null) subPhotosButton.onClick.AddListener(() => StartCoroutine(SelectSubPhotos()));
        else Debug.LogError("[CubeDataFixManager] SubPhotosButton이 할당되지 않았습니다!");

        if (resetPhotosButton != null) resetPhotosButton.onClick.AddListener(ResetSubPhotos);
        else Debug.LogError("[CubeDataFixManager] ResetPhotosButton이 할당되지 않았습니다!");

        if (submitButton != null) submitButton.onClick.AddListener(() => StartCoroutine(ValidateAndSubmit()));
        else Debug.LogError("[CubeDataFixManager] SubmitButton이 할당되지 않았습니다!");

        if (openButton != null) openButton.onClick.AddListener(BeginEdit);   // 창을 여는 장면 연결(SetActive)보다 뒤에 불린다
        if (mainPhotoRemove != null) mainPhotoRemove.onClick.AddListener(RemoveMain);
        if (deletePlaceButton != null) deletePlaceButton.onClick.AddListener(AskDelete);
        if (moveHereToggle != null)
        {
            moveHereToggle.isOn = false;
            moveHereToggle.onValueChanged.AddListener(_ => RefreshLocation(true));
        }
        if (locationNote != null) noteColor = locationNote.color;

        // 카테고리 토글 초기화
        if (categoryToggle != null)
        {
            categoryToggle.isOn = false;
            categoryToggle.onValueChanged.AddListener(OnCategoryToggleChanged);
            UpdateCategoryToggleUI();
        }

        // Component validation
        if (descriptionInput == null) Debug.LogError("[CubeDataFixManager] DescriptionInput이 할당되지 않았습니다!");
        if (nameInput == null) Debug.LogError("[CubeDataFixManager] NameInput이 할당되지 않았습니다!");
        if (mainPhotoDisplay == null) Debug.LogError("[CubeDataFixManager] MainPhotoDisplay가 할당되지 않았습니다!");
        if (subPhotoGrid == null) Debug.LogError("[CubeDataFixManager] SubPhotoGrid가 할당되지 않았습니다!");
        if (loadingText == null) Debug.LogError("[CubeDataFixManager] LoadingText가 할당되지 않았습니다!");
        if (petFriendlyToggle == null) Debug.LogError("[CubeDataFixManager] PetFriendlyToggle이 할당되지 않았습니다!");
        if (separateRestroomToggle == null) Debug.LogError("[CubeDataFixManager] SeparateRestroomToggle이 할당되지 않았습니다!");
        if (instagramIDInput == null) Debug.LogError("[CubeDataFixManager] InstagramIDInput이 할당되지 않았습니다!");
        if (warningObj == null) Debug.LogError("[CubeDataFixManager] WarningObj가 할당되지 않았습니다!");
        if (fixUIPanel == null) Debug.LogError("[CubeDataFixManager] FixUIPanel이 할당되지 않았습니다!");
        if (disableObject == null) Debug.LogError("[CubeDataFixManager] DisableObject가 할당되지 않았습니다!");

        SetUIActive(loadingText?.gameObject, false);
        SetUIActive(warningObj, false);
        SetUIActive(fixUIPanel, false);
        SetUIActive(disableObject, false);
        HideSpinner();

        InitializeObjectPool();
    }

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
        subPhotoDisplays = new List<Image>();
        for (int i = 0; i < MAX_SUB_PHOTOS; i++)
        {
            GameObject imageObj = new GameObject($"SubPhoto_{i}");
            imageObj.transform.SetParent(subPhotoGrid?.transform, false);
            Image img = imageObj.AddComponent<Image>();
            img.preserveAspect = true;
            subPhotoDisplays.Add(img);
            if (subPhotoRemoveTemplate != null)
            {
                // 칸마다 오른쪽 위 x — 그 사진만 뺀다 (뒤 사진이 한 칸씩 당겨진다)
                Button x = Instantiate(subPhotoRemoveTemplate, imageObj.transform, false);
                x.name = "RemoveX0926";
                x.gameObject.SetActive(true);
                int k = i;
                x.onClick.AddListener(() => RemoveSub(k));
            }
            imageObj.SetActive(false);
        }
        if (subReorder != null) subReorder.Bind(subPhotoDisplays, () => subPhotos.Count, MoveSub, () => isProcessing);
    }
    #endregion

    #region Image Path Sorting
    /// <summary>
    /// iOS PHPickerViewController 비동기 반환 경로를 선택 순서대로 정렬
    /// </summary>
    private void SortPathsByFileIndex(string[] paths)
    {
        if (paths == null || paths.Length <= 1) return;
        if (Application.platform != RuntimePlatform.IPhonePlayer) return;

        Array.Sort(paths, (a, b) =>
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
        var match = Regex.Match(fileName, @"(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
    }
    #endregion

    #region HEIC Processing Methods
    private bool NeedsHEICConversion(string path)
    {
        string extension = Path.GetExtension(path).ToLower();
        return Array.Exists(iOSImageFormats, format => format == extension);
    }

    private IEnumerator LoadImageWithConversion(string imagePath, System.Action<Texture2D> onComplete)
    {
        Texture2D convertedTexture = null;

#if UNITY_IOS && !UNITY_EDITOR
        // iOS에서 네이티브 변환 시도
        yield return StartCoroutine(ConvertHEICToJPG(imagePath, (result) => {
            convertedTexture = result;
        }));
#else
        // 에디터나 다른 플랫폼에서 직접 로드 시도
        yield return StartCoroutine(LoadImageDirect(imagePath, (result) => {
            convertedTexture = result;
        }));
#endif

        if (convertedTexture == null)
        {
            Debug.LogError($"[HEIC] 2단계 변환 실패: {Path.GetFileName(imagePath)}");
        }

        onComplete?.Invoke(convertedTexture);
    }

    private IEnumerator LoadImageDirect(string imagePath, System.Action<Texture2D> onComplete)
    {
        Texture2D texture = null;
        
        try
        {
            if (File.Exists(imagePath))
            {
                byte[] imageBytes = File.ReadAllBytes(imagePath);
                texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                bool success = texture.LoadImage(imageBytes);
                
                if (!success)
                {
                    Debug.LogError($"[HEIC] LoadImage 실패: {Path.GetFileName(imagePath)}");
                    if (texture != null) Destroy(texture);
                    texture = null;
                }
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
    private IEnumerator ConvertHEICToJPG(string heicPath, System.Action<Texture2D> onComplete)
    {
        Texture2D result = null;
        
        try
        {
            // iOS 네이티브 변환 로직
            if (File.Exists(heicPath))
            {
                string tempJpgPath = Path.Combine(Path.GetTempPath(), 
                    $"converted_{DateTime.Now.Ticks}.jpg");
                
                // 임시로 NativeGallery의 변환 기능 사용
                Texture2D tempTexture = NativeGallery.LoadImageAtPath(heicPath, maxSize: 2048);
                if (tempTexture != null)
                {
                    byte[] jpgBytes = tempTexture.EncodeToJPG(80);
                    File.WriteAllBytes(tempJpgPath, jpgBytes);
                    
                    // 변환된 JPG 로드
                    result = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    result.LoadImage(jpgBytes);
                    
                    // 임시 파일 정리
                    if (File.Exists(tempJpgPath))
                    {
                        File.Delete(tempJpgPath);
                    }
                    
                    Destroy(tempTexture);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HEIC] iOS 변환 실패: {e.Message}");
            if (result != null) Destroy(result);
            result = null;
        }
        
        onComplete?.Invoke(result);
        yield return null;
    }
#endif
    #endregion

    #region Photo Processing Methods
    /// <summary>
    /// 메인 사진 선택 - PhotoSourceDialog로 촬영/갤러리/취소 표시
    /// </summary>
    private IEnumerator SelectAndCropMainPhoto()
    {
        if (isProcessing) yield break;

        if (photoSourceDialog != null)
        {
            photoSourceDialog.Show(
                "",
                onCamera: () => StartCoroutine(CaptureMainPhotoFromCamera()),
                onGallery: () => StartCoroutine(SelectMainPhotoFromGallery())
            );
        }
        else
        {
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

        MainLoading(true);
        bool isLoading = true;
        string capturedPath = null;
        bool permissionDenied = false;

        NativeCamera.TakePicture((path) =>
        {
            if (string.IsNullOrEmpty(path))
                permissionDenied = true;
            capturedPath = path;
            isLoading = false;
        }, maxSize: 2048);

        yield return new WaitUntil(() => !isLoading);

        if (permissionDenied)
        {
            // 찍지 않고 닫아도 경로가 비어 온다 — 권한이 없을 때만 알린다
            if (!NativeCamera.CheckPermission(true)) ShowWarning("Camera permission required");
            MainLoading(false);
            isProcessing = false;
            yield break;
        }

        if (!string.IsNullOrEmpty(capturedPath))
        {
            yield return StartCoroutine(ProcessMainPhotoWithFallback(capturedPath, () => { }));
        }
        else
        {
            ShowWarning(GetLocalizedText("photo_selection_failed"));
        }

        MainLoading(false);
        isProcessing = false;
    }

    /// <summary>
    /// 갤러리에서 메인 사진 선택
    /// </summary>
    private IEnumerator SelectMainPhotoFromGallery()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        MainLoading(true);
        bool isLoading = true;

        NativeGallery.GetImageFromGallery((path) =>
        {
            if (!string.IsNullOrEmpty(path))
            {
                StartCoroutine(ProcessMainPhotoWithFallback(path, () => {
                    isLoading = false;
                }));
            }
            else
            {
                // 고르지 않고 닫았다 — 실패 안내는 띄우지 않는다
                isLoading = false;
            }
        }, "image/*");

        yield return new WaitUntil(() => !isLoading);
        MainLoading(false);
        isProcessing = false;
    }

    private IEnumerator ProcessMainPhotoWithFallback(string path, System.Action onComplete)
    {
        bool step1Success = false;
        bool processingComplete = false;

        MainLoading(true);
        yield return null;   // 사진 칸의 빈 칸이 먼저 그려지게 — 사진 읽기(LoadImageAtPath)는 그동안 화면을 멈춘다

        // 1단계: NativeGallery.LoadImageAtPath 시도
        try
        {
            Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize: 2048);
            
            if (texture != null && texture.width > 8 && texture.height > 8)
            {
                ProcessCropAndDisplay(texture, () => {
                    processingComplete = true;
                });
                step1Success = true;
            }
            else
            {
                if (texture != null) Destroy(texture);
            }
        }
        catch (System.Exception)
        {
            // 1단계 실패 - 2단계에서 재시도
        }

        if (step1Success)
        {
            yield return new WaitUntil(() => processingComplete);
            onComplete?.Invoke();
            yield break;
        }

        // 2단계: 수동 HEIC 변환
        processingComplete = false;
        yield return StartCoroutine(LoadImageWithConversion(path, (convertedTexture) => {
            if (convertedTexture != null)
            {
                ProcessCropAndDisplay(convertedTexture, () => {
                    processingComplete = true;
                });
            }
            else
            {
                Debug.LogError($"[HEIC] 모든 단계 실패: {Path.GetFileName(path)}");
                ShowWarning(GetLocalizedText("photo_selection_failed"));
                processingComplete = true;
            }
        }));

        yield return new WaitUntil(() => processingComplete);
        onComplete?.Invoke();
    }

    // 대표 사진 자르기 (장소 추가와 같은 R0926PhotoCrop) — 자르는 동안 fixUIPanel 은 숨긴다
    private void ProcessCropAndDisplay(Texture2D sourceTexture, System.Action onComplete)
    {
        if (photoCrop == null)
        {
            Debug.LogError("[CubeDataFixManager] PhotoCrop 이 연결되지 않았습니다");
            Destroy(sourceTexture);
            ShowWarning(GetLocalizedText("main_photo_crop_failed"));
            onComplete?.Invoke();
            return;
        }

        photoCrop.Open(sourceTexture, cropped =>
        {
            try
            {
                // 크롭 완료 후 fixUIPanel 복원
                if (fixUIPanel != null) fixUIPanel.SetActive(true);

                if (cropped != null)
                {
                    if (mainPhoto != null) Destroy(mainPhoto);
                    mainPhoto = cropped;
                    mainKeepPath = null;   // 새로 고른 대표 사진 — 저장할 때 올린다

                    if (mainPhotoDisplay != null)
                    {
                        SetMainSprite(mainPhoto);
                        mainPhotoDisplay.gameObject.SetActive(true);
                    }
                }
                else
                {
                    ShowWarning(GetLocalizedText("main_photo_crop_failed"));
                }
            }
            finally
            {
                Destroy(sourceTexture);   // 원본 — 예전엔 남아 사진을 고를 때마다 쌓였다
                onComplete?.Invoke();
            }
        }, () =>
        {
            // 취소 — 넣어 둔 대표 사진은 그대로 (예전엔 취소해도 '자르기 실패'가 떴다)
            Destroy(sourceTexture);
            if (fixUIPanel != null) fixUIPanel.SetActive(true);
            onComplete?.Invoke();
        });

        StartCoroutine(HideFixUIPanelDelayed());
    }

    private IEnumerator HideFixUIPanelDelayed()
    {
        // 2프레임 대기: 자르기 화면이 완전히 그려진 후 숨김 (그 사이 닫혔으면 그대로)
        yield return null;
        yield return null;
        if (fixUIPanel != null && photoCrop != null && photoCrop.IsOpen)
            fixUIPanel.SetActive(false);
    }

    /// <summary>
    /// 서브 사진 선택 - PhotoSourceDialog로 촬영/갤러리/취소 표시
    /// </summary>
    private IEnumerator SelectSubPhotos()
    {
        if (isProcessing) yield break;

        if (photoSourceDialog != null)
        {
            photoSourceDialog.Show(
                "",
                onCamera: () => StartCoroutine(StartContinuousCaptureMode()),
                onGallery: () => StartCoroutine(SelectSubPhotosFromGallery())
            );
        }
        else
        {
            yield return StartCoroutine(SelectSubPhotosFromGallery());
        }

        yield break;
    }

    /// <summary>
    /// 연속 촬영 모드 시작 (Sub 사진들을 여러 장 연속 촬영)
    /// </summary>
    private IEnumerator StartContinuousCaptureMode()
    {
        yield return StartCoroutine(CaptureNextSubPhoto());
    }

    /// <summary>
    /// 다음 Sub 사진 촬영 (연속 촬영 모드)
    /// </summary>
    private IEnumerator CaptureNextSubPhoto()
    {
        int remainingSlots = MAX_SUB_PHOTOS - subPhotos.Count;
        if (remainingSlots <= 0)
        {
            ShowWarning(GetLocalizedText("max_sub_photos_exceeded"));
            yield break;
        }

        bool captureDone = false;
        string capturedPath = null;
        bool permissionDenied = false;

        SubLoading(1);   // 카메라를 닫고 돌아오면 그 자리에 빈 칸이 돌고 있게
        NativeCamera.TakePicture((path) =>
        {
            if (string.IsNullOrEmpty(path))
                permissionDenied = true;
            capturedPath = path;
            captureDone = true;
        }, maxSize: 2048);

        yield return new WaitUntil(() => captureDone);

        if (permissionDenied)
        {
            SubLoading(0);
            // 찍지 않고 닫아도 경로가 비어 온다 — 권한이 없을 때만 알린다
            if (!NativeCamera.CheckPermission(true)) ShowWarning("Camera permission required");
            yield break;
        }

        if (!string.IsNullOrEmpty(capturedPath))
        {
            yield return null;   // 빈 칸이 먼저 그려지게 — 사진 읽기는 그동안 화면을 멈춘다
            Texture2D texture = NativeGallery.LoadImageAtPath(capturedPath, maxSize: 1024);
            if (texture != null && texture.width > 8 && texture.height > 8)
            {
                AddSub(texture, null);
                UpdateSubPhotoGrid();
            }
            else
            {
                if (texture != null) Destroy(texture);
                bool conversionComplete = false;
                yield return StartCoroutine(LoadImageWithConversion(capturedPath, (result) =>
                {
                    if (result != null)
                    {
                        AddSub(result, null);
                        UpdateSubPhotoGrid();
                    }
                    conversionComplete = true;
                }));
                yield return new WaitUntil(() => conversionComplete);
            }
            SubLoading(0);

            int currentCount = subPhotos.Count;
            if (continueCaptureDialog != null)
            {
                string message = $"{currentCount}/{MAX_SUB_PHOTOS}";
                continueCaptureDialog.Show(
                    message,
                    onYes: () => StartCoroutine(CaptureNextSubPhoto()),
                    onNo: () => { }
                );
            }
        }
    }

    /// <summary>
    /// 갤러리에서 Sub 사진들 선택 (여러 장 동시 선택)
    /// </summary>
    private IEnumerator SelectSubPhotosFromGallery()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        SubLoading(1);   // 고르는 동안 한 칸 — 몇 장인지는 고른 뒤에 안다
        bool isLoading = true;

        NativeGallery.GetImagesFromGallery((paths) =>
        {
            if (paths != null && paths.Length > 0)
            {
                // 남은 칸보다 많이 골라도 들어가는 만큼은 넣는다 — 넘친 장수는 LoadSubPhotosWithFallback 가 알린다
                StartCoroutine(LoadSubPhotosWithFallback(paths, () => {
                    isLoading = false;
                }));
            }
            else
            {
                // 고르지 않고 닫았다 — 실패 안내는 띄우지 않는다
                isLoading = false;
            }
        }, "image/*");

        yield return new WaitUntil(() => !isLoading);
        SubLoading(0);
        UpdateSubPhotoGrid();
        isProcessing = false;
    }

    private IEnumerator LoadSubPhotosWithFallback(string[] paths, System.Action onComplete)
    {
        // iOS PHPickerViewController는 비동기 처리로 선택 순서가 보장되지 않음
        // 임시 파일명에 포함된 인덱스 번호로 정렬하여 선택 순서 복원
        SortPathsByFileIndex(paths);

        int processedCount = 0;
        int totalPaths = paths.Length;

        // 들어갈 장수만큼 빈 칸을 먼저 깔고 한 프레임 — 사진 읽기(LoadImageAtPath)는 그동안 화면을 멈춘다
        int left = Mathf.Min(paths.Length, MAX_SUB_PHOTOS - subPhotos.Count);
        SubLoading(left);
        yield return null;

        foreach (string path in paths)
        {
            if (subPhotos.Count >= MAX_SUB_PHOTOS)
            {
                ShowWarning(GetLocalizedText("max_sub_photos_exceeded"));
                break;
            }

            bool imageProcessed = false;
            
            // 1단계: NativeGallery.LoadImageAtPath 시도
            Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize: 1024);
            
            if (texture != null && texture.width > 8 && texture.height > 8)
            {
                AddSub(texture, null);
                imageProcessed = true;
            }
            else
            {
                if (texture != null) Destroy(texture);
                
                // 2단계: 수동 HEIC 변환
                yield return StartCoroutine(LoadImageWithConversion(path, (convertedTexture) => {
                    if (convertedTexture != null)
                    {
                        AddSub(convertedTexture, null);
                    }
                    else
                    {
                        Debug.LogError($"[HEIC] 서브사진 로드 실패: {Path.GetFileName(path)}");
                        ShowWarning(GetLocalizedText("photo_selection_failed"));
                    }
                    imageProcessed = true;
                }));
            }

            yield return new WaitUntil(() => imageProcessed);
            processedCount++;
            // 한 장 들어올 때마다 빈 칸 하나가 사진으로 바뀐다
            SubLoading(--left);
            UpdateSubPhotoGrid();
            yield return null;
        }

        SubLoading(0);
        onComplete?.Invoke();
    }

    private void ResetSubPhotos()
    {
        foreach (var photo in subPhotos)
        {
            Destroy(photo);
        }
        subPhotos.Clear();
        subKeepPaths.Clear();
        UpdateSubPhotoGrid();
        ShowWarning(GetLocalizedText("sub_photos_reset"));
        CancelInvoke("HideWarning");
        Invoke("HideWarning", 2f);
    }

    private void UpdateSubPhotoGrid()
    {
        for (int i = 0; i < subPhotoDisplays.Count; i++)
        {
            // 같은 사진이면 스프라이트를 그대로 둔다 — 예전엔 부를 때마다 모든 칸의 스프라이트를 새로 만들어 쌓았다 (10-20)
            Image img = subPhotoDisplays[i];
            Texture2D tex = i < subPhotos.Count ? subPhotos[i] : null;
            if (img.sprite != null && (tex == null || img.sprite.texture != tex))
            {
                Destroy(img.sprite);
                img.sprite = null;
            }
            if (tex != null && img.sprite == null) img.sprite = ThumbSprite(tex);
            img.gameObject.SetActive(tex != null);
        }
    }

    private void SetMainSprite(Texture2D tex)
    {
        if (mainSprite != null) Destroy(mainSprite);
        mainSprite = tex != null ? GetOrCreateSprite(tex) : null;
        if (mainPhotoDisplay != null) mainPhotoDisplay.sprite = mainSprite;
    }

    private void MainLoading(bool on)
    {
        if (photoLoading != null) photoLoading.SetMain(on);
    }

    private void SubLoading(int count)
    {
        if (photoLoading != null) photoLoading.SetSub(count);
    }
    #endregion

    #region UI and Component Management
    private void SetUIActive(GameObject uiElement, bool active)
    {
        if (uiElement != null) uiElement.SetActive(active);
    }

    private void SetUIText(Text uiText, string text)
    {
        if (uiText != null) uiText.text = text;
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
        
        try
        {
            StopCoroutine(SpinnerAnimation());
        }
        catch (System.Exception)
        {
            // 이미 정지된 경우 무시
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

    private Sprite GetOrCreateSprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    // 추가 사진 칸은 가운데를 정사각형으로 잘라 칸을 채운다 — 세로로 긴 사진이 가는 띠로 보였다 (10-20)
    private static Sprite ThumbSprite(Texture2D texture)
    {
        int side = Mathf.Min(texture.width, texture.height);
        var rect = new Rect((texture.width - side) / 2, (texture.height - side) / 2, side, side);
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
    #endregion

    // ============================================================
    // 카테고리 토글 — 누를 때마다 순환 (none → shop → food → cafe → park → etc → none)
    // ============================================================
    #region Category Toggle

    private void OnCategoryToggleChanged(bool value)
    {
        currentCategoryIndex = (currentCategoryIndex + 1) % categoryValues.Length;
        selectedCategory = categoryValues[currentCategoryIndex];
        UpdateCategoryToggleUI();

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

        if (categoryToggle != null)
        {
            Transform bgTf = categoryToggle.transform.Find("Background");
            if (bgTf != null)
            {
                Image bgImg = bgTf.GetComponent<Image>();
                if (bgImg != null) bgImg.color = bgColor;

                Transform checkTf = bgTf.Find("Checkmark");
                if (checkTf != null)
                {
                    Image checkImg = checkTf.GetComponent<Image>();
                    if (checkImg != null) checkImg.color = bgColor;
                }
            }

            if (categoryToggle.graphic != null)
                categoryToggle.graphic.color = bgColor;
        }

        if (categoryToggleLabel != null)
        {
            if (string.IsNullOrEmpty(selectedCategory))
                categoryToggleLabel.text = GetLocalizedText("category_select");
            else
                categoryToggleLabel.text = GetLocalizedText("category_" + selectedCategory);

            categoryToggleLabel.color = string.IsNullOrEmpty(selectedCategory) ? Color.white : bgColor;
        }
    }

    // 분류 색 — 장소 추가·AR 인디케이터·목록·필터 칩과 같은 값 (10-25)
    public Color GetCategoryColor(string category) => DataManager.GetCategoryColor(category);

    #endregion

    #region Validation and Upload
    private IEnumerator ValidateAndSubmit()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        if (doubleTap == null && editId <= 0)
        {
            ShowWarning(GetLocalizedText("no_object_selected"));
            Debug.LogError("[CubeDataFixManager] ValidateAndSubmit: DoubleTap3D가 null입니다!");
            isProcessing = false;
            yield break;
        }

        int id = editId > 0 ? editId : doubleTap.GetId();   // 채워 둔 장소 — 창이 열린 사이 다른 오브젝트를 두 번 눌러도 그 장소로
        if (id <= 0)
        {
            ShowWarning(GetLocalizedText("valid_id_not_found"));
            Debug.LogError($"[CubeDataFixManager] 유효하지 않은 ID: {id}");
            isProcessing = false;
            yield break;
        }

        if (descriptionInput == null)
        {
            ShowWarning(GetLocalizedText("enter_description"));
            Debug.LogError("[CubeDataFixManager] DescriptionInput이 null입니다!");
            isProcessing = false;
            yield break;
        }

        if (nameInput == null)
        {
            ShowWarning(GetLocalizedText("enter_place_name"));
            Debug.LogError("[CubeDataFixManager] NameInput이 null입니다!");
            isProcessing = false;
            yield break;
        }

        string description = descriptionInput.text?.Trim() ?? "";
        string name = nameInput.text?.Trim() ?? "";

        if (!ownerMode && string.IsNullOrEmpty(description))   // 내 장소는 바로 고치므로 '수정할 내용'이 없다
        {
            ShowWarning(GetLocalizedText("enter_description"));
            isProcessing = false;
            yield break;
        }

        if (string.IsNullOrEmpty(name))
        {
            ShowWarning(GetLocalizedText("enter_place_name"));
            isProcessing = false;
            yield break;
        }

        string instagramID = showInstagram ? (instagramIDInput?.text?.Trim() ?? "") : "";
        bool petFriendly = petFriendlyToggle?.isOn ?? false;
        bool separateRestroom = separateRestroomToggle?.isOn ?? false;

        // 분류는 비워도 된다 — 분류를 지우면 흰색 (10-25 대표님)

        if (mainPhoto == null)
        {
            ShowWarning(GetLocalizedText("upload_main_photo"));
            isProcessing = false;
            yield break;
        }

        // 사진이 있던 장소는 추가 사진을 다 빼고(초기화) 저장하지 않는다
        if (subPhotos.Count == 0 && loadedSubs > 0)
        {
            ShowWarning(R0926LocalizedText.Say("추가 사진을 한 장 이상 넣어 주세요", "Add at least one extra photo", "追加写真を1枚以上入れてください", "请至少添加一张附加照片", "Añade al menos una foto adicional"));
            isProcessing = false;
            yield break;
        }

        if (ownerMode && id == editId)
        {
            yield return StartCoroutine(SubmitOwnerEdit(name, petFriendly, separateRestroom, instagramID));
        }
        else
        {
            // 시간 제한은 요청을 보낸 뒤부터 (CubeUploadManager.SendRequestWithTimeout)
            yield return StartCoroutine(ProcessAndUploadData(
                this, id, petFriendly, separateRestroom, instagramID, showInstagram, description, name,
                selectedCategory));
        }

        if (!isProcessing)
        {
            SetUIActive(fixUIPanel, false);
            ResetToInitialState();
            int gen = loadGen;
            yield return new WaitForSeconds(2f);
            if (gen != loadGen) yield break;   // 그사이 다른 장소를 열었다 — 사진을 불러오는 중이면 저장을 막아 둔 채로
        }
        isProcessing = false;
    }

    // 보내는 동안 화면 가운데 카드 — 예전엔 2초짜리 경고 글에 퍼센트만 떴다
    private void ShowUploadProgress(int percent)
    {
        if (busy != null) busy.Show(R0926LocalizedText.Say("보내는 중…", "Sending…", "送信中…", "发送中…", "Enviando…") + " " + percent + "%", percent / 100f);
        else ShowWarning(GetLocalizedText("submitting_countdown").Replace("{0}", percent + "%"));
    }

    private void HideBusy()
    {
        if (busy != null) busy.Hide();
    }

    private IEnumerator ProcessAndUploadData(
        CubeDataFixManager form, int id, bool petFriendly, bool separateRestroom,
        string instagramID, bool showInstagram, string description, string name,
        string category)
    {
        ShowUploadProgress(0);
        yield return null;   // 카드가 먼저 그려지게 — 사진 줄이기·인코딩은 그동안 화면을 멈춘다

        WWWForm formData = new WWWForm();

        formData.AddField("target_id", id.ToString());
        formData.AddField("pet_friendly", petFriendly ? "true" : "false");
        formData.AddField("separate_restroom", separateRestroom ? "true" : "false");
        formData.AddField("instagram_id", showInstagram ? instagramID : "");
        formData.AddField("description", description);
        formData.AddField("name", name);
        formData.AddField("category", category);

        formData.AddField("timezone", GetTimezone());
        formData.AddField("timezone_offset", GetTimezoneOffset());

        string folder = $"fix_{DateTime.Now:yyyyMMdd_HHmmss}";
        formData.AddField("folder", folder);
        formData.AddField("username", "");

        Texture2D mainPhoto = form.GetMainPhoto();
        if (mainPhoto != null)
        {
            Texture2D resizedMainPhoto = ResizeTextureWithRenderTexture(mainPhoto, 444, 444);
            byte[] mainPhotoBytes = resizedMainPhoto.EncodeToJPG(50);
            if (mainPhotoBytes.Length == 0)
            {
                Debug.LogError("[CubeDataFixManager] 메인 사진 데이터가 비어 있습니다!");
                ShowWarning(GetLocalizedText("main_photo_upload_failed"));
                HideBusy();
                yield break;
            }
            string mainPath = "main.jpg";
            formData.AddBinaryData("main_photo", mainPhotoBytes, mainPath, "image/jpeg");
            Destroy(resizedMainPhoto);
        }
        else
        {
            ShowWarning(GetLocalizedText("upload_main_photo"));
            HideBusy();
            yield break;
        }

        List<Texture2D> subPhotos = form.GetSubPhotos();
        for (int i = 1; i <= subPhotos.Count; i++)
        {
            if (i > MAX_SUB_PHOTOS) break;
            Texture2D subPhoto = subPhotos[i - 1];
            Texture2D resizedSubPhoto = ResizeTextureKeepAspectWithRenderTexture(subPhoto, 800, 800);
            byte[] subPhotoBytes = resizedSubPhoto.EncodeToJPG(50);
            if (subPhotoBytes.Length == 0)
            {
                Debug.LogError($"[CubeDataFixManager] 서브 사진 #{i} 데이터가 비어 있습니다!");
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
            LoginManager.ApplyAuth(www);
            bool timedOut = false;
            yield return StartCoroutine(CubeUploadManager.SendRequestWithTimeout(www, uploadTimeoutSeconds, ShowUploadProgress, t => timedOut = t));

            HideBusy();

            if (timedOut)
            {
                // 입력은 그대로 — 다시 누를 수 있다
                Debug.LogWarning($"[CubeDataFixManager] 수정 요청 시간 초과 ({uploadTimeoutSeconds}s) — 요청 중단");
                isProcessing = true;
                ShowWarning(GetLocalizedText("request_timeout"));
                yield break;
            }

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;

                if (responseText.Contains("Fix Upload Succeeded!") || www.responseCode == 200)
                {
                    isProcessing = false;
                    ShowWarning(GetLocalizedText("fix_success"));

                    SetUIActive(fixUIPanel, false);
                    ResetToInitialState();
                    yield break;
                }
                else
                {
                    Debug.LogWarning($"[CubeDataFixManager] 서버 응답이 성공으로 간주되지 않음: {responseText}");
                    isProcessing = true;
                    ShowWarning(GetLocalizedText("server_error"));
                }
            }
            else
            {
                Debug.LogError($"[CubeDataFixManager] 수정 요청 실패: {www.error} (응답 코드: {www.responseCode})");
                isProcessing = true;
                ShowWarning(GetLocalizedText("server_error"));
            }
        }
    }
    #endregion

    #region 내 장소 고치기 (2026-10-26)
    // 대표님 (10-25): 「'정보 수정' 버튼을 눌러서 수정할 때는 기존에 있던 것들을 그대로 불러와서 각 사진 우측 위에 (메인사진포함) x버튼 …
    // 순서대로 있던 그대로 불러와서 … '수정하기' 버튼을 눌러서 수정되도록」 · 「본인이 올린 것에 한해서는 직접 삭제」 ·
    // 「위치가 좀 바뀌었다면 … 수정한 사람이 수정하는 경우 이게 바로 적용, 위치가 바뀌도록」
    // 서버가 내 장소라고 확인하면 바로 고친다(/api/place/<id>/edit) — 남의 장소·확인 못 한 때는 예전처럼 수정 요청(/fix_upload)

    /// <summary>장소 '···' 의 '정보 수정' — 그 장소의 지금 값(이름·분류·스위치·사진을 올린 순서 그대로)으로 채운다</summary>
    private void BeginEdit()
    {
        loadGen++;
        ResetToInitialState();
        editId = 0;
        PlaceData local = DataManager.Instance != null ? DataManager.Instance.PlaceOf(doubleTap) : null;
        SetOwnerMode(RemoveRequest.LooksMine(local));
        if (local == null)
        {
            // 우팡 장소가 아닌 것(공공데이터·교통 등) — 이름만 (예전엔 빈 칸)
            if (nameInput != null && doubleTap != null) nameInput.text = doubleTap.GetName() ?? "";
            return;
        }
        editId = local.id;
        StartCoroutine(LoadPlace(local, loadGen));
    }

    private IEnumerator LoadPlace(PlaceData local, int gen)
    {
        isProcessing = true;   // 다 불러오기 전엔 저장·사진 고르기를 막는다
        Fill(local);
        MainLoading(true);

        PlaceData place = local;
        bool mine = false;
        using (UnityWebRequest www = UnityWebRequest.Get(PlaceUrl(local.id)))
        {
            LoginManager.ApplyAuth(www);
            www.timeout = 15;
            yield return www.SendWebRequest();
            if (gen != loadGen) yield break;
            PlaceEditResponse res = www.result == UnityWebRequest.Result.Success ? PlaceEditResponse.Parse(www.downloadHandler.text) : null;
            if (res != null && res.ok && res.place != null && res.place.id == local.id)
            {
                place = res.place;
                mine = res.mine;
            }
        }
        // 내 장소인지는 서버가 정한다 — 묻지 못했으면 수정 요청으로 (요청은 옛 서버에서도 된다)
        SetOwnerMode(mine);
        Fill(place);

        List<string> subs = new List<string>();
        foreach (string p in DataManager.FlattenSubPhotos(place))
            if (!subs.Contains(p) && subs.Count < MAX_SUB_PHOTOS) subs.Add(p);
        loadedSubs = subs.Count;
        SubLoading(subs.Count);
        int failed = 0;

        if (!string.IsNullOrEmpty(place.main_photo))
        {
            Texture2D tex = null;
            yield return StartCoroutine(DownloadPhoto(place.main_photo, gen, t => tex = t));
            if (gen != loadGen) { if (tex != null) Destroy(tex); yield break; }
            if (tex == null) { failed++; if (ownerMode) tex = Placeholder(); }
            if (tex != null)
            {
                mainPhoto = tex;
                mainKeepPath = place.main_photo;
                SetMainSprite(tex);
                if (mainPhotoDisplay != null) mainPhotoDisplay.gameObject.SetActive(true);
            }
        }
        MainLoading(false);

        int left = subs.Count;
        foreach (string p in subs)
        {
            Texture2D tex = null;
            yield return StartCoroutine(DownloadPhoto(p, gen, t => tex = t));
            if (gen != loadGen) { if (tex != null) Destroy(tex); yield break; }
            if (tex == null) { failed++; if (ownerMode) tex = Placeholder(); }
            if (tex != null) AddSub(tex, p);
            SubLoading(--left);
            UpdateSubPhotoGrid();
        }
        SubLoading(0);
        if (failed > 0)
            ShowWarning(ownerMode
                ? R0926LocalizedText.Say("사진 " + failed + "장을 불러오지 못했어요 — 그대로 두면 지워지지 않아요", "Couldn't load " + failed + " photo(s) — they stay unless you remove them", "写真" + failed + "枚を読み込めませんでした — そのままなら消えません", failed + " 张照片未能加载 — 不删除就会保留", "No se cargaron " + failed + " foto(s) — se conservan si no las quitas")
                : R0926LocalizedText.Say("사진 " + failed + "장을 불러오지 못했어요", "Couldn't load " + failed + " photo(s)", "写真" + failed + "枚を読み込めませんでした", failed + " 张照片未能加载", "No se cargaron " + failed + " foto(s)"));
        isProcessing = false;
    }

    // 서버 사진 한 장 — 512 썸네일 먼저(옛 사진은 원본이 2048px 까지라 열 장이면 메모리가 크다), 안 되면 원본
    private IEnumerator DownloadPhoto(string path, int gen, Action<Texture2D> done)
    {
        Texture2D got = null;
        string clean = path.Replace("\\", "/").TrimStart('/');
        string[] urls = path.StartsWith("http") ? new[] { path } : new[] { Base + "/thumb/512/" + clean, Base + "/" + clean };
        foreach (string url in urls)
        {
            using (UnityWebRequest r = UnityWebRequestTexture.GetTexture(url, true))   // 읽기 전용 — CPU 쪽 사본을 남기지 않는다
            {
                r.timeout = 20;
                yield return r.SendWebRequest();
                if (gen != loadGen) break;
                if (r.result == UnityWebRequest.Result.Success)
                {
                    got = DownloadHandlerTexture.GetContent(r);
                    if (got != null) break;
                }
            }
        }
        done(got);
    }

    // 받지 못한 사진 — 내 장소면 회색 칸으로 남긴다 (경로는 그대로라 저장해도 지워지지 않는다 · x 로 뺄 수 있다)
    private static Texture2D Placeholder()
    {
        var t = new Texture2D(8, 8, TextureFormat.RGB24, false);
        var px = new Color32[64];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(58, 60, 66, 255);
        t.SetPixels32(px);
        t.Apply(false, true);
        return t;
    }

    private void Fill(PlaceData p)
    {
        if (nameInput != null) nameInput.text = p.name ?? "";
        if (petFriendlyToggle != null) petFriendlyToggle.isOn = p.pet_friendly;
        if (separateRestroomToggle != null) separateRestroomToggle.isOn = p.separate_restroom;
        string insta = (p.instagram_id ?? "").Trim();
        if (instagramToggle != null) instagramToggle.isOn = insta.Length > 0;   // 켜야 칸이 보인다 — 끄면 칸을 비우므로 글은 그 뒤에
        if (instagramIDInput != null && insta.Length > 0) instagramIDInput.text = insta;
        SetCategory(p.category);
        hasPlaceCoords = !(p.latitude == 0f && p.longitude == 0f);
        placeLat = p.latitude;
        placeLon = p.longitude;
        RefreshLocation(true);
    }

    // 장소의 분류를 칩에 — 고를 수 없는 옛 분류(gov 등)는 칩을 비워 두고 값은 그대로 (저장해도 바뀌지 않게)
    private void SetCategory(string raw)
    {
        raw = raw ?? "";
        int idx = Array.IndexOf(categoryValues, raw);
        currentCategoryIndex = idx > 0 ? idx : 0;
        selectedCategory = raw;
        loadedCategory = raw;
        if (categoryToggle != null)
        {
            categoryToggle.onValueChanged.RemoveListener(OnCategoryToggleChanged);
            categoryToggle.isOn = currentCategoryIndex != 0;
            categoryToggle.onValueChanged.AddListener(OnCategoryToggleChanged);
        }
        UpdateCategoryToggleUI();
    }

    /// <summary>내 장소면 '내 장소 수정'(바로 고침 · 위치 줄 · 삭제) — 아니면 '장소 수정 요청'(수정할 내용 칸)</summary>
    private void SetOwnerMode(bool mine)
    {
        ownerMode = mine;
        if (localizer != null) localizer.SetOwnerMode(mine);
        if (descriptionInput != null) descriptionInput.gameObject.SetActive(!mine);
        SetUIActive(descriptionLabel, !mine);
        SetUIActive(locationLabel, mine);
        SetUIActive(locationRow, mine);
        SetUIActive(deletePlaceButton != null ? deletePlaceButton.gameObject : null, mine);
        if (moveHereToggle != null) moveHereToggle.isOn = false;
        if (submitLabel != null)
        {
            if (mine) submitLabel.Set("수정하기", "Save changes", "保存する", "保存修改", "Guardar cambios");
            else submitLabel.Set("수정 요청", "Send request", "修正を依頼", "提交申请", "Enviar solicitud");
        }
        RefreshLocation(true);
    }

    private void AddSub(Texture2D tex, string keepPath)
    {
        subPhotos.Add(tex);
        subKeepPaths.Add(keepPath);
    }

    /// <summary>추가 사진 x — 그 사진만 빼고 뒤 사진을 당긴다. 사진이 있던 장소면 마지막 한 장은 빼지 않는다(새 사진을 먼저 넣고 빼면 된다)</summary>
    private void RemoveSub(int i)
    {
        if (isProcessing || i < 0 || i >= subPhotos.Count || (subReorder != null && subReorder.Engaged)) return;
        if (subPhotos.Count == 1 && loadedSubs > 0)
        {
            ShowWarning(R0926LocalizedText.Say("추가 사진은 한 장 이상 있어야 해요 — 새 사진을 먼저 넣고 빼 주세요", "Keep at least one extra photo — add a new one first, then remove this",
                "追加写真は1枚以上必要です — 先に新しい写真を入れてから外してください", "至少要保留一张附加照片 — 请先添加新照片再删除", "Debe quedar al menos una foto adicional — añade otra antes de quitar esta"));
            return;
        }
        Destroy(subPhotos[i]);
        subPhotos.RemoveAt(i);
        if (i < subKeepPaths.Count) subKeepPaths.RemoveAt(i);
        UpdateSubPhotoGrid();
    }

    /// <summary>꾹 눌러 옮기기 — from 자리 사진을 to 자리로 (사이 사진은 한 칸씩 밀린다). 저장하는 순서도 이대로</summary>
    private void MoveSub(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= subPhotos.Count || to >= subPhotos.Count) return;
        var tex = subPhotos[from];
        subPhotos.RemoveAt(from);
        subPhotos.Insert(to, tex);
        if (from < subKeepPaths.Count && to < subKeepPaths.Count)
        {
            string keep = subKeepPaths[from];
            subKeepPaths.RemoveAt(from);
            subKeepPaths.Insert(to, keep);
        }
        UpdateSubPhotoGrid();
    }

    /// <summary>대표 사진 x — 빼면 저장 전에 새 대표 사진을 넣어야 한다 (대표 사진은 꼭 있어야 한다)</summary>
    private void RemoveMain()
    {
        if (isProcessing) return;
        if (mainPhoto != null) Destroy(mainPhoto);
        mainPhoto = null;
        mainKeepPath = null;
        SetMainSprite(null);
        if (mainPhotoDisplay != null) mainPhotoDisplay.gameObject.SetActive(false);
    }

    private void AskDelete()
    {
        if (isProcessing || !ownerMode || editId <= 0 || removeRequest == null) return;
        removeRequest.AskOwnerDelete(editId);
    }

    private void OnPlaceDeleted(int id)
    {
        if (id != editId) return;
        loadGen++;
        SetUIActive(fixUIPanel, false);
        ResetToInitialState();
        editId = 0;
    }

    // 위치 줄 — 장소 좌표 · 지금 내 위치에서 얼마나 떨어졌나 (스위치를 켜면 저장할 때 지금 위치로 옮긴다)
    private void RefreshLocation(bool force)
    {
        if (!ownerMode || locationRow == null) return;
        if (!force && Time.unscaledTime < nextNoteAt) return;
        nextNoteAt = Time.unscaledTime + 1f;
        if (locationCoords != null)
            locationCoords.text = hasPlaceCoords
                ? placeLat.ToString("F4", CultureInfo.InvariantCulture) + ", " + placeLon.ToString("F4", CultureInfo.InvariantCulture)
                : "—";
        if (locationNote == null) return;
        locationNote.color = noteColor;
        bool move = moveHereToggle != null && moveHereToggle.isOn;
        if (!TryGetHere(out double lat, out double lon, out _))
        {
            locationNote.text = R0926LocalizedText.Say("지금 위치를 찾는 중…", "Finding your location…", "現在地を取得中…", "正在获取你的位置…", "Buscando tu ubicación…");
            return;
        }
        if (!hasPlaceCoords)
        {
            locationNote.text = move ? R0926LocalizedText.Say("저장하면 지금 위치로 옮겨요", "Saving moves it to where you are", "保存すると現在地に移動します", "保存后移到你现在的位置", "Al guardar se moverá a tu ubicación") : "";
            return;
        }
        double m = Meters(placeLat, placeLon, lat, lon);
        string d = m < 1000 ? Mathf.RoundToInt((float)m) + "m" : (m / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "km";
        if (!move)
            locationNote.text = R0926LocalizedText.Say("지금 내 위치에서 " + d, d + " from you", "現在地から" + d, "距你 " + d, "A " + d + " de ti");
        else if (m > 1000)
        {
            locationNote.text = R0926LocalizedText.Say(d + " 떨어진 지금 위치로 옮겨져요", "It will move " + d + " to where you are", d + "離れた現在地に移動します", "将移到 " + d + " 外你现在的位置", "Se moverá " + d + " hasta tu ubicación");
            locationNote.color = new Color(1f, 0.42f, 0.42f, 1f);   // 멀리 옮기는 건 실수일 수 있다
        }
        else
            locationNote.text = R0926LocalizedText.Say("저장하면 지금 위치로 옮겨요 · " + d, "Saving moves it to where you are · " + d, "保存すると現在地に移動します · " + d, "保存后移到你现在的位置 · " + d, "Al guardar se moverá a tu ubicación · " + d);
    }

    // 지금 내 위치 — 장소 추가와 같은 차례 (Geospatial → 휴대폰 위치 + 고도 보정 · 에디터는 가상 위치)
    private bool TryGetHere(out double lat, out double lon, out double alt) => GeoHere.TryGet(out lat, out lon, out alt, earthManager);

    private static double Meters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0, Rad = Math.PI / 180.0;
        double dLat = (lat2 - lat1) * Rad, dLon = (lon2 - lon1) * Rad;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Rad) * Math.Cos(lat2 * Rad) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    private IEnumerator SubmitOwnerEdit(string name, bool petFriendly, bool separateRestroom, string instagramID)
    {
        // 옮기기를 켰으면 지금 위치 — 못 읽으면 보내지 않는다 (엉뚱한 곳으로 옮기지 않게)
        bool move = moveHereToggle != null && moveHereToggle.isOn;
        double lat = 0, lon = 0, alt = 0;
        if (move && !TryGetHere(out lat, out lon, out alt))
        {
            ShowWarning(R0926LocalizedText.Say("지금 위치를 아직 못 찾았어요", "Can't find your location yet", "現在地がまだ見つかりません", "还没找到你的位置", "Aún no encontramos tu ubicación"));
            isProcessing = true;
            yield break;
        }

        ShowUploadProgress(0);
        yield return null;   // 카드가 먼저 그려지게 — 사진 줄이기·인코딩은 그동안 화면을 멈춘다

        WWWForm form = new WWWForm();
        form.AddField("name", name);
        form.AddField("category", selectedCategory);
        if (selectedCategory != loadedCategory && Array.IndexOf(categoryValues, selectedCategory) > 0)
            form.AddField("color", ColorUtility.ToHtmlStringRGB(GetCategoryColor(selectedCategory)));   // 분류를 바꿨을 때만 — 그대로면 원래 색 · 비우면 서버가 NULL(흰색)
        form.AddField("pet_friendly", petFriendly ? "true" : "false");
        form.AddField("separate_restroom", separateRestroom ? "true" : "false");
        form.AddField("instagram_id", instagramID);

        if (mainKeepPath != null) form.AddField("main", "keep");
        else
        {
            Texture2D resized = ResizeTextureWithRenderTexture(mainPhoto, 444, 444);
            byte[] bytes = resized.EncodeToJPG(50);
            Destroy(resized);
            form.AddField("main", "new");
            form.AddBinaryData("main_photo", bytes, "main.jpg", "image/jpeg");
        }

        // 추가 사진 — 보이는 순서대로, 서버에 있던 것은 경로 · 새로 고른 것은 "new:n" (n 번째 sub_photos 파일)
        List<string> plan = new List<string>();
        int fresh = 0;
        for (int i = 0; i < subPhotos.Count && i < MAX_SUB_PHOTOS; i++)
        {
            string keep = i < subKeepPaths.Count ? subKeepPaths[i] : null;
            if (keep != null)
            {
                plan.Add(keep);
                continue;
            }
            Texture2D resized = ResizeTextureKeepAspectWithRenderTexture(subPhotos[i], 800, 800);
            byte[] bytes = resized.EncodeToJPG(50);
            Destroy(resized);
            if (bytes.Length == 0) continue;
            plan.Add("new:" + fresh);
            form.AddBinaryData("sub_photos", bytes, "sub_" + (fresh + 1) + ".jpg", "image/jpeg");
            fresh++;
        }
        form.AddField("subs", JsonConvert.SerializeObject(plan));

        if (move)
        {
            form.AddField("latitude", lat.ToString("F6", CultureInfo.InvariantCulture));
            form.AddField("longitude", lon.ToString("F6", CultureInfo.InvariantCulture));
            form.AddField("altitude", alt.ToString("F2", CultureInfo.InvariantCulture));
        }

        using (UnityWebRequest www = UnityWebRequest.Post(PlaceUrl(editId, "/edit"), form))
        {
            LoginManager.ApplyAuth(www);
            bool timedOut = false;
            yield return StartCoroutine(CubeUploadManager.SendRequestWithTimeout(www, uploadTimeoutSeconds, ShowUploadProgress, t => timedOut = t));
            HideBusy();

            if (timedOut)
            {
                isProcessing = true;   // 입력은 그대로 — 다시 누를 수 있다
                ShowWarning(GetLocalizedText("request_timeout"));
                yield break;
            }

            PlaceEditResponse res = PlaceEditResponse.Parse(www.downloadHandler != null ? www.downloadHandler.text : null);
            if (www.result == UnityWebRequest.Result.Success && res != null && res.ok && res.place != null)
            {
                if (DataManager.Instance != null) DataManager.Instance.ApplyEditedPlace(res.place, res.moved);
                isProcessing = false;
                ShowWarning(R0926LocalizedText.Say("고쳤어요", "Saved", "保存しました", "已保存", "Guardado"));
                SetUIActive(fixUIPanel, false);
                ResetToInitialState();
                yield break;
            }

            LoginManager.CheckUnauthorized(www);
            string err = res != null ? res.error ?? "" : "";
            Debug.LogWarning($"[CubeDataFixManager] 고치기 실패 id={editId}: {www.responseCode} {err} {www.error}");
            if (err == "not_owner") SetOwnerMode(false);   // 이름만 같은 남의 장소 — 이제 수정 요청으로 보낼 수 있다
            isProcessing = true;
            ShowWarning(EditError(err));
        }
    }

    private string EditError(string code)
    {
        switch (code)
        {
            case "login_required": return R0926LocalizedText.Say("로그인이 필요해요", "Please log in", "ログインが必要です", "请先登录", "Inicia sesión");
            case "not_owner": return R0926LocalizedText.Say("내가 올린 장소가 아니에요 — 수정 요청으로 보내 주세요", "Not your place — send it as a request", "自分の場所ではありません — 修正依頼として送ってください", "不是你上传的地点 — 请提交修改申请", "No es tu lugar — envíalo como solicitud");
            case "not_found": return R0926LocalizedText.Say("이미 지워진 장소예요", "This place was deleted", "この場所は削除されています", "该地点已被删除", "Este lugar fue eliminado");
            case "unknown_photo": return R0926LocalizedText.Say("다른 곳에서 고쳐진 장소예요 — 다시 열어 주세요", "Changed elsewhere — open it again", "他の場所で変更されました — もう一度開いてください", "已在别处修改 — 请重新打开", "Se cambió en otro lugar — ábrelo de nuevo");
            case "bad_photo": return R0926LocalizedText.Say("사진을 읽지 못했어요 — 다른 사진으로 해 주세요", "Couldn't read a photo — try another", "写真を読み込めませんでした — 別の写真にしてください", "无法读取照片 — 请换一张", "No se pudo leer una foto — prueba con otra");
            case "bad_location": return R0926LocalizedText.Say("지금 위치가 이상해요 — 잠시 뒤 다시 해 주세요", "Your location looks wrong — try again", "現在地が正しくありません — 後でもう一度", "你的位置异常 — 请稍后再试", "Tu ubicación no es válida — inténtalo de nuevo");
            case "name_too_long": return R0926LocalizedText.Say("이름이 너무 길어요", "The name is too long", "名前が長すぎます", "名称太长", "El nombre es demasiado largo");
            case "too_large": return R0926LocalizedText.Say("사진이 너무 커요", "Photos are too large", "写真が大きすぎます", "照片太大", "Las fotos son demasiado grandes");
            default: return GetLocalizedText("server_error");
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
                return "Europe/Madrid";
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

    public Texture2D GetMainPhoto()
    {
        return mainPhoto;
    }

    public List<Texture2D> GetSubPhotos()
    {
        return subPhotos;
    }
    #endregion

    #region Localization
    private string GetLocalizedText(string key)
    {
        // 1단계: LocalizationManager가 있으면 우선 사용
        if (LocalizationManager.Instance != null)
        {
            return LocalizationManager.Instance.GetText(key);
        }

        // 2단계: LocalizationManager가 없으면 내장 다국어 사용
        SystemLanguage lang = Application.systemLanguage;
        
        switch (key)
        {
            case "loading_main_photo":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "메인사진 로딩 중...";
                    case SystemLanguage.Japanese: return "メイン写真読み込み中...";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "正在加载主照片...";
                    case SystemLanguage.Spanish: return "Cargando foto principal...";
                    default: return "Loading main photo...";
                }

            case "loading_sub_photos":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "서브 사진 로딩 중...";
                    case SystemLanguage.Japanese: return "サブ写真読み込み中...";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "正在加载子照片...";
                    case SystemLanguage.Spanish: return "Cargando fotos secundarias...";
                    default: return "Loading sub photos...";
                }

            case "photo_selection_failed":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "사진 선택에 실패했습니다";
                    case SystemLanguage.Japanese: return "写真の選択に失敗しました";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "照片选择失败";
                    case SystemLanguage.Spanish: return "Error al seleccionar la foto";
                    default: return "Photo selection failed";
                }

            case "main_photo_crop_failed":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "메인 사진 크롭에 실패했습니다";
                    case SystemLanguage.Japanese: return "メイン写真のクロップに失敗しました";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "主照片裁剪失败";
                    case SystemLanguage.Spanish: return "Error al recortar la foto principal";
                    default: return "Main photo crop failed";
                }

            case "max_sub_photos_exceeded":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "최대 10장까지만 선택 가능합니다";
                    case SystemLanguage.Japanese: return "最大10枚まで選択できます";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "最多只能选择10张照片";
                    case SystemLanguage.Spanish: return "Solo se pueden seleccionar hasta 10 fotos";
                    default: return "Maximum 10 photos can be selected";
                }

            case "sub_photos_reset":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "서브 사진이 리셋되었습니다";
                    case SystemLanguage.Japanese: return "サブ写真がリセットされました";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "子照片已重置";
                    case SystemLanguage.Spanish: return "Las fotos secundarias se han restablecido";
                    default: return "Sub photos have been reset";
                }

            case "no_object_selected":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "수정할 오브젝트가 선택되지 않았습니다";
                    case SystemLanguage.Japanese: return "修正するオブジェクトが選択されていません";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "未选择要修改的对象";
                    case SystemLanguage.Spanish: return "No se ha seleccionado objeto para modificar";
                    default: return "No object selected for modification";
                }

            case "valid_id_not_found":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "유효한 ID가 발견되지 않았습니다";
                    case SystemLanguage.Japanese: return "有効なIDが見つかりませんでした";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "未找到有效ID";
                    case SystemLanguage.Spanish: return "No se encontró un ID válido";
                    default: return "Valid ID not found";
                }

            case "enter_description":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "설명을 입력해주세요";
                    case SystemLanguage.Japanese: return "説明を入力してください";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "请输入描述";
                    case SystemLanguage.Spanish: return "Por favor ingrese una descripción";
                    default: return "Please enter a description";
                }

            case "enter_place_name":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "장소 이름을 입력해주세요";
                    case SystemLanguage.Japanese: return "場所名を入力してください";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "请输入地点名称";
                    case SystemLanguage.Spanish: return "Por favor ingrese el nombre del lugar";
                    default: return "Please enter place name";
                }

            case "upload_main_photo":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "메인 사진을 업로드해주세요";
                    case SystemLanguage.Japanese: return "メイン写真をアップロードしてください";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "请上传主照片";
                    case SystemLanguage.Spanish: return "Por favor suba la foto principal";
                    default: return "Please upload main photo";
                }

            case "main_photo_upload_failed":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "메인 사진 업로드에 실패했습니다";
                    case SystemLanguage.Japanese: return "メイン写真のアップロードに失敗しました";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "主照片上传失败";
                    case SystemLanguage.Spanish: return "Error al subir la foto principal";
                    default: return "Main photo upload failed";
                }

            case "sub_photo_upload_failed":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "서브 사진 업로드에 실패했습니다";
                    case SystemLanguage.Japanese: return "サブ写真のアップロードに失敗しました";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "子照片上传失败";
                    case SystemLanguage.Spanish: return "Error al subir la foto secundaria";
                    default: return "Sub photo upload failed";
                }

            case "submitting_countdown":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "제출 중... {0}";
                    case SystemLanguage.Japanese: return "送信中... {0}";
                    case SystemLanguage.Chinese:
                    case SystemLanguage.ChineseSimplified: return "提交中... {0}";
                    case SystemLanguage.Spanish: return "Enviando... {0}";
                    default: return "Submitting... {0}";
                }

            case "request_timeout":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "요청 시간이 초과되었습니다";
                    case SystemLanguage.Japanese: return "リクエストがタイムアウトしました";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "请求超时";
                    case SystemLanguage.Spanish: return "Se agotó el tiempo de solicitud";
                    default: return "Request timeout";
                }

            case "fix_success":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "수정이 완료되었습니다";
                    case SystemLanguage.Japanese: return "修正が完了しました";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "修改完成";
                    case SystemLanguage.Spanish: return "Modificación completada";
                    default: return "Fix completed successfully";
                }

            case "server_error":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "서버 오류가 발생했습니다";
                    case SystemLanguage.Japanese: return "サーバーエラーが発生しました";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "服务器错误";
                    case SystemLanguage.Spanish: return "Error del servidor";
                    default: return "Server error occurred";
                }

            case "category_select":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "분류";
                    case SystemLanguage.Japanese: return "カテゴリー";
                    case SystemLanguage.Chinese:
                    case SystemLanguage.ChineseSimplified: return "分类";
                    case SystemLanguage.Spanish: return "Categoría";
                    default: return "Category";
                }

            case "category_shop":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "샵";
                    case SystemLanguage.Japanese: return "ショップ";
                    case SystemLanguage.Chinese:
                    case SystemLanguage.ChineseSimplified: return "商店";
                    case SystemLanguage.Spanish: return "Tienda";
                    default: return "Shop";
                }

            case "category_food":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "음식점";
                    case SystemLanguage.Japanese: return "飲食店";
                    case SystemLanguage.Chinese:
                    case SystemLanguage.ChineseSimplified: return "餐厅";
                    case SystemLanguage.Spanish: return "Restaurante";
                    default: return "Restaurant";
                }

            case "category_cafe":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "카페";
                    case SystemLanguage.Japanese: return "カフェ";
                    case SystemLanguage.Chinese:
                    case SystemLanguage.ChineseSimplified: return "咖啡厅";
                    case SystemLanguage.Spanish: return "Café";
                    default: return "Cafe";
                }

            case "category_park":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "공원";
                    case SystemLanguage.Japanese: return "公園";
                    case SystemLanguage.Chinese:
                    case SystemLanguage.ChineseSimplified: return "公园";
                    case SystemLanguage.Spanish: return "Parque";
                    default: return "Park";
                }

            case "category_etc":
                switch (lang)
                {
                    case SystemLanguage.Korean: return "기타";
                    case SystemLanguage.Japanese: return "その他";
                    case SystemLanguage.Chinese:
                    case SystemLanguage.ChineseSimplified: return "其他";
                    case SystemLanguage.Spanish: return "Otros";
                    default: return "Others";
                }

            default:
                return key;
        }
    }
    #endregion

    #region State Management
    private void ResetToInitialState()
    {
        // 사진 관련 초기화
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
        subKeepPaths.Clear();
        loadedSubs = 0;
        mainKeepPath = null;

        if (mainPhotoDisplay != null) 
        {
            SetMainSprite(null);
            mainPhotoDisplay.gameObject.SetActive(false);
        }
        MainLoading(false);
        SubLoading(0);
        if (subPhotoGrid != null)
        {
            foreach (var display in subPhotoDisplays)
            {
                if (display.sprite != null) Destroy(display.sprite);
                display.sprite = null;
                display.gameObject.SetActive(false);
            }
        }

        // UI 요소 초기화
        if (descriptionInput != null) descriptionInput.text = "";
        if (nameInput != null) nameInput.text = "";
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

        if (disableObject != null)
        {
            disableObject.SetActive(false);
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

        loadedCategory = "";
        hasPlaceCoords = false;
        if (moveHereToggle != null) moveHereToggle.isOn = false;
        isProcessing = false;
    }
    #endregion

    #region Debug Methods
    [ContextMenu("Test Fix UI")]
    void TestFixUI()
    {
        if (fixUIPanel != null)
        {
            fixUIPanel.SetActive(!fixUIPanel.activeSelf);
        }
    }

    [ContextMenu("Reset All States")]
    void ResetAllStates()
    {
        ResetToInitialState();
    }
    #endregion
}

/// <summary>GET /api/place/&lt;id&gt; · POST /api/place/&lt;id&gt;/edit · DELETE /api/place/&lt;id&gt; 의 답</summary>
public class PlaceEditResponse
{
    public bool ok;
    public bool mine;
    public bool moved;
    public string error;
    public PlaceData place;

    /// <summary>JSON 이 아니면(옛 서버의 404 화면 등) null</summary>
    public static PlaceEditResponse Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonConvert.DeserializeObject<PlaceEditResponse>(json, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }
        catch (Exception)
        {
            return null;
        }
    }
}
