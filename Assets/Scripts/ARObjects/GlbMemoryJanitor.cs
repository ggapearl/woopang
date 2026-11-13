using UnityEngine;

/// <summary>
/// GLB 모델을 지우거나 새로 띄울 때마다 하던 메모리 전체 정리(Resources.UnloadUnusedAssets + 강제 GC 두 번)를 한데 모아
/// 잠잠해진 뒤 한 번만 한다 — 11-14 대표님 「3.번은 정리해줘」 (11-06 FPS 검토: 3D 오브젝트가 뜨고 사라질 때마다 전체 정리를
/// 강제로 해서 그 순간 멈칫했다. 점진 GC 가 꺼져 있어 GC 한 번이 화면을 통째로 세우고, 모델 16개가 뜨면 16번 섰다)
/// 쓰지 않게 된 메시 · 재질 · 텍스처는 UnloadUnusedAssets 가 치운다 (그 안에서 GC 도 한 번 돈다)
/// </summary>
public static class GlbMemoryJanitor
{
    private const float QuietSeconds = 1.5f;   // 마지막 요청 뒤 이만큼 잠잠하면 — 한꺼번에 지우고 띄우는 동안은 기다린다
    private const float MinInterval = 10f;     // 정리끼리 최소 간격

    private static float lastRequest, lastRun = float.NegativeInfinity;
    private static bool pending, urgent;
    private static AsyncOperation running;
    private static GameObject host;

    /// <summary>정리 요청 수 · 실제로 정리한 수 (앱을 켠 뒤로) — 성능 기록 · 시험용</summary>
    public static int Requests { get; private set; }
    public static int Runs { get; private set; }

    public static void Request()
    {
        Requests++;
        pending = true;
        lastRequest = Time.realtimeSinceStartup;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lastRequest = 0f; lastRun = float.NegativeInfinity;
        pending = urgent = false;
        running = null; host = null;
        Requests = Runs = 0;
    }

    // 장면을 읽은 뒤 한 번 — 모델의 OnDestroy(장면을 닫을 때)에서 새 오브젝트를 만들지 않게 미리 둔다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (host != null) return;
        host = new GameObject("GlbMemoryJanitor") { hideFlags = HideFlags.HideInHierarchy };
        Object.DontDestroyOnLoad(host);
        host.AddComponent<Runner>();
        Application.lowMemory -= OnLowMemory;
        Application.lowMemory += OnLowMemory;
    }

    // 기기가 메모리가 모자라다고 하면 간격을 기다리지 않는다
    private static void OnLowMemory() { pending = true; urgent = true; }

    private static void Tick()
    {
        if (!pending || running != null && !running.isDone) return;
        float now = Time.realtimeSinceStartup;
        if (!urgent)
        {
            if (BootOverlay.Showing) return;   // 시작화면 동안은 미룬다
            if (now - lastRequest < QuietSeconds || now - lastRun < MinInterval) return;
        }
        pending = urgent = false;
        lastRun = now;
        Runs++;
        running = Resources.UnloadUnusedAssets();
    }

    private class Runner : MonoBehaviour
    {
        private void Update() => Tick();
    }
}
