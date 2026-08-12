// =============================================================================
// JKPC - Shadow.hlsl
// 阴影计算: PCF, CSM
// Layer 1 — 所有模块共享
// 规范 → 文档 §2.1.5
// =============================================================================

#ifndef JKPC_SHADOW_HLSL_INCLUDED
#define JKPC_SHADOW_HLSL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// ---------------------------------------------------------------------------
// 阴影坐标计算
// ---------------------------------------------------------------------------
float4 JKPC_GetShadowCoord(float3 positionWS, float4 positionCS)
{
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        return ComputeScreenPos(positionCS);
    #else
        return TransformWorldToShadowCoord(positionWS);
    #endif
}

// ---------------------------------------------------------------------------
// 主光源阴影采样
// 使用 URP 内置 CSM + PCF: MainLightRealtimeShadow(shadowCoord)
// CSM级联数: 4 | PCF: 3×3 tent filter | 阴影分辨率: 4096
// ---------------------------------------------------------------------------
half JKPC_MainLightShadow(float4 shadowCoord)
{
    return MainLightRealtimeShadow(shadowCoord);
}

// ---------------------------------------------------------------------------
// 额外光源阴影
// ---------------------------------------------------------------------------
half JKPC_AdditionalLightShadow(int lightIndex, float3 positionWS)
{
    #if defined(_ADDITIONAL_LIGHT_SHADOWS)
        return AdditionalLightRealtimeShadow(lightIndex, positionWS, half3(0, 0, 0));
    #else
        return 1.0h;
    #endif
}

#endif // JKPC_SHADOW_HLSL_INCLUDED
