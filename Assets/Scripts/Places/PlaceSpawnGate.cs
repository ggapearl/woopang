using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 장소 오브젝트를 띄우기 전에 사진부터 — 11-19 대표님 (P1)
/// 「사진이 없이 오브젝트 먼저(좌표값 먼저 불러와서) 기본 프리팹으로 만드는 경우가 있는데 이렇게 하지 말고,
///  메인사진 · 서브사진까지 모두 불러오고 나서 완료가 되면 오브젝트를 생성하는 방식으로」
///  · 메인 사진(큐브 앞면)은 메모리로 받아 둔다 — 매니저가 만들 때 Take 로 넘겨받아 바로 붙인다 (기본 면이 보이는 틈이 없다)
///  · 상세 사진(두 번 눌러 여는 창)은 전부 디스크로 받아 둔다 (SubPhotoCache · Spawn 순위) — 창을 열면 디스크에서 바로 뜬다
///  · 한 번에 3곳씩, 부른 순서(가까운 것부터)대로. 시작하기 직전에 아직 띄울 곳인지(wanted) 다시 본다
///  · 한 곳에 10초까지 — 넘으면 받은 것만으로 띄운다 (남은 상세 사진은 뒤에서 계속 받는다)
///  · 메인 사진이 없거나(404) 못 받으면 더 기다리지 않는다 — 매니저가 예전처럼(분류 그림 · 기본 면 + 뒤에서 다시 받기) 띄운다
///  · 다 되면 ready(key) 를 한 번 부른다. 받아 둔 사진을 30초 안에 가져가지 않으면 지운다
/// 키: "dm_번호" (DataManager) · "tour_번호" (TourAPIManager) — FilterManager 와 같은 이름
/// </summary>
public static class PlaceSpawnGate
{
    private const int MaxActive = 3;          // 동시에 준비하는 곳
    private const float Deadline = 10f;       // 한 곳에 기다리는 최대 시간 (그 곳을 시작한 때부터)
    private const int MainTimeout = 8;        // 메인 사진 한 번 받기
    private const float KeepReady = 30f;      // 준비를 마치고 가져가지 않은 사진을 지우기까지

    private sealed class Entry
    {
        public string key, main, texSrc;
        public List<string> subs;
        public Func<bool> wanted;
        public Action<string> ready;
        public bool started, done, cancelled;
        public Texture2D tex;
        public float doneAt;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    private static readonly List<Entry> queue = new List<Entry>();
    private static readonly List<string> expired = new List<string>();
    private static int active;
    private static Runner runner;

    /// <summary>준비를 마친 곳 수 (앱을 켠 뒤로) — 시험용</summary>
    public static int Completed { get; private set; }
#if UNITY_EDITOR
    /// <summary>시험용 — 곳마다 메인 사진을 받기 전에 이만큼 늦춘다 (느린 회선 흉내)</summary>
    public static float SimulatedDelay;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        entries.Clear(); queue.Clear(); expired.Clear();
        active = 0; runner = null; Completed = 0;
#if UNITY_EDITOR
        SimulatedDelay = 0f;
#endif
    }

