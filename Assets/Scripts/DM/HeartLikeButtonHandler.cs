using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 하트 아이콘 버튼 핸들러
/// 한번 터치하면 좋아요 취소 (unlike)
/// 여러번 연속 터치해도 토글되지 않음 (쿨다운 적용)
/// </summary>
public class HeartLikeButtonHandler : MonoBehaviour, IPointerClickHandler
{
    private int messageId;
    private bool isLiked;
    private Action<int, bool> onLikeToggle;

    // 연속 터치 방지를 위한 쿨다운
    private float lastClickTime = 0f;
    private float clickCooldown = 1.0f; // 1초 쿨다운

    // 현재 요청 진행 중인지
    private bool isProcessing = false;

    /// <summary>
    /// 초기화
    /// </summary>
    /// <param name="messageId">메시지 ID</param>
    /// <param name="isLiked">현재 좋아요 상태</param>
    /// <param name="callback">좋아요 토글 콜백 (messageId, newLikedState)</param>
    public void Initialize(int messageId, bool isLiked, Action<int, bool> callback)
    {
        this.messageId = messageId;
        this.isLiked = isLiked;
        this.onLikeToggle = callback;
    }

    /// <summary>
    /// 좋아요 상태 업데이트
    /// </summary>
    public void UpdateLikedState(bool liked)
    {
        this.isLiked = liked;
        isProcessing = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        float currentTime = Time.time;

        // 쿨다운 체크 - 연속 터치 방지
        if (currentTime - lastClickTime < clickCooldown)
            return;

        // 이미 요청 진행 중이면 무시
        if (isProcessing)
            return;

        lastClickTime = currentTime;
        isProcessing = true;

        // 좋아요 토글
        onLikeToggle?.Invoke(messageId, !isLiked);
    }

    /// <summary>
    /// 요청 완료 후 호출 (성공/실패 상관없이)
    /// </summary>
    public void OnRequestComplete()
    {
        isProcessing = false;
    }
}
