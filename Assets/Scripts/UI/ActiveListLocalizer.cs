using UnityEngine;
using UnityEngine.UI;

public class ActiveListLocalizer : MonoBehaviour
{
    [SerializeField] private Text activeListLabel; // Active List 텍스트 UI
    [SerializeField] private Text messagesLabel;   // Messages 텍스트 UI

    private void Start()
    {
        LocalizeTexts();
    }

    private void LocalizeTexts()
    {
        if (activeListLabel != null)
        {
            activeListLabel.text = GetTranslatedActiveList();
        }
        else
        {
            Debug.LogWarning("ActiveListLabel is not assigned.");
        }

        if (messagesLabel != null)
        {
            messagesLabel.text = GetTranslatedMessages();
        }
        else
        {
            Debug.LogWarning("MessagesLabel is not assigned.");
        }
    }

    private string GetTranslatedActiveList()
    {
        SystemLanguage language = Application.systemLanguage;

        switch (language)
        {
            case SystemLanguage.Korean:
                return "주변 리스트";
            case SystemLanguage.Japanese:
                return "アクティブリスト";
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return "活动列表";
            case SystemLanguage.Spanish:
                return "LISTA ACTIVA";
            default:
                return "ACTIVE LIST";
        }
    }

    private string GetTranslatedMessages()
    {
        SystemLanguage language = Application.systemLanguage;

        switch (language)
        {
            case SystemLanguage.Korean:
                return "메세지";
            case SystemLanguage.Japanese:
                return "メッセージ";
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return "消息";
            case SystemLanguage.Spanish:
                return "MENSAJES";
            default:
                return "MESSAGES";
        }
    }
}