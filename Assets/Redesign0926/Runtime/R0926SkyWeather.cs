using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 하늘 날씨 — 휴대폰을 하늘로 들면 하늘 먼 곳(약 600m)에 날씨가 걸린다.
///  · 조건: 위로 25° 이상 + 화면 가운데 부근에 60m 안쪽 AR 오브젝트가 없을 것
///  · 가까운 오브젝트가 있으면 오브젝트가 먼저 → 날씨는 위쪽 작은 칩으로 줄어든다
///  · 하늘의 날씨판은 한 번 걸리면 그 방향에 머물러, 휴대폰을 움직이면 하늘에 붙은 것처럼 흘러간다
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

    [Header("위쪽 작은 칩 — 판이 떠 있으면 '접기', 접혔거나 오브젝트 우선이면 날씨 + '펼치기'")]
    [SerializeField] private CanvasGroup chipGroup;
    [SerializeField] private Text chipText;
    [SerializeField] private RectTransform chipArrow;
    [SerializeField] private RectTransform chipRect;

    [Header("조건 (보이는 각도는 설정 탭 — R0926SkySettings)")]
    [SerializeField] private float nearObjectDistance = 60f;
    [Tooltip("이 각도 이상으로 들고 거의 움직이지 않으면 누워서 쓰는 것으로 본다")]
    [SerializeField] private float lyingPitch = 55f;
    [SerializeField] private float lyingSeconds = 6f;
    [SerializeField] private float boardDistance = 600f;
    [Tooltip("하늘에서 날씨판이 차지하는 폭 (시야각 도)")]
    [SerializeField] private float boardFov = 42f;
    [SerializeField] private float refreshMinutes = 10f;

    private Camera cam;
    private bool hasData;
    private bool anchored;
    private float nextFetch;
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
        public float tomorrow_min;
        public float pm10 = -1;
        public float moon;
    }

    private bool collapsed;          // 사용자가 '접기' — 앱을 켜는 동안 유지 (처음 상태는 설정)
    private bool lying, lyingOverride;
    private float highSince = -1f;
    private Vector3 highPos;
    private bool boardShowing;
    private string lastChip;

    private void Start()
    {
        collapsed = R0926SkySettings.StartCollapsed;
        if (skyGroup != null) skyGroup.alpha = 0f;
        if (chipGroup != null) chipGroup.alpha = 0f;
        if (skyBoard != null) skyBoard.gameObject.SetActive(false);
    }

    /// <summary>위쪽 칩을 누르면 — 판이 떠 있으면 접고, 접혀 있으면 편다</summary>
    public void ToggleCollapse()
    {
        if (boardShowing) collapsed = true;
        else
        {
            collapsed = false;
            if (lying) lyingOverride = true;   // 누워 있다고 봤어도 직접 펴면 따른다
        }
    }

    private void Update()
    {
        bool allowed = R0926SkySettings.Enabled && R0926SkySettings.Weather;
        if (allowed && Time.unscaledTime >= nextFetch) { nextFetch = Time.unscaledTime + 30f; StartCoroutine(Fetch()); }
        if (cam == null) cam = Camera.main;
        if (cam == null || skyBoard == null) return;

        Vector3 fwd = cam.transform.forward;
        float pitch = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
        bool up = pitch > R0926SkySettings.MinPitch;
        lookUpSince = up ? (lookUpSince < 0f ? Time.unscaledTime : lookUpSince) : -1f;
        bool settled = up && Time.unscaledTime - lookUpSince > 0.6f;   // 잠깐 스친 건 무시
        UpdateLying(pitch);
        bool near = settled && NearObjectInView();
        bool lyingHide = lying && !lyingOverride && R0926SkySettings.HideWhenLying;

        bool showSky = allowed && hasData && settled && !near && !collapsed && !lyingHide;
        bool showChip = allowed && hasData && settled;
        boardShowing = showSky;
        UpdateChip(showSky);

        if (showSky && !anchored) Anchor(fwd, pitch);
        if (!up) anchored = false;
        if (anchored)
        {
            // 너무 옆으로 돌아서면(70° 이상) 새 방향으로 다시 건다
            Vector3 toBoard = (skyBoard.position - cam.transform.position).normalized;
            if (Vector3.Angle(Flat(toBoard), Flat(fwd)) > 70f) Anchor(fwd, pitch);
            skyBoard.rotation = Quaternion.LookRotation(skyBoard.position - cam.transform.position);
        }

        Fade(skyGroup, showSky, 1.6f);
        Fade(chipGroup, showChip, 3f);
        if (chipGroup != null) { bool touch = chipGroup.alpha > 0.5f; chipGroup.blocksRaycasts = touch; chipGroup.interactable = touch; }
        bool boardVisible = skyGroup != null && skyGroup.alpha > 0.001f;
        if (skyBoard.gameObject.activeSelf != boardVisible) skyBoard.gameObject.SetActive(boardVisible);
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
        string text = boardUp ? L(R0926LocalizedText.Lang(), "하늘 접기", "Hide sky", "空をたたむ", "收起天空", "Ocultar cielo") : weatherChip;
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
        if (Input.location.status != LocationServiceStatus.Running) yield break;
        var ld = Input.location.lastData;
        string url = $"{ApiConfig.MAIN_SERVER}/api/weather?lat={ld.latitude:F4}&lon={ld.longitude:F4}";
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            Weather w;
            try { w = JsonUtility.FromJson<Weather>(req.downloadHandler.text); }
            catch (Exception) { yield break; }
            if (w == null) yield break;
            Show(w);
            hasData = true;
            nextFetch = Time.unscaledTime + refreshMinutes * 60f;
        }
    }

    // ── 문구 ────────────────────────────────────────────────
    private void Show(Weather w)
    {
        string lang = R0926LocalizedText.Lang();
        int t = Mathf.RoundToInt(w.temp);
        bool rain = w.code >= 51 && w.code <= 67 || w.code >= 80 && w.code <= 82 || w.code >= 95;
        string cond = Condition(w.code, w.is_day, lang);
        tempText.text = t + "°";
        condText.text = rain && w.precip_prob >= 0 ? cond + " · " + L(lang, "강수", "rain", "降水", "降水", "lluvia") + " " + Mathf.RoundToInt(w.precip_prob) + "%" : cond;

        string detail;
        if (rain)
            detail = L(lang, "우산 챙기세요", "Take an umbrella", "傘を持って出かけましょう", "记得带伞", "Lleva paraguas");
        else if (!w.is_day)
            detail = L(lang, "달", "Moon", "月", "月亮", "Luna") + " " + Mathf.RoundToInt(w.moon * 100) + "% · "
                   + L(lang, "내일 아침", "Tomorrow low", "明朝", "明早", "Mañana") + " " + Mathf.RoundToInt(w.tomorrow_min) + "°"
                   + (string.IsNullOrEmpty(w.sunrise) ? "" : " · " + L(lang, "일출", "Sunrise", "日の出", "日出", "Amanecer") + " " + Clock(w.sunrise));
        else
            detail = (w.pm10 >= 0 ? L(lang, "미세먼지", "Air", "PM10", "PM10", "Aire") + " " + Pm(w.pm10, lang) + " · " : "")
                   + (string.IsNullOrEmpty(w.sunset) ? "" : L(lang, "일몰", "Sunset", "日没", "日落", "Atardecer") + " " + Clock(w.sunset) + " · ")
                   + L(lang, "바람", "Wind", "風", "风", "Viento") + " " + w.wind.ToString("0.#") + "m/s";
        detailText.text = detail;
        weatherChip = cond + " " + t + "°";
        lastChip = null;   // 다음 프레임에 칩 글자 갱신
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
