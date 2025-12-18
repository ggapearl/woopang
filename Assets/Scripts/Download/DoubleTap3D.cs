using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

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
    public float fadeDuration = 0.3f;
    public float swipeSpeed = 15f;

    private float lastTapTime = 0f;
    private bool isFullscreen = false;
    private int currentIndex = 0;
    private int imageIndex = -1;
    private bool isPlaceInfoPage = true;
    private bool isFading = false;
    private Vector2 touchStartPos;
    private bool isSwiping;
    private RectTransform currentImageRect;
    private Vector2 imageTargetPos;
    private bool isDragging = false;

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
            enabled = false;
            return;
        }

        currentImageRect = fullscreenImage.GetComponent<RectTransform>();

        // 이미지 비율 유지 설정
        fullscreenImage.preserveAspect = true;
        fullscreenImage.type = Image.Type.Simple;

        imageDisplayController = GetComponentInParent<ImageDisplayController>();
        if (imageDisplayController == null)
        {
            imageDisplayController = GetComponentInChildren<ImageDisplayController>();
        }

        if (placeInfoTextPanel != null)
        {
            placeInfoText = placeInfoTextPanel.GetComponentInChildren<Text>();
            placeInfoTextPanel.SetActive(false);
        }

        fullscreenCanvasGroup.gameObject.SetActive(false);
        guidePanel.SetActive(false);
        fullscreenCanvasGroup.alpha = 0f;

        if (descriptionTextUI != null)
        {
            descriptionTextUI.gameObject.SetActive(false);
        }

        instagramButton.onClick.AddListener(OnInstagramButtonClick);
        nextButton.onClick.AddListener(ShowNextImage);
        previousButton.onClick.AddListener(ShowPreviousImage);
        closeButton.onClick.AddListener(CloseFullscreen);

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

        cachedImageData.Clear();

        for (int i = 0; i < imageSprites.Count; i++)
        {
            if (imageSprites[i] != null && imageSprites[i].texture != null)
            {
                try
                {
                    byte[] imageData = imageSprites[i].texture.EncodeToPNG();
                    cachedImageData[i] = imageData;
                }
                catch (Exception e)
                {
                }
            }
        }

        imagesAreCached = true;
    }

    // 캐시에서 이미지 복원
    private void RestoreImagesFromCache()
    {
        if (cachedImageData.Count == 0)
        {
            return;
        }

        List<Sprite> restoredSprites = new List<Sprite>();

        foreach (var kvp in cachedImageData)
        {
            try
            {
                Texture2D restoredTexture = new Texture2D(2, 2);
                restoredTexture.LoadImage(kvp.Value);

                Sprite restoredSprite = Sprite.Create(
                    restoredTexture,
                    new Rect(0, 0, restoredTexture.width, restoredTexture.height),
                    new Vector2(0.5f, 0.5f)
                );

                restoredSprites.Add(restoredSprite);
            }
            catch (Exception e)
            {
            }
        }

        imageSprites = restoredSprites;
    }

    // 캐시 메모리 해제
    private void ClearImageCache()
    {
        cachedImageData.Clear();
        imagesAreCached = false;
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
        }
        else if (!pauseStatus && savedFullscreenState && savedObjectId == this.id)
        {
            StartCoroutine(RestoreFullscreenForiOS());
        }
    }

    private IEnumerator RestoreFullscreenForiOS()
    {
        yield return new WaitForSeconds(0.5f);

        if (imagesAreCached && cachedImageData.Count > 0)
        {
            RestoreImagesFromCache();
        }

        isFullscreen = true;
        imageIndex = savedImageIndex;
        isPlaceInfoPage = savedIsPlaceInfoPage;
        currentIndex = imageIndex >= 0 ? imageIndex : 0;

        fullscreenCanvasGroup.gameObject.SetActive(true);
        guidePanel.SetActive(true);
        fullscreenCanvasGroup.alpha = 1f;

        ShowImage(savedImageIndex);
        UpdateInfoImages();

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
    }
