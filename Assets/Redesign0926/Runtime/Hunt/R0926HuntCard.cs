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
///  · 숨기기: 처음엔 주최 수칙 동의 → 양식(현장 GPS · 방식 · 인원 · 보이는 거리 · 범위 · 공개 · 대상 · 시작 · 진행 시간)
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
    [SerializeField] private RectTransform modeChips, winnerChips, playerChips, visibleChips, radiusChips, coordChips, audienceChips, startChips, durationChips;
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
    [SerializeField] private GameObject extendRow;         // 주최자 — '1시간 연장하기 · 1일 연장하기' 한 줄
    [SerializeField] private Button extendButton, extendDayButton;
    [SerializeField] private GameObject msgBox;
    [SerializeField] private InputField msgInput;
    [SerializeField] private Button msgSend;
    [SerializeField] private RectTransform feedList;
    [SerializeField] private UploadInputMirror inputMirror;   // 키보드 위 입력줄 — 보내기(엔터)로 한마디·코드를 바로

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

    // 고른 사진(JPG)·3D 모델(GLB) — 숨길 때 올리고, 올린 주소는 같은 파일로 다시 숨길 때 쓴다
    private byte[] objData;
    private string objKind, objUrl;
    private Texture2D objTex;                 // 고른 사진 (미리보기 액자)
    private string pickNote;                  // 고르기 칸 아래 알림 (못 연 파일 등)
    private bool uploading, previewLoading;
    private readonly R0926PickerWait pick = new R0926PickerWait();
    private const int ModelMax = 10 * 1024 * 1024;   // 서버 OBJECT_MAX['model'] 과 같다

    private static string Lang => R0926LocalizedText.Lang();
    private static bool Ko => Lang == "ko";
    private static string T(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);

    // ── 칩 정의 (값, 한국어, 영어, 일본어, 중국어, 스페인어) ──
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Modes = { ("touch", "먼저 터치", "First touch", "先にタッチ", "先触碰", "Primero en tocar"), ("code", "코드 입력", "Enter a code", "コード入力", "输入代码", "Introducir código") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Winners = { ("1", "1명", "1", "1人", "1人", "1"), ("3", "3명", "3", "3人", "3人", "3"), ("5", "5명", "5", "5人", "5人", "5"), ("10", "10명", "10", "10人", "10人", "10"), ("all", "모두", "Everyone", "全員", "所有人", "Todos") };
    // 참가 인원 상한 (2026-10-12 대표님 — 서버 1~100 · 당첨 인원 이상. 칩은 모두 당첨 최대 10명 이상이라 어긋나지 않는다)
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Players = { ("10", "10명", "10", "10人", "10人", "10"), ("20", "20명", "20", "20人", "20人", "20"), ("30", "30명", "30", "30人", "30人", "30"), ("50", "50명", "50", "50人", "50人", "50"), ("100", "100명", "100", "100人", "100人", "100") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Visibles = { ("10", "10m", "10m", "10m", "10m", "10m"), ("30", "30m", "30m", "30m", "30m", "30m"), ("50", "50m", "50m", "50m", "50m", "50m"), ("100", "100m", "100m", "100m", "100m", "100m"), ("200", "200m", "200m", "200m", "200m", "200m") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Radii = { ("1", "1km", "1km", "1km", "1km", "1km"), ("5", "5km", "5km", "5km", "5km", "5km"), ("10", "10km", "10km", "10km", "10km", "10km"), ("30", "30km", "30km", "30km", "30km", "30km"), ("100", "100km", "100km", "100km", "100km", "100km") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Coords = { ("exact", "정확히", "Exact", "正確", "精确", "Exacta"), ("approx", "대략", "Approx.", "おおよそ", "大致", "Aprox."), ("hidden", "비공개", "Hidden", "非公開", "不公开", "Oculta") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Audiences = { ("all", "모두", "Everyone", "全員", "所有人", "Todos"), ("friends", "맞팔만", "Mutuals", "相互フォロー", "互关好友", "Mutuos"), ("code", "참가 코드", "Join code", "参加コード", "参加代码", "Código") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Starts = { ("0", "바로", "Now", "すぐ", "立即", "Ahora"), ("60", "1시간 뒤", "In 1h", "1時間後", "1小时后", "En 1 h"), ("180", "3시간 뒤", "In 3h", "3時間後", "3小时后", "En 3 h"), ("1440", "내일", "Tomorrow", "明日", "明天", "Mañana"), ("2880", "2일 뒤", "In 2 days", "2日後", "2天后", "En 2 días") };
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Durations = { ("30", "30분", "30 min", "30分", "30分钟", "30 min"), ("60", "1시간", "1 hour", "1時間", "1小时", "1 hora"), ("180", "3시간", "3 hours", "3時間", "3小时", "3 horas"), ("1440", "1일", "1 day", "1日", "1天", "1 día"), ("4320", "3일", "3 days", "3日", "3天", "3 días"), ("10080", "7일", "7 days", "7日", "7天", "7 días") };
    // 보물 모양 (2026-10-24 대표님 — 주최자가 고른다. 서버 hunts.object_kind · 모르는 모양은 옛 앱에서 보물상자)
    private static readonly (string v, string ko, string en, string ja, string zh, string es)[] Shapes = { ("chest", "보물상자", "Chest", "宝箱", "宝箱", "Cofre"), ("gift", "선물상자", "Gift box", "ギフト箱", "礼物盒", "Regalo"), ("egg", "보물알", "Egg", "宝の卵", "宝蛋", "Huevo"), ("gem", "보석", "Gem", "宝石", "宝石", "Gema"), ("star", "별", "Star", "星", "星星", "Estrella"), ("heart", "하트", "Heart", "ハート", "爱心", "Corazón"), ("photo", "사진", "Photo", "写真", "照片", "Foto"), ("model", "3D 모델", "3D model", "3Dモデル", "3D 模型", "Modelo 3D") };

    private void Awake()
    {
        foreach (var t in new[] { rowTemplate, chipTemplate, ruleTemplate, feedTemplate }) if (t != null) t.SetActive(false);
        Wire(heroButton, OnHero);
        Wire(rulesLink, () => StartCoroutine(OpenRules(false)));
        Wire(joinCodeButton, () => StartCoroutine(JoinByCode()));
        Wire(loginButton, () => { if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup(); });
        Wire(formBack, ShowHub);
        Wire(submitButton, () => StartCoroutine(Submit()));
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
    }

    private void OnApplicationFocus(bool hasFocus) => R0926PickerWait.NoteFocus(hasFocus);

    /// <summary>상세가 근접 확인 중인 게임 (없으면 0) — R0926HuntTreasure 가 같은 게임을 따로 묻지 않는다</summary>
    public static int NearHuntId { get; private set; }

    // 로그인하고 돌아오면 처음 화면을 다시 — '로그인하면 …' 안내가 그대로 남아 있었다
    private void OnLoginChanged(bool loggedIn)
    {
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
    internal static bool TryLocation(out float lat, out float lon, out float acc)
    {
        // 켜진 직후 첫 위치가 오기 전엔 0,0 이 온다 — 장소·3D모델 올리기(gpsData == zero)와 같이 '아직 없음'
        if (Input.location.status == LocationServiceStatus.Running)
        {
            var ld = Input.location.lastData;
            if (ld.latitude != 0f || ld.longitude != 0f)
            {
                lat = ld.latitude; lon = ld.longitude; acc = ld.horizontalAccuracy;
                return true;
            }
        }
#if UNITY_EDITOR
        if (VirtualLocation.Instance != null)
        {
            lat = VirtualLocation.Instance.Latitude; lon = VirtualLocation.Instance.Longitude; acc = 5f;
            return true;
        }
#endif
        lat = lon = acc = 0f;
        return false;
    }

    private static string Q(float v) => v.ToString("F6", CultureInfo.InvariantCulture);

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
        string q = TryLocation(out float lat, out float lon, out _) ? "?lat=" + Q(lat) + "&lon=" + Q(lon) : "";
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
        PaintSubmit();
    }

    private void BuildChips()
    {
        Chips(shapeChips, "shape", Shapes, "chest");
        Chips(modeChips, "mode", Modes, "touch");
        Chips(winnerChips, "winners", Winners, "1");
        Chips(playerChips, "players", Players, "30");
        Chips(visibleChips, "visible", Visibles, "50");
        Chips(radiusChips, "radius", Radii, "5");
        Chips(coordChips, "coord", Coords, "exact");
        Chips(audienceChips, "audience", Audiences, "all");
        Chips(startChips, "start", Starts, "0");
        Chips(durationChips, "duration", Durations, "60");
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
        if (codeBox != null) codeBox.SetActive(picked.TryGetValue("mode", out var m) && m == "code");
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
        if (submitLabel != null) submitLabel.text = uploading ? T("올리는 중…", "Uploading…", "アップロード中…", "正在上传…", "Subiendo…")
                                                  : busy ? T("숨기는 중…", "Hiding…", "隠しています…", "正在藏宝…", "Escondiendo…") : T("이 자리에 보물 숨기기", "Hide the treasure here", "ここに宝を隠す", "在这里藏宝", "Esconder el tesoro aquí");
    }

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

    private IEnumerator Submit()
    {
        if (busy) yield break;
        if (!TryLocation(out float lat, out float lon, out float acc)) { FormMsg(T("위치를 아직 못 받았어요", "Location not available yet", "まだ位置情報を取得できていません", "尚未获取到位置", "Aún no hay ubicación")); yield break; }
        string title = titleInput != null ? titleInput.text.Trim() : "";
        if (title.Length == 0) { FormMsg(R0926HuntApi.ErrorText("title", Lang)); yield break; }
        string shape = Shape;
        bool file = shape == "photo" || shape == "model";
        if (file && (objData == null || objKind != shape))
        {
            FormMsg(shape == "photo" ? T("보물로 쓸 사진을 골라 주세요", "Choose a photo for the treasure", "宝にする写真を選んでください", "请选择用作宝藏的照片", "Elige una foto para el tesoro")
                                     : T("보물로 쓸 3D 모델(GLB)을 골라 주세요", "Choose a 3D model (GLB) for the treasure", "宝にする3Dモデル（GLB）を選んでください", "请选择用作宝藏的 3D 模型（GLB）", "Elige un modelo 3D (GLB) para el tesoro"));
            yield break;
        }
        if (shape == "model" && shapePreview != null && shapePreview.Loading) { FormMsg(T("3D 모델을 불러오는 중이에요 — 잠시만요", "Still loading the 3D model — one moment", "3Dモデルを読み込み中です — 少々お待ちください", "正在加载 3D 模型 — 请稍候", "Cargando el modelo 3D — un momento")); yield break; }
        var b = new JSONObject();
        b["title"] = title;
        b["hint"] = hintInput != null ? hintInput.text.Trim() : "";
        b["mode"] = picked["mode"];
        if (picked["mode"] == "code")
        {
            b["code"] = codeInput != null ? codeInput.text.Trim() : "";
            b["question"] = questionInput != null ? questionInput.text.Trim() : "";
        }
        if (picked["winners"] == "all") b["winners_all"] = true; else b["winners"] = int.Parse(picked["winners"]);
        if (picked.TryGetValue("players", out var players)) b["max_players"] = int.Parse(players);
        b["prize"] = prizeInput != null ? prizeInput.text.Trim() : "";
        b["visible_m"] = int.Parse(picked["visible"]);
        b["radius_km"] = float.Parse(picked["radius"], CultureInfo.InvariantCulture);
        b["coord_mode"] = picked["coord"];
        if (picked["coord"] == "approx") b["approx_m"] = 300;
        b["audience"] = picked["audience"];
        b["start_in_min"] = int.Parse(picked["start"]);
        b["duration_min"] = int.Parse(picked["duration"]);
        b["lat"] = lat; b["lon"] = lon; b["acc"] = acc; b["mock"] = false;
        b["object_kind"] = shape;

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
                    if (string.IsNullOrEmpty(url)) FormMsg(uc == 401 ? R0926HuntApi.ErrorText("login", Lang) : R0926HuntApi.ErrorText(uj != null ? (string)uj["error"] : null, Lang));
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
            string jc = j["join_code"];
            Toast(string.IsNullOrEmpty(jc) ? T("보물을 숨겼어요!", "Treasure hidden!", "宝を隠しました！", "宝藏已藏好！", "¡Tesoro escondido!") : T("보물을 숨겼어요! 참가 코드 " + jc, "Hidden! Join code " + jc, "宝を隠しました！参加コード " + jc, "宝藏已藏好！参加代码 " + jc, "¡Escondido! Código " + jc), 4f);
            StartCoroutine(OpenDetail(j["hunt"]["id"].AsInt));
            yield break;
        }
        if (j["error"] == "need_rules") { rulesAccepted = false; StartCoroutine(OpenRules(true)); yield break; }
        FormMsg(code == 401 ? R0926HuntApi.ErrorText("login", Lang) : R0926HuntApi.ErrorText(j["error"], Lang));
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
        yield return R0926HuntApi.Get("/api/hunts/rules?lang=" + (Ko ? "ko" : "en"), (c, r) => { code = c; j = r; });
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
        string q = TryLocation(out float lat, out float lon, out _) ? "?lat=" + Q(lat) + "&lon=" + Q(lon) : "";
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Get("/api/hunts/" + id + q, (c, r) => { code = c; j = r; });
        if (code != 200) { Toast(R0926HuntApi.ErrorText(j["error"], Lang), 3f); yield break; }
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
        string q = TryLocation(out float lat, out float lon, out _) ? "?lat=" + Q(lat) + "&lon=" + Q(lon) : "";
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
        lines.Add(T("찾는 방식  ", "How  ", "見つけ方  ", "寻宝方式  ", "Cómo  ") + (h["mode"] == "code" ? T("코드 입력", "Enter a code", "コード入力", "输入代码", "Introducir código") + (string.IsNullOrEmpty(h["question"]) ? "" : " — " + h["question"]) : T("먼저 터치한 사람", "First to touch", "先にタッチした人", "最先触碰的人", "Primero en tocar")));
        int wm = h["winners_max"].AsInt;
        lines.Add(T("당첨  ", "Winners  ", "当選  ", "中奖  ", "Ganadores  ") + (wm > 0 ? T(wm + "명 · " + h["found"].AsInt + "명 찾음", wm + " · " + h["found"].AsInt + " found", wm + "人 · " + h["found"].AsInt + "人発見", wm + " 人 · " + h["found"].AsInt + " 人找到", wm + " · " + h["found"].AsInt + " encontrados") : T("찾는 사람 모두 · " + h["found"].AsInt + "명 찾음", "Everyone · " + h["found"].AsInt + " found", "見つけた人全員 · " + h["found"].AsInt + "人発見", "找到的人都中奖 · " + h["found"].AsInt + " 人找到", "Todos · " + h["found"].AsInt + " encontrados")));
        if (!string.IsNullOrEmpty(h["prize"])) lines.Add(T("상품  ", "Prize  ", "賞品  ", "奖品  ", "Premio  ") + h["prize"]);
        lines.Add(T("보이는 거리 ", "Visible within ", "見える距離 ", "可见距离 ", "Visible a ") + h["visible_m"].AsInt + "m" + (h["radius_km"].IsNull ? "" : T(" · 참가 범위 ", " · area ", " · 参加範囲 ", " · 参加范围 ", " · área ") + h["radius_km"].AsFloat.ToString("0.#", CultureInfo.InvariantCulture) + "km"));
        lines.Add(T("위치 공개  ", "Location  ", "位置の公開  ", "位置公开  ", "Ubicación  ") + (h["coord_mode"] == "exact" ? T("정확히", "exact", "正確", "精确", "exacta") : h["coord_mode"] == "approx" ? T("대략 " + h["approx_m"].AsInt + "m 원", "approx. " + h["approx_m"].AsInt + "m", "おおよそ " + h["approx_m"].AsInt + "m の円", "大致 " + h["approx_m"].AsInt + "m 范围", "aprox. " + h["approx_m"].AsInt + "m") : T("비공개", "hidden", "非公開", "不公开", "oculta")));
        if (host && !string.IsNullOrEmpty(h["join_code"])) lines.Add(T("참가 코드  ", "Join code  ", "参加コード  ", "参加代码  ", "Código  ") + h["join_code"]);
        bool full = h["full"].AsBool;
        lines.Add(T("참가  ", "Players  ", "参加  ", "参加  ", "Jugadores  ") + PlayerCount(h) + T("명", "", "人", "人", "") + (full ? T(" · 마감", " · full", " · 満員", " · 已满", " · completo") : ""));
        if (dInfo != null) dInfo.text = string.Join("\n", lines);
        if (dMine != null)
            dMine.text = host ? (open ? T("내가 연 게임이에요 — 보물 근처에 가면 내 보물이 보여요", "You're the host — your treasure shows up when you're near it", "あなたが主催したゲームです — 宝の近くに行くと自分の宝が見えます", "这是你主办的游戏 — 走近宝藏就能看到", "Organizas este juego — verás tu tesoro cuando estés cerca")
                                      : T("내가 연 게임이에요", "You're the host", "あなたが主催したゲームです", "这是你主办的游戏", "Organizas este juego"))
                       : found ? T(me["rank"].AsInt + "번째로 찾았어요!", "You found it (#" + me["rank"].AsInt + ")", me["rank"].AsInt + "番目に見つけました！", "你第 " + me["rank"].AsInt + " 个找到了！", "¡Lo encontraste! (#" + me["rank"].AsInt + ")")
                       : me["gave_up"].AsBool ? T("못 찾았다고 남겼어요", "You marked it as not found", "見つけられなかったと記録しました", "已标记为未找到", "Lo marcaste como no encontrado")
                       : joined ? T("참가 중이에요 — 보물 근처에 가면 보물상자가 보여요. 두 번 톡톡 눌러 열어요", "Joined — get close, then double-tap the treasure chest", "参加中です — 宝に近づくと宝箱が見えます。2回タップして開けましょう", "已参加 — 走近宝藏会看到宝箱，双击即可打开", "Participando — acércate y toca dos veces el cofre")
                       : open && full ? T("참가 인원이 다 찼어요 — 누가 나가면 자리가 생겨요", "This game is full — a spot opens if someone leaves", "参加人数がいっぱいです — 誰かが抜けると空きが出ます", "参加人数已满 — 有人退出就会空出名额", "Este juego está completo — habrá sitio si alguien sale")
                       : "";
        PaintDistance();

        Set(joinButton, open && !joined && !host && h["audience"] != "code" && !full);
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
        bool have = TryLocation(out float lat, out float lon, out _);
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
        if (!TryLocation(out float lat, out float lon, out float acc)) { if (dNear != null) dNear.text = T("위치를 찾는 중이에요…", "Finding your location…", "位置を確認しています…", "正在获取位置…", "Buscando tu ubicación…"); yield break; }
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
        if (visible) bandText = T("보물이 보이는 거리예요 — 보물상자를 두 번 톡톡 눌러 열어요", "In view — double-tap the treasure chest to open it", "宝が見える距離です — 宝箱を2回タップして開けましょう", "已进入可见范围 — 双击宝箱即可打开", "A la vista — toca dos veces el cofre para abrirlo");
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
        if (!TryLocation(out float lat, out float lon, out float acc)) yield break;
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
                if (!R0926HuntCelebrate.Show(id, current["title"], j["rank"].AsInt, current["prize"], current["host"]["username"]))
                    Toast(T("보물을 찾았어요! " + j["rank"].AsInt + "번째", "You found it! #" + j["rank"].AsInt, "宝を見つけました！" + j["rank"].AsInt + "番目", "找到宝藏了！第 " + j["rank"].AsInt + " 个", "¡Lo encontraste! #" + j["rank"].AsInt), 4f);
                StartCoroutine(OpenDetail(id));
                break;
            case "need_code": Toast(T("코드를 넣어 주세요", "Enter the code", "コードを入力してください", "请输入代码", "Introduce el código") + (string.IsNullOrEmpty(j["question"]) ? "" : " — " + j["question"]), 3.5f); if (claimCodeBox != null) claimCodeBox.SetActive(true); break;
            case "wrong_code": Toast(T("코드가 달라요 — 남은 기회 " + j["tries_left"].AsInt + "번", "Wrong code — " + j["tries_left"].AsInt + " tries left", "コードが違います — 残り " + j["tries_left"].AsInt + "回", "代码错误 — 还剩 " + j["tries_left"].AsInt + " 次机会", "Código incorrecto — quedan " + j["tries_left"].AsInt + " intentos"), 3f); break;
            case "locked": Toast(T("10분 동안 코드를 넣을 수 없어요", "Code locked for 10 minutes", "10分間コードを入力できません", "10 分钟内无法输入代码", "Código bloqueado 10 minutos"), 3f); break;
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
            if (!TryLocation(out float lat, out float lon, out _)) { Toast(R0926HuntApi.ErrorText("location", Lang), 3f); yield break; }
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
