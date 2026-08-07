// =============================================================================
// JKPC - Input.hlsl
// 通用输入结构体: Attributes, Varyings
// Layer 1 — 所有模块共享
// 规范 → 文档 §4.4
// =============================================================================

#ifndef JKPC_INPUT_HLSL_INCLUDED
#define JKPC_INPUT_HLSL_INCLUDED

#include "Assets/Art folder/Shader/JKPC/Include/Common.hlsl"
// 顶点着色器需要: TransformWorldToShadowCoord → 由 Shadow.hlsl 引入 URP Shadows.hlsl
#include "Assets/Art folder/Shader/JKPC/Include/Shadow.hlsl"

// ---------------------------------------------------------------------------
// Attributes — 顶点输入
// ---------------------------------------------------------------------------
struct JKPCAttributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 lightmapUV   : TEXCOORD1;
    half4  color         : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// ---------------------------------------------------------------------------
// Varyings — 顶点到片元
// ---------------------------------------------------------------------------
struct JKPCVaryings
{
    float4 positionCS       : SV_POSITION;
    float2 uv               : TEXCOORD0;
    float3 positionWS       : TEXCOORD1;
    half3  normalWS         : TEXCOORD2;
    half4  tangentWS        : TEXCOORD3;    // xyz = tangent, w = sign
    float2 lightmapUV       : TEXCOORD4;
    float4 shadowCoord      : TEXCOORD5;
    half   fogCoord         : TEXCOORD6;
    half4  color            : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Hair 额外需要 bitangentWS
struct JKPCVaryingsHair
{
    float4 positionCS       : SV_POSITION;
    float2 uv               : TEXCOORD0;
    float3 positionWS       : TEXCOORD1;
    half3  normalWS         : TEXCOORD2;
    half4  tangentWS        : TEXCOORD3;    // xyz = tangent, w = sign
    half3  bitangentWS      : TEXCOORD4;    // ★ Hair专有: 毛发朝向
    float2 lightmapUV       : TEXCOORD5;
    float4 shadowCoord      : TEXCOORD6;
    half   fogCoord         : TEXCOORD7;
    float2 uv2              : TEXCOORD8;    // ★ Hair专有: 纯净的顶点 UV2（不乘 unity_LightmapST）
    half4  color            : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// ---------------------------------------------------------------------------
// 通用顶点着色器 — Standard / Skin / Hero / Map 共用
// ---------------------------------------------------------------------------
JKPCVaryings JKPC_Vert(JKPCAttributes input)
{
    JKPCVaryings output = (JKPCVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = posInputs.positionCS;
    output.uv = input.texcoord;
    output.positionWS = posInputs.positionWS;
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w);
    output.color = input.color;
    output.lightmapUV = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    // shadow coord: 有 shadow keyword 时才计算，否则传 (0,0,0,0)
    // GetMainLight(shadowCoord) 在无 shadow keyword 时直接返回 shadowAttenuation=1，不使用这个 coord
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        output.shadowCoord = GetShadowCoord(posInputs);
    #else
        output.shadowCoord = float4(0, 0, 0, 0);
    #endif
    output.fogCoord = ComputeFogFactor(posInputs.positionCS.z);

    return output;
}

// ---------------------------------------------------------------------------
// Hair 顶点着色器 — 额外计算 bitangentWS
// ---------------------------------------------------------------------------
JKPCVaryingsHair JKPC_VertHair(JKPCAttributes input)
{
    JKPCVaryingsHair output = (JKPCVaryingsHair)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = posInputs.positionCS;
    output.uv = input.texcoord;
    output.positionWS = posInputs.positionWS;
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w);
    output.bitangentWS = normalInputs.bitangentWS;
    output.color = input.color;
    output.lightmapUV = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    output.uv2 = input.lightmapUV;          // 纯净 UV2，供 Hair Alpha Map 或其他 UV2 需求使用
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        output.shadowCoord = GetShadowCoord(posInputs);
    #else
        output.shadowCoord = float4(0, 0, 0, 0);
    #endif
    output.fogCoord = ComputeFogFactor(posInputs.positionCS.z);

    return output;
}

#endif // JKPC_INPUT_HLSL_INCLUDED
