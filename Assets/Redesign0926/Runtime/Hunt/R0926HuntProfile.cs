using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 프로필 카드의 '주최' 칸과 펼침 — 보물찾기에서 주최자에 관한 건 전부 그 사람 프로필에서 (2026-10-05 대표님 · 시안 우팡_보물찾기_프로필_1005).
///  · 숫자 줄(팔로잉·팔로워) 옆 '주최' 칸 — 주최한 적이 있거나 지금 열린 게임이 있는 사람만. 숫자 아래 별점(3명부터), 적으면 '평가 모으는 중'
///    주최한 적은 없고 남의 게임에 참여 중이면 '참여' 칸 (2026-10-13 대표님: 참석자도 내 프로필·남이 본 프로필에 진행 중인 보물찾기)
///  · 누르면 같은 카드 안에서 펼친다: 주최 기록 · 지금 열린 게임(주최/참여) · 참여자 평가 · 별점 남기기(자격 있는 참가자) · 참여자 리뷰 · 신고/차단
///  · 서버 한 번 부르기 GET /api/hunts/profile/&lt;아이디&gt; — 별점 POST /api/hunts/&lt;게임&gt;/rate · 신고 POST /api/hunts/report · 차단은 기존 /api/block
///  · 카드가 화면보다 길어지면 R0926ProfileLayout 이 펼침 칸만 줄이고 그 안을 스크롤한다
///  · 별점 알림(review=1)·게임 상세의 주최자 줄로 열면 펼친 채로 (ExpandNext)
/// 리뷰 한 줄은 키보드 위 입력줄(프로필 창의 UploadInputMirror)로 쓴다.
/// </summary>
public class R0926HuntProfile : MonoBehaviour
{
#if UNITY_EDITOR
    /// <summary>플레이 시험 — 보고 있는 프로필 대신 이 시험 아이디의 보물찾기 기록을 (프로필 자체는 실제 회원이어야 열린다)</summary>
    public static string EditorHostAlias;
#endif

    [SerializeField] private R0926ProfileLayout layout;

    [Header("숫자 줄의 '주최' 칸")]
    [SerializeField] private RectTransform stats;                 // FollowStats — 카드 배치 항목
    [SerializeField] private HorizontalLayoutGroup statsRow;
    [SerializeField] private Button column;
    [SerializeField] private Image columnBox;
    [SerializeField] private Text columnMain;                     // 숫자 + 별
    [SerializeField] private Text columnLabel;                    // 주최

    [Header("펼침")]
    [SerializeField] private RectTransform section;               // 카드 배치 항목 — 켜고 끈다
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform content;
    [SerializeField] private Text headRight;
    [SerializeField] private GameObject suspendBox;
    [SerializeField] private Text suspendText;
    [SerializeField] private GameObject recordBox;
    [SerializeField] private Text[] recordNums;                    // 주최 · 최근 30일 · 평균 참가 · 마지막
    [SerializeField] private Text recordFoot;
    [SerializeField] private RectTransform openList;
    [SerializeField] private GameObject openTemplate;
    [SerializeField] private GameObject rateBox;
    [SerializeField] private Text rateNum;
    [SerializeField] private Text rateStars;
    [SerializeField] private Text rateCount;

    [Header("별점 남기기")]
    [SerializeField] private GameObject form;
    [SerializeField] private Text formTitle;
    [SerializeField] private RectTransform formGames;              // 남길 게임이 여럿이면 고르는 칩
    [SerializeField] private GameObject chipTemplate;
    [SerializeField] private Button[] starButtons;
    [SerializeField] private InputField reviewInput;
    [SerializeField] private Text reviewCount;
    [SerializeField] private Button sendButton;
    [SerializeField] private Text sendLabel;
    [SerializeField] private Text notPlayed;

    [Header("참여자 리뷰")]
    [SerializeField] private GameObject reviewsBox;
    [SerializeField] private RectTransform reviewList;
    [SerializeField] private GameObject reviewTemplate;
    [SerializeField] private Button moreButton;

    [Header("신고 · 차단")]
    [SerializeField] private GameObject actions;
    [SerializeField] private Button reportButton;
    [SerializeField] private Button blockButton;
    [SerializeField] private GameObject reasonBox;
    [SerializeField] private Text reasonTitle;
    [SerializeField] private RectTransform reasonList;
    [SerializeField] private GameObject reasonTemplate;
    [SerializeField] private Button reasonCancel;

