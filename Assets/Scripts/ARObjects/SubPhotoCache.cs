using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 상세 사진(두 번 눌러 여는 정보 창) 디스크 캐시 — 11-17 대표님 「더블터치해서 사진발생하는 건 진짜 중요한 거라서 바로바로 발생해줘야」.
/// 창(화살표·애견동반 등)은 바로 뜨는데 사진이 늦던 까닭: 두 번 누른 그때부터 한 장씩 받았고, 옛 사진은 원본이 4169×5906(1.3MB)이라
/// 받고 푸는 데 오래 걸렸으며(장당 메모리 약 100MB), 다른 장소를 열었다 오면 같은 사진도 다시 받았다.
///  · 가까운 오브젝트(앞쪽 먼저)의 첫 장을 미리 디스크에 받아 둔다 — 메모리에는 풀지 않는다 (10-23 아이폰 꺼짐: 주변 사진을 미리 다 풀어 뒀다)
///  · 한 번 누르면(두 번 누르기의 첫 번째) 그 장소 앞 사진부터 받기 시작한다
///  · 열 때는 디스크에서 풀어 바로 띄우고, 없으면 받으면서 남긴다 — 첫 장이 혼자 먼저, 나머지는 3장씩 함께
///  · 우팡 서버 사진은 /thumb/1080/ (원본이 그보다 작으면 서버가 원본을 그대로 준다) — 안 되면 원본
///  · 미리 받고 열지 않은 것은 1시간, 열어 본 것은 마지막으로 본 뒤 3일 지나면 지운다. 합계 80MB 를 넘으면 오래된 것부터
///  · 11-19 P1 — 장소를 띄우기 전에 그 장소의 상세 사진을 전부 받는다 (PlaceSpawnGate · Spawn 순위: 정보 창 사진 다음, 미리 받기보다 먼저)
/// </summary>
public static class SubPhotoCache
{
    public enum Prio { Prefetch, Spawn, Normal, First }

    private const int ThumbSize = 1080;
    private const int MaxFirst = 2, MaxNormal = 3, MaxSpawn = 4, MaxPrefetch = 2;   // 동시에 받는 수
    private const int PrefetchNearest = 8;            // 가까운 오브젝트 몇 곳의 첫 장을 받아 둘지
    private const float ScanInterval = 2f;
    private const long PrefetchBudget = 30L << 20;    // 앱을 켠 뒤 미리 받기로 쓰는 데이터 상한
    private const long CacheCap = 80L << 20;
    private const float SweepInterval = 600f;
    private const float RetryAfterFail = 300f;        // 못 받은 사진은 5분 동안 미리 받지 않는다
    private static readonly TimeSpan PreTtl = TimeSpan.FromHours(1), SeenTtl = TimeSpan.FromDays(3), TmpTtl = TimeSpan.FromMinutes(10);

    private class Job
    {
        public string key, primary, fallback;
        public Prio prio, pool;
        public int order;
        public bool done, ok, diskFailed;
    }

    private static readonly Dictionary<string, Job> jobs = new Dictionary<string, Job>();
    private static readonly List<Job> waiting = new List<Job>();
    private static readonly Dictionary<string, float> failedAt = new Dictionary<string, float>();
    private static readonly List<KeyValuePair<float, string>> picks = new List<KeyValuePair<float, string>>();
    private static readonly Comparison<Job> ByPrio = (a, b) => a.prio != b.prio ? ((int)b.prio).CompareTo((int)a.prio) : a.order.CompareTo(b.order);
    private static readonly Comparison<KeyValuePair<float, string>> ByDist = (a, b) => a.Key.CompareTo(b.Key);
    private static int runFirst, runNormal, runSpawn, runPrefetch, orderSeq;
    private static int firstDecoding;   // 첫 장을 푸는 중 — 다른 칸은 기다린다 (큰 사진끼리 동시에 풀면 첫 장이 늦다)
    private static long prefetchBytes;
    private static float nextScan, nextSweep;
    private static volatile bool sweeping;
    private static string root;
    private static Runner runner;

    /// <summary>열 때 디스크에 있던 사진 · 없어서 받은 사진 · 미리 받아 둔 사진 수 (앱을 켠 뒤로) — 시험용</summary>
    public static int OpenHits { get; private set; }
    public static int OpenMisses { get; private set; }
    public static int Prefetched { get; private set; }
#if UNITY_EDITOR
    /// <summary>시험용 — 네트워크로 받을 때마다 이만큼 늦춘다 (휴대폰 LTE 흉내)</summary>
    public static float SimulatedNetDelay;
    /// <summary>시험용 — 상대 주소 사진을 받을 서버 (비면 MAIN_SERVER)</summary>
    public static string EditorBase;
#endif

