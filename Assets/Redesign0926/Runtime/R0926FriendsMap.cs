using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 목록 시트의 '지도' — 맞팔 친구의 대략적인 위치(약 1km)를 세계 지도에.
///  · 서버: /api/p2p/friends_map · /api/p2p/map_share (토큰 필수, 규칙은 서버가 지킨다)
///  · 지도: Natural Earth 세계 지도 한 장 + 동아시아 정밀 지도를 겹친 메르카토르 (외부 지도 서버 호출 없음)
///    + 깊이 확대하면 우팡 서버의 지도 타일(R0926MapTiles — 서버가 켜 둔 경우만)
///  · 한 손가락 이동 · 두 손가락 확대 · 두 번 탭 확대 · +/− · 내 위치 · 가까운 친구는 숫자로 묶음
/// 이 컴포넌트는 지도 영역(viewport)에 붙는다 — 끌기·휠 이벤트를 직접 받는다.
/// </summary>
public class R0926FriendsMap : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
{
    // 지도 그림과 반드시 같은 값 (render_worldmap.py / render_detail.py)
    private const float LatTop = 78f, LatBottom = -58f;
    public const float DetailL = 118f, DetailR = 146f, DetailT = 46f, DetailB = 22f;

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
    [SerializeField] private RectTransform meDot;
    [SerializeField] private Image mePulse;
    [SerializeField] private Text chipText;
    [SerializeField] private float mapAspect = 4096f / 2283f;
    [Tooltip("타일이 없을 때 최대 확대 (세계 지도 폭의 배수) — 앱에 든 그림만으론 이 이상은 뭉개진다")]
    [SerializeField] private float maxZoom = 200f;
    [Tooltip("깊은 확대용 서버 지도 타일 — 서버가 켜 두면 동네 수준(화면 폭 약 2km)까지")]
    [SerializeField] private R0926MapTiles tiles;
    [SerializeField] private float clusterRadius = 120f;

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

    private static readonly Dictionary<string, Texture2D> avatarCache = new Dictionary<string, Texture2D>();
    private static readonly Color[] Palette =
    {
        new Color(0.97f, 0.78f, 0.45f), new Color(0.56f, 0.77f, 1f), new Color(0.72f, 0.91f, 0.53f),
        new Color(1f, 0.62f, 0.71f), new Color(0.79f, 0.71f, 1f), new Color(0.49f, 0.86f, 0.84f),
    };

    private readonly List<Friend> friends = new List<Friend>();
    private readonly List<GameObject> pinPool = new List<GameObject>();
    private readonly List<GameObject> clusterPool = new List<GameObject>();
    private readonly List<GameObject> rows = new List<GameObject>();
    private RectTransform viewport;
    private Canvas canvas;
    private float zoom = 1f;
    private Vector2 origin;          // 지도 왼쪽 위 모서리 — viewport 왼쪽 위 기준, 아래가 +
    private bool sharing, loggedIn, loaded, fitted, busy, mapShown;
    private int mutual;
    private string selectedId;
    private float nextRefresh;
    private float prevPinch = -1f;
    private Vector2 lastSize;
    private Coroutine anim;

    // ── 시작 ─────────────────────────────────────────────────
    private void Awake()
    {
        viewport = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        if (pinTemplate != null) pinTemplate.SetActive(false);
        if (clusterTemplate != null) clusterTemplate.SetActive(false);
        if (rowTemplate != null) rowTemplate.SetActive(false);
    }

    // 지도 패널이 열리고 닫힐 때 (탭 전환은 R0926SheetModes)
    private void OnEnable()
    {
        mapShown = true;
        EnsureTextures();
        nextRefresh = 0f;
        fitted = false;
    }

    private void OnDisable() { mapShown = false; }

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

        if (Time.unscaledTime >= nextRefresh && !busy)
        {
            nextRefresh = Time.unscaledTime + refreshSeconds;
            StartCoroutine(Fetch());
        }

        Vector2 size = viewport.rect.size;
        if (size != lastSize) { lastSize = size; Clamp(); Layout(); }

        Pinch();
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
        if (!loggedIn) { loaded = true; Refresh(); yield break; }

