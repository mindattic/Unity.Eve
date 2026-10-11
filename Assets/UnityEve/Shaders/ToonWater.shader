// Cel-shaded water for the park fountain: a flat banded tint (not photographic reflection), a gentle
// vertex ripple, and a scrolling toon sparkle standing in for broken specular highlights on moving
// water. No camera-depth sampling or refraction: this is a small decorative pool, not an ocean system.
Shader "UnityEve/ToonWater"
{
    Properties
    {
        _BaseColor ("Deep water", Color) = (0.12, 0.32, 0.38, 0.85)
        _FoamColor ("Shallow / foam", Color) = (0.55, 0.85, 0.85, 0.9)
        _Bands ("Sparkle bands", Range(1, 4)) = 2
        _WaveHeight ("Ripple height", Range(0, 0.1)) = 0.03
        _WaveFrequency ("Ripple frequency", Range(0.5, 6)) = 2.2
        _WaveSpeed ("Ripple speed", Range(0, 4)) = 1.4
        _SparkleScale ("Sparkle tiling", Range(1, 40)) = 14
        _SparkleSpeed ("Sparkle scroll speed", Range(0, 2)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ToonWaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FoamColor;
                half _Bands;
                half _WaveHeight;
                half _WaveFrequency;
                half _WaveSpeed;
                half _SparkleScale;
                half _SparkleSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 posOS = v.positionOS.xyz;
                float3 posWS = TransformObjectToWorld(posOS);
                // Two overlapping sine ripples so the surface never looks like it is bobbing as one slab.
                half t = _Time.y * _WaveSpeed;
                half ripple = sin((posWS.x + posWS.z) * _WaveFrequency + t) + sin((posWS.x - posWS.z) * _WaveFrequency * 0.7h - t * 1.3h);
                posOS.y += ripple * _WaveHeight;
                VertexPositionInputs p = GetVertexPositionInputs(posOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Quantise into flat steps, same cel language as the opaque Toon shader.
            float Band(float x)
            {
                float f = floor(x * _Bands + 0.5h) / _Bands;
                return f;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i)
                half3 n = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                half4 shadowMask = half4(1, 1, 1, 1);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS), i.positionWS, shadowMask);
                half lit = saturate(dot(n, mainLight.direction)) * mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // Two scrolling diagonal streaks of "broken" specular, banded into flat toon glints.
                half t = _Time.y * _SparkleSpeed;
                half streakA = Band(frac(sin((i.uv.x + i.uv.y + t) * _SparkleScale) * 43758.5h));
                half streakB = Band(frac(sin((i.uv.x - i.uv.y - t * 1.4h) * _SparkleScale * 1.3h) * 12543.2h));
                half sparkle = saturate(streakA * streakB * 2.0h - 0.6h) * saturate(lit * 2.0h);

                // Fresnel: steep angles (the pool's near edge, the basin rim) read as shallow/foam.
                half fresnel = pow(1.0h - saturate(dot(n, v)), 2.0h);
                half3 colour = lerp(_BaseColor.rgb, _FoamColor.rgb, saturate(fresnel + sparkle));
                colour *= 0.55h + 0.45h * lit;
                colour += mainLight.color * sparkle * 0.5h;
                colour = MixFog(colour, i.fogFactor);
                half alpha = lerp(_BaseColor.a, _FoamColor.a, fresnel);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
