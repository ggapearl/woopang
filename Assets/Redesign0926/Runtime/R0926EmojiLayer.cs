using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 글 아래 그림 층 — R0926EmojiText 가 만든다. 글이 쓰는 것과 같은 설정으로 글자 자리를 다시 재서(색 글자 태그도 자리는 그대로)
/// 우팡 이모지 글자 칸(1.18em) 가운데에 그림을 놓는다. 그림은 emoji_s 한 장이라 이모지가 몇 개든 한 번에 그린다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class R0926EmojiLayer : MaskableGraphic
{
    private const float Advance = 1.18f;   // WoopangEmoji.ttf 의 글자 폭 (em)
    private const float Size = 1.1f;       // 그림 한 변 (em) — 칸보다 조금 작게
    private const float Lift = 0f;         // 줄 가운데에서 위로 (em)

    [System.NonSerialized] private Text host;
    private TextGenerator gen;

    public override Texture mainTexture => R0926Emoji.Small;

    internal static R0926EmojiLayer For(Text t)
    {
        var found = t.transform.Find("Emoji0926");
        var layer = found != null ? found.GetComponent<R0926EmojiLayer>() : null;
        if (layer == null)
        {
            var go = new GameObject("Emoji0926", typeof(RectTransform), typeof(CanvasRenderer), typeof(R0926EmojiLayer));
            go.layer = t.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(t.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = t.rectTransform.pivot;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            layer = go.GetComponent<R0926EmojiLayer>();
            layer.raycastTarget = false;
        }
        layer.host = t;
        layer.SetVerticesDirty();
        return layer;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (host == null || !host.isActiveAndEnabled || host.font == null) return;
        string s = host.text;
        if (!R0926Emoji.HasPua(s)) return;

        var hrt = host.rectTransform;
        Vector2 size = hrt.rect.size;
        if (gen == null) gen = new TextGenerator(s.Length + 1);
        gen.PopulateWithErrors(s, host.GetGenerationSettings(size), host.gameObject);
        var chars = gen.characters;
        var lines = gen.lines;
        if (lines.Count == 0) return;

        float upp = 1f / host.pixelsPerUnit;
        Vector2 off = (hrt.pivot - rectTransform.pivot) * size;   // 글 자리는 글의 기준점에서 잰다
        Color32 col = new Color(1f, 1f, 1f, host.color.a * color.a);
        int count = Mathf.Min(s.Length, Mathf.Min(gen.characterCountVisible, chars.Count));
        int line = 0;
        for (int i = 0; i < count; i++)
        {
            int e = R0926Emoji.IndexOf(s[i]);
            if (e < 0) continue;
            var ch = chars[i];
            if (ch.charWidth <= 0f) continue;
            while (line + 1 < lines.Count && lines[line + 1].startCharIdx <= i) line++;
            var li = lines[line];
            float em = ch.charWidth / Advance;
            float half = em * Size * 0.5f;
            float cx = ch.cursorPos.x + ch.charWidth * 0.5f;
            float cy = li.topY - li.height * 0.5f + em * Lift;
            var uv = R0926Emoji.SmallUV(e);
            var a = new Vector2((cx - half) * upp + off.x, (cy - half) * upp + off.y);
            var b = new Vector2((cx + half) * upp + off.x, (cy + half) * upp + off.y);
            int v = vh.currentVertCount;
            vh.AddVert(new Vector3(a.x, a.y), col, new Vector2(uv.xMin, uv.yMin));
            vh.AddVert(new Vector3(a.x, b.y), col, new Vector2(uv.xMin, uv.yMax));
            vh.AddVert(new Vector3(b.x, b.y), col, new Vector2(uv.xMax, uv.yMax));
            vh.AddVert(new Vector3(b.x, a.y), col, new Vector2(uv.xMax, uv.yMin));
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v + 2, v + 3, v);
        }
    }
}
