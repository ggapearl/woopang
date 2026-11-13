using System;
using UnityEngine;

/// <summary>
/// 메뉴 모양 — 기본메뉴 / 물방울메뉴 (설정 › 화면, 30·60 FPS 바로 위 · 11-08 대표님).
/// 물방울메뉴는 도크 · 위치 칩 · 상태 알약처럼 AR 화면 위에 뜨는 반투명 바를, 뒤 화면이 흐리게 비치고 가장자리가 굴절·반짝이는 유리로 그린다 (R0926Glass).
/// 애플은 Liquid Glass 의 셰이더 값을 공개하지 않는다 — 공개된 원칙(캡슐 모양 · 가장자리 렌즈 · 반사광 · 뒤 화면 색을 따르는 옅은 색)대로 비슷하게 만든 것.
/// 기본은 기본메뉴.
/// </summary>
public static class R0926MenuStyle
{
    private const string Key = "r0926_menu_style";

    public static event Action Changed;

    public static bool Droplet
    {
        get => PlayerPrefs.GetInt(Key, 0) == 1;
        set
        {
            if (Droplet == value) return;
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
