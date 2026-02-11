Shader "Custom/RoundedCube"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Roundness ("Roundness", Range(0, 0.5)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _Roundness;

        // Distance to the edge of a rounded cube
        float sdRoundBox(float3 p, float3 b, float r)
        {
            float3 q = abs(p) - b;
            return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - r;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Get object space position
            float3 objectPos = mul(unity_WorldToObject, float4(IN.worldPos, 1.0)).xyz;

            // Calculate distance to rounded cube surface
            float3 boxSize = float3(0.5, 0.5, 0.5); // Half extents of unit cube
            float dist = sdRoundBox(objectPos, boxSize - _Roundness, _Roundness);

            // Discard pixels outside the rounded cube
            if (dist > 0.01)
                discard;

            // Apply texture and color
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
