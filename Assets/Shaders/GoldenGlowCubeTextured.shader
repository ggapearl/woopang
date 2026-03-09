Shader "Woopang/GoldenGlowCubeTextured"
{
    // 이미지/텍스처를 적용해도 모서리 빛 번짐 효과가 유지되는 셰이더
    // Shader that maintains edge glow even when texture/image is applied
    Properties
    {
        [Header(Texture)]
        _MainTex ("Main Texture", 2D) = "white" {}
        _TextureBlend ("Texture Blend", Range(0, 1)) = 1.0

        [Header(Base Colors)]
        _BaseColor ("Base Color (No Texture)", Color) = (0.12, 0.08, 0.16, 1)
        _GoldColor ("Gold Tint Color", Color) = (0.83, 0.69, 0.22, 1)
        _TintStrength ("Gold Tint Strength", Range(0, 1)) = 0.2

        [Header(Edge Glow - Fresnel)]
        _EdgeColor ("Edge Glow Color", Color) = (1, 0.9, 0.5, 1)
        _EdgePower ("Edge Power", Range(0.1, 10)) = 2.5
        _EdgeIntensity ("Edge Intensity", Range(0, 5)) = 2.0
        _EdgeWidth ("Edge Width", Range(0, 0.5)) = 0.15

        [Header(Inner Emission)]
        _EmissionColor ("Emission Color", Color) = (0.83, 0.69, 0.22, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 3)) = 0.5

        [Header(Surface)]
        _Metallic ("Metallic", Range(0, 1)) = 0.3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.7

        [Header(Animation)]
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1
        _PulseMin ("Pulse Min", Range(0, 2)) = 0.8
        _PulseMax ("Pulse Max", Range(1, 5)) = 1.3
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _TextureBlend;
                float4 _BaseColor;
                float4 _GoldColor;
                float _TintStrength;
                float4 _EdgeColor;
                float _EdgePower;
                float _EdgeIntensity;
                float _EdgeWidth;
                float4 _EmissionColor;
                float _EmissionIntensity;
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
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Sample texture
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Fresnel for edge glow (모서리 빛 번짐)
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _EdgePower);

                // Edge mask - UV 기반 모서리 감지 (이미지 위에도 적용)
                float2 edgeUV = abs(input.uv - 0.5) * 2.0; // 0~1 범위, 중앙이 0, 모서리가 1
                float edgeMask = max(
                    smoothstep(1.0 - _EdgeWidth * 2.0, 1.0, edgeUV.x),
                    smoothstep(1.0 - _EdgeWidth * 2.0, 1.0, edgeUV.y)
                );

                // 코너 강조
                float cornerMask = smoothstep(1.0 - _EdgeWidth * 3.0, 1.0, length(edgeUV));
                edgeMask = max(edgeMask, cornerMask);

                // Animated pulse
                float pulse = lerp(_PulseMin, _PulseMax, (sin(_Time.y * _PulseSpeed + _PulsePhaseOffset) + 1.0) * 0.5);

                // Base color (텍스처 또는 기본 색상)
                half3 baseColor = lerp(_BaseColor.rgb, texColor.rgb, _TextureBlend * texColor.a);

                // Gold tint 추가
                baseColor = lerp(baseColor, baseColor * _GoldColor.rgb, _TintStrength);

                // Main light
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));

                // Diffuse
                half3 diffuse = baseColor * (NdotL * 0.5 + 0.5) * mainLight.color;

                // Specular
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                half3 specular = pow(NdotH, 64 * _Smoothness) * _GoldColor.rgb * _Metallic;

                // Edge glow (Fresnel + UV 기반 모서리)
                // 이미지가 있어도 모서리 글로우는 위에 오버레이됨
                float combinedEdge = max(fresnel, edgeMask);
                half3 edgeGlow = _EdgeColor.rgb * combinedEdge * _EdgeIntensity * pulse;

                // Inner emission
                half3 innerGlow = _EmissionColor.rgb * _EmissionIntensity * pulse * 0.3;

                // Combine - edge glow를 마지막에 더해서 항상 보이게
                half3 finalColor = diffuse + specular + innerGlow;
                finalColor = lerp(finalColor, finalColor + edgeGlow, combinedEdge);

                // Edge 부분은 더 밝게 (additive)
                finalColor += edgeGlow * 0.5;

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

    FallBack "Universal Render Pipeline/Lit"
}
