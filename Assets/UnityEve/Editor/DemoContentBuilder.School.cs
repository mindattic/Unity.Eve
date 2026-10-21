using UnityEngine;
using Unity.Eve.Battle;
using Unity.Eve.Cameras;
using Unity.Eve.Interaction;

namespace Unity.Eve.Editor
{
    /// <summary>
    /// Ashgrove School, through the gap left by the Market Square's old south-east wall: a fenced
    /// schoolyard with a jungle gym and a backboard, facing the shuttered school building. A second new
    /// area (owner direction 2026-10-11, after Ashgrove Park), home to the two new alien types.
    /// </summary>
    public static partial class DemoContentBuilder
    {
        private static void BuildSchool(SceneKit k)
        {
            var c = k.Content;
            // Already "GroundEast" (Market.cs); a thin connecting path from the square's south-east gap
            // down into the fenced yard.
            Block(k.Level, "SchoolPath", new Vector3(62f, 0f, -20f), new Vector3(68f, 0.02f, -14f), k.Sidewalk, false);
            Block(k.Level, "SchoolYard", new Vector3(62f, 0f, -50f), new Vector3(88f, 0.02f, -20f), k.Sidewalk, false);

            Facade(k, "SchoolBuilding", new Vector3(64f, 0f, -58f), new Vector3(86f, 7f, -50f), k.Brick, 21, faceSouth: false);
            // Steps and doors sit on the yard side of the north wall (the building's volume fills z < -50).
            Block(k.Level, "SchoolSteps", new Vector3(72f, 0.02f, -50f), new Vector3(78f, 0.2f, -49.7f), k.Plaster);
            Block(k.Level, "SchoolDoors", new Vector3(73.9f, 0.02f, -50.05f), new Vector3(76.1f, 2.6f, -49.95f), k.DoorMat, false);
            // Yaw 180: the yard camera looks south at the sign from its +Z side, not its default -Z face.
            SignText(k.Level, "KINGSPORT ELEMENTARY", new Vector3(75f, 5.2f, -49.9f), 180f, 0.5f, new Color(0.9f, 0.85f, 0.7f));

            // A chain-link boundary either side of the yard (the building closes the south edge).
            Block(k.Level, "SchoolFenceW", new Vector3(62f, 0f, -50f), new Vector3(62.15f, 1.8f, -20f), k.Metal).AddComponent<CoverProp>();
            Block(k.Level, "SchoolFenceE", new Vector3(87.85f, 0f, -50f), new Vector3(88f, 1.8f, -20f), k.Metal).AddComponent<CoverProp>();

            // Jungle gym: a simple bar frame, climbed over or ducked behind.
            var gym = new GameObject("JungleGym").transform;
            gym.SetParent(k.Level);
            gym.position = new Vector3(68f, 0f, -30f);
            void Bar(Vector3 min, Vector3 max) => LocalBox(gym, "Bar", min, max, k.Metal);
            foreach (var (dx, dz) in new[] { (-1.4f, -1f), (1.4f, -1f), (-1.4f, 1f), (1.4f, 1f) })
                Bar(new Vector3(dx - 0.05f, 0f, dz - 0.05f), new Vector3(dx + 0.05f, 1.8f, dz + 0.05f));
            Bar(new Vector3(-1.45f, 1.7f, -1.05f), new Vector3(1.45f, 1.85f, -0.95f));
            Bar(new Vector3(-1.45f, 1.7f, 0.95f), new Vector3(1.45f, 1.85f, 1.05f));
            for (var x = -1.2f; x <= 1.2f; x += 0.6f)
                Bar(new Vector3(x - 0.04f, 1.7f, -1f), new Vector3(x + 0.04f, 1.85f, 1f));
            gym.gameObject.AddComponent<CoverProp>();

            // A backboard on its pole, and a flagpole.
            var hoopAt = new Vector3(81f, 0f, -42f);
            Block(k.Level, "HoopPole", hoopAt + new Vector3(-0.08f, 0f, -0.08f), hoopAt + new Vector3(0.08f, 3.2f, 0.08f), k.Metal).AddComponent<CoverProp>();
            Block(k.Level, "Backboard", hoopAt + new Vector3(-0.6f, 2.6f, 0.05f), hoopAt + new Vector3(0.6f, 3.3f, 0.12f), k.Trim, false);
            var flagAt = new Vector3(65f, 0f, -24f);
            Block(k.Level, "Flagpole", flagAt + new Vector3(-0.06f, 0f, -0.06f), flagAt + new Vector3(0.06f, 5.5f, 0.06f), k.Metal).AddComponent<CoverProp>();

            Bench(k, new Vector3(73f, 0.02f, -24f), Vector3.back);
            Bench(k, new Vector3(78f, 0.02f, -34f), Vector3.left);
            Tree(k, new Vector3(64f, 0.02f, -46f), 5.3f, 111);
            Tree(k, new Vector3(85f, 0.02f, -24f), 5.6f, 112);
            StreetLamp(k, new Vector3(64f, 0.02f, -22f), 135f);
            StreetLamp(k, new Vector3(85f, 0.02f, -48f), 315f);

            var brickPile = Marker(k, "Pickup_Brick", new Vector3(83f, 0.02f, -46f), 0f);
            Attach<Pickup>(brickPile, new Vector3(83f, 0.02f, -45f), 1.3f, 0.2f).Configure(c.Items["brick"], 3, Glint(k, brickPile.transform));

            // ---- Encounter and screen.
            Encounter(k, "11_School", "Ashgrove School", new Vector3(63f, 0f, -48f), new Vector3(87f, 0f, -22f),
                ("warden", new Vector3(2f, 0f, -3f)), ("spitter", new Vector3(-4f, 0f, 4f)), ("crawler", new Vector3(4f, 0f, 2f)));

            var schoolShot = Shot(k, "School", new Vector3(75f, 6.5f, -16f), new Vector3(75f, 0.5f, -35f), ShotMode.Fixed, 58f);
            Zone(k, "School", new Vector3(62f, 0, -52f), new Vector3(88f, 0, -18f), schoolShot);
            Tour(k, "28_school", new Vector3(75f, 0.02f, -24f), 180f);
        }
    }
}
