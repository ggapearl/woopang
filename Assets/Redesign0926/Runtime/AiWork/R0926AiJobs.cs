using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// AI 작업 보관함 (11-26 대표님 4번 「…해당 창을 닫거나 앱이 꺼지더라도 해당 요청한 3D모델링 작업이 잃어버리지 않고 가지고 있을 수 있록 glb 로 저장되도록 하거나
/// 프로필에서 확인이 되어서 서버에서 가지고 있어야할 것 같거든? 해당 로직 지금 API연결하는 건 아니지만 미리 잘 정리해줘」).
/// 외부 3D 제작 AI 는 한 번에 몇 분 걸린다 — 서버가 작업을 맡아 끝까지 만들어 가지고 있고, 폰은 맡긴 것을 적어 두었다가 다 되면 GLB 를 받아 둔다.
///  · 상태가 바뀔 때마다 폰 저장소(ai_jobs/jobs.json)에 적는다 — 창을 닫거나 앱이 꺼져도 다음에 켜면 이어서 확인한다
///  · 폰이 정한 번호(id)를 서버에 같이 보낸다(Idempotency-Key) — 끊겨서 다시 보내도 서버는 한 번만 만든다(돈이 두 번 나가지 않게)
///  · 앱을 다시 깔거나 폰을 바꾸면 로그인할 때 서버의 「내 작업」 목록으로 채운다 · 계정마다 따로 보인다
///  · 지우기 · 그만두기는 서버에 알릴 때까지 다시 보낸다 — 목록 복구로 되살아나지 않게
///  · Base(서버 주소)가 비어 있으면 아무것도 하지 않는다 — 지금의 폰 안 무료 작업(R0926AiWork)은 그대로.
/// 서버가 돌려줄 작업 한 건(JSON): id · client_id · kind · style · request · place · state(queued|running|done|failed|canceled) · progress(0~100)
///  · position(앞 순서) · stage(단계 말) · label · error · glb_url · thumb_url(/ 로 시작하면 Base 기준) · created · expires(유닉스 초). 목록은 { "jobs": [...] }.
/// 설계 · 화면(단계 스피너 · 프로필 「내 AI 작업」): C:\woopang-app\mockups\우팡_AI작업보관_설계_1127.html
/// </summary>
public static class R0926AiJobs
{
    public enum State { Sending, Queued, Running, Done, Failed, Canceled }

    [Serializable]
    public sealed class Entry
    {
        public string id;          // 폰이 정한 번호 (서버에 Idempotency-Key 로)
        public string serverId;
        public string owner;       // 맡긴 계정 — 다른 계정으로 로그인하면 보이지 않는다
        public string kind;        // "character" · "photo"
        public string style;       // R0926AiWork.Style 이름
        public string request;
        public string place;
        public State state;
        public int progress;       // 0 ~ 100
        public int position;       // 앞 순서 (기다리는 동안)
        public string stage;       // 서버가 준 단계 말 — 「3D 모양을 만드는 중」
        public string label;
        public string error;
        public string glbUrl;
        public string thumbUrl;
        public bool hasSrc, hasGlb, hasThumb;   // 폰에 둔 파일 — 이름은 번호로 정해져 있다 (SrcPath · GlbPath · ThumbPath)
        public bool removed;                    // 지웠다 — 서버에 알릴 때까지만 남는다 (화면에는 안 보임)
        public bool cancelPending;              // 그만뒀다 — 서버에 알릴 때까지
        public long created, updated, expires;  // 유닉스 초 (expires — 서버가 정리하는 때 · 「○일 뒤 정리」)
        public bool seen;                       // 완성을 본 적 있다 (프로필 줄 점)
        public int tries;                       // 연달아 실패한 보내기 · 받기
        [NonSerialized] public float nextTry;   // 다음 시도 (realtimeSinceStartup)

        public bool Working => state == State.Sending || state == State.Queued || state == State.Running;
        public bool NeedsFetch => state == State.Done && !hasGlb && !string.IsNullOrEmpty(glbUrl) && tries < MaxFetchTries;
    }

    [Serializable]
    private sealed class Store { public List<Entry> jobs = new List<Entry>(); }

    [Serializable]
    private sealed class Dto
    {
        public string id, client_id, kind, style, request, place, state, stage, label, error, glb_url, thumb_url;
        public int progress, position;
        public long created, expires;
    }

    [Serializable]
    private sealed class ListDto { public Dto[] jobs; }

    /// <summary>서버 주소 (끝 / 없이) — 비어 있으면 끔</summary>
    public static string Base;

