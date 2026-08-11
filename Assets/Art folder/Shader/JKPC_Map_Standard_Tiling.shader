// =============================================================================
// JKPC_Map_Standard_Tiling.shader
// 棋盘 — Standard (PBR, 无CharacterLighting) + Tiling 控件
// Shader路径: JKPC/Map/Standard_Tiling
// ★ 由 JKPC_Map_Standard.shader 复制而来，仅增加 TRANSFORM_TEX(_BaseMap) 以支持 Inspector 中的 Tiling/Offset
// 规范 → 文档 §2.3, §7.4
// =============================================================================

Shader "JKPC/Map/Standard_Tiling"
{
    Properties
    {
        // ===== 基础PBR =====
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Normal]      _NormalMap ("Normal Map", 2D) = "bump" {}
                      _NormalScale ("Normal Scale", Range(0, 2)) = 1.0
                      _MetallicGlossMap ("Metallic Gloss Map (R=M G=S B=AO)", 2D) = "white" {}
                      _Metallic ("Metallic", Range(0, 2)) = 1.0
                      _Smoothness ("Smoothness", Range(0, 2)) = 1.0
                      _AOIntensity ("AO Intensity", Range(0, 1)) = 1.0

        // ===== 自发光 =====
        [Toggle(_EMISSION_ON)] _EmissionEnabled ("Enable Emission", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,0)
              _EmissionMap ("Emission Map", 2D) = "black" {}
              _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1.0

        // ===== 表面控制 (GUI联动，隐藏) =====
        [HideInInspector] _Surface ("Surface Type", Float) = 0
        [HideInInspector] _Blend ("Blend Mode", Float) = 0
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

        // ===== 渲染设置 =====
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        [HideInInspector] [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
        _QueueOffset ("Queue Offset", Float) = 0
    }

    HLSLINCLUDE
        #include "Assets/Art folder/Shader/JKPC/Include/Common.hlsl"
        #include "Assets/Art folder/Shader/JKPC/Library/Map/Map_Input.hlsl"
        #include "Assets/Art folder/Shader/JKPC/Library/Map/Map_Properties.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
        #if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        #endif

        float3 _LightDirection;
        float3 _LightPosition;

        JKPCVaryings vert(JKPCAttributes input)
        {
            JKPCVaryings output = (JKPCVaryings)0;

            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 positionOS = input.positionOS.xyz;
            VertexPositionInputs posInputs = GetVertexPositionInputs(positionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

            output.positionCS = posInputs.positionCS;
            output.uv = input.texcoord;
            output.positionWS = posInputs.positionWS;
            output.normalWS = normalInputs.normalWS;
            output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w);
            output.lightmapUV = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(posInputs);
            #else
                output.shadowCoord = float4(0, 0, 0, 0);
            #endif
            output.fogCoord = ComputeFogFactor(posInputs.positionCS.z);

            return output;
        }

        struct MapShadowAttributes
        {
            float4 positionOS   : POSITION;
            float3 normalOS     : NORMAL;
            float2 texcoord     : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct MapShadowVaryings
        {
            #if defined(_ALPHATEST_ON)
                float2 uv       : TEXCOORD0;
            #endif
            float4 positionCS   : SV_POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float4 GetMapShadowPositionHClip(MapShadowAttributes input)
        {
            float3 positionOS = input.positionOS.xyz;
            float3 positionWS = TransformObjectToWorld(positionOS);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

            return positionCS;
        }

        MapShadowVaryings ShadowPassVertex(MapShadowAttributes input)
        {
            MapShadowVaryings output = (MapShadowVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);

            #if defined(_ALPHATEST_ON)
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
            #endif

            output.positionCS = GetMapShadowPositionHClip(input);
            return output;
        }

        struct MapDepthOnlyAttributes
        {
            float4 position     : POSITION;
            float2 texcoord     : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct MapDepthOnlyVaryings
        {
            #if defined(_ALPHATEST_ON)
                float2 uv       : TEXCOORD0;
            #endif
            float4 positionCS   : SV_POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        MapDepthOnlyVaryings DepthOnlyVertex(MapDepthOnlyAttributes input)
        {
            MapDepthOnlyVaryings output = (MapDepthOnlyVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            #if defined(_ALPHATEST_ON)
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
            #endif

            float3 positionOS = input.position.xyz;
            output.positionCS = TransformObjectToHClip(positionOS);
            return output;
        }

        struct MapDepthNormalsAttributes
        {
            float4 positionOS   : POSITION;
            float4 tangentOS    : TANGENT;
            float2 texcoord     : TEXCOORD0;
            float3 normal       : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct MapDepthNormalsVaryings
        {
            float4 positionCS   : SV_POSITION;
            #if defined(_ALPHATEST_ON) || defined(_WRITE_MATERIAL_MRT)
                float2 uv       : TEXCOORD1;
            #endif
            float3 normalWS     : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        MapDepthNormalsVaryings DepthNormalsVertex(MapDepthNormalsAttributes input)
        {
            MapDepthNormalsVaryings output = (MapDepthNormalsVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            #if defined(_ALPHATEST_ON) || defined(_WRITE_MATERIAL_MRT)
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
            #endif

            float3 positionOS = input.positionOS.xyz;
            output.positionCS = TransformObjectToHClip(positionOS);

            VertexNormalInputs normalInput = GetVertexNormalInputs(input.normal, input.tangentOS);
            output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);
            return output;
        }

        half4 ShadowPassFragment(MapShadowVaryings input) : SV_TARGET
        {
            UNITY_SETUP_INSTANCE_ID(input);

            #if defined(_ALPHATEST_ON)
                Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
            #endif

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif

            return 0;
        }

        half DepthOnlyFragment(MapDepthOnlyVaryings input) : SV_TARGET
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #if defined(_ALPHATEST_ON)
                Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
            #endif

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif

            return input.positionCS.z;
        }

        void DepthNormalsFragment(
            MapDepthNormalsVaryings input
            , out half4 outNormalWS : SV_Target0
        #ifdef _WRITE_RENDERING_LAYERS
            , out float4 outRenderingLayers : SV_Target1
        #elif defined(_WRITE_MATERIAL_MRT)
            , out half4 outAlbedo : SV_Target1
        #endif
        )
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #if defined(_ALPHATEST_ON) || defined(_WRITE_MATERIAL_MRT)
                half4 albedoAlpha = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
            #endif

            #if defined(_ALPHATEST_ON)
                Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
            #endif

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif

            #if defined(_GBUFFER_NORMALS_OCT)
                float3 normalWS = normalize(input.normalWS);
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                outNormalWS = half4(packedNormalWS, 0.0);
            #else
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                outNormalWS = half4(normalWS, 0.0);
            #endif

            #ifdef _WRITE_RENDERING_LAYERS
                uint renderingLayers = GetMeshRenderingLayer();
                outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
            #elif defined(_WRITE_MATERIAL_MRT)
                half4 mgMap = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half metallic = mgMap.r * _Metallic;
                half smoothness = mgMap.g * _Smoothness;
                outAlbedo = half4(albedoAlpha.rgb * _BaseColor.rgb, smoothness);
                outNormalWS.a = metallic;
            #endif
        }
    ENDHLSL


    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            Cull [_CullMode]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog

            #pragma shader_feature_local _EMISSION_ON
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _SURFACE_TYPE_TRANSPARENT

            #include "Assets/Art folder/Shader/JKPC/Library/Map/Map_Input.hlsl"
            #include "Assets/Art folder/Shader/JKPC/Library/Map/Map_Properties.hlsl"
            #include "Assets/Art folder/Shader/JKPC/Library/Map/Map_PBR.hlsl"


            half4 frag(JKPCVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);

                // 纹理采样
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalScale);
                half4 mgMap = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);

                half metallic = mgMap.r * _Metallic;
                half baseSpecArea = mgMap.g * _Smoothness;
                half roughness = 1.0h - baseSpecArea;
                half ao = lerp(1.0h, mgMap.b, _AOIntensity);


                // 法线
                half3 bitangentWS = ComputeBitangent(input.normalWS, input.tangentWS);
                half3 normalWS = normalize(TransformTangentToWorld_JKPC(normalTS, input.tangentWS.xyz, bitangentWS, input.normalWS));
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                JKPCBRDFData brdf = InitBRDFData(baseColor.rgb, metallic, roughness, ao);

                // PBR (无CharacterLighting，支持 Lightmap)
                half3 color = Map_PBR(brdf, baseColor.rgb, baseSpecArea, normalWS, viewDirWS, input.positionWS, input.positionCS, input.shadowCoord, input.lightmapUV);


                // Emission
                #ifdef _EMISSION_ON
                    half3 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
                    color += emissionMap * _EmissionColor.rgb * _EmissionIntensity;
                #endif

                // AlphaTest
                #ifdef _ALPHATEST_ON
                    clip(baseColor.a - _Cutoff);
                #endif

                color = MixFog(color, input.fogCoord);

                #ifdef _SURFACE_TYPE_TRANSPARENT
                    return half4(color, baseColor.a);
                #else
                    return half4(color, 1.0h);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "Bake Color"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero, [_SrcBlend] [_DstBlend]
            Cull Off
            ZWrite [_ZWrite]
            Conservative On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertBake
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #define _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog

            #pragma shader_feature_local _EMISSION_ON
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _SURFACE_TYPE_TRANSPARENT

            #include "Assets/Art folder/Shader/JKPC/Library/Map/Map_Input.hlsl"
            #include "Assets/Art folder/Shader/JKPC/Library/Map/Map_Properties.hlsl"
            #include "Assets/Art folder/Shader/JKPC/Library/Map/Map_PBR.hlsl"

            float4 _LightmapST;

            float4 ClipToFragPosition(float4 clipPos)
            {
                float3 ndc = clipPos.xyz / clipPos.w;
                float2 screen01 = ndc.xy * 0.5 + 0.5;

                #if UNITY_UV_STARTS_AT_TOP
                    screen01.y = 1.0 - screen01.y;
                #endif

                float4 fragPos;
                fragPos.xy = screen01 * _ScreenParams.xy;
                fragPos.z  = ndc.z;
                fragPos.w  = 1.0 / clipPos.w;
                return fragPos;
            }

            JKPCVaryings vertBake(JKPCAttributes input)
            {
                JKPCVaryings output = (JKPCVaryings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = input.positionOS.xyz;
                VertexPositionInputs posInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv = input.texcoord;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w);
                // lightmapUV 仅用于采样 Unity 烘焙好的 lightmap（仍按 lightmapScaleOffset 定位）
                output.lightmapUV = input.lightmapUV * _LightmapST.xy + _LightmapST.zw;
                // 输出位置使用美术展开好的原始 UV2（分组图集布局），与 lightmap 采样 UV 解耦
                output.positionCS = float4((frac(input.lightmapUV.xy) * 2 - 1) * float2(1, -1), 0, 1);
                output.shadowCoord = ClipToFragPosition(posInputs.positionCS);
                output.fogCoord = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }
            
            TEXTURE2D(_Lightmap);
            SAMPLER(sampler_Lightmap);
            TEXTURE2D(_LightmapInd);
            SAMPLER(sampler_LightmapInd);
            half4 _AmbientColor;
            half4 _SHAr;
            half4 _SHAg;
            half4 _SHAb;
            half4 _SHBr;
            half4 _SHBg;
            half4 _SHBb;
            half4 _SHC;
            
            float3 SampleSH9(float3 N)
            {
                float4 shAr = _SHAr;
                float4 shAg = _SHAg;
                float4 shAb = _SHAb;
                float4 shBr = _SHBr;
                float4 shBg = _SHBg;
                float4 shBb = _SHBb;
                float4 shCr = _SHC;

                // Linear + constant polynomial terms
                float3 res = SHEvalLinearL0L1(N, shAr, shAg, shAb);

                // Quadratic polynomials
                res += SHEvalLinearL2(N, shBr, shBg, shBb, shCr);

            #ifdef UNITY_COLORSPACE_GAMMA
                res = LinearToSRGB(res);
            #endif

                return res;
            }
            
            half3 Map_Baked(
                JKPCBRDFData brdf,
                half3 N, half3 V,
                float3 positionWS,
                float4 positionCS,
                float4 shadowCoord,
                float2 lightmapUV)
            {
                half3 reflectDir = ReflectDirection(V, N);

                // ── 直接光照 ─────────────────────────────────────────────────────────────
                half3 directLight = JKPC_AllDirectLighting(brdf, N, V, positionWS, positionCS, shadowCoord);


                // ── 间接漫反射: Lightmap 优先，无 Lightmap 时回退到 SH ─────────────────

                half4 encodedIrradiance = SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, lightmapUV);
                half3 lightmapColor     = DecodeLightmap(encodedIrradiance, half4(34.493242, 2.2, 0, 0));

                // #ifdef DIRLIGHTMAP_COMBINED
                    // half4 lightmapDir = SAMPLE_TEXTURE2D(_LightmapInd, sampler_LightmapInd, lightmapUV);
                    // half  halfLambert = dot(N, lightmapDir.xyz - 0.5h) + 0.5h;
                    // lightmapColor    *= halfLambert / max(1e-4h, lightmapDir.w);
                // #endif

                half3 indirectDiffuse = (max(0, SampleSH9(N)) + lightmapColor) * brdf.albedo * brdf.ao;

                // ── 间接高光: 反射探针 ────────────────────────────────────────────────────
                half NdotV = max(dot(N, V), JKPC_EPSILON);
                half3 indirectSpecular = JKPC_IndirectSpecular(reflectDir, brdf.perceptualRoughness, brdf.f0, NdotV);
                
                return directLight + indirectDiffuse + indirectSpecular;
            }

            half4 frag(JKPCVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);

                // 纹理采样
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalScale);
                half4 mgMap = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);

                half metallic = mgMap.r * _Metallic;
                half baseSpecArea = mgMap.g * _Smoothness;
                half roughness = 1.0h - baseSpecArea;
                half ao = lerp(1.0h, mgMap.b, _AOIntensity);
                
                // 法线
                half3 bitangentWS = ComputeBitangent(input.normalWS, input.tangentWS);
                half3 normalWS = normalize(TransformTangentToWorld_JKPC(normalTS, input.tangentWS.xyz, bitangentWS, input.normalWS));
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                JKPCBRDFData brdf = InitBRDFData(baseColor.rgb, metallic, roughness, ao);

                // PBR (无CharacterLighting，支持 Lightmap)
                half3 color = Map_Baked(brdf, normalWS, viewDirWS, input.positionWS, input.shadowCoord,
                    TransformWorldToShadowCoord(input.positionWS), input.lightmapUV);
                
                // Emission
                // #ifdef _EMISSION_ON
                    half3 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
                    color += emissionMap * _EmissionColor.rgb * _EmissionIntensity;
                // #endif

                // AlphaTest

                return half4(color * 0.5, baseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            ENDHLSL

        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _ALPHATEST_ON
            // Material MRT PrePass（multi_compile 而非 multi_compile_fragment，因为 vertex shader 也需要此 keyword 来传递 UV）
            #pragma multi_compile _ _WRITE_MATERIAL_MRT
            ENDHLSL

        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #pragma shader_feature_local _EMISSION_ON
            #include "Assets/Art folder/Shader/JKPC/Include/Common.hlsl"
            #include "Assets/Art folder/Shader/JKPC/Library/Map/Map_Properties.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "JKPC.Editor.ShaderGUI.MapTilingGUI"

    FallBack "Universal Render Pipeline/Lit"
}
