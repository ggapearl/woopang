using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Google.XR.ARCoreExtensions;

/// <summary>
/// 나침반 모드 (11-06 대표님 「'손전등 버튼' 은 하지 않을거고, '나침반 모드' 로 해서 어두운환경에서는 자연스럽게
/// 이 모드로 반영해서 오브젝트가 위치를 잡도록 해주면 좋을 것 같아」).
/// 화면이 어둡거나 하늘·흰 벽만 비춰 AR 위치 추적이 멈추면 저절로 켜진다 — 카메라 방향은 휴대폰 자세(자이로)와 나침반,
/// 자리는 GPS. AR 이 이미 잡아 둔 오브젝트는 그 자리에 두고, 못 잡은 것은 GPS 로 잰 「대략 그 방향」에 놓는다.
/// 밝아져 위치 추적이 돌아오면 원래 방식(지오스페이셜 앵커)으로 돌아간다.
/// 방향 기준: 밝을 때 지구 추적이 잡아 둔 방향(최근 10분)이 있으면 그것을 쓰고 나침반은 자이로가 조금씩 틀어지는 것만 바로잡는다.
/// 처음부터 어두웠으면 나침반(자북 → 편각으로 진북)을 쓴다.
/// </summary>
[DefaultExecutionOrder(-60)]
public class R0926CompassMode : MonoBehaviour
{
    [SerializeField] private Camera arCamera;
    [Tooltip("카메라를 AR 추적으로 움직이는 컴포넌트 (TrackedPoseDriver) — 나침반 모드 동안 끄고 이 스크립트가 돌린다")]
    [SerializeField] private Behaviour poseDriver;
    [SerializeField] private ARSession session;
    [SerializeField] private AREarthManager earth;
    [SerializeField] private LoadingManager loading;

    private const float EnterAfter = 2.5f;       // 어둠이 이만큼 이어지면
    private const float EnterAfterInit = 8f;     // 처음부터 추적이 안 잡힌 채(초기화 중) 이만큼이면
    private const float ArmGiveUp = 6f;          // GPS·센서가 없어 못 켜면 이 뒤로는 원래 어둠 안내에 맡긴다 (뒤에서 계속 시도)
    private const float ExitAfter = 1.5f;        // 위치 추적 + 지구 추적이 이만큼 이어지면 끝
    private const float VioOnlyExit = 15f;       // 위치 추적만 돌아오고 지구 추적이 안 오면
    private const float ReenterGap = 4f;
    private const float CacheMaxAge = 600f;
    private const float TauCompass = 1.5f;       // 나침반만 믿을 때 — 방향을 따라가는 빠르기
    private const float TauCached = 60f;         // 지구 추적 방향이 있을 때 — 자이로 틀어짐만 천천히
    private const float TauAbs = 0.12f;          // 자이로가 없는 기기 — 나침반을 바로 부드럽게
    private const float TauAbsRel = 0.5f;        // 자이로가 있으면 — 「나침반이 자이로보다 몇 도 돌아 있나」를 평균 (돌리는 중에도 늦지 않는다)
    private const float FollowStart = 6f;        // GPS 로 이만큼 움직여야 카메라가 따라간다 (GPS 흔들림)
    private const float FollowTau = 1.5f;
    private const float ToastGap = 300f;
    private const float VehicleKmh = 15f;        // 차로 움직일 땐 켜지 않는다 (LoadingManager 와 같은 기준)

