Shader "UI/FastBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Size ("Blur Size", Range(0, 20)) = 5.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        GrabPass { "_GrabTexture" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 grabPos  : TEXCOORD1;
            };

            fixed4 _Color;
            sampler2D _GrabTexture;
            float4 _GrabTexture_TexelSize;
            float _Size;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.grabPos = ComputeGrabScreenPos(OUT.vertex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float4 sum = float4(0,0,0,0);
                float2 uv = IN.grabPos.xy / IN.grabPos.w;
                
                // Simple 9-tap box blur for performance
                // In production, dual-pass Kawase or Gaussian is better but this is fast enough for UI.
                
                float offset = _Size * _GrabTexture_TexelSize.x;
                float offsetY = _Size * _GrabTexture_TexelSize.y;

                sum += tex2D(_GrabTexture, uv + float2(-offset, -offsetY));
                sum += tex2D(_GrabTexture, uv + float2(0, -offsetY));
                sum += tex2D(_GrabTexture, uv + float2(offset, -offsetY));
                
                sum += tex2D(_GrabTexture, uv + float2(-offset, 0));
                sum += tex2D(_GrabTexture, uv);
                sum += tex2D(_GrabTexture, uv + float2(offset, 0));
                
                sum += tex2D(_GrabTexture, uv + float2(-offset, offsetY));
                sum += tex2D(_GrabTexture, uv + float2(0, offsetY));
                sum += tex2D(_GrabTexture, uv + float2(offset, offsetY));

                return (sum / 9.0) * IN.color;
            }
            ENDCG
        }
    }
}
