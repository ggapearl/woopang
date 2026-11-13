using System;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// 성능 기록 — 10초마다 한 줄: 프레임 속도 · 가장 긴 프레임 · 멈칫(50ms 넘는 프레임) 수 · 배터리 잔량 · 충전 상태 · 발열 단계 ·
/// 저전력 모드 · 메모리 · 목표 FPS · GLB 정리(요청/실제)를 기기 안 파일(앱 문서 폴더 perf_log.txt)에 남긴다.
/// 서버로 보내지 않고, 위치 · 계정은 적지 않는다. 1분에 한 번은 같은 줄을 기기 로그(Xcode 콘솔 · logcat 'Unity')에도 남긴다.
/// 11-14 대표님 「3.번은 정리해줘」 — 11-06 「노래 틀면 60→30」 검토 때 제안: 맥 iOS 빌드 세션이 숫자로 확인하면 추측이 필요 없다
/// </summary>
public static class PerfLog
{
    public const string FileName = "perf_log.txt";
    private const float Interval = 10f;
    private const int ConsoleEvery = 6;            // 기기 로그는 여섯 줄에 한 번 (1분)
    private const long MaxBytes = 512 * 1024;      // 넘으면 perf_log.1.txt 로 넘기고 새로
    private const float HitchSeconds = 0.05f;

    private static GameObject host;
    private static ProfilerRecorder sysMem, gcMem;
    private static float windowStart, worst;
    private static int frames, hitches, lines;
    private static string path;

    /// <summary>마지막으로 남긴 줄 — 시험용</summary>
    public static string LastLine { get; private set; }
    public static string FilePath => path;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int _WoopangThermalState();
    [DllImport("__Internal")] private static extern int _WoopangLowPowerMode();
    private static readonly string[] IosThermal = { "nominal", "fair", "serious", "critical" };
#elif UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject power;
    private static int sdk = -1;
    private static readonly string[] AndroidThermal = { "none", "light", "moderate", "severe", "critical", "emergency", "shutdown" };
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        host = null; path = null; LastLine = null;
        frames = hitches = lines = 0; worst = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (host != null) return;
        try
        {
            path = Path.Combine(Application.persistentDataPath, FileName);
            sysMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            gcMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            Write($"=== start {Stamp()} v{Application.version} {Application.platform} {SystemInfo.deviceModel} {SystemInfo.operatingSystem} ===");
        }
        catch (Exception) { }
        host = new GameObject("PerfLog") { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<Runner>();
        Restart();
    }

    private static void Restart()
    {
        windowStart = Time.realtimeSinceStartup;
        frames = hitches = 0;
        worst = 0f;
    }

    private static void Frame()
    {
        float dt = Time.unscaledDeltaTime;
        frames++;
        if (dt > worst) worst = dt;
        if (dt > HitchSeconds) hitches++;
        float now = Time.realtimeSinceStartup;
        if (now - windowStart < Interval) return;
        Sample(now - windowStart);
        Restart();
    }

    private static void Sample(float span)
    {
        string thermal = "?";
        int lowPower = -1;
        DeviceState(ref thermal, ref lowPower);
        float batt = SystemInfo.batteryLevel;
        var ci = CultureInfo.InvariantCulture;
        string line = string.Format(ci,
            "{0} t={1:0} fps={2:0.0} target={3} worst={4:0}ms hitch={5} batt={6} charge={7} thermal={8} lowpower={9} sysmem={10}MB gcmem={11}MB glbclean={12}/{13}",
            Stamp(), Time.realtimeSinceStartup, frames / Mathf.Max(span, 0.001f), Application.targetFrameRate, worst * 1000f, hitches,
            batt < 0f ? "?" : Mathf.RoundToInt(batt * 100f).ToString(ci), SystemInfo.batteryStatus, thermal, lowPower < 0 ? "?" : lowPower.ToString(ci),
            Mb(sysMem), Mb(gcMem), GlbMemoryJanitor.Runs, GlbMemoryJanitor.Requests);
        LastLine = line;
        Write(line);
#if !UNITY_EDITOR
        if (lines % ConsoleEvery == 0) Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[PerfLog] {0}", line);
#endif
        lines++;
    }

    private static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static long Mb(ProfilerRecorder r) => r.Valid ? r.LastValue / (1024 * 1024) : -1;

    private static void DeviceState(ref string thermal, ref int lowPower)
    {
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            int s = _WoopangThermalState();
            thermal = s >= 0 && s < IosThermal.Length ? IosThermal[s] : s.ToString(CultureInfo.InvariantCulture);
            lowPower = _WoopangLowPowerMode();
        }
        catch (Exception) { }
#elif UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (power == null)
            {
                using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                    power = act.Call<AndroidJavaObject>("getSystemService", "power");
            }
            if (power == null) return;
            lowPower = power.Call<bool>("isPowerSaveMode") ? 1 : 0;
            if (sdk >= 29)
            {
                int s = power.Call<int>("getCurrentThermalStatus");
                thermal = s >= 0 && s < AndroidThermal.Length ? AndroidThermal[s] : s.ToString(CultureInfo.InvariantCulture);
            }
        }
        catch (Exception) { }
#endif
    }

    private static void Write(string line)
    {
        if (path == null) return;
        try
        {
            var fi = new FileInfo(path);
            if (fi.Exists && fi.Length > MaxBytes)
            {
                string old = Path.Combine(Application.persistentDataPath, "perf_log.1.txt");
                if (File.Exists(old)) File.Delete(old);
                File.Move(path, old);
            }
            File.AppendAllText(path, line + "\n");
        }
        catch (Exception) { }
    }

    private class Runner : MonoBehaviour
    {
        private void Update() => Frame();

        // 백그라운드에서 돌아온 첫 프레임(멈춰 있던 시간)을 멈칫으로 세지 않는다
        private void OnApplicationPause(bool paused)
        {
            if (paused) Write(Stamp() + " pause");
            Restart();
        }

        private void OnDestroy()
        {
            if (sysMem.Valid) sysMem.Dispose();
            if (gcMem.Valid) gcMem.Dispose();
        }
    }
}
