using Google.XR.ARCoreExtensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// AR 화면 아래쪽 3D 길안내 (11-15 대표님 (B) 「노치에서만 길안내를 하더라도 간단히 방향이나 거리수는 적당히 하단 쪽으로 해서 안내하는 3D모델이 AR화면에서도
/// 발생해서 안내를 지속적으로」 · 시안 B1 「화살표」). 목록에서 장소를 고르면 모든 휴대폰에서 — 땅에 누운 분홍 화살표(R0926GuideArrowMesh)가 그 장소 쪽을
/// 가리키고 위 알약에 「우팡이네집 290m」 — 방향 말(앞 · 옆 · 뒤)은 돌 때마다 바뀌어 산만해 빼고 방향은 화살표만 (대표님 H5 「위의 방향 멘트가 계속 바뀌어 산만」). 방향 · 거리는 노치 안내와 같은 셈: AR 에 떠 있으면 카메라와 오브젝트 사이, 아니면 VPS 방위 → 나침반과 위치.
/// 경로 안내(건당 돈을 내는 길찾기)는 쓰지 않는다 — 곧은 방향만.
///  · 20m 안이면 「거의 다 왔어요」, 10m 안에 1초 머물면 「도착했어요」 뒤 끝 — AR 에 뜬 오브젝트든 좌표로만 안내하는 곳(지도 핀 · 고를 때 AR 에 없던 장소)이든
///    같은 잣대 (11-16 둘째 대표님 「노치 위 표시에서도 10m 거리까지 도착하면 완성 … 두번 도착했다고 나오지 않도록」). GPS 는 몇 m 씩 흔들려 2m 는 닿지 않을 수 있다
///    도착은 여기서 한 번만 정한다 — 같은 곳을 안내하는 노치는 거리도 이것을 쓰고 같은 순간 「도착했어요」(떨림은 여기 한 번), 안내가 끝나면 노치도 같이 끝
///    그 전에 2m 안에 들어와 가까이 카드(R0926NearModal)가 그 오브젝트를 담으면 거기서 끝 — 카드가 이어 받는다
///  · 거리는 땅 위 거리만 — 높이는 보지 않는다 (11-16 대표님 「이동거리의 경우 평면거리로만」)
///  · 시작하는 곳: 목록 줄 · 장소 상세 위 「길안내」(R0926DetailGuide) · 지도 핀 말풍선 「길안내」. 사람에게는 하지 않는다 (11-16 「스토킹 위험 방지」)
///  · 처음 3초는 알약 위에 「누르면 안내를 끝내요」 — 그동안 누르면 끝(노치 안내도 같이), 그 뒤에 누르면 그 말이 다시 3초. 20분이 지나도 끝
///  · 알약 오른쪽 끝 × — 언제든 한 번 누르면 끝, 노치 안내도 같이 (11-16 대표님 「길안내의 경우 하다가 취소할 수 있도록」). 긴 이름은 글을 줄여 × 와 겹치지 않게
///  · 화살표 둘레를 잡고 아래로 밀면 끝 (11-18 대표님 J4 「방향안내 하단 화살표 부분을 아래로 스와이프 → 안내 종료」) — 손가락을 따라 내려가며 옅어지고,
///    200 넘게 내렸거나 빠르게 튕겼으면 끝(노치도 같이 — × 와 같다) · 덜 내렸으면 제자리로. 옆 · 위로 민 것은 그대로.
///    받는 자리는 고리 · 화살표 둘레(R0926DragZone)뿐 — 알약 · × 누름은 그대로, 그 밖은 AR 오브젝트 몫
///  · 창 · 하늘 · 가까이 카드가 떠 있는 동안은 숨고 안내는 이어진다
/// </summary>
public class R0926GuideArrow : MonoBehaviour
{
    [SerializeField] private RectTransform group;      // 화살표 · 알약 · 안내 줄 — 판 가운데가 기준점
    [SerializeField] private CanvasGroup groupAlpha;
    [SerializeField] private R0926GuideArrowMesh arrow;
    [SerializeField] private Button pill;
    [SerializeField] private RectTransform pillRect;
    [Tooltip("알약 오른쪽 끝 × — 한 번에 안내 끝 (없으면 예전처럼 알약만)")]
    [SerializeField] private Button close;
    [Tooltip("화살표 둘레 끌기 자리 — 아래로 밀면 안내 끝 (11-18 J4 · 없으면 밀기 없음)")]
    [SerializeField] private R0926DragZone swipeZone;
    [SerializeField] private Text label;
    [SerializeField] private CanvasGroup hintGroup;
    [SerializeField] private Text hint;
    [Tooltip("이 창들이 떠 있는 동안 숨는다 (물방울메뉴의 GlassCovers + AR 미리보기 카드)")]
    [SerializeField] private GameObject[] covers;
    [SerializeField] private R0926SkyWeather sky;
    [SerializeField] private RectTransform skyCard;     // 아래에 고정한 하늘 카드 — 그 위로 비켜 선다
    [SerializeField] private R0926NearModal near;
    [SerializeField] private RectTransform[] floor;     // 주소줄 · 도크 — 이 위에

