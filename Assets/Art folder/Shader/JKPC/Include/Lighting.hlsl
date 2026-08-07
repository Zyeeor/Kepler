// =============================================================================
// JKPC - Lighting.hlsl
// 光照计算: 直接光 + 间接光 + 多光源
// Layer 1 — 所有模块共享
// 规范 → 文档 §2.1.1 ~ §2.1.4
// =============================================================================

#ifndef JKPC_LIGHTING_HLSL_INCLUDED
#define JKPC_LIGHTING_HLSL_INCLUDED

#include "Assets/Art folder/Shader/JKPC/Include/BRDF.hlsl"
#include "Assets/Art folder/Shader/JKPC/Include/ForwardPlusLighting.hlsl"

#ifndef JKPC_SIMPLESH_CONTROLS_DECLARED
half4 _GlobalLightTint;
half  _EnvironmentIntensity;
half  _ReflectionIntensity;
half  _DiffuseLightingIntensity;
half  _SpecularLightingIntensity;
half  _SimpleSHControlsEnabled;
#endif

// ---------------------------------------------------------------------------
// 直接光照 — 单光源  §2.1.2
// ---------------------------------------------------------------------------
half3 JKPC_DirectLighting(JKPCBRDFData brdf, Light light, half3 N, half3 V)
{
    half3 L = light.direction;
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    half3 brdfResult = DirectBRDF(brdf, N, L, V);
    return brdfResult * light.color * atten;
}

// ---------------------------------------------------------------------------
// 间接光照(IBL) — §2.1.3
// ---------------------------------------------------------------------------
// 环境漫反射: SH(N) L2球谐
half3 JKPC_IndirectDiffuse(half3 N, half3 albedo, half ao)
{
    half3 sh = SampleSH(N);
    return sh * albedo * ao;
}

// 环境高光: Split-Sum Approximation
// prefilteredColor = SampleCubeMap(reflectDir, roughness * SPECCUBE_LOD_STEPS)
// envBRDF = URP内置近似
half3 JKPC_IndirectSpecular(half3 reflectDir, half perceptualRoughness, half3 f0, half NdotV)
{
    // 预过滤环境贴图 — 按粗糙度选择 Mip
    half mip = perceptualRoughness * (1.7h - 0.7h * perceptualRoughness) * UNITY_SPECCUBE_LOD_STEPS;
    half3 prefilteredColor = DecodeHDREnvironment(
        SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectDir, mip),
        unity_SpecCube0_HDR
    );

    // BRDF LUT 近似 (URP approach)
    half3 F = F_SchlickRoughness(f0, NdotV, perceptualRoughness);
    half surfaceReduction = 1.0h / (JKPC_SQUARE(perceptualRoughness) + 1.0h);
    half oneMinusReflectivity = 1.0h - max(max(f0.r, f0.g), f0.b);
    half grazingTerm = saturate((1.0h - perceptualRoughness) + (1.0h - oneMinusReflectivity));

    return prefilteredColor * surfaceReduction * lerp(f0, grazingTerm, pow(1.0h - NdotV, 5.0h));
}

// ---------------------------------------------------------------------------
// 完整直接光照（主光源 + Forward+ 多光源）
// ★ 已接入 Light Layers (Rendering Layer Mask) 过滤：
//   - 主光：不匹配 → 直接置 0
//   - 附加光：不匹配 → loop 内 continue 跳过
// ---------------------------------------------------------------------------
half3 JKPC_AllDirectLighting(
    JKPCBRDFData brdf,
    half3 N, half3 V,
    float3 positionWS,
    float4 positionCS,
    float4 shadowCoord)
{
    half3 color = 0;

    // 主方向光
    Light mainLight = JKPC_GetMainLight(shadowCoord);
    if (JKPC_MainLightLayerTest())
    {
        color += JKPC_DirectLighting(brdf, mainLight, N, V);
    }

    // Forward+ Additional Lights — 需要 inputData 供 Tile Culling
    uint lightCount = GetAdditionalLightsCount();
    JKPC_INIT_FORWARD_PLUS_DATA(positionWS, positionCS)
    // shadowMask: 用于 additional lights 的阴影遮罩（支持 _ADDITIONAL_LIGHT_SHADOWS）
    half4 shadowMask = unity_ProbesOcclusion;
    LIGHT_LOOP_BEGIN(lightCount)
        Light addLight = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        if (!JKPC_AdditionalLightLayerTest(addLight)) continue;
        color += JKPC_DirectLighting(brdf, addLight, N, V);
    LIGHT_LOOP_END

    return color;
}

// ---------------------------------------------------------------------------
// 完整间接光照
// ---------------------------------------------------------------------------
half3 JKPC_AllIndirectLighting(
    JKPCBRDFData brdf,
    half3 N, half3 V,
    half3 reflectDir)
{
    half NdotV = max(dot(N, V), JKPC_EPSILON);

    half3 indirectDiffuse = JKPC_IndirectDiffuse(N, brdf.albedo, brdf.ao);
    half3 indirectSpecular = JKPC_IndirectSpecular(reflectDir, brdf.perceptualRoughness, brdf.f0, NdotV);

    return indirectDiffuse + indirectSpecular;
}

// ---------------------------------------------------------------------------
// 直接光照 split 版本 — 单光源 diffuse / spec 分通道
// 与 JKPC_DirectLighting 等价：diffOut + specOut == JKPC_DirectLighting(...)
// 用于半透明 alpha 保护：specular 走 emission 通道防止被 alpha 削弱
// ---------------------------------------------------------------------------
void JKPC_DirectLighting_Split(JKPCBRDFData brdf, Light light, half3 N, half3 V,
                               out half3 diffOut, out half3 specOut)
{
    half3 L = light.direction;
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    half3 lightEnergy = light.color * atten;

    half3 brdfDiff, brdfSpec;
    DirectBRDF_Split(brdf, N, L, V, brdfDiff, brdfSpec);

    diffOut = brdfDiff * lightEnergy;
    specOut = brdfSpec * lightEnergy;
}

// ---------------------------------------------------------------------------
// 完整直接光照 split 版本 — 主光 + Forward+ 多光源 diffuse / spec 分通道
// 与 JKPC_AllDirectLighting 等价：colorDiff + colorSpec == JKPC_AllDirectLighting(...)
// 已接入 Light Layers 过滤，与原版语义一致
// ---------------------------------------------------------------------------
void JKPC_AllDirectLighting_Split(
    JKPCBRDFData brdf,
    half3 N, half3 V,
    float3 positionWS,
    float4 positionCS,
    float4 shadowCoord,
    out half3 colorDiff,
    out half3 colorSpec)
{
    colorDiff = 0;
    colorSpec = 0;

    // 主方向光
    Light mainLight = JKPC_GetMainLight(shadowCoord);
    if (JKPC_MainLightLayerTest())
    {
        half3 d, s;
        JKPC_DirectLighting_Split(brdf, mainLight, N, V, d, s);
        colorDiff += d;
        colorSpec += s;
    }

    // Forward+ Additional Lights
    uint lightCount = GetAdditionalLightsCount();
    JKPC_INIT_FORWARD_PLUS_DATA(positionWS, positionCS)
    half4 shadowMask = unity_ProbesOcclusion;
    LIGHT_LOOP_BEGIN(lightCount)
        Light addLight = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        if (!JKPC_AdditionalLightLayerTest(addLight)) continue;
        half3 d, s;
        JKPC_DirectLighting_Split(brdf, addLight, N, V, d, s);
        colorDiff += d;
        colorSpec += s;
    LIGHT_LOOP_END
}

#endif // JKPC_LIGHTING_HLSL_INCLUDED