    private const float ColumnSpacing = 30f;   // 세 칸일 때 숫자 줄 간격 (두 칸일 땐 원래 190)
    private const float ColumnExtra = 60f;     // '주최' 칸은 이름 한 줄이 더 있어 숫자 줄을 이만큼 늘린다
    private const int ReviewMax = 80;          // 서버 REVIEW_MAX
    private const int ReviewsFirst = 3;

    private static readonly Color Ink = new Color(0.953f, 0.961f, 0.965f, 1f);
    private static readonly Color Soft = new Color(0.82f, 0.84f, 0.86f, 1f);
    private static readonly Color Gold = new Color(0.910f, 0.749f, 0.310f, 1f);
    private static readonly Color Teal = new Color(0.247f, 0.757f, 0.690f, 1f);
    private static readonly Color Amber = new Color(1f, 0.702f, 0.278f, 1f);
    private static readonly Color StarOff = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color BoxOff = new Color(0.910f, 0.749f, 0.310f, 0.12f);
    private static readonly Color BoxOn = new Color(0.910f, 0.749f, 0.310f, 0.24f);
    private static readonly Color ChipOn = new Color(0.910f, 0.749f, 0.310f, 0.22f);
    private static readonly Color ChipOff = new Color(1f, 1f, 1f, 0.06f);
    private const string GoldHex = "#E8BF4F", DimHex = "#E8BF4F4D", MutedHex = "#8C959D", TealHex = "#3FC1B0";
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    // 신고 사유 — 서버가 준 것만, 이 순서로 (서버 JSON 은 이름순으로 온다)
    private static readonly (string key, string en)[] HostReasons =
        { ("danger", "Hidden in a dangerous place"), ("no_prize", "Didn't give the promised prize"), ("fake", "Fake or scam"), ("harass", "Rude words or behavior"), ("other", "Other") };
    private static readonly (string key, string en)[] ReviewReasons =
        { ("abuse", "Abuse or insults"), ("fake", "False review"), ("other", "Other") };

    private static string expandFor;    // 곧 열릴 이 사람 프로필은 펼친 채로
    private static string expandHunt;   // 별점 남길 게임을 이것으로 (게임 상세·별점 알림에서 왔을 때)
    private static bool expandToForm;
    private static float expandAt = -99f;

    private string shownId;             // 프로필 창이 보여 주는 사람
    private JSONNode data;              // 그 사람 보물찾기 기록 (못 받았으면 null)
    private int gen;
    private bool expanded, busy, allReviews;
    private int pick = -1;              // 별점 남길 게임 (me.can_review 안 순서)
    private string pickedHunt;          // 입력칸을 채운 게임 — 게임을 바꾸면 그 게임의 내 별점·리뷰로
    private int stars;
    private string reportTarget;
    private int reportRid;
    private float blockArmed = -99f;
    private float lastWant = -1f;
    private float baseSpacing = -1f;
    private TextAnchor baseAlign;

    private static string Lang => R0926LocalizedText.Lang();
    private static string T(string ko, string en) => Lang == "ko" ? ko : en;

    /// <summary>곧 열릴 이 사람 프로필은 보물찾기 칸을 펼친 채로 — toForm 이면 별점 남기기까지 내린다 (huntId 게임을 골라 둔 채)</summary>
    public static void ExpandNext(string userId, bool toForm, string huntId = null)
    {
        expandFor = userId;
        expandHunt = huntId;
        expandToForm = toForm;
        expandAt = Time.realtimeSinceStartup;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        expandFor = null;
        expandHunt = null;
        expandToForm = false;
        expandAt = -99f;
    }

