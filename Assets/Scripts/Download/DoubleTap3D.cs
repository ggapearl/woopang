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
    public Text createdByText; // Created by (username) 표시
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
    [SerializeField] private string placeName; // 인스펙터에서 설정 가능 (씬 배치 Cube용)
    private string instagramId;
    [SerializeField] private int id = -1; // 인스펙터에서 설정 가능 (씬 배치 Cube용)
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
    private bool isCooldown = false; // 더블탭 쿨다운
    private bool canClose = true; // 닫기 방지 쿨다운

    [Header("Comment Preview")]
    public GameObject commentPreviewPanel;
    public Text previewText;
    public Text previewLikeCount;
    public Image previewLikeIcon; // 좋아요 아이콘 이미지
    public Sprite likedSprite;    // 좋아요 있을 때 (채워진 하트)
    public Sprite likeIcon;       // 좋아요 없을 때 (빈 하트)

    private Dictionary<string, string> noCommentTranslations = new Dictionary<string, string>
    {
        { "en", "No comments yet. Be the first to comment!" },
        { "ko", "아직 댓글이 없습니다. 첫 댓글을 남겨보세요!" },
        { "ja", "コメントはまだありません。最初のコメントを残してください！" },
        { "zh", "暂无评论。快来抢沙发吧！" },
        { "es", "Aún no hay comentarios. ¡Sé el primero en comentar!" }
    };

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

        // 코멘트 프리뷰 UI: fullscreenCanvasGroup 안에 하나만 존재해야 함
        if (fullscreenCanvasGroup != null)
        {
            // 기존에 생성된 패널이 있는지 확인
            Transform existingPanel = fullscreenCanvasGroup.transform.Find("CommentPreviewPanel");
            if (existingPanel != null)
            {
                // 기존 패널 사용
                commentPreviewPanel = existingPanel.gameObject;
            }

            // 패널이 없으면 새로 생성
            if (commentPreviewPanel == null)
            {
                CreateCommentPreviewUI();
            }

            // 패널 참조 연결
            if (commentPreviewPanel != null)
            {
                if (previewText == null)
                    previewText = commentPreviewPanel.transform.Find("PreviewText")?.GetComponent<Text>();
                if (previewLikeCount == null)
                    previewLikeCount = commentPreviewPanel.transform.Find("PreviewLike")?.GetComponent<Text>();

                // 버튼 클릭 리스너 연결 (각 인스턴스마다 등록하되, isFullscreen 체크로 필터링)
                Button panelBtn = commentPreviewPanel.GetComponent<Button>();
                if (panelBtn == null)
                {
                    panelBtn = commentPreviewPanel.AddComponent<Button>();
                    panelBtn.transition = Selectable.Transition.None;
                }
                // 중복 등록 방지 후 리스너 추가
                panelBtn.onClick.RemoveListener(OnCommentPreviewClicked);
                panelBtn.onClick.AddListener(OnCommentPreviewClicked);
            }
        }

        currentImageRect = fullscreenImage.GetComponent<RectTransform>();

        // 이미지 비율 유지 설정
        fullscreenImage.preserveAspect = true;
        fullscreenImage.type = Image.Type.Simple;

        // fullscreenImage를 형제들 중 가장 먼저 그려지도록 (UI상 가장 뒤에 배치)
        fullscreenImage.transform.SetAsFirstSibling();

        imageDisplayController = GetComponentInParent<ImageDisplayController>();
        if (imageDisplayController == null)
        {
            imageDisplayController = GetComponentInChildren<ImageDisplayController>();
        }

        // 씬 배치 Cube의 경우: id가 설정되지 않았으면 부모 이름에서 추출 시도
        // 예: "0005_Cube_Train" -> 부모/조상 오브젝트에서 숫자 id를 찾음
        if (id == -1)
        {
            TryParseIdFromHierarchy();
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

        // Created by (username) 텍스트 UI 동적 생성
        if (createdByText == null && fullscreenCanvasGroup != null)
        {
            // 기존에 생성된 것이 있는지 확인
            Transform existingCreatedBy = fullscreenCanvasGroup.transform.Find("CreatedByText");
            if (existingCreatedBy != null)
            {
                createdByText = existingCreatedBy.GetComponent<Text>();
            }
            else
            {
                CreateCreatedByUI();
            }
        }
        if (createdByText != null)
        {
            createdByText.gameObject.SetActive(false);
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
        // 댓글창이 열려있으면 DoubleTap3D 스와이프 동작 중지
        if (CommentManager.Instance != null && CommentManager.Instance.IsPanelOpen) return;

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
                else
                {
                    // 수직 스와이프 감지
                    if (swipeDelta.y > swipeThreshold)
                    {
                        // 위로 스와이프 -> 댓글창 열기
                        if (CommentManager.Instance != null)
                        {
                            CommentManager.Instance.OpenCommentPanel(this.id, this.placeName);
                            isSwiping = false; // 스와이프 종료 처리
                        }
                    }
                    else if (swipeDelta.y < -swipeThreshold)
                    {
                        // 아래로 스와이프 -> 패널 닫기 (기존 로직)
                        CloseFullscreen();
                        isSwiping = false;
                        isDragging = false;
                    }
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

    /// <summary>
    /// Created by (username) UI 동적 생성
    /// </summary>
    private void CreateCreatedByUI()
    {
        GameObject textObj = new GameObject("CreatedByText");
        textObj.transform.SetParent(fullscreenCanvasGroup.transform, false);

        createdByText = textObj.AddComponent<Text>();
        createdByText.font = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (createdByText.font == null)
            createdByText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        createdByText.fontSize = 18;
        createdByText.color = new Color(0.8f, 0.8f, 0.8f, 1f); // 연한 회색
        createdByText.alignment = TextAnchor.MiddleLeft;

        // 그림자 효과 추가
        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(1, -1);

        RectTransform rect = textObj.GetComponent<RectTransform>();
        // 이미지 아래에 배치 (하단 왼쪽)
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0, 0);
        rect.sizeDelta = new Vector2(0, 30);
        rect.anchoredPosition = new Vector2(20, 190); // CommentPreviewPanel 위에 위치

        textObj.SetActive(false);
    }

    private void CreateCommentPreviewUI()
    {
        // Panel
        GameObject panelObj = new GameObject("CommentPreviewPanel");
        panelObj.transform.SetParent(fullscreenCanvasGroup.transform, false);
        commentPreviewPanel = panelObj;
        
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0); // Bottom stretch
        panelRect.anchorMax = new Vector2(1, 0);
        panelRect.pivot = new Vector2(0.5f, 0);
        panelRect.sizeDelta = new Vector2(0, 80);
        panelRect.anchoredPosition = new Vector2(0, 100); // Slightly above bottom (above close button)

        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0.5f); // Semi-transparent black

        // Button for click interaction
        Button panelBtn = panelObj.AddComponent<Button>();
        panelBtn.transition = Selectable.Transition.None; // 터치 피드백 제거
        panelBtn.onClick.AddListener(OnCommentPreviewClicked);

        // Text
        GameObject textObj = new GameObject("PreviewText");
        textObj.transform.SetParent(panelObj.transform, false);
        previewText = textObj.AddComponent<Text>();
        previewText.font = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (previewText.font == null)
            previewText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        previewText.fontSize = 24; // Larger text
        previewText.color = Color.white;
        previewText.alignment = TextAnchor.MiddleLeft;
        previewText.resizeTextForBestFit = true;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20, 0);
        textRect.offsetMax = new Vector2(-80, 0); // Space for likes

        // Like Count
        GameObject likeObj = new GameObject("PreviewLike");
        likeObj.transform.SetParent(panelObj.transform, false);
        previewLikeCount = likeObj.AddComponent<Text>();
        previewLikeCount.font = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (previewLikeCount.font == null)
            previewLikeCount.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        previewLikeCount.fontSize = 20;
        previewLikeCount.color = Color.white;
        previewLikeCount.alignment = TextAnchor.MiddleRight;
        
        RectTransform likeRect = likeObj.GetComponent<RectTransform>();
        likeRect.anchorMin = new Vector2(1, 0);
        likeRect.anchorMax = new Vector2(1, 1);
        likeRect.offsetMin = new Vector2(-80, 0);
        likeRect.offsetMax = new Vector2(-20, 0);
    }

    private void OnDoubleTapCube()
    {
        if (isCooldown) return;

        isCooldown = true;
        StartCoroutine(ResetCooldown());

        OnDoubleTapEvent?.Invoke(this);

        isFullscreen = !isFullscreen;

        if (isFullscreen)
        {
            // 열릴 때 닫기 방지 쿨다운 시작
            canClose = false;
            StartCoroutine(EnableCloseAfterDelay());

            // ⭐ 이미지가 없거나 유효하지 않으면 서버에서 동적으로 로드
            bool needsImageLoad = imageSprites == null || imageSprites.Count == 0;
            if (!needsImageLoad && imageSprites.Count > 0)
            {
                // 스프라이트가 있지만 유효한지 확인
                foreach (var sprite in imageSprites)
                {
                    if (sprite == null || sprite.texture == null || sprite.texture == Texture2D.blackTexture)
                    {
                        needsImageLoad = true;
                        break;
                    }
                }
            }

            if (needsImageLoad && id > 0)
            {
                StartCoroutine(FetchSubPhotosFromServer(id));
            }

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

            // ⭐ 코멘트 프리뷰 업데이트
            if (CommentManager.Instance != null)
            {
                if (commentPreviewPanel != null) commentPreviewPanel.SetActive(true);
                
                CommentManager.Instance.GetBestComment(this.id, (data) => 
                {
                    if (previewText != null)
                    {
                        if (data != null)
                        {
                            string content = data.content;
                            if (content.Length > 40)
                            {
                                content = content.Substring(0, 40) + "... 더보기";
                            }
                            // 콜론 제거, 띄어쓰기 추가
                            previewText.text = $"<b>{data.username}</b>  {content}";

                            // 좋아요 아이콘/숫자 처리
                            if (previewLikeIcon != null)
                            {
                                previewLikeIcon.gameObject.SetActive(true);
                                // 좋아요 있으면 채워진 하트, 없으면 빈 하트
                                if (data.like_count > 0)
                                {
                                    previewLikeIcon.sprite = likedSprite;
                                }
                                else
                                {
                                    previewLikeIcon.sprite = likeIcon;
                                }
                                previewLikeIcon.color = Color.white;
                            }

                            if (previewLikeCount != null)
                            {
                                // 좋아요 0이면 숫자 표시 안함
                                previewLikeCount.text = data.like_count > 0 ? data.like_count.ToString() : "";
                            }
                        }
                        else
                        {
                            previewText.text = "아직 댓글이 없습니다. 첫 댓글을 남겨보세요!";
                            if (previewLikeCount != null) previewLikeCount.text = "";
                            // 댓글 없으면 빈 하트 표시
                            if (previewLikeIcon != null)
                            {
                                previewLikeIcon.sprite = likeIcon;
                                previewLikeIcon.color = Color.white;
                                previewLikeIcon.gameObject.SetActive(true);
                            }
                        }
                    }
                });
            }
        }
        else
        {
            // 닫기 시도: 쿨다운 중이면 닫지 않음
            if (!canClose)
            {
                isFullscreen = true; // 상태 복구
                return;
            }
            CloseFullscreen();
        }
    }

    IEnumerator ResetCooldown()
    {
        yield return new WaitForSeconds(0.5f); // 입력 쿨다운은 조금 짧게
        isCooldown = false;
    }

    IEnumerator EnableCloseAfterDelay()
    {
        yield return new WaitForSeconds(1f); // 1초 동안 닫기 방지
        canClose = true;
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
            fullscreenImage.color = Color.white;

            // fullscreenImage가 guidePanel(FullScreenGuide) 뒤에 위치하도록 설정
            fullscreenImage.transform.SetAsFirstSibling();
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

        // Created by (username) 표시
        if (createdByText != null)
        {
            bool showCreatedBy = !string.IsNullOrEmpty(username) && isFullscreen;
            createdByText.gameObject.SetActive(showCreatedBy);
            if (showCreatedBy)
            {
                createdByText.text = $"Created by {username}";
            }
        }

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
        imageSprites = sprites ?? new List<Sprite>();

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
    public string GetName() => placeName;

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

    /// <summary>
    /// 씬 배치 Cube용: 부모/조상 오브젝트 이름에서 id 파싱 시도
    /// 패턴: "0132_Cube_집" -> id=132, placeName="집"
    /// </summary>
    private void TryParseIdFromHierarchy()
    {
        Transform current = transform;

        // 자신과 부모들의 이름 확인
        while (current != null)
        {
            string objName = current.gameObject.name;

            // "0132_Cube_집" 또는 "Place_132_cube" 패턴 파싱
            if (TryParseNamePattern(objName))
            {
                return;
            }

            current = current.parent;
        }
    }

    /// <summary>
    /// 오브젝트 이름에서 id와 placeName 파싱
    /// </summary>
    private bool TryParseNamePattern(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        // 패턴 1: "0132_Cube_집" 또는 "132_Cube_집" (숫자 + _Cube_ + 이름)
        var match1 = System.Text.RegularExpressions.Regex.Match(name, @"^(\d+)_Cube_(.+)$");
        if (match1.Success)
        {
            if (int.TryParse(match1.Groups[1].Value, out int parsedId) && parsedId > 0)
            {
                id = parsedId;
                if (string.IsNullOrEmpty(placeName))
                {
                    placeName = match1.Groups[2].Value;
                }
                return true;
            }
        }

        // 패턴 2: "Place_132_cube" (DataManager 생성 패턴)
        var match2 = System.Text.RegularExpressions.Regex.Match(name, @"Place_(\d+)_");
        if (match2.Success)
        {
            if (int.TryParse(match2.Groups[1].Value, out int parsedId))
            {
                id = parsedId;
                return true;
            }
        }

        // 패턴 3: 이름에 숫자만 있는 경우 (예: "132")
        if (int.TryParse(name, out int directId) && directId > 0)
        {
            id = directId;
            return true;
        }

        return false;
    }

    private void OnCommentPreviewClicked()
    {
        // 현재 fullscreen 상태인 인스턴스만 댓글창 열기
        // (여러 DoubleTap3D 인스턴스가 동일한 버튼에 리스너를 등록하므로 필터링 필요)
        if (!isFullscreen)
        {
            return;
        }

        if (CommentManager.Instance != null && this.id != -1)
        {
            Debug.Log($"[DoubleTap3D] OnCommentPreviewClicked - id: {this.id}, placeName: {this.placeName}");
            CommentManager.Instance.OpenCommentPanel(this.id, this.placeName);
        }
    }

    private IEnumerator FetchSubPhotosFromServer(int locationId)
    {
        string url = $"{ApiConfig.MAIN_SERVER}/locations/{locationId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                List<string> allSubPhotos = ParseSubPhotosFromJson(json);

                if (allSubPhotos != null && allSubPhotos.Count > 0)
                {
                    yield return StartCoroutine(LoadSubPhotosDirectly(allSubPhotos));
                }
            }
        }
    }

    private List<string> ParseSubPhotosFromJson(string json)
    {
        List<string> photos = new List<string>();

        try
        {
            int startIndex = json.IndexOf("\"sub_photos\":");
            if (startIndex == -1) return photos;

            int bracketStart = json.IndexOf('[', startIndex);
            if (bracketStart == -1) return photos;

            int depth = 0;
            int bracketEnd = bracketStart;
            for (int i = bracketStart; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']') depth--;

                if (depth == 0)
                {
                    bracketEnd = i;
                    break;
                }
            }

            string subPhotosStr = json.Substring(bracketStart, bracketEnd - bracketStart + 1);

            int pos = 0;
            while (pos < subPhotosStr.Length)
            {
                int quoteStart = subPhotosStr.IndexOf('"', pos);
                if (quoteStart == -1) break;

                int quoteEnd = subPhotosStr.IndexOf('"', quoteStart + 1);
                if (quoteEnd == -1) break;

                string photoUrl = subPhotosStr.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                if (!string.IsNullOrEmpty(photoUrl) && (photoUrl.Contains("uploads/") || photoUrl.StartsWith("http")))
                {
                    photos.Add(photoUrl);
                }

                pos = quoteEnd + 1;
            }
        }
        catch (System.Exception)
        {
            // JSON 파싱 실패 시 무시 (빈 리스트 반환)
        }

        return photos;
    }

    private IEnumerator LoadSubPhotosDirectly(List<string> photoUrls)
    {
        List<Sprite> newSprites = new List<Sprite>();

        foreach (string photoUrl in photoUrls)
        {
            string fullUrl = photoUrl.StartsWith("http") ? photoUrl : ApiConfig.MAIN_SERVER + "/" + photoUrl.Replace("\\", "/");

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                    if (texture != null)
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        if (sprite != null)
                        {
                            newSprites.Add(sprite);
                        }
                    }
                }
            }
        }

        if (newSprites.Count > 0)
        {
            imageSprites = newSprites;

            if (isFullscreen && imageIndex >= 0)
            {
                ShowImage(imageIndex);
            }

#if UNITY_IOS
            CacheImagesForFullscreen();
#endif
        }
    }

}
