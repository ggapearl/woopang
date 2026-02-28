using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

/// <summary>
/// 아바타 탭 핸들러 - 클릭 시 프로필 패널 열기
/// 메시지 버블의 아바타에 추가
/// </summary>
public class AvatarTapHandler : MonoBehaviour, IPointerClickHandler
{
    private string userId;
    private string username;
    private Action<string, string> onAvatarTapped;

    /// <summary>
    /// 아바타 탭 핸들러 초기화
    /// </summary>
    /// <param name="userId">사용자 ID</param>
    /// <param name="username">사용자명</param>
    /// <param name="callback">아바타 탭 시 콜백 (userId, username)</param>
    public void Initialize(string userId, string username, Action<string, string> callback)
    {
        this.userId = userId;
        this.username = username;
        this.onAvatarTapped = callback;

        // 레이캐스트 타겟 확인
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = true;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(userId)) return;

        onAvatarTapped?.Invoke(userId, username);
    }
}