    private void Awake()
    {
        foreach (var t in new[] { openTemplate, chipTemplate, reviewTemplate, reasonTemplate }) if (t != null) t.SetActive(false);
        Wire(column, () => Expand(!expanded));
        Wire(sendButton, () => StartCoroutine(Rate()));
        Wire(moreButton, () => { allReviews = true; PaintReviews(); });
        Wire(reportButton, () => OpenReasons("host", 0));
        Wire(blockButton, OnBlock);
        Wire(reasonCancel, () => { if (reasonBox != null) reasonBox.SetActive(false); });
        if (starButtons != null)
            for (int i = 0; i < starButtons.Length; i++)
            {
                int n = i + 1;
                Wire(starButtons[i], () => { stars = n; PaintStars(); });
            }
        if (section != null) section.gameObject.SetActive(false);
        if (reasonBox != null) reasonBox.SetActive(false);
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

    private void OnDisable()
    {
        shownId = null;
        data = null;
        gen++;
        busy = false;
        Expand(false);
        SetColumn(false);
    }

    private void Update()
    {
        var pm = ProfileManager.Instance;
        string id = pm != null ? pm.CurrentProfileId : null;
        if (id != shownId) { shownId = id; Load(); }
        if (!expanded) return;
        Fit();
        if (form != null && form.activeInHierarchy && reviewCount != null)
        {
            string n = (reviewInput != null ? (reviewInput.text ?? "").Length : 0) + "/" + ReviewMax;
            if (reviewCount.text != n) reviewCount.text = n;
        }
        if (blockArmed > 0f && Time.unscaledTime - blockArmed > 3f) { blockArmed = -99f; PaintActions(); }
    }

    // ── 받기 ──────────────────────────────────────────────
    private void Load()
    {
        gen++;
        data = null;
        busy = false;
        allReviews = false;
        pick = -1;
        pickedHunt = null;
        Expand(false);
        SetColumn(false);
        if (!string.IsNullOrEmpty(shownId) && R0926HuntApi.CanCall) StartCoroutine(Fetch(gen, false));
    }

    private void Reload()
    {
        if (data != null && !string.IsNullOrEmpty(shownId)) StartCoroutine(Fetch(gen, true));
    }

    private IEnumerator Fetch(int g, bool keep)
    {
        string id = shownId;
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(EditorHostAlias)) id = EditorHostAlias;
#endif
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Get("/api/hunts/profile/" + UnityWebRequest.EscapeURL(id), (c, r) => { code = c; j = r; });
        if (g != gen || code != 200 || !j["ok"].AsBool) yield break;   // 로그인 전·서버 오류 — 칸 없이 예전 그대로
        data = j;
        bool open = !keep && expandFor == shownId && Time.realtimeSinceStartup - expandAt < 20f;
        if (open && !string.IsNullOrEmpty(expandHunt))
        {
            var list = j["me"]["can_review"].AsArray;
            for (int i = 0; i < list.Count; i++) if (list[i]["can"].AsBool && list[i]["hunt_id"].AsInt.ToString() == expandHunt) pick = i;
        }
        Paint();
        if (open && column != null && column.gameObject.activeSelf)
        {
            bool toForm = expandToForm;
            expandFor = null;
            Expand(true);
            if (toForm && form != null && form.activeSelf) StartCoroutine(ScrollTo((RectTransform)form.transform));
        }
    }

    // ── 숫자 줄의 '주최' 칸 ───────────────────────────────
    private void Paint()
    {
        var host = data["host"];
        int hosted = host["hosted"].AsInt;
        int joined = data["joined"].Count;   // 서버가 아직 안 주면 0 — 예전 그대로
        bool hosts = hosted > 0 || host["open"].Count > 0 || data["suspended"].AsBool;
        SetColumn(hosts || joined > 0);
        if (!hosts && joined == 0) { Expand(false); return; }
        if (hosts)
        {
            string line2 = !host["rating"].IsNull ? Stars(host["rating"].AsFloat) + " <b>" + host["rating"].AsFloat.ToString("0.0", CI) + "</b>"
                         : hosted > 0 ? T("평가 모으는 중", "Collecting ratings") : T("진행 중", "Live now");
            if (columnMain != null) columnMain.text = "<size=88><b>" + hosted + "</b></size>\n<size=38><color=" + GoldHex + ">" + line2 + "</color></size>";
            if (columnLabel != null) columnLabel.text = T("주최", "Hosted");
        }
        else
        {
            if (columnMain != null) columnMain.text = "<size=88><b>" + joined + "</b></size>\n<size=38><color=" + TealHex + ">" + T("진행 중", "Live now") + "</color></size>";
            if (columnLabel != null) columnLabel.text = T("참여", "Joined");
        }
        PaintSection();
    }

    private void SetColumn(bool on)
    {
        if (column != null && column.gameObject.activeSelf != on) column.gameObject.SetActive(on);
        if (statsRow != null)
        {
            if (baseSpacing < 0f) { baseSpacing = statsRow.spacing; baseAlign = statsRow.childAlignment; }
            float sp = on ? ColumnSpacing : baseSpacing;
            var al = on ? TextAnchor.UpperCenter : baseAlign;   // 숫자를 같은 높이에 — '주최' 칸만 아래로 길다
            if (statsRow.spacing != sp) statsRow.spacing = sp;
            if (statsRow.childAlignment != al) statsRow.childAlignment = al;
        }
        if (layout != null) layout.SetExtra(stats, on ? ColumnExtra : 0f);
    }

