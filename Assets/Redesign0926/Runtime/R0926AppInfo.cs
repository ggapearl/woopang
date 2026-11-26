using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 탭 '앱 정보' — 사용가이드 · 문의 · 인스타그램 · 개인정보처리방침 · 관광 정보 출처 · 오픈소스 라이선스. 버전과 저작권 그림은 설정 탭 맨 아래에 작게.
/// 예전엔 목록 맨 아래 '버전'을 펼쳐야 보였다(2026-10-06 대표님).
/// </summary>
public class R0926AppInfo : MonoBehaviour
{
    public const string ContactMail = "master@woopang.com";
    public const string Instagram = "woopang_ar";

    [SerializeField] private Text versionText;
    [SerializeField] private R0926PrivacyPage privacyPage;
    [SerializeField] private R0926PrivacyPage licensePage;

    private void OnEnable()
    {
        if (versionText != null) versionText.text = "Version " + Application.version;
    }

    // 사용가이드 다시 보기 — 설정이 든 목록 시트를 내린 뒤 첫 실행 때와 같은 카드 안내
    // (11-26 대표님 26번 「사용가이드를 설정탭 '앱정보' 맨 위에 (문의 위쪽) 버튼을 하나 만들어서 누르면 사용가이드 나오도록」)
    public void OpenGuide()
    {
        Feedback();
        var guide = FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
        if (guide == null) return;
        var sheet = GetComponentInParent<R0926Closer>(true);
        if (sheet != null && sheet.isActiveAndEnabled) sheet.AnimateClose(null);   // X 와 같은 길 — 내려간 뒤 닫기 버튼
        guide.Replay(sheet);
    }

    public void OpenMail() => Open("mailto:" + ContactMail);
    public void OpenInstagram() => Open("https://www.instagram.com/" + Instagram + "/");

    // 예전엔 홈페이지 첫 화면으로 나갔다 — 이제 앱 안에서 바로 (2026-10-23 대표님)
    public void OpenPrivacy()
    {
        Feedback();
        if (privacyPage != null) privacyPage.Open();
    }

    // 이모지 그림(Fluent Emoji · MIT) 고지 — 개인정보처리방침과 같은 모양의 한 장 (11-02)
    public void OpenLicenses()
    {
        Feedback();
        if (licensePage != null) licensePage.Open();
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
