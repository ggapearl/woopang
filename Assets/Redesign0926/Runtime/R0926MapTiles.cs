using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 친구 지도 깊은 확대 — 우팡 서버가 내주는 지도 타일(웹 메르카토르 z/x/y PNG)을 세계 그림 위에 겹친다.
/// 앱은 우팡 서버만 부른다 (지도 회사 키·캐시는 서버 몫 — docs/claude/server-spec-1005.md 5).
/// 서버가 /api/map/tiles/info 로 켜져 있다고 답할 때만 타일을 받고, 아니면 아무것도 받지 않는다 (앱에 든 그림만 — 흐리게 확대).
/// 받는 중인 칸은 받아 둔 위 단계 타일을 잘라 채우고, 축소하는 중이면 받아 둔 아래 단계 타일로 채운다.
/// 한 단계 위 타일을 화면 두 배 넓이로 미리 받아 두어 축소하면 바로 보이고, 옮기는 동안에도 비지 않는다.
/// 처음 보는 곳은 화면을 한두 장으로 덮는 두 단계 위 타일부터 받아 흐리게라도 한 번에 채우고, 막 받은 타일은 그 위에 서서히 나타난다.
/// 지도를 닫으면 메모리의 타일은 버리고(안 쓰는 동안 메모리 0), 받은 타일은 기기 캐시 폴더에 서버가 적어 준 기간만 둔다 — 다시 열면 거기서 바로.
/// </summary>
public class R0926MapTiles : MonoBehaviour
{
    [Tooltip("타일을 놓을 곳 — 지도 창 안, 세계 그림 위 · 핀 아래")]
    [SerializeField] private RectTransform layer;
    [Tooltip("지도 출처 (서버가 알려 준 글) — 타일이 보일 때만")]
    [SerializeField] private Text attribution;
    [Tooltip("이보다 덜 확대됐으면 앱에 든 그림으로 충분 — 타일을 받지 않는다")]
    [SerializeField] private int minLevel = 5;
    [SerializeField] private int maxCached = 96;
    [SerializeField] private int maxRequests = 6;
    [SerializeField] private float tilePixels = 256f;

    // 서버가 주는 단계까지 (서버 상한 19). 지도 좌표는 double 로 받아 깊어도 칸이 어긋나지 않는다
    private const int HardMaxLevel = 19;
    private const int MaxVisible = 80;
    private const int ParentLevels = 6;
    private const int MaxDiskLoads = 6;
    private const int CoverLevels = 2;              // 처음 보는 곳 — 이만큼 위 단계 타일로 화면을 먼저 덮는다 (한 장이 칸 16개)
    private const float FadeTime = 0.2f;            // 막 받은 타일이 나타나는 시간 — 흐린 칸이 또렷한 칸으로 툭 바뀌지 않게
    private const int MaxKeepSec = 259200;          // 서버가 더 길게 적어 와도 3일까지 (브이월드 약관 제19조 — 서버 캐시와 같은 기준)
    private const long DiskCapBytes = 80L << 20;    // 기기 캐시 상한 (타일 한 장 약 20KB — 4천 장쯤). 넘으면 곧 만료될 것부터 지운다

    [Serializable] private class Info { public bool enabled; public int min_zoom; public int max_zoom; public string attribution; public string layer; }

    private struct Load { public long key; public UnityWebRequest req; public bool disk; }

#if UNITY_EDITOR
    /// <summary>플레이 시험이 시험 서버로 돌릴 때 (비어 있으면 실제 서버)</summary>
    public static string EditorBase;
    private static string InfoUrl => string.IsNullOrEmpty(EditorBase) ? ApiConfig.MAP_TILES_INFO : EditorBase + "/api/map/tiles/info";
    private static string TileUrl => string.IsNullOrEmpty(EditorBase) ? ApiConfig.MAP_TILES : EditorBase + "/api/map/tiles/{z}/{x}/{y}.png";
#else
    private static string InfoUrl => ApiConfig.MAP_TILES_INFO;
    private static string TileUrl => ApiConfig.MAP_TILES;
#endif

