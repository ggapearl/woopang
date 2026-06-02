using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// dance_anim (category="anim") 인디케이터 탭 → 확인 패널 → 명시 다운로드 → Full 스폰 → 자동 디스폰.
///
/// 설계 의도:
/// - 자동 Full 스폰 금지(FilterManager에서 차단). 데이터·배터리·저사양폰 부담 차단.
/// - 사용자가 인디케이터를 직접 탭하고 "3D 보기" 버튼을 눌렀을 때만 GLB 다운로드 + 캐릭터 등장.
/// - 활성 스폰은 거리 100m / 시간 3분 초과 시 자동 정리해 누적 부담 방지.
///
/// Panel UI는 [DanceAnimPanelSetup] 에디터 스크립트가 씬에 자동 생성 + Inspector 필드 연결.
/// </summary>
public class DanceAnimController : MonoBehaviour
{
    public static DanceAnimController Instance { get; private set; }

    /// <summary>
    /// Instance가 null이면 씬에서 찾아 설정. Awake 타이밍 문제 또는 GameObject가
    /// 비활성 상태인 경우의 안전망. 발견 못하면 null 반환.
    /// </summary>
    public static DanceAnimController EnsureInstance()
    {
        if (Instance != null) return Instance;
        var found = Object.FindAnyObjectByType<DanceAnimController>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            Debug.LogWarning($"[dbg-DanceAnim] EnsureInstance: 씬에서 발견 ({found.gameObject.name}) — Awake 못 돌았던 듯");
            return found;
        }
        Debug.LogError($"[dbg-DanceAnim] EnsureInstance: 씬에 DanceAnimController 자체가 없음 — 빌드된 씬 확인 필요");
        return null;
    }

    [Header("Panel UI (DanceAnimPanelSetup 에디터 스크립트가 자동 연결)")]
    public GameObject confirmPanel;
    public Text titleText;
    public Text sizeText;
    public Button confirmButton;
    public Button cancelButton;
    public GameObject progressGroup;
    public Text progressText;

    [Header("Auto-Despawn")]
    [Tooltip("이 거리(m) 이상 멀어지면 자동 디스폰")]
    public float despawnDistanceMeters = 100f;
    [Tooltip("이 시간(초) 경과 후 자동 디스폰")]
    public float despawnTimeoutSeconds = 180f;
    [Tooltip("디스폰 체크 주기(초)")]
    public float despawnCheckInterval = 5f;

    [Header("Spawn 대기")]
    [Tooltip("Detail API + GLB 다운로드 대기 최대 시간(초)")]
    public float spawnTimeoutSeconds = 20f;

    // 현재 활성 스폰 추적 (placeId → 스폰 시각)
    private readonly Dictionary<int, float> activeSpawns = new Dictionary<int, float>();
    private int pendingId = -1;
    private string pendingName = "";
    private Coroutine spawnFlow;

    void Awake()
    {
        string parentName = transform.parent != null ? transform.parent.name : "<root>";
        Debug.Log($"[dbg-DanceAnim] Awake on '{gameObject.name}' (active={gameObject.activeInHierarchy}, parent={parentName})");
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[dbg-DanceAnim] 중복 인스턴스 발견 — 자기 자신 destroy");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log($"[dbg-DanceAnim] Instance 설정 완료 (id={GetInstanceID()})");
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmPressed);
        if (cancelButton != null) cancelButton.onClick.AddListener(HideConfirm);
    }

    void OnEnable()
    {
        // Awake 못 돌면 OnEnable에서 한 번 더 시도
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        StartCoroutine(AutoDespawnLoop());
    }

    /// <summary>큐브 플레이스홀더가 더블탭됐을 때 호출 — 다운로드 확인 패널 표시.</summary>
    public void OnAnimCubeDoubleTapped(int placeId, string placeName)
    {
        Debug.Log($"[dbg-DanceAnim] OnAnimCubeDoubleTapped: id={placeId} name='{placeName}'");

        if (activeSpawns.ContainsKey(placeId) &&
            DataManager.Instance != null &&
            DataManager.Instance.GetSpawnedObjects().ContainsKey(placeId))
        {
            Debug.Log($"[dbg-DanceAnim] 이미 활성 GLB 스폰됨 — 무시 (id={placeId})");
            return;
        }

        activeSpawns.Remove(placeId);
        pendingId = placeId;
        pendingName = placeName ?? "3D 콘텐츠";

        // 파일 크기 미리 확인 (HEAD 요청, 비동기 업데이트)
        StartCoroutine(FetchSizeAndUpdateUI(placeId));

        if (titleText != null) titleText.text = pendingName;
        if (sizeText != null) sizeText.text = "크기 확인 중...";
        if (progressGroup != null) progressGroup.SetActive(false);
        if (confirmButton != null) confirmButton.interactable = true;
        if (confirmPanel != null) confirmPanel.SetActive(true);
    }

    IEnumerator FetchSizeAndUpdateUI(int id)
    {
        DataManager dm = DataManager.Instance;
        if (dm == null) yield break;
        if (!dm.GetPlaceDataMap().TryGetValue(id, out var place)) yield break;
        string url = ResolveUrl(place.model_url);
        if (string.IsNullOrEmpty(url)) yield break;

        using (var head = UnityWebRequest.Head(url))
        {
            head.timeout = 5;
            yield return head.SendWebRequest();
            if (head.result == UnityWebRequest.Result.Success)
            {
                string lenStr = head.GetResponseHeader("Content-Length");
                if (long.TryParse(lenStr, out long bytes) && bytes > 0)
                {
                    string sizeStr = bytes < 1024 * 1024
                        ? $"{bytes / 1024f:F1} KB"
                        : $"{bytes / (1024f * 1024f):F1} MB";
                    if (id == pendingId && sizeText != null)
                        sizeText.text = $"3D 보기 ({sizeStr} · WiFi 권장)";
                    Debug.Log($"[dbg-DanceAnim] HEAD OK: id={id} size={bytes:N0} bytes ({sizeStr})");
                }
            }
            else
            {
                Debug.LogWarning($"[dbg-DanceAnim] HEAD 실패: {head.error}");
                if (id == pendingId && sizeText != null)
                    sizeText.text = "3D 보기 (다운로드 필요)";
            }
        }
    }

    static string ResolveUrl(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        if (raw.StartsWith("http")) return raw;
        return ApiConfig.MAIN_SERVER + "/" + raw.Replace("\\", "/");
    }

    /// <summary>구 인터페이스 호환 (인디케이터 탭) — 큐브 더블탭과 동일 처리.</summary>
    public void OnIndicatorTapped(int placeId, string placeName) => OnAnimCubeDoubleTapped(placeId, placeName);

    public void HideConfirm()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        pendingId = -1;
    }

    void OnConfirmPressed()
    {
        if (pendingId < 0) { HideConfirm(); return; }
        if (spawnFlow != null) StopCoroutine(spawnFlow);
        spawnFlow = StartCoroutine(SpawnFlow(pendingId));
    }

    IEnumerator SpawnFlow(int id)
    {
        Debug.Log($"[dbg-DanceAnim] SpawnFlow START id={id}");
        if (progressGroup != null) progressGroup.SetActive(true);
        if (confirmButton != null) confirmButton.interactable = false;
        if (progressText != null) progressText.text = "준비 중...";

        DataManager dm = DataManager.Instance;
        if (dm == null)
        {
            Debug.LogError("[dbg-DanceAnim] DataManager.Instance is NULL");
            HideConfirm();
            yield break;
        }

        // 1) PlaceData 확보 (detail 없으면 SpawnFullObject가 batch fetch 트리거)
        if (!dm.GetPlaceDataMap().TryGetValue(id, out var place))
        {
            Debug.Log($"[dbg-DanceAnim] placeDataMap에 id={id} 없음 — detail fetch 트리거");
            if (progressText != null) progressText.text = "데이터 받는 중...";
            dm.SpawnFullObject(id.ToString());
            float fetchWait = 0f;
            while (fetchWait < 10f && !dm.GetPlaceDataMap().ContainsKey(id))
            {
                yield return new WaitForSeconds(0.3f);
                fetchWait += 0.3f;
            }
            if (!dm.GetPlaceDataMap().TryGetValue(id, out place))
            {
                Debug.LogError($"[dbg-DanceAnim] detail fetch 실패 id={id}");
                if (progressText != null) progressText.text = "데이터 받기 실패. 다시 시도해주세요.";
                yield return new WaitForSeconds(2f);
                HideConfirm();
                yield break;
            }
        }
        Debug.Log($"[dbg-DanceAnim] placeData OK: name={place.name} model_url={place.model_url} model_type={place.model_type}");

        // 2) URL 해결 (상대/풀 둘 다 처리)
        string url = ResolveUrl(place.model_url);
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError($"[dbg-DanceAnim] model_url empty");
            if (progressText != null) progressText.text = "URL 없음. DB 확인 필요.";
            yield return new WaitForSeconds(2f);
            HideConfirm();
            yield break;
        }
        Debug.Log($"[dbg-DanceAnim] resolved URL: {url}");

        // 3) UnityWebRequest로 직접 다운로드 + 진행률
        if (progressText != null) progressText.text = "3D 콘텐츠 다운로드 시작...";
        byte[] glbBytes = null;
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 30;
            var op = req.SendWebRequest();
            float startT = Time.realtimeSinceStartup;
            while (!op.isDone)
            {
                float pct = req.downloadProgress * 100f; // 0..1 → 0..100
                if (progressText != null)
                    progressText.text = $"3D 다운로드 중... {pct:F0}%";
                yield return null;
            }
            float elapsed = Time.realtimeSinceStartup - startT;
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[dbg-DanceAnim] 다운로드 실패: {req.error} (URL={url})");
                if (progressText != null) progressText.text = $"다운로드 실패: {req.error}";
                yield return new WaitForSeconds(3f);
                HideConfirm();
                yield break;
            }
            glbBytes = req.downloadHandler.data;
            Debug.Log($"[dbg-DanceAnim] 다운로드 OK: {glbBytes.Length:N0} bytes / {elapsed:F1}s");
        }

        // 4) GLBModelLoader 캐시에 사전 주입 — Promote가 곧 호출할 때 네트워크 안 거치게
        GLBModelLoader.PreloadCache(url, glbBytes);
        Debug.Log($"[dbg-DanceAnim] PreloadCache 주입 완료");

        if (progressText != null) progressText.text = "3D 오브젝트 로딩 중...";

        // 5) 큐브 → GLB 교체 (PromoteCubeToGLB가 디스폰 후 glbPrefab + 캐시된 바이트로 즉시 로드)
        bool promoted = dm.PromoteCubeToGLB(id);
        Debug.Log($"[dbg-DanceAnim] PromoteCubeToGLB: {promoted}");

        // 6) 실제 GLB 인스턴스화 + 메시 로드 완료까지 대기
        // 로드 시간은 모델 크기/디바이스에 따라 1~5초. % 표시는 평균 3s 기준 추정.
        float spawnWait = 0f;
        const float estimatedLoadSeconds = 3f;
        bool modelReady = false;
        while (spawnWait < spawnTimeoutSeconds)
        {
            yield return new WaitForSeconds(0.1f);
            spawnWait += 0.1f;
            if (dm.GetSpawnedObjects().TryGetValue(id, out var glbObj) && glbObj != null)
            {
                var loader = glbObj.GetComponentInChildren<GLBModelLoader>(true);
                if (loader != null && loader.IsModelLoaded)
                {
                    modelReady = true;
                    break;
                }
                if (progressText != null)
                {
                    // 시간 기반 추정 %, 실제 완료 전엔 95% 캡 (사용자 기다림 체감 완화)
                    float loadPct = Mathf.Min((spawnWait / estimatedLoadSeconds) * 100f, 95f);
                    progressText.text = $"3D 오브젝트 로딩 중... {loadPct:F0}%";
                }
            }
        }

        if (!modelReady)
        {
            Debug.LogWarning($"[dbg-DanceAnim] GLB 로드 타임아웃 id={id} after {spawnWait:F1}s");
            if (progressText != null) progressText.text = "로딩 시간 초과. 다시 시도해주세요.";
            yield return new WaitForSeconds(2f);
            HideConfirm();
            yield break;
        }

        Debug.Log($"[dbg-DanceAnim] GLB 표시 완료 id={id} (총 {spawnWait:F1}s 대기)");
        activeSpawns[id] = Time.realtimeSinceStartup;
        HideConfirm();
    }

    IEnumerator AutoDespawnLoop()
    {
        var wait = new WaitForSeconds(despawnCheckInterval);
        while (true)
        {
            yield return wait;
            if (activeSpawns.Count == 0) continue;

            DataManager dm = DataManager.Instance;
            if (dm == null) continue;

            Vector2 gps = GetUserGPS();
            float now = Time.realtimeSinceStartup;
            var placeMap = dm.GetPlaceDataMap();
            var toRemove = new List<int>();

            foreach (var kv in activeSpawns)
            {
                int id = kv.Key;
                float age = now - kv.Value;
                bool timeout = age >= despawnTimeoutSeconds;
                bool distant = false;

                if ((gps.x != 0f || gps.y != 0f) && placeMap.ContainsKey(id))
                {
                    var p = placeMap[id];
                    float d = HaversineMeters(gps.x, gps.y, p.latitude, p.longitude);
                    if (d > despawnDistanceMeters) distant = true;
                }
                if (timeout || distant) toRemove.Add(id);
            }

            foreach (int id in toRemove)
            {
                dm.DespawnFullObject(id.ToString());
                activeSpawns.Remove(id);
            }
        }
    }

    static Vector2 GetUserGPS()
    {
#if UNITY_EDITOR
        return Vector2.zero;
#else
        if (Input.location.status == LocationServiceStatus.Running)
            return new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        return Vector2.zero;
#endif
    }

    static float HaversineMeters(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000f;
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);
        float a = Mathf.Sin(dLat / 2f) * Mathf.Sin(dLat / 2f) +
                  Mathf.Cos(Mathf.Deg2Rad * lat1) * Mathf.Cos(Mathf.Deg2Rad * lat2) *
                  Mathf.Sin(dLon / 2f) * Mathf.Sin(dLon / 2f);
        return R * 2f * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1f - a));
    }
}
