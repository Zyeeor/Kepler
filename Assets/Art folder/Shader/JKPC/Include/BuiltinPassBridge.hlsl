// =============================================================================
// JKPC_BuiltinPassBridge.hlsl
// 为 URP 内置 Pass（ShadowCaster/DepthNormals/Meta）提供必需的桥接函数
// 这些函数在 URP 的 LitInput.hlsl 中定义，但我们不能直接 include 它（CBUFFER 冲突）
// =============================================================================
#ifndef JKPC_BUILTIN_PASS_BRIDGE_HLSL
#define JKPC_BUILTIN_PASS_BRIDGE_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

// ---------------------------------------------------------------------------
// SampleAlbedoAlpha — ShadowCasterPass / DepthNormalsPass 在 _ALPHATEST_ON 时需要
// ---------------------------------------------------------------------------
#ifndef UNIVERSAL_INPUT_SURFACE_INCLUDED
half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
{
#if defined(JKPC_HAIR_CUSTOM_ALBEDO_ALPHA)
    return JKPC_SampleHairAlbedoAlpha(uv);
#else
    return half4(SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv));
#endif
}

half Alpha(half albedoAlpha, half4 color, half cutoff)
{
    half alpha = albedoAlpha * color.a;
#if defined(JKPC_HAIR_CUSTOM_ALPHA_CUTOFF)
    alpha = AlphaDiscard(alpha, JKPC_GetHairAlphaCutoff());
#else
    alpha = AlphaDiscard(alpha, cutoff);
#endif
    return alpha;
}

half3 SampleNormal(float2 uv, TEXTURE2D_PARAM(bumpMap, samplerBumpMap), half scale = half(1.0))
{
#ifdef _NORMALMAP
    half4 n = SAMPLE_TEXTURE2D(bumpMap, samplerBumpMap, uv);
    return UnpackNormalScale(n, scale);
#else
    return half3(0.0h, 0.0h, 1.0h);
#endif
}

half3 SampleEmission(float2 uv, half3 emissionColor, TEXTURE2D_PARAM(emissionMap, samplerEmissionMap))
{
#ifndef _EMISSION
    return 0;
#else
    return SAMPLE_TEXTURE2D(emissionMap, samplerEmissionMap, uv).rgb * emissionColor;
#endif
}
#endif // UNIVERSAL_INPUT_SURFACE_INCLUDED

// ---------------------------------------------------------------------------
// InitializeStandardLitSurfaceData — LitMetaPass.hlsl 需要
// 简化版: 只填充 albedo/alpha/emission，其余置零
// ---------------------------------------------------------------------------
#ifndef UNIVERSAL_INPUT_SURFACE_PBR_INCLUDED
inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));

    outSurfaceData = (SurfaceData)0;
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.alpha  = albedoAlpha.a * _BaseColor.a;

    // Emission: 采样贴图 × 颜色 × 强度，_EMISSION_ON 关闭时输出 0
#ifdef _EMISSION_ON
    #if defined(JKPC_HAIR_CUSTOM_EMISSION_UV)
        half3 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, JKPC_TransformHairEmissionUV(uv)).rgb;
    #else
        half3 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
    #endif
    outSurfaceData.emission = emissionTex * _EmissionColor.rgb * _EmissionIntensity;
#else
    outSurfaceData.emission = half3(0, 0, 0);
#endif

    outSurfaceData.metallic             = 0;
    outSurfaceData.specular             = half3(0, 0, 0);
    outSurfaceData.smoothness           = 0.5h;
    outSurfaceData.normalTS             = half3(0, 0, 1);
    outSurfaceData.occlusion            = 1;
    outSurfaceData.clearCoatMask        = 0;
    outSurfaceData.clearCoatSmoothness  = 0;
}
#endif // UNIVERSAL_INPUT_SURFACE_PBR_INCLUDED

#endif // JKPC_BUILTIN_PASS_BRIDGE_HLSL
