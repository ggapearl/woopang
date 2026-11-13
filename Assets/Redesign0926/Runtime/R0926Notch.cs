using System.Globalization;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using Google.XR.ARCoreExtensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// 아이폰 노치(또는 다이내믹 아일랜드) 양옆으로 검은 판을 늘려 작은 정보를 띄운다 — 노치가 있는 아이폰의 세로 화면에서만.
/// 안드로이드 · 노치 없는 아이폰 · 가로 화면에선 아무것도 하지 않는다 (예전 그대로).
///  · 하늘을 접으면(R0926SkyWeather) 날씨 그림 + 기온 — 휴대폰을 내려도 보인다. 누르면 하늘 판을 화면 위쪽에 고정해 편다 (11-09 대표님
///    「'하늘접기' 버튼을 눌럿을 때 … '노치' 에 반영 … '날씨 이모지' 와 '온도' 정도만」 · 「노치를 터치해서는 … 화면 상단에 고정해둬」)
///  · 목록에서 장소를 고르면 화살표 + 거리 '↑ 45m' — 멀어서 AR 에 없어도 어느 쪽으로 얼마나 가면 되는지 (11-09 대표님 「노치 화면에 화살표로
///    방향과 거리를 추적해서」). 처음 3초는 아래로 늘어나 장소 이름과 '누르면 안내를 끝내요'. 작을 때 누르면 다시 이름, 이름이 보일 때 누르면 끝.
///    15m 안에 1.5초 머물면 '도착했어요' 뒤 끝, 20분이 지나도 끝
///  · 창(목록 · 메세지 · 프로필 …)이 화면을 덮으면 숨는다 — 안내는 이어지고 창을 닫으면 다시 보인다
/// 노치 자리: Screen.cutouts(맞아 보일 때만) → 기기 이름 · 위쪽 안전영역으로 고른 표. Unity 의 아이폰 노치 표는 새 기기에서 비어 있거나 틀린다(UUM-56430).
/// 판은 진짜 노치보다 조금 작은 크기에서 열리고 닫힌 뒤엔 꺼 둔다 — 표가 몇 pt 틀려도 노치 밖으로 삐져나오지 않게.
/// 판이 떠 있는 동안 상태 막대(시계 · 배터리)는 숨긴다 — 우팡은 상태 막대를 보이게 두는데 그 자리가 바로 노치 양옆이다
/// </summary>
public class R0926Notch : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform shape;
    [SerializeField] private Image shapeImage;
    [Tooltip("노치 아이폰 — 위 어깨가 바깥으로 휘고 아래 모서리가 둥근 9조각 (r0926_notch)")]
    [SerializeField] private Sprite notchSprite;
    [Tooltip("다이내믹 아일랜드 — 알약 (r0926_pill)")]
    [SerializeField] private Sprite pillSprite;
    [SerializeField] private CanvasGroup content;
    [SerializeField] private RectTransform leftSlot;
    [SerializeField] private RectTransform rightSlot;
    [SerializeField] private R0926WeatherIcon weatherIcon;
    [SerializeField] private RectTransform arrow;
    [SerializeField] private RectTransform check;
    [SerializeField] private Text rightText;
    [Tooltip("아래로 늘어났을 때 — 장소 이름 · 작은 안내 줄")]
    [SerializeField] private CanvasGroup wide;
    [SerializeField] private Text titleText;
    [SerializeField] private Text hintText;
    [Tooltip("이 창들이 떠 있는 동안 숨는다 (물방울메뉴의 GlassCovers 와 같은 목록)")]
    [SerializeField] private GameObject[] covers;
    [SerializeField] private R0926SkyWeather sky;
    [SerializeField] private float arriveMeters = 15f;

    private enum Mode { None, Weather, Guide }

    // 크기 (pt — 기기 배율로 캔버스 단위로 바꾼다)
    private const float NotchExtra = 4f;     // 노치 아이폰은 판을 노치보다 이만큼 길게 — 그림이 너무 작지 않게
    private const float Ear = 46f;           // 노치 옆 한 칸의 폭 (최소)
    private const float EarPad = 9f;
    private const float IconPt = 24f, ArrowPt = 19f, TextPt = 17f, TitlePt = 15.5f, HintPt = 12f;
    private const float ExpandGap = 6f, TitleLine = 22f, HintLine = 17f, ExpandBottom = 9f, ExpandMinW = 290f;
    private const float ExpandTime = 3f, ArriveHold = 1.5f, ArriveShow = 4f, MaxGuideSeconds = 20f * 60f;

    public static R0926Notch Instance { get; private set; }

    /// <summary>노치가 있는 아이폰의 세로 화면 — 하늘 칩 대신 노치에 날씨, 목록 줄은 노치 안내</summary>
    public static bool Available
    {
        get
        {
            var n = Instance;
            if (n == null || !n.isActiveAndEnabled) return false;
            n.Measure();
            return n.hasCut;
        }
    }

    /// <summary>하늘을 접은 동안 노치에 날씨 — 하늘 판이 매 프레임 알린다 (바뀐 것만 다시 그린다)</summary>
    public static void SetWeather(bool on, int code, bool day, int temp)
    {
        if (Instance != null) Instance.Weather(on, code, day, temp);
    }

    /// <summary>목록에서 고른 장소로 노치 안내 — 노치가 없으면 false (예전처럼 줄에 알린다)</summary>
    public static bool Guide(float lat, float lon, Target target, string name)
        => Instance != null && Instance.isActiveAndEnabled && Instance.StartGuide(lat, lon, target, name);

    public static void StopGuide()
    {
        if (Instance != null) Instance.EndGuide();
    }

    // 노치 자리 (pt) — 화면이 바뀔 때만 다시 잰다
    private bool measured, hasCut, island;
    private int mW, mH;
    private Rect mSafe;
    private float cutWpt, cutHpt, cutTopPt, ppt = 3f;
    private float upt, sizedUpt = -1f;       // 캔버스 단위 / pt

    // 보이는 것
    private Mode shown = Mode.None;
    private bool dirty;
    private float open, openVel, expand, expandVel, fade;
    private float expandUntil, pendingExpand;
    private string titleWord = "", hintWord = "";

    // 날씨
    private bool weatherOn, wDay = true;
    private int wCode = -1, wTemp = int.MinValue;

    // 안내
    private bool guiding, hasAngle;
    private float gLat, gLon, gStart, nearSince = -1f, arrivedAt = -1f;
    private Target gTarget;
    private string gName = "", distWord = "…";
    private float angle, angleVel, meters = -1f;
