using System;
using SimpleJSON;

/// <summary>
/// 보물찾기 설정 (서버 /api/hunts/prefs — 계정마다 서버에 있다). 설정 탭과 보물찾기 카드가 마지막으로 받은 값을 같이 본다.
///  · Enabled  — 보물찾기 참여. 끄면 근처 목록·새 게임 알림에 더해 참가해 둔 게임 알림도 끊긴다 (서버가 막는다)
///  · NotifyNew — 근처에 새 게임이 열리면 푸시 (서버)
///  · NotifyNear — 참가한 게임의 보물에 가까워질 때 진동 (앱 — R0926HuntCard)
/// 서버 기본값은 모두 켜짐 — 받기 전에도 켜짐으로 둔다.
/// </summary>
public static class R0926HuntPrefs
{
    public static bool Enabled = true;
    public static bool NotifyNew = true;
    public static bool NotifyNear = true;
    /// <summary>서버에서 한 번이라도 받았는지 (설정 탭이 '불러오는 중'을 가린다)</summary>
    public static bool Known;
    public static event Action Changed;

    /// <summary>서버 응답의 prefs 를 받아 둔다 (/prefs · /nearby 둘 다 같은 모양)</summary>
    public static void Take(JSONNode p)
    {
        if (p == null || !p.HasKey("enabled")) return;
        Enabled = p["enabled"].AsBool;
        NotifyNew = p["notify_new"].AsBool;
        NotifyNear = p["notify_near"].AsBool;
        Known = true;
        Changed?.Invoke();
    }

    public static bool Get(string key) => key == "enabled" ? Enabled : key == "notify_new" ? NotifyNew : NotifyNear;

    public static void Set(string key, bool on)
    {
        if (key == "enabled") Enabled = on;
        else if (key == "notify_new") NotifyNew = on;
        else NotifyNear = on;
        Changed?.Invoke();
    }

    // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 '앱을 새로 켠 것'과 같게
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        Enabled = NotifyNew = NotifyNear = true;
        Known = false;
        Changed = null;
    }
}
