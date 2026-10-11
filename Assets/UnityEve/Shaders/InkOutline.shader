// Borderlands-style ink: a full-screen pass (URP Full Screen Pass Renderer Feature, before transparents)
// that finds silhouettes and creases from the depth and normals textures and draws them as soft-edged
// strokes. Stroke width is in 1080p pixels and scales with the screen, swells close to the camera and
// thins with distance, and a low-frequency wobble varies it along the line like a brush. Lines sink
// into the night fog with the geometry behind them.
Shader "UnityEve/InkOutline"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.03, 0.025, 0.04, 1)
        _Thickness ("Stroke width (px at 1080p)", Range(0.5, 6)) = 3.4
        _DepthThreshold ("Depth edge (fraction of distance)", Range(0.005, 0.5)) = 0.06
        _NormalThreshold ("Crease (1 - cos angle)", Range(0.05, 1)) = 0.3
        _Wobble ("Brush wobble", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "Ink"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _InkColor;
            float _Thickness;
            float _DepthThreshold;
            float _NormalThreshold;
            float _Wobble;

            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 0..1 edge strength sampling a ring of 8 neighbours at the given pixel radius.
            float Edge(float2 uv, float radiusPx, float centreDepth, float3 centreNormal)
            {
                float2 texel = radiusPx / _ScreenParams.xy;
                const float2 dirs[8] =
                {
                    float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                    float2(0.7071, 0.7071), float2(-0.7071, 0.7071), float2(0.7071, -0.7071), float2(-0.7071, -0.7071)
                };
                float depthEdge = 0.0;
                float normalEdge = 0.0;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float2 suv = uv + dirs[k] * texel;
                    float d = EyeDepth(suv);
                    // Only the nearer side of a silhouette draws, so strokes hug the object in front.
                    float gap = (d - centreDepth) / max(centreDepth, 0.1);
                    depthEdge = max(depthEdge, smoothstep(_DepthThreshold, _DepthThreshold * 2.0, gap));
                    float3 n = SampleSceneNormals(suv);
                    normalEdge = max(normalEdge, smoothstep(_NormalThreshold, _NormalThreshold * 1.6, 1.0 - dot(n, centreNormal)));
                }
                return max(depthEdge, normalEdge * 0.85);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 colour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float depth = EyeDepth(uv);
                float3 normal = SampleSceneNormals(uv);
                float sky = step(_ProjectionParams.z * 0.98, depth);

                // Width: screen-scaled, heavier up close, wobbling along the stroke.
                float scale = _ScreenParams.y / 1080.0;
                float nearness = lerp(1.45, 0.55, saturate(depth / 22.0));
                float2 brushUv = uv * float2(_ScreenParams.x / _ScreenParams.y, 1.0) * 26.0;
                float wobble = lerp(1.0, 0.45 + 1.1 * ValueNoise(brushUv), _Wobble);
                float radius = max(0.75, _Thickness * scale * nearness * wobble);

                // Two rings (full and half radius) give a solid core with a soft, anti-aliased rim.
                float edge = max(Edge(uv, radius, depth, normal), Edge(uv, radius * 0.5, depth, normal));
                edge = max(edge, 0.5 * Edge(uv, radius * 1.35, depth, normal));
                edge *= 1.0 - sky;

                // Sink into exp2 fog with the surface.
                float f = unity_FogParams.x * depth;
                edge *= saturate(exp2(-f * f));

                colour.rgb = lerp(colour.rgb, _InkColor.rgb, saturate(edge) * _InkColor.a);
                return colour;
            }
            ENDHLSL
        }
    }
}
