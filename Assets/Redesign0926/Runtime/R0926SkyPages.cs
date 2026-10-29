using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 하늘 옆 장 (2026-10-23 대표님 「'날씨' 를 옆으로 스와이프해서 … 근처 보물찾기 · 근처핫플 · 근처축제 … '언론사' 라이브」).
/// 하늘 날씨판을 좌우로만 밀면 날씨 → 근처 보물찾기 → 근처 축제 → 근처 핫플 → 근처 친구 → 언론사 라이브.
///  · 내용이 있는 장만 넣는다 (라이브는 늘). 설정의 '동네 소식 · 라이브'를 끄면 날씨 한 장
///  · 보물찾기: 내 범위 안 진행 중 · 곧 시작 게임 — 새 게임 알림을 못 받은 사람도 눌러서 참가 (R0926HuntList.Nearby)
///  · 축제: 50km 안 진행 중 · 2주 안에 시작 (R0926Nearby) — 누르면 축제 이름으로 찾은 쪽
///  · 핫플: 3km 안 요즘 좋아요 · 댓글이 많은 곳 (서버 /api/nearby/hot) — 누르면 AR 의 그 박스를 짚어 준다
///  · 축제 · 핫플 맨 위의 광고 줄(관리자가 띄운 협력 업체): '광고 · …' · 고른 이모지 그림 — 누르면 업체 링크(없으면 카카오맵의 그 자리). 보이면 노출 · 누르면 클릭을 센다
///  · 친구: 위치를 서로 공유하는 맞팔 친구 중 20km 안 (친구 지도와 같은 길) — 누르면 프로필
///  · 라이브: 언론사 유튜브 생방송
/// 데이터는 휴대폰을 하늘 쪽으로 들 때만 묻는다 (설정한 각도보다 조금 아래부터 — 판이 뜰 때 이미 와 있게).
/// 판(SkyWeather0926)은 보일 때만 켜져 있어 이 스크립트는 늘 켜진 표식에 두고, 손가락은 판 틀의 R0926SkyBoardInput 이 넘겨준다.
/// 판을 아래로 끌거나 📌 를 누르면 화면 아래쪽에 고정, 고정한 판을 위로 밀거나 📌 를 다시 누르면 하늘로 (R0926SkyWeather.SetPinned)
/// </summary>
public class R0926SkyPages : MonoBehaviour
{
    public enum Kind { Weather, Hunt, Festival, Hot, Friends, Live }

    [Serializable]
    public class Row
    {
        public RectTransform root;
        public Image bg;
        public Image icon;
        public Image face;      // 친구 — 둥근 바탕 (사진 · 첫 글자)
        public RawImage photo;
        public Text initial;
        public Text title;
        public Text sub;
        public Text right;
        [NonSerialized] public float flashUntil, noteUntil;
    }

    [Serializable]
    public class Page
    {
        public Kind kind;
        public RectTransform root;
        public CanvasGroup group;
        public RectTransform head;   // 머리 알약 (날씨 장은 지역 알약이 대신해 비어 있다)
        public Image headIcon;
        public Text headText;
        public Row[] rows;           // 날씨 · 라이브 장은 비어 있다
    }

    [SerializeField] private R0926SkyWeather sky;
    [Tooltip("판의 틀(Frame0926) — 손가락 자리를 판 좌표로 바꾼다 (고정하면 틀째 화면 UI 로 옮겨 간다)")]
    [SerializeField] private RectTransform board;
    [SerializeField] private Page[] pages;
    [Tooltip("라이브 장의 방송사 칸 — Channels 순서")]
    [SerializeField] private RectTransform[] liveChips;
    [SerializeField] private RectTransform dots;
    [SerializeField] private Image[] dotImages;
    [Tooltip("줄 그림 — r0926_f_* (축제) · r0926_c_* (장소 분류) · r0926_s_* · r0926_i_treasure")]
    [SerializeField] private Sprite[] icons;

    [Header("밀기 (판 단위 — 판 폭 1000)")]
    [SerializeField] private float pageSpacing = 1150f;
    [SerializeField] private float flipDistance = 170f;
    [SerializeField] private float flipSpeed = 1400f;

    [Header("고정 — 📌 · 아래로 끌기 / 위로 밀기")]
    [SerializeField] private RectTransform pinButton;
    [SerializeField] private Image pinIcon;
    [Tooltip("이만큼 끌면 놓았을 때 고정 · 풀기 (판 단위)")]
    [SerializeField] private float pinPull = 130f;

    [Header("범위")]
    [SerializeField] private float festivalKm = 50f;
    [SerializeField] private int festivalSoonDays = 14;
    [SerializeField] private float friendKm = 20f;

    // 언론사 유튜브 생방송 — '@채널/live' 는 그 채널이 지금 하는 생방송으로 열린다 (10-25 여섯 곳 모두 생방송 중인 것을 확인)
    private static readonly string[] Handles = { "ytnnews24", "yonhapnewstv23", "newskbs", "MBCNEWS11", "sbsnews8", "jtbc_news" };

    private static readonly Color RowColor = new Color(0.094f, 0.118f, 0.149f, 0.34f);
    private static readonly Color FlashColor = new Color(0.95f, 0.48f, 0.63f, 0.62f);
    private static readonly Color PinGold = new Color(1f, 0.82f, 0.48f, 1f);   // 위쪽 하늘 칩의 노란 점과 같은 색
    private static readonly Color[] Palette =   // 친구 지도와 같은 색 (같은 사람은 같은 색)
    {
        new Color(0.97f, 0.78f, 0.45f), new Color(0.56f, 0.77f, 1f), new Color(0.72f, 0.91f, 0.53f),
        new Color(1f, 0.62f, 0.71f), new Color(0.79f, 0.71f, 1f), new Color(0.49f, 0.86f, 0.84f),
    };
    private const float DotSize = 18f, DotWide = 46f, DotGap = 16f;
    private const float FlashTime = 0.35f, NoteTime = 2.5f;
    private const string SwipedKey = "Sky_Swiped";
    private const string PinKey = "Sky_PinUsed";
    private const float PinTilt = 35f;   // 안 꽂힌 핀은 비스듬히

