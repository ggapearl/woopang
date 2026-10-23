using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 탭 '앱 정보' — 문의 · 인스타그램 · 개인정보처리방침 · 관광 정보 출처. 버전과 저작권 그림은 설정 탭 맨 아래에 작게.
/// 예전엔 목록 맨 아래 '버전'을 펼쳐야 보였다(2026-10-06 대표님).
/// </summary>
public class R0926AppInfo : MonoBehaviour
{
    public const string ContactMail = "master@woopang.com";
    public const string Instagram = "woopang_ar";

    [SerializeField] private Text versionText;
    [SerializeField] private R0926PrivacyPage privacyPage;

    private void OnEnable()
    {
        if (versionText != null) versionText.text = "Version " + Application.version;
    }

    public void OpenMail() => Open("mailto:" + ContactMail);
    public void OpenInstagram() => Open("https://www.instagram.com/" + Instagram + "/");

    // 예전엔 홈페이지 첫 화면으로 나갔다 — 이제 앱 안에서 바로 (2026-10-23 대표님)
    public void OpenPrivacy()
    {
        Feedback();
        if (privacyPage != null) privacyPage.Open();
    }

    private static void Open(string url)
    {
        Feedback();
        Application.OpenURL(url);
    }

    private static void Feedback()
    {
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect();
    }
}
