// Screen Space Decal Shader for URP v5
// With Metallic / Smoothness / Emission support
//
// Usage:
//   1. Create a Cube (center at origin, size 1). Scale = projection range.
//   2. Create Material, select shader "Custom/Decal/ScreenSpaceDecal".
//   3. URP Asset must enable "Depth Texture".
//   4. MeshRenderer Cull = Front (already set in shader).
//
// MaskMap channels: R=Metallic, G=AO, B=Smoothness
// Emission supports HDR (intensity > 1 will bloom if post-processing is on).
Shader "Custom/Decal/ScreenSpaceDecal"
{
    Properties
    {
        [Header(Main Maps)]
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)

        [Header(Normal Map)]
        [Toggle(_USE_NORMALMAP)] _UseNormalMap ("Enable Normal Map", Float) = 0
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1

        [Header(Mask Map)]
        [Toggle(_USE_MASK)] _UseMask ("Enable Mask Map", Float) = 0
        _MaskMap ("Mask Map (R=Metallic G=AO B=Smooth)", 2D) = "white" {}

        [Header(PBR Override)]
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5

        [Header(Emission)]
        [Toggle(_USE_EMISSION)] _UseEmission ("Enable Emission", Float) = 0
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1

        [Header(Fade)]
        _AngleFade ("Angle Fade", Range(0, 1)) = 0.5
        _EdgeFade ("Edge Softness", Range(0, 1)) = 0.1

        [Header(Blending)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        // Common: Transparent = SrcAlpha(5) / OneMinusSrcAlpha(10)
        //         Additive    = SrcAlpha(5) / One(1)
        //         Multiply    = DstColor(2) / Zero(0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Decal"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest LEqual
            Cull Front
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _USE_NORMALMAP
            #pragma shader_feature_local _USE_MASK
            #pragma shader_feature_local _USE_EMISSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _NormalStrength;
                half _Smoothness;
                half _Metallic;
                half _EmissionIntensity;
                half _AngleFade;
                half _EdgeFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
            };

            // Manual world position reconstruction (works across all URP versions)
            float3 ReconstructWorldPosFromDepth(float2 screenUV, float rawDepth)
            {
                float2 ndcXY = screenUV * 2.0 - 1.0;

                #if UNITY_UV_STARTS_AT_TOP
                    ndcXY.y = -ndcXY.y;
                #endif

                float4 viewPos = mul(unity_CameraInvProjection, float4(ndcXY, rawDepth, 1.0));
                viewPos.xyz /= viewPos.w;

                float3 worldPos = mul(unity_CameraToWorld, float4(viewPos.xyz, 1.0)).xyz;
                return worldPos;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInput.positionCS;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(screenUV)).r;

                float3 worldPos = ReconstructWorldPosFromDepth(screenUV, rawDepth);

                float3 localPos = mul(unity_WorldToObject, float4(worldPos, 1.0)).xyz;
                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3( 0.5,  0.5,  0.5);

                float3 toMin = localPos - boxMin;
                float3 toMax = boxMax - localPos;
                float3 dist  = min(toMin, toMax);
                float clipValue = min(min(dist.x, dist.y), dist.z);
                clip(clipValue - 1e-4);

                float2 decalUV = localPos.xy + 0.5;
                decalUV = decalUV * _BaseMap_ST.xy + _BaseMap_ST.zw;

                float3 worldNormal = normalize(cross(ddy(worldPos), ddx(worldPos)));
                #if UNITY_UV_STARTS_AT_TOP
                    worldNormal = -worldNormal;
                #endif
                float facing = saturate(abs(dot(worldNormal, float3(0, 0, 1))));
                float angleFactor = lerp(1.0, facing, _AngleFade);

                float ex = smoothstep(0, _EdgeFade, dist.x);
                float ey = smoothstep(0, _EdgeFade, dist.y);
                float ez = smoothstep(0, _EdgeFade, dist.z);
                float edgeFade = saturate(ex * ey * ez);

                // Base color + alpha
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, decalUV) * _BaseColor;
                half alpha = baseColor.a * angleFactor * edgeFade;
                clip(alpha - 0.003);

                // PBR params (defaults from sliders)
                half metallic   = _Metallic;
                half smoothness = _Smoothness;
                half occlusion  = 1.0;

                // MaskMap overrides sliders when enabled
                #ifdef _USE_MASK
                    half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, decalUV);
                    metallic   = mask.r * _Metallic;
                    occlusion  = lerp(1.0, mask.g, 0.6);
                    smoothness = mask.b * _Smoothness;
                #endif

                // Emission
                half3 emission = half3(0, 0, 0);
                #ifdef _USE_EMISSION
                    half3 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, decalUV).rgb;
                    emission = emissionTex * _EmissionColor.rgb * _EmissionIntensity;
                #endif

                InputData inputData = (InputData)0;
                inputData.positionWS  = worldPos;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(worldPos);
                inputData.shadowCoord = TransformWorldToShadowCoord(worldPos);
                inputData.normalizedScreenSpaceUV = screenUV;

                #ifdef _USE_NORMALMAP
                    half3 normalTS = UnpackNormalScale(
                        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, decalUV),
                        _NormalStrength
                    );
                    float3x3 tbn = float3x3(
                        float3(1, 0, 0),
                        float3(0, 1, 0),
                        worldNormal
                    );
                    inputData.normalWS = normalize(mul(normalTS, tbn));
                #else
                    inputData.normalWS = worldNormal;
                #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = baseColor.rgb;
                surfaceData.alpha      = alpha;
                surfaceData.metallic   = metallic;
                surfaceData.occlusion  = occlusion;
                surfaceData.smoothness = smoothness;
                surfaceData.specular   = 0;
                surfaceData.emission   = emission;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.a = alpha;

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}