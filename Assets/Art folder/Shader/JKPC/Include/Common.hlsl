// =============================================================================
// JKPC - Common.hlsl
// 通用宏、常量、工具函数
// Layer 1 — 所有模块共享
// =============================================================================

#ifndef JKPC_COMMON_HLSL_INCLUDED
#define JKPC_COMMON_HLSL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

// ---------------------------------------------------------------------------
// 常量
// ---------------------------------------------------------------------------
#define JKPC_INV_PI     0.31830988618h
#define JKPC_PI         3.14159265359h
#define JKPC_TWO_PI     6.28318530718h
#define JKPC_HALF_PI    1.57079632679h
#define JKPC_EPSILON    1e-5h

// ---------------------------------------------------------------------------
// 通用工具宏
// ---------------------------------------------------------------------------
#define JKPC_SQUARE(x)  ((x) * (x))

// SafeNormalize: 直接使用 URP Core 提供的 SafeNormalize(float3)
// 无需重复定义



// 重映射 [inMin, inMax] -> [outMin, outMax]
half Remap(half value, half inMin, half inMax, half outMin, half outMax)
{
    return outMin + (value - inMin) * (outMax - outMin) / (inMax - inMin);
}

// ---------------------------------------------------------------------------
// 颜色空间工具
// ---------------------------------------------------------------------------
half Luminance_JKPC(half3 color)
{
    return dot(color, half3(0.2126h, 0.7152h, 0.0722h));
}

half3 LinearToSRGB_JKPC(half3 color)
{
    return LinearToSRGB(max(color, 0.0h));
}

half3 SRGBToLinear_JKPC(half3 color)
{
    return SRGBToLinear(max(color, 0.0h));
}

// Gamma 项目专用：输入颜色通常处于 Gamma/sRGB，计算前转 Linear；输出前再转回 Gamma。
// 默认不转换，避免影响 Linear 项目下的原始 shader。
half3 JKPC_ColorToCalcSpace(half3 color)
{
#if defined(JKPC_NPRSTANDARD_LINEAR_CALC_IN_GAMMA)
    return SRGBToLinear_JKPC(color);
#else
    return color;
#endif
}

// C# 已经主动 .linear 后推送的颜色不能再次做 Gamma->Linear。
// 典型来源：CharacterLightingApplier.SetGlobalColor(_CL_*, color.linear)。
half3 JKPC_PreLinearColorToCalcSpace(half3 color)
{
    return color;
}

half3 JKPC_ColorFromCalcSpace(half3 color)
{
#if defined(JKPC_NPRSTANDARD_LINEAR_CALC_IN_GAMMA)
    return LinearToSRGB_JKPC(color);
#else
    return color;
#endif
}

half3 JKPC_MixFogInCalcSpace(half3 color, half fogCoord)
{
#if defined(JKPC_NPRSTANDARD_LINEAR_CALC_IN_GAMMA)
    half3 fogColor = JKPC_ColorToCalcSpace(unity_FogColor.rgb);
    return MixFogColor(color, fogColor, fogCoord);
#else
    return MixFog(color, fogCoord);
#endif
}



// ---------------------------------------------------------------------------
// Forward+ InputData 辅助
// ---------------------------------------------------------------------------
// URP Forward+ 的 LIGHT_LOOP_BEGIN 宏需要一个名为 inputData 的局部变量
// 它通过 inputData.normalizedScreenSpaceUV 和 inputData.positionWS 做 Tile Culling
// 此宏在 LIGHT_LOOP_BEGIN 之前调用，构建最小化的 InputData
#define JKPC_INIT_FORWARD_PLUS_DATA(posWS, posCS) \
    InputData inputData = (InputData)0; \
    inputData.positionWS = posWS; \
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(posCS);

#endif // JKPC_COMMON_HLSL_INCLUDED
