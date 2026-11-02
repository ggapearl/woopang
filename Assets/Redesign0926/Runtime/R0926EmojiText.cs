using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 글(예전 방식 Text) 속 우팡 이모지 글자 자리에 그림을 얹는다 (N-U) — 말풍선 · 대화 목록 미리보기 · 댓글 · 입력칸 글.
/// convert 면 글이 바뀔 때마다 진짜 이모지를 우팡 이모지 글자로 바꾼다. 글을 넣자마자 폭을 재는 곳(말풍선 · 미리보기 · 댓글)은
/// 넣을 때 R0926Emoji.ToDisplay 로 바꿔 넣는다 — 여기 것은 그 밖의 길을 위한 안전망. 입력칸 글은 칸 쪽 R0926EmojiInput 이 바꾸므로 끈다.
/// 그림 층(R0926EmojiLayer)은 처음 이모지가 들어올 때 글 아래에 만든다.
/// </summary>
[RequireComponent(typeof(Text))]
[DisallowMultipleComponent]
public class R0926EmojiText : MonoBehaviour
{
    public bool convert = true;

    private Text text;
    private R0926EmojiLayer layer;
    private string seen;
    private bool shown, dirty;

    private void Awake() => text = GetComponent<Text>();

    private void OnEnable()
    {
        text.RegisterDirtyVerticesCallback(OnTextDirty);
        seen = null;
        Refresh();
    }

    private void OnDisable() => text.UnregisterDirtyVerticesCallback(OnTextDirty);

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        string s = text.text;
        if (!ReferenceEquals(s, seen))
        {
            if (convert)
            {
                string d = R0926Emoji.ToDisplay(s);
                if (!ReferenceEquals(d, s))
                {
                    text.text = d;
                    s = text.text;
                }
            }
            seen = s;
            if (layer == null && R0926Emoji.HasPua(s)) layer = R0926EmojiLayer.For(text);
            dirty = true;
        }
        if (text.enabled != shown) { shown = text.enabled; dirty = true; }   // 글만 꺼지는 곳(입력칸 안내 글 등)
        if (dirty && layer != null)
        {
            dirty = false;
            layer.SetVerticesDirty();
        }
    }

    // 글이 다시 그려질 때(색 · 크기 · 자리) 그림도 — 그리는 도중이면 다음 LateUpdate 에
    private void OnTextDirty()
    {
        if (layer == null) return;
        if (CanvasUpdateRegistry.IsRebuildingGraphics()) dirty = true;
        else layer.SetVerticesDirty();
    }
}
