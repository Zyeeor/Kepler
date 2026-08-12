// =============================================================================
// JKPC - ForwardPlusLighting.hlsl
// Forward+ 专用光照工具 (适配 Tile-Based Culling)
// Layer 1 — 所有模块共享
// 规范 → 文档 §2.1.4
// =============================================================================

#ifndef JKPC_FORWARDPLUSLIGHTING_HLSL_INCLUDED
#define JKPC_FORWARDPLUSLIGHTING_HLSL_INCLUDED

// ---------------------------------------------------------------------------
// JKPC 全局开启 Light Layers（Rendering Layer Mask）
// 必须放在 include URP Lighting.hlsl 之前，确保 URP 内部宏分支也按"开启"展开。
// ---------------------------------------------------------------------------
#if !defined(_LIGHT_LAYERS)
    #define _LIGHT_LAYERS
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/Art folder/Shader/JKPC/Include/Shadow.hlsl"

// ---------------------------------------------------------------------------
// 获取主方向光
// ---------------------------------------------------------------------------
// 有 shadow keyword: 传入 shadowCoord 计算阴影衰减
// 无 shadow keyword: 直接用无参版本，shadowAttenuation=1.0，保证无 shadow 时也正常受光
Light JKPC_GetMainLight(float4 shadowCoord)
{
    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        Light mainLight = GetMainLight(shadowCoord);
    #else
        Light mainLight = GetMainLight();
    #endif
    return mainLight;
}

// ---------------------------------------------------------------------------
// Rendering Layer Mask 判断 — Light Layers (URP 14+)
// 主光：与 _MainLightLayerMask 比对
// 附加光：与 light.layerMask 比对（GetAdditionalLight 已经从 _AdditionalLightsBuffer 取出）
// 比对时机：拿到 Light 之后立即判断；不匹配则跳过该灯。
// 实现细节：渲染层在片元内通过 GetMeshRenderingLayer() 直接读 per-draw uniform，
//          不占用 interpolator，Vert / Frag 行为一致。
// ---------------------------------------------------------------------------
bool JKPC_MainLightLayerTest()
{
    uint meshRenderingLayers = GetMeshRenderingLayer();
    return IsMatchingLightLayer(_MainLightLayerMask, meshRenderingLayers);
}

bool JKPC_AdditionalLightLayerTest(Light light)
{
    uint meshRenderingLayers = GetMeshRenderingLayer();
    return IsMatchingLightLayer(light.layerMask, meshRenderingLayers);
}

// ---------------------------------------------------------------------------
// Forward+ 多光源循环  — §2.1.4
// 必须使用 LIGHT_LOOP_BEGIN/END 宏, URP Forward+ 才能正确做 Tile-Based Culling
// ---------------------------------------------------------------------------
// 用法示例:
//   uint lightCount = GetAdditionalLightsCount();
//   LIGHT_LOOP_BEGIN(lightCount)
//       Light addLight = GetAdditionalLight(lightIndex, positionWS);
//       if (!JKPC_AdditionalLightLayerTest(addLight)) continue;   // ★ Light Layers
//       color += YourLightingFunction(addLight, ...);
//   LIGHT_LOOP_END

// ---------------------------------------------------------------------------
// 额外光源衰减辅助
// ---------------------------------------------------------------------------
half JKPC_AdditionalLightAttenuation(Light light)
{
    return light.distanceAttenuation * light.shadowAttenuation;
}

#endif // JKPC_FORWARDPLUSLIGHTING_HLSL_INCLUDED
