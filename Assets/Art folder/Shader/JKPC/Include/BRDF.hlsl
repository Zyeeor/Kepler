// =============================================================================
// JKPC - BRDF.hlsl
// BRDF计算函数: Disney Diffuse, GGX NDF, Schlick Fresnel, Smith-GGX Geometry
// Layer 1 — 所有模块共享
// 算法规范 → 文档 §2.1.1
// =============================================================================

#ifndef JKPC_BRDF_HLSL_INCLUDED
#define JKPC_BRDF_HLSL_INCLUDED

#include "Assets/Art folder/Shader/JKPC/Include/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/BSDF.hlsl"
 

// 环境光版本: 增加粗糙度补偿
half3 F_SchlickRoughness(half3 f0, half NdotV, half perceptualRoughness)
{
    half3 reflectivity = max(1.0h - perceptualRoughness, f0);
    return f0 + (reflectivity - f0) * pow(1.0h - NdotV, 5.0h);
}

// ---------------------------------------------------------------------------
// Smith-GGX Height-Correlated Geometry (Visibility)  — §2.1.1
// ---------------------------------------------------------------------------
// V已包含 1/(4·NdotV·NdotL) 分母，BRDF组装时不需要再除
half V_SmithGGXCorrelated(half NdotL, half NdotV, half roughness)
{
    half a2 = JKPC_SQUARE(roughness);
    half lambdaV = NdotL * sqrt(JKPC_SQUARE(NdotV) * (1.0h - a2) + a2);
    half lambdaL = NdotV * sqrt(JKPC_SQUARE(NdotL) * (1.0h - a2) + a2);
    return 0.5h / (lambdaV + lambdaL + JKPC_EPSILON);
}

// ---------------------------------------------------------------------------
// BRDF数据结构
// ---------------------------------------------------------------------------
struct JKPCBRDFData
{
    half3 albedo;               // baseColor * (1 - metallic)
    half3 f0;                   // lerp(0.04, baseColor, metallic)
    half  metallic;
    half  perceptualRoughness;
    half  roughness;            // perceptualRoughness²
    half  ao;
};

// ---------------------------------------------------------------------------
// Roughness 安全下限 — 与 URP Lit 一致
// ---------------------------------------------------------------------------
// HALF_MIN_SQRT = 0.0078125 (2^-7)
// 当 smoothness=1 时 perceptualRoughness=0, roughness=0, 导致:
//   D_GGX 分子 a²=0 → 高光 NDF 归零, 完全丢失直射光高光
//   V_SmithGGXCorrelated 退化 → 数值不稳定
// URP Lit 在 InitializeBRDFData 中做相同 clamp (BRDF.hlsl:63)
#define JKPC_HALF_MIN_SQRT 0.0078125h

// 初始化BRDFData — Standard / Skin通用
JKPCBRDFData InitBRDFData(half3 baseColor, half metallic, half perceptualRoughness, half ao)
{
    JKPCBRDFData data;
    data.metallic = metallic;
    data.perceptualRoughness = perceptualRoughness;
    data.roughness = max(JKPC_SQUARE(perceptualRoughness), JKPC_HALF_MIN_SQRT);
    data.albedo = baseColor * (1.0h - metallic);
    data.f0 = lerp(0.04h, baseColor, metallic);
    data.ao = ao;
    return data;
}

// ---------------------------------------------------------------------------
// BRDF组装  — §2.1.2
// ---------------------------------------------------------------------------
// specular = D_GGX * F_Schlick * V_SmithGGX  (V已含分母)
half3 DirectBRDF(JKPCBRDFData brdf, half3 N, half3 L, half3 V)
{
    half3 H = SafeNormalize(L + V);
    half NdotL = saturate(dot(N, L));
    half NdotV = max(dot(N, V), JKPC_EPSILON);
    half NdotH = saturate(dot(N, H));
    half VdotH = saturate(dot(V, H));
    half LdotH = saturate(dot(L, H));

    // 漫反射
    half diffuseTerm = DisneyDiffuse(NdotL, NdotV, LdotH, brdf.perceptualRoughness);

    // 高光
    half  D = D_GGX(NdotH, brdf.roughness);
    half3 F = F_Schlick(brdf.f0, VdotH);
    half  Vis = V_SmithGGXCorrelated(NdotL, NdotV, brdf.roughness);
    half3 specular = D * F * Vis;

    return (brdf.albedo * diffuseTerm + specular) * NdotL;
}

// ---------------------------------------------------------------------------
// BRDF组装 — 漫反射/高光分离 NdotL 版本
// ---------------------------------------------------------------------------
// diffuseNdotL: 经过二值化等处理的漫反射 NdotL
// specNdotL: 原始物理 NdotL，用于高光（保持高光物理正确性）
// 供 Lighting.hlsl 在二值化漫反射时使用
half3 DirectBRDF_SeparateNdotL(JKPCBRDFData brdf, half3 N, half3 L, half3 V, half diffuseNdotL)
{
    half3 H = SafeNormalize(L + V);
    half specNdotL = saturate(dot(N, L));
    half NdotV = max(dot(N, V), JKPC_EPSILON);
    half NdotH = saturate(dot(N, H));
    half VdotH = saturate(dot(V, H));
    half LdotH = saturate(dot(L, H));

    // 漫反射 — 使用处理后的 diffuseNdotL
    half diffuseTerm = DisneyDiffuse(diffuseNdotL, NdotV, LdotH, brdf.perceptualRoughness);

    // 高光 — 使用物理 specNdotL，保持正确的高光截止
    half  D = D_GGX(NdotH, brdf.roughness);
    half3 F = F_Schlick(brdf.f0, VdotH);
    half  Vis = V_SmithGGXCorrelated(specNdotL, NdotV, brdf.roughness);
    half3 specular = D * F * Vis;

    // diffuse 乘 diffuseNdotL，specular 乘 specNdotL
    return brdf.albedo * diffuseTerm * diffuseNdotL + specular * specNdotL;
}

// ---------------------------------------------------------------------------
// BRDF 组装 — diffuse / specular 分通道输出（半透明 alpha 保护用）
// ---------------------------------------------------------------------------
// 与 DirectBRDF 完全等价：diff + spec == DirectBRDF(...)
// 但分两路返回，让上层（半透明 alpha 预乘 / specular 保护）能独立处理
void DirectBRDF_Split(JKPCBRDFData brdf, half3 N, half3 L, half3 V,
                      out half3 diffuseOut, out half3 specularOut)
{
    half3 H = SafeNormalize(L + V);
    half NdotL = saturate(dot(N, L));
    half NdotV = max(dot(N, V), JKPC_EPSILON);
    half NdotH = saturate(dot(N, H));
    half VdotH = saturate(dot(V, H));
    half LdotH = saturate(dot(L, H));

    half diffuseTerm = DisneyDiffuse(NdotL, NdotV, LdotH, brdf.perceptualRoughness);

    half  D = D_GGX(NdotH, brdf.roughness);
    half3 F = F_Schlick(brdf.f0, VdotH);
    half  Vis = V_SmithGGXCorrelated(NdotL, NdotV, brdf.roughness);
    half3 specular = D * F * Vis;

    diffuseOut  = brdf.albedo * diffuseTerm * NdotL;
    specularOut = specular * NdotL;
}

#endif // JKPC_BRDF_HLSL_INCLUDED
