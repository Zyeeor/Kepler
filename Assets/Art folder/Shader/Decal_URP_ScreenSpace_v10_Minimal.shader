// Minimal screen-space volume decal for URP.
// Purpose: validate renderer submission, camera depth and world-position reconstruction
// before adding lighting, normal, mask or emission features.
Shader "Custom/Decal/ScreenSpaceDecal_v10_Minimal"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Enum(Final,0,PassMagenta,1,RawDepth,2,LocalPosition,3)]
        _DebugView ("Debug View", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "ScreenSpaceDecalMinimal"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _DebugView;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // This branch runs before depth sampling and box clipping.
                // If it is invisible, the renderer did not submit this pass,
                // or the projector cube/layer/queue is not being drawn.
                if (_DebugView > 0.5h && _DebugView < 1.5h)
                    return half4(1, 0, 1, 0.75h);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS.xy);
                float rawDepth = SampleSceneDepth(screenUV);

                if (_DebugView > 1.5h && _DebugView < 2.5h)
                    return half4(rawDepth.xxx, 1);

                // Reject sky/background before reconstructing world position.
                #if UNITY_REVERSED_Z
                    clip(rawDepth - 1e-6);
                    float deviceDepth = rawDepth;
                #else
                    clip(1.0 - rawDepth - 1e-6);
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 worldPos = ComputeWorldSpacePosition(
                    screenUV,
                    deviceDepth,
                    UNITY_MATRIX_I_VP
                );
                float3 localPos = mul(unity_WorldToObject, float4(worldPos, 1.0)).xyz;

                if (_DebugView > 2.5h)
                    return half4(saturate(localPos + 0.5), 1);

                // Projector mesh must be Unity's centered unit cube: [-0.5, 0.5].
                float boxDistance = 0.5 - max(
                    abs(localPos.x),
                    max(abs(localPos.y), abs(localPos.z))
                );
                clip(boxDistance);

                float2 decalUV = localPos.xy + 0.5;
                decalUV = decalUV * _BaseMap_ST.xy + _BaseMap_ST.zw;

                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, decalUV) * _BaseColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
