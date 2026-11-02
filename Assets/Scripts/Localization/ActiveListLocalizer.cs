using UnityEngine;
using UnityEngine.UI;

public class ActiveListLocalizer : MonoBehaviour
{
    public Text activeListLabel;
    public Text messagesLabel;

    private void Start()
    {
        if (LocalizationManager.Instance != null)
        {
            if (activeListLabel != null)
                activeListLabel.text = LocalizationManager.Instance.GetText("active_list_label");
            
            if (messagesLabel != null)
                messagesLabel.text = LocalizationManager.Instance.GetText("messages_label");
        }
        else
        {
            Debug.LogWarning("[ActiveListLocalizer] LocalizationManager Instance not found.");
        }
    }
}