    private readonly List<int> shown = new List<int>();   // 지금 넣은 장 — pages 의 칸 번호, 왼쪽부터
    private readonly List<int> next = new List<int>();
    private float pos, vel;      // 보는 자리 (shown 의 칸 번호 — 미는 중엔 사이 값)
    private int target;
    private Kind current = Kind.Weather;
    private bool wasVisible;
    private float hiddenAt = -999f;
    private float nextCollect, nextPrefetch;
    private int huntVer = -1, nearVer = -1, friendVer = -1;

    private readonly List<R0926Nearby.Festival> fests = new List<R0926Nearby.Festival>();
    private readonly List<Mate> mates = new List<Mate>();
    private Dictionary<string, Sprite> iconMap;
    private readonly Dictionary<Text, string> raw = new Dictionary<Text, string>();

    // ── 친구 (친구 지도와 같은 서버 길 — 서로 위치를 공유하는 맞팔만 온다) ──
    [Serializable] private class Friend { public string user_id; public string username; public string avatar_url; public float lat; public float lon; public int ago; }
    [Serializable] private class FriendsResp { public string status; public bool sharing; public Friend[] friends; }
    private struct Mate { public Friend f; public float km; public bool exact; }
    private readonly List<Friend> friends = new List<Friend>();
    private int friendsVersion;
    private bool friendsBusy;
    private float nextFriends;
    private float profileWaitUntil;
    private static readonly Dictionary<string, Texture2D> avatars = new Dictionary<string, Texture2D>();

    // ── 밀기 ──
    private bool dragging, dragMoved, pressing;
    private int dragPointer;
    private float dragStartX, dragStartPos, lastX, lastT, flingV;
    private float hintAt = -1f, hintOffset;
    private int hintsLeft = 3;

    // ── 고정 (위아래로 끌기) ──
    private bool pulling, armed;
    private float pullStartY, pullRaw, pull, pullVel, lastY;
    private float pinLook = -1f, pinScale = 1f, pinHintAt = -1f;
    private int pinHintsLeft = 2;

    private void Update()
    {
        if (sky == null || board == null || pages == null || pages.Length == 0) return;
        bool eventsOn = R0926SkySettings.Enabled && R0926SkySettings.Events;
        bool visible = board.gameObject.activeInHierarchy;
        if (eventsOn && (visible || sky.Pitch > R0926SkySettings.MinPitch - 12f)) Prefetch();
        if (!visible)
        {
            if (wasVisible)
            {
                wasVisible = false; hiddenAt = Time.unscaledTime; dragging = false; hintAt = -1f; hintOffset = 0f;
                pulling = armed = pressing = false; pull = pullVel = 0f; pinHintAt = -1f;
                sky.SetPull(0f);
            }
            return;
        }
        if (!wasVisible) Shown();
        wasVisible = true;

        bool changed = huntVer != R0926HuntList.NearbyVersion || nearVer != R0926Nearby.Version || friendVer != friendsVersion;
        if (changed || Time.unscaledTime >= nextCollect)
        {
            huntVer = R0926HuntList.NearbyVersion; nearVer = R0926Nearby.Version; friendVer = friendsVersion;
            nextCollect = Time.unscaledTime + 1f;   // 걸으면 거리가 바뀐다
            Collect();
            Rebuild(eventsOn);
            Bind();
        }
        // 손가락이 판에 닿아 있는 동안은 휴대폰이 기울어도 판을 붙잡는다 (손을 뗐는데 신호가 안 온 경우는 화면에 닿은 손가락이 없으면 푼다)
        if (pressing) { if (Touches() > 0) sky.Hold(); else pressing = false; }
        Animate();
        Layout();
        sky.SetPull(pull);
    }

    // 판이 다시 뜰 때 — 오래(2분) 안 봤으면 첫 장부터, 아직 한 번도 안 밀어 봤으면 옆 장이 있다고 살짝 밀어 보인다.
    // 고정을 아직 안 해 봤으면 그다음에 📌 가 두어 번 콩콩
    private void Shown()
    {
        if (Time.unscaledTime - hiddenAt > 120f) { target = 0; pos = 0f; vel = 0f; current = Kind.Weather; }
        nextCollect = 0f;
        hintAt = PlayerPrefs.GetInt(SwipedKey, 0) == 0 && hintsLeft > 0 ? Time.unscaledTime + 1.6f : -1f;
        pinHintAt = PlayerPrefs.GetInt(PinKey, 0) == 0 && pinHintsLeft > 0 && !sky.Pinned ? Time.unscaledTime + 3f : -1f;
    }

    // ── 받기 ─────────────────────────────────────────────────
    private void Prefetch()
    {
        if (Time.unscaledTime < nextPrefetch) return;
        nextPrefetch = Time.unscaledTime + 2f;
        var h = R0926HuntList.RefreshIfDue();
        if (h != null) StartCoroutine(h);
        if (!friendsBusy && Time.unscaledTime >= nextFriends) StartCoroutine(FetchFriends());
        if (!R0926HuntCard.TryLocation(out double lat, out double lon, out _)) return;
        var f = R0926Nearby.FestivalsIfDue(lat, lon);
        if (f != null) StartCoroutine(f);
        var t = R0926Nearby.HotIfDue(lat, lon);
        if (t != null) StartCoroutine(t);
    }

