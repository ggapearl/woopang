using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Globalization;
using UnityEngine.Networking;
using SimpleJSON;
using System.Text;

public class LocationManager : MonoBehaviour
{
    [SerializeField] private Image statusImage;
    [SerializeField] private Sprite successSprite;
    [SerializeField] private Sprite failSprite;
    [SerializeField] private Text infoText;
    [Tooltip("켜면 주소·좌표를 한 줄로 (0926 도크 위 칩)")]
    [SerializeField] private bool singleLine = false;
    [SerializeField] private float refreshInterval = 30f;

    /// <summary>날씨판 위에 쓰는 짧은 지역 이름 — '예산군 대흥면' (시·군·구 + 읍·면·동, 나라·광역 단위는 뺀다)</summary>
    public static string ShortRegion { get; private set; }
    public static event System.Action<string> ShortRegionChanged;

    private string currentLanguage;
    private bool isRefreshing = false;
    private WaitForSeconds waitOneSecond = new WaitForSeconds(1f);
    private WaitForSeconds waitRefreshInterval;
    private StringBuilder textBuilder = new StringBuilder(200);

    // 방금 보여 준 주소 — 이만큼 안 움직였으면 다시 묻지 않는다 (30초마다 주소 서버를 부르던 것)
    private const float SameSpotMeters = 25f;
    private string lastText;
    private float lastLat, lastLon;

    void Awake()
    {
        waitRefreshInterval = new WaitForSeconds(refreshInterval);
    }

    void Start()
    {
        currentLanguage = AppLanguage.Code;
        DisplayInitializingMessage();
        StartCoroutine(CheckLocationService());
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            DisplayInitializingMessage();
            StartCoroutine(CheckLocationService());
        }
    }

    void DisplayInitializingMessage()
    {
        statusImage.gameObject.SetActive(false);

        string message = currentLanguage switch
        {
            "ko" => "위치서비스 초기화 중",
            "ja" => "位置サービス初期化中",
            "zh" => "位置服务初始化中",
            "es" => "Inicializando el servicio de ubicación",
            _ => "Initializing location service"
        };
        infoText.text = message;
    }

    IEnumerator CheckLocationService()
    {
#if UNITY_EDITOR
        // VirtualLocation이 있으면 그 좌표를, 없으면 기본 청주 좌표 사용
        float lat = VirtualLocation.Instance != null ? VirtualLocation.Instance.Latitude : 36.6361f;
        float lon = VirtualLocation.Instance != null ? VirtualLocation.Instance.Longitude : 126.8280f;
        StartCoroutine(GetAddressFromCoordinates(lat, lon));
        if (!isRefreshing)
        {
            isRefreshing = true;
            StartCoroutine(RefreshLocationPeriodically());
        }
        yield break;
#else
        if (!Input.location.isEnabledByUser)
        {
            DisplayLocationDisabledMessage();
            yield break;
        }

        Input.location.Start();

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return waitOneSecond;
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            DisplayLocationDisabledMessage();
            yield break;
        }
        else if (maxWait <= 0)
        {
            DisplayLocationDisabledMessage();
            yield break;
        }
        else if (Input.location.status == LocationServiceStatus.Running)
        {
            float latitude = Input.location.lastData.latitude;
            float longitude = Input.location.lastData.longitude;
            StartCoroutine(GetAddressFromCoordinates(latitude, longitude));

            if (!isRefreshing)
            {
                isRefreshing = true;
                StartCoroutine(RefreshLocationPeriodically());
            }
        }
