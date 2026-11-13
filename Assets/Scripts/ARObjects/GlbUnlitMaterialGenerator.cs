using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using UnityEngine;
using MaterialBase = GLTFast.Schema.MaterialBase;
using TextureInfoBase = GLTFast.Schema.TextureInfoBase;

// glTF 재질 → URP/Unlit (Resources/GLBFallbackUnlit 복제) 로 바로 만든다.
// glTFast 기본 생성기는 셰이더 그래프를 Shader.Find 로 찾는데 그 셰이더가 빌드에 안 들어가 재질이 비고,
// 바탕 그림(털 무늬)·눈 색이 사라져 모델이 흰 덩어리로 떴다 (11-12 대표님 「보물찾기 3D모델 … 흰색으로만 발생하고 강아지처럼 안보이는데」).
// 에디터는 셰이더 그래프를 찾아서 멀쩡해 보였다. 이 생성기는 Resources 재질만 쓰므로 에디터와 빌드가 같다.
public sealed class GlbUnlitMaterialGenerator : IMaterialGenerator
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    static GlbUnlitMaterialGenerator s_Instance;
    static Material s_Template;
    static Material s_Default;

    // 템플릿을 못 쓰면 null — 그땐 glTFast 기본 생성기로 둔다
    public static GlbUnlitMaterialGenerator Instance
    {
        get
        {
            if (s_Template == null) s_Template = Resources.Load<Material>("GLBFallbackUnlit");
            if (s_Template == null || s_Template.shader == null || !s_Template.shader.isSupported) return null;
            return s_Instance ??= new GlbUnlitMaterialGenerator();
        }
    }

    public void SetLogger(ICodeLogger logger) { }

    public Material GetDefaultMaterial(bool pointsSupport = false)
    {
        if (s_Default == null) s_Default = new Material(s_Template) { name = "glTF-Default" };
        return s_Default;
    }

    public Material GenerateMaterial(MaterialBase gltfMaterial, IGltfReadable gltf, bool pointsSupport = false)
    {
        var m = new Material(s_Template) { name = gltfMaterial.name };

        Color color = Color.white;
        TextureInfoBase tex = null;
        var specGloss = gltfMaterial.Extensions?.KHR_materials_pbrSpecularGlossiness;
        if (specGloss != null)
        {
            color = specGloss.DiffuseColor;
            tex = specGloss.diffuseTexture;
        }
        else if (gltfMaterial.PbrMetallicRoughness != null)
        {
            color = gltfMaterial.PbrMetallicRoughness.BaseColor;
            tex = gltfMaterial.PbrMetallicRoughness.BaseColorTexture;
        }
        m.SetColor(BaseColorId, color.gamma); // glTF 색은 선형값 — SetColor 가 감마→선형으로 바꾸므로 감마로 넘긴다
        SetBaseMap(m, tex, gltf);

        m.SetFloat("_Cull", gltfMaterial.doubleSided ? 0f : 2f);
        switch (gltfMaterial.GetAlphaMode())
        {
            case MaterialBase.AlphaMode.Mask:
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", gltfMaterial.alphaCutoff);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                break;
            case MaterialBase.AlphaMode.Blend:
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;
        }
        return m;
    }

    // glTFast 와 같은 셈: 메시 UV 는 위아래가 뒤집혀 들어오므로 KHR_texture_transform 의 세로 이동을 1-oy-sy 로 옮긴다 (회전은 Unlit 이 못 받음)
    static void SetBaseMap(Material m, TextureInfoBase info, IGltfReadable gltf)
    {
        if (info == null || info.index < 0) return;
        var t = gltf.GetTexture(info.index);
        if (t == null) return;
        m.SetTexture(BaseMapId, t);

        float sx = 1f, sy = 1f, ox = 0f, oy = 0f;
        var tt = info.Extensions?.KHR_texture_transform;
        if (tt != null)
        {
            if (tt.scale != null && tt.scale.Length >= 2) { sx = tt.scale[0]; sy = tt.scale[1]; }
            float o0 = 0f, o1 = 0f;
            if (tt.offset != null && tt.offset.Length >= 2) { o0 = tt.offset[0]; o1 = tt.offset[1]; }
            ox = o0;
            oy = 1f - o1 - sy;
        }
        if (gltf.IsTextureYFlipped(info.index)) { oy = 1f - oy; sy = -sy; }
        m.SetTextureScale(BaseMapId, new Vector2(sx, sy));
        m.SetTextureOffset(BaseMapId, new Vector2(ox, oy));
    }
}
