using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 입력줄의 이모지 단추 (N-U · 10-26 대표님 「'전송버튼' 좌측에 적절히 '이모지' 버튼」) — 대화·댓글 입력칸 오른쪽 끝,
/// 장소 이름·설명 칸의 키보드 위 입력줄(UploadInputMirror · 11-05 대표님 「'장소추가' 하는 탭의 텍스트입력필드에서도 '이모지'」).
/// 누르면 키보드 대신 이모지 창(R0926EmojiPanel)이 뜨고, 떠 있는 동안은 ⌨ 로 바뀌어 누르면 키보드로 돌아간다.
/// </summary>
[RequireComponent(typeof(Button))]
public class R0926EmojiButton : MonoBehaviour
{
    [SerializeField] private InputField input;
    [SerializeField] private MobileKeyboardHandler keyboard;
    [SerializeField] private UploadInputMirror mirror;   // 키보드 위 입력줄이면 이쪽 (keyboard 는 비워 둔다)
    [SerializeField] private R0926EmojiPanel panel;
    [SerializeField] private Image icon;
    [SerializeField] private Sprite smile;
    [SerializeField] private Sprite keys;

    private void Awake() => GetComponent<Button>().onClick.AddListener(OnTap);

    private IR0926KeyboardHost Host => keyboard != null ? keyboard : mirror != null ? (IR0926KeyboardHost)mirror : null;

    private void OnTap()
    {
        var host = Host;
        if (panel == null || input == null || host == null || !input.interactable) return;
        if (R0926EmojiPanel.IsOpenFor(input)) panel.SwitchToKeyboard();
        else
        {
            if (mirror != null) mirror.BeforeEmoji();
            panel.Open(input, host);
        }
    }

    private void LateUpdate()
    {
        if (icon == null || input == null) return;
        var want = R0926EmojiPanel.IsOpenFor(input) ? keys : smile;
        if (icon.sprite != want) icon.sprite = want;
        float a = input.interactable ? 1f : 0.35f;   // 공지 방처럼 칸이 꺼져 있으면 흐리게
        if (!Mathf.Approximately(icon.color.a, a)) icon.color = new Color(icon.color.r, icon.color.g, icon.color.b, a);
    }
}
