using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// 실제 눈처럼 보이게 — 가까우면 또렷하게, 멀면 옅고 뿌옇게 (대기 원근).
///  · 안개(대기색): 씬 안개를 켜고 색은 카메라가 잰 주변 밝기에 맞춘다 (낮=옅은 하늘색, 밤=어두운 남색)
///  · 큐브 테두리 빛: 가까우면 조금 더 밝게(작아도 잘 보이게), 멀면 약하게
/// AR 오브젝트(큐브·3D 캐릭터)를 거리에 따라 살짝 흐리게 — 화면 밖 화살표·박스의 거리별 흐림과 같은 방식.
///  · 큐브(T5EdgeLine 셰이더): _GlobalAlpha 로 투명도를 낮춘다 (셰이더가 원래 투명 렌더링)
///  · GLB 등 불투명 재질: 투명으로 바꾸면 겹침이 깨져서, 대신 색을 살짝 어둡게 해 멀어 보이게 한다
/// 오브젝트마다 MaterialPropertyBlock 으로만 덮어써서 원본 재질·Renderer.enabled(표시/숨김 로직)는 건드리지 않는다.
/// 대상: 5개 데이터 매니저의 GetSpawnedObjects() 전부.
/// </summary>
public class R0926DistanceFade : MonoBehaviour
{
    [Header("거리 (m)")]
    [Tooltip("이 거리까지는 선명")]
    [SerializeField] private float nearDistance = 40f;
    [Tooltip("이 거리부터 가장 흐림")]
    [SerializeField] private float farDistance = 400f;

    [Header("가장 멀 때")]
    [Range(0f, 1f)] [Tooltip("큐브 투명도 (1=그대로)")]
    [SerializeField] private float farAlpha = 0.62f;
    [Range(0f, 1f)] [Tooltip("3D 캐릭터 등 불투명 재질의 밝기 배율 (1=그대로)")]
    [SerializeField] private float farTint = 0.82f;
    [Tooltip("가까울 때 큐브 테두리 빛 배율 — 작아도 잘 보이게")]
    [SerializeField] private float nearEdgeBoost = 1.25f;
    [Tooltip("멀 때 큐브 테두리 빛 배율")]
    [SerializeField] private float farEdge = 0.5f;

    [Header("대기 원근 (안개)")]
    [SerializeField] private bool useFog = true;
    [SerializeField] private float fogStart = 60f;
    [SerializeField] private float fogEnd = 1500f;
    [SerializeField] private Color dayHaze = new Color(0.77f, 0.81f, 0.85f, 1f);
    [SerializeField] private Color nightHaze = new Color(0.16f, 0.19f, 0.25f, 1f);

    [Header("실제 스폰 반경에 맞추기")]
    [Tooltip("FilterManager 의 3D 오브젝트 반경(기본 500m)에 흐림·안개·카메라 거리를 맞춘다. " +
             "3D 오브젝트는 그 반경 안에만 생기고 밖은 화살표·박스(메시 없음)라, 카메라를 1000m 까지 그릴 이유가 없다.")]
    [SerializeField] private bool matchSpawnRadius = true;
    [Tooltip("카메라 최대 렌더 거리 = 스폰 반경 + 이 여유 (GPS 오차·배분 히스테리시스)")]
    [SerializeField] private float farClipMargin = 150f;

    [Tooltip("큐브가 처음 보일 때 서서히 나타나는 시간 (초) — 툭 튀어나오지 않게")]
    [SerializeField] private float fadeInSeconds = 0.45f;

    [Tooltip("갱신 주기 (초) — 매 프레임 할 필요 없음 (나타나는 중일 때만 매 프레임)")]
    [SerializeField] private float interval = 0.2f;

    private readonly Dictionary<int, float> shownAt = new Dictionary<int, float>();
    private readonly Dictionary<int, float> lastShown = new Dictionary<int, float>();
    private bool anyAppearing;

    private float farClip = -1f;   // matchSpawnRadius 일 때 카메라에 넣을 값

