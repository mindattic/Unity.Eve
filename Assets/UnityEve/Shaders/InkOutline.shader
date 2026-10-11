// Borderlands-style ink: a full-screen pass (URP Full Screen Pass Renderer Feature, before transparents)
// that draws clean, even strokes at silhouettes and creases.
// - Silhouettes come from the second difference of 1/depth across opposite neighbours. 1/depth is
//   linear in screen space on any plane, so flat floors and walls stay clean however grazing the angle;
//   only real steps in depth ink, and only on the nearer side, so strokes hug the object in front.
// - Creases come from the normals texture.
// Stroke width is in 1080p pixels, scales with the screen, tapers gently with distance, and the stroke
// is anti-aliased by sampling a full and a half radius. Lines sink into the night fog with the surface.
Shader "UnityEve/InkOutline"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.03, 0.025, 0.04, 1)
        _Thickness ("Stroke width (px at 1080p)", Range(0.5, 6)) = 1.1
        _DepthThreshold ("Silhouette sensitivity", Range(0.005, 0.5)) = 0.04
        _NormalThreshold ("Crease (1 - cos angle)", Range(0.05, 1)) = 0.35
        _FadeDistance ("Lines fade out by (m)", Float) = 40
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
            float _FadeDistance;

            float InvDepth(float2 uv)
            {
                return 1.0 / max(LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams), 1e-3);
            }

            // 0..1 edge strength from four opposite-neighbour pairs at the given pixel radius.
            float Edge(float2 uv, float radiusPx, float invC, float3 nC)
            {
                float2 texel = radiusPx / _ScreenParams.xy;
                const float2 dirs[4] = { float2(1, 0), float2(0, 1), float2(0.7071, 0.7071), float2(0.7071, -0.7071) };
                float silhouette = 0.0;
                float crease = 0.0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float2 o = dirs[k] * texel;
                    float invA = InvDepth(uv + o);
                    float invB = InvDepth(uv - o);
                    // Negative curvature of 1/depth: this pixel sits on a nearer surface than the line
                    // through its neighbours, i.e. the front side of a depth step. Zero on any plane.
                    float bulge = (2.0 * invC - invA - invB) / invC;
                    silhouette = max(silhouette, smoothstep(_DepthThreshold, _DepthThreshold * 2.0, bulge));
                    float3 nA = SampleSceneNormals(uv + o);
                    float3 nB = SampleSceneNormals(uv - o);
                    float turn = max(1.0 - dot(nA, nC), 1.0 - dot(nB, nC));
                    crease = max(crease, smoothstep(_NormalThreshold, _NormalThreshold * 1.5, turn));
                }
                return max(silhouette, crease);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 colour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float depth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                if (depth > _ProjectionParams.z * 0.98) return colour; // empty sky: nothing to ink
                float invC = 1.0 / max(depth, 1e-3);
                float3 nC = SampleSceneNormals(uv);

                float scale = _ScreenParams.y / 1080.0;
                // A fine pen line: about a pixel wide at 1080p, a touch finer in the distance.
                float radius = max(0.6, _Thickness * scale * lerp(1.0, 0.75, saturate(depth / 25.0)));
                float edge = max(Edge(uv, radius * 0.6, invC, nC), 0.8 * Edge(uv, radius, invC, nC));

                float f = unity_FogParams.x * depth;
                edge *= saturate(exp2(-f * f)) * saturate((_FadeDistance - depth) / (_FadeDistance * 0.3));

                colour.rgb = lerp(colour.rgb, _InkColor.rgb, saturate(edge) * _InkColor.a);
                return colour;
            }
            ENDHLSL
        }
    }
}
