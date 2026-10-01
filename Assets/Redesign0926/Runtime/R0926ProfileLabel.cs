using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도크 오른쪽 칸 글자: 로그인했으면 그 사람의 닉네임(아이디), 아니면 '로그인'.
/// 이름은 ProfileManager 가 채우는 (숨겨 둔) 미니 프로필 이름을 먼저 쓰고, 비어 있으면 로그인 정보에서.
/// </summary>
[RequireComponent(typeof(Text))]
public class R0926ProfileLabel : MonoBehaviour
{
    private const int MaxChars = 7;   // 도크 한 칸에 들어가는 길이 — 넘으면 말줄임

    private Text text;
    private string shown;
    private float next;

    private void Awake()
    {
        text = GetComponent<Text>();
    }

    private void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.5f;

        bool loggedIn = LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn;
        string lang = R0926LocalizedText.Lang();
        string label;
        if (!loggedIn)
            label = lang switch { "ko" => "로그인", "ja" => "ログイン", "zh" => "登录", "es" => "Entrar", _ => "Sign in" };
        else
        {
            string name = CurrentName();
            label = string.IsNullOrEmpty(name)
                ? lang switch { "ko" => "프로필", "ja" => "プロフィール", "zh" => "我的", "es" => "Perfil", _ => "Profile" }
                : (name.Length > MaxChars ? name.Substring(0, MaxChars - 1) + "…" : name);
        }
        if (label == shown) return;
        shown = label;
        text.text = label;
    }

    private static string CurrentName()
    {
        var pm = ProfileManager.Instance;
        string n = pm != null && pm.miniUsernameText != null ? pm.miniUsernameText.text : null;
        if (!string.IsNullOrWhiteSpace(n) && n != "Login") return n.Trim();
        var user = LoginManager.Instance != null ? LoginManager.Instance.CurrentUser : null;
        return user != null ? user.username : null;
    }
}