#endif
    }

    void DisplayLocationDisabledMessage()
    {
        statusImage.sprite = failSprite;
        statusImage.gameObject.SetActive(true);

        string message = currentLanguage switch
        {
            "ko" => "위치서비스가 활성화되지 않았습니다.",
            "ja" => "位置サービスが有効になっていません。",
            "zh" => "位置服务未启用。",
            "es" => "El servicio de ubicación no está activado.",
            _ => "Location service is not enabled."
        };
        infoText.text = message;
    }

    IEnumerator GetAddressFromCoordinates(float latitude, float longitude)
    {
        statusImage.sprite = successSprite;
        statusImage.gameObject.SetActive(true);

        if (lastText != null && Meters(latitude, longitude, lastLat, lastLon) < SameSpotMeters)
        {
            infoText.text = lastText;
            yield break;
        }

        // 한국어 화면 · 국내 — 우팡 서버(브이월드 주소, 도로명·지번이 정확)가 먼저. 안 되면 아래 OSM 으로
        if (currentLanguage == "ko" && R0926Geo.InKorea(latitude, longitude))
        {
            R0926Geo.Address a = null;
            yield return R0926Geo.FindAddress(latitude, longitude, r => a = r);
            if (a != null)
            {
                SetShortRegion(a.region);
                textBuilder.Clear();
                if (singleLine)
                {
                    // 도크 바로 위 한 줄: "대흥면 지곡리 · 36.6361, 126.8280"
                    if (!string.IsNullOrEmpty(a.line)) textBuilder.Append(a.line).Append("  ·  ");
                    AppendCoords(latitude, longitude);
                }
                else
                {
                    AppendCoords(latitude, longitude);
                    textBuilder.Append("\n").Append(string.IsNullOrEmpty(a.road) ? a.parcel : a.road);
                }
                Remember(latitude, longitude);
                yield break;
            }
        }

        StringBuilder urlBuilder = new StringBuilder(100);
        urlBuilder.Append("https://nominatim.openstreetmap.org/reverse?lat=");
        urlBuilder.Append(latitude.ToString("F4"));
        urlBuilder.Append("&lon=");
        urlBuilder.Append(longitude.ToString("F4"));
        urlBuilder.Append("&format=json&accept-language=");
        urlBuilder.Append(currentLanguage);

        using (UnityWebRequest request = UnityWebRequest.Get(urlBuilder.ToString()))
        {
            request.SetRequestHeader("User-Agent", "WoopangARApp/1.0");
            request.timeout = 10; // 10초 타임아웃
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    JSONNode data = JSON.Parse(jsonResponse);
                    SetShortRegion(BuildShortRegion(data["address"]));

                    if (singleLine)
                    {
                        // 도크 바로 위 한 줄: "지곡리, 예산군 · 36.6361, 126.8280"
                        textBuilder.Clear();
                        string[] parts = (data["display_name"].Value ?? "").Split(',');
                        if (parts.Length >= 2) textBuilder.Append(parts[0].Trim()).Append(", ").Append(parts[1].Trim()).Append("  ·  ");
                        else if (parts.Length == 1 && parts[0].Trim().Length > 0) textBuilder.Append(parts[0].Trim()).Append("  ·  ");
                        AppendCoords(latitude, longitude);
                        Remember(latitude, longitude);
                        yield break;
                    }

                    textBuilder.Clear();
                    textBuilder.Append("Lat: ").Append(latitude.ToString("F4"));
                    textBuilder.Append(", Lon: ").Append(longitude.ToString("F4"));
                    textBuilder.Append("\n");

                    string displayName = data["display_name"].Value;
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        string[] addressParts = displayName.Split(',');
                        if (addressParts.Length >= 3)
                        {
                            textBuilder.Append(addressParts[0].Trim()).Append(", ");
                            textBuilder.Append(addressParts[1].Trim()).Append(", ");
                            textBuilder.Append(addressParts[2].Trim());
                        }
                        else
                        {
                            textBuilder.Append(displayName);
                        }
                    }
                    else
                    {
                        textBuilder.Append(R0926LocalizedText.Say("주소 정보 없음", "No address", "住所情報なし", "无地址信息", "Sin dirección"));
                    }

                    Remember(latitude, longitude);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LocationManager] JSON 파싱 에러: {e.Message}");
                    textBuilder.Clear();
                    AppendCoords(latitude, longitude);
                    infoText.text = textBuilder.ToString();
                }
            }
            else
            {
                // API 요청 실패 시 조용히 처리 (에디터에서 타임아웃 발생 정상)

                // 좌표만이라도 표시
                textBuilder.Clear();
                AppendCoords(latitude, longitude);
                infoText.text = textBuilder.ToString();
            }
        }
    }

    // 주소를 찾은 글만 기억한다 — 좌표만 찍힌 실패는 다음 번에 다시 묻는다
    private void Remember(float latitude, float longitude)
    {
        lastText = textBuilder.ToString();
        lastLat = latitude;
        lastLon = longitude;
        infoText.text = lastText;
    }

    private static float Meters(float lat1, float lon1, float lat2, float lon2)
    {
        float dy = (lat1 - lat2) * 111320f;
        float dx = (lon1 - lon2) * 111320f * Mathf.Cos(lat1 * Mathf.Deg2Rad);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static void SetShortRegion(string region)
    {
        if (string.IsNullOrEmpty(region) || region == ShortRegion) return;
        ShortRegion = region;
        ShortRegionChanged?.Invoke(region);
    }

    // OSM 주소 키 — 작은 단위가 뒤에 오도록. 한국은 지역마다 '면'이 town/village 등 다른 키로 와서 이름 끝 글자로 단계를 가른다
    private static readonly string[] RegionKeys = { "borough", "city_district", "county", "city", "municipality", "town", "village", "suburb", "quarter", "neighbourhood" };

    /// <summary>
    /// 시·군·구 + 읍·면·동. 예: 예산군 대흥면 · 종로구 청운효자동 · 영통구 매탄동 · 세종시 보람동.
    /// 특별시·광역시·도·나라는 뺀다. 한국 이름이 아니면(외국·로마자) 작은 단위 + 시 이름.
    /// </summary>
    private static string BuildShortRegion(JSONNode addr)
    {
        if (addr == null) return null;
        string sgg = null, emd = null;
        foreach (string key in RegionKeys)
        {
            string v = addr[key].Value;
            if (string.IsNullOrEmpty(v)) continue;
            v = v.Trim();
            if (sgg == null && IsSigungu(v)) sgg = ShortSigungu(v);
            else if (emd == null && IsEupMyeonDong(v)) emd = v;
        }
        // 시골은 OSM 에 읍·면이 없고 리만 오는 곳이 많다 ('예산군' 만 찍혔다) → '예산군 지곡리'
        if (sgg != null && emd == null)
            foreach (string key in new[] { "village", "hamlet", "neighbourhood", "quarter" })
            {
                string v = addr[key].Value;
                if (!string.IsNullOrEmpty(v) && v.Trim().EndsWith("리")) { emd = v.Trim(); break; }
            }
        if (sgg != null || emd != null) return sgg != null && emd != null ? sgg + " " + emd : (sgg ?? emd);

        // 한국식 이름이 아니다 — 동네 + 도시 (예: Shibuya, Tokyo)
        string small = null, big = null;
        foreach (string key in new[] { "suburb", "quarter", "neighbourhood", "village", "town" })
            if (small == null && !string.IsNullOrEmpty(addr[key].Value)) small = addr[key].Value.Trim();
        foreach (string key in new[] { "city", "town", "county", "city_district", "borough" })
            if (big == null && !string.IsNullOrEmpty(addr[key].Value) && addr[key].Value.Trim() != small) big = addr[key].Value.Trim();
        if (small != null && big != null) return small + ", " + big;
        return small ?? big;
    }

    private static bool IsSigungu(string v)
    {
        if (v.EndsWith("특별시") || v.EndsWith("광역시")) return false;
        if (v.EndsWith("구") || v.EndsWith("군") || v.EndsWith("시")) return true;
        string l = v.ToLowerInvariant();   // 로마자 (영어 등)
        return l.EndsWith("-gu") || l.EndsWith("-gun") || l.EndsWith("-si");
    }

    // 세종은 시·군·구 없이 '세종특별자치시 ○○동' 으로 와서 판에 길게 찍혔다 → '세종시 ○○동'
    private static string ShortSigungu(string v)
    {
        const string SpecialCity = "특별자치시";
        return v.EndsWith(SpecialCity) ? v.Substring(0, v.Length - SpecialCity.Length) + "시" : v;
    }

    private static bool IsEupMyeonDong(string v)
    {
        if (v.EndsWith("읍") || v.EndsWith("면") || v.EndsWith("동") || v.EndsWith("가")) return true;
        string l = v.ToLowerInvariant();
        return l.EndsWith("-eup") || l.EndsWith("-myeon") || l.EndsWith("-dong") || l.EndsWith("-ga");
    }

    private void AppendCoords(double latitude, double longitude)
    {
        if (singleLine) textBuilder.Append("<color=#8C959D>").Append(latitude.ToString("F4")).Append(", ").Append(longitude.ToString("F4")).Append("</color>");
        else textBuilder.Append("Lat: ").Append(latitude.ToString("F4")).Append(", Lon: ").Append(longitude.ToString("F4"));
    }

    IEnumerator RefreshLocationPeriodically()
    {
        while (isRefreshing)
        {
            yield return waitRefreshInterval;
#if UNITY_EDITOR
            // VirtualLocation이 있으면 그 좌표를, 없으면 기본 청주 좌표 사용
            float latitude = VirtualLocation.Instance != null ? VirtualLocation.Instance.Latitude : 36.6361f;
            float longitude = VirtualLocation.Instance != null ? VirtualLocation.Instance.Longitude : 126.8280f;
            // 에디터에서 주기적 갱신 (로그 제거)
            StartCoroutine(GetAddressFromCoordinates(latitude, longitude));
#else
            if (Input.location.status == LocationServiceStatus.Running)
            {
                float latitude = Input.location.lastData.latitude;
                float longitude = Input.location.lastData.longitude;
                // 주기적 갱신 (로그 제거)
                StartCoroutine(GetAddressFromCoordinates(latitude, longitude));
            }
            else
            {
                isRefreshing = false;
            }
#endif
        }
    }

    public void RequestLocationUpdate()
    {
        DisplayInitializingMessage();
        StartCoroutine(CheckLocationService());
    }

    void OnDisable()
    {
        isRefreshing = false;
        Input.location.Stop();
    }
}