    private void Expand(bool on)
    {
        expanded = on && data != null;
        if (columnBox != null) columnBox.color = expanded ? BoxOn : BoxOff;
        if (section == null) return;
        if (section.gameObject.activeSelf != expanded) section.gameObject.SetActive(expanded);
        if (reasonBox != null && !expanded) reasonBox.SetActive(false);
        if (!expanded) { lastWant = -1f; if (layout != null) layout.SetExtra(section, 0f); return; }
        if (content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        lastWant = -1f;
        Fit();
        if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; }
    }

    // 펼침 칸은 내용 높이만큼 — 화면보다 길면 카드 배치가 줄이고 그 안을 스크롤한다
    private void Fit()
    {
        if (content == null || layout == null) return;
        float want = LayoutUtility.GetPreferredHeight(content);
        if (Mathf.Approximately(want, lastWant)) return;
        lastWant = want;
        layout.SetExtra(section, want);
    }

    // ── 펼침 ──────────────────────────────────────────────
    private void PaintSection()
    {
        var host = data["host"];
        var me = data["me"];
        bool mine = me["is_me"].AsBool;
        int hosted = host["hosted"].AsInt;
        bool hosts = hosted > 0 || host["open"].Count > 0;
        if (headRight != null)
            headRight.text = hosts ? (mine ? T("내 주최 기록", "My hosting") : T("주최 기록 · 리뷰", "Hosting · reviews"))
                                   : (mine ? T("내가 참여 중", "I'm playing") : T("참여 중", "Playing"));

        // 주최 정지 — 본인(과 관리자)에게만 온다
        bool suspended = data["suspended"].AsBool;
        if (suspendBox != null) suspendBox.SetActive(suspended);
        if (suspended && suspendText != null) suspendText.text = SuspendText(data["suspended_reason"]);

        // 주최 기록
        if (recordBox != null) recordBox.SetActive(hosted > 0);
        if (hosted > 0 && recordNums != null && recordNums.Length >= 4)
        {
            Num(recordNums[0], hosted.ToString(), T("주최", "hosted"));
            Num(recordNums[1], host["hosted_30d"].AsInt.ToString(), T("최근 30일", "30 days"));
            Num(recordNums[2], host["avg_participants"].AsFloat.ToString("0.#", CI), T("평균 참가", "avg players"));
            Num(recordNums[3], Ago(When(host["last_at"]), false), T("마지막", "last"));
            if (recordFoot != null) recordFoot.text = Foot(host);
        }

        // 지금 열린 게임 — 주최한 것 다음에 참여 중인 것. 줄마다 '주최'·'참여'
        Clear(openList);
        var open = host["open"].AsArray;
        var joined = data["joined"].AsArray;
        if (openList != null) openList.gameObject.SetActive(open.Count + joined.Count > 0);
        foreach (JSONNode h in open) OpenRow(h, false);
        foreach (JSONNode h in joined) OpenRow(h, true);

        // 참여자 평가
        if (rateBox != null) rateBox.SetActive(hosted > 0);
        int raters = host["raters"].AsInt;
        if (!host["rating"].IsNull)
        {
            float r = host["rating"].AsFloat;
            if (rateNum != null) rateNum.text = r.ToString("0.0", CI);
            if (rateStars != null) rateStars.text = Stars(r);
            if (rateCount != null) rateCount.text = T(raters + "명 평가", raters + (raters == 1 ? " rating" : " ratings"));
        }
        else
        {
            if (rateNum != null) rateNum.text = "–";
            if (rateStars != null) rateStars.text = "<size=40>" + T("평가 모으는 중", "Collecting ratings") + "</size>";
            if (rateCount != null) rateCount.text = raters > 0 ? T(raters + "명 평가", raters + (raters == 1 ? " rating" : " ratings")) : T("아직 평가가 없어요", "No ratings yet");
        }

        PaintForm(me, mine);
        PaintReviews();
        PaintActions();
    }

