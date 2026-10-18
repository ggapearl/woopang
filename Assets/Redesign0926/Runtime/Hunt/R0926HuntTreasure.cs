using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SimpleJSON;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if !UNITY_EDITOR
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARSubsystems;
#endif

/// <summary>
/// 보물찾기 AR 보물 — 참가 중인 게임(내가 연 게임도)의 보물에 '보이는 거리'까지 다가가면 그 자리에 금빛 상자를 띄운다.
///  · 근접 확인(/near)이 보인다고 할 때만 서버가 보물 좌표를 준다 — 그 자리에 지오스페이셜 앵커로 놓는다 (높이는 휴대폰보다 조금 아래)
///  · 상자나 그 박스·화살표를 두 번 톡톡 누르면 찾기(/claim): 찾음 → 축하 화면(R0926HuntCelebrate) → 그 게임 상세 · 조금 더 가까이 ·
///    코드 게임 → 상세의 코드 칸 · 먼저 찾음 · 끝남. 한 번만 누르면 '두 번 톡톡' 안내 (10-14 대표님: '보물 열기' 버튼 대신 더블터치)
///  · 주최자는 참가는 못 해도 자기 보물은 보인다 — 누르면 '내가 숨긴 보물'이라고만 알린다 (10-14 대표님)
///  · 지켜볼 게임은 카드가 알려 준다(상세 · 처음 화면의 참가·주최 목록). 기기에 적어 두어 앱을 다시 켜도 이어 보고,
///    나가기 · 찾음 · 끝남 · 참가 아님이면 지운다. 설정에서 보물찾기 참여를 끄거나 로그아웃하면 띄우지 않는다
///  · 카드 상세가 그 게임을 보고 있으면 따로 묻지 않고 카드의 근접 확인 결과를 받아 쓴다 (서버는 한 사람에게 8초에 한 번만 답한다)
///  · 장소가 아니라 장소 목록·지도·숨기기(X)에는 넣지 않는다 — 목록에 뜨면 위치가 드러난다
///  · 줌(ARObjectZoomController) · 백그라운드 복귀·fallback(LoadingManager) · 하늘 날씨 가림은 다섯 데이터 매니저와 같이 따른다 (docs/claude/ar-object-spawn.md)
/// 캔버스에 둔다 — 추가 창이 닫혀 있어도 돈다.
/// </summary>
public class R0926HuntTreasure : MonoBehaviour
{
    [SerializeField] private GameObject treasurePrefab;   // HuntTreasure0926 — 앵커 + 금빛 상자(Target)
    [SerializeField] private CanvasGroup toast;           // 상태 알약 아래 알림
    [SerializeField] private RectTransform toastPill;
    [SerializeField] private Text toastTitle;
    [SerializeField] private Text toastSub;
    [SerializeField] private GameObject[] covers;         // AR 화면을 덮는 창 (상세·목록·메시지 등) — 열리면 알림을 걷고 '근처에 있어요'는 닫힐 때까지 미룬다

    private const string PrefKey = "r0926_hunt_treasure";
    private const int MaxTracked = 5;
    private const float BelowEye = 0.6f;                  // 보물 높이 — 휴대폰(눈높이)보다 이만큼 아래
    private const float TextLeft = 136f;                  // 알림 글 시작 (왼쪽 금빛 동그라미 다음)
    private const float DoubleTap = 0.45f;                // 두 번째 누름을 기다리는 시간
    private static readonly Color Gold = new Color(0.910f, 0.749f, 0.310f, 1f);

    private class Hunt
    {
        public int id;
        public string title;
        public bool host;            // 내가 연 게임 — 보이기만 하고 찾기는 없다
        public string prize;         // 상품 (null = 아직 모름 — 앱을 다시 켜면 카드가 다시 알려 줄 때까지)
        public string hostName;
        public long startsAt;        // 예약 게임 — 이 시각(유닉스 초)부터 묻는다
        public float nextPoll;
        public int rank = -1;        // 지난 근접 단계 (0 멀다 ~ 4 30m · 5 보임) — 가까워질 때만 진동
        public int misses;           // 보이다가 안 보인 횟수 — 경계에서 깜빡이지 않게 두 번 연속이면 치운다
        public bool spot;            // 서버가 보물 자리를 줬다 (보이는 거리 안)
        public double lat, lon;
        public bool announce;        // 처음 보였을 때 알림 (카드 상세가 보는 중이면 카드가 이미 알린다)
        public GameObject obj;
    }

