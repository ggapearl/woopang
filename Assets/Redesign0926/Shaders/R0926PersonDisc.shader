// 사람 오브젝트 — 프로필 사진 원 + 분홍 테두리 (10-25 대표님 「정면이 항상 보이는 동그라미 … 테두리에 핑크색 원 (우측하단 프로필 버튼처럼)」).
// 도크 프로필 버튼(R0926AvatarPulse)처럼 테두리가 4초마다 숨쉬고, 밝아질 무렵 물결이 한 번 퍼진다.
// 판(Quad) 하나에 전부 그린다 — 물결이 퍼질 자리가 있어야 해서 원은 판보다 작다(_Radius). 투명 가장자리는 화면 픽셀 기준으로 매끄럽게.
Shader "Universal Render Pipeline/R0926PersonDisc"
{
    Properties
    {
        [MainTexture] _BaseMap ("Photo", 2D) = "white" {}
        [MainColor] _BaseColor ("Photo Tint", Color) = (1, 1, 1, 1)
        _RingColor ("Ring Color", Color) = (0.914, 0.325, 0.514, 1)
        _Radius ("Disc Radius (UV)", Range(0.1, 0.5)) = 0.36
        _RingWidth ("Ring Width (UV)", Range(0, 0.1)) = 0.03
        _Breath ("Ring Breath", Range(0, 0.1)) = 0.035
        _Period ("Pulse Seconds", Range(1, 10)) = 4
        _GlobalAlpha ("Global Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "PersonDisc"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            Blend One OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _RingColor;
                float _Radius;
                float _RingWidth;
                float _Breath;
                float _Period;
                half _GlobalAlpha;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            // [from, to] 띠 안이면 1 — 가장자리 aa 만큼 매끄럽게
            float Band(float d, float from, float to, float aa)
            {
                return smoothstep(from - aa, from + aa, d) * (1.0 - smoothstep(to - aa, to + aa, d));
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 p = i.uv - 0.5;
                float d = length(p);
                float aa = max(fwidth(d), 1e-4);

                float ph = frac(_Time.y / _Period);
                float ring = _Radius * (1.0 + _Breath * (0.5 - 0.5 * cos(ph * TWO_PI)));

                half3 rgb = 0;
                half a = 0;

                // 물결 — 가장 밝아질 무렵(0.35~0.95) 한 번, 판 끝까지는 가지 않는다
                if (ph >= 0.35 && ph < 0.95)
                {
                    float k = (ph - 0.35) / 0.6;
                    float e = 1.0 - (1.0 - k) * (1.0 - k);
                    float r = _Radius * lerp(1.0, min(1.3, 0.49 / _Radius), e);
                    a = Band(d, r - _RingWidth * 0.5, r, aa) * 0.7 * (1.0 - k);
                    rgb = _RingColor.rgb * a;
                }

                // 사진 — 테두리 안쪽 가장자리가 사진 끝을 살짝 덮는다 (도크 프로필과 같이)
                float photoR = _Radius - _RingWidth * 0.5;
                float inPhoto = 1.0 - smoothstep(photoR - aa, photoR + aa, d);
                float2 uv = p / (photoR * 2.0) + 0.5;
                half3 photo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb * _BaseColor.rgb;
                rgb = lerp(rgb, photo, inPhoto);
                a = lerp(a, 1.0, inPhoto);

                // 테두리
                float inRing = Band(d, ring - _RingWidth, ring, aa);
                rgb = lerp(rgb, _RingColor.rgb, inRing);
                a = lerp(a, 1.0, inRing);

                return half4(rgb, a) * _GlobalAlpha;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
