using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.InputSystem; // 추가
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;

public class CommentManager : MonoBehaviour
{
    public static CommentManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject commentPanel;
    public RectTransform panelRect;
    public InputField commentInputField;
    public Button sendButton;
    public Transform commentContent;
    public GameObject commentItemPrefab;
    public GameObject skeletonPrefab; // 스켈레톤 프리팹
    public Button closeButton;
    public Text titleText;
    public GameObject buttonSpinner; // 로딩 스피너

    [Header("Animation")]
    public float slideDuration = 0.3f;
    public CanvasGroup panelCanvasGroup;

    [Header("Heart Sprites")]
    [Tooltip("빈 하트 스프라이트 (좋아요 안 누른 상태)")]
    public Sprite unlikeSprite; // heart_unlike 스프라이트 연결
    [Tooltip("채워진 하트 스프라이트 (좋아요 누른 상태)")]
    public Sprite likedSprite; // heart_pink 또는 heart 스프라이트 연결

    [Header("Input Settings")]
    public int maxCommentLength = 500;

    [Header("Skeleton Settings")]
    [Tooltip("스켈레톤 아이템 높이 (실제 댓글과 유사하게)")]
    public float skeletonItemHeight = 160f;
    [Tooltip("스켈레톤 아바타 사이즈")]
    public float skeletonAvatarSize = 60f;
    [Tooltip("스켈레톤 이름 바 너비")]
    public float skeletonNameWidth = 150f;
    [Tooltip("스켈레톤 내용 바 너비")]
    public float skeletonContentWidth = 380f;

    private int currentLocationId = -1;
    public bool IsPanelOpen { get; private set; } = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        AutoConnectFields();

        if (commentPanel != null)
        {
            commentPanel.SetActive(false);
            if (panelRect != null)
                panelRect.anchoredPosition = new Vector2(0, -Screen.height);
        }

        // closeButton 자동 연결 (Inspector에서 연결되지 않은 경우 fallback)
        if (closeButton == null)
        {
            Transform found = transform.Find("CloseButton");
            if (found == null) found = transform.Find("closeButton");
            if (found == null) found = transform.Find("Close");
            if (found != null) closeButton = found.GetComponent<Button>();
        }

