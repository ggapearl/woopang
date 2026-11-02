using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 입력칸에 들어온 진짜 이모지(기기 키보드의 이모지 · 붙여 넣기)를 우팡 이모지 글자로 (N-U) — 칸 안에서도 우팡 이모지로 보인다.
/// 칸이 키보드 글을 읽은 뒤(InputField.LateUpdate)에 바꿔야 키보드 쪽 글도 같이 바뀌어 다음 글자에 되돌아오지 않는다 — 그래서 늦게 돈다.
/// 모르는 이모지는 그대로 둔다 (보낼 때 되돌릴 수 없으니 지우지 않는다).
/// </summary>
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(InputField))]
public class R0926EmojiInput : MonoBehaviour
{
    private const float CaretWait = 1f;   // 키보드가 다 올라올 때까지 커서 자리를 들고 기다리는 시간

    private InputField input;
    private string seen;
    private int caretOnFocus = -1;
    private float caretUntil;

    private void Awake() => input = GetComponent<InputField>();

    private void LateUpdate()
    {
        string s = input.text;
        if (!ReferenceEquals(s, seen))
        {
            seen = s;
            string d = R0926Emoji.ToInput(s);
            if (!ReferenceEquals(d, s))
            {
                int caret = input.isFocused ? Mathf.Clamp(input.caretPosition, 0, s.Length) : s.Length;
                int at = R0926Emoji.ToInput(s.Substring(0, caret)).Length;
                input.SetTextWithoutNotify(d);
                seen = input.text;
                SetCaret(at);
                input.onValueChanged.Invoke(seen);
            }
        }

        if (caretOnFocus >= 0)
        {
            var kb = input.touchScreenKeyboard;
            if (input.isFocused && (kb == null || kb.status == TouchScreenKeyboard.Status.Visible))
            {
                SetCaret(caretOnFocus);
                caretOnFocus = -1;
            }
            else if (Time.unscaledTime > caretUntil) caretOnFocus = -1;
        }
    }

    /// <summary>이모지 창에서 키보드로 돌아갈 때 — 칸이 다시 잡히면 커서를 이 자리에 (그냥 두면 글 끝으로 간다)</summary>
    public void CaretOnFocus(int at)
    {
        caretOnFocus = at;
        caretUntil = Time.unscaledTime + CaretWait;
    }

    private void SetCaret(int at)
    {
        if (!input.isFocused) return;
        at = Mathf.Clamp(at, 0, input.text.Length);
        input.caretPosition = at;
        var kb = input.touchScreenKeyboard;
        if (kb != null && kb.canSetSelection) kb.selection = new RangeInt(at, 0);
    }
}
