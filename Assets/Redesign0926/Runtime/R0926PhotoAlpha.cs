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

    /// <summary>올릴 바이트 — 투명한 사진은 PNG, 아니면 JPG</summary>
    public static byte[] Encode(Texture2D t, int jpgQuality) => Has(t) ? t.EncodeToPNG() : t.EncodeToJPG(jpgQuality);

    private static bool Transparent(Texture2D t)
    {
        var px = t.GetPixels32();
        for (int i = 0; i < px.Length; i++) if (px[i].a < Opaque) return true;
        return false;
    }
}