    private static void Boot()
    {
        if (runner != null) return;
        var go = new GameObject("PlaceSpawnGate") { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    /// <summary>사진을 다 받아 띄울 준비가 됐는가</summary>
    public static bool Ready(string key) => key != null && entries.TryGetValue(key, out var e) && e.done;

    /// <summary>준비 중인가 (줄 서 있거나 받는 중)</summary>
    public static bool Busy(string key) => key != null && entries.TryGetValue(key, out var e) && !e.done;

    /// <summary>
    /// 사진 준비를 줄 세운다 — 이미 있으면 그대로. main 은 큐브 앞면 사진(없으면 null) · subs 는 상세 사진 전부.
    /// wanted 는 시작하기 직전에 본다(빠졌으면 버린다) · ready 는 다 되면 한 번
    /// </summary>
    public static void Prepare(string key, string main, IList<string> subs, Func<bool> wanted, Action<string> ready)
    {
        if (string.IsNullOrEmpty(key) || entries.ContainsKey(key)) return;
        Boot();
        var e = new Entry { key = key, main = string.IsNullOrEmpty(main) ? null : main, subs = new List<string>(), wanted = wanted, ready = ready };
        if (subs != null)
            foreach (var s in subs)
                if (!string.IsNullOrEmpty(s) && !e.subs.Contains(s)) e.subs.Add(s);
        entries[key] = e;
        queue.Add(e);
        Pump();
    }

    /// <summary>받아 둔 메인 사진을 넘겨받는다 — 이제 부른 쪽이 갖는다(붙이거나 지운다). 준비가 끝나지 않았으면 null (준비는 이어 간다)</summary>
    public static Texture2D Take(string key, out string src)
    {
        src = null;
        if (key == null || !entries.TryGetValue(key, out var e) || !e.done) return null;
        entries.Remove(key);
        src = e.texSrc;
        Texture2D t = e.tex;
        e.tex = null;
        return t;
    }

    /// <summary>버린다 (받아 둔 사진도 지운다) — 띄우지 않기로 했을 때</summary>
    public static void Drop(string key)
    {
        if (key == null || !entries.TryGetValue(key, out var e)) return;
        entries.Remove(key);
        queue.Remove(e);
        e.cancelled = true;
        if (e.tex != null) { UnityEngine.Object.Destroy(e.tex); e.tex = null; }
    }

    private static void Pump()
    {
        for (int i = 0; i < queue.Count && active < MaxActive; i++)
        {
            Entry e = queue[i];
            queue.RemoveAt(i--);
            bool want;
            try { want = e.wanted == null || e.wanted(); }
            catch (Exception) { want = false; }
            if (!want) { entries.Remove(e.key); continue; }   // 기다리는 사이 멀어졌다 · 다른 길로 떴다 — 다시 부르면 다시 준비한다
            e.started = true;
            active++;
            runner.StartCoroutine(Run(e));
        }
    }

    private static IEnumerator Run(Entry e)
    {
        float until = Time.realtimeSinceStartup + Deadline;
        foreach (string s in e.subs) SubPhotoCache.Prefetch(s, SubPhotoCache.Prio.Spawn);   // 디스크에 있으면 그만
#if UNITY_EDITOR
        if (SimulatedDelay > 0f) yield return new WaitForSecondsRealtime(SimulatedDelay);
#endif
        if (e.main != null && !e.cancelled)
        {
            ImageDisplayController.MainPhotoUrls(e.main, out string primary, out string fallback);
            foreach (string url in new[] { primary, fallback })
            {
                if (url == null || e.tex != null || e.cancelled) continue;
                float left = until - Time.realtimeSinceStartup;
                if (left < 0.5f) break;
                UnityWebRequest rq = null;
                UnityWebRequestAsyncOperation op = null;
                try
                {
                    rq = UnityWebRequestTexture.GetTexture(url, true);   // 읽기 전용 — CPU 쪽 사본을 남기지 않는다 (ImageDisplayController 와 같다)
                    rq.timeout = Mathf.Clamp(Mathf.CeilToInt(left), 1, MainTimeout);
                    op = rq.SendWebRequest();
                }
                catch (Exception) { op = null; }
                if (rq == null) continue;
                using (rq)
                {
                    if (op != null) yield return op;
                    if (op != null && rq.result == UnityWebRequest.Result.Success && !e.cancelled)
                    {
                        Texture2D t = null;
                        try { t = DownloadHandlerTexture.GetContent(rq); } catch (Exception) { }
                        if (t != null) { e.tex = t; e.texSrc = e.main; }
                    }
                }
            }
        }

        // 상세 사진 — 전부 디스크에 받았거나(못 받은 것은 끝났거나) 10초가 될 때까지
        while (!e.cancelled && Time.realtimeSinceStartup < until && AnySubBusy(e.subs)) yield return null;

        active--;
        if (e.cancelled)
        {
            if (e.tex != null) { UnityEngine.Object.Destroy(e.tex); e.tex = null; }
            Pump();
            yield break;
        }
        e.done = true;
        e.doneAt = Time.realtimeSinceStartup;
        Completed++;
        Pump();
        try { e.ready?.Invoke(e.key); }
        catch (Exception ex) { Debug.LogError($"[PlaceSpawnGate] 띄우기 예외: {e.key}: {ex.Message}"); }
    }

    private static bool AnySubBusy(List<string> subs)
    {
        for (int i = 0; i < subs.Count; i++)
            if (SubPhotoCache.Busy(subs[i])) return true;
        return false;
    }

    private static void Expire()
    {
        float now = Time.realtimeSinceStartup;
        expired.Clear();
        foreach (var kv in entries)
            if (kv.Value.done && now - kv.Value.doneAt > KeepReady) expired.Add(kv.Key);
        foreach (string k in expired) Drop(k);
    }

    private sealed class Runner : MonoBehaviour
    {
        private float next;

        private void Update()
        {
            if (Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + 1f;
            Expire();
        }
    }
}
