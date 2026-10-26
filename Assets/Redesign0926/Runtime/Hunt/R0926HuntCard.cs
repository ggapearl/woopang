using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// '추가' 창 세 번째 카드 — 보물찾기 (2026-10-06 대표님: 주최는 장소 추가·3D모델 추가 옆에서. 따로 탭은 찾기 어렵다).
///  · 처음: 맨 위 '지금 이 자리에 보물 숨기기' · 코드로 참가 · 근처 보물찾기(참가) · 내 게임
///  · 숨기기: 처음엔 주최 수칙 동의 → 양식(현장 GPS · 제목 · 상품 · 힌트 · 모양 · 방식 · 인원 · 보이는 거리 · 범위 · 대상 · 시작 · 진행 시간)
///    → 확인 카드(고치기 · 숨기기) → 숨기면 같은 카드에 참가 코드 · 공유하기 (10-26) · 공유 링크로 열면 OpenLink(코드가 있으면 바로 참가)
///  · 상세: 게임 정보 · 참가/나가기/포기 · 8초마다 근접 단계 · 찾기는 AR 보물을 두 번 톡톡(R0926HuntTreasure — '보물 열기' 버튼은 10-14 대표님 요청으로 없앰),
///    코드 게임은 보이는 거리 안에서 코드 칸 · 찾은 사람·주최자 메시지 · 주최자 늘리기(1시간·1일 한 줄)/끝내기/위치 공개
///  · 한마디·코드 칸은 키보드 위 입력줄의 보내기(엔터)로 바로 보낸다 (UploadInputMirror.SetSubmitAction)
/// 서버: R0926HuntApi (/api/hunts). UI 틀은 에디터 Redesign0926ApplyHunt 가 만들고, 줄·칩은 여기서 본보기로 찍는다.
/// </summary>
public class R0926HuntCard : MonoBehaviour
{
    [Header("화면")]
    [SerializeField] private GameObject hub;
    [SerializeField] private GameObject form;
    [SerializeField] private GameObject rules;
    [SerializeField] private GameObject detail;
    [SerializeField] private ScrollRect hubScroll, formScroll, rulesScroll, detailScroll;

    [Header("처음 화면")]
    [SerializeField] private Button heroButton;
    [SerializeField] private Button rulesLink;
    [SerializeField] private InputField joinCodeInput;
    [SerializeField] private Button joinCodeButton;
    [SerializeField] private Text nearHead;
    [SerializeField] private RectTransform nearList;
    [SerializeField] private Text nearEmpty;
    [SerializeField] private GameObject mineHead;
    [SerializeField] private RectTransform mineList;
    [SerializeField] private GameObject loginBox;
    [SerializeField] private Button loginButton;
    [SerializeField] private GameObject offBox;          // 설정에서 보물찾기를 꺼 둔 사람

    [Header("본보기 (꺼 둔 채)")]
    [SerializeField] private GameObject rowTemplate;
    [SerializeField] private GameObject chipTemplate;
    [SerializeField] private GameObject ruleTemplate;
    [SerializeField] private GameObject feedTemplate;

    [Header("숨기기 양식")]
    [SerializeField] private Button formBack;
    [SerializeField] private Text gpsText;
    [SerializeField] private InputField titleInput, hintInput, codeInput, questionInput, prizeInput;
    [SerializeField] private GameObject codeBox;
    [SerializeField] private RectTransform modeChips, winnerChips, playerChips, visibleChips, radiusChips, audienceChips, startChips, durationChips;
    [SerializeField] private Text audienceNote;          // '누구에게' 아래 — 고른 대상이 무엇인지 (참가 코드는 어떻게 쓰이는지)
    [SerializeField] private Button submitButton;
    [SerializeField] private Text submitLabel;
    [SerializeField] private Text formMsg;

    [Header("보물 모양 (10-24 — 고르면 미리보기 · 사진·3D 모델은 숨길 때 올린다)")]
    [SerializeField] private RectTransform shapeChips;
    [SerializeField] private R0926HuntShapePreview shapePreview;
    [SerializeField] private Button pickButton;
    [SerializeField] private Text pickLabel;
    [SerializeField] private Text pickCaption;

    [Header("주최 수칙")]
    [SerializeField] private Text rulesTitle;
    [SerializeField] private RectTransform rulesList;
    [SerializeField] private Button agreeRow;
    [SerializeField] private Image agreeBox;
    [SerializeField] private GameObject agreeCheck;
    [SerializeField] private Text agreeLabel;
    [SerializeField] private Button agreeButton;
    [SerializeField] private Image agreeButtonBg;
    [SerializeField] private Button rulesBack;

    [Header("상세")]
    [SerializeField] private Button detailBack;
    [SerializeField] private Text dTitle, dStatus, dInfo, dNear, dMine, feedHead;
    [SerializeField] private Button dHost;
    [SerializeField] private Text dHostText;
    [SerializeField] private GameObject claimCodeBox;
    [SerializeField] private InputField claimCodeInput;
    [SerializeField] private Button claimCodeButton;      // 코드 게임 — 넣은 코드로 열기
    [SerializeField] private Button joinButton, leaveButton, giveupButton, endButton, revealButton, profileButton;
    [SerializeField] private Button shareButton;           // 공유하기 — 주최자는 늘, 다른 사람은 '모두' 게임만
    [SerializeField] private GameObject extendRow;         // 주최자 — '1시간 연장하기 · 1일 연장하기' 한 줄
    [SerializeField] private Button extendButton, extendDayButton;
    [SerializeField] private GameObject msgBox;
    [SerializeField] private InputField msgInput;
    [SerializeField] private Button msgSend;
    [SerializeField] private RectTransform feedList;
    [SerializeField] private UploadInputMirror inputMirror;   // 키보드 위 입력줄 — 보내기(엔터)로 한마디·코드를 바로

    [Header("숨기기 전 확인 카드 (10-26 — 캔버스 맨 위 층 · 숨긴 뒤엔 참가 코드 · 공유하기)")]
    [SerializeField] private GameObject review;
    [SerializeField] private Button reviewDim;
    [SerializeField] private Text reviewHead, reviewSub, reviewNote, reviewMsg, reviewCode;
    [SerializeField] private Text reviewWarn;   // 낚시성·도박·유해 → 계정 정지·삭제 (10-24 대표님) — 숨기기 전에만
    [SerializeField] private GameObject reviewCodeBox;
    [SerializeField] private RectTransform reviewRows;
    [SerializeField] private GameObject reviewRowTemplate;
    [SerializeField] private Button reviewEdit, reviewGo, reviewShare, reviewDone;
    [SerializeField] private Text reviewGoLabel;

    [Header("알림")]
    [SerializeField] private Text toast;

    private static readonly Color Ink = new Color(0.953f, 0.961f, 0.965f, 1f);
    private static readonly Color Muted = new Color(0.549f, 0.584f, 0.616f, 1f);
    private static readonly Color Gold = new Color(0.910f, 0.749f, 0.310f, 1f);
    private static readonly Color Teal = new Color(0.247f, 0.757f, 0.690f, 1f);
    private static readonly Color Amber = new Color(1f, 0.702f, 0.278f, 1f);
    private static readonly Color Red = new Color(1f, 0.42f, 0.42f, 1f);
    private static readonly Color ChipOn = new Color(0.910f, 0.749f, 0.310f, 0.22f);
    private static readonly Color ChipOff = new Color(1f, 1f, 1f, 0.06f);

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly Dictionary<string, string> picked = new Dictionary<string, string>();
    private bool rulesAccepted, agreed, busy, built;
    private int rulesVersion = 1;
    private JSONNode current;                 // 상세에 띄운 게임
    private float nextNear;
    private int nearRank = -1;                // 지난번 근접 단계 (0 멀다 ~ 4 30m · 5 보임) — 가까워질 때만 진동
    private float nextDist;
    private float nextReload;                 // 상세를 보는 동안 소식·상태를 다시 받는 때
    private int detailGen;                    // 상세를 새로 열 때마다 — 그 전에 떠난 다시 받기는 버린다
    private Coroutine toastCo;

    // 확인 카드 — 숨긴 뒤(postedId > 0)엔 그 게임의 참가 코드 · 공유하기
    private int postedId;
    private string postedCode, postedTitle, postedPrize, postedAudience;

    /// <summary>숨기기 확인 카드가 떠 있다 — 추가 창 옆으로 넘기기·아래로 밀어 닫기를 막는다 (SwipePanelController · R0926SwipeDismiss). 거짓이면 카드가 스스로 닫힌다 (R0926HuntReview)</summary>
    public static bool Reviewing { get; private set; }

    // 고른 사진(JPG)·3D 모델(GLB) — 숨길 때 올리고, 올린 주소는 같은 파일로 다시 숨길 때 쓴다
    private byte[] objData;
    private string objKind, objUrl;
    private Texture2D objTex;                 // 고른 사진 (미리보기 액자)
    private string pickNote;                  // 고르기 칸 아래 알림 (못 연 파일 등)
    private bool uploading, previewLoading;
    private readonly R0926PickerWait pick = new R0926PickerWait();
    private const int ModelMax = 10 * 1024 * 1024;   // 서버 OBJECT_MAX['model'] 과 같다

    private static string Lang => R0926LocalizedText.Lang();
    private static string T(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);

