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
}