    private static Info info;          // 앱이 켜져 있는 동안 한 번 받는다 (못 받으면 10분 뒤 다시)
    private static float infoRetryAt;
    private static bool diskCleaned;   // 기기 캐시 정리는 앱이 켜질 때마다 한 번
    private UnityWebRequest infoReq;

    private readonly Dictionary<long, Texture2D> cache = new Dictionary<long, Texture2D>();
    private readonly Dictionary<long, int> lastUsed = new Dictionary<long, int>();
    private readonly Dictionary<long, float> failedUntil = new Dictionary<long, float>();
    private readonly List<Load> inflight = new List<Load>();
    private readonly List<(float dist, long key, bool ahead)> wanted = new List<(float, long, bool)>();   // ahead: 미리 받기 (위 단계 두 배 넓이)
    private readonly List<(long key, float x, float y, double w, double h)> ahead = new List<(long, float, float, double, double)>();
    private readonly HashSet<long> notOnDisk = new HashSet<long>();   // 기기 캐시에 없다고 본 칸 — 매 프레임 파일을 다시 보지 않게
    private readonly List<RawImage> pool = new List<RawImage>();
    private readonly HashSet<long> inUse = new HashSet<long>();   // 지금 화면에 쓰는 타일 (위·아래 단계 포함) — 버리지 않는다
    private readonly HashSet<long> cover = new HashSet<long>();   // 아무것도 못 그린 칸을 덮는 위 단계 타일 — 가장 먼저 받는다
    private readonly Dictionary<long, float> arrived = new Dictionary<long, float>();   // 막 받은 타일 → 받은 시각 (나타나는 중)
    private bool fading;               // 마지막 화면에 나타나는 중인 타일이 있었다 — 다 나타날 때까지 매 프레임 다시 그린다
    private int failures;
    private float pausedUntil;
    private int holes;                 // 마지막 화면에서 아무것도 못 그린 칸 수 (아래 세계 그림이 비친다)
    private string diskDir;

    private bool hasView;
    private double lastOx, lastOy, lastMapW, lastMapH;
    private Vector2 lastView;
    private float lastScale = 1f;

    private static readonly Rect FullUv = new Rect(0f, 0f, 1f, 1f);
    // 보이는 칸을 가운데부터, 그다음 미리 받기
    private static readonly Comparison<(float dist, long key, bool ahead)> ByPriority =
        (p, q) => p.ahead != q.ahead ? (p.ahead ? 1 : -1) : p.dist.CompareTo(q.dist);

    public bool Available => info != null && info.enabled;
    public int MaxLevel => info != null && info.max_zoom > 0 ? Mathf.Clamp(info.max_zoom, MinLevel, HardMaxLevel) : HardMaxLevel;
    // 지도 회사가 주는 가장 얕은 단계 (브이월드 6) — 그보다 얕은 칸은 묻지 않는다
    private int MinLevel => info != null ? Mathf.Clamp(info.min_zoom, minLevel, HardMaxLevel) : minLevel;

    /// <summary>타일을 몇 단계 덜 자세한 것으로 받아 크게 그리나 — 화면이 촘촘할수록 더 (약 200dpi 에 256px 한 장).
    /// 화면 픽셀 그대로 그리면 3배 화면에선 지도 글자가 1/3 크기이고 한 화면에 60~70장을 받아야 했다 (10-21)</summary>
    public float DensityLevels
    {
        get
        {
            float dpi = Screen.dpi;
            float d = dpi > 0f ? dpi / 200f : Mathf.Min(Screen.width, Screen.height) / 500f;   // dpi 를 모르는 기기 — 폰 폭 약 400dp 로 어림
            return Mathf.Log(Mathf.Clamp(d, 1f, 3f), 2f);
        }
    }

    /// <summary>이 단계 타일이 화면 픽셀과 1:1 이 되는 지도 확대 배율 (R0926FriendsMap 의 zoom 단위)</summary>
    public double ZoomForLevel(double level, float viewWidth, float scale)
        => tilePixels * Math.Pow(2, level) / Mathf.Max(1f, viewWidth * scale);

