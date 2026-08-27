Shader "UI/PossessionImprintMeteor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _MeteorColor ("Meteor Color", Color) = (0.25, 0.85, 1, 1)
        _Progress ("Flight Progress", Range(0, 1)) = 0
        _TailLength ("Tail Length", Range(0.08, 0.75)) = 0.34
        _Glow ("Glow", Float) = 1.8
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MeteorColor;
            float _Progress;
            float _TailLength;
            float _Glow;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float progress = saturate(_Progress);
                float tailLength = max(0.08, _TailLength);
                float behindHead = progress - uv.x;
                float insidePath = smoothstep(0.0, 0.035, behindHead);
                float tailFalloff = exp(-max(0.0, behindHead) / tailLength * 4.5) * insidePath;

                float centerDistance = abs(uv.y - 0.5);
                float aura = exp(-centerDistance * 7.5);
                float core = exp(-centerDistance * 34.0);
                float headDistance = abs(uv.x - progress);
                float head = exp(-headDistance * headDistance * 950.0);
                float flare = exp(-headDistance * 65.0) * exp(-centerDistance * 3.5);

                float sparkBand = saturate(sin((uv.x * 78.0) + (_Time.y * 9.0)) * 0.5 + 0.5);
                float sparkNoise = Hash21(float2(floor(uv.x * 38.0), floor(uv.y * 7.0)));
                float sparks = tailFalloff * step(0.72, sparkBand) * step(0.78, sparkNoise) * exp(-centerDistance * 15.0);

                float tail = tailFalloff * (aura * 0.75 + core * 0.85);
                float headGlow = head * (aura * 1.8 + core * 2.8) + flare * 0.95 + sparks * 1.5;
                float alpha = saturate((tail + headGlow) * max(0.0, _Glow));
                alpha *= tex2D(_MainTex, uv).a * i.color.a * _MeteorColor.a;

                float3 color = _MeteorColor.rgb * alpha;
                return float4(color, alpha);
            }
            ENDCG
        }
    }
}
