Shader "Universal Render Pipeline/T5EdgeLine"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.1, 0.1, 0.1, 1)

        [Header(Texture Padding)]
        _TexturePadding ("Texture Padding", Range(0, 0.2)) = 0.05
        _PaddingColor ("Padding Area Color", Color) = (0.05, 0.05, 0.05, 1)

        [Header(T5 Edge Lines)]
        _EdgeColor ("T5 Line Color", Color) = (1, 0.95, 0.8, 1)
        _EdgeWidth ("Line Width", Range(0.001, 0.2)) = 0.02
        _EdgeIntensity ("Line Intensity", Range(0, 20)) = 10.0
        _EdgeSharpness ("Line Sharpness", Range(0.1, 10)) = 2.0

        [Header(Animation)]
        _GlowPulseSpeed ("Pulse Speed", Range(0, 5)) = 1.0
        _MinGlow ("Min Glow", Range(0, 1)) = 0.8

        [Header(T5 Tube Effect)]
        _TubeGlow ("Tube Glow Radius", Range(0, 0.5)) = 0.1
        _InnerGlow ("Inner Brightness", Range(0, 2)) = 1.5

        [Header(Rounded Corners)]
        _Roundness ("Corner Roundness", Range(0, 0.5)) = 0.1

        [Header(Back Face)]
        _BackFaceAlpha ("Back Face Alpha", Range(0, 1)) = 0.3
        _BackFaceTint ("Back Face Tint", Color) = (0.7, 0.7, 0.7, 1)

        [Header(Fade Control)]
        _GlobalAlpha ("Global Alpha", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        LOD 300

        // 뒷면 먼저 렌더링 (흐리게)
        Pass
        {
            Name "BackFace"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex vert
            #pragma fragment fragBack

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _PaddingColor;
                half4 _EdgeColor;
                half4 _BackFaceTint;
                half _TexturePadding;
                half _EdgeWidth;
                half _EdgeIntensity;
                half _EdgeSharpness;
                half _GlowPulseSpeed;
                half _MinGlow;
                half _TubeGlow;
                half _InnerGlow;
                half _Roundness;
                half _BackFaceAlpha;
                half _GlobalAlpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.positionOS = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            float sdRoundBox(float3 p, float3 b, float r)
            {
                float3 q = abs(p) - b;
                return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - r;
            }

            half GetEdgeDistance(float3 pos)
            {
                float3 absPos = abs(pos);
                float distX1 = length(absPos.yz - float2(0.5, 0.5));
                float distY1 = length(absPos.xz - float2(0.5, 0.5));
                float distZ1 = length(absPos.xy - float2(0.5, 0.5));
                return min(distX1, min(distY1, distZ1));
            }

            half4 fragBack(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 boxSize = float3(0.5, 0.5, 0.5);
                float dist = sdRoundBox(input.positionOS, boxSize - _Roundness, _Roundness);

                if (dist > 0.01)
                    discard;

                // 텍스처 패딩
                float2 paddedUV = (input.uv - 0.5) * (1.0 + _TexturePadding * 2.0) + 0.5;
                bool isPaddingArea = paddedUV.x < 0.0 || paddedUV.x > 1.0 || paddedUV.y < 0.0 || paddedUV.y > 1.0;

                half3 baseColor;
                half baseAlpha = 1.0;

                if (isPaddingArea)
                {
                    baseColor = _PaddingColor.rgb * _BaseColor.rgb;
                    baseAlpha = _PaddingColor.a;
                }
                else
                {
                    half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, paddedUV);
                    baseColor = baseMap.rgb * _BaseColor.rgb;
                    baseAlpha = baseMap.a;
                }

                // 모서리 글로우
                half edgeDist = GetEdgeDistance(input.positionOS);
                half edgeMask = 1.0 - saturate(edgeDist / _EdgeWidth);
                edgeMask = pow(edgeMask, _EdgeSharpness);
                half tubeGlow = 1.0 - saturate(edgeDist / (_EdgeWidth + _TubeGlow));
                tubeGlow = pow(tubeGlow, _EdgeSharpness * 0.5);
                half centerBright = 1.0 - saturate(edgeDist / (_EdgeWidth * 0.5));
                centerBright = pow(centerBright, _EdgeSharpness * 2.0) * _InnerGlow;
                half pulse = _MinGlow + (1.0 - _MinGlow) * (0.5 + 0.5 * sin(_Time.y * _GlowPulseSpeed));
                half glowStrength = (edgeMask + tubeGlow * 0.5 + centerBright) * pulse;
                half3 edgeGlow = _EdgeColor.rgb * glowStrength * _EdgeIntensity;

                // 뒷면 색상: 흐리게 처리
                half3 finalColor = (baseColor + edgeGlow) * _BackFaceTint.rgb;
                finalColor = MixFog(finalColor, input.fogFactor);

                // 뒷면 알파값 적용 (GlobalAlpha로 전체 투명도 제어)
                half finalAlpha = max(baseAlpha, glowStrength * 0.5) * _BackFaceAlpha * _GlobalAlpha;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }

        // 앞면 렌더링
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _PaddingColor;
                half4 _EdgeColor;
                half4 _BackFaceTint;
                half _TexturePadding;
                half _EdgeWidth;
                half _EdgeIntensity;
                half _EdgeSharpness;
                half _GlowPulseSpeed;
                half _MinGlow;
                half _TubeGlow;
                half _InnerGlow;
                half _Roundness;
                half _BackFaceAlpha;
                half _GlobalAlpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.positionOS = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            // Signed Distance Field for rounded box
            float sdRoundBox(float3 p, float3 b, float r)
            {
                float3 q = abs(p) - b;
                return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - r;
            }

            // 큐브의 12개 모서리까지의 거리 계산
            half GetEdgeDistance(float3 pos)
            {
                // 큐브 크기 0.5 기준 (Unity cube는 -0.5 ~ 0.5)
                float3 absPos = abs(pos);

                // 각 축에서 모서리까지의 거리
                // 큐브의 모서리는 두 축이 최대값(0.5)이고 한 축은 자유로운 위치

                // X축 평행 모서리 (4개)
                float distX1 = length(absPos.yz - float2(0.5, 0.5)); // 모서리: (x, ±0.5, ±0.5)

                // Y축 평행 모서리 (4개)
                float distY1 = length(absPos.xz - float2(0.5, 0.5)); // 모서리: (±0.5, y, ±0.5)

                // Z축 평행 모서리 (4개)
                float distZ1 = length(absPos.xy - float2(0.5, 0.5)); // 모서리: (±0.5, ±0.5, z)

                // 가장 가까운 모서리까지의 거리
                float minDist = min(distX1, min(distY1, distZ1));

                return minDist;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Rounded corners clipping
                float3 boxSize = float3(0.5, 0.5, 0.5); // Half extents of unit cube
                float dist = sdRoundBox(input.positionOS, boxSize - _Roundness, _Roundness);

                // Discard pixels outside the rounded cube
                if (dist > 0.01)
                    discard;

                // 텍스처 패딩 적용: UV를 중심으로 스케일링하여 텍스처를 작게 만듦
                float2 paddedUV = input.uv;
                float padding = _TexturePadding;

                // UV를 중심(0.5, 0.5) 기준으로 스케일링
                paddedUV = (input.uv - 0.5) * (1.0 + padding * 2.0) + 0.5;

                // 패딩 영역인지 확인 (UV가 0~1 범위를 벗어나면 패딩 영역)
                bool isPaddingArea = paddedUV.x < 0.0 || paddedUV.x > 1.0 || paddedUV.y < 0.0 || paddedUV.y > 1.0;

                // Base color with alpha support
                half4 baseMap;
                half3 baseColor;
                half baseAlpha = 1.0;

                if (isPaddingArea)
                {
                    // 패딩 영역: 패딩 색상 사용
                    baseColor = _PaddingColor.rgb * _BaseColor.rgb;
                    baseAlpha = _PaddingColor.a;
                }
                else
                {
                    // 텍스처 영역: 텍스처 샘플링
                    baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, paddedUV);
                    baseColor = baseMap.rgb * _BaseColor.rgb;
                    baseAlpha = baseMap.a; // 텍스처 알파값 사용
                }

                // 모서리까지의 거리 계산
                half edgeDist = GetEdgeDistance(input.positionOS);

                // T5 조명 효과: 중심이 밝고 바깥으로 갈수록 어두워짐
                // 1. 메인 라인
                half edgeMask = 1.0 - saturate(edgeDist / _EdgeWidth);
                edgeMask = pow(edgeMask, _EdgeSharpness);

                // 2. T5 튜브의 발광 (더 넓게 퍼지는 효과)
                half tubeGlow = 1.0 - saturate(edgeDist / (_EdgeWidth + _TubeGlow));
                tubeGlow = pow(tubeGlow, _EdgeSharpness * 0.5);

                // 3. 중심부 더 밝게 (T5 조명의 중심 특성)
                half centerBright = 1.0 - saturate(edgeDist / (_EdgeWidth * 0.5));
                centerBright = pow(centerBright, _EdgeSharpness * 2.0) * _InnerGlow;

                // T5 조명 펄스 효과
                half pulse = _MinGlow + (1.0 - _MinGlow) * (0.5 + 0.5 * sin(_Time.y * _GlowPulseSpeed));

                // 최종 발광 강도
                half glowStrength = (edgeMask + tubeGlow * 0.5 + centerBright) * pulse;

                // T5 조명 색상
                half3 edgeGlow = _EdgeColor.rgb * glowStrength * _EdgeIntensity;

                // 최종 색상: 베이스 + T5 모서리 라인
                half3 finalColor = baseColor + edgeGlow;

                // HDR 발광 (Bloom 효과)
                finalColor += edgeGlow * edgeMask * 0.5;

                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                // 최종 알파값: 텍스처 알파 + 엣지 글로우 영역은 항상 보이도록 (GlobalAlpha로 전체 투명도 제어)
                half finalAlpha = max(baseAlpha, glowStrength * 0.5) * _GlobalAlpha;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }

        // Shadow casting pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            half _Roundness;

            // Signed Distance Field for rounded box
            float sdRoundBox(float3 p, float3 b, float r)
            {
                float3 q = abs(p) - b;
                return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - r;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                // Simple shadow bias
                float3 positionWS = vertexInput.positionWS;
                float3 normalWS = normalInput.normalWS;
                float4 positionCS = TransformWorldToHClip(positionWS);

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                // Rounded corners clipping for shadows
                float3 boxSize = float3(0.5, 0.5, 0.5);
                float dist = sdRoundBox(input.positionOS, boxSize - _Roundness, _Roundness);

                if (dist > 0.01)
                    discard;

                return 0;
            }
            ENDHLSL
        }

        // Depth pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            half _Roundness;

            // Signed Distance Field for rounded box
            float sdRoundBox(float3 p, float3 b, float r)
            {
                float3 q = abs(p) - b;
                return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - r;
            }

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                // Rounded corners clipping for depth
                float3 boxSize = float3(0.5, 0.5, 0.5);
                float dist = sdRoundBox(input.positionOS, boxSize - _Roundness, _Roundness);

                if (dist > 0.01)
                    discard;

                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