    private IEnumerator FetchFriends()
    {
        var lm = LoginManager.Instance;
        if (lm == null || !lm.IsLoggedIn || string.IsNullOrEmpty(lm.CurrentUser.id))
        {
            if (friends.Count > 0) { friends.Clear(); friendsVersion++; }
            nextFriends = Time.unscaledTime + 30f;
            yield break;
        }
        friendsBusy = true;
        nextFriends = Time.unscaledTime + 60f;
        using (var req = UnityWebRequest.Get(FriendsUrl()))
        {
            LoginManager.ApplyAuth(req);
            req.timeout = 10;
            yield return req.SendWebRequest();
            friendsBusy = false;
            if (req.responseCode == 401) { friends.Clear(); friendsVersion++; yield break; }
            if (req.result != UnityWebRequest.Result.Success) yield break;   // 끊김 — 지난 목록 그대로
            FriendsResp r = null;
            try { r = JsonUtility.FromJson<FriendsResp>(req.downloadHandler.text); } catch (Exception) { }
            if (r == null) yield break;
            friends.Clear();
            if (r.sharing && r.friends != null) friends.AddRange(r.friends);
            friendsVersion++;
        }
    }

    private static string FriendsUrl()
    {
        string url = ApiConfig.P2P_SERVER + "/api/p2p/friends_map";
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(R0926FriendsMap.EditorBase)) return R0926FriendsMap.EditorBase + url.Substring(ApiConfig.MAIN_SERVER.Length);
#endif
        return url;
    }

    // 축제는 가까운 · 곧 하는 것만, 친구는 20km 안 — 1초마다 (걸으면 바뀐다)
    private void Collect()
    {
        bool hasMe = R0926HuntCard.TryLocation(out double myLat, out double myLon, out _);
        DateTime today = DateTime.Now.Date;
        fests.Clear();
        foreach (var f in R0926Nearby.Festivals)
        {
            if (f == null || string.IsNullOrEmpty(f.title)) continue;
            if (!f.promo)   // 광고 줄은 관리자가 정한 반경 · 기간을 서버가 이미 맞췄다
            {
                if (f.dist > festivalKm * 1000f) continue;
                if (R0926Nearby.TryDate(f.end, out var e) && e < today) continue;
                if (R0926Nearby.TryDate(f.start, out var s) && s > today.AddDays(festivalSoonDays)) continue;
            }
            fests.Add(f);
        }
        // 광고 줄 맨 위, 그다음 하는 중인 것, 그다음 가까운 순
        fests.Sort((a, b) =>
        {
            if (a.promo != b.promo) return a.promo ? -1 : 1;
            bool na = !R0926Nearby.TryDate(a.start, out var sa) || sa <= today, nb = !R0926Nearby.TryDate(b.start, out var sb) || sb <= today;
            return na != nb ? (na ? -1 : 1) : a.dist.CompareTo(b.dist);
        });

        mates.Clear();
        if (hasMe)
        {
            var p2p = P2PManager.Instance;
            foreach (var f in friends)
            {
                double la = f.lat, lo = f.lon;
                bool exact = false;
                // 근처(P2P 공유 반경 안)면 AR 과 같은 정확한 자리 — 서버 자리는 약 1km 칸 (친구 지도와 같다)
                if (p2p != null && p2p.TryGetNearbyUser(f.user_id, out var u) && (u.latitude != 0 || u.longitude != 0)) { la = u.latitude; lo = u.longitude; exact = true; }
                float km = (float)(R0926Nearby.Meters(myLat, myLon, la, lo) / 1000.0);
                if (km <= friendKm) mates.Add(new Mate { f = f, km = km, exact = exact });
            }
            mates.Sort((a, b) => a.f.ago != b.f.ago ? a.f.ago.CompareTo(b.f.ago) : a.km.CompareTo(b.km));   // 마지막 접속이 가까운 친구가 위 (10-29)
        }
    }

    private int Count(Kind k)
    {
        switch (k)
        {
            case Kind.Hunt: return R0926HuntList.Nearby.Count;
            case Kind.Festival: return fests.Count;
            case Kind.Hot: return R0926Nearby.HotPlaces.Count;
            case Kind.Friends: return mates.Count;
            case Kind.Live: return Handles.Length;
            default: return 0;
        }
    }

    // 넣을 장을 다시 고른다 — 보던 장이 그대로 있으면 그 장을 지킨다 (앞에 장이 끼거나 빠져도 화면이 튀지 않게)
    private void Rebuild(bool eventsOn)
    {
        next.Clear();
        for (int i = 0; i < pages.Length; i++)
        {
            var k = pages[i].kind;
            bool on = k == Kind.Weather ? sky.WeatherReady : eventsOn && Count(k) > 0;
            if (on) next.Add(i);
        }
        if (Same(next, shown)) return;
        int now = next.FindIndex(i => pages[i].kind == current);
        if (now < 0) now = Mathf.Clamp(target, 0, Mathf.Max(0, next.Count - 1));
        pos += now - target;
        dragStartPos += now - target;   // 미는 중이면 손가락 밑의 장이 그대로 따라온다
        target = now;
        shown.Clear();
        shown.AddRange(next);
        if (shown.Count > 0) current = pages[shown[target]].kind;
    }

    private static bool Same(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // ── 그리기 ───────────────────────────────────────────────
    private void Bind()
    {
        foreach (int i in shown)
        {
            var p = pages[i];
            Head(p);
            if (p.rows == null) continue;
            for (int r = 0; r < p.rows.Length; r++)
            {
                var row = p.rows[r];
                if (row == null || row.root == null) continue;
                bool on = r < Count(p.kind);
                if (row.root.gameObject.activeSelf != on) row.root.gameObject.SetActive(on);
                if (on) BindRow(p.kind, r, row);
            }
        }
    }

    private void Head(Page p)
    {
        if (p.headText == null) return;
        string t;
        switch (p.kind)
        {
            case Kind.Hunt: t = L("근처 보물찾기", "Treasure hunts nearby", "近くの宝探し", "附近寻宝", "Tesoros cerca"); break;
            case Kind.Festival: t = L("근처 축제", "Festivals nearby", "近くのお祭り", "附近节庆", "Festivales cerca"); break;
            case Kind.Hot: t = L("근처 핫플", "Hot spots nearby", "近くの人気スポット", "附近热门", "Lugares de moda"); break;
            case Kind.Friends: t = L("근처 친구", "Friends nearby", "近くの友だち", "附近好友", "Amigos cerca"); break;
            case Kind.Live: t = L("언론사 라이브", "News live", "ニュースライブ", "新闻直播", "Noticias en directo"); break;
            default: return;
        }
        if (raw.TryGetValue(p.headText, out var old) && old == t) return;
        raw[p.headText] = t;
        p.headText.text = t;
        if (p.head == null) return;
        var lr = p.headText.rectTransform;   // 알약 폭 = 그림 쪽 안쪽 여백 + 글 + 오른쪽 여백 (날씨 장의 지역 알약과 같은 꼴)
        p.head.sizeDelta = new Vector2(lr.offsetMin.x - lr.offsetMax.x + p.headText.preferredWidth, p.head.sizeDelta.y);
    }

    private void BindRow(Kind k, int r, Row row)
    {
        bool face = k == Kind.Friends;
        if (row.icon != null && row.icon.gameObject.activeSelf == face) row.icon.gameObject.SetActive(!face);
        if (row.face != null && row.face.gameObject.activeSelf != face) row.face.gameObject.SetActive(face);
        string title, sub, right;
        switch (k)
        {
            case Kind.Hunt:
            {
                var n = R0926HuntList.Nearby[r];
                if (n.locked) SetIcon(row, "r0926_i_treasure_lock", "r0926_i_treasure");   // 비밀번호를 건 보물 — 자물쇠 배지
                else SetIcon(row, "r0926_i_treasure");
                title = n.title;
                sub = HuntSub(n);
                right = n.meters >= 0f ? R0926Nearby.Km(n.meters) : n.bandM > 0f ? R0926HuntList.Label(R0926HuntList.Coord.Hidden, 0f, 0f, n.bandM) : "";
                break;
            }
            case Kind.Festival:
            {
                var f = fests[r];
                if (f.promo) Promo(row, k, f.ad_id, f.emoji_url, "r0926_f_" + f.icon, "r0926_f_party");
                else SetIcon(row, "r0926_f_" + f.icon, "r0926_f_party");
                title = f.title;
                sub = f.promo ? PromoSub(f.sub) : FestSub(f);
                right = R0926Nearby.Km(f.dist);
                break;
            }
            case Kind.Hot:
            {
                var h = R0926Nearby.HotPlaces[r];
                if (h.promo) Promo(row, k, h.ad_id, h.emoji_url, "r0926_f_" + h.icon, "r0926_c_" + h.icon, "r0926_s_hot");
                else SetIcon(row, "r0926_c_" + h.category, "r0926_s_hot");
                title = h.name;
                sub = HotSub(h);
                double m = R0926HuntCard.TryLocation(out double la, out double lo, out _) ? R0926Nearby.Meters(la, lo, h.lat, h.lon) : h.dist;
                right = R0926Nearby.Km((float)m);
                break;
            }
            case Kind.Friends:
            {
                var m = mates[r];
                Face(row, m.f);
                title = string.IsNullOrEmpty(m.f.username) ? "?" : m.f.username;
                sub = R0926FriendsMap.Ago(m.f.ago);
                right = R0926FriendsMap.Dist(m.km, m.exact);
                break;
            }
            default: return;
        }
        if (Time.unscaledTime < row.noteUntil) sub = L("아직 AR화면 내에 없음", "Not in AR view yet", "まだAR画面にありません", "尚未出现在AR画面中", "Aún no está en la vista AR");
        Put(row.right, right);
        // 제목 · 둘째 줄은 오른쪽 거리 글자 앞까지
        if (row.right != null)
        {
            float edge = -(-row.right.rectTransform.anchoredPosition.x + row.right.preferredWidth + 28f);
            foreach (var t in new[] { row.title, row.sub })
            {
                if (t == null) continue;
                var tr = t.rectTransform;
                if (Mathf.Abs(tr.offsetMax.x - edge) > 1f) { tr.offsetMax = new Vector2(edge, tr.offsetMax.y); raw.Remove(t); }
            }
        }
        Put(row.title, title);
        Put(row.sub, sub);
    }

    private string HuntSub(R0926HuntList.Near n)
    {
        if (n.found) return L("찾은 보물입니다", "Treasure found", "見つけた宝です", "已找到的宝藏", "Tesoro encontrado");   // 게임은 아직 진행 중 — 못 찾은 게임 뒤에 남는다
        string state = n.joined ? L("참가 중", "Joined", "参加中", "已参加", "Participando")
                     : n.live ? L("진행 중", "On now", "開催中", "进行中", "En curso")
                     : Starts(n.startsAt);
        string more = n.prize.Length > 0 ? L("선물 ", "Prize: ", "景品 ", "奖品 ", "Premio: ") + n.prize
                    : n.maxPlayers > 0 ? n.players + "/" + n.maxPlayers + L("명", "", "人", "人", "")
                    : n.players > 0 ? string.Format(L("{0}명 참가", "{0} joined", "{0}人参加", "{0}人参加", "{0} participan"), n.players) : "";
        string s = n.official ? L("공식", "Official", "公式", "官方", "Oficial") + " · " + state : state;
        return more.Length > 0 ? s + " · " + more : s;
    }

    private static string Starts(DateTime at)
    {
        if (at == DateTime.MinValue) return L("곧 시작", "Starting soon", "まもなく開始", "即将开始", "Empieza pronto");
        string hm = at.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (at.Date == DateTime.Now.Date) return L("오늘 " + hm + " 시작", "Starts " + hm, "今日 " + hm + " 開始", "今天 " + hm + " 开始", "Empieza a las " + hm);
        string md = at.Month + "." + at.Day;
        return L(md + " " + hm + " 시작", "Starts " + at.Month + "/" + at.Day + " " + hm, at.Month + "/" + at.Day + " " + hm + " 開始",
                 at.Month + "/" + at.Day + " " + hm + " 开始", "Empieza el " + at.Day + "/" + at.Month + " " + hm);
    }

    // 진행 중이면 언제까지, 곧 하면 언제부터 언제까지
    private static string FestSub(R0926Nearby.Festival f)
    {
        DateTime today = DateTime.Now.Date;
        bool hasS = R0926Nearby.TryDate(f.start, out var s), hasE = R0926Nearby.TryDate(f.end, out var e);
        if (hasS && s > today)
            return hasE && e > s ? L(s.Month + "." + s.Day + " ~ " + e.Month + "." + e.Day, s.Month + "/" + s.Day + " – " + e.Month + "/" + e.Day,
                                     s.Month + "/" + s.Day + "〜" + e.Month + "/" + e.Day, s.Month + "/" + s.Day + " – " + e.Month + "/" + e.Day,
                                     s.Day + "/" + s.Month + " – " + e.Day + "/" + e.Month)
                                 : R0926Nearby.When(f.start, f.end);
        if (hasE && e == today) return R0926Nearby.When(f.start, f.end);
        string on = L("진행 중", "On now", "開催中", "进行中", "En curso");
        return hasE ? on + " · " + L(e.Month + "." + e.Day + "까지", "until " + e.Month + "/" + e.Day, e.Month + "/" + e.Day + "まで", "至" + e.Month + "/" + e.Day, "hasta el " + e.Day + "/" + e.Month) : on;
    }

    private static string HotSub(R0926Nearby.Hot h)
    {
        // 협력 업체 줄은 '광고'를 먼저 밝힌다
        if (h.promo) return PromoSub(h.sub);
        var parts = new List<string>(2);
        if (h.likes > 0) parts.Add(string.Format(L("좋아요 {0}", "{0} likes", "いいね {0}", "{0} 个赞", "{0} me gusta"), h.likes));
        if (h.comments > 0) parts.Add(string.Format(L("댓글 {0}", "{0} comments", "コメント {0}", "{0} 条评论", "{0} comentarios"), h.comments));
        if (parts.Count == 0) return h.fresh ? L("새로 올라온 곳", "Newly added", "新しく追加", "新上线", "Recién añadido") : "";
        return string.Join(" · ", parts);
    }

    private void SetIcon(Row row, params string[] names)
    {
        if (row.icon == null) return;
        Sprite s = null;
        foreach (var n in names) if ((s = Icon(n)) != null) break;
        if (row.icon.sprite != s) row.icon.sprite = s;
        row.icon.enabled = s != null;
    }

    // 광고 줄 — 관리자가 고른 이모지 그림(받는 동안은 기본 그림). 지금 보는 장이면 노출을 센다 (같은 광고는 앱을 켤 때마다 한 번)
    private void Promo(Row row, Kind k, string adId, string emojiUrl, params string[] names)
    {
        var s = R0926Nearby.Emoji(emojiUrl, this);
        if (s != null && row.icon != null)
        {
            if (row.icon.sprite != s) row.icon.sprite = s;
            row.icon.enabled = true;
        }
        else SetIcon(row, names);
        if (k == current) R0926Nearby.AdView(this, adId, k == Kind.Hot ? "hot" : "festival");
    }

    // 광고 줄 둘째 줄 — '광고'를 먼저 밝힌다
    private static string PromoSub(string sub) => R0926Nearby.AdLabel + (string.IsNullOrEmpty(sub) ? "" : " · " + sub);

    private Sprite Icon(string name)
    {
        if (iconMap == null)
        {
            iconMap = new Dictionary<string, Sprite>();
            if (icons != null) foreach (var s in icons) if (s != null) iconMap[s.name] = s;
        }
        return !string.IsNullOrEmpty(name) && iconMap.TryGetValue(name, out var sp) ? sp : null;
    }

    private void Face(Row row, Friend f)
    {
        if (row.face != null) row.face.color = Palette[((f.username ?? "").GetHashCode() & 0x7fffffff) % Palette.Length];
        if (row.initial != null) row.initial.text = string.IsNullOrEmpty(f.username) ? "?" : f.username.Substring(0, 1).ToUpperInvariant();
        if (row.photo == null) return;
        Texture2D tex = null;
        if (!string.IsNullOrEmpty(f.avatar_url) && !avatars.TryGetValue(f.avatar_url, out tex)) StartCoroutine(LoadAvatar(f.avatar_url));
        row.photo.texture = tex;
        row.photo.enabled = tex != null;
    }

    private IEnumerator LoadAvatar(string url)
    {
        avatars[url] = null;   // 같은 사진을 두 번 받지 않게
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            avatars[url] = DownloadHandlerTexture.GetContent(req);
        }
        nextCollect = 0f;   // 다음 프레임에 사진을 끼운다
    }

    // 한 줄에 안 들어가면 끝을 줄이고 '…' (긴 축제 이름 · 선물)
    private void Put(Text t, string s)
    {
        if (t == null) return;
        s = s ?? "";
        if (raw.TryGetValue(t, out var old) && old == s) return;
        raw[t] = s;
        t.text = s;
        float w = t.rectTransform.rect.width;
        if (w <= 0f || t.preferredWidth <= w) return;
        int lo = 0, hi = s.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            t.text = Cut(s, mid);
            if (t.preferredWidth <= w) lo = mid; else hi = mid - 1;
        }
        t.text = Cut(s, lo);
    }

    private static string Cut(string s, int n)
    {
        if (n > 0 && n < s.Length && char.IsHighSurrogate(s[n - 1])) n--;   // 이모지 반쪽이 남지 않게
        return s.Substring(0, n).TrimEnd() + "…";
    }

    // ── 움직임 ───────────────────────────────────────────────
    private void Animate()
    {
        float now = Time.unscaledTime;
        if (!dragging) pos = Mathf.SmoothDamp(pos, target, ref vel, 0.16f, Mathf.Infinity, Time.unscaledDeltaTime);
        if (!pulling) pull = Mathf.Abs(pull) < 0.5f ? 0f : Mathf.SmoothDamp(pull, 0f, ref pullVel, 0.12f, Mathf.Infinity, Time.unscaledDeltaTime);
        hintOffset = 0f;
        if (hintAt > 0f && now >= hintAt && !dragging && shown.Count > 1)
        {
            float t = (now - hintAt) / 0.9f;
            if (t >= 1f) { hintAt = -1f; hintsLeft--; }
            else hintOffset = (target < shown.Count - 1 ? -1f : 1f) * 150f * Mathf.Sin(t * Mathf.PI);
        }
    }

    private void Layout()
    {
        for (int i = 0; i < pages.Length; i++)
        {
            var p = pages[i];
            if (p.root == null) continue;
            int si = shown.IndexOf(i);
            float x = si < 0 ? 0f : (si - pos) * pageSpacing + hintOffset;
            float a = si < 0 ? 0f : Mathf.Clamp01(1f - Mathf.Abs(x) / pageSpacing * 1.1f);
            bool on = a > 0.001f;
            if (p.root.gameObject.activeSelf != on) p.root.gameObject.SetActive(on);
            if (!on) continue;
            p.root.anchoredPosition = new Vector2(x, 0f);
            float s = Mathf.Lerp(1f, 0.9f, Mathf.Clamp01(Mathf.Abs(x) / pageSpacing));
            p.root.localScale = new Vector3(s, s, 1f);
            if (p.group != null) p.group.alpha = a;
            if (p.rows == null) continue;
            foreach (var row in p.rows)
            {
                if (row?.bg == null) continue;
                float f = Mathf.Clamp01((row.flashUntil - Time.unscaledTime) / FlashTime);
                row.bg.color = Color.Lerp(RowColor, FlashColor, f);
            }
        }
        LayoutDots();
        LayoutPin();
    }

    // 📌 — 꽂혀 있으면(고정) 금색으로 곧게, 아니면 흰색으로 비스듬히. 끄는 중엔 놓으면 될 모양을 미리 · 조금 크게
    private void LayoutPin()
    {
        if (pinIcon == null) return;
        bool look = sky.Pinned != armed;
        float to = look ? 1f : 0f;
        float was = pinLook;
        pinLook = pinLook < 0f ? to : Mathf.MoveTowards(pinLook, to, Time.unscaledDeltaTime * 6f);
        float s = armed ? 1.2f : 1f;
        if (pinHintAt > 0f && Time.unscaledTime >= pinHintAt)
        {
            float t = Time.unscaledTime - pinHintAt;
            if (t >= 1.2f) { pinHintAt = -1f; pinHintsLeft--; }
            else s += 0.22f * Mathf.Abs(Mathf.Sin(t / 1.2f * Mathf.PI * 2f));
        }
        pinScale = Mathf.MoveTowards(pinScale, s, Time.unscaledDeltaTime * 3f);
        var rt = pinIcon.rectTransform;
        if (!Mathf.Approximately(rt.localScale.x, pinScale)) rt.localScale = new Vector3(pinScale, pinScale, 1f);
        if (Mathf.Approximately(was, pinLook)) return;
        rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(PinTilt, 0f, pinLook));
        pinIcon.color = Color.Lerp(Color.white, PinGold, pinLook);
    }

    private void LayoutDots()
    {
        if (dots == null || dotImages == null) return;
        int n = Mathf.Min(shown.Count, dotImages.Length);
        bool on = n > 1;
        if (dots.gameObject.activeSelf != on) dots.gameObject.SetActive(on);
        if (!on) return;
        float at = Mathf.Clamp(pos, 0f, n - 1);
        float total = -DotGap;
        for (int i = 0; i < n; i++) total += Mathf.Lerp(DotSize, DotWide, Mathf.Clamp01(1f - Mathf.Abs(i - at))) + DotGap;
        float x = -total / 2f;
        for (int i = 0; i < dotImages.Length; i++)
        {
            var d = dotImages[i];
            if (d == null) continue;
            bool show = i < n;
            if (d.gameObject.activeSelf != show) d.gameObject.SetActive(show);
            if (!show) continue;
            float k = Mathf.Clamp01(1f - Mathf.Abs(i - at));
            float w = Mathf.Lerp(DotSize, DotWide, k);
            d.rectTransform.sizeDelta = new Vector2(w, DotSize);
            d.rectTransform.anchoredPosition = new Vector2(x + w / 2f, 0f);
            d.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.42f, 1f, k));
            x += w + DotGap;
        }
    }

    // ── 손가락 (R0926SkyBoardInput 이 넘겨준다) ──────────────
    public void PointerDown()
    {
        dragMoved = false;
        pressing = true;
        sky.Hold();   // 누르는 손에 휴대폰이 기울어도 판이 사라지지 않게
    }

    public void PointerUp() => pressing = false;

    public void BeginDrag(PointerEventData e)
    {
        dragMoved = true;
        sky.Hold();
        if (!Local(e.position, out var at)) return;
        Vector2 d = e.position - e.pressPosition;   // 첫 걸음(delta)만 보면 손떨림에 갈린다 — 누른 데서부터
        if (Mathf.Abs(d.y) > Mathf.Abs(d.x))
        {
            // 위아래 — 하늘 판은 아래로 끌어 고정, 고정한 판은 위로 밀어 하늘로
            pulling = true;
            armed = false;
            dragPointer = e.pointerId;
            pullStartY = lastY = at.y;
            pullRaw = 0f;
            lastT = Time.unscaledTime;
            flingV = 0f;
            pullVel = 0f;
            pinHintAt = -1f;
            return;
        }
        if (shown.Count <= 1) return;
        dragging = true;
        dragPointer = e.pointerId;
        dragStartX = lastX = at.x;
        dragStartPos = pos;
        lastT = Time.unscaledTime;
        flingV = 0f;
        vel = 0f;
        hintAt = -1f;
    }

    public void Drag(PointerEventData e)
    {
        if (e.pointerId != dragPointer || !dragging && !pulling) return;
        sky.Hold();
        if (Touches() >= 2) { dragging = pulling = armed = false; return; }   // 두 손가락(확대)이 시작되면 놓는다 — 제자리로
        if (!Local(e.position, out var at)) return;
        if (pulling) { Pull(at.y); return; }
        float x = at.x;
        float p = dragStartPos - (x - dragStartX) / pageSpacing;
        float max = shown.Count - 1;
        if (p < 0f) p *= 0.3f; else if (p > max) p = max + (p - max) * 0.3f;   // 끝 장에선 고무줄처럼
        pos = p;
        float dt = Time.unscaledTime - lastT;
        if (dt > 0.0001f) flingV = Mathf.Lerp(flingV, (x - lastX) / dt, 0.5f);
        lastX = x;
        lastT = Time.unscaledTime;
    }

    // 판이 손가락을 고무줄처럼 따라간다 — 고정 · 풀기 쪽으로는 넉넉히, 반대쪽으로는 조금만
    private void Pull(float y)
    {
        float dt = Time.unscaledTime - lastT;
        if (dt > 0.0001f) flingV = Mathf.Lerp(flingV, (y - lastY) / dt, 0.5f);
        lastY = y;
        lastT = Time.unscaledTime;
        pullRaw = y - pullStartY;
        float way = sky.Pinned ? pullRaw : -pullRaw;   // + 면 놓았을 때 고정 · 풀기 쪽
        float give = way > 0f ? Rubber(pullRaw, 0.8f, 170f) : Rubber(pullRaw, 0.25f, 50f);
        pull = give;
        bool arm = way > pinPull;
        if (arm == armed) return;
        armed = arm;
        if (arm) Haptic();   // 여기서 놓으면 된다
    }

    private static float Rubber(float v, float k, float max) => Mathf.Sign(v) * max * (1f - Mathf.Exp(-Mathf.Abs(v) * k / max));

    public void EndDrag(PointerEventData e)
    {
        if (e.pointerId != dragPointer) return;
        if (pulling)
        {
            pulling = false;
            if (Time.unscaledTime - lastT > 0.12f) flingV = 0f;
            bool toPin = !sky.Pinned;
            float way = toPin ? -pullRaw : pullRaw, fling = toPin ? -flingV : flingV;
            bool go = armed || fling > flipSpeed && way > 40f;   // 끝까지 안 끌어도 휙 튕기면
            armed = false;
            if (go) SetPin(toPin);
            return;
        }
        if (!dragging) return;
        dragging = false;
        if (Time.unscaledTime - lastT > 0.12f) flingV = 0f;   // 멈췄다 뗐으면 튕긴 게 아니다
        float moved = (pos - target) * pageSpacing;   // + 면 손가락이 왼쪽으로 = 다음 장
        int to = target;
        if (moved > flipDistance || -flingV > flipSpeed) to = target + 1;
        else if (moved < -flipDistance || flingV > flipSpeed) to = target - 1;
        to = Mathf.Clamp(to, 0, shown.Count - 1);
        if (to != target)
        {
            target = to;
            current = pages[shown[target]].kind;
            PlayerPrefs.SetInt(SwipedKey, 1);
            Haptic();
        }
    }

    public void Click(PointerEventData e)
    {
        if (dragMoved || e.dragging) return;
        var hit = e.pointerPressRaycast.gameObject;
        if (hit == null) return;
        if (pinButton != null && hit.transform.IsChildOf(pinButton)) { SetPin(!sky.Pinned); return; }
        if (shown.Count == 0) return;
        var p = pages[shown[Mathf.Clamp(target, 0, shown.Count - 1)]];
        if (p.kind == Kind.Live)
        {
            for (int i = 0; i < liveChips.Length && i < Handles.Length; i++)
                if (liveChips[i] != null && hit.transform.IsChildOf(liveChips[i]))
                {
                    Haptic();
                    Application.OpenURL("https://www.youtube.com/@" + Handles[i] + "/live");
                    return;
                }
            return;
        }
        if (p.rows == null) return;
        for (int r = 0; r < p.rows.Length; r++)
        {
            var row = p.rows[r];
            if (row?.root == null || !row.root.gameObject.activeSelf || !hit.transform.IsChildOf(row.root)) continue;
            Tap(p.kind, r, row);
            return;
        }
    }

    private void Tap(Kind k, int r, Row row)
    {
        row.flashUntil = Time.unscaledTime + FlashTime;
        Haptic();
        switch (k)
        {
            case Kind.Hunt:
                if (r < R0926HuntList.Nearby.Count) R0926HuntPush.OpenHunt(R0926HuntList.Nearby[r].id);   // 그 게임 상세 — 참가하기
                break;
            case Kind.Festival:
                if (r >= fests.Count) break;
                var f = fests[r];
                if (f.promo) R0926Nearby.AdClick(this, f.ad_id, "festival", R0926Nearby.PromoLink(f.link, f.title, f.lat, f.lon));
                else R0926Nearby.Open(R0926Nearby.FestivalLink(f.title));
                break;
            case Kind.Hot:
                if (r >= R0926Nearby.HotPlaces.Count) break;
                var h = R0926Nearby.HotPlaces[r];
                if (h.promo) { R0926Nearby.AdClick(this, h.ad_id, "hot", R0926Nearby.PromoLink(h.link, h.name, h.lat, h.lon)); break; }
                var t = FindTarget(h.id, h.name);
                var ind = t != null ? FindAnyObjectByType<OffScreenIndicator>() : null;
                if (ind == null || !ind.Focus(t))
                {
                    // 아직 AR 에 없다 (거리 설정 밖 · 불러오는 중) — 그 줄에 알려 준다
                    row.noteUntil = Time.unscaledTime + NoteTime;
                    nextCollect = 0f;
                    StartCoroutine(ClearNote(row));
                }
                break;
            case Kind.Friends:
                if (r < mates.Count) ShowProfile(mates[r].f.user_id);
                break;
        }
    }

    private IEnumerator ClearNote(Row row)
    {
        yield return new WaitForSecondsRealtime(NoteTime + 0.05f);
        nextCollect = 0f;
    }

    private void ShowProfile(string id)
    {
        var pm = ProfileManager.Instance;
        if (pm == null || string.IsNullOrEmpty(id) || Time.unscaledTime < profileWaitUntil) return;
        profileWaitUntil = Time.unscaledTime + 3f;
        pm.ShowProfile(id, ok =>
        {
            profileWaitUntil = 0f;
            if (!ok && ToastManager.Instance != null)
                ToastManager.Instance.ShowWarning(L("프로필을 불러오지 못했어요", "Couldn't load the profile", "プロフィールを読み込めませんでした", "无法加载个人资料", "No se pudo cargar el perfil"));
        });
    }

    // 우팡 장소의 AR 박스 — id 와 이름이 둘 다 맞는 것 먼저 (매니저끼리 숫자 id 가 겹칠 수 있다)
    private static Target FindTarget(string id, string name)
    {
        Target idOnly = null;
        foreach (var t in FindObjectsByType<Target>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t == null || t.placeId != id) continue;
            if (t.PlaceName == name) return t;
            if (idOnly == null) idOnly = t;
        }
        return idOnly;
    }

    private void SetPin(bool on)
    {
        sky.SetPinned(on);
        if (sky.Pinned != on) return;   // 판이 안 떠 있다
        Haptic();
        pinHintAt = -1f;
        if (!on || PlayerPrefs.GetInt(PinKey, 0) != 0) return;
        PlayerPrefs.SetInt(PinKey, 1);   // 처음 고정했을 때만 — 푸는 법
        if (ToastManager.Instance != null)
            ToastManager.Instance.ShowInfo(L("고정했어요 · 위로 밀면 하늘로 돌아가요", "Pinned · Swipe up to send it back to the sky", "固定しました · 上にスワイプすると空に戻ります",
                                             "已固定 · 向上滑动即可放回天空", "Fijado · Desliza hacia arriba para devolverlo al cielo"));
    }

    // 손가락 자리 → 판 단위. 틀의 부모 좌표로 재 틀 배율로 나눈다 — 끄는 동안 틀이 손가락을 따라 움직여도 기준이 흔들리지 않게.
    // 하늘에선 월드 캔버스라 카메라로, 고정하면 화면 UI(오버레이)라 카메라 없이 (예전엔 늘 카메라를 넘겼다)
    private bool Local(Vector2 screen, out Vector2 p)
    {
        p = Vector2.zero;
        var parent = board.parent as RectTransform;
        var canvas = board.GetComponentInParent<Canvas>();
        if (parent == null || canvas == null) return false;
        canvas = canvas.rootCanvas;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out var lp)) return false;
        p = lp / Mathf.Max(0.0001f, board.localScale.x);
        return true;
    }

    private static int Touches()
    {
        var ts = Touchscreen.current;
        if (ts == null) return 0;
        int n = 0;
        foreach (var t in ts.touches) if (t.isInProgress) n++;
        return n;
    }

    private static void Haptic()
    {
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerLightHaptic();
    }

    private static string L(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);

#if UNITY_EDITOR
    /// <summary>플레이 시험 — 지금 보는 장 · 넣은 장 수</summary>
    public Kind EditorCurrent => shown.Count > 0 ? pages[shown[Mathf.Clamp(target, 0, shown.Count - 1)]].kind : Kind.Weather;
    public int EditorPageCount => shown.Count;
    public void EditorGo(Kind k)
    {
        int i = shown.FindIndex(x => pages[x].kind == k);
        if (i >= 0) { target = i; current = k; }
    }
    public RectTransform EditorPinButton => pinButton;
    public RectTransform EditorFrame => board;
    public float EditorPull => pull;
    public bool EditorArmed => armed;
#endif
}