    // 열린 게임 한 줄 — 누르면 추가 창 보물찾기 카드의 그 게임 상세 (같은 게임을 다시 눌러도 연다)
    private void OpenRow(JSONNode h, bool joined)
    {
        if (openTemplate == null || openList == null) return;
        var row = Instantiate(openTemplate, openList);
        row.SetActive(true);
        string tag = joined ? "<color=" + TealHex + ">" + T("참여", "Joined") + "</color>  " : "<color=" + GoldHex + ">" + T("주최", "Host") + "</color>  ";
        SetText(row, "Title", tag + (h["official"].AsBool ? "★ " : "") + ((string)h["title"]).Replace("<", "＜"));
        SetText(row, "Sub", OpenSub(h));
        var dot = row.transform.Find("Dot")?.GetComponent<Image>();
        if (dot != null) dot.color = h["status"] == "live" ? Teal : Amber;
        int id = h["id"].AsInt;
        Wire(row.GetComponent<Button>(), () => R0926HuntPush.OpenHunt(id));
    }

    private static void Num(Text t, string n, string label)
    {
        if (t != null) t.text = "<size=60><b>" + n + "</b></size>\n<size=30><color=" + MutedHex + ">" + label + "</color></size>";
    }

    private static string SuspendText(string reason)
    {
        // 신고가 쌓여 멈춘 것(서버: '30일 안에 참가자 3명이 신고')과 관리자가 따로 멈춘 것
        string lead = string.IsNullOrEmpty(reason) || reason.Contains("신고")
            ? T("최근 30일 동안 참가자 3명이 신고해 ", "3 participants reported you in 30 days — ")
            : reason + " — ";
        return lead + T("<b>새 보물찾기를 열 수 없어요.</b> 관리자가 확인한 뒤 풀어 드려요. 이미 진행 중인 게임은 그대로예요.",
                        "<b>you can't open new hunts.</b> An admin will review and lift it. Games already running continue.");
    }

    private static string Foot(JSONNode host)
    {
        var first = When(host["first_at"]);
        if (!first.HasValue) return "";
        var f = first.Value.ToLocalTime();
        string s = T("첫 주최 " + f.Month + "월 " + f.Day + "일", "First hosted " + f.ToString("MMM d", CI));
        int hosted = host["hosted"].AsInt;
        double days = Math.Max(1.0, (DateTime.UtcNow - first.Value).TotalDays);
        double perWeek = hosted / Math.Max(1.0, days / 7.0), perMonth = hosted / Math.Max(1.0, days / 30.0);
        if (days >= 7 && perWeek >= 1) s += T(" · 주 " + Math.Round(perWeek) + "회꼴", " · ~" + Math.Round(perWeek) + "/week");
        else if (days >= 30 && perMonth >= 1) s += T(" · 월 " + Math.Round(perMonth) + "회꼴", " · ~" + Math.Round(perMonth) + "/month");
        return s;
    }

    private static string OpenSub(JSONNode h)
    {
        var now = DateTime.UtcNow;
        var parts = new List<string>();
        if (h["status"] == "scheduled")
        {
            var s = When(h["starts_at"]);
            parts.Add(s.HasValue ? T(Span(s.Value - now) + " 뒤 시작", "Starts in " + Span(s.Value - now)) : T("곧 시작", "Soon"));
        }
        else
        {
            var e = When(h["ends_at"]);
            parts.Add(e.HasValue ? T("진행 중 · " + Span(e.Value - now) + " 남음", "Live · " + Span(e.Value - now) + " left") : T("진행 중", "Live"));
        }
        parts.Add(T("참가 " + h["participants"].AsInt + "명", h["participants"].AsInt + " players"));
        int wm = h["winners_max"].AsInt;
        parts.Add(wm > 0 ? T(h["found"].AsInt + "/" + wm + " 찾음", h["found"].AsInt + "/" + wm + " found") : T(h["found"].AsInt + "명 찾음", h["found"].AsInt + " found"));
        return string.Join(" · ", parts);
    }