        if (sendButton != null) sendButton.onClick.AddListener(PostComment);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);

        if (commentInputField != null)
        {
            // ★ textComponent/placeholder 교차 참조 수정 (씬 직렬화 오류 방지)
            RepairInputFieldReferences(commentInputField);

            // 디버그: 수정 후 상태 확인
            Debug.Log($"[WP-DBG] CommentInput after repair: " +
                $"textComp={(commentInputField.textComponent != null ? commentInputField.textComponent.gameObject.name : "null")} " +
                $"placeholder={(commentInputField.placeholder != null ? commentInputField.placeholder.gameObject.name : "null")} " +
                $"same={(commentInputField.textComponent != null && commentInputField.placeholder != null && commentInputField.textComponent.gameObject == commentInputField.placeholder.gameObject)}");

            commentInputField.onValueChanged.AddListener(OnInputValueChanged);
            commentInputField.characterLimit = maxCommentLength;
            AutoExpandInputField.Setup(commentInputField, 0f, 300f);
            commentInputField.shouldHideMobileInput = true;

            // 로컬라이즈된 placeholder 설정
            ApplyLocalizedPlaceholder();
        }

        // commentContent에 VerticalLayoutGroup + ContentSizeFitter 보장 (댓글 겹침 방지)
        EnsureContentLayout();

        // Add Swipe to Close capability — HandleArea에서만 닫기 스와이프 반응
        if (commentPanel != null)
        {
            SwipeToClose swipeHandler = commentPanel.GetComponent<SwipeToClose>();
            if (swipeHandler == null) swipeHandler = commentPanel.AddComponent<SwipeToClose>();

            swipeHandler.panelRect = panelRect;
            swipeHandler.onClose = ClosePanel;
            swipeHandler.enableSwipeUpExpand = true;
            swipeHandler.onExpand = ExpandPanelWithoutKeyboard;

            // HandleArea를 찾아서 handleBar로 설정
            // HandleArea 자체(부모)를 설정해야 함 — 자식 Handle 핸들바 오브젝트가 아니라
            Transform handleArea = FindInChildren(commentPanel.transform, "HandleArea");
            if (handleArea == null) handleArea = FindInChildren(commentPanel.transform, "HandleBar");
            // HandleArea가 없으면 Handle의 부모를 사용
            if (handleArea == null)
            {
                Transform handle = FindInChildren(commentPanel.transform, "Handle");
                if (handle != null) handleArea = handle.parent;
            }

            // 추가 스와이프 영역: 타이틀("댓글") 텍스트 포함
            System.Collections.Generic.List<RectTransform> extraAreas = new System.Collections.Generic.List<RectTransform>();

            if (handleArea != null)
            {
                swipeHandler.handleBar = handleArea.GetComponent<RectTransform>();
                Debug.Log($"[WP-DBG] SwipeToClose handleBar set to: {handleArea.name}");

                // HandleArea의 부모(헤더 전체 영역)도 포함 — 타이틀 텍스트 터치 포함
                if (handleArea.parent != null && handleArea.parent != commentPanel.transform)
                {
                    RectTransform headerRect = handleArea.parent.GetComponent<RectTransform>();
                    if (headerRect != null) extraAreas.Add(headerRect);
                }
            }
            else
            {
                Debug.LogWarning("[WP-DBG] SwipeToClose: HandleArea not found, entire panel will respond to swipe");
            }

            // 타이틀 텍스트 오브젝트 직접 탐색해서 추가
            Transform titleObj = FindInChildren(commentPanel.transform, "TitleText");
            if (titleObj == null) titleObj = FindInChildren(commentPanel.transform, "Title");
            if (titleObj == null) titleObj = FindInChildren(commentPanel.transform, "Text_Title");
            if (titleObj != null)
            {
                RectTransform titleRect = titleObj.GetComponent<RectTransform>();
                if (titleRect != null && !extraAreas.Contains(titleRect))
                    extraAreas.Add(titleRect);
            }

            if (extraAreas.Count > 0)
                swipeHandler.additionalSwipeAreas = extraAreas.ToArray();
        }

        // MobileKeyboardHandler 자동 설정
        SetupMobileKeyboardHandler();
    }

    /// <summary>
    /// Inspector에서 연결되지 않은 필드를 런타임에 자동 연결 (fallback)
    /// </summary>
    private void AutoConnectFields()
    {
        // CommentPanel_HQ 찾기
        if (commentPanel == null)
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                Transform found = FindInChildren(root.transform, "CommentPanel_HQ");
                if (found != null)
                {
                    commentPanel = found.gameObject;
                    break;
                }
            }
            // Canvas 하위에서도 찾기
            if (commentPanel == null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform found = FindInChildren(canvas.transform, "CommentPanel_HQ");
                    if (found != null) commentPanel = found.gameObject;
                }
            }
        }

        if (commentPanel == null) return;

        // panelRect (Sheet 또는 CommentPanel_HQ 자체)
        if (panelRect == null)
        {
            Transform sheet = commentPanel.transform.Find("Sheet");
            panelRect = sheet != null ? sheet.GetComponent<RectTransform>() : commentPanel.GetComponent<RectTransform>();
        }

        // panelCanvasGroup
        if (panelCanvasGroup == null)
            panelCanvasGroup = commentPanel.GetComponentInChildren<CanvasGroup>(true);

        // commentInputField
        if (commentInputField == null)
            commentInputField = commentPanel.GetComponentInChildren<InputField>(true);

        // commentContent (Scroll View > Viewport > Content)
        if (commentContent == null)
        {
            Transform content = FindInChildren(commentPanel.transform, "Content");
            if (content != null) commentContent = content;
        }

        // sendButton (InputArea 하위 Button)
        if (sendButton == null)
        {
            Transform inputArea = FindInChildren(commentPanel.transform, "InputArea");
            if (inputArea != null)
            {
                Button[] btns = inputArea.GetComponentsInChildren<Button>(true);
                foreach (var btn in btns)
                {
                    if (btn.GetComponent<InputField>() == null)
                    {
                        sendButton = btn;
                        break;
                    }
                }
            }
        }

        // closeButton (Header 하위 Button)
        if (closeButton == null)
        {
            Transform header = FindInChildren(commentPanel.transform, "Header");
            if (header != null)
                closeButton = header.GetComponentInChildren<Button>(true);
        }

        // titleText (Header 하위 Text)
        if (titleText == null)
        {
            Transform header = FindInChildren(commentPanel.transform, "Header");
            if (header != null)
                titleText = header.GetComponentInChildren<Text>(true);
        }
    }

    private Transform FindInChildren(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindInChildren(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// commentContent에 VerticalLayoutGroup + ContentSizeFitter가 없으면 추가.
    /// 댓글 아이템들이 겹치지 않고 올바르게 배치되도록 보장.
    /// </summary>
    private void EnsureContentLayout()
    {
        if (commentContent == null) return;

        // VerticalLayoutGroup 확인/추가
        VerticalLayoutGroup vlg = commentContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = commentContent.gameObject.AddComponent<VerticalLayoutGroup>();
            Debug.Log("[WP-DBG] Added VerticalLayoutGroup to commentContent");
        }
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 12f; // 댓글 간 간격
        vlg.padding = new RectOffset(0, 0, 8, 8);

        // ContentSizeFitter 확인/추가
        ContentSizeFitter csf = commentContent.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = commentContent.gameObject.AddComponent<ContentSizeFitter>();
            Debug.Log("[WP-DBG] Added ContentSizeFitter to commentContent");
        }
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    /// <summary>
    /// InputField의 textComponent/placeholder 교차 참조 수정.
    /// 씬 직렬화 오류로 둘이 같은 오브젝트를 가리킬 수 있음.
    /// </summary>
    private void RepairInputFieldReferences(InputField input)
    {
        if (input == null) return;

        bool crossLinked = input.textComponent != null && input.placeholder != null
                           && input.textComponent.gameObject == input.placeholder.gameObject;

        if (input.textComponent == null || crossLinked)
        {
            // "Text" 또는 "Text (Legacy)" 자식에서 Text 컴포넌트 탐색
            Transform textTr = input.transform.Find("Text");
            if (textTr == null) textTr = input.transform.Find("Text (Legacy)");

            if (textTr != null)
            {
                Text textComp = textTr.GetComponent<Text>();
                if (textComp != null)
                {
                    input.textComponent = textComp;
                    Debug.Log($"[WP-DBG] RepairInputField: textComponent → {textTr.name}");
                }
            }
            else
            {
                // 이름으로 못 찾으면 placeholder가 아닌 첫 번째 Text 자식 사용
                Graphic placeholderGraphic = input.placeholder;
                Text[] texts = input.GetComponentsInChildren<Text>(true);
                foreach (var t in texts)
                {
                    if (placeholderGraphic != null && t.gameObject == placeholderGraphic.gameObject) continue;
                    if (t.gameObject == input.gameObject) continue;
                    input.textComponent = t;
                    Debug.Log($"[WP-DBG] RepairInputField: textComponent → {t.gameObject.name} (fallback)");
                    break;
                }
            }
        }

        if (input.placeholder == null || crossLinked)
        {
            // 이름으로 찾기 (다양한 이름 시도)
            Transform placeholderTr = input.transform.Find("Placeholder");
            if (placeholderTr == null) placeholderTr = input.transform.Find("placeholder");
            if (placeholderTr == null) placeholderTr = input.transform.Find("PlaceholderText");

            if (placeholderTr != null)
            {
                Graphic graphic = placeholderTr.GetComponent<Graphic>();
                if (graphic != null)
                {
                    input.placeholder = graphic;
                    Debug.Log($"[WP-DBG] RepairInputField: placeholder → {placeholderTr.name}");
                }
            }
            else
            {
                // 이름으로 못 찾으면 textComponent가 아닌 다른 Text 자식 사용
                Text[] texts = input.GetComponentsInChildren<Text>(true);
                foreach (var t in texts)
                {
                    if (t == input.textComponent) continue;
                    if (t.gameObject == input.gameObject) continue;
                    input.placeholder = t;
                    Debug.Log($"[WP-DBG] RepairInputField: placeholder → {t.gameObject.name} (fallback)");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 댓글 입력창에 디바이스 언어에 맞는 placeholder 텍스트 적용
    /// </summary>
    private void ApplyLocalizedPlaceholder()
    {
        if (commentInputField == null || commentInputField.placeholder == null) return;

        Text placeholderText = commentInputField.placeholder.GetComponent<Text>();
        if (placeholderText == null) return;

        // LocalizationManager가 있으면 사용, 없으면 fallback
        string localizedText = null;
        if (LocalizationManager.Instance != null)
            localizedText = LocalizationManager.Instance.GetText("comment_placeholder");

        if (string.IsNullOrEmpty(localizedText))
            localizedText = "댓글 입력...";

        placeholderText.text = localizedText;
    }

    void Update()
    {
        // Android Back Button support (New Input System)
        if (IsPanelOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClosePanel();
        }
    }

    private void OnInputValueChanged(string text)
    {
        if (sendButton != null)
        {
            Image btnImg = sendButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = string.IsNullOrEmpty(text) ? Color.gray : new Color(0, 0.58f, 0.96f);
            }
            sendButton.interactable = !string.IsNullOrEmpty(text);
        }
    }

    private void GenerateMockComments()
    {
        // 반복 중 수정 방지를 위해 리스트에 먼저 수집
        var children = new List<GameObject>();
        foreach (Transform child in commentContent)
            children.Add(child.gameObject);
        foreach (var child in children)
            Destroy(child);

        for (int i = 0; i < 4; i++)
        {
            CommentData mock = new CommentData
            {
                id = i,
                username = $"User_{i}",
                content = i % 2 == 0 ? "정말 멋진 장소네요!" : "다음에 꼭 가보고 싶어요. 정보 감사합니다. 긴 글 테스트 긴 글 테스트 긴 글 테스트",
                created_at = System.DateTime.Now.AddHours(-i * 5).ToString(),
                like_count = i * 10,
                is_liked = false
            };
            CreateCommentItem(mock);
        }
    }

    private void CreateCommentItem(CommentData data)
    {
        if (commentItemPrefab == null)
        {
            Debug.LogError("[CommentManager] CreateCommentItem - commentItemPrefab이 null입니다! 인스펙터에서 연결 필요");
            return;
        }

        GameObject itemObj = Instantiate(commentItemPrefab, commentContent);
        itemObj.SetActive(true); // 템플릿이 꺼져있으므로 켜줌

        CommentItem itemScript = itemObj.GetComponent<CommentItem>();
        if (itemScript != null)
        {
            // 하트 스프라이트 주입 (프리팹에 설정되어 있지 않으면 CommentManager에서 연결)
            if (itemScript.likeIcon == null && unlikeSprite != null)
                itemScript.likeIcon = unlikeSprite;
            if (itemScript.likedSprite == null && likedSprite != null)
                itemScript.likedSprite = likedSprite;

            itemScript.Setup(data);

            // 내 댓글이면 SwipeToDeleteHandler 부착 (MessagePanel과 동일한 오버레이 패턴)
            if (itemScript.IsMyComment)
            {
                SetupCommentSwipeDelete(itemObj, itemScript);
            }
        }
        else
        {
            Debug.LogError($"[CommentManager] CreateCommentItem - CommentItem 컴포넌트가 없습니다! 프리팹: {commentItemPrefab.name}");
        }

        // 부모(commentContent) 레이아웃도 재계산 — 새 아이템 높이 반영
        LayoutRebuilder.ForceRebuildLayoutImmediate(commentContent as RectTransform);
    }

    /// <summary>
    /// 댓글 아이템에 SwipeToDeleteHandler 부착 (MessagePanel과 동일한 패턴)
    /// </summary>
    private void SetupCommentSwipeDelete(GameObject itemObj, CommentItem itemScript)
    {
        SwipeToDeleteHandler handler = itemObj.GetComponent<SwipeToDeleteHandler>();
        if (handler == null)
            handler = itemObj.AddComponent<SwipeToDeleteHandler>();

        string userId = "";
        if (LoginManager.Instance != null && LoginManager.Instance.CurrentUser != null)
            userId = LoginManager.Instance.CurrentUser.id;

        handler.Initialize(userId, 80f, () =>
        {
            itemScript.DeleteThisComment();
        });
    }

    public void OpenCommentPanel(int locationId, string objectName = null)
    {
        currentLocationId = locationId;
        if (commentPanel != null)
        {
            Debug.Log($"[WP-DBG] OpenCommentPanel: panelRect={panelRect?.gameObject.name} anchoredPos={panelRect?.anchoredPosition} " +
                      $"anchMin={panelRect?.anchorMin} anchMax={panelRect?.anchorMax} offMin={panelRect?.offsetMin} offMax={panelRect?.offsetMax}");

            // MKH backgroundRect 확인
            MobileKeyboardHandler mkhComp = commentPanel.GetComponent<MobileKeyboardHandler>();
            if (mkhComp != null)
            {
                Debug.Log($"[WP-DBG] MKH bgRect={(mkhComp.backgroundRect != null ? mkhComp.backgroundRect.gameObject.name : "null")} " +
                          $"isSameAsPanelRect={mkhComp.backgroundRect == panelRect} " +
                          $"expandOnKb={mkhComp.expandPanelOnKeyboard}");
            }

            // MKH를 슬라이드 애니메이션 동안 비활성화
            // → 패널이 화면 밖(-2532)에 있을 때 원본값을 캡처하면 AnimatePanel이 패널을 다시 밖으로 밀어냄
            MobileKeyboardHandler mkhToDefer = commentPanel.GetComponent<MobileKeyboardHandler>();
            if (mkhToDefer != null) mkhToDefer.enabled = false;

            // ★ CommentPanel을 항상 최상위 형제로 이동 — FullScreenPanel보다 위에 렌더되도록
            commentPanel.transform.SetAsLastSibling();
            commentPanel.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(SlidePanelThenEnableMKH(mkhToDefer));

            // SwipeToClose 쿨다운 시작 — 오픈 스와이프가 닫기로 오인되는 것 방지
            SwipeToClose swipe = commentPanel.GetComponent<SwipeToClose>();
            if (swipe != null) swipe.NotifyOpened();
            
            // Placeholder 업데이트 (object name이 있으면 커스텀, 없으면 기본 로컬라이즈)
            if (commentInputField != null && commentInputField.placeholder != null)
            {
                Text placeText = commentInputField.placeholder.GetComponent<Text>();
                if (placeText != null)
                {
                    if (!string.IsNullOrEmpty(objectName))
                        placeText.text = $"{objectName}에 댓글 달기...";
                    else
                        ApplyLocalizedPlaceholder();
                }
            }
            
            string currentUserId = (LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn) 
                                   ? LoginManager.Instance.CurrentUser.id 
                                   : "";
            
            StartCoroutine(FetchComments(locationId, currentUserId));
        }
        IsPanelOpen = true;
    }

    /// <summary>
    /// MobileKeyboardHandler를 댓글 패널에 자동 설정
    /// </summary>
    private void SetupMobileKeyboardHandler()
    {
        if (commentPanel == null) return;

        MobileKeyboardHandler handler = commentPanel.GetComponent<MobileKeyboardHandler>();
        if (handler == null)
            handler = commentPanel.AddComponent<MobileKeyboardHandler>();

        // InputArea 찾기
        Transform inputAreaTr = commentPanel.transform.Find("InputArea");
        if (inputAreaTr == null)
        {
            // 직접 InputField의 부모를 InputArea로 사용
            if (commentInputField != null)
                inputAreaTr = commentInputField.transform.parent;
        }
        if (inputAreaTr != null)
            handler.inputAreaRect = inputAreaTr.GetComponent<RectTransform>();

        // ScrollRect 연결
        if (commentContent != null)
        {
            ScrollRect sr = commentContent.GetComponentInParent<ScrollRect>();
            if (sr != null)
            {
                handler.chatScrollRect = sr;
                handler.scrollViewRect = sr.GetComponent<RectTransform>();
            }
        }

        // 키보드 닫기 대상 InputField 연결
        handler.targetInputField = commentInputField;
    }

    /// <summary>
    /// 핸들바 위로 스와이프 시 키보드 없이 패널 전체 확장
    /// </summary>
    public void ExpandPanelWithoutKeyboard()
    {
        MobileKeyboardHandler mkh = commentPanel != null ? commentPanel.GetComponent<MobileKeyboardHandler>() : null;
        if (mkh != null)
        {
            mkh.DismissKeyboardOnly(keepExpanded: true);
        }
        Debug.Log("[WP-DBG] ExpandPanelWithoutKeyboard called");
    }

    public void ClosePanel()
    {
        Debug.Log($"[WP-DBG] ClosePanel() called, IsPanelOpen={IsPanelOpen}\n{System.Environment.StackTrace}");
        if (IsPanelOpen)
        {
            // MKH 먼저 비활성화 — 닫기 슬라이드 중 offset 충돌 방지
            MobileKeyboardHandler mkh = commentPanel.GetComponent<MobileKeyboardHandler>();
            if (mkh != null) mkh.enabled = false;

            StopAllCoroutines();
            StartCoroutine(SlidePanel(false));
            IsPanelOpen = false;
        }
    }

    /// <summary>
    /// 슬라이드 완료 후 MKH 활성화 — MKH가 최종 위치 기준으로 원본값을 캡처하도록
    /// </summary>
    private IEnumerator SlidePanelThenEnableMKH(MobileKeyboardHandler mkhToEnable)
    {
        yield return StartCoroutine(SlidePanel(true));

        // 슬라이드 완료 → MKH 활성화 (이때 offset이 올바른 값)
        if (mkhToEnable != null)
        {
            mkhToEnable.enabled = true;
            Debug.Log($"[WP-DBG] MKH re-enabled after slide, bgRect offset=({panelRect.offsetMin}, {panelRect.offsetMax})");
        }
    }

    private IEnumerator SlidePanel(bool open)
    {
        float timer = 0f;
        Vector2 startPos = panelRect.anchoredPosition;
        Vector2 targetPos = open ? Vector2.zero : new Vector2(0, -Screen.height);

        float startAlpha = panelCanvasGroup.alpha;
        float targetAlpha = open ? 1f : 0f;

        Debug.Log($"[WP-DBG] SlidePanel START open={open} startPos={startPos} targetPos={targetPos} duration={slideDuration}");

        while (timer < slideDuration)
        {
            timer += Time.deltaTime;
            float t = timer / slideDuration;
            t = t * t * (3f - 2f * t);

            panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            yield return null;
        }

        panelRect.anchoredPosition = targetPos;
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = targetAlpha;

        Debug.Log($"[WP-DBG] SlidePanel DONE open={open} finalPos={panelRect.anchoredPosition}");

        if (!open) commentPanel.SetActive(false);
    }

    private IEnumerator FetchComments(int locationId, string currentUserId)
    {
        // commentContent null 체크
        if (commentContent == null)
        {
            Debug.LogError("[CommentManager] commentContent가 null입니다! 인스펙터에서 할당 필요");
            yield break;
        }

        // Clear existing comments (Real & Skeleton) - 반복 중 수정 방지
        var existingChildren = new List<GameObject>();
        foreach (Transform child in commentContent)
            existingChildren.Add(child.gameObject);
        foreach (var child in existingChildren)
            Destroy(child);

        ShowSkeleton(); // 로딩 시작 시 스켈레톤 표시
        float skeletonStartTime = Time.time;

        string url = $"{ApiConfig.MAIN_SERVER}/comments?location_id={locationId}&user_id={currentUserId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            // 최소 0.8초 스켈레톤 표시 (너무 빨리 사라지지 않도록)
            float elapsed = Time.time - skeletonStartTime;
            if (elapsed < 0.8f)
                yield return new WaitForSeconds(0.8f - elapsed);

            HideSkeleton(); // 로딩 완료 시 제거

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                List<CommentData> comments = ParseComments(json);

                foreach (var comment in comments)
                {
                    CreateCommentItem(comment);
                }

                // 모든 댓글 로드 후 전체 레이아웃 재계산
                StartCoroutine(RebuildContentLayoutDelayed());
            }
            else
            {
                Debug.LogError($"[CommentManager] Failed to fetch comments: {request.error}");
            }
        }
    }

    private void ShowSkeleton()
    {
        if (commentContent == null) return;

        // skeletonPrefab이 있으면 프리팹 사용, 없으면 동적 생성
        int count = 4;
        for (int i = 0; i < count; i++)
        {
            GameObject skel;
            if (skeletonPrefab != null)
            {
                skel = Instantiate(skeletonPrefab, commentContent);
            }
            else
            {
                skel = CreateCommentSkeletonItem(commentContent);
            }
            skel.name = "SkeletonItem";
            skel.SetActive(true);
        }
    }

    private GameObject CreateCommentSkeletonItem(Transform parent)
    {
        Color bgColor = new Color(0.15f, 0.15f, 0.18f, 1f);
        Color contentColor = new Color(0.22f, 0.22f, 0.26f, 1f);
        float height = skeletonItemHeight > 0 ? skeletonItemHeight : 140f;
        float avatarSz = skeletonAvatarSize > 0 ? skeletonAvatarSize : 50f;
        float nameW = skeletonNameWidth > 0 ? skeletonNameWidth : 120f;
        float contentW = skeletonContentWidth > 0 ? skeletonContentWidth : 300f;

        GameObject item = new GameObject("SkeletonItem");
        item.transform.SetParent(parent, false);

        RectTransform itemRect = item.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0, height);

        LayoutElement le = item.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        Image itemBg = item.AddComponent<Image>();
        itemBg.color = bgColor;

        // 구분선 (하단에 얇은 라인) — 스켈레톤 간 시각적 구분
        GameObject divider = new GameObject("Divider");
        divider.transform.SetParent(item.transform, false);
        RectTransform divRect = divider.AddComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0.05f, 0f);
        divRect.anchorMax = new Vector2(0.95f, 0f);
        divRect.pivot = new Vector2(0.5f, 0f);
        divRect.sizeDelta = new Vector2(0, 1f);
        divRect.anchoredPosition = Vector2.zero;
        Image divImg = divider.AddComponent<Image>();
        divImg.color = new Color(0.3f, 0.3f, 0.35f, 0.5f);

        float avatarX = 20f;
        float textStartX = avatarX + avatarSz + 16f;

        // 아바타 (둥근 원) — 상단에 배치
        CreateSkeletonBlock(item.transform, "Avatar",
            new Vector2(avatarX, height * 0.2f), new Vector2(avatarSz, avatarSz), contentColor);

        // 유저명 바
        CreateSkeletonBlock(item.transform, "NameLine",
            new Vector2(textStartX, height * 0.22f), new Vector2(nameW, 16f), contentColor);

        // 댓글 내용 바 1 (긴 줄)
        CreateSkeletonBlock(item.transform, "ContentLine1",
            new Vector2(textStartX, -2f), new Vector2(contentW, 14f),
            new Color(contentColor.r, contentColor.g, contentColor.b, 0.7f));

        // 댓글 내용 바 2 (짧은 줄)
        CreateSkeletonBlock(item.transform, "ContentLine2",
            new Vector2(textStartX, -22f), new Vector2(contentW * 0.6f, 14f),
            new Color(contentColor.r, contentColor.g, contentColor.b, 0.4f));

        // 쉬머 효과
        item.AddComponent<ShimmerEffect>();

        return item;
    }

    private static void CreateSkeletonBlock(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        GameObject block = new GameObject(name);
        block.transform.SetParent(parent, false);

        RectTransform rect = block.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = block.AddComponent<Image>();
        img.color = color;
    }

    private void HideSkeleton()
    {
        // 반복 중 수정 방지를 위해 삭제할 스켈레톤 먼저 수집
        var skeletonsToRemove = new List<GameObject>();
        foreach (Transform child in commentContent)
        {
            if (child.name.Contains("Skeleton"))
                skeletonsToRemove.Add(child.gameObject);
        }
        foreach (var skel in skeletonsToRemove)
            Destroy(skel);
    }

    /// <summary>
    /// 댓글 로드/추가 후 전체 Content 레이아웃을 지연 재계산.
    /// ContentSizeFitter + VLG 전파를 보장하기 위해 수 프레임 대기.
    /// </summary>
    private IEnumerator RebuildContentLayoutDelayed()
    {
        yield return null;
        yield return null;

        if (commentContent != null)
        {
            Canvas.ForceUpdateCanvases();

            // 각 CommentItem의 ContentSizeFitter 재계산
            var fitters = commentContent.GetComponentsInChildren<ContentSizeFitter>(true);
            foreach (var fitter in fitters)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(fitter.GetComponent<RectTransform>());
            }

            // commentContent 전체 재계산
            LayoutRebuilder.ForceRebuildLayoutImmediate(commentContent as RectTransform);
        }
    }

    public void PostComment()
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup();
            return;
        }

        if (string.IsNullOrEmpty(commentInputField.text)) return;

        string content = commentInputField.text.Trim();
        if (string.IsNullOrEmpty(content)) return;

        // 입력 초기화
        commentInputField.text = "";

        // 키보드만 닫고 패널 확장 상태 유지
        MobileKeyboardHandler mkh = commentPanel.GetComponent<MobileKeyboardHandler>();
        if (mkh != null)
        {
            mkh.DismissKeyboardOnly(keepExpanded: true);
        }

        // 입력필드 포커스 해제 (키보드 닫기)
        if (commentInputField != null)
        {
            commentInputField.DeactivateInputField();
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        // 선처리: 즉시 댓글 아이템 표시
        GameObject optimisticItem = AddOptimisticComment(content);

        StartCoroutine(PostCommentCoroutine(content, optimisticItem));
    }

    /// <summary>
    /// 선처리: 즉시 댓글 아이템을 화면에 표시 (서버 응답 전)
    /// </summary>
    private GameObject AddOptimisticComment(string content)
    {
        if (commentItemPrefab == null || commentContent == null) return null;

        string userId = LoginManager.Instance?.CurrentUser?.id ?? "";
        string username = LoginManager.Instance?.CurrentUser?.username ?? "";

        CommentData tempData = new CommentData
        {
            id = -1, // 임시 ID
            user_id = userId,
            username = username,
            content = content,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            like_count = 0,
            is_liked = false
        };

        // 댓글 아이템 생성
        GameObject itemObj = Instantiate(commentItemPrefab, commentContent);
        itemObj.SetActive(true);

        CommentItem itemScript = itemObj.GetComponent<CommentItem>();
        if (itemScript != null)
        {
            if (itemScript.likeIcon == null && unlikeSprite != null)
                itemScript.likeIcon = unlikeSprite;
            if (itemScript.likedSprite == null && likedSprite != null)
                itemScript.likedSprite = likedSprite;

            itemScript.Setup(tempData);
        }

        // 스크롤 최하단으로
        if (commentContent != null)
        {
            Canvas.ForceUpdateCanvases();
            ScrollRect sr = commentContent.GetComponentInParent<ScrollRect>();
            if (sr != null)
                sr.normalizedPosition = Vector2.zero;
        }

        return itemObj;
    }

    private IEnumerator PostCommentCoroutine(string content, GameObject optimisticItem = null)
    {
        string url = $"{ApiConfig.MAIN_SERVER}/comments";

        CommentPostData postData = new CommentPostData
        {
            location_id = currentLocationId,
            user_id = LoginManager.Instance.CurrentUser.id,
            username = LoginManager.Instance.CurrentUser.username,
            content = content
        };

        string json = JsonUtility.ToJson(postData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 선처리 아이템이 이미 표시되어 있으므로 전체 새로고침 불필요
            }
            else
            {
                Debug.LogError($"[CommentManager] PostComment failed: {request.error}");

                // 전송 실패: 선처리 아이템 제거
                if (optimisticItem != null)
                    Destroy(optimisticItem);
            }
        }
    }

    private IEnumerator SpinButton()
    {
        if (buttonSpinner == null) yield break;
        RectTransform rect = buttonSpinner.GetComponent<RectTransform>();
        while (true)
        {
            rect.Rotate(0, 0, -360 * Time.deltaTime); // 1초에 한바퀴
            yield return null;
        }
    }

    private List<CommentData> ParseComments(string json)
    {
        string wrappedJson = "{\"items\":" + json + "}";
        return JsonUtility.FromJson<CommentListWrapper>(wrappedJson).items;
    }

    public void ToggleLocationLike(int locationId, System.Action<int, bool> callback)
    {
        if (LoginManager.Instance == null || !LoginManager.Instance.IsLoggedIn)
        {
            if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup();
            return;
        }
        StartCoroutine(ToggleLocationLikeCoroutine(locationId, callback));
    }

    public void GetBestComment(int locationId, System.Action<CommentData> callback)
    {
        // 로그인 여부 상관없이 댓글 조회 가능
        string currentUserId = (LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn)
                                ? LoginManager.Instance.CurrentUser.id
                                : "";
        StartCoroutine(FetchBestCommentCoroutine(locationId, currentUserId, callback));
    }

    private IEnumerator FetchBestCommentCoroutine(int locationId, string userId, System.Action<CommentData> callback)
    {
        string url = $"{ApiConfig.MAIN_SERVER}/comments?location_id={locationId}&user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                List<CommentData> comments = ParseComments(json);

                if (comments != null && comments.Count > 0)
                {
                    // 좋아요 순 내림차순, 그 다음 최신순
                    comments.Sort((a, b) => {
                        int likeCompare = b.like_count.CompareTo(a.like_count);
                        if (likeCompare != 0) return likeCompare;
                        return string.Compare(b.created_at, a.created_at);
                    });
                    callback?.Invoke(comments[0]);
                }
                else
                {
                    callback?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"[CommentManager] FetchBestComment failed: {request.error}");
                callback?.Invoke(null);
            }
        }
    }

    private IEnumerator ToggleLocationLikeCoroutine(int locationId, System.Action<int, bool> callback)
    {
        string url = $"{ApiConfig.MAIN_SERVER}/locations/like";
        LikePostData data = new LikePostData { location_id = locationId, user_id = LoginManager.Instance.CurrentUser.id };
        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<LocationLikeResponse>(request.downloadHandler.text);
                callback?.Invoke(response.total_likes, response.action == "liked");
            }
        }
    }

    void OnDestroy()
    {
        // 메모리 누수 방지: 이벤트 리스너 정리
        if (sendButton != null) sendButton.onClick.RemoveListener(PostComment);
        if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
        if (commentInputField != null) commentInputField.onValueChanged.RemoveListener(OnInputValueChanged);
    }
}

[System.Serializable]
public class CommentData
{
    public int id;
    public string user_id;
    public string username;
    public string content;
    public string created_at;
    public int like_count;
    public bool is_liked;
}

[System.Serializable]
public class CommentListWrapper
{
    public List<CommentData> items;
}

[System.Serializable]
public class CommentPostData
{
    public int location_id;
    public string user_id;
    public string username;
    public string content;
}

[System.Serializable]
public class LikePostData
{
    public int location_id;
    public int comment_id;
    public string user_id;
}

[System.Serializable]
public class LocationLikeResponse
{
    public string action;
    public int total_likes;
}