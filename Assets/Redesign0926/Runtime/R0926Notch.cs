using System.Collections.Generic;
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
/// 아이폰 노치(또는 다이내믹 아일랜드)를 아래로 한 줄 늘려 작은 정보를 띄운다 — 노치가 있는 아이폰의 세로 화면에서만.
/// 안드로이드 · 노치 없는 아이폰 · 가로 화면에선 아무것도 하지 않는다 (예전 그대로).
/// 판의 폭은 그 기기의 노치 그대로 — 양옆의 시계 · 배터리 · LTE 는 가리지 않고, 그림과 글은 노치 아래 한 줄에 붙여 쓴다
/// (11-17 대표님 「노치만 덮는게 아니라 해당 줄 전체를 덮어서 시간, 배터리, LTE 여부 등 다 안보이도록 … 각 기계에 맞게 노치사이즈를 맞춰서
///  해당 부분에만」 · 「날씨 로고랑 온도는 바로 옆에 붙도록」). 예전엔 노치 양옆으로 늘리고 그동안 상태 막대를 숨겼다
///  · 하늘을 접으면(R0926SkyWeather) 하늘 판의 장 — 날씨 그림 + 기온. 좌우로 밀면 근처 보물찾기 · 축제 · 핫플 · 친구의 첫 줄(그림 + 거리)
///    (11-17 「좌우로 스와이프해서 각각의 친구나 보물찾기 정보등을 노치 내에서」 — R0926SkyPages.NotchItems). 누르면 하늘 판을 화면 위쪽에
///    고정해 그 장으로 편다 (「노치를 터치하면 바로 상단에 고정된 채로」). 접을 때마다 잠깐 '터치하면 돌아가요'
///  · 목록에서 장소를 고르면 화살표 + 거리 '↑ 45m' — 멀어서 AR 에 없어도 어느 쪽으로 얼마나 가면 되는지 (11-09 대표님 「노치 화면에 화살표로
///    방향과 거리를 추적해서」). 처음 3초는 넓게 펴져 장소 이름과 '누르면 안내를 끝내요' — 그동안만 상태 막대를 숨긴다(판이 노치 양옆까지 덮는다).
///    작을 때 누르면 다시 이름, 이름이 보일 때 누르면 끝.
///    아래 화살표(R0926GuideArrow)가 같은 곳을 안내하는 동안은 거리 · 도착을 화살표가 정하고 노치는 같은 순간 '도착했어요', 끝도 같이 — 10m · 한 번만
///    (11-16 둘째 대표님 「노치 위 표시에서도 10m 거리까지 도착하면 완성 … 두번 도착했다고 나오지 않도록」). 화살표가 없을 때만 스스로 10m 안에 1.5초. 20분이 지나도 끝
///  · 창(목록 · 메세지 · 프로필 …)이 화면을 덮으면 숨는다 — 안내는 이어지고 창을 닫으면 다시 보인다
///  · 노치 아래로 내려온 만큼(DropPx) 위쪽 칩 · 상태 알약 · 확대 표시(R0926SafeInset 위쪽)가 비켜 내려간다
///  · 다이내믹 아일랜드 아이폰은 아래 줄 없이 아일랜드 높이 그대로 양옆으로만 — 왼쪽(시계 쪽 40pt) 기온 · 거리, 오른쪽(신호 쪽 26pt) 날씨 그림 · 화살표
///    (11-24 시안 「바꾸는 안」 · 11-26 대표님 「위에 노치관련 수정안 확인했어. 1,2번 둘다 진행해」). 아래로 내려오지 않아 칩 · 알약도 비켜 서지 않는다.
///    3초 이름 판 · '터치하면 돌아가요'는 예전처럼 잠깐만 아래로 펴진다. 노치 아이폰(X ~ 14 · 16e)은 양옆이 시계 · 배터리로 차 있어 아래 한 줄 그대로
///    → 11-28 꺼 둠(IslandSides): 대표님 실기기(1.2.72)에서 눌러도 안 펴지고 날씨 그림이 신호 막대에 닿았다 — 양옆은 상태 막대 줄 안이라 아이폰이
///      손가락을 앱에 넘기지 않고, 신호 쪽 빈칸도 실제로는 약 22pt 다. 아일랜드도 다시 아래 한 줄 (그 줄의 아래쪽은 상태 막대 밖이라 눌린다)
///  · 누르는 자리는 판보다 양옆 · 아래로 조금 넓다 (위쪽은 상태 막대가 가져가므로). 하늘 장을 아래로 끌어도 편다 (11-28 「위에서 아래로 스와이프해도 날씨내용 자체가 아예 안나와」)
/// 노치 자리: Screen.cutouts(맞아 보일 때만) → 기기 이름 · 위쪽 안전영역으로 고른 표. Unity 의 아이폰 노치 표는 새 기기에서 비어 있거나 틀린다(UUM-56430).
/// 판은 진짜 노치보다 조금 작은 크기에서 열리고 닫힌 뒤엔 꺼 둔다 — 표가 몇 pt 틀려도 노치 밖으로 삐져나오지 않게.
/// </summary>
public class R0926Notch : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform shape;
    [SerializeField] private Image shapeImage;
    [Tooltip("노치 아이폰 — 위 어깨가 바깥으로 휘고 아래 모서리가 둥근 9조각 (r0926_notch)")]
    [SerializeField] private Sprite notchSprite;
    [Tooltip("다이내믹 아일랜드 — 알약 (r0926_pill)")]
    [SerializeField] private Sprite pillSprite;
    [Tooltip("노치 아래 한 줄 — 판에 꽉 차게 늘어나 있고, 좌우로 밀려난 그림 · 글은 판 밖에서 잘린다 (RectMask2D)")]
    [SerializeField] private CanvasGroup content;
    [Tooltip("그림 칸 — 날씨 · 장 그림 · 친구 얼굴 · 화살표 · 도착 ✓")]
    [SerializeField] private RectTransform leftSlot;
    [Tooltip("글 칸 — 기온 · 거리 (그림 바로 오른쪽)")]
    [SerializeField] private RectTransform rightSlot;
    [SerializeField] private R0926WeatherIcon weatherIcon;
    [Tooltip("보물찾기 · 축제 · 핫플 장의 그림")]
    [SerializeField] private Image pageIcon;
    [Tooltip("친구 장 — 둥근 바탕(첫 글자) · 사진")]
    [SerializeField] private Image face;
    [SerializeField] private Text faceInitial;
    [SerializeField] private RawImage facePhoto;
    [SerializeField] private RectTransform arrow;
    [SerializeField] private RectTransform check;
    [SerializeField] private Text rightText;
    [Tooltip("접은 뒤 잠깐 — '터치하면 돌아가요' (그동안 판이 줄 아래로 조금 늘어난다)")]
    [SerializeField] private Text noteText;
    [Tooltip("넓게 펴졌을 때 — 장소 이름 · 작은 안내 줄")]
    [SerializeField] private CanvasGroup wide;
    [SerializeField] private Text titleText;
    [SerializeField] private Text hintText;
    [Tooltip("이 창들이 떠 있는 동안 숨는다 (물방울메뉴의 GlassCovers 와 같은 목록)")]
    [SerializeField] private GameObject[] covers;
    [SerializeField] private R0926SkyWeather sky;

    private enum Mode { None, Sky, Guide }

    // 크기 (pt — 기기 배율로 캔버스 단위로 바꾼다)
    private const float NotchExtra = 4f;     // 노치 아이폰 판의 둥근 모서리 기준 높이 = 노치 + 이만큼 (아일랜드는 + 2)
    private const float RowPt = 27f;         // 노치 아래 한 줄
    private const float RowPad = 7f, RowGap = 4f;
    private const float NoteLine = 16f, NoteBottom = 4f, NoteAlpha = 0.72f;
    private const float IconPt = 21f, ArrowPt = 17f, TextPt = 16f, MinTextPt = 11f, NotePt = 11.5f, TitlePt = 15.5f, HintPt = 12f;
    private const float ExpandGap = 2f, TitleLine = 22f, HintLine = 17f, ExpandBottom = 9f, ExpandMinW = 290f;
    private const float DropGap = 6f;        // 노치 아래 줄과 그 밑 칩 · 알약 사이
    private const float FlingPt = 260f;      // 이보다 빨리 튕기면 조금만 밀어도 옆 장 (pt/초)
    private const float ExpandTime = 3f, NoteTime = 2.6f, FoldWait = 2f;
    private const float SoloArriveMeters = 10f, ArriveHold = 1.5f, ArriveShow = 4f, MaxGuideSeconds = 20f * 60f;
    // 다이내믹 아일랜드 양옆 (아이폰 15 실측 시안) — 늘리는 폭 · 글 가운데 / 그림 가운데가 아일랜드 끝에서 떨어진 거리 · 글 칸 폭
    private const float SideLeftPt = 40f, SideRightPt = 26f, SideTextAtPt = 17.5f, SideIconAtPt = 12f, SideTextRoomPt = 30f;
    private const float SideIconPt = 19f, SideArrowPt = 15f, SideTextPt = 15f, SideDistPt = 13.5f;
    private const float SideEdgePt = 2f, SideMinPt = 9f;   // 긴 말(12.3km · 3km 이내)은 왼쪽 칸 양끝 2pt 까지 · 9pt 까지 줄인다
    // 아일랜드 양옆 배치 — 11-28 꺼 둠 (요약 주석). 홈 화면 실시간 현황(Live Activity)을 만들면 앱 안도 그 모양에 맞출 때 다시 쓴다
    private static readonly bool IslandSides = false;
    private const float HitSidePt = 8f, HitBelowPt = 6f;   // 누르는 자리를 판 양옆 · 아래로 넓히는 만큼 (아래는 칩까지의 틈 DropGap 만큼만)
    private bool Sides => island && IslandSides;

    public static R0926Notch Instance { get; private set; }

    /// <summary>노치가 있는 아이폰의 세로 화면 — 하늘 칩 대신 노치에 하늘 장, 목록 줄은 노치 안내</summary>
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

    /// <summary>노치 판이 위쪽 안전영역 밑으로 내려온 만큼 (화면 px · 그 밑 칩까지의 틈 포함) — 위쪽 안전영역만큼 미는 것(R0926SafeInset)이 그만큼 더 내려간다</summary>
    public static float DropPx { get; private set; }

    /// <summary>하늘을 노치로 접은 동안 — 하늘 판이 매 프레임 알린다. weather 면 첫 장이 날씨, 나머지 장은 R0926SkyPages.NotchItems (바뀐 것만 다시 그린다)</summary>
    public static void SetSky(bool on, bool weather, int code, bool day, int temp)
    {
        if (Instance != null) Instance.Sky(on, weather, code, day, temp);
    }

    /// <summary>하늘 판을 노치로 접었다 ('하늘 접기' · 위쪽에 고정한 판을 위로 밀기) — 판이 보던 장부터, 노치가 뜨면 잠깐 '터치하면 돌아가요'</summary>
    public static void Folded(R0926SkyPages.Kind page)
    {
        var n = Instance;
        if (n == null) return;
        n.pageKind = page;
        n.page = 0;       // 그 장이 노치에 없으면(라이브 등) 첫 장
        n.itemVer = -1;   // 다음 프레임에 그 장을 찾는다
        n.foldAt = Time.unscaledTime;
    }

    /// <summary>노치 아래 줄의 가운데 (화면 px · 아일랜드면 양옆으로 늘어난 판의 가운데) — 하늘 판이 접힐 때 이리로 들어간다. 노치가 없으면 false</summary>
    public static bool RowCenter(out Vector2 screen)
    {
        screen = Vector2.zero;
        var n = Instance;
        if (n == null || !n.isActiveAndEnabled) return false;
        n.Measure();
        if (!n.hasCut) return false;
        float x = n.Sides ? (SideRightPt - SideLeftPt) * 0.5f * n.ppt : 0f;
        float midPt = n.Sides ? n.cutTopPt + n.cutHpt * 0.5f : n.RowTopPt + RowPt * 0.5f;
        screen = new Vector2(UnityEngine.Device.Screen.width * 0.5f + x, UnityEngine.Device.Screen.height - midPt * n.ppt);
        return true;
    }

    /// <summary>목록에서 고른 장소로 노치 안내 — 노치가 없으면 false (예전처럼 줄에 알린다)</summary>
    public static bool Guide(float lat, float lon, Target target, string name)
        => Instance != null && Instance.isActiveAndEnabled && Instance.StartGuide(lat, lon, target, name);

    public static void StopGuide()
    {
        if (Instance != null) Instance.EndGuide();
    }

    /// <summary>아래 화살표가 같은 곳의 도착을 정했다 — 노치도 같은 순간 '도착했어요' (도착은 한 번 · 떨림은 화살표 쪽 한 번)</summary>
    public static void GuideArrived(float lat, float lon)
    {
        var n = Instance;
        if (n != null && n.guiding && n.arrivedAt < 0f && n.gLat == lat && n.gLon == lon) n.Arrive(Time.unscaledTime, false);
    }

    /// <summary>아래 화살표의 안내가 끝났다 (도착 뒤 · × · 20분 · 가까이 카드가 이어 받음) — 같은 곳을 안내하던 노치도 같이 끝</summary>
    public static void GuideEnded(float lat, float lon)
    {
        var n = Instance;
        if (n != null && n.guiding && n.gLat == lat && n.gLon == lon) n.EndGuide();
    }

    // 노치 자리 (pt) — 화면이 바뀔 때만 다시 잰다
    private bool measured, hasCut, island;
    private int mW, mH;
    private Rect mSafe;
    private float cutWpt, cutHpt, cutTopPt, insetPt, ppt = 3f;
    private float upt, sizedUpt = -1f;       // 캔버스 단위 / pt

    // 보이는 것
    private Mode shown = Mode.None;
    private bool dirty;
    private float open, openVel, expand, expandVel, note, noteVel, fade;
    private float expandUntil, pendingExpand, noteUntil, foldAt = -1f;
    private string titleWord = "", hintWord = "";

    // 하늘 장 — [날씨] + 판의 다른 장 첫 줄. 보던 장(pageKind)이 그대로 있으면 그 장을 지킨다
    private bool skyOn, weatherOn, itemsWeather, wDay = true;
    private int wCode = -1, wTemp = int.MinValue;
    private readonly List<R0926SkyPages.NotchItem> items = new List<R0926SkyPages.NotchItem>();
    private int itemVer = -1, page;
    private R0926SkyPages.Kind pageKind = R0926SkyPages.Kind.Weather;

    // 좌우로 밀기 (캔버스 단위) — 줄은 판의 몸통 폭에서 잘린다 (노치 어깨 밖으로 비치지 않게)
    private RectMask2D clip;
    private bool sliding;
    private float slide, slideVel, dragX, lastX, lastT, flingV;

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
        clip = content != null ? content.GetComponent<RectMask2D>() : null;
    }

    private void OnDisable()
    {
        StatusBar(false);
        DropPx = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void _SetStatusBarHidden(bool hidden);
#endif
    private bool barHidden;

    // 판이 넓게 펴진 동안(안내 시작 · 도착 — 노치 양옆까지 덮는다)만 상태 막대를 숨기고 접히면 다시 보인다 (StatusBarStylePlugin.mm)
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
        insetPt = (h - safe.yMax) / Mathf.Max(0.01f, ppt);
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
            wPt = cw + 2f; hPt = ch; topPt = 0f;
            return true;
        }
        NotchTable(major, minor, inset, out wPt, out hPt);
        return true;
    }

    // 실제 노치보다 2pt 넓게 (양쪽 1pt) — 아래로 늘인 판이 노치와 한 몸으로 이어 보이게. 양옆의 시계 · 배터리 자리는 건드리지 않는다
    private static void NotchTable(int major, int minor, float inset, out float wPt, out float hPt)
    {
        if (major == 14 && minor == 4) { wPt = 177f; hPt = 36f; return; }                            // 13 mini
        if (major == 14 || major == 17 && minor == 5) { wPt = 164f; hPt = 34f; return; }             // 13 · 13 Pro · 13 Pro Max · 14 · 14 Plus · 16e
        if (major == 13 && minor == 1) { wPt = 228f; hPt = 36f; return; }                            // 12 mini
        if (major == 13) { wPt = 212f; hPt = 33f; return; }                                          // 12 · 12 Pro · 12 Pro Max
        if (major == 11 && minor == 8 || major == 12 && minor == 1) { wPt = 232f; hPt = 34f; return; }   // XR · 11
        if (major >= 10 && major <= 12) { wPt = 211f; hPt = 31f; return; }                           // X · XS · XS Max · 11 Pro · 11 Pro Max
        // 모르는 기기 — 위쪽 안전영역으로 (좁은 쪽으로 — 넓게 잡으면 시계 · 배터리를 가린다)
        if (inset < 46f) { wPt = 211f; hPt = 31f; }
        else if (inset < 49f) { wPt = 164f; hPt = 34f; }
        else { wPt = 177f; hPt = 36f; }
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

    // 화면 위에서 노치(아일랜드) 아래 변까지 (pt) — 그 밑이 그림 · 글 줄
    private float RowTopPt => island ? cutTopPt + cutHpt : cutHpt;

    // ── 하늘 장 · 안내 ────────────────────────────────────────

    private void Sky(bool on, bool weather, int code, bool day, int temp)
    {
        skyOn = on && hasCut;
        bool w = weather && code >= 0;
        if (w != weatherOn) { weatherOn = w; itemVer = -1; }
        if (!w) return;
        if (code == wCode && day == wDay && temp == wTemp) return;
        wCode = code; wDay = day; wTemp = temp;
        if (shown == Mode.Sky) dirty = true;
    }

    private void SyncItems()
    {
        int ver = R0926SkyPages.NotchVersion;
        if (ver == itemVer && weatherOn == itemsWeather) return;
        itemVer = ver;
        itemsWeather = weatherOn;
        items.Clear();
        if (weatherOn) items.Add(new R0926SkyPages.NotchItem { kind = R0926SkyPages.Kind.Weather });
        items.AddRange(R0926SkyPages.NotchItems);
        int at = items.FindIndex(x => x.kind == pageKind);
        page = at >= 0 ? at : Mathf.Clamp(page, 0, Mathf.Max(0, items.Count - 1));
        if (shown == Mode.Sky) dirty = true;
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
        // 아래 화살표가 같은 곳을 안내하면 거리 · 도착은 화살표 것 — 둘이 다른 거리를 보이거나 '도착했어요'가 두 번 나오지 않게 (11-16 둘째)
        if (R0926GuideArrow.Leads(gLat, gLon, out float led))
        {
            nearSince = -1f;
            if (led >= 0f) meters = led;
        }
        else if (haveLoc)
        {
            meters = (float)GeoHere.Meters(lat, lon, gLat, gLon);
            if (meters <= SoloArriveMeters)
            {
                if (nearSince < 0f) nearSince = now;
                if (now - nearSince >= ArriveHold) { Arrive(now, true); return; }
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
        if (d != distWord) { distWord = d; if (shown == Mode.Guide && rightText != null) { rightText.text = d; FitRight(false); } }
        if (shown == Mode.Guide && arrow != null)
        {
            arrow.localEulerAngles = new Vector3(0f, 0f, -angle);
            var img = arrow.GetComponent<Image>();
            if (img != null) img.canvasRenderer.SetAlpha(hasAngle ? 1f : 0.35f);   // 방향을 아직 모른다
        }
    }

    private void Arrive(float now, bool haptic)
    {
        arrivedAt = now;
        titleWord = L("도착했어요", "You've arrived", "到着しました", "已到达", "Has llegado");
        hintWord = gName;
        expandUntil = now + ArriveShow;
        pendingExpand = 0f;
        if (shown == Mode.Guide) dirty = true;
        if (haptic) Haptic();   // 화살표가 정한 도착이면 떨림은 화살표 쪽 한 번
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
        if (skyOn) SyncItems();

        Mode want = !hasCut ? Mode.None : guiding ? Mode.Guide : skyOn && items.Count > 0 ? Mode.Sky : Mode.None;
        bool show = want != Mode.None && upt > 0f && !Covered();

        // 내용이 바뀌면 글·그림이 사라진 뒤에 갈아 끼운다 (닫히는 중엔 그대로 두고 판과 같이 사라진다)
        Mode target = want != Mode.None ? want : shown;
        if (target != shown)
        {
            fade = Mathf.MoveTowards(fade, 0f, dt * 10f);
            if (fade <= 0f || open < 0.05f) { shown = target; dirty = true; sliding = false; slide = slideVel = 0f; }
        }
        else fade = Mathf.MoveTowards(fade, 1f, dt * 6f);
        if (upt > 0f && Mathf.Abs(upt - sizedUpt) > sizedUpt * 0.005f) Size();
        if (dirty) Refresh();

        if (pendingExpand > 0f && show && shown == Mode.Guide && open > 0.85f) { expandUntil = now + pendingExpand; pendingExpand = 0f; }
        if (foldAt >= 0f)
        {
            // 접은 뒤 노치가 펴지면 잠깐 '터치하면 돌아가요' — 그사이 안내가 노치를 쓰면 건너뛴다
            if (show && shown == Mode.Sky && open > 0.85f) { noteUntil = now + NoteTime; foldAt = -1f; }
            else if (now - foldAt > FoldWait) foldAt = -1f;
        }
        bool wantExpand = show && shown == Mode.Guide && now < expandUntil;
        bool wantNote = show && shown == Mode.Sky && now < noteUntil;
        Spring(ref open, ref openVel, show ? 1f : 0f, dt);
        Spring(ref expand, ref expandVel, wantExpand ? 1f : 0f, dt);
        Spring(ref note, ref noteVel, wantNote ? 1f : 0f, dt);
        if (!show && open < 0.01f && Mathf.Abs(openVel) < 0.05f) open = openVel = expand = expandVel = note = noteVel = 0f;

        bool on = show || open > 0f;
        StatusBar(on && expand > 0.3f);
        if (shape.gameObject.activeSelf != on) shape.gameObject.SetActive(on);
        if (!on) { DropPx = 0f; return; }
        if (!sliding) slide = Mathf.Abs(slide) < 0.5f ? 0f : Mathf.SmoothDamp(slide, 0f, ref slideVel, 0.13f, Mathf.Infinity, dt);
        Layout();
        float rowW = Mathf.Max(1f, cutWpt * upt);
        if (content != null) content.alpha = fade * Mathf.Clamp01((open - 0.55f) / 0.4f) * (1f - Mathf.Clamp01(Mathf.Abs(slide) / rowW * 1.3f));
        if (wide != null) wide.alpha = fade * Mathf.Clamp01((expand - 0.5f) / 0.4f);
        if (noteText != null)
        {
            var c = noteText.color;
            float a = NoteAlpha * fade * Mathf.Clamp01((note - 0.45f) / 0.45f);
            if (!Mathf.Approximately(c.a, a)) { c.a = a; noteText.color = c; }
        }
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

    private float Shoulder => island ? 0f : 24f / Mathf.Max(0.01f, shapeImage.pixelsPerUnitMultiplier);

    // 기기 배율이 바뀌면 (처음 · 해상도 바뀜) 그림 · 글 크기와 판 모양
    private void Size()
    {
        sizedUpt = upt;
        float ch = (cutHpt + (island ? 2f : NotchExtra)) * upt;
        shapeImage.sprite = island ? pillSprite : notchSprite;
        shapeImage.type = Image.Type.Sliced;
        // 둥근 모서리는 노치 높이로 정한다 — 알약은 아일랜드와 같은 둥글기, 노치는 아래 모서리 반지름이 그 높이의 0.4 (그림의 반지름 40px). 아래로 늘어나도 그대로
        shapeImage.pixelsPerUnitMultiplier = island ? 126f / ch : 100f / ch;
        // 누르는 자리 — 판보다 양옆 · 아래로 조금 넓게 (음수 = 넓힘 · x 왼 y 아래 z 오른 w 위)
        shapeImage.raycastPadding = new Vector4(-HitSidePt, -HitBelowPt, -HitSidePt, 0f) * upt;
        // 아일랜드 양옆은 오른쪽 칸이 26pt 라 그림을 조금 작게 (시안 날씨 19pt · 화살표 15pt)
        float iconPt = Sides ? SideIconPt : IconPt, arrowPt = Sides ? SideArrowPt : ArrowPt;
        Vector2 icon = Vector2.one * iconPt * upt;
        if (weatherIcon != null) ((RectTransform)weatherIcon.transform).sizeDelta = icon;
        if (pageIcon != null) pageIcon.rectTransform.sizeDelta = icon;
        if (face != null) face.rectTransform.sizeDelta = icon;
        if (faceInitial != null) faceInitial.fontSize = Mathf.RoundToInt(iconPt * 0.52f * upt);
        if (arrow != null) arrow.sizeDelta = Vector2.one * arrowPt * upt;
        if (check != null) check.sizeDelta = Vector2.one * arrowPt * upt;
        if (titleText != null) titleText.fontSize = Mathf.RoundToInt(TitlePt * upt);
        if (hintText != null) hintText.fontSize = Mathf.RoundToInt(HintPt * upt);
        if (clip != null)
        {
            // 양옆 배치는 글 · 그림이 판 끝 가까이 있어 흐리게 자르는 폭을 좁힌다
            float side = Shoulder + (Sides ? 1f : 2f) * upt;
            clip.padding = new Vector4(side, 0f, side, 0f);
            clip.softness = new Vector2Int(Mathf.RoundToInt((Sides ? 2f : 6f) * upt), 0);
        }
        dirty = true;   // 날씨 그림 · 글 크기는 Refresh 에서
    }

    private void Refresh()
    {
        dirty = false;
        bool skyMode = shown == Mode.Sky, guide = shown == Mode.Guide, arrived = guide && arrivedAt >= 0f;
        var it = skyMode && page >= 0 && page < items.Count ? items[page] : default;
        bool weather = skyMode && it.kind == R0926SkyPages.Kind.Weather;
        bool friend = skyMode && it.kind == R0926SkyPages.Kind.Friends;
        bool pic = skyMode && !weather && !friend;
        Show(weatherIcon != null ? weatherIcon.gameObject : null, weather);
        Show(pageIcon != null ? pageIcon.gameObject : null, pic);
        Show(face != null ? face.gameObject : null, friend);
        Show(arrow != null ? arrow.gameObject : null, guide && !arrived);
        Show(check != null ? check.gameObject : null, arrived);
        if (weather && weatherIcon != null && wCode >= 0) weatherIcon.Set(wCode, wDay);
        if (pic && pageIcon != null)
        {
            pageIcon.sprite = it.icon;
            pageIcon.enabled = it.icon != null;
        }
        if (friend && face != null)
        {
            face.color = it.faceColor;
            if (faceInitial != null) faceInitial.text = it.initial ?? "";
            if (facePhoto != null) { facePhoto.texture = it.photo; facePhoto.enabled = it.photo != null; }
        }
        if (rightText != null)
        {
            rightText.text = weather ? (wTemp == int.MinValue ? "" : wTemp + "°") : skyMode ? it.text ?? "" : guide ? distWord : "";
            FitRight(weather);
        }
        if (noteText != null)
        {
            noteText.text = L("터치하면 돌아가요", "Tap to bring it back", "タップで戻ります", "点按即可还原", "Toca para volver");
            Fit(noteText, NotePt, (cutWpt - 12f) * upt);
        }
        if (titleText != null) titleText.text = titleWord;
        if (hintText != null) hintText.text = hintWord;
    }

    private static void Show(GameObject g, bool on)
    {
        if (g != null && g.activeSelf != on) g.SetActive(on);
    }

    // 판 폭에 안 들어가면 글자를 줄인다 (긴 거리 말 — 'a menos de 3km' · 영어 안내 줄 등)
    private void Fit(Text t, float pt, float room, float minPt = MinTextPt)
    {
        if (upt <= 0f) return;
        int size = Mathf.RoundToInt(pt * upt), min = Mathf.RoundToInt(minPt * upt);
        t.fontSize = size;
        while (size > min && t.preferredWidth > room) t.fontSize = --size;
    }

    // 줄 글(기온 · 거리) 크기 — 아일랜드 왼쪽 칸은 시안대로 30pt 안에, 그래도 길면 칸 양끝(2pt)까지 쓰며 9pt 까지
    private void FitRight(bool weatherText)
    {
        if (rightText == null || upt <= 0f) return;
        if (!Sides) { Fit(rightText, TextPt, (cutWpt - 2f * RowPad - IconPt - RowGap) * upt); return; }
        Fit(rightText, weatherText ? SideTextPt : SideDistPt, SideTextRoomPt * upt);
        float room = (SideLeftPt - 2f * SideEdgePt) * upt;
        if (rightText.preferredWidth > room) Fit(rightText, rightText.fontSize / upt, room, SideMinPt);
    }

    private void Layout()
    {
        float cutW = cutWpt * upt, cutH = cutHpt * upt, shoulder = Shoulder, row = RowPt * upt;
        float rootW = ((RectTransform)transform).rect.width;
        float topOpen = island ? (cutTopPt - 1f) * upt : 0f;
        // 판 위 변에서 줄 위 · 아래 변까지 — 아일랜드는 아래 줄 없이 아일랜드 자체가 줄 (위아래 1pt 씩 크게). 그 밑에 '터치하면 돌아가요' · 이름 판
        float rowTop = Sides ? 1f * upt : RowTopPt * upt - topOpen;
        float band = Sides ? rowTop + cutH + 1f * upt : rowTop + row;
        float hOpen = band + note * (NoteLine + NoteBottom) * upt;
        float wOpen = Sides ? cutW + (SideLeftPt + SideRightPt) * upt : cutW;
        float bodyExp = Mathf.Min(Mathf.Max(wOpen, ExpandMinW * upt), rootW - 24f * upt - 2f * shoulder);
        float hExp = band + (ExpandGap + TitleLine + HintLine + ExpandBottom) * upt;
        float bodyClosed = cutW * 0.92f, hClosed = cutH * 0.92f;   // 진짜 노치 뒤에 숨는 크기에서 열린다
        float w = Mathf.LerpUnclamped(Mathf.LerpUnclamped(bodyClosed, wOpen, open), bodyExp, expand);
        float h = Mathf.LerpUnclamped(Mathf.LerpUnclamped(hClosed, hOpen, open), hExp, expand);
        float topClosed = island ? cutTopPt * upt + (cutH - hClosed) * 0.5f : 0f;
        // 아일랜드 양옆은 시계 쪽이 더 넓어 판 가운데가 아일랜드보다 왼쪽 — 넓게 펴지면 다시 화면 가운데
        float shift = Sides ? (SideRightPt - SideLeftPt) * 0.5f * upt * Mathf.Clamp01(open) * (1f - Mathf.Clamp01(expand)) : 0f;
        shape.anchoredPosition = new Vector2(shift, -Mathf.LerpUnclamped(topClosed, topOpen, Mathf.Clamp01(open)));
        shape.sizeDelta = new Vector2(Mathf.Max(0f, w + 2f * shoulder), Mathf.Max(0f, h));

        float textW = rightText != null && rightText.text.Length > 0 ? rightText.preferredWidth : 0f;
        if (Sides)
        {
            // 아일랜드 양옆 — 왼쪽(시계 쪽)에 글, 오른쪽(신호 쪽)에 그림. 자리는 아일랜드 기준(판이 치우친 만큼 되돌림) · 미는 동안은 손가락을 따라 옆으로
            float midY = -(rowTop + cutH * 0.5f), cx = slide - shift;
            // 글 가운데는 아일랜드에서 17.5pt — 긴 말은 칸(40pt) 양끝 2pt 안으로 밀어 넣는다
            float edge = SideEdgePt * upt, leftW = SideLeftPt * upt;
            float at = textW > leftW - 2f * edge ? leftW * 0.5f : Mathf.Clamp(SideTextAtPt * upt, edge + textW * 0.5f, leftW - edge - textW * 0.5f);
            if (leftSlot != null) leftSlot.anchoredPosition = new Vector2(cx + cutW * 0.5f + SideIconAtPt * upt, midY);
            if (rightSlot != null)
            {
                rightSlot.anchoredPosition = new Vector2(cx - cutW * 0.5f - at, midY);
                rightSlot.sizeDelta = new Vector2(textW + 2f * upt, cutH);
            }
        }
        else
        {
            // 줄 — 그림과 글을 한 묶음으로 가운데 (기온은 날씨 그림 바로 옆). 미는 동안은 손가락을 따라 옆으로
            float mid = -(rowTop + row * 0.5f);
            float iconW = (shown == Mode.Guide ? ArrowPt : IconPt) * upt;
            float gap = textW > 0f ? RowGap * upt : 0f;
            float x = -(iconW + gap + textW) * 0.5f + slide;
            if (leftSlot != null) leftSlot.anchoredPosition = new Vector2(x + iconW * 0.5f, mid);
            if (rightSlot != null)
            {
                rightSlot.anchoredPosition = new Vector2(x + iconW + gap + textW * 0.5f, mid);
                rightSlot.sizeDelta = new Vector2(textW + 2f * upt, row);
            }
        }
        if (noteText != null)
        {
            var t = noteText.rectTransform;
            t.anchoredPosition = new Vector2(-shift, -(band + NoteLine * 0.5f * upt));   // 아일랜드 바로 밑 가운데
            t.sizeDelta = new Vector2(cutW, NoteLine * upt);
        }
        if (titleText != null)
        {
            var t = titleText.rectTransform;
            t.anchoredPosition = new Vector2(0f, -(band + (ExpandGap + TitleLine * 0.5f) * upt));
            t.sizeDelta = new Vector2(bodyExp - 28f * upt, TitleLine * upt);
        }
        if (hintText != null)
        {
            var t = hintText.rectTransform;
            t.anchoredPosition = new Vector2(0f, -(band + (ExpandGap + TitleLine + HintLine * 0.5f) * upt));
            t.sizeDelta = new Vector2(bodyExp - 28f * upt, HintLine * upt);
        }

        // 칩 · 알약이 비켜 설 만큼 — 넓게 펴진 판은 잠깐이라 넣지 않는다 (그동안은 판이 위를 덮는다). 아일랜드 양옆은 위쪽 안전영역 안이라 '터치하면 돌아가요' 동안만
        float bottomPt = (Sides ? cutTopPt + cutHpt + 1f : RowTopPt + RowPt) + note * (NoteLine + NoteBottom);
        DropPx = Mathf.Max(0f, bottomPt + DropGap - insetPt) * ppt * Mathf.Clamp01(open);
    }

    // ── 누르기 · 밀기 ────────────────────────────────────────

    public void OnPointerClick(PointerEventData e)
    {
        if (open < 0.5f || sliding || e.dragging) return;   // 민 손가락을 뗀 것 — 누른 게 아니다
        float now = Time.unscaledTime;
        Haptic();
        if (shown == Mode.Guide && guiding)
        {
            // 이름이 보이면(처음 · 도착 · 눌러서 펼친 동안) 끝, 작으면 이름부터
            if (arrivedAt >= 0f || now < expandUntil) { EndGuide(); R0926GuideArrow.Stop(); return; }   // 한 안내 — 아래 화살표도 같이 끝
            titleWord = gName;
            hintWord = HintEnd();
            expandUntil = now + ExpandTime;
            dirty = true;
            return;
        }
        if (shown == Mode.Sky) UnfoldSky();
    }

    // 하늘 판을 화면 위쪽에 고정해 지금 보던 장으로 편다 (누르기 · 아래로 끌기)
    private void UnfoldSky()
    {
        if (sky == null) return;
        Vector3 c = shape.TransformPoint(shape.rect.center);   // 오버레이 캔버스 — 월드 좌표가 곧 화면 픽셀
        var kind = page >= 0 && page < items.Count ? items[page].kind : R0926SkyPages.Kind.Weather;
        sky.UnfoldFromNotch(new Vector2(c.x, c.y), kind);
    }

    // 좌우로 밀면 옆 장 — 끝에서 처음으로 돈다. 하늘 장을 아래로 끌면 편다 (판의 아래쪽에서 시작한 손가락 — 화면 맨 위 끝에서 끄는 건 아이폰 알림 센터라 앱에 오지 않는다)
    public void OnBeginDrag(PointerEventData e)
    {
        Vector2 d = e.position - e.pressPosition;
        if (shown == Mode.Sky && open >= 0.85f && -d.y > Mathf.Abs(d.x))
        {
            Haptic();
            UnfoldSky();
            return;
        }
        if (shown != Mode.Sky || items.Count <= 1 || open < 0.85f || Mathf.Abs(d.x) < Mathf.Abs(d.y)) return;
        sliding = true;
        dragX = e.pressPosition.x;
        lastX = e.position.x;
        lastT = Time.unscaledTime;
        flingV = slideVel = 0f;
        Drag(e.position.x);
    }

    public void OnDrag(PointerEventData e)
    {
        if (sliding) Drag(e.position.x);
    }

    private void Drag(float px)
    {
        float k = UnitsPerPx;
        slide = (px - dragX) * k;
        float dt = Time.unscaledTime - lastT;
        if (dt > 0.0001f) flingV = Mathf.Lerp(flingV, (px - lastX) * k / dt, 0.5f);
        lastX = px;
        lastT = Time.unscaledTime;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!sliding) return;
        sliding = false;
        if (Time.unscaledTime - lastT > 0.12f) flingV = 0f;   // 멈췄다 뗐으면 튕긴 게 아니다
        float rowW = cutWpt * upt, fling = FlingPt * upt;
        if (slide < -rowW * 0.22f || flingV < -fling) Flip(1);
        else if (slide > rowW * 0.22f || flingV > fling) Flip(-1);
        // 덜 밀었으면 제자리로 (Update)
    }

    // 옆 장으로 — 새 장은 손가락 반대쪽에서 들어온다
    private void Flip(int dir)
    {
        if (items.Count <= 1) return;
        page = ((page + dir) % items.Count + items.Count) % items.Count;
        pageKind = items[page].kind;
        slide = dir * cutWpt * upt * 0.6f;
        slideVel = 0f;
        dirty = true;
        Haptic();
    }

    private float UnitsPerPx => ((RectTransform)transform).rect.height / Mathf.Max(1, UnityEngine.Device.Screen.height);

    // ── 글 ───────────────────────────────────────────────────

    internal static string HintEnd() => L("누르면 안내를 끝내요", "Tap to end guidance", "タップで案内を終了", "点按结束导航", "Toca para terminar la guía");

    internal static string Dist(float m)
    {
        var inv = CultureInfo.InvariantCulture;
        if (m < 100f) return Mathf.RoundToInt(m).ToString(inv) + "m";
        if (m < 995f) return (Mathf.RoundToInt(m / 10f) * 10).ToString(inv) + "m";
        if (m < 9950f) return (m / 1000f).ToString("0.0", inv) + "km";
        return Mathf.RoundToInt(m / 1000f).ToString(inv) + "km";
    }

    // 목록 줄 이름 — 글자 꾸밈(<color> 등)은 빼고, 길면 18자에서 자른다
    internal static string Clean(string s)
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
    private static void ResetEditorStatics() { EditorForceNoNotch = false; EditorIsland = false; EditorHeading = null; DropPx = 0f; }
    public void EditorRemeasure() => measured = false;
    public RectTransform EditorShape => shape;
    public bool EditorIslandShape => island;
    public Vector4 EditorHitPadding => shapeImage != null ? shapeImage.raycastPadding : Vector4.zero;
    public float EditorOpen => open;
    public float EditorExpand => expand;
    public float EditorNote => note;
    public bool EditorGuiding => guiding;
    public string EditorMode => shown.ToString();
    public string EditorRight => rightText != null ? rightText.text : null;
    public string EditorTitle => titleText != null ? titleText.text : null;
    public string EditorNoteText => noteText != null ? noteText.text : null;
    public float EditorArrow => arrow != null && arrow.gameObject.activeSelf ? Mathf.DeltaAngle(0f, -arrow.localEulerAngles.z) : float.NaN;
    public bool EditorArrived => arrivedAt >= 0f;
    public Vector2 EditorCutPt => new Vector2(cutWpt, cutHpt);
    public float EditorCutTopPt => cutTopPt;
    public float EditorRowTopPt => RowTopPt;
    public float EditorInsetPt => insetPt;
    public float EditorPpt => ppt;
    public bool EditorBarHidden => barHidden;
    public string EditorHint => hintText != null ? hintText.text : null;
    public bool EditorCheck => check != null && check.gameObject.activeSelf;
    public R0926SkyPages.Kind EditorPage => page >= 0 && page < items.Count ? items[page].kind : R0926SkyPages.Kind.Weather;
    public int EditorPageCount => items.Count;
    public RectTransform EditorLeftSlot => leftSlot;
    public RectTransform EditorRightSlot => rightSlot;
    public bool EditorFaceShown => face != null && face.gameObject.activeInHierarchy;
    public bool EditorIconShown => pageIcon != null && pageIcon.gameObject.activeInHierarchy && pageIcon.sprite != null;
    public bool EditorWeatherShown => weatherIcon != null && weatherIcon.gameObject.activeInHierarchy;
#endif
}
