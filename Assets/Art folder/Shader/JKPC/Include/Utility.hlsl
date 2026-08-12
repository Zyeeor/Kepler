// =============================================================================
// JKPC - Utility.hlsl
// 工具函数: 纹理采样、坐标变换、颜色转换
// Layer 1 — 所有模块共享
// =============================================================================

#ifndef JKPC_UTILITY_HLSL_INCLUDED
#define JKPC_UTILITY_HLSL_INCLUDED

#include "Assets/Art folder/Shader/JKPC/Include/Common.hlsl"

// ---------------------------------------------------------------------------
// 纹理采样辅助
// ---------------------------------------------------------------------------
// 带ST变换的纹理采样
#define JKPC_SAMPLE_TEXTURE2D_ST(tex, samp, uv, st) \
    SAMPLE_TEXTURE2D(tex, samp, (uv) * (st).xy + (st).zw)

// 法线贴图解包 (BC5: RG通道, B通道运行时重建)
half3 UnpackNormalBC5(half4 packedNormal, half normalScale)
{
    half3 normal;
    normal.xy = packedNormal.rg * 2.0h - 1.0h;
    normal.xy *= normalScale;
    normal.z = sqrt(saturate(1.0h - dot(normal.xy, normal.xy)));
    return normal;
}

// ---------------------------------------------------------------------------
// 坐标变换辅助
// ---------------------------------------------------------------------------
// 切线空间法线转世界空间
half3 TransformTangentToWorld_JKPC(half3 normalTS, half3 tangentWS, half3 bitangentWS, half3 normalWS)
{
    return normalize(
        normalTS.x * tangentWS +
        normalTS.y * bitangentWS +
        normalTS.z * normalWS
    );
}

// 计算世界空间副切线
half3 ComputeBitangent(half3 normalWS, half4 tangentWS)
{
    return cross(normalWS, tangentWS.xyz) * tangentWS.w;
}

// ---------------------------------------------------------------------------
// 反射方向
// ---------------------------------------------------------------------------
half3 ReflectDirection(half3 viewDirWS, half3 normalWS)
{
    return reflect(-viewDirWS, normalWS);
}

// ---------------------------------------------------------------------------
// 屏幕空间UV
// ---------------------------------------------------------------------------
float2 ComputeScreenUV(float4 positionCS)
{
    float2 uv = positionCS.xy / _ScaledScreenParams.xy;
    return uv;
}

#endif // JKPC_UTILITY_HLSL_INCLUDED
