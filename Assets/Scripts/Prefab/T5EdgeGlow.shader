Shader "Custom/T5EdgeGlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EdgeColor ("Edge Glow Color", Color) = (1, 1, 1, 1)
        _EdgeIntensity ("Edge Intensity", Range(0, 10)) = 3.0
        _EdgeWidth ("Edge Width", Range(0, 1)) = 0.3
        _CoreColor ("Core Color", Color) = (0.2, 0.2, 0.2, 1)
        _GlowPulseSpeed ("Pulse Speed", Range(0, 5)) = 1.0
        _MinGlow ("Min Glow", Range(0, 1)) = 0.5
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
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _EdgeColor;
            float _EdgeIntensity;
            float _EdgeWidth;
            float4 _CoreColor;
            float _GlowPulseSpeed;
            float _MinGlow;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 기본 텍스처
                fixed4 texColor = tex2D(_MainTex, i.uv);

                // 카메라 방향 계산
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Fresnel 효과 (모서리 감지)
                float fresnel = 1.0 - saturate(dot(i.worldNormal, viewDir));
                fresnel = pow(fresnel, 1.0 / (_EdgeWidth + 0.01)); // 가장자리 폭 조절

                // 펄스 효과 (T5 조명의 미세한 깜빡임)
                float pulse = _MinGlow + (1.0 - _MinGlow) * (0.5 + 0.5 * sin(_Time.y * _GlowPulseSpeed));

                // 모서리 발광 계산
                float3 edgeGlow = _EdgeColor.rgb * fresnel * _EdgeIntensity * pulse;

                // 최종 색상: 코어 색상 + 모서리 발광
                float3 finalColor = lerp(_CoreColor.rgb * texColor.rgb, edgeGlow, fresnel);

                // Emission 추가 (Bloom 효과를 위해)
                finalColor += edgeGlow * fresnel;

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
