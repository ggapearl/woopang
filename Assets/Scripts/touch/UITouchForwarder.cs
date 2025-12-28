using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UITouchForwarder : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private AudioClip customSound;

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
            UIFeedbackManager.Instance.HandleTouchFeedbackDirect(customSound);
        }
    }
}