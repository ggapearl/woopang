using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// 하늘 날씨 — 휴대폰을 하늘로 들면 하늘 먼 곳(약 600m)에 날씨가 걸린다.
///  · 조건: 위로 25° 이상 + 화면 가운데 부근에 60m 안쪽 AR 오브젝트가 없을 것
///  · 가까운 오브젝트가 있으면 오브젝트가 먼저 → 날씨는 위쪽 작은 칩으로 줄어든다
///  · 하늘의 날씨판은 한 번 걸리면 그 방향에 머물러, 휴대폰을 움직이면 하늘에 붙은 것처럼 흘러간다
///  · 밤하늘·천장처럼 특징이 없어 AR 추적이 안 잡히면 카메라 방향이 멈춘다 — 그때는 휴대폰 중력 센서로 기울기를 읽고
///    날씨판은 화면 앞에 붙여 보여 준다 (예전엔 밤·실내에서 하늘을 비춰도 날씨가 안 떴다)
///  · 판을 좌우로 밀면 옆 장 — 근처 보물찾기 · 축제 · 핫플 · 친구 · 언론사 라이브 (R0926SkyPages)
///  · 판을 아래로 끌거나 📌 를 누르면 화면 아래쪽(주소줄 위)에 고정 — 휴대폰을 내려도 남는다. 위로 밀거나 📌 를 다시 누르면 하늘로
///    (10-25 대표님 「조금만 움직여도 해당 내용이 사라질 수 있으니 … 고정」). 한 번 뜬 판은 설정 각도보다 조금 더 내려야 사라진다
/// 데이터: 우팡 서버 /api/weather (10분 캐시). 스폰 시스템과 따로 돌아 기존 오브젝트 로직을 건드리지 않는다.
/// </summary>
public class R0926SkyWeather : MonoBehaviour
{
    [Header("하늘 날씨판 (월드 공간 캔버스)")]
    [SerializeField] private Transform skyBoard;
    [SerializeField] private CanvasGroup skyGroup;
    [SerializeField] private Text tempText;
    [SerializeField] private Text condText;
    [SerializeField] private Text detailText;
    [Tooltip("2줄이 한 줄에 안 들어가면 이 크기까지 줄인다 (넘치면 뒷부분이 잘려 안 보인다)")]
    [SerializeField] private int detailMinFont = 34;
    [SerializeField] private R0926WeatherIcon icon;       // 기온 왼쪽의 날씨 그림
    [Tooltip("판 위의 지역 이름 — '예산군 대흥면' (LocationManager.ShortRegion)")]
    [SerializeField] private Text regionText;
    [Tooltip("지역 이름의 바탕 알약 — 이름이 없으면 숨고, 폭은 이름 길이에 맞춘다 (regionText 는 이 안에 핀 오른쪽으로 늘어나 있다)")]
    [SerializeField] private RectTransform regionPill;

    [Header("3시간 후 · 6시간 후 · 내일")]
    [SerializeField] private GameObject forecast;
    [SerializeField] private Text[] fcLabels;
    [SerializeField] private R0926WeatherIcon[] fcIcons;
    [SerializeField] private Text[] fcTemps;

    [Header("위쪽 작은 칩 — 판이 떠 있으면 '접기', 접혔거나 오브젝트 우선이면 날씨 + '펼치기'")]
    [SerializeField] private CanvasGroup chipGroup;
    [SerializeField] private Text chipText;
    [SerializeField] private RectTransform chipArrow;
    [SerializeField] private RectTransform chipRect;
    [Tooltip("이것들이 떠 있는 동안 칩은 숨는다 — 노치 아래 같은 줄을 쓰는 확대/축소 표시 · 아이콘 줄을 칩 바로 아래까지 올린 장소 상세")]
    [SerializeField] private GameObject[] chipWaitFor;

    [Header("조건 (보이는 각도는 설정 탭 — R0926SkySettings)")]
    [SerializeField] private float nearObjectDistance = 60f;
    [Tooltip("이 각도 이상으로 들고 거의 움직이지 않으면 누워서 쓰는 것으로 본다")]
    [SerializeField] private float lyingPitch = 55f;
    [SerializeField] private float lyingSeconds = 6f;
    [SerializeField] private float boardDistance = 600f;
    [Tooltip("하늘에서 날씨판이 차지하는 폭 (시야각 도)")]
    [SerializeField] private float boardFov = 42f;
    [SerializeField] private float refreshMinutes = 10f;
    [Tooltip("한 번 뜬 판은 설정 각도보다 이만큼 더 내려야 사라진다 (경계에서 깜빡이지 않게)")]
    [SerializeField] private float hideSlack = 8f;

