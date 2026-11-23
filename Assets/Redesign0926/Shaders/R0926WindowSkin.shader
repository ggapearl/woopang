// 창 바탕 (S8 · 11-23 대표님) — R0926WindowSkin 이 붙은 Image 에만 쓰인다.
// 창 = 남색(진하기 70/80/90) · 물방울이면 뒤 AR 이 흐리게 비침 + 가장자리 빛 · 네온이면 흰 선 하나 + 바깥 빛 (안쪽 두 번째 선 없음)
// 줄 = 목록 강조 줄의 네온 (바탕 .12 · 흰 고리 1px · 빛) · 테 = 도크 · 알림 알약 둘레의 네온
// 값은 시안(C:\woopang-app\mockups\우팡_화면시안_네온물방울_1122.html)의 CSS 를 그대로 옮겼다 — 길이 단위는 CSS px(아이폰 pt)
// uv1 = (가운데에서 x, y · 캔버스 단위, 플래그, CSS 1px 당 캔버스 단위) · uv2 = (반폭, 반높이, 모서리 반지름, 원래 알파) · uv3 = (창 진하기, 뒤 그늘, -, -)
// 플래그: 1 뒤 비춤 · 2 물방울 · 4 네온 · 8 가려진 창(불투명 남색) · 16 줄 · 32 테
// 색 섞기는 브라우저처럼 감마 공간에서 한 뒤 선형으로 내보낸다. 화면에 선형으로 섞이는 반투명은 알파를 보정한다.
Shader "Hidden/R0926/WindowSkin"
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
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 shape : TEXCOORD1;
                float4 size : TEXCOORD2;
                float4 extra : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float4 worldPosition : TEXCOORD0;
                float4 shape : TEXCOORD1;
                float4 size : TEXCOORD2;
                float4 extra : TEXCOORD3;
                float2 screenUV : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;
            sampler2D _R0926GlassWide;      // R0926GlassBlur 가 넓게 흐린 AR 화면 (1/8)
            float _R0926GlassWideReady;
            float4 _R0926GlassScreen;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.shape = v.shape;
                o.size = v.size;
                o.extra = v.extra;
                // 화면 위치는 클립 좌표에서 (R0926LiquidGlass 와 같게)
                float flipY = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(0, 1, 0, 0))).y >= 0.0 ? 1.0 : -1.0;
                float2 ndc = o.vertex.xy / max(o.vertex.w, 1e-5);
                o.screenUV = float2(ndc.x, ndc.y * flipY) * 0.5 + 0.5;
                return o;
            }

            // 둥근 사각형까지의 거리 (안쪽이 음수)
            float SdRR(float2 p, float2 hs, float r)
            {
                float2 q = abs(p) - hs + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            // 표준 정규 누적분포 근사 — CSS 그림자 흐림(가우스, σ = 흐림/2)
            float Phi(float x) { return 1.0 / (1.0 + exp(-1.702 * x)); }

            // 위에 덧칠 (감마 · 미리 곱한 색)
            void Over(inout float4 acc, float3 c, float a)
            {
                acc.rgb = c * a + acc.rgb * (1.0 - a);
                acc.a = a + acc.a * (1.0 - a);
            }

            float Bit(float f, float b) { return fmod(floor(f / b + 0.001), 2.0); }

            // 바깥 빛이 상위 RectMask2D 경계에서 뚝 잘리지 않게 — 경계 14 캔버스 단위 앞에서부터 옅게
            float ClipFade(float2 w)
            {
                #ifdef UNITY_UI_CLIP_RECT
                float2 lo = saturate((w - _ClipRect.xy) / 14.0);
                float2 hi = saturate((_ClipRect.zw - w) / 14.0);
                return lo.x * lo.y * hi.x * hi.y;
                #else
                return 1.0;
                #endif
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (i.size.x < 0.001) return 0;
                float flags = floor(i.shape.z + 0.5);
                float kSee = Bit(flags, 1.0), kDrop = Bit(flags, 2.0), kNeon = Bit(flags, 4.0), kCovered = Bit(flags, 8.0);
                float kRow = Bit(flags, 16.0), kRim = Bit(flags, 32.0);

                float k = max(i.shape.w, 1e-3);                   // CSS 1px 당 캔버스 단위
                float2 p = i.shape.xy / k;                         // 가운데에서 (CSS px, 위가 +)
                float2 hs = max(i.size.xy / k, 0.01);
                float rad = min(max(i.size.z / k, 0.0), min(hs.x, hs.y));
                float upp = max(length(float2(ddx(p.x), ddy(p.x))), 1e-4);   // 화면 한 픽셀 = CSS px
                float d = SdRR(p, hs, rad);
                float cover = saturate(0.5 - d / upp);
                float fade = saturate(i.color.a / max(i.size.w, 1e-3));      // 캔버스 그룹으로 사라질 때
                float3 g = LinearToGammaSpace(i.color.rgb);                  // 빛 색 (감마)

                // 테두리 안쪽(패딩 상자) — CSS 안쪽 그림자는 테두리 1.5px 안에서부터
                float b = 1.5;
                float2 hsP = max(hs - b, 0.01);
                float radP = max(rad - b, 0.0);
                float dP = SdRR(p, hsP, radP);
                float inPad = saturate(0.5 - dP / upp);

                float4 acc = 0;      // 안쪽 (감마 · 미리 곱한 색)
                float glowA = 0;     // 바깥 빛 (색 g)
                float4 accO = 0;     // 바깥 (줄 — 흰 고리 포함)
                float opaque = 0;

                if (kRow > 0.5)
                {
                    // 줄 (.neon .row.hl): 바탕 rc .12 · 안쪽 빛 7px .3 · 바깥 빛 5px .7 / 12px .24 · 흰 고리 1px .8
                    acc = float4(g * 0.12, 0.12);
                    Over(acc, g, 0.3 * Phi(d / 3.5));
                    float keep = (1.0 - 0.7 * Phi(-d / 2.5)) * (1.0 - 0.24 * Phi(-d / 6.0));
                    accO = float4(g * (1.0 - keep), 1.0 - keep);
                    float ring = saturate(0.5 + d / upp) * saturate(0.5 - (d - 1.0) / upp);
                    Over(accO, float3(1, 1, 1), 0.8 * ring);
                    accO *= ClipFade(i.worldPosition.xy);
                }
                else if (kRim > 0.5)
                {
                    // 테 (.dock.neon): 흰 선 1.5px .92 · 안쪽 빛 3px .5 · 바깥 빛 2px .95 / 9px .5 / 20px .22
                    Over(acc, g, 0.5 * Phi(dP / 1.5) * inPad);
                    Over(acc, float3(1, 1, 1), 0.92 * saturate(0.5 + dP / upp));
                    float keep = (1.0 - 0.95 * Phi(-d / 1.0)) * (1.0 - 0.5 * Phi(-d / 4.5)) * (1.0 - 0.22 * Phi(-d / 10.0));
                    glowA = (1.0 - keep) * ClipFade(i.worldPosition.xy);
                }
                else
                {
                    // 창 — 남색 rgba(12,22,50, 진하기)
                    float dens = saturate(i.extra.x);
                    float3 navy = float3(12.0, 22.0, 50.0) / 255.0;
                    float see = kSee * step(0.5, _R0926GlassWideReady);
                    if (see > 0.5)
                    {
                        // 뒤 AR 화면 — 넓게 흐린 것을 육각으로 한 번 더 (backdrop-filter: blur(18px) saturate(1.7))
                        float2 uvPx = 1.0 / (max(_R0926GlassScreen.xy, 1.0) * upp);
                        float2 uv = i.screenUV;
                        float r = 9.0;
                        float3 bg = tex2D(_R0926GlassWide, uv).rgb * 0.28;
                        bg += tex2D(_R0926GlassWide, uv + float2(r, 0.0) * uvPx).rgb * 0.12;
                        bg += tex2D(_R0926GlassWide, uv + float2(-r, 0.0) * uvPx).rgb * 0.12;
                        bg += tex2D(_R0926GlassWide, uv + float2(0.5 * r, 0.866 * r) * uvPx).rgb * 0.12;
                        bg += tex2D(_R0926GlassWide, uv + float2(-0.5 * r, 0.866 * r) * uvPx).rgb * 0.12;
                        bg += tex2D(_R0926GlassWide, uv + float2(0.5 * r, -0.866 * r) * uvPx).rgb * 0.12;
                        bg += tex2D(_R0926GlassWide, uv + float2(-0.5 * r, -0.866 * r) * uvPx).rgb * 0.12;
                        bg *= 1.0 - saturate(i.extra.y);                 // 창 뒤 그늘 — 둘레(선형으로 섞임)와 같게
                        float3 bgG = LinearToGammaSpace(bg);
                        float lum = dot(bgG, float3(0.2126, 0.7152, 0.0722));
                        bgG = saturate(lerp(lum.xxx, bgG, 1.7));
                        acc = float4(lerp(bgG, navy, dens), 1.0);
                        opaque = 1.0;
                    }
                    else if (kCovered > 0.5)
                    {
                        acc = float4(navy, 1.0);                          // 다른 창 위에 겹친 창 — 밑 창이 비치지 않게
                        opaque = 1.0;
                    }
                    else
                    {
                        acc = float4(navy * dens, dens);
                    }

                    if (kDrop > 0.5)
                    {
                        // 아래 옅은 푸른 빛 (inset 0 -22px 34px -26px rgba(150,195,255,.32)) · 위 옅은 흰 빛 (inset 0 16px 28px -20px .32)
                        Over(acc, float3(150.0, 195.0, 255.0) / 255.0, 0.32 * Phi(SdRR(p - float2(0.0, 22.0), hsP + 26.0, radP + 26.0) / 17.0) * inPad);
                        Over(acc, float3(1, 1, 1), 0.32 * Phi(SdRR(p - float2(0.0, -16.0), hsP + 20.0, radP + 20.0) / 14.0) * inPad);
                        // 가장자리 1px 빛 — 아래 .12 · 오른쪽 .06 · 왼쪽 .13 · 위 .42
                        Over(acc, float3(1, 1, 1), 0.12 * inPad * saturate(0.5 + SdRR(p - float2(0.0, 1.0), hsP, radP) / upp));
                        Over(acc, float3(1, 1, 1), 0.06 * inPad * saturate(0.5 + SdRR(p + float2(1.0, 0.0), hsP, radP) / upp));
                        Over(acc, float3(1, 1, 1), 0.13 * inPad * saturate(0.5 + SdRR(p - float2(1.0, 0.0), hsP, radP) / upp));
                        Over(acc, float3(1, 1, 1), 0.42 * inPad * saturate(0.5 + SdRR(p + float2(0.0, 1.0), hsP, radP) / upp));
                    }
                    if (kNeon > 0.5)
                    {
                        // 안쪽 빛 10px .18 · 3px .55
                        Over(acc, g, 0.18 * Phi(dP / 5.0) * inPad);
                        Over(acc, g, 0.55 * Phi(dP / 1.5) * inPad);
                    }
                    if (kDrop > 0.5)
                    {
                        // 반짝임 — 왼쪽 위 흰 빛 · 오른쪽 아래 푸른 빛 (radial-gradient)
                        float2 tl = float2(p.x + hs.x, hs.y - p.y);
                        float2 wh = 2.0 * hs;
                        float t1 = length((tl - float2(0.16, -0.05) * wh) / (float2(1.2, 0.3) * wh));
                        Over(acc, float3(1, 1, 1), 0.13 * saturate(1.0 - t1 / 0.62) * inPad);
                        float t2 = length((tl - float2(0.88, 1.04) * wh) / (float2(0.7, 0.2) * wh));
                        Over(acc, float3(170.0, 205.0, 255.0) / 255.0, 0.11 * saturate(1.0 - t2 / 0.66) * inPad);
                    }
                    if (kNeon > 0.5)
                    {
                        // 흰 선 하나 1.5px .96 · 바깥 빛 2px .95 / 9px .55 / 22px .26
                        Over(acc, float3(1, 1, 1), 0.96 * saturate(0.5 + dP / upp));
                        float keep = (1.0 - 0.95 * Phi(-d / 1.0)) * (1.0 - 0.55 * Phi(-d / 4.5)) * (1.0 - 0.26 * Phi(-d / 11.0));
                        glowA = (1.0 - keep) * ClipFade(i.worldPosition.xy);
                    }
                }

                // 안쪽 · 바깥을 합친다 — 반투명은 화면에 선형으로 섞이므로 알파 보정 (남색 창 1.76 · 빛 1.7)
                float3 colIn = acc.rgb / max(acc.a, 1e-4);
                float aIn = opaque > 0.5 ? acc.a : (kRow + kRim > 0.5 ? pow(saturate(acc.a), 1.7) : 1.0 - pow(1.0 - saturate(acc.a), 1.76));
                float3 colOut = kRow > 0.5 ? accO.rgb / max(accO.a, 1e-4) : g;
                float aOut = pow(saturate(kRow > 0.5 ? accO.a : glowA), 1.7);
                float a = aIn * cover + aOut * (1.0 - cover);
                float3 col = (colIn * aIn * cover + colOut * aOut * (1.0 - cover)) / max(a, 1e-4);
                fixed4 c = fixed4(GammaToLinearSpace(saturate(col)), a * fade);

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(cover - 0.5);   // 마스크로 쓰일 때 — 창 모양 안쪽만 (바깥 빛 자리로 안의 것이 새지 않게)
                #endif
                return c;
            }
            ENDCG
        }
    }
}