    private static readonly List<Hunt> hunts = new List<Hunt>();
    private static bool loaded, hidden;
    private static string saved;
    private static R0926HuntTreasure instance;
    private static ARObjectZoomController zoom;
#if !UNITY_EDITOR
    private static AREarthManager earth;
#endif

    /// <summary>띄운 보물 (키 hunt_&lt;id&gt;) — 줌·하늘 날씨가 다섯 매니저의 것과 같이 본다</summary>
    public static readonly Dictionary<string, GameObject> SpawnedObjects = new Dictionary<string, GameObject>();

    private bool polling, claiming;
    private float nextRetry;
    private int pressed;                                  // 누른 보물 (0 = 없음) — 떼는 자리가 가까워야 누름
    private Vector2 pressAt;
    private static int tapId;                             // 한 번 누르고 두 번째를 기다리는 보물 (0 = 없음)
    private static float tapAt;
    private Coroutine toastCo;
    private bool toastFading;

    private static string Lang => R0926LocalizedText.Lang();
    private static string T(string ko, string en) => Lang == "ko" ? ko : en;

    // ── 카드가 부른다 ─────────────────────────────────────
    /// <summary>카드가 게임을 그릴 때 (상세 · 처음 화면의 참가·주최 목록) — 참가 중·아직 못 찾음이거나 내가 연 게임이면 지켜보고, 아니면 그만 본다</summary>
    public static void Track(JSONNode h)
    {
        if (h == null || !h.HasKey("id")) return;
        int id = h["id"].AsInt;
        string status = h["status"];
        bool host = h["is_host"].AsBool;
        bool on = (status == "live" || status == "scheduled") && (host || (h["me"]["joined"].AsBool && h["me"]["rank"].AsInt <= 0));
        if (!on) { Untrack(id); return; }
        Load();
        var t = Get(id);
        if (t != null) hunts.Remove(t);
        else
        {
            if (hunts.Count >= MaxTracked) Remove(hunts[0]);
            t = new Hunt { id = id };
        }
        hunts.Add(t);   // 최근에 본 게임이 뒤로 — 다섯 개가 넘으면 가장 오래된 것부터 놓는다
        t.title = h["title"];
        t.host = host;
        if (h.HasKey("prize")) t.prize = h["prize"];
        if (h["host"].HasKey("username")) t.hostName = h["host"]["username"];
        t.startsAt = status == "scheduled" ? Unix(h["starts_at"]) : 0;
        Save();
    }

    /// <summary>나가기 · 찾음 · 끝남 — 그만 보고 띄운 보물도 치운다</summary>
    public static void Untrack(int id)
    {
        Load();
        var t = Get(id);
        if (t == null) return;
        Remove(t);
        Save();
    }

    /// <summary>카드 상세의 근접 확인 결과 — 같은 게임을 따로 묻지 않고 이걸로 놓거나 치운다 (진동은 카드가)</summary>
    public static void Apply(int id, long code, JSONNode j)
    {
        Load();
        Result(Get(id), code, j, false);
    }

    public static bool Tracking(int id)
    {
        Load();
        return Get(id) != null;
    }

    // ── LoadingManager 가 다섯 데이터 매니저와 같이 부른다 ──────────
    /// <summary>백그라운드 복구 — false 면 숨기고(SetActive) true 면 다시 켠다</summary>
    public static void SetObjectsActive(bool on)
    {
        foreach (var go in SpawnedObjects.Values) if (go != null) go.SetActive(on);
    }

    /// <summary>fallback·복구 중 렌더러만 끄고 켠다 (Target 은 그대로 — 박스·화살표는 남는다)</summary>
    public static void SetRenderersVisible(bool visible)
    {
        hidden = !visible;
        foreach (var go in SpawnedObjects.Values)
        {
            if (go == null) continue;
            var a = go.GetComponent<CustomARGeospatialCreatorAnchor>();
            if (a != null) a.SetForceHideRenderers(!visible);
#if UNITY_EDITOR
            if (visible) foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;   // 에디터엔 앵커가 없어 위 호출이 다시 켜지 않는다
#endif
        }
    }

