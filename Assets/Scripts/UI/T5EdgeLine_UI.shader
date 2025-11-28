Shader "UI/T5EdgeLine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(T5 Edge Lines)]
        _EdgeColor ("T5 Line Color", Color) = (1, 0.95, 0.8, 1)
        _EdgeWidth ("Line Width", Range(0.001, 0.1)) = 0.015
        _EdgeIntensity ("Line Intensity", Range(0, 10)) = 5.0
        _EdgeSharpness ("Line Sharpness", Range(0.1, 10)) = 2.0

        [Header(Animation)]
        _GlowPulseSpeed ("Pulse Speed", Range(0, 5)) = 1.0
        _MinGlow ("Min Glow", Range(0, 1)) = 0.8

        [Header(Corner Settings)]
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.05

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _EdgeColor;
            half _EdgeWidth;
            half _EdgeIntensity;
            half _EdgeSharpness;
            half _GlowPulseSpeed;
            half _MinGlow;
            half _CornerRadius;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 2D 이미지의 **외부 엣지** 거리 계산 (사진 밖 테두리용)
            half GetEdgeDistance(float2 uv)
            {
                // UV 0~1을 -0.5~0.5로 변환
                float2 pos = uv - 0.5;

                // 이미지 외곽선까지의 거리 (0에 가까울수록 외곽선)
                float2 edgeDist = abs(pos) - (0.5 - _CornerRadius);

                // 코너 영역: 둥근 모서리 처리
                if (edgeDist.x > 0 && edgeDist.y > 0)
                {
                    // 코너 영역 - 원호 거리
                    float cornerDist = length(edgeDist) - _CornerRadius;
                    return -cornerDist; // 음수로 반환 (외부 엣지용)
                }

                // 직선 영역: 외곽선에서 가장 가까운 거리
                float straightDist = max(edgeDist.x, edgeDist.y);

                // 외곽선에 가까울수록 0에 가까움
                return -straightDist;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 기본 텍스처 샘플링
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // 가장자리까지의 거리 계산
                half edgeDist = GetEdgeDistance(IN.texcoord);

                // T5 조명 효과: 중심이 밝고 바깥으로 갈수록 어두워짐
                // 1. 메인 라인
                half edgeMask = 1.0 - saturate(edgeDist / _EdgeWidth);
                edgeMask = pow(edgeMask, _EdgeSharpness);

                // 2. 부드러운 발광
                half tubeGlow = 1.0 - saturate(edgeDist / (_EdgeWidth * 2.0));
                tubeGlow = pow(tubeGlow, _EdgeSharpness * 0.5);

                // T5 조명 펄스 효과
                half pulse = _MinGlow + (1.0 - _MinGlow) * (0.5 + 0.5 * sin(_Time.y * _GlowPulseSpeed));

                // 최종 발광 강도
                half glowStrength = (edgeMask + tubeGlow * 0.3) * pulse;

                // T5 조명 색상
                half3 edgeGlow = _EdgeColor.rgb * glowStrength * _EdgeIntensity;

                // 최종 색상: 베이스 + T5 모서리 라인
                color.rgb += edgeGlow * color.a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
