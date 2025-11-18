Shader "Custom/T5EdgeGlowSmooth"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EdgeColor ("T5 Glow Color", Color) = (1, 0.9, 0.7, 1) // T5 조명 색상 (따뜻한 화이트)
        _EdgeIntensity ("Edge Intensity", Range(0, 15)) = 5.0
        _EdgeWidth ("Edge Width", Range(0.01, 2)) = 0.5
        _CoreColor ("Core Color", Color) = (0.15, 0.15, 0.15, 1)
        _GlowPulseSpeed ("Pulse Speed", Range(0, 5)) = 0.5
        _MinGlow ("Min Glow", Range(0, 1)) = 0.7
        _Smoothness ("Edge Smoothness", Range(0, 1)) = 0.8 // 모서리 부드러움
        _BevelAmount ("Bevel Amount", Range(0, 0.5)) = 0.1 // 베벨 효과
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 objectPos : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _EdgeColor;
            float _EdgeIntensity;
            float _EdgeWidth;
            float4 _CoreColor;
            float _GlowPulseSpeed;
            float _MinGlow;
            float _Smoothness;
            float _BevelAmount;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.objectPos = v.vertex.xyz;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 기본 텍스처
                fixed4 texColor = tex2D(_MainTex, i.uv);

                // 카메라 방향 계산
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Fresnel 효과 (모서리 감지) - T5 조명처럼
                float fresnel = 1.0 - saturate(dot(i.worldNormal, viewDir));

                // 부드러운 모서리 효과
                fresnel = pow(fresnel, 1.0 / (_EdgeWidth + 0.01));
                fresnel = smoothstep(0.0, 1.0, fresnel) * _Smoothness + fresnel * (1.0 - _Smoothness);

                // 큐브 모서리 감지 (12개 모서리 강조)
                float3 absPos = abs(i.objectPos);
                float maxCoord = max(absPos.x, max(absPos.y, absPos.z));
                float edgeDistance = length(absPos - maxCoord);

                // 모서리에 가까울수록 더 밝게
                float edgeMask = 1.0 - saturate(edgeDistance / _BevelAmount);
                edgeMask = smoothstep(0.0, 1.0, edgeMask);

                // T5 조명 펄스 효과 (부드러운 깜빡임)
                float pulse = _MinGlow + (1.0 - _MinGlow) * (0.5 + 0.5 * sin(_Time.y * _GlowPulseSpeed));

                // 모서리 발광 계산 (T5 조명 스타일)
                float glowStrength = saturate(fresnel + edgeMask * 2.0);
                float3 edgeGlow = _EdgeColor.rgb * glowStrength * _EdgeIntensity * pulse;

                // 최종 색상: 코어 + 모서리 T5 발광
                float3 finalColor = _CoreColor.rgb * texColor.rgb;
                finalColor += edgeGlow;

                // HDR 발광 효과 (Bloom을 위한 강한 발광)
                finalColor += edgeGlow * glowStrength * 0.5;

                UNITY_APPLY_FOG(i.fogCoord, finalColor);

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
