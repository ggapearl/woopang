using UnityEngine;
using UnityEngine.UI; // UI 요소를 사용하기 위해 필요

public class OpenPrivacyPolicy : MonoBehaviour
{
    // 개인정보처리방침 URL
    private string privacyPolicyUrl = "https://woopang.com"; // 여기에 실제 URL 입력

    void Start()
    {
        // 버튼 컴포넌트를 가져옴
        Button btn = GetComponent<Button>();
        // 버튼 클릭 시 OpenPrivacyPolicyLink 메서드 호출
        btn.onClick.AddListener(OpenPrivacyPolicyLink);
    }

    void OpenPrivacyPolicyLink()
    {
        // URL을 외부 브라우저에서 열기
        Application.OpenURL(privacyPolicyUrl);
    }
}