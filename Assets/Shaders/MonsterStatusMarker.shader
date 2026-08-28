Shader "Possession/MonsterStatusMarker"
{
    Properties
    {
        [HDR] _MarkerColor ("Marker Color", Color) = (0.25, 0.9, 1, 1)
        _MarkerKind ("Marker Kind (0=Lightning, 1=Heart)", Range(0, 1)) = 0
        _GlowIntensity ("Glow Intensity", Range(0, 8)) = 2.2
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 5
        _MarkerTex ("Marker Texture", 2D) = "white" {}
        _UseMarkerTex ("Use Marker Texture", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "MonsterStatusMarker"
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual

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

            TEXTURE2D(_MarkerTex);
            SAMPLER(sampler_MarkerTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _MarkerColor;
                half _MarkerKind;
                half _GlowIntensity;
                half _PulseSpeed;
                half _UseMarkerTex;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 0.0001));
                return length(p - lerp(a, b, t));
            }

            float LightningMask(float2 p)
            {
                float d = SegmentDistance(p, float2(0.18, 0.58), float2(-0.05, 0.08));
                d = min(d, SegmentDistance(p, float2(-0.05, 0.08), float2(0.20, 0.08)));
                d = min(d, SegmentDistance(p, float2(0.20, 0.08), float2(-0.20, -0.58)));
                d = min(d, SegmentDistance(p, float2(-0.20, -0.58), float2(-0.02, -0.12)));
                d = min(d, SegmentDistance(p, float2(-0.02, -0.12), float2(-0.24, -0.12)));

                float core = 1.0 - smoothstep(0.035, 0.09, d);
                float glow = 1.0 - smoothstep(0.09, 0.32, d);
                return saturate(core * 1.1 + glow * 0.38);
            }

            float HeartMask(float2 p)
            {
                float x = p.x * 1.18;
                float y = p.y * 1.18;
                float q = x * x + y * y - 0.52;
                float heartFunction = q * q * q - x * x * y * y * y;
                float fill = 1.0 - smoothstep(-0.018, 0.018, heartFunction);
                float edge = 1.0 - smoothstep(0.018, 0.055, abs(heartFunction));
                return saturate(fill + edge * 0.18);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float procedural = lerp(LightningMask(p), HeartMask(p), step(0.5, _MarkerKind));
                float4 tex = SAMPLE_TEXTURE2D(_MarkerTex, sampler_MarkerTex, input.uv);
                float marker = lerp(procedural, tex.a, _UseMarkerTex);
                float pulse = 0.88 + 0.12 * sin(_Time.y * _PulseSpeed);
                float alpha = saturate(marker * pulse * _MarkerColor.a);
                half3 color = lerp(_MarkerColor.rgb, tex.rgb, _UseMarkerTex) * (_GlowIntensity * pulse);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
