Shader "Possession/CharacterFX"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1

        [Header(Dissolve)]
        _CorpseFade ("Corpse Fade (1=visible)", Range(0,1)) = 1
        _DissolveAmount ("Dissolve Amount (0=solid)", Range(0,1)) = 0
        _DissolveNoiseScale ("Dissolve Noise Scale", Float) = 12
        _DissolveSoftness ("Dissolve Softness", Range(0.01, 0.5)) = 0.12

        [Header(Possession Rim)]
        _RimColor ("Rim Color", Color) = (0.55, 0.2, 1, 1)
        _RimIntensity ("Rim Intensity", Range(0, 8)) = 0
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5

        [Header(Hit Flash)]
        _HitFlashColor ("Hit Flash Color", Color) = (1, 0.9, 0.9, 1)
        _HitFlashAmount ("Hit Flash Amount", Range(0, 1)) = 0

        [Header(Surface)]
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
        _Metallic ("Metallic", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _CorpseFade;
                half _DissolveAmount;
                half _DissolveNoiseScale;
                half _DissolveSoftness;
                half4 _RimColor;
                half _RimIntensity;
                half _RimPower;
                half4 _HitFlashColor;
                half _HitFlashAmount;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS = nInputs.normalWS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // 0 = solid body, 1 = fully dissolved. Transparent holes grow with progress.
                half dissolve = saturate(max(1.0h - _CorpseFade, _DissolveAmount));
                float noise = ValueNoise(input.uv * _DissolveNoiseScale + input.positionWS.xz * 0.35);
                half soft = max(_DissolveSoftness, 1e-4h);
                // Keep original albedo; only alpha opens up where noise is eaten by dissolve.
                half dissolveMask = 1.0h - smoothstep(noise - soft, noise + soft, dissolve);
                half alpha = albedo.a * dissolveMask * saturate(_CorpseFade);
                clip(alpha - 0.004h);

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                // Lifted lighting so dissolve keeps the authored look instead of collapsing to black.
                half3 ambient = SampleSH(normalWS);
                half3 lighting = mainLight.color * (ndotl * 0.45h + 0.55h) + ambient * 0.4h;

                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = pow(1.0h - saturate(dot(normalWS, viewDir)), _RimPower);
                half3 rimCol = _RimColor.rgb * rim * _RimIntensity;

                half3 color = albedo.rgb * lighting + rimCol;
                color = lerp(color, _HitFlashColor.rgb, _HitFlashAmount);
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
