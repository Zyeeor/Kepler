Shader "Possession/BossSevenfoldDistortion"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.42,0.18,0.62,1)
        [HDR] _RimColor ("Rim Color", Color) = (1.1,0.25,2.4,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 2.3
        _RimPulse ("Rim Pulse", Range(0,4)) = 1
        _DistortionStrength ("Distortion", Range(0,1)) = 0
        _VertexWarp ("Vertex Warp", Range(0,1)) = 0
        _ChromaticSplit ("Chromatic Split", Range(0,0.08)) = 0.01
        _DissolveAmount ("Dissolve", Range(0,1)) = 0
        _SinChannel ("Sin Channel", Range(0,6)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _RimColor;
                half _RimPower;
                half _RimPulse;
                half _DistortionStrength;
                half _VertexWarp;
                half _ChromaticSplit;
                half _DissolveAmount;
                half _SinChannel;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float fog:TEXCOORD3; };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float phase = _Time.y * 3.1 + positionOS.y * 1.7 + _SinChannel;
                positionOS += input.normalOS * sin(phase) * _VertexWarp * 0.12;
                positionOS.xz += sin(phase * 0.71 + positionOS.zx * 2.3) * _DistortionStrength * 0.06;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input):SV_Target
            {
                float noise = Hash21(floor(input.positionWS.xz * 7.0 + _Time.y * 2.0));
                clip(noise - _DissolveAmount);
                half3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight();
                half diffuse = saturate(dot(normalWS, light.direction)) * 0.55h + 0.45h;
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = pow(1.0h - saturate(dot(normalWS, viewDir)), _RimPower) * _RimPulse;
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half hue = frac(_SinChannel / 7.0h);
                half3 sinTint = 0.72h + 0.28h * cos(6.28318h * (hue + half3(0.0h, 0.33h, 0.67h)));
                half3 color = baseColor * diffuse * sinTint + _RimColor.rgb * rim;
                color += half3(_ChromaticSplit, 0, -_ChromaticSplit) * rim;
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