    private const float ArriveMeters = 10f, ArriveHold = 1f, ArriveShow = 2.5f, AlmostMeters = 20f, MaxGuideSeconds = 20f * 60f;
    private const float HintFor = 3f, FadeTime = 0.22f, ArMinMeters = 1f;
    // 자리 (캔버스 단위 — 시안 × 3.36): 판 가운데는 바닥(주소줄) 위 300, 알약 가운데는 판 가운데 위 442
    private const float PlaneUp = 300f, PillUp = 442f, PillH = 112f, PillPad = 48f, Above = 600f, TopReserve = 340f, Side = 48f;
    private const float CloseRoom = 114f;   // × 자리 — 동그라미 76 · 오른쪽 18 · 글과 사이 20 (Redesign0926ApplyGuideArrow 의 × 와 같게)
    private const int NameSize = 50, InfoSize = 42;
    // 아래로 밀어 끝내기 (J4) — 시트 밀어 닫기(R0926SwipeDismiss)와 같은 잣대 (캔버스 단위): 200 넘게 내렸거나 · 1600/초로 튕기며 40 넘게. 끝나면 2400/초로 마저 내려간다
    private const float PullEnd = 200f, PullFlick = 1600f, PullFlickMin = 40f, PullAway = 2400f;

    public static R0926GuideArrow Instance { get; private set; }

    /// <summary>목록에서 고른 장소로 안내를 시작한다 — 좌표가 없으면 false</summary>
    public static bool Guide(float lat, float lon, Target target, string name)
        => Instance != null && Instance.isActiveAndEnabled && Instance.StartGuide(lat, lon, target, name);

    /// <summary>안내를 끝낸다 (노치를 눌러 끝냈을 때도)</summary>
    public static void Stop()
    {
        if (Instance != null) Instance.EndGuide();
    }

    /// <summary>이 좌표로 안내하는 중이면 true · 지금 거리(모르면 -1) — 같은 곳을 안내하는 노치는 거리 · 도착을 이것으로 (도착은 한 번)</summary>
    public static bool Leads(float lat, float lon, out float meters)
    {
        var g = Instance;
        bool on = g != null && g.isActiveAndEnabled && g.guiding && g.gLat == lat && g.gLon == lon;
        meters = on ? g.meters : -1f;
        return on;
    }

    private bool guiding;
    private float gLat, gLon;
    private Target gTarget;
    private string gName = "";
    private float gStart, nearSince = -1f, arrivedAt = -1f;
    private float meters = -1f, angle, angleVel;
    private bool hasAngle;
    private bool hintPending;
    private float hintUntil = -10f;
    private float vis, hintVis, arrowVis = 1f;
    private bool placed;
    private float groupY, groupScale = 1f;
    private string shownLabel;
    private readonly Vector3[] corners = new Vector3[4];
    private bool pulling, pullAway;
    private int pullPointer;
    private float pull, pullFrom, pullLastY, pullLastT, pullVel;   // pull — 지금 끌어내린 만큼 (아래 +)

#if !UNITY_EDITOR
    private static AREarthManager earth;
    private static float earthSearchAt;
    private static bool compassFailed;
#endif

