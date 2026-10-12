using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 탭 '앱 정보' — 문의 · 인스타그램 · 개인정보처리방침 · 관광 정보 출처. 버전은 설정 탭 맨 아래에 작게.
/// 예전엔 목록 맨 아래 '버전'을 펼쳐야 보였다(2026-10-06 대표님).
/// </summary>
public class R0926AppInfo : MonoBehaviour
{
    public const string ContactMail = "pdnom@naver.com";
    public const string Instagram = "woopang_ar";

    [SerializeField] private Text versionText;

    private void OnEnable()
    {
        if (versionText != null) versionText.text = "Version " + Application.version;
    }

    public void OpenMail() => Open("mailto:" + ContactMail);
    public void OpenInstagram() => Open("https://www.instagram.com/" + Instagram + "/");
    public void OpenPrivacy() => Open(ApiConfig.MAIN_SERVER);   // 예전 '버전' 펼침의 [CLICK HERE] 와 같은 곳

    private static void Open(string url)
    {
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect();
        Application.OpenURL(url);
    }
}
