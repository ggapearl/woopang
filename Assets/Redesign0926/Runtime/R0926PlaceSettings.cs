using System;
using UnityEngine;

/// <summary>
/// 장소 박스 설정 (목록 시트 '설정' 탭 ▸ 장소). 기기에 저장된다.
/// </summary>
public static class R0926PlaceSettings
{
    /// <summary>오브젝트 삭제 기능 — 장소 박스 오른쪽 위 X 로 이 기기에서 숨기기. 기본 켜짐. 끄면 X 없이 꺾쇠 넷 그대로</summary>
    public static bool RemoveButton
    {
        get { try { return PlayerPrefs.GetInt("Place_RemoveX", 1) == 1; } catch (Exception) { return true; } }
        set { try { PlayerPrefs.SetInt("Place_RemoveX", value ? 1 : 0); PlayerPrefs.Save(); } catch (Exception) { } }
    }

    /// <summary>
    /// 멀리 있는 오브젝트 흐리게 — 0 끄기 · 1 약하게(기본) · 2 강하게.
    /// 큐브·3D 모델·안개는 R0926DistanceFade 가 0.2초마다, 장소 박스·화살표는 OffScreenIndicator 가 매 프레임 본다.
    /// 10-20 대표님 「오브젝트 설정에서 투명도 — 멀리 있는 거 흐리게 보이도록 하는 거 설정할 수 있도록」 ·
    /// 10-27 「'끄기 / 약하게 / 강하게' 로」 — 예전 네 칸(Place_FarFade 0 끄기 · 1 약하게 · 2 보통 · 3 강하게)은 읽을 때 옮긴다(보통은 약하게로)
    /// </summary>
    public static int FarFade
    {
        get
        {
            if (farFade < 0)
            {
                try
                {
                    if (PlayerPrefs.HasKey(FarFadeKey)) farFade = Mathf.Clamp(PlayerPrefs.GetInt(FarFadeKey, 1), 0, 2);
                    else
                    {
                        int old = PlayerPrefs.GetInt("Place_FarFade", 2);
                        farFade = old <= 0 ? 0 : old >= 3 ? 2 : 1;
                    }
                }
                catch (Exception) { farFade = 1; }
            }
            return farFade;
        }
        set
        {
            farFade = Mathf.Clamp(value, 0, 2);
            try { PlayerPrefs.SetInt(FarFadeKey, farFade); PlayerPrefs.Save(); } catch (Exception) { }
        }
    }
    private const string FarFadeKey = "Place_FarFade3";
    private static int farFade = -1;
}