#if !UNITY_EDITOR
    private AREarthManager earth;
    private float earthSearchAt;
    private bool compassFailed;
#endif

    private void Awake()
    {
        Instance = this;
        if (shape != null) shape.gameObject.SetActive(false);
    }

    private void OnDisable() => StatusBar(false);

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void _SetStatusBarHidden(bool hidden);
#endif
    private bool barHidden;

    // 노치 옆 판이 떠 있는 동안 상태 막대를 숨기고, 판이 거의 닫히면 다시 보인다 (StatusBarStylePlugin.mm)
    private void StatusBar(bool hide)
    {
        if (hide == barHidden) return;
        barHidden = hide;
#if UNITY_IOS && !UNITY_EDITOR
        _SetStatusBarHidden(hide);
#endif
    }

    // ── 노치 자리 ─────────────────────────────────────────────

    private void Measure()
    {
        int w = UnityEngine.Device.Screen.width, h = UnityEngine.Device.Screen.height;
        Rect safe = UnityEngine.Device.Screen.safeArea;
        if (measured && w == mW && h == mH && safe == mSafe) return;
        measured = true; mW = w; mH = h; mSafe = safe;
        hasCut = Cutout(w, h, safe, out island, out cutWpt, out cutHpt, out cutTopPt, out ppt);
        sizedUpt = -1f;   // 가로로 돌리면 숨기만 한다 — 안내는 이어지고 세로로 돌아오면 다시 보인다
    }

    private static bool Cutout(int w, int h, Rect safe, out bool isl, out float wPt, out float hPt, out float topPt, out float scale)
    {
        isl = false; wPt = hPt = topPt = 0f; scale = 3f;
#if UNITY_EDITOR
        if (EditorForceNoNotch) return false;
#endif
        if (UnityEngine.Device.Application.platform != RuntimePlatform.IPhonePlayer || h <= w || w <= 0) return false;
        scale = PointScale(w);
        float inset = (h - safe.yMax) / scale;
        if (inset < 30f) return false;   // 노치 없는 아이폰 — 상태 막대 높이뿐 (홈 버튼 · SE)
        string model = UnityEngine.Device.SystemInfo.deviceModel ?? "";
        ParseModel(model, out int major, out int minor);
        isl = inset >= 54f || IslandModel(major, minor);
#if UNITY_EDITOR
        if (EditorIsland) isl = true;
#endif
        if (isl)
        {
            // 다이내믹 아일랜드 126 × 37.33pt — 위쪽 안전영역 59pt(14 Pro · 15 · 16)는 위에서 11.3pt, 62pt(16 Pro · 17)는 14.3pt
            wPt = 126f; hPt = 37.33f; topPt = Mathf.Max(5f, inset - 47.67f);
            return true;
        }
        foreach (var c in UnityEngine.Device.Screen.cutouts)
        {
            // 위 가운데에 붙은 노치로 보일 때만 믿는다
            float cw = c.width / scale, ch = c.height / scale;
            if (Mathf.Abs(c.center.x - w * 0.5f) > w * 0.08f || h - c.yMax > 2f * scale) continue;
            if (cw < 120f || cw > 260f || ch < 24f || ch > 42f) continue;
            wPt = cw + 4f; hPt = ch; topPt = 0f;
            return true;
        }
        NotchTable(major, minor, inset, out wPt, out hPt);
        return true;
    }

    // 실제 노치보다 몇 pt 넓게 잡는다 — 좁게 잡으면 옆 칸의 그림·글이 진짜 노치 밑으로 숨는다
    private static void NotchTable(int major, int minor, float inset, out float wPt, out float hPt)
    {
        if (major == 14 && minor == 4) { wPt = 182f; hPt = 36f; return; }                            // 13 mini
        if (major == 14 || major == 17 && minor == 5) { wPt = 170f; hPt = 34f; return; }             // 13 · 13 Pro · 13 Pro Max · 14 · 14 Plus · 16e
        if (major == 13 && minor == 1) { wPt = 232f; hPt = 36f; return; }                            // 12 mini
        if (major == 13) { wPt = 216f; hPt = 33f; return; }                                          // 12 · 12 Pro · 12 Pro Max
        if (major == 11 && minor == 8 || major == 12 && minor == 1) { wPt = 236f; hPt = 34f; return; }   // XR · 11
        if (major >= 10 && major <= 12) { wPt = 214f; hPt = 31f; return; }                           // X · XS · XS Max · 11 Pro · 11 Pro Max
        // 모르는 기기 — 위쪽 안전영역으로 (넓은 쪽으로)
        if (inset < 46f) { wPt = 214f; hPt = 31f; }
        else if (inset < 49f) { wPt = 236f; hPt = 34f; }
        else { wPt = 232f; hPt = 36f; }
    }

    private static bool IslandModel(int major, int minor)
        => major == 15 && minor >= 2 || major == 16 || major == 17 && minor != 5 || major >= 18;

    private static void ParseModel(string model, out int major, out int minor)
    {
        major = minor = -1;
        if (!model.StartsWith("iPhone")) return;
        string rest = model.Substring(6);
        int comma = rest.IndexOf(',');
        if (comma <= 0) return;
        int.TryParse(rest.Substring(0, comma), NumberStyles.Integer, CultureInfo.InvariantCulture, out major);
        int.TryParse(rest.Substring(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out minor);
    }

    // pt 배율 — 아이폰 논리 폭 중 가장 가까운 것 (12·13 mini 는 1080px 에 375pt = 2.88)
    private static readonly float[] Widths = { 375f, 390f, 393f, 402f, 414f, 428f, 430f, 440f };
    private static float PointScale(int w)
    {
        float guess = w >= 950 ? w / 3f : w / 2f, best = guess, bestD = float.MaxValue;
        foreach (var lw in Widths)
        {
            float d = Mathf.Abs(lw - guess);
            if (d < bestD) { bestD = d; best = lw; }
        }
        return bestD < 20f ? w / best : (w >= 950 ? 3f : 2f);
    }

    // ── 날씨 · 안내 ───────────────────────────────────────────

    private void Weather(bool on, int code, bool day, int temp)
    {
        weatherOn = on && hasCut;
        if (!on) return;
        if (code == wCode && day == wDay && temp == wTemp) return;
        wCode = code; wDay = day; wTemp = temp;
        if (shown == Mode.Weather) dirty = true;
    }

    private bool StartGuide(float lat, float lon, Target target, string name)
    {
        Measure();
        if (!hasCut || lat == 0f && lon == 0f) return false;
        guiding = true;
        gLat = lat; gLon = lon; gTarget = target; gName = Clean(name);
        gStart = Time.unscaledTime;
        nearSince = arrivedAt = -1f;
        meters = -1f; hasAngle = false; angleVel = 0f;
        distWord = "…";
        titleWord = gName;
        hintWord = HintEnd();
        expandUntil = 0f;
        pendingExpand = ExpandTime;   // 목록이 내려가 노치가 보이면 그때부터 3초
        if (shown == Mode.Guide) dirty = true;
        return true;
    }

    private void EndGuide()
    {
        guiding = false;
        gTarget = null;
        arrivedAt = -1f;
        expandUntil = pendingExpand = 0f;
    }

    private void UpdateGuide(float now, float dt)
    {
        if (!guiding) return;
        if (now - gStart > MaxGuideSeconds) { EndGuide(); return; }
        if (arrivedAt >= 0f)
        {
            if (now - arrivedAt > ArriveShow) EndGuide();
            return;
        }
        bool haveLoc = GeoHere.TryGet(out double lat, out double lon, out _);
        if (haveLoc)
        {
            meters = (float)GeoHere.Meters(lat, lon, gLat, gLon);
            if (meters <= arriveMeters)
            {
                if (nearSince < 0f) nearSince = now;
                if (now - nearSince >= ArriveHold) { Arrive(now); return; }
            }
            else nearSince = -1f;
        }
        float? rel = RelativeAngle(haveLoc, lat, lon);
        if (rel != null)
        {
            angle = hasAngle ? Mathf.SmoothDampAngle(angle, rel.Value, ref angleVel, 0.18f, Mathf.Infinity, dt) : rel.Value;
            hasAngle = true;
        }
        string d = meters >= 0f ? Dist(meters) : "…";
        if (d != distWord) { distWord = d; if (shown == Mode.Guide && rightText != null) rightText.text = d; }
        if (shown == Mode.Guide && arrow != null)
        {
            arrow.localEulerAngles = new Vector3(0f, 0f, -angle);
            var img = arrow.GetComponent<Image>();
            if (img != null) img.canvasRenderer.SetAlpha(hasAngle ? 1f : 0.35f);   // 방향을 아직 모른다
        }
    }

    private void Arrive(float now)
    {
        arrivedAt = now;
        titleWord = L("도착했어요", "You've arrived", "到着しました", "已到达", "Has llegado");
        hintWord = gName;
        expandUntil = now + ArriveShow;
        pendingExpand = 0f;
        if (shown == Mode.Guide) dirty = true;
        Haptic();
    }

    // 기기가 향한 쪽 기준 그 장소의 방향 (도, 오른쪽 +) — AR 에 떠 있으면 카메라와 오브젝트 사이(목록 줄 방향과 같은 셈), 아니면 방위와 기기 방향
    private float? RelativeAngle(bool haveLoc, double lat, double lon)
    {
        var cam = Camera.main;
        if (gTarget != null && gTarget.isActiveAndEnabled && gTarget.IsAnchorReady && cam != null && ArTracking())
        {
            Vector3 v = gTarget.transform.position - cam.transform.position;
            v.y = 0f;
            if (v.sqrMagnitude > 1f) return Vector3.SignedAngle(CamFlat(cam.transform.rotation), v, Vector3.up);
        }
        if (!haveLoc) return null;
        float? heading = Heading(cam);
        if (heading == null) return null;
        double dx = (gLon - lon) * System.Math.Cos(lat * System.Math.PI / 180.0), dy = gLat - lat;
        float bearing = (float)(System.Math.Atan2(dx, dy) * 180.0 / System.Math.PI);
        return Mathf.DeltaAngle(heading.Value, bearing);
    }

    private static bool ArTracking()
    {
#if UNITY_EDITOR
        if (EditorHeading != null) return false;   // 시험은 방위로
#endif
        return ARSession.state == ARSessionState.SessionTracking;
    }

    // 세워 들면 렌즈 쪽, 눕혀 들면 화면 위쪽이 '앞' — 둘의 수평 성분을 더하면 어느 기울기에서도 같은 쪽 (R0926PlaceRows 와 같다)
    private static Vector3 CamFlat(Quaternion q)
    {
        Vector3 f = q * Vector3.forward, u = q * Vector3.up;
        var v = new Vector3(f.x + u.x, 0f, f.z + u.z);
        return v.sqrMagnitude < 0.01f ? new Vector3(f.x, 0f, f.z) : v;
    }

    // 기기가 향한 방위 (북 0 · 동 90) — 지구 추적(VPS)이 잡혔으면 그 방향, 아니면 휴대폰 나침반
    private float? Heading(Camera cam)
    {
#if UNITY_EDITOR
        if (EditorHeading != null) return EditorHeading.Value;
        if (cam == null) return null;
        var ef = CamFlat(cam.transform.rotation);
        return Mathf.Atan2(ef.x, ef.z) * Mathf.Rad2Deg;   // 에디터 — 월드 +z 를 북쪽으로
#else
        if (earth == null && Time.unscaledTime >= earthSearchAt)
        {
            earthSearchAt = Time.unscaledTime + 5f;   // 없는 씬에서 매 프레임 찾지 않게
            earth = FindAnyObjectByType<AREarthManager>();
        }
        if (earth != null && earth.EarthTrackingState == TrackingState.Tracking)
        {
            var pose = earth.CameraGeospatialPose;
            if (pose.OrientationYawAccuracy <= 25)
            {
                Vector3 v = CamFlat(pose.EunRotation);   // x 동 · z 북
                if (v.sqrMagnitude > 0.0001f) return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            }
        }
        if (compassFailed) return null;
        try
        {
            if (!Input.compass.enabled) { Input.compass.enabled = true; return null; }   // 켠 직후엔 아직 값이 없다
            if (Input.compass.timestamp <= 0) return null;
            float h = Input.compass.trueHeading;
            return h < 0f ? Input.compass.magneticHeading : h;
        }
        catch (System.InvalidOperationException)
        {
            compassFailed = true;
            return null;
        }
#endif
    }

    // ── 화면 ─────────────────────────────────────────────────

    private void Update()
    {
        if (shape == null || shapeImage == null) return;
        float now = Time.unscaledTime, dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        Measure();
        int sh = UnityEngine.Device.Screen.height;
        upt = hasCut && sh > 0 ? ((RectTransform)transform).rect.height / sh * ppt : 0f;
        UpdateGuide(now, dt);

        Mode want = !hasCut ? Mode.None : guiding ? Mode.Guide : weatherOn ? Mode.Weather : Mode.None;
        bool show = want != Mode.None && upt > 0f && !Covered();

        // 내용이 바뀌면 글·그림이 사라진 뒤에 갈아 끼운다 (닫히는 중엔 그대로 두고 판과 같이 사라진다)
        Mode target = want != Mode.None ? want : shown;
        if (target != shown)
        {
            fade = Mathf.MoveTowards(fade, 0f, dt * 10f);
            if (fade <= 0f || open < 0.05f) { shown = target; dirty = true; }
        }
        else fade = Mathf.MoveTowards(fade, 1f, dt * 6f);
        if (upt > 0f && Mathf.Abs(upt - sizedUpt) > sizedUpt * 0.005f) Size();
        if (dirty) Refresh();

        if (pendingExpand > 0f && show && shown == Mode.Guide && open > 0.85f) { expandUntil = now + pendingExpand; pendingExpand = 0f; }
        bool wantExpand = show && shown == Mode.Guide && now < expandUntil;
        Spring(ref open, ref openVel, show ? 1f : 0f, dt);
        Spring(ref expand, ref expandVel, wantExpand ? 1f : 0f, dt);
        if (!show && open < 0.01f && Mathf.Abs(openVel) < 0.05f) open = openVel = expand = expandVel = 0f;

        bool on = show || open > 0f;
        StatusBar(show || open > 0.3f);
        if (shape.gameObject.activeSelf != on) shape.gameObject.SetActive(on);
        if (!on) return;
        Layout();
        if (content != null) content.alpha = fade * Mathf.Clamp01((open - 0.55f) / 0.4f);
        if (wide != null) wide.alpha = fade * Mathf.Clamp01((expand - 0.5f) / 0.4f);
        shapeImage.raycastTarget = show && open > 0.5f;
    }

    private bool Covered()
    {
        if (covers == null) return false;
        foreach (var g in covers) if (g != null && g.activeInHierarchy) return true;
        return false;
    }

    // 살짝 튀었다 닿는다 (감쇠비 약 0.8 · 0.35초)
    private static void Spring(ref float x, ref float v, float target, float dt)
    {
        const float K = 300f, C = 28f;
        v += ((target - x) * K - v * C) * dt;
        x += v * dt;
    }

    private float CompactH => cutHpt * upt + (island ? 2f : NotchExtra) * upt;
    private float Shoulder => island ? 0f : 24f / Mathf.Max(0.01f, shapeImage.pixelsPerUnitMultiplier);

    // 기기 배율이 바뀌면 (처음 · 해상도 바뀜) 그림 · 글 크기와 판 모양
    private void Size()
    {
        sizedUpt = upt;
        float ch = CompactH;
        shapeImage.sprite = island ? pillSprite : notchSprite;
        shapeImage.type = Image.Type.Sliced;
        // 알약은 높이의 반이 둥근 끝, 노치는 아래 모서리 반지름이 높이의 0.4 (그림의 반지름 40px)
        shapeImage.pixelsPerUnitMultiplier = island ? 126f / ch : 100f / ch;
        if (weatherIcon != null) ((RectTransform)weatherIcon.transform).sizeDelta = Vector2.one * IconPt * upt;
        if (arrow != null) arrow.sizeDelta = Vector2.one * ArrowPt * upt;
        if (check != null) check.sizeDelta = Vector2.one * ArrowPt * upt;
        if (rightText != null) rightText.fontSize = Mathf.RoundToInt(TextPt * upt);
        if (titleText != null) titleText.fontSize = Mathf.RoundToInt(TitlePt * upt);
        if (hintText != null) hintText.fontSize = Mathf.RoundToInt(HintPt * upt);
        dirty = true;   // 날씨 그림은 크기에 맞춰 다시 짠다
    }

    private void Refresh()
    {
        dirty = false;
        bool weather = shown == Mode.Weather, guide = shown == Mode.Guide, arrived = guide && arrivedAt >= 0f;
        if (weatherIcon != null && weatherIcon.gameObject.activeSelf != weather) weatherIcon.gameObject.SetActive(weather);
        if (arrow != null && arrow.gameObject.activeSelf != (guide && !arrived)) arrow.gameObject.SetActive(guide && !arrived);
        if (check != null && check.gameObject.activeSelf != arrived) check.gameObject.SetActive(arrived);
        if (weather && weatherIcon != null && wCode >= 0) weatherIcon.Set(wCode, wDay);
        if (rightText != null) rightText.text = weather ? (wTemp == int.MinValue ? "" : wTemp + "°") : guide ? distWord : "";
        if (titleText != null) titleText.text = titleWord;
        if (hintText != null) hintText.text = hintWord;
    }

    private void Layout()
    {
        float pad = EarPad * upt;
        float ear = Mathf.Max(Ear * upt, (rightText != null ? rightText.preferredWidth : 0f) + 2f * pad);
        float cutW = cutWpt * upt, cutH = cutHpt * upt, ch = CompactH, shoulder = Shoulder;
        float rootW = ((RectTransform)transform).rect.width;
        float bodyOpen = cutW + 2f * ear;
        float bodyExp = Mathf.Min(Mathf.Max(bodyOpen, ExpandMinW * upt), rootW - 24f * upt - 2f * shoulder);
        float hExp = ch + (ExpandGap + TitleLine + HintLine + ExpandBottom) * upt;
        float bodyClosed = cutW * 0.92f, hClosed = cutH * 0.92f;   // 진짜 노치 뒤에 숨는 크기에서 열린다
        float w = Mathf.LerpUnclamped(Mathf.LerpUnclamped(bodyClosed, bodyOpen, open), bodyExp, expand);
        float h = Mathf.LerpUnclamped(Mathf.LerpUnclamped(hClosed, ch, open), hExp, expand);
        float topClosed = island ? cutTopPt * upt + (cutH - hClosed) * 0.5f : 0f;
        float topOpen = island ? cutTopPt * upt - upt : 0f;
        shape.anchoredPosition = new Vector2(0f, -Mathf.LerpUnclamped(topClosed, topOpen, Mathf.Clamp01(open)));
        shape.sizeDelta = new Vector2(Mathf.Max(0f, w + 2f * shoulder), Mathf.Max(0f, h));

        if (leftSlot != null) leftSlot.anchoredPosition = new Vector2(-(cutW + ear) * 0.5f, -ch * 0.5f);
        if (rightSlot != null)
        {
            rightSlot.anchoredPosition = new Vector2((cutW + ear) * 0.5f, -ch * 0.5f);
            rightSlot.sizeDelta = new Vector2(ear, ch);
        }
        if (titleText != null)
        {
            var t = titleText.rectTransform;
            t.anchoredPosition = new Vector2(0f, -(ch + (ExpandGap + TitleLine * 0.5f) * upt));
            t.sizeDelta = new Vector2(bodyExp - 28f * upt, TitleLine * upt);
        }
        if (hintText != null)
        {
            var t = hintText.rectTransform;
            t.anchoredPosition = new Vector2(0f, -(ch + (ExpandGap + TitleLine + HintLine * 0.5f) * upt));
            t.sizeDelta = new Vector2(bodyExp - 28f * upt, HintLine * upt);
        }
    }

    // ── 누르기 ───────────────────────────────────────────────

    public void OnPointerClick(PointerEventData e)
    {
        if (open < 0.5f) return;
        float now = Time.unscaledTime;
        Haptic();
        if (shown == Mode.Guide && guiding)
        {
            // 이름이 보이면(처음 · 도착 · 눌러서 펼친 동안) 끝, 작으면 이름부터
            if (arrivedAt >= 0f || now < expandUntil) { EndGuide(); return; }
            titleWord = gName;
            hintWord = HintEnd();
            expandUntil = now + ExpandTime;
            dirty = true;
            return;
        }
        if (shown == Mode.Weather && sky != null)
        {
            Vector3 c = shape.TransformPoint(shape.rect.center);   // 오버레이 캔버스 — 월드 좌표가 곧 화면 픽셀
            sky.UnfoldFromNotch(new Vector2(c.x, c.y));
        }
    }

    // ── 글 ───────────────────────────────────────────────────

    private static string HintEnd() => L("누르면 안내를 끝내요", "Tap to end guidance", "タップで案内を終了", "点按结束导航", "Toca para terminar la guía");

    private static string Dist(float m)
    {
        var inv = CultureInfo.InvariantCulture;
        if (m < 100f) return Mathf.RoundToInt(m).ToString(inv) + "m";
        if (m < 995f) return (Mathf.RoundToInt(m / 10f) * 10).ToString(inv) + "m";
        if (m < 9950f) return (m / 1000f).ToString("0.0", inv) + "km";
        return Mathf.RoundToInt(m / 1000f).ToString(inv) + "km";
    }

    // 목록 줄 이름 — 글자 꾸밈(<color> 등)은 빼고, 길면 18자에서 자른다
    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        bool tag = false;
        foreach (char ch in s)
        {
            if (ch == '<') tag = true;
            else if (ch == '>') tag = false;
            else if (!tag) sb.Append(ch);
        }
        string t = sb.ToString().Trim();
        var si = new StringInfo(t);
        return si.LengthInTextElements > 18 ? si.SubstringByTextElements(0, 17) + "…" : t;
    }

    private static void Haptic()
    {
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerLightHaptic();
    }

    private static string L(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);