    /// <summary>작업 하나가 바뀌었다 (메인 스레드) — 「AI 작업」 창의 단계 스피너 · 프로필 「내 AI 작업」이 듣는다. 지운 것도 알린다(Find 가 null)</summary>
    public static event Action<Entry> Changed;

    public static bool On => !string.IsNullOrEmpty(Base);

    private const float PollEvery = 3f, MaxBackoff = 60f;
    private const int MaxFetchTries = 8, MaxSyncTries = 5;
    private const long KeepEndedSeconds = 30L * 24 * 3600;   // 실패 · 그만둔 기록은 30일 뒤 폰에서도 정리
    private static readonly List<Entry> jobs = new List<Entry>();
    private static bool loaded, synced;
    private static int syncTries;
    private static float syncNext;
    private static Runner runner;

#if UNITY_EDITOR
    /// <summary>플레이 시험 — 저장 폴더를 바꾼다 (null 이면 persistentDataPath/ai_jobs)</summary>
    public static string EditorDir;
    public static float EditorPollEvery = -1f;

    /// <summary>플레이 시험 — 「앱을 껐다 켬」: 메모리를 비우고 폰 저장소에서 다시 읽어 이어 간다</summary>
    public static void EditorReload()
    {
        if (runner != null) UnityEngine.Object.DestroyImmediate(runner.gameObject);   // 바로 — 지난 일꾼이 이 프레임에 기록을 다시 쓰지 않게
        runner = null;
        jobs.Clear();
        loaded = synced = false;
        syncTries = 0;
        syncNext = 0f;
        Boot();
    }
#endif

