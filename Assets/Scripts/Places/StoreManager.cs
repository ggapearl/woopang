using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// 근처 가게 — 소상공인시장진흥공단 상가(상권)정보 (11-18 N8a 대표님 「새 가게 DB(소상공인 상가정보) → 이름 「공공데이터」」).
/// 여섯 번째 데이터 매니저 — 서버가 분기마다 받아 둔 가게 중 가까운 몇 곳(반경 50m · 20곳)만 받는다 (「앱은 가까운 몇 개만 받기」).
/// 사람이 올린 장소가 먼저다 — 같은 이름이 30m 안에 있으면 서버가 뺀다.
///  · 띄우기는 FilterManager 중앙 배분(IPlaceCacheProvider) · 사진이 있으면 다 받은 뒤에(PlaceSpawnGate, 11-19 P1) · 없으면 분류 스티커(Resources/PublicIcons)
///  · 걸으면서 25m 마다 다시 묻는다 (배분의 1km 다시 받기로는 50m 반경을 따라가지 못한다) · 차로 달리는 중(25km/h 넘음)엔 묻지 않는다
///  · 서버에 아직 길이 없거나(404) 자료를 받기 전(not_ready)이면 30분 쉬었다 다시 묻는다
/// </summary>
public class StoreManager : MonoBehaviour, IPlaceCacheProvider, IPlaceCacheLoadTime
{
    private static StoreManager instance;
    public static StoreManager Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<StoreManager>(FindObjectsInactive.Include);
            return instance;
        }
    }

    public const string Prefix = "store_";
    private const string KeyName = "stores";
    private static readonly string[] Categories = { "food", "cafe", "shop", "stay" };

#if UNITY_EDITOR
    public static string EditorBase;   // 플레이 시험 — 가게 목록을 내 PC 시험 서버에서 받는다 (끝나면 null)