    // 풀어 준 사진 → 그 주소 (번역 버튼이 화면 대신 사진 파일을 저장한다 — 11-26 21번). 지워진 사진은 쌓이면 걷어 낸다
    private static readonly Dictionary<Texture2D, string> sources = new Dictionary<Texture2D, string>();
    private static readonly List<Texture2D> gone = new List<Texture2D>();
    private const int SourcesSweepAt = 128;

    private static string PreDir => root + "/pre";      // 미리 받고 아직 안 연 것
    private static string SeenDir => root + "/seen";    // 열어 본 것

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        jobs.Clear(); waiting.Clear(); failedAt.Clear(); picks.Clear(); sources.Clear(); gone.Clear();
        runFirst = runNormal = runSpawn = runPrefetch = orderSeq = firstDecoding = 0;
        prefetchBytes = 0;
        nextScan = nextSweep = 0f;
        root = null; runner = null;
        OpenHits = OpenMisses = Prefetched = 0;
#if UNITY_EDITOR
        SimulatedNetDelay = 0f;
        EditorBase = null;
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (runner != null) return;
        root = Path.Combine(Application.temporaryCachePath, "subphotos").Replace('\\', '/');
        var go = new GameObject("SubPhotoCache") { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
        nextSweep = Time.realtimeSinceStartup + 10f;   // 켜지는 동안은 미룬다
    }

    /// <summary>디스크로만 미리 받는다 (이미 있으면 그만, 받는 중이면 순서만 앞당긴다)</summary>
    public static void Prefetch(string src, Prio prio)
    {
        if (string.IsNullOrEmpty(src)) return;
        Boot();
        string norm = Norm(src), key = Key(norm);
        if (Find(key) == null) Enqueue(key, norm, prio);
    }

    /// <summary>디스크에 받아 둔 사진인가</summary>
    public static bool Has(string src)
    {
        if (string.IsNullOrEmpty(src)) return false;
        Boot();
        return Find(Key(Norm(src))) != null;
    }

    /// <summary>받으려고 줄 서 있거나 받는 중인가 (PlaceSpawnGate 가 다 받을 때까지 기다린다)</summary>
    public static bool Busy(string src) => !string.IsNullOrEmpty(src) && jobs.ContainsKey(Key(Norm(src)));

    /// <summary>정보 창에 띄울 사진 — 디스크에 있으면 풀고, 없으면 받아서(남기고) 푼다. 못 하면 null. done 은 꼭 한 번 불린다</summary>
    public static void Load(string src, Prio prio, Action<Texture2D> done)
    {
        Boot();
        runner.StartCoroutine(LoadRoutine(src ?? "", prio, done));
    }

    private static IEnumerator LoadRoutine(string src, Prio prio, Action<Texture2D> done)
    {
        string norm = Norm(src), key = Key(norm);
        string path = Find(key);
        Job job = null;
        if (path != null) OpenHits++;
        else
        {
            OpenMisses++;
            job = Enqueue(key, norm, prio);
            while (!job.done) yield return null;
            path = job.ok ? Find(key) : null;
        }

        bool first = prio == Prio.First;
        if (first) firstDecoding++;
        else while (firstDecoding > 0) yield return null;

        Texture2D tex = null;
        if (path != null)
        {
            path = MarkSeen(key, path);
            yield return Decode(new Uri(path).AbsoluteUri, t => tex = t);
            if (tex == null) TryDelete(path);   // 깨진 파일 — 다음엔 다시 받는다
        }
        if (tex == null && (path != null || job != null && job.diskFailed))
        {
            // 디스크로는 안 됐다(공간 부족 · 풀 수 없는 파일) — 예전처럼 바로 받아 푼다
            Urls(norm, out var primary, out var fallback);
            yield return Decode(primary, t => tex = t);
            if (tex == null && fallback != null) yield return Decode(fallback, t => tex = t);
        }
        if (first) firstDecoding--;
        if (tex != null) Remember(tex, norm);
        done(tex);
    }

    private static void Remember(Texture2D tex, string norm)
    {
        if (sources.Count >= SourcesSweepAt)
        {
            foreach (var k in sources.Keys) if (k == null) gone.Add(k);   // 정보 창이 놓아준(지운) 사진
            foreach (var k in gone) sources.Remove(k);
            gone.Clear();
        }
        sources[tex] = norm;
    }

    /// <summary>
    /// 이 캐시가 풀어 준 사진의 원본 주소와 디스크에 받아 둔 파일 (번역 버튼 — 화면 대신 사진만 저장, 11-26 21번).
    /// 우팡 서버 사진은 창에 띄운 줄인 것(/thumb/1080/)이 아니라 원본 주소. 받아 둔 파일이 없으면 cached 는 null. 모르는 그림이면 false
    /// </summary>
    public static bool Source(Texture tex, out string original, out string cached)
    {
        original = cached = null;
        var t = tex as Texture2D;
        if (t == null || !sources.TryGetValue(t, out var norm)) return false;
        Urls(norm, out var primary, out var fallback);
        original = fallback ?? primary;
        cached = Find(Key(norm));
        return true;
    }