    // ── 칩 정의 (값, 한국어, 영어, 일본어, 중국어, 스페인어) ──
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Modes = { ("touch", "먼저 터치", "First touch", "先にタッチ", "先触碰", "Primero en tocar"), ("code", "비밀번호", "Password", "パスワード", "密码", "Contraseña") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Winners = { ("1", "1명", "1", "1人", "1人", "1"), ("3", "3명", "3", "3人", "3人", "3"), ("5", "5명", "5", "5人", "5人", "5"), ("10", "10명", "10", "10人", "10人", "10"), ("all", "모두", "Everyone", "全員", "所有人", "Todos") };
    // 참가 인원 상한 (2026-10-12 대표님 — 서버 1~100 · 당첨 인원 이상. 칩은 모두 당첨 최대 10명 이상이라 어긋나지 않는다)
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Players = { ("10", "10명", "10", "10人", "10人", "10"), ("20", "20명", "20", "20人", "20人", "20"), ("30", "30명", "30", "30人", "30人", "30"), ("50", "50명", "50", "50人", "50人", "50"), ("100", "100명", "100", "100人", "100人", "100") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Visibles = { ("10", "10m", "10m", "10m", "10m", "10m"), ("30", "30m", "30m", "30m", "30m", "30m"), ("50", "50m", "50m", "50m", "50m", "50m"), ("100", "100m", "100m", "100m", "100m", "100m"), ("200", "200m", "200m", "200m", "200m", "200m") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Radii = { ("1", "1km", "1km", "1km", "1km", "1km"), ("5", "5km", "5km", "5km", "5km", "5km"), ("10", "10km", "10km", "10km", "10km", "10km"), ("30", "30km", "30km", "30km", "30km", "30km"), ("100", "100km", "100km", "100km", "100km", "100km") };
    // 위치 공개(정확히·대략·비공개) 칩은 없앴다 — 늘 정확히 (10-26 대표님). 예전 대략·비공개 게임은 상세가 그대로 그린다
    // 누구에게 — 팔로워 모두 = 주최자를 팔로우하는 사람(맞팔 포함 · 10-26 대표님)
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Audiences = { ("all", "모두", "Everyone", "全員", "所有人", "Todos"), ("friends", "맞팔", "Mutuals", "相互フォロー", "互关好友", "Mutuos"), ("follower", "팔로워 모두", "Followers", "フォロワー全員", "所有粉丝", "Seguidores"), ("code", "참가 코드", "Join code", "参加コード", "参加代码", "Código") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Starts = { ("0", "바로", "Now", "すぐ", "立即", "Ahora"), ("60", "1시간 뒤", "In 1h", "1時間後", "1小时后", "En 1 h"), ("180", "3시간 뒤", "In 3h", "3時間後", "3小时后", "En 3 h"), ("1440", "내일", "Tomorrow", "明日", "明天", "Mañana"), ("2880", "2일 뒤", "In 2 days", "2日後", "2天后", "En 2 días") };
    // 진행 시간 — 1시간부터 · 7일은 '일주일' (10-26 대표님 — 30분은 없앰. 서버는 옛 앱을 위해 15분부터 그대로 받는다)
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Durations = { ("60", "1시간", "1 hour", "1時間", "1小时", "1 hora"), ("180", "3시간", "3 hours", "3時間", "3小时", "3 horas"), ("1440", "1일", "1 day", "1日", "1天", "1 día"), ("4320", "3일", "3 days", "3日", "3天", "3 días"), ("10080", "일주일", "1 week", "1週間", "1周", "1 semana") };
    // 보물 모양 (2026-10-24 대표님 — 주최자가 고른다. 서버 hunts.object_kind · 모르는 모양은 옛 앱에서 보물상자).
    // 10-26 대표님 — 고르는 칩은 보물상자·선물상자·하트·사진·3D 모델만. 예전에 숨긴 보물알·보석·별 게임은 AR 이 그대로 그린다
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Shapes = { ("chest", "보물상자", "Chest", "宝箱", "宝箱", "Cofre"), ("gift", "선물상자", "Gift box", "ギフト箱", "礼物盒", "Regalo"), ("heart", "하트", "Heart", "ハート", "爱心", "Corazón"), ("photo", "사진", "Photo", "写真", "照片", "Foto"), ("model", "3D 모델", "3D model", "3Dモデル", "3D 模型", "Modelo 3D") };

    private void Awake()
    {
        foreach (var t in new[] { rowTemplate, chipTemplate, ruleTemplate, feedTemplate }) if (t != null) t.SetActive(false);
        Wire(heroButton, OnHero);
        Wire(rulesLink, () => StartCoroutine(OpenRules(false)));
        Wire(joinCodeButton, () => StartCoroutine(JoinByCode()));
        Wire(loginButton, () => { if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup(); });
        Wire(formBack, ShowHub);
        Wire(submitButton, OnSubmit);
        Wire(reviewEdit, () => { if (!busy) HideReview(); });
        Wire(reviewGo, () => StartCoroutine(Post()));
        Wire(reviewDone, CloseDone);
        Wire(reviewShare, () => Share(postedId, postedCode, postedTitle, postedPrize));
        Wire(reviewDim, OnReviewBack);
        Wire(shareButton, ShareCurrent);
        Wire(pickButton, OnPick);
        Wire(agreeRow, () => { agreed = !agreed; PaintAgree(); });
        Wire(agreeButton, () => StartCoroutine(Accept()));
        Wire(rulesBack, ShowHub);
        Wire(detailBack, ShowHub);
        Wire(dHost, () => OpenHostProfile(false));
        Wire(profileButton, () => OpenHostProfile(true));   // 별점 남기기 — 프로필의 보물찾기 칸을 펼쳐 남기기까지
        Wire(claimCodeButton, () => StartCoroutine(Claim()));
        Wire(joinButton, () => StartCoroutine(Act("join")));
        Wire(leaveButton, () => StartCoroutine(Act("leave")));
        Wire(giveupButton, () => StartCoroutine(Act("giveup")));
        Wire(extendButton, () => StartCoroutine(Act("extend", 60)));
        Wire(extendDayButton, () => StartCoroutine(Act("extend", 1440)));
        Wire(endButton, () => StartCoroutine(Act("end")));
        Wire(revealButton, () => StartCoroutine(Act("reveal")));
        Wire(msgSend, () => StartCoroutine(SendMessage()));
        if (inputMirror != null)
        {
            // 입력줄의 보내기(또는 키패드 엔터)를 누르면 글이 칸에 들어간 뒤 바로 보낸다 — 대화창과 같이 (10-14 대표님)
            inputMirror.SetSubmitAction(msgInput, () => StartCoroutine(SendMessage()));
            inputMirror.SetSubmitAction(claimCodeInput, () => StartCoroutine(Claim()));
        }
    }

    private static void Wire(Button b, Action a)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() =>
        {
            if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect();
            a();
        });
    }

    private void OnEnable()
    {
        if (!built) { BuildChips(); built = true; }
        if (LoginManager.Instance != null) LoginManager.Instance.OnLoginStateChanged += OnLoginChanged;
        ShowHub();
    }

    private void OnDisable()
    {
        if (LoginManager.Instance != null) LoginManager.Instance.OnLoginStateChanged -= OnLoginChanged;
        NearHuntId = 0;
        busy = false;   // 보내는 중에 창이 닫히면 코루틴이 멈춘 채 남아 — 다시 열었을 때 버튼이 다 먹통이었다
        uploading = false;
        Reviewing = false;   // 확인 카드는 다음 LateUpdate 에 스스로 닫힌다 (꺼지는 중엔 다른 오브젝트를 끄지 않는다)
        postedId = 0;
    }

    private void OnApplicationFocus(bool hasFocus) => R0926PickerWait.NoteFocus(hasFocus);

    /// <summary>상세가 근접 확인 중인 게임 (없으면 0) — R0926HuntTreasure 가 같은 게임을 따로 묻지 않는다</summary>
    public static int NearHuntId { get; private set; }

    // 로그인하고 돌아오면 처음 화면을 다시 — '로그인하면 …' 안내가 그대로 남아 있었다
    private void OnLoginChanged(bool loggedIn)
    {
        if (loggedIn && linkId > 0 && R0926HuntApi.CanCall) { StartCoroutine(OpenLink(linkId, linkCode)); return; }   // 공유 링크로 왔다가 로그인했다 — 이어서
        if (hub != null && hub.activeInHierarchy) ShowHub();
    }

    private void Update()
    {
        NearHuntId = detail != null && detail.activeInHierarchy && current != null && NearMode(current) ? current["id"].AsInt : 0;
        if (form != null && form.activeInHierarchy && gpsText != null) PaintGps();
        if (form != null && form.activeInHierarchy && shapePreview != null)
        {
            // 고른 3D 모델을 미리보기가 못 열었다 — 숨겨도 보물상자로만 보이니 다시 고르게 한다
            if (objKind == "model" && shapePreview.ModelFailed)
            {
                ClearObject();
                pickNote = T("이 3D 모델은 열 수 없어요 — 다른 GLB 를 골라 주세요", "Couldn't open this 3D model — choose another GLB", "この3Dモデルは開けません — 別のGLBを選んでください", "无法打开此 3D 模型 — 请选择其他 GLB", "No se pudo abrir este modelo 3D — elige otro GLB");
                PaintShape();
            }
            if (shapePreview.Loading != previewLoading) { previewLoading = shapePreview.Loading; PaintPick(); }
        }
        if (detail != null && detail.activeInHierarchy && current != null && Time.unscaledTime >= nextNear && !busy)
        {
            nextNear = Time.unscaledTime + 8.5f;
            StartCoroutine(Near());
        }
        if (detail != null && detail.activeInHierarchy && current != null && Time.unscaledTime >= nextDist)
        {
            nextDist = Time.unscaledTime + 2f;
            PaintDistance();
        }
        // 보고 있는 동안 시작·끝·새 소식이 바뀐다 — 알림을 꺼 두었어도 30초마다 (글을 쓰는 중엔 미룬다)
        if (detail != null && detail.activeInHierarchy && current != null && Time.unscaledTime >= nextReload && !busy)
        {
            nextReload = Time.unscaledTime + 30f;
            if (!Typing()) StartCoroutine(ReloadDetail(current["id"].AsInt));
        }
    }

    // ── 위치 ──────────────────────────────────────────────
    // 장소 올리기·정보 수정과 같은 자리 (GeoHere) — Geospatial(VPS) 이 잡혀 있으면 그것(AR 보물 앵커와 같은 기준), 아니면 휴대폰 위치.
    // 예전엔 휴대폰 위치(float)만 봐서 숨긴 자리가 GPS 오차만큼 AR 보물과 어긋날 수 있었다. 보내고 저장하는 값은 소수 6자리 (10-26)
    internal static bool TryLocation(out double lat, out double lon, out float acc)
    {
        if (GeoHere.TryGet(out lat, out lon, out _, out acc))
        {
            lat = Math.Round(lat, 6); lon = Math.Round(lon, 6);
            return true;
        }
        acc = 0f;
        return false;
    }

    private static string Q(double v) => v.ToString("F6", CultureInfo.InvariantCulture);

    // ── 처음 화면 ─────────────────────────────────────────
    private void Show(GameObject view)
    {
        foreach (var v in new[] { hub, form, rules, detail }) if (v != null) v.SetActive(v == view);
        var sr = view == hub ? hubScroll : view == form ? formScroll : view == rules ? rulesScroll : detailScroll;
        if (sr != null) sr.verticalNormalizedPosition = 1f;
    }

    public void ShowHub()
    {
        current = null;
        Show(hub);
        if (offBox != null) offBox.SetActive(false);
        if (!R0926HuntApi.CanCall) { ShowLoggedOut(); return; }
        if (loginBox != null) loginBox.SetActive(false);
        if (nearHead != null) nearHead.gameObject.SetActive(true);
        StartCoroutine(LoadHub());
    }

    // 로그인 전(또는 토큰 만료) — 목록을 못 불러오니 '근처에 없어요'·'내 게임' 대신 로그인 안내만 (없는 것처럼 보였다)
    private void ShowLoggedOut()
    {
        ClearRows(nearList, mineList);
        if (loginBox != null) loginBox.SetActive(true);
        if (nearHead != null) nearHead.gameObject.SetActive(false);
        if (nearEmpty != null) nearEmpty.gameObject.SetActive(false);
        if (mineHead != null) mineHead.SetActive(false);
    }

    private IEnumerator LoadHub()
    {
        string q = TryLocation(out double lat, out double lon, out _) ? "?lat=" + Q(lat) + "&lon=" + Q(lon) : "";
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Get("/api/hunts/nearby" + q, (c, r) => { code = c; j = r; });
        if (code == 401) { ShowLoggedOut(); yield break; }
        if (code != 200) { if (nearEmpty != null) { nearEmpty.gameObject.SetActive(true); nearEmpty.text = T("보물찾기를 불러오지 못했어요", "Couldn't load treasure hunts", "宝探しを読み込めませんでした", "无法加载寻宝活动", "No se pudieron cargar las búsquedas del tesoro"); } yield break; }
        rulesAccepted = j["host_rules"]["accepted"].AsBool;
        rulesVersion = j["host_rules"]["version"].AsInt > 0 ? j["host_rules"]["version"].AsInt : 1;
        bool enabled = j["prefs"]["enabled"].AsBool || !j["prefs"].HasKey("enabled");
        R0926HuntPrefs.Take(j["prefs"]);
        R0926HuntList.Take(j);   // 장소 목록의 보물찾기 줄도 같은 응답으로
        if (offBox != null) offBox.SetActive(!enabled);

        ClearRows(nearList, mineList);
        var near = j["hunts"].AsArray;
        foreach (JSONNode h in near) AddRow(nearList, h);
        if (nearHead != null) nearHead.text = T("근처 보물찾기", "Nearby hunts", "近くの宝探し", "附近的寻宝", "Búsquedas cercanas") + (near.Count > 0 ? "  " + near.Count : "");
        if (nearEmpty != null)
        {
            nearEmpty.gameObject.SetActive(near.Count == 0 && enabled);   // 꺼 두었으면 offBox 가 이미 말한다 (같은 말이 두 줄 나왔다)
            nearEmpty.text = T("근처에 열린 보물찾기가 없어요 — 첫 보물을 숨겨 보세요", "No hunts nearby yet — hide the first treasure", "近くで開催中の宝探しはありません — 最初の宝を隠してみましょう", "附近还没有寻宝活动 — 来藏第一个宝藏吧", "Aún no hay búsquedas cerca — esconde el primer tesoro");
        }
        var seen = new HashSet<int>();
        int mine = 0;
        foreach (var key in new[] { "hosted", "joined" })
            foreach (JSONNode h in j[key].AsArray)
                if (seen.Add(h["id"].AsInt)) { AddRow(mineList, h); mine++; }
        if (mineHead != null) mineHead.SetActive(mine > 0);
        // 참가 중인 게임 · 내가 연 게임 — 보물 근처에 가면 AR 로 띄운다 (주최자는 보이기만)
        foreach (var key in new[] { "hosted", "joined" })
            foreach (JSONNode h in j[key].AsArray) R0926HuntTreasure.Track(h);
    }

    private void ClearRows(params RectTransform[] lists)
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            var g = spawned[i];
            if (g == null) { spawned.RemoveAt(i); continue; }
            foreach (var l in lists)
                if (l != null && g.transform.parent == l) { Destroy(g); spawned.RemoveAt(i); break; }
        }
    }