#endif
    private static string NearUrl
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorBase)) return EditorBase.TrimEnd('/') + "/api/stores/near";
#endif
            return ApiConfig.STORES_NEAR;
        }
    }

    public GameObject samplePrefab;
    [SerializeField] private GameObject indicatorOnlyPrefab;

    [Header("받기")]
    [Tooltip("이 반경(m) 안의 가게만 받는다 — 서버 허용 10~200")]
    [SerializeField] private int radius = 50;
    [Tooltip("한 번에 받는 가게 수 — 서버 허용 1~50")]
    [SerializeField] private int limit = 20;
    [Tooltip("마지막으로 받은 자리에서 이만큼(m) 움직이면 다시 묻는다")]
    [SerializeField] private float refetchMeters = 25f;
    [Tooltip("다시 묻는 최소 간격 (초)")]
    [SerializeField] private float minFetchInterval = 8f;
    [Tooltip("이보다 빠르면(km/h — 차 · 지하철) 다시 묻지 않는다")]
    [SerializeField] private float maxFetchSpeedKmh = 25f;
    [Tooltip("서버에 가게 자료가 아직 없을 때(404 · not_ready) 쉬는 시간 (초)")]
    [SerializeField] private float dormantSeconds = 1800f;
    [SerializeField] private int poolSize = 8;

    private readonly Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, StoreData> placeDataMap = new Dictionary<string, StoreData>();
    private readonly Dictionary<string, GameObject> indicatorOnlyObjects = new Dictionary<string, GameObject>();
    private readonly Queue<GameObject> objectPool = new Queue<GameObject>();
    private readonly Queue<GameObject> indicatorOnlyPool = new Queue<GameObject>();
    private readonly List<CachedPlaceData> lightCache = new List<CachedPlaceData>();
    private Dictionary<string, bool> currentFilters;

    private bool isCacheReady;
    private bool cacheReadyEventFired;
    public event System.Action CacheBecameReady;

    private bool fetching;
    private bool hasFetchPos;
    private double fetchLat, fetchLon;
    private float lastFetchAt = -999f;
    private float dormantUntil;
    private float backoffUntil;
    private int fails;
    private bool parseErrorLogged;
    private FilterManager fm;

    private readonly Queue<string> photoReadyQueue = new Queue<string>();
    private Coroutine photoReadyLoop;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        // 칩 값은 FilterManager 가 곧 넣는다 — 먼저 넣었으면 그대로 (저장된 값으로 덮지 않는다)
        if (currentFilters == null)
        {
            currentFilters = new Dictionary<string, bool>
            {
                { "petFriendlyOnly", PlayerPrefs.GetInt("Filter_PetFriendly_V3", 0) == 1 },
                { KeyName, PlayerPrefs.GetInt("Filter_Stores_V1", 1) == 1 },
                { "categoryFilter", PlayerPrefs.GetInt("Filter_Category_V1", 0) != 0 }
            };
        }
        StartCoroutine(InitializeObjectPool());
        if (FindFm() != null) fm.RegisterCacheProvider(this);
    }

    private void OnEnable() => StartCoroutine(FollowPosition());

    private void OnDisable()
    {
        fetching = false;        // 꺼지면 받던 코루틴이 멈춘다 — 다시 켜지면 새로 묻게
        photoReadyLoop = null;   // 다음 준비 완료 때 다시 시작하게
    }

    private FilterManager FindFm()
    {
        if (fm == null) fm = FindAnyObjectByType<FilterManager>(FindObjectsInactive.Include);
        return fm;
    }

    // 오프닝이 걷힌 뒤 한가한 프레임에 하나씩 (교통 매니저와 같다)
    private IEnumerator InitializeObjectPool()
    {
        if (samplePrefab == null) yield break;
        while (objectPool.Count + spawnedObjects.Count < poolSize)
        {
            yield return null;
            if (!SpawnPace.Idle) continue;
            GameObject obj = NewObject();
            obj.SetActive(false);
            objectPool.Enqueue(obj);
            SpawnPace.Took();
        }
    }

    // ── 받기 ──

    private bool TryHere(out double lat, out double lon)
    {
        lat = lon = 0;
        if (FindFm() != null)
        {
            Vector2 h = fm.Here;
            if (h.x != 0f || h.y != 0f) { lat = h.x; lon = h.y; return true; }
        }
        return GeoHere.TryGet(out lat, out lon, out _);
    }

    // 걸으면서 따라간다 — 첫 목록은 FilterManager(RefreshAllCaches · 못 받으면 다시 받기)가 묻는다
    private IEnumerator FollowPosition()
    {
        var wait = new WaitForSeconds(3f);
        while (true)
        {
            yield return wait;
            if (!isCacheReady || fetching || BootOverlay.Showing) continue;
            float now = Time.realtimeSinceStartup;
            if (now < dormantUntil || now < backoffUntil) continue;
            if (!TryHere(out double lat, out double lon)) continue;
            if (dormantUntil > 0f)
            {
                dormantUntil = 0f;   // 쉬는 시간이 끝났다 — 자리와 상관없이 한 번 다시 묻는다
                StartCoroutine(Fetch(lat, lon));
                continue;
            }
            if (now - lastFetchAt < minFetchInterval) continue;
            if (fm != null && fm.CurrentSpeedKmh > maxFetchSpeedKmh) continue;
            if (hasFetchPos && GeoHere.Meters(fetchLat, fetchLon, lat, lon) < refetchMeters) continue;
            StartCoroutine(Fetch(lat, lon));
        }
    }

    private IEnumerator Fetch(double lat, double lon)
    {
        fetching = true;
        lastFetchAt = Time.realtimeSinceStartup;
        string url = NearUrl
            + "?lat=" + lat.ToString("F6", CultureInfo.InvariantCulture)
            + "&lon=" + lon.ToString("F6", CultureInfo.InvariantCulture)
            + "&radius=" + radius.ToString(CultureInfo.InvariantCulture)
            + "&limit=" + limit.ToString(CultureInfo.InvariantCulture);
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();
            fetching = false;
            if (this == null) yield break;

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (req.responseCode == 404) Sleep(lat, lon);                                       // 서버에 아직 가게 길이 없다
                else if (req.responseCode == 429) backoffUntil = Time.realtimeSinceStartup + 60f;  // 너무 자주 물었다
                else Fail();                                                                        // 끊김 · 서버 오류 — FilterManager 도 다시 받는다
                yield break;
            }

            StoresResponse res = null;
            try { res = JsonConvert.DeserializeObject<StoresResponse>(req.downloadHandler.text); }
            catch (System.Exception ex)
            {
                if (!parseErrorLogged) { parseErrorLogged = true; Debug.LogError($"[StoreManager] 가게 목록을 읽지 못함: {ex.Message}"); }
            }
            if (res == null) { Sleep(lat, lon); yield break; }   // 가게 길이 아닌 응답 — 404 와 같이 쉰다
            if (!res.ok) { Fail(); yield break; }
            fails = 0;
            if (res.status == "not_ready") { Sleep(lat, lon); yield break; }
            Adopt(res.items, lat, lon);
        }
    }

    private void Fail()
    {
        fails++;
        backoffUntil = Time.realtimeSinceStartup + Mathf.Min(120f, 8f * Mathf.Pow(2f, Mathf.Min(fails, 4)));
    }

    // 빈 목록으로 준비를 마치고 쉰다 — 배분 · 다시 받기가 기다리지 않게
    private void Sleep(double lat, double lon)
    {
        dormantUntil = Time.realtimeSinceStartup + dormantSeconds;
        Adopt(null, lat, lon);
    }

    private static bool Known(string category) => System.Array.IndexOf(Categories, category) >= 0;

    private void Adopt(List<StoreData> items, double lat, double lon)
    {
        bool wasReady = isCacheReady;
        hasFetchPos = true;
        fetchLat = lat;
        fetchLon = lon;
        lightCache.Clear();
        var keep = new HashSet<string>();
        if (items != null)
        {
            foreach (var s in items)
            {
                if (s == null || string.IsNullOrEmpty(s.id) || (s.lat == 0 && s.lon == 0) || !keep.Add(s.id)) continue;
                if (!Known(s.category)) s.category = "shop";
                placeDataMap[s.id] = s;
                lightCache.Add(new CachedPlaceData
                {
                    uniqueId = Prefix + s.id,
                    rawId = s.id,
                    displayName = s.name ?? "",
                    latitude = s.lat,
                    longitude = s.lon,
                    altitude = 0,
                    category = s.category,
                    sourceManager = "Store",
                    modelType = "cube",
                    petFriendly = false,   // 가게 자료엔 애견동반이 없다 — 애견동반을 켜면 숨는다 (FilterManager.PetAllowed)
                    filterKey = KeyName
                });
                if (lightCache.Count >= MaxCacheSize) break;
            }
        }
        // 떠 있는 곳 정보는 배분이 거둘 때까지 둔다
        var drop = new List<string>();
        foreach (var id in placeDataMap.Keys)
            if (!keep.Contains(id) && !spawnedObjects.ContainsKey(id) && !indicatorOnlyObjects.ContainsKey(id)) drop.Add(id);
        foreach (var id in drop) placeDataMap.Remove(id);

        MarkCacheReady();
        if (wasReady && FindFm() != null) fm.TriggerReallocation();   // 처음은 CacheBecameReady 로 배분이 다시 돈다
    }

    private void MarkCacheReady()
    {
        isCacheReady = true;
        LastLoadedAt = Time.realtimeSinceStartup;
        if (cacheReadyEventFired) return;
        cacheReadyEventFired = true;
        CacheBecameReady?.Invoke();
    }

