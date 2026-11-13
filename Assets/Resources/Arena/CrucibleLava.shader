// Molten lava for the Crucible's fire pits (Arena.DestructibleFloor). Unlit and fully procedural: domain-warped noise in
// world space (so neighbouring pits flow into each other as one molten patch), with bright churning veins over a dark
// red base. _Heat drives the state: 2 = white-hot (the pit has just opened), 1 = burning, 0 = crusted over.
// Lives in Resources (with CrucibleLava.mat) so builds always include it.
Shader "Anathema/CrucibleLava"
{
    Properties
    {
        _Hot ("Hot", Color) = (1, 0.88, 0.45, 1)
        _Mid ("Mid", Color) = (1, 0.36, 0.03, 1)
        _Dark ("Dark", Color) = (0.38, 0.04, 0.01, 1)
        _Crust ("Crust", Color) = (0.09, 0.035, 0.025, 1)
        _Scale ("Scale", Float) = 0.75
        _Speed ("Speed", Float) = 0.3
        _Heat ("Heat", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Hot, _Mid, _Dark, _Crust;
            float _Scale, _Speed, _Heat;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xz;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * noise(p);
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 p = i.world * _Scale;

                // Domain warp: the pattern churns and drifts rather than just sliding.
                float2 warp = float2(fbm(p + float2(t, 0.3 * t)), fbm(p + float2(-0.4 * t, t) + 5.2));
                float n = fbm(p * 1.3 + warp * 2.2 - t * 0.5);

                // Veins: thin bright bands where the noise crosses its middle.
                float vein = 1.0 - saturate(abs(n - 0.5) * 7.0);
                vein = vein * vein;
                float pulse = 0.85 + 0.15 * sin(_Time.y * 3.0 + n * 9.0);

                float3 col = lerp(_Dark.rgb, _Mid.rgb, saturate(n * 1.8 - 0.25));
                col = lerp(col, _Hot.rgb, vein);
                col *= pulse;

                // Cooling: a dark crust with the veins dimming out last. Opening: a white-hot flash.
                float3 crust = _Crust.rgb + _Mid.rgb * vein * 0.5 * saturate(_Heat * 2.0);
                col = lerp(crust, col, saturate(_Heat));
                col = lerp(col, _Hot.rgb, saturate(_Heat - 1.0) * 0.85);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
