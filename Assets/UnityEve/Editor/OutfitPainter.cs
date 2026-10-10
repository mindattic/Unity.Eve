using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityEve.Editor
{
    /// <summary>
    /// Ported from ExperimentEve (babylon/src/player/outfit.ts). The CC0 base character ships a body
    /// with no clothes, and the only free outfit pack is medieval. So her clothes are painted rather
    /// than modelled: each triangle is classified by height up the figure and distance out from the
    /// spine, then rasterized into a new albedo in UV space with the colour that region should be.
    /// Baked into the texture she skins with, it deforms with every animation and costs nothing at runtime.
    /// </summary>
    public static class OutfitPainter
    {
        private const int Size = 1024;

        private readonly struct Region
        {
            public readonly float From, To, MaxSpread;
            public readonly Color32 Color;
            public readonly int Grain;

            public Region(float from, float to, byte r, byte g, byte b, int grain, float maxSpread = float.MaxValue)
            {
                From = from;
                To = to;
                Color = new Color32(r, g, b, 255);
                Grain = grain;
                MaxSpread = maxSpread;
            }
        }

        // Kat: work boots, jeans, belt, a dark tank top, bare arms.
        private static readonly Region[] Outfit =
        {
            new Region(-0.01f, 0.075f, 26, 22, 20, 10),          // boots
            new Region(0.075f, 0.10f, 38, 32, 28, 8),            // boot cuff
            new Region(0.10f, 0.475f, 46, 58, 84, 16),           // jeans
            new Region(0.475f, 0.50f, 34, 30, 30, 8),            // belt
            new Region(0.50f, 0.805f, 40, 42, 46, 12, 0.19f),    // tank: the spread limit keeps it off the arms
        };

        private static readonly Color32 Skin = new Color32(176, 132, 104, 255);

        /// <returns>The saved texture asset.</returns>
        public static Texture2D Paint(Mesh mesh, string assetPath)
        {
            var positions = mesh.vertices;
            var uvs = mesh.uv;
            var tris = mesh.triangles;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            foreach (var p in positions)
            {
                minY = Math.Min(minY, p.y);
                maxY = Math.Max(maxY, p.y);
            }
            var height = Math.Max(0.001f, maxY - minY);

            var pixels = new Color32[Size * Size];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = Skin;

            uint seed = 90210;
            for (var t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                var centre = (positions[a] + positions[b] + positions[c]) / 3f;
                var h = (centre.y - minY) / height;
                var spread = new Vector2(centre.x, centre.z).magnitude / height;
                var region = -1;
                for (var r = 0; r < Outfit.Length; r++)
                    if (h >= Outfit[r].From && h < Outfit[r].To && spread <= Outfit[r].MaxSpread)
                    {
                        region = r;
                        break;
                    }
                if (region < 0) continue;

                seed = seed * 1103515245u + 12345u;
                var jitter = ((seed >> 8) / (float)(1 << 24) - 0.5f) * Outfit[region].Grain;
                var col = Outfit[region].Color;
                var shaded = new Color32(Clamp(col.r + jitter), Clamp(col.g + jitter), Clamp(col.b + jitter), 255);
                FillTriangle(pixels, uvs[a] * Size, uvs[b] * Size, uvs[c] * Size, shaded);
            }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static byte Clamp(float v) => (byte)Math.Max(0, Math.Min(255, Math.Round(v)));

        /// <summary>Barycentric fill, padded by a pixel so neighbouring triangles leave no seams.</summary>
        private static void FillTriangle(Color32[] px, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            var minX = Math.Max(0, (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x))) - 1);
            var maxX = Math.Min(Size - 1, (int)Math.Ceiling(Math.Max(a.x, Math.Max(b.x, c.x))) + 1);
            var minY = Math.Max(0, (int)Math.Floor(Math.Min(a.y, Math.Min(b.y, c.y))) - 1);
            var maxY = Math.Min(Size - 1, (int)Math.Ceiling(Math.Max(a.y, Math.Max(b.y, c.y))) + 1);
            var area = Edge(a, b, c);
            if (Math.Abs(area) < 1e-6f) return;

            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var w0 = Edge(b, c, p) / area;
                var w1 = Edge(c, a, p) / area;
                var w2 = Edge(a, b, p) / area;
                // A small overreach past each edge closes the hairline seams between neighbours.
                const float eps = -0.08f;
                if (w0 >= eps && w1 >= eps && w2 >= eps) px[y * Size + x] = color;
            }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }
}
