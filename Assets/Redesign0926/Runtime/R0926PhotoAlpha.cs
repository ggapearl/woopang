using UnityEngine;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// 투명한 사진(PNG 로고 · 스티커 등)은 투명한 그대로 올린다 — 11-24 대표님 「투명PNG 에 있어서는 검은색으로 채우지말고 알파값 빼서, 투명하도록」.
/// 예전엔 자르기(RGB24)와 올리기(JPG)가 투명한 곳을 검게 굳혀, 근처 장소 카드 · 목록 · AR 큐브에 검은 모서리가 떴다.
/// 자르기 · 줄이기는 원본에 알파 칸이 있으면 RGBA32 로 읽어 투명한 화소가 있을 때만 RGBA32 로 남기고(→ PNG), 없으면 예전처럼 RGB24(→ JPG).
/// 서버는 파일 이름(main.jpg · sub_1.jpg)과 상관없이 내용을 보고(/thumb) 투명도를 지킨다.
/// </summary>
public static class R0926PhotoAlpha
{
    private const byte Opaque = 250;   // 이보다 옅은 화소가 하나라도 있으면 투명한 사진

    /// <summary>원본이 알파 칸을 가졌다 (갤러리 PNG 는 RGBA32 로 읽힌다 · JPG 는 RGB24)</summary>
    public static bool CanHave(Texture t) => t != null && GraphicsFormatUtility.HasAlphaChannel(t.graphicsFormat);

    /// <summary>투명한 사진 — Read 가 RGBA32 로 남긴 것</summary>
    public static bool Has(Texture2D t) => t != null && t.format == TextureFormat.RGBA32;

    /// <summary>지금 RenderTexture.active 의 (0,0)~(w,h) 를 읽는다 — 원본에 알파 칸이 있고 투명한 화소가 있으면 RGBA32, 아니면 RGB24 (Apply 는 부른 쪽이)</summary>
    public static Texture2D Read(int w, int h, bool alphaSource)
    {
        if (alphaSource)
        {
            var a = new Texture2D(w, h, TextureFormat.RGBA32, false);
            a.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            if (Transparent(a)) return a;
            Object.Destroy(a);
        }
        var t = new Texture2D(w, h, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        return t;
    }

    /// <summary>
    /// 정사각형이 아닌 사진을 찌그러뜨리지 않고 정사각형으로 (11-26 대표님 13번 「'우팡이네집' … 좌우로 찌그러진 채로 사진을 불러오거든?」 —
    /// 앱 밖에서 올린 옛 로고는 4:3 투명 캔버스 가운데 동그라미라, 정사각형 칸 · AI(512×512) · 저장(444×444) · AR 큐브 면이 늘려 넣으며 눌렀다).
    /// 알파 칸이 있는 사진(투명 로고)은 다 보이게 — 긴 변을 맞추고 남는 곳은 투명. 아니면 가운데를 잘라 채운다(근처 목록 칸과 같다).
    /// 한 변은 maxSide 를 넘지 않는다. 읽기 전용(readable=false)이면 CPU 쪽 사본을 남기지 않는다. 받은 쪽이 지운다.
    /// </summary>
    public static Texture2D Square(Texture src, bool readable = false, int maxSide = 512)
    {
        int w = src.width, h = src.height;
        bool fit = CanHave(src);
        int side = Mathf.Max(1, Mathf.Min(maxSide, fit ? Mathf.Max(w, h) : Mathf.Min(w, h)));
        bool mips = src.mipmapCount > 1;
        var prev = RenderTexture.active;
        Texture2D dst;
        RenderTexture rt;
        if (fit)
        {
            float k = side / (float)Mathf.Max(w, h);
            int cw = Mathf.Clamp(Mathf.RoundToInt(w * k), 1, side), ch = Mathf.Clamp(Mathf.RoundToInt(h * k), 1, side);
            rt = RenderTexture.GetTemporary(cw, ch);
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            dst = new Texture2D(side, side, TextureFormat.RGBA32, mips);
            dst.SetPixels32(new Color32[side * side]);   // 남는 곳은 투명
            dst.ReadPixels(new Rect(0, 0, cw, ch), (side - cw) / 2, (side - ch) / 2, false);
        }
        else
        {
            Vector2 scale = w >= h ? new Vector2(h / (float)w, 1f) : new Vector2(1f, w / (float)h);
            rt = RenderTexture.GetTemporary(side, side);
            Graphics.Blit(src, rt, scale, (Vector2.one - scale) * 0.5f);
            RenderTexture.active = rt;
            dst = new Texture2D(side, side, TextureFormat.RGB24, mips);
            dst.ReadPixels(new Rect(0, 0, side, side), 0, 0, false);
        }
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        dst.wrapMode = TextureWrapMode.Clamp;
        dst.Apply(mips, !readable);
        return dst;
    }

    /// <summary>올릴 바이트 — 투명한 사진은 PNG, 아니면 JPG</summary>
    public static byte[] Encode(Texture2D t, int jpgQuality) => Has(t) ? t.EncodeToPNG() : t.EncodeToJPG(jpgQuality);

    private static bool Transparent(Texture2D t)
    {
        var px = t.GetPixels32();
        for (int i = 0; i < px.Length; i++) if (px[i].a < Opaque) return true;
        return false;
    }
}
