Shader "Custom/UnlitGlowPulse"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // 기본 텍스처
        _GlowColor ("Glow Color", Color) = (1, 1, 1, 1) // 빛나는 색상 (투명도 포함)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.0 // 빛 강도
        _PulseInterval ("Pulse Interval (seconds)", Range(1, 10)) = 3.0 // 깜빡임 간격 (기본 3초)
        _PulseWidth ("Pulse Width (seconds)", Range(0.1, 5)) = 0.5 // 깜빡임 지속 시간 (기본 0.5초)
        _FlickerMax ("Flicker Max Intensity", Range(0, 1)) = 1.0 // 최대 밝기
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" } // 투명 렌더링 설정
        Blend SrcAlpha OneMinusSrcAlpha // 알파 블렌딩
        ZWrite Off // 깊이 버퍼 비활성화 (투명 오브젝트용)

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _GlowColor;
            float _GlowIntensity;
            float _PulseInterval;
            float _PulseWidth;
            float _FlickerMax;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 기본 텍스처 색상
                fixed4 texColor = tex2D(_MainTex, i.uv);

                // 주기적인 펄스 계산
                float timeInCycle = fmod(_Time.y, _PulseInterval); // 주기 내 시간 (0 ~ _PulseInterval)
                float flicker;

                // 펄스 구간인지 확인
                if (timeInCycle < _PulseWidth)
                {
                    // 깜빡임 구간: 부드러운 전환
                    float t = timeInCycle / _PulseWidth; // 0 ~ 1로 정규화
                    flicker = _FlickerMax * smoothstep(0.0, 1.0, 1.0 - t); // 최대에서 0으로 감소
                }
                else
                {
                    flicker = 0.0; // 꺼짐 상태
                }

                // 최종 색상: 텍스처 + 빛나는 색상 * 점멸 * 강도
                fixed4 finalColor = texColor + _GlowColor * flicker * _GlowIntensity;
                
                // 투명도 유지
                finalColor.a = _GlowColor.a * flicker;

                return finalColor;
            }
            ENDCG
        }
    }
}