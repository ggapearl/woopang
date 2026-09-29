using System;
using UnityEngine;

/// <summary>
/// 하늘 화면 설정 (목록 시트 '설정' 탭). 기기에 저장된다.
/// 하늘에는 앞으로 날씨 말고도 동네 이벤트·게임, 제휴사 라이브, 광고가 번갈아 뜬다 — 사람마다 켜고 끌 수 있게.
/// </summary>
public static class R0926SkySettings
{
    public static event Action Changed;

    /// <summary>보이는 각도 단계 → 휴대폰을 얼마나 들어야 하늘 화면이 뜨는지 (도)</summary>
    public static readonly float[] AngleSteps = { 18f, 28f, 45f };

    public static bool Enabled { get => Get("Sky_Enabled", true); set => Set("Sky_Enabled", value); }
    public static bool Weather { get => Get("Sky_Weather", true); set => Set("Sky_Weather", value); }
    public static bool Events { get => Get("Sky_Events", true); set => Set("Sky_Events", value); }
    public static bool Live { get => Get("Sky_Live", true); set => Set("Sky_Live", value); }
    public static bool Ads { get => Get("Sky_Ads", true); set => Set("Sky_Ads", value); }
    /// <summary>누워서 휴대폰을 위로 들고 쓸 때(천장) 하늘 화면을 숨긴다</summary>
    public static bool HideWhenLying { get => Get("Sky_HideLying", true); set => Set("Sky_HideLying", value); }
    /// <summary>앱을 켤 때 하늘 화면을 접힌 채(작은 칩)로 시작</summary>
    public static bool StartCollapsed { get => Get("Sky_StartCollapsed", false); set => Set("Sky_StartCollapsed", value); }

    public static int Angle
    {
        get => Mathf.Clamp(Safe(() => PlayerPrefs.GetInt("Sky_Angle", 1), 1), 0, AngleSteps.Length - 1);
        set { Safe(() => { PlayerPrefs.SetInt("Sky_Angle", Mathf.Clamp(value, 0, AngleSteps.Length - 1)); PlayerPrefs.Save(); return 0; }, 0); Changed?.Invoke(); }
    }

    public static float MinPitch => AngleSteps[Angle];

    private static bool Get(string key, bool def) => Safe(() => PlayerPrefs.GetInt(key, def ? 1 : 0) == 1, def);

    private static void Set(string key, bool v)
    {
        Safe(() => { PlayerPrefs.SetInt(key, v ? 1 : 0); PlayerPrefs.Save(); return 0; }, 0);
        Changed?.Invoke();
    }

    private static T Safe<T>(Func<T> f, T fallback)
    {
        try { return f(); } catch (Exception) { return fallback; }
    }
}