    // ── 별점 남기기 ───────────────────────────────────────
    private void PaintForm(JSONNode me, bool mine)
    {
        var list = me["can_review"].AsArray;
        var ok = new List<int>();
        for (int i = 0; i < list.Count; i++) if (list[i]["can"].AsBool) ok.Add(i);
        if (!ok.Contains(pick)) pick = ok.Count > 0 ? ok[0] : -1;
        bool on = !mine && pick >= 0;
        if (form != null) form.SetActive(on);

        // 참가했지만 나서지 않은 게임 — 남길 수 있는 게임이 없을 때만 까닭을 알린다
        string why = "";
        if (!mine && ok.Count == 0)
            foreach (JSONNode g in list)
                if (g["why"] == "not_played") { why = T("'" + g["title"] + "' — 보물을 찾았거나 게임 중 1km 안까지 간 참가자만 별점을 남길 수 있어요", "'" + g["title"] + "' — only players who found it or got within 1km can rate"); break; }
        if (notPlayed != null) { notPlayed.text = why; notPlayed.gameObject.SetActive(why.Length > 0); }
        if (!on) return;

        var g0 = list[pick];
        string hid = g0["hunt_id"].AsInt.ToString();
        if (pickedHunt != hid)
        {
            // 처음 보거나 게임을 바꿨다 — 그 게임의 내 별점·리뷰로 (다시 받기만 했으면 쓰던 것을 그대로 둔다)
            pickedHunt = hid;
            stars = g0["my_rating"].IsNull ? 0 : g0["my_rating"].AsInt;
            if (reviewInput != null) SetInput(g0["my_review"]);
        }
        var ended = When(g0["ended_at"]);
        var until = When(g0["rate_until"]);
        string day = ended.HasValue ? " (" + ended.Value.ToLocalTime().ToString("M/d", CI) + ")" : "";
        string till = until.HasValue ? until.Value.ToLocalTime().ToString("M/d", CI) : "";
        if (formTitle != null)
            formTitle.text = T("<b>" + g0["title"] + day + "</b> 어땠나요?", "How was <b>" + g0["title"] + day + "</b>?")
                           + (till.Length > 0 ? "  <size=30><color=" + MutedHex + ">" + T(till + "까지 고칠 수 있어요", "editable until " + till) + "</color></size>" : "");
        if (sendLabel != null) sendLabel.text = g0["my_rating"].IsNull ? T("남기기", "Post") : T("고치기", "Update");
        PaintStars();

        // 남길 게임이 여럿이면 고른다
        Clear(formGames);
        if (formGames != null) formGames.gameObject.SetActive(ok.Count > 1);
        if (ok.Count > 1 && chipTemplate != null && formGames != null)
            foreach (int i in ok)
            {
                var c = Instantiate(chipTemplate, formGames);
                c.SetActive(true);
                var e = When(list[i]["ended_at"]);
                SetText(c, "Label", list[i]["title"] + (e.HasValue ? " (" + e.Value.ToLocalTime().ToString("M/d", CI) + ")" : "")
                                    + (list[i]["my_rating"].IsNull ? "" : "  ★" + list[i]["my_rating"].AsInt));
                var img = c.GetComponent<Image>();
                if (img != null) img.color = i == pick ? ChipOn : ChipOff;
                var lt = c.transform.Find("Label")?.GetComponent<Text>();
                if (lt != null) lt.color = i == pick ? Gold : Soft;
                int k = i;
                Wire(c.GetComponent<Button>(), () => { pick = k; PaintForm(data["me"], false); });
            }
    }

    // 입력칸은 키보드 위 입력줄이 꺼 둔다(enabled=false) — 잠깐 켜서 넣어야 글이 보인다
    private void SetInput(string text)
    {
        bool was = reviewInput.enabled;
        if (!was) reviewInput.enabled = true;
        reviewInput.text = text ?? "";
        reviewInput.ForceLabelUpdate();
        if (!was) reviewInput.enabled = false;
    }

    private void PaintStars()
    {
        if (starButtons == null) return;
        for (int i = 0; i < starButtons.Length; i++)
        {
            var t = starButtons[i] != null ? starButtons[i].GetComponentInChildren<Text>(true) : null;
            if (t != null) t.color = i < stars ? Gold : StarOff;
        }
    }

    private IEnumerator Rate()
    {
        if (busy || data == null || pick < 0) yield break;
        if (stars < 1) { Toast(R0926HuntApi.ErrorText("stars", Lang), false); yield break; }
        var g = data["me"]["can_review"][pick];
        var b = new JSONObject();
        b["stars"] = stars;
        b["review"] = reviewInput != null ? (reviewInput.text ?? "").Trim() : "";
        b["device_id"] = SystemInfo.deviceUniqueIdentifier;
        busy = true;
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Post("/api/hunts/" + g["hunt_id"].AsInt + "/rate", b, (c, r) => { code = c; j = r; });
        busy = false;
        if (code == 200)
        {
            Toast(j["changed"].AsBool ? T("별점을 고쳤어요", "Rating updated") : T("별점을 남겼어요 — 고마워요!", "Thanks for rating!"), true);
            Reload();
        }
        else Toast(R0926HuntApi.ErrorText(code == 401 ? "login" : (string)j["error"], Lang), false);
    }

