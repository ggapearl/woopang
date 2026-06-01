using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmPressed);
        if (cancelButton != null) cancelButton.onClick.AddListener(HideConfirm);
    }

    void Start()
    {
        StartCoroutine(AutoDespawnLoop());
    }

    /// <summary>큐브 플레이스홀더가 더블탭됐을 때 호출 — 다운로드 확인 패널 표시.</summary>
    public void OnAnimCubeDoubleTapped(int placeId, string placeName)
    {
        if (activeSpawns.ContainsKey(placeId)) return; // 이미 GLB로 교체됨 무시
        pendingId = placeId;
        pendingName = placeName ?? "3D 콘텐츠";

        if (titleText != null) titleText.text = pendingName;
        if (sizeText != null) sizeText.text = "3D로 보기 (다운로드 필요 · WiFi 권장)";
        if (progressGroup != null) progressGroup.SetActive(false);
        if (confirmButton != null) confirmButton.interactable = true;
        if (confirmPanel != null) confirmPanel.SetActive(true);
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
        if (progressGroup != null) progressGroup.SetActive(true);
        if (confirmButton != null) confirmButton.interactable = false;
        if (progressText != null) progressText.text = "데이터 받는 중...";

        DataManager dm = DataManager.Instance;
        if (dm == null) { HideConfirm(); yield break; }

        // 큐브 → GLB 교체 (PromoteCubeToGLB가 디스폰 후 glbPrefab + model_url로 재스폰).
        // placeDataMap에 detail 없으면 false 반환 → SpawnFullObject가 batch detail fetch 트리거.
        bool promoted = dm.PromoteCubeToGLB(id);
        if (!promoted)
        {
            // detail 없음 → fetch 시작됨, 대기
            dm.SpawnFullObject(id.ToString());
        }

        float waited = 0f;
        while (waited < spawnTimeoutSeconds)
        {
            yield return new WaitForSeconds(0.3f);
            waited += 0.3f;
            if (progressText != null)
                progressText.text = $"3D 받는 중... {waited:F1}s";
            if (dm.GetSpawnedObjects().ContainsKey(id))
            {
                // promote 재시도 — detail 이제 있으므로 성공
                if (!promoted) dm.PromoteCubeToGLB(id);
                break;
            }
        }

        if (!dm.GetSpawnedObjects().ContainsKey(id))
        {
            if (progressText != null) progressText.text = "실패. 잠시 후 다시 시도해주세요.";
            yield return new WaitForSeconds(2f);
            HideConfirm();
            yield break;
        }

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
