using UnityEditor;
using UnityEngine;

// 이모지 아틀라스 가져오기 — 키패드 장(emoji_p*)은 밉맵 없이 2048, 글 속 장(emoji_s)은 밉맵 4096 · 둘 다 ASTC 6x6
public class R0926EmojiImport : AssetPostprocessor
{
    private const string Dir = "Assets/Redesign0926/Resources/Emoji/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Dir)) return;
        var ti = (TextureImporter)assetImporter;
        bool small = assetPath.EndsWith("emoji_s.png");
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.isReadable = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.mipmapEnabled = small;
        ti.filterMode = small ? FilterMode.Trilinear : FilterMode.Bilinear;
        ti.maxTextureSize = small ? 4096 : 2048;
        ti.textureCompression = TextureImporterCompression.Compressed;
        foreach (var platform in new[] { "Android", "iPhone" })
        {
            var s = ti.GetPlatformTextureSettings(platform);
            s.overridden = true;
            s.maxTextureSize = ti.maxTextureSize;
            s.format = TextureImporterFormat.ASTC_6x6;
            s.textureCompression = TextureImporterCompression.Compressed;
            ti.SetPlatformTextureSettings(s);
        }
    }
}
