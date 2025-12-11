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
    public Text debugText;
    private Text placeInfoText;
    public float tapSpeed = 0.5f;
    public float swipeThreshold = 30f;  // 50f → 30f (더 민감하게)
    public float fadeDuration = 1.0f;  // 0.5초 → 1.0초 (2배 느리게)

    private float lastTapTime = 0f;
    private bool isFullscreen = false;
    private int currentIndex = 0;
    private int imageIndex = -1;
    private bool isPlaceInfoPage = true;
    private bool isFading = false;
    private Vector2 touchStartPos;
    private bool isSwiping;

    // FullScreenGuide 패널 페이드용 CanvasGroup
    private CanvasGroup guidePanelCanvasGroup;

    private Sprite infoSprite1;
    private Sprite infoSprite2;
    private bool petFriendly;
    private bool separateRestroom;
    private string descriptionText;
    private string placeName;
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

        // FullScreenGuide 패널에 CanvasGroup 추가 (없으면 자동 생성)
        if (guidePanel != null)
        {
            guidePanelCanvasGroup = guidePanel.GetComponent<CanvasGroup>();
            if (guidePanelCanvasGroup == null)
            {
                guidePanelCanvasGroup = guidePanel.AddComponent<CanvasGroup>();
            }
            guidePanelCanvasGroup.alpha = 0f;
        }

        if (descriptionTextUI != null)
        {
            descriptionTextUI.gameObject.SetActive(false);
        }

        fullscreenImage.preserveAspect = true;
        fullscreenImage.type = Image.Type.Simple;

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
        if (Input.touchCount == 1 && Time.timeSinceLevelLoad > 2f)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.position;
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
            else if (touch.phase == TouchPhase.Moved && isSwiping && isFullscreen)
            {
                Vector2 swipeDelta = touch.position - touchStartPos;

                // 좌우 스와이프: 이미지 넘기기
                if (Mathf.Abs(swipeDelta.x) > swipeThreshold && Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
                {
                    if (swipeDelta.x > 0)
                        ShowPreviousImage();  // 오른쪽 스와이프 → 이전 이미지
                    else
                        ShowNextImage();      // 왼쪽 스와이프 → 다음 이미지
                    isSwiping = false;
                }
                // 위→아래 스와이프: 패널 닫기
                else if (Mathf.Abs(swipeDelta.y) > swipeThreshold && Mathf.Abs(swipeDelta.y) > Mathf.Abs(swipeDelta.x) && swipeDelta.y < 0)
                {
                    CloseFullscreen();  // 페이드아웃으로 닫힘
                    isSwiping = false;
                }
            }
            else if (touch.phase == TouchPhase.Ended && isSwiping && isFullscreen)
            {
                // 터치 종료 시점에서도 스와이프 거리 체크 (빠른 스와이프 감지)
                Vector2 swipeDelta = touch.position - touchStartPos;

                // 좌우 스와이프: 이미지 넘기기
                if (Mathf.Abs(swipeDelta.x) > swipeThreshold && Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
                {
                    if (swipeDelta.x > 0)
                        ShowPreviousImage();  // 오른쪽 스와이프 → 이전 이미지
                    else
                        ShowNextImage();      // 왼쪽 스와이프 → 다음 이미지
                }
                // 아래로 스와이프: 패널 닫기
                else if (Mathf.Abs(swipeDelta.y) > swipeThreshold && Mathf.Abs(swipeDelta.y) > Mathf.Abs(swipeDelta.x) && swipeDelta.y < 0)
                {
                    CloseFullscreen();  // 페이드아웃으로 닫힘
                }

                isSwiping = false;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
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

            StartCoroutine(FadeInCanvas(fadeDuration));

            if (debugText != null)
            {
                debugText.text = $"DoubleTap: ID = {id}, InstagramId = {instagramId ?? "null"}";
            }
        }
        else
        {
            CloseFullscreen();
        }
    }

    public void ShowNextImage()
    {
        if (imageSprites.Count == 0 || isFading) return;

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
        StartCoroutine(CrossFadeImage(fadeDuration));
    }

    public void ShowPreviousImage()
    {
        if (imageSprites.Count == 0 || isFading) return;

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
        StartCoroutine(CrossFadeImage(fadeDuration));
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
        this.placeName = name;
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

        Debug.Log($"[DoubleTap3D] SetInfoImages 호출 - ID: {id}, Description: {description}, Name: {placeName}, GameObject: {gameObject.name}");

        if (debugText != null)
        {
            debugText.text = $"SetInfoImages: ID = {id}, InstagramId = {instagramId ?? "null"}";
        }
    }

    private void UpdateInfoImages()
    {
        infoImage1.gameObject.SetActive(petFriendly && infoSprite1 != null);
        if (petFriendly && infoSprite1 != null) infoImage1.sprite = infoSprite1;

        infoImage2.gameObject.SetActive(separateRestroom && infoSprite2 != null);
        if (separateRestroom && infoSprite2 != null) infoImage2.sprite = infoSprite2;

        instagramButton.gameObject.SetActive(!string.IsNullOrEmpty(instagramId));

        nameText.gameObject.SetActive(!string.IsNullOrEmpty(placeName) && isFullscreen);
        if (!string.IsNullOrEmpty(placeName)) nameText.text = placeName;

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
            
            if (debugText != null)
            {
                debugText.text = $"InstagramButton: ID = {id}, InstagramId = {instagramId}, URL = {url}";
            }
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
        if (guidePanelCanvasGroup != null)
        {
            guidePanelCanvasGroup.alpha = 0f;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            fullscreenCanvasGroup.alpha = alpha;
            if (guidePanelCanvasGroup != null)
            {
                guidePanelCanvasGroup.alpha = alpha;
            }
            yield return null;
        }

        fullscreenCanvasGroup.alpha = 1f;
        if (guidePanelCanvasGroup != null)
        {
            guidePanelCanvasGroup.alpha = 1f;
        }
    }

    IEnumerator FadeOutCanvas(float duration)
    {
        isFading = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            fullscreenCanvasGroup.alpha = alpha;
            if (guidePanelCanvasGroup != null)
            {
                guidePanelCanvasGroup.alpha = alpha;
            }
            yield return null;
        }

        fullscreenCanvasGroup.alpha = 0f;
        if (guidePanelCanvasGroup != null)
        {
            guidePanelCanvasGroup.alpha = 0f;
        }
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
    public string GetName() => placeName;
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
        placeName = null;
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