    private static long Key(int z, int x, int y) => ((long)z << 48) | ((long)x << 24) | (uint)y;
    private static int KZ(long k) => (int)(k >> 48);
    private static int KX(long k) => (int)((k >> 24) & 0xFFFFFF);
    private static int KY(long k) => (int)(k & 0xFFFFFF);

    private void OnEnable()
    {
        if (!Available && infoReq == null && Time.realtimeSinceStartup >= infoRetryAt)
        {
            infoReq = UnityWebRequest.Get(InfoUrl);
            infoReq.timeout = 8;
            infoReq.SendWebRequest();
        }
        PrepareDisk();
    }

    private void OnDisable()
    {
        if (infoReq != null) { infoReq.Abort(); infoReq.Dispose(); infoReq = null; }
        foreach (var f in inflight) { f.req.Abort(); f.req.Dispose(); }
        inflight.Clear();
        foreach (var t in cache.Values) if (t != null) Destroy(t);
        cache.Clear();
        lastUsed.Clear();
        arrived.Clear();
        fading = false;
        notOnDisk.Clear();
        foreach (var img in pool) if (img != null) { img.texture = null; img.enabled = false; }
        if (attribution != null) attribution.enabled = false;
        wanted.Clear();
        inUse.Clear();
        hasView = false;
    }

