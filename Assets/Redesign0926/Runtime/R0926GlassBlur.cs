using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 물방울메뉴의 「뒤 화면」 — AR 카메라 화면(투명한 것까지 다 그린 뒤)을 1/4 크기로 줄여 세 번 흐린 것을 _R0926GlassTex 로 둔다.
/// 뒤를 비추는 유리(R0926Glass.Live)가 하나라도 있을 때만 돈다. 렌더러 설정 파일은 건드리지 않고 메인 카메라에 그때그때 패스를 끼운다.
/// </summary>
public static class R0926GlassBlur
{
    private static readonly int TexId = Shader.PropertyToID("_R0926GlassTex");
    private static readonly int ReadyId = Shader.PropertyToID("_R0926GlassReady");
    private static readonly int ScreenId = Shader.PropertyToID("_R0926GlassScreen");
    private static readonly int SrcTexelId = Shader.PropertyToID("_R0926SrcTexel");
    private static readonly int BlurTexelId = Shader.PropertyToID("_R0926BlurTexel");

    private static Material mat;
    private static BlurPass pass;
    private static bool hooked;

    /// <summary>마지막으로 흐린 화면을 만든 프레임 · 크기 (시험용)</summary>
    public static int LastFrame { get; private set; } = -1;
    public static Vector2Int Size { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Rest();
        LastFrame = -1;
    }

    public static void Need(Shader shader)
    {
        if (mat == null && shader != null) mat = CoreUtils.CreateEngineMaterial(shader);
        if (mat == null || hooked) return;
        pass ??= new BlurPass();
        RenderPipelineManager.beginCameraRendering += OnCamera;
        hooked = true;
    }

    public static void Rest()
    {
        if (hooked) RenderPipelineManager.beginCameraRendering -= OnCamera;
        hooked = false;
        Shader.SetGlobalFloat(ReadyId, 0f);
        pass?.Free();
    }

    private static void OnCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam.cameraType != CameraType.Game || cam.targetTexture != null || cam != Camera.main) return;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data == null || data.scriptableRenderer == null) return;
        pass.mat = mat;
        data.scriptableRenderer.EnqueuePass(pass);
    }

    private class BlurPass : ScriptableRenderPass
    {
        public Material mat;
        private RTHandle a, b;

        public BlurPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            requiresIntermediateTexture = true;   // 화면(백버퍼)은 읽을 수 없다 — 중간 그림에 그리게
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer frame)
        {
            var res = frame.Get<UniversalResourceData>();
            var desc = frame.Get<UniversalCameraData>().cameraTargetDescriptor;
            if (mat == null || res.isActiveTargetBackBuffer || desc.msaaSamples > 1 || !res.activeColorTexture.IsValid())
            {
                Shader.SetGlobalFloat(ReadyId, 0f);
                return;
            }
            int w = Mathf.Max(1, desc.width / 4), h = Mathf.Max(1, desc.height / 4);
            Alloc(ref a, w, h, "R0926GlassA");
            Alloc(ref b, w, h, "R0926GlassB");
            mat.SetVector(SrcTexelId, new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height));
            mat.SetVector(BlurTexelId, new Vector4(1f / w, 1f / h, w, h));

            var ta = rg.ImportTexture(a);
            var tb = rg.ImportTexture(b);
            Blit(rg, res.activeColorTexture, ta, 0, "R0926 Glass Down");
            Blit(rg, ta, tb, 1, "R0926 Glass Blur 1");
            Blit(rg, tb, ta, 2, "R0926 Glass Blur 2");
            Blit(rg, ta, tb, 3, "R0926 Glass Blur 3");

            Shader.SetGlobalTexture(TexId, b.rt);
            Shader.SetGlobalFloat(ReadyId, 1f);
            Shader.SetGlobalVector(ScreenId, new Vector4(Screen.width, Screen.height, 0f, 0f));
            LastFrame = Time.frameCount;
            Size = new Vector2Int(w, h);
        }

        private void Blit(RenderGraph rg, TextureHandle src, TextureHandle dst, int shaderPass, string name)
        {
            using (var builder = rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(src, dst, mat, shaderPass), name, returnBuilder: true))
                builder.AllowPassCulling(false);
        }

        private static void Alloc(ref RTHandle h, int w, int hgt, string name)
        {
            if (h != null && h.rt != null && h.rt.width == w && h.rt.height == hgt) return;
            h?.Release();
            // 선형 색 공간 — sRGB 로 담아야 8비트로도 어두운 곳이 뭉개지지 않는다 (읽을 땐 다시 선형으로)
            h = RTHandles.Alloc(w, hgt, GraphicsFormat.R8G8B8A8_SRGB, filterMode: FilterMode.Bilinear, wrapMode: TextureWrapMode.Clamp, name: name);
        }

        public void Free()
        {
            a?.Release(); a = null;
            b?.Release(); b = null;
        }
    }
}