    private static IEnumerator Decode(string url, Action<Texture2D> got)
    {
        UnityWebRequest rq;
        try   // 깨진 주소 등으로 예외가 나도 이 사진만 건너뛴다
        {
            rq = UnityWebRequestTexture.GetTexture(url, true);   // 읽기 전용 — CPU 쪽 사본을 남기지 않는다
            rq.timeout = 15;
        }
        catch (Exception e) { Debug.LogError($"[SubPhotoCache] 사진 요청 실패: {e.Message} ({url})"); yield break; }

        using (rq)
        {
            yield return rq.SendWebRequest();
            if (rq.result != UnityWebRequest.Result.Success) yield break;
            Texture2D t = null;
            try { t = DownloadHandlerTexture.GetContent(rq); } catch (Exception) { }
            got(t);
        }
    }

    private static Job Enqueue(string key, string norm, Prio prio)
    {
        if (jobs.TryGetValue(key, out var j))
        {
            if (prio > j.prio) { j.prio = prio; Pump(); }
            return j;
        }
        Urls(norm, out var primary, out var fallback);
        j = new Job { key = key, primary = primary, fallback = fallback, prio = prio, order = ++orderSeq };
        jobs[key] = j;
        waiting.Add(j);
        Pump();
        return j;
    }

    // 첫 장은 바로 · 나머지는 첫 장을 받는 동안 기다린다(첫 장이 혼자 빨리 오게) · 띄우기(Spawn)는 정보 창 사진을 받을 게 없을 때 ·
    // 미리 받기는 정보 창 사진도 띄우기 사진도 받을 게 없을 때만
    private static void Pump()
    {
        if (waiting.Count == 0) return;
        waiting.Sort(ByPrio);
        Prio top = waiting[0].prio;
        for (int i = 0; i < waiting.Count; i++)
        {
            var j = waiting[i];
            bool go = j.prio == Prio.First ? runFirst < MaxFirst
                    : j.prio == Prio.Normal ? runFirst == 0 && runNormal < MaxNormal
                    : j.prio == Prio.Spawn ? top <= Prio.Spawn && runFirst == 0 && runNormal == 0 && runSpawn < MaxSpawn
                    : top == Prio.Prefetch && runFirst == 0 && runNormal == 0 && runSpawn == 0 && runPrefetch < MaxPrefetch;
            if (!go) continue;
            waiting.RemoveAt(i--);
            j.pool = j.prio;
            if (j.pool == Prio.First) runFirst++; else if (j.pool == Prio.Normal) runNormal++; else if (j.pool == Prio.Spawn) runSpawn++; else runPrefetch++;
            runner.StartCoroutine(Run(j));
        }
    }

    private static IEnumerator Run(Job j)
    {
#if UNITY_EDITOR
        if (SimulatedNetDelay > 0f) yield return new WaitForSecondsRealtime(SimulatedNetDelay);
#endif
        string final = PreDir + "/" + j.key;
        foreach (string url in new[] { j.primary, j.fallback })
        {
            if (url == null || j.ok || j.diskFailed) continue;
            string tmp = final + "." + j.order + ".tmp";
            UnityWebRequest rq = null;
            try
            {
                Directory.CreateDirectory(PreDir);
                rq = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET, new DownloadHandlerFile(tmp) { removeFileOnAbort = true }, null) { timeout = 15 };
            }
            catch (Exception) { rq?.Dispose(); rq = null; j.diskFailed = true; }
            if (rq == null) break;

            bool good;
            ulong bytes;
            using (rq)
            {
                yield return rq.SendWebRequest();
                string type = rq.GetResponseHeader("Content-Type") ?? "";
                good = rq.result == UnityWebRequest.Result.Success && !type.StartsWith("text/", StringComparison.OrdinalIgnoreCase) && type.IndexOf("json", StringComparison.OrdinalIgnoreCase) < 0;
                if (rq.result == UnityWebRequest.Result.DataProcessingError) j.diskFailed = true;   // 파일에 못 썼다
                bytes = rq.downloadedBytes;
            }
            if (good && bytes > 0)
            {
                try
                {
                    if (File.Exists(final)) File.Delete(final);
                    File.Move(tmp, final);
                    j.ok = true;
                    if (j.pool == Prio.Prefetch) { prefetchBytes += (long)bytes; Prefetched++; }
                }
                catch (Exception) { j.diskFailed = true; }
            }
            TryDelete(tmp);
        }