    private void Awake()
    {
        Instance = this;
        if (pill != null) pill.onClick.AddListener(OnPill);
        if (close != null) close.onClick.AddListener(OnClose);
        if (swipeZone != null) { swipeZone.onBegin = BeginPull; swipeZone.onDrag = Pull; swipeZone.onEnd = EndPull; }
        if (group != null) group.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private bool StartGuide(float lat, float lon, Target target, string name)
    {
        if (lat == 0f && lon == 0f) return false;
        guiding = true;
        gLat = lat; gLon = lon; gTarget = target; gName = R0926Notch.Clean(name);
        gStart = Time.unscaledTime;
        nearSince = arrivedAt = -1f;
        meters = -1f; hasAngle = false; angleVel = 0f;
        hintPending = true;   // 목록이 내려가 화살표가 보이면 그때부터 3초
        hintUntil = -10f;
        shownLabel = null;
        arrowVis = 1f;
        pull = 0f; pulling = pullAway = false;   // 밀어 끝낸 판이 사라지는 중에 다시 안내해도 제자리에서
        return true;
    }

    private void EndGuide()
    {
        bool was = guiding;
        guiding = false;
        gTarget = null;
        hintPending = false;
        if (was) R0926Notch.GuideEnded(gLat, gLon);   // 한 안내 — 같은 곳을 안내하던 노치도 같이 끝 (도착 뒤 · 20분 · 가까이 카드가 이어 받을 때도)
    }

    private void OnPill()
    {
        if (!guiding) return;
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerLightHaptic();
        float now = Time.unscaledTime;
        if (arrivedAt >= 0f || now < hintUntil)
        {
            EndGuide();
            R0926Notch.StopGuide();   // 한 안내 — 노치도 같이 끝
            return;
        }
        hintUntil = now + HintFor;
    }

    // × — 안내 줄이 없어도 한 번에 끝 (11-16 대표님 「길안내의 경우 하다가 취소할 수 있도록」)
    private void OnClose()
    {
        if (!guiding) return;
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerLightHaptic();
        EndGuide();
        R0926Notch.StopGuide();   // 한 안내 — 노치도 같이 끝
    }

    private void Update()
    {
        if (group == null || groupAlpha == null) return;
        float now = Time.unscaledTime, dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        if (guiding) Track(now, dt);
        bool want = guiding && !Covered();
        vis = Mathf.MoveTowards(vis, want ? 1f : 0f, dt / FadeTime);
        bool on = vis > 0f;
        if (on != group.gameObject.activeSelf)
        {
            group.gameObject.SetActive(on);
            if (on) placed = false;
        }
        if (!on) { pull = 0f; pulling = pullAway = false; return; }
        if (want && hintPending) { hintPending = false; hintUntil = now + HintFor; }
        Settle(dt);
        groupAlpha.alpha = vis * vis * (3f - 2f * vis) * (1f - 0.65f * Mathf.Clamp01(pull / (PullEnd * 1.5f)));   // 끌어내릴수록 옅게
        groupAlpha.blocksRaycasts = want;
        Place(dt);
        Show(now, dt);
    }

    // ── 안내 ─────────────────────────────────────────────────

    private void Track(float now, float dt)
    {
        if (now - gStart > MaxGuideSeconds) { EndGuide(); return; }
        if (arrivedAt >= 0f)
        {
            if (now - arrivedAt > ArriveShow) EndGuide();
            return;
        }
        if (gTarget != null && (near != null ? near.TookOver(gTarget) : R0926NearHold.Held.Contains(gTarget))) { EndGuide(); return; }   // 「발견한 장소」 카드가 이어 받았다 (「근처 장소」 10곳에 든 것은 안내를 이어 간다)

        bool haveLoc = GeoHere.TryGet(out double lat, out double lon, out _);
        float? rel = null;
        float m = -1f;
        var cam = Camera.main;
        if (gTarget != null && gTarget.isActiveAndEnabled && gTarget.IsAnchorReady && cam != null && ArTracking())
        {
            Vector3 v = gTarget.transform.position - cam.transform.position;
            v.y = 0f;
            m = v.magnitude;   // AR 에 떠 있으면 그 오브젝트까지 — 가까이 카드(2m)와 같은 잣대
            if (m > ArMinMeters) rel = Vector3.SignedAngle(CamFlat(cam.transform.rotation), v, Vector3.up);
        }
        if (m < 0f && haveLoc) m = (float)GeoHere.Meters(lat, lon, gLat, gLon);
        if (rel == null && haveLoc)
        {
            float? heading = Heading(cam);
            if (heading != null)
            {
                double dx = (gLon - lon) * System.Math.Cos(lat * System.Math.PI / 180.0), dy = gLat - lat;
                float bearing = (float)(System.Math.Atan2(dx, dy) * 180.0 / System.Math.PI);
                rel = Mathf.DeltaAngle(heading.Value, bearing);
            }
        }
        meters = m;
        if (meters >= 0f && meters <= ArriveMeters)
        {
            if (nearSince < 0f) nearSince = now;
            if (now - nearSince >= ArriveHold) { Arrive(now); return; }
        }
        else nearSince = -1f;
        if (rel != null)
        {
            angle = hasAngle ? Mathf.SmoothDampAngle(angle, rel.Value, ref angleVel, 0.18f, Mathf.Infinity, dt) : rel.Value;
            angle = Mathf.Repeat(angle + 180f, 360f) - 180f;   // SmoothDampAngle 은 ±180° 를 넘어 쌓인다 (뒤를 지나 왼쪽으로 돌면 270°) — 늘 ±180° 안으로
            hasAngle = true;
        }
    }

    private void Arrive(float now)
    {
        arrivedAt = now;
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerLightHaptic();
        R0926Notch.GuideArrived(gLat, gLon);   // 같은 곳을 안내하는 노치도 같은 순간 — 「도착했어요」는 한 번
    }

    private bool Covered()
    {
        if (R0926GuideOverlay.BlocksWorld) return true;
        if (sky != null && sky.SkyShowing) return true;   // 하늘을 올려다보는 동안
        if (near != null && near.Showing) return true;    // 가까이 카드가 가운데에 떠 있는 동안 (그 카드도 바닥 위까지 쓴다)
        if (covers != null)
            foreach (var c in covers)
                if (c != null && c.activeInHierarchy) return true;
        return false;
    }

    // ── 화면 ─────────────────────────────────────────────────

    private void Show(float now, float dt)
    {
        bool arrived = arrivedAt >= 0f;
        if (arrow != null)
        {
            arrowVis = Mathf.MoveTowards(arrowVis, arrived ? 0f : 1f, dt / FadeTime);   // 도착하면 알약만
            arrow.Yaw = angle;
            arrow.Known = hasAngle;
            arrow.FloorY = -PlaneUp;   // 판 가운데에서 바닥(주소줄 위)까지 — 뒤를 가리켜도 꺾쇠가 주소줄을 덮지 않게
            arrow.color = new Color(1f, 1f, 1f, arrowVis);
            arrow.SetVerticesDirty();   // 오르내림 · 꺾쇠 — 보이는 동안 매 프레임
        }
        if (swipeZone != null) swipeZone.raycastTarget = !arrived;   // 도착하면 화살표가 사라진다 — 그 자리는 다시 AR 오브젝트 몫
        string title, info;
        if (arrived) { title = L("도착했어요", "You've arrived", "到着しました", "已到达", "Has llegado"); info = gName; }
        else
        {
            title = meters >= 0f && meters <= AlmostMeters ? L("거의 다 왔어요", "Almost there", "もうすぐです", "快到了", "Ya casi llegas") : gName;
            info = meters >= 0f ? R0926Notch.Dist(meters) : "…";   // 이름 + 거리만 — 방향은 화살표 (H5)
        }
        string text = "<b>" + title + "</b>  <size=" + InfoSize + "><color=#FFFFFFCC>" + info + "</color></size>";
        if (label != null && text != shownLabel)
        {
            shownLabel = text;
            label.text = text;
            if (pillRect != null)
            {
                float maxW = ((RectTransform)transform).rect.width / Mathf.Max(0.01f, groupScale) - Side * 2f;
                float right = close != null ? CloseRoom : PillPad;   // 글 오른쪽 — × 가 있으면 × 자리
                float k = Mathf.Min(1f, (maxW - PillPad - right) / Mathf.Max(1f, label.preferredWidth));   // 긴 이름은 글을 줄여 알약 안에
                var lr = label.rectTransform;
                lr.offsetMin = new Vector2(PillPad, lr.offsetMin.y);
                lr.offsetMax = new Vector2(-right, lr.offsetMax.y);
                lr.localScale = new Vector3(k, k, 1f);
                pillRect.sizeDelta = new Vector2(Mathf.Min(maxW, label.preferredWidth * k + PillPad + right), PillH);
            }
        }
        if (hintGroup != null)
        {
            bool showHint = !arrived && now < hintUntil;
            hintVis = Mathf.MoveTowards(hintVis, showHint ? 1f : 0f, dt / FadeTime);
            hintGroup.alpha = hintVis;
            if (hint != null && showHint) hint.text = R0926Notch.HintEnd();
        }
    }

    // 자리 — 주소줄 · 도크(아래에 고정한 하늘 카드가 있으면 그것) 위. 위가 모자라면(가로 화면) 줄인다
    private void Place(float dt)
    {
        var host = (RectTransform)transform;
        var area = host.rect;
        float k = area.height / Mathf.Max(1f, Screen.height);
        var sa = Screen.safeArea;
        float bottom = area.yMin + sa.yMin * k;
        float top = area.yMax - (Screen.height - sa.yMax) * k - TopReserve;
        if (floor != null)
            foreach (var f in floor)
            {
                if (f == null || !f.gameObject.activeInHierarchy) continue;
                f.GetWorldCorners(corners);
                bottom = Mathf.Max(bottom, host.InverseTransformPoint(corners[1]).y);
            }
        if (sky != null && sky.Pinned && !sky.PinnedTop && skyCard != null && skyCard.gameObject.activeInHierarchy)
        {
            skyCard.GetWorldCorners(corners);
            bottom = Mathf.Max(bottom, host.InverseTransformPoint(corners[1]).y);
        }
        float scale = Mathf.Clamp((top - bottom) / (PlaneUp + Above), 0.5f, 1f);
        float y = bottom + PlaneUp * scale - area.yMin;
        if (!placed) { groupY = y; groupScale = scale; placed = true; }
        else
        {
            float f = 1f - Mathf.Exp(-10f * dt);
            groupY = Mathf.Lerp(groupY, y, f);
            groupScale = Mathf.Lerp(groupScale, scale, f);
        }
        group.anchoredPosition = new Vector2(0f, groupY - pull);   // 끌어내린 만큼 아래로 (J4)
        group.localScale = new Vector3(groupScale, groupScale, 1f);
        if (pillRect != null) pillRect.anchoredPosition = new Vector2(0f, PillUp);
    }

    // ── 아래로 밀어 끝내기 (J4) ─────────────────────────────

    private void BeginPull(PointerEventData e)
    {
        if (!guiding || pullAway || arrivedAt >= 0f) return;
        Vector2 d = e.position - e.pressPosition;
        if (d.y >= 0f || Mathf.Abs(d.y) < Mathf.Abs(d.x)) return;   // 아래로 민 것만 — 옆 · 위로 민 것은 받지 않는다
        if (!HostY(e, out float y)) return;
        pulling = true;
        pullPointer = e.pointerId;
        pullFrom = y + pull;   // 여기서부터 따라 내려온다 (판단 거리만큼 튀지 않게 · 제자리로 돌아오던 중이면 그 자리에서)
        pullLastY = y;
        pullLastT = Time.unscaledTime;
        pullVel = 0f;
    }

    private void Pull(PointerEventData e)
    {
        if (!pulling || e.pointerId != pullPointer) return;
        if (!guiding || Touches() >= 2) { pulling = false; return; }   // 안내가 다른 데서 끝났거나 두 손가락(확대) — 놓고 제자리로
        if (!HostY(e, out float y)) return;
        pull = Mathf.Max(0f, pullFrom - y);
        float dt = Time.unscaledTime - pullLastT;
        if (dt > 0.0001f) pullVel = Mathf.Lerp(pullVel, (pullLastY - y) / dt, 0.5f);
        pullLastY = y;
        pullLastT = Time.unscaledTime;
    }

    private void EndPull(PointerEventData e)
    {
        if (!pulling || e.pointerId != pullPointer) return;
        pulling = false;
        if (Time.unscaledTime - pullLastT > 0.12f) pullVel = 0f;   // 멈췄다 뗐으면 튕긴 게 아니다
        if (!guiding || Covered()) return;
        if (pull > PullEnd || pullVel > PullFlick && pull > PullFlickMin)
        {
            pullAway = true;   // 손을 뗀 자리에서 마저 내려가며 사라진다
            OnClose();         // × 와 같다 — 떨림 · 노치 안내도 같이 끝
        }
    }

    // 손을 뗀 뒤 — 끝냈으면 마저 내려가고, 아니면 제자리로
    private void Settle(float dt)
    {
        if (pulling) return;
        if (pullAway) pull += PullAway * dt;
        else if (pull > 0f)
        {
            pull = Mathf.Lerp(pull, 0f, 1f - Mathf.Exp(-16f * dt));
            if (pull < 0.5f) pull = 0f;
        }
    }

    private bool HostY(PointerEventData e, out float y)
    {
        bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var lp);
        y = lp.y;
        return ok;
    }

