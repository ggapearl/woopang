Shader "Woopang/GoldenGlowCube"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.12, 0.08, 0.16, 1)
        _GoldColor ("Gold Color", Color) = (0.83, 0.69, 0.22, 1)
        _GlowColor ("Glow Color", Color) = (1, 0.85, 0.4, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 2

        [Header(Edge Glow Fresnel)]
        _EdgeColor ("Edge Color", Color) = (1, 0.9, 0.5, 1)
        _EdgePower ("Edge Power", Range(0.1, 10)) = 2
        _EdgeIntensity ("Edge Intensity", Range(0, 5)) = 1.5

        [Header(Inner Glow)]
        _InnerGlowColor ("Inner Glow Color", Color) = (0.83, 0.69, 0.22, 0.3)
        _InnerGlowIntensity ("Inner Glow Intensity", Range(0, 3)) = 0.5

        [Header(Surface)]
        _Metallic ("Metallic", Range(0, 1)) = 0.3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.7

        [Header(Animation)]
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1
        _PulseMin ("Pulse Min", Range(0, 2)) = 0.8
        _PulseMax ("Pulse Max", Range(1, 5)) = 1.5
        _PulsePhaseOffset ("Pulse Phase Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        // Main Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _GoldColor;
                float4 _GlowColor;
                float _GlowIntensity;
                float4 _EdgeColor;
                float _EdgePower;
                float _EdgeIntensity;
                float4 _InnerGlowColor;
                float _InnerGlowIntensity;
                float _Metallic;
                float _Smoothness;
                float _PulseSpeed;
                float _PulseMin;
                float _PulseMax;
                float _PulsePhaseOffset;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Fresnel (Edge glow)
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _EdgePower);

                // Animated pulse
                float pulse = lerp(_PulseMin, _PulseMax, (sin(_Time.y * _PulseSpeed + _PulsePhaseOffset) + 1.0) * 0.5);

                // Base surface color (dark purple-brown)
                half3 baseColor = _BaseColor.rgb;

                // Gold gradient based on normal direction
                float goldFactor = saturate(dot(normalWS, float3(0.5, 0.8, 0.3)));
                baseColor = lerp(baseColor, _GoldColor.rgb * 0.3, goldFactor * 0.5);

                // Main light
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));

                // Simple lighting
                half3 diffuse = baseColor * (NdotL * 0.5 + 0.5) * mainLight.color;

                // Specular
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                half3 specular = pow(NdotH, 64 * _Smoothness) * _GoldColor.rgb * _Metallic;

                // Edge glow (Fresnel rim)
                half3 edgeGlow = _EdgeColor.rgb * fresnel * _EdgeIntensity * pulse;

                // Inner emission glow
                half3 innerGlow = _GlowColor.rgb * _GlowIntensity * pulse * 0.3;

                // Combine
                half3 finalColor = diffuse + specular + edgeGlow + innerGlow;

                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    // Fallback for non-URP
    FallBack "Universal Render Pipeline/Lit"
}
