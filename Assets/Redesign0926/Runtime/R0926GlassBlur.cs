using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 물방울메뉴의 「뒤 화면」 — AR 카메라 화면(투명한 것까지 다 그린 뒤)을 1/4 크기로 줄여 세 번 흐린 것을 _R0926GlassTex 로 둔다.
/// 뒤를 비추는 것(물방울 알약 R0926Glass · 창 바탕 R0926WindowSkin)이 하나라도 있을 때만 돈다.
/// 창 바탕은 더 넓게 흐린 것을 쓴다 — 1/8 로 한 번 더 줄여 네 번 흐린 _R0926GlassWide (창이 열려 있을 때만). 렌더러 설정 파일은 건드리지 않고 메인 카메라에 그때그때 패스를 끼운다.
/// </summary>
public static class R0926GlassBlur
{
    private static readonly int TexId = Shader.PropertyToID("_R0926GlassTex");
    private static readonly int ReadyId = Shader.PropertyToID("_R0926GlassReady");
    private static readonly int ScreenId = Shader.PropertyToID("_R0926GlassScreen");
    private static readonly int SrcTexelId = Shader.PropertyToID("_R0926SrcTexel");
    private static readonly int BlurTexelId = Shader.PropertyToID("_R0926BlurTexel");
    private static readonly int WideId = Shader.PropertyToID("_R0926GlassWide");
    private static readonly int WideReadyId = Shader.PropertyToID("_R0926GlassWideReady");

    private static Material mat, matWide;
    private static BlurPass pass;
    private static bool hooked;
    private static readonly HashSet<object> users = new HashSet<object>();
    private static readonly HashSet<object> wideUsers = new HashSet<object>();

    /// <summary>마지막으로 흐린 화면을 만든 프레임 · 크기 (시험용)</summary>
    public static int LastFrame { get; private set; } = -1;
    public static Vector2Int Size { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        users.Clear();
        wideUsers.Clear();
        Rest();
        LastFrame = -1;
    }

    /// <summary>who 가 뒤 화면을 쓴다 — 쓰는 게 하나라도 있으면 돈다 (wide = 창 바탕용 넓게 흐린 것도)</summary>
    public static void Need(object who, Shader shader, bool wide = false)
    {
        users.Add(who);
        if (wide) wideUsers.Add(who);
        else wideUsers.Remove(who);
        Need(shader);
        if (wide && matWide == null && mat != null) matWide = new Material(mat);
    }

    /// <summary>who 가 다 썼다 — 아무도 안 쓰면 쉰다</summary>
    public static void Release(object who)
    {
        wideUsers.Remove(who);
        if (users.Remove(who) && users.Count == 0) Rest();
    }

    private static void Need(Shader shader)
    {
        if (mat == null && shader != null) mat = CoreUtils.CreateEngineMaterial(shader);
        if (mat == null || hooked) return;
        pass ??= new BlurPass();
        RenderPipelineManager.beginCameraRendering += OnCamera;
        hooked = true;
    }

    private static void Rest()
    {
        if (hooked) RenderPipelineManager.beginCameraRendering -= OnCamera;
        hooked = false;
        Shader.SetGlobalFloat(ReadyId, 0f);
        Shader.SetGlobalFloat(WideReadyId, 0f);
        pass?.Free();
    }

    private static void OnCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam.cameraType != CameraType.Game || cam.targetTexture != null || cam != Camera.main) return;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data == null || data.scriptableRenderer == null) return;
        pass.mat = mat;
        pass.matWide = wideUsers.Count > 0 ? matWide : null;
        data.scriptableRenderer.EnqueuePass(pass);
    }

    private class BlurPass : ScriptableRenderPass
    {
        public Material mat, matWide;
        private RTHandle a, b, c, d;

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
                Shader.SetGlobalFloat(WideReadyId, 0f);
                return;
            }
            int w = Mathf.Max(1, desc.width / 4), h = Mathf.Max(1, desc.height / 4);
            Alloc(ref a, w, h, "R0926GlassA");
            Alloc(ref b, w, h, "R0926GlassB");
            mat.SetVector(SrcTexelId, new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height));
            mat.SetVector(BlurTexelId, new Vector4(1f / w, 1f / h, w, h));

            var ta = rg.ImportTexture(a);
            var tb = rg.ImportTexture(b);
            Blit(rg, res.activeColorTexture, ta, mat, 0, "R0926 Glass Down");
            Blit(rg, ta, tb, mat, 1, "R0926 Glass Blur 1");
            Blit(rg, tb, ta, mat, 2, "R0926 Glass Blur 2");
            Blit(rg, ta, tb, mat, 3, "R0926 Glass Blur 3");

            // 창 바탕용 — 1/8 로 줄여 네 번 더 (재질을 따로 둔다: 칸 크기 값이 패스마다 달라서)
            if (matWide != null)
            {
                int w2 = Mathf.Max(1, w / 2), h2 = Mathf.Max(1, h / 2);
                Alloc(ref c, w2, h2, "R0926GlassC");
                Alloc(ref d, w2, h2, "R0926GlassD");
                matWide.SetVector(BlurTexelId, new Vector4(1f / w2, 1f / h2, w2, h2));
                var tc = rg.ImportTexture(c);
                var td = rg.ImportTexture(d);
                Blit(rg, tb, tc, matWide, 1, "R0926 Glass Wide 1");
                Blit(rg, tc, td, matWide, 2, "R0926 Glass Wide 2");
                Blit(rg, td, tc, matWide, 3, "R0926 Glass Wide 3");
                Blit(rg, tc, td, matWide, 4, "R0926 Glass Wide 4");
                Shader.SetGlobalTexture(WideId, d.rt);
                Shader.SetGlobalFloat(WideReadyId, 1f);
            }
            else Shader.SetGlobalFloat(WideReadyId, 0f);

            Shader.SetGlobalTexture(TexId, b.rt);
            Shader.SetGlobalFloat(ReadyId, 1f);
            Shader.SetGlobalVector(ScreenId, new Vector4(Screen.width, Screen.height, 0f, 0f));
            LastFrame = Time.frameCount;
            Size = new Vector2Int(w, h);
        }

        private static void Blit(RenderGraph rg, TextureHandle src, TextureHandle dst, Material m, int shaderPass, string name)
        {
            using (var builder = rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(src, dst, m, shaderPass), name, returnBuilder: true))
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
            c?.Release(); c = null;
            d?.Release(); d = null;
        }
    }
}
