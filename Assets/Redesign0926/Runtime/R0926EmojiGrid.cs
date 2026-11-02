using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이모지 창의 그림 한 장(emoji_p0~6, 256개씩) — 그 장에 든 이모지를 목록 자리에 한 번에 그린다 (R0926EmojiPanel 이 만든다).
/// 큰 그림이 아직 안 읽혔으면 글 속 그림(emoji_s)으로 먼저 그린다 — 열자마자 빈칸이 보이지 않게.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class R0926EmojiGrid : MaskableGraphic
{
    public struct Item
    {
        public int emoji;
        public Vector2 center;   // 목록(content) 왼쪽 위에서 (아래는 -)
    }

    [System.NonSerialized] public float size = 140f;
    private readonly List<Item> items = new List<Item>();
    private Texture2D page;

    public override Texture mainTexture => page != null ? page : R0926Emoji.Small;

    public Texture2D Page
    {
        get => page;
        set
        {
            if (page == value) return;
            page = value;
            SetMaterialDirty();
            SetVerticesDirty();
        }
    }

    public void SetItems(List<Item> list)
    {
        items.Clear();
        items.AddRange(list);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (items.Count == 0) return;
        var r = rectTransform.rect;
        Vector2 origin = new Vector2(r.xMin, r.yMax);   // 왼쪽 위
        float half = size * 0.5f;
        Color32 col = color;
        foreach (var it in items)
        {
            var uv = page != null ? R0926Emoji.PageUV(it.emoji) : R0926Emoji.SmallUV(it.emoji);
            float x = origin.x + it.center.x, y = origin.y + it.center.y;
            int v = vh.currentVertCount;
            vh.AddVert(new Vector3(x - half, y - half), col, new Vector2(uv.xMin, uv.yMin));
            vh.AddVert(new Vector3(x - half, y + half), col, new Vector2(uv.xMin, uv.yMax));
            vh.AddVert(new Vector3(x + half, y + half), col, new Vector2(uv.xMax, uv.yMax));
            vh.AddVert(new Vector3(x + half, y - half), col, new Vector2(uv.xMax, uv.yMin));
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v + 2, v + 3, v);
        }
    }
}
