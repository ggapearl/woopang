using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 목록 시트의 '지도' — 맞팔 친구의 대략적인 위치(약 1km)와 내 보물찾기 게임을 세계 지도에.
///  · 서버: /api/p2p/friends_map · /api/p2p/map_share (토큰 필수, 규칙은 서버가 지킨다)
///  · 보물찾기: 장소 목록과 같은 게임(R0926HuntList — 내가 열었거나 참가 중)을 게임의 공개 설정대로 —
///    정확히 공개면 그 자리, 대략 공개면 원(이 안 어딘가), 비공개는 자리가 없어 안 나온다. 누르면 그 게임 상세
///  · 지도: Natural Earth 세계 지도 한 장 + 동아시아 정밀 지도를 겹친 메르카토르 (외부 지도 서버 호출 없음)
///    + 깊이 확대하면 우팡 서버의 지도 타일(R0926MapTiles — 서버가 켜 둔 경우만)
///  · 한 손가락 이동 · 두 손가락 확대 · 두 번 탭 확대 · +/− · 내 위치 · 가까운 친구는 숫자로 묶음
///  · 보던 자리를 기억한다 — 다른 창에 다녀오거나 앱을 다시 켜도 그 자리 (처음 열 때·공유를 켜고 끌 때만 모두가 보이게 맞춘다)
///  · 꾹 누르면 그 자리에 핀 + 주소(국내) · 위도·경도(복사) · 돋보기에 좌표·주소·장소 이름을 넣으면 그 자리에 핀을 꽂고 동네가 보이게 확대
///    (주소·장소는 우팡 서버가 브이월드로 찾는다 — R0926Geo · 여럿이면 검색 줄 아래 목록에서 고른다)
/// 이 컴포넌트는 지도 영역(viewport)에 붙는다 — 끌기·휠 이벤트를 직접 받는다.
/// 지도 좌표는 double — 깊이 확대하면 지도 전체가 수억 단위라 float 로는 핀·타일이 어긋나고 끌기가 뚝뚝 끊긴다.
/// </summary>
public class R0926FriendsMap : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler,
    IPointerDownHandler, IPointerUpHandler
{
    // 지도 그림과 반드시 같은 값 (render_worldmap.py / render_detail.py)
    private const float LatTop = 78f, LatBottom = -58f;
    public const float DetailL = 118f, DetailR = 146f, DetailT = 46f, DetailB = 22f;

    /// <summary>서버 타일의 가장 깊은 단계보다 이만큼 더 확대 — 그 타일을 두 배로 키워 보여 준다 (조금 흐리지만 골목·건물 이름까지 읽힌다)</summary>
    public const float OverZoomLevels = 1f;

    [Header("지도")]
    [Tooltip("지도 그림은 Resources 에서 처음 열 때만 불러온다 — 안 쓰는 사람은 메모리 0")]
    [SerializeField] private RawImage worldImage;
    [SerializeField] private RawImage detailImage;
    [SerializeField] private string worldResource = "Maps0926/r0926_worldmap";
    [SerializeField] private string detailResource = "Maps0926/r0926_worldmap_asia";
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform pinLayer;
    [SerializeField] private GameObject pinTemplate;
    [SerializeField] private GameObject clusterTemplate;
    [Tooltip("깊이 확대했을 때 친구 핀 둘레의 '이 근처' 원 (친구 위치는 약 1km 칸으로 온다)")]
    [SerializeField] private GameObject areaTemplate;
    [SerializeField] private RectTransform meDot;
    [SerializeField] private Image mePulse;
    [SerializeField] private Text chipText;
    [SerializeField] private float mapAspect = 4096f / 2283f;
    [Tooltip("타일이 없을 때 최대 확대 (세계 지도 폭의 배수) — 앱에 든 그림만으론 이 이상은 뭉개진다")]
    [SerializeField] private float maxZoom = 200f;
    [Tooltip("깊은 확대용 서버 지도 타일 — 서버가 켜 두면 동네 수준까지")]
    [SerializeField] private R0926MapTiles tiles;
    [SerializeField] private float clusterRadius = 120f;

    [Header("보물찾기")]
    [Tooltip("게임 자리 — 금빛 테두리 원 안의 상자 + 게임 이름. 누르면 그 게임 상세")]
    [SerializeField] private GameObject huntPinTemplate;
    [Tooltip("대략 공개 게임의 '이 안 어딘가' 원")]
    [SerializeField] private GameObject huntAreaTemplate;

    [Header("좌표 — 꾹 눌러 핀 · 주소·장소·좌표 검색")]
    [Tooltip("꾹 누른 곳·검색한 곳에 꽂는 핀 (피벗이 핀 끝) — 자식 말풍선에 주소 · 좌표 · 복사 · 지우기")]
    [SerializeField] private RectTransform dropPin;
    [SerializeField] private RectTransform dropBubble;
    [SerializeField] private Text dropText;
    [Tooltip("말풍선 위 줄 — 장소 이름이나 그 자리 주소 (국내만 · 우팡 서버가 찾아 준다)")]
    [SerializeField] private Text dropName;
    [SerializeField] private GameObject searchButton;
    [SerializeField] private GameObject searchBar;
    [SerializeField] private InputField searchInput;
    [Tooltip("검색 줄 아래 결과 목록 — 이름 · 주소 줄 (누르면 그 자리로)")]
    [SerializeField] private GameObject searchResults;
    [SerializeField] private Button[] resultRows;
    [SerializeField] private float longPressSeconds = 0.5f;

    [Header("동의 · 안내 카드")]
    [SerializeField] private GameObject dim;
    [SerializeField] private GameObject consentCard;
    [SerializeField] private Text consentTitle;
    [SerializeField] private Text consentBody;
    [SerializeField] private GameObject consentSwitchRow;
    [SerializeField] private Image consentSwitch;
    [SerializeField] private Text consentNote;

    [Header("친구 목록")]
    [SerializeField] private RectTransform listContent;
    [SerializeField] private GameObject rowTemplate;
    [SerializeField] private Text listHint;
    [Tooltip("친구 목록 끝의 광고 자리 — 친구가 적으면 빈 칸을 채운다")]
    [SerializeField] private RectTransform adSlot;

    [Header("공유 줄")]
    [SerializeField] private Text shareText;
    [SerializeField] private Image shareSwitch;
    [SerializeField] private Sprite switchOn;
    [SerializeField] private Sprite switchOff;

    [SerializeField] private float refreshSeconds = 60f;

    [Serializable] private class Friend { public string user_id; public string username; public string avatar_url; public float lat; public float lon; public int ago; }
    [Serializable] private class Resp { public string status; public bool sharing; public int mutual; public Friend[] friends; }

    private struct D2
    {
        public double x, y;
        public D2(double x, double y) { this.x = x; this.y = y; }
    }

    private static readonly Dictionary<string, Texture2D> avatarCache = new Dictionary<string, Texture2D>();
    private static readonly Color[] Palette =
    {
        new Color(0.97f, 0.78f, 0.45f), new Color(0.56f, 0.77f, 1f), new Color(0.72f, 0.91f, 0.53f),
        new Color(1f, 0.62f, 0.71f), new Color(0.79f, 0.71f, 1f), new Color(0.49f, 0.86f, 0.84f),
    };

    private readonly List<Friend> friends = new List<Friend>();
    private readonly List<GameObject> pinPool = new List<GameObject>();
    private readonly List<GameObject> clusterPool = new List<GameObject>();
    private readonly List<GameObject> areaPool = new List<GameObject>();
    private readonly List<GameObject> huntPinPool = new List<GameObject>();
    private readonly List<GameObject> huntAreaPool = new List<GameObject>();
    private readonly List<GameObject> rows = new List<GameObject>();
    private RectTransform viewport;
    private Canvas canvas;
    private double zoom = 1;
    private D2 origin;               // 지도 왼쪽 위 모서리 — viewport 왼쪽 위 기준, 아래가 +
    private bool sharing, loggedIn, loaded, fitted, busy, mapShown, netError;
    private bool touched;            // 마지막으로 맞춘 뒤 손으로 움직였다 — 보물찾기 목록이 바뀌어도 다시 맞추지 않는다
    private int mutual;
    private string selectedId;
    private float nextRefresh, nextHuntCheck;
    private float prevPinch = -1f;
    private Vector2 lastSize;
    private Coroutine anim;
    private const string ViewKey = "R0926Map.View";   // 보던 자리 "가운데 x|가운데 y|배율" (정규 좌표)
    private bool restorePending;
    private bool hasDrop;
    private double dropLat, dropLon;
    private int dropSeq;                    // 핀을 옮길 때마다 — 늦게 온 옛 자리 주소는 버린다
    private R0926Geo.Place[] results;       // 검색 결과 목록에 보이는 것
    private Coroutine searchCo;
    private const int NoPress = int.MinValue;
    private int pressId = NoPress;   // 지도 바탕을 누르고 있는 손가락
    private Vector2 pressPos;
    private float pressAt;
    private bool pressFired;         // 꾹 눌러 핀을 꽂았다 — 그 손을 뗄 때의 탭은 확대로 치지 않는다

    // ── 시작 ─────────────────────────────────────────────────
    private void Awake()
    {
        viewport = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        if (pinTemplate != null) pinTemplate.SetActive(false);
        if (clusterTemplate != null) clusterTemplate.SetActive(false);
        if (areaTemplate != null) areaTemplate.SetActive(false);
        if (huntPinTemplate != null) huntPinTemplate.SetActive(false);
        if (huntAreaTemplate != null) huntAreaTemplate.SetActive(false);
        if (rowTemplate != null) rowTemplate.SetActive(false);
    }

    // 지도 패널이 열리고 닫힐 때 (탭 전환은 R0926SheetModes)
    private void OnEnable()
    {
        mapShown = true;
        EnsureTextures();
        nextRefresh = 0f;
        nextHuntCheck = 0f;
        fitted = false;
        restorePending = true;   // 보던 자리 — 지도 창 크기가 잡힌 뒤 (Update)
        R0926HuntList.Changed += OnHuntsChanged;
    }

    private void OnDisable()
    {
        SaveView();
        mapShown = false;
        pressId = NoPress;
        searchCo = null;   // 꺼지면 코루틴도 멈춘다 — 다시 켜서 찾을 수 있게
        R0926HuntList.Changed -= OnHuntsChanged;
    }

    // 앱이 뒤로 가면 그대로 꺼질 수 있다 — 그 전에 보던 자리를
    private void OnApplicationPause(bool paused)
    {
        if (paused && mapShown) SaveView();
    }

    private void EnsureTextures()
    {
        if (worldImage != null && worldImage.texture == null) worldImage.texture = Resources.Load<Texture2D>(worldResource);
        if (detailImage != null && detailImage.texture == null) detailImage.texture = Resources.Load<Texture2D>(detailResource);
    }

    private static void Kill(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
    }

    private void Update()
    {
        if (!mapShown) return;

        if (restorePending && viewport.rect.width > 1f)
        {
            restorePending = false;
            if (RestoreView()) fitted = true;
            else if (loaded) Refresh();   // 기억한 자리가 없다 — 처음처럼 모두가 보이게
        }

        if (Time.unscaledTime >= nextRefresh && !busy)
        {
            nextRefresh = Time.unscaledTime + refreshSeconds;
            StartCoroutine(Fetch());
        }
        // 보물찾기 목록 — 장소 목록과 같은 것을 같은 주기(1분)로. 이미 묻는 중이거나 때가 안 됐으면 아무것도 안 한다
        if (Time.unscaledTime >= nextHuntCheck)
        {
            nextHuntCheck = Time.unscaledTime + 5f;
            var co = R0926HuntList.RefreshIfDue();
            if (co != null) StartCoroutine(co);
        }

        Vector2 size = viewport.rect.size;
        if (size != lastSize) { lastSize = size; Clamp(); Layout(); }

        Pinch();
        if (pressId != NoPress && !pressFired && Time.unscaledTime - pressAt >= longPressSeconds)
        {
            pressFired = true;
            pressId = NoPress;
            DropPinAt(LocalTopLeft(pressPos));
        }
        if (mePulse != null)
        {
            float t = Mathf.Repeat(Time.unscaledTime, 1.6f) / 1.6f;
            mePulse.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 2.4f, t);
            var c = mePulse.color; c.a = Mathf.Lerp(0.35f, 0f, t); mePulse.color = c;
        }
    }

    // ── 서버 ─────────────────────────────────────────────────
    private IEnumerator Fetch()
    {
        loggedIn = LoginManager.Instance != null && LoginManager.Instance.CurrentUser != null && !string.IsNullOrEmpty(LoginManager.Instance.CurrentUser.id);
        if (!loggedIn) { loaded = true; netError = false; Refresh(); yield break; }

        busy = true;
        using (var req = UnityWebRequest.Get(ApiConfig.P2P_SERVER + "/api/p2p/friends_map"))
        {
            LoginManager.ApplyAuth(req);
            req.timeout = 10;
            yield return req.SendWebRequest();
            busy = false;
            if (req.result != UnityWebRequest.Result.Success)
            {
                if (!loaded) { loaded = true; netError = true; Refresh(); }
                yield break;
            }
            Resp r = null;
            try { r = JsonUtility.FromJson<Resp>(req.downloadHandler.text); } catch (Exception) { }
            if (r == null) yield break;
            sharing = r.sharing;
            mutual = r.mutual;
            friends.Clear();
            if (r.friends != null) friends.AddRange(r.friends);
            loaded = true;
            netError = false;
            Refresh();
        }
    }

    /// <summary>공유 스위치 (동의 카드·아래 줄 공통)</summary>
    public void ToggleShare()
    {
        if (!loggedIn || busy) return;
        StartCoroutine(SetShare(!sharing));
    }

    private IEnumerator SetShare(bool on)
    {
        busy = true;
        var req = new UnityWebRequest(ApiConfig.P2P_SERVER + "/api/p2p/map_share", "POST");
        LoginManager.ApplyAuth(req);
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(on ? "{\"enabled\":true}" : "{\"enabled\":false}"));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 10;
        yield return req.SendWebRequest();
        bool ok = req.result == UnityWebRequest.Result.Success;
        req.Dispose();
        busy = false;
        if (!ok)
        {
            if (ToastManager.Instance != null) ToastManager.Instance.ShowError(L("지금은 바꿀 수 없어요. 잠시 후 다시 해 주세요", "Couldn't change it now. Try again shortly",
                "今は変更できません。しばらくしてからお試しください", "暂时无法更改，请稍后再试", "No se pudo cambiar ahora. Inténtalo luego"));
            yield break;
        }
        sharing = on;
        fitted = false;
        nextRefresh = 0f;   // 바로 다시 불러 친구 위치 반영
        Refresh();
    }

    // 보물찾기 목록이 바뀌었다 (참가·나가기·새 게임·1분마다 다시 물음) — 아직 지도를 손대지 않았으면 게임까지 다시 맞춘다
    private void OnHuntsChanged()
    {
        if (!loaded) return;
        if (!touched) fitted = false;
        Refresh();
        Layout(force: true);   // 같은 게임의 이름이 바뀐 것도
    }

    // ── 화면 ─────────────────────────────────────────────────
    private void Refresh()
    {
        // 보물찾기 게임이 지도에 있으면 위치 공유를 꺼 두었어도 지도를 가리지 않는다 — 공유 스위치는 아래 줄에 그대로
        bool showConsent = !loggedIn || netError || (!sharing && !HasHuntMarkers);
        if (dim != null) dim.SetActive(showConsent);
        if (consentCard != null) consentCard.SetActive(showConsent);
        if (consentSwitchRow != null) consentSwitchRow.SetActive(loggedIn && !netError);
        if (consentSwitch != null) consentSwitch.sprite = sharing ? switchOn : switchOff;
        if (shareSwitch != null) shareSwitch.sprite = sharing ? switchOn : switchOff;

        if (consentTitle != null) consentTitle.text = L("친구 지도", "Friends map", "友だちマップ", "好友地图", "Mapa de amigos");
        if (consentBody != null)
        {
            consentBody.text = netError
                ? L("친구 위치를 불러오지 못했어요. 인터넷 연결을 확인해 주세요.", "Couldn't load friends' locations. Check your connection.",
                    "友だちの位置を読み込めませんでした。接続を確認してください。", "无法加载好友位置，请检查网络。", "No se pudieron cargar las ubicaciones. Revisa tu conexión.")
                : !loggedIn
                ? L("로그인하면 맞팔로우한 친구끼리 대략적인 위치를 서로 볼 수 있어요.", "Log in to see roughly where your mutual friends are.",
                    "ログインすると相互フォローの友だちとおおよその位置を見せ合えます。", "登录后，互相关注的好友可以互相查看大致位置。", "Inicia sesión para ver dónde están tus amigos mutuos.")
                : L("맞팔로우한 친구끼리 대략적인 위치(약 1km)를 서로 볼 수 있어요. 내 위치를 켜야 친구 위치도 보여요.",
                    "Mutual friends can see each other's rough location (about 1 km). Turn yours on to see theirs.",
                    "相互フォローの友だち同士で、おおよその位置（約1km）を見せ合えます。自分の位置をオンにすると友だちの位置も見えます。",
                    "互相关注的好友可以互相查看大致位置（约1公里）。开启你的位置后才能看到好友。",
                    "Los amigos mutuos pueden ver su ubicación aproximada (1 km). Activa la tuya para ver la de ellos.");
        }
        if (consentNote != null)
            consentNote.text = L("언제든 끌 수 있고, 끄면 지도에서 바로 사라져요.", "You can turn it off anytime — you disappear right away.",
                "いつでもオフにでき、オフにするとすぐ地図から消えます。", "可随时关闭，关闭后立即从地图消失。", "Puedes desactivarlo cuando quieras; desapareces al instante.");
        if (shareText != null)
            shareText.text = sharing
                ? L("내 위치 공유 중 · 약 1km 단위", "Sharing my location · about 1 km", "位置を共有中 · 約1km", "正在共享位置 · 约1公里", "Compartiendo ubicación · ~1 km")
                : L("내 위치 공유 꺼짐", "Location sharing off", "位置の共有オフ", "位置共享已关闭", "Ubicación no compartida");

        if (chipText != null)
        {
            ChipBox.SetActive(loggedIn && sharing && !Searching);   // 검색 줄이 같은 자리를 쓴다
            chipText.text = string.Format(L("맞팔 친구 {0}명 중 {1}명 공유 중", "{1} of {0} mutual friends sharing", "相互フォロー{0}人中 {1}人が共有中", "{0} 位互关好友中 {1} 位在共享", "{1} de {0} amigos mutuos comparten"), mutual, friends.Count);
        }

        BuildRows();
        if (!fitted && loaded && !restorePending) { FitAll(); fitted = true; }
        Clamp();
        Layout();
    }

    private void BuildRows()
    {
        foreach (var r in rows) Kill(r);
        rows.Clear();
        if (listHint != null)
        {
            bool empty = loggedIn && sharing && friends.Count == 0;
            listHint.gameObject.SetActive(empty || !loggedIn || !sharing);
            listHint.text = empty
                ? L("맞팔 친구가 위치를 켜면 여기에 나와요.", "Mutual friends who share appear here.", "位置を共有した相互フォローの友だちがここに表示されます。", "开启共享的互关好友会显示在这里。", "Aquí aparecen tus amigos mutuos que comparten.")
                : string.Format(L("맞팔 친구 {0}명", "{0} mutual friends", "相互フォロー {0}人", "互关好友 {0} 位", "{0} amigos mutuos"), mutual);
        }
        if (rowTemplate == null || listContent == null || !sharing) { if (adSlot != null) adSlot.SetAsLastSibling(); return; }

        Vector2 me = MyLatLon();
        var sorted = new List<Friend>(friends);
        sorted.Sort((a, b) => Km(me, a).CompareTo(Km(me, b)));
        foreach (var f in sorted)
        {
            var go = Instantiate(rowTemplate, listContent);
            go.name = "Friend_" + f.user_id;
            go.SetActive(true);
            FillAvatar(go.transform.Find("Avatar"), f);
            var name = go.transform.Find("Name")?.GetComponent<Text>();
            if (name != null) name.text = "@" + f.username;
            var sub = go.transform.Find("Sub")?.GetComponent<Text>();
            if (sub != null)
            {
                float km = Km(me, f);
                sub.text = Ago(f.ago) + (me != Vector2.zero ? " · " + (km < 1f ? "1km" : Mathf.RoundToInt(km).ToString("N0") + "km") : "");
            }
            var sel = go.transform.Find("Sel");
            if (sel != null) sel.gameObject.SetActive(f.user_id == selectedId);
            var btn = go.GetComponent<Button>();
            if (btn != null) { var ff = f; btn.onClick.AddListener(() => Focus(ff)); }
            rows.Add(go);
        }
        if (adSlot != null) adSlot.SetAsLastSibling();
    }

    private void FillAvatar(Transform avatar, Friend f)
    {
        // 구조: Avatar(묶음) > Face(원형 마스크 · 바탕색) > Photo · Initial, 그리고 Avatar > Live(초록 점 — 마스크 밖)
        if (avatar == null) return;
        var bg = avatar.Find("Face")?.GetComponent<Image>();
        if (bg != null) bg.color = Palette[((f.username ?? "").GetHashCode() & 0x7fffffff) % Palette.Length];
        var initial = avatar.Find("Face/Initial")?.GetComponent<Text>();
        if (initial != null) initial.text = string.IsNullOrEmpty(f.username) ? "?" : f.username.Substring(0, 1).ToUpperInvariant();
        var live = avatar.Find("Live");
        if (live != null) live.gameObject.SetActive(f.ago < 600);
        var group = avatar.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = f.ago >= 3 * 3600 ? 0.6f : 1f;
        var photo = avatar.Find("Face/Photo")?.GetComponent<RawImage>();
        if (photo != null)
        {
            photo.enabled = false;
            if (!string.IsNullOrEmpty(f.avatar_url))
            {
                if (avatarCache.TryGetValue(f.avatar_url, out var tex) && tex != null) { photo.texture = tex; photo.enabled = true; }
                else StartCoroutine(LoadAvatar(f.avatar_url));
            }
        }
    }

    private IEnumerator LoadAvatar(string url)
    {
        if (avatarCache.ContainsKey(url)) yield break;
        avatarCache[url] = null;   // 중복 요청 방지
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            avatarCache[url] = DownloadHandlerTexture.GetContent(req);
        }
        BuildRows();
        Layout(force: true);
    }

    // ── 투영 ─────────────────────────────────────────────────
    private static double MercY(double lat) => Math.Log(Math.Tan(Math.PI / 4 + Math.Max(-85.0, Math.Min(85.0, lat)) * (Math.PI / 180) / 2));
    private static readonly double YTop = MercY(LatTop), YBot = MercY(LatBottom);
    private static readonly D2 DetailMin = NormD(DetailT, DetailL), DetailMax = NormD(DetailB, DetailR);

    /// <summary>웹 메르카토르 세로 좌표(0 = 북위 85.05°, 1 = 남위 85.05° — 지도 타일 줄) → 이 지도의 정규 세로 좌표 (둘은 1차식 관계)</summary>
    public static double WebToNormY(double webY) => (YTop - Math.PI + 2 * Math.PI * webY) / (YTop - YBot);

    private bool Tiled => tiles != null && tiles.Available;
    /// <summary>최대 확대 — 타일이 있으면 서버가 주는 가장 깊은 단계 + OverZoomLevels, 없으면 maxZoom</summary>
    private double MaxZoom => Tiled ? Math.Max(maxZoom, tiles.ZoomForLevel(tiles.MaxLevel + OverZoomLevels, viewport.rect.width, Scale)) : maxZoom;
    /// <summary>친구·내 위치를 눌렀을 때 확대 — 타일이 있으면 시내 수준(13 단계 · 화면 폭 약 15km)</summary>
    private double FocusZoom => Tiled ? Math.Min(MaxZoom, tiles.ZoomForLevel(13, viewport.rect.width, Scale)) : 16;

    /// <summary>위경도 → 지도 정규 좌표 (0,0 = 왼쪽 위, 1,1 = 오른쪽 아래)</summary>
    public static Vector2 Norm(float lat, float lon) { var n = NormD(lat, lon); return new Vector2((float)n.x, (float)n.y); }
    private static D2 NormD(double lat, double lon) => new D2((lon + 180) / 360, (YTop - MercY(lat)) / (YTop - YBot));

    private D2 MapSize => new D2(viewport.rect.width * zoom, viewport.rect.width * zoom / mapAspect);
    private Vector2 ToView(D2 n) { D2 m = MapSize; return new Vector2((float)(origin.x + n.x * m.x), (float)(origin.y + n.y * m.y)); }
    private D2 ToNorm(Vector2 p) { D2 m = MapSize; return new D2((p.x - origin.x) / m.x, (p.y - origin.y) / m.y); }
    private static double Clip(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

    private void Clamp()
    {
        Vector2 v = viewport.rect.size;
        D2 m = MapSize;
        origin.x = m.x >= v.x ? Clip(origin.x, v.x - m.x, 0) : (v.x - m.x) / 2;
        origin.y = m.y >= v.y ? Clip(origin.y, v.y - m.y, 0) : (v.y - m.y) / 2;
        if (content != null)
        {
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.sizeDelta = v;
            content.anchoredPosition = Vector2.zero;
        }
        PlaceImage(worldImage, new D2(0, 0), new D2(1, 1));
        PlaceImage(detailImage, DetailMin, DetailMax);
    }

    // 그림 한 장(정규 좌표 a~b 를 덮는다) — 화면에 보이는 부분만 잘라서. 통째로 키우면 깊은 확대에서 수억 단위가 되어 float 가 못 담는다
    private void PlaceImage(RawImage img, D2 a, D2 b)
    {
        if (img == null) return;
        Vector2 v = viewport.rect.size;
        D2 m = MapSize;
        double l = origin.x + a.x * m.x, r = origin.x + b.x * m.x, t = origin.y + a.y * m.y, btm = origin.y + b.y * m.y;
        double cl = Math.Max(l, -2), cr = Math.Min(r, v.x + 2), ct = Math.Max(t, -2), cb = Math.Min(btm, v.y + 2);
        bool on = cr > cl && cb > ct;
        if (img.enabled != on) img.enabled = on;
        if (!on) return;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2((float)cl, (float)-ct);
        rt.sizeDelta = new Vector2((float)(cr - cl), (float)(cb - ct));
        double w = r - l, h = btm - t;
        img.uvRect = new Rect((float)((cl - l) / w), (float)(1 - (cb - t) / h), (float)((cr - cl) / w), (float)((cb - ct) / h));   // 그림의 v 는 아래에서 위로
    }

    private void ZoomAt(Vector2 viewPoint, double factor)
    {
        D2 n = ToNorm(viewPoint);
        zoom = Clip(zoom * factor, 1, MaxZoom);
        D2 m = MapSize;
        origin = new D2(viewPoint.x - n.x * m.x, viewPoint.y - n.y * m.y);
        Clamp();
        Layout();
    }

    private void CenterOn(D2 n)
    {
        Vector2 c = viewport.rect.size / 2f;
        D2 m = MapSize;
        origin = new D2(c.x - n.x * m.x, c.y - n.y * m.y);
        Clamp();
    }

    private void FitAll()
    {
        touched = false;
        Vector2 me = MyLatLon();
        var pts = new List<D2>();
        if (me != Vector2.zero) pts.Add(NormD(me.x, me.y));
        if (sharing) foreach (var f in friends) pts.Add(NormD(f.lat, f.lon));
        foreach (var h in HuntSpots) if (h.coord != R0926HuntList.Coord.Hidden) pts.Add(NormD(h.lat, h.lon));
        if (pts.Count == 0) { zoom = 1; CenterOn(new D2(0.5, 0.45)); return; }

        D2 min = pts[0], max = pts[0];
        foreach (var p in pts)
        {
            min = new D2(Math.Min(min.x, p.x), Math.Min(min.y, p.y));
            max = new D2(Math.Max(max.x, p.x), Math.Max(max.y, p.y));
        }
        Vector2 v = viewport.rect.size;
        double minBox = Tiled ? 0.00005 : 0.004;   // 친구들이 한 동네에 모여 있어도 지나치게 확대하지 않게 (타일 있으면 약 2km)
        double bw = Math.Max(max.x - min.x, minBox), bh = Math.Max(max.y - min.y, minBox);
        double z = Math.Min(1 / (bw * 1.4), v.y * mapAspect / (bh * 1.4 * v.x));
        zoom = Clip(z, 1, pts.Count == 1 ? Math.Max(14, FocusZoom * 0.1) : MaxZoom * 0.5);
        CenterOn(new D2((min.x + max.x) / 2, (min.y + max.y) / 2));
    }

    // ── 핀 ───────────────────────────────────────────────────
    private void Layout(bool force = false)
    {
        D2 m = MapSize;
        if (tiles != null) tiles.UpdateView(origin.x, origin.y, m.x, m.y, viewport.rect.size, Scale);
        if (pinLayer == null) return;
        Vector2 me = MyLatLon();
        if (meDot != null)
        {
            meDot.gameObject.SetActive(me != Vector2.zero);
            if (me != Vector2.zero) Place(meDot, ToView(NormD(me.x, me.y)));
        }

        Vector2 v = viewport.rect.size;
        int p = 0, c = 0, a = 0;
        if (sharing && loggedIn)
        {
            // 화면 거리 기준으로 가까운 친구를 묶는다 (욕심쟁이 방식 — 친구 수가 적어 충분)
            var groups = new List<(Vector2 pos, List<Friend> members)>();
            foreach (var f in friends)
            {
                Vector2 q = ToView(NormD(f.lat, f.lon));
                int hit = -1;
                for (int i = 0; i < groups.Count; i++)
                    if ((groups[i].pos - q).sqrMagnitude < clusterRadius * clusterRadius) { hit = i; break; }
                if (hit < 0) groups.Add((q, new List<Friend> { f }));
                else
                {
                    var g = groups[hit];
                    g.members.Add(f);
                    groups[hit] = ((g.pos * (g.members.Count - 1) + q) / g.members.Count, g.members);
                }
            }
            foreach (var g in groups)
            {
                float ar = AreaRadius(g.members[0].lat);
                float edge = Mathf.Max(120f, ar);
                if (g.pos.x < -edge || g.pos.y < -edge || g.pos.x > v.x + edge || g.pos.y > v.y + edge) continue;
                if (ar > 0f)
                {
                    var area = Pool(areaPool, areaTemplate, a++, behind: true);
                    ((RectTransform)area.transform).sizeDelta = new Vector2(ar * 2f, ar * 2f);
                    Place((RectTransform)area.transform, g.pos);
                }
                if (g.members.Count == 1)
                {
                    var go = Pool(pinPool, pinTemplate, p++);
                    var f = g.members[0];
                    if (force || go.name != "Pin_" + f.user_id)
                    {
                        go.name = "Pin_" + f.user_id;
                        FillAvatar(go.transform.Find("Avatar"), f);
                        var nm = go.transform.Find("Name/Text")?.GetComponent<Text>();
                        if (nm != null) nm.text = f.username;
                        var btn = go.GetComponent<Button>();
                        if (btn != null) { btn.onClick.RemoveAllListeners(); var ff = f; btn.onClick.AddListener(() => Focus(ff)); }
                    }
                    Place((RectTransform)go.transform, g.pos);
                }
                else
                {
                    var go = Pool(clusterPool, clusterTemplate, c++);
                    var t = go.GetComponentInChildren<Text>();
                    if (t != null) t.text = g.members.Count.ToString();
                    var btn = go.GetComponent<Button>();
                    if (btn != null) { btn.onClick.RemoveAllListeners(); var at = g.pos; btn.onClick.AddListener(() => Animate(at, 3f)); }
                    Place((RectTransform)go.transform, g.pos);
                }
            }
        }
        int hp = 0, ha = 0;
        LayoutHunts(v, force, ref hp, ref ha);
        PlaceDrop(v);
        for (int i = p; i < pinPool.Count; i++) if (pinPool[i].activeSelf) pinPool[i].SetActive(false);
        for (int i = c; i < clusterPool.Count; i++) if (clusterPool[i].activeSelf) clusterPool[i].SetActive(false);
        for (int i = a; i < areaPool.Count; i++) if (areaPool[i].activeSelf) areaPool[i].SetActive(false);
        for (int i = hp; i < huntPinPool.Count; i++) if (huntPinPool[i].activeSelf) huntPinPool[i].SetActive(false);
        for (int i = ha; i < huntAreaPool.Count; i++) if (huntAreaPool[i].activeSelf) huntAreaPool[i].SetActive(false);
    }

    // 보물찾기 — 정확히 공개(내가 연 게임 포함)면 그 자리, 대략 공개면 원 가운데 + 원. 친구와 묶지 않는다
    private void LayoutHunts(Vector2 v, bool force, ref int hp, ref int ha)
    {
        if (huntPinTemplate == null) return;
        foreach (var h in HuntSpots)
        {
            if (h.coord == R0926HuntList.Coord.Hidden) continue;
            Vector2 q = ToView(NormD(h.lat, h.lon));
            float r = h.coord == R0926HuntList.Coord.Approx ? MetersToView(h.approxM, h.lat) : 0f;
            float edge = Mathf.Max(160f, r);
            if (q.x < -edge || q.y < -edge || q.x > v.x + edge || q.y > v.y + edge) continue;
            if (r >= 64f && huntAreaTemplate != null)   // 상자보다 커 보일 때부터
            {
                var area = Pool(huntAreaPool, huntAreaTemplate, ha++, behind: true);
                ((RectTransform)area.transform).sizeDelta = new Vector2(r * 2f, r * 2f);
                Place((RectTransform)area.transform, q);
            }
            var go = Pool(huntPinPool, huntPinTemplate, hp++);
            if (force || go.name != "Hunt_" + h.id)
            {
                go.name = "Hunt_" + h.id;
                var nm = go.transform.Find("Name/Text")?.GetComponent<Text>();
                if (nm != null) nm.text = h.title.Length <= 12 ? h.title : h.title.Substring(0, 11) + "…";
                var btn = go.GetComponent<Button>();
                if (btn != null) { btn.onClick.RemoveAllListeners(); int id = h.id; btn.onClick.AddListener(() => R0926HuntPush.OpenHunt(id)); }
            }
            Place((RectTransform)go.transform, q);
        }
    }

    private bool HasHuntMarkers
    {
        get
        {
            foreach (var h in HuntSpots) if (h.coord != R0926HuntList.Coord.Hidden) return true;
            return false;
        }
    }

    // 친구 위치는 서버가 약 1km 칸(소수 둘째 자리)으로 뭉개 보낸다 — 칸 가운데에서 모서리까지 약 0.7km.
    // 그 칸이 아바타보다 커 보일 만큼 확대하면 핀이 정확한 건물처럼 보이지 않게 '이 근처' 원을 같이 그린다.
    private const float ApproxKm = 0.7f;

    private float AreaRadius(float lat)
    {
        if (areaTemplate == null) return 0f;
        float r = MetersToView(ApproxKm * 1000f, lat);
        return r >= 64f ? r : 0f;
    }

    // 그 위도에서 몇 m 가 지금 지도에서 몇 단위인지 (메르카토르 — 위도가 높을수록 같은 거리가 크게)
    private float MetersToView(float meters, double lat) =>
        (float)(meters / 1000.0 * MapSize.x / (40075.0 * Math.Max(0.05, Math.Cos(lat * Math.PI / 180))));

    private GameObject Pool(List<GameObject> pool, GameObject template, int index, bool behind = false)
    {
        while (pool.Count <= index)
        {
            var go = Instantiate(template, pinLayer);
            go.name = "_";
            if (behind) go.transform.SetAsFirstSibling();   // 핀·묶음보다 뒤
            pool.Add(go);
        }
        if (!pool[index].activeSelf) pool[index].SetActive(true);
        return pool[index];
    }

    private static void Place(RectTransform rt, Vector2 viewPos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(viewPos.x, -viewPos.y);
    }

    // ── 이동 ─────────────────────────────────────────────────
    private void Focus(Friend f)
    {
        selectedId = f.user_id;
        foreach (var r in rows)
        {
            var sel = r != null ? r.transform.Find("Sel") : null;
            if (sel != null) sel.gameObject.SetActive(r.name == "Friend_" + f.user_id);
        }
        StartAnim(NormD(f.lat, f.lon), Math.Max(zoom, FocusZoom));
    }

    public void ZoomIn() => Animate(viewport.rect.size / 2f, 2f);
    public void ZoomOut() => Animate(viewport.rect.size / 2f, 0.5f);

    public void LocateMe()
    {
        Vector2 me = MyLatLon();
        if (me == Vector2.zero) return;
        StartAnim(NormD(me.x, me.y), Math.Max(zoom, FocusZoom));
    }

    private void Animate(Vector2 viewPoint, float factor)
    {
        StartAnim(ToNorm(viewPoint), Clip(zoom * factor, 1, MaxZoom), keepPoint: factor != 3f ? viewPoint : (Vector2?)null);
    }

    private void StartAnim(D2 n, double targetZoom, Vector2? keepPoint = null)
    {
        touched = true;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(AnimRoutine(n, targetZoom, keepPoint));
    }

    private IEnumerator AnimRoutine(D2 n, double targetZoom, Vector2? keepPoint)
    {
        double z0 = zoom;
        float t = 0f;
        Vector2 center = viewport.rect.size / 2f;
        D2 from = ToNorm(center);   // 지금 화면 가운데의 지도 좌표
        // 멀리 가면 잠깐 물러났다가 다가간다 — 깊이 확대한 채 수백 km 를 미끄러지면 빈 타일만 지나간다
        D2 m0 = MapSize;
        double far = Math.Sqrt(Math.Pow((n.x - from.x) * m0.x, 2) + Math.Pow((n.y - from.y) * m0.y, 2)) / Math.Max(1f, viewport.rect.width);
        double dip = keepPoint.HasValue || far < 2 ? 0 : Math.Log(far / 2);
        float dur = 0.35f + (float)Math.Min(0.45, dip * 0.08);
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / dur);
            double e = 1 - Math.Pow(1 - t, 3);
            zoom = Math.Max(1, Math.Exp(Math.Log(z0) + (Math.Log(targetZoom) - Math.Log(z0)) * e - dip * Math.Sin(Math.PI * e)));
            D2 m = MapSize;
            if (keepPoint.HasValue)
                origin = new D2(keepPoint.Value.x - n.x * m.x, keepPoint.Value.y - n.y * m.y);   // 누른 곳을 붙잡고 확대
            else
                origin = new D2(center.x - (from.x + (n.x - from.x) * e) * m.x, center.y - (from.y + (n.y - from.y) * e) * m.y);
            Clamp();
            Layout();
            yield return null;
        }
        anim = null;
    }

    private Vector2 LocalTopLeft(Vector2 screen)
    {
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screen, cam, out var local);
        Rect r = viewport.rect;
        return new Vector2(local.x - r.xMin, r.yMax - local.y);
    }

    private float Scale => canvas != null ? canvas.scaleFactor : 1f;

    public void OnBeginDrag(PointerEventData e) { if (anim != null) { StopCoroutine(anim); anim = null; } touched = true; pressId = NoPress; }

    public void OnDrag(PointerEventData e)
    {
        if (Touches(out _, out _) >= 2) return;   // 두 손가락이면 확대·축소 쪽
        origin = new D2(origin.x + e.delta.x / Scale, origin.y - e.delta.y / Scale);
        Clamp();
        Layout();
    }

    public void OnEndDrag(PointerEventData e) { }

    public void OnScroll(PointerEventData e)
    {
        touched = true;
        ZoomAt(LocalTopLeft(e.position), Mathf.Pow(1.15f, e.scrollDelta.y));
    }

    // 지도 바탕을 누른 것만 꾹 누르기로 센다 — 핀·버튼·동의 카드·검색 줄·말풍선은 제 일을 한다
    public void OnPointerDown(PointerEventData e)
    {
        pressFired = false;
        pressId = e.pointerCurrentRaycast.gameObject == gameObject && Touches(out _, out _) < 2 ? e.pointerId : NoPress;
        pressPos = e.position;
        pressAt = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData e) { if (e.pointerId == pressId) pressId = NoPress; }

    public void OnPointerClick(PointerEventData e)
    {
        if (pressFired) { pressFired = false; return; }
        if (e.clickCount == 2) Animate(LocalTopLeft(e.position), 2f);
    }

    private void Pinch()
    {
        // 새 입력 시스템만 켜져 있다 — 예전 Input.touchCount 는 기기에서 예외가 나 끌기·확대가 통째로 안 됐다
        if (Touches(out Vector2 a, out Vector2 b) != 2) { prevPinch = -1f; return; }
        pressId = NoPress;   // 두 손가락 — 꾹 누르기 아님
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (prevPinch < 0f && !RectTransformUtility.RectangleContainsScreenPoint(viewport, (a + b) / 2f, cam)) return;
        float d = Vector2.Distance(a, b);
        Vector2 mid = LocalTopLeft((a + b) / 2f);
        if (prevPinch > 0f && d > 1f)
        {
            if (anim != null) { StopCoroutine(anim); anim = null; }
            touched = true;
            ZoomAt(mid, d / prevPinch);
        }
        prevPinch = d;
    }

    private static int Touches(out Vector2 a, out Vector2 b)
    {
        a = b = default;
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts == null) return 0;
        int n = 0;
        foreach (var t in ts.touches)
        {
            if (!t.isInProgress) continue;
            if (n == 0) a = t.position.ReadValue(); else if (n == 1) b = t.position.ReadValue();
            n++;
        }
        return n;
    }

    // ── 좌표 ─────────────────────────────────────────────────
    private bool Searching => searchBar != null && searchBar.activeSelf;
    private GameObject ChipBox => chipText == null ? null : chipText.transform.parent != null ? chipText.transform.parent.gameObject : chipText.gameObject;

    private void DropPinAt(Vector2 viewPoint)
    {
        D2 n = ToNorm(viewPoint);
        if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1) return;   // 세계 지도 위아래 빈 곳
        LatLon(n, out double lat, out double lon);
        SetDrop(lat, lon);
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerMediumHaptic();
    }

    // name — 검색한 장소 이름. 없으면(꾹 누른 자리) 그 자리 주소를 우팡 서버에 물어 위 줄에 (국내만)
    private void SetDrop(double lat, double lon, string name = null)
    {
        if (dropPin == null) return;
        hasDrop = true;
        dropLat = lat;
        dropLon = lon;
        int seq = ++dropSeq;
        if (dropText != null) dropText.text = CoordText(lat, lon);
        SetDropName(name);
        dropPin.gameObject.SetActive(true);
        if (dropBubble != null) LayoutRebuilder.ForceRebuildLayoutImmediate(dropBubble);
        Layout();
        if (name == null && dropName != null && R0926Geo.InKorea(lat, lon) && isActiveAndEnabled) StartCoroutine(DropAddress(lat, lon, seq));
    }

    private IEnumerator DropAddress(double lat, double lon, int seq)
    {
        R0926Geo.Address a = null;
        yield return R0926Geo.FindAddress(lat, lon, r => a = r);
        if (a == null || seq != dropSeq || !hasDrop) yield break;
        SetDropName(!string.IsNullOrEmpty(a.brief) ? a.brief : a.line);
        if (dropBubble != null) LayoutRebuilder.ForceRebuildLayoutImmediate(dropBubble);
        if (viewport != null) PlaceDrop(viewport.rect.size);   // 말풍선이 넓어졌다 — 지도 안으로 다시 민다
    }

    private void SetDropName(string name)
    {
        if (dropName == null) return;
        bool on = !string.IsNullOrEmpty(name);
        dropName.text = on ? Ellipsis(name, 24) : "";
        if (dropName.gameObject.activeSelf != on) dropName.gameObject.SetActive(on);
    }

    // 말풍선·목록 한 줄에 들어가게 — 넘치면 말줄임
    private static string Ellipsis(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1).TrimEnd() + "…";

    // 꽂은 핀 — 지도와 같이 움직이고 다른 핀 위. 말풍선은 핀 위(지도 위쪽 끝이면 아래), 좌우로는 지도 안에 들게 민다
    private void PlaceDrop(Vector2 v)
    {
        if (dropPin == null) return;
        Vector2 q = hasDrop ? ToView(NormD(dropLat, dropLon)) : Vector2.zero;
        bool on = hasDrop && q.x > -80f && q.x < v.x + 80f && q.y > 0f && q.y < v.y + 220f;
        if (dropPin.gameObject.activeSelf != on) dropPin.gameObject.SetActive(on);
        if (!on) return;
        if (dropPin.GetSiblingIndex() != dropPin.parent.childCount - 1) dropPin.SetAsLastSibling();
        Place(dropPin, q);
        if (dropBubble == null) return;
        Vector2 b = dropBubble.rect.size;
        bool below = q.y - dropPin.rect.height - 12f - b.y < 12f;
        float lo = 16f + b.x / 2f - q.x, hi = v.x - 16f - b.x / 2f - q.x;
        dropBubble.anchorMin = dropBubble.anchorMax = new Vector2(0.5f, below ? 0f : 1f);
        dropBubble.pivot = new Vector2(0.5f, below ? 1f : 0f);
        dropBubble.anchoredPosition = new Vector2(lo > hi ? 0f : Mathf.Clamp(0f, lo, hi), below ? -14f : 12f);
    }

    public void ClearDropPin()
    {
        hasDrop = false;
        dropSeq++;
        SetDropName(null);
        if (dropPin != null) dropPin.gameObject.SetActive(false);
    }

    /// <summary>꽂은 핀의 좌표를 복사 — "37.566500, 126.978000" (구글 지도 검색칸에 그대로 들어간다)</summary>
    public void CopyDropPin()
    {
        if (!hasDrop) return;
        GUIUtility.systemCopyBuffer = CoordText(dropLat, dropLon);
        if (ToastManager.Instance != null) ToastManager.Instance.ShowSuccess(L("좌표를 복사했어요", "Coordinates copied", "座標をコピーしました", "已复制坐标", "Coordenadas copiadas"));
    }

    public void OpenSearch()
    {
        if (searchBar == null) return;
        searchBar.SetActive(true);
        if (searchButton != null) searchButton.SetActive(false);
        if (ChipBox != null) ChipBox.SetActive(false);
        HideResults();
        if (searchInput == null) return;
        searchInput.text = "";
        searchInput.Select();
        searchInput.ActivateInputField();
    }

    public void CloseSearch()
    {
        StopSearch();
        HideResults();
        if (searchInput != null) searchInput.DeactivateInputField();
        if (searchBar != null) searchBar.SetActive(false);
        if (searchButton != null) searchButton.SetActive(true);
        if (ChipBox != null) ChipBox.SetActive(loggedIn && sharing);
    }

    /// <summary>칸의 글을 고치면 지난 결과 목록은 거둔다</summary>
    public void SearchTyped(string _)
    {
        StopSearch();
        HideResults();
    }

    /// <summary>복사해 둔 좌표(카톡으로 받은 좌표·구글 지도 주소 등)를 칸에 넣고 바로 찾는다</summary>
    public void PasteSearch()
    {
        string s = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(s))
        {
            if (ToastManager.Instance != null) ToastManager.Instance.ShowInfo(L("복사해 둔 좌표가 없어요", "Nothing copied yet", "コピーした座標がありません", "没有已复制的坐标", "No hay nada copiado"));
            return;
        }
        s = s.Trim();
        if (s.Length > 200) s = s.Substring(0, 200);
        if (searchInput != null) searchInput.text = s;
        SearchCoord(s);
    }

    /// <summary>
    /// 검색 줄에서 찾기 — 좌표면 바로 그 자리, 아니면 주소·장소 이름으로 우팡 서버에 묻는다 (국내 · 한국어 주소).
    /// 한 곳이면 바로 그 자리로, 여럿이면 검색 줄 아래 목록에서 고른다. 그 자리에 핀을 꽂고 동네가 보이게 확대
    /// </summary>
    public void SearchCoord(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return;
        s = s.Trim();
        if (!TryParseCoord(s, out double lat, out double lon))
        {
            SearchPlace(s);
            return;
        }
        if (lat > LatTop || lat < LatBottom)
        {
            if (ToastManager.Instance != null) ToastManager.Instance.ShowError(L("지도 밖의 좌표예요 (북위 78° ~ 남위 58°까지)", "That's off the map (78°N to 58°S)",
                "地図の範囲外です（北緯78°〜南緯58°）", "超出地图范围（北纬78°至南纬58°）", "Fuera del mapa (78°N a 58°S)"));
            return;
        }
        CloseSearch();
        SetDrop(lat, lon);
        FlyTo(lat, lon, SearchLevel);
    }

    private const double SearchLevel = 16;   // 화면 폭 약 2km — 그 자리와 둘레 길이 함께 보인다

    private void FlyTo(double lat, double lon, double level)
    {
        double z = Tiled ? tiles.ZoomForLevel(level, viewport.rect.width, Scale) : 64 * Math.Pow(2, level - SearchLevel);
        StartAnim(NormD(lat, lon), Clip(z, 1, MaxZoom));
    }

    private void SearchPlace(string q)
    {
        if (q.Length < 2)
        {
            if (ToastManager.Instance != null) ToastManager.Instance.ShowInfo(L("두 글자 이상 넣어 주세요", "Type at least 2 characters", "2文字以上入力してください", "请至少输入2个字", "Escribe al menos 2 caracteres"));
            return;
        }
        StopSearch();
        ShowResults(null);   // '찾는 중…' 한 줄
        searchCo = StartCoroutine(SearchPlaceCo(q));
    }

    private IEnumerator SearchPlaceCo(string q)
    {
        R0926Geo.Place[] got = null;
        bool answered = false;
        yield return R0926Geo.Search(q, SearchNear(), (r, ok) => { got = r; answered = ok; });
        searchCo = null;
        if (!Searching) yield break;   // 그 사이 검색 줄을 닫았다
        if (!answered || got.Length == 0)
        {
            HideResults();
            if (ToastManager.Instance == null) yield break;
            if (!answered)
                ToastManager.Instance.ShowError(L("지금은 주소를 찾을 수 없어요 · 좌표로 찾아 보세요", "Address search is unavailable right now · try coordinates",
                    "今は住所を検索できません · 座標でお試しください", "暂时无法搜索地址 · 请用坐标查找", "La búsqueda no está disponible · prueba con coordenadas"));
            else
                ToastManager.Instance.ShowInfo(L("찾지 못했어요 · 주소나 장소 이름을 바꿔 보세요 (국내만)", "No match · try another address or place (Korea only)",
                    "見つかりません · 住所や場所名を変えてみてください（韓国のみ）", "未找到 · 请换个地址或地名（仅限韩国）", "Sin resultados · prueba otra dirección o lugar (solo Corea)"));
            yield break;
        }
        if (got.Length == 1) GoToPlace(got[0]);
        else ShowResults(got);
    }

    private void StopSearch()
    {
        if (searchCo != null) StopCoroutine(searchCo);
        searchCo = null;
    }

    // 장소 이름은 지금 보는 곳 둘레부터 — 동네가 보일 만큼(화면 폭 경도 2° 안) 확대해 있으면 화면 가운데, 아니면 내 위치
    private Vector2? SearchNear()
    {
        if (viewport != null && viewport.rect.width > 1f && zoom >= 180)
        {
            LatLon(ToNorm(viewport.rect.size / 2f), out double la, out double lo);
            return new Vector2((float)la, (float)lo);
        }
        Vector2 me = MyLatLon();
        return me == Vector2.zero ? (Vector2?)null : me;
    }

    private void GoToPlace(R0926Geo.Place p)
    {
        CloseSearch();
        SetDrop(p.lat, p.lon, PinName(p));
        FlyTo(p.lat, p.lon, PlaceLevel(p));
    }

    // 핀 위 이름 — 주소·동네는 시·도를 뺀다 ('서울특별시 중구 세종대로 110' → '중구 세종대로 110'). 목록에는 다 보인다
    private static string PinName(R0926Geo.Place p)
    {
        string t = (p.title ?? "").Trim();
        if (p.kind == "place") return t;
        int sp = t.IndexOf(' ');
        if (sp < 0 || t.IndexOf(' ', sp + 1) < 0) return t;   // 두 단계까지는 그대로 ('서울특별시 종로구')
        string head = t.Substring(0, sp);
        if (head.StartsWith("세종")) return "세종시" + t.Substring(sp);
        return head.EndsWith("시") || head.EndsWith("도") ? t.Substring(sp + 1) : t;
    }

    // 동네 이름이면 그 동네가 다 보이게 — 도 9 · 시·군·구 12 · 읍·면·동 14 · 장소·주소 16
    private static double PlaceLevel(R0926Geo.Place p)
    {
        if (p.kind != "district" || string.IsNullOrEmpty(p.title)) return SearchLevel;
        string t = p.title.Trim();
        if (t.IndexOf(' ') < 0 && (t.EndsWith("도") || t.EndsWith("특별시") || t.EndsWith("광역시"))) return 9;
        return t.EndsWith("시") || t.EndsWith("군") || t.EndsWith("구") ? 12 : 14;
    }

    // items 가 null 이면 '찾는 중…' 한 줄
    private void ShowResults(R0926Geo.Place[] items)
    {
        results = items;
        if (searchResults == null || resultRows == null) return;
        int n = items == null ? 1 : Math.Min(items.Length, resultRows.Length);
        for (int i = 0; i < resultRows.Length; i++)
        {
            var row = resultRows[i];
            if (row == null) continue;
            bool on = i < n;
            if (row.gameObject.activeSelf != on) row.gameObject.SetActive(on);
            if (!on) continue;
            row.interactable = items != null;
            var title = row.transform.Find("Title")?.GetComponent<Text>();
            var addr = row.transform.Find("Address")?.GetComponent<Text>();
            string a = items == null ? "" : items[i].address ?? "";
            if (title != null) title.text = items == null ? L("찾는 중…", "Searching…", "検索中…", "搜索中…", "Buscando…") : Ellipsis(items[i].title ?? "", 26);
            if (addr != null)
            {
                addr.text = Ellipsis(a, 34);
                if (addr.gameObject.activeSelf != (a.Length > 0)) addr.gameObject.SetActive(a.Length > 0);
            }
        }
        searchResults.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)searchResults.transform);
    }

    private void HideResults()
    {
        results = null;
        if (searchResults != null && searchResults.activeSelf) searchResults.SetActive(false);
    }

    /// <summary>검색 결과 줄을 누름 — 그 자리에 핀을 꽂고 이동</summary>
    public void PickResult(int i)
    {
        if (results == null || i < 0 || i >= results.Length) return;
        GoToPlace(results[i]);
    }

    private static string CoordText(double lat, double lon) =>
        lat.ToString("F6", CultureInfo.InvariantCulture) + ", " + lon.ToString("F6", CultureInfo.InvariantCulture);

    // 정규 좌표 → 위경도 (NormD 의 반대)
    private static void LatLon(D2 n, out double lat, out double lon)
    {
        double y = YTop - n.y * (YTop - YBot);
        lat = (2 * Math.Atan(Math.Exp(y)) - Math.PI / 2) * 180 / Math.PI;
        lon = Clip(n.x, 0, 1) * 360 - 180;
    }

    // 수 하나 — 도분초(° ' ")까지. 방위 글자(N·S·E·W)는 따로 본다 (Hemi)
    private static readonly Regex CoordRx = new Regex(
        @"(?<![\d.])(?<deg>-?\d{1,3}(?:\.\d+)?)(?!\d)(?:\s*°(?:\s*(?<min>\d{1,2}(?:\.\d+)?)\s*'(?!'))?(?:\s*(?<sec>\d{1,2}(?:\.\d+)?)\s*(?:""|''))?)?");

    /// <summary>
    /// 좌표 글을 위경도로 — "37.5665, 126.9780" · "37.5665 126.9780" · 구글 지도 주소(…/@37.5665,126.9780,15z) ·
    /// 도분초 "37°33'59.4"N 126°58'40.8"E" · "북위 37도 33분, 동경 126도 58분" · S·W 는 음수 · 경도를 먼저 써도(90 을 넘으면) 알아본다
    /// </summary>
    public static bool TryParseCoord(string s, out double lat, out double lon)
    {
        lat = lon = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (s.Length > 300) s = s.Substring(0, 300);
        s = s.Replace('−', '-').Replace('–', '-').Replace('˚', '°').Replace('º', '°').Replace('′', '\'').Replace('’', '\'').Replace('″', '"').Replace('”', '"')
             .Replace("북위", "N").Replace("남위", "S").Replace("동경", "E").Replace("서경", "W").Replace("도", "°").Replace("분", "'").Replace("초", "\"");
        var found = new List<(double v, char pre, char post, bool deg, bool dec)>();
        foreach (Match m in CoordRx.Matches(s))
        {
            string deg = m.Groups["deg"].Value;
            if (!double.TryParse(deg, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) continue;
            double mi = 0, se = 0;
            if (m.Groups["min"].Success && !double.TryParse(m.Groups["min"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out mi)) continue;
            if (m.Groups["sec"].Success && !double.TryParse(m.Groups["sec"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out se)) continue;
            if (mi >= 60 || se >= 60) continue;
            double abs = Math.Abs(v) + mi / 60 + se / 3600;
            found.Add((deg.StartsWith("-") ? -abs : abs, Hemi(s, m.Index - 1, -1), Hemi(s, m.Index + m.Length, 1), m.Value.IndexOf('°') >= 0, deg.IndexOf('.') >= 0));
        }
        if (found.Count < 2) return false;
        // 방위는 글 전체가 한 가지로 쓴다 — 첫 수 앞에 있으면 모두 앞("N 37 E 126"), 아니면 모두 뒤("37N 126E")
        bool usePre = found[0].pre != ' ';
        var all = found.ConvertAll(f => (v: f.v, hemi: usePre ? f.pre : f.post, marked: f.deg || (usePre ? f.pre : f.post) != ' ', dec: f.dec));
        // 고를 두 수 — 방위나 ° 가 붙은 것 > 소수점이 있는 것(주소 속 확대 단계 '15z' 같은 수를 건너뛴다) > 아무 수.
        // 아무 수(정수 둘)는 낱말이 없는 글('37 127')에서만 — '강남대로 123길 45' 같은 주소의 번지를 좌표로 읽지 않게 (주소 검색으로 간다)
        var pick = all.FindAll(f => f.marked);
        if (pick.Count < 2) pick = all.FindAll(f => f.dec);
        if (pick.Count < 2 && !Regex.IsMatch(s, @"[가-힣]|[A-Za-z]{2,}")) pick = all;
        if (pick.Count < 2) return false;
        var a = pick[0];
        var b = pick[1];
        bool swap = a.hemi == 'E' || a.hemi == 'W' || b.hemi == 'N' || b.hemi == 'S'
                    || (a.hemi == ' ' && b.hemi == ' ' && Math.Abs(a.v) > 90 && Math.Abs(b.v) <= 90);
        if (swap) { var t = a; a = b; b = t; }
        lat = a.hemi == 'S' ? -Math.Abs(a.v) : a.hemi == 'N' ? Math.Abs(a.v) : a.v;
        lon = b.hemi == 'W' ? -Math.Abs(b.v) : b.hemi == 'E' ? Math.Abs(b.v) : b.v;
        return Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180;
    }

    // i 에서 dir 쪽으로 빈칸을 건너 만나는 방위 글자 하나 (단어 속 글자는 아님) — 없으면 ' '
    private static char Hemi(string s, int i, int dir)
    {
        while (i >= 0 && i < s.Length && s[i] == ' ') i += dir;
        if (i < 0 || i >= s.Length || "NSEW".IndexOf(s[i]) < 0) return ' ';
        int j = i + dir;
        return j >= 0 && j < s.Length && char.IsLetter(s[j]) ? ' ' : s[i];
    }

    // 보던 자리 — 지도 창을 닫을 때·앱이 뒤로 갈 때 적고, 다시 열 때 그대로
    private void SaveView()
    {
        if (!fitted || restorePending || viewport == null || viewport.rect.width <= 1f) return;
        D2 c = ToNorm(viewport.rect.size / 2f);
        var ci = CultureInfo.InvariantCulture;
        PlayerPrefs.SetString(ViewKey, c.x.ToString("R", ci) + "|" + c.y.ToString("R", ci) + "|" + zoom.ToString("R", ci));
        PlayerPrefs.Save();
    }

    private bool RestoreView()
    {
        var p = PlayerPrefs.GetString(ViewKey, "").Split('|');
        var ci = CultureInfo.InvariantCulture;
        if (p.Length != 3 || !double.TryParse(p[0], NumberStyles.Float, ci, out double x) || !double.TryParse(p[1], NumberStyles.Float, ci, out double y)
            || !double.TryParse(p[2], NumberStyles.Float, ci, out double z) || double.IsNaN(x + y + z) || double.IsInfinity(x + y + z)) return false;
        zoom = Clip(z, 1, 1e8);   // 타일 정보가 아직 안 왔을 수 있다 — 지금의 최대 확대로 자르지 않는다
        CenterOn(new D2(Clip(x, 0, 1), Clip(y, 0, 1)));
        touched = true;
        Layout(force: true);
        return true;
    }

    // ── 도우미 ───────────────────────────────────────────────
#if UNITY_EDITOR
    private static Vector2 previewMe;
    private List<R0926HuntList.Spot> previewHunts;
    private List<R0926HuntList.Spot> HuntSpots => previewHunts ?? R0926HuntList.Spots;

    /// <summary>캡처용 미리보기 (편집 모드) — 예시 친구 5명 · 보물찾기 둘(정확히 · 대략). zoomTo 가 있으면 그 자리로 확대</summary>
    public void EditorPreview(bool share, float zoomTo = 0f, float lat = 0f, float lon = 0f)
    {
        viewport = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        EnsureTextures();
        previewMe = new Vector2(37.50f, 127.03f);
        loggedIn = true; sharing = share; mutual = 12; loaded = true; fitted = false; netError = false; restorePending = false;
        friends.Clear();
        friends.Add(new Friend { user_id = "1", username = "minji", lat = 37.57f, lon = 126.98f, ago = 60 });
        friends.Add(new Friend { user_id = "2", username = "junho", lat = 35.18f, lon = 129.08f, ago = 720 });
        friends.Add(new Friend { user_id = "3", username = "hana", lat = 35.68f, lon = 139.69f, ago = 11000 });
        friends.Add(new Friend { user_id = "4", username = "leo.k", lat = 34.05f, lon = -118.24f, ago = 2400 });
        friends.Add(new Friend { user_id = "5", username = "chloe", lat = 48.86f, lon = 2.35f, ago = 36000 });
        previewHunts = new List<R0926HuntList.Spot>();
        if (share)
        {
            previewHunts.Add(new R0926HuntList.Spot { id = -1, title = "서울숲 가을 보물찾기", coord = R0926HuntList.Coord.Exact, lat = 37.5444, lon = 127.0374 });
            previewHunts.Add(new R0926HuntList.Spot { id = -2, title = "여의도 한강공원 주말 보물찾기", coord = R0926HuntList.Coord.Approx, lat = 37.5283, lon = 126.9326, approxM = 500f });
        }
        else friends.Clear();
        selectedId = "2";
        Refresh();
        if (zoomTo > 0f) { zoom = zoomTo; CenterOn(NormD(lat, lon)); touched = true; Layout(force: true); }   // 시험 중 보물찾기 목록이 바뀌어도 다시 맞추지 않게
        if (listContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
    }

    public void EditorPreviewClear()
    {
        foreach (var r in rows) Kill(r);
        rows.Clear();
        foreach (var g in pinPool) Kill(g);
        foreach (var g in clusterPool) Kill(g);
        foreach (var g in areaPool) Kill(g);
        foreach (var g in huntPinPool) Kill(g);
        foreach (var g in huntAreaPool) Kill(g);
        pinPool.Clear(); clusterPool.Clear(); areaPool.Clear(); huntPinPool.Clear(); huntAreaPool.Clear(); friends.Clear();
        previewMe = Vector2.zero;
        previewHunts = null;
        ClearDropPin();
        if (Searching) CloseSearch();
        if (worldImage != null) worldImage.texture = null;
        if (detailImage != null) detailImage.texture = null;
    }
#else
    private static List<R0926HuntList.Spot> HuntSpots => R0926HuntList.Spots;
#endif

    private static Vector2 MyLatLon()
    {
#if UNITY_EDITOR
        if (previewMe != Vector2.zero) return previewMe;
        if (VirtualLocation.Instance != null) return new Vector2(VirtualLocation.Instance.Latitude, VirtualLocation.Instance.Longitude);
#endif
        if (Input.location.status == LocationServiceStatus.Running)
            return new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        return Vector2.zero;
    }

    private static float Km(Vector2 me, Friend f)
    {
        if (me == Vector2.zero) return 0f;
        float dLat = (f.lat - me.x) * Mathf.Deg2Rad, dLon = (f.lon - me.y) * Mathf.Deg2Rad;
        float h = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) + Mathf.Cos(me.x * Mathf.Deg2Rad) * Mathf.Cos(f.lat * Mathf.Deg2Rad) * Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        return 6371f * 2f * Mathf.Asin(Mathf.Min(1f, Mathf.Sqrt(h)));
    }

    private static string Ago(int sec)
    {
        if (sec < 180) return L("방금", "just now", "たった今", "刚刚", "ahora");
        if (sec < 3600) return string.Format(L("{0}분 전", "{0}m ago", "{0}分前", "{0}分钟前", "hace {0} min"), sec / 60);
        return string.Format(L("{0}시간 전", "{0}h ago", "{0}時間前", "{0}小时前", "hace {0} h"), sec / 3600);
    }

    private static string L(string ko, string en, string ja, string zh, string es)
    {
        switch (R0926LocalizedText.Lang())
        {
            case "ko": return ko;
            case "ja": return ja;
            case "zh": return zh;
            case "es": return es;
            default: return en;
        }
    }
}
