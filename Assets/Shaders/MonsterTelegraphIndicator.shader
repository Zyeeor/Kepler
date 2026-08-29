Shader "Possession/MonsterTelegraphIndicator"
{
    Properties
    {
        [HDR] _IndicatorColor ("Indicator Color", Color) = (1, 0.02, 0.01, 1)
        _IndicatorIntensity ("Indicator Intensity", Range(0, 5)) = 1.1
        _IndicatorProgress ("Cast Progress", Range(0, 1)) = 0
        _RingWidth ("Ring Width", Range(0.01, 0.2)) = 0.045
        _ShapeType ("Shape Type (0=Circle 1=Rect 2=Sector)", Float) = 0
        _SectorAngle ("Sector Angle (degrees)", Float) = 100
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _IndicatorColor;
                half _IndicatorIntensity;
                half _IndicatorProgress;
                half _RingWidth;
                half _ShapeType;
                half _SectorAngle;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float SoftBand(float value, float center, float width)
            {
                return 1.0 - smoothstep(width, width * 2.0, abs(value - center));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uvPoint = input.uv * 2.0 - 1.0;
                float progress = saturate(_IndicatorProgress);

                // ── Sector（前方扇形预警）：从原点沿 +X（forward）展开 _SectorAngle 度 ──
                if (_ShapeType > 1.5)
                {
                    float distS = length(uvPoint);
                    float angS = atan2(uvPoint.y, uvPoint.x);   // -π ~ π，+X 为 0
                    float halfAngleS = radians(_SectorAngle) * 0.5;
                    float angAbsS = abs(angS);

                    // 圆弧边：仅扇形角度内显示，否则会在 distS=0.84 处形成一圈完整圆环（脚下红圈 bug）。
                    float arcMask = step(angAbsS, halfAngleS);
                    float edgeArc = SoftBand(distS, 0.84, _RingWidth) * arcMask;
                    // 两侧直线边：在 angAbsS≈halfAngleS 处，仅半径内显示。
                    float sideWidth = radians(max(_SectorAngle, 1.0)) * 0.06;
                    float edgeSide = SoftBand(angAbsS, halfAngleS, sideWidth) * step(distS, 0.84);
                    float edge = max(edgeArc, edgeSide);

                    float insideS = (distS <= 0.84 && angAbsS <= halfAngleS) ? 1.0 : 0.0;
                    float angNorm = angAbsS / max(0.001, halfAngleS);
                    float spokesS = pow(saturate(cos(angNorm * 8.0) * 0.5 + 0.5), 6.0);
                    float sweepS = 1.0 - smoothstep(progress - 0.07, progress + 0.07, distS);
                    float innerPatternS = insideS * (spokesS * (0.08 + progress * 0.4) + sweepS * 0.24);
                    float brightnessS = lerp(0.22, 1.15, progress) * (0.84 + 0.16 * sin(_Time.y * 9.0));
                    float alphaS = saturate((edge * 1.3 + innerPatternS) * brightnessS * _IndicatorColor.a);
                    return half4(_IndicatorColor.rgb * _IndicatorIntensity * brightnessS, alphaS);
                }

                // ── Rect（直线预警带）：长度沿 +X，宽度沿 +Y ──
                if (_ShapeType > 0.5)
                {
                    float2 box = abs(uvPoint);
                    float edgeX = SoftBand(box.x, 0.84, _RingWidth);
                    float edgeY = SoftBand(box.y, 0.84, _RingWidth);
                    float edge = max(edgeX, edgeY);
                    float inner = saturate(1.0 - max(box.x, box.y) / 0.84);
                    float ticks = pow(saturate(cos(uvPoint.x * 15.0) * 0.5 + 0.5), 12.0);
                    float sweepX = uvPoint.x * 0.5 + 0.5;
                    float sweep = 1.0 - smoothstep(progress - 0.07, progress + 0.07, sweepX);
                    float innerPattern = inner * (ticks * (0.08 + progress * 0.5) + sweep * 0.24);
                    float brightness = lerp(0.22, 1.15, progress) * (0.84 + 0.16 * sin(_Time.y * 9.0));
                    float alpha = saturate((edge * 1.3 + innerPattern) * brightness * _IndicatorColor.a);
                    return half4(_IndicatorColor.rgb * _IndicatorIntensity * brightness, alpha);
                }

                // ── Circle（原有圆形逻辑） ──
                float radius = length(uvPoint);
                clip(0.99 - radius);

                float edgeC = SoftBand(radius, 0.84, _RingWidth);
                float edgeGlow = exp(-abs(radius - 0.84) * 34.0) * 0.45;
                float innerFade = saturate(1.0 - radius / 0.84);

                float angle = atan2(uvPoint.y, uvPoint.x) / 6.2831853 + 0.5;
                angle = frac(angle + _Time.y * 0.035);
                float spokes = pow(saturate(cos(angle * 37.6991) * 0.5 + 0.5), 18.0);
                float rings = SoftBand(radius, 0.38 + 0.18 * sin(_Time.y * 2.0), 0.012);
                float sweepC = 1.0 - smoothstep(progress - 0.08, progress + 0.08, angle);
                float innerPatternC = innerFade * (spokes * (0.10 + progress * 0.75)
                    + rings * (0.12 + progress * 0.45)
                    + sweepC * 0.16);

                float brightnessC = lerp(0.22, 1.15, progress);
                brightnessC *= 0.84 + 0.16 * sin(_Time.y * 9.0);
                float alphaC = saturate((edgeC * 1.25 + edgeGlow + innerPatternC) * brightnessC * _IndicatorColor.a);
                return half4(_IndicatorColor.rgb * _IndicatorIntensity * brightnessC, alphaC);
            }
            ENDHLSL
        }
    }
}