    [Header("고정 — 판의 틀(Frame0926)만 화면 UI 로 옮겨 붙인다")]
    [SerializeField] private RectTransform frame;
    [SerializeField] private CanvasGroup frameGroup;
    [Tooltip("화면 UI 의 고정 자리 (SkyPin0926 — 장소 표시 위 · 도크와 창 아래, 아래 가운데 기준)")]
    [SerializeField] private RectTransform pinLayer;
    [Tooltip("판 전체에서 손가락을 받는 판 — 고정했을 때만 유리로 보인다 (카메라 화면 위에서도 글이 읽히게)")]
    [SerializeField] private Image glass;
    [Tooltip("고정한 판은 이것들 위에 놓는다 (주소줄 · 도크)")]
    [SerializeField] private RectTransform[] pinFloor;
    [Tooltip("고정한 판의 유리 폭 — 화면 폭의 이만큼")]
    [SerializeField] private float pinWidth = 0.86f;
    [SerializeField] private float pinGap = 24f;

    private Camera cam;
    private bool hasData;
    private bool anchored;
    private float nextFetch;
    private bool fetching;
    // 마지막으로 받은 날씨와 자리 — 앱을 켜자마자 하늘을 비춰도 바로 보이게 (예전엔 GPS 가 잡히기 전 첫 시도를 버리고 30초 뒤에야 다시 받았다)
    private const string CacheJson = "SkyWeather_json", CacheAt = "SkyWeather_at", CacheLat = "SkyWeather_lat", CacheLon = "SkyWeather_lon";
    private const double CacheHours = 0.5;   // 오래된 날씨(예: 비 오기 전 '맑음')를 켜자마자 보여 주지 않게 30분까지만
    private float lookUpSince = -1f;
    private readonly List<GameObject> scratch = new List<GameObject>(64);

    [Serializable]
    private class Weather
    {
        public float temp;
        public int code;
        public bool is_day;
        public float wind;
        public float precip_prob = -1;
        public string sunrise;
        public string sunset;
        public float tomorrow_min = -99;
        public float tomorrow_max = -99;
        public int tomorrow_code = -1;
        public float h3_temp = -99;
        public int h3_code = -1;
        public bool h3_day = true;
        public float h6_temp = -99;
        public int h6_code = -1;
        public bool h6_day = true;
        public float pm10 = -1;
        public float moon;
        // 기상청 초단기실황 (서버가 보내면 '지금' 날씨는 이쪽을 따른다 — 예보 모델은 국지적인 비를 놓친다)
        public int pty = -1;      // 강수형태 0 없음 · 1 비 · 2 비/눈 · 3 눈 · 5 빗방울 · 6 빗방울눈날림 · 7 눈날림
        public float rn1 = -1;    // 1시간 강수량 (mm)
        public float humidity = -1;   // 상대습도 % — 서버가 아직 안 주면 -1 (줄에서 뺀다)
        public string region;         // 판 위 지역 이름 — 서버가 카카오 행정구역으로 ('예산군 대흥면'). 한국 밖·예전 서버면 비어 있다
    }

    private string serverRegion;      // 있으면 기기 주소(LocationManager.ShortRegion — OSM, 시골에선 면이 빠진다)보다 먼저

    private bool collapsed;          // 사용자가 '접기' — 앱을 켜는 동안 유지 (처음 상태는 설정)
    private bool lying, lyingOverride;
    private float highSince = -1f;
    private Vector3 highPos;
    private bool boardShowing;
    private bool skyShow;            // 하늘에 걸린 판이 보일 때 (고정과 상관없이 — 풀면 이 자리로 돌아간다)
    private float holdUntil;
    private bool pinned;
    private float pinT = 1f;         // 고정 · 풀기로 옮기는 중 0 → 1
    private Vector2 fromPos;
    private float fromScale, fromAlpha;
    private Color glassColor;
    private Vector2 frameSize;
    private float pull;              // 위아래로 끄는 손가락을 따라간 만큼 (틀 단위 — R0926SkyPages)
    private const float PinTime = 0.34f;
    private readonly Vector3[] corners = new Vector3[4];
    private string lastChip;
    private float pillSince = -1f;
    private const float PillWaitMax = 30f;   // 알약이 이보다 오래 안 걷히면(최종 개수가 안 온 경우 등) 칩이 더 기다리지 않는다

    private void Start()
    {
        collapsed = R0926SkySettings.StartCollapsed;
        ShowRegion(LocationManager.ShortRegion);
        if (skyGroup != null) skyGroup.alpha = 0f;
        if (chipGroup != null) chipGroup.alpha = 0f;
        if (skyBoard != null) skyBoard.gameObject.SetActive(false);
        if (frame != null) frameSize = frame.rect.size;
        if (glass != null) { glassColor = glass.color; SetGlass(0f); }
        LoadCache();
    }

    private void OnEnable() { LocationManager.ShortRegionChanged += ShowRegion; }
    private void OnDisable() { LocationManager.ShortRegionChanged -= ShowRegion; }

#if UNITY_EDITOR
    /// <summary>스토어 캡처(Redesign0926PlayTest)용 — 판의 지역 이름을 이 글로 고정 (에디터 위치가 찍히지 않게). null 이면 실제 지역</summary>
    public static string EditorRegionOverride;
    public void EditorRefreshRegion() => ShowRegion(LocationManager.ShortRegion);
#endif

