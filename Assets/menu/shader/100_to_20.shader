Shader "Custom/DistanceAlpha" {
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            float4 _Color;

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target {
                // 투명도를 거리에 따라 조절
                float distance = length(_WorldSpaceCameraPos - i.worldPos);
                float alpha = clamp(1.0 - (distance - 20.0) / (100.0 - 20.0), 0.0, 1.0);

                // 결과 색상 계산
                half4 color = _Color;
                color.a = alpha;

                return color;
            }
            ENDCG
        }
    }
}
