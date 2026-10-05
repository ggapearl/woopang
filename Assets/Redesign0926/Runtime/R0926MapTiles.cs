using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 친구 지도 깊은 확대 — 우팡 서버가 내주는 지도 타일(웹 메르카토르 z/x/y PNG)을 세계 그림 위에 겹친다.
/// 앱은 우팡 서버만 부른다 (지도 회사 키·캐시는 서버 몫 — docs/claude/server-spec-1005.md 5).
/// 서버가 /api/map/tiles/info 로 켜져 있다고 답할 때만 타일을 받고, 아니면 아무것도 받지 않는다 (앱에 든 그림만 — 흐리게 확대).
/// 받는 중인 칸은 이미 받은 위 단계 타일을 잘라 채운다. 지도를 닫으면 받은 타일을 버린다 (안 쓰는 동안 메모리 0).
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

    // 지도 좌표가 큰 수가 되면 float 오차로 칸이 어긋난다 — 16 단계(화면 폭 약 2km)까지
    private const int HardMaxLevel = 16;
    private const int MaxVisible = 80;
    private const int ParentLevels = 6;

    [System.Serializable] private class Info { public bool enabled; public int max_zoom; public string attribution; }

    private static Info info;          // 앱이 켜져 있는 동안 한 번 받는다 (못 받으면 10분 뒤 다시)
    private static float infoRetryAt;
    private UnityWebRequest infoReq;

    private readonly Dictionary<long, Texture2D> cache = new Dictionary<long, Texture2D>();
    private readonly Dictionary<long, int> lastUsed = new Dictionary<long, int>();
    private readonly Dictionary<long, float> failedUntil = new Dictionary<long, float>();
    private readonly List<(long key, UnityWebRequest req)> inflight = new List<(long, UnityWebRequest)>();
    private readonly List<(float dist, long key)> wanted = new List<(float, long)>();
    private readonly List<RawImage> pool = new List<RawImage>();
    private readonly HashSet<long> inUse = new HashSet<long>();   // 지금 화면에 쓰는 타일 (위 단계 포함) — 버리지 않는다
    private int failures;
    private float pausedUntil;

    private bool hasView;
    private Vector2 lastOrigin, lastMap, lastView;
    private float lastScale = 1f;

    public bool Available => info != null && info.enabled;
    public int MaxLevel => info != null && info.max_zoom > 0 ? Mathf.Clamp(info.max_zoom, minLevel, HardMaxLevel) : HardMaxLevel;

    /// <summary>이 단계 타일이 화면 픽셀과 1:1 이 되는 지도 확대 배율 (R0926FriendsMap 의 zoom 단위)</summary>
    public float ZoomForLevel(float level, float viewWidth, float scale)
        => tilePixels * Mathf.Pow(2f, level) / Mathf.Max(1f, viewWidth * scale);

    private static long Key(int z, int x, int y) => ((long)z << 48) | ((long)x << 24) | (uint)y;
    private static int KZ(long k) => (int)(k >> 48);
    private static int KX(long k) => (int)((k >> 24) & 0xFFFFFF);
    private static int KY(long k) => (int)(k & 0xFFFFFF);

    private void OnEnable()
    {
        if (info == null && infoReq == null && Time.realtimeSinceStartup >= infoRetryAt)
        {
            infoReq = UnityWebRequest.Get(ApiConfig.MAP_TILES_INFO);
            infoReq.timeout = 8;
            infoReq.SendWebRequest();
        }
    }

    private void OnDisable()
    {
        if (infoReq != null) { infoReq.Abort(); infoReq.Dispose(); infoReq = null; }
        foreach (var f in inflight) { f.req.Abort(); f.req.Dispose(); }
        inflight.Clear();
        foreach (var t in cache.Values) if (t != null) Destroy(t);
        cache.Clear();
        lastUsed.Clear();
        inUse.Clear();
        foreach (var img in pool) if (img != null) { img.texture = null; img.enabled = false; }
        if (attribution != null) attribution.enabled = false;
        hasView = false;
    }

    /// <summary>지도가 움직일 때마다 R0926FriendsMap 이 부른다 — origin: 지도 왼쪽 위(지도 창 왼쪽 위 기준, 아래가 +), map: 지도 전체 크기</summary>
    public void UpdateView(Vector2 origin, Vector2 map, Vector2 view, float scale)
    {
        hasView = true;
        lastOrigin = origin; lastMap = map; lastView = view; lastScale = scale;
        wanted.Clear();
        inUse.Clear();
        int shown = 0;
        if (Available && layer != null && map.x > 1f && map.y > 1f)
        {
            float lv = Mathf.Log(map.x * scale / tilePixels, 2f);
            int z = Mathf.Clamp(Mathf.RoundToInt(lv), 0, MaxLevel);
            if (z >= minLevel)
            {
                int n = 1 << z;
                float tw = map.x / n;
                float a = R0926FriendsMap.WebToNormY(0f);
                float th = map.y * (R0926FriendsMap.WebToNormY(1f) - a) / n;
                float baseY = origin.y + map.y * a;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(-origin.x / tw)), x1 = Mathf.Min(n - 1, Mathf.FloorToInt((view.x - origin.x) / tw));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(-baseY / th)), y1 = Mathf.Min(n - 1, Mathf.FloorToInt((view.y - baseY) / th));
                if (x1 >= x0 && y1 >= y0 && (x1 - x0 + 1) * (y1 - y0 + 1) <= MaxVisible)
                {
                    // 큰 수끼리의 뺄셈은 첫 칸에서 한 번만 — 나머지는 작은 수로 더해 칸 사이에 틈이 생기지 않게
                    float px0 = origin.x + x0 * tw, py0 = baseY + y0 * th;
                    Vector2 center = view / 2f;
                    int frame = Time.frameCount;
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            float px = px0 + (x - x0) * tw, py = py0 + (y - y0) * th;
                            long k = Key(z, x, y);
                            if (Show(shown, k, px, py, tw, th, frame)) shown++;
                            if (!cache.ContainsKey(k))
                                wanted.Add(((new Vector2(px + tw / 2f, py + th / 2f) - center).sqrMagnitude, k));
                        }
                    wanted.Sort((p, q) => p.dist.CompareTo(q.dist));   // 가운데부터 받는다
                }
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

    // 칸 하나 — 받은 타일, 없으면 받아 둔 위 단계 타일을 잘라서. 둘 다 없으면 안 그린다 (아래 세계 그림이 보인다)
    private bool Show(int index, long k, float x, float y, float w, float h, int frame)
    {
        Texture2D tex = null;
        Rect uv = new Rect(0f, 0f, 1f, 1f);
        if (cache.TryGetValue(k, out tex)) { lastUsed[k] = frame; inUse.Add(k); }
        else
        {
            int z = KZ(k), tx = KX(k), ty = KY(k);
            for (int up = 1; up <= ParentLevels && z - up >= minLevel; up++)
            {
                long pk = Key(z - up, tx >> up, ty >> up);
                if (!cache.TryGetValue(pk, out tex)) continue;
                lastUsed[pk] = frame;
                inUse.Add(pk);
                float s = 1f / (1 << up);
                int ox = tx - ((tx >> up) << up), oy = ty - ((ty >> up) << up);
                uv = new Rect(ox * s, 1f - (oy + 1) * s, s, s);   // 그림의 v 는 아래에서 위로
                break;
            }
        }
        if (tex == null) return false;

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
        img.rectTransform.anchoredPosition = new Vector2(x, -y);
        img.rectTransform.sizeDelta = new Vector2(w + 1f, h + 1f);   // 1 겹쳐 칸 사이 실금을 덮는다
        if (!img.enabled) img.enabled = true;
        return true;
    }

    private void Update()
    {
        if (infoReq != null && infoReq.isDone)
        {
            Info got = null;
            if (infoReq.result == UnityWebRequest.Result.Success)
                try { got = JsonUtility.FromJson<Info>(infoReq.downloadHandler.text); } catch (System.Exception) { }
            infoReq.Dispose();
            infoReq = null;
            if (got != null) info = got;
            else infoRetryAt = Time.realtimeSinceStartup + 600f;   // 서버가 아직 타일을 안 준다 — 10분 뒤 다시
            if (Available && hasView) UpdateView(lastOrigin, lastMap, lastView, lastScale);
        }
        if (!Available) return;

        bool changed = false;
        for (int i = inflight.Count - 1; i >= 0; i--)
        {
            var (k, req) = inflight[i];
            if (!req.isDone) continue;
            inflight.RemoveAt(i);
            if (req.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(req);
                if (tex != null)
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    cache[k] = tex;
                    lastUsed[k] = Time.frameCount;
                    failures = 0;
                    changed = true;
                }
            }
            else
            {
                failedUntil[k] = Time.realtimeSinceStartup + 30f;
                if (++failures >= 8) { pausedUntil = Time.realtimeSinceStartup + 60f; failures = 0; }   // 서버가 힘들다 — 잠깐 쉰다
            }
            req.Dispose();
        }
        if (changed) { Trim(); if (hasView) UpdateView(lastOrigin, lastMap, lastView, lastScale); }

        if (Time.realtimeSinceStartup < pausedUntil) return;
        foreach (var w in wanted)
        {
            if (inflight.Count >= maxRequests) break;
            long k = w.key;
            if (cache.ContainsKey(k) || IsInflight(k)) continue;
            if (failedUntil.TryGetValue(k, out float until) && Time.realtimeSinceStartup < until) continue;
            string url = ApiConfig.MAP_TILES.Replace("{z}", KZ(k).ToString()).Replace("{x}", KX(k).ToString()).Replace("{y}", KY(k).ToString());
            var req = UnityWebRequestTexture.GetTexture(url, true);   // 읽기 전용 — CPU 쪽 사본을 남기지 않는다
            req.timeout = 15;
            LoginManager.ApplyAuth(req);
            req.SendWebRequest();
            inflight.Add((k, req));
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
        }
    }
}