        busy = true;
        using (var req = UnityWebRequest.Get(ApiConfig.P2P_SERVER + "/api/p2p/friends_map"))
        {
            LoginManager.ApplyAuth(req);
            req.timeout = 10;
            yield return req.SendWebRequest();
            busy = false;
            if (req.result != UnityWebRequest.Result.Success)
            {
                if (!loaded) { loaded = true; Refresh(networkError: true); }
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

    // ── 화면 ─────────────────────────────────────────────────
    private void Refresh(bool networkError = false)
    {
        bool showConsent = !loggedIn || !sharing || networkError;
        if (dim != null) dim.SetActive(showConsent);
        if (consentCard != null) consentCard.SetActive(showConsent);
        if (consentSwitchRow != null) consentSwitchRow.SetActive(loggedIn && !networkError);
        if (consentSwitch != null) consentSwitch.sprite = sharing ? switchOn : switchOff;
        if (shareSwitch != null) shareSwitch.sprite = sharing ? switchOn : switchOff;

        if (consentTitle != null) consentTitle.text = L("친구 지도", "Friends map", "友だちマップ", "好友地图", "Mapa de amigos");
        if (consentBody != null)
        {
            consentBody.text = networkError
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
            var chipBox = chipText.transform.parent != null ? chipText.transform.parent.gameObject : chipText.gameObject;
            chipBox.SetActive(loggedIn && sharing);
            chipText.text = string.Format(L("맞팔 친구 {0}명 중 {1}명 공유 중", "{1} of {0} mutual friends sharing", "相互フォロー{0}人中 {1}人が共有中", "{0} 位互关好友中 {1} 位在共享", "{1} de {0} amigos mutuos comparten"), mutual, friends.Count);
        }

        BuildRows();
        if (!fitted && loaded) { FitAll(); fitted = true; }
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
    private static float MercY(float lat) => Mathf.Log(Mathf.Tan(Mathf.PI / 4f + Mathf.Clamp(lat, -85f, 85f) * Mathf.Deg2Rad / 2f));
    private static readonly float YTop = MercY(LatTop), YBot = MercY(LatBottom);

    /// <summary>웹 메르카토르 세로 좌표(0 = 북위 85.05°, 1 = 남위 85.05° — 지도 타일 줄) → 이 지도의 정규 세로 좌표 (둘은 1차식 관계)</summary>
    public static float WebToNormY(float webY) => (YTop - Mathf.PI + 2f * Mathf.PI * webY) / (YTop - YBot);

    private bool Tiled => tiles != null && tiles.Available;
    /// <summary>최대 확대 — 타일이 있으면 그 최대 단계까지, 없으면 maxZoom</summary>
    private float MaxZoom => Tiled ? Mathf.Max(maxZoom, tiles.ZoomForLevel(tiles.MaxLevel + 0.25f, viewport.rect.width, Scale)) : maxZoom;
    /// <summary>친구·내 위치를 눌렀을 때 확대 — 타일이 있으면 시내 수준(13 단계 · 화면 폭 약 15km)</summary>
    private float FocusZoom => Tiled ? Mathf.Min(MaxZoom, tiles.ZoomForLevel(13f, viewport.rect.width, Scale)) : 16f;

    /// <summary>위경도 → 지도 정규 좌표 (0,0 = 왼쪽 위, 1,1 = 오른쪽 아래)</summary>
    public static Vector2 Norm(float lat, float lon) => new Vector2((lon + 180f) / 360f, (YTop - MercY(lat)) / (YTop - YBot));

    private Vector2 MapSize => new Vector2(viewport.rect.width * zoom, viewport.rect.width * zoom / mapAspect);
    private Vector2 ToView(Vector2 n) => origin + Vector2.Scale(n, MapSize);

    private void Clamp()
    {
        Vector2 v = viewport.rect.size, m = MapSize;
        origin.x = m.x >= v.x ? Mathf.Clamp(origin.x, v.x - m.x, 0f) : (v.x - m.x) / 2f;
        origin.y = m.y >= v.y ? Mathf.Clamp(origin.y, v.y - m.y, 0f) : (v.y - m.y) / 2f;
        if (content != null)
        {
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.sizeDelta = m;
            content.anchoredPosition = new Vector2(origin.x, -origin.y);
        }
    }

    private void ZoomAt(Vector2 viewPoint, float factor)
    {
        float nz = Mathf.Clamp(zoom * factor, 1f, MaxZoom);
        Vector2 n = Vector2.Scale(viewPoint - origin, new Vector2(1f / MapSize.x, 1f / MapSize.y));
        zoom = nz;
        origin = viewPoint - Vector2.Scale(n, MapSize);
        Clamp();
        Layout();
    }

    private void CenterOn(Vector2 n)
    {
        origin = viewport.rect.size / 2f - Vector2.Scale(n, MapSize);
        Clamp();
    }

    private void FitAll()
    {
        Vector2 me = MyLatLon();
        var pts = new List<Vector2>();
        if (me != Vector2.zero) pts.Add(Norm(me.x, me.y));
        if (sharing) foreach (var f in friends) pts.Add(Norm(f.lat, f.lon));
        if (pts.Count == 0) { zoom = 1f; CenterOn(new Vector2(0.5f, 0.45f)); return; }

        Vector2 min = pts[0], max = pts[0];
        foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        Vector2 v = viewport.rect.size;
        float minBox = Tiled ? 0.00005f : 0.004f;   // 친구들이 한 동네에 모여 있어도 지나치게 확대하지 않게 (타일 있으면 약 2km)
        float bw = Mathf.Max(max.x - min.x, minBox), bh = Mathf.Max(max.y - min.y, minBox);
        float z = Mathf.Min(1f / (bw * 1.4f), v.y * mapAspect / (bh * 1.4f * v.x));
        zoom = Mathf.Clamp(z, 1f, pts.Count == 1 ? Mathf.Max(14f, FocusZoom * 0.1f) : MaxZoom * 0.5f);
        CenterOn((min + max) / 2f);
    }

    // ── 핀 ───────────────────────────────────────────────────
    private void Layout(bool force = false)
    {
        if (tiles != null) tiles.UpdateView(origin, MapSize, viewport.rect.size, Scale);
        if (pinLayer == null) return;
        Vector2 me = MyLatLon();
        if (meDot != null)
        {
            meDot.gameObject.SetActive(me != Vector2.zero);
            if (me != Vector2.zero) Place(meDot, ToView(Norm(me.x, me.y)));
        }

        int p = 0, c = 0;
        if (sharing && loggedIn)
        {
            // 화면 거리 기준으로 가까운 친구를 묶는다 (욕심쟁이 방식 — 친구 수가 적어 충분)
            var groups = new List<(Vector2 pos, List<Friend> members)>();
            foreach (var f in friends)
            {
                Vector2 q = ToView(Norm(f.lat, f.lon));
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
            Vector2 v = viewport.rect.size;
            foreach (var g in groups)
            {
                if (g.pos.x < -120 || g.pos.y < -120 || g.pos.x > v.x + 120 || g.pos.y > v.y + 120) continue;
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
        for (int i = p; i < pinPool.Count; i++) if (pinPool[i].activeSelf) pinPool[i].SetActive(false);
        for (int i = c; i < clusterPool.Count; i++) if (clusterPool[i].activeSelf) clusterPool[i].SetActive(false);
    }

    private GameObject Pool(List<GameObject> pool, GameObject template, int index)
    {
        while (pool.Count <= index)
        {
            var go = Instantiate(template, pinLayer);
            go.name = "_";
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
        Vector2 target = Norm(f.lat, f.lon);
        StartAnim(target, Mathf.Max(zoom, FocusZoom));
    }

    public void ZoomIn() => Animate(viewport.rect.size / 2f, 2f);
    public void ZoomOut() => Animate(viewport.rect.size / 2f, 0.5f);

    public void LocateMe()
    {
        Vector2 me = MyLatLon();
        if (me == Vector2.zero) return;
        StartAnim(Norm(me.x, me.y), Mathf.Max(zoom, FocusZoom));
    }

    private void Animate(Vector2 viewPoint, float factor)
    {
        Vector2 n = Vector2.Scale(viewPoint - origin, new Vector2(1f / MapSize.x, 1f / MapSize.y));
        StartAnim(n, Mathf.Clamp(zoom * factor, 1f, MaxZoom), keepPoint: factor != 3f ? viewPoint : (Vector2?)null);
    }

    private void StartAnim(Vector2 n, float targetZoom, Vector2? keepPoint = null)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(AnimRoutine(n, targetZoom, keepPoint));
    }

    private IEnumerator AnimRoutine(Vector2 n, float targetZoom, Vector2? keepPoint)
    {
        float z0 = zoom, t = 0f;
        Vector2 center = viewport.rect.size / 2f;
        Vector2 from = Vector2.Scale(center - origin, new Vector2(1f / MapSize.x, 1f / MapSize.y));   // 지금 화면 가운데의 지도 좌표
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / 0.35f);
            float e = 1f - Mathf.Pow(1f - t, 3f);
            zoom = Mathf.Exp(Mathf.Lerp(Mathf.Log(z0), Mathf.Log(targetZoom), e));
            if (keepPoint.HasValue)
                origin = keepPoint.Value - Vector2.Scale(n, MapSize);        // 누른 곳을 붙잡고 확대
            else
                origin = center - Vector2.Scale(Vector2.Lerp(from, n, e), MapSize);
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

    public void OnBeginDrag(PointerEventData e) { if (anim != null) { StopCoroutine(anim); anim = null; } }

    public void OnDrag(PointerEventData e)
    {
        if (Touches(out _, out _) >= 2) return;   // 두 손가락이면 확대·축소 쪽
        origin += new Vector2(e.delta.x, -e.delta.y) / Scale;
        Clamp();
        Layout();
    }

    public void OnEndDrag(PointerEventData e) { }

    public void OnScroll(PointerEventData e)
    {
        ZoomAt(LocalTopLeft(e.position), Mathf.Pow(1.15f, e.scrollDelta.y));
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.clickCount == 2) Animate(LocalTopLeft(e.position), 2f);
    }

    private void Pinch()
    {
        // 새 입력 시스템만 켜져 있다 — 예전 Input.touchCount 는 기기에서 예외가 나 끌기·확대가 통째로 안 됐다
        if (Touches(out Vector2 a, out Vector2 b) != 2) { prevPinch = -1f; return; }
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (prevPinch < 0f && !RectTransformUtility.RectangleContainsScreenPoint(viewport, (a + b) / 2f, cam)) return;
        float d = Vector2.Distance(a, b);
        Vector2 mid = LocalTopLeft((a + b) / 2f);
        if (prevPinch > 0f && d > 1f)
        {
            if (anim != null) { StopCoroutine(anim); anim = null; }
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

    // ── 도우미 ───────────────────────────────────────────────
#if UNITY_EDITOR
    private static Vector2 previewMe;

    /// <summary>캡처용 미리보기 (편집 모드) — 예시 친구 5명. zoomTo 가 있으면 그 자리로 확대</summary>
    public void EditorPreview(bool share, float zoomTo = 0f, float lat = 0f, float lon = 0f)
    {
        viewport = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        EnsureTextures();
        previewMe = new Vector2(37.50f, 127.03f);
        loggedIn = true; sharing = share; mutual = 12; loaded = true; fitted = false;
        friends.Clear();
        friends.Add(new Friend { user_id = "1", username = "minji", lat = 37.57f, lon = 126.98f, ago = 60 });
        friends.Add(new Friend { user_id = "2", username = "junho", lat = 35.18f, lon = 129.08f, ago = 720 });
        friends.Add(new Friend { user_id = "3", username = "hana", lat = 35.68f, lon = 139.69f, ago = 11000 });
        friends.Add(new Friend { user_id = "4", username = "leo.k", lat = 34.05f, lon = -118.24f, ago = 2400 });
        friends.Add(new Friend { user_id = "5", username = "chloe", lat = 48.86f, lon = 2.35f, ago = 36000 });
        if (!share) friends.Clear();
        selectedId = "2";
        Refresh();
        if (zoomTo > 0f) { zoom = zoomTo; CenterOn(Norm(lat, lon)); Layout(); }
        if (listContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
    }

    public void EditorPreviewClear()
    {
        foreach (var r in rows) Kill(r);
        rows.Clear();
        foreach (var g in pinPool) Kill(g);
        foreach (var g in clusterPool) Kill(g);
        pinPool.Clear(); clusterPool.Clear(); friends.Clear();
        previewMe = Vector2.zero;
        if (worldImage != null) worldImage.texture = null;
        if (detailImage != null) detailImage.texture = null;
    }
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
