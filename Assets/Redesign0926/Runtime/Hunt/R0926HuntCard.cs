using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

    private static string Lang => R0926LocalizedText.Lang();
    private static bool Ko => Lang == "ko";
    private static string T(string ko, string en) => Ko ? ko : en;

    // ── 칩 정의 (값, 한국어, 영어) ──
    private static readonly (string v, string ko, string en)[] Modes = { ("touch", "먼저 터치", "First touch"), ("code", "코드 입력", "Enter a code") };
    private static readonly (string v, string ko, string en)[] Winners = { ("1", "1명", "1"), ("3", "3명", "3"), ("5", "5명", "5"), ("10", "10명", "10"), ("all", "모두", "Everyone") };
    // 참가 인원 상한 (2026-10-12 대표님 — 서버 1~100 · 당첨 인원 이상. 칩은 모두 당첨 최대 10명 이상이라 어긋나지 않는다)
    private static readonly (string v, string ko, string en)[] Players = { ("10", "10명", "10"), ("20", "20명", "20"), ("30", "30명", "30"), ("50", "50명", "50"), ("100", "100명", "100") };
    private static readonly (string v, string ko, string en)[] Visibles = { ("10", "10m", "10m"), ("30", "30m", "30m"), ("50", "50m", "50m"), ("100", "100m", "100m"), ("200", "200m", "200m") };
    private static readonly (string v, string ko, string en)[] Radii = { ("1", "1km", "1km"), ("5", "5km", "5km"), ("10", "10km", "10km"), ("30", "30km", "30km"), ("100", "100km", "100km") };
    private static readonly (string v, string ko, string en)[] Coords = { ("exact", "정확히", "Exact"), ("approx", "대략", "Approx."), ("hidden", "비공개", "Hidden") };
    private static readonly (string v, string ko, string en)[] Audiences = { ("all", "모두", "Everyone"), ("friends", "맞팔만", "Mutuals"), ("code", "참가 코드", "Join code") };
    private static readonly (string v, string ko, string en)[] Starts = { ("0", "바로", "Now"), ("60", "1시간 뒤", "In 1h"), ("180", "3시간 뒤", "In 3h"), ("1440", "내일", "Tomorrow"), ("2880", "2일 뒤", "In 2 days") };
    private static readonly (string v, string ko, string en)[] Durations = { ("30", "30분", "30 min"), ("60", "1시간", "1 hour"), ("180", "3시간", "3 hours"), ("1440", "1일", "1 day"), ("4320", "3일", "3 days"), ("10080", "7일", "7 days") };

    private void Awake()
    {
        foreach (var t in new[] { rowTemplate, chipTemplate, ruleTemplate, feedTemplate }) if (t != null) t.SetActive(false);
        Wire(heroButton, OnHero);
        Wire(rulesLink, () => StartCoroutine(OpenRules(false)));
        Wire(joinCodeButton, () => StartCoroutine(JoinByCode()));
        Wire(loginButton, () => { if (LoginManager.Instance != null) LoginManager.Instance.ShowLoginRequirementPopup(); });
        Wire(formBack, ShowHub);
        Wire(submitButton, () => StartCoroutine(Submit()));
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
    }

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
        if (code != 200) { if (nearEmpty != null) { nearEmpty.gameObject.SetActive(true); nearEmpty.text = T("보물찾기를 불러오지 못했어요", "Couldn't load treasure hunts"); } yield break; }
        rulesAccepted = j["host_rules"]["accepted"].AsBool;
        rulesVersion = j["host_rules"]["version"].AsInt > 0 ? j["host_rules"]["version"].AsInt : 1;
        bool enabled = j["prefs"]["enabled"].AsBool || !j["prefs"].HasKey("enabled");
        R0926HuntPrefs.Take(j["prefs"]);
        if (offBox != null) offBox.SetActive(!enabled);

        ClearRows(nearList, mineList);
        var near = j["hunts"].AsArray;
        foreach (JSONNode h in near) AddRow(nearList, h);
        if (nearHead != null) nearHead.text = T("근처 보물찾기", "Nearby hunts") + (near.Count > 0 ? "  " + near.Count : "");
        if (nearEmpty != null)
        {
            nearEmpty.gameObject.SetActive(near.Count == 0 && enabled);   // 꺼 두었으면 offBox 가 이미 말한다 (같은 말이 두 줄 나왔다)
            nearEmpty.text = T("근처에 열린 보물찾기가 없어요 — 첫 보물을 숨겨 보세요", "No hunts nearby yet — hide the first treasure");
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
        string badge = h["is_host"].AsBool ? T("주최", "Host")
                     : me["rank"].AsInt > 0 ? T("찾음", "Found")
                     : status == "ended" || status == "cancelled" ? T("끝", "Ended")
                     : me["joined"].AsBool ? T("참가 중", "Joined")
                     : h["full"].AsBool ? T("마감", "Full") : T("보기", "View");
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
        if (h.HasKey("distance_m")) parts.Add(Dist(h["distance_m"].AsFloat) + (h["coord_mode"] == "approx" ? T(" 근처", " away") : ""));
        else if (h.HasKey("distance_text")) parts.Add(h["distance_text"]);
        parts.Add(T("참가 ", "Players ") + PlayerCount(h));
        int wm = h["winners_max"].AsInt;
        parts.Add(wm > 0 ? T(h["found"].AsInt + "/" + wm + "명 찾음", h["found"].AsInt + "/" + wm + " found") : T(h["found"].AsInt + "명 찾음", h["found"].AsInt + " found"));
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
        if (m < 60) return m + T("분", "m");
        if (m < 600) return m / 60 + T("시간", "h") + (m % 60 > 0 ? " " + m % 60 + T("분", "m") : "");
        if (m < 2880) return (m + 30) / 60 + T("시간", "h");
        return m / 1440 + T("일", "d");
    }

    private static string TimeText(JSONNode h)
    {
        var now = DateTime.UtcNow;
        string status = h["status"];
        if (status == "scheduled") { var s = When(h["starts_at"]); return s.HasValue ? T(Span(s.Value - now) + " 뒤 시작", "Starts in " + Span(s.Value - now)) : T("곧 시작", "Soon"); }
        if (status == "live") { var e = When(h["ends_at"]); return e.HasValue ? T(Span(e.Value - now) + " 남음", Span(e.Value - now) + " left") : T("진행 중", "Live"); }
        return status == "cancelled" ? T("취소됨", "Cancelled") : T("끝남", "Ended");
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
        PaintCodeBox();
        PaintSubmit();
    }

    private void BuildChips()
    {
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

    private void Chips(RectTransform box, string key, (string v, string ko, string en)[] defs, string initial)
    {
        if (box == null || chipTemplate == null) return;
        picked[key] = initial;
        foreach (var d in defs)
        {
            var c = Instantiate(chipTemplate, box);
            c.name = "Chip_" + d.v;
            c.SetActive(true);
            SetText(c, "Label", Ko ? d.ko : d.en);
            string v = d.v;
            Wire(c.GetComponent<Button>(), () => { picked[key] = v; PaintChips(box, key); if (key == "mode") PaintCodeBox(); });
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
        if (!TryLocation(out _, out _, out float acc)) { gpsText.text = T("위치를 찾는 중이에요…", "Finding your location…"); gpsText.color = Muted; return; }
        int a = Mathf.RoundToInt(acc);
        bool good = acc > 0f && acc <= 30f;
        gpsText.text = good ? T("지금 서 있는 자리에 숨겨요 · 정확도 " + a + "m", "Hidden right where you stand · accuracy " + a + "m")
                            : T("정확도 " + a + "m — 하늘이 보이는 곳에서 잠시 기다려 주세요 (30m 이내)", "Accuracy " + a + "m — wait outdoors (need 30m)");
        gpsText.color = good ? Teal : Amber;
    }

    private void PaintSubmit()
    {
        if (submitLabel != null) submitLabel.text = busy ? T("숨기는 중…", "Hiding…") : T("이 자리에 보물 숨기기", "Hide the treasure here");
    }

    private IEnumerator Submit()
    {
        if (busy) yield break;
        if (!TryLocation(out float lat, out float lon, out float acc)) { FormMsg(T("위치를 아직 못 받았어요", "Location not available yet")); yield break; }
        string title = titleInput != null ? titleInput.text.Trim() : "";
        if (title.Length == 0) { FormMsg(R0926HuntApi.ErrorText("title", Lang)); yield break; }
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

        busy = true; PaintSubmit();
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Post("/api/hunts", b, (c, r) => { code = c; j = r; });
        busy = false; PaintSubmit();
        if (code == 200 && j["ok"].AsBool)
        {
            foreach (var f in new[] { titleInput, hintInput, codeInput, questionInput, prizeInput }) if (f != null) f.text = "";
            string jc = j["join_code"];
            Toast(string.IsNullOrEmpty(jc) ? T("보물을 숨겼어요!", "Treasure hidden!") : T("보물을 숨겼어요! 참가 코드 " + jc, "Hidden! Join code " + jc), 4f);
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
        if (l != null) { l.text = T("동의하고 보물 숨기기", "Agree and hide a treasure"); l.color = agreed ? new Color(0.1f, 0.08f, 0.02f, 1f) : Muted; }
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
        if (c6.Length < 6) { Toast(T("참가 코드 6자리를 넣어 주세요", "Enter the 6-character code"), 3f); yield break; }
        long code = 0; JSONNode j = null;
        var b = new JSONObject(); b["code"] = c6;
        yield return R0926HuntApi.Post("/api/hunts/join_code", b, (c, r) => { code = c; j = r; });
        if (code == 200) { if (joinCodeInput != null) joinCodeInput.text = ""; Toast(T("참가했어요!", "Joined!"), 2.5f); StartCoroutine(OpenDetail(j["hunt"]["id"].AsInt)); }
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
            dHostText.text = T("주최 ", "Host ") + host_["username"] + stars + T("  · 주최 " + host_["hosted"].AsInt + "회", "  · hosted " + host_["hosted"].AsInt);
        }
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(h["hint"])) lines.Add(T("힌트  ", "Hint  ") + h["hint"]);
        lines.Add(T("찾는 방식  ", "How  ") + (h["mode"] == "code" ? T("코드 입력", "Enter a code") + (string.IsNullOrEmpty(h["question"]) ? "" : " — " + h["question"]) : T("먼저 터치한 사람", "First to touch")));
        int wm = h["winners_max"].AsInt;
        lines.Add(T("당첨  ", "Winners  ") + (wm > 0 ? T(wm + "명 · " + h["found"].AsInt + "명 찾음", wm + " · " + h["found"].AsInt + " found") : T("찾는 사람 모두 · " + h["found"].AsInt + "명 찾음", "Everyone · " + h["found"].AsInt + " found")));
        if (!string.IsNullOrEmpty(h["prize"])) lines.Add(T("상품  ", "Prize  ") + h["prize"]);
        lines.Add(T("보이는 거리 ", "Visible within ") + h["visible_m"].AsInt + "m" + (h["radius_km"].IsNull ? "" : T(" · 참가 범위 ", " · area ") + h["radius_km"].AsFloat.ToString("0.#", CultureInfo.InvariantCulture) + "km"));
        lines.Add(T("위치 공개  ", "Location  ") + (h["coord_mode"] == "exact" ? T("정확히", "exact") : h["coord_mode"] == "approx" ? T("대략 " + h["approx_m"].AsInt + "m 원", "approx. " + h["approx_m"].AsInt + "m") : T("비공개", "hidden")));
        if (host && !string.IsNullOrEmpty(h["join_code"])) lines.Add(T("참가 코드  ", "Join code  ") + h["join_code"]);
        bool full = h["full"].AsBool;
        lines.Add(T("참가  ", "Players  ") + PlayerCount(h) + T("명", "") + (full ? T(" · 마감", " · full") : ""));
        if (dInfo != null) dInfo.text = string.Join("\n", lines);
        if (dMine != null)
            dMine.text = host ? (open ? T("내가 연 게임이에요 — 보물 근처에 가면 내 보물이 보여요", "You're the host — your treasure shows up when you're near it")
                                      : T("내가 연 게임이에요", "You're the host"))
                       : found ? T(me["rank"].AsInt + "번째로 찾았어요!", "You found it (#" + me["rank"].AsInt + ")")
                       : me["gave_up"].AsBool ? T("못 찾았다고 남겼어요", "You marked it as not found")
                       : joined ? T("참가 중이에요 — 보물 근처에 가면 금빛 상자가 보여요. 두 번 톡톡 눌러 열어요", "Joined — get close, then double-tap the golden box")
                       : open && full ? T("참가 인원이 다 찼어요 — 누가 나가면 자리가 생겨요", "This game is full — a spot opens if someone leaves")
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
            if (l != null) l.text = me["my_rating"].AsInt > 0 ? T("주최자 프로필에서 별점 고치기", "Edit your rating on the host's profile") : T("주최자 프로필에서 별점 남기기", "Rate on the host's profile");
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
            d = m <= h["approx_m"].AsFloat ? T("대략 위치 원 안이에요", "inside the rough area") : T("약 ", "about ") + Dist(Mathf.Round(m / 100f) * 100f);
        }
        else if (h.HasKey("distance_m")) d = (h["coord_mode"] == "approx" ? T("약 ", "about ") : "") + Dist(h["distance_m"].AsFloat);
        else if (h.HasKey("distance_text")) d = h["distance_text"];
        dNear.text = d != null ? T("나와의 거리  ", "Distance  ") + d : T("나와의 거리 — 위치를 찾는 중이에요", "Distance — finding your location");
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
        if (!TryLocation(out float lat, out float lon, out float acc)) { if (dNear != null) dNear.text = T("위치를 찾는 중이에요…", "Finding your location…"); yield break; }
        var b = new JSONObject(); b["lat"] = lat; b["lon"] = lon; b["acc"] = acc;
        long code = 0; JSONNode j = null;
        int id = h["id"].AsInt;
        yield return R0926HuntApi.Post("/api/hunts/" + id + "/near", b, (c, r) => { code = c; j = r; });
        R0926HuntTreasure.Apply(id, code, j);   // 같은 결과로 AR 보물도 — 따로 묻지 않는다
        if (current == null || current["id"].AsInt != id || code != 200) yield break;
        string band = j["band"];
        bool visible = j["visible"].AsBool;
        string bandText = band == "30m" ? T("30m 안 — 거의 다 왔어요!", "Within 30m — almost there!")
                        : band == "100m" ? T("100m 안", "Within 100m")
                        : band == "300m" ? T("300m 안", "Within 300m")
                        : band == "1km" ? T("1km 안", "Within 1km")
                        : T("아직 멀어요", "Still far");
        if (j.HasKey("distance_m")) bandText += " · " + Dist(j["distance_m"].AsFloat);
        if (visible) bandText = T("보물이 보이는 거리예요 — 금빛 상자를 두 번 톡톡 눌러 열어요", "In view — double-tap the golden box to open it");
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
                if (!R0926HuntCelebrate.Show(id, current["title"], j["rank"].AsInt, current["prize"], current["host"]["username"]))
                    Toast(T("보물을 찾았어요! " + j["rank"].AsInt + "번째", "You found it! #" + j["rank"].AsInt), 4f);
                StartCoroutine(OpenDetail(id));
                break;
            case "need_code": Toast(T("코드를 넣어 주세요", "Enter the code") + (string.IsNullOrEmpty(j["question"]) ? "" : " — " + j["question"]), 3.5f); if (claimCodeBox != null) claimCodeBox.SetActive(true); break;
            case "wrong_code": Toast(T("코드가 달라요 — 남은 기회 " + j["tries_left"].AsInt + "번", "Wrong code — " + j["tries_left"].AsInt + " tries left"), 3f); break;
            case "locked": Toast(T("10분 동안 코드를 넣을 수 없어요", "Code locked for 10 minutes"), 3f); break;
            case "too_far": Toast(T("조금만 더 가까이 — " + j["remaining_m"].AsInt + "m", "A bit closer — " + j["remaining_m"].AsInt + "m"), 3f); break;
            case "taken": Toast(T("아쉽게도 다른 사람이 먼저 찾았어요", "Someone found it first"), 3.5f); StartCoroutine(OpenDetail(id)); break;
            case "ended": Toast(T("게임이 끝났어요", "The game has ended"), 3f); StartCoroutine(OpenDetail(id)); break;
            case "weak_gps": Toast(R0926HuntApi.ErrorText("weak_gps", Lang), 3.5f); break;
            case "device_used": Toast(T("이 기기로는 이미 찾았어요", "Already found on this device"), 3f); break;
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
                Toast(minutes % 1440 == 0 ? T(minutes / 1440 + "일 늘렸어요", "Extended by " + minutes / 1440 + (minutes == 1440 ? " day" : " days"))
                                          : T(minutes / 60 + "시간 늘렸어요", "Extended by " + minutes / 60 + (minutes == 60 ? " hour" : " hours")), 2.5f);
            }
            else
            {
                if (same && prevEnd != null) { current["ends_at"] = prevEnd; PaintDetail(); }
                Toast(j["error"] == "duration" ? T("최대 " + j["max_min"].AsInt / 1440 + "일까지만 늘릴 수 있어요", "Up to " + j["max_min"].AsInt / 1440 + " days in total")
                                               : R0926HuntApi.ErrorText(j["error"], Lang), 3f);
            }
            yield break;
        }
        if (code == 200)
        {
            Toast(what == "join" ? T("참가했어요! 보물 근처에 가 보세요", "Joined! Head to the treasure")
                : what == "leave" ? T("나왔어요 — 이 게임 알림은 더 오지 않아요", "Left — no more alerts from this game")
                : what == "giveup" ? T("못 찾았다고 남겼어요", "Marked as not found")
                : what == "end" ? T("게임을 마쳤어요", "Game ended")
                : T("위치를 공개했어요", "Location revealed"), 2.5f);
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
        if (code == 200) { msgInput.text = ""; Toast(T("참가자·주최자에게 보냈어요", "Sent to players and host"), 2.5f); StartCoroutine(OpenDetail(id)); }
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
