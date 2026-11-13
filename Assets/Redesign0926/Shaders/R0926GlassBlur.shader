// 물방울메뉴의 뒤 화면 — 카메라 화면을 1/4 로 줄이고(0) 카와세 흐림 세 번(1~3). R0926GlassBlur 가 부른다.
Shader "Hidden/R0926/GlassBlur"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _R0926SrcTexel;    // 카메라 화면 한 칸 (1/w, 1/h)
        float4 _R0926BlurTexel;   // 줄인 화면 한 칸

        half4 Tap(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0); }

        half4 Four(float2 uv, float2 o)
        {
            return (Tap(uv + float2(o.x, o.y)) + Tap(uv + float2(-o.x, o.y)) + Tap(uv + float2(o.x, -o.y)) + Tap(uv + float2(-o.x, -o.y))) * 0.25;
        }

        half4 FragDown(Varyings i) : SV_Target { return Four(i.texcoord, _R0926SrcTexel.xy); }          // 4x4 칸을 한 칸으로
        half4 FragK1(Varyings i) : SV_Target { return Four(i.texcoord, _R0926BlurTexel.xy * 0.5); }
        half4 FragK2(Varyings i) : SV_Target { return Four(i.texcoord, _R0926BlurTexel.xy * 1.5); }
        half4 FragK3(Varyings i) : SV_Target { return Four(i.texcoord, _R0926BlurTexel.xy * 2.5); }
        ENDHLSL

        Pass { Name "Down" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDown
            ENDHLSL }
        Pass { Name "Kawase1" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragK1
            ENDHLSL }
        Pass { Name "Kawase2" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragK2
            ENDHLSL }
        Pass { Name "Kawase3" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragK3
            ENDHLSL }
    }
}