        j.done = true;
        if (j.pool == Prio.First) runFirst--; else if (j.pool == Prio.Normal) runNormal--; else if (j.pool == Prio.Spawn) runSpawn--; else runPrefetch--;
        jobs.Remove(j.key);
        if (j.ok) failedAt.Remove(j.key); else failedAt[j.key] = Time.realtimeSinceStartup;
        Pump();
    }

    // 미리 받은 것을 열었다 — 열어 본 쪽으로 옮기고 본 시각을 남긴다 (3일 뒤 지움)
    private static string MarkSeen(string key, string path)
    {
        string seen = SeenDir + "/" + key;
        try
        {
            if (path != seen)
            {
                Directory.CreateDirectory(SeenDir);
                if (File.Exists(seen)) File.Delete(seen);
                File.Move(path, seen);
            }
            File.SetLastWriteTimeUtc(seen, DateTime.UtcNow);
            return seen;
        }
        catch (Exception) { return File.Exists(seen) ? seen : path; }
    }

    private static void Tick()
    {
        float now = Time.realtimeSinceStartup;
        if (now >= nextSweep) { nextSweep = now + SweepInterval; StartSweep(); }
        if (now < nextScan) return;
        nextScan = now + ScanInterval;
        if (prefetchBytes >= PrefetchBudget || BootOverlay.Showing || Application.internetReachability == NetworkReachability.NotReachable) return;
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 cp = cam.transform.position, fw = cam.transform.forward;
        picks.Clear();
        foreach (var d in DoubleTap3D.Live)
        {
            string src = d != null ? d.PrefetchSrc : null;
            if (src == null) continue;
            Vector3 v = d.transform.position - cp;
            float s = v.sqrMagnitude;
            if (Vector3.Dot(fw, v) < 0f) s *= 4f;   // 뒤쪽은 두 배 먼 것으로 친다 — 앞에 보이는 것부터
            picks.Add(new KeyValuePair<float, string>(s, src));
        }
        picks.Sort(ByDist);
        for (int i = 0; i < picks.Count && i < PrefetchNearest; i++)
        {
            string norm = Norm(picks[i].Value), key = Key(norm);
            if (jobs.ContainsKey(key) || Find(key) != null) continue;
            if (failedAt.TryGetValue(key, out float f) && now - f < RetryAfterFail) continue;
            Enqueue(key, norm, Prio.Prefetch);
        }
    }

    private static void StartSweep()
    {
        if (sweeping || root == null) return;
        sweeping = true;
        string r = root;
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try { Sweep(r, DateTime.UtcNow, CacheCap); }
            catch (Exception) { }
            finally { sweeping = false; }
        });
    }

    // 뒤 스레드에서 돈다 — 유니티 API 를 쓰지 않는다
    private static void Sweep(string r, DateTime now, long cap)
    {
        var keep = new List<FileInfo>();
        long total = 0;
        foreach (var (dir, ttl) in new[] { (r + "/pre", PreTtl), (r + "/seen", SeenTtl) })
        {
            var di = new DirectoryInfo(dir);
            if (!di.Exists) continue;
            foreach (var f in di.GetFiles())
            {
                bool tmp = f.Name.EndsWith(".tmp", StringComparison.Ordinal);
                if (now - f.LastWriteTimeUtc > (tmp ? TmpTtl : ttl)) { TryDelete(f.FullName); continue; }
                if (tmp) continue;
                keep.Add(f);
                total += f.Length;
            }
        }
        if (total <= cap) return;
        keep.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
        foreach (var f in keep)
        {
            if (total <= cap / 4 * 3) break;
            total -= f.Length;
            TryDelete(f.FullName);
        }
    }

    private static string Norm(string src) => src.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? src : src.Replace('\\', '/').TrimStart('/');

    private static void Urls(string norm, out string primary, out string fallback)
    {
        if (norm.StartsWith("http", StringComparison.OrdinalIgnoreCase)) { primary = ApiConfig.Secure(norm); fallback = null; return; }
        string server = ApiConfig.MAIN_SERVER;
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(EditorBase)) server = EditorBase;
#endif
        primary = server + "/thumb/" + ThumbSize + "/" + norm;
        fallback = server + "/" + norm;
    }

    // 주소 → 파일 이름 (FNV-1a 64 + 길이)
    private static string Key(string norm)
    {
        ulong h = 14695981039346656037UL;
        foreach (char c in norm) { h ^= c; h *= 1099511628211UL; }
        return h.ToString("x16") + norm.Length.ToString("x");
    }

    private static string Find(string key)
    {
        if (root == null) return null;
        string p = SeenDir + "/" + key;
        if (File.Exists(p)) return p;
        p = PreDir + "/" + key;
        return File.Exists(p) ? p : null;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
    }

    private class Runner : MonoBehaviour
    {
        private void Update() => Tick();
    }
}
