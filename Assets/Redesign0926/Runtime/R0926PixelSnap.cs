using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 픽셀 맞춤 캔버스에서 그림이 움직이면 네 모서리를 따로 반올림해 높이가 1px 씩 오간다. 가로세로 비율을 지키는 긴 그림은
/// 그때마다 가로가 10px 가까이 늘었다 줄었다 해서 밀 때 떨려 보인다 (11-20 대표님 S6 — 설정 맨 아래 저작권 그림).
/// 그리는 칸은 반올림 전 크기 그대로 두고 왼쪽 아래 모서리만 화면 픽셀 칸에 맞춘다 — 글자처럼 1px 씩 통째로 옮겨 가며 늘 같은 모양.
/// </summary>
[RequireComponent(typeof(Image))]
public class R0926PixelSnap : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount != 4) return;
        var img = graphic as Image;
        var rt = transform as RectTransform;
        if (img == null || rt == null || img.type != Image.Type.Simple || img.canvas == null) return;
        var root = img.canvas.rootCanvas;
        if (root.renderMode == RenderMode.WorldSpace) return;
        Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

        // 그림이 그려질 칸 — Image 와 같은 방식(여백 · 가로세로 비율)이되 픽셀 반올림 전 칸에서
        Rect r = rt.rect;
        if (r.width <= 0f || r.height <= 0f) return;
        if (img.sprite != null)
        {
            Vector2 size = img.sprite.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            if (img.preserveAspect)
            {
                float ratio = size.x / size.y;
                if (ratio > r.width / r.height) { float h = r.width / ratio; r.y += (r.height - h) * rt.pivot.y; r.height = h; }
                else { float w = r.height * ratio; r.x += (r.width - w) * rt.pivot.x; r.width = w; }
            }
            Vector4 p = UnityEngine.Sprites.DataUtility.GetPadding(img.sprite);
            r = Rect.MinMaxRect(r.x + r.width * p.x / size.x, r.y + r.height * p.y / size.y,
                                r.x + r.width * (size.x - p.z) / size.x, r.y + r.height * (size.y - p.w) / size.y);
        }

        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(r.min));
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(r.max));
        Vector2 d = new Vector2(Mathf.Round(a.x) - a.x, Mathf.Round(a.y) - a.y);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, a + d, cam, out var min) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, b + d, cam, out var max)) return;

        // 지금 꼭짓점 4개를 새 칸의 같은 쪽 모서리로
        var v = new UIVertex();
        Vector2 c = Vector2.zero;
        for (int i = 0; i < 4; i++) { vh.PopulateUIVertex(ref v, i); c += (Vector2)v.position; }
        c /= 4f;
        for (int i = 0; i < 4; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            v.position = new Vector3(v.position.x < c.x ? min.x : max.x, v.position.y < c.y ? min.y : max.y, v.position.z);
            vh.SetUIVertex(v, i);
        }
    }
}
