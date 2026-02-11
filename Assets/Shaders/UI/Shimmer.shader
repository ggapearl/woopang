Shader "UI/Shimmer"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        
        _ShimmerColor ("Shimmer Color", Color) = (1,1,1,0.5)
        _ShimmerWidth ("Shimmer Width", Range(0, 1)) = 0.4
        _ShimmerSpeed ("Shimmer Speed", Range(0.1, 10)) = 2.0
        _ShimmerAngle ("Shimmer Angle", Range(-89, 89)) = 45
        
        // UI Masking
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _ShimmerColor;
            float _ShimmerWidth;
            float _ShimmerSpeed;
            float _ShimmerAngle;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Shimmer Logic
                float2 uv = IN.texcoord;
                float time = _Time.y * _ShimmerSpeed;
                
                // Calculate shimmer position based on time and angle
                float rad = _ShimmerAngle * 0.0174533;
                float pos = frac(time) * 2.0 - 0.5; // Loop 0 to 1
                
                // Rotate UV for angled shimmer
                float2 center = float2(0.5, 0.5);
                float2 rotatedUV = uv - center;
                float s = sin(rad);
                float c = cos(rad);
                float2 rUV = float2(
                    rotatedUV.x * c - rotatedUV.y * s,
                    rotatedUV.x * s + rotatedUV.y * c
                ) + center;

                // Simple band logic
                float dist = abs(rUV.x - pos);
                float shimmer = smoothstep(_ShimmerWidth, 0, dist);
                
                color.rgb += _ShimmerColor.rgb * shimmer * _ShimmerColor.a;
                
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                return color;
            }
            ENDCG
        }
    }
}
