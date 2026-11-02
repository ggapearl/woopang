using UnityEngine;
using System;

/// <summary>
/// [DEPRECATED] DirectMessageManager - MessagePanelManager로 통합됨
///
/// 이 클래스는 하위 호환성을 위해 유지되며, 모든 호출을 MessagePanelManager로 전달합니다.
/// 새로운 코드에서는 MessagePanelManager.Instance를 직접 사용하세요.
/// </summary>
[Obsolete("DirectMessageManager는 MessagePanelManager로 통합되었습니다. MessagePanelManager.Instance를 사용하세요.")]
public class DirectMessageManager : MonoBehaviour
{
    public static DirectMessageManager Instance { get; private set; }

    [Header("=== DEPRECATED - MessagePanelManager 사용 권장 ===")]
    [Tooltip("이 컴포넌트는 더 이상 사용되지 않습니다")]
    public bool showDeprecationWarning = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }

    void Start()
    {
        // MessagePanelManager가 없으면 경고
        if (MessagePanelManager.Instance == null)
        {
            Debug.LogError("[DirectMessageManager] MessagePanelManager.Instance가 없습니다. 씬에 MessagePanelManager를 추가하세요.");
        }
    }

    #region Forwarding Methods (호환성 유지용)

    /// <summary>
    /// [DEPRECATED] OpenDMPanel -> MessagePanelManager.OpenMessagePanel
    /// </summary>
    [Obsolete("OpenDMPanel은 더 이상 사용되지 않습니다. MessagePanelManager.Instance.OpenMessagePanel()을 사용하세요.")]
    public void OpenDMPanel()
    {
        if (MessagePanelManager.Instance != null)
        {
            MessagePanelManager.Instance.OpenMessagePanel();
        }
        else
        {
            Debug.LogError("[DirectMessageManager] MessagePanelManager.Instance가 없습니다.");
        }
    }

    /// <summary>
    /// [DEPRECATED] 이 기능은 제거되었습니다. 검색으로 팔로잉 유저와 대화를 시작하세요.
    /// </summary>
    [Obsolete("OpenFollowingSelect 기능이 제거되었습니다. MessagePanelManager의 검색 기능을 사용하세요.")]
    public void OpenFollowingSelect()
    {
    }

    /// <summary>
    /// [DEPRECATED] OpenConversation -> MessagePanelManager.OpenChatRoom
    /// </summary>
    [Obsolete("OpenConversation은 더 이상 사용되지 않습니다. MessagePanelManager.Instance.OpenChatRoom()을 사용하세요.")]
    public void OpenConversation(string userId, string username, string avatarUrl = null)
    {
        if (MessagePanelManager.Instance != null)
        {
            MessagePanelManager.Instance.OpenChatRoom(userId, username, avatarUrl);
        }
        else
        {
            Debug.LogError("[DirectMessageManager] MessagePanelManager.Instance가 없습니다.");
        }
    }

    /// <summary>
    /// [DEPRECATED] CloseDMPanel -> MessagePanelManager.CloseMessagePanel
    /// </summary>
    [Obsolete("CloseDMPanel은 더 이상 사용되지 않습니다. MessagePanelManager.Instance.CloseMessagePanel()을 사용하세요.")]
    public void CloseDMPanel()
    {
        if (MessagePanelManager.Instance != null)
        {
            MessagePanelManager.Instance.CloseMessagePanel();
        }
    }

    /// <summary>
    /// [DEPRECATED] RefreshUnreadCount -> MessagePanelManager.RefreshUnreadCount
    /// </summary>
    [Obsolete("RefreshUnreadCount는 더 이상 사용되지 않습니다. MessagePanelManager.Instance.RefreshUnreadCount()를 사용하세요.")]
    public void RefreshUnreadCount()
    {
        if (MessagePanelManager.Instance != null)
        {
            MessagePanelManager.Instance.RefreshUnreadCount();
        }
    }

    /// <summary>
    /// [DEPRECATED] SendMessage - MessagePanelManager에서는 직접 UI를 통해 전송
    /// </summary>
    [Obsolete("SendMessage는 더 이상 사용되지 않습니다. MessagePanelManager의 채팅방 UI를 통해 메시지를 전송하세요.")]
    public void SendMessage(string recipientId, string content, Action<bool> callback = null)
    {
        callback?.Invoke(false);
    }

    #endregion
}