    /// <summary>지도가 움직일 때마다 R0926FriendsMap 이 부른다 — ox, oy: 지도 왼쪽 위(지도 창 왼쪽 위 기준, 아래가 +), mapW, mapH: 지도 전체 크기.
    /// 깊이 확대하면 지도가 수억 단위라 double 로 받는다 — 칸 자리는 화면 안의 작은 수가 된 뒤에야 float 로</summary>
    public void UpdateView(double ox, double oy, double mapW, double mapH, Vector2 view, float scale)
    {
        hasView = true;
        lastOx = ox; lastOy = oy; lastMapW = mapW; lastMapH = mapH; lastView = view; lastScale = scale;
        wanted.Clear();
        inUse.Clear();
        cover.Clear();
        fading = false;
        int shown = 0;
        holes = 0;
        if (Available && layer != null && mapW > 1 && mapH > 1)
        {
            double lv = Math.Log(mapW * scale / tilePixels, 2);   // 타일이 화면 픽셀과 1:1 이 되는 단계
            double a = R0926FriendsMap.WebToNormY(0), b = R0926FriendsMap.WebToNormY(1) - a;
            double baseY = oy + mapH * a;
            // 덜 확대됐으면 앱에 든 그림으로 (1:1 기준 그대로) — 받는 단계는 화면 밀도만큼 덜 자세한 것을 크게.
            // 화면이 커서(태블릿·가로) 칸이 너무 많으면 한 단계 더 덜 자세한 타일로
            int z = Math.Floor(lv + 0.5) < MinLevel ? MinLevel - 1 : Mathf.Clamp((int)Math.Floor(lv - DensityLevels + 0.5), MinLevel, MaxLevel);
            int n = 1, x0 = 0, x1 = -1, y0 = 0, y1 = -1;
            double tw = 0, th = 0;
            for (; z >= MinLevel; z--)
            {
                n = 1 << z;
                tw = mapW / n;
                th = mapH * b / n;
                x0 = Mathf.Max(0, (int)Math.Floor(-ox / tw)); x1 = Mathf.Min(n - 1, (int)Math.Floor((view.x - ox) / tw));
                y0 = Mathf.Max(0, (int)Math.Floor(-baseY / th)); y1 = Mathf.Min(n - 1, (int)Math.Floor((view.y - baseY) / th));
                if ((x1 - x0 + 1) * (y1 - y0 + 1) <= MaxVisible) break;
            }
            if (z >= MinLevel && x1 >= x0 && y1 >= y0)
            {
                Vector2 center = view / 2f;
                int frame = Time.frameCount;
                // 아무것도 못 그린 칸은 그 칸을 덮는 두 단계 위 타일(화면 전체가 한두 장)을 가장 먼저 받는다 — 처음 보는 곳도 흐리게라도
                // 화면이 한 번에 차고, 자세한 칸은 받는 대로 그 위에 (10-23 — 처음 보는 동네는 브이월드가 한 장에 0.5~1.5초라 2초쯤 듬성듬성 비었다)
                int zc = Math.Max(MinLevel, z - CoverLevels), d = z - zc;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = (float)(ox + x * tw), py = (float)(baseY + y * th);
                        long k = Key(z, x, y);
                        int used = Show(shown, k, px, py, (float)tw, (float)th, frame);
                        if (used == 0)
                        {
                            holes++;
                            if (d > 0) cover.Add(Key(zc, x >> d, y >> d));
                        }
                        shown += used;
                        if (!cache.ContainsKey(k)) Want(k, px, py, tw, th, center, false);
                    }
                foreach (long ck in cover)
                    if (!cache.ContainsKey(ck)) wanted.Add((-1f, ck, false));
                // 다음에 쓸 칸 — 한 단계 위 타일을 화면 두 배 넓이로: 한 단계 축소하면 그대로 보이고, 옮기거나 확대하는 동안 빈 곳을 흐리게라도 채운다.
                // (화면 둘레 한 줄을 같은 단계로 더 받는 것은 요청이 두 배가 돼 뺐다 — 10-21 시험에서 서버 분당 상한에 걸림)
                // 보이는 칸과 함께 메모리에 다 들어갈 때만 — 칸이 많은 큰 화면에선 미리 받은 것끼리 서로 밀어내며 다시 받게 된다
                ahead.Clear();
                if (z - 1 >= MinLevel)
                {
                    int m = n >> 1;
                    double pw = tw * 2, ph = th * 2;
                    int px0 = Mathf.Max(0, (int)Math.Floor((-view.x * 0.5 - ox) / pw)), px1 = Mathf.Min(m - 1, (int)Math.Floor((view.x * 1.5 - ox) / pw));
                    int py0 = Mathf.Max(0, (int)Math.Floor((-view.y * 0.5 - baseY) / ph)), py1 = Mathf.Min(m - 1, (int)Math.Floor((view.y * 1.5 - baseY) / ph));
                    for (int y = py0; y <= py1; y++)
                        for (int x = px0; x <= px1; x++)
                            ahead.Add((Key(z - 1, x, y), (float)(ox + x * pw), (float)(baseY + y * ph), pw, ph));
                }
                if (inUse.Count + wanted.Count + ahead.Count <= maxCached)
                    foreach (var c in ahead)
                    {
                        if (cache.ContainsKey(c.key)) lastUsed[c.key] = frame;   // 지난 화면 타일보다 늦게 버리게
                        else Want(c.key, c.x, c.y, c.w, c.h, center, true);
                    }
                wanted.Sort(ByPriority);
            }
        }
        for (int i = shown; i < pool.Count; i++) if (pool[i].enabled) { pool[i].enabled = false; pool[i].texture = null; }
        if (attribution != null)
        {
            bool on = shown > 0 && !string.IsNullOrEmpty(info.attribution);
            attribution.enabled = on;
            if (on) attribution.text = info.attribution;
        }
    }

    private void Want(long k, float x, float y, double w, double h, Vector2 center, bool ahead)
        => wanted.Add(((new Vector2(x + (float)w / 2f, y + (float)h / 2f) - center).sqrMagnitude, k, ahead));

    // 칸 하나 — 받은 타일. 없으면 받아 둔 위 단계 타일을 잘라 깔고, 받아 둔 아래 단계 타일(축소하는 중)이 있으면 그 위에.
    // 막 받은 타일은 그 위에 서서히 나타난다. 아무것도 없으면 안 그린다 (아래 세계 그림이 보인다). 쓴 그림 수를 돌려준다
    private int Show(int index, long k, float x, float y, float w, float h, int frame)
    {
        float alpha = 0f;
        if (cache.TryGetValue(k, out var own))
        {
            Use(k, frame);
            alpha = Opacity(k);
            if (alpha >= 1f)
            {
                Place(index, own, FullUv, x, y, w, h, 1f);
                return 1;
            }
        }
        Texture2D tex;
        int z = KZ(k), tx = KX(k), ty = KY(k), used = 0, kids = 0;
        if (z < MaxLevel)
            for (int i = 0; i < 4; i++)
                if (cache.ContainsKey(Key(z + 1, tx * 2 + (i & 1), ty * 2 + (i >> 1)))) kids++;
        if (kids < 4)
            for (int up = 1; up <= ParentLevels && z - up >= MinLevel; up++)
            {
                long pk = Key(z - up, tx >> up, ty >> up);
                if (!cache.TryGetValue(pk, out tex)) continue;
                Use(pk, frame);
                float s = 1f / (1 << up);
                int ox = tx - ((tx >> up) << up), oy = ty - ((ty >> up) << up);
                Place(index + used++, tex, new Rect(ox * s, 1f - (oy + 1) * s, s, s), x, y, w, h, 1f);   // 그림의 v 는 아래에서 위로
                break;
            }
        if (kids > 0)
            for (int i = 0; i < 4; i++)
            {
                int cx = i & 1, cy = i >> 1;
                long ck = Key(z + 1, tx * 2 + cx, ty * 2 + cy);
                if (!cache.TryGetValue(ck, out tex)) continue;
                Use(ck, frame);
                Place(index + used++, tex, FullUv, x + cx * w / 2f, y + cy * h / 2f, w / 2f, h / 2f, 1f);
            }
        if (own != null) Place(index + used++, own, FullUv, x, y, w, h, alpha);
        return used;
    }

    private void Use(long k, int frame)
    {
        lastUsed[k] = frame;
        inUse.Add(k);
    }

    // 막 받은 타일의 불투명도 — FadeTime 동안 0 → 1. 다 나타나면 기록을 지운다
    private float Opacity(long k)
    {
        if (!arrived.TryGetValue(k, out float at)) return 1f;
        float p = (Time.realtimeSinceStartup - at) / FadeTime;
        if (p >= 1f) { arrived.Remove(k); return 1f; }
        fading = true;
        return p <= 0f ? 0f : p * p * (3f - 2f * p);
    }

    private void Place(int index, Texture2D tex, Rect uv, float x, float y, float w, float h, float alpha)
    {
        while (pool.Count <= index)
        {
            var go = new GameObject("Tile", typeof(RectTransform), typeof(RawImage));
            go.layer = layer.gameObject.layer;
            go.transform.SetParent(layer, false);
            var ri = go.GetComponent<RawImage>();
            ri.raycastTarget = false;
            var rt = ri.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            pool.Add(ri);
        }
        var img = pool[index];
        img.texture = tex;
        img.uvRect = uv;
        img.color = new Color(1f, 1f, 1f, alpha);
        img.rectTransform.anchoredPosition = new Vector2(x, -y);
        img.rectTransform.sizeDelta = new Vector2(w + 1f, h + 1f);   // 1 겹쳐 칸 사이 실금을 덮는다
        if (!img.enabled) img.enabled = true;
    }

    private void Update()
    {
        if (infoReq != null && infoReq.isDone)
        {
            Info got = null;
            if (infoReq.result == UnityWebRequest.Result.Success)
                try { got = JsonUtility.FromJson<Info>(infoReq.downloadHandler.text); } catch (Exception) { }
            infoReq.Dispose();
            infoReq = null;
            if (got != null) { info = got; diskDir = null; }
            if (!Available) infoRetryAt = Time.realtimeSinceStartup + 600f;   // 서버가 아직 타일을 안 준다(없음·꺼짐) — 10분 뒤 다시
            PrepareDisk();
            if (Available && hasView) UpdateView(lastOx, lastOy, lastMapW, lastMapH, lastView, lastScale);
        }
        if (!Available) return;

        float now = Time.realtimeSinceStartup;
        bool changed = false;
        for (int i = inflight.Count - 1; i >= 0; i--)
        {
            Load f = inflight[i];
            if (!f.req.isDone) continue;
            inflight.RemoveAt(i);
            var req = f.req;
            Texture2D tex = req.result == UnityWebRequest.Result.Success ? DownloadHandlerTexture.GetContent(req) : null;
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                cache[f.key] = tex;
                lastUsed[f.key] = Time.frameCount;
                arrived[f.key] = now;
                changed = true;
                if (!f.disk) { failures = 0; SaveToDisk(f.key, req); }
            }
            else if (f.disk) { notOnDisk.Add(f.key); DeleteLater(TileFile(f.key)); }   // 망가진 파일 — 지우고 서버에서 다시
            else if (req.responseCode == 404)
                failedUntil[f.key] = now + 600f;   // 그 칸엔 지도가 없다(국외·바다 밖) — 서버 탓이 아니니 쉬지 않는다
            else if (req.responseCode == 429)
                pausedUntil = Mathf.Max(pausedUntil, now + 3f);   // 서버의 분당 상한 — 잠깐 쉬고 같은 칸을 다시 (예전엔 그 칸이 30초 비고 여덟 번이면 1분 멈췄다)
            else
            {
                failedUntil[f.key] = now + 30f;
                if (++failures >= 8) { pausedUntil = now + 60f; failures = 0; }   // 서버가 힘들다 — 잠깐 쉰다
            }
            req.Dispose();
        }
        if (changed) Trim();
        if ((changed || fading) && hasView) UpdateView(lastOx, lastOy, lastMapW, lastMapH, lastView, lastScale);   // 막 받은 타일이 나타나는 동안은 매 프레임

        int net = 0, disk = 0;
        foreach (var f in inflight) if (f.disk) disk++; else net++;
        bool netOk = now >= pausedUntil;
        foreach (var w in wanted)
        {
            if ((net >= maxRequests || !netOk) && disk >= MaxDiskLoads) break;
            long k = w.key;
            if (cache.ContainsKey(k) || IsInflight(k)) continue;
            if (failedUntil.TryGetValue(k, out float until) && now < until) continue;
            if (diskDir != null && !notOnDisk.Contains(k))
            {
                if (disk >= MaxDiskLoads) continue;
                string file = TileFile(k);
                if (OnDisk(file))
                {
                    var dreq = UnityWebRequestTexture.GetTexture(new Uri(file).AbsoluteUri, true);
                    dreq.SendWebRequest();
                    inflight.Add(new Load { key = k, req = dreq, disk = true });
                    disk++;
                    continue;
                }
                notOnDisk.Add(k);
            }
            if (net >= maxRequests || !netOk) continue;
            string url = TileUrl.Replace("{z}", KZ(k).ToString()).Replace("{x}", KX(k).ToString()).Replace("{y}", KY(k).ToString());
            var req = UnityWebRequestTexture.GetTexture(url, true);   // 읽기 전용 — CPU 쪽 사본을 남기지 않는다
            req.timeout = 15;
            LoginManager.ApplyAuth(req);
            req.SendWebRequest();
            inflight.Add(new Load { key = k, req = req });
            net++;
        }
    }

    private bool IsInflight(long k)
    {
        foreach (var f in inflight) if (f.key == k) return true;
        return false;
    }

    // 오래 안 쓴 타일부터 버린다 (지금 보이는 칸은 남긴다)
    private void Trim()
    {
        while (cache.Count > maxCached)
        {
            long oldest = 0; int best = int.MaxValue; bool found = false;
            foreach (var kv in lastUsed)
                if (kv.Value < best && !inUse.Contains(kv.Key) && cache.ContainsKey(kv.Key)) { best = kv.Value; oldest = kv.Key; found = true; }
            if (!found) break;
            if (cache.TryGetValue(oldest, out var t) && t != null) Destroy(t);
            cache.Remove(oldest);
            lastUsed.Remove(oldest);
            arrived.Remove(oldest);
        }
    }

    // ── 기기 캐시 — 서버가 알려 준 층 이름별 폴더 (지도 회사·층을 바꾸면 새 폴더, 지난 폴더는 기한이 지나면 정리) ──
    // 파일 시각을 만료 시각으로 적어 둔다 — 지금보다 뒤면 쓴다. 쓰기·정리는 뒤에서 (화면이 멈추지 않게)

    private void PrepareDisk()
    {
        if (diskDir != null || !Available || string.IsNullOrEmpty(Application.temporaryCachePath)) return;
        string root = Path.Combine(Application.temporaryCachePath, "maptiles");
        string id = info.layer;
        bool plain = !string.IsNullOrEmpty(id) && id.Length <= 32;
        if (plain) foreach (char c in id) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'z')) { plain = false; break; }
        diskDir = Path.Combine(root, plain ? id : "default");
        if (!diskCleaned) { diskCleaned = true; CleanLater(root); }
    }

    private string TileFile(long k) => Path.Combine(diskDir, KZ(k).ToString(), KX(k).ToString(), KY(k) + ".tile");

    private static bool OnDisk(string file)
    {
        try { return File.GetLastWriteTimeUtc(file) > DateTime.UtcNow; } catch (Exception) { return false; }
    }

    private void SaveToDisk(long k, UnityWebRequest req)
    {
        if (diskDir == null) return;
        int sec = Math.Min(MaxAge(Header(req, "Cache-Control")), MaxKeepSec);
        byte[] bytes = sec > 0 ? req.downloadHandler.data : null;
        if (bytes == null || bytes.Length == 0) return;
        string file = TileFile(k);
        DateTime until = DateTime.UtcNow.AddSeconds(sec);
        notOnDisk.Remove(k);
        Task.Run(() =>
        {
            string tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(tmp, bytes);
                File.SetLastWriteTimeUtc(tmp, until);
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
            }
            catch (Exception) { try { File.Delete(tmp); } catch (Exception) { } }
        });
    }

    private static void DeleteLater(string file) => Task.Run(() => { try { File.Delete(file); } catch (Exception) { } });

    // 기한이 지난 타일·저장하다 끊긴 임시 파일을 지우고, 그래도 상한을 넘으면 곧 만료될 것부터
    private static void CleanLater(string root) => Task.Run(() =>
    {
        try
        {
            if (!Directory.Exists(root)) return;
            DateTime now = DateTime.UtcNow;
            var keep = new List<(DateTime until, long size, string path)>();
            long total = 0;
            foreach (var f in new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories))
                try
                {
                    if (f.Name.EndsWith(".tmp", StringComparison.Ordinal) || f.LastWriteTimeUtc <= now) { f.Delete(); continue; }
                    keep.Add((f.LastWriteTimeUtc, f.Length, f.FullName));
                    total += f.Length;
                }
                catch (Exception) { }
            if (total > DiskCapBytes)
            {
                keep.Sort((p, q) => p.until.CompareTo(q.until));
                foreach (var e in keep)
                {
                    if (total <= DiskCapBytes * 3 / 4) break;
                    try { File.Delete(e.path); total -= e.size; } catch (Exception) { }
                }
            }
            var dirs = Directory.GetDirectories(root, "*", SearchOption.AllDirectories);
            Array.Sort(dirs, (p, q) => q.Length.CompareTo(p.Length));   // 깊은 폴더부터 — 빈 폴더 지우기
            foreach (var d in dirs)
                try
                {
                    using (var e = Directory.EnumerateFileSystemEntries(d).GetEnumerator())
                        if (!e.MoveNext()) Directory.Delete(d);
                }
                catch (Exception) { }
        }
        catch (Exception) { }
    });

    private static string Header(UnityWebRequest req, string name)
    {
        var all = req.GetResponseHeaders();   // HTTP/2 는 머리글 이름이 소문자로 온다
        if (all != null)
            foreach (var kv in all)
                if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) return kv.Value;
        return null;
    }

    // Cache-Control 의 보관 기간(초) — no-store·no-cache·없음이면 0 (기기에 두지 않는다)
    private static int MaxAge(string cc)
    {
        if (string.IsNullOrEmpty(cc)) return 0;
        cc = cc.ToLowerInvariant();
        if (cc.Contains("no-store") || cc.Contains("no-cache")) return 0;
        int i = cc.IndexOf("max-age=", StringComparison.Ordinal);
        if (i < 0) return 0;
        int sec = 0;
        for (int j = i + 8; j < cc.Length && j < i + 17 && cc[j] >= '0' && cc[j] <= '9'; j++) sec = sec * 10 + (cc[j] - '0');
        return sec;
    }
}