    private void ShowRegion(string region)
    {
        if (!string.IsNullOrEmpty(serverRegion)) region = serverRegion;
#if UNITY_EDITOR
        if (EditorRegionOverride != null) region = EditorRegionOverride;
#endif
        if (regionText == null) return;
        bool has = !string.IsNullOrEmpty(region);
        regionText.text = region ?? "";
        regionText.enabled = has;
        if (regionPill == null) return;
        if (regionPill.gameObject.activeSelf != has) regionPill.gameObject.SetActive(has);
        if (!has) return;
        var lr = regionText.rectTransform;   // 알약 폭 = 핀 쪽 안쪽 여백 + 이름 + 오른쪽 여백
        regionPill.sizeDelta = new Vector2(lr.offsetMin.x - lr.offsetMax.x + regionText.preferredWidth, regionPill.sizeDelta.y);
    }

    /// <summary>
    /// '지금' 날씨 코드 — 기상청 실황 강수형태(PTY)가 오면 그걸 따른다.
    /// 실황이 '강수 없음'이면 모델이 비라고 해도 흐림으로, 실황이 비면 모델이 맑음이라도 비로.
    /// </summary>
    private static int NowCode(Weather w)
    {
        bool wet = w.code >= 51 && w.code <= 67 || w.code >= 71 && w.code <= 82 || w.code >= 85;
        switch (w.pty)
        {
            case 0: return wet ? 3 : w.code;
            case 1: return w.code >= 61 && w.code <= 65 || w.code >= 80 && w.code <= 82 || w.code >= 95 ? w.code : 61;
            case 2: case 6: return 66;
            case 3: case 7: return 71;
            case 5: return 51;
            default: return w.code;   // 서버가 실황을 안 보냄
        }
    }

