// 물방울메뉴 — 알약(둥근 사각형)을 물방울 유리로 그린다. R0926Glass 가 붙은 Image 에만 쓰인다.
// 애플은 Liquid Glass 의 값을 공개하지 않았다 — 공개된 원칙(캡슐 모양 · 가장자리 렌즈 굴절 · 흐린 뒤 화면 · 테두리 반사광 · 약한 색수차)대로 비슷하게 만든 것.
// uv1 = (가운데에서 이 점까지 x, y, 뒤 화면 비춤 0/1) · uv2 = (반폭, 반높이, 모서리 반지름, 원래 알파)
// 뒤 화면 _R0926GlassTex 는 R0926GlassBlur 가 만든다. 모양 정보가 없으면 예전 그림(스프라이트 × 색) 그대로.
Shader "Hidden/R0926/LiquidGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }

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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 shape : TEXCOORD1;
                float4 size : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 shape : TEXCOORD2;
                float4 size : TEXCOORD3;
                float2 screenUV : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;
            sampler2D _R0926GlassTex;
            float _R0926GlassReady;
            float4 _R0926GlassScreen;   // 화면 픽셀 크기 — 오버레이 캔버스의 월드 좌표가 곧 화면 픽셀

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                o.shape = v.shape;
                o.size = v.size;
                o.screenUV = mul(unity_ObjectToWorld, v.vertex).xy / max(_R0926GlassScreen.xy, 1.0);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 plain = tex2D(_MainTex, i.texcoord) * i.color;   // 모양 정보가 없을 때 — 예전 그림

                float2 p = i.shape.xy;
                float2 hs = max(i.size.xy, 0.001);
                float has = step(0.002, min(i.size.x, i.size.y));
                float rad = i.size.z > 0.001 ? min(i.size.z, min(hs.x, hs.y)) : min(hs.x, hs.y);
                float tintA = i.size.w > 0.001 ? i.size.w : 0.6;
                float see = step(0.5, i.shape.z) * step(0.5, _R0926GlassReady);

                // 화면 한 픽셀이 모양 단위로 얼마인지 — 가장자리 부드럽게 · 굴절을 화면 좌표로 옮길 때
                float upp = max(length(float2(ddx(p.x), ddy(p.x))), 1e-4);

                // 둥근 사각형까지의 거리 (안쪽이 음수) · 가장자리 바깥 방향
                float2 q = abs(p) - hs + rad;
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - rad;
                float2 nq = max(q, 0.0);
                float nn = dot(nq, nq);
                float2 sp = float2(p.x >= 0.0 ? 1.0 : -1.0, p.y >= 0.0 ? 1.0 : -1.0);
                float2 n = (nn > 1e-8 ? nq * rsqrt(max(nn, 1e-8)) : (q.x > q.y ? float2(1, 0) : float2(0, 1))) * sp;

                float cover = saturate(0.5 - d / upp);
                float e = max(-d, 0.0);                      // 가장자리에서 안쪽으로
                float bez = max(rad * 0.7, 2.0 * upp);       // 렌즈처럼 휘는 띠
                float t = saturate(1.0 - e / bez);
                float lens = t * t;

                // 뒤 화면 — 가장자리에서 안쪽을 끌어와 볼록 렌즈처럼 · 빨강과 파랑은 조금씩 어긋나게 (색수차)
                float2 uvPerUnit = 1.0 / (max(_R0926GlassScreen.xy, 1.0) * upp);
                float2 off = -n * lens * (0.35 * bez) * uvPerUnit;
                float2 uv = i.screenUV;
                half3 bg;
                bg.r = tex2D(_R0926GlassTex, uv + off * 1.08).r;
                bg.g = tex2D(_R0926GlassTex, uv + off).g;
                bg.b = tex2D(_R0926GlassTex, uv + off * 0.92).b;
                half lum = dot(bg, half3(0.2126, 0.7152, 0.0722));
                bg = saturate(lerp(lum.xxx, bg, 1.25));          // 비친 색을 조금 더 또렷하게
                half3 glassCol = lerp(bg, i.color.rgb, saturate(tintA * 0.72));

                // 테두리 반사광 — 왼쪽 위에서 빛, 맞은편 아래에 약한 되비침 · 가장자리 안쪽 은은한 밝음
                float rimW = max(4.0, 1.5 * upp);
                float rim = saturate(1.0 - e / rimW);
                rim *= rim;
                float nl = dot(n, float2(-0.5, 0.866));
                float spec = rim * (0.10 + 0.55 * pow(saturate(nl), 4.0) + 0.25 * pow(saturate(-nl), 4.0));
                float sheen = t * t * t * 0.06;

                // 비출 때: 불투명한 유리(뒤 화면을 직접 그린다) · 안 비출 때(창이 열림 · 아직 준비 전): 예전 반투명 색 + 테두리 빛
                half3 cSee = glassCol + spec + sheen;
                float aFlat = saturate(tintA + spec + sheen);
                half3 cFlat = (i.color.rgb * tintA + spec + sheen) / max(aFlat, 1e-3);
                half4 c = see > 0.5 ? half4(cSee, 1.0) : half4(cFlat, aFlat);
                c.a *= cover * saturate(i.color.a / tintA);   // 캔버스 그룹으로 사라질 때
                c = has > 0.5 ? c : plain;

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