    private static int Touches()
    {
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts == null) return 0;
        int n = 0;
        foreach (var t in ts.touches) if (t.isInProgress) n++;
        return n;
    }

    private static string L(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);

    // ── 방향 (R0926Notch 와 같은 셈) ─────────────────────────

    private static bool ArTracking()
    {
#if UNITY_EDITOR
        if (R0926Notch.EditorHeading != null) return false;   // 시험은 방위로
#endif
        return ARSession.state == ARSessionState.SessionTracking;
    }

    // 세워 들면 렌즈 쪽, 눕혀 들면 화면 위쪽이 '앞' — 둘의 수평 성분을 더하면 어느 기울기에서도 같은 쪽
    private static Vector3 CamFlat(Quaternion q)
    {
        Vector3 f = q * Vector3.forward, u = q * Vector3.up;
        var v = new Vector3(f.x + u.x, 0f, f.z + u.z);
        return v.sqrMagnitude < 0.01f ? new Vector3(f.x, 0f, f.z) : v;
    }

    // 기기가 향한 방위 (북 0 · 동 90) — 지구 추적(VPS)이 잡혔으면 그 방향, 아니면 휴대폰 나침반
    private static float? Heading(Camera cam)
    {
#if UNITY_EDITOR
        if (R0926Notch.EditorHeading != null) return R0926Notch.EditorHeading.Value;
        if (cam == null) return null;
        var ef = CamFlat(cam.transform.rotation);
        return Mathf.Atan2(ef.x, ef.z) * Mathf.Rad2Deg;   // 에디터 — 월드 +z 를 북쪽으로
#else
        if (earth == null && Time.unscaledTime >= earthSearchAt)
        {
            earthSearchAt = Time.unscaledTime + 5f;
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
            if (!Input.compass.enabled) { Input.compass.enabled = true; return null; }
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

#if UNITY_EDITOR
    /// <summary>시험용 (Redesign0926PlayTest guidearrow)</summary>
    public bool EditorGuiding => guiding;
    public bool EditorShowing => group != null && group.gameObject.activeInHierarchy && vis >= 0.99f;
    public bool EditorHidden => group == null || !group.gameObject.activeSelf;
    public string EditorLabel => label != null ? label.text : null;
    public float EditorYaw => arrow != null ? arrow.Yaw : float.NaN;
    public bool EditorKnown => arrow != null && arrow.Known;
    public bool EditorHint => hintGroup != null && hintGroup.alpha > 0.9f;
    public bool EditorArrived => arrivedAt >= 0f;
    public float EditorMeters => meters;
    public RectTransform EditorGroup => group;
    public RectTransform EditorPill => pillRect;
    public R0926GuideArrowMesh EditorMesh => arrow;
    public void EditorTapPill() => OnPill();
    public Button EditorClose => close;
    public Text EditorLabelText => label;
    public float EditorPull => pull;
    public R0926DragZone EditorSwipeZone => swipeZone;
#endif
}
