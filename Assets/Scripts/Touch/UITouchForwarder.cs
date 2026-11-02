using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UITouchForwarder : MonoBehaviour, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private AudioClip customSound;
    [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;   // 이 버튼만 소리 크기 (메세지 보내기 0.8)

    private bool blocked;

    // 버튼이 아니어도 누를 수 있는 것(말풍선 하트 · 아바타 · 슬라이더 칸)에 일부러 붙인다 — 누를 게 하나도 없을 때만 알린다
    void Start()
    {
        if (!CanBePressed())
        {
            Debug.LogWarning($"UITouchForwarder on {gameObject.name} has nothing to press (no Selectable or click handler)!");
        }
    }

    bool CanBePressed()
    {
        if (GetComponentInChildren<Selectable>(true) != null) return true;
        foreach (var h in GetComponents<IPointerClickHandler>())
            if (!ReferenceEquals(h, this)) return true;
        return false;
    }

    // 막힌 버튼은 소리 없이 — 클릭 때 보면 누르자마자 스스로 잠그는 버튼(보내기 등)까지 막힌 것으로 보여 손을 뗄 때 본다
    public void OnPointerUp(PointerEventData eventData)
    {
        var selectable = GetComponent<Selectable>();
        blocked = selectable != null && !selectable.IsInteractable();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var feedback = UIFeedbackManager.Instance;
        if (feedback == null || blocked) return;
        if (feedback.FedBackThisFrame) return;   // 버튼이 스스로 소리를 냈다 (보물찾기 버튼)
        feedback.HandleTouchFeedbackDirect(customSound, volumeScale);
    }
}