using UnityEngine;
using UnityEngine.UI; // Button 컴포넌트를 사용하기 위해 필요

public class HapticButton : MonoBehaviour
{
    [SerializeField] private Button hapticButton; // 햅틱 반응을 일으킬 버튼

    private void Start()
    {
        // 버튼 클릭 이벤트에 햅틱 반응 메서드 연결
        if (hapticButton != null)
        {
            hapticButton.onClick.AddListener(TriggerHapticFeedback);
        }
        else
        {
            Debug.LogError("HapticButton이 설정되지 않았습니다!");
        }
    }

    private void TriggerHapticFeedback()
    {
        // 모바일 디바이스에서 진동 발생 (안드로이드/iOS 지원)
        if (Application.isMobilePlatform)
        {
            Handheld.Vibrate(); // 기본 진동 패턴 (약 0.5초 동안 진동)
            Debug.Log("버튼 클릭: 햅틱 피드백 발생!");
        }
        else
        {
            Debug.LogWarning("이 기능은 모바일 디바이스에서만 작동합니다.");
        }
    }
}