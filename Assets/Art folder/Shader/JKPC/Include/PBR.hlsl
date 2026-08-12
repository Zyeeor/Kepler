// =============================================================================
// JKPC - PBR.hlsl
// PBR 完整集成: 整合 BRDF + Lighting + Shadow + CharacterLighting
// Layer 1 — 所有模块共享的 PBR 入口
// =============================================================================

#ifndef JKPC_PBR_HLSL_INCLUDED
#define JKPC_PBR_HLSL_INCLUDED

#include "Assets/Art folder/Shader/JKPC/Include/BRDF.hlsl"
#include "Assets/Art folder/Shader/JKPC/Include/Lighting.hlsl"
#include "Assets/Art folder/Shader/JKPC/Include/Utility.hlsl"

// ---------------------------------------------------------------------------
// 完整PBR计算 (不含CharacterLighting，用于Map等)
// ---------------------------------------------------------------------------
half3 JKPC_PBR_NoCharacterLighting(
    JKPCBRDFData brdf,
    half3 N, half3 V,
    float3 positionWS,
    float4 positionCS,
    float4 shadowCoord)
{
    half3 reflectDir = ReflectDirection(V, N);

    // 直接光照 (主光源 + Forward+ 多光源)
    half3 directLight = JKPC_AllDirectLighting(brdf, N, V, positionWS, positionCS, shadowCoord);

    // 间接光照 (SH + 反射探针)
    half3 indirectLight = JKPC_AllIndirectLighting(brdf, N, V, reflectDir);

    return directLight + indirectLight;
}

#endif // JKPC_PBR_HLSL_INCLUDED
