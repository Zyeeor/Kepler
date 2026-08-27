Shader "Possession/MonsterTelegraphIndicator"
{
    Properties
    {
        [HDR] _IndicatorColor ("Indicator Color", Color) = (1, 0.02, 0.01, 1)
        _IndicatorIntensity ("Indicator Intensity", Range(0, 5)) = 1.1
        _IndicatorProgress ("Cast Progress", Range(0, 1)) = 0
        _RingWidth ("Ring Width", Range(0.01, 0.2)) = 0.045
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _IndicatorColor;
                half _IndicatorIntensity;
                half _IndicatorProgress;
                half _RingWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float SoftBand(float value, float center, float width)
            {
                return 1.0 - smoothstep(width, width * 2.0, abs(value - center));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uvPoint = input.uv * 2.0 - 1.0;
                float radius = length(uvPoint);
                clip(0.99 - radius);

                float progress = saturate(_IndicatorProgress);
                float edge = SoftBand(radius, 0.84, _RingWidth);
                float edgeGlow = exp(-abs(radius - 0.84) * 34.0) * 0.45;
                float innerFade = saturate(1.0 - radius / 0.84);

                float angle = atan2(uvPoint.y, uvPoint.x) / 6.2831853 + 0.5;
                angle = frac(angle + _Time.y * 0.035);
                float spokes = pow(saturate(cos(angle * 37.6991) * 0.5 + 0.5), 18.0);
                float rings = SoftBand(radius, 0.38 + 0.18 * sin(_Time.y * 2.0), 0.012);
                float sweep = 1.0 - smoothstep(progress - 0.08, progress + 0.08, angle);
                float innerPattern = innerFade * (spokes * (0.10 + progress * 0.75)
                    + rings * (0.12 + progress * 0.45)
                    + sweep * 0.16);

                float brightness = lerp(0.22, 1.15, progress);
                brightness *= 0.84 + 0.16 * sin(_Time.y * 9.0);
                float alpha = saturate((edge * 1.25 + edgeGlow + innerPattern) * brightness * _IndicatorColor.a);
                return half4(_IndicatorColor.rgb * _IndicatorIntensity * brightness, alpha);
            }
            ENDHLSL
        }
    }
}