    private static string Dir
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorDir)) return EditorDir;
#endif
            return Path.Combine(Application.persistentDataPath, "ai_jobs");
        }
    }

    private static string StorePath => Path.Combine(Dir, "jobs.json");
    public static string SrcPath(Entry e) => e != null && e.hasSrc ? Path.Combine(Dir, e.id + ".src.png") : null;
    public static string GlbPath(Entry e) => e != null && e.hasGlb ? Path.Combine(Dir, e.id + ".glb") : null;
    public static string ThumbPath(Entry e) => e != null && e.hasThumb ? Path.Combine(Dir, e.id + ".png") : null;

    private static string Me()
    {
        var lm = LoginManager.Instance;
        return lm != null && lm.IsLoggedIn && lm.CurrentUser != null ? lm.CurrentUser.id ?? "" : "";
    }

    private static bool Mine(Entry e, string me) => me != "" && e.owner == me;

    /// <summary>지금 계정의 작업 — 최근 것부터 (프로필 「내 AI 작업」)</summary>
    public static List<Entry> All
    {
        get
        {
            Load();
            string me = Me();
            var list = jobs.FindAll(e => !e.removed && Mine(e, me));
            list.Sort((a, b) => b.created.CompareTo(a.created));
            return list;
        }
    }

    public static Entry Find(string id)
    {
        Load();
        string me = Me();
        return jobs.Find(e => e.id == id && !e.removed && Mine(e, me));
    }

    /// <summary>다 됐는데 아직 안 본 것 — 프로필 줄 점</summary>
    public static int Unseen
    {
        get
        {
            int n = 0;
            foreach (var e in All) if (e.state == State.Done && e.hasGlb && !e.seen) n++;
            return n;
        }
    }

    public static void MarkSeen(string id)
    {
        var e = Find(id);
        if (e == null || e.seen) return;
        e.seen = true;
        Save();
    }

    /// <summary>맡기기 — 기록(사진 포함)을 먼저 폰에 적고 보낸다. 서버 주소가 없거나 로그인 전이면 null (서버가 계정마다 하루 횟수를 센다)</summary>
    public static Entry Submit(byte[] photo, string kind, string style, string request, string place)
    {
        string me = Me();
        if (!On || me == "") return null;
        Load();
        long now = Now();
        var e = new Entry
        {
            id = Guid.NewGuid().ToString("N"), owner = me,
            kind = kind ?? "", style = style ?? "", request = (request ?? "").Trim(), place = place ?? "",
            state = State.Sending, created = now, updated = now,
        };
        if (photo != null && photo.Length > 0 && WriteFile(e.id + ".src.png", photo)) e.hasSrc = true;
        jobs.Add(e);
        Save();
        Changed?.Invoke(e);
        Kick();
        return e;
    }

    /// <summary>다시 — 실패 · 그만둔 것은 같은 사진 · 요청으로 새로 맡기고(새 번호) 앞의 것은 지운다. 다 됐는데 못 받은 것은 받기만 다시</summary>
    public static Entry Retry(string id)
    {
        var e = Find(id);
        if (e == null) return null;
        if (e.state == State.Done && !e.hasGlb)
        {
            e.tries = 0;
            e.nextTry = 0f;
            Save();
            Kick();
            return e;
        }
        if (e.state != State.Failed && e.state != State.Canceled) return e;
        string src = SrcPath(e);
        var n = Submit(src != null && File.Exists(src) ? File.ReadAllBytes(src) : null, e.kind, e.style, e.request, e.place);
        if (n != null) Remove(e.id);
        return n;
    }

    /// <summary>만들기 그만 — 아직 시작 전이면 서버는 비용 없이 접는다</summary>
    public static void Cancel(string id)
    {
        var e = Find(id);
        if (e == null || !e.Working) return;
        e.cancelPending = true;
        e.nextTry = 0f;
        Mark(e, State.Canceled);
        Kick();
    }

    /// <summary>목록에서 지운다 — 폰에 둔 파일도 · 서버에도 알린다 (장소에 쓴 것은 장소 쪽 사본이 남는다)</summary>
    public static void Remove(string id)
    {
        var e = Find(id);
        if (e == null) return;
        foreach (var f in new[] { e.id + ".src.png", e.id + ".glb", e.id + ".png" }) DeleteFile(f);
        e.hasSrc = e.hasGlb = e.hasThumb = false;
        e.removed = true;
        e.nextTry = 0f;
        Save();
        Changed?.Invoke(e);
        Kick();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()   // 플레이를 다시 눌러도(도메인 다시 읽기 꺼짐) 지난 판의 것이 남지 않게
    {
        jobs.Clear();
        loaded = synced = false;
        syncTries = 0;
        syncNext = 0f;
        runner = null;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!On) return;
        Load();
        Kick();   // 끝나지 않은 것 이어서 · 서버 목록과 맞추기
    }

    // ── 폰 저장소 ─────────────────────────────────────────────

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        jobs.Clear();
        string path = StorePath, tmp = path + ".tmp";
        try
        {
            if (!File.Exists(path) && File.Exists(tmp)) File.Move(tmp, path);   // 바꿔 쓰던 중에 꺼졌다
            if (!File.Exists(path)) return;
            var s = JsonUtility.FromJson<Store>(File.ReadAllText(path));
            if (s == null || s.jobs == null) return;
            long old = Now() - KeepEndedSeconds;
            bool pruned = false;
            foreach (var e in s.jobs)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if ((e.state == State.Failed || e.state == State.Canceled) && !e.cancelPending && !e.removed && e.updated < old)
                {
                    foreach (var f in new[] { e.id + ".src.png", e.id + ".glb", e.id + ".png" }) DeleteFile(f);
                    pruned = true;
                    continue;
                }
                jobs.Add(e);
            }
            if (pruned) Save();
        }
        catch (Exception ex)
        {
            Debug.LogError("[R0926AiJobs] 기록을 읽지 못했어요 — 따로 두고 새로 시작: " + ex.Message);
            try { File.Copy(path, path + ".bad", true); } catch { }
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string path = StorePath, tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(new Store { jobs = jobs }));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception ex) { Debug.LogError("[R0926AiJobs] 기록을 쓰지 못했어요: " + ex.Message); }
    }

    private static bool WriteFile(string name, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, name), tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return true;
        }
        catch (Exception ex) { Debug.LogError("[R0926AiJobs] 파일을 쓰지 못했어요: " + ex.Message); return false; }
    }

    private static void DeleteFile(string name)
    {
        try { File.Delete(Path.Combine(Dir, name)); } catch { }
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static void Mark(Entry e, State s)
    {
        e.state = s;
        e.updated = Now();
        Save();
        Changed?.Invoke(e);
    }

    private static void Backoff(Entry e)
    {
        e.tries++;
        e.nextTry = Time.realtimeSinceStartup + Mathf.Min(MaxBackoff, Mathf.Pow(2f, e.tries));
    }

    private static string JobUrl(string serverId) => Base + "/api/ai/jobs/" + UnityWebRequest.EscapeURL(serverId ?? "");

    // ── 서버와 주고받기 ───────────────────────────────────────

    private static void Kick()
    {
        if (!On) return;
        if (runner == null)
        {
            var go = new GameObject("R0926AiJobs") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }
        runner.Wake();
    }

    private static bool HasWork(string me)
    {
        foreach (var e in jobs)
            if (Mine(e, me) && (e.removed || e.cancelPending || e.Working || e.NeedsFetch)) return true;
        return false;
    }

    private sealed class Runner : MonoBehaviour
    {
        private const float DeadAfter = 90f;   // 요청은 길어야 60초 — 이보다 오래 소식이 없으면 멈춘 것
        private Coroutine loop;
        private LoginManager lm;
        private bool kicked;
        private float beat;

        public void Wake()
        {
            kicked = true;   // 맡기기 · 그만두기 · 지우기는 기다리지 않고 바로
            if (loop == null) { beat = Time.realtimeSinceStartup; loop = StartCoroutine(Loop()); }
        }

        // 뜻밖의 오류로 코루틴이 끊기면 다시 띄운다 — 맡긴 작업이 앱을 다시 켤 때까지 멈춰 있지 않게
        private void Update()
        {
            if (loop == null || Time.realtimeSinceStartup - beat < DeadAfter) return;
            StopCoroutine(loop);
            loop = null;
            Debug.LogError("[R0926AiJobs] 작업 확인이 멈춰 다시 시작해요");
            Wake();
        }

        private void Start()
        {
            lm = LoginManager.Instance;
            if (lm != null) lm.OnLoginStateChanged += OnLogin;
        }

        // 로그인(다시 깔고 처음 · 다른 계정) — 그 계정의 서버 목록으로 다시 맞춘다
        private void OnLogin(bool on)
        {
            if (!on) return;
            synced = false;
            syncTries = 0;
            syncNext = 0f;
            Wake();
        }

        private IEnumerator Loop()
        {
            while (On)
            {
                beat = Time.realtimeSinceStartup;
                string me = Me();
                if (!synced && me != "" && syncTries < MaxSyncTries && Time.realtimeSinceStartup >= syncNext) yield return Sync(me);
                if (!HasWork(me) && (synced || me == "" || syncTries >= MaxSyncTries)) break;
                foreach (var e in jobs.ToArray())
                {
                    if (!On || Me() != me || !jobs.Contains(e) || !Mine(e, me)) continue;
                    if (Time.realtimeSinceStartup < e.nextTry) continue;
                    if (e.removed)
                    {
                        if (string.IsNullOrEmpty(e.serverId)) { jobs.Remove(e); Save(); }   // 서버에 닿은 적 없다
                        else yield return Delete(e);
                        continue;
                    }
                    if (e.cancelPending)
                    {
                        if (string.IsNullOrEmpty(e.serverId)) { e.cancelPending = false; Save(); }
                        else yield return CancelOnServer(e);
                        continue;
                    }
                    if (e.state == State.Sending) yield return Send(e);
                    else if (e.state == State.Queued || e.state == State.Running) yield return Poll(e);
                    if (!e.removed && e.NeedsFetch && jobs.Contains(e)) yield return Fetch(e);
                }
                float wait = PollEvery;
#if UNITY_EDITOR
                if (EditorPollEvery > 0f) wait = EditorPollEvery;
#endif
                float until = Time.realtimeSinceStartup + wait;
                kicked = false;
                while (!kicked && Time.realtimeSinceStartup < until) { beat = Time.realtimeSinceStartup; yield return null; }
            }
            loop = null;
        }

        private void OnDestroy()
        {
            if (lm != null) lm.OnLoginStateChanged -= OnLogin;
            if (runner == this) runner = null;
        }
    }

    private static bool Refused(long code) => code == 400 || code == 403 || code == 413 || code == 422 || code == 429;

    private static void Field(List<IMultipartFormSection> form, string name, string value)
    {
        if (!string.IsNullOrEmpty(value)) form.Add(new MultipartFormDataSection(name, value));
    }

    // 맡기기 — 같은 번호면 서버는 처음 것을 돌려준다 (응답을 못 받고 끊겼어도 다시 보내면 된다)
    private static IEnumerator Send(Entry e)
    {
        var form = new List<IMultipartFormSection>();
        Field(form, "client_id", e.id);   // 빈 칸은 넣지 않는다 — Unity 가 빈 칸에서 오류를 내 일꾼이 멈췄다 (11-27 시험)
        Field(form, "kind", e.kind);
        Field(form, "style", e.style);
        Field(form, "request", e.request);
        Field(form, "place", e.place);
        string src = SrcPath(e);
        byte[] pic = src != null && File.Exists(src) ? File.ReadAllBytes(src) : null;
        if (pic != null && pic.Length > 0) form.Add(new MultipartFormFileSection("photo", pic, "photo.png", "image/png"));
        using (var req = UnityWebRequest.Post(Base + "/api/ai/jobs", form))
        {
            req.SetRequestHeader("Idempotency-Key", e.id);
            LoginManager.ApplyAuth(req);
            req.timeout = 30;
            yield return req.SendWebRequest();
            long code = req.responseCode;
            var d = Parse<Dto>(req.downloadHandler != null ? req.downloadHandler.text : null);
            bool ok = req.result == UnityWebRequest.Result.Success && d != null && !string.IsNullOrEmpty(d.id);
            if (e.removed || e.state != State.Sending)   // 그사이 지움 · 그만둠 — 서버가 받았으면 번호를 남겨 거기서도 접는다
            {
                if (ok) { e.serverId = d.id; Save(); }
                yield break;
            }
            if (ok)
            {
                e.serverId = d.id;
                e.tries = 0;
                Apply(e, d, true);
                yield break;
            }
            if (Refused(code))   // 서버가 받지 않았다 (요청 · 하루 횟수 · 크기) — 다시 보내도 같다
            {
                e.error = d != null && !string.IsNullOrEmpty(d.error) ? d.error : "받지 않았어요 (" + code + ")";
                Mark(e, State.Failed);
                yield break;
            }
            LoginManager.CheckUnauthorized(req);
            Backoff(e);   // 끊김 · 서버 오류 — 같은 번호로 다시
        }
    }

    private static IEnumerator Poll(Entry e)
    {
        if (string.IsNullOrEmpty(e.serverId)) { e.state = State.Sending; Save(); yield break; }   // 번호를 못 받았다 — 같은 번호로 다시 보내면 서버가 찾아 준다
        using (var req = UnityWebRequest.Get(JobUrl(e.serverId)))
        {
            LoginManager.ApplyAuth(req);
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (e.removed || !e.Working) yield break;
            if (req.result == UnityWebRequest.Result.Success)
            {
                var d = Parse<Dto>(req.downloadHandler.text);
                if (d != null) Apply(e, d, false);
            }
            else if (req.responseCode == 404)
            {
                e.error = "서버에 작업이 없어요";
                Mark(e, State.Failed);
            }
            else LoginManager.CheckUnauthorized(req);   // 끊김은 다음 차례에 다시
        }
    }

    private static IEnumerator Delete(Entry e)
    {
        using (var req = UnityWebRequest.Delete(JobUrl(e.serverId)))
        {
            LoginManager.ApplyAuth(req);
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success || req.responseCode == 404) { jobs.Remove(e); Save(); }
            else { LoginManager.CheckUnauthorized(req); Backoff(e); }
        }
    }

    private static IEnumerator CancelOnServer(Entry e)
    {
        using (var req = UnityWebRequest.PostWwwForm(JobUrl(e.serverId) + "/cancel", ""))
        {
            LoginManager.ApplyAuth(req);
            req.timeout = 15;
            yield return req.SendWebRequest();
            long code = req.responseCode;
            if (req.result == UnityWebRequest.Result.Success || code == 404 || code == 409) { e.cancelPending = false; e.tries = 0; Save(); }   // 409 — 이미 끝난 것
            else { LoginManager.CheckUnauthorized(req); Backoff(e); }
        }
    }

    // 다 된 GLB · 정지 사진을 폰에 받아 둔다 — 서버가 정리해도 폰에는 남게
    private static IEnumerator Fetch(Entry e)
    {
        byte[] glb = null;
        yield return Download(e.glbUrl, b => glb = b);
        if (!jobs.Contains(e) || e.removed) yield break;
        bool isGlb = glb != null && glb.Length >= 12 && glb[0] == 'g' && glb[1] == 'l' && glb[2] == 'T' && glb[3] == 'F';   // 오류 화면을 GLB 로 적지 않게
        if (!isGlb || !WriteFile(e.id + ".glb", glb)) { Backoff(e); yield break; }
        bool thumb = false;
        if (!string.IsNullOrEmpty(e.thumbUrl))
        {
            byte[] png = null;
            yield return Download(e.thumbUrl, b => png = b);
            thumb = png != null && png.Length > 0 && WriteFile(e.id + ".png", png);
        }
        if (e.removed) { DeleteFile(e.id + ".glb"); DeleteFile(e.id + ".png"); yield break; }   // 받는 사이 지웠다 — 늦게 쓴 파일 정리
        if (!jobs.Contains(e)) yield break;
        e.hasGlb = true;   // GLB · 정지 사진을 함께 — 화면은 「완성」을 한 번에 그린다
        e.hasThumb = thumb;
        e.tries = 0;
        e.updated = Now();
        Save();
        Changed?.Invoke(e);
    }

    private static IEnumerator Download(string url, Action<byte[]> done)
    {
        if (string.IsNullOrEmpty(url)) { done(null); yield break; }
        if (url.StartsWith("/")) url = Base + url;
        using (var req = UnityWebRequest.Get(url))
        {
            if (url.StartsWith(Base + "/")) LoginManager.ApplyAuth(req);   // 토큰은 우리 서버에만 (저장소 서명 주소에는 붙이지 않는다)
            req.timeout = 60;
            yield return req.SendWebRequest();
            done(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null);
        }
    }

    // 로그인한 계정의 서버 「내 작업」으로 채운다 — 다시 깔았거나 폰을 바꿨을 때 (앱을 켤 때 · 로그인할 때 한 번)
    private static IEnumerator Sync(string me)
    {
        using (var req = UnityWebRequest.Get(Base + "/api/ai/jobs?mine=1"))
        {
            LoginManager.ApplyAuth(req);
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (Me() != me) yield break;   // 그사이 계정이 바뀌었다
            if (req.result != UnityWebRequest.Result.Success)
            {
                if (req.responseCode == 404) { synced = true; yield break; }   // 서버에 아직 없는 기능
                LoginManager.CheckUnauthorized(req);
                syncTries++;
                syncNext = Time.realtimeSinceStartup + Mathf.Min(MaxBackoff, Mathf.Pow(2f, syncTries));
                yield break;
            }
            synced = true;
            syncTries = 0;
            var list = Parse<ListDto>(req.downloadHandler.text);
            if (list == null || list.jobs == null) yield break;
            bool added = false;
            foreach (var d in list.jobs)
            {
                if (d == null || string.IsNullOrEmpty(d.id)) continue;
                var e = jobs.Find(x => x.owner == me && (x.serverId == d.id || !string.IsNullOrEmpty(d.client_id) && x.id == d.client_id));
                if (e != null)
                {
                    if (string.IsNullOrEmpty(e.serverId)) e.serverId = d.id;
                    Apply(e, d, false);   // 지운 것 · 그만둔 것은 서버 말로 되살리지 않는다
                    continue;
                }
                e = new Entry
                {
                    id = !string.IsNullOrEmpty(d.client_id) ? d.client_id : "s" + d.id, owner = me,
                    serverId = d.id, kind = d.kind, style = d.style, request = d.request, place = d.place,
                    state = State.Queued, created = d.created > 0 ? d.created : Now(),
                };
                jobs.Add(e);
                Apply(e, d, true);
                e.seen = true;   // 예전에 본 것일 수 있다 — 점은 이 폰에서 새로 완성되는 것만
                added = true;
            }
            if (added) Save();
        }
    }

    private static void Apply(Entry e, Dto d, bool force)
    {
        if (e.removed || e.state == State.Canceled) return;
        State s = ToState(d.state, e.state);
        bool changed = force || s != e.state || d.progress != e.progress || d.position != e.position || d.stage != e.stage;
        if (s == State.Done && e.state != State.Done) e.seen = false;   // 새로 완성 — 프로필 줄 점
        e.state = s;
        e.progress = Mathf.Clamp(d.progress, 0, 100);
        e.position = Mathf.Max(0, d.position);
        e.stage = d.stage;
        if (!string.IsNullOrEmpty(d.label)) e.label = d.label;
        if (!string.IsNullOrEmpty(d.error)) e.error = d.error;
        if (!string.IsNullOrEmpty(d.glb_url)) e.glbUrl = d.glb_url;
        if (!string.IsNullOrEmpty(d.thumb_url)) e.thumbUrl = d.thumb_url;
        if (d.expires > 0) e.expires = d.expires;
        if (!changed) return;
        e.updated = Now();
        Save();
        Changed?.Invoke(e);
    }

    private static State ToState(string s, State keep)
    {
        switch (s)
        {
            case "queued": return State.Queued;
            case "running": return State.Running;
            case "done": return State.Done;
            case "failed": return State.Failed;
            case "canceled": return State.Canceled;
            default: return keep == State.Sending ? State.Queued : keep;   // 처음 받았는데 모르는 말 — 서버가 맡은 것으로
        }
    }

    private static T Parse<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<T>(json); }
        catch { return null; }
    }
}
