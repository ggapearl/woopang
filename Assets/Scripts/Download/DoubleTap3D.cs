using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Networking;

public class DoubleTap3D : MonoBehaviour
{
    public CanvasGroup fullscreenCanvasGroup;
    public GameObject guidePanel;
    public Image fullscreenImage;
    public List<Sprite> imageSprites = new List<Sprite>();
    public Image infoImage1;
    public Image infoImage2;
    public Button instagramButton;
    public Button previousButton;
    public Button nextButton;
    public Button closeButton;
    public Text nameText;
    public Text descriptionTextUI;
    public GameObject placeInfoTextPanel;
    private Text placeInfoText;
    public float tapSpeed = 0.5f;
    public float swipeThreshold = 50f;
    public float fadeDuration = 0.5f;

    [Header("Photo Layout Settings")]
    [Tooltip("사진 너비 (픽셀)")]
    [SerializeField] private float photoWidth = 1080f;
    [Tooltip("사진 높이 (픽셀) - 0이면 화면 높이에 맞춤")]
    [SerializeField] private float photoHeight = 0f;
    [Tooltip("사진 좌우 여백 (픽셀)")]
    [SerializeField] private float photoMarginHorizontal = 0f;
    [Tooltip("사진 상하 여백 (픽셀)")]
    [SerializeField] private float photoMarginVertical = 0f;

    private float lastTapTime = 0f;
    private bool isFullscreen = false;
    private int currentIndex = 0;
    private int imageIndex = -1;
    private bool isPlaceInfoPage = true;
    private bool isFading = false;
    private Vector2 touchStartPos;
    private bool isSwiping;

    // SwipePanelController-style photo navigation
    private RectTransform photoContainer;
    private List<Image> photoImages = new List<Image>();
    private Vector2 containerBasePos;
    private Vector2 containerTargetPos;
    private bool isDragging = false;
    private Vector2 dragStartPos;
    private float slideSpeed = 12f;

    [Tooltip("사진 간 간격 (픽셀)")]
    [SerializeField] private float photoSpacing = 40f;

    // 댓글 시스템
    private GameObject commentPanel;
    private bool isCommentVisible = false;

    private Sprite infoSprite1;
    private Sprite infoSprite2;
    private bool petFriendly;
    private bool separateRestroom;
    private string descriptionText;
    private string name;
    private string instagramId;
    private int id = -1;
    private string username;
    private string tel;
    private string address;
    private string overview;
    private string petInfo;

    // 이미지 URL 저장
    private List<string> imageUrls = new List<string>();
    private ImageDisplayController imageDisplayController;

    // iOS 캐싱 시스템
    private Dictionary<int, byte[]> cachedImageData = new Dictionary<int, byte[]>();
    private bool imagesAreCached = false;

    public static event Action<DoubleTap3D> OnDoubleTapEvent;

#if UNITY_IOS
    private static bool savedFullscreenState = false;
    private static int savedObjectId = -1;
    private static int savedImageIndex = -1;
    private static bool savedIsPlaceInfoPage = true;
#endif