#if UNITY_EDITOR
    /// <summary>시험용 (Redesign0926PlayTest notch) — 노치 없는 아이폰 흉내 · 다이내믹 아일랜드 흉내 · 기기 방위(북 0)</summary>
    public static bool EditorForceNoNotch, EditorIsland;
    public static float? EditorHeading;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEditorStatics() { EditorForceNoNotch = false; EditorIsland = false; EditorHeading = null; }
    public void EditorRemeasure() => measured = false;
    public RectTransform EditorShape => shape;
    public bool EditorIslandShape => island;
    public float EditorOpen => open;
    public float EditorExpand => expand;
    public bool EditorGuiding => guiding;
    public string EditorMode => shown.ToString();
    public string EditorRight => rightText != null ? rightText.text : null;
    public string EditorTitle => titleText != null ? titleText.text : null;
    public float EditorArrow => arrow != null && arrow.gameObject.activeSelf ? Mathf.DeltaAngle(0f, -arrow.localEulerAngles.z) : float.NaN;
    public bool EditorArrived => arrivedAt >= 0f;
    public Vector2 EditorCutPt => new Vector2(cutWpt, cutHpt);
    public float EditorCutTopPt => cutTopPt;
    public float EditorPpt => ppt;
    public bool EditorBarHidden => barHidden;
    public string EditorHint => hintText != null ? hintText.text : null;
    public bool EditorCheck => check != null && check.gameObject.activeSelf;
#endif
}
