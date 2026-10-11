// The weapon range dome: a semi-transparent shell of hexagon cells (Parasite Eve / Parasite Mutant style).
// The lattice is laid out in spherical coordinates on the unit sphere primitive with cells a fixed size in
// metres; a fresnel edge outlines the silhouette, a scan band sweeps up and a few cells twinkle. Shell
// close to the lens fades out so a fixed camera near the dome is not curtained, and a second, faint pass
// draws the shell behind walls so range still reads in rooms smaller than the weapon's reach.
// Animates on _UnscaledTime (set by RangeDome) so it keeps moving while the world is paused.
Shader "UnityEve/HexDome"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.33, 1, 0.73, 1)
        _FillAlpha ("Fill alpha", Range(0, 1)) = 0.025
        _LineAlpha ("Line alpha", Range(0, 1)) = 0.3
        _RimAlpha ("Fresnel alpha", Range(0, 1)) = 0.3
        _HiddenAlpha ("Behind-wall strength", Range(0, 1)) = 0.35
        _CellSize ("Hex size (m)", Float) = 0.8
        _LineWidth ("Line width", Range(0.01, 0.3)) = 0.045
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _FillAlpha;
            half _LineAlpha;
            half _RimAlpha;
            half _HiddenAlpha;
            float _CellSize;
            half _LineWidth;
        CBUFFER_END

        float _UnscaledTime;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 dirOS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
        };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS = p.positionCS;
            o.positionWS = p.positionWS;
            o.dirOS = v.positionOS.xyz;
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            return o;
        }

        float Hash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        // Returns (offset within the nearest hex cell, that cell's id).
        float4 HexCell(float2 uv)
        {
            const float2 r = float2(1.0, 1.7320508);
            const float2 h = r * 0.5;
            float2 a = uv - r * floor(uv / r) - h;
            float2 b = (uv - h) - r * floor((uv - h) / r) - h;
            float2 gv = dot(a, a) < dot(b, b) ? a : b;
            return float4(gv, uv - gv);
        }

        half4 Dome(Varyings i, float strength)
        {
            float3 d = normalize(i.dirOS);
            // Cells stay a fixed size in metres, whatever the weapon's range.
            float radius = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20)) * 0.5;
            float cells = max(12.0, round(2.0 * PI * radius / _CellSize));
            float azimuth = atan2(d.z, d.x) / (2.0 * PI) + 0.5;
            float latitude = asin(clamp(d.y, -1.0, 1.0));
            float2 uv = float2(azimuth * cells, latitude * cells / (2.0 * PI) * 1.1547);

            float4 cell = HexCell(uv);
            float2 g = abs(cell.xy);
            float edge = 0.5 - max(dot(g, normalize(float2(1.0, 1.7320508))), g.x);
            float aa = max(fwidth(edge), 1e-4);
            float lattice = 1.0 - smoothstep(_LineWidth - aa, _LineWidth + aa, edge);
            // Cells fade out toward the cap, where the lattice pinches.
            lattice *= saturate((1.0 - d.y) * 3.0);

            float t = _UnscaledTime;
            float twinkle = step(0.93, Hash(cell.zw + floor(t * 2.0))) * 0.15;
            float scan = saturate(1.0 - abs(frac(t * 0.45) - saturate(d.y)) * 9.0);

            float3 n = normalize(i.normalWS);
            float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
            float fresnel = pow(1.0 - abs(dot(n, v)), 3.0);

            // From inside the shell (a fixed camera within range) the dome must not curtain the view.
            float3 centre = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
            float inside = step(distance(_WorldSpaceCameraPos, centre), radius);
            fresnel *= 1.0 - inside;

            float alpha = _FillAlpha + twinkle * 0.2 + lattice * _LineAlpha * (0.6 + 0.4 * scan) + fresnel * _RimAlpha + scan * 0.04;
            // Hide the lower half (the dome sits on the ground) and shell right in front of the lens.
            alpha *= step(-0.02, d.y) * lerp(1.0, 0.6, inside);
            alpha *= saturate((distance(_WorldSpaceCameraPos, i.positionWS) - 1.0) / 3.0);
            half3 colour = _BaseColor.rgb * (1.0 + lattice * 0.6 + scan * 0.5);
            return half4(colour, saturate(alpha * strength));
        }
        ENDHLSL

        Pass
        {
            // The shell where it is not occluded.
            Name "HexDome"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target { return Dome(i, 1.0); }
            ENDHLSL
        }

        Pass
        {
            // The shell behind walls and floors, faint, so range reads in tight rooms.
            Name "HexDomeHidden"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target { return Dome(i, _HiddenAlpha); }
            ENDHLSL
        }
    }
}
