// =============================================================================
// JKPC - Map_PBR.hlsl
// 棋盘PBR实现 (无CharacterLighting，使用URP默认灯光，支持 Lightmap)
// Layer 2 — Library/Map/
// 规范 → 文档 §2.3
// =============================================================================

#ifndef JKPC_MAP_PBR_HLSL_INCLUDED
#define JKPC_MAP_PBR_HLSL_INCLUDED

#include "Assets/Art folder/Shader/JKPC/Include/PBR.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

// ---------------------------------------------------------------------------
// Map PBR 完整光照 — 不含 CharacterLighting，固定使用 URP 默认直接光，支持 Lightmap + SSAO
// lightmapUV: 顶点传来的 lightmap UV（已经过 unity_LightmapST 变换）
// ---------------------------------------------------------------------------

half3 Map_PBR(
    JKPCBRDFData brdf,
    half3 baseColor,
    half baseSpecArea,
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
    half3 indirectDiffuse;
    #ifdef LIGHTMAP_ON
        half4 encodedIrradiance = SAMPLE_TEXTURE2D(unity_Lightmap, samplerunity_Lightmap, samplerunity_Lightmap, lightmapUV);
        half3 lightmapColor     = DecodeLightmap(encodedIrradiance, half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0, 0));

        #ifdef DIRLIGHTMAP_COMBINED
            half4 lightmapDir = SAMPLE_TEXTURE2D(unity_Lightmap, samplerunity_LightmapInd, samplerunity_Lightmap, lightmapUV);
            half  halfLambert = dot(N, lightmapDir.xyz - 0.5h) + 0.5h;
            lightmapColor    *= halfLambert / max(1e-4h, lightmapDir.w);
        #endif

        indirectDiffuse = lightmapColor * brdf.albedo * brdf.ao;
    #else
        indirectDiffuse = JKPC_IndirectDiffuse(N, brdf.albedo, brdf.ao);
    #endif

    // ── 间接高光: 反射探针 ────────────────────────────────────────────────────
    half NdotV = max(dot(N, V), JKPC_EPSILON);
    half3 indirectSpecular = JKPC_IndirectSpecular(reflectDir, brdf.perceptualRoughness, brdf.f0, NdotV);

    // ── SSAO ──────────────────────────────────────────────────────────────────
    #if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
        float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
        AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(screenUV);
        directLight      *= aoFactor.directAmbientOcclusion;
        indirectDiffuse  *= aoFactor.indirectAmbientOcclusion;
        indirectSpecular *= aoFactor.indirectAmbientOcclusion;
    #endif

    return directLight + indirectDiffuse + indirectSpecular;
}

// 向后兼容重载（不传 lightmapUV 时退化为纯 SH，等同旧行为）
half3 Map_PBR(
    JKPCBRDFData brdf,
    half3 baseColor,
    half baseSpecArea,
    half3 N, half3 V,
    float3 positionWS,
    float4 positionCS,
    float4 shadowCoord)
{
    half3 reflectDir = ReflectDirection(V, N);

    half3 directLight = JKPC_AllDirectLighting(brdf, N, V, positionWS, positionCS, shadowCoord);


    half3 indirectLight = JKPC_AllIndirectLighting(brdf, N, V, reflectDir);

    #if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
        float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
        AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(screenUV);
        directLight   *= aoFactor.directAmbientOcclusion;
        indirectLight *= aoFactor.indirectAmbientOcclusion;
    #endif

    return directLight + indirectLight;
}

#endif // JKPC_MAP_PBR_HLSL_INCLUDED