    public static bool Active { get; private set; }
    /// <summary>켜져 있거나 켜려는 중 — LoadingManager 가 이 동안 어둠 안내·fallback 을 띄우지 않는다</summary>
    public static bool Engaged => Active || (instance != null && instance.armStart >= 0f && Time.realtimeSinceStartup - instance.armStart < ArmGiveUp);
    private static R0926CompassMode instance;

#if UNITY_EDITOR
    // 시험용 (Redesign0926PlayTest compass) — null 이면 손대지 않는다
    public static bool? EditorDark;
    public static Quaternion? EditorAbs;   // 자북 기준 카메라 자세 (x 동 · y 위 · z 북)
    public static Quaternion? EditorRel;   // 자이로 자세 (아무 방향 기준)
    public static float? EditorDeclination;
    public static bool EditorCompassThrew;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = false;
        instance = null;
#if UNITY_EDITOR
        EditorDark = null; EditorAbs = null; EditorRel = null; EditorDeclination = null; EditorCompassThrew = false;
#endif
    }

    // 어둠 판정
    private float lostSince = -1f, darkSince = -1f, armStart = -1f, nextTry, exitedAt = -100f, lastToast = -1000f;
    private NotTrackingReason darkReason;
    private float bothOkSince = -1f, vioOnlySince = -1f;
    // 밝을 때 기억해 둔 것 — 지구 기준 방향과 자리, 마지막으로 추적된 카메라와 그때의 자이로
    private bool cacheOk;
    private double cLat, cLon, cAlt;
    private Vector3 cPos;
    private float cPsi, cTime, nextCache;
    private bool havePair;
    private Quaternion pairCam, pairRel;
    // 켜져 있는 동안
    private double rLat, rLon, rAlt, eLat, eLon;
    private Vector3 rPos, eCamPos, camTarget;
    private Quaternion eFrozenRot;
    private float psi, bias, tau, off, nextPlace;
    private bool rAltGood, baseIsRel, following, pdOff;
    // 센서
    private AttitudeSensor relDev, absDev;
    private MagneticFieldSensor magDev;
    private bool relMine, absMine, magMine, compassMine, compassFailed;
    private Vector3 smoothAcc, frameG;
    private int frameGNo = -1;
    private bool frameGOk;
    private Quaternion smoothAbs;
    private bool haveAbs;
    private float absRelOff;
    private bool haveAbsRel;
    private float gyroScore;
    private int gyroSamples;
    private double lastLat, lastLon;
    private bool haveLoc;
    private FilterManager filter;
    private readonly List<CustomARGeospatialCreatorAnchor> tmp = new List<CustomARGeospatialCreatorAnchor>();

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
        if (poseDriver == null && arCamera != null)
            foreach (var b in arCamera.GetComponents<Behaviour>())
                if (b.GetType().Name.Contains("PoseDriver")) { poseDriver = b; break; }
        if (session == null) session = FindFirstObjectByType<ARSession>();
        if (earth == null) earth = FindFirstObjectByType<AREarthManager>();
        if (loading == null) loading = FindFirstObjectByType<LoadingManager>();
    }

    private void OnEnable()
    {
        instance = this;
        FindSensors();
        if (relDev != null && !relDev.enabled) { InputSystem.EnableDevice(relDev); relMine = true; }
    }

    private void OnDisable()
    {
        if (Active) Exit(false);
        Disarm();
        if (relMine && relDev != null && relDev.added) InputSystem.DisableDevice(relDev);
        relMine = false;
        if (instance == this) instance = null;
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        // 돌아오면 AR 세션이 새로 시작된다 — 기억해 둔 방향·자리는 버린다 (복귀는 LoadingManager 의 백그라운드 복구가)
        if (Active) Exit(false); else Disarm();
        cacheOk = false;
        havePair = false;
        lostSince = darkSince = -1f;
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (!Observe(out bool vio, out NotTrackingReason reason, out bool initializing))
        {
            if (Active) Exit(false); else if (armStart >= 0f) Disarm();
            return;
        }
        if (GeoHere.TryGet(out double la, out double lo, out _, out _, earth, false)) { lastLat = la; lastLon = lo; haveLoc = true; }
        bool earthOk = EarthOk();
        Quaternion? rel = RelativeCam();

        if (Active)
        {
            if (vio && earthOk)
            {
                vioOnlySince = -1f;
                if (bothOkSince < 0f) bothOkSince = now;
                else if (now - bothOkSince >= ExitAfter) { Exit(true); return; }
            }
            else if (vio)
            {
                bothOkSince = -1f;
                if (vioOnlySince < 0f) vioOnlySince = now;
                else if (now - vioOnlySince >= VioOnlyExit) { Exit(true); return; }
            }
            else bothOkSince = vioOnlySince = -1f;
            SampleAbs(dt, rel);
            Steer(dt, rel);
            if (now >= nextPlace) { nextPlace = now + 0.25f; PlaceObjects(); }
            return;
        }

        if (vio)
        {
            lostSince = darkSince = -1f;
            if (armStart >= 0f) Disarm();
            if (rel != null && arCamera != null) { pairCam = arCamera.transform.rotation; pairRel = rel.Value; havePair = true; }
            if (earthOk && now >= nextCache) { nextCache = now + 0.25f; UpdateCache(now); }
            return;
        }

        // 추적이 끊겼다
        if (lostSince < 0f) lostSince = now;
        if (reason == NotTrackingReason.InsufficientLight || reason == NotTrackingReason.InsufficientFeatures)
        {
            if (darkSince < 0f) darkSince = now;
            darkReason = reason;
        }
        bool ready = darkSince >= 0f
            ? now - darkSince >= EnterAfter
            : initializing && (reason == NotTrackingReason.Initializing || reason == NotTrackingReason.None) && now - lostSince >= EnterAfterInit;
        if (darkSince < 0f && !ready) return;
        if (now - exitedAt < ReenterGap) return;
        if (loading != null && loading.IsBackgroundRecovering) return;
        if (Speed() >= VehicleKmh) return;
        if (armStart < 0f) Arm(now);
        SampleAbs(dt, rel);
        if (!ready || now < nextTry) return;
        nextTry = now + (now - armStart < ArmGiveUp ? 0.1f : 1f);
        TryEnter(now, rel);
    }

    // ── 켜고 끄기 ──

    private void Arm(float now)
    {
        armStart = now;
        nextTry = now;
        haveAbs = haveAbsRel = false;
        if (absDev != null && absDev != relDev && !absDev.enabled) { InputSystem.EnableDevice(absDev); absMine = true; }
        if (magDev != null && !magDev.enabled) { InputSystem.EnableDevice(magDev); magMine = true; }
        try { if (!Input.compass.enabled) { Input.compass.enabled = true; compassMine = true; } }
        catch (System.Exception) { compassFailed = true; }
#if UNITY_EDITOR
        EditorCompassThrew = compassFailed;
#endif
    }

    private void Disarm()
    {
        armStart = -1f;
        haveAbs = haveAbsRel = false;
        if (absMine && absDev != null && absDev.added) InputSystem.DisableDevice(absDev);
        if (magMine && magDev != null && magDev.added) InputSystem.DisableDevice(magDev);
        absMine = magMine = false;
        if (compassMine) { try { Input.compass.enabled = false; } catch (System.Exception) { } compassMine = false; }
    }

    private void TryEnter(float now, Quaternion? rel)
    {
        if (arCamera == null) return;
        if (!GeoHere.TryGet(out double lat, out double lon, out double alt, out _, earth, false)) return;
        bool cache = cacheOk && now - cTime <= CacheMaxAge;
        Quaternion? abs = AbsEst(rel);
        if (!cache && abs == null) return;
        if (rel == null && abs == null) return;

        Transform ct = arCamera.transform;
        Quaternion frozen = ct.rotation;   // 추적이 끊긴 순간에 멈춰 있는 카메라
        Quaternion b0 = rel ?? abs.Value;
        // 지금 휴대폰이 실제로 보는 쪽 — 끊긴 뒤 돌린 만큼은 자이로가 안다
        Quaternion camNow = rel != null && havePair
            ? Y(Yaw(pairCam * Quaternion.Inverse(pairRel))) * rel.Value
            : Y(Yaw(frozen) - Yaw(b0)) * b0;

        if (cache)
        {
            rLat = cLat; rLon = cLon; rAlt = cAlt; rPos = cPos; psi = cPsi;
            rAltGood = true; tau = TauCached;
            // 나침반이 휴대폰마다 조금씩 틀어져 있다 — 지구 추적 방향에 맞춰 둔다
            bias = abs != null ? Yaw(Y(-psi) * camNow * Quaternion.Inverse(abs.Value)) : 0f;
        }
        else
        {
            rLat = lat; rLon = lon; rAlt = alt; rPos = ct.position;
            psi = Yaw(camNow * Quaternion.Inverse(abs.Value));
            rAltGood = false; tau = TauCompass; bias = 0f;
        }
        eLat = lat; eLon = lon; eCamPos = ct.position; eFrozenRot = frozen; camTarget = eCamPos;
        following = false;
        baseIsRel = rel != null;
        off = Yaw(camNow * Quaternion.Inverse(b0));

        Active = true;
        CustomARGeospatialCreatorAnchor.CompassHold = true;
        bothOkSince = vioOnlySince = -1f;
        if (poseDriver != null && poseDriver.enabled) { poseDriver.enabled = false; pdOff = true; }
        ct.rotation = Y(off) * b0;
        tmp.Clear(); tmp.AddRange(CustomARGeospatialCreatorAnchor.All);
        foreach (var a in tmp) if (a != null) a.CompassFreeze();
        if (loading != null) loading.YieldToCompass();
        PlaceObjects();
        nextPlace = now + 0.25f;
        Toast(now);
    }

    private void Exit(bool resume)
    {
        Active = false;
        CustomARGeospatialCreatorAnchor.CompassHold = false;
        tmp.Clear(); tmp.AddRange(CustomARGeospatialCreatorAnchor.All);
        foreach (var a in tmp) if (a != null) a.CompassRelease();
        if (pdOff && poseDriver != null) poseDriver.enabled = true;
        pdOff = false;
#if UNITY_EDITOR
        if (arCamera != null) arCamera.transform.SetPositionAndRotation(eCamPos, eFrozenRot);
#endif
        exitedAt = Time.realtimeSinceStartup;
        lostSince = darkSince = -1f;
        bothOkSince = vioOnlySince = -1f;
        following = false;
        Disarm();
        if (!resume) { cacheOk = false; havePair = false; }
        else if (loading != null) loading.OnCompassExit();
    }

    // ── 켜져 있는 동안 ──

    private void Steer(float dt, Quaternion? rel)
    {
        if (arCamera == null) return;
        Transform ct = arCamera.transform;
        Quaternion? abs = AbsEst(rel);
        Quaternion b;
        if (rel != null)
        {
            if (!baseIsRel) { off = Yaw(ct.rotation * Quaternion.Inverse(rel.Value)); baseIsRel = true; }
            b = rel.Value;
        }
        else if (abs != null)
        {
            if (baseIsRel) { off = Yaw(ct.rotation * Quaternion.Inverse(abs.Value)); baseIsRel = false; }
            b = abs.Value;
        }
        else return;
        if (abs != null)
        {
            float target = Yaw(Y(psi + bias) * abs.Value * Quaternion.Inverse(b));
            off += Mathf.DeltaAngle(off, target) * (1f - Mathf.Exp(-dt / tau));
        }
        ct.rotation = Y(off) * b;

        // 걸어가면 GPS 로 따라간다
        if (GeoHere.TryGet(out double lat, out double lon, out _, out float acc, earth, false) && acc < 30f)
        {
            Vector3 d = Enu(lat, lon, eLat, eLon);
            if (!following && d.magnitude > FollowStart) following = true;
            if (following) camTarget = eCamPos + Y(psi) * d;
        }
        if (following) ct.position = Vector3.Lerp(ct.position, camTarget, 1f - Mathf.Exp(-dt / FollowTau));
    }

    private void PlaceObjects()
    {
        Quaternion rot = Y(psi);
        tmp.Clear(); tmp.AddRange(CustomARGeospatialCreatorAnchor.All);
        foreach (var a in tmp)
        {
            if (a == null || !a.HasCoordinates || a.HoldsARPose || a.CompassPlaced) continue;
            Vector3 d = Enu(a.Latitude, a.Longitude, rLat, rLon);
            // 높이 — 지구 추적 높이가 있으면 그 차이 (너무 높거나 낮은 건 눈에 보이는 데로) · 없으면 눈높이 조금 아래
            d.y = rAltGood && a.Altitude != 0 ? Mathf.Clamp((float)(a.Altitude - rAlt), -30f, 60f) : -0.5f;
            a.CompassPlace(rPos + rot * d, rot);
        }
    }

    private void Toast(float now)
    {
        if (now - lastToast < ToastGap || ToastManager.Instance == null) return;
        lastToast = now;
        string msg = darkSince >= 0f && darkReason == NotTrackingReason.InsufficientLight
            ? R0926LocalizedText.Say("어두워서 나침반으로 방향을 잡았어요 · 위치는 대략이에요",
                "It's dark, so we're using the compass · positions are approximate",
                "暗いのでコンパスで方向を合わせました · 位置はおおよそです",
                "光线太暗，已改用指南针定向 · 位置为大致位置",
                "Está oscuro, usamos la brújula · las posiciones son aproximadas")
            : R0926LocalizedText.Say("주변이 잘 안 보여 나침반으로 방향을 잡았어요 · 위치는 대략이에요",
                "We can't see much around, so we're using the compass · positions are approximate",
                "周りがよく見えないのでコンパスで方向を合わせました · 位置はおおよそです",
                "周围看不清，已改用指南针定向 · 位置为大致位置",
                "No se ve bien el entorno, usamos la brújula · las posiciones son aproximadas");
        ToastManager.Instance.ShowInfo(msg);
    }

    // ── 상태 ──

    private bool Observe(out bool vio, out NotTrackingReason reason, out bool initializing)
    {
#if UNITY_EDITOR
        if (EditorDark == null) { vio = true; reason = NotTrackingReason.None; initializing = false; return false; }
        vio = EditorDark == false;
        reason = vio ? NotTrackingReason.None : NotTrackingReason.InsufficientLight;
        initializing = !vio;
        return true;
#else
        vio = false; reason = NotTrackingReason.None; initializing = false;
        var sub = session != null ? session.subsystem : null;
        if (sub == null || !sub.running) return false;
        vio = sub.trackingState == TrackingState.Tracking;
        reason = sub.notTrackingReason;
        initializing = ARSession.state == ARSessionState.SessionInitializing;
        return true;
#endif
    }

    private bool EarthOk()
    {
#if UNITY_EDITOR
        return EditorDark == false;
#else
        return earth != null && earth.EarthTrackingState == TrackingState.Tracking;
#endif
    }

    private void UpdateCache(float now)
    {
#if !UNITY_EDITOR
        if (arCamera == null || earth == null) return;
        var pose = earth.CameraGeospatialPose;
        if (pose.OrientationYawAccuracy > 25) return;
        cLat = pose.Latitude; cLon = pose.Longitude; cAlt = pose.Altitude;
        cPos = arCamera.transform.position;
        cPsi = Yaw(arCamera.transform.rotation * Quaternion.Inverse(pose.EunRotation));
        cTime = now;
        cacheOk = true;
#endif
    }

    private float Speed()
    {
        if (filter == null) filter = FindFirstObjectByType<FilterManager>();
        return filter != null ? filter.CurrentSpeedKmh : -1f;
    }

    // ── 센서 ──

    private void FindSensors()
    {
        relDev = absDev = null;
        AttitudeSensor other = null;
        foreach (var d in InputSystem.devices)
        {
            if (!(d is AttitudeSensor a)) continue;
            string n = a.GetType().Name;
            if (n.Contains("GameRotation")) relDev = a;        // 안드로이드 — 나침반 없이 자이로만 (흔들림·자기장 튐 없음)
            else if (n == "AndroidRotationVector") absDev = a; // 안드로이드 — 자북 기준
            else if (other == null) other = a;                 // iOS — 방향 기준은 아무 쪽
        }
        if (relDev == null) relDev = other ?? absDev;
        magDev = MagneticFieldSensor.current;
    }

    private static Quaternion AttToCam(Quaternion q) => Quaternion.Euler(90f, 0f, 0f) * new Quaternion(q.x, q.y, -q.z, -q.w);

    /// <summary>자이로 자세 → 카메라 (방향 기준은 아무 쪽). 중력과 안 맞는 기기는 쓰지 않는다</summary>
    private Quaternion? RelativeCam()
    {
#if UNITY_EDITOR
        if (EditorRel != null) return EditorRel;
#endif
        if (relDev == null || !relDev.enabled) return null;
        Quaternion q = relDev.attitude.ReadValue();
        if (q.x == 0f && q.y == 0f && q.z == 0f) return null;   // 아직 값이 안 왔다 (안드로이드는 w 를 1 로 채운다)
        Quaternion cam = AttToCam(q);
        if (Gravity(out Vector3 g))
        {
            Vector3 down = cam * new Vector3(g.x, g.y, -g.z).normalized;
            float good = Vector3.Angle(down, Vector3.down) < 15f ? 1f : 0f;
            gyroScore = gyroSamples == 0 ? good : Mathf.Lerp(gyroScore, good, 0.05f);
            gyroSamples++;
        }
        return gyroSamples >= 20 && gyroScore > 0.6f ? cam : (Quaternion?)null;
    }

    /// <summary>
    /// 진북 기준 카메라 자세를 잰다 (나침반은 흔들린다). 자이로가 있으면 「나침반 − 자이로」 방위 차이만 평균 낸다 —
    /// 둘이 같이 돌므로 휴대폰을 빨리 돌려도 늦지 않는다 (나침반만 부드럽게 하면 돌릴 때마다 몇 도 밀렸다 돌아온다).
    /// </summary>
    private void SampleAbs(float dt, Quaternion? rel)
    {
        Quaternion? raw = AbsoluteRaw(out bool isTrue);
        if (raw == null) return;
        Quaternion t = isTrue ? raw.Value : Y(Declination()) * raw.Value;
        smoothAbs = haveAbs ? Quaternion.Slerp(smoothAbs, t, 1f - Mathf.Exp(-dt / TauAbs)) : t;
        haveAbs = true;
        if (rel == null) return;
        float s = Yaw(t * Quaternion.Inverse(rel.Value));
        absRelOff = haveAbsRel ? absRelOff + Mathf.DeltaAngle(absRelOff, s) * (1f - Mathf.Exp(-dt / TauAbsRel)) : s;
        haveAbsRel = true;
    }

    private Quaternion? AbsEst(Quaternion? rel)
    {
        if (rel != null && haveAbsRel) return Y(absRelOff) * rel.Value;
        return haveAbs ? smoothAbs : (Quaternion?)null;
    }

    private Quaternion? AbsoluteRaw(out bool isTrue)
    {
        isTrue = false;
#if UNITY_EDITOR
        if (EditorAbs != null) return EditorAbs;
#endif
        if (absDev != null && absDev.enabled)
        {
            Quaternion q = absDev.attitude.ReadValue();
            if (!(q.x == 0f && q.y == 0f && q.z == 0f)) return AttToCam(q);
        }
        Quaternion? mg = MagGravityCam();
        if (mg != null) return mg;
        Quaternion? hc = HeadingCam();
        if (hc != null) { isTrue = true; return hc; }
        return null;
    }

    /// <summary>자기장 + 중력 → 자북 기준 카메라 자세 (기기 좌표: x 오른쪽 · y 위 · z 화면 밖)</summary>
    private Quaternion? MagGravityCam()
    {
        Vector3 m = Vector3.zero;
        if (magDev != null && magDev.enabled) m = magDev.magneticField.ReadValue();
        else if (!compassFailed)
        {
            try { if (Input.compass.enabled) m = Input.compass.rawVector; }
            catch (System.Exception) { compassFailed = true; }
        }
        float mm = m.magnitude;
        if (mm < 10f || mm > 200f) return null;
        m = ScreenComp() * m;   // 중력은 Input System 이 화면 방향을 맞춰 준다 — 자기장도 똑같이
        if (!Gravity(out Vector3 g)) return null;
        Vector3 up = -g.normalized;
        float dip = Vector3.Dot(m, up);
        if (haveLoc && ((lastLat > 15 && dip > 0f) || (lastLat < -15 && dip < 0f))) m = -m;   // 북반구에선 자기장이 아래로 — 반대면 부호가 뒤집힌 기기
        Vector3 east = Vector3.Cross(m, up);
        if (east.sqrMagnitude < 1f) return null;
        east.Normalize();
        Vector3 north = Vector3.Cross(up, east);
        return Quaternion.LookRotation(new Vector3(-east.z, -up.z, -north.z), new Vector3(east.y, up.y, north.y));
    }

    /// <summary>자기장 값을 못 받는 기기 — 휴대폰이 알려 주는 진북 방위 + 중력</summary>
    private Quaternion? HeadingCam()
    {
        if (compassFailed) return null;
        float h;
        try
        {
            if (!Input.compass.enabled || Input.compass.timestamp <= 0) return null;
            h = Input.compass.trueHeading;
        }
        catch (System.Exception) { compassFailed = true; return null; }
        if (!Gravity(out Vector3 g)) return null;
        Quaternion level = Quaternion.FromToRotation(new Vector3(g.x, g.y, -g.z).normalized, Vector3.down);
        return Y(h - Yaw(level)) * level;
    }

    private static Quaternion ScreenComp()
    {
        if (!InputSystem.settings.compensateForScreenOrientation) return Quaternion.identity;
        switch (Screen.orientation)
        {
            case ScreenOrientation.PortraitUpsideDown: return Quaternion.Euler(0, 0, 180);
            case ScreenOrientation.LandscapeLeft: return Quaternion.Euler(0, 0, 90);
            case ScreenOrientation.LandscapeRight: return Quaternion.Euler(0, 0, 270);
        }
        return Quaternion.identity;
    }

    /// <summary>편각 (자북 → 진북, 동쪽 +) — 휴대폰이 알려 주면 그 값, 아니면 한국 평균 서편 8.5°</summary>
    private float Declination()
    {
#if UNITY_EDITOR
        if (EditorDeclination != null) return EditorDeclination.Value;
#endif
        if (!compassFailed)
        {
            try
            {
                var c = Input.compass;
                if (c.enabled && c.timestamp > 0)
                {
                    float d = Mathf.DeltaAngle(c.magneticHeading, c.trueHeading);
                    if (Mathf.Abs(d) > 0.3f && Mathf.Abs(d) < 30f) return d;
                }
            }
            catch (System.Exception) { compassFailed = true; }
        }
        if (haveLoc && lastLat > 33 && lastLat < 39 && lastLon > 124 && lastLon < 132) return -8.5f;
        return 0f;
    }

    /// <summary>중력 (기기 좌표 · 아래 방향) — 한 프레임에 한 번만 읽는다</summary>
    private bool Gravity(out Vector3 g)
    {
        if (frameGNo == Time.frameCount) { g = frameG; return frameGOk; }
        frameGNo = Time.frameCount;
        g = Vector3.zero;
        var gs = GravitySensor.current;
        if (gs != null)
        {
            if (!gs.enabled) InputSystem.EnableDevice(gs);
            g = gs.gravity.ReadValue();
        }
        if (g.sqrMagnitude < 0.01f)
        {
            var acc = Accelerometer.current;
            if (acc != null)
            {
                if (!acc.enabled) InputSystem.EnableDevice(acc);
                smoothAcc = Vector3.Lerp(smoothAcc, acc.acceleration.ReadValue(), 0.15f);
                g = smoothAcc;
            }
        }
        frameG = g;
        frameGOk = g.sqrMagnitude >= 0.01f;
        return frameGOk;
    }

    // ── 계산 ──

    private static Quaternion Y(float deg) => Quaternion.Euler(0f, deg, 0f);

    /// <summary>바라보는 방위 (도) — 하늘이나 땅을 똑바로 봐도 휴대폰 윗변으로 이어서 잰다</summary>
    private static float Yaw(Quaternion q)
    {
        Vector3 f = q * Vector3.forward, u = q * Vector3.up;
        Vector3 v = f - u * f.y;
        if (v.x * v.x + v.z * v.z < 1e-8f) return 0f;
        return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    }

    /// <summary>(lat0, lon0) 에서 본 동쪽·북쪽 거리 (m) — x 동 · z 북</summary>
    private static Vector3 Enu(double lat, double lon, double lat0, double lon0)
    {
        double phi = lat0 * System.Math.PI / 180.0;
        double mLat = 111132.954 - 559.822 * System.Math.Cos(2 * phi) + 1.175 * System.Math.Cos(4 * phi);
        double mLon = 111412.84 * System.Math.Cos(phi) - 93.5 * System.Math.Cos(3 * phi);
        return new Vector3((float)((lon - lon0) * mLon), 0f, (float)((lat - lat0) * mLat));
    }
}