    void Start()
    {
        if (fullscreenCanvasGroup == null || fullscreenImage == null || guidePanel == null ||
            infoImage1 == null || infoImage2 == null || instagramButton == null ||
            previousButton == null || nextButton == null || closeButton == null || nameText == null)
        {
            Debug.LogError("[DoubleTap3D] 필수 UI 요소가 할당되지 않았습니다!");
            enabled = false;
            return;
        }

        // 풀스크린 이미지에 둥근 모서리 적용
        RoundedImage roundedImage = fullscreenImage.GetComponent<RoundedImage>();
        if (roundedImage == null)
        {
            roundedImage = fullscreenImage.gameObject.AddComponent<RoundedImage>();
            roundedImage.cornerRadius = 30f; // 모서리 둥글기 조정 (0~100)
        }

        imageDisplayController = GetComponentInParent<ImageDisplayController>();
        if (imageDisplayController == null)
        {
            imageDisplayController = GetComponentInChildren<ImageDisplayController>();
        }

        if (descriptionTextUI == null)
        {
            Debug.LogWarning("[DoubleTap3D] descriptionTextUI가 할당되지 않았습니다!");
        }

        if (placeInfoTextPanel != null)
        {
            placeInfoText = placeInfoTextPanel.GetComponentInChildren<Text>();
            if (placeInfoText == null)
            {
                Debug.LogWarning("[DoubleTap3D] placeInfoTextPanel 하위에 Text 컴포넌트가 없음!");
            }
            placeInfoTextPanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[DoubleTap3D] placeInfoTextPanel이 할당되지 않았습니다!");
        }

        fullscreenCanvasGroup.gameObject.SetActive(false);
        guidePanel.SetActive(false);
        fullscreenCanvasGroup.alpha = 0f;

        if (descriptionTextUI != null)
        {
            descriptionTextUI.gameObject.SetActive(false);
        }

        fullscreenImage.preserveAspect = true;
        fullscreenImage.type = Image.Type.Simple;

        // Initialize photo container for swipe navigation
        InitializePhotoContainer();

        instagramButton.onClick.AddListener(OnInstagramButtonClick);
        nextButton.onClick.AddListener(ShowNextImage);
        previousButton.onClick.AddListener(ShowPreviousImage);
        closeButton.onClick.AddListener(CloseFullscreen);

        if (GetComponent<Collider>() == null)
        {
            Debug.LogError("[DoubleTap3D] Collider가 없습니다!");
        }

#if UNITY_IOS
        if (fullscreenCanvasGroup != null)
        {
            Canvas canvas = fullscreenCanvasGroup.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }
        }
#endif

