using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UITouchForwarder : MonoBehaviour, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private AudioClip customSound;
    [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;   // 이 버튼만 소리 크기 (메세지 보내기 0.8)

    private bool blocked;

    void Start()
    {
        if (!GetComponent<Selectable>())
        {
            Debug.LogWarning($"UITouchForwarder on {gameObject.name} requires a Button component!");
        }
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