    private static readonly int GlobalAlpha = Shader.PropertyToID("_GlobalAlpha");
    private static readonly int EdgeIntensity = Shader.PropertyToID("_EdgeIntensity");
    private static readonly int[] ColorIds =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("baseColorFactor"),   // glTFast 셰이더 그래프
        Shader.PropertyToID("_BaseColorFactor"),
        Shader.PropertyToID("_Color"),
    };

    private class Entry
    {
        public Renderer[] renderers;
        public float refreshAt;
    }

    private readonly Dictionary<int, Entry> cache = new Dictionary<int, Entry>();
    private readonly List<int> seen = new List<int>();
    private MaterialPropertyBlock mpb;
    private Camera cam;
    private float next;

    private ARCameraManager arCam;
    private float brightness = 0.6f;   // 0(밤)~1(낮) — 빛 추정이 없으면 낮 쪽 기본값

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        var fm = matchSpawnRadius ? FindFirstObjectByType<FilterManager>() : null;
        if (fm != null && fm.FullObjectRadius > 50f)
        {
            float r = fm.FullObjectRadius;
            farDistance = r * 0.9f;          // 가장 먼 3D 오브젝트가 가장 옅게
            fogEnd = r * 2.4f;               // 반경 끝에서 약 40% 뿌옇게 — 사라지지는 않는다
            farClip = r + farClipMargin;     // 500m → 650m
        }
        if (useFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.fogColor = Color.Lerp(nightHaze, dayHaze, brightness);
        }
        arCam = FindFirstObjectByType<ARCameraManager>();
        if (arCam != null) arCam.frameReceived += OnFrame;
    }

    private void OnDestroy()
    {
        if (arCam != null) arCam.frameReceived -= OnFrame;
    }

    private void OnFrame(ARCameraFrameEventArgs args)
    {
        if (args.lightEstimation.averageBrightness.HasValue)
            brightness = Mathf.Lerp(brightness, Mathf.Clamp01(args.lightEstimation.averageBrightness.Value * 1.6f), 0.05f);
    }

    private void Update()
    {
        if (Time.unscaledTime < next) return;
        anyAppearing = false;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        if (farClip > 0f && !Mathf.Approximately(cam.farClipPlane, farClip)) cam.farClipPlane = farClip;

        if (useFog) RenderSettings.fogColor = Color.Lerp(nightHaze, dayHaze, brightness);

        seen.Clear();
        if (DataManager.Instance != null) Visit(DataManager.Instance.GetSpawnedObjects()?.Values);
        if (TourAPIManager.Instance != null) Visit(TourAPIManager.Instance.GetSpawnedObjects()?.Values);
        if (SubwayManager.Instance != null) Visit(SubwayManager.Instance.GetSpawnedObjects()?.Values);
        if (TrainStationManager.Instance != null) Visit(TrainStationManager.Instance.GetSpawnedObjects()?.Values);
        if (TerminalManager.Instance != null) Visit(TerminalManager.Instance.GetSpawnedObjects()?.Values);

        // 사라진 오브젝트 캐시 정리
        if (cache.Count > seen.Count + 32)
        {
            var keep = new HashSet<int>(seen);
            var drop = new List<int>();
            foreach (var k in cache.Keys) if (!keep.Contains(k)) drop.Add(k);
            foreach (var k in drop) { cache.Remove(k); shownAt.Remove(k); lastShown.Remove(k); }
        }
        next = Time.unscaledTime + (anyAppearing ? 0f : interval);   // 나타나는 동안만 매 프레임
    }

    // 렌더러가 실제로 켜진 순간부터 잰다 (앵커가 잡히기 전엔 꺼져 있다). 0.6초 넘게 안 보였다면 다시 나타나는 것.
    private float Appear(int id, Renderer[] renderers)
    {
        bool on = false;
        foreach (var r in renderers) if (r != null && r.enabled) { on = true; break; }
        if (!on) { lastShown.Remove(id); return 0f; }
        float now = Time.unscaledTime;
        if (!lastShown.TryGetValue(id, out float ls) || now - ls > 0.6f) shownAt[id] = now;
        lastShown[id] = now;
        float t = fadeInSeconds > 0f ? Mathf.Clamp01((now - shownAt[id]) / fadeInSeconds) : 1f;
        if (t < 1f) anyAppearing = true;
        return t * t * (3f - 2f * t);
    }

    private void Visit(IEnumerable<GameObject> objects)
    {
        if (objects == null) return;
        Vector3 eye = cam.transform.position;
        foreach (var go in objects)
        {
            if (go == null || !go.activeInHierarchy) continue;
            int id = go.GetInstanceID();
            seen.Add(id);

            if (!cache.TryGetValue(id, out var e) || Time.unscaledTime >= e.refreshAt)
            {
                // GLB 는 늦게 붙으므로 가끔 다시 모은다
                e = new Entry { renderers = go.GetComponentsInChildren<Renderer>(true), refreshAt = Time.unscaledTime + 2f };
                cache[id] = e;
            }

            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(nearDistance, farDistance, Vector3.Distance(eye, go.transform.position)));
            float appear = Appear(id, e.renderers);               // 처음 보일 때 0→1 (큐브만 — 불투명 GLB 는 투명으로 못 바꾼다)
            float alpha = Mathf.Lerp(1f, farAlpha, t) * appear;
            float tint = Mathf.Lerp(1.04f, farTint, t);          // 가까우면 아주 살짝 밝게
            float edge = Mathf.Lerp(nearEdgeBoost, farEdge, t) * appear;
            foreach (var r in e.renderers)
                if (r != null) Apply(r, alpha, tint, edge);
        }
    }

    private void Apply(Renderer r, float alpha, float tint, float edge)
    {
        var mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (m == null) continue;
            r.GetPropertyBlock(mpb, i);   // 다른 코드가 넣은 값은 살린다
            if (m.HasProperty(GlobalAlpha))
            {
                mpb.SetFloat(GlobalAlpha, m.GetFloat(GlobalAlpha) * alpha);
                if (m.HasProperty(EdgeIntensity)) mpb.SetFloat(EdgeIntensity, m.GetFloat(EdgeIntensity) * edge);
            }
            else
            {
                foreach (var cid in ColorIds)
                {
                    if (!m.HasProperty(cid)) continue;
                    Color c = m.GetColor(cid);
                    mpb.SetColor(cid, new Color(c.r * tint, c.g * tint, c.b * tint, c.a));
                    break;
                }
            }
            r.SetPropertyBlock(mpb, i);
        }
    }
}
