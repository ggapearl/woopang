using System;
using UnityEngine;

/// <summary>
/// 설정 › 화면 › 디스플레이 선택 (11-23 대표님 S8) — 물방울 모드 · 네온 모드는 따로 켜고 끈다 (둘 다 고를 수 있다 · 둘 다 끄면 예전 기본 모양).
/// 물방울 모드 = 도크 · 알약이 물방울 유리 · 창 뒤로 AR 화면이 흐리게 비침 · 창 안 버튼은 바탕만 투명 (「버튼 바탕만 투명」 — 글자는 그대로)
/// 네온 모드 = 창 · 도크 · 알림 테두리에 흰 선 하나 + 푸른 빛 (목록 창은 고른 칩 색)
/// 창 배경 진하기 = 약하게 / 중간 / 진하게 = 70 / 80 / 90 % (기본 70) — 창 바탕만 · 글자 · 사진 · 지도는 그대로
/// 값은 이 기기에만 (PlayerPrefs). 창마다 매 프레임 읽으므로 한 번 읽어 두고, 바꿀 때만 저장한다.
/// </summary>
public static class R0926MenuStyle
{
    private const string Key = "r0926_menu_style";               // 물방울 — 예전 「물방울메뉴」 값 그대로 (고른 적 없으면 켬 · 11-15 대표님)
    private const string NeonKey = "r0926_neon";                 // 네온 (고른 적 없으면 켬 · 11-23)
    private const string DensityKey = "r0926_window_density";    // 창 배경 진하기 % (고른 적 없으면 70)
    public static readonly int[] Densities = { 70, 80, 90 };    // 약하게 · 중간 · 진하게
    public static event Action Changed;

    private static int droplet = -1, neon = -1, density = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => droplet = neon = density = -1;

    public static bool Droplet
    {
        get { if (droplet < 0) droplet = PlayerPrefs.GetInt(Key, 1) == 1 ? 1 : 0; return droplet == 1; }
        set { if (Droplet == value) return; droplet = value ? 1 : 0; PlayerPrefs.SetInt(Key, droplet); PlayerPrefs.Save(); Changed?.Invoke(); }
    }

    public static bool Neon
    {
        get { if (neon < 0) neon = PlayerPrefs.GetInt(NeonKey, 1) == 1 ? 1 : 0; return neon == 1; }
        set { if (Neon == value) return; neon = value ? 1 : 0; PlayerPrefs.SetInt(NeonKey, neon); PlayerPrefs.Save(); Changed?.Invoke(); }
    }

    /// <summary>창 배경 진하기 % (70 · 80 · 90) — 저장된 값이 셋 중 하나가 아니면 70</summary>
    public static int Density
    {
        get
        {
            if (density < 0)
            {
                int v = PlayerPrefs.GetInt(DensityKey, Densities[0]);
                density = Array.IndexOf(Densities, v) >= 0 ? v : Densities[0];
            }
            return density;
        }
        set
        {
            int v = Array.IndexOf(Densities, value) >= 0 ? value : Densities[0];
            if (Density == v) return;
            density = v;
            PlayerPrefs.SetInt(DensityKey, v);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>둘 다 끔 — 예전 기본 모양 (불투명 회색 창 · 회색 도크)</summary>
    public static bool Classic => !Droplet && !Neon;

    /// <summary>PlayerPrefs 를 직접 바꾼 뒤 (시험) — 다시 읽고 화면에 알린다</summary>
    public static void Reload()
    {
        droplet = neon = density = -1;
        Changed?.Invoke();
    }
}