    private void LoadCache()
    {
        string json = PlayerPrefs.GetString(CacheJson, "");
        if (string.IsNullOrEmpty(json)) return;
        if (!long.TryParse(PlayerPrefs.GetString(CacheAt, "0"), out long at)) return;
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - at > CacheHours * 3600) return;
        try
        {
            var w = JsonUtility.FromJson<Weather>(json);
            if (w == null) return;
            Show(w);
            hasData = true;
        }
        catch (Exception) { }
    }

    /// <summary>날씨를 받아 판에 그릴 수 있다 (설정에서 날씨를 껐으면 false) — 하늘 옆 장이 날씨 장을 넣을지 본다</summary>
    public bool WeatherReady => R0926SkySettings.Enabled && R0926SkySettings.Weather && hasData;
    /// <summary>휴대폰이 수평보다 몇 도 위를 보는지 (마지막 프레임) — 하늘 옆 장이 미리 받아 둘 때를 본다</summary>
    public float Pitch { get; private set; }

    /// <summary>판을 화면에 고정했는지 — 휴대폰을 내려도 화면 아래쪽에 남는다</summary>
    public bool Pinned => pinned;

    /// <summary>
    /// 고정 · 풀기 (판의 📌 · 판을 아래로 끌기 / 위로 밀기 — R0926SkyPages). 떠 있는 판만 고정한다.
    /// 고정하면 지금 보이는 자리에서 화면 아래쪽으로 내려오고, 풀면 하늘 자리로 돌아간다 (하늘을 안 보고 있으면 위로 날아가며 사라진다)
    /// </summary>
    public void SetPinned(bool on)
    {
        if (on == pinned || frame == null || frameGroup == null || pinLayer == null || glass == null || cam == null) return;
        if (on && !skyShow) return;
        pinned = on;
        pinT = 0f;
        if (on)
        {
            if (!ScreenRect(frame, out fromPos, out fromScale)) PinnedRect(out fromPos, out fromScale);
            fromAlpha = skyGroup != null ? skyGroup.alpha : 1f;
            frame.SetParent(pinLayer, false);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0f);   // 하늘에선 판에 꽉 차게 늘어나 있다 — 크기를 직접
            frame.sizeDelta = frameSize;
            frame.localRotation = Quaternion.identity;
            collapsed = false;
        }
        else
        {
            fromPos = frame.anchoredPosition;
            fromScale = frame.localScale.x;
            fromAlpha = frameGroup.alpha;
        }
        UpdatePin();
    }

    /// <summary>판을 만지는 중 — 잠깐 기울어져도 사라지지 않게 (미는 손에 휴대폰이 같이 기운다)</summary>
    public void Hold() => holdUntil = Time.unscaledTime + 1.2f;

    /// <summary>위아래로 끄는 동안 판이 손가락을 따라 내려오거나 올라간 만큼 (틀 단위)</summary>
    public void SetPull(float y) => pull = y;

    /// <summary>위쪽 칩을 누르면 — 판이 떠 있으면 접고, 접혀 있으면 편다</summary>
    public void ToggleCollapse()
    {
        if (boardShowing) { collapsed = true; SetPinned(false); }
        else
        {
            collapsed = false;
            if (lying) lyingOverride = true;   // 누워 있다고 봤어도 직접 펴면 따른다
        }
    }

    private void Update()
    {
        // 판에는 날씨 장과 옆 장(근처 보물찾기 · 축제 · 핫플 · 친구 · 언론사 라이브 — R0926SkyPages)이 있다.
        // 날씨를 켰으면 날씨가 올 때까지 기다리고(장 순서가 뒤늦게 밀리지 않게), 날씨를 끄고 옆 장만 켰으면 바로 띄운다
        bool weatherOn = R0926SkySettings.Enabled && R0926SkySettings.Weather;
        bool eventsOn = R0926SkySettings.Enabled && R0926SkySettings.Events;
        if (weatherOn && !fetching && Time.unscaledTime >= nextFetch) StartCoroutine(Fetch());
        bool ready = weatherOn ? hasData : eventsOn;
        if (cam == null) cam = Camera.main;
        if (cam == null || skyBoard == null) return;

        // AR 이 자리를 잡았을 때만 카메라 방향·위치를 믿는다
        bool tracking = ARSession.state == ARSessionState.SessionTracking;
        Vector3 fwd = cam.transform.forward;
        float camPitch = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
        float pitch = SensorPitch() ?? camPitch;
        Pitch = pitch;
        // 한 번 뜬 판은 설정 각도보다 hideSlack 더 내려야 사라진다 — 경계에서 조금만 기울여도 꺼지던 것
        bool up = pitch > R0926SkySettings.MinPitch - (skyShow ? hideSlack : 0f);
        lookUpSince = up ? (lookUpSince < 0f ? Time.unscaledTime : lookUpSince) : -1f;
        bool settled = up && Time.unscaledTime - lookUpSince > 0.6f;   // 잠깐 스친 건 무시
        if (tracking) UpdateLying(pitch);
        else
        {
            // 추적이 안 되면 카메라 위치가 멈춰 '안 움직인다 = 누워 있다'로 잘못 봤다 — 이때는 누움 판정을 하지 않는다
            highSince = -1f;
            if (pitch < 30f) { lying = false; lyingOverride = false; }
        }
        bool held = skyShow && Time.unscaledTime < holdUntil;   // 판을 만지는 중
        bool near = tracking && settled && !held && NearObjectInView();
        bool lyingHide = lying && !lyingOverride && R0926SkySettings.HideWhenLying;

        skyShow = ready && (settled || held) && !near && !collapsed && !lyingHide;
        if (pinned && !ready) SetPinned(false);   // 설정에서 하늘을 껐다
        // 칩은 노치 아래 상태 알약('N곳을 찾았어요' 등)과 같은 자리 — 알약이 걷힐 때까지 기다린다 (앱을 켜자마자 둘이 겹치던 것)
        bool pill = R0926StatusPill.AnyShowing;
        if (!pill) pillSince = -1f; else if (pillSince < 0f) pillSince = Time.unscaledTime;
        bool covered = false;
        if (chipWaitFor != null) foreach (var g in chipWaitFor) if (g != null && g.activeInHierarchy) { covered = true; break; }
        bool showChip = ready && settled && !covered && !(pill && Time.unscaledTime - pillSince < PillWaitMax);
        boardShowing = skyShow || pinned;
        UpdateChip(boardShowing);

        if (!tracking)
        {
            // 화면 앞에 붙인다 — 카메라가 멈춰 있어도 늘 화면 가운데 보인다
            anchored = false;
            if (skyShow || skyBoard.gameObject.activeSelf) PinToCamera();
        }
        else if (skyShow && !anchored) Anchor(fwd, camPitch);
        if (!up && !held) anchored = false;
        if (tracking && anchored)
        {
            // 너무 옆으로 돌아서면(70° 이상) 새 방향으로 다시 건다
            Vector3 toBoard = (skyBoard.position - cam.transform.position).normalized;
            if (Vector3.Angle(Flat(toBoard), Flat(fwd)) > 70f) Anchor(fwd, camPitch);
            skyBoard.rotation = Quaternion.LookRotation(skyBoard.position - cam.transform.position);
        }

        Fade(skyGroup, skyShow, 1.6f);
        if (covered && chipGroup != null) chipGroup.alpha = 0f;   // 같은 자리에 확대/축소 표시가 곧장 뜬다 — 서서히 사라지면 글자가 겹친다
        else Fade(chipGroup, showChip, 3f);
        if (chipGroup != null) { bool touch = chipGroup.alpha > 0.5f; chipGroup.blocksRaycasts = touch; chipGroup.interactable = touch; }
        // 판도 또렷이 보일 때만 손가락을 받는다 (옆으로 밀기 · 줄 누르기) — 사라지는 중엔 뒤의 AR 을 누를 수 있게
        if (skyGroup != null) { bool touch = skyShow && skyGroup.alpha > 0.5f; skyGroup.blocksRaycasts = touch; skyGroup.interactable = touch; }
        bool boardVisible = skyGroup != null && skyGroup.alpha > 0.001f;
        if (skyBoard.gameObject.activeSelf != boardVisible) skyBoard.gameObject.SetActive(boardVisible);
        UpdatePin();   // 하늘 자리가 이번 프레임에 정해진 뒤 — 풀 때 그 자리로 간다
    }

    // ── 고정 ─────────────────────────────────────────────────
    // 고정하면 틀이 화면 UI 의 고정 자리(pinLayer)로 옮겨 가 화면 아래쪽(주소줄 위)에 머문다.
    // 하늘의 빈 판(skyBoard)은 그동안에도 평소처럼 걸리고 사라진다 — 풀면 그 자리로 돌아가 이어진다
    private void UpdatePin()
    {
        if (frame == null || frameGroup == null || pinLayer == null || glass == null) return;
        if (!pinned && pinT >= 1f)
        {
            // 하늘에 있다 — 끄는 손가락만 따라간다
            if (!Mathf.Approximately(frame.anchoredPosition.y, pull)) frame.anchoredPosition = new Vector2(0f, pull);
            return;
        }
        pinT = Mathf.Min(1f, pinT + Time.unscaledDeltaTime / PinTime);
        float e = 1f - (1f - pinT) * (1f - pinT) * (1f - pinT);   // 빨리 떠나 살며시 닿는다
        Vector2 toPos;
        float toScale, toAlpha = 1f;
        if (pinned) { PinnedRect(out toPos, out toScale); toPos.y += pull * toScale; }
        else if (skyShow && ScreenRect(skyBoard, out toPos, out toScale)) toAlpha = skyGroup != null ? skyGroup.alpha : 1f;
        else { toPos = fromPos + new Vector2(0f, pinLayer.rect.height * 0.35f); toScale = fromScale * 0.9f; toAlpha = 0f; }   // 하늘을 안 보고 있으면 위로 날아가며 사라진다
        frame.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, e);
        float s = Mathf.LerpUnclamped(fromScale, toScale, e);
        frame.localScale = new Vector3(s, s, 1f);
        frameGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, e);
        SetGlass(pinned ? e : 1f - e);
        bool touch = pinned && pinT > 0.6f;
        frameGroup.blocksRaycasts = touch;
        frameGroup.interactable = touch;
        if (!pinned && pinT >= 1f) BackToSky();
    }

    private void BackToSky()
    {
        frame.SetParent(skyBoard, false);
        frame.anchorMin = Vector2.zero;   // 판에 꽉 차게 — 판 크기가 바뀌어도 따라간다
        frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        frame.localScale = Vector3.one;
        frame.localRotation = Quaternion.identity;
        frameGroup.alpha = 1f;
        frameGroup.blocksRaycasts = frameGroup.interactable = true;   // 하늘에선 판(skyGroup)이 막고 연다
        SetGlass(0f);
    }

    // 고정 자리 — 화면 아래쪽 가운데, 주소줄 · 도크 바로 위. 유리 폭이 화면 폭의 pinWidth (가로 화면에선 높이의 42% 까지)
    private void PinnedRect(out Vector2 pos, out float scale)
    {
        var g = glass.rectTransform;
        float bottom = g.localPosition.y + g.rect.yMin, top = g.localPosition.y + g.rect.yMax;
        var area = pinLayer.rect;
        scale = Mathf.Min(area.width * pinWidth / Mathf.Max(1f, g.rect.width), area.height * 0.42f / Mathf.Max(1f, top - bottom));
        pos = new Vector2(0f, Floor() + pinGap - bottom * scale);
    }

    // 주소줄 · 도크 중 가장 높은 윗변 — pinLayer 아래 변에서 위로
    private float Floor()
    {
        float floor = 0f;
        if (pinFloor == null) return floor;
        foreach (var f in pinFloor)
        {
            if (f == null || !f.gameObject.activeInHierarchy) continue;
            f.GetWorldCorners(corners);
            floor = Mathf.Max(floor, pinLayer.InverseTransformPoint(corners[1]).y - pinLayer.rect.yMin);
        }
        return floor;
    }

    // 하늘의 판(또는 틀)이 지금 화면에서 차지하는 자리 — 고정 자리와 같은 좌표 (pinLayer 아래 가운데 기준 · 배율)
    private bool ScreenRect(Transform t, out Vector2 pos, out float scale)
    {
        pos = Vector2.zero;
        scale = 1f;
        if (frameSize.x <= 0f) return false;
        Vector3 c = cam.WorldToScreenPoint(t.position);
        Vector3 r = cam.WorldToScreenPoint(t.TransformPoint(new Vector3(frameSize.x * 0.5f, 0f, 0f)));
        if (c.z <= 0f || r.z <= 0f) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(pinLayer, c, null, out var lp)) return false;
        var area = pinLayer.rect;
        pos = lp - new Vector2(area.center.x, area.yMin);
        scale = Vector2.Distance(c, r) / Mathf.Max(0.0001f, pinLayer.lossyScale.x) / (frameSize.x * 0.5f);
        return true;
    }

    private void SetGlass(float k)
    {
        if (glass == null) return;
        var c = glassColor;
        c.a *= Mathf.Clamp01(k);
        glass.color = c;
    }

    // 누워서 위로 들고 쓰는 경우(천장) — 크게 기울인 채 거의 움직이지 않으면
    private void UpdateLying(float pitch)
    {
        if (pitch < 30f) { highSince = -1f; lying = false; lyingOverride = false; return; }
        if (pitch < lyingPitch) { highSince = -1f; return; }
        Vector3 p = cam.transform.position;
        if (highSince < 0f || (p - highPos).sqrMagnitude > 0.25f * 0.25f) { highSince = Time.unscaledTime; highPos = p; return; }
        if (Time.unscaledTime - highSince > lyingSeconds) lying = true;
    }

    private void UpdateChip(bool boardUp)
    {
        if (chipText == null) return;
        string lang = R0926LocalizedText.Lang();
        string text = boardUp ? L(lang, "하늘 접기", "Hide sky", "空をたたむ", "收起天空", "Ocultar cielo")
                    : WeatherReady && weatherChip.Length > 0 ? weatherChip
                    : L(lang, "하늘 펼치기", "Show sky", "空を広げる", "展开天空", "Mostrar cielo");   // 날씨를 끄고 옆 장만 볼 때
        if (text == lastChip) return;
        lastChip = text;
        chipText.text = text;
        if (chipArrow != null) chipArrow.localEulerAngles = new Vector3(0f, 0f, boardUp ? 180f : 0f);   // 위 화살표 = 접기, 아래 = 펼치기
        if (chipRect != null) chipRect.sizeDelta = new Vector2(76f + chipText.preferredWidth + 18f + 58f, chipRect.sizeDelta.y);
    }

    private string weatherChip = "";

    private void Anchor(Vector3 fwd, float pitch)
    {
        float elev = Mathf.Clamp(pitch, 30f, 60f) * Mathf.Deg2Rad;
        Vector3 flat = Flat(fwd);
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
        Vector3 dir = (flat * Mathf.Cos(elev) + Vector3.up * Mathf.Sin(elev)).normalized;
        float d = Mathf.Min(boardDistance, cam.farClipPlane * 0.7f);
        skyBoard.position = cam.transform.position + dir * d;
        skyBoard.rotation = Quaternion.LookRotation(dir);
        // 캔버스 폭 1000 기준으로 원하는 시야각만큼 보이게
        float width = 2f * d * Mathf.Tan(boardFov * 0.5f * Mathf.Deg2Rad);
        float k = width / 1000f;
        skyBoard.localScale = new Vector3(k, k, k);
        anchored = true;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }

    private void PinToCamera()
    {
        float d = Mathf.Min(boardDistance, cam.farClipPlane * 0.7f);
        skyBoard.position = cam.transform.position + cam.transform.forward * d;
        skyBoard.rotation = Quaternion.LookRotation(cam.transform.forward);
        float k = 2f * d * Mathf.Tan(boardFov * 0.5f * Mathf.Deg2Rad) / 1000f;
        skyBoard.localScale = new Vector3(k, k, k);
    }

    /// <summary>
    /// 휴대폰 기울기(카메라가 수평보다 몇 도 위를 보는지) — 중력 센서(없으면 가속도계). AR 추적과 상관없이 늘 맞다.
    /// 기기 좌표에서 z 는 화면 밖(사용자 쪽), 뒤 카메라는 -z 를 본다 → 위를 볼수록 중력의 z 가 +1 에 가깝다
    /// </summary>
    private Vector3 smoothedAcc;
    private float? SensorPitch()
    {
        Vector3 g = Vector3.zero;
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
                smoothedAcc = Vector3.Lerp(smoothedAcc, acc.acceleration.ReadValue(), 0.15f);   // 손떨림을 걸러 중력만
                g = smoothedAcc;
            }
        }
        if (g.sqrMagnitude < 0.01f) return null;
        return Mathf.Asin(Mathf.Clamp(g.normalized.z, -1f, 1f)) * Mathf.Rad2Deg;
    }

    private static void Fade(CanvasGroup g, bool show, float speed)
    {
        if (g == null) return;
        float target = show ? 1f : 0f;
        if (!Mathf.Approximately(g.alpha, target))
            g.alpha = Mathf.MoveTowards(g.alpha, target, Time.unscaledDeltaTime * speed);
    }

    private bool NearObjectInView()
    {
        Vector3 eye = cam.transform.position;
        if (Check(DataManager.Instance?.GetSpawnedObjects()?.Values, eye)) return true;
        if (Check(TourAPIManager.Instance?.GetSpawnedObjects()?.Values, eye)) return true;
        if (Check(SubwayManager.Instance?.GetSpawnedObjects()?.Values, eye)) return true;
        if (Check(TrainStationManager.Instance?.GetSpawnedObjects()?.Values, eye)) return true;
        if (Check(TerminalManager.Instance?.GetSpawnedObjects()?.Values, eye)) return true;
        if (Check(R0926HuntTreasure.SpawnedObjects.Values, eye)) return true;
        return false;
    }

    private bool Check(IEnumerable<GameObject> objs, Vector3 eye)
    {
        if (objs == null) return false;
        foreach (var go in objs)
        {
            if (go == null || !go.activeInHierarchy) continue;
            Vector3 p = go.transform.position;
            if ((p - eye).sqrMagnitude > nearObjectDistance * nearObjectDistance) continue;
            Vector3 v = cam.WorldToViewportPoint(p);
            if (v.z > 0f && v.x > 0.1f && v.x < 0.9f && v.y > 0.05f && v.y < 0.95f) return true;
        }
        return false;
    }

    private IEnumerator Fetch()
    {
        fetching = true;
        float lat, lon;
        bool fromGps = false;
        if (Input.location.status == LocationServiceStatus.Running)
        {
            var ld = Input.location.lastData;
            lat = ld.latitude; lon = ld.longitude; fromGps = true;
        }
#if UNITY_EDITOR
        else if (VirtualLocation.Instance != null)
        {
            lat = VirtualLocation.Instance.Latitude; lon = VirtualLocation.Instance.Longitude; fromGps = true;
        }
#endif
        else if (PlayerPrefs.HasKey(CacheLat))
        {
            // GPS 가 아직이면 지난번 자리로 먼저 받는다 (자리가 잡히면 곧 다시)
            lat = PlayerPrefs.GetFloat(CacheLat); lon = PlayerPrefs.GetFloat(CacheLon);
        }
        else
        {
            nextFetch = Time.unscaledTime + 1.5f;
            fetching = false;
            yield break;
        }

        // 소수점은 문화권과 무관하게 '.' — 스페인어 기기에선 '37,5759' 로 찍혀 주소가 깨졌다
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string url = ApiConfig.MAIN_SERVER + "/api/weather?lat=" + lat.ToString("F4", inv) + "&lon=" + lon.ToString("F4", inv);
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();
            Weather w = null;
            if (req.result == UnityWebRequest.Result.Success)
            {
                try { w = JsonUtility.FromJson<Weather>(req.downloadHandler.text); }
                catch (Exception) { w = null; }
            }
            if (w == null)
            {
                nextFetch = Time.unscaledTime + (hasData ? 60f : 4f);   // 아직 아무것도 없으면 금방 다시
                fetching = false;
                yield break;
            }
            Show(w);
            hasData = true;
            PlayerPrefs.SetString(CacheJson, req.downloadHandler.text);
            PlayerPrefs.SetString(CacheAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            if (fromGps) { PlayerPrefs.SetFloat(CacheLat, lat); PlayerPrefs.SetFloat(CacheLon, lon); }
            nextFetch = Time.unscaledTime + (fromGps ? refreshMinutes * 60f : 3f);   // 지난번 자리로 받았으면 GPS 가 잡히는 대로 다시
        }
        fetching = false;
    }

    // ── 문구 ────────────────────────────────────────────────
    private void Show(Weather w)
    {
        serverRegion = string.IsNullOrEmpty(w.region) ? null : w.region;
        ShowRegion(LocationManager.ShortRegion);
        string lang = R0926LocalizedText.Lang();
        int t = Mathf.RoundToInt(w.temp);
        int code = NowCode(w);
        bool rain = code >= 51 && code <= 67 || code >= 80 && code <= 82 || code >= 95;
        string cond = Condition(code, w.is_day, lang);
        tempText.text = t + "°";
        if (icon != null)
        {
            // 그림과 기온을 한 묶음으로 가운데에
            var irt = (RectTransform)icon.transform;
            float iw = irt.sizeDelta.x, tw = tempText.preferredWidth, gap = 26f;
            float total = iw + gap + tw;
            irt.anchoredPosition = new Vector2(-total / 2f + iw / 2f, irt.anchoredPosition.y);
            tempText.rectTransform.anchoredPosition = new Vector2(total / 2f - tw / 2f, tempText.rectTransform.anchoredPosition.y);
            icon.Set(code, w.is_day);
        }

        // 1줄: 상태 · (비 오면 강수 확률, 아니면 미세먼지)
        // 기상청 실황 1시간 강수량이 있으면 그걸, 없으면 강수 확률
        string extra = rain && w.rn1 > 0f ? L(lang, "1시간", "1h", "1時間", "1小时", "1 h") + " " + w.rn1.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "mm"
                     : rain && w.precip_prob >= 0 ? L(lang, "강수", "rain", "降水", "降水", "lluvia") + " " + Mathf.RoundToInt(w.precip_prob) + "%"
                     : w.pm10 >= 0 ? L(lang, "미세먼지", "Air", "PM10", "PM10", "Aire") + " " + Pm(w.pm10, lang) : null;
        condText.text = extra == null ? cond : cond + " · " + extra;

        // 2줄: 비 → 우산 · 습도 · 바람 / 밤 → 습도 · 달 · 일출 / 낮 → 습도 · 일몰 · 바람 (습도는 서버가 줄 때만)
        string wind = L(lang, "바람", "Wind", "風", "风", "Viento") + " " + w.wind.ToString("0.#") + "m/s";
        string humid = w.humidity >= 0f ? L(lang, "습도", "Humidity", "湿度", "湿度", "Humedad") + " " + Mathf.RoundToInt(w.humidity) + "% · " : "";
        if (rain)
            SetDetail(L(lang, "우산 챙기세요", "Take an umbrella", "傘を持って出かけましょう", "记得带伞", "Lleva paraguas") + " · " + humid + wind);
        else if (!w.is_day)
            SetDetail(humid + L(lang, "달", "Moon", "月", "月亮", "Luna") + " " + Mathf.RoundToInt(w.moon * 100) + "%"
                      + (string.IsNullOrEmpty(w.sunrise) ? "" : " · " + L(lang, "일출", "Sunrise", "日の出", "日出", "Amanecer") + " " + Clock(w.sunrise)));
        else
            SetDetail(humid + (string.IsNullOrEmpty(w.sunset) ? "" : L(lang, "일몰", "Sunset", "日没", "日落", "Atardecer") + " " + Clock(w.sunset) + " · ") + wind);

        bool hasFc = w.h3_temp > -90f && fcTemps != null && fcTemps.Length >= 3;
        if (forecast != null && forecast.activeSelf != hasFc) forecast.SetActive(hasFc);
        if (hasFc)
        {
            string[] names =
            {
                L(lang, "3시간 후", "In 3h", "3時間後", "3小时后", "En 3 h"),
                L(lang, "6시간 후", "In 6h", "6時間後", "6小时后", "En 6 h"),
                L(lang, "내일", "Tomorrow", "明日", "明天", "Mañana"),
            };
            for (int i = 0; i < 3; i++) if (fcLabels != null && i < fcLabels.Length && fcLabels[i] != null) fcLabels[i].text = names[i];
            fcTemps[0].text = Mathf.RoundToInt(w.h3_temp) + "°";
            fcTemps[1].text = w.h6_temp > -90f ? Mathf.RoundToInt(w.h6_temp) + "°" : "–";
            fcTemps[2].text = w.tomorrow_max > -90f
                ? Mathf.RoundToInt(w.tomorrow_max) + "°<size=" + Mathf.RoundToInt(fcTemps[2].fontSize * 0.8f) + "><color=#FFFFFFB3> / " + Mathf.RoundToInt(w.tomorrow_min) + "°</color></size>"
                : "–";
            if (fcIcons != null && fcIcons.Length >= 3)
            {
                if (fcIcons[0] != null) fcIcons[0].Set(w.h3_code >= 0 ? w.h3_code : code, w.h3_day);
                if (fcIcons[1] != null) fcIcons[1].Set(w.h6_code >= 0 ? w.h6_code : code, w.h6_day);
                if (fcIcons[2] != null) fcIcons[2].Set(w.tomorrow_code >= 0 ? w.tomorrow_code : code, true);
            }
        }
        weatherChip = cond + " " + t + "°";
        lastChip = null;   // 다음 프레임에 칩 글자 갱신
    }

    // 2줄은 높이가 한 줄뿐이라 줄바꿈되면 뒷부분이 잘린다 — 길어진 줄(습도 추가 · 일본어 비 등)만 글자를 줄여 한 줄에 맞춘다
    private int detailFont;
    private void SetDetail(string line)
    {
        if (detailText == null) return;
        if (detailFont <= 0) detailFont = detailText.fontSize;
        int size = detailFont;
        detailText.fontSize = size;
        detailText.text = line;
        float w = detailText.rectTransform.rect.width;
        while (w > 0f && size > detailMinFont && detailText.preferredWidth > w) detailText.fontSize = --size;
    }

    private static string Clock(string iso)
    {
        int k = iso.IndexOf('T');
        return k >= 0 && iso.Length >= k + 6 ? iso.Substring(k + 1, 5) : iso;
    }

    private static string Pm(float pm10, string lang)
    {
        // 한국 PM10 기준 (좋음 ~30 · 보통 ~80 · 나쁨 ~150 · 매우나쁨)
        if (pm10 <= 30) return L(lang, "좋음", "good", "良い", "优", "buena");
        if (pm10 <= 80) return L(lang, "보통", "moderate", "普通", "良", "moderada");
        if (pm10 <= 150) return L(lang, "나쁨", "poor", "悪い", "差", "mala");
        return L(lang, "매우 나쁨", "very poor", "非常に悪い", "很差", "muy mala");
    }

    private static string Condition(int code, bool day, string lang)
    {
        if (code == 0) return day ? L(lang, "맑음", "Clear", "晴れ", "晴", "Despejado") : L(lang, "맑은 밤", "Clear night", "晴れた夜", "晴朗的夜", "Noche despejada");
        if (code <= 2) return L(lang, "구름 조금", "Partly cloudy", "晴れ時々曇り", "少云", "Poco nuboso");
        if (code == 3) return L(lang, "흐림", "Cloudy", "曇り", "阴", "Nublado");
        if (code == 45 || code == 48) return L(lang, "안개", "Fog", "霧", "雾", "Niebla");
        if (code <= 57) return L(lang, "이슬비", "Drizzle", "霧雨", "毛毛雨", "Llovizna");
        if (code <= 67) return L(lang, "비", "Rain", "雨", "雨", "Lluvia");
        if (code <= 77) return L(lang, "눈", "Snow", "雪", "雪", "Nieve");
        if (code <= 82) return L(lang, "소나기", "Showers", "にわか雨", "阵雨", "Chubascos");
        if (code <= 86) return L(lang, "눈 소나기", "Snow showers", "にわか雪", "阵雪", "Chubascos de nieve");
        return L(lang, "뇌우", "Thunderstorm", "雷雨", "雷雨", "Tormenta");
    }

    private static string L(string lang, string ko, string en, string ja, string zh, string es)
    {
        switch (lang)
        {
            case "ko": return ko;
            case "ja": return ja;
            case "zh": return zh;
            case "es": return es;
            default: return en;
        }
    }
}