    // ── 참여자 리뷰 ───────────────────────────────────────
    private void PaintReviews()
    {
        if (data == null) return;
        var reviews = data["reviews"].AsArray;
        bool mine = data["me"]["is_me"].AsBool;
        if (reviewsBox != null) reviewsBox.SetActive(reviews.Count > 0);
        Clear(reviewList);
        int n = allReviews ? reviews.Count : Mathf.Min(ReviewsFirst, reviews.Count);
        for (int i = 0; i < n && reviewTemplate != null && reviewList != null; i++)
        {
            var rv = reviews[i];
            var row = Instantiate(reviewTemplate, reviewList);
            row.SetActive(true);
            SetText(row, "Body/Head", "<color=" + GoldHex + ">" + Stars(rv["stars"].AsInt) + "</color>  <color=" + MutedHex + ">"
                                      + rv["hunt"]["title"] + " · " + Ago(When(rv["at"]), true) + "</color>");
            SetText(row, "Body/Text", rv["text"]);
            // 내 프로필이면 리뷰마다 신고 — 관리자가 보고 숨긴다
            var rep = row.transform.Find("Report");
            if (rep != null)
            {
                rep.gameObject.SetActive(mine);
                int rid = rv["rid"].AsInt;
                Wire(rep.GetComponent<Button>(), () => OpenReasons("review", rid));
            }
        }
        if (moreButton != null)
        {
            moreButton.gameObject.SetActive(!allReviews && reviews.Count > ReviewsFirst);
            var t = moreButton.GetComponentInChildren<Text>(true);
            if (t != null) t.text = T("리뷰 " + (reviews.Count - ReviewsFirst) + "개 더 보기", "Show " + (reviews.Count - ReviewsFirst) + " more");
        }
    }

    // ── 신고 · 차단 ───────────────────────────────────────
    private void PaintActions()
    {
        if (data == null || actions == null) return;
        var me = data["me"];
        bool mine = me["is_me"].AsBool;
        actions.SetActive(!mine);
        if (mine) return;
        if (reportButton != null)
        {
            // 신고는 이 사람 게임에 참가한 적이 있어야 (서버가 다시 확인한다)
            reportButton.gameObject.SetActive(me["can_report"].AsBool);
            bool reported = me["reported"].AsBool;
            reportButton.interactable = !reported;
            var t = reportButton.GetComponentInChildren<Text>(true);
            if (t != null) t.text = reported ? T("신고함 — 관리자가 확인 중", "Reported — under review") : T("주최자 신고", "Report host");
        }
        if (blockButton != null)
        {
            bool blocked = me["blocked"].AsBool;
            var t = blockButton.GetComponentInChildren<Text>(true);
            if (t != null) t.text = blocked ? T("차단 풀기", "Unblock") : blockArmed > 0f ? T("한 번 더 누르면 차단", "Tap again to block") : T("차단", "Block");
        }
    }

    private void OpenReasons(string target, int rid)
    {
        if (data == null || reasonBox == null) return;
        reportTarget = target;
        reportRid = rid;
        var given = target == "host" ? data["report_reasons"] : data["review_report_reasons"];
        var order = new List<(string key, string en)>(target == "host" ? HostReasons : ReviewReasons);
        foreach (KeyValuePair<string, JSONNode> kv in given.AsObject)
            if (order.FindIndex(o => o.key == kv.Key) < 0) order.Add((kv.Key, kv.Value));
        Clear(reasonList);
        foreach (var o in order)
        {
            if (!given.HasKey(o.key) || reasonTemplate == null || reasonList == null) continue;
            var row = Instantiate(reasonTemplate, reasonList);
            row.SetActive(true);
            SetText(row, "Label", T(given[o.key], o.en));
            string key = o.key;
            Wire(row.GetComponent<Button>(), () => StartCoroutine(Report(key)));
        }
        if (reasonTitle != null)
            reasonTitle.text = target == "host" ? T("주최자를 신고하는 까닭을 골라 주세요", "Why are you reporting this host?")
                                                : T("이 리뷰를 신고하는 까닭을 골라 주세요", "Why are you reporting this review?");
        reasonBox.SetActive(true);
        StartCoroutine(ScrollTo((RectTransform)reasonBox.transform));
    }

