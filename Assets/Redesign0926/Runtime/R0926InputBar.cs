using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 입력줄(채팅 · 키보드 위 입력줄) — 글이 있으면 보내기 버튼이 분홍으로 켜진다. 보내기 동작은 원래 코드 그대로.
/// </summary>
public class R0926InputBar : MonoBehaviour
{
    [SerializeField] private InputField input;
    [SerializeField] private Image sendBg;
    [Tooltip("글이 없어도 켜 둘지 (키보드 위 입력줄의 '완료'처럼)")]
    [SerializeField] private bool alwaysOn;

    private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f);
    private static readonly Color Off = new Color(0.227f, 0.251f, 0.275f);
    private float k = -1f;

    private void LateUpdate()
    {
        if (sendBg == null) return;
        bool on = alwaysOn || (input != null && !string.IsNullOrWhiteSpace(input.text));
        float want = on ? 1f : 0f;
        k = k < 0f ? want : Mathf.MoveTowards(k, want, Time.unscaledDeltaTime / 0.2f);
        sendBg.color = Color.Lerp(Off, Pink, k);
    }
}
