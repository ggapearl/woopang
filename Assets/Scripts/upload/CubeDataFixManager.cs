using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using System;

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

    [Header("Upload Settings")]
    [SerializeField] private string serverUrl = "https://woopang.com:5000/fix_upload/";
    [SerializeField] private float uploadTimeoutSeconds = 20f;
    [SerializeField] private int countdownSeconds = 20;

    // HEIC 처리용 변수들
    private readonly string[] iOSImageFormats = { 
        ".heic", ".heif", ".png", ".jpg", ".jpeg", 
        ".tiff", ".tif", ".bmp", ".gif" 
    };

    private Texture2D mainPhoto;
    private List<Texture2D> subPhotos = new List<Texture2D>();
    private List<Image> subPhotoDisplays = new List<Image>();
    private bool showInstagram;
    private const int MAX_SUB_PHOTOS = 10;
    private bool isProcessing = false;
    private float elapsedTime = 0f;

    private void Awake()
    {
        if (FindObjectsOfType<CubeDataFixManager>().Length > 1)
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
        if (doubleTap != null)
        {
            Debug.Log($"[CubeDataFixManager] 초기 DoubleTap3D 연결됨 - ID: {doubleTap.GetId()}, GameObject: {(doubleTap != null ? doubleTap.gameObject.name : "null")}");
        }
        else
        {
            Debug.LogWarning("[CubeDataFixManager] DoubleTap3D가 연결되지 않았습니다. 더블 터치로 동적으로 설정됩니다.");
        }

        InitializeComponents();
        ResetToInitialState();

        DoubleTap3D.OnDoubleTapEvent += HandleDoubleTap;
        Debug.Log("[CubeDataFixManager] DoubleTap3D.OnDoubleTapEvent 구독 완료");
    }

    void OnDestroy()
    {
        DoubleTap3D.OnDoubleTapEvent -= HandleDoubleTap;
        Debug.Log("[CubeDataFixManager] DoubleTap3D.OnDoubleTapEvent 구독 해제");
    }

    private void HandleDoubleTap(DoubleTap3D tappedDoubleTap)
    {
        doubleTap = tappedDoubleTap;
        Debug.Log($"[CubeDataFixManager] 더블 터치로 DoubleTap3D 업데이트 - ID: {(doubleTap != null ? doubleTap.GetId() : -1)}, GameObject: {(doubleTap != null ? doubleTap.gameObject.name : "null")}");
    }

    #region Component Initialization
    private void InitializeComponents()
    {
        if (instagramToggle != null) instagramToggle.onValueChanged.AddListener(OnInstagramToggleChanged);
        else Debug.LogError("[CubeDataFixManager] InstagramToggle이 할당되지 않았습니다!");

        if (mainPhotoButton != null) mainPhotoButton.onClick.AddListener(() => StartCoroutine(SelectAndCropMainPhoto()));
        else Debug.LogError("[CubeDataFixManager] MainPhotoButton이 할당되지 않았습니다!");

        if (subPhotosButton != null) subPhotosButton.onClick.AddListener(() => StartCoroutine(SelectSubPhotos()));
        else Debug.LogError("[CubeDataFixManager] SubPhotosButton이 할당되지 않았습니다!");

        if (resetPhotosButton != null) resetPhotosButton.onClick.AddListener(ResetSubPhotos);
        else Debug.LogError("[CubeDataFixManager] ResetPhotosButton이 할당되지 않았습니다!");

        if (submitButton != null) submitButton.onClick.AddListener(() => StartCoroutine(ValidateAndSubmit()));
        else Debug.LogError("[CubeDataFixManager] SubmitButton이 할당되지 않았습니다!");

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
            instagramIDInput.interactable = value;
            instagramIDInput.image.color = value ? Color.white : Color.gray;
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
            imageObj.SetActive(false);
        }
        Debug.Log($"✅ [CubeDataFixManager] 서브 사진 디스플레이 초기화 완료: {subPhotoDisplays.Count}개");
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
        Debug.Log($"[HEIC] 2단계 변환 시작: {Path.GetFileName(imagePath)}");
        
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

        if (convertedTexture != null)
        {
            Debug.Log($"[HEIC] ✅ 2단계 변환 성공: {convertedTexture.width}x{convertedTexture.height}");
        }
        else
        {
            Debug.LogError($"[HEIC] ❌ 2단계 변환 실패: {Path.GetFileName(imagePath)}");
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
                    
                    Debug.Log($"[HEIC] iOS 네이티브 변환 성공: {result.width}x{result.height}");
                    
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
    private IEnumerator SelectAndCropMainPhoto()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        ShowSpinner(GetLocalizedText("loading_main_photo"));
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
                ShowWarning(GetLocalizedText("photo_selection_failed"));
                isLoading = false;
            }
        }, "image/*");

        yield return new WaitUntil(() => !isLoading);
        HideSpinner();
        isProcessing = false;
    }

    private IEnumerator ProcessMainPhotoWithFallback(string path, System.Action onComplete)
    {
        bool step1Success = false;
        bool processingComplete = false;

        // 1단계: NativeGallery.LoadImageAtPath 시도
        try
        {
            Debug.Log($"[HEIC] 1단계 시도: {Path.GetFileName(path)}");
            Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize: 2048);
            
            if (texture != null && texture.width > 8 && texture.height > 8)
            {
                Debug.Log($"[HEIC] ✅ 1단계 성공 - NativeGallery 로드: {texture.width}x{texture.height}");
                ProcessCropAndDisplay(texture, () => {
                    processingComplete = true;
                });
                step1Success = true;
            }
            else
            {
                Debug.Log($"[HEIC] ⚠️ 1단계 실패 - 2단계 시도 필요");
                if (texture != null) Destroy(texture);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HEIC] 1단계 예외: {e.Message}");
        }

        if (step1Success)
        {
            yield return new WaitUntil(() => processingComplete);
            yield break;
        }

        // 2단계: 수동 HEIC 변환
        processingComplete = false;
        yield return StartCoroutine(LoadImageWithConversion(path, (convertedTexture) => {
            if (convertedTexture != null)
            {
                Debug.Log($"[HEIC] ✅ 2단계 성공 - 수동 변환: {convertedTexture.width}x{convertedTexture.height}");
                ProcessCropAndDisplay(convertedTexture, () => {
                    processingComplete = true;
                });
            }
            else
            {
                Debug.LogError($"[HEIC] ❌ 모든 단계 실패: {Path.GetFileName(path)}");
                ShowWarning(GetLocalizedText("photo_selection_failed"));
                processingComplete = true;
            }
        }));

        yield return new WaitUntil(() => processingComplete);
        onComplete?.Invoke();
    }

    private void ProcessCropAndDisplay(Texture2D sourceTexture, System.Action onComplete)
    {
        try
        {
            ImageCropper.Instance.Show(sourceTexture, (success, original, cropped) =>
            {
                try
                {
                    if (success && cropped is Texture2D croppedTexture)
                    {
                        if (mainPhoto != null) Destroy(mainPhoto);
                        mainPhoto = croppedTexture;
                        
                        if (mainPhotoDisplay != null) 
                        {
                            mainPhotoDisplay.sprite = GetOrCreateSprite(mainPhoto);
                            mainPhotoDisplay.gameObject.SetActive(true);
                        }
                        Debug.Log("[CubeDataFixManager] 메인사진 크롭 및 표시 완료");
                    }
                    else
                    {
                        ShowWarning(GetLocalizedText("main_photo_crop_failed"));
                    }
                }
                finally
                {
                    onComplete?.Invoke();
                }
            }, new ImageCropper.Settings
            {
                autoZoomEnabled = true,
                selectionMinAspectRatio = 1.0f,
                selectionMaxAspectRatio = 1.0f
            });
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CubeDataFixManager] 크롭 처리 오류: {e.Message}");
            ShowWarning(GetLocalizedText("main_photo_crop_failed"));
            onComplete?.Invoke();
        }
    }

    private IEnumerator SelectSubPhotos()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        ShowSpinner(GetLocalizedText("loading_sub_photos"));
        bool isLoading = true;

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
                    StartCoroutine(LoadSubPhotosWithFallback(paths, () => {
                        isLoading = false;
                    }));
                }
            }
            else
            {
                ShowWarning(GetLocalizedText("photo_selection_failed"));
                isLoading = false;
            }
        }, "image/*");

        yield return new WaitUntil(() => !isLoading);
        HideSpinner();
        UpdateSubPhotoGrid();
        isProcessing = false;
    }

    private IEnumerator LoadSubPhotosWithFallback(string[] paths, System.Action onComplete)
    {
        int processedCount = 0;
        int totalPaths = paths.Length;

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
                Debug.Log($"[HEIC] ✅ 서브사진 1단계 성공: {Path.GetFileName(path)} - {texture.width}x{texture.height}");
                subPhotos.Add(texture);
                imageProcessed = true;
            }
            else
            {
                Debug.Log($"[HEIC] ⚠️ 서브사진 1단계 실패, 2단계 시도: {Path.GetFileName(path)}");
                if (texture != null) Destroy(texture);
                
                // 2단계: 수동 HEIC 변환
                yield return StartCoroutine(LoadImageWithConversion(path, (convertedTexture) => {
                    if (convertedTexture != null)
                    {
                        Debug.Log($"[HEIC] ✅ 서브사진 2단계 성공: {Path.GetFileName(path)} - {convertedTexture.width}x{convertedTexture.height}");
                        subPhotos.Add(convertedTexture);
                    }
                    else
                    {
                        Debug.LogError($"[HEIC] ❌ 서브사진 로드 실패: {Path.GetFileName(path)}");
                        ShowWarning(GetLocalizedText("photo_selection_failed"));
                    }
                    imageProcessed = true;
                }));
            }

            yield return new WaitUntil(() => imageProcessed);
            processedCount++;
            yield return null;
        }

        Debug.Log($"[HEIC] 서브사진 처리 완료: 총 {subPhotos.Count}장 로드됨");
        onComplete?.Invoke();
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
        Debug.Log($"📸 [CubeDataFixManager] UpdateSubPhotoGrid 호출 - subPhotos: {subPhotos.Count}개, subPhotoDisplays: {subPhotoDisplays.Count}개");
        
        for (int i = 0; i < subPhotoDisplays.Count; i++)
        {
            if (i < subPhotos.Count)
            {
                subPhotoDisplays[i].sprite = GetOrCreateSprite(subPhotos[i]);
                subPhotoDisplays[i].gameObject.SetActive(true);
                Debug.Log($"✅ [CubeDataFixManager] 서브 사진 #{i+1} 미리보기 활성화");
            }
            else
            {
                subPhotoDisplays[i].sprite = null;
                subPhotoDisplays[i].gameObject.SetActive(false);
            }
        }
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
        Debug.Log($"[CubeDataFixManager] 스피너 표시: {message}");
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
        
        Debug.Log("[CubeDataFixManager] 스피너 숨김 완료");
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
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
    #endregion

    #region Validation and Upload
    private IEnumerator ValidateAndSubmit()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        if (doubleTap == null)
        {
            ShowWarning(GetLocalizedText("no_object_selected"));
            Debug.LogError("[CubeDataFixManager] ValidateAndSubmit: DoubleTap3D가 null입니다!");
            isProcessing = false;
            yield break;
        }

        int id = doubleTap.GetId();
        Debug.Log($"[CubeDataFixManager] ValidateAndSubmit - ID: {id}");
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
        Debug.Log($"[CubeDataFixManager] Description 입력값: '{description}'");
        Debug.Log($"[CubeDataFixManager] Name 입력값: '{name}'");

        if (string.IsNullOrEmpty(description))
        {
            ShowWarning(GetLocalizedText("enter_description"));
            Debug.LogWarning("[CubeDataFixManager] 설명이 비어 있습니다!");
            isProcessing = false;
            yield break;
        }

        if (string.IsNullOrEmpty(name))
        {
            ShowWarning(GetLocalizedText("enter_place_name"));
            Debug.LogWarning("[CubeDataFixManager] 이름이 비어 있습니다!");
            isProcessing = false;
            yield break;
        }

        string instagramID = showInstagram ? (instagramIDInput?.text?.Trim() ?? "") : "";
        bool petFriendly = petFriendlyToggle?.isOn ?? false;
        bool separateRestroom = separateRestroomToggle?.isOn ?? false;

        if (mainPhoto == null)
        {
            ShowWarning(GetLocalizedText("upload_main_photo"));
            Debug.LogWarning("[CubeDataFixManager] 메인 사진이 없습니다!");
            isProcessing = false;
            yield break;
        }

        Coroutine countdownCoroutine = StartCoroutine(ShowCountdownWarning(countdownSeconds));
        yield return StartCoroutine(SendWithTimeout(
            ProcessAndUploadData(
                this, id, petFriendly, separateRestroom, instagramID, showInstagram, description, name,
                countdownCoroutine)));

        if (!isProcessing)
        {
            Debug.Log("✅ 수정 요청 성공 - UI 초기화");
            SetUIActive(fixUIPanel, false);
            ResetToInitialState();
            yield return new WaitForSeconds(2f);
        }
        else
        {
            Debug.Log("⚠️ 수정 요청 실패 - UI 초기화하지 않음");
        }
        isProcessing = false;
    }

    private IEnumerator ShowCountdownWarning(int seconds)
    {
        for (int i = seconds; i >= 1; i--)
        {
            ShowWarning(GetLocalizedText("submitting_countdown").Replace("{0}", i.ToString()));
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator SendWithTimeout(IEnumerator routine)
    {
        elapsedTime = 0f;
        bool isCompleted = false;

        Coroutine co = StartCoroutine(routine);
        yield return StartCoroutine(WaitForRoutine(co, uploadTimeoutSeconds, () => isCompleted));

        if (!isProcessing)
        {
            isCompleted = true;
            Debug.Log("✅ SendWithTimeout: 수정 요청 성공으로 완료 플래그 설정");
        }
        else
        {
            Debug.Log("⚠️ SendWithTimeout: 수정 요청 실패로 완료 플래그 미설정");
        }
    }

    private IEnumerator WaitForRoutine(Coroutine routine, float timeout, Func<bool> isCompleted)
    {
        while (routine != null && elapsedTime < timeout && !isCompleted() && isProcessing)
        {
            elapsedTime += Time.deltaTime;
            Debug.Log($"⏳ WaitForRoutine: 대기 중... Elapsed: {elapsedTime:F2}초 / Timeout: {timeout}초");
            yield return null;
        }

        if (routine != null && !isCompleted() && elapsedTime >= timeout)
        {
            Debug.LogWarning($"⏰ 타임아웃 발생: Elapsed {elapsedTime:F2}초, Timeout: {timeout}초");
            StopCoroutine(routine);
            isProcessing = true;
            ShowWarning(GetLocalizedText("request_timeout"));
        }
        else if (isCompleted())
        {
            Debug.Log("✅ WaitForRoutine: 수정 요청 성공으로 루프 종료");
        }
        else
        {
            Debug.Log("⚠️ WaitForRoutine: 루프 종료 (기타 이유)");
        }
    }

    private IEnumerator ProcessAndUploadData(
        CubeDataFixManager form, int id, bool petFriendly, bool separateRestroom,
        string instagramID, bool showInstagram, string description, string name, Coroutine countdownCoroutine)
    {
        WWWForm formData = new WWWForm();

        formData.AddField("target_id", id.ToString());
        formData.AddField("pet_friendly", petFriendly ? "true" : "false");
        formData.AddField("separate_restroom", separateRestroom ? "true" : "false");
        formData.AddField("instagram_id", showInstagram ? instagramID : "");
        formData.AddField("description", description);
        formData.AddField("name", name);
        
        formData.AddField("timezone", GetTimezone());
        formData.AddField("timezone_offset", GetTimezoneOffset());
        
        string folder = $"fix_{DateTime.Now:yyyyMMdd_HHmmss}";
        Debug.Log($"📁 Folder value before sending: {folder}");
        formData.AddField("folder", folder);
        formData.AddField("username", "");

        Texture2D mainPhoto = form.GetMainPhoto();
        if (mainPhoto != null)
        {
            Debug.Log($"📸 메인 사진 데이터 확인: 크기 {mainPhoto.width}x{mainPhoto.height}, 포맷 {mainPhoto.format}");
            Texture2D resizedMainPhoto = ResizeTextureWithRenderTexture(mainPhoto, 444, 444);
            byte[] mainPhotoBytes = resizedMainPhoto.EncodeToJPG(50);
            Debug.Log($"📸 메인 사진 바이트 크기: {mainPhotoBytes.Length} bytes");
            if (mainPhotoBytes.Length == 0)
            {
                Debug.LogError("❌ 메인 사진 데이터가 비어 있습니다!");
                ShowWarning(GetLocalizedText("main_photo_upload_failed"));
                yield break;
            }
            string mainPath = "main.jpg";
            formData.AddBinaryData("main_photo", mainPhotoBytes, mainPath, "image/jpeg");
            Destroy(resizedMainPhoto);
        }
        else
        {
            Debug.LogWarning("❌ 메인 사진이 없습니다!");
            ShowWarning(GetLocalizedText("upload_main_photo"));
            yield break;
        }

        List<Texture2D> subPhotos = form.GetSubPhotos();
        Debug.Log($"📂 서브 사진 개수: {subPhotos.Count}");
        for (int i = 1; i <= subPhotos.Count; i++)
        {
            if (i > MAX_SUB_PHOTOS) break;
            Texture2D subPhoto = subPhotos[i - 1];
            Debug.Log($"📸 서브 사진 #{i} 데이터 확인: 크기 {subPhoto.width}x{subPhoto.height}, 포맷 {subPhoto.format}");
            Texture2D resizedSubPhoto = ResizeTextureKeepAspectWithRenderTexture(subPhoto, 800, 800);
            byte[] subPhotoBytes = resizedSubPhoto.EncodeToJPG(50);
            Debug.Log($"📸 서브 사진 #{i} 바이트 크기: {subPhotoBytes.Length} bytes");
            if (subPhotoBytes.Length == 0)
            {
                Debug.LogError($"❌ 서브 사진 #{i} 데이터가 비어 있습니다!");
                ShowWarning(GetLocalizedText("sub_photo_upload_failed"));
                Destroy(resizedSubPhoto);
                continue;
            }
            string subPath = $"sub_{i}.jpg";
            formData.AddBinaryData("sub_photos", subPhotoBytes, subPath, "image/jpeg");
            Destroy(resizedSubPhoto);
        }

        Debug.Log($"🌐 서버로 요청 전송: {serverUrl}");
        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, formData))
        {
            www.timeout = Mathf.RoundToInt(uploadTimeoutSeconds);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;
                Debug.Log($"✅ 업로드 응답: {responseText} (응답 코드: {www.responseCode})");

                if (responseText.Contains("Fix Upload Succeeded!") || www.responseCode == 200)
                {
                    isProcessing = false;
                    StopCoroutine(countdownCoroutine);
                    ShowWarning(GetLocalizedText("fix_success"));
                    Debug.Log("✅ 수정 요청 성공: 루틴 종료");

                    SetUIActive(fixUIPanel, false);
                    ResetToInitialState();
                    yield break;
                }
                else
                {
                    Debug.LogWarning($"⚠️ 서버 응답이 성공으로 간주되지 않음: {responseText}");
                    isProcessing = true;
                    ShowWarning(GetLocalizedText("server_error"));
                }
            }
            else
            {
                Debug.LogError($"❌ 수정 요청 실패: {www.error} (응답 코드: {www.responseCode})");
                isProcessing = true;
                ShowWarning(GetLocalizedText("server_error"));
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
                    case SystemLanguage.Korean: return "제출 중... {0}초 남음";
                    case SystemLanguage.Japanese: return "送信中... {0}秒残り";
                    case SystemLanguage.Chinese: 
                    case SystemLanguage.ChineseSimplified: return "提交中... 还剩{0}秒";
                    case SystemLanguage.Spanish: return "Enviando... {0} segundos restantes";
                    default: return "Submitting... {0} seconds remaining";
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

            default:
                Debug.LogWarning($"[CubeDataFixManager] 알 수 없는 다국어 키: {key}");
                return key; // 키를 그대로 반환
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

        if (mainPhotoDisplay != null) 
        {
            mainPhotoDisplay.sprite = null;
            mainPhotoDisplay.gameObject.SetActive(false);
        }
        if (subPhotoGrid != null)
        {
            foreach (var display in subPhotoDisplays)
            {
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
                instagramIDInput.interactable = false;
                instagramIDInput.image.color = Color.gray;
            }
        }

        if (disableObject != null)
        {
            disableObject.SetActive(false);
            Debug.Log($"[CubeDataFixManager] DisableObject 비활성화됨: {disableObject.name}");
        }

        isProcessing = false;
        elapsedTime = 0f;

        Debug.Log("✅ UI 및 상태 초기화 완료");
    }
    #endregion

    #region Debug Methods
    [ContextMenu("Test Fix UI")]
    void TestFixUI()
    {
        if (fixUIPanel != null)
        {
            fixUIPanel.SetActive(!fixUIPanel.activeSelf);
            Debug.Log($"Fix UI Panel 상태: {fixUIPanel.activeSelf}");
        }
    }

    [ContextMenu("Reset All States")]
    void ResetAllStates()
    {
        ResetToInitialState();
        Debug.Log("모든 상태 리셋 완료");
    }

    [ContextMenu("Show Current Status")]
    void ShowCurrentStatus()
    {
        Debug.Log($"=== CubeDataFixManager Status ===");
        Debug.Log($"DoubleTap ID: {(doubleTap != null ? doubleTap.GetId().ToString() : "null")}");
        Debug.Log($"Is Processing: {isProcessing}");
        Debug.Log($"Has Main Photo: {mainPhoto != null}");
        Debug.Log($"Sub Photos Count: {subPhotos.Count}");
        Debug.Log($"Show Instagram: {showInstagram}");
        Debug.Log($"Fix UI Panel Active: {(fixUIPanel != null ? fixUIPanel.activeSelf.ToString() : "null")}");
        Debug.Log($"Sub Photo Displays Count: {subPhotoDisplays.Count}");
        Debug.Log($"Upload Timeout: {uploadTimeoutSeconds}초");
        Debug.Log($"Countdown Seconds: {countdownSeconds}초");
        Debug.Log($"================================");
    }
    #endregion
}