    private IEnumerator Report(string reason)
    {
        if (busy || data == null) yield break;
        var b = new JSONObject();
        b["target"] = reportTarget;
        b["reason"] = reason;
        if (reportTarget == "host") b["host_id"] = data["user_id"];
        else b["rid"] = reportRid;
        busy = true;
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Post("/api/hunts/report", b, (c, r) => { code = c; j = r; });
        busy = false;
        if (code == 200)
        {
            if (reasonBox != null) reasonBox.SetActive(false);
            Toast(j["duplicate"].AsBool ? T("이미 신고했어요 — 관리자가 확인하고 있어요", "Already reported — under review")
                                        : T("신고했어요 — 관리자가 확인해요", "Reported — an admin will review it"), true);
            Reload();
        }
        else Toast(R0926HuntApi.ErrorText(code == 401 ? "login" : (string)j["error"], Lang), false);
    }

    // 차단은 두 번 눌러야 (잘못 눌러 차단되지 않게) · 풀기는 한 번에
    private void OnBlock()
    {
        if (busy || data == null) return;
        bool blocked = data["me"]["blocked"].AsBool;
        if (!blocked && blockArmed < 0f) { blockArmed = Time.unscaledTime; PaintActions(); return; }
        blockArmed = -99f;
        StartCoroutine(Block(!blocked));
    }

    private IEnumerator Block(bool on)
    {
        var b = new JSONObject();
        var user = LoginManager.Instance != null ? LoginManager.Instance.CurrentUser : null;
        b["blocker_id"] = user != null ? user.id : "";
        b["blocked_id"] = data["user_id"];
        if (on) b["reason"] = "hunt";
        busy = true;
        long code = 0;
        yield return R0926HuntApi.Post(on ? "/api/block" : "/api/unblock", b, (c, r) => code = c);
        busy = false;
        if (code == 200 || code == 201)
        {
            Toast(on ? T("차단했어요 — 이 사람 게임이 목록·알림에서 사라져요", "Blocked — their hunts won't show up or notify you")
                     : T("차단을 풀었어요", "Unblocked"), true);
            Reload();
        }
        else
        {
            Toast(on ? T("차단하지 못했어요 — 로그인 상태를 확인해 주세요", "Couldn't block — please check you're signed in")
                     : T("차단을 풀지 못했어요", "Couldn't unblock"), false);
            PaintActions();
        }
    }

    // ── 부품 ──────────────────────────────────────────────
    private IEnumerator ScrollTo(RectTransform target)
    {
        yield return null;
        yield return null;   // 펼친 칸이 자리를 잡은 뒤
        if (scroll == null || content == null || target == null || !target.gameObject.activeInHierarchy) yield break;
        Canvas.ForceUpdateCanvases();
        var view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        float top = -target.anchoredPosition.y - target.rect.height * (1f - target.pivot.y);
        float max = Mathf.Max(0f, content.rect.height - view.rect.height);
        scroll.StopMovement();
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(top - 24f, 0f, max));
    }

    private static void Clear(RectTransform list)
    {
        if (list == null) return;
        for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
    }

    private static void SetText(GameObject root, string child, string text)
    {
        var t = root.transform.Find(child)?.GetComponent<Text>();
        if (t != null) t.text = text ?? "";
    }

    private static void Toast(string s, bool ok)
    {
        if (ToastManager.Instance == null) return;
        if (ok) ToastManager.Instance.ShowSuccess(s);
        else ToastManager.Instance.ShowError(s);
    }

    // 별 다섯 — 반올림한 만큼 금색, 나머지 옅게
    private static string Stars(float r)
    {
        int full = Mathf.Clamp(Mathf.RoundToInt(r), 0, 5);
        return new string('★', full) + (full < 5 ? "<color=" + DimHex + ">" + new string('★', 5 - full) + "</color>" : "");
    }

    private static DateTime? When(JSONNode n)
    {
        if (n == null || n.IsNull || string.IsNullOrEmpty(n.Value)) return null;
        if (DateTime.TryParse(n.Value, CI, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)) return t;
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

    // 며칠 전 — 날짜(달력) 기준. ago 면 '전'을 붙인다 ('마지막' 칸은 '3일'만)
    private static string Ago(DateTime? at, bool ago)
    {
        if (!at.HasValue) return "–";
        int d = (int)(DateTime.Now.Date - at.Value.ToLocalTime().Date).TotalDays;
        if (d <= 0) return T("오늘", "today");
        if (d == 1) return T("어제", "yesterday");
        string ko = d < 14 ? d + "일" : d < 60 ? d / 7 + "주" : d / 30 + "달";
        string en = d < 14 ? d + "d" : d < 60 ? d / 7 + "w" : d / 30 + "mo";
        return T(ago ? ko + " 전" : ko, ago ? en + " ago" : en);
    }
}
