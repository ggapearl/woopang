using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 첫 실행 가이드 — 스와이프 방식 온보딩
/// 기존 guidePages(01~06) 이미지를 좌우 스와이프로 전환
/// 하단 도트 인디케이터 + 페이지별 페이드/슬라이드 애니메이션
/// </summary>
public class FirstTimeGuide : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private GameObject[] guidePages;   // 6개 페이지 (01~06)
    [SerializeField] private Text guideText;
    [SerializeField] private Button nextButton;         // 기존 right — 비활성 처리
    [SerializeField] private Button previousButton;     // 기존 left — 비활성 처리
    [SerializeField] private Button confirmButton;      // 기존 check — 마지막 페이지에서만

    [Header("배경 이미지 (모든 페이지 공통)")]
    [Tooltip("guidePanel 전체에 깔리는 백그라운드 (예: back.png) — 가이드 진입 시 자동 페이드인")]
    [SerializeField] private Image backgroundImage;

    [Header("페이지별 강조 이미지 (살짝 커졌다 작아졌다 반짝)")]
    [Tooltip("페이지마다 펄스(살짝 커졌다 작아졌다 + 알파 반짝) 시킬 이미지를 0개 이상 연결\n" +
             "예: 01,02,03 → 페이지 자체 이미지 / 04 → 비워둠 / 05 → 05-1만 / 06 → 06-1만\n" +
             "guidePages 인덱스와 일치시키며, 한 페이지에 여러 개 연결하려면 같은 페이지 인덱스로 여러 항목 추가")]
    [SerializeField] private PulseTarget[] pageHighlights;

    [Header("스와이프 설정")]
    [SerializeField] private float swipeThreshold = 80f;
    [SerializeField] private float snapDuration = 0.18f;

    [Header("애니메이션 설정")]
    [SerializeField] private float pageFadeDuration = 0.32f;
    [SerializeField] private float pageSlideOffset = 50f;
    [SerializeField] private float textFadeDelay = 0.05f;

    [Header("비주얼 설정")]
    [SerializeField] private float backgroundDimAlpha = 0.55f;    // 뒤 화면 디밍 강도
    [SerializeField] private float panelEntryDuration = 0.6f;     // 최초 진입 페이드인
    [SerializeField] private float imageParallaxFactor = 1.25f;   // 이미지가 텍스트보다 얼마나 빠르게 움직이는지
    [SerializeField] private float swipeHintDelay = 1.6f;         // 첫 페이지에서 힌트 표시 지연
    [SerializeField] private float swipeHintDuration = 2.6f;      // 힌트 반복 주기

    [Header("SwipeHint 위치 설정")]
    [Tooltip("SwipeHint anchor min — (0.5, 0)이면 하단 중앙")]
    [SerializeField] private Vector2 swipeHintAnchorMin = new Vector2(0.5f, 0f);
    [SerializeField] private Vector2 swipeHintAnchorMax = new Vector2(0.5f, 0f);
    [SerializeField] private Vector2 swipeHintPivot = new Vector2(0.5f, 0f);
    [Tooltip("SwipeHint 위치 — anchor 기준 오프셋 (px)")]
    [SerializeField] private Vector2 swipeHintAnchoredPosition = new Vector2(0f, 90f);
    [Tooltip("SwipeHint 크기 (px)")]
    [SerializeField] private Vector2 swipeHintSize = new Vector2(220f, 40f);
    [Tooltip("SwipeHint 텍스트")]
    [SerializeField] private string swipeHintText = "‹  swipe  ›";
    [Tooltip("SwipeHint 폰트 사이즈")]
    [SerializeField] private int swipeHintFontSize = 22;
    [Tooltip("SwipeHint 텍스트 색상")]
    [SerializeField] private Color swipeHintColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private float confirmPulseAmplitude = 0.1f;  // 확인 버튼 스케일 펄스
    [SerializeField] private float confirmGlowAmplitude = 0.45f;  // 확인 버튼 밝기/알파 반짝임 강도
    [SerializeField] private float confirmPulseSpeed = 3.0f;      // 확인 버튼 펄스 주기 속도

    [Header("Dot Indicator 설정")]
    [Tooltip("DotIndicator의 anchor — (0.5, 0)이면 하단 중앙")]
    [SerializeField] private Vector2 dotAnchorMin = new Vector2(0.5f, 0f);
    [SerializeField] private Vector2 dotAnchorMax = new Vector2(0.5f, 0f);
    [SerializeField] private Vector2 dotPivot = new Vector2(0.5f, 0f);
    [Tooltip("DotIndicator 위치 — anchor 기준 오프셋 (px). y 값을 키우면 더 위로 올라감")]
    [SerializeField] private Vector2 dotAnchoredPosition = new Vector2(0f, 40f);
    [Tooltip("도트 크기 (px)")]
    [SerializeField] private float dotSize = 16f;
    [Tooltip("도트 사이 간격 (px)")]
    [SerializeField] private float dotSpacing = 12f;
    [Tooltip("도트 사이 진행바(연결선) 두께 (px)")]
    [SerializeField] private float dotLineThickness = 2f;
    [Tooltip("진행바 배경 색상 + 알파")]
    [SerializeField] private Color dotLineBgColor = new Color(1f, 1f, 1f, 0.15f);
    [Tooltip("진행바 채워지는 부분 색상 + 알파")]
    [SerializeField] private Color dotLineFillColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("활성화된 도트의 스케일 배수 (1 = 그대로, 1.5 = 1.5배 큼)")]
    [SerializeField] private float dotActiveScale = 1.6f;
    [Tooltip("활성화된 도트의 위쪽 오프셋 (px). 키울수록 활성 도트가 위로 떠오름")]
    [SerializeField] private float dotActiveYOffset = 6f;

    /// <summary>
    /// 인스펙터에서 "어느 페이지의 어느 이미지를 펄스시킬지" 직접 연결
    /// </summary>
    [System.Serializable]
    public class PulseTarget
    {
        [Tooltip("guidePages 배열의 인덱스 (0=Page_01, 1=Page_02, ..., 4=Page_05, 5=Page_06)")]
        public int pageIndex;
        [Tooltip("살짝 커졌다 작아졌다 + 알파 반짝거릴 Image (예: 01,02,03 자체 / 05-1 / 06-1)")]
        public Graphic targetGraphic;
        [Tooltip("펄스 진폭 — 0.05f(은은) ~ 0.15f(강조). 비워두면 0.07f")]
        public float scaleAmplitude = 0.07f;
        [Tooltip("알파 반짝임 강도 — 0~1. 비워두면 0.35f")]
        public float glowAmplitude = 0.35f;
        [Tooltip("펄스 속도 — 1.5(느림) ~ 4.0(빠름). 비워두면 2.4f")]
        public float pulseSpeed = 2.4f;
    }

    private const string FIRST_TIME_KEY = "IsFirstTime";
    private int currentPage = 0;
    private int pageCount;
    private float delayBeforeGuide = 6f;

    // 전환 상태
    private bool isTransitioning = false;

    // 도트 인디케이터
    private Image[] dotImages;
    private GameObject dotContainer;
    private Color dotActive = new Color(1f, 1f, 1f, 1f);
    private Color dotInactive = new Color(1f, 1f, 1f, 0.35f);

    // 페이지별 CanvasGroup (애니메이션용)
    private CanvasGroup[] pageCanvasGroups;
    private CanvasGroup textCanvasGroup;
    private CanvasGroup panelCanvasGroup;

    // 각 페이지의 "정중앙 기준 위치" — Start 시 한 번만 캡처해서 모든 슬라이드/스냅의 기준점으로 사용
    // (드래그된 오프셋이 base로 굳어버리는 정렬 어긋남 버그 방지)
    private Vector2[] pageBasePositions;

    // 비주얼 확장 — 배경 디밍 / 스와이프 힌트 / 확인 버튼 펄스
    private Image backgroundDim;
    private GameObject swipeHintObj;
    private CanvasGroup swipeHintCG;
    private Coroutine swipeHintRoutine;
    private Coroutine confirmPulseRoutine;
    private Color? confirmButtonBaseColor; // 펄스 시작 시 한 번만 캡처해서 종료 시 복원용
    private Image dotProgressBar;
    private RectTransform dotProgressBarRect;

    // 페이지별 강조 이미지 펄스 — 페이지 인덱스별로 코루틴 묶어 관리
    private List<Coroutine> activePulseRoutines = new List<Coroutine>();

    // ============================================================
    // 언어별 안내 텍스트 (6단계)
    // ============================================================
    private Dictionary<string, string[]> guideTemplates = new Dictionary<string, string[]>
    {
        { "en", new string[] {
            "You can register your current location by pressing the '+' button at the top",
            "You can check nearby places by pressing the bottom-left button",
            "You can check messages by pressing the bottom-right button",
            "Follow the arrows that appear at the edges of the screen to discover AR models",
            "Touch the AR model objects to check information about that location",
            "Enjoy WOOPANG!" } },
        { "ko", new string[] {
            "상단 '+' 버튼을 눌러 현재 위치한 장소를 등록할 수 있어요",
            "좌측하단 버튼을 눌러 근처 장소를 확인할 수 있어요",
            "우측하단 버튼을 눌러 메세지를 확인할 수 있어요",
            "화면 모서리에 발생한 화살표를 따라가면 AR모형을 발견할 수 있어요",
            "AR모형의 오브젝트를 터치하여 해당 장소의 정보를 확인할 수 있어요",
            "즐거운 우팡하세요!" } },
        { "ja", new string[] {
            "上部の「+」ボタンを押して現在地を登録できます",
            "左下のボタンを押して近くの場所を確認できます",
            "右下のボタンを押してメッセージを確認できます",
            "画面の端に表示される矢印に従ってARモデルを発見できます",
            "ARモデルのオブジェクトをタッチしてその場所の情報を確認できます",
            "Woopangを楽しんでください！" } },
        { "zh", new string[] {
            "按顶部的\"+\"按钮可以注册当前位置",
            "按左下角按钮可以查看附近地点",
            "按右下角按钮可以查看消息",
            "跟随屏幕边缘出现的箭头可以发现AR模型",
            "触摸AR模型对象可以查看该地点的信息",
            "享受Woopang吧！" } },
        { "es", new string[] {
            "Puedes registrar tu ubicación actual presionando el botón '+' en la parte superior",
            "Puedes verificar lugares cercanos presionando el botón inferior izquierdo",
            "Puedes verificar mensajes presionando el botón inferior derecho",
            "Sigue las flechas que aparecen en los bordes de la pantalla para descubrir modelos AR",
            "Toca los objetos del modelo AR para verificar información sobre esa ubicación",
            "¡Disfruta de WOOPANG!" } }
    };

    // ============================================================
    // Lifecycle
    // ============================================================

    void Start()
    {
        if (guidePanel == null || guidePages == null || guidePages.Length == 0 || guideText == null)
        {
            Debug.LogError("[FirstTimeGuide] UI 요소가 연결되지 않았습니다!");
            return;
        }

        pageCount = guidePages.Length;

        // 기존 이전/다음 버튼 비활성
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (previousButton != null) previousButton.gameObject.SetActive(false);
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);
            confirmButton.gameObject.SetActive(false);
        }

        SetupSwipeArea();
        SetupCanvasGroups();
        SetupBackgroundDim();
        SetupBackgroundImage();
        CreateDotIndicator();
        CreateSwipeHint();

        StartCoroutine(StartGuideSequence());
    }

    // ============================================================
    // 배경 디밍 — 뒤 화면을 반투명 검정으로 가려 집중도 상승
    // ============================================================
    private void SetupBackgroundDim()
    {
        if (guidePanel == null) return;

        GameObject dimObj = new GameObject("BackgroundDim", typeof(RectTransform));
        dimObj.transform.SetParent(guidePanel.transform, false);
        dimObj.transform.SetAsFirstSibling(); // 가장 뒤에 배치

        RectTransform rt = dimObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-500, -500); // 화면 밖까지 여유있게 덮음
        rt.offsetMax = new Vector2(500, 500);

        backgroundDim = dimObj.AddComponent<Image>();
        backgroundDim.color = new Color(0, 0, 0, 0f); // 초기 투명 — 진입 시 페이드인
        backgroundDim.raycastTarget = false;
    }

    // ============================================================
    // 배경 이미지 — 모든 페이지 공통, 가이드 진입 시 함께 페이드인
    // ============================================================
    private void SetupBackgroundImage()
    {
        if (backgroundImage == null) return;

        // 가이드 진입 전엔 알파 0으로 시작 (PanelEntryAnimation의 panelCanvasGroup이 통째로 페이드 처리)
        backgroundImage.raycastTarget = false; // 스와이프 입력 막지 않음
        // 가장 뒤로 보내되, BackgroundDim이 backgroundImage 위에 깔려야 디밍이 보이므로
        // 순서: BackgroundImage(가장 뒤) → BackgroundDim(그 위) → 나머지
        backgroundImage.transform.SetAsFirstSibling();
    }

    // ============================================================
    // 초기 설정
    // ============================================================

    private void SetupSwipeArea()
    {
        // guidePanel에 투명 Image 추가 (레이캐스트 수신용)
        Image panelImg = guidePanel.GetComponent<Image>();
        if (panelImg == null)
        {
            panelImg = guidePanel.AddComponent<Image>();
            panelImg.color = new Color(0, 0, 0, 0.01f); // 거의 투명
            panelImg.raycastTarget = true;
        }

        // 드래그 핸들러 부착
        GuidePanelDragHandler dragHandler = guidePanel.GetComponent<GuidePanelDragHandler>();
        if (dragHandler == null)
            dragHandler = guidePanel.AddComponent<GuidePanelDragHandler>();
        dragHandler.Init(this);
    }

    /// <summary>
    /// GuidePanelDragHandler — 드래그 중 실시간 페이지 이동 미리보기
    /// </summary>
    public void OnDragging(float deltaX)
    {
        if (isTransitioning) return;
        if (pageCanvasGroups == null || pageCanvasGroups.Length == 0) return;
        if (pageBasePositions == null) return;

        RectTransform rt = guidePages[currentPage].GetComponent<RectTransform>();
        // 드래그 저항감 (끝 페이지에서 더 무겁게)
        bool atEdge = (deltaX > 0 && currentPage == 0) || (deltaX < 0 && currentPage == pageCount - 1);
        float resistance = atEdge ? 0.35f : 0.9f;
        float offsetX = deltaX * resistance;

        // base 위치 기준 상대 이동 — 페이지가 (0,0) 외 위치에 디자인되어도 정상 동작
        Vector2 basePos = pageBasePositions[currentPage];
        rt.anchoredPosition = new Vector2(basePos.x + offsetX, basePos.y);

        // 드래그 중 알파 미묘하게 감소 (전환 예고)
        float dragRatio = Mathf.Clamp01(Mathf.Abs(deltaX) / (swipeThreshold * 2f));
        pageCanvasGroups[currentPage].alpha = 1f - dragRatio * 0.3f;
    }

    /// <summary>
    /// GuidePanelDragHandler에서 호출 — 드래그 종료 시 페이지 전환 판정
    /// </summary>
    public void OnSwipe(float deltaX)
    {
        if (isTransitioning)
        {
            ResetCurrentPagePosition();
            return;
        }

        if (Mathf.Abs(deltaX) >= swipeThreshold)
        {
            if (deltaX < 0 && currentPage < pageCount - 1)
            {
                GoToPage(currentPage + 1, 1);
                return;
            }
            else if (deltaX > 0 && currentPage > 0)
            {
                GoToPage(currentPage - 1, -1);
                return;
            }
        }

        // 임계값 미달 또는 엣지 — 원위치 스냅백
        StartCoroutine(SnapBackCurrentPage());
    }

    private void ResetCurrentPagePosition()
    {
        if (pageCanvasGroups == null || pageBasePositions == null) return;
        RectTransform rt = guidePages[currentPage].GetComponent<RectTransform>();
        rt.anchoredPosition = pageBasePositions[currentPage];
        pageCanvasGroups[currentPage].alpha = 1f;
    }

    private IEnumerator SnapBackCurrentPage()
    {
        RectTransform rt = guidePages[currentPage].GetComponent<RectTransform>();
        CanvasGroup cg = pageCanvasGroups[currentPage];
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = pageBasePositions[currentPage];
        float startAlpha = cg.alpha;

        float elapsed = 0f;
        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / snapDuration;
            float eased = EaseOutCubic(t);
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            cg.alpha = Mathf.Lerp(startAlpha, 1f, eased);
            yield return null;
        }
        rt.anchoredPosition = endPos;
        cg.alpha = 1f;
    }

    private void SetupCanvasGroups()
    {
        pageCanvasGroups = new CanvasGroup[pageCount];
        pageBasePositions = new Vector2[pageCount];
        for (int i = 0; i < pageCount; i++)
        {
            CanvasGroup cg = guidePages[i].GetComponent<CanvasGroup>();
            if (cg == null) cg = guidePages[i].AddComponent<CanvasGroup>();
            pageCanvasGroups[i] = cg;
            cg.alpha = 0f;

            // 디자이너가 에디터에서 배치한 위치를 정중앙 기준으로 고정
            RectTransform rt = guidePages[i].GetComponent<RectTransform>();
            pageBasePositions[i] = rt.anchoredPosition;
        }

        // 텍스트용 CanvasGroup
        textCanvasGroup = guideText.GetComponent<CanvasGroup>();
        if (textCanvasGroup == null) textCanvasGroup = guideText.gameObject.AddComponent<CanvasGroup>();
        textCanvasGroup.alpha = 0f;

        // 전체 패널용 CanvasGroup — 진입 페이드인 / 종료 페이드아웃
        panelCanvasGroup = guidePanel.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null) panelCanvasGroup = guidePanel.AddComponent<CanvasGroup>();
    }

    private void CreateDotIndicator()
    {
        // 도트 컨테이너 — guidePanel 하위에 동적 생성. 위치/크기는 Inspector로 조절
        dotContainer = new GameObject("DotIndicator", typeof(RectTransform));
        dotContainer.transform.SetParent(guidePanel.transform, false);

        RectTransform containerRect = dotContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = dotAnchorMin;
        containerRect.anchorMax = dotAnchorMax;
        containerRect.pivot = dotPivot;
        containerRect.anchoredPosition = dotAnchoredPosition;

        float totalWidth = pageCount * dotSize + (pageCount - 1) * dotSpacing;
        containerRect.sizeDelta = new Vector2(totalWidth, dotSize);

        // HorizontalLayoutGroup
        HorizontalLayoutGroup hlg = dotContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = dotSpacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        dotImages = new Image[pageCount];
        for (int i = 0; i < pageCount; i++)
        {
            // 부모 — LayoutGroup이 정렬하는 슬롯
            GameObject dot = new GameObject($"Dot_{i}", typeof(RectTransform));
            dot.transform.SetParent(dotContainer.transform, false);

            RectTransform dotRect = dot.GetComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(dotSize, dotSize);

            // 자식 Visual — Y 오프셋/스케일 변형은 여기에만 적용 (LayoutGroup 영향 안 받음)
            GameObject visual = new GameObject("Visual", typeof(RectTransform));
            visual.transform.SetParent(dot.transform, false);

            RectTransform visualRect = visual.GetComponent<RectTransform>();
            visualRect.anchorMin = Vector2.zero;
            visualRect.anchorMax = Vector2.one;
            visualRect.offsetMin = Vector2.zero;
            visualRect.offsetMax = Vector2.zero;
            visualRect.pivot = new Vector2(0.5f, 0.5f);

            Image img = visual.AddComponent<Image>();
            img.color = (i == 0) ? dotActive : dotInactive;
            img.raycastTarget = false;

            // 원형으로 만들기 위해 Knob sprite 사용 (없으면 사각형)
            Sprite knob = Resources.Load<Sprite>("UI/Skin/Knob");
            if (knob != null) img.sprite = knob;

            dotImages[i] = img;
        }

        // 도트 뒤에 얇은 연결선(진행 바) 추가 — 진행도 시각화
        GameObject lineBg = new GameObject("DotLineBg", typeof(RectTransform));
        lineBg.transform.SetParent(dotContainer.transform, false);
        lineBg.transform.SetAsFirstSibling();
        RectTransform lineBgRect = lineBg.GetComponent<RectTransform>();
        lineBgRect.anchorMin = new Vector2(0f, 0.5f);
        lineBgRect.anchorMax = new Vector2(1f, 0.5f);
        lineBgRect.pivot = new Vector2(0.5f, 0.5f);
        lineBgRect.sizeDelta = new Vector2(0f, dotLineThickness);
        Image lineBgImg = lineBg.AddComponent<Image>();
        lineBgImg.color = dotLineBgColor;
        lineBgImg.raycastTarget = false;

        GameObject lineFill = new GameObject("DotProgressBar", typeof(RectTransform));
        lineFill.transform.SetParent(lineBg.transform, false);
        dotProgressBarRect = lineFill.GetComponent<RectTransform>();
        dotProgressBarRect.anchorMin = new Vector2(0f, 0f);
        dotProgressBarRect.anchorMax = new Vector2(0f, 1f);
        dotProgressBarRect.pivot = new Vector2(0f, 0.5f);
        dotProgressBarRect.sizeDelta = new Vector2(0f, 0f);
        dotProgressBar = lineFill.AddComponent<Image>();
        dotProgressBar.color = dotLineFillColor;
        dotProgressBar.raycastTarget = false;
    }

    // ============================================================
    // 스와이프 힌트 — 첫 페이지에서 좌우 이동 힌트 아이콘 표시
    // ============================================================
    private void CreateSwipeHint()
    {
        swipeHintObj = new GameObject("SwipeHint", typeof(RectTransform));
        swipeHintObj.transform.SetParent(guidePanel.transform, false);

        RectTransform rt = swipeHintObj.GetComponent<RectTransform>();
        rt.anchorMin = swipeHintAnchorMin;
        rt.anchorMax = swipeHintAnchorMax;
        rt.pivot = swipeHintPivot;
        rt.anchoredPosition = swipeHintAnchoredPosition;
        rt.sizeDelta = swipeHintSize;

        Text hintText = swipeHintObj.AddComponent<Text>();
        hintText.text = swipeHintText;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.color = swipeHintColor;
        hintText.raycastTarget = false;
        hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
        hintText.verticalOverflow = VerticalWrapMode.Overflow;

        // 폰트 적용 (프로젝트 규칙)
        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (customFont != null) hintText.font = customFont;
        hintText.fontSize = swipeHintFontSize;

        swipeHintCG = swipeHintObj.AddComponent<CanvasGroup>();
        swipeHintCG.alpha = 0f;
        swipeHintCG.blocksRaycasts = false;
        swipeHintCG.interactable = false;
    }

    private IEnumerator SwipeHintLoop()
    {
        yield return new WaitForSeconds(swipeHintDelay);
        RectTransform rt = swipeHintObj.GetComponent<RectTransform>();
        Vector2 basePos = rt.anchoredPosition;

        while (currentPage == 0 && guidePanel.activeSelf)
        {
            // 페이드인 + 좌우 살짝 이동 + 페이드아웃 반복
            float half = swipeHintDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / half;
                swipeHintCG.alpha = Mathf.SmoothStep(0f, 1f, t);
                float xOffset = Mathf.Sin(t * Mathf.PI * 2f) * 8f;
                rt.anchoredPosition = new Vector2(basePos.x + xOffset, basePos.y);
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / half;
                swipeHintCG.alpha = Mathf.SmoothStep(1f, 0f, t);
                float xOffset = Mathf.Sin((1f + t) * Mathf.PI * 2f) * 8f;
                rt.anchoredPosition = new Vector2(basePos.x + xOffset, basePos.y);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
        }

        swipeHintCG.alpha = 0f;
        rt.anchoredPosition = basePos;
    }

    // ============================================================
    // 가이드 시퀀스
    // ============================================================

    private IEnumerator StartGuideSequence()
    {
        yield return new WaitForSeconds(delayBeforeGuide);

        if (IsFirstTime())
        {
            ShowGuide();
            SetFirstTimeFlag();
        }
        else
        {
            guidePanel.SetActive(false);
        }
    }

    private void ShowGuide()
    {
        guidePanel.SetActive(true);
        currentPage = 0;

        // 모든 페이지 활성화하되 alpha=0
        for (int i = 0; i < pageCount; i++)
        {
            guidePages[i].SetActive(i == 0);
            pageCanvasGroups[i].alpha = 0f;
        }

        // 초기 진입 시 첫 페이지 텍스트 세팅
        string lang = GetLanguageCode();
        if (guideTemplates.ContainsKey(lang))
            guideText.text = guideTemplates[lang][currentPage];
        else
            guideText.text = guideTemplates["en"][currentPage];

        UpdateDots();
        UpdateConfirmButton();
        StartPulseForCurrentPage();

        // 패널 전체 페이드인 + 살짝 스케일업 (진입 연출) → 이후 첫 페이지 애니메이션
        StartCoroutine(PanelEntryAnimation());

        // 스와이프 힌트 시작
        if (swipeHintRoutine != null) StopCoroutine(swipeHintRoutine);
        swipeHintRoutine = StartCoroutine(SwipeHintLoop());
    }

    private IEnumerator PanelEntryAnimation()
    {
        panelCanvasGroup.alpha = 0f;
        RectTransform panelRT = guidePanel.GetComponent<RectTransform>();
        Vector3 originalScale = panelRT.localScale;
        panelRT.localScale = originalScale * 0.96f;

        if (backgroundDim != null) backgroundDim.color = new Color(0, 0, 0, 0f);

        float elapsed = 0f;
        while (elapsed < panelEntryDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / panelEntryDuration;
            float eased = EaseOutCubic(t);

            panelCanvasGroup.alpha = eased;
            panelRT.localScale = Vector3.Lerp(originalScale * 0.96f, originalScale, eased);

            if (backgroundDim != null)
                backgroundDim.color = new Color(0, 0, 0, eased * backgroundDimAlpha);

            yield return null;
        }

        panelCanvasGroup.alpha = 1f;
        panelRT.localScale = originalScale;
        if (backgroundDim != null)
            backgroundDim.color = new Color(0, 0, 0, backgroundDimAlpha);

        // 첫 페이지 컨텐츠 페이드인
        yield return StartCoroutine(AnimatePageIn(0, 1));
    }

    // ============================================================
    // 스와이프 입력 — GuidePanelDragHandler가 guidePanel에서 처리
    // ============================================================

    // ============================================================
    // 페이지 전환
    // ============================================================

    /// <param name="direction">1=다음(왼쪽), -1=이전(오른쪽)</param>
    private void GoToPage(int targetPage, int direction)
    {
        if (targetPage < 0 || targetPage >= pageCount) return;
        if (isTransitioning) return;

        StartCoroutine(TransitionPage(currentPage, targetPage, direction));
    }

    private IEnumerator TransitionPage(int fromPage, int toPage, int direction)
    {
        isTransitioning = true;

        // 현재 페이지 페이드아웃
        yield return StartCoroutine(AnimatePageOut(fromPage, direction));

        guidePages[fromPage].SetActive(false);
        currentPage = toPage;

        // 텍스트 + 도트 업데이트
        string lang = GetLanguageCode();
        guideText.text = guideTemplates[lang][currentPage];

        UpdateDots();
        UpdateConfirmButton();

        // 새 페이지 페이드인 — 활성화 먼저 해야 PulseGraphicLoop의
        // activeInHierarchy 체크가 통과됨 (StartPulseForCurrentPage는 활성화 후에 호출)
        guidePages[toPage].SetActive(true);
        StartPulseForCurrentPage();

        yield return StartCoroutine(AnimatePageIn(toPage, direction));

        isTransitioning = false;
    }

    // ============================================================
    // 페이지별 강조 이미지 펄스 — 현재 페이지에 해당하는 항목만 작동
    // (직전 페이지 펄스는 정지 + 원본 색상/스케일 복원)
    // ============================================================
    private void StartPulseForCurrentPage()
    {
        StopAllPagePulses();

        if (pageHighlights == null || pageHighlights.Length == 0) return;

        for (int i = 0; i < pageHighlights.Length; i++)
        {
            PulseTarget t = pageHighlights[i];
            if (t == null) continue;
            if (t.pageIndex != currentPage) continue;
            if (t.targetGraphic == null) continue;

            Coroutine co = StartCoroutine(PulseGraphicLoop(t));
            activePulseRoutines.Add(co);
        }
    }

    private void StopAllPagePulses()
    {
        if (activePulseRoutines == null) return;
        foreach (var co in activePulseRoutines)
        {
            if (co != null) StopCoroutine(co);
        }
        activePulseRoutines.Clear();

        // 모든 펄스 대상 — 원래 스케일/색상으로 복원 (페이지 전환 시 잔상 방지)
        if (pageHighlights == null) return;
        foreach (var t in pageHighlights)
        {
            if (t == null || t.targetGraphic == null) continue;
            t.targetGraphic.transform.localScale = Vector3.one;
            // 원래 색상은 캡처해두지 않고 매번 새로 시작 시점 색상을 baseColor로 잡으므로
            // alpha만 1로 보정하지 말고, 사용자가 인스펙터에서 설정한 색을 신뢰 → 별도 복원 불필요
        }
    }

    private IEnumerator PulseGraphicLoop(PulseTarget t)
    {
        Transform tr = t.targetGraphic.transform;
        Vector3 baseScale = tr.localScale; // 인스펙터에서 디자이너가 설정한 스케일을 기준으로
        Color baseColor = t.targetGraphic.color; // 시작 시점의 색상을 기준 색상으로 캡처

        float time = 0f;
        while (t.targetGraphic != null && t.targetGraphic.gameObject.activeInHierarchy)
        {
            time += Time.deltaTime;
            float sin = Mathf.Sin(time * t.pulseSpeed);  // -1 ~ 1
            float pulse01 = (sin + 1f) * 0.5f;           // 0 ~ 1

            // 스케일 — baseScale 기준으로 살짝 부풀었다 줄었다
            tr.localScale = baseScale * (1f + sin * t.scaleAmplitude);

            // 알파 살짝 반짝 (밝아졌다 어두워졌다 — 흰빛 살짝 섞기)
            Color glow = Color.Lerp(baseColor, Color.white, pulse01 * t.glowAmplitude * 0.5f);
            glow.a = Mathf.Clamp01(
                baseColor.a * (1f - t.glowAmplitude * 0.25f)
                + pulse01 * t.glowAmplitude * 0.25f);
            t.targetGraphic.color = glow;

            yield return null;
        }

        // 코루틴 종료 시 base 상태로 복원
        if (t.targetGraphic != null)
        {
            tr.localScale = baseScale;
            t.targetGraphic.color = baseColor;
        }
    }

    // ============================================================
    // 페이지 애니메이션
    // ============================================================

    private IEnumerator AnimatePageIn(int pageIndex, int direction)
    {
        CanvasGroup cg = pageCanvasGroups[pageIndex];
        RectTransform rt = guidePages[pageIndex].GetComponent<RectTransform>();

        // 종착점은 항상 캐시된 base 위치 — 정확히 가운데로 수렴 보장
        Vector2 basePos = pageBasePositions[pageIndex];
        float startX = basePos.x + pageSlideOffset * direction * imageParallaxFactor;
        rt.anchoredPosition = new Vector2(startX, basePos.y);
        cg.alpha = 0f;

        // 텍스트 숨기기
        textCanvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < pageFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pageFadeDuration;
            float eased = EaseOutCubic(t);

            cg.alpha = eased;
            rt.anchoredPosition = new Vector2(
                Mathf.Lerp(startX, basePos.x, eased),
                basePos.y);

            yield return null;
        }

        // 명시적으로 base에 정렬 — 부동소수 잔차 제거
        cg.alpha = 1f;
        rt.anchoredPosition = basePos;

        // 텍스트 딜레이 후 페이드인
        yield return new WaitForSeconds(textFadeDelay);
        yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 0f, 1f, pageFadeDuration * 0.55f));
    }

    private IEnumerator AnimatePageOut(int pageIndex, int direction)
    {
        CanvasGroup cg = pageCanvasGroups[pageIndex];
        RectTransform rt = guidePages[pageIndex].GetComponent<RectTransform>();
        Vector2 basePos = pageBasePositions[pageIndex];
        Vector2 startPos = rt.anchoredPosition; // 드래그된 위치에서 자연스럽게 이어지도록
        float endX = basePos.x - pageSlideOffset * direction * imageParallaxFactor;

        // 텍스트 먼저 페이드아웃
        yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 1f, 0f, pageFadeDuration * 0.35f));

        float outDuration = pageFadeDuration * 0.55f;
        float elapsed = 0f;
        while (elapsed < outDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / outDuration;
            float eased = EaseInCubic(t);

            cg.alpha = 1f - eased;
            rt.anchoredPosition = new Vector2(
                Mathf.Lerp(startPos.x, endX, eased),
                basePos.y);

            yield return null;
        }

        cg.alpha = 0f;
        // ★ 비활성화 직전 반드시 base 위치로 복원 — 다음 표시 시 정렬 어긋남 방지
        rt.anchoredPosition = basePos;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    // ============================================================
    // 도트 인디케이터
    // ============================================================

    private void UpdateDots()
    {
        if (dotImages == null) return;
        for (int i = 0; i < pageCount; i++)
        {
            StartCoroutine(AnimateDot(i, i == currentPage));
        }

        // 진행 바 — 현재 페이지 비율만큼 채움
        if (dotProgressBarRect != null)
        {
            float progress = pageCount > 1 ? (float)currentPage / (pageCount - 1) : 1f;
            StartCoroutine(AnimateProgressBar(progress));
        }
    }

    private IEnumerator AnimateProgressBar(float targetNormalized)
    {
        float startWidth = dotProgressBarRect.sizeDelta.x;
        RectTransform parent = dotProgressBarRect.parent as RectTransform;
        float parentWidth = parent != null ? parent.rect.width : 0f;
        float targetWidth = parentWidth * targetNormalized;

        float elapsed = 0f;
        float duration = 0.35f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutCubic(elapsed / duration);
            float w = Mathf.Lerp(startWidth, targetWidth, t);
            dotProgressBarRect.sizeDelta = new Vector2(w, dotProgressBarRect.sizeDelta.y);
            yield return null;
        }
        dotProgressBarRect.sizeDelta = new Vector2(targetWidth, dotProgressBarRect.sizeDelta.y);
    }

    private IEnumerator AnimateDot(int index, bool active)
    {
        Image img = dotImages[index];
        RectTransform dotRT = img.transform as RectTransform;

        Color targetColor = active ? dotActive : dotInactive;
        Color startColor = img.color;

        float targetScale = active ? dotActiveScale : 1f;
        float startScale = img.transform.localScale.x;

        float targetY = active ? dotActiveYOffset : 0f;
        float startY = dotRT != null ? dotRT.anchoredPosition.y : 0f;

        float elapsed = 0f;
        float duration = 0.25f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            img.color = Color.Lerp(startColor, targetColor, t);
            float s = Mathf.Lerp(startScale, targetScale, t);
            img.transform.localScale = new Vector3(s, s, 1f);
            if (dotRT != null)
            {
                Vector2 ap = dotRT.anchoredPosition;
                ap.y = Mathf.Lerp(startY, targetY, t);
                dotRT.anchoredPosition = ap;
            }
            yield return null;
        }
        img.color = targetColor;
        img.transform.localScale = new Vector3(targetScale, targetScale, 1f);
        if (dotRT != null)
        {
            Vector2 ap = dotRT.anchoredPosition;
            ap.y = targetY;
            dotRT.anchoredPosition = ap;
        }
    }

    // ============================================================
    // 확인 버튼 (마지막 페이지)
    // ============================================================

    private void UpdateConfirmButton()
    {
        if (confirmButton == null) return;

        bool shouldShow = currentPage == pageCount - 1;
        confirmButton.gameObject.SetActive(shouldShow);

        if (confirmPulseRoutine != null)
        {
            StopCoroutine(confirmPulseRoutine);
            confirmPulseRoutine = null;
            RestoreConfirmButtonVisual();
        }

        if (shouldShow)
            confirmPulseRoutine = StartCoroutine(PulseConfirmButton());
    }

    /// <summary>
    /// 마지막 페이지 확인 버튼 — 스케일 + 알파/밝기 반짝임으로
    /// "여기 누를 수 있는 버튼이 있어요" 시각적 어필.
    /// </summary>
    private IEnumerator PulseConfirmButton()
    {
        // Button 컴포넌트 transition=ColorTint이면 graphic.color는 매 프레임 덮어쓰기됨.
        // 따라서 CanvasGroup.alpha로 후광 반짝임 효과를 구현 (transition과 충돌 없음).
        CanvasGroup cg = confirmButton.GetComponent<CanvasGroup>();
        if (cg == null) cg = confirmButton.gameObject.AddComponent<CanvasGroup>();

        // 후광 반짝임 진폭 — confirmGlowAmplitude 만큼 알파가 줄었다가 1로 되돌아옴
        // (예: 0.45 → 0.55 ↔ 1.0 사이에서 반복)
        float minAlpha = Mathf.Clamp01(1f - confirmGlowAmplitude);

        float time = 0f;
        while (confirmButton != null && confirmButton.gameObject.activeSelf)
        {
            time += Time.deltaTime;
            float sin = Mathf.Sin(time * confirmPulseSpeed);          // -1 ~ 1
            float pulse01 = (sin + 1f) * 0.5f;                        // 0 ~ 1

            // 스케일은 고정 — 후광처럼 알파만 부드럽게 반짝
            cg.alpha = Mathf.Lerp(minAlpha, 1f, pulse01);

            yield return null;
        }
        RestoreConfirmButtonVisual();
    }

    private void RestoreConfirmButtonVisual()
    {
        if (confirmButton == null) return;
        confirmButton.transform.localScale = Vector3.one;

        // CanvasGroup.alpha 복원 (PulseConfirmButton이 사용하는 채널)
        CanvasGroup cg = confirmButton.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        // 혹시 이전 버전(graphic.color 변경)을 남긴 경우를 위한 호환성 복원
        Graphic g = confirmButton.targetGraphic;
        if (g != null && confirmButtonBaseColor.HasValue)
            g.color = confirmButtonBaseColor.Value;
    }

    private void OnConfirmButtonClicked()
    {
        StartCoroutine(CloseGuide());
    }

    private IEnumerator CloseGuide()
    {
        // 반복 코루틴 정리
        if (swipeHintRoutine != null) { StopCoroutine(swipeHintRoutine); swipeHintRoutine = null; }
        if (confirmPulseRoutine != null)
        {
            StopCoroutine(confirmPulseRoutine);
            confirmPulseRoutine = null;
            RestoreConfirmButtonVisual();
        }
        StopAllPagePulses();

        CanvasGroup panelCG = panelCanvasGroup != null ? panelCanvasGroup : guidePanel.AddComponent<CanvasGroup>();
        RectTransform panelRT = guidePanel.GetComponent<RectTransform>();
        Vector3 startScale = panelRT.localScale;
        Vector3 targetScale = startScale * 1.02f;
        float startDimAlpha = backgroundDim != null ? backgroundDim.color.a : 0f;

        float elapsed = 0f;
        float duration = 0.6f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float eased = EaseInCubic(t);
            panelCG.alpha = 1f - eased;
            panelRT.localScale = Vector3.Lerp(startScale, targetScale, eased);
            if (backgroundDim != null)
                backgroundDim.color = new Color(0, 0, 0, Mathf.Lerp(startDimAlpha, 0f, eased));
            yield return null;
        }

        guidePanel.SetActive(false);
        panelCG.alpha = 1f;
        panelRT.localScale = startScale;
    }

    // ============================================================
    // 유틸리티
    // ============================================================

    private bool IsFirstTime()
    {
        return PlayerPrefs.GetInt(FIRST_TIME_KEY, 0) == 0;
    }

    private void SetFirstTimeFlag()
    {
        PlayerPrefs.SetInt(FIRST_TIME_KEY, 1);
        PlayerPrefs.Save();
    }

    private string GetLanguageCode()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.Japanese: return "ja";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return "zh";
            case SystemLanguage.Spanish: return "es";
            default: return "en";
        }
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    // ============================================================
    // 테스트 트리거 — Inspector 버튼에서 호출
    // ============================================================

    public void ForceShowGuide()
    {
        if (!Application.isPlaying) return;
        if (guidePanel == null || guidePages == null || guidePages.Length == 0 || guideText == null) return;

        // Start가 아직 실행 안 됐으면 기본 세팅 수행
        if (pageCanvasGroups == null || pageCanvasGroups.Length == 0)
        {
            pageCount = guidePages.Length;
            SetupSwipeArea();
            SetupCanvasGroups();
            SetupBackgroundDim();
            SetupBackgroundImage();
            CreateDotIndicator();
            CreateSwipeHint();
        }

        currentPage = 0;
        PlayerPrefs.DeleteKey(FIRST_TIME_KEY);
        PlayerPrefs.Save();
        StopAllCoroutines();
        ShowGuide();
    }

    public void ResetFirstTimeFlag()
    {
        PlayerPrefs.DeleteKey(FIRST_TIME_KEY);
        PlayerPrefs.Save();
    }
}

/// <summary>
/// guidePanel에 런타임 부착되어 스와이프 입력을 FirstTimeGuide에 전달
/// </summary>
public class GuidePanelDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private FirstTimeGuide guide;
    private Vector2 dragStartPos;

    public void Init(FirstTimeGuide guide)
    {
        this.guide = guide;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStartPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (guide == null) return;
        float deltaX = eventData.position.x - dragStartPos.x;
        guide.OnDragging(deltaX);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (guide != null)
        {
            float deltaX = eventData.position.x - dragStartPos.x;
            guide.OnSwipe(deltaX);
        }
    }
}