    private void AddRow(RectTransform list, JSONNode h)
    {
        if (list == null || rowTemplate == null) return;
        var row = Instantiate(rowTemplate, list);
        row.SetActive(true);
        spawned.Add(row);
        string status = h["status"];
        var dot = row.transform.Find("Dot")?.GetComponent<Image>();
        if (dot != null) dot.color = status == "live" ? Teal : status == "scheduled" ? Amber : Muted;
        SetText(row, "Title", (h["official"].AsBool ? "★ " : "") + h["title"]);
        if (h["mode"] == "code" && row.transform.Find("Lock") is RectTransform lockIcon)   // 비밀번호를 건 보물 — 제목 앞 작은 자물쇠
        {
            lockIcon.gameObject.SetActive(true);
            if (row.transform.Find("Title") is RectTransform tr) tr.offsetMin += new Vector2(lockIcon.sizeDelta.x + 10, 0);
        }
        SetText(row, "Sub", Summary(h));
        var me = h["me"];
        string badge = h["is_host"].AsBool ? T("주최", "Host", "主催", "主办", "Tu juego")
                     : me["rank"].AsInt > 0 ? T("찾음", "Found", "発見", "已找到", "Encontrado")
                     : status == "ended" || status == "cancelled" ? T("끝", "Ended", "終了", "已结束", "Terminado")
                     : me["joined"].AsBool ? T("참가 중", "Joined", "参加中", "参加中", "Participando")
                     : h["full"].AsBool ? T("마감", "Full", "満員", "已满", "Completo") : T("보기", "View", "見る", "查看", "Ver");
        SetText(row, "Badge", badge);
        int id = h["id"].AsInt;
        var b = row.GetComponent<Button>();
        if (b != null) Wire(b, () => StartCoroutine(OpenDetail(id)));
    }

    private static void SetText(GameObject root, string child, string text)
    {
        var t = root.transform.Find(child)?.GetComponent<Text>();
        if (t != null) t.text = text ?? "";
    }

    private static string Summary(JSONNode h)
    {
        var parts = new List<string>();
        if (h.HasKey("distance_m")) parts.Add(Dist(h["distance_m"].AsFloat) + (h["coord_mode"] == "approx" ? T(" 근처", " away", " 付近", " 附近", " aprox.") : ""));
        else if (h.HasKey("distance_text")) parts.Add(h["distance_text"]);
        parts.Add(T("참가 ", "Players ", "参加 ", "参加 ", "Jugadores ") + PlayerCount(h));
        int wm = h["winners_max"].AsInt;
        parts.Add(wm > 0 ? T(h["found"].AsInt + "/" + wm + "명 찾음", h["found"].AsInt + "/" + wm + " found", h["found"].AsInt + "/" + wm + "人発見", h["found"].AsInt + "/" + wm + " 人找到", h["found"].AsInt + "/" + wm + " encontrados") : T(h["found"].AsInt + "명 찾음", h["found"].AsInt + " found", h["found"].AsInt + "人発見", h["found"].AsInt + " 人找到", h["found"].AsInt + " encontrados"));
        parts.Add(TimeText(h));
        return string.Join(" · ", parts);
    }

    // 참가 수 — 상한이 있으면 'N/M' (관리자 공식 게임은 상한 없이 'N')
    private static string PlayerCount(JSONNode h)
    {
        int mp = h["max_players"].AsInt;
        return h["participants"].AsInt + (mp > 0 ? "/" + mp : "");
    }

    private static string Dist(float m) => m < 1000f ? Mathf.RoundToInt(m) + "m" : (m / 1000f).ToString(m < 10000f ? "0.0" : "0", CultureInfo.InvariantCulture) + "km";

    private static DateTime? When(JSONNode n)
    {
        if (n == null || n.IsNull || string.IsNullOrEmpty(n.Value)) return null;
        if (DateTime.TryParse(n.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)) return t;
        return null;
    }

    // 분은 올림 — 방금 만들거나 늘린 게임이 '1시간 59분'이 아니라 '2시간'으로 보이게. 10시간 안쪽은 분까지
    private static string Span(TimeSpan s)
    {
        int m = Mathf.Max(1, Mathf.CeilToInt((float)s.TotalMinutes));
        if (m < 60) return m + T("분", "m", "分", "分钟", " min");
        if (m < 600) return m / 60 + T("시간", "h", "時間", "小时", " h") + (m % 60 > 0 ? " " + m % 60 + T("분", "m", "分", "分钟", " min") : "");
        if (m < 2880) return (m + 30) / 60 + T("시간", "h", "時間", "小时", " h");
        return m / 1440 + T("일", "d", "日", "天", " días");
    }

    private static string TimeText(JSONNode h)
    {
        var now = DateTime.UtcNow;
        string status = h["status"];
        if (status == "scheduled") { var s = When(h["starts_at"]); return s.HasValue ? T(Span(s.Value - now) + " 뒤 시작", "Starts in " + Span(s.Value - now), Span(s.Value - now) + "後に開始", Span(s.Value - now) + "后开始", "Empieza en " + Span(s.Value - now)) : T("곧 시작", "Soon", "まもなく開始", "即将开始", "Pronto"); }
        if (status == "live") { var e = When(h["ends_at"]); return e.HasValue ? T(Span(e.Value - now) + " 남음", Span(e.Value - now) + " left", "残り" + Span(e.Value - now), "剩余 " + Span(e.Value - now), "Quedan " + Span(e.Value - now)) : T("진행 중", "Live", "開催中", "进行中", "En curso"); }
        return status == "cancelled" ? T("취소됨", "Cancelled", "中止", "已取消", "Cancelado") : T("끝남", "Ended", "終了", "已结束", "Terminado");
    }

    // ── 숨기기 ────────────────────────────────────────────
    private void OnHero()
    {
        if (!R0926HuntApi.CanCall) { if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup(); return; }
        if (!rulesAccepted) StartCoroutine(OpenRules(true));
        else OpenForm();
    }

    private void OpenForm()
    {
        Show(form);
        if (formMsg != null) formMsg.text = "";
        pickNote = null;
        PaintCodeBox();
        PaintShape();
        PaintAudience();
        PaintSubmit();
    }

    // 기본값 (10-26 대표님) — 보물상자 · 먼저 터치 · 당첨 1명 · 참가 100명 · 보이는 거리 10m · 참가 범위 10km · 모두 · 바로 · 일주일
    private void BuildChips()
    {
        Chips(shapeChips, "shape", Shapes, "chest");
        Chips(modeChips, "mode", Modes, "touch");
        Chips(winnerChips, "winners", Winners, "1");
        Chips(playerChips, "players", Players, "100");
        Chips(visibleChips, "visible", Visibles, "10");
        Chips(radiusChips, "radius", Radii, "10");
        Chips(audienceChips, "audience", Audiences, "all");
        Chips(startChips, "start", Starts, "0");
        Chips(durationChips, "duration", Durations, "10080");
    }

    private void Chips(RectTransform box, string key, (string v, string ko, string en, string ja, string zh, string es)[] defs, string initial)
    {
        if (box == null || chipTemplate == null) return;
        picked[key] = initial;
        foreach (var d in defs)
        {
            var c = Instantiate(chipTemplate, box);
            c.name = "Chip_" + d.v;
            c.SetActive(true);
            SetText(c, "Label", T(d.ko, d.en, d.ja, d.zh, d.es));
            string v = d.v;
            Wire(c.GetComponent<Button>(), () =>
            {
                picked[key] = v; PaintChips(box, key);
                if (key == "mode") PaintCodeBox();
                else if (key == "shape") { pickNote = null; PaintShape(); }
                else if (key == "audience") PaintAudience();
            });
        }
        PaintChips(box, key);
    }

    private void PaintChips(RectTransform box, string key)
    {
        foreach (Transform c in box)
        {
            if (!c.gameObject.activeSelf || !c.name.StartsWith("Chip_")) continue;
            bool on = c.name == "Chip_" + picked[key];
            var img = c.GetComponent<Image>();
            if (img != null) img.color = on ? ChipOn : ChipOff;
            var l = c.Find("Label")?.GetComponent<Text>();
            if (l != null) { l.color = on ? Gold : Muted; l.fontStyle = on ? FontStyle.Bold : FontStyle.Normal; }
        }
    }

    private void PaintCodeBox()
    {
        bool code = picked.TryGetValue("mode", out var m) && m == "code";
        if (codeBox != null) codeBox.SetActive(code);
        if (shapePreview != null) shapePreview.Locked = code;   // 미리보기 보물에도 자물쇠
    }

    private void PaintGps()
    {
        if (!TryLocation(out _, out _, out float acc)) { gpsText.text = T("위치를 찾는 중이에요…", "Finding your location…", "位置を確認しています…", "正在获取位置…", "Buscando tu ubicación…"); gpsText.color = Muted; return; }
        int a = Mathf.RoundToInt(acc);
        bool good = acc > 0f && acc <= 30f;
        gpsText.text = good ? T("지금 서 있는 자리에 숨겨요 · 정확도 " + a + "m", "Hidden right where you stand · accuracy " + a + "m", "今立っている場所に隠します · 精度 " + a + "m", "藏在你现在站的位置 · 精度 " + a + "m", "Se esconde justo donde estás · precisión " + a + "m")
                            : T("정확도 " + a + "m — 하늘이 보이는 곳에서 잠시 기다려 주세요 (30m 이내)", "Accuracy " + a + "m — wait outdoors (need 30m)", "精度 " + a + "m — 空が見える場所で少しお待ちください（30m以内）", "精度 " + a + "m — 请在能看到天空的地方稍等（需 30m 以内）", "Precisión " + a + "m — espera al aire libre (máx. 30m)");
        gpsText.color = good ? Teal : Amber;
    }

    private void PaintSubmit()
    {
        string state = uploading ? T("올리는 중…", "Uploading…", "アップロード中…", "正在上传…", "Subiendo…")
                     : busy ? T("숨기는 중…", "Hiding…", "隠しています…", "正在藏宝…", "Escondiendo…") : null;
        if (submitLabel != null) submitLabel.text = state ?? T("이 자리에 보물 숨기기", "Hide the treasure here", "ここに宝を隠す", "在这里藏宝", "Esconder el tesoro aquí");
        if (reviewGoLabel != null) reviewGoLabel.text = state ?? T("숨기기", "Hide it", "隠す", "藏起来", "Esconderlo");
        if (reviewEdit != null) reviewEdit.interactable = !busy;
    }

    private void PaintAudience()
    {
        if (audienceNote != null) audienceNote.text = AudienceText(picked.TryGetValue("audience", out var a) ? a : "all");
    }

    private static string AudienceText(string a) => a switch
    {
        "friends" => T("나와 서로 팔로우하는 사람만 보고 참가할 수 있어요", "Only people who follow each other with you can see and join", "相互フォローの人だけが見て参加できます", "只有与你互相关注的人可以看到并参加", "Solo quienes os seguís mutuamente pueden verlo y unirse"),
        "follower" => T("나를 팔로우하는 사람만 보고 참가할 수 있어요 (맞팔 포함)", "Only your followers can see and join (mutuals included)", "あなたをフォローしている人だけが見て参加できます（相互フォローを含む）", "只有关注你的人可以看到并参加（含互关）", "Solo tus seguidores pueden verlo y unirse (mutuos incluidos)"),
        "code" => T("목록·알림에 나오지 않아요 — 숨기면 참가 코드가 만들어지고, 코드나 공유 링크를 받은 사람만 어디서든 참가할 수 있어요",
                    "Hidden from lists and alerts — a join code is made when you hide it, and only people with the code or link can join, from anywhere",
                    "一覧・通知には出ません — 隠すと参加コードが作られ、コードか共有リンクを受け取った人だけがどこからでも参加できます",
                    "不会出现在列表和通知中 — 藏好后会生成参加代码，只有收到代码或分享链接的人才能在任何地方参加",
                    "No aparece en listas ni avisos: al esconderlo se crea un código y solo quien tenga el código o el enlace puede unirse, desde cualquier lugar"),
        _ => T("참가 범위 안의 우팡 사용자라면 누구나 보고 참가할 수 있어요", "Anyone on WOOPANG within the join area can see and join", "参加範囲内のWOOPANGユーザーなら誰でも見て参加できます", "参加范围内的任何 WOOPANG 用户都可以看到并参加", "Cualquier usuario de WOOPANG dentro del área puede verlo y unirse"),
    };

    // ── 보물 모양 ─────────────────────────────────────────
    private string Shape => picked.TryGetValue("shape", out var s) ? s : "chest";

