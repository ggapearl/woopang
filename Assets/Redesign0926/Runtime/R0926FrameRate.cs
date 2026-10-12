using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// 화면 프레임 30 / 60 — 설정 탭 '화면'에서 고른다. 기본 30 (배터리·발열). AR 추적은 카메라 속도 그대로.
/// ARSession 의 Match Frame Rate 가 켜져 있으면 매 프레임 카메라 속도(아이폰 60)로 덮어쓰므로 먼저 끈다.
/// </summary>
public static class R0926FrameRate
{
    private const string Key = "r0926_fps";

    public static int Fps
    {
        get => PlayerPrefs.GetInt(Key, 30) >= 60 ? 60 : 30;
        set
        {
            PlayerPrefs.SetInt(Key, value >= 60 ? 60 : 30);
            PlayerPrefs.Save();
            Apply();
        }
    }

    public static void Apply()
    {
        var session = Object.FindFirstObjectByType<ARSession>(FindObjectsInactive.Include);
        if (session != null) session.matchFrameRateRequested = false;
        Application.targetFrameRate = Fps;
    }
}