    /// <summary>앵커가 안 붙은 보물만 다시 — 켜져 있고 · 안 붙었고 · 재시도 중이 아닐 때만 (ar-object-spawn.md 2.1)</summary>
    public static void RetryFailedAnchors()
    {
        foreach (var go in SpawnedObjects.Values)
        {
            if (go == null || !go.activeSelf) continue;
            var a = go.GetComponent<CustomARGeospatialCreatorAnchor>();
            if (a == null || a.IsAnchorCreated || a.IsRetrying) continue;
            a.RecreateAnchor();
        }
    }

    // ── 지켜보기 ──────────────────────────────────────────
    private void Awake() => instance = this;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 '앱을 새로 켠 것'과 같게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        hunts.Clear();
        SpawnedObjects.Clear();
        loaded = hidden = false;
        saved = null;
        instance = null;
        zoom = null;
        tapId = 0;
#if !UNITY_EDITOR
        earth = null;
#endif
    }

    private void Update()
    {
        if (toastCo != null && !toastFading && Covered()) { StopCoroutine(toastCo); toastCo = StartCoroutine(FadeToast(8f)); }   // 창의 탭·제목을 가리지 않게 (찾은 뒤 상세 등)
        if (tapId != 0 && Time.unscaledTime - tapAt > DoubleTap)
        {
            tapId = 0;   // 한 번만 눌렀다 — 여는 법을 알려 준다
            Say(T("두 번 톡톡 누르면 보물이 열려요", "Double-tap to open the treasure"), null, 2.5f);
        }
        Load();
        if (hunts.Count == 0) return;
        Pointer();
        bool on = R0926HuntApi.CanCall && R0926HuntPrefs.Enabled;
        foreach (var t in hunts)
        {
            if (!on)
            {
                if (t.spot || t.obj != null) { Despawn(t, false); t.spot = false; t.rank = -1; }
                continue;
            }
            if (t.spot && t.obj == null) Spawn(t);
            if (t.announce && t.obj != null && !Covered())
            {
                t.announce = false;
                if (t.host) Say(T("내가 숨긴 보물이 보여요", "Your treasure is in view"), T("참가자가 찾으면 메세지로 알려 드려요", "We'll message you when someone finds it"), 4f);
                else Say(T("보물이 근처에 있어요!", "Treasure nearby!"), T("금빛 상자를 두 번 톡톡 눌러 열어 보세요", "Double-tap the golden box to open it"), 4f);
            }
        }
        if (!on) return;
        float now = Time.unscaledTime;
        if (now >= nextRetry) { nextRetry = now + 2f; RetryFailedAnchors(); }
        if (polling) return;
        long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var t in hunts)
            if (now >= t.nextPoll && unix >= t.startsAt && t.id != R0926HuntCard.NearHuntId) { StartCoroutine(Poll(t)); break; }
    }

    private IEnumerator Poll(Hunt t)
    {
        polling = true;
        t.nextPoll = Time.unscaledTime + 30f;   // 답이 없으면 이만큼 뒤에 다시
        if (R0926HuntCard.TryLocation(out float lat, out float lon, out float acc))
        {
            var b = new JSONObject(); b["lat"] = lat; b["lon"] = lon; b["acc"] = acc;
            long code = 0; JSONNode j = null;
            yield return R0926HuntApi.Post("/api/hunts/" + t.id + "/near", b, (c, r) => { code = c; j = r; });
            if (hunts.Contains(t)) Result(t, code, j, true);
        }
        else t.nextPoll = Time.unscaledTime + 5f;
        polling = false;
    }

    private static void Result(Hunt t, long code, JSONNode j, bool own)
    {
        if (t == null || j == null) return;
        float now = Time.unscaledTime;
        if (code == 200)
        {
            bool visible = j["visible"].AsBool && j.HasKey("object");
            string band = j["band"];
            int rank = visible ? 5 : band == "30m" ? 4 : band == "100m" ? 3 : band == "300m" ? 2 : band == "1km" ? 1 : 0;
            if (own && t.rank >= 0 && rank > t.rank && R0926HuntPrefs.NotifyNear) R0926HuntCard.Buzz();   // 카드와 같이 — 가까워질 때만 · 첫 확인은 기준만
            t.rank = rank;
            t.nextPoll = now + (rank >= 2 ? 8.5f : rank == 1 ? 15f : 30f);
            if (visible)
            {
                t.misses = 0;
                t.lat = j["object"]["lat"].AsDouble;
                t.lon = j["object"]["lon"].AsDouble;
                if (!t.spot) t.announce = own;
                t.spot = true;
            }
            else if (t.spot && ++t.misses >= 2) { Despawn(t, false); t.spot = false; }
        }
        else if (code == 429) t.nextPoll = now + Mathf.Max(2f, j["retry_after"].AsFloat);
        else if (code == 404 && t.startsAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() < t.startsAt + 120) t.nextPoll = now + 15f;   // 막 시작 — 서버가 진행 중으로 넘기는 사이
        else if (code == 404 || code == 403) Untrack(t.id);   // 끝났거나 참가자가 아니다 (다른 계정으로 로그인 등)
        else t.nextPoll = now + 30f;                          // 네트워크 · 순간이동 · 로그인 만료 — 잠시 뒤
    }

    // ── 띄우기 · 치우기 ───────────────────────────────────
    private void Spawn(Hunt t)
    {
        if (treasurePrefab == null || !Altitude(out double alt)) return;   // 지구 추적 전엔 높이를 모른다 — 다음 프레임에
        var go = Instantiate(treasurePrefab);
        go.name = "HuntTreasure_" + t.id;
        go.transform.localScale = Vector3.one * Zoom();
        var target = go.GetComponentInChildren<Target>(true);
        if (target != null)
        {
            target.PlaceName = t.title;
            target.placeId = "hunt_" + t.id;
            target.gpsLatitude = (float)t.lat;
            target.gpsLongitude = (float)t.lon;
            target.TargetColor = Gold;
            target.Sparkle = true;   // 화살표·박스 둘레 별빛 — 장소 인디케이터와 한눈에 갈리게
            int id = t.id;
            target.OnIndicatorTapped = () => Tap(id);
        }
        var anchor = go.GetComponent<CustomARGeospatialCreatorAnchor>();
        if (anchor != null)
        {
            anchor.SetCoordinatesAndCreateAnchor(t.lat, t.lon, alt);
            if (hidden) anchor.SetForceHideRenderers(true);
        }
        t.obj = go;
        SpawnedObjects["hunt_" + t.id] = go;
    }

    private static bool Altitude(out double alt)
    {
#if UNITY_EDITOR
        var cam = Camera.main;
        alt = (cam != null ? cam.transform.position.y : 0f) - BelowEye;
        return true;
#else
        alt = 0;
        if (earth == null) earth = FindFirstObjectByType<AREarthManager>();
        if (earth == null || earth.EarthTrackingState != TrackingState.Tracking) return false;
        alt = earth.CameraGeospatialPose.Altitude - BelowEye;
        return true;
#endif
    }

    private static float Zoom()
    {
        if (zoom == null) zoom = FindFirstObjectByType<ARObjectZoomController>();
        return zoom != null ? zoom.GetZoomLevel() : 1f;
    }

    private static void Despawn(Hunt t, bool pop)
    {
        SpawnedObjects.Remove("hunt_" + t.id);
        var go = t.obj;
        t.obj = null;
        if (go == null) return;
        var a = go.GetComponent<CustomARGeospatialCreatorAnchor>();
        if (a != null) a.ReleaseAnchor();
        if (pop && instance != null && go.activeInHierarchy) instance.StartCoroutine(Pop(go));
        else Destroy(go);
    }

    // 찾았을 때 — 환하게 빛나며 빙글 돌다 커진 뒤 쏙 사라진다 (그 사이 축하 화면이 뜬다)
    private static IEnumerator Pop(GameObject go)
    {
        var cube = go.GetComponentInChildren<GoldenGlowCube>();
        if (cube != null) cube.OnTouchEnter();
        Transform spin = cube != null ? cube.transform : go.transform;
        Vector3 s0 = go.transform.localScale;
        for (float k = 0f; k < 1f && go != null; k += Time.unscaledDeltaTime / 0.7f)
        {
            float grow = k < 0.45f ? Mathf.SmoothStep(1f, 1.4f, k / 0.45f) : Mathf.SmoothStep(1.4f, 0f, (k - 0.45f) / 0.55f);
            go.transform.localScale = s0 * grow;
            spin.Rotate(0f, 900f * Time.unscaledDeltaTime, 0f, Space.Self);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private static void Remove(Hunt t)
    {
        Despawn(t, false);
        hunts.Remove(t);
    }

    // ── 누르기 ────────────────────────────────────────────
    private void Pointer()
    {
        if (SpawnedObjects.Count == 0) { pressed = 0; return; }
#if UNITY_EDITOR
        var m = Mouse.current;
        if (m == null) return;
        if (m.leftButton.wasPressedThisFrame) Press(m.position.ReadValue(), OverUI(-1));
        else if (m.leftButton.wasReleasedThisFrame) Release(m.position.ReadValue());
#else
        var ts = Touchscreen.current;
        if (ts == null) return;
        int down = 0;
        foreach (var tc in ts.touches) if (tc.isInProgress) down++;
        if (down > 1) { pressed = 0; return; }   // 두 손가락(줌) — 누름이 아니다
        var p = ts.primaryTouch;
        if (p.press.wasPressedThisFrame) Press(p.position.ReadValue(), OverUI(p.touchId.ReadValue()));
        else if (p.press.wasReleasedThisFrame) Release(p.position.ReadValue());
#endif
    }

#if UNITY_EDITOR
    // 플레이 모드 시험(Redesign0926PlayTest)이 손가락 대신 넣는다 — 그 자리를 덮은 UI 는 직접 본다
    public void EditorFeed(bool down, Vector2 pos)
    {
        if (!down) { Release(pos); return; }
        var es = EventSystem.current;
        var hits = new List<RaycastResult>();
        if (es != null) es.RaycastAll(new PointerEventData(es) { position = pos }, hits);
        Press(pos, hits.Count > 0);
    }
#endif

    private static bool OverUI(int pointerId)
    {
        var es = EventSystem.current;
        return es != null && (pointerId < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(pointerId));
    }

    private void Press(Vector2 pos, bool overUI)
    {
        pressed = 0;
        if (overUI) return;   // 화면 위 버튼·창
        var cam = Camera.main;
        if (cam == null || !Physics.Raycast(cam.ScreenPointToRay(pos), out var hit, 500f)) return;
        foreach (var t in hunts)
            if (t.obj != null && hit.transform.IsChildOf(t.obj.transform)) { pressed = t.id; pressAt = pos; return; }
    }

    private void Release(Vector2 pos)
    {
        int id = pressed;
        pressed = 0;
        float slop = Screen.dpi > 0f ? Screen.dpi * 0.15f : 40f;
        if (id != 0 && (pos - pressAt).sqrMagnitude <= slop * slop) Tap(id);
    }

    // 두 번 톡톡 — 첫 누름은 상자가 움찔하기만, 0.45초 안에 같은 보물을 또 누르면 연다
    private static void Tap(int id)
    {
        if (instance == null || instance.claiming) return;
        var t = Get(id);
        if (t == null) return;
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect();
        instance.Nudge(t);
        if (t.host)
        {
            tapId = 0;
            Say(T("내가 숨긴 보물이에요", "This is your treasure"), T("참가자가 찾으면 메세지로 알려 드려요", "We'll message you when someone finds it"), 3f);
            return;
        }
        float now = Time.unscaledTime;
        if (tapId == id && now - tapAt <= DoubleTap)
        {
            tapId = 0;
            instance.StartCoroutine(instance.Claim(t));
            return;
        }
        tapId = id;
        tapAt = now;
    }

    // 누를 때마다 상자가 살짝 커지며 빛났다가 돌아온다
    private void Nudge(Hunt t)
    {
        var cube = t.obj != null ? t.obj.GetComponentInChildren<GoldenGlowCube>() : null;
        if (cube != null) StartCoroutine(NudgeCo(cube));
    }

    private static IEnumerator NudgeCo(GoldenGlowCube cube)
    {
        cube.OnTouchEnter();
        yield return new WaitForSecondsRealtime(0.16f);
        if (cube != null) cube.OnTouchExit();
    }

    private IEnumerator Claim(Hunt t)
    {
        if (!R0926HuntCard.TryLocation(out float lat, out float lon, out float acc)) { Say(R0926HuntApi.ErrorText("location", Lang), null, 3f); yield break; }
        claiming = true;
        var b = new JSONObject(); b["lat"] = lat; b["lon"] = lon; b["acc"] = acc; b["mock"] = false;
        b["device_id"] = SystemInfo.deviceUniqueIdentifier;
        int id = t.id;
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Post("/api/hunts/" + id + "/claim", b, (c, r) => { code = c; j = r; });
        claiming = false;
        string result = j["result"];
        switch (result)
        {
            case "found":
                if (hunts.Contains(t)) { Despawn(t, true); hunts.Remove(t); Save(); }
                // 축하 화면 — 상품 안내 · 주최자에게 간 메세지. '확인'을 누르면 그 게임 상세(결과 · 주최자에게 한마디)
                if (!R0926HuntCelebrate.Show(id, t.title, j["rank"].AsInt, t.prize, t.hostName))
                {
                    Say(T("보물을 찾았어요!", "You found it!"), T(j["rank"].AsInt + "번째로 찾았어요", "You're #" + j["rank"].AsInt), 3.5f);
                    yield return new WaitForSecondsRealtime(1.6f);
                    R0926HuntPush.Open(id.ToString(), null, false);
                }
                break;
            case "need_code":
                Say(T("코드를 넣어 주세요", "Enter the code"), Short(j["question"]), 3.5f);
                claiming = true;                                   // 상세가 열릴 때까지 다시 누르지 않게
                yield return new WaitForSecondsRealtime(1.2f);
                claiming = false;
                R0926HuntPush.Open(id.ToString(), null, false);   // 상세의 코드 칸 (카드의 근접 확인이 켠다)
                break;
            case "too_far":
                Say(T("조금만 더 가까이", "A bit closer"), T(j["remaining_m"].AsInt + "m 더 가 보세요", j["remaining_m"].AsInt + "m to go"), 3f);
                break;
            case "taken": Say(T("아쉽게도 다른 사람이 먼저 찾았어요", "Someone found it first"), null, 3.5f); Untrack(id); break;
            case "ended": Say(T("게임이 끝났어요", "The game has ended"), null, 3f); Untrack(id); break;
            case "device_used": Say(T("이 기기로는 이미 찾았어요", "Already found on this device"), null, 3f); Untrack(id); break;
            case "host":   // 예전에 적어 둔 줄이라 내 게임인 줄 몰랐다 — 계속 보여 준다
                if (hunts.Contains(t)) { t.host = true; Save(); }
                Say(T("내가 숨긴 보물이에요", "This is your treasure"), T("참가자가 찾으면 메세지로 알려 드려요", "We'll message you when someone finds it"), 3f);
                break;
            case "not_joined": Say(R0926HuntApi.ErrorText(result, Lang), null, 3f); Untrack(id); break;
            case "locked": Say(T("10분 동안 코드를 넣을 수 없어요", "Code locked for 10 minutes"), null, 3f); break;
            default: Say(R0926HuntApi.ErrorText(result ?? j["error"], Lang), null, 3f); break;
        }
    }

    private static string Short(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return s.Length > 24 ? s.Substring(0, 23) + "…" : s;
    }

    // ── 알림 (상태 알약 아래) ─────────────────────────────
    private static void Say(string title, string sub, float secs)
    {
        if (instance != null) instance.ShowToast(title, sub, secs);
    }

    public static void HideToast()
    {
        if (instance == null || instance.toast == null || !instance.toast.gameObject.activeSelf) return;
        if (instance.toastCo != null) instance.StopCoroutine(instance.toastCo);
        instance.toastCo = instance.StartCoroutine(instance.FadeToast(8f));
    }

    private void ShowToast(string title, string sub, float secs)
    {
        if (toast == null || toastPill == null || toastTitle == null) return;
        // 긴 한 줄('… — …')은 두 줄로 — 화면 폭을 넘지 않게
        int cut = string.IsNullOrEmpty(sub) && title != null ? title.IndexOf(" — ", StringComparison.Ordinal) : -1;
        if (cut > 0) { sub = title.Substring(cut + 3); title = title.Substring(0, cut); }
        if (toastCo != null) StopCoroutine(toastCo);
        toastCo = StartCoroutine(ToastCo(title, sub, secs));
    }

    private IEnumerator ToastCo(string title, string sub, float secs)
    {
        toastFading = false;
        bool two = !string.IsNullOrEmpty(sub) && toastSub != null;
        toastTitle.text = title;
        if (toastSub != null) { toastSub.text = two ? sub : ""; toastSub.gameObject.SetActive(two); }
        toast.gameObject.SetActive(true);
        Layout(two);
        for (float a = toast.alpha; a < 1f; a += Time.unscaledDeltaTime * 6f) { toast.alpha = a; yield return null; }
        toast.alpha = 1f;
        yield return new WaitForSecondsRealtime(secs);
        yield return FadeToast(4f);
    }

    private IEnumerator FadeToast(float speed)
    {
        toastFading = true;
        for (float a = toast.alpha; a > 0f; a -= Time.unscaledDeltaTime * speed) { toast.alpha = a; yield return null; }
        toast.alpha = 0f;
        toast.gameObject.SetActive(false);
        toastFading = false;
        toastCo = null;
    }

    private bool Covered()
    {
        if (covers != null)
            foreach (var p in covers)
                if (p != null && p.activeInHierarchy) return true;
        return false;
    }

    // 글 길이에 맞춰 알약 폭 — 화면 폭에서 양옆 48 은 남긴다
    private void Layout(bool two)
    {
        float textW = Mathf.Max(toastTitle.preferredWidth, two ? toastSub.preferredWidth : 0f);
        var area = toast.transform.parent as RectTransform;
        float max = area != null ? area.rect.width - 96f : 1300f;
        float w = Mathf.Min(max, TextLeft + textW + 52f);
        toastPill.sizeDelta = new Vector2(w, two ? 156f : 112f);
        var tr = toastTitle.rectTransform;
        tr.anchoredPosition = new Vector2(TextLeft, two ? 24f : 0f);
        tr.sizeDelta = new Vector2(w - TextLeft - 40f, 56f);
        if (two)
        {
            var sr = toastSub.rectTransform;
            sr.anchoredPosition = new Vector2(TextLeft, -28f);
            sr.sizeDelta = new Vector2(w - TextLeft - 40f, 48f);
        }
    }

    // ── 기기에 적어 두기 (게임 id · 시작 시각 · 제목) ─────────
    private static Hunt Get(int id)
    {
        foreach (var t in hunts) if (t.id == id) return t;
        return null;
    }

    // 한 줄에 게임 하나: id · 시작 시각 · 제목 (내가 연 게임은 끝에 'h' 한 칸 더)
    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        hunts.Clear();
        saved = PlayerPrefs.GetString(PrefKey, "");
        foreach (var line in saved.Split('\n'))
        {
            var p = line.Split(new[] { '\t' }, 4);
            if (p.Length >= 3 && int.TryParse(p[0], out int id) && long.TryParse(p[1], out long at) && Get(id) == null)
                hunts.Add(new Hunt { id = id, startsAt = at, title = p[2], host = p.Length == 4 && p[3] == "h" });
        }
    }

    private static void Save()
    {
        var sb = new StringBuilder();
        foreach (var t in hunts)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(t.id).Append('\t').Append(t.startsAt).Append('\t').Append((t.title ?? "").Replace('\n', ' ').Replace('\t', ' '));
            if (t.host) sb.Append("\th");
        }
        string s = sb.ToString();
        if (s == saved) return;
        saved = s;
        if (s.Length == 0) PlayerPrefs.DeleteKey(PrefKey);
        else PlayerPrefs.SetString(PrefKey, s);
        PlayerPrefs.Save();
    }

    private static long Unix(JSONNode n)
    {
        if (n == null || n.IsNull || string.IsNullOrEmpty(n.Value)) return 0;
        return DateTime.TryParse(n.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
            ? new DateTimeOffset(d, TimeSpan.Zero).ToUnixTimeSeconds() : 0;
    }
}
