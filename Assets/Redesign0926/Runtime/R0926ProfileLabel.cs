using UnityEngine;
using UnityEngine.UI;

/// <summary>도크 오른쪽 칸 글자: 로그인했으면 '프로필', 아니면 '로그인'.</summary>
[RequireComponent(typeof(Text))]
public class R0926ProfileLabel : MonoBehaviour
{
    private Text text;
    private int state = -1;
    private float next;

    private void Awake()
    {
        text = GetComponent<Text>();
    }

    private void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.5f;
        int s = LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn ? 1 : 0;
        if (s == state) return;
        state = s;
        string lang = R0926LocalizedText.Lang();
        text.text = s == 1
            ? lang switch { "ko" => "프로필", "ja" => "プロフィール", "zh" => "我的", "es" => "Perfil", _ => "Profile" }
            : lang switch { "ko" => "로그인", "ja" => "ログイン", "zh" => "登录", "es" => "Entrar", _ => "Sign in" };
    }
}