#endif

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();

        // 버튼 리스너 개별 제거 (다른 컴포넌트의 리스너 보호)
        if (instagramButton != null)
            instagramButton.onClick.RemoveListener(OnInstagramButtonClick);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(ShowNextImage);
        if (previousButton != null)
            previousButton.onClick.RemoveListener(ShowPreviousImage);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseFullscreen);
    }

    void Update()
    {
        if (Touch.activeTouches.Count == 1 && Time.timeSinceLevelLoad > 2f)
        {
            var touch = Touch.activeTouches[0];

            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.screenPosition;
                isSwiping = true;

                Ray ray = Camera.main.ScreenPointToRay(touch.screenPosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
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
            }
            else if (touch.phase == TouchPhase.Moved && isSwiping && isFullscreen)
            {
                Vector2 swipeDelta = touch.screenPosition - touchStartPos;

                // 드래그 중: 이미지를 실시간으로 이동
                if (Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
                {
                    // 좌우 스와이프 - 이미지 드래그
                    isDragging = true;
                    if (currentImageRect != null)
                    {
                        // 경계 체크 (엘라스틱 효과)
                        float dragX = swipeDelta.x;

                        // 첫 이미지에서 오른쪽 드래그 제한
                        bool isAtStart = (placeInfoTextPanel == null || !isPlaceInfoPage) && imageIndex == 0;
                        if (isAtStart && dragX > 0)
                        {
                            dragX *= 0.3f; // 엘라스틱 저항
                        }

                        // 마지막 이미지에서 왼쪽 드래그 제한
                        bool isAtEnd = imageIndex == imageSprites.Count - 1 && !isPlaceInfoPage;
                        if (isAtEnd && dragX < 0)
                        {
                            dragX *= 0.3f; // 엘라스틱 저항
                        }

                        currentImageRect.anchoredPosition = imageTargetPos + new Vector2(dragX, 0);
                    }
                }
                else if (swipeDelta.y < -swipeThreshold)
                {
                    // 아래로 스와이프 - 패널 닫기
                    CloseFullscreen();
                    isSwiping = false;
                    isDragging = false;
                }
            }
            else if (touch.phase == TouchPhase.Ended && isFullscreen)
            {
                if (isDragging)
                {
                    Vector2 swipeDelta = touch.screenPosition - touchStartPos;

                    // 스와이프 거리가 threshold를 넘었는지 확인
                    if (Mathf.Abs(swipeDelta.x) > swipeThreshold)
                    {
                        if (swipeDelta.x > 0)
                            ShowPreviousImage();
                        else
                            ShowNextImage();
                    }
                    else
                    {
                        // threshold 미달 - 원래 위치로 복귀
                        ResetImagePosition();
                    }
                    isDragging = false;
                }
                isSwiping = false;
            }
        }

        // 이미지가 타겟 위치로 부드럽게 이동
        if (!isDragging && isFullscreen && currentImageRect != null)
        {
            currentImageRect.anchoredPosition = Vector2.Lerp(
                currentImageRect.anchoredPosition,
                imageTargetPos,
                Time.deltaTime * swipeSpeed
            );
        }
    }

    private void OnDoubleTapCube()
    {
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

            ShowImage(imageIndex);
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
        }
        else
        {
            CloseFullscreen();
        }
    }

    public void ShowNextImage()
    {
        if (imageSprites.Count == 0) return;

        // PlaceInfo 페이지에서 첫 이미지로 이동
        if (placeInfoTextPanel != null && isPlaceInfoPage)
        {
            isPlaceInfoPage = false;
            imageIndex = 0;
            currentIndex++;
            StartCoroutine(CrossFadeImage(imageIndex));
            UpdatePlaceInfoVisibility();
        }
        // 마지막 이미지가 아니면 다음 이미지로
        else if (imageIndex < imageSprites.Count - 1)
        {
            imageIndex++;
            currentIndex++;
            StartCoroutine(CrossFadeImage(imageIndex));
        }
        // 마지막 이미지에서는 더 이상 진행하지 않음 (경계)
    }

    public void ShowPreviousImage()
    {
        if (imageSprites.Count == 0) return;

        // 첫 이미지에서 PlaceInfo 페이지로 이동
        if (placeInfoTextPanel != null && !isPlaceInfoPage && imageIndex == 0)
        {
            isPlaceInfoPage = true;
            imageIndex = -1;
            currentIndex--;
            if (currentIndex < 0) currentIndex = 0;
            ShowImage(-1);
            UpdatePlaceInfoVisibility();
        }
        // 첫 이미지가 아니면 이전 이미지로
        else if (imageIndex > 0)
        {
            imageIndex--;
            currentIndex--;
            if (currentIndex < 0) currentIndex = 0;
            StartCoroutine(CrossFadeImage(imageIndex));
        }
        // PlaceInfo 페이지이거나 첫 이미지에서는 더 이상 뒤로 가지 않음 (경계)
    }

    private void UpdatePlaceInfoVisibility()
    {
        if (placeInfoTextPanel != null)
        {
            placeInfoTextPanel.SetActive(isPlaceInfoPage && isFullscreen);
        }
    }

    private void ShowImage(int index)
    {
        if (index == -1)
        {
            fullscreenImage.enabled = false;
        }
        else if (index >= 0 && index < imageSprites.Count)
        {
            fullscreenImage.enabled = true;
            fullscreenImage.sprite = imageSprites[index];
        }
        ResetImagePosition();
    }

    private IEnumerator CrossFadeImage(int index)
    {
        if (index < 0 || index >= imageSprites.Count) yield break;

        float elapsed = 0f;
        Color startColor = fullscreenImage.color;

        // Fade out
        while (elapsed < fadeDuration / 2)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / (fadeDuration / 2));
            fullscreenImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        // 이미지 변경
        fullscreenImage.sprite = imageSprites[index];
        ResetImagePosition();

        // Fade in
        elapsed = 0f;
        while (elapsed < fadeDuration / 2)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / (fadeDuration / 2));
            fullscreenImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        fullscreenImage.color = startColor;
    }

    private void ResetImagePosition()
    {
        imageTargetPos = Vector2.zero;
        if (currentImageRect != null)
        {
            currentImageRect.anchoredPosition = Vector2.zero;
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

        // 이미 풀스크린이 열려있다면 이미지 재생성
        if (isFullscreen)
        {
            ShowImage(imageIndex);
#if UNITY_IOS
            CacheImagesForFullscreen();
#endif
        }
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
