// Practical screen-space volume decal for URP 14.
// Built on the validated v10 depth reconstruction path.
Shader "Custom/Decal/ScreenSpaceDecal_v11"
{
    Properties
    {
        [Header(Base Color and Alpha)]
        _BaseMap ("Base Map (RGB Color, A Alpha)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _AlphaRemapMin ("Alpha Remap Min", Range(0, 1)) = 0
        _AlphaRemapMax ("Alpha Remap Max", Range(0, 1)) = 1
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0.001

        [Header(UV Controls)]
        _UVRotation ("UV Rotation", Range(0, 360)) = 0
        [Toggle] _FlipX ("Flip X", Float) = 0
        [Toggle] _FlipY ("Flip Y", Float) = 0

        [Header(Volume Edge Fade)]
        _XYEdgeFade ("XY Edge Fade", Range(0, 0.5)) = 0.02
        _DepthFade ("Depth Fade", Range(0, 0.5)) = 0.02
        _EdgePower ("Edge Power", Range(0.1, 8)) = 1

        [Header(Angle Fade)]
        [Toggle] _UseAngleFade ("Enable Angle Fade", Float) = 1
        _AngleFadeStart ("Start Angle", Range(0, 89)) = 60
        _AngleFadeEnd ("End Angle", Range(1, 90)) = 85

        [Header(Distance Fade)]
        [Toggle] _UseDistanceFade ("Enable Distance Fade", Float) = 0
        _DistanceFadeStart ("Start Distance", Float) = 25
        _DistanceFadeEnd ("End Distance", Float) = 50

        [Header(Normal)]
        [Toggle(_USE_NORMALMAP)] _UseNormalMap ("Enable Normal Map", Float) = 0
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _NormalOpacity ("Normal Opacity", Range(0, 1)) = 1

        [Header(Simple PBR)]
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5

        [Header(Emission)]
        [Toggle(_USE_EMISSION)] _UseEmission ("Enable Emission", Float) = 0
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 20)) = 1
        _EmissionOpacity ("Emission Opacity", Range(0, 1)) = 1

        [Header(Debug)]
        [Enum(Final,0,PassMagenta,1,RawDepth,2,LocalPosition,3,SurfaceNormal,4,FadeFactors,5)]
        _DebugView ("Debug View", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "ScreenSpaceDecal"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _USE_NORMALMAP
            #pragma shader_feature_local_fragment _USE_EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _Opacity;
                half _AlphaRemapMin;
                half _AlphaRemapMax;
                half _AlphaClip;
                half _UVRotation;
                half _FlipX;
                half _FlipY;
                half _XYEdgeFade;
                half _DepthFade;
                half _EdgePower;
                half _UseAngleFade;
                half _AngleFadeStart;
                half _AngleFadeEnd;
                half _UseDistanceFade;
                float _DistanceFadeStart;
                float _DistanceFadeEnd;
                half _NormalStrength;
                half _NormalOpacity;
                half _Metallic;
                half _Smoothness;
                half _EmissionIntensity;
                half _EmissionOpacity;
                half _DebugView;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float2 TransformDecalUV(float2 uv)
            {
                float2 centeredUV = uv - 0.5;
                centeredUV.x *= lerp(1.0, -1.0, saturate(_FlipX));
                centeredUV.y *= lerp(1.0, -1.0, saturate(_FlipY));

                float radiansValue = radians(_UVRotation);
                float sineValue;
                float cosineValue;
                sincos(radiansValue, sineValue, cosineValue);
                centeredUV = float2(
                    centeredUV.x * cosineValue - centeredUV.y * sineValue,
                    centeredUV.x * sineValue + centeredUV.y * cosineValue
                );

                return (centeredUV + 0.5) * _BaseMap_ST.xy + _BaseMap_ST.zw;
            }

            half3 TransformDecalNormalTS(half3 normalTS)
            {
                // UV uses Rotation * Flip, so sampled tangent normals use the inverse basis.
                float radiansValue = radians(_UVRotation);
                float sineValue;
                float cosineValue;
                sincos(radiansValue, sineValue, cosineValue);
                half2 rotatedXY = half2(
                    normalTS.x * cosineValue + normalTS.y * sineValue,
                    -normalTS.x * sineValue + normalTS.y * cosineValue
                );
                rotatedXY.x *= lerp(1.0h, -1.0h, saturate(_FlipX));
                rotatedXY.y *= lerp(1.0h, -1.0h, saturate(_FlipY));
                return normalize(half3(rotatedXY, normalTS.z));
            }

            float3 GetReceiverNormalWS(float3 worldPos, float3 viewDirectionWS)
            {
                float3 normalWS = normalize(cross(ddy(worldPos), ddx(worldPos)));
                if (dot(normalWS, viewDirectionWS) < 0.0)
                    normalWS = -normalWS;
                return normalWS;
            }

            float3 ApplyDecalNormal(float3 receiverNormalWS, half3 normalTS, half blendWeight)
            {
                float3 projectorXWS = normalize(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 projectorYWS = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));

                float3 tangentWS = projectorXWS - receiverNormalWS * dot(projectorXWS, receiverNormalWS);
                if (dot(tangentWS, tangentWS) < 1e-5)
                    tangentWS = projectorYWS - receiverNormalWS * dot(projectorYWS, receiverNormalWS);
                tangentWS = normalize(tangentWS);

                float3 bitangentWS = normalize(cross(receiverNormalWS, tangentWS));
                if (dot(bitangentWS, projectorYWS) < 0.0)
                    bitangentWS = -bitangentWS;

                float3 mappedNormalWS = normalize(
                    tangentWS * normalTS.x +
                    bitangentWS * normalTS.y +
                    receiverNormalWS * normalTS.z
                );
                return normalize(lerp(receiverNormalWS, mappedNormalWS, saturate(blendWeight)));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Runs before depth sampling, clipping and lighting.
                if (_DebugView > 0.5h && _DebugView < 1.5h)
                    return half4(1, 0, 1, 0.75h);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS.xy);
                float rawDepth = SampleSceneDepth(screenUV);

                if (_DebugView > 1.5h && _DebugView < 2.5h)
                    return half4(rawDepth.xxx, 1);

                #if UNITY_REVERSED_Z
                    clip(rawDepth - 1e-6);
                    float deviceDepth = rawDepth;
                #else
                    clip(1.0 - rawDepth - 1e-6);
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 worldPos = ComputeWorldSpacePosition(
                    screenUV,
                    deviceDepth,
                    UNITY_MATRIX_I_VP
                );
                float3 localPos = mul(unity_WorldToObject, float4(worldPos, 1.0)).xyz;

                if (_DebugView > 2.5h && _DebugView < 3.5h)
                    return half4(saturate(localPos + 0.5), 1);

                float3 boxDistance = 0.5 - abs(localPos);
                clip(min(boxDistance.x, min(boxDistance.y, boxDistance.z)));

                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(worldPos);
                float3 receiverNormalWS = GetReceiverNormalWS(worldPos, viewDirectionWS);

                if (_DebugView > 3.5h && _DebugView < 4.5h)
                    return half4(receiverNormalWS * 0.5 + 0.5, 1);

                float xyEdgeDistance = min(boxDistance.x, boxDistance.y);
                half xyFade = smoothstep(0.0, max(_XYEdgeFade, 1e-4h), xyEdgeDistance);
                half depthFade = smoothstep(0.0, max(_DepthFade, 1e-4h), boxDistance.z);
                half edgeFade = pow(saturate(xyFade * depthFade), _EdgePower);

                float3 projectorZWS = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));
                half angleFade = 1.0h;
                if (_UseAngleFade > 0.5h)
                {
                    half startAngle = min(_AngleFadeStart, _AngleFadeEnd);
                    half endAngle = max(_AngleFadeStart, _AngleFadeEnd);
                    endAngle = max(endAngle, startAngle + 0.01h);
                    // The projector's local +Z points into the receiving surface.
                    half facing = saturate(dot(receiverNormalWS, -projectorZWS));
                    half startCos = cos(radians(startAngle));
                    half endCos = cos(radians(endAngle));
                    angleFade = smoothstep(endCos, startCos, facing);
                }

                half distanceFade = 1.0h;
                if (_UseDistanceFade > 0.5h)
                {
                    float fadeStart = max(min(_DistanceFadeStart, _DistanceFadeEnd), 0.0);
                    float fadeEnd = max(max(_DistanceFadeStart, _DistanceFadeEnd), fadeStart + 1e-3);
                    float cameraDistance = distance(_WorldSpaceCameraPos, worldPos);
                    distanceFade = 1.0h - smoothstep(fadeStart, fadeEnd, cameraDistance);
                }

                if (_DebugView > 4.5h)
                    return half4(edgeFade, angleFade, distanceFade, 1);

                float2 decalUV = TransformDecalUV(localPos.xy + 0.5);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, decalUV);
                half alphaMin = min(_AlphaRemapMin, _AlphaRemapMax);
                half alphaMax = max(_AlphaRemapMin, _AlphaRemapMax);
                half alphaRange = max(alphaMax - alphaMin, 1e-4h);
                half remappedAlpha = saturate((baseSample.a - alphaMin) / alphaRange);
                clip(remappedAlpha - _AlphaClip);
                half alpha = remappedAlpha * _BaseColor.a * _Opacity * edgeFade * angleFade * distanceFade;
                clip(alpha - 1e-5h);

                half3 finalNormalWS = receiverNormalWS;
                #ifdef _USE_NORMALMAP
                    half3 normalTS = UnpackNormalScale(
                        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, decalUV),
                        _NormalStrength
                    );
                    normalTS = TransformDecalNormalTS(normalTS);
                    finalNormalWS = ApplyDecalNormal(receiverNormalWS, normalTS, _NormalOpacity);
                #endif

                half3 emission = 0;
                #ifdef _USE_EMISSION
                    half3 emissionSample = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, decalUV).rgb;
                    emission = emissionSample * _EmissionColor.rgb * _EmissionIntensity * _EmissionOpacity;
                #endif

                InputData inputData = (InputData)0;
                inputData.positionWS = worldPos;
                inputData.normalWS = finalNormalWS;
                inputData.viewDirectionWS = viewDirectionWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(worldPos);
                inputData.fogCoord = ComputeFogFactor(TransformWorldToHClip(worldPos).z);
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(finalNormalWS);
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseSample.rgb * _BaseColor.rgb;
                surfaceData.alpha = alpha;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1.0h;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.emission = emission;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = alpha;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
