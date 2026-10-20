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
    /// 멀리 있는 오브젝트 흐리게 — 0 끄기 · 1 약하게 · 2 보통(기본, 지금까지의 모습) · 3 강하게 (R0926DistanceFade 가 0.2초마다 본다).
    /// 10-20 대표님 「오브젝트 설정에서 투명도 — 멀리 있는 거 흐리게 보이도록 하는 거 설정할 수 있도록」
    /// </summary>
    public static int FarFade
    {
        get
        {
            if (farFade < 0)
            {
                try { farFade = Mathf.Clamp(PlayerPrefs.GetInt("Place_FarFade", 2), 0, 3); }
                catch (Exception) { farFade = 2; }
            }
            return farFade;
        }
        set
        {
            farFade = Mathf.Clamp(value, 0, 3);
            try { PlayerPrefs.SetInt("Place_FarFade", farFade); PlayerPrefs.Save(); } catch (Exception) { }
        }
    }
    private static int farFade = -1;
}
