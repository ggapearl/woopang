#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

/// <summary>
/// 홈으로 나가도 이어지는 길안내 (11-26 대표님 10번 ② · 시안 우팡_노치_시안_1124 「바꾸는 안 2」) — R0926GuideArrow 가 안내를 시작 · 도착 · 끝낼 때 부른다.
///  · 아이폰: 아일랜드 · 잠금 화면 실시간 현황(Live Activity) — Plugins/iOS/WoopangLiveGuide.swift 가 직접 위치를 재어 거리 · 방향을 바꾼다 (유니티는 홈에서 멈춘다)
///  · 안드로이드: 위치 포그라운드 서비스 + 진행 중 알림 — native/android/WoopangGuide 에 준비만 해 두었다. Play Console 에 「포그라운드 서비스(위치)」 신고 · 영상이 먼저라
///    WOOPANG_GUIDE_SERVICE 정의를 켜기 전에는 아무것도 하지 않는다 (켜는 법 native/android/WoopangGuide/README.md)
///  · 배터리: 안내하는 동안만 — 도착(10m 안 1초) · 끝내기 · 20분이면 바로 끈다. 앱이 꺼졌다 켜지면 남은 것을 정리한다
/// </summary>
public static class R0926LiveGuide
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int _WoopangLiveGuideBegin(string name, double lat, double lon, double arriveM, double maxSec);
    [DllImport("__Internal")] private static extern void _WoopangLiveGuideEnd(int arrived);
#endif

#if UNITY_EDITOR
    /// <summary>플레이 시험이 본다 — 에디터는 네이티브를 부르지 않고 부른 것만 남긴다 (null 이면 남기지 않는다)</summary>
    public static System.Collections.Generic.List<string> EditorCalls;
#endif

    private static bool on;

    /// <summary>안내 시작 — 앞의 안내가 남아 있으면 그것부터 끝낸다</summary>
    public static void Begin(string name, float lat, float lon, float arriveMeters, float maxSeconds)
    {
        on = NativeBegin(name ?? "", lat, lon, arriveMeters, maxSeconds);
    }

    /// <summary>안내 끝 — arrived 면 「도착했어요」를 잠깐 보이고 닫는다</summary>
    public static void End(bool arrived)
    {
        if (!on) return;
        on = false;
        NativeEnd(arrived);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        on = false;
#if !UNITY_EDITOR
        NativeEnd(false);   // 앱이 꺼졌다 켜졌다 — 지난번 안내가 잠금 화면에 남아 있으면 지운다
#endif
    }

    private static bool NativeBegin(string name, float lat, float lon, float arriveMeters, float maxSeconds)
    {
#if UNITY_EDITOR
        EditorCalls?.Add($"begin {name} {arriveMeters:0} {maxSeconds:0}");
        return true;
#elif UNITY_IOS
        try { return _WoopangLiveGuideBegin(name, lat, lon, arriveMeters, maxSeconds) != 0; }
        catch (System.Exception e) { Debug.LogWarning("[LiveGuide] " + e.Message); return false; }
#elif UNITY_ANDROID && WOOPANG_GUIDE_SERVICE
        try
        {
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var service = new AndroidJavaClass("com.que.woopang.WoopangGuideService"))
                return service.CallStatic<bool>("begin", activity, name, (double)lat, (double)lon, (double)arriveMeters, (double)maxSeconds);
        }
        catch (System.Exception e) { Debug.LogWarning("[LiveGuide] " + e.Message); return false; }
#else
        return false;
#endif
    }

    private static void NativeEnd(bool arrived)
    {
#if UNITY_EDITOR
        EditorCalls?.Add(arrived ? "arrived" : "end");
#elif UNITY_IOS
        try { _WoopangLiveGuideEnd(arrived ? 1 : 0); }
        catch (System.Exception e) { Debug.LogWarning("[LiveGuide] " + e.Message); }
#elif UNITY_ANDROID && WOOPANG_GUIDE_SERVICE
        try
        {
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var service = new AndroidJavaClass("com.que.woopang.WoopangGuideService"))
                service.CallStatic("end", activity, arrived);
        }
        catch (System.Exception e) { Debug.LogWarning("[LiveGuide] " + e.Message); }
#endif
    }
}
