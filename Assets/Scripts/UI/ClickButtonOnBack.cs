using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ClickButtonOnBack : MonoBehaviour
{
    private Button myButton;

    void Awake()
    {
        myButton = GetComponent<Button>();
    }

    void OnEnable()
    {
        if (BackButtonHandler.Instance != null)
        {
            BackButtonHandler.Instance.RegisterButton(myButton);
        }
    }

    void OnDisable()
    {
        if (BackButtonHandler.Instance != null)
        {
            BackButtonHandler.Instance.UnregisterButton(myButton);
        }
    }
}