    private void PaintShape()
    {
        string k = Shape;
        bool have = objData != null && objKind == k;
        if (shapePreview != null)
        {
            if (have && k == "photo") shapePreview.ShowPhoto(objTex);
            else if (have && k == "model") shapePreview.ShowModel(objData);
            else shapePreview.Show(k == "model" ? "chest" : k);   // 3D 모델을 아직 안 골랐으면 — 못 열 때 보이는 보물상자
        }
        PaintPick();
    }

    private void PaintPick()
    {
        string k = Shape;
        bool file = k == "photo" || k == "model";
        if (pickButton != null) pickButton.gameObject.SetActive(file);
        if (pickCaption != null) pickCaption.gameObject.SetActive(file);
        if (!file) return;
        bool have = objData != null && objKind == k;
        if (pickLabel != null)
            pickLabel.text = k == "photo" ? (have ? T("다른 사진 고르기", "Choose another photo", "別の写真を選ぶ", "选择其他照片", "Elegir otra foto") : T("앨범에서 사진 고르기", "Choose a photo", "アルバムから写真を選ぶ", "从相册选择照片", "Elegir una foto"))
                                          : (have ? T("다른 3D 모델 고르기", "Choose another 3D model", "別の3Dモデルを選ぶ", "选择其他 3D 模型", "Elegir otro modelo 3D") : T("3D 모델(GLB) 고르기", "Choose a 3D model (GLB)", "3Dモデル（GLB）を選ぶ", "选择 3D 模型（GLB）", "Elegir un modelo 3D (GLB)"));
        if (pickCaption == null) return;
        bool warn = !string.IsNullOrEmpty(pickNote);
        pickCaption.color = warn ? Amber : Muted;
        if (warn) pickCaption.text = pickNote;
        else if (have && k == "photo") pickCaption.text = T("사진", "Photo", "写真", "照片", "Foto") + (objTex != null ? " " + objTex.width + "×" + objTex.height : "") + " · " + Size(objData.Length);
        else if (have) pickCaption.text = T("3D 모델", "3D model", "3Dモデル", "3D 模型", "Modelo 3D") + " · " + Size(objData.Length)
                                          + (shapePreview != null && shapePreview.Loading ? T(" · 불러오는 중…", " · loading…", " · 読み込み中…", " · 正在加载…", " · cargando…") : "");
        else pickCaption.text = k == "photo" ? T("고른 사진을 액자에 끼워 숨겨요", "Your photo goes in a frame as the treasure", "選んだ写真を額に入れて隠します", "所选照片会装进相框藏起来", "Tu foto se esconde en un marco")
                                             : T("GLB 한 파일 · 10MB 까지 — 고르면 여기서 먼저 보여 줘요", "One GLB file, up to 10MB — previewed here first", "GLB 1ファイル・10MBまで — 選ぶとここで先に確認できます", "单个 GLB 文件，最大 10MB — 选好后先在这里预览", "Un solo archivo GLB, hasta 10 MB — se previsualiza aquí");
    }

    private static string Size(int n) => n < 1024 * 1024 ? Mathf.Max(1, n / 1024) + "KB" : (n / 1048576f).ToString("0.0", CultureInfo.InvariantCulture) + "MB";

    private void OnPick()
    {
        if (busy) return;
        // 앞서 연 고르기 창의 답이 끝내 안 왔으면(창이 못 떴거나 앱 전환으로 잃음) 버리고 새로 연다 (CubeUploadManager 와 같이)
        if (pick.Waiting && !pick.TryAbandon()) { FormMsg(R0926PickerWait.BusyText); return; }
        StartCoroutine(Shape == "photo" ? PickPhoto() : PickModel());
    }

    private IEnumerator PickPhoto()
    {
        if (!R0926PickerWait.PluginFree(R0926PickerWait.Source.Gallery)) { FormMsg(R0926PickerWait.BusyText); yield break; }
        bool answered = false;
        string path = null;
        int t = pick.Begin(R0926PickerWait.Source.Gallery);
        try
        {
            NativeGallery.GetImageFromGallery(p => { if (!pick.End(t)) return; path = p; answered = true; },
                T("보물 사진 고르기", "Choose a treasure photo", "宝の写真を選ぶ", "选择宝藏照片", "Elige la foto del tesoro"), "image/*");
        }
        catch (Exception) { pick.End(t); answered = true; }
        yield return new WaitUntil(() => answered || !pick.Holds(t));
        if (!answered || string.IsNullOrEmpty(path)) yield break;   // 표를 버렸거나 고르지 않고 닫았다 — 고른 것은 그대로
        if (pickCaption != null) { pickCaption.text = T("사진을 읽는 중…", "Reading the photo…", "写真を読み込み中…", "正在读取照片…", "Leyendo la foto…"); pickCaption.color = Muted; }
        yield return null;   // 사진 읽기는 화면을 멈춘다 — 안내를 먼저 그린다
        Texture2D tex = null;
        byte[] jpg = null;
        try
        {
            tex = NativeGallery.LoadImageAtPath(path, 1280, false, false);   // 서버도 긴 변 1280 으로 줄인다
            if (tex != null) jpg = tex.EncodeToJPG(85);
        }
        catch (Exception) { jpg = null; }
        if (jpg == null)
        {
            if (tex != null) Destroy(tex);
            PickFailed(T("이 사진은 열 수 없어요 — 다른 사진을 골라 주세요", "Couldn't open this photo — choose another", "この写真は開けません — 別の写真を選んでください", "无法打开这张照片 — 请选择其他照片", "No se pudo abrir esta foto — elige otra"));
            yield break;
        }
        TakeObject("photo", jpg, tex);
    }

    private IEnumerator PickModel()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!NativeFilePicker.CheckPermission())
        {
            var perm = NativeFilePicker.Permission.Denied;
            bool asked = false;
            NativeFilePicker.RequestPermissionAsync(p => { perm = p; asked = true; });
            yield return new WaitUntil(() => asked);
            if (perm != NativeFilePicker.Permission.Granted) { PickFailed(T("파일을 열 권한이 없어요 — 설정에서 허용해 주세요", "No permission to open files — allow it in Settings", "ファイルを開く権限がありません — 設定で許可してください", "没有打开文件的权限 — 请在设置中允许", "Sin permiso para abrir archivos — actívalo en Ajustes")); yield break; }
        }
        string[] types = { "model/gltf-binary", "application/octet-stream", "*/*" };
#else
        string[] types = { "com.khronos.glb", "public.data", "public.item", "public.content" };
#endif
        if (!R0926PickerWait.PluginFree(R0926PickerWait.Source.File)) { FormMsg(R0926PickerWait.BusyText); yield break; }
        bool answered = false;
        string path = null;
        int t = pick.Begin(R0926PickerWait.Source.File);
        try { NativeFilePicker.PickFile(p => { if (!pick.End(t)) return; path = p; answered = true; }, types); }
        catch (Exception) { pick.End(t); answered = true; }
        yield return new WaitUntil(() => answered || !pick.Holds(t));
        if (!answered || string.IsNullOrEmpty(path)) yield break;
        byte[] data = null;
        string err = null;
        try
        {
            if (new FileInfo(path).Length > ModelMax) err = R0926HuntApi.ErrorText("file_size", Lang);
            else data = File.ReadAllBytes(path);
        }
        catch (Exception) { err = T("파일을 읽지 못했어요 — 다시 골라 주세요", "Couldn't read the file — choose it again", "ファイルを読み込めませんでした — もう一度選んでください", "无法读取文件 — 请重新选择", "No se pudo leer el archivo — elígelo de nuevo"); }
        if (err == null && !IsGlb(data)) err = R0926HuntApi.ErrorText("file_type", Lang);
        if (err != null) { PickFailed(err); yield break; }
        TakeObject("model", data, null);
    }

    // GLB 2.0 한 파일 — 서버(_glb_ok)와 같은 앞머리 검사 (바깥 파일을 부르는지는 서버가 본다)
    private static bool IsGlb(byte[] d) =>
        d != null && d.Length >= 20 && d[0] == 0x67 && d[1] == 0x6C && d[2] == 0x54 && d[3] == 0x46
        && BitConverter.ToUInt32(d, 4) == 2 && BitConverter.ToUInt32(d, 8) == (uint)d.Length;

    private void TakeObject(string kind, byte[] data, Texture2D tex)
    {
        ClearObject();
        objKind = kind; objData = data; objTex = tex; pickNote = null;
        picked["shape"] = kind;
        if (shapeChips != null) PaintChips(shapeChips, "shape");
        PaintShape();
    }

    // 고르다 못 연 파일 — 앞서 고른 것은 그대로 두고 칸 아래에 알린다
    private void PickFailed(string note)
    {
        pickNote = note;
        PaintPick();
    }

    private void ClearObject()
    {
        if (objTex != null) Destroy(objTex);
        objTex = null; objData = null; objKind = null; objUrl = null;
    }

#if UNITY_EDITOR
    /// <summary>플레이 시험 — 고르기 창 없이 사진(JPG·PNG)·3D 모델(GLB)을 고른 것처럼</summary>
    public void EditorPick(string kind, byte[] bytes)
    {
        if (kind == "photo")
        {
            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes)) TakeObject("photo", tex.EncodeToJPG(85), tex);
            else { Destroy(tex); PickFailed("photo"); }
        }
        else if (IsGlb(bytes)) TakeObject("model", bytes, null);
        else PickFailed(R0926HuntApi.ErrorText("file_type", Lang));
    }
