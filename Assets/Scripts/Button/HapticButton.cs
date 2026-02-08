using UnityEngine;
using UnityEngine.UI; // Button ������Ʈ�� ����ϱ� ���� �ʿ�

public class HapticButton : MonoBehaviour
{
    [SerializeField] private Button hapticButton; // ��ƽ ������ ����ų ��ư

    private void Start()
    {
        // ��ư Ŭ�� �̺�Ʈ�� ��ƽ ���� �޼��� ����
        if (hapticButton != null)
        {
            hapticButton.onClick.AddListener(TriggerHapticFeedback);
        }
        else
        {
            Debug.LogError("HapticButton�� �������� �ʾҽ��ϴ�!");
        }
    }

    private void OnDestroy()
    {
        if (hapticButton != null)
        {
            hapticButton.onClick.RemoveListener(TriggerHapticFeedback);
        }
    }

    private void TriggerHapticFeedback()
    {
        // ����� ����̽����� ���� �߻� (�ȵ���̵�/iOS ����)
        if (Application.isMobilePlatform)
        {
            Handheld.Vibrate(); // �⺻ ���� ���� (�� 0.5�� ���� ����)
            Debug.Log("��ư Ŭ��: ��ƽ �ǵ�� �߻�!");
        }
        else
        {
            Debug.LogWarning("�� ����� ����� ����̽������� �۵��մϴ�.");
        }
    }
}