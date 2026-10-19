using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UITouchForwarder : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private AudioClip customSound;
    [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;   // 이 버튼만 소리 크기 (메세지 보내기 0.8)

    void Start()
    {
        if (!GetComponent<Button>())
        {
            Debug.LogWarning($"UITouchForwarder on {gameObject.name} requires a Button component!");
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (UIFeedbackManager.Instance != null)
        {
            UIFeedbackManager.Instance.HandleTouchFeedbackDirect(customSound, volumeScale);
        }
    }
}