#endif

    // 서버처럼 빈칸을 빼고 센 글자 수 (두 칸을 쓰는 이모지도 한 글자)
    private static int Letters(string s)
    {
        int n = 0;
        foreach (char c in s) if (!char.IsWhiteSpace(c) && !char.IsLowSurrogate(c)) n++;
        return n;
    }

    // ── 숨기기 → 확인 카드 → 올리기 (10-26 대표님 — 올리기 전에 한 번 보고 '숨기기') ──
    private void OnSubmit()
    {
        if (busy || !Validate(out _, out _, out _)) return;
        if (formMsg != null) formMsg.text = "";   // 앞서 틀렸던 안내 — 고치기로 돌아와도 남지 않게
        OpenReview();
    }

    private static string Field(InputField f) => f != null ? f.text.Trim() : "";

    // 양식 확인 — 틀리면 알림 줄(확인 카드가 떠 있으면 카드 안)에 적고 false
    private bool Validate(out double lat, out double lon, out float acc)
    {
        if (!TryLocation(out lat, out lon, out acc)) { Warn(T("위치를 아직 못 받았어요", "Location not available yet", "まだ位置情報を取得できていません", "尚未获取到位置", "Aún no hay ubicación")); return false; }
        if (Field(titleInput).Length == 0) { Warn(R0926HuntApi.ErrorText("title", Lang)); return false; }
        string shape = Shape;
        if ((shape == "photo" || shape == "model") && (objData == null || objKind != shape))
        {
            Warn(shape == "photo" ? T("보물로 쓸 사진을 골라 주세요", "Choose a photo for the treasure", "宝にする写真を選んでください", "请选择用作宝藏的照片", "Elige una foto para el tesoro")
                                  : T("보물로 쓸 3D 모델(GLB)을 골라 주세요", "Choose a 3D model (GLB) for the treasure", "宝にする3Dモデル（GLB）を選んでください", "请选择用作宝藏的 3D 模型（GLB）", "Elige un modelo 3D (GLB) para el tesoro"));
            return false;
        }
        if (shape == "model" && shapePreview != null && shapePreview.Loading) { Warn(T("3D 모델을 불러오는 중이에요 — 잠시만요", "Still loading the 3D model — one moment", "3Dモデルを読み込み中です — 少々お待ちください", "正在加载 3D 模型 — 请稍候", "Cargando el modelo 3D — un momento")); return false; }
        // 비밀번호는 4자 — 비밀번호 힌트는 선택 (10-26 대표님 — 힌트는 모두 선택으로 통일 · 서버도 같은 규칙)
        if (picked["mode"] == "code" && Letters(codeInput != null ? codeInput.text : "") != R0926HuntApi.CodeLen) { Warn(R0926HuntApi.ErrorText("code", Lang)); return false; }
        return true;
    }

    private void Warn(string s)
    {
        if (review != null && review.activeSelf && reviewMsg != null) { reviewMsg.text = s; reviewMsg.gameObject.SetActive(true); }
        else FormMsg(s);
    }

    private void OpenReview()
    {
        postedId = 0;
        if (review == null) { StartCoroutine(Post()); return; }   // 확인 카드를 아직 안 만든 장면 — 예전처럼 바로
        Reviewing = true;
        review.SetActive(true);
        PaintReview();
    }

    private void HideReview()
    {
        Reviewing = false;
        if (review != null) review.SetActive(false);
    }

    // 바깥(어두운 곳)·뒤로가기 — 올리는 중엔 그대로, 숨긴 뒤면 '확인'과 같이, 아니면 '고치기'와 같이
    private void OnReviewBack()
    {
        if (busy) return;
        if (postedId > 0) CloseDone(); else HideReview();
    }

    private void CloseDone()
    {
        int id = postedId;
        postedId = 0;
        HideReview();
        if (id > 0) StartCoroutine(OpenDetail(id));
    }

    private static string Pick((string v, string ko, string en, string ja, string zh, string es)[] defs, string v)
    {
        foreach (var d in defs) if (d.v == v) return T(d.ko, d.en, d.ja, d.zh, d.es);
        return v;
    }

    private static string Cut(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";

    // 확인 카드 — 고른 그대로 한 줄씩 (숨긴 뒤엔 시간 늘리기·끝내기만 된다)
    private void PaintReview()
    {
        TryLocation(out _, out _, out float acc);
        string none = T("없음", "None", "なし", "无", "Ninguno");
        string prize = Field(prizeInput), hint = Field(hintInput), question = Field(questionInput);
        bool locked = picked["mode"] == "code", code = picked["audience"] == "code";
        var rows = new List<(string, string)>
        {
            (T("제목", "Title", "タイトル", "标题", "Título"), Field(titleInput)),
            (T("상품", "Prize", "賞品", "奖品", "Premio"), prize.Length > 0 ? prize : none),
            (T("힌트", "Hint", "ヒント", "提示", "Pista"), hint.Length > 0 ? Cut(hint, 60) : none),
            (T("보물 모양", "Shape", "宝の形", "宝藏外形", "Forma"), Pick(Shapes, Shape)),
            (T("찾는 방식", "How", "見つけ方", "寻宝方式", "Cómo"), locked ? T("비밀번호 ", "Password ", "パスワード ", "密码 ", "Contraseña ") + Field(codeInput) + (question.Length > 0 ? "\n" + T("힌트 ", "Hint ", "ヒント ", "提示 ", "Pista ") + Cut(question, 40) : "") : Pick(Modes, "touch")),
            (T("당첨 · 참가", "Winners · players", "当選 · 参加", "中奖 · 参加", "Ganadores · jugadores"), Pick(Winners, picked["winners"]) + " · " + T("최대 ", "up to ", "最大 ", "最多 ", "hasta ") + Pick(Players, picked["players"])),
            (T("보이는 거리", "Visible within", "見える距離", "可见距离", "Visible a"), Pick(Visibles, picked["visible"])),
            (T("참가 범위", "Join area", "参加範囲", "参加范围", "Área"), code ? T("코드를 받은 사람은 어디서든", "Anywhere, with the code", "コードを受け取った人はどこからでも", "收到代码的人可在任何地方参加", "Desde cualquier lugar, con el código") : Pick(Radii, picked["radius"])),
            (T("누구에게", "Who can join", "参加できる人", "谁可以参加", "Quién puede unirse"), Pick(Audiences, picked["audience"])),
            (T("시작 · 진행", "Start · length", "開始 · 期間", "开始 · 时长", "Inicio · duración"), Pick(Starts, picked["start"]) + " · " + Pick(Durations, picked["duration"])),
            (T("숨길 자리", "Spot", "隠す場所", "藏宝位置", "Lugar"), T("지금 서 있는 자리", "Where you stand now", "今立っている場所", "你现在站的位置", "Donde estás ahora") + (acc > 0f ? T(" · 정확도 ", " · accuracy ", " · 精度 ", " · 精度 ", " · precisión ") + Mathf.RoundToInt(acc) + "m" : "")),
        };
        if (reviewHead != null) reviewHead.text = T("이대로 숨길까요?", "Hide it like this?", "この内容で隠しますか？", "就这样藏起来吗？", "¿Lo escondemos así?");
        if (reviewSub != null) reviewSub.text = T("숨긴 뒤에는 내용을 고칠 수 없어요 — 한 번 더 확인해 주세요", "You can't edit it after hiding — please check once more", "隠した後は内容を修正できません — もう一度確認してください", "藏好后无法修改内容 — 请再确认一次", "No podrás editarlo después — revísalo una vez más");
        if (reviewNote != null) reviewNote.text = AudienceText(picked["audience"]);
        if (reviewWarn != null)
        {
            reviewWarn.text = T("낚시성·도박·유해한 게임은 내려가고, 계정이 정지되거나 삭제될 수 있어요",
                                "Bait, gambling or harmful games are taken down, and the account may be suspended or deleted",
                                "釣り・賭博・有害なゲームは取り下げられ、アカウントが停止または削除されることがあります",
                                "诱导、赌博或有害的游戏会被撤下，账号可能被停用或删除",
                                "Los juegos engañosos, de apuestas o dañinos se retiran, y la cuenta puede suspenderse o eliminarse");
            reviewWarn.gameObject.SetActive(true);
        }
        if (reviewMsg != null) { reviewMsg.text = ""; reviewMsg.gameObject.SetActive(false); }
        if (reviewCodeBox != null) reviewCodeBox.SetActive(false);
        if (reviewRows != null)
        {
            reviewRows.gameObject.SetActive(true);
            for (int i = reviewRows.childCount - 1; i >= 0; i--)
            {
                var c = reviewRows.GetChild(i).gameObject;
                if (c != reviewRowTemplate) { c.SetActive(false); Destroy(c); }
            }
            if (reviewRowTemplate != null)
                foreach (var (k, v) in rows)
                {
                    var g = Instantiate(reviewRowTemplate, reviewRows);
                    g.SetActive(true);
                    SetText(g, "Label", k);
                    SetText(g, "Value", v);
                }
        }
        Set(reviewEdit, true); Set(reviewGo, true); Set(reviewShare, false); Set(reviewDone, false);
        PaintSubmit();
    }

    // 숨긴 뒤 — 같은 카드에 참가 코드 · 공유하기 · 확인 (참가 코드 게임은 코드·링크를 받은 사람만 들어온다)
    private void PaintDone()
    {
        bool hasCode = !string.IsNullOrEmpty(postedCode);
        if (reviewHead != null) reviewHead.text = T("보물을 숨겼어요!", "Treasure hidden!", "宝を隠しました！", "宝藏已藏好！", "¡Tesoro escondido!");
        if (reviewSub != null) reviewSub.text = postedTitle;
        if (reviewRows != null) reviewRows.gameObject.SetActive(false);
        if (reviewCodeBox != null) reviewCodeBox.SetActive(hasCode);
        if (reviewCode != null) reviewCode.text = postedCode ?? "";
        if (reviewNote != null) reviewNote.text = DoneText(postedAudience);
        if (reviewWarn != null) reviewWarn.gameObject.SetActive(false);
        if (reviewMsg != null) { reviewMsg.text = ""; reviewMsg.gameObject.SetActive(false); }
        Set(reviewEdit, false); Set(reviewGo, false); Set(reviewShare, true); Set(reviewDone, true);
    }

    private static string DoneText(string a) => a switch
    {
        "code" => T("이 코드나 공유 링크를 받은 사람만 어디서든 참가할 수 있어요 — 목록·알림에는 나오지 않으니 공유해 주세요",
                    "Only people with this code or the shared link can join, from anywhere — it isn't listed, so share it",
                    "このコードか共有リンクを受け取った人だけがどこからでも参加できます — 一覧に出ないので共有してください",
                    "只有收到此代码或分享链接的人才能在任何地方参加 — 不会出现在列表中，请分享出去",
                    "Solo quien tenga este código o el enlace puede unirse, desde cualquier lugar — no aparece en listas, así que compártelo"),
        "friends" => T("맞팔 친구에게만 보여요 — 링크를 받아도 맞팔이 아니면 참가할 수 없어요", "Only mutuals can see it — people without a mutual follow can't join, even with the link", "相互フォローの友だちにだけ表示されます — リンクを受け取っても相互フォローでなければ参加できません", "仅互关好友可见 — 即使收到链接，非互关也无法参加", "Solo lo ven tus mutuos — sin seguimiento mutuo no pueden unirse, aunque tengan el enlace"),
        "follower" => T("나를 팔로우하는 사람에게만 보여요 — 링크를 받은 사람도 팔로우하면 참가할 수 있어요", "Only your followers can see it — people with the link can join once they follow you", "あなたのフォロワーにだけ表示されます — リンクを受け取った人もフォローすれば参加できます", "仅关注你的人可见 — 收到链接的人关注你后即可参加", "Solo lo ven tus seguidores — quien tenga el enlace puede unirse si te sigue"),
        _ => T("참가 범위 안의 사람들이 목록에서 보고 참가해요 — 공유하면 더 많은 사람이 찾아와요", "People in the join area will find it in their list — share it to bring more", "参加範囲内の人が一覧で見つけて参加します — 共有するともっと多くの人が来ます", "参加范围内的人会在列表中看到并参加 — 分享出去会有更多人来", "La gente dentro del área lo verá en su lista — compártelo para atraer a más"),
    };

    private IEnumerator Post()
    {
        if (busy || postedId > 0) yield break;
        if (!Validate(out double lat, out double lon, out float acc)) yield break;   // 카드를 보는 사이 위치·고른 파일이 바뀌었을 수 있다
        string title = Field(titleInput), prize = Field(prizeInput), audience = picked["audience"];
        string shape = Shape;
        bool file = shape == "photo" || shape == "model";
        var b = new JSONObject();
        b["title"] = title;
        b["hint"] = Field(hintInput);
        b["mode"] = picked["mode"];
        if (picked["mode"] == "code")
        {
            b["code"] = Field(codeInput);
            b["question"] = Field(questionInput);
        }
        if (picked["winners"] == "all") b["winners_all"] = true; else b["winners"] = int.Parse(picked["winners"]);
        if (picked.TryGetValue("players", out var players)) b["max_players"] = int.Parse(players);
        b["prize"] = prize;
        b["visible_m"] = int.Parse(picked["visible"]);
        b["radius_km"] = float.Parse(picked["radius"], CultureInfo.InvariantCulture);
        b["coord_mode"] = "exact";   // 위치 공개 칩은 없앴다 — 늘 정확히 (10-26 대표님)
        b["audience"] = audience;
        b["start_in_min"] = int.Parse(picked["start"]);
        b["duration_min"] = int.Parse(picked["duration"]);
        b["lat"] = lat; b["lon"] = lon; b["acc"] = acc; b["mock"] = false;
        b["object_kind"] = shape;
        if (reviewMsg != null) reviewMsg.gameObject.SetActive(false);

        busy = true; PaintSubmit();
        if (file)
        {
            // 사진·3D 모델은 먼저 올리고 받은 주소로 숨긴다 — 숨기기가 막혀 다시 누르면 올린 주소를 그대로 쓴다
            if (string.IsNullOrEmpty(objUrl))
            {
                byte[] data = objData;
                uploading = true; PaintSubmit();
                long uc = 0; JSONNode uj = null;
                yield return R0926HuntApi.Upload("/api/hunts/object", data, shape == "photo" ? "treasure.jpg" : "treasure.glb", shape == "photo" ? "image/jpeg" : "model/gltf-binary",
                                                 new Dictionary<string, string> { { "kind", shape } }, (c, r) => { uc = c; uj = r; });
                uploading = false;
                string url = uc == 200 && uj != null && uj["ok"].AsBool ? (string)uj["url"] : null;
                if (string.IsNullOrEmpty(url) || !ReferenceEquals(objData, data))
                {
                    busy = false; PaintSubmit();
                    if (string.IsNullOrEmpty(url)) Warn(uc == 401 ? R0926HuntApi.ErrorText("login", Lang) : R0926HuntApi.ErrorText(uj != null ? (string)uj["error"] : null, Lang));
                    yield break;   // 올리는 사이 고른 파일이 바뀌었으면 — 바뀐 것을 보고 다시 누르게
                }
                objUrl = url;
            }
            b["object_url"] = objUrl;
        }
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Post("/api/hunts", b, (c, r) => { code = c; j = r; });
        busy = false; PaintSubmit();
        if (code == 200 && j["ok"].AsBool)
        {
            foreach (var f in new[] { titleInput, hintInput, codeInput, questionInput, prizeInput }) if (f != null) f.text = "";
            if (file)
            {
                // 고른 파일은 이 보물 것 — 다음 보물은 새로 고른다 (내 3D 모델은 AR 에서 다시 받지 않게 캐시에)
                if (shape == "model" && objData != null) GLBModelLoader.PreloadCache(R0926HuntApi.Url(objUrl), objData);
                ClearObject();
                picked["shape"] = "chest";
                if (shapeChips != null) PaintChips(shapeChips, "shape");
                PaintShape();
            }
            R0926HuntList.Stale();
            postedId = j["hunt"]["id"].AsInt;
            postedCode = j["join_code"];
            postedTitle = title; postedPrize = prize; postedAudience = audience;
            if (review != null && review.activeSelf) PaintDone();
            else CloseDone();
            yield break;
        }
        if (j["error"] == "need_rules") { HideReview(); rulesAccepted = false; StartCoroutine(OpenRules(true)); yield break; }
        Warn(code == 401 ? R0926HuntApi.ErrorText("login", Lang) : R0926HuntApi.ErrorText(j["error"], Lang));
    }

    // ── 공유하기 (10-26 대표님 — 카톡·문자·SNS 로 널리) ──
    // 링크는 woopang.com/hunt/<번호>(?c=참가 코드) — 앱이 있으면 '우팡 앱에서 열기', 없으면 스토어. 좌표는 싣지 않는다
    private void Share(int id, string code, string title, string prize)
    {
        if (id <= 0) return;
        bool hasCode = !string.IsNullOrEmpty(code);
        var lines = new List<string> { T("[우팡 보물찾기] ", "[WOOPANG treasure hunt] ", "[WOOPANG 宝探し] ", "[WOOPANG 寻宝] ", "[WOOPANG búsqueda del tesoro] ") + title };
        if (!string.IsNullOrEmpty(prize)) lines.Add(T("상품: ", "Prize: ", "賞品: ", "奖品: ", "Premio: ") + prize);
        if (hasCode) lines.Add(T("참가 코드: ", "Join code: ", "参加コード: ", "参加代码: ", "Código: ") + code);
        lines.Add(R0926HuntApi.Url("hunt/" + id) + (hasCode ? "?c=" + code : ""));
        if (R0926Share.Text(string.Join("\n", lines), T("보물찾기 공유하기", "Share treasure hunt", "宝探しを共有", "分享寻宝", "Compartir búsqueda del tesoro"))) return;
        string copied = T("링크를 복사했어요 — 붙여 넣어 보내 주세요", "Link copied — paste it to share", "リンクをコピーしました — 貼り付けて送ってください", "链接已复制 — 粘贴后发送即可", "Enlace copiado — pégalo para compartir");
        if (review != null && review.activeSelf) Warn(copied); else Toast(copied, 3f);
    }

    private void ShareCurrent()
    {
        if (current == null) return;
        Share(current["id"].AsInt, current["is_host"].AsBool ? (string)current["join_code"] : null, current["title"], current["prize"]);
    }

    // ── 공유 링크로 열기 (R0926HuntPush 가 부른다) ──
    private int linkId;
    private string linkCode;

    /// <summary>공유 링크(woopang://hunt?id=…&amp;c=…)로 들어왔다 — 코드가 있으면 바로 참가하고 그 게임 상세를, 없으면 상세만.
    /// 로그인 전이면 처음 화면에 코드를 넣어 두고, 로그인하면 이어서 연다</summary>
    public IEnumerator OpenLink(int id, string code)
    {
        if (!R0926HuntApi.CanCall)
        {
            linkId = id; linkCode = code;
            ShowHub();
            if (joinCodeInput != null && !string.IsNullOrEmpty(code)) joinCodeInput.text = code;
            Toast(T("로그인하면 이 보물찾기를 바로 열어요", "Log in to open this treasure hunt", "ログインするとこの宝探しを開きます", "登录后即可打开这个寻宝", "Inicia sesión para abrir esta búsqueda"), 3.5f);
            if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup();
            yield break;
        }
        linkId = 0; linkCode = null;
        if (string.IsNullOrEmpty(code)) { yield return OpenDetail(id); yield break; }
        long rc = 0; JSONNode j = null;
        var b = new JSONObject(); b["code"] = code;
        yield return R0926HuntApi.Post("/api/hunts/join_code", b, (c, r) => { rc = c; j = r; });
        if (rc == 200)
        {
            if (joinCodeInput != null) joinCodeInput.text = "";
            R0926HuntList.Stale();
            Toast(T("참가했어요!", "Joined!", "参加しました！", "已参加！", "¡Te uniste!"), 2.5f);
            yield return OpenDetail(j["hunt"]["id"].AsInt);
            yield break;
        }
        if (j["error"] == "host") { yield return OpenDetail(id); yield break; }   // 내가 연 게임 — 상세로
        ShowHub();
        if (joinCodeInput != null) joinCodeInput.text = code;
        Toast(R0926HuntApi.ErrorText(j["error"], Lang), 3.5f);
    }

    private void FormMsg(string s)
    {
        if (formMsg != null) { formMsg.text = s; formMsg.color = Amber; }
    }

    // ── 주최 수칙 ─────────────────────────────────────────
    private IEnumerator OpenRules(bool thenForm)
    {
        Show(rules);
        agreed = false;
        PaintAgree();
        if (agreeButton != null) agreeButton.gameObject.SetActive(thenForm || !rulesAccepted);
        if (agreeRow != null) agreeRow.gameObject.SetActive(thenForm || !rulesAccepted);
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Get("/api/hunts/rules?lang=" + Lang, (c, r) => { code = c; j = r; });   // 다섯 말 (예전엔 ko 가 아니면 en)
        if (code != 200) { Toast(R0926HuntApi.ErrorText(code == 401 ? "login" : "", Lang), 3f); yield break; }
        rulesVersion = j["version"].AsInt;
        rulesAccepted = j["accepted"].AsBool;
        if (rulesTitle != null) rulesTitle.text = j["title"];
        if (agreeLabel != null) agreeLabel.text = j["agree"];
        for (int i = rulesList.childCount - 1; i >= 0; i--)
        {
            var c = rulesList.GetChild(i).gameObject;
            if (c != ruleTemplate) Destroy(c);
        }
        int n = 0;
        foreach (JSONNode it in j["items"].AsArray)
        {
            var g = Instantiate(ruleTemplate, rulesList);
            g.SetActive(true);
            SetText(g, "Icon/Num", (++n).ToString());   // 서버 글의 이모지는 앱 글꼴이 못 그린다 — 번호 원으로
            SetText(g, "Title", it["title"]);
            SetText(g, "Body", it["body"]);
            if (it["warn"].AsBool && g.transform.Find("Title")?.GetComponent<Text>() is Text wt) wt.color = Amber;   // 계정 정지·삭제 경고 줄
        }
    }

    private void PaintAgree()
    {
        if (agreeCheck != null) agreeCheck.SetActive(agreed);
        if (agreeBox != null) agreeBox.color = agreed ? Gold : new Color(1f, 1f, 1f, 0.26f);
        if (agreeButtonBg != null) agreeButtonBg.color = agreed ? Gold : new Color(1f, 1f, 1f, 0.1f);
        if (agreeButton != null) agreeButton.interactable = agreed;
        var l = agreeButton != null ? agreeButton.GetComponentInChildren<Text>() : null;
        if (l != null) { l.text = T("동의하고 보물 숨기기", "Agree and hide a treasure", "同意して宝を隠す", "同意并藏宝", "Aceptar y esconder un tesoro"); l.color = agreed ? new Color(0.1f, 0.08f, 0.02f, 1f) : Muted; }
    }

    private IEnumerator Accept()
    {
        if (!agreed) yield break;
        long code = 0; JSONNode j = null;
        var b = new JSONObject(); b["version"] = rulesVersion;
        yield return R0926HuntApi.Post("/api/hunts/rules/accept", b, (c, r) => { code = c; j = r; });
        if (code == 200) { rulesAccepted = true; OpenForm(); }
        else Toast(R0926HuntApi.ErrorText(j["error"], Lang), 3f);
    }

    // ── 코드로 참가 ───────────────────────────────────────
    private IEnumerator JoinByCode()
    {
        if (!R0926HuntApi.CanCall) { if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup(); yield break; }
        string c6 = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
        if (c6.Length < 6) { Toast(T("참가 코드 6자리를 넣어 주세요", "Enter the 6-character code", "6桁の参加コードを入力してください", "请输入 6 位参加代码", "Introduce el código de 6 caracteres"), 3f); yield break; }
        long code = 0; JSONNode j = null;
        var b = new JSONObject(); b["code"] = c6;
        yield return R0926HuntApi.Post("/api/hunts/join_code", b, (c, r) => { code = c; j = r; });
        if (code == 200) { if (joinCodeInput != null) joinCodeInput.text = ""; R0926HuntList.Stale(); Toast(T("참가했어요!", "Joined!", "参加しました！", "已参加！", "¡Te uniste!"), 2.5f); StartCoroutine(OpenDetail(j["hunt"]["id"].AsInt)); }
        else Toast(R0926HuntApi.ErrorText(j["error"], Lang), 3f);
    }

    // ── 상세 ──────────────────────────────────────────────
    public IEnumerator OpenDetail(int id)
    {
        detailGen++;
        string q = TryLocation(out double lat, out double lon, out _) ? "?lat=" + Q(lat) + "&lon=" + Q(lon) : "";
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Get("/api/hunts/" + id + q, (c, r) => { code = c; j = r; });
        if (code != 200)
        {
            // 팔로워 게임 링크를 받았는데 아직 팔로우 전 — 알리고 주최자 프로필을 연다 (팔로우하면 참가, 10-26)
            string host = j["error"] == "followers_only" ? (string)j["host_id"] : null;
            bool follow = !string.IsNullOrEmpty(host) && ProfileManager.Instance != null;
            Toast(R0926HuntApi.ErrorText(j["error"], Lang) + (follow ? T(" — 프로필에서 팔로우해 주세요", " — follow them from their profile", " — プロフィールからフォローしてください", " — 请在主页关注", " — síguelo desde su perfil") : ""), 3f);
            if (!follow) yield break;
            yield return new WaitForSecondsRealtime(1.6f);   // 알림을 읽을 틈 — 프로필이 추가 창 위로 덮는다
            if (!isActiveAndEnabled) yield break;
#if UNITY_EDITOR
            EditorFollowHost = host;
#endif
            ProfileManager.Instance.ShowProfile(host);
            yield break;
        }
        current = j["hunt"];
        Show(detail);
        PaintDetail();
        nextNear = Time.unscaledTime + 0.3f;
        nearRank = -1;
        nextReload = Time.unscaledTime + 30f;
        // 알림으로 바로 들어오면 처음 화면(설정값이 같이 오는 곳)을 거치지 않는다 — '가까워지면 진동' 값을 따로 받아 둔다
        if (!R0926HuntPrefs.Known)
            yield return R0926HuntApi.Get("/api/hunts/prefs", (c, r) => { if (c == 200) R0926HuntPrefs.Take(r["prefs"]); });
    }

    /// <summary>그 게임 소식(알림)이 왔을 때 — 그 게임 상세를 보고 있으면 다시 받고 true (R0926HuntPush 가 부른다 · 알림은 띄우지 않는다)</summary>
    public bool RefreshIfShowing(int id)
    {
        if (detail == null || !detail.activeInHierarchy || current == null || current["id"].AsInt != id) return false;
        nextReload = Time.unscaledTime + 30f;
        StartCoroutine(ReloadDetail(id));
        return true;
    }

    // 보던 자리(스크롤)는 그대로 두고 상태·소식만 다시 그린다 — 코드 칸은 근접 확인이 정한 대로 남긴다
    private IEnumerator ReloadDetail(int id)
    {
        int gen = detailGen;
        string q = TryLocation(out double lat, out double lon, out _) ? "?lat=" + Q(lat) + "&lon=" + Q(lon) : "";
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Get("/api/hunts/" + id + q, (c, r) => { code = c; j = r; });
        if (code != 200 || gen != detailGen || busy || current == null || current["id"].AsInt != id || !detail.activeInHierarchy) yield break;
        bool wasNear = NearMode(current);
        bool codeOn = claimCodeBox != null && claimCodeBox.activeSelf;
        current = j["hunt"];
        PaintDetail();
        if (!NearMode(current)) yield break;
        if (wasNear) { if (claimCodeBox != null) claimCodeBox.SetActive(codeOn); }
        else { nextNear = Time.unscaledTime + 0.3f; nearRank = -1; }   // 방금 시작했다 — 근접 확인을 바로 (첫 확인은 기준만 잡는다)
    }

    private bool Typing() => (msgInput != null && msgInput.isFocused) || (claimCodeInput != null && claimCodeInput.isFocused);

    private void PaintDetail()
    {
        var h = current;
        R0926HuntTreasure.Track(h);   // 참가 중·아직 못 찾음이면 AR 보물을 지켜보고, 찾음·끝남이면 치운다
        var me = h["me"];
        bool host = h["is_host"].AsBool, joined = me["joined"].AsBool, found = me["rank"].AsInt > 0;
        string status = h["status"];
        bool open = status == "live" || status == "scheduled";
        if (dTitle != null) dTitle.text = (h["official"].AsBool ? "★ " : "") + h["title"];
        if (dStatus != null) { dStatus.text = TimeText(h); dStatus.color = status == "live" ? Teal : status == "scheduled" ? Amber : Muted; }
        var host_ = h["host"];
        if (dHostText != null)
        {
            string stars = host_["rating"].IsNull || host_["rating"].AsFloat <= 0 ? "" : "  ★" + host_["rating"].AsFloat.ToString("0.0", CultureInfo.InvariantCulture);
            dHostText.text = T("주최 ", "Host ", "主催 ", "主办 ", "Organiza ") + host_["username"] + stars + T("  · 주최 " + host_["hosted"].AsInt + "회", "  · hosted " + host_["hosted"].AsInt, "  · 主催 " + host_["hosted"].AsInt + "回", "  · 主办 " + host_["hosted"].AsInt + " 次", "  · organizó " + host_["hosted"].AsInt);
        }
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(h["hint"])) lines.Add(T("힌트  ", "Hint  ", "ヒント  ", "提示  ", "Pista  ") + h["hint"]);
        bool locked = h["mode"] == "code";
        lines.Add(T("찾는 방식  ", "How  ", "見つけ方  ", "寻宝方式  ", "Cómo  ") + (locked ? T("비밀번호 (4자)", "Password (4 characters)", "パスワード（4文字）", "密码（4 个字符）", "Contraseña (4 caracteres)") : T("먼저 터치한 사람", "First to touch", "先にタッチした人", "最先触碰的人", "Primero en tocar")));
        if (locked && !string.IsNullOrEmpty(h["question"])) lines.Add(T("비밀번호 힌트  ", "Password hint  ", "パスワードのヒント  ", "密码提示  ", "Pista de la contraseña  ") + h["question"]);
        int wm = h["winners_max"].AsInt;
        lines.Add(T("당첨  ", "Winners  ", "当選  ", "中奖  ", "Ganadores  ") + (wm > 0 ? T(wm + "명 · " + h["found"].AsInt + "명 찾음", wm + " · " + h["found"].AsInt + " found", wm + "人 · " + h["found"].AsInt + "人発見", wm + " 人 · " + h["found"].AsInt + " 人找到", wm + " · " + h["found"].AsInt + " encontrados") : T("찾는 사람 모두 · " + h["found"].AsInt + "명 찾음", "Everyone · " + h["found"].AsInt + " found", "見つけた人全員 · " + h["found"].AsInt + "人発見", "找到的人都中奖 · " + h["found"].AsInt + " 人找到", "Todos · " + h["found"].AsInt + " encontrados")));
        if (!string.IsNullOrEmpty(h["prize"])) lines.Add(T("상품  ", "Prize  ", "賞品  ", "奖品  ", "Premio  ") + h["prize"]);
        lines.Add(T("보이는 거리 ", "Visible within ", "見える距離 ", "可见距离 ", "Visible a ") + h["visible_m"].AsInt + "m" + (h["radius_km"].IsNull ? "" : T(" · 참가 범위 ", " · area ", " · 参加範囲 ", " · 参加范围 ", " · área ") + h["radius_km"].AsFloat.ToString("0.#", CultureInfo.InvariantCulture) + "km"));
        // 위치 공개 칩은 없앴다(10-26 — 늘 정확히). 예전에 대략·비공개로 숨긴 게임만 그 줄을 남긴다
        if (h["coord_mode"] != "exact") lines.Add(T("위치 공개  ", "Location  ", "位置の公開  ", "位置公开  ", "Ubicación  ") + (h["coord_mode"] == "approx" ? T("대략 " + h["approx_m"].AsInt + "m 원", "approx. " + h["approx_m"].AsInt + "m", "おおよそ " + h["approx_m"].AsInt + "m の円", "大致 " + h["approx_m"].AsInt + "m 范围", "aprox. " + h["approx_m"].AsInt + "m") : T("비공개", "hidden", "非公開", "不公开", "oculta")));
        if (host && !string.IsNullOrEmpty(h["join_code"])) lines.Add(T("참가 코드  ", "Join code  ", "参加コード  ", "参加代码  ", "Código  ") + h["join_code"]);
        bool full = h["full"].AsBool;
        lines.Add(T("참가  ", "Players  ", "参加  ", "参加  ", "Jugadores  ") + PlayerCount(h) + T("명", "", "人", "人", "") + (full ? T(" · 마감", " · full", " · 満員", " · 已满", " · completo") : ""));
        if (dInfo != null) dInfo.text = string.Join("\n", lines);
        if (dMine != null)
            dMine.text = host ? (open ? T("내가 연 게임이에요 — 보물 근처에 가면 내 보물이 보여요", "You're the host — your treasure shows up when you're near it", "あなたが主催したゲームです — 宝の近くに行くと自分の宝が見えます", "这是你主办的游戏 — 走近宝藏就能看到", "Organizas este juego — verás tu tesoro cuando estés cerca")
                                      : T("내가 연 게임이에요", "You're the host", "あなたが主催したゲームです", "这是你主办的游戏", "Organizas este juego"))
                       : found ? T(me["rank"].AsInt + "번째로 찾았어요!", "You found it (#" + me["rank"].AsInt + ")", me["rank"].AsInt + "番目に見つけました！", "你第 " + me["rank"].AsInt + " 个找到了！", "¡Lo encontraste! (#" + me["rank"].AsInt + ")")
                       : me["gave_up"].AsBool ? T("못 찾았다고 남겼어요", "You marked it as not found", "見つけられなかったと記録しました", "已标记为未找到", "Lo marcaste como no encontrado")
                       : joined ? T("참가 중이에요 — 보물 근처에 가면 보물상자가 보여요", "Joined — the treasure chest shows up when you get close", "参加中です — 宝に近づくと宝箱が見えます", "已参加 — 走近宝藏会看到宝箱", "Participando — verás el cofre cuando estés cerca")
                       : open && full ? T("참가 인원이 다 찼어요 — 누가 나가면 자리가 생겨요", "This game is full — a spot opens if someone leaves", "参加人数がいっぱいです — 誰かが抜けると空きが出ます", "参加人数已满 — 有人退出就会空出名额", "Este juego está completo — habrá sitio si alguien sale")
                       : "";
        PaintDistance();

        Set(joinButton, open && !joined && !host && h["audience"] != "code" && !full);
        Set(shareButton, open && (host || h["audience"] == "all"));   // 맞팔·팔로워·코드 게임은 주최자만 퍼뜨린다
        Set(leaveButton, open && joined && !host && !found);
        Set(giveupButton, status == "live" && joined && !host && !found && !me["gave_up"].AsBool);
        if (extendRow != null) extendRow.SetActive(host && open && !h["ends_at"].IsNull);
        Set(endButton, host && open);
        Set(revealButton, host && !open && h["coord_mode"] != "exact");
        Set(profileButton, !host && me["can_rate"].AsBool);
        if (profileButton != null && me["can_rate"].AsBool)
        {
            var l = profileButton.GetComponentInChildren<Text>();
            if (l != null) l.text = me["my_rating"].AsInt > 0 ? T("주최자 프로필에서 별점 고치기", "Edit your rating on the host's profile", "主催者のプロフィールで評価を修正", "在主办者资料中修改评分", "Edita tu valoración en el perfil del organizador") : T("주최자 프로필에서 별점 남기기", "Rate on the host's profile", "主催者のプロフィールで評価する", "在主办者资料中评分", "Valora en el perfil del organizador");
        }
        if (msgBox != null) msgBox.SetActive(found || host);
        // 코드 칸은 근접 확인(Near)이 참가자에게 보이는 거리 안일 때만 켠다 — 주최자·찾은 사람·끝난 게임엔 없다
        if (claimCodeBox != null) claimCodeBox.SetActive(false);

        // 소식
        for (int i = feedList.childCount - 1; i >= 0; i--)
        {
            var c = feedList.GetChild(i).gameObject;
            if (c != feedTemplate) Destroy(c);
        }
        var feed = h["feed"].AsArray;
        if (feedHead != null) feedHead.gameObject.SetActive(feed.Count > 0);
        foreach (JSONNode f in feed)
        {
            var g = Instantiate(feedTemplate, feedList);
            g.SetActive(true);
            string who = f["kind"] == "message" && !string.IsNullOrEmpty(f["user"]["username"]) ? f["user"]["username"] + ": " : "";
            var at = When(f["at"]);
            SetText(g, "Text", who + f["text"]);
            SetText(g, "Time", at.HasValue ? at.Value.ToLocalTime().ToString("M/d HH:mm", CultureInfo.InvariantCulture) : "");
        }
    }

    private static void Set(Button b, bool on) { if (b != null) b.gameObject.SetActive(on); }

    // 보물 근처 알림을 받는 중(참가·진행 중·아직 못 찾음)엔 그 줄을 Near() 가 쓴다
    private static bool NearMode(JSONNode h) =>
        h["status"] == "live" && h["me"]["joined"].AsBool && !h["is_host"].AsBool && h["me"]["rank"].AsInt <= 0;

    // 나와의 거리 (2026-10-06 대표님: 눌렀을 때 얼마나 먼지) — 좌표를 받은 게임(정확 공개·주최자)은 내 위치로 바로 잰다(걸으면 바뀐다).
    // 대략 공개는 원 가운데까지 100m 단위, 비공개는 서버가 준 굵은 거리('1km 이내')만
    private void PaintDistance()
    {
        var h = current;
        if (dNear == null || h == null || NearMode(h)) return;
        bool have = TryLocation(out double lat, out double lon, out _);
        string d = null;
        if (have && h.HasKey("lat")) d = Dist(Meters(lat, lon, h["lat"].AsDouble, h["lon"].AsDouble));
        else if (have && h.HasKey("approx_lat"))
        {
            float m = Meters(lat, lon, h["approx_lat"].AsDouble, h["approx_lon"].AsDouble);
            d = m <= h["approx_m"].AsFloat ? T("대략 위치 원 안이에요", "inside the rough area", "おおよその範囲の中です", "在大致范围内", "dentro del área aproximada") : T("약 ", "about ", "約 ", "约 ", "unos ") + Dist(Mathf.Round(m / 100f) * 100f);
        }
        else if (h.HasKey("distance_m")) d = (h["coord_mode"] == "approx" ? T("약 ", "about ", "約 ", "约 ", "unos ") : "") + Dist(h["distance_m"].AsFloat);
        else if (h.HasKey("distance_text")) d = h["distance_text"];
        dNear.text = d != null ? T("나와의 거리  ", "Distance  ", "自分との距離  ", "与我的距离  ", "Distancia  ") + d : T("나와의 거리 — 위치를 찾는 중이에요", "Distance — finding your location", "自分との距離 — 位置を確認しています", "与我的距离 — 正在获取位置", "Distancia — buscando tu ubicación");
        dNear.color = Ink;
    }

    private static float Meters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0, Rad = Math.PI / 180.0;
        double dLat = (lat2 - lat1) * Rad, dLon = (lon2 - lon1) * Rad;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Rad) * Math.Cos(lat2 * Rad) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return (float)(R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)));
    }

#if UNITY_EDITOR
    public static int EditorBuzzes;   // 플레이 시험이 센다 (에디터엔 진동이 없다)
    public static string EditorFollowHost;   // 팔로워 게임 링크로 연 주최자 프로필 (시험 아이디는 실제 서버에 프로필이 없어 창이 안 뜬다)
#endif

    internal static void Buzz()
    {
#if UNITY_EDITOR
        EditorBuzzes++;
#else
        Handheld.Vibrate();
#endif
    }

    private IEnumerator Near()
    {
        var h = current;
        if (h == null || h["status"] != "live" || !h["me"]["joined"].AsBool || h["is_host"].AsBool || h["me"]["rank"].AsInt > 0) yield break;
        if (!TryLocation(out double lat, out double lon, out float acc)) { if (dNear != null) dNear.text = T("위치를 찾는 중이에요…", "Finding your location…", "位置を確認しています…", "正在获取位置…", "Buscando tu ubicación…"); yield break; }
        var b = new JSONObject(); b["lat"] = lat; b["lon"] = lon; b["acc"] = acc;
        long code = 0; JSONNode j = null;
        int id = h["id"].AsInt;
        yield return R0926HuntApi.Post("/api/hunts/" + id + "/near", b, (c, r) => { code = c; j = r; });
        R0926HuntTreasure.Apply(id, code, j);   // 같은 결과로 AR 보물도 — 따로 묻지 않는다
        if (current == null || current["id"].AsInt != id || code != 200) yield break;
        string band = j["band"];
        bool visible = j["visible"].AsBool;
        string bandText = band == "30m" ? T("30m 안 — 거의 다 왔어요!", "Within 30m — almost there!", "30m以内 — もうすぐです！", "30m 以内 — 快到了！", "A menos de 30m — ¡casi llegas!")
                        : band == "100m" ? T("100m 안", "Within 100m", "100m以内", "100m 以内", "A menos de 100m")
                        : band == "300m" ? T("300m 안", "Within 300m", "300m以内", "300m 以内", "A menos de 300m")
                        : band == "1km" ? T("1km 안", "Within 1km", "1km以内", "1km 以内", "A menos de 1km")
                        : T("아직 멀어요", "Still far", "まだ遠いです", "还很远", "Aún lejos");
        if (j.HasKey("distance_m")) bandText += " · " + Dist(j["distance_m"].AsFloat);
        if (visible) bandText = T("보물이 보이는 거리예요", "In view", "宝が見える距離です", "已进入可见范围", "A la vista");
        if (dNear != null) { dNear.text = bandText; dNear.color = visible ? Gold : Ink; }
        // 가까워지면 진동 — 단계가 한 칸이라도 가까워질 때만 (상세를 연 뒤 첫 확인은 기준만 잡는다 · 설정에서 끌 수 있다)
        int rank = visible ? 5 : band == "30m" ? 4 : band == "100m" ? 3 : band == "300m" ? 2 : band == "1km" ? 1 : 0;
        if (nearRank >= 0 && rank > nearRank && R0926HuntPrefs.NotifyNear) Buzz();
        nearRank = rank;
        if (claimCodeBox != null) claimCodeBox.SetActive(visible && current["mode"] == "code");
    }

    private IEnumerator Claim()
    {
        if (busy || current == null) yield break;
        if (!TryLocation(out double lat, out double lon, out float acc)) yield break;
        var b = new JSONObject(); b["lat"] = lat; b["lon"] = lon; b["acc"] = acc; b["mock"] = false;
        b["device_id"] = SystemInfo.deviceUniqueIdentifier;
        if (current["mode"] == "code" && claimCodeInput != null) b["code"] = claimCodeInput.text.Trim();
        busy = true;
        long code = 0; JSONNode j = null;
        int id = current["id"].AsInt;
        yield return R0926HuntApi.Post("/api/hunts/" + id + "/claim", b, (c, r) => { code = c; j = r; });
        busy = false;
        string result = j["result"];
        switch (result)
        {
            case "found":
                if (claimCodeInput != null) claimCodeInput.text = "";
                R0926HuntTreasure.Untrack(id);
                R0926HuntList.Stale();
                if (!R0926HuntCelebrate.Show(id, current["title"], j["rank"].AsInt, j["total"].AsInt, j["ended"].AsBool, current["prize"], current["host"]["username"], current["object_kind"], current["object_url"], null, current["mode"] == "code"))
                    Toast(T("보물을 찾았어요! " + j["rank"].AsInt + "번째", "You found it! #" + j["rank"].AsInt, "宝を見つけました！" + j["rank"].AsInt + "番目", "找到宝藏了！第 " + j["rank"].AsInt + " 个", "¡Lo encontraste! #" + j["rank"].AsInt), 4f);
                StartCoroutine(OpenDetail(id));
                break;
            case "need_code": Toast(T("비밀번호를 넣어 주세요", "Enter the password", "パスワードを入力してください", "请输入密码", "Introduce la contraseña") + (string.IsNullOrEmpty(j["question"]) ? "" : " — " + j["question"]), 3.5f); if (claimCodeBox != null) claimCodeBox.SetActive(true); break;
            case "wrong_code": Toast(T("비밀번호가 달라요 — 남은 기회 " + j["tries_left"].AsInt + "번", "Wrong password — " + j["tries_left"].AsInt + " tries left", "パスワードが違います — 残り " + j["tries_left"].AsInt + "回", "密码错误 — 还剩 " + j["tries_left"].AsInt + " 次机会", "Contraseña incorrecta — quedan " + j["tries_left"].AsInt + " intentos"), 3f); break;
            case "locked": Toast(T("10분 동안 비밀번호를 넣을 수 없어요", "Password locked for 10 minutes", "10分間パスワードを入力できません", "10 分钟内无法输入密码", "Contraseña bloqueada 10 minutos"), 3f); break;
            case "too_far": Toast(T("조금만 더 가까이 — " + j["remaining_m"].AsInt + "m", "A bit closer — " + j["remaining_m"].AsInt + "m", "もう少し近くへ — " + j["remaining_m"].AsInt + "m", "再靠近一点 — " + j["remaining_m"].AsInt + "m", "Un poco más cerca — " + j["remaining_m"].AsInt + "m"), 3f); break;
            case "taken": Toast(T("아쉽게도 다른 사람이 먼저 찾았어요", "Someone found it first", "残念、ほかの人が先に見つけました", "可惜，别人先找到了", "Alguien lo encontró primero"), 3.5f); StartCoroutine(OpenDetail(id)); break;
            case "ended": Toast(T("게임이 끝났어요", "The game has ended", "ゲームが終了しました", "游戏已结束", "El juego terminó"), 3f); StartCoroutine(OpenDetail(id)); break;
            case "weak_gps": Toast(R0926HuntApi.ErrorText("weak_gps", Lang), 3.5f); break;
            case "device_used": Toast(T("이 기기로는 이미 찾았어요", "Already found on this device", "この端末ではすでに見つけています", "此设备已经找到过了", "Ya se encontró en este dispositivo"), 3f); break;
            default: Toast(R0926HuntApi.ErrorText(result ?? j["error"], Lang), 3f); break;
        }
    }

    private IEnumerator Act(string what, int minutes = 0)
    {
        if (busy || current == null) yield break;
        int id = current["id"].AsInt;
        var b = new JSONObject();
        if (what == "join")
        {
            if (!TryLocation(out double lat, out double lon, out _)) { Toast(R0926HuntApi.ErrorText("location", Lang), 3f); yield break; }
            b["lat"] = lat; b["lon"] = lon;
        }
        string prevEnd = null;
        if (what == "extend")
        {
            b["minutes"] = minutes;
            // 누르자마자 남은 시간을 늘려 그린다 — 서버가 거절하면 되돌린다
            prevEnd = current["ends_at"];
            var end = When(current["ends_at"]);
            if (end.HasValue)
            {
                current["ends_at"] = end.Value.AddMinutes(minutes).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                PaintDetail();
            }
        }
        busy = true;
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Post("/api/hunts/" + id + "/" + what, b, (c, r) => { code = c; j = r; });
        busy = false;
        bool same = current != null && current["id"].AsInt == id;
        if (what == "extend")
        {
            if (code == 200)
            {
                if (same && j.HasKey("ends_at")) { current["ends_at"] = j["ends_at"]; PaintDetail(); StartCoroutine(ReloadDetail(id)); }   // 소식 줄('1시간 늘었어요')까지 — 보던 자리는 그대로
                Toast(minutes % 1440 == 0 ? T(minutes / 1440 + "일 늘렸어요", "Extended by " + minutes / 1440 + (minutes == 1440 ? " day" : " days"), minutes / 1440 + "日延長しました", "已延长 " + minutes / 1440 + " 天", "Ampliado " + minutes / 1440 + (minutes == 1440 ? " día" : " días"))
                                          : T(minutes / 60 + "시간 늘렸어요", "Extended by " + minutes / 60 + (minutes == 60 ? " hour" : " hours"), minutes / 60 + "時間延長しました", "已延长 " + minutes / 60 + " 小时", "Ampliado " + minutes / 60 + (minutes == 60 ? " hora" : " horas")), 2.5f);
            }
            else
            {
                if (same && prevEnd != null) { current["ends_at"] = prevEnd; PaintDetail(); }
                Toast(j["error"] == "duration" ? T("최대 " + j["max_min"].AsInt / 1440 + "일까지만 늘릴 수 있어요", "Up to " + j["max_min"].AsInt / 1440 + " days in total", "延長できるのは合計 " + j["max_min"].AsInt / 1440 + "日までです", "最多只能延长到 " + j["max_min"].AsInt / 1440 + " 天", "Hasta " + j["max_min"].AsInt / 1440 + " días en total")
                                               : R0926HuntApi.ErrorText(j["error"], Lang), 3f);
            }
            yield break;
        }
        if (code == 200)
        {
            R0926HuntList.Stale();   // 참가·나가기·포기·마침 — 장소 목록의 보물찾기 줄을 다음에 다시 받는다
            Toast(what == "join" ? T("참가했어요! 보물 근처에 가 보세요", "Joined! Head to the treasure", "参加しました！宝の近くへ行ってみましょう", "已参加！去宝藏附近看看吧", "¡Te uniste! Ve hacia el tesoro")
                : what == "leave" ? T("나왔어요 — 이 게임 알림은 더 오지 않아요", "Left — no more alerts from this game", "退出しました — このゲームの通知は届きません", "已退出 — 不会再收到此游戏的通知", "Saliste — no recibirás más avisos de este juego")
                : what == "giveup" ? T("못 찾았다고 남겼어요", "Marked as not found", "見つけられなかったと記録しました", "已标记为未找到", "Marcado como no encontrado")
                : what == "end" ? T("게임을 마쳤어요", "Game ended", "ゲームを終了しました", "游戏已结束", "Juego terminado")
                : T("위치를 공개했어요", "Location revealed", "位置を公開しました", "已公开位置", "Ubicación revelada"), 2.5f);
            if (what == "leave") { R0926HuntTreasure.Untrack(id); ShowHub(); }
            else StartCoroutine(OpenDetail(id));
        }
        else Toast(R0926HuntApi.ErrorText(j["error"], Lang), 3f);
    }

    private IEnumerator SendMessage()
    {
        if (busy || current == null || msgInput == null) yield break;
        string text = msgInput.text.Trim();
        if (text.Length == 0) yield break;
        int id = current["id"].AsInt;
        var b = new JSONObject(); b["text"] = text;
        busy = true;
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Post("/api/hunts/" + id + "/message", b, (c, r) => { code = c; j = r; });
        busy = false;
        if (code == 200) { msgInput.text = ""; Toast(T("참가자·주최자에게 보냈어요", "Sent to players and host", "参加者・主催者に送りました", "已发送给参加者和主办者", "Enviado a jugadores y organizador"), 2.5f); StartCoroutine(OpenDetail(id)); }
        else Toast(R0926HuntApi.ErrorText(j["error"], Lang), 3f);
    }

    private void OpenHostProfile(bool toForm)
    {
        if (current == null) return;
        string hostId = current["host"]["id"];
        if (string.IsNullOrEmpty(hostId) || ProfileManager.Instance == null) return;
        R0926HuntProfile.ExpandNext(hostId, toForm, toForm ? current["id"].AsInt.ToString() : null);
        ProfileManager.Instance.ShowProfile(hostId);
    }

    // ── 알림 ──────────────────────────────────────────────
    private void Toast(string s, float secs)
    {
        if (toast == null) return;
        if (toastCo != null) StopCoroutine(toastCo);
        toastCo = StartCoroutine(ToastCo(s, secs));
    }

    private IEnumerator ToastCo(string s, float secs)
    {
        toast.text = s;
        toast.transform.parent.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(secs);
        toast.transform.parent.gameObject.SetActive(false);
        toastCo = null;
    }
}
