Shader "VFX/PandaPostV1.0"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "black" {}
        _centerU ("centerU", Range(0, 1)) = 0.5
        _centerV ("centerV", Range(0, 1)) = 0.5
        [HDR]_Color1 ("Color1", Color) = (1, 1, 1, 0)
        [HDR]_Color2 ("Color2", Color) = (0, 0, 0, 0)
        _LineTilingU ("LineTilingU", Range(0, 5)) = 2
        _LineTilingV ("LineTilingV", Range(1, 20)) = 8
        _LineUVScale ("LineUVScale", Range(0, 4)) = 0
        _LineUVScaleK ("LineUVScaleK", Float) = 0
        _LineColorScale ("LineColorScale", Range(-1, 3)) = 0
        _BlurFactor ("BlurFactor", Range(0, 1)) = 0
        _BlurFactorK ("BlurFactorK", Float) = 0
        _Soft ("Soft", Range(0, 1)) = 0.5
        _StepFactor ("StepFactor", Range(0, 2)) = 0.6
        _StepFactorK ("StepFactorK", Float) = 0
        _Logo ("Logo", 2D) = "white" {}
        _Tex ("Tex", 2D) = "white" {}
        _TexRotator ("TexRotator", Range(0, 1)) = 0.075
        _TexAlpha ("TexAlpha", Range(0, 1)) = 0.07
        _VignettePowerK ("VignettePowerK", Float) = 1.5
        _VignettePower ("VignettePower", Range(1, 3)) = 1.5
        _VignetteScale ("VignetteScale", Range(0, 3)) = 1.5
        _VignetteScaleK ("VignetteScaleK", Float) = 1.5
        _MainAlpha ("MainAlpha", Range(0, 1)) = 1
        _MainAlphaK ("MainAlphaK", Float) = 1
        [Toggle]_IfMainAlpha ("IfMainAlpha", Float) = 0
        [Toggle]_IfStepFactor ("IfStepFactor", Float) = 0
        [Toggle]_IfLineUVScale ("IfLineUVScale", Float) = 0
        [Toggle]_IfBlurFactor ("IfBlurFactor", Float) = 0
        _LogoAlpha ("LogoAlpha", Range(0, 1)) = 0.2
        [HideInInspector]_Logo_ST ("Logo_ST", Vector) = (3, 3, 0, 0.1)
        [Toggle]_LogoAR ("LogoAR", Float) = 0
        _LineOffset ("LineOffset", Range(0, 5)) = 0
        _RedBlueFactorK ("RedBlueFactorK", Float) = 0
        _RedBlueFactor ("RedBlueFactor", Range(0, 1.5)) = 0
        [KeywordEnum(Normal, BlackWhiteFlash, ColorReverse)]_ColorStyle ("ColorStyle", Float) = 0
        _zhenfuK ("zhenfuK", Float) = 0
        _zhenfu ("zhenfu", Range(0, 1)) = 0
        [Toggle]_IfRedBlueFactor ("IfRedBlueFactor", Float) = 0
        _zhenpinK ("zhenpinK", Float) = 0
        _zhenpin ("zhenpin", Range(0, 1)) = 0
        [Toggle]_Ifzhenpin ("Ifzhenpin", Float) = 0
        [Toggle]_Ifzhenfu ("Ifzhenfu", Float) = 0
        [HideInInspector]_Tex_ST ("Tex_ST", Vector) = (300, 300, 0, 0)
        [Toggle]_IfVignetteScale ("IfVignetteScale", Float) = 0
        [Toggle]_IfVignettePower ("IfVignettePower", Float) = 0
        [Toggle]_TexAR ("TexAR", Float) = 1
        [HideInInspector]_texcoord ("", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PandaPostURP"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _COLORSTYLE_NORMAL _COLORSTYLE_BLACKWHITEFLASH _COLORSTYLE_COLORREVERSE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_Tex);
            SAMPLER(sampler_Tex);
            TEXTURE2D(_Logo);
            SAMPLER(sampler_Logo);

            CBUFFER_START(UnityPerMaterial)
                float _centerU;
                float _centerV;
                float4 _Color1;
                float4 _Color2;
                float _LineTilingU;
                float _LineTilingV;
                float _LineUVScale;
                float _LineUVScaleK;
                float _LineColorScale;
                float _BlurFactor;
                float _BlurFactorK;
                float _Soft;
                float _StepFactor;
                float _StepFactorK;
                float4 _Tex_ST;
                float _TexRotator;
                float _TexAlpha;
                float _VignettePowerK;
                float _VignettePower;
                float _VignetteScale;
                float _VignetteScaleK;
                float _MainAlpha;
                float _MainAlphaK;
                float _IfMainAlpha;
                float _IfStepFactor;
                float _IfLineUVScale;
                float _IfBlurFactor;
                float _LogoAlpha;
                float4 _Logo_ST;
                float _LogoAR;
                float _LineOffset;
                float _RedBlueFactorK;
                float _RedBlueFactor;
                float _ColorStyle;
                float _zhenfuK;
                float _zhenfu;
                float _IfRedBlueFactor;
                float _zhenpinK;
                float _zhenpin;
                float _Ifzhenpin;
                float _Ifzhenfu;
                float _IfVignetteScale;
                float _IfVignettePower;
                float _TexAR;
            CBUFFER_END

            float4 SampleSource(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }

            float2 VoronoiHash(float2 hashInput)
            {
                float2 hashValue = float2(
                    dot(hashInput, float2(127.1, 311.7)),
                    dot(hashInput, float2(269.5, 183.3)));
                return frac(sin(hashValue) * 43758.5453);
            }

            float Voronoi(float2 value, float timeValue)
            {
                float2 cell = floor(value);
                float2 localValue = frac(value);
                float nearestDistance = 8.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbour = float2(x, y);
                        float2 offset = VoronoiHash(cell + neighbour);
                        offset = sin(timeValue + offset * 6.2831) * 0.5 + 0.5;
                        float2 difference = localValue - neighbour - offset;
                        nearestDistance = min(nearestDistance, 0.5 * dot(difference, difference));
                    }
                }

                return nearestDistance;
            }

            float2 PolarCoordinates(float2 uv, float2 center, float2 tiling)
            {
                float2 centeredUv = uv - center;
                return float2(
                    length(centeredUv) * tiling.x * 2.0,
                    atan2(centeredUv.x, centeredUv.y) * (1.0 / TWO_PI) * tiling.y);
            }

            float3 ApplyColorStyle(float3 sourceColor, float proceduralMask, float stepFactor)
            {
                #if defined(_COLORSTYLE_BLACKWHITEFLASH)
                    float3 preparedColor = saturate(proceduralMask + sourceColor);
                    float grayscale = dot(preparedColor, float3(0.299, 0.587, 0.114));
                    float threshold = smoothstep(stepFactor, stepFactor - _Soft, grayscale);
                    return saturate(lerp(_Color1.rgb, _Color2.rgb, threshold));
                #elif defined(_COLORSTYLE_COLORREVERSE)
                    return 1.0 - saturate(sourceColor);
                #else
                    return sourceColor;
                #endif
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUv = input.texcoord;
                float radialDistance = length(screenUv - 0.5);

                float vignettePower = _IfVignettePower == 0.0
                    ? _VignettePower
                    : _VignettePowerK;
                float vignetteScale = _IfVignetteScale == 0.0
                    ? _VignetteScale
                    : _VignetteScaleK;
                float vignette = 1.0 - saturate(
                    pow(radialDistance, vignettePower) * vignetteScale);

                float2 radialCenter = 1.0 - float2(_centerU, _centerV);
                float shakeFrequency = (_Ifzhenpin == 0.0 ? _zhenpin : _zhenpinK) * 60.0;
                float shakeAmplitude = (_Ifzhenfu == 0.0 ? _zhenfu : _zhenfuK) * 0.05;
                float2 shakeOffset = float2(
                    cos(_Time.y * shakeFrequency) * shakeAmplitude,
                    sin(_Time.y * shakeFrequency * 0.7) * shakeAmplitude);
                float2 animatedCenter = radialCenter + shakeOffset;

                float2 polarTilingA = float2(2.0, 50.0) * float2(_LineTilingU, _LineTilingV);
                float2 polarTilingB = float2(_LineTilingU, _LineTilingV) * float2(1.0, 100.0);
                float2 polarA = PolarCoordinates(screenUv, animatedCenter, polarTilingA) + float2(_LineOffset, 0.0);
                float2 polarB = PolarCoordinates(screenUv, animatedCenter, polarTilingB) + float2(_LineOffset, 0.0);

                float distortionA = Voronoi(polarA * 0.7, 0.2);
                float distortionB = Voronoi(polarB * 1.1, 0.0);
                float lineUvScale = _IfLineUVScale == 0.0
                    ? _LineUVScale
                    : _LineUVScaleK;
                float distortionWeight = pow(1.0 - radialDistance, 3.0) *
                    distortionA * distortionB * lineUvScale;
                float2 distortedUv = lerp(screenUv, animatedCenter, distortionWeight);

                float2 baseUv = distortedUv - shakeOffset;
                float chromatic = (_IfRedBlueFactor == 0.0
                    ? _RedBlueFactor
                    : _RedBlueFactorK) * 0.1;
                float halfChromatic = chromatic * 0.5;

                float2 redUv = distortedUv * (1.0 + chromatic) -
                    (float2(chromatic * 0.5, chromatic * 0.5) + shakeOffset);
                float2 blueUv = distortedUv * (1.0 - chromatic) -
                    (float2(-chromatic * 0.5, -chromatic * 0.5) + shakeOffset);
                float2 halfRedUv = distortedUv * (1.0 + halfChromatic) -
                    (float2(halfChromatic * 0.5, halfChromatic * 0.5) + shakeOffset);
                float2 halfBlueUv = distortedUv * (1.0 - halfChromatic) -
                    (float2(-halfChromatic * 0.5, -halfChromatic * 0.5) + shakeOffset);

                float3 baseBlur = 0.0;
                float3 redBlur = 0.0;
                float3 blueBlur = 0.0;
                float3 halfRedBlur = 0.0;
                float3 halfBlueBlur = 0.0;
                const int sampleCount = 30;
                float blurStep = (_IfBlurFactor == 0.0
                    ? _BlurFactor
                    : _BlurFactorK) * 0.0006;
                float2 blurDirection = baseUv - radialCenter;

                [loop]
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float sampleOffset = blurStep * sampleIndex;
                    baseUv -= sampleOffset * blurDirection;
                    redUv -= sampleOffset * blurDirection;
                    blueUv -= sampleOffset * blurDirection;
                    halfRedUv -= sampleOffset * blurDirection;
                    halfBlueUv -= sampleOffset * blurDirection;

                    baseBlur += SampleSource(baseUv).rgb;
                    redBlur += SampleSource(redUv).rgb;
                    blueBlur += SampleSource(blueUv).rgb;
                    halfRedBlur += SampleSource(halfRedUv).rgb;
                    halfBlueBlur += SampleSource(halfBlueUv).rgb;
                }

                baseBlur /= sampleCount;
                redBlur /= sampleCount;
                blueBlur /= sampleCount;
                halfRedBlur /= sampleCount;
                halfBlueBlur /= sampleCount;

                float detailA = Voronoi(polarA * 0.5, 0.3);
                float detailB = Voronoi(polarB, 0.1);
                float proceduralMask = _LineColorScale *
                    detailA * detailB * pow(radialDistance, 0.1);
                float stepFactor = _IfStepFactor == 0.0
                    ? _StepFactor
                    : _StepFactorK;

                float3 styledBase = ApplyColorStyle(baseBlur, proceduralMask, stepFactor);
                float3 styledRed = ApplyColorStyle(redBlur, proceduralMask, stepFactor);
                float3 styledBlue = ApplyColorStyle(blueBlur, proceduralMask, stepFactor);
                float3 styledHalfRed = ApplyColorStyle(halfRedBlur, proceduralMask, stepFactor);
                float3 styledHalfBlue = ApplyColorStyle(halfBlueBlur, proceduralMask, stepFactor);

                float3 fullChromaticColor = float3(
                    styledRed.r,
                    styledBase.g,
                    styledBlue.b);
                float3 halfChromaticColor = float3(
                    styledHalfRed.r,
                    styledBase.g,
                    styledHalfBlue.b);
                float3 effectColor = lerp(fullChromaticColor, halfChromaticColor, 0.7);

                float mainAlpha = _IfMainAlpha == 0.0
                    ? _MainAlpha
                    : _MainAlphaK;
                float4 result = lerp(
                    SampleSource(screenUv),
                    float4(effectColor, 0.0),
                    mainAlpha);

                float textureAngle = _TexRotator * TWO_PI;
                float textureSin;
                float textureCos;
                sincos(textureAngle, textureSin, textureCos);
                float2 textureUv = screenUv * _Tex_ST.xy + _Tex_ST.zw;
                textureUv = mul(
                    textureUv,
                    float2x2(textureCos, -textureSin, textureSin, textureCos));
                float4 textureSample = SAMPLE_TEXTURE2D(_Tex, sampler_Tex, textureUv);
                float textureValue = _TexAR == 0.0
                    ? textureSample.a
                    : textureSample.r;
                result = lerp(
                    vignette * result,
                    textureValue.xxxx,
                    _TexAlpha);

                float2 logoUv = screenUv * _Logo_ST.xy + _Logo_ST.zw;
                logoUv.x -= _Logo_ST.x - 1.0;
                logoUv = clamp(logoUv, 0.0, 1.0);
                float4 logoSample = SAMPLE_TEXTURE2D(_Logo, sampler_Logo, logoUv);
                float logoValue = _LogoAR == 0.0
                    ? logoSample.a
                    : logoSample.r;
                result = lerp(
                    result,
                    logoValue.xxxx,
                    saturate(logoValue * _LogoAlpha));

                return result;
            }
            ENDHLSL
        }
    }

    Fallback Off
    CustomEditor "PostGUI"
}