        StartCoroutine(IgnoreInitialTouch(2f));

#if UNITY_IOS
        if (savedFullscreenState && savedObjectId == this.id && this.id != -1)
        {
            StartCoroutine(RestoreFullscreenForiOS());
        }
#endif
    }

    IEnumerator IgnoreInitialTouch(float duration)
    {
        yield return new WaitForSeconds(duration);
    }

    // 이미지를 byte[]로 캐싱
    private void CacheImagesForFullscreen()
    {
        if (imagesAreCached) return;

        Debug.Log("[DoubleTap3D] 이미지 캐싱 시작");
        cachedImageData.Clear();

        for (int i = 0; i < imageSprites.Count; i++)
        {
            if (imageSprites[i] != null && imageSprites[i].texture != null)
            {
                try
                {
                    // 텍스처를 PNG 바이트 배열로 변환
                    byte[] imageData = imageSprites[i].texture.EncodeToPNG();
                    cachedImageData[i] = imageData;
                    Debug.Log($"[DoubleTap3D] 이미지 {i} 캐싱 완료 - 크기: {imageData.Length} bytes");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DoubleTap3D] 이미지 {i} 캐싱 실패: {e.Message}");
                }
            }
        }

        imagesAreCached = true;
        Debug.Log($"[DoubleTap3D] 총 {cachedImageData.Count}개 이미지 캐싱 완료");
    }

    // 캐시에서 이미지 복원
    private void RestoreImagesFromCache()
    {
        if (cachedImageData.Count == 0)
        {
            Debug.LogWarning("[DoubleTap3D] 캐시된 이미지가 없음");
            return;
        }

        Debug.Log("[DoubleTap3D] 캐시에서 이미지 복원 시작");
        List<Sprite> restoredSprites = new List<Sprite>();

        foreach (var kvp in cachedImageData)
        {
            try
            {
                // byte[]에서 텍스처 재생성
                Texture2D restoredTexture = new Texture2D(2, 2);
                restoredTexture.LoadImage(kvp.Value);
                
                // 스프라이트 재생성
                Sprite restoredSprite = Sprite.Create(
                    restoredTexture, 
                    new Rect(0, 0, restoredTexture.width, restoredTexture.height), 
                    new Vector2(0.5f, 0.5f)
                );
                
                restoredSprites.Add(restoredSprite);
                Debug.Log($"[DoubleTap3D] 이미지 {kvp.Key} 복원 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DoubleTap3D] 이미지 {kvp.Key} 복원 실패: {e.Message}");
            }
        }

        // 복원된 스프라이트로 교체
        imageSprites = restoredSprites;
        Debug.Log($"[DoubleTap3D] 총 {restoredSprites.Count}개 이미지 복원 완료");
    }

    // 캐시 메모리 해제
    private void ClearImageCache()
    {
        cachedImageData.Clear();
        imagesAreCached = false;
        Debug.Log("[DoubleTap3D] 이미지 캐시 메모리 해제");
    }

    /// <summary>
    /// SwipePanelController 스타일의 사진 컨테이너 초기화
    /// </summary>
    private void InitializePhotoContainer()
    {
        // 기존 fullscreenImage의 부모를 컨테이너로 사용
        if (fullscreenImage != null && fullscreenImage.transform.parent != null)
        {
            Transform parent = fullscreenImage.transform.parent;

            // 컨테이너 GameObject 생성
            GameObject containerObj = new GameObject("PhotoContainer");
            containerObj.transform.SetParent(parent, false);

            photoContainer = containerObj.AddComponent<RectTransform>();
            photoContainer.anchorMin = new Vector2(0, 0);
            photoContainer.anchorMax = new Vector2(1, 1);
            photoContainer.sizeDelta = Vector2.zero;
            photoContainer.anchoredPosition = Vector2.zero;

            // 기존 fullscreenImage를 컨테이너 아래로 이동
            fullscreenImage.transform.SetParent(photoContainer, false);

            containerBasePos = Vector2.zero;
            containerTargetPos = Vector2.zero;

            Debug.Log("[DoubleTap3D] 사진 컨테이너 초기화 완료");
        }
        else
        {
            Debug.LogError("[DoubleTap3D] fullscreenImage 또는 부모가 없어 컨테이너 초기화 실패");
        }
    }

    /// <summary>
    /// 사진들을 수평으로 배치 (SwipePanelController 패턴)
    /// </summary>
    private void ArrangePhotos()
    {
        if (photoContainer == null || fullscreenImage == null) return;

        // 기존 추가 이미지들 제거
        foreach (var img in photoImages)
        {
            if (img != null && img != fullscreenImage)
            {
                Destroy(img.gameObject);
            }
        }
        photoImages.Clear();

        // 사진 크기 계산
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        float actualPhotoWidth = (photoWidth > 0) ? photoWidth : screenWidth;
        float actualPhotoHeight = (photoHeight > 0) ? photoHeight : screenHeight;
        float slotWidth = actualPhotoWidth + photoSpacing; // 사진 너비 + 간격
        int currentSlot = 0;

        // placeInfoTextPanel이 있으면 첫 번째 슬롯에 배치
        if (placeInfoTextPanel != null)
        {
            RectTransform panelRect = placeInfoTextPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                placeInfoTextPanel.transform.SetParent(photoContainer, false);
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);  // 중앙 앵커
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);  // 중앙 앵커
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(actualPhotoWidth - photoMarginHorizontal * 2, actualPhotoHeight - photoMarginVertical * 2);
                panelRect.anchoredPosition = new Vector2(slotWidth * currentSlot, 0);  // 중앙 기준 위치
                placeInfoTextPanel.SetActive(true);
                currentSlot++;
            }
        }

        // fullscreenImage를 다음 슬롯으로 사용
        RectTransform imageRect = fullscreenImage.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);  // 중앙 앵커
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);  // 중앙 앵커
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(actualPhotoWidth - photoMarginHorizontal * 2, actualPhotoHeight - photoMarginVertical * 2);
        imageRect.anchoredPosition = new Vector2(slotWidth * currentSlot, 0);  // 중앙 기준 위치
        photoImages.Add(fullscreenImage);

        currentSlot++;

        // 추가 사진들을 위한 Image 생성
        for (int i = 1; i < imageSprites.Count; i++)
        {
            GameObject photoObj = new GameObject($"Photo_{i}");
            photoObj.transform.SetParent(photoContainer, false);

            Image img = photoObj.AddComponent<Image>();
            img.preserveAspect = true;
            img.type = Image.Type.Simple;

            // RoundedImage 적용
            RoundedImage rounded = photoObj.AddComponent<RoundedImage>();
            rounded.cornerRadius = 30f;

            // T5EdgeLineEffect 적용 (얇은 외곽선)
            T5EdgeLineEffect effect = photoObj.AddComponent<T5EdgeLineEffect>();
            effect.SetSettings(
                new Color(1f, 0.95f, 0.8f, 1f),
                0.008f,
                2.0f,
                2.0f,
                1.0f,
                0.8f,
                0.05f
            );

            RectTransform rect = photoObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);  // 중앙 앵커
            rect.anchorMax = new Vector2(0.5f, 0.5f);  // 중앙 앵커
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(actualPhotoWidth - photoMarginHorizontal * 2, actualPhotoHeight - photoMarginVertical * 2);
            rect.anchoredPosition = new Vector2(slotWidth * currentSlot, 0);  // 중앙 기준 위치

            photoImages.Add(img);
            currentSlot++;
        }

        UpdatePhotoSprites();
        Debug.Log($"[DoubleTap3D] {currentSlot}개 슬롯 배치 완료 (간격={photoSpacing}px, 사진크기={actualPhotoWidth}x{actualPhotoHeight}, 여백={photoMarginHorizontal}x{photoMarginVertical}, placeInfoPanel={placeInfoTextPanel != null}, photos={imageSprites.Count})");
    }

    /// <summary>
    /// 현재 인덱스에 맞춰 사진 스프라이트 업데이트
    /// </summary>
    private void UpdatePhotoSprites()
    {
        if (photoImages.Count == 0 || imageSprites.Count == 0) return;

        // photoImages는 실제 사진 Image들만 포함 (placeInfoPanel 제외)
        for (int i = 0; i < photoImages.Count && i < imageSprites.Count; i++)
        {
            Image img = photoImages[i];
            if (img == null) continue;

            img.gameObject.SetActive(true);
            if (imageSprites[i] != null)
            {
                img.sprite = imageSprites[i];
                img.color = Color.white;
            }
            else
            {
                Debug.LogWarning($"[DoubleTap3D] Sprite at index {i} is null!");
                img.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
        }
    }

#if UNITY_IOS
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && isFullscreen && this.id != -1)
        {
            savedFullscreenState = true;
            savedObjectId = this.id;
            savedImageIndex = imageIndex;
            savedIsPlaceInfoPage = isPlaceInfoPage;
            Debug.Log($"[iOS] 풀스크린 상태 저장: ID={id}, ImageIndex={imageIndex}, IsPlaceInfoPage={isPlaceInfoPage}");
        }
        else if (!pauseStatus && savedFullscreenState && savedObjectId == this.id)
        {
            StartCoroutine(RestoreFullscreenForiOS());
        }
    }

    private IEnumerator RestoreFullscreenForiOS()
    {
        Debug.Log($"[iOS] 풀스크린 복원 시작: ID={id}");
        
        yield return new WaitForSeconds(0.5f);
        
        // 캐시에서 이미지 복원
        if (imagesAreCached && cachedImageData.Count > 0)
        {
            RestoreImagesFromCache();
        }
        
        // 풀스크린 UI 복원
        isFullscreen = true;
        imageIndex = savedImageIndex;
        isPlaceInfoPage = savedIsPlaceInfoPage;
        currentIndex = imageIndex >= 0 ? imageIndex : 0;
        
        fullscreenCanvasGroup.gameObject.SetActive(true);
        guidePanel.SetActive(true);
        fullscreenCanvasGroup.alpha = 1f;
        
        // 이미지 표시 (복원된 스프라이트 사용)
        ShowImage(currentIndex);
        UpdateInfoImages();
        
        // 버튼 리스너 재설정
        instagramButton.onClick.RemoveAllListeners();
        instagramButton.onClick.AddListener(OnInstagramButtonClick);
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(ShowNextImage);
        previousButton.onClick.RemoveAllListeners();
        previousButton.onClick.AddListener(ShowPreviousImage);
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(CloseFullscreen);
        
        savedFullscreenState = false;
        savedObjectId = -1;
        savedImageIndex = -1;
        savedIsPlaceInfoPage = true;
        
        Debug.Log($"[iOS] 풀스크린 복원 완료: ID={id}, 이미지 개수={imageSprites.Count}");
    }
