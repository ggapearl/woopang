using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class DoubleTap3D : MonoBehaviour
{
    public CanvasGroup fullscreenCanvasGroup;
    public GameObject guidePanel;
    public Image fullscreenImage;
    public Image nextFullscreenImage; // 슬라이드 전환용 두 번째 이미지
    public List<Sprite> imageSprites = new List<Sprite>();
    public Image infoImage1;
    public Image infoImage2;
    public Button instagramButton;
    public Button previousButton;
    public Button nextButton;
    public Button closeButton;
    public Text nameText;
    public Text descriptionTextUI;
    public Text createdByText;
    public GameObject placeInfoTextPanel;
    private Text placeInfoText;
    public float tapSpeed = 0.3f;
    public float swipeThreshold = 50f;
    public float fadeDuration = 0.3f;
    public float swipeSpeed = 15f;

    [Header("Slide Settings")]
    public float slideDuration = 0.25f;
    [Tooltip("슬라이드 시 이미지 간 간격 (px)")]
    public float slideImageGap = 200f;
    [Tooltip("다음/이전 이미지 페이드인/아웃 시간 (초)")]
    public float slideFadeDuration = 0.4f;

    [Header("Touch Block")]
    [Tooltip("더블탭 인식 후 추가 터치 차단 시간 (초). 패널 열린 직후 연속 터치로 인한 오작동 방지.")]
    public float touchBlockDuration = 2f;

    private float lastTapTime = 0f;
    private bool isFullscreen = false;
    private int currentIndex = 0;
    private int imageIndex = -1;
    private bool isPlaceInfoPage = true;
    private Vector2 touchStartPos;
    private bool isSwiping;
    private RectTransform currentImageRect;
    private RectTransform nextImageRect;
    private Vector2 imageTargetPos;
    private Vector2 imageBasePos; // Inspector에서 설정한 기본 위치 (Y=-200 등)
    private bool isDragging = false;
    private bool isSliding = false;
    private int dragDirection = 0; // -1: 왼쪽(다음), 1: 오른쪽(이전), 0: 없음
    private float nextImageFadeTime = 0f; // 다음 이미지 페이드인 시작 시간

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

    // iOS 캐싱 시스템 (백그라운드 복귀 시 텍스처 복원용)
    private struct CachedTextureData
    {
        public byte[] rawData;
        public int width;
        public int height;
        public TextureFormat format;
    }
    private Dictionary<int, CachedTextureData> cachedImageData = new Dictionary<int, CachedTextureData>();
    private bool imagesAreCached = false;
    private bool isCooldown = false; // 더블탭 쿨다운
    private bool canClose = true; // 닫기 방지 쿨다운
    private bool isFadingOut = false; // FadeOut 코루틴 중복 실행 방지

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

    /// <summary>
    /// 프리팹에서 Instantiate된 경우 UI 참조가 null → 씬에서 이름으로 자동 검색
    /// </summary>
    private void AutoConnectFullscreenUI()
    {
        if (fullscreenCanvasGroup != null) return; // 이미 연결됨

        // GameObject.Find()는 비활성 오브젝트를 못 찾음
        // FullScreenPanel은 비활성 상태(m_IsActive:0)이므로 Canvas에서 재귀 검색
        GameObject panel = GameObject.Find("FullScreenPanel");
        if (panel == null)
        {
            // 비활성 오브젝트 검색: 모든 Canvas에서 자식 중 "FullScreenPanel" 찾기
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas.gameObject.scene.name == null) continue; // 프리팹 에셋 제외
                Transform found = FindChildRecursive(canvas.transform, "FullScreenPanel");
                if (found != null)
                {
                    panel = found.gameObject;
                    break;
                }
            }
        }
        if (panel == null) return;

        fullscreenCanvasGroup = panel.GetComponent<CanvasGroup>();

        Transform panelT = panel.transform;
        Transform guideT = panelT.Find("GuidePanel");
        if (guideT != null) guidePanel = guideT.gameObject;

        // FullScreenPanel 하위 재귀 검색 헬퍼
        System.Func<string, Transform> findChild = null;
        findChild = (string name) => {
            Transform found = panelT.Find(name);
            if (found != null) return found;
            // 재귀 검색
            foreach (Transform child in panelT.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        };

        Transform t;
        t = findChild("FullScreenImage");
        if (t != null) fullscreenImage = t.GetComponent<Image>();

        t = findChild("NextFullscreenImage");
        if (t != null) nextFullscreenImage = t.GetComponent<Image>();

        t = findChild("InfoImage1");
        if (t != null) infoImage1 = t.GetComponent<Image>();

        t = findChild("InfoImage2");
        if (t != null) infoImage2 = t.GetComponent<Image>();

        t = findChild("InstagramButton");
        if (t != null) instagramButton = t.GetComponent<Button>();

        t = findChild("PreviousButton");
        if (t != null) previousButton = t.GetComponent<Button>();
        if (previousButton == null)
        {
            t = findChild("Button_Previous");
            if (t != null) previousButton = t.GetComponent<Button>();
        }

        t = findChild("NextButton");
        if (t != null) nextButton = t.GetComponent<Button>();
        if (nextButton == null)
        {
            t = findChild("Button_Next");
            if (t != null) nextButton = t.GetComponent<Button>();
        }

        t = findChild("CloseButton");
        if (t != null) closeButton = t.GetComponent<Button>();

        t = findChild("NameText");
        if (t != null) nameText = t.GetComponent<Text>();

        t = findChild("DescriptionText");
        if (t != null) descriptionTextUI = t.GetComponent<Text>();

        t = findChild("CreatedByText");
        if (t != null) createdByText = t.GetComponent<Text>();

        t = findChild("PlaceInfoTextPanel");
        if (t != null) placeInfoTextPanel = t.gameObject;
    }

    /// <summary>
    /// 비활성 오브젝트 포함 재귀 검색
    /// </summary>
    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    void Start()
    {
        // 프리팹에서 Instantiate된 경우 UI 참조가 null이므로 씬에서 자동 검색
        AutoConnectFullscreenUI();

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
                if (previewLikeIcon == null)
                    previewLikeIcon = commentPreviewPanel.transform.Find("PreviewLikeIcon")?.GetComponent<Image>();
                if (previewLikeCount == null)
                    previewLikeCount = commentPreviewPanel.transform.Find("PreviewLike")?.GetComponent<Text>();

                // 버튼 클릭 리스너 — 공유 UI이므로 모든 리스너 제거 후 현재 인스턴스만 등록
                Button panelBtn = commentPreviewPanel.GetComponent<Button>();
                if (panelBtn == null)
                {
                    panelBtn = commentPreviewPanel.AddComponent<Button>();
                    panelBtn.transition = Selectable.Transition.None;
                }
                panelBtn.onClick.RemoveAllListeners();
                panelBtn.onClick.AddListener(OnCommentPreviewClicked);
            }
        }

        currentImageRect = fullscreenImage.GetComponent<RectTransform>();

        // Inspector에서 설정한 기본 위치 저장 (Y=-200 등)
        imageBasePos = currentImageRect.anchoredPosition;
        imageTargetPos = imageBasePos;

        // Inspector에서 이전 값이 남아있을 수 있으므로 최소 200px 보장
        if (slideImageGap < 200f) slideImageGap = 200f;
        if (slideFadeDuration <= 0f) slideFadeDuration = 0.4f;

        fullscreenImage.preserveAspect = true;
        fullscreenImage.type = Image.Type.Simple;

        // nextFullscreenImage: 같은 부모에 이미 존재하면 재사용
        if (nextFullscreenImage == null)
        {
            Transform existingNext = fullscreenImage.transform.parent.Find("NextFullscreenImage");
            if (existingNext != null)
            {
                nextFullscreenImage = existingNext.GetComponent<Image>();
            }
            else
            {
                GameObject nextImgObj = new GameObject("NextFullscreenImage");
                nextImgObj.transform.SetParent(fullscreenImage.transform.parent, false);
                nextFullscreenImage = nextImgObj.AddComponent<Image>();
            }
        }

        // nextFullscreenImage 설정 (fullscreenImage와 동일)
        nextFullscreenImage.preserveAspect = true;
        nextFullscreenImage.type = Image.Type.Simple;
        // Material 복사 (fullscreenImage에 설정된 UI Material 적용)
        if (fullscreenImage.material != null)
        {
            nextFullscreenImage.material = fullscreenImage.material;
        }

        // RectTransform 동기화
        RectTransform srcRect = currentImageRect;
        nextImageRect = nextFullscreenImage.GetComponent<RectTransform>();
        nextImageRect.anchorMin = srcRect.anchorMin;
        nextImageRect.anchorMax = srcRect.anchorMax;
        nextImageRect.pivot = srcRect.pivot;
        nextImageRect.sizeDelta = srcRect.sizeDelta;
        nextImageRect.anchoredPosition = new Vector2(Screen.width + slideImageGap, imageBasePos.y);

        nextFullscreenImage.enabled = false;

        // fullscreenImage를 형제들 중 가장 먼저 그려지도록 (UI상 가장 뒤에 배치)
        fullscreenImage.transform.SetAsFirstSibling();
        nextFullscreenImage.transform.SetAsFirstSibling();

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

        // Created by (username) 텍스트 UI 연결
        // ⚠️ 동적 생성 대신 FullScreenGuide 자식에 미리 배치된 오브젝트 사용
        // 에디터에서 WOOPANG > Setup > Add CreatedByText to FullScreenGuide 실행
        if (createdByText == null && guidePanel != null)
        {
            Transform existingCreatedBy = guidePanel.transform.Find("CreatedByText");
            if (existingCreatedBy != null)
            {
                createdByText = existingCreatedBy.GetComponent<Text>();
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
                    Texture2D tex = imageSprites[i].texture;
                    cachedImageData[i] = new CachedTextureData
                    {
                        rawData = tex.GetRawTextureData(),
                        width = tex.width,
                        height = tex.height,
                        format = tex.format
                    };
                }
                catch (Exception)
                {
                    // 이미지 캐싱 실패 - 무시
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
                CachedTextureData cached = kvp.Value;
                Texture2D restoredTexture = new Texture2D(cached.width, cached.height, cached.format, false);
                restoredTexture.LoadRawTextureData(cached.rawData);
                restoredTexture.Apply();

                Sprite restoredSprite = Sprite.Create(
                    restoredTexture,
                    new Rect(0, 0, cached.width, cached.height),
                    new Vector2(0.5f, 0.5f)
                );

                restoredSprites.Add(restoredSprite);
            }
            catch (Exception)
            {
                // 이미지 복원 실패 - 무시
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
            CacheImagesForFullscreen();
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
        // ★ 상태 일관성 자동 복구: isFadingOut 아닌데 실제 state가 다르면 강제 동기화
        // FadeOut이 완료된 후 이상 상태 방지
        if (!isFadingOut && fullscreenCanvasGroup != null)
        {
            bool actual = fullscreenCanvasGroup.gameObject.activeSelf;
            if (isFullscreen && !actual)
            {
                isFullscreen = false;
            }
        }

        // 댓글 패널이 열려있으면 모든 터치 입력 무시 (댓글 패널이 최상위 UI)
        if (CommentManager.Instance != null && CommentManager.Instance.IsPanelOpen)
            return;

#if UNITY_EDITOR
        // 에디터 마우스 디버깅
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame && Time.timeSinceLevelLoad > 2f)
        {
            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

            if (isFullscreen) { /* 풀스크린 모드는 스와이프로만 닫기 — 더블탭 닫기 방지 */ }
            else
            {
                Ray ray = Camera.main.ScreenPointToRay(mousePos);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == gameObject)
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
#endif

        int touchCount = Touch.activeTouches.Count;
        if (touchCount == 1 && Time.timeSinceLevelLoad > 2f)
        {
            var touch = Touch.activeTouches[0];

            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.screenPosition;
                isSwiping = true;

                // 비풀스크린: UI 위 터치이면 3D 오브젝트 터치 무시
                // 풀스크린: 전체 화면이 UI이므로 가드 스킵 (스와이프/버튼 모두 필요)
                bool isOverUI = !isFullscreen && EventSystem.current != null &&
                    EventSystem.current.IsPointerOverGameObject(touch.touchId);

                if (isOverUI)
                {
                    return;
                }

                // 풀스크린 상태에서는 3D 더블탭으로 닫기 방지 (스와이프로만 닫기)
                if (isFullscreen)
                {
                    return;
                }

                Ray ray = Camera.main.ScreenPointToRay(touch.screenPosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == gameObject)
                {
                    float timeSinceLastTap = Time.time - lastTapTime;
                    if (timeSinceLastTap < tapSpeed && timeSinceLastTap > 0.1f)
                    {
                        OnDoubleTapCube();
                    }
                    lastTapTime = Time.time;
                }
            }
            else if (touch.phase == TouchPhase.Moved && isSwiping && isFullscreen && !isSliding)
            {
                Vector2 swipeDelta = touch.screenPosition - touchStartPos;

                // 드래그 중: 이미지를 실시간으로 이동
                if (Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
                {
                    isDragging = true;
                    if (currentImageRect != null)
                    {
                        float dragX = swipeDelta.x;

                        // 경계 체크
                        bool isAtStart = (placeInfoTextPanel == null || !isPlaceInfoPage) && imageIndex == 0;
                        bool isAtEnd = imageIndex == imageSprites.Count - 1 && !isPlaceInfoPage;

                        if (isAtStart && dragX > 0)
                            dragX *= 0.3f;
                        if (isAtEnd && dragX < 0)
                            dragX *= 0.3f;

                        // 현재 이미지 이동 (Y는 기본 위치 유지)
                        currentImageRect.anchoredPosition = new Vector2(imageTargetPos.x + dragX, imageBasePos.y);

                        // 다음/이전 이미지 미리보기
                        float screenW = Screen.width;
                        int newDir = dragX < 0 ? -1 : (dragX > 0 ? 1 : 0);

                        if (newDir != 0 && newDir != dragDirection)
                        {
                            dragDirection = newDir;
                            PrepareNextImage(dragDirection);
                        }

                        if (nextFullscreenImage != null && nextFullscreenImage.enabled && nextImageRect != null)
                        {
                            // 간격 포함하여 다음 이미지 위치 계산
                            float nextBaseX = (dragDirection == -1) ? (screenW + slideImageGap) : -(screenW + slideImageGap);
                            nextImageRect.anchoredPosition = new Vector2(nextBaseX + dragX, imageBasePos.y);

                            // 페이드인: 드래그 시작 후 slideFadeDuration 동안 알파 0→1
                            float fadeElapsed = Time.time - nextImageFadeTime;
                            float fadeAlpha = (slideFadeDuration > 0f) ? Mathf.Clamp01(fadeElapsed / slideFadeDuration) : 1f;
                            nextFullscreenImage.color = new Color(1f, 1f, 1f, fadeAlpha);
                        }
                    }
                }
                else
                {
                    if (swipeDelta.y > swipeThreshold)
                    {
                        if (CommentManager.Instance != null)
                        {
                            CommentManager.Instance.OpenCommentPanel(this.id, this.placeName);
                        }
                        else
                        {
                            Debug.LogWarning("[DoubleTap3D] CommentManager.Instance is null");
                        }
                        isSwiping = false;
                        isDragging = false;
                        return; // 이 터치의 후속 처리 중단
                    }
                    else if (swipeDelta.y < -swipeThreshold)
                    {
                        CloseFullscreen();
                        isSwiping = false;
                        isDragging = false;
                        return; // 이 터치의 후속 처리 중단
                    }
                }
            }
            else if (touch.phase == TouchPhase.Ended && isFullscreen)
            {
                if (isDragging && !isSliding)
                {
                    Vector2 swipeDelta = touch.screenPosition - touchStartPos;

                    if (Mathf.Abs(swipeDelta.x) > swipeThreshold)
                    {
                        if (swipeDelta.x > 0)
                            ShowPreviousImage();
                        else
                            ShowNextImage();
                    }
                    else
                    {
                        ResetImagePosition();
                    }
                    isDragging = false;
                }
                isSwiping = false;
                dragDirection = 0;
            }
        }

        // 드래그 중이 아니고 슬라이드 중이 아닐 때: 위치 스냅백
        if (!isDragging && !isSliding && isFullscreen && currentImageRect != null)
        {
            currentImageRect.anchoredPosition = Vector2.Lerp(
                currentImageRect.anchoredPosition,
                imageTargetPos,
                Time.deltaTime * swipeSpeed
            );
            if (nextFullscreenImage != null && nextFullscreenImage.enabled && nextImageRect != null)
            {
                float snapX = (dragDirection == -1) ? (Screen.width + slideImageGap) : -(Screen.width + slideImageGap);
                nextImageRect.anchoredPosition = Vector2.Lerp(
                    nextImageRect.anchoredPosition,
                    new Vector2(snapX, imageBasePos.y),
                    Time.deltaTime * swipeSpeed
                );

                // 스냅백 시 페이드아웃
                float curAlpha = nextFullscreenImage.color.a;
                float newAlpha = Mathf.MoveTowards(curAlpha, 0f, Time.deltaTime / Mathf.Max(slideFadeDuration, 0.01f));
                nextFullscreenImage.color = new Color(1f, 1f, 1f, newAlpha);
                if (newAlpha <= 0f)
                    nextFullscreenImage.enabled = false;
            }
        }
    }

    // CreateCreatedByUI() 제거됨 - FullScreenGuide 자식으로 미리 배치
    // 에디터에서 WOOPANG > Setup > Add CreatedByText to FullScreenGuide 실행

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

        // Like Icon (하트 아이콘 Image)
        GameObject likeIconObj = new GameObject("PreviewLikeIcon");
        likeIconObj.transform.SetParent(panelObj.transform, false);
        previewLikeIcon = likeIconObj.AddComponent<Image>();
        previewLikeIcon.preserveAspect = true;
        previewLikeIcon.raycastTarget = false;
        if (likeIcon != null) previewLikeIcon.sprite = likeIcon;
        previewLikeIcon.color = Color.white;

        RectTransform likeIconRect = likeIconObj.GetComponent<RectTransform>();
        likeIconRect.anchorMin = new Vector2(1, 0.5f);
        likeIconRect.anchorMax = new Vector2(1, 0.5f);
        likeIconRect.pivot = new Vector2(1, 0.5f);
        likeIconRect.sizeDelta = new Vector2(28, 28);
        likeIconRect.anchoredPosition = new Vector2(-52, 0);

        // Like Count (좋아요 숫자)
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
        likeRect.offsetMin = new Vector2(-45, 0);
        likeRect.offsetMax = new Vector2(-10, 0);
    }

    private void OnDoubleTapCube()
    {
        if (isCooldown)
        {
            return;
        }

        // dance_anim 카테고리는 일반 정보 패널 대신 DanceAnimController(다운로드 패널) 라우팅.
        // 큐브 = 플레이스홀더, 더블탭 = "다운로드" 버튼 띄우기.
        if (id > 0 && DataManager.Instance != null && DataManager.Instance.IsAnimCategory(id))
        {
            Debug.Log($"[dbg-DoubleTap] anim 카테고리 감지 id={id} → DanceAnimController로 라우팅");
            var ctrl = DanceAnimController.Instance ?? DanceAnimController.EnsureInstance();
            if (ctrl != null)
            {
                string displayName = !string.IsNullOrEmpty(placeName) ? placeName : "3D 콘텐츠";
                ctrl.OnAnimCubeDoubleTapped(id, displayName);
                return;
            }
            else
            {
                Debug.LogError($"[dbg-DoubleTap] DanceAnimController 못 찾음 (Instance NULL + 씬에 없음) — 빌드된 씬 확인 필요");
            }
        }

        // ★ 상태 일관성 검증: isFullscreen과 실제 GameObject 상태가 다른 경우 수정
        bool actualState = fullscreenCanvasGroup != null && fullscreenCanvasGroup.gameObject.activeSelf;
        if (isFullscreen != actualState)
        {
            isFullscreen = actualState;
        }

        isCooldown = true;
        StartCoroutine(ResetCooldown());

        OnDoubleTapEvent?.Invoke(this);

        // 열기/닫기 결정 (토글 전 상태 기준)
        bool shouldOpen = !isFullscreen;

        if (shouldOpen)
        {
            // 더블탭 확정 사운드 + 햅틱 (Object3DTouchHaptic 연동)
            Object3DTouchHaptic haptic = GetComponentInChildren<Object3DTouchHaptic>(true);
            if (haptic == null) haptic = GetComponent<Object3DTouchHaptic>();
            if (haptic != null)
            {
                haptic.PlayDoubleTapFeedback();
            }

            isFullscreen = true;

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

            currentIndex = 0;
            isPlaceInfoPage = placeInfoTextPanel != null;
            imageIndex = placeInfoTextPanel != null ? -1 : 0;

            ShowImage(imageIndex);
            UpdateInfoImages();

            // 진행 중이던 FadeOut 코루틴 중단 후 열기
            if (isFadingOut)
            {
                StopAllCoroutines();
                isFadingOut = false;
            }

            fullscreenCanvasGroup.gameObject.SetActive(true);
            guidePanel.SetActive(true);

            instagramButton.onClick.RemoveAllListeners();
            instagramButton.onClick.AddListener(OnInstagramButtonClick);

            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(ShowNextImage);
            previousButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(ShowPreviousImage);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseFullscreen);

            StartCoroutine(FadeInCanvas(fadeDuration));

            // ⭐ 코멘트 프리뷰 업데이트 + 버튼 리스너 재등록 (공유 UI — 현재 fullscreen 인스턴스로)
            if (CommentManager.Instance != null)
            {
                if (commentPreviewPanel != null)
                {
                    commentPreviewPanel.SetActive(true);
                    Button panelBtn = commentPreviewPanel.GetComponent<Button>();
                    if (panelBtn != null)
                    {
                        panelBtn.onClick.RemoveAllListeners();
                        panelBtn.onClick.AddListener(OnCommentPreviewClicked);
                    }
                }
                
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
                                // 현재 사용자가 좋아요를 눌렀으면 채워진 하트, 아니면 빈 하트
                                previewLikeIcon.sprite = data.is_liked ? likedSprite : likeIcon;
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
                            string langCode = Application.systemLanguage == SystemLanguage.Korean ? "ko"
                            : Application.systemLanguage == SystemLanguage.Japanese ? "ja"
                            : Application.systemLanguage == SystemLanguage.Chinese || Application.systemLanguage == SystemLanguage.ChineseSimplified || Application.systemLanguage == SystemLanguage.ChineseTraditional ? "zh"
                            : Application.systemLanguage == SystemLanguage.Spanish ? "es"
                            : "en";
                        string noCommentMsg = noCommentTranslations.ContainsKey(langCode) ? noCommentTranslations[langCode] : noCommentTranslations["en"];
                        previewText.text = noCommentMsg;
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
                return;
            }
            CloseFullscreen();
        }
    }

    IEnumerator ResetCooldown()
    {
        yield return new WaitForSeconds(touchBlockDuration);
        isCooldown = false;
    }

    IEnumerator EnableCloseAfterDelay()
    {
        yield return new WaitForSeconds(1f); // 1초 동안 닫기 방지
        canClose = true;
    }

    public void ShowNextImage()
    {
        if (imageSprites.Count == 0 || isSliding) return;

        // PlaceInfo 페이지에서 첫 이미지로 이동
        if (placeInfoTextPanel != null && isPlaceInfoPage)
        {
            int targetIdx = 0;
            isPlaceInfoPage = false;
            imageIndex = targetIdx;
            currentIndex++;
            StartCoroutine(SlideToImage(-1, targetIdx));
            UpdatePlaceInfoVisibility();
        }
        else if (imageIndex < imageSprites.Count - 1)
        {
            int targetIdx = imageIndex + 1;
            imageIndex = targetIdx;
            currentIndex++;
            StartCoroutine(SlideToImage(-1, targetIdx));
        }
        else
        {
            ResetImagePosition();
        }
    }

    public void ShowPreviousImage()
    {
        if (imageSprites.Count == 0 || isSliding) return;

        if (placeInfoTextPanel != null && !isPlaceInfoPage && imageIndex == 0)
        {
            isPlaceInfoPage = true;
            imageIndex = -1;
            currentIndex--;
            if (currentIndex < 0) currentIndex = 0;
            StartCoroutine(SlideToImage(1, -1));
            UpdatePlaceInfoVisibility();
        }
        else if (imageIndex > 0)
        {
            int targetIdx = imageIndex - 1;
            imageIndex = targetIdx;
            currentIndex--;
            if (currentIndex < 0) currentIndex = 0;
            StartCoroutine(SlideToImage(1, targetIdx));
        }
        else
        {
            ResetImagePosition();
        }
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
            Sprite sprite = imageSprites[index];
            // 유효하지 않은 스프라이트(null 또는 검정 텍스처)면 이미지 숨김 — 서버 로드 완료 시 갱신됨
            if (sprite == null || sprite.texture == null || sprite.texture == Texture2D.blackTexture)
            {
                fullscreenImage.enabled = false;
            }
            else
            {
                fullscreenImage.enabled = true;
                fullscreenImage.sprite = sprite;
                fullscreenImage.color = Color.white;

                // fullscreenImage가 guidePanel(FullScreenGuide) 뒤에 위치하도록 설정
                fullscreenImage.transform.SetAsFirstSibling();
            }
        }
        else
        {
            // imageSprites 범위 밖 (아직 로드 중) — 이미지 숨김
            fullscreenImage.enabled = false;
        }
        ResetImagePosition();
    }

    /// <summary>
    /// 드래그 방향에 따라 nextFullscreenImage에 다음/이전 이미지를 준비
    /// </summary>
    private void PrepareNextImage(int direction)
    {
        if (nextFullscreenImage == null) return;

        int targetIndex = -999;
        if (direction == -1) // 왼쪽 드래그 → 다음 이미지
        {
            if (isPlaceInfoPage && placeInfoTextPanel != null)
                targetIndex = 0;
            else if (imageIndex < imageSprites.Count - 1)
                targetIndex = imageIndex + 1;
        }
        else if (direction == 1) // 오른쪽 드래그 → 이전 이미지
        {
            if (!isPlaceInfoPage && imageIndex == 0 && placeInfoTextPanel != null)
                targetIndex = -1; // PlaceInfo 페이지
            else if (imageIndex > 0)
                targetIndex = imageIndex - 1;
        }

        if (targetIndex == -999)
        {
            nextFullscreenImage.enabled = false;
            return;
        }

        if (targetIndex == -1)
        {
            // PlaceInfo 페이지 (이미지 없음)
            nextFullscreenImage.enabled = false;
        }
        else if (targetIndex >= 0 && targetIndex < imageSprites.Count)
        {
            nextFullscreenImage.sprite = imageSprites[targetIndex];
            nextFullscreenImage.color = new Color(1f, 1f, 1f, 0f); // 페이드인 시작: 투명
            nextFullscreenImage.enabled = true;
            nextImageFadeTime = Time.time;

            float screenW = Screen.width;
            float posX = (direction == -1) ? (screenW + slideImageGap) : -(screenW + slideImageGap);
            if (nextImageRect != null)
                nextImageRect.anchoredPosition = new Vector2(posX, imageBasePos.y);
        }
    }

    /// <summary>
    /// 슬라이드 애니메이션으로 이미지 전환
    /// </summary>
    private IEnumerator SlideToImage(int direction, int targetIndex)
    {
        // direction: -1 = 다음(왼쪽 슬라이드), 1 = 이전(오른쪽 슬라이드)
        isSliding = true;
        float screenW = Screen.width;
        float baseY = imageBasePos.y;

        // 현재 이미지: direction 방향으로 밀려남 (간격 포함)
        Vector2 currentStart = currentImageRect.anchoredPosition;
        Vector2 currentEnd = new Vector2(direction * (screenW + slideImageGap) + imageBasePos.x, baseY);

        // 다음 이미지: 반대편에서 중앙으로 (간격 포함)
        Vector2 nextStart = (nextImageRect != null && nextFullscreenImage.enabled)
            ? nextImageRect.anchoredPosition
            : new Vector2(-direction * (screenW + slideImageGap) + imageBasePos.x, baseY);
        Vector2 nextEnd = imageBasePos;

        // targetIndex == -1은 PlaceInfo 페이지 (이미지 숨김)
        bool showNext = targetIndex >= 0 && targetIndex < imageSprites.Count;

        if (showNext && nextFullscreenImage != null)
        {
            nextFullscreenImage.sprite = imageSprites[targetIndex];
            nextFullscreenImage.enabled = true;
            if (nextImageRect != null)
                nextImageRect.anchoredPosition = nextStart;
        }

        // 슬라이드 시작 시 nextImage 알파 = 드래그 중 페이드인된 값 유지 → slideDuration 동안 1로 보간
        float nextAlphaStart = (showNext && nextFullscreenImage != null) ? nextFullscreenImage.color.a : 1f;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);

            currentImageRect.anchoredPosition = Vector2.Lerp(currentStart, currentEnd, t);
            if (showNext && nextImageRect != null)
            {
                nextImageRect.anchoredPosition = Vector2.Lerp(nextStart, nextEnd, t);
                // 슬라이드 중 알파 보간 (현재값 → 1)
                float a = Mathf.Lerp(nextAlphaStart, 1f, t);
                nextFullscreenImage.color = new Color(1f, 1f, 1f, a);
            }

            yield return null;
        }

        // 슬라이드 완료
        if (showNext)
        {
            // nextImage가 중앙에 도착 — 위치 확정
            if (nextImageRect != null)
                nextImageRect.anchoredPosition = imageBasePos;

            // fullscreenImage를 새 스프라이트로 교체 (nextImage 뒤에 있어 보이지 않음)
            fullscreenImage.sprite = imageSprites[targetIndex];
            fullscreenImage.color = Color.white;
            fullscreenImage.enabled = true;
            currentImageRect.anchoredPosition = imageBasePos;

            // 한 프레임 대기 — fullscreenImage 렌더링 후 nextImage 숨김 (깜빡임 방지)
            yield return null;

            if (nextFullscreenImage != null)
            {
                nextFullscreenImage.enabled = false;
                if (nextImageRect != null)
                    nextImageRect.anchoredPosition = new Vector2(screenW + slideImageGap, baseY);
            }
        }
        else
        {
            // PlaceInfo 전환 — 화면 밖으로 완전히 나간 후 비활성화
            currentImageRect.anchoredPosition = currentEnd;
            yield return null;
            fullscreenImage.enabled = false;
            currentImageRect.anchoredPosition = imageBasePos;
        }

        imageTargetPos = imageBasePos;

        fullscreenImage.transform.SetAsFirstSibling();
        if (nextFullscreenImage != null)
            nextFullscreenImage.transform.SetAsFirstSibling();

        isSliding = false;
        dragDirection = 0;
    }

    private void ResetImagePosition()
    {
        imageTargetPos = imageBasePos;
        if (currentImageRect != null)
        {
            currentImageRect.anchoredPosition = imageBasePos;
        }
        if (nextFullscreenImage != null)
        {
            nextFullscreenImage.enabled = false;
            if (nextImageRect != null)
                nextImageRect.anchoredPosition = new Vector2(Screen.width + slideImageGap, imageBasePos.y);
        }
        dragDirection = 0;
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
        imagesAreCached = false;

        if (isFullscreen)
        {
            ShowImage(imageIndex);
        }
    }

    public void SetImageUrls(List<string> urls)
    {
        imageUrls = urls;
    }

    private void CloseFullscreen()
    {
        // 이미 FadeOut 진행 중이면 중복 실행 방지
        if (isFadingOut)
        {
            return;
        }


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
        isFadingOut = true;
        StartCoroutine(FadeOutCanvas(fadeDuration));
    }

    IEnumerator FadeInCanvas(float duration)
    {
        // FadeOut이 진행 중이었다면 취소 상태로 간주하고 isFadingOut 리셋
        isFadingOut = false;

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
        isFadingOut = false;

    }

    public int GetId()
    {
        return id;
    }
    public string GetUsername() => username;
    public string GetName() => placeName;

    public void ResetData()
    {
        // 진행 중인 이미지 로딩 코루틴 중단 (이전 장소 사진이 새 오브젝트에 표시되는 것 방지)
        StopAllCoroutines();

        // fullscreen 열려있으면 닫기
        if (isFullscreen)
        {
            isFullscreen = false;
            isFadingOut = false;
            if (fullscreenCanvasGroup != null)
                fullscreenCanvasGroup.gameObject.SetActive(false);
        }

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
        imageSprites.Clear();
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

        if (!isFullscreen)
        {
            return;
        }

        if (CommentManager.Instance != null && this.id != -1)
        {
            CommentManager.Instance.OpenCommentPanel(this.id, this.placeName);
        }
    }

    private IEnumerator FetchSubPhotosFromServer(int locationId)
    {
        int myId = locationId; // 요청 시점의 장소 ID 기록
        string url = $"{ApiConfig.MAIN_SERVER}/locations/{locationId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            // yield 후 장소가 바뀌었으면 무시 (풀 재활용)
            if (this.id != myId) yield break;

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
        int myId = this.id; // 로딩 시작 시점의 장소 ID 기록
        List<Sprite> newSprites = new List<Sprite>();

        foreach (string photoUrl in photoUrls)
        {
            string fullUrl = photoUrl.StartsWith("http") ? photoUrl : ApiConfig.MAIN_SERVER + "/" + photoUrl.Replace("\\", "/");

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();

                // 장소 ID가 바뀌었으면 풀 재활용으로 다른 장소가 된 것 → 즉시 종료
                if (this.id != myId)
                {
                    foreach (var s in newSprites)
                    {
                        if (s != null) { if (s.texture != null) Destroy(s.texture); Destroy(s); }
                    }
                    yield break;
                }

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

        // 최종 ID 체크
        if (this.id != myId) yield break;

        if (newSprites.Count > 0)
        {
            imageSprites = newSprites;
            imagesAreCached = false;

            if (isFullscreen && imageIndex >= 0)
            {
                ShowImage(imageIndex);
            }
        }
    }

}
