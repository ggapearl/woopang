using UnityEngine;

/// <summary>
/// 스켈레톤 UI 가 쓰는 스프라이트. 모양이 늘 같으므로 한 번만 만들어 공유한다.
///
/// 예전 스켈레톤은 Image 에 sprite 를 지정하지 않은 채 Type.Sliced 만 걸어서
/// "원형"이라는 주석과 달리 각진 사각형이 그려졌다. 여기서 실제 모양을 만든다.
/// 텍스처는 작고(원 64x64, 라운드 32x32) 씬에 저장되지 않는다.
/// </summary>
public static class SkeletonSprites
{
    private static Sprite circle;
    private static Sprite rounded;

    /// <summary>가장자리가 부드러운 원. 아바타 자리표시자용.</summary>
    public static Sprite Circle
    {
        get
        {
            if (circle != null) return circle;

            const int size = 64;
            var tex = NewTexture(size, size);
            float r = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // 경계에서 1px 만 부드럽게 — 계단현상 방지
                    float a = Mathf.Clamp01(r - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            circle.hideFlags = HideFlags.HideAndDontSave;
            return circle;
        }
    }

    /// <summary>모서리가 둥근 사각형. 9-슬라이스용이라 Image.Type.Sliced 로 쓸 것.</summary>
    public static Sprite Rounded
    {
        get
        {
            if (rounded != null) return rounded;

            const int size = 32;
            const float radius = 8f;
            var tex = NewTexture(size, size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 모서리 원 중심까지의 거리로 알파를 깎는다
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                    100f, 0, SpriteMeshType.FullRect,
                                    new Vector4(radius, radius, radius, radius));
            rounded.hideFlags = HideFlags.HideAndDontSave;
            return rounded;
        }
    }

    private static Texture2D NewTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        return tex;
    }
}
