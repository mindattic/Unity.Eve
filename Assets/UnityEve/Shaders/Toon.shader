// Unity Eve cel shader: banded light from the moon and every point light, a flat ambient fill, a thin
// rim, and an inverted-hull ink outline sized in render-target pixels (so it stays 1 px on the 400x300
// PS1 target). Keeps URP Lit's property names (_BaseMap, _BaseColor, _EmissionColor, _Smoothness) so the
// content builder and the enemy telegraph glow (a MaterialPropertyBlock on _EmissionColor) work unchanged.
Shader "UnityEve/Toon"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _Smoothness ("Smoothness (hard specular above 0.3)", Range(0, 1)) = 0.1
        _Bands ("Light bands", Range(1, 6)) = 3
        _RimColor ("Rim", Color) = (0.35, 0.4, 0.55, 1)
        _OutlineWidth ("Outline width (px)", Range(0, 4)) = 1
        _OutlineColor ("Outline", Color) = (0.02, 0.02, 0.03, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _Smoothness;
            half _Bands;
            half4 _RimColor;
            half _OutlineWidth;
            half4 _OutlineColor;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Quantise delivered light energy into flat steps (the cel look). Energy is not clamped to 1,
            // so a bright lamp keeps its brightness: it just arrives in pools with hard edges.
            half Band(half energy)
            {
                return floor(max(energy, 0.0h) * _Bands + 0.5h) / _Bands;
            }

            half3 Shade(Light light, half3 n, half3 v)
            {
                half amount = saturate(dot(n, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
                half peak = max(max(light.color.r, light.color.g), max(light.color.b, 1e-3h));
                half3 c = light.color / peak * Band(amount * peak);
                // A hard specular dot on shiny things (metal, wet windows).
                half3 h = normalize(light.direction + v);
                half spec = step(0.3h, _Smoothness) * step(1.0h - 0.04h * (1.0h - _Smoothness), dot(n, h)) * step(0.5h, amount);
                return c + light.color * spec * 0.6h;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 n = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = v;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);

                half3 light = SampleSH(n);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS), i.positionWS, shadowMask);
                light += Shade(mainLight, n, v);

                #if defined(_ADDITIONAL_LIGHTS)
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                        light += Shade(GetAdditionalLight(dirIndex, i.positionWS, shadowMask), n, v);
                    #endif
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        light += Shade(GetAdditionalLight(lightIndex, i.positionWS, shadowMask), n, v);
                    LIGHT_LOOP_END
                #endif

                // Thin rim so silhouettes read against the night.
                half rim = step(0.72h, 1.0h - saturate(dot(n, v)));
                half3 colour = albedo.rgb * light + albedo.rgb * _RimColor.rgb * rim * 0.35h + _EmissionColor.rgb;
                colour = MixFog(colour, i.fogFactor);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            // Inverted hull: back faces pushed out along the normal by a fixed number of pixels.
            Name "ToonOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float4 pos = TransformObjectToHClip(v.positionOS.xyz);
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(v.normalOS));
                float2 dir = normalCS.xy;
                if (dot(dir, dir) > 1e-8) dir = normalize(dir);
                pos.xy += dir * (_OutlineWidth * 2.0 / _ScreenParams.xy) * pos.w;
                o.positionCS = pos;
                o.fogFactor = ComputeFogFactor(pos.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
