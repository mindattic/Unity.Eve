using UnityEngine;
using UnityEve.Cameras;

namespace UnityEve.Editor
{
    /// <summary>
    /// Ashgrove Park, through the gap behind Saint Ansel's: a cel-shaded fountain pool (UnityEve/ToonWater),
    /// trees and benches around it. A first, self-contained district to prove out the look before the rest
    /// of Kingsport grows (owner direction 2026-10-11: one new area, verified, before committing to more).
    /// </summary>
    public static partial class DemoContentBuilder
    {
        private static void BuildPark(SceneKit k)
        {
            // The ground here is already "GroundEast" (Market.cs); this is a thin connecting path from the
            // square's opening (see the removed SquareWallNE) up to the park's fountain plaza.
            Block(k.Level, "ParkPath", new Vector3(62f, 0f, 8f), new Vector3(68f, 0.02f, 14f), k.Sidewalk, false);
            Block(k.Level, "ParkPlaza", new Vector3(68f, 0f, 20f), new Vector3(84f, 0.02f, 38f), k.Sidewalk, false);

            // The fountain: a working pool this time, not the Market Square's dry sludge.
            var fountain = new GameObject("ParkFountain").transform;
            fountain.SetParent(k.Level);
            void Cyl(string name, Vector3 at, float radius, float height, Material m, bool collide)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(fountain);
                go.transform.position = at + Vector3.up * (height * 0.5f);
                go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
                go.GetComponent<Renderer>().sharedMaterial = m;
                if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            var water = ToonWaterMaterial("ParkFountainWater", new Color(0.1f, 0.3f, 0.36f, 0.85f), new Color(0.55f, 0.85f, 0.85f, 0.9f));
            var centre = new Vector3(76f, 0.06f, 29f);
            Cyl("Basin", centre, 3.1f, 0.6f, k.Plaster, true);
            Cyl("Water", centre, 2.85f, 0.64f, water, false);
            Cyl("Pillar", centre, 0.4f, 2.3f, k.Plaster, true);
            Cyl("Bowl", centre + Vector3.up * 2.3f, 1.1f, 0.3f, k.Plaster, false);
            Cyl("BowlWater", centre + Vector3.up * 2.3f, 0.95f, 0.32f, water, false);
            PointLight(k, "ParkFountainGlow", centre + Vector3.up * 1f, new Color(0.5f, 0.85f, 0.95f), 3.5f, 7f, flicker: true);

            // Benches ring the fountain; trees mark the plaza's edge.
            Bench(k, centre + new Vector3(-4.2f, 0f, 0f), Vector3.right);
            Bench(k, centre + new Vector3(4.2f, 0f, 0f), Vector3.left);
            Bench(k, centre + new Vector3(0f, 0f, -4.2f), Vector3.forward);
            Bench(k, centre + new Vector3(0f, 0f, 4.2f), Vector3.back);
            Tree(k, new Vector3(69f, 0.02f, 22f), 5.2f, 101);
            Tree(k, new Vector3(83f, 0.02f, 22f), 5.8f, 102);
            Tree(k, new Vector3(69f, 0.02f, 36f), 5.5f, 103);
            Tree(k, new Vector3(83f, 0.02f, 36f), 5f, 104);
            Tree(k, new Vector3(76f, 0.02f, 42f), 6f, 105);
            StreetLamp(k, new Vector3(65f, 0.02f, 32f), 90f);
            StreetLamp(k, new Vector3(87f, 0.02f, 32f), 270f);

            // ---- Screen.
            var parkShot = Shot(k, "Park", new Vector3(76f, 6.5f, 15f), new Vector3(76f, 0.5f, 29f), ShotMode.Fixed, 55f);
            Zone(k, "Park", new Vector3(62f, 0, 8f), new Vector3(90f, 0, 50f), parkShot);
            Tour(k, "16_park", new Vector3(76f, 0.06f, 16f), 0f);
        }
    }
}
