using UnityEngine;
using UnityEngine.UI; // UI ��Ҹ� ����ϱ� ���� �ʿ�

public class OpenPrivacyPolicy : MonoBehaviour
{
    // ��������ó����ħ URL
    private string privacyPolicyUrl => ApiConfig.MAIN_SERVER;

    void Start()
    {
        // ��ư ������Ʈ�� ������
        Button btn = GetComponent<Button>();
        // ��ư Ŭ�� �� OpenPrivacyPolicyLink �޼��� ȣ��
        btn.onClick.AddListener(OpenPrivacyPolicyLink);
    }

    void OpenPrivacyPolicyLink()
    {
        // URL�� �ܺ� ���������� ����
        Application.OpenURL(privacyPolicyUrl);
    }
}