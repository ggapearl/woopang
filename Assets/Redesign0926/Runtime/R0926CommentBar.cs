using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 댓글 입력줄 — 둥근 유리 입력칸 · 내 사진 · 보내기(글이 있으면 분홍으로 켜진다) · 빠른 반응(누르면 입력칸에 붙는다).
/// 보내기·글자 검사는 CommentManager 그대로. 보내기 버튼 색만 여기서 다시 칠한다(원래는 회색/파랑).
/// </summary>
public class R0926CommentBar : MonoBehaviour
{
    [SerializeField] private InputField input;
    [SerializeField] private Image sendBg;
    [SerializeField] private Image avatar;
    [Tooltip("내 사진을 가져올 곳 (도크 프로필 사진)")]
    [SerializeField] private Image avatarSource;

    private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f);
    private static readonly Color Off = new Color(0.227f, 0.251f, 0.275f);
    private float k = -1f;

    public void React(string emoji)
    {
        if (input == null || string.IsNullOrEmpty(emoji)) return;
        if (input.characterLimit > 0 && input.text.Length + emoji.Length > input.characterLimit) return;
        input.text += emoji;   // onValueChanged → CommentManager 가 보내기를 켠다
        input.MoveTextEnd(false);
    }

    private void LateUpdate()
    {
        if (avatar != null && avatarSource != null && avatar.sprite != avatarSource.sprite)
        {
            avatar.sprite = avatarSource.sprite;
            avatar.enabled = avatar.sprite != null;
        }
        if (input == null || sendBg == null) return;
        float want = string.IsNullOrWhiteSpace(input.text) ? 0f : 1f;
        k = k < 0f ? want : Mathf.MoveTowards(k, want, Time.unscaledDeltaTime / 0.2f);
        sendBg.color = Color.Lerp(Off, Pink, k);
        float pop = want > 0f ? 1f + 0.12f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) : 1f;
        sendBg.rectTransform.localScale = Vector3.one * pop;
    }
}
