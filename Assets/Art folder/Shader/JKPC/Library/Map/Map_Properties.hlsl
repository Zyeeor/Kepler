// =============================================================================
// Map_Properties.hlsl
// CBUFFER + Texture 声明 — 所有 Pass 共用，保证 SRP Batcher 兼容
// =============================================================================
#ifndef JKPC_MAP_PROPERTIES_HLSL
#define JKPC_MAP_PROPERTIES_HLSL

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _NormalMap_ST;
    float4 _MetallicGlossMap_ST;
    float4 _EmissionMap_ST;
    half4  _BaseColor;
    half   _NormalScale;
    half   _Metallic;
    half   _Smoothness;
    half   _AOIntensity;

    half   _EmissionEnabled;
    half4  _EmissionColor;
    half   _EmissionIntensity;

    half   _CullMode;


    half   _ZWrite;
    half   _QueueOffset;
    half   _Surface;
    half   _Blend;
    half   _SrcBlend;
    half   _DstBlend;
    half   _Cutoff;
CBUFFER_END

TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap);       SAMPLER(sampler_NormalMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_EmissionMap);     SAMPLER(sampler_EmissionMap);

// URP 内置 Pass 桥接函数
#include "Assets/Art folder/Shader/JKPC/Include/BuiltinPassBridge.hlsl"

#endif // JKPC_MAP_PROPERTIES_HLSL