#if UNITY_EDITOR
    /// <summary>시험용 — 쉬는 중 · 기다리는 중을 풀고 지금 자리로 바로 묻는다</summary>
    public void EditorRefetch()
    {
        dormantUntil = backoffUntil = 0f;
        fails = 0;
        if (!fetching && isActiveAndEnabled && TryHere(out double lat, out double lon)) StartCoroutine(Fetch(lat, lon));
    }

    public bool EditorDormant => Time.realtimeSinceStartup < dormantUntil;
    public bool EditorFetching => fetching;
#endif

    // ── 보이기 ──

    /// <summary>가게 분류 스티커 (사진이 없을 때 큐브 앞면 · 목록 줄 그림) — 기존 공공데이터 스티커와 같은 모양</summary>
    public static string StickerPath(string category) => "PublicIcons/" + (Known(category) ? category : "shop");

    /// <summary>정보 창 사진 — 관광공사 사진이 이어진 가게만 (우팡 서버에서 찾지 않는다)</summary>
    public static List<string> Photos(StoreData s) =>
        s == null || string.IsNullOrEmpty(s.photo) ? new List<string>() : new List<string> { s.photo };

    /// <summary>정보 창 글 — 오브젝트를 띄울 때와 목록 사진으로 열 때(R0926PlaceInfo) 같은 값</summary>
    public static void ConfigureInfo(DoubleTap3D dt, StoreData s)
    {
        if (dt == null || s == null) return;
        dt.SetInfoImages(
            sprite1: null,
            sprite2: null,
            petFriendly: false,
            separateRestroom: false,
            description: null,
            name: s.name,
            id: -1,
            username: "WOOPANG",
            instagramId: null,
            tel: string.IsNullOrEmpty(s.tel) ? null : s.tel,   // 행정안전부 인허가(영업 중)와 맞춘 가게만 (11-27 · 서버 public_store_status)
            address: s.addr,
            overview: Overview(s),
            petInfo: null);
    }

    /// <summary>업종 · 층 / 출처 / 사진 출처</summary>
    public static string Overview(StoreData s)
    {
        var sb = new System.Text.StringBuilder();
        string floor = FloorText(s.floor);
        if (!string.IsNullOrEmpty(s.upjong)) sb.Append(s.upjong);
        if (!string.IsNullOrEmpty(floor)) sb.Append(sb.Length > 0 ? " · " : "").Append(floor);
        if (sb.Length > 0) sb.Append("\n\n");
        sb.Append(R0926LocalizedText.Say("출처: ", "Source: ", "出典: ", "来源: ", "Fuente: "));
        sb.Append(R0926LocalizedText.Say(
            "소상공인시장진흥공단 상가(상권)정보",
            "SEMAS (Small Enterprise and Market Service) store data",
            "小商工人市場振興公団 商店街(商圏)情報",
            "小商工人市场振兴公团 商铺(商圈)信息",
            "Datos de comercios de SEMAS (Servicio de Pequeñas Empresas y Mercados)"));
        if (!string.IsNullOrEmpty(s.photo) && !string.IsNullOrEmpty(s.photo_credit))
        {
            sb.Append("\n");
            sb.Append(R0926LocalizedText.Say("사진: ", "Photo: ", "写真: ", "照片: ", "Foto: "));
            sb.Append(s.photo_credit == "한국관광공사"
                ? R0926LocalizedText.Say("한국관광공사", "Korea Tourism Organization", "韓国観光公社", "韩国观光公社", "Organización de Turismo de Corea")
                : s.photo_credit);
        }
        if (!string.IsNullOrEmpty(s.tel)) sb.Append("\n전화: 행정안전부 지방행정 인허가 정보");   // 출처 표시 (11-27 · 새 글은 한국어만)
        return sb.ToString();
    }

    /// <summary>상가정보 층(flrNo) — "1" → 1층 · "B1"/"지1"/"지하1" → 지하 1층 · 그 밖은 그대로</summary>
    public static string FloorText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string f = raw.Trim();
        bool under = false;
        if (f.StartsWith("지하")) { under = true; f = f.Substring(2); }
        else if (f.StartsWith("지") || f.StartsWith("B") || f.StartsWith("b")) { under = true; f = f.Substring(1); }
        f = f.Trim();
        if (f.EndsWith("층")) f = f.Substring(0, f.Length - 1).Trim();
        if (!int.TryParse(f, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n <= 0) return raw.Trim();
        return under
            ? R0926LocalizedText.Say($"지하 {n}층", $"B{n}", $"地下{n}階", $"地下{n}楼", $"Sótano {n}")
            : R0926LocalizedText.Say($"{n}층", $"Floor {n}", $"{n}階", $"{n}楼", $"Planta {n}");
    }

    /// <summary>
    /// 칩 · 분류 — 분류를 고른 동안은 그 분류(음식점 · 카페 · 샵 · 숙소)만, '전체'면 공공데이터 칩.
    /// 애견동반을 켜면 숨긴다 (가게 자료엔 애견동반이 없다 — 배분 · 목록과 같은 규칙)
    /// </summary>
    private bool ShouldShow(string category)
    {
        if (currentFilters == null) return true;
        if (currentFilters.TryGetValue("petFriendlyOnly", out bool petOnly) && petOnly) return false;
        if (currentFilters.TryGetValue("categoryFilter", out bool categoryActive) && categoryActive)
        {
            string want = FilterManager.ActiveCategoryNow;
            return string.IsNullOrEmpty(want) || want == category;
        }
        return !currentFilters.TryGetValue(KeyName, out bool on) || on;
    }

    private string CategoryOfId(string rawId)
    {
        if (rawId != null && placeDataMap.TryGetValue(rawId, out StoreData s)) return s.category;
        var c = rawId != null ? lightCache.Find(x => x.rawId == rawId) : null;
        return c != null ? c.category : "shop";
    }

    private bool InRange(StoreData s, float maxDist)
    {
        if (!TryHere(out double lat, out double lon)) return true;
        return GeoHere.Meters(lat, lon, s.lat, s.lon) <= maxDist;
    }

    public void ApplyFilters(Dictionary<string, bool> filters)
    {
        if (filters == null) return;
        currentFilters = new Dictionary<string, bool>(filters);
        foreach (var kvp in spawnedObjects)
            if (kvp.Value != null) kvp.Value.SetActive(ShouldShow(CategoryOfId(kvp.Key)));
        foreach (var kvp in indicatorOnlyObjects)
            if (kvp.Value != null) kvp.Value.SetActive(ShouldShow(CategoryOfId(kvp.Key)));
    }

    public void UpdateDistanceFilter(float maxDistance, float currentLat, float currentLon)
    {
        foreach (var kvp in spawnedObjects)
        {
            GameObject obj = kvp.Value;
            if (obj == null) continue;
            bool show = ShouldShow(CategoryOfId(kvp.Key));
            if (show && placeDataMap.TryGetValue(kvp.Key, out StoreData s))
                show = GeoHere.Meters(currentLat, currentLon, s.lat, s.lon) <= maxDistance;
            if (obj.activeSelf != show) obj.SetActive(show);
        }
    }

    public void SetAllRenderersVisible(bool visible)
    {
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value == null) continue;
            var anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor != null) anchor.SetForceHideRenderers(!visible);
        }
    }

    public void SetAllObjectsVisible(bool visible)
    {
        if (visible)
        {
            float maxDist = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
            if (TryHere(out double lat, out double lon)) UpdateDistanceFilter(maxDist, (float)lat, (float)lon);
            else ApplyFilters(currentFilters);
            return;
        }
        foreach (var kvp in spawnedObjects)
            if (kvp.Value != null) kvp.Value.SetActive(false);
    }

    public Dictionary<string, StoreData> GetPlaceDataMap() => placeDataMap;
    public Dictionary<string, GameObject> GetSpawnedObjects() => spawnedObjects;
    public Dictionary<string, GameObject> GetIndicatorOnlyObjects() => indicatorOnlyObjects;

    public int GetVisibleObjectCount()
    {
        int count = 0;
        foreach (var kvp in spawnedObjects)
            if (kvp.Value != null && kvp.Value.activeSelf) count++;
        return count;
    }

    // ── 만들기 ──

    // 사진이 있으면 받아 둔 사진을 바로 붙이고, 없거나 못 받았으면 분류 스티커 (기본 면이 보이는 틈이 없다)
    private GameObject CreateObject(StoreData s)
    {
        Texture2D tex = PlaceSpawnGate.Take(Prefix + s.id, out string src);
        try
        {
            GameObject obj = BuildObject(s);
            if (obj != null)
            {
                var display = obj.GetComponentInChildren<ImageDisplayController>(true);
                if (display != null)
                {
                    if (tex != null && !string.IsNullOrEmpty(s.photo) && src == s.photo) { display.SetBaseMapTexture(tex, s.photo); tex = null; }
                    else display.SetBaseMapFromResources(StickerPath(s.category));
                }
            }
            return obj;
        }
        finally { if (tex != null) Destroy(tex); }
    }

    private GameObject BuildObject(StoreData s)
    {
        if (HiddenPlaces.IsHidden(Prefix + s.id) || samplePrefab == null) return null;
        GameObject obj = GetFromPool();
        if (obj == null) return null;

        bool show = ShouldShow(s.category) && InRange(s, PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f));
        Target target = obj.GetComponentInChildren<Target>(true);
        if (!show && target != null) target.enabled = false;   // 켜는 순간 인디케이터가 등록되지 않게

        obj.SetActive(true);
        obj.name = "Store_" + s.id;
        if (!Setup(obj, s))
        {
            if (target != null) target.enabled = true;
            ReturnToPool(obj);
            return null;
        }
        if (!show)
        {
            obj.SetActive(false);
            if (target != null) target.enabled = true;
        }
        ARObjectZoomController.Fit(obj);   // 핀치 줌 중에 뜨거나 풀에서 다시 꺼내도 지금 배율로
        spawnedObjects[s.id] = obj;
        return obj;
    }

    private bool Setup(GameObject obj, StoreData s)
    {
        var doubleTap = obj.GetComponentInChildren<DoubleTap3D>(true);
        var anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
        var target = obj.GetComponentInChildren<Target>(true);
        if (doubleTap == null || anchor == null || target == null) return false;

        ConfigureInfo(doubleTap, s);
        doubleTap.SetImageUrls(Photos(s), false);
        anchor.SetCoordinatesAndCreateAnchor(s.lat, s.lon, 0);   // 땅 높이 (관광공사 자료와 같다)
        target.PlaceName = string.IsNullOrEmpty(s.name)
            ? R0926LocalizedText.Say("알 수 없는 장소", "Unknown place", "不明な場所", "未知地点", "Lugar desconocido")
            : s.name;
        target.placeId = s.id;
        target.gpsLatitude = s.lat;    // 거리 · 길 안내 화살표 (R0926DetailGuide) — 앵커가 아직 없어도
        target.gpsLongitude = s.lon;
        target.TargetColor = DataManager.GetCategoryColor(s.category);
        return true;
    }

    private GameObject GetFromPool()
    {
        while (objectPool.Count > 0)
        {
            GameObject obj = objectPool.Dequeue();
            if (obj == null) continue;
            var doubleTap = obj.GetComponentInChildren<DoubleTap3D>(true);
            if (doubleTap != null) doubleTap.ResetData();
            return obj;
        }
        if (spawnedObjects.Count >= poolSize * 4) return null;
        return NewObject();
    }

    // 본(관광공사 큐브)에는 앞면에 그림을 붙이는 부품이 없다 — 만들 때 붙인다 (분류 스티커 · 가게 사진).
    // 본이 꺼져 있어 사본도 꺼진 채 나오므로, 켜질 때 부품의 Awake 가 원래 면을 기억한다 (풀로 돌아갈 때 되돌릴 면)
    private GameObject NewObject()
    {
        GameObject obj = Instantiate(samplePrefab, Vector3.zero, Quaternion.identity);
        if (obj.GetComponentInChildren<ImageDisplayController>(true) != null) return obj;
        var doubleTap = obj.GetComponentInChildren<DoubleTap3D>(true);
        var face = doubleTap != null ? doubleTap.GetComponent<Renderer>() : null;
        if (face == null) { Debug.LogError("[StoreManager] 오브젝트 본에 앞면이 없음"); return obj; }
        var display = obj.AddComponent<ImageDisplayController>();
        display.cubeRenderer = face;
        display.doubleTap3DScript = doubleTap;
        return obj;
    }

    // 풀에 있는 동안 사진을 들고 있지 않는다 (스티커는 함께 쓰는 에셋이라 지우지 않는다 — ImageDisplayController)
    private void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;
        var display = obj.GetComponentInChildren<ImageDisplayController>(true);
        if (display != null) display.ClearImages();
        var doubleTap = obj.GetComponentInChildren<DoubleTap3D>(true);
        if (doubleTap != null) doubleTap.ReleasePhotos();
        obj.SetActive(false);
        objectPool.Enqueue(obj);
    }

    // ── 사진을 다 받은 뒤에 띄우기 (11-19 P1 · PlaceSpawnGate — 관광공사 자료와 같은 길) ──

    /// <summary>사진을 다 받았으면(사진이 없으면) true. 아니면 준비를 시작하고 false — 다 받으면 SpawnPhotoReady 가 띄운다</summary>
    private bool PhotosReady(StoreData s)
    {
        if (string.IsNullOrEmpty(s.photo)) return true;
        string key = Prefix + s.id;
        if (PlaceSpawnGate.Ready(key)) return true;
        if (PlaceSpawnGate.Busy(key)) return false;
        string id = s.id;
        PlaceSpawnGate.Prepare(key, s.photo, Photos(s), () => WantsSpawn(id), OnPhotosReady);
        return PlaceSpawnGate.Ready(key);
    }

    /// <summary>아직 띄울 곳인가 — 배분이 3D 로 고른 곳. 숨겼거나 · 이미 떴거나 · 목록에서 빠졌으면 false</summary>
    private bool WantsSpawn(string id)
    {
        if (this == null || id == null || spawnedObjects.ContainsKey(id) || !placeDataMap.ContainsKey(id) || !InList(id) || HiddenPlaces.IsHidden(Prefix + id)) return false;
        return FindFm() == null || !fm.isActiveAndEnabled || fm.WantsFull(Prefix + id);
    }

    private void OnPhotosReady(string key)
    {
        if (this == null || key == null || key.Length <= Prefix.Length) return;
        photoReadyQueue.Enqueue(key.Substring(Prefix.Length));
        if (photoReadyLoop == null && isActiveAndEnabled) photoReadyLoop = StartCoroutine(SpawnPhotoReady());
    }

    // 사진을 다 받은 곳을 한 프레임에 하나씩 (SpawnPace — 배분과 같은 문)
    private IEnumerator SpawnPhotoReady()
    {
        yield return null;   // 부른 자리(배분 중)에서 바로 만들지 않는다
        while (photoReadyQueue.Count > 0)
        {
            while (!SpawnPace.Ready) yield return null;
            string id = photoReadyQueue.Dequeue();
            string key = Prefix + id;
            if (!WantsSpawn(id)) { PlaceSpawnGate.Drop(key); continue; }
            if (!PlaceSpawnGate.Ready(key)) continue;   // 그새 다른 길로 가져갔다 · 새로 준비하는 중
            if (indicatorOnlyObjects.ContainsKey(id)) DespawnIndicatorOnly(id);   // 받는 동안 둔 임시 화살표
            GameObject obj = null;
            try { obj = CreateObject(placeDataMap[id]); }
            catch (System.Exception ex) { Debug.LogError($"[StoreManager] 가게 오브젝트 만들기 예외: {id}: {ex.Message}"); }
            if (obj != null) SpawnPace.Took();
        }
        photoReadyLoop = null;
    }

    // ============================================================
    // IPlaceCacheProvider
    // ============================================================

    public string FilterKey => KeyName;
    public int MaxCacheSize => 50;
    public bool IsCacheReady => isCacheReady;
    public float LastLoadedAt { get; private set; } = -1f;

    public List<CachedPlaceData> GetCachedPlaces() => lightCache;

    public bool SpawnFullObject(string rawId)
    {
        if (HiddenPlaces.IsHidden(Prefix + rawId)) return false;   // 이 기기에서 X 로 숨긴 장소
        if (spawnedObjects.ContainsKey(rawId)) return true;
        if (!placeDataMap.TryGetValue(rawId, out StoreData s)) return false;
        if (!PhotosReady(s)) return false;   // 사진을 받는 중 — FilterManager 가 임시 화살표를 둔다 · 다 받으면 SpawnPhotoReady 가
        return CreateObject(s) != null;
    }

    public bool SpawnIndicatorOnly(string rawId)
    {
        if (HiddenPlaces.IsHidden(Prefix + rawId)) return false;
        if (indicatorOnlyObjects.ContainsKey(rawId)) return true;
        if (indicatorOnlyPrefab == null) return false;
        CachedPlaceData cached = lightCache.Find(c => c.rawId == rawId);
        if (cached == null) return false;

        GameObject obj = null;
        while (obj == null && indicatorOnlyPool.Count > 0) obj = indicatorOnlyPool.Dequeue();
        if (obj == null) obj = Instantiate(indicatorOnlyPrefab);
        obj.name = "Indicator_Store_" + rawId;
        obj.SetActive(true);

        var target = obj.GetComponentInChildren<Target>(true);
        if (target != null)
        {
            target.PlaceName = cached.displayName;
            target.placeId = cached.rawId;
            target.gpsLatitude = cached.latitude;
            target.gpsLongitude = cached.longitude;
            target.TargetColor = DataManager.GetCategoryColor(cached.category);
        }
        var anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
        if (anchor != null) anchor.SetCoordinatesAndCreateAnchor(cached.latitude, cached.longitude, 0);

        indicatorOnlyObjects[rawId] = obj;
        return true;
    }

    public void DespawnFullObject(string rawId)
    {
        if (!spawnedObjects.TryGetValue(rawId, out GameObject obj)) return;
        spawnedObjects.Remove(rawId);
        ReturnToPool(obj);
        ForgetIfGone(rawId);
    }

    public void DespawnIndicatorOnly(string rawId)
    {
        if (!indicatorOnlyObjects.TryGetValue(rawId, out GameObject obj)) return;
        indicatorOnlyObjects.Remove(rawId);
        ForgetIfGone(rawId);
        if (obj == null) return;
        obj.SetActive(false);
        indicatorOnlyPool.Enqueue(obj);
    }

    private bool InList(string rawId) => lightCache.Exists(c => c.rawId == rawId);

    // 새 목록에서 빠진 가게 — 떠 있는 동안 남겨 둔 정보를 배분이 거둔 뒤에 지운다 (목록 · 근처 모달에 남지 않게)
    private void ForgetIfGone(string rawId)
    {
        if (spawnedObjects.ContainsKey(rawId) || indicatorOnlyObjects.ContainsKey(rawId) || InList(rawId)) return;
        placeDataMap.Remove(rawId);
    }

    public HashSet<string> GetSpawnedFullIds()
    {
        var result = new HashSet<string>();
        foreach (string id in spawnedObjects.Keys) result.Add(Prefix + id);
        return result;
    }

    public HashSet<string> GetSpawnedIndicatorIds()
    {
        var result = new HashSet<string>();
        foreach (string id in indicatorOnlyObjects.Keys) result.Add(Prefix + id);
        return result;
    }

    public void RefreshCache(float lat, float lon)
    {
        float now = Time.realtimeSinceStartup;
        if (now < dormantUntil)
        {
            MarkCacheReady();   // 쉬는 중 — 빈 목록 그대로 받은 것으로 (다시 받기가 계속 묻지 않게)
            return;
        }
        if (fetching || now < backoffUntil || !isActiveAndEnabled) return;
        StartCoroutine(Fetch(lat, lon));
    }

    /// <summary>
    /// 앵커 생성에 실패한 오브젝트만 선별하여 재시도 — FilterManager.AllocationLoop 가 매 tick 호출
    /// </summary>
    public void RetryFailedAnchors()
    {
        RetryFailedAnchorsIn(spawnedObjects);
        RetryFailedAnchorsIn(indicatorOnlyObjects);
    }

    private static void RetryFailedAnchorsIn(Dictionary<string, GameObject> objects)
    {
        foreach (var kvp in objects)
        {
            if (kvp.Value == null || !kvp.Value.activeSelf) continue;
            var anchor = kvp.Value.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor == null || anchor.IsAnchorCreated || anchor.IsRetrying) continue;
            anchor.RecreateAnchor();
        }
    }
}

/// <summary>근처 가게 한 곳 — 서버 /api/stores/near 의 items 한 줄</summary>
[System.Serializable]
public class StoreData
{
    public string id { get; set; }              // "st_<상가업소번호>"
    public string name { get; set; }
    public string category { get; set; }        // food · cafe · shop · stay
    public string upjong { get; set; }          // 업종 (상권업종소분류명)
    public string floor { get; set; }           // 층 (flrNo 그대로)
    public string addr { get; set; }
    public double lat { get; set; }
    public double lon { get; set; }
    public string photo { get; set; }           // 관광공사 자료와 이어진 가게만
    public string photo_credit { get; set; }
    public string tel { get; set; }             // 전화 — 인허가(영업 중)와 맞춘 가게만, 아니면 "" (11-27)
    public bool owner { get; set; }             // 가게 주인이 「내 가게」로 고친 곳 (나중에)
    public string source { get; set; }
    public float dist { get; set; }
}

public class StoresResponse
{
    public bool ok { get; set; }
    public string status { get; set; }          // "ok" · "not_ready" (서버가 아직 자료를 받기 전)
    public string source { get; set; }
    public List<StoreData> items { get; set; }
}
