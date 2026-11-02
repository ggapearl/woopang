using UnityEngine;

/// <summary>
/// 작은 흰 별빛 입자 재료 — 코드로 그린 네 갈래 별 + 늘 빌드에 들어가는 Sprites/Default.
/// 셰이더를 이름으로 찾던 예전 입자는 기기 빌드에 그 셰이더가 없거나 URP 와 맞지 않아 크고 흰색이 아닌 사각형으로 나왔다 (10-22)
/// </summary>
public static class StarSparkle
{
    private static Texture2D texture;
    private static Material material;

    /// <summary>별빛 재질 (모두 같이 쓴다). 셰이더가 없으면 null — 그땐 입자를 아예 띄우지 않는다</summary>
    public static Material Material
    {
        get
        {
            if (material != null) return material;
            Shader sh = Shader.Find("Sprites/Default");   // GraphicsSettings 의 '늘 넣는 셰이더' — 기기 빌드에도 있다
            if (sh == null) return null;
            material = new Material(sh) { name = "StarSparkle", mainTexture = Texture };
            return material;
        }
    }

    /// <summary>✦ 네 갈래 별 (가운데 통통하고 끝은 뾰족) + 둘레 은은한 빛. 흰색이고 모양은 알파로만</summary>
    public static Texture2D Texture
    {
        get
        {
            if (texture != null) return texture;
            const int n = 128;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float s = Mathf.Sqrt(Mathf.Abs(u)) + Mathf.Sqrt(Mathf.Abs(v));
                    float star = Mathf.Clamp01((1f - s * s / 0.9f) * 3f);
                    float glow = 0.35f * Mathf.Exp(-(u * u + v * v) / 0.04f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(star + glow) * 255f));
                }
            texture = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "StarSparkle", wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(px);
            texture.Apply(true, true);
            return texture;
        }
    }

    /// <summary>별이 깜빡이는 크기 곡선 (수명 0~1) — 피어나서 blinks 번 작아졌다 커지고 사그라든다</summary>
    public static AnimationCurve TwinkleCurve(int blinks)
    {
        var c = new AnimationCurve(new Keyframe(0f, 0f));
        float step = 0.82f / (2 * blinks);
        for (int k = 0; k <= 2 * blinks; k++)
            c.AddKey(0.08f + k * step, k % 2 == 0 ? 1f : 0.3f);
        c.AddKey(1f, 0f);
        return c;
    }
}