#endif

    void Update()
    {
        // SwipePanelController 스타일 Lerp 보간 (드래그 중이 아닐 때)
        if (!isDragging && photoContainer != null && isFullscreen)
        {
            photoContainer.anchoredPosition = Vector2.Lerp(
                photoContainer.anchoredPosition,
                containerTargetPos,
                Time.deltaTime * slideSpeed
            );
        }

        if (Input.touchCount == 1 && Time.timeSinceLevelLoad > 2f)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.position;
                dragStartPos = touch.position;
                isSwiping = true;

                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    Debug.Log($"[DoubleTap3D] 레이캐스트 히트 - 오브젝트: {hit.collider.gameObject.name}, 이 오브젝트: {gameObject.name}");
                    if (hit.collider.gameObject == gameObject)
                    {
                        float timeSinceLastTap = Time.time - lastTapTime;
                        if (timeSinceLastTap < tapSpeed && timeSinceLastTap > 0.1f)
                        {
                            OnDoubleTapCube();
                        }
                        lastTapTime = Time.time;
                    }
                }
                else
                {
                    Debug.Log($"[DoubleTap3D] 레이캐스트 히트 실패 - 터치 위치: {touch.position}");
                }
            }
            else if (touch.phase == TouchPhase.Moved && isSwiping)
            {
                if (isFullscreen && photoContainer != null)
                {
                    Vector2 currentPos = touch.position;
                    Vector2 swipeDelta = currentPos - dragStartPos;

                    // 가로/세로 중 더 큰 방향으로 제스처 결정
                    if (Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
                    {
                        // 가로 스와이프: 실시간 드래그
                        isDragging = true;
                        float deltaX = swipeDelta.x;
                        photoContainer.anchoredPosition = containerTargetPos + new Vector2(deltaX, 0);
                    }
                    // 세로 스와이프는 TouchPhase.Ended에서 처리
                }
                else
                {
                    // 풀스크린이 아닐 때는 기존 로직
                    Vector2 swipeDelta = touch.position - touchStartPos;

                    if (Mathf.Abs(swipeDelta.y) > swipeThreshold && swipeDelta.y < 0)
                    {
                        CloseFullscreen();
                        isSwiping = false;
                    }
                }
            }
            else if (touch.phase == TouchPhase.Ended && isSwiping)
            {
                if (isFullscreen && photoContainer != null)
                {
                    Vector2 endPos = touch.position;
                    Vector2 swipeDelta = endPos - dragStartPos;
                    float swipeDistanceX = swipeDelta.x;
                    float swipeDistanceY = swipeDelta.y;

                    // 가로/세로 중 더 큰 방향으로 제스처 처리
                    if (Mathf.Abs(swipeDistanceX) > Mathf.Abs(swipeDistanceY))
                    {
                        // 가로 스와이프: 사진 전환
                        if (isDragging)
                        {
                            if (Mathf.Abs(swipeDistanceX) > swipeThreshold)
                            {
                                if (swipeDistanceX > 0)
                                    ShowPreviousImage();
                                else
                                    ShowNextImage();
                            }
                            else
                            {
                                // 스와이프 거리가 충분하지 않으면 원래 위치로 복원
                                photoContainer.anchoredPosition = containerTargetPos;
                            }
                            isDragging = false;
                        }
                    }
                    else
                    {
                        // 세로 스와이프: 페이드 닫기 또는 댓글
                        if (Mathf.Abs(swipeDistanceY) > swipeThreshold)
                        {
                            if (swipeDistanceY < 0)
                            {
                                // 아래로 스와이프: 풀스크린 닫기
                                CloseFullscreen();
                            }
                            else
                            {
                                // 위로 스와이프: 댓글 열기
                                ShowComments();
                            }
                        }
                    }
                }
                isSwiping = false;
            }
        }
    }

    private void OnDoubleTapCube()
    {
        Debug.Log($"[DoubleTap3D] 더블 터치 발생 - ID: {id}, GameObject: {gameObject.name}");

        OnDoubleTapEvent?.Invoke(this);

        isFullscreen = !isFullscreen;

        if (isFullscreen)
        {
            // 풀스크린 열 때 이미지 캐싱 (iOS 대비)
#if UNITY_IOS
            CacheImagesForFullscreen();
#endif

            currentIndex = 0;
            isPlaceInfoPage = placeInfoTextPanel != null;
            imageIndex = placeInfoTextPanel != null ? -1 : 0;

            // SwipePanelController 스타일: 사진 배치
            ArrangePhotos();

            ShowImage(currentIndex);
            UpdateInfoImages();
            fullscreenCanvasGroup.gameObject.SetActive(true);
            guidePanel.SetActive(true);

            instagramButton.onClick.RemoveAllListeners();
            instagramButton.onClick.AddListener(OnInstagramButtonClick);

            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(ShowNextImage);
            previousButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(ShowPreviousImage);

            // 컨테이너 위치 초기화
            if (photoContainer != null)
            {
                containerTargetPos = Vector2.zero;
                photoContainer.anchoredPosition = Vector2.zero;
            }

            StartCoroutine(FadeInCanvas(fadeDuration));
        }
        else
        {
            CloseFullscreen();
        }
    }

    public void ShowNextImage()
    {
        if (imageSprites.Count == 0) return;

        if (placeInfoTextPanel != null && isPlaceInfoPage)
        {
            isPlaceInfoPage = false;
            imageIndex = 0;
        }
        else if (imageIndex < imageSprites.Count - 1)
        {
            imageIndex++;
        }
        else
        {
            imageIndex = 0;
        }

        currentIndex++;

        // SwipePanelController 스타일: 컨테이너 목표 위치 업데이트
        UpdateContainerTargetPosition();
    }

    public void ShowPreviousImage()
    {
        if (imageSprites.Count == 0) return;

        if (placeInfoTextPanel != null && !isPlaceInfoPage && imageIndex == 0)
        {
            isPlaceInfoPage = true;
            imageIndex = -1;
        }
        else if (imageIndex > 0)
        {
            imageIndex--;
        }
        else
        {
            imageIndex = imageSprites.Count - 1;
        }

        currentIndex--;
        if (currentIndex < 0) currentIndex = 0;

        // SwipePanelController 스타일: 컨테이너 목표 위치 업데이트
        UpdateContainerTargetPosition();
    }

    /// <summary>
    /// 현재 인덱스에 맞춰 컨테이너 목표 위치 업데이트
    /// </summary>
    private void UpdateContainerTargetPosition()
    {
        if (photoContainer == null) return;

        // placeInfoTextPanel이 있으면 첫 번째가 info, 사진은 그 다음부터
        int totalIndex = isPlaceInfoPage ? 0 : (placeInfoTextPanel != null ? imageIndex + 1 : imageIndex);

        // photoWidth 값 사용 (0이면 화면 너비)
        float actualPhotoWidth = (photoWidth > 0) ? photoWidth : Screen.width;
        float slotWidth = actualPhotoWidth + photoSpacing;
        containerTargetPos = new Vector2(-slotWidth * totalIndex, 0);

        Debug.Log($"[DoubleTap3D] 컨테이너 목표 위치 업데이트: {containerTargetPos}, imageIndex={imageIndex}, isPlaceInfoPage={isPlaceInfoPage}, slotWidth={slotWidth}");
    }

    private void ShowImage(int index)
    {
        if (placeInfoTextPanel != null && isPlaceInfoPage)
        {
            fullscreenImage.gameObject.SetActive(false);
            if (placeInfoTextPanel != null)
            {
                placeInfoTextPanel.SetActive(isFullscreen);
            }
        }
        else if (imageSprites.Count > 0 && imageIndex >= 0 && imageIndex < imageSprites.Count)
        {
            fullscreenImage.gameObject.SetActive(true);
            if (imageSprites[imageIndex] != null)
            {
                fullscreenImage.sprite = imageSprites[imageIndex];
                fullscreenImage.color = Color.white;
            }
            else
            {
                Debug.LogWarning($"[DoubleTap3D] Sprite at index {imageIndex} is null!");
                fullscreenImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
            if (placeInfoTextPanel != null)
            {
                placeInfoTextPanel.SetActive(false);
            }
        }
        else
        {
            fullscreenImage.gameObject.SetActive(false);
            if (placeInfoTextPanel != null)
            {
                placeInfoTextPanel.SetActive(false);
            }
        }
    }

    public void SetInfoImages(Sprite sprite1, Sprite sprite2, bool petFriendly, bool separateRestroom, string description, string name, int id = -1, string username = null, string instagramId = null, string tel = null, string address = null, string overview = null, string petInfo = null)
    {
        infoSprite1 = sprite1;
        infoSprite2 = sprite2;
        this.petFriendly = petFriendly;
        this.separateRestroom = separateRestroom;
        this.descriptionText = description;
        this.name = name;
        this.id = id;
        this.username = username;
        this.instagramId = instagramId;
        this.tel = tel;
        this.address = address;
        this.overview = overview;
        this.petInfo = petInfo;

        if (isFullscreen)
        {
            UpdateInfoImages();
        }

        Debug.Log($"[DoubleTap3D] SetInfoImages 호출 - ID: {id}, Description: {description}, Name: {name}, GameObject: {gameObject.name}");
    }

    private void UpdateInfoImages()
    {
        infoImage1.gameObject.SetActive(petFriendly && infoSprite1 != null);
        if (petFriendly && infoSprite1 != null) infoImage1.sprite = infoSprite1;

        infoImage2.gameObject.SetActive(separateRestroom && infoSprite2 != null);
        if (separateRestroom && infoSprite2 != null) infoImage2.sprite = infoSprite2;

        instagramButton.gameObject.SetActive(!string.IsNullOrEmpty(instagramId));

        nameText.gameObject.SetActive(!string.IsNullOrEmpty(name) && isFullscreen);
        if (!string.IsNullOrEmpty(name)) nameText.text = name;

        if (descriptionTextUI != null)
        {
            descriptionTextUI.gameObject.SetActive(!string.IsNullOrEmpty(descriptionText) && isFullscreen);
            if (!string.IsNullOrEmpty(descriptionText)) descriptionTextUI.text = descriptionText;
        }

        if (placeInfoTextPanel != null)
        {
            placeInfoTextPanel.SetActive(isPlaceInfoPage && isFullscreen);
            if (isPlaceInfoPage && isFullscreen)
            {
                List<string> infoLines = new List<string>();
                bool isKorean = Application.systemLanguage == SystemLanguage.Korean;

                if (!string.IsNullOrEmpty(tel))
                    infoLines.Add($"{(isKorean ? "전화번호" : "Phone")}: {tel}");
                if (!string.IsNullOrEmpty(address))
                    infoLines.Add($"{(isKorean ? "주소" : "Address")}: {address}");
                if (!string.IsNullOrEmpty(overview))
                    infoLines.Add($"{(isKorean ? "개요" : "Overview")}: {overview}");
                if (!string.IsNullOrEmpty(petInfo))
                    infoLines.Add($"{(isKorean ? "반려견 동반정보" : "Pet Companion Info")}:\n{petInfo}");

                if (placeInfoText != null)
                {
                    placeInfoText.text = infoLines.Count > 0 ? string.Join("\n\n", infoLines) : "";
                }
            }
        }
    }

    private void OnInstagramButtonClick()
    {
        if (!string.IsNullOrEmpty(instagramId))
        {
            string url = $"https://www.instagram.com/{instagramId}/";
            Application.OpenURL(url);
        }
    }

    public void SetImageSprites(List<Sprite> sprites)
    {
        imageSprites = sprites;
        // 풀스크린이 이미 열려있다면 즉시 캐싱
#if UNITY_IOS
        if (isFullscreen)
        {
            CacheImagesForFullscreen();
        }
#endif
    }

    public void SetImageUrls(List<string> urls)
    {
        imageUrls = urls;
    }

    /// <summary>
    /// 댓글 패널 열기
    /// </summary>
    private void ShowComments()
    {
        CommentSystem commentSystem = GetComponentInChildren<CommentSystem>();
        if (commentSystem == null)
        {
            commentSystem = fullscreenCanvasGroup.GetComponentInChildren<CommentSystem>();
        }

        if (commentSystem != null)
        {
            if (commentSystem.IsPanelOpen)
            {
                commentSystem.CloseFullCommentPanel();
            }
            else
            {
                commentSystem.LoadComments(id);
                commentSystem.OpenFullCommentPanel();
            }
            Debug.Log($"[DoubleTap3D] 댓글 패널 토글: {id}");
        }
        else
        {
            Debug.LogWarning("[DoubleTap3D] CommentSystem을 찾을 수 없습니다.");
        }
    }

    private void CloseFullscreen()
    {
        // 풀스크린 닫을 때 캐시 메모리 해제
#if UNITY_IOS
        ClearImageCache();

        if (savedObjectId == this.id)
        {
            savedFullscreenState = false;
            savedObjectId = -1;
            savedImageIndex = -1;
            savedIsPlaceInfoPage = true;
        }
#endif
        StartCoroutine(FadeOutCanvas(fadeDuration));
    }

    IEnumerator CrossFadeImage(float duration)
    {
        isFading = true;
        float elapsed = 0f;
        Color startColor = fullscreenImage.color;

        while (elapsed < duration / 2)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / (duration / 2));
            fullscreenImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        ShowImage(currentIndex);
        elapsed = 0f;
        while (elapsed < duration / 2)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / (duration / 2));
            fullscreenImage.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        fullscreenImage.color = Color.white;
        isFading = false;
    }

    IEnumerator FadeInCanvas(float duration)
    {
        float elapsed = 0f;
        fullscreenCanvasGroup.alpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fullscreenCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }

        fullscreenCanvasGroup.alpha = 1f;
    }

    IEnumerator FadeOutCanvas(float duration)
    {
        isFading = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fullscreenCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }

        fullscreenCanvasGroup.alpha = 0f;
        fullscreenCanvasGroup.gameObject.SetActive(false);
        guidePanel.SetActive(false);
        if (descriptionTextUI != null)
        {
            descriptionTextUI.gameObject.SetActive(false);
        }
        if (placeInfoTextPanel != null)
        {
            placeInfoTextPanel.SetActive(false);
        }
        isFullscreen = false;
        isFading = false;
    }

    public int GetId()
    {
        Debug.Log($"[DoubleTap3D] GetId 호출 - ID: {id}, GameObject: {gameObject.name}");
        return id;
    }
    public string GetUsername() => username;
    public string GetName() => name;
    public bool IsPetFriendly() => petFriendly;
    public bool IsSeparateRestroom() => separateRestroom;
    public string GetInstagramId() => instagramId;

    public void ResetData()
    {
        infoSprite1 = null;
        infoSprite2 = null;
        petFriendly = false;
        separateRestroom = false;
        descriptionText = null;
        name = null;
        id = -1;
        username = null;
        instagramId = null;
        tel = null;
        address = null;
        overview = null;
        petInfo = null;
        imageUrls.Clear();
        ClearImageCache();
    }
}