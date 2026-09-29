using UnityEngine;

/// <summary>
/// 앱 시작 화면(오프닝)이 떠 있는 동안 true.
/// 시작 화면 아래에서 떴다가 보이기도 전에 사라지는 안내(주변 오브젝트 개수 등)가 끝날 때까지 기다리게 한다.
/// </summary>
public static class BootOverlay
{
    public static bool Showing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch() => Showing